using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DevkitServer.Configuration.Converters;
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
        writer.WriteStringValue("#" + (value.a < 1f ? ColorUtility.ToHtmlStringRGBA(value) : ColorUtility.ToHtmlStringRGB(value)).ToLowerInvariant());
    }
}