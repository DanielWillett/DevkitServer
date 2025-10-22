using Newtonsoft.Json;
using System;
using System.Globalization;
using UnityEngine;

namespace DanielWillett.LevelObjectIcons.Converters;

/// <inheritdoc />
public class QuaternionEulerConverter : QuaternionConverter
{
    /// <inheritdoc />
    public QuaternionEulerConverter()
    {
        WriteEuler = true;
    }
}

/// <inheritdoc />
public class QuaternionConverter : JsonConverter
{
    /// <summary>
    /// Write the values in Euler form instead of Quaternion.
    /// </summary>
    protected bool WriteEuler;
    private static readonly char[] SplitChars = { ',' };
    /// <inheritdoc />
    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        Quaternion quaternion = (Quaternion)value;

        Formatting originalFormatting = writer.Formatting;
        writer.Formatting = Formatting.None;

        writer.WriteStartArray();
        if (WriteEuler)
        {
            Vector3 euler = quaternion.eulerAngles;
            writer.WriteValue(euler.x);
            writer.WriteValue(euler.y);
            writer.WriteValue(euler.z);
        }
        else
        {
            writer.WriteValue(quaternion.x);
            writer.WriteValue(quaternion.y);
            writer.WriteValue(quaternion.z);
            writer.WriteValue(quaternion.w);
        }
        writer.WriteEndArray();

        writer.Formatting = originalFormatting;
    }
    /// <inheritdoc />
    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        float x = 0f, y = 0f, z = 0f, w = 0f;
        bool euler = true;
        if (reader.TokenType == JsonToken.StartArray)
        {
            int ct = 0;
            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.EndArray)
                {
                    if (ct is 3 or 4)
                        break;
                    throw new JsonSerializationException("Array notation for Quaternion must provide 3 (euler) or 4 float elements.");
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
                        case 4:
                            w = val;
                            euler = false;
                            break;
                    }
                }
                else throw new JsonSerializationException("Invalid number in Quaternion array notation.");
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
                                throw new JsonSerializationException("Invalid x in Quaternion object notation.");
                        }
                        else if (prop.Equals("y", StringComparison.InvariantCultureIgnoreCase))
                        {
                            if (reader.TokenType is JsonToken.Integer or JsonToken.Float)
                                y = Convert.ToSingle(reader.Value);
                            else
                                throw new JsonSerializationException("Invalid y in Quaternion object notation.");
                        }
                        else if (prop.Equals("z", StringComparison.InvariantCultureIgnoreCase))
                        {
                            if (reader.TokenType is JsonToken.Integer or JsonToken.Float)
                                z = Convert.ToSingle(reader.Value);
                            else
                                throw new JsonSerializationException("Invalid z in Quaternion object notation.");
                        }
                        else if (prop.Equals("w", StringComparison.InvariantCultureIgnoreCase))
                        {
                            if (reader.TokenType is JsonToken.Integer or JsonToken.Float)
                            {
                                w = Convert.ToSingle(reader.Value);
                                euler = false;
                            }
                            else
                                throw new JsonSerializationException("Invalid w in Quaternion object notation.");
                        }
                    }
                }
            }
        }
        else if (reader.TokenType == JsonToken.String)
        {
            string[] strs = ((string)reader.Value).Split(SplitChars, StringSplitOptions.RemoveEmptyEntries);
            if (strs.Length is 3 or 4)
            {
                if (!float.TryParse(strs[0].Replace('(', ' '), NumberStyles.Number, CultureInfo.InvariantCulture, out x))
                    throw new JsonSerializationException("Invalid x in Quaternion string notation.");
                if (!float.TryParse(strs[1], NumberStyles.Number, CultureInfo.InvariantCulture, out y))
                    throw new JsonSerializationException("Invalid y in Quaternion string notation.");
                if (!float.TryParse(strs.Length == 3 ? strs[2].Replace(')', ' ') : strs[2], NumberStyles.Number, CultureInfo.InvariantCulture, out z))
                    throw new JsonSerializationException("Invalid z in Quaternion string notation.");
                if (strs.Length == 4)
                {
                    if (!float.TryParse(strs[3].Replace(')', ' '), NumberStyles.Number, CultureInfo.InvariantCulture, out w))
                        throw new JsonSerializationException("Invalid w in Quaternion string notation.");

                    euler = false;
                }
            }
            else
                throw new JsonSerializationException("String notation for Quaternion must provide 3 (euler) or 4 float elements.");
        }
        else
            throw new JsonSerializationException("Unexpected token " + reader.TokenType + " for reading Quaternion.");

        return euler ? Quaternion.Euler(x, y, z) : new Quaternion(x, y, z, w);
    }

    /// <inheritdoc />
    public override bool CanConvert(Type objectType) => objectType == typeof(Quaternion);
}
