using System;

namespace JetBrains.SymbolStorage.Impl.Storages
{
  internal enum StorageRwMode
  {
    /// <summary>
    /// Read-only access
    /// </summary>
    Read,
    /// <summary>
    /// Only creating new items is allowed
    /// </summary>
    Create,
    /// <summary>
    /// Full access
    /// </summary>
    ReadWrite
  }
}