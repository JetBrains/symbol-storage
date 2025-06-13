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
  internal sealed class UploadCommand : ICommand
  {
    private readonly ILogger myLogger;
    private readonly string mySource;
    private readonly StorageFormat myNewStorageFormat;
    private readonly IStorage myStorage;
    private readonly CollisionResolutionMode myCollisionResolutionMode;
    private readonly CollisionResolutionMode myPeCollisionResolutionMode;
    private readonly string? myBackupStorageDir;
    private readonly int myDegreeOfParallelism;

    public UploadCommand(
      ILogger logger,
      IStorage storage,
      int degreeOfParallelism,
      string source,
      StorageFormat newStorageFormat,
      CollisionResolutionMode collisionResolutionMode,
      CollisionResolutionMode peCollisionResolutionMode,
      string? backupStorageDir)
    {
      if ((collisionResolutionMode == CollisionResolutionMode.Overwrite || peCollisionResolutionMode == CollisionResolutionMode.Overwrite) && string.IsNullOrEmpty(backupStorageDir))
        throw new ArgumentException("Backup storage must be specified when collision resolution mode is 'overwrite'");

      myLogger = logger ?? throw new ArgumentNullException(nameof(logger));
      myStorage = storage ?? throw new ArgumentNullException(nameof(storage));
      myDegreeOfParallelism = degreeOfParallelism;
      mySource = source ?? throw new ArgumentNullException(nameof(source));
      myNewStorageFormat = newStorageFormat;
      myCollisionResolutionMode = collisionResolutionMode;
      myPeCollisionResolutionMode = peCollisionResolutionMode;
      myBackupStorageDir = (collisionResolutionMode == CollisionResolutionMode.Overwrite || peCollisionResolutionMode == CollisionResolutionMode.Overwrite) ? backupStorageDir : null;
    }

    public async Task<int> ExecuteAsync()
    {
      using var srcStorage = new FileSystemStorage(mySource);

      var (srcFiles, isValid) = await ValidateAndLoadFilesListFromStorage(srcStorage);
      if (!isValid)
      {
        myLogger.Error("Found some issues in source storage, uploading was interrupted");
        return 1;
      }

      (var uploadFiles, isValid) = await BuildFilesListForUploading(srcStorage, srcFiles);
      if (!isValid)
      {
        myLogger.Error("Found some issues in source storage, uploading was interrupted");
        return 1;
      }

      await UploadFiles(srcStorage, uploadFiles);
      return 0;
    }

    /// <summary>
    /// Loads list of files from <paramref name="srcStorage"/> and validates, that everything is fine.
    /// </summary>
    /// <param name="srcStorage">Source storage</param>
    /// <returns>List of files and a flag that everything is OK</returns>
    private async Task<(List<string> files, bool valid)> ValidateAndLoadFilesListFromStorage(IStorage srcStorage)
    {
      var validator = new Validator(myLogger, srcStorage, "Source");
      var srcStorageFormat = await validator.ValidateStorageMarkersAsync();

      var tagItems = await validator.LoadTagItemsAsync(myDegreeOfParallelism);
      validator.DumpProducts(tagItems);
      validator.DumpProperties(tagItems);
      var (totalSize, files) = await validator.GatherDataFilesAsync();
      var (statistics, _) = await validator.ValidateAsync(myDegreeOfParallelism, tagItems, files, srcStorageFormat, Validator.ValidateMode.Validate);
      myLogger.Info($"[{DateTime.Now:s}] Done with source validation (size: {totalSize.ToKibibyte()}, files: {files.Count + tagItems.Count}, warnings: {statistics.Warnings}, errors: {statistics.Errors})");

      var srcFiles = new List<string>(tagItems.Count + files.Count);
      srcFiles.AddRange(tagItems.Select(x => x.Key));
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
    private async Task<(List<(string src, string dst)> srcDstPairs, bool valid)> BuildFilesListForUploading(IStorage srcStorage, List<string> srcFiles)
    {
      var dstValidator = new Validator(myLogger, myStorage, "Destination");
      var dstStorageFormat = await dstValidator.CreateOrValidateStorageMarkersAsync(myNewStorageFormat);

      myLogger.Info($"[{DateTime.Now:s}] Checking file compatibility...");
      var uploadFiles = new List<(string src, string dst)>(srcFiles.Count);
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

        if (await myStorage.ExistsAsync(dstFile))
        {
          bool isSameFile = false;
          var dstLen = await myStorage.GetLengthAsync(dstFile);
          var srcLen = await srcStorage.GetLengthAsync(srcFile);
          if (dstLen == srcLen)
          {
            using var hash = SHA256.Create();
            var dstHash = await myStorage.OpenForReadingAsync(dstFile, stream => hash.ComputeHashAsync(stream));
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
            if (await ProcessFileCollision(logger, myStorage, backupStorage, srcFile, dstFile))
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
    private async Task<bool> ProcessFileCollision(ILogger logger, IStorage dstStorage, IStorage? backupStorage, string srcFile, string dstFile)
    {
      var resolutionMode = srcFile.IsPeWithWeakHashFile() ? myPeCollisionResolutionMode : myCollisionResolutionMode;
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
    private async Task UploadFiles(IStorage srcStorage, List<(string src, string dst)> uploadFiles)
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
        await myStorage.CreateForWritingAsync(dstFile, TagUtil.IsTagFile(dstFile) ? AccessMode.Private : AccessMode.Public, memoryStream);
        Interlocked.Add(ref totalSize, memoryStream.Length);
      });

      await myStorage.InvalidateExternalServicesAsync();
      myLogger.Info($"[{DateTime.Now:s}] Done with uploading (size: {totalSize.ToKibibyte()}, files: {uploadFiles.Count})");
    }
  }
}