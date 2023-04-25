using DevkitServer.API.Permissions;
using DevkitServer.Plugins;
using System.Reflection;

namespace DevkitServer.API;
public abstract class Plugin : IDevkitServerColorPlugin
{
    public static readonly Color DefaultColor = new Color32(204, 153, 255, 255);
    private readonly string _defaultName;

    /// <inheritdoc/>
    public virtual string Name => _defaultName;

    /// <inheritdoc/>
    public virtual string MenuName => Name;

    /// <inheritdoc/>
    public virtual Color Color => DefaultColor;

    /// <inheritdoc/>
    public string DataDirectory { get; }

    /// <inheritdoc/>
    public string LocalizationDirectory { get; }

    /// <summary>
    /// The data path of the main localization file. This should be a directory.
    /// </summary>
    public string MainLocalizationDirectory { get; }

    /// <inheritdoc/>
    public Assembly Assembly { get; }

    /// <inheritdoc/>
    public Local Translations { get; private set; }

    /// <inheritdoc/>
    public string PermissionPrefix { get; set; }

    protected Plugin()
    {
        Assembly = GetType().Assembly;
        _defaultName = Assembly.GetName().Name + "/" + GetType().Name;
        // ReSharper disable once VirtualMemberCallInConstructor (Reason: expecting literal string override)
        string name = Name ?? _defaultName;
        DataDirectory = Path.Combine(PluginLoader.PluginDirectory, Assembly.GetName().Name + "." + name);
        LocalizationDirectory = Path.Combine(DataDirectory, "Localization");
        MainLocalizationDirectory = Path.Combine(LocalizationDirectory, "Main");
        Translations = Localization.tryRead(MainLocalizationDirectory, false);
        PermissionPrefix = name.ToLowerInvariant().Replace('.', '-');
        if (Attribute.GetCustomAttribute(GetType(), typeof(PermissionPrefixAttribute)) is PermissionPrefixAttribute { Prefix.Length: > 0 } attr)
            PermissionPrefix = attr.Prefix;
    }
    protected virtual LocalDatDictionary DefaultLocalization => new LocalDatDictionary();

    /// <inheritdoc/>
    void IDevkitServerPlugin.Load()
    {
        Local lcl = Translations;
        DevkitServerUtility.UpdateLocalizationFile(ref lcl, DefaultLocalization, MainLocalizationDirectory);
        Translations = lcl;
        Load();
    }

    /// <inheritdoc/>
    void IDevkitServerPlugin.Unload() => Unload();

    /// <summary>
    /// Called to load the plugin.
    /// </summary>
    protected abstract void Load();

    /// <summary>
    /// Called to unload the plugin.
    /// </summary>
    protected abstract void Unload();

    /// <inheritdoc/>
    public void LogDebug(string message, ConsoleColor color = ConsoleColor.DarkGray) =>
        Logger.LogDebug("[" + Name.ToUpperInvariant().Colorize(Color) + "] " + message, color);

    /// <inheritdoc/>
    public void LogInfo(string message, ConsoleColor color = ConsoleColor.DarkCyan) =>
        Logger.LogInfo("[" + Name.ToUpperInvariant().Colorize(Color) + "] " + message, color);

    /// <inheritdoc/>
    public void LogWarning(string message, ConsoleColor color = ConsoleColor.Yellow) =>
        Logger.LogWarning("[" + Name.ToUpperInvariant().Colorize(Color) + "] " + message, color);

    /// <inheritdoc/>
    public void LogError(string message, ConsoleColor color = ConsoleColor.Red) =>
        Logger.LogError("[" + Name.ToUpperInvariant().Colorize(Color) + "] " + message, color);

    /// <inheritdoc/>
    public void LogError(Exception ex) =>
        Logger.LogError(ex, method: Name.ToUpperInvariant().Colorize(Color));
}

public abstract class Plugin<TConfig> : Plugin, IDevkitServerPlugin<TConfig> where TConfig : class, new()
{
    private readonly JsonConfigurationFile<TConfig> _config;
    public TConfig Configuration => _config.Configuration;
    public virtual string RelativeMainConfigFileName => "config_main.json";
    protected Plugin()
    {
        // ReSharper disable once VirtualMemberCallInConstructor (Reason: expecting literal string override)
        _config = new JsonConfigurationFile<TConfig>(Path.Combine(DataDirectory, RelativeMainConfigFileName));

        _config.ReloadConfig();
    }
    public void ReloadConfig() => _config.ReloadConfig();
    public void SaveConfig() => _config.SaveConfig();
}