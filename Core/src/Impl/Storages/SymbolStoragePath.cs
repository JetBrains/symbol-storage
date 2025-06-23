using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JetBrains.SymbolStorage.Impl.Storages
{
  [JsonConverter(typeof(SymbolPathJsonConverter))]
  internal readonly struct SymbolStoragePath : IEquatable<SymbolStoragePath>, IComparable<SymbolStoragePath>
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

    public static SymbolStoragePath FromSystemPath(string path)
    {
      return new SymbolStoragePath(path.NormalizeLinux());
    }
    public static SymbolStoragePath FromSystemPath(string path, string basePath)
    {
      return new SymbolStoragePath(System.IO.Path.GetRelativePath(basePath, path).NormalizeLinux());
    }

    public static SymbolStoragePath Combine(SymbolStoragePath path1, SymbolStoragePath path2)
    {
      // SymbolPath validation logic forbid `DirectorySeparator` at the beginning and at the end,
      // thus it is safe just to concat paths with DirectorySeparatorString in the middle
      return new SymbolStoragePath(string.Concat(path1.Path, DirectorySeparatorString, path2.Path), validate: false);
    }
    public static SymbolStoragePath Combine(SymbolStoragePath path1, SymbolStoragePath path2, SymbolStoragePath path3)
    {
      // SymbolPath validation logic forbid `DirectorySeparator` at the beginning and at the end,
      // thus it is safe just to concat paths with DirectorySeparatorString in the middle
      return new SymbolStoragePath(string.Concat(path1.Path, DirectorySeparatorString, path2.Path, DirectorySeparatorString, path3.Path), validate: false);
    }

    public static ReadOnlySpan<char> GetDirectoryNameAsSpan(SymbolStoragePath storagePath)
    {
      var pathStr = storagePath.Path;
      int end = pathStr.Length - 1;
      while (end >= 0 && pathStr[end] != DirectorySeparator)
        end--;
      while (end >= 0 && pathStr[end] == DirectorySeparator)
        end--;

      return end < 0 ? "".AsSpan() : storagePath.Path.AsSpan(0, end + 1);
    }
    public static SymbolStoragePath? GetDirectoryName(SymbolStoragePath storagePath)
    {
      var dirSpan = GetDirectoryNameAsSpan(storagePath);
      if (dirSpan.Length == 0)
        return null;
      return new SymbolStoragePath(new string(dirSpan), validate: false);
    }
    
    public static ReadOnlySpan<char> GetFileNameAsSpan(SymbolStoragePath storagePath)
    {
      int separatorIndex = storagePath.Path.LastIndexOf(DirectorySeparator);
      return separatorIndex < 0 ? storagePath.Path.AsSpan() : storagePath.Path.AsSpan(separatorIndex + 1);
    }
    public static SymbolStoragePath GetFileName(SymbolStoragePath storagePath)
    {
      var fileNameSpan = GetFileNameAsSpan(storagePath);
      if (fileNameSpan.Length == storagePath.Path.Length)
        return storagePath;
      return new SymbolStoragePath(new string(fileNameSpan), validate: false);
    }
    

    private SymbolStoragePath(string path, bool validate)
    {
      if (validate)
        ValidatePathCorrectness(path);
      Path = path;
    }
    
    public SymbolStoragePath(string path)
    {
      ValidatePathCorrectness(path);
      Path = path;
    }
    
    public string Path { get; }

    public MemoryExtensions.SpanSplitEnumerator<char> GetPathComponents()
    {
      return Path.AsSpan().Split(DirectorySeparator);
    }

    public override string ToString()
    {
      return Path;
    }

    public override bool Equals(object? obj)
    {
      return obj is SymbolStoragePath path && Equals(path);
    }
    
    public bool Equals(SymbolStoragePath other)
    {
      return Path == other.Path;
    }
    
    public int CompareTo(SymbolStoragePath other)
    {
      return string.Compare(Path, other.Path, StringComparison.Ordinal);
    }

    public override int GetHashCode()
    {
      return Path.GetHashCode();
    }

    public static bool operator ==(SymbolStoragePath left, SymbolStoragePath right)
    {
      return left.Path == right.Path;
    }
    public static bool operator !=(SymbolStoragePath left, SymbolStoragePath right)
    {
      return left.Path != right.Path;
    }
    
    public static bool operator ==(SymbolStoragePath left, string right)
    {
      return left.Path == right;
    }
    public static bool operator !=(SymbolStoragePath left, string right)
    {
      return left.Path != right;
    }
  }
  
  
  internal class SymbolPathJsonConverter : JsonConverter<SymbolStoragePath>
  {
    public override SymbolStoragePath Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
      return new SymbolStoragePath(reader.GetString()!);
    }

    public override void Write(Utf8JsonWriter writer, SymbolStoragePath symbolStoragePathValue, JsonSerializerOptions options)
    {
      writer.WriteStringValue(symbolStoragePathValue.Path); 
    }
  }
}