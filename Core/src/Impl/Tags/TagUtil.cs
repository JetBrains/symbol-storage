using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using JetBrains.SymbolStorage.Impl.Commands;
using JetBrains.SymbolStorage.Impl.Storages;
using Newtonsoft.Json;

namespace JetBrains.SymbolStorage.Impl.Tags
{
  internal static class TagUtil
  {
    public const string TagDirectory = "_jb.tags";
    public const string TagExtension = ".tag";

    [NotNull]
    public static Tag Clone([NotNull] this Tag tag)
    {
      var newTag = new Tag();
      foreach (var filedInfo in typeof(Tag).GetFields())
        filedInfo.SetValue(newTag, filedInfo.GetValue(tag));
      return newTag;
    }

    [NotNull]
    public static async Task<Tag> ReadTagScriptAsync([NotNull] Stream stream)
    {
      using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
      var str = await reader.ReadToEndAsync();
      var tag = JsonConvert.DeserializeObject<Tag>(str);
      tag.Directories = tag.Directories?.Select(PathUtil.NormalizeSystem).ToArray();
      return tag;
    }

    public static async Task WriteTagScriptAsync([NotNull] Tag tag, [NotNull] Stream stream)
    {
      if (tag == null)
        throw new ArgumentNullException(nameof(tag));
      var tmp = tag.Clone();
      tmp.Directories = tag.Directories?.Select(PathUtil.NormalizeLinux).ToArray();
      await using var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true);
      var str = JsonConvert.SerializeObject(tmp, Formatting.Indented);
      await writer.WriteAsync(str);
    }

    public static async Task<IReadOnlyCollection<KeyValuePair<string, Tag>>> GetAllTagScriptsAsync(
      [NotNull] this IStorage storage,
      int degreeOfParallelism,
      [CanBeNull] Action<string> progress = null)
    {
      if (storage == null)
        throw new ArgumentNullException(nameof(storage));
      return await storage.GetChildrenAsync(ChildrenMode.Default, TagDirectory).ParallelFor(degreeOfParallelism, async item =>
        {
          var file = item.Name;
          progress?.Invoke(file);
          return new KeyValuePair<string, Tag>(file, await storage.OpenForReadingAsync(file, ReadTagScriptAsync));
        });
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsStorageFormatFile([NotNull] string file) =>
      file == Markers.Flat ||
      file == Markers.SingleTier ||
      file == Markers.TwoTier;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsStorageCasingFile([NotNull] string file) =>
      file == Markers.LowerCase ||
      file == Markers.UpperCase;

    public static bool IsTagFile([NotNull] string file) => file.StartsWith(TagDirectory + Path.DirectorySeparatorChar);
    public static bool IsDataFile([NotNull] string file) => !(IsStorageFormatFile(file) || IsStorageCasingFile(file) || IsTagFile(file));

    public static bool ValidateProduct([CanBeNull] this string product) => !string.IsNullOrWhiteSpace(product) && product.All(IsValidProduct);
    public static bool ValidateVersion([CanBeNull] this string version) => !string.IsNullOrWhiteSpace(version) && version.All(IsValidVersion);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsValidProduct(char c) =>
      char.IsLetterOrDigit(c)
      || c == '_'
      || c == '-'
      || c == '+';

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsValidVersion(char c) =>
      char.IsLetterOrDigit(c)
      || c == '_'
      || c == '-'
      || c == '+'
      || c == '.';

    [NotNull]
    public static IReadOnlyCollection<KeyValuePair<string, string>> ParseProperties([NotNull] this IEnumerable<string> list)
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
      char.IsLetterOrDigit(c)
      || c == '_'
      || c == '-'
      || c == '+'
      || c == '.';

    private static bool IsValidPropertyValue(char c) =>
      char.IsLetterOrDigit(c)
      || c == '_'
      || c == '-'
      || c == '+'
      || c == '.'
      || c == '{'
      || c == '}'
      || c == '<'
      || c == '>'
      || c == '('
      || c == ')'
      || c == '|';
  }
}