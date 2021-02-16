using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using JetBrains.SymbolStorage.Impl.Logger;
using JetBrains.SymbolStorage.Impl.Storages;
using JetBrains.SymbolStorage.Impl.Tags;

namespace JetBrains.SymbolStorage.Impl.Commands
{
  internal sealed class Validator
  {
    [CanBeNull]
    private readonly string myId;

    private readonly ILogger myLogger;
    private readonly IStorage myStorage;

    public Validator([NotNull] ILogger logger, [NotNull] IStorage storage, [CanBeNull] string id = null)
    {
      myLogger = logger ?? throw new ArgumentNullException(nameof(logger));
      myStorage = storage ?? throw new ArgumentNullException(nameof(storage));
      myId = id != null ? " (" + id + ")" : "";
    }

    public async Task<StorageFormat> CreateOrValidateStorageMarkers(StorageFormat newStorageFormat)
    {
      if (!await myStorage.IsEmpty())
        return await ValidateStorageMarkers();
      await CreateStorageMarkers(newStorageFormat);
      return newStorageFormat;
    }

    public async Task CreateStorageMarkers(StorageFormat newStorageFormat)
    {
      myLogger.Info($"[{DateTime.Now:s}] Creating storage markers{myId}...");
      if (!await myStorage.IsEmpty())
        throw new Exception("The empty storage is expected");
      var files = new List<string> {Markers.SingleTier};
      switch (newStorageFormat)
      {
      case StorageFormat.Normal: break;
      case StorageFormat.LowerCase:
        files.Add(Markers.LowerCase);
        break;
      case StorageFormat.UpperCase:
        files.Add(Markers.UpperCase);
        break;
      default: throw new ArgumentOutOfRangeException(nameof(newStorageFormat), newStorageFormat, null);
      }

      foreach (var file in files)
        await myStorage.CreateEmpty(file, AccessMode.Private);
      await myStorage.InvalidateExternalServices(files);
    }

    public async Task<StorageFormat> ValidateStorageMarkers()
    {
      myLogger.Info($"[{DateTime.Now:s}] Validating storage markers{myId}...");
      var isFlat = myStorage.Exists(Markers.Flat);
      var isSingleTier = myStorage.Exists(Markers.SingleTier);
      var isTwoTier = myStorage.Exists(Markers.TwoTier);
      var isLowerCase = myStorage.Exists(Markers.LowerCase);
      var isUpperCase = myStorage.Exists(Markers.UpperCase);

      if (await isFlat)
        throw new ApplicationException("The flat storage format isn't supported");
      if (await isTwoTier)
        throw new ApplicationException("The two-tier storage format isn't supported");
      if (!await isSingleTier)
        throw new ApplicationException("The single-tier storage marker is absent");

      var lowerCase = await isLowerCase;
      var upperCase = await isUpperCase;
      if (!lowerCase && !upperCase)
        return StorageFormat.Normal;
      if (lowerCase && !upperCase)
        return StorageFormat.LowerCase;
      if (!lowerCase && upperCase)
        return StorageFormat.UpperCase;
      throw new ApplicationException("The storage wasn't properly configured, both lower and upper case were presented");
    }

    public async Task<IReadOnlyCollection<KeyValuePair<string, Tag>>> LoadTagItems()
    {
      myLogger.Info($"[{DateTime.Now:s}] Loading tag files{myId}...");
      return await myStorage.GetAllTagScripts(x => myLogger.Info($"  Loading {x}")).ToListAsync();
    }

    public async Task<Tuple<IReadOnlyCollection<KeyValuePair<string, Tag>>, IReadOnlyCollection<KeyValuePair<string, Tag>>>> LoadTagItems(
      IReadOnlyCollection<string> incProductWildcards,
      IReadOnlyCollection<string> excProductWildcards,
      IReadOnlyCollection<string> incVersionWildcards,
      IReadOnlyCollection<string> excVersionWildcards)
    {
      var tagItems = await LoadTagItems();
      var incProductRegexs = incProductWildcards.Select(x => new Regex(ConvertWildcardToRegex(x))).ToArray();
      var excProductRegexs = excProductWildcards.Select(x => new Regex(ConvertWildcardToRegex(x))).ToArray();
      var incVersionRegexs = incVersionWildcards.Select(x => new Regex(ConvertWildcardToRegex(x))).ToArray();
      var excVersionRegexs = excVersionWildcards.Select(x => new Regex(ConvertWildcardToRegex(x))).ToArray();
      var inc = new List<KeyValuePair<string, Tag>>();
      var exc = new List<KeyValuePair<string, Tag>>();
      foreach (var tagItem in tagItems)
        if ((incProductRegexs.Length == 0 || incProductRegexs.Any(y => y.IsMatch(tagItem.Value.Product ?? ""))) &&
            (incVersionRegexs.Length == 0 || incVersionRegexs.Any(y => y.IsMatch(tagItem.Value.Version ?? ""))) &&
            excProductRegexs.All(y => !y.IsMatch(tagItem.Value.Product ?? "")) &&
            excVersionRegexs.All(y => !y.IsMatch(tagItem.Value.Version ?? "")))
          inc.Add(tagItem);
        else
          exc.Add(tagItem);
      return Tuple.Create((IReadOnlyCollection<KeyValuePair<string, Tag>>) inc, (IReadOnlyCollection<KeyValuePair<string, Tag>>) exc);
    }

    [NotNull]
    private static string ConvertWildcardToRegex([NotNull] string str)
    {
      return "^" + Regex.Escape(str).Replace("\\?", ".").Replace("\\*", ".*") + "$";
    }

    public void DumpProducts([NotNull] IEnumerable<KeyValuePair<string, Tag>> tagItems)
    {
      myLogger.Info($"[{DateTime.Now:s}] Tags{myId}...");
      foreach (var product in tagItems.OrderBy(x => x.Value.Product, StringComparer.Ordinal).ThenBy(x => x.Value.Version, StringComparer.Ordinal).GroupBy(x => x.Value.Product))
      {
        myLogger.Info($"  {product.Key}");
        foreach (var version in product.GroupBy(x => x.Value.Version))
          myLogger.Info($"    {version.Key}");
      }
    }

    public void DumpProperties([NotNull] IEnumerable<KeyValuePair<string, Tag>> tagItems)
    {
      myLogger.Info($"[{DateTime.Now:s}] Tag properties{myId}...");
      foreach (var property in tagItems.SelectMany(x => x.Value.Properties).OrderBy(x => x.Key, StringComparer.Ordinal).ThenBy(x => x.Value, StringComparer.Ordinal).GroupBy(x => x.Key))
      {
        myLogger.Info($"  {property.Key}");
        foreach (var value in property.Select(x => x.Value).Distinct())
          myLogger.Info($"    {value}");
      }
    }

    public enum ValidateMode
    {
      Validate,
      Fix,
      Delete
    }

    public async Task<Tuple<Statistics, long>> Validate(
      [NotNull] IEnumerable<KeyValuePair<string, Tag>> items,
      [NotNull] IReadOnlyCollection<string> files,
      StorageFormat storageFormat,
      ValidateMode mode,
      bool verifyAcl = false)
    {
      myLogger.Info($"[{DateTime.Now:s}] Validating storage{myId}...");
      var fix = mode == ValidateMode.Fix || mode == ValidateMode.Delete;
      var statistics = new Statistics();
      ILogger logger = new LoggerWithStatistics(myLogger, statistics);
      files = await ValidateDataFiles(logger, files, storageFormat, fix);
      var tree = await CreateDirectoryTree(files);
      foreach (var item in items)
      {
        var tagFile = item.Key;
        var tag = item.Value;
        logger.Info($"  Validating {tagFile}");

        if (!tag.Product.ValidateProduct())
          logger.Error($"Invalid product {tag.Product} in file {tagFile}");

        if (!tag.Version.ValidateVersion())
          logger.Error($"Invalid version {tag.Version} in file {tagFile}");

        if (tag.Directories == null || tag.Directories.Length == 0)
        {
          logger.Error($"The empty directory list in {tagFile}");
          if (fix)
          {
            logger.Fix($"The tag will be deleted {tagFile}");
            await myStorage.Delete(tagFile);
          }
        }
        else
        {
          var isDirty = false;
          for (var index = 0; index < tag.Directories.Length; index++)
          {
            var dir = tag.Directories[index];

            switch (dir.ValidateAndFixDataPath(StorageFormat.Normal, out var fixedDir))
            {
            case PathUtil.ValidateAndFixErrors.Ok:
              break;
            case PathUtil.ValidateAndFixErrors.Error:
              logger.Error($"The tag directory {dir} from {tagFile} has invalid format");
              break;
            case PathUtil.ValidateAndFixErrors.CanBeFixed:
              logger.Error($"The tag directory {dir} from {tagFile} has invalid format");
              if (fix)
              {
                isDirty = true;
                tag.Directories[index] = fixedDir;
              }

              break;
            default:
              throw new ArgumentOutOfRangeException();
            }

            var dstDir = dir.ValidateAndFixDataPath(storageFormat, out fixedDir) == PathUtil.ValidateAndFixErrors.CanBeFixed ? fixedDir : dir;
            var node = tree;
            foreach (var part in dstDir.GetPathComponents())
              node = node?.Lookup(part);

            if (node == null)
              logger.Error($"The directory {dir} from {tagFile} id wasn't found");
            else
              node.IncrementReferences();
          }

          if (isDirty)
          {
            logger.Fix($"The tag file {tagFile} will be overwritten");
            await using var stream = new MemoryStream();
            TagUtil.WriteTagScript(tag, stream);
            await myStorage.CreateForWriting(tagFile, AccessMode.Private, stream.Length, stream.Rewind());
            await myStorage.InvalidateExternalServices(new[] {tagFile});
          }
        }
      }

      var deleted = await ValidateUnreachable(logger, tree, mode);
      if (verifyAcl)
        await ValidateAcl(logger, files, fix);
      return Tuple.Create(statistics, deleted);
    }

    private async Task ValidateAcl([NotNull] ILogger logger, [NotNull] IEnumerable<string> files, bool fix)
    {
      if (!myStorage.SupportAccessMode)
      {
        logger.Warning("Validating access rights is not supported for storage");
        return;
      }

      logger.Info($"[{DateTime.Now:s}] Validating access rights{myId}...");
      foreach (var file in files)
        if (TagUtil.IsTagFile(file) || TagUtil.IsStorageCasingFile(file))
        {
          if (await myStorage.GetAccessMode(file) != AccessMode.Private)
          {
            logger.Error($"The internal file {file} has invalid access rights");
            if (fix)
            {
              logger.Fix($"Update access rights for the internal file {file}");
              await myStorage.SetAccessMode(file, AccessMode.Private);
            }
          }
        }
        else
        {
          if (await myStorage.GetAccessMode(file) != AccessMode.Public)
          {
            logger.Error($"The storage file {file} has invalid access rights");
            if (fix)
            {
              logger.Fix($"Update access rights for the storage file {file}");
              await myStorage.SetAccessMode(file, AccessMode.Public);
            }
          }
        }
    }

    [NotNull]
    private async Task<IReadOnlyCollection<string>> ValidateDataFiles([NotNull] ILogger logger, [NotNull] IEnumerable<string> files, StorageFormat storageFormat, bool fix)
    {
      logger.Info($"[{DateTime.Now:s}] Validating data files{myId}...");

      var res = new List<string>();
      foreach (var file in files)
      {
        logger.Info($"  Validating {file}");
        switch (file.ValidateAndFixDataPath(storageFormat, out var fixedFile))
        {
        case PathUtil.ValidateAndFixErrors.Ok:
          res.Add(file);
          break;
        case PathUtil.ValidateAndFixErrors.Error:
          logger.Error($"Found unexpected file {file} location");
          goto case PathUtil.ValidateAndFixErrors.Ok;
        case PathUtil.ValidateAndFixErrors.CanBeFixed:
          logger.Error($"Found unexpected file {file} location");
          if (fix)
          {
            logger.Fix($"Rename file {file} to file {fixedFile}");
            await myStorage.Rename(file, fixedFile, AccessMode.Public);
            res.Add(fixedFile);
          }
          else
            res.Add(file);

          break;
        default:
          throw new ArgumentOutOfRangeException();
        }
      }

      return res;
    }

    [NotNull]
    private Task<PathTreeNode> CreateDirectoryTree([NotNull] IEnumerable<string> files)
    {
      var tree = new PathTreeNode();
      foreach (var file in files)
      {
        var node = tree;
        foreach (var part in Path.GetDirectoryName(file).GetPathComponents())
          node = node.Lookup(part) ?? node.Insert(part);
        node.AddFile(file);
      }

      return Task.FromResult(tree);
    }

    private async Task<long> ValidateUnreachable([NotNull] ILogger logger, [NotNull] PathTreeNode tree, ValidateMode mode)
    {
      logger.Info(mode == ValidateMode.Delete
        ? $"[{DateTime.Now:s}] Delete unreachable files{myId}..."
        : $"[{DateTime.Now:s}] Validating unreachable files{myId}...");
      long deleted = 0;
      var stack = new Stack<PathTreeNode>();
      stack.Push(tree);
      while (stack.Count > 0)
      {
        var node = stack.Pop();
        if (node.HasChildren)
          foreach (var child in node.Children)
            stack.Push(child);
        if (!node.HasReferences)
          foreach (var file in node.Files)
          {
            if (mode == ValidateMode.Delete)
            {
              logger.Info($"  Deleting {file}");
              Interlocked.Increment(ref deleted);
              await myStorage.Delete(file);
            }
            else
            {
              logger.Error($"The file {file} should be deleted as unreferenced");
              if (mode == ValidateMode.Fix)
              {
                logger.Fix($"The file {file} will be deleted as unreferenced");
                await myStorage.Delete(file);
              }
            }
          }
      }

      return deleted;
    }

    public async Task<Tuple<long, IReadOnlyCollection<string>>> GatherDataFiles()
    {
      myLogger.Info($"[{DateTime.Now:s}] Gathering data files{myId}...");
      long totalSize = 0;
      var files = await myStorage.GetChildren(ChildrenMode.WithSize).Select(x =>
        {
          Interlocked.Add(ref totalSize, x.Size);
          return x.Name;
        }).Where(TagUtil.IsDataFile).ToListAsync();
      return new Tuple<long, IReadOnlyCollection<string>>(totalSize, files);
    }
  }
}