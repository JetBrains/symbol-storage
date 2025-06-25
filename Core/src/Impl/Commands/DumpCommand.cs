using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.SymbolStorage.Impl.Logger;
using JetBrains.SymbolStorage.Impl.Storages;

namespace JetBrains.SymbolStorage.Impl.Commands
{
  internal sealed class DumpCommand : IStatsReportingCommand
  {
    private readonly string myBaseDir;
    private readonly int myDegreeOfParallelism;
    private readonly bool myIsCompressPe;
    private readonly bool myIsCompressWPdb;
    private readonly ILogger myLogger;
    private readonly IReadOnlyCollection<string> mySources;
    private readonly string mySymbolReferenceFile;
    private long mySubOpsCount;

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

    public long SubOperationsCount => Volatile.Read(ref mySubOpsCount);
    
    public async Task<int> ExecuteAsync()
    {
      Volatile.Write(ref mySubOpsCount, 0);
      var map = new List<KeyValuePair<string, SymbolStoragePath>>();
      var mapSyncObj = new Lock();
      
      var statistics = await new LocalFilesScanner(myLogger, myDegreeOfParallelism, myIsCompressPe, myIsCompressWPdb, false, mySources,
        (_, _, srcFile, dstFile) =>
          {
            Interlocked.Increment(ref mySubOpsCount);
            lock (mapSyncObj)
            {
              map.Add(KeyValuePair.Create(srcFile, dstFile));
            }
            return Task.CompletedTask;
          },
        (_, _, srcFile, dstFile) =>
          {
            Interlocked.Increment(ref mySubOpsCount);
            lock (mapSyncObj)
            {
              map.Add(KeyValuePair.Create(srcFile, dstFile));
            }
            return Task.CompletedTask;
          }, myBaseDir).ExecuteAsync();
      myLogger.Info($"[{DateTime.Now:s}] Done with data (warnings: {statistics.Warnings}, errors: {statistics.Errors})");
      if (statistics.HasProblems)
      {
        myLogger.Error("Found some issues, creating was interrupted");
        return 1;
      }

      WriteSymRef(map);
      return 0;
    }

    private void WriteSymRef(List<KeyValuePair<string, SymbolStoragePath>> map)
    {
      myLogger.Info($"[{DateTime.Now:s}] Writing symbol reference file...");
      map.Sort((a, b) => string.Compare(a.Key, b.Key, StringComparison.Ordinal));
      var keyLen = map.Max(x => x.Key.Length);
      using var writer = File.CreateText(mySymbolReferenceFile);
      foreach (var (key, value) in map)
      {
        writer.Write(key.PadRight(keyLen));
        writer.Write(' ');
        writer.WriteLine(value);
      }
    }
  }
}