using System;

namespace JetBrains.SymbolStorage.Impl.Storages
{
  internal readonly struct SymbolPath : IEquatable<SymbolPath>
  {
    public static void ValidatePathCorrectness(string? path)
    {
      if (string.IsNullOrEmpty(path))
        throw new ArgumentNullException(nameof(path));
      if (path[0] == '/'  || path[^1] == '/')
        throw new ArgumentException("Symbol path cannot start and end with path separator", nameof(path));
      if (path.Contains('\\'))
        throw new ArgumentException("Only Linux style separators allowed", nameof(path));
    }

    public static SymbolPath FromSystemPath(string path)
    {
      return new SymbolPath(path.NormalizeLinux());
    }
    public static SymbolPath FromSystemPath(string path, string basePath)
    {
      return new SymbolPath(System.IO.Path.GetRelativePath(basePath, path).NormalizeLinux());
    }
    
    public SymbolPath(string path)
    {
      ValidatePathCorrectness(path);
      Path = path;
    }
    
    public string Path { get; }
    

    public override string ToString()
    {
      return Path;
    }

    public override bool Equals(object? obj)
    {
      return obj is SymbolPath path && Equals(path);
    }
    
    public bool Equals(SymbolPath other)
    {
      return Path == other.Path;
    }

    public override int GetHashCode()
    {
      return Path.GetHashCode();
    }

    public static bool operator ==(SymbolPath left, SymbolPath right)
    {
      return left.Path == right.Path;
    }

    public static bool operator !=(SymbolPath left, SymbolPath right)
    {
      return left.Path != right.Path;
    }
  }
}