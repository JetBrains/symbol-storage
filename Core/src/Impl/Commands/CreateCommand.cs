using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using JetBrains.Annotations;
using JetBrains.SymbolStorage.Impl.Logger;
using JetBrains.SymbolStorage.Impl.Storages;
using JetBrains.SymbolStorage.Impl.Tags;
using Microsoft.Deployment.Compression.Cab;

namespace JetBrains.SymbolStorage.Impl.Commands
{
  internal sealed class CreateCommand : ICommand
  {
    private readonly StorageFormat myExpectedStorageFormat;
    private readonly bool myIsCompressPe;
    private readonly bool myIsCompressWPdb;
    private readonly bool myIsKeepNonCompressed;
    private readonly ILogger myLogger;
    private readonly IEnumerable<KeyValuePair<string, string>> myProperties;
    private readonly IReadOnlyCollection<string> mySources;
    private readonly IStorage myStorage;
    private readonly string myToolId;
    private readonly Identity myIdentity;
    private readonly int myDegreeOfParallelism;
    private readonly bool myIsProtected;

    public CreateCommand(
      [NotNull] ILogger logger,
      [NotNull] IStorage storage,
      int degreeOfParallelism,
      StorageFormat expectedStorageFormat,
      [NotNull] string toolId,
      [NotNull] Identity identity,
      bool isProtected,
      bool isCompressPe,
      bool isCompressWPdb,
      bool isKeepNonCompressed,
      [NotNull] IEnumerable<KeyValuePair<string, string>> properties,
      [NotNull] IReadOnlyCollection<string> sources)
    {
      myLogger = logger ?? throw new ArgumentNullException(nameof(logger));
      myStorage = storage ?? throw new ArgumentNullException(nameof(storage));
      myDegreeOfParallelism = degreeOfParallelism;
      myExpectedStorageFormat = expectedStorageFormat;
      myToolId = toolId ?? throw new ArgumentNullException(nameof(toolId));
      myIdentity = identity ?? throw new ArgumentNullException(nameof(identity));
      myIsProtected = isProtected;
      myIsCompressPe = isCompressPe;
      myIsCompressWPdb = isCompressWPdb;
      myIsKeepNonCompressed = isKeepNonCompressed;
      myProperties = properties ?? throw new ArgumentNullException(nameof(properties));
      mySources = sources ?? throw new ArgumentNullException(nameof(sources));
    }

    public async Task<int> ExecuteAsync()
    {
      await new Validator(myLogger, myStorage).CreateStorageMarkersAsync(myExpectedStorageFormat);

      var dstFiles = new ConcurrentBag<string>();
      var statistics = await new Scanner(myLogger, myDegreeOfParallelism, myIsCompressPe, myIsCompressWPdb, myIsKeepNonCompressed, mySources,
        async (srcDir, srcFile, dstFile) =>
          {
            await WriteData(Path.Combine(srcDir, srcFile), stream => myStorage.CreateForWritingAsync(dstFile, AccessMode.Public, stream));
            dstFiles.Add(dstFile);
          },
        async (srcDir, srcFile, dstFile) =>
          {
            await WriteDataPacked(Path.Combine(srcDir, srcFile), dstFile, stream => myStorage.CreateForWritingAsync(dstFile, AccessMode.Public, stream));
            dstFiles.Add(dstFile);
          }).ExecuteAsync();
      myLogger.Info($"[{DateTime.Now:s}] Done with data (warnings: {statistics.Warnings}, errors: {statistics.Errors})");
      if (statistics.HasProblems)
      {
        myLogger.Error("Found some issues, creating was interrupted");
        return 1;
      }

      await WriteTag(dstFiles.Select(Path.GetDirectoryName).Distinct());
      await myStorage.InvalidateExternalServicesAsync();
      return 0;
    }

    private async Task WriteTag([NotNull] IEnumerable<string> dirs)
    {
      myLogger.Info($"[{DateTime.Now:s}] Writing tag file...");
      var fileId = Guid.NewGuid();
      await using var stream = new MemoryStream();
      await TagUtil.WriteTagScriptAsync(new Tag
        {
          ToolId = myToolId,
          FileId = fileId,
          Product = myIdentity.Product,
          Version = myIdentity.Version,
          CreationUtcTime = DateTime.UtcNow,
          IsProtected = myIsProtected,
          Properties = myProperties.Select(x => new TagKeyValue
            {
              Key = x.Key,
              Value = x.Value
            }).ToArray(),
          Directories = dirs.OrderBy(x => x, StringComparer.Ordinal).ToArray()
        }, stream);

      await myStorage.CreateForWritingAsync(TagUtil.MakeTagFile(myIdentity, fileId), AccessMode.Private, stream);
    }

    private static async Task WriteData(
      [NotNull] string sourceFile,
      [NotNull] Func<Stream, Task> writeStorageFile)
    {
      await using var stream = File.OpenRead(sourceFile);
      await writeStorageFile(stream);
    }

    private static async Task WriteDataPacked(
      [NotNull] string sourceFile,
      [NotNull] string packedStorageRelativeFile,
      [NotNull] Func<Stream, Task> writeStorageFile)
    {
      if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        throw new PlatformNotSupportedException("The Windows PDB and PE compression works only on Windows");
      var tempFile = Path.GetTempFileName();
      try
      {
        new CabInfo(tempFile).PackFileSet(Path.GetDirectoryName(sourceFile), new Dictionary<string, string>
          {
            // Note: packedStorageRelativeFile should be in following format: [cc/]aaa.bbb/<hash>/aaa.bb_ 
            {Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(packedStorageRelativeFile)))!, Path.GetFileName(sourceFile)}
          });

        await using var stream = File.Open(tempFile, FileMode.Open, FileAccess.ReadWrite);
        PatchCompressed(File.GetLastWriteTime(sourceFile), stream);
        await writeStorageFile(stream);
      }
      finally
      {
        File.Delete(tempFile);
      }
    }

    private static void PatchCompressed(DateTime writeSourceFileTime, [NotNull] Stream stream)
    {
      if (stream == null)
        throw new ArgumentNullException(nameof(stream));
      // Bug: The C# library randomizes CCAB::setID in CabPacker::CreateFci(): `pccab.setID = checked ((short) new Random().Next((int) short.MinValue, 32768));`.
      //      See https://docs.microsoft.com/en-us/windows/win32/api/fci/ns-fci-ccab for details

      var buffer = new byte[0x40];

      var pos = stream.Position;
      if (stream.Read(buffer, 0, buffer.Length) != buffer.Length)
        throw new FormatException("Too short Microsoft CAB file");

      if (buffer[0] != 'M' || buffer[1] != 'S' || buffer[2] != 'C' || buffer[3] != 'F')
        throw new FormatException("Microsoft CAB file is expected");

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