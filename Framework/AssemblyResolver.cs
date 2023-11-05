﻿using DevkitServer.API;
using DevkitServer.Plugins;
using SDG.Framework.Modules;
using System.Reflection;
using System.Runtime.CompilerServices;
using Module = SDG.Framework.Modules.Module;

namespace DevkitServer.Framework;
internal class AssemblyResolver : IDisposable
{
    internal bool TriedToLoadUIExtensionModule { get; private set; }

    private const int LogColorArgb = -4663389;

    private readonly UnsupportedModule[] _unsupportedModules =
    {
        new UnsupportedModule("LevelObjectIcons", new AssemblyName("LevelObjectIcons, Version=1.0.0.0, Culture=neutral, PublicKeyToken=355a72a9074e7dec"),
            onTriedToLoad: _ =>
            {
                Log(" + Module 'LevelObjectIcons' is unsupported because DevkitServer already implements the same LevelObjectIcon UI.");
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
        /*
         * No clue why System.Runtime.CompilerServices.Unsafe refuses to load but this fixes it.
         */

        const string sysRtCsUnsafe = "System.Runtime.CompilerServices.Unsafe";

        if (args.Name.StartsWith(sysRtCsUnsafe, StringComparison.Ordinal) && args.Name.Length == sysRtCsUnsafe.Length || args.Name[sysRtCsUnsafe.Length] == ',')
        {
            Assembly asm = typeof(Unsafe).Assembly;
            Log($"Resolved assembly: \"{args.Name}\" -> \"{asm.FullName}\" @ \"{asm.Location}\".");
            return asm;
        }

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
        Log($"    - From assembly: \"{args.RequestingAssembly.FullName}\".");

        return null;
    }

    private static void Log(string msg)
    {
        if (!DevkitServerModule.InitializedLogging)
        {
            Console.WriteLine($"[{DateTime.UtcNow.ToString(Logger.TimeFormat)}] [DEVKIT SERVER] [INFO]  {msg}");
            UnturnedLog.info("[DEVKIT SERVER] [INFO]  " + msg);
        }
        else
        {
            Logger.LogInfo(msg.Colorize(LogColorArgb), ConsoleColor.DarkGray);
        }
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
                if (module.assemblies != null && module.assemblies.Any(x => x == Accessor.DevkitServer))
                    continue;

                foreach (ModuleAssembly asmInfo in module.config.Assemblies)
                {
                    string filePath = DevkitServerUtility.GetModuleAssemblyPath(module.config, asmInfo);
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