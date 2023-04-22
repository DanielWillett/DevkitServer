using System.Diagnostics;
using DevkitServer.Configuration;
using System.Reflection;
using DevkitServer.Plugins;

namespace DevkitServer.API;
public abstract class Plugin : IDevkitServerPlugin
{
    private readonly string _defaultName;
    protected string DataPath { get; }
    public Assembly Assembly { get; }

    /// <summary>The name of the plugin used for display and data path.</summary>
    /// <remarks>Must be a constant literal string override</remarks>
    public virtual string Name => _defaultName;
    /// <summary>Log color of your plugin.</summary>
    public virtual Color Color { get; } = new Color32(204, 153, 255, 255);
    public Local Translations { get; private set; }
    protected Plugin()
    {
        Assembly = GetType().Assembly;
        _defaultName = Assembly.GetName().Name + "/" + GetType().Name;
        // ReSharper disable once VirtualMemberCallInConstructor (Reason: expecting literal string override)
        string name = Name ?? _defaultName;
        DataPath = Path.Combine(PluginLoader.PluginDirectory, Assembly.GetName().Name + "." + name);
        Translations = Localization.tryRead(DataPath, false);
        CompareLocal();
    }
    protected void CompareLocal()
    {
        DatDictionary def = DefaultLocalization;
        Local current = Translations;
        DatDictionary @new = new DatDictionary();
        foreach (KeyValuePair<string, IDatNode> pair in def)
        {
            if (!current.has(pair.Key))
                @new.Add(pair.Key, pair.Value);
            else @new.Add(pair.Key, new DatValue(current.format(pair.Key)));
        }

        Translations = new Local(@new, def);
        string path = Path.Combine(DataPath, "English.dat");
        bool eng = Provider.language.Equals("English", StringComparison.InvariantCultureIgnoreCase);
        if (!File.Exists(path))
        {
            DevkitServerUtility.WriteData(path, eng ? @new : def);
            if (eng) return;
        }
        
        path = Path.Combine(DataPath, Provider.language + ".dat");
        DevkitServerUtility.WriteData(path, @new);
    }
    protected virtual DatDictionary DefaultLocalization => new DatDictionary();
    void IDevkitServerPlugin.Load() => Load();
    void IDevkitServerPlugin.Unload() => Unload();
    protected abstract void Load();
    protected abstract void Unload();
    
    public void LogDebug(string message, ConsoleColor color = ConsoleColor.Gray) =>
        Logger.LogDebug("[" + Name.ToUpperInvariant().Colorize(Color) + "] " + message, color);
    public void LogInfo(string message, ConsoleColor color = ConsoleColor.Gray) =>
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