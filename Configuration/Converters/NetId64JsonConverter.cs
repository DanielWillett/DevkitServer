using DevkitServer.Multiplayer.Actions;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DevkitServer.Configuration.Converters;
public sealed class NetId64JsonConverter : JsonConverter<NetId64>
{
    public override NetId64 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return NetId64.Invalid;
            case JsonTokenType.Number:
                if (reader.TryGetUInt64(out ulong value))
                    return value == 0 ? NetId64.Invalid : new NetId64(value);

                throw new JsonException("Failed to parse number as a NetID64 (UInt64 format).");

            case JsonTokenType.String:
                ReadOnlySpan<char> str = reader.GetString()!;

                if (str.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    str = str[2..];

                if (ulong.TryParse(str, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value))
                    return new NetId64(value);

                throw new JsonException("Failed to parse string as a NetID64 (Hexadecimal UInt64 format).");

            case JsonTokenType.StartObject:
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndObject)
                        break;

                    if (reader.TokenType != JsonTokenType.PropertyName)
                        continue;

                    string prop = reader.GetString()!;
                    if (prop.Equals("id", StringComparison.InvariantCultureIgnoreCase) && reader.Read())
                    {
                        switch (reader.TokenType)
                        {
                            case JsonTokenType.Null:
                                return NetId64.Invalid;
                            case JsonTokenType.Number:
                                if (reader.TryGetUInt64(out value))
                                    return value == 0 ? NetId64.Invalid : new NetId64(value);

                                throw new JsonException("Failed to parse number as a NetID64 (Object UInt64 format).");

                            case JsonTokenType.String:
                                str = reader.GetString()!;

                                if (str.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                                    str = str[2..];

                                if (ulong.TryParse(str, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value))
                                    return new NetId64(value);

                                throw new JsonException("Failed to parse string as a NetID64 (Object Hexadecimal UInt64 format).");

                            default:
                                throw new JsonException($"Failed to parse a NetID64, unknown token: {reader.TokenType} in (Object format).");
                        }
                    }
                }

                throw new JsonException("Failed to parse object as a NetID64 (Object format).");
            default:
                throw new JsonException($"Failed to parse a NetID64, unknown token: {reader.TokenType}.");
        }
    }

    public override void Write(Utf8JsonWriter writer, NetId64 value, JsonSerializerOptions options)
    {
        if (value.IsNull())
            writer.WriteNullValue();
        else
            writer.WriteStringValue("0x" + value.Id.ToString("x16"));
    }
}