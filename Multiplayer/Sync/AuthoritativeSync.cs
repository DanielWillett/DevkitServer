using System.Reflection;
using DevkitServer.Multiplayer.Networking;
using DevkitServer.Players;
using JetBrains.Annotations;

namespace DevkitServer.Multiplayer.Sync;
public abstract class AuthoritativeSync : MonoBehaviour
{
    protected static readonly NetCall<ulong, Type> SendAuthoritativeSyncAuthority = new NetCall<ulong, Type>((ushort)NetCalls.TileSyncAuthority);
    protected bool HasAuthorityIntl;

    /// <remarks><see langword="null"/> for the server-side authority instance.</remarks>
    public EditorUser? User { get; internal set; }
    public bool IsOwner { get; protected set; }

#if CLIENT
    private static MethodInfo? _setAuthMethod;
    [NetCall(NetCallSource.FromServer, (ushort)NetCalls.TileSyncAuthority)]
    [UsedImplicitly]
    private static void ReceiveAuthoritativeSyncAuthority(MessageContext ctx, ulong s64, Type type)
    {
        _setAuthMethod ??= typeof(AuthoritativeSync).GetMethod(nameof(ReceiveAuthoritativeSyncAuthorityGeneric), BindingFlags.Static | BindingFlags.NonPublic)!;
        _setAuthMethod.MakeGenericMethod(type).Invoke(null, new object[] { s64 });
    }
    private static void ReceiveAuthoritativeSyncAuthorityGeneric<TSync>(ulong steam64) where TSync : AuthoritativeSync<TSync>
    {
        if (steam64 == 0)
        {
            AuthoritativeSync<TSync>? serversideAuthority = AuthoritativeSync<TSync>.ServersideAuthority;
            if (serversideAuthority != null)
                serversideAuthority.HasAuthority = true;
            return;
        }
        EditorUser? user = UserManager.FromId(steam64);
        if (user != null && user.TileSync != null)
            user.TileSync.HasAuthority = true;
    }
#endif
}

public abstract class AuthoritativeSync<TSync> : AuthoritativeSync where TSync : AuthoritativeSync<TSync>
{
    public static TSync? Authority { get; private set; }
    public static TSync? ServersideAuthority { get; private set; }
    public bool HasAuthority
    {
        get => HasAuthorityIntl;
#if CLIENT
        internal
#endif
        set
        {
            if (HasAuthorityIntl == value)
                return;
            if (value)
            {
#if SERVER
                SendAuthoritativeSyncAuthority.Invoke(Provider.GatherRemoteClientConnections(), User == null ? 0 : User.SteamId.m_SteamID, typeof(TSync));
#endif
                TSync? old = GetAuthority();
                if (old != null)
                    old.HasAuthority = false;
                Authority = (TSync)this;
            }
            else
            {
                if (Authority == this)
                    Authority = null;
            }
            HasAuthorityIntl = value;
            if (User == null)
                Logger.LogDebug($"Server-side authority {this.Format()} {(value ? "gained" : "lost")} authority.");
            else
                Logger.LogDebug($"{User.Format()} {this.Format()} {(value ? "gained" : "lost")} authority.");
            OnAuthorityUpdated(value);
        }
    }
    public static TSync CreateServersideAuthority()
    {
        return (TSync)DevkitServerModule.GameObjectHost.AddComponent(typeof(TSync));
    }
    public static void DestroyServersideAuthority()
    {
        if (ServersideAuthority != null)
        {
            Destroy(ServersideAuthority);
            ServersideAuthority = null;
        }
    }
    internal static void SendAuthority(ITransportConnection connection)
    {
        TSync? auth = GetAuthority();
        if (auth != null)
            SendAuthoritativeSyncAuthority.Invoke(connection, auth.User == null ? 0 : auth.User.SteamId.m_SteamID, typeof(TSync));
    }
    [Pure]
    public static TSync? GetAuthority()
    {
        ThreadUtil.assertIsGameThread();

        TSync? authoritativeSync = Authority;
        if (authoritativeSync != null && authoritativeSync.HasAuthority)
            return authoritativeSync;
        authoritativeSync = ServersideAuthority;
        if (authoritativeSync != null && authoritativeSync.HasAuthority)
            return authoritativeSync;

        for (int i = 0; i < UserManager.Users.Count; ++i)
        {
            IReadOnlyList<AuthoritativeSync> ts = UserManager.Users[i].Syncs;
            for (int j = 0; j < ts.Count; ++j)
                if (ts[j] is TSync sync)
                    return sync;
        }

        return null;
    }

    protected abstract void Init();
    protected abstract void Deinit();
    protected virtual void OnAuthorityUpdated(bool authority) { }
    [UsedImplicitly]
    private void Start()
    {
        Init();
        if (User == null)
        {
#if SERVER
            IsOwner = true;
#endif
#if CLIENT
            IsOwner = false;
#endif
            Logger.LogDebug("Server-side authority TileSync initialized.");
            if (ServersideAuthority != null)
                Destroy(ServersideAuthority);
            ServersideAuthority = (TSync)this;
            HasAuthority = true;
        }
        else
        {
            Logger.LogDebug($"Client {User.Format()} TileSync initialized.");
#if CLIENT
            IsOwner = User == EditorUser.User;
#endif
#if SERVER
            IsOwner = false;
#endif
        }
    }
    [UsedImplicitly]
    private void OnDestroy()
    {
        Deinit();
#if SERVER
        if (HasAuthority && this != ServersideAuthority && ServersideAuthority != null)
        {
            ServersideAuthority.HasAuthority = true;
        }
#endif
    }
}