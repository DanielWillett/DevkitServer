using DevkitServer.Multiplayer.Networking;
using DevkitServer.Util.Encoding;

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
#if SERVER
        NavigationUtil.OnFlagRemoved += OnFlagRemoved;
#endif
        NavigationUtil.OnFlagIndexUpdated += OnFlagIndexUpdated;
    }

    internal static void Shutdown()
    {
#if SERVER
        NavigationUtil.OnFlagRemoved -= OnFlagRemoved;
#endif
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
            Logger.LogDebug($"[{Source}] Released blocking net id to save navigation flag: # {fromNav.Format()} ({netId.Format()}, # {toNav.Format()}).");
            NetIdRegistry.Release(blockingNetId);
        }

        NetIdRegistry.Release(netId);
        NetIdRegistry.Assign(netId, toNav);
        NetIds[fromNav] = NetId.INVALID;
        NetIds[toNav] = netId;
        Logger.LogDebug($"[{Source}] Moved navigation flag NetId: # {fromNav.Format()} ({netId.Format()}, # {toNav.Format()}).");
    }
#if SERVER
    private static void OnFlagRemoved(Flag flag, byte nav)
    {
        if (!DevkitServerModule.IsEditing)
            return;

        NetId netId = NetIds[nav];
        if (netId.IsNull())
            return;

        NetIdRegistry.Release(netId);
        NetIds[nav] = NetId.INVALID;
        Logger.LogDebug($"[{Source}] Removed navigation flag NetId: ({netId.Format()}, # {nav.Format()}).");
    }
#endif
#if CLIENT
    [NetCall(NetCallSource.FromServer, DevkitServerNetCall.SendBindNavigation)]
    private static StandardErrorCode ReceiveBindHierarchyItem(MessageContext ctx, byte nav, NetId netId)
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
            Logger.LogWarning($"Unable to release NetId to navigation flag {nav.Format()}, NetId not registered.", method: Source);
            return;
        }

        if (!NetIdRegistry.Release(id))
            Logger.LogWarning($"Unable to release NetId to navigation flag {nav.Format()}, NetId not registered in NetIdRegistry.", method: Source);

        NetIds[nav] = NetId.INVALID;

        if (Level.isLoaded)
            Logger.LogDebug($"[{Source}] Released navigation flag NetId: {id.Format()} ({nav.Format()}).");
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
                Logger.LogDebug($"[{Source}] Released old NetId pairing: {old.Format()}.");
        }

        NetIds[nav] = netId;
        NetIdRegistry.Assign(netId, nav);

        if (Level.isLoaded)
            Logger.LogDebug($"[{Source}] Claimed new NetId: {netId.Format()} to navigation flag {nav.Format()}.");
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

        Logger.LogInfo($"[{Source}] Assigned NetIds for {nav.Format()} navigation flag{nav.S()}.");
    }
#endif

#if CLIENT

    public void LoadData(NavigationNetIdReplicatedLevelData data)
    {
        int ct = Math.Min(NetIds.Length, data.NetIds.Length);
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

    public NavigationNetIdReplicatedLevelData SaveData()
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