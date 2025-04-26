#if CLIENT
using DevkitServer;
using DevkitServer.API;
using DevkitServer.Plugins;
using SDG.Framework.Modules;

// ReSharper disable once CheckNamespace
namespace DanielWillett.UITools.API.Extensions;

/// <summary>
/// Stores cached information about a UI extension.
/// </summary>
public class DevkitServerUIExtensionInfo : UIExtensionInfo
{
    /// <summary>
    /// Plugin the extension is from.
    /// </summary>
    public IDevkitServerPlugin? Plugin { get; }

    internal DevkitServerUIExtensionInfo(Type implementationType, Type parentType, int priority, IDevkitServerPlugin? plugin)
        : base(implementationType, parentType, priority, plugin == null ? DevkitServerModule.Module : PluginLoader.GetMockModule(plugin))
    {
        Plugin = plugin;
    }

    internal DevkitServerUIExtensionInfo(Type implementationType, Type parentType, int priority, Module module)
        : base(implementationType, parentType, priority, module)
    {
        Plugin = null;
    }
}
#endif