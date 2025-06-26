using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace JetBrains.SymbolStorage.Impl.Storages.ZipHelpers
{
  internal class PooledZipArchiveProvider : ZipArchiveProvider
  {
    private readonly SemaphoreSlim myConcurrencyLimiter;
    private readonly ConcurrentStack<ZipArchiveContainer> myContainers;
    private volatile bool myIsDisposed;
    
    public PooledZipArchiveProvider(string archivePath, int concurrencyLevel)
    {
      if (concurrencyLevel < 1)
        throw new ArgumentOutOfRangeException(nameof(concurrencyLevel));
      
      myConcurrencyLimiter = new SemaphoreSlim(concurrencyLevel);
      ConcurrencyLevel = concurrencyLevel;
      ArchivePath = archivePath;
      myContainers = new ConcurrentStack<ZipArchiveContainer>();
      
      myContainers.Push(ZipArchiveContainer.Open(archivePath, ZipArchiveMode.Read));

      myIsDisposed = false;
    }
    
    public string ArchivePath { get; }
    public int ConcurrencyLevel { get; }
    
    public override ZipArchiveStorageRwMode Mode => ZipArchiveStorageRwMode.Read;
    
    
    public override async Task<ZipArchiveGuard> RentAsync(bool writable)
    {
      if (writable)
        throw new InvalidOperationException("PooledZipArchiveProvider operates only in read-only mode");
      
      await myConcurrencyLimiter.WaitAsync();
      if (myContainers.TryPop(out var container))
        return new ZipArchiveGuard(container, this);
      
      return new ZipArchiveGuard(ZipArchiveContainer.Open(ArchivePath, ZipArchiveMode.Read), this);
    }

    internal override void Release(ZipArchiveContainer archive)
    {
      Debug.Assert(archive.CurrentArchiveMode == ZipArchiveMode.Read);
      myContainers.Push(archive);
      myConcurrencyLimiter.Release();
    }

    protected override void Dispose(bool disposing)
    {
      if (disposing && !myIsDisposed)
      {
        for (int i = 0; i < ConcurrencyLevel; i++)
          myConcurrencyLimiter.Wait();

        try
        {
          while (myContainers.TryPop(out var container))
            container.Dispose();
        }
        finally
        {
          myConcurrencyLimiter.Release(ConcurrencyLevel);
        }
        
        myConcurrencyLimiter.Dispose();
        myIsDisposed = true;
      }
    }
  }
}