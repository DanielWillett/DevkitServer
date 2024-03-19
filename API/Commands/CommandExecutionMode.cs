using DevkitServer.Players;

namespace DevkitServer.API.Commands;

/// <summary>
/// Defines when a command is allowed to be executed (singleplayer, multiplayer, etc).
/// </summary>
[Flags]
public enum CommandExecutionMode
{
    /// <summary>
    /// Always allow this command.
    /// </summary>
    Always = 0,

    /// <summary>
    /// Only run this command in edit mode.
    /// </summary>
    RequireEditing = 1 << 0,

    /// <summary>
    /// Only run this command in play mode.
    /// </summary>
    RequirePlaying = 1 << 1,

    /// <summary>
    /// Only run this command in menu.
    /// </summary>
    RequireMenu = 1 << 2,

    /// <summary>
    /// Only run this command in multiplayer.
    /// </summary>
    RequireMultiplayer = 1 << 3,

    /// <summary>
    /// Only run this command in singleplayer.
    /// </summary>
    RequireSingleplayer = 1 << 4,

    /// <summary>
    /// Only run this command in multiplayer edit mode. Includes <see cref="RequireEditing"/> and <see cref="RequireMultiplayer"/>.
    /// </summary>
    RequireMultiEditing = RequireMultiplayer | RequireEditing,

    /// <summary>
    /// Only run this command in singleplayer edit mode. Includes <see cref="RequireEditing"/> and <see cref="RequireSingleplayer"/>.
    /// </summary>
    RequireSingleEditing = RequireSingleplayer | RequireEditing,

    /// <summary>
    /// Only run this command in multiplayer play mode. Includes <see cref="RequirePlaying"/> and <see cref="RequireMultiplayer"/>.
    /// </summary>
    /// <remarks>Includes LAN.</remarks>
    RequireMultiPlaying = RequireMultiplayer | RequirePlaying,

    /// <summary>
    /// Only run this command in singleplayer play mode. Includes <see cref="RequirePlaying"/> and <see cref="RequireSingleplayer"/>.
    /// </summary>
    RequireSinglePlaying = RequireSingleplayer | RequirePlaying,

    /// <summary>
    /// Cheats Enabled must be in Commands.dat if on a server.
    /// </summary>
    RequireCheatsEnabled = 1 << 5,

    /// <summary>
    /// Doesn't check control mode when in mult-edit mode. For example,
    /// if you're in player controller mode <see cref="RequireEditing"/>
    /// will pass instead of <see cref="RequirePlaying"/>.
    /// </summary>
    IgnoreControlMode = 1 << 6,

    /// <summary>
    /// Only run this command in multiplayer play mode while using <see cref="CameraController.Player"/> in multiplayer edit mode. Includes <see cref="RequireMultiEditing"/>.
    /// </summary>
    PlayerControlModeOnly = (1 << 7) | RequireMultiEditing,

    /// <summary>
    /// This command is only enabled in multiplayer and singleplayer play modes.
    /// </summary>
    VanillaSituationsOnly = RequireSinglePlaying | RequireMultiPlaying,

    /// <summary>
    /// This command can not be ran in menu.
    /// </summary>
    NoMenu = 1 << 8,

    /// <summary>
    /// This command can not be used.
    /// </summary>
    Disabled = 1 << 31
}