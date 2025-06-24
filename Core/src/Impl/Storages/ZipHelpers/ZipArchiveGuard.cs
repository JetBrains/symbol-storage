using System;
using System.IO.Compression;

namespace JetBrains.SymbolStorage.Impl.Storages.ZipHelpers
{
  internal struct ZipArchiveGuard(ZipArchive archive, ZipArchiveProvider provider) : IDisposable
  {
    private ZipArchiveProvider? myProvider = provider;

    public ZipArchive Archive { get; } = archive;

    public void Dispose()
    {
      myProvider?.Release(Archive);
      myProvider = null;
    }
  }
}