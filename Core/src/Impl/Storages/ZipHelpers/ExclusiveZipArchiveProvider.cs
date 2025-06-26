using System;
using System.Diagnostics;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace JetBrains.SymbolStorage.Impl.Storages.ZipHelpers
{
  internal class ExclusiveZipArchiveProvider : ZipArchiveProvider
  {
    private static ZipArchiveMode ZipArchiveStorageRwModeToZipArchiveMode(ZipArchiveStorageRwMode mode) => mode switch
    {
      ZipArchiveStorageRwMode.Create => ZipArchiveMode.Create,
      ZipArchiveStorageRwMode.Read => ZipArchiveMode.Read,
      ZipArchiveStorageRwMode.ReadWrite => ZipArchiveMode.Update,
      ZipArchiveStorageRwMode.ReadWithAutoWritePromotion => ZipArchiveMode.Read,
      _ => throw new ArgumentException($"Unknown ZipArchiveStorageRwMode: {mode}", nameof(mode))
    };
    
    private readonly SemaphoreSlim myLock;
    private readonly ZipArchiveContainer myArchive;
    private readonly long myMaxDirtyBytes;
    private volatile bool myIsDisposed;

    public ExclusiveZipArchiveProvider(string archivePath, ZipArchiveStorageRwMode mode = ZipArchiveStorageRwMode.ReadWrite, long maxDirtyBytes = long.MaxValue)
    {
      ArchivePath = archivePath;
      myLock = new SemaphoreSlim(1, 1);
      myArchive = ZipArchiveContainer.Open(archivePath, ZipArchiveStorageRwModeToZipArchiveMode(mode));
      myMaxDirtyBytes = maxDirtyBytes;
      Mode = mode;
      myIsDisposed = false;
    }
    
    public string ArchivePath { get; }
    public override ZipArchiveStorageRwMode Mode { get; }

    public override async Task<ZipArchiveGuard> RentAsync(bool writable)
    {
      Debug.Assert(
        (writable && (Mode == ZipArchiveStorageRwMode.Create || Mode == ZipArchiveStorageRwMode.ReadWrite || Mode == ZipArchiveStorageRwMode.ReadWithAutoWritePromotion)) ||
        (!writable && (Mode == ZipArchiveStorageRwMode.Read || Mode == ZipArchiveStorageRwMode.ReadWrite || Mode == ZipArchiveStorageRwMode.ReadWithAutoWritePromotion)));
      
      await myLock.WaitAsync();
      if (writable && Mode == ZipArchiveStorageRwMode.ReadWithAutoWritePromotion && myArchive.CurrentArchiveMode != ZipArchiveMode.Update)
        myArchive.PromoteToUpdateModeIfNeeded();
      
      return new ZipArchiveGuard(myArchive, this);
    }

    internal override void Release(ZipArchiveContainer archive)
    {
      if (archive.CurrentArchiveMode == ZipArchiveMode.Update && archive.DirtyBytes > myMaxDirtyBytes)
      {
        archive.Reopen();
      }
      
      myLock.Release();
    }

    protected override void Dispose(bool disposing)
    {
      if (disposing && !myIsDisposed)
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

        myLock.Dispose();
        myIsDisposed = true;
      }
    }
  }
}