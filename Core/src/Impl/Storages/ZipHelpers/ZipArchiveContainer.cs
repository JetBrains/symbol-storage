using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace JetBrains.SymbolStorage.Impl.Storages.ZipHelpers
{
  internal sealed class ZipArchiveContainer: IDisposable
  {
    public static ZipArchiveContainer Open(string archivePath, ZipArchiveMode mode)
    {
      DateTimeOffset openedAt = DateTimeOffset.Now; // Get time before opening the archive
      return new ZipArchiveContainer(archivePath, ZipFile.Open(archivePath, mode), openedAt);
    }
    
    private readonly string myArchivePath;
    private readonly ZipArchiveMode myArchiveMode;
    
    private ZipArchiveContainer(string archivePath, ZipArchive archive, DateTimeOffset openedAt)
    {
      myArchivePath = archivePath;
      myArchiveMode = archive.Mode;
      Archive = archive;
      OpenedAt = openedAt;
      DirtyBytes = 0;
    }
    
    public ZipArchive Archive { get; private set; }
    public DateTimeOffset OpenedAt { get; private set; }
    public long DirtyBytes { get; private set; }
    

    public void AddDirtyBytes(long bytes)
    {
      Debug.Assert(bytes >= 0);
      DirtyBytes += bytes;
    }

    public async Task WriteToArchiveAsync(ZipArchiveEntry entry, Stream data)
    {
      long initialPosition = data.Position;
      await using (var targetStream = entry.Open())
      {
        await data.CopyToAsync(targetStream);
      }
      
      AddDirtyBytes(data.Position - initialPosition);
    }

    /// <summary>
    /// Reopens archive file
    /// </summary>
    /// <remarks>
    /// In <c>ZipArchiveMode.Update</c> the archive stores all data in memory,
    /// thus at some point it is better to sync its content to the disk.
    /// This can be done by reopening the archive file
    /// </remarks>
    public void Reopen()
    {
      Archive.Dispose();
      OpenedAt = DateTimeOffset.Now; // Get time before opening the archive
      Archive = ZipFile.Open(myArchivePath, myArchiveMode);
      DirtyBytes = 0;
    }

    public void Dispose()
    {
      Archive.Dispose();
    }
  }
}