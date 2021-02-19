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

    public Task Delete(string file)
    {
      var key = file.CheckSystemFile().NormalizeLinux();
      return myS3Client.DeleteObjectAsync(new DeleteObjectRequest
        {
          BucketName = myBucketName,
          Key = key
        });
    }

    public async Task Rename(string srcFile, string dstFile, AccessMode mode)
    {
      var srcKey = srcFile.CheckSystemFile().NormalizeLinux();
      var dstKey = dstFile.CheckSystemFile().NormalizeLinux();
      srcFile.CheckSystemFile();
      dstFile.CheckSystemFile();
      await myS3Client.CopyObjectAsync(new CopyObjectRequest
        {
          SourceBucket = myBucketName,
          SourceKey = srcKey,
          DestinationBucket = myBucketName,
          DestinationKey = dstKey,
          CannedACL = GetS3CannedAcl(mode)
        });
      await myS3Client.DeleteObjectAsync(new DeleteObjectRequest
        {
          BucketName = myBucketName,
          Key = srcKey
        });
    }

    public async Task<long> GetLength(string file)
    {
      var key = file.CheckSystemFile().NormalizeLinux();
      var response = await myS3Client.GetObjectMetadataAsync(new GetObjectMetadataRequest
        {
          BucketName = myBucketName,
          Key = key
        });
      return response.ContentLength;
    }

    public bool SupportAccessMode => true;

    public async Task<AccessMode> GetAccessMode(string file)
    {
      var key = file.CheckSystemFile().NormalizeLinux();
      var respond = await myS3Client.GetACLAsync(new GetACLRequest
        {
          BucketName = myBucketName,
          Key = key
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
      var key = file.CheckSystemFile().NormalizeLinux();
      await myS3Client.PutACLAsync(new PutACLRequest
        {
          BucketName = myBucketName,
          Key = key,
          CannedACL = GetS3CannedAcl(mode)
        });
    }

    public async Task<TResult> OpenForReading<TResult>(string file, Func<Stream, TResult> func)
    {
      if (func == null)
        throw new ArgumentNullException(nameof(func));
      var key = file.CheckSystemFile().NormalizeLinux();
      using var response = await myS3Client.GetObjectAsync(new GetObjectRequest
        {
          BucketName = myBucketName,
          Key = key
        });
      return func(response.ResponseStream);
    }

    public Task OpenForReading(string file, Action<Stream> action) => OpenForReading<object>(file,
        x =>
          {
            action(x);
            return null;
          });

    public async Task CreateForWriting(string file, AccessMode mode, long length, Stream stream)
    {
      if (stream == null)
        throw new ArgumentNullException(nameof(stream));
      if (stream.CanSeek)
      {
        if (stream.Length - stream.Position != length)
          throw new ArgumentException(nameof(length));
      }

      var key = file.CheckSystemFile().NormalizeLinux();
      await myS3Client.PutObjectAsync(new PutObjectRequest
        {
          BucketName = myBucketName,
          Key = key,
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

    public async Task InvalidateExternalServices(IEnumerable<string> fileMasks)
    {
      if (!string.IsNullOrEmpty(myCloudFrontDistributionId))
      {
        var items = fileMasks != null
          ? fileMasks.Select(x => '/' + x.CheckSystemFile().NormalizeLinux()).ToList()
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
    private static S3CannedACL GetS3CannedAcl(AccessMode mode) => mode switch
      {
        AccessMode.Private => S3CannedACL.Private,
        AccessMode.Public => S3CannedACL.PublicRead,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
      };

    private static bool IsNotDataJsonFile([NotNull] S3Object s3Object) => s3Object.Key != ".data.json";
    private static bool IsDirectory([NotNull] S3Object s3Object) => s3Object.Key.EndsWith("/") && s3Object.Size == 0;
    private static bool IsUserFile([NotNull] S3Object s3Object) => !IsDirectory(s3Object) && IsNotDataJsonFile(s3Object);
  }
}