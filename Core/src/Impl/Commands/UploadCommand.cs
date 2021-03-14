using System;
using System.Collections.Concurrent;
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
  internal sealed class UploadCommand : ICommand
  {
    private readonly ILogger myLogger;
    private readonly string mySource;
    private readonly StorageFormat myNewStorageFormat;
    private readonly IStorage myStorage;
    private readonly int myDegreeOfParallelism;

    public UploadCommand(
      [NotNull] ILogger logger,
      [NotNull] IStorage storage,
      int degreeOfParallelism,
      [NotNull] string source,
      StorageFormat newStorageFormat)
    {
      myLogger = logger ?? throw new ArgumentNullException(nameof(logger));
      myStorage = storage ?? throw new ArgumentNullException(nameof(storage));
      myDegreeOfParallelism = degreeOfParallelism;
      mySource = source ?? throw new ArgumentNullException(nameof(source));
      myNewStorageFormat = newStorageFormat;
    }

    public async Task<int> ExecuteAsync()
    {
      var srcStorage = (IStorage) new FileSystemStorage(mySource);

      IReadOnlyCollection<string> srcFiles;
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

        srcFiles = tagItems.Select(x => x.Key).Concat(files).ToList();
      }

      var dstValidator = new Validator(myLogger, myStorage, "Destination");
      var dstStorageFormat = await dstValidator.CreateOrValidateStorageMarkersAsync(myNewStorageFormat);

      {
        myLogger.Info($"[{DateTime.Now:s}] Checking file compatibility...");
        var uploadFiles = new ConcurrentBag<Tuple<string, string>>();

        {
          var statistics = new Statistics();
          ILogger logger = new LoggerWithStatistics(myLogger, statistics);

          long existFiles = 0;
          await srcFiles.ParallelFor(myDegreeOfParallelism, async srcFile =>
            {
              logger.Verbose($"  Checking {srcFile}");
              var hash = SHA256.Create();
              var dstFile = TagUtil.IsDataFile(srcFile) && srcFile.ValidateAndFixDataPath(dstStorageFormat, out var fixedFile) == PathUtil.ValidateAndFixErrors.CanBeFixed
                ? fixedFile
                : srcFile;
              if (await myStorage.ExistsAsync(dstFile))
              {
                Interlocked.Increment(ref existFiles);
                var dstLen = await myStorage.GetLengthAsync(dstFile);
                var srcLen = await srcStorage.GetLengthAsync(srcFile);
                if (srcLen != dstLen)
                  logger.Error($"The file {srcFile} length {srcLen} differs then the destination length {dstLen}");
                else
                {
                  var dstHash = await myStorage.OpenForReadingAsync(dstFile, stream => hash.ComputeHashAsync(stream));
                  var srcHash = await srcStorage.OpenForReadingAsync(srcFile, stream => hash.ComputeHashAsync(stream));
                  if (!srcHash.SequenceEqual(dstHash))
                    logger.Error($"The file {srcFile} hash {srcHash.ToHex()} differs then the destination hash {dstHash.ToHex()}");
                }
              }
              else
                uploadFiles.Add(Tuple.Create(srcFile, dstFile));
            });

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
          await uploadFiles.ParallelFor(myDegreeOfParallelism, async item =>
            {
              var (srcFile, dstFile) = item;
              myLogger.Info($"  Uploading {srcFile}");
              await using var memoryStream = new MemoryStream();
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