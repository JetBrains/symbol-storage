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

      public void Close()
      {
        Storage.Dispose();
      }
      
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
    
    [TestMethod]
    public async Task DeleteFromPreallocatedStorageTest()
    {
      using var storage = CreateStorageWithFiles([Path.Combine("abc", "bcd.bin")], RwMode.ReadWrite);
      var recordName = SymbolStoragePath.Combine("abc", "bcd.bin");
      var recordNameNonExisted = SymbolStoragePath.Combine("abc", "www.dat");
      
      Assert.IsTrue(await storage.Storage.ExistsAsync(recordName));
      Assert.IsFalse(await storage.Storage.ExistsAsync(recordNameNonExisted));
      
      await storage.Storage.DeleteAsync(recordName);
      Assert.IsFalse(await storage.Storage.ExistsAsync(recordName));
      
      await storage.Storage.DeleteAsync(recordNameNonExisted);
      Assert.IsFalse(await storage.Storage.ExistsAsync(recordNameNonExisted));
    }
        
    [TestMethod]
    public async Task RenameInPreallocatedStorageTest()
    {
      using var storage = CreateStorageWithFiles([Path.Combine("abc", "bcd.bin")], RwMode.ReadWrite);
      var recordName = SymbolStoragePath.Combine("abc", "bcd.bin");
      var newRecordName = SymbolStoragePath.Combine("efk", "www.dat");
      
      Assert.IsTrue(await storage.Storage.ExistsAsync(recordName));
      Assert.IsFalse(await storage.Storage.ExistsAsync(newRecordName));
      
      await storage.Storage.RenameAsync(recordName, newRecordName, AccessMode.Public);
      Assert.IsFalse(await storage.Storage.ExistsAsync(recordName));
      Assert.IsTrue(await storage.Storage.ExistsAsync(newRecordName));
    }
    
    [DataTestMethod]
    [DataRow(RwMode.Read)]
    [DataRow(RwMode.ReadWrite)]
    public async Task ReadFromPreallocatedStorageTest(RwMode rwMode)
    {
      using var storage = CreateStorageWithFiles([Path.Combine("abc", "bcd.bin")], rwMode);
      var recordName = SymbolStoragePath.Combine("abc", "bcd.bin");
      var recordNameNonExisted = SymbolStoragePath.Combine("abc", "www.dat");

      using var memoryStream = new MemoryStream();
      await storage.Storage.OpenForReadingAsync(recordName, async stream => await stream.CopyToAsync(memoryStream));
      
      Assert.IsTrue(OurTestData.SequenceEqual(memoryStream.ToArray()));
      
      await Assert.ThrowsAsync<Exception>(async () =>
      {
        await storage.Storage.OpenForReadingAsync(recordNameNonExisted, async stream => await stream.CopyToAsync(memoryStream));
      });
    }

    [TestMethod]
    [DataRow(RwMode.Read)]
    [DataRow(RwMode.ReadWrite)]
    public async Task GetChildrenFromPreallocatedStorageTest(RwMode rwMode)
    {
      using var storage = CreateStorageWithFiles([
        Path.Combine("test_path_1", "file_1.txt"),
        Path.Combine("test_path_1", "file_2.txt"),
        Path.Combine("test_path_2", "file_3.txt")
      ], rwMode);
      var directoryName = new SymbolStoragePath("test_path_1");
      var directory2Name = new SymbolStoragePath("test_path_2");
      var directory3Name = new SymbolStoragePath("test_path_3");
      var recordName = SymbolStoragePath.Combine(directoryName, "file_1.txt");
      var record2Name = SymbolStoragePath.Combine(directoryName, "file_2.txt");
      var recordInOtherDirName = SymbolStoragePath.Combine(directory2Name, "file_3.txt");


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


    [TestMethod]
    public async Task PutDataToStorageTest()
    {
      using var storage = CreateEmptyStorage();
      var recordName = new SymbolStoragePath("test_path/file.txt");
      try
      {
        await storage.Storage.CreateForWritingAsync(recordName, AccessMode.Public, new MemoryStream(OurTestData, false));
        Assert.IsTrue(await storage.Storage.ExistsAsync(recordName));
      }
      finally
      {
        await storage.Storage.DeleteAsync(recordName);
      }
    }

    [TestMethod]
    public async Task GetDataFromStorageTest()
    {
      using var storage = CreateEmptyStorage();
      var recordName = new SymbolStoragePath("test_path/file.txt");

      await storage.Storage.CreateForWritingAsync(recordName, AccessMode.Public, new MemoryStream(OurTestData, false));

      var memoryStream = new MemoryStream();
      await storage.Storage.OpenForReadingAsync(recordName, async stream => { await stream.CopyToAsync(memoryStream); });

      Assert.IsTrue(OurTestData.SequenceEqual(memoryStream.ToArray()));
    }
    
    [TestMethod]
    public async Task PutReopenGetTest()
    {
      var recordName = new SymbolStoragePath("test_path/file.txt");

      using var storage = CreateEmptyStorage();
      await storage.Storage.CreateForWritingAsync(recordName, AccessMode.Public, new MemoryStream(OurTestData, false));
      storage.Close();

      using (var storageForRead = new ZipArchiveStorage(storage.ArchivePath, StorageRwMode.Read))
      {
        var memoryStream = new MemoryStream();
        await storageForRead.OpenForReadingAsync(recordName, async stream => { await stream.CopyToAsync(memoryStream); });

        Assert.IsTrue(OurTestData.SequenceEqual(memoryStream.ToArray())); 
      }
    }
    
    [TestMethod]
    public async Task GetDataLengthFromStorageTest()
    {
      using var storage = CreateEmptyStorage();
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
      using var storage = CreateEmptyStorage();
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
      using var storage = CreateEmptyStorage();
      var recordName = SymbolStoragePath.Combine("test_path", $"file_{Guid.NewGuid():N}.txt");
      var recordName2 = new SymbolStoragePath($"file_{Guid.NewGuid():N}.txt");
      try
      {
        await storage.Storage.DeleteAsync(recordName2);
        
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
      using var storage = CreateEmptyStorage();
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
      using var storage = CreateEmptyStorage();
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
    public async Task IsEmptyTest()
    {
      using var storage = CreateEmptyStorage();
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
      using var storage = CreateEmptyStorage();
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