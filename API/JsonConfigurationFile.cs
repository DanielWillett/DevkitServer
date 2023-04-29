using DevkitServer.Configuration;
using SDG.Framework.Devkit;
using System.Text.Json;

namespace DevkitServer.API;
public class JsonConfigurationFile<TConfig> : IJsonSettingProvider, IConfigProvider<TConfig> where TConfig : class, new()
{
    public virtual TConfig? Default => null;
    public TConfig Configuration
    {
        get => _config;
        set
        {
            lock (_sync)
            {
                _config = value;
            }
        }
    }

    private TConfig _config = null!;
    private readonly object _sync = new object();
    private string _file = null!;
    public JsonReaderOptions ReaderOptions { get; set; } = DevkitServerConfig.ReaderOptions;
    public JsonWriterOptions WriterOptions { get; set; } = DevkitServerConfig.WriterOptions;
    public JsonSerializerOptions SerializerOptions { get; set; } = DevkitServerConfig.SerializerSettings;
    public string File
    {
        get => _file;
        set
        {
            lock (_sync)
            {
                _file = value;
            }
        }
    }
    public JsonConfigurationFile(string file)
    {
        File = file;
    }
    public void ReloadConfig()
    {
        lock (_sync)
        {
            _config = ReadFromFile(File, this, Default);
            WriteToFile(File, _config);
        }
    }
    public void SaveConfig()
    {
        lock (_sync)
        {
            WriteToFile(File, _config, this);
        }
    }
    public static TConfig? WriteToFile(string path, TConfig config, IJsonSettingProvider? options = null)
    {
        try
        {
            if (Path.GetDirectoryName(path) is { } dir)
                Directory.CreateDirectory(dir);
            using FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            if (config == null)
            {
                config = new TConfig();
                if (config is IDefaultable def)
                    def.SetDefaults();
            }
            Utf8JsonWriter writer = new Utf8JsonWriter(fs, options == null ? DevkitServerConfig.WriterOptions : options.WriterOptions);
            JsonSerializer.Serialize(writer, config, options == null ? DevkitServerConfig.SerializerSettings : options.SerializerOptions);
            return config;
        }
        catch (Exception ex)
        {
            Logger.LogError("[" + typeof(TConfig).Format() + "] Error writing config file: \"" + path + "\".");
            Logger.LogError(ex);
        }

        return null;
    }
    public static TConfig ReadFromFile(string path, IJsonSettingProvider? options = null, TConfig? @default = null)
    {
        TConfig config;
        try
        {
            if (System.IO.File.Exists(path))
            {
                using FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                int len = (int)Math.Min(fs.Length, int.MaxValue);
                byte[] bytes = new byte[len];
                int l = fs.Read(bytes, 0, len);
                if (l != len)
                {
                    byte[] bytes2 = bytes;
                    bytes = new byte[l];
                    Buffer.BlockCopy(bytes2, 0, bytes, 0, Math.Min(l, len));
                    Array.Resize(ref bytes, l);
                }

                if (len > 0)
                {
                    Utf8JsonReader reader = new Utf8JsonReader(bytes, DevkitServerConfig.ReaderOptions);
                    config = JsonSerializer.Deserialize<TConfig>(ref reader,
                               options == null
                                   ? DevkitServerConfig.SerializerSettings
                                   : options.SerializerOptions)
                           ?? throw new JsonException("Failed to read SystemConfig: returned null.");
                    if (config is IDirtyable dirty2)
                        dirty2.isDirty = false;
                }
            }

            if (@default != null)
                return @default;
        }
        catch (Exception ex)
        {
            Logger.LogError("[" + typeof(TConfig).Format() + "] Error reading config file: \"" + path + "\".");
            Logger.LogError(ex);

            string oldpath = path;
            try
            {
                int c = 0;
                do
                {
                    ++c;
                    path = Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileNameWithoutExtension(path) + "_backup_" + c + Path.GetExtension(path));
                }
                while (System.IO.File.Exists(path));
                System.IO.File.Move(oldpath, path);
            }
            catch (Exception ex2)
            {
                Logger.LogError("[" + typeof(TConfig).Format() + "] Error backing up invalid config file from: \"" + oldpath + "\" to \"" + path + "\".");
                Logger.LogError(ex2);
            }
#if SERVER
            Logger.LogWarning("[" + typeof(TConfig).Format() + "] Server startup halted. " +
                              "fix the config errors in \"" + path + "\" and rename it to \"" + oldpath + "\" or retart the server to use the default config.");
            DevkitServerModule.Fault();
#endif
            if (@default != null)
                return @default;
        }
        config = new TConfig();
        if (config is IDefaultable def)
            def.SetDefaults();
        if (config is IDirtyable dirty)
            dirty.isDirty = false;
        return config;
    }
}
