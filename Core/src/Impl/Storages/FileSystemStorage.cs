using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace JetBrains.SymbolStorage.Impl.Storages
{
  internal sealed class FileSystemStorage : IStorage
  {
    private readonly string myRootDir;

    public FileSystemStorage([NotNull] string rootDir)
    {
      myRootDir = rootDir ?? throw new ArgumentNullException(nameof(rootDir));
      Directory.CreateDirectory(myRootDir);
    }

    public Task<bool> ExistsAsync(string file)
    {
      file.CheckSystemFile();
      return Task.Run(() => File.Exists(Path.Combine(myRootDir, file)));
    }

    public Task DeleteAsync(string file)
    {
      file.CheckSystemFile();
      return Task.Run(() =>
        {
          File.Delete(Path.Combine(myRootDir, file));
          TryRemoveEmptyDirsToRootDir(Path.GetDirectoryName(file) ?? "");
        });
    }

    public Task RenameAsync(string srcFile, string dstFile, AccessMode mode)
    {
      srcFile.CheckSystemFile();
      dstFile.CheckSystemFile();
      return Task.Run(() =>
        {
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
        });
    }

    public Task<long> GetLengthAsync(string file)
    {
      file.CheckSystemFile();
      return Task.Run(() => new FileInfo(Path.Combine(myRootDir, file)).Length);
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
      await using var stream = File.OpenRead(Path.Combine(myRootDir, file));
      return await func(stream);
    }

    public Task OpenForReadingAsync(string file, Func<Stream, Task> func) => OpenForReadingAsync(file, async x =>
      {
        await func(x);
        return true;
      });

    public Task CreateForWritingAsync(string file, AccessMode mode, Stream stream)
    {
      if (stream == null)
        throw new ArgumentNullException(nameof(stream));
      if (!stream.CanSeek)
        throw new ArgumentException("The stream should support the seek operation", nameof(stream));
      file.CheckSystemFile();
      return Task.Run(() =>
        {
          stream.Seek(0, SeekOrigin.Begin);
          var length = stream.Length;
          var fullFile = Path.Combine(myRootDir, file);
          Directory.CreateDirectory(Path.GetDirectoryName(fullFile) ?? "");
          using var outStream = File.Create(fullFile);
          var buffer = new byte[Math.Min(length, 85000)];
          int read;
          while (length > 0 && (read = stream.Read(buffer, 0, checked((int) Math.Min(length, buffer.Length)))) > 0)
          {
            outStream.Write(buffer, 0, read);
            length -= read;
          }
        });
    }

    public Task<bool> IsEmptyAsync()
    {
      return Task.Run(() => !Directory.EnumerateFileSystemEntries(myRootDir).Any());
    }

    public async IAsyncEnumerable<ChildrenItem> GetChildrenAsync(ChildrenMode mode, [NotNull] string prefixDir)
    {
      var stack = new Stack<string>();
      stack.Push(string.IsNullOrEmpty(prefixDir) ? myRootDir : Path.Combine(myRootDir, prefixDir));
      while (stack.Count > 0)
      {
        var dir = stack.Pop();
        foreach (var subDir in Directory.EnumerateDirectories(dir))
          stack.Push(subDir);
        foreach (var file in Directory.EnumerateFiles(dir))
        {
          yield return new ChildrenItem
            {
              Name = Path.GetRelativePath(myRootDir, file),
              Size = (mode & ChildrenMode.WithSize) != 0 ? new FileInfo(file).Length : -1
            };
        }
      }
    }

    public Task InvalidateExternalServicesAsync(IEnumerable<string> fileMasks)
    {
      if (fileMasks != null)
        foreach (var key in fileMasks)
          key.CheckSystemFile();
      return Task.CompletedTask;
    }

    private void TryRemoveEmptyDirsToRootDir([NotNull] string dir)
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
  }
}