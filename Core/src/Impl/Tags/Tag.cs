using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JetBrains.SymbolStorage.Impl.Tags
{
  internal sealed record Tag
  {
    [JsonPropertyOrder(10)]
    [JsonConverter(typeof(DateTimeCustomJsonConverter))]
    public DateTime CreationUtcTime { get; set; }
    
    [JsonPropertyOrder(1000)]
    [JsonRequired]
    public required string[] Directories { get; set; }
    
    [JsonPropertyOrder(1)]
    [JsonRequired]
    [JsonConverter(typeof(GuidCustomJsonConverter))]
    public required Guid FileId { get; set; }
    
    [JsonPropertyOrder(11)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsProtected { get; set; }
    
    [JsonPropertyOrder(2)]
    [JsonRequired]
    public required string Product { get; set; }
    
    [JsonPropertyOrder(100)]
    public TagKeyValue[]? Properties { get; set; }
    
    [JsonPropertyOrder(0)]
    [JsonRequired]
    public required string? ToolId { get; set; }
    
    [JsonPropertyOrder(3)]
    [JsonRequired]
    public required string Version { get; set; }
  }
  
  internal class DateTimeCustomJsonConverter : JsonConverter<DateTime>
  {
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
      return DateTime.ParseExact(reader.GetString()!, "s", null);
    }

    public override void Write(Utf8JsonWriter writer, DateTime dateTimeValue, JsonSerializerOptions options)
    {
      writer.WriteStringValue(dateTimeValue.ToString("s")); 
    }
  }
  
  internal class GuidCustomJsonConverter : JsonConverter<Guid>
  {
    public override Guid Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
      return Guid.ParseExact(reader.GetString()!, "D");
    }

    public override void Write(Utf8JsonWriter writer, Guid guidValue, JsonSerializerOptions options)
    {
      writer.WriteStringValue(guidValue.ToString("D")); 
    }
  }
}