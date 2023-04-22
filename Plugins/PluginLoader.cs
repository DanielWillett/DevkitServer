using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.Hosting;
using System.Security;
using System.Security.Permissions;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using DevkitServer.API;
using DevkitServer.Configuration;

namespace DevkitServer.Plugins;
public static class PluginLoader
{
    private static readonly List<IDevkitServerPlugin> _plugins = new List<IDevkitServerPlugin>();
    private static readonly List<PluginLibrary> _libs = new List<PluginLibrary>();
    private static readonly List<PluginAssembly> _pluginWrappers = new List<PluginAssembly>();
    private static readonly IReadOnlyList<IDevkitServerPlugin> _roPlugins = _plugins.AsReadOnly();
    private static readonly IReadOnlyList<PluginLibrary> _roLibs = _libs.AsReadOnly();
    public static readonly string PluginDirectory = Path.Combine(DevkitServerConfig.FilePath, "Plugins");
    public static readonly string LibraryDirectory = Path.Combine(DevkitServerConfig.FilePath, "Libraries");
    public static event Action<IDevkitServerPlugin>? OnPluginLoaded; 
    public static event Action<IDevkitServerPlugin>? OnPluginUnloaded;
    public static IReadOnlyList<IDevkitServerPlugin> Plugins => _roPlugins;
    public static IReadOnlyList<PluginLibrary> Libraries => _roLibs;
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
                             Attribute.GetCustomAttribute(x, typeof(PluginLoadPriorityAttribute)) is PluginLoadPriorityAttribute attr
                                 ? attr.Priority
                                 : 0
                         ))
            {
                if (type.IsAbstract || Attribute.IsDefined(type, typeof(IgnorePluginAttribute)))
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
                    break;
                }
            }

            if (pluginsTemp.Count > 0)
            {
                Logger.LogInfo("Found " + pluginsTemp.Count.Format() + " plugin" + (pluginsTemp.Count == 1 ? string.Empty : "s") +
                               " from " + Path.GetFileName(pluginDll).Format() + " (" + assembly.GetName().Version.Format() + ")");
                PluginAssembly info = new PluginAssembly();
                info.Plugins.AddRange(pluginsTemp);
                _pluginWrappers.Add(info);
                _plugins.AddRange(pluginsTemp);
            }
            else
            {
                Logger.LogInfo("Found no plugins from " + Path.GetFileName(pluginDll).Format() + " (" + assembly.GetName().Version.Format() + ")");
            }
        }

        pluginsTemp = pluginsTemp.OrderByDescending(x =>
            Attribute.GetCustomAttribute(x.GetType(), typeof(PluginLoadPriorityAttribute)) is
                PluginLoadPriorityAttribute attr
                ? attr.Priority
                : 0
        ).ToList();
        for (int i = 0; i < pluginsTemp.Count; ++i)
        {
            IDevkitServerPlugin plugin = pluginsTemp[i];
            try
            {
                plugin.Load();
                OnPluginLoaded?.Invoke(plugin);
                Logger.LogInfo("[LOAD " + plugin.Name.ToUpperInvariant() + "] Loaded " + plugin.Name.Colorize(plugin is Plugin p ? p.Color : new Color32(204, 153, 255, 255)));
            }
            catch (Exception ex)
            {
                Logger.LogError("Plugin failed to load: " + plugin.GetType().Format() + ".", method: "LOAD " + plugin.Name.ToUpperInvariant());
                Logger.LogError(ex, method: "LOAD " + plugin.Name.ToUpperInvariant());
                for (int j = i - 1; j >= 0; --j)
                {
                    IDevkitServerPlugin plugin2 = pluginsTemp[j];
                    if (plugin.Assembly == plugin2.Assembly)
                    {
                        try
                        {
                            plugin2.Unload();
                            OnPluginUnloaded?.Invoke(plugin2);
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
    }

    private static Assembly TryResolveType(object sender, ResolveEventArgs args)
    {
        Logger.LogDebug("Type resolve args: " + args.Name + " (" + args.RequestingAssembly + ").");
        return null!;
    }
    private static Assembly TryResolveAssembly(object sender, ResolveEventArgs args)
    {
        Logger.LogDebug("Assembly resolve args: " + args.Name + " (" + args.RequestingAssembly + ").");
        return null!;
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
}