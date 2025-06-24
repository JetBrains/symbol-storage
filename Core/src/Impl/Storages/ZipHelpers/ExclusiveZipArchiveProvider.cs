using System;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace JetBrains.SymbolStorage.Impl.Storages.ZipHelpers
{
  internal class ExclusiveZipArchiveProvider : ZipArchiveProvider
  {
    private readonly SemaphoreSlim myLock;
    private readonly ZipArchive myArchive;

    public ExclusiveZipArchiveProvider(string archivePath, ZipArchiveMode mode = ZipArchiveMode.Update)
    {
      ArchivePath = archivePath;
      myLock = new SemaphoreSlim(1, 1);
      myArchive = ZipFile.Open(archivePath, mode);
    }
    
    public string ArchivePath { get; }
    
    public override async Task<ZipArchiveGuard> RentAsync()
    {
      await myLock.WaitAsync();
      return new ZipArchiveGuard(myArchive, this);
    }

    internal override void Release(ZipArchive archive)
    {
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