using DevkitServer.API.Commands;

namespace DevkitServer.Core.Commands.Subsystem;

/// <summary>
/// Contains information about vanilla command types.
/// </summary>
/// <remarks>Used for the command override system.</remarks>
public static class VanillaCommandInfo
{
    /// <summary>
    /// Get information about a vanilla command.
    /// </summary>
    /// <param name="command">Type deriving from <see cref="Command"/>.</param>
    /// <param name="mode">The execution situation flags for the command.</param>
    /// <param name="terminalOnly">If the command should only be executed from the console/terminal.</param>
    /// <param name="dedicatedServerOnly">If the command can only run when <see cref="Dedicator.IsDedicatedServer"/> is <see langword="true"/>.</param>
    /// <param name="serverOnly">If the command can only run when <see cref="Provider.isServer"/> is <see langword="true"/>.</param>
    /// <param name="startupOnly">If the command can only be ran from the <c>Commands.dat</c> file.</param>
    public static void GetInfo(Type command, out CommandExecutionMode mode, out bool terminalOnly, out bool dedicatedServerOnly, out bool serverOnly, out bool startupOnly)
    {
        terminalOnly = false;
        dedicatedServerOnly = false;
        serverOnly = false;
        startupOnly = false;
        mode = CommandExecutionMode.Always;
        // using names in case a type gets removed.

        if (command.Name.Equals("CommandAdmin", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.RequireMultiplayer;
            dedicatedServerOnly = true;
        }
        else if (command.Name.Equals("CommandAdmins", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.RequireMultiplayer;
            terminalOnly = true;
            dedicatedServerOnly = true;
        }
        else if (command.Name.Equals("CommandAirdrop", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.RequirePlaying;
            serverOnly = true;
        }
        else if (command.Name.Equals("CommandAnimal", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.RequireCheatsEnabled | CommandExecutionMode.RequirePlaying;
            serverOnly = true;
        }
        else if (command.Name.Equals("CommandArmor", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.Disabled;
        }
        else if (command.Name.Equals("CommandBan", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.RequireMultiplayer;
            dedicatedServerOnly = true;
        }
        else if (command.Name.Equals("CommandBans", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.RequireMultiplayer;
            terminalOnly = true;
            dedicatedServerOnly = true;
        }
        else if (command.Name.Equals("CommandBind", StringComparison.Ordinal))
        {
            // startup only
            mode = CommandExecutionMode.RequireMultiplayer;
            dedicatedServerOnly = true;
            startupOnly = true;
        }
        else if (command.Name.Equals("CommandCamera", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.RequireMultiplayer;
            dedicatedServerOnly = true;
            startupOnly = true;
        }
        else if (command.Name.Equals("CommandChatrate", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.RequireMultiplayer;
            dedicatedServerOnly = true;
        }
        else if (command.Name.Equals("CommandCheats", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.RequireMultiplayer;
            dedicatedServerOnly = true;
            startupOnly = true;
        }
        else if (command.Name.Equals("CommandCopyServerCode", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.RequireMultiplayer;
            serverOnly = true;
            terminalOnly = true;
        }
        else if (command.Name.Equals("CommandCopyFakeIP", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.RequireMultiplayer;
            serverOnly = true;
            terminalOnly = true;
        }
        else if (command.Name.Equals("CommandCycle", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.Always;
            serverOnly = true;
        }
        else if (command.Name.Equals("CommandDay", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.Always;
            serverOnly = true;
        }
        else if (command.Name.Equals("CommandDebug", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.Always;
            terminalOnly = true;
            dedicatedServerOnly = true;
        }
        else if (command.Name.Equals("CommandDecay", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.Disabled;
        }
        else if (command.Name.Equals("CommandEffectUI", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.RequirePlaying;
            serverOnly = true;
        }
        else if (command.Name.Equals("CommandExperience", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.RequireCheatsEnabled | CommandExecutionMode.RequirePlaying;
            serverOnly = true;
        }
        else if (command.Name.Equals("CommandFilter", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.RequireMultiplayer;
            dedicatedServerOnly = true;
            startupOnly = true;
        }
        else if (command.Name.Equals("CommandFlag", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.RequireCheatsEnabled | CommandExecutionMode.RequirePlaying;
            serverOnly = true;
        }
        else if (command.Name.Equals("CommandGameMode", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.Disabled;
            startupOnly = true;
        }
        else if (command.Name.Equals("CommandGive", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.RequireCheatsEnabled | CommandExecutionMode.RequirePlaying;
            serverOnly = true;
        }
        else if (command.Name.Equals("CommandGold", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.RequireMultiplayer;
            dedicatedServerOnly = true;
            startupOnly = true;
        }
        else if (command.Name.Equals("CommandGSLT", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.RequireMultiplayer;
            dedicatedServerOnly = true;
            startupOnly = true;
        }
        else if (command.Name.Equals("CommandHelp", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.Always;
        }
        else if (command.Name.Equals("CommandHideAdmins", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.RequireMultiplayer;
            dedicatedServerOnly = true;
            startupOnly = true;
        }
        else if (command.Name.Equals("CommandKick", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.RequireMultiplayer;
            dedicatedServerOnly = true;
        }
        else if (command.Name.Equals("CommandKill", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.RequirePlaying;
            serverOnly = true;
        }
        else if (command.Name.Equals("CommandLoadout", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.RequirePlaying;
        }
        else if (command.Name.Equals("CommandLog", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.RequireMultiplayer;
            dedicatedServerOnly = true;
        }
        else if (command.Name.Equals("CommandLogTransportConnections", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.RequireMultiplayer;
            serverOnly = true;
            terminalOnly = true;
        }
        else if (command.Name.Equals("CommandLogMemoryUsage", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.NoMenu;
            serverOnly = true;
        }
        else if (command.Name.Equals("CommandMap", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.RequireMultiplayer;
            dedicatedServerOnly = true;
            startupOnly = true;
        }
        else if (command.Name.Equals("CommandMaxPlayers", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.RequireMultiplayer;
            dedicatedServerOnly = true;
        }
        else if (command.Name.Equals("CommandMode", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.RequireMultiplayer;
            dedicatedServerOnly = true;
            startupOnly = true;
        }
        else if (command.Name.Equals("CommandModules", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.Always;
            terminalOnly = true;
        }
        else if (command.Name.Equals("CommandName", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.RequireMultiplayer;
            dedicatedServerOnly = true;
        }
        else if (command.Name.Equals("CommandNight", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.NoMenu;
            serverOnly = true;
        }
        else if (command.Name.Equals("CommandNpcEvent", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.RequireCheatsEnabled | CommandExecutionMode.RequirePlaying;
            serverOnly = true;
        }
        else if (command.Name.Equals("CommandOwner", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.RequireMultiplayer;
            dedicatedServerOnly = true;
            startupOnly = true;
        }
        else if (command.Name.Equals("CommandPassword", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.RequireMultiplayer;
            dedicatedServerOnly = true;
            startupOnly = true;
        }
        else if (command.Name.Equals("CommandPermit", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.RequireMultiplayer;
            dedicatedServerOnly = true;
        }
        else if (command.Name.Equals("CommandPermits", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.RequireMultiplayer;
            terminalOnly = true;
            dedicatedServerOnly = true;
        }
        else if (command.Name.Equals("CommandPlayers", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.RequireMultiplayer;
            terminalOnly = true;
            dedicatedServerOnly = true;
        }
        else if (command.Name.Equals("CommandPort", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.RequireMultiplayer;
            dedicatedServerOnly = true;
            startupOnly = true;
        }
        else if (command.Name.Equals("CommandPvE", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.RequireMultiplayer;
            dedicatedServerOnly = true;
            startupOnly = true;
        }
        else if (command.Name.Equals("CommandQuest", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.RequireCheatsEnabled | CommandExecutionMode.RequirePlaying;
            serverOnly = true;
        }
        else if (command.Name.Equals("CommandQueue", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.RequireMultiplayer;
            dedicatedServerOnly = true;
        }
        else if (command.Name.Equals("CommandReload", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.Always;
            serverOnly = true;
        }
        else if (command.Name.Equals("CommandReputation", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.RequireCheatsEnabled | CommandExecutionMode.RequirePlaying;
            serverOnly = true;
        }
        else if (command.Name.Equals("CommandResetConfig", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.Always;
            serverOnly = true;
        }
        else if (command.Name.Equals("CommandSave", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.NoMenu;
            serverOnly = true;
        }
        else if (command.Name.Equals("CommandSay", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.NoMenu;
            dedicatedServerOnly = true;
        }
        else if (command.Name.Equals("CommandScheduledShutdownInfo", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.RequireMultiplayer;
            terminalOnly = true;
            serverOnly = true;
        }
        else if (command.Name.Equals("CommandSetNpcSpawnId", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.RequireCheatsEnabled | CommandExecutionMode.RequirePlaying;
            serverOnly = true;
        }
        else if (command.Name.Equals("CommandShutdown", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.RequireMultiplayer;
            dedicatedServerOnly = true;
        }
        else if (command.Name.Equals("CommandSlay", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.RequireMultiplayer;
            dedicatedServerOnly = true;
        }
        else if (command.Name.Equals("CommandSpy", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.RequireMultiplayer;
            serverOnly = true;
        }
        else if (command.Name.Equals("CommandSync", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.Always;
            dedicatedServerOnly = true;
            startupOnly = true;
        }
        else if (command.Name.Equals("CommandTeleport", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.RequirePlaying;
            serverOnly = true;
        }
        else if (command.Name.Equals("CommandTime", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.NoMenu;
            serverOnly = true;
        }
        else if (command.Name.Equals("CommandTimeout", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.RequireMultiplayer;
            dedicatedServerOnly = true;
        }
        else if (command.Name.Equals("CommandUnadmin", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.RequireMultiplayer;
            dedicatedServerOnly = true;
        }
        else if (command.Name.Equals("CommandUnban", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.RequireMultiplayer;
            dedicatedServerOnly = true;
        }
        else if (command.Name.Equals("CommandUnlockNpcAchievement", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.RequireCheatsEnabled | CommandExecutionMode.RequirePlaying;
            serverOnly = true;
        }
        else if (command.Name.Equals("CommandUnpermit", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.RequireMultiplayer;
            dedicatedServerOnly = true;
        }
        else if (command.Name.Equals("CommandVehicle", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.RequireCheatsEnabled | CommandExecutionMode.RequirePlaying;
            serverOnly = true;
        }
        else if (command.Name.Equals("CommandVehicle", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.RequireCheatsEnabled | CommandExecutionMode.RequirePlaying;
            serverOnly = true;
        }
        else if (command.Name.Equals("CommandVotify", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.RequireMultiplayer;
            dedicatedServerOnly = true;
        }
        else if (command.Name.Equals("CommandWeather", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.NoMenu;
            serverOnly = true;
        }
        else if (command.Name.Equals("CommandWelcome", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.Always;
            dedicatedServerOnly = true;
        }
        else if (command.Name.Equals("CommandWhitelisted", StringComparison.Ordinal))
        {
            mode = CommandExecutionMode.Always;
            dedicatedServerOnly = true;
            startupOnly = true;
        }
        else
        {
            Logger.LogWarning($"Unknown vanilla command type: {command.Format()}. This command may be allowed at times it shouldn't be.");
        }

        serverOnly |= dedicatedServerOnly;
    }
}
