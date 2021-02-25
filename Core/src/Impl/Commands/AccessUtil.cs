using System;
using Amazon;
using JetBrains.Annotations;
using JetBrains.SymbolStorage.Impl.Storages;

namespace JetBrains.SymbolStorage.Impl.Commands
{
  internal static class AccessUtil
  {
    public const string AwsS3AccessKeyEnvironmentVariable = "JETBRAINS_AWSS3_ACCESS_KEY";
    public const string AwsS3SecretKeyEnvironmentVariable = "JETBRAINS_AWSS3_SECRET_KEY";
    public const string AwsCloudFrontDistributionIdEnvironmentVariable = "JETBRAINS_AWSCF_DISTRIBUTION_ID";

    public const string NormalStorageFormat = "normal";
    public const string LowerStorageFormat = "lower";
    public const string UpperStorageFormat = "upper";

    public static readonly string DefaultAwsS3RegionEndpoint = RegionEndpoint.EUWest1.SystemName;

    [NotNull]
    public static IStorage GetStorage([CanBeNull] string dir, [CanBeNull] string awsS3BucketName, [CanBeNull] string awsS3RegionEndpoint)
    {
      if (!string.IsNullOrEmpty(dir) && string.IsNullOrEmpty(awsS3BucketName))
        return GetFileSystemStorage(dir);
      if (string.IsNullOrEmpty(dir) && !string.IsNullOrEmpty(awsS3BucketName))
        return GetAwsS3Storage(awsS3BucketName, awsS3RegionEndpoint ?? DefaultAwsS3RegionEndpoint);
      throw new Exception("The storage location option should be defined");
    }

    [NotNull]
    private static IStorage GetFileSystemStorage([NotNull] string dir)
    {
      return new FileSystemStorage(dir);
    }

    [NotNull]
    private static IStorage GetAwsS3Storage([NotNull] string awsS3BucketName, [NotNull] string awsS3RegionEndpoint)
    {
      var accessKey = Environment.GetEnvironmentVariable(AwsS3AccessKeyEnvironmentVariable) ?? ConsoleUtil.ReadHiddenConsoleInput("Enter AWS S3 access key");
      var secretKey = Environment.GetEnvironmentVariable(AwsS3SecretKeyEnvironmentVariable) ?? ConsoleUtil.ReadHiddenConsoleInput("Enter AWS S3 secret key");
      var cloudFrontDistributionId = Environment.GetEnvironmentVariable(AwsCloudFrontDistributionIdEnvironmentVariable).ConvertSpecialEmptyValue() ?? ConsoleUtil.ReadHiddenConsoleInput("Enter AWS Cloud Front distribution identifier");
      return new AwsS3Storage(accessKey, secretKey, awsS3BucketName, awsS3RegionEndpoint, cloudFrontDistributionId);
    }

    [CanBeNull]
    private static string ConvertSpecialEmptyValue([CanBeNull] this string env)
    {
      // Bug: cmd.exe doesn't support empty environment variables!!! So, use `_` for empty value...
      return env == "_" ? "" : env;
    }

    public static StorageFormat GetStorageFormat([CanBeNull] string casing) => casing switch
      {
        null => StorageFormat.Normal,
        NormalStorageFormat => StorageFormat.Normal,
        LowerStorageFormat => StorageFormat.LowerCase,
        UpperStorageFormat => StorageFormat.UpperCase,
        _ => throw new ArgumentOutOfRangeException(nameof(casing), casing, null)
      };
  }
}