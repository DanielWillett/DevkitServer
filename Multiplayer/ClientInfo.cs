using DevkitServer.API.Permissions;
using DevkitServer.Multiplayer.Networking;
using DevkitServer.Util.Encoding;
#if CLIENT
using JetBrains.Annotations;
#endif
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

    internal static readonly NetCallRaw<ClientInfo> SendClientInfo = new NetCallRaw<ClientInfo>((ushort)NetCalls.SendClientInfo, ReadInfo, WriteInfo);

    public const ushort DataVersion = 2;
#if CLIENT
    public static ClientInfo? Info { get; private set; }

    [NetCall(NetCallSource.FromServer, (ushort)NetCalls.SendClientInfo)]
    [UsedImplicitly]
    private static void ReceiveClientInfo(MessageContext ctx, ClientInfo info)
    {
        Info = info;
        UserPermissions.UserHandler.ReceivePermissions(info.Permissions, info.PermissionGroups);
        Logger.LogDebug("Received client info.");
        Logger.DumpJson(info);
        ctx.Acknowledge();

        OnClientInfoReadyEvent.TryInvoke(info);
    }
    internal static void OnDisconnect()
    {
        Info = null;
        Logger.LogDebug("Cleared client info.");
    }
#endif


#nullable disable
    /// <remarks>
    /// This is not kept updated after initial connection. To access an updated list use <see cref="Permissions.PlayerHandler"/>.
    /// </remarks>
    public Permission[] Permissions { get; internal set; }

    /// <remarks>
    /// This is not kept updated after initial connection. To access an updated list use <see cref="Permissions.PlayerHandler"/>.
    /// </remarks>
    public PermissionGroup[] PermissionGroups { get; internal set; }

    public bool ServerRemovesCosmeticImprovements { get; internal set; }
    public bool ServerUsesBypassingObjectSelectionLimitPermission { get; internal set; }
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
        ushort v = reader.ReadUInt16();
        int len = reader.ReadInt32();
        List<Permission> perms = new List<Permission>(len);
        for (int i = 0; i < len; ++i)
        {
            string str = reader.ReadString();
            if (Permission.TryParse(str, out Permission p))
                perms.Add(p);
            else
                Logger.LogInfo("Unable to find permission: " + str.Format() + ", usually not a problem.");
        }

        Permissions = perms.ToArrayFast();

        PermissionGroups = new PermissionGroup[reader.ReadInt32()];
        for (int i = 0; i < PermissionGroups.Length; ++i)
            PermissionGroups[i] = PermissionGroup.ReadPermissionGroup(reader);

        if (v < 1)
            reader.ReadBool();

        if (v > 1)
        {
            byte flag = reader.ReadUInt8();
            ServerRemovesCosmeticImprovements = (flag & 1) != 0;
            ServerUsesBypassingObjectSelectionLimitPermission = (flag & 2) != 0;
        }
    }
    public void Write(ByteWriter writer)
    {
        writer.Write(DataVersion);

        writer.Write(Permissions == null ? 0 : Permissions.Length);
        for (int i = 0; i < Permissions!.Length; i++)
        {
            Permission permission = Permissions[i];
            writer.Write(permission.ToString());
        }

        writer.Write(PermissionGroups == null ? 0 : PermissionGroups.Length);
        for (int i = 0; i < PermissionGroups!.Length; ++i)
            PermissionGroup.WritePermissionGroup(writer, PermissionGroups[i]);

        byte flag = 0;
        if (ServerRemovesCosmeticImprovements)
            flag |= 1;
        if (ServerUsesBypassingObjectSelectionLimitPermission)
            flag |= 2;

        writer.Write(flag);
    }
#if SERVER
    internal static void ApplyServerSettings(ClientInfo info, EditorUser user)
    {
        SystemConfig systemConfig = DevkitServerConfig.Config;
        info.ServerRemovesCosmeticImprovements = systemConfig.RemoveCosmeticImprovements;
        info.ServerUsesBypassingObjectSelectionLimitPermission = systemConfig.UseBypassingObjectSelectionLimitPermission;
    }
#endif
}
