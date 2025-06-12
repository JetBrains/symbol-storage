#nullable enable

using System;
using System.Threading.Tasks;
using JetBrains.SymbolStorage.Impl.Logger;
using JetBrains.SymbolStorage.Impl.Storages;

namespace JetBrains.SymbolStorage.Impl.Commands
{
  internal sealed class NewCommand : ICommand
  {
    private readonly ILogger myLogger;
    private readonly StorageFormat myNewStorageFormat;
    private readonly IStorage myStorage;

    public NewCommand(
      ILogger logger,
      IStorage storage,
      StorageFormat newStorageFormat)
    {
      myLogger = logger ?? throw new ArgumentNullException(nameof(logger));
      myStorage = storage ?? throw new ArgumentNullException(nameof(storage));
      myNewStorageFormat = newStorageFormat;
    }

    public async Task<int> ExecuteAsync()
    {
      var validator = new Validator(myLogger, myStorage);
      await validator.CreateStorageMarkersAsync(myNewStorageFormat);
      myLogger.Info($"[{DateTime.Now:s}] Done");
      return 0;
    }
  }
}