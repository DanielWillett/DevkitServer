using DevkitServer.API;
using DevkitServer.API.Devkit.Spawns;
using DevkitServer.Models;
using DevkitServer.Util.Region;
#if CLIENT
using DevkitServer.API.Permissions;
using DevkitServer.API.UI.Extensions;
using DevkitServer.Core.Permissions;
using DevkitServer.Core.UI.Extensions;
using DevkitServer.Multiplayer.Actions;
using DevkitServer.Multiplayer.Levels;
using SDG.Framework.Devkit;
using SDG.Framework.Utilities;
#endif

namespace DevkitServer.Util;

public delegate void AnimalSpawnTableArgs(AnimalTable spawnTable, int index);
public delegate void VehicleSpawnTableArgs(VehicleTable spawnTable, int index);
public delegate void ItemSpawnTableArgs(ItemTable spawnTable, int index);
public delegate void ZombieSpawnTableArgs(ZombieTable spawnTable, int index);

public delegate void AnimalSpawnTierArgs(AnimalTier spawnTier, SpawnTierIdentifier identifier, HierarchicalEventSource eventSource);
public delegate void VehicleSpawnTierArgs(VehicleTier spawnTier, SpawnTierIdentifier identifier, HierarchicalEventSource eventSource);
public delegate void ItemSpawnTierArgs(ItemTier spawnTier, SpawnTierIdentifier identifier, HierarchicalEventSource eventSource);
public delegate void ZombieSpawnTierArgs(ZombieSlot spawnTier, SpawnTierIdentifier identifier, HierarchicalEventSource eventSource);

public delegate void AnimalSpawnAssetArgs(AnimalSpawn spawnAsset, SpawnAssetIdentifier identifier, HierarchicalEventSource eventSource);
public delegate void VehicleSpawnAssetArgs(VehicleSpawn spawnAsset, SpawnAssetIdentifier identifier, HierarchicalEventSource eventSource);
public delegate void ItemSpawnAssetArgs(ItemSpawn spawnAsset, SpawnAssetIdentifier identifier, HierarchicalEventSource eventSource);
public delegate void ZombieSpawnAssetArgs(ZombieCloth spawnAsset, SpawnAssetIdentifier identifier, HierarchicalEventSource eventSource);

public delegate void AnimalSpawnTableIndexUpdated(AnimalTable spawnTable, int fromIndex, int toIndex);
public delegate void VehicleSpawnTableIndexUpdated(VehicleTable spawnTable, int fromIndex, int toIndex);
public delegate void ItemSpawnTableIndexUpdated(ItemTable spawnTable, int fromIndex, int toIndex);
public delegate void ZombieSpawnTableIndexUpdated(ZombieTable spawnTable, int fromIndex, int toIndex);

public delegate void AnimalSpawnTierIndexUpdated(AnimalTier spawnTier, SpawnTierIdentifier fromIndex, SpawnTierIdentifier toIndex, HierarchicalEventSource eventSource);
public delegate void VehicleSpawnTierIndexUpdated(VehicleTier spawnTier, SpawnTierIdentifier fromIndex, SpawnTierIdentifier toIndex, HierarchicalEventSource eventSource);
public delegate void ItemSpawnTierIndexUpdated(ItemTier spawnTier, SpawnTierIdentifier fromIndex, SpawnTierIdentifier toIndex, HierarchicalEventSource eventSource);
public delegate void ZombieSpawnTierIndexUpdated(ZombieSlot spawnTier, SpawnTierIdentifier fromIndex, SpawnTierIdentifier toIndex, HierarchicalEventSource eventSource);

public delegate void AnimalSpawnAssetIndexUpdated(AnimalSpawn spawnAsset, SpawnAssetIdentifier fromIndex, SpawnAssetIdentifier toIndex, HierarchicalEventSource eventSource);
public delegate void VehicleSpawnAssetIndexUpdated(VehicleSpawn spawnAsset, SpawnAssetIdentifier fromIndex, SpawnAssetIdentifier toIndex, HierarchicalEventSource eventSource);
public delegate void ItemSpawnAssetIndexUpdated(ItemSpawn spawnAsset, SpawnAssetIdentifier fromIndex, SpawnAssetIdentifier toIndex, HierarchicalEventSource eventSource);
public delegate void ZombieSpawnAssetIndexUpdated(ZombieCloth spawnAsset, SpawnAssetIdentifier fromIndex, SpawnAssetIdentifier toIndex, HierarchicalEventSource eventSource);

[EarlyTypeInit]
public static class SpawnTableUtil
{
    private static readonly InstanceSetter<ZombieTable, ZombieDifficultyAsset?>? SetCachedDifficultyAsset
        = Accessor.GenerateInstanceSetter<ZombieTable, ZombieDifficultyAsset?>("cachedDifficulty", throwOnError: false);

#if CLIENT
    private static readonly StaticSetter<byte>? SetSelectedAnimalTier
        = Accessor.GenerateStaticSetter<EditorSpawnsAnimalsUI, byte>("selectedTier", throwOnError: false);
    private static readonly StaticSetter<byte>? SetSelectedAnimalSpawn
        = Accessor.GenerateStaticSetter<EditorSpawnsAnimalsUI, byte>("selectAnimal", throwOnError: false);
    
    private static readonly StaticGetter<byte>? GetSelectedAnimalTier
        = Accessor.GenerateStaticGetter<EditorSpawnsAnimalsUI, byte>("selectedTier", throwOnError: false);
    private static readonly StaticGetter<byte>? GetSelectedAnimalSpawn
        = Accessor.GenerateStaticGetter<EditorSpawnsAnimalsUI, byte>("selectAnimal", throwOnError: false);

    private static readonly StaticSetter<byte>? SetSelectedVehicleTier
        = Accessor.GenerateStaticSetter<EditorSpawnsVehiclesUI, byte>("selectedTier", throwOnError: false);
    private static readonly StaticSetter<byte>? SetSelectedVehicleSpawn
        = Accessor.GenerateStaticSetter<EditorSpawnsVehiclesUI, byte>("selectVehicle", throwOnError: false);
    
    private static readonly StaticGetter<byte>? GetSelectedVehicleTier
        = Accessor.GenerateStaticGetter<EditorSpawnsVehiclesUI, byte>("selectedTier", throwOnError: false);
    private static readonly StaticGetter<byte>? GetSelectedVehicleSpawn
        = Accessor.GenerateStaticGetter<EditorSpawnsVehiclesUI, byte>("selectVehicle", throwOnError: false);

    private static readonly StaticSetter<byte>? SetSelectedItemTier
        = Accessor.GenerateStaticSetter<EditorSpawnsItemsUI, byte>("selectedTier", throwOnError: false);
    private static readonly StaticSetter<byte>? SetSelectedItemSpawn
        = Accessor.GenerateStaticSetter<EditorSpawnsItemsUI, byte>("selectItem", throwOnError: false);

    private static readonly StaticGetter<byte>? GetSelectedItemTier
        = Accessor.GenerateStaticGetter<EditorSpawnsItemsUI, byte>("selectedTier", throwOnError: false);
    private static readonly StaticGetter<byte>? GetSelectedItemSpawn
        = Accessor.GenerateStaticGetter<EditorSpawnsItemsUI, byte>("selectItem", throwOnError: false);

    private static readonly StaticSetter<byte>? SetSelectedZombieTier
        = Accessor.GenerateStaticSetter<EditorSpawnsZombiesUI, byte>("selectedSlot", throwOnError: false);
    private static readonly StaticSetter<byte>? SetSelectedZombieSpawn
        = Accessor.GenerateStaticSetter<EditorSpawnsZombiesUI, byte>("selectItem", throwOnError: false);

    private static readonly StaticGetter<byte>? GetSelectedZombieTier
        = Accessor.GenerateStaticGetter<EditorSpawnsZombiesUI, byte>("selectedSlot", throwOnError: false);
    private static readonly StaticGetter<byte>? GetSelectedZombieSpawn
        = Accessor.GenerateStaticGetter<EditorSpawnsZombiesUI, byte>("selectItem", throwOnError: false);
#endif

    internal static readonly CachedMulticastEvent<AnimalSpawnTableArgs> EventOnAnimalSpawnTableNameUpdated = new CachedMulticastEvent<AnimalSpawnTableArgs>(typeof(SpawnTableUtil), nameof(OnAnimalSpawnTableNameUpdated));
    internal static readonly CachedMulticastEvent<VehicleSpawnTableArgs> EventOnVehicleSpawnTableNameUpdated = new CachedMulticastEvent<VehicleSpawnTableArgs>(typeof(SpawnTableUtil), nameof(OnVehicleSpawnTableNameUpdated));
    internal static readonly CachedMulticastEvent<ItemSpawnTableArgs> EventOnItemSpawnTableNameUpdated = new CachedMulticastEvent<ItemSpawnTableArgs>(typeof(SpawnTableUtil), nameof(OnItemSpawnTableNameUpdated));
    internal static readonly CachedMulticastEvent<ZombieSpawnTableArgs> EventOnZombieSpawnTableNameUpdated = new CachedMulticastEvent<ZombieSpawnTableArgs>(typeof(SpawnTableUtil), nameof(OnZombieSpawnTableNameUpdated));
    
    internal static readonly CachedMulticastEvent<AnimalSpawnTierArgs> EventOnAnimalSpawnTierNameUpdated = new CachedMulticastEvent<AnimalSpawnTierArgs>(typeof(SpawnTableUtil), nameof(OnAnimalSpawnTierNameUpdated));
    internal static readonly CachedMulticastEvent<VehicleSpawnTierArgs> EventOnVehicleSpawnTierNameUpdated = new CachedMulticastEvent<VehicleSpawnTierArgs>(typeof(SpawnTableUtil), nameof(OnVehicleSpawnTierNameUpdated));
    internal static readonly CachedMulticastEvent<ItemSpawnTierArgs> EventOnItemSpawnTierNameUpdated = new CachedMulticastEvent<ItemSpawnTierArgs>(typeof(SpawnTableUtil), nameof(OnItemSpawnTierNameUpdated));
    
    internal static readonly CachedMulticastEvent<AnimalSpawnTierArgs> EventOnAnimalSpawnTierChanceUpdated = new CachedMulticastEvent<AnimalSpawnTierArgs>(typeof(SpawnTableUtil), nameof(OnAnimalSpawnTierChanceUpdated));
    internal static readonly CachedMulticastEvent<VehicleSpawnTierArgs> EventOnVehicleSpawnTierChanceUpdated = new CachedMulticastEvent<VehicleSpawnTierArgs>(typeof(SpawnTableUtil), nameof(OnVehicleSpawnTierChanceUpdated));
    internal static readonly CachedMulticastEvent<ItemSpawnTierArgs> EventOnItemSpawnTierChanceUpdated = new CachedMulticastEvent<ItemSpawnTierArgs>(typeof(SpawnTableUtil), nameof(OnItemSpawnTierChanceUpdated));
    internal static readonly CachedMulticastEvent<ZombieSpawnTierArgs> EventOnZombieSpawnTierChanceUpdated = new CachedMulticastEvent<ZombieSpawnTierArgs>(typeof(SpawnTableUtil), nameof(OnZombieSpawnTierChanceUpdated));
    
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
    
    internal static readonly CachedMulticastEvent<AnimalSpawnAssetArgs> EventOnAnimalSpawnAssetUpdated = new CachedMulticastEvent<AnimalSpawnAssetArgs>(typeof(SpawnTableUtil), nameof(OnAnimalSpawnAssetUpdated));
    internal static readonly CachedMulticastEvent<VehicleSpawnAssetArgs> EventOnVehicleSpawnAssetUpdated = new CachedMulticastEvent<VehicleSpawnAssetArgs>(typeof(SpawnTableUtil), nameof(OnVehicleSpawnAssetUpdated));
    internal static readonly CachedMulticastEvent<ItemSpawnAssetArgs> EventOnItemSpawnAssetUpdated = new CachedMulticastEvent<ItemSpawnAssetArgs>(typeof(SpawnTableUtil), nameof(OnItemSpawnAssetUpdated));
    internal static readonly CachedMulticastEvent<ZombieSpawnAssetArgs> EventOnZombieSpawnAssetUpdated = new CachedMulticastEvent<ZombieSpawnAssetArgs>(typeof(SpawnTableUtil), nameof(OnZombieSpawnAssetUpdated));
    
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
    
    public static event AnimalSpawnTierArgs OnAnimalSpawnTierNameUpdated
    {
        add => EventOnAnimalSpawnTierNameUpdated.Add(value);
        remove => EventOnAnimalSpawnTierNameUpdated.Remove(value);
    }
    public static event VehicleSpawnTierArgs OnVehicleSpawnTierNameUpdated
    {
        add => EventOnVehicleSpawnTierNameUpdated.Add(value);
        remove => EventOnVehicleSpawnTierNameUpdated.Remove(value);
    }
    public static event ItemSpawnTierArgs OnItemSpawnTierNameUpdated
    {
        add => EventOnItemSpawnTierNameUpdated.Add(value);
        remove => EventOnItemSpawnTierNameUpdated.Remove(value);
    }
    
    public static event AnimalSpawnTierArgs OnAnimalSpawnTierChanceUpdated
    {
        add => EventOnAnimalSpawnTierChanceUpdated.Add(value);
        remove => EventOnAnimalSpawnTierChanceUpdated.Remove(value);
    }
    public static event VehicleSpawnTierArgs OnVehicleSpawnTierChanceUpdated
    {
        add => EventOnVehicleSpawnTierChanceUpdated.Add(value);
        remove => EventOnVehicleSpawnTierChanceUpdated.Remove(value);
    }
    public static event ItemSpawnTierArgs OnItemSpawnTierChanceUpdated
    {
        add => EventOnItemSpawnTierChanceUpdated.Add(value);
        remove => EventOnItemSpawnTierChanceUpdated.Remove(value);
    }
    public static event ZombieSpawnTierArgs OnZombieSpawnTierChanceUpdated
    {
        add => EventOnZombieSpawnTierChanceUpdated.Add(value);
        remove => EventOnZombieSpawnTierChanceUpdated.Remove(value);
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
    
    public static event AnimalSpawnAssetArgs OnAnimalSpawnAssetUpdated
    {
        add => EventOnAnimalSpawnAssetUpdated.Add(value);
        remove => EventOnAnimalSpawnAssetUpdated.Remove(value);
    }
    public static event VehicleSpawnAssetArgs OnVehicleSpawnAssetUpdated
    {
        add => EventOnVehicleSpawnAssetUpdated.Add(value);
        remove => EventOnVehicleSpawnAssetUpdated.Remove(value);
    }
    public static event ItemSpawnAssetArgs OnItemSpawnAssetUpdated
    {
        add => EventOnItemSpawnAssetUpdated.Add(value);
        remove => EventOnItemSpawnAssetUpdated.Remove(value);
    }
    public static event ZombieSpawnAssetArgs OnZombieSpawnAssetUpdated
    {
        add => EventOnZombieSpawnAssetUpdated.Add(value);
        remove => EventOnZombieSpawnAssetUpdated.Remove(value);
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
    /// Gets the currently slected table in the spawn UI. <paramref name="index"/> will be -1 if nothing is selected but will still return <see langword="true"/>.
    /// </summary>
    /// <returns><see langword="false"/> in the case of an invalid spawn type, otherwise <see langword="true"/>.</returns>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool TryGetSelectedTable(SpawnType spawnType, out int index)
    {
        ThreadUtil.assertIsGameThread();

        if (spawnType is not SpawnType.Animal and not SpawnType.Vehicle and not SpawnType.Item and not SpawnType.Zombie)
        {
            index = -1;
            return false;
        }

        byte selectedTable = spawnType switch
        {
            SpawnType.Animal => EditorSpawns.selectedAnimal,
            SpawnType.Vehicle => EditorSpawns.selectedVehicle,
            SpawnType.Item => EditorSpawns.selectedItem,
            _ => EditorSpawns.selectedZombie
        };

        index = selectedTable == byte.MaxValue ? -1 : selectedTable;
        return true;
    }

    /// <summary>
    /// Gets the currently slected table in the spawn UI. <paramref name="index"/> will be  but will still return <see langword="true"/>.
    /// </summary>
    /// <returns>-1 if nothing is selected or an invalid spawn type is given, otherwise the table index.</returns>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static int GetSelectedTable(SpawnType spawnType)
    {
        ThreadUtil.assertIsGameThread();

        if (spawnType is not SpawnType.Animal and not SpawnType.Vehicle and not SpawnType.Item and not SpawnType.Zombie)
            return -1;

        byte selectedTable = spawnType switch
        {
            SpawnType.Animal => EditorSpawns.selectedAnimal,
            SpawnType.Vehicle => EditorSpawns.selectedVehicle,
            SpawnType.Item => EditorSpawns.selectedItem,
            _ => EditorSpawns.selectedZombie
        };

        return selectedTable == byte.MaxValue ? -1 : selectedTable;
    }
    
    /// <summary>
    /// Gets the currently slected tier in the spawn UI. <paramref name="identifier"/> will be <see langword="null"/> if nothing is selected but will still return <see langword="true"/>.
    /// </summary>
    /// <returns><see langword="false"/> in the case of a reflection failure or invalid spawn type, otherwise <see langword="true"/>.</returns>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool TryGetSelectedTier(SpawnType spawnType, out SpawnTierIdentifier? identifier)
    {
        ThreadUtil.assertIsGameThread();

        if (spawnType is not SpawnType.Animal and not SpawnType.Vehicle and not SpawnType.Item and not SpawnType.Zombie)
        {
            identifier = null;
            return false;
        }

        StaticGetter<byte>? getter = spawnType switch
        {
            SpawnType.Animal => GetSelectedAnimalTier,
            SpawnType.Vehicle => GetSelectedVehicleTier,
            SpawnType.Item => GetSelectedItemTier,
            _ => GetSelectedZombieTier
        };

        if (getter == null)
        {
            identifier = null;
            return false;
        }

        byte selected = getter();
        byte selectedTable = spawnType switch
        {
            SpawnType.Animal => EditorSpawns.selectedAnimal,
            SpawnType.Vehicle => EditorSpawns.selectedVehicle,
            SpawnType.Item => EditorSpawns.selectedItem,
            _ => EditorSpawns.selectedZombie
        };

        if (selected == byte.MaxValue || selectedTable == byte.MaxValue)
        {
            identifier = null;
            return true;
        }

        SpawnTierIdentifier spawnTierIdentifier = new SpawnTierIdentifier(spawnType, selectedTable, selected);
        identifier = spawnTierIdentifier;
        return spawnTierIdentifier.CheckSafe();
    }

    /// <summary>
    /// Gets the currently slected tier asset in the spawn UI. <paramref name="index"/> will be <see langword="null"/> if nothing is selected but will still return <see langword="true"/>.
    /// </summary>
    /// <returns><see langword="false"/> in the case of a reflection failure or invalid spawn type, otherwise <see langword="true"/>.</returns>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool TryGetSelectedTierAsset(SpawnType spawnType, out SpawnAssetIdentifier? identifier)
    {
        ThreadUtil.assertIsGameThread();

        if (spawnType is not SpawnType.Animal and not SpawnType.Vehicle and not SpawnType.Item and not SpawnType.Zombie)
        {
            identifier = null;
            return false;
        }

        StaticGetter<byte>? getter = spawnType switch
        {
            SpawnType.Animal => GetSelectedAnimalSpawn,
            SpawnType.Vehicle => GetSelectedVehicleSpawn,
            SpawnType.Item => GetSelectedItemSpawn,
            _ => GetSelectedZombieSpawn
        };
        StaticGetter<byte>? tierGetter = spawnType switch
        {
            SpawnType.Animal => GetSelectedAnimalTier,
            SpawnType.Vehicle => GetSelectedVehicleTier,
            SpawnType.Item => GetSelectedItemTier,
            _ => GetSelectedZombieTier
        };
        if (getter == null || tierGetter == null)
        {
            identifier = null;
            return false;
        }

        byte selected = getter();
        byte selectedTier = tierGetter();
        byte selectedTable = spawnType switch
        {
            SpawnType.Animal => EditorSpawns.selectedAnimal,
            SpawnType.Vehicle => EditorSpawns.selectedVehicle,
            SpawnType.Item => EditorSpawns.selectedItem,
            _ => EditorSpawns.selectedZombie
        };
        if (selected == byte.MaxValue || selectedTier == byte.MaxValue || selectedTable == byte.MaxValue)
        {
            identifier = null;
            return true;
        }

        SpawnAssetIdentifier spawnAssetIdentifier = new SpawnAssetIdentifier(spawnType, selectedTable, selectedTier, selected);
        identifier = spawnAssetIdentifier;
        return spawnAssetIdentifier.CheckSafe();
    }

    /// <summary>
    /// Deselects the currently selected tier and asset (if any).
    /// </summary>
    public static bool DeselectTier(SpawnType spawnType)
    {
        if (spawnType is not SpawnType.Animal and not SpawnType.Vehicle and not SpawnType.Item and not SpawnType.Zombie)
            return false;

        if (TryGetSelectedTier(spawnType, out SpawnTierIdentifier? alreadySelected) && !alreadySelected.HasValue)
            return false;

        ResetUISelections(spawnType);
        UpdateUISelection(spawnType);
        return true;
    }

    /// <summary>
    /// Deselects the currently selected tier asset (if any).
    /// </summary>
    public static bool DeselectTierAsset(SpawnType spawnType)
    {
        if (spawnType is not SpawnType.Animal and not SpawnType.Vehicle and not SpawnType.Item and not SpawnType.Zombie)
            return false;

        if (TryGetSelectedTierAsset(spawnType, out SpawnAssetIdentifier? alreadySelected) && !alreadySelected.HasValue)
            return false;

        StaticSetter<byte>? assetSetter = spawnType switch
        {
            SpawnType.Animal => SetSelectedAnimalSpawn,
            SpawnType.Vehicle => SetSelectedVehicleSpawn,
            SpawnType.Item => SetSelectedItemSpawn,
            _ => SetSelectedZombieSpawn
        };
        assetSetter?.Invoke(byte.MaxValue);
        UpdateUISelection(spawnType);
        return true;
    }

    /// <summary>
    /// Change the currently selected tier and table and update the corresponding UI.
    /// </summary>
    /// <returns><see langword="false"/> in the case of a reflection failure or out of range table or tier, otherwise <see langword="true"/>.</returns>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool TrySelectTier(SpawnTierIdentifier identifier)
    {
        if (TryGetSelectedTier(identifier.Type, out SpawnTierIdentifier? alreadySelected) && alreadySelected.HasValue && alreadySelected.Value.Equals(identifier))
            return false;

        if (!identifier.CheckSafe())
            return false;

        SpawnType spawnType = identifier.Type;

        StaticSetter<byte>? setter = spawnType switch
        {
            SpawnType.Animal => SetSelectedAnimalTier,
            SpawnType.Vehicle => SetSelectedVehicleTier,
            SpawnType.Item => SetSelectedItemTier,
            _ => SetSelectedZombieTier
        };
        StaticSetter<byte>? assetSetter = spawnType switch
        {
            SpawnType.Animal => SetSelectedAnimalSpawn,
            SpawnType.Vehicle => SetSelectedVehicleSpawn,
            SpawnType.Item => SetSelectedItemSpawn,
            _ => SetSelectedZombieSpawn
        };

        if (setter == null)
            return false;

        setter.Invoke((byte)identifier.TierIndex);
        assetSetter?.Invoke(byte.MaxValue);

        SelectTableAndUpdateUI(spawnType, identifier.TableIndex);

        return true;
    }

    /// <summary>
    /// Change the currently selected tier asset, tier, and table and update the corresponding UI.
    /// </summary>
    /// <returns><see langword="false"/> in the case of a reflection failure or out of range table, tier, or tier asset, otherwise <see langword="true"/>.</returns>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool TrySelectTierAsset(SpawnAssetIdentifier identifier)
    {
        if (TryGetSelectedTierAsset(identifier.Type, out SpawnAssetIdentifier? alreadySelected) && alreadySelected.HasValue && alreadySelected.Value.Equals(identifier))
            return false;

        if (!identifier.CheckSafe())
            return false;

        SpawnType spawnType = identifier.Type;
        StaticSetter<byte>? setter = spawnType switch
        {
            SpawnType.Animal => SetSelectedAnimalSpawn,
            SpawnType.Vehicle => SetSelectedVehicleSpawn,
            SpawnType.Item => SetSelectedItemSpawn,
            _ => SetSelectedZombieSpawn
        };
        StaticSetter<byte>? tierSetter = spawnType switch
        {
            SpawnType.Animal => SetSelectedAnimalTier,
            SpawnType.Vehicle => SetSelectedVehicleTier,
            SpawnType.Item => SetSelectedItemTier,
            _ => SetSelectedZombieTier
        };

        if (setter == null || (tierSetter == null && (!alreadySelected.HasValue || alreadySelected.Value.TierIndex != identifier.TierIndex)))
            return false;

        setter.Invoke((byte)identifier.AssetIndex);
        if (!alreadySelected.HasValue || alreadySelected.Value.TierIndex != identifier.TierIndex)
            tierSetter!.Invoke((byte)identifier.TierIndex);

        SelectTableAndUpdateUI(spawnType, identifier.TableIndex);

        return true;
    }
    private static void SelectTableAndUpdateUI(SpawnType spawnType, int index)
    {
        switch (spawnType)
        {
            case SpawnType.Animal:
                if (EditorSpawns.selectedAnimal != index)
                {
                    EditorSpawns.selectedAnimal = (byte)index;
                    if (EditorSpawns.animalSpawn != null && EditorSpawns.animalSpawn.TryGetComponent(out Renderer renderer))
                        renderer.material.color = LevelAnimals.tables[index].color;
                }

                EditorSpawnsAnimalsUI.updateSelection();
                break;

            case SpawnType.Vehicle:
                if (EditorSpawns.selectedVehicle != index)
                {
                    EditorSpawns.selectedVehicle = (byte)index;
                    if (EditorSpawns.vehicleSpawn != null && EditorSpawns.vehicleSpawn.TryGetComponent(out Renderer renderer))
                        renderer.material.color = LevelVehicles.tables[index].color;
                }

                EditorSpawnsVehiclesUI.updateSelection();
                break;

            case SpawnType.Item:
                if (EditorSpawns.selectedItem != index)
                {
                    EditorSpawns.selectedItem = (byte)index;
                    if (EditorSpawns.itemSpawn != null && EditorSpawns.itemSpawn.TryGetComponent(out Renderer renderer))
                        renderer.material.color = LevelItems.tables[index].color;
                }

                EditorSpawnsItemsUI.updateSelection();
                break;

            case SpawnType.Zombie:
                if (EditorSpawns.selectedZombie != index)
                {
                    EditorSpawns.selectedZombie = (byte)index;
                    if (EditorSpawns.zombieSpawn != null && EditorSpawns.zombieSpawn.TryGetComponent(out Renderer renderer))
                        renderer.material.color = LevelZombies.tables[index].color;
                }

                EditorSpawnsZombiesUI.updateSelection();
                break;
        }
    }
    internal static void UpdateUISelection(SpawnType spawnType)
    {
        switch (spawnType)
        {
            case SpawnType.Animal:
                EditorSpawnsAnimalsUI.updateSelection();
                break;

            case SpawnType.Vehicle:
                EditorSpawnsVehiclesUI.updateSelection();
                break;

            case SpawnType.Item:
                EditorSpawnsItemsUI.updateSelection();
                break;

            case SpawnType.Zombie:
                EditorSpawnsZombiesUI.updateSelection();
                break;
        }
    }
    internal static void UpdateUITable(SpawnType spawnType)
    {
        switch (spawnType)
        {
            case SpawnType.Animal:
                EditorSpawnsAnimalsUI.updateTables();
                break;

            case SpawnType.Vehicle:
                EditorSpawnsVehiclesUI.updateTables();
                break;

            case SpawnType.Item:
                EditorSpawnsItemsUI.updateTables();
                break;

            case SpawnType.Zombie:
                EditorSpawnsZombiesUI.updateTables();
                break;
        }
    }
    private static void ResetUISelections(SpawnType spawnType)
    {
        StaticSetter<byte>? setter = spawnType switch
        {
            SpawnType.Animal => SetSelectedAnimalTier,
            SpawnType.Vehicle => SetSelectedVehicleTier,
            SpawnType.Item => SetSelectedItemTier,
            _ => SetSelectedZombieTier
        };
        StaticSetter<byte>? assetSetter = spawnType switch
        {
            SpawnType.Animal => SetSelectedAnimalSpawn,
            SpawnType.Vehicle => SetSelectedVehicleSpawn,
            SpawnType.Item => SetSelectedItemSpawn,
            _ => SetSelectedZombieSpawn
        };
        setter?.Invoke(byte.MaxValue);
        assetSetter?.Invoke(byte.MaxValue);
    }

    /// <summary>
    /// Set the spawn table at the given index as the selected table for new spawns.
    /// </summary>
    /// <param name="applyToSelected">Should all selected spawnpoints get set to this table?</param>
    /// <param name="replicateApplyToSelected">If <paramref name="applyToSelected"/> is <see langword="true"/>, should this be sent to the server (if applicable)?</param>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SelectTable(SpawnType spawnType, int index, bool applyToSelected, bool replicateApplyToSelected)
    {
        switch (spawnType)
        {
            default:
                return false;

            case SpawnType.Animal:
                SelectAnimalTable(index, applyToSelected, replicateApplyToSelected);
                break;

            case SpawnType.Vehicle:
                SelectVehicleTable(index, applyToSelected, replicateApplyToSelected);
                break;

            case SpawnType.Item:
                SelectItemTable(index, applyToSelected, replicateApplyToSelected);
                break;

            case SpawnType.Zombie:
                SelectZombieTable(index, applyToSelected, replicateApplyToSelected);
                break;
        }

        return true;
    }

    /// <summary>
    /// Deselect the current animal table, if any.
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static void DeselectAnimalTable()
    {
        ThreadUtil.assertIsGameThread();

        EditorSpawns.selectedAnimal = byte.MaxValue;
        ResetUISelections(SpawnType.Animal);
        EditorSpawnsAnimalsUI.updateSelection();
    }

    /// <summary>
    /// Set the given <see cref="AnimalTable"/> as the selected animal table for new spawns.
    /// </summary>
    /// <param name="applyToSelected">Should all selected <see cref="AnimalSpawnpoint"/>s get set to this table?</param>
    /// <param name="replicateApplyToSelected">If <paramref name="applyToSelected"/> is <see langword="true"/>, should this be sent to the server (if applicable)?</param>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SelectAnimalTable(this AnimalTable table, bool applyToSelected, bool replicateApplyToSelected) => SelectAnimalTable(LevelAnimals.tables.IndexOf(table), applyToSelected, replicateApplyToSelected);

    /// <summary>
    /// Set the <see cref="AnimalTable"/> at the given index as the selected animal table for new spawns.
    /// </summary>
    /// <param name="applyToSelected">Should all selected <see cref="AnimalSpawnpoint"/>s get set to this table?</param>
    /// <param name="replicateApplyToSelected">If <paramref name="applyToSelected"/> is <see langword="true"/>, should this be sent to the server (if applicable)?</param>
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

        if (index != EditorSpawns.selectedAnimal)
        {
            EditorSpawns.selectedAnimal = (byte)index;
            if (EditorSpawns.animalSpawn != null && EditorSpawns.animalSpawn.TryGetComponent(out Renderer renderer))
                renderer.material.color = LevelAnimals.tables[index].color;
            ResetUISelections(SpawnType.Animal);
            EditorSpawnsAnimalsUI.updateSelection();
        }

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

                Logger.DevkitServer.LogConditional(nameof(SelectAnimalTable), $"Spawn table updated for {node.Format()}.");
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
        ResetUISelections(SpawnType.Vehicle);
        EditorSpawnsVehiclesUI.updateSelection();
    }

    /// <summary>
    /// Set the given <see cref="VehicleTable"/> as the selected vehicle table for new spawns.
    /// </summary>
    /// <param name="applyToSelected">Should all selected <see cref="VehicleSpawnpoint"/>s get set to this table?</param>
    /// <param name="replicateApplyToSelected">If <paramref name="applyToSelected"/> is <see langword="true"/>, should this be sent to the server (if applicable)?</param>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SelectVehicleTable(this VehicleTable table, bool applyToSelected, bool replicateApplyToSelected) => SelectVehicleTable(LevelVehicles.tables.IndexOf(table), applyToSelected, replicateApplyToSelected);

    /// <summary>
    /// Set the <see cref="VehicleTable"/> at the given index as the selected vehicle table for new spawns.
    /// </summary>
    /// <param name="applyToSelected">Should all selected <see cref="VehicleSpawnpoint"/>s get set to this table?</param>
    /// <param name="replicateApplyToSelected">If <paramref name="applyToSelected"/> is <see langword="true"/>, should this be sent to the server (if applicable)?</param>
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

        if (index != EditorSpawns.selectedVehicle)
        {
            EditorSpawns.selectedVehicle = (byte)index;
            if (EditorSpawns.vehicleSpawn != null && EditorSpawns.vehicleSpawn.TryGetComponent(out Renderer renderer))
                renderer.material.color = LevelVehicles.tables[index].color;
            ResetUISelections(SpawnType.Vehicle);
            EditorSpawnsVehiclesUI.updateSelection();
        }

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

                Logger.DevkitServer.LogConditional(nameof(SelectVehicleTable), $"Spawn table updated for {node.Format()}.");
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
        ResetUISelections(SpawnType.Item);
        EditorSpawnsItemsUI.updateSelection();
    }

    /// <summary>
    /// Set the given <see cref="ItemTable"/> as the selected item table for new spawns.
    /// </summary>
    /// <param name="applyToSelected">Should all selected <see cref="ItemSpawnpoint"/>s get set to this table?</param>
    /// <param name="replicateApplyToSelected">If <paramref name="applyToSelected"/> is <see langword="true"/>, should this be sent to the server (if applicable)?</param>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SelectItemTable(this ItemTable table, bool applyToSelected, bool replicateApplyToSelected) => SelectItemTable(LevelItems.tables.IndexOf(table), applyToSelected, replicateApplyToSelected);

    /// <summary>
    /// Set the <see cref="ItemTable"/> at the given index as the selected item table for new spawns.
    /// </summary>
    /// <param name="applyToSelected">Should all selected <see cref="ItemSpawnpoint"/>s get set to this table?</param>
    /// <param name="replicateApplyToSelected">If <paramref name="applyToSelected"/> is <see langword="true"/>, should this be sent to the server (if applicable)?</param>
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

        if (index != EditorSpawns.selectedItem)
        {
            EditorSpawns.selectedItem = (byte)index;
            if (EditorSpawns.itemSpawn != null && EditorSpawns.itemSpawn.TryGetComponent(out Renderer renderer))
                renderer.material.color = LevelItems.tables[index].color;
            ResetUISelections(SpawnType.Item);
            EditorSpawnsItemsUI.updateSelection();
        }

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

                Logger.DevkitServer.LogConditional(nameof(SelectItemTable), $"Spawn table updated for {node.Format()}.");
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
        ResetUISelections(SpawnType.Zombie);
        EditorSpawnsZombiesUI.updateSelection();
    }

    /// <summary>
    /// Set the given <see cref="ZombieTable"/> as the selected zombie table for new spawns.
    /// </summary>
    /// <param name="applyToSelected">Should all selected <see cref="ZombieSpawnpoint"/>s get set to this table?</param>
    /// <param name="replicateApplyToSelected">If <paramref name="applyToSelected"/> is <see langword="true"/>, should this be sent to the server (if applicable)?</param>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SelectZombieTable(this ZombieTable table, bool applyToSelected, bool replicateApplyToSelected) => SelectZombieTable(LevelZombies.tables.IndexOf(table), applyToSelected, replicateApplyToSelected);

    /// <summary>
    /// Set the <see cref="ZombieTable"/> at the given index as the selected zombie table for new spawns.
    /// </summary>
    /// <param name="applyToSelected">Should all selected <see cref="ZombieSpawnpoint"/>s get set to this table?</param>
    /// <param name="replicateApplyToSelected">If <paramref name="applyToSelected"/> is <see langword="true"/>, should this be sent to the server (if applicable)?</param>
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

        if (index != EditorSpawns.selectedZombie)
        {
            EditorSpawns.selectedZombie = (byte)index;
            if (EditorSpawns.zombieSpawn != null && EditorSpawns.zombieSpawn.TryGetComponent(out Renderer renderer))
                renderer.material.color = LevelZombies.tables[index].color;
            ResetUISelections(SpawnType.Zombie);
            EditorSpawnsZombiesUI.updateSelection();
        }

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

                Logger.DevkitServer.LogConditional(nameof(SelectZombieTable), $"Spawn table updated for {node.Format()}.");
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
    /// <param name="index">Table index of the target table.</param>
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
                                EventOnAnimalSpawnAssetRemoved.TryInvoke(tier.table[j], assetId, HierarchicalEventSource.GrandParentObject);
#if DEBUG
                                Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Animal spawn asset removed: {assetId.Format()} (parent removed).");
#endif
                            }
                        }
                        
                        EventOnAnimalSpawnTierRemoved.TryInvoke(tier, id, HierarchicalEventSource.ParentObject);
#if DEBUG
                        Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Animal spawn tier removed: {tier.name.Format()} ({id.Format()}) (parent removed).");
#endif
                    }
                }
                
                EventOnAnimalSpawnTableRemoved.TryInvoke(animalTable, index);
                Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Animal spawn table removed: {animalTable.name.Format()} (# {index.Format()}).");
                hasAssetRem = !EventOnAnimalSpawnAssetIndexUpdated.IsEmpty;
                bool hasTierRem = !EventOnAnimalSpawnTierIndexUpdated.IsEmpty;
                if (EventOnAnimalSpawnTableIndexUpdated.IsEmpty && !hasTierRem && !hasAssetRem)
                    break;

                for (int i = index; i < LevelAnimals.tables.Count; ++i)
                {
                    AnimalTable table2 = LevelAnimals.tables[i];
                    EventOnAnimalSpawnTableIndexUpdated.TryInvoke(table2, i + 1, i);
#if DEBUG
                    Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Animal spawn table index updated: {table2.name.Format()} (# {(i + 1).Format()} -> {i.Format()}).");
#endif
                    if (!hasTierRem && !hasAssetRem)
                        continue;

                    maxCt = Math.Min(byte.MaxValue - 1, table2.tiers.Count);

                    for (int k = 0; k < maxCt; k++)
                    {
                        AnimalTier tier = table2.tiers[k];
                        SpawnTierIdentifier id = new SpawnTierIdentifier(spawnType, (byte)i, (byte)k);
                        SpawnTierIdentifier idOld = new SpawnTierIdentifier(spawnType, (byte)(i + 1), (byte)k);

                        EventOnAnimalSpawnTierIndexUpdated.TryInvoke(tier, idOld, id, HierarchicalEventSource.ParentObject);

                        if (hasAssetRem)
                        {
                            int raw = id.Raw;
                            int rawOld = idOld.Raw;
                            int maxAssetCt = Math.Min(byte.MaxValue - 1, tier.table.Count);

                            for (int j = 0; j < maxAssetCt; ++j)
                            {
                                SpawnAssetIdentifier assetId = new SpawnAssetIdentifier(raw | (j << 24));
                                SpawnAssetIdentifier assetOldId = new SpawnAssetIdentifier(rawOld | (j << 24));
                                EventOnAnimalSpawnAssetIndexUpdated.TryInvoke(tier.table[j], assetOldId, assetId, HierarchicalEventSource.GrandParentObject);
#if DEBUG
                                Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Animal spawn asset index updated: {assetOldId.Format()} -> {assetId.Format()} (parent updated).");
#endif
                            }
                        }
#if DEBUG
                        Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Animal spawn tier index updated: {idOld.Format()} -> {id.Format()} (parent updated).");
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
                                EventOnVehicleSpawnAssetRemoved.TryInvoke(tier.table[j], assetId, HierarchicalEventSource.GrandParentObject);
#if DEBUG
                                Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Vehicle spawn asset removed: {assetId.Format()} (parent removed).");
#endif
                            }
                        }

                        EventOnVehicleSpawnTierRemoved.TryInvoke(tier, id, HierarchicalEventSource.ParentObject);
#if DEBUG
                        Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Vehicle spawn tier removed: {tier.name.Format()} ({id.Format()}) (parent removed).");
#endif
                    }
                }

                EventOnVehicleSpawnTableRemoved.TryInvoke(vehicleTable, index);
                Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Vehicle spawn table removed: {vehicleTable.name.Format()} (# {index.Format()}).");
                hasAssetRem = !EventOnVehicleSpawnAssetIndexUpdated.IsEmpty;
                hasTierRem = !EventOnVehicleSpawnTierIndexUpdated.IsEmpty;
                if (EventOnVehicleSpawnTableIndexUpdated.IsEmpty && !hasTierRem && !hasAssetRem)
                    break;

                for (int i = index; i < LevelVehicles.tables.Count; ++i)
                {
                    VehicleTable table2 = LevelVehicles.tables[i];
                    EventOnVehicleSpawnTableIndexUpdated.TryInvoke(table2, i + 1, i);
#if DEBUG
                    Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Vehicle spawn table index updated: {table2.name.Format()} (# {(i + 1).Format()} -> {i.Format()}).");
#endif
                    if (!hasTierRem && !hasAssetRem)
                        continue;

                    maxCt = Math.Min(byte.MaxValue - 1, table2.tiers.Count);

                    for (int k = 0; k < maxCt; k++)
                    {
                        VehicleTier tier = table2.tiers[k];
                        SpawnTierIdentifier id = new SpawnTierIdentifier(spawnType, (byte)i, (byte)k);
                        SpawnTierIdentifier idOld = new SpawnTierIdentifier(spawnType, (byte)(i + 1), (byte)k);

                        EventOnVehicleSpawnTierIndexUpdated.TryInvoke(tier, idOld, id, HierarchicalEventSource.ParentObject);

                        if (hasAssetRem)
                        {
                            int raw = id.Raw;
                            int rawOld = idOld.Raw;
                            int maxAssetCt = Math.Min(byte.MaxValue - 1, tier.table.Count);

                            for (int j = 0; j < maxAssetCt; ++j)
                            {
                                SpawnAssetIdentifier assetId = new SpawnAssetIdentifier(raw | (j << 24));
                                SpawnAssetIdentifier assetOldId = new SpawnAssetIdentifier(rawOld | (j << 24));
                                EventOnVehicleSpawnAssetIndexUpdated.TryInvoke(tier.table[j], assetOldId, assetId, HierarchicalEventSource.GrandParentObject);
#if DEBUG
                                Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Vehicle spawn asset index updated: {assetOldId.Format()} -> {assetId.Format()} (parent updated).");
#endif
                            }
                        }
#if DEBUG
                        Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Vehicle spawn tier index updated: {idOld.Format()} -> {id.Format()} (parent updated).");
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
                                EventOnItemSpawnAssetRemoved.TryInvoke(tier.table[j], assetId, HierarchicalEventSource.GrandParentObject);
#if DEBUG
                                Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Item spawn asset removed: {assetId.Format()} (parent removed).");
#endif
                            }
                        }

                        EventOnItemSpawnTierRemoved.TryInvoke(tier, id, HierarchicalEventSource.ParentObject);
#if DEBUG
                        Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Item spawn tier removed: {tier.name.Format()} ({id.Format()}) (parent removed).");
#endif
                    }
                }

                EventOnItemSpawnTableRemoved.TryInvoke(itemTable, index);
                Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Item spawn table removed: {itemTable.name.Format()} (# {index.Format()}).");
                hasAssetRem = !EventOnItemSpawnAssetIndexUpdated.IsEmpty;
                hasTierRem = !EventOnItemSpawnTierIndexUpdated.IsEmpty;
                if (EventOnItemSpawnTableIndexUpdated.IsEmpty && !hasTierRem && !hasAssetRem)
                    break;

                for (int i = index; i < LevelItems.tables.Count; ++i)
                {
                    ItemTable table2 = LevelItems.tables[i];
                    EventOnItemSpawnTableIndexUpdated.TryInvoke(table2, i + 1, i);
#if DEBUG
                    Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Item spawn table index updated: {table2.name.Format()} (# {(i + 1).Format()} -> {i.Format()}).");
#endif
                    if (!hasTierRem && !hasAssetRem)
                        continue;

                    maxCt = Math.Min(byte.MaxValue - 1, table2.tiers.Count);

                    for (int k = 0; k < maxCt; k++)
                    {
                        ItemTier tier = table2.tiers[k];
                        SpawnTierIdentifier id = new SpawnTierIdentifier(spawnType, (byte)i, (byte)k);
                        SpawnTierIdentifier idOld = new SpawnTierIdentifier(spawnType, (byte)(i + 1), (byte)k);

                        EventOnItemSpawnTierIndexUpdated.TryInvoke(tier, idOld, id, HierarchicalEventSource.ParentObject);

                        if (hasAssetRem)
                        {
                            int raw = id.Raw;
                            int rawOld = idOld.Raw;
                            int maxAssetCt = Math.Min(byte.MaxValue - 1, tier.table.Count);

                            for (int j = 0; j < maxAssetCt; ++j)
                            {
                                SpawnAssetIdentifier assetId = new SpawnAssetIdentifier(raw | (j << 24));
                                SpawnAssetIdentifier assetOldId = new SpawnAssetIdentifier(rawOld | (j << 24));
                                EventOnItemSpawnAssetIndexUpdated.TryInvoke(tier.table[j], assetOldId, assetId, HierarchicalEventSource.GrandParentObject);
#if DEBUG
                                Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Item spawn asset index updated: {assetOldId.Format()} -> {assetId.Format()} (parent updated).");
#endif
                            }
                        }
#if DEBUG
                        Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Item spawn tier index updated: {idOld.Format()} -> {id.Format()} (parent updated).");
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

                hasAssetRem = !EventOnZombieSpawnAssetRemoved.IsEmpty;

                if (!EventOnZombieSpawnTierRemoved.IsEmpty || hasAssetRem)
                {
                    for (int i = 0; i < zombieTable.slots.Length; i++)
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
                                EventOnZombieSpawnAssetRemoved.TryInvoke(tier.table[j], assetId, HierarchicalEventSource.GrandParentObject);
#if DEBUG
                                Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Zombie spawn asset removed: {assetId.Format()} (parent removed).");
#endif
                            }
                        }

                        EventOnZombieSpawnTierRemoved.TryInvoke(tier, id, HierarchicalEventSource.ParentObject);
#if DEBUG
                        Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Zombie spawn tier removed: {id.Format()} (parent removed).");
#endif
                    }
                }

                EventOnZombieSpawnTableRemoved.TryInvoke(zombieTable, index);
                Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Zombie spawn table removed: {zombieTable.name.Format()} (# {index.Format()}).");
                hasAssetRem = !EventOnZombieSpawnAssetIndexUpdated.IsEmpty;
                hasTierRem = !EventOnZombieSpawnTierIndexUpdated.IsEmpty;
                if (EventOnZombieSpawnTableIndexUpdated.IsEmpty && !hasTierRem && !hasAssetRem)
                    break;

                for (int i = index; i < LevelZombies.tables.Count; ++i)
                {
                    ZombieTable table2 = LevelZombies.tables[i];
                    EventOnZombieSpawnTableIndexUpdated.TryInvoke(table2, i + 1, i);
#if DEBUG
                    Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Zombie spawn table index updated: {table2.name.Format()} (# {(i + 1).Format()} -> {i.Format()}).");
#endif
                    if (!hasTierRem && !hasAssetRem)
                        continue;

                    for (int k = 0; k < table2.slots.Length; k++)
                    {
                        ZombieSlot tier = zombieTable.slots[k];
                        SpawnTierIdentifier id = new SpawnTierIdentifier(spawnType, (byte)i, (byte)k);
                        SpawnTierIdentifier idOld = new SpawnTierIdentifier(spawnType, (byte)(i + 1), (byte)k);

                        EventOnZombieSpawnTierIndexUpdated.TryInvoke(tier, idOld, id, HierarchicalEventSource.ParentObject);

                        if (hasAssetRem)
                        {
                            int raw = id.Raw;
                            int rawOld = idOld.Raw;
                            int maxAssetCt = Math.Min(byte.MaxValue - 1, tier.table.Count);

                            for (int j = 0; j < maxAssetCt; ++j)
                            {
                                SpawnAssetIdentifier assetId = new SpawnAssetIdentifier(raw | (j << 24));
                                SpawnAssetIdentifier assetOldId = new SpawnAssetIdentifier(rawOld | (j << 24));
                                EventOnZombieSpawnAssetIndexUpdated.TryInvoke(tier.table[j], assetOldId, assetId, HierarchicalEventSource.GrandParentObject);
#if DEBUG
                                Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Zombie spawn asset index updated: {assetOldId.Format()} -> {assetId.Format()} (parent updated).");
#endif
                            }
                        }
#if DEBUG
                        Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Zombie spawn tier index updated: {idOld.Format()} -> {id.Format()} (parent updated).");
#endif
                    }
                }

                break;
        }

#if CLIENT
        if (Level.isEditor)
            UpdateUITable(spawnType);
#endif
        return true;
    }

    /// <summary>
    /// Locally remove a spawn table's tier and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool RemoveSpawnTableTierLocal(SpawnTierIdentifier identifier)
    {
        ThreadUtil.assertIsGameThread();

        if (!identifier.CheckSafe())
            return false;

        SpawnType spawnType = identifier.Type;
        int tableIndex = identifier.TableIndex;
        int tierIndex = identifier.TierIndex;

        switch (spawnType)
        {
            case SpawnType.Zombie:
                return false;

            case SpawnType.Animal:
                AnimalTable animalTable = LevelAnimals.tables[tableIndex];
                AnimalTier animalTier = animalTable.tiers[tierIndex];
                animalTable.tiers.RemoveAt(tierIndex);

#if CLIENT
                if (tableIndex == EditorSpawns.selectedAnimal && GetSelectedAnimalTier != null && GetSelectedAnimalTier() == tierIndex)
                {
                    int newIndex = tierIndex;
                    if (newIndex == animalTable.tiers.Count - 1)
                        --newIndex;
                    if (newIndex >= animalTable.tiers.Count || newIndex < 0)
                        DeselectTier(SpawnType.Animal);
                    else
                        TrySelectTier(new SpawnTierIdentifier(identifier.Type, (byte)tableIndex, (byte)newIndex));
                }
#endif

                int maxCt = Math.Min(byte.MaxValue - 1, animalTier.table.Count);

                if (!EventOnAnimalSpawnAssetRemoved.IsEmpty)
                {
                    for (int i = 0; i < maxCt; i++)
                    {
                        AnimalSpawn spawn = animalTier.table[i];
                        SpawnAssetIdentifier id = new SpawnAssetIdentifier(spawnType, (byte)tableIndex, (byte)tierIndex, (byte)i);
                        
                        EventOnAnimalSpawnAssetRemoved.TryInvoke(spawn, id, HierarchicalEventSource.ParentObject);
#if DEBUG
                        Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Animal spawn asset removed: {spawn.animal.Format()} ({id.Format()}) (parent removed).");
#endif
                    }
                }
                
                EventOnAnimalSpawnTierRemoved.TryInvoke(animalTier, identifier, HierarchicalEventSource.ThisObject);
                Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Animal spawn tier removed: {animalTier.name.Format()} ({identifier.Format()}).");
                bool hasAssetRem = !EventOnAnimalSpawnAssetIndexUpdated.IsEmpty;
                if (EventOnAnimalSpawnTierIndexUpdated.IsEmpty && !hasAssetRem)
                    break;

                for (int i = tierIndex; i < animalTable.tiers.Count; ++i)
                {
                    AnimalTier tier2 = animalTable.tiers[i];
                    EventOnAnimalSpawnTierIndexUpdated.TryInvoke(tier2,
                        new SpawnTierIdentifier(spawnType, (byte)tableIndex, (byte)(i + 1)),
                        new SpawnTierIdentifier(spawnType, (byte)tableIndex, (byte)i), HierarchicalEventSource.ThisObject);
#if DEBUG
                    Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Animal spawn tier index updated: {tier2.name.Format()} (# {(i + 1).Format()} -> {i.Format()}).");
#endif
                    if (!hasAssetRem)
                        continue;

                    maxCt = Math.Min(byte.MaxValue - 1, tier2.table.Count);

                    for (int k = 0; k < maxCt; k++)
                    {
                        AnimalSpawn spawn = tier2.table[k];
                        SpawnAssetIdentifier id = new SpawnAssetIdentifier(spawnType, (byte)tableIndex, (byte)i, (byte)k);
                        SpawnAssetIdentifier idOld = new SpawnAssetIdentifier(spawnType, (byte)tableIndex, (byte)(i + 1), (byte)k);

                        EventOnAnimalSpawnAssetIndexUpdated.TryInvoke(spawn, idOld, id, HierarchicalEventSource.ParentObject);
#if DEBUG
                        Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Animal spawn asset index updated: {idOld.Format()} -> {id.Format()} (parent updated).");
#endif
                    }
                }

                break;
            case SpawnType.Vehicle:
                VehicleTable vehicleTable = LevelVehicles.tables[tableIndex];
                VehicleTier vehicleTier = vehicleTable.tiers[tierIndex];
                vehicleTable.tiers.RemoveAt(tierIndex);

#if CLIENT
                if (tableIndex == EditorSpawns.selectedVehicle && GetSelectedVehicleTier != null && GetSelectedVehicleTier() == tierIndex)
                {
                    int newIndex = tierIndex;
                    if (newIndex == vehicleTable.tiers.Count - 1)
                        --newIndex;
                    if (newIndex >= vehicleTable.tiers.Count || newIndex < 0)
                        DeselectTier(SpawnType.Vehicle);
                    else
                        TrySelectTier(new SpawnTierIdentifier(identifier.Type, (byte)tableIndex, (byte)newIndex));
                }
#endif

                maxCt = Math.Min(byte.MaxValue - 1, vehicleTier.table.Count);

                if (!EventOnVehicleSpawnAssetRemoved.IsEmpty)
                {
                    for (int i = 0; i < maxCt; i++)
                    {
                        VehicleSpawn spawn = vehicleTier.table[i];
                        SpawnAssetIdentifier id = new SpawnAssetIdentifier(spawnType, (byte)tableIndex, (byte)tierIndex, (byte)i);

                        EventOnVehicleSpawnAssetRemoved.TryInvoke(spawn, id, HierarchicalEventSource.ParentObject);
#if DEBUG
                        Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Vehicle spawn asset removed: {spawn.vehicle.Format()} ({id.Format()}) (parent removed).");
#endif
                    }
                }

                EventOnVehicleSpawnTierRemoved.TryInvoke(vehicleTier, identifier, HierarchicalEventSource.ThisObject);
                Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Vehicle spawn tier removed: {vehicleTable.name.Format()} ({identifier.Format()}).");
                hasAssetRem = !EventOnVehicleSpawnAssetIndexUpdated.IsEmpty;
                if (EventOnVehicleSpawnTierIndexUpdated.IsEmpty && !hasAssetRem)
                    break;

                for (int i = tierIndex; i < vehicleTable.tiers.Count; ++i)
                {
                    VehicleTier tier2 = vehicleTable.tiers[i];
                    EventOnVehicleSpawnTierIndexUpdated.TryInvoke(tier2,
                        new SpawnTierIdentifier(spawnType, (byte)tableIndex, (byte)(i + 1)),
                        new SpawnTierIdentifier(spawnType, (byte)tableIndex, (byte)i), HierarchicalEventSource.ThisObject);
#if DEBUG
                    Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Vehicle spawn tier index updated: {tier2.name.Format()} (# {(i + 1).Format()} -> {i.Format()}).");
#endif
                    if (!hasAssetRem)
                        continue;

                    maxCt = Math.Min(byte.MaxValue - 1, tier2.table.Count);

                    for (int k = 0; k < maxCt; k++)
                    {
                        VehicleSpawn spawn = tier2.table[k];
                        SpawnAssetIdentifier id = new SpawnAssetIdentifier(spawnType, (byte)tableIndex, (byte)i, (byte)k);
                        SpawnAssetIdentifier idOld = new SpawnAssetIdentifier(spawnType, (byte)tableIndex, (byte)(i + 1), (byte)k);

                        EventOnVehicleSpawnAssetIndexUpdated.TryInvoke(spawn, idOld, id, HierarchicalEventSource.ParentObject);
#if DEBUG
                        Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Vehicle spawn tier index updated: {idOld.Format()} -> {id.Format()} (parent updated).");
#endif
                    }
                }

                break;
            case SpawnType.Item:
                ItemTable itemTable = LevelItems.tables[tableIndex];
                ItemTier itemTier = itemTable.tiers[tierIndex];
                itemTable.tiers.RemoveAt(tierIndex);

#if CLIENT
                if (tableIndex == EditorSpawns.selectedItem && GetSelectedItemTier != null && GetSelectedItemTier() == tierIndex)
                {
                    int newIndex = tierIndex;
                    if (newIndex == itemTable.tiers.Count - 1)
                        --newIndex;
                    if (newIndex >= itemTable.tiers.Count || newIndex < 0)
                        DeselectTier(SpawnType.Item);
                    else
                        TrySelectTier(new SpawnTierIdentifier(identifier.Type, (byte)tableIndex, (byte)newIndex));
                }
#endif

                maxCt = Math.Min(byte.MaxValue - 1, itemTier.table.Count);

                if (!EventOnItemSpawnAssetRemoved.IsEmpty)
                {
                    for (int i = 0; i < maxCt; i++)
                    {
                        ItemSpawn spawn = itemTier.table[i];
                        SpawnAssetIdentifier id = new SpawnAssetIdentifier(spawnType, (byte)tableIndex, (byte)tierIndex, (byte)i);

                        EventOnItemSpawnAssetRemoved.TryInvoke(spawn, id, HierarchicalEventSource.ParentObject);
#if DEBUG
                        Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Item spawn asset removed: {spawn.item.Format()} ({id.Format()}) (parent removed).");
#endif
                    }
                }

                EventOnItemSpawnTierRemoved.TryInvoke(itemTier, identifier, HierarchicalEventSource.ThisObject);
                Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Item spawn tier removed: {itemTable.name.Format()} ({identifier.Format()}).");
                hasAssetRem = !EventOnItemSpawnAssetIndexUpdated.IsEmpty;
                if (EventOnItemSpawnTierIndexUpdated.IsEmpty && !hasAssetRem)
                    break;

                for (int i = tierIndex; i < itemTable.tiers.Count; ++i)
                {
                    ItemTier tier2 = itemTable.tiers[i];
                    EventOnItemSpawnTierIndexUpdated.TryInvoke(tier2,
                        new SpawnTierIdentifier(spawnType, (byte)tableIndex, (byte)(i + 1)),
                        new SpawnTierIdentifier(spawnType, (byte)tableIndex, (byte)i), HierarchicalEventSource.ThisObject);
#if DEBUG
                    Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Item spawn tier index updated: {tier2.name.Format()} (# {(i + 1).Format()} -> {i.Format()}).");
#endif
                    if (!hasAssetRem)
                        continue;

                    maxCt = Math.Min(byte.MaxValue - 1, tier2.table.Count);

                    for (int k = 0; k < maxCt; k++)
                    {
                        ItemSpawn spawn = tier2.table[k];
                        SpawnAssetIdentifier id = new SpawnAssetIdentifier(spawnType, (byte)tableIndex, (byte)i, (byte)k);
                        SpawnAssetIdentifier idOld = new SpawnAssetIdentifier(spawnType, (byte)tableIndex, (byte)(i + 1), (byte)k);

                        EventOnItemSpawnAssetIndexUpdated.TryInvoke(spawn, idOld, id, HierarchicalEventSource.ParentObject);
#if DEBUG
                        Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Item spawn asset index updated: {idOld.Format()} -> {id.Format()} (parent updated).");
#endif
                    }
                }

                break;
        }

#if CLIENT
        if (Level.isEditor)
            UpdateUISelection(spawnType);
#endif
        return true;
    }

    /// <summary>
    /// Locally remove a spawn table tier's asset and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool RemoveSpawnTableTierAssetLocal(SpawnAssetIdentifier identifier)
    {
        ThreadUtil.assertIsGameThread();

        if (!identifier.CheckSafe())
            return false;

        SpawnType spawnType = identifier.Type;
        int tableIndex = identifier.TableIndex;
        int tierIndex = identifier.TierIndex;
        int assetIndex = identifier.AssetIndex;

        switch (spawnType)
        {
            case SpawnType.Animal:
                AnimalTable animalTable = LevelAnimals.tables[tableIndex];
                AnimalTier animalTier = animalTable.tiers[tierIndex];
                AnimalSpawn animalSpawn = animalTier.table[assetIndex];
                animalTier.table.RemoveAt(assetIndex);

#if CLIENT
                if (tableIndex == EditorSpawns.selectedAnimal
                    && GetSelectedAnimalTier != null && GetSelectedAnimalTier() == tierIndex
                    && GetSelectedAnimalSpawn != null && GetSelectedAnimalSpawn() == assetIndex)
                {
                    DeselectTierAsset(SpawnType.Animal);
                }
#endif
                EventOnAnimalSpawnAssetRemoved.TryInvoke(animalSpawn, identifier, HierarchicalEventSource.ThisObject);
                Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Animal spawn table tier asset removed: {animalSpawn.animal.Format()} ({identifier.Format()}).");
                
                if (EventOnAnimalSpawnAssetIndexUpdated.IsEmpty)
                    break;

                for (int i = assetIndex; i < animalTier.table.Count; ++i)
                {
                    AnimalSpawn spawn2 = animalTier.table[i];
                    EventOnAnimalSpawnAssetIndexUpdated.TryInvoke(spawn2,
                        new SpawnAssetIdentifier(spawnType, (byte)tableIndex, (byte)tierIndex, (byte)(i + 1)),
                        new SpawnAssetIdentifier(spawnType, (byte)tableIndex, (byte)tierIndex, (byte)i), HierarchicalEventSource.ThisObject);
#if DEBUG
                    Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Animal spawn table tier asset index updated: {spawn2.animal.Format()} (# {(i + 1).Format()} -> {i.Format()}).");
#endif
                }

                break;

            case SpawnType.Vehicle:
                VehicleTable vehicleTable = LevelVehicles.tables[tableIndex];
                VehicleTier vehicleTier = vehicleTable.tiers[tierIndex];
                VehicleSpawn vehicleSpawn = vehicleTier.table[assetIndex];
                vehicleTier.table.RemoveAt(assetIndex);

#if CLIENT
                if (tableIndex == EditorSpawns.selectedVehicle
                    && GetSelectedVehicleTier != null && GetSelectedVehicleTier() == tierIndex
                    && GetSelectedVehicleSpawn != null && GetSelectedVehicleSpawn() == assetIndex)
                {
                    DeselectTierAsset(SpawnType.Vehicle);
                }
#endif
                EventOnVehicleSpawnAssetRemoved.TryInvoke(vehicleSpawn, identifier, HierarchicalEventSource.ThisObject);
                Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Vehicle spawn table tier asset removed: {vehicleSpawn.vehicle.Format()} ({identifier.Format()}).");
                
                if (EventOnVehicleSpawnAssetIndexUpdated.IsEmpty)
                    break;

                for (int i = assetIndex; i < vehicleTier.table.Count; ++i)
                {
                    VehicleSpawn spawn2 = vehicleTier.table[i];
                    EventOnVehicleSpawnAssetIndexUpdated.TryInvoke(spawn2,
                        new SpawnAssetIdentifier(spawnType, (byte)tableIndex, (byte)tierIndex, (byte)(i + 1)),
                        new SpawnAssetIdentifier(spawnType, (byte)tableIndex, (byte)tierIndex, (byte)i), HierarchicalEventSource.ThisObject);
#if DEBUG
                    Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Vehicle spawn table tier asset index updated: {spawn2.vehicle.Format()} (# {(i + 1).Format()} -> {i.Format()}).");
#endif
                }

                break;

            case SpawnType.Item:
                ItemTable itemTable = LevelItems.tables[tableIndex];
                ItemTier itemTier = itemTable.tiers[tierIndex];
                ItemSpawn itemSpawn = itemTier.table[assetIndex];
                itemTier.table.RemoveAt(assetIndex);

#if CLIENT
                if (tableIndex == EditorSpawns.selectedItem
                    && GetSelectedItemTier != null && GetSelectedItemTier() == tierIndex
                    && GetSelectedItemSpawn != null && GetSelectedItemSpawn() == assetIndex)
                {
                    DeselectTierAsset(SpawnType.Item);
                }
#endif
                EventOnItemSpawnAssetRemoved.TryInvoke(itemSpawn, identifier, HierarchicalEventSource.ThisObject);
                Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Item spawn table tier asset removed: {itemSpawn.item.Format()} ({identifier.Format()}).");
                
                if (EventOnItemSpawnAssetIndexUpdated.IsEmpty)
                    break;

                for (int i = assetIndex; i < itemTier.table.Count; ++i)
                {
                    ItemSpawn spawn2 = itemTier.table[i];
                    EventOnItemSpawnAssetIndexUpdated.TryInvoke(spawn2,
                        new SpawnAssetIdentifier(spawnType, (byte)tableIndex, (byte)tierIndex, (byte)(i + 1)),
                        new SpawnAssetIdentifier(spawnType, (byte)tableIndex, (byte)tierIndex, (byte)i), HierarchicalEventSource.ThisObject);
#if DEBUG
                    Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Item spawn table tier asset index updated: {spawn2.item.Format()} (# {(i + 1).Format()} -> {i.Format()}).");
#endif
                }

                break;

            case SpawnType.Zombie:
                ZombieTable zombieTable = LevelZombies.tables[tableIndex];
                ZombieSlot zombieTier = zombieTable.slots[tierIndex];
                ZombieCloth zombieSpawn = zombieTier.table[assetIndex];
                zombieTier.table.RemoveAt(assetIndex);

#if CLIENT
                if (tableIndex == EditorSpawns.selectedZombie
                    && GetSelectedZombieTier != null && GetSelectedZombieTier() == tierIndex
                    && GetSelectedZombieSpawn != null && GetSelectedZombieSpawn() == assetIndex)
                {
                    DeselectTierAsset(SpawnType.Zombie);
                }
#endif
                EventOnZombieSpawnAssetRemoved.TryInvoke(zombieSpawn, identifier, HierarchicalEventSource.ThisObject);
                Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Zombie spawn table tier asset removed: {zombieSpawn.item.Format()} ({identifier.Format()}).");
                
                if (EventOnZombieSpawnAssetIndexUpdated.IsEmpty)
                    break;

                for (int i = assetIndex; i < zombieTier.table.Count; ++i)
                {
                    ZombieCloth spawn2 = zombieTier.table[i];
                    EventOnZombieSpawnAssetIndexUpdated.TryInvoke(spawn2,
                        new SpawnAssetIdentifier(spawnType, (byte)tableIndex, (byte)tierIndex, (byte)(i + 1)),
                        new SpawnAssetIdentifier(spawnType, (byte)tableIndex, (byte)tierIndex, (byte)i), HierarchicalEventSource.ThisObject);
#if DEBUG
                    Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Zombie spawn table tier asset index updated: {spawn2.item.Format()} (# {(i + 1).Format()} -> {i.Format()}).");
#endif
                }

                break;
        }

#if CLIENT
        if (Level.isEditor)
            UpdateUISelection(spawnType);
#endif
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
    /// <param name="index">Table index of the target table.</param>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SetSpawnTableNameLocal(SpawnType spawnType, int index, string newName)
#if CLIENT
    {
        return SetSpawnTableNameLocal(spawnType, index, newName, true);
    }

    /// <summary>
    /// Locally set the name of a spawn table and call the necessary events.
    /// </summary>
    /// <param name="index">Table index of the target table.</param>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    internal static bool SetSpawnTableNameLocal(SpawnType spawnType, int index, string newName, bool updateField)
#endif
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
#if CLIENT
                if (Level.isEditor)
                    UIExtensionManager.GetInstance<EditorSpawnsAnimalsUIExtension>()?.UpdateTableName(index, updateField);
#endif

                EventOnAnimalSpawnTableNameUpdated.TryInvoke(animalTable, index);
                break;
            
            case SpawnType.Vehicle:
                VehicleTable vehicleTable = LevelVehicles.tables[index];
                vehicleTable.name = newName;
#if CLIENT
                if (Level.isEditor)
                    UIExtensionManager.GetInstance<EditorSpawnsVehiclesUIExtension>()?.UpdateTableName(index, updateField);
#endif

                EventOnVehicleSpawnTableNameUpdated.TryInvoke(vehicleTable, index);
                break;
            
            case SpawnType.Item:
                ItemTable itemTable = LevelItems.tables[index];
                itemTable.name = newName;
#if CLIENT
                if (Level.isEditor)
                    UIExtensionManager.GetInstance<EditorSpawnsItemsUIExtension>()?.UpdateTableName(index, updateField);
#endif

                EventOnItemSpawnTableNameUpdated.TryInvoke(itemTable, index);
                break;
            
            case SpawnType.Zombie:
                ZombieTable zombieTable = LevelZombies.tables[index];
                zombieTable.name = newName;
#if CLIENT
                if (Level.isEditor)
                    UIExtensionManager.GetInstance<EditorSpawnsZombiesUIExtension>()?.UpdateTableName(index, updateField);
#endif

                EventOnZombieSpawnTableNameUpdated.TryInvoke(zombieTable, index);
                break;
        }
        Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Set {spawnType.ToString().ToLowerInvariant()} " +
                                                                          $"spawn table (# {index.Format()}) name to {newName.Format()}.");

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
    /// <param name="index">Table index of the target table.</param>
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
                byte oldTable = EditorSpawns.selectedAnimal;
                EditorSpawns.selectedAnimal = (byte)index;
                try
                {
                    animalTable.color = newColor;
                }
                finally
                {
                    EditorSpawns.selectedAnimal = oldTable;
                }
#if CLIENT
                if (Level.isEditor && EditorSpawns.selectedAnimal == index)
                    UIExtensionManager.GetInstance<EditorSpawnsAnimalsUIExtension>()?.UpdateTableColor();
#endif

                EventOnAnimalSpawnTableColorUpdated.TryInvoke(animalTable, index);
                break;
            
            case SpawnType.Vehicle:
                VehicleTable vehicleTable = LevelVehicles.tables[index];
                oldTable = EditorSpawns.selectedVehicle;
                EditorSpawns.selectedVehicle = (byte)index;
                try
                {
                    vehicleTable.color = newColor;
                }
                finally
                {
                    EditorSpawns.selectedVehicle = oldTable;
                }
#if CLIENT
                if (Level.isEditor && EditorSpawns.selectedVehicle == index)
                    UIExtensionManager.GetInstance<EditorSpawnsVehiclesUIExtension>()?.UpdateTableColor();
#endif

                EventOnVehicleSpawnTableColorUpdated.TryInvoke(vehicleTable, index);
                break;
            
            case SpawnType.Item:
                ItemTable itemTable = LevelItems.tables[index];
                oldTable = EditorSpawns.selectedItem;
                EditorSpawns.selectedItem = (byte)index;
                try
                {
                    itemTable.color = newColor;
                }
                finally
                {
                    EditorSpawns.selectedItem = oldTable;
                }
#if CLIENT
                if (Level.isEditor && EditorSpawns.selectedItem == index)
                    UIExtensionManager.GetInstance<EditorSpawnsItemsUIExtension>()?.UpdateTableColor();
#endif

                EventOnItemSpawnTableColorUpdated.TryInvoke(itemTable, index);
                break;
            
            case SpawnType.Zombie:
                ZombieTable zombieTable = LevelZombies.tables[index];
                oldTable = EditorSpawns.selectedZombie;
                EditorSpawns.selectedZombie = (byte)index;
                try
                {
                    zombieTable.color = newColor;
                }
                finally
                {
                    EditorSpawns.selectedZombie = oldTable;
                }
#if CLIENT
                if (Level.isEditor && EditorSpawns.selectedZombie == index)
                    UIExtensionManager.GetInstance<EditorSpawnsZombiesUIExtension>()?.UpdateTableColor();
#endif

                EventOnZombieSpawnTableColorUpdated.TryInvoke(zombieTable, index);
                break;
        }
        Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Set {spawnType.ToString().ToLowerInvariant()} " +
                                                                    $"spawn table (# {index.Format()}) color to {newColor.Format()}.");

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
    /// <param name="index">Table index of the target table.</param>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SetSpawnTableSpawnAssetLocal(SpawnType spawnType, int index, AssetReference<SpawnAsset> newSpawnAsset)
    {
        if (newSpawnAsset.isNull)
            return SetSpawnTableSpawnAssetLocal(spawnType, index, null);

        ThreadUtil.assertIsGameThread();

        if (Assets.find(newSpawnAsset) is not { } spawnAsset)
            return false;

        return SetSpawnTableSpawnAssetLocal(spawnType, index, spawnAsset);
    }

    /// <summary>
    /// Locally set the spawn asset (usually called 'tableID', except for <see cref="ZombieTable.lootID"/>) of a spawn table and call the necessary events.
    /// </summary>
    /// <param name="index">Table index of the target table.</param>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SetSpawnTableSpawnAssetLocal(SpawnType spawnType, int index, ushort newSpawnAssetLegacyId)
    {
        if (newSpawnAssetLegacyId == 0)
            return SetSpawnTableSpawnAssetLocal(spawnType, index, null);

        ThreadUtil.assertIsGameThread();

        if (Assets.find(EAssetType.SPAWN, newSpawnAssetLegacyId) is not SpawnAsset spawnAsset)
            return false;

        return SetSpawnTableSpawnAssetLocal(spawnType, index, spawnAsset);
    }

    /// <summary>
    /// Locally set the spawn asset (usually called 'tableID', except for <see cref="ZombieTable.lootID"/>) of a spawn table and call the necessary events.
    /// </summary>
    /// <param name="index">Table index of the target table.</param>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SetSpawnTableSpawnAssetLocal(SpawnType spawnType, int index, SpawnAsset? newSpawnAsset)
    {
        ThreadUtil.assertIsGameThread();

        if (!CheckSpawnTableSafe(spawnType, index))
            return false;

        ushort id = newSpawnAsset?.id ?? 0;

        switch (spawnType)
        {
            case SpawnType.Animal:
                AnimalTable animalTable = LevelAnimals.tables[index];
                animalTable.tableID = id;
#if CLIENT
                if (Level.isEditor && EditorSpawns.selectedAnimal == index)
                    UIExtensionManager.GetInstance<EditorSpawnsAnimalsUIExtension>()?.UpdateTableId();
#endif

                EventOnAnimalSpawnTableSpawnAssetUpdated.TryInvoke(animalTable, index);
                break;
            
            case SpawnType.Vehicle:
                VehicleTable vehicleTable = LevelVehicles.tables[index];
                vehicleTable.tableID = id;
#if CLIENT
                if (Level.isEditor && EditorSpawns.selectedVehicle == index)
                    UIExtensionManager.GetInstance<EditorSpawnsVehiclesUIExtension>()?.UpdateTableId();
#endif

                EventOnVehicleSpawnTableSpawnAssetUpdated.TryInvoke(vehicleTable, index);
                break;
            
            case SpawnType.Item:
                ItemTable itemTable = LevelItems.tables[index];
                itemTable.tableID = id;
#if CLIENT
                if (Level.isEditor && EditorSpawns.selectedItem == index)
                    UIExtensionManager.GetInstance<EditorSpawnsItemsUIExtension>()?.UpdateTableId();
#endif

                EventOnItemSpawnTableSpawnAssetUpdated.TryInvoke(itemTable, index);
                break;
            
            case SpawnType.Zombie:
                ZombieTable zombieTable = LevelZombies.tables[index];
                zombieTable.lootID = id;
#if CLIENT
                if (Level.isEditor && EditorSpawns.selectedZombie == index)
                    UIExtensionManager.GetInstance<EditorSpawnsZombiesUIExtension>()?.UpdateLootId();
#endif

                EventOnZombieSpawnTableSpawnAssetUpdated.TryInvoke(zombieTable, index);
                break;
        }

        Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Set {spawnType.ToString().ToLowerInvariant()} " +
                                                                          $"spawn table (# {index.Format()}) asset ID to {id.Format()}.");
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
    /// <param name="index">Table index of the target table.</param>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SetZombieSpawnTableIsMegaLocal(int index, bool isMega)
    {
        ThreadUtil.assertIsGameThread();

        if (!CheckSpawnTableSafe(SpawnType.Zombie, index))
            return false;

        ZombieTable zombieTable = LevelZombies.tables[index];
        zombieTable.isMega = isMega;
#if CLIENT
        if (Level.isEditor && EditorSpawns.selectedZombie == index)
            UIExtensionManager.GetInstance<EditorSpawnsZombiesUIExtension>()?.UpdateIsMega();
#endif

        Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Set zombie spawn table (# {index.Format()}) isMega to {isMega.Format()}.");
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
    /// <param name="index">Table index of the target table.</param>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SetZombieSpawnTableHealthLocal(int index, ushort health)
    {
        ThreadUtil.assertIsGameThread();

        if (!CheckSpawnTableSafe(SpawnType.Zombie, index))
            return false;

        ZombieTable zombieTable = LevelZombies.tables[index];
        zombieTable.health = health;
#if CLIENT
        if (Level.isEditor && EditorSpawns.selectedZombie == index)
            UIExtensionManager.GetInstance<EditorSpawnsZombiesUIExtension>()?.UpdateHealth();
#endif

        Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Set zombie spawn table (# {index.Format()}) health to {health.Format()}.");
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
    /// <param name="index">Table index of the target table.</param>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SetZombieSpawnTableDamageLocal(int index, byte damage)
    {
        ThreadUtil.assertIsGameThread();

        if (!CheckSpawnTableSafe(SpawnType.Zombie, index))
            return false;

        ZombieTable zombieTable = LevelZombies.tables[index];
        zombieTable.damage = damage;
#if CLIENT
        if (Level.isEditor && EditorSpawns.selectedZombie == index)
            UIExtensionManager.GetInstance<EditorSpawnsZombiesUIExtension>()?.UpdateDamage();
#endif

        Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Set zombie spawn table (# {index.Format()}) damage to {damage.Format()}.");
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
    /// <param name="index">Table index of the target table.</param>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SetZombieSpawnTableLootIndexLocal(int index, byte lootIndex)
    {
        ThreadUtil.assertIsGameThread();

        if (!CheckSpawnTableSafe(SpawnType.Zombie, index))
            return false;

        ZombieTable zombieTable = LevelZombies.tables[index];
        zombieTable.lootIndex = lootIndex;
#if CLIENT
        if (Level.isEditor && EditorSpawns.selectedZombie == index)
            UIExtensionManager.GetInstance<EditorSpawnsZombiesUIExtension>()?.UpdateLootIndex();
#endif

        Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Set zombie spawn table (# {index.Format()}) loot index to {lootIndex.Format()} ({
            (lootIndex < LevelItems.tables.Count
                ? LevelItems.tables[lootIndex].name.Format(false)
                : "out of range".Colorize(ConsoleColor.Red)
            )}).");
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
    /// <param name="index">Table index of the target table.</param>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SetZombieSpawnTableXPLocal(int index, uint xp)
    {
        ThreadUtil.assertIsGameThread();

        if (!CheckSpawnTableSafe(SpawnType.Zombie, index))
            return false;

        ZombieTable zombieTable = LevelZombies.tables[index];
        zombieTable.xp = xp;
#if CLIENT
        if (Level.isEditor && EditorSpawns.selectedZombie == index)
            UIExtensionManager.GetInstance<EditorSpawnsZombiesUIExtension>()?.UpdateXP();
#endif

        Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Set zombie spawn table (# {index.Format()}) XP to {xp.Format()}.");
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
    /// <param name="index">Table index of the target table.</param>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SetZombieSpawnTableRegenLocal(int index, float regen)
    {
        ThreadUtil.assertIsGameThread();

        if (!CheckSpawnTableSafe(SpawnType.Zombie, index))
            return false;

        ZombieTable zombieTable = LevelZombies.tables[index];
        zombieTable.regen = regen;
#if CLIENT
        if (Level.isEditor && EditorSpawns.selectedZombie == index)
            UIExtensionManager.GetInstance<EditorSpawnsZombiesUIExtension>()?.UpdateRegen();
#endif

        Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Set zombie spawn table (# {index.Format()}) regen to {regen.Format()}.");
        EventOnZombieSpawnTableRegenUpdated.TryInvoke(zombieTable, index);
        return true;
    }

    /// <summary>
    /// Locally set <see cref="ZombieTable.difficultyGUID"/> of a spawn table and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SetZombieSpawnTableDifficultyAssetLocal(this ZombieTable spawnTable, AssetReference<ZombieDifficultyAsset> difficultyAsset)
    {
        ThreadUtil.assertIsGameThread();

        return SetZombieSpawnTableDifficultyAssetLocal(LevelZombies.tables.IndexOf(spawnTable), difficultyAsset);
    }

    /// <summary>
    /// Locally set <see cref="ZombieTable.difficultyGUID"/> of a spawn table and call the necessary events.
    /// </summary>
    /// <param name="index">Table index of the target table.</param>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SetZombieSpawnTableDifficultyAssetLocal(int index, AssetReference<ZombieDifficultyAsset> difficultyAsset)
    {
        ThreadUtil.assertIsGameThread();

        if (!CheckSpawnTableSafe(SpawnType.Zombie, index))
            return false;

        ZombieTable zombieTable = LevelZombies.tables[index];
        zombieTable.difficultyGUID = difficultyAsset.GUID.ToString("N");
        zombieTable.ResetCachedZombieDifficultyAsset();
#if CLIENT
        if (Level.isEditor && EditorSpawns.selectedZombie == index)
            UIExtensionManager.GetInstance<EditorSpawnsZombiesUIExtension>()?.UpdateDifficultyAsset();
#endif

        Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Set zombie spawn table (# {index.Format()}) difficulty asset to {difficultyAsset.Format()}.");
        EventOnZombieSpawnTableDifficultyAssetUpdated.TryInvoke(zombieTable, index);
        return true;
    }

    /// <summary>
    /// Reset <see cref="ZombieTable.cachedDifficulty"/> after changing the value of <see cref="ZombieTable.difficultyGUID"/>.
    /// </summary>
    /// <returns><see langword="false"/> in the case of a reflection failure, otherwise <see langword="true"/>.</returns>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool ResetCachedZombieDifficultyAsset(this ZombieTable spawnTable)
    {
        ThreadUtil.assertIsGameThread();

        if (SetCachedDifficultyAsset == null)
            return false;

        SetCachedDifficultyAsset(spawnTable, null);
        return true;
    }

    /// <summary>
    /// Locally set the name of a spawn tier and call the necessary events. Zombie tiers do not have names and will return <see langword="false"/>.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SetSpawnTableTierNameLocal(SpawnTierIdentifier identifier, string newName)
    {
        ThreadUtil.assertIsGameThread();

        newName ??= string.Empty;

        if (!identifier.CheckSafe())
            return false;

        switch (identifier.Type)
        {
            case SpawnType.Animal:
                AnimalTier animalTier = LevelAnimals.tables[identifier.TableIndex].tiers[identifier.TierIndex];
                animalTier.name = newName;
#if CLIENT
                if (Level.isEditor && EditorSpawns.selectedAnimal == identifier.TableIndex)
                    UIExtensionManager.GetInstance<EditorSpawnsAnimalsUIExtension>()?.UpdateTierName(identifier.TierIndex);
#endif

                EventOnAnimalSpawnTierNameUpdated.TryInvoke(animalTier, identifier, HierarchicalEventSource.ThisObject);
                break;

            case SpawnType.Vehicle:
                VehicleTier vehicleTier = LevelVehicles.tables[identifier.TableIndex].tiers[identifier.TierIndex];
                vehicleTier.name = newName;
#if CLIENT
                if (Level.isEditor && EditorSpawns.selectedVehicle == identifier.TableIndex)
                    UIExtensionManager.GetInstance<EditorSpawnsVehiclesUIExtension>()?.UpdateTierName(identifier.TierIndex);
#endif

                EventOnVehicleSpawnTierNameUpdated.TryInvoke(vehicleTier, identifier, HierarchicalEventSource.ThisObject);
                break;

            case SpawnType.Item:
                ItemTier itemTier = LevelItems.tables[identifier.TableIndex].tiers[identifier.TierIndex];
                itemTier.name = newName;
#if CLIENT
                if (Level.isEditor && EditorSpawns.selectedItem == identifier.TableIndex)
                    UIExtensionManager.GetInstance<EditorSpawnsItemsUIExtension>()?.UpdateTierName(identifier.TierIndex);
#endif

                EventOnItemSpawnTierNameUpdated.TryInvoke(itemTier, identifier, HierarchicalEventSource.ThisObject);
                break;

            case SpawnType.Zombie:
                return false;
        }

        Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Set {identifier.Format()} name to {newName.Format()}.");
        return true;
    }

    /// <summary>
    /// Locally set the chance of a spawn tier and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SetSpawnTableTierChanceLocal(SpawnTierIdentifier identifier, float chance)
#if CLIENT
    {
        return SetSpawnTableTierChanceLocal(identifier, chance, true);
    }

    /// <summary>
    /// Locally set the chance of a spawn tier and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    internal static bool SetSpawnTableTierChanceLocal(SpawnTierIdentifier identifier, float chance, bool updateSlider)
#endif
    {
        ThreadUtil.assertIsGameThread();

        if (!identifier.CheckSafe())
            return false;

        switch (identifier.Type)
        {
            case SpawnType.Animal:
                AnimalTier animalTier = LevelAnimals.tables[identifier.TableIndex].tiers[identifier.TierIndex];
                animalTier.chance = chance;
#if CLIENT
                if (Level.isEditor && EditorSpawns.selectedAnimal == identifier.TableIndex)
                    UIExtensionManager.GetInstance<EditorSpawnsAnimalsUIExtension>()?.UpdateTierChance(identifier.TierIndex, updateSlider);
#endif

                EventOnAnimalSpawnTierChanceUpdated.TryInvoke(animalTier, identifier, HierarchicalEventSource.ThisObject);
                break;

            case SpawnType.Vehicle:
                VehicleTier vehicleTier = LevelVehicles.tables[identifier.TableIndex].tiers[identifier.TierIndex];
                vehicleTier.chance = chance;
#if CLIENT
                if (Level.isEditor && EditorSpawns.selectedVehicle == identifier.TableIndex)
                    UIExtensionManager.GetInstance<EditorSpawnsVehiclesUIExtension>()?.UpdateTierChance(identifier.TierIndex, updateSlider);
#endif

                EventOnVehicleSpawnTierChanceUpdated.TryInvoke(vehicleTier, identifier, HierarchicalEventSource.ThisObject);
                break;

            case SpawnType.Item:
                ItemTier itemTier = LevelItems.tables[identifier.TableIndex].tiers[identifier.TierIndex];
                itemTier.chance = chance;
#if CLIENT
                if (Level.isEditor && EditorSpawns.selectedItem == identifier.TableIndex)
                    UIExtensionManager.GetInstance<EditorSpawnsItemsUIExtension>()?.UpdateTierChance(identifier.TierIndex, updateSlider);
#endif

                EventOnItemSpawnTierChanceUpdated.TryInvoke(itemTier, identifier, HierarchicalEventSource.ThisObject);
                break;

            case SpawnType.Zombie:
                ZombieSlot zombieTier = LevelZombies.tables[identifier.TableIndex].slots[identifier.TierIndex];
                zombieTier.chance = chance;
#if CLIENT
                if (Level.isEditor && EditorSpawns.selectedZombie == identifier.TableIndex)
                    UIExtensionManager.GetInstance<EditorSpawnsZombiesUIExtension>()?.UpdateSlotChance(identifier.TierIndex, updateSlider);
#endif

                EventOnZombieSpawnTierChanceUpdated.TryInvoke(zombieTier, identifier, HierarchicalEventSource.ThisObject);
                break;
        }

        Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Set {identifier.Format()} chance to {chance.Format("P")}.");
        return true;
    }

    /// <summary>
    /// Locally set the asset of a spawn tier asset and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SetSpawnTableTierAssetLocal(SpawnAssetIdentifier identifier, ushort legacyId)
    {
        ThreadUtil.assertIsGameThread();

        if (!identifier.CheckSafe())
            return false;

        Asset? a = null;

        if (legacyId != 0)
        {
            a = Assets.find(identifier.Type switch
            {
                SpawnType.Animal => EAssetType.ANIMAL,
                SpawnType.Vehicle => EAssetType.VEHICLE,
                _ => EAssetType.ITEM
            } , legacyId);
        }

        if (a == null && legacyId != 0)
            return false;

        return SetSpawnTableTierAssetLocal(identifier, a);
    }

    /// <summary>
    /// Locally set the asset of a spawn tier asset and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SetSpawnTableTierAssetLocal(SpawnAssetIdentifier identifier, AssetReference<Asset> asset)
    {
        ThreadUtil.assertIsGameThread();

        Asset? a = asset.Find();

        if (a == null && !asset.isNull)
            return false;

        return SetSpawnTableTierAssetLocal(identifier, a);
    }

    /// <summary>
    /// Locally set the asset of a spawn tier asset and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool SetSpawnTableTierAssetLocal(SpawnAssetIdentifier identifier, Asset? asset)
    {
        ThreadUtil.assertIsGameThread();

        if (!identifier.CheckSafe())
            return false;

        if (asset != null)
        {
            switch (identifier.Type)
            {
                case SpawnType.Animal:
                    if (asset is not AnimalAsset)
                        return false;
                    break;

                case SpawnType.Vehicle:
                    if (asset is not VehicleAsset)
                        return false;
                    break;

                case SpawnType.Item:
                case SpawnType.Zombie:
                default:
                    if (asset is not ItemAsset)
                        return false;
                    break;
            }
        }

        ushort id = asset?.id ?? 0;

        int tableIndex = identifier.TableIndex,
            tierIndex = identifier.TierIndex,
            assetIndex = identifier.AssetIndex;

        switch (identifier.Type)
        {
            case SpawnType.Animal:
                AnimalSpawn animalSpawn = new AnimalSpawn(id);
                LevelAnimals.tables[tableIndex].tiers[tierIndex].table[assetIndex] = animalSpawn;
#if CLIENT
                if (Level.isEditor && EditorSpawns.selectedAnimal == tableIndex &&
                    TryGetSelectedTier(SpawnType.Animal, out SpawnTierIdentifier? selectedTier) &&
                    selectedTier.HasValue && selectedTier.Value.TierIndex == tierIndex)
                {
                    UIExtensionManager.GetInstance<EditorSpawnsAnimalsUIExtension>()?.UpdateSpawnName(assetIndex);
                }
#endif

                EventOnAnimalSpawnAssetUpdated.TryInvoke(animalSpawn, identifier, HierarchicalEventSource.ThisObject);
                break;

            case SpawnType.Vehicle:
                VehicleSpawn vehicleSpawn = new VehicleSpawn(id);
                LevelVehicles.tables[tableIndex].tiers[tierIndex].table[assetIndex] = vehicleSpawn;
#if CLIENT
                if (Level.isEditor && EditorSpawns.selectedVehicle == tableIndex &&
                    TryGetSelectedTier(SpawnType.Vehicle, out selectedTier) &&
                    selectedTier.HasValue && selectedTier.Value.TierIndex == tierIndex)
                {
                    UIExtensionManager.GetInstance<EditorSpawnsVehiclesUIExtension>()?.UpdateSpawnName(assetIndex);
                }
#endif

                EventOnVehicleSpawnAssetUpdated.TryInvoke(vehicleSpawn, identifier, HierarchicalEventSource.ThisObject);
                break;

            case SpawnType.Item:
                ItemSpawn itemSpawn = new ItemSpawn(id);
                LevelItems.tables[tableIndex].tiers[tierIndex].table[assetIndex] = itemSpawn;
#if CLIENT
                if (Level.isEditor && EditorSpawns.selectedItem == tableIndex &&
                    TryGetSelectedTier(SpawnType.Item, out selectedTier) &&
                    selectedTier.HasValue && selectedTier.Value.TierIndex == tierIndex)
                {
                    UIExtensionManager.GetInstance<EditorSpawnsItemsUIExtension>()?.UpdateSpawnName(assetIndex);
                }
#endif

                EventOnItemSpawnAssetUpdated.TryInvoke(itemSpawn, identifier, HierarchicalEventSource.ThisObject);
                break;

            case SpawnType.Zombie:
                ZombieCloth zombieSpawn = new ZombieCloth(id);
                LevelZombies.tables[tableIndex].slots[tierIndex].table[assetIndex] = zombieSpawn;
#if CLIENT
                if (Level.isEditor && EditorSpawns.selectedZombie == tableIndex &&
                    TryGetSelectedTier(SpawnType.Zombie, out selectedTier) &&
                    selectedTier.HasValue && selectedTier.Value.TierIndex == tierIndex)
                {
                    UIExtensionManager.GetInstance<EditorSpawnsZombiesUIExtension>()?.UpdateSpawnName(assetIndex);
                }
#endif

                EventOnZombieSpawnAssetUpdated.TryInvoke(zombieSpawn, identifier, HierarchicalEventSource.ThisObject);
                break;
        }

        Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Set {identifier.Format()} ID to {id.Format("P")} ({asset.Format()}).");
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
    /// Normalizes all chances so that they add up to 1 (100%).
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <param name="index">Table index of the target table.</param>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool NormalizeChancesLocal(SpawnType spawnType, int index)
    {
        ThreadUtil.assertIsGameThread();

        if (!CheckSpawnTableSafe(spawnType, index))
            return false;

        switch (spawnType)
        {
            case SpawnType.Animal:
                AnimalTable animalTable = LevelAnimals.tables[index];
                float ttl = 0f;
                int ct = Math.Min(byte.MaxValue, animalTable.tiers.Count);

                for (int i = 0; i < ct; ++i)
                    ttl += animalTable.tiers[i].chance;

                if (ttl == 1f)
                    return true;

                for (int i = 0; i < ct; ++i)
                    animalTable.tiers[i].chance /= ttl;

                if (!EventOnAnimalSpawnTierChanceUpdated.IsEmpty)
                {
                    for (int i = 0; i < ct; ++i)
                    {
                        EventOnAnimalSpawnTierChanceUpdated?.TryInvoke(animalTable.tiers[i],
                            new SpawnTierIdentifier(spawnType, (byte)index, (byte)i), HierarchicalEventSource.ParentObject);
                    }
                }

                break;

            case SpawnType.Vehicle:
                VehicleTable vehicleTable = LevelVehicles.tables[index];
                ttl = 0f;
                ct = Math.Min(byte.MaxValue, vehicleTable.tiers.Count);

                for (int i = 0; i < ct; ++i)
                    ttl += vehicleTable.tiers[i].chance;

                if (ttl == 1f)
                    return true;

                for (int i = 0; i < ct; ++i)
                    vehicleTable.tiers[i].chance /= ttl;

                if (!EventOnVehicleSpawnTierChanceUpdated.IsEmpty)
                {
                    for (int i = 0; i < ct; ++i)
                    {
                        EventOnVehicleSpawnTierChanceUpdated.TryInvoke(vehicleTable.tiers[i],
                            new SpawnTierIdentifier(spawnType, (byte)index, (byte)i), HierarchicalEventSource.ParentObject);
                    }
                }

                break;

            case SpawnType.Item:
                ItemTable itemTable = LevelItems.tables[index];
                ttl = 0f;
                ct = Math.Min(byte.MaxValue, itemTable.tiers.Count);

                for (int i = 0; i < ct; ++i)
                    ttl += itemTable.tiers[i].chance;

                if (ttl == 1f)
                    return true;

                for (int i = 0; i < ct; ++i)
                    itemTable.tiers[i].chance /= ttl;

                if (!EventOnItemSpawnTierChanceUpdated.IsEmpty)
                {
                    for (int i = 0; i < ct; ++i)
                    {
                        EventOnItemSpawnTierChanceUpdated.TryInvoke(itemTable.tiers[i],
                            new SpawnTierIdentifier(spawnType, (byte)index, (byte)i), HierarchicalEventSource.ParentObject);
                    }
                }

                break;

            case SpawnType.Zombie:
                ZombieTable zombieTable = LevelZombies.tables[index];
                ttl = 0f;
                ct = Math.Min(byte.MaxValue, zombieTable.slots.Length);

                for (int i = 0; i < ct; ++i)
                    ttl += zombieTable.slots[i].chance;

                if (ttl == 1f)
                    return true;

                for (int i = 0; i < ct; ++i)
                    zombieTable.slots[i].chance /= ttl;

                if (!EventOnZombieSpawnTierChanceUpdated.IsEmpty)
                {
                    for (int i = 0; i < ct; ++i)
                    {
                        EventOnZombieSpawnTierChanceUpdated.TryInvoke(zombieTable.slots[i],
                            new SpawnTierIdentifier(spawnType, (byte)index, (byte)i), HierarchicalEventSource.ParentObject);
                    }
                }

                break;

        }

        Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Normalized {spawnType.ToString().ToLowerInvariant()} spawn table (# {index.Format()}) chances");
        return true;
    }

    internal static int GetTableCountUnsafe(SpawnType spawnType)
    {
        return spawnType switch
        {
            SpawnType.Animal => LevelAnimals.tables.Count,
            SpawnType.Vehicle => LevelVehicles.tables.Count,
            SpawnType.Item => LevelItems.tables.Count,
            SpawnType.Zombie => LevelZombies.tables.Count,
            _ => 0
        };
    }
}