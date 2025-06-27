using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.SymbolStorage.Impl.Logger;
using JetBrains.SymbolStorage.Impl.Storages;
using JetBrains.SymbolStorage.Impl.Tags;

namespace JetBrains.SymbolStorage.Impl.Commands
{
  internal sealed class UploadCommand : IStatsReportingCommand
  {
    private readonly ILogger myLogger;
    private readonly IStorage mySourceStorage;
    private readonly StorageFormat myNewStorageFormat;
    private readonly IStorage myTargetStorage;
    private readonly CollisionResolutionMode myCollisionResolutionMode;
    private readonly CollisionResolutionMode myPeCollisionResolutionMode;
    private readonly string? myBackupStorageDir;
    private readonly int myDegreeOfParallelism;
    private long mySubOpsCount;

    public UploadCommand(
      ILogger logger,
      IStorage targetStorage,
      int degreeOfParallelism,
      IStorage sourceStorage,
      StorageFormat newStorageFormat,
      CollisionResolutionMode collisionResolutionMode,
      CollisionResolutionMode peCollisionResolutionMode,
      string? backupStorageDir)
    {
      if ((collisionResolutionMode == CollisionResolutionMode.Overwrite || peCollisionResolutionMode == CollisionResolutionMode.Overwrite) && string.IsNullOrEmpty(backupStorageDir))
        throw new ArgumentException("Backup storage must be specified when collision resolution mode is 'overwrite'");

      myLogger = logger ?? throw new ArgumentNullException(nameof(logger));
      myTargetStorage = targetStorage ?? throw new ArgumentNullException(nameof(targetStorage));
      myDegreeOfParallelism = degreeOfParallelism;
      mySourceStorage = sourceStorage ?? throw new ArgumentNullException(nameof(sourceStorage));
      myNewStorageFormat = newStorageFormat;
      myCollisionResolutionMode = collisionResolutionMode;
      myPeCollisionResolutionMode = peCollisionResolutionMode;
      myBackupStorageDir = (collisionResolutionMode == CollisionResolutionMode.Overwrite || peCollisionResolutionMode == CollisionResolutionMode.Overwrite) ? backupStorageDir : null;
    }

    public long SubOperationsCount => Volatile.Read(ref mySubOpsCount);
    
    public async Task<int> ExecuteAsync()
    {
      Volatile.Write(ref mySubOpsCount, 0);

      var (srcFiles, isValid) = await ValidateAndLoadFilesListFromStorage(mySourceStorage);
      Volatile.Write(ref mySubOpsCount, srcFiles.Count);
      if (!isValid)
      {
        myLogger.Error("Found some issues in source storage, uploading was interrupted");
        return 1;
      }

      (var uploadFiles, isValid) = await BuildFilesListForUploading(mySourceStorage, srcFiles);
      if (!isValid)
      {
        myLogger.Error("Found some issues in source storage, uploading was interrupted");
        return 1;
      }

      await UploadFiles(mySourceStorage, uploadFiles);
      return 0;
    }

    /// <summary>
    /// Loads list of files from <paramref name="srcStorage"/> and validates, that everything is fine.
    /// </summary>
    /// <param name="srcStorage">Source storage</param>
    /// <returns>List of files and a flag that everything is OK</returns>
    private async Task<(List<SymbolStoragePath> files, bool valid)> ValidateAndLoadFilesListFromStorage(IStorage srcStorage)
    {
      var validator = new StorageManager(myLogger, srcStorage, "Source");
      var srcStorageFormat = await validator.ValidateStorageMarkersAsync();

      var tagItems = await validator.LoadTagItemsAsync(myDegreeOfParallelism);
      validator.DumpProducts(tagItems);
      validator.DumpProperties(tagItems);
      var (files, totalSize) = await validator.GatherDataFilesAsync();
      var (statistics, _) = await validator.ValidateAndFixAsync(myDegreeOfParallelism, tagItems, files, srcStorageFormat, StorageManager.ValidateMode.Validate);
      myLogger.Info($"[{DateTime.Now:s}] Done with source validation (size: {totalSize.ToKibibyte()}, files: {files.Count + tagItems.Count}, warnings: {statistics.Warnings}, errors: {statistics.Errors})");

      var srcFiles = new List<SymbolStoragePath>(tagItems.Count + files.Count);
      srcFiles.AddRange(tagItems.Select(x => x.TagFile));
      srcFiles.AddRange(files);
      return (srcFiles, !statistics.HasProblems);
    }

    /// <summary>
    /// Build list of pairs (source file -> destination file) to upload to destination storage.
    /// Additionally do validation and resolve file name collisions.
    /// </summary>
    /// <param name="srcStorage">Source storage</param>
    /// <param name="srcFiles">Files from source storage</param>
    /// <returns>List of files to upload + validation result</returns>
    private async Task<(List<(SymbolStoragePath src, SymbolStoragePath dst)> srcDstPairs, bool valid)> BuildFilesListForUploading(IStorage srcStorage, List<SymbolStoragePath> srcFiles)
    {
      var dstValidator = new StorageManager(myLogger, myTargetStorage, "Destination");
      var dstStorageFormat = await dstValidator.CreateOrValidateStorageMarkersAsync(myNewStorageFormat);

      myLogger.Info($"[{DateTime.Now:s}] Checking file compatibility...");
      var uploadFiles = new List<(SymbolStoragePath src, SymbolStoragePath dst)>(srcFiles.Count);
      var uploadFilesSync = new Lock();

      var statistics = new Statistics();
      ILogger logger = new LoggerWithStatistics(myLogger, statistics);

      using var backupStorage = !string.IsNullOrEmpty(myBackupStorageDir) ? new FileSystemStorage(myBackupStorageDir) : null;

      long existFiles = 0;
      long collisionFiles = 0;
      await srcFiles.ParallelForAsync(myDegreeOfParallelism, async srcFile =>
      {
        logger.Verbose($"  Checking {srcFile}");
        var dstFile = srcFile;
        if (TagUtil.IsDataFile(srcFile))
        {
          switch (srcFile.ValidateAndFixDataPath(dstStorageFormat, out var fixedFile))
          {
            case PathUtil.ValidateAndFixErrors.Ok:
              break;
            case PathUtil.ValidateAndFixErrors.CanBeFixed:
              dstFile = fixedFile;
              break;
            case PathUtil.ValidateAndFixErrors.Error:
              logger.Error($"The source file name ({srcFile}) cannot be translated to the destination storage file format");
              return;
            default:
              throw new InvalidOperationException("Unknown ValidateAndFixErrors value");
          }
        }

        if (await myTargetStorage.ExistsAsync(dstFile))
        {
          bool isSameFile = false;
          var dstLen = await myTargetStorage.GetLengthAsync(dstFile);
          var srcLen = await srcStorage.GetLengthAsync(srcFile);
          if (dstLen == srcLen)
          {
            using var hash = SHA256.Create();
            var dstHash = await myTargetStorage.OpenForReadingAsync(dstFile, stream => hash.ComputeHashAsync(stream));
            var srcHash = await srcStorage.OpenForReadingAsync(srcFile, stream => hash.ComputeHashAsync(stream));
            if (srcHash.SequenceEqual(dstHash))
            {
              Interlocked.Increment(ref existFiles);
              isSameFile = true;
            }
          }

          if (!isSameFile)
          {
            // Collision resolution logic
            Interlocked.Increment(ref collisionFiles);
            if (await ProcessFileCollision(logger, myTargetStorage, backupStorage, srcFile, dstFile))
            {
              lock (uploadFilesSync)
              {
                // Storage overwrites existed files
                uploadFiles.Add((srcFile, dstFile));
              }
            }
          }
        }
        else
        {
          lock (uploadFilesSync)
          {
            uploadFiles.Add((srcFile, dstFile));
          }
        }
      });

      myLogger.Info($"[{DateTime.Now:s}] Done with compatibility (new files: {uploadFiles.Count}, same files: {existFiles}, collisions: {collisionFiles}, warnings: {statistics.Warnings}, errors: {statistics.Errors})");
      return (uploadFiles, !statistics.HasProblems);
    }

    /// <summary>
    /// Collision resolution logic
    /// </summary>
    private async Task<bool> ProcessFileCollision(ILogger logger, IStorage dstStorage, IStorage? backupStorage, SymbolStoragePath srcFile, SymbolStoragePath dstFile)
    {
      var resolutionMode = srcFile.IsPeFileWithWeakHash() ? myPeCollisionResolutionMode : myCollisionResolutionMode;
      switch (resolutionMode)
      {
        case CollisionResolutionMode.Terminate:
          logger.Error($"The source file {srcFile} differs from the destination. Processing will be terminated.");
          return false;
        case CollisionResolutionMode.KeepExisted:
          logger.Fix($"The source file {srcFile} differs from the destination. Preserve existed file.");
          return false;
        case CollisionResolutionMode.Overwrite when backupStorage != null:
          logger.Fix($"The source file {srcFile} differs from the destination. File will be overwritten, backup will be created.");
          // Assume that there is no collisions most of the time and for rare circumstances it is ok to re-read file from destination storage
          await dstStorage.OpenForReadingAsync(dstFile, async stream => { await backupStorage.CreateForWritingAsync(dstFile, AccessMode.Public, stream); });
          // Storage overwrites existed files, so this will work as expected
          return true;
        case CollisionResolutionMode.Overwrite when backupStorage == null:
        case CollisionResolutionMode.OverwriteWithoutBackup:
          logger.Fix($"The source file {srcFile} differs from the destination. File will be overwritten without backup.");
          // Storage overwrites existed files, so this will work as expected
          return true;
        default:
          throw new InvalidOperationException("Unknown CollisionResolutionMode value: " + resolutionMode);
      }
    }
    
    /// <summary>
    /// Uploads all required files from source storage to destination storage
    /// </summary>
    /// <param name="srcStorage">Source storage</param>
    /// <param name="uploadFiles">Files to be uploaded</param>
    private async Task UploadFiles(IStorage srcStorage, List<(SymbolStoragePath src, SymbolStoragePath dst)> uploadFiles)
    {
      if (uploadFiles.Count <= 0)
        return;

      myLogger.Info($"[{DateTime.Now:s}] Uploading...");
      long totalSize = 0;
      await uploadFiles.ParallelForAsync(myDegreeOfParallelism, async item =>
      {
        var (srcFile, dstFile) = item;
        myLogger.Info($"  Uploading {srcFile}");
        using var memoryStream = new MemoryStream();
        await srcStorage.OpenForReadingAsync(srcFile, stream => stream.CopyToAsync(memoryStream));
        await myTargetStorage.CreateForWritingAsync(dstFile, TagUtil.IsTagFile(dstFile) ? AccessMode.Private : AccessMode.Public, memoryStream);
        Interlocked.Add(ref totalSize, memoryStream.Length);
      });

      await myTargetStorage.InvalidateExternalServicesAsync();
      myLogger.Info($"[{DateTime.Now:s}] Done with uploading (size: {totalSize.ToKibibyte()}, files: {uploadFiles.Count})");
    }
  }
}