using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using JetBrains.SymbolStorage.Impl.Logger;
using JetBrains.SymbolStorage.Impl.Storages;
using JetBrains.SymbolStorage.Impl.Tags;

namespace JetBrains.SymbolStorage.Impl.Commands
{
  internal sealed class UploadCommand
  {
    private readonly ILogger myLogger;
    private readonly string mySource;
    private readonly StorageFormat myNewStorageFormat;
    private readonly IStorage myStorage;

    public UploadCommand(
      [NotNull] ILogger logger,
      [NotNull] IStorage storage,
      [NotNull] string source,
      StorageFormat newStorageFormat)
    {
      myLogger = logger ?? throw new ArgumentNullException(nameof(logger));
      myStorage = storage ?? throw new ArgumentNullException(nameof(storage));
      mySource = source ?? throw new ArgumentNullException(nameof(source));
      myNewStorageFormat = newStorageFormat;
    }

    public async Task<int> Execute()
    {
      var srcStorage = (IStorage) new FileSystemStorage(mySource);

      IReadOnlyCollection<string> srcFiles;
      {
        var validator = new Validator(myLogger, srcStorage, "Source");
        var srcStorageFormat = await validator.ValidateStorageMarkers();

        var tagItems = await validator.LoadTagItems();
        validator.DumpProducts(tagItems);
        validator.DumpProperties(tagItems);
        var (totalSize, files) = await validator.GatherDataFiles();
        var (statistics, _) = await validator.Validate(tagItems, files, srcStorageFormat, Validator.ValidateMode.Validate);
        myLogger.Info($"[{DateTime.Now:s}] Done with source validation (size: {totalSize.ToKibibyte()}, files: {files.Count + tagItems.Count}, warnings: {statistics.Warnings}, errors: {statistics.Errors})");
        if (statistics.HasProblems)
        {
          myLogger.Error("Found some issues in source storage, uploading was interrupted");
          return 1;
        }

        srcFiles = tagItems.Select(x => x.Key).Concat(files).ToList();
      }

      var dstValidator = new Validator(myLogger, myStorage, "Destination");
      var dstStorageFormat = await dstValidator.CreateOrValidateStorageMarkers(myNewStorageFormat);

      {
        myLogger.Info($"[{DateTime.Now:s}] Checking file compatibility...");
        var uploadFiles = new List<Tuple<string, string>>();

        {
          var statistics = new Statistics();
          ILogger logger = new LoggerWithStatistics(myLogger, statistics);
          var hash = SHA256.Create();

          long existFiles = 0;
          foreach (var srcFile in srcFiles)
          {
            logger.Info($"  Checking {srcFile}");
            var dstFile = TagUtil.IsDataFile(srcFile) && srcFile.ValidateAndFixDataPath(dstStorageFormat, out var fixedFile) == PathUtil.ValidateAndFixErrors.CanBeFixed
              ? fixedFile
              : srcFile;
            if (await myStorage.Exists(dstFile))
            {
              Interlocked.Increment(ref existFiles);
              var dstLen = await myStorage.GetLength(dstFile);
              var srcLen = await srcStorage.GetLength(srcFile);
              if (srcLen != dstLen)
                logger.Error($"The file {srcFile} length {srcLen} differs then the destination length {dstLen}");
              else
              {
                var dstHash = await myStorage.OpenForReading(dstFile, stream => hash.ComputeHash(stream));
                var srcHash = await srcStorage.OpenForReading(srcFile, stream => hash.ComputeHash(stream));
                if (!srcHash.SequenceEqual(dstHash))
                  logger.Error($"The file {srcFile} hash {srcHash.ToHex()} differs then the destination hash {dstHash.ToHex()}");
              }
            }
            else
              uploadFiles.Add(Tuple.Create(srcFile, dstFile));
          }

          myLogger.Info($"[{DateTime.Now:s}] Done with compatibility (new files: {uploadFiles.Count}, same files: {existFiles}, warnings: {statistics.Warnings}, errors: {statistics.Errors})");
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
          foreach (var (srcFile, dstFile) in uploadFiles)
          {
            myLogger.Info($"  Uploading {srcFile}");
            await using var memoryStream = new MemoryStream();
            await srcStorage.OpenForReading(srcFile, stream => stream.CopyTo(memoryStream));
            await myStorage.CreateForWriting(dstFile, TagUtil.IsTagFile(dstFile) ? AccessMode.Private : AccessMode.Public, memoryStream.Length, memoryStream.Rewind());
            Interlocked.Add(ref totalSize, memoryStream.Length);
          }

          await myStorage.InvalidateExternalServices(uploadFiles.Select(x => x.Item2).ToList());
          myLogger.Info($"[{DateTime.Now:s}] Done with uploading (size: {totalSize.ToKibibyte()}, files: {uploadFiles.Count})");
        }
      }

      return 0;
    }
  }
}