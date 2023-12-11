using DevkitServer.Core.Tools;
using DevkitServer.Models;
using DevkitServer.Multiplayer.Networking;
using DevkitServer.Util.Encoding;
using DevkitServer.Util.Region;

namespace DevkitServer.Multiplayer.Levels;
public sealed class SpawnpointNetIdDatabase : IReplicatedLevelDataSource<SpawnpointNetIdReplicatedLevelData>
{
    public ushort CurrentDataVersion => 0;

    private const string Source = "SPAWNPOINT NET IDS";
    private static readonly Dictionary<int, NetId> AnimalSpawnAssignments = new Dictionary<int, NetId>(32);
    private static readonly Dictionary<int, NetId> PlayerSpawnAssignments = new Dictionary<int, NetId>(16);
    private static readonly Dictionary<int, NetId> VehicleSpawnAssignments = new Dictionary<int, NetId>(128);
    private static readonly Dictionary<RegionIdentifier, NetId> ItemSpawnAssignments = new Dictionary<RegionIdentifier, NetId>(512);
    private static readonly Dictionary<RegionIdentifier, NetId> ZombieSpawnAssignments = new Dictionary<RegionIdentifier, NetId>(512);

    private static Dictionary<int, NetId> GetIndexAssignments(SpawnType type) => type switch
    {
        SpawnType.Animal => AnimalSpawnAssignments,
        SpawnType.Player => PlayerSpawnAssignments,
        _ => VehicleSpawnAssignments
    };
    private static Dictionary<RegionIdentifier, NetId> GetRegionAssignments(SpawnType type) => type switch
    {
        SpawnType.Item => ItemSpawnAssignments,
        _ => ZombieSpawnAssignments
    };

    [UsedImplicitly]
    internal static NetCall<byte, int, NetId> SendBindSpawnpoint = new NetCall<byte, int, NetId>(DevkitServerNetCall.SendBindSpawnpoint);
    private SpawnpointNetIdDatabase() { }
    internal static void Init()
    {
#if SERVER
        SpawnUtil.OnAnimalSpawnpointRemoved += OnAnimalSpawnRemoved;
        SpawnUtil.OnPlayerSpawnpointRemoved += OnPlayerSpawnRemoved;
        SpawnUtil.OnVehicleSpawnpointRemoved += OnVehicleSpawnRemoved;
        SpawnUtil.OnItemSpawnpointRemoved += OnItemSpawnRemoved;
        SpawnUtil.OnZombieSpawnpointRemoved += OnZombieSpawnRemoved;
#endif
        SpawnUtil.OnAnimalSpawnpointIndexUpdated += OnAnimalSpawnIndexUpdated;
        SpawnUtil.OnPlayerSpawnpointIndexUpdated += OnPlayerSpawnIndexUpdated;
        SpawnUtil.OnVehicleSpawnpointIndexUpdated += OnVehicleSpawnIndexUpdated;
        SpawnUtil.OnItemSpawnpointRegionUpdated += OnItemSpawnRegionUpdated;
        SpawnUtil.OnZombieSpawnpointRegionUpdated += OnZombieSpawnRegionUpdated;
    }

    internal static void Shutdown()
    {
#if SERVER
        SpawnUtil.OnAnimalSpawnpointRemoved -= OnAnimalSpawnRemoved;
        SpawnUtil.OnPlayerSpawnpointRemoved -= OnPlayerSpawnRemoved;
        SpawnUtil.OnVehicleSpawnpointRemoved -= OnVehicleSpawnRemoved;
        SpawnUtil.OnItemSpawnpointRemoved -= OnItemSpawnRemoved;
        SpawnUtil.OnZombieSpawnpointRemoved -= OnZombieSpawnRemoved;
#endif
        SpawnUtil.OnAnimalSpawnpointIndexUpdated -= OnAnimalSpawnIndexUpdated;
        SpawnUtil.OnPlayerSpawnpointIndexUpdated -= OnPlayerSpawnIndexUpdated;
        SpawnUtil.OnVehicleSpawnpointIndexUpdated -= OnVehicleSpawnIndexUpdated;
        SpawnUtil.OnItemSpawnpointRegionUpdated -= OnItemSpawnRegionUpdated;
        SpawnUtil.OnZombieSpawnpointRegionUpdated -= OnZombieSpawnRegionUpdated;
    }

    private static void OnAnimalSpawnIndexUpdated(AnimalSpawnpoint point, int fromIndex, int toIndex)
    {
        if (!DevkitServerModule.IsEditing)
            return;

        AnimalSpawnAssignments.TryGetValue(toIndex, out NetId blockingNetId);
        AnimalSpawnAssignments.TryGetValue(fromIndex, out NetId netId);
        if (!blockingNetId.IsNull() && blockingNetId == netId || netId.IsNull())
            return;

        if (!blockingNetId.IsNull())
        {
            Logger.LogDebug($"[{Source}] Released blocking net id to save animal spawnpoint: {point.point.Format()} ({netId.Format()}, # {toIndex.Format()}).");
            NetIdRegistry.Release(blockingNetId);
        }

        NetIdRegistry.Release(netId);
        NetIdRegistry.Assign(netId, toIndex);
        AnimalSpawnAssignments.Remove(fromIndex);
        AnimalSpawnAssignments[toIndex] = netId;
        Logger.LogDebug($"[{Source}] Moved animal spawnpoint NetId: {point.point.Format()} ({netId.Format()}, # {toIndex.Format()}).");
    }
    private static void OnPlayerSpawnIndexUpdated(PlayerSpawnpoint point, int fromIndex, int toIndex)
    {
        if (!DevkitServerModule.IsEditing)
            return;

        PlayerSpawnAssignments.TryGetValue(toIndex, out NetId blockingNetId);
        PlayerSpawnAssignments.TryGetValue(fromIndex, out NetId netId);
        if (!blockingNetId.IsNull() && blockingNetId == netId || netId.IsNull())
            return;

        if (!blockingNetId.IsNull())
        {
            Logger.LogDebug($"[{Source}] Released blocking net id to save player spawnpoint: {point.point.Format()} ({netId.Format()}, # {toIndex.Format()}).");
            NetIdRegistry.Release(blockingNetId);
        }

        NetIdRegistry.Release(netId);
        NetIdRegistry.Assign(netId, toIndex);
        PlayerSpawnAssignments.Remove(fromIndex);
        PlayerSpawnAssignments[toIndex] = netId;
        Logger.LogDebug($"[{Source}] Moved player spawnpoint NetId: {point.point.Format()} ({netId.Format()}, # {toIndex.Format()}).");
    }
    private static void OnVehicleSpawnIndexUpdated(VehicleSpawnpoint point, int fromIndex, int toIndex)
    {
        if (!DevkitServerModule.IsEditing)
            return;

        VehicleSpawnAssignments.TryGetValue(toIndex, out NetId blockingNetId);
        VehicleSpawnAssignments.TryGetValue(fromIndex, out NetId netId);
        if (!blockingNetId.IsNull() && blockingNetId == netId || netId.IsNull())
            return;

        if (!blockingNetId.IsNull())
        {
            Logger.LogDebug($"[{Source}] Released blocking net id to save vehicle spawnpoint: {point.point.Format()} ({netId.Format()}, # {toIndex.Format()}).");
            NetIdRegistry.Release(blockingNetId);
        }

        NetIdRegistry.Release(netId);
        NetIdRegistry.Assign(netId, toIndex);
        VehicleSpawnAssignments.Remove(fromIndex);
        VehicleSpawnAssignments[toIndex] = netId;
        Logger.LogDebug($"[{Source}] Moved vehicle spawnpoint NetId: {point.point.Format()} ({netId.Format()}, # {toIndex.Format()}).");
    }
    private static void OnItemSpawnRegionUpdated(ItemSpawnpoint point, RegionIdentifier fromRegion, RegionIdentifier toRegion)
    {
        if (!DevkitServerModule.IsEditing)
            return;

        ItemSpawnAssignments.TryGetValue(toRegion, out NetId blockingNetId);
        ItemSpawnAssignments.TryGetValue(fromRegion, out NetId netId);
        if (!blockingNetId.IsNull() && blockingNetId == netId || netId.IsNull())
            return;

        if (!blockingNetId.IsNull())
        {
            Logger.LogDebug($"[{Source}] Released blocking net id to save item spawnpoint: {point.point.Format()} ({netId.Format()}, {toRegion.Format()}).");
            NetIdRegistry.Release(blockingNetId);
        }

        NetIdRegistry.Release(netId);
        NetIdRegistry.Assign(netId, toRegion);
        ItemSpawnAssignments.Remove(fromRegion);
        ItemSpawnAssignments[toRegion] = netId;
        Logger.LogDebug($"[{Source}] Moved item spawnpoint NetId: {point.point.Format()} ({netId.Format()}, {toRegion.Format()}).");
    }
    private static void OnZombieSpawnRegionUpdated(ZombieSpawnpoint point, RegionIdentifier fromRegion, RegionIdentifier toRegion)
    {
        if (!DevkitServerModule.IsEditing)
            return;

        ZombieSpawnAssignments.TryGetValue(toRegion, out NetId blockingNetId);
        ZombieSpawnAssignments.TryGetValue(fromRegion, out NetId netId);
        if (!blockingNetId.IsNull() && blockingNetId == netId || netId.IsNull())
            return;

        if (!blockingNetId.IsNull())
        {
            Logger.LogDebug($"[{Source}] Released blocking net id to save zombie spawnpoint: {point.point.Format()} ({netId.Format()}, {toRegion.Format()}).");
            NetIdRegistry.Release(blockingNetId);
        }

        NetIdRegistry.Release(netId);
        NetIdRegistry.Assign(netId, toRegion);
        ZombieSpawnAssignments.Remove(fromRegion);
        ZombieSpawnAssignments[toRegion] = netId;
        Logger.LogDebug($"[{Source}] Moved zombie spawnpoint NetId: {point.point.Format()} ({netId.Format()}, {toRegion.Format()}).");
    }
#if SERVER
    private static void OnAnimalSpawnRemoved(AnimalSpawnpoint point, int index)
    {
        if (!DevkitServerModule.IsEditing)
            return;

        AnimalSpawnAssignments.TryGetValue(index, out NetId netId);
        if (netId.IsNull())
            return;

        NetIdRegistry.Release(netId);
        AnimalSpawnAssignments.Remove(index);
        Logger.LogDebug($"[{Source}] Removed animal spawnpoint NetId: {point.point.Format()} ({netId.Format()}, # {index.Format()}).");
    }
    private static void OnPlayerSpawnRemoved(PlayerSpawnpoint point, int index)
    {
        if (!DevkitServerModule.IsEditing)
            return;

        PlayerSpawnAssignments.TryGetValue(index, out NetId netId);
        if (netId.IsNull())
            return;

        NetIdRegistry.Release(netId);
        PlayerSpawnAssignments.Remove(index);
        Logger.LogDebug($"[{Source}] Removed player spawnpoint NetId: {point.point.Format()} ({netId.Format()}, # {index.Format()}).");
    }
    private static void OnVehicleSpawnRemoved(VehicleSpawnpoint point, int index)
    {
        if (!DevkitServerModule.IsEditing)
            return;

        VehicleSpawnAssignments.TryGetValue(index, out NetId netId);
        if (netId.IsNull())
            return;

        NetIdRegistry.Release(netId);
        VehicleSpawnAssignments.Remove(index);
        Logger.LogDebug($"[{Source}] Removed vehicle spawnpoint NetId: {point.point.Format()} ({netId.Format()}, # {index.Format()}).");
    }
    private static void OnItemSpawnRemoved(ItemSpawnpoint point, RegionIdentifier region)
    {
        if (!DevkitServerModule.IsEditing)
            return;

        ItemSpawnAssignments.TryGetValue(region, out NetId netId);
        if (netId.IsNull())
            return;

        NetIdRegistry.Release(netId);
        ItemSpawnAssignments.Remove(region);
        Logger.LogDebug($"[{Source}] Removed item spawnpoint NetId: {point.point.Format()} ({netId.Format()}, {region.Format()}).");
    }
    private static void OnZombieSpawnRemoved(ZombieSpawnpoint point, RegionIdentifier region)
    {
        if (!DevkitServerModule.IsEditing)
            return;

        ZombieSpawnAssignments.TryGetValue(region, out NetId netId);
        if (netId.IsNull())
            return;

        NetIdRegistry.Release(netId);
        ZombieSpawnAssignments.Remove(region);
        Logger.LogDebug($"[{Source}] Removed zombie spawnpoint NetId: {point.point.Format()} ({netId.Format()}, {region.Format()}).");
    }
#endif
    
#if CLIENT
    [NetCall(NetCallSource.FromServer, DevkitServerNetCall.SendBindSpawnpoint)]
    private static StandardErrorCode ReceiveBindSpawnpoint(MessageContext ctx, byte spawnType, int index, NetId netId)
    {
        SpawnType type = (SpawnType)spawnType;
        if (type is not SpawnType.Animal and not SpawnType.Player and not SpawnType.Vehicle and not SpawnType.Item and not SpawnType.Zombie)
            return StandardErrorCode.InvalidData;

        switch (type)
        {
            case SpawnType.Animal:
                if (LevelAnimals.spawns.Count <= index)
                    return StandardErrorCode.NotFound;
                break;

            case SpawnType.Player:
                if (LevelPlayers.spawns.Count <= index)
                    return StandardErrorCode.NotFound;
                break;

            default:
                if (LevelVehicles.spawns.Count <= index)
                    return StandardErrorCode.NotFound;
                break;

            case SpawnType.Item:
                RegionIdentifier id = RegionIdentifier.CreateUnsafe(index);
                if (id.IsInvalid)
                    return StandardErrorCode.InvalidData;
                List<ItemSpawnpoint> items = LevelItems.spawns[id.X, id.Y];
                if (items.Count <= id.Index)
                    return StandardErrorCode.NotFound;

                ClaimBasicNetId(id, type, netId);
                return StandardErrorCode.Success;

            case SpawnType.Zombie:
                id = RegionIdentifier.CreateUnsafe(index);
                if (id.IsInvalid)
                    return StandardErrorCode.InvalidData;
                List<ZombieSpawnpoint> zombies = LevelZombies.spawns[id.X, id.Y];
                if (zombies.Count <= id.Index)
                    return StandardErrorCode.NotFound;

                ClaimBasicNetId(id, type, netId);
                return StandardErrorCode.Success;
        }

        ClaimBasicNetId(index, type, netId);
        return StandardErrorCode.Success;
    }
#endif
    public static void RemoveIndexSpawnpoint(SpawnType type, int index)
    {
        if (type is not SpawnType.Animal and not SpawnType.Player and not SpawnType.Vehicle)
            return;

        Dictionary<int, NetId> assignments = GetIndexAssignments(type);

        if (assignments.TryGetValue(index, out NetId netId))
        {
            NetIdRegistry.Release(netId);
            assignments.Remove(index);
            if (Level.isLoaded)
                Logger.LogDebug($"[{Source}] Released {type.ToString().ToLower()} spawn NetId: {netId.Format()} ({type.Format()}, # {index.Format()}).");
        }
        else
            Logger.LogWarning($"Unable to release NetId to {type.ToString().ToLower()} spawn {netId.Format()} ({type.Format()}, # {index.Format()}), NetId not registered.", method: Source);
    }
    public static void RemoveRegionSpawnpoint(SpawnType type, RegionIdentifier id)
    {
        if (type is not SpawnType.Item and not SpawnType.Zombie)
            return;

        Dictionary<RegionIdentifier, NetId> assignments = GetRegionAssignments(type);

        if (assignments.TryGetValue(id, out NetId netId))
        {
            NetIdRegistry.Release(netId);
            assignments.Remove(id);
            if (Level.isLoaded)
                Logger.LogDebug($"[{Source}] Released {type.ToString().ToLower()} spawn NetId: {netId.Format()} ({type.Format()}, {id.Format()}).");
        }
        else
            Logger.LogWarning($"Unable to release NetId to {type.ToString().ToLower()} spawn {netId.Format()} ({type.Format()}, {id.Format()}), NetId not registered.", method: Source);
    }
    public static NetId AddIndexSpawnpoint(SpawnType type, int index)
    {
        if (type is not SpawnType.Animal and not SpawnType.Player and not SpawnType.Vehicle)
            return NetId.INVALID;

        NetId netId = NetIdRegistry.Claim();

        ClaimBasicNetId(index, type, netId);

        return netId;
    }
    public static NetId AddRegionSpawnpoint(SpawnType type, RegionIdentifier id)
    {
        if (type is not SpawnType.Item and not SpawnType.Zombie)
            return NetId.INVALID;

        NetId netId = NetIdRegistry.Claim();

        ClaimBasicNetId(id, type, netId);

        return netId;
    }
    public static bool TryGetAnimalSpawnpoint(NetId netId, out AnimalSpawnpoint spawnpoint)
    {
        object? value = NetIdRegistry.Get(netId);
        if (value is int index && LevelAnimals.spawns.Count > index)
        {
            spawnpoint = LevelAnimals.spawns[index];
            return true;
        }

        spawnpoint = null!;
        return false;
    }
    public static bool TryGetPlayerSpawnpoint(NetId netId, out PlayerSpawnpoint spawnpoint)
    {
        object? value = NetIdRegistry.Get(netId);
        if (value is int index && LevelPlayers.spawns.Count > index)
        {
            spawnpoint = LevelPlayers.spawns[index];
            return true;
        }

        spawnpoint = null!;
        return false;
    }
    public static bool TryGetVehicleSpawnpoint(NetId netId, out VehicleSpawnpoint spawnpoint)
    {
        object? value = NetIdRegistry.Get(netId);
        if (value is int index && LevelVehicles.spawns.Count > index)
        {
            spawnpoint = LevelVehicles.spawns[index];
            return true;
        }

        spawnpoint = null!;
        return false;
    }
    public static bool TryGetItemSpawnpoint(NetId netId, out ItemSpawnpoint spawnpoint)
    {
        object? value = NetIdRegistry.Get(netId);
        if (value is RegionIdentifier { IsInvalid: false } id)
        {
            List<ItemSpawnpoint> region = LevelItems.spawns[id.X, id.Y];
            if (region.Count > id.Index)
            {
                spawnpoint = region[id.Index];
                return true;
            }
        }

        spawnpoint = null!;
        return false;
    }
    public static bool TryGetZombieSpawnpoint(NetId netId, out ZombieSpawnpoint spawnpoint)
    {
        object? value = NetIdRegistry.Get(netId);
        if (value is RegionIdentifier { IsInvalid: false } id)
        {
            List<ZombieSpawnpoint> region = LevelZombies.spawns[id.X, id.Y];
            if (region.Count > id.Index)
            {
                spawnpoint = region[id.Index];
                return true;
            }
        }

        spawnpoint = null!;
        return false;
    }
    public static bool TryGetAnimalSpawnNetId(AnimalSpawnpoint spawnpoint, out NetId netId)
    {
        int index = LevelAnimals.spawns.IndexOf(spawnpoint);
        if (index == -1)
        {
            netId = NetId.INVALID;
            return false;
        }
        return TryGetAnimalSpawnNetId(index, out netId);
    }

    public static bool TryGetAnimalSpawnNetId(int index, out NetId netId)
    {
        if (!AnimalSpawnAssignments.TryGetValue(index, out netId))
        {
            netId = NetId.INVALID;
            return false;
        }

        return true;
    }
    public static bool TryGetPlayerSpawnNetId(PlayerSpawnpoint spawnpoint, out NetId netId)
    {
        int index = LevelPlayers.spawns.IndexOf(spawnpoint);
        if (index == -1)
        {
            netId = NetId.INVALID;
            return false;
        }
        return TryGetPlayerSpawnNetId(index, out netId);
    }

    public static bool TryGetPlayerSpawnNetId(int index, out NetId netId)
    {
        if (!PlayerSpawnAssignments.TryGetValue(index, out netId))
        {
            netId = NetId.INVALID;
            return false;
        }

        return true;
    }
    public static bool TryGetVehicleSpawnNetId(VehicleSpawnpoint spawnpoint, out NetId netId)
    {
        int index = LevelVehicles.spawns.IndexOf(spawnpoint);
        if (index == -1)
        {
            netId = NetId.INVALID;
            return false;
        }
        return TryGetVehicleSpawnNetId(index, out netId);
    }

    public static bool TryGetVehicleSpawnNetId(int index, out NetId netId)
    {
        if (!VehicleSpawnAssignments.TryGetValue(index, out netId))
        {
            netId = NetId.INVALID;
            return false;
        }

        return true;
    }
    public static bool TryGetItemSpawnNetId(ItemSpawnpoint spawnpoint, out NetId netId)
    {
        foreach (RegionCoord coord in RegionUtil.EnumerateRegions(spawnpoint.point))
        {
            List<ItemSpawnpoint> region = LevelItems.spawns[coord.x, coord.y];
            int index = region.IndexOf(spawnpoint);
            if (index == -1)
            {
                netId = NetId.INVALID;
                return false;
            }

            return TryGetItemSpawnNetId(new RegionIdentifier(coord, index), out netId);
        }

        netId = NetId.INVALID;
        return false;
    }
    public static bool TryGetItemSpawnNetId(RegionIdentifier id, out NetId netId)
    {
        if (!ItemSpawnAssignments.TryGetValue(id, out netId))
        {
            netId = NetId.INVALID;
            return false;
        }

        return true;
    }
    public static bool TryGetZombieSpawnNetId(ZombieSpawnpoint spawnpoint, out NetId netId)
    {
        foreach (RegionCoord coord in RegionUtil.EnumerateRegions(spawnpoint.point))
        {
            List<ZombieSpawnpoint> region = LevelZombies.spawns[coord.x, coord.y];
            int index = region.IndexOf(spawnpoint);
            if (index == -1)
            {
                netId = NetId.INVALID;
                return false;
            }

            return TryGetZombieSpawnNetId(new RegionIdentifier(coord, index), out netId);
        }

        netId = NetId.INVALID;
        return false;
    }
    public static bool TryGetZombieSpawnNetId(RegionIdentifier id, out NetId netId)
    {
        if (!ZombieSpawnAssignments.TryGetValue(id, out netId))
        {
            netId = NetId.INVALID;
            return false;
        }

        return true;
    }
#if SERVER
    internal static void AssignExisting()
    {
        AnimalSpawnAssignments.Clear();
        PlayerSpawnAssignments.Clear();
        VehicleSpawnAssignments.Clear();
        ItemSpawnAssignments.Clear();
        ZombieSpawnAssignments.Clear();

        List<AnimalSpawnpoint> animals = LevelAnimals.spawns;

        int count = 0;

        for (; count < animals.Count; ++count)
            AddIndexSpawnpoint(SpawnType.Animal, count);
        
        Logger.LogInfo($"[{Source}] Assigned NetIds for {count.Format()} animal spawnpoint{count.S()}.");

        List<PlayerSpawnpoint> players = LevelPlayers.spawns;

        count = 0;

        for (; count < players.Count; ++count)
            AddIndexSpawnpoint(SpawnType.Player, count);
        
        Logger.LogInfo($"[{Source}] Assigned NetIds for {count.Format()} player spawnpoint{count.S()}.");

        List<VehicleSpawnpoint> vehicles = LevelVehicles.spawns;

        count = 0;

        for (; count < vehicles.Count; ++count)
            AddIndexSpawnpoint(SpawnType.Vehicle, count);
        
        Logger.LogInfo($"[{Source}] Assigned NetIds for {count.Format()} vehicle spawnpoint{count.S()}.");

        count = 0;

        foreach (RegionCoord region in RegionUtil.LinearEnumerateRegions())
        {
            List<ItemSpawnpoint> items = LevelItems.spawns[region.x, region.y];
            for (int i = 0; i < items.Count; ++i)
                AddRegionSpawnpoint(SpawnType.Item, new RegionIdentifier(region, i));
            count += items.Count;
        }

        Logger.LogInfo($"[{Source}] Assigned NetIds for {count.Format()} item spawnpoint{count.S()}.");

        count = 0;

        foreach (RegionCoord region in RegionUtil.LinearEnumerateRegions())
        {
            List<ZombieSpawnpoint> zombies = LevelZombies.spawns[region.x, region.y];
            for (int i = 0; i < zombies.Count; ++i)
                AddRegionSpawnpoint(SpawnType.Zombie, new RegionIdentifier(region, i));
            count += zombies.Count;
        }

        Logger.LogInfo($"[{Source}] Assigned NetIds for {count.Format()} zombie spawnpoint{count.S()}.");
    }
#endif
    private static void ClaimBasicNetId(int index, SpawnType typeChecked, NetId netId)
    {
        if (!netId.IsNull() && NetIdRegistry.Release(netId))
        {
            if (Level.isLoaded)
                Logger.LogDebug($"[{Source}] Released old NetId pairing: {netId.Format()}.");
        }

        if (typeChecked == SpawnType.Animal)
        {
            if (AnimalSpawnAssignments.TryGetValue(index, out NetId old))
                AnimalSpawnAssignments.Remove(index);

            if (NetIdRegistry.Release(old))
            {
                if (Level.isLoaded)
                    Logger.LogDebug($"[{Source}] Released old animal NetId pairing: {old.Format()}.");
            }

            if (!netId.IsNull())
                AnimalSpawnAssignments[index] = netId;
        }
        else if (typeChecked == SpawnType.Player)
        {
            if (PlayerSpawnAssignments.TryGetValue(index, out NetId old))
                PlayerSpawnAssignments.Remove(index);

            if (NetIdRegistry.Release(old))
            {
                if (Level.isLoaded)
                    Logger.LogDebug($"[{Source}] Released old player NetId pairing: {old.Format()}.");
            }

            if (!netId.IsNull())
                PlayerSpawnAssignments[index] = netId;
        }
        else
        {
            if (VehicleSpawnAssignments.TryGetValue(index, out NetId old))
                VehicleSpawnAssignments.Remove(index);

            if (NetIdRegistry.Release(old))
            {
                if (Level.isLoaded)
                    Logger.LogDebug($"[{Source}] Released old vehicle NetId pairing: {old.Format()}.");
            }

            if (!netId.IsNull())
                VehicleSpawnAssignments[index] = netId;
        }

        if (!netId.IsNull())
        {
            NetIdRegistry.Assign(netId, index);

            if (Level.isLoaded)
                Logger.LogDebug($"[{Source}] Claimed new NetId: {netId.Format()} @ {typeChecked.Format()} (# {index.Format()}).");
        }
        else
        {
            Logger.LogDebug($"[{Source}] Released NetId: {netId.Format()} @ {typeChecked.Format()} (# {index.Format()}).");
        }
    }
    private static void ClaimBasicNetId(RegionIdentifier id, SpawnType typeChecked, NetId netId)
    {
        if (!netId.IsNull() && NetIdRegistry.Release(netId))
        {
            if (Level.isLoaded)
                Logger.LogDebug($"[{Source}] Released old NetId pairing: {netId.Format()}.");
        }

        if (typeChecked == SpawnType.Item)
        {
            if (ItemSpawnAssignments.TryGetValue(id, out NetId old))
                ItemSpawnAssignments.Remove(id);

            if (NetIdRegistry.Release(old))
            {
                if (Level.isLoaded)
                    Logger.LogDebug($"[{Source}] Released old item NetId pairing: {old.Format()}.");
            }

            if (!netId.IsNull())
                ItemSpawnAssignments[id] = netId;
        }
        else
        {
            if (ZombieSpawnAssignments.TryGetValue(id, out NetId old))
                ZombieSpawnAssignments.Remove(id);

            if (NetIdRegistry.Release(old))
            {
                if (Level.isLoaded)
                    Logger.LogDebug($"[{Source}] Released old zombie NetId pairing: {old.Format()}.");
            }

            if (!netId.IsNull())
                ZombieSpawnAssignments[id] = netId;
        }

        if (!netId.IsNull())
        {
            NetIdRegistry.Assign(netId, id);

            if (Level.isLoaded)
                Logger.LogDebug($"[{Source}] Claimed new NetId: {netId.Format()} @ {typeChecked.Format()} ({id.Format()}).");
        }
        else
        {
            Logger.LogDebug($"[{Source}] Released NetId: {netId.Format()} @ {typeChecked.Format()} ({id.Format()}).");
        }
    }
#if CLIENT
    public void LoadData(SpawnpointNetIdReplicatedLevelData data)
    {
        NetId[] netIds = data.NetIds;
        int[] indexes = data.Indexes;

        int index = 0;

        int maxIndex = data.IndexPlayer;
        for (; index < maxIndex; ++index)
        {
            ClaimBasicNetId(indexes[index], SpawnType.Animal, netIds[index]);
        }

        maxIndex = data.IndexVehicle;
        for (; index < maxIndex; ++index)
        {
            ClaimBasicNetId(indexes[index], SpawnType.Player, netIds[index]);
        }

        maxIndex = data.IndexItem;
        for (; index < maxIndex; ++index)
        {
            ClaimBasicNetId(indexes[index], SpawnType.Vehicle, netIds[index]);
        }

        maxIndex = data.IndexZombie;
        for (; index < maxIndex; ++index)
        {
            RegionIdentifier id = RegionIdentifier.CreateUnsafe(indexes[index]);
            if (!id.IsInvalid)
                ClaimBasicNetId(id, SpawnType.Item, netIds[index]);
            else
                Logger.LogWarning($"Received item spawn with invalid region identifier: {id.Format()}.");
        }

        maxIndex = data.Count;
        for (; index < maxIndex; ++index)
        {
            RegionIdentifier id = RegionIdentifier.CreateUnsafe(indexes[index]);
            if (!id.IsInvalid)
                ClaimBasicNetId(id, SpawnType.Zombie, netIds[index]);
            else
                Logger.LogWarning($"Received zombie spawn with invalid region identifier: {id.Format()}.");
        }
    }
#elif SERVER       
    public SpawnpointNetIdReplicatedLevelData SaveData()
    {
        SpawnpointNetIdReplicatedLevelData data = new SpawnpointNetIdReplicatedLevelData();
        NetId[] netIds = new NetId[AnimalSpawnAssignments.Count + PlayerSpawnAssignments.Count + VehicleSpawnAssignments.Count + ItemSpawnAssignments.Count + ZombieSpawnAssignments.Count];
        int[] indexes = new int[netIds.Length];

        data.NetIds = netIds;
        data.Indexes = indexes;
        data.Count = netIds.Length;

        int index = 0;

        foreach (KeyValuePair<int, NetId> lvlObject in AnimalSpawnAssignments)
        {
            netIds[index] = lvlObject.Value;
            indexes[index] = lvlObject.Key;
            ++index;
        }

        data.IndexPlayer = AnimalSpawnAssignments.Count;
        foreach (KeyValuePair<int, NetId> lvlObject in PlayerSpawnAssignments)
        {
            netIds[index] = lvlObject.Value;
            indexes[index] = lvlObject.Key;
            ++index;
        }

        data.IndexVehicle = data.IndexPlayer + PlayerSpawnAssignments.Count;
        foreach (KeyValuePair<int, NetId> lvlObject in VehicleSpawnAssignments)
        {
            netIds[index] = lvlObject.Value;
            indexes[index] = lvlObject.Key;
            ++index;
        }

        data.IndexItem = data.IndexVehicle + VehicleSpawnAssignments.Count;
        foreach (KeyValuePair<RegionIdentifier, NetId> lvlObject in ItemSpawnAssignments)
        {
            netIds[index] = lvlObject.Value;
            indexes[index] = lvlObject.Key.Raw;
            ++index;
        }

        data.IndexZombie = data.IndexItem + ItemSpawnAssignments.Count;
        foreach (KeyValuePair<RegionIdentifier, NetId> lvlObject in ZombieSpawnAssignments)
        {
            netIds[index] = lvlObject.Value;
            indexes[index] = lvlObject.Key.Raw;
            ++index;
        }

        return data;
    }
#endif
    public SpawnpointNetIdReplicatedLevelData ReadData(ByteReader reader, ushort version)
    {
        SpawnpointNetIdReplicatedLevelData data = new SpawnpointNetIdReplicatedLevelData
        {
            IndexPlayer = reader.ReadInt32(),
            IndexVehicle = reader.ReadInt32(),
            IndexItem = reader.ReadInt32(),
            IndexZombie = reader.ReadInt32(),
            Count = reader.ReadInt32()
        };

        int dataCount = data.Count;
        NetId[] netIds = new NetId[dataCount];
        int[] indexes = new int[dataCount];

        for (int i = 0; i < dataCount; ++i)
            data.Indexes[i] = reader.ReadInt32();
        for (int i = 0; i < dataCount; ++i)
            data.NetIds[i] = reader.ReadNetId();

        data.NetIds = netIds;
        data.Indexes = indexes;

        return data;
    }
    public void WriteData(ByteWriter writer, SpawnpointNetIdReplicatedLevelData data)
    {
        int dataCount = data.Count;

        writer.Write(data.IndexPlayer);
        writer.Write(data.IndexVehicle);
        writer.Write(data.IndexItem);
        writer.Write(data.IndexZombie);
        writer.Write(dataCount);

        for (int i = 0; i < dataCount; ++i)
            writer.Write(data.Indexes[i]);
        for (int i = 0; i < dataCount; ++i)
            writer.Write(data.NetIds[i]);
    }
}

#nullable disable
public class SpawnpointNetIdReplicatedLevelData
{
    public int[] Indexes { get; set; }
    public NetId[] NetIds { get; set; }
    public int IndexPlayer { get; set; }
    public int IndexVehicle { get; set; }
    public int IndexItem { get; set; }
    public int IndexZombie { get; set; }
    public int Count { get; set; }
}

#nullable restore