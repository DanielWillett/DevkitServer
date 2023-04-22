using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using DevkitServer.Configuration;

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