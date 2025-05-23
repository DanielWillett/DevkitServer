using DanielWillett.ReflectionTools;
using DevkitServer.API.Abstractions;
using DevkitServer.Core.Logging.Loggers;
using DevkitServer.Plugins;
using System.Globalization;
#if CLIENT
#endif

namespace DevkitServer.API;
public abstract class Plugin : CoreLogger, IDevkitServerColorPlugin, ICachedTranslationSourcePlugin, IReflectionDoneListenerDevkitServerPlugin
{
    public static readonly Color32 DefaultColor = new Color32(204, 153, 255, 255);
    private readonly string _defaultName;
    private string? _localEnglishPath;
    private string? _localPrimaryPath;

    /// <inheritdoc/>
    public virtual string Name => _defaultName;

    /// <inheritdoc/>
    public virtual string MenuName => Name;

    /// <inheritdoc/>
    public virtual Color32 Color => DefaultColor;

    /// <inheritdoc/>
    public string DataDirectory { get; }

    /// <inheritdoc/>
    public string LocalizationDirectory { get; }

    /// <summary>
    /// The data path of the main localization file. This should be a directory.
    /// </summary>
    public string MainLocalizationDirectory { get; }

    /// <inheritdoc/>
    public string CommandLocalizationDirectory { get; }

    /// <inheritdoc/>
    public Local Translations { get; private set; }

    /// <inheritdoc/>
    public abstract bool DeveloperMode { get; }

    /// <inheritdoc/>
    PluginAssembly IDevkitServerPlugin.Assembly { get; set; } = null!;

    /// <inheritdoc/>
    string IDevkitServerPlugin.PermissionPrefix { get; set; } = null!;

    /// <summary>
    /// Information about the assembly containing the plugin.
    /// </summary>
    public PluginAssembly Assembly => ((IDevkitServerPlugin)this).Assembly;

    /// <summary>
    /// Must be unique among all loaded plugins. Can not contain a period.
    /// </summary>
    public string PermissionPrefix => ((IDevkitServerPlugin)this).PermissionPrefix;

    protected Plugin() : base("plugin")
    {
        string asmName = GetType().Assembly.GetName().Name;
        _defaultName = GetType().Name;
        // ReSharper disable once VirtualMemberCallInConstructor (Reason: expecting literal string override)
        string name = Name ?? _defaultName;

        bool isSameName = string.Compare(name, asmName, CultureInfo.InvariantCulture,
            CompareOptions.IgnoreCase | CompareOptions.IgnoreKanaType | CompareOptions.IgnoreNonSpace | CompareOptions.IgnoreSymbols | CompareOptions.IgnoreWidth
            ) == 0;

        DataDirectory = Path.Combine(PluginLoader.PluginsDirectory, isSameName ? asmName : asmName + "." + name);
        LocalizationDirectory = Path.Combine(DataDirectory, "Localization");
        CommandLocalizationDirectory = Path.Combine(LocalizationDirectory, "Commands");
        MainLocalizationDirectory = Path.Combine(LocalizationDirectory, "Main");
        Translations = DevkitServerUtility.ReadLocalFromFileOrFolder(MainLocalizationDirectory, out _localPrimaryPath, out _localEnglishPath);
        ((IDevkitServerPlugin)this).PermissionPrefix = name.ToLowerInvariant().Replace('.', '-');
        if (GetType().TryGetAttributeSafe(out PermissionPrefixAttribute prefixAttr, true) && !string.IsNullOrWhiteSpace(prefixAttr.Prefix))
            ((IDevkitServerPlugin)this).PermissionPrefix = prefixAttr.Prefix;
        else if (GetType().Assembly.TryGetAttributeSafe(out prefixAttr, true) && !string.IsNullOrWhiteSpace(prefixAttr.Prefix))
            ((IDevkitServerPlugin)this).PermissionPrefix = prefixAttr.Prefix;
    }
    
    /// <summary>
    /// The default values for your primary localization file. This will contain shared local keys that aren't tied to a specific command or UI.
    /// </summary>
    /// <remarks>This is only ever queried once so there is no performance penalty to using an expression definition instead of a backing field.</remarks>
    protected virtual LocalDatDictionary DefaultLocalization => new LocalDatDictionary();

    /// <inheritdoc/>
    void IDevkitServerPlugin.Load()
    {
        Local lcl = Translations;
        DevkitServerUtility.UpdateLocalizationFile(ref lcl, DefaultLocalization, MainLocalizationDirectory, _localPrimaryPath, _localEnglishPath);
        Translations = lcl;
        Load();
    }

    /// <inheritdoc/>
    void IDevkitServerPlugin.Unload()
        => Unload();
    void IReflectionDoneListenerDevkitServerPlugin.OnReflectionDone(PluginAssembly assembly, bool isFirstPluginInAssembly)
        => OnReflectionDone(assembly, isFirstPluginInAssembly);

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
        Local lcl = DevkitServerUtility.ReadLocalFromFileOrFolder(MainLocalizationDirectory, out _localPrimaryPath, out _localEnglishPath);
        DevkitServerUtility.UpdateLocalizationFile(ref lcl, DefaultLocalization, MainLocalizationDirectory, _localPrimaryPath, _localEnglishPath);
        Translations = lcl;
    }
    ITranslationSource ICachedTranslationSourcePlugin.TranslationSource { get; set; } = null!;
    public sealed override string CoreType => this.GetSource();
    public sealed override bool IsSeverityEnabled(Severity severity, object? source) => severity == Severity.Debug && DeveloperMode || base.IsSeverityEnabled(severity, source);
    string ILogSource.Source => string.Empty;
    bool? ILogSource.GetExplicitVisibilitySetting(Severity severity)
    {
        if (severity == Severity.Debug && DeveloperMode)
            return true;

        return null;
    }
}

/// <summary>
/// Base class for a DevkitServer plugin with a JSON config file (with it's config data of type <typeparamref name="TConfig"/>).
/// </summary>
public abstract class Plugin<TConfig> : Plugin, IDevkitServerPlugin<TConfig> where TConfig : class, new()
{
    private readonly JsonConfigurationFile<TConfig> _config;

    /// <summary>
    /// Configuration instance of a plugin with the config <typeparamref name="TConfig"/>.
    /// </summary>
    public TConfig Configuration => _config.Configuration;

    /// <summary>
    /// Path to the config file relative to the plugin's data directory.
    /// </summary>
    /// <remarks>Will not be used if the path isn't under the plugin's data directory.</remarks>
    public virtual string RelativeMainConfigFileName => "." + Path.DirectorySeparatorChar + "config_main.json";
    protected Plugin()
    {
        // ReSharper disable once VirtualMemberCallInConstructor (Reason: expecting literal string override)
        string path = RelativeMainConfigFileName;
        if (!Path.IsPathRooted(path))
            path = Path.Combine(DataDirectory, path);
        if (!FileUtil.IsChildOf(DataDirectory, path))
        {
            this.LogWarning($"Main config directory {path.Format()} is not valid. Must be a child of {DataDirectory.Format()}.");
            path = "config_main.json";
        }

        _config = new JsonConfigurationFile<TConfig>(Path.Combine(DataDirectory, path)) { Faultable = true };

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