using System.Text.Json.Serialization;

namespace JetBrains.SymbolStorage.Impl.Tags
{
  internal sealed record TagKeyValue
  {
    [JsonPropertyOrder(0)]
    [JsonRequired]
    public required string Key { get; init; }
    
    [JsonPropertyOrder(1)]
    [JsonRequired]
    public required string Value { get; init; }
  }
}