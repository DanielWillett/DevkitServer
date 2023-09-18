using NuGet.Versioning;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DevkitServer.Launcher.Models;
internal class NuGetIndex
{
    [JsonPropertyName("version")]
    [JsonConverter(typeof(NuGetVersionConverter))]
    public NuGetVersion? Version { get; set; }

    [JsonPropertyName("resources")]
    public NuGetResource[] Resources { get; set; } = Array.Empty<NuGetResource>();
}

#nullable disable
internal class NuGetResource
{
    [JsonPropertyName("@id")]
    public string Id { get; set; }

    [JsonPropertyName("@type")]
    public string Type { get; set; }

    [JsonPropertyName("comment")]
    public string Comment { get; set; }
}
#nullable restore

internal class NuGetVersionConverter : JsonConverter<NuGetVersion>
{
    public override NuGetVersion? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType == JsonTokenType.Null ? null : NuGetVersion.Parse(reader.GetString()!);
    }
    public override void Write(Utf8JsonWriter writer, NuGetVersion value, JsonSerializerOptions options)
    {
        if (value == null)
            writer.WriteNullValue();
        else
            writer.WriteStringValue(value.ToString());
    }
}