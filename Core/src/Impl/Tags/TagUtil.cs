using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using JetBrains.SymbolStorage.Impl.Commands;
using JetBrains.SymbolStorage.Impl.Storages;
using Newtonsoft.Json;

namespace JetBrains.SymbolStorage.Impl.Tags
{
  internal static class TagUtil
  {
    private const string TagDirectory = "_jb.tags";
    private const string TagExtension = ".tag";
    private static readonly string TagDirectoryPathPrefix = TagDirectory + Path.DirectorySeparatorChar;

    public static string MakeTagFile(Identity identity, Guid fileId)
    {
      if (identity == null)
        throw new ArgumentNullException(nameof(identity));
      return Path.Combine(TagDirectory, identity.Product, identity.Product + '-' + identity.Version + '-' + fileId.ToString("N") + TagExtension);
    }

    public static Tag Clone(this Tag tag)
    {
      var newTag = new Tag();
      foreach (var filedInfo in typeof(Tag).GetFields())
        filedInfo.SetValue(newTag, filedInfo.GetValue(tag));
      return newTag;
    }

    public static async Task<Tag> ReadTagScriptAsync(Stream stream)
    {
      using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
      var str = await reader.ReadToEndAsync();
      var tag = JsonConvert.DeserializeObject<Tag>(str);
      if (tag == null)
        throw new ArgumentException("Expected to read tag object in json format, but null received");
      tag.Directories = tag.Directories?.Select(PathUtil.NormalizeSystem).ToArray();
      return tag;
    }

    public static async Task WriteTagScriptAsync(Tag tag, Stream stream)
    {
      if (tag == null)
        throw new ArgumentNullException(nameof(tag));
      var tmp = tag.Clone();
      tmp.Directories = tag.Directories?.Select(PathUtil.NormalizeLinux).ToArray();
      await using var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true);
      var str = JsonConvert.SerializeObject(tmp, Formatting.Indented);
      await writer.WriteAsync(str);
    }

    public static async Task<List<TagFileData>> GetAllTagScriptsAsync(
      this IStorage storage,
      int degreeOfParallelism,
      Action<string>? progress = null)
    {
      if (storage == null)
        throw new ArgumentNullException(nameof(storage));
      return await storage.GetChildrenAsync(ChildrenMode.Default, TagDirectory).ParallelForAsync(degreeOfParallelism, async item =>
        {
          var file = item.Name;
          progress?.Invoke(file);
          return new TagFileData(file, await storage.OpenForReadingAsync(file, ReadTagScriptAsync));
        });
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsStorageFormatFile(string file) =>
      file == Markers.Flat ||
      file == Markers.SingleTier ||
      file == Markers.TwoTier;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsStorageCasingFile(string file) =>
      file == Markers.LowerCase ||
      file == Markers.UpperCase;

    public static bool IsTagFile(string file) => file.StartsWith(TagDirectoryPathPrefix);
    public static bool IsDataFile(string file) => !(IsStorageFormatFile(file) || IsStorageCasingFile(file) || IsTagFile(file));

    public static bool ValidateProduct(string? product) => !string.IsNullOrEmpty(product) && product.All(IsValidProduct);
    public static bool ValidateVersion(string? version) => !string.IsNullOrEmpty(version) && version.All(IsValidVersion);

    public static bool ValidateProductWildcard(string? productWildcard) => !string.IsNullOrEmpty(productWildcard) && productWildcard.All(c => IsWildcard(c) || IsValidProduct(c));
    public static bool ValidateVersionWildcard(string? versionWildcard) => !string.IsNullOrEmpty(versionWildcard) && versionWildcard.All(c => IsWildcard(c) || IsValidVersion(c));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsWildcard(char c) =>
      c == '*' ||
      c == '?';

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsValidProduct(char c) =>
      char.IsLetterOrDigit(c) ||
      c == '_' ||
      c == '-' ||
      c == '+';

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsValidVersion(char c) =>
      char.IsLetterOrDigit(c) ||
      c == '_' ||
      c == '-' ||
      c == '+' ||
      c == '.';

    public static IReadOnlyCollection<KeyValuePair<string, string>> ParseProperties(this IEnumerable<string> list)
    {
      if (list == null)
        throw new ArgumentNullException(nameof(list));
      var res = new SortedList<string, string>(StringComparer.OrdinalIgnoreCase);
      foreach (var str in list.SelectMany(x => x.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)))
      {
        var parts = str.Split('=');
        if (parts.Length != 2)
          throw new Exception($"Invalid property format {str}");
        var key = parts[0];
        var value = parts[1];
        if (!key.All(IsValidPropertyKey))
          throw new Exception("Invalid property key");
        if (!key.All(IsValidPropertyValue))
          throw new Exception("Invalid property value");
        if (res.ContainsKey(key))
          throw new Exception($"Property {key} was defined twice");
        res.Add(key, value);
      }

      return res.ToList();
    }

    private static bool IsValidPropertyKey(char c) =>
      char.IsLetterOrDigit(c) ||
      c == '_' ||
      c == '-' ||
      c == '+' ||
      c == '.';

    private static bool IsValidPropertyValue(char c) =>
      char.IsLetterOrDigit(c) ||
      c == '_' ||
      c == '-' ||
      c == '+' ||
      c == '.' ||
      c == '{' ||
      c == '}' ||
      c == '<' ||
      c == '>' ||
      c == '(' ||
      c == ')' ||
      c == '|';
  }
}