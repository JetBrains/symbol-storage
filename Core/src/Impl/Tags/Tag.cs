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
    [CanBeNull]
    [DataMember(Order = 1000, IsRequired = true)]
    public string[] Directories;

    [CanBeNull]
    [DataMember(Order = 1)]
    public string FileId;

    [CanBeNull]
    [DataMember(Order = 2, IsRequired = true)]
    public string Product;

    [CanBeNull]
    [DataMember(Order = 100)]
    public TagKeyValue[] Properties;

    [CanBeNull]
    [DataMember(Order = 0, IsRequired = true)]
    public string ToolId;

    [IgnoreDataMember]
    public DateTime CreationUtcTime;

    [CanBeNull]
    [DataMember(Order = 3, IsRequired = true)]
    public string Version;

    [DataMember(Order = 4, Name = nameof(CreationUtcTime))]
    public string _CreationUtcTime
    {
      get => CreationUtcTime.ToString("s");
      set => CreationUtcTime = DateTime.ParseExact(value, "s", null);
    }
  }
}