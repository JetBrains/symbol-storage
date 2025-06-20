using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace JetBrains.SymbolStorage.Impl.Storages
{
  internal sealed class FileSystemStorage : IStorage
  {
    private readonly string myRootDir;

    public FileSystemStorage(string rootDir)
    {
      myRootDir = rootDir ?? throw new ArgumentNullException(nameof(rootDir));
      Directory.CreateDirectory(myRootDir);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string SymbolPathToDiskPath(SymbolPath path)
    {
      string relativePath = path.Path;
      if (Path.DirectorySeparatorChar != '/')
        relativePath = relativePath.Replace('/', Path.DirectorySeparatorChar);

      return Path.Combine(myRootDir, relativePath);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string SymbolPathToRelativeDiskPath(SymbolPath path)
    {
      string relativePath = path.Path;
      if (Path.DirectorySeparatorChar != '/')
        relativePath = relativePath.Replace('/', Path.DirectorySeparatorChar);

      return relativePath;
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private SymbolPath DiskPathToSymbolPath(string diskPath)
    {
      return SymbolPath.FromSystemPath(diskPath, basePath: myRootDir);
    }
    
    public async Task<bool> ExistsAsync(SymbolPath file)
    {
      await Task.Yield();
      return File.Exists(SymbolPathToDiskPath(file));
    }

    public async Task DeleteAsync(SymbolPath file)
    {
      await Task.Yield();
      File.Delete(SymbolPathToDiskPath(file));
      TryRemoveEmptyDirsToRootDir(Path.GetDirectoryName(SymbolPathToRelativeDiskPath(file)) ?? "");
    }

    public async Task RenameAsync(SymbolPath srcFile, SymbolPath dstFile, AccessMode mode)
    {
      await Task.Yield();
      var tempExt = '.' + Guid.NewGuid().ToString("N") + ".tmp";

      var dstDir = Path.GetDirectoryName(SymbolPathToRelativeDiskPath(dstFile));
      var fullDir = myRootDir;
      foreach (var part in string.IsNullOrEmpty(dstDir) ? [] : dstDir.Split(Path.DirectorySeparatorChar))
      {
        var newFullDir = Path.Combine(fullDir, part);

        // Note: Should works on casing-insensitive file system!!! 
        var realDir = Directory.GetDirectories(fullDir, part).FirstOrDefault();
        if (realDir == null)
          Directory.CreateDirectory(newFullDir);
        else if (realDir != newFullDir)
        {
          var tempDir = newFullDir + tempExt;
          Directory.Move(realDir, tempDir);
          Directory.Move(tempDir, newFullDir);
        }

        fullDir = newFullDir;
      }

      var newFileName = Path.GetFileName(SymbolPathToRelativeDiskPath(dstFile));
      var fullNewFile = Path.Combine(fullDir, newFileName);

      // Note: Should works on casing-insensitive file system!!!
      var realFullFile = Directory.GetFiles(fullDir, newFileName).FirstOrDefault();
      if (realFullFile == null)
        File.Move(SymbolPathToDiskPath(srcFile), fullNewFile);
      else if (realFullFile != fullNewFile)
      {
        var tempFile = fullNewFile + tempExt;
        File.Move(SymbolPathToDiskPath(srcFile), tempFile);
        File.Move(tempFile, fullNewFile);
      }

      TryRemoveEmptyDirsToRootDir(Path.GetDirectoryName(SymbolPathToRelativeDiskPath(srcFile)) ?? "");
    }

    public async Task<long> GetLengthAsync(SymbolPath file)
    {
      await Task.Yield();
      return new FileInfo(SymbolPathToDiskPath(file)).Length;
    }

    public bool SupportAccessMode => false;

    public Task<AccessMode> GetAccessModeAsync(SymbolPath file)
    {
      return Task.FromResult(AccessMode.Unknown);
    }

    public Task SetAccessModeAsync(SymbolPath file, AccessMode mode)
    {
      return Task.CompletedTask;
    }

    public async Task<TResult> OpenForReadingAsync<TResult>(SymbolPath file, Func<Stream, Task<TResult>> func)
    {
      if (func == null)
        throw new ArgumentNullException(nameof(func));
      await Task.Yield();
      await using var stream = File.OpenRead(SymbolPathToDiskPath(file));
      return await func(stream);
    }

    public Task OpenForReadingAsync(SymbolPath file, Func<Stream, Task> func) => OpenForReadingAsync(file, async x =>
      {
        await func(x);
        return true;
      });

    public async Task CreateForWritingAsync(SymbolPath file, AccessMode mode, Stream stream)
    {
      if (stream == null)
        throw new ArgumentNullException(nameof(stream));
      if (!stream.CanSeek)
        throw new ArgumentException("The stream should support the seek operation", nameof(stream));
      await Task.Yield();
      stream.Seek(0, SeekOrigin.Begin);
      var fullFile = SymbolPathToDiskPath(file);
      Directory.CreateDirectory(Path.GetDirectoryName(fullFile) ?? "");
      await using var outStream = File.Create(fullFile);
      await stream.CopyToAsync(outStream);
    }

    public async Task<bool> IsEmptyAsync()
    {
      await Task.Yield();
      return !Directory.EnumerateFileSystemEntries(myRootDir).Any();
    }

    public async IAsyncEnumerable<ChildrenItem> GetChildrenAsync(ChildrenMode mode, SymbolPath? prefixDir = null)
    {
      await Task.Yield();
      var stack = new Stack<string>();
      stack.Push(prefixDir != null ? SymbolPathToDiskPath(prefixDir.Value) : myRootDir);
      
      while (stack.Count > 0)
      {
        var dir = stack.Pop();
        foreach (var path in Directory.EnumerateFileSystemEntries(dir))
        {
          var file = new FileInfo(path);
          if ((file.Attributes & FileAttributes.Directory) == 0)
          {
            yield return new ChildrenItem
            {
              FileName = DiskPathToSymbolPath(path),
              Size = (mode & ChildrenMode.WithSize) != 0 ? file.Length : null
            };
          }
          else
          {
            stack.Push(path);
          }
        }
      }
    }

    public Task InvalidateExternalServicesAsync(IEnumerable<SymbolPath>? fileMasks = null)
    {
      return Task.CompletedTask;
    }

    private void TryRemoveEmptyDirsToRootDir(string dir)
    {
      while (!string.IsNullOrEmpty(dir))
      {
        var fullDir = Path.Combine(myRootDir, dir);
        if (Directory.EnumerateFileSystemEntries(fullDir).Any())
          return;
        Directory.Delete(fullDir);
        dir = Path.GetDirectoryName(dir) ?? "";
      }
    }

    public void Dispose()
    {
    }
  }
}