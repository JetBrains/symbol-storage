using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace JetBrains.SymbolStorage.Impl
{
  internal static class PathUtil
  {
    private const string PdbExt = ".pdb";
    private const string DllExt = ".dll";
    private const string ExeExt = ".exe";

    public static string GetPackedExtension(string ext)
    {
      if (!ext.StartsWith("."))
        throw new Exception("Invalid extension format");
      if (ext.Length < 1)
        throw new Exception("At least one symbol in extension is expected");
      return ext.Substring(0, ext.Length - 1) + '_';
    }
    
    public static string[] GetPathComponents(this string? path) => string.IsNullOrEmpty(path) ? Array.Empty<string>() : path.Split(Path.DirectorySeparatorChar);
    
    public static MemoryExtensions.SpanSplitEnumerator<char> GetPathComponents(this ReadOnlySpan<char> path)
    {
      if (path.Length == 0)
        return new MemoryExtensions.SpanSplitEnumerator<char>();
      return path.Split(Path.DirectorySeparatorChar);
    }

    public enum ValidateAndFixErrors
    {
      Ok,
      CanBeFixed,
      Error
    }

    public static ValidateAndFixErrors ValidateAndFixDataPath(this string path, StorageFormat storageFormat, out string fixedPath)
    {
      fixedPath = path;
      var parts = path.GetPathComponents();
      if (parts.Length != 2 && parts.Length != 3)
        return ValidateAndFixErrors.Error;
      if (parts.Any(x => x.Length == 0))
        return ValidateAndFixErrors.Error;

      switch (storageFormat)
      {
      case StorageFormat.Normal:
        {
          var namePartLower = parts[0].ToLowerInvariant();
          var hashPartLower = parts[1].ToLowerInvariant();

          var nameExt = Path.GetExtension(namePartLower);
          if (nameExt == PdbExt)
          {
            if (!hashPartLower.All(IsHex))
              return ValidateAndFixErrors.Error;

            if (hashPartLower.Length == 40)
            {
              // Note: See https://github.com/dotnet/symstore/blob/master/docs/specs/SSQP_Key_Conventions.md#portable-pdb-signature
              //       This code expects that the real age never be 0xFFFFFFFF!!!
              if (hashPartLower.Substring(32, 8) == "ffffffff")
                hashPartLower = hashPartLower.Substring(0, 32) + "FFFFFFFF";
            }
          }
          else if (nameExt == DllExt || nameExt == ExeExt)
          {
            if (!hashPartLower.All(IsHex))
              return ValidateAndFixErrors.Error;

            if (hashPartLower.Length > 8)
            {
              // Note: See https://github.com/dotnet/symstore/blob/master/docs/specs/SSQP_Key_Conventions.md#pe-timestamp-filesize
              hashPartLower = hashPartLower.Substring(0, 8).ToUpperInvariant() + hashPartLower.Substring(8);
            }
          }

          var builder = new StringBuilder()
            .Append(namePartLower)
            .Append(Path.DirectorySeparatorChar)
            .Append(hashPartLower);

          if (parts.Length > 2)
          {
            var filePartLower = parts[2].ToLowerInvariant();
            if (filePartLower.EndsWith("_"))
            {
              if (namePartLower.Substring(0, namePartLower.Length - 1) != filePartLower.Substring(0, namePartLower.Length - 1))
                return ValidateAndFixErrors.Error;
            }
            else if (namePartLower != filePartLower)
              return ValidateAndFixErrors.Error;

            builder
              .Append(Path.DirectorySeparatorChar)
              .Append(filePartLower);
          }

          var newPath = builder.ToString();
          if (newPath == path)
            return ValidateAndFixErrors.Ok;

          fixedPath = newPath;
          return ValidateAndFixErrors.CanBeFixed;
        }
      case StorageFormat.LowerCase:
        {
          var pathOrig = path.ToLowerInvariant();
          if (path == pathOrig)
            return ValidateAndFixErrors.Ok;
          fixedPath = pathOrig;
          return ValidateAndFixErrors.CanBeFixed;
        }
      case StorageFormat.UpperCase:
        {
          var pathOrig = path.ToUpperInvariant();
          if (path == pathOrig)
            return ValidateAndFixErrors.Ok;
          fixedPath = pathOrig;
          return ValidateAndFixErrors.CanBeFixed;
        }
      default:
        throw new ArgumentOutOfRangeException(nameof(storageFormat), storageFormat, null);
      }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsHex(this char ch) =>
      ch >= '0' && ch <= '9' ||
      ch >= 'a' && ch <= 'f' ||
      ch >= 'A' && ch <= 'F';

    public static string CheckSystemFile(this string? file)
    {
      if (string.IsNullOrEmpty(file))
        throw new ArgumentNullException(nameof(file));
      if (Path.DirectorySeparatorChar == file[0] ||
          Path.DirectorySeparatorChar == file[^1])
        throw new ArgumentException(null, nameof(file));
      if (file.Contains(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? '/' : '\\'))
        throw new ArgumentException(null, nameof(file));
      return file;
    }
    
    public static string NormalizeLinux(this string path)
    {
      if (path == null)
        throw new ArgumentNullException(nameof(path));
      return path.Replace('\\', '/');
    }

    public static string NormalizeSystem(this string path)
    {
      if (path == null)
        throw new ArgumentNullException(nameof(path));
      return path.Replace('/', Path.DirectorySeparatorChar);
    }
    
    public static bool IsPeWithWeakHashFile(this string path)
    {
      var extension = Path.GetExtension(path.AsSpan());
      if (extension.Length != 4)
        return false;

      Span<char> loweredExt = stackalloc char[4];
      extension.ToLowerInvariant(loweredExt);

      // Check extension
      if (!(loweredExt is ".exe" || loweredExt is ".dll" || loweredExt is ".sys" ||
            loweredExt is ".ex_" || loweredExt is ".dl_" || loweredExt is ".sy_"))
      {
        return false;
      }

      // Check for weak hash
      var directory = Path.GetFileName(Path.GetDirectoryName(path.AsSpan()));
      if (directory.Length <= 8 || directory.Length > 18)
        return false;

      for (int i = 0; i < directory.Length; i++)
      {
        if (!IsHex(directory[i]))
          return false;
      }

      return true;
    }
  }
}