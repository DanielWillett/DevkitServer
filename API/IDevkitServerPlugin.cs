using DevkitServer.API.Abstractions;
using DevkitServer.API.Logging;
using DevkitServer.Plugins;

namespace DevkitServer.API;
public interface IDevkitServerPlugin<out TConfig> : IReloadableDevkitServerPlugin, IConfigProvider<TConfig> where TConfig : class, new();
public interface IDevkitServerPlugin : IDevkitServerSourceLogger
{
    /// <summary>
    /// Must be unique among all loaded plugins. Can not contain a period.
    /// </summary>
    string PermissionPrefix { get; set; }
    /// <summary>
    /// The name of the plugin used for display and data path.
    /// </summary>
    /// <remarks>Must be a constant literal string override. Defaults to AssemblyName/ClassName.</remarks>
    string Name { get; }

    /// <summary>
    /// The name of the plugin displayed in the server info panel.
    /// </summary>
    /// <remarks>Defaults to <see cref="Name"/>.</remarks>
    string MenuName { get; }

    /// <summary>
    /// The data path of the plugin. Config, localization, etc will root from here. This should be a directory.
    /// </summary>
    /// <remarks>Must be set in the constructor.</remarks>
    string DataDirectory { get; }

    /// <summary>
    /// The data path of the base localization folder for commands, etc. This should be a directory.
    /// </summary>
    /// <remarks>Must be set in the constructor.</remarks>
    string LocalizationDirectory { get; }

    /// <summary>
    /// The data path of the command localization folder, etc. This should be a directory.
    /// </summary>
    /// <remarks>Must be set in the constructor.</remarks>
    string CommandLocalizationDirectory { get; }

    /// <summary>
    /// Information about the assembly containing the plugin.
    /// </summary>
    PluginAssembly Assembly { get; set; }

    /// <summary>
    /// Main translation file for the plugin.
    /// </summary>
    Local Translations { get; }

    /// <summary>
    /// Whether the plugin is actively in development. Recommended to use <c>#if DEBUG</c>/<c>#else</c>/<c>#endif</c>
    /// flags to set this to <see langword="true"/> during debug and <see langword="false"/> in release.
    /// </summary>
    /// <remarks>Used to disable various readonly restrictions to plugin files (like object icons).</remarks>
    bool DeveloperMode { get; }

    /// <summary>
    /// Called to load the plugin.
    /// </summary>
    void Load();

    /// <summary>
    /// Called to unload the plugin.
    /// </summary>
    void Unload();
}
public interface IReloadableDevkitServerPlugin : IDevkitServerPlugin
{
    /// <summary>
    /// Reload config files, etc.
    /// </summary>
    void Reload();
}
public interface IReflectionDoneListenerDevkitServerPlugin : IDevkitServerPlugin
{
    /// <summary>
    /// Called when reflection for the plugin's assembly is ran. Will only call once shortly after <see cref="IDevkitServerPlugin.Load"/>.
    /// </summary>
    void OnReflectionDone(PluginAssembly assembly, bool isFirstPluginInAssembly);
}

public interface IDevkitServerColorPlugin : IDevkitServerPlugin
{
    /// <summary>
    /// Color a plugin uses with logging.
    /// </summary>
    /// <remarks>Default: rgb(204, 153, 255), <see cref="Plugin.DefaultColor"/>.</remarks>
    Color32 Color { get; }
}
internal interface ICachedTranslationSourcePlugin : IDevkitServerPlugin
{
    ITranslationSource TranslationSource { get; set; }
}