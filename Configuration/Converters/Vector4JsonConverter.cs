using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DevkitServer.Configuration.Converters;

public sealed class Vector4JsonConverter : JsonConverter<Vector4>
{
    private static readonly char[] SplitChars = { ',' };
    public override Vector4 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        float x = 0f, y = 0f, z = 0f, w = 0f;
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            int ct = 0;
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                {
                    if (ct == 4)
                        break;
                    throw new JsonException("Array notation for Vector4 must provide 4 float elements.");
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
                        case 3:
                            z = fl;
                            break;
                        case 4:
                            w = fl;
                            break;
                    }
                }
                else throw new JsonException("Invalid number in Vector4 array notation.");
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
                                throw new JsonException("Invalid x in Vector4 object notation.");
                        }
                        else if (prop.Equals("y", StringComparison.InvariantCultureIgnoreCase))
                        {
                            if (reader.TokenType == JsonTokenType.Number && reader.TryGetSingle(out float fl))
                                y = fl;
                            else
                                throw new JsonException("Invalid y in Vector4 object notation.");
                        }
                        else if (prop.Equals("z", StringComparison.InvariantCultureIgnoreCase))
                        {
                            if (reader.TokenType == JsonTokenType.Number && reader.TryGetSingle(out float fl))
                                z = fl;
                            else
                                throw new JsonException("Invalid z in Vector4 object notation.");
                        }
                        else if (prop.Equals("w", StringComparison.InvariantCultureIgnoreCase))
                        {
                            if (reader.TokenType == JsonTokenType.Number && reader.TryGetSingle(out float fl))
                                w = fl;
                            else
                                throw new JsonException("Invalid w in Vector4 object notation.");
                        }
                    }
                }
            }
        }
        else if (reader.TokenType == JsonTokenType.String)
        {
            string[] strs = reader.GetString()!.Split(SplitChars, StringSplitOptions.RemoveEmptyEntries);
            if (strs.Length == 4)
            {
                if (!float.TryParse(strs[0].Replace('(', ' '), NumberStyles.Number, CultureInfo.InvariantCulture, out x))
                    throw new JsonException("Invalid x in Vector4 string notation.");
                if (!float.TryParse(strs[1], NumberStyles.Number, CultureInfo.InvariantCulture, out y))
                    throw new JsonException("Invalid y in Vector4 string notation.");
                if (!float.TryParse(strs[2], NumberStyles.Number, CultureInfo.InvariantCulture, out y))
                    throw new JsonException("Invalid z in Vector4 string notation.");
                if (!float.TryParse(strs[3].Replace(')', ' '), NumberStyles.Number, CultureInfo.InvariantCulture, out z))
                    throw new JsonException("Invalid w in Vector4 string notation.");
            }
            else
                throw new JsonException("String notation for Vector4 must provide 4 float elements.");
        }
        else
            throw new JsonException("Unexpected token " + reader.TokenType + " for reading Vector4.");

        return new Vector4(x, y, z, w);
    }
    public override void Write(Utf8JsonWriter writer, Vector4 value, JsonSerializerOptions options)
    {
        using JsonIndent indent = writer.StopIndenting();
        writer.WriteStartArray();
        writer.WriteNumberValue(value.x);
        writer.WriteNumberValue(value.y);
        writer.WriteNumberValue(value.z);
        writer.WriteNumberValue(value.w);
        writer.WriteEndArray();
    }
}