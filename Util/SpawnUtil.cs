using DevkitServer.Models;
using DevkitServer.Util.Region;
#if CLIENT
using DevkitServer.Core;
using DevkitServer.Core.Tools;
using UnityEngine.Rendering;
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
    /// Locally sets the <see cref="PlayerSpawnpoint.isAlt"/> backing field to <paramref name="isAlternate"/>.
    /// </summary>
    /// <exception cref="MemberAccessException">Reflection failure.</exception>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static void SetIsAlternate(this PlayerSpawnpoint spawn, bool isAlternate)
    {
        ThreadUtil.assertIsGameThread();

        if (SetPlayerSpawnpointIsAlternate == null)
            throw new MemberAccessException("Instance setter for PlayerSpawnpoint.isAlt is not valid.");

        bool oldIsAlternate = spawn.isAlt;
        SetPlayerSpawnpointIsAlternate.Invoke(spawn, isAlternate);
        Logger.LogDebug($"Player spawnpoint updated: {(oldIsAlternate ? "Alternate" : "Primary")} -> {(isAlternate ? "Alternate" : "Primary")}");
    }

    /// <summary>
    /// Locally removes an <see cref="AnimalSpawnpoint"/> from the list and destroyes the node.
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool RemoveAnimalSpawn(AnimalSpawnpoint spawn) => RemoveAnimalSpawn(spawn, true);
    internal static bool RemoveAnimalSpawn(AnimalSpawnpoint spawn, bool destroyNode)
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
            Logger.LogDebug($"Animal spawnpoint index updated after replacing other removed spawnpoint on add: {oldIndex.Format()} -> {index.Format()}.");
        }

        Logger.LogDebug($"Animal spawnpoint removed: {index.Format()}.");

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
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool RemoveVehicleSpawn(VehicleSpawnpoint spawn) => RemoveVehicleSpawn(spawn, true);
    internal static bool RemoveVehicleSpawn(VehicleSpawnpoint spawn, bool destroyNode)
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
            Logger.LogDebug($"Vehicle spawnpoint index updated after replacing other removed spawnpoint on add: {oldIndex.Format()} -> {index.Format()}.");
        }

        Logger.LogDebug($"Vehicle spawnpoint removed: {index.Format()}.");

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
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool RemovePlayerSpawn(PlayerSpawnpoint spawn) => RemovePlayerSpawn(spawn, true);
    internal static bool RemovePlayerSpawn(PlayerSpawnpoint spawn, bool destroyNode)
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
            Logger.LogDebug($"Player spawnpoint index updated after replacing other removed spawnpoint on add: {oldIndex.Format()} -> {index.Format()}.");
        }

        Logger.LogDebug($"Player spawnpoint removed: {index.Format()}.");
        
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
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool RemoveItemSpawn(ItemSpawnpoint spawn) => RemoveItemSpawn(spawn, true);
    internal static bool RemoveItemSpawn(ItemSpawnpoint spawn, bool destroyNode)
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
                Logger.LogDebug($"Item spawnpoint index updated after replacing other removed spawnpoint on add: {oldRegion.Format()} -> {newRegion.Format()}.");
            }

            Logger.LogDebug($"Item spawnpoint removed: {newRegion.Format()}.");

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
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool RemoveZombieSpawn(ZombieSpawnpoint spawn) => RemoveZombieSpawn(spawn, true);
    internal static bool RemoveZombieSpawn(ZombieSpawnpoint spawn, bool destroyNode)
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
                Logger.LogDebug($"Zombie spawnpoint index updated after replacing other removed spawnpoint on add: {oldRegion.Format()} -> {newRegion.Format()}.");
            }

            Logger.LogDebug($"Zombie spawnpoint removed: {newRegion.Format()}.");

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
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static void AddAnimalSpawn(AnimalSpawnpoint spawn)
    {
        ThreadUtil.assertIsGameThread();

#if CLIENT
        SetResourceShaders(spawn.node);
#endif

        int index = LevelAnimals.spawns.IndexOf(spawn);
        bool added = index == -1;
        if (added)
        {
            index = LevelAnimals.spawns.Count;
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
            Logger.LogDebug("Animal spawnpoint added.");
        }
        else
            Logger.LogDebug("Animal spawnpoint already added.");
    }

    /// <summary>
    /// Add a <see cref="VehicleSpawnpoint"/> to <see cref="LevelVehicles"/> if it isn't already added and adds any necessary component to it.
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static void AddVehicleSpawn(VehicleSpawnpoint spawn)
    {
        ThreadUtil.assertIsGameThread();

#if CLIENT
        SetResourceShaders(spawn.node);
#endif

        int index = LevelVehicles.spawns.IndexOf(spawn);
        bool added = index == -1;
        if (added)
        {
            index = LevelVehicles.spawns.Count;
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
            Logger.LogDebug("Vehicle spawnpoint added.");
        }
        else
            Logger.LogDebug("Vehicle spawnpoint already added.");
    }

    /// <summary>
    /// Add a <see cref="PlayerSpawnpoint"/> to <see cref="LevelPlayers"/> if it isn't already added and adds any necessary component to it.
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static void AddPlayerSpawn(PlayerSpawnpoint spawn)
    {
        ThreadUtil.assertIsGameThread();

#if CLIENT
        SetResourceShaders(spawn.node);
#endif

        int index = LevelPlayers.spawns.IndexOf(spawn);
        bool added = index == -1;
        if (added)
        {
            index = LevelPlayers.spawns.Count;
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
            Logger.LogDebug("Player spawnpoint added.");
        }
        else
            Logger.LogDebug("Player spawnpoint already added.");
    }

    /// <summary>
    /// Add an <see cref="ItemSpawnpoint"/> to <see cref="LevelItems"/> if it isn't already added and adds any necessary component to it.
    /// </summary>
    /// <remarks>Region is calculated from <see cref="ItemSpawnpoint.point"/>. If it's out of bounds the editor node will be destroyed and nothing will be added.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static void AddItemSpawn(ItemSpawnpoint spawn)
    {
        ThreadUtil.assertIsGameThread();

#if CLIENT
        SetResourceShaders(spawn.node);
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
                region[index] = region[region.Count - 1];
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
                Logger.LogDebug($"Item spawnpoint region updated after replacing other removed spawnpoint on add: {changedOldRegion.Value.Format()} -> {changedNewRegion.Value.Format()}.");
            }
            alreadyExists = false;
            regionChanged = true;
        }


        if (!alreadyExists)
        {
            List<ItemSpawnpoint> region = LevelItems.spawns[x2, y2];
            index2 = region.Count;
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

            Logger.LogDebug($"Item spawnpoint region updated on add: {oldRegion.Format()} -> {regionId.Format()}.");
        }
        else if (!alreadyExists)
        {
            EventOnItemSpawnpointAdded.TryInvoke(spawn, regionId);
            Logger.LogDebug($"Item spawnpoint added: {regionId.Format()}.");
        }
    }

    /// <summary>
    /// Add a <see cref="ZombieSpawnpoint"/> to <see cref="LevelZombies"/> if it isn't already added and adds any necessary component to it.
    /// </summary>
    /// <remarks>Region is calculated from <see cref="ItemSpawnpoint.point"/>. If it's out of bounds the editor node will be destroyed and nothing will be added.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static void AddZombieSpawn(ZombieSpawnpoint spawn)
    {
        ThreadUtil.assertIsGameThread();

#if CLIENT
        SetResourceShaders(spawn.node);
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
                region[index] = region[region.Count - 1];
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
                Logger.LogDebug($"Zombie spawnpoint region updated after replacing other removed spawnpoint on add: {changedOldRegion.Value.Format()} -> {changedNewRegion.Value.Format()}.");
            }
            alreadyExists = false;
            regionChanged = true;
        }


        if (!alreadyExists)
        {
            List<ZombieSpawnpoint> region = LevelZombies.spawns[x2, y2];
            index2 = region.Count;
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

            Logger.LogDebug($"Zombie spawnpoint region updated on add: {oldRegion.Format()} -> {regionId.Format()}.");
        }
        else if (!alreadyExists)
        {
            EventOnZombieSpawnpointAdded.TryInvoke(spawn, regionId);
            Logger.LogDebug($"Zombie spawnpoint added: {regionId.Format()}.");
        }
    }

    /// <summary>
    /// Locally moves the spawnpoint to <paramref name="position"/>. Calls local events.
    /// </summary>
    /// <exception cref="MissingMemberException">Reflection failure.</exception>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static void MoveSpawnpoint(AnimalSpawnpoint point, Vector3 position)
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
        Logger.LogDebug($"Animal spawnpoint moved: {oldPosition.Format("F0")} -> {position.Format("F0")}.");
    }

    /// <summary>
    /// Locally moves the spawnpoint to <paramref name="position"/>. Calls local events.
    /// </summary>
    /// <exception cref="MissingMemberException">Reflection failure.</exception>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static void MoveSpawnpoint(VehicleSpawnpoint point, Vector3 position)
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
        Logger.LogDebug($"Vehicle spawnpoint moved: {oldPosition.Format("F0")} -> {position.Format("F0")}.");
    }

    /// <summary>
    /// Locally moves the spawnpoint to <paramref name="position"/>. Calls local events.
    /// </summary>
    /// <exception cref="MissingMemberException">Reflection failure.</exception>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static void MoveSpawnpoint(PlayerSpawnpoint point, Vector3 position)
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
        Logger.LogDebug($"Player spawnpoint moved: {oldPosition.Format("F0")} -> {position.Format("F0")}.");
    }

    /// <summary>
    /// Locally moves the spawnpoint to <paramref name="position"/>. Calls local events.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="position"/> is not a valid position in the region grid.</exception>
    /// <exception cref="MissingMemberException">Reflection failure.</exception>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static void MoveSpawnpoint(ItemSpawnpoint point, Vector3 position, out RegionIdentifier region)
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
                    region[index] = region[region.Count - 1];
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
                    Logger.LogDebug($"Item spawnpoint region updated after replacing other removed spawnpoint on move: {changedOldRegion.Value.Format()} -> {newRegion.Format()}.");
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

            Logger.LogDebug($"Item spawnpoint moved: {removedFrom.Value.Format()} -> {region.Format()}, pos: {oldPosition.Format("F0")} -> {position.Format("F0")}.");
        }
        else
        {
            EventOnItemSpawnpointAdded.TryInvoke(point, region);
            Logger.LogDebug($"Item spawnpoint added on move: {region.Format()}.");
        }
    }

    /// <summary>
    /// Locally moves the spawnpoint to <paramref name="position"/>. Calls local events.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="position"/> is not a valid position in the region grid.</exception>
    /// <exception cref="MissingMemberException">Reflection failure.</exception>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static void MoveSpawnpoint(ZombieSpawnpoint point, Vector3 position, out RegionIdentifier region)
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
                    region[index] = region[region.Count - 1];
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
                    Logger.LogDebug($"Zombie spawnpoint region updated after replacing other removed spawnpoint on move: {changedOldRegion.Value.Format()} -> {newRegion.Format()}.");
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

            Logger.LogDebug($"Zombie spawnpoint moved: {removedFrom.Value.Format()} -> {region.Format()}, pos: {oldPosition.Format("F0")} -> {position.Format("F0")}.");
        }
        else
        {
            EventOnZombieSpawnpointAdded.TryInvoke(point, region);
            Logger.LogDebug($"Zombie spawnpoint added on move: {region.Format()}.");
        }
    }

    /// <summary>
    /// Locally changes the yaw (y-axis) angle of the spawnpoint to <paramref name="yaw"/>. Calls local events.
    /// </summary>
    /// <exception cref="MissingMemberException">Reflection failure.</exception>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static void RotateSpawnpoint(VehicleSpawnpoint point, float yaw)
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
        Logger.LogDebug($"Vehicle spawnpoint rotated: {oldYaw.Format("F0")}° -> {yaw.Format("F0")}°.");
    }

    /// <summary>
    /// Locally changes the yaw (y-axis) angle of the spawnpoint to <paramref name="yaw"/>. Calls local events.
    /// </summary>
    /// <exception cref="MissingMemberException">Reflection failure.</exception>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static void RotateSpawnpoint(PlayerSpawnpoint point, float yaw)
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
        Logger.LogDebug($"Player spawnpoint rotated: {oldYaw.Format("F0")}° -> {yaw.Format("F0")}°.");
    }

    /// <summary>
    /// Locally changes the position and yaw (y-axis) angle of the spawnpoint to <paramref name="position"/> and <paramref name="yaw"/>. Calls local events.
    /// </summary>
    /// <exception cref="MissingMemberException">Reflection failure.</exception>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static void TransformSpawnpoint(VehicleSpawnpoint point, Vector3 position, float yaw)
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
        Logger.LogDebug($"Vehicle spawnpoint transformed: {oldPosition.Format("F0")} -> {position.Format("F0")}, {oldYaw.Format("F0")}° -> {yaw.Format("F0")}°.");
    }

    /// <summary>
    /// Locally changes the position and yaw (y-axis) angle of the spawnpoint to <paramref name="position"/> and <paramref name="yaw"/>. Calls local events.
    /// </summary>
    /// <exception cref="MissingMemberException">Reflection failure.</exception>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static void TransformSpawnpoint(PlayerSpawnpoint point, Vector3 position, float yaw)
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
        Logger.LogDebug($"Player spawnpoint transformed: {oldPosition.Format("F0")} -> {position.Format("F0")}, {oldYaw.Format("F0")}° -> {yaw.Format("F0")}°.");
    }

    internal static void SetPoint(this AnimalSpawnpoint spawn, Vector3 point) => SetAnimalSpawnpointPoint?.Invoke(spawn, point);
    internal static void SetPoint(this VehicleSpawnpoint spawn, Vector3 point) => SetVehicleSpawnpointPoint?.Invoke(spawn, point);
    internal static void SetPoint(this PlayerSpawnpoint spawn, Vector3 point) => SetPlayerSpawnpointPoint?.Invoke(spawn, point);
    internal static void SetPoint(this ItemSpawnpoint spawn, Vector3 point) => SetItemSpawnpointPoint?.Invoke(spawn, point);
    internal static void SetPoint(this ZombieSpawnpoint spawn, Vector3 point) => SetZombieSpawnpointPoint?.Invoke(spawn, point);
    internal static void SetYaw(this VehicleSpawnpoint spawn, float yaw) => SetVehicleSpawnpointAngle?.Invoke(spawn, yaw);
    internal static void SetYaw(this PlayerSpawnpoint spawn, float yaw) => SetPlayerSpawnpointAngle?.Invoke(spawn, yaw);
#if CLIENT
    internal static void SetResourceShaders(Transform? node)
    {
        if (node == null)
            return;
        node.gameObject.layer = 3;
        if (SharedResources.LogicShader != null && node.gameObject.TryGetComponent(out Renderer renderer))
            renderer.material.shader = SharedResources.LogicShader;
    }
#endif
}
