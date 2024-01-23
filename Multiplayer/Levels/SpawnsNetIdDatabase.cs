#if DEBUG
// #define LEVEL_DATA_LOGGING
#endif
using DevkitServer.API.Devkit.Spawns;
using DevkitServer.Models;
using DevkitServer.Multiplayer.Actions;
using DevkitServer.Multiplayer.Networking;
using DevkitServer.Util.Encoding;
using DevkitServer.Util.Region;

namespace DevkitServer.Multiplayer.Levels;
public sealed class SpawnsNetIdDatabase : IReplicatedLevelDataSource<SpawnsNetIdDatabaseLevelData>
{
    public ushort CurrentDataVersion => 0;

    private const string Source = "SPAWN NET IDS";
    private static readonly Dictionary<int, NetId64> AnimalSpawnAssignments = new Dictionary<int, NetId64>(32);
    private static readonly Dictionary<int, NetId64> PlayerSpawnAssignments = new Dictionary<int, NetId64>(16);
    private static readonly Dictionary<int, NetId64> VehicleSpawnAssignments = new Dictionary<int, NetId64>(128);
    private static readonly Dictionary<RegionIdentifier, NetId64> ItemSpawnAssignments = new Dictionary<RegionIdentifier, NetId64>(512);
    private static readonly Dictionary<RegionIdentifier, NetId64> ZombieSpawnAssignments = new Dictionary<RegionIdentifier, NetId64>(512);
    
    private static readonly Dictionary<SpawnTierIdentifier, NetId64> SpawnTierAssignments = new Dictionary<SpawnTierIdentifier, NetId64>(128);
    private static readonly Dictionary<SpawnAssetIdentifier, NetId64> SpawnAssetAssignments = new Dictionary<SpawnAssetIdentifier, NetId64>(512);

    private static readonly NetId64[] AnimalSpawnTables = new NetId64[byte.MaxValue];
    private static readonly NetId64[] PlayerSpawnTables = new NetId64[byte.MaxValue];
    private static readonly NetId64[] VehicleSpawnTables = new NetId64[byte.MaxValue];
    private static readonly NetId64[] ItemSpawnTables = new NetId64[byte.MaxValue];
    private static readonly NetId64[] ZombieSpawnTables = new NetId64[byte.MaxValue];
    

    private static readonly Dictionary<NetId64, int> SpawnNetIds = new Dictionary<NetId64, int>(1831);

    private static Dictionary<int, NetId64> GetIndexAssignments(SpawnType type) => type switch
    {
        SpawnType.Animal => AnimalSpawnAssignments,
        SpawnType.Player => PlayerSpawnAssignments,
        _ => VehicleSpawnAssignments
    };

    private static Dictionary<RegionIdentifier, NetId64> GetRegionAssignments(SpawnType type) => type switch
    {
        SpawnType.Item => ItemSpawnAssignments,
        _ => ZombieSpawnAssignments
    };

    [UsedImplicitly]
    internal static NetCall<byte, int, NetId64> SendBindSpawnpoint = new NetCall<byte, int, NetId64>(DevkitServerNetCall.SendBindSpawnpoint);
    private SpawnsNetIdDatabase() { }
    internal static void Init()
    {
#if SERVER
        SpawnUtil.OnAnimalSpawnpointRemoved += OnAnimalSpawnRemoved;
        SpawnUtil.OnPlayerSpawnpointRemoved += OnPlayerSpawnRemoved;
        SpawnUtil.OnVehicleSpawnpointRemoved += OnVehicleSpawnRemoved;
        SpawnUtil.OnItemSpawnpointRemoved += OnItemSpawnRemoved;
        SpawnUtil.OnZombieSpawnpointRemoved += OnZombieSpawnRemoved;
        
        SpawnTableUtil.OnAnimalSpawnTableRemoved += OnAnimalSpawnTableRemoved;
        SpawnTableUtil.OnVehicleSpawnTableRemoved += OnVehicleSpawnTableRemoved;
        SpawnTableUtil.OnItemSpawnTableRemoved += OnItemSpawnTableRemoved;
        SpawnTableUtil.OnZombieSpawnTableRemoved += OnZombieSpawnTableRemoved;

        SpawnTableUtil.OnAnimalSpawnTierRemoved += OnAnimalSpawnTierRemoved;
        SpawnTableUtil.OnVehicleSpawnTierRemoved += OnVehicleSpawnTierRemoved;
        SpawnTableUtil.OnItemSpawnTierRemoved += OnItemSpawnTierRemoved;

        SpawnTableUtil.OnAnimalSpawnAssetRemoved += OnAnimalSpawnAssetRemoved;
        SpawnTableUtil.OnVehicleSpawnAssetRemoved += OnVehicleSpawnAssetRemoved;
        SpawnTableUtil.OnItemSpawnAssetRemoved += OnItemSpawnAssetRemoved;
        SpawnTableUtil.OnZombieSpawnAssetRemoved += OnZombieSpawnAssetRemoved;
#endif
        SpawnUtil.OnAnimalSpawnpointIndexUpdated += OnAnimalSpawnIndexUpdated;
        SpawnUtil.OnPlayerSpawnpointIndexUpdated += OnPlayerSpawnIndexUpdated;
        SpawnUtil.OnVehicleSpawnpointIndexUpdated += OnVehicleSpawnIndexUpdated;
        SpawnUtil.OnItemSpawnpointRegionUpdated += OnItemSpawnRegionUpdated;
        SpawnUtil.OnZombieSpawnpointRegionUpdated += OnZombieSpawnRegionUpdated;

        SpawnTableUtil.OnAnimalSpawnTableIndexUpdated += OnAnimalSpawnTableIndexUpdated;
        SpawnTableUtil.OnVehicleSpawnTableIndexUpdated += OnVehicleSpawnTableIndexUpdated;
        SpawnTableUtil.OnItemSpawnTableIndexUpdated += OnItemSpawnTableIndexUpdated;
        SpawnTableUtil.OnZombieSpawnTableIndexUpdated += OnZombieSpawnTableIndexUpdated;

        SpawnTableUtil.OnAnimalSpawnTierIndexUpdated += OnAnimalSpawnTierIndexUpdated;
        SpawnTableUtil.OnVehicleSpawnTierIndexUpdated += OnVehicleSpawnTierIndexUpdated;
        SpawnTableUtil.OnItemSpawnTierIndexUpdated += OnItemSpawnTierIndexUpdated;

        SpawnTableUtil.OnAnimalSpawnAssetIndexUpdated += OnAnimalSpawnAssetIndexUpdated;
        SpawnTableUtil.OnVehicleSpawnAssetIndexUpdated += OnVehicleSpawnAssetIndexUpdated;
        SpawnTableUtil.OnItemSpawnAssetIndexUpdated += OnItemSpawnAssetIndexUpdated;
        SpawnTableUtil.OnZombieSpawnAssetIndexUpdated += OnZombieSpawnAssetIndexUpdated;
    }

    internal static void Shutdown()
    {
#if SERVER
        SpawnUtil.OnAnimalSpawnpointRemoved -= OnAnimalSpawnRemoved;
        SpawnUtil.OnPlayerSpawnpointRemoved -= OnPlayerSpawnRemoved;
        SpawnUtil.OnVehicleSpawnpointRemoved -= OnVehicleSpawnRemoved;
        SpawnUtil.OnItemSpawnpointRemoved -= OnItemSpawnRemoved;
        SpawnUtil.OnZombieSpawnpointRemoved -= OnZombieSpawnRemoved;

        SpawnTableUtil.OnAnimalSpawnTableRemoved -= OnAnimalSpawnTableRemoved;
        SpawnTableUtil.OnVehicleSpawnTableRemoved -= OnVehicleSpawnTableRemoved;
        SpawnTableUtil.OnItemSpawnTableRemoved -= OnItemSpawnTableRemoved;
        SpawnTableUtil.OnZombieSpawnTableRemoved -= OnZombieSpawnTableRemoved;

        SpawnTableUtil.OnAnimalSpawnTierRemoved -= OnAnimalSpawnTierRemoved;
        SpawnTableUtil.OnVehicleSpawnTierRemoved -= OnVehicleSpawnTierRemoved;
        SpawnTableUtil.OnItemSpawnTierRemoved -= OnItemSpawnTierRemoved;

        SpawnTableUtil.OnAnimalSpawnAssetRemoved -= OnAnimalSpawnAssetRemoved;
        SpawnTableUtil.OnVehicleSpawnAssetRemoved -= OnVehicleSpawnAssetRemoved;
        SpawnTableUtil.OnItemSpawnAssetRemoved -= OnItemSpawnAssetRemoved;
        SpawnTableUtil.OnZombieSpawnAssetRemoved -= OnZombieSpawnAssetRemoved;
#endif
        SpawnUtil.OnAnimalSpawnpointIndexUpdated -= OnAnimalSpawnIndexUpdated;
        SpawnUtil.OnPlayerSpawnpointIndexUpdated -= OnPlayerSpawnIndexUpdated;
        SpawnUtil.OnVehicleSpawnpointIndexUpdated -= OnVehicleSpawnIndexUpdated;
        SpawnUtil.OnItemSpawnpointRegionUpdated -= OnItemSpawnRegionUpdated;
        SpawnUtil.OnZombieSpawnpointRegionUpdated -= OnZombieSpawnRegionUpdated;

        SpawnTableUtil.OnAnimalSpawnTableIndexUpdated -= OnAnimalSpawnTableIndexUpdated;
        SpawnTableUtil.OnVehicleSpawnTableIndexUpdated -= OnVehicleSpawnTableIndexUpdated;
        SpawnTableUtil.OnItemSpawnTableIndexUpdated -= OnItemSpawnTableIndexUpdated;
        SpawnTableUtil.OnZombieSpawnTableIndexUpdated -= OnZombieSpawnTableIndexUpdated;

        SpawnTableUtil.OnAnimalSpawnTierIndexUpdated -= OnAnimalSpawnTierIndexUpdated;
        SpawnTableUtil.OnVehicleSpawnTierIndexUpdated -= OnVehicleSpawnTierIndexUpdated;
        SpawnTableUtil.OnItemSpawnTierIndexUpdated -= OnItemSpawnTierIndexUpdated;

        SpawnTableUtil.OnAnimalSpawnAssetIndexUpdated -= OnAnimalSpawnAssetIndexUpdated;
        SpawnTableUtil.OnVehicleSpawnAssetIndexUpdated -= OnVehicleSpawnAssetIndexUpdated;
        SpawnTableUtil.OnItemSpawnAssetIndexUpdated -= OnItemSpawnAssetIndexUpdated;
        SpawnTableUtil.OnZombieSpawnAssetIndexUpdated -= OnZombieSpawnAssetIndexUpdated;
    }
    internal static void Clear()
    {
        AnimalSpawnAssignments.Clear();
        PlayerSpawnAssignments.Clear();
        VehicleSpawnAssignments.Clear();
        ItemSpawnAssignments.Clear();
        ZombieSpawnAssignments.Clear();
        
        SpawnTierAssignments.Clear();
        SpawnAssetAssignments.Clear();
        
        SpawnNetIds.Clear();
        
        Array.Clear(AnimalSpawnTables, 0, byte.MaxValue);
        Array.Clear(ItemSpawnTables, 0, byte.MaxValue);
        Array.Clear(PlayerSpawnTables, 0, byte.MaxValue);
        Array.Clear(VehicleSpawnTables, 0, byte.MaxValue);
        Array.Clear(ZombieSpawnTables, 0, byte.MaxValue);
    }

    private static void OnAnimalSpawnTableIndexUpdated(AnimalTable spawnTable, int fromIndex, int toIndex)
    {
        if (!DevkitServerModule.IsEditing)
            return;

        NetId64 blockingNetId = AnimalSpawnTables[toIndex];
        NetId64 netId = AnimalSpawnTables[fromIndex];

        if (!blockingNetId.IsNull() && blockingNetId == netId || netId.IsNull())
            return;

        if (!blockingNetId.IsNull())
        {
            Logger.DevkitServer.LogDebug(Source, $"Released blocking net id to save animal spawn table: {spawnTable.name.Format()} ({netId.Format()}, # {toIndex.Format()}).");
            SpawnNetIds.Remove(blockingNetId);
        }

        AnimalSpawnTables[fromIndex] = NetId64.Invalid;
        AnimalSpawnTables[toIndex] = netId;
        SpawnNetIds[netId] = toIndex;
        Logger.DevkitServer.LogDebug(Source, $"Moved animal spawn table NetId: {spawnTable.name.Format()} ({netId.Format()}, # {toIndex.Format()}).");
    }
    private static void OnVehicleSpawnTableIndexUpdated(VehicleTable spawnTable, int fromIndex, int toIndex)
    {
        if (!DevkitServerModule.IsEditing)
            return;

        NetId64 blockingNetId = VehicleSpawnTables[toIndex];
        NetId64 netId = VehicleSpawnTables[fromIndex];

        if (!blockingNetId.IsNull() && blockingNetId == netId || netId.IsNull())
            return;

        if (!blockingNetId.IsNull())
        {
            Logger.DevkitServer.LogDebug(Source, $"Released blocking net id to save vehicle spawn table: {spawnTable.name.Format()} ({netId.Format()}, # {toIndex.Format()}).");
            SpawnNetIds.Remove(blockingNetId);
        }

        VehicleSpawnTables[fromIndex] = NetId64.Invalid;
        VehicleSpawnTables[toIndex] = netId;
        SpawnNetIds[netId] = toIndex;
        Logger.DevkitServer.LogDebug(Source, $"Moved vehicle spawn table NetId: {spawnTable.name.Format()} ({netId.Format()}, # {toIndex.Format()}).");
    }
    private static void OnItemSpawnTableIndexUpdated(ItemTable spawnTable, int fromIndex, int toIndex)
    {
        if (!DevkitServerModule.IsEditing)
            return;

        NetId64 blockingNetId = ItemSpawnTables[toIndex];
        NetId64 netId = ItemSpawnTables[fromIndex];

        if (!blockingNetId.IsNull() && blockingNetId == netId || netId.IsNull())
            return;

        if (!blockingNetId.IsNull())
        {
            Logger.DevkitServer.LogDebug(Source, $"Released blocking net id to save item spawn table: {spawnTable.name.Format()} ({netId.Format()}, # {toIndex.Format()}).");
            SpawnNetIds.Remove(blockingNetId);
        }

        ItemSpawnTables[fromIndex] = NetId64.Invalid;
        ItemSpawnTables[toIndex] = netId;
        SpawnNetIds[netId] = toIndex;
        Logger.DevkitServer.LogDebug(Source, $"Moved item spawn table NetId: {spawnTable.name.Format()} ({netId.Format()}, # {toIndex.Format()}).");
    }
    private static void OnZombieSpawnTableIndexUpdated(ZombieTable spawnTable, int fromIndex, int toIndex)
    {
        if (!DevkitServerModule.IsEditing)
            return;

        NetId64 blockingNetId = ZombieSpawnTables[toIndex];
        NetId64 netId = ZombieSpawnTables[fromIndex];

        if (!blockingNetId.IsNull() && blockingNetId == netId || netId.IsNull())
            return;

        if (!blockingNetId.IsNull())
        {
            Logger.DevkitServer.LogDebug(Source, $"Released blocking net id to save zombie spawn table: {spawnTable.name.Format()} ({netId.Format()}, # {toIndex.Format()}).");
            SpawnNetIds.Remove(blockingNetId);
        }

        ZombieSpawnTables[fromIndex] = NetId64.Invalid;
        ZombieSpawnTables[toIndex] = netId;
        SpawnNetIds[netId] = toIndex;
        Logger.DevkitServer.LogDebug(Source, $"Moved zombie spawn table NetId: {spawnTable.name.Format()} ({netId.Format()}, # {toIndex.Format()}).");
    }
    private static void OnSpawnTierIndexUpdated(SpawnTierIdentifier fromIndex, SpawnTierIdentifier toIndex)
    {
        if (!DevkitServerModule.IsEditing)
            return;

        SpawnTierAssignments.TryGetValue(toIndex, out NetId64 blockingNetId);
        SpawnTierAssignments.TryGetValue(fromIndex, out NetId64 netId);
        if (!blockingNetId.IsNull() && blockingNetId == netId || netId.IsNull())
            return;

        if (!blockingNetId.IsNull())
        {
            Logger.DevkitServer.LogDebug(Source, $"Released blocking net id to save {toIndex.Type.ToString().ToLowerInvariant()} spawn tier: ({netId.Format()}, {toIndex.Format()}).");
            SpawnNetIds.Remove(blockingNetId);
        }

        SpawnTierAssignments.Remove(fromIndex);
        SpawnTierAssignments[toIndex] = netId;
        SpawnNetIds[netId] = toIndex.Raw;
        Logger.DevkitServer.LogDebug(Source, $"Moved {toIndex.Type.ToString().ToLowerInvariant()} spawn tier NetId: ({netId.Format()}, {toIndex.Format()}).");
    }
    private static void OnSpawnAssetIndexUpdated(SpawnAssetIdentifier fromIndex, SpawnAssetIdentifier toIndex)
    {
        if (!DevkitServerModule.IsEditing)
            return;

        SpawnAssetAssignments.TryGetValue(toIndex, out NetId64 blockingNetId);
        SpawnAssetAssignments.TryGetValue(fromIndex, out NetId64 netId);
        if (!blockingNetId.IsNull() && blockingNetId == netId || netId.IsNull())
            return;

        if (!blockingNetId.IsNull())
        {
            Logger.DevkitServer.LogDebug(Source, $"Released blocking net id to save {toIndex.Type.ToString().ToLowerInvariant()} spawn asset: ({netId.Format()}, {toIndex.Format()}).");
            SpawnNetIds.Remove(blockingNetId);
        }

        SpawnAssetAssignments.Remove(fromIndex);
        SpawnAssetAssignments[toIndex] = netId;
        SpawnNetIds[netId] = toIndex.Raw;
        Logger.DevkitServer.LogDebug(Source, $"Moved {toIndex.Type.ToString().ToLowerInvariant()} spawn asset NetId: ({netId.Format()}, {toIndex.Format()}).");
    }
    private static void OnAnimalSpawnTierIndexUpdated(AnimalTier spawnTier, SpawnTierIdentifier fromIndex, SpawnTierIdentifier toIndex, HierarchicalEventSource eventSource)
    {
        OnSpawnTierIndexUpdated(fromIndex, toIndex);
    }
    private static void OnVehicleSpawnTierIndexUpdated(VehicleTier spawnTier, SpawnTierIdentifier fromIndex, SpawnTierIdentifier toIndex, HierarchicalEventSource eventSource)
    {
        OnSpawnTierIndexUpdated(fromIndex, toIndex);
    }
    private static void OnItemSpawnTierIndexUpdated(ItemTier spawnTier, SpawnTierIdentifier fromIndex, SpawnTierIdentifier toIndex, HierarchicalEventSource eventSource)
    {
        OnSpawnTierIndexUpdated(fromIndex, toIndex);
    }
    private static void OnAnimalSpawnAssetIndexUpdated(AnimalSpawn spawnAsset, SpawnAssetIdentifier fromIndex, SpawnAssetIdentifier toIndex, HierarchicalEventSource eventSource)
    {
        OnSpawnAssetIndexUpdated(fromIndex, toIndex);
    }
    private static void OnVehicleSpawnAssetIndexUpdated(VehicleSpawn spawnAsset, SpawnAssetIdentifier fromIndex, SpawnAssetIdentifier toIndex, HierarchicalEventSource eventSource)
    {
        OnSpawnAssetIndexUpdated(fromIndex, toIndex);
    }
    private static void OnItemSpawnAssetIndexUpdated(ItemSpawn spawnAsset, SpawnAssetIdentifier fromIndex, SpawnAssetIdentifier toIndex, HierarchicalEventSource eventSource)
    {
        OnSpawnAssetIndexUpdated(fromIndex, toIndex);
    }
    private static void OnZombieSpawnAssetIndexUpdated(ZombieCloth spawnAsset, SpawnAssetIdentifier fromIndex, SpawnAssetIdentifier toIndex, HierarchicalEventSource eventSource)
    {
        OnSpawnAssetIndexUpdated(fromIndex, toIndex);
    }
    private static void OnAnimalSpawnIndexUpdated(AnimalSpawnpoint point, int fromIndex, int toIndex)
    {
        if (!DevkitServerModule.IsEditing)
            return;

        AnimalSpawnAssignments.TryGetValue(toIndex, out NetId64 blockingNetId);
        AnimalSpawnAssignments.TryGetValue(fromIndex, out NetId64 netId);
        if (!blockingNetId.IsNull() && blockingNetId == netId || netId.IsNull())
            return;

        if (!blockingNetId.IsNull())
        {
            Logger.DevkitServer.LogDebug(Source, $"Released blocking net id to save animal spawnpoint: {point.point.Format()} ({netId.Format()}, # {toIndex.Format()}).");
            SpawnNetIds.Remove(blockingNetId);
        }

        AnimalSpawnAssignments.Remove(fromIndex);
        AnimalSpawnAssignments[toIndex] = netId;
        SpawnNetIds[netId] = toIndex;
        Logger.DevkitServer.LogDebug(Source, $"Moved animal spawnpoint NetId: {point.point.Format()} ({netId.Format()}, # {toIndex.Format()}).");
    }
    private static void OnPlayerSpawnIndexUpdated(PlayerSpawnpoint point, int fromIndex, int toIndex)
    {
        if (!DevkitServerModule.IsEditing)
            return;

        PlayerSpawnAssignments.TryGetValue(toIndex, out NetId64 blockingNetId);
        PlayerSpawnAssignments.TryGetValue(fromIndex, out NetId64 netId);
        if (!blockingNetId.IsNull() && blockingNetId == netId || netId.IsNull())
            return;

        if (!blockingNetId.IsNull())
        {
            Logger.DevkitServer.LogDebug(Source, $"Released blocking net id to save player spawnpoint: {point.point.Format()} ({netId.Format()}, # {toIndex.Format()}).");
            SpawnNetIds.Remove(blockingNetId);
        }

        PlayerSpawnAssignments.Remove(fromIndex);
        PlayerSpawnAssignments[toIndex] = netId;
        SpawnNetIds[netId] = toIndex;
        Logger.DevkitServer.LogDebug(Source, $"Moved player spawnpoint NetId: {point.point.Format()} ({netId.Format()}, # {toIndex.Format()}).");
    }
    private static void OnVehicleSpawnIndexUpdated(VehicleSpawnpoint point, int fromIndex, int toIndex)
    {
        if (!DevkitServerModule.IsEditing)
            return;

        VehicleSpawnAssignments.TryGetValue(toIndex, out NetId64 blockingNetId);
        VehicleSpawnAssignments.TryGetValue(fromIndex, out NetId64 netId);
        if (!blockingNetId.IsNull() && blockingNetId == netId || netId.IsNull())
            return;

        if (!blockingNetId.IsNull())
        {
            Logger.DevkitServer.LogDebug(Source, $"Released blocking net id to save vehicle spawnpoint: {point.point.Format()} ({netId.Format()}, # {toIndex.Format()}).");
            SpawnNetIds.Remove(blockingNetId);
        }

        VehicleSpawnAssignments.Remove(fromIndex);
        VehicleSpawnAssignments[toIndex] = netId;
        SpawnNetIds[netId] = toIndex;
        Logger.DevkitServer.LogDebug(Source, $"Moved vehicle spawnpoint NetId: {point.point.Format()} ({netId.Format()}, # {toIndex.Format()}).");
    }
    private static void OnItemSpawnRegionUpdated(ItemSpawnpoint point, RegionIdentifier fromRegion, RegionIdentifier toRegion)
    {
        if (!DevkitServerModule.IsEditing)
            return;

        ItemSpawnAssignments.TryGetValue(toRegion, out NetId64 blockingNetId);
        ItemSpawnAssignments.TryGetValue(fromRegion, out NetId64 netId);
        if (!blockingNetId.IsNull() && blockingNetId == netId || netId.IsNull())
            return;

        if (!blockingNetId.IsNull())
        {
            Logger.DevkitServer.LogDebug(Source, $"Released blocking net id to save item spawnpoint: {point.point.Format()} ({netId.Format()}, {toRegion.Format()}).");
            SpawnNetIds.Remove(blockingNetId);
        }

        ItemSpawnAssignments.Remove(fromRegion);
        ItemSpawnAssignments[toRegion] = netId;
        SpawnNetIds[netId] = toRegion.Raw;
        Logger.DevkitServer.LogDebug(Source, $"Moved item spawnpoint NetId: {point.point.Format()} ({netId.Format()}, {toRegion.Format()}).");
    }
    private static void OnZombieSpawnRegionUpdated(ZombieSpawnpoint point, RegionIdentifier fromRegion, RegionIdentifier toRegion)
    {
        if (!DevkitServerModule.IsEditing)
            return;

        ZombieSpawnAssignments.TryGetValue(toRegion, out NetId64 blockingNetId);
        ZombieSpawnAssignments.TryGetValue(fromRegion, out NetId64 netId);
        if (!blockingNetId.IsNull() && blockingNetId == netId || netId.IsNull())
            return;

        if (!blockingNetId.IsNull())
        {
            Logger.DevkitServer.LogDebug(Source, $"Released blocking net id to save zombie spawnpoint: {point.point.Format()} ({netId.Format()}, {toRegion.Format()}).");
            SpawnNetIds.Remove(blockingNetId);
        }

        ZombieSpawnAssignments.Remove(fromRegion);
        ZombieSpawnAssignments[toRegion] = netId;
        SpawnNetIds[netId] = toRegion.Raw;
        Logger.DevkitServer.LogDebug(Source, $"Moved zombie spawnpoint NetId: {point.point.Format()} ({netId.Format()}, {toRegion.Format()}).");
    }
#if SERVER
    private static void OnAnimalSpawnTableRemoved(AnimalTable spawnTable, int index)
    {
        if (!DevkitServerModule.IsEditing)
            return;

        NetId64 netId = AnimalSpawnTables[index];
        if (netId.IsNull())
            return;

        AnimalSpawnTables[index] = NetId64.Invalid;
        SpawnNetIds.Remove(netId);
        Logger.DevkitServer.LogDebug(Source, $"Removed animal spawn table NetId: {spawnTable.name.Format()} ({netId.Format()}, # {index.Format()}).");
    }
    private static void OnVehicleSpawnTableRemoved(VehicleTable spawnTable, int index)
    {
        if (!DevkitServerModule.IsEditing)
            return;

        NetId64 netId = VehicleSpawnTables[index];
        if (netId.IsNull())
            return;

        VehicleSpawnTables[index] = NetId64.Invalid;
        SpawnNetIds.Remove(netId);
        Logger.DevkitServer.LogDebug(Source, $"Removed vehicle spawn table NetId: {spawnTable.name.Format()} ({netId.Format()}, # {index.Format()}).");
    }
    private static void OnItemSpawnTableRemoved(ItemTable spawnTable, int index)
    {
        if (!DevkitServerModule.IsEditing)
            return;

        NetId64 netId = ItemSpawnTables[index];
        if (netId.IsNull())
            return;

        ItemSpawnTables[index] = NetId64.Invalid;
        SpawnNetIds.Remove(netId);
        Logger.DevkitServer.LogDebug(Source, $"Removed item spawn table NetId: {spawnTable.name.Format()} ({netId.Format()}, # {index.Format()}).");
    }
    private static void OnZombieSpawnTableRemoved(ZombieTable spawnTable, int index)
    {
        if (!DevkitServerModule.IsEditing)
            return;

        NetId64 netId = ZombieSpawnTables[index];
        if (netId.IsNull())
            return;

        ZombieSpawnTables[index] = NetId64.Invalid;
        SpawnNetIds.Remove(netId);
        Logger.DevkitServer.LogDebug(Source, $"Removed zombie spawn table NetId: {spawnTable.name.Format()} ({netId.Format()}, # {index.Format()}).");
    }
    private static void OnSpawnTierRemoved(SpawnTierIdentifier identifier)
    {
        if (!DevkitServerModule.IsEditing)
            return;

        SpawnTierAssignments.TryGetValue(identifier, out NetId64 netId);
        if (netId.IsNull())
            return;

        SpawnTierAssignments.Remove(identifier);
        SpawnNetIds.Remove(netId);
        Logger.DevkitServer.LogDebug(Source, $"Removed {identifier.Type.ToString().ToLowerInvariant()} spawn tier NetId: ({netId.Format()}, # {identifier.Format()}).");
    }
    private static void OnSpawnAssetRemoved(SpawnAssetIdentifier identifier)
    {
        if (!DevkitServerModule.IsEditing)
            return;

        SpawnAssetAssignments.TryGetValue(identifier, out NetId64 netId);
        if (netId.IsNull())
            return;

        SpawnAssetAssignments.Remove(identifier);
        SpawnNetIds.Remove(netId);
        Logger.DevkitServer.LogDebug(Source, $"Removed {identifier.Type.ToString().ToLowerInvariant()} spawn asset NetId: ({netId.Format()}, # {identifier.Format()}).");
    }
    private static void OnAnimalSpawnTierRemoved(AnimalTier spawnTier, SpawnTierIdentifier identifier, HierarchicalEventSource eventSource)
    {
        OnSpawnTierRemoved(identifier);
    }
    private static void OnVehicleSpawnTierRemoved(VehicleTier spawnTier, SpawnTierIdentifier identifier, HierarchicalEventSource eventSource)
    {
        OnSpawnTierRemoved(identifier);
    }
    private static void OnItemSpawnTierRemoved(ItemTier spawnTier, SpawnTierIdentifier identifier, HierarchicalEventSource eventSource)
    {
        OnSpawnTierRemoved(identifier);
    }
    private static void OnAnimalSpawnAssetRemoved(AnimalSpawn spawnAsset, SpawnAssetIdentifier identifier, HierarchicalEventSource eventSource)
    {
        OnSpawnAssetRemoved(identifier);
    }
    private static void OnVehicleSpawnAssetRemoved(VehicleSpawn spawnAsset, SpawnAssetIdentifier identifier, HierarchicalEventSource eventSource)
    {
        OnSpawnAssetRemoved(identifier);
    }
    private static void OnItemSpawnAssetRemoved(ItemSpawn spawnAsset, SpawnAssetIdentifier identifier, HierarchicalEventSource eventSource)
    {
        OnSpawnAssetRemoved(identifier);
    }
    private static void OnZombieSpawnAssetRemoved(ZombieCloth spawnAsset, SpawnAssetIdentifier identifier, HierarchicalEventSource eventSource)
    {
        OnSpawnAssetRemoved(identifier);
    }
    private static void OnAnimalSpawnRemoved(AnimalSpawnpoint point, int index)
    {
        if (!DevkitServerModule.IsEditing)
            return;

        AnimalSpawnAssignments.TryGetValue(index, out NetId64 netId);
        if (netId.IsNull())
            return;

        AnimalSpawnAssignments.Remove(index);
        SpawnNetIds.Remove(netId);
        Logger.DevkitServer.LogDebug(Source, $"Removed animal spawnpoint NetId: {point.point.Format()} ({netId.Format()}, # {index.Format()}).");
    }
    private static void OnPlayerSpawnRemoved(PlayerSpawnpoint point, int index)
    {
        if (!DevkitServerModule.IsEditing)
            return;

        PlayerSpawnAssignments.TryGetValue(index, out NetId64 netId);
        if (netId.IsNull())
            return;

        PlayerSpawnAssignments.Remove(index);
        SpawnNetIds.Remove(netId);
        Logger.DevkitServer.LogDebug(Source, $"Removed player spawnpoint NetId: {point.point.Format()} ({netId.Format()}, # {index.Format()}).");
    }
    private static void OnVehicleSpawnRemoved(VehicleSpawnpoint point, int index)
    {
        if (!DevkitServerModule.IsEditing)
            return;

        VehicleSpawnAssignments.TryGetValue(index, out NetId64 netId);
        if (netId.IsNull())
            return;

        VehicleSpawnAssignments.Remove(index);
        SpawnNetIds.Remove(netId);
        Logger.DevkitServer.LogDebug(Source, $"Removed vehicle spawnpoint NetId: {point.point.Format()} ({netId.Format()}, # {index.Format()}).");
    }
    private static void OnItemSpawnRemoved(ItemSpawnpoint point, RegionIdentifier region)
    {
        if (!DevkitServerModule.IsEditing)
            return;

        ItemSpawnAssignments.TryGetValue(region, out NetId64 netId);
        if (netId.IsNull())
            return;

        ItemSpawnAssignments.Remove(region);
        SpawnNetIds.Remove(netId);
        Logger.DevkitServer.LogDebug(Source, $"Removed item spawnpoint NetId: {point.point.Format()} ({netId.Format()}, {region.Format()}).");
    }
    private static void OnZombieSpawnRemoved(ZombieSpawnpoint point, RegionIdentifier region)
    {
        if (!DevkitServerModule.IsEditing)
            return;

        ZombieSpawnAssignments.TryGetValue(region, out NetId64 netId);
        if (netId.IsNull())
            return;

        ZombieSpawnAssignments.Remove(region);
        SpawnNetIds.Remove(netId);
        Logger.DevkitServer.LogDebug(Source, $"Removed zombie spawnpoint NetId: {point.point.Format()} ({netId.Format()}, {region.Format()}).");
    }
#endif
    
#if CLIENT
    [NetCall(NetCallSource.FromServer, DevkitServerNetCall.SendBindSpawnpoint)]
    private static StandardErrorCode ReceiveBindSpawnpoint(MessageContext ctx, byte spawnType, int indexOrIdentifier, NetId64 netId)
    {
        if (spawnType > 15)
        {
            SpawnType type = (SpawnType)(spawnType - 15);
            if (type is not SpawnType.Animal and not SpawnType.Vehicle and not SpawnType.Item and not SpawnType.Zombie)
                return StandardErrorCode.InvalidData;

            SpawnAssetIdentifier id = new SpawnAssetIdentifier(indexOrIdentifier);
            if (!id.CheckSafe())
                return StandardErrorCode.NotFound;

            ClaimBasicAssetNetId(id, netId);
            return StandardErrorCode.Success;
        }
        if (spawnType > 10)
        {
            SpawnType type = (SpawnType)(spawnType - 10);
            if (type is not SpawnType.Animal and not SpawnType.Vehicle and not SpawnType.Item and not SpawnType.Zombie)
                return StandardErrorCode.InvalidData;
            
            SpawnTierIdentifier id = new SpawnTierIdentifier(indexOrIdentifier);
            if (!id.CheckSafe())
                return StandardErrorCode.NotFound;

            ClaimBasicTierNetId(id, netId);
            return StandardErrorCode.Success;
        }
        if (spawnType > 5)
        {
            SpawnType type = (SpawnType)(spawnType - 5);
            if (type is not SpawnType.Animal and not SpawnType.Vehicle and not SpawnType.Item and not SpawnType.Zombie)
                return StandardErrorCode.InvalidData;

            switch (type)
            {
                case SpawnType.Animal:
                    if (LevelAnimals.tables.Count <= indexOrIdentifier)
                        return StandardErrorCode.NotFound;
                    break;

                case SpawnType.Vehicle:
                    if (LevelVehicles.tables.Count <= indexOrIdentifier)
                        return StandardErrorCode.NotFound;
                    break;

                case SpawnType.Item:
                    if (LevelItems.tables.Count <= indexOrIdentifier)
                        return StandardErrorCode.NotFound;
                    break;

                case SpawnType.Zombie:
                    if (LevelZombies.tables.Count <= indexOrIdentifier)
                        return StandardErrorCode.NotFound;
                    break;
            }

            ClaimBasicTableNetId(indexOrIdentifier, type, netId);
            return StandardErrorCode.Success;
        }
        else
        {
            SpawnType type = (SpawnType)spawnType;
            if (type is not SpawnType.Animal and not SpawnType.Player and not SpawnType.Vehicle and not SpawnType.Item and not SpawnType.Zombie)
                return StandardErrorCode.InvalidData;

            switch (type)
            {
                case SpawnType.Animal:
                    if (LevelAnimals.spawns.Count <= indexOrIdentifier)
                        return StandardErrorCode.NotFound;
                    break;

                case SpawnType.Player:
                    if (LevelPlayers.spawns.Count <= indexOrIdentifier)
                        return StandardErrorCode.NotFound;
                    break;

                default:
                    if (LevelVehicles.spawns.Count <= indexOrIdentifier)
                        return StandardErrorCode.NotFound;
                    break;

                case SpawnType.Item:
                    RegionIdentifier id = RegionIdentifier.CreateUnsafe(indexOrIdentifier);
                    if (id.IsInvalid)
                        return StandardErrorCode.InvalidData;
                    List<ItemSpawnpoint> items = LevelItems.spawns[id.X, id.Y];
                    if (items.Count <= id.Index)
                        return StandardErrorCode.NotFound;

                    ClaimBasicSpawnpointNetId(id, type, netId);
                    return StandardErrorCode.Success;

                case SpawnType.Zombie:
                    id = RegionIdentifier.CreateUnsafe(indexOrIdentifier);
                    if (id.IsInvalid)
                        return StandardErrorCode.InvalidData;
                    List<ZombieSpawnpoint> zombies = LevelZombies.spawns[id.X, id.Y];
                    if (zombies.Count <= id.Index)
                        return StandardErrorCode.NotFound;

                    ClaimBasicSpawnpointNetId(id, type, netId);
                    return StandardErrorCode.Success;
            }

            ClaimBasicSpawnpointNetId(indexOrIdentifier, type, netId);
            return StandardErrorCode.Success;
        }
    }
#endif
    public static bool RemoveSpawnTable(SpawnType type, int index)
    {
        ThreadUtil.assertIsGameThread();
        
        if (index is < 0 or > byte.MaxValue || type is not SpawnType.Animal and not SpawnType.Vehicle and not SpawnType.Item and not SpawnType.Zombie)
            return false;

        switch (type)
        {
            case SpawnType.Animal:
                NetId64 netId = AnimalSpawnTables[index];
                AnimalSpawnTables[index] = NetId64.Invalid;
                SpawnNetIds.Remove(netId);
                return !netId.IsNull();
            case SpawnType.Vehicle:
                netId = VehicleSpawnTables[index];
                VehicleSpawnTables[index] = NetId64.Invalid;
                SpawnNetIds.Remove(netId);
                return !netId.IsNull();
            case SpawnType.Item:
                netId = ItemSpawnTables[index];
                ItemSpawnTables[index] = NetId64.Invalid;
                SpawnNetIds.Remove(netId);
                return !netId.IsNull();
            case SpawnType.Zombie:
                netId = ZombieSpawnTables[index];
                ZombieSpawnTables[index] = NetId64.Invalid;
                SpawnNetIds.Remove(netId);
                return !netId.IsNull();
        }

        return false;
    }
    public static void RemoveIndexSpawnpoint(SpawnType type, int index)
    {
        ThreadUtil.assertIsGameThread();

        if (type is not SpawnType.Animal and not SpawnType.Player and not SpawnType.Vehicle)
            return;

        Dictionary<int, NetId64> assignments = GetIndexAssignments(type);

        if (assignments.Remove(index, out NetId64 netId))
        {
            SpawnNetIds.Remove(netId);
#if !LEVEL_DATA_LOGGING
            if (Level.isLoaded)
#endif
                Logger.DevkitServer.LogDebug(Source, $"Released {type.ToString().ToLower()} spawn NetId: {netId.Format()} ({type.Format()}, # {index.Format()}).");
        }
        else
            Logger.DevkitServer.LogWarning(Source, $"Unable to release NetId to {type.ToString().ToLower()} spawn {netId.Format()} ({type.Format()}, # {index.Format()}), NetId not registered.");
    }
    public static void RemoveRegionSpawnpoint(SpawnType type, RegionIdentifier id)
    {
        ThreadUtil.assertIsGameThread();

        if (type is not SpawnType.Item and not SpawnType.Zombie)
            return;

        Dictionary<RegionIdentifier, NetId64> assignments = GetRegionAssignments(type);

        if (assignments.Remove(id, out NetId64 netId))
        {
            SpawnNetIds.Remove(netId);
#if !LEVEL_DATA_LOGGING
            if (Level.isLoaded)
#endif
                Logger.DevkitServer.LogDebug(Source, $"Released {type.ToString().ToLower()} spawn NetId: {netId.Format()} ({type.Format()}, {id.Format()}).");
        }
        else
            Logger.DevkitServer.LogWarning(Source, $"Unable to release NetId to {type.ToString().ToLower()} spawn {netId.Format()} ({type.Format()}, {id.Format()}), NetId not registered.");
    }
    public static NetId64 AddIndexSpawnpoint(SpawnType type, int index)
    {
        ThreadUtil.assertIsGameThread();

        if (type is not SpawnType.Animal and not SpawnType.Player and not SpawnType.Vehicle)
            return NetId64.Invalid;
        
        NetId64 netId = NetId64Registry.GetUniqueId();

        ClaimBasicSpawnpointNetId(index, type, netId);

        return netId;
    }
    public static NetId64 AddRegionSpawnpoint(SpawnType type, RegionIdentifier id)
    {
        ThreadUtil.assertIsGameThread();

        if (type is not SpawnType.Item and not SpawnType.Zombie)
            return NetId64.Invalid;

        NetId64 netId = NetId64Registry.GetUniqueId();

        ClaimBasicSpawnpointNetId(id, type, netId);

        return netId;
    }
    public static NetId64 AddSpawnTable(SpawnType type, int index)
    {
        ThreadUtil.assertIsGameThread();

        if (type is not SpawnType.Animal and not SpawnType.Vehicle and not SpawnType.Item and not SpawnType.Zombie)
            return NetId64.Invalid;

        NetId64 netId = NetId64Registry.GetUniqueId();

        ClaimBasicTableNetId(index, type, netId);

        return netId;
    }
    public static NetId64 AddSpawnTier(SpawnTierIdentifier identifier)
    {
        ThreadUtil.assertIsGameThread();

        if (identifier.Type is not SpawnType.Animal and not SpawnType.Vehicle and not SpawnType.Item)
            return NetId64.Invalid;

        NetId64 netId = NetId64Registry.GetUniqueId();

        ClaimBasicTierNetId(identifier, netId);

        return netId;
    }
    public static NetId64 AddSpawnAsset(SpawnAssetIdentifier identifier)
    {
        ThreadUtil.assertIsGameThread();

        if (identifier.Type is not SpawnType.Animal and not SpawnType.Vehicle and not SpawnType.Item and not SpawnType.Zombie)
            return NetId64.Invalid;

        NetId64 netId = NetId64Registry.GetUniqueId();

        ClaimBasicAssetNetId(identifier, netId);

        return netId;
    }
    public static NetId64 RegisterIndexSpawnpoint(SpawnType type, int index)
    {
        ThreadUtil.assertIsGameThread();

        if (type is not SpawnType.Animal and not SpawnType.Player and not SpawnType.Vehicle)
            return NetId64.Invalid;
        
        NetId64 netId = NetId64Registry.GetUniqueId();

        ClaimBasicSpawnpointNetId(index, type, netId);

        return netId;
    }
    public static NetId64 RegisterRegionSpawnpoint(SpawnType type, RegionIdentifier id)
    {
        ThreadUtil.assertIsGameThread();

        if (type is not SpawnType.Item and not SpawnType.Zombie)
            return NetId64.Invalid;

        NetId64 netId = NetId64Registry.GetUniqueId();

        ClaimBasicSpawnpointNetId(id, type, netId);

        return netId;
    }
    public static NetId64 RegisterSpawnTable(SpawnType type, int index, NetId64 netId)
    {
        ThreadUtil.assertIsGameThread();

        if (type is not SpawnType.Animal and not SpawnType.Vehicle and not SpawnType.Item and not SpawnType.Zombie)
            return NetId64.Invalid;

        ClaimBasicTableNetId(index, type, netId);

        return netId;
    }
    public static NetId64 RegisterSpawnTier(SpawnTierIdentifier identifier, NetId64 netId)
    {
        ThreadUtil.assertIsGameThread();

        if (identifier.Type is not SpawnType.Animal and not SpawnType.Vehicle and not SpawnType.Item)
            return NetId64.Invalid;

        ClaimBasicTierNetId(identifier, netId);

        return netId;
    }
    public static NetId64 RegisterSpawnAsset(SpawnAssetIdentifier identifier, NetId64 netId)
    {
        ThreadUtil.assertIsGameThread();

        if (identifier.Type is not SpawnType.Animal and not SpawnType.Vehicle and not SpawnType.Item and not SpawnType.Zombie)
            return NetId64.Invalid;

        ClaimBasicAssetNetId(identifier, netId);

        return netId;
    }
    public static bool TryGetAnimalSpawnpoint(NetId64 netId, out int spawnIndex)
    {
        ThreadUtil.assertIsGameThread();

        if (SpawnNetIds.TryGetValue(netId, out int index) && LevelAnimals.spawns.Count > index)
        {
            spawnIndex = index;
            return true;
        }

        spawnIndex = -1;
        return false;
    }
    public static bool TryGetAnimalSpawnpoint(NetId64 netId, out AnimalSpawnpoint spawnpoint)
    {
        ThreadUtil.assertIsGameThread();

        if (SpawnNetIds.TryGetValue(netId, out int index) && LevelAnimals.spawns.Count > index)
        {
            spawnpoint = LevelAnimals.spawns[index];
            return true;
        }

        spawnpoint = null!;
        return false;
    }
    public static bool TryGetPlayerSpawnpoint(NetId64 netId, out int spawnIndex)
    {
        ThreadUtil.assertIsGameThread();

        if (SpawnNetIds.TryGetValue(netId, out int index) && LevelPlayers.spawns.Count > index)
        {
            spawnIndex = index;
            return true;
        }

        spawnIndex = -1;
        return false;
    }
    public static bool TryGetPlayerSpawnpoint(NetId64 netId, out PlayerSpawnpoint spawnpoint)
    {
        ThreadUtil.assertIsGameThread();

        if (SpawnNetIds.TryGetValue(netId, out int index) && LevelPlayers.spawns.Count > index)
        {
            spawnpoint = LevelPlayers.spawns[index];
            return true;
        }

        spawnpoint = null!;
        return false;
    }
    public static bool TryGetVehicleSpawnpoint(NetId64 netId, out int spawnIndex)
    {
        ThreadUtil.assertIsGameThread();

        if (SpawnNetIds.TryGetValue(netId, out int index) && LevelVehicles.spawns.Count > index)
        {
            spawnIndex = index;
            return true;
        }

        spawnIndex = -1;
        return false;
    }
    public static bool TryGetVehicleSpawnpoint(NetId64 netId, out VehicleSpawnpoint spawnpoint)
    {
        ThreadUtil.assertIsGameThread();

        if (SpawnNetIds.TryGetValue(netId, out int index) && LevelVehicles.spawns.Count > index)
        {
            spawnpoint = LevelVehicles.spawns[index];
            return true;
        }

        spawnpoint = null!;
        return false;
    }
    public static bool TryGetItemSpawnpoint(NetId64 netId, out ItemSpawnpoint spawnpoint)
    {
        ThreadUtil.assertIsGameThread();

        if (SpawnNetIds.TryGetValue(netId, out int raw))
        {
            RegionIdentifier id = RegionIdentifier.CreateUnsafe(raw);
            if (id.CheckSafe())
            {
                List<ItemSpawnpoint> region = LevelItems.spawns[id.X, id.Y];
                if (region.Count > id.Index)
                {
                    spawnpoint = region[id.Index];
                    return true;
                }
            }
        }

        spawnpoint = null!;
        return false;
    }
    public static bool TryGetItemSpawnpoint(NetId64 netId, out RegionIdentifier regionIdentifier)
    {
        ThreadUtil.assertIsGameThread();

        if (SpawnNetIds.TryGetValue(netId, out int raw))
        {
            RegionIdentifier id = RegionIdentifier.CreateUnsafe(raw);
            if (id.CheckSafe() && LevelItems.spawns[id.X, id.Y].Count > id.Index)
            {
                regionIdentifier = id;
                return true;
            }
        }

        regionIdentifier = RegionIdentifier.Invalid;
        return false;
    }
    public static bool TryGetZombieSpawnpoint(NetId64 netId, out ZombieSpawnpoint spawnpoint)
    {
        ThreadUtil.assertIsGameThread();

        if (SpawnNetIds.TryGetValue(netId, out int raw))
        {
            RegionIdentifier id = RegionIdentifier.CreateUnsafe(raw);
            if (!id.IsInvalid)
            {
                List<ZombieSpawnpoint> region = LevelZombies.spawns[id.X, id.Y];
                if (region.Count > id.Index)
                {
                    spawnpoint = region[id.Index];
                    return true;
                }
            }
        }

        spawnpoint = null!;
        return false;
    }
    public static bool TryGetZombieSpawnpoint(NetId64 netId, out RegionIdentifier regionIdentifier)
    {
        ThreadUtil.assertIsGameThread();

        if (SpawnNetIds.TryGetValue(netId, out int raw))
        {
            RegionIdentifier id = RegionIdentifier.CreateUnsafe(raw);
            if (id.CheckSafe() && LevelZombies.spawns[id.X, id.Y].Count > id.Index)
            {
                regionIdentifier = id;
                return true;
            }
        }

        regionIdentifier = RegionIdentifier.Invalid;
        return false;
    }
    public static bool TryGetSpawnTable(SpawnType spawnType, NetId64 netId, out int index)
    {
        ThreadUtil.assertIsGameThread();

        if (spawnType is not SpawnType.Animal and not SpawnType.Vehicle and not SpawnType.Item and not SpawnType.Zombie)
        {
            index = -1;
            return false;
        }

        if (SpawnNetIds.TryGetValue(netId, out index))
        {
            return spawnType switch
            {
                SpawnType.Animal => LevelAnimals.tables.Count,
                SpawnType.Vehicle => LevelVehicles.tables.Count,
                SpawnType.Item => LevelItems.tables.Count,
                _ => LevelZombies.tables.Count
            } > index;
        }

        index = -1;
        return false;
    }
    public static bool TryGetSpawnTableTier(SpawnType spawnType, NetId64 netId, out SpawnTierIdentifier identifier)
    {
        ThreadUtil.assertIsGameThread();

        if (spawnType is not SpawnType.Animal and not SpawnType.Vehicle and not SpawnType.Item and not SpawnType.Zombie)
        {
            identifier = default;
            return false;
        }

        if (SpawnNetIds.TryGetValue(netId, out int index))
        {
            identifier = new SpawnTierIdentifier(index);
            return spawnType == identifier.Type && identifier.CheckSafe();
        }

        identifier = default;
        return false;
    }
    public static bool TryGetSpawnTableTierAsset(SpawnType spawnType, NetId64 netId, out SpawnAssetIdentifier identifier)
    {
        ThreadUtil.assertIsGameThread();

        if (spawnType is not SpawnType.Animal and not SpawnType.Vehicle and not SpawnType.Item and not SpawnType.Zombie)
        {
            identifier = default;
            return false;
        }

        if (SpawnNetIds.TryGetValue(netId, out int index))
        {
            identifier = new SpawnAssetIdentifier(index);
            return spawnType == identifier.Type && identifier.CheckSafe();
        }

        identifier = default;
        return false;
    }
    public static bool TryGetAnimalSpawnTable(NetId64 netId, out AnimalTable spawnTable)
    {
        if (TryGetSpawnTable(SpawnType.Animal, netId, out int index))
        {
            spawnTable = LevelAnimals.tables[index];
            return true;
        }

        spawnTable = null!;
        return false;
    }
    public static bool TryGetVehicleSpawnTable(NetId64 netId, out VehicleTable spawnTable)
    {
        if (TryGetSpawnTable(SpawnType.Vehicle, netId, out int index))
        {
            spawnTable = LevelVehicles.tables[index];
            return true;
        }

        spawnTable = null!;
        return false;
    }
    public static bool TryGetItemSpawnTable(NetId64 netId, out ItemTable spawnTable)
    {
        if (TryGetSpawnTable(SpawnType.Item, netId, out int index))
        {
            spawnTable = LevelItems.tables[index];
            return true;
        }

        spawnTable = null!;
        return false;
    }
    public static bool TryGetZombieSpawnTable(NetId64 netId, out ZombieTable spawnTable)
    {
        if (TryGetSpawnTable(SpawnType.Zombie, netId, out int index))
        {
            spawnTable = LevelZombies.tables[index];
            return true;
        }

        spawnTable = null!;
        return false;
    }
    public static bool TryGetAnimalSpawnTableTier(NetId64 netId, out AnimalTier spawnTable)
    {
        if (TryGetSpawnTableTier(SpawnType.Animal, netId, out SpawnTierIdentifier identifier))
        {
            spawnTable = LevelAnimals.tables[identifier.TableIndex].tiers[identifier.TierIndex];
            return true;
        }

        spawnTable = null!;
        return false;
    }
    public static bool TryGetVehicleSpawnTableTier(NetId64 netId, out VehicleTier spawnTable)
    {
        if (TryGetSpawnTableTier(SpawnType.Vehicle, netId, out SpawnTierIdentifier identifier))
        {
            spawnTable = LevelVehicles.tables[identifier.TableIndex].tiers[identifier.TierIndex];
            return true;
        }

        spawnTable = null!;
        return false;
    }
    public static bool TryGetItemSpawnTableTier(NetId64 netId, out ItemTier spawnTable)
    {
        if (TryGetSpawnTableTier(SpawnType.Item, netId, out SpawnTierIdentifier identifier))
        {
            spawnTable = LevelItems.tables[identifier.TableIndex].tiers[identifier.TierIndex];
            return true;
        }

        spawnTable = null!;
        return false;
    }
    public static bool TryGetAnimalSpawnTable(NetId64 netId, out int index) => TryGetSpawnTable(SpawnType.Animal, netId, out index);
    public static bool TryGetVehicleSpawnTable(NetId64 netId, out int index) => TryGetSpawnTable(SpawnType.Vehicle, netId, out index);
    public static bool TryGetItemSpawnTable(NetId64 netId, out int index) => TryGetSpawnTable(SpawnType.Item, netId, out index);
    public static bool TryGetZombieSpawnTable(NetId64 netId, out int index) => TryGetSpawnTable(SpawnType.Zombie, netId, out index);
    public static bool TryGetAnimalSpawnTableTier(NetId64 netId, out SpawnTierIdentifier identifier) => TryGetSpawnTableTier(SpawnType.Animal, netId, out identifier);
    public static bool TryGetVehicleSpawnTableTier(NetId64 netId, out SpawnTierIdentifier identifier) => TryGetSpawnTableTier(SpawnType.Vehicle, netId, out identifier);
    public static bool TryGetItemSpawnTableTier(NetId64 netId, out SpawnTierIdentifier identifier) => TryGetSpawnTableTier(SpawnType.Item, netId, out identifier);
    public static bool TryGetAnimalSpawnTableTierAsset(NetId64 netId, out SpawnAssetIdentifier identifier) => TryGetSpawnTableTierAsset(SpawnType.Animal, netId, out identifier);
    public static bool TryGetVehicleSpawnTableTierAsset(NetId64 netId, out SpawnAssetIdentifier identifier) => TryGetSpawnTableTierAsset(SpawnType.Vehicle, netId, out identifier);
    public static bool TryGetItemSpawnTableTierAsset(NetId64 netId, out SpawnAssetIdentifier identifier) => TryGetSpawnTableTierAsset(SpawnType.Item, netId, out identifier);
    public static bool TryGetZombieSpawnTableTierAsset(NetId64 netId, out SpawnAssetIdentifier identifier) => TryGetSpawnTableTierAsset(SpawnType.Zombie, netId, out identifier);
    public static bool TryGetSpawnTableNetId(AnimalTable spawnTable, out NetId64 netId)
    {
        ThreadUtil.assertIsGameThread();

        int index = LevelAnimals.tables.IndexOf(spawnTable);
        if (index != -1)
            return TryGetSpawnTableNetId(SpawnType.Animal, index, out netId);

        netId = NetId64.Invalid;
        return false;
    }
    public static bool TryGetSpawnTableNetId(VehicleTable spawnTable, out NetId64 netId)
    {
        ThreadUtil.assertIsGameThread();

        int index = LevelVehicles.tables.IndexOf(spawnTable);
        if (index != -1)
            return TryGetSpawnTableNetId(SpawnType.Vehicle, index, out netId);

        netId = NetId64.Invalid;
        return false;
    }
    public static bool TryGetSpawnTableNetId(ItemTable spawnTable, out NetId64 netId)
    {
        ThreadUtil.assertIsGameThread();

        int index = LevelItems.tables.IndexOf(spawnTable);
        if (index != -1)
            return TryGetSpawnTableNetId(SpawnType.Item, index, out netId);
        
        netId = NetId64.Invalid;
        return false;
    }
    public static bool TryGetSpawnTableNetId(ZombieTable spawnTable, out NetId64 netId)
    {
        ThreadUtil.assertIsGameThread();

        int index = LevelZombies.tables.IndexOf(spawnTable);
        if (index != -1)
            return TryGetSpawnTableNetId(SpawnType.Zombie, index, out netId);

        netId = NetId64.Invalid;
        return false;
    }
    public static bool TryGetSpawnTableNetId(SpawnType type, int index, out NetId64 netId)
    {
        ThreadUtil.assertIsGameThread();

        if (index > byte.MaxValue || index < 0 || type is not SpawnType.Animal and not SpawnType.Vehicle and not SpawnType.Item and not SpawnType.Zombie)
        {
            netId = NetId64.Invalid;
            return false;
        }

        netId = (type switch
        {
            SpawnType.Animal => AnimalSpawnTables,
            SpawnType.Vehicle => VehicleSpawnTables,
            SpawnType.Item => ItemSpawnTables,
            _ => ZombieSpawnTables
        })[index];
        return !netId.IsNull();
    }
    public static bool TryGetSpawnTierNetId(SpawnTierIdentifier tier, out NetId64 netId)
    {
        ThreadUtil.assertIsGameThread();

        if (!SpawnTierAssignments.TryGetValue(tier, out netId))
        {
            netId = NetId64.Invalid;
            return false;
        }

        return true;
    }
    public static bool TryGetSpawnAssetNetId(SpawnAssetIdentifier asset, out NetId64 netId)
    {
        ThreadUtil.assertIsGameThread();

        if (!SpawnAssetAssignments.TryGetValue(asset, out netId))
        {
            netId = NetId64.Invalid;
            return false;
        }

        return true;
    }
    public static bool TryGetAnimalSpawnNetId(AnimalSpawnpoint spawnpoint, out NetId64 netId)
    {
        ThreadUtil.assertIsGameThread();

        int index = LevelAnimals.spawns.IndexOf(spawnpoint);
        if (index == -1)
        {
            netId = NetId64.Invalid;
            return false;
        }
        return TryGetAnimalSpawnNetId(index, out netId);
    }

    public static bool TryGetAnimalSpawnNetId(int index, out NetId64 netId)
    {
        ThreadUtil.assertIsGameThread();

        if (!AnimalSpawnAssignments.TryGetValue(index, out netId))
        {
            netId = NetId64.Invalid;
            return false;
        }

        return true;
    }
    public static bool TryGetPlayerSpawnNetId(PlayerSpawnpoint spawnpoint, out NetId64 netId)
    {
        ThreadUtil.assertIsGameThread();

        int index = LevelPlayers.spawns.IndexOf(spawnpoint);
        if (index == -1)
        {
            netId = NetId64.Invalid;
            return false;
        }
        return TryGetPlayerSpawnNetId(index, out netId);
    }

    public static bool TryGetPlayerSpawnNetId(int index, out NetId64 netId)
    {
        ThreadUtil.assertIsGameThread();

        if (!PlayerSpawnAssignments.TryGetValue(index, out netId))
        {
            netId = NetId64.Invalid;
            return false;
        }

        return true;
    }
    public static bool TryGetVehicleSpawnNetId(VehicleSpawnpoint spawnpoint, out NetId64 netId)
    {
        ThreadUtil.assertIsGameThread();

        int index = LevelVehicles.spawns.IndexOf(spawnpoint);
        if (index == -1)
        {
            netId = NetId64.Invalid;
            return false;
        }
        return TryGetVehicleSpawnNetId(index, out netId);
    }

    public static bool TryGetVehicleSpawnNetId(int index, out NetId64 netId)
    {
        ThreadUtil.assertIsGameThread();

        if (!VehicleSpawnAssignments.TryGetValue(index, out netId))
        {
            netId = NetId64.Invalid;
            return false;
        }

        return true;
    }
    public static bool TryGetItemSpawnNetId(ItemSpawnpoint spawnpoint, out NetId64 netId)
    {
        ThreadUtil.assertIsGameThread();

        foreach (RegionCoord coord in RegionUtil.EnumerateRegions(spawnpoint.point))
        {
            List<ItemSpawnpoint> region = LevelItems.spawns[coord.x, coord.y];
            int index = region.IndexOf(spawnpoint);
            if (index == -1)
            {
                netId = NetId64.Invalid;
                return false;
            }

            return TryGetItemSpawnNetId(new RegionIdentifier(coord, index), out netId);
        }

        netId = NetId64.Invalid;
        return false;
    }
    public static bool TryGetItemSpawnNetId(RegionIdentifier id, out NetId64 netId)
    {
        ThreadUtil.assertIsGameThread();

        if (!ItemSpawnAssignments.TryGetValue(id, out netId))
        {
            netId = NetId64.Invalid;
            return false;
        }

        return true;
    }
    public static bool TryGetZombieSpawnNetId(ZombieSpawnpoint spawnpoint, out NetId64 netId)
    {
        ThreadUtil.assertIsGameThread();

        foreach (RegionCoord coord in RegionUtil.EnumerateRegions(spawnpoint.point))
        {
            List<ZombieSpawnpoint> region = LevelZombies.spawns[coord.x, coord.y];
            int index = region.IndexOf(spawnpoint);
            if (index == -1)
            {
                netId = NetId64.Invalid;
                return false;
            }

            return TryGetZombieSpawnNetId(new RegionIdentifier(coord, index), out netId);
        }

        netId = NetId64.Invalid;
        return false;
    }
    public static bool TryGetZombieSpawnNetId(RegionIdentifier id, out NetId64 netId)
    {
        ThreadUtil.assertIsGameThread();

        if (!ZombieSpawnAssignments.TryGetValue(id, out netId))
        {
            netId = NetId64.Invalid;
            return false;
        }

        return true;
    }
#if SERVER
    internal static void AssignExisting()
    {
        Clear();

        List<AnimalSpawnpoint> animals = LevelAnimals.spawns;

        int count = 0;
        int size = Math.Min(ushort.MaxValue, animals.Count);
        for (; count < size; ++count)
            AddIndexSpawnpoint(SpawnType.Animal, count);
        
        Logger.DevkitServer.LogInfo(Source, $"Assigned NetIds for {count.Format()} animal spawnpoint{count.S()}.");

        List<PlayerSpawnpoint> players = LevelPlayers.spawns;

        count = 0;
        size = Math.Min(byte.MaxValue, players.Count);
        for (; count < size; ++count)
            AddIndexSpawnpoint(SpawnType.Player, count);
        
        Logger.DevkitServer.LogInfo(Source, $"Assigned NetIds for {count.Format()} player spawnpoint{count.S()}.");

        List<VehicleSpawnpoint> vehicles = LevelVehicles.spawns;

        count = 0;
        size = Math.Min(ushort.MaxValue, vehicles.Count);
        for (; count < size; ++count)
            AddIndexSpawnpoint(SpawnType.Vehicle, count);
        
        Logger.DevkitServer.LogInfo(Source, $"Assigned NetIds for {count.Format()} vehicle spawnpoint{count.S()}.");

        count = 0;

        foreach (RegionCoord region in RegionUtil.LinearEnumerateRegions())
        {
            List<ItemSpawnpoint> items = LevelItems.spawns[region.x, region.y];
            size = Math.Min(ushort.MaxValue, items.Count);
            for (int i = 0; i < size; ++i)
                AddRegionSpawnpoint(SpawnType.Item, new RegionIdentifier(region, i));
            count += size;
        }

        Logger.DevkitServer.LogInfo(Source, $"Assigned NetIds for {count.Format()} item spawnpoint{count.S()}.");

        count = 0;

        foreach (RegionCoord region in RegionUtil.LinearEnumerateRegions())
        {
            List<ZombieSpawnpoint> zombies = LevelZombies.spawns[region.x, region.y];
            size = Math.Min(ushort.MaxValue, zombies.Count);
            for (int i = 0; i < size; ++i)
                AddRegionSpawnpoint(SpawnType.Zombie, new RegionIdentifier(region, i));
            count += size;
        }

        Logger.DevkitServer.LogInfo(Source, $"Assigned NetIds for {count.Format()} zombie spawnpoint{count.S()}.");

        count = 0;
        int tierCount = 0, assetCount = 0;

        List<AnimalTable> animalTables = LevelAnimals.tables;
        size = Math.Min(byte.MaxValue, animalTables.Count);
        for (; count < size; ++count)
        {
            AddSpawnTable(SpawnType.Animal, count);
            
            List<AnimalTier> tiers = animalTables[count].tiers;
            byte i = (byte)count;
            int tierSize = Math.Min(byte.MaxValue, tiers.Count);
            for (byte j = 0; j < tierSize; ++j)
            {
                AddSpawnTier(new SpawnTierIdentifier(SpawnType.Animal, i, j));
                List<AnimalSpawn> assets = tiers[j].table;
                int assetSize = Math.Min(byte.MaxValue, assets.Count);
                for (int k = 0; k < assetSize; ++k)
                    AddSpawnAsset(new SpawnAssetIdentifier(SpawnType.Animal, i, j, (byte)k));
                assetCount += assetSize;
            }
            tierCount += tierSize;
        }
        
        Logger.DevkitServer.LogInfo(Source, $"Assigned NetIds for {count.Format()} animal spawn table{count.S()}, {tierCount.Format()} animal spawn tier{tierCount.S()}, and {assetCount.Format()} animal spawn assets.");

        count = 0;
        tierCount = 0;
        assetCount = 0;

        List<VehicleTable> vehicleTables = LevelVehicles.tables;
        size = Math.Min(byte.MaxValue, vehicleTables.Count);
        for (; count < size; ++count)
        {
            AddSpawnTable(SpawnType.Vehicle, count);
            
            List<VehicleTier> tiers = vehicleTables[count].tiers;
            byte i = (byte)count;
            int tierSize = Math.Min(byte.MaxValue, tiers.Count);
            for (byte j = 0; j < tierSize; ++j)
            {
                AddSpawnTier(new SpawnTierIdentifier(SpawnType.Vehicle, i, j));
                List<VehicleSpawn> assets = tiers[j].table;
                int assetSize = Math.Min(byte.MaxValue, assets.Count);
                for (int k = 0; k < assetSize; ++k)
                    AddSpawnAsset(new SpawnAssetIdentifier(SpawnType.Vehicle, i, j, (byte)k));
                assetCount += assetSize;
            }
            tierCount += tierSize;
        }
        
        Logger.DevkitServer.LogInfo(Source, $"Assigned NetIds for {count.Format()} vehicle spawn table{count.S()}, {tierCount.Format()} vehicle spawn tier{tierCount.S()}, and {assetCount.Format()} vehicle spawn assets.");

        count = 0;
        tierCount = 0;
        assetCount = 0;

        List<ItemTable> itemTables = LevelItems.tables;
        size = Math.Min(byte.MaxValue, itemTables.Count);
        for (; count < size; ++count)
        {
            AddSpawnTable(SpawnType.Item, count);
            
            List<ItemTier> tiers = itemTables[count].tiers;
            byte i = (byte)count;
            int tierSize = Math.Min(byte.MaxValue, tiers.Count);
            for (byte j = 0; j < tierSize; ++j)
            {
                AddSpawnTier(new SpawnTierIdentifier(SpawnType.Item, i, j));
                List<ItemSpawn> assets = tiers[j].table;
                int assetSize = Math.Min(byte.MaxValue, assets.Count);
                for (int k = 0; k < assetSize; ++k)
                    AddSpawnAsset(new SpawnAssetIdentifier(SpawnType.Item, i, j, (byte)k));
                assetCount += assetSize;
            }
            tierCount += tierSize;
        }
        
        Logger.DevkitServer.LogInfo(Source, $"Assigned NetIds for {count.Format()} item spawn table{count.S()}, {tierCount.Format()} item spawn tier{tierCount.S()}, and {assetCount.Format()} item spawn assets.");

        count = 0;
        assetCount = 0;

        List<ZombieTable> zombieTables = LevelZombies.tables;
        size = Math.Min(byte.MaxValue, zombieTables.Count);
        for (; count < size; ++count)
        {
            AddSpawnTable(SpawnType.Zombie, count);
            
            ZombieSlot[] tiers = zombieTables[count].slots;
            byte i = (byte)count;
            for (byte j = 0; j < tiers.Length; ++j)
            {
                List<ZombieCloth> assets = tiers[j].table;
                int assetSize = Math.Min(byte.MaxValue, assets.Count);
                for (int k = 0; k < assetSize; ++k)
                    AddSpawnAsset(new SpawnAssetIdentifier(SpawnType.Zombie, i, j, (byte)k));
                assetCount += assetSize;
            }
        }
        
        Logger.DevkitServer.LogInfo(Source, $"Assigned NetIds for {count.Format()} zombie spawn table{count.S()} and {assetCount.Format()} zombie spawn clothings.");
    }
#endif
    private static void ClaimBasicSpawnpointNetId(int index, SpawnType typeChecked, NetId64 netId)
    {
        if (!netId.IsNull() && SpawnNetIds.ContainsKey(netId))
        {
#if !LEVEL_DATA_LOGGING
            if (Level.isLoaded)
#endif
                Logger.DevkitServer.LogDebug(Source, $"Released old NetId pairing: {netId.Format()}.");
        }
        
        if (typeChecked == SpawnType.Animal)
        {
            AnimalSpawnAssignments.Remove(index, out NetId64 old);

            if (SpawnNetIds.Remove(old))
            {
#if !LEVEL_DATA_LOGGING
                if (Level.isLoaded)
#endif
                    Logger.DevkitServer.LogDebug(Source, $"Released old animal NetId pairing: {old.Format()}.");
            }

            if (!netId.IsNull())
                AnimalSpawnAssignments[index] = netId;
        }
        else if (typeChecked == SpawnType.Player)
        {
            PlayerSpawnAssignments.Remove(index, out NetId64 old);

            if (SpawnNetIds.Remove(old))
            {
#if !LEVEL_DATA_LOGGING
                if (Level.isLoaded)
#endif
                    Logger.DevkitServer.LogDebug(Source, $"Released old player NetId pairing: {old.Format()}.");
            }

            if (!netId.IsNull())
                PlayerSpawnAssignments[index] = netId;
        }
        else
        {
            VehicleSpawnAssignments.Remove(index, out NetId64 old);

            if (SpawnNetIds.Remove(old))
            {
#if !LEVEL_DATA_LOGGING
                if (Level.isLoaded)
#endif
                    Logger.DevkitServer.LogDebug(Source, $"Released old vehicle NetId pairing: {old.Format()}.");
            }

            if (!netId.IsNull())
                VehicleSpawnAssignments[index] = netId;
        }

        if (!netId.IsNull())
        {
            SpawnNetIds[netId] = index;

#if !LEVEL_DATA_LOGGING
            if (Level.isLoaded)
#endif
                Logger.DevkitServer.LogDebug(Source, $"Claimed new NetId: {netId.Format()} @ {typeChecked.Format()} (# {index.Format()}).");
        }
        else
        {
            Logger.DevkitServer.LogDebug(Source, $"Released NetId: {netId.Format()} @ {typeChecked.Format()} (# {index.Format()}).");
        }
    }
    private static void ClaimBasicSpawnpointNetId(RegionIdentifier id, SpawnType typeChecked, NetId64 netId)
    {
        if (!netId.IsNull() && SpawnNetIds.ContainsKey(netId))
        {
#if !LEVEL_DATA_LOGGING
            if (Level.isLoaded)
#endif
                Logger.DevkitServer.LogDebug(Source, $"Released old NetId pairing: {netId.Format()}.");
        }

        if (typeChecked == SpawnType.Item)
        {
            ItemSpawnAssignments.Remove(id, out NetId64 old);

            if (SpawnNetIds.Remove(old))
            {
#if !LEVEL_DATA_LOGGING
                if (Level.isLoaded)
#endif
                    Logger.DevkitServer.LogDebug(Source, $"Released old item NetId pairing: {old.Format()}.");
            }

            if (!netId.IsNull())
                ItemSpawnAssignments[id] = netId;
        }
        else
        {
            ZombieSpawnAssignments.Remove(id, out NetId64 old);

            if (SpawnNetIds.Remove(old))
            {
#if !LEVEL_DATA_LOGGING
                if (Level.isLoaded)
#endif
                    Logger.DevkitServer.LogDebug(Source, $"Released old zombie NetId pairing: {old.Format()}.");
            }

            if (!netId.IsNull())
                ZombieSpawnAssignments[id] = netId;
        }

        if (!netId.IsNull())
        {
            SpawnNetIds[netId] = id.Raw;

#if !LEVEL_DATA_LOGGING
            if (Level.isLoaded)
#endif
                Logger.DevkitServer.LogDebug(Source, $"Claimed new NetId: {netId.Format()} @ {typeChecked.Format()} ({id.Format()}).");
        }
        else
        {
            Logger.DevkitServer.LogDebug(Source, $"Released NetId: {netId.Format()} @ {typeChecked.Format()} ({id.Format()}).");
        }
    }
    private static void ClaimBasicAssetNetId(SpawnAssetIdentifier id, NetId64 netId)
    {
        if (!netId.IsNull() && SpawnNetIds.ContainsKey(netId))
        {
#if !LEVEL_DATA_LOGGING
            if (Level.isLoaded)
#endif
                Logger.DevkitServer.LogDebug(Source, $"Released old NetId pairing: {netId.Format()}.");
        }

        SpawnAssetAssignments.Remove(id, out NetId64 old);

        if (SpawnNetIds.Remove(old))
        {
#if !LEVEL_DATA_LOGGING
            if (Level.isLoaded)
#endif
                Logger.DevkitServer.LogDebug(Source, $"Released old NetId pairing: {old.Format()}.");
        }
        
        if (!netId.IsNull())
        {
            SpawnAssetAssignments[id] = netId;
            SpawnNetIds[netId] = id.Raw;

#if !LEVEL_DATA_LOGGING
            if (Level.isLoaded)
#endif
                Logger.DevkitServer.LogDebug(Source, $"Claimed new NetId: {netId.Format()} @ {id.Type.Format()} ({id.Format()}).");
        }
        else
        {
            Logger.DevkitServer.LogDebug(Source, $"Released NetId: {netId.Format()} @ {id.Type.Format()} ({id.Format()}).");
        }
    }
    private static void ClaimBasicTierNetId(SpawnTierIdentifier id, NetId64 netId)
    {
        if (!netId.IsNull() && SpawnNetIds.ContainsKey(netId))
        {
#if !LEVEL_DATA_LOGGING
            if (Level.isLoaded)
#endif
                Logger.DevkitServer.LogDebug(Source, $"Released old NetId pairing: {netId.Format()}.");
        }

        SpawnTierAssignments.Remove(id, out NetId64 old);

        if (SpawnNetIds.Remove(old))
        {
#if !LEVEL_DATA_LOGGING
            if (Level.isLoaded)
#endif
                Logger.DevkitServer.LogDebug(Source, $"Released old NetId pairing: {old.Format()}.");
        }
        
        if (!netId.IsNull())
        {
            SpawnTierAssignments[id] = netId;
            SpawnNetIds[netId] = id.Raw;

#if !LEVEL_DATA_LOGGING
            if (Level.isLoaded)
#endif
                Logger.DevkitServer.LogDebug(Source, $"Claimed new NetId: {netId.Format()} @ {id.Type.Format()} ({id.Format()}).");
        }
        else
        {
            Logger.DevkitServer.LogDebug(Source, $"Released NetId: {netId.Format()} @ {id.Type.Format()} ({id.Format()}).");
        }
    }
    private static void ClaimBasicTableNetId(int index, SpawnType typeChecked, NetId64 netId)
    {
        if (!netId.IsNull() && SpawnNetIds.ContainsKey(netId))
        {
#if !LEVEL_DATA_LOGGING
            if (Level.isLoaded)
#endif
                Logger.DevkitServer.LogDebug(Source, $"Released old NetId pairing: {netId.Format()}.");
        }

        NetId64 removed = NetId64.Invalid;
        switch (typeChecked)
        {
            case SpawnType.Animal:
                removed = AnimalSpawnTables[index];
                AnimalSpawnTables[index] = netId;
                break;
            case SpawnType.Vehicle:
                removed = VehicleSpawnTables[index];
                VehicleSpawnTables[index] = netId;
                break;
            case SpawnType.Item:
                removed = ItemSpawnTables[index];
                ItemSpawnTables[index] = netId;
                break;
            case SpawnType.Zombie:
                removed = ZombieSpawnTables[index];
                ZombieSpawnTables[index] = netId;
                break;
        }
        
        if (!removed.IsNull())
        {
            SpawnNetIds.Remove(removed);
#if !LEVEL_DATA_LOGGING
            if (Level.isLoaded)
#endif
                Logger.DevkitServer.LogDebug(Source, $"Released old NetId pairing: {removed.Format()}.");
        }
        
        if (!netId.IsNull())
        {
            SpawnNetIds[netId] = index;

#if !LEVEL_DATA_LOGGING
            if (Level.isLoaded)
#endif
                Logger.DevkitServer.LogDebug(Source, $"Claimed new NetId: {netId.Format()} @ {typeChecked.Format()} ({index.Format()}).");
        }
        else
        {
            Logger.DevkitServer.LogDebug(Source, $"Released NetId: {netId.Format()} @ {typeChecked.Format()} ({index.Format()}).");
        }
    }
#if CLIENT
    public void LoadData(SpawnsNetIdDatabaseLevelData data)
    {
        Clear();
        
        NetId64[] netIds = data.NetIds;
        int[] indexes = data.Indexes;

        int index = 0;

        int maxIndex = data.IndexPlayer;
        for (; index < maxIndex; ++index)
        {
            ClaimBasicSpawnpointNetId(indexes[index], SpawnType.Animal, netIds[index]);
        }

        maxIndex = data.IndexVehicle;
        for (; index < maxIndex; ++index)
        {
            ClaimBasicSpawnpointNetId(indexes[index], SpawnType.Player, netIds[index]);
        }

        maxIndex = data.IndexItem;
        for (; index < maxIndex; ++index)
        {
            ClaimBasicSpawnpointNetId(indexes[index], SpawnType.Vehicle, netIds[index]);
        }

        maxIndex = data.IndexZombie;
        for (; index < maxIndex; ++index)
        {
            RegionIdentifier id = RegionIdentifier.CreateUnsafe(indexes[index]);
            if (!id.IsInvalid)
                ClaimBasicSpawnpointNetId(id, SpawnType.Item, netIds[index]);
            else
                Logger.DevkitServer.LogWarning(Source, $"Received item spawn with invalid region identifier: {id.Format()}.");
        }

        maxIndex = data.IndexAnimalTable;
        for (; index < maxIndex; ++index)
        {
            RegionIdentifier id = RegionIdentifier.CreateUnsafe(indexes[index]);
            if (!id.IsInvalid)
                ClaimBasicSpawnpointNetId(id, SpawnType.Zombie, netIds[index]);
            else
                Logger.DevkitServer.LogWarning(Source, $"Received zombie spawn with invalid region identifier: {id.Format()}.");
        }

        maxIndex = data.IndexVehicleTable;
        for (; index < maxIndex; ++index)
        {
            int tableIndex = indexes[index];
            if (tableIndex is >= 0 and <= byte.MaxValue)
                ClaimBasicTableNetId(tableIndex, SpawnType.Animal, netIds[index]);
            else
                Logger.DevkitServer.LogWarning(Source, $"Received animal spawn table with invalid index: {tableIndex.Format()}.");
        }

        maxIndex = data.IndexItemTable;
        for (; index < maxIndex; ++index)
        {
            int tableIndex = indexes[index];
            if (tableIndex is >= 0 and <= byte.MaxValue)
                ClaimBasicTableNetId(tableIndex, SpawnType.Vehicle, netIds[index]);
            else
                Logger.DevkitServer.LogWarning(Source, $"Received vehicle spawn table with invalid index: {tableIndex.Format()}.");
        }

        maxIndex = data.IndexZombieTable;
        for (; index < maxIndex; ++index)
        {
            int tableIndex = indexes[index];
            if (tableIndex is >= 0 and <= byte.MaxValue)
                ClaimBasicTableNetId(tableIndex, SpawnType.Item, netIds[index]);
            else
                Logger.DevkitServer.LogWarning(Source, $"Received item spawn table with invalid index: {tableIndex.Format()}.");
        }

        maxIndex = data.IndexTiers;
        for (; index < maxIndex; ++index)
        {
            int tableIndex = indexes[index];
            if (tableIndex is >= 0 and <= byte.MaxValue)
                ClaimBasicTableNetId(tableIndex, SpawnType.Zombie, netIds[index]);
            else
                Logger.DevkitServer.LogWarning(Source, $"Received zombie spawn table with invalid index: {tableIndex.Format()}.");
        }

        maxIndex = data.IndexAssets;
        for (; index < maxIndex; ++index)
        {
            SpawnTierIdentifier tier = new SpawnTierIdentifier(indexes[index]);
            if (tier.Type is SpawnType.Animal or SpawnType.Vehicle or SpawnType.Item or SpawnType.Zombie)
                ClaimBasicTierNetId(tier, netIds[index]);
            else
                Logger.DevkitServer.LogWarning(Source, $"Received spawn tier with invalid spawn type: {tier.Type.Format()}.");
        }

        maxIndex = data.Count;
        for (; index < maxIndex; ++index)
        {
            SpawnAssetIdentifier asset = new SpawnAssetIdentifier(indexes[index]);
            if (asset.Type is SpawnType.Animal or SpawnType.Vehicle or SpawnType.Item or SpawnType.Zombie)
                ClaimBasicAssetNetId(asset, netIds[index]);
            else
                Logger.DevkitServer.LogWarning(Source, $"Received spawn asset with invalid spawn type: {asset.Type.Format()}.");
        }
    }
#elif SERVER
    public SpawnsNetIdDatabaseLevelData SaveData(CSteamID user)
    {
        SpawnsNetIdDatabaseLevelData data = new SpawnsNetIdDatabaseLevelData();

        int animalSpawnTableCount = 0;
        int vehicleSpawnTableCount = 0;
        int itemSpawnTableCount = 0;
        int zombieSpawnTableCount = 0;
        
        for (int i = 0; i < byte.MaxValue; ++i)
        {
            bool any = false;
            if (AnimalSpawnTables[i].Id != 0)
            {
                ++animalSpawnTableCount;
                any = true;
            }
            if (VehicleSpawnTables[i].Id != 0)
            {
                ++vehicleSpawnTableCount;
                any = true;
            }
            if (ItemSpawnTables[i].Id != 0)
            {
                ++itemSpawnTableCount;
                any = true;
            }
            if (ZombieSpawnTables[i].Id != 0)
            {
                ++zombieSpawnTableCount;
                any = true;
            }

            if (!any)
                break;
        }
        
        NetId64[] netIds = new NetId64[AnimalSpawnAssignments.Count
                                       + PlayerSpawnAssignments.Count
                                       + VehicleSpawnAssignments.Count
                                       + ItemSpawnAssignments.Count
                                       + ZombieSpawnAssignments.Count
                                       + animalSpawnTableCount
                                       + vehicleSpawnTableCount
                                       + itemSpawnTableCount
                                       + zombieSpawnTableCount
                                       + SpawnTierAssignments.Count
                                       + SpawnAssetAssignments.Count];
        
        int[] indexes = new int[netIds.Length];

        data.NetIds = netIds;
        data.Indexes = indexes;
        data.Count = netIds.Length;

        AnimalSpawnAssignments.Keys.CopyTo(indexes, 0);
        AnimalSpawnAssignments.Values.CopyTo(netIds, 0);
        
        int index = AnimalSpawnAssignments.Count;
        data.IndexPlayer = index;

        PlayerSpawnAssignments.Keys.CopyTo(indexes, index);
        PlayerSpawnAssignments.Values.CopyTo(netIds, index);
        index += PlayerSpawnAssignments.Count;

        data.IndexVehicle = index;

        VehicleSpawnAssignments.Keys.CopyTo(indexes, index);
        VehicleSpawnAssignments.Values.CopyTo(netIds, index);
        index += VehicleSpawnAssignments.Count;

        data.IndexItem = index;
        ItemSpawnAssignments.Values.CopyTo(netIds, index);
        foreach (RegionIdentifier region in ItemSpawnAssignments.Keys)
        {
            indexes[index] = region.Raw;
            ++index;
        }

        data.IndexZombie = index;
        ZombieSpawnAssignments.Values.CopyTo(netIds, index);
        foreach (RegionIdentifier region in ZombieSpawnAssignments.Keys)
        {
            indexes[index] = region.Raw;
            ++index;
        }

        data.IndexAnimalTable = index;
        AnimalSpawnTables.AsSpan(0, animalSpawnTableCount).CopyTo(netIds.AsSpan(index));
        for (int i = 0; i < animalSpawnTableCount; ++i)
        {
            indexes[index] = i;
            ++index;
        }

        data.IndexVehicleTable = index;
        VehicleSpawnTables.AsSpan(0, vehicleSpawnTableCount).CopyTo(netIds.AsSpan(index));
        for (int i = 0; i < vehicleSpawnTableCount; ++i)
        {
            indexes[index] = i;
            ++index;
        }

        data.IndexItemTable = index;
        ItemSpawnTables.AsSpan(0, itemSpawnTableCount).CopyTo(netIds.AsSpan(index));
        for (int i = 0; i < itemSpawnTableCount; ++i)
        {
            indexes[index] = i;
            ++index;
        }

        data.IndexZombieTable = index;
        ZombieSpawnTables.AsSpan(0, zombieSpawnTableCount).CopyTo(netIds.AsSpan(index));
        for (int i = 0; i < zombieSpawnTableCount; ++i)
        {
            indexes[index] = i;
            ++index;
        }

        data.IndexTiers = index;
        SpawnTierAssignments.Values.CopyTo(netIds, index);
        foreach (SpawnTierIdentifier tier in SpawnTierAssignments.Keys)
        {
            indexes[index] = tier.Raw;
            ++index;
        }

        data.IndexAssets = index;
        SpawnAssetAssignments.Values.CopyTo(netIds, index);
        foreach (SpawnAssetIdentifier asset in SpawnAssetAssignments.Keys)
        {
            indexes[index] = asset.Raw;
            ++index;
        }

        return data;
    }
#endif
    public SpawnsNetIdDatabaseLevelData ReadData(ByteReader reader, ushort version)
    {
        SpawnsNetIdDatabaseLevelData data = new SpawnsNetIdDatabaseLevelData
        {
            IndexPlayer = reader.ReadInt32(),
            IndexVehicle = reader.ReadInt32(),
            IndexItem = reader.ReadInt32(),
            IndexZombie = reader.ReadInt32(),
            IndexAnimalTable = reader.ReadInt32(),
            IndexVehicleTable = reader.ReadInt32(),
            IndexItemTable = reader.ReadInt32(),
            IndexZombieTable = reader.ReadInt32(),
            IndexTiers = reader.ReadInt32(),
            IndexAssets = reader.ReadInt32(),
            Count = reader.ReadInt32()
        };

        int dataCount = data.Count;
        NetId64[] netIds = new NetId64[dataCount];
        int[] indexes = new int[dataCount];

        for (int i = 0; i < dataCount; ++i)
            indexes[i] = reader.ReadInt32();
        for (int i = 0; i < dataCount; ++i)
            netIds[i] = new NetId64(reader.ReadUInt64());

        data.NetIds = netIds;
        data.Indexes = indexes;

        return data;
    }
    public void WriteData(ByteWriter writer, SpawnsNetIdDatabaseLevelData data)
    {
        int dataCount = data.Count;

        writer.ExtendBufferFor(11 * sizeof(int) + data.Indexes.Length * sizeof(int) + data.NetIds.Length * sizeof(ulong));

        writer.Write(data.IndexPlayer);
        writer.Write(data.IndexVehicle);
        writer.Write(data.IndexItem);
        writer.Write(data.IndexZombie);
        writer.Write(data.IndexAnimalTable);
        writer.Write(data.IndexVehicleTable);
        writer.Write(data.IndexItemTable);
        writer.Write(data.IndexZombieTable);
        writer.Write(data.IndexTiers);
        writer.Write(data.IndexAssets);
        writer.Write(dataCount);

        for (int i = 0; i < dataCount; ++i)
            writer.Write(data.Indexes[i]);
        for (int i = 0; i < dataCount; ++i)
            writer.Write(data.NetIds[i].Id);
    }
}

#nullable disable
public class SpawnsNetIdDatabaseLevelData
{
    public int[] Indexes { get; set; }
    public NetId64[] NetIds { get; set; }
    public int IndexPlayer { get; set; }
    public int IndexVehicle { get; set; }
    public int IndexItem { get; set; }
    public int IndexZombie { get; set; }
    public int IndexAnimalTable { get; set; }
    public int IndexVehicleTable { get; set; }
    public int IndexItemTable { get; set; }
    public int IndexZombieTable { get; set; }
    public int IndexTiers { get; set; }
    public int IndexAssets { get; set; }
    public int Count { get; set; }
}

#nullable restore