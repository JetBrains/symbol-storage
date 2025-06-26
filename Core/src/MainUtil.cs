using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using JetBrains.Annotations;
using JetBrains.SymbolStorage.Impl;
using JetBrains.SymbolStorage.Impl.Commands;
using JetBrains.SymbolStorage.Impl.Logger;
using JetBrains.SymbolStorage.Impl.Storages;
using JetBrains.SymbolStorage.Impl.Tags;
using Microsoft.Extensions.CommandLineUtils;

namespace JetBrains.SymbolStorage
{
  public static class MainUtil
  {
    public enum MainMode
    {
      Full,
      UploadOnly
    }

    // Bug: Unix has one byte for exit code instead of Windows!!!
    //
    // Standard posix exit code meaning:
    //     1   - Catchall for general errors
    //     2   - Misuse of shell builtins (according to Bash documentation)
    //   126   - Command invoked cannot execute
    //   127   - “command not found”
    //   128   - Invalid argument to exit
    //   128+n - Fatal error signal “n”
    //   130   - Script terminated by Control-C
    //   255\* - Exit status out of range
    public static byte Main(Assembly mainAssembly, [Annotations.NotNull] string[] args, MainMode mode)
    {
      try
      {
        var assemblyName = mainAssembly.GetName();
        var toolName = assemblyName.Name;
        var toolVersion = assemblyName.Version!.ToString(3);
        var commandLine = new CommandLineApplication
          {
            FullName = toolName
          };
        commandLine.HelpOption("-h|--help");
        commandLine.VersionOption("--version", () => toolVersion);

        var dirOption = commandLine.Option("-d|--directory", "The local directory with symbol server storage.", CommandOptionType.SingleValue);
        var awsS3BucketNameOption = commandLine.Option("-a|--aws-s3", $"The AWS S3 bucket with symbol server storage. The access and private keys will be asked in console. Use {AccessUtil.AwsS3AccessKeyEnvironmentVariable}, {AccessUtil.AwsS3SecretKeyEnvironmentVariable}, {AccessUtil.AwsS3SessionTokenEnvironmentVariable} (optional, '_' = no value) and {AccessUtil.AwsCloudFrontDistributionIdEnvironmentVariable} (optional, '_' = no value) environment variables for unattended mode.", CommandOptionType.SingleValue);
        var awsS3RegionEndpointOption = commandLine.Option("-ar|--aws-s3-region", $"The AWS S3 region endpoint with symbol server storage. Default is {AccessUtil.DefaultAwsS3RegionEndpoint}.", CommandOptionType.SingleValue);
        var degreeOfParallelismOption = commandLine.Option("-t|--tasks", $"Execute task count in parallel. Default is the processor count ({AccessUtil.DefaultDegreeOfParallelism} for now).", CommandOptionType.SingleValue);
        var verboseOption = commandLine.Option("-v|--verbose", "Verbose mode.", CommandOptionType.NoValue);

        static void FilterOptions(
          CommandLineApplication x,
          out CommandOption incFilterProductOption,
          out CommandOption excFilterProductOption,
          out CommandOption incFilterVersionOption,
          out CommandOption excFilterVersionOption)
        {
          incFilterProductOption = x.Option("-fpi|--product-include-filter", "Select wildcard for include product filtering.", CommandOptionType.MultipleValue);
          excFilterProductOption = x.Option("-fpe|--product-exclude-filter", "Select wildcard for exclude product filtering.", CommandOptionType.MultipleValue);
          incFilterVersionOption = x.Option("-fvi|--version-include-filter", "Select wildcard for include version filtering.", CommandOptionType.MultipleValue);
          excFilterVersionOption = x.Option("-fve|--version-exclude-filter", "Select wildcard for exclude version filtering.", CommandOptionType.MultipleValue);
        }

        if (mode == MainMode.Full)
        {
          commandLine.Command("validate", x =>
            {
              x.HelpOption("-h|--help");
              x.Description = "Storage inconsistency check and fix known issues by request";
              var aclOption = x.Option("-r|--rights", "Validate access rights.", CommandOptionType.NoValue);
              var fixOption = x.Option("-f|--fix", "Fix known issues if possible.", CommandOptionType.NoValue);
              x.OnExecute(() => new ValidateCommand(
                new ConsoleLogger(verboseOption.HasValue()),
                AccessUtil.GetStorage(dirOption.Value(), awsS3BucketNameOption.Value(), awsS3RegionEndpointOption.Value()),
                AccessUtil.GetDegreeOfParallelism(degreeOfParallelismOption.Value()),
                aclOption.HasValue(),
                fixOption.HasValue()).ExecuteAsync());
            });

          static void SafetyPeriodOptions(
            CommandLineApplication x,
            TimeSpan? defaultPeriod,
            out CommandOption safetyPeriodOption)
          {
            string description = "The safety period for young files (files with a lower age will be skipped).";
            if (defaultPeriod != null)
              description += $" {defaultPeriod.Value.Days:D} days by default.";
            
            safetyPeriodOption = x.Option("-sp|--safety-period", description, CommandOptionType.SingleValue);
          }

          commandLine.Command("list", x =>
            {
              x.HelpOption("-h|--help");
              x.Description = "List storage metadata information";
              FilterOptions(x,
                out var incFilterProductOption,
                out var excFilterProductOption,
                out var incFilterVersionOption,
                out var excFilterVersionOption);
              SafetyPeriodOptions(x, null, out var safetyPeriodOption);
              var filterProtectedOption = x.Option("-fr|--protected-filter", $"Filter by protected value: {AccessUtil.ProtectedAll}, {AccessUtil.ProtectedOn} and {AccessUtil.ProtectedOff}. The default is {AccessUtil.ProtectedAll}.", CommandOptionType.SingleValue);
              x.OnExecute(() => new ListCommand(
                new ConsoleLogger(verboseOption.HasValue()),
                AccessUtil.GetStorage(dirOption.Value(), awsS3BucketNameOption.Value(), awsS3RegionEndpointOption.Value()),
                AccessUtil.GetDegreeOfParallelism(degreeOfParallelismOption.Value()),
                new IdentityFilter(
                  incFilterProductOption.Values,
                  excFilterProductOption.Values,
                  incFilterVersionOption.Values,
                  excFilterVersionOption.Values),
                ParseDays(safetyPeriodOption.Value(), defaultDays: null),
                ParseProtected(filterProtectedOption.Value(), AccessUtil.ProtectedAll)).ExecuteAsync());
            });

          commandLine.Command("delete", x =>
            {
              x.HelpOption("-h|--help");
              x.Description = "Delete storage metadata and referenced data files";
              FilterOptions(x,
                out var incFilterProductOption,
                out var excFilterProductOption,
                out var incFilterVersionOption,
                out var excFilterVersionOption);
              SafetyPeriodOptions(x, AccessUtil.DefaultSafetyPeriod, out var safetyPeriodOption);
              x.OnExecute(() => new DeleteCommand(
                new ConsoleLogger(verboseOption.HasValue()),
                AccessUtil.GetStorage(dirOption.Value(), awsS3BucketNameOption.Value(), awsS3RegionEndpointOption.Value()),
                AccessUtil.GetDegreeOfParallelism(degreeOfParallelismOption.Value()),
                new IdentityFilter(
                  incFilterProductOption.Values,
                  excFilterProductOption.Values,
                  incFilterVersionOption.Values,
                  excFilterVersionOption.Values),
                ParseDays(safetyPeriodOption.Value(), defaultDays: AccessUtil.DefaultSafetyPeriod).Value).ExecuteAsync());
            });
        }

        static void StorageOptions(
          CommandLineApplication x,
          out CommandOption newStorageFormatOption)
        {
          newStorageFormatOption = x.Option("-nsf|--new-storage-format", $"Select data files format for a new storage: {AccessUtil.NormalStorageFormat} (default), {AccessUtil.LowerStorageFormat}, {AccessUtil.UpperStorageFormat}.", CommandOptionType.SingleValue);
        }

        commandLine.Command("new", x =>
          {
            x.HelpOption("-h|--help");
            x.Description = "Create empty storage";
            StorageOptions(x, out var newStorageFormatOption);
            x.OnExecute(() => new NewCommand(
              new ConsoleLogger(verboseOption.HasValue()),
              AccessUtil.GetStorage(dirOption.Value(), awsS3BucketNameOption.Value(), awsS3RegionEndpointOption.Value()),
              AccessUtil.GetStorageFormat(newStorageFormatOption.Value())).ExecuteAsync());
          });

        commandLine.Command("upload", x =>
          {
            x.HelpOption("-h|--help");
            x.Description = "Upload one storage to another one with the source storage inconsistency check";
            var sourceOption = x.Option("-s|--source", "Source storage directory.", CommandOptionType.SingleValue);
            var collisionResolutionMode = x.Option("-crm|--collision-resolution", $"Collision resolution mode: {CollisionResolutionMode.Terminate} (default), {CollisionResolutionMode.KeepExisted}, {CollisionResolutionMode.Overwrite}, {CollisionResolutionMode.OverwriteWithoutBackup}.", CommandOptionType.SingleValue);
            var peCollisionResolutionMode = x.Option("-crmpe|--collision-resolution-pe", $"Collision resolution mode override for PE weak hash: {CollisionResolutionMode.Terminate}, {CollisionResolutionMode.KeepExisted}, {CollisionResolutionMode.Overwrite}, {CollisionResolutionMode.OverwriteWithoutBackup}.", CommandOptionType.SingleValue);
            var backupStorage = x.Option("-bckp|--backup-directory", "Directory to store backup in case of collisions", CommandOptionType.SingleValue);
            StorageOptions(x, out var newStorageFormatOption);
            x.OnExecute(() => new UploadCommand(
              new ConsoleLogger(verboseOption.HasValue()),
              AccessUtil.GetStorage(dirOption.Value(), awsS3BucketNameOption.Value(), awsS3RegionEndpointOption.Value()),
              AccessUtil.GetDegreeOfParallelism(degreeOfParallelismOption.Value()),
              sourceOption.Value(),
              AccessUtil.GetStorageFormat(newStorageFormatOption.Value()),
              collisionResolutionMode: AccessUtil.GetCollisionResolutionMode(collisionResolutionMode.Value()),
              peCollisionResolutionMode: AccessUtil.GetCollisionResolutionMode(peCollisionResolutionMode.Value(), AccessUtil.GetCollisionResolutionMode(collisionResolutionMode.Value())),
              backupStorageDir: backupStorage.Value()).ExecuteAsync());
          });

        commandLine.Command("create", x =>
          {
            x.HelpOption("-h|--help");
            x.Description = "Create temporary storage and upload it to another one";
            var compressWPdbOption = x.Option("-cwpdb|--compress-windows-pdb", "Enable compression for Windows PDB files. Windows only. Incompatible with the SSQP.", CommandOptionType.NoValue);
            var compressPeOption = x.Option("-cpe|--compress-pe", "Enable compression for PE files. Windows only. Incompatible with the SSQP.", CommandOptionType.NoValue);
            var keepNonCompressedOption = x.Option("-k|--keep-non-compressed", "Store also non-compressed version in storage.", CommandOptionType.NoValue);
            var propertiesOption = x.Option("-p|--property", "The property to be stored in metadata in following format: <key1>=<value1>[,<key2>=<value2>[,...]]. Can be declared many times.", CommandOptionType.MultipleValue);
            var protectedOption = x.Option("-r|--protected", "Protect files form deletion.", CommandOptionType.NoValue);
            var collisionResolutionMode = x.Option("-crm|--collision-resolution", $"Collision resolution mode: {CollisionResolutionMode.Terminate} (default), {CollisionResolutionMode.KeepExisted}, {CollisionResolutionMode.Overwrite}, {CollisionResolutionMode.OverwriteWithoutBackup}.", CommandOptionType.SingleValue);
            var peCollisionResolutionMode = x.Option("-crmpe|--collision-resolution-pe", $"Collision resolution mode override for PE weak hash: {CollisionResolutionMode.Terminate}, {CollisionResolutionMode.KeepExisted}, {CollisionResolutionMode.Overwrite}, {CollisionResolutionMode.OverwriteWithoutBackup}.", CommandOptionType.SingleValue);
            var backupStorage = x.Option("-bckp|--backup-directory", "Directory to store backup in case of collisions", CommandOptionType.SingleValue);
            StorageOptions(x, out var newStorageFormatOption);
            var productArgument = x.Argument("product", "The product name.");
            var versionArgument = x.Argument("version", "The product version.");
            var sourcesOption = x.Argument("path [path [...]] or @file", "Source directories or files with symbols, executables and shared libraries.", true);
            x.OnExecute(async () =>
              {
                var storage = AccessUtil.GetStorage(dirOption.Value(), awsS3BucketNameOption.Value(), awsS3RegionEndpointOption.Value());
                var newStorageFormat = AccessUtil.GetStorageFormat(newStorageFormatOption.Value());
                var sources = await ParsePaths(sourcesOption.Values);
                var properties = propertiesOption.Values.ParseProperties();
                var tempDir = Path.Combine(Path.GetTempPath(), "storage_" + Guid.NewGuid().ToString("D"));
                ILogger logger = new ConsoleLogger(verboseOption.HasValue());
                var degreeOfParallelism = AccessUtil.GetDegreeOfParallelism(degreeOfParallelismOption.Value());
                var parsedCollisionResolutionMode = AccessUtil.GetCollisionResolutionMode(collisionResolutionMode.Value());
                var parsedPeCollisionResolutionMode = AccessUtil.GetCollisionResolutionMode(peCollisionResolutionMode.Value(), parsedCollisionResolutionMode);
                if ((parsedCollisionResolutionMode == CollisionResolutionMode.Overwrite || parsedPeCollisionResolutionMode == CollisionResolutionMode.Overwrite) && !backupStorage.HasValue())
                  throw new ArgumentException("Backup directory must be specified when collision resolution mode is 'overwrite'");
                
                try
                {
                  var res = await new CreateCommand(
                    logger,
                    new FileSystemStorage(tempDir),
                    degreeOfParallelism,
                    StorageFormat.Normal,
                    toolName + '/' + toolVersion,
                    new Identity(
                      productArgument.Value,
                      versionArgument.Value),
                    protectedOption.HasValue(),
                    compressPeOption.HasValue(),
                    compressWPdbOption.HasValue(),
                    keepNonCompressedOption.HasValue(),
                    properties,
                    sources).ExecuteAsync();
                  if (res != 0)
                    return res;

                  return await new UploadCommand(
                    logger,
                    storage,
                    degreeOfParallelism,
                    tempDir,
                    newStorageFormat,
                    parsedCollisionResolutionMode,
                    parsedPeCollisionResolutionMode,
                    backupStorage.Value()).ExecuteAsync();
                }
                finally
                {
                  Directory.Delete(tempDir, true);
                }
              });
          });
        
        commandLine.Command("dump", x =>
          {
            x.HelpOption("-h|--help");
            x.Description = "Dump symbol references";
            var compressWPdbOption = x.Option("-cwpdb|--compress-windows-pdb", "Enable compression for Windows PDB files. Windows only. Incompatible with the SSQP.", CommandOptionType.NoValue);
            var compressPeOption = x.Option("-cpe|--compress-pe", "Enable compression for PE files. Windows only. Incompatible with the SSQP.", CommandOptionType.NoValue);
            var symbolReferenceFileOption = x.Argument("symref", "Symbol references file.");
            var baseDirOption = x.Argument("basedir", "Base Directory.");
            var sourcesOption = x.Argument("path [path [...]] or @file", "Source directories or files with symbols, executables and shared libraries.", true);

            x.OnExecute(async () =>
              {
                var sources = await ParsePaths(sourcesOption.Values.Count != 0 ? sourcesOption.Values : new[] { baseDirOption.Value });
                return await new DumpCommand(
                  new ConsoleLogger(verboseOption.HasValue()),
                  AccessUtil.GetDegreeOfParallelism(degreeOfParallelismOption.Value()),
                  compressPeOption.HasValue(),
                  compressWPdbOption.HasValue(),
                  symbolReferenceFileOption.Value,
                  sources,
                  baseDirOption.Value).ExecuteAsync();
              });
          });

        commandLine.Command("protect", x =>
          {
            x.HelpOption("-h|--help");
            x.Description = "Protect storage files from deletion";
            FilterOptions(x,
              out var incFilterProductOption,
              out var excFilterProductOption,
              out var incFilterVersionOption,
              out var excFilterVersionOption);
            var clearOption = x.Option("-c|--clear", "Clear protection.", CommandOptionType.NoValue);
            x.OnExecute(() => new ProtectedCommand(
              new ConsoleLogger(verboseOption.HasValue()),
              AccessUtil.GetStorage(dirOption.Value(), awsS3BucketNameOption.Value(), awsS3RegionEndpointOption.Value()),
              AccessUtil.GetDegreeOfParallelism(degreeOfParallelismOption.Value()),
              new IdentityFilter(
                incFilterProductOption.Values,
                excFilterProductOption.Values,
                incFilterVersionOption.Values,
                excFilterVersionOption.Values),
              !clearOption.HasValue()).ExecuteAsync());
          });

        if (args.Length != 0)
        {
          var res = commandLine.Execute(args);
          if (0 <= res && res < 126)
            return (byte) res;
          return 255;
        }

        commandLine.ShowHint();
        return 127;
      }
      catch (Exception e)
      {
        ConsoleLogger.Exception(e);
        return 126;
      }
    }

    [return: NotNullIfNotNull(nameof(defaultDays))]
    private static TimeSpan? ParseDays([CanBeNull] string days, TimeSpan? defaultDays) => days != null ? TimeSpan.FromDays(ulong.Parse(days)) : defaultDays;

    private static bool? ParseProtected([CanBeNull] string value, [Annotations.NotNull] string defaultValue) => value != null
      ? AccessUtil.GetProtectedValue(value)
      : AccessUtil.GetProtectedValue(defaultValue);

    private static async Task<IReadOnlyCollection<string>> ParsePaths([Annotations.NotNull] IEnumerable<string> paths)
    {
      if (paths == null)
        throw new ArgumentNullException(nameof(paths));
      var res = new List<string>();
      foreach (var path in paths)
        if (path.StartsWith('@'))
        {
          using var reader = new StreamReader(path.Substring(1));
          string line;
          while ((line = await reader.ReadLineAsync()) != null)
            if (line.Length != 0)
              res.Add(line);
        }
        else
          res.Add(path);

      return res;
    }
  }
}