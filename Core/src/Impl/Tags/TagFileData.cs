using JetBrains.SymbolStorage.Impl.Storages;

namespace JetBrains.SymbolStorage.Impl.Tags
{
  internal readonly record struct TagFileData(SymbolPath TagFile, Tag Tag);
}