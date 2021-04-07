using System;
using System.Runtime.Serialization;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace JetBrains.SymbolStorage.Impl.Tags
{
  [DataContract]
  [Serializable]
  [JsonObject]
  internal sealed class Tag
  {
    [IgnoreDataMember]
    public DateTime CreationUtcTime;

    [CanBeNull]
    [DataMember(Order = 1000, IsRequired = true)]
    public string[] Directories;

    [IgnoreDataMember]
    public Guid FileId;

    [DataMember(Order = 11, EmitDefaultValue = false)]
    public bool IsProtected;

    [CanBeNull]
    [DataMember(Order = 2, IsRequired = true)]
    public string Product;

    [CanBeNull]
    [DataMember(Order = 100)]
    public TagKeyValue[] Properties;

    [CanBeNull]
    [DataMember(Order = 0, IsRequired = true)]
    public string ToolId;

    [CanBeNull]
    [DataMember(Order = 3, IsRequired = true)]
    public string Version;

    [DataMember(Order = 10, Name = nameof(CreationUtcTime))]
    public string _CreationUtcTime
    {
      get => CreationUtcTime.ToString("s");
      set => CreationUtcTime = DateTime.ParseExact(value, "s", null);
    }

    [DataMember(Order = 1, Name = nameof(FileId))]
    public string _FileId
    {
      get => FileId.ToString("D");
      set => FileId = Guid.ParseExact(value, "D");
    }
  }
}