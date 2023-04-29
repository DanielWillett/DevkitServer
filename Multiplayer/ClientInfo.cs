using DevkitServer.API.Permissions;
using DevkitServer.Multiplayer.Networking;
using DevkitServer.Util.Encoding;
using JetBrains.Annotations;

namespace DevkitServer.Multiplayer;
public sealed class ClientInfo
{
    internal static readonly NetCallRaw<ClientInfo> SendClientInfo = new NetCallRaw<ClientInfo>((ushort)NetCalls.SendClientInfo, ReadInfo, WriteInfo);
    public const ushort DataVersion = 0;
#if CLIENT
    public static ClientInfo? Info { get; private set; }

    [NetCall(NetCallSource.FromServer, (ushort)NetCalls.SendClientInfo)]
    [UsedImplicitly]
    private static void ReceiveClientInfo(MessageContext ctx, ClientInfo info)
    {
        Info = info;
        UserPermissions.UserHandler.ReceivePermissions(info.Permissions, info.PermissionGroups);
        Logger.LogDebug("Received client info.");
        ctx.Acknowledge();
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
        _ = reader.ReadUInt16();
        int len = reader.ReadInt32();
        List<Permission> perms = new List<Permission>(len);
        for (int i = 0; i < len; ++i)
        {
            string str = reader.ReadString();
            if (Permission.TryParse(str, out Permission p))
                perms.Add(p);
            else
                Logger.LogWarning("Unable to parse permission: " + str.Format() + ".");
        }

        Permissions = perms.ToArray();

        PermissionGroups = new PermissionGroup[reader.ReadInt32()];
        for (int i = 0; i < perms.Count; ++i)
            PermissionGroups[i] = PermissionGroup.ReadPermissionGroup(reader);
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
        for (int i = 0; i < PermissionGroups!.Length; i++)
            PermissionGroup.WritePermissionGroup(writer, PermissionGroups[i]);
    }
}
