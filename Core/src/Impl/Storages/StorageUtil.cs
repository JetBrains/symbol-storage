#nullable enable

using System;
using System.IO;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace JetBrains.SymbolStorage.Impl.Storages
{
  internal static class StorageUtil
  {
    private static readonly MemoryStream ourEmptyStream = new(Array.Empty<byte>(), false);

    public static Task CreateEmptyAsync(this IStorage storage, string file, AccessMode mode) => storage.CreateForWritingAsync(file, mode, ourEmptyStream);
  }
}