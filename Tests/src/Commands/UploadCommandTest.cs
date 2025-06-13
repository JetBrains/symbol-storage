using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.SymbolStorage.Impl;
using JetBrains.SymbolStorage.Impl.Commands;
using JetBrains.SymbolStorage.Impl.Storages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JetBrains.SymbolStorage.Tests.Commands
{
  [TestClass]
  public class UploadCommandTest
  {
    [TestMethod]
    public async Task UploadCommandHappyPathTest()
    {
      var testDir = Path.Combine(Path.GetTempPath(), "upload_test_" + Guid.NewGuid().ToString("N"));
      var sourceStorageDir = Path.Combine(testDir, "src");
      var targetStorageDir = Path.Combine(testDir, "dest");
      try
      {
        var createdFiles = new List<(string path, long size)>();
        using (var sourceStorage = new FileSystemStorage(sourceStorageDir))
        {
          var newCommand = new NewCommand(new DummyLogger(), sourceStorage, StorageFormat.Normal);
          await newCommand.ExecuteAsync();
          
          DateTime baseTime = DateTime.Now;
          for (int i = 0; i < 10; i++)
          {
            var file = CommandTestUtil.CreateExeFile(baseTime.AddMinutes(i), (byte)i);
            var fileStream = new MemoryStream(file, false);
            var path = CommandTestUtil.GetPePathInStorage($"minexe_{i % 5}.exe", fileStream);
            fileStream.Seek(0, SeekOrigin.Begin);
            await sourceStorage.CreateForWritingAsync(path, AccessMode.Public, fileStream);
            await CommandTestUtil.WriteTag(sourceStorage, "minexe", $"v1.0.{i}", [Path.GetDirectoryName(path)!]);
            createdFiles.Add((path, file.Length));
          }
          
          for (int i = 0; i < 5; i++)
          {
            var file = CommandTestUtil.CreatePortablePdbFile(Guid.NewGuid(), (char)((byte)'a' + i));
            var fileStream = new MemoryStream(file, false);
            var path = CommandTestUtil.GetPortablePdbPathInStorage($"minpdb_{i}.pdb", fileStream);
            fileStream.Seek(0, SeekOrigin.Begin);
            await sourceStorage.CreateForWritingAsync(path, AccessMode.Public, fileStream);
            await CommandTestUtil.WriteTag(sourceStorage, "minpdb", $"v1.1.{i}", [Path.GetDirectoryName(path)!]);
            createdFiles.Add((path, file.Length));
          }
        }

        using (var targetStorage = new FileSystemStorage(targetStorageDir))
        {
          var command = new UploadCommand(new DummyLogger(), targetStorage, Environment.ProcessorCount, sourceStorageDir, StorageFormat.Normal, 
            CollisionResolutionMode.Terminate, CollisionResolutionMode.Terminate, null);

          var commandResult = await command.ExecuteAsync();
          Assert.AreEqual(0, commandResult);

          foreach (var (createdFile, size) in createdFiles)
          {
            Assert.IsTrue(await targetStorage.ExistsAsync(createdFile));
            Assert.AreEqual(size, await targetStorage.GetLengthAsync(createdFile));
          }
        }
      }
      finally
      {
        Directory.Delete(testDir, true);
      }
    }
    
    [TestMethod]
    public async Task UploadCommandPeCollisionTest()
    {
      var testDir = Path.Combine(Path.GetTempPath(), "upload_test_" + Guid.NewGuid().ToString("N"));
      var sourceStorage1Dir = Path.Combine(testDir, "src1");
      var sourceStorage2Dir = Path.Combine(testDir, "src2");
      var backupStorageDir = Path.Combine(testDir, "backup");
      var destStorageDir = Path.Combine(testDir, "dest");
      
      try
      {
        string pePathInStorage;
        byte[] origPeFile;
        byte[] changedPeFile;
        DateTime baseTime = DateTime.Now;

        using (var sourceStorage1 = new FileSystemStorage(sourceStorage1Dir))
        {
          var newCommand = new NewCommand(new DummyLogger(), sourceStorage1, StorageFormat.Normal);
          await newCommand.ExecuteAsync();
          
          var file = CommandTestUtil.CreateExeFile(baseTime, 100);
          var fileStream = new MemoryStream(file, false);
          var path = CommandTestUtil.GetPePathInStorage($"minexe.exe", fileStream);
          pePathInStorage = path;
          fileStream.Seek(0, SeekOrigin.Begin);
          await sourceStorage1.CreateForWritingAsync(path, AccessMode.Public, fileStream);
          await CommandTestUtil.WriteTag(sourceStorage1, "minexe", $"v1.0.0", [Path.GetDirectoryName(path)!]);
          origPeFile = file;
        }
        
        using (var sourceStorage2 = new FileSystemStorage(sourceStorage2Dir))
        {
          var newCommand = new NewCommand(new DummyLogger(), sourceStorage2, StorageFormat.Normal);
          await newCommand.ExecuteAsync();
          
          var file = CommandTestUtil.CreateExeFile(baseTime, 101);
          var fileStream = new MemoryStream(file, false);
          var path = CommandTestUtil.GetPePathInStorage($"minexe.exe", fileStream);
          Assert.AreEqual(pePathInStorage, path);
          fileStream.Seek(0, SeekOrigin.Begin);
          await sourceStorage2.CreateForWritingAsync(path, AccessMode.Public, fileStream);
          await CommandTestUtil.WriteTag(sourceStorage2, "minexe", $"v1.0.1", [Path.GetDirectoryName(path)!]);
          changedPeFile = file;
        }
        
        using (var targetStorage = new FileSystemStorage(destStorageDir))
        {
          var command = new UploadCommand(new DummyLogger(), targetStorage, Environment.ProcessorCount, sourceStorage1Dir, StorageFormat.Normal, 
            collisionResolutionMode: CollisionResolutionMode.Terminate, peCollisionResolutionMode: CollisionResolutionMode.Terminate,  backupStorageDir: null);

          var commandResult = await command.ExecuteAsync();
          Assert.AreEqual(0, commandResult);

          var fileInStorage = await CommandTestUtil.LoadFileFromStorage(targetStorage, pePathInStorage);
          Assert.IsTrue(origPeFile.SequenceEqual(fileInStorage));
        }
        
        
        // Test CollisionResolutionMode.Terminate
        using (var targetStorage = new FileSystemStorage(destStorageDir))
        {
          var command = new UploadCommand(new DummyLogger(), targetStorage, Environment.ProcessorCount, sourceStorage2Dir, StorageFormat.Normal, 
            collisionResolutionMode: CollisionResolutionMode.Terminate, peCollisionResolutionMode: CollisionResolutionMode.Terminate,  backupStorageDir: null);

          var commandResult = await command.ExecuteAsync();
          Assert.AreNotEqual(0, commandResult);

          var fileInStorage = await CommandTestUtil.LoadFileFromStorage(targetStorage, pePathInStorage);
          Assert.IsTrue(origPeFile.SequenceEqual(fileInStorage));
        }
        
        // Test CollisionResolutionMode.KeepExisted mode
        using (var targetStorage = new FileSystemStorage(destStorageDir))
        {
          var command = new UploadCommand(new DummyLogger(), targetStorage, Environment.ProcessorCount, sourceStorage2Dir, StorageFormat.Normal, 
            collisionResolutionMode: CollisionResolutionMode.Terminate, peCollisionResolutionMode: CollisionResolutionMode.KeepExisted,  backupStorageDir: null);

          var commandResult = await command.ExecuteAsync();
          Assert.AreEqual(0, commandResult);

          var fileInStorage = await CommandTestUtil.LoadFileFromStorage(targetStorage, pePathInStorage);
          Assert.IsTrue(origPeFile.SequenceEqual(fileInStorage));
        }
        
        // Test CollisionResolutionMode.Overwrite mode
        using (var targetStorage = new FileSystemStorage(destStorageDir))
        {
          var command = new UploadCommand(new DummyLogger(), targetStorage, Environment.ProcessorCount, sourceStorage2Dir, StorageFormat.Normal, 
            collisionResolutionMode: CollisionResolutionMode.Terminate, peCollisionResolutionMode: CollisionResolutionMode.Overwrite,  backupStorageDir: backupStorageDir);

          var commandResult = await command.ExecuteAsync();
          Assert.AreEqual(0, commandResult);

          var fileInStorage = await CommandTestUtil.LoadFileFromStorage(targetStorage, pePathInStorage);
          Assert.IsTrue(changedPeFile.SequenceEqual(fileInStorage));

          using (var backupStorage = new FileSystemStorage(backupStorageDir))
          {
            var fileInBackupStorage = await CommandTestUtil.LoadFileFromStorage(backupStorage, pePathInStorage);
            Assert.IsTrue(origPeFile.SequenceEqual(fileInBackupStorage)); 
          }
        }
        
        
        // Recreate storage
        Directory.Delete(destStorageDir, true);
        using (var targetStorage = new FileSystemStorage(destStorageDir))
        {
          var command = new UploadCommand(new DummyLogger(), targetStorage, Environment.ProcessorCount, sourceStorage1Dir, StorageFormat.Normal, 
            collisionResolutionMode: CollisionResolutionMode.Terminate, peCollisionResolutionMode: CollisionResolutionMode.Terminate,  backupStorageDir: null);

          var commandResult = await command.ExecuteAsync();
          Assert.AreEqual(0, commandResult);

          var fileInStorage = await CommandTestUtil.LoadFileFromStorage(targetStorage, pePathInStorage);
          Assert.IsTrue(origPeFile.SequenceEqual(fileInStorage));
        }
        
                
        // Test CollisionResolutionMode.OverwriteWithoutBackup mode
        using (var targetStorage = new FileSystemStorage(destStorageDir))
        {
          var command = new UploadCommand(new DummyLogger(), targetStorage, Environment.ProcessorCount, sourceStorage2Dir, StorageFormat.Normal, 
            collisionResolutionMode: CollisionResolutionMode.Terminate, peCollisionResolutionMode: CollisionResolutionMode.OverwriteWithoutBackup,  backupStorageDir: null);

          var commandResult = await command.ExecuteAsync();
          Assert.AreEqual(0, commandResult);

          var fileInStorage = await CommandTestUtil.LoadFileFromStorage(targetStorage, pePathInStorage);
          Assert.IsTrue(changedPeFile.SequenceEqual(fileInStorage));
        }
      }
      finally
      {
        Directory.Delete(testDir, true);
      }
    }
  }
}