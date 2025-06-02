using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.SymbolStorage.Impl.Storages;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JetBrains.SymbolStorage.Tests
{
  [TestClass]
  public class AwsS3StorageTest
  {
    private const string AwsS3AccessKeyEnvironmentVariable = "JETBRAINS_AWSS3_ACCESS_KEY";
    private const string AwsS3SecretKeyEnvironmentVariable = "JETBRAINS_AWSS3_SECRET_KEY";
    private const string AwsS3BucketNameEnvironmentVariable = "JETBRAINS_AWSS3_BUCKET_NAME";
    private const string AwsS3RegionEnvironmentVariable = "JETBRAINS_AWSS3_REGION";
    private const string AwsCloudFrontDistributionIdEnvironmentVariable = "JETBRAINS_AWSCF_DISTRIBUTION_ID";
    
    private static readonly byte[] OurTestData = Enumerable.Range(0, 1024).Select(i => (byte)(i % 256)).ToArray();
    
    private static AwsS3Storage CreateAwsStorageClient()
    {
      var accessKey = Environment.GetEnvironmentVariable(AwsS3AccessKeyEnvironmentVariable);
      var secretKey = Environment.GetEnvironmentVariable(AwsS3SecretKeyEnvironmentVariable);
      var bucketName = Environment.GetEnvironmentVariable(AwsS3BucketNameEnvironmentVariable);
      var region = Environment.GetEnvironmentVariable(AwsS3RegionEnvironmentVariable);
      var cloudFrontDistributionId = Environment.GetEnvironmentVariable(AwsCloudFrontDistributionIdEnvironmentVariable);
      
      if (string.IsNullOrEmpty(accessKey) || string.IsNullOrEmpty(secretKey) || string.IsNullOrEmpty(bucketName) || string.IsNullOrEmpty(region))
        Assert.Inconclusive("AWS Storage Environment Variables not provided");
      if (string.IsNullOrEmpty(cloudFrontDistributionId))
        cloudFrontDistributionId = null;
      
      return new AwsS3Storage(accessKey: accessKey,
        secretKey: secretKey,
        bucketName: bucketName,
        region: region,
        cloudFrontDistributionId: cloudFrontDistributionId,
        supportAcl: true);
    }
    
    [TestMethod]
    public async Task PutDataToStorageTest()
    {
      using var client = CreateAwsStorageClient();
      var recordName = $"test_path{Path.DirectorySeparatorChar}file_{Guid.NewGuid():N}.txt";
      try
      {
        await client.CreateForWritingAsync(recordName, AccessMode.Public, new MemoryStream(OurTestData, false));
      }
      finally
      {
        await client.DeleteAsync(recordName);
      }
    }
    
    [TestMethod]
    public async Task GetDataFromStorageTest()
    {
      using var client = CreateAwsStorageClient();
      var recordName = $"test_path{Path.DirectorySeparatorChar}file_{Guid.NewGuid():N}.txt";
      try
      {
        await client.CreateForWritingAsync(recordName, AccessMode.Public, new MemoryStream(OurTestData, false));

        var memoryStream = new MemoryStream();
        await client.OpenForReadingAsync(recordName, async stream =>
        {
          await stream.CopyToAsync(memoryStream);
        });
        
        Assert.IsTrue(OurTestData.SequenceEqual(memoryStream.ToArray()));
      }
      finally
      {
        await client.DeleteAsync(recordName);
      }
    }
    
    [TestMethod]
    public async Task GetDataLengthFromStorageTest()
    {
      using var client = CreateAwsStorageClient();
      var recordName = $"test_path{Path.DirectorySeparatorChar}file_{Guid.NewGuid():N}.txt";
      try
      {
        await client.CreateForWritingAsync(recordName, AccessMode.Public, new MemoryStream(OurTestData, false));

        Assert.AreEqual(OurTestData.Length, await client.GetLengthAsync(recordName));
      }
      finally
      {
        await client.DeleteAsync(recordName);
      }

      await Assert.ThrowsAsync<Exception>(async () =>
      {
        _ = await client.GetLengthAsync(recordName);
      });
    }
    
    [TestMethod]
    public async Task ExistsInStorageTest()
    {
      using var client = CreateAwsStorageClient();
      var recordName = $"test_path{Path.DirectorySeparatorChar}file_{Guid.NewGuid():N}.txt";
      try
      {
        Assert.IsFalse(await client.ExistsAsync(recordName));
        
        await client.CreateForWritingAsync(recordName, AccessMode.Public, new MemoryStream(OurTestData, false));

        Assert.IsTrue(await client.ExistsAsync(recordName));
      }
      finally
      {
        await client.DeleteAsync(recordName);
        Assert.IsFalse(await client.ExistsAsync(recordName));
      }
    }
    
    [TestMethod] public async Task DeleteDataFromStorageTest()
    {
      using var client = CreateAwsStorageClient();
      var recordName = $"test_path{Path.DirectorySeparatorChar}file_{Guid.NewGuid():N}.txt";
      try
      {
        Assert.IsFalse(await client.ExistsAsync(recordName));
        await client.DeleteAsync(recordName); // Expecting that it is fine to delete non-existed record
        
        await client.CreateForWritingAsync(recordName, AccessMode.Public, new MemoryStream(OurTestData, false));
        Assert.IsTrue(await client.ExistsAsync(recordName));
        
        await client.DeleteAsync(recordName);
        Assert.IsFalse(await client.ExistsAsync(recordName));
      }
      finally
      {
        await client.DeleteAsync(recordName);
      }
    }
    
    [TestMethod]
    public async Task RenameInStorageTest()
    {
      using var client = CreateAwsStorageClient();
      var recordName = $"test_path{Path.DirectorySeparatorChar}file_{Guid.NewGuid():N}.txt";
      var renamedRecordName = $"test_path{Path.DirectorySeparatorChar}file_{Guid.NewGuid():N}.txt";
      try
      {
        Assert.IsFalse(await client.ExistsAsync(recordName));
        Assert.IsFalse(await client.ExistsAsync(renamedRecordName));
        
        await client.CreateForWritingAsync(recordName, AccessMode.Public, new MemoryStream(OurTestData, false));

        Assert.IsTrue(await client.ExistsAsync(recordName));
        Assert.IsFalse(await client.ExistsAsync(renamedRecordName));

        await client.RenameAsync(recordName, renamedRecordName, AccessMode.Public);
        
        Assert.IsFalse(await client.ExistsAsync(recordName));
        Assert.IsTrue(await client.ExistsAsync(renamedRecordName));
        
        var memoryStream = new MemoryStream();
        await client.OpenForReadingAsync(renamedRecordName, async stream =>
        {
          await stream.CopyToAsync(memoryStream);
        });
        
        Assert.IsTrue(OurTestData.SequenceEqual(memoryStream.ToArray()));
      }
      finally
      {
        await client.DeleteAsync(recordName);
        await client.DeleteAsync(renamedRecordName);
      }
    }
    
    [TestMethod]
    public async Task AccessModeOperationsTest()
    {
      using var client = CreateAwsStorageClient();
      var recordName = $"test_path{Path.DirectorySeparatorChar}file_{Guid.NewGuid():N}.txt";
      try
      {
        await client.CreateForWritingAsync(recordName, AccessMode.Public, new MemoryStream(OurTestData, false));

        var mode = await client.GetAccessModeAsync(recordName);
        Assert.AreEqual(AccessMode.Public, mode);

        await client.SetAccessModeAsync(recordName, AccessMode.Private);
        
        mode = await client.GetAccessModeAsync(recordName);
        Assert.AreEqual(AccessMode.Private, mode);
      }
      finally
      {
        await client.DeleteAsync(recordName);
      }
    }
    
    [TestMethod]
    public async Task IsEmptyBucketTest()
    {
      using var client = CreateAwsStorageClient();
      var recordName = $"test_path_{Guid.NewGuid():N}{Path.DirectorySeparatorChar}file_{Guid.NewGuid():N}.txt";
      try
      {
        await client.CreateForWritingAsync(recordName, AccessMode.Public, new MemoryStream(OurTestData, false));

        // Because S3 bucket is shared, it can be non-empty at the beginning of the test
        Assert.IsFalse(await client.IsEmptyAsync());
      }
      finally
      {
        await client.DeleteAsync(recordName);
      }
    }
    
    [TestMethod]
    public async Task GetChildrenTest()
    {
      using var client = CreateAwsStorageClient();
      var directoryName = $"test_path_{Guid.NewGuid():N}";
      var recordName = $"{directoryName}{Path.DirectorySeparatorChar}file_{Guid.NewGuid():N}.txt";
      var record2Name = $"{directoryName}{Path.DirectorySeparatorChar}file_{Guid.NewGuid():N}.txt";
      var recordInOtherDirName = $"{directoryName}_2{Path.DirectorySeparatorChar}file_{Guid.NewGuid():N}.txt";
      try
      {
        await client.CreateForWritingAsync(recordName, AccessMode.Public, new MemoryStream(OurTestData, false));
        await client.CreateForWritingAsync(record2Name, AccessMode.Public, new MemoryStream(OurTestData, false));
        await client.CreateForWritingAsync(recordInOtherDirName, AccessMode.Public, new MemoryStream(OurTestData, false));

        var files = await (client.GetChildrenAsync(ChildrenMode.WithSize, directoryName)).ToListAsync();
        Assert.AreEqual(2, files.Count);
        
        Assert.IsTrue(files.Any(f => f.Name == recordName));
        Assert.IsTrue(files.Any(f => f.Name == record2Name));
        Assert.IsTrue(files.All(f => f.Size == OurTestData.Length));
        
        
        files = await (client.GetChildrenAsync(ChildrenMode.WithSize, directoryName + "_2")).ToListAsync();
        Assert.AreEqual(1, files.Count);
        
        Assert.IsTrue(files.Any(f => f.Name == recordInOtherDirName));
        Assert.IsTrue(files.All(f => f.Size == OurTestData.Length));
        
        
        files = await (client.GetChildrenAsync(ChildrenMode.WithSize, directoryName + "_3")).ToListAsync();
        Assert.AreEqual(0, files.Count);
        
        
        var fullCount = await (client.GetChildrenAsync(ChildrenMode.WithSize, null)).CountAsync();
        Assert.IsTrue(fullCount >= 3); // Because S3 bucket is shared, it can be non-empty at the beginning of the test
      }
      finally
      {
        await client.DeleteAsync(recordName);
        await client.DeleteAsync(record2Name);
        await client.DeleteAsync(recordInOtherDirName);
      }
    }
    
    [TestMethod]
    public async Task InvalidateRecordsInCacheTest()
    {
      using var client = CreateAwsStorageClient();
      var directory = $"test_path_{Guid.NewGuid():N}";
      var recordName = $"directory{Path.DirectorySeparatorChar}file_{Guid.NewGuid():N}.txt";
      try
      {
        await client.CreateForWritingAsync(recordName, AccessMode.Public, new MemoryStream(OurTestData, false));

        await client.InvalidateExternalServicesAsync([directory + Path.DirectorySeparatorChar + "*"]);
      }
      finally
      {
        await client.DeleteAsync(recordName);
      }
    }
  }
}