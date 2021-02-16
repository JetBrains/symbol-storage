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

    public Task<bool> Exists(string file)
    {
      if (string.IsNullOrEmpty(file))
        throw new ArgumentNullException(nameof(file));
      return Task.Run(() => File.Exists(Path.Combine(myRootDir, file)));
    }

    public Task Delete(string file)
    {
      if (string.IsNullOrEmpty(file))
        throw new ArgumentNullException(nameof(file));
      return Task.Run(() =>
        {
          File.Delete(Path.Combine(myRootDir, file));
          TryRemoveEmptyDirsToRootDir(Path.GetDirectoryName(file) ?? "");
        });
    }

    public Task Rename(string file, string newFile, AccessMode mode)
    {
      if (string.IsNullOrEmpty(file))
        throw new ArgumentNullException(nameof(file));
      if (string.IsNullOrEmpty(newFile))
        throw new ArgumentNullException(nameof(newFile));
      return Task.Run(() =>
        {
          var tempExt = '.' + Guid.NewGuid().ToString("N") + ".tmp";
          
          var dstDir = Path.GetDirectoryName(newFile);
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

          var fullNewFile = Path.Combine(fullDir, Path.GetFileName(newFile));

          // Note: Should works on casing-insensitive file system!!!
          var realFullFile = Directory.GetFiles(fullDir, Path.GetFileName(newFile)).FirstOrDefault();
          if (realFullFile == null)
            File.Move(Path.Combine(myRootDir, file), fullNewFile);
          else if (realFullFile != fullNewFile)
          {
            var tempFile = fullNewFile + tempExt;
            File.Move(Path.Combine(myRootDir, file), tempFile);
            File.Move(tempFile, Path.Combine(myRootDir, newFile));
          }

          TryRemoveEmptyDirsToRootDir(Path.GetDirectoryName(file) ?? "");
        });
    }

    public Task<long> GetLength(string file)
    {
      if (string.IsNullOrEmpty(file))
        throw new ArgumentNullException(nameof(file));
      return Task.Run(() => new FileInfo(Path.Combine(myRootDir, file)).Length);
    }

    public bool SupportAccessMode => false;

    public Task<AccessMode> GetAccessMode(string file)
    {
      if (file == null) throw new ArgumentNullException(nameof(file));
      return Task.FromResult(AccessMode.Unknown);
    }

    public Task SetAccessMode(string file, AccessMode mode)
    {
      if (file == null) throw new ArgumentNullException(nameof(file));
      return Task.CompletedTask;
    }

    public Task<TResult> OpenForReading<TResult>(string file, Func<Stream, TResult> func)
    {
      if (string.IsNullOrEmpty(file))
        throw new ArgumentNullException(nameof(file));
      if (func == null)
        throw new ArgumentNullException(nameof(func));
      return Task.Run(() =>
        {
          using var stream = File.OpenRead(Path.Combine(myRootDir, file));
          return func(stream);
        });
    }

    public Task OpenForReading(string file, Action<Stream> action)
    {
      return OpenForReading<object>(file, x =>
        {
          action(x);
          return null;
        });
    }

    public Task CreateForWriting(string file, AccessMode mode, long length, Stream stream)
    {
      if (string.IsNullOrEmpty(file))
        throw new ArgumentNullException(nameof(file));
      if (stream == null)
        throw new ArgumentNullException(nameof(stream));
      if (stream.CanSeek)
      {
        if (stream.Length - stream.Position != length)
          throw new ArgumentException(nameof(length));
      }

      return Task.Run(() =>
        {
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

    public Task<bool> IsEmpty()
    {
      return Task.Run(() => !Directory.EnumerateFileSystemEntries(myRootDir).Any());
    }

    public async IAsyncEnumerable<ChildrenItem> GetChildren(ChildrenMode mode, [NotNull] string prefixDir)
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

    public Task InvalidateExternalServices(IEnumerable<string> keys)
    {
      return Task.CompletedTask;
    }

    private void TryRemoveEmptyDirsToRootDir([NotNull] string dir)
    {
      if (dir == null)
        throw new ArgumentNullException(nameof(dir));
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