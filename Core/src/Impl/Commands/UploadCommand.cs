#nullable enable

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
    private readonly string? myBackupStorage;
    private readonly int myDegreeOfParallelism;

    public UploadCommand(
      ILogger logger,
      IStorage storage,
      int degreeOfParallelism,
      string source,
      StorageFormat newStorageFormat,
      CollisionResolutionMode collisionResolutionMode,
      CollisionResolutionMode peCollisionResolutionMode,
      string? backupStorage)
    {
      if ((collisionResolutionMode == CollisionResolutionMode.Overwrite || peCollisionResolutionMode == CollisionResolutionMode.Overwrite) && string.IsNullOrEmpty(backupStorage))
        throw new ArgumentException("Backup storage must be specified when collision resolution mode is 'overwrite'");
      
      myLogger = logger ?? throw new ArgumentNullException(nameof(logger));
      myStorage = storage ?? throw new ArgumentNullException(nameof(storage));
      myDegreeOfParallelism = degreeOfParallelism;
      mySource = source ?? throw new ArgumentNullException(nameof(source));
      myNewStorageFormat = newStorageFormat;
      myCollisionResolutionMode = collisionResolutionMode;
      myPeCollisionResolutionMode = peCollisionResolutionMode;
      myBackupStorage = (collisionResolutionMode == CollisionResolutionMode.Overwrite || peCollisionResolutionMode == CollisionResolutionMode.Overwrite) ? backupStorage : null;
    }

    public async Task<int> ExecuteAsync()
    {
      using var srcStorage = new FileSystemStorage(mySource);

      List<string> srcFiles;
      {
        var validator = new Validator(myLogger, srcStorage, "Source");
        var srcStorageFormat = await validator.ValidateStorageMarkersAsync();

        var tagItems = await validator.LoadTagItemsAsync(myDegreeOfParallelism);
        validator.DumpProducts(tagItems);
        validator.DumpProperties(tagItems);
        var (totalSize, files) = await validator.GatherDataFilesAsync();
        var (statistics, _) = await validator.ValidateAsync(myDegreeOfParallelism, tagItems, files, srcStorageFormat, Validator.ValidateMode.Validate);
        myLogger.Info($"[{DateTime.Now:s}] Done with source validation (size: {totalSize.ToKibibyte()}, files: {files.Count + tagItems.Count}, warnings: {statistics.Warnings}, errors: {statistics.Errors})");
        if (statistics.HasProblems)
        {
          myLogger.Error("Found some issues in source storage, uploading was interrupted");
          return 1;
        }
        
        srcFiles = new List<string>(tagItems.Count + files.Count);
        srcFiles.AddRange(tagItems.Select(x => x.Key));
        srcFiles.AddRange(files);
      }

      var dstValidator = new Validator(myLogger, myStorage, "Destination");
      var dstStorageFormat = await dstValidator.CreateOrValidateStorageMarkersAsync(myNewStorageFormat);

      {
        myLogger.Info($"[{DateTime.Now:s}] Checking file compatibility...");
        var uploadFiles = new List<(string src, string dst)>(srcFiles.Count);
        var uploadFilesSync = new Lock();

        {
          var statistics = new Statistics();
          ILogger logger = new LoggerWithStatistics(myLogger, statistics);

          using var backupStorage = !string.IsNullOrEmpty(myBackupStorage) ? new FileSystemStorage(myBackupStorage) : null;

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
                  switch (myCollisionResolutionMode)
                  {
                    case CollisionResolutionMode.Terminate:
                      logger.Error($"The source file {srcFile} differs from the destination. Processing will be terminated.");
                      break;
                    case CollisionResolutionMode.KeepExisted:
                      logger.Fix($"The source file {srcFile} differs from the destination. Preserve existed file.");
                      break;
                    case CollisionResolutionMode.Overwrite when backupStorage != null:
                      logger.Fix($"The source file {srcFile} differs from the destination. File will be overwritten, backup will be created.");
                      // Assume that there is no collisions most of the time and for rare circumstances it is ok to re-read file from destination storage
                      await myStorage.OpenForReadingAsync(dstFile, async stream =>
                      {
                        await backupStorage.CreateForWritingAsync(dstFile, AccessMode.Public, stream);
                      });
                      lock (uploadFilesSync)
                      {
                        uploadFiles.Add((srcFile, dstFile));
                      }
                      break;
                    case CollisionResolutionMode.Overwrite when backupStorage == null:
                    case CollisionResolutionMode.OverwriteWithoutBackup:
                      logger.Fix($"The source file {srcFile} differs from the destination. File will be overwritten without backup.");
                      lock (uploadFilesSync)
                      {
                        // TODO: handle overwrites in the next phase
                        uploadFiles.Add((srcFile, dstFile));
                      }
                      break;
                    default:
                      throw new InvalidOperationException("Unknown CollisionResolutionMode value: " + myCollisionResolutionMode);
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
          if (statistics.HasProblems)
          {
            myLogger.Error("Found some issues in source storage, uploading was interrupted");
            return 1;
          }
        }
        
        if (uploadFiles.Count > 0)
        {
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

      return 0;
    }
  }
}