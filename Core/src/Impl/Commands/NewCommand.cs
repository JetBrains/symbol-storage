using System;
using System.Threading.Tasks;
using JetBrains.Annotations;
using JetBrains.SymbolStorage.Impl.Logger;
using JetBrains.SymbolStorage.Impl.Storages;

namespace JetBrains.SymbolStorage.Impl.Commands
{
  internal sealed class NewCommand
  {
    private readonly ILogger myLogger;
    private readonly StorageFormat myNewStorageFormat;
    private readonly IStorage myStorage;

    public NewCommand(
      [NotNull] ILogger logger,
      [NotNull] IStorage storage,
      StorageFormat newStorageFormat)
    {
      myLogger = logger ?? throw new ArgumentNullException(nameof(logger));
      myStorage = storage ?? throw new ArgumentNullException(nameof(storage));
      myNewStorageFormat = newStorageFormat;
    }

    public async Task<int> Execute()
    {
      var validator = new Validator(myLogger, myStorage);
      await validator.CreateStorageMarkers(myNewStorageFormat);
      myLogger.Info($"[{DateTime.Now:s}] Done");
      return 0;
    }
  }
}