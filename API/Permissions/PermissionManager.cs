using DevkitServer.Core.Commands.Subsystem;
using DevkitServer.Multiplayer.Networking;
#if CLIENT
using DevkitServer.Multiplayer;
#endif
#if SERVER
using DevkitServer.Configuration;
#endif

namespace DevkitServer.API.Permissions;
public static class PermissionManager
{
    /// <summary>
    /// Used by implementations of <see cref="IPermissionHandler"/> or <see cref="IUserPermissionHandler"/> as a multi-threaded lock.
    /// </summary>
    /// <remarks>Do NOT switch to main thread while locked. Do not use in Init or Dispose.</remarks>
    public static SemaphoreSlim PermissionLockingSemaphore { get; } = new SemaphoreSlim(1, 1);

    private static volatile int _inited;

    private static readonly CachedMulticastEvent<Action<PermissionGroup>> EventGlobalPermissionGroupUpdated = new CachedMulticastEvent<Action<PermissionGroup>>(typeof(PermissionManager), nameof(GlobalPermissionGroupUpdated));
    private static readonly CachedMulticastEvent<Action<PermissionGroup>> EventGlobalPermissionGroupRegistered = new CachedMulticastEvent<Action<PermissionGroup>>(typeof(PermissionManager), nameof(GlobalPermissionGroupRegistered));
    private static readonly CachedMulticastEvent<Action<PermissionGroup>> EventGlobalPermissionGroupDeregistered = new CachedMulticastEvent<Action<PermissionGroup>>(typeof(PermissionManager), nameof(GlobalPermissionGroupDeregistered));
    
    public static event Action<PermissionGroup> GlobalPermissionGroupUpdated
    {
        add => EventGlobalPermissionGroupUpdated.Add(value);
        remove => EventGlobalPermissionGroupUpdated.Remove(value);
    }
    public static event Action<PermissionGroup> GlobalPermissionGroupRegistered
    {
        add => EventGlobalPermissionGroupRegistered.Add(value);
        remove => EventGlobalPermissionGroupRegistered.Remove(value);
    }
    public static event Action<PermissionGroup> GlobalPermissionGroupDeregistered
    {
        add => EventGlobalPermissionGroupDeregistered.Add(value);
        remove => EventGlobalPermissionGroupDeregistered.Remove(value);
    }

#if SERVER
    private static readonly CachedMulticastEvent<Action<PermissionBranch, ulong, bool>> EventGlobalUserPermissionUpdated = new CachedMulticastEvent<Action<PermissionBranch, ulong, bool>>(typeof(PermissionManager), nameof(GlobalUserPermissionUpdated));
    private static readonly CachedMulticastEvent<Action<PermissionGroup, ulong, bool>> EventGlobalUserPermissionGroupUpdated = new CachedMulticastEvent<Action<PermissionGroup, ulong, bool>>(typeof(PermissionManager), nameof(GlobalUserPermissionGroupUpdated));

    public static event Action<PermissionBranch, ulong, bool> GlobalUserPermissionUpdated
    {
        add => EventGlobalUserPermissionUpdated.Add(value);
        remove => EventGlobalUserPermissionUpdated.Remove(value);
    }
    public static event Action<PermissionGroup, ulong, bool> GlobalUserPermissionGroupUpdated
    {
        add => EventGlobalUserPermissionGroupUpdated.Add(value);
        remove => EventGlobalUserPermissionGroupUpdated.Remove(value);
    }
#else
    private static readonly CachedMulticastEvent<Action<PermissionBranch, bool>> EventGlobalUserPermissionUpdated = new CachedMulticastEvent<Action<PermissionBranch, bool>>(typeof(PermissionManager), nameof(GlobalUserPermissionUpdated));
    private static readonly CachedMulticastEvent<Action<PermissionGroup, bool>> EventGlobalUserPermissionGroupUpdated = new CachedMulticastEvent<Action<PermissionGroup, bool>>(typeof(PermissionManager), nameof(GlobalUserPermissionGroupUpdated));

    public static event Action<PermissionBranch, bool> GlobalUserPermissionUpdated
    {
        add => EventGlobalUserPermissionUpdated.Add(value);
        remove => EventGlobalUserPermissionUpdated.Remove(value);
    }
    public static event Action<PermissionGroup, bool> GlobalUserPermissionGroupUpdated
    {
        add => EventGlobalUserPermissionGroupUpdated.Add(value);
        remove => EventGlobalUserPermissionGroupUpdated.Remove(value);
    }
#endif


    [UsedImplicitly]
    private static readonly NetCallRaw<PermissionGroup> SendPermissionGroupLateRegistered = new NetCallRaw<PermissionGroup>(
        DevkitServerNetCall.SendPermissionGroupLateRegistered, PermissionGroup.ReadPermissionGroup, PermissionGroup.WritePermissionGroup);

    [UsedImplicitly]
    private static readonly NetCallRaw<PermissionGroup> SendPermissionGroupUpdate = new NetCallRaw<PermissionGroup>(
        DevkitServerNetCall.SendPermissionGroupUpdate, PermissionGroup.ReadPermissionGroup, PermissionGroup.WritePermissionGroup);

    [UsedImplicitly]
    private static readonly NetCall<string> SendPermissionGroupDeregistered = new NetCall<string>(DevkitServerNetCall.SendPermissionGroupDeregistered);

    [UsedImplicitly]
    internal static readonly NetCallRaw<PermissionBranch, bool> SendPermissionState = new NetCallRaw<PermissionBranch, bool>(DevkitServerNetCall.SendPermissionState,
        PermissionBranch.Read, null, PermissionBranch.Write, null);

    [UsedImplicitly]
    internal static readonly NetCallRaw<PermissionGroup, bool> SendPermissionGroupState = new NetCallRaw<PermissionGroup, bool>(DevkitServerNetCall.SendPermissionGroupState,
        PermissionGroup.ReadPermissionGroup, null, PermissionGroup.WritePermissionGroup, null);

    [UsedImplicitly]
    internal static readonly NetCall SendClearPermissions = new NetCall(DevkitServerNetCall.SendClearPermissions);

    [UsedImplicitly]
    internal static readonly NetCall SendClearPermissionGroups = new NetCall(DevkitServerNetCall.SendClearPermissionGroups);


    private static IPermissionHandler _permissions = new DevkitServerPermissions();
    private static IUserPermissionHandler _userPermissions = (IUserPermissionHandler)_permissions;

    public static IPermissionHandler Permissions
    {
        get => _permissions;
        set
        {
            ThreadUtil.assertIsGameThread();

            if (value == null)
                throw new ArgumentNullException(nameof(value));

            PermissionLockingSemaphore.Wait();
            try
            {
                IPermissionHandler old = Interlocked.Exchange(ref _permissions, value);
                IUserPermissionHandler userHandler = _userPermissions;
                int inited = Interlocked.Exchange(ref _inited, 1);

                if (!ReferenceEquals(value, userHandler))
                {
                    try
                    {
                        value.Init();
                    }
                    catch (Exception ex)
                    {
                        Logger.DevkitServer.LogError("SetPermissionHandler", ex, $"Error initializing permission provider: {value.GetType().Format()}.");
                        if (Interlocked.CompareExchange(ref _permissions, old, value) != value)
                            return;

                        if (value is IDisposable disp)
                        {
                            try
                            {
                                disp.Dispose();
                            }
                            catch (Exception ex2)
                            {
                                Logger.DevkitServer.LogError("SetPermissionHandler", ex2, $"Error disposing permission provider after initialization failure: {value.GetType().Format()}.");
                            }
                        }

                        Logger.DevkitServer.LogInfo("SetPermissionHandler", $"Rolled back to old permission provider: {old.Format()}");
                        return;
                    }
                }
                
                if (!ReferenceEquals(old, userHandler) && inited != 0)
                {
                    if (old is IDisposable disp)
                    {
                        try
                        {
                            disp.Dispose();
                        }
                        catch (Exception ex)
                        {
                            Logger.DevkitServer.LogError("SetPermissionHandler", ex, $"Error disposing permission provider: {old.GetType().Format()}.");
                        }
                    }

                    old.PermissionGroupDeregistered -= EventGlobalPermissionGroupDeregistered.TryInvoke;
                    old.PermissionGroupRegistered -= EventGlobalPermissionGroupRegistered.TryInvoke;
                    old.PermissionGroupUpdated -= EventGlobalPermissionGroupUpdated.TryInvoke;
                }

                value.PermissionGroupDeregistered += EventGlobalPermissionGroupDeregistered.TryInvoke;
                value.PermissionGroupRegistered += EventGlobalPermissionGroupRegistered.TryInvoke;
                value.PermissionGroupUpdated += EventGlobalPermissionGroupUpdated.TryInvoke;
            }
            finally
            {
                PermissionLockingSemaphore.Release();
            }
        }
    }
    public static IUserPermissionHandler UserPermissions
    {
        get => _userPermissions;
        set
        {
            ThreadUtil.assertIsGameThread();

            if (value == null)
                throw new ArgumentNullException(nameof(value));

            PermissionLockingSemaphore.Wait();
            try
            {
                IUserPermissionHandler old = Interlocked.Exchange(ref _userPermissions, value);
                IPermissionHandler permHandler = _permissions;
                int inited = Interlocked.Exchange(ref _inited, 1);

                if (!ReferenceEquals(value, permHandler))
                {
                    try
                    {
                        value.Init();
                    }
                    catch (Exception ex)
                    {
                        Logger.DevkitServer.LogError("SetUserPermissionsHandler", ex, $"Error initializing user permission provider: {value.GetType().Format()}.");
                        if (Interlocked.CompareExchange(ref _userPermissions, old, value) != value)
                            return;
                        if (value is IDisposable disp)
                        {
                            try
                            {
                                disp.Dispose();
                            }
                            catch (Exception ex2)
                            {
                                Logger.DevkitServer.LogError("SetUserPermissionsHandler", ex2, $"Error disposing user permission provider after initialization failure: {value.GetType().Format()}.");
                            }
                        }

                        Logger.DevkitServer.LogInfo("SetUserPermissionsHandler", $"Rolled back to old user permission provider: {old.Format()}");
                        return;
                    }
                }

                if (!ReferenceEquals(old, permHandler) && inited != 0)
                {
                    if (old is IDisposable disp)
                    {
                        try
                        {
                            disp.Dispose();
                        }
                        catch (Exception ex)
                        {
                            Logger.DevkitServer.LogError("SetUserPermissionsHandler", ex, $"Error disposing user permission provider: {old.GetType().Format()}.");
                        }
                    }

                    old.UserPermissionGroupUpdated -= EventGlobalUserPermissionGroupUpdated.TryInvoke;
                    old.UserPermissionUpdated -= EventGlobalUserPermissionUpdated.TryInvoke;
                }

                value.UserPermissionGroupUpdated += EventGlobalUserPermissionGroupUpdated.TryInvoke;
                value.UserPermissionUpdated += EventGlobalUserPermissionUpdated.TryInvoke;
            }
            finally
            {
                PermissionLockingSemaphore.Release();
            }
        }
    }

    internal static void InitHandlers()
    {
        Logger.DevkitServer.LogDebug("Permissions", "Loading permissions...");
        if (Interlocked.Exchange(ref _inited, 1) == 0)
        {
            IPermissionHandler handler = _permissions;
            IUserPermissionHandler userHandler = _userPermissions;
            handler.Init();
            if (!ReferenceEquals(userHandler, handler))
                userHandler.Init();
        }

        Logger.DevkitServer.LogInfo("Permissions", $"Found: {_permissions.PermissionGroups.Count.Format()} registered permission group(s).");
    }

    public static bool Has(this PermissionLeaf leaf
#if SERVER
        , ulong user
#endif
        , bool checkForSuperuser = true)
    {
#if CLIENT
        ClientInfo? info = ClientInfo.Info;
        Player? pl = Player.player;
        if (checkForSuperuser && info is { ServerTreatsAdminsAsSuperuser: true } && pl != null && pl.channel.owner.isAdmin)
            return true;
#elif SERVER
        if (checkForSuperuser && DevkitServerConfig.Config.AdminsAreSuperusers)
        {
            for (int i = 0; i < SteamAdminlist.list.Count; ++i)
            {
                if (SteamAdminlist.list[i].playerID.m_SteamID == user)
                    return true;
            }
        }
#endif

        bool v = leaf.Valid;

        if (!v && !checkForSuperuser)
            return false;

#if SERVER
        IReadOnlyList<PermissionBranch> branches = UserPermissions.GetPermissions(user);
#else
        IReadOnlyList<PermissionBranch> branches = UserPermissions.Permissions;
#endif
        bool hasRemovedSuperuser = false;
        bool hasSubbed = false, hasAdded = false;
        for (int i = branches.Count - 1; i >= 0; i--)
        {
            PermissionBranch branch = branches[i];
            if (checkForSuperuser && !hasRemovedSuperuser && branch.IsSuperuser)
            {
                if (branch.Mode == PermissionMode.Subtractive)
                    hasRemovedSuperuser = true;
                else return true;
            }

            if (v && branch.Contains(leaf))
            {
                if (branch.Mode == PermissionMode.Subtractive)
                    hasSubbed = true;
                else if (branch.Mode == PermissionMode.Additive)
                {
                    hasAdded = true;
                    hasSubbed = false;
                }
            }
        }

        if (hasAdded && !hasSubbed)
            return true;
        if (hasSubbed)
            return false;

#if SERVER
        IReadOnlyList<PermissionGroup> groups = UserPermissions.GetPermissionGroups(user);
#else
        IReadOnlyList<PermissionGroup> groups = UserPermissions.PermissionGroups;
#endif

        foreach (PermissionGroup group in groups)
        {
            for (int i = group.Permissions.Count - 1; i >= 0; i--)
            {
                PermissionBranch branch = group.Permissions[i];
                if (checkForSuperuser && !hasRemovedSuperuser && branch.IsSuperuser)
                {
                    if (branch.Mode == PermissionMode.Subtractive)
                        hasRemovedSuperuser = true;
                    else return true;
                }

                if (v && branch.Contains(leaf))
                {
                    if (branch.Mode == PermissionMode.Subtractive)
                        hasSubbed = true;
                    else if (branch.Mode == PermissionMode.Additive)
                    {
                        hasAdded = true;
                        hasSubbed = false;
                    }
                }
            }

            if (hasAdded && !hasSubbed)
                return true;
            if (hasSubbed)
                return false;
        }

        return false;
    }
#if SERVER
    /// <summary>
    /// Chceks against a list of required permissions to see if the client passes the check.
    /// </summary>
    /// <param name="any">When <see langword="true"/>, the check looks for just one permission available, otherwise it looks for all parameters available.</param>
    /// <param name="superuser">When <see langword="true"/>, the check includes a check for <see cref="Permission.SuperuserPermission"/> (which gives all permissions).</param>
    public static bool CheckPermission(this SteamPlayer? user, IEnumerable<PermissionLeaf> permissions, bool any, bool superuser = true)
    {
        if (user == null)
            return true;

        ulong s64 = user.playerID.steamID.m_SteamID;

        if (superuser && default(PermissionLeaf).Has(s64, true))
            return true;

        if (user.isAdmin && DevkitServerConfig.Config.AdminsAreSuperusers)
            return true;

        bool anyMissed = false, anyHit = false;
        foreach (PermissionLeaf p in permissions)
        {
            if (p.Has(s64))
                anyHit = true;
            else
                anyMissed = true;
        }

        // if no permissions were provided or the right amount were found
        return !(anyHit || anyMissed) || (any ? anyHit : !anyMissed);
    }
#endif
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

#if SERVER
    /// <summary>
    /// Tell all clients about a registered <see cref="PermissionGroup"/>.
    /// </summary>
    /// <remarks>You shouldn't ever need to call this unless you're creating your own implementation of <see cref="IPermissionHandler"/>.</remarks>
    public static void ReplicateLatePermissionGroupRegistration(PermissionGroup group)
    {
        if (Provider.clients.Count > 0)
            SendPermissionGroupLateRegistered.Invoke(Provider.GatherRemoteClientConnections(), group);
    }
    /// <summary>
    /// Tell all clients about a deregistered <see cref="PermissionGroup"/>.
    /// </summary>
    /// <remarks>You shouldn't ever need to call this unless you're creating your own implementation of <see cref="IPermissionHandler"/>.</remarks>
    public static void ReplicatePermissionGroupDeregistration(PermissionGroup permissionGroup)
    {
        if (Provider.clients.Count > 0)
            SendPermissionGroupDeregistered.Invoke(Provider.GatherRemoteClientConnections(), permissionGroup.Id);
    }
    /// <summary>
    /// Tell all clients about an updated <see cref="PermissionGroup"/>.
    /// </summary>
    /// <remarks>You shouldn't ever need to call this unless you're creating your own implementation of <see cref="IPermissionHandler"/>.</remarks>
    public static void ReplicatePermissionGroupUpdate(PermissionGroup permissionGroup)
    {
        if (Provider.clients.Count > 0)
            SendPermissionGroupUpdate.Invoke(Provider.GatherRemoteClientConnections(), permissionGroup);
    }
#endif

#if CLIENT
    [NetCall(NetCallSource.FromServer, DevkitServerNetCall.SendPermissionGroupLateRegistered)]
    [UsedImplicitly]
    private static void ReceivePermissionGroupLateRegistered(MessageContext ctx, PermissionGroup group)
    {
        Permissions.Register(group);
        ctx.Acknowledge(StandardErrorCode.Success);
    }

    [NetCall(NetCallSource.FromServer, DevkitServerNetCall.SendPermissionGroupDeregistered)]
    [UsedImplicitly]
    private static void ReceivePermissionGroupLateRegistered(MessageContext ctx, string groupId)
    {
        foreach (PermissionGroup group in Permissions.PermissionGroups)
        {
            if (group.Id.Equals(groupId, StringComparison.InvariantCultureIgnoreCase))
            {
                ctx.Acknowledge(Permissions.Deregister(group) ? StandardErrorCode.Success : StandardErrorCode.NotFound);
                break;
            }
        }

        ctx.Acknowledge(StandardErrorCode.NotFound);
    }
    [NetCall(NetCallSource.FromServer, DevkitServerNetCall.SendPermissionGroupUpdate)]
    [UsedImplicitly]
    private static void ReceivePermissionGroupUpdate(MessageContext ctx, PermissionGroup group)
    {
        ctx.Acknowledge(Permissions.ReceivePermissionGroupUpdate(group) ? StandardErrorCode.Success : StandardErrorCode.NotFound);
    }
#endif
}
