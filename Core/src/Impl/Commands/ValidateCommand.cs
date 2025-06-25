using System;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.SymbolStorage.Impl.Logger;
using JetBrains.SymbolStorage.Impl.Storages;

namespace JetBrains.SymbolStorage.Impl.Commands
{
  internal sealed class ValidateCommand : IStatsReportingCommand
  {
    private readonly bool myFix;
    private readonly ILogger myLogger;
    private readonly IStorage myStorage;
    private readonly int myDegreeOfParallelism;
    private readonly bool myVerifyAcl;
    private long mySubOpsCount;

    public ValidateCommand(
      ILogger logger,
      IStorage storage,
      int degreeOfParallelism,
      bool verifyAcl,
      bool fix)
    {
      myLogger = logger ?? throw new ArgumentNullException(nameof(logger));
      myStorage = storage ?? throw new ArgumentNullException(nameof(storage));
      myDegreeOfParallelism = degreeOfParallelism;
      myVerifyAcl = verifyAcl;
      myFix = fix;
    }
    
    public long SubOperationsCount => Volatile.Read(ref mySubOpsCount);

    public async Task<int> ExecuteAsync()
    {
      Volatile.Write(ref mySubOpsCount, 0);
      
      var validator = new StorageManager(myLogger, myStorage);
      var storageFormat = await validator.ValidateStorageMarkersAsync();
      var tagItems = await validator.LoadTagItemsAsync(myDegreeOfParallelism);
      validator.DumpProducts(tagItems);
      validator.DumpProperties(tagItems);
      var (files, totalSize) = await validator.GatherDataFilesAsync();
      Volatile.Write(ref mySubOpsCount, files.Count);
      var (statistics, _) = await validator.ValidateAndFixAsync(myDegreeOfParallelism, tagItems, files, storageFormat, myFix ? StorageManager.ValidateMode.Fix : StorageManager.ValidateMode.Validate, myVerifyAcl);
      if (statistics.Fixes > 0)
        await myStorage.InvalidateExternalServicesAsync();
      myLogger.Info($"[{DateTime.Now:s}] Done (size: {totalSize.ToKibibyte()}, warnings: {statistics.Warnings}, errors: {statistics.Errors}, fixes: {statistics.Fixes})");
      return statistics.HasProblems ? 1 : 0;
    }
  }
}