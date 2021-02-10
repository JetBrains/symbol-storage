using System;
using System.Runtime.Serialization;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace JetBrains.SymbolStorage.Impl.Tags
{
  [DataContract]
  [Serializable]
  [JsonObject]
  internal sealed class TagKeyValue
  {
    [CanBeNull]
    [DataMember(Order = 0)]
    public string Key;

    [CanBeNull]
    [DataMember(Order = 1)]
    public string Value;
  }
}