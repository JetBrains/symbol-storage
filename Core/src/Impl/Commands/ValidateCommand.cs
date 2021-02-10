using System;
using System.Threading.Tasks;
using JetBrains.Annotations;
using JetBrains.SymbolStorage.Impl.Logger;
using JetBrains.SymbolStorage.Impl.Storages;

namespace JetBrains.SymbolStorage.Impl.Commands
{
  internal sealed class ValidateCommand
  {
    private readonly bool myFix;
    private readonly ILogger myLogger;
    private readonly IStorage myStorage;
    private readonly bool myVerifyAcl;

    public ValidateCommand(
      [NotNull] ILogger logger,
      [NotNull] IStorage storage,
      bool verifyAcl,
      bool fix)
    {
      myLogger = logger ?? throw new ArgumentNullException(nameof(logger));
      myStorage = storage ?? throw new ArgumentNullException(nameof(storage));
      myVerifyAcl = verifyAcl;
      myFix = fix;
    }

    public async Task<int> Execute()
    {
      var validator = new Validator(myLogger, myStorage);
      var storageFormat = await validator.ValidateStorageMarkers();
      var tagItems = await validator.LoadTagItems();
      validator.DumpProducts(tagItems);
      validator.DumpProperties(tagItems);
      var (totalSize, files) = await validator.GatherDataFiles();
      var (statistics, _) = await validator.Validate(tagItems, files, storageFormat, myFix ? Validator.ValidateMode.Fix : Validator.ValidateMode.Validate, myVerifyAcl);
      myLogger.Info($"[{DateTime.Now:s}] Done (size: {totalSize.ToKibibyte()}, warnings: {statistics.Warnings}, errors: {statistics.Errors}, fixes: {statistics.Fixes})");
      return statistics.HasProblems ? 1 : 0;
    }
  }
}