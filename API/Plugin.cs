using DevkitServer.API.Permissions;
using DevkitServer.Plugins;
using System.Reflection;

namespace DevkitServer.API;
public abstract class Plugin : IDevkitServerColorPlugin
{
    public static readonly Color DefaultColor = new Color32(204, 153, 255, 255);
    private readonly string _defaultName;
    public virtual string Name => _defaultName;
    public virtual string MenuName => Name;
    public virtual Color Color => DefaultColor;
    protected string DataPath { get; }
    public Assembly Assembly { get; }
    public Local Translations { get; private set; }
    public string PermissionPrefix { get; set; }
    protected Plugin()
    {
        Assembly = GetType().Assembly;
        _defaultName = Assembly.GetName().Name + "/" + GetType().Name;
        // ReSharper disable once VirtualMemberCallInConstructor (Reason: expecting literal string override)
        string name = Name ?? _defaultName;
        DataPath = Path.Combine(PluginLoader.PluginDirectory, Assembly.GetName().Name + "." + name);
        Translations = Localization.tryRead(DataPath, false);
        PermissionPrefix = name.ToLowerInvariant().Replace('.', '-');
        if (Attribute.GetCustomAttribute(GetType(), typeof(PermissionPrefixAttribute)) is PermissionPrefixAttribute { Prefix.Length: > 0 } attr)
            PermissionPrefix = attr.Prefix;
    }
    protected virtual DatDictionary DefaultLocalization => new DatDictionary();
    void IDevkitServerPlugin.Load()
    {
        Local lcl = Translations;
        DevkitServerUtility.UpdateLocalizationFile(ref lcl, DefaultLocalization, DataPath);
        Translations = lcl;
        Load();
    }

    void IDevkitServerPlugin.Unload() => Unload();
    protected abstract void Load();
    protected abstract void Unload();
    
    public void LogDebug(string message, ConsoleColor color = ConsoleColor.DarkGray) =>
        Logger.LogDebug("[" + Name.ToUpperInvariant().Colorize(Color) + "] " + message, color);
    public void LogInfo(string message, ConsoleColor color = ConsoleColor.DarkCyan) =>
        Logger.LogInfo("[" + Name.ToUpperInvariant().Colorize(Color) + "] " + message, color);
    public void LogWarning(string message, ConsoleColor color = ConsoleColor.Yellow) =>
        Logger.LogWarning("[" + Name.ToUpperInvariant().Colorize(Color) + "] " + message, color);
    public void LogError(string message, ConsoleColor color = ConsoleColor.Red) =>
        Logger.LogError("[" + Name.ToUpperInvariant().Colorize(Color) + "] " + message, color);
    public void LogError(Exception ex) =>
        Logger.LogError(ex, method: Name.ToUpperInvariant().Colorize(Color));
}

public abstract class Plugin<TConfig> : Plugin, IDevkitServerPlugin<TConfig> where TConfig : class, new()
{
    private readonly JsonConfigurationFile<TConfig> _config;
    public TConfig Configuration => _config.Configuration;
    protected Plugin()
    {
        _config = new JsonConfigurationFile<TConfig>(Path.Combine(DataPath, "config_main.json"));

        _config.ReloadConfig();
    }
    public void ReloadConfig() => _config.ReloadConfig();
    public void SaveConfig() => _config.SaveConfig();
}