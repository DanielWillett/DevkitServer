using Newtonsoft.Json;
using System;
using System.Globalization;
using UnityEngine;

namespace DanielWillett.LevelObjectIcons.Converters;


/// <inheritdoc />
public class Vector3Converter : JsonConverter
{
    private static readonly char[] SplitChars = { ',' };
    /// <inheritdoc />
    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        Vector3 v3 = (Vector3)value;

        Formatting originalFormatting = writer.Formatting;
        writer.Formatting = Formatting.None;

        writer.WriteStartArray();
        writer.WriteValue(v3.x);
        writer.WriteValue(v3.y);
        writer.WriteValue(v3.z);
        writer.WriteEndArray();

        writer.Formatting = originalFormatting;
    }
    /// <inheritdoc />
    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        float x = 0f, y = 0f, z = 0f;
        if (reader.TokenType == JsonToken.StartArray)
        {
            int ct = 0;
            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.EndArray)
                {
                    if (ct == 3)
                        break;
                    throw new JsonSerializationException("Array notation for Vector3 must provide 3 float elements.");
                }
                if (reader.TokenType is JsonToken.Integer or JsonToken.Float)
                {
                    float val = Convert.ToSingle(reader.Value);
                    switch (++ct)
                    {
                        case 1:
                            x = val;
                            break;
                        case 2:
                            y = val;
                            break;
                        case 3:
                            z = val;
                            break;
                    }
                }
                else throw new JsonSerializationException("Invalid number in Vector3 array notation.");
            }
        }
        else if (reader.TokenType == JsonToken.StartObject)
        {
            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.EndObject)
                    break;
                if (reader.TokenType == JsonToken.PropertyName)
                {
                    string? prop = (string)reader.Value;
                    if (reader.Read() && prop is not null)
                    {
                        if (prop.Equals("x", StringComparison.InvariantCultureIgnoreCase))
                        {
                            if (reader.TokenType is JsonToken.Integer or JsonToken.Float)
                                x = Convert.ToSingle(reader.Value);
                            else
                                throw new JsonSerializationException("Invalid x in Vector3 object notation.");
                        }
                        else if (prop.Equals("y", StringComparison.InvariantCultureIgnoreCase))
                        {
                            if (reader.TokenType is JsonToken.Integer or JsonToken.Float)
                                y = Convert.ToSingle(reader.Value);
                            else
                                throw new JsonSerializationException("Invalid y in Vector3 object notation.");
                        }
                        else if (prop.Equals("z", StringComparison.InvariantCultureIgnoreCase))
                        {
                            if (reader.TokenType is JsonToken.Integer or JsonToken.Float)
                                z = Convert.ToSingle(reader.Value);
                            else
                                throw new JsonSerializationException("Invalid z in Vector3 object notation.");
                        }
                    }
                }
            }
        }
        else if (reader.TokenType == JsonToken.String)
        {
            string[] strs = ((string)reader.Value).Split(SplitChars, StringSplitOptions.RemoveEmptyEntries);
            if (strs.Length == 3)
            {
                if (!float.TryParse(strs[0].Replace('(', ' '), NumberStyles.Number, CultureInfo.InvariantCulture, out x))
                    throw new JsonSerializationException("Invalid x in Vector3 string notation.");
                if (!float.TryParse(strs[1], NumberStyles.Number, CultureInfo.InvariantCulture, out y))
                    throw new JsonSerializationException("Invalid y in Vector3 string notation.");
                if (!float.TryParse(strs[2].Replace(')', ' '), NumberStyles.Number, CultureInfo.InvariantCulture, out z))
                    throw new JsonSerializationException("Invalid z in Vector3 string notation.");
            }
            else
                throw new JsonSerializationException("String notation for Vector3 must provide 3 float elements.");
        }
        else
            throw new JsonSerializationException("Unexpected token " + reader.TokenType + " for reading Vector3.");

        return new Vector3(x, y, z);
    }

    /// <inheritdoc />
    public override bool CanConvert(Type objectType) => objectType == typeof(Vector3);
}
