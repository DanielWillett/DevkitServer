using System.Text.Json;

namespace DevkitServer.API;
public interface IConfigProvider<out TConfig> where TConfig : class, new()
{
    TConfig Configuration { get; }
    void ReloadConfig();
    void SaveConfig();
}
public interface IJsonSettingProvider
{
    JsonReaderOptions ReaderOptions { get; }
    JsonWriterOptions WriterOptions { get; }
    JsonSerializerOptions SerializerOptions { get; }
}