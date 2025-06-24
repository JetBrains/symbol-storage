using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.SymbolStorage.Impl.Storages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JetBrains.SymbolStorage.Tests
{
  [TestClass]
  public class FileSystemStorageTest
  {
    private readonly struct StorageHolder(string path) : IDisposable
    {
      public string StoragePath { get; } = path;
      public FileSystemStorage Storage { get; } = new(path);

      public void Dispose()
      {
        Storage.Dispose();
        Directory.Delete(StoragePath, recursive: true);
      }
    }
    
    private static readonly byte[] OurTestData = Enumerable.Range(0, 1024).Select(i => (byte)(i % 256)).ToArray();
    
    private static StorageHolder CreateStorage()
    {
      var tempDir = Path.Combine(Path.GetTempPath(), $"filesystemstorage-{Guid.NewGuid():N}");
      return new StorageHolder(tempDir);
    }
    
    
    [TestMethod]
    public async Task PutDataToStorageTest()
    {
      using var storage = CreateStorage();
      var recordName = SymbolStoragePath.Combine("test_path", $"file_{Guid.NewGuid():N}.txt");
      try
      {
        await storage.Storage.CreateForWritingAsync(recordName, AccessMode.Public, new MemoryStream(OurTestData, false));
      }
      finally
      {
        await storage.Storage.DeleteAsync(recordName);
      }
    }
    
    [TestMethod]
    public async Task GetDataFromStorageTest()
    {
      using var storage = CreateStorage();
      var recordName = SymbolStoragePath.Combine("test_path", $"file_{Guid.NewGuid():N}.txt");
      try
      {
        await storage.Storage.CreateForWritingAsync(recordName, AccessMode.Public, new MemoryStream(OurTestData, false));

        var memoryStream = new MemoryStream();
        await storage.Storage.OpenForReadingAsync(recordName, async stream =>
        {
          await stream.CopyToAsync(memoryStream);
        });
        
        Assert.IsTrue(OurTestData.SequenceEqual(memoryStream.ToArray()));
      }
      finally
      {
        await storage.Storage.DeleteAsync(recordName);
      }
    }
    
    [TestMethod]
    public async Task GetDataLengthFromStorageTest()
    {
      using var storage = CreateStorage();
      var recordName = SymbolStoragePath.Combine("test_path", $"file_{Guid.NewGuid():N}.txt");
      try
      {
        await storage.Storage.CreateForWritingAsync(recordName, AccessMode.Public, new MemoryStream(OurTestData, false));

        Assert.AreEqual(OurTestData.Length, await storage.Storage.GetLengthAsync(recordName));
      }
      finally
      {
        await storage.Storage.DeleteAsync(recordName);
      }

      await Assert.ThrowsAsync<Exception>(async () =>
      {
        _ = await storage.Storage.GetLengthAsync(recordName);
      });
    }
    
    [TestMethod]
    public async Task ExistsInStorageTest()
    {
      using var storage = CreateStorage();
      var recordName = SymbolStoragePath.Combine("test_path", $"file_{Guid.NewGuid():N}.txt");
      try
      {
        Assert.IsFalse(await storage.Storage.ExistsAsync(recordName));
        
        await storage.Storage.CreateForWritingAsync(recordName, AccessMode.Public, new MemoryStream(OurTestData, false));

        Assert.IsTrue(await storage.Storage.ExistsAsync(recordName));
      }
      finally
      {
        await storage.Storage.DeleteAsync(recordName);
        Assert.IsFalse(await storage.Storage.ExistsAsync(recordName));
      }
    }
    
    [TestMethod] 
    public async Task DeleteDataFromStorageTest()
    {
      using var storage = CreateStorage();
      var recordName = SymbolStoragePath.Combine("test_path", $"file_{Guid.NewGuid():N}.txt");
      try
      {
        Assert.IsFalse(await storage.Storage.ExistsAsync(recordName));
        await storage.Storage.DeleteAsync(recordName); // Expecting that it is fine to delete non-existed record
        
        await storage.Storage.CreateForWritingAsync(recordName, AccessMode.Public, new MemoryStream(OurTestData, false));
        Assert.IsTrue(await storage.Storage.ExistsAsync(recordName));
        
        await storage.Storage.DeleteAsync(recordName);
        Assert.IsFalse(await storage.Storage.ExistsAsync(recordName));
      }
      finally
      {
        await storage.Storage.DeleteAsync(recordName);
      }
    }
    
    [TestMethod]
    public async Task PutOverwritesDataInStorageTest()
    {
      using var storage = CreateStorage();
      var recordName = SymbolStoragePath.Combine("test_path", $"file_{Guid.NewGuid():N}.txt");
      try
      {
        Assert.IsFalse(await storage.Storage.ExistsAsync(recordName));
        
        await storage.Storage.CreateForWritingAsync(recordName, AccessMode.Public, new MemoryStream(OurTestData, false));
        Assert.IsTrue(await storage.Storage.ExistsAsync(recordName));
        
        var modifiedTestData = OurTestData.ToArray();
        modifiedTestData[0] = unchecked((byte)(modifiedTestData[0] + 1));
        await storage.Storage.CreateForWritingAsync(recordName, AccessMode.Public, new MemoryStream(modifiedTestData, false));
        Assert.IsTrue(await storage.Storage.ExistsAsync(recordName));
        
        var memoryStream = new MemoryStream();
        await storage.Storage.OpenForReadingAsync(recordName, async stream =>
        {
          await stream.CopyToAsync(memoryStream);
        });
        
        Assert.IsTrue(modifiedTestData.SequenceEqual(memoryStream.ToArray()));
      }
      finally
      {
        await storage.Storage.DeleteAsync(recordName);
      }
    }
    
    [TestMethod]
    public async Task RenameInStorageTest()
    {
      using var storage = CreateStorage();
      var recordName = SymbolStoragePath.Combine("test_path", $"file_{Guid.NewGuid():N}.txt");
      var renamedRecordName = SymbolStoragePath.Combine("test_path", $"file_{Guid.NewGuid():N}_rn.txt");
      try
      {
        Assert.IsFalse(await storage.Storage.ExistsAsync(recordName));
        Assert.IsFalse(await storage.Storage.ExistsAsync(renamedRecordName));
        
        await storage.Storage.CreateForWritingAsync(recordName, AccessMode.Public, new MemoryStream(OurTestData, false));

        Assert.IsTrue(await storage.Storage.ExistsAsync(recordName));
        Assert.IsFalse(await storage.Storage.ExistsAsync(renamedRecordName));

        await storage.Storage.RenameAsync(recordName, renamedRecordName, AccessMode.Public);
        
        Assert.IsFalse(await storage.Storage.ExistsAsync(recordName));
        Assert.IsTrue(await storage.Storage.ExistsAsync(renamedRecordName));
        
        var memoryStream = new MemoryStream();
        await storage.Storage.OpenForReadingAsync(renamedRecordName, async stream =>
        {
          await stream.CopyToAsync(memoryStream);
        });
        
        Assert.IsTrue(OurTestData.SequenceEqual(memoryStream.ToArray()));
      }
      finally
      {
        await storage.Storage.DeleteAsync(recordName);
        await storage.Storage.DeleteAsync(renamedRecordName);
      }
    }
    
    [TestMethod]
    public async Task IsEmptyBucketTest()
    {
      using var storage = CreateStorage();
      var recordName = SymbolStoragePath.Combine($"test_path_{Guid.NewGuid():N}", $"file_{Guid.NewGuid():N}.txt");
      try
      {
        Assert.IsTrue(await storage.Storage.IsEmptyAsync());
        
        await storage.Storage.CreateForWritingAsync(recordName, AccessMode.Public, new MemoryStream(OurTestData, false));
        Assert.IsFalse(await storage.Storage.IsEmptyAsync());
      }
      finally
      {
        await storage.Storage.DeleteAsync(recordName);
      }
    }
    
    [TestMethod]
    public async Task GetChildrenTest()
    {
      using var storage = CreateStorage();
      var directoryName = new SymbolStoragePath($"test_path_{Guid.NewGuid():N}");
      var directory2Name = new SymbolStoragePath(directoryName.Path + "_2");
      var directory3Name = new SymbolStoragePath(directoryName.Path + "_3");
      var recordName = SymbolStoragePath.Combine(directoryName, $"file_{Guid.NewGuid():N}.txt");
      var record2Name = SymbolStoragePath.Combine(directoryName, $"file_{Guid.NewGuid():N}.txt");
      var recordInOtherDirName = SymbolStoragePath.Combine(directory2Name, $"file_{Guid.NewGuid():N}.txt");
      try
      {
        await storage.Storage.CreateForWritingAsync(recordName, AccessMode.Public, new MemoryStream(OurTestData, false));
        await storage.Storage.CreateForWritingAsync(record2Name, AccessMode.Public, new MemoryStream(OurTestData, false));
        await storage.Storage.CreateForWritingAsync(recordInOtherDirName, AccessMode.Public, new MemoryStream(OurTestData, false));

        var files = await (storage.Storage.GetChildrenAsync(ChildrenMode.WithSize, directoryName)).ToListAsync();
        Assert.AreEqual(2, files.Count);
        
        Assert.IsTrue(files.Any(f => f.FileName == recordName));
        Assert.IsTrue(files.Any(f => f.FileName == record2Name));
        Assert.IsTrue(files.All(f => f.Size == OurTestData.Length));
        
        
        files = await (storage.Storage.GetChildrenAsync(ChildrenMode.WithSize, directory2Name)).ToListAsync();
        Assert.AreEqual(1, files.Count);
        
        Assert.IsTrue(files.Any(f => f.FileName == recordInOtherDirName));
        Assert.IsTrue(files.All(f => f.Size == OurTestData.Length));
        
        
        files = await (storage.Storage.GetChildrenAsync(ChildrenMode.WithSize, directory3Name)).ToListAsync();
        Assert.AreEqual(0, files.Count);
        
        
        var fullCount = await (storage.Storage.GetChildrenAsync(ChildrenMode.WithSize, null)).CountAsync();
        Assert.AreEqual(3, fullCount);
      }
      finally
      {
        await storage.Storage.DeleteAsync(recordName);
        await storage.Storage.DeleteAsync(record2Name);
        await storage.Storage.DeleteAsync(recordInOtherDirName);
      }
    }
  }
}