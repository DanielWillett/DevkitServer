namespace DevkitServer.API.Permissions;

/// <summary>
/// Handles the saving, loading, and replication of client or server permissions.
/// </summary>
public interface IUserPermissionHandler
{
    /// <summary>
    /// Ran on startup, or when the <see cref="UserPermissions.UserHandler"/> setter is ran.
    /// </summary>
    void Init();

#if SERVER
    /// <summary>
    /// Called when a <see cref="Permission"/> is given to or taken from a user.
    /// </summary>
    event Action<PermissionBranch, ulong, bool> UserPermissionUpdated;

    /// <summary>
    /// Called when a <see cref="PermissionGroup"/> is given to or taken from a user.
    /// </summary>
    event Action<PermissionGroup, ulong, bool> UserPermissionGroupUpdated;

    /// <summary>
    /// Reload any configs.
    /// </summary>
    void Reload();

    /// <summary>
    /// Gives a lone <see cref="PermissionBranch"/> to a user.
    /// <remarks>Saves to dat file.</remarks>
    /// </summary>
    void AddPermission(ulong user, PermissionBranch permission);

    /// <summary>
    /// Removes a lone <see cref="PermissionBranch"/> from a user.
    /// <remarks>Saves to dat file.</remarks>
    /// </summary>
    void RemovePermission(ulong user, PermissionBranch permission);

    /// <summary>
    /// Adds a <see cref="PermissionGroup"/> to a user.
    /// <remarks>Saves to dat file.</remarks>
    /// </summary>
    void AddPermissionGroup(ulong user, PermissionGroup group);

    /// <summary>
    /// Removes a <see cref="PermissionGroup"/> from a user.
    /// <remarks>Saves to dat file.</remarks>
    /// </summary>
    void RemovePermissionGroup(ulong user, PermissionGroup group);

    /// <summary>
    /// Removes all <see cref="PermissionBranch"/>es from a user.
    /// <remarks>Saves to dat file.</remarks>
    /// </summary>
    void ClearPermissions(ulong user);

    /// <summary>
    /// Removes all <see cref="PermissionGroup"/>s from a user.
    /// <remarks>Saves to dat file.</remarks>
    /// </summary>
    void ClearPermissionGroups(ulong user);

    /// <summary>
    /// Get a list of all <see cref="PermissionBranch"/>es a user has.
    /// </summary>
    /// <remarks>If the user is offline or <paramref name="forceReload"/> is <see langword="true"/>,
    /// their dat file will be read instead of referencing their <see cref="EditorUser.Permissions"/> data.</remarks>
    IReadOnlyList<PermissionBranch> GetPermissions(ulong user, bool forceReload = false);

    /// <summary>
    /// Get a list of all <see cref="PermissionGroup"/>s a user has.
    /// </summary>
    /// <remarks>If the user is offline or <paramref name="forceReload"/> is <see langword="true"/>,
    /// their dat file will be read instead of referencing their <see cref="EditorUser.PermissionGroups"/> data.</remarks>
    IReadOnlyList<PermissionGroup> GetPermissionGroups(ulong user, bool forceReload = false);

#else
    /// <summary>
    /// Called when a <see cref="Permission"/> is given to or taken from the client.
    /// </summary>
    event Action<PermissionBranch, bool> UserPermissionUpdated;

    /// <summary>
    /// Called when a <see cref="PermissionGroup"/> is given to or taken from the client.
    /// </summary>
    event Action<PermissionGroup, bool> UserPermissionGroupUpdated;

    /// <summary>
    /// List of all <see cref="PermissionBranch"/>es the client has.
    /// </summary>
    IReadOnlyList<PermissionBranch> Permissions { get; }

    /// <summary>
    /// List of all <see cref="PermissionGroup"/>s the client has.
    /// </summary>
    IReadOnlyList<PermissionGroup> PermissionGroups { get; }

    /// <summary>
    /// Called when the client receives their permission info on join.
    /// </summary>
    void ReceivePermissions(IReadOnlyList<PermissionBranch> permissions, IReadOnlyList<PermissionGroup> groups);

    /// <summary>
    /// Called when the client receives a state update from the server for one <see cref="PermissionBranch"/>.
    /// </summary>
    void ReceivePermissionState(PermissionBranch permission, bool state);

    /// <summary>
    /// Called when the client receives a state update from the server for one <see cref="PermissionGroup"/>.
    /// </summary>
    void ReceivePermissionGroupState(PermissionGroup group, bool state);

    /// <summary>
    /// Called when the client receives a clear <see cref="PermissionBranch"/>es demand from the server.
    /// </summary>
    void ReceiveClearPermissions();

    /// <summary>
    /// Called when the client receives a clear <see cref="PermissionGroup"/>s demand from the server.
    /// </summary>
    void ReceiveClearPermissionGroups();
#endif
}
