using System;
using System.IO;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace JetBrains.SymbolStorage.Impl.Storages
{
  internal static class StorageUtil
  {
    private static readonly Stream ourStream = new MemoryStream(Array.Empty<byte>(), false);

    public static Task CreateEmpty(this IStorage storage, [NotNull] string file, AccessMode mode)
    {
      return storage.CreateForWriting(file, mode, 0, ourStream);
    }
  }
}