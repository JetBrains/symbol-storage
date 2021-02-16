using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Amazon;
using Amazon.CloudFront;
using Amazon.CloudFront.Model;
using Amazon.S3;
using Amazon.S3.Model;
using JetBrains.Annotations;

namespace JetBrains.SymbolStorage.Impl.Storages
{
  internal sealed class AwsS3Storage : IStorage
  {
    private const string AwsS3GroupUriAllUsers = "http://acs.amazonaws.com/groups/global/AllUsers";
    private readonly string myBucketName;
    private readonly string myCloudFrontDistributionId;
    private readonly IAmazonS3 myS3Client;
    private readonly IAmazonCloudFront myCloudFrontClient;

    public AwsS3Storage(
      [NotNull] string accessKey,
      [NotNull] string secretKey,
      [NotNull] string bucketName,
      [CanBeNull] string cloudFrontDistributionId = null,
      [CanBeNull] string region = null)
    {
      var regionEndpoint = region != null ? RegionEndpoint.GetBySystemName(region) : RegionEndpoint.EUWest1;
      myS3Client = new AmazonS3Client(accessKey, secretKey, regionEndpoint);
      myCloudFrontClient = new AmazonCloudFrontClient(accessKey, secretKey, regionEndpoint);
      myBucketName = bucketName ?? throw new ArgumentNullException(nameof(bucketName));
      myCloudFrontDistributionId = cloudFrontDistributionId;
    }

    public async Task<bool> Exists(string file)
    {
      if (string.IsNullOrEmpty(file))
        throw new ArgumentNullException(nameof(file));
      try
      {
        await myS3Client.GetObjectMetadataAsync(new GetObjectMetadataRequest
          {
            BucketName = myBucketName,
            Key = file.NormalizeLinux()
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

    public Task Delete(string file)
    {
      if (string.IsNullOrEmpty(file))
        throw new ArgumentNullException(nameof(file));
      return myS3Client.DeleteObjectAsync(new DeleteObjectRequest
        {
          BucketName = myBucketName,
          Key = file.NormalizeLinux()
        });
    }

    public async Task Rename(string file, string newFile, AccessMode mode)
    {
      if (string.IsNullOrEmpty(file))
        throw new ArgumentNullException(nameof(file));
      if (string.IsNullOrEmpty(newFile))
        throw new ArgumentNullException(nameof(newFile));
      var srcFile = file.NormalizeLinux();
      await myS3Client.CopyObjectAsync(new CopyObjectRequest
        {
          SourceBucket = myBucketName,
          SourceKey = srcFile,
          DestinationBucket = myBucketName,
          DestinationKey = newFile.NormalizeLinux(),
          CannedACL = GetS3CannedAcl(mode)
        });
      await myS3Client.DeleteObjectAsync(new DeleteObjectRequest
        {
          BucketName = myBucketName,
          Key = srcFile
        });
    }

    public async Task<long> GetLength(string file)
    {
      if (string.IsNullOrEmpty(file))
        throw new ArgumentNullException(nameof(file));
      var response = await myS3Client.GetObjectMetadataAsync(new GetObjectMetadataRequest
        {
          BucketName = myBucketName,
          Key = file.NormalizeLinux()
        });
      return response.ContentLength;
    }

    public bool SupportAccessMode => true;

    public async Task<AccessMode> GetAccessMode(string file)
    {
      if (string.IsNullOrEmpty(file))
        throw new ArgumentNullException(nameof(file));
      var respond = await myS3Client.GetACLAsync(new GetACLRequest
        {
          BucketName = myBucketName,
          Key = file.NormalizeLinux()
        });

      var hasUnknown = false;
      var hasReadPublic = false;
      var hasFullControlOwner = false;
      var ownerId = respond.AccessControlList.Owner.Id;
      foreach (var grant in respond.AccessControlList.Grants)
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

    public async Task SetAccessMode(string file, AccessMode mode)
    {
      if (string.IsNullOrEmpty(file))
        throw new ArgumentNullException(nameof(file));
      await myS3Client.PutACLAsync(new PutACLRequest
        {
          BucketName = myBucketName,
          Key = file.NormalizeLinux(),
          CannedACL = GetS3CannedAcl(mode)
        });
    }

    public async Task<TResult> OpenForReading<TResult>(string file, Func<Stream, TResult> func)
    {
      if (string.IsNullOrEmpty(file))
        throw new ArgumentNullException(nameof(file));
      if (func == null)
        throw new ArgumentNullException(nameof(func));
      using var response = await myS3Client.GetObjectAsync(new GetObjectRequest
        {
          BucketName = myBucketName,
          Key = file.NormalizeLinux()
        });
      return func(response.ResponseStream);
    }

    public Task OpenForReading(string file, Action<Stream> action)
    {
      return OpenForReading<object>(file,
        x =>
          {
            action(x);
            return null;
          });
    }

    public async Task CreateForWriting(string file, AccessMode mode, long length, Stream stream)
    {
      if (string.IsNullOrEmpty(file))
        throw new ArgumentNullException(nameof(file));
      if (stream == null)
        throw new ArgumentNullException(nameof(stream));
      if (stream.CanSeek)
      {
        if (stream.Length - stream.Position != length)
          throw new ArgumentException(nameof(length));
      }

      await myS3Client.PutObjectAsync(new PutObjectRequest
        {
          BucketName = myBucketName,
          Key = file.NormalizeLinux(),
          InputStream = stream,
          AutoCloseStream = false,
          AutoResetStreamPosition = false,
          Headers =
            {
              ContentLength = length
            },
          CannedACL = GetS3CannedAcl(mode)
        });
    }

    public async Task<bool> IsEmpty()
    {
      var response = await myS3Client.ListObjectsAsync(new ListObjectsRequest
        {
          BucketName = myBucketName,
          MaxKeys = 2
        });
      return !response.S3Objects.Where(IsNotDataJsonFile).Any();
    }

    public async IAsyncEnumerable<ChildrenItem> GetChildren(ChildrenMode mode, string prefixDir)
    {
      for (var request = new ListObjectsRequest
        {
          BucketName = myBucketName,
          Prefix = string.IsNullOrEmpty(prefixDir) ? null : prefixDir.NormalizeLinux() + '/'
        };;)
      {
        var response = await myS3Client.ListObjectsAsync(request);
        foreach (var s3Object in response.S3Objects.Where(IsUserFile))
        {
          yield return new ChildrenItem
            {
              Name = s3Object.Key.NormalizeSystem(),
              Size = (mode & ChildrenMode.WithSize) != 0 ? s3Object.Size : -1
            };
        }

        request.Marker = response.NextMarker;
        if (!response.IsTruncated)
          break;
      }
    }
    
    public async Task InvalidateExternalServices(IEnumerable<string> keys)
    {
      if (!string.IsNullOrEmpty(myCloudFrontDistributionId))
      {
        var items = keys != null 
          ? keys.Select(x => '/' + x.NormalizeLinux()).ToList()
          : new List<string> {"/*"};
        if (items.Count > 0)
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

    [NotNull]
    private static S3CannedACL GetS3CannedAcl(AccessMode mode)
    {
      return mode switch
        {
          AccessMode.Private => S3CannedACL.Private,
          AccessMode.Public => S3CannedACL.PublicRead,
          _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };
    }

    private static bool IsNotDataJsonFile([NotNull] S3Object s3Object) => s3Object.Key != ".data.json";
    private static bool IsDirectory([NotNull] S3Object s3Object) => s3Object.Key.EndsWith("/") && s3Object.Size == 0;
    private static bool IsUserFile([NotNull] S3Object s3Object) => !IsDirectory(s3Object) && IsNotDataJsonFile(s3Object);
  }
}