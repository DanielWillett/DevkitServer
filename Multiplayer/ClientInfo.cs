using DevkitServer.API.Permissions;
using DevkitServer.Multiplayer.Networking;
using DevkitServer.Util.Encoding;
#if CLIENT
using JetBrains.Annotations;
#endif
#if SERVER
using DevkitServer.Players;
#endif

namespace DevkitServer.Multiplayer;
public sealed class ClientInfo
{
#if CLIENT
    public static event Action<ClientInfo>? OnClientInfoReady;
#else
    public static event Action<EditorUser, ClientInfo>? OnClientInfoReady;
#endif
    internal static readonly NetCallRaw<ClientInfo> SendClientInfo = new NetCallRaw<ClientInfo>((ushort)NetCalls.SendClientInfo, ReadInfo, WriteInfo);
    public const ushort DataVersion = 1;
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
        if (OnClientInfoReady == null) return;
        foreach (Action<ClientInfo> inv in OnClientInfoReady.GetInvocationList().Cast<Action<ClientInfo>>())
        {
            try
            {
                inv(info);
            }
            catch (Exception ex)
            {
                Logger.LogError("Plugin threw an error in " + typeof(ClientInfo).Format() + "." + nameof(OnClientInfoReady) + ".");
                Logger.LogError(ex);
            }
        }
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

        Permissions = perms.ToArray();

        PermissionGroups = new PermissionGroup[reader.ReadInt32()];
        for (int i = 0; i < PermissionGroups.Length; ++i)
            PermissionGroups[i] = PermissionGroup.ReadPermissionGroup(reader);

        if (v < 1)
            reader.ReadBool();
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
    }

#if SERVER
    internal static void TryInvokeOnClientInfoReady(EditorUser user, ClientInfo info)
    {
        if (OnClientInfoReady == null) return;
        foreach (Action<EditorUser, ClientInfo> inv in OnClientInfoReady.GetInvocationList().Cast<Action<EditorUser, ClientInfo>>())
        {
            try
            {
                inv(user, info);
            }
            catch (Exception ex)
            {
                Logger.LogError("Plugin threw an error in " + typeof(ClientInfo).Format() + "." + nameof(OnClientInfoReady) + " for " + user.Format() + ".");
                Logger.LogError(ex);
            }
        }
    }
#endif
}
