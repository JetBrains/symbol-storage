using System;
using System.IO;
using System.Reflection;
using Amazon;
using JetBrains.Annotations;
using JetBrains.SymbolStorage.Impl;
using JetBrains.SymbolStorage.Impl.Commands;
using JetBrains.SymbolStorage.Impl.Logger;
using JetBrains.SymbolStorage.Impl.Storages;
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
    public static byte Main(Assembly mainAssembly, [NotNull] string[] args, MainMode mode)
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
        var awsS3BucketNameOption = commandLine.Option("-a|--aws-s3", $"The AWS S3 bucket with symbol server storage. The access and private keys will be asked in console. Use {AccessUtil.AwsS3AccessKeyEnvironmentVariable}, {AccessUtil.AwsS3SecretKeyEnvironmentVariable} and {AccessUtil.AwsCloudFrontDistributionIdEnvironmentVariable} environment variables for unattended mode.", CommandOptionType.SingleValue);
        var awsS3RegionEndpointOption = commandLine.Option("-ar|--aws-s3-region", $"The AWS S3 region endpoint with symbol server storage. Default is {AccessUtil.DefaultAwsS3RegionEndpoint}.", CommandOptionType.SingleValue);

        if (mode == MainMode.Full)
        {
          commandLine.Command("validate", x =>
            {
              x.HelpOption("-h|--help");
              x.Description = "Storage inconsistency check and fix known issues by request";
              var aclOption = x.Option("-r|--rights", "Validate access rights.", CommandOptionType.NoValue);
              var fixOption = x.Option("-f|--fix", "Fix known issues if possible.", CommandOptionType.NoValue);
              x.OnExecute(() => new ValidateCommand(
                ConsoleLogger.Instance,
                AccessUtil.GetStorage(dirOption.Value(), awsS3BucketNameOption.Value(), awsS3RegionEndpointOption.Value()),
                aclOption.HasValue(),
                fixOption.HasValue()).Execute());
            });

          commandLine.Command("list", x =>
            {
              x.HelpOption("-h|--help");
              x.Description = "List storage metadata information";
              var incFilterProductOption = x.Option("-fpi|--product-include-filter", "Select wildcard for include product filtering.", CommandOptionType.MultipleValue);
              var excFilterProductOption = x.Option("-fpe|--product-exclude-filter", "Select wildcard for exclude product filtering.", CommandOptionType.MultipleValue);
              var incFilterVersionOption = x.Option("-fvi|--version-include-filter", "Select wildcard for include version filtering.", CommandOptionType.MultipleValue);
              var excFilterVersionOption = x.Option("-fve|--version-exclude-filter", "Select wildcard for exclude version filtering.", CommandOptionType.MultipleValue);
              x.OnExecute(() => new ListCommand(
                ConsoleLogger.Instance,
                AccessUtil.GetStorage(dirOption.Value(), awsS3BucketNameOption.Value(), awsS3RegionEndpointOption.Value()),
                incFilterProductOption.Values,
                excFilterProductOption.Values,
                incFilterVersionOption.Values,
                excFilterVersionOption.Values).Execute());
            });

          commandLine.Command("delete", x =>
            {
              x.HelpOption("-h|--help");
              x.Description = "Delete storage metadata and referenced data files";
              var incFilterProductOption = x.Option("-fpi|--product-include-filter", "Select wildcard for include product filtering.", CommandOptionType.MultipleValue);
              var excFilterProductOption = x.Option("-fpe|--product-exclude-filter", "Select wildcard for exclude product filtering.", CommandOptionType.MultipleValue);
              var incFilterVersionOption = x.Option("-fvi|--version-include-filter", "Select wildcard for include version filtering.", CommandOptionType.MultipleValue);
              var excFilterVersionOption = x.Option("-fve|--version-exclude-filter", "Select wildcard for exclude version filtering.", CommandOptionType.MultipleValue);
              x.OnExecute(() => new DeleteCommand(
                ConsoleLogger.Instance,
                AccessUtil.GetStorage(dirOption.Value(), awsS3BucketNameOption.Value(), awsS3RegionEndpointOption.Value()),
                incFilterProductOption.Values,
                excFilterProductOption.Values,
                incFilterVersionOption.Values,
                excFilterVersionOption.Values).Execute());
            });
        }

        commandLine.Command("new", x =>
          {
            x.HelpOption("-h|--help");
            x.Description = "Create empty storage";
            var newStorageFormatOption = x.Option("-nsf|--new-storage-format", $"Select data files format for a new storage: {AccessUtil.NormalStorageFormat} (default), {AccessUtil.LowerStorageFormat}, {AccessUtil.UpperStorageFormat}.", CommandOptionType.SingleValue);
            x.OnExecute(() => new NewCommand(
              ConsoleLogger.Instance,
              AccessUtil.GetStorage(dirOption.Value(), awsS3BucketNameOption.Value(), awsS3RegionEndpointOption.Value()),
              AccessUtil.GetStorageFormat(newStorageFormatOption.Value())).Execute());
          });

        commandLine.Command("upload", x =>
          {
            x.HelpOption("-h|--help");
            x.Description = "Upload one storage to another one with the source storage inconsistency check";
            var sourceOption = x.Option("-s|--source", "Source storage directory.", CommandOptionType.SingleValue);
            var newStorageFormatOption = x.Option("-nsf|--new-storage-format", $"Select data files format for a new storage: {AccessUtil.NormalStorageFormat} (default), {AccessUtil.LowerStorageFormat}, {AccessUtil.UpperStorageFormat}.", CommandOptionType.SingleValue);
            x.OnExecute(() => new UploadCommand(
              ConsoleLogger.Instance,
              AccessUtil.GetStorage(dirOption.Value(), awsS3BucketNameOption.Value(), awsS3RegionEndpointOption.Value()),
              sourceOption.Value(),
              AccessUtil.GetStorageFormat(newStorageFormatOption.Value())).Execute());
          });

        commandLine.Command("create", x =>
          {
            x.HelpOption("-h|--help");
            x.Description = "Create temporary storage and upload it to another one";
            var compressWPdbOption = x.Option("-cwpdb|--compress-windows-pdb", "Enable compression for Windows PDB files. Windows only. Incompatible with the SSQP.", CommandOptionType.NoValue);
            var compressPeOption = x.Option("-cpe|--compress-pe", "Enable compression for PE files. Windows only. Incompatible with the SSQP.", CommandOptionType.NoValue);
            var keepNonCompressedOption = x.Option("-k|--keep-non-compressed", "Store also non-compressed version in storage.", CommandOptionType.NoValue);
            var propertiesOption = x.Option("-p|--property", "The property to be stored in metadata in following format: <key>=<value>. Can be declared many times.", CommandOptionType.MultipleValue);
            var newStorageFormatOption = x.Option("-nsf|--new-storage-format", $"Select data files format for a new storage: {AccessUtil.NormalStorageFormat} (default), {AccessUtil.LowerStorageFormat}, {AccessUtil.UpperStorageFormat}.", CommandOptionType.SingleValue);
            var productArgument = x.Argument("product", "The product name.");
            var versionArgument = x.Argument("version", "The product version.");
            var sourcesOption = x.Argument("path [path [...]]", "Source directories or files with symbols, executables and shared libraries.", true);
            x.OnExecute(async () =>
              {
                var storage = AccessUtil.GetStorage(dirOption.Value(), awsS3BucketNameOption.Value(), awsS3RegionEndpointOption.Value());
                var newStorageFormat = AccessUtil.GetStorageFormat(newStorageFormatOption.Value());
                
                var tempDir = Path.Combine(Path.GetTempPath(), "storage_" + Guid.NewGuid().ToString("D"));
                try
                {
                  var res = await new CreateCommand(
                    ConsoleLogger.Instance,
                    new FileSystemStorage(tempDir),
                    StorageFormat.Normal,
                    toolName + '/' + toolVersion,
                    productArgument.Value,
                    versionArgument.Value,
                    compressPeOption.HasValue(),
                    compressWPdbOption.HasValue(),
                    keepNonCompressedOption.HasValue(),
                    propertiesOption.Values,
                    sourcesOption.Values).Execute();
                  if (res != 0)
                    return res;

                  return await new UploadCommand(
                    ConsoleLogger.Instance,
                    storage,
                    tempDir,
                    newStorageFormat).Execute();
                }
                finally
                {
                  Directory.Delete(tempDir, true);
                }
              });
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
        ConsoleLogger.Instance.Error(e.ToString());
        return 126;
      }
    }
  }
}