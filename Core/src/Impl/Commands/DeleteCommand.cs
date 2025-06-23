using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using JetBrains.Annotations;
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
      [NotNull] ILogger logger,
      [NotNull] IStorage storage,
      int degreeOfParallelism,
      [NotNull] IdentityFilter identityFilter,
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
      var validator = new Validator(myLogger, myStorage);
      var storageFormat = await validator.ValidateStorageMarkersAsync();

      long deleteTags;
      IReadOnlyCollection<KeyValuePair<string, Tag>> tagItems;
      {
        var (incTagItems, excTagItems) = await validator.LoadTagItemsAsync(myDegreeOfParallelism, myIdentityFilter, mySafetyPeriod, false);
        validator.DumpProducts(incTagItems);
        validator.DumpProperties(incTagItems);
        deleteTags = incTagItems.Count;

        myLogger.Info($"[{DateTime.Now:s}] Deleting tag files...");
        await incTagItems.ParallelForAsync(myDegreeOfParallelism, async tagItem =>
          {
            var file = tagItem.Key;
            myLogger.Info($"  Deleting {file}");
            await myStorage.DeleteAsync(file);
          });

        tagItems = excTagItems;
      }

      {
        var (_, files) = await validator.GatherDataFilesAsync();
        var (statistics, deleted) = await validator.ValidateAsync(myDegreeOfParallelism, tagItems, files, storageFormat, Validator.ValidateMode.Delete);
        if (deleted > 0)
          await myStorage.InvalidateExternalServicesAsync();
        myLogger.Info($"[{DateTime.Now:s}] Done (deleted tag files: {deleteTags}, deleted data files: {deleted}, warnings: {statistics.Warnings}, errors: {statistics.Errors}, fixes: {statistics.Fixes})");
        return statistics.HasProblems ? 1 : 0;
      }
    }
  }
}