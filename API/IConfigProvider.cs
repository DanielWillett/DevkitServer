using System.Text.Json;

namespace DevkitServer.API;

/// <summary>
/// Provides a configuration implementation with reload and save methods.
/// </summary>
public interface IConfigProvider<out TConfig> where TConfig : class, new()
{
    TConfig Configuration { get; }
    void ReloadConfig();
    void SaveConfig();
}

/// <summary>
/// Provides config for JSON serialization.
/// </summary>
public interface IJsonSettingProvider
{
    JsonReaderOptions ReaderOptions { get; }
    JsonWriterOptions WriterOptions { get; }
    JsonSerializerOptions SerializerOptions { get; }
}