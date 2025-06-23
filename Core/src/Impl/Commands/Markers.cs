using JetBrains.SymbolStorage.Impl.Storages;

namespace JetBrains.SymbolStorage.Impl.Commands
{
  internal static class Markers
  {
    public static readonly SymbolStoragePath Flat = new SymbolStoragePath("flat.txt");
    public static readonly SymbolStoragePath SingleTier = new SymbolStoragePath("pingme.txt");
    public static readonly SymbolStoragePath TwoTier = new SymbolStoragePath("index2.txt");

    public static readonly SymbolStoragePath LowerCase = new SymbolStoragePath("_jb.lowercase");
    public static readonly SymbolStoragePath UpperCase = new SymbolStoragePath("_jb.uppercase");
  }
}