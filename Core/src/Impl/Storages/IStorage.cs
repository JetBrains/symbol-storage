using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace JetBrains.SymbolStorage.Impl.Storages
{
  internal interface IStorage
  {
    Task<bool> Exists([NotNull] string file);
    Task Delete([NotNull] string file);
    Task Rename([NotNull] string file, [NotNull] string newFile, AccessMode mode);
    Task<long> GetLength([NotNull] string file);

    bool SupportAccessMode { get; }
    Task<AccessMode> GetAccessMode([NotNull] string file);
    Task SetAccessMode([NotNull] string file, AccessMode mode);
    
    Task<TResult> OpenForReading<TResult>([NotNull] string file, [NotNull] Func<Stream, TResult> func);
    Task OpenForReading([NotNull] string file, [NotNull] Action<Stream> action);
    Task CreateForWriting([NotNull] string file, AccessMode mode, long length, [NotNull] Stream stream);

    Task<bool> IsEmpty();
    
    IAsyncEnumerable<ChildrenItem> GetChildren(ChildrenMode mode, string prefixDir = null);

    Task InvalidateExternalServices([CanBeNull] IEnumerable<string> keys = null);
  }
}