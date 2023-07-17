using DevkitServer.Multiplayer.Levels;
using DevkitServer.Players;
#if SERVER
using DevkitServer.Levels;
#endif
#if CLIENT
using DevkitServer.Multiplayer.Networking;
#endif

namespace DevkitServer.Multiplayer;
[EarlyTypeInit]
public static class UserManager
{
    private static readonly CachedMulticastEvent<Action<EditorUser>> EventOnUserConnected = new CachedMulticastEvent<Action<EditorUser>>(typeof(UserManager), nameof(OnUserConnected));
    private static readonly CachedMulticastEvent<Action<EditorUser>> EventOnUserDisconnected = new CachedMulticastEvent<Action<EditorUser>>(typeof(UserManager), nameof(OnUserDisconnected));
    public static event Action<EditorUser> OnUserConnected
    {
        add => EventOnUserConnected.Add(value);
        remove => EventOnUserDisconnected.Remove(value);
    }
    public static event Action<EditorUser> OnUserDisconnected
    {
        add => EventOnUserDisconnected.Add(value);
        remove => EventOnUserDisconnected.Remove(value);
    }
    private static readonly List<EditorUser> UsersIntl = new List<EditorUser>(16);
    public static IReadOnlyList<EditorUser> Users { get; } = UsersIntl.AsReadOnly();
    public static EditorUser? FromId(ulong id)
    {
        lock (UsersIntl)
        {
            List<EditorUser> users = UsersIntl;

            int min = 0;
            int max = users.Count - 1;
            while (min <= max)
            {
                int index = min + (max - min) / 2;
                ulong s64 = users[index].SteamId.m_SteamID;
                if (s64 == id)
                    return users[index];
                if (s64 < id)
                    min = index + 1;
                else
                    max = index - 1;
            }

            return null;
        }
    }
    
    public static void ForEach(Action<EditorUser> action)
    {
        lock (UsersIntl)
            UsersIntl.ForEach(action);
    }
#if SERVER
    public static EditorUser? FromConnection(ITransportConnection connection)
    {
        if (connection == null) return null;

        lock (UsersIntl)
        {
            List<EditorUser> users = UsersIntl;
            for (int i = 0; i < users.Count; ++i)
            {
                if (connection.Equals(users[i].Connection))
                    return users[i];
            }

            return null;
        }
    }
#endif
    public static EditorUser? FromId(CSteamID id) => FromId(id.m_SteamID);
    public static EditorUser? FromSteamPlayer(SteamPlayer player) => player == null ? null : FromId(player.playerID.steamID.m_SteamID);
    public static EditorUser? FromPlayer(Player player) => player == null ? null : FromId(player.channel.owner.playerID.steamID.m_SteamID);
    internal static void AddUser(CSteamID player)
    {
        lock (UsersIntl)
        {
#if SERVER
            BackupManager.PlayerHasJoinedSinceLastBackup = true;
#endif
            for (int i = 0; i < Provider.clients.Count; ++i)
            {
                SteamPlayer pl = Provider.clients[i];
                if (pl.playerID.steamID.m_SteamID == player.m_SteamID)
                {
                    if (AddUser(pl))
                        return;
                    break;
                }
            }
        }

        Provider.kick(player, "Player not properly set up.");
    }
    internal static void OnAccepted(SteamPlayer steamPlayer)
    {
        EditorLevel.PendingToReceiveActions.Remove(steamPlayer.transportConnection);
        Logger.LogInfo($"{steamPlayer.playerID.steamID.Format()} ({steamPlayer.playerID.playerName.Format(false)}) accepted.");
    }
    internal static bool AddUser(SteamPlayer pl)
    {
        lock (UsersIntl)
        {
            if (pl.player.gameObject.TryGetComponent(out EditorUser user))
            {
                user.IsOnline = true;
                bool added = false;
                ulong s64 = pl.playerID.steamID.m_SteamID;
                for (int j = 0; j < UsersIntl.Count; ++j)
                {
                    EditorUser u = UsersIntl[j];
                    if (u.SteamId.m_SteamID > s64)
                    {
                        UsersIntl.Insert(j, user);
                        added = true;
                        break;
                    }
                    if (u.SteamId.m_SteamID == user.SteamId.m_SteamID)
                    {
                        Logger.LogWarning("User {" + user.SteamId.m_SteamID.Format() + "} was already online.", method: "USERS");
                        RemoveUser(u);
                        UsersIntl[j] = user;
                        added = true;
                        break;
                    }
                }
                if (!added) UsersIntl.Add(user);

                user.Player = pl;
                user.IsOnline = true;
#if SERVER
                user.Connection = pl.transportConnection;
#endif
                user.Init();
                EventOnUserConnected.TryInvoke(user);
#if SERVER
                Logger.LogInfo("[USERS] Player added: " + user.DisplayName.Format() + " {" + user.SteamId.m_SteamID.Format() + "} @ " + user.Connection.Format() + ".");
#else
                Logger.LogInfo("[USERS] Player added: " + user.DisplayName.Format() + " {" + user.SteamId.m_SteamID.Format() + "} @ " + (user.Connection != null ? "Current Session" : "Remote Session") + ".");
#endif
                return true;
            }

            return false;
        }
    }
#if CLIENT
    internal static void Disconnect()
    {
        lock (UsersIntl)
        {
            if (EditorUser.User != null)
            {
                RemoveUser(EditorUser.User);
                EditorUser.User = null;
            }
            for (int i = Users.Count - 1; i >= 0; --i)
            {
                RemoveUser(Users[i]);
            }

            if (Users.Count > 0)
            {
                Logger.LogWarning("Unable to properly remove all users.", method: "USERS");
                UsersIntl.Clear();
            }
        }
    }
#endif
    internal static void RemoveUser(CSteamID player)
    {
        lock (UsersIntl)
        {
            EditorUser? user = FromId(player);
            if (user == null)
                return;
            RemoveUser(user);
        }
    }
    private static void RemoveUser(EditorUser user)
    {
#if CLIENT
        if (user.SteamId.m_SteamID == Provider.client.m_SteamID)
        {
            HighSpeedConnection? hs = HighSpeedConnection.Instance;
            hs?.Dispose();
        }
#endif
        UsersIntl.Remove(user);
        EventOnUserDisconnected.TryInvoke(user);
#if SERVER
        user.Input.Save();
#endif
        user.IsOnline = false;
        user.Player = null;
        Logger.LogInfo("[USERS] Player removed: " + user.DisplayName + " {" + user.SteamId.m_SteamID + "}.");
        Object.Destroy(user);
    }

    public static EditorUser? FromName(string name, NameSearchType type) => FromName(name, type, UsersIntl);
    public static EditorUser? FromName(string name, NameSearchType type, IEnumerable<EditorUser> selection)
    {
        lock (UsersIntl)
        {
            IEnumerable<EditorUser> editorUsers = selection as EditorUser[] ?? selection.ToArray();
            if (type == NameSearchType.CharacterName)
            {
                foreach (EditorUser current in editorUsers.OrderBy(SelectCharacterName))
                {
                    if (current.Player!.playerID.characterName.IndexOf(name, StringComparison.InvariantCultureIgnoreCase) != -1)
                        return current;
                }
                foreach (EditorUser current in editorUsers.OrderBy(SelectNickName))
                {
                    if (current.Player!.playerID.nickName.IndexOf(name, StringComparison.InvariantCultureIgnoreCase) != -1)
                        return current;
                }
                foreach (EditorUser current in editorUsers.OrderBy(SelectPlayerName))
                {
                    if (current.Player!.playerID.playerName.IndexOf(name, StringComparison.InvariantCultureIgnoreCase) != -1)
                        return current;
                }
                return null;
            }

            if (type == NameSearchType.NickName)
            {
                foreach (EditorUser current in editorUsers.OrderBy(SelectNickName))
                {
                    if (current.Player!.playerID.nickName.IndexOf(name, StringComparison.InvariantCultureIgnoreCase) != -1)
                        return current;
                }
                foreach (EditorUser current in editorUsers.OrderBy(SelectCharacterName))
                {
                    if (current.Player!.playerID.characterName.IndexOf(name, StringComparison.InvariantCultureIgnoreCase) != -1)
                        return current;
                }
                foreach (EditorUser current in editorUsers.OrderBy(SelectPlayerName))
                {
                    if (current.Player!.playerID.playerName.IndexOf(name, StringComparison.InvariantCultureIgnoreCase) != -1)
                        return current;
                }
                return null;
            }

            if (type == NameSearchType.PlayerName)
            {
                foreach (EditorUser current in editorUsers.OrderBy(SelectPlayerName))
                {
                    if (current.Player!.playerID.playerName.IndexOf(name, StringComparison.InvariantCultureIgnoreCase) != -1)
                        return current;
                }
                foreach (EditorUser current in editorUsers.OrderBy(SelectNickName))
                {
                    if (current.Player!.playerID.nickName.IndexOf(name, StringComparison.InvariantCultureIgnoreCase) != -1)
                        return current;
                }
                foreach (EditorUser current in editorUsers.OrderBy(SelectCharacterName))
                {
                    if (current.Player!.playerID.characterName.IndexOf(name, StringComparison.InvariantCultureIgnoreCase) != -1)
                        return current;
                }
                return null;
            }

            return FromName(name, NameSearchType.CharacterName, editorUsers);
        }
        
        int SelectPlayerName(EditorUser u) => u.Player!.playerID.playerName.Length;
        int SelectNickName(EditorUser u) => u.Player!.playerID.nickName.Length;
        int SelectCharacterName(EditorUser u) => u.Player!.playerID.characterName.Length;
    }
}
public enum NameSearchType : byte
{
    CharacterName,
    NickName,
    PlayerName
}