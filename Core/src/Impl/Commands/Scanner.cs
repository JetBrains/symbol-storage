using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using JetBrains.SymbolStorage.Impl.Logger;
using Microsoft.SymbolStore;
using Microsoft.SymbolStore.KeyGenerators;

namespace JetBrains.SymbolStorage.Impl.Commands
{
  internal sealed partial class Scanner
  {
    private readonly int myDegreeOfParallelism;
    private readonly string myBaseDir;
    private readonly bool myCompressPe;
    private readonly bool myCompressWPdb;
    private readonly bool myIsKeepNonCompressed;
    private readonly ILogger myLogger;
    private readonly Func<ITracer, string, string, string, Task> myProcessNormal;
    private readonly Func<ITracer, string, string, string, Task> myProcessCompressed;
    private readonly IEnumerable<string> mySources;

    public Scanner(
      [NotNull] ILogger logger,
      int degreeOfParallelism,
      bool compressPe,
      bool compressWPdb,
      bool isKeepNonCompressed,
      [NotNull] IEnumerable<string> sourcePaths,
      [NotNull] Func<ITracer, string, string, string, Task> processNormal,
      [NotNull] Func<ITracer, string, string, string, Task> processPacked,
      [CanBeNull] string baseDir = null)
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
      var items = await mySources.ParallelFor(myDegreeOfParallelism, async source =>
        {
          await Task.Yield();
          var fullPath = Path.GetFullPath(source);
          if (File.Exists(fullPath))
            return new[] {new KeyValuePair<string, string>(string.IsNullOrEmpty(myBaseDir) ? Path.GetDirectoryName(fullPath) ?? "" : myBaseDir, fullPath)};

          if (Directory.Exists(fullPath))
            return Directory.GetFiles(fullPath, "*", SearchOption.AllDirectories).Select(x => new KeyValuePair<string, string>(string.IsNullOrEmpty(myBaseDir) ? fullPath : myBaseDir, x)).ToArray();

          tracer.Error($"The source path {fullPath} doesn't exist");
          return Array.Empty<KeyValuePair<string, string>>();
        });

      await items.SelectMany(x => x).ParallelFor(myDegreeOfParallelism, item => ScanFileAsync(item.Key, item.Value, tracer));
      return statistics;
    }

    private async Task ScanFileAsync([NotNull] string sourceDir, [NotNull] string sourceFile, [NotNull] ITracer tracer)
    {
      var srcFile = Path.GetRelativePath(sourceDir, sourceFile);
      tracer.Information($"  Scanning {srcFile}...");
      foreach (var (symbolStoreKey, keyType) in await GetKeyInfos(tracer, sourceFile))
      {
        var index = symbolStoreKey.Index;
        if (!SymbolStoreKey.IsKeyValid(index))
          tracer.Error($"Invalid key index in file {index}");
        else if (keyType == KeyType.Elf && Path.GetExtension(sourceFile) == ".debug" && !index.EndsWith("/_.debug"))
        {
          // Bug: Check that ELF .debug was processed right way! See https://github.com/dotnet/symstore/issues/158
          tracer.Error($"ELF file {sourceFile} was processed incorrectly because Microsoft.SymbolStore doesn't support .debug extension");
        }
        else
        {
          var dstFile = index.NormalizeSystem();
          if (
            myCompressWPdb && keyType == KeyType.WPdb ||
            myCompressPe && keyType == KeyType.Pe)
          {
            await myProcessCompressed(tracer, sourceDir, srcFile, Path.ChangeExtension(dstFile, PathUtil.GetPackedExtension(Path.GetExtension(dstFile))));
            if (myIsKeepNonCompressed)
              await myProcessNormal(tracer, sourceDir, srcFile, dstFile);
          }
          else
            await myProcessNormal(tracer, sourceDir, srcFile, dstFile);
        }
      }
    }

    private static async Task<IEnumerable<Tuple<SymbolStoreKey, KeyType>>> GetKeyInfos([NotNull] ITracer tracer, [NotNull] string file)
    {
      await Task.Yield();

      using var symbolStoreFile = new SymbolStoreFile(File.OpenRead(file), file);
      foreach (var (generator, keyType) in GetGenerators(tracer, symbolStoreFile))
      {
        symbolStoreFile.Stream.Seek(0, SeekOrigin.Begin);
        if (generator.IsValid())
          return generator.GetKeys(KeyTypeFlags.IdentityKey).Select(x => Tuple.Create(x, keyType)).ToList();
      }

      tracer.Warning($"Invalid file {file} type");
      return Enumerable.Empty<Tuple<SymbolStoreKey, KeyType>>();
    }

    [ItemNotNull]
    private static IEnumerable<Tuple<KeyGenerator, KeyType>> GetGenerators([NotNull] ITracer tracer, [NotNull] SymbolStoreFile file)
    {
      yield return Tuple.Create((KeyGenerator) new PortablePDBFileKeyGenerator(tracer, file), KeyType.Other);
      yield return Tuple.Create((KeyGenerator) new PDBFileKeyGenerator(tracer, file), KeyType.WPdb);
      yield return Tuple.Create((KeyGenerator) new PEFileKeyGenerator(tracer, file), KeyType.Pe);
      yield return Tuple.Create((KeyGenerator) new ELFFileKeyGenerator(tracer, file), KeyType.Elf);
      yield return Tuple.Create((KeyGenerator) new MachOFileKeyGenerator(tracer, file), KeyType.Other);
    }
  }
}