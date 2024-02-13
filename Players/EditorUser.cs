using System.Globalization;
using DevkitServer.API.Permissions;
using DevkitServer.Multiplayer;
using DevkitServer.Multiplayer.Actions;
using DevkitServer.Multiplayer.Movement;
using DevkitServer.Multiplayer.Sync;
#if CLIENT
using DevkitServer.Configuration;
using DevkitServer.Multiplayer.Networking;
#endif

namespace DevkitServer.Players;
/// <summary>
/// Represents a connected user. This component is added to the Player, not the editor.
/// </summary>
public class EditorUser : MonoBehaviour, IComparable<EditorUser>
{
    internal readonly List<AuthoritativeSync> IntlSyncs = new List<AuthoritativeSync>(4);
#if CLIENT
    public static EditorUser? User { get; internal set; }
#endif
    public CSteamID SteamId { get; private set; }
#if SERVER
    private List<PermissionBranch> _perms = null!;
    private List<PermissionGroup> _permGrps = null!;
    public IReadOnlyList<PermissionBranch> Permissions { get; private set; } = null!;
    public IReadOnlyList<PermissionGroup> PermissionGroups { get; private set; } = null!;
    public ITransportConnection Connection { get; internal set; } = null!;
#else
    public IClientTransport? Connection { get; internal set; }
#endif
    public UserInput Input { get; private set; } = null!;
    public UserMovement? Movement { get; private set; }
    public UserTransactions Transactions { get; private set; } = null!;
    public EditorActions Actions { get; private set; } = null!;
    public TileSync TileSync { get; private set; } = null!;
    public ObjectSync ObjectSync { get; private set; } = null!;
    public HierarchySync HierarchySync { get; private set; } = null!;
    public NavigationSync NavigationSync { get; private set; } = null!;
    public RoadSync RoadSync { get; private set; } = null!;
    public string DisplayName { get; private set; } = null!;
    public SteamPlayer? Player { get; internal set; }
    public IReadOnlyList<AuthoritativeSync> Syncs { get; }

    public bool IsOnline { get; internal set; }
    public bool IsOwner { get; private set; }
    public GameObject EditorObject { get; private set; } = null!;
    /// <summary>
    /// Position of the active controller.
    /// </summary>
    public Vector3 Position
    {
        get
        {
            Transform? aim = Input.Aim;
            return aim == null ? Vector3.zero : aim.transform.position;
        }
    }
    /// <summary>
    /// Rotation of the active controller.
    /// </summary>
    public Quaternion Rotation
    {
        get
        {
            Transform? aim = Input.Aim;
            return aim == null ? Quaternion.identity : aim.transform.rotation;
        }
    }
    /// <summary>
    /// Forward vector of the active controller.
    /// </summary>
    public Vector3 Forward
    {
        get
        {
            Transform? aim = Input.Aim;
            return aim == null ? Vector3.forward : aim.transform.forward;
        }
    }

    /// <summary>
    /// Active controller camera look object (like player.look.aim).
    /// </summary>
    public Transform? ControllerLook => Input.Aim;
    private EditorUser()
    {
        Syncs = IntlSyncs.AsReadOnly();
    }
    internal void PreInit(CSteamID player, string displayName)
    {
        SteamId = player;
        DisplayName = displayName;
        Logger.DevkitServer.LogDebug("USERS", "Editor User created: " + SteamId.m_SteamID.Format() + " (" + displayName.Format() + ").");
    }
    internal void Init()
    {
#if CLIENT
        IsOwner = this == User;
#else
        IsOwner = false;
#endif
        EditorObject = IsOwner ? Editor.editor.gameObject : new GameObject("Editor {" + SteamId.m_SteamID.ToString(CultureInfo.InvariantCulture) + "}");
        DevkitServerGamemode.SetupEditorObject(EditorObject, this);
        Input = EditorObject.GetComponent<UserInput>();
        Movement = EditorObject.GetComponent<UserMovement>();
        Transactions = EditorObject.GetComponent<UserTransactions>();
        Actions = EditorObject.GetComponent<EditorActions>();
        TileSync = EditorObject.GetComponent<TileSync>();
        ObjectSync = EditorObject.GetComponent<ObjectSync>();
        HierarchySync = EditorObject.GetComponent<HierarchySync>();
        NavigationSync = EditorObject.GetComponent<NavigationSync>();
        RoadSync = EditorObject.GetComponent<RoadSync>();
        IntlSyncs.Add(TileSync);
        IntlSyncs.Add(ObjectSync);
        IntlSyncs.Add(HierarchySync);
        IntlSyncs.Add(NavigationSync);
        IntlSyncs.Add(RoadSync);
#if CLIENT
        StartCoroutine(DeactivateAfterFrame());
#else
        Player!.player.gameObject.SetActive(false);
#endif
#if SERVER
        _perms = PermissionManager.UserPermissions.GetPermissions(SteamId.m_SteamID, true)?.ToList() ?? [];
        _permGrps = PermissionManager.UserPermissions.GetPermissionGroups(SteamId.m_SteamID, true)?.ToList() ?? [];
        Permissions = _perms.AsReadOnly();
        PermissionGroups = _permGrps.AsReadOnly();
        TileSync.SendAuthority(Connection);
        ObjectSync.SendAuthority(Connection);
        HierarchySync.SendAuthority(Connection);
        NavigationSync.SendAuthority(Connection);
        RoadSync.SendAuthority(Connection);
#endif
        Logger.DevkitServer.LogDebug("USERS", "Editor User initialized: " + SteamId.m_SteamID.Format() + " (" + DisplayName.Format() + ").");
    }
#if CLIENT
    private IEnumerator DeactivateAfterFrame()
    {
        yield return new WaitForEndOfFrame();
        Player!.player.gameObject.SetActive(false);
        Logger.DevkitServer.LogDebug("USERS", "Player deactivated.");
    }
#endif
#if SERVER
    internal void AddPermission(PermissionBranch permission)
    {
        ThreadUtil.assertIsGameThread();

        for (int i = 0; i < _perms.Count; ++i)
        {
            if (_perms[i].Equals(permission))
                return;
        }

        PermissionManager.SendPermissionState.Invoke(Connection, permission, true);
        _perms.Add(permission);
    }

    internal void RemovePermission(PermissionBranch permission)
    {
        ThreadUtil.assertIsGameThread();

        for (int i = _perms.Count - 1; i >= 0; --i)
        {
            if (_perms[i].Equals(permission))
            {
                PermissionManager.SendPermissionState.Invoke(Connection, permission, false);
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

        PermissionManager.SendPermissionGroupState.Invoke(Connection, group, true);
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
                PermissionManager.SendPermissionGroupState.Invoke(Connection, group, false);
                _permGrps.RemoveAt(i);
                break;
            }
        }
    }

    internal void ClearPermissionGroups()
    {
        ThreadUtil.assertIsGameThread();

        _permGrps.Clear();
        PermissionManager.SendClearPermissionGroups.Invoke(Connection);
    }
    internal void ClearPermissions()
    {
        ThreadUtil.assertIsGameThread();

        _perms.Clear();
        PermissionManager.SendClearPermissions.Invoke(Connection);
    }
#endif

#if CLIENT
    [NetCall(NetCallSource.FromServer, DevkitServerNetCall.SendPermissionState)]
    private static void ReceivePermissionState(MessageContext ctx, PermissionBranch perm, bool state)
    {
        PermissionManager.UserPermissions.ReceivePermissionState(perm, state);
        ctx.Acknowledge();
    }
    [NetCall(NetCallSource.FromServer, DevkitServerNetCall.SendClearPermissions)]
    private static void ReceiveClearPermissions(MessageContext ctx)
    {
        PermissionManager.UserPermissions.ReceiveClearPermissions();
        ctx.Acknowledge();
    }
    [NetCall(NetCallSource.FromServer, DevkitServerNetCall.SendPermissionGroupState)]
    private static void ReceivePermissionGroupState(MessageContext ctx, PermissionGroup group, bool state)
    {
        PermissionManager.UserPermissions.ReceivePermissionGroupState(group, state);
        ctx.Acknowledge();
    }
    [NetCall(NetCallSource.FromServer, DevkitServerNetCall.SendClearPermissionGroups)]
    private static void ReceiveClearPermissionGroups(MessageContext ctx)
    {
        PermissionManager.UserPermissions.ReceiveClearPermissions();
        ctx.Acknowledge();
    }
#endif

    [UsedImplicitly]
    private void OnDestroy()
    {
        Player = null;
        IsOnline = false;
        if (!IsOwner && EditorObject != null) Destroy(EditorObject);
        Logger.DevkitServer.LogDebug("USERS", $"Editor User destroyed: {SteamId.Format()}.");
    }
#if CLIENT
    internal static void OnClientConnected()
    {
        DevkitServerConfig.SeverFolderIntl = null;
        FileUtil.CheckDirectory(false, DevkitServerConfig.ServerFolder);
        if (!DevkitServerModule.IsEditing)
            return;
        Commander.init();
        if (!SDG.Unturned.Player.player.TryGetComponent(out EditorUser user))
        {
            Logger.DevkitServer.LogWarning("USERS", "Unable to find Editor user in client-side player.");
            return;
        }
        Logger.DevkitServer.LogDebug("USERS", "Registered client-side editor user.");
        User = user;
        UserManager.EventOnConnectedToServer.TryInvoke();
    }
    internal static void OnEnemyConnected(SteamPlayer player)
    {
        UserManager.AddUser(player);
    }
    internal static void OnClientDisconnected()
    {
        // DevkitServerModule.Instance.UnloadBundle();
        TileSync.DestroyServersideAuthority();
        try
        {
            HighSpeedConnection.Instance?.CloseConnection();
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogWarning("USERS", ex, "Unable to close high-speed server!");
        }
        UserManager.Disconnect();

        if (User is not null)
        {
            if (User.isActiveAndEnabled)
                Destroy(User);

            User = null;
            Logger.DevkitServer.LogDebug("USERS", "Deregistered client-side editor user.");
        }
        if (DevkitServerModule.IsEditing)
        {
            DevkitServerModule.RegisterDisconnectFromEditingServer();
        }
        
        ClientInfo.OnDisconnect();
    }
    internal static void OnEnemyDisconnected(SteamPlayer player)
    {
        UserManager.RemoveUser(player.playerID.steamID);
    }
#endif
    public int CompareTo(EditorUser other) => SteamId.m_SteamID.CompareTo(other.SteamId.m_SteamID);

    public override string ToString()
    {
        string? pn = Player?.playerID.playerName;
        string? cn = Player?.playerID.characterName;
        string? nn = Player?.playerID.nickName;
        string s64 = SteamId.m_SteamID.ToString("D17");
        bool pws = string.IsNullOrWhiteSpace(pn);
        bool cws = string.IsNullOrWhiteSpace(cn);
        bool nws = string.IsNullOrWhiteSpace(nn);
        if (pws && cws && nws)
            return s64 + " (" + DisplayName + ")";
        if (pws)
        {
            if (cws)
                return s64 + " (" + nn + ")";
            if (nws || nn!.Equals(cn, StringComparison.Ordinal))
                return s64 + " (" + cn + ")";
            return s64 + " (" + cn + "|" + nn + ")";
        }
        if (cws)
        {
            if (pws)
                return s64 + " (" + nn + ")";
            if (nws || nn!.Equals(pn, StringComparison.Ordinal))
                return s64 + " (" + pn + ")";
            return s64 + " (" + pn + "|" + nn + ")";
        }
        if (nws)
        {
            if (pws)
                return s64 + " (" + cn + ")";
            if (cws || cn!.Equals(pn, StringComparison.Ordinal))
                return s64 + " (" + pn + ")";
            return s64 + " (" + pn + "|" + cn + ")";
        }

        bool nep = nn!.Equals(pn, StringComparison.Ordinal);
        bool nec = nn.Equals(cn, StringComparison.Ordinal);
        bool pec = nec && nep || pn!.Equals(cn, StringComparison.Ordinal);
        if (nep && nec)
            return s64 + " (" + nn + ")";
        if (pec || nec)
            return s64 + " (" + pn + "|" + nn + ")";
        if (nep)
            return s64 + " (" + pn + "|" + cn + ")";
        
        return s64 + " (" + pn + "|" + cn + "|" + nn + ")";
    }

    public static implicit operator ulong(EditorUser user) => user.SteamId.m_SteamID;
    public static implicit operator CSteamID(EditorUser user) => user.SteamId;
    public static explicit operator SteamPlayer?(EditorUser user) => user.Player;
    public static explicit operator Player?(EditorUser user) => user.Player?.player;
    public static explicit operator SteamPlayerID?(EditorUser user) => user.Player?.playerID;
}
