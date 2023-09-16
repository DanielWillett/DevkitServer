using DevkitServer.API.Abstractions;
using DevkitServer.Util.Encoding;

namespace DevkitServer.API;

/// <summary>
/// Stores read/writable information about a translation: a key, source, and arguments.
/// </summary>
public readonly struct TranslationData
{
    /// <summary>
    /// Translation key used to find the translation value.
    /// </summary>
    public string TranslationKey { get; }
    /// <summary>
    /// Source of the translation, could be an item, file, etc. Use <see cref="TranslationSource"/> to create them.
    /// </summary>
    public ITranslationSource Source { get; }
    /// <summary>
    /// Formatting argumens, similar to how <see cref="string.Format"/> works. Use <see cref="Array.Empty"/> if you dont need to pass any formatting parameters.
    /// </summary>
    public object?[] FormattingArguments { get; }

    /// <summary>
    /// Passes a translation key and source with no formatting.
    /// </summary>
    public TranslationData(string translationKey, ITranslationSource source)
    {
        TranslationKey = translationKey ?? throw new ArgumentNullException(nameof(translationKey));
        Source = source ?? throw new ArgumentNullException(nameof(source));
        FormattingArguments = Array.Empty<object>();
    }

    /// <summary>
    /// Passes a translation key and source with formatting arguments.
    /// </summary>
    public TranslationData(string translationKey, ITranslationSource source, params object?[] formatting)
    {
        TranslationKey = translationKey ?? throw new ArgumentNullException(nameof(translationKey));
        Source = source ?? throw new ArgumentNullException(nameof(source));
        FormattingArguments = formatting is not { Length: > 0 } ? Array.Empty<object>() : formatting;
    }

    /// <summary>
    /// Read translation data from a <see cref="ByteReader"/>.
    /// </summary>
    public TranslationData(ByteReader reader)
    {
        TranslationKey = reader.ReadString();
        Source = TranslationSource.Read(reader)!;
        FormattingArguments = reader.ReadFormattingParameters();
    }

    /// <summary>
    /// Read translation data from a <see cref="ByteReader"/>.
    /// </summary>
    public static TranslationData Read(ByteReader reader) => new TranslationData(reader);

    /// <summary>
    /// Write translation data to a <see cref="ByteWriter"/>.
    /// </summary>
    public static void Write(ByteWriter writer, TranslationData data)
    {
        writer.Write(data.TranslationKey);
        TranslationSource.Write(writer, data.Source);
        writer.WriteFormattingParameters(data.FormattingArguments ?? Array.Empty<object>());
    }

    /// <summary>
    /// Translates the value.
    /// </summary>
    public string GetLocalTranslation()
    {
        if (Source == null)
            return TranslationKey ?? "#NAME";

        return Source.Translate(TranslationKey, (FormattingArguments ?? Array.Empty<object>())!);
    }
}
