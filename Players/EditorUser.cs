using System.Globalization;
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
    public string DisplayName { get; private set; } = null!;
    public SteamPlayer? Player { get; internal set; }

    public bool IsOnline { get; internal set; }

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
        Logger.LogDebug("Editor User initialized: " + player.m_SteamID.ToString(CultureInfo.InvariantCulture) + " (" + displayName + ").");
    }

    [UsedImplicitly]
    private void OnDestroy()
    {
        Player = null;
        IsOnline = false;
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
    internal static void OnClientDisconnected()
    {
        if (User != null || User is not null)
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
    }
#endif
    public int CompareTo(EditorUser other) => SteamId.m_SteamID.CompareTo(other.SteamId.m_SteamID);
}
