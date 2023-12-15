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
        try
        {
            if (Commander.commands == null)
                Commander.init();
            if (CommandHandler.Handler.Commands != null)
            {
                foreach (VanillaCommand command in CommandHandler.Handler.Commands.OfType<VanillaCommand>().ToList())
                {
                    if (!Commander.commands!.Contains(command.Command))
                    {
                        Logger.LogDebug($"Removing command: {command.Format()}, outdated.");
                        CommandHandler.Handler.TryDeregisterCommand(command);
                        continue;
                    }

                    VanillaCommandInfo.GetInfo(command.Command.GetType(), out CommandExecutionMode mode, out _, out bool dedicatedServerOnly, out bool serverOnly, out bool startupOnly);
                    if (mode == CommandExecutionMode.Disabled)
                    {
                        Logger.LogDebug($"Removing command: {command.Format()}, disabled.");
                        CommandHandler.Handler.TryDeregisterCommand(command);
                        continue;
                    }

                    if (startupOnly)
                    {
                        Logger.LogDebug($"Removing command: {command.Format()}, startup only.");
                        CommandHandler.Handler.TryDeregisterCommand(command);
                        continue;
                    }
#if CLIENT
                    if (dedicatedServerOnly)
                    {
                        Logger.LogDebug($"Removing command: {command.Format()}, dedicated server only.");
                        CommandHandler.Handler.TryDeregisterCommand(command);
                        continue;
                    }
#endif
                    if (serverOnly && !Provider.isServer)
                    {
                        Logger.LogDebug($"Removing command: {command.Format()}, authority only.");
                        CommandHandler.Handler.TryDeregisterCommand(command);
                    }

                    if (!PassesMode(mode))
                    {
                        Logger.LogDebug($"Removing command: {command.Format()}, not executable in this mode.");
                        CommandHandler.Handler.TryDeregisterCommand(command);
                    }
                }
            }

            foreach (Command command in Commander.commands!)
            {
                if (command == null || CommandHandler.Handler.Commands!.Any(x => x is VanillaCommand cmd && cmd.Command == command))
                    continue;

                VanillaCommandInfo.GetInfo(command.GetType(), out CommandExecutionMode mode, out _, out bool dedicatedServerOnly, out bool serverOnly, out bool startupOnly);

                if (mode == CommandExecutionMode.Disabled)
                {
                    Logger.LogDebug($"Skipping command: {command.Format()}, disabled.");
                    continue;
                }

                if (startupOnly)
                {
                    Logger.LogDebug($"Skipping command: {command.Format()}, startup only.");
                    continue;
                }
#if CLIENT
                if (dedicatedServerOnly)
                {
                    Logger.LogDebug($"Skipping command: {command.Format()}, dedicated server only.");
                    continue;
                }
#endif
                if (serverOnly && !Provider.isServer)
                {
                    Logger.LogDebug($"Skipping command: {command.Format()}, authority only.");
                    continue;
                }

                if (!PassesMode(mode))
                {
                    Logger.LogDebug($"Skipping command: {command.Format()}, not executable in this mode.");
                    continue;
                }

                VanillaCommand cmd = new VanillaCommand(command);

                CommandHandler.Handler.TryRegisterCommand(cmd);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to register vanilla commands.");
            Logger.LogError(ex);
        }
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

        if ((mode & CommandExecutionMode.Disabled) == CommandExecutionMode.Disabled)
            throw ctx.Reply(DevkitServerModule.CommandLocalization, "CommandDisabled");

        if ((mode & CommandExecutionMode.RequirePlaying) == CommandExecutionMode.RequirePlaying)
        {
#if SERVER
            if (!Provider.isServer || (mode & CommandExecutionMode.IgnoreControlMode) != 0 || ctx.EditorUser == null || ctx.EditorUser.Input.Controller != CameraController.Player)
                throw ctx.Reply(DevkitServerModule.CommandLocalization, "CommandMustBePlayer");
#else
            if ((mode & CommandExecutionMode.IgnoreControlMode) == 0 ? UserInput.LocalController != CameraController.Player : Level.isEditor)
                throw ctx.Reply(DevkitServerModule.CommandLocalization, "CommandMustBePlayer");
#endif
        }

        if ((mode & CommandExecutionMode.RequireEditing) == CommandExecutionMode.RequireEditing)
        {
#if SERVER
            if (!Provider.isServer || !DevkitServerModule.IsEditing || (mode & CommandExecutionMode.IgnoreControlMode) != 0 || ctx.EditorUser != null && ctx.EditorUser.Input.Controller != CameraController.Editor)
                throw ctx.Reply(DevkitServerModule.CommandLocalization, "CommandMustBeEditor");
#else
            if ((mode & CommandExecutionMode.IgnoreControlMode) == 0 ? UserInput.LocalController != CameraController.Editor : !Level.isEditor)
                throw ctx.Reply(DevkitServerModule.CommandLocalization, "CommandMustBeEditor");
#endif
        }

        if ((mode & CommandExecutionMode.RequireMultiplayer) == CommandExecutionMode.RequireMultiplayer)
        {
#if SERVER
            if (!Provider.isServer)
                throw ctx.Reply(DevkitServerModule.CommandLocalization, "CommandMustBeMultiplayer");
#else
            if ((Provider.isServer && Provider.isClient && Provider.clients.Count == 1) || !Level.isLoaded)
                throw ctx.Reply(DevkitServerModule.CommandLocalization, "CommandMustBeMultiplayer");
#endif
        }

        if ((mode & CommandExecutionMode.RequireSingleplayer) == CommandExecutionMode.RequireSingleplayer)
        {
#if SERVER
            throw ctx.Reply(DevkitServerModule.CommandLocalization, "CommandMustBeSingleplayer");
#else
            if (!Provider.isServer || !Provider.isClient || Provider.clients.Count != 1 || !Level.isLoaded)
                throw ctx.Reply(DevkitServerModule.CommandLocalization, "CommandMustBeSingleplayer");
#endif
        }

        if ((mode & CommandExecutionMode.RequireMenu) == CommandExecutionMode.RequireMenu)
        {
#if SERVER
            throw ctx.Reply(DevkitServerModule.CommandLocalization, "CommandMustBeMenu");
#else
            if (MenuUI.window == null)
                throw ctx.Reply(DevkitServerModule.CommandLocalization, "CommandMustBeMenu");
#endif
        }

        if ((mode & CommandExecutionMode.RequireCheatsEnabled) == CommandExecutionMode.RequireCheatsEnabled)
        {
#if SERVER
            if (!Provider.hasCheats)
                throw ctx.Reply(DevkitServerModule.CommandLocalization, "CommandRequiresCheats");
#else
            if (Provider.isServer ? Provider.hasCheats : Provider.CurrentServerAdvertisement is not { hasCheats: false })
                throw ctx.Reply(DevkitServerModule.CommandLocalization, "CommandRequiresCheats");
#endif
        }

        if ((mode & CommandExecutionMode.PlayerControlModeOnly) == CommandExecutionMode.PlayerControlModeOnly)
        {
#if SERVER
            if (ctx.EditorUser == null || ctx.EditorUser.Input.Controller != CameraController.Player)
                throw ctx.Reply(DevkitServerModule.CommandLocalization, "CommandMustBeEditorPlayer");
#else
            if (!DevkitServerModule.IsEditing || UserInput.LocalController != CameraController.Player)
                throw ctx.Reply(DevkitServerModule.CommandLocalization, "CommandMustBeEditorPlayer");
#endif
        }

        if ((mode & CommandExecutionMode.NoMenu) == CommandExecutionMode.NoMenu)
        {
            if (Level.editing == null)
                throw ctx.Reply(DevkitServerModule.CommandLocalization, "CommandRequiresNotMenu");
        }
    }
    public static bool PassesMode(CommandExecutionMode mode)
    {
        if (mode == CommandExecutionMode.Always)
            return true;

        if ((mode & CommandExecutionMode.Disabled) == CommandExecutionMode.Disabled)
            return false;

        if ((mode & CommandExecutionMode.RequirePlaying) == CommandExecutionMode.RequirePlaying)
        {
#if SERVER
            if (!Provider.isServer)
                return false;
#else
            if ((mode & CommandExecutionMode.IgnoreControlMode) == 0 ? UserInput.LocalController != CameraController.Player : Level.isEditor)
                return false;
#endif
        }

        if ((mode & CommandExecutionMode.RequireEditing) == CommandExecutionMode.RequireEditing)
        {
#if SERVER
            if (!Provider.isServer || !DevkitServerModule.IsEditing)
                return false;
#else
            if ((mode & CommandExecutionMode.IgnoreControlMode) == 0 ? UserInput.LocalController != CameraController.Editor : !Level.isEditor)
                return false;
#endif
        }

        if ((mode & CommandExecutionMode.RequireMultiplayer) == CommandExecutionMode.RequireMultiplayer)
        {
#if SERVER
            if (!Provider.isServer)
                return false;
#else
            if (Level.editing == null)
                return false;
#endif
        }

        if ((mode & CommandExecutionMode.RequireSingleplayer) == CommandExecutionMode.RequireSingleplayer)
        {
#if SERVER
            return false;
#else
            if (!Provider.isServer || !Provider.isClient || Level.editing == null)
                return false;
#endif
        }

        if ((mode & CommandExecutionMode.RequireMenu) == CommandExecutionMode.RequireMenu)
        {
#if SERVER
            return false;
#else
            if (MenuUI.window == null)
                return false;
#endif
        }

        if ((mode & CommandExecutionMode.PlayerControlModeOnly) == CommandExecutionMode.PlayerControlModeOnly)
        {
#if CLIENT
            if (!DevkitServerModule.IsEditing)
                return false;
#endif
        }

        if ((mode & CommandExecutionMode.NoMenu) == CommandExecutionMode.NoMenu)
        {
            if (Level.editing == null)
                return false;
        }

        return true;
    }
}
