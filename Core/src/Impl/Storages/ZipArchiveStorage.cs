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
    
    public ZipArchiveStorage(string archivePath, StorageRwMode mode, int? cocurrencyLevel = null)
    {
      RwMode = mode;
      myProvider = new ExclusiveZipArchiveProvider(archivePath);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string SymbolPathToZipPath(SymbolStoragePath storagePath)
    {
      return storagePath.Path;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SymbolStoragePath ZipPathToSymbolPath(string zipPath)
    {
      return SymbolStoragePath.FromSystemPath(zipPath);
    }
    
    public StorageRwMode RwMode { get; }
    private bool CanRead => (RwMode & StorageRwMode.Read) != 0;
    private bool CanWrite => (RwMode & StorageRwMode.Write) != 0;
    
    public async Task<bool> ExistsAsync(SymbolStoragePath file)
    {
      if (!CanRead)
        throw new InvalidOperationException("ZipFileStorage created without Read access");

      await Task.Yield();
      using (var archive = await myProvider.RentAsync())
      {
        return archive.Archive.GetEntry(SymbolPathToZipPath(file)) != null;
      }
    }

    public Task DeleteAsync(SymbolStoragePath file)
    {
      throw new NotImplementedException();
    }

    public Task RenameAsync(SymbolStoragePath srcFile, SymbolStoragePath dstFile, AccessMode mode)
    {
      throw new NotImplementedException();
    }

    public Task<long> GetLengthAsync(SymbolStoragePath file)
    {
      throw new NotImplementedException();
    }

    public bool SupportAccessMode => false;
    
    public Task<AccessMode> GetAccessModeAsync(SymbolStoragePath file)
    {
      return Task.FromResult(AccessMode.Unknown);
    }
    public Task SetAccessModeAsync(SymbolStoragePath file, AccessMode mode)
    {
      return Task.CompletedTask;
    }

    public Task<TResult> OpenForReadingAsync<TResult>(SymbolStoragePath file, Func<Stream, Task<TResult>> func)
    {
      throw new NotImplementedException();
    }

    public Task OpenForReadingAsync(SymbolStoragePath file, Func<Stream, Task> func)
    {
      throw new NotImplementedException();
    }

    public Task CreateForWritingAsync(SymbolStoragePath file, AccessMode mode, Stream stream)
    {
      throw new NotImplementedException();
    }

    public Task<bool> IsEmptyAsync()
    {
      throw new NotImplementedException();
    }

    public IAsyncEnumerable<ChildrenItem> GetChildrenAsync(ChildrenMode mode, SymbolStoragePath? prefixDir = null)
    {
      throw new NotImplementedException();
    }

    public Task InvalidateExternalServicesAsync(IEnumerable<SymbolStoragePath>? fileMasks = null)
    {
      throw new NotImplementedException();
    }
    
    public void Dispose()
    {
      myProvider.Dispose();
    }
  }
}