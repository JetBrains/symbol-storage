using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JetBrains.SymbolStorage.Impl.Storages
{
  [JsonConverter(typeof(SymbolPathJsonConverter))]
  internal readonly struct SymbolPath : IEquatable<SymbolPath>
  {
    public const char DirectorySeparator = '/';
    public const string DirectorySeparatorString = "/";
    
    public static void ValidatePathCorrectness(string? path)
    {
      if (string.IsNullOrEmpty(path))
        throw new ArgumentNullException(nameof(path));
      if (path[0] == DirectorySeparator  || path[^1] == DirectorySeparator)
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

    public static SymbolPath Combine(SymbolPath path1, SymbolPath path2)
    {
      // SymbolPath validation logic forbid `DirectorySeparator` at the beginning and at the end,
      // thus it is safe just to concat paths with DirectorySeparatorString in the middle
      return new SymbolPath(string.Concat(path1.Path, DirectorySeparatorString, path2.Path), validate: false);
    }
    public static SymbolPath Combine(SymbolPath path1, SymbolPath path2, SymbolPath path3)
    {
      // SymbolPath validation logic forbid `DirectorySeparator` at the beginning and at the end,
      // thus it is safe just to concat paths with DirectorySeparatorString in the middle
      return new SymbolPath(string.Concat(path1.Path, DirectorySeparatorString, path2.Path, DirectorySeparatorString, path3.Path), validate: false);
    }


    private SymbolPath(string path, bool validate)
    {
      if (validate)
        ValidatePathCorrectness(path);
      Path = path;
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
    
    public static bool operator ==(SymbolPath left, string right)
    {
      return left.Path == right;
    }
    public static bool operator !=(SymbolPath left, string right)
    {
      return left.Path != right;
    }
  }
  
  
  internal class SymbolPathJsonConverter : JsonConverter<SymbolPath>
  {
    public override SymbolPath Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
      return new SymbolPath(reader.GetString()!);
    }

    public override void Write(Utf8JsonWriter writer, SymbolPath symbolPathValue, JsonSerializerOptions options)
    {
      writer.WriteStringValue(symbolPathValue.Path); 
    }
  }
}