#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

    public async Task<bool> ExistsAsync(string file)
    {
      file.CheckSystemFile();
      await Task.Yield();
      return File.Exists(Path.Combine(myRootDir, file));
    }

    public async Task DeleteAsync(string file)
    {
      file.CheckSystemFile();
      await Task.Yield();
      File.Delete(Path.Combine(myRootDir, file));
      TryRemoveEmptyDirsToRootDir(Path.GetDirectoryName(file) ?? "");
    }

    public async Task RenameAsync(string srcFile, string dstFile, AccessMode mode)
    {
      srcFile.CheckSystemFile();
      dstFile.CheckSystemFile();
      await Task.Yield();
      var tempExt = '.' + Guid.NewGuid().ToString("N") + ".tmp";

      var dstDir = Path.GetDirectoryName(dstFile);
      var fullDir = myRootDir;
      foreach (var part in string.IsNullOrEmpty(dstDir) ? Array.Empty<string>() : dstDir.Split(Path.DirectorySeparatorChar))
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

      var fullNewFile = Path.Combine(fullDir, Path.GetFileName(dstFile));

      // Note: Should works on casing-insensitive file system!!!
      var realFullFile = Directory.GetFiles(fullDir, Path.GetFileName(dstFile)).FirstOrDefault();
      if (realFullFile == null)
        File.Move(Path.Combine(myRootDir, srcFile), fullNewFile);
      else if (realFullFile != fullNewFile)
      {
        var tempFile = fullNewFile + tempExt;
        File.Move(Path.Combine(myRootDir, srcFile), tempFile);
        File.Move(tempFile, Path.Combine(myRootDir, dstFile));
      }

      TryRemoveEmptyDirsToRootDir(Path.GetDirectoryName(srcFile) ?? "");
    }

    public async Task<long> GetLengthAsync(string file)
    {
      file.CheckSystemFile();
      await Task.Yield();
      return new FileInfo(Path.Combine(myRootDir, file)).Length;
    }

    public bool SupportAccessMode => false;

    public Task<AccessMode> GetAccessModeAsync(string file)
    {
      file.CheckSystemFile();
      return Task.FromResult(AccessMode.Unknown);
    }

    public Task SetAccessModeAsync(string file, AccessMode mode)
    {
      file.CheckSystemFile();
      return Task.CompletedTask;
    }

    public async Task<TResult> OpenForReadingAsync<TResult>(string file, Func<Stream, Task<TResult>> func)
    {
      if (func == null)
        throw new ArgumentNullException(nameof(func));
      file.CheckSystemFile();
      await Task.Yield();
      await using var stream = File.OpenRead(Path.Combine(myRootDir, file));
      return await func(stream);
    }

    public Task OpenForReadingAsync(string file, Func<Stream, Task> func) => OpenForReadingAsync(file, async x =>
      {
        await func(x);
        return true;
      });

    public async Task CreateForWritingAsync(string file, AccessMode mode, Stream stream)
    {
      if (stream == null)
        throw new ArgumentNullException(nameof(stream));
      if (!stream.CanSeek)
        throw new ArgumentException("The stream should support the seek operation", nameof(stream));
      file.CheckSystemFile();
      await Task.Yield();
      stream.Seek(0, SeekOrigin.Begin);
      var length = stream.Length;
      var fullFile = Path.Combine(myRootDir, file);
      Directory.CreateDirectory(Path.GetDirectoryName(fullFile) ?? "");
      await using var outStream = File.Create(fullFile);
      var buffer = new byte[Math.Min(length, 85000)];
      int read;
      while (length > 0 && (read = await stream.ReadAsync(buffer, 0, checked((int) Math.Min(length, buffer.Length)))) > 0)
      {
        outStream.Write(buffer, 0, read);
        length -= read;
      }
    }

    public async Task<bool> IsEmptyAsync()
    {
      await Task.Yield();
      return !Directory.EnumerateFileSystemEntries(myRootDir).Any();
    }

    public async IAsyncEnumerable<ChildrenItem> GetChildrenAsync(ChildrenMode mode, string? prefixDir = null)
    {
      await Task.Yield();
      var stack = new Stack<string>();
      stack.Push(string.IsNullOrEmpty(prefixDir) ? myRootDir : Path.Combine(myRootDir, prefixDir));
      while (stack.Count > 0)
      {
        var dir = stack.Pop();
        foreach (var path in Directory.EnumerateFileSystemEntries(dir))
        {
          var file = new FileInfo(path);
          if ((file.Attributes & FileAttributes.Directory) == 0)
            yield return new ChildrenItem
              {
                Name = Path.GetRelativePath(myRootDir, path),
                Size = (mode & ChildrenMode.WithSize) != 0 ? file.Length : null
              };
          else
            stack.Push(path);
        }
      }
    }

    public Task InvalidateExternalServicesAsync(IEnumerable<string>? fileMasks = null)
    {
      if (fileMasks != null)
        foreach (var key in fileMasks)
          key.CheckSystemFile();
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