using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using JetBrains.Annotations;
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
    private readonly TimeSpan mySafetyPeriod;
    private readonly bool? myProtectedFilter;

    public ListCommand(
      [NotNull] ILogger logger,
      [NotNull] IStorage storage,
      int degreeOfParallelism,
      [NotNull] IdentityFilter identityFilter,
      TimeSpan safetyPeriod,
      bool? protectedFilter)
    {
      myLogger = logger ?? throw new ArgumentNullException(nameof(logger));
      myStorage = storage ?? throw new ArgumentNullException(nameof(storage));
      myDegreeOfParallelism = degreeOfParallelism;
      myIdentityFilter = identityFilter ?? throw new ArgumentNullException(nameof(identityFilter));
      mySafetyPeriod = safetyPeriod;
      myProtectedFilter = protectedFilter;
    }

    public async Task<int> ExecuteAsync()
    {
      var validator = new Validator(myLogger, myStorage);
      var (tagItems, _) = await validator.LoadTagItemsAsync(myDegreeOfParallelism, myIdentityFilter, mySafetyPeriod, myProtectedFilter);
      validator.DumpProducts(tagItems);
      validator.DumpProperties(tagItems);
      myLogger.Info($"[{DateTime.Now:s}] Done (tags: {tagItems.Count})");
      return 0;
    }
  }
}