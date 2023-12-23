using DevkitServer.API;
using DevkitServer.API.Abstractions;
using DevkitServer.API.Cartography.ChartColorProviders;
using DevkitServer.API.Multiplayer;
using DevkitServer.API.Permissions;
using DevkitServer.Compat;
using DevkitServer.Configuration;
using DevkitServer.Core.Commands.Subsystem;
using DevkitServer.Core.Logging.Loggers;
using DevkitServer.Framework;
using DevkitServer.Multiplayer.Levels;
using DevkitServer.Multiplayer.Networking;
using DevkitServer.Patches;
using HarmonyLib;
using SDG.Framework.Modules;
using System.Reflection;
using System.Runtime.CompilerServices;
using Unturned.SystemEx;
using Module = SDG.Framework.Modules.Module;
using Type = System.Type;
#if CLIENT
using DevkitServer.API.Cartography.Compositors;
using DevkitServer.API.UI.Extensions;
#endif

namespace DevkitServer.Plugins;
public static class PluginLoader
{
    private static string? _pluginsDir;
    private static string? _libraryDir;

    /// <summary>
    /// Path to the directory that plugins are read from.
    /// </summary>
    public static string PluginsDirectory => _pluginsDir ??= Path.Combine(DevkitServerConfig.Directory, "Plugins");

    /// <summary>
    /// Path to the directory that libraries are read from.
    /// </summary>
    public static string LibrariesDirectory => _libraryDir ??= Path.Combine(DevkitServerConfig.Directory, "Libraries");

    private static readonly CachedMulticastEvent<Action<IDevkitServerPlugin>> OnPluginLoadedEvent = new CachedMulticastEvent<Action<IDevkitServerPlugin>>(typeof(PluginLoader), nameof(OnPluginLoaded));
    private static readonly CachedMulticastEvent<Action<IDevkitServerPlugin>> OnPluginUnloadedEvent = new CachedMulticastEvent<Action<IDevkitServerPlugin>>(typeof(PluginLoader), nameof(OnPluginUnloaded));
    private static readonly CachedMulticastEvent<Action> OnPluginsLoadedEvent = new CachedMulticastEvent<Action>(typeof(PluginLoader), nameof(OnPluginsLoaded));
    private static readonly CachedMulticastEvent<Action> OnPluginsUnloadingEvent = new CachedMulticastEvent<Action>(typeof(PluginLoader), nameof(OnPluginsUnloading));

    private static readonly List<IDevkitServerPlugin> PluginsIntl = new List<IDevkitServerPlugin>();
    private static readonly List<PluginLibrary> LibrariesIntl = new List<PluginLibrary>();
    private static readonly List<PluginAssembly> AssembliesIntl = new List<PluginAssembly>();
    private static readonly Dictionary<string, PluginLibrary> LibraryDictionary = new Dictionary<string, PluginLibrary>();

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
    /// <remarks>Not safe to access from another thread.</remarks>
    public static IReadOnlyList<IDevkitServerPlugin> Plugins { get; } = PluginsIntl.AsReadOnly();

    /// <summary>
    /// All loaded libraries.
    /// </summary>
    /// <remarks>Not safe to access from another thread.</remarks>
    public static IReadOnlyList<PluginLibrary> Libraries { get; } = LibrariesIntl.AsReadOnly();

    /// <summary>
    /// All loaded assemblies and their plugins and patching info.
    /// </summary>
    /// <remarks>Not safe to access from another thread.</remarks>
    public static IReadOnlyList<PluginAssembly> Assemblies { get; } = AssembliesIntl.AsReadOnly();

    /// <summary>
    /// Gets the color of a plugin, or default color if one is not defined.
    /// </summary>
    [Pure]
    public static Color32 GetColor(this IDevkitServerPlugin? plugin)
        => plugin switch
        {
            null => DevkitServerModule.ModuleColor,
            IDevkitServerColorPlugin p => p.Color,
            _ => Plugin.DefaultColor
        };

    /// <summary>
    /// Gets whether or not a plugin is currently loaded (if it's in <see cref="Plugins"/>.)
    /// </summary>
    public static bool IsLoaded(IDevkitServerPlugin plugin)
    {
        lock (PluginsIntl)
            return PluginsIntl.Contains(plugin);
    }

    /// <summary>
    /// Gets whether or not a plugin library is currently loaded.
    /// </summary>
    public static bool IsLoaded(AssemblyName assemblyName)
    {
        lock (LibraryDictionary)
            return LibraryDictionary.TryGetValue(assemblyName.FullName, out PluginLibrary lib) && lib.Assembly != null;
    }

    /// <summary>
    /// Gets whether or not a plugin library is currently loaded or registered to be loaded.
    /// </summary>
    public static bool IsRegistered(AssemblyName assemblyName)
    {
        lock (LibraryDictionary)
            return LibraryDictionary.ContainsKey(assemblyName.FullName);
    }

    /// <summary>
    /// Force an assembly to load if it's not already.
    /// </summary>
    /// <returns><see langword="False"/> if it was already loaded, otherwise <see langword="true"/>.</returns>
    public static bool ForceLoadLibrary(PluginLibrary library)
    {
        return library.ForceLoad();
    }
    private static void AssertPluginValid(IDevkitServerPlugin plugin)
    {
        try
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
        }
        catch (NotImplementedException) // for unit tests
        {
            if (DevkitServerModule.Module != null)
                throw;

            plugin.LogWarning($"Directories not implemented for plugin {plugin.Name}.");
        }

        if (string.IsNullOrWhiteSpace(plugin.PermissionPrefix))
        {
            plugin.LogError("Invalid PermissionPrefix: " + plugin.PermissionPrefix.Format() + ". PermissionPrefix can not be empty.");
            throw new Exception($"Plugin {plugin.Name}'s 'PermissionPrefix' can not be empty.");
        }
        if (plugin.PermissionPrefix.IndexOf("::", StringComparison.Ordinal) != -1)
        {
            plugin.LogError("Invalid PermissionPrefix: " + plugin.PermissionPrefix.Format() + ". PermissionPrefix can not contain '::'.");
            throw new Exception("Plugin " + plugin.Name + "'s 'PermissionPrefix' can not contain '::'.");
        }
        if (PermissionBranch.IsPlus(plugin.PermissionPrefix[0]) || PermissionBranch.IsDash(plugin.PermissionPrefix[0]))
        {
            plugin.LogError("Invalid PermissionPrefix: " + plugin.PermissionPrefix.Format() + ". PermissionPrefix can not start with a plus or minus.");
            throw new Exception("Plugin " + plugin.Name + "'s 'PermissionPrefix' can not start with a plus or minus.");
        }

        if (plugin.PermissionPrefix.Equals(PermissionLeaf.CoreModulePrefix, StringComparison.InvariantCultureIgnoreCase))
        {
            plugin.LogError("Invalid PermissionPrefix: " + plugin.PermissionPrefix.Format() + ". PermissionPrefix can not equal " + PermissionLeaf.CoreModulePrefix.Format() + ".");
            throw new Exception("Plugin " + plugin.Name + "'s 'PermissionPrefix' can not equal " + PermissionLeaf.CoreModulePrefix + ".");
        }
        if (plugin.PermissionPrefix.Equals(PermissionLeaf.DevkitServerModulePrefix, StringComparison.InvariantCultureIgnoreCase))
        {
            plugin.LogError("Invalid PermissionPrefix: " + plugin.PermissionPrefix.Format() + ". PermissionPrefix can not equal " + PermissionLeaf.DevkitServerModulePrefix.Format() + ".");
            throw new Exception("Plugin " + plugin.Name + "'s 'PermissionPrefix' can not equal " + PermissionLeaf.DevkitServerModulePrefix + ".");
        }

        if (DevkitServerModule.Module != null) // for unit tests
        {
            string defaultModuleName = DevkitServerModule.MainLocalization?.format("Name") ?? DevkitServerModule.ModuleName;
            if (plugin.MenuName.Equals(defaultModuleName, StringComparison.InvariantCultureIgnoreCase))
            {
                plugin.LogError("Invalid MenuName: " + plugin.MenuName.Format() + ". MenuName can't be equal to " + defaultModuleName.Format() + ".");
                throw new Exception("Plugin " + plugin.Name + "'s 'MenuName' can't be equal to \"" + defaultModuleName + "\".");
            }
        }
    }

    /// <summary>
    /// Registers and loads a plugin. If it's the first plugin to be loaded from that assembly, all patching and reflection will be done.
    /// </summary>
    /// <remarks>It is expected for you to pass a new()'d class as an argument to this.</remarks>
    /// <exception cref="AggregateException">Error(s) loading the plugin.</exception>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="NotSupportedException">Method was called on a non-game thread.</exception>
    public static void RegisterPlugin(IDevkitServerPlugin plugin)
    {
        ThreadUtil.assertIsGameThread();

        if (plugin == null)
            throw new ArgumentNullException(nameof(plugin));

        lock (PluginsIntl)
        {
            if (plugin.PermissionPrefix != null)
            {
                bool dup;
                do
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
                            break;
                        }
                    }
                }
                while (dup);
            }

            try
            {
                AssertPluginValid(plugin);
                InitPlugin(plugin);
                plugin.Load();

                if (DevkitServerModule.UnturnedLoaded) // for unit tests
                    PluginAdvertising.Get().AddPlugin(plugin.MenuName);

                PluginsIntl.Add(plugin);
                Assembly asm = plugin.GetType().Assembly;
                for (int i = 0; i < AssembliesIntl.Count; ++i)
                {
                    if (AssembliesIntl[i].Assembly == asm)
                    {
                        AssembliesIntl[i].AddPlugin(plugin);
                        plugin.Assembly = AssembliesIntl[i];
                        goto skipAssemblyAdd;
                    }
                }

                PluginAssembly assemblyWrapper = new PluginAssembly(asm, asm.Location);
                assemblyWrapper.AddPlugin(plugin);
                AssembliesIntl.Add(assemblyWrapper);
                assemblyWrapper.ReflectAndPatch();

                if (assemblyWrapper.CustomNetMessageListeners.Count > 0)
                    NetFactory.ReclaimMessageBlock();

                plugin.Assembly = assemblyWrapper;
                skipAssemblyAdd:
                OnPluginLoadedEvent.TryInvoke(plugin);
                string src = "LOAD " + plugin.GetSource();
                plugin.LogInfo(src, "Loaded successfully.");
            }
            catch (Exception ex)
            {
                string src = "LOAD " + plugin.GetSource();
                Logger.DevkitServer.LogError(src, ex, "Plugin failed to load: " + plugin.GetType().Format() + ".");
                Exception? unloadEx = null;
                try
                {
                    plugin.Unload();
                    OnPluginUnloadedEvent.TryInvoke(plugin);
                }
                catch (Exception ex2)
                {
                    unloadEx = ex2;
                    Logger.DevkitServer.LogError(src, ex2, "Plugin failed to unload: " + plugin.GetType().Format() + ".");
                }

                throw new AggregateException("Plugin failed to load and unload: " + plugin.GetType().FullName + ".", unloadEx != null ? [ ex, unloadEx ] : new Exception[] { ex });
            }

            if (plugin is IReflectionDoneListenerDevkitServerPlugin refl)
            {
                try
                {
                    refl.OnReflectionDone(refl.Assembly, refl.Assembly.Plugins.Count > 0 && ReferenceEquals(refl.Assembly.Plugins[0], refl));
                }
                catch (Exception ex)
                {
                    refl.LogError($"Caught and ignored exception in {typeof(IReflectionDoneListenerDevkitServerPlugin).Format()}." +
                                  $"{nameof(IReflectionDoneListenerDevkitServerPlugin.OnReflectionDone).Colorize(FormattingColorType.Method)}:");
                    refl.LogError(ex);
                }
            }
        }
    }

    /// <summary>
    /// Registers and loads a plugin library.
    /// </summary>
    /// <remarks>It is expected for you to pass a new()'d class as an argument to this.</remarks>
    /// <exception cref="ArgumentException">Error loading the assembly.</exception>
    /// <exception cref="InvalidOperationException">A library with that assembly is already registered.</exception>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="FileNotFoundException"><paramref name="dllPath"/> does not exist.</exception>
    public static void RegisterLibrary(string dllPath)
    {
        lock (LibraryDictionary)
        {
            string fn = Path.GetFileNameWithoutExtension(dllPath);
            string src = "LOAD " + fn.ToUpperInvariant().Format(false);
            if (!File.Exists(dllPath))
            {
                throw new FileNotFoundException("Could not find an assembly named " + fn + ".", dllPath);
            }

            AssemblyName asmName;
            try
            {
                asmName = AssemblyName.GetAssemblyName(dllPath);
            }
            catch (Exception ex)
            {
                Logger.DevkitServer.LogError(src, ex, $"[] Unable to load plugin library: {dllPath.Format()}.");
                throw new ArgumentException("Failed to read assembly.", nameof(dllPath), ex);
            }

            if (LibraryDictionary.TryGetValue(asmName.FullName, out PluginLibrary existing))
                throw new InvalidOperationException($"The library \"{asmName.FullName}\" is already registered from \"{existing.File}\".");

            PluginLibrary lib = new PluginLibrary(asmName, dllPath);

            LibrariesIntl.Add(lib);
            LibraryDictionary.Add(lib.Name.FullName, lib);

            src = "LOAD " + asmName.Name.ToUpperInvariant().Format(false);

            if (lib.Assembly != null)
                Logger.DevkitServer.LogInfo(src, $"Plugin library already loaded: {Path.GetFileName(dllPath).Format()}.");
            else
                Logger.DevkitServer.LogInfo(src, $"Discovered plugin library: {Path.GetFileName(dllPath).Format()}.");
        }
    }
    internal static string GetSource(this IDevkitServerPlugin plugin)
    {
        Color32 color = plugin.GetColor();
        string src = plugin.Name.ToUpperInvariant().Colorize(color);
        if (plugin.DeveloperMode)
            src += " (".ColorizeNoReset(FormattingColorType.Punctuation) + "dev".ColorizeNoReset(FormattingColorType.FlowKeyword) + ")".Colorize(FormattingColorType.Punctuation);

        return src;
    }

    /// <summary>
    /// Unloads and deregisters a plugin. Unpatching will only be done when the last plugin is deregistered from the assembly.
    /// </summary>
    /// <exception cref="AggregateException">Error(s) unloading the plugin.</exception>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="NotSupportedException">Method was called on a non-game thread.</exception>
    public static void DeregisterPlugin(IDevkitServerPlugin plugin)
    {
        ThreadUtil.assertIsGameThread();

        if (plugin == null)
            throw new ArgumentNullException(nameof(plugin));

        lock (PluginsIntl)
        {
            for (int i = PluginsIntl.Count - 1; i >= 0; --i)
            {
                if (!PluginsIntl[i].Equals(plugin))
                    continue;

                plugin = PluginsIntl[i];
                PluginAdvertising.Get().RemovePlugin(plugin.MenuName);
                PluginsIntl.RemoveAt(i);
                Assembly asm = plugin.GetType().Assembly;
                PluginAssembly? assemblyWrapper = null;
                for (int j = 0; j < AssembliesIntl.Count; ++j)
                {
                    PluginAssembly assembly = AssembliesIntl[j];
                    if (assembly.Assembly != asm)
                        continue;

                    assembly.RemovePlugin(plugin);
                    assemblyWrapper = assembly;
                    break;
                }

                string src = "UNLOAD " + plugin.GetSource();

                try
                {
                    plugin.Unload();
                }
                catch (Exception ex)
                {
                    Logger.DevkitServer.LogError(src, ex, "Plugin failed to unload: " + plugin.GetType().Format() + ".");
                    throw new AggregateException("Plugin failed to unload: " + plugin.GetType().FullName + ".", ex);
                }
                finally
                {
                    OnPluginUnloadedEvent.TryInvoke(plugin);
                }

                if (assemblyWrapper is { Plugins.Count: 0 })
                {
                    assemblyWrapper.Unpatch();
                }

                break;
            }
        }
    }
    internal static void LoadPlugins()
    {
        Directory.CreateDirectory(PluginsDirectory);
        Directory.CreateDirectory(LibrariesDirectory);

        string[] plugins = Directory.GetFiles(PluginsDirectory, "*.dll", SearchOption.TopDirectoryOnly);
        string[] libraries = Directory.GetFiles(LibrariesDirectory, "*.dll", SearchOption.TopDirectoryOnly);

        DevkitServerModule.InitializedPluginLoader = true;

        lock (LibraryDictionary)
        {
            foreach (string libDll in libraries)
            {
                string fn = Path.GetFileNameWithoutExtension(libDll);
                string src = "LOAD " + fn.ToUpperInvariant().Format(false);
                AssemblyName asmName;
                try
                {
                    asmName = AssemblyName.GetAssemblyName(libDll);
                }
                catch (Exception ex)
                {
                    Logger.DevkitServer.LogError(src, ex, $"Unable to load plugin library: {libDll.Format()}.");
                    continue;
                }

                PluginLibrary lib = new PluginLibrary(asmName, libDll);

                LibrariesIntl.Add(lib);
                LibraryDictionary.Add(lib.Name.FullName, lib);
                src = "LOAD " + asmName.Name.ToUpperInvariant().Format(false);
                if (lib.Assembly != null)
                    Logger.DevkitServer.LogInfo(src, $"Plugin library already loaded: {Path.GetFileName(libDll).Format()}.");
                else
                    Logger.DevkitServer.LogInfo(src, $"Discovered plugin library: {Path.GetFileName(libDll).Format()}.");
            }
        }

        lock (PluginsIntl)
        {
            // load assemblies, discover and create classes

            List<IDevkitServerPlugin> pluginsTemp = new List<IDevkitServerPlugin>();
            foreach (string pluginDll in plugins)
            {
                Assembly assembly = Assembly.LoadFrom(pluginDll);
                List<Type> types = Accessor.GetTypesSafe(assembly);
                foreach (Type type in types
                             .Where(typeof(IDevkitServerPlugin).IsAssignableFrom)
                             .OrderByDescending(x => x.GetPriority()))
                {
                    if (type.IsInterface)
                        continue;

                    string src = "LOAD " + assembly.GetName().Name.ToUpperInvariant();
                    if (type.IsAbstract || type.IsIgnored())
                    {
                        Logger.DevkitServer.LogInfo(src, $"Skipped loading {type.Format()}.");
                        continue;
                    }

                    try
                    {
                        pluginsTemp.Add((IDevkitServerPlugin)Activator.CreateInstance(type));
                    }
                    catch (Exception ex)
                    {
                        Logger.DevkitServer.LogError(src, "Failed to load assembly:" + pluginDll.Format() + ".");
                        Logger.DevkitServer.LogError(src, ex, " Plugin failed to load: " + type.Format() + ".");
                        pluginsTemp.RemoveAll(x => x.GetType().Assembly == assembly);
                        break;
                    }
                }

                if (pluginsTemp.Count > 0)
                {
                    Logger.DevkitServer.LogInfo("LoadPlugins", $"Found {pluginsTemp.Count.Format()} plugin{(pluginsTemp.Count == 1 ? string.Empty : "s")} " +
                                   $"from {Path.GetFileName(pluginDll).Format()} ({assembly.GetName().Version.Format()})");
                    PluginAssembly? info = AssembliesIntl.Find(x => x.Assembly == assembly);
                    if (info == null)
                    {
                        info = new PluginAssembly(assembly, pluginDll);
                        AssembliesIntl.Add(info);
                    }
                    for (int k = 0; k < pluginsTemp.Count; ++k)
                        info.AddPlugin(pluginsTemp[k]);
                    PluginsIntl.AddRange(pluginsTemp);
                    for (int i = 0; i < pluginsTemp.Count; ++i)
                        pluginsTemp[i].Assembly = info;
                    pluginsTemp.Clear();
                }
                else
                {
                    Logger.DevkitServer.LogInfo("LoadPlugins", "Found no plugins from " + Path.GetFileName(pluginDll).Format() + " (" + assembly.GetName().Version.Format() + ")");
                }
            }

            // resolve duplicate permission prefixes
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
                        if (!plugin2.PermissionPrefix.Equals(plugin.PermissionPrefix, StringComparison.InvariantCultureIgnoreCase))
                            continue;

                        plugin2.PermissionPrefix = "_" + plugin2.PermissionPrefix;
                        plugin2.LogWarning("Conflicting permission prefix with " + plugin.Format() +
                                           " (" + plugin.PermissionPrefix.Format() + "). Overriding to " + plugin2.PermissionPrefix.Format() + ".");
                        dup = true;
                    }
                }
            }

            PermissionManager.InitHandlers();

            pluginsTemp = PluginsIntl.OrderByDescending(x => x.GetType().GetPriority()).ToList();

            // load plugins based on priority
            for (int i = 0; i < pluginsTemp.Count; ++i)
            {
                IDevkitServerPlugin plugin = pluginsTemp[i];
                string src = "LOAD " + plugin.GetSource();
                try
                {
                    AssertPluginValid(plugin);
                    InitPlugin(plugin);
                    plugin.Load();
                    OnPluginLoadedEvent.TryInvoke(plugin);
                    plugin.LogInfo(src, "Loaded successfully.");
                }
                catch (Exception ex)
                {
                    Logger.DevkitServer.LogError(src, ex, $"Plugin failed to load: {plugin.GetType().Format()}.");
                    for (int j = i; j >= 0; --j)
                    {
                        IDevkitServerPlugin plugin2 = pluginsTemp[j];
                        if (plugin.Assembly == plugin2.Assembly)
                        {
                            try
                            {
                                plugin2.Unload();
                                OnPluginUnloadedEvent.TryInvoke(plugin2);
                                Logger.DevkitServer.LogInfo(src, $" Unloaded {plugin2.GetType().Format()}.");
                            }
                            catch (Exception ex2)
                            {
                                Logger.DevkitServer.LogError(src, ex2, $" Plugin failed to unload: {plugin2.GetType().Format()}.");
                            }
                        }
                    }
                    pluginsTemp.Clear();
                    Logger.DevkitServer.LogError(src, "Failed to load assembly: " + plugin.Assembly.Assembly.GetName().Name.Format() + ".");
                    break;
                }
            }

            DevkitServerModule.JustBeforePluginsReflect();

            for (int i = 0; i < pluginsTemp.Count; ++i)
            {
                pluginsTemp[i].Assembly.ReflectAndPatch();
            }

            NetFactory.ReclaimMessageBlock();

            for (int i = 0; i < pluginsTemp.Count; ++i)
            {
                if (pluginsTemp[i] is not IReflectionDoneListenerDevkitServerPlugin refl)
                    continue;

                try
                {
                    refl.OnReflectionDone(refl.Assembly, refl.Assembly.Plugins.Count > 0 && ReferenceEquals(refl.Assembly.Plugins[0], refl));
                }
                catch (Exception ex)
                {
                    refl.LogError($"Caught and ignored exception in {typeof(IReflectionDoneListenerDevkitServerPlugin).Format()}." +
                                  $"{nameof(IReflectionDoneListenerDevkitServerPlugin.OnReflectionDone).Colorize(FormattingColorType.Method)}:");
                    refl.LogError(ex);
                }
            }

            IPluginAdvertising advertisingFramework = PluginAdvertising.Get();

            string devkitServer = DevkitServerModule.MainLocalization.Translate("Name");
            advertisingFramework.RemovePlugins(advertisingFramework.GetPluginNames().Where(x => !x.Equals(devkitServer, StringComparison.Ordinal)));
            advertisingFramework.AddPlugins(PluginsIntl.OrderBy(x => x.Name).Select(x => x.MenuName).Where(x => !string.IsNullOrEmpty(x)));

            Logger.DevkitServer.LogDebug("LoadPlugins", $"Advertised plugins: {string.Join(", ", advertisingFramework.GetPluginNames())}.");

            CommandHandler.InitImpl();
        }

        OnPluginsLoadedEvent.TryInvoke();
    }
    private static void InitPlugin(IDevkitServerPlugin plugin)
    {
        if (plugin is ICachedTranslationSourcePlugin cachedTranslationSourcePlugin)
        {
            cachedTranslationSourcePlugin.TranslationSource = TranslationSource.FromPlugin(plugin);
        }
    }
    internal static void Unload()
    {
        OnPluginsUnloadingEvent.TryInvoke();
        IDevkitServerPlugin[] plugins;
        lock (PluginsIntl)
            plugins = PluginsIntl.ToArray();

        for (int i = 0; i < plugins.Length; i++)
        {
            try
            {
                DeregisterPlugin(plugins[i]);
            }
            catch
            {
                // ignored
            }
        }
    }

    /// <summary>
    /// Finds a loaded plugin given an assembly. Will fail if there are more than one plugin loaded from the assembly.
    /// </summary>
    public static IDevkitServerPlugin? FindPluginForAssembly(Assembly assembly)
    {
        // check if the assembly just has one plugin
        if (assembly == Accessor.DevkitServer) return null;
        lock (AssembliesIntl)
        {
            for (int i = 0; i < AssembliesIntl.Count; ++i)
            {
                if (AssembliesIntl[i].Assembly == assembly)
                {
                    if (AssembliesIntl[i].Plugins.Count == 1)
                        return AssembliesIntl[i].Plugins[0];
                    break;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Finds a loaded plugin given any member. If there are more than one plugin in it's assembly, the <see cref="PluginIdentifierAttribute"/>s of the method and its declaring classes will be examined.
    /// </summary>
    public static IDevkitServerPlugin? FindPluginForMember(MemberInfo member)
    {
        Type? relaventType = member as Type ?? member.DeclaringType;

        if (relaventType == null)
            return null;

        // check if the assembly just has one plugin
        Assembly asm = relaventType.Assembly;
        if (asm == Accessor.DevkitServer) return null;
        lock (AssembliesIntl)
        {
            for (int i = 0; i < AssembliesIntl.Count; ++i)
            {
                if (AssembliesIntl[i].Assembly == asm)
                {
                    if (AssembliesIntl[i].Plugins.Count == 1)
                        return AssembliesIntl[i].Plugins[0];
                    break;
                }
            }
        }

        IDevkitServerPlugin? plugin = null;

        lock (PluginsIntl)
        {
            // find attribute on member
            if (member.TryGetAttributeSafe(out PluginIdentifierAttribute attribute) && attribute.PluginType != null)
            {
                Type? ptype = attribute.PluginType;
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
            return type.GetAttributeSafe(typeof(PluginIdentifierAttribute), true) is PluginIdentifierAttribute { PluginType: { } ptype }
                ? FindPluginInstanceOfType(ptype)
                : null;
        }

        checkAssembly:
        if (plugin == null || plugin.Assembly.Assembly != asm)
            return null;

        return plugin;
    }

    internal static PluginLibrary? ResolveAssembly(string argsName)
    {
        lock (LibraryDictionary)
        {
            if (LibraryDictionary.TryGetValue(argsName, out PluginLibrary library))
            {
                library.ForceLoad();
                if (library.Assembly != null)
                    return library;
            }

            AssemblyName argName = new AssemblyName(argsName);
            foreach (KeyValuePair<string, PluginLibrary> lib in LibraryDictionary)
            {
                AssemblyName name = new AssemblyName(lib.Key);
                if (!name.Name.Equals(argName.Name))
                    continue;

                if (name.Version < argName.Version)
                    continue;

                AssemblyResolver.Log($"Assembly name ({name.Name}) matched name and version but not an exact match, loading anyway.");

                lib.Value.ForceLoad();
                if (lib.Value.Assembly != null)
                    return lib.Value;
            }
        }

        return null;
    }

    /// <summary>
    /// Finds an existing, or creates a new <see cref="ModulePlugin"/> for a foreign module (for compatability reasons).
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="InvalidOperationException">Tried to create a plugin with <see cref="DevkitServerModule.Module"/>.</exception>
    /// <exception cref="Exception">Failed to create the plugin for a variety of reasons.</exception>
    public static ModulePlugin FindOrCreateModulePlugin(Module module)
    {
        ThreadUtil.assertIsGameThread();

        if (module == null)
            throw new ArgumentNullException(nameof(module));

        if (module == DevkitServerModule.Module)
            throw new InvalidOperationException("DevkitServer is not a foreign module.");

        lock (PluginsIntl)
        {
            ModulePlugin? plugin = PluginsIntl.OfType<ModulePlugin>().FirstOrDefault(x => x.Module == module);

            if (plugin != null) return plugin;

            ModulePlugin newPlugin = new ModulePlugin(module);
            string src = "LOAD " + newPlugin.GetSource();

            if (!module.isEnabled)
            {
                try
                {
                    module.isEnabled = true;
                }
                catch (Exception ex)
                {
                    Logger.DevkitServer.LogError(src, ex, $"Failed to enable module: {module.config.Name.Format(false)}.");
                }
            }

            PluginAssembly? primaryModuleAssembly = null;
            if (module.assemblies != null)
            {
                lock (AssembliesIntl)
                {
                    List<Type> types = Accessor.GetTypesSafe(module.assemblies);
                    
                    foreach (Type type in types.Where(x => typeof(IModuleNexus).IsAssignableFrom(x)))
                    {
                        if (AssembliesIntl.Any(x => x.Assembly == type.Assembly))
                            continue;

                        PluginAssembly assembly = new PluginAssembly(type.Assembly, type.Assembly.Location, false);
                        assembly.AddPlugin(newPlugin);
                        assembly.ReflectAndPatch();

                        AssembliesIntl.Add(assembly);
                        primaryModuleAssembly = assembly;
                        Logger.DevkitServer.LogDebug(src, $"Discovered nexus DLL in module {module.config.Name.Format(false)}: {type.Assembly.FullName.Format()}.");
                    }
                }
            }

            if (primaryModuleAssembly == null)
            {
                Logger.DevkitServer.LogWarning(src, $"Unable to find assemblies for module plugin: {module.config.Name.Format(false)}.");
                newPlugin.Assembly = new PluginAssembly();
                newPlugin.Assembly.AddPlugin(newPlugin);
            }
            else
            {
                newPlugin.Assembly = primaryModuleAssembly;
            }

            try
            {
                AssertPluginValid(newPlugin);
                InitPlugin(newPlugin);
                ((IDevkitServerPlugin)newPlugin).Load();
                OnPluginLoadedEvent.TryInvoke(newPlugin);
                Logger.DevkitServer.LogInfo(src, "Loaded successfully.");
            }
            catch (Exception ex)
            {
                Logger.DevkitServer.LogError(src, $"Failed to load module plugin assembly: {(primaryModuleAssembly?.Assembly.GetName().Name).Format()}.");
                Logger.DevkitServer.LogError(src, $" Module plugin failed to load: {module.config.Name.Format(false)}.");
                throw new Exception($"Failed to create a plugin for {module.config.Name}.", ex);
            }

            PluginsIntl.Add(newPlugin);

            return newPlugin;
        }
    }
}

public class PluginLibrary
{
    private Assembly? _assembly;
    public Assembly? Assembly => _assembly;
    public AssemblyName Name { get; }
    public string File { get; }
    public PluginLibrary(AssemblyName assemblyName, string file)
    {
        Name = assemblyName;
        _assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(x => x.GetName().Equals(assemblyName));
        File = file;
    }

    internal bool ForceLoad()
    {
        if (_assembly != null)
            return false;

        lock (this)
        {
            if (_assembly != null)
                return false;

            if (!System.IO.File.Exists(File))
            {
                Logger.DevkitServer.LogError("LOAD " + Name.Name.ToUpperInvariant().Format(false), $"Failed to load assembly file at {File.Format()}: file missing.");
            }

            try
            {
                Assembly asm = Assembly.LoadFrom(File);

                Interlocked.CompareExchange(ref _assembly, asm, null);
            }
            catch
            {
                Logger.DevkitServer.LogError("LOAD " + Name.Name.ToUpperInvariant().Format(false), $"Failed to load assembly file at {File.Format()}.");
                throw;
            }

            return true;
        }
    }
}
public class PluginAssembly : CoreLogger
{
    private readonly List<IDevkitServerPlugin> _plugins;
    private readonly List<NetInvokerInfo> _netCalls;
    private readonly List<NetMethodInfo> _netMethods;
    private readonly List<HierarchyItemTypeIdentifierFactoryInfo> _hierarchyItemFactories;
    private readonly List<ReplicatedLevelDataSourceInfo> _replicatedLevelDataSources;
    private readonly List<ChartColorProviderInfo> _chartColorProviders;
    private readonly List<ICustomNetMessageListener> _customNetMessageListeners;
#if CLIENT
    private readonly List<CartographyCompositorInfo> _cartographyCompositors;
#endif
    public IReadOnlyList<IDevkitServerPlugin> Plugins { get; }
    public Assembly Assembly { get; }
    public string? File { get; }
    public Harmony? Patcher { get; internal set; }
    public bool HasReflected { get; private set; }
    public bool HasPatched { get; private set; }
    public string? HarmonyId => Patcher?.Id;
    public IReadOnlyList<NetInvokerInfo> NetCalls { get; }
    public IReadOnlyList<NetMethodInfo> NetMethods { get; }
    public IReadOnlyList<HierarchyItemTypeIdentifierFactoryInfo> HierarchyItemFactories { get; }
    public IReadOnlyList<ReplicatedLevelDataSourceInfo> ReplicatedLevelDataSources { get; }
    public IReadOnlyList<ChartColorProviderInfo> ChartColorProviders { get; }
    public IReadOnlyList<ICustomNetMessageListener> CustomNetMessageListeners { get; }
#if CLIENT
    public IReadOnlyList<CartographyCompositorInfo> CartographyCompositors { get; }
#endif
    public PluginAssembly(Assembly assembly, string file, bool autoPatch = true) : base(assembly.GetName().Name.ToUpperInvariant())
    {
        _plugins = new List<IDevkitServerPlugin>(0);
        _netCalls = new List<NetInvokerInfo>(0);
        _netMethods = new List<NetMethodInfo>(0);
        _hierarchyItemFactories = new List<HierarchyItemTypeIdentifierFactoryInfo>(0);
        _replicatedLevelDataSources = new List<ReplicatedLevelDataSourceInfo>(0);
        _chartColorProviders = new List<ChartColorProviderInfo>(0);
        _customNetMessageListeners = new List<ICustomNetMessageListener>(0);

        Plugins = _plugins.AsReadOnly();
        NetCalls = _netCalls.AsReadOnly();
        NetMethods = _netMethods.AsReadOnly();
        HierarchyItemFactories = _hierarchyItemFactories.AsReadOnly();
        ReplicatedLevelDataSources = _replicatedLevelDataSources.AsReadOnly();
        ChartColorProviders = _chartColorProviders.AsReadOnly();
        CustomNetMessageListeners = _customNetMessageListeners.AsReadOnly();

#if CLIENT
        _cartographyCompositors = new List<CartographyCompositorInfo>(0);
        CartographyCompositors = _cartographyCompositors.AsReadOnly();
#endif

        Assembly = assembly;
        Patcher = autoPatch ? new Harmony(PatchesMain.HarmonyId + ".assembly." + assembly.GetName().Name.ToLowerInvariant().Replace('_', '.').Replace(' ', '.')) : null;
        File = string.IsNullOrEmpty(file) ? null : file;
    }
    internal PluginAssembly() : base("unknown assembly")
    {
        _plugins = new List<IDevkitServerPlugin>(1);
        _netCalls = new List<NetInvokerInfo>(0);
        _netMethods = new List<NetMethodInfo>(0);
        _hierarchyItemFactories = new List<HierarchyItemTypeIdentifierFactoryInfo>(0);
        _replicatedLevelDataSources = new List<ReplicatedLevelDataSourceInfo>(0);
        _chartColorProviders = new List<ChartColorProviderInfo>(0);
        _customNetMessageListeners = new List<ICustomNetMessageListener>(0);

        Plugins = _plugins.AsReadOnly();
        NetCalls = _netCalls.AsReadOnly();
        NetMethods = _netMethods.AsReadOnly();
        HierarchyItemFactories = _hierarchyItemFactories.AsReadOnly();
        ReplicatedLevelDataSources = _replicatedLevelDataSources.AsReadOnly();
        ChartColorProviders = _chartColorProviders.AsReadOnly();
        CustomNetMessageListeners = _customNetMessageListeners.AsReadOnly();

#if CLIENT
        _cartographyCompositors = new List<CartographyCompositorInfo>(0);
        CartographyCompositors = _cartographyCompositors.AsReadOnly();
#endif
        File = null!;
        Assembly = null!;
        Patcher = null!;
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
        const string src = nameof(ReflectAndPatch);

        if (!HasReflected)
        {
            HasReflected = true;
#if CLIENT
            UIExtensionManager.Reflect(Assembly);
#endif
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

            HierarchyItemTypeIdentifierEx.RegisterFromAssembly(Assembly, _hierarchyItemFactories);
            ReplicatedLevelDataRegistry.RegisterFromAssembly(Assembly, _replicatedLevelDataSources, this);

            List<Type> types = Accessor.GetTypesSafe(Assembly);

            foreach (Type type in types
                         .Select(x => new KeyValuePair<Type, EarlyTypeInitAttribute?>(x, x.GetAttributeSafe<EarlyTypeInitAttribute>()))
                         .Where(x => x.Value != null)
                         .OrderByDescending(x => x.Value!.Priority)
                         .ThenBy(x => x.Key.Name)
                         .Select(x => x.Key))
            {
                try
                {
                    RuntimeHelpers.RunClassConstructor(type.TypeHandle);
                    CreateDirectoryAttribute.CreateInType(type);
                    this.LogDebug(src, $"Initialized static module {type.Format()}.");
                }
                catch (Exception ex)
                {
                    this.LogError(src, ex, $"Error while initializing static module {type.Format()}.");
                    break;
                }
            }

            foreach (Type type in types.Where(typeof(ICustomNetMessageListener).TryIsAssignableFrom))
            {
                if (type.IsAbstract || type.IsInterface)
                    continue;

                try
                {
                    ICustomNetMessageListener listener = (ICustomNetMessageListener)Activator.CreateInstance(type);
                    listener.Assembly = this;
                    _customNetMessageListeners.Add(listener);
                }
                catch (Exception ex)
                {
                    this.LogError(src, ex, $"Error creating custom net message listener {type.Format()}.");
                }
            }

            foreach (Type type in types.Where(typeof(IChartColorProvider).TryIsAssignableFrom))
            {
                if (type.IsAbstract || type.IsInterface)
                    continue;

                IDevkitServerPlugin? plugin = PluginLoader.FindPluginForMember(type);
                if (plugin == null)
                {
                    this.LogError(src, $"Unable to link {type.Format()} to a plugin. Use the {typeof(PluginIdentifierAttribute).Format()} to link a " +
                             $"chart color provider to a plugin when multiple plugins are loaded from an assembly.");
                    continue;
                }

                ConstructorInfo? ctor = type.GetConstructor(Array.Empty<Type>());
                if (ctor == null)
                {
                    this.LogError(src, $"Chart color provider {type.Format()} must have a parameterless constructor.");
                    continue;
                }
                
                _chartColorProviders.Add(new ChartColorProviderInfo(type, plugin, type.GetPriority()));
            }
#if CLIENT
            foreach (Type type in types.Where(typeof(ICartographyCompositor).TryIsAssignableFrom))
            {
                if (type.IsAbstract || type.IsInterface)
                    continue;

                IDevkitServerPlugin? plugin = PluginLoader.FindPluginForMember(type);
                if (plugin == null)
                {
                    this.LogError(src, $"Unable to link {type.Format()} to a plugin. Use the {typeof(PluginIdentifierAttribute).Format()} to link a " +
                             $"cartography compositor to a plugin when multiple plugins are loaded from an assembly.");
                    continue;
                }

                ConstructorInfo? ctor = type.GetConstructor(Array.Empty<Type>());
                if (ctor == null)
                {
                    this.LogError(src, $"Cartography compositor {type.Format()} must have a parameterless constructor.");
                    continue;
                }

                bool supportsSatellite = true, supportsChart = true;

                try
                {
                    ICartographyCompositor compositor = (ICartographyCompositor)Activator.CreateInstance(type, true);
                    supportsSatellite = compositor.SupportsSatellite;
                    supportsChart = compositor.SupportsChart;
                }
                catch (Exception ex)
                {
                    this.LogError(src, ex, $"Error checking for 'SupportsSatellite' and 'SupportsChart' on cartography compositor {type.Format()}.");
                }
                
                _cartographyCompositors.Add(new CartographyCompositorInfo(type, plugin, type.GetPriority(), supportsSatellite, supportsChart));
            }

#endif

            CreateDirectoryAttribute.CreateInAssembly(Assembly);
        }
        if (!HasPatched && Patcher != null)
        {
            HasPatched = true;
            try
            {
                Patcher.PatchAll(Assembly);
                this.LogInfo(src, $"Applied all patches in {Assembly.Location.Format()}.");
            }
            catch (Exception ex)
            {
                this.LogError(src, ex, $"Error while patching in assembly at {Assembly.Location.Format()}.");
            }
        }
    }
    internal void Unpatch()
    {
        ThreadUtil.assertIsGameThread();

        if (!HasPatched || Patcher == null) return;
        HasPatched = false;
        string src = "INIT " + Assembly.GetName().Name.ToUpperInvariant();

        try
        {
            Patcher.UnpatchAll(HarmonyId);
            this.LogInfo(src, $"Removed all patches in {Assembly.Location.Format()}.");
        }
        catch (Exception ex)
        {
            this.LogError(src, ex, $"Error while unpatching in assembly at {Assembly.Location.Format()}.");
        }
    }
    public sealed override string CoreType => Plugins.Count == 1 ? Plugins[0].GetSource() : base.CoreType;
    public sealed override bool IsSeverityEnabled(Severity severity, object? source)
    {
        if (Plugins.Count == 1 && severity == Severity.Debug && Plugins[0].DeveloperMode)
            return true;

        return base.IsSeverityEnabled(severity, source);
    }
}