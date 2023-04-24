using DevkitServer.Multiplayer.Networking;
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
#if SERVER
                    user.Connection = Provider.findTransportConnection(player);
#endif
                    _users.Add(user);
                    OnUserConnected?.Invoke(user);
#if SERVER
                    Logger.LogInfo("Player added: " + user.DisplayName.Format() + " {" + user.SteamId.m_SteamID.Format() + "} @ " + user.Connection.Format() + ".");
#else
                    Logger.LogInfo("Player added: " + user.DisplayName.Format() + " {" + user.SteamId.m_SteamID.Format() + "} @ " + (user.Connection != null ? "Current Session" : "Remote Session") + ".");
#endif
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
#if CLIENT
        if (player.m_SteamID == Provider.client.m_SteamID)
            HighSpeedConnection.Instance?.Dispose();
#endif
        _users.Remove(user);
        OnUserDisconnected?.Invoke(user);
        user.IsOnline = false;
        user.Player = null;
        Logger.LogInfo("Player removed: " + user.DisplayName + " {" + user.SteamId.m_SteamID + "}.");
        Object.Destroy(user);
    }

    public static EditorUser? FromName(string name, bool includeContains = false) => FromName(name, includeContains, Users);
    public static EditorUser? FromName(string name, bool includeContains, IEnumerable<EditorUser> selection)
    {
        if (name == null) return null;
        EditorUser? player = selection.Where(s => s.Player != null).FirstOrDefault(
            s =>
            s.Player!.playerID.characterName.Equals(name, StringComparison.InvariantCultureIgnoreCase) ||
            s.Player!.playerID.nickName.Equals(name, StringComparison.InvariantCultureIgnoreCase) ||
            s.Player!.playerID.playerName.Equals(name, StringComparison.InvariantCultureIgnoreCase)
            );
        if (includeContains && player == null)
        {
            player = selection.Where(s => s.Player != null).FirstOrDefault(s =>
                s.Player!.playerID.characterName.IndexOf(name, StringComparison.InvariantCultureIgnoreCase) != -1 ||
                s.Player!.playerID.nickName.IndexOf(name, StringComparison.InvariantCultureIgnoreCase) != -1 ||
                s.Player!.playerID.playerName.IndexOf(name, StringComparison.InvariantCultureIgnoreCase) != -1);
        }
        return player;
    }
    /// <summary>Slow, use rarely.</summary>
    public static EditorUser? FromName(string name, NameSearchType type)
    {
        if (type == NameSearchType.CharacterName)
        {
            foreach (EditorUser current in Users.Where(NotNull).OrderBy(SelectCharacterName))
            {
                if (current.Player!.playerID.characterName.IndexOf(name, StringComparison.InvariantCultureIgnoreCase) != -1)
                    return current;
            }
            foreach (EditorUser current in Users.Where(NotNull).OrderBy(SelectNickName))
            {
                if (current.Player!.playerID.nickName.IndexOf(name, StringComparison.InvariantCultureIgnoreCase) != -1)
                    return current;
            }
            foreach (EditorUser current in Users.Where(NotNull).OrderBy(SelectPlayerName))
            {
                if (current.Player!.playerID.playerName.IndexOf(name, StringComparison.InvariantCultureIgnoreCase) != -1)
                    return current;
            }
            return null;
        }
        else if (type == NameSearchType.NickName)
        {
            foreach (EditorUser current in Users.Where(NotNull).OrderBy(SelectNickName))
            {
                if (current.Player!.playerID.nickName.IndexOf(name, StringComparison.InvariantCultureIgnoreCase) != -1)
                    return current;
            }
            foreach (EditorUser current in Users.Where(NotNull).OrderBy(SelectCharacterName))
            {
                if (current.Player!.playerID.characterName.IndexOf(name, StringComparison.InvariantCultureIgnoreCase) != -1)
                    return current;
            }
            foreach (EditorUser current in Users.Where(NotNull).OrderBy(SelectPlayerName))
            {
                if (current.Player!.playerID.playerName.IndexOf(name, StringComparison.InvariantCultureIgnoreCase) != -1)
                    return current;
            }
            return null;
        }
        else if (type == NameSearchType.PlayerName)
        {
            foreach (EditorUser current in Users.Where(NotNull).OrderBy(SelectPlayerName))
            {
                if (current.Player!.playerID.playerName.IndexOf(name, StringComparison.InvariantCultureIgnoreCase) != -1)
                    return current;
            }
            foreach (EditorUser current in Users.Where(NotNull).OrderBy(SelectNickName))
            {
                if (current.Player!.playerID.nickName.IndexOf(name, StringComparison.InvariantCultureIgnoreCase) != -1)
                    return current;
            }
            foreach (EditorUser current in Users.Where(NotNull).OrderBy(SelectCharacterName))
            {
                if (current.Player!.playerID.characterName.IndexOf(name, StringComparison.InvariantCultureIgnoreCase) != -1)
                    return current;
            }
            return null;
        }
        else return FromName(name, NameSearchType.CharacterName);

        bool NotNull(EditorUser u) => u.Player != null;
        string SelectPlayerName(EditorUser u) => u.Player!.playerID.playerName;
        string SelectNickName(EditorUser u) => u.Player!.playerID.nickName;
        string SelectCharacterName(EditorUser u) => u.Player!.playerID.characterName;
    }
}
public enum NameSearchType : byte
{
    CharacterName,
    NickName,
    PlayerName
}