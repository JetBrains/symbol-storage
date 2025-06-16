using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using JetBrains.SymbolStorage.Impl.Logger;
using JetBrains.SymbolStorage.Impl.Storages;
using JetBrains.SymbolStorage.Impl.Tags;

namespace JetBrains.SymbolStorage.Impl.Commands
{
  internal sealed class DeleteCommand : ICommand
  {
    private readonly ILogger myLogger;
    private readonly IStorage myStorage;
    private readonly TimeSpan mySafetyPeriod;
    private readonly int myDegreeOfParallelism;
    private readonly IdentityFilter myIdentityFilter;

    public DeleteCommand(
      ILogger logger,
      IStorage storage,
      int degreeOfParallelism,
      IdentityFilter identityFilter,
      TimeSpan safetyPeriod)
    {
      myLogger = logger ?? throw new ArgumentNullException(nameof(logger));
      myStorage = storage ?? throw new ArgumentNullException(nameof(storage));
      myDegreeOfParallelism = degreeOfParallelism;
      myIdentityFilter = identityFilter ?? throw new ArgumentNullException(nameof(identityFilter));
      mySafetyPeriod = safetyPeriod;
    }

    public async Task<int> ExecuteAsync()
    {
      var validator = new StorageManager(myLogger, myStorage);
      var storageFormat = await validator.ValidateStorageMarkersAsync();

      long deleteTags;
      List<TaggedFile> tagItems;
      {
        var (incTagItems, excTagItems) = await validator.LoadTagItemsAsync(myDegreeOfParallelism, myIdentityFilter, mySafetyPeriod, false);
        validator.DumpProducts(incTagItems);
        validator.DumpProperties(incTagItems);
        deleteTags = incTagItems.Count;

        myLogger.Info($"[{DateTime.Now:s}] Deleting tag files...");
        await incTagItems.ParallelForAsync(myDegreeOfParallelism, async tagItem =>
          {
            var file = tagItem.TagFile;
            myLogger.Info($"  Deleting {file}");
            await myStorage.DeleteAsync(file);
          });

        tagItems = excTagItems;
      }

      {
        var (files, _) = await validator.GatherDataFilesAsync();
        var (statistics, deleted) = await validator.ValidateAndFixAsync(myDegreeOfParallelism, tagItems, files, storageFormat, StorageManager.ValidateMode.Delete);
        if (deleted > 0)
          await myStorage.InvalidateExternalServicesAsync();
        myLogger.Info($"[{DateTime.Now:s}] Done (deleted tag files: {deleteTags}, deleted data files: {deleted}, warnings: {statistics.Warnings}, errors: {statistics.Errors}, fixes: {statistics.Fixes})");
        return statistics.HasProblems ? 1 : 0;
      }
    }
  }
}