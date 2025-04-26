using SDG.Framework.Modules;
using System.Diagnostics.CodeAnalysis;

namespace DevkitServer.API;

/// <summary>
/// A representation of a <see cref="IDevkitServerPlugin"/> as a <see cref="Module"/>.
/// </summary>
/// <remarks>This can be used for compatability for libraries that expect a module.</remarks>
internal class PluginModule : Module, IEquatable<Module>
{
    /// <summary>
    /// The plugin represented by this module.
    /// </summary>
    public IDevkitServerPlugin Plugin { get; }

    protected internal PluginModule(IDevkitServerPlugin plugin) : base(new ModuleConfig
    {
        Name = plugin.Name,
        IsEnabled = false
    })
    {
        Plugin = plugin;
    }

    /// <inheritdoc />
    public bool Equals(Module other)
    {
        return other is PluginModule module && Plugin.Equals(module.Plugin);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is PluginModule module && Plugin.Equals(module.Plugin);
    }

    /// <inheritdoc />
    public override int GetHashCode() => Plugin.GetHashCode();

    /// <inheritdoc />
    public override string ToString() => Plugin.Name;
}

/// <summary>
/// Extensions to convert a 'mock' <see cref="Module"/> to a <see cref="IDevkitServerPlugin"/>.
/// </summary>
public static class PluginModuleExtensions
{
    /// <summary>
    /// Try to get this module as a plugin module.
    /// </summary>
    public static IDevkitServerPlugin? GetDevkitServerPlugin(this Module module)
    {
        return (module as PluginModule)?.Plugin;
    }

    /// <summary>
    /// Try to get this module as a plugin module.
    /// </summary>
    public static bool TryGetDevkitServerPlugin(this Module module, [MaybeNullWhen(false)] out IDevkitServerPlugin plugin)
    {
        if (module is PluginModule pm)
        {
            plugin = pm.Plugin;
            return true;
        }

        plugin = null;
        return false;
    }
}