#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.SymbolStorage.Impl.Logger;

namespace JetBrains.SymbolStorage.Impl.Commands
{
  internal sealed class DumpCommand : ICommand
  {
    private readonly string myBaseDir;
    private readonly int myDegreeOfParallelism;
    private readonly bool myIsCompressPe;
    private readonly bool myIsCompressWPdb;
    private readonly ILogger myLogger;
    private readonly IReadOnlyCollection<string> mySources;
    private readonly string mySymbolReferenceFile;

    public DumpCommand(
      ILogger logger,
      int degreeOfParallelism,
      bool isCompressPe,
      bool isCompressWPdb,
      string symbolReferenceFile,
      IReadOnlyCollection<string> sources,
      string baseDir)
    {
      myLogger = logger ?? throw new ArgumentNullException(nameof(logger));
      myDegreeOfParallelism = degreeOfParallelism;
      myIsCompressPe = isCompressPe;
      myIsCompressWPdb = isCompressWPdb;
      mySymbolReferenceFile = symbolReferenceFile ?? throw new ArgumentNullException(nameof(symbolReferenceFile));
      mySources = sources ?? throw new ArgumentNullException(nameof(sources));
      myBaseDir = baseDir ?? throw new ArgumentNullException(nameof(baseDir));
    }

    public async Task<int> ExecuteAsync()
    {
      var map = new ConcurrentBag<KeyValuePair<string, string>>();
      var statistics = await new Scanner(myLogger, myDegreeOfParallelism, myIsCompressPe, myIsCompressWPdb, false, mySources,
        (_, _, srcFile, dstFile) =>
          {
            map.Add(KeyValuePair.Create(srcFile, dstFile));
            return Task.CompletedTask;
          },
        (_, _, srcFile, dstFile) =>
          {
            map.Add(KeyValuePair.Create(srcFile, dstFile));
            return Task.CompletedTask;
          }, myBaseDir).ExecuteAsync();
      myLogger.Info($"[{DateTime.Now:s}] Done with data (warnings: {statistics.Warnings}, errors: {statistics.Errors})");
      if (statistics.HasProblems)
      {
        myLogger.Error("Found some issues, creating was interrupted");
        return 1;
      }

      await WriteSymRef(map);
      return 0;
    }

    private Task WriteSymRef(IEnumerable<KeyValuePair<string, string>> map)
    {
      myLogger.Info($"[{DateTime.Now:s}] Writing symbol reference file...");
      var orderedMap = map.OrderBy(x => x.Key).ToList();
      var keyLen = orderedMap.Max(x => x.Key.Length);
      using var writer = File.CreateText(mySymbolReferenceFile);
      foreach (var (key, value) in orderedMap)
      {
        writer.Write(key.PadRight(keyLen));
        writer.Write(' ');
        writer.WriteLine(value);
      }

      return Task.CompletedTask;
    }
  }
}