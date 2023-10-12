using System.Reflection;
using System.Runtime.CompilerServices;
using DevkitServer.Plugins;

namespace DevkitServer.Framework;
internal class AssemblyResolver : IDisposable
{
    private const int LogColorArgb = -4663389;
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
}
