#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using JetBrains.SymbolStorage.Impl.Commands;
using WixToolset.Dtf.Compression.Cab;

namespace JetBrains.SymbolStorage.Impl
{
  internal record struct CompressionFileInfo(string PathInArchive, string Path);
  
  internal static class CabCompressionUtil
  {
    public static void VerifyPlatformSupported()
    {
      if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        throw new PlatformNotSupportedException("The Windows PDB and PE compression works only on Windows");
    }
    
    
    public static void CompressFiles(string archiveFilePath, IEnumerable<CompressionFileInfo> files)
    {
      VerifyPlatformSupported();

      var fileMap = files.ToDictionary(k => k.PathInArchive, v => v.Path);
      new CabInfo(archiveFilePath).PackFileSet(null, fileMap);
      using var stream = File.Open(archiveFilePath, FileMode.Open, FileAccess.ReadWrite);
      PatchCompressed(stream);
    }

    private static void PatchCompressed(Stream stream)
    {
      // Bug: The C# library randomizes CCAB::setID in CabPacker::CreateFci(): `pccab.setID = checked ((short) new Random().Next((int) short.MinValue, 32768));`.
      //      See https://docs.microsoft.com/en-us/windows/win32/api/fci/ns-fci-ccab for details

      var buffer = new byte[0x40];

      var pos = stream.Position;
      if (stream.Read(buffer, 0, buffer.Length) != buffer.Length)
        throw new FormatException("Too short Microsoft CAB file");

      if (buffer[0] != 'M' || buffer[1] != 'S' || buffer[2] != 'C' || buffer[3] != 'F')
        throw new FormatException("Microsoft CAB file is expected");

      // setID
      const ushort id = 0xFFFF;
      buffer[0x20] = id & 0xFF;
      buffer[0x21] = id >> 8;

      // DOS date + DOS time, see https://docs.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-filetimetodosdatetime
      var dateTime = new DateTime(1980, 1, 1, 0, 0, 0);
      var date = dateTime.ToDosDate();
      var time = dateTime.ToDosTime();
      buffer[0x36] = (byte) date;
      buffer[0x37] = (byte) (date >> 8);
      buffer[0x38] = (byte) time;
      buffer[0x39] = (byte) (time >> 8);

      stream.Seek(pos, SeekOrigin.Begin);
      stream.Write(buffer, 0, buffer.Length);
    }
  }
}