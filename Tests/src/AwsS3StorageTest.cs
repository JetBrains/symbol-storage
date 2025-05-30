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
      var recordName = $"test_path{Path.PathSeparator}file_{Guid.NewGuid():N}.txt";
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
      var recordName = $"test_path{Path.PathSeparator}file_{Guid.NewGuid():N}.txt";
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
  }
}