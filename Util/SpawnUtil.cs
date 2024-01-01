using DevkitServer.API.Iterators;
using DevkitServer.Models;
using DevkitServer.Util.Region;
using DevkitServer.API;
#if CLIENT
using DevkitServer.Core;
using DevkitServer.API.Devkit.Spawns;
#endif

namespace DevkitServer.Util;

public delegate void AnimalSpawnpointMoved(AnimalSpawnpoint point, Vector3 fromPosition, Vector3 toPosition);
public delegate void VehicleSpawnpointMoved(VehicleSpawnpoint point, Vector3 fromPosition, Vector3 toPosition, float fromAngle, float toAngle);
public delegate void PlayerSpawnpointMoved(PlayerSpawnpoint point, Vector3 fromPosition, Vector3 toPosition, float fromAngle, float toAngle);
public delegate void ItemSpawnpointMoved(ItemSpawnpoint point, RegionIdentifier region, Vector3 fromPosition, Vector3 toPosition);
public delegate void ZombieSpawnpointMoved(ZombieSpawnpoint point, RegionIdentifier region, Vector3 fromPosition, Vector3 toPosition);

public delegate void AnimalSpawnpointIndexUpdated(AnimalSpawnpoint point, int fromIndex, int toIndex);
public delegate void VehicleSpawnpointIndexUpdated(VehicleSpawnpoint point, int fromIndex, int toIndex);
public delegate void PlayerSpawnpointIndexUpdated(PlayerSpawnpoint point, int fromIndex, int toIndex);
public delegate void ItemSpawnpointRegionUpdated(ItemSpawnpoint point, RegionIdentifier fromRegion, RegionIdentifier toRegion);
public delegate void ZombieSpawnpointRegionUpdated(ZombieSpawnpoint point, RegionIdentifier fromRegion, RegionIdentifier toRegion);

public delegate void AnimalSpawnpointArgs(AnimalSpawnpoint point, int index);
public delegate void VehicleSpawnpointArgs(VehicleSpawnpoint point, int index);
public delegate void PlayerSpawnpointArgs(PlayerSpawnpoint point, int index);
public delegate void ItemSpawnpointArgs(ItemSpawnpoint point, RegionIdentifier region);
public delegate void ZombieSpawnpointArgs(ZombieSpawnpoint point, RegionIdentifier region);

public delegate void PlayerSpawnpointIsAlternateArgs(PlayerSpawnpoint point, int index, bool isAlternate);

/// <summary>
/// Various utilities for working with the animal, vehicle, player, item, and zombie spawn systems.
/// </summary>
public static class SpawnUtil
{
    internal static readonly CachedMulticastEvent<AnimalSpawnpointMoved> EventOnAnimalSpawnpointMoved = new CachedMulticastEvent<AnimalSpawnpointMoved>(typeof(SpawnUtil), nameof(OnAnimalSpawnpointMoved));
    internal static readonly CachedMulticastEvent<VehicleSpawnpointMoved> EventOnVehicleSpawnpointMoved = new CachedMulticastEvent<VehicleSpawnpointMoved>(typeof(SpawnUtil), nameof(OnVehicleSpawnpointMoved));
    internal static readonly CachedMulticastEvent<PlayerSpawnpointMoved> EventOnPlayerSpawnpointMoved = new CachedMulticastEvent<PlayerSpawnpointMoved>(typeof(SpawnUtil), nameof(OnPlayerSpawnpointMoved));
    internal static readonly CachedMulticastEvent<ItemSpawnpointMoved> EventOnItemSpawnpointMoved = new CachedMulticastEvent<ItemSpawnpointMoved>(typeof(SpawnUtil), nameof(OnItemSpawnpointMoved));
    internal static readonly CachedMulticastEvent<ZombieSpawnpointMoved> EventOnZombieSpawnpointMoved = new CachedMulticastEvent<ZombieSpawnpointMoved>(typeof(SpawnUtil), nameof(OnZombieSpawnpointMoved));
    
    internal static readonly CachedMulticastEvent<AnimalSpawnpointIndexUpdated> EventOnAnimalSpawnpointIndexUpdated = new CachedMulticastEvent<AnimalSpawnpointIndexUpdated>(typeof(SpawnUtil), nameof(OnAnimalSpawnpointIndexUpdated));
    internal static readonly CachedMulticastEvent<VehicleSpawnpointIndexUpdated> EventOnVehicleSpawnpointIndexUpdated = new CachedMulticastEvent<VehicleSpawnpointIndexUpdated>(typeof(SpawnUtil), nameof(OnVehicleSpawnpointIndexUpdated));
    internal static readonly CachedMulticastEvent<PlayerSpawnpointIndexUpdated> EventOnPlayerSpawnpointIndexUpdated = new CachedMulticastEvent<PlayerSpawnpointIndexUpdated>(typeof(SpawnUtil), nameof(OnPlayerSpawnpointIndexUpdated));
    internal static readonly CachedMulticastEvent<ItemSpawnpointRegionUpdated> EventOnItemSpawnpointRegionUpdated = new CachedMulticastEvent<ItemSpawnpointRegionUpdated>(typeof(SpawnUtil), nameof(OnItemSpawnpointRegionUpdated));
    internal static readonly CachedMulticastEvent<ZombieSpawnpointRegionUpdated> EventOnZombieSpawnpointRegionUpdated = new CachedMulticastEvent<ZombieSpawnpointRegionUpdated>(typeof(SpawnUtil), nameof(OnZombieSpawnpointRegionUpdated));

    internal static readonly CachedMulticastEvent<AnimalSpawnpointArgs> EventOnAnimalSpawnpointAdded = new CachedMulticastEvent<AnimalSpawnpointArgs>(typeof(SpawnUtil), nameof(OnAnimalSpawnpointAdded));
    internal static readonly CachedMulticastEvent<VehicleSpawnpointArgs> EventOnVehicleSpawnpointAdded = new CachedMulticastEvent<VehicleSpawnpointArgs>(typeof(SpawnUtil), nameof(OnVehicleSpawnpointAdded));
    internal static readonly CachedMulticastEvent<PlayerSpawnpointArgs> EventOnPlayerSpawnpointAdded = new CachedMulticastEvent<PlayerSpawnpointArgs>(typeof(SpawnUtil), nameof(OnPlayerSpawnpointAdded));
    internal static readonly CachedMulticastEvent<ItemSpawnpointArgs> EventOnItemSpawnpointAdded = new CachedMulticastEvent<ItemSpawnpointArgs>(typeof(SpawnUtil), nameof(OnItemSpawnpointAdded));
    internal static readonly CachedMulticastEvent<ZombieSpawnpointArgs> EventOnZombieSpawnpointAdded = new CachedMulticastEvent<ZombieSpawnpointArgs>(typeof(SpawnUtil), nameof(OnZombieSpawnpointAdded));
    
    internal static readonly CachedMulticastEvent<AnimalSpawnpointArgs> EventOnAnimalSpawnpointRemoved = new CachedMulticastEvent<AnimalSpawnpointArgs>(typeof(SpawnUtil), nameof(OnAnimalSpawnpointRemoved));
    internal static readonly CachedMulticastEvent<VehicleSpawnpointArgs> EventOnVehicleSpawnpointRemoved = new CachedMulticastEvent<VehicleSpawnpointArgs>(typeof(SpawnUtil), nameof(OnVehicleSpawnpointRemoved));
    internal static readonly CachedMulticastEvent<PlayerSpawnpointArgs> EventOnPlayerSpawnpointRemoved = new CachedMulticastEvent<PlayerSpawnpointArgs>(typeof(SpawnUtil), nameof(OnPlayerSpawnpointRemoved));
    internal static readonly CachedMulticastEvent<ItemSpawnpointArgs> EventOnItemSpawnpointRemoved = new CachedMulticastEvent<ItemSpawnpointArgs>(typeof(SpawnUtil), nameof(OnItemSpawnpointRemoved));
    internal static readonly CachedMulticastEvent<ZombieSpawnpointArgs> EventOnZombieSpawnpointRemoved = new CachedMulticastEvent<ZombieSpawnpointArgs>(typeof(SpawnUtil), nameof(OnZombieSpawnpointRemoved));

    internal static readonly CachedMulticastEvent<AnimalSpawnpointArgs> EventOnAnimalSpawnTableChanged = new CachedMulticastEvent<AnimalSpawnpointArgs>(typeof(SpawnTableUtil), nameof(OnAnimalSpawnTableChanged));
    internal static readonly CachedMulticastEvent<VehicleSpawnpointArgs> EventOnVehicleSpawnTableChanged = new CachedMulticastEvent<VehicleSpawnpointArgs>(typeof(SpawnTableUtil), nameof(OnVehicleSpawnTableChanged));
    internal static readonly CachedMulticastEvent<ItemSpawnpointArgs> EventOnItemSpawnTableChanged = new CachedMulticastEvent<ItemSpawnpointArgs>(typeof(SpawnTableUtil), nameof(OnItemSpawnTableChanged));
    internal static readonly CachedMulticastEvent<ZombieSpawnpointArgs> EventOnZombieSpawnTableChanged = new CachedMulticastEvent<ZombieSpawnpointArgs>(typeof(SpawnTableUtil), nameof(OnZombieSpawnTableChanged));
    
    internal static readonly CachedMulticastEvent<PlayerSpawnpointIsAlternateArgs> EventOnPlayerSpawnpointIsAlternateChanged = new CachedMulticastEvent<PlayerSpawnpointIsAlternateArgs>(typeof(SpawnTableUtil), nameof(OnPlayerSpawnpointIsAlternateChanged));

    private static readonly InstanceSetter<AnimalSpawnpoint, Vector3>? SetAnimalSpawnpointPoint = Accessor.GenerateInstanceSetter<AnimalSpawnpoint, Vector3>("_point");
    private static readonly InstanceSetter<VehicleSpawnpoint, Vector3>? SetVehicleSpawnpointPoint = Accessor.GenerateInstanceSetter<VehicleSpawnpoint, Vector3>("_point");
    private static readonly InstanceSetter<PlayerSpawnpoint, Vector3>? SetPlayerSpawnpointPoint = Accessor.GenerateInstanceSetter<PlayerSpawnpoint, Vector3>("_point");
    private static readonly InstanceSetter<ItemSpawnpoint, Vector3>? SetItemSpawnpointPoint = Accessor.GenerateInstanceSetter<ItemSpawnpoint, Vector3>("_point");
    private static readonly InstanceSetter<ZombieSpawnpoint, Vector3>? SetZombieSpawnpointPoint = Accessor.GenerateInstanceSetter<ZombieSpawnpoint, Vector3>("_point");

    private static readonly InstanceSetter<VehicleSpawnpoint, float>? SetVehicleSpawnpointAngle = Accessor.GenerateInstanceSetter<VehicleSpawnpoint, float>("_angle");
    private static readonly InstanceSetter<PlayerSpawnpoint, float>? SetPlayerSpawnpointAngle = Accessor.GenerateInstanceSetter<PlayerSpawnpoint, float>("_angle");

    private static readonly InstanceSetter<PlayerSpawnpoint, bool>? SetPlayerSpawnpointIsAlternate = Accessor.GenerateInstanceSetter<PlayerSpawnpoint, bool>("_isAlt");

    /// <summary>
    /// Called when an <see cref="AnimalSpawnpoint"/> is moved.
    /// </summary>
    public static event AnimalSpawnpointMoved OnAnimalSpawnpointMoved
    {
        add => EventOnAnimalSpawnpointMoved.Add(value);
        remove => EventOnAnimalSpawnpointMoved.Remove(value);
    }

    /// <summary>
    /// Called when a <see cref="VehicleSpawnpoint"/> is moved.
    /// </summary>
    public static event VehicleSpawnpointMoved OnVehicleSpawnpointMoved
    {
        add => EventOnVehicleSpawnpointMoved.Add(value);
        remove => EventOnVehicleSpawnpointMoved.Remove(value);
    }

    /// <summary>
    /// Called when a <see cref="PlayerSpawnpoint"/> is moved.
    /// </summary>
    public static event PlayerSpawnpointMoved OnPlayerSpawnpointMoved
    {
        add => EventOnPlayerSpawnpointMoved.Add(value);
        remove => EventOnPlayerSpawnpointMoved.Remove(value);
    }

    /// <summary>
    /// Called when an <see cref="ItemSpawnpoint"/> is moved.
    /// </summary>
    public static event ItemSpawnpointMoved OnItemSpawnpointMoved
    {
        add => EventOnItemSpawnpointMoved.Add(value);
        remove => EventOnItemSpawnpointMoved.Remove(value);
    }

    /// <summary>
    /// Called when a <see cref="ZombieSpawnpoint"/> is moved.
    /// </summary>
    public static event ZombieSpawnpointMoved OnZombieSpawnpointMoved
    {
        add => EventOnZombieSpawnpointMoved.Add(value);
        remove => EventOnZombieSpawnpointMoved.Remove(value);
    }


    /// <summary>
    /// Called when an <see cref="AnimalSpawnpoint"/>'s index changes.
    /// </summary>
    public static event AnimalSpawnpointIndexUpdated OnAnimalSpawnpointIndexUpdated
    {
        add => EventOnAnimalSpawnpointIndexUpdated.Add(value);
        remove => EventOnAnimalSpawnpointIndexUpdated.Remove(value);
    }

    /// <summary>
    /// Called when an <see cref="VehicleSpawnpoint"/>'s index changes.
    /// </summary>
    public static event VehicleSpawnpointIndexUpdated OnVehicleSpawnpointIndexUpdated
    {
        add => EventOnVehicleSpawnpointIndexUpdated.Add(value);
        remove => EventOnVehicleSpawnpointIndexUpdated.Remove(value);
    }

    /// <summary>
    /// Called when an <see cref="PlayerSpawnpoint"/>'s index changes.
    /// </summary>
    public static event PlayerSpawnpointIndexUpdated OnPlayerSpawnpointIndexUpdated
    {
        add => EventOnPlayerSpawnpointIndexUpdated.Add(value);
        remove => EventOnPlayerSpawnpointIndexUpdated.Remove(value);
    }

    /// <summary>
    /// Called when an <see cref="ItemSpawnpoint"/>'s region changes.
    /// </summary>
    public static event ItemSpawnpointRegionUpdated OnItemSpawnpointRegionUpdated
    {
        add => EventOnItemSpawnpointRegionUpdated.Add(value);
        remove => EventOnItemSpawnpointRegionUpdated.Remove(value);
    }

    /// <summary>
    /// Called when a <see cref="ZombieSpawnpoint"/>'s region changes.
    /// </summary>
    public static event ZombieSpawnpointRegionUpdated OnZombieSpawnpointRegionUpdated
    {
        add => EventOnZombieSpawnpointRegionUpdated.Add(value);
        remove => EventOnZombieSpawnpointRegionUpdated.Remove(value);
    }


    /// <summary>
    /// Called when an <see cref="AnimalSpawnpoint"/> is added.
    /// </summary>
    public static event AnimalSpawnpointArgs OnAnimalSpawnpointAdded
    {
        add => EventOnAnimalSpawnpointAdded.Add(value);
        remove => EventOnAnimalSpawnpointAdded.Remove(value);
    }

    /// <summary>
    /// Called when a <see cref="VehicleSpawnpoint"/> is added.
    /// </summary>
    public static event VehicleSpawnpointArgs OnVehicleSpawnpointAdded
    {
        add => EventOnVehicleSpawnpointAdded.Add(value);
        remove => EventOnVehicleSpawnpointAdded.Remove(value);
    }

    /// <summary>
    /// Called when a <see cref="PlayerSpawnpoint"/> is added.
    /// </summary>
    public static event PlayerSpawnpointArgs OnPlayerSpawnpointAdded
    {
        add => EventOnPlayerSpawnpointAdded.Add(value);
        remove => EventOnPlayerSpawnpointAdded.Remove(value);
    }

    /// <summary>
    /// Called when an <see cref="ItemSpawnpoint"/> is added.
    /// </summary>
    public static event ItemSpawnpointArgs OnItemSpawnpointAdded
    {
        add => EventOnItemSpawnpointAdded.Add(value);
        remove => EventOnItemSpawnpointAdded.Remove(value);
    }

    /// <summary>
    /// Called when a <see cref="ZombieSpawnpoint"/> is added.
    /// </summary>
    public static event ZombieSpawnpointArgs OnZombieSpawnpointAdded
    {
        add => EventOnZombieSpawnpointAdded.Add(value);
        remove => EventOnZombieSpawnpointAdded.Remove(value);
    }


    /// <summary>
    /// Called when an <see cref="AnimalSpawnpoint"/> is removed.
    /// </summary>
    public static event AnimalSpawnpointArgs OnAnimalSpawnpointRemoved
    {
        add => EventOnAnimalSpawnpointRemoved.Add(value);
        remove => EventOnAnimalSpawnpointRemoved.Remove(value);
    }

    /// <summary>
    /// Called when a <see cref="VehicleSpawnpoint"/> is removed.
    /// </summary>
    public static event VehicleSpawnpointArgs OnVehicleSpawnpointRemoved
    {
        add => EventOnVehicleSpawnpointRemoved.Add(value);
        remove => EventOnVehicleSpawnpointRemoved.Remove(value);
    }

    /// <summary>
    /// Called when a <see cref="PlayerSpawnpoint"/> is removed.
    /// </summary>
    public static event PlayerSpawnpointArgs OnPlayerSpawnpointRemoved
    {
        add => EventOnPlayerSpawnpointRemoved.Add(value);
        remove => EventOnPlayerSpawnpointRemoved.Remove(value);
    }

    /// <summary>
    /// Called when an <see cref="ItemSpawnpoint"/> is removed.
    /// </summary>
    public static event ItemSpawnpointArgs OnItemSpawnpointRemoved
    {
        add => EventOnItemSpawnpointRemoved.Add(value);
        remove => EventOnItemSpawnpointRemoved.Remove(value);
    }

    /// <summary>
    /// Called when a <see cref="ZombieSpawnpoint"/> is removed.
    /// </summary>
    public static event ZombieSpawnpointArgs OnZombieSpawnpointRemoved
    {
        add => EventOnZombieSpawnpointRemoved.Add(value);
        remove => EventOnZombieSpawnpointRemoved.Remove(value);
    }

    /// <summary>
    /// Called when an <see cref="AnimalSpawnpoint"/>'s <see cref="AnimalTable"/> is changed.
    /// </summary>
    public static event AnimalSpawnpointArgs OnAnimalSpawnTableChanged
    {
        add => EventOnAnimalSpawnTableChanged.Add(value);
        remove => EventOnAnimalSpawnTableChanged.Remove(value);
    }

    /// <summary>
    /// Called when an <see cref="ItemSpawnpoint"/>'s <see cref="ItemTable"/> is changed.
    /// </summary>
    public static event ItemSpawnpointArgs OnItemSpawnTableChanged
    {
        add => EventOnItemSpawnTableChanged.Add(value);
        remove => EventOnItemSpawnTableChanged.Remove(value);
    }

    /// <summary>
    /// Called when a <see cref="VehicleSpawnpoint"/>'s <see cref="VehicleTable"/> is changed.
    /// </summary>
    public static event VehicleSpawnpointArgs OnVehicleSpawnTableChanged
    {
        add => EventOnVehicleSpawnTableChanged.Add(value);
        remove => EventOnVehicleSpawnTableChanged.Remove(value);
    }

    /// <summary>
    /// Called when a <see cref="ZombieSpawnpoint"/>'s <see cref="ZombieTable"/> is changed.
    /// </summary>
    public static event ZombieSpawnpointArgs OnZombieSpawnTableChanged
    {
        add => EventOnZombieSpawnTableChanged.Add(value);
        remove => EventOnZombieSpawnTableChanged.Remove(value);
    }

    /// <summary>
    /// Called when a <see cref="PlayerSpawnpoint"/>'s alternate setting is changed.
    /// </summary>
    public static event PlayerSpawnpointIsAlternateArgs OnPlayerSpawnpointIsAlternateChanged
    {
        add => EventOnPlayerSpawnpointIsAlternateChanged.Add(value);
        remove => EventOnPlayerSpawnpointIsAlternateChanged.Remove(value);
    }

    /// <summary>
    /// Locally sets the <see cref="PlayerSpawnpoint.isAlt"/> backing field to <paramref name="isAlternate"/>.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="MemberAccessException">Reflection failure.</exception>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static void SetIsAlternateLocal(this PlayerSpawnpoint spawn, bool isAlternate)
    {
        ThreadUtil.assertIsGameThread();

        if (SetPlayerSpawnpointIsAlternate == null)
            throw new MemberAccessException("Instance setter for PlayerSpawnpoint.isAlt is not valid.");

        int index = LevelPlayers.spawns.IndexOf(spawn);
        bool oldIsAlternate = spawn.isAlt;

#if CLIENT
        if (oldIsAlternate != isAlternate && spawn.node != null && spawn.node.TryGetComponent(out PlayerSpawnpointNode node))
        {
            node.Color = isAlternate ? Color.black : Color.white;
        }
#endif

        SetPlayerSpawnpointIsAlternate.Invoke(spawn, isAlternate);
        if (index >= 0)
        {
            Logger.DevkitServer.LogDebug(nameof(SetIsAlternateLocal), $"Player spawnpoint updated: {(oldIsAlternate ? "Alternate" : "Primary")} -> {(isAlternate ? "Alternate" : "Primary")}");
            EventOnPlayerSpawnpointIsAlternateChanged.TryInvoke(spawn, index, isAlternate);
        }
    }

    /// <summary>
    /// Enumerates item spawns by region (starting at the middle of the map).
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static ListRegionsEnumerator<ItemSpawnpoint> EnumerateItemSpawns()
    {
        ThreadUtil.assertIsGameThread();

        return LevelItems.spawns.CastFrom();
    }

    /// <summary>
    /// Enumerates zombie spawns by region (starting at the middle of the map).
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static ListRegionsEnumerator<ZombieSpawnpoint> EnumerateZombieSpawns()
    {
        ThreadUtil.assertIsGameThread();

        return LevelZombies.spawns.CastFrom();
    }

    /// <summary>
    /// Enumerates item spawns by region (starting at the middle of the map).
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static ListRegionsEnumerator<ItemSpawnpoint> EnumerateItemSpawns(byte centerX, byte centerY, byte maxRegionDistance = 255)
    {
        ThreadUtil.assertIsGameThread();

        return LevelItems.spawns.CastFrom(centerX, centerY, maxRegionDistance);
    }

    /// <summary>
    /// Enumerates zombie spawns by region (starting at the specified region).
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static ListRegionsEnumerator<ZombieSpawnpoint> EnumerateZombieSpawns(byte centerX, byte centerY, byte maxRegionDistance = 255)
    {
        ThreadUtil.assertIsGameThread();

        return LevelZombies.spawns.CastFrom(centerX, centerY, maxRegionDistance);
    }

    /// <summary>
    /// Enumerates item spawns by region (starting at the specified region).
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static ListRegionsEnumerator<ItemSpawnpoint> EnumerateItemSpawns(RegionCoord center, byte maxRegionDistance = 255)
    {
        ThreadUtil.assertIsGameThread();

        return LevelItems.spawns.CastFrom(center, maxRegionDistance);
    }

    /// <summary>
    /// Enumerates zombie spawns by region (starting at the specified region).
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static ListRegionsEnumerator<ZombieSpawnpoint> EnumerateZombieSpawns(RegionCoord center, byte maxRegionDistance = 255)
    {
        ThreadUtil.assertIsGameThread();

        return LevelZombies.spawns.CastFrom(center, maxRegionDistance);
    }

    /// <summary>
    /// Enumerates item spawns by region (starting at the region containing the specified point).
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static ListRegionsEnumerator<ItemSpawnpoint> EnumerateItemSpawns(Vector3 center, byte maxRegionDistance = 255)
    {
        ThreadUtil.assertIsGameThread();

        return LevelItems.spawns.CastFrom(center, maxRegionDistance);
    }

    /// <summary>
    /// Enumerates zombie spawns by region (starting at the region containing the specified point).
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static ListRegionsEnumerator<ZombieSpawnpoint> EnumerateZombieSpawns(Vector3 center, byte maxRegionDistance = 255)
    {
        ThreadUtil.assertIsGameThread();

        return LevelZombies.spawns.CastFrom(center, maxRegionDistance);
    }

    /// <summary>
    /// Enumerates animal spawns starting at the center of the map.
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static DistanceListIterator<AnimalSpawnpoint> EnumerateAnimalSpawns()
    {
        ThreadUtil.assertIsGameThread();

        return new DistanceListIterator<AnimalSpawnpoint>(LevelAnimals.spawns, x => x.point, Vector3.zero, useXZAxisOnly: true);
    }

    /// <summary>
    /// Enumerates player spawns starting at the center of the map.
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static DistanceListIterator<PlayerSpawnpoint> EnumeratePlayerSpawns()
    {
        ThreadUtil.assertIsGameThread();

        return new DistanceListIterator<PlayerSpawnpoint>(LevelPlayers.spawns, x => x.point, Vector3.zero, useXZAxisOnly: true);
    }

    /// <summary>
    /// Enumerates vehicle spawns starting at the center of the map.
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static DistanceListIterator<VehicleSpawnpoint> EnumerateVehicleSpawns()
    {
        ThreadUtil.assertIsGameThread();

        return new DistanceListIterator<VehicleSpawnpoint>(LevelVehicles.spawns, x => x.point, Vector3.zero, useXZAxisOnly: true);
    }

    /// <summary>
    /// Enumerates animal spawns starting at <paramref name="center"/>.
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static DistanceListIterator<AnimalSpawnpoint> EnumerateAnimalSpawns(Vector3 center)
    {
        ThreadUtil.assertIsGameThread();

        return new DistanceListIterator<AnimalSpawnpoint>(LevelAnimals.spawns, x => x.point, center, useXZAxisOnly: true);
    }

    /// <summary>
    /// Enumerates player spawns starting at <paramref name="center"/>.
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static DistanceListIterator<PlayerSpawnpoint> EnumeratePlayerSpawns(Vector3 center)
    {
        ThreadUtil.assertIsGameThread();

        return new DistanceListIterator<PlayerSpawnpoint>(LevelPlayers.spawns, x => x.point, center, useXZAxisOnly: true);
    }

    /// <summary>
    /// Enumerates vehicle spawns starting at <paramref name="center"/>.
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static DistanceListIterator<VehicleSpawnpoint> EnumerateVehicleSpawns(Vector3 center)
    {
        ThreadUtil.assertIsGameThread();

        return new DistanceListIterator<VehicleSpawnpoint>(LevelVehicles.spawns, x => x.point, center, useXZAxisOnly: true);
    }

    /// <summary>
    /// Enumerates animal spawns starting the center of the specified region.
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static DistanceListIterator<AnimalSpawnpoint> EnumerateAnimalSpawns(byte centerX, byte centerY)
    {
        ThreadUtil.assertIsGameThread();

        Regions.tryGetPoint(centerX, centerY, out Vector3 center);
        center.x += Regions.REGION_SIZE / 2f;
        center.z += Regions.REGION_SIZE / 2f;
        return new DistanceListIterator<AnimalSpawnpoint>(LevelAnimals.spawns, x => x.point, center, useXZAxisOnly: true);
    }

    /// <summary>
    /// Enumerates player spawns starting the center of the specified region.
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static DistanceListIterator<PlayerSpawnpoint> EnumeratePlayerSpawns(byte centerX, byte centerY)
    {
        ThreadUtil.assertIsGameThread();

        Regions.tryGetPoint(centerX, centerY, out Vector3 center);
        center.x += Regions.REGION_SIZE / 2f;
        center.z += Regions.REGION_SIZE / 2f;
        return new DistanceListIterator<PlayerSpawnpoint>(LevelPlayers.spawns, x => x.point, center, useXZAxisOnly: true);
    }

    /// <summary>
    /// Enumerates vehicle spawns starting the center of the specified region.
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static DistanceListIterator<VehicleSpawnpoint> EnumerateVehicleSpawns(byte centerX, byte centerY)
    {
        ThreadUtil.assertIsGameThread();

        Regions.tryGetPoint(centerX, centerY, out Vector3 center);
        center.x += Regions.REGION_SIZE / 2f;
        center.z += Regions.REGION_SIZE / 2f;
        return new DistanceListIterator<VehicleSpawnpoint>(LevelVehicles.spawns, x => x.point, center, useXZAxisOnly: true);
    }

    /// <summary>
    /// Enumerates animal spawns starting the center of the specified region.
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static DistanceListIterator<AnimalSpawnpoint> EnumerateAnimalSpawns(RegionCoord center)
    {
        ThreadUtil.assertIsGameThread();

        Regions.tryGetPoint(center.x, center.y, out Vector3 center2);
        center2.x += Regions.REGION_SIZE / 2f;
        center2.z += Regions.REGION_SIZE / 2f;
        return new DistanceListIterator<AnimalSpawnpoint>(LevelAnimals.spawns, x => x.point, center2, useXZAxisOnly: true);
    }

    /// <summary>
    /// Enumerates vehicle spawns starting the center of the specified region.
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static DistanceListIterator<VehicleSpawnpoint> EnumerateVehicleSpawns(RegionCoord center)
    {
        ThreadUtil.assertIsGameThread();

        Regions.tryGetPoint(center.x, center.y, out Vector3 center2);
        center2.x += Regions.REGION_SIZE / 2f;
        center2.z += Regions.REGION_SIZE / 2f;
        return new DistanceListIterator<VehicleSpawnpoint>(LevelVehicles.spawns, x => x.point, center2, useXZAxisOnly: true);
    }

    /// <summary>
    /// Enumerates player spawns starting the center of the specified region.
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static DistanceListIterator<PlayerSpawnpoint> EnumeratePlayerSpawns(RegionCoord center)
    {
        ThreadUtil.assertIsGameThread();

        Regions.tryGetPoint(center.x, center.y, out Vector3 center2);
        center2.x += Regions.REGION_SIZE / 2f;
        center2.z += Regions.REGION_SIZE / 2f;
        return new DistanceListIterator<PlayerSpawnpoint>(LevelPlayers.spawns, x => x.point, center2, useXZAxisOnly: true);
    }

    /// <summary>
    /// Execute <paramref name="action"/> for each item spawn starting at the provided region.
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static void ForEachItemSpawn(byte centerX, byte centerY, byte maxRegionDistance, ForEach<ItemSpawnpoint> action)
    {
        ThreadUtil.assertIsGameThread();

        if (LevelItems.spawns == null)
            return;
        SurroundingRegionsIterator iterator = new SurroundingRegionsIterator(centerX, centerY, maxRegionDistance);
        while (iterator.MoveNext())
        {
            List<ItemSpawnpoint> region = LevelItems.spawns[iterator.Current.x, iterator.Current.y];
            for (int i = 0; i < region.Count; ++i)
                action(region[i]);
        }
    }

    /// <summary>
    /// Execute <paramref name="action"/> for each item spawn starting at the provided region.
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static void ForEachItemSpawn(byte centerX, byte centerY, byte maxRegionDistance, ForEachWhile<ItemSpawnpoint> action)
    {
        ThreadUtil.assertIsGameThread();

        if (LevelItems.spawns == null)
            return;
        SurroundingRegionsIterator iterator = new SurroundingRegionsIterator(centerX, centerY, maxRegionDistance);
        while (iterator.MoveNext())
        {
            List<ItemSpawnpoint> region = LevelItems.spawns[iterator.Current.x, iterator.Current.y];
            for (int i = 0; i < region.Count; ++i)
            {
                if (!action(region[i]))
                    return;
            }
        }
    }

    /// <summary>
    /// Execute <paramref name="action"/> for each zombie spawn starting at the provided region.
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static void ForEachZombieSpawn(byte centerX, byte centerY, byte maxRegionDistance, ForEach<ItemSpawnpoint> action)
    {
        ThreadUtil.assertIsGameThread();

        if (LevelItems.spawns == null)
            return;
        SurroundingRegionsIterator iterator = new SurroundingRegionsIterator(centerX, centerY, maxRegionDistance);
        while (iterator.MoveNext())
        {
            List<ItemSpawnpoint> region = LevelItems.spawns[iterator.Current.x, iterator.Current.y];
            for (int i = 0; i < region.Count; ++i)
                action(region[i]);
        }
    }

    /// <summary>
    /// Execute <paramref name="action"/> for each zombie spawn starting at the provided region.
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static void ForEachZombieSpawn(byte centerX, byte centerY, byte maxRegionDistance, ForEachWhile<ItemSpawnpoint> action)
    {
        ThreadUtil.assertIsGameThread();

        if (LevelItems.spawns == null)
            return;
        SurroundingRegionsIterator iterator = new SurroundingRegionsIterator(centerX, centerY, maxRegionDistance);
        while (iterator.MoveNext())
        {
            List<ItemSpawnpoint> region = LevelItems.spawns[iterator.Current.x, iterator.Current.y];
            for (int i = 0; i < region.Count; ++i)
            {
                if (!action(region[i]))
                    return;
            }
        }
    }

    /// <summary>
    /// Locally removes an <see cref="AnimalSpawnpoint"/> from the list and destroyes the node.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool RemoveAnimalSpawnLocal(AnimalSpawnpoint spawn) => RemoveAnimalSpawnLocal(spawn, true);

    /// <summary>
    /// Locally removes an <see cref="AnimalSpawnpoint"/> from the list and destroyes the node.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    internal static bool RemoveAnimalSpawnLocal(AnimalSpawnpoint spawn, bool destroyNode)
    {
        ThreadUtil.assertIsGameThread();

        int index = LevelAnimals.spawns.IndexOf(spawn);
        if (index == -1)
        {
            if (destroyNode && spawn.node != null)
            {
#if CLIENT
                if (spawn.node.TryGetComponent(out AnimalSpawnpointNode node))
                    node.IgnoreDestroy = true;
#endif
                Object.Destroy(spawn.node.gameObject);
            }
            return false;
        }

        if (index == LevelAnimals.spawns.Count - 1)
        {
            LevelAnimals.spawns.RemoveAt(index);
            EventOnAnimalSpawnpointRemoved.TryInvoke(spawn, index);
        }
        else
        {
            int oldIndex = LevelAnimals.spawns.Count - 1;
            LevelAnimals.spawns[index] = LevelAnimals.spawns[oldIndex];
            AnimalSpawnpoint changed = LevelAnimals.spawns[index];
#if CLIENT
            if (changed.node != null && changed.node.TryGetComponent(out AnimalSpawnpointNode node))
            {
                node.IsAdded = true;
                node.Index = index;
                node.Spawnpoint = changed;
            }
#endif
            LevelAnimals.spawns.RemoveAt(oldIndex);
            EventOnAnimalSpawnpointRemoved.TryInvoke(spawn, index);

            EventOnAnimalSpawnpointIndexUpdated.TryInvoke(changed, oldIndex, index);
            Logger.DevkitServer.LogDebug(nameof(RemoveAnimalSpawnLocal), $"Animal spawnpoint index updated after replacing other removed spawnpoint on add: {oldIndex.Format()} -> {index.Format()}.");
        }

        Logger.DevkitServer.LogDebug(nameof(RemoveAnimalSpawnLocal), $"Animal spawnpoint removed: {index.Format()}.");

        if (destroyNode && spawn.node != null)
        {
#if CLIENT
            if (spawn.node.TryGetComponent(out AnimalSpawnpointNode node))
                node.IgnoreDestroy = true;
#endif
            Object.Destroy(spawn.node.gameObject);
        }
        return true;
    }

    /// <summary>
    /// Locally removes a <see cref="VehicleSpawnpoint"/> from the list and destroyes the node.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool RemoveVehicleSpawnLocal(VehicleSpawnpoint spawn) => RemoveVehicleSpawnLocal(spawn, true);

    /// <summary>
    /// Locally removes a <see cref="VehicleSpawnpoint"/> from the list and destroyes the node.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    internal static bool RemoveVehicleSpawnLocal(VehicleSpawnpoint spawn, bool destroyNode)
    {
        ThreadUtil.assertIsGameThread();

        int index = LevelVehicles.spawns.IndexOf(spawn);
        if (index == -1)
        {
            if (destroyNode && spawn.node != null)
            {
#if CLIENT
                if (spawn.node.TryGetComponent(out VehicleSpawnpointNode node))
                    node.IgnoreDestroy = true;
#endif
                Object.Destroy(spawn.node.gameObject);
            }
            return false;
        }

        if (index == LevelVehicles.spawns.Count - 1)
        {
            LevelVehicles.spawns.RemoveAt(index);
            EventOnVehicleSpawnpointRemoved.TryInvoke(spawn, index);
        }
        else
        {
            int oldIndex = LevelVehicles.spawns.Count - 1;
            LevelVehicles.spawns[index] = LevelVehicles.spawns[oldIndex];
            VehicleSpawnpoint changed = LevelVehicles.spawns[index];
#if CLIENT
            if (changed.node != null && changed.node.TryGetComponent(out VehicleSpawnpointNode node))
            {
                node.IsAdded = true;
                node.Index = index;
                node.Spawnpoint = changed;
            }
#endif
            LevelVehicles.spawns.RemoveAt(oldIndex);
            EventOnVehicleSpawnpointRemoved.TryInvoke(spawn, index);

            EventOnVehicleSpawnpointIndexUpdated.TryInvoke(changed, oldIndex, index);
            Logger.DevkitServer.LogDebug(nameof(RemoveVehicleSpawnLocal), $"Vehicle spawnpoint index updated after replacing other removed spawnpoint on add: {oldIndex.Format()} -> {index.Format()}.");
        }

        Logger.DevkitServer.LogDebug(nameof(RemoveVehicleSpawnLocal), $"Vehicle spawnpoint removed: {index.Format()}.");

        if (destroyNode && spawn.node != null)
        {
#if CLIENT
            if (spawn.node.TryGetComponent(out VehicleSpawnpointNode node))
                node.IgnoreDestroy = true;
#endif
            Object.Destroy(spawn.node.gameObject);
        }
        return true;
    }

    /// <summary>
    /// Locally removes a <see cref="PlayerSpawnpoint"/> from the list and destroyes the node.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool RemovePlayerSpawnLocal(PlayerSpawnpoint spawn) => RemovePlayerSpawnLocal(spawn, true);

    /// <summary>
    /// Locally removes a <see cref="PlayerSpawnpoint"/> from the list and destroyes the node.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    internal static bool RemovePlayerSpawnLocal(PlayerSpawnpoint spawn, bool destroyNode)
    {
        ThreadUtil.assertIsGameThread();

        int index = LevelPlayers.spawns.IndexOf(spawn);
        if (index == -1)
        {
            if (destroyNode && spawn.node != null)
            {
#if CLIENT
                if (spawn.node.TryGetComponent(out PlayerSpawnpointNode node))
                    node.IgnoreDestroy = true;
#endif
                Object.Destroy(spawn.node.gameObject);
            }
            return false;
        }

        if (index == LevelPlayers.spawns.Count - 1)
        {
            LevelPlayers.spawns.RemoveAt(index);
            EventOnPlayerSpawnpointRemoved.TryInvoke(spawn, index);
        }
        else
        {
            int oldIndex = LevelPlayers.spawns.Count - 1;
            LevelPlayers.spawns[index] = LevelPlayers.spawns[oldIndex];
            PlayerSpawnpoint changed = LevelPlayers.spawns[index];
#if CLIENT
            if (changed.node != null && changed.node.TryGetComponent(out PlayerSpawnpointNode node))
            {
                node.IsAdded = true;
                node.Index = index;
                node.Spawnpoint = changed;
            }
#endif
            LevelPlayers.spawns.RemoveAt(oldIndex);
            EventOnPlayerSpawnpointRemoved.TryInvoke(spawn, index);

            EventOnPlayerSpawnpointIndexUpdated.TryInvoke(changed, oldIndex, index);
            Logger.DevkitServer.LogDebug(nameof(RemovePlayerSpawnLocal), $"Player spawnpoint index updated after replacing other removed spawnpoint on add: {oldIndex.Format()} -> {index.Format()}.");
        }

        Logger.DevkitServer.LogDebug(nameof(RemovePlayerSpawnLocal), $"Player spawnpoint removed: {index.Format()}.");
        
        if (destroyNode && spawn.node != null)
        {
#if CLIENT
            if (spawn.node.TryGetComponent(out PlayerSpawnpointNode node))
                node.IgnoreDestroy = true;
#endif
            Object.Destroy(spawn.node.gameObject);
        }
        return true;
    }

    /// <summary>
    /// Locally removes an <see cref="ItemSpawnpoint"/> from the list and destroyes the node.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool RemoveItemSpawnLocal(ItemSpawnpoint spawn) => RemoveItemSpawnLocal(spawn, true);

    /// <summary>
    /// Locally removes an <see cref="ItemSpawnpoint"/> from the list and destroyes the node.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    internal static bool RemoveItemSpawnLocal(ItemSpawnpoint spawn, bool destroyNode)
    {
        ThreadUtil.assertIsGameThread();

        bool removed = false;
        RegionUtil.ForEachRegion(spawn.point, coord =>
        {
            List<ItemSpawnpoint> region = LevelItems.spawns[coord.x, coord.y];
            int index = region.IndexOf(spawn);
            if (index == -1)
                return true;
            RegionIdentifier newRegion = new RegionIdentifier(coord, index);

            if (index == region.Count - 1)
            {
                region.RemoveAt(index);
                EventOnItemSpawnpointRemoved.TryInvoke(spawn, newRegion);
            }
            else
            {
                RegionIdentifier oldRegion = new RegionIdentifier(coord, region.Count - 1);
                region[index] = region[oldRegion.Index];
                ItemSpawnpoint changed = region[index];
#if CLIENT
                if (changed.node != null && changed.node.TryGetComponent(out ItemSpawnpointNode node))
                {
                    node.IsAdded = true;
                    node.Region = newRegion;
                    node.Spawnpoint = changed;
                }
#endif
                region.RemoveAt(oldRegion.Index);
                EventOnItemSpawnpointRemoved.TryInvoke(spawn, newRegion);

                EventOnItemSpawnpointRegionUpdated.TryInvoke(changed, oldRegion, newRegion);
                Logger.DevkitServer.LogDebug(nameof(RemoveItemSpawnLocal), $"Item spawnpoint index updated after replacing other removed spawnpoint on add: {oldRegion.Format()} -> {newRegion.Format()}.");
            }

            Logger.DevkitServer.LogDebug(nameof(RemoveItemSpawnLocal), $"Item spawnpoint removed: {newRegion.Format()}.");

            if (destroyNode && spawn.node != null)
            {
#if CLIENT
                if (spawn.node.TryGetComponent(out ItemSpawnpointNode node))
                    node.IgnoreDestroy = true;
#endif
                Object.Destroy(spawn.node.gameObject);
            }
            removed = true;
            return false;
        });

        if (!removed && destroyNode && spawn.node != null)
        {
#if CLIENT
            if (spawn.node.TryGetComponent(out ItemSpawnpointNode node))
                node.IgnoreDestroy = true;
#endif
            Object.Destroy(spawn.node.gameObject);
        }

        return removed;
    }

    /// <summary>
    /// Locally removes a <see cref="ZombieSpawnpoint"/> from the list and destroyes the node.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool RemoveZombieSpawnLocal(ZombieSpawnpoint spawn) => RemoveZombieSpawnLocal(spawn, true);

    /// <summary>
    /// Locally removes a <see cref="ZombieSpawnpoint"/> from the list and destroyes the node.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    internal static bool RemoveZombieSpawnLocal(ZombieSpawnpoint spawn, bool destroyNode)
    {
        ThreadUtil.assertIsGameThread();

        bool removed = false;
        RegionUtil.ForEachRegion(spawn.point, coord =>
        {
            List<ZombieSpawnpoint> region = LevelZombies.spawns[coord.x, coord.y];
            int index = region.IndexOf(spawn);
            if (index == -1)
                return true;
            RegionIdentifier newRegion = new RegionIdentifier(coord, index);

            if (index == region.Count - 1)
            {
                region.RemoveAt(index);
                EventOnZombieSpawnpointRemoved.TryInvoke(spawn, newRegion);
            }
            else
            {
                RegionIdentifier oldRegion = new RegionIdentifier(coord, region.Count - 1);
                region[index] = region[oldRegion.Index];
                ZombieSpawnpoint changed = region[index];
#if CLIENT
                if (changed.node != null && changed.node.TryGetComponent(out ZombieSpawnpointNode node))
                {
                    node.IsAdded = true;
                    node.Region = newRegion;
                    node.Spawnpoint = changed;
                }
#endif
                region.RemoveAt(oldRegion.Index);
                EventOnZombieSpawnpointRemoved.TryInvoke(spawn, newRegion);

                EventOnZombieSpawnpointRegionUpdated.TryInvoke(changed, oldRegion, newRegion);
                Logger.DevkitServer.LogDebug(nameof(RemoveZombieSpawnLocal), $"Zombie spawnpoint index updated after replacing other removed spawnpoint on add: {oldRegion.Format()} -> {newRegion.Format()}.");
            }

            Logger.DevkitServer.LogDebug(nameof(RemoveZombieSpawnLocal), $"Zombie spawnpoint removed: {newRegion.Format()}.");

            if (destroyNode && spawn.node != null)
            {
#if CLIENT
                if (spawn.node.TryGetComponent(out ZombieSpawnpointNode node))
                    node.IgnoreDestroy = true;
#endif
                Object.Destroy(spawn.node.gameObject);
            }
            removed = true;
            return false;
        });

        if (!removed && destroyNode && spawn.node != null)
        {
#if CLIENT
            if (spawn.node.TryGetComponent(out ZombieSpawnpointNode node))
                node.IgnoreDestroy = true;
#endif
            Object.Destroy(spawn.node.gameObject);
        }

        return removed;
    }

    /// <summary>
    /// Add an <see cref="AnimalSpawnpoint"/> to <see cref="LevelAnimals"/> if it isn't already added and adds any necessary component to it.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="InvalidOperationException">Too many spawns in the world (65535).</exception>
    public static void AddAnimalSpawnLocal(AnimalSpawnpoint spawn)
    {
        ThreadUtil.assertIsGameThread();

#if CLIENT
        SetNodeShaders(spawn.node);
#endif

        int index = LevelAnimals.spawns.IndexOf(spawn);
        bool added = index == -1;
        if (added)
        {
            index = LevelAnimals.spawns.Count;
            if (index >= ushort.MaxValue)
                throw new InvalidOperationException($"There can not be more than {ushort.MaxValue} animal spawns in the world.");
            LevelAnimals.spawns.Add(spawn);
        }
#if CLIENT
        if (spawn.node != null)
        {
            if (!spawn.node.TryGetComponent(out AnimalSpawnpointNode node))
                node = spawn.node.gameObject.AddComponent<AnimalSpawnpointNode>();

            node.IsAdded = true;
            node.Spawnpoint = spawn;
            node.Index = index;
        }
#endif
        if (added)
        {
            EventOnAnimalSpawnpointAdded.TryInvoke(spawn, index);
            Logger.DevkitServer.LogDebug(nameof(AddAnimalSpawnLocal), "Animal spawnpoint added.");
        }
        else
            Logger.DevkitServer.LogDebug(nameof(AddAnimalSpawnLocal), "Animal spawnpoint already added.");
    }

    /// <summary>
    /// Add a <see cref="VehicleSpawnpoint"/> to <see cref="LevelVehicles"/> if it isn't already added and adds any necessary component to it.
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="InvalidOperationException">Too many spawns in the world (65535).</exception>
    public static void AddVehicleSpawnLocal(VehicleSpawnpoint spawn)
    {
        ThreadUtil.assertIsGameThread();

#if CLIENT
        SetNodeShaders(spawn.node);
#endif

        int index = LevelVehicles.spawns.IndexOf(spawn);
        bool added = index == -1;
        if (added)
        {
            index = LevelVehicles.spawns.Count;
            if (index >= ushort.MaxValue)
                throw new InvalidOperationException($"There can not be more than {ushort.MaxValue} vehicle spawns in the world.");
            LevelVehicles.spawns.Add(spawn);
        }
#if CLIENT
        if (spawn.node != null)
        {
            if (!spawn.node.TryGetComponent(out VehicleSpawnpointNode node))
                node = spawn.node.gameObject.AddComponent<VehicleSpawnpointNode>();

            node.IsAdded = true;
            node.Spawnpoint = spawn;
            node.Index = index;
        }
#endif
        if (added)
        {
            EventOnVehicleSpawnpointAdded.TryInvoke(spawn, index);
            Logger.DevkitServer.LogDebug(nameof(AddVehicleSpawnLocal), "Vehicle spawnpoint added.");
        }
        else
            Logger.DevkitServer.LogDebug(nameof(AddVehicleSpawnLocal), "Vehicle spawnpoint already added.");
    }

    /// <summary>
    /// Add a <see cref="PlayerSpawnpoint"/> to <see cref="LevelPlayers"/> if it isn't already added and adds any necessary component to it.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="InvalidOperationException">Too many spawns in the world (255).</exception>
    public static void AddPlayerSpawnLocal(PlayerSpawnpoint spawn)
    {
        ThreadUtil.assertIsGameThread();

#if CLIENT
        SetNodeShaders(spawn.node);
#endif

        int index = LevelPlayers.spawns.IndexOf(spawn);
        bool added = index == -1;
        if (added)
        {
            index = LevelPlayers.spawns.Count;
            if (index >= byte.MaxValue)
                throw new InvalidOperationException($"There can not be more than {byte.MaxValue} player spawns in the world.");
            LevelPlayers.spawns.Add(spawn);
        }
#if CLIENT
        if (spawn.node != null)
        {
            if (!spawn.node.TryGetComponent(out PlayerSpawnpointNode node))
                node = spawn.node.gameObject.AddComponent<PlayerSpawnpointNode>();

            node.IsAdded = true;
            node.Spawnpoint = spawn;
            node.Index = index;
        }
#endif
        if (added)
        {
            EventOnPlayerSpawnpointAdded.TryInvoke(spawn, index);
            Logger.DevkitServer.LogDebug(nameof(AddPlayerSpawnLocal), "Player spawnpoint added.");
        }
        else
            Logger.DevkitServer.LogDebug(nameof(AddPlayerSpawnLocal), "Player spawnpoint already added.");
    }

    /// <summary>
    /// Add an <see cref="ItemSpawnpoint"/> to <see cref="LevelItems"/> if it isn't already added and adds any necessary component to it.
    /// </summary>
    /// <remarks>Non-replicating. Region is calculated from <see cref="ItemSpawnpoint.point"/>. If it's out of bounds the editor node will be destroyed and nothing will be added.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="InvalidOperationException">Too many spawns in that region (65535).</exception>
    public static void AddItemSpawnLocal(ItemSpawnpoint spawn)
    {
        ThreadUtil.assertIsGameThread();

#if CLIENT
        SetNodeShaders(spawn.node);
#endif

        int worldSize = Regions.WORLD_SIZE;
        int x = 0, y = 0;
        bool alreadyExists = false;
        int index = -1;
        for (; x < worldSize; ++x)
        {
            for (; y < worldSize; ++y)
            {
                index = LevelItems.spawns[x, y].IndexOf(spawn);
                if (index != -1)
                {
                    alreadyExists = true;
                    break;
                }
            }
        }
        int index2 = index;

        RegionIdentifier? changedOldRegion = null, changedNewRegion = null;
        ItemSpawnpoint? changed = null;

        bool regionChanged = false;
        if (!Regions.tryGetCoordinate(spawn.point, out byte x2, out byte y2))
        {
            if (alreadyExists)
                goto setNode;

            if (spawn.node != null)
            {
#if CLIENT
                if (spawn.node.TryGetComponent(out ItemSpawnpointNode node))
                    node.IgnoreDestroy = true;
#endif
                Object.Destroy(spawn.node.gameObject);
            }

            return;
        }

        if (alreadyExists && (x2 != x || y2 != y))
        {
            List<ItemSpawnpoint> region = LevelItems.spawns[x, y];
            if (region.Count - 1 == index)
                region.RemoveAt(index);
            else if (region.Count > 1)
            {
                region[index] = region[^1];
                changed = region[index];
                changedOldRegion = new RegionIdentifier(x, y, region.Count - 1);
                changedNewRegion = new RegionIdentifier(x, y, index);
#if CLIENT
                if (changed.node != null && changed.node.TryGetComponent(out ItemSpawnpointNode node))
                {
                    node.IsAdded = true;
                    node.Region = changedNewRegion.Value;
                    node.Spawnpoint = changed;
                }
#endif
                region.RemoveAt(region.Count - 1);
                Logger.DevkitServer.LogDebug(nameof(AddItemSpawnLocal), $"Item spawnpoint region updated after replacing other removed spawnpoint on add: {changedOldRegion.Value.Format()} -> {changedNewRegion.Value.Format()}.");
            }
            alreadyExists = false;
            regionChanged = true;
        }


        if (!alreadyExists)
        {
            List<ItemSpawnpoint> region = LevelItems.spawns[x2, y2];
            index2 = region.Count;
            if (index2 >= ushort.MaxValue)
                throw new InvalidOperationException($"There can not be more than {ushort.MaxValue} item spawns in a region ({x2}, {y2}).");
            region.Add(spawn);
        }

        setNode:
        RegionIdentifier regionId = x2 == byte.MaxValue || index2 == -1 ? RegionIdentifier.Invalid : new RegionIdentifier(x2, y2, index2);
#if CLIENT
        if (spawn.node != null)
        {
            if (!spawn.node.TryGetComponent(out ItemSpawnpointNode node))
                node = spawn.node.gameObject.AddComponent<ItemSpawnpointNode>();

            node.IsAdded = true;
            node.Spawnpoint = spawn;
            node.Region = regionId;
        }
#endif
        if (regionChanged)
        {
            RegionIdentifier oldRegion = new RegionIdentifier(x, y, index);
            EventOnItemSpawnpointRegionUpdated.TryInvoke(spawn, oldRegion, regionId);

            if (changed != null)
                EventOnItemSpawnpointRegionUpdated.TryInvoke(changed, changedOldRegion!.Value, changedNewRegion!.Value);

            Logger.DevkitServer.LogDebug(nameof(AddItemSpawnLocal), $"Item spawnpoint region updated on add: {oldRegion.Format()} -> {regionId.Format()}.");
        }
        else if (!alreadyExists)
        {
            EventOnItemSpawnpointAdded.TryInvoke(spawn, regionId);
            Logger.DevkitServer.LogDebug(nameof(AddItemSpawnLocal), $"Item spawnpoint added: {regionId.Format()}.");
        }
    }

    /// <summary>
    /// Add a <see cref="ZombieSpawnpoint"/> to <see cref="LevelZombies"/> if it isn't already added and adds any necessary component to it.
    /// </summary>
    /// <remarks>Non-replicating. Region is calculated from <see cref="ZombieSpawnpoint.point"/>. If it's out of bounds the editor node will be destroyed and nothing will be added.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="InvalidOperationException">Too many spawns in that region (65535).</exception>
    public static void AddZombieSpawnLocal(ZombieSpawnpoint spawn)
    {
        ThreadUtil.assertIsGameThread();

#if CLIENT
        SetNodeShaders(spawn.node);
#endif

        int worldSize = Regions.WORLD_SIZE;
        int x = 0, y = 0;
        bool alreadyExists = false;
        int index = -1;
        for (; x < worldSize; ++x)
        {
            for (; y < worldSize; ++y)
            {
                index = LevelZombies.spawns[x, y].IndexOf(spawn);
                if (index != -1)
                {
                    alreadyExists = true;
                    break;
                }
            }
        }
        int index2 = index;

        RegionIdentifier? changedOldRegion = null, changedNewRegion = null;
        ZombieSpawnpoint? changed = null;

        bool regionChanged = false;
        if (!Regions.tryGetCoordinate(spawn.point, out byte x2, out byte y2))
        {
            if (alreadyExists)
                goto setNode;

            if (spawn.node != null)
            {
#if CLIENT
                if (spawn.node.TryGetComponent(out ZombieSpawnpointNode node))
                    node.IgnoreDestroy = true;
#endif
                Object.Destroy(spawn.node.gameObject);
            }

            return;
        }

        if (alreadyExists && (x2 != x || y2 != y))
        {
            List<ZombieSpawnpoint> region = LevelZombies.spawns[x, y];
            if (region.Count - 1 == index)
                region.RemoveAt(index);
            else if (region.Count > 1)
            {
                region[index] = region[^1];
                changed = region[index];
                changedOldRegion = new RegionIdentifier(x, y, region.Count - 1);
                changedNewRegion = new RegionIdentifier(x, y, index);
#if CLIENT
                if (changed.node != null && changed.node.TryGetComponent(out ZombieSpawnpointNode node))
                {
                    node.IsAdded = true;
                    node.Region = changedNewRegion.Value;
                    node.Spawnpoint = changed;
                }
#endif
                region.RemoveAt(region.Count - 1);
                Logger.DevkitServer.LogDebug(nameof(AddZombieSpawnLocal), $"Zombie spawnpoint region updated after replacing other removed spawnpoint on add: {changedOldRegion.Value.Format()} -> {changedNewRegion.Value.Format()}.");
            }
            alreadyExists = false;
            regionChanged = true;
        }


        if (!alreadyExists)
        {
            List<ZombieSpawnpoint> region = LevelZombies.spawns[x2, y2];
            index2 = region.Count;
            if (index2 >= ushort.MaxValue)
                throw new InvalidOperationException($"There can not be more than {ushort.MaxValue} zombie spawns in a region ({x2}, {y2}).");
            region.Add(spawn);
        }

        setNode:
        RegionIdentifier regionId = x2 == byte.MaxValue || index2 == -1 ? RegionIdentifier.Invalid : new RegionIdentifier(x2, y2, index2);
#if CLIENT
        if (spawn.node != null)
        {
            if (!spawn.node.TryGetComponent(out ZombieSpawnpointNode node))
                node = spawn.node.gameObject.AddComponent<ZombieSpawnpointNode>();

            node.IsAdded = true;
            node.Spawnpoint = spawn;
            node.Region = regionId;
        }
#endif
        if (regionChanged)
        {
            RegionIdentifier oldRegion = new RegionIdentifier(x, y, index);
            EventOnZombieSpawnpointRegionUpdated.TryInvoke(spawn, oldRegion, regionId);

            if (changed != null)
                EventOnZombieSpawnpointRegionUpdated.TryInvoke(changed, changedOldRegion!.Value, changedNewRegion!.Value);

            Logger.DevkitServer.LogDebug(nameof(AddZombieSpawnLocal), $"Zombie spawnpoint region updated on add: {oldRegion.Format()} -> {regionId.Format()}.");
        }
        else if (!alreadyExists)
        {
            EventOnZombieSpawnpointAdded.TryInvoke(spawn, regionId);
            Logger.DevkitServer.LogDebug(nameof(AddZombieSpawnLocal), $"Zombie spawnpoint added: {regionId.Format()}.");
        }
    }

    /// <summary>
    /// Locally moves the spawnpoint to <paramref name="position"/>. Calls local events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="MissingMemberException">Reflection failure.</exception>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static void MoveSpawnpointLocal(AnimalSpawnpoint point, Vector3 position)
    {
        if (SetAnimalSpawnpointPoint == null)
            throw new MissingMemberException("Instance setter for AnimalSpawnpoint.point is not valid.");

        ThreadUtil.assertIsGameThread();

        Vector3 oldPosition = point.point;
        point.SetPoint(position);
        if (point.node != null)
        {
            if (!point.node.transform.position.IsNearlyEqual(position))
                point.node.transform.position = position;
#if CLIENT
            if (!point.node.TryGetComponent(out AnimalSpawnpointNode spawnpoint))
                spawnpoint = point.node.gameObject.AddComponent<AnimalSpawnpointNode>();

            spawnpoint.Spawnpoint = point;
            spawnpoint.Index = LevelAnimals.spawns.IndexOf(point);
#endif
        }

        EventOnAnimalSpawnpointMoved.TryInvoke(point, oldPosition, position);
        Logger.DevkitServer.LogDebug(nameof(MoveSpawnpointLocal), $"Animal spawnpoint moved: {oldPosition.Format("F0")} -> {position.Format("F0")}.");
    }

    /// <summary>
    /// Locally moves the spawnpoint to <paramref name="position"/>. Calls local events.
    /// </summary>
    /// <exception cref="MissingMemberException">Reflection failure.</exception>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static void MoveSpawnpointLocal(VehicleSpawnpoint point, Vector3 position)
    {
        if (SetVehicleSpawnpointPoint == null)
            throw new MissingMemberException("Instance setter for VehicleSpawnpoint.point is not valid.");

        ThreadUtil.assertIsGameThread();

        Vector3 oldPosition = point.point;
        point.SetPoint(position);
        if (point.node != null)
        {
            if (!point.node.transform.position.IsNearlyEqual(position))
                point.node.transform.position = position;
#if CLIENT
            if (!point.node.TryGetComponent(out VehicleSpawnpointNode spawnpoint))
                spawnpoint = point.node.gameObject.AddComponent<VehicleSpawnpointNode>();

            spawnpoint.Spawnpoint = point;
            spawnpoint.Index = LevelVehicles.spawns.IndexOf(point);
#endif
        }

        EventOnVehicleSpawnpointMoved.TryInvoke(point, oldPosition, position, point.angle, point.angle);
        Logger.DevkitServer.LogDebug(nameof(MoveSpawnpointLocal), $"Vehicle spawnpoint moved: {oldPosition.Format("F0")} -> {position.Format("F0")}.");
    }

    /// <summary>
    /// Locally moves the spawnpoint to <paramref name="position"/>. Calls local events.
    /// </summary>
    /// <exception cref="MissingMemberException">Reflection failure.</exception>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static void MoveSpawnpointLocal(PlayerSpawnpoint point, Vector3 position)
    {
        if (SetPlayerSpawnpointPoint == null)
            throw new MissingMemberException("Instance setter for PlayerSpawnpoint.point is not valid.");

        ThreadUtil.assertIsGameThread();

        Vector3 oldPosition = point.point;
        point.SetPoint(position);
        if (point.node != null)
        {
            if (!point.node.transform.position.IsNearlyEqual(position))
                point.node.transform.position = position;
#if CLIENT
            if (!point.node.TryGetComponent(out PlayerSpawnpointNode spawnpoint))
                spawnpoint = point.node.gameObject.AddComponent<PlayerSpawnpointNode>();

            spawnpoint.Spawnpoint = point;
            spawnpoint.Index = LevelPlayers.spawns.IndexOf(point);
#endif
        }

        EventOnPlayerSpawnpointMoved.TryInvoke(point, oldPosition, position, point.angle, point.angle);
        Logger.DevkitServer.LogDebug(nameof(MoveSpawnpointLocal), $"Player spawnpoint moved: {oldPosition.Format("F0")} -> {position.Format("F0")}.");
    }

    /// <summary>
    /// Locally moves the spawnpoint to <paramref name="position"/>. Calls local events.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="position"/> is not a valid position in the region grid.</exception>
    /// <exception cref="MissingMemberException">Reflection failure.</exception>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static void MoveSpawnpointLocal(ItemSpawnpoint point, Vector3 position, out RegionIdentifier region)
    {
        if (SetItemSpawnpointPoint == null)
            throw new MissingMemberException("Instance setter for ItemSpawnpoint.point is not valid.");

        ThreadUtil.assertIsGameThread();

        RegionUtil.AssertGetRegion(position, out byte x, out byte y);
        Vector3 oldPosition = point.point;
        point.SetPoint(position);

        RegionIdentifier? removedFrom = null;
        bool needsRemove = true;

        RegionIdentifier? changedOldRegion = null;
        ItemSpawnpoint? changed = null;

        RegionUtil.ForEachRegion(oldPosition, coord =>
        {
            int index = LevelItems.spawns[coord.x, coord.y].IndexOf(point);
            if (index == -1)
                return true;

            RegionIdentifier newRegion = new RegionIdentifier(coord.x, coord.y, index);

            needsRemove = x != coord.x || y != coord.y;
            if (needsRemove)
            {
                List<ItemSpawnpoint> region = LevelItems.spawns[coord.x, coord.y];
                if (region.Count - 1 == index)
                    region.RemoveAt(index);
                else if (region.Count > 1)
                {
                    region[index] = region[^1];
                    changed = region[index];
                    changedOldRegion = new RegionIdentifier(coord.x, coord.y, region.Count - 1);
#if CLIENT
                    if (changed.node != null && changed.node.TryGetComponent(out ItemSpawnpointNode node))
                    {
                        node.IsAdded = true;
                        node.Region = newRegion;
                        node.Spawnpoint = changed;
                    }
#endif
                    region.RemoveAt(region.Count - 1);
                    Logger.DevkitServer.LogDebug(nameof(MoveSpawnpointLocal), $"Item spawnpoint region updated after replacing other removed spawnpoint on move: {changedOldRegion.Value.Format()} -> {newRegion.Format()}.");
                }
            }
            removedFrom = newRegion;
            return false;
        });
        if (needsRemove)
        {
            List<ItemSpawnpoint> regionList = LevelItems.spawns[x, y];
            region = new RegionIdentifier(x, y, regionList.Count);
            regionList.Add(point);
        }
        else
            region = removedFrom!.Value;

        if (point.node != null)
        {
            if (!point.node.transform.position.IsNearlyEqual(position))
                point.node.transform.position = position;
#if CLIENT
            if (!point.node.TryGetComponent(out ItemSpawnpointNode spawnpoint))
                spawnpoint = point.node.gameObject.AddComponent<ItemSpawnpointNode>();

            spawnpoint.IsAdded = true;
            spawnpoint.Region = region;
            spawnpoint.Spawnpoint = point;
#endif
        }

        if (removedFrom.HasValue)
        {
            if (needsRemove)
                EventOnItemSpawnpointRegionUpdated.TryInvoke(point, removedFrom.Value, region);

            if (changed != null)
                EventOnItemSpawnpointRegionUpdated.TryInvoke(changed, changedOldRegion!.Value, removedFrom.Value);

            EventOnItemSpawnpointMoved.TryInvoke(point, region, oldPosition, position);

            Logger.DevkitServer.LogDebug(nameof(MoveSpawnpointLocal), $"Item spawnpoint moved: {removedFrom.Value.Format()} -> {region.Format()}, pos: {oldPosition.Format("F0")} -> {position.Format("F0")}.");
        }
        else
        {
            EventOnItemSpawnpointAdded.TryInvoke(point, region);
            Logger.DevkitServer.LogDebug(nameof(MoveSpawnpointLocal), $"Item spawnpoint added on move: {region.Format()}.");
        }
    }

    /// <summary>
    /// Locally moves the spawnpoint to <paramref name="position"/>. Calls local events.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="position"/> is not a valid position in the region grid.</exception>
    /// <exception cref="MissingMemberException">Reflection failure.</exception>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static void MoveSpawnpointLocal(ZombieSpawnpoint point, Vector3 position, out RegionIdentifier region)
    {
        if (SetZombieSpawnpointPoint == null)
            throw new MissingMemberException("Instance setter for ZombieSpawnpoint.point is not valid.");

        ThreadUtil.assertIsGameThread();

        RegionUtil.AssertGetRegion(position, out byte x, out byte y);
        Vector3 oldPosition = point.point;
        point.SetPoint(position);

        RegionIdentifier? removedFrom = null;
        bool needsRemove = true;

        RegionIdentifier? changedOldRegion = null;
        ZombieSpawnpoint? changed = null;

        RegionUtil.ForEachRegion(oldPosition, coord =>
        {
            int index = LevelZombies.spawns[coord.x, coord.y].IndexOf(point);
            if (index == -1)
                return true;

            RegionIdentifier newRegion = new RegionIdentifier(coord.x, coord.y, index);

            needsRemove = x != coord.x || y != coord.y;
            if (needsRemove)
            {
                List<ZombieSpawnpoint> region = LevelZombies.spawns[coord.x, coord.y];
                if (region.Count - 1 == index)
                    region.RemoveAt(index);
                else if (region.Count > 1)
                {
                    region[index] = region[^1];
                    changed = region[index];
                    changedOldRegion = new RegionIdentifier(coord.x, coord.y, region.Count - 1);
#if CLIENT
                    if (changed.node != null && changed.node.TryGetComponent(out ZombieSpawnpointNode node))
                    {
                        node.IsAdded = true;
                        node.Region = newRegion;
                        node.Spawnpoint = changed;
                    }
#endif
                    region.RemoveAt(region.Count - 1);
                    Logger.DevkitServer.LogDebug(nameof(MoveSpawnpointLocal), $"Zombie spawnpoint region updated after replacing other removed spawnpoint on move: {changedOldRegion.Value.Format()} -> {newRegion.Format()}.");
                }
            }
            removedFrom = newRegion;
            return false;
        });
        if (needsRemove)
        {
            List<ZombieSpawnpoint> regionList = LevelZombies.spawns[x, y];
            region = new RegionIdentifier(x, y, regionList.Count);
            regionList.Add(point);
        }
        else
            region = removedFrom!.Value;

        if (point.node != null)
        {
            if (!point.node.transform.position.IsNearlyEqual(position))
                point.node.transform.position = position;
#if CLIENT
            if (!point.node.TryGetComponent(out ZombieSpawnpointNode spawnpoint))
                spawnpoint = point.node.gameObject.AddComponent<ZombieSpawnpointNode>();

            spawnpoint.IsAdded = true;
            spawnpoint.Region = region;
            spawnpoint.Spawnpoint = point;
#endif
        }

        if (removedFrom.HasValue)
        {
            if (needsRemove)
                EventOnZombieSpawnpointRegionUpdated.TryInvoke(point, removedFrom.Value, region);

            if (changed != null)
                EventOnZombieSpawnpointRegionUpdated.TryInvoke(changed, changedOldRegion!.Value, removedFrom!.Value);

            EventOnZombieSpawnpointMoved.TryInvoke(point, region, oldPosition, position);

            Logger.DevkitServer.LogDebug(nameof(MoveSpawnpointLocal), $"Zombie spawnpoint moved: {removedFrom.Value.Format()} -> {region.Format()}, pos: {oldPosition.Format("F0")} -> {position.Format("F0")}.");
        }
        else
        {
            EventOnZombieSpawnpointAdded.TryInvoke(point, region);
            Logger.DevkitServer.LogDebug(nameof(MoveSpawnpointLocal), $"Zombie spawnpoint added on move: {region.Format()}.");
        }
    }

    /// <summary>
    /// Locally changes the yaw (y-axis) angle of the spawnpoint to <paramref name="yaw"/>. Calls local events.
    /// </summary>
    /// <exception cref="MissingMemberException">Reflection failure.</exception>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static void RotateSpawnpointLocal(VehicleSpawnpoint point, float yaw)
    {
        if (SetVehicleSpawnpointAngle == null)
            throw new MissingMemberException("Instance setter for VehicleSpawnpoint.angle is not valid.");

        ThreadUtil.assertIsGameThread();

        float oldYaw = point.angle;
        point.SetYaw(yaw);
        if (point.node != null)
        {
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
            if (!point.node.transform.rotation.IsNearlyEqual(rotation))
                point.node.transform.rotation = rotation;
#if CLIENT
            if (!point.node.TryGetComponent(out VehicleSpawnpointNode spawnpoint))
                spawnpoint = point.node.gameObject.AddComponent<VehicleSpawnpointNode>();

            spawnpoint.Spawnpoint = point;
#endif
        }

        EventOnVehicleSpawnpointMoved.TryInvoke(point, point.point, point.point, oldYaw, yaw);
        Logger.DevkitServer.LogDebug(nameof(RotateSpawnpointLocal), $"Vehicle spawnpoint rotated: {oldYaw.Format("F0")}° -> {yaw.Format("F0")}°.");
    }

    /// <summary>
    /// Locally changes the yaw (y-axis) angle of the spawnpoint to <paramref name="yaw"/>. Calls local events.
    /// </summary>
    /// <exception cref="MissingMemberException">Reflection failure.</exception>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static void RotateSpawnpointLocal(PlayerSpawnpoint point, float yaw)
    {
        if (SetPlayerSpawnpointAngle == null)
            throw new MissingMemberException("Instance setter for PlayerSpawnpoint.angle is not valid.");

        ThreadUtil.assertIsGameThread();

        float oldYaw = point.angle;
        point.SetYaw(yaw);
        if (point.node != null)
        {
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
            if (!point.node.transform.rotation.IsNearlyEqual(rotation))
                point.node.transform.rotation = rotation;
#if CLIENT
            if (!point.node.TryGetComponent(out PlayerSpawnpointNode spawnpoint))
                spawnpoint = point.node.gameObject.AddComponent<PlayerSpawnpointNode>();

            spawnpoint.Spawnpoint = point;
#endif
        }

        EventOnPlayerSpawnpointMoved.TryInvoke(point, point.point, point.point, oldYaw, yaw);
        Logger.DevkitServer.LogDebug(nameof(RotateSpawnpointLocal), $"Player spawnpoint rotated: {oldYaw.Format("F0")}° -> {yaw.Format("F0")}°.");
    }

    /// <summary>
    /// Locally changes the position and yaw (y-axis) angle of the spawnpoint to <paramref name="position"/> and <paramref name="yaw"/>. Calls local events.
    /// </summary>
    /// <exception cref="MissingMemberException">Reflection failure.</exception>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static void TransformSpawnpointLocal(VehicleSpawnpoint point, Vector3 position, float yaw)
    {
        if (SetVehicleSpawnpointPoint == null)
            throw new MissingMemberException("Instance setter for VehicleSpawnpoint.point is not valid.");
        if (SetVehicleSpawnpointAngle == null)
            throw new MissingMemberException("Instance setter for VehicleSpawnpoint.angle is not valid.");

        ThreadUtil.assertIsGameThread();

        float oldYaw = point.angle;
        Vector3 oldPosition = point.point;
        point.SetYaw(yaw);
        point.SetPoint(position);
        if (point.node != null)
        {
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
            bool rot = !point.node.transform.rotation.IsNearlyEqual(rotation);
            bool pos = !point.node.transform.position.IsNearlyEqual(position);

            if (rot && pos)
                point.node.transform.SetPositionAndRotation(position, rotation);
            else if (pos)
                point.node.transform.position = position;
            else if (rot)
                point.node.transform.rotation = rotation;
#if CLIENT
            if (!point.node.TryGetComponent(out VehicleSpawnpointNode spawnpoint))
                spawnpoint = point.node.gameObject.AddComponent<VehicleSpawnpointNode>();

            spawnpoint.Spawnpoint = point;
#endif
        }

        EventOnVehicleSpawnpointMoved.TryInvoke(point, oldPosition, position, oldYaw, yaw);
        Logger.DevkitServer.LogDebug(nameof(TransformSpawnpointLocal), $"Vehicle spawnpoint transformed: {oldPosition.Format("F0")} -> {position.Format("F0")}, {oldYaw.Format("F0")}° -> {yaw.Format("F0")}°.");
    }

    /// <summary>
    /// Locally changes the position and yaw (y-axis) angle of the spawnpoint to <paramref name="position"/> and <paramref name="yaw"/>. Calls local events.
    /// </summary>
    /// <exception cref="MissingMemberException">Reflection failure.</exception>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static void TransformSpawnpointLocal(PlayerSpawnpoint point, Vector3 position, float yaw)
    {
        if (SetPlayerSpawnpointPoint == null)
            throw new MissingMemberException("Instance setter for PlayerSpawnpoint.point is not valid.");
        if (SetPlayerSpawnpointAngle == null)
            throw new MissingMemberException("Instance setter for PlayerSpawnpoint.angle is not valid.");

        ThreadUtil.assertIsGameThread();

        float oldYaw = point.angle;
        Vector3 oldPosition = point.point;
        point.SetYaw(yaw);
        point.SetPoint(position);
        if (point.node != null)
        {
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
            bool rot = !point.node.transform.rotation.IsNearlyEqual(rotation);
            bool pos = !point.node.transform.position.IsNearlyEqual(position);

            if (rot && pos)
                point.node.transform.SetPositionAndRotation(position, rotation);
            else if (pos)
                point.node.transform.position = position;
            else if (rot)
                point.node.transform.rotation = rotation;
#if CLIENT
            if (!point.node.TryGetComponent(out PlayerSpawnpointNode spawnpoint))
                spawnpoint = point.node.gameObject.AddComponent<PlayerSpawnpointNode>();

            spawnpoint.Spawnpoint = point;
#endif
        }

        EventOnPlayerSpawnpointMoved.TryInvoke(point, oldPosition, position, oldYaw, yaw);
        Logger.DevkitServer.LogDebug(nameof(TransformSpawnpointLocal), $"Player spawnpoint transformed: {oldPosition.Format("F0")} -> {position.Format("F0")}, {oldYaw.Format("F0")}° -> {yaw.Format("F0")}°.");
    }

    internal static void SetPoint(this AnimalSpawnpoint spawn, Vector3 point) => SetAnimalSpawnpointPoint?.Invoke(spawn, point);
    internal static void SetPoint(this VehicleSpawnpoint spawn, Vector3 point) => SetVehicleSpawnpointPoint?.Invoke(spawn, point);
    internal static void SetPoint(this PlayerSpawnpoint spawn, Vector3 point) => SetPlayerSpawnpointPoint?.Invoke(spawn, point);
    internal static void SetPoint(this ItemSpawnpoint spawn, Vector3 point) => SetItemSpawnpointPoint?.Invoke(spawn, point);
    internal static void SetPoint(this ZombieSpawnpoint spawn, Vector3 point) => SetZombieSpawnpointPoint?.Invoke(spawn, point);
    internal static void SetYaw(this VehicleSpawnpoint spawn, float yaw) => SetVehicleSpawnpointAngle?.Invoke(spawn, yaw);
    internal static void SetYaw(this PlayerSpawnpoint spawn, float yaw) => SetPlayerSpawnpointAngle?.Invoke(spawn, yaw);
#if CLIENT
    internal static void SetNodeShaders(Transform? node)
    {
        if (node == null)
            return;
        node.gameObject.layer = 3;
        if (SharedResources.LogicShader != null && node.gameObject.TryGetComponent(out Renderer renderer))
            renderer.material.shader = SharedResources.LogicShader;
    }
#endif
}
