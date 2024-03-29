﻿using DevkitServer.API;
using DevkitServer.Multiplayer.Networking;
using DevkitServer.Players;
#if CLIENT
using System.Reflection;
#endif

namespace DevkitServer.Multiplayer.Sync;
/*
 * Todo list when adding a new sync class:
 *
 * Create class XXXSync : AuthoritativeSync<XXXSync>
 *  - Configure SupportsClientsideAuthority as false if needed.
 *
 * Add CreateServerAuthority call in DevkitServerModule.OnPostLevelLoaded
 * Add DestroyServerAuthority call in DevkitServerModule.shutdown
 * Add editor.AddComponent<XXXSync>().User = user; to DevkitServerGamemode.SetupEditorObject
 * Add new XXXSync property in EditorUser
 * Add XXXSync = EditorObject.GetComponent<XXXSync>(); in EditorUser.Init
 * Add IntlSyncs.Add(XXXSync); in EditorUser.Init
 * Add XXXSync.SendAuthority(Connection); in EditorUser.Init
 */
public abstract class AuthoritativeSync : MonoBehaviour
{
    protected static readonly NetCall<ulong, Type> SendAuthoritativeSyncAuthority = new NetCall<ulong, Type>((ushort)DevkitServerNetCall.TileSyncAuthority);
    protected static readonly Color AuthColor = new Color32(102, 255, 255, 255);
    protected static readonly Color NoAuthColor = new Color32(255, 80, 80, 255);
    protected bool HasAuthorityIntl;

    /// <remarks><see langword="null"/> for the server-side authority instance.</remarks>
    public EditorUser? User { get; internal set; }
    public bool IsOwner { get; protected set; }
    public virtual bool SupportsClientsideAuthority => true;

#if CLIENT
    private static MethodInfo? _setAuthMethod;
    [NetCall(NetCallSource.FromServer, DevkitServerNetCall.TileSyncAuthority)]
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

public abstract class AuthoritativeSync<TSync> : AuthoritativeSync, ITerminalFormattable where TSync : AuthoritativeSync<TSync>
{
    protected static readonly string Source = typeof(TSync).Name;
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
                Logger.DevkitServer.LogDebug(Source, $"Server-side authority {GetType().Format()} {(value ? "gained" : "lost")} authority.");
            else
                Logger.DevkitServer.LogDebug(Source, $"{User.Format()}'s {GetType().Format()} {(value ? "gained" : "lost")} authority.");
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
#if SERVER
    internal static void SendAuthority(ITransportConnection connection)
    {
        TSync? auth = GetAuthority();
        if (auth != null)
            SendAuthoritativeSyncAuthority.Invoke(connection, auth.User == null ? 0 : auth.User.SteamId.m_SteamID, typeof(TSync));
    }
#endif
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
                if (ts[j] is TSync { HasAuthority: true } sync)
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
            Logger.DevkitServer.LogDebug(Source, $"Server-side authority {GetType().Format()} initialized.");
            if (ServersideAuthority != null)
                Destroy(ServersideAuthority);
            ServersideAuthority = (TSync)this;
            HasAuthority = true;
        }
        else
        {
            Logger.DevkitServer.LogDebug(Source, $"Client {User.Format()} {GetType().Format()} initialized.");
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
        Logger.DevkitServer.LogDebug(Source, this.Format() + " destroyed.");
    }
    public override string ToString() => GetType().Name + " (" + (HasAuthority ? "Authority" : "Non-Authority") + ") (" + (User is null ? "Serverside" : User.SteamId.m_SteamID.ToString()) + ")";
    public string Format(ITerminalFormatProvider provider) => GetType().Format() + " (" + (HasAuthority ? "Authority".Colorize(AuthColor) : "Non-Authority".Colorize(NoAuthColor)) + ") (" + (User is null ? "Serverside" : User.SteamId.m_SteamID.Format()) + ")";
}