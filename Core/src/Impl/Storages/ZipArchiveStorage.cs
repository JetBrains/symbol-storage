using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using JetBrains.SymbolStorage.Impl.Storages.ZipHelpers;

namespace JetBrains.SymbolStorage.Impl.Storages
{
  internal class ZipArchiveStorage : IStorage
  {
    private readonly ZipArchiveProvider myProvider;
    
    public ZipArchiveStorage(string archivePath, ZipArchiveStorageRwMode mode, int? concurrencyLevel = null, long? maxDirtyBytes = null)
    {
      RwMode = mode switch
      {
        ZipArchiveStorageRwMode.Read => StorageRwMode.Read,
        ZipArchiveStorageRwMode.Create => StorageRwMode.Create,
        ZipArchiveStorageRwMode.ReadWrite => StorageRwMode.ReadWrite,
        ZipArchiveStorageRwMode.ReadWithAutoWritePromotion => StorageRwMode.ReadWrite,
        _ => throw new ArgumentException("Unknown RW Mode: " + mode.ToString())
      };

      myProvider = mode switch
      {
        ZipArchiveStorageRwMode.Read => new PooledZipArchiveProvider(archivePath, concurrencyLevel: concurrencyLevel ?? 8),
        ZipArchiveStorageRwMode.Create => new ExclusiveZipArchiveProvider(archivePath, mode, maxDirtyBytes: long.MaxValue),
        ZipArchiveStorageRwMode.ReadWrite => new ExclusiveZipArchiveProvider(archivePath, mode, maxDirtyBytes: maxDirtyBytes ?? long.MaxValue),
        ZipArchiveStorageRwMode.ReadWithAutoWritePromotion => new ExclusiveZipArchiveProvider(archivePath, mode, maxDirtyBytes: maxDirtyBytes ?? long.MaxValue),
        _ => throw new ArgumentException("Unknown RW Mode: " + mode.ToString())
      };
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string SymbolPathToZipPath(SymbolStoragePath storagePath)
    {
      return storagePath.Path;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SymbolStoragePath ZipPathToSymbolPath(string zipPath)
    {
      return new SymbolStoragePath(zipPath);
    }
    
    public StorageRwMode RwMode { get; }
    private bool CanRead => RwMode == StorageRwMode.Read || RwMode == StorageRwMode.ReadWrite;
    private bool CanCreate => RwMode == StorageRwMode.Create || RwMode == StorageRwMode.ReadWrite;
    private bool CanWrite => RwMode == StorageRwMode.ReadWrite;
    
    public async Task<bool> ExistsAsync(SymbolStoragePath file)
    {
      if (!CanRead)
        throw new InvalidOperationException("ZipFileStorage created without Read access");

      await Task.Yield();
      using (var archive = await myProvider.RentAsync(writable: false))
      {
        return archive.Archive.GetEntry(SymbolPathToZipPath(file)) != null;
      }
    }

    public async Task DeleteAsync(SymbolStoragePath file)
    {
      if (!CanWrite)
        throw new InvalidOperationException("ZipFileStorage created without Write access");

      await Task.Yield();
      using (var archive = await myProvider.RentAsync(writable: true))
      {
        var entry = archive.Archive.GetEntry(SymbolPathToZipPath(file));
        entry?.Delete();
      }
    }

    public async Task RenameAsync(SymbolStoragePath srcFile, SymbolStoragePath dstFile, AccessMode mode)
    {
      if (!CanWrite)
        throw new InvalidOperationException("ZipFileStorage created without Write access");

      await Task.Yield();
      using (var archive = await myProvider.RentAsync(writable: true))
      {
        var srcEntry = archive.Archive.GetEntry(SymbolPathToZipPath(srcFile));
        if (srcEntry == null)
          throw new KeyNotFoundException($"Specified file ({srcFile}) was not found in zip storage");

        var targetEntry = archive.Archive.CreateEntry(SymbolPathToZipPath(dstFile));

        await using (var srcStream = srcEntry.Open())
        {
          await archive.WriteToArchiveAsync(targetEntry, srcStream);
        }
        
        srcEntry.Delete();
      }
    }

    public async Task<long> GetLengthAsync(SymbolStoragePath file)
    {
      if (!CanRead)
        throw new InvalidOperationException("ZipFileStorage created without Read access");

      await Task.Yield();
      using (var archive = await myProvider.RentAsync(writable: false))
      {
        var entry = archive.Archive.GetEntry(SymbolPathToZipPath(file));
        if (entry == null)
          throw new KeyNotFoundException($"Specified file ({file}) was not found in zip storage");

        return archive.GetEntryLength(entry);
      }
    }

    public bool SupportAccessMode => false;
    
    public async Task<AccessMode> GetAccessModeAsync(SymbolStoragePath file)
    {
      if (!CanRead)
        throw new InvalidOperationException("ZipFileStorage created without Read access");
      
      return await ValueTask.FromResult(AccessMode.Unknown);
    }
    public async Task SetAccessModeAsync(SymbolStoragePath file, AccessMode mode)
    {
      if (!CanWrite)
        throw new InvalidOperationException("ZipFileStorage created without Write access");
      
      await Task.CompletedTask;
    }

    public async Task<TResult> OpenForReadingAsync<TResult>(SymbolStoragePath file, Func<Stream, Task<TResult>> func)
    {
      if (!CanRead)
        throw new InvalidOperationException("ZipFileStorage created without Read access");

      await Task.Yield();
      using (var archive = await myProvider.RentAsync(writable: false))
      {
        var entry = archive.Archive.GetEntry(SymbolPathToZipPath(file));
        if (entry == null)
          throw new KeyNotFoundException($"Specified file ({file}) was not found in zip storage");

        await using (var srcStream = entry.Open())
        {
          return await func(srcStream);
        }
      }
    }

    public Task OpenForReadingAsync(SymbolStoragePath file, Func<Stream, Task> func) => OpenForReadingAsync(file, async x =>
    {
      await func(x);
      return true;
    });

    public async Task CreateForWritingAsync(SymbolStoragePath file, AccessMode mode, Stream stream)
    {
      if (!CanCreate)
        throw new InvalidOperationException("ZipFileStorage created without Write or Create access");

      await Task.Yield();
      using (var archive = await myProvider.RentAsync(writable: true))
      {
        if (CanWrite)
        {
          var existedEntry = archive.Archive.GetEntry(SymbolPathToZipPath(file));
          existedEntry?.Delete();
        }

        var entry = archive.Archive.CreateEntry(SymbolPathToZipPath(file));
        await archive.WriteToArchiveAsync(entry, stream);
      }
    }

    public async Task<bool> IsEmptyAsync()
    {
      if (!CanRead)
        throw new InvalidOperationException("ZipFileStorage created without Read access");
      
      await Task.Yield();
      using (var archive = await myProvider.RentAsync(writable: false))
      {
        return archive.Archive.Entries.Count == 0;
      }
    }

    public async IAsyncEnumerable<ChildrenItem> GetChildrenAsync(ChildrenMode mode, SymbolStoragePath? prefixDir = null)
    {
      if (!CanRead)
        throw new InvalidOperationException("ZipFileStorage created without Read access");
      
      await Task.Yield();
      using (var archive = await myProvider.RentAsync(writable: false))
      {
        string? prefix = prefixDir != null ? SymbolPathToZipPath(prefixDir.Value) + "/" : null;
        
        foreach (var zipArchiveEntry in archive.Archive.Entries)
        {
          if (prefix != null && !zipArchiveEntry.FullName.StartsWith(prefix))
            continue;
          
          yield return new ChildrenItem(
            ZipPathToSymbolPath(zipArchiveEntry.FullName),
            mode == ChildrenMode.WithSize ? archive.GetEntryLength(zipArchiveEntry) : null
          );
        }
      }
    }

    public Task InvalidateExternalServicesAsync(IEnumerable<SymbolStoragePath>? fileMasks = null)
    {
      return Task.CompletedTask;
    }
    
    public void Dispose()
    {
      myProvider.Dispose();
    }
  }
}