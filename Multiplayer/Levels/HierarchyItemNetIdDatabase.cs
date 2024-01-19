using DevkitServer.Multiplayer.Networking;
using DevkitServer.Util.Encoding;
using SDG.Framework.Devkit;

namespace DevkitServer.Multiplayer.Levels;
public sealed class HierarchyItemNetIdDatabase : IReplicatedLevelDataSource<HierarchyItemNetIdReplicatedLevelData>
{
    public ushort CurrentDataVersion => 0;

    private const string Source = "HIERARCHY ITEM NET IDS";
    private static readonly Dictionary<uint, NetId> HierarchyItemAssignments = new Dictionary<uint, NetId>(1024);
    [UsedImplicitly]
    internal static NetCall<uint, NetId> SendBindHierarchyItem = new NetCall<uint, NetId>(DevkitServerNetCall.SendBindHierarchyItem);
#if SERVER
    private static bool _initialLoaded;
#endif
    private HierarchyItemNetIdDatabase() { }
    internal static void Init()
    {
#if SERVER
        LevelHierarchy.itemRemoved += LevelHierarchyOnItemRemoved;
#endif
    }
    internal static void Shutdown()
    {
#if SERVER
        _initialLoaded = false;
#endif
        LevelHierarchy.itemRemoved -= LevelHierarchyOnItemRemoved;
    }
    private static void LevelHierarchyOnItemRemoved(IDevkitHierarchyItem item)
    {
#if SERVER
        if (!_initialLoaded) return;
#endif
        Logger.DevkitServer.LogDebug(Source, $"Hierarchy item removed: {item.Format()}.");
        RemoveHierarchyItem(item);
    }
#if CLIENT
    [NetCall(NetCallSource.FromServer, DevkitServerNetCall.SendBindHierarchyItem)]
    private static void ReceiveBindHierarchyItem(MessageContext ctx, uint instanceId, NetId netId)
    {
        if (HierarchyUtil.TryFindItem(instanceId, out IDevkitHierarchyItem item))
        {
            RegisterHierarchyItem(item, netId);
            ctx.Acknowledge(StandardErrorCode.Success);
        }
        else
            ctx.Acknowledge(StandardErrorCode.NotFound);
    }
#endif
    public static void RemoveHierarchyItem(IDevkitHierarchyItem item)
    {
        if (item == null)
            return;

        if (HierarchyItemAssignments.TryGetValue(item.instanceID, out NetId netId))
        {
            if (HierarchyUtil.TryGetTransform(item, out Transform transform))
                NetIdRegistry.ReleaseTransform(netId, transform);
            else
                NetIdRegistry.Release(netId);
            HierarchyItemAssignments.Remove(item.instanceID);
            if (Level.isLoaded)
                Logger.DevkitServer.LogDebug(Source, $"Released hierarchy item NetId: {netId.Format()} ({item.instanceID}, {item.Format()}).");
        }
        else
            Logger.DevkitServer.LogWarning(Source, $"Unable to release NetId to hierarchy item {netId.Format()} ({item.instanceID}, {item.Format()}), NetId not registered.");
    }
    public static NetId AddHierarchyItem(IDevkitHierarchyItem item)
    {
        NetId netId = NetIdRegistry.Claim();

        RegisterHierarchyItem(item, netId);

        return netId;
    }
    public static bool TryGetHierarchyItem(NetId netId, out IDevkitHierarchyItem item)
    {
        object? value = NetIdRegistry.Get(netId);
        if (value is Transform transform)
        {
            item = HierarchyUtil.GetItem(transform)!;
            return item != null;
        }
        
        if (value is uint instanceId)
        {
            item = HierarchyUtil.FindItem(instanceId)!;
            return item != null;
        }

        item = null!;
        return false;
    }
    public static bool TryGetHierarchyItemNetId(IDevkitHierarchyItem item, out NetId netId) => TryGetHierarchyItemNetId(item.instanceID, out netId);
    public static bool TryGetHierarchyItemNetId(uint instanceId, out NetId netId)
    {
        if (!HierarchyItemAssignments.TryGetValue(instanceId, out netId))
        {
            netId = NetId.INVALID;
            return false;
        }

        return true;
    }
    internal static void RegisterHierarchyItem(IDevkitHierarchyItem item, NetId netId)
    {
        if (HierarchyUtil.TryGetTransform(item, out Transform transform))
            ClaimTransformNetId(item, transform, netId);
        else
            ClaimBasicNetId(item, netId);
    }
#if SERVER
    internal static void AssignExisting()
    {
        if (LevelHierarchy.instance == null)
            throw new InvalidOperationException("LevelHierarchy not loaded.");

        _initialLoaded = true;

        HierarchyItemAssignments.Clear();

        List<IDevkitHierarchyItem> items = LevelHierarchy.instance.items;

        int hierarchyItemIndex = 0;

        for (; hierarchyItemIndex < items.Count; ++hierarchyItemIndex)
            AddHierarchyItem(items[hierarchyItemIndex]);
        
        Logger.DevkitServer.LogInfo(Source, $"Assigned NetIds for {hierarchyItemIndex.Format()} hierarchy item{hierarchyItemIndex.S()}.");
    }
#endif
    private static void ClaimBasicNetId(IDevkitHierarchyItem item, NetId netId)
    {
        if (HierarchyItemAssignments.TryGetValue(item.instanceID, out NetId old))
        {
            Transform? t = NetIdRegistry.GetTransform(netId, null);
            if (t != null)
            {
                if (Level.isLoaded)
                    Logger.DevkitServer.LogDebug(Source, $"NetId already set: {old.Format()} @ {(t == null ? null : t.name).Format()}.");
                NetIdRegistry.ReleaseTransform(old, t);
            }
            else if (NetIdRegistry.Release(old))
            {
                if (Level.isLoaded)
                    Logger.DevkitServer.LogDebug(Source, $"Released old NetId pairing: {old.Format()}.");
            }
        }

        HierarchyItemAssignments[item.instanceID] = netId;
        NetIdRegistry.Assign(netId, item.instanceID);
        if (Level.isLoaded)
            Logger.DevkitServer.LogDebug(Source, $"Claimed new NetId: {netId.Format()} @ {item.instanceID.Format()} ({item.Format()}).");
    }
    private static void ClaimTransformNetId(IDevkitHierarchyItem item, Transform transform, NetId netId)
    {
        if (HierarchyItemAssignments.TryGetValue(item.instanceID, out NetId old))
        {
            Transform? t = NetIdRegistry.GetTransform(netId, null);
            if (t != null)
            {
                if (t == transform)
                {
                    HierarchyItemAssignments[item.instanceID] = netId;
                    if (Level.isLoaded)
                        Logger.DevkitServer.LogDebug(Source, $"NetId was already registered: {old.Format()} @ {t.name.Format()}.");
                    return;
                }
                if (Level.isLoaded)
                    Logger.DevkitServer.LogDebug(Source, $"NetId already set: {old.Format()} @ {(t == null ? null : t.name).Format()}.");
                NetIdRegistry.ReleaseTransform(old, t);
            }
            else if (NetIdRegistry.Release(old))
            {
                if (Level.isLoaded)
                    Logger.DevkitServer.LogDebug(Source, $"Released old NetId pairing: {old.Format()}.");
            }
        }

        HierarchyItemAssignments[item.instanceID] = netId;
        if (NetIdRegistry.GetTransformNetId(transform, out old, out string path))
        {
            if (old == netId)
            {
                if (Level.isLoaded)
                    Logger.DevkitServer.LogDebug(Source, $"NetId was already claimed: {old.Format()} @ {path.Format()}.");
                return;
            }
            NetIdRegistry.ReleaseTransform(old, transform);
            if (Level.isLoaded)
                Logger.DevkitServer.LogDebug(Source, $"Released old transform pairing NetId: {old.Format()} @ {path.Format()}.");
        }
        NetIdRegistry.AssignTransform(netId, transform);
        if (Level.isLoaded)
            Logger.DevkitServer.LogDebug(Source, $"Claimed new NetId: {netId.Format()} @ {transform.name.Format()}.");
    }


#if CLIENT
    public void LoadData(HierarchyItemNetIdReplicatedLevelData data)
    {
        HierarchyItemAssignments.Clear();
        
        NetId[] netIds = data.NetIds;
        uint[] items = data.InstanceIds;
        for (int i = 0; i < items.Length; ++i)
        {
            uint instanceId = items[i];
            if (!HierarchyUtil.TryFindItem(instanceId, out IDevkitHierarchyItem item))
            {
                Logger.DevkitServer.LogWarning(Source, $"Unable to find hierarchy item in level data info: {instanceId.Format()}.");
                continue;
            }

            RegisterHierarchyItem(item, netIds[i]);
        }
    }
#elif SERVER
    public HierarchyItemNetIdReplicatedLevelData SaveData(CSteamID user)
    {
        NetId[] netIds = new NetId[HierarchyItemAssignments.Count];
        uint[] items = new uint[HierarchyItemAssignments.Count];
        int index = 0;

        foreach (KeyValuePair<uint, NetId> lvlObject in HierarchyItemAssignments)
        {
            netIds[index] = lvlObject.Value;
            items[index] = lvlObject.Key;
            ++index;
        }

        return new HierarchyItemNetIdReplicatedLevelData
        {
            InstanceIds = items,
            NetIds = netIds
        };
    }
#endif

    public void WriteData(ByteWriter writer, HierarchyItemNetIdReplicatedLevelData data)
    {
        NetId[] netIds = data.NetIds;
        uint[] instanceIds = data.InstanceIds;

        int ct = Math.Min(instanceIds.Length, netIds.Length);
        writer.Write(ct);

        for (int i = 0; i < ct; ++i)
            writer.Write(netIds[i].id);
        for (int i = 0; i < ct; ++i)
            writer.Write(instanceIds[i]);
    }
    public HierarchyItemNetIdReplicatedLevelData ReadData(ByteReader reader, ushort dataVersion)
    {
        int ct = reader.ReadInt32();
        NetId[] netIds = new NetId[ct];
        uint[] items = new uint[ct];

        for (int i = 0; i < ct; ++i)
            netIds[i] = reader.ReadNetId();
        for (int i = 0; i < ct; ++i)
            items[i] = reader.ReadUInt32();

        return new HierarchyItemNetIdReplicatedLevelData
        {
            InstanceIds = items,
            NetIds = netIds
        };
    }
}

#nullable disable
public class HierarchyItemNetIdReplicatedLevelData
{
    public NetId[] NetIds { get; set; }
    public uint[] InstanceIds { get; set; }
}
#nullable restore