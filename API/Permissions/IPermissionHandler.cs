using DevkitServer.Multiplayer.Networking;
using JetBrains.Annotations;
#if SERVER
using DevkitServer.Players;
#endif

namespace DevkitServer.API.Permissions;
public interface IPermissionHandler
{
    /// <summary>
    /// Fired when a <see cref="PermissionGroup"/> is changed.
    /// </summary>
    /// <remarks>Sever-side or client-side.</remarks>
    event Action<PermissionGroup>? PermissionGroupUpdated;
    /// <summary>
    /// Fired when a <see cref="PermissionGroup"/> is registered (including on startup).
    /// </summary>
    /// <remarks>Sever-side or client-side.</remarks>
    event Action<PermissionGroup>? PermissionGroupRegistered;
    /// <summary>
    /// Fired when a <see cref="PermissionGroup"/> is deregistered (not including on shutdown).
    /// </summary>
    /// <remarks>Sever-side or client-side.</remarks>
    event Action<PermissionGroup>? PermissionGroupDeregistered;
    /// <summary>
    /// Fired when a <see cref="Permission"/> is registered (including on startup).
    /// </summary>
    /// <remarks>Sever-side or client-side.</remarks>
    event Action<Permission>? PermissionRegistered;
    /// <summary>
    /// Fired when a <see cref="Permission"/> is deregistered (not including on shutdown).
    /// </summary>
    /// <remarks>Sever-side or client-side.</remarks>
    event Action<Permission>? PermissionDeregistered;
    /// <summary>
    /// Ran on startup, or when the <see cref="UserPermissions.Handler"/> setter is ran.
    /// </summary>
    void Init();
    /// <summary>
    /// List of all registered <see cref="Permission"/>s.
    /// </summary>
    /// <remarks>These may be out of sync on the client if different plugins are present on the server compared to on the client.</remarks>
    IReadOnlyList<Permission> Permissions { get; }
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
    /// Register a <see cref="Permission"/> that wasn't registered using the <see cref="PermissionAttribute"/>.
    /// </summary>
    /// <remarks>Will replicate to clients.</remarks>
    /// <returns><see langword="true"/> if the <see cref="Permission"/> was added (otherwise it was already registered).</returns>
    bool Register(Permission permission);
    /// <summary>
    /// Register a <see cref="PermissionGroup"/> that wasn't read from the config file.
    /// </summary>
    /// <remarks>Will replicate to clients and save to config.</remarks>
    /// <returns><see langword="true"/> if the <see cref="PermissionGroup"/> was added 
    /// (otherwise a <see cref="PermissionGroup"/> with the same <see cref="PermissionGroup.Id"/> was already registered).</returns>
    bool Register(PermissionGroup group);
    /// <summary>
    /// Deregister a <see cref="Permission"/> that has already been registered.
    /// </summary>
    /// <remarks>Will replicate to clients.</remarks>
    /// <returns><see langword="true"/> if the <see cref="Permission"/> was removed (otherwise it wasn't registered).</returns>
    bool Deregister(Permission permission);
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
    /// <remarks>Will replicate any updates to clients.</remarks>
    void SavePermissionGroup(PermissionGroup group);
#endif
}

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
    event Action<Permission, ulong, bool>? UserPermissionUpdated;
    /// <summary>
    /// Called when a <see cref="PermissionGroup"/> is given to or taken from a user.
    /// </summary>
    event Action<PermissionGroup, ulong, bool>? UserPermissionGroupUpdated;
    /// <summary>
    /// Reload any configs.
    /// </summary>
    void Reload();
    /// <summary>
    /// Gives a lone <see cref="Permission"/> to a user.
    /// <remarks>Saves to dat file.</remarks>
    /// </summary>
    void AddPermission(ulong user, Permission permission);
    /// <summary>
    /// Removes a lone <see cref="Permission"/> from a user.
    /// <remarks>Saves to dat file.</remarks>
    /// </summary>
    void RemovePermission(ulong user, Permission permission);
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
    /// Removes all <see cref="Permission"/>s from a user.
    /// <remarks>Saves to dat file.</remarks>
    /// </summary>
    void ClearPermissions(ulong user);
    /// <summary>
    /// Removes all <see cref="PermissionGroup"/>s from a user.
    /// <remarks>Saves to dat file.</remarks>
    /// </summary>
    void ClearPermissionGroups(ulong user);
    /// <summary>
    /// Get a list of all <see cref="Permission"/>s a user has.
    /// </summary>
    /// <remarks>If the user is offline or <paramref name="forceReload"/> is <see langword="true"/>,
    /// their dat file will be read instead of referencing their <see cref="EditorUser.Permissions"/> data.</remarks>
    IReadOnlyList<Permission> GetPermissions(ulong user, bool forceReload = false);
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
    event Action<Permission, bool>? UserPermissionUpdated;
    /// <summary>
    /// Called when a <see cref="PermissionGroup"/> is given to or taken from the client.
    /// </summary>
    event Action<PermissionGroup, bool>? UserPermissionGroupUpdated;
    /// <summary>
    /// List of all <see cref="Permission"/>s the client has.
    /// </summary>
    IReadOnlyList<Permission> Permissions { get; }
    /// <summary>
    /// List of all <see cref="PermissionGroup"/>s the client has.
    /// </summary>
    IReadOnlyList<PermissionGroup> PermissionGroups { get; }
    /// <summary>
    /// Called when the client receives their permission info on join.
    /// </summary>
    void ReceivePermissions(IReadOnlyList<Permission> permissions, IReadOnlyList<PermissionGroup> groups);
    /// <summary>
    /// Called when the client receives a state update from the server for one <see cref="Permission"/>.
    /// </summary>
    void ReceivePermissionState(Permission permission, bool state);
    /// <summary>
    /// Called when the client receives a state update from the server for one <see cref="PermissionGroup"/>.
    /// </summary>
    void ReceivePermissionGroupState(PermissionGroup group, bool state);
    /// <summary>
    /// Called when the client receives a clear <see cref="Permission"/>s demand from the server.
    /// </summary>
    void ReceiveClearPermissions();
    /// <summary>
    /// Called when the client receives a clear <see cref="PermissionGroup"/>s demand from the server.
    /// </summary>
    void ReceiveClearPermissionGroups();
#endif
}

public static class PermissionsEx
{
    [UsedImplicitly]
    private static readonly NetCall<string> SendPermissionLateRegistered = new NetCall<string>((ushort)NetCalls.SendPermissionLateRegistered);
    [UsedImplicitly]
    private static readonly NetCallRaw<PermissionGroup> SendPermissionGroupLateRegistered = new NetCallRaw<PermissionGroup>(
        (ushort)NetCalls.SendPermissionGroupLateRegistered, PermissionGroup.ReadPermissionGroup, PermissionGroup.WritePermissionGroup);
    [UsedImplicitly]
    private static readonly NetCallRaw<PermissionGroup> SendPermissionGroupUpdate = new NetCallRaw<PermissionGroup>(
        (ushort)NetCalls.SendPermissionGroupUpdate, PermissionGroup.ReadPermissionGroup, PermissionGroup.WritePermissionGroup);
    [UsedImplicitly]
    private static readonly NetCall<string> SendPermissionDeregistered = new NetCall<string>((ushort)NetCalls.SendPermissionDeregistered);
    [UsedImplicitly]
    private static readonly NetCall<string> SendPermissionGroupDeregistered = new NetCall<string>((ushort)NetCalls.SendPermissionGroupDeregistered);
    /// <summary>
    /// Check to see if either the client or a user has a permission.
    /// </summary>
    public static bool Has(this Permission permission
#if SERVER
        , ulong player
#endif
    , bool superuser = true)
    {
        return UserPermissions.UserHandler.HasPermission(
#if SERVER
            player,
#endif
            permission, superuser);
    }
    /// <summary>
    /// Check to see if either the client or a user has a permission.
    /// </summary>
    public static bool HasPermission(this IUserPermissionHandler handler,
#if SERVER
        ulong player,
#endif
        Permission permission, bool superuser = true)
    {
        bool suEx = permission.Equals(Permission.SuperuserPermission);
#if SERVER
        IReadOnlyList<Permission> list = handler.GetPermissions(player);
#else
        IReadOnlyList<Permission> list = handler.Permissions;
#endif
        foreach (Permission p in list)
        {
            if ((!suEx && p.Equals(permission)) || superuser && p.Equals(Permission.SuperuserPermission))
                return true;
        }
#if SERVER
        IReadOnlyList<PermissionGroup> list2 = handler.GetPermissionGroups(player);
#else
        IReadOnlyList<PermissionGroup> list2 = handler.PermissionGroups;
#endif
        bool removedSuperuser = false;
        for (int i = list2.Count - 1; i >= 0; i--)
        {
            PermissionGroup g = list2[i];
            foreach (GroupPermission perm in g.Permissions)
            {
                bool su = superuser && perm.Permission.Equals(Permission.SuperuserPermission);
                if (!removedSuperuser && perm.IsRemoved && su)
                    removedSuperuser = true;
                if ((!suEx && perm.Permission.Equals(permission)) || su && !removedSuperuser)
                    return !perm.IsRemoved;
            }
        }

        return false;
    }
#if CLIENT
    /// <summary>
    /// Check to see if the client has permission to run a vanilla command.
    /// </summary>
    public static bool ClientHasPermissionToRun(this Command command)
    {
        return false;
    }
#endif
#if SERVER
    /// <summary>
    /// Chceks against a list of required permissions to see if the client passes the check.
    /// </summary>
    /// <param name="any">When <see langword="true"/>, the check looks for just one permission available, otherwise it looks for all parameters available.</param>
    /// <param name="superuser">When <see langword="true"/>, the check includes a check for <see cref="Permission.SuperuserPermission"/> (which gives all permissions).</param>
    public static bool CheckPermission(this EditorUser? user, IEnumerable<Permission> permissions, bool any, bool superuser = true)
    {
        if (user == null)
            return true;
        if (superuser && UserPermissions.UserHandler.HasPermission(user.SteamId.m_SteamID, Permission.SuperuserPermission))
            return true;
        bool anyMissed = false, anyHit = false;
        foreach (Permission p in permissions)
        {
            if (UserPermissions.UserHandler.HasPermission(user.SteamId.m_SteamID, p))
                anyHit = true;
            else
                anyMissed = true;
        }

        // if no permissions were provided or the right amount were found
        return !(anyHit || anyMissed) || (any ? anyHit : !anyMissed);
    }
#endif
    /// <summary>
    /// Looks for an already registered <see cref="Permission"/> if it exists and returns it, otherwise returns <paramref name="comparand"/>.
    /// </summary>
    public static Permission TryFindEqualPermission(this IPermissionHandler handler, Permission comparand)
    {
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));
        if (comparand == null)
            throw new ArgumentNullException(nameof(comparand));
        for (int i = 0; i < handler.Permissions.Count; ++i)
        {
            Permission p = handler.Permissions[i];
            if (p.Equals(comparand))
                return p;
        }
        return comparand;
    }
    /// <summary>
    /// Find a registered <see cref="Permission"/> by <paramref name="id"/> and <paramref name="plugin"/>.
    /// </summary>
    public static Permission? FindPermission(this IPermissionHandler handler, IDevkitServerPlugin plugin, string id)
    {
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));
        if (plugin == null)
            throw new ArgumentNullException(nameof(plugin));
        if (id == null)
            throw new ArgumentNullException(nameof(id));

        foreach (Permission permission in handler.Permissions)
        {
            if (permission.Plugin == plugin && permission.Id.Equals(id, StringComparison.InvariantCultureIgnoreCase))
                return permission;
        }

        return null;
    }
    /// <summary>
    /// Find a registered <see cref="Permission"/> by <paramref name="id"/> belonging to the core (unturned) namespace.
    /// </summary>
    public static Permission? FindCorePermission(this IPermissionHandler handler, string id)
    {
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));
        if (id == null)
            throw new ArgumentNullException(nameof(id));

        foreach (Permission permission in handler.Permissions)
        {
            if (permission.Core && permission.Id.Equals(id, StringComparison.InvariantCultureIgnoreCase))
                return permission;
        }

        return null;
    }
    /// <summary>
    /// Find a registered <see cref="Permission"/> by <paramref name="id"/> belonging to the Devkit Server (devkitserver) namespace.
    /// </summary>
    public static Permission? FindDevkitServerPermission(this IPermissionHandler handler, string id)
    {
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));
        if (id == null)
            throw new ArgumentNullException(nameof(id));

        foreach (Permission permission in handler.Permissions)
        {
            if (permission.DevkitServer && permission.Id.Equals(id, StringComparison.InvariantCultureIgnoreCase))
                return permission;
        }

        return null;
    }
    /// <summary>
    /// Find a registered <see cref="PermissionGroup"/> by <paramref name="id"/>.
    /// </summary>
    public static PermissionGroup? FindPermissionGroup(this IPermissionHandler handler, string id)
    {
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));
        if (id == null)
            throw new ArgumentNullException(nameof(id));
        foreach (PermissionGroup group in handler.PermissionGroups)
        {
            if (group.Id.Equals(id, StringComparison.InvariantCultureIgnoreCase))
                return group;
        }

        return null;
    }
    /// <summary>
    /// Find a registered <see cref="PermissionGroup"/> by <paramref name="id"/>.
    /// </summary>
    public static bool TryFindPermissionGroup(this IPermissionHandler handler, string id, out PermissionGroup group)
    {
        group = FindPermissionGroup(handler, id)!;
        return group != null;
    }
    /// <summary>
    /// Find a registered <see cref="Permission"/> by <paramref name="id"/> and <paramref name="plugin"/>.
    /// </summary>
    public static bool TryFindPermission(this IPermissionHandler handler, IDevkitServerPlugin plugin, string id, out Permission permission)
    {
        permission = FindPermission(handler, plugin, id)!;
        return permission != null;
    }
    /// <summary>
    /// Find a registered <see cref="Permission"/> by <paramref name="id"/> belonging to the core (unturned) namespace.
    /// </summary>
    public static bool TryFindCorePermission(this IPermissionHandler handler, string id, out Permission permission)
    {
        permission = FindCorePermission(handler, id)!;
        return permission != null;
    }
    /// <summary>
    /// Find a registered <see cref="Permission"/> by <paramref name="id"/> belonging to the Devkit Server (devkitserver) namespace.
    /// </summary>
    public static bool TryFindDevkitServerPermission(this IPermissionHandler handler, string id, out Permission permission)
    {
        permission = FindDevkitServerPermission(handler, id)!;
        return permission != null;
    }

#if SERVER
    /// <summary>
    /// Tell all clients about a registered <see cref="Permission"/>.
    /// </summary>
    /// <remarks>You shouldn't ever need to call this unless you're creating your own implementation of <see cref="IPermissionHandler"/>.</remarks>
    public static void ReplicateLatePermissionRegistration(Permission permission)
    {
        SendPermissionLateRegistered.Invoke(Provider.GatherRemoteClientConnections(), permission.ToString());
    }
    /// <summary>
    /// Tell all clients about a registered <see cref="PermissionGroup"/>.
    /// </summary>
    /// <remarks>You shouldn't ever need to call this unless you're creating your own implementation of <see cref="IPermissionHandler"/>.</remarks>
    public static void ReplicateLatePermissionGroupRegistration(PermissionGroup group)
    {
        SendPermissionGroupLateRegistered.Invoke(Provider.GatherRemoteClientConnections(), group);
    }
    /// <summary>
    /// Tell all clients about a deregistered <see cref="Permission"/>.
    /// </summary>
    /// <remarks>You shouldn't ever need to call this unless you're creating your own implementation of <see cref="IPermissionHandler"/>.</remarks>
    public static void ReplicatePermissionDeregistration(Permission permission)
    {
        SendPermissionDeregistered.Invoke(Provider.GatherRemoteClientConnections(), permission.ToString());
    }
    /// <summary>
    /// Tell all clients about a deregistered <see cref="PermissionGroup"/>.
    /// </summary>
    /// <remarks>You shouldn't ever need to call this unless you're creating your own implementation of <see cref="IPermissionHandler"/>.</remarks>
    public static void ReplicatePermissionGroupDeregistration(PermissionGroup permissionGroup)
    {
        SendPermissionGroupDeregistered.Invoke(Provider.GatherRemoteClientConnections(), permissionGroup.Id);
    }
    /// <summary>
    /// Tell all clients about an updated <see cref="PermissionGroup"/>.
    /// </summary>
    /// <remarks>You shouldn't ever need to call this unless you're creating your own implementation of <see cref="IPermissionHandler"/>.</remarks>
    public static void ReplicatePermissionGroupUpdate(PermissionGroup permissionGroup)
    {
        SendPermissionGroupUpdate.Invoke(Provider.GatherRemoteClientConnections(), permissionGroup);
    }
#endif

#if CLIENT
    [NetCall(NetCallSource.FromServer, (ushort)NetCalls.SendPermissionLateRegistered)]
    [UsedImplicitly]
    private static void ReceivePermissionLateRegistered(MessageContext ctx, string str)
    {
        if (Permission.TryParse(str, out Permission permission))
        {
            UserPermissions.Handler.Register(permission);
            ctx.Acknowledge(StandardErrorCode.Success);
            return;
        }

        ctx.Acknowledge(StandardErrorCode.NotFound);
    }
    [NetCall(NetCallSource.FromServer, (ushort)NetCalls.SendPermissionGroupLateRegistered)]
    [UsedImplicitly]
    private static void ReceivePermissionGroupLateRegistered(MessageContext ctx, PermissionGroup group)
    {
        UserPermissions.Handler.Register(group);
        ctx.Acknowledge(StandardErrorCode.Success);
    }
    [NetCall(NetCallSource.FromServer, (ushort)NetCalls.SendPermissionDeregistered)]
    [UsedImplicitly]
    private static void ReceivePermissionDeregistered(MessageContext ctx, string str)
    {
        if (Permission.TryParse(str, out Permission permission))
        {
            ctx.Acknowledge(UserPermissions.Handler.Deregister(permission) ? StandardErrorCode.Success : StandardErrorCode.NotFound);
            return;
        }

        ctx.Acknowledge(StandardErrorCode.NotFound);
    }
    [NetCall(NetCallSource.FromServer, (ushort)NetCalls.SendPermissionGroupDeregistered)]
    [UsedImplicitly]
    private static void ReceivePermissionGroupLateRegistered(MessageContext ctx, string groupId)
    {
        foreach (PermissionGroup group in UserPermissions.Handler.PermissionGroups)
        {
            if (group.Id.Equals(groupId, StringComparison.InvariantCultureIgnoreCase))
            {
                ctx.Acknowledge(UserPermissions.Handler.Deregister(group) ? StandardErrorCode.Success : StandardErrorCode.NotFound);
                break;
            }
        }

        ctx.Acknowledge(StandardErrorCode.NotFound);
    }
    [NetCall(NetCallSource.FromServer, (ushort)NetCalls.SendPermissionGroupUpdate)]
    [UsedImplicitly]
    private static void ReceivePermissionGroupUpdate(MessageContext ctx, PermissionGroup group)
    {
        ctx.Acknowledge(UserPermissions.Handler.ReceivePermissionGroupUpdate(group) ? StandardErrorCode.Success : StandardErrorCode.NotFound);
    }
#endif
}