using System.Text.Json;

namespace DevkitServer.Configuration;

/// <summary>
/// Extension methods for <see cref="Utf8JsonWriter"/>.
/// </summary>
public static class Utf8JsonWriterEx
{
    /// <summary>
    /// Stops indenting until disposed. Use this with a <see langword="using"/> statement.
    /// </summary>
    public static JsonIndent StopIndenting(this Utf8JsonWriter writer)
    {
        return JsonIndent.SetOptions == null || !writer.Options.Indented ? default : new JsonIndent(writer, false);
    }

    /// <summary>
    /// Starts indenting until disposed. Use this with a <see langword="using"/> statement.
    /// </summary>
    public static JsonIndent StartIndenting(this Utf8JsonWriter writer)
    {
        return JsonIndent.SetOptions == null || writer.Options.Indented ? default : new JsonIndent(writer, true);
    }
}