using System.Globalization;
using DevkitServer.API.Permissions;
using DevkitServer.Multiplayer;
#if CLIENT
using DevkitServer.Multiplayer.Networking;
using DevkitServer.Players.UI;
#endif
using JetBrains.Annotations;
using SDG.Framework.Debug;
using UnityEngine.PlayerLoop;

namespace DevkitServer.Players;
public class EditorUser : MonoBehaviour, IComparable<EditorUser>
{
#if CLIENT
    public static EditorUser? User { get; internal set; }
#endif
    public CSteamID SteamId { get; private set; }
#if SERVER
    private List<Permission> _perms = null!;
    public ITransportConnection Connection { get; internal set; } = null!;
    public IReadOnlyList<Permission> Permissions { get; private set; } = null!;
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


    internal void Init(CSteamID player, string displayName)
    {
        SteamId = player;
        DisplayName = displayName;
#if SERVER
        Connection = Provider.findTransportConnection(player);
#endif
#if CLIENT
        IsOwner = this == User;
#endif
        EditorObject = IsOwner ? Editor.editor.gameObject : new GameObject("Editor {" + SteamId.m_SteamID.ToString(CultureInfo.InvariantCulture) + "}");
        DevkitServerGamemode.SetupEditorObject(EditorObject, this);
        Input = EditorObject.GetComponent<UserInput>();
        Terrain = EditorObject.GetComponent<EditorTerrain>();
#if SERVER
        ClientInfo info = DevkitServerGamemode.GetClientInfo(this);
        ClientInfo.SendClientInfo.Invoke(Connection, info);
        _perms = info.Permissions.ToList();
        Permissions = _perms.AsReadOnly();
#endif
        Logger.LogDebug("Editor User initialized: " + SteamId.m_SteamID.ToString(CultureInfo.InvariantCulture) + " (" + displayName + ").");
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

        API.Permissions.UserPermissions.SendPermissionState.Invoke(Connection, permission, true);
        _perms.Add(permission);
    }

    internal void RemovePermission(Permission permission)
    {
        ThreadUtil.assertIsGameThread();

        for (int i = _perms.Count - 1; i >= 0; --i)
        {
            if (_perms[i].Equals(permission))
            {
                API.Permissions.UserPermissions.SendPermissionState.Invoke(Connection, permission, false);
                _perms.RemoveAt(i);
                break;
            }
        }
    }

    internal void ClearPermissions()
    {
        ThreadUtil.assertIsGameThread();

        _perms.Clear();
        API.Permissions.UserPermissions.SendClearPermissions.Invoke(Connection);
    }
#endif

#if CLIENT
    [NetCall(NetCallSource.FromServer, (ushort)NetCalls.SendPermissionState)]
    private static void ReceivePermissionState(MessageContext ctx, Permission perm, bool state)
    {
        UserPermissions.PlayerHandler.ReceivePermissionState(perm, state);
        ctx.Acknowledge();
    }
    [NetCall(NetCallSource.FromServer, (ushort)NetCalls.SendClearPermissions)]
    private static void ReceiveClearPermission(MessageContext ctx)
    {
        UserPermissions.PlayerHandler.ReceiveClearPermissions();
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

        if (User != null || User is not null)
        {
            if (User.isActiveAndEnabled)
                Destroy(User);

            DevkitEditorHUD.Close(true);
            User = null;
            Logger.LogDebug("Deregistered client-side editor user.");
            return;
        }
        if (DevkitServerModule.IsEditing)
        {
            Logger.LogWarning("Unable to find Editor user in client-side player.");
            DevkitServerModule.RegisterDisconnectFromEditingServer();
        }
    }
    internal static void OnEnemyDisconnected(SteamPlayer player)
    {
        UserManager.RemovePlayer(player.playerID.steamID);
    }
#endif
    public int CompareTo(EditorUser other) => SteamId.m_SteamID.CompareTo(other.SteamId.m_SteamID);
}
