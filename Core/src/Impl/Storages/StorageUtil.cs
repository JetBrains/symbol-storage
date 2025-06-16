using System;
using System.IO;
using System.Threading.Tasks;

namespace JetBrains.SymbolStorage.Impl.Storages
{
  internal static class StorageUtil
  {
    private static readonly MemoryStream ourEmptyStream = new([], false);

    public static Task CreateEmptyAsync(this IStorage storage, string file, AccessMode mode) => storage.CreateForWritingAsync(file, mode, ourEmptyStream);
  }
}