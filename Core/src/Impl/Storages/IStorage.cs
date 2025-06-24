using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace JetBrains.SymbolStorage.Impl.Storages
{
  internal interface IStorage : IDisposable
  {
    StorageRwMode RwMode { get; }
    
    Task<bool> ExistsAsync(SymbolStoragePath file);
    Task DeleteAsync(SymbolStoragePath file);
    Task RenameAsync(SymbolStoragePath srcFile, SymbolStoragePath dstFile, AccessMode mode);
    Task<long> GetLengthAsync(SymbolStoragePath file);

    bool SupportAccessMode { get; }
    Task<AccessMode> GetAccessModeAsync(SymbolStoragePath file);
    Task SetAccessModeAsync(SymbolStoragePath file, AccessMode mode);
    
    Task<TResult> OpenForReadingAsync<TResult>(SymbolStoragePath file, Func<Stream, Task<TResult>> func);
    Task OpenForReadingAsync(SymbolStoragePath file, Func<Stream, Task> func);
    Task CreateForWritingAsync(SymbolStoragePath file, AccessMode mode, Stream stream);

    Task<bool> IsEmptyAsync();
    
    IAsyncEnumerable<ChildrenItem> GetChildrenAsync(ChildrenMode mode, SymbolStoragePath? prefixDir = null);

    Task InvalidateExternalServicesAsync(IEnumerable<SymbolStoragePath>? fileMasks = null);
  }
}