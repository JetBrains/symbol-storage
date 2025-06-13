using System;
using System.Threading.Tasks;
using JetBrains.SymbolStorage.Impl.Logger;
using JetBrains.SymbolStorage.Impl.Storages;
using JetBrains.SymbolStorage.Impl.Tags;

namespace JetBrains.SymbolStorage.Impl.Commands
{
  internal sealed class ListCommand : ICommand
  {
    private readonly ILogger myLogger;
    private readonly IStorage myStorage;
    private readonly int myDegreeOfParallelism;
    private readonly IdentityFilter myIdentityFilter;
    private readonly TimeSpan? myMinItemAgeFilter;
    private readonly bool? myProtectedFilter;

    public ListCommand(
      ILogger logger,
      IStorage storage,
      int degreeOfParallelism,
      IdentityFilter identityFilter,
      TimeSpan? minItemAgeFilter,
      bool? protectedFilter)
    {
      myLogger = logger ?? throw new ArgumentNullException(nameof(logger));
      myStorage = storage ?? throw new ArgumentNullException(nameof(storage));
      myDegreeOfParallelism = degreeOfParallelism;
      myIdentityFilter = identityFilter ?? throw new ArgumentNullException(nameof(identityFilter));
      myMinItemAgeFilter = minItemAgeFilter;
      myProtectedFilter = protectedFilter;
    }

    public async Task<int> ExecuteAsync()
    {
      var validator = new Validator(myLogger, myStorage);
      var (tagItems, _) = await validator.LoadTagItemsAsync(myDegreeOfParallelism, myIdentityFilter, myMinItemAgeFilter, myProtectedFilter);
      validator.DumpProducts(tagItems);
      validator.DumpProperties(tagItems);
      myLogger.Info($"[{DateTime.Now:s}] Done (tags: {tagItems.Count})");
      return 0;
    }
  }
}