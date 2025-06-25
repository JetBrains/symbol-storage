using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.SymbolStorage.Impl.Logger;
using JetBrains.SymbolStorage.Impl.Storages;
using JetBrains.SymbolStorage.Impl.Tags;

namespace JetBrains.SymbolStorage.Impl.Commands
{
  internal sealed class ProtectedCommand : IStatsReportingCommand
  {
    private readonly ILogger myLogger;
    private readonly IStorage myStorage;
    private readonly int myDegreeOfParallelism;
    private readonly IdentityFilter myIdentityFilter;
    private readonly bool myIsProtected;
    private long mySubOpsCount;

    public ProtectedCommand(
      ILogger logger,
      IStorage storage,
      int degreeOfParallelism,
      IdentityFilter identityFilter,
      bool isProtected)
    {
      myLogger = logger ?? throw new ArgumentNullException(nameof(logger));
      myStorage = storage ?? throw new ArgumentNullException(nameof(storage));
      myDegreeOfParallelism = degreeOfParallelism;
      myIdentityFilter = identityFilter ?? throw new ArgumentNullException(nameof(identityFilter));
      myIsProtected = isProtected;
    }
    
    public long SubOperationsCount => Volatile.Read(ref mySubOpsCount);

    public async Task<int> ExecuteAsync()
    {
      Volatile.Write(ref mySubOpsCount, 0);
      
      var validator = new StorageManager(myLogger, myStorage);
      await validator.ValidateStorageMarkersAsync();
      var (tagItems, _) = await validator.LoadTagItemsAsync(myDegreeOfParallelism, myIdentityFilter, null, !myIsProtected);
      validator.DumpProducts(tagItems);
      validator.DumpProperties(tagItems);

      myLogger.Info($"[{DateTime.Now:s}] Updating tag files");
      await tagItems.ParallelForAsync(myDegreeOfParallelism, async tagItem =>
        {
          Interlocked.Increment(ref mySubOpsCount);
          var tagFile = tagItem.TagFile;
          myLogger.Verbose($"  Updating {tagFile}...");

          var tag = tagItem.Tag with
          {
            IsProtected = myIsProtected
          };
         
          using var stream = new MemoryStream();
          await TagUtil.WriteTagScriptAsync(tag, stream);
          await myStorage.CreateForWritingAsync(tagFile, AccessMode.Private, stream);
        });
      myLogger.Info($"[{DateTime.Now:s}] Done (tags: {tagItems.Count})");
      return 0;
    }
  }
}