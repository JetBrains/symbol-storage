using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace JetBrains.SymbolStorage.Impl.Storages
{
  internal interface IStorage : IDisposable
  {
    Task<bool> ExistsAsync(string file);
    Task DeleteAsync(string file);
    Task RenameAsync(string srcFile, string dstFile, AccessMode mode);
    Task<long> GetLengthAsync(string file);

    bool SupportAccessMode { get; }
    Task<AccessMode> GetAccessModeAsync(string file);
    Task SetAccessModeAsync(string file, AccessMode mode);
    
    Task<TResult> OpenForReadingAsync<TResult>(string file, Func<Stream, Task<TResult>> func);
    Task OpenForReadingAsync(string file, Func<Stream, Task> func);
    Task CreateForWritingAsync(string file, AccessMode mode, Stream stream);

    Task<bool> IsEmptyAsync();
    
    IAsyncEnumerable<ChildrenItem> GetChildrenAsync(ChildrenMode mode, string? prefixDir = null);

    Task InvalidateExternalServicesAsync(IEnumerable<string>? fileMasks = null);
  }
}