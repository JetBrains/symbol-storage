using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
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
    
    /// <summary>
    /// Creates CAB archive with single file inside.
    /// The method guarantees a stable result, so that compressed files can be compared by hash.
    /// </summary>
    /// <remarks>
    /// Since metadata patching is required, only single file compression is currently supported
    /// </remarks>
    /// <param name="archiveFilePath">CAB-archive file path</param>
    /// <param name="file">File to be compressed into archive</param>
    public static void CompressFile(string archiveFilePath, CompressionFileInfo file)
    {
      VerifyPlatformSupported();

      var fileMap = new Dictionary<string, string>() { [file.PathInArchive] = file.Path };
      new CabInfo(archiveFilePath).PackFileSet(null, fileMap);
      using var stream = File.Open(archiveFilePath, FileMode.Open, FileAccess.ReadWrite);
      PatchCompressed(File.GetLastWriteTime(file.Path), stream);
    }

    private static void PatchCompressed(DateTime writeSourceFileTime, Stream stream)
    {
      // Bug: The C# library randomizes CCAB::setID in CabPacker::CreateFci(): `pccab.setID = checked ((short) new Random().Next((int) short.MinValue, 32768));`.
      //      See https://docs.microsoft.com/en-us/windows/win32/api/fci/ns-fci-ccab for details

      var buffer = new byte[0x40];

      var pos = stream.Position;
      if (stream.Read(buffer, 0, buffer.Length) != buffer.Length)
        throw new FormatException("Too short Microsoft CAB file");

      if (buffer[0] != 'M' || buffer[1] != 'S' || buffer[2] != 'C' || buffer[3] != 'F')
        throw new FormatException("Microsoft CAB file is expected");
      
      // Additional time correctness validation
      var span = TimeSpan.FromSeconds(2);
      var ceil = writeSourceFileTime.ToCeil(span);
      var floor = writeSourceFileTime.ToFloor(span);
      if (floor.Year >= 1980)
      {
        var cab = DateTimeUtil.ToDateTime(
          ToUInt16(buffer[0x37], buffer[0x36]),
          ToUInt16(buffer[0x39], buffer[0x38]));
        if (cab < floor || ceil < cab)
          throw new FormatException("The time in the CAB-file record is out of 2 seconds range which can be patched");
      }
      else
      {
        throw new FormatException("The source time after rounding to floor is early then 1.1.1980");
      }

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
    
    private static ushort ToUInt16(byte hi, byte lo)
    {
      return (ushort) ((hi << 8) | lo);
    }
  }
}