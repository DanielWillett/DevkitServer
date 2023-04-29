using System.Globalization;
using DevkitServer.API.Permissions;
using DevkitServer.Multiplayer;
#if CLIENT
using DevkitServer.Multiplayer.Networking;
using DevkitServer.Players.UI;
#endif
using JetBrains.Annotations;

namespace DevkitServer.Players;
public class EditorUser : MonoBehaviour, IComparable<EditorUser>
{
    public ClientInfo ClientInfo { get; private set; } = null!;
#if CLIENT
    public static EditorUser? User { get; internal set; }
#endif
    public CSteamID SteamId { get; private set; }
#if SERVER
    private List<Permission> _perms = null!;
    private List<PermissionGroup> _permGrps = null!;
    public IReadOnlyList<Permission> Permissions { get; private set; } = null!;
    public IReadOnlyList<PermissionGroup> PermissionGroups { get; private set; } = null!;
    public ITransportConnection Connection { get; internal set; } = null!;
#else
    public IClientTransport? Connection { get; internal set; }
#endif
    public UserInput Input { get; private set; } = null!;
    public EditorTerrain Terrain { get; private set; } = null!;
    public string DisplayName { get; private set; } = null!;
    public SteamPlayer? Player { get; internal set; }

    public bool IsOnline { get; internal set; }
    public bool IsOwner { get; private set; }
    public GameObject EditorObject { get; private set; } = null!;

    
    internal void PreInit(CSteamID player, string displayName)
    {
        SteamId = player;
        DisplayName = displayName;
        Logger.LogDebug("Editor User created: " + SteamId.m_SteamID.Format() + " (" + displayName.Format() + ").");
    }
    internal void Init()
    {
#if CLIENT
        IsOwner = this == User;
#endif
        EditorObject = IsOwner ? Editor.editor.gameObject : new GameObject("Editor {" + SteamId.m_SteamID.ToString(CultureInfo.InvariantCulture) + "}");
        DevkitServerGamemode.SetupEditorObject(EditorObject, this);
        Input = EditorObject.GetComponent<UserInput>();
        Terrain = EditorObject.GetComponent<EditorTerrain>();
#if SERVER
        ClientInfo = DevkitServerGamemode.GetClientInfo(this);
        _perms = ClientInfo.Permissions.ToList();
        _permGrps = ClientInfo.PermissionGroups.ToList();
        Permissions = _perms.AsReadOnly();
        PermissionGroups = _permGrps.AsReadOnly();
        ClientInfo.SendClientInfo.Invoke(Connection, ClientInfo);
#endif
        Logger.LogDebug("Editor User initialized: " + SteamId.m_SteamID.Format() + " (" + DisplayName.Format() + ").");
    }
#if SERVER
    internal void AddPermission(Permission permission)
    {
        ThreadUtil.assertIsGameThread();

        for (int i = 0; i < _perms.Count; ++i)
        {
            if (_perms[i].Equals(permission))
                return;
        }

        UserPermissions.SendPermissionState.Invoke(Connection, permission, true);
        _perms.Add(permission);
    }

    internal void RemovePermission(Permission permission)
    {
        ThreadUtil.assertIsGameThread();

        for (int i = _perms.Count - 1; i >= 0; --i)
        {
            if (_perms[i].Equals(permission))
            {
                UserPermissions.SendPermissionState.Invoke(Connection, permission, false);
                _perms.RemoveAt(i);
                break;
            }
        }
    }
    internal void AddPermissionGroup(PermissionGroup group)
    {
        ThreadUtil.assertIsGameThread();

        for (int i = 0; i < _permGrps.Count; ++i)
        {
            if (_permGrps[i].Equals(group))
                return;
        }

        UserPermissions.SendPermissionGroupState.Invoke(Connection, group, true);
        bool added = false;
        for (int i = 0; i < _permGrps.Count; ++i)
        {
            if (_permGrps[i].Priority <= group.Priority)
                continue;
            _permGrps.Insert(i, group);
            added = true;
            break;
        }
        if (!added)
            _permGrps.Add(group);
    }

    internal void RemovePermissionGroup(PermissionGroup group)
    {
        ThreadUtil.assertIsGameThread();

        for (int i = _permGrps.Count - 1; i >= 0; --i)
        {
            if (_permGrps[i].Equals(group))
            {
                UserPermissions.SendPermissionGroupState.Invoke(Connection, group, false);
                _permGrps.RemoveAt(i);
                break;
            }
        }
    }

    internal void ClearPermissionGroups()
    {
        ThreadUtil.assertIsGameThread();

        _permGrps.Clear();
        UserPermissions.SendClearPermissionGroups.Invoke(Connection);
    }
    internal void ClearPermissions()
    {
        ThreadUtil.assertIsGameThread();

        _perms.Clear();
        UserPermissions.SendClearPermissions.Invoke(Connection);
    }
#endif

#if CLIENT
    [NetCall(NetCallSource.FromServer, (ushort)NetCalls.SendPermissionState)]
    private static void ReceivePermissionState(MessageContext ctx, Permission perm, bool state)
    {
        UserPermissions.UserHandler.ReceivePermissionState(perm, state);
        ctx.Acknowledge();
    }
    [NetCall(NetCallSource.FromServer, (ushort)NetCalls.SendClearPermissions)]
    private static void ReceiveClearPermissions(MessageContext ctx)
    {
        UserPermissions.UserHandler.ReceiveClearPermissions();
        ctx.Acknowledge();
    }
    [NetCall(NetCallSource.FromServer, (ushort)NetCalls.SendPermissionGroupState)]
    private static void ReceivePermissionGroupState(MessageContext ctx, PermissionGroup group, bool state)
    {
        UserPermissions.UserHandler.ReceivePermissionGroupState(group, state);
        ctx.Acknowledge();
    }
    [NetCall(NetCallSource.FromServer, (ushort)NetCalls.SendClearPermissionGroups)]
    private static void ReceiveClearPermissionGroups(MessageContext ctx)
    {
        UserPermissions.UserHandler.ReceiveClearPermissions();
        ctx.Acknowledge();
    }
#endif

    [UsedImplicitly]
    private void OnDestroy()
    {
        Player = null;
        IsOnline = false;
        if (!IsOwner && EditorObject != null) Destroy(EditorObject);
        Logger.LogDebug("Editor User destroyed: " + SteamId.m_SteamID.ToString(CultureInfo.InvariantCulture) + ".");
    }
#if CLIENT
    internal static void OnClientConnected()
    {
        if (!DevkitServerModule.IsEditing)
            return;
        Commander.init();
        if (!SDG.Unturned.Player.player.TryGetComponent(out EditorUser user))
        {
            Logger.LogWarning("Unable to find Editor user in client-side player.");
            return;
        }
        Logger.LogDebug("Registered client-side editor user.");
        User = user;
    }
    internal static void OnEnemyConnected(SteamPlayer player)
    {
        UserManager.AddPlayer(player.playerID.steamID);
    }
    internal static void OnClientDisconnected()
    {
        DevkitServerModule.Instance.UnloadBundle();
        try
        {
            HighSpeedConnection.Instance?.CloseConnection();
        }
        catch (Exception ex)
        {
            Logger.LogWarning("Unable to close high-speed server!");
            Logger.LogError(ex);
        }
        UserManager.Disconnect();

        if (User != null)
        {
            if (User.isActiveAndEnabled)
                Destroy(User);

            User = null;
            Logger.LogDebug("Deregistered client-side editor user.");
            return;
        }
        if (DevkitServerModule.IsEditing)
        {
            Logger.LogWarning("Unable to find Editor user in client-side player.");
            DevkitServerModule.RegisterDisconnectFromEditingServer();
        }

        DevkitEditorHUD.Close(true);
        ClientInfo.OnDisconnect();
    }
    internal static void OnEnemyDisconnected(SteamPlayer player)
    {
        UserManager.RemovePlayer(player.playerID.steamID);
    }
#endif
    public int CompareTo(EditorUser other) => SteamId.m_SteamID.CompareTo(other.SteamId.m_SteamID);

    public override string ToString() => $"{{{SteamId.m_SteamID}}} ({Player?.playerID.playerName}/{Player?.playerID.characterName}/{Player?.playerID.nickName})";

    public static implicit operator ulong(EditorUser user) => user.SteamId.m_SteamID;
    public static implicit operator CSteamID(EditorUser user) => user.SteamId;
    public static explicit operator SteamPlayer?(EditorUser user) => user.Player;
    public static explicit operator Player?(EditorUser user) => user.Player?.player;
    public static explicit operator SteamPlayerID?(EditorUser user) => user.Player?.playerID;
}
