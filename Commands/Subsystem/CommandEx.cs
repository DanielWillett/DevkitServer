using DevkitServer.API;
using DevkitServer.API.Commands;
using DevkitServer.API.Permissions;
using DevkitServer.Plugins;
using System.Reflection;

namespace DevkitServer.Commands.Subsystem;
public static class CommandEx
{
    public static void LogDebug(this IExecutableCommand command, string message, ConsoleColor color = ConsoleColor.DarkGray)
    {
        message = "[" + command.CommandName.ToUpperInvariant() + " CMD] " + message;
        if (command.Plugin != null)
            command.Plugin.LogDebug(message, color);
        else
            Logger.LogDebug(message, color);
    }
    public static void LogInfo(this IExecutableCommand command, string message, ConsoleColor color = ConsoleColor.DarkCyan)
    {
        message = "[" + command.CommandName.ToUpperInvariant() + " CMD] " + message;
        if (command.Plugin != null)
            command.Plugin.LogInfo(message, color);
        else
            Logger.LogInfo(message, color);
    }
    public static void LogWarning(this IExecutableCommand command, string message, ConsoleColor color = ConsoleColor.Yellow)
    {
        if (command.Plugin != null)
            command.Plugin.LogWarning("[" + command.CommandName.ToUpperInvariant() + " CMD] " + message, color);
        else
            Logger.LogWarning(message, color, method: command.CommandName.ToUpperInvariant() + " CMD");
    }
    public static void LogError(this IExecutableCommand command, string message, ConsoleColor color = ConsoleColor.Red)
    {
        if (command.Plugin != null)
            command.Plugin.LogError("[" + command.CommandName.ToUpperInvariant() + " CMD] " + message, color);
        else
            Logger.LogError(message, color, method: command.CommandName.ToUpperInvariant() + " CMD");
    }
    public static void LogError(this IExecutableCommand command, Exception ex)
    {
        if (command.Plugin != null)
            command.Plugin.LogError(ex);
        else
            Logger.LogError(ex, method: command.CommandName.ToUpperInvariant() + " CMD");
    }
    public static void RegisterVanillaCommands()
    {
#if SERVER
        foreach (Command command in Commander.commands)
        {
            if (command == null) continue;
            VanillaCommand cmd = new VanillaCommand(command);

            CommandHandler.Handler.TryRegisterCommand(cmd);
        }
#endif
    }
    public static void DefaultReflectCommands()
    {
        Type exe = typeof(IExecutableCommand);

        Assembly local = Assembly.GetExecutingAssembly();
        foreach (Assembly asm in new Assembly[] { local }.Concat(PluginLoader.Plugins.Select(x => x.Assembly.Assembly).Distinct()))
        {
            foreach (Type type in Accessor.GetTypesSafe(asm))
            {
                if (type.IsAbstract || !exe.IsAssignableFrom(type) || Attribute.IsDefined(type, typeof(IgnoreAttribute)))
                    continue;
                try
                {
                    IExecutableCommand command = (IExecutableCommand)Activator.CreateInstance(type);
                    IDevkitServerPlugin? plugin = PluginLoader.FindPluginForMember(type);
                    if (plugin == null && local != asm)
                    {
                        command.LogError("Unable to link " + type.Format() + " to a plugin. Use the " + typeof(PluginIdentifierAttribute).Format() +
                                         " to link a command to a plugin when multiple plugins are loaded from an assembly.");
                        continue;
                    }
                    if (local == asm)
                    {
                        foreach (Permission perm in command.Permissions)
                        {
                            if (!perm.DevkitServer)
                                command.LogWarning("DevkitServer flag not set in DevkitServer permission: " + perm.Format() + ".");
                            if (perm.Core)
                                command.LogWarning("Core flag set in DevkitServer permission: " + perm.Format() + ".");
                            if (perm.Plugin != null)
                                command.LogWarning("Plugin property set in DevkitServer permission: " + perm.Format() + ".");
                        }
                    }
                    else
                    {
                        foreach (Permission perm in command.Permissions)
                        { 
                            if (perm.DevkitServer)
                                command.LogWarning("DevkitServer flag set in permission: " + perm.Format() + ".");
                            if (perm.Core)
                                command.LogWarning("Core flag set in permission: " + perm.Format() + ".");
                            if (perm.Plugin == null)
                                command.LogWarning("Plugin property not set in permission: " + perm.Format() + ".");
                        }
                    }

                    command.Plugin = plugin;
                    CommandHandler.Handler.TryRegisterCommand(command);
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error loading command of type " + type.Format() + " from assembly " + asm.GetName().Name.Format() + ".");
                    Logger.LogInfo("If you don't want this command to load, add the " + typeof(IgnoreAttribute).Format() + " to it's class.");
                    Logger.LogError(ex);
                }
            }
        }
    }
}
