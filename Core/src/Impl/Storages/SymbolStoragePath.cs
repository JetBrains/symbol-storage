using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JetBrains.SymbolStorage.Impl.Storages
{
  [JsonConverter(typeof(SymbolPathJsonConverter))]
  internal readonly struct SymbolStoragePath : IEquatable<SymbolStoragePath>, IComparable<SymbolStoragePath>
  {
    public const char DirectorySeparator = '/';
    public const string DirectorySeparatorString = "/";
    
    public static void ValidatePathCorrectness(ReadOnlySpan<char> path)
    {
      if (path.Length == 0)
        throw new ArgumentException("Symbol path cannot be empty", nameof(path));
      if (path[0] == DirectorySeparator  || path[^1] == DirectorySeparator)
        throw new ArgumentException("Symbol path cannot start and end with path separator", nameof(path));
      if (path.Contains('\\'))
        throw new ArgumentException("Only Linux style separators allowed", nameof(path));
    }
    public static void ValidatePathCorrectness(string? path)
    {
      if (path == null)
        throw new ArgumentNullException(nameof(path));
      ValidatePathCorrectness(path.AsSpan());
    }

    public static SymbolStoragePath FromSystemPath(string path)
    {
      return new SymbolStoragePath(path.NormalizeLinux());
    }
    public static SymbolStoragePath FromSystemPath(string path, string basePath)
    {
      return new SymbolStoragePath(System.IO.Path.GetRelativePath(basePath, path).NormalizeLinux());
    }

    public static SymbolStoragePath? FromRef(SymbolStoragePathRef path)
    {
      if (path.IsEmpty)
        return null;

      return new SymbolStoragePath(new string(path.Path), validate: false);
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

    public static SymbolStoragePathRef GetDirectoryName(SymbolStoragePathRef storagePath)
    {
      var pathStr = storagePath.Path;
      int end = pathStr.Length - 1;
      while (end >= 0 && pathStr[end] != DirectorySeparator)
        end--;
      while (end >= 0 && pathStr[end] == DirectorySeparator)
        end--;

      return end < 0 ? SymbolStoragePathRef.Empty : SymbolStoragePathRef.CreateUnsafe(storagePath.Path.Slice(0, end + 1));
    }
    public static SymbolStoragePath? GetDirectoryName(SymbolStoragePath storagePath)
    {
      return GetDirectoryName(storagePath.AsRef()).IntoOwned();
    }
    
    public static SymbolStoragePathRef GetFileName(SymbolStoragePathRef storagePath)
    {
      int separatorIndex = storagePath.Path.LastIndexOf(DirectorySeparator);
      return separatorIndex < 0 ? storagePath : SymbolStoragePathRef.CreateUnsafe(storagePath.Path.Slice(separatorIndex + 1));
    }
    public static SymbolStoragePath GetFileName(SymbolStoragePath storagePath)
    {
      var fileName = GetFileName(storagePath.AsRef());
      return fileName.Path.Length == storagePath.Path.Length ? storagePath : fileName.IntoOwned()!.Value;
    }
    
    public static ReadOnlySpan<char> GetExtension(SymbolStoragePathRef storagePath)
    {
      return System.IO.Path.GetExtension(storagePath.Path);
    }
    public static string GetExtension(SymbolStoragePath storagePath)
    {
      return System.IO.Path.GetExtension(storagePath.Path);
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
    public int Length => Path.Length;
    public char this[int index] => Path[index];

    public SymbolStoragePathRef AsRef()
    {
      return new SymbolStoragePathRef(this);
    }
    
    public string[] GetPathComponents()
    {
      return Path.Split(DirectorySeparator);
    }

    public SymbolStoragePath ToLower()
    {
      return new SymbolStoragePath(Path.ToLowerInvariant(), validate: false);
    }
    public SymbolStoragePath ToUpper()
    {
      return new SymbolStoragePath(Path.ToUpperInvariant(), validate: false);
    }

    public override string ToString()
    {
      return Path;
    }

    public override bool Equals([NotNullWhen(true)] object? obj)
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
    
    public static bool operator ==(SymbolStoragePath left, SymbolStoragePathRef right)
    {
      return left.Path.AsSpan().SequenceEqual(right.Path);
    }
    public static bool operator !=(SymbolStoragePath left, SymbolStoragePathRef right)
    {
      return !left.Path.AsSpan().SequenceEqual(right.Path);
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

  internal readonly ref struct SymbolStoragePathRef : IEquatable<SymbolStoragePathRef>
  {
    public static void ValidateRefPathCorrectness(ReadOnlySpan<char> path)
    {
      // Allow zero length ref-paths to represent non-existing path sequence
      if (path.Length == 0) 
        return;
      SymbolStoragePath.ValidatePathCorrectness(path);
    }
    
    internal static SymbolStoragePathRef CreateUnsafe(ReadOnlySpan<char> path)
    {
      return new SymbolStoragePathRef(path, validate: false);
    }
    public static SymbolStoragePathRef Empty => new SymbolStoragePathRef("".AsSpan(), validate: false);
    
    public SymbolStoragePathRef(SymbolStoragePath path)
    {
      Path = path.Path.AsSpan();
    }
    public SymbolStoragePathRef(ReadOnlySpan<char> path)
    {
      ValidateRefPathCorrectness(path);
      Path = path;
    }
    private SymbolStoragePathRef(ReadOnlySpan<char> path, bool validate)
    {
      if (validate)
        ValidateRefPathCorrectness(path);
      
      Path = path;
    }
    
    public ReadOnlySpan<char> Path { get; }
    public bool IsEmpty => Path.Length == 0;
    public int Length => Path.Length;
    public char this[int index] => Path[index];

    public MemoryExtensions.SpanSplitEnumerator<char> GetPathComponents()
    {
      return IsEmpty ? new MemoryExtensions.SpanSplitEnumerator<char>() : Path.Split(SymbolStoragePath.DirectorySeparator);
    }
    

    public SymbolStoragePath? IntoOwned()
    {
      return SymbolStoragePath.FromRef(this);
    }
    
    public override string ToString()
    {
      return new string(Path);
    }
    
    public bool Equals(SymbolStoragePathRef other)
    {
      return Path.SequenceEqual(other.Path);
    }
    public override bool Equals([NotNullWhen(true)] object? obj)
    {
      return false;
    }

    public override int GetHashCode()
    {
      return string.GetHashCode(Path);
    }

    public static bool operator ==(SymbolStoragePathRef left, SymbolStoragePathRef right)
    {
      return left.Path.SequenceEqual(right.Path);
    }
    public static bool operator !=(SymbolStoragePathRef left, SymbolStoragePathRef right)
    {
      return !left.Path.SequenceEqual(right.Path);
    }
    
    public static bool operator ==(SymbolStoragePathRef left, SymbolStoragePath right)
    {
      return left.Path.SequenceEqual(right.Path.AsSpan());
    }
    public static bool operator !=(SymbolStoragePathRef left, SymbolStoragePath right)
    {
      return !left.Path.SequenceEqual(right.Path.AsSpan());
    }
    
    public static bool operator ==(SymbolStoragePathRef left, string right)
    {
      return left.Path.SequenceEqual(right.AsSpan());
    }
    public static bool operator !=(SymbolStoragePathRef left, string right)
    {
      return !left.Path.SequenceEqual(right.AsSpan());
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