using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DevkitServer.Configuration;
public class DevkitServerConfig
{
    private static readonly object Sync = new object();

    private static readonly JavaScriptEncoder Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;

    private static readonly JsonConverter[] Converters =
    {
        new Vector3JsonConverter(),
        new Vector2JsonConverter(),
        new ColorJsonConverter(),
        new Color32JsonConverter(),
        new CSteamIDJsonConverter(),
        new AssetReferenceJsonConverterFactory()
    };

    internal static readonly InstanceSetter<Utf8JsonWriter, JsonWriterOptions>? SetWriterOptions = Accessor.GenerateInstanceSetter<Utf8JsonWriter, JsonWriterOptions>("_options");

    public static readonly JsonSerializerOptions SerializerSettings = new JsonSerializerOptions
    {
        WriteIndented = true,
        IncludeFields = true,
        AllowTrailingCommas = true,
        Encoder = Encoder,
        MaxDepth = 32
    };
    public static readonly JsonSerializerOptions CondensedSerializerSettings = new JsonSerializerOptions
    {
        WriteIndented = false,
        IncludeFields = true,
        AllowTrailingCommas = true,
        Encoder = Encoder,
        MaxDepth = 32
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
    public static readonly string FilePath = Path.Combine(UnturnedPaths.RootDirectory.FullName, "DevkitServer");
    public static readonly string ConfigFilePath = Path.Combine(FilePath, "client_config.json");
#else
    public static readonly string FilePath = Path.Combine(UnturnedPaths.RootDirectory.FullName, "DevkitServer", Provider.serverID);
    public static readonly string ConfigFilePath = Path.Combine(FilePath, "server_config.json");
#endif

    private static SystemConfig? _config;
    public static SystemConfig Config
    {
        get
        {
            lock (Sync)
            {
                if (_config == null)
                    Reload();
                return _config!;
            }
        }
    }
    public static void Reset()
    {
        Save();
    }
    public static void Reload()
    {
        _config = Read();
        Save();
    }
    public static void Save()
    {
        lock (Sync)
        {
            try
            {
                string path = ConfigFilePath;
                if (Path.GetDirectoryName(path) is { } dir)
                    Directory.CreateDirectory(dir);
                using FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
                SystemConfig? config = _config;
                if (config == null)
                {
                    config = new SystemConfig();
                    config.SetDefaults();
                }
                Utf8JsonWriter writer = new Utf8JsonWriter(fs, WriterOptions);
                JsonSerializer.Serialize(writer, config, SerializerSettings);
                _config = config;
            }
            catch (Exception ex)
            {
                Logger.LogError("Error writing config file: \"" + ConfigFilePath + "\".");
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
                string path = ConfigFilePath;
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
                Logger.LogError("Error reading config file: \"" + ConfigFilePath + "\".");
                Logger.LogError(ex);

                try
                {
                    int c = 0;
                    string path;
                    do
                    {
                        ++c;
                        path = Path.Combine(Path.GetDirectoryName(ConfigFilePath)!, Path.GetFileNameWithoutExtension(ConfigFilePath) + "_backup_" + c + Path.GetExtension(ConfigFilePath));
                    }
                    while (File.Exists(path));
                    File.Copy(ConfigFilePath, path);
                }
                catch (Exception ex2)
                {
                    Logger.LogError("Error backing up config file from: \"" + ConfigFilePath + "\".");
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
    [JsonPropertyName("extended_visual_ansi_support")]
    public bool ConsoleExtendedVisualANSISupport { get; set; }

    [JsonPropertyName("visual_ansi_support")]
    public bool ConsoleVisualANSISupport { get; set; }

#if SERVER
    [JsonPropertyName("disable_map_download")]
    public bool DisableMapDownload { get; set; }

    [JsonPropertyName("high_speed")]
    public TcpServerInfo? TcpSettings;

    public class TcpServerInfo
    {
        [JsonPropertyName("enable_high_speed_support")]
        public bool EnableHighSpeedSupport { get; set; }

        [JsonPropertyName("high_speed_tcp_port")]
        public ushort HighSpeedPort { get; set; }
    }
#endif

    public void SetDefaults()
    {
        ConsoleExtendedVisualANSISupport = true;
        ConsoleVisualANSISupport = true;
#if SERVER
        DisableMapDownload = false;
        TcpSettings = new TcpServerInfo { EnableHighSpeedSupport = false, HighSpeedPort = (ushort)(Provider.port + 2) };
#endif
    }
}