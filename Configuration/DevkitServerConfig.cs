﻿using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DevkitServer.Configuration;
public class DevkitServerConfig
{
    private static readonly object Sync = new object();

    private static readonly JavaScriptEncoder Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;

    private static readonly JsonConverter[] Converters =
    {
        new Vector3JsonConverter()
    };

    internal static readonly InstanceSetter<Utf8JsonWriter, JsonWriterOptions>? SetWriterOptions = Accessor.GenerateInstanceSetter<Utf8JsonWriter, JsonWriterOptions>("_options");

    public static readonly JsonSerializerOptions SerializerSettings = new JsonSerializerOptions
    {
        WriteIndented = true,
        IncludeFields = true,
        AllowTrailingCommas = true,
        Encoder = Encoder
    };
    public static readonly JsonSerializerOptions CondensedSerializerSettings = new JsonSerializerOptions
    {
        WriteIndented = false,
        IncludeFields = true,
        AllowTrailingCommas = true,
        Encoder = Encoder
    };

    public static readonly JsonWriterOptions WriterOptions = new JsonWriterOptions { Indented = true, Encoder = Encoder };
    public static readonly JsonWriterOptions CondensedWriterOptions = new JsonWriterOptions { Indented = false, Encoder = Encoder };
    public static readonly JsonReaderOptions ReaderOptions = new JsonReaderOptions { AllowTrailingCommas = true };
    static DevkitServerConfig()
    {
        for (int i = 0; i < Converters.Length; ++i)
        {
            JsonConverter converter = Converters[i];
            SerializerSettings.Converters.Add(converter);
            CondensedSerializerSettings.Converters.Add(converter);
        }
    }

#if CLIENT
    public static readonly string FilePath = Path.Combine(UnturnedPaths.RootDirectory.FullName, "DevkitServer", "client_config.json");
#else
    public static readonly string FilePath = Path.Combine(UnturnedPaths.RootDirectory.FullName, "DevkitServer", Provider.serverID, "server_config.json");
#endif

    private static SystemConfig? _client;
    public static SystemConfig Config
    {
        get
        {
            lock (Sync)
            {
                if (_client == null)
                    Reload();
                return _client!;
            }
        }
    }
    public static void Reset()
    {
        Save();
    }
    public static void Reload()
    {
        _client = Read();
        Save();
    }
    public static void Save()
    {
        lock (Sync)
        {
            try
            {
                string path = FilePath;
                if (Path.GetDirectoryName(path) is { } dir)
                    Directory.CreateDirectory(dir);
                using FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
                SystemConfig? config = _client;
                if (config == null)
                {
                    config = new SystemConfig();
                    config.SetDefaults();
                }
                Utf8JsonWriter writer = new Utf8JsonWriter(fs, WriterOptions);
                JsonSerializer.Serialize(writer, config, SerializerSettings);
                _client = config;
            }
            catch (Exception ex)
            {
                Logger.LogError("Error writing config file: \"" + FilePath + "\".");
                Logger.LogError(ex);
            }
        }
    }

    private static SystemConfig Read()
    {
        ThreadUtil.assertIsGameThread();

        lock (Sync)
        {
            try
            {
                string path = FilePath;
                if (File.Exists(path))
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
                        Utf8JsonReader reader = new Utf8JsonReader(bytes, ReaderOptions);
                        return JsonSerializer.Deserialize<SystemConfig>(ref reader, SerializerSettings) ?? throw new JsonException("Failed to read SystemConfig: returned null.");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error reading config file: \"" + FilePath + "\".");
                Logger.LogError(ex);

                try
                {
                    int c = 0;
                    string path;
                    do
                    {
                        ++c;
                        path = Path.Combine(Path.GetDirectoryName(FilePath)!, Path.GetFileNameWithoutExtension(FilePath) + "_backup_" + c + Path.GetExtension(FilePath));
                    }
                    while (File.Exists(path));
                    File.Copy(FilePath, path);
                }
                catch (Exception ex2)
                {
                    Logger.LogError("Error backing up config file from: \"" + FilePath + "\".");
                    Logger.LogError(ex2);
                }
#if SERVER
                Logger.LogWarning("Server startup halted... fix the config errors or retart the server to use a clean config.");
                DevkitServerModule.Fault();
#endif
            }
            SystemConfig config = new SystemConfig();
            config.SetDefaults();
            return config;
        }
    }
}
public class SystemConfig
{
    [JsonPropertyName("disable_map_download")]
    public bool DisableMapDownload { get; set; }

    public void SetDefaults()
    {
        DisableMapDownload = false;
    }
}