using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace JetBrains.SymbolStorage.Impl.Storages
{
  internal interface IStorage : IDisposable
  {
    Task<bool> ExistsAsync(SymbolPath file);
    Task DeleteAsync(SymbolPath file);
    Task RenameAsync(SymbolPath srcFile, SymbolPath dstFile, AccessMode mode);
    Task<long> GetLengthAsync(SymbolPath file);

    bool SupportAccessMode { get; }
    Task<AccessMode> GetAccessModeAsync(SymbolPath file);
    Task SetAccessModeAsync(SymbolPath file, AccessMode mode);
    
    Task<TResult> OpenForReadingAsync<TResult>(SymbolPath file, Func<Stream, Task<TResult>> func);
    Task OpenForReadingAsync(SymbolPath file, Func<Stream, Task> func);
    Task CreateForWritingAsync(SymbolPath file, AccessMode mode, Stream stream);

    Task<bool> IsEmptyAsync();
    
    IAsyncEnumerable<ChildrenItem> GetChildrenAsync(ChildrenMode mode, SymbolPath? prefixDir = null);

    Task InvalidateExternalServicesAsync(IEnumerable<SymbolPath>? fileMasks = null);
  }
}