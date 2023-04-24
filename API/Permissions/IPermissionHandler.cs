using DevkitServer.Players;

namespace DevkitServer.API.Permissions;
public interface IPermissionHandler
{
    void Init();
    IReadOnlyList<Permission> Permissions { get; }
    void Reload();
    void LateRegister(Permission permission);
}

public interface IPlayerPermissionHandler
{
    void Init();
#if SERVER
    event Action<Permission, ulong, bool>? PermissionUpdated;
    void Reload();
    void AddPermission(ulong player, Permission permission);
    void RemovePermission(ulong player, Permission permission);
    void ClearPermissions(ulong player);
    IReadOnlyList<Permission> GetPermissions(ulong player, bool forceReload = false);
#else
    event Action<Permission, bool>? PermissionUpdated;
    IReadOnlyList<Permission> Permissions { get; }
    void ReceivePermissions(IReadOnlyList<Permission> permissions);
    void ReceivePermissionState(Permission permission, bool state);
    void ReceiveClearPermissions();
#endif
}

public static class PermissionsEx
{
    public static bool HasPermission(this IPlayerPermissionHandler handler,
#if SERVER
        ulong player,
#endif
        Permission permission)
    {
#if SERVER
        IReadOnlyList<Permission> list = handler.GetPermissions(player);
#else
        IReadOnlyList<Permission> list = handler.Permissions;
#endif
        foreach (Permission p in list)
        {
            if (p.Equals(permission))
                return true;
        }

        return false;
    }
#if CLIENT
    public static bool ClientHasPermissionToRun(this Command command)
    {
        return false;
    }
#endif
#if SERVER
    public static bool CheckPermission(this EditorUser? user, IEnumerable<Permission> permissions, bool any)
    {
        if (user == null)
            return true;

        bool anyMissed = false, anyHit = false;
        foreach (Permission p in permissions)
        {
            if (UserPermissions.PlayerHandler.HasPermission(user.SteamId.m_SteamID, p))
                anyHit = true;
            else
                anyMissed = true;
        }

        return any ? anyHit : !anyMissed;
    }
    public static void SetPermission(this IPlayerPermissionHandler handler, ulong player, Permission permission, bool hasPermission)
    {
        if (hasPermission)
            handler.AddPermission(player, permission);
        else
            handler.RemovePermission(player, permission);
    }
#endif
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
    public static bool TryFindPermission(this IPermissionHandler handler, IDevkitServerPlugin plugin, string id, out Permission permission)
    {
        permission = FindPermission(handler, plugin, id)!;
        return permission != null;
    }
    public static bool TryFindCorePermission(this IPermissionHandler handler, string id, out Permission permission)
    {
        permission = FindCorePermission(handler, id)!;
        return permission != null;
    }
    public static bool TryFindDevkitServerPermission(this IPermissionHandler handler, string id, out Permission permission)
    {
        permission = FindDevkitServerPermission(handler, id)!;
        return permission != null;
    }
}