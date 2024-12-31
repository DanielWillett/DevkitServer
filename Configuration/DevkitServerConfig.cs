using DevkitServer.API;
using DevkitServer.API.Permissions;
using DevkitServer.Configuration.Converters;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
#if CLIENT
using DevkitServer.Multiplayer;
#endif

namespace DevkitServer.Configuration;

[EarlyTypeInit(-1)]
public class DevkitServerConfig
{
    private const string Source = "CONFIG";
    private static readonly object Sync = new object();

    private static readonly JavaScriptEncoder Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;

    private static readonly JsonConverter[] Converters =
    [
        new QuaternionJsonConverter(),
        new Vector4JsonConverter(),
        new Vector3JsonConverter(),
        new Vector2JsonConverter(),
        new ColorJsonConverter(),
        new Color32JsonConverter(),
        new CSteamIDJsonConverter(),
        new TypeJsonConverter(),
        new PermissionLeafConverter(),
        new PermissionBranchConverter(),
        new PermissionGroupConverter(),
        new AssetReferenceJsonConverterFactory(),
        new TimeSpanConverter(),
        new NetIdJsonConverter(),
        new NetId64JsonConverter()
    ];

    public static readonly JsonSerializerOptions SerializerSettings = new JsonSerializerOptions
    {
        WriteIndented = true,
        AllowTrailingCommas = true,
        Encoder = Encoder,
        MaxDepth = 32,
        ReadCommentHandling = JsonCommentHandling.Skip
    };
    public static readonly JsonSerializerOptions CondensedSerializerSettings = new JsonSerializerOptions
    {
        WriteIndented = false,
        AllowTrailingCommas = true,
        Encoder = Encoder,
        MaxDepth = 32,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public static readonly JsonWriterOptions WriterOptions = new JsonWriterOptions { Indented = true, Encoder = Encoder, SkipValidation = false };
    public static readonly JsonWriterOptions CondensedWriterOptions = new JsonWriterOptions { Indented = false, Encoder = Encoder, SkipValidation = false };
    public static readonly JsonReaderOptions ReaderOptions = new JsonReaderOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip, MaxDepth = 32 };
    public static readonly JsonDocumentOptions DocumentSettings = new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip, MaxDepth = 32 };
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
    public static readonly string Directory = Path.Combine(DevkitServerModule.UnturnedLoaded ? UnturnedPaths.RootDirectory.FullName : FileUtil.DesktopOrCurrentDir, "DevkitServer");
    public static readonly string ConfigFilePath = Path.Combine(Directory, "client_config.json");
    public static string ServerFolder => SeverFolderIntl ??= Path.Combine(Directory, "Temp_" + FileUtil.GetServerUniqueFileName());
#else
    [CreateDirectory]
    public static readonly string Directory = DevkitServerModule.UnturnedLoaded ? Path.Combine(UnturnedPaths.RootDirectory.FullName, "Servers", Provider.serverID, "DevkitServer") : Path.Combine(FileUtil.DesktopOrCurrentDir, "DevkitServer");

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

    private static DevkitServerSystemConfig? _config;
    public static DevkitServerSystemConfig Config
    {
        get
        {
            DevkitServerSystemConfig? cfg = _config;
            if (cfg != null)
                return cfg;

            lock (Sync)
            {
                cfg = _config;
                if (cfg == null)
                    _config = cfg = Read();
                
                return cfg;
            }
        }
    }

#if CLIENT
    public static bool RemoveCosmeticImprovements => DevkitServerModule.IsEditing && ClientInfo.Info != null ? ClientInfo.Info.ServerRemovesCosmeticImprovements : Config.RemoveCosmeticImprovements;
#else
    public static bool RemoveCosmeticImprovements => Config.RemoveCosmeticImprovements;
#endif
    public static void ResetToDefaults()
    {
        (_config ??= new DevkitServerSystemConfig()).SetDefaults();
        Save();
    }
    public static void Reload()
    {
        lock (Sync)
            _config = Read();
        // Save();
    }
    public static void Save()
    {
        lock (Sync)
            Write();
    }
    private static void Write()
    {
        try
        {
            string path = ConfigFilePath;
            if (Path.GetDirectoryName(path) is { } dir)
                System.IO.Directory.CreateDirectory(dir);
            using FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            DevkitServerSystemConfig? config = _config;
            if (config == null)
            {
                config = new DevkitServerSystemConfig();
                config.SetDefaults();
            }
            Utf8JsonWriter writer = new Utf8JsonWriter(fs, WriterOptions);
            JsonSerializer.Serialize(writer, config, SerializerSettings);
            _config = config;
        }
        catch (Exception ex)
        {
            if (DevkitServerModule.InitializedLogging)
            {
                Logger.DevkitServer.LogError(Source, ex, $"Error writing config file: {ConfigFilePath.Format()}.");
            }
            else
            {
                CommandWindow.LogError($"[{Source}] Error writing config file: \"{ConfigFilePath}\".");
                CommandWindow.LogError(ex);
            }
        }
    }

    private static DevkitServerSystemConfig Read()
    {
        ThreadUtil.assertIsGameThread();

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
                            Logger.DevkitServer.LogInfo(Source, $"Copied default config file from: {modulePath.Format()}.");
                        else
                            CommandWindow.Log($"[{Source}] Copied default config file from: \"{modulePath}\".");
                    }
                }
            }
            catch (Exception ex)
            {
                if (DevkitServerModule.InitializedLogging)
                {
                    Logger.DevkitServer.LogError(Source, ex, $"Error copying default config file: {ConfigFilePath.Format()}.");
                }
                else
                {
                    CommandWindow.LogError($"[{Source}] Error copying default config file: \"" + ConfigFilePath + "\".");
                    CommandWindow.LogError(ex);
                }
            }
            if (File.Exists(path))
            {
                ReadOnlySpan<byte> bytes = FileUtil.ReadAllBytesUtf8(path);
                if (bytes.Length > 0)
                {
                    Utf8JsonReader reader = new Utf8JsonReader(bytes, ReaderOptions);
                    return JsonSerializer.Deserialize<DevkitServerSystemConfig>(ref reader, SerializerSettings) ?? throw new JsonException("Failed to read SystemConfig: returned null.");
                }
            }
        }
        catch (Exception ex)
        {
            if (DevkitServerModule.InitializedLogging)
            {
                Logger.DevkitServer.LogError(Source, ex, $"Error reading config file: {ConfigFilePath.Format()}.");
            }
            else
            {
                CommandWindow.LogError($"[{Source}] Error reading config file: \"{ConfigFilePath}\".");
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
                Thread.BeginCriticalRegion();
                try
                {
                    File.Copy(ConfigFilePath, path);
                }
                finally
                {
                    Thread.EndCriticalRegion();
                }
            }
            catch (Exception ex2)
            {
                if (DevkitServerModule.InitializedLogging)
                {
                    Logger.DevkitServer.LogError(Source, ex2, $"Error backing up config file from: {ConfigFilePath.Format()}.");
                }
                else
                {
                    CommandWindow.LogError($"[{Source}] Error backing up config file from: \"{ConfigFilePath}\".");
                    CommandWindow.LogError(ex);
                }
            }
#if SERVER
            if (DevkitServerModule.InitializedLogging)
                Logger.DevkitServer.LogWarning(Source, "Server startup halted... fix the config errors or retart the server to use a clean config.");
            else
                CommandWindow.LogWarning($"[{Source}] Server startup halted... fix the config errors or retart the server to use a clean config.");

            DevkitServerModule.Fault();
#endif
        }

        DevkitServerSystemConfig config = new DevkitServerSystemConfig();
        config.SetDefaults();
        return config;
    }
    /// <summary>
    /// Recursively deletes all files and folders in <see cref="TempFolder"/>.
    /// </summary>
    public static void ClearTempFolder()
    {
        Logger.DevkitServer.LogDebug(nameof(ClearTempFolder), "Clearing temporary folders.");
        try
        {
            string tempFolder = TempFolder;
            if (!System.IO.Directory.Exists(tempFolder))
            {
                System.IO.Directory.CreateDirectory(tempFolder);
                return;
            }
            DirectoryInfo dir = new DirectoryInfo(tempFolder);
            TryDeleteRecursive(dir, false, true);
            string dsPath = Directory;
            foreach (string folder in System.IO.Directory.EnumerateDirectories(dsPath, "Temp_*", SearchOption.TopDirectoryOnly))
            {
                TryDeleteRecursive(new DirectoryInfo(folder), true, true);
            }
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError(Source, ex, "Error clearing temp folder.");
        }
    }
    private static bool TryDeleteRecursive(DirectoryInfo dir, bool del, bool root)
    {
        try
        {
            bool anyFail = false;
            foreach (FileSystemInfo entry in dir.EnumerateFileSystemInfos("*", SearchOption.TopDirectoryOnly))
            {
                if (entry is DirectoryInfo dir2)
                {
                    anyFail |= TryDeleteRecursive(dir2, true, false);
                    if (root)
                        Logger.DevkitServer.LogDebug(nameof(ClearTempFolder), $"Removed temporary folder: {Path.GetRelativePath(TempFolder, dir2.FullName).Format(false)}.");
                }
                else if (entry is FileInfo file)
                {
                    try
                    {
                        file.Delete();
                        if (root)
                            Logger.DevkitServer.LogDebug(nameof(ClearTempFolder), $"Removed temporary file: {Path.GetRelativePath(TempFolder, file.FullName).Format(false)}.");
                    }
                    catch (Exception ex)
                    {
                        Logger.DevkitServer.LogError(Source, $" • Error deleting file: {file.FullName.Format(true)}.");
                        Logger.DevkitServer.LogError(Source, $"   + {ex.GetType().Format()} - {ex.Message.Format(false)}{(ex.Message.EndsWith(".") ? string.Empty : ".")}");
                        anyFail = true;
                    }
                }
            }

            if (!anyFail && del)
            {
                dir.Delete(false);
            }
            
            return anyFail;
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError(Source, $" • Error deleting folder: {dir.FullName.Format(true)}.");
            Logger.DevkitServer.LogError(Source, $"   + {ex.GetType().Format()} - {ex.Message.Format(false)}{(ex.Message.EndsWith(".") ? string.Empty : ".")}");
            return true;
        }
    }
}
public class DevkitServerSystemConfig : SchemaConfiguration
{
#if SERVER
    protected override string GetSchemaURI() => DevkitServerModule.GetRelativeRepositoryUrl("Module/Schemas/server_config_schema.json", true);
#else
    protected override string GetSchemaURI() => DevkitServerModule.GetRelativeRepositoryUrl("Module/Schemas/client_config_schema.json", true);
#endif

#nullable disable
    /// <summary>
    /// Full (24-bit RGB) console color support using virtual terminal sequences (works in the Windows command prompt and most web terminals).
    /// </summary>
    /// <remarks>If your console contains weird symbols, try setting this to false.</remarks>
    [JsonPropertyName("terminal_full_rgb_support")]
    public bool ConsoleFullRGBSupport { get; set; }

    /// <summary>
    /// Basic (4-bit) console color support using virtual terminal sequences (works in most terminals).
    /// </summary>
    /// <remarks>If your console contains weird symbols after setting 'terminal_full_rgb_support' to false, try setting this to false.</remarks>
    [JsonPropertyName("terminal_virtual_sequence_support")]
    public bool ConsoleVirtualSequenceSupport { get; set; }

    /// <summary>
    /// Removes cosmetic improvements.
    /// </summary>
    [JsonPropertyName("walmart_pc_support")]
    public bool RemoveCosmeticImprovements { get; set; }

    /// <summary>
    /// Add a log to Unturned/Logs that includes whatever visual ANSI codes are used to color the console.
    /// </summary>
    [JsonPropertyName("ansi_log")]
    public bool ANSILog { get; set; }

    /// <summary>
    /// Adds extra debug logging from DevkitServer. Helps with diagnosing problems but could lower performance.
    /// </summary>
    [JsonPropertyName("debug_logging")]
    public bool DebugLogging { get; set; }

    /// <summary>
    /// Hide your current map name from Steam friends list and Discord rich presence. When used on a server it forces all users to hide their map names.
    /// </summary>
    /// <remarks>Plugins can override this relatively easily when on servers.</remarks>
    [JsonPropertyName("hide_map_name")]
    public bool HideMapNameFromRichPresence { get; set; }

    /// <summary>
    /// The max amount of area in pixels rendered on a chart at once. Lower this if you're crashing due to running out of memory when trying to render a chart.
    /// </summary>
    [JsonPropertyName("max_chart_render_chunk_size")]
    public int MaxChartRenderChunkSize { get; set; }

#if CLIENT
    /// <summary>
    /// Falls back to the vanilla chart and map functions when in singleplayer edit mode.
    /// </summary>
    /// <remarks>To keep clients from disconnecting while rendering it's required in multiplayer mode.</remarks>
    [JsonPropertyName("use_vanilla_cartography_in_singleplayer")]
    public bool UseVanillaCartographyInSingleplayer { get; set; }

    /// <summary>
    /// Enables a preview window for your selected object in the Object Editor.
    /// </summary>
    /// <remarks>Disable if you're seeing crashes or lag when editing objects.</remarks>
    [JsonPropertyName("enable_object_ui_extension")]
    public bool EnableObjectUIExtension { get; set; }

    /// <summary>
    /// Enables an override for better initial map creation (adding a new map).
    /// </summary>
    /// <remarks>Disable if you're seeing warnings about Level.add on load, or crashes when creating maps.</remarks>
    [JsonPropertyName("enable_better_map_creation")]
    public bool EnableBetterLevelCreation { get; set; }

    /// <summary>
    /// On Windows, disables opening the terminal window when you launch the game.
    /// </summary>
    [JsonPropertyName("disable_terminal")]
    public bool DisableTerminal { get; set; }

    /// <summary>
    /// Key used to toggle the Live Editor checkbox.
    /// </summary>
    [JsonPropertyName("edit_keybind")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public KeyCode LevelObjectEditKeybind { get; set; }

    /// <summary>
    /// Key used to print all objects that don't have an offset to Client.log.
    /// </summary>
    [JsonPropertyName("log_mising_keybind")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public KeyCode LogMissingLevelObjectKeybind { get; set; }

    /// <summary>
    /// Enables cycling between materials in the material palette. May cause some lag on lower end machines.
    /// </summary>
    [JsonPropertyName("cycle_material_palette")]
    public bool ShouldCycleLevelObjectMaterialPalette { get; set; }

    /// <summary>
    /// Disables searching other modules for default icon providers. Set this to true if errors arise from ApplyDefaultProviders.
    /// </summary>
    [JsonPropertyName("disable_default_provider_search")]
    public bool DisableDefaultLevelObjectIconProviderSearch { get; set; }
#endif

#if SERVER
    /// <summary>
    /// Maximum number of failed passwords before the connecting user gets put on a join cooldown.
    /// </summary>
    /// <remarks>0 or -1 = infinite.</remarks>
    [JsonPropertyName("max_password_attempts")]
    public int PasswordAttempts { get; set; }

    /// <summary>
    /// Number of seconds after hitting the max password attempts that it'll reset.
    /// </summary>
    /// <remarks>&lt;= 0 is the same as setting 'max_password_attempts' to 0 or -1.</remarks>
    [JsonPropertyName("wrong_password_block_expire_seconds")]
    public float WrongPasswordBlockExpireSeconds { get; set; }

    /// <summary>
    /// Options for newly created maps.
    /// </summary>
    [JsonPropertyName("new_level_info")]
    public NewLevelCreationOptions NewLevelInfo { get; set; }

    /// <summary>
    /// Not recommended, tries to read the map from your files instead of downloading it on join.
    /// </summary>
    /// <remarks>You will almost certainly get out of sync from the server.</remarks>
    [JsonPropertyName("disable_map_download")]
    public bool DisableMapDownload { get; set; }

    /// <summary>
    /// Admins ('blue hammers', given with the vanilla /admin command) are treated as having the '*' permission which gives them all permissions.
    /// </summary>
    [JsonPropertyName("admins_are_superusers")]
    public bool AdminsAreSuperusers { get; set; }

    /// <summary>
    /// When clients change the time in the lighting menu, it will update on all clients.
    /// </summary>
    [JsonPropertyName("sync_editor_time")]
    public bool SyncEditorTime { get; set; }

    /// <summary>
    /// When clients change the preview weather in the lighting menu, it will update on all clients.
    /// </summary>
    [JsonPropertyName("sync_editor_weather")]
    public bool SyncEditorWeather { get; set; }

    /// <summary>
    /// Set this to true to not kick clients for missing or mismatched assets. A message will still be logged.
    /// </summary>
    [JsonPropertyName("disable_asset_validation")]
    public bool DisableAssetValidation { get; set; }

    /// <summary>
    /// Default permissions a user has.
    /// </summary>
    [JsonPropertyName("default_permissions")]
    public PermissionBranch[] DefaultUserPermissions { get; set; }

    /// <summary>
    /// Default permission groups a user has.
    /// </summary>
    [JsonPropertyName("default_permission_groups")]
    public string[] DefaultUserPermissionGroups { get; set; }

    [JsonPropertyName("high_speed")]
    public TcpServerInfo TcpSettings { get; set; }

    [JsonPropertyName("max_client_edit_fps")]
    public int MaxClientEditFPS { get; set; } = 50;
#endif
#nullable restore
#if SERVER

    [JsonPropertyName("user_savedata_override")]
    public string? UserSavedataLocationOverride { get; set; }

#endif
    public void SetDefaults()
    {
        ConsoleFullRGBSupport = true;
        ConsoleVirtualSequenceSupport = true;
        RemoveCosmeticImprovements = false;
        DebugLogging = false;
#if CLIENT
        DisableTerminal = false;
        UseVanillaCartographyInSingleplayer = false;
        EnableObjectUIExtension = true;
        EnableBetterLevelCreation = true;
        LevelObjectEditKeybind = KeyCode.F8;
        LogMissingLevelObjectKeybind = KeyCode.Keypad5;
        ShouldCycleLevelObjectMaterialPalette = true;
        DisableDefaultLevelObjectIconProviderSearch = false;
#endif
#if SERVER
        NewLevelInfo = NewLevelCreationOptions.Default;
        DisableMapDownload = false;
        TcpSettings = new TcpServerInfo { EnableHighSpeedSupport = false, HighSpeedPort = (ushort)(Provider.port + 2) };
        DefaultUserPermissions = Array.Empty<PermissionBranch>();
        DefaultUserPermissionGroups =
        [
            "viewer"
        ];
        UserSavedataLocationOverride = null;
        AdminsAreSuperusers = true;
        SyncEditorTime = false;
        SyncEditorWeather = false;
        MaxClientEditFPS = 50;
        PasswordAttempts = 4;
        MaxChartRenderChunkSize = 4096;
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