using DevkitServer.API;
using DevkitServer.API.Permissions;
using DevkitServer.Commands.Subsystem;
using DevkitServer.Configuration;
using System.Reflection;
using Action = System.Action;

namespace DevkitServer.Plugins;
public static class PluginLoader
{
    public static readonly string PluginDirectory = Path.Combine(DevkitServerConfig.FilePath, "Plugins");
    public static readonly string LibraryDirectory = Path.Combine(DevkitServerConfig.FilePath, "Libraries");

    private static readonly List<IDevkitServerPlugin> _plugins = new List<IDevkitServerPlugin>();
    private static readonly List<PluginLibrary> _libs = new List<PluginLibrary>();
    private static readonly List<PluginAssembly> _pluginWrappers = new List<PluginAssembly>();
    private static readonly IReadOnlyList<IDevkitServerPlugin> _roPlugins = _plugins.AsReadOnly();
    private static readonly IReadOnlyList<PluginLibrary> _roLibs = _libs.AsReadOnly();

    /// <summary>
    /// Invoked a plugin loads, either on startup or by <see cref="RegisterPlugin"/>.
    /// </summary>
    /// <remarks>Exceptions will not break execution.</remarks>
    public static event Action<IDevkitServerPlugin>? OnPluginLoaded;

    /// <summary>
    /// Invoked a plugin unloads, either on startup, shutdown or by <see cref="DeregisterPlugin"/>.
    /// </summary>
    /// <remarks>All plugins in an assembly will unload if any fail to load on startup, invoking this event. Exceptions will not break execution.</remarks>
    public static event Action<IDevkitServerPlugin>? OnPluginUnloaded;

    /// <summary>
    /// Invoked after all plugins load on startup.
    /// </summary>
    /// <remarks>Exceptions will not break execution.</remarks>
    public static event Action? OnPluginsLoaded;

    /// <summary>
    /// Invoked after all plugins unload on shutdown.
    /// </summary>
    /// <remarks>Exceptions will not break execution.</remarks>
    public static event Action? OnPluginsUnloaded;

    /// <summary>
    /// All currently loaded plugins.
    /// </summary>
    public static IReadOnlyList<IDevkitServerPlugin> Plugins => _roPlugins;
    /// <summary>
    /// All loaded libraries.
    /// </summary>
    public static IReadOnlyList<PluginLibrary> Libraries => _roLibs;
    private static void AssertPluginValid(IDevkitServerPlugin plugin)
    {
        if (string.IsNullOrWhiteSpace(plugin.DataDirectory) || !Uri.TryCreate(plugin.DataDirectory, UriKind.Absolute, out Uri uri) || !uri.IsFile)
        {
            plugin.LogError("DataDirectory invalid: " + plugin.DataDirectory.Format() + ".");
            throw new Exception("DataDirectory invalid: \"" + plugin.DataDirectory + "\".");
        }
        if (string.IsNullOrWhiteSpace(plugin.LocalizationDirectory) || !Uri.TryCreate(plugin.LocalizationDirectory, UriKind.Absolute, out uri) || !uri.IsFile)
        {
            plugin.LogError("LocalizationDirectory invalid: " + plugin.LocalizationDirectory.Format() + ".");
            throw new Exception("LocalizationDirectory invalid: \"" + plugin.LocalizationDirectory + "\".");
        }
        if (plugin is Plugin p && (string.IsNullOrWhiteSpace(p.MainLocalizationDirectory) || !Uri.TryCreate(p.MainLocalizationDirectory, UriKind.Absolute, out uri) || !uri.IsFile))
        {
            plugin.LogError("MainLocalizationDirectory invalid: " + p.MainLocalizationDirectory.Format() + ".");
            throw new Exception("MainLocalizationDirectory invalid: \"" + p.MainLocalizationDirectory + "\".");
        }

        if (string.IsNullOrWhiteSpace(plugin.PermissionPrefix))
        {
            plugin.LogError("Invalid PermissionPrefix: " + plugin.PermissionPrefix.Format() + ". PermissionPrefix can not be empty.");
            throw new Exception("Plugin " + plugin.Name + "'s 'PermissionPrefix' can not be empty.");
        }
        if (plugin.PermissionPrefix[0] is '-' or '+')
        {
            plugin.LogError("Invalid PermissionPrefix: " + plugin.PermissionPrefix.Format() + ". PermissionPrefix can not start with a '-' or '+'.");
            throw new Exception("Plugin " + plugin.Name + "'s 'PermissionPrefix' can not start with a '-' or '+'.");
        }

        if (plugin.PermissionPrefix.IndexOf('.') != -1)
        {
            plugin.LogError("Invalid PermissionPrefix: " + plugin.PermissionPrefix.Format() + ". PermissionPrefix can not contain a period.");
            throw new Exception("Plugin " + plugin.Name + "'s 'PermissionPrefix' can not contain a period.");
        }
        if (plugin.PermissionPrefix.Equals(Permission.CoreModuleCode, StringComparison.InvariantCultureIgnoreCase))
        {
            plugin.LogError("Invalid PermissionPrefix: " + plugin.PermissionPrefix.Format() + ". PermissionPrefix can not equal " + Permission.CoreModuleCode.Format() + ".");
            throw new Exception("Plugin " + plugin.Name + "'s 'PermissionPrefix' can not equal " + Permission.CoreModuleCode + ".");
        }
        if (plugin.PermissionPrefix.Equals(Permission.DevkitServerModuleCode, StringComparison.InvariantCultureIgnoreCase))
        {
            plugin.LogError("Invalid PermissionPrefix: " + plugin.PermissionPrefix.Format() + ". PermissionPrefix can not equal " + Permission.DevkitServerModuleCode.Format() + ".");
            throw new Exception("Plugin " + plugin.Name + "'s 'PermissionPrefix' can not equal " + Permission.DevkitServerModuleCode + ".");
        }
        string defaultModuleName = DevkitServerModule.MainLocalization.format("Name");
        if (plugin.MenuName.Equals(defaultModuleName, StringComparison.InvariantCultureIgnoreCase))
        {
            plugin.LogError("Invalid MenuName: " + plugin.MenuName.Format() + ". MenuName can't be equal to " + defaultModuleName.Format() + ".");
            throw new Exception("Plugin " + plugin.Name + "'s 'MenuName' can't be equal to \"" + defaultModuleName + "\".");
        }
    }
    /// <exception cref="AggregateException">Error(s) loading the plugin.</exception>
    /// <exception cref="ArgumentNullException"/>
    public static void RegisterPlugin(IDevkitServerPlugin plugin)
    {
        if (plugin == null)
            throw new ArgumentNullException(nameof(plugin));
        bool dup = true;
        while (dup)
        {
            dup = false;
            for (int i = 0; i < _plugins.Count; i++)
            {
                IDevkitServerPlugin plugin2 = _plugins[i];
                if (plugin2.PermissionPrefix.Equals(plugin.PermissionPrefix, StringComparison.InvariantCultureIgnoreCase))
                {
                    plugin.PermissionPrefix = "_" + plugin.PermissionPrefix;
                    plugin.LogWarning("Conflicting permission prefix with " + plugin2.Format() +
                                      " (" + plugin2.PermissionPrefix.Format() + "). Overriding to " + plugin.PermissionPrefix.Format() + ".");
                    dup = true;
                }
            }
        }

        try
        {
            plugin.Load();
            AssertPluginValid(plugin);
            PluginAdvertising.Get().AddPlugin(plugin.MenuName);
            _plugins.Add(plugin);
            Assembly asm = plugin.GetType().Assembly;
            for (int i = 0; i < _pluginWrappers.Count; ++i)
            {
                if (_pluginWrappers[i].Assembly == asm)
                {
                    _pluginWrappers[i].Plugins.Add(plugin);
                    goto skipAssemblyAdd;
                }
            }

            PluginAssembly assemblyWrapper = new PluginAssembly(asm);
            assemblyWrapper.Plugins.Add(plugin);
            _pluginWrappers.Add(assemblyWrapper);
            skipAssemblyAdd:
            TryInvokeOnPluginLoaded(plugin);
            Logger.LogInfo("[LOAD " + plugin.Name.ToUpperInvariant() + "] Loaded " + plugin.Name.Colorize(plugin is IDevkitServerColorPlugin p ? p.Color : Plugin.DefaultColor));
        }
        catch (Exception ex)
        {
            Logger.LogError("Plugin failed to load: " + plugin.GetType().Format() + ".", method: "LOAD " + plugin.Name.ToUpperInvariant());
            Logger.LogError(ex, method: "LOAD " + plugin.Name.ToUpperInvariant());
            Exception? unloadEx = null;
            try
            {
                plugin.Unload();
                TryInvokeOnPluginUnloaded(plugin);
            }
            catch (Exception ex2)
            {
                unloadEx = ex2;
                Logger.LogError("Plugin failed to unload: " + plugin.GetType().Format() + ".", method: "LOAD " + plugin.Name.ToUpperInvariant());
                Logger.LogError(ex2, method: "LOAD " + plugin.Name.ToUpperInvariant());
            }
            throw new AggregateException("Plugin failed to load and unload: " + plugin.GetType().FullName + ".", unloadEx != null ? new Exception[] { ex, unloadEx } : new Exception[] { ex });
        }
    }

    /// <exception cref="AggregateException">Error(s) unloading the plugin.</exception>
    /// <exception cref="ArgumentNullException"/>
    public static void DeregisterPlugin(IDevkitServerPlugin plugin)
    {
        if (plugin == null)
            throw new ArgumentNullException(nameof(plugin));
        for (int i = _plugins.Count - 1; i >= 0; --i)
        {
            if (_plugins[i].Equals(plugin))
            {
                plugin = _plugins[i];
                PluginAdvertising.Get().RemovePlugin(plugin.MenuName);
                _plugins.RemoveAt(i);
                Assembly asm = plugin.GetType().Assembly;
                for (int j = 0; j < _pluginWrappers.Count; ++j)
                {
                    if (_pluginWrappers[j].Assembly == asm)
                    {
                        _pluginWrappers[j].Plugins.Remove(plugin);
                        break;
                    }
                }

                try
                {
                    plugin.Unload();
                }
                catch (Exception ex)
                {
                    Logger.LogError("Plugin failed to unload: " + plugin.GetType().Format() + ".", method: "LOAD " + plugin.Name.ToUpperInvariant());
                    Logger.LogError(ex, method: "LOAD " + plugin.Name.ToUpperInvariant());
                    throw new AggregateException("Plugin failed to unload: " + plugin.GetType().FullName + ".", ex);
                }
                finally
                {
                    TryInvokeOnPluginUnloaded(plugin);
                }
                break;
            }
        }
    }
    internal static void Load()
    {
        Directory.CreateDirectory(PluginDirectory);
        Directory.CreateDirectory(LibraryDirectory);

        string[] plugins = Directory.GetFiles(PluginDirectory, "*.dll", SearchOption.TopDirectoryOnly);
        string[] libraries = Directory.GetFiles(LibraryDirectory, "*.dll", SearchOption.TopDirectoryOnly);

        foreach (string libDll in libraries)
        {
            AssemblyName asmName = AssemblyName.GetAssemblyName(libDll);
            Assembly? asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(x => AssemblyName.ReferenceMatchesDefinition(x.GetName(), asmName));
            if (asm != null)
            {
                Logger.LogInfo("Library already loaded: " + Path.GetFileName(libDll).Format() + ", skipped.");
                continue;
            }
            asm = Assembly.LoadFrom(libDll);
            if (asm != null)
            {
                Logger.LogInfo("Loaded Library: " + Path.GetFileName(libDll).Format() + ".");
                PluginLibrary lib = new PluginLibrary(asm, libDll);
                _libs.Add(lib);
            }
        }

        List<IDevkitServerPlugin> pluginsTemp = new List<IDevkitServerPlugin>();
        foreach (string pluginDll in plugins)
        {
            Type[] types;
            Assembly assembly = Assembly.LoadFrom(pluginDll);
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types;
            }
            foreach (Type type in types
                         .Where(x => typeof(IDevkitServerPlugin).IsAssignableFrom(x))
                         .OrderByDescending(x =>
                             Attribute.GetCustomAttribute(x, typeof(LoadPriorityAttribute)) is LoadPriorityAttribute attr
                                 ? attr.Priority
                                 : 0
                         ))
            {
                if (type.IsAbstract || Attribute.IsDefined(type, typeof(IgnoreAttribute)))
                {
                    Logger.LogInfo("[LOAD " + assembly.GetName().Name.ToUpperInvariant() + "] Skipped loading " + type.Format() + ".");
                    continue;
                }

                try
                {
                    pluginsTemp.Add((IDevkitServerPlugin)Activator.CreateInstance(type));
                }
                catch (Exception ex)
                {
                    Logger.LogError("Failed to load assembly:" + pluginDll.Format() + ".", method: "LOAD " + type.Name.ToUpperInvariant());
                    Logger.LogError(" Plugin failed to load: " + type.Format() + ".", method: "LOAD " + type.Name.ToUpperInvariant());
                    Logger.LogError(ex);
                    pluginsTemp.RemoveAll(x => x.GetType().Assembly == assembly);
                    break;
                }
            }

            if (pluginsTemp.Count > 0)
            {
                Logger.LogInfo("Found " + pluginsTemp.Count.Format() + " plugin" + (pluginsTemp.Count == 1 ? string.Empty : "s") +
                               " from " + Path.GetFileName(pluginDll).Format() + " (" + assembly.GetName().Version.Format() + ")");
                PluginAssembly info = new PluginAssembly(assembly);
                info.Plugins.AddRange(pluginsTemp);
                _pluginWrappers.Add(info);
                _plugins.AddRange(pluginsTemp);
            }
            else
            {
                Logger.LogInfo("Found no plugins from " + Path.GetFileName(pluginDll).Format() + " (" + assembly.GetName().Version.Format() + ")");
            }
        }

        bool dup = true;
        while (dup)
        {
            dup = false;
            for (int i = 0; i < _plugins.Count; i++)
            {
                IDevkitServerPlugin plugin = _plugins[i];
                for (int j = i + 1; j < _plugins.Count; ++j)
                {
                    IDevkitServerPlugin plugin2 = _plugins[j];
                    if (plugin2.PermissionPrefix.Equals(plugin.PermissionPrefix, StringComparison.InvariantCultureIgnoreCase))
                    {
                        plugin2.PermissionPrefix = "_" + plugin2.PermissionPrefix;
                        plugin2.LogWarning("Conflicting permission prefix with " + plugin.Format() +
                                          " (" + plugin.PermissionPrefix.Format() + "). Overriding to " + plugin2.PermissionPrefix.Format() + ".");
                        dup = true;
                    }
                }
            }
        }

        UserPermissions.InitHandlers();

        pluginsTemp = pluginsTemp.OrderByDescending(x =>
            Attribute.GetCustomAttribute(x.GetType(), typeof(LoadPriorityAttribute)) is
                LoadPriorityAttribute attr
                ? attr.Priority
                : 0
        ).ToList();
        for (int i = 0; i < pluginsTemp.Count; ++i)
        {
            IDevkitServerPlugin plugin = pluginsTemp[i];
            try
            {
                plugin.Load();
                AssertPluginValid(plugin);
                TryInvokeOnPluginLoaded(plugin);
                Logger.LogInfo("[LOAD " + plugin.Name.ToUpperInvariant() + "] Loaded " + plugin.Name.Colorize(plugin is IDevkitServerColorPlugin p ? p.Color : Plugin.DefaultColor));
            }
            catch (Exception ex)
            {
                Logger.LogError("Plugin failed to load: " + plugin.GetType().Format() + ".", method: "LOAD " + plugin.Name.ToUpperInvariant());
                Logger.LogError(ex, method: "LOAD " + plugin.Name.ToUpperInvariant());
                for (int j = i; j >= 0; --j)
                {
                    IDevkitServerPlugin plugin2 = pluginsTemp[j];
                    if (plugin.Assembly == plugin2.Assembly)
                    {
                        try
                        {
                            plugin2.Unload();
                            TryInvokeOnPluginUnloaded(plugin2);
                        }
                        catch (Exception ex2)
                        {
                            Logger.LogError(" Plugin failed to unload: " + plugin2.GetType().Format() + ".",
                                method: "LOAD " + plugin2.Name.ToUpperInvariant());
                            Logger.LogError(ex2, method: "LOAD " + plugin2.Name.ToUpperInvariant());
                        }
                    }
                }

                pluginsTemp.Clear();
                Logger.LogError("Failed to load assembly: " + plugin.Assembly.GetName().Name.Format() + ".", method: "LOAD " + plugin.Name.ToUpperInvariant());
                break;
            }
        }

        foreach (IDevkitServerPlugin plugin in _plugins.OrderBy(x => x.Name))
            PluginAdvertising.Get().AddPlugin(plugin.MenuName);

        CommandHandler.InitImpl();
        TryInvokeOnPluginsLoaded();
    }
    internal static void Unload()
    {
        foreach (IDevkitServerPlugin plugin in _plugins.ToList())
        {
            try
            {
                DeregisterPlugin(plugin);
            }
            catch
            {
                // ignored
            }
        }

        TryInvokeOnPluginsUnloaded();
    }
    private static void TryInvokeOnPluginLoaded(IDevkitServerPlugin plugin)
    {
        if (OnPluginLoaded == null)
            return;
        foreach (Action<IDevkitServerPlugin> action in OnPluginLoaded.GetInvocationList().Cast<Action<IDevkitServerPlugin>>())
        {
            try
            {
                action(plugin);
            }
            catch (Exception ex)
            {
                Logger.LogError("A plugin threw an exception from " + nameof(PluginLoader) + "." + nameof(OnPluginLoaded) + ":");
                Logger.LogError(ex);
            }
        }
    }
    private static void TryInvokeOnPluginUnloaded(IDevkitServerPlugin plugin)
    {
        if (OnPluginUnloaded == null)
            return;
        foreach (Action<IDevkitServerPlugin> action in OnPluginUnloaded.GetInvocationList().Cast<Action<IDevkitServerPlugin>>())
        {
            try
            {
                action(plugin);
            }
            catch (Exception ex)
            {
                Logger.LogError("A plugin threw an exception from " + nameof(PluginLoader) + "." + nameof(OnPluginUnloaded) + ":");
                Logger.LogError(ex);
            }
        }
    }
    private static void TryInvokeOnPluginsLoaded()
    {
        if (OnPluginsLoaded == null)
            return;
        foreach (Action action in OnPluginsLoaded.GetInvocationList().Cast<Action>())
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Logger.LogError("A plugin threw an exception from " + nameof(PluginLoader) + "." + nameof(OnPluginsLoaded) + ":");
                Logger.LogError(ex);
            }
        }
    }
    private static void TryInvokeOnPluginsUnloaded()
    {
        if (OnPluginsUnloaded == null)
            return;
        foreach (Action action in OnPluginsUnloaded.GetInvocationList().Cast<Action>())
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Logger.LogError("A plugin threw an exception from " + nameof(PluginLoader) + "." + nameof(OnPluginsUnloaded) + ":");
                Logger.LogError(ex);
            }
        }
    }

    public static IDevkitServerPlugin? FindPluginForMember(MemberInfo member)
    {
        Type? relaventType = member as Type ?? member.DeclaringType;

        if (relaventType == null)
            return null;

        // check if the assembly just has one plugin
        Assembly asm = relaventType.Assembly;
        if (asm == Assembly.GetExecutingAssembly()) return null;
        for (int i = 0; i < _pluginWrappers.Count; ++i)
        {
            if (_pluginWrappers[i].Assembly == asm)
            {
                if (_pluginWrappers[i].Plugins.Count == 1)
                    return _pluginWrappers[i].Plugins[0];
                break;
            }
        }

        IDevkitServerPlugin? plugin = null;

        // find attribute on member
        if (Attribute.GetCustomAttribute(member, typeof(PluginIdentifierAttribute)) is PluginIdentifierAttribute { PluginType: { } ptype })
        {
            for (int i = 0; i < _plugins.Count; ++i)
            {
                if (ptype == _plugins[i].GetType())
                {
                    plugin = _plugins[i];
                    goto checkAssembly;
                }
            }
            for (int i = 0; i < _plugins.Count; ++i)
            {
                if (ptype.IsInstanceOfType(_plugins[i]))
                {
                    plugin = _plugins[i];
                    goto checkAssembly;
                }
            }
        }

        // check for nested type
        for (Type? declType = relaventType; declType != null;)
        {
            plugin = FindPluginForType(declType);
            if (plugin != null)
                goto checkAssembly;
            declType = declType.DeclaringType;
        }

        IDevkitServerPlugin? FindPluginInstanceOfType(Type type)
        {
            for (int i = 0; i < _plugins.Count; ++i)
            {
                if (type.IsInstanceOfType(_plugins[i]))
                    return _plugins[i];
            }
            for (int i = 0; i < _plugins.Count; ++i)
            {
                if (type.IsInstanceOfType(_plugins[i]))
                    return _plugins[i];
            }

            return null;
        }

        IDevkitServerPlugin? FindPluginForType(Type type)
        {
            if (FindPluginInstanceOfType(type) is { } p)
                return p;
            return Attribute.GetCustomAttribute(type, typeof(PluginIdentifierAttribute)) is PluginIdentifierAttribute { PluginType: { } ptype }
                ? FindPluginInstanceOfType(ptype)
                : null;
        }

        checkAssembly:
        if (plugin == null)
            return null;
        if (plugin.Assembly != asm)
            return null;

        return plugin;
    }
}

public class PluginLibrary
{
    public Assembly Assembly { get; }
    public string File { get; }
    public PluginLibrary(Assembly assembly, string file)
    {
        Assembly = assembly;
        File = file;
    }
}
internal class PluginAssembly
{
    public List<IDevkitServerPlugin> Plugins { get; } = new List<IDevkitServerPlugin>();
    public Assembly Assembly { get; }

    public PluginAssembly(Assembly assembly)
    {
        Assembly = assembly;
    }
}