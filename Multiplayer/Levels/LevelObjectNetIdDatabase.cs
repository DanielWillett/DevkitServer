using DevkitServer.Levels;
using DevkitServer.Models;
using DevkitServer.Multiplayer.Networking;

namespace DevkitServer.Multiplayer.Levels;
[EarlyTypeInit]
public static class LevelObjectNetIdDatabase
{
    private const string Source = "OBJECT NET IDS";
    private static readonly Dictionary<RegionIdentifier, NetId> BuildableAssignments = new Dictionary<RegionIdentifier, NetId>(128);
    private static readonly Dictionary<uint, NetId> LevelObjectAssignments = new Dictionary<uint, NetId>(1024);
    [UsedImplicitly]
    internal static NetCall<uint, NetId> SendBindObject = new NetCall<uint, NetId>(NetCalls.SendBindObject);
    internal static void Init()
    {
        LevelObjectUtil.OnBuildableRegionUpdated += OnBuildableRegionUpdated;
        LevelObjectUtil.OnBuildableRemoved += OnBuildableRemoved;
    }
    internal static void Shutdown()
    {
        LevelObjectUtil.OnBuildableRegionUpdated -= OnBuildableRegionUpdated;
        LevelObjectUtil.OnBuildableRemoved -= OnBuildableRemoved;
    }
#if CLIENT
    [NetCall(NetCallSource.FromServer, NetCalls.SendBindObject)]
    private static void ReceiveBindObject(MessageContext ctx, uint instanceId, NetId netId)
    {
        if (LevelObjectUtil.TryFindObject(instanceId, out RegionIdentifier id))
        {
            LevelObject obj = LevelObjectUtil.GetObjectUnsafe(id);
            RegisterObject(obj, netId);
            ctx.Acknowledge(StandardErrorCode.Success);
        }
        else
            ctx.Acknowledge(StandardErrorCode.NotFound);
    }
#endif
    private static void OnBuildableRemoved(LevelBuildableObject buildable, RegionIdentifier id)
    {
        if (!DevkitServerModule.IsEditing)
            return;

        RemoveIdentifier(id, buildable, true);
    }
    private static void OnBuildableRegionUpdated(LevelBuildableObject buildable, RegionIdentifier from, RegionIdentifier to)
    {
        if (!DevkitServerModule.IsEditing)
            return;

        NetId netId = RemoveIdentifier(from, buildable, false);
        if (netId != NetId.INVALID)
            BuildableAssignments[to] = netId;
    }
    private static NetId RemoveIdentifier(RegionIdentifier id, LevelBuildableObject buildable, bool release)
    {
        if (!BuildableAssignments.TryGetValue(id, out NetId originalNetId))
            return NetId.INVALID;
        if (!release)
            BuildableAssignments.Remove(id);
        else
            RemoveBuildable(buildable, id);
        // move down all the indexes above this one
        for (int i = id.Index + 1; i <= ushort.MaxValue; ++i)
        {
            RegionIdentifier laterIndex = new RegionIdentifier(id.X, id.Y, (ushort)i);
            if (!BuildableAssignments.TryGetValue(laterIndex, out NetId netId))
                break;
            BuildableAssignments.Remove(laterIndex);
            BuildableAssignments[new RegionIdentifier(id.X, id.Y, (ushort)(i - 1))] = netId;
        }

        return originalNetId;
    }
    public static void RemoveObject(LevelObject obj)
    {
        if (obj == null)
            return;

        if (LevelObjectAssignments.TryGetValue(obj.instanceID, out NetId netId))
        {
            NetIdRegistry.ReleaseTransform(netId, obj.transform);
            LevelObjectAssignments.Remove(obj.instanceID);
            if (Level.isLoaded)
                Logger.LogDebug($"[{Source}] Released object NetId: {netId.Format()} ({obj.instanceID}, {(obj.asset?.FriendlyName).Format()}, {obj.GUID.Format()})");
        }
        else
            Logger.LogWarning($"Unable to release NetId to object {obj.instanceID.Format()}, {(obj.asset?.FriendlyName).Format()}, {obj.GUID.Format()}, NetId not registered.", method: Source);
    }
    public static NetId AddObject(LevelObject obj)
    {
        if (obj.transform == null)
        {
            if (obj.asset == null)
                Logger.LogWarning($"Unable to assign NetId to object {obj.instanceID.Format()} ({(obj.GUID == Guid.Empty ? obj.id.Format() : obj.GUID.Format())}), asset not found.");
            else
                Logger.LogWarning($"Unable to assign NetId to object {obj.instanceID.Format()} ({(obj.asset?.FriendlyName).Format()} / {obj.GUID.Format()}), transform was null.", method: Source);
            return NetId.INVALID;
        }
        NetId netId = NetIdRegistry.Claim();
        ClaimNetId(LevelObjectAssignments, obj.instanceID, obj.transform, netId);
        return netId;
    }
    public static void RemoveBuildable(RegionIdentifier id) => RemoveBuildable(LevelObjectUtil.GetBuildable(id)!, id);
    internal static void RemoveBuildable(LevelBuildableObject buildable, RegionIdentifier id)
    {
        if (buildable == null)
            return;
        if (BuildableAssignments.TryGetValue(id, out NetId netId))
        {
            NetIdRegistry.ReleaseTransform(netId, buildable.transform);
            BuildableAssignments.Remove(id);
            if (Level.isLoaded)
                Logger.LogDebug($"[{Source}] Released buildable NetId: {netId.Format()} ({id.Format()}, {(buildable.asset?.FriendlyName).Format()}, {(buildable.asset == null ? null : (object)buildable.asset.GUID).Format()})");
        }
        else
            Logger.LogWarning($"Unable to release NetId to buildable {id.Format()}, {(buildable.asset?.FriendlyName).Format()}, {(buildable.asset == null ? null : (object)buildable.asset.GUID).Format()}, NetId not registered.", method: Source);
    }
    public static NetId AddBuildable(RegionIdentifier id) => AddBuildable(LevelObjectUtil.GetBuildable(id)!, id);
    internal static NetId AddBuildable(LevelBuildableObject buildable, RegionIdentifier id)
    {
        if (buildable == null || buildable.transform == null)
        {
            if (buildable != null && buildable.asset == null)
                Logger.LogWarning($"Unable to assign NetId to buildable at {buildable.point.Format("F1")}, asset not found.");
            else
                Logger.LogWarning($"Unable to assign NetId to buildable {(buildable?.asset?.FriendlyName).Format()} / {(buildable?.asset == null ? null : (object)buildable.asset.GUID).Format()}, transform was null.", method: Source);
            return NetId.INVALID;
        }
        NetId netId = NetIdRegistry.Claim();
        ClaimNetId(BuildableAssignments, id, buildable.transform, netId);
        return netId;
    }
    public static bool TryGetObjectOrBuildable(NetId netId, out LevelObject? levelObject, out LevelBuildableObject? buildable, out RegionIdentifier buildableId)
    {
        levelObject = null;
        buildable = null;

        buildableId = RegionIdentifier.Invalid;
        
        if (netId == NetId.INVALID)
            return false;

        Transform t = NetIdRegistry.GetTransform(netId, null);

        if (t == null)
            return false;

        if (LevelObjectUtil.TryFindObject(t, out RegionIdentifier id, false))
            levelObject = LevelObjectUtil.GetObjectUnsafe(id);
        else if (LevelObjectUtil.TryFindBuildable(t, out id))
        {
            buildable = LevelObjectUtil.GetBuildableUnsafe(id);
            buildableId = id;
        }

        return buildable != null || levelObject != null;
    }
    public static bool TryGetObjectNetId(LevelObject obj, out NetId netId) => TryGetObjectNetId(obj.instanceID, out netId);
    public static bool TryGetObjectNetId(uint instanceId, out NetId netId)
    {
        if (!LevelObjectAssignments.TryGetValue(instanceId, out netId))
        {
            netId = NetId.INVALID;
            return false;
        }

        return true;
    }
    public static bool TryGetBuildableNetId(LevelBuildableObject obj, out NetId netId)
    {
        if (!LevelObjectUtil.TryFindBuildable(obj.transform, out RegionIdentifier id))
        {
            netId = NetId.INVALID;
            return false;
        }
        return TryGetBuildableNetId(id, out netId);
    }
    public static bool TryGetBuildableNetId(RegionIdentifier id, out NetId netId)
    {
        if (!BuildableAssignments.TryGetValue(id, out netId))
        {
            netId = NetId.INVALID;
            return false;
        }

        return true;
    }
    internal static void RegisterObject(LevelObject obj, NetId netId) => ClaimNetId(LevelObjectAssignments, obj.instanceID, obj.transform, netId);
    internal static void RegisterBuildable(LevelBuildableObject obj, RegionIdentifier id, NetId netId) => ClaimNetId(BuildableAssignments, id, obj.transform, netId);
#if CLIENT
    public static void LoadFromLevelData()
    {
        LevelData data = EditorLevel.ServerPendingLevelData ?? throw new InvalidOperationException("Level data not loaded.");
        NetId[] netIds = data.LevelObjectNetIds;
        RegionIdentifier[] buildables = data.Buildables;
        uint[] objects = data.Objects;
        for (int i = 0; i < buildables.Length; ++i)
        {
            RegionIdentifier id = buildables[i];
            LevelBuildableObject? buildable = LevelObjectUtil.GetBuildable(id);
            if (buildable == null)
            {
                Logger.LogWarning($"Unable to find buildable in level data info: {id.Format()}.");
                continue;
            }

            RegisterBuildable(buildable, id, netIds[i]);
        }

        int offset = buildables.Length;
        Vector3 last = Vector3.zero;
        bool any = false;
        for (int i = 0; i < objects.Length; ++i)
        {
            uint instanceId = objects[i];
            if (!(any
                    ? LevelObjectUtil.TryFindObject(last, instanceId, out RegionIdentifier id)
                    : LevelObjectUtil.TryFindObject(instanceId, out id)))
            {
                Logger.LogWarning($"Unable to find object in level data info: {instanceId.Format()}.");
                continue;
            }

            LevelObject obj = ObjectManager.getObject(id.X, id.Y, id.Index);
            Transform? t = obj.GetTransform();
            if (t != null)
                last = t.position;
            any = true;
            RegisterObject(obj, netIds[i + offset]);
        }
    }
#endif
#if SERVER
    internal static void AssignExisting()
    {
        if (LevelObjects.buildables == null || LevelObjects.objects == null)
            throw new InvalidOperationException("LevelObjects not loaded.");
        BuildableAssignments.Clear();
        LevelObjectAssignments.Clear();
        int buildables = 0, objects = 0;
        for (int x = 0; x < Regions.WORLD_SIZE; ++x)
        {
            for (int y = 0; y < Regions.WORLD_SIZE; ++y)
            {
                List<LevelBuildableObject> region = LevelObjects.buildables[x, y];
                for (int i = 0; i < region.Count; ++i)
                {
                    AddBuildable(new RegionIdentifier(x, y, i));
                    ++buildables;
                }
            }
        }
        Logger.LogInfo($"[{Source}] Assigned NetIds for {buildables.Format()} buildable{buildables.S()}.");
        for (int x = 0; x < Regions.WORLD_SIZE; ++x)
        {
            for (int y = 0; y < Regions.WORLD_SIZE; ++y)
            {
                List<LevelObject> region = LevelObjects.objects[x, y];
                for (int i = 0; i < region.Count; ++i)
                {
                    AddObject(region[i]);
                    ++objects;
                }
            }
        }
        Logger.LogInfo($"[{Source}] Assigned NetIds for {objects.Format()} object{objects.S()}.");
    }
#endif

    private static void ClaimNetId<T>(Dictionary<T, NetId> registry, T value, Transform transform, NetId netId)
    {
        if (registry.TryGetValue(value, out NetId old))
        {
            Transform? t = NetIdRegistry.GetTransform(netId, null);
            if (t != null)
            {
                if (t == transform)
                {
                    registry[value] = netId;
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

        registry[value] = netId;
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
        ThreadUtil.assertIsGameThread();

        NetId[] netIds = new NetId[BuildableAssignments.Count + LevelObjectAssignments.Count];
        RegionIdentifier[] buildables = BuildableAssignments.Count == 0 ? Array.Empty<RegionIdentifier>() : new RegionIdentifier[BuildableAssignments.Count];
        uint[] objects = new uint[LevelObjectAssignments.Count];
        int index = 0;
        foreach (KeyValuePair<RegionIdentifier, NetId> buildable in BuildableAssignments)
        {
            netIds[index] = buildable.Value;
            buildables[index] = buildable.Key;
            ++index;
        }

        int offset = index;
        index = 0;
        foreach (KeyValuePair<uint, NetId> lvlObject in LevelObjectAssignments)
        {
            netIds[offset + index] = lvlObject.Value;
            objects[index] = lvlObject.Key;
            ++index;
        }

        data.Objects = objects;
        data.Buildables = buildables;
        data.LevelObjectNetIds = netIds;
    }
}
