using System;
using System.IO;
using System.Threading.Tasks;
using JetBrains.Annotations;
using JetBrains.SymbolStorage.Impl.Logger;
using JetBrains.SymbolStorage.Impl.Storages;
using JetBrains.SymbolStorage.Impl.Tags;

namespace JetBrains.SymbolStorage.Impl.Commands
{
  internal sealed class ProtectedCommand : ICommand
  {
    private readonly ILogger myLogger;
    private readonly IStorage myStorage;
    private readonly int myDegreeOfParallelism;
    private readonly IdentityFilter myIdentityFilter;
    private readonly bool myIsProtected;

    public ProtectedCommand(
      [NotNull] ILogger logger,
      [NotNull] IStorage storage,
      int degreeOfParallelism,
      [NotNull] IdentityFilter identityFilter,
      bool isProtected)
    {
      myLogger = logger ?? throw new ArgumentNullException(nameof(logger));
      myStorage = storage ?? throw new ArgumentNullException(nameof(storage));
      myDegreeOfParallelism = degreeOfParallelism;
      myIdentityFilter = identityFilter ?? throw new ArgumentNullException(nameof(identityFilter));
      myIsProtected = isProtected;
    }

    public async Task<int> ExecuteAsync()
    {
      var validator = new Validator(myLogger, myStorage);
      await validator.ValidateStorageMarkersAsync();
      var (tagItems, _) = await validator.LoadTagItemsAsync(myDegreeOfParallelism, myIdentityFilter, TimeSpan.Zero, !myIsProtected);
      validator.DumpProducts(tagItems);
      validator.DumpProperties(tagItems);

      myLogger.Info($"[{DateTime.Now:s}] Updating tag files");
      await tagItems.ParallelFor(myDegreeOfParallelism, async tagItem =>
        {
          var tagFile = tagItem.Key;
          myLogger.Verbose($"  Updating {tagFile}...");

          var tag = tagItem.Value.Clone();
          tag.IsProtected = myIsProtected;
         
          await using var stream = new MemoryStream();
          await TagUtil.WriteTagScriptAsync(tag, stream);
          await myStorage.CreateForWritingAsync(tagFile, AccessMode.Private, stream);
        });
     myLogger.Info($"[{DateTime.Now:s}] Done (tags: {tagItems.Count})");
      return 0;
    }
  }
}