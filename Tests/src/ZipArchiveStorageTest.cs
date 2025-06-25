using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.SymbolStorage.Impl.Storages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JetBrains.SymbolStorage.Tests
{
  [TestClass]
  public class ZipArchiveStorageTest
  {
    public enum RwMode
    {
      Read,
      Write,
      ReadWrite
    }

    private static StorageRwMode ConvertRwMode(RwMode mode) => mode switch
    {
      RwMode.Read => StorageRwMode.Read,
      RwMode.Write => StorageRwMode.Write,
      RwMode.ReadWrite => StorageRwMode.ReadWrite,
      _ => throw new ArgumentException("unknown rw mode")
    };
    
    private readonly struct StorageHolder(string path, StorageRwMode rwMode) : IDisposable
    {
      public string ArchivePath { get; } = path;
      public ZipArchiveStorage Storage { get; } = new(path, rwMode);

      public void Dispose()
      {
        Storage.Dispose();
        Directory.Delete(Path.GetDirectoryName(ArchivePath)!, recursive: true);
      }
    }
    
    private static readonly byte[] OurTestData = Enumerable.Range(0, 1024).Select(i => (byte)(i % 256)).ToArray();
    
    private static StorageHolder CreateEmptyStorage(RwMode rwMode = RwMode.ReadWrite)
    {
      var tempDir = Path.Combine(Path.GetTempPath(), $"zipstorage-{Guid.NewGuid():N}");
      Directory.CreateDirectory(tempDir);
      return new StorageHolder(Path.Combine(tempDir, "archive.zip"), ConvertRwMode(rwMode));
    }
    private static StorageHolder CreateStorageWithFiles(IEnumerable<string> files, RwMode rwMode = RwMode.ReadWrite)
    {
      var tempDir = Path.Combine(Path.GetTempPath(), $"zipstorage-{Guid.NewGuid():N}");
      Directory.CreateDirectory(tempDir);
      
      var filesDir = Path.Combine(tempDir, "files");
      foreach (var file in files)
      {
        var dir = Path.GetDirectoryName(file);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(Path.Combine(filesDir, dir)))
          Directory.CreateDirectory(Path.Combine(filesDir, dir));
        
        File.WriteAllBytes(Path.Combine(filesDir, file), OurTestData);
      }
      

      var archiveFile = Path.Combine(tempDir, "archive.zip");
      ZipFile.CreateFromDirectory(filesDir, archiveFile);
      
      return new StorageHolder(archiveFile, ConvertRwMode(rwMode));
    }



    [DataTestMethod]
    [DataRow(RwMode.Read)]
    [DataRow(RwMode.ReadWrite)]
    public async Task ExistsInPreallocatedStorageTest(RwMode rwMode)
    {
      using var storage = CreateStorageWithFiles([Path.Combine("abc", "bcd.bin"), Path.Combine("efk.bin")], rwMode);
      var recordName = SymbolStoragePath.Combine("abc", "bcd.bin");
      var recordName2 = new SymbolStoragePath("efk.bin");
      var recordNameNonExisted = SymbolStoragePath.Combine("abc", "www.dat");

      Assert.IsTrue(await storage.Storage.ExistsAsync(recordName));
      Assert.IsTrue(await storage.Storage.ExistsAsync(recordName2));
      Assert.IsFalse(await storage.Storage.ExistsAsync(recordNameNonExisted));
    }
    
    [DataTestMethod]
    [DataRow(RwMode.Read)]
    [DataRow(RwMode.ReadWrite)]
    public async Task GetDataLengthFromPreallocatedStorageTest(RwMode rwMode)
    {
      using var storage = CreateStorageWithFiles([Path.Combine("abc", "bcd.bin")], rwMode);
      var recordName = SymbolStoragePath.Combine("abc", "bcd.bin");
      var recordNameNonExisted = SymbolStoragePath.Combine("abc", "www.dat");
      
      Assert.AreEqual(OurTestData.Length, await storage.Storage.GetLengthAsync(recordName));
      
      await Assert.ThrowsAsync<Exception>(async () =>
      {
        _ = await storage.Storage.GetLengthAsync(recordNameNonExisted);
      });
    }
  }
}