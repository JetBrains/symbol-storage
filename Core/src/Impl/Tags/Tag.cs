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
    [DataMember(Order = 1000, IsRequired = false, EmitDefaultValue = false)]
    public string[] Directories;

    [CanBeNull]
    [DataMember(Order = 1)]
    public string FileId;

    [CanBeNull]
    [DataMember(Order = 2)]
    public string Product;

    [CanBeNull]
    [DataMember(Order = 100, IsRequired = false, EmitDefaultValue = false)]
    public TagKeyValue[] Properties;

    [CanBeNull]
    [DataMember(Order = 0)]
    public string ToolId;

    [CanBeNull]
    [DataMember(Order = 3)]
    public string Version;
  }
}