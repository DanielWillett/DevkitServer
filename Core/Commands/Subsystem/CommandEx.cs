using DevkitServer.API;
using DevkitServer.API.Commands;
using DevkitServer.API.Permissions;
using DevkitServer.Players;
using DevkitServer.Plugins;
using System.Reflection;

namespace DevkitServer.Core.Commands.Subsystem;
public static class CommandEx
{
    public static Type GetPluginType(this IExecutableCommand command) => command.Plugin?.GetType() ?? (command is VanillaCommand ? typeof(Provider) : typeof(DevkitServerModule));
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
                if (type.IsAbstract || !exe.IsAssignableFrom(type) || type.IsIgnored())
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
                        foreach (PermissionLeaf perm in command.Permissions)
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
                        foreach (PermissionLeaf perm in command.Permissions)
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
    public static void AssertMode(this CommandContext ctx, CommandExecutionMode mode)
    {
        if (mode == CommandExecutionMode.Always)
            return;

        if ((mode & CommandExecutionMode.Disabled) != 0)
            throw ctx.Reply(DevkitServerModule.CommandLocalization, "CommandDisabled");

        if ((mode & CommandExecutionMode.RequirePlaying) != 0)
        {
#if SERVER
            if (!Provider.isServer || (mode & CommandExecutionMode.IgnoreControlMode) != 0 || ctx.EditorUser == null || ctx.EditorUser.Input.Controller != CameraController.Player)
                throw ctx.Reply(DevkitServerModule.CommandLocalization, "CommandMustBePlayer");
#else
            if ((mode & CommandExecutionMode.IgnoreControlMode) == 0 ? UserInput.LocalController != CameraController.Player : Level.isEditor)
                throw ctx.Reply(DevkitServerModule.CommandLocalization, "CommandMustBePlayer");
#endif
        }

        if ((mode & CommandExecutionMode.RequireEditing) != 0)
        {
#if SERVER
            if (!Provider.isServer || !DevkitServerModule.IsEditing || (mode & CommandExecutionMode.IgnoreControlMode) != 0 || ctx.EditorUser != null && ctx.EditorUser.Input.Controller != CameraController.Editor)
                throw ctx.Reply(DevkitServerModule.CommandLocalization, "CommandMustBeEditor");
#else
            if ((mode & CommandExecutionMode.IgnoreControlMode) == 0 ? UserInput.LocalController != CameraController.Editor : !Level.isEditor)
                throw ctx.Reply(DevkitServerModule.CommandLocalization, "CommandMustBeEditor");
#endif
        }

        if ((mode & CommandExecutionMode.RequireMultiplayer) != 0)
        {
#if SERVER
            if (!Provider.isServer)
                throw ctx.Reply(DevkitServerModule.CommandLocalization, "CommandMustBeMultiplayer");
#else
            if ((Provider.isServer && Provider.isClient && Provider.clients.Count == 1) || !Level.isLoaded)
                throw ctx.Reply(DevkitServerModule.CommandLocalization, "CommandMustBeMultiplayer");
#endif
        }

        if ((mode & CommandExecutionMode.RequireSingleplayer) != 0)
        {
#if SERVER
            throw ctx.Reply(DevkitServerModule.CommandLocalization, "CommandMustBeSingleplayer");
#else
            if (!Provider.isServer || !Provider.isClient || Provider.clients.Count != 1 || !Level.isLoaded)
                throw ctx.Reply(DevkitServerModule.CommandLocalization, "CommandMustBeSingleplayer");
#endif
        }

        if ((mode & CommandExecutionMode.RequireMenu) != 0)
        {
#if SERVER
            throw ctx.Reply(DevkitServerModule.CommandLocalization, "CommandMustBeMenu");
#else
            if (MenuUI.window == null)
                throw ctx.Reply(DevkitServerModule.CommandLocalization, "CommandMustBeMenu");
#endif
        }

        if ((mode & CommandExecutionMode.RequireCheatsEnabled) != 0)
        {
#if SERVER
            if (!Provider.hasCheats)
                throw ctx.Reply(DevkitServerModule.CommandLocalization, "CommandRequiresCheats");
#else
            if (Provider.isServer ? Provider.hasCheats : Provider.currentServerInfo is not { hasCheats: false })
                throw ctx.Reply(DevkitServerModule.CommandLocalization, "CommandRequiresCheats");
#endif
        }

        if ((mode & CommandExecutionMode.PlayerControlModeOnly) != 0)
        {
#if SERVER
            if (ctx.EditorUser == null || ctx.EditorUser.Input.Controller != CameraController.Player)
                throw ctx.Reply(DevkitServerModule.CommandLocalization, "CommandMustBeEditorPlayer");
#else
            if (!DevkitServerModule.IsEditing || UserInput.LocalController != CameraController.Player)
                throw ctx.Reply(DevkitServerModule.CommandLocalization, "CommandMustBeEditorPlayer");
#endif
        }
    }
}
