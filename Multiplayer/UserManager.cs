using DevkitServer.Players;

namespace DevkitServer.Multiplayer;
[EarlyTypeInit]
public static class UserManager
{
    public static event Action<EditorUser>? OnUserConnected;
    public static event Action<EditorUser>? OnUserDisconnected;
    private static readonly List<EditorUser> _users = new List<EditorUser>(16);
    public static IReadOnlyList<EditorUser> Users { get; } = _users.AsReadOnly();
    public static EditorUser? FromId(ulong id)
    {
        int min = 0;
        int max = _users.Count - 1;
        while (min <= max)
        {
            int index = min + (max - min >> 1);
            int comparison = _users[index].SteamId.m_SteamID.CompareTo(id);
            if (comparison == 0)
                return _users[index];
            if (comparison < 0)
                min = index + 1;
            else
                max = index - 1;
        }
        for (int i = 0; i < _users.Count; ++i)
            if (id == _users[i].SteamId.m_SteamID)
                return _users[i];
        return null;
    }
#if SERVER
    public static EditorUser? FromConnection(ITransportConnection connection)
    {
        if (connection == null) return null;

        for (int i = 0; i < _users.Count; ++i)
        {
            if (connection.Equals(_users[i].Connection))
                return _users[i];
        }

        return null;
    }
#endif
    public static EditorUser? FromId(CSteamID id) => FromId(id.m_SteamID);
    public static EditorUser? FromSteamPlayer(SteamPlayer player) => player == null ? null : FromId(player.playerID.steamID.m_SteamID);
    public static EditorUser? FromPlayer(Player player) => player == null ? null : FromId(player.channel.owner.playerID.steamID.m_SteamID);
    internal static void AddPlayer(CSteamID player)
    {
        for (int i = 0; i < Provider.clients.Count; ++i)
        {
            SteamPlayer pl = Provider.clients[i];
            if (pl.playerID.steamID.m_SteamID == player.m_SteamID)
            {
                if (pl.player.gameObject.TryGetComponent(out EditorUser user))
                {
                    user.IsOnline = true;
                    if (!_users.Contains(user))
                    {
                        bool added = false;
                        for (int j = 0; j < _users.Count; ++j)
                        {
                            EditorUser u = _users[j];
                            if (u.SteamId.m_SteamID > player.m_SteamID)
                            {
                                _users.Insert(j, user);
                                added = true;
                                break;
                            }
                            if (u == user)
                            {
                                added = true;
                                break;
                            }
                        }
                        if (!added) _users.Add(user);
                    }

                    user.Player = pl;
                    user.IsOnline = true;
                    _users.Add(user);
                    OnUserConnected?.Invoke(user);
                    Logger.LogInfo("Player added: " + user.DisplayName + " {" + user.SteamId.m_SteamID + "}.");
                    return;
                }
            }
        }

        Provider.kick(player, "Player not properly set up.");
    }

    internal static void RemovePlayer(CSteamID player)
    {
        EditorUser? user = FromId(player);
        if (user == null)
            return;

        _users.Remove(user);
        OnUserDisconnected?.Invoke(user);
        user.IsOnline = false;
        user.Player = null;
        Logger.LogInfo("Player removed: " + user.DisplayName + " {" + user.SteamId.m_SteamID + "}.");
        Object.Destroy(user);
    }
}