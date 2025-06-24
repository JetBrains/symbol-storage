using System;
using System.IO.Compression;
using System.Threading.Tasks;

namespace JetBrains.SymbolStorage.Impl.Storages.ZipHelpers
{
  internal abstract class ZipArchiveProvider : IDisposable
  {
    public abstract Task<ZipArchiveGuard> RentAsync();
    internal abstract void Release(ZipArchive archive);


    protected virtual void Dispose(bool disposing)
    {
      
    }
    public void Dispose()
    {
      Dispose(disposing: true);
      GC.SuppressFinalize(this);
    }
  }
}