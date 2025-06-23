using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.SymbolStorage.Impl.Logger;
using JetBrains.SymbolStorage.Impl.Storages;
using Microsoft.SymbolStore;
using Microsoft.SymbolStore.KeyGenerators;

namespace JetBrains.SymbolStorage.Impl.Commands
{
  internal sealed partial class LocalFilesScanner
  {
    private readonly int myDegreeOfParallelism;
    private readonly string? myBaseDir;
    private readonly bool myCompressPe;
    private readonly bool myCompressWPdb;
    private readonly bool myIsKeepNonCompressed;
    private readonly ILogger myLogger;
    private readonly Func<ITracer, string, string, SymbolStoragePath, Task> myProcessNormal;
    private readonly Func<ITracer, string, string, SymbolStoragePath, Task> myProcessCompressed;
    private readonly IEnumerable<string> mySources;

    public LocalFilesScanner(
      ILogger logger,
      int degreeOfParallelism,
      bool compressPe,
      bool compressWPdb,
      bool isKeepNonCompressed,
      IEnumerable<string> sourcePaths,
      Func<ITracer, string, string, SymbolStoragePath, Task> processNormal,
      Func<ITracer, string, string, SymbolStoragePath, Task> processPacked,
      string? baseDir = null)
    {
      myLogger = logger ?? throw new ArgumentNullException(nameof(logger));
      myCompressPe = compressPe;
      myCompressWPdb = compressWPdb;
      myIsKeepNonCompressed = isKeepNonCompressed;
      mySources = sourcePaths ?? throw new ArgumentNullException(nameof(sourcePaths));
      myProcessNormal = processNormal ?? throw new ArgumentNullException(nameof(processNormal));
      myProcessCompressed = processPacked ?? throw new ArgumentNullException(nameof(processPacked));
      myDegreeOfParallelism = degreeOfParallelism;
      myBaseDir = baseDir;
    }

    public async Task<Statistics> ExecuteAsync()
    {
      myLogger.Info($"[{DateTime.Now:s}] Scanning source files...");
      var statistics = new Statistics();
      ITracer tracer = new Tracer(new LoggerWithStatistics(myLogger, statistics));
      
      List<List<(string dir, string file)>> items = mySources.ParallelFor(myDegreeOfParallelism, source =>
      {
        var fullPath = Path.GetFullPath(source);
        if (File.Exists(fullPath))
          return [(string.IsNullOrEmpty(myBaseDir) ? Path.GetDirectoryName(fullPath) ?? "" : myBaseDir, fullPath)];

        if (Directory.Exists(fullPath))
          return Directory.GetFiles(fullPath, "*", SearchOption.AllDirectories).Select(x => (string.IsNullOrEmpty(myBaseDir) ? fullPath : myBaseDir, x)).ToList();

        tracer.Error($"The source path {fullPath} doesn't exist");
        return [];
      });

      await items.SelectMany(x => x).ParallelForAsync(myDegreeOfParallelism, item => new ValueTask(ScanFileAsync(item.dir, item.file, tracer)));
      return statistics;
    }

    private async Task ScanFileAsync(string sourceDir, string sourceFile, ITracer tracer)
    {
      var srcFile = Path.GetRelativePath(sourceDir, sourceFile);
      tracer.Information($"  Scanning {srcFile}...");
      foreach (var (symbolStoreKey, keyType) in GetKeyInfos(tracer, sourceFile))
      {
        var index = symbolStoreKey.Index;
        if (!SymbolStoreKey.IsKeyValid(index))
        {
          tracer.Error($"Invalid key index in file {index}");
        }
        else if (keyType == KeyType.Elf && Path.GetExtension(sourceFile) == ".debug" && !index.EndsWith("/_.debug"))
        {
          // Bug: Check that ELF .debug was processed right way! See https://github.com/dotnet/symstore/issues/158
          tracer.Error($"ELF file {sourceFile} was processed incorrectly because Microsoft.SymbolStore doesn't support .debug extension");
        }
        else
        {
          var dstFile = index;
          if (
            myCompressWPdb && keyType == KeyType.WPdb ||
            myCompressPe && keyType == KeyType.Pe)
          {
            await myProcessCompressed(tracer, sourceDir, srcFile, SymbolStoragePath.FromSystemPath(Path.ChangeExtension(dstFile, PathUtil.GetPackedExtension(Path.GetExtension(dstFile.AsSpan())))));
            if (myIsKeepNonCompressed)
              await myProcessNormal(tracer, sourceDir, srcFile, SymbolStoragePath.FromSystemPath(dstFile));
          }
          else
          {
            await myProcessNormal(tracer, sourceDir, srcFile, SymbolStoragePath.FromSystemPath(dstFile));
          }
        }
      }
    }

    /// <summary>
    /// Return symbol store key for the specified <paramref name="file"/>
    /// At the moment all supported file formats have one key (one location within store),
    /// but in the future there may be changes and therefore a List is used.
    /// </summary>
    private static List<(SymbolStoreKey, KeyType)> GetKeyInfos(ITracer tracer, string file)
    {
      using var symbolStoreFile = new SymbolStoreFile(File.OpenRead(file), file);
      foreach (var (generator, keyType) in GetGenerators(tracer, symbolStoreFile))
      {
        symbolStoreFile.Stream.Seek(0, SeekOrigin.Begin);
        if (generator.IsValid())
          return generator.GetKeys(KeyTypeFlags.IdentityKey).Select(x => (x, keyType)).ToList();
      }

      tracer.Warning($"Invalid file {file} type");
      return [];
    }
    
    private static IEnumerable<(KeyGenerator, KeyType)> GetGenerators(ITracer tracer, SymbolStoreFile file)
    {
      yield return (new PortablePDBFileKeyGenerator(tracer, file), KeyType.Other);
      yield return (new PDBFileKeyGenerator(tracer, file), KeyType.WPdb);
      yield return (new PEFileKeyGenerator(tracer, file), KeyType.Pe);
      yield return (new ELFFileKeyGenerator(tracer, file), KeyType.Elf);
      yield return (new MachOFileKeyGenerator(tracer, file), KeyType.Other);
    }
  }
}