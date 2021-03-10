using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using JetBrains.Annotations;
using JetBrains.SymbolStorage.Impl.Logger;
using JetBrains.SymbolStorage.Impl.Storages;

namespace JetBrains.SymbolStorage.Impl.Commands
{
  internal sealed class ListCommand : ICommand
  {
    private readonly ILogger myLogger;
    private readonly IStorage myStorage;
    private readonly IReadOnlyCollection<string> myIncProductWildcards;
    private readonly IReadOnlyCollection<string> myExcProductWildcards;
    private readonly IReadOnlyCollection<string> myIncVersionWildcards;
    private readonly IReadOnlyCollection<string> myExcVersionWildcards;
    private readonly TimeSpan mySafetyPeriod;

    public ListCommand(
      [NotNull] ILogger logger,
      [NotNull] IStorage storage,
      [NotNull] IReadOnlyCollection<string> incProductWildcards,
      [NotNull] IReadOnlyCollection<string> excProductWildcards,
      [NotNull] IReadOnlyCollection<string> incVersionWildcards,
      [NotNull] IReadOnlyCollection<string> excVersionWildcards,
      TimeSpan safetyPeriod)
    {
      myLogger = logger ?? throw new ArgumentNullException(nameof(logger));
      myStorage = storage ?? throw new ArgumentNullException(nameof(storage));
      myIncProductWildcards = incProductWildcards ?? throw new ArgumentNullException(nameof(incProductWildcards));
      myExcProductWildcards = excProductWildcards ?? throw new ArgumentNullException(nameof(excProductWildcards));
      myIncVersionWildcards = incVersionWildcards ?? throw new ArgumentNullException(nameof(incVersionWildcards));
      myExcVersionWildcards = excVersionWildcards ?? throw new ArgumentNullException(nameof(excVersionWildcards));
      mySafetyPeriod = safetyPeriod;
    }

    public async Task<int> ExecuteAsync()
    {
      var validator = new Validator(myLogger, myStorage);
      var (tagItems, _) = await validator.LoadTagItemsAsync(
        myIncProductWildcards,
        myExcProductWildcards,
        myIncVersionWildcards,
        myExcVersionWildcards,
        mySafetyPeriod);
      validator.DumpProducts(tagItems);
      validator.DumpProperties(tagItems);
      myLogger.Info($"[{DateTime.Now:s}] Done (tags: {tagItems.Count})");
      return 0;
    }
  }
}