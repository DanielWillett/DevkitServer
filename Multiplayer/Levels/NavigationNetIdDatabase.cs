using DanielWillett.SpeedBytes;
using DanielWillett.SpeedBytes.Compression;
using DevkitServer.Multiplayer.Networking;

namespace DevkitServer.Multiplayer.Levels;
public sealed class NavigationNetIdDatabase : IReplicatedLevelDataSource<NavigationNetIdReplicatedLevelData>
{
    private const string Source = "NAVIGATION NET IDS";

    private static readonly NetId[] NetIds = new NetId[256];

    [UsedImplicitly]
    internal static NetCall<byte, NetId> SendBindNavigation = new NetCall<byte, NetId>(DevkitServerNetCall.SendBindNavigation);
    public ushort CurrentDataVersion => 0;
    internal static void Init()
    {
        NavigationUtil.OnFlagRemoved += OnFlagRemoved;
        NavigationUtil.OnFlagIndexUpdated += OnFlagIndexUpdated;
    }

    internal static void Shutdown()
    {
        NavigationUtil.OnFlagRemoved -= OnFlagRemoved;
        NavigationUtil.OnFlagIndexUpdated -= OnFlagIndexUpdated;
    }
    private static void OnFlagIndexUpdated(Flag flag, byte fromNav, byte toNav)
    {
        if (!DevkitServerModule.IsEditing)
            return;

        NetId blockingNetId = NetIds[toNav];
        NetId netId = NetIds[fromNav];
        if (!blockingNetId.IsNull() && blockingNetId == netId || netId.IsNull())
            return;

        if (!blockingNetId.IsNull())
        {
            Logger.DevkitServer.LogDebug(Source, $"Released blocking net id to save navigation flag: # {fromNav.Format()} ({netId.Format()}, # {toNav.Format()}).");
            NetIdRegistry.Release(blockingNetId);
        }

        NetIdRegistry.Release(netId);
        NetIdRegistry.Assign(netId, toNav);
        NetIds[fromNav] = NetId.INVALID;
        NetIds[toNav] = netId;
        Logger.DevkitServer.LogDebug(Source, $"Moved navigation flag NetId: # {fromNav.Format()} ({netId.Format()}, # {toNav.Format()}).");
    }
    private static void OnFlagRemoved(Flag flag, byte nav)
    {
        if (!DevkitServerModule.IsEditing)
            return;

        NetId netId = NetIds[nav];
        if (netId.IsNull())
            return;

        NetIdRegistry.Release(netId);
        NetIds[nav] = NetId.INVALID;
        Logger.DevkitServer.LogDebug(Source, $"Removed navigation flag NetId: ({netId.Format()}, # {nav.Format()}).");
    }
#if CLIENT
    [NetCall(NetCallSource.FromServer, DevkitServerNetCall.SendBindNavigation)]
    private static StandardErrorCode ReceiveBindNavigation(MessageContext ctx, byte nav, NetId netId)
    {
        RegisterNavigation(nav, netId);
        return StandardErrorCode.Success;
    }
#endif
    public static void RemoveNavigation(byte nav)
    {
        NetId id = NetIds[nav];
        if (id.IsNull())
        {
            Logger.DevkitServer.LogWarning(Source, $"Unable to release NetId to navigation flag {nav.Format()}, NetId not registered.");
            return;
        }

        if (!NetIdRegistry.Release(id))
            Logger.DevkitServer.LogWarning(Source, $"Unable to release NetId to navigation flag {nav.Format()}, NetId not registered in NetIdRegistry.");

        NetIds[nav] = NetId.INVALID;

        if (Level.isLoaded)
            Logger.DevkitServer.LogDebug(Source, $"Released navigation flag NetId: {id.Format()} ({nav.Format()}).");
    }
    public static NetId AddNavigation(byte nav)
    {
        NetId netId = NetIdRegistry.Claim();

        RegisterNavigation(nav, netId);

        return netId;
    }
    public static bool TryGetNavigation(NetId netId, out byte nav)
    {
        object? value = NetIdRegistry.Get(netId);

        if (value is byte navId)
        {
            nav = navId;
            return true;
        }

        nav = byte.MaxValue;
        return false;
    }
    public static bool TryGetNavigationNetId(byte nav, out NetId netId)
    {
        netId = NetIds[nav];
        return !netId.IsNull();
    }
    internal static void RegisterNavigation(byte nav, NetId netId)
    {
        NetId old = NetIds[nav];
        if (!old.IsNull() && old != netId && NetIdRegistry.Release(old))
        {
            if (Level.isLoaded)
                Logger.DevkitServer.LogDebug(Source, $"Released old NetId pairing: {old.Format()}.");
        }

        NetIds[nav] = netId;
        NetIdRegistry.Assign(netId, nav);

        if (Level.isLoaded)
            Logger.DevkitServer.LogDebug(Source, $"Claimed new NetId: {netId.Format()} to navigation flag {nav.Format()}.");
    }

#if SERVER
    internal static void AssignExisting()
    {
        if (LevelNavigation.flagData == null)
            throw new InvalidOperationException("LevelNavigation not loaded.");

        Array.Clear(NetIds, 0, NetIds.Length);

        IReadOnlyList<Flag> flags = NavigationUtil.NavigationFlags;

        int nav = 0;

        int ct = Math.Min(byte.MaxValue, flags.Count);

        for (; nav < ct; ++nav)
            AddNavigation((byte)nav);

        Logger.DevkitServer.LogInfo(Source, $"Assigned NetIds for {nav.Format()} navigation flag{nav.S()}.");
    }
#endif

#if CLIENT

    public void LoadData(NavigationNetIdReplicatedLevelData data)
    {
        int ct = Math.Min(NetIds.Length, data.NetIds.Length);
        if (ct < NetIds.Length)
            Array.Clear(NetIds, ct, NetIds.Length - ct);
        for (int i = 0; i < ct; ++i)
            NetIds[i] = new NetId(data.NetIds[i]);

        for (int i = 0; i < ct; ++i)
        {
            if (NetIds[i].IsNull())
                continue;

            NetIdRegistry.Assign(NetIds[i], (byte)i);
        }
    }

#elif SERVER

    public NavigationNetIdReplicatedLevelData SaveData(CSteamID user)
    {
        uint[] netIds = new uint[NetIds.Length];

        for (int i = 0; i < NetIds.Length; ++i)
            netIds[i] = NetIds[i].id;

        return new NavigationNetIdReplicatedLevelData
        {
            NetIds = netIds
        };
    }

#endif
    public void WriteData(ByteWriter writer, NavigationNetIdReplicatedLevelData data)
    {
        writer.WriteZeroCompressed(data.NetIds);
    }
    public NavigationNetIdReplicatedLevelData ReadData(ByteReader reader, ushort dataVersion)
    {
        return new NavigationNetIdReplicatedLevelData
        {
            NetIds = reader.ReadZeroCompressedUInt32Array(false)
        };
    }
}

#nullable disable
public class NavigationNetIdReplicatedLevelData
{
    public uint[] NetIds { get; set; }
}
#nullable restore