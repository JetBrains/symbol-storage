using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Amazon;
using Amazon.CloudFront;
using Amazon.CloudFront.Model;
using Amazon.S3;
using Amazon.S3.Model;
using JetBrains.Annotations;
using ThirdParty.MD5;

namespace JetBrains.SymbolStorage.Impl.Storages
{
  internal sealed class AwsS3Storage : IStorage
  {
    private const string AwsS3GroupUriAllUsers = "http://acs.amazonaws.com/groups/global/AllUsers";
    private readonly string myBucketName;
    private readonly string myCloudFrontDistributionId;
    private readonly AmazonS3Client myS3Client;
    private readonly AmazonCloudFrontClient myCloudFrontClient;
    private readonly bool mySupportAcl;

    public AwsS3Storage(
      [NotNull] string accessKey,
      [NotNull] string secretKey,
      [NotNull] string bucketName,
      [NotNull] string region,
      [CanBeNull] string cloudFrontDistributionId = null,
      bool supportAcl = true)
    {
      var regionEndpoint = RegionEndpoint.GetBySystemName(region);
      myS3Client = new AmazonS3Client(accessKey, secretKey, regionEndpoint);
      myCloudFrontClient = new AmazonCloudFrontClient(accessKey, secretKey, regionEndpoint);
      myBucketName = bucketName ?? throw new ArgumentNullException(nameof(bucketName));
      myCloudFrontDistributionId = cloudFrontDistributionId;
      mySupportAcl = supportAcl;
    }

    public async Task<bool> ExistsAsync(string file)
    {
      var key = file.CheckSystemFile().NormalizeLinux();
      try
      {
        await myS3Client.GetObjectMetadataAsync(new GetObjectMetadataRequest
          {
            BucketName = myBucketName,
            Key = key
          });
        return true;
      }
      catch (AmazonS3Exception e)
      {
        if (string.Equals(e.ErrorCode, "NotFound"))
          return false;
        throw;
      }
    }

    public async Task DeleteAsync(string file)
    {
      var key = file.CheckSystemFile().NormalizeLinux();
      await myS3Client.DeleteObjectAsync(new DeleteObjectRequest
        {
          BucketName = myBucketName,
          Key = key
        });
    }

    public async Task RenameAsync(string srcFile, string dstFile, AccessMode mode)
    {
      var srcKey = srcFile.CheckSystemFile().NormalizeLinux();
      var dstKey = dstFile.CheckSystemFile().NormalizeLinux();
      await myS3Client.CopyObjectAsync(new CopyObjectRequest
        {
          SourceBucket = myBucketName,
          SourceKey = srcKey,
          DestinationBucket = myBucketName,
          DestinationKey = dstKey,
          CannedACL = GetS3CannedAcl(mode, mySupportAcl)
        });
      await myS3Client.DeleteObjectAsync(new DeleteObjectRequest
        {
          BucketName = myBucketName,
          Key = srcKey
        });
    }

    public async Task<long> GetLengthAsync(string file)
    {
      var key = file.CheckSystemFile().NormalizeLinux();
      var response = await myS3Client.GetObjectMetadataAsync(new GetObjectMetadataRequest
        {
          BucketName = myBucketName,
          Key = key
        });
      return response.ContentLength;
    }

    public bool SupportAccessMode => mySupportAcl;

    public async Task<AccessMode> GetAccessModeAsync(string file)
    {
      var key = file.CheckSystemFile().NormalizeLinux();
      if (!mySupportAcl)
        return AccessMode.Unknown;
      var respond = await myS3Client.GetObjectAclAsync(new GetObjectAclRequest()
        {
          BucketName = myBucketName,
          Key = key
        });

      var hasUnknown = false;
      var hasReadPublic = false;
      var hasFullControlOwner = false;
      var ownerId = respond.Owner.Id;
      foreach (var grant in respond.Grants)
      {
        var grantee = grant.Grantee;
        if (grantee.Type == GranteeType.Group && grantee.URI == AwsS3GroupUriAllUsers && grant.Permission == S3Permission.READ)
          hasReadPublic = true;
        else if (grantee.Type == GranteeType.CanonicalUser && grantee.CanonicalUser == ownerId && grant.Permission == S3Permission.FULL_CONTROL)
          hasFullControlOwner = true;
        else
          hasUnknown = true;
      }

      if (hasUnknown || !hasFullControlOwner)
        return AccessMode.Unknown;
      return hasReadPublic ? AccessMode.Public : AccessMode.Private;
    }

    public async Task SetAccessModeAsync(string file, AccessMode mode)
    {
      var key = file.CheckSystemFile().NormalizeLinux();
      if (mySupportAcl)
      {
        await myS3Client.PutObjectAclAsync(new PutObjectAclRequest()
        {
          BucketName = myBucketName,
          Key = key,
          ACL = GetS3CannedAcl(mode, true)
        });
      }
    }

    public async Task<TResult> OpenForReadingAsync<TResult>(string file, Func<Stream, Task<TResult>> func)
    {
      if (func == null)
        throw new ArgumentNullException(nameof(func));
      var key = file.CheckSystemFile().NormalizeLinux();
      using var response = await myS3Client.GetObjectAsync(new GetObjectRequest
        {
          BucketName = myBucketName,
          Key = key
        });
      return await func(response.ResponseStream);
    }

    public Task OpenForReadingAsync(string file, Func<Stream, Task> func) => OpenForReadingAsync(file, async x =>
      {
        await func(x);
        return true;
      });

    public async Task CreateForWritingAsync(string file, AccessMode mode, Stream stream)
    {
      if (stream == null)
        throw new ArgumentNullException(nameof(stream));
      if (!stream.CanSeek)
        throw new ArgumentException("The stream should support the seek operation", nameof(stream));
      var key = file.CheckSystemFile().NormalizeLinux();
      await Task.Yield();
      
      stream.Seek(0, SeekOrigin.Begin);
      string md5Hash;
      using (var md5Alg = new MD5Managed())
      {
        md5Hash = Convert.ToBase64String(await md5Alg.ComputeHashAsync(stream));
      }
      
      stream.Seek(0, SeekOrigin.Begin);
      await myS3Client.PutObjectAsync(new PutObjectRequest
      {
        BucketName = myBucketName,
        Key = key,
        InputStream = stream,
        AutoCloseStream = false,
        Headers =
        {
          ContentLength = stream.Length,
          ContentMD5 = md5Hash
        },
        CannedACL = GetS3CannedAcl(mode, mySupportAcl)
      });
    }

    public async Task<bool> IsEmptyAsync()
    {
      var response = await myS3Client.ListObjectsV2Async(new ListObjectsV2Request()
      {
        BucketName = myBucketName,
        MaxKeys = 2,
      });
      
      // According to the tests, `ListObjectsV2Async` returns null in `response.S3Objects` when no keys found
      if (response.S3Objects == null)
        return true;
      
      return !response.S3Objects.Where(IsNotDataJsonFile).Any();
    }

    public async IAsyncEnumerable<ChildrenItem> GetChildrenAsync(ChildrenMode mode, string prefixDir)
    {
      var request = new ListObjectsV2Request()
      {
        BucketName = myBucketName,
        Prefix = string.IsNullOrEmpty(prefixDir) ? null : prefixDir.NormalizeLinux() + "/"
      };
      
      bool isCompleted = false;
      while (!isCompleted)
      {
        var response = await myS3Client.ListObjectsV2Async(request);
        // According to the tests, `ListObjectsV2Async` returns null in `response.S3Objects` when no keys found
        if (response.S3Objects == null)
          break;
        
        foreach (var s3Object in response.S3Objects.Where(IsUserFile))
        {
          yield return new ChildrenItem
          {
            Name = s3Object.Key.NormalizeSystem(),
            Size = (mode & ChildrenMode.WithSize) != 0 && s3Object.Size.HasValue ? s3Object.Size.Value : -1
          };
        }

        request.ContinuationToken = response.ContinuationToken;
        Debug.Assert(response.IsTruncated.HasValue, "Unexpected null value of IsTruncated property. It should always be specified");
        isCompleted = !(response.IsTruncated ?? false);
      }
    }

    public async Task InvalidateExternalServicesAsync(IEnumerable<string> fileMasks = null)
    {
      if (!string.IsNullOrEmpty(myCloudFrontDistributionId))
      {
        var items = fileMasks != null
          ? fileMasks.Select(x => "/" + x.CheckSystemFile().NormalizeLinux()).ToList()
          : new List<string> { "/*" };
        if (items.Count > 0)
        {
          await myCloudFrontClient.CreateInvalidationAsync(new CreateInvalidationRequest
          {
            DistributionId = myCloudFrontDistributionId,
            InvalidationBatch = new InvalidationBatch(new Paths
              {
                Items = items,
                Quantity = items.Count
              }, $"symbol-storage-{DateTime.UtcNow:s}")
          });
        }
      }
    }

    [CanBeNull]
    private static S3CannedACL GetS3CannedAcl(AccessMode mode, bool supportAcl) => mode switch
      {
        AccessMode.Private => supportAcl ? S3CannedACL.Private : null,
        AccessMode.Public => supportAcl ? S3CannedACL.PublicRead : null,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, $"Unknown {nameof(AccessMode)} value")
      };

    private static bool IsNotDataJsonFile([NotNull] S3Object s3Object) => s3Object.Key != ".data.json";
    private static bool IsDirectory([NotNull] S3Object s3Object) => s3Object.Key.EndsWith('/') && s3Object.Size == 0;
    private static bool IsUserFile([NotNull] S3Object s3Object) => !IsDirectory(s3Object) && IsNotDataJsonFile(s3Object);

    public void Dispose()
    {
      myS3Client.Dispose();
      myCloudFrontClient.Dispose();
    }
  }
}