using DevkitServer.API.Abstractions;
using DevkitServer.API.Permissions;
using DevkitServer.Plugins;
#if CLIENT
using DevkitServer.API.UI.Extensions;
#endif

namespace DevkitServer.API;
public abstract class Plugin : IDevkitServerColorPlugin, ICachedTranslationSourcePlugin, IReflectionDoneListenerDevkitServerPlugin
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
    public PluginAssembly Assembly { get; set; } = null!;

    /// <inheritdoc/>
    public Local Translations { get; private set; }

    /// <inheritdoc/>
    public string PermissionPrefix { get; set; }

    /// <inheritdoc/>
    public abstract bool DeveloperMode { get; }

    protected Plugin()
    {
        string asmName = GetType().Assembly.GetName().Name;
        _defaultName = asmName + "/" + GetType().Name;
        // ReSharper disable once VirtualMemberCallInConstructor (Reason: expecting literal string override)
        string name = Name ?? _defaultName;
        DataDirectory = Path.Combine(PluginLoader.PluginsDirectory, asmName + "." + name);
        LocalizationDirectory = Path.Combine(DataDirectory, "Localization");
        MainLocalizationDirectory = Path.Combine(LocalizationDirectory, "Main");
        Translations = Localization.tryRead(MainLocalizationDirectory, false);
        PermissionPrefix = name.ToLowerInvariant().Replace('.', '-');
        if (GetType().TryGetAttributeSafe(out PermissionPrefixAttribute prefixAttr, true) && !string.IsNullOrWhiteSpace(prefixAttr.Prefix))
            PermissionPrefix = prefixAttr.Prefix;
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

    void IReflectionDoneListenerDevkitServerPlugin.OnReflectionDone(PluginAssembly assembly, bool isFirstPluginInAssembly)
    {
        OnReflectionDone(assembly, isFirstPluginInAssembly);
    }

    /// <summary>
    /// Called to load the plugin.
    /// </summary>
    protected abstract void Load();

    /// <summary>
    /// Called when reflection for the plugin's assembly is ran. Will only call once shortly after <see cref="IDevkitServerPlugin.Load"/>.
    /// </summary>
    protected virtual void OnReflectionDone(PluginAssembly assembly, bool isFirstPluginInAssembly) { }

    /// <summary>
    /// Called to unload the plugin.
    /// </summary>
    protected abstract void Unload();

    /// <summary>
    /// Re-read the main translation file.
    /// </summary>
    public void ReloadTranslations()
    {
        Local lcl = Localization.tryRead(MainLocalizationDirectory, false);
        DevkitServerUtility.UpdateLocalizationFile(ref lcl, DefaultLocalization, MainLocalizationDirectory);
        Translations = lcl;
    }

    /// <inheritdoc/>
    public void LogDebug(string message, ConsoleColor color = ConsoleColor.DarkGray) =>
        Logger.LogDebug("[" + this.GetSource() + "] " + message, color);

    /// <inheritdoc/>
    public void LogInfo(string message, ConsoleColor color = ConsoleColor.DarkCyan) =>
        Logger.LogInfo("[" + this.GetSource() + "] " + message, color);

    /// <inheritdoc/>
    public void LogWarning(string message, ConsoleColor color = ConsoleColor.Yellow) =>
        Logger.LogWarning(message, color, method: this.GetSource());

    /// <inheritdoc/>
    public void LogError(string message, ConsoleColor color = ConsoleColor.Red) =>
        Logger.LogError(message, color, method: this.GetSource());

    /// <inheritdoc/>
    public void LogError(Exception ex) =>
        Logger.LogError(ex, method: this.GetSource());
#if CLIENT
    public void RegisterUIExtension(Type implementationType, Type parentUIType, int priority)
    {
        UIExtensionManager.RegisterExtension(new UIExtensionInfo(implementationType, parentUIType, priority, this));
    }
#endif
#nullable disable
    ITranslationSource ICachedTranslationSourcePlugin.TranslationSource { get; set; }
#nullable restore
}

public abstract class Plugin<TConfig> : Plugin, IDevkitServerPlugin<TConfig> where TConfig : class, new()
{
    private readonly JsonConfigurationFile<TConfig> _config;
    public TConfig Configuration => _config.Configuration;
    public virtual string RelativeMainConfigFileName => "config_main.json";
    protected Plugin()
    {
        // ReSharper disable once VirtualMemberCallInConstructor (Reason: expecting literal string override)
        _config = new JsonConfigurationFile<TConfig>(Path.Combine(DataDirectory, RelativeMainConfigFileName)) { Faultable = true };

        _config.ReloadConfig();
    }

    void IReloadableDevkitServerPlugin.Reload()
    {
        ReloadConfig();
        Reload();
    }
    /// <summary>
    /// Reload external config files, etc.
    /// </summary>
    public virtual void Reload() { }
    /// <summary>
    /// Re-read <see cref="Configuration"/> from file.
    /// </summary>
    public void ReloadConfig() => _config.ReloadConfig();
    /// <summary>
    /// Save <see cref="Configuration"/> to file.
    /// </summary>
    public void SaveConfig() => _config.SaveConfig();
}