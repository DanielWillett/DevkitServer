using DevkitServer.API;
using DevkitServer.API.Devkit.Spawns;
using DevkitServer.API.Iterators;
using DevkitServer.API.Permissions;
using DevkitServer.Core.Permissions;
using DevkitServer.Models;
using DevkitServer.Multiplayer.Actions;
using DevkitServer.Multiplayer.Levels;
using DevkitServer.Multiplayer.Networking;
using DevkitServer.Players;
using DevkitServer.Util.Region;
#if CLIENT
using Cysharp.Threading.Tasks;
using DevkitServer.Core;
using DevkitServer.Core.Tools;
#elif SERVER
using DevkitServer.API.UI;
#endif

namespace DevkitServer.Util;

public delegate void AnimalSpawnpointMoved(AnimalSpawnpoint point, int index, Vector3 fromPosition, Vector3 toPosition);
public delegate void VehicleSpawnpointMoved(VehicleSpawnpoint point, int index, Vector3 fromPosition, Vector3 toPosition, float fromAngle, float toAngle);
public delegate void PlayerSpawnpointMoved(PlayerSpawnpoint point, int index, Vector3 fromPosition, Vector3 toPosition, float fromAngle, float toAngle);
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
    public const int MaxUpdateSpawnTypeCount = 32;
    public const int MaxMoveSpawnCount = 24;
    public const int MaxUpdateSpawnIsAlternateCount = 32;
    public const int MaxDeleteSpawnCount = 32;

    [UsedImplicitly]
    private static readonly NetCall<byte, NetId64, Vector3, float, byte> RequestSpawnInstantiation = new NetCall<byte, NetId64, Vector3, float, byte>(DevkitServerNetCall.RequestSpawnInstantiation);
    [UsedImplicitly]
    private static readonly NetCall<byte, NetId64, NetId64, Vector3, float, byte, ulong> SendSpawnInstantiation = new NetCall<byte, NetId64, NetId64, Vector3, float, byte, ulong>(DevkitServerNetCall.SendSpawnInstantiation);

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
    /// Locally sets the <see cref="PlayerSpawnpoint.isAlt"/> backing field to <paramref name="isAlternate"/>.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <returns>The status of the operation.</returns>
    /// <exception cref="MemberAccessException">Reflection failure.</exception>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static SpawnpointEventResult SetPlayerIsAlternateLocal(int playerSpawnpointIndex, bool isAlternate)
    {
        ThreadUtil.assertIsGameThread();

        if (SetPlayerSpawnpointIsAlternate == null)
            throw new MemberAccessException("Instance setter for PlayerSpawnpoint.isAlt is not valid.");

        if (playerSpawnpointIndex < 0 || playerSpawnpointIndex >= LevelPlayers.spawns.Count)
            return SpawnpointEventResult.IndexOutOfRange;

        PlayerSpawnpoint spawn = LevelPlayers.spawns[playerSpawnpointIndex];
        bool oldIsAlternate = spawn.isAlt;

        if (oldIsAlternate == isAlternate)
            return SpawnpointEventResult.IgnoredAlreadySameValue;

#if CLIENT
        if (oldIsAlternate != isAlternate && spawn.node != null && spawn.node.TryGetComponent(out PlayerSpawnpointNode node))
        {
            node.Color = isAlternate ? Color.black : Color.white;
        }
#endif

        SetPlayerSpawnpointIsAlternate.Invoke(spawn, isAlternate);

        Logger.DevkitServer.LogDebug(nameof(SetPlayerIsAlternateLocal), $"Player spawnpoint updated: {(oldIsAlternate ? "Alternate" : "Primary")} -> {(isAlternate ? "Alternate" : "Primary")}");
        EventOnPlayerSpawnpointIsAlternateChanged.TryInvoke(spawn, playerSpawnpointIndex, isAlternate);

        return SpawnpointEventResult.Success;
    }

    /// <summary>
    /// Sets the <see cref="PlayerSpawnpoint.isAlt"/> backing field to <paramref name="isAlternate"/>.
    /// </summary>
    /// <remarks>Replicates to clients.</remarks>
    /// <returns>The status of the operation.</returns>
    /// <exception cref="MemberAccessException">Reflection failure.</exception>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static SpawnpointEventResult SetPlayerIsAlternate(int playerSpawnpointIndex, bool isAlternate)
    {
        ThreadUtil.assertIsGameThread();

        if (!DevkitServerModule.IsEditing)
            return SetPlayerIsAlternateLocal(playerSpawnpointIndex, isAlternate);
        
        if (playerSpawnpointIndex < 0 || playerSpawnpointIndex >= LevelPlayers.spawns.Count)
            return SpawnpointEventResult.IndexOutOfRange;

        PlayerSpawnpoint spawn = LevelPlayers.spawns[playerSpawnpointIndex];
        if (spawn.isAlt == isAlternate)
            return SpawnpointEventResult.IgnoredAlreadySameValue;

        if (!SpawnsNetIdDatabase.TryGetPlayerSpawnNetId(playerSpawnpointIndex, out NetId64 netId))
        {
            Logger.DevkitServer.LogWarning(nameof(SetPlayerIsAlternate), $"Unable to find NetId of player spawn {playerSpawnpointIndex.Format()}.");
            return SpawnpointEventResult.NetIdNotFound;
        }

#if CLIENT
        if (!VanillaPermissions.SpawnsPlayerEdit.Has())
            return SpawnpointEventResult.NoPermissions;

        SetPlayerSpawnpointIsAlternateProperties properties = new SetPlayerSpawnpointIsAlternateProperties(netId, isAlternate, CachedTime.DeltaTime);

        bool shouldAllow = true;
        ClientEvents.InvokeOnSetPlayerSpawnpointIsAlternateRequested(in properties, ref shouldAllow);

        if (!shouldAllow)
            return SpawnpointEventResult.CancelledByEvent;

        SetPlayerIsAlternateLocal(playerSpawnpointIndex, isAlternate);

        ClientEvents.InvokeOnSetPlayerSpawnpointIsAlternate(in properties);
        if (!ClientEvents.ListeningOnSetPlayerSpawnpointsIsAlternate)
            return SpawnpointEventResult.Success;

        NetId64.OneArray[0] = netId;
        ClientEvents.InvokeOnSetPlayerSpawnpointsIsAlternate(new SetPlayerSpawnpointsIsAlternateProperties(NetId64.OneArray, isAlternate, CachedTime.DeltaTime));
#elif SERVER
        SetPlayerIsAlternateLocal(playerSpawnpointIndex, isAlternate);
        EditorActions.QueueServerAction(new SetPlayerSpawnpointsIsAlternateAction
        {
            DeltaTime = CachedTime.DeltaTime,
            NetIds = [ netId ],
            IsAlternate = isAlternate
        });
#endif
        return SpawnpointEventResult.Success;
    }

    /// <summary>
    /// Locally removes a spawnpoint from the list and destroyes the node.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <returns>The status of the operation.</returns>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Spawn type must be 'Animal', 'Vehicle', or 'Player'.</exception>
    public static SpawnpointEventResult RemoveSpawnLocal(SpawnType spawnType, int index) => RemoveSpawnLocal(spawnType, index, true);

    /// <summary>
    /// Locally removes a spawnpoint from the list and destroyes the node.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <returns>The status of the operation.</returns>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Spawn type must be 'Animal', 'Vehicle', or 'Player'.</exception>
    internal static SpawnpointEventResult RemoveSpawnLocal(SpawnType spawnType, int index, bool destroyNode)
    {
        ThreadUtil.assertIsGameThread();

        if (spawnType is not SpawnType.Animal and not SpawnType.Vehicle and not SpawnType.Player)
            throw new ArgumentOutOfRangeException(nameof(spawnType), "Spawn type must be 'Animal', 'Vehicle', or 'Player'.");

        if (!CheckSpawnpointSafe(spawnType, index))
            return SpawnpointEventResult.IndexOutOfRange;

        if (spawnType == SpawnType.Animal)
            RemoveAnimalSpawnLocal(index, destroyNode);
        else if (spawnType == SpawnType.Vehicle)
            RemoveVehicleSpawnLocal(index, destroyNode);
        else
            RemovePlayerSpawnLocal(index, destroyNode);

        return SpawnpointEventResult.Success;
    }

    /// <summary>
    /// Removes a spawnpoint from the list and destroyes the node.
    /// </summary>
    /// <remarks>Replicates to clients.</remarks>
    /// <returns>The status of the operation.</returns>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Spawn type must be 'Animal', 'Vehicle', or 'Player'.</exception>
    public static SpawnpointEventResult RemoveSpawn(SpawnType spawnType, int index)
    {
        ThreadUtil.assertIsGameThread();

        if (!DevkitServerModule.IsEditing)
            return RemoveSpawnLocal(spawnType, index, true);

        if (spawnType is not SpawnType.Animal and not SpawnType.Vehicle and not SpawnType.Player)
            throw new ArgumentOutOfRangeException(nameof(spawnType), "Spawn type must be 'Animal', 'Vehicle', or 'Player'.");

        if (!CheckSpawnpointSafe(spawnType, index))
            return SpawnpointEventResult.IndexOutOfRange;

        if (!SpawnsNetIdDatabase.TryGetSpawnNetId(spawnType, index, out NetId64 netId))
        {
            Logger.DevkitServer.LogWarning(nameof(RemoveSpawn), $"Unable to find NetId of {spawnType.GetLowercaseText()} spawn at {index.Format()}.");
            return SpawnpointEventResult.NetIdNotFound;
        }

#if CLIENT
        if (!VanillaPermissions.SpawnsDelete(spawnType).Has())
            return SpawnpointEventResult.NoPermissions;

        DeleteSpawnProperties properties = new DeleteSpawnProperties(netId, spawnType, CachedTime.DeltaTime);

        bool shouldAllow = true;
        ClientEvents.InvokeOnDeleteSpawnRequested(in properties, ref shouldAllow);

        if (!shouldAllow)
            return SpawnpointEventResult.CancelledByEvent;

        RemoveSpawnLocal(spawnType, index, true);

        ClientEvents.InvokeOnDeleteSpawn(in properties);
        if (!ClientEvents.ListeningOnDeleteSpawns)
            return SpawnpointEventResult.Success;

        NetId64.OneArray[0] = netId;
        ClientEvents.InvokeOnDeleteSpawns(new DeleteSpawnsProperties(NetId64.OneArray, spawnType, CachedTime.DeltaTime));
#elif SERVER
        RemoveSpawnLocal(spawnType, index, true);
        EditorActions.QueueServerAction(new DeleteSpawnsAction
        {
            DeltaTime = CachedTime.DeltaTime,
            SpawnType = spawnType,
            NetIds = [ netId ]
        });
#endif
        return SpawnpointEventResult.Success;
    }

    /// <summary>
    /// Locally removes a spawnpoint from the list and destroyes the node.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <returns>The status of the operation.</returns>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Spawn type must be 'Item' or 'Zombie'.</exception>
    public static SpawnpointEventResult RemoveSpawnLocal(SpawnType spawnType, RegionIdentifier region) => RemoveSpawnLocal(spawnType, region, true);

    /// <summary>
    /// Locally removes a spawnpoint from the list and destroyes the node.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <returns>The status of the operation.</returns>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Spawn type must be 'Item' or 'Zombie'.</exception>
    internal static SpawnpointEventResult RemoveSpawnLocal(SpawnType spawnType, RegionIdentifier region, bool destroyNode)
    {
        ThreadUtil.assertIsGameThread();

        if (spawnType is not SpawnType.Item and not SpawnType.Zombie)
            throw new ArgumentOutOfRangeException(nameof(spawnType), "Spawn type must be 'Item' or 'Zombie'.");

        if (!CheckSpawnpointSafe(spawnType, region))
            return SpawnpointEventResult.IndexOutOfRange;

        if (spawnType == SpawnType.Item)
            RemoveItemSpawnLocal(region, destroyNode);
        else
            RemoveZombieSpawnLocal(region, destroyNode);

        return SpawnpointEventResult.Success;
    }

    /// <summary>
    /// Removes a spawnpoint from the list and destroyes the node.
    /// </summary>
    /// <remarks>Replicates to clients.</remarks>
    /// <returns>The status of the operation.</returns>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Spawn type must be 'Item' or 'Zombie'.</exception>
    public static SpawnpointEventResult RemoveSpawn(SpawnType spawnType, RegionIdentifier region)
    {
        ThreadUtil.assertIsGameThread();

        if (!DevkitServerModule.IsEditing)
            return RemoveSpawnLocal(spawnType, region, true);

        if (spawnType is not SpawnType.Item and not SpawnType.Zombie)
            throw new ArgumentOutOfRangeException(nameof(spawnType), "Spawn type must be 'Item' or 'Zombie'.");

        if (!CheckSpawnpointSafe(spawnType, region))
            return SpawnpointEventResult.IndexOutOfRange;

        if (!SpawnsNetIdDatabase.TryGetSpawnNetId(spawnType, region, out NetId64 netId))
        {
            Logger.DevkitServer.LogWarning(nameof(RemoveSpawn), $"Unable to find NetId of {spawnType.GetLowercaseText()} spawn at {region.Format()}.");
            return SpawnpointEventResult.NetIdNotFound;
        }

#if CLIENT
        if (!VanillaPermissions.SpawnsDelete(spawnType).Has())
            return SpawnpointEventResult.NoPermissions;

        DeleteSpawnProperties properties = new DeleteSpawnProperties(netId, spawnType, CachedTime.DeltaTime);

        bool shouldAllow = true;
        ClientEvents.InvokeOnDeleteSpawnRequested(in properties, ref shouldAllow);

        if (!shouldAllow)
            return SpawnpointEventResult.CancelledByEvent;

        RemoveSpawnLocal(spawnType, region, true);

        ClientEvents.InvokeOnDeleteSpawn(in properties);
        if (!ClientEvents.ListeningOnDeleteSpawns)
            return SpawnpointEventResult.Success;

        NetId64.OneArray[0] = netId;
        ClientEvents.InvokeOnDeleteSpawns(new DeleteSpawnsProperties(NetId64.OneArray, spawnType, CachedTime.DeltaTime));
#elif SERVER
        RemoveSpawnLocal(spawnType, region, true);
        EditorActions.QueueServerAction(new DeleteSpawnsAction
        {
            DeltaTime = CachedTime.DeltaTime,
            SpawnType = spawnType,
            NetIds = [ netId ]
        });
#endif

        return SpawnpointEventResult.Success;
    }
    private static void RemoveAnimalSpawnLocal(int index, bool destroyNode)
    {
        ThreadUtil.assertIsGameThread();

        AnimalSpawnpoint spawn = LevelAnimals.spawns[index];

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
            Object.Destroy(spawn.node.gameObject);
    }
    private static void RemoveVehicleSpawnLocal(int index, bool destroyNode)
    {
        ThreadUtil.assertIsGameThread();

        VehicleSpawnpoint spawn = LevelVehicles.spawns[index];

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
            Object.Destroy(spawn.node.gameObject);
    }
    private static void RemovePlayerSpawnLocal(int index, bool destroyNode)
    {
        ThreadUtil.assertIsGameThread();

        PlayerSpawnpoint spawn = LevelPlayers.spawns[index];

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
            Object.Destroy(spawn.node.gameObject);
    }
    private static void RemoveItemSpawnLocal(RegionIdentifier region, bool destroyNode)
    {
        ThreadUtil.assertIsGameThread();

        ItemSpawnpoint spawn = region.FromList(LevelItems.spawns);

        List<ItemSpawnpoint> regionList = region.GetList(LevelItems.spawns);
        int index = region.Index;

        if (index == regionList.Count - 1)
        {
            regionList.RemoveAt(index);
            EventOnItemSpawnpointRemoved.TryInvoke(spawn, region);
        }
        else
        {
            RegionIdentifier oldRegion = new RegionIdentifier(region.X, region.Y, regionList.Count - 1);
            regionList[index] = regionList[oldRegion.Index];
            ItemSpawnpoint changed = regionList[index];
#if CLIENT
            if (changed.node != null && changed.node.TryGetComponent(out ItemSpawnpointNode node))
            {
                node.IsAdded = true;
                node.Region = region;
                node.Spawnpoint = changed;
            }
#endif
            regionList.RemoveAt(oldRegion.Index);
            EventOnItemSpawnpointRemoved.TryInvoke(spawn, region);

            EventOnItemSpawnpointRegionUpdated.TryInvoke(changed, oldRegion, region);
            Logger.DevkitServer.LogDebug(nameof(RemoveItemSpawnLocal), $"Item spawnpoint index updated after replacing other removed spawnpoint on add: {oldRegion.Format()} -> {region.Format()}.");
        }

        Logger.DevkitServer.LogDebug(nameof(RemoveItemSpawnLocal), $"Item spawnpoint removed: {region.Format()}.");

        if (destroyNode && spawn.node != null)
            Object.Destroy(spawn.node.gameObject);
    }
    private static void RemoveZombieSpawnLocal(RegionIdentifier region, bool destroyNode)
    {
        ThreadUtil.assertIsGameThread();

        ZombieSpawnpoint spawn = region.FromList(LevelZombies.spawns);

        List<ZombieSpawnpoint> regionList = region.GetList(LevelZombies.spawns);
        int index = region.Index;

        if (index == regionList.Count - 1)
        {
            regionList.RemoveAt(index);
            EventOnZombieSpawnpointRemoved.TryInvoke(spawn, region);
        }
        else
        {
            RegionIdentifier oldRegion = new RegionIdentifier(region.X, region.Y, regionList.Count - 1);
            regionList[index] = regionList[oldRegion.Index];
            ZombieSpawnpoint changed = regionList[index];
#if CLIENT
            if (changed.node != null && changed.node.TryGetComponent(out ZombieSpawnpointNode node))
            {
                node.IsAdded = true;
                node.Region = region;
                node.Spawnpoint = changed;
            }
#endif
            regionList.RemoveAt(oldRegion.Index);
            EventOnZombieSpawnpointRemoved.TryInvoke(spawn, region);

            EventOnZombieSpawnpointRegionUpdated.TryInvoke(changed, oldRegion, region);
            Logger.DevkitServer.LogDebug(nameof(RemoveZombieSpawnLocal), $"Zombie spawnpoint index updated after replacing other removed spawnpoint on add: {oldRegion.Format()} -> {region.Format()}.");
        }

        Logger.DevkitServer.LogDebug(nameof(RemoveZombieSpawnLocal), $"Zombie spawnpoint removed: {region.Format()}.");

        if (destroyNode && spawn.node != null)
            Object.Destroy(spawn.node.gameObject);
    }

    /// <summary>
    /// Add an <see cref="AnimalSpawnpoint"/> to <see cref="LevelAnimals"/> if it isn't already added and adds any necessary component to it.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <returns>Index of the created spawnpoint.</returns>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="InvalidOperationException">Too many spawns in the world (65535).</exception>
    public static int AddAnimalSpawnpointLocal(AnimalSpawnpoint spawn)
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
            Logger.DevkitServer.LogDebug(nameof(AddAnimalSpawnpointLocal), "Animal spawnpoint added.");
            if (DevkitServerModule.IsEditing)
                SpawnsNetIdDatabase.RegisterIndexSpawnpoint(SpawnType.Animal, index);
        }
        else
            Logger.DevkitServer.LogDebug(nameof(AddAnimalSpawnpointLocal), "Animal spawnpoint already added.");

        return index;
    }

    /// <summary>
    /// Add a <see cref="VehicleSpawnpoint"/> to <see cref="LevelVehicles"/> if it isn't already added and adds any necessary component to it.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <returns>Index of the created spawnpoint.</returns>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="InvalidOperationException">Too many spawns in the world (65535).</exception>
    public static int AddVehicleSpawnpointLocal(VehicleSpawnpoint spawn)
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
            Logger.DevkitServer.LogDebug(nameof(AddVehicleSpawnpointLocal), "Vehicle spawnpoint added.");
            if (DevkitServerModule.IsEditing)
                SpawnsNetIdDatabase.RegisterIndexSpawnpoint(SpawnType.Vehicle, index);
        }
        else
            Logger.DevkitServer.LogDebug(nameof(AddVehicleSpawnpointLocal), "Vehicle spawnpoint already added.");

        return index;
    }

    /// <summary>
    /// Add a <see cref="PlayerSpawnpoint"/> to <see cref="LevelPlayers"/> if it isn't already added and adds any necessary component to it.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <returns>Index of the created spawnpoint.</returns>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="InvalidOperationException">Too many spawns in the world (255).</exception>
    public static int AddPlayerSpawnpointLocal(PlayerSpawnpoint spawn)
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
            Logger.DevkitServer.LogDebug(nameof(AddPlayerSpawnpointLocal), "Player spawnpoint added.");
            if (DevkitServerModule.IsEditing)
                SpawnsNetIdDatabase.RegisterIndexSpawnpoint(SpawnType.Player, index);
        }
        else
            Logger.DevkitServer.LogDebug(nameof(AddPlayerSpawnpointLocal), "Player spawnpoint already added.");

        return index;
    }

    /// <summary>
    /// Add an <see cref="ItemSpawnpoint"/> to <see cref="LevelItems"/> if it isn't already added and adds any necessary component to it.
    /// </summary>
    /// <remarks>Non-replicating. Region is calculated from <see cref="ItemSpawnpoint.point"/>.</remarks>
    /// <returns>Region identifier of the created spawnpoint.</returns>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="InvalidOperationException">Too many spawns in that region (65535). - OR - The given poisition is not in a region.</exception>
    public static RegionIdentifier AddItemSpawnpointLocal(ItemSpawnpoint spawn)
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
                if (index == -1)
                    continue;
                alreadyExists = true;
                break;
            }
        }
        int index2 = index;

        RegionIdentifier? changedOldRegion = null, changedNewRegion = null;
        ItemSpawnpoint? changed = null;

        bool regionChanged = false;
        if (!Regions.tryGetCoordinate(spawn.point, out byte x2, out byte y2))
            throw new InvalidOperationException($"Given position {spawn.point:0.##} is not in a region.");

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
                Logger.DevkitServer.LogDebug(nameof(AddItemSpawnpointLocal), $"Item spawnpoint region updated after replacing other removed spawnpoint on add: {changedOldRegion.Value.Format()} -> {changedNewRegion.Value.Format()}.");
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

            Logger.DevkitServer.LogDebug(nameof(AddItemSpawnpointLocal), $"Item spawnpoint region updated on add: {oldRegion.Format()} -> {regionId.Format()}.");
        }
        else if (!alreadyExists)
        {
            EventOnItemSpawnpointAdded.TryInvoke(spawn, regionId);
            Logger.DevkitServer.LogDebug(nameof(AddItemSpawnpointLocal), $"Item spawnpoint added: {regionId.Format()}.");
            if (DevkitServerModule.IsEditing)
                SpawnsNetIdDatabase.RegisterRegionSpawnpoint(SpawnType.Item, regionId);
        }

        return regionId;
    }

    /// <summary>
    /// Add a <see cref="ZombieSpawnpoint"/> to <see cref="LevelZombies"/> if it isn't already added and adds any necessary component to it.
    /// </summary>
    /// <remarks>Non-replicating. Region is calculated from <see cref="ZombieSpawnpoint.point"/>.</remarks>
    /// <returns>Region identifier of the created spawnpoint.</returns>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="InvalidOperationException">Too many spawns in that region (65535). - OR - The given poisition is not in a region.</exception>
    public static RegionIdentifier AddZombieSpawnpointLocal(ZombieSpawnpoint spawn)
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
            throw new InvalidOperationException($"Given position {spawn.point:0.##} is not in a region.");

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
                Logger.DevkitServer.LogDebug(nameof(AddZombieSpawnpointLocal), $"Zombie spawnpoint region updated after replacing other removed spawnpoint on add: {changedOldRegion.Value.Format()} -> {changedNewRegion.Value.Format()}.");
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

            Logger.DevkitServer.LogDebug(nameof(AddZombieSpawnpointLocal), $"Zombie spawnpoint region updated on add: {oldRegion.Format()} -> {regionId.Format()}.");
        }
        else if (!alreadyExists)
        {
            EventOnZombieSpawnpointAdded.TryInvoke(spawn, regionId);
            Logger.DevkitServer.LogDebug(nameof(AddZombieSpawnpointLocal), $"Zombie spawnpoint added: {regionId.Format()}.");
            if (DevkitServerModule.IsEditing)
                SpawnsNetIdDatabase.RegisterRegionSpawnpoint(SpawnType.Zombie, regionId);
        }

        return regionId;
    }

    /// <summary>
    /// Locally moves the spawnpoint to <paramref name="position"/>. Calls local events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <returns>The status of the operation.</returns>
    /// <exception cref="MissingMemberException">Reflection failure.</exception>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Spawn type must be 'Animal', 'Vehicle', or 'Player'.</exception>
    public static SpawnpointEventResult MoveSpawnpointLocal(SpawnType spawnType, int index, Vector3 position)
    {
        if (SetVehicleSpawnpointPoint == null)
            throw new MissingMemberException("Instance setter for VehicleSpawnpoint.point is not valid.");

        if (spawnType is not SpawnType.Animal and not SpawnType.Vehicle and not SpawnType.Player)
            throw new ArgumentOutOfRangeException(nameof(spawnType), "Spawn type must be 'Animal', 'Vehicle', or 'Player'.");

        ThreadUtil.assertIsGameThread();

        if (!CheckSpawnpointSafe(spawnType, index))
            return SpawnpointEventResult.IndexOutOfRange;

        Vector3 oldPosition;
        switch (spawnType)
        {
            case SpawnType.Animal:
                AnimalSpawnpoint animalSpawnpoint = LevelAnimals.spawns[index];
                oldPosition = animalSpawnpoint.point;
                animalSpawnpoint.SetPoint(position);
                if (animalSpawnpoint.node != null)
                {
                    if (!animalSpawnpoint.node.transform.position.IsNearlyEqual(position))
                        animalSpawnpoint.node.transform.position = position;
#if CLIENT
                    if (!animalSpawnpoint.node.TryGetComponent(out AnimalSpawnpointNode spawnpoint))
                        spawnpoint = animalSpawnpoint.node.gameObject.AddComponent<AnimalSpawnpointNode>();

                    spawnpoint.Spawnpoint = animalSpawnpoint;
                    spawnpoint.Index = index;
#endif
                }
                EventOnAnimalSpawnpointMoved.TryInvoke(animalSpawnpoint, index, oldPosition, position);
                break;

            case SpawnType.Player:
                PlayerSpawnpoint playerSpawnpoint = LevelPlayers.spawns[index];
                oldPosition = playerSpawnpoint.point;
                playerSpawnpoint.SetPoint(position);
                if (playerSpawnpoint.node != null)
                {
                    if (!playerSpawnpoint.node.transform.position.IsNearlyEqual(position))
                        playerSpawnpoint.node.transform.position = position;
#if CLIENT
                    if (!playerSpawnpoint.node.TryGetComponent(out PlayerSpawnpointNode spawnpoint))
                        spawnpoint = playerSpawnpoint.node.gameObject.AddComponent<PlayerSpawnpointNode>();

                    spawnpoint.Spawnpoint = playerSpawnpoint;
                    spawnpoint.Index = index;
#endif
                }
                EventOnPlayerSpawnpointMoved.TryInvoke(playerSpawnpoint, index, oldPosition, position, playerSpawnpoint.angle, playerSpawnpoint.angle);
                break;

            default: // Vehicle
                VehicleSpawnpoint vehicleSpawnpoint = LevelVehicles.spawns[index];
                oldPosition = vehicleSpawnpoint.point;
                vehicleSpawnpoint.SetPoint(position);
                if (vehicleSpawnpoint.node != null)
                {
                    if (!vehicleSpawnpoint.node.transform.position.IsNearlyEqual(position))
                        vehicleSpawnpoint.node.transform.position = position;
#if CLIENT
                    if (!vehicleSpawnpoint.node.TryGetComponent(out VehicleSpawnpointNode spawnpoint))
                        spawnpoint = vehicleSpawnpoint.node.gameObject.AddComponent<VehicleSpawnpointNode>();

                    spawnpoint.Spawnpoint = vehicleSpawnpoint;
                    spawnpoint.Index = index;
#endif
                }
                EventOnVehicleSpawnpointMoved.TryInvoke(vehicleSpawnpoint, index, oldPosition, position, vehicleSpawnpoint.angle, vehicleSpawnpoint.angle);
                break;
        }

        Logger.DevkitServer.LogDebug(nameof(MoveSpawnpointLocal), $"{spawnType} spawnpoint moved: {oldPosition.Format("F0")} -> {position.Format("F0")}.");
        return SpawnpointEventResult.Success;
    }

    /// <summary>
    /// Moves the spawnpoint to <paramref name="position"/>. Calls local events.
    /// </summary>
    /// <remarks>Replicates to clients.</remarks>
    /// <returns>The status of the operation.</returns>
    /// <exception cref="MissingMemberException">Reflection failure.</exception>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Spawn type must be 'Animal', 'Vehicle', or 'Player'.</exception>
    public static SpawnpointEventResult MoveSpawnpoint(SpawnType spawnType, int index, Vector3 position)
    {
        ThreadUtil.assertIsGameThread();

        if (!DevkitServerModule.IsEditing)
            return MoveSpawnpointLocal(spawnType, index, position);

        if (spawnType is not SpawnType.Animal and not SpawnType.Vehicle and not SpawnType.Player)
            throw new ArgumentOutOfRangeException(nameof(spawnType), "Spawn type must be 'Animal', 'Vehicle', or 'Player'.");

        if (!CheckSpawnpointSafe(spawnType, index))
            return SpawnpointEventResult.IndexOutOfRange;

        if (!SpawnsNetIdDatabase.TryGetSpawnNetId(spawnType, index, out NetId64 netId))
        {
            Logger.DevkitServer.LogWarning(nameof(MoveSpawnpoint), $"Unable to find NetId of {spawnType.GetLowercaseText()} spawn at {index.Format()}.");
            return SpawnpointEventResult.NetIdNotFound;
        }

        float yaw = spawnType switch
        {
            SpawnType.Player => LevelPlayers.spawns[index].angle,
            SpawnType.Vehicle => LevelVehicles.spawns[index].angle,
            _ => 0f
        };
        Vector3 originalPos = spawnType switch
        {
            SpawnType.Player => LevelPlayers.spawns[index].point,
            SpawnType.Vehicle => LevelVehicles.spawns[index].point,
            _ => LevelAnimals.spawns[index].point
        };

        TransformationDelta t = new TransformationDelta(TransformationDelta.TransformFlags.Position, position, yaw, originalPos, yaw);
#if CLIENT
        if (!VanillaPermissions.SpawnsEdit(spawnType).Has())
            return SpawnpointEventResult.NoPermissions;

        MoveSpawnFinalProperties properties = new MoveSpawnFinalProperties(netId, t, spawnType, false, CachedTime.DeltaTime);

        bool shouldAllow = true;
        ClientEvents.InvokeOnMoveSpawnFinalRequested(in properties, ref shouldAllow);

        if (!shouldAllow)
            return SpawnpointEventResult.CancelledByEvent;

        MoveSpawnpointLocal(spawnType, index, position);

        ClientEvents.InvokeOnMoveSpawnFinal(in properties);
        if (!ClientEvents.ListeningOnMoveSpawnsFinal)
            return SpawnpointEventResult.Success;

        NetId64.OneArray[0] = netId;
        TransformationDelta.OneArray[0] = t;
        ClientEvents.InvokeOnMoveSpawnsFinal(new MoveSpawnsFinalProperties(NetId64.OneArray, TransformationDelta.OneArray, spawnType, false, CachedTime.DeltaTime));
#elif SERVER
        MoveSpawnpointLocal(spawnType, index, position);
        EditorActions.QueueServerAction(new MoveSpawnsFinalAction
        {
            DeltaTime = CachedTime.DeltaTime,
            SpawnType = spawnType,
            NetIds = [ netId ],
            Transformations = [ t ]
        });
#endif

        return SpawnpointEventResult.Success;
    }

    /// <summary>
    /// Locally moves the spawnpoint to <paramref name="position"/>. Calls local events.
    /// </summary>
    /// <remarks>Non-replicating. See by-reference overload to update <paramref name="region"/> if it changes.</remarks>
    /// <returns>The status of the operation.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="position"/> is not a valid position in the region grid - OR - Spawn type must be 'Item' or 'Zombie'.</exception>
    /// <exception cref="MissingMemberException">Reflection failure.</exception>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static SpawnpointEventResult MoveSpawnpointLocal(SpawnType spawnType, RegionIdentifier region, Vector3 position)
    {
        return MoveSpawnpointLocal(spawnType, ref region, position);
    }

    /// <summary>
    /// Locally moves the spawnpoint to <paramref name="position"/>. Calls local events. <paramref name="region"/> will be updated if the spawnpoint was moved out of the current region.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <returns>The status of the operation.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="position"/> is not a valid position in the region grid - OR - Spawn type must be 'Item' or 'Zombie'.</exception>
    /// <exception cref="MissingMemberException">Reflection failure.</exception>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static SpawnpointEventResult MoveSpawnpointLocal(SpawnType spawnType, ref RegionIdentifier region, Vector3 position)
    {
        if (SetItemSpawnpointPoint == null)
            throw new MissingMemberException("Instance setter for ItemSpawnpoint.point is not valid.");

        if (spawnType is not SpawnType.Item and not SpawnType.Zombie)
            throw new ArgumentOutOfRangeException(nameof(spawnType), "Spawn type must be 'Item' or 'Zombie'.");

        ThreadUtil.assertIsGameThread();

        RegionUtil.AssertGetRegion(position, out byte newX, out byte newY);

        if (!CheckSpawnpointSafe(spawnType, region))
            return SpawnpointEventResult.IndexOutOfRange;

        Vector3 oldPosition;
        RegionIdentifier oldRegion;
        if (spawnType == SpawnType.Item)
        {
            ItemSpawnpoint spawn = region.FromList(LevelItems.spawns);
            List<ItemSpawnpoint> regionList = region.GetList(LevelItems.spawns);

            int index = region.Index;

            oldPosition = spawn.point;
            spawn.SetPoint(position);

            RegionIdentifier? changedOldRegion = null;
            ItemSpawnpoint? changed = null;

            oldRegion = region;

            bool needsRemove = newX != region.X || newY != region.Y;
            if (needsRemove)
            {
                if (regionList.Count - 1 == index)
                    regionList.RemoveAt(index);
                else if (regionList.Count > 1)
                {
                    regionList[index] = regionList[^1];
                    changed = regionList[index];
                    changedOldRegion = new RegionIdentifier(region.X, region.Y, regionList.Count - 1);
#if CLIENT
                    if (changed.node != null && changed.node.TryGetComponent(out ItemSpawnpointNode node))
                    {
                        node.IsAdded = true;
                        node.Region = oldRegion;
                        node.Spawnpoint = changed;
                    }
#endif
                    regionList.RemoveAt(regionList.Count - 1);
                    Logger.DevkitServer.LogDebug(nameof(MoveSpawnpointLocal), $"Item spawnpoint region updated after replacing other removed spawnpoint on move: {changedOldRegion.Value.Format()} -> {region.Format()}.");
                }

                List<ItemSpawnpoint> newRegionList = LevelItems.spawns[newX, newY];
                region = new RegionIdentifier(newX, newY, regionList.Count);
                newRegionList.Add(spawn);
            }

            if (spawn.node != null)
            {
                if (!spawn.node.transform.position.IsNearlyEqual(position))
                    spawn.node.transform.position = position;
#if CLIENT
                if (!spawn.node.TryGetComponent(out ItemSpawnpointNode spawnpoint))
                    spawnpoint = spawn.node.gameObject.AddComponent<ItemSpawnpointNode>();

                spawnpoint.IsAdded = true;
                spawnpoint.Region = region;
                spawnpoint.Spawnpoint = spawn;
#endif
            }

            if (needsRemove)
                EventOnItemSpawnpointRegionUpdated.TryInvoke(spawn, oldRegion, region);

            if (changed != null)
                EventOnItemSpawnpointRegionUpdated.TryInvoke(changed, changedOldRegion!.Value, oldRegion);

            EventOnItemSpawnpointMoved.TryInvoke(spawn, region, oldPosition, position);
        }
        else // Zombie
        {
            ZombieSpawnpoint spawn = region.FromList(LevelZombies.spawns);
            List<ZombieSpawnpoint> regionList = region.GetList(LevelZombies.spawns);

            int index = region.Index;

            oldPosition = spawn.point;
            spawn.SetPoint(position);

            RegionIdentifier? changedOldRegion = null;
            ZombieSpawnpoint? changed = null;

            oldRegion = region;

            bool needsRemove = newX != region.X || newY != region.Y;
            if (needsRemove)
            {
                if (regionList.Count - 1 == index)
                    regionList.RemoveAt(index);
                else if (regionList.Count > 1)
                {
                    regionList[index] = regionList[^1];
                    changed = regionList[index];
                    changedOldRegion = new RegionIdentifier(region.X, region.Y, regionList.Count - 1);
#if CLIENT
                    if (changed.node != null && changed.node.TryGetComponent(out ZombieSpawnpointNode node))
                    {
                        node.IsAdded = true;
                        node.Region = oldRegion;
                        node.Spawnpoint = changed;
                    }
#endif
                    regionList.RemoveAt(regionList.Count - 1);
                    Logger.DevkitServer.LogDebug(nameof(MoveSpawnpointLocal), $"Zombie spawnpoint region updated after replacing other removed spawnpoint on move: {changedOldRegion.Value.Format()} -> {region.Format()}.");
                }

                List<ZombieSpawnpoint> newRegionList = LevelZombies.spawns[newX, newY];
                region = new RegionIdentifier(newX, newY, regionList.Count);
                newRegionList.Add(spawn);
            }

            if (spawn.node != null)
            {
                if (!spawn.node.transform.position.IsNearlyEqual(position))
                    spawn.node.transform.position = position;
#if CLIENT
                if (!spawn.node.TryGetComponent(out ZombieSpawnpointNode spawnpoint))
                    spawnpoint = spawn.node.gameObject.AddComponent<ZombieSpawnpointNode>();

                spawnpoint.IsAdded = true;
                spawnpoint.Region = region;
                spawnpoint.Spawnpoint = spawn;
#endif
            }

            if (needsRemove)
                EventOnZombieSpawnpointRegionUpdated.TryInvoke(spawn, oldRegion, region);

            if (changed != null)
                EventOnZombieSpawnpointRegionUpdated.TryInvoke(changed, changedOldRegion!.Value, oldRegion);

            EventOnZombieSpawnpointMoved.TryInvoke(spawn, region, oldPosition, position);
        }

        Logger.DevkitServer.LogDebug(nameof(MoveSpawnpointLocal), $"{spawnType.GetPropercaseText()} spawnpoint moved: {oldRegion.Format()} -> {region.Format()}, pos: {oldPosition.Format("F0")} -> {position.Format("F0")}.");
        return SpawnpointEventResult.Success;
    }

    /// <summary>
    /// Moves the spawnpoint to <paramref name="position"/>. Calls local events.
    /// </summary>
    /// <remarks>Replicates to clients. See by-reference overload to update <paramref name="region"/> if it changes.</remarks>
    /// <returns>The status of the operation.</returns>
    /// <exception cref="MissingMemberException">Reflection failure.</exception>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="position"/> is not a valid position in the region grid - OR - Spawn type must be 'Item' or 'Zombie'.</exception>
    public static SpawnpointEventResult MoveSpawnpoint(SpawnType spawnType, RegionIdentifier region, Vector3 position)
    {
        return MoveSpawnpoint(spawnType, ref region, position);
    }

    /// <summary>
    /// Moves the spawnpoint to <paramref name="position"/>. Calls local events. <paramref name="region"/> will be updated if the spawnpoint was moved out of the current region.
    /// </summary>
    /// <remarks>Replicates to clients.</remarks>
    /// <returns>The status of the operation.</returns>
    /// <exception cref="MissingMemberException">Reflection failure.</exception>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="position"/> is not a valid position in the region grid - OR - Spawn type must be 'Item' or 'Zombie'.</exception>
    public static SpawnpointEventResult MoveSpawnpoint(SpawnType spawnType, ref RegionIdentifier region, Vector3 position)
    {
        ThreadUtil.assertIsGameThread();

        if (!DevkitServerModule.IsEditing)
            return MoveSpawnpointLocal(spawnType, ref region, position);

        if (spawnType is not SpawnType.Item and not SpawnType.Zombie)
            throw new ArgumentOutOfRangeException(nameof(spawnType), "Spawn type must be 'Item' or 'Zombie'.");

        if (!CheckSpawnpointSafe(spawnType, region))
            return SpawnpointEventResult.IndexOutOfRange;

        if (!SpawnsNetIdDatabase.TryGetSpawnNetId(spawnType, region, out NetId64 netId))
        {
            Logger.DevkitServer.LogWarning(nameof(MoveSpawnpoint), $"Unable to find NetId of {spawnType.GetLowercaseText()} spawn at {region.Format()}.");
            return SpawnpointEventResult.NetIdNotFound;
        }

        const float yaw = 0f;
        Vector3 originalPos = spawnType switch
        {
            SpawnType.Item => region.FromList(LevelItems.spawns).point,
            _ => region.FromList(LevelZombies.spawns).point
        };

        TransformationDelta t = new TransformationDelta(TransformationDelta.TransformFlags.Position, position, yaw, originalPos, yaw);
#if CLIENT
        if (!VanillaPermissions.SpawnsEdit(spawnType).Has())
            return SpawnpointEventResult.NoPermissions;

        MoveSpawnFinalProperties properties = new MoveSpawnFinalProperties(netId, t, spawnType, false, CachedTime.DeltaTime);

        bool shouldAllow = true;
        ClientEvents.InvokeOnMoveSpawnFinalRequested(in properties, ref shouldAllow);

        if (!shouldAllow)
            return SpawnpointEventResult.CancelledByEvent;

        MoveSpawnpointLocal(spawnType, ref region, position);

        ClientEvents.InvokeOnMoveSpawnFinal(in properties);
        if (!ClientEvents.ListeningOnMoveSpawnsFinal)
            return SpawnpointEventResult.Success;

        NetId64.OneArray[0] = netId;
        TransformationDelta.OneArray[0] = t;
        ClientEvents.InvokeOnMoveSpawnsFinal(new MoveSpawnsFinalProperties(NetId64.OneArray, TransformationDelta.OneArray, spawnType, false, CachedTime.DeltaTime));
#elif SERVER
        MoveSpawnpointLocal(spawnType, ref region, position);
        EditorActions.QueueServerAction(new MoveSpawnsFinalAction
        {
            DeltaTime = CachedTime.DeltaTime,
            SpawnType = spawnType,
            NetIds = [ netId ],
            Transformations = [ t ]
        });
#endif

        return SpawnpointEventResult.Success;
    }

    /// <summary>
    /// Locally changes the yaw (y-axis) angle of the spawnpoint to <paramref name="yaw"/>. Calls local events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <returns>The status of the operation.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Spawn type must be 'Vehicle' or 'Player'.</exception>
    /// <exception cref="MissingMemberException">Reflection failure.</exception>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static SpawnpointEventResult RotateSpawnpointLocal(SpawnType spawnType, int index, float yaw)
    {
        ThreadUtil.assertIsGameThread();

        if (spawnType is not SpawnType.Player and not SpawnType.Vehicle)
            throw new ArgumentOutOfRangeException(nameof(spawnType), "Spawn type must be 'Vehicle', or 'Player'.");

        if (!CheckSpawnpointSafe(spawnType, index))
            return SpawnpointEventResult.IndexOutOfRange;

        float oldYaw;
        if (spawnType == SpawnType.Vehicle)
        {
            VehicleSpawnpoint vehicleSpawnpoint = LevelVehicles.spawns[index];
            oldYaw = vehicleSpawnpoint.angle;
            vehicleSpawnpoint.SetYaw(yaw);
            if (vehicleSpawnpoint.node != null)
            {
                Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
                if (!vehicleSpawnpoint.node.transform.rotation.IsNearlyEqual(rotation))
                    vehicleSpawnpoint.node.transform.rotation = rotation;
#if CLIENT
                if (!vehicleSpawnpoint.node.TryGetComponent(out VehicleSpawnpointNode spawnpoint))
                    spawnpoint = vehicleSpawnpoint.node.gameObject.AddComponent<VehicleSpawnpointNode>();

                spawnpoint.Spawnpoint = vehicleSpawnpoint;
#endif
            }

            EventOnVehicleSpawnpointMoved.TryInvoke(vehicleSpawnpoint, index, vehicleSpawnpoint.point, vehicleSpawnpoint.point, oldYaw, yaw);
        }
        else // Player
        {
            PlayerSpawnpoint playerSpawnpoint = LevelPlayers.spawns[index];
            oldYaw = playerSpawnpoint.angle;
            playerSpawnpoint.SetYaw(yaw);
            if (playerSpawnpoint.node != null)
            {
                Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
                if (!playerSpawnpoint.node.transform.rotation.IsNearlyEqual(rotation))
                    playerSpawnpoint.node.transform.rotation = rotation;
#if CLIENT
                if (!playerSpawnpoint.node.TryGetComponent(out PlayerSpawnpointNode spawnpoint))
                    spawnpoint = playerSpawnpoint.node.gameObject.AddComponent<PlayerSpawnpointNode>();

                spawnpoint.Spawnpoint = playerSpawnpoint;
#endif
            }

            EventOnPlayerSpawnpointMoved.TryInvoke(playerSpawnpoint, index, playerSpawnpoint.point, playerSpawnpoint.point, oldYaw, yaw);
        }

        Logger.DevkitServer.LogDebug(nameof(RotateSpawnpointLocal), $"{spawnType.GetPropercaseText()} spawnpoint rotated: {oldYaw.Format("F0")}° -> {yaw.Format("F0")}°.");
        return SpawnpointEventResult.Success;
    }

    /// <summary>
    /// Locally changes the yaw (y-axis) angle of the spawnpoint to <paramref name="yaw"/>. Calls local events.
    /// </summary>
    /// <remarks>Replicates to clients.</remarks>
    /// <returns>The status of the operation.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Spawn type must be 'Vehicle' or 'Player'.</exception>
    /// <exception cref="MissingMemberException">Reflection failure.</exception>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static SpawnpointEventResult RotateSpawnpoint(SpawnType spawnType, int index, float yaw)
    {
        ThreadUtil.assertIsGameThread();

        if (!DevkitServerModule.IsEditing)
            return RotateSpawnpointLocal(spawnType, index, yaw);

        if (spawnType is not SpawnType.Player and not SpawnType.Vehicle)
            throw new ArgumentOutOfRangeException(nameof(spawnType), "Spawn type must be 'Vehicle', or 'Player'.");

        if (!CheckSpawnpointSafe(spawnType, index))
            return SpawnpointEventResult.IndexOutOfRange;

        if (!SpawnsNetIdDatabase.TryGetSpawnNetId(spawnType, index, out NetId64 netId))
        {
            Logger.DevkitServer.LogWarning(nameof(RotateSpawnpoint), $"Unable to find NetId of {spawnType.GetLowercaseText()} spawn at {index.Format()}.");
            return SpawnpointEventResult.NetIdNotFound;
        }

        float oldYaw = spawnType switch
        {
            SpawnType.Player => LevelPlayers.spawns[index].angle,
            _ => LevelVehicles.spawns[index].angle
        };
        Vector3 pos = spawnType switch
        {
            SpawnType.Player => LevelPlayers.spawns[index].point,
            _ => LevelVehicles.spawns[index].point
        };

        TransformationDelta t = new TransformationDelta(TransformationDelta.TransformFlags.Rotation, pos, oldYaw, pos, yaw);
#if CLIENT
        if (!VanillaPermissions.SpawnsEdit(spawnType).Has())
            return SpawnpointEventResult.NoPermissions;

        MoveSpawnFinalProperties properties = new MoveSpawnFinalProperties(netId, t, spawnType, false, CachedTime.DeltaTime);

        bool shouldAllow = true;
        ClientEvents.InvokeOnMoveSpawnFinalRequested(in properties, ref shouldAllow);

        if (!shouldAllow)
            return SpawnpointEventResult.CancelledByEvent;

        RotateSpawnpointLocal(spawnType, index, yaw);

        ClientEvents.InvokeOnMoveSpawnFinal(in properties);
        if (!ClientEvents.ListeningOnMoveSpawnsFinal)
            return SpawnpointEventResult.Success;

        NetId64.OneArray[0] = netId;
        TransformationDelta.OneArray[0] = t;
        ClientEvents.InvokeOnMoveSpawnsFinal(new MoveSpawnsFinalProperties(NetId64.OneArray, TransformationDelta.OneArray, spawnType, false, CachedTime.DeltaTime));
#elif SERVER
        RotateSpawnpointLocal(spawnType, index, yaw);
        EditorActions.QueueServerAction(new MoveSpawnsFinalAction
        {
            DeltaTime = CachedTime.DeltaTime,
            SpawnType = spawnType,
            NetIds = [ netId ],
            Transformations = [ t ]
        });
#endif

        return SpawnpointEventResult.Success;
    }

    /// <summary>
    /// Locally changes the position and yaw (y-axis) angle of the spawnpoint to <paramref name="position"/> and <paramref name="yaw"/>. Calls local events.
    /// </summary>
    /// <returns>The status of the operation.</returns>
    /// <remarks>Non-replicating. Yaw is ignored for <see cref="AnimalSpawnpoint"/>.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">Spawn type must be 'Animal', 'Vehicle', or 'Player'.</exception>
    /// <exception cref="MissingMemberException">Reflection failure.</exception>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static SpawnpointEventResult TransformSpawnpointLocal(SpawnType spawnType, int index, Vector3 position, float yaw)
    {
        ThreadUtil.assertIsGameThread();

        if (spawnType is not SpawnType.Animal and not SpawnType.Player and not SpawnType.Vehicle)
            throw new ArgumentOutOfRangeException(nameof(spawnType), "Spawn type must be 'Animal', 'Vehicle', or 'Player'.");

        if (spawnType == SpawnType.Animal)
            return MoveSpawnpointLocal(spawnType, index, position);

        if (!CheckSpawnpointSafe(spawnType, index))
            return SpawnpointEventResult.IndexOutOfRange;

        float oldYaw;
        Vector3 oldPosition;
        if (spawnType == SpawnType.Vehicle)
        {
            VehicleSpawnpoint vehicleSpawnpoint = LevelVehicles.spawns[index];
            oldYaw = vehicleSpawnpoint.angle;
            oldPosition = vehicleSpawnpoint.point;
            vehicleSpawnpoint.SetYaw(yaw);
            vehicleSpawnpoint.SetPoint(position);
            if (vehicleSpawnpoint.node != null)
            {
                Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
                bool rot = vehicleSpawnpoint.node.transform.rotation != rotation;
                bool pos = vehicleSpawnpoint.node.transform.position != position;

                if (rot && pos)
                    vehicleSpawnpoint.node.transform.SetPositionAndRotation(position, rotation);
                else if (pos)
                    vehicleSpawnpoint.node.transform.position = position;
                else if (rot)
                    vehicleSpawnpoint.node.transform.rotation = rotation;
#if CLIENT
                if (!vehicleSpawnpoint.node.TryGetComponent(out VehicleSpawnpointNode spawnpoint))
                    spawnpoint = vehicleSpawnpoint.node.gameObject.AddComponent<VehicleSpawnpointNode>();

                spawnpoint.Spawnpoint = vehicleSpawnpoint;
#endif
            }

            EventOnVehicleSpawnpointMoved.TryInvoke(vehicleSpawnpoint, index, vehicleSpawnpoint.point, vehicleSpawnpoint.point, oldYaw, yaw);
        }
        else
        {
            PlayerSpawnpoint playerSpawnpoint = LevelPlayers.spawns[index];
            oldYaw = playerSpawnpoint.angle;
            oldPosition = playerSpawnpoint.point;
            playerSpawnpoint.SetYaw(yaw);
            playerSpawnpoint.SetPoint(position);
            if (playerSpawnpoint.node != null)
            {
                Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
                bool rot = playerSpawnpoint.node.transform.rotation != rotation;
                bool pos = playerSpawnpoint.node.transform.position != position;

                if (rot && pos)
                    playerSpawnpoint.node.transform.SetPositionAndRotation(position, rotation);
                else if (pos)
                    playerSpawnpoint.node.transform.position = position;
                else if (rot)
                    playerSpawnpoint.node.transform.rotation = rotation;
#if CLIENT
                if (!playerSpawnpoint.node.TryGetComponent(out PlayerSpawnpointNode spawnpoint))
                    spawnpoint = playerSpawnpoint.node.gameObject.AddComponent<PlayerSpawnpointNode>();

                spawnpoint.Spawnpoint = playerSpawnpoint;
#endif
            }

            EventOnPlayerSpawnpointMoved.TryInvoke(playerSpawnpoint, index, playerSpawnpoint.point, playerSpawnpoint.point, oldYaw, yaw);
        }

        Logger.DevkitServer.LogDebug(nameof(TransformSpawnpointLocal), $"{spawnType.GetPropercaseText()} spawnpoint transformed: {oldPosition.Format("F0")} -> {position.Format("F0")}, {oldYaw.Format("F0")}° -> {yaw.Format("F0")}°.");
        return SpawnpointEventResult.Success;
    }

    /// <summary>
    /// Changes the position and yaw (y-axis) angle of the spawnpoint to <paramref name="position"/> and <paramref name="yaw"/>. Calls local events.
    /// </summary>
    /// <returns>The status of the operation.</returns>
    /// <remarks>Replicates to clients. Yaw is ignored for <see cref="AnimalSpawnpoint"/>.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">Spawn type must be 'Animal', 'Vehicle', or 'Player'.</exception>
    /// <exception cref="MissingMemberException">Reflection failure.</exception>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static SpawnpointEventResult TransformSpawnpoint(SpawnType spawnType, int index, Vector3 position, float yaw)
    {
        ThreadUtil.assertIsGameThread();

        if (spawnType == SpawnType.Animal)
            return MoveSpawnpoint(spawnType, index, position);

        if (!DevkitServerModule.IsEditing)
            return TransformSpawnpointLocal(spawnType, index, position, yaw);

        if (spawnType is not SpawnType.Player and not SpawnType.Vehicle)
            throw new ArgumentOutOfRangeException(nameof(spawnType), "Spawn type must be 'Animal', 'Vehicle', or 'Player'.");

        if (!CheckSpawnpointSafe(spawnType, index))
            return SpawnpointEventResult.IndexOutOfRange;

        if (!SpawnsNetIdDatabase.TryGetSpawnNetId(spawnType, index, out NetId64 netId))
        {
            Logger.DevkitServer.LogWarning(nameof(TransformSpawnpoint), $"Unable to find NetId of {spawnType.GetLowercaseText()} spawn at {index.Format()}.");
            return SpawnpointEventResult.NetIdNotFound;
        }

        float oldYaw = spawnType switch
        {
            SpawnType.Player => LevelPlayers.spawns[index].angle,
            _ => LevelVehicles.spawns[index].angle
        };
        Vector3 pos = spawnType switch
        {
            SpawnType.Player => LevelPlayers.spawns[index].point,
            _ => LevelVehicles.spawns[index].point
        };

        TransformationDelta t = new TransformationDelta(TransformationDelta.TransformFlags.AllNew, pos, oldYaw, pos, yaw);
#if CLIENT
        if (!VanillaPermissions.SpawnsEdit(spawnType).Has())
            return SpawnpointEventResult.NoPermissions;

        MoveSpawnFinalProperties properties = new MoveSpawnFinalProperties(netId, t, spawnType, false, CachedTime.DeltaTime);

        bool shouldAllow = true;
        ClientEvents.InvokeOnMoveSpawnFinalRequested(in properties, ref shouldAllow);

        if (!shouldAllow)
            return SpawnpointEventResult.CancelledByEvent;

        TransformSpawnpointLocal(spawnType, index, position, yaw);

        ClientEvents.InvokeOnMoveSpawnFinal(in properties);
        if (!ClientEvents.ListeningOnMoveSpawnsFinal)
            return SpawnpointEventResult.Success;

        NetId64.OneArray[0] = netId;
        TransformationDelta.OneArray[0] = t;
        ClientEvents.InvokeOnMoveSpawnsFinal(new MoveSpawnsFinalProperties(NetId64.OneArray, TransformationDelta.OneArray, spawnType, false, CachedTime.DeltaTime));
#elif SERVER
        TransformSpawnpointLocal(spawnType, index, position, yaw);
        EditorActions.QueueServerAction(new MoveSpawnsFinalAction
        {
            DeltaTime = CachedTime.DeltaTime,
            SpawnType = spawnType,
            NetIds = [netId],
            Transformations = [t]
        });
#endif

        return SpawnpointEventResult.Success;
    }

    /// <summary>
    /// Locally adds an animal spawnpoint at the given position with the given spawn table.
    /// </summary>
    /// <remarks>Does not replicate.</remarks>
    /// <returns>Index of the created spawnpoint.</returns>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="spawnTable"/> does not reference a valid spawn table.</exception>
    /// <exception cref="InvalidOperationException">Too many spawns in the world (65535).</exception>
    public static int AddAnimalSpawnpointLocal(int spawnTable, Vector3 position)
    {
        return AddSpawnpointLocal(SpawnType.Animal, spawnTable, position, 0f);
    }

    /// <summary>
    /// Locally adds a vehicle spawnpoint at the given position and rotation with the given spawn table.
    /// </summary>
    /// <remarks>Does not replicate.</remarks>
    /// <returns>Index of the created spawnpoint.</returns>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="spawnTable"/> does not reference a valid spawn table.</exception>
    /// <exception cref="InvalidOperationException">Too many spawns in the world (65535).</exception>
    public static int AddVehicleSpawnpointLocal(int spawnTable, Vector3 position, float yaw = 0f)
    {
        return AddSpawnpointLocal(SpawnType.Vehicle, spawnTable, position, yaw);
    }

    /// <summary>
    /// Locally adds a player spawnpoint at the given position and rotation with the given spawn table.
    /// </summary>
    /// <remarks>Does not replicate.</remarks>
    /// <returns>Index of the created spawnpoint.</returns>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="spawnTable"/> does not reference a valid spawn table.</exception>
    /// <exception cref="InvalidOperationException">Too many spawns in the world (255).</exception>
    public static int AddPlayerSpawnpointLocal(Vector3 position, float yaw = 0f, bool isAlt = false)
    {
        return AddSpawnpointLocal(SpawnType.Player, byte.MaxValue, position, yaw, isAlt ? (byte)1 : (byte)0);
    }

    /// <summary>
    /// Locally adds an item spawnpoint at the given position with the given spawn table.
    /// </summary>
    /// <remarks>Does not replicate.</remarks>
    /// <returns>Region identifier of the created spawnpoint.</returns>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="spawnTable"/> does not reference a valid spawn table.</exception>
    /// <exception cref="InvalidOperationException">Too many spawns in that region (65535). - OR - The given poisition is not in a region.</exception>
    public static RegionIdentifier AddItemSpawnpointLocal(int spawnTable, Vector3 position)
    {
        return RegionIdentifier.CreateUnsafe(AddSpawnpointLocal(SpawnType.Item, spawnTable, position, 0f));
    }

    /// <summary>
    /// Locally adds a zombie spawnpoint at the given position with the given spawn table.
    /// </summary>
    /// <remarks>Does not replicate.</remarks>
    /// <returns>Region identifier of the created spawnpoint.</returns>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="spawnTable"/> does not reference a valid spawn table.</exception>
    /// <exception cref="InvalidOperationException">Too many spawns in that region (65535). - OR - The given poisition is not in a region.</exception>
    public static RegionIdentifier AddZombieSpawnpointLocal(int spawnTable, Vector3 position)
    {
        return RegionIdentifier.CreateUnsafe(AddSpawnpointLocal(SpawnType.Item, spawnTable, position, 0f));
    }

    internal static int AddSpawnpointLocal(SpawnType spawnType, int spawnTable, Vector3 position, float yaw, byte instantiationFlags = 0)
    {
        ThreadUtil.assertIsGameThread();

        if (spawnType is not SpawnType.Animal and not SpawnType.Player and not SpawnType.Vehicle and not SpawnType.Item and not SpawnType.Zombie)
            throw new ArgumentOutOfRangeException(nameof(spawnType), "Spawn type must be one of the following: Animal, Vehicle, Item, or Zombie.");

        if (!SpawnTableUtil.CheckSpawnTableSafe(spawnType, spawnTable))
            throw new ArgumentOutOfRangeException(nameof(spawnTable), $"Index {spawnTable} does not point to an existing {spawnType.GetLowercaseText()} table.");

        switch (spawnType)
        {
            case SpawnType.Animal:
                AnimalSpawnpoint animalSpawnpoint = new AnimalSpawnpoint((byte)spawnTable, position);
                return AddAnimalSpawnpointLocal(animalSpawnpoint);
            case SpawnType.Player:
                PlayerSpawnpoint playerSpawnpoint = new PlayerSpawnpoint(position, yaw, (instantiationFlags & 1) != 0);
                return AddPlayerSpawnpointLocal(playerSpawnpoint);
            case SpawnType.Vehicle:
                VehicleSpawnpoint vehicleSpawnpoint = new VehicleSpawnpoint((byte)spawnTable, position, yaw);
                return AddVehicleSpawnpointLocal(vehicleSpawnpoint);
            case SpawnType.Item:
                ItemSpawnpoint itemSpawnpoint = new ItemSpawnpoint((byte)spawnTable, position);
                return AddItemSpawnpointLocal(itemSpawnpoint).Raw;
            default: // Zombie
                ZombieSpawnpoint zombieSpawnpoint = new ZombieSpawnpoint((byte)spawnTable, position);
                return AddZombieSpawnpointLocal(zombieSpawnpoint).Raw;
        }
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
    internal static bool SanityCheckIndex(AnimalSpawnpoint spawn, ref int index)
    {
        if (index >= 0 && index < LevelAnimals.spawns.Count && ReferenceEquals(spawn, LevelAnimals.spawns[index]))
            return true;
        
        List<AnimalSpawnpoint> list = LevelAnimals.spawns;
        int ct = Math.Min(ushort.MaxValue, list.Count);
        for (int i = 0; i < ct; ++i)
        {
            if (!ReferenceEquals(spawn, list[i]))
                continue;

            index = i;
            return true;
        }

        return false;
    }
    internal static bool SanityCheckIndex(PlayerSpawnpoint spawn, ref int index)
    {
        if (index >= 0 && index < LevelPlayers.spawns.Count && ReferenceEquals(spawn, LevelPlayers.spawns[index]))
            return true;
        
        List<PlayerSpawnpoint> list = LevelPlayers.spawns;
        int ct = Math.Min(byte.MaxValue, list.Count);
        for (int i = 0; i < ct; ++i)
        {
            if (!ReferenceEquals(spawn, list[i]))
                continue;

            index = i;
            return true;
        }

        return false;
    }
    internal static bool SanityCheckIndex(VehicleSpawnpoint spawn, ref int index)
    {
        if (index >= 0 && index < LevelVehicles.spawns.Count && ReferenceEquals(spawn, LevelVehicles.spawns[index]))
            return true;

        List<VehicleSpawnpoint> list = LevelVehicles.spawns;
        int ct = Math.Min(ushort.MaxValue, list.Count);
        for (int i = 0; i < ct; ++i)
        {
            if (!ReferenceEquals(spawn, list[i]))
                continue;

            index = i;
            return true;
        }

        return false;
    }
    internal static bool SanityCheckRegion(ZombieSpawnpoint spawn, ref RegionIdentifier region)
    {
        if (region.TryFromList(LevelZombies.spawns, out ZombieSpawnpoint match) && ReferenceEquals(spawn, match))
            return true;
        
        foreach (RegionCoord regionCoord in RegionUtil.EnumerateRegions(spawn.point))
        {
            List<ZombieSpawnpoint> regionList = LevelZombies.spawns[regionCoord.x, regionCoord.y];
            int ct = Math.Min(ushort.MaxValue, regionList.Count);
            for (int i = 0; i < ct; ++i)
            {
                if (!ReferenceEquals(spawn, regionList[i]))
                    continue;

                region = new RegionIdentifier(regionCoord, i);
                return true;
            }
        }

        return false;
    }
    internal static bool SanityCheckRegion(ItemSpawnpoint spawn, ref RegionIdentifier region)
    {
        if (region.TryFromList(LevelItems.spawns, out ItemSpawnpoint match) && ReferenceEquals(spawn, match))
            return true;
        
        foreach (RegionCoord regionCoord in RegionUtil.EnumerateRegions(spawn.point))
        {
            List<ItemSpawnpoint> regionList = LevelItems.spawns[regionCoord.x, regionCoord.y];
            int ct = Math.Min(ushort.MaxValue, regionList.Count);
            for (int i = 0; i < ct; ++i)
            {
                if (!ReferenceEquals(spawn, regionList[i]))
                    continue;

                region = new RegionIdentifier(regionCoord, i);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks to make sure <paramref name="index"/> is in range of the corresponding internal spawn list.
    /// </summary>
    /// <param name="useUnsafeComparison">Converts <paramref name="index"/> to a <see cref="RegionIdentifier"/> when <paramref name="spawnType"/> is a region spawn.</param>
    public static bool CheckSpawnpointSafe(SpawnType spawnType, int index, bool useUnsafeComparison = false)
    {
        return spawnType switch
        {
            SpawnType.Animal => index >= 0 && LevelAnimals.spawns.Count > index,
            SpawnType.Vehicle => index >= 0 && LevelVehicles.spawns.Count > index,
            SpawnType.Player => index >= 0 && LevelPlayers.spawns.Count > index,
            SpawnType.Item or SpawnType.Zombie => useUnsafeComparison && CheckSpawnpointSafe(spawnType, RegionIdentifier.CreateUnsafe(index), false),
            _ => false
        };
    }

    /// <summary>
    /// Checks to make sure <paramref name="region"/> is in range of the corresponding internal spawn region lists.
    /// </summary>
    /// <param name="useUnsafeComparison">Converts <paramref name="region"/> to an <see cref="int"/> when <paramref name="spawnType"/> is an index spawn.</param>
    public static bool CheckSpawnpointSafe(SpawnType spawnType, RegionIdentifier region, bool useUnsafeComparison = false)
    {
        return spawnType switch
        {
            SpawnType.Zombie => region.CheckSafe() && LevelZombies.spawns[region.X, region.Y].Count > region.Index,
            SpawnType.Item => region.CheckSafe() && LevelZombies.spawns[region.X, region.Y].Count > region.Index,
            SpawnType.Animal or SpawnType.Vehicle or SpawnType.Player => useUnsafeComparison && CheckSpawnpointSafe(spawnType, region.Raw, false),
            _ => false
        };
    }

    // avoid ToString calls for no reason, used in logs a lot
    internal static string GetLowercaseText(this SpawnType spawnType) => spawnType switch
    {
        SpawnType.Animal => "animal",
        SpawnType.Player => "player",
        SpawnType.Vehicle => "vehicle",
        SpawnType.Item => "item",
        SpawnType.Zombie => "zombie",
        _ => spawnType.ToString().ToLowerInvariant()
    };
    // avoid ToString calls for no reason, used in logs a lot
    internal static string GetPropercaseText(this SpawnType spawnType) => spawnType switch
    {
        SpawnType.Animal => nameof(SpawnType.Animal),
        SpawnType.Player => nameof(SpawnType.Player),
        SpawnType.Vehicle => nameof(SpawnType.Vehicle),
        SpawnType.Item => nameof(SpawnType.Item),
        SpawnType.Zombie => nameof(SpawnType.Zombie),
        _ => spawnType.ToString().ToLowerInvariant()
    };
    internal static int GetSpawnCountUnsafe(SpawnType spawnType) => spawnType switch
    {
        SpawnType.Animal => LevelAnimals.spawns.Count,
        SpawnType.Player => LevelPlayers.spawns.Count,
        _ => LevelVehicles.spawns.Count
    };

    internal static int GetSpawnCountUnsafe(SpawnType spawnType, int regionX, int regionY)
    {
        if (!Regions.checkSafe(regionX, regionY))
            return 0;

        return spawnType switch
        {
            SpawnType.Item => LevelItems.spawns[regionX, regionY].Count,
            _ => LevelZombies.spawns[regionX, regionY].Count
        };
    }

#if SERVER

    /// <summary>
    /// Adds an animal spawnpoint at the given position with the given spawn table.
    /// </summary>
    /// <remarks>Replicates to clients.</remarks>
    /// <returns>Index of the created spawnpoint.</returns>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="spawnTable"/> does not reference a valid spawn table.</exception>
    /// <exception cref="InvalidOperationException">Too many spawns in the world (65535).</exception>
    public static int AddAnimalSpawnpoint(int spawnTable, Vector3 position, EditorUser? owner = null)
    {
        ThreadUtil.assertIsGameThread();

        return AddSpawnpoint(SpawnType.Animal, spawnTable, position, 0f, owner == null ? MessageContext.Nil : MessageContext.CreateFromCaller(owner));
    }

    /// <summary>
    /// Adds a vehicle spawnpoint at the given position and rotation with the given spawn table.
    /// </summary>
    /// <remarks>Replicates to clients.</remarks>
    /// <returns>Index of the created spawnpoint.</returns>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="spawnTable"/> does not reference a valid spawn table.</exception>
    /// <exception cref="InvalidOperationException">Too many spawns in the world (65535).</exception>
    public static int AddVehicleSpawnpoint(int spawnTable, Vector3 position, float yaw = 0f, EditorUser? owner = null)
    {
        ThreadUtil.assertIsGameThread();

        return AddSpawnpoint(SpawnType.Vehicle, spawnTable, position, yaw, owner == null ? MessageContext.Nil : MessageContext.CreateFromCaller(owner));
    }

    /// <summary>
    /// Adds a player spawnpoint at the given position and rotation with the given spawn table.
    /// </summary>
    /// <remarks>Replicates to clients.</remarks>
    /// <returns>Index of the created spawnpoint.</returns>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="spawnTable"/> does not reference a valid spawn table.</exception>
    /// <exception cref="InvalidOperationException">Too many spawns in the world (255).</exception>
    public static int AddPlayerSpawnpoint(Vector3 position, float yaw = 0f, bool isAlt = false, EditorUser? owner = null)
    {
        ThreadUtil.assertIsGameThread();

        return AddSpawnpoint(SpawnType.Player, byte.MaxValue, position, yaw, owner == null ? MessageContext.Nil : MessageContext.CreateFromCaller(owner), isAlt ? (byte)1 : (byte)0);
    }

    /// <summary>
    /// Adds an item spawnpoint at the given position with the given spawn table.
    /// </summary>
    /// <remarks>Replicates to clients.</remarks>
    /// <returns>Region identifier of the created spawnpoint.</returns>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="spawnTable"/> does not reference a valid spawn table.</exception>
    /// <exception cref="InvalidOperationException">Too many spawns in that region (65535). - OR - The given poisition is not in a region.</exception>
    public static RegionIdentifier AddItemSpawnpoint(int spawnTable, Vector3 position, EditorUser? owner = null)
    {
        ThreadUtil.assertIsGameThread();

        return RegionIdentifier.CreateUnsafe(AddSpawnpoint(SpawnType.Item, spawnTable, position, 0f, owner == null ? MessageContext.Nil : MessageContext.CreateFromCaller(owner)));
    }

    /// <summary>
    /// Adds a zombie spawnpoint at the given position with the given spawn table.
    /// </summary>
    /// <remarks>Replicates to clients.</remarks>
    /// <returns>Region identifier of the created spawnpoint.</returns>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="spawnTable"/> does not reference a valid spawn table.</exception>
    /// <exception cref="InvalidOperationException">Too many spawns in that region (65535). - OR - The given poisition is not in a region.</exception>
    public static RegionIdentifier AddZombieSpawnpoint(int spawnTable, Vector3 position, EditorUser? owner = null)
    {
        ThreadUtil.assertIsGameThread();

        return RegionIdentifier.CreateUnsafe(AddSpawnpoint(SpawnType.Item, spawnTable, position, 0f, owner == null ? MessageContext.Nil : MessageContext.CreateFromCaller(owner)));
    }
    internal static int AddSpawnpoint(SpawnType spawnType, int spawnTable, Vector3 position, float yaw, MessageContext ctx, byte instantiateFlags = 0)
    {
        DevkitServerModule.AssertIsDevkitServerClient();

        ulong owner = ctx.GetCaller() is { } user ? user.SteamId.m_SteamID : 0ul;

        int index = AddSpawnpointLocal(spawnType, spawnTable, position, yaw, instantiateFlags);

        if (index == -1)
            return -1;

        NetId64 spawnNetId;

        if (spawnType is SpawnType.Player or SpawnType.Animal or SpawnType.Vehicle)
            SpawnsNetIdDatabase.TryGetSpawnNetId(spawnType, index, out spawnNetId);
        else
            SpawnsNetIdDatabase.TryGetSpawnNetId(spawnType, RegionIdentifier.CreateUnsafe(index), out spawnNetId);

        PooledTransportConnectionList list = ctx.IsRequest ? DevkitServerUtility.GetAllConnections(ctx.Connection) : DevkitServerUtility.GetAllConnections();

        byte tableIndex;
        switch (spawnType)
        {
            case SpawnType.Animal:
                AnimalSpawnpoint animalSpawnpoint = LevelAnimals.spawns[index];
                position = animalSpawnpoint.point;
                yaw = 0f;
                tableIndex = animalSpawnpoint.type;
                instantiateFlags = 0;
                break;

            case SpawnType.Player:
                PlayerSpawnpoint playerSpawnpoint = LevelPlayers.spawns[index];
                position = playerSpawnpoint.point;
                yaw = playerSpawnpoint.angle;
                tableIndex = byte.MaxValue;
                instantiateFlags = playerSpawnpoint.isAlt ? (byte)1 : (byte)0;
                break;

            case SpawnType.Vehicle:
                VehicleSpawnpoint vehicleSpawnpoint = LevelVehicles.spawns[index];
                position = vehicleSpawnpoint.point;
                yaw = vehicleSpawnpoint.angle;
                tableIndex = vehicleSpawnpoint.type;
                instantiateFlags = 0;
                break;

            case SpawnType.Item:
                ItemSpawnpoint itemSpawnpoint = RegionIdentifier.CreateUnsafe(index).FromList(LevelItems.spawns);
                position = itemSpawnpoint.point;
                yaw = 0f;
                tableIndex = itemSpawnpoint.type;
                instantiateFlags = 0;
                break;

            default: // Zombie
                ZombieSpawnpoint zombieSpawnpoint = RegionIdentifier.CreateUnsafe(index).FromList(LevelZombies.spawns);
                position = zombieSpawnpoint.point;
                yaw = 0f;
                tableIndex = zombieSpawnpoint.type;
                instantiateFlags = 0;
                break;
        }

        SpawnsNetIdDatabase.TryGetSpawnTableNetId(spawnType, tableIndex, out NetId64 spawnTableNetId);

        if (ctx.IsRequest)
            ctx.ReplyLayered(SendSpawnInstantiation, (byte)spawnType, spawnNetId, spawnTableNetId, position, yaw, instantiateFlags, owner);

        SendSpawnInstantiation.Invoke(list, (byte)spawnType, spawnNetId, spawnTableNetId, position, yaw, instantiateFlags, owner);

        // todo SyncIfAuthority(index);

        return index;
    }

    [NetCall(NetCallSource.FromClient, DevkitServerNetCall.RequestSpawnInstantiation)]
    private static void ReceiveInstantiateSpawnRequest(MessageContext ctx, byte spawnTypePacked, NetId64 spawnTableNetId, Vector3 position, float yaw, byte instantiationFlags)
    {
        SpawnType spawnType = (SpawnType)spawnTypePacked;

        EditorUser? user = ctx.GetCaller();
        if (user == null || !user.IsOnline)
        {
            Logger.DevkitServer.LogError(nameof(ReceiveInstantiateSpawnRequest), $"Unable to get user from {spawnType.GetLowercaseText()} spawnpoint instantiation request.");
            ctx.Acknowledge(StandardErrorCode.NoPermissions);
            return;
        }

        if (!VanillaPermissions.SpawnsAdd(spawnType).Has(user))
        {
            ctx.Acknowledge(StandardErrorCode.NoPermissions);
            EditorMessage.SendNoPermissionMessage(user, VanillaPermissions.SpawnsAdd(spawnType));
            return;
        }

        if (spawnType is not SpawnType.Animal and not SpawnType.Player and not SpawnType.Vehicle and not SpawnType.Item and not SpawnType.Zombie)
        {
            Logger.DevkitServer.LogError(nameof(ReceiveInstantiateSpawnRequest), $"Invalid spawn type in {user.SteamId.Format()} spawnpoint instantiation request.");
            ctx.Acknowledge(StandardErrorCode.InvalidData);
            return;
        }

        int spawnTableIndex = -1;
        if (!spawnTableNetId.IsNull() && !SpawnsNetIdDatabase.TryGetSpawnTable(spawnType, spawnTableNetId, out spawnTableIndex))
        {
            spawnTableIndex = -1;
            Logger.DevkitServer.LogWarning(nameof(ReceiveInstantiateSpawnRequest), $"Expected {spawnType.GetLowercaseText()} table with " +
                $"net ID: {spawnTableNetId.Format()}, didn't find one. Defaulting to first available table.");
        }

        if (spawnTableIndex is <= 0 or > byte.MaxValue)
            spawnTableIndex = 0;

        int result = AddSpawnpoint(spawnType, spawnTableIndex, position, yaw, ctx, instantiationFlags);

        Logger.DevkitServer.LogDebug(nameof(ReceiveInstantiateSpawnRequest), $"Granted request for instantiation of {spawnType.GetLowercaseText()} " +
                                                                             $"spawnpoint {(spawnType is SpawnType.Item or SpawnType.Zombie
                                                                                 ? RegionIdentifier.CreateUnsafe(result).Format()
                                                                                 : ("#" + result.Format()))} from {user.SteamId.Format()}.");
    }
#elif CLIENT

    [NetCall(NetCallSource.FromServer, DevkitServerNetCall.SendSpawnInstantiation)]
    internal static StandardErrorCode ReceiveSpawnInstantiation(MessageContext ctx, byte spawnTypePacked, NetId64 netId, NetId64 spawnTableNetId, Vector3 position, float yaw, byte instantiationFlags, ulong owner)
    {
        SpawnType spawnType = (SpawnType)spawnTypePacked;

        if (!EditorActions.HasProcessedPendingSpawns)
        {
            EditorActions.TemporaryEditorActions?.QueueSpawnInstantiation(netId, spawnTableNetId, spawnType, position, yaw, instantiationFlags, owner);
            return StandardErrorCode.Success;
        }

        try
        {
            int spawnTableIndex = -1;
            if (!spawnTableNetId.IsNull() && !SpawnsNetIdDatabase.TryGetSpawnTable(spawnType, spawnTableNetId, out spawnTableIndex))
            {
                spawnTableIndex = -1;
                Logger.DevkitServer.LogWarning(nameof(ReceiveSpawnInstantiation), $"Expected {spawnType.GetLowercaseText()} table with " +
                    $"net ID: {spawnTableNetId.Format()}, didn't find one. Defaulting to first available table.");
            }

            if (spawnTableIndex is <= 0 or > byte.MaxValue)
                spawnTableIndex = 0;

            int index = AddSpawnpointLocal(spawnType, spawnTableIndex, position, yaw, instantiationFlags);

            if (spawnType is SpawnType.Item or SpawnType.Zombie)
                SpawnsNetIdDatabase.RegisterRegionSpawnpoint(spawnType, RegionIdentifier.CreateUnsafe(index), netId);
            else
                SpawnsNetIdDatabase.RegisterIndexSpawnpoint(spawnType, index, netId);

            Logger.DevkitServer.LogDebug(nameof(ReceiveSpawnInstantiation), $"Assigned {spawnType.GetLowercaseText()} spawnpoint NetId: {netId.Format()}.");

            if (owner == Provider.client.m_SteamID && SpawnTableUtil.IsEditingSpawns(spawnType) && UserInput.ActiveTool is DevkitServerSpawnsTool spawnsTool)
            {
                if (spawnType is SpawnType.Item or SpawnType.Zombie)
                    spawnsTool.Select(RegionIdentifier.CreateUnsafe(index), true);
                else
                    spawnsTool.Select(index, true);
            }

            // todo SyncIfAuthority(spawnType, index);
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError(nameof(ReceiveSpawnInstantiation), ex, $"Failed to initialize {spawnType.GetLowercaseText()} spawnpoint: {netId.Format()} ({position.Format("0.##")} @ {yaw.Format()}°).");
            return StandardErrorCode.GenericError;
        }

        return StandardErrorCode.Success;
    }

    /// <summary>
    /// Sends a request to the server to add a spawnpoint with the specified info.
    /// </summary>
    /// <remarks>Thread-safe.</remarks>
    /// <exception cref="InvalidOperationException">Not connected to a DevkitServer server.</exception>
    public static UniTask<int> RequestAddAnimalSpawnpoint(int spawnTable, Vector3 position, CancellationToken token = default)
    {
        return RequestAddSpawnpoint(SpawnType.Animal, spawnTable, position, 0f, token);
    }

    /// <summary>
    /// Sends a request to the server to add a spawnpoint with the specified info.
    /// </summary>
    /// <remarks>Thread-safe.</remarks>
    /// <exception cref="InvalidOperationException">Not connected to a DevkitServer server.</exception>
    public static UniTask<int> RequestAddVehicleSpawnpoint(int spawnTable, Vector3 position, float yaw = 0f, CancellationToken token = default)
    {
        return RequestAddSpawnpoint(SpawnType.Vehicle, spawnTable, position, yaw, token);
    }

    /// <summary>
    /// Sends a request to the server to add a spawnpoint with the specified info.
    /// </summary>
    /// <remarks>Thread-safe.</remarks>
    /// <exception cref="InvalidOperationException">Not connected to a DevkitServer server.</exception>
    public static UniTask<int> RequestAddPlayerSpawnpoint(Vector3 position, float yaw = 0f, bool isAlt = false, CancellationToken token = default)
    {
        return RequestAddSpawnpoint(SpawnType.Player, byte.MaxValue, position, yaw, token, isAlt ? (byte)1 : (byte)0);
    }

    /// <summary>
    /// Sends a request to the server to add a spawnpoint with the specified info.
    /// </summary>
    /// <remarks>Thread-safe.</remarks>
    /// <exception cref="InvalidOperationException">Not connected to a DevkitServer server.</exception>
    public static async UniTask<RegionIdentifier> RequestAddItemSpawnpoint(int spawnTable, Vector3 position, CancellationToken token = default)
    {
        return RegionIdentifier.CreateUnsafe(await RequestAddSpawnpoint(SpawnType.Item, spawnTable, position, 0f, token));
    }

    /// <summary>
    /// Sends a request to the server to add a spawnpoint with the specified info.
    /// </summary>
    /// <remarks>Thread-safe.</remarks>
    /// <exception cref="InvalidOperationException">Not connected to a DevkitServer server.</exception>
    public static async UniTask<RegionIdentifier> RequestAddZombieSpawnpoint(int spawnTable, Vector3 position, CancellationToken token = default)
    {
        return RegionIdentifier.CreateUnsafe(await RequestAddSpawnpoint(SpawnType.Zombie, spawnTable, position, 0f, token));
    }
    internal static async UniTask<int> RequestAddSpawnpoint(SpawnType spawnType, int spawnTable, Vector3 position, float yaw, CancellationToken token, byte instantiateFlags = 0)
    {
        DevkitServerModule.AssertIsDevkitServerClient();

        await UniTask.SwitchToMainThread(token);

        SpawnsNetIdDatabase.TryGetSpawnTableNetId(spawnType, spawnTable, out NetId64 spawnTableNetId);

        bool shouldAllow = true;

        TransformationDelta t = new TransformationDelta(spawnType is SpawnType.Vehicle or SpawnType.Player
            ? TransformationDelta.TransformFlags.AllNew
            : TransformationDelta.TransformFlags.Position, position, yaw, default, default);

        if (ClientEvents.ListeningOnRequestInstantiateSpawnpointRequested)
        {
            ClientEvents.InvokeOnRequestInstantiateSpawnpointRequested(new RequestInstantiateSpawnpointProperties(t, spawnTableNetId, spawnType), ref shouldAllow);

            if (!shouldAllow)
                return -1;
        }

        NetTask netTask = RequestSpawnInstantiation.Request(SendSpawnInstantiation, (byte)spawnType, spawnTableNetId, position, yaw, instantiateFlags, 10000);

        if (!ClientEvents.EventOnRequestInstantiateSpawnpoint.IsEmpty)
            ClientEvents.EventOnRequestInstantiateSpawnpoint.TryInvoke(new RequestInstantiateSpawnpointProperties(t, spawnTableNetId, spawnType));

        RequestResponse response2 = default;
        if (token.CanBeCanceled)
        {
            await Task.Run(async () =>
            {
                response2 = await netTask;
            }, token);
        }
        else
            response2 = await netTask;

        await UniTask.SwitchToMainThread(token);

        RequestResponse response = response2;
        if (!response.Responded)
            return -1;

        if (!response.TryGetParameter(1, out NetId64 netId))
        {
            Logger.DevkitServer.LogWarning(nameof(RequestAddSpawnpoint), $"Failed to get NetId from incoming SendSpawnInstantiation ({spawnType.GetLowercaseText()}).");
            return -1;
        }
        
        if (spawnType is SpawnType.Animal or SpawnType.Player or SpawnType.Vehicle)
        {
            if (!SpawnsNetIdDatabase.TryGetSpawnpoint(spawnType, netId, out int index))
            {
                Logger.DevkitServer.LogWarning(nameof(RequestAddSpawnpoint), $"Failed to resolve NetId in spawnpoint database from incoming SendSpawnInstantiation ({spawnType.GetLowercaseText()}).");
                return -1;
            }

            if (index < GetSpawnCountUnsafe(spawnType))
                return index;
        }
        else
        {
            if (!SpawnsNetIdDatabase.TryGetSpawnpoint(spawnType, netId, out RegionIdentifier id))
            {
                Logger.DevkitServer.LogWarning(nameof(RequestAddSpawnpoint), $"Failed to resolve NetId in spawnpoint database from incoming SendSpawnInstantiation ({spawnType.GetLowercaseText()}).");
                return -1;
            }

            if (spawnType == SpawnType.Item && id.CheckSafe(LevelItems.spawns)
                || spawnType == SpawnType.Zombie && id.CheckSafe(LevelZombies.spawns))
                return id.Raw;
        }

        Logger.DevkitServer.LogWarning(nameof(RequestAddSpawnpoint), $"Failed to resolve NetId (index out of range) in spawnpoint database from incoming SendSpawnInstantiation ({spawnType.GetLowercaseText()}).");
        return -1;
    }
#endif
}