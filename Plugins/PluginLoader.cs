using DevkitServer.API;
using DevkitServer.API.Permissions;
using DevkitServer.Commands.Subsystem;
using DevkitServer.Configuration;
using System.Reflection;
using DevkitServer.Multiplayer.Networking;
using DevkitServer.Patches;
using HarmonyLib;
using Action = System.Action;

namespace DevkitServer.Plugins;
public static class PluginLoader
{
    public static readonly string PluginDirectory = Path.Combine(DevkitServerConfig.Directory, "Plugins");
    public static readonly string LibraryDirectory = Path.Combine(DevkitServerConfig.Directory, "Libraries");

    private static readonly CachedMulticastEvent<Action<IDevkitServerPlugin>> OnPluginLoadedEvent = new CachedMulticastEvent<Action<IDevkitServerPlugin>>(typeof(PluginLoader), nameof(OnPluginLoaded));
    private static readonly CachedMulticastEvent<Action<IDevkitServerPlugin>> OnPluginUnloadedEvent = new CachedMulticastEvent<Action<IDevkitServerPlugin>>(typeof(PluginLoader), nameof(OnPluginUnloaded));
    private static readonly CachedMulticastEvent<Action> OnPluginsLoadedEvent = new CachedMulticastEvent<Action>(typeof(PluginLoader), nameof(OnPluginsLoaded));
    private static readonly CachedMulticastEvent<Action> OnPluginsUnloadingEvent = new CachedMulticastEvent<Action>(typeof(PluginLoader), nameof(OnPluginsUnloading));

    private static readonly List<IDevkitServerPlugin> PluginsIntl = new List<IDevkitServerPlugin>();
    private static readonly List<PluginLibrary> LibrariesIntl = new List<PluginLibrary>();
    private static readonly List<PluginAssembly> AssembliesIntl = new List<PluginAssembly>();

    /// <summary>
    /// Invoked a plugin loads, either on startup or by <see cref="RegisterPlugin"/>.
    /// </summary>
    /// <remarks>Exceptions will not break execution.</remarks>
    public static event Action<IDevkitServerPlugin> OnPluginLoaded
    {
        add => OnPluginLoadedEvent.Add(value);
        remove => OnPluginLoadedEvent.Remove(value);
    }

    /// <summary>
    /// Invoked a plugin unloads, either on startup, shutdown or by <see cref="DeregisterPlugin"/>.
    /// </summary>
    /// <remarks>All plugins in an assembly will unload if any fail to load on startup, invoking this event. Exceptions will not break execution.</remarks>
    public static event Action<IDevkitServerPlugin> OnPluginUnloaded
    {
        add => OnPluginUnloadedEvent.Add(value);
        remove => OnPluginUnloadedEvent.Remove(value);
    }

    /// <summary>
    /// Invoked after all plugins load on startup.
    /// </summary>
    /// <remarks>Exceptions will not break execution.</remarks>
    public static event Action OnPluginsLoaded
    {
        add => OnPluginsLoadedEvent.Add(value);
        remove => OnPluginsLoadedEvent.Remove(value);
    }

    /// <summary>
    /// Invoked before all plugins unload on shutdown.
    /// </summary>
    /// <remarks>Exceptions will not break execution.</remarks>
    public static event Action OnPluginsUnloading
    {
        add => OnPluginsUnloadingEvent.Add(value);
        remove => OnPluginsUnloadingEvent.Remove(value);
    }

    /// <summary>
    /// All currently loaded plugins.
    /// </summary>
    public static IReadOnlyList<IDevkitServerPlugin> Plugins { get; } = PluginsIntl.AsReadOnly();
    /// <summary>
    /// All loaded libraries.
    /// </summary>
    public static IReadOnlyList<PluginLibrary> Libraries { get; } = LibrariesIntl.AsReadOnly();
    /// <summary>
    /// All loaded assemblies and their plugins and patching info.
    /// </summary>
    public static IReadOnlyList<PluginAssembly> Assemblies { get; } = AssembliesIntl.AsReadOnly();
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
            for (int i = 0; i < PluginsIntl.Count; i++)
            {
                IDevkitServerPlugin plugin2 = PluginsIntl[i];
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
            PluginsIntl.Add(plugin);
            Assembly asm = plugin.GetType().Assembly;
            for (int i = 0; i < AssembliesIntl.Count; ++i)
            {
                if (AssembliesIntl[i].Assembly == asm)
                {
                    AssembliesIntl[i].AddPlugin(plugin);
                    goto skipAssemblyAdd;
                }
            }

            PluginAssembly assemblyWrapper = new PluginAssembly(asm);
            assemblyWrapper.AddPlugin(plugin);
            AssembliesIntl.Add(assemblyWrapper);
            skipAssemblyAdd:
            OnPluginLoadedEvent.TryInvoke(plugin);
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
                OnPluginUnloadedEvent.TryInvoke(plugin);
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
        for (int i = PluginsIntl.Count - 1; i >= 0; --i)
        {
            if (PluginsIntl[i].Equals(plugin))
            {
                plugin = PluginsIntl[i];
                PluginAdvertising.Get().RemovePlugin(plugin.MenuName);
                PluginsIntl.RemoveAt(i);
                Assembly asm = plugin.GetType().Assembly;
                for (int j = 0; j < AssembliesIntl.Count; ++j)
                {
                    PluginAssembly assembly = AssembliesIntl[j];
                    if (assembly.Assembly == asm)
                    {
                        assembly.RemovePlugin(plugin);
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
                    OnPluginUnloadedEvent.TryInvoke(plugin);
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
                LibrariesIntl.Add(lib);
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
                Logger.LogInfo("[LOAD PLUGINS] Found " + pluginsTemp.Count.Format() + " plugin" + (pluginsTemp.Count == 1 ? string.Empty : "s") +
                               " from " + Path.GetFileName(pluginDll).Format() + " (" + assembly.GetName().Version.Format() + ")");
                PluginAssembly? info = AssembliesIntl.Find(x => x.Assembly == assembly);
                if (info == null)
                {
                    info = new PluginAssembly(assembly);
                    AssembliesIntl.Add(info);
                }
                for (int k = 0; k < pluginsTemp.Count; ++k)
                    info.AddPlugin(pluginsTemp[k]);
                PluginsIntl.AddRange(pluginsTemp);
            }
            else
            {
                Logger.LogInfo("[LOAD PLUGINS] Found no plugins from " + Path.GetFileName(pluginDll).Format() + " (" + assembly.GetName().Version.Format() + ")");
            }
        }

        bool dup = true;
        while (dup)
        {
            dup = false;
            for (int i = 0; i < PluginsIntl.Count; i++)
            {
                IDevkitServerPlugin plugin = PluginsIntl[i];
                for (int j = i + 1; j < PluginsIntl.Count; ++j)
                {
                    IDevkitServerPlugin plugin2 = PluginsIntl[j];
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
            Color color = plugin.GetColor();
            string src = "LOAD " + plugin.Name.ToUpperInvariant().Colorize(color);
            try
            {
                AssertPluginValid(plugin);
                plugin.Load();
                OnPluginLoadedEvent.TryInvoke(plugin);
                Logger.LogInfo($"[{src}] Loaded successfully.");
            }
            catch (Exception ex)
            {
                Logger.LogError("Plugin failed to load: " + plugin.GetType().Format() + ".", method: src);
                Logger.LogError(ex, method: src);
                for (int j = i; j >= 0; --j)
                {
                    IDevkitServerPlugin plugin2 = pluginsTemp[j];
                    if (plugin.Assembly == plugin2.Assembly)
                    {
                        try
                        {
                            plugin2.Unload();
                            OnPluginUnloadedEvent.TryInvoke(plugin2);
                            Logger.LogInfo($"[{src}]  Unloaded {plugin2.GetType().Format()}.");
                        }
                        catch (Exception ex2)
                        {
                            Logger.LogError($" Plugin failed to unload: {plugin2.GetType().Format()}.", method: src);
                            Logger.LogError(ex2, method: src);
                        }
                    }
                }
                plugin.Assembly.Unpatch();
                pluginsTemp.Clear();
                Logger.LogError("Failed to load assembly: " + plugin.Assembly.Assembly.GetName().Name.Format() + ".", method: src);
                break;
            }
            plugin.Assembly.ReflectAndPatch();
        }

        IPluginAdvertising advertisingFramework = PluginAdvertising.Get();

        string devkitServer = DevkitServerModule.MainLocalization.Translate("Name");
        advertisingFramework.RemovePlugins(advertisingFramework.GetPluginNames().Where(x => !x.Equals(devkitServer, StringComparison.Ordinal)));
        advertisingFramework.AddPlugins(PluginsIntl.OrderBy(x => x.Name).Select(x => x.MenuName).Where(x => !string.IsNullOrEmpty(x)));

        CommandHandler.InitImpl();

        OnPluginsLoadedEvent.TryInvoke();
    }
    internal static void Unload()
    {
        OnPluginsUnloadingEvent.TryInvoke();

        foreach (IDevkitServerPlugin plugin in PluginsIntl.ToList())
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
    }

    public static IDevkitServerPlugin? FindPluginForMember(MemberInfo member)
    {
        Type? relaventType = member as Type ?? member.DeclaringType;

        if (relaventType == null)
            return null;

        // check if the assembly just has one plugin
        Assembly asm = relaventType.Assembly;
        if (asm == Assembly.GetExecutingAssembly()) return null;
        for (int i = 0; i < AssembliesIntl.Count; ++i)
        {
            if (AssembliesIntl[i].Assembly == asm)
            {
                if (AssembliesIntl[i].Plugins.Count == 1)
                    return AssembliesIntl[i].Plugins[0];
                break;
            }
        }

        IDevkitServerPlugin? plugin = null;

        // find attribute on member
        if (Attribute.GetCustomAttribute(member, typeof(PluginIdentifierAttribute)) is PluginIdentifierAttribute { PluginType: { } ptype })
        {
            for (int i = 0; i < PluginsIntl.Count; ++i)
            {
                if (ptype == PluginsIntl[i].GetType())
                {
                    plugin = PluginsIntl[i];
                    goto checkAssembly;
                }
            }
            for (int i = 0; i < PluginsIntl.Count; ++i)
            {
                if (ptype.IsInstanceOfType(PluginsIntl[i]))
                {
                    plugin = PluginsIntl[i];
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
            for (int i = 0; i < PluginsIntl.Count; ++i)
            {
                if (type.IsInstanceOfType(PluginsIntl[i]))
                    return PluginsIntl[i];
            }
            for (int i = 0; i < PluginsIntl.Count; ++i)
            {
                if (type.IsInstanceOfType(PluginsIntl[i]))
                    return PluginsIntl[i];
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
        if (plugin.Assembly.Assembly != asm)
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
public class PluginAssembly
{
    private readonly List<IDevkitServerPlugin> _plugins = new List<IDevkitServerPlugin>();
    private readonly List<NetInvokerInfo> _netCalls = new List<NetInvokerInfo>();
    private readonly List<NetMethodInfo> _netMethods = new List<NetMethodInfo>();
    public IReadOnlyList<IDevkitServerPlugin> Plugins { get; }
    public Assembly Assembly { get; }
    public Harmony Patcher { get; internal set; }
    public bool HasReflected { get; private set; }
    public bool HasPatched { get; private set; }
    public string HarmonyId => Patcher.Id;
    public IReadOnlyList<NetInvokerInfo> NetCalls { get; }
    public IReadOnlyList<NetMethodInfo> NetMethods { get; }
    public PluginAssembly(Assembly assembly)
    {
        Assembly = assembly;
        Plugins = _plugins.AsReadOnly();
        NetCalls = _netCalls.AsReadOnly();
        NetMethods = _netMethods.AsReadOnly();
        Patcher = new Harmony(PatchesMain.HarmonyId + ".assembly." + assembly.GetName().Name.ToLowerInvariant());
    }
    internal void AddPlugin(IDevkitServerPlugin plugin)
    {
        plugin.Assembly = this;
        _plugins.Add(plugin);
    }

    internal void RemovePlugin(IDevkitServerPlugin plugin) => _plugins.Remove(plugin);

    internal void ReflectAndPatch()
    {
        ThreadUtil.assertIsGameThread();

        if (!HasReflected)
        {
            HasReflected = true;
            CreateDirectoryAttribute.CreateInAssembly(Assembly);
            _netMethods.Clear();
            _netCalls.Clear();
            NetFactory.Reflect(Assembly,
#if SERVER
                NetCallSource.FromClient
#else
                NetCallSource.FromServer
#endif
                , _netMethods, _netCalls
            );
        }
        if (!HasPatched)
        {
            HasPatched = true;
            try
            {
                Patcher.PatchAll(Assembly);
                if (Plugins.Count > 0)
                    Plugins[0].LogInfo($"Applied all patches in {Assembly.Location.Format()}.");
                else
                    Logger.LogError($"Applied all patches in {Assembly.Location.Format()}.", method: Assembly.GetName().Name.ToUpperInvariant());
            }
            catch (Exception ex)
            {
                if (Plugins.Count > 0)
                {
                    Plugins[0].LogError($"Error while patching in assembly at {Assembly.Location.Format()}.");
                    Plugins[0].LogError(ex);
                }
                else
                {
                    string src = Assembly.GetName().Name.ToUpperInvariant();
                    Logger.LogError($"Error while patching in assembly at {Assembly.Location.Format()}.", method: src);
                    Logger.LogError(ex, method: src);
                }
            }
        }
    }
    internal void Unpatch()
    {
        ThreadUtil.assertIsGameThread();

        if (!HasPatched) return;
        HasPatched = false;

        try
        {
            Patcher.UnpatchAll(HarmonyId);
            if (Plugins.Count > 0)
                Plugins[0].LogInfo($"Removed all patches in {Assembly.Location.Format()}.");
            else
                Logger.LogError($"Removed all patches in {Assembly.Location.Format()}.", method: Assembly.GetName().Name.ToUpperInvariant());
        }
        catch (Exception ex)
        {
            if (Plugins.Count > 0)
            {
                Plugins[0].LogError($"Error while unpatching in assembly at {Assembly.Location.Format()}.");
                Plugins[0].LogError(ex);
            }
            else
            {
                string src = Assembly.GetName().Name.ToUpperInvariant();
                Logger.LogError($"Error while unpatching in assembly at {Assembly.Location.Format()}.", method: src);
                Logger.LogError(ex, method: src);
            }
        }
    }
}