#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.SymbolStorage.Impl;
using Microsoft.SymbolStore;
using JetBrains.SymbolStorage.Impl.Logger;
using JetBrains.SymbolStorage.Impl.Storages;
using JetBrains.SymbolStorage.Impl.Tags;
using Microsoft.SymbolStore.KeyGenerators;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JetBrains.SymbolStorage.Tests.Commands
{
  internal static class CommandTestUtil
  {
    private static readonly byte[] MinimalPe = [77, 90, 0, 0, 80, 69, 0, 0, 100, 134, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 128, 0, 34, 0, 11, 2, 73, 78, 75, 66, 79, 88, 0, 0, 0, 0, 0, 0, 0, 0, 156, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 4, 0, 0, 0, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 6, 0, 0, 0, 0, 0, 0, 0, 12, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 104, 0, 1, 0, 0, 88, 195, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];
    private const int PeTimeOffset = 12;
    private const int PeModificationOffset = 158;

    private static readonly byte[] MinimalPortablePdb = [66, 83, 74, 66, 1, 0, 1, 0, 0, 0, 0, 0, 12, 0, 0, 0, 80, 68, 66, 32, 118, 49, 46, 48, 0, 0, 0, 0, 0, 0, 6, 0, 124, 0, 0, 0, 64, 0, 0, 0, 35, 80, 100, 98, 0, 0, 0, 0, 188, 0, 0, 0, 180, 0, 0, 0, 35, 126, 0, 0, 112, 1, 0, 0, 4, 0, 0, 0, 35, 83, 116, 114, 105, 110, 103, 115, 0, 0, 0, 0, 116, 1, 0, 0, 4, 0, 0, 0, 35, 85, 83, 0, 120, 1, 0, 0, 32, 0, 0, 0, 35, 71, 85, 73, 68, 0, 0, 0, 152, 1, 0, 0, 48, 1, 0, 0, 35, 66, 108, 111, 98, 0, 0, 0, 137, 3, 134, 173, 255, 39, 86, 70, 159, 63, 226, 24, 75, 239, 252, 192, 190, 12, 82, 160, 0, 0, 0, 0, 71, 20, 0, 0, 9, 0, 0, 0, 1, 0, 0, 0, 6, 0, 0, 0, 2, 0, 0, 0, 2, 0, 0, 0, 6, 0, 0, 0, 3, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 2, 0, 0, 1, 0, 0, 0, 0, 0, 0, 39, 0, 0, 0, 0, 0, 0, 0, 4, 0, 13, 0, 0, 0, 2, 0, 0, 0, 1, 0, 0, 0, 2, 0, 0, 0, 27, 0, 1, 0, 31, 0, 2, 0, 65, 0, 0, 0, 0, 0, 2, 0, 80, 0, 0, 0, 0, 0, 2, 0, 95, 0, 0, 0, 0, 0, 2, 0, 108, 0, 0, 0, 0, 0, 2, 0, 118, 0, 0, 0, 0, 0, 2, 0, 125, 0, 0, 0, 0, 0, 2, 0, 133, 0, 0, 0, 0, 0, 2, 0, 145, 0, 0, 0, 0, 0, 2, 0, 161, 0, 0, 0, 0, 0, 2, 0, 174, 0, 0, 0, 0, 0, 2, 0, 183, 0, 0, 0, 0, 0, 2, 0, 190, 0, 0, 0, 0, 0, 2, 0, 0, 0, 197, 0, 0, 0, 0, 0, 1, 0, 2, 0, 1, 0, 1, 0, 0, 0, 0, 0, 99, 0, 0, 0, 0, 0, 0, 0, 1, 0, 8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 236, 22, 24, 255, 94, 170, 16, 77, 135, 247, 111, 73, 99, 131, 52, 96, 248, 98, 81, 63, 198, 7, 211, 17, 144, 83, 0, 192, 79, 163, 2, 161, 0, 6, 83, 121, 115, 116, 101, 109, 2, 1, 1, 2, 67, 58, 12, 68, 111, 99, 117, 109, 101, 110, 116, 115, 46, 99, 115, 3, 92, 11, 14, 20, 219, 235, 42, 6, 123, 47, 14, 13, 103, 138, 0, 44, 88, 122, 40, 6, 5, 108, 61, 206, 1, 97, 1, 98, 1, 99, 1, 100, 4, 49, 46, 99, 115, 7, 92, 11, 52, 54, 56, 58, 60, 1, 68, 4, 50, 46, 99, 115, 7, 92, 11, 52, 54, 56, 73, 75, 1, 67, 4, 51, 46, 99, 115, 7, 92, 11, 52, 54, 88, 58, 90, 4, 120, 46, 99, 115, 7, 92, 11, 52, 54, 56, 58, 103, 1, 65, 6, 92, 11, 116, 54, 56, 103, 5, 92, 11, 52, 54, 103, 1, 66, 6, 92, 11, 52, 128, 131, 90, 4, 52, 46, 99, 115, 8, 92, 11, 52, 128, 131, 56, 128, 140, 1, 42, 4, 53, 46, 99, 115, 6, 92, 11, 128, 154, 128, 156, 5, 58, 54, 46, 99, 115, 3, 47, 128, 168, 4, 88, 46, 99, 115, 6, 92, 11, 52, 54, 128, 178, 6, 92, 11, 52, 128, 131, 103, 104, 0, 1, 0, 0, 1, 7, 5, 0, 2, 1, 0, 21, 6, 8, 0, 3, 7, 0, 21, 20, 0, 0, 4, 7, 0, 21, 20, 0, 7, 0, 21, 20, 0, 7, 0, 0, 0, 5, 6, 0, 21, 20, 0, 0, 6, 7, 0, 21, 20, 0, 0, 7, 7, 0, 21, 20, 0, 0, 8, 7, 0, 21, 20, 0, 0, 9, 7, 0, 21, 20, 0, 0, 10, 7, 0, 21, 20, 0, 0, 11, 7, 0, 21, 20, 0, 0, 12, 7, 0, 21, 20, 0, 0, 13, 7, 0, 21, 20, 0, 7, 0, 1, 2, 121, 0, 0];
    private const int PortablePdbGuidOffset = 124;
    private const int PortablePdbModificationOffset = 431;
    
    public static byte[] CreateExeFile(DateTime createdAt, byte contentByte)
    {
      var result = MinimalPe.ToArray();
      uint unixTime = (uint)(createdAt - DateTime.UnixEpoch).TotalSeconds;
      BitConverter.TryWriteBytes(result.AsSpan(PeTimeOffset, 4), unixTime);
      result[PeModificationOffset] = contentByte;
      return result;
    }
    
    public static string GetPePathInStorage(string name, Stream content)
    {
      var keyGenerator = new PEFileKeyGenerator(new DummyTracer(), new SymbolStoreFile(content, name));
      Assert.IsTrue(keyGenerator.IsValid());
      var key = keyGenerator.GetKeys(KeyTypeFlags.IdentityKey).Single();
      return key.Index.NormalizeSystem();
    }
    
    public static byte[] CreatePortablePdbFile(Guid pdbId, char contentByte)
    {
      var result = MinimalPortablePdb.ToArray();
      pdbId.TryWriteBytes(result.AsSpan(PortablePdbGuidOffset));
      result[PortablePdbModificationOffset] = (byte)contentByte;
      return result;
    }
    
    public static string GetPortablePdbPathInStorage(string name, Stream content)
    {
      var keyGenerator = new PortablePDBFileKeyGenerator(new DummyTracer(), new SymbolStoreFile(content, name));
      Assert.IsTrue(keyGenerator.IsValid());
      var key = keyGenerator.GetKeys(KeyTypeFlags.IdentityKey).Single();
      return key.Index.NormalizeSystem();
    }
    
    public static async Task WriteTag(IStorage storage, string product, string version, IEnumerable<string> dirs)
    {
      var fileId = Guid.NewGuid();
      using var stream = new MemoryStream();
      await TagUtil.WriteTagScriptAsync(new Tag
      {
        ToolId = "tool",
        FileId = fileId,
        Product = product,
        Version = version,
        CreationUtcTime = DateTime.UtcNow,
        IsProtected = false,
        Properties = [],
        Directories = dirs.OrderBy(x => x, StringComparer.Ordinal).Distinct().ToArray()
      }, stream);

      await storage.CreateForWritingAsync(TagUtil.MakeTagFile(new Identity(product, version), fileId), AccessMode.Private, stream);
    }

    public static async Task<byte[]> LoadFileFromStorage(IStorage storage, string path)
    {
      var memoryStream = new MemoryStream();
      await storage.OpenForReadingAsync(path, async srcStream =>
      {
        await srcStream.CopyToAsync(memoryStream);
      });
      return memoryStream.ToArray();
    }
    
  }
  
  
  internal class DummyTracer : ITracer
  {
    public void WriteLine(string message) { }
    public void WriteLine(string format, params object[] arguments) { }
    public void Information(string message) { }
    public void Information(string format, params object[] arguments) { }
    public void Warning(string message) { }
    public void Warning(string format, params object[] arguments) { }
    public void Error(string message) { }
    public void Error(string format, params object[] arguments) { }
    public void Verbose(string message) { }
    public void Verbose(string format, params object[] arguments) { }
  }
    
  internal class DummyLogger : ILogger
  {
    public void Verbose(string str) { }
    public void Info(string str) { }
    public void Fix(string str) { }
    public void Warning(string str) { }
    public void Error(string str) { }
  }
}