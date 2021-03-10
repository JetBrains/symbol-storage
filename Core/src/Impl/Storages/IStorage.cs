using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace JetBrains.SymbolStorage.Impl.Storages
{
  internal interface IStorage
  {
    Task<bool> ExistsAsync([NotNull] string file);
    Task DeleteAsync([NotNull] string file);
    Task RenameAsync([NotNull] string srcFile, [NotNull] string dstFile, AccessMode mode);
    Task<long> GetLengthAsync([NotNull] string file);

    bool SupportAccessMode { get; }
    Task<AccessMode> GetAccessModeAsync([NotNull] string file);
    Task SetAccessModeAsync([NotNull] string file, AccessMode mode);
    
    Task<TResult> OpenForReadingAsync<TResult>([NotNull] string file, [NotNull] Func<Stream, Task<TResult>> func);
    Task OpenForReadingAsync([NotNull] string file, [NotNull] Func<Stream, Task> func);
    Task CreateForWritingAsync([NotNull] string file, AccessMode mode, [NotNull] Stream stream);

    Task<bool> IsEmptyAsync();
    
    IAsyncEnumerable<ChildrenItem> GetChildrenAsync(ChildrenMode mode, string prefixDir = null);

    Task InvalidateExternalServicesAsync([CanBeNull] IEnumerable<string> fileMasks = null);
  }
}