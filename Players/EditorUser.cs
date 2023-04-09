using System.Globalization;
using DevkitServer.Multiplayer;
#if CLIENT
using DevkitServer.Players.UI;
#endif
using JetBrains.Annotations;

namespace DevkitServer.Players;
public class EditorUser : MonoBehaviour, IComparable<EditorUser>
{
#if CLIENT
    public static EditorUser? User { get; internal set; }
#endif
    public CSteamID SteamId { get; private set; }
#if SERVER
    public ITransportConnection Connection { get; private set; } = null!;
#else
    public IClientTransport Connection { get; private set; } = null!;
#endif
    public UserInput Input { get; private set; } = null!;
    public EditorTerrain Terrain { get; private set; } = null!;
    public string DisplayName { get; private set; } = null!;
    public SteamPlayer? Player { get; internal set; }

    public bool IsOnline { get; internal set; }
    public bool IsOwner { get; private set; }
    public GameObject EditorObject { get; private set; } = null!;

    internal void Init(CSteamID player,
#if SERVER
        ITransportConnection connection,
#else
        IClientTransport connection,
#endif
        string displayName)
    {
        SteamId = player;
        Connection = connection;
        DisplayName = displayName;
        if (IsOwner)
        {
            EditorObject = Editor.editor.gameObject;
            Logger.DumpGameObject(EditorObject);
        }
        else
            EditorObject = new GameObject("Editor {" + SteamId.m_SteamID.ToString(CultureInfo.InvariantCulture) + "}");
#if CLIENT
        IsOwner = this == User;
#endif
        DevkitServerGamemode.SetupEditorObject(EditorObject, this);
        Input = EditorObject.GetComponent<UserInput>();
        Terrain = EditorObject.GetComponent<EditorTerrain>();
        if (Input == null)
        {
            Logger.LogError("Invalid EditorUser setup; UserInput not found!");
            Provider.kick(player, "Invalid setup [1].");
            return;
        }
        if (Terrain == null)
        {
            Logger.LogError("Invalid EditorUser setup; EditorTerrain not found!");
            Provider.kick(player, "Invalid setup [2].");
            return;
        }

        Logger.LogDebug("Editor User initialized: " + SteamId.m_SteamID.ToString(CultureInfo.InvariantCulture) + " (" + displayName + ").");
    }

    [UsedImplicitly]
    private void OnDestroy()
    {
        Player = null;
        IsOnline = false;
        if (!IsOwner) Destroy(EditorObject);
        Logger.LogDebug("Editor User destroyed: " + SteamId.m_SteamID.ToString(CultureInfo.InvariantCulture) + ".");
    }
#if CLIENT
    internal static void OnClientConnected()
    {
        if (!DevkitServerModule.IsEditing)
            return;
        DevkitEditorHUD.Open();
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
