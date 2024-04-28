using DanielWillett.ReflectionTools;
using DevkitServer.API;
using System.Text.Json;

namespace DevkitServer.Configuration;

/// <summary>
/// Represents a temporarily indented or not indented section of JSON while using a <see cref="Utf8JsonWriter"/>.
/// </summary>
/// <remarks>Use with <see cref="Utf8JsonWriterEx.StartIndenting"/> and <see cref="Utf8JsonWriterEx.StopIndenting"/>.</remarks>
[EarlyTypeInit]
public readonly struct JsonIndent : IDisposable
{
    internal static readonly InstanceSetter<Utf8JsonWriter, JsonWriterOptions>? SetOptions = Accessor.GenerateInstanceSetter<Utf8JsonWriter, JsonWriterOptions>("_options");

    /// <summary>
    /// Initial value of the indent setting.
    /// </summary>
    public readonly bool StartValue;

    /// <summary>
    /// Active writer for this indent section.
    /// </summary>
    public readonly Utf8JsonWriter? Writer;
    internal JsonIndent(Utf8JsonWriter writer, bool isIndented)
    {
        Writer = writer;
        StartValue = writer.Options.Indented;
        if (StartValue != isIndented)
            SetOptions?.Invoke(writer, writer.Options with { Indented = StartValue });
    }

    /// <summary>
    /// Revert your changes to the indent setting of a <see cref="Utf8JsonWriter"/>.
    /// </summary>
    public void Dispose()
    {
        if (Writer != null && Writer.Options.Indented != StartValue)
            SetOptions?.Invoke(Writer, Writer.Options with { Indented = StartValue });
    }
}