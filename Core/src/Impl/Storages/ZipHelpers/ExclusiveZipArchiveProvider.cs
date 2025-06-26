using System;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace JetBrains.SymbolStorage.Impl.Storages.ZipHelpers
{
  internal class ExclusiveZipArchiveProvider : ZipArchiveProvider
  {
    private readonly SemaphoreSlim myLock;
    private readonly ZipArchiveContainer myArchive;
    private readonly ZipArchiveMode myArchiveMode;
    private readonly long myMaxDirtyBytes;

    public ExclusiveZipArchiveProvider(string archivePath, ZipArchiveMode mode = ZipArchiveMode.Update, long maxDirtyBytes = long.MaxValue)
    {
      ArchivePath = archivePath;
      myLock = new SemaphoreSlim(1, 1);
      myArchive = ZipArchiveContainer.Open(archivePath, mode);
      myArchiveMode = mode;
      myMaxDirtyBytes = maxDirtyBytes;
      Mode = mode;
    }
    
    public string ArchivePath { get; }
    public override ZipArchiveMode Mode { get; }

    public override async Task<ZipArchiveGuard> RentAsync()
    {
      await myLock.WaitAsync();
      return new ZipArchiveGuard(myArchive, this);
    }

    internal override void Release(ZipArchiveContainer archive)
    {
      if (myArchiveMode == ZipArchiveMode.Update && archive.DirtyBytes > myMaxDirtyBytes)
      {
        archive.Reopen();
      }
      
      myLock.Release();
    }

    protected override void Dispose(bool disposing)
    {
      if (disposing)
      {
        myLock.Wait();
        try
        {
          myArchive.Dispose();
        }
        finally
        {
          myLock.Release();
        }
      }
    }
  }
}