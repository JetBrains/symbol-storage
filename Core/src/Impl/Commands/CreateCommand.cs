using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.SymbolStorage.Impl.Logger;
using JetBrains.SymbolStorage.Impl.Storages;
using JetBrains.SymbolStorage.Impl.Tags;

namespace JetBrains.SymbolStorage.Impl.Commands
{
  internal sealed class CreateCommand : ICommand
  {
    private readonly StorageFormat myExpectedStorageFormat;
    private readonly bool myIsCompressPe;
    private readonly bool myIsCompressWPdb;
    private readonly bool myIsKeepNonCompressed;
    private readonly ILogger myLogger;
    private readonly IEnumerable<KeyValuePair<string, string>> myProperties;
    private readonly IReadOnlyCollection<string> mySources;
    private readonly IStorage myStorage;
    private readonly string myToolId;
    private readonly Identity myIdentity;
    private readonly int myDegreeOfParallelism;
    private readonly bool myIsProtected;

    public CreateCommand(
      ILogger logger,
      IStorage storage,
      int degreeOfParallelism,
      StorageFormat expectedStorageFormat,
      string toolId,
      Identity identity,
      bool isProtected,
      bool isCompressPe,
      bool isCompressWPdb,
      bool isKeepNonCompressed,
      IEnumerable<KeyValuePair<string, string>> properties,
      IReadOnlyCollection<string> sources)
    {
      myLogger = logger ?? throw new ArgumentNullException(nameof(logger));
      myStorage = storage ?? throw new ArgumentNullException(nameof(storage));
      myDegreeOfParallelism = degreeOfParallelism;
      myExpectedStorageFormat = expectedStorageFormat;
      myToolId = toolId ?? throw new ArgumentNullException(nameof(toolId));
      myIdentity = identity ?? throw new ArgumentNullException(nameof(identity));
      myIsProtected = isProtected;
      myIsCompressPe = isCompressPe;
      myIsCompressWPdb = isCompressWPdb;
      myIsKeepNonCompressed = isKeepNonCompressed;
      myProperties = properties ?? throw new ArgumentNullException(nameof(properties));
      mySources = sources ?? throw new ArgumentNullException(nameof(sources));
    }

    public async Task<int> ExecuteAsync()
    {
      await new StorageManager(myLogger, myStorage).CreateStorageMarkersAsync(myExpectedStorageFormat);

      var dstFiles = new ConcurrentDictionary<string, bool>();
      var statistics = await new LocalFilesScanner(myLogger, myDegreeOfParallelism, myIsCompressPe, myIsCompressWPdb, myIsKeepNonCompressed, mySources,
        async (tracer, srcDir, srcFile, dstFile) =>
          {
            if (dstFiles.TryAdd(dstFile, false))
              await WriteData(Path.Combine(srcDir, srcFile), stream => myStorage.CreateForWritingAsync(dstFile, AccessMode.Public, stream));
            else
              tracer.Warning($"The file {dstFile} already was created");
          },
        async (tracer, srcDir, srcFile, dstFile) =>
          {
            if (dstFiles.TryAdd(dstFile, false))
              await WriteDataPacked(Path.Combine(srcDir, srcFile), dstFile, stream => myStorage.CreateForWritingAsync(dstFile, AccessMode.Public, stream));
            else
              tracer.Warning($"The file {dstFile} already was created");
          }).ExecuteAsync();
      myLogger.Info($"[{DateTime.Now:s}] Done with data (warnings: {statistics.Warnings}, errors: {statistics.Errors})");
      if (statistics.HasProblems)
      {
        myLogger.Error("Found some issues, creating was interrupted");
        return 1;
      }

      await WriteTag(dstFiles.Select(x =>
      {
        var dir = Path.GetDirectoryName(x.Key);
        Debug.Assert(dir != null);
        return dir;
      }));
      
      await myStorage.InvalidateExternalServicesAsync();
      return 0;
    }

    private async Task WriteTag(IEnumerable<string> dirs)
    {
      myLogger.Info($"[{DateTime.Now:s}] Writing tag file...");
      var fileId = Guid.NewGuid();
      using var stream = new MemoryStream();
      await TagUtil.WriteTagScriptAsync(new Tag
        {
          ToolId = myToolId,
          FileId = fileId,
          Product = myIdentity.Product,
          Version = myIdentity.Version,
          CreationUtcTime = DateTime.UtcNow,
          IsProtected = myIsProtected,
          Properties = myProperties.Select(x => new TagKeyValue
            {
              Key = x.Key,
              Value = x.Value
            }).ToArray(),
          Directories = dirs.OrderBy(x => x, StringComparer.Ordinal).Distinct().ToArray()
        }, stream);

      await myStorage.CreateForWritingAsync(TagUtil.MakeTagFile(myIdentity, fileId), AccessMode.Private, stream);
    }

    private static async Task WriteData(
      string sourceFile,
      Func<Stream, Task> writeStorageFile)
    {
      await using var stream = File.OpenRead(sourceFile);
      await writeStorageFile(stream);
    }

    private static async Task WriteDataPacked(
      string sourceFile,
      string packedStorageRelativeFile,
      Func<Stream, Task> writeStorageFile)
    {
      CabCompressionUtil.VerifyPlatformSupported();
      
      // Note: packedStorageRelativeFile should be in following format: [cc/]aaa.bbb/<hash>/aaa.bb_ 
      var filePathInArchive = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(packedStorageRelativeFile)));
      if (filePathInArchive == null)
        throw new ArgumentException("Incorrect file path in archive. Expected format: '[cc/]aaa.bbb/<hash>/aaa.bb_'", nameof(packedStorageRelativeFile));
      
      var tempFile = Path.GetTempFileName();
      try
      {
        CabCompressionUtil.CompressFile(tempFile, 
          new CompressionFileInfo(filePathInArchive, sourceFile)
        );
        
        await using var stream = File.Open(tempFile, FileMode.Open, FileAccess.Read);
        await writeStorageFile(stream);
      }
      finally
      {
        File.Delete(tempFile);
      }
    }
  }
}