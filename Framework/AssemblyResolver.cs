using DevkitServer.API;
using DevkitServer.Plugins;
using SDG.Framework.Modules;
using System.Reflection;
using Module = SDG.Framework.Modules.Module;

namespace DevkitServer.Framework;
internal class AssemblyResolver : IDisposable
{
    private const string Source = "ASSEMBLY RESOLVER";
    internal bool TriedToLoadUIExtensionModule { get; private set; }
    internal bool TriedToLoadLevelObjectIcons { get; private set; }

    private const int LogColorArgb = -4663389;

    private readonly UnsupportedModule[] _unsupportedModules =
    {
        new UnsupportedModule("LevelObjectIcons", new AssemblyName("LevelObjectIcons, Version=1.0.0.0, Culture=neutral, PublicKeyToken=355a72a9074e7dec"),
            onTriedToLoad: asmResolver =>
            {
                asmResolver.TriedToLoadLevelObjectIcons = true;
                Log(" + Module 'LevelObjectIcons' is unsupported because DevkitServer already implements the same LevelObjectIcon UI. " +
                    "Any referencing modules will be forwarded to DevkitServer.");
            }),

        new UnsupportedModule("UnturnedUITools", new AssemblyName("UnturnedUITools, Version=1.1.1.0, Culture=neutral, PublicKeyToken=5e66f8e265922cfe"),
            onTriedToLoad: asmResolver =>
            {
                asmResolver.TriedToLoadUIExtensionModule = true;
                Log(" + Module 'UnturnedUITools' is unsupported because DevkitServer already implements similar extension and UI tools. " +
                    "Any referencing modules will be forwarded to DevkitServer.");
            })
    };

    internal AssemblyResolver()
    {
        AppDomain.CurrentDomain.AssemblyLoad += CurrentDomainAssemblyLoad;
        AppDomain.CurrentDomain.AssemblyResolve += CurrentDomainOnAssemblyResolve;
    }

    private static void CurrentDomainAssemblyLoad(object sender, AssemblyLoadEventArgs args)
    {
        Log($"Assembly loaded: \"{args.LoadedAssembly.FullName}\".");
    }

    private static Assembly? CurrentDomainOnAssemblyResolve(object sender, ResolveEventArgs args)
    {
        if (DevkitServerModule.InitializedPluginLoader)
        {
            PluginLibrary? library = PluginLoader.ResolveAssembly(args.Name);
            if (library?.Assembly != null)
            {
                Log($"Resolved assembly: \"{args.Name}\" -> \"{library.Assembly.FullName}\" @ \"{library.Assembly.Location}\".");
                return library.Assembly;
            }
        }

        Log($"Unresolved assembly: \"{args.Name}\".");
        if (args.RequestingAssembly != null)
            Log($"    - From assembly: \"{args.RequestingAssembly.FullName}\".");

        return null;
    }

    internal static void Log(string msg)
    {
        Logger.DevkitServer.LogInfo(Source, msg.Colorize(LogColorArgb));
    }

    public void Dispose()
    {
        AppDomain.CurrentDomain.AssemblyLoad -= CurrentDomainAssemblyLoad;
        AppDomain.CurrentDomain.AssemblyResolve -= CurrentDomainOnAssemblyResolve;
    }

    internal void ShutdownUnsupportedModules()
    {
        for (int i = 0; i < _unsupportedModules.Length; i++)
        {
            ref UnsupportedModule unsupportedModule = ref _unsupportedModules[i];
            foreach (Module module in ModuleHook.modules)
            {
                if (module.assemblies != null && module.assemblies.Any(x => x == AccessorExtensions.DevkitServer))
                    continue;

                foreach (ModuleAssembly asmInfo in module.config.Assemblies)
                {
                    string filePath = FileUtil.GetModuleAssemblyPath(module.config, asmInfo);
                    if (!File.Exists(filePath))
                        continue;

                    try
                    {
                        AssemblyName asmName = AssemblyName.GetAssemblyName(filePath);
                        if (unsupportedModule.AssemblyName.Name.Equals(asmName.Name, StringComparison.OrdinalIgnoreCase) &&
                            unsupportedModule.AssemblyName.GetPublicKeyToken().SequenceEqual(asmName.GetPublicKeyToken()))
                        {
                            if (unsupportedModule.ShouldUnloadDevkitServerIfPresent)
                            {
                                Log($"Unsupported module found: {unsupportedModule.Name} at {filePath.Format()}.");
                                DevkitServerModule.Fault();
                                return;
                            }
                            if (unsupportedModule.ShouldUnloadOtherModuleIfPresent)
                            {
                                UnloadOtherModule(module);
                            }
                            if ((unsupportedModule.ShouldUnloadDevkitServerIfPresent || unsupportedModule.ShouldUnloadOtherModuleIfPresent) && !unsupportedModule.HasTriedToLoad)
                            {
                                unsupportedModule.OnTriedToLoad?.Invoke(this);
                                unsupportedModule.HasTriedToLoad = true;
                            }
                        }
                    }
#if !DEBUG
                    catch { /* ignored */ }
#else
                    catch (Exception ex)
                    {
                        Log($"Error checking assembly name of {filePath.Format()}:");
                        Log(ex.ToString());
                    }
#endif
                }
            }
        }
    }

    private static void UnloadOtherModule(Module module)
    {
        if (module.isEnabled) // module was loaded before DevkitServer
        {
            try
            {
                module.isEnabled = false;
                Log($"Unloaded unsupported module: {module.config.Name}.");
            }
            catch (Exception ex)
            {
                Log($"Unsupported module failed to unload: {module.config.Name}.");
                Log(ex.ToString());
                DevkitServerModule.Fault();
            }
        }
        else if (module.config.IsEnabled)
        {
            module.config.IsEnabled = false;
            Log($"Stopped unsupported module from loading: {module.config.Name}.");
        }
    }

    private struct UnsupportedModule
    {
        public readonly string Name;
        public readonly AssemblyName AssemblyName;
        public readonly bool ShouldUnloadDevkitServerIfPresent;
        public readonly bool ShouldUnloadOtherModuleIfPresent;
        public readonly Action<AssemblyResolver>? OnTriedToLoad;

        public bool HasTriedToLoad;
        public UnsupportedModule(string name, AssemblyName assemblyName, Action<AssemblyResolver>? onTriedToLoad = null, bool shouldUnloadDevkitServerIfPresent = false, bool shouldUnloadOtherModuleIfPresent = true)
        {
            Name = name;
            AssemblyName = assemblyName;
            OnTriedToLoad = onTriedToLoad;
            ShouldUnloadDevkitServerIfPresent = shouldUnloadDevkitServerIfPresent;
            ShouldUnloadOtherModuleIfPresent = shouldUnloadOtherModuleIfPresent;
            HasTriedToLoad = false;
        }
    }
}