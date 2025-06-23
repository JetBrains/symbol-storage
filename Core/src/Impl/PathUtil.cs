using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using JetBrains.SymbolStorage.Impl.Storages;

namespace JetBrains.SymbolStorage.Impl
{
  internal static class PathUtil
  {
    private const string PdbExt = ".pdb";
    private const string DllExt = ".dll";
    private const string ExeExt = ".exe";

    public static string GetPackedExtension(string ext)
    {
      return GetPackedExtension(ext.AsSpan());
    }
    public static string GetPackedExtension(ReadOnlySpan<char> ext)
    {
      if (!ext.StartsWith('.'))
        throw new Exception("Invalid extension format");
      if (ext.Length < 1)
        throw new Exception("At least one symbol in extension is expected");
      
      return string.Concat(ext.Slice(0, ext.Length - 1), "_".AsSpan());
    }
    
    public static string[] GetPathComponents(this string? path) => string.IsNullOrEmpty(path) ? Array.Empty<string>() : path.Split(Path.DirectorySeparatorChar);
    
    public enum ValidateAndFixErrors
    {
      Ok,
      CanBeFixed,
      Error
    }
    
    public static ValidateAndFixErrors ValidateAndFixDataPath(this SymbolStoragePath storagePath, StorageFormat storageFormat, out SymbolStoragePath fixedStoragePath)
    {
      fixedStoragePath = storagePath;
      var parts = storagePath.GetPathComponents();
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

          var nameExt = Path.GetExtension(namePartLower.AsSpan());
          if (nameExt is PdbExt)
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
          else if (nameExt is DllExt || nameExt is ExeExt)
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
            .Append(SymbolStoragePath.DirectorySeparator)
            .Append(hashPartLower);

          if (parts.Length > 2)
          {
            var filePartLower = parts[2].ToLowerInvariant();
            if (filePartLower.EndsWith('_'))
            {
              if (namePartLower.Substring(0, namePartLower.Length - 1) != filePartLower.Substring(0, namePartLower.Length - 1))
                return ValidateAndFixErrors.Error;
            }
            else if (namePartLower != filePartLower)
              return ValidateAndFixErrors.Error;

            builder
              .Append(SymbolStoragePath.DirectorySeparator)
              .Append(filePartLower);
          }

          var newPath = builder.ToString();
          if (storagePath == newPath)
            return ValidateAndFixErrors.Ok;

          fixedStoragePath = new SymbolStoragePath(newPath);
          return ValidateAndFixErrors.CanBeFixed;
        }
      case StorageFormat.LowerCase:
        {
          var pathOrig = storagePath.ToLower();
          if (storagePath == pathOrig)
            return ValidateAndFixErrors.Ok;
          fixedStoragePath = pathOrig;
          return ValidateAndFixErrors.CanBeFixed;
        }
      case StorageFormat.UpperCase:
        {
          var pathOrig = storagePath.ToUpper();
          if (storagePath == pathOrig)
            return ValidateAndFixErrors.Ok;
          fixedStoragePath = pathOrig;
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
    
    public static bool IsPeFileWithWeakHash(this SymbolStoragePath path)
    {
      var extension = SymbolStoragePath.GetExtension(path.AsRef());
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
      var directory = SymbolStoragePath.GetFileName(SymbolStoragePath.GetDirectoryName(path.AsRef()));
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