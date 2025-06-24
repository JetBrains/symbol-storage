using System;

namespace JetBrains.SymbolStorage.Impl.Storages
{
  [Flags]
  internal enum StorageRwMode
  {
    None = 0,
    Read = 1,
    Write = 2,
    ReadWrite = Read | Write
  }
}