using DevkitServer.API.Permissions;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using DevkitServer.API;


#if CLIENT
using System.Globalization;
#endif

namespace DevkitServer.Configuration;
[EarlyTypeInit(-1)]
public class DevkitServerConfig
{
    private const string Source = "CONFIG";
    private static readonly object Sync = new object();

    private static readonly JavaScriptEncoder Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;

    private static readonly JsonConverter[] Converters =
    {
        new QuaternionJsonConverter(),
        new Vector4JsonConverter(),
        new Vector3JsonConverter(),
        new Vector2JsonConverter(),
        new ColorJsonConverter(),
        new Color32JsonConverter(),
        new CSteamIDJsonConverter(),
        new TypeJsonConverter(),
        new PermissionConverter(),
        new PermissionGroupConverter(),
        new GroupPermissionConverter(),
        new AssetReferenceJsonConverterFactory()
    };

    internal static readonly InstanceSetter<Utf8JsonWriter, JsonWriterOptions>? SetWriterOptions = Accessor.GenerateInstanceSetter<Utf8JsonWriter, JsonWriterOptions>("_options");

    public static readonly JsonSerializerOptions SerializerSettings = new JsonSerializerOptions
    {
        WriteIndented = true,
        IncludeFields = true,
        AllowTrailingCommas = true,
        Encoder = Encoder,
        MaxDepth = 32,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals | JsonNumberHandling.AllowReadingFromString
    };
    public static readonly JsonSerializerOptions CondensedSerializerSettings = new JsonSerializerOptions
    {
        WriteIndented = false,
        IncludeFields = true,
        AllowTrailingCommas = true,
        Encoder = Encoder,
        MaxDepth = 32,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals | JsonNumberHandling.AllowReadingFromString
    };

    public static readonly JsonWriterOptions WriterOptions = new JsonWriterOptions { Indented = true, Encoder = Encoder, MaxDepth = 32, SkipValidation = false };
    public static readonly JsonWriterOptions CondensedWriterOptions = new JsonWriterOptions { Indented = false, Encoder = Encoder, MaxDepth = 32, SkipValidation = false };
    public static readonly JsonReaderOptions ReaderOptions = new JsonReaderOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip, MaxDepth = 32 };
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
    internal static string? SeverFolderIntl;
    [CreateDirectory]
    public static readonly string Directory = Path.Combine(UnturnedPaths.RootDirectory.FullName, "DevkitServer");
    public static readonly string ConfigFilePath = Path.Combine(Directory, "client_config.json");
    public static string ServerFolder => SeverFolderIntl ??= Path.Combine(Directory, "Temp_" + Parser.getIPFromUInt32(Provider.currentServerInfo.ip) + "_" + Provider.currentServerInfo.connectionPort.ToString(CultureInfo.InvariantCulture));
#else
    [CreateDirectory]
    public static readonly string Directory = Path.Combine(UnturnedPaths.RootDirectory.FullName, "Servers", Provider.serverID, "DevkitServer");

    public static readonly string ConfigFilePath = Path.Combine(Directory, "server_config.json");
    public static readonly string PermissionGroupsPath = Path.Combine(Directory, "permission_groups.json");
    public static string ServerFolder = Directory;
#endif

    [CreateDirectory]
    public static readonly string TempFolder = Path.Combine(Directory, "Temp");

    private static string? _lvl;
    private static LevelInfo? _lvlInfo;
    public static string LevelDirectory
    {
        get
        {
            LevelInfo? lvl = Level.info;
            if (_lvl != null && _lvlInfo == lvl) return _lvl;
            _lvlInfo = lvl;
            if (lvl != null)
            {
#if SERVER
                return _lvl = Path.Combine(Directory, "Levels", lvl.name);
#else
                return _lvl = Path.Combine(ServerFolder, "Levels", lvl.name);
#endif
            }

            throw new NotSupportedException("Level not loaded");
        }
    }

    [CreateDirectory]
    public static readonly string BundlesFolder = Path.Combine(UnturnedPaths.RootDirectory.FullName, "Modules", DevkitServerModule.ModuleName, "Bundles");
    [CreateDirectory]
    public static readonly string LocalizationFilePath = Path.Combine(Directory, "Localization");
    [CreateDirectory]
    public static readonly string CommandLocalizationFilePath = Path.Combine(LocalizationFilePath, "Commands");

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
    public static void ResetToDefaults()
    {
        (_config ??= new SystemConfig()).SetDefaults();
        Save();
    }
    public static void Reload()
    {
        _config = Read();
        // Save();
    }
    public static void Save()
    {
        lock (Sync)
        {
            try
            {
                string path = ConfigFilePath;
                if (Path.GetDirectoryName(path) is { } dir)
                    System.IO.Directory.CreateDirectory(dir);
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
                Logger.LogError("Error writing config file: \"" + ConfigFilePath + "\".", method: Source);
                Logger.LogError(ex, method: Source);
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
                try
                {
                    if (!File.Exists(path))
                    {
#if SERVER
                        string? modulePath = DevkitServerModule.FindModuleFile("server_config.json");
#else
                        string? modulePath = DevkitServerModule.FindModuleFile("client_config.json");
#endif
                        if (modulePath != null)
                        {
                            string? dir = Path.GetDirectoryName(path);
                            if (dir != null)
                                System.IO.Directory.CreateDirectory(dir);
                            File.Copy(modulePath, path, false);

                            if (DevkitServerModule.InitializedLogging)
                                Logger.LogInfo($"[{Source}] Copied default config file from: \"{modulePath}\".");
                            else
                                CommandWindow.Log($"[{Source}] Copied default config file from: \"{modulePath}\".");
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (DevkitServerModule.InitializedLogging)
                    {
                        Logger.LogError("Error copying default config file: \"" + ConfigFilePath + "\".", method: Source);
                        Logger.LogError(ex, method: Source);
                    }
                    else
                    {
                        CommandWindow.LogError("Error copying default config file: \"" + ConfigFilePath + "\".");
                        CommandWindow.LogError(ex);
                    }
                }
                if (File.Exists(path))
                {
                    using FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                    DevkitServerUtility.AdvancePastUTF8Bom(fs);
                    int len = (int)Math.Min(fs.Length - fs.Position, int.MaxValue);
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
                if (DevkitServerModule.InitializedLogging)
                {
                    Logger.LogError("Error reading config file: \"" + ConfigFilePath + "\".", method: Source);
                    Logger.LogError(ex, method: Source);
                }
                else
                {
                    CommandWindow.LogError("Error reading config file: \"" + ConfigFilePath + "\".");
                    CommandWindow.LogError(ex);
                }

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
                    if (DevkitServerModule.InitializedLogging)
                    {
                        Logger.LogError("Error backing up config file from: \"" + ConfigFilePath + "\".", method: Source);
                        Logger.LogError(ex2, method: Source);
                    }
                    else
                    {
                        CommandWindow.LogError("Error backing up config file from: \"" + ConfigFilePath + "\".");
                        CommandWindow.LogError(ex);
                    }
                }
#if SERVER
                if (DevkitServerModule.InitializedLogging)
                    Logger.LogWarning("Server startup halted... fix the config errors or retart the server to use a clean config.", method: Source);
                else
                    CommandWindow.LogWarning("Server startup halted... fix the config errors or retart the server to use a clean config.");

                DevkitServerModule.Fault();
#endif
            }

            SystemConfig config = new SystemConfig();
            config.SetDefaults();
            return config;
        }
    }
    /// <summary>
    /// Recursively deletes all files and folders in <see cref="TempFolder"/>.
    /// </summary>
    public static void ClearTempFolder()
    {
        try
        {
            string tempFolder = TempFolder;
            if (!System.IO.Directory.Exists(tempFolder))
            {
                System.IO.Directory.CreateDirectory(tempFolder);
                return;
            }
            DirectoryInfo dir = new DirectoryInfo(tempFolder);
            TryDeleteRecursive(dir, false);
        }
        catch (Exception ex)
        {
            Logger.LogError("Error clearing temp folder.", method: Source);
            Logger.LogError(ex, method: Source);
        }
    }
    private static bool TryDeleteRecursive(DirectoryInfo dir, bool del)
    {
        try
        {
            bool anyFail = false;
            foreach (FileSystemInfo entry in dir.EnumerateFileSystemInfos("*", SearchOption.TopDirectoryOnly))
            {
                if (entry is DirectoryInfo dir2)
                {
                    anyFail |= TryDeleteRecursive(dir2, true);
                }
                else if (entry is FileInfo file)
                {
                    try
                    {
                        file.Delete();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($" • Error deleting file: {file.FullName.Format(true)}.", method: Source);
                        Logger.LogError($"   + {ex.GetType().Format()} - {ex.Message.Format(false)}{(ex.Message.EndsWith(".") ? string.Empty : ".")}", method: Source);
                        anyFail = true;
                    }
                }
            }

            if (!anyFail && del)
                dir.Delete(false);
            
            return anyFail;
        }
        catch (Exception ex)
        {
            Logger.LogError($" • Error deleting folder: {dir.FullName.Format(true)}.", method: Source);
            Logger.LogError($"   + {ex.GetType().Format()} - {ex.Message.Format(false)}{(ex.Message.EndsWith(".") ? string.Empty : ".")}", method: Source);
            return true;
        }
    }
}
public class SystemConfig : SchemaConfiguration
{
#if SERVER
    public override string SchemaURI => DevkitServerModule.GetRelativeRepositoryUrl("Module/Schemas/server_config_schema.json", true);
#else
    public override string SchemaURI => DevkitServerModule.GetRelativeRepositoryUrl("Module/Schemas/client_config_schema.json", true);
#endif

#nullable disable
    [JsonPropertyName("extended_visual_ansi_support")]
    public bool ConsoleExtendedVisualANSISupport { get; set; }

    [JsonPropertyName("visual_ansi_support")]
    public bool ConsoleVisualANSISupport { get; set; }

    [JsonPropertyName("walmart_pc_support")]
    public bool RemoveCosmeticImprovements { get; set; }

    [JsonPropertyName("ansi_log")]
    public bool ANSILog { get; set; }

#if CLIENT
    [JsonPropertyName("enable_object_ui_extension")]
    public bool EnableObjectUIExtension { get; set; }

    [JsonPropertyName("enable_better_map_creation")]
    public bool EnableBetterLevelCreation { get; set; }
#endif

#if SERVER
    [JsonPropertyName("new_level_info")]
    public NewLevelCreationOptions NewLevelInfo { get; set; }

    [JsonPropertyName("disable_map_download")]
    public bool DisableMapDownload { get; set; }

    [JsonPropertyName("default_permissions")]
    public string[] DefaultUserPermissions { get; set; }

    [JsonPropertyName("default_permission_groups")]
    public string[] DefaultUserPermissionGroups { get; set; }

    [JsonPropertyName("high_speed")]
    public TcpServerInfo TcpSettings;
#endif
#nullable restore
#if SERVER

    [JsonPropertyName("user_savedata_override")]
    public string? UserSavedataLocationOverride { get; set; }

#endif
    public void SetDefaults()
    {
        ConsoleExtendedVisualANSISupport = true;
        ConsoleVisualANSISupport = true;
        RemoveCosmeticImprovements = false;
#if CLIENT
        EnableObjectUIExtension = true;
        EnableBetterLevelCreation = true;
#endif
#if SERVER
        NewLevelInfo = NewLevelCreationOptions.Default;
        DisableMapDownload = false;
        TcpSettings = new TcpServerInfo { EnableHighSpeedSupport = false, HighSpeedPort = (ushort)(Provider.port + 2) };
        DefaultUserPermissions = Array.Empty<string>();
        DefaultUserPermissionGroups = new string[]
        {
            "viewer"
        };
        UserSavedataLocationOverride = null;
#endif
    }
#if SERVER
    public class TcpServerInfo
    {
        [JsonPropertyName("enable_high_speed_support")]
        public bool EnableHighSpeedSupport { get; set; }

        [JsonPropertyName("high_speed_tcp_port")]
        public ushort HighSpeedPort { get; set; }
    }
    public class NewLevelCreationOptions
    {
        public static readonly NewLevelCreationOptions Default = new NewLevelCreationOptions
        {
            LevelSize = ELevelSize.MEDIUM,
            LevelType = ELevelType.SURVIVAL,
            Owner = CSteamID.Nil
        };

        [JsonConverter(typeof(JsonStringEnumConverter))]
        [JsonPropertyName("gamemode_type")]
        public ELevelType LevelType { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        [JsonPropertyName("start_size")]
        public ELevelSize LevelSize { get; set; }

        [JsonConverter(typeof(CSteamIDJsonConverter))]
        [JsonPropertyName("map_owner")]
        public CSteamID Owner { get; set; }
    }
#endif
}