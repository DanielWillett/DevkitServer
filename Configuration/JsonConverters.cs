using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Mono.Cecil;

namespace DevkitServer.Configuration;
public sealed class Vector3JsonConverter : JsonConverter<Vector3>
{
    private static readonly char[] SplitChars = { ',' };
    public override Vector3 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        float x = 0f, y = 0f, z = 0f;
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            int ct = 0;
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                {
                    if (ct == 3)
                        break;
                    throw new JsonException("Array notation for Vector3 must provide 3 float elements.");
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
                    }
                }
                else throw new JsonException("Invalid number in Vector3 array notation.");
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
                                throw new JsonException("Invalid x in Vector3 object notation.");
                        }
                        else if (prop.Equals("y", StringComparison.InvariantCultureIgnoreCase))
                        {
                            if (reader.TokenType == JsonTokenType.Number && reader.TryGetSingle(out float fl))
                                y = fl;
                            else
                                throw new JsonException("Invalid y in Vector3 object notation.");
                        }
                        else if (prop.Equals("z", StringComparison.InvariantCultureIgnoreCase))
                        {
                            if (reader.TokenType == JsonTokenType.Number && reader.TryGetSingle(out float fl))
                                z = fl;
                            else
                                throw new JsonException("Invalid z in Vector3 object notation.");
                        }
                    }
                }
            }
        }
        else if (reader.TokenType == JsonTokenType.String)
        {
            string[] strs = reader.GetString()!.Split(SplitChars, StringSplitOptions.RemoveEmptyEntries);
            if (strs.Length == 3)
            {
                if (!float.TryParse(strs[0].Replace('(', ' '), NumberStyles.Number, CultureInfo.InvariantCulture, out x))
                    throw new JsonException("Invalid x in Vector3 string notation.");
                if (!float.TryParse(strs[1], NumberStyles.Number, CultureInfo.InvariantCulture, out y))
                    throw new JsonException("Invalid y in Vector3 string notation.");
                if (!float.TryParse(strs[2].Replace(')', ' '), NumberStyles.Number, CultureInfo.InvariantCulture, out z))
                    throw new JsonException("Invalid z in Vector3 string notation.");
            }
            else
                throw new JsonException("String notation for Vector3 must provide 3 float elements.");
        }
        else
            throw new JsonException("Unexpected token " + reader.TokenType + " for reading Vector3.");

        return new Vector3(x, y, z);
    }
    public override void Write(Utf8JsonWriter writer, Vector3 value, JsonSerializerOptions options)
    {
        JsonWriterOptions opt2 = writer.Options;
        if (opt2.Indented && DevkitServerConfig.SetWriterOptions != null)
            DevkitServerConfig.SetWriterOptions(writer, opt2 with { Indented = false });
        writer.WriteStartArray();
        writer.WriteNumberValue(value.x);
        writer.WriteNumberValue(value.y);
        writer.WriteNumberValue(value.z);
        writer.WriteEndArray();
        if (opt2.Indented && DevkitServerConfig.SetWriterOptions != null)
            DevkitServerConfig.SetWriterOptions(writer, opt2);
    }
}
public sealed class QuaternionJsonConverter : JsonConverter<Quaternion>
{
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
        JsonWriterOptions opt2 = writer.Options;
        if (opt2.Indented && DevkitServerConfig.SetWriterOptions != null)
            DevkitServerConfig.SetWriterOptions(writer, opt2 with { Indented = false });
        writer.WriteStartArray();
        writer.WriteNumberValue(value.x);
        writer.WriteNumberValue(value.y);
        writer.WriteNumberValue(value.z);
        writer.WriteNumberValue(value.w);
        writer.WriteEndArray();
        if (opt2.Indented && DevkitServerConfig.SetWriterOptions != null)
            DevkitServerConfig.SetWriterOptions(writer, opt2);
    }
}
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
        JsonWriterOptions opt2 = writer.Options;
        if (opt2.Indented && DevkitServerConfig.SetWriterOptions != null)
            DevkitServerConfig.SetWriterOptions(writer, opt2 with { Indented = false });
        writer.WriteStartArray();
        writer.WriteNumberValue(value.x);
        writer.WriteNumberValue(value.y);
        writer.WriteEndArray();
        if (opt2.Indented && DevkitServerConfig.SetWriterOptions != null)
            DevkitServerConfig.SetWriterOptions(writer, opt2);
    }
}
public sealed class ColorJsonConverter : JsonConverter<Color>
{
    private static readonly char[] SplitChars = { ',' };
    public override Color Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        float r = 0f, g = 0f, b = 0f, a = 1f;
        bool hsv = false;
        bool hdr = false;
        bool rgb = false;
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            int ct = 0;
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                {
                    if (ct is 3 or 4)
                        break;
                    throw new JsonException("Array notation for Color must provide 3 or 4 float elements.");
                }
                if (reader.TokenType == JsonTokenType.Number && reader.TryGetSingle(out float fl))
                {
                    switch (++ct)
                    {
                        case 1:
                            r = fl;
                            break;
                        case 2:
                            g = fl;
                            break;
                        case 3:
                            b = fl;
                            break;
                        case 4:
                            a = fl;
                            break;
                    }
                }
                else throw new JsonException("Invalid number in Color array notation.");
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
                        if (prop.Equals("r", StringComparison.InvariantCultureIgnoreCase) ||
                            prop.Equals("red", StringComparison.InvariantCultureIgnoreCase))
                        {
                            if (hsv)
                                throw new JsonException("HSV and RGB not supported in Color object notation.");
                            rgb = true;
                            if (reader.TokenType == JsonTokenType.Number && reader.TryGetSingle(out float fl))
                                r = fl;
                            else
                                throw new JsonException("Invalid red channel in Color object notation.");
                        }
                        else if (prop.Equals("g", StringComparison.InvariantCultureIgnoreCase) ||
                                 prop.Equals("green", StringComparison.InvariantCultureIgnoreCase))
                        {
                            if (hsv)
                                throw new JsonException("HSV and RGB not supported in Color object notation.");
                            rgb = true;
                            if (reader.TokenType == JsonTokenType.Number && reader.TryGetSingle(out float fl))
                                g = fl;
                            else
                                throw new JsonException("Invalid green channel in Color object notation.");
                        }
                        else if (prop.Equals("b", StringComparison.InvariantCultureIgnoreCase) ||
                                 prop.Equals("blue", StringComparison.InvariantCultureIgnoreCase))
                        {
                            if (hsv)
                                throw new JsonException("HSV and RGB not supported in Color object notation.");
                            rgb = true;
                            if (reader.TokenType == JsonTokenType.Number && reader.TryGetSingle(out float fl))
                                b = fl;
                            else
                                throw new JsonException("Invalid blue channel in Color object notation.");
                        }
                        else if (prop.Equals("h", StringComparison.InvariantCultureIgnoreCase) ||
                                 prop.Equals("hue", StringComparison.InvariantCultureIgnoreCase))
                        {
                            if (rgb)
                                throw new JsonException("HSV and RGB not supported in Color object notation.");
                            hsv = true;
                            if (reader.TokenType == JsonTokenType.Number && reader.TryGetSingle(out float fl))
                                r = fl;
                            else
                                throw new JsonException("Invalid hue channel in Color object notation.");
                        }
                        else if (prop.Equals("h", StringComparison.InvariantCultureIgnoreCase) ||
                                 prop.Equals("saturation", StringComparison.InvariantCultureIgnoreCase) ||
                                 prop.Equals("sat", StringComparison.InvariantCultureIgnoreCase))
                        {
                            if (rgb)
                                throw new JsonException("HSV and RGB not supported in Color object notation.");
                            hsv = true;
                            if (reader.TokenType == JsonTokenType.Number && reader.TryGetSingle(out float fl))
                                g = fl;
                            else
                                throw new JsonException("Invalid saturation channel in Color object notation.");
                        }
                        else if (prop.Equals("v", StringComparison.InvariantCultureIgnoreCase) ||
                                 prop.Equals("value", StringComparison.InvariantCultureIgnoreCase) ||
                                 prop.Equals("val", StringComparison.InvariantCultureIgnoreCase))
                        {
                            if (rgb)
                                throw new JsonException("HSV and RGB not supported in Color object notation.");
                            hsv = true;
                            if (reader.TokenType == JsonTokenType.Number && reader.TryGetSingle(out float fl))
                                b = fl;
                            else
                                throw new JsonException("Invalid value channel in Color object notation.");
                        }
                        else if (prop.Equals("hdr", StringComparison.InvariantCultureIgnoreCase) ||
                                 prop.Equals("high-dynamic-range", StringComparison.InvariantCultureIgnoreCase) ||
                                 prop.Equals("dynamic-range", StringComparison.InvariantCultureIgnoreCase))
                        {
                            if (reader.TokenType == JsonTokenType.True)
                                hdr = true;
                            else if (reader.TokenType != JsonTokenType.False)
                                throw new JsonException("Invalid value channel in Color object notation.");
                            else hdr = false;
                        }
                        else if (prop.Equals("a", StringComparison.InvariantCultureIgnoreCase) ||
                                 prop.Equals("alpha", StringComparison.InvariantCultureIgnoreCase) ||
                                 prop.Equals("transparency", StringComparison.InvariantCultureIgnoreCase))
                        {
                            if (reader.TokenType == JsonTokenType.Number && reader.TryGetSingle(out float fl))
                                a = fl;
                            else
                                throw new JsonException("Invalid alpha channel in Color object notation.");
                        }
                    }
                }
            }
        }
        else if (reader.TokenType == JsonTokenType.String)
        {
            string str = reader.GetString()!;
            if (str.Length > 0 && str[0] == '#')
            {
                if (ColorUtility.TryParseHtmlString(str, out Color color))
                    return color;

                throw new JsonException("String notation for Color must provide 3 or 4 float elements.");
            }
            string[] strs = str.Split(SplitChars, StringSplitOptions.RemoveEmptyEntries);
            if (strs.Length is 3 or 4)
            {
                if (strs[0].StartsWith("hsv")) hsv = true;
                int ind = strs[0].IndexOf('(');
                if (ind != -1 && strs[0].Length > ind + 1) strs[0] = strs[0].Substring(ind + 1);
                if (!float.TryParse(strs[0], NumberStyles.Number, CultureInfo.InvariantCulture, out r))
                    throw new JsonException("Invalid " + (hsv ? "hue" : "red") + " channel in Color string notation.");
                if (!float.TryParse(strs[1], NumberStyles.Number, CultureInfo.InvariantCulture, out g))
                    throw new JsonException("Invalid " + (hsv ? "saturation" : "green") + " channel in Color string notation.");
                if (!float.TryParse(strs[2].Replace(')', ' '), NumberStyles.Number, CultureInfo.InvariantCulture, out b))
                    throw new JsonException("Invalid " + (hsv ? "value" : "blue") + " channel in Color string notation.");
                if (strs.Length > 3 && !float.TryParse(strs[3].Replace(')', ' '), NumberStyles.Number, CultureInfo.InvariantCulture, out a))
                    throw new JsonException("Invalid alpha channel in Color string notation.");
            }
            else if (ColorUtility.TryParseHtmlString("#" + str, out Color color))
                return color;
            else
                throw new JsonException("String notation for Color must provide 3 or 4 float elements or a \"#ffffff\" notation.");
        }
        else
            throw new JsonException("Unexpected token " + reader.TokenType + " for reading Color.");

        a = Mathf.Clamp01(a);

        if (hsv)
            return Color.HSVToRGB(r, g, b, hdr) with { a = a };
        
        if (!hdr)
        {
            r = Mathf.Clamp01(r);
            g = Mathf.Clamp01(g);
            b = Mathf.Clamp01(b);
        }
        return new Color(r, g, b, a);
    }
    public override void Write(Utf8JsonWriter writer, Color value, JsonSerializerOptions options)
    {
        JsonWriterOptions opt2 = writer.Options;
        if (opt2.Indented && DevkitServerConfig.SetWriterOptions != null)
            DevkitServerConfig.SetWriterOptions(writer, opt2 with { Indented = false });
        writer.WriteStartArray();
        writer.WriteNumberValue(value.r);
        writer.WriteNumberValue(value.g);
        writer.WriteNumberValue(value.b);
        if (value.a < 1f)
            writer.WriteNumberValue(value.a);
        writer.WriteEndArray();
        if (opt2.Indented && DevkitServerConfig.SetWriterOptions != null)
            DevkitServerConfig.SetWriterOptions(writer, opt2);
    }
}
public sealed class Color32JsonConverter : JsonConverter<Color32>
{
    private static readonly char[] SplitChars = { ',' };
    public override Color32 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        byte r = 0, g = 0, b = 0, a = byte.MaxValue;
        bool hsv = false;
        bool rgb = false;
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            int ct = 0;
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                {
                    if (ct is 3 or 4)
                        break;
                    throw new JsonException("Array notation for Color32 must provide 3 or 4 float elements.");
                }
                if (reader.TokenType == JsonTokenType.Number && reader.TryGetByte(out byte bt))
                {
                    switch (++ct)
                    {
                        case 1:
                            r = bt;
                            break;
                        case 2:
                            g = bt;
                            break;
                        case 3:
                            b = bt;
                            break;
                        case 4:
                            a = bt;
                            break;
                    }
                }
                else throw new JsonException("Invalid number in Color32 array notation.");
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
                        if (prop.Equals("r", StringComparison.InvariantCultureIgnoreCase) ||
                            prop.Equals("red", StringComparison.InvariantCultureIgnoreCase))
                        {
                            if (hsv)
                                throw new JsonException("HSV and RGB not supported in Color32 object notation.");
                            rgb = true;
                            if (reader.TokenType == JsonTokenType.Number && reader.TryGetByte(out byte bt))
                                r = bt;
                            else
                                throw new JsonException("Invalid red channel in Color32 object notation.");
                        }
                        else if (prop.Equals("g", StringComparison.InvariantCultureIgnoreCase) ||
                                 prop.Equals("green", StringComparison.InvariantCultureIgnoreCase))
                        {
                            if (hsv)
                                throw new JsonException("HSV and RGB not supported in Color32 object notation.");
                            rgb = true;
                            if (reader.TokenType == JsonTokenType.Number && reader.TryGetByte(out byte bt))
                                g = bt;
                            else
                                throw new JsonException("Invalid green channel in Color32 object notation.");
                        }
                        else if (prop.Equals("b", StringComparison.InvariantCultureIgnoreCase) ||
                                 prop.Equals("blue", StringComparison.InvariantCultureIgnoreCase))
                        {
                            if (hsv)
                                throw new JsonException("HSV and RGB not supported in Color32 object notation.");
                            rgb = true;
                            if (reader.TokenType == JsonTokenType.Number && reader.TryGetByte(out byte bt))
                                b = bt;
                            else
                                throw new JsonException("Invalid blue channel in Color32 object notation.");
                        }
                        else if (prop.Equals("h", StringComparison.InvariantCultureIgnoreCase) ||
                                 prop.Equals("hue", StringComparison.InvariantCultureIgnoreCase))
                        {
                            if (rgb)
                                throw new JsonException("HSV and RGB not supported in Color32 object notation.");
                            hsv = true;
                            if (reader.TokenType == JsonTokenType.Number && reader.TryGetByte(out byte bt))
                                r = bt;
                            else
                                throw new JsonException("Invalid hue channel in Color32 object notation.");
                        }
                        else if (prop.Equals("h", StringComparison.InvariantCultureIgnoreCase) ||
                                 prop.Equals("saturation", StringComparison.InvariantCultureIgnoreCase) ||
                                 prop.Equals("sat", StringComparison.InvariantCultureIgnoreCase))
                        {
                            if (rgb)
                                throw new JsonException("HSV and RGB not supported in Color32 object notation.");
                            hsv = true;
                            if (reader.TokenType == JsonTokenType.Number && reader.TryGetByte(out byte bt))
                                g = bt;
                            else
                                throw new JsonException("Invalid saturation channel in Color32 object notation.");
                        }
                        else if (prop.Equals("v", StringComparison.InvariantCultureIgnoreCase) ||
                                 prop.Equals("value", StringComparison.InvariantCultureIgnoreCase) ||
                                 prop.Equals("val", StringComparison.InvariantCultureIgnoreCase))
                        {
                            if (rgb)
                                throw new JsonException("HSV and RGB not supported in Color32 object notation.");
                            hsv = true;
                            if (reader.TokenType == JsonTokenType.Number && reader.TryGetByte(out byte bt))
                                b = bt;
                            else
                                throw new JsonException("Invalid value channel in Color32 object notation.");
                        }
                        else if (prop.Equals("a", StringComparison.InvariantCultureIgnoreCase) ||
                                 prop.Equals("alpha", StringComparison.InvariantCultureIgnoreCase) ||
                                 prop.Equals("transparency", StringComparison.InvariantCultureIgnoreCase))
                        {
                            if (reader.TokenType == JsonTokenType.Number && reader.TryGetByte(out byte bt))
                                a = bt;
                            else
                                throw new JsonException("Invalid alpha channel in Color32 object notation.");
                        }
                    }
                }
            }
        }
        else if (reader.TokenType == JsonTokenType.String)
        {
            string str = reader.GetString()!;
            if (str.Length > 0 && str[0] == '#')
            {
                if (ColorUtility.TryParseHtmlString(str, out Color color))
                    return color;

                throw new JsonException("String notation for Color32 must provide 3 or 4 float elements.");
            }
            string[] strs = str.Split(SplitChars, StringSplitOptions.RemoveEmptyEntries);
            if (strs.Length is 3 or 4)
            {
                if (strs[0].StartsWith("hsv")) hsv = true;
                int ind = strs[0].IndexOf('(');
                if (ind != -1 && strs[0].Length > ind + 1) strs[0] = strs[0].Substring(ind + 1);
                if (!byte.TryParse(strs[0], NumberStyles.Number, CultureInfo.InvariantCulture, out r))
                    throw new JsonException("Invalid " + (hsv ? "hue" : "red") + " channel in Color32 string notation.");
                if (!byte.TryParse(strs[1], NumberStyles.Number, CultureInfo.InvariantCulture, out g))
                    throw new JsonException("Invalid " + (hsv ? "saturation" : "green") + " channel in Color32 string notation.");
                if (!byte.TryParse(strs[2].Replace(')', ' '), NumberStyles.Number, CultureInfo.InvariantCulture, out b))
                    throw new JsonException("Invalid " + (hsv ? "value" : "blue") + " channel in Color32 string notation.");
                if (strs.Length > 3 && !byte.TryParse(strs[3].Replace(')', ' '), NumberStyles.Number, CultureInfo.InvariantCulture, out a))
                    throw new JsonException("Invalid alpha channel in Color32 string notation.");
            }
            else if (ColorUtility.TryParseHtmlString("#" + str, out Color color))
                return color;
            else
                throw new JsonException("String notation for Color32 must provide 3 or 4 float elements or a \"#ffffff\" notation.");
        }
        else
            throw new JsonException("Unexpected token " + reader.TokenType + " for reading Color32.");

        if (hsv)
            return Color.HSVToRGB(r, g, b, true) with { a = a };
        
        return new Color32(r, g, b, a);
    }
    public override void Write(Utf8JsonWriter writer, Color32 value, JsonSerializerOptions options)
    {
        JsonWriterOptions opt2 = writer.Options;
        if (opt2.Indented && DevkitServerConfig.SetWriterOptions != null)
            DevkitServerConfig.SetWriterOptions(writer, opt2 with { Indented = false });
        writer.WriteStartArray();
        writer.WriteNumberValue(value.r);
        writer.WriteNumberValue(value.g);
        writer.WriteNumberValue(value.b);
        if (value.a < 1f)
            writer.WriteNumberValue(value.a);
        writer.WriteEndArray();
        if (opt2.Indented && DevkitServerConfig.SetWriterOptions != null)
            DevkitServerConfig.SetWriterOptions(writer, opt2);
    }
}
public sealed class AssetReferenceJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(AssetReference<>);
    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        return (JsonConverter?)Activator.CreateInstance(typeof(AssetReferenceJsonConverter<>).MakeGenericType(typeToConvert.GetGenericArguments()[0]));
    }
}
public sealed class AssetReferenceJsonConverter<TAsset> : JsonConverter<AssetReference<TAsset>> where TAsset : Asset
{
    public static AssetReferenceJsonConverter<TAsset> Instance { get; } = new AssetReferenceJsonConverter<TAsset>();
    public override AssetReference<TAsset> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return new AssetReference<TAsset>();
            case JsonTokenType.Number:
                if (reader.TryGetUInt16(out ushort id))
                {
                    try
                    {
                        return new AssetReference<TAsset>(Assets.find(AssetTypeHelper<TAsset>.Type, id) is { } asset ? asset.GUID : default);
                    }
                    catch (Exception ex)
                    {
                        throw new JsonException("Tried to read a UInt16 formatted asset reference before assets were loaded.", ex);
                    }
                }

                throw new JsonException("Failed to read a UInt16 formatted asset reference.");
            case JsonTokenType.String:
                string str = reader.GetString()!;
                if (!Guid.TryParse(str, out Guid guid))
                    throw new JsonException("Failed to read a Guid formatted asset reference.");

                return new AssetReference<TAsset>(guid);
            case JsonTokenType.StartObject:
                Guid? guid2 = null;
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndObject)
                        break;
                    if (reader.TokenType == JsonTokenType.PropertyName)
                    {
                        string? prop = reader.GetString();
                        if (reader.Read() && prop != null && "GUID".Equals(prop, StringComparison.InvariantCultureIgnoreCase) || "_guid".Equals(prop, StringComparison.InvariantCultureIgnoreCase))
                        {
                            if (reader.TokenType == JsonTokenType.String)
                            {
                                str = reader.GetString()!;
                                if (Guid.TryParse(str, out guid))
                                {
                                    guid2 = guid;
                                    continue;
                                }
                            }
                            throw new JsonException("Failed to read a Guid-object formatted asset reference.");
                        }
                    }
                }

                if (guid2.HasValue)
                    return new AssetReference<TAsset>(guid2.Value);

                throw new JsonException("Failed to read an object formatted asset reference.");

            default:
                throw new JsonException("Unknown " + typeof(TAsset).Name + " token: " + reader.TokenType + ".");
        }
    }

    public override void Write(Utf8JsonWriter writer, AssetReference<TAsset> value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.GUID.ToString("N"));
    }
}
public sealed class CSteamIDJsonConverter : JsonConverter<CSteamID>
{
    public override CSteamID Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return CSteamID.Nil;

            case JsonTokenType.String:
                string str = reader.GetString()!;
                if (!DevkitServerUtility.TryParseSteamId(str, out CSteamID steamId))
                    throw new JsonException("Unknown string notation for CSteamID. Valid formats: Steam64 ID, Steam2 ID, Steam3 ID, common strings.");
                return steamId;

            case JsonTokenType.Number:
                if (reader.TryGetUInt64(out ulong steam64))
                    return new CSteamID(steam64);
                throw new JsonException("Unknown numeric (64-bit) notation for CSteamID. Valid formats: Steam64 ID, Steam2 ID, Steam3 ID, preset strings.");

            case JsonTokenType.StartObject:
                ulong? csteamid = null;
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndObject)
                        break;
                    if (reader.TokenType == JsonTokenType.PropertyName)
                    {
                        string? prop = reader.GetString();
                        if (reader.Read() && prop != null && "m_SteamID".Equals(prop, StringComparison.InvariantCultureIgnoreCase) || "id".Equals(prop, StringComparison.InvariantCultureIgnoreCase))
                        {
                            if (reader.TokenType == JsonTokenType.String)
                            {
                                str = reader.GetString()!;
                                if (!DevkitServerUtility.TryParseSteamId(str, out steamId))
                                {
                                    csteamid = steamId.m_SteamID;
                                    continue;
                                }
                            }
                            else if (reader.TokenType == JsonTokenType.Number)
                            {
                                if (reader.TryGetUInt64(out ulong id))
                                {
                                    csteamid = id;
                                    continue;
                                }
                            }
                            else if (reader.TokenType == JsonTokenType.Null)
                            {
                                csteamid = 0;
                                continue;
                            }
                            throw new JsonException("Failed to read a Guid-object formatted asset reference.");
                        }
                    }
                }

                if (csteamid.HasValue)
                    return new CSteamID(csteamid.Value);

                throw new JsonException("Unknown object-numeric (64-bit) notation for CSteamID. Valid formats: Steam64 ID, Steam2 ID, Steam3 ID, preset strings.");

            default:
                throw new JsonException("Unknown CSteamID token: " + reader.TokenType + ".");
        }
    }
    public override void Write(Utf8JsonWriter writer, CSteamID value, JsonSerializerOptions options)
    {
        if (value.m_SteamID == default)
            writer.WriteNullValue();
        else if (value.m_SteamID == CSteamID.OutofDateGS.m_SteamID)
            writer.WriteStringValue("out-of-date-gs");
        else if (value.m_SteamID == CSteamID.LanModeGS.m_SteamID)
            writer.WriteStringValue("lan-mode-gs");
        else if (value.m_SteamID == CSteamID.NotInitYetGS.m_SteamID)
            writer.WriteStringValue("not-init-yet-gs");
        else if (value.m_SteamID == CSteamID.NonSteamGS.m_SteamID)
            writer.WriteStringValue("non-steam-gs");
        else
            writer.WriteNumberValue(value.m_SteamID);
    }
}
public sealed class TypeJsonConverter : JsonConverter<Type>
{
    public override Type? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;
            case JsonTokenType.String:
                string? str = reader.GetString();
                if (string.IsNullOrEmpty(str) || str!.Equals("null", StringComparison.InvariantCultureIgnoreCase) || str.Equals("<null>", StringComparison.InvariantCultureIgnoreCase))
                    return null;
                Type? type = Type.GetType(str, false, true);
                if (type == null)
                {
                    type = Assembly.GetExecutingAssembly().GetType(str, false, true);
                    if (type == null)
                        throw new JsonException("Unknown type: \"" + str + "\".");
                }

                return type;
            default:
                throw new JsonException("Unexpected token " + reader.TokenType + " in JSON type converter.");
        }
    }

    public override void Write(Utf8JsonWriter writer, Type value, JsonSerializerOptions options)
    {
        if (value == null)
            writer.WriteNullValue();
        else if (value.Assembly == Assembly.GetExecutingAssembly())
            writer.WriteStringValue(value.FullName);
        else
            writer.WriteStringValue(value.AssemblyQualifiedName);
    }
}