using DevkitServer.API;
using DevkitServer.API.Devkit.Spawns;
using DevkitServer.API.Permissions;
using DevkitServer.Core.Permissions;
using DevkitServer.Models;
using DevkitServer.Multiplayer.Actions;
using DevkitServer.Multiplayer.Levels;
using DevkitServer.Util.Region;
using SDG.Framework.Devkit;
using SDG.Framework.Utilities;

namespace DevkitServer.Util;

public delegate void AnimalSpawnTableArgs(AnimalTable spawnTable, int index);
public delegate void VehicleSpawnTableArgs(VehicleTable spawnTable, int index);
public delegate void ItemSpawnTableArgs(ItemTable spawnTable, int index);
public delegate void ZombieSpawnTableArgs(ZombieTable spawnTable, int index);

public delegate void AnimalSpawnTierArgs(AnimalTier spawnTier, SpawnTierIdentifier identifier);
public delegate void VehicleSpawnTierArgs(VehicleTier spawnTier, SpawnTierIdentifier identifier);
public delegate void ItemSpawnTierArgs(ItemTier spawnTier, SpawnTierIdentifier identifier);
public delegate void ZombieSpawnTierArgs(ZombieSlot spawnTier, SpawnTierIdentifier identifier);

public delegate void AnimalSpawnAssetArgs(AnimalSpawn spawnAsset, SpawnAssetIdentifier identifier);
public delegate void VehicleSpawnAssetArgs(VehicleSpawn spawnAsset, SpawnAssetIdentifier identifier);
public delegate void ItemSpawnAssetArgs(ItemSpawn spawnAsset, SpawnAssetIdentifier identifier);
public delegate void ZombieSpawnAssetArgs(ZombieCloth spawnAsset, SpawnAssetIdentifier identifier);

public delegate void AnimalSpawnTableIndexUpdated(AnimalTable spawnTable, int fromIndex, int toIndex);
public delegate void VehicleSpawnTableIndexUpdated(VehicleTable spawnTable, int fromIndex, int toIndex);
public delegate void ItemSpawnTableIndexUpdated(ItemTable spawnTable, int fromIndex, int toIndex);
public delegate void ZombieSpawnTableIndexUpdated(ZombieTable spawnTable, int fromIndex, int toIndex);

public delegate void AnimalSpawnTierIndexUpdated(AnimalTier spawnTier, SpawnTierIdentifier fromIndex, SpawnTierIdentifier toIndex);
public delegate void VehicleSpawnTierIndexUpdated(VehicleTier spawnTier, SpawnTierIdentifier fromIndex, SpawnTierIdentifier toIndex);
public delegate void ItemSpawnTierIndexUpdated(ItemTier spawnTier, SpawnTierIdentifier fromIndex, SpawnTierIdentifier toIndex);
public delegate void ZombieSpawnTierIndexUpdated(ZombieSlot spawnTier, SpawnTierIdentifier fromIndex, SpawnTierIdentifier toIndex);

public delegate void AnimalSpawnAssetIndexUpdated(AnimalSpawn spawnAsset, SpawnAssetIdentifier fromIndex, SpawnAssetIdentifier toIndex);
public delegate void VehicleSpawnAssetIndexUpdated(VehicleSpawn spawnAsset, SpawnAssetIdentifier fromIndex, SpawnAssetIdentifier toIndex);
public delegate void ItemSpawnAssetIndexUpdated(ItemSpawn spawnAsset, SpawnAssetIdentifier fromIndex, SpawnAssetIdentifier toIndex);
public delegate void ZombieSpawnAssetIndexUpdated(ZombieCloth spawnAsset, SpawnAssetIdentifier fromIndex, SpawnAssetIdentifier toIndex);

[EarlyTypeInit]
public static class SpawnTableUtil
{
    private static readonly InstanceSetter<ZombieTable, ZombieDifficultyAsset?>? SetCachedDifficultyAsset = Accessor.GenerateInstanceSetter<ZombieTable, ZombieDifficultyAsset?>("cachedDifficulty", throwOnError: false);

    internal static readonly CachedMulticastEvent<AnimalSpawnTableArgs> EventOnAnimalSpawnTableNameUpdated = new CachedMulticastEvent<AnimalSpawnTableArgs>(typeof(SpawnTableUtil), nameof(OnAnimalSpawnTableNameUpdated));
    internal static readonly CachedMulticastEvent<VehicleSpawnTableArgs> EventOnVehicleSpawnTableNameUpdated = new CachedMulticastEvent<VehicleSpawnTableArgs>(typeof(SpawnTableUtil), nameof(OnVehicleSpawnTableNameUpdated));
    internal static readonly CachedMulticastEvent<ItemSpawnTableArgs> EventOnItemSpawnTableNameUpdated = new CachedMulticastEvent<ItemSpawnTableArgs>(typeof(SpawnTableUtil), nameof(OnItemSpawnTableNameUpdated));
    internal static readonly CachedMulticastEvent<ZombieSpawnTableArgs> EventOnZombieSpawnTableNameUpdated = new CachedMulticastEvent<ZombieSpawnTableArgs>(typeof(SpawnTableUtil), nameof(OnZombieSpawnTableNameUpdated));
    
    internal static readonly CachedMulticastEvent<AnimalSpawnTableArgs> EventOnAnimalSpawnTableColorUpdated = new CachedMulticastEvent<AnimalSpawnTableArgs>(typeof(SpawnTableUtil), nameof(OnAnimalSpawnTableColorUpdated));
    internal static readonly CachedMulticastEvent<VehicleSpawnTableArgs> EventOnVehicleSpawnTableColorUpdated = new CachedMulticastEvent<VehicleSpawnTableArgs>(typeof(SpawnTableUtil), nameof(OnVehicleSpawnTableColorUpdated));
    internal static readonly CachedMulticastEvent<ItemSpawnTableArgs> EventOnItemSpawnTableColorUpdated = new CachedMulticastEvent<ItemSpawnTableArgs>(typeof(SpawnTableUtil), nameof(OnItemSpawnTableColorUpdated));
    internal static readonly CachedMulticastEvent<ZombieSpawnTableArgs> EventOnZombieSpawnTableColorUpdated = new CachedMulticastEvent<ZombieSpawnTableArgs>(typeof(SpawnTableUtil), nameof(OnZombieSpawnTableColorUpdated));
    
    internal static readonly CachedMulticastEvent<AnimalSpawnTableArgs> EventOnAnimalSpawnTableSpawnAssetUpdated = new CachedMulticastEvent<AnimalSpawnTableArgs>(typeof(SpawnTableUtil), nameof(OnAnimalSpawnTableSpawnAssetUpdated));
    internal static readonly CachedMulticastEvent<VehicleSpawnTableArgs> EventOnVehicleSpawnTableSpawnAssetUpdated = new CachedMulticastEvent<VehicleSpawnTableArgs>(typeof(SpawnTableUtil), nameof(OnVehicleSpawnTableSpawnAssetUpdated));
    internal static readonly CachedMulticastEvent<ItemSpawnTableArgs> EventOnItemSpawnTableSpawnAssetUpdated = new CachedMulticastEvent<ItemSpawnTableArgs>(typeof(SpawnTableUtil), nameof(OnItemSpawnTableSpawnAssetUpdated));
    internal static readonly CachedMulticastEvent<ZombieSpawnTableArgs> EventOnZombieSpawnTableSpawnAssetUpdated = new CachedMulticastEvent<ZombieSpawnTableArgs>(typeof(SpawnTableUtil), nameof(OnZombieSpawnTableSpawnAssetUpdated));
    
    internal static readonly CachedMulticastEvent<ZombieSpawnTableArgs> EventOnZombieSpawnTableIsMegaUpdated = new CachedMulticastEvent<ZombieSpawnTableArgs>(typeof(SpawnTableUtil), nameof(OnZombieSpawnTableIsMegaUpdated));
    internal static readonly CachedMulticastEvent<ZombieSpawnTableArgs> EventOnZombieSpawnTableHealthUpdated = new CachedMulticastEvent<ZombieSpawnTableArgs>(typeof(SpawnTableUtil), nameof(OnZombieSpawnTableHealthUpdated));
    internal static readonly CachedMulticastEvent<ZombieSpawnTableArgs> EventOnZombieSpawnTableDamageUpdated = new CachedMulticastEvent<ZombieSpawnTableArgs>(typeof(SpawnTableUtil), nameof(OnZombieSpawnTableDamageUpdated));
    internal static readonly CachedMulticastEvent<ZombieSpawnTableArgs> EventOnZombieSpawnTableLootIndexUpdated = new CachedMulticastEvent<ZombieSpawnTableArgs>(typeof(SpawnTableUtil), nameof(OnZombieSpawnTableLootIndexUpdated));
    internal static readonly CachedMulticastEvent<ZombieSpawnTableArgs> EventOnZombieSpawnTableXPUpdated = new CachedMulticastEvent<ZombieSpawnTableArgs>(typeof(SpawnTableUtil), nameof(OnZombieSpawnTableXPUpdated));
    internal static readonly CachedMulticastEvent<ZombieSpawnTableArgs> EventOnZombieSpawnTableRegenUpdated = new CachedMulticastEvent<ZombieSpawnTableArgs>(typeof(SpawnTableUtil), nameof(OnZombieSpawnTableRegenUpdated));
    internal static readonly CachedMulticastEvent<ZombieSpawnTableArgs> EventOnZombieSpawnTableDifficultyAssetUpdated = new CachedMulticastEvent<ZombieSpawnTableArgs>(typeof(SpawnTableUtil), nameof(OnZombieSpawnTableDifficultyAssetUpdated));

    internal static readonly CachedMulticastEvent<AnimalSpawnTableArgs> EventOnAnimalSpawnTableRemoved = new CachedMulticastEvent<AnimalSpawnTableArgs>(typeof(SpawnTableUtil), nameof(OnAnimalSpawnTableRemoved));
    internal static readonly CachedMulticastEvent<VehicleSpawnTableArgs> EventOnVehicleSpawnTableRemoved = new CachedMulticastEvent<VehicleSpawnTableArgs>(typeof(SpawnTableUtil), nameof(OnVehicleSpawnTableRemoved));
    internal static readonly CachedMulticastEvent<ItemSpawnTableArgs> EventOnItemSpawnTableRemoved = new CachedMulticastEvent<ItemSpawnTableArgs>(typeof(SpawnTableUtil), nameof(OnItemSpawnTableRemoved));
    internal static readonly CachedMulticastEvent<ZombieSpawnTableArgs> EventOnZombieSpawnTableRemoved = new CachedMulticastEvent<ZombieSpawnTableArgs>(typeof(SpawnTableUtil), nameof(OnZombieSpawnTableRemoved));
    
    internal static readonly CachedMulticastEvent<AnimalSpawnTableIndexUpdated> EventOnAnimalSpawnTableIndexUpdated = new CachedMulticastEvent<AnimalSpawnTableIndexUpdated>(typeof(SpawnTableUtil), nameof(OnAnimalSpawnTableIndexUpdated));
    internal static readonly CachedMulticastEvent<VehicleSpawnTableIndexUpdated> EventOnVehicleSpawnTableIndexUpdated = new CachedMulticastEvent<VehicleSpawnTableIndexUpdated>(typeof(SpawnTableUtil), nameof(OnVehicleSpawnTableIndexUpdated));
    internal static readonly CachedMulticastEvent<ItemSpawnTableIndexUpdated> EventOnItemSpawnTableIndexUpdated = new CachedMulticastEvent<ItemSpawnTableIndexUpdated>(typeof(SpawnTableUtil), nameof(OnItemSpawnTableIndexUpdated));
    internal static readonly CachedMulticastEvent<ZombieSpawnTableIndexUpdated> EventOnZombieSpawnTableIndexUpdated = new CachedMulticastEvent<ZombieSpawnTableIndexUpdated>(typeof(SpawnTableUtil), nameof(OnZombieSpawnTableIndexUpdated));
    
    internal static readonly CachedMulticastEvent<AnimalSpawnTierArgs> EventOnAnimalSpawnTierRemoved = new CachedMulticastEvent<AnimalSpawnTierArgs>(typeof(SpawnTableUtil), nameof(OnAnimalSpawnTierRemoved));
    internal static readonly CachedMulticastEvent<VehicleSpawnTierArgs> EventOnVehicleSpawnTierRemoved = new CachedMulticastEvent<VehicleSpawnTierArgs>(typeof(SpawnTableUtil), nameof(OnVehicleSpawnTierRemoved));
    internal static readonly CachedMulticastEvent<ItemSpawnTierArgs> EventOnItemSpawnTierRemoved = new CachedMulticastEvent<ItemSpawnTierArgs>(typeof(SpawnTableUtil), nameof(OnItemSpawnTierRemoved));
    internal static readonly CachedMulticastEvent<ZombieSpawnTierArgs> EventOnZombieSpawnTierRemoved = new CachedMulticastEvent<ZombieSpawnTierArgs>(typeof(SpawnTableUtil), nameof(OnItemSpawnTierRemoved));
    
    internal static readonly CachedMulticastEvent<AnimalSpawnTierIndexUpdated> EventOnAnimalSpawnTierIndexUpdated = new CachedMulticastEvent<AnimalSpawnTierIndexUpdated>(typeof(SpawnTableUtil), nameof(OnAnimalSpawnTierIndexUpdated));
    internal static readonly CachedMulticastEvent<VehicleSpawnTierIndexUpdated> EventOnVehicleSpawnTierIndexUpdated = new CachedMulticastEvent<VehicleSpawnTierIndexUpdated>(typeof(SpawnTableUtil), nameof(OnVehicleSpawnTierIndexUpdated));
    internal static readonly CachedMulticastEvent<ItemSpawnTierIndexUpdated> EventOnItemSpawnTierIndexUpdated = new CachedMulticastEvent<ItemSpawnTierIndexUpdated>(typeof(SpawnTableUtil), nameof(OnItemSpawnTierIndexUpdated));
    internal static readonly CachedMulticastEvent<ZombieSpawnTierIndexUpdated> EventOnZombieSpawnTierIndexUpdated = new CachedMulticastEvent<ZombieSpawnTierIndexUpdated>(typeof(SpawnTableUtil), nameof(OnZombieSpawnTierIndexUpdated));
    
    internal static readonly CachedMulticastEvent<AnimalSpawnAssetArgs> EventOnAnimalSpawnAssetRemoved = new CachedMulticastEvent<AnimalSpawnAssetArgs>(typeof(SpawnTableUtil), nameof(OnAnimalSpawnAssetRemoved));
    internal static readonly CachedMulticastEvent<VehicleSpawnAssetArgs> EventOnVehicleSpawnAssetRemoved = new CachedMulticastEvent<VehicleSpawnAssetArgs>(typeof(SpawnTableUtil), nameof(OnVehicleSpawnAssetRemoved));
    internal static readonly CachedMulticastEvent<ItemSpawnAssetArgs> EventOnItemSpawnAssetRemoved = new CachedMulticastEvent<ItemSpawnAssetArgs>(typeof(SpawnTableUtil), nameof(OnItemSpawnAssetRemoved));
    internal static readonly CachedMulticastEvent<ZombieSpawnAssetArgs> EventOnZombieSpawnAssetRemoved = new CachedMulticastEvent<ZombieSpawnAssetArgs>(typeof(SpawnTableUtil), nameof(OnZombieSpawnAssetRemoved));
    
    internal static readonly CachedMulticastEvent<AnimalSpawnAssetIndexUpdated> EventOnAnimalSpawnAssetIndexUpdated = new CachedMulticastEvent<AnimalSpawnAssetIndexUpdated>(typeof(SpawnTableUtil), nameof(OnAnimalSpawnAssetIndexUpdated));
    internal static readonly CachedMulticastEvent<VehicleSpawnAssetIndexUpdated> EventOnVehicleSpawnAssetIndexUpdated = new CachedMulticastEvent<VehicleSpawnAssetIndexUpdated>(typeof(SpawnTableUtil), nameof(OnVehicleSpawnAssetIndexUpdated));
    internal static readonly CachedMulticastEvent<ItemSpawnAssetIndexUpdated> EventOnItemSpawnAssetIndexUpdated = new CachedMulticastEvent<ItemSpawnAssetIndexUpdated>(typeof(SpawnTableUtil), nameof(OnItemSpawnAssetIndexUpdated));
    internal static readonly CachedMulticastEvent<ZombieSpawnAssetIndexUpdated> EventOnZombieSpawnAssetIndexUpdated = new CachedMulticastEvent<ZombieSpawnAssetIndexUpdated>(typeof(SpawnTableUtil), nameof(OnZombieSpawnAssetIndexUpdated));

    public static event AnimalSpawnTableArgs OnAnimalSpawnTableNameUpdated
    {
        add => EventOnAnimalSpawnTableNameUpdated.Add(value);
        remove => EventOnAnimalSpawnTableNameUpdated.Remove(value);
    }
    public static event VehicleSpawnTableArgs OnVehicleSpawnTableNameUpdated
    {
        add => EventOnVehicleSpawnTableNameUpdated.Add(value);
        remove => EventOnVehicleSpawnTableNameUpdated.Remove(value);
    }
    public static event ItemSpawnTableArgs OnItemSpawnTableNameUpdated
    {
        add => EventOnItemSpawnTableNameUpdated.Add(value);
        remove => EventOnItemSpawnTableNameUpdated.Remove(value);
    }
    public static event ZombieSpawnTableArgs OnZombieSpawnTableNameUpdated
    {
        add => EventOnZombieSpawnTableNameUpdated.Add(value);
        remove => EventOnZombieSpawnTableNameUpdated.Remove(value);
    }

    public static event AnimalSpawnTableArgs OnAnimalSpawnTableColorUpdated
    {
        add => EventOnAnimalSpawnTableColorUpdated.Add(value);
        remove => EventOnAnimalSpawnTableColorUpdated.Remove(value);
    }
    public static event VehicleSpawnTableArgs OnVehicleSpawnTableColorUpdated
    {
        add => EventOnVehicleSpawnTableColorUpdated.Add(value);
        remove => EventOnVehicleSpawnTableColorUpdated.Remove(value);
    }
    public static event ItemSpawnTableArgs OnItemSpawnTableColorUpdated
    {
        add => EventOnItemSpawnTableColorUpdated.Add(value);
        remove => EventOnItemSpawnTableColorUpdated.Remove(value);
    }
    public static event ZombieSpawnTableArgs OnZombieSpawnTableColorUpdated
    {
        add => EventOnZombieSpawnTableColorUpdated.Add(value);
        remove => EventOnZombieSpawnTableColorUpdated.Remove(value);
    }
    
    public static event AnimalSpawnTableArgs OnAnimalSpawnTableSpawnAssetUpdated
    {
        add => EventOnAnimalSpawnTableSpawnAssetUpdated.Add(value);
        remove => EventOnAnimalSpawnTableSpawnAssetUpdated.Remove(value);
    }
    public static event VehicleSpawnTableArgs OnVehicleSpawnTableSpawnAssetUpdated
    {
        add => EventOnVehicleSpawnTableSpawnAssetUpdated.Add(value);
        remove => EventOnVehicleSpawnTableSpawnAssetUpdated.Remove(value);
    }
    public static event ItemSpawnTableArgs OnItemSpawnTableSpawnAssetUpdated
    {
        add => EventOnItemSpawnTableSpawnAssetUpdated.Add(value);
        remove => EventOnItemSpawnTableSpawnAssetUpdated.Remove(value);
    }
    public static event ZombieSpawnTableArgs OnZombieSpawnTableSpawnAssetUpdated
    {
        add => EventOnZombieSpawnTableSpawnAssetUpdated.Add(value);
        remove => EventOnZombieSpawnTableSpawnAssetUpdated.Remove(value);
    }

    public static event ZombieSpawnTableArgs OnZombieSpawnTableIsMegaUpdated
    {
        add => EventOnZombieSpawnTableIsMegaUpdated.Add(value);
        remove => EventOnZombieSpawnTableIsMegaUpdated.Remove(value);
    }
    public static event ZombieSpawnTableArgs OnZombieSpawnTableHealthUpdated
    {
        add => EventOnZombieSpawnTableHealthUpdated.Add(value);
        remove => EventOnZombieSpawnTableHealthUpdated.Remove(value);
    }
    public static event ZombieSpawnTableArgs OnZombieSpawnTableDamageUpdated
    {
        add => EventOnZombieSpawnTableDamageUpdated.Add(value);
        remove => EventOnZombieSpawnTableDamageUpdated.Remove(value);
    }
    public static event ZombieSpawnTableArgs OnZombieSpawnTableLootIndexUpdated
    {
        add => EventOnZombieSpawnTableLootIndexUpdated.Add(value);
        remove => EventOnZombieSpawnTableLootIndexUpdated.Remove(value);
    }
    public static event ZombieSpawnTableArgs OnZombieSpawnTableXPUpdated
    {
        add => EventOnZombieSpawnTableXPUpdated.Add(value);
        remove => EventOnZombieSpawnTableXPUpdated.Remove(value);
    }
    public static event ZombieSpawnTableArgs OnZombieSpawnTableRegenUpdated
    {
        add => EventOnZombieSpawnTableRegenUpdated.Add(value);
        remove => EventOnZombieSpawnTableRegenUpdated.Remove(value);
    }
    public static event ZombieSpawnTableArgs OnZombieSpawnTableDifficultyAssetUpdated
    {
        add => EventOnZombieSpawnTableDifficultyAssetUpdated.Add(value);
        remove => EventOnZombieSpawnTableDifficultyAssetUpdated.Remove(value);
    }

    public static event AnimalSpawnTierArgs OnAnimalSpawnTierRemoved
    {
        add => EventOnAnimalSpawnTierRemoved.Add(value);
        remove => EventOnAnimalSpawnTierRemoved.Remove(value);
    }
    public static event VehicleSpawnTierArgs OnVehicleSpawnTierRemoved
    {
        add => EventOnVehicleSpawnTierRemoved.Add(value);
        remove => EventOnVehicleSpawnTierRemoved.Remove(value);
    }
    public static event ItemSpawnTierArgs OnItemSpawnTierRemoved
    {
        add => EventOnItemSpawnTierRemoved.Add(value);
        remove => EventOnItemSpawnTierRemoved.Remove(value);
    }
    public static event ZombieSpawnTierArgs OnZombieSpawnTierRemoved
    {
        add => EventOnZombieSpawnTierRemoved.Add(value);
        remove => EventOnZombieSpawnTierRemoved.Remove(value);
    }

    public static event AnimalSpawnTierIndexUpdated OnAnimalSpawnTierIndexUpdated
    {
        add => EventOnAnimalSpawnTierIndexUpdated.Add(value);
        remove => EventOnAnimalSpawnTierIndexUpdated.Remove(value);
    }
    public static event VehicleSpawnTierIndexUpdated OnVehicleSpawnTierIndexUpdated
    {
        add => EventOnVehicleSpawnTierIndexUpdated.Add(value);
        remove => EventOnVehicleSpawnTierIndexUpdated.Remove(value);
    }
    public static event ItemSpawnTierIndexUpdated OnItemSpawnTierIndexUpdated
    {
        add => EventOnItemSpawnTierIndexUpdated.Add(value);
        remove => EventOnItemSpawnTierIndexUpdated.Remove(value);
    }
    public static event ZombieSpawnTierIndexUpdated OnZombieSpawnTierIndexUpdated
    {
        add => EventOnZombieSpawnTierIndexUpdated.Add(value);
        remove => EventOnZombieSpawnTierIndexUpdated.Remove(value);
    }

    public static event AnimalSpawnTableArgs OnAnimalSpawnTableRemoved
    {
        add => EventOnAnimalSpawnTableRemoved.Add(value);
        remove => EventOnAnimalSpawnTableRemoved.Remove(value);
    }
    public static event VehicleSpawnTableArgs OnVehicleSpawnTableRemoved
    {
        add => EventOnVehicleSpawnTableRemoved.Add(value);
        remove => EventOnVehicleSpawnTableRemoved.Remove(value);
    }
    public static event ItemSpawnTableArgs OnItemSpawnTableRemoved
    {
        add => EventOnItemSpawnTableRemoved.Add(value);
        remove => EventOnItemSpawnTableRemoved.Remove(value);
    }
    public static event ZombieSpawnTableArgs OnZombieSpawnTableRemoved
    {
        add => EventOnZombieSpawnTableRemoved.Add(value);
        remove => EventOnZombieSpawnTableRemoved.Remove(value);
    }

    public static event AnimalSpawnTableIndexUpdated OnAnimalSpawnTableIndexUpdated
    {
        add => EventOnAnimalSpawnTableIndexUpdated.Add(value);
        remove => EventOnAnimalSpawnTableIndexUpdated.Remove(value);
    }
    public static event VehicleSpawnTableIndexUpdated OnVehicleSpawnTableIndexUpdated
    {
        add => EventOnVehicleSpawnTableIndexUpdated.Add(value);
        remove => EventOnVehicleSpawnTableIndexUpdated.Remove(value);
    }
    public static event ItemSpawnTableIndexUpdated OnItemSpawnTableIndexUpdated
    {
        add => EventOnItemSpawnTableIndexUpdated.Add(value);
        remove => EventOnItemSpawnTableIndexUpdated.Remove(value);
    }
    public static event ZombieSpawnTableIndexUpdated OnZombieSpawnTableIndexUpdated
    {
        add => EventOnZombieSpawnTableIndexUpdated.Add(value);
        remove => EventOnZombieSpawnTableIndexUpdated.Remove(value);
    }

    public static event AnimalSpawnAssetArgs OnAnimalSpawnAssetRemoved
    {
        add => EventOnAnimalSpawnAssetRemoved.Add(value);
        remove => EventOnAnimalSpawnAssetRemoved.Remove(value);
    }
    public static event VehicleSpawnAssetArgs OnVehicleSpawnAssetRemoved
    {
        add => EventOnVehicleSpawnAssetRemoved.Add(value);
        remove => EventOnVehicleSpawnAssetRemoved.Remove(value);
    }
    public static event ItemSpawnAssetArgs OnItemSpawnAssetRemoved
    {
        add => EventOnItemSpawnAssetRemoved.Add(value);
        remove => EventOnItemSpawnAssetRemoved.Remove(value);
    }
    public static event ZombieSpawnAssetArgs OnZombieSpawnAssetRemoved
    {
        add => EventOnZombieSpawnAssetRemoved.Add(value);
        remove => EventOnZombieSpawnAssetRemoved.Remove(value);
    }

    public static event AnimalSpawnAssetIndexUpdated OnAnimalSpawnAssetIndexUpdated
    {
        add => EventOnAnimalSpawnAssetIndexUpdated.Add(value);
        remove => EventOnAnimalSpawnAssetIndexUpdated.Remove(value);
    }
    public static event VehicleSpawnAssetIndexUpdated OnVehicleSpawnAssetIndexUpdated
    {
        add => EventOnVehicleSpawnAssetIndexUpdated.Add(value);
        remove => EventOnVehicleSpawnAssetIndexUpdated.Remove(value);
    }
    public static event ItemSpawnAssetIndexUpdated OnItemSpawnAssetIndexUpdated
    {
        add => EventOnItemSpawnAssetIndexUpdated.Add(value);
        remove => EventOnItemSpawnAssetIndexUpdated.Remove(value);
    }
    public static event ZombieSpawnAssetIndexUpdated OnZombieSpawnAssetIndexUpdated
    {
        add => EventOnZombieSpawnAssetIndexUpdated.Add(value);
        remove => EventOnZombieSpawnAssetIndexUpdated.Remove(value);
    }

#if CLIENT
    /// <summary>
    /// Deselect the current animal table, if any.
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static void DeselectAnimalTable()
    {
        ThreadUtil.assertIsGameThread();

        EditorSpawns.selectedAnimal = byte.MaxValue;
        EditorSpawnsAnimalsUI.updateSelection();
    }

    /// <summary>
    /// Set the given <see cref="AnimalTable"/> as the selected animal table for new spawns.
    /// </summary>
    /// <param name="applyToSelected">Should all selected <see cref="AnimalSpawnpoint"/>s get set to this table?</param>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SelectAnimalTable(this AnimalTable table, bool applyToSelected, bool replicateApplyToSelected) => SelectAnimalTable(LevelAnimals.tables.IndexOf(table), applyToSelected, replicateApplyToSelected);

    /// <summary>
    /// Set the <see cref="AnimalTable"/> at the given index as the selected animal table for new spawns.
    /// </summary>
    /// <param name="applyToSelected">Should all selected <see cref="AnimalSpawnpoint"/>s get set to this table?</param>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SelectAnimalTable(int index, bool applyToSelected, bool replicateApplyToSelected) // todo replicate all these
    {
        if (index == byte.MaxValue)
        {
            DeselectAnimalTable();
            return true;
        }

        ThreadUtil.assertIsGameThread();

        if (index < 0 || index >= LevelAnimals.tables.Count || index > byte.MaxValue)
            return false;

        EditorSpawns.selectedAnimal = (byte)index;
        if (EditorSpawns.animalSpawn != null && EditorSpawns.animalSpawn.TryGetComponent(out Renderer renderer))
            renderer.material.color = LevelAnimals.tables[index].color;
        EditorSpawnsAnimalsUI.updateSelection();

        if (!applyToSelected || !VanillaPermissions.SpawnTablesAnimalEdit.Has())
            return true;

        replicateApplyToSelected &= DevkitServerModule.IsEditing;
        bool singleListening = replicateApplyToSelected && (ClientEvents.ListeningOnSetSpawnpointType || ClientEvents.ListeningOnSetSpawnpointTypeRequested);
        bool batchListening = replicateApplyToSelected && ClientEvents.ListeningOnSetSpawnpointsType;
        replicateApplyToSelected &= singleListening || batchListening;
        NetId64 spawnTableNetId;
        if (replicateApplyToSelected)
            SpawnsNetIdDatabase.TryGetSpawnTableNetId(SpawnType.Animal, index, out spawnTableNetId);
        else
            spawnTableNetId = NetId64.Invalid;

        float dt = CachedTime.DeltaTime;

        SetSpawnpointTypeProperties singleProperties = default;
        List<NetId64>? toUpdate = batchListening ? ListPool<NetId64>.claim() : null;
        try
        {
            foreach (DevkitSelection selection in DevkitSelectionManager.selection)
            {
                if (!selection.gameObject.TryGetComponent(out AnimalSpawnpointNode node) ||
                    node.Spawnpoint.type == index)
                    continue;
                 
                if (singleListening)
                {
                    bool shouldAllow = true;
                    singleProperties = new SetSpawnpointTypeProperties(node.NetId, spawnTableNetId, SpawnType.Animal, dt);
                    ClientEvents.InvokeOnSetSpawnpointTypeRequested(in singleProperties, ref shouldAllow);
                    if (!shouldAllow)
                        continue;
                }

                node.Spawnpoint.type = (byte)index;
                node.Color = LevelAnimals.tables[index].color;

                Logger.DevkitServer.LogDebug(nameof(SelectAnimalTable), $"Spawn table updated for {node.Format()}.");
                SpawnUtil.EventOnAnimalSpawnTableChanged.TryInvoke(node.Spawnpoint, node.Index);

                if (singleListening)
                    ClientEvents.InvokeOnSetSpawnpointType(in singleProperties);

                if (!batchListening)
                    continue;

                toUpdate!.Add(node.NetId);
                if (toUpdate.Count >= SpawnUtil.MaxUpdateSpawnTypeCount)
                    Flush(toUpdate, dt, spawnTableNetId);
            }
            if (batchListening)
                Flush(toUpdate!, dt, spawnTableNetId);

            static void Flush(List<NetId64> toUpdate, float dt, NetId64 spawnTableNetId)
            {
                SetSpawnpointsTypeProperties properties = new SetSpawnpointsTypeProperties(toUpdate.ToSpan(), spawnTableNetId, SpawnType.Animal, dt);

                ClientEvents.InvokeOnSetSpawnpointsType(in properties);

                toUpdate.Clear();
            }
        }
        finally
        {
            if (batchListening)
                ListPool<NetId64>.release(toUpdate);
        }

        return true;
    }

    /// <summary>
    /// Deselect the current vehicle table, if any.
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static void DeselectVehicleTable()
    {
        ThreadUtil.assertIsGameThread();

        EditorSpawns.selectedVehicle = byte.MaxValue;
        EditorSpawnsVehiclesUI.updateSelection();
    }

    /// <summary>
    /// Set the given <see cref="VehicleTable"/> as the selected vehicle table for new spawns.
    /// </summary>
    /// <param name="applyToSelected">Should all selected <see cref="VehicleSpawnpoint"/>s get set to this table?</param>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SelectVehicleTable(this VehicleTable table, bool applyToSelected, bool replicateApplyToSelected) => SelectVehicleTable(LevelVehicles.tables.IndexOf(table), applyToSelected, replicateApplyToSelected);

    /// <summary>
    /// Set the <see cref="VehicleTable"/> at the given index as the selected vehicle table for new spawns.
    /// </summary>
    /// <param name="applyToSelected">Should all selected <see cref="VehicleSpawnpoint"/>s get set to this table?</param>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SelectVehicleTable(int index, bool applyToSelected, bool replicateApplyToSelected)
    {
        if (index == byte.MaxValue)
        {
            DeselectVehicleTable();
            return true;
        }

        ThreadUtil.assertIsGameThread();

        if (index < 0 || index >= LevelVehicles.tables.Count || index > byte.MaxValue)
            return false;

        EditorSpawns.selectedVehicle = (byte)index;
        if (EditorSpawns.vehicleSpawn != null && EditorSpawns.vehicleSpawn.TryGetComponent(out Renderer renderer))
            renderer.material.color = LevelVehicles.tables[index].color;
        EditorSpawnsVehiclesUI.updateSelection();

        if (!applyToSelected || !VanillaPermissions.SpawnTablesVehicleEdit.Has())
            return true;

        replicateApplyToSelected &= DevkitServerModule.IsEditing;
        bool singleListening = replicateApplyToSelected && (ClientEvents.ListeningOnSetSpawnpointType || ClientEvents.ListeningOnSetSpawnpointTypeRequested);
        bool batchListening = replicateApplyToSelected && ClientEvents.ListeningOnSetSpawnpointsType;
        replicateApplyToSelected &= singleListening || batchListening;
        NetId64 spawnTableNetId;
        if (replicateApplyToSelected)
            SpawnsNetIdDatabase.TryGetSpawnTableNetId(SpawnType.Vehicle, index, out spawnTableNetId);
        else
            spawnTableNetId = NetId64.Invalid;

        float dt = CachedTime.DeltaTime;

        SetSpawnpointTypeProperties singleProperties = default;
        List<NetId64>? toUpdate = batchListening ? ListPool<NetId64>.claim() : null;
        try
        {
            foreach (DevkitSelection selection in DevkitSelectionManager.selection)
            {
                if (!selection.gameObject.TryGetComponent(out VehicleSpawnpointNode node) ||
                    node.Spawnpoint.type == index)
                    continue;

                if (singleListening)
                {
                    bool shouldAllow = true;
                    singleProperties = new SetSpawnpointTypeProperties(node.NetId, spawnTableNetId, SpawnType.Vehicle, dt);
                    ClientEvents.InvokeOnSetSpawnpointTypeRequested(in singleProperties, ref shouldAllow);
                    if (!shouldAllow)
                        continue;
                }

                node.Spawnpoint.type = (byte)index;
                node.Color = LevelVehicles.tables[index].color;

                Logger.DevkitServer.LogDebug(nameof(SelectVehicleTable), $"Spawn table updated for {node.Format()}.");
                SpawnUtil.EventOnVehicleSpawnTableChanged.TryInvoke(node.Spawnpoint, node.Index);

                if (singleListening)
                    ClientEvents.InvokeOnSetSpawnpointType(in singleProperties);

                if (!batchListening)
                    continue;

                toUpdate!.Add(node.NetId);
                if (toUpdate.Count >= SpawnUtil.MaxUpdateSpawnTypeCount)
                    Flush(toUpdate, dt, spawnTableNetId);
            }
            if (batchListening)
                Flush(toUpdate!, dt, spawnTableNetId);

            static void Flush(List<NetId64> toUpdate, float dt, NetId64 spawnTableNetId)
            {
                SetSpawnpointsTypeProperties properties = new SetSpawnpointsTypeProperties(toUpdate.ToSpan(), spawnTableNetId, SpawnType.Vehicle, dt);

                ClientEvents.InvokeOnSetSpawnpointsType(in properties);

                toUpdate.Clear();
            }
        }
        finally
        {
            if (batchListening)
                ListPool<NetId64>.release(toUpdate);
        }

        return true;
    }

    /// <summary>
    /// Deselect the current item table, if any.
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static void DeselectItemTable()
    {
        ThreadUtil.assertIsGameThread();

        EditorSpawns.selectedItem = byte.MaxValue;
        EditorSpawnsItemsUI.updateSelection();
    }

    /// <summary>
    /// Set the given <see cref="ItemTable"/> as the selected item table for new spawns.
    /// </summary>
    /// <param name="applyToSelected">Should all selected <see cref="ItemSpawnpoint"/>s get set to this table?</param>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SelectItemTable(this ItemTable table, bool applyToSelected, bool replicateApplyToSelected) => SelectItemTable(LevelItems.tables.IndexOf(table), applyToSelected, replicateApplyToSelected);

    /// <summary>
    /// Set the <see cref="ItemTable"/> at the given index as the selected item table for new spawns.
    /// </summary>
    /// <param name="applyToSelected">Should all selected <see cref="ItemSpawnpoint"/>s get set to this table?</param>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SelectItemTable(int index, bool applyToSelected, bool replicateApplyToSelected)
    {
        if (index == byte.MaxValue)
        {
            DeselectItemTable();
            return true;
        }

        ThreadUtil.assertIsGameThread();

        if (index < 0 || index >= LevelItems.tables.Count || index > byte.MaxValue)
            return false;

        EditorSpawns.selectedItem = (byte)index;
        if (EditorSpawns.itemSpawn != null && EditorSpawns.itemSpawn.TryGetComponent(out Renderer renderer))
            renderer.material.color = LevelItems.tables[index].color;
        EditorSpawnsItemsUI.updateSelection();

        if (!applyToSelected || !VanillaPermissions.SpawnTablesItemEdit.Has())
            return true;

        replicateApplyToSelected &= DevkitServerModule.IsEditing;
        bool singleListening = replicateApplyToSelected && (ClientEvents.ListeningOnSetSpawnpointType || ClientEvents.ListeningOnSetSpawnpointTypeRequested);
        bool batchListening = replicateApplyToSelected && ClientEvents.ListeningOnSetSpawnpointsType;
        replicateApplyToSelected &= singleListening || batchListening;
        NetId64 spawnTableNetId;
        if (replicateApplyToSelected)
            SpawnsNetIdDatabase.TryGetSpawnTableNetId(SpawnType.Item, index, out spawnTableNetId);
        else
            spawnTableNetId = NetId64.Invalid;

        float dt = CachedTime.DeltaTime;

        SetSpawnpointTypeProperties singleProperties = default;
        List<NetId64>? toUpdate = batchListening ? ListPool<NetId64>.claim() : null;
        try
        {
            foreach (DevkitSelection selection in DevkitSelectionManager.selection)
            {
                if (!selection.gameObject.TryGetComponent(out ItemSpawnpointNode node) ||
                    node.Spawnpoint.type == index)
                    continue;

                if (singleListening)
                {
                    bool shouldAllow = true;
                    singleProperties = new SetSpawnpointTypeProperties(node.NetId, spawnTableNetId, SpawnType.Item, dt);
                    ClientEvents.InvokeOnSetSpawnpointTypeRequested(in singleProperties, ref shouldAllow);
                    if (!shouldAllow)
                        continue;
                }

                node.Spawnpoint.type = (byte)index;
                node.Color = LevelItems.tables[index].color;

                Logger.DevkitServer.LogDebug(nameof(SelectItemTable), $"Spawn table updated for {node.Format()}.");
                SpawnUtil.EventOnItemSpawnTableChanged.TryInvoke(node.Spawnpoint, node.Region);

                if (singleListening)
                    ClientEvents.InvokeOnSetSpawnpointType(in singleProperties);

                if (!batchListening)
                    continue;

                toUpdate!.Add(node.NetId);
                if (toUpdate.Count >= SpawnUtil.MaxUpdateSpawnTypeCount)
                    Flush(toUpdate, dt, spawnTableNetId);
            }
            if (batchListening)
                Flush(toUpdate!, dt, spawnTableNetId);

            static void Flush(List<NetId64> toUpdate, float dt, NetId64 spawnTableNetId)
            {
                SetSpawnpointsTypeProperties properties = new SetSpawnpointsTypeProperties(toUpdate.ToSpan(), spawnTableNetId, SpawnType.Item, dt);

                ClientEvents.InvokeOnSetSpawnpointsType(in properties);

                toUpdate.Clear();
            }
        }
        finally
        {
            if (batchListening)
                ListPool<NetId64>.release(toUpdate);
        }

        return true;
    }

    /// <summary>
    /// Deselect the current zombie table, if any.
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static void DeselectZombieTable()
    {
        ThreadUtil.assertIsGameThread();

        EditorSpawns.selectedZombie = byte.MaxValue;
        EditorSpawnsZombiesUI.updateSelection();
    }

    /// <summary>
    /// Set the given <see cref="ZombieTable"/> as the selected zombie table for new spawns.
    /// </summary>
    /// <param name="applyToSelected">Should all selected <see cref="ZombieSpawnpoint"/>s get set to this table?</param>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SelectZombieTable(this ZombieTable table, bool applyToSelected, bool replicateApplyToSelected) => SelectZombieTable(LevelZombies.tables.IndexOf(table), applyToSelected, replicateApplyToSelected);

    /// <summary>
    /// Set the <see cref="ZombieTable"/> at the given index as the selected zombie table for new spawns.
    /// </summary>
    /// <param name="applyToSelected">Should all selected <see cref="ZombieSpawnpoint"/>s get set to this table?</param>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SelectZombieTable(int index, bool applyToSelected, bool replicateApplyToSelected)
    {
        if (index == byte.MaxValue)
        {
            DeselectZombieTable();
            return true;
        }

        ThreadUtil.assertIsGameThread();

        if (index < 0 || index >= LevelZombies.tables.Count || index > byte.MaxValue)
            return false;

        EditorSpawns.selectedZombie = (byte)index;
        if (EditorSpawns.zombieSpawn != null && EditorSpawns.zombieSpawn.TryGetComponent(out Renderer renderer))
            renderer.material.color = LevelZombies.tables[index].color;
        EditorSpawnsZombiesUI.updateSelection();

        if (!applyToSelected || !VanillaPermissions.SpawnTablesZombieEdit.Has())
            return true;

        replicateApplyToSelected &= DevkitServerModule.IsEditing;
        bool singleListening = replicateApplyToSelected && (ClientEvents.ListeningOnSetSpawnpointType || ClientEvents.ListeningOnSetSpawnpointTypeRequested);
        bool batchListening = replicateApplyToSelected && ClientEvents.ListeningOnSetSpawnpointsType;
        replicateApplyToSelected &= singleListening || batchListening;
        NetId64 spawnTableNetId;
        if (replicateApplyToSelected)
            SpawnsNetIdDatabase.TryGetSpawnTableNetId(SpawnType.Zombie, index, out spawnTableNetId);
        else
            spawnTableNetId = NetId64.Invalid;

        float dt = CachedTime.DeltaTime;

        SetSpawnpointTypeProperties singleProperties = default;
        List<NetId64>? toUpdate = batchListening ? ListPool<NetId64>.claim() : null;
        try
        {
            foreach (DevkitSelection selection in DevkitSelectionManager.selection)
            {
                if (!selection.gameObject.TryGetComponent(out ZombieSpawnpointNode node) ||
                    node.Spawnpoint.type == index)
                    continue;

                if (singleListening)
                {
                    bool shouldAllow = true;
                    singleProperties = new SetSpawnpointTypeProperties(node.NetId, spawnTableNetId, SpawnType.Zombie, dt);
                    ClientEvents.InvokeOnSetSpawnpointTypeRequested(in singleProperties, ref shouldAllow);
                    if (!shouldAllow)
                        continue;
                }

                node.Spawnpoint.type = (byte)index;
                node.Color = LevelZombies.tables[index].color;

                Logger.DevkitServer.LogDebug(nameof(SelectZombieTable), $"Spawn table updated for {node.Format()}.");
                SpawnUtil.EventOnZombieSpawnTableChanged.TryInvoke(node.Spawnpoint, node.Region);

                if (singleListening)
                    ClientEvents.InvokeOnSetSpawnpointType(in singleProperties);

                if (!batchListening)
                    continue;

                toUpdate!.Add(node.NetId);
                if (toUpdate.Count >= SpawnUtil.MaxUpdateSpawnTypeCount)
                    Flush(toUpdate, dt, spawnTableNetId);
            }
            if (batchListening)
                Flush(toUpdate!, dt, spawnTableNetId);

            static void Flush(List<NetId64> toUpdate, float dt, NetId64 spawnTableNetId)
            {
                SetSpawnpointsTypeProperties properties = new SetSpawnpointsTypeProperties(toUpdate.ToSpan(), spawnTableNetId, SpawnType.Zombie, dt);

                ClientEvents.InvokeOnSetSpawnpointsType(in properties);

                toUpdate.Clear();
            }
        }
        finally
        {
            if (batchListening)
                ListPool<NetId64>.release(toUpdate);
        }

        return true;
    }
#endif

    /// <summary>
    /// Locally remove a spawn table, any spawnpoints using it, and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool RemoveSpawnTableLocal(this AnimalTable spawnTable)
    {
        ThreadUtil.assertIsGameThread();

        return RemoveSpawnTableLocal(SpawnType.Animal, LevelAnimals.tables.IndexOf(spawnTable));
    }

    /// <summary>
    /// Locally remove a spawn table, any spawnpoints using it, and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool RemoveSpawnTableLocal(this VehicleTable spawnTable)
    {
        ThreadUtil.assertIsGameThread();

        return RemoveSpawnTableLocal(SpawnType.Vehicle, LevelVehicles.tables.IndexOf(spawnTable));
    }

    /// <summary>
    /// Locally remove a spawn table, any spawnpoints using it, and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool RemoveSpawnTableLocal(this ItemTable spawnTable)
    {
        ThreadUtil.assertIsGameThread();

        return RemoveSpawnTableLocal(SpawnType.Item, LevelItems.tables.IndexOf(spawnTable));
    }

    /// <summary>
    /// Locally remove a spawn table, any spawnpoints using it, and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool RemoveSpawnTableLocal(this ZombieTable spawnTable)
    {
        ThreadUtil.assertIsGameThread();

        return RemoveSpawnTableLocal(SpawnType.Zombie, LevelZombies.tables.IndexOf(spawnTable));
    }

    /// <summary>
    /// Locally remove a spawn table, any spawnpoints using it, and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool RemoveSpawnTableLocal(SpawnType spawnType, int index)
    {
        ThreadUtil.assertIsGameThread();

        if (!CheckSpawnTableSafe(spawnType, index))
            return false;
        
        switch (spawnType)
        {
            case SpawnType.Animal:
                AnimalTable animalTable = LevelAnimals.tables[index];
                LevelAnimals.tables.RemoveAt(index);
                
                for (int i = LevelAnimals.spawns.Count - 1; i >= 0; --i)
                {
                    AnimalSpawnpoint spawnpoint = LevelAnimals.spawns[i];
                    if (spawnpoint.type == index)
                        SpawnUtil.RemoveAnimalSpawnLocal(spawnpoint, true);
                    else if (spawnpoint.type > index)
                        --spawnpoint.type;
                }

#if CLIENT
                if (index == EditorSpawns.selectedAnimal)
                {
                    int newIndex = index;
                    if (newIndex == LevelAnimals.tables.Count - 1)
                        --newIndex;
                    if (newIndex >= LevelAnimals.tables.Count || newIndex < 0)
                        DeselectAnimalTable();
                    else
                        SelectAnimalTable(newIndex, false, false);
                }
#else
                if (index == EditorSpawns.selectedAnimal)
                {
                    int newIndex = index;
                    if (newIndex == LevelAnimals.tables.Count - 1)
                        --newIndex;
                    if (newIndex >= LevelAnimals.tables.Count || newIndex < 0)
                        EditorSpawns.selectedAnimal = byte.MaxValue;
                    else
                        EditorSpawns.selectedAnimal = (byte)newIndex;
                }
#endif

                int maxCt = Math.Min(byte.MaxValue - 1, animalTable.tiers.Count);

                bool hasAssetRem = !EventOnAnimalSpawnAssetRemoved.IsEmpty;

                if (!EventOnAnimalSpawnTierRemoved.IsEmpty || hasAssetRem)
                {
                    for (int i = 0; i < maxCt; i++)
                    {
                        AnimalTier tier = animalTable.tiers[i];
                        SpawnTierIdentifier id = new SpawnTierIdentifier(spawnType, (byte)index, (byte)i);
                        if (hasAssetRem)
                        {
                            int raw = id.Raw;
                            int maxAssetCt = Math.Min(byte.MaxValue - 1, tier.table.Count);

                            for (int j = 0; j < maxAssetCt; ++j)
                            {
                                SpawnAssetIdentifier assetId = new SpawnAssetIdentifier(raw | (j << 24));
                                EventOnAnimalSpawnAssetRemoved.TryInvoke(tier.table[j], assetId);
#if DEBUG
                                Logger.DevkitServer.LogDebug(nameof(RemoveSpawnTableLocal), $"Animal spawn asset removed: {assetId.Format()} (parent removed).");
#endif
                            }
                        }
                        
                        EventOnAnimalSpawnTierRemoved.TryInvoke(tier, id);
#if DEBUG
                        Logger.DevkitServer.LogDebug(nameof(RemoveSpawnTableLocal), $"Animal spawn tier removed: {tier.name.Format()} ({id.Format()}) (parent removed).");
#endif
                    }
                }
                
                EventOnAnimalSpawnTableRemoved.TryInvoke(animalTable, index);
                Logger.DevkitServer.LogDebug(nameof(RemoveSpawnTableLocal), $"Animal spawn table removed: {animalTable.name.Format()} (# {index.Format()}).");
                hasAssetRem = !EventOnAnimalSpawnAssetIndexUpdated.IsEmpty;
                bool hasTierRem = !EventOnAnimalSpawnTierIndexUpdated.IsEmpty;
                if (EventOnAnimalSpawnTableIndexUpdated.IsEmpty && !hasTierRem && !hasAssetRem)
                    break;

                for (int i = index; i < LevelAnimals.tables.Count; ++i)
                {
                    EventOnAnimalSpawnTableIndexUpdated.TryInvoke(LevelAnimals.tables[i], i + 1, i);
#if DEBUG
                    Logger.DevkitServer.LogDebug(nameof(RemoveSpawnTableLocal), $"Animal spawn table index updated: {LevelAnimals.tables[i].name.Format()} (# {(i + 1).Format()} -> {i.Format()}).");
#endif
                    if (!hasTierRem && !hasAssetRem)
                        continue;

                    for (int k = 0; k < maxCt; k++)
                    {
                        AnimalTier tier = animalTable.tiers[k];
                        SpawnTierIdentifier id = new SpawnTierIdentifier(spawnType, (byte)i, (byte)k);
                        SpawnTierIdentifier idOld = new SpawnTierIdentifier(spawnType, (byte)(i + 1), (byte)k);

                        EventOnAnimalSpawnTierIndexUpdated.TryInvoke(tier, idOld, id);

                        if (hasAssetRem)
                        {
                            int raw = id.Raw;
                            int rawOld = idOld.Raw;
                            int maxAssetCt = Math.Min(byte.MaxValue - 1, tier.table.Count);

                            for (int j = 0; j < maxAssetCt; ++j)
                            {
                                SpawnAssetIdentifier assetId = new SpawnAssetIdentifier(raw | (j << 24));
                                SpawnAssetIdentifier assetOldId = new SpawnAssetIdentifier(rawOld | (j << 24));
                                EventOnAnimalSpawnAssetIndexUpdated.TryInvoke(tier.table[j], assetOldId, assetId);
#if DEBUG
                                Logger.DevkitServer.LogDebug(nameof(RemoveSpawnTableLocal), $"Animal spawn asset index updated: {assetOldId.Format()} -> {assetId.Format()}.");
#endif
                            }
                        }
#if DEBUG
                        Logger.DevkitServer.LogDebug(nameof(RemoveSpawnTableLocal), $"Animal spawn tier index updated: {idOld.Format()} -> {id.Format()}.");
#endif
                    }
                }

                break;
            case SpawnType.Vehicle:
                VehicleTable vehicleTable = LevelVehicles.tables[index];
                LevelVehicles.tables.RemoveAt(index);

                for (int i = LevelVehicles.spawns.Count - 1; i >= 0; --i)
                {
                    VehicleSpawnpoint spawnpoint = LevelVehicles.spawns[i];
                    if (spawnpoint.type == index)
                        SpawnUtil.RemoveVehicleSpawnLocal(spawnpoint, true);
                    else if (spawnpoint.type > index)
                        --spawnpoint.type;
                }

#if CLIENT
                if (index == EditorSpawns.selectedVehicle)
                {
                    int newIndex = index;
                    if (newIndex == LevelVehicles.tables.Count - 1)
                        --newIndex;
                    if (newIndex >= LevelVehicles.tables.Count || newIndex < 0)
                        DeselectVehicleTable();
                    else
                        SelectVehicleTable(newIndex, false, false);
                }
#else
                if (index == EditorSpawns.selectedVehicle)
                {
                    int newIndex = index;
                    if (newIndex == LevelVehicles.tables.Count - 1)
                        --newIndex;
                    if (newIndex >= LevelVehicles.tables.Count || newIndex < 0)
                        EditorSpawns.selectedVehicle = byte.MaxValue;
                    else
                        EditorSpawns.selectedVehicle = (byte)newIndex;
                }
#endif

                maxCt = Math.Min(byte.MaxValue - 1, vehicleTable.tiers.Count);

                hasAssetRem = !EventOnVehicleSpawnAssetRemoved.IsEmpty;

                if (!EventOnVehicleSpawnTierRemoved.IsEmpty || hasAssetRem)
                {
                    for (int i = 0; i < maxCt; i++)
                    {
                        VehicleTier tier = vehicleTable.tiers[i];
                        SpawnTierIdentifier id = new SpawnTierIdentifier(spawnType, (byte)index, (byte)i);
                        if (hasAssetRem)
                        {
                            int raw = id.Raw;
                            int maxAssetCt = Math.Min(byte.MaxValue - 1, tier.table.Count);

                            for (int j = 0; j < maxAssetCt; ++j)
                            {
                                SpawnAssetIdentifier assetId = new SpawnAssetIdentifier(raw | (j << 24));
                                EventOnVehicleSpawnAssetRemoved.TryInvoke(tier.table[j], assetId);
#if DEBUG
                                Logger.DevkitServer.LogDebug(nameof(RemoveSpawnTableLocal), $"Vehicle spawn asset removed: {assetId.Format()} (parent removed).");
#endif
                            }
                        }

                        EventOnVehicleSpawnTierRemoved.TryInvoke(tier, id);
#if DEBUG
                        Logger.DevkitServer.LogDebug(nameof(RemoveSpawnTableLocal), $"Vehicle spawn tier removed: {tier.name.Format()} ({id.Format()}) (parent removed).");
#endif
                    }
                }

                EventOnVehicleSpawnTableRemoved.TryInvoke(vehicleTable, index);
                Logger.DevkitServer.LogDebug(nameof(RemoveSpawnTableLocal), $"Vehicle spawn table removed: {vehicleTable.name.Format()} (# {index.Format()}).");
                hasAssetRem = !EventOnVehicleSpawnAssetIndexUpdated.IsEmpty;
                hasTierRem = !EventOnVehicleSpawnTierIndexUpdated.IsEmpty;
                if (EventOnVehicleSpawnTableIndexUpdated.IsEmpty && !hasTierRem && !hasAssetRem)
                    break;

                for (int i = index; i < LevelVehicles.tables.Count; ++i)
                {
                    EventOnVehicleSpawnTableIndexUpdated.TryInvoke(LevelVehicles.tables[i], i + 1, i);
#if DEBUG
                    Logger.DevkitServer.LogDebug(nameof(RemoveSpawnTableLocal), $"Vehicle spawn table index updated: {LevelVehicles.tables[i].name.Format()} (# {(i + 1).Format()} -> {i.Format()}).");
#endif
                    if (!hasTierRem && !hasAssetRem)
                        continue;

                    for (int k = 0; k < maxCt; k++)
                    {
                        VehicleTier tier = vehicleTable.tiers[k];
                        SpawnTierIdentifier id = new SpawnTierIdentifier(spawnType, (byte)i, (byte)k);
                        SpawnTierIdentifier idOld = new SpawnTierIdentifier(spawnType, (byte)(i + 1), (byte)k);

                        EventOnVehicleSpawnTierIndexUpdated.TryInvoke(tier, idOld, id);

                        if (hasAssetRem)
                        {
                            int raw = id.Raw;
                            int rawOld = idOld.Raw;
                            int maxAssetCt = Math.Min(byte.MaxValue - 1, tier.table.Count);

                            for (int j = 0; j < maxAssetCt; ++j)
                            {
                                SpawnAssetIdentifier assetId = new SpawnAssetIdentifier(raw | (j << 24));
                                SpawnAssetIdentifier assetOldId = new SpawnAssetIdentifier(rawOld | (j << 24));
                                EventOnVehicleSpawnAssetIndexUpdated.TryInvoke(tier.table[j], assetOldId, assetId);
#if DEBUG
                                Logger.DevkitServer.LogDebug(nameof(RemoveSpawnTableLocal), $"Vehicle spawn asset index updated: {assetOldId.Format()} -> {assetId.Format()}.");
#endif
                            }
                        }
#if DEBUG
                        Logger.DevkitServer.LogDebug(nameof(RemoveSpawnTableLocal), $"Vehicle spawn tier index updated: {idOld.Format()} -> {id.Format()}.");
#endif
                    }
                }

                break;
            case SpawnType.Item:
                ItemTable itemTable = LevelItems.tables[index];
                LevelItems.tables.RemoveAt(index);

                foreach (RegionCoord r in RegionUtil.LinearEnumerateRegions())
                {
                    List<ItemSpawnpoint> region = LevelItems.spawns[r.x, r.y];
                    for (int i = region.Count - 1; i >= 0; --i)
                    {
                        ItemSpawnpoint spawnpoint = region[i];
                        if (spawnpoint.type == index)
                            SpawnUtil.RemoveItemSpawnLocal(spawnpoint, true);
                        else if (spawnpoint.type > index)
                            --spawnpoint.type;
                    }
                }
                for (int i = 0; i < LevelZombies.tables.Count; ++i)
                {
                    ZombieTable table = LevelZombies.tables[i];
                    if (table.lootIndex <= index)
                        continue;

                    --table.lootIndex;
                }

#if CLIENT
                if (index == EditorSpawns.selectedItem)
                {
                    int newIndex = index;
                    if (newIndex == LevelItems.tables.Count - 1)
                        --newIndex;
                    if (newIndex >= LevelItems.tables.Count || newIndex < 0)
                        DeselectItemTable();
                    else
                        SelectItemTable(newIndex, false, false);
                }
#else
                if (index == EditorSpawns.selectedItem)
                {
                    int newIndex = index;
                    if (newIndex == LevelItems.tables.Count - 1)
                        --newIndex;
                    if (newIndex >= LevelItems.tables.Count || newIndex < 0)
                        EditorSpawns.selectedItem = byte.MaxValue;
                    else
                        EditorSpawns.selectedItem = (byte)newIndex;
                }
#endif

                maxCt = Math.Min(byte.MaxValue - 1, itemTable.tiers.Count);

                hasAssetRem = !EventOnItemSpawnAssetRemoved.IsEmpty;

                if (!EventOnItemSpawnTierRemoved.IsEmpty || hasAssetRem)
                {
                    for (int i = 0; i < maxCt; i++)
                    {
                        ItemTier tier = itemTable.tiers[i];
                        SpawnTierIdentifier id = new SpawnTierIdentifier(spawnType, (byte)index, (byte)i);
                        if (hasAssetRem)
                        {
                            int raw = id.Raw;
                            int maxAssetCt = Math.Min(byte.MaxValue - 1, tier.table.Count);

                            for (int j = 0; j < maxAssetCt; ++j)
                            {
                                SpawnAssetIdentifier assetId = new SpawnAssetIdentifier(raw | (j << 24));
                                EventOnItemSpawnAssetRemoved.TryInvoke(tier.table[j], assetId);
#if DEBUG
                                Logger.DevkitServer.LogDebug(nameof(RemoveSpawnTableLocal), $"Item spawn asset removed: {assetId.Format()} (parent removed).");
#endif
                            }
                        }

                        EventOnItemSpawnTierRemoved.TryInvoke(tier, id);
#if DEBUG
                        Logger.DevkitServer.LogDebug(nameof(RemoveSpawnTableLocal), $"Item spawn tier removed: {tier.name.Format()} ({id.Format()}) (parent removed).");
#endif
                    }
                }

                EventOnItemSpawnTableRemoved.TryInvoke(itemTable, index);
                Logger.DevkitServer.LogDebug(nameof(RemoveSpawnTableLocal), $"Item spawn table removed: {itemTable.name.Format()} (# {index.Format()}).");
                hasAssetRem = !EventOnItemSpawnAssetIndexUpdated.IsEmpty;
                hasTierRem = !EventOnItemSpawnTierIndexUpdated.IsEmpty;
                if (EventOnItemSpawnTableIndexUpdated.IsEmpty && !hasTierRem && !hasAssetRem)
                    break;

                for (int i = index; i < LevelItems.tables.Count; ++i)
                {
                    EventOnItemSpawnTableIndexUpdated.TryInvoke(LevelItems.tables[i], i + 1, i);
#if DEBUG
                    Logger.DevkitServer.LogDebug(nameof(RemoveSpawnTableLocal), $"Item spawn table index updated: {LevelItems.tables[i].name.Format()} (# {(i + 1).Format()} -> {i.Format()}).");
#endif
                    if (!hasTierRem && !hasAssetRem)
                        continue;

                    for (int k = 0; k < maxCt; k++)
                    {
                        ItemTier tier = itemTable.tiers[k];
                        SpawnTierIdentifier id = new SpawnTierIdentifier(spawnType, (byte)i, (byte)k);
                        SpawnTierIdentifier idOld = new SpawnTierIdentifier(spawnType, (byte)(i + 1), (byte)k);

                        EventOnItemSpawnTierIndexUpdated.TryInvoke(tier, idOld, id);

                        if (hasAssetRem)
                        {
                            int raw = id.Raw;
                            int rawOld = idOld.Raw;
                            int maxAssetCt = Math.Min(byte.MaxValue - 1, tier.table.Count);

                            for (int j = 0; j < maxAssetCt; ++j)
                            {
                                SpawnAssetIdentifier assetId = new SpawnAssetIdentifier(raw | (j << 24));
                                SpawnAssetIdentifier assetOldId = new SpawnAssetIdentifier(rawOld | (j << 24));
                                EventOnItemSpawnAssetIndexUpdated.TryInvoke(tier.table[j], assetOldId, assetId);
#if DEBUG
                                Logger.DevkitServer.LogDebug(nameof(RemoveSpawnTableLocal), $"Item spawn asset index updated: {assetOldId.Format()} -> {assetId.Format()}.");
#endif
                            }
                        }
#if DEBUG
                        Logger.DevkitServer.LogDebug(nameof(RemoveSpawnTableLocal), $"Item spawn tier index updated: {idOld.Format()} -> {id.Format()}.");
#endif
                    }
                }

                break;
            case SpawnType.Zombie:
                ZombieTable zombieTable = LevelZombies.tables[index];
                LevelZombies.tables.RemoveAt(index);

                foreach (RegionCoord r in RegionUtil.LinearEnumerateRegions())
                {
                    List<ZombieSpawnpoint> region = LevelZombies.spawns[r.x, r.y];
                    for (int i = region.Count - 1; i >= 0; --i)
                    {
                        ZombieSpawnpoint spawnpoint = region[i];
                        if (spawnpoint.type == index)
                            SpawnUtil.RemoveZombieSpawnLocal(spawnpoint, true);
                        else if (spawnpoint.type > index)
                            --spawnpoint.type;
                    }
                }

#if CLIENT
                if (index == EditorSpawns.selectedZombie)
                {
                    int newIndex = index;
                    if (newIndex == LevelZombies.tables.Count - 1)
                        --newIndex;
                    if (newIndex >= LevelZombies.tables.Count || newIndex < 0)
                        DeselectZombieTable();
                    else
                        SelectZombieTable(newIndex, false, false);
                }
#else
                if (index == EditorSpawns.selectedZombie)
                {
                    int newIndex = index;
                    if (newIndex == LevelZombies.tables.Count - 1)
                        --newIndex;
                    if (newIndex >= LevelZombies.tables.Count || newIndex < 0)
                        EditorSpawns.selectedZombie = byte.MaxValue;
                    else
                        EditorSpawns.selectedZombie = (byte)newIndex;
                }
#endif

                maxCt = Math.Min(byte.MaxValue - 1, zombieTable.slots.Length);

                hasAssetRem = !EventOnZombieSpawnAssetRemoved.IsEmpty;

                if (!EventOnZombieSpawnTierRemoved.IsEmpty || hasAssetRem)
                {
                    for (int i = 0; i < maxCt; i++)
                    {
                        ZombieSlot tier = zombieTable.slots[i];
                        SpawnTierIdentifier id = new SpawnTierIdentifier(spawnType, (byte)index, (byte)i);
                        if (hasAssetRem)
                        {
                            int raw = id.Raw;
                            int maxAssetCt = Math.Min(byte.MaxValue - 1, tier.table.Count);

                            for (int j = 0; j < maxAssetCt; ++j)
                            {
                                SpawnAssetIdentifier assetId = new SpawnAssetIdentifier(raw | (j << 24));
                                EventOnZombieSpawnAssetRemoved.TryInvoke(tier.table[j], assetId);
#if DEBUG
                                Logger.DevkitServer.LogDebug(nameof(RemoveSpawnTableLocal), $"Zombie spawn asset removed: {assetId.Format()} (parent removed).");
#endif
                            }
                        }

                        EventOnZombieSpawnTierRemoved.TryInvoke(tier, id);
#if DEBUG
                        Logger.DevkitServer.LogDebug(nameof(RemoveSpawnTableLocal), $"Zombie spawn tier removed: {id.Format()} (parent removed).");
#endif
                    }
                }

                EventOnZombieSpawnTableRemoved.TryInvoke(zombieTable, index);
                Logger.DevkitServer.LogDebug(nameof(RemoveSpawnTableLocal), $"Zombie spawn table removed: {zombieTable.name.Format()} (# {index.Format()}).");
                hasAssetRem = !EventOnZombieSpawnAssetIndexUpdated.IsEmpty;
                hasTierRem = !EventOnZombieSpawnTierIndexUpdated.IsEmpty;
                if (EventOnZombieSpawnTableIndexUpdated.IsEmpty && !hasTierRem && !hasAssetRem)
                    break;

                for (int i = index; i < LevelZombies.tables.Count; ++i)
                {
                    EventOnZombieSpawnTableIndexUpdated.TryInvoke(LevelZombies.tables[i], i + 1, i);
#if DEBUG
                    Logger.DevkitServer.LogDebug(nameof(RemoveSpawnTableLocal), $"Zombie spawn table index updated: {LevelZombies.tables[i].name.Format()} (# {(i + 1).Format()} -> {i.Format()}).");
#endif
                    if (!hasTierRem && !hasAssetRem)
                        continue;

                    for (int k = 0; k < maxCt; k++)
                    {
                        ZombieSlot tier = zombieTable.slots[k];
                        SpawnTierIdentifier id = new SpawnTierIdentifier(spawnType, (byte)i, (byte)k);
                        SpawnTierIdentifier idOld = new SpawnTierIdentifier(spawnType, (byte)(i + 1), (byte)k);

                        EventOnZombieSpawnTierIndexUpdated.TryInvoke(tier, idOld, id);

                        if (hasAssetRem)
                        {
                            int raw = id.Raw;
                            int rawOld = idOld.Raw;
                            int maxAssetCt = Math.Min(byte.MaxValue - 1, tier.table.Count);

                            for (int j = 0; j < maxAssetCt; ++j)
                            {
                                SpawnAssetIdentifier assetId = new SpawnAssetIdentifier(raw | (j << 24));
                                SpawnAssetIdentifier assetOldId = new SpawnAssetIdentifier(rawOld | (j << 24));
                                EventOnZombieSpawnAssetIndexUpdated.TryInvoke(tier.table[j], assetOldId, assetId);
#if DEBUG
                                Logger.DevkitServer.LogDebug(nameof(RemoveSpawnTableLocal), $"Zombie spawn asset index updated: {assetOldId.Format()} -> {assetId.Format()}.");
#endif
                            }
                        }
#if DEBUG
                        Logger.DevkitServer.LogDebug(nameof(RemoveSpawnTableLocal), $"Zombie spawn tier index updated: {idOld.Format()} -> {id.Format()}.");
#endif
                    }
                }

                break;
        }

        return true;
    }

    /// <summary>
    /// Locally set the name of a spawn table and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SetSpawnTableNameLocal(this AnimalTable spawnTable, string newName)
    {
        ThreadUtil.assertIsGameThread();

        return SetSpawnTableNameLocal(SpawnType.Animal, LevelAnimals.tables.IndexOf(spawnTable), newName);
    }

    /// <summary>
    /// Locally set the name of a spawn table and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SetSpawnTableNameLocal(this VehicleTable spawnTable, string newName)
    {
        ThreadUtil.assertIsGameThread();

        return SetSpawnTableNameLocal(SpawnType.Vehicle, LevelVehicles.tables.IndexOf(spawnTable), newName);
    }

    /// <summary>
    /// Locally set the name of a spawn table and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SetSpawnTableNameLocal(this ItemTable spawnTable, string newName)
    {
        ThreadUtil.assertIsGameThread();

        return SetSpawnTableNameLocal(SpawnType.Item, LevelItems.tables.IndexOf(spawnTable), newName);
    }

    /// <summary>
    /// Locally set the name of a spawn table and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SetSpawnTableNameLocal(this ZombieTable spawnTable, string newName)
    {
        ThreadUtil.assertIsGameThread();

        return SetSpawnTableNameLocal(SpawnType.Zombie, LevelZombies.tables.IndexOf(spawnTable), newName);
    }

    /// <summary>
    /// Locally set the name of a spawn table and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SetSpawnTableNameLocal(SpawnType spawnType, int index, string newName)
    {
        ThreadUtil.assertIsGameThread();

        newName ??= string.Empty;

        if (!CheckSpawnTableSafe(spawnType, index))
            return false;

        switch (spawnType)
        {
            case SpawnType.Animal:
                AnimalTable animalTable = LevelAnimals.tables[index];
                animalTable.name = newName;
                
                EventOnAnimalSpawnTableNameUpdated.TryInvoke(animalTable, index);
                break;
            
            case SpawnType.Vehicle:
                VehicleTable vehicleTable = LevelVehicles.tables[index];
                vehicleTable.name = newName;

                EventOnVehicleSpawnTableNameUpdated.TryInvoke(vehicleTable, index);
                break;
            
            case SpawnType.Item:
                ItemTable itemTable = LevelItems.tables[index];
                itemTable.name = newName;

                EventOnItemSpawnTableNameUpdated.TryInvoke(itemTable, index);
                break;
            
            case SpawnType.Zombie:
                ZombieTable zombieTable = LevelZombies.tables[index];
                zombieTable.name = newName;

                EventOnZombieSpawnTableNameUpdated.TryInvoke(zombieTable, index);
                break;
        }

        return true;
    }

    /// <summary>
    /// Locally set the color of a spawn table and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SetSpawnTableColorLocal(this AnimalTable spawnTable, Color newColor)
    {
        ThreadUtil.assertIsGameThread();

        return SetSpawnTableColorLocal(SpawnType.Animal, LevelAnimals.tables.IndexOf(spawnTable), newColor);
    }

    /// <summary>
    /// Locally set the color of a spawn table and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SetSpawnTableColorLocal(this VehicleTable spawnTable, Color newColor)
    {
        ThreadUtil.assertIsGameThread();

        return SetSpawnTableColorLocal(SpawnType.Vehicle, LevelVehicles.tables.IndexOf(spawnTable), newColor);
    }

    /// <summary>
    /// Locally set the color of a spawn table and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SetSpawnTableColorLocal(this ItemTable spawnTable, Color newColor)
    {
        ThreadUtil.assertIsGameThread();

        return SetSpawnTableColorLocal(SpawnType.Item, LevelItems.tables.IndexOf(spawnTable), newColor);
    }

    /// <summary>
    /// Locally set the color of a spawn table and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SetSpawnTableColorLocal(this ZombieTable spawnTable, Color newColor)
    {
        ThreadUtil.assertIsGameThread();

        return SetSpawnTableColorLocal(SpawnType.Zombie, LevelZombies.tables.IndexOf(spawnTable), newColor);
    }

    /// <summary>
    /// Locally set the color of a spawn table and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SetSpawnTableColorLocal(SpawnType spawnType, int index, Color newColor)
    {
        ThreadUtil.assertIsGameThread();

        if (!CheckSpawnTableSafe(spawnType, index))
            return false;

        newColor = new Color(Mathf.Clamp01(newColor.r), Mathf.Clamp01(newColor.g), Mathf.Clamp01(newColor.b), 1f);

        switch (spawnType)
        {
            case SpawnType.Animal:
                AnimalTable animalTable = LevelAnimals.tables[index];
                animalTable.color = newColor;
                
                EventOnAnimalSpawnTableColorUpdated.TryInvoke(animalTable, index);
                break;
            
            case SpawnType.Vehicle:
                VehicleTable vehicleTable = LevelVehicles.tables[index];
                vehicleTable.color = newColor;

                EventOnVehicleSpawnTableColorUpdated.TryInvoke(vehicleTable, index);
                break;
            
            case SpawnType.Item:
                ItemTable itemTable = LevelItems.tables[index];
                itemTable.color = newColor;

                EventOnItemSpawnTableColorUpdated.TryInvoke(itemTable, index);
                break;
            
            case SpawnType.Zombie:
                ZombieTable zombieTable = LevelZombies.tables[index];
                zombieTable.color = newColor;

                EventOnZombieSpawnTableColorUpdated.TryInvoke(zombieTable, index);
                break;
        }

        return true;
    }

    /// <summary>
    /// Locally set the spawn asset (<see cref="AnimalTable.tableID"/>) of a spawn table and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SetSpawnTableSpawnAssetLocal(this AnimalTable spawnTable, AssetReference<SpawnAsset> newSpawnAsset)
    {
        ThreadUtil.assertIsGameThread();

        return SetSpawnTableSpawnAssetLocal(SpawnType.Animal, LevelAnimals.tables.IndexOf(spawnTable), newSpawnAsset);
    }

    /// <summary>
    /// Locally set the spawn asset (<see cref="VehicleTable.tableID"/>) of a spawn table and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SetSpawnTableSpawnAssetLocal(this VehicleTable spawnTable, AssetReference<SpawnAsset> newSpawnAsset)
    {
        ThreadUtil.assertIsGameThread();

        return SetSpawnTableSpawnAssetLocal(SpawnType.Vehicle, LevelVehicles.tables.IndexOf(spawnTable), newSpawnAsset);
    }

    /// <summary>
    /// Locally set the spawn asset (<see cref="ItemTable.tableID"/>) of a spawn table and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SetSpawnTableSpawnAssetLocal(this ItemTable spawnTable, AssetReference<SpawnAsset> newSpawnAsset)
    {
        ThreadUtil.assertIsGameThread();

        return SetSpawnTableSpawnAssetLocal(SpawnType.Item, LevelItems.tables.IndexOf(spawnTable), newSpawnAsset);
    }

    /// <summary>
    /// Locally set the spawn asset (<see cref="ZombieTable.lootID"/>) of a spawn table and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SetSpawnTableSpawnAssetLocal(this ZombieTable spawnTable, AssetReference<SpawnAsset> newSpawnAsset)
    {
        ThreadUtil.assertIsGameThread();

        return SetSpawnTableSpawnAssetLocal(SpawnType.Zombie, LevelZombies.tables.IndexOf(spawnTable), newSpawnAsset);
    }

    /// <summary>
    /// Locally set the spawn asset (<see cref="AnimalTable.tableID"/>) of a spawn table and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SetSpawnTableSpawnAssetLocal(this AnimalTable spawnTable, ushort newSpawnAssetLegacyId)
    {
        ThreadUtil.assertIsGameThread();

        return SetSpawnTableSpawnAssetLocal(SpawnType.Animal, LevelAnimals.tables.IndexOf(spawnTable), newSpawnAssetLegacyId);
    }

    /// <summary>
    /// Locally set the spawn asset (<see cref="VehicleTable.tableID"/>) of a spawn table and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SetSpawnTableSpawnAssetLocal(this VehicleTable spawnTable, ushort newSpawnAssetLegacyId)
    {
        ThreadUtil.assertIsGameThread();

        return SetSpawnTableSpawnAssetLocal(SpawnType.Vehicle, LevelVehicles.tables.IndexOf(spawnTable), newSpawnAssetLegacyId);
    }

    /// <summary>
    /// Locally set the spawn asset (<see cref="ItemTable.tableID"/>) of a spawn table and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SetSpawnTableSpawnAssetLocal(this ItemTable spawnTable, ushort newSpawnAssetLegacyId)
    {
        ThreadUtil.assertIsGameThread();

        return SetSpawnTableSpawnAssetLocal(SpawnType.Item, LevelItems.tables.IndexOf(spawnTable), newSpawnAssetLegacyId);
    }

    /// <summary>
    /// Locally set the spawn asset (<see cref="ZombieTable.lootID"/>) of a spawn table and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SetSpawnTableSpawnAssetLocal(this ZombieTable spawnTable, ushort newSpawnAssetLegacyId)
    {
        ThreadUtil.assertIsGameThread();

        return SetSpawnTableSpawnAssetLocal(SpawnType.Zombie, LevelZombies.tables.IndexOf(spawnTable), newSpawnAssetLegacyId);
    }

    /// <summary>
    /// Locally set the spawn asset (<see cref="AnimalTable.tableID"/>) of a spawn table and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SetSpawnTableSpawnAssetLocal(this AnimalTable spawnTable, SpawnAsset newSpawnAsset)
    {
        ThreadUtil.assertIsGameThread();

        return SetSpawnTableSpawnAssetLocal(SpawnType.Animal, LevelAnimals.tables.IndexOf(spawnTable), newSpawnAsset);
    }

    /// <summary>
    /// Locally set the spawn asset (<see cref="VehicleTable.tableID"/>) of a spawn table and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SetSpawnTableSpawnAssetLocal(this VehicleTable spawnTable, SpawnAsset newSpawnAsset)
    {
        ThreadUtil.assertIsGameThread();

        return SetSpawnTableSpawnAssetLocal(SpawnType.Vehicle, LevelVehicles.tables.IndexOf(spawnTable), newSpawnAsset);
    }

    /// <summary>
    /// Locally set the spawn asset (<see cref="ItemTable.tableID"/>) of a spawn table and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SetSpawnTableSpawnAssetLocal(this ItemTable spawnTable, SpawnAsset newSpawnAsset)
    {
        ThreadUtil.assertIsGameThread();

        return SetSpawnTableSpawnAssetLocal(SpawnType.Item, LevelItems.tables.IndexOf(spawnTable), newSpawnAsset);
    }

    /// <summary>
    /// Locally set the spawn asset (<see cref="ZombieTable.lootID"/>) of a spawn table and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SetSpawnTableSpawnAssetLocal(this ZombieTable spawnTable, SpawnAsset newSpawnAsset)
    {
        ThreadUtil.assertIsGameThread();

        return SetSpawnTableSpawnAssetLocal(SpawnType.Zombie, LevelZombies.tables.IndexOf(spawnTable), newSpawnAsset);
    }

    /// <summary>
    /// Locally set the spawn asset (usually called 'tableID', except for <see cref="ZombieTable.lootID"/>) of a spawn table and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SetSpawnTableSpawnAssetLocal(SpawnType spawnType, int index, AssetReference<SpawnAsset> newSpawnAsset)
    {
        ThreadUtil.assertIsGameThread();

        if (Assets.find(newSpawnAsset) is not { } spawnAsset)
            return false;

        return SetSpawnTableSpawnAssetLocal(spawnType, index, spawnAsset);
    }

    /// <summary>
    /// Locally set the spawn asset (usually called 'tableID', except for <see cref="ZombieTable.lootID"/>) of a spawn table and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SetSpawnTableSpawnAssetLocal(SpawnType spawnType, int index, ushort newSpawnAssetLegacyId)
    {
        ThreadUtil.assertIsGameThread();

        if (Assets.find(EAssetType.SPAWN, newSpawnAssetLegacyId) is not SpawnAsset spawnAsset)
            return false;

        return SetSpawnTableSpawnAssetLocal(spawnType, index, spawnAsset);
    }

    /// <summary>
    /// Locally set the spawn asset (usually called 'tableID', except for <see cref="ZombieTable.lootID"/>) of a spawn table and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SetSpawnTableSpawnAssetLocal(SpawnType spawnType, int index, SpawnAsset newSpawnAsset)
    {
        ThreadUtil.assertIsGameThread();

        if (!CheckSpawnTableSafe(spawnType, index))
            return false;

        ushort id = newSpawnAsset.id;

        switch (spawnType)
        {
            case SpawnType.Animal:
                AnimalTable animalTable = LevelAnimals.tables[index];
                animalTable.tableID = id;
                
                EventOnAnimalSpawnTableSpawnAssetUpdated.TryInvoke(animalTable, index);
                break;
            
            case SpawnType.Vehicle:
                VehicleTable vehicleTable = LevelVehicles.tables[index];
                vehicleTable.tableID = id;

                EventOnVehicleSpawnTableSpawnAssetUpdated.TryInvoke(vehicleTable, index);
                break;
            
            case SpawnType.Item:
                ItemTable itemTable = LevelItems.tables[index];
                itemTable.tableID = id;

                EventOnItemSpawnTableSpawnAssetUpdated.TryInvoke(itemTable, index);
                break;
            
            case SpawnType.Zombie:
                ZombieTable zombieTable = LevelZombies.tables[index];
                zombieTable.lootID = id;

                EventOnZombieSpawnTableSpawnAssetUpdated.TryInvoke(zombieTable, index);
                break;
        }

        return true;
    }

    /// <summary>
    /// Locally set <see cref="ZombieTable.isMega"/> of a spawn table and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SetZombieSpawnTableIsMegaLocal(this ZombieTable spawnTable, bool isMega)
    {
        ThreadUtil.assertIsGameThread();

        return SetZombieSpawnTableIsMegaLocal(LevelZombies.tables.IndexOf(spawnTable), isMega);
    }

    /// <summary>
    /// Locally set <see cref="ZombieTable.isMega"/> of a spawn table and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SetZombieSpawnTableIsMegaLocal(int index, bool isMega)
    {
        ThreadUtil.assertIsGameThread();

        if (!CheckSpawnTableSafe(SpawnType.Zombie, index))
            return false;

        ZombieTable zombieTable = LevelZombies.tables[index];
        zombieTable.isMega = isMega;

        EventOnZombieSpawnTableIsMegaUpdated.TryInvoke(zombieTable, index);
        return true;
    }

    /// <summary>
    /// Locally set <see cref="ZombieTable.health"/> of a spawn table and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SetZombieSpawnTableHealthLocal(this ZombieTable spawnTable, ushort health)
    {
        ThreadUtil.assertIsGameThread();

        return SetZombieSpawnTableHealthLocal(LevelZombies.tables.IndexOf(spawnTable), health);
    }

    /// <summary>
    /// Locally set <see cref="ZombieTable.health"/> of a spawn table and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SetZombieSpawnTableHealthLocal(int index, ushort health)
    {
        ThreadUtil.assertIsGameThread();

        if (!CheckSpawnTableSafe(SpawnType.Zombie, index))
            return false;

        ZombieTable zombieTable = LevelZombies.tables[index];
        zombieTable.health = health;

        EventOnZombieSpawnTableHealthUpdated.TryInvoke(zombieTable, index);
        return true;
    }

    /// <summary>
    /// Locally set <see cref="ZombieTable.damage"/> of a spawn table and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SetZombieSpawnTableDamageLocal(this ZombieTable spawnTable, byte damage)
    {
        ThreadUtil.assertIsGameThread();

        return SetZombieSpawnTableDamageLocal(LevelZombies.tables.IndexOf(spawnTable), damage);
    }

    /// <summary>
    /// Locally set <see cref="ZombieTable.damage"/> of a spawn table and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SetZombieSpawnTableDamageLocal(int index, byte damage)
    {
        ThreadUtil.assertIsGameThread();

        if (!CheckSpawnTableSafe(SpawnType.Zombie, index))
            return false;

        ZombieTable zombieTable = LevelZombies.tables[index];
        zombieTable.damage = damage;

        EventOnZombieSpawnTableDamageUpdated.TryInvoke(zombieTable, index);
        return true;
    }

    /// <summary>
    /// Locally set <see cref="ZombieTable.lootIndex"/> of a spawn table and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SetZombieSpawnTableLootIndexLocal(this ZombieTable spawnTable, ItemTable? lootTable)
    {
        if (lootTable == null)
            return SetZombieSpawnTableLootIndexLocal(LevelZombies.tables.IndexOf(spawnTable), byte.MaxValue);

        ThreadUtil.assertIsGameThread();

        int index = LevelItems.tables.IndexOf(lootTable);
        return index is >= 0 and < byte.MaxValue && SetZombieSpawnTableLootIndexLocal(LevelZombies.tables.IndexOf(spawnTable), (byte)index);
    }

    /// <summary>
    /// Locally set <see cref="ZombieTable.lootIndex"/> of a spawn table and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SetZombieSpawnTableLootIndexLocal(this ZombieTable spawnTable, byte lootIndex)
    {
        ThreadUtil.assertIsGameThread();

        return SetZombieSpawnTableLootIndexLocal(LevelZombies.tables.IndexOf(spawnTable), lootIndex);
    }

    /// <summary>
    /// Locally set <see cref="ZombieTable.lootIndex"/> of a spawn table and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SetZombieSpawnTableLootIndexLocal(int index, byte lootIndex)
    {
        ThreadUtil.assertIsGameThread();

        if (!CheckSpawnTableSafe(SpawnType.Zombie, index))
            return false;

        ZombieTable zombieTable = LevelZombies.tables[index];
        zombieTable.lootIndex = lootIndex;

        EventOnZombieSpawnTableLootIndexUpdated.TryInvoke(zombieTable, index);
        return true;
    }

    /// <summary>
    /// Locally set <see cref="ZombieTable.xp"/> of a spawn table and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SetZombieSpawnTableXPLocal(this ZombieTable spawnTable, uint xp)
    {
        ThreadUtil.assertIsGameThread();

        return SetZombieSpawnTableXPLocal(LevelZombies.tables.IndexOf(spawnTable), xp);
    }

    /// <summary>
    /// Locally set <see cref="ZombieTable.xp"/> of a spawn table and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SetZombieSpawnTableXPLocal(int index, uint xp)
    {
        ThreadUtil.assertIsGameThread();

        if (!CheckSpawnTableSafe(SpawnType.Zombie, index))
            return false;

        ZombieTable zombieTable = LevelZombies.tables[index];
        zombieTable.xp = xp;

        EventOnZombieSpawnTableXPUpdated.TryInvoke(zombieTable, index);
        return true;
    }

    /// <summary>
    /// Locally set <see cref="ZombieTable.regen"/> of a spawn table and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SetZombieSpawnTableRegenLocal(this ZombieTable spawnTable, float regen)
    {
        ThreadUtil.assertIsGameThread();

        return SetZombieSpawnTableRegenLocal(LevelZombies.tables.IndexOf(spawnTable), regen);
    }

    /// <summary>
    /// Locally set <see cref="ZombieTable.regen"/> of a spawn table and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SetZombieSpawnTableRegenLocal(int index, float regen)
    {
        ThreadUtil.assertIsGameThread();

        if (!CheckSpawnTableSafe(SpawnType.Zombie, index))
            return false;

        ZombieTable zombieTable = LevelZombies.tables[index];
        zombieTable.regen = regen;

        EventOnZombieSpawnTableRegenUpdated.TryInvoke(zombieTable, index);
        return true;
    }

    /// <summary>
    /// Locally set <see cref="ZombieTable.difficultyGUID"/> of a spawn table and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SetZombieSpawnTableDifficultyAssetLocal(this ZombieTable spawnTable, AssetReference<ZombieDifficultyAsset> regen)
    {
        ThreadUtil.assertIsGameThread();

        return SetZombieSpawnTableDifficultyAssetLocal(LevelZombies.tables.IndexOf(spawnTable), regen);
    }

    /// <summary>
    /// Locally set <see cref="ZombieTable.difficultyGUID"/> of a spawn table and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SetZombieSpawnTableDifficultyAssetLocal(int index, AssetReference<ZombieDifficultyAsset> regen)
    {
        ThreadUtil.assertIsGameThread();

        if (!CheckSpawnTableSafe(SpawnType.Zombie, index))
            return false;

        ZombieTable zombieTable = LevelZombies.tables[index];
        zombieTable.difficultyGUID = regen.GUID.ToString("N");

        EventOnZombieSpawnTableDifficultyAssetUpdated.TryInvoke(zombieTable, index);
        zombieTable.ResetCachedZombieDifficultyAsset();
        return true;
    }

    /// <summary>
    /// Reset <see cref="ZombieTable.cachedDifficulty"/> after changing the value of <see cref="ZombieTable.difficultyGUID"/>.
    /// </summary>
    /// <returns><see langword="false"/> in the case of a reflection failure, otherwise <see langword="true"/>.</returns>
    public static bool ResetCachedZombieDifficultyAsset(this ZombieTable spawnTable)
    {
        if (SetCachedDifficultyAsset == null)
            return false;

        SetCachedDifficultyAsset(spawnTable, null);
        return true;
    }

    /// <summary>
    /// Checks to make sure <paramref name="index"/> is in range of the corresponding internal table list.
    /// </summary>
    public static bool CheckSpawnTableSafe(SpawnType spawnType, int index)
    {
        return index >= 0 && spawnType switch
        {
            SpawnType.Animal => LevelAnimals.tables.Count > index,
            SpawnType.Vehicle => LevelVehicles.tables.Count > index,
            SpawnType.Item => LevelItems.tables.Count > index,
            SpawnType.Zombie => LevelZombies.tables.Count > index,
            _ => false
        };
    }

    /// <summary>
    /// Checks to make sure <paramref name="tierIdentifier"/> is in range of the corresponding internal table lists.
    /// </summary>
    public static bool CheckSpawnTierSafe(SpawnTierIdentifier tierIdentifier)
    {
        int tableInd = tierIdentifier.TableIndex;
        int tierInd = tierIdentifier.TierIndex;
        return tierIdentifier.Type switch
        {
            SpawnType.Animal  => LevelAnimals.tables.Count > tableInd
                                 && LevelAnimals.tables[tableInd].tiers.Count > tierInd,

            SpawnType.Vehicle => LevelVehicles.tables.Count > tableInd
                                 && LevelVehicles.tables[tableInd].tiers.Count > tierInd,

            SpawnType.Item    => LevelItems.tables.Count > tableInd
                                 && LevelItems.tables[tableInd].tiers.Count > tierInd,

            SpawnType.Zombie  => LevelZombies.tables.Count > tableInd
                                 && LevelZombies.tables[tableInd].slots.Length > tierInd,

            _ => false
        };
    }

    /// <summary>
    /// Checks to make sure <paramref name="assetIdentifier"/> is in range of the corresponding internal table lists.
    /// </summary>
    public static bool CheckSpawnAssetSafe(SpawnAssetIdentifier assetIdentifier)
    {
        int tableInd = assetIdentifier.TableIndex;
        int tierInd = assetIdentifier.TierIndex;
        int assetInd = assetIdentifier.AssetIndex;
        return assetIdentifier.Type switch
        {
            SpawnType.Animal  => LevelAnimals.tables.Count > tableInd
                                 && LevelAnimals.tables[tableInd] is { } tier
                                 && tier.tiers.Count > tierInd
                                 && tier.tiers[tierInd].table.Count > assetInd,

            SpawnType.Vehicle => LevelVehicles.tables.Count > tableInd
                                 && LevelVehicles.tables[tableInd] is { } tier
                                 && tier.tiers.Count > tierInd
                                 && tier.tiers[tierInd].table.Count > assetInd,

            SpawnType.Item    => LevelItems.tables.Count > tableInd
                                 && LevelItems.tables[tableInd] is { } tier
                                 && tier.tiers.Count > tierInd
                                 && tier.tiers[tierInd].table.Count > assetInd,

            SpawnType.Zombie  => LevelZombies.tables.Count > tableInd
                                 && LevelZombies.tables[tableInd] is { } tier
                                 && tier.slots.Length > tierInd
                                 && tier.slots[tierInd].table.Count > assetInd,

            _ => false
        };
    }
}