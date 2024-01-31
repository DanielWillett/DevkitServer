using DevkitServer.Models;
using DevkitServer.Multiplayer.Actions;

namespace DevkitServer.API.Devkit.Spawns;

/// <summary>
/// Defines a result from a method in <see cref="SpawnUtil"/> that may not merit an exception.
/// </summary>
public enum SpawnpointEventResult
{
    /// <summary>
    /// All went well.
    /// </summary>
    Success,

    /// <summary>
    /// The value was already what you're trying to set it to, so the method did nothing.
    /// </summary>
    IgnoredAlreadySameValue,

    /// <summary>
    /// The spawn at the provided index or <see cref="RegionIdentifier"/> did not exist.
    /// </summary>
    IndexOutOfRange,
    
    /// <summary>
    /// Unable to find a <see cref="NetId64"/> associated with the given spawnpoint. This could be due to a plugin not using DevkitServer APIs.
    /// </summary>
    NetIdNotFound,

    /// <summary>
    /// A OnXXXRequested event in <see cref="ClientEvents"/> cancelled the request.
    /// </summary>
    CancelledByEvent,

    /// <summary>
    /// The current user doesn't have permissions to make this request.
    /// </summary>
    NoPermissions
}
