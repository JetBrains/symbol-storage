namespace JetBrains.SymbolStorage.Impl.Storages
{
  internal enum ZipArchiveStorageRwMode
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
    ReadWrite,
    /// <summary>
    /// Use read access initially, but promote to <see cref="ReadWrite"/> on the first write access
    /// </summary>
    ReadWithAutoWritePromotion
  }
}