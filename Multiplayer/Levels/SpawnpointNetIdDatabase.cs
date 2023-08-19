﻿using DevkitServer.Core.Tools;
using DevkitServer.Levels;
using DevkitServer.Models;
using DevkitServer.Multiplayer.Networking;
using DevkitServer.Util.Encoding;
using DevkitServer.Util.Region;

namespace DevkitServer.Multiplayer.Levels;
internal static class SpawnpointNetIdDatabase
{
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
    internal static NetCall<uint, NetId> SendBindHierarchyItem = new NetCall<uint, NetId>(NetCalls.SendBindHierarchyItem);
#if SERVER
    private static bool _initialLoaded;
#endif
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
        _initialLoaded = false;
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
    [NetCall(NetCallSource.FromServer, NetCalls.SendBindIndexSpawnpoint)]
    private static StandardErrorCode ReceiveBindIndexSpawnpoint(MessageContext ctx, byte spawnType, int index, NetId netId)
    {
        SpawnType type = (SpawnType)spawnType;
        if (type is not SpawnType.Animal and not SpawnType.Player and not SpawnType.Vehicle)
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
#if CLIENT
    public static void LoadFromLevelData()
    {
        LevelData data = EditorLevel.ServerPendingLevelData ?? throw new InvalidOperationException("Level data not loaded.");
        NetId[] netIds = data.SpawnNetIds;
        int[] indexes = data.SpawnIndexes;
        
        int index = 0;
        for (; index < data.SpawnIndexPlayer; ++index)
        {
            ClaimBasicNetId(indexes[index], SpawnType.Animal, netIds[index]);
        }
        for (; index < data.SpawnIndexVehicle; ++index)
        {
            ClaimBasicNetId(indexes[index], SpawnType.Player, netIds[index]);
        }
        for (; index < data.SpawnIndexItem; ++index)
        {
            ClaimBasicNetId(indexes[index], SpawnType.Vehicle, netIds[index]);
        }
        for (; index < data.SpawnIndexZombie; ++index)
        {
            ClaimBasicNetId(new RegionIdentifier(indexes[index]), SpawnType.Item, netIds[index]);
        }
        for (; index < data.SpawnCount; ++index)
        {
            ClaimBasicNetId(new RegionIdentifier(indexes[index]), SpawnType.Zombie, netIds[index]);
        }
    }
#endif
#if SERVER
    internal static void AssignExisting()
    {
        _initialLoaded = true;

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
    public static void GatherData(LevelData data)
    {
        NetId[] netIds = new NetId[AnimalSpawnAssignments.Count + PlayerSpawnAssignments.Count + VehicleSpawnAssignments.Count + ItemSpawnAssignments.Count + ZombieSpawnAssignments.Count];
        int[] indexes = new int[netIds.Length];

        data.SpawnNetIds = netIds;
        data.SpawnIndexes = indexes;
        data.SpawnCount = netIds.Length;

        int index = 0;

        foreach (KeyValuePair<int, NetId> lvlObject in AnimalSpawnAssignments)
        {
            netIds[index] = lvlObject.Value;
            indexes[index] = lvlObject.Key;
            ++index;
        }

        data.SpawnIndexPlayer = AnimalSpawnAssignments.Count;
        foreach (KeyValuePair<int, NetId> lvlObject in PlayerSpawnAssignments)
        {
            netIds[index] = lvlObject.Value;
            indexes[index] = lvlObject.Key;
            ++index;
        }

        data.SpawnIndexVehicle = data.SpawnIndexPlayer + PlayerSpawnAssignments.Count;
        foreach (KeyValuePair<int, NetId> lvlObject in VehicleSpawnAssignments)
        {
            netIds[index] = lvlObject.Value;
            indexes[index] = lvlObject.Key;
            ++index;
        }

        data.SpawnIndexItem = data.SpawnIndexVehicle + VehicleSpawnAssignments.Count;
        foreach (KeyValuePair<RegionIdentifier, NetId> lvlObject in ItemSpawnAssignments)
        {
            netIds[index] = lvlObject.Value;
            indexes[index] = lvlObject.Key.Raw;
            ++index;
        }

        data.SpawnIndexZombie = data.SpawnIndexItem + ItemSpawnAssignments.Count;
        foreach (KeyValuePair<RegionIdentifier, NetId> lvlObject in ZombieSpawnAssignments)
        {
            netIds[index] = lvlObject.Value;
            indexes[index] = lvlObject.Key.Raw;
            ++index;
        }
    }
    internal static void ReadToDatabase(ByteReader reader, LevelData data)
    {
        data.SpawnIndexPlayer = reader.ReadInt32();
        data.SpawnIndexVehicle = reader.ReadInt32();
        data.SpawnIndexItem = reader.ReadInt32();
        data.SpawnIndexZombie = reader.ReadInt32();
        data.SpawnCount = reader.ReadInt32();

        data.SpawnNetIds = new NetId[data.SpawnCount];
        data.SpawnIndexes = new int[data.SpawnCount];

        for (int i = 0; i < data.SpawnCount; ++i)
        {
            data.SpawnIndexes[i] = reader.ReadInt32();
            data.SpawnNetIds[i] = reader.ReadNetId();
        }
    }
    internal static void WriteToDatabase(ByteWriter writer, LevelData data)
    {
        writer.Write(data.SpawnIndexPlayer);
        writer.Write(data.SpawnIndexVehicle);
        writer.Write(data.SpawnIndexItem);
        writer.Write(data.SpawnIndexZombie);
        writer.Write(data.SpawnCount);

        for (int i = 0; i < data.SpawnCount; ++i)
        {
            writer.Write(data.SpawnIndexes[i]);
            writer.Write(data.SpawnNetIds[i]);
        }
    }
}
