namespace DevkitServer.API.Permissions;

/// <summary>
/// Handles the registration, saving, and loading of permission and permission groups.
/// </summary>
public interface IPermissionHandler
{
    /// <summary>
    /// Fired when a <see cref="PermissionGroup"/> is changed.
    /// </summary>
    /// <remarks>Sever-side or client-side.</remarks>
    event Action<PermissionGroup> PermissionGroupUpdated;

    /// <summary>
    /// Fired when a <see cref="PermissionGroup"/> is registered (including on startup).
    /// </summary>
    /// <remarks>Sever-side or client-side.</remarks>
    event Action<PermissionGroup> PermissionGroupRegistered;

    /// <summary>
    /// Fired when a <see cref="PermissionGroup"/> is deregistered (not including on shutdown).
    /// </summary>
    /// <remarks>Sever-side or client-side.</remarks>
    event Action<PermissionGroup> PermissionGroupDeregistered;

    /// <summary>
    /// Ran on startup, or when the <see cref="UserPermissions.Handler"/> setter is ran. Not necessarily on main thread.
    /// </summary>
    void Init();

    /// <summary>
    /// List of all registered <see cref="PermissionGroup"/>s.
    /// </summary>
    /// <remarks>Will stay in sync with the server, <see cref="PermissionGroup.Permissions"/> may not include all permissions on the server (if certain plugins from the server aren't present on the client).</remarks>
    IReadOnlyList<PermissionGroup> PermissionGroups { get; }

    /// <summary>
    /// Reload any configs.
    /// </summary>
    void Reload();

    /// <summary>
    /// Register a <see cref="PermissionGroup"/> that wasn't read from the config file.
    /// </summary>
    /// <remarks>Will replicate to clients and save to config.</remarks>
    /// <returns><see langword="true"/> if the <see cref="PermissionGroup"/> was added 
    /// (otherwise a <see cref="PermissionGroup"/> with the same <see cref="PermissionGroup.Id"/> was already registered).</returns>
    bool Register(PermissionGroup group);

    /// <summary>
    /// Deregister a <see cref="PermissionGroup"/> that has already been registered.
    /// </summary>
    /// <remarks>Will replicate to clients and save to config.</remarks>
    /// <returns><see langword="true"/> if the <see cref="PermissionGroup"/> was removed (otherwise it wasn't registered).</returns>
    bool Deregister(PermissionGroup group);

#if CLIENT

    /// <summary>
    /// Invoked when a <see cref="PermissionGroup"/> update is sent from the server.
    /// </summary>
    /// <remarks>Client-side only.</remarks>
    bool ReceivePermissionGroupUpdate(PermissionGroup group);

#endif
#if SERVER

    /// <summary>
    /// Called when a <see cref="PermissionGroup"/> is updated (by /p command, plugin, etc).
    /// </summary>
    /// <remarks>Will replicate any updates to clients. Server-side only.</remarks>
    void SavePermissionGroup(PermissionGroup group);

#endif
}