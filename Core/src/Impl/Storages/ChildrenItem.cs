namespace JetBrains.SymbolStorage.Impl.Storages
{
  internal readonly record struct ChildrenItem(SymbolStoragePath FileName, long? Size);
}