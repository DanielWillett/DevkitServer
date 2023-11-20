using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DevkitServer.Configuration.Converters;

public sealed class Vector2JsonConverter : JsonConverter<Vector2>
{
    private static readonly char[] SplitChars = { ',' };
    public override Vector2 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        float x = 0f, y = 0f;
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            int ct = 0;
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                {
                    if (ct == 2)
                        break;
                    throw new JsonException("Array notation for Vector2 must provide 2 float elements.");
                }
                if (reader.TokenType == JsonTokenType.Number && reader.TryGetSingle(out float fl))
                {
                    switch (++ct)
                    {
                        case 1:
                            x = fl;
                            break;
                        case 2:
                            y = fl;
                            break;
                    }
                }
                else throw new JsonException("Invalid number in Vector2 array notation.");
            }
        }
        else if (reader.TokenType == JsonTokenType.StartObject)
        {
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                    break;
                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    string? prop = reader.GetString()!;
                    if (reader.Read() && prop is not null)
                    {
                        if (prop.Equals("x", StringComparison.InvariantCultureIgnoreCase))
                        {
                            if (reader.TokenType == JsonTokenType.Number && reader.TryGetSingle(out float fl))
                                x = fl;
                            else
                                throw new JsonException("Invalid x in Vector2 object notation.");
                        }
                        else if (prop.Equals("y", StringComparison.InvariantCultureIgnoreCase))
                        {
                            if (reader.TokenType == JsonTokenType.Number && reader.TryGetSingle(out float fl))
                                y = fl;
                            else
                                throw new JsonException("Invalid y in Vector2 object notation.");
                        }
                    }
                }
            }
        }
        else if (reader.TokenType == JsonTokenType.String)
        {
            string[] strs = reader.GetString()!.Split(SplitChars, StringSplitOptions.RemoveEmptyEntries);
            if (strs.Length == 2)
            {
                if (!float.TryParse(strs[0].Replace('(', ' '), NumberStyles.Number, CultureInfo.InvariantCulture, out x))
                    throw new JsonException("Invalid x in Vector2 string notation.");
                if (!float.TryParse(strs[1].Replace(')', ' '), NumberStyles.Number, CultureInfo.InvariantCulture, out y))
                    throw new JsonException("Invalid y in Vector2 string notation.");
            }
            else
                throw new JsonException("String notation for Vector3 must provide 2 float elements.");
        }
        else
            throw new JsonException("Unexpected token " + reader.TokenType + " for reading Vector3.");

        return new Vector2(x, y);
    }
    public override void Write(Utf8JsonWriter writer, Vector2 value, JsonSerializerOptions options)
    {
        using JsonIndent indent = writer.StopIndenting();
        writer.WriteStartArray();
        writer.WriteNumberValue(value.x);
        writer.WriteNumberValue(value.y);
        writer.WriteEndArray();
    }
}