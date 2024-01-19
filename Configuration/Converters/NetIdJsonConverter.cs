using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DevkitServer.Configuration.Converters;
public sealed class NetIdJsonConverter : JsonConverter<NetId>
{
    public override NetId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return NetId.INVALID;
            case JsonTokenType.Number:
                if (reader.TryGetUInt32(out uint value))
                    return value == 0 ? NetId.INVALID : new NetId(value);

                throw new JsonException("Failed to parse number as a NetID (UInt32 format).");

            case JsonTokenType.String:
                ReadOnlySpan<char> str = reader.GetString()!;

                if (str.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    str = str[2..];

                if (uint.TryParse(str, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value))
                    return new NetId(value);

                throw new JsonException("Failed to parse string as a NetID (Hexadecimal UInt32 format).");

            case JsonTokenType.StartObject:
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndObject)
                        break;
                    if (reader.TokenType == JsonTokenType.PropertyName)
                    {
                        string prop = reader.GetString()!;
                        if (prop.Equals("id", StringComparison.InvariantCultureIgnoreCase) && reader.Read())
                        {
                            switch (reader.TokenType)
                            {
                                case JsonTokenType.Null:
                                    return NetId.INVALID;
                                case JsonTokenType.Number:
                                    if (reader.TryGetUInt32(out value))
                                        return value == 0 ? NetId.INVALID : new NetId(value);

                                    throw new JsonException("Failed to parse number as a NetID (Object UInt32 format).");

                                case JsonTokenType.String:
                                    str = reader.GetString()!;

                                    if (str.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                                        str = str[2..];

                                    if (uint.TryParse(str, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value))
                                        return new NetId(value);

                                    throw new JsonException("Failed to parse string as a NetID (Object Hexadecimal UInt32 format).");

                                default:
                                    throw new JsonException($"Failed to parse a NetID, unknown token: {reader.TokenType} in (Object format).");
                            }
                        }
                    }
                }

                throw new JsonException("Failed to parse object as a NetID (Object format).");
            default:
                throw new JsonException($"Failed to parse a NetID, unknown token: {reader.TokenType}.");
        }
    }

    public override void Write(Utf8JsonWriter writer, NetId value, JsonSerializerOptions options)
    {
        if (value.IsNull())
            writer.WriteNullValue();
        else
            writer.WriteStringValue("0x" + value.id.ToString("x8"));
    }
}