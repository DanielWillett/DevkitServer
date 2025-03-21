using DevkitServer.Configuration;
using SDG.Framework.Devkit;
using System.Text.Json;
using System.Text.Json.Serialization;
using DevkitServer.Util.Encoding;

namespace DevkitServer.API;

/// <summary>
/// Managed JSON config file of type <typeparamref name="TConfig"/>.
/// </summary>
/// <remarks>Has built in backup and default features.</remarks>
public class JsonConfigurationFile<TConfig> : IJsonSettingProvider, IConfigProvider<TConfig> where TConfig : class, new()
{
    protected CachedMulticastEvent<Action> EventOnRead;

    /// <summary>
    /// Any edits done during this event will be written.
    /// </summary>
    public event Action OnRead
    {
        add => EventOnRead.Add(value);
        remove => EventOnRead.Remove(value);
    }
    [JsonIgnore]
    public virtual TConfig? Default => null;
    [JsonIgnore]
    public TConfig Configuration
    {
        get => _config;
        set
        {
            lock (_sync)
            {
                TConfig old = Interlocked.Exchange(ref _config, value);
                if (old is IDisposable d && !ReferenceEquals(old, value))
                    d.Dispose();
            }
        }
    }

    private TConfig _config = null!;
    private readonly object _sync = new object();
    private string _file = null!;
    [JsonIgnore]
    public JsonReaderOptions ReaderOptions { get; set; } = DevkitServerConfig.ReaderOptions;
    [JsonIgnore]
    public JsonWriterOptions WriterOptions { get; set; } = DevkitServerConfig.WriterOptions;
    [JsonIgnore]
    public JsonSerializerOptions SerializerOptions { get; set; } = DevkitServerConfig.SerializerSettings;

    /// <summary>
    /// File where the config is expected to be stored.
    /// </summary>
    [JsonIgnore]
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
    /// <summary>
    /// Tells reloading to not make backups, copies, or save on read.
    /// </summary>
    public bool ReadOnlyReloading { get; set; }
    internal bool Faultable { get; set; }
    internal bool Defaultable { get; set; }
    public JsonConfigurationFile(string file)
    {
        File = file;
        EventOnRead = new CachedMulticastEvent<Action>(GetType(), nameof(OnRead));
    }
    protected virtual void OnReload() { }
    public void ReloadConfig()
    {
        lock (_sync)
        {
            TConfig old = ReadFromFile(File, Faultable, this, Default, ReadOnlyReloading, Defaultable);
            old = Interlocked.Exchange(ref _config, old);
            if (old is IDisposable d)
                d.Dispose();
            EventOnRead.TryInvoke();
            try
            {
                OnReload();
            }
            catch (Exception ex)
            {
                Logger.DevkitServer.LogError(typeof(TConfig).Format(), ex, $"Exception in {nameof(OnReload).Colorize(ConsoleColor.White)} after reading {typeof(TConfig).Format()} config at {File.Format(false)}.");
            }
            if (!ReadOnlyReloading)
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
            Utf8JsonWriter writer = new Utf8JsonWriter(fs, options?.WriterOptions ?? DevkitServerConfig.WriterOptions);
            JsonSerializer.Serialize(writer, config, options == null ? DevkitServerConfig.SerializerSettings : options.SerializerOptions);
            return config;
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError(typeof(TConfig).Format(), ex, "Error writing config file: \"" + path + "\".");
        }

        return null;
    }

    public static TConfig ReadFromFile(string path, IJsonSettingProvider? options = null, TConfig? @default = null, bool readOnly = false)
        => ReadFromFile(path, false, options, @default, readOnly);
    internal static TConfig ReadFromFile(string path, bool faultable, IJsonSettingProvider? options = null, TConfig? @default = null, bool readOnly = false, bool defaultable = false)
    {
        TConfig config;
        try
        {
            try
            {
                if (!readOnly && !System.IO.File.Exists(path) && defaultable)
                {
                    string fn = Path.GetFileName(path);
                    string? moduleFile = DevkitServerModule.FindModuleFile(fn);
                    if (moduleFile != null)
                    {
                        System.IO.File.Copy(moduleFile, path, false);
                        Logger.DevkitServer.LogInfo(typeof(TConfig).Format(), $"Copied default config file from: {moduleFile.Format()}.");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.DevkitServer.LogError(typeof(TConfig).Format(), ex, $"Error copying default config file: {path.Format()}.");
            }

            if (System.IO.File.Exists(path))
            {
                ReadOnlySpan<byte> bytes = FileUtil.ReadAllBytesUtf8(path);

                if (bytes.Length > 0)
                {
                    Utf8JsonReader reader = new Utf8JsonReader(bytes, DevkitServerConfig.ReaderOptions);
                    config = JsonSerializer.Deserialize<TConfig>(ref reader,
                               options == null
                                   ? DevkitServerConfig.SerializerSettings
                                   : options.SerializerOptions)
                           ?? throw new JsonException("Failed to read " + typeof(TConfig).Name + ": returned null.");
                    if (config is IDirtyable dirty2)
                        dirty2.isDirty = false;
                    return config;
                }
            }

            if (@default != null)
                return @default;
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError(typeof(TConfig).Format(), ex, $"Error reading config file: {path.Format()}.");

            string oldpath = path;
            if (!readOnly)
            {
                try
                {
                    int c = 0;
                    do
                    {
                        ++c;
                        path = Path.Combine(Path.GetDirectoryName(oldpath)!, Path.GetFileNameWithoutExtension(oldpath) + "_backup_" + c + Path.GetExtension(oldpath));
                    }
                    while (System.IO.File.Exists(path));
                    System.IO.File.Move(oldpath, path);
                }
                catch (Exception ex2)
                {
                    Logger.DevkitServer.LogError(typeof(TConfig).Format(), ex2, $"Error backing up invalid config file from: {oldpath.Format()} to {path.Format()}.");
                }
            }
#if SERVER
            if (faultable)
            {
                Logger.DevkitServer.LogWarning(typeof(TConfig).Format(), "Server startup halted." + (readOnly ? string.Empty :
                    $" Fix the config errors in {path.Format()} and rename it to {oldpath.Format()} or retart the server to use the default config."));
                DevkitServerModule.Fault();
            }
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
