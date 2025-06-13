using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.SymbolStorage.Impl.Logger;
using JetBrains.SymbolStorage.Impl.Storages;
using JetBrains.SymbolStorage.Impl.Tags;

namespace JetBrains.SymbolStorage.Impl.Commands
{
  internal sealed class Validator
  {
    private readonly string? myId;

    private readonly ILogger myLogger;
    private readonly IStorage myStorage;

    public Validator(ILogger logger, IStorage storage, string? id = null)
    {
      myLogger = logger ?? throw new ArgumentNullException(nameof(logger));
      myStorage = storage ?? throw new ArgumentNullException(nameof(storage));
      myId = id != null ? " (" + id + ")" : "";
    }

    public async Task<StorageFormat> CreateOrValidateStorageMarkersAsync(StorageFormat newStorageFormat)
    {
      if (!await myStorage.IsEmptyAsync())
        return await ValidateStorageMarkersAsync();
      await CreateStorageMarkersAsync(newStorageFormat);
      return newStorageFormat;
    }

    public async Task CreateStorageMarkersAsync(StorageFormat newStorageFormat)
    {
      myLogger.Info($"[{DateTime.Now:s}] Creating storage markers{myId}...");
      if (!await myStorage.IsEmptyAsync())
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
        await myStorage.CreateEmptyAsync(file, AccessMode.Private);
    }

    public async Task<StorageFormat> ValidateStorageMarkersAsync()
    {
      myLogger.Info($"[{DateTime.Now:s}] Validating storage markers{myId}...");
      var isFlat = myStorage.ExistsAsync(Markers.Flat);
      var isSingleTier = myStorage.ExistsAsync(Markers.SingleTier);
      var isTwoTier = myStorage.ExistsAsync(Markers.TwoTier);
      var isLowerCase = myStorage.ExistsAsync(Markers.LowerCase);
      var isUpperCase = myStorage.ExistsAsync(Markers.UpperCase);

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

    public Task<IReadOnlyCollection<KeyValuePair<string, Tag>>> LoadTagItemsAsync(int degreeOfParallelism)
    {
      myLogger.Info($"[{DateTime.Now:s}] Loading tag files{myId}...");
      return myStorage.GetAllTagScriptsAsync(degreeOfParallelism, x => myLogger.Verbose($"  Loading {x}"));
    }
    
    public async Task<Tuple<IReadOnlyCollection<KeyValuePair<string, Tag>>, IReadOnlyCollection<KeyValuePair<string, Tag>>>> LoadTagItemsAsync(
      int degreeOfParallelism,
      IdentityFilter identityFilter,
      TimeSpan? minItemAgeFilter,
      bool? protectedFilter)
    {
      if (identityFilter == null)
        throw new ArgumentNullException(nameof(identityFilter));
      var tagItems = await LoadTagItemsAsync(degreeOfParallelism);
      var inc = new List<KeyValuePair<string, Tag>>();
      var exc = new List<KeyValuePair<string, Tag>>();
      foreach (var tagItem in tagItems)
      {
        var tag = tagItem.Value;
        if (identityFilter.IsMatch(tag.Product ?? "", tag.Version ?? "") &&
            (minItemAgeFilter == null || tag.CreationUtcTime + minItemAgeFilter.Value < DateTime.UtcNow) &&
            (protectedFilter == null || protectedFilter == tag.IsProtected))
          inc.Add(tagItem);
        else
          exc.Add(tagItem);
      }

      return new(inc, exc);
    }

    public void DumpProducts(IEnumerable<KeyValuePair<string, Tag>> tagItems)
    {
      myLogger.Info($"[{DateTime.Now:s}] Tags{myId}...");
      foreach (var product in tagItems.OrderBy(x => x.Value.Product, StringComparer.Ordinal).ThenBy(x => x.Value.Version, StringComparer.Ordinal).GroupBy(x => x.Value.Product))
      {
        myLogger.Info($"  {product.Key}");
        foreach (var version in product.GroupBy(x => x.Value.Version))
          myLogger.Info($"    {version.Key}");
      }
    }

    public void DumpProperties(IEnumerable<KeyValuePair<string, Tag>> tagItems)
    {
      myLogger.Info($"[{DateTime.Now:s}] Tag properties{myId}...");
      foreach (var property in tagItems.SelectMany(x => x.Value.Properties ?? []).OrderBy(x => x.Key, StringComparer.Ordinal).ThenBy(x => x.Value, StringComparer.Ordinal).GroupBy(x => x.Key))
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

    public async Task<Tuple<Statistics, long>> ValidateAsync(
      int degreeOfParallelism,
      IEnumerable<KeyValuePair<string, Tag>> items,
      IReadOnlyCollection<string> files,
      StorageFormat storageFormat,
      ValidateMode mode,
      bool verifyAcl = false)
    {
      myLogger.Info($"[{DateTime.Now:s}] Validating storage{myId}...");
      var fix = mode == ValidateMode.Fix || mode == ValidateMode.Delete;
      var statistics = new Statistics();
      ILogger logger = new LoggerWithStatistics(myLogger, statistics);
      files = await ValidateDataFilesAsync(logger, degreeOfParallelism, files, storageFormat, fix);
      var tree = CreateDirectoryTree(degreeOfParallelism, files);
      await items.ParallelForAsync(degreeOfParallelism, async item =>
        {
          var tagFile = item.Key;
          var tag = item.Value;
          logger.Verbose($"  Validating {tagFile}");
          var isDirty = false;

          if (!TagUtil.ValidateProduct(tag.Product))
            logger.Error($"Invalid product {tag.Product} in file {tagFile}");

          if (!TagUtil.ValidateVersion(tag.Version))
            logger.Error($"Invalid version {tag.Version} in file {tagFile}");

          if (tag.CreationUtcTime == DateTime.MinValue)
          {
            logger.Error($"The empty creation time in {tagFile}");
            if (fix)
            {
              var newCreationUtcTime = TryFixCreationTime(tag);
              if (newCreationUtcTime != null)
              {
                logger.Fix($"The creation time will be assigned to tag {tagFile}");
                isDirty = true;
                tag.CreationUtcTime = newCreationUtcTime.Value;
              }
            }
          }

          if (tag.Directories == null || tag.Directories.Length == 0)
          {
            logger.Error($"The empty directory list in {tagFile}");
            if (fix)
            {
              logger.Fix($"The tag will be deleted {tagFile}");
              await myStorage.DeleteAsync(tagFile);
              return;
            }
          }
          else
          {
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
              var node = tree.LookupPathRecursive(dstDir);
              
              if (node == null)
                logger.Error($"The directory {dir} from {tagFile} id wasn't found");
              else
                node.IncrementReferences();
            }
          }

          if (isDirty)
          {
            logger.Info($"The tag file {tagFile} will be overwritten");
            await using var stream = new MemoryStream();
            await TagUtil.WriteTagScriptAsync(tag, stream);
            await myStorage.CreateForWritingAsync(tagFile, AccessMode.Private, stream);
          }
        });

      var deleted = await ValidateUnreachableAsync(logger, tree, mode);
      if (verifyAcl)
        await ValidateAclAsync(logger, degreeOfParallelism, files, fix);
      return Tuple.Create(statistics, deleted);
    }

    private static DateTime? TryFixCreationTime(Tag tag)
    {
      if (tag.Product == "dotNetDiv")
        if (tag.Version == "beforeWaves")
          return new DateTime(2005, 5, 30);
        else
        {
          var parts = tag.Properties?.FirstOrDefault(x => x.Key == "semanticVersion")?.Value?.Split('.', '-');
          if (parts?.Length >= 4)
            return DateTime.ParseExact(parts[2] + parts[3].PadLeft(6, '0'), "yyyyMMddHHmmss", null);
        }
      else if (tag.Product == "libleveldb" || tag.Product == "leveldb")
      {
        var parts = tag.Version?.Split('.');
        if (parts != null && parts.Length >= 2)
          return DateTime.ParseExact(parts[0], "yyyyMMdd", null);
      }
      else if (tag.Product == "coreclr")
      {
        var parts = tag.Version?.Split('.');
        if (parts != null && parts.Length >= 3)
          return DateTime.ParseExact(parts[2], "yyyyMMdd", null);
      }

      return null;
    }

    private async Task ValidateAclAsync(ILogger logger, int degreeOfParallelism, IEnumerable<string> files, bool fix)
    {
      if (!myStorage.SupportAccessMode)
      {
        logger.Warning("Validating access rights is not supported for storage");
        return;
      }

      logger.Info($"[{DateTime.Now:s}] Validating access rights{myId}...");
      await files.ParallelForAsync(degreeOfParallelism, async file =>
        {
          logger.Verbose($"  Validating {file}");
          if (TagUtil.IsTagFile(file) || TagUtil.IsStorageCasingFile(file))
          {
            if (await myStorage.GetAccessModeAsync(file) != AccessMode.Private)
            {
              logger.Error($"The internal file {file} has invalid access rights");
              if (fix)
              {
                logger.Fix($"Update access rights for the internal file {file}");
                await myStorage.SetAccessModeAsync(file, AccessMode.Private);
              }
            }
          }
          else
          {
            if (await myStorage.GetAccessModeAsync(file) != AccessMode.Public)
            {
              logger.Error($"The storage file {file} has invalid access rights");
              if (fix)
              {
                logger.Fix($"Update access rights for the storage file {file}");
                await myStorage.SetAccessModeAsync(file, AccessMode.Public);
              }
            }
          }
        });
    }

    private async Task<IReadOnlyCollection<string>> ValidateDataFilesAsync(
      ILogger logger,
      int degreeOfParallelism,
      IEnumerable<string> files,
      StorageFormat storageFormat,
      bool fix)
    {
      logger.Info($"[{DateTime.Now:s}] Validating data files{myId}...");

      var res = new ConcurrentBag<string>();
      await files.ParallelForAsync(degreeOfParallelism, async file =>
        {
          logger.Verbose($"  Validating {file}");
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
              await myStorage.RenameAsync(file, fixedFile, AccessMode.Public);
              res.Add(fixedFile);
            }
            else
              res.Add(file);

            break;
          default:
            throw new ArgumentOutOfRangeException();
          }
        });

      return res;
    }

    private static PathTree CreateDirectoryTree(int degreeOfParallelism, IEnumerable<string> files)
    {
      var tree = PathTree.BuildNew();
      files.ParallelFor(degreeOfParallelism, file =>
      {
        var node = tree.Root;
        var directory = Path.GetDirectoryName(file.AsSpan());
        if (directory.Length > 0)
          node = node.AddPathRecursive(directory);
        node.AddFile(file);
      });

      return tree.Build();
    }

    private async Task<long> ValidateUnreachableAsync(ILogger logger, PathTree tree, ValidateMode mode)
    {
      logger.Info(mode == ValidateMode.Delete
        ? $"[{DateTime.Now:s}] Delete unreachable files{myId}..."
        : $"[{DateTime.Now:s}] Validating unreachable files{myId}...");
      long deleted = 0;
      var stack = new Stack<PathTreeNode>();
      stack.Push(tree.Root);
      while (stack.Count > 0)
      {
        var node = stack.Pop();
        if (node.HasChildren)
        {
          foreach (var child in node.GetChildren())
            stack.Push(child);
        }

        if (!node.HasReferences)
        {
          foreach (var file in node.GetFiles())
          {
            if (mode == ValidateMode.Delete)
            {
              logger.Info($"  Deleting {file}");
              Interlocked.Increment(ref deleted);
              await myStorage.DeleteAsync(file);
            }
            else
            {
              logger.Error($"The file {file} should be deleted as unreferenced");
              if (mode == ValidateMode.Fix)
              {
                logger.Fix($"The file {file} will be deleted as unreferenced");
                await myStorage.DeleteAsync(file);
              }
            }
          }
        }
      }

      return deleted;
    }

    public async Task<Tuple<long, IReadOnlyCollection<string>>> GatherDataFilesAsync()
    {
      myLogger.Info($"[{DateTime.Now:s}] Gathering data files{myId}...");
      long totalSize = 0;
      var files = await myStorage.GetChildrenAsync(ChildrenMode.WithSize).Select(x =>
        {
          if (x.Size.HasValue)
            Interlocked.Add(ref totalSize, x.Size.Value);
          return x.Name;
        }).Where(TagUtil.IsDataFile).ToListAsync();
      return new Tuple<long, IReadOnlyCollection<string>>(totalSize, files);
    }
  }
}