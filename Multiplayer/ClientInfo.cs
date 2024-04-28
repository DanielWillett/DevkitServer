using DanielWillett.SpeedBytes;
using DevkitServer.API.Permissions;
using DevkitServer.Multiplayer.Levels;
#if SERVER
using DevkitServer.Configuration;
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
    internal static readonly CachedMulticastEvent<Action<CSteamID, ClientInfo>> OnClientInfoReadyEvent = new CachedMulticastEvent<Action<CSteamID, ClientInfo>>(typeof(ClientInfo), nameof(OnClientInfoReady));
    /// <summary>
    /// Called before being sent to the client.
    /// </summary>
    public static event Action<CSteamID, ClientInfo> OnClientInfoReady
#endif
    {
        add => OnClientInfoReadyEvent.Add(value);
        remove => OnClientInfoReadyEvent.Remove(value);
    }

    public const ushort DataVersion = 1;
#if CLIENT
    public static ClientInfo? Info { get; private set; }
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

    /// <summary>
    /// If the server forces cosmetic improvements off that affect the server's performance.
    /// </summary>
    public bool ServerRemovesCosmeticImprovements { get; private set; }

    /// <summary>
    /// If the server treats vanilla admins as superusers (having all permissions).
    /// </summary>
    public bool ServerTreatsAdminsAsSuperuser { get; private set; }

    /// <summary>
    /// If the server has high-speed TCP server set up.
    /// </summary>
    public bool ServerHasHighSpeedSupport { get; private set; }

    /// <summary>
    /// If the server has 'hide_map_name' set to true.
    /// </summary>
    public bool ServerForcesHideMapNameFromRichPresence { get; private set; }

    /// <summary>
    /// If the server has 'sync_editor_time' set to true.
    /// </summary>
    public bool ServerSyncsEditorTime { get; private set; }

    /// <summary>
    /// If the server has 'sync_editor_weather' set to true.
    /// </summary>
    public bool ServerSyncsEditorWeather { get; private set; }

    /// <summary>
    /// The server's max cleint FPS when doing edits that would create actions every frame.
    /// </summary>
    public int ServerMaxClientEditFPS { get; private set; }
#nullable restore

    public void Read(ByteReader reader, ushort dataVersion)
    {
        _ = dataVersion;
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
        ServerTreatsAdminsAsSuperuser = (flag & (1 << 1)) == 0;
        ServerHasHighSpeedSupport = (flag & (1 << 2)) != 0;
        ServerForcesHideMapNameFromRichPresence = (flag & (1 << 3)) != 0;
        ServerSyncsEditorTime = (flag & (1 << 4)) != 0;
        ServerSyncsEditorWeather = (flag & (1 << 5)) != 0;

        ServerMaxClientEditFPS = reader.ReadInt32();
    }
    public void Write(ByteWriter writer)
    {
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
            flag |= 1 << 1;
        if (ServerHasHighSpeedSupport)
            flag |= 1 << 2;
        if (ServerForcesHideMapNameFromRichPresence)
            flag |= 1 << 3;
        if (ServerSyncsEditorTime)
            flag |= 1 << 4;
        if (ServerSyncsEditorWeather)
            flag |= 1 << 5;

        writer.Write(flag);
        writer.Write(ServerMaxClientEditFPS);
    }
#if SERVER
    internal static void ApplyServerSettings(ClientInfo info)
    {
        DevkitServerSystemConfig systemConfig = DevkitServerConfig.Config;
        info.ServerRemovesCosmeticImprovements = systemConfig.RemoveCosmeticImprovements;
        info.ServerTreatsAdminsAsSuperuser = systemConfig.AdminsAreSuperusers;
        info.ServerMaxClientEditFPS = systemConfig.MaxClientEditFPS;
        info.ServerHasHighSpeedSupport = systemConfig.TcpSettings.EnableHighSpeedSupport && !Provider.configData.Server.Use_FakeIP;
        info.ServerForcesHideMapNameFromRichPresence = systemConfig.HideMapNameFromRichPresence;
        info.ServerSyncsEditorTime = systemConfig.SyncEditorTime;
        info.ServerSyncsEditorWeather = systemConfig.SyncEditorWeather;
    }
#endif

    public sealed class ClientInfoReplicatedLevelDataSource : IReplicatedLevelDataSource<ClientInfo>
    {
        public ushort CurrentDataVersion => 0;
#if CLIENT
        public void LoadData(ClientInfo data)
        {
            Info = data;
            PermissionManager.UserPermissions.ReceivePermissions(data.Permissions, data.PermissionGroups);
            Logger.DevkitServer.LogInfo(nameof(ClientInfo), "Received client info.");
            if (Logger.Debug)
            {
                Logger.DevkitServer.LogDebug(nameof(ClientInfo), "=======================================");
                LoggerExtensions.DumpJson(data);
                Logger.DevkitServer.LogDebug(nameof(ClientInfo), "=======================================");
            }

            OnClientInfoReadyEvent.TryInvoke(data);
        }
#else
        public ClientInfo SaveData(CSteamID user)
        {
            ClientInfo info = DevkitServerGamemode.GetClientInfo(user);
            ApplyServerSettings(info);
            OnClientInfoReadyEvent.TryInvoke(user, info);
            LoggerExtensions.DumpJson(info);
            return info;
        }
#endif
        public void WriteData(ByteWriter writer, ClientInfo data)
        {
            data.Write(writer);
        }
        public ClientInfo ReadData(ByteReader reader, ushort dataVersion)
        {
            ClientInfo info = new ClientInfo();
            info.Read(reader, dataVersion);
            return info;
        }
    }
}
