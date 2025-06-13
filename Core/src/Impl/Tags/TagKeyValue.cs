using System;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace JetBrains.SymbolStorage.Impl.Tags
{
  [DataContract]
  [Serializable]
  [JsonObject]
  internal sealed class TagKeyValue
  {
    [DataMember(Order = 0)]
    public string? Key;

    [DataMember(Order = 1)]
    public string? Value;
  }
}