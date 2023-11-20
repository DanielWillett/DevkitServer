using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DevkitServer.Configuration.Converters;
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
        string str = "#" + value.r.ToString("X2", CultureInfo.InvariantCulture) + value.g.ToString("X2", CultureInfo.InvariantCulture) + value.b.ToString("X2", CultureInfo.InvariantCulture);
        if (value.a != byte.MaxValue)
            str += value.a.ToString("X2", CultureInfo.InvariantCulture);

        writer.WriteStringValue(str.ToLowerInvariant());
    }
}