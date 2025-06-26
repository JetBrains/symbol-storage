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
    private readonly bool myLoadFileSizes;

    public ListCommand(
      ILogger logger,
      IStorage storage,
      int degreeOfParallelism,
      IdentityFilter identityFilter,
      TimeSpan? minItemAgeFilter,
      bool? protectedFilter,
      bool loadFileSizes)
    {
      myLogger = logger ?? throw new ArgumentNullException(nameof(logger));
      myStorage = storage ?? throw new ArgumentNullException(nameof(storage));
      myDegreeOfParallelism = degreeOfParallelism;
      myIdentityFilter = identityFilter ?? throw new ArgumentNullException(nameof(identityFilter));
      myMinItemAgeFilter = minItemAgeFilter;
      myProtectedFilter = protectedFilter;
      myLoadFileSizes = loadFileSizes;
    }

    public async Task<int> ExecuteAsync()
    {
      var validator = new StorageManager(myLogger, myStorage);
      var (tagItems, _) = await validator.LoadTagItemsAsync(myDegreeOfParallelism, myIdentityFilter, myMinItemAgeFilter, myProtectedFilter);
      validator.DumpProducts(tagItems);
      validator.DumpProperties(tagItems);

      if (myLoadFileSizes)
      {
        var (fileSizes, totalSize) = await validator.GetFileSizesAsync(tagItems);
        validator.DumpFileSizes(fileSizes);
        
        myLogger.Info($"[{DateTime.Now:s}] Done (tags: {tagItems.Count}, totalSize: {totalSize.ToKibibyte()})");
      }
      else
      {
        myLogger.Info($"[{DateTime.Now:s}] Done (tags: {tagItems.Count})");  
      }
      
      return 0;
    }
  }
}