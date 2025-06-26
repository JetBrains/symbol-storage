using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace JetBrains.SymbolStorage.Impl.Storages.ZipHelpers
{
  internal struct ZipArchiveGuard(ZipArchiveContainer archiveContainer, ZipArchiveProvider provider) : IDisposable
  {
    private ZipArchiveProvider? myProvider = provider;

    public ZipArchive Archive => archiveContainer.Archive;

    public bool IsNewlyCreatedEntry(ZipArchiveEntry entry)
    {
      return entry.LastWriteTime >= archiveContainer.OpenedAt;
    }

    /// <summary>
    /// Returns the length of the entry
    /// </summary>
    /// <remarks>
    /// If archive in <c>ZipArchiveMode.Update</c> mode and entry is newly created then <c>entry.Length</c> is not available.
    /// In this mode the archive is in memory, so it is safe to open the stream and get its length
    /// </remarks>
    public long GetEntryLength(ZipArchiveEntry entry)
    {
      Debug.Assert(myProvider != null);
      if (myProvider.Mode == ZipArchiveMode.Update && IsNewlyCreatedEntry(entry))
      {
        using (var stream = entry.Open())
        {
          return stream.Length;
        }
      }
      
      return entry.Length;
    }

    public void AddDirtyBytes(long bytes)
    {
      Debug.Assert(myProvider != null);
      archiveContainer.AddDirtyBytes(bytes);
    }
    public Task WriteToArchiveAsync(ZipArchiveEntry entry, Stream data)
    {
      Debug.Assert(myProvider != null);
      return archiveContainer.WriteToArchiveAsync(entry, data);
    }
    
    public void Dispose()
    {
      myProvider?.Release(archiveContainer);
      myProvider = null;
    }
  }
}