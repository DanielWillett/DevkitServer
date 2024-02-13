using DevkitServer.API;
using DevkitServer.Multiplayer.Levels;
using DevkitServer.Players;
#if SERVER
using DevkitServer.API.Abstractions;
using DevkitServer.Levels;
using DevkitServer.Multiplayer.Networking;
using System.Reflection;
using System.Reflection.Emit;
#endif
#if CLIENT
using DevkitServer.Multiplayer.Networking;
#endif

namespace DevkitServer.Multiplayer;
[EarlyTypeInit]
public static class UserManager
{
#if CLIENT
    internal static readonly CachedMulticastEvent<Action> EventOnConnectedToServer = new CachedMulticastEvent<Action>(typeof(UserManager), nameof(OnConnectedToServer));
#endif
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
#if CLIENT
    public static event Action OnConnectedToServer
    {
        add => EventOnConnectedToServer.Add(value);
        remove => EventOnConnectedToServer.Remove(value);
    }
#endif
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

        CSteamID steamId = TryGetSteamId(connection);
        if (steamId.UserSteam64())
        {
            EditorUser? user = FromId(steamId.m_SteamID);
            if (user != null)
                return user;
        }

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
        Logger.DevkitServer.LogInfo("USERS", $"{steamPlayer.playerID.steamID.Format()} ({steamPlayer.playerID.playerName.Format(false)}) accepted.");
    }
    internal static bool AddUser(SteamPlayer pl)
    {
        lock (UsersIntl)
        {
            if (!pl.player.gameObject.TryGetComponent(out EditorUser user))
                return false;

            user.IsOnline = true;
            bool added = false;
            ulong s64 = pl.playerID.steamID.m_SteamID;
            for (int j = 0; j < UsersIntl.Count; ++j)
            {
                EditorUser u = UsersIntl[j];
                if (u.SteamId.m_SteamID == user.SteamId.m_SteamID)
                {
                    Logger.DevkitServer.LogWarning("USERS", "User {" + user.SteamId.m_SteamID.Format() + "} was already online.");
                    RemoveUser(u);
                    UsersIntl[j] = user;
                    added = true;
                    break;
                }
            }
            if (!added)
            {
                for (int j = 0; j < UsersIntl.Count; ++j)
                {
                    EditorUser u = UsersIntl[j];
                    if (u.SteamId.m_SteamID > s64)
                    {
                        UsersIntl.Insert(j, user);
                        added = true;
                        break;
                    }
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
            Logger.DevkitServer.LogInfo("USERS", "Player added: " + user.DisplayName.Format() + " {" + user.SteamId.m_SteamID.Format() + "} @ " + user.Connection.Format() + ".");
#else
            Logger.DevkitServer.LogInfo("USERS", "Player added: " + user.DisplayName.Format() + " {" + user.SteamId.m_SteamID.Format() + "} @ " + (user.Connection != null ? "Current Session" : "Remote Session") + ".");
#endif
            return true;

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
                Logger.DevkitServer.LogWarning("USERS", "Unable to properly remove all users.");
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
        Logger.DevkitServer.LogInfo("USERS", "Player removed: " + user.DisplayName + " {" + user.SteamId.m_SteamID + "}.");
        Object.Destroy(user);
    }

    public static SteamPlayer? FromName(string name, NameSearchType type) => FromName(name, type, Provider.clients);
    public static SteamPlayer? FromName(string name, NameSearchType type, IEnumerable<SteamPlayer> selection)
    {
        lock (UsersIntl)
        {
            SteamPlayer[] steamPlayers = selection.ToArray();
            if (type == NameSearchType.CharacterName)
            {
                Array.Sort(steamPlayers, SortCharacterName);
                foreach (SteamPlayer current in steamPlayers)
                {
                    if (current.playerID.characterName.IndexOf(name, StringComparison.InvariantCultureIgnoreCase) != -1)
                        return current;
                }
                Array.Sort(steamPlayers, SortNickName);
                foreach (SteamPlayer current in steamPlayers)
                {
                    if (current.playerID.nickName.IndexOf(name, StringComparison.InvariantCultureIgnoreCase) != -1)
                        return current;
                }
                Array.Sort(steamPlayers, SortPlayerName);
                foreach (SteamPlayer current in steamPlayers)
                {
                    if (current.playerID.playerName.IndexOf(name, StringComparison.InvariantCultureIgnoreCase) != -1)
                        return current;
                }
                return null;
            }

            if (type == NameSearchType.NickName)
            {
                Array.Sort(steamPlayers, SortNickName);
                foreach (SteamPlayer current in steamPlayers)
                {
                    if (current.playerID.nickName.IndexOf(name, StringComparison.InvariantCultureIgnoreCase) != -1)
                        return current;
                }
                Array.Sort(steamPlayers, SortCharacterName);
                foreach (SteamPlayer current in steamPlayers)
                {
                    if (current.playerID.characterName.IndexOf(name, StringComparison.InvariantCultureIgnoreCase) != -1)
                        return current;
                }
                Array.Sort(steamPlayers, SortPlayerName);
                foreach (SteamPlayer current in steamPlayers)
                {
                    if (current.playerID.playerName.IndexOf(name, StringComparison.InvariantCultureIgnoreCase) != -1)
                        return current;
                }
                return null;
            }

            if (type == NameSearchType.PlayerName)
            {
                Array.Sort(steamPlayers, SortPlayerName);
                foreach (SteamPlayer current in steamPlayers)
                {
                    if (current.playerID.playerName.IndexOf(name, StringComparison.InvariantCultureIgnoreCase) != -1)
                        return current;
                }
                Array.Sort(steamPlayers, SortCharacterName);
                foreach (SteamPlayer current in steamPlayers)
                {
                    if (current.playerID.characterName.IndexOf(name, StringComparison.InvariantCultureIgnoreCase) != -1)
                        return current;
                }
                Array.Sort(steamPlayers, SortNickName);
                foreach (SteamPlayer current in steamPlayers)
                {
                    if (current.playerID.nickName.IndexOf(name, StringComparison.InvariantCultureIgnoreCase) != -1)
                        return current;
                }
                return null;
            }

            return FromName(name, NameSearchType.CharacterName, steamPlayers);
        }
        
        int SortPlayerName(SteamPlayer a, SteamPlayer b) => a.playerID.playerName.Length.CompareTo(b.playerID.playerName.Length);
        int SortNickName(SteamPlayer a, SteamPlayer b) => a.playerID.nickName.Length.CompareTo(b.playerID.nickName.Length);
        int SortCharacterName(SteamPlayer a, SteamPlayer b) => a.playerID.characterName.Length.CompareTo(b.playerID.characterName.Length);
    }
#if SERVER
    private static Func<ITransportConnection, CSteamID>? _getSteamIdFunction;
    private static bool _hasGeneratedSteamIdFunction;

    /// <summary>
    /// Tries to get the Steam64 ID of a transport connection first from it's implementation (SteamNetworkingSockets, etc) then just with <see cref="Provider.findTransportConnectionSteamId"/>.
    /// </summary>
    /// <returns>The <see cref="CSteamID"/> of the user behind the connection, or just <see cref="CSteamID.Nil"/>.</returns>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static CSteamID TryGetSteamId(ITransportConnection connection)
    {
        if (connection is HighSpeedConnection hs)
            return new CSteamID(hs.Steam64);

        ThreadUtil.assertIsGameThread();

        if (!_hasGeneratedSteamIdFunction)
        {
            _hasGeneratedSteamIdFunction = true;
            try
            {
                GenerateTransportConnectionSteamIdMethod();
            }
            catch (Exception ex)
            {
                Logger.DevkitServer.LogError(nameof(TryGetSteamId), ex, "Failed to create ITransportConnection to CSteamID converter method.");
            }
        }
        
        if (_getSteamIdFunction != null)
        {
            CSteamID id = _getSteamIdFunction(connection);
            if (id.m_SteamID != 0)
                return id;
        }

        return Provider.findTransportConnectionSteamId(connection);
    }
    private static void GenerateTransportConnectionSteamIdMethod()
    {
        Accessor.GetDynamicMethodFlags(true, out MethodAttributes attr, out CallingConventions convention);

        DynamicMethod method = new DynamicMethod("TryGetSteamIdIntl", attr, convention, typeof(CSteamID),
            [ typeof(ITransportConnection) ], typeof(UserManager), true);
        method.DefineParameter(1, ParameterAttributes.None, "transportConnection");

        IOpCodeEmitter emitter = method.AsEmitter(false);

        Type? loopback = Accessor.AssemblyCSharp.GetType("SDG.NetTransport.Loopback.TransportConnection_Loopback");
        Type? steamNetworking = Accessor.AssemblyCSharp.GetType("SDG.NetTransport.SteamNetworking.TransportConnection_SteamNetworking");
        Type? steamSockets = Accessor.AssemblyCSharp.GetType("SDG.NetTransport.SteamNetworkingSockets.TransportConnection_SteamNetworkingSockets");

        MethodInfo? getClientSteamId = loopback == null ? null : typeof(Provider).GetProperty(nameof(Provider.client),
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, typeof(CSteamID), Type.EmptyTypes,
            null)?.GetGetMethod(true);

        FieldInfo? steamNetworkingSteamId = steamNetworking?.GetField("steamId", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo? steamSocketsIdentity = steamSockets?.GetField("steamIdentity", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        MethodInfo? getIdentityId = steamSocketsIdentity == null
            ? null : typeof(SteamNetworkingIdentity).GetMethod(nameof(SteamNetworkingIdentity.GetSteamID),
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, CallingConventions.Any, Type.EmptyTypes, null);

        emitter.Emit(OpCodes.Ldarg_0);

        Label? lblNext = null;
        if (steamSockets != null && steamSocketsIdentity != null && getIdentityId != null)
        {
            lblNext = emitter.DefineLabel();
            emitter.Emit(OpCodes.Dup);
            emitter.Emit(OpCodes.Isinst, steamSockets);
            emitter.Emit(OpCodes.Brfalse_S, lblNext.Value);
            emitter.Emit(OpCodes.Ldflda, steamSocketsIdentity);
            emitter.Emit(OpCodes.Call, getIdentityId);
            emitter.Emit(OpCodes.Ret);
        }

        emitter.TryMarkLabel(lblNext);

        lblNext = null;
        if (loopback != null && getClientSteamId != null)
        {
            lblNext = emitter.DefineLabel();
            emitter.Emit(OpCodes.Dup);
            emitter.Emit(OpCodes.Isinst, loopback);
            emitter.Emit(OpCodes.Brfalse_S, lblNext.Value);
            emitter.Emit(OpCodes.Pop);
            emitter.Emit(OpCodes.Call, getClientSteamId);
            emitter.Emit(OpCodes.Ret);
        }

        emitter.TryMarkLabel(lblNext);

        lblNext = null;
        if (steamNetworking != null && steamNetworkingSteamId != null)
        {
            lblNext = emitter.DefineLabel();
            emitter.Emit(OpCodes.Dup);
            emitter.Emit(OpCodes.Isinst, steamNetworking);
            emitter.Emit(OpCodes.Brfalse_S, lblNext.Value);
            emitter.Emit(OpCodes.Unbox, steamNetworking);
            emitter.Emit(OpCodes.Ldflda, steamNetworkingSteamId);
            emitter.Emit(OpCodes.Ldobj, typeof(CSteamID));
            emitter.Emit(OpCodes.Ret);
        }

        emitter.TryMarkLabel(lblNext);
        emitter.DeclareLocal(typeof(CSteamID));

        emitter.Emit(OpCodes.Pop);
        emitter.Emit(OpCodes.Ldloca_S, 0);
        emitter.Emit(OpCodes.Initobj, typeof(CSteamID));
        emitter.Emit(OpCodes.Ldloc_0);
        emitter.Emit(OpCodes.Ret);

        _getSteamIdFunction = (Func<ITransportConnection, CSteamID>)method.CreateDelegate(typeof(Func<ITransportConnection, CSteamID>));
    }
#endif
}
public enum NameSearchType : byte
{
    CharacterName,
    NickName,
    PlayerName
}