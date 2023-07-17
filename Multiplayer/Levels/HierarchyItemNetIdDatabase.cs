using DevkitServer.Levels;
using DevkitServer.Multiplayer.Networking;
using SDG.Framework.Devkit;

namespace DevkitServer.Multiplayer.Levels;
internal static class HierarchyItemNetIdDatabase
{
    private const string Source = "HIERARCHY ITEM NET IDS";
    private static readonly Dictionary<uint, NetId> HierarchyItemAssignments = new Dictionary<uint, NetId>(1024);
    [UsedImplicitly]
    internal static NetCall<uint, NetId> SendBindHierarchyItem = new NetCall<uint, NetId>(NetCalls.SendBindHierarchyItem);
#if SERVER
    private static bool _initialLoaded;
#endif
    internal static void Init()
    {
#if SERVER
        LevelHierarchy.itemRemoved += LevelHierarchyOnItemRemoved;
        LevelHierarchy.itemAdded += LevelHierarchyOnItemAdded;
#endif
}
    internal static void Shutdown()
    {
#if SERVER
        _initialLoaded = false;
        LevelHierarchy.itemRemoved -= LevelHierarchyOnItemRemoved;
        LevelHierarchy.itemAdded -= LevelHierarchyOnItemAdded;
#endif
    }
#if SERVER
    private static void LevelHierarchyOnItemAdded(IDevkitHierarchyItem item)
    {
        if (!_initialLoaded) return;
        Logger.LogDebug($"[{Source}] Hierarchy item added: {item.Format()}.");
        AddHierarchyItem(item);
    }
    private static void LevelHierarchyOnItemRemoved(IDevkitHierarchyItem item)
    {
        if (!_initialLoaded) return;
        Logger.LogDebug($"[{Source}] Hierarchy item removed: {item.Format()}.");
        RemoveHierarchyItem(item);
    }
#endif
#if CLIENT
    [NetCall(NetCallSource.FromServer, NetCalls.SendBindHierarchyItem)]
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
                Logger.LogDebug($"[{Source}] Released hierarchy item NetId: {netId.Format()} ({item.instanceID}, {item.Format()})");
        }
        else
            Logger.LogWarning($"Unable to release NetId to hierarchy item {netId.Format()} ({item.instanceID}, {item.Format()}), NetId not registered.", method: Source);
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
#if CLIENT
    public static void LoadFromLevelData()
    {
        LevelData data = EditorLevel.ServerPendingLevelData ?? throw new InvalidOperationException("Level data not loaded.");
        NetId[] netIds = data.HierarchyItemNetIds;
        uint[] items = data.HierarchyItems;
        for (int i = 0; i < items.Length; ++i)
        {
            uint instanceId = items[i];
            if (!HierarchyUtil.TryFindItem(instanceId, out IDevkitHierarchyItem item))
            {
                Logger.LogWarning($"Unable to find hierarchy item in level data info: {instanceId.Format()}.");
                continue;
            }

            RegisterHierarchyItem(item, netIds[i]);
        }
    }
#endif
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
        
        Logger.LogInfo($"[{Source}] Assigned NetIds for {hierarchyItemIndex.Format()} hierarchy items{hierarchyItemIndex.S()}.");
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
                    Logger.LogDebug($"[{Source}] NetId already set: {old.Format()} @ {(t == null ? null : t.name).Format()}.");
                NetIdRegistry.ReleaseTransform(old, t);
            }
            else if (NetIdRegistry.Release(old))
            {
                if (Level.isLoaded)
                    Logger.LogDebug($"[{Source}] Released old NetId pairing: {old.Format()}.");
            }
        }

        HierarchyItemAssignments[item.instanceID] = netId;
        NetIdRegistry.Assign(netId, item.instanceID);
        if (Level.isLoaded)
            Logger.LogDebug($"[{Source}] Claimed new NetId: {netId.Format()} @ {item.instanceID.Format()} ({item.Format()}).");
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
                        Logger.LogDebug($"[{Source}] NetId was already registered: {old.Format()} @ {t.name.Format()}.");
                    return;
                }
                if (Level.isLoaded)
                    Logger.LogDebug($"[{Source}] NetId already set: {old.Format()} @ {(t == null ? null : t.name).Format()}.");
                NetIdRegistry.ReleaseTransform(old, t);
            }
            else if (NetIdRegistry.Release(old))
            {
                if (Level.isLoaded)
                    Logger.LogDebug($"[{Source}] Released old NetId pairing: {old.Format()}.");
            }
        }

        HierarchyItemAssignments[item.instanceID] = netId;
        if (NetIdRegistry.GetTransformNetId(transform, out old, out string path))
        {
            if (old == netId)
            {
                if (Level.isLoaded)
                    Logger.LogDebug($"[{Source}] NetId was already claimed: {old.Format()} @ {path.Format()}.");
                return;
            }
            NetIdRegistry.ReleaseTransform(old, transform);
            if (Level.isLoaded)
                Logger.LogDebug($"[{Source}] Released old transform pairing NetId: {old.Format()} @ {path.Format()}.");
        }
        NetIdRegistry.AssignTransform(netId, transform);
        if (Level.isLoaded)
            Logger.LogDebug($"[{Source}] Claimed new NetId: {netId.Format()} @ {transform.name.Format()}.");
    }
    public static void GatherData(LevelData data)
    {
        NetId[] netIds = new NetId[HierarchyItemAssignments.Count];
        uint[] objects = new uint[HierarchyItemAssignments.Count];
        int index = 0;

        foreach (KeyValuePair<uint, NetId> lvlObject in HierarchyItemAssignments)
        {
            netIds[index] = lvlObject.Value;
            objects[index] = lvlObject.Key;
            ++index;
        }

        data.HierarchyItems = objects;
        data.HierarchyItemNetIds = netIds;
    }
}
