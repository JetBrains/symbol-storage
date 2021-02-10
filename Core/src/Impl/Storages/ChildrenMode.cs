using System;

namespace JetBrains.SymbolStorage.Impl.Storages
{
  [Flags]
  internal enum ChildrenMode
  {
    Default = 0,
    WithSize = 0x1
  }
}