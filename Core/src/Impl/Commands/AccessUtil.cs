#nullable enable

using System;
using Amazon;
using JetBrains.SymbolStorage.Impl.Storages;

namespace JetBrains.SymbolStorage.Impl.Commands
{
  internal static class AccessUtil
  {
    public const string AwsS3AccessKeyEnvironmentVariable = "JETBRAINS_AWSS3_ACCESS_KEY";
    public const string AwsS3SecretKeyEnvironmentVariable = "JETBRAINS_AWSS3_SECRET_KEY";
    public const string AwsS3SessionTokenEnvironmentVariable = "JETBRAINS_AWSS3_SESSION_TOKEN";
    public const string AwsCloudFrontDistributionIdEnvironmentVariable = "JETBRAINS_AWSCF_DISTRIBUTION_ID";

    public const string NormalStorageFormat = "normal";
    public const string LowerStorageFormat = "lower";
    public const string UpperStorageFormat = "upper";

    public static readonly string DefaultAwsS3RegionEndpoint = RegionEndpoint.EUWest1.SystemName;
    public static readonly int DefaultDegreeOfParallelism = Environment.ProcessorCount;

    public static readonly TimeSpan DefaultSafetyPeriod = TimeSpan.FromDays(60);

    public const string ProtectedAll = "all";
    public const string ProtectedOn = "protected";
    public const string ProtectedOff = "unprotected";
    
    public const string CollisionResolutionTerminate = "terminate";
    public const string CollisionResolutionKeepExisted = "keepexisted";
    public const string CollisionResolutionOverwrite = "overwrite";
    public const string CollisionResolutionOverwriteWithoutBackup = "overwritewithoutbackup";
    

    public static bool? GetProtectedValue(string value) => value switch
      {
        ProtectedAll => null,
        ProtectedOn => true,
        ProtectedOff => false,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
      };
    
    public static int GetDegreeOfParallelism(string? degreeOfParallelism)
    {
      var res = degreeOfParallelism != null
        ? int.Parse(degreeOfParallelism)
        : DefaultDegreeOfParallelism;
      if (res < 1)
        throw new ArgumentException("The execute task count can't be less then 1");
      Console.WriteLine("Execute {0} tasks in parallel", res);
      return res;
    }
    
    public static IStorage GetStorage(string? dir, string? awsS3BucketName, string? awsS3RegionEndpoint)
    {
      if (!string.IsNullOrEmpty(dir) && string.IsNullOrEmpty(awsS3BucketName))
        return GetFileSystemStorage(dir);
      if (string.IsNullOrEmpty(dir) && !string.IsNullOrEmpty(awsS3BucketName))
        return GetAwsS3Storage(awsS3BucketName, awsS3RegionEndpoint ?? DefaultAwsS3RegionEndpoint);
      throw new Exception("The storage location option should be defined");
    }
    
    private static IStorage GetFileSystemStorage(string dir)
    {
      return new FileSystemStorage(dir);
    }
    
    private static IStorage GetAwsS3Storage(string awsS3BucketName, string awsS3RegionEndpoint)
    {
      var accessKey = Environment.GetEnvironmentVariable(AwsS3AccessKeyEnvironmentVariable) ?? ConsoleUtil.ReadHiddenConsoleInput("Enter AWS S3 access key");
      var secretKey = Environment.GetEnvironmentVariable(AwsS3SecretKeyEnvironmentVariable) ?? ConsoleUtil.ReadHiddenConsoleInput("Enter AWS S3 secret key");
      var sessionToken = Environment.GetEnvironmentVariable(AwsS3SessionTokenEnvironmentVariable).ConvertSpecialEmptyValue() ?? ConsoleUtil.ReadHiddenConsoleInput("Enter AWS S3 session token (optional)");
      var cloudFrontDistributionId = Environment.GetEnvironmentVariable(AwsCloudFrontDistributionIdEnvironmentVariable).ConvertSpecialEmptyValue() ?? ConsoleUtil.ReadHiddenConsoleInput("Enter AWS Cloud Front distribution identifier (optional)");
      return new AwsS3Storage(
        accessKey, 
        secretKey, 
        sessionToken: sessionToken, 
        bucketName: awsS3BucketName, 
        region: awsS3RegionEndpoint, 
        cloudFrontDistributionId: cloudFrontDistributionId);
    }

    private static string? ConvertSpecialEmptyValue(this string? env)
    {
      // Bug: cmd.exe doesn't support empty environment variables!!! So, use `_` for empty value...
      return env == "_" ? "" : env;
    }

    public static StorageFormat GetStorageFormat(string? casing) => casing switch
      {
        null => StorageFormat.Normal,
        NormalStorageFormat => StorageFormat.Normal,
        LowerStorageFormat => StorageFormat.LowerCase,
        UpperStorageFormat => StorageFormat.UpperCase,
        _ => throw new ArgumentOutOfRangeException(nameof(casing), casing, null)
      };

    public static CollisionResolutionMode GetCollisionResolutionMode(string? value, CollisionResolutionMode defaultMode = CollisionResolutionMode.Terminate) => value?.ToLowerInvariant() switch
    {
      null => defaultMode,
      CollisionResolutionTerminate => CollisionResolutionMode.Terminate,
      CollisionResolutionKeepExisted => CollisionResolutionMode.KeepExisted,
      CollisionResolutionOverwrite => CollisionResolutionMode.Overwrite,
      CollisionResolutionOverwriteWithoutBackup => CollisionResolutionMode.OverwriteWithoutBackup,
      _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown collision resolution mode")
    };
  }
}