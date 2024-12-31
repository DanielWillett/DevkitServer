using System.Text.Json;

namespace DevkitServer.Configuration;

public delegate bool ReadTopLevelPropertiesHandler<TState>(ref Utf8JsonReader reader, string propertyName, ref TState state);

/// <summary>
/// Extension methods for <see cref="Utf8JsonWriter"/>.
/// </summary>
public static class Utf8JsonReaderEx
{
    /// <summary>
    /// Find all top-level properties on the current object.
    /// </summary>
    public static int ReadTopLevelProperties(this ref Utf8JsonReader reader, ReadTopLevelPropertiesHandler<object?> action)
    {
        object? state = null;
        return ReadTopLevelProperties(ref reader, ref state, action);
    }

    /// <summary>
    /// Find all top-level properties on the current object.
    /// </summary>
    public static int ReadTopLevelProperties<TState>(this ref Utf8JsonReader reader, ref TState state, ReadTopLevelPropertiesHandler<TState> action)
    {
        int objectLevel = 0;
        int arrayLevel = 0;
        int propCount = 0;

        if (reader.TokenType != JsonTokenType.PropertyName && !reader.Read())
            return 0;

        do
        {
            if (reader.TokenType == JsonTokenType.PropertyName && objectLevel <= 0 && arrayLevel <= 0)
            {
                string property = reader.GetString()!;
                if (!reader.Read())
                    return propCount;

                ++propCount;
                if (!action(ref reader, property, ref state))
                    return propCount;
            }

            if (propCount == 0)
                continue;

            switch (reader.TokenType)
            {
                case JsonTokenType.StartObject:
                    ++objectLevel;
                    break;

                case JsonTokenType.StartArray:
                    ++arrayLevel;
                    break;

                case JsonTokenType.EndObject:
                    --objectLevel;
                    break;

                case JsonTokenType.EndArray:
                    --arrayLevel;
                    break;
            }

            if (objectLevel < 0)
                break;
        }
        while (reader.Read());

        return propCount;
    }
}