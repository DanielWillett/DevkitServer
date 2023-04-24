using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DevkitServer.API;
public interface IDevkitServerPlugin<out TConfig> : IDevkitServerPlugin, IConfigProvider<TConfig> where TConfig : class, new() { }
public interface IDevkitServerPlugin
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
    /// Assembly containing the plugin.
    /// </summary>
    Assembly Assembly { get; }

    /// <summary>
    /// Main translation file for the plugin.
    /// </summary>
    Local Translations { get; }

    /// <summary>
    /// Send a debug message to the log. Shoud be ignored when not building with the DEBUG flag.
    /// </summary>
    void LogDebug(string message, ConsoleColor color = ConsoleColor.DarkGray);

    /// <summary>
    /// Send information message to the log.
    /// </summary>
    void LogInfo(string message, ConsoleColor color = ConsoleColor.DarkCyan);

    /// <summary>
    /// Send a warning message to the log.
    /// </summary>
    void LogWarning(string message, ConsoleColor color = ConsoleColor.Yellow);

    /// <summary>
    /// Send an error message to the log.
    /// </summary>
    void LogError(string message, ConsoleColor color = ConsoleColor.Red);

    /// <summary>
    /// Send an <see cref="Exception"/> to the log.
    /// </summary>
    void LogError(Exception ex);

    /// <summary>
    /// Called to load the plugin.
    /// </summary>
    void Load();

    /// <summary>
    /// Called to unload the plugin.
    /// </summary>
    void Unload();
}
public interface IDevkitServerColorPlugin : IDevkitServerPlugin
{
    /// <summary>
    /// Color a plugin uses with logging.
    /// </summary>
    /// <remarks>Default: rgb(204, 153, 255), <see cref="Plugin.DefaultColor"/>.</remarks>
    Color Color { get; }
}