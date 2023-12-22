using DevkitServer.API.Permissions;
using DevkitServer.Multiplayer.Networking;
using DevkitServer.Util.Encoding;
#if SERVER
using DevkitServer.Configuration;
using DevkitServer.Players;
#endif

namespace DevkitServer.Multiplayer;
public sealed class ClientInfo
{
#if CLIENT
    private static readonly CachedMulticastEvent<Action<ClientInfo>> OnClientInfoReadyEvent = new CachedMulticastEvent<Action<ClientInfo>>(typeof(ClientInfo), nameof(OnClientInfoReady));
    /// <summary>
    /// Called after permissions are applied.
    /// </summary>
    public static event Action<ClientInfo> OnClientInfoReady
#else
    internal static readonly CachedMulticastEvent<Action<EditorUser, ClientInfo>> OnClientInfoReadyEvent = new CachedMulticastEvent<Action<EditorUser, ClientInfo>>(typeof(ClientInfo), nameof(OnClientInfoReady));
    /// <summary>
    /// Called before being sent to the client.
    /// </summary>
    public static event Action<EditorUser, ClientInfo> OnClientInfoReady
#endif
    {
        add => OnClientInfoReadyEvent.Add(value);
        remove => OnClientInfoReadyEvent.Remove(value);
    }

    internal static readonly NetCallRaw<ClientInfo> SendClientInfo = new NetCallRaw<ClientInfo>((ushort)DevkitServerNetCall.SendClientInfo, ReadInfo, WriteInfo);

    public const ushort DataVersion = 1;
#if CLIENT
    public static ClientInfo? Info { get; private set; }

    [NetCall(NetCallSource.FromServer, DevkitServerNetCall.SendClientInfo)]
    [UsedImplicitly]
    private static void ReceiveClientInfo(MessageContext ctx, ClientInfo info)
    {
        Info = info;
        PermissionManager.UserPermissions.ReceivePermissions(info.Permissions, info.PermissionGroups);
        Logger.DevkitServer.LogInfo(nameof(ReceiveClientInfo), "Received client info.");
        if (Logger.Debug)
        {
            Logger.DevkitServer.LogDebug(nameof(ReceiveClientInfo), "=======================================");
            LoggerExtensions.DumpJson(info);
            Logger.DevkitServer.LogDebug(nameof(ReceiveClientInfo), "=======================================");
        }
        ctx.Acknowledge();

        OnClientInfoReadyEvent.TryInvoke(info);
    }
    internal static void OnDisconnect()
    {
        Info = null;
        Logger.DevkitServer.LogDebug(nameof(ClientInfo), "Cleared client info.");
    }
#endif


#nullable disable
    /// <remarks>
    /// This is not kept updated after initial connection. To access an updated list use <see cref="Permissions.PlayerHandler"/>.
    /// </remarks>
    public PermissionBranch[] Permissions { get; internal set; }

    /// <remarks>
    /// This is not kept updated after initial connection. To access an updated list use <see cref="Permissions.PlayerHandler"/>.
    /// </remarks>
    public PermissionGroup[] PermissionGroups { get; internal set; }

    public bool ServerRemovesCosmeticImprovements { get; private set; }
    public bool ServerTreatsAdminsAsSuperuser { get; private set; }
    public bool ServerHasHighSpeedSupport { get; private set; }
    public int ServerMaxClientEditFPS { get; private set; }
#nullable restore
    internal ClientInfo() { }

    public static void WriteInfo(ByteWriter writer, ClientInfo info)
        => info.Write(writer);
    public static ClientInfo ReadInfo(ByteReader reader)
    {
        ClientInfo info = new ClientInfo();
        info.Read(reader);
        return info;
    }
    public void Read(ByteReader reader)
    {
        ushort v = reader.ReadUInt16(); // version
        int len = reader.ReadInt32();
        List<PermissionBranch> perms = new List<PermissionBranch>(len);
        for (int i = 0; i < len; ++i)
        {
            PermissionBranch branch = PermissionBranch.Read(reader);
            if (branch.Valid)
                perms.Add(branch);
            else
                Logger.DevkitServer.LogDebug(nameof(ClientInfo), $"Invalid permission skipped: {branch.Format()}.");
        }

        Permissions = perms.ToArrayFast();

        PermissionGroups = new PermissionGroup[reader.ReadInt32()];
        for (int i = 0; i < PermissionGroups.Length; ++i)
            PermissionGroups[i] = PermissionGroup.ReadPermissionGroup(reader);

        byte flag = reader.ReadUInt8();
        ServerRemovesCosmeticImprovements = (flag & 1) != 0;
        ServerTreatsAdminsAsSuperuser = (flag & 2) == 0;
        ServerHasHighSpeedSupport = (flag & 4) != 0;

        if (v > 0)
            ServerMaxClientEditFPS = reader.ReadInt32();
    }
    public void Write(ByteWriter writer)
    {
        writer.Write(DataVersion);

        writer.Write(Permissions == null ? 0 : Permissions.Length);
        for (int i = 0; i < Permissions!.Length; i++)
        {
            PermissionBranch.Write(writer, Permissions[i]);
        }

        writer.Write(PermissionGroups == null ? 0 : PermissionGroups.Length);
        for (int i = 0; i < PermissionGroups!.Length; ++i)
            PermissionGroup.WritePermissionGroup(writer, PermissionGroups[i]);

        byte flag = 0;
        if (ServerRemovesCosmeticImprovements)
            flag |= 1;
        if (!ServerTreatsAdminsAsSuperuser)
            flag |= 2;
        if (ServerHasHighSpeedSupport)
            flag |= 4;

        writer.Write(flag);
        writer.Write(ServerMaxClientEditFPS);
    }
#if SERVER
    internal static void ApplyServerSettings(ClientInfo info, EditorUser user)
    {
        DevkitServerSystemConfig systemConfig = DevkitServerConfig.Config;
        info.ServerRemovesCosmeticImprovements = systemConfig.RemoveCosmeticImprovements;
        info.ServerTreatsAdminsAsSuperuser = systemConfig.AdminsAreSuperusers;
        info.ServerMaxClientEditFPS = systemConfig.MaxClientEditFPS;
        info.ServerHasHighSpeedSupport = systemConfig.TcpSettings.EnableHighSpeedSupport && !Provider.configData.Server.Use_FakeIP;
    }
#endif
}
