using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DevkitServer.Configuration.Converters;
public sealed class QuaternionEulerPreferredJsonConverter : QuaternionJsonConverter
{
    public QuaternionEulerPreferredJsonConverter() => WriteEuler = true;
}
public class QuaternionJsonConverter : JsonConverter<Quaternion>
{
    public bool WriteEuler { get; protected set; }
    private static readonly char[] SplitChars = { ',' };
    public override Quaternion Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        float x = 0f, y = 0f, z = 0f, w = 0f;
        bool euler = true;
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            int ct = 0;
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                {
                    if (ct == 3)
                        break;
                    throw new JsonException("Array notation for Quaternion must provide 3 or 4 float elements.");
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
                            euler = false;
                            break;
                    }
                }
                else throw new JsonException("Invalid number in Quaternion array notation.");
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
                                throw new JsonException("Invalid x in Quaternion object notation.");
                        }
                        else if (prop.Equals("y", StringComparison.InvariantCultureIgnoreCase))
                        {
                            if (reader.TokenType == JsonTokenType.Number && reader.TryGetSingle(out float fl))
                                y = fl;
                            else
                                throw new JsonException("Invalid y in Quaternion object notation.");
                        }
                        else if (prop.Equals("z", StringComparison.InvariantCultureIgnoreCase))
                        {
                            if (reader.TokenType == JsonTokenType.Number && reader.TryGetSingle(out float fl))
                                z = fl;
                            else
                                throw new JsonException("Invalid z in Quaternion object notation.");
                        }
                        else if (prop.Equals("w", StringComparison.InvariantCultureIgnoreCase))
                        {
                            euler = false;
                            if (reader.TokenType == JsonTokenType.Number && reader.TryGetSingle(out float fl))
                                w = fl;
                            else
                                throw new JsonException("Invalid w in Quaternion object notation.");
                        }
                    }
                }
            }
        }
        else if (reader.TokenType == JsonTokenType.String)
        {
            string[] strs = reader.GetString()!.Split(SplitChars, StringSplitOptions.RemoveEmptyEntries);
            if (strs.Length is 3 or 4)
            {
                if (!float.TryParse(strs[0].Replace('(', ' '), NumberStyles.Number, CultureInfo.InvariantCulture, out x))
                    throw new JsonException("Invalid x in Quaternion string notation.");
                if (!float.TryParse(strs[1], NumberStyles.Number, CultureInfo.InvariantCulture, out y))
                    throw new JsonException("Invalid y in Quaternion string notation.");
                if (!float.TryParse(strs[2].Replace(')', ' '), NumberStyles.Number, CultureInfo.InvariantCulture, out z))
                    throw new JsonException("Invalid z in Quaternion string notation.");
                if (strs.Length == 4)
                {
                    if (!float.TryParse(strs[3].Replace(')', ' '), NumberStyles.Number, CultureInfo.InvariantCulture, out w))
                        throw new JsonException("Invalid w in Quaternion string notation.");

                    euler = false;
                }
            }
            else
                throw new JsonException("String notation for Quaternion must provide 3 or 4 float elements.");
        }
        else
            throw new JsonException("Unexpected token " + reader.TokenType + " for reading Vector3.");

        return euler ? Quaternion.Euler(x, y, z) : new Quaternion(x, y, z, w);
    }
    public override void Write(Utf8JsonWriter writer, Quaternion value, JsonSerializerOptions options)
    {
        using JsonIndent indent = writer.StopIndenting();
        writer.WriteStartArray();
        if (WriteEuler)
        {
            Vector3 euler = value.eulerAngles;
            writer.WriteNumberValue(euler.x);
            writer.WriteNumberValue(euler.y);
            writer.WriteNumberValue(euler.z);
        }
        else
        {
            writer.WriteNumberValue(value.x);
            writer.WriteNumberValue(value.y);
            writer.WriteNumberValue(value.z);
            writer.WriteNumberValue(value.w);
        }
        writer.WriteEndArray();
    }
}