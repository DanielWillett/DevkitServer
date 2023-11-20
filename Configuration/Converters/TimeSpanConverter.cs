using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DevkitServer.Configuration.Converters;
public sealed class TimeSpanConverter : JsonConverter<TimeSpan>
{
    public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("Invalid token type: " + reader.TokenType + ", expected String.");
        }

        return TimeSpan.Parse(reader.GetString()!, CultureInfo.InvariantCulture);
    }
    public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString("c"));
    }
}