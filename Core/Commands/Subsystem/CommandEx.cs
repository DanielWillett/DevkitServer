using DanielWillett.ReflectionTools;
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
                        Logger.DevkitServer.LogDebug(nameof(RegisterVanillaCommands), $"Removing command: {command.Format()}, outdated.");
                        CommandHandler.Handler.TryDeregisterCommand(command);
                        continue;
                    }

                    VanillaCommandInfo.GetInfo(command.Command.GetType(), out CommandExecutionMode mode, out _, out bool dedicatedServerOnly, out bool serverOnly, out bool startupOnly);
                    if (mode == CommandExecutionMode.Disabled)
                    {
                        Logger.DevkitServer.LogDebug(nameof(RegisterVanillaCommands), $"Removing command: {command.Format()}, disabled.");
                        CommandHandler.Handler.TryDeregisterCommand(command);
                        continue;
                    }

                    if (startupOnly)
                    {
                        Logger.DevkitServer.LogDebug(nameof(RegisterVanillaCommands), $"Removing command: {command.Format()}, startup only.");
                        CommandHandler.Handler.TryDeregisterCommand(command);
                        continue;
                    }
#if CLIENT
                    if (dedicatedServerOnly)
                    {
                        Logger.DevkitServer.LogDebug(nameof(RegisterVanillaCommands), $"Removing command: {command.Format()}, dedicated server only.");
                        CommandHandler.Handler.TryDeregisterCommand(command);
                        continue;
                    }
#endif
                    if (serverOnly && !Provider.isServer)
                    {
                        Logger.DevkitServer.LogDebug(nameof(RegisterVanillaCommands), $"Removing command: {command.Format()}, authority only.");
                        CommandHandler.Handler.TryDeregisterCommand(command);
                    }

                    if (!PassesMode(mode))
                    {
                        Logger.DevkitServer.LogDebug(nameof(RegisterVanillaCommands), $"Removing command: {command.Format()}, not executable in this mode.");
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
                    Logger.DevkitServer.LogDebug(nameof(RegisterVanillaCommands), $"Skipping command: {command.Format()}, disabled.");
                    continue;
                }

                if (startupOnly)
                {
                    Logger.DevkitServer.LogDebug(nameof(RegisterVanillaCommands), $"Skipping command: {command.Format()}, startup only.");
                    continue;
                }
#if CLIENT
                if (dedicatedServerOnly)
                {
                    Logger.DevkitServer.LogDebug(nameof(RegisterVanillaCommands), $"Skipping command: {command.Format()}, dedicated server only.");
                    continue;
                }
#endif
                if (serverOnly && !Provider.isServer)
                {
                    Logger.DevkitServer.LogDebug(nameof(RegisterVanillaCommands), $"Skipping command: {command.Format()}, authority only.");
                    continue;
                }

                if (!PassesMode(mode))
                {
                    Logger.DevkitServer.LogDebug(nameof(RegisterVanillaCommands), $"Skipping command: {command.Format()}, not executable in this mode.");
                    continue;
                }

                VanillaCommand cmd = new VanillaCommand(command);

                CommandHandler.Handler.TryRegisterCommand(cmd);
            }
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError(nameof(RegisterVanillaCommands), ex, "Failed to register vanilla commands.");
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
                    Logger.DevkitServer.LogError(nameof(DefaultReflectCommands), ex, $"Error loading command of type {type.Format()} from assembly {asm.GetName().Name.Format()}.");
                    Logger.DevkitServer.LogInfo(nameof(DefaultReflectCommands), $"If you don't want this command to load, add the {typeof(IgnoreAttribute).Format()} to it's class.");
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
            if ((mode & CommandExecutionMode.IgnoreControlMode) != 0 && DevkitServerModule.IsEditing)
                throw ctx.Reply(DevkitServerModule.CommandLocalization, "CommandMustBePlayer");

            if ((mode & CommandExecutionMode.IgnoreControlMode) == 0 && ctx.EditorUser != null && ctx.EditorUser.Control.Controller != CameraController.Player)
                throw ctx.Reply(DevkitServerModule.CommandLocalization, "CommandMustBePlayer");
#else
            if ((mode & CommandExecutionMode.IgnoreControlMode) == 0 ? UserControl.LocalController != CameraController.Player : Level.isEditor)
                throw ctx.Reply(DevkitServerModule.CommandLocalization, "CommandMustBePlayer");
#endif
        }

        if ((mode & CommandExecutionMode.RequireEditing) == CommandExecutionMode.RequireEditing)
        {
#if SERVER
            if ((mode & CommandExecutionMode.IgnoreControlMode) != 0 && !DevkitServerModule.IsEditing)
                throw ctx.Reply(DevkitServerModule.CommandLocalization, "CommandMustBeEditor");

            if ((mode & CommandExecutionMode.IgnoreControlMode) == 0 && (!DevkitServerModule.IsEditing || ctx.EditorUser == null || ctx.EditorUser.Control.Controller != CameraController.Editor))
                throw ctx.Reply(DevkitServerModule.CommandLocalization, "CommandMustBeEditor");
#else
            if ((mode & CommandExecutionMode.IgnoreControlMode) == 0 ? UserControl.LocalController != CameraController.Editor : !Level.isEditor)
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
            if (Provider.isServer ? !Provider.hasCheats : Provider.CurrentServerAdvertisement is { hasCheats: false })
                throw ctx.Reply(DevkitServerModule.CommandLocalization, "CommandRequiresCheats");
#endif
        }

        if ((mode & CommandExecutionMode.PlayerControlModeOnly) == CommandExecutionMode.PlayerControlModeOnly)
        {
#if SERVER
            if (ctx.EditorUser == null || ctx.EditorUser.Control.Controller != CameraController.Player)
                throw ctx.Reply(DevkitServerModule.CommandLocalization, "CommandMustBeEditorPlayer");
#else
            if (!DevkitServerModule.IsEditing || UserControl.LocalController != CameraController.Player)
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
            if ((mode & CommandExecutionMode.IgnoreControlMode) != 0 && DevkitServerModule.IsEditing)
                return false;
#else
            if ((mode & CommandExecutionMode.IgnoreControlMode) != 0 && Level.isEditor)
                return false;
#endif
        }

        if ((mode & CommandExecutionMode.RequireEditing) == CommandExecutionMode.RequireEditing)
        {
#if SERVER
            if ((mode & CommandExecutionMode.IgnoreControlMode) != 0 && !DevkitServerModule.IsEditing)
                return false;
#else
            if ((mode & CommandExecutionMode.IgnoreControlMode) != 0 && !Level.isEditor)
                return false;
#endif
        }

        if ((mode & CommandExecutionMode.RequireMultiplayer) == CommandExecutionMode.RequireMultiplayer)
        {
#if SERVER
            if (!Provider.isServer)
                return false;
#else
            if ((Provider.isServer && Provider.isClient && Provider.clients.Count == 1) || !Level.isLoaded)
                return false;
#endif
        }

        if ((mode & CommandExecutionMode.RequireSingleplayer) == CommandExecutionMode.RequireSingleplayer)
        {
#if SERVER
            return false;
#else
            if (!Provider.isServer || !Provider.isClient || Provider.clients.Count != 1 || !Level.isLoaded)
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

        if ((mode & CommandExecutionMode.RequireCheatsEnabled) == CommandExecutionMode.RequireCheatsEnabled)
        {
#if SERVER
            if (!Provider.hasCheats)
                return false;
#else
            if (Provider.isServer ? !Provider.hasCheats : Provider.CurrentServerAdvertisement is { hasCheats: false })
                return false;
#endif
        }

        if ((mode & CommandExecutionMode.PlayerControlModeOnly) == CommandExecutionMode.PlayerControlModeOnly)
        {
#if CLIENT
            if (!DevkitServerModule.IsEditing || UserControl.LocalController != CameraController.Player)
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
