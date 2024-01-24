using DevkitServer.API;
using DevkitServer.API.Devkit.Spawns;
using DevkitServer.API.Permissions;
using DevkitServer.Core.Permissions;
using DevkitServer.Models;
using DevkitServer.Multiplayer.Actions;
using DevkitServer.Multiplayer.Levels;
using DevkitServer.Multiplayer.Networking;
using DevkitServer.Util.Region;
#if CLIENT
using Cysharp.Threading.Tasks;
using DevkitServer.API.Abstractions;
using DevkitServer.API.UI;
using DevkitServer.API.UI.Extensions;
using DevkitServer.Core.Tools;
using DevkitServer.Core.UI.Extensions;
using DevkitServer.Players;
using SDG.Framework.Devkit;
using SDG.Framework.Utilities;
#elif SERVER
using DevkitServer.API.UI;
using DevkitServer.Players;
#endif

namespace DevkitServer.Util;

public delegate void AnimalSpawnTableArgs(AnimalTable spawnTable, int index);
public delegate void VehicleSpawnTableArgs(VehicleTable spawnTable, int index);
public delegate void ItemSpawnTableArgs(ItemTable spawnTable, int index);
public delegate void ZombieSpawnTableArgs(ZombieTable spawnTable, int index);
public delegate void SpawnTableArgs(SpawnType spawnType, int index);

public delegate void AnimalSpawnTierArgs(AnimalTier spawnTier, SpawnTierIdentifier identifier, HierarchicalEventSource eventSource);
public delegate void VehicleSpawnTierArgs(VehicleTier spawnTier, SpawnTierIdentifier identifier, HierarchicalEventSource eventSource);
public delegate void ItemSpawnTierArgs(ItemTier spawnTier, SpawnTierIdentifier identifier, HierarchicalEventSource eventSource);
public delegate void ZombieSpawnTierArgs(ZombieSlot spawnTier, SpawnTierIdentifier identifier, HierarchicalEventSource eventSource);
public delegate void SpawnTierArgs(SpawnTierIdentifier identifier, HierarchicalEventSource eventSource);

public delegate void AnimalSpawnAssetArgs(AnimalSpawn spawnAsset, SpawnAssetIdentifier identifier, HierarchicalEventSource eventSource);
public delegate void VehicleSpawnAssetArgs(VehicleSpawn spawnAsset, SpawnAssetIdentifier identifier, HierarchicalEventSource eventSource);
public delegate void ItemSpawnAssetArgs(ItemSpawn spawnAsset, SpawnAssetIdentifier identifier, HierarchicalEventSource eventSource);
public delegate void ZombieSpawnAssetArgs(ZombieCloth spawnAsset, SpawnAssetIdentifier identifier, HierarchicalEventSource eventSource);
public delegate void SpawnAssetArgs(SpawnAssetIdentifier identifier, HierarchicalEventSource eventSource);

public delegate void AnimalSpawnTableIndexUpdated(AnimalTable spawnTable, int fromIndex, int toIndex);
public delegate void VehicleSpawnTableIndexUpdated(VehicleTable spawnTable, int fromIndex, int toIndex);
public delegate void ItemSpawnTableIndexUpdated(ItemTable spawnTable, int fromIndex, int toIndex);
public delegate void ZombieSpawnTableIndexUpdated(ZombieTable spawnTable, int fromIndex, int toIndex);
public delegate void SpawnTableIndexUpdated(SpawnType spawnType, int fromIndex, int toIndex);

public delegate void AnimalSpawnTierIndexUpdated(AnimalTier spawnTier, SpawnTierIdentifier fromIndex, SpawnTierIdentifier toIndex, HierarchicalEventSource eventSource);
public delegate void VehicleSpawnTierIndexUpdated(VehicleTier spawnTier, SpawnTierIdentifier fromIndex, SpawnTierIdentifier toIndex, HierarchicalEventSource eventSource);
public delegate void ItemSpawnTierIndexUpdated(ItemTier spawnTier, SpawnTierIdentifier fromIndex, SpawnTierIdentifier toIndex, HierarchicalEventSource eventSource);
public delegate void ZombieSpawnTierIndexUpdated(ZombieSlot spawnTier, SpawnTierIdentifier fromIndex, SpawnTierIdentifier toIndex, HierarchicalEventSource eventSource);
public delegate void SpawnTierIndexUpdated(SpawnTierIdentifier fromIndex, SpawnTierIdentifier toIndex, HierarchicalEventSource eventSource);

public delegate void AnimalSpawnAssetIndexUpdated(AnimalSpawn spawnAsset, SpawnAssetIdentifier fromIndex, SpawnAssetIdentifier toIndex, HierarchicalEventSource eventSource);
public delegate void VehicleSpawnAssetIndexUpdated(VehicleSpawn spawnAsset, SpawnAssetIdentifier fromIndex, SpawnAssetIdentifier toIndex, HierarchicalEventSource eventSource);
public delegate void ItemSpawnAssetIndexUpdated(ItemSpawn spawnAsset, SpawnAssetIdentifier fromIndex, SpawnAssetIdentifier toIndex, HierarchicalEventSource eventSource);
public delegate void ZombieSpawnAssetIndexUpdated(ZombieCloth spawnAsset, SpawnAssetIdentifier fromIndex, SpawnAssetIdentifier toIndex, HierarchicalEventSource eventSource);
public delegate void SpawnAssetIndexUpdated(SpawnAssetIdentifier fromIndex, SpawnAssetIdentifier toIndex, HierarchicalEventSource eventSource);

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

    [UsedImplicitly]
    private static readonly NetCall<NetId64, byte, string, ushort, Color, ulong> SendBasicSpawnTableInstantiation = new NetCall<NetId64, byte, string, ushort, Color, ulong>(DevkitServerNetCall.SendBasicSpawnTableInstantiation);

    [UsedImplicitly]
    private static readonly NetCall<NetId64, string, long, uint, float, NetId64, Guid, Color, ulong[], ulong> SendZombieSpawnTableInstantiation = new NetCall<NetId64, string, long, uint, float, NetId64, Guid, Color, ulong[], ulong>(DevkitServerNetCall.SendZombieSpawnTableInstantiation);
    
    [UsedImplicitly]
    private static readonly NetCall<byte, string> RequestSpawnTableInstantiation = new NetCall<byte, string>(DevkitServerNetCall.RequestSpawnTableInstantiation);

    [UsedImplicitly]
    private static readonly NetCall<NetId64, NetId64, byte, float, string, ulong> SendSpawnTierInstantiation = new NetCall<NetId64, NetId64, byte, float, string, ulong>(DevkitServerNetCall.SendSpawnTierInstantiation);

    [UsedImplicitly]
    private static readonly NetCall<NetId64, byte, string> RequestSpawnTierInstantiation = new NetCall<NetId64, byte, string>(DevkitServerNetCall.RequestSpawnTierInstantiation);

    [UsedImplicitly]
    private static readonly NetCall<NetId64, NetId64, byte, ushort, ulong> SendSpawnAssetInstantiation = new NetCall<NetId64, NetId64, byte, ushort, ulong>(DevkitServerNetCall.SendSpawnAssetInstantiation);

    [UsedImplicitly]
    private static readonly NetCall<NetId64, byte, ushort> RequestSpawnAssetInstantiation = new NetCall<NetId64, byte, ushort>(DevkitServerNetCall.RequestSpawnAssetInstantiation);

    internal static readonly CachedMulticastEvent<AnimalSpawnTableArgs> EventOnAnimalSpawnTableNameUpdated = new CachedMulticastEvent<AnimalSpawnTableArgs>(typeof(SpawnTableUtil), nameof(OnAnimalSpawnTableNameUpdated));
    internal static readonly CachedMulticastEvent<VehicleSpawnTableArgs> EventOnVehicleSpawnTableNameUpdated = new CachedMulticastEvent<VehicleSpawnTableArgs>(typeof(SpawnTableUtil), nameof(OnVehicleSpawnTableNameUpdated));
    internal static readonly CachedMulticastEvent<ItemSpawnTableArgs> EventOnItemSpawnTableNameUpdated = new CachedMulticastEvent<ItemSpawnTableArgs>(typeof(SpawnTableUtil), nameof(OnItemSpawnTableNameUpdated));
    internal static readonly CachedMulticastEvent<ZombieSpawnTableArgs> EventOnZombieSpawnTableNameUpdated = new CachedMulticastEvent<ZombieSpawnTableArgs>(typeof(SpawnTableUtil), nameof(OnZombieSpawnTableNameUpdated));
    internal static readonly CachedMulticastEvent<SpawnTableArgs> EventOnSpawnTableNameUpdated = new CachedMulticastEvent<SpawnTableArgs>(typeof(SpawnTableUtil), nameof(OnSpawnTableNameUpdated));
    
    internal static readonly CachedMulticastEvent<AnimalSpawnTierArgs> EventOnAnimalSpawnTierNameUpdated = new CachedMulticastEvent<AnimalSpawnTierArgs>(typeof(SpawnTableUtil), nameof(OnAnimalSpawnTierNameUpdated));
    internal static readonly CachedMulticastEvent<VehicleSpawnTierArgs> EventOnVehicleSpawnTierNameUpdated = new CachedMulticastEvent<VehicleSpawnTierArgs>(typeof(SpawnTableUtil), nameof(OnVehicleSpawnTierNameUpdated));
    internal static readonly CachedMulticastEvent<ItemSpawnTierArgs> EventOnItemSpawnTierNameUpdated = new CachedMulticastEvent<ItemSpawnTierArgs>(typeof(SpawnTableUtil), nameof(OnItemSpawnTierNameUpdated));
    internal static readonly CachedMulticastEvent<SpawnTierArgs> EventOnSpawnTierNameUpdated = new CachedMulticastEvent<SpawnTierArgs>(typeof(SpawnTableUtil), nameof(OnSpawnTierNameUpdated));
    
    internal static readonly CachedMulticastEvent<AnimalSpawnTierArgs> EventOnAnimalSpawnTierChanceUpdated = new CachedMulticastEvent<AnimalSpawnTierArgs>(typeof(SpawnTableUtil), nameof(OnAnimalSpawnTierChanceUpdated));
    internal static readonly CachedMulticastEvent<VehicleSpawnTierArgs> EventOnVehicleSpawnTierChanceUpdated = new CachedMulticastEvent<VehicleSpawnTierArgs>(typeof(SpawnTableUtil), nameof(OnVehicleSpawnTierChanceUpdated));
    internal static readonly CachedMulticastEvent<ItemSpawnTierArgs> EventOnItemSpawnTierChanceUpdated = new CachedMulticastEvent<ItemSpawnTierArgs>(typeof(SpawnTableUtil), nameof(OnItemSpawnTierChanceUpdated));
    internal static readonly CachedMulticastEvent<ZombieSpawnTierArgs> EventOnZombieSpawnTierChanceUpdated = new CachedMulticastEvent<ZombieSpawnTierArgs>(typeof(SpawnTableUtil), nameof(OnZombieSpawnTierChanceUpdated));
    internal static readonly CachedMulticastEvent<SpawnTierArgs> EventOnSpawnTierChanceUpdated = new CachedMulticastEvent<SpawnTierArgs>(typeof(SpawnTableUtil), nameof(OnSpawnTierChanceUpdated));
    
    internal static readonly CachedMulticastEvent<AnimalSpawnTableArgs> EventOnAnimalSpawnTableAdded = new CachedMulticastEvent<AnimalSpawnTableArgs>(typeof(SpawnTableUtil), nameof(OnAnimalSpawnTableAdded));
    internal static readonly CachedMulticastEvent<VehicleSpawnTableArgs> EventOnVehicleSpawnTableAdded = new CachedMulticastEvent<VehicleSpawnTableArgs>(typeof(SpawnTableUtil), nameof(OnVehicleSpawnTableAdded));
    internal static readonly CachedMulticastEvent<ItemSpawnTableArgs> EventOnItemSpawnTableAdded = new CachedMulticastEvent<ItemSpawnTableArgs>(typeof(SpawnTableUtil), nameof(OnItemSpawnTableAdded));
    internal static readonly CachedMulticastEvent<ZombieSpawnTableArgs> EventOnZombieSpawnTableAdded = new CachedMulticastEvent<ZombieSpawnTableArgs>(typeof(SpawnTableUtil), nameof(OnZombieSpawnTableAdded));
    internal static readonly CachedMulticastEvent<SpawnTableArgs> EventOnSpawnTableAdded = new CachedMulticastEvent<SpawnTableArgs>(typeof(SpawnTableUtil), nameof(OnSpawnTableAdded));
    
    internal static readonly CachedMulticastEvent<AnimalSpawnTierArgs> EventOnAnimalSpawnTierAdded = new CachedMulticastEvent<AnimalSpawnTierArgs>(typeof(SpawnTableUtil), nameof(OnAnimalSpawnTierAdded));
    internal static readonly CachedMulticastEvent<VehicleSpawnTierArgs> EventOnVehicleSpawnTierAdded = new CachedMulticastEvent<VehicleSpawnTierArgs>(typeof(SpawnTableUtil), nameof(OnVehicleSpawnTierAdded));
    internal static readonly CachedMulticastEvent<ItemSpawnTierArgs> EventOnItemSpawnTierAdded = new CachedMulticastEvent<ItemSpawnTierArgs>(typeof(SpawnTableUtil), nameof(OnItemSpawnTierAdded));
    internal static readonly CachedMulticastEvent<ZombieSpawnTierArgs> EventOnZombieSpawnTierAdded = new CachedMulticastEvent<ZombieSpawnTierArgs>(typeof(SpawnTableUtil), nameof(OnZombieSpawnTierAdded));
    internal static readonly CachedMulticastEvent<SpawnTierArgs> EventOnSpawnTierAdded = new CachedMulticastEvent<SpawnTierArgs>(typeof(SpawnTableUtil), nameof(OnSpawnTierAdded));
    
    internal static readonly CachedMulticastEvent<AnimalSpawnTableArgs> EventOnAnimalSpawnTableColorUpdated = new CachedMulticastEvent<AnimalSpawnTableArgs>(typeof(SpawnTableUtil), nameof(OnAnimalSpawnTableColorUpdated));
    internal static readonly CachedMulticastEvent<VehicleSpawnTableArgs> EventOnVehicleSpawnTableColorUpdated = new CachedMulticastEvent<VehicleSpawnTableArgs>(typeof(SpawnTableUtil), nameof(OnVehicleSpawnTableColorUpdated));
    internal static readonly CachedMulticastEvent<ItemSpawnTableArgs> EventOnItemSpawnTableColorUpdated = new CachedMulticastEvent<ItemSpawnTableArgs>(typeof(SpawnTableUtil), nameof(OnItemSpawnTableColorUpdated));
    internal static readonly CachedMulticastEvent<ZombieSpawnTableArgs> EventOnZombieSpawnTableColorUpdated = new CachedMulticastEvent<ZombieSpawnTableArgs>(typeof(SpawnTableUtil), nameof(OnZombieSpawnTableColorUpdated));
    internal static readonly CachedMulticastEvent<SpawnTableArgs> EventOnSpawnTableColorUpdated = new CachedMulticastEvent<SpawnTableArgs>(typeof(SpawnTableUtil), nameof(OnSpawnTableColorUpdated));
    
    internal static readonly CachedMulticastEvent<AnimalSpawnTableArgs> EventOnAnimalSpawnTableSpawnAssetUpdated = new CachedMulticastEvent<AnimalSpawnTableArgs>(typeof(SpawnTableUtil), nameof(OnAnimalSpawnTableSpawnAssetUpdated));
    internal static readonly CachedMulticastEvent<VehicleSpawnTableArgs> EventOnVehicleSpawnTableSpawnAssetUpdated = new CachedMulticastEvent<VehicleSpawnTableArgs>(typeof(SpawnTableUtil), nameof(OnVehicleSpawnTableSpawnAssetUpdated));
    internal static readonly CachedMulticastEvent<ItemSpawnTableArgs> EventOnItemSpawnTableSpawnAssetUpdated = new CachedMulticastEvent<ItemSpawnTableArgs>(typeof(SpawnTableUtil), nameof(OnItemSpawnTableSpawnAssetUpdated));
    internal static readonly CachedMulticastEvent<ZombieSpawnTableArgs> EventOnZombieSpawnTableSpawnAssetUpdated = new CachedMulticastEvent<ZombieSpawnTableArgs>(typeof(SpawnTableUtil), nameof(OnZombieSpawnTableSpawnAssetUpdated));
    internal static readonly CachedMulticastEvent<SpawnTableArgs> EventOnSpawnTableSpawnAssetUpdated = new CachedMulticastEvent<SpawnTableArgs>(typeof(SpawnTableUtil), nameof(OnSpawnTableSpawnAssetUpdated));
    
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
    internal static readonly CachedMulticastEvent<SpawnTableArgs> EventOnSpawnTableRemoved = new CachedMulticastEvent<SpawnTableArgs>(typeof(SpawnTableUtil), nameof(OnSpawnTableRemoved));
    
    internal static readonly CachedMulticastEvent<AnimalSpawnTableIndexUpdated> EventOnAnimalSpawnTableIndexUpdated = new CachedMulticastEvent<AnimalSpawnTableIndexUpdated>(typeof(SpawnTableUtil), nameof(OnAnimalSpawnTableIndexUpdated));
    internal static readonly CachedMulticastEvent<VehicleSpawnTableIndexUpdated> EventOnVehicleSpawnTableIndexUpdated = new CachedMulticastEvent<VehicleSpawnTableIndexUpdated>(typeof(SpawnTableUtil), nameof(OnVehicleSpawnTableIndexUpdated));
    internal static readonly CachedMulticastEvent<ItemSpawnTableIndexUpdated> EventOnItemSpawnTableIndexUpdated = new CachedMulticastEvent<ItemSpawnTableIndexUpdated>(typeof(SpawnTableUtil), nameof(OnItemSpawnTableIndexUpdated));
    internal static readonly CachedMulticastEvent<ZombieSpawnTableIndexUpdated> EventOnZombieSpawnTableIndexUpdated = new CachedMulticastEvent<ZombieSpawnTableIndexUpdated>(typeof(SpawnTableUtil), nameof(OnZombieSpawnTableIndexUpdated));
    internal static readonly CachedMulticastEvent<SpawnTableIndexUpdated> EventOnSpawnTableIndexUpdated = new CachedMulticastEvent<SpawnTableIndexUpdated>(typeof(SpawnTableUtil), nameof(OnSpawnTableIndexUpdated));
    
    internal static readonly CachedMulticastEvent<AnimalSpawnTierArgs> EventOnAnimalSpawnTierRemoved = new CachedMulticastEvent<AnimalSpawnTierArgs>(typeof(SpawnTableUtil), nameof(OnAnimalSpawnTierRemoved));
    internal static readonly CachedMulticastEvent<VehicleSpawnTierArgs> EventOnVehicleSpawnTierRemoved = new CachedMulticastEvent<VehicleSpawnTierArgs>(typeof(SpawnTableUtil), nameof(OnVehicleSpawnTierRemoved));
    internal static readonly CachedMulticastEvent<ItemSpawnTierArgs> EventOnItemSpawnTierRemoved = new CachedMulticastEvent<ItemSpawnTierArgs>(typeof(SpawnTableUtil), nameof(OnItemSpawnTierRemoved));
    internal static readonly CachedMulticastEvent<ZombieSpawnTierArgs> EventOnZombieSpawnTierRemoved = new CachedMulticastEvent<ZombieSpawnTierArgs>(typeof(SpawnTableUtil), nameof(OnZombieSpawnTierRemoved));
    internal static readonly CachedMulticastEvent<SpawnTierArgs> EventOnSpawnTierRemoved = new CachedMulticastEvent<SpawnTierArgs>(typeof(SpawnTableUtil), nameof(OnSpawnTierRemoved));
    
    internal static readonly CachedMulticastEvent<AnimalSpawnTierIndexUpdated> EventOnAnimalSpawnTierIndexUpdated = new CachedMulticastEvent<AnimalSpawnTierIndexUpdated>(typeof(SpawnTableUtil), nameof(OnAnimalSpawnTierIndexUpdated));
    internal static readonly CachedMulticastEvent<VehicleSpawnTierIndexUpdated> EventOnVehicleSpawnTierIndexUpdated = new CachedMulticastEvent<VehicleSpawnTierIndexUpdated>(typeof(SpawnTableUtil), nameof(OnVehicleSpawnTierIndexUpdated));
    internal static readonly CachedMulticastEvent<ItemSpawnTierIndexUpdated> EventOnItemSpawnTierIndexUpdated = new CachedMulticastEvent<ItemSpawnTierIndexUpdated>(typeof(SpawnTableUtil), nameof(OnItemSpawnTierIndexUpdated));
    internal static readonly CachedMulticastEvent<ZombieSpawnTierIndexUpdated> EventOnZombieSpawnTierIndexUpdated = new CachedMulticastEvent<ZombieSpawnTierIndexUpdated>(typeof(SpawnTableUtil), nameof(OnZombieSpawnTierIndexUpdated));
    internal static readonly CachedMulticastEvent<SpawnTierIndexUpdated> EventOnSpawnTierIndexUpdated = new CachedMulticastEvent<SpawnTierIndexUpdated>(typeof(SpawnTableUtil), nameof(OnSpawnTierIndexUpdated));
    
    internal static readonly CachedMulticastEvent<AnimalSpawnAssetArgs> EventOnAnimalSpawnAssetRemoved = new CachedMulticastEvent<AnimalSpawnAssetArgs>(typeof(SpawnTableUtil), nameof(OnAnimalSpawnAssetRemoved));
    internal static readonly CachedMulticastEvent<VehicleSpawnAssetArgs> EventOnVehicleSpawnAssetRemoved = new CachedMulticastEvent<VehicleSpawnAssetArgs>(typeof(SpawnTableUtil), nameof(OnVehicleSpawnAssetRemoved));
    internal static readonly CachedMulticastEvent<ItemSpawnAssetArgs> EventOnItemSpawnAssetRemoved = new CachedMulticastEvent<ItemSpawnAssetArgs>(typeof(SpawnTableUtil), nameof(OnItemSpawnAssetRemoved));
    internal static readonly CachedMulticastEvent<ZombieSpawnAssetArgs> EventOnZombieSpawnAssetRemoved = new CachedMulticastEvent<ZombieSpawnAssetArgs>(typeof(SpawnTableUtil), nameof(OnZombieSpawnAssetRemoved));
    internal static readonly CachedMulticastEvent<SpawnAssetArgs> EventOnSpawnAssetRemoved = new CachedMulticastEvent<SpawnAssetArgs>(typeof(SpawnTableUtil), nameof(OnSpawnAssetRemoved));
    
    internal static readonly CachedMulticastEvent<AnimalSpawnAssetArgs> EventOnAnimalSpawnAssetAdded = new CachedMulticastEvent<AnimalSpawnAssetArgs>(typeof(SpawnTableUtil), nameof(OnAnimalSpawnAssetAdded));
    internal static readonly CachedMulticastEvent<VehicleSpawnAssetArgs> EventOnVehicleSpawnAssetAdded = new CachedMulticastEvent<VehicleSpawnAssetArgs>(typeof(SpawnTableUtil), nameof(OnVehicleSpawnAssetAdded));
    internal static readonly CachedMulticastEvent<ItemSpawnAssetArgs> EventOnItemSpawnAssetAdded = new CachedMulticastEvent<ItemSpawnAssetArgs>(typeof(SpawnTableUtil), nameof(OnItemSpawnAssetAdded));
    internal static readonly CachedMulticastEvent<ZombieSpawnAssetArgs> EventOnZombieSpawnAssetAdded = new CachedMulticastEvent<ZombieSpawnAssetArgs>(typeof(SpawnTableUtil), nameof(OnZombieSpawnAssetAdded));
    internal static readonly CachedMulticastEvent<SpawnAssetArgs> EventOnSpawnAssetAdded = new CachedMulticastEvent<SpawnAssetArgs>(typeof(SpawnTableUtil), nameof(OnSpawnAssetAdded));
    
    internal static readonly CachedMulticastEvent<AnimalSpawnAssetArgs> EventOnAnimalSpawnTierAssetUpdated = new CachedMulticastEvent<AnimalSpawnAssetArgs>(typeof(SpawnTableUtil), nameof(OnAnimalSpawnTierAssetUpdated));
    internal static readonly CachedMulticastEvent<VehicleSpawnAssetArgs> EventOnVehicleSpawnTierAssetUpdated = new CachedMulticastEvent<VehicleSpawnAssetArgs>(typeof(SpawnTableUtil), nameof(OnVehicleSpawnTierAssetUpdated));
    internal static readonly CachedMulticastEvent<ItemSpawnAssetArgs> EventOnItemSpawnTierAssetUpdated = new CachedMulticastEvent<ItemSpawnAssetArgs>(typeof(SpawnTableUtil), nameof(OnItemSpawnTierAssetUpdated));
    internal static readonly CachedMulticastEvent<ZombieSpawnAssetArgs> EventOnZombieSpawnTierAssetUpdated = new CachedMulticastEvent<ZombieSpawnAssetArgs>(typeof(SpawnTableUtil), nameof(OnZombieSpawnTierAssetUpdated));
    internal static readonly CachedMulticastEvent<SpawnAssetArgs> EventOnSpawnTierAssetUpdated = new CachedMulticastEvent<SpawnAssetArgs>(typeof(SpawnTableUtil), nameof(OnSpawnTierAssetUpdated));
    
    internal static readonly CachedMulticastEvent<AnimalSpawnAssetIndexUpdated> EventOnAnimalSpawnAssetIndexUpdated = new CachedMulticastEvent<AnimalSpawnAssetIndexUpdated>(typeof(SpawnTableUtil), nameof(OnAnimalSpawnAssetIndexUpdated));
    internal static readonly CachedMulticastEvent<VehicleSpawnAssetIndexUpdated> EventOnVehicleSpawnAssetIndexUpdated = new CachedMulticastEvent<VehicleSpawnAssetIndexUpdated>(typeof(SpawnTableUtil), nameof(OnVehicleSpawnAssetIndexUpdated));
    internal static readonly CachedMulticastEvent<ItemSpawnAssetIndexUpdated> EventOnItemSpawnAssetIndexUpdated = new CachedMulticastEvent<ItemSpawnAssetIndexUpdated>(typeof(SpawnTableUtil), nameof(OnItemSpawnAssetIndexUpdated));
    internal static readonly CachedMulticastEvent<ZombieSpawnAssetIndexUpdated> EventOnZombieSpawnAssetIndexUpdated = new CachedMulticastEvent<ZombieSpawnAssetIndexUpdated>(typeof(SpawnTableUtil), nameof(OnZombieSpawnAssetIndexUpdated));
    internal static readonly CachedMulticastEvent<SpawnAssetIndexUpdated> EventOnSpawnAssetIndexUpdated = new CachedMulticastEvent<SpawnAssetIndexUpdated>(typeof(SpawnTableUtil), nameof(OnSpawnAssetIndexUpdated));

    /// <summary>
    /// Invoked when an <see cref="AnimalTable"/>'s name is changed locally by any player.
    /// </summary>
    public static event AnimalSpawnTableArgs OnAnimalSpawnTableNameUpdated
    {
        add => EventOnAnimalSpawnTableNameUpdated.Add(value);
        remove => EventOnAnimalSpawnTableNameUpdated.Remove(value);
    }

    /// <summary>
    /// Invoked when a <see cref="VehicleTable"/>'s name is changed locally by any player.
    /// </summary>
    public static event VehicleSpawnTableArgs OnVehicleSpawnTableNameUpdated
    {
        add => EventOnVehicleSpawnTableNameUpdated.Add(value);
        remove => EventOnVehicleSpawnTableNameUpdated.Remove(value);
    }

    /// <summary>
    /// Invoked when an <see cref="ItemTable"/>'s name is changed locally by any player.
    /// </summary>
    public static event ItemSpawnTableArgs OnItemSpawnTableNameUpdated
    {
        add => EventOnItemSpawnTableNameUpdated.Add(value);
        remove => EventOnItemSpawnTableNameUpdated.Remove(value);
    }

    /// <summary>
    /// Invoked when a <see cref="ZombieTable"/>'s name is changed locally by any player.
    /// </summary>
    public static event ZombieSpawnTableArgs OnZombieSpawnTableNameUpdated
    {
        add => EventOnZombieSpawnTableNameUpdated.Add(value);
        remove => EventOnZombieSpawnTableNameUpdated.Remove(value);
    }

    /// <summary>
    /// Invoked when any spawn table's name is changed locally by any player.
    /// </summary>
    public static event SpawnTableArgs OnSpawnTableNameUpdated
    {
        add => EventOnSpawnTableNameUpdated.Add(value);
        remove => EventOnSpawnTableNameUpdated.Remove(value);
    }


    /// <summary>
    /// Invoked when an <see cref="AnimalTier"/>'s name is changed locally by any player.
    /// </summary>
    public static event AnimalSpawnTierArgs OnAnimalSpawnTierNameUpdated
    {
        add => EventOnAnimalSpawnTierNameUpdated.Add(value);
        remove => EventOnAnimalSpawnTierNameUpdated.Remove(value);
    }

    /// <summary>
    /// Invoked when a <see cref="VehicleTier"/>'s name is changed locally by any player.
    /// </summary>
    public static event VehicleSpawnTierArgs OnVehicleSpawnTierNameUpdated
    {
        add => EventOnVehicleSpawnTierNameUpdated.Add(value);
        remove => EventOnVehicleSpawnTierNameUpdated.Remove(value);
    }

    /// <summary>
    /// Invoked when an <see cref="ItemTier"/>'s name is changed locally by any player.
    /// </summary>
    public static event ItemSpawnTierArgs OnItemSpawnTierNameUpdated
    {
        add => EventOnItemSpawnTierNameUpdated.Add(value);
        remove => EventOnItemSpawnTierNameUpdated.Remove(value);
    }

    /// <summary>
    /// Invoked when any spawn table tier's name is changed locally by any player. Zombie tiers (<see cref="ZombieSlot"/>) can not be renamed.
    /// </summary>
    public static event SpawnTierArgs OnSpawnTierNameUpdated
    {
        add => EventOnSpawnTierNameUpdated.Add(value);
        remove => EventOnSpawnTierNameUpdated.Remove(value);
    }


    /// <summary>
    /// Invoked when an <see cref="AnimalTier"/>'s chance is changed locally by any player.
    /// </summary>
    public static event AnimalSpawnTierArgs OnAnimalSpawnTierChanceUpdated
    {
        add => EventOnAnimalSpawnTierChanceUpdated.Add(value);
        remove => EventOnAnimalSpawnTierChanceUpdated.Remove(value);
    }

    /// <summary>
    /// Invoked when a <see cref="VehicleTier"/>'s chance is changed locally by any player.
    /// </summary>
    public static event VehicleSpawnTierArgs OnVehicleSpawnTierChanceUpdated
    {
        add => EventOnVehicleSpawnTierChanceUpdated.Add(value);
        remove => EventOnVehicleSpawnTierChanceUpdated.Remove(value);
    }

    /// <summary>
    /// Invoked when an <see cref="ItemTier"/>'s chance is changed locally by any player.
    /// </summary>
    public static event ItemSpawnTierArgs OnItemSpawnTierChanceUpdated
    {
        add => EventOnItemSpawnTierChanceUpdated.Add(value);
        remove => EventOnItemSpawnTierChanceUpdated.Remove(value);
    }

    /// <summary>
    /// Invoked when a <see cref="ZombieSlot"/>'s chance is changed locally by any player.
    /// </summary>
    public static event ZombieSpawnTierArgs OnZombieSpawnTierChanceUpdated
    {
        add => EventOnZombieSpawnTierChanceUpdated.Add(value);
        remove => EventOnZombieSpawnTierChanceUpdated.Remove(value);
    }

    /// <summary>
    /// Invoked when any spawn table tier's chance is changed locally by any player.
    /// </summary>
    public static event SpawnTierArgs OnSpawnTierChanceUpdated
    {
        add => EventOnSpawnTierChanceUpdated.Add(value);
        remove => EventOnSpawnTierChanceUpdated.Remove(value);
    }


    /// <summary>
    /// Invoked when an <see cref="AnimalTable"/> is added locally by any player.
    /// </summary>
    public static event AnimalSpawnTableArgs OnAnimalSpawnTableAdded
    {
        add => EventOnAnimalSpawnTableAdded.Add(value);
        remove => EventOnAnimalSpawnTableAdded.Remove(value);
    }

    /// <summary>
    /// Invoked when a <see cref="VehicleTable"/> is added locally by any player.
    /// </summary>
    public static event VehicleSpawnTableArgs OnVehicleSpawnTableAdded
    {
        add => EventOnVehicleSpawnTableAdded.Add(value);
        remove => EventOnVehicleSpawnTableAdded.Remove(value);
    }

    /// <summary>
    /// Invoked when an <see cref="ItemTable"/> is added locally by any player.
    /// </summary>
    public static event ItemSpawnTableArgs OnItemSpawnTableAdded
    {
        add => EventOnItemSpawnTableAdded.Add(value);
        remove => EventOnItemSpawnTableAdded.Remove(value);
    }

    /// <summary>
    /// Invoked when a <see cref="ZombieTable"/> is added locally by any player.
    /// </summary>
    public static event ZombieSpawnTableArgs OnZombieSpawnTableAdded
    {
        add => EventOnZombieSpawnTableAdded.Add(value);
        remove => EventOnZombieSpawnTableAdded.Remove(value);
    }

    /// <summary>
    /// Invoked when any spawn table is added locally by any player.
    /// </summary>
    public static event SpawnTableArgs OnSpawnTableAdded
    {
        add => EventOnSpawnTableAdded.Add(value);
        remove => EventOnSpawnTableAdded.Remove(value);
    }


    /// <summary>
    /// Invoked when an <see cref="AnimalTier"/> is added locally by any player. Also invoked if it's in a newly added <see cref="AnimalTable"/>.
    /// </summary>
    public static event AnimalSpawnTierArgs OnAnimalSpawnTierAdded
    {
        add => EventOnAnimalSpawnTierAdded.Add(value);
        remove => EventOnAnimalSpawnTierAdded.Remove(value);
    }

    /// <summary>
    /// Invoked when a <see cref="VehicleTier"/> is added locally by any player. Also invoked if it's in a newly added <see cref="VehicleTable"/>.
    /// </summary>
    public static event VehicleSpawnTierArgs OnVehicleSpawnTierAdded
    {
        add => EventOnVehicleSpawnTierAdded.Add(value);
        remove => EventOnVehicleSpawnTierAdded.Remove(value);
    }

    /// <summary>
    /// Invoked when an <see cref="ItemTier"/> is added locally by any player. Also invoked if it's in a newly added <see cref="ItemTable"/>.
    /// </summary>
    public static event ItemSpawnTierArgs OnItemSpawnTierAdded
    {
        add => EventOnItemSpawnTierAdded.Add(value);
        remove => EventOnItemSpawnTierAdded.Remove(value);
    }

    /// <summary>
    /// Invoked when a <see cref="ZombieSlot"/> is added locally by any player. This can only happen when a <see cref="ZombieTable"/> is added as players can not add slots.
    /// </summary>
    public static event ZombieSpawnTierArgs OnZombieSpawnTierAdded
    {
        add => EventOnZombieSpawnTierAdded.Add(value);
        remove => EventOnZombieSpawnTierAdded.Remove(value);
    }

    /// <summary>
    /// Invoked when any spawn table tier is added locally by any player. Also invoked if it's in a newly added spawn table.
    /// </summary>
    public static event SpawnTierArgs OnSpawnTierAdded
    {
        add => EventOnSpawnTierAdded.Add(value);
        remove => EventOnSpawnTierAdded.Remove(value);
    }


    /// <summary>
    /// Invoked when an <see cref="AnimalTable"/>'s color is changed locally by any player.
    /// </summary>
    public static event AnimalSpawnTableArgs OnAnimalSpawnTableColorUpdated
    {
        add => EventOnAnimalSpawnTableColorUpdated.Add(value);
        remove => EventOnAnimalSpawnTableColorUpdated.Remove(value);
    }

    /// <summary>
    /// Invoked when a <see cref="VehicleTable"/>'s color is changed locally by any player.
    /// </summary>
    public static event VehicleSpawnTableArgs OnVehicleSpawnTableColorUpdated
    {
        add => EventOnVehicleSpawnTableColorUpdated.Add(value);
        remove => EventOnVehicleSpawnTableColorUpdated.Remove(value);
    }

    /// <summary>
    /// Invoked when an <see cref="ItemTable"/>'s color is changed locally by any player.
    /// </summary>
    public static event ItemSpawnTableArgs OnItemSpawnTableColorUpdated
    {
        add => EventOnItemSpawnTableColorUpdated.Add(value);
        remove => EventOnItemSpawnTableColorUpdated.Remove(value);
    }

    /// <summary>
    /// Invoked when a <see cref="ZombieTable"/>'s color is changed locally by any player.
    /// </summary>
    public static event ZombieSpawnTableArgs OnZombieSpawnTableColorUpdated
    {
        add => EventOnZombieSpawnTableColorUpdated.Add(value);
        remove => EventOnZombieSpawnTableColorUpdated.Remove(value);
    }

    /// <summary>
    /// Invoked when any spawn table's color is changed locally by any player.
    /// </summary>
    public static event SpawnTableArgs OnSpawnTableColorUpdated
    {
        add => EventOnSpawnTableColorUpdated.Add(value);
        remove => EventOnSpawnTableColorUpdated.Remove(value);
    }


    /// <summary>
    /// Invoked when an <see cref="AnimalTable"/>'s <c>tableID</c> is changed locally by any player.
    /// </summary>
    public static event AnimalSpawnTableArgs OnAnimalSpawnTableSpawnAssetUpdated
    {
        add => EventOnAnimalSpawnTableSpawnAssetUpdated.Add(value);
        remove => EventOnAnimalSpawnTableSpawnAssetUpdated.Remove(value);
    }

    /// <summary>
    /// Invoked when a <see cref="VehicleTable"/>'s <c>tableID</c> is changed locally by any player.
    /// </summary>
    public static event VehicleSpawnTableArgs OnVehicleSpawnTableSpawnAssetUpdated
    {
        add => EventOnVehicleSpawnTableSpawnAssetUpdated.Add(value);
        remove => EventOnVehicleSpawnTableSpawnAssetUpdated.Remove(value);
    }

    /// <summary>
    /// Invoked when an <see cref="ItemTable"/>'s <c>tableID</c> is changed locally by any player.
    /// </summary>
    public static event ItemSpawnTableArgs OnItemSpawnTableSpawnAssetUpdated
    {
        add => EventOnItemSpawnTableSpawnAssetUpdated.Add(value);
        remove => EventOnItemSpawnTableSpawnAssetUpdated.Remove(value);
    }

    /// <summary>
    /// Invoked when a <see cref="ZombieTable"/>'s <c>lootID</c> is changed locally by any player.
    /// </summary>
    public static event ZombieSpawnTableArgs OnZombieSpawnTableSpawnAssetUpdated
    {
        add => EventOnZombieSpawnTableSpawnAssetUpdated.Add(value);
        remove => EventOnZombieSpawnTableSpawnAssetUpdated.Remove(value);
    }

    /// <summary>
    /// Invoked when any spawn table's <c>tableID</c> (or <see cref="ZombieTable.lootID"/> for zombies) is changed locally by any player.
    /// </summary>
    public static event SpawnTableArgs OnSpawnTableSpawnAssetUpdated
    {
        add => EventOnSpawnTableSpawnAssetUpdated.Add(value);
        remove => EventOnSpawnTableSpawnAssetUpdated.Remove(value);
    }


    /// <summary>
    /// Invoked when a <see cref="ZombieTable"/>'s <c>isMega</c> is changed locally by any player.
    /// </summary>
    public static event ZombieSpawnTableArgs OnZombieSpawnTableIsMegaUpdated
    {
        add => EventOnZombieSpawnTableIsMegaUpdated.Add(value);
        remove => EventOnZombieSpawnTableIsMegaUpdated.Remove(value);
    }

    /// <summary>
    /// Invoked when a <see cref="ZombieTable"/>'s <c>health</c> is changed locally by any player.
    /// </summary>
    public static event ZombieSpawnTableArgs OnZombieSpawnTableHealthUpdated
    {
        add => EventOnZombieSpawnTableHealthUpdated.Add(value);
        remove => EventOnZombieSpawnTableHealthUpdated.Remove(value);
    }

    /// <summary>
    /// Invoked when a <see cref="ZombieTable"/>'s <c>damage</c> is changed locally by any player.
    /// </summary>
    public static event ZombieSpawnTableArgs OnZombieSpawnTableDamageUpdated
    {
        add => EventOnZombieSpawnTableDamageUpdated.Add(value);
        remove => EventOnZombieSpawnTableDamageUpdated.Remove(value);
    }

    /// <summary>
    /// Invoked when a <see cref="ZombieTable"/>'s <c>lootIndex</c> is changed locally by any player.
    /// </summary>
    public static event ZombieSpawnTableArgs OnZombieSpawnTableLootIndexUpdated
    {
        add => EventOnZombieSpawnTableLootIndexUpdated.Add(value);
        remove => EventOnZombieSpawnTableLootIndexUpdated.Remove(value);
    }

    /// <summary>
    /// Invoked when a <see cref="ZombieTable"/>'s <c>xp</c> is changed locally by any player.
    /// </summary>
    public static event ZombieSpawnTableArgs OnZombieSpawnTableXPUpdated
    {
        add => EventOnZombieSpawnTableXPUpdated.Add(value);
        remove => EventOnZombieSpawnTableXPUpdated.Remove(value);
    }

    /// <summary>
    /// Invoked when a <see cref="ZombieTable"/>'s <c>regen</c> is changed locally by any player.
    /// </summary>
    public static event ZombieSpawnTableArgs OnZombieSpawnTableRegenUpdated
    {
        add => EventOnZombieSpawnTableRegenUpdated.Add(value);
        remove => EventOnZombieSpawnTableRegenUpdated.Remove(value);
    }

    /// <summary>
    /// Invoked when a <see cref="ZombieTable"/>'s difficulty asset is changed locally by any player.
    /// </summary>
    public static event ZombieSpawnTableArgs OnZombieSpawnTableDifficultyAssetUpdated
    {
        add => EventOnZombieSpawnTableDifficultyAssetUpdated.Add(value);
        remove => EventOnZombieSpawnTableDifficultyAssetUpdated.Remove(value);
    }


    /// <summary>
    /// Invoked when an <see cref="AnimalTier"/> is removed by any player. Also invoked when the parent <see cref="AnimalTable"/> is removed.
    /// </summary>
    public static event AnimalSpawnTierArgs OnAnimalSpawnTierRemoved
    {
        add => EventOnAnimalSpawnTierRemoved.Add(value);
        remove => EventOnAnimalSpawnTierRemoved.Remove(value);
    }

    /// <summary>
    /// Invoked when a <see cref="VehicleTier"/> is removed by any player. Also invoked when the parent <see cref="VehicleTable"/> is removed.
    /// </summary>
    public static event VehicleSpawnTierArgs OnVehicleSpawnTierRemoved
    {
        add => EventOnVehicleSpawnTierRemoved.Add(value);
        remove => EventOnVehicleSpawnTierRemoved.Remove(value);
    }

    /// <summary>
    /// Invoked when an <see cref="ItemTier"/> is removed by any player. Also invoked when the parent <see cref="ItemTable"/> is removed.
    /// </summary>
    public static event ItemSpawnTierArgs OnItemSpawnTierRemoved
    {
        add => EventOnItemSpawnTierRemoved.Add(value);
        remove => EventOnItemSpawnTierRemoved.Remove(value);
    }

    /// <summary>
    /// Invoked when a <see cref="ZombieSlot"/> is removed by any player. Also invoked when the parent <see cref="ZombieTable"/> is removed.
    /// </summary>
    public static event ZombieSpawnTierArgs OnZombieSpawnTierRemoved
    {
        add => EventOnZombieSpawnTierRemoved.Add(value);
        remove => EventOnZombieSpawnTierRemoved.Remove(value);
    }

    /// <summary>
    /// Invoked when any spawn table tier is removed by any player. Also invoked when the parent spawn table is removed.
    /// </summary>
    public static event SpawnTierArgs OnSpawnTierRemoved
    {
        add => EventOnSpawnTierRemoved.Add(value);
        remove => EventOnSpawnTierRemoved.Remove(value);
    }


    /// <summary>
    /// Invoked when the index of an <see cref="AnimalTier"/> is updated. Also invoked when the index of the parent <see cref="AnimalTable"/> is updated.
    /// </summary>
    public static event AnimalSpawnTierIndexUpdated OnAnimalSpawnTierIndexUpdated
    {
        add => EventOnAnimalSpawnTierIndexUpdated.Add(value);
        remove => EventOnAnimalSpawnTierIndexUpdated.Remove(value);
    }

    /// <summary>
    /// Invoked when the index of a <see cref="VehicleTier"/> is updated. Also invoked when the index of the parent <see cref="VehicleTable"/> is updated.
    /// </summary>
    public static event VehicleSpawnTierIndexUpdated OnVehicleSpawnTierIndexUpdated
    {
        add => EventOnVehicleSpawnTierIndexUpdated.Add(value);
        remove => EventOnVehicleSpawnTierIndexUpdated.Remove(value);
    }

    /// <summary>
    /// Invoked when the index of an <see cref="ItemTier"/> is updated. Also invoked when the index of the parent <see cref="ItemTable"/> is updated.
    /// </summary>
    public static event ItemSpawnTierIndexUpdated OnItemSpawnTierIndexUpdated
    {
        add => EventOnItemSpawnTierIndexUpdated.Add(value);
        remove => EventOnItemSpawnTierIndexUpdated.Remove(value);
    }

    /// <summary>
    /// Invoked when the index of a <see cref="ZombieSlot"/> is updated. This will only be invoked when the index of the parent <see cref="ZombieTable"/> is updated as slots can't be removed or reordered.
    /// </summary>
    public static event ZombieSpawnTierIndexUpdated OnZombieSpawnTierIndexUpdated
    {
        add => EventOnZombieSpawnTierIndexUpdated.Add(value);
        remove => EventOnZombieSpawnTierIndexUpdated.Remove(value);
    }

    /// <summary>
    /// Invoked when the index of any spawn table tier is updated. Also invoked when the index of the parent spawn table is updated.
    /// </summary>
    public static event SpawnTierIndexUpdated OnSpawnTierIndexUpdated
    {
        add => EventOnSpawnTierIndexUpdated.Add(value);
        remove => EventOnSpawnTierIndexUpdated.Remove(value);
    }


    /// <summary>
    /// Invoked when an <see cref="AnimalTable"/> is removed by any player.
    /// </summary>
    public static event AnimalSpawnTableArgs OnAnimalSpawnTableRemoved
    {
        add => EventOnAnimalSpawnTableRemoved.Add(value);
        remove => EventOnAnimalSpawnTableRemoved.Remove(value);
    }

    /// <summary>
    /// Invoked when a <see cref="VehicleTable"/> is removed by any player.
    /// </summary>
    public static event VehicleSpawnTableArgs OnVehicleSpawnTableRemoved
    {
        add => EventOnVehicleSpawnTableRemoved.Add(value);
        remove => EventOnVehicleSpawnTableRemoved.Remove(value);
    }

    /// <summary>
    /// Invoked when an <see cref="ItemTable"/> is removed by any player.
    /// </summary>
    public static event ItemSpawnTableArgs OnItemSpawnTableRemoved
    {
        add => EventOnItemSpawnTableRemoved.Add(value);
        remove => EventOnItemSpawnTableRemoved.Remove(value);
    }

    /// <summary>
    /// Invoked when a <see cref="ZombieTable"/> is removed by any player.
    /// </summary>
    public static event ZombieSpawnTableArgs OnZombieSpawnTableRemoved
    {
        add => EventOnZombieSpawnTableRemoved.Add(value);
        remove => EventOnZombieSpawnTableRemoved.Remove(value);
    }

    /// <summary>
    /// Invoked when any spawn table is removed by any player.
    /// </summary>
    public static event SpawnTableArgs OnSpawnTableRemoved
    {
        add => EventOnSpawnTableRemoved.Add(value);
        remove => EventOnSpawnTableRemoved.Remove(value);
    }


    /// <summary>
    /// Invoked when the index of an <see cref="AnimalTable"/> is updated.
    /// </summary>
    public static event AnimalSpawnTableIndexUpdated OnAnimalSpawnTableIndexUpdated
    {
        add => EventOnAnimalSpawnTableIndexUpdated.Add(value);
        remove => EventOnAnimalSpawnTableIndexUpdated.Remove(value);
    }

    /// <summary>
    /// Invoked when the index of a <see cref="VehicleTable"/> is updated.
    /// </summary>
    public static event VehicleSpawnTableIndexUpdated OnVehicleSpawnTableIndexUpdated
    {
        add => EventOnVehicleSpawnTableIndexUpdated.Add(value);
        remove => EventOnVehicleSpawnTableIndexUpdated.Remove(value);
    }

    /// <summary>
    /// Invoked when the index of an <see cref="ItemTable"/> is updated.
    /// </summary>
    public static event ItemSpawnTableIndexUpdated OnItemSpawnTableIndexUpdated
    {
        add => EventOnItemSpawnTableIndexUpdated.Add(value);
        remove => EventOnItemSpawnTableIndexUpdated.Remove(value);
    }

    /// <summary>
    /// Invoked when the index of a <see cref="ZombieTable"/> is updated.
    /// </summary>
    public static event ZombieSpawnTableIndexUpdated OnZombieSpawnTableIndexUpdated
    {
        add => EventOnZombieSpawnTableIndexUpdated.Add(value);
        remove => EventOnZombieSpawnTableIndexUpdated.Remove(value);
    }

    /// <summary>
    /// Invoked when the index of any spawn table is updated.
    /// </summary>
    public static event SpawnTableIndexUpdated OnSpawnTableIndexUpdated
    {
        add => EventOnSpawnTableIndexUpdated.Add(value);
        remove => EventOnSpawnTableIndexUpdated.Remove(value);
    }


    /// <summary>
    /// Invoked when an <see cref="AnimalSpawn"/> is removed by any player. Also invoked when the parent <see cref="AnimalTable"/> or <see cref="AnimalTier"/> is removed.
    /// </summary>
    public static event AnimalSpawnAssetArgs OnAnimalSpawnAssetRemoved
    {
        add => EventOnAnimalSpawnAssetRemoved.Add(value);
        remove => EventOnAnimalSpawnAssetRemoved.Remove(value);
    }

    /// <summary>
    /// Invoked when a <see cref="VehicleSpawn"/> is removed by any player. Also invoked when the parent <see cref="VehicleTable"/> or <see cref="VehicleTier"/> is removed.
    /// </summary>
    public static event VehicleSpawnAssetArgs OnVehicleSpawnAssetRemoved
    {
        add => EventOnVehicleSpawnAssetRemoved.Add(value);
        remove => EventOnVehicleSpawnAssetRemoved.Remove(value);
    }

    /// <summary>
    /// Invoked when an <see cref="ItemSpawn"/> is removed by any player. Also invoked when the parent <see cref="ItemTable"/> or <see cref="ItemTier"/> is removed.
    /// </summary>
    public static event ItemSpawnAssetArgs OnItemSpawnAssetRemoved
    {
        add => EventOnItemSpawnAssetRemoved.Add(value);
        remove => EventOnItemSpawnAssetRemoved.Remove(value);
    }

    /// <summary>
    /// Invoked when a <see cref="ZombieCloth"/> is removed by any player. Also invoked when the parent <see cref="ZombieTable"/> is removed.
    /// </summary>
    public static event ZombieSpawnAssetArgs OnZombieSpawnAssetRemoved
    {
        add => EventOnZombieSpawnAssetRemoved.Add(value);
        remove => EventOnZombieSpawnAssetRemoved.Remove(value);
    }

    /// <summary>
    /// Invoked when any spawn table tier asset is removed by any player. Also invoked when the parent spawn table or spawn table tier is removed.
    /// </summary>
    public static event SpawnAssetArgs OnSpawnAssetRemoved
    {
        add => EventOnSpawnAssetRemoved.Add(value);
        remove => EventOnSpawnAssetRemoved.Remove(value);
    }


    /// <summary>
    /// Invoked when an <see cref="AnimalSpawn"/> is added by any player. Also invoked when the parent <see cref="AnimalTable"/> or <see cref="AnimalTier"/> is added.
    /// </summary>
    public static event AnimalSpawnAssetArgs OnAnimalSpawnAssetAdded
    {
        add => EventOnAnimalSpawnAssetAdded.Add(value);
        remove => EventOnAnimalSpawnAssetAdded.Remove(value);
    }

    /// <summary>
    /// Invoked when a <see cref="VehicleSpawn"/> is added by any player. Also invoked when the parent <see cref="VehicleTable"/> or <see cref="VehicleTier"/> is added.
    /// </summary>
    public static event VehicleSpawnAssetArgs OnVehicleSpawnAssetAdded
    {
        add => EventOnVehicleSpawnAssetAdded.Add(value);
        remove => EventOnVehicleSpawnAssetAdded.Remove(value);
    }

    /// <summary>
    /// Invoked when an <see cref="ItemSpawn"/> is added by any player. Also invoked when the parent <see cref="ItemTable"/> or <see cref="ItemTier"/> is added.
    /// </summary>
    public static event ItemSpawnAssetArgs OnItemSpawnAssetAdded
    {
        add => EventOnItemSpawnAssetAdded.Add(value);
        remove => EventOnItemSpawnAssetAdded.Remove(value);
    }

    /// <summary>
    /// Invoked when a <see cref="ZombieCloth"/> is added by any player. Also invoked when the parent <see cref="ZombieTable"/> is added.
    /// </summary>
    public static event ZombieSpawnAssetArgs OnZombieSpawnAssetAdded
    {
        add => EventOnZombieSpawnAssetAdded.Add(value);
        remove => EventOnZombieSpawnAssetAdded.Remove(value);
    }

    /// <summary>
    /// Invoked when any spawn table tier asset is added by any player. Also invoked when the parent spawn table or spawn table tier is added.
    /// </summary>
    public static event SpawnAssetArgs OnSpawnAssetAdded
    {
        add => EventOnSpawnAssetAdded.Add(value);
        remove => EventOnSpawnAssetAdded.Remove(value);
    }


    /// <summary>
    /// Invoked when an <see cref="AnimalSpawn"/> is updated by any player.
    /// </summary>
    public static event AnimalSpawnAssetArgs OnAnimalSpawnTierAssetUpdated
    {
        add => EventOnAnimalSpawnTierAssetUpdated.Add(value);
        remove => EventOnAnimalSpawnTierAssetUpdated.Remove(value);
    }

    /// <summary>
    /// Invoked when a <see cref="VehicleSpawn"/> is updated by any player.
    /// </summary>
    public static event VehicleSpawnAssetArgs OnVehicleSpawnTierAssetUpdated
    {
        add => EventOnVehicleSpawnTierAssetUpdated.Add(value);
        remove => EventOnVehicleSpawnTierAssetUpdated.Remove(value);
    }

    /// <summary>
    /// Invoked when an <see cref="ItemSpawn"/> is updated by any player.
    /// </summary>
    public static event ItemSpawnAssetArgs OnItemSpawnTierAssetUpdated
    {
        add => EventOnItemSpawnTierAssetUpdated.Add(value);
        remove => EventOnItemSpawnTierAssetUpdated.Remove(value);
    }

    /// <summary>
    /// Invoked when a <see cref="ZombieCloth"/> is updated by any player.
    /// </summary>
    public static event ZombieSpawnAssetArgs OnZombieSpawnTierAssetUpdated
    {
        add => EventOnZombieSpawnTierAssetUpdated.Add(value);
        remove => EventOnZombieSpawnTierAssetUpdated.Remove(value);
    }

    /// <summary>
    /// Invoked when any spawn table tier asset is updated by any player.
    /// </summary>
    public static event SpawnAssetArgs OnSpawnTierAssetUpdated
    {
        add => EventOnSpawnTierAssetUpdated.Add(value);
        remove => EventOnSpawnTierAssetUpdated.Remove(value);
    }


    /// <summary>
    /// Invoked when the index of an <see cref="AnimalSpawn"/> is updated. Also invoked when the indexes of the parent <see cref="AnimalTable"/> or <see cref="AnimalTier"/> is updated.
    /// </summary>
    public static event AnimalSpawnAssetIndexUpdated OnAnimalSpawnAssetIndexUpdated
    {
        add => EventOnAnimalSpawnAssetIndexUpdated.Add(value);
        remove => EventOnAnimalSpawnAssetIndexUpdated.Remove(value);
    }

    /// <summary>
    /// Invoked when the index of a <see cref="VehicleSpawn"/> is updated. Also invoked when the indexes of the parent <see cref="VehicleTable"/> or <see cref="VehicleTier"/> is updated.
    /// </summary>
    public static event VehicleSpawnAssetIndexUpdated OnVehicleSpawnAssetIndexUpdated
    {
        add => EventOnVehicleSpawnAssetIndexUpdated.Add(value);
        remove => EventOnVehicleSpawnAssetIndexUpdated.Remove(value);
    }

    /// <summary>
    /// Invoked when the index of an <see cref="ItemSpawn"/> is updated. Also invoked when the indexes of the parent <see cref="ItemTable"/> or <see cref="ItemTier"/> is updated.
    /// </summary>
    public static event ItemSpawnAssetIndexUpdated OnItemSpawnAssetIndexUpdated
    {
        add => EventOnItemSpawnAssetIndexUpdated.Add(value);
        remove => EventOnItemSpawnAssetIndexUpdated.Remove(value);
    }

    /// <summary>
    /// Invoked when the index of a <see cref="ZombieCloth"/> is updated. Also invoked when the index of the parent <see cref="ZombieTable"/> is updated.
    /// </summary>
    public static event ZombieSpawnAssetIndexUpdated OnZombieSpawnAssetIndexUpdated
    {
        add => EventOnZombieSpawnAssetIndexUpdated.Add(value);
        remove => EventOnZombieSpawnAssetIndexUpdated.Remove(value);
    }

    /// <summary>
    /// Invoked when the index of any spawn table tier asset is updated. Also invoked when the indexes of the parent spawn table or spawn table tier is updated.
    /// </summary>
    public static event SpawnAssetIndexUpdated OnSpawnAssetIndexUpdated
    {
        add => EventOnSpawnAssetIndexUpdated.Add(value);
        remove => EventOnSpawnAssetIndexUpdated.Remove(value);
    }

#if CLIENT
    /// <summary>
    /// Is the local client currently editing this <paramref name="spawnType"/>?
    /// </summary>
    public static bool IsEditingSpawns(SpawnType spawnType)
    {
        if (!Level.isEditor || !EditorSpawns.isSpawning || spawnType is not SpawnType.Animal and not SpawnType.Player and not SpawnType.Vehicle and not SpawnType.Item and not SpawnType.Zombie)
            return false;

        if (UserInput.ActiveTool is DevkitServerSpawnsTool tool)
        {
            return tool.Type == spawnType;
        }

        (ESpawnMode a, ESpawnMode b) = spawnType switch
        {
            SpawnType.Animal => (ESpawnMode.ADD_ANIMAL, ESpawnMode.REMOVE_ANIMAL),
            SpawnType.Vehicle => (ESpawnMode.ADD_VEHICLE, ESpawnMode.REMOVE_VEHICLE),
            SpawnType.Item => (ESpawnMode.ADD_ITEM, ESpawnMode.REMOVE_ITEM),
            SpawnType.Zombie => (ESpawnMode.ADD_ZOMBIE, ESpawnMode.REMOVE_ZOMBIE),
            _ => (ESpawnMode.ADD_PLAYER, ESpawnMode.REMOVE_PLAYER)
        };

        return EditorSpawns.spawnMode == a || EditorSpawns.spawnMode == b;
    }

    /// <summary>
    /// Is the table at <paramref name="tableIndex"/> the selected table of the local client for <paramref name="spawnType"/>?
    /// </summary>
    public static bool IsTableSelected(SpawnType spawnType, int tableIndex) => tableIndex >= 0 && spawnType switch
    {
        SpawnType.Animal => EditorSpawns.selectedAnimal,
        SpawnType.Vehicle => EditorSpawns.selectedVehicle,
        SpawnType.Item => EditorSpawns.selectedItem,
        SpawnType.Zombie => EditorSpawns.selectedZombie,
        _ => -1
    } == tableIndex;

    /// <summary>
    /// Is the local client currently editing this <paramref name="spawnType"/> and do they have the table at <paramref name="tableIndex"/> selected?
    /// </summary>
    public static bool IsEditingSpawns(SpawnType spawnType, int tableIndex)
    {
        if (spawnType == SpawnType.Player || !IsEditingSpawns(spawnType))
            return false;

        return IsTableSelected(spawnType, tableIndex);
    }

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
    /// Deselect the currently selected spawn table (if any) for the given <paramref name="spawnType"/>.
    /// </summary>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool DeselectTable(SpawnType spawnType) => SelectTable(spawnType, byte.MaxValue, false, false);

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

                bool hasAssetRem = !EventOnAnimalSpawnAssetRemoved.IsEmpty || !EventOnSpawnAssetRemoved.IsEmpty;
                bool hasTierRem = !EventOnAnimalSpawnTierRemoved.IsEmpty || !EventOnSpawnTierRemoved.IsEmpty;

                if (hasTierRem || hasAssetRem || DevkitServerModule.IsDebug)
                {
                    for (int i = 0; i < maxCt; i++)
                    {
                        AnimalTier tier = animalTable.tiers[i];
                        SpawnTierIdentifier id = new SpawnTierIdentifier(spawnType, (byte)index, (byte)i);
                        if (hasAssetRem || DevkitServerModule.IsDebug)
                        {
                            int raw = id.Raw;
                            int maxAssetCt = Math.Min(byte.MaxValue - 1, tier.table.Count);

                            for (int j = 0; j < maxAssetCt; ++j)
                            {
                                SpawnAssetIdentifier assetId = new SpawnAssetIdentifier(raw | (j << 24));
                                EventOnAnimalSpawnAssetRemoved.TryInvoke(tier.table[j], assetId, HierarchicalEventSource.GrandparentObject);
                                EventOnSpawnAssetRemoved.TryInvoke(assetId, HierarchicalEventSource.GrandparentObject);
                                Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Animal spawn asset removed: {assetId.Format()} (parent removed).");
                            }
                        }
                        
                        EventOnAnimalSpawnTierRemoved.TryInvoke(tier, id, HierarchicalEventSource.ParentObject);
                        EventOnSpawnTierRemoved.TryInvoke(id, HierarchicalEventSource.ParentObject);
                        Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Animal spawn tier removed: {tier.name.Format()} ({id.Format()}) (parent removed).");
                    }
                }
                
                EventOnAnimalSpawnTableRemoved.TryInvoke(animalTable, index);
                EventOnSpawnTableRemoved.TryInvoke(spawnType, index);
                Logger.DevkitServer.LogDebug(nameof(RemoveSpawnTableLocal), $"Animal spawn table removed: {animalTable.name.Format()} (# {index.Format()}).");
                hasAssetRem = !EventOnAnimalSpawnAssetIndexUpdated.IsEmpty || !EventOnSpawnAssetIndexUpdated.IsEmpty;
                hasTierRem = !EventOnAnimalSpawnTierIndexUpdated.IsEmpty || !EventOnSpawnTierIndexUpdated.IsEmpty;
                if (EventOnAnimalSpawnTableIndexUpdated.IsEmpty && EventOnSpawnTableIndexUpdated.IsEmpty && !hasTierRem && !hasAssetRem && !DevkitServerModule.IsDebug)
                    break;

                for (int i = index; i < LevelAnimals.tables.Count; ++i)
                {
                    AnimalTable table2 = LevelAnimals.tables[i];
                    EventOnAnimalSpawnTableIndexUpdated.TryInvoke(table2, i + 1, i);
                    EventOnSpawnTableIndexUpdated.TryInvoke(spawnType, i + 1, i);
                    Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Animal spawn table index updated: {table2.name.Format()} (# {(i + 1).Format()} -> {i.Format()}).");
                    if (!hasTierRem && !hasAssetRem && !DevkitServerModule.IsDebug)
                        continue;

                    maxCt = Math.Min(byte.MaxValue - 1, table2.tiers.Count);

                    for (int k = 0; k < maxCt; k++)
                    {
                        AnimalTier tier = table2.tiers[k];
                        SpawnTierIdentifier id = new SpawnTierIdentifier(spawnType, (byte)i, (byte)k);
                        SpawnTierIdentifier idOld = new SpawnTierIdentifier(spawnType, (byte)(i + 1), (byte)k);

                        EventOnAnimalSpawnTierIndexUpdated.TryInvoke(tier, idOld, id, HierarchicalEventSource.ParentObject);
                        EventOnSpawnTierIndexUpdated.TryInvoke(idOld, id, HierarchicalEventSource.ParentObject);

                        if (hasAssetRem || DevkitServerModule.IsDebug)
                        {
                            int raw = id.Raw;
                            int rawOld = idOld.Raw;
                            int maxAssetCt = Math.Min(byte.MaxValue - 1, tier.table.Count);

                            for (int j = 0; j < maxAssetCt; ++j)
                            {
                                SpawnAssetIdentifier assetId = new SpawnAssetIdentifier(raw | (j << 24));
                                SpawnAssetIdentifier assetOldId = new SpawnAssetIdentifier(rawOld | (j << 24));
                                EventOnAnimalSpawnAssetIndexUpdated.TryInvoke(tier.table[j], assetOldId, assetId, HierarchicalEventSource.GrandparentObject);
                                EventOnSpawnAssetIndexUpdated.TryInvoke(assetOldId, assetId, HierarchicalEventSource.GrandparentObject);
                                Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Animal spawn asset index updated: {assetOldId.Format()} -> {assetId.Format()} (parent updated).");
                            }
                        }
                        Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Animal spawn tier index updated: {idOld.Format()} -> {id.Format()} (parent updated).");
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

                hasAssetRem = !EventOnVehicleSpawnAssetRemoved.IsEmpty || !EventOnSpawnAssetRemoved.IsEmpty;
                hasTierRem = !EventOnVehicleSpawnTierRemoved.IsEmpty || !EventOnSpawnTierRemoved.IsEmpty;

                if (hasTierRem || hasAssetRem || DevkitServerModule.IsDebug)
                {
                    for (int i = 0; i < maxCt; i++)
                    {
                        VehicleTier tier = vehicleTable.tiers[i];
                        SpawnTierIdentifier id = new SpawnTierIdentifier(spawnType, (byte)index, (byte)i);
                        if (hasAssetRem || DevkitServerModule.IsDebug)
                        {
                            int raw = id.Raw;
                            int maxAssetCt = Math.Min(byte.MaxValue - 1, tier.table.Count);

                            for (int j = 0; j < maxAssetCt; ++j)
                            {
                                SpawnAssetIdentifier assetId = new SpawnAssetIdentifier(raw | (j << 24));
                                EventOnVehicleSpawnAssetRemoved.TryInvoke(tier.table[j], assetId, HierarchicalEventSource.GrandparentObject);
                                EventOnSpawnAssetRemoved.TryInvoke(assetId, HierarchicalEventSource.GrandparentObject);
                                Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Vehicle spawn asset removed: {assetId.Format()} (parent removed).");
                            }
                        }

                        EventOnVehicleSpawnTierRemoved.TryInvoke(tier, id, HierarchicalEventSource.ParentObject);
                        EventOnSpawnTierRemoved.TryInvoke(id, HierarchicalEventSource.ParentObject);
                        Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Vehicle spawn tier removed: {tier.name.Format()} ({id.Format()}) (parent removed).");
                    }
                }

                EventOnVehicleSpawnTableRemoved.TryInvoke(vehicleTable, index);
                EventOnSpawnTableRemoved.TryInvoke(spawnType, index);
                Logger.DevkitServer.LogDebug(nameof(RemoveSpawnTableLocal), $"Vehicle spawn table removed: {vehicleTable.name.Format()} (# {index.Format()}).");
                hasAssetRem = !EventOnVehicleSpawnAssetIndexUpdated.IsEmpty || !EventOnSpawnAssetIndexUpdated.IsEmpty;
                hasTierRem = !EventOnVehicleSpawnTierIndexUpdated.IsEmpty || !EventOnSpawnTierIndexUpdated.IsEmpty;
                if (EventOnVehicleSpawnTableIndexUpdated.IsEmpty && EventOnSpawnTableIndexUpdated.IsEmpty && !hasTierRem && !hasAssetRem && !DevkitServerModule.IsDebug)
                    break;

                for (int i = index; i < LevelVehicles.tables.Count; ++i)
                {
                    VehicleTable table2 = LevelVehicles.tables[i];
                    EventOnVehicleSpawnTableIndexUpdated.TryInvoke(table2, i + 1, i);
                    EventOnSpawnTableIndexUpdated.TryInvoke(spawnType, i + 1, i);
                    Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Vehicle spawn table index updated: {table2.name.Format()} (# {(i + 1).Format()} -> {i.Format()}).");

                    if (!hasTierRem && !hasAssetRem && !DevkitServerModule.IsDebug)
                        continue;

                    maxCt = Math.Min(byte.MaxValue - 1, table2.tiers.Count);

                    for (int k = 0; k < maxCt; k++)
                    {
                        VehicleTier tier = table2.tiers[k];
                        SpawnTierIdentifier id = new SpawnTierIdentifier(spawnType, (byte)i, (byte)k);
                        SpawnTierIdentifier idOld = new SpawnTierIdentifier(spawnType, (byte)(i + 1), (byte)k);

                        EventOnVehicleSpawnTierIndexUpdated.TryInvoke(tier, idOld, id, HierarchicalEventSource.ParentObject);
                        EventOnSpawnTierIndexUpdated.TryInvoke(idOld, id, HierarchicalEventSource.ParentObject);

                        if (hasAssetRem || DevkitServerModule.IsDebug)
                        {
                            int raw = id.Raw;
                            int rawOld = idOld.Raw;
                            int maxAssetCt = Math.Min(byte.MaxValue - 1, tier.table.Count);

                            for (int j = 0; j < maxAssetCt; ++j)
                            {
                                SpawnAssetIdentifier assetId = new SpawnAssetIdentifier(raw | (j << 24));
                                SpawnAssetIdentifier assetOldId = new SpawnAssetIdentifier(rawOld | (j << 24));
                                EventOnVehicleSpawnAssetIndexUpdated.TryInvoke(tier.table[j], assetOldId, assetId, HierarchicalEventSource.GrandparentObject);
                                EventOnSpawnAssetIndexUpdated.TryInvoke(assetOldId, assetId, HierarchicalEventSource.GrandparentObject);
                                Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Vehicle spawn asset index updated: {assetOldId.Format()} -> {assetId.Format()} (parent updated).");
                            }
                        }
                        Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Vehicle spawn tier index updated: {idOld.Format()} -> {id.Format()} (parent updated).");
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

                hasAssetRem = !EventOnItemSpawnAssetRemoved.IsEmpty || !EventOnSpawnAssetRemoved.IsEmpty;
                hasTierRem = !EventOnItemSpawnTierRemoved.IsEmpty || !EventOnSpawnTierRemoved.IsEmpty;

                if (hasTierRem || hasAssetRem || DevkitServerModule.IsDebug)
                {
                    for (int i = 0; i < maxCt; i++)
                    {
                        ItemTier tier = itemTable.tiers[i];
                        SpawnTierIdentifier id = new SpawnTierIdentifier(spawnType, (byte)index, (byte)i);
                        if (hasAssetRem || DevkitServerModule.IsDebug)
                        {
                            int raw = id.Raw;
                            int maxAssetCt = Math.Min(byte.MaxValue - 1, tier.table.Count);

                            for (int j = 0; j < maxAssetCt; ++j)
                            {
                                SpawnAssetIdentifier assetId = new SpawnAssetIdentifier(raw | (j << 24));
                                EventOnItemSpawnAssetRemoved.TryInvoke(tier.table[j], assetId, HierarchicalEventSource.GrandparentObject);
                                EventOnSpawnAssetRemoved.TryInvoke(assetId, HierarchicalEventSource.GrandparentObject);
                                Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Item spawn asset removed: {assetId.Format()} (parent removed).");
                            }
                        }

                        EventOnItemSpawnTierRemoved.TryInvoke(tier, id, HierarchicalEventSource.ParentObject);
                        EventOnSpawnTierRemoved.TryInvoke(id, HierarchicalEventSource.ParentObject);
                        Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Item spawn tier removed: {tier.name.Format()} ({id.Format()}) (parent removed).");
                    }
                }

                EventOnItemSpawnTableRemoved.TryInvoke(itemTable, index);
                EventOnSpawnTableRemoved.TryInvoke(spawnType, index);
                Logger.DevkitServer.LogDebug(nameof(RemoveSpawnTableLocal), $"Item spawn table removed: {itemTable.name.Format()} (# {index.Format()}).");
                hasAssetRem = !EventOnItemSpawnAssetIndexUpdated.IsEmpty || !EventOnSpawnAssetIndexUpdated.IsEmpty;
                hasTierRem = !EventOnItemSpawnTierIndexUpdated.IsEmpty || !EventOnSpawnTierIndexUpdated.IsEmpty;
                if (EventOnItemSpawnTableIndexUpdated.IsEmpty && EventOnSpawnTableIndexUpdated.IsEmpty && !hasTierRem && !hasAssetRem && !DevkitServerModule.IsDebug)
                    break;

                for (int i = index; i < LevelItems.tables.Count; ++i)
                {
                    ItemTable table2 = LevelItems.tables[i];
                    EventOnItemSpawnTableIndexUpdated.TryInvoke(table2, i + 1, i);
                    EventOnSpawnTableIndexUpdated.TryInvoke(spawnType, i + 1, i);
                    Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Item spawn table index updated: {table2.name.Format()} (# {(i + 1).Format()} -> {i.Format()}).");

                    if (!hasTierRem && !hasAssetRem && !DevkitServerModule.IsDebug)
                        continue;

                    maxCt = Math.Min(byte.MaxValue - 1, table2.tiers.Count);

                    for (int k = 0; k < maxCt; k++)
                    {
                        ItemTier tier = table2.tiers[k];
                        SpawnTierIdentifier id = new SpawnTierIdentifier(spawnType, (byte)i, (byte)k);
                        SpawnTierIdentifier idOld = new SpawnTierIdentifier(spawnType, (byte)(i + 1), (byte)k);

                        EventOnItemSpawnTierIndexUpdated.TryInvoke(tier, idOld, id, HierarchicalEventSource.ParentObject);
                        EventOnSpawnTierIndexUpdated.TryInvoke(idOld, id, HierarchicalEventSource.ParentObject);

                        if (hasAssetRem || DevkitServerModule.IsDebug)
                        {
                            int raw = id.Raw;
                            int rawOld = idOld.Raw;
                            int maxAssetCt = Math.Min(byte.MaxValue - 1, tier.table.Count);

                            for (int j = 0; j < maxAssetCt; ++j)
                            {
                                SpawnAssetIdentifier assetId = new SpawnAssetIdentifier(raw | (j << 24));
                                SpawnAssetIdentifier assetOldId = new SpawnAssetIdentifier(rawOld | (j << 24));
                                EventOnItemSpawnAssetIndexUpdated.TryInvoke(tier.table[j], assetOldId, assetId, HierarchicalEventSource.GrandparentObject);
                                EventOnSpawnAssetIndexUpdated.TryInvoke(assetOldId, assetId, HierarchicalEventSource.GrandparentObject);
                                Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Item spawn asset index updated: {assetOldId.Format()} -> {assetId.Format()} (parent updated).");
                            }
                        }
                        Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Item spawn tier index updated: {idOld.Format()} -> {id.Format()} (parent updated).");
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

                hasAssetRem = !EventOnZombieSpawnAssetRemoved.IsEmpty || !EventOnSpawnAssetRemoved.IsEmpty;
                hasTierRem = !EventOnZombieSpawnTierRemoved.IsEmpty || !EventOnSpawnTierRemoved.IsEmpty;

                if (hasTierRem || hasAssetRem || DevkitServerModule.IsDebug)
                {
                    for (int i = 0; i < zombieTable.slots.Length; i++)
                    {
                        ZombieSlot tier = zombieTable.slots[i];
                        SpawnTierIdentifier id = new SpawnTierIdentifier(spawnType, (byte)index, (byte)i);
                        if (hasAssetRem || DevkitServerModule.IsDebug)
                        {
                            int raw = id.Raw;
                            int maxAssetCt = Math.Min(byte.MaxValue - 1, tier.table.Count);

                            for (int j = 0; j < maxAssetCt; ++j)
                            {
                                SpawnAssetIdentifier assetId = new SpawnAssetIdentifier(raw | (j << 24));
                                EventOnZombieSpawnAssetRemoved.TryInvoke(tier.table[j], assetId, HierarchicalEventSource.GrandparentObject);
                                EventOnSpawnAssetRemoved.TryInvoke(assetId, HierarchicalEventSource.GrandparentObject);
                                Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Zombie spawn asset removed: {assetId.Format()} (parent removed).");
                            }
                        }

                        EventOnZombieSpawnTierRemoved.TryInvoke(tier, id, HierarchicalEventSource.ParentObject);
                        EventOnSpawnTierRemoved.TryInvoke(id, HierarchicalEventSource.ParentObject);
                        Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Zombie spawn tier removed: {id.Format()} (parent removed).");
                    }
                }

                EventOnZombieSpawnTableRemoved.TryInvoke(zombieTable, index);
                EventOnSpawnTableRemoved.TryInvoke(spawnType, index);
                Logger.DevkitServer.LogDebug(nameof(RemoveSpawnTableLocal), $"Zombie spawn table removed: {zombieTable.name.Format()} (# {index.Format()}).");
                hasAssetRem = !EventOnZombieSpawnAssetIndexUpdated.IsEmpty || !EventOnSpawnAssetIndexUpdated.IsEmpty;
                hasTierRem = !EventOnZombieSpawnTierIndexUpdated.IsEmpty || !EventOnSpawnTierIndexUpdated.IsEmpty;
                if (EventOnZombieSpawnTableIndexUpdated.IsEmpty && EventOnSpawnTableIndexUpdated.IsEmpty && !hasTierRem && !hasAssetRem && !DevkitServerModule.IsDebug)
                    break;

                for (int i = index; i < LevelZombies.tables.Count; ++i)
                {
                    ZombieTable table2 = LevelZombies.tables[i];
                    EventOnZombieSpawnTableIndexUpdated.TryInvoke(table2, i + 1, i);
                    EventOnSpawnTableIndexUpdated.TryInvoke(spawnType, i + 1, i);
                    Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Zombie spawn table index updated: {table2.name.Format()} (# {(i + 1).Format()} -> {i.Format()}).");

                    if (!hasTierRem && !hasAssetRem && !DevkitServerModule.IsDebug)
                        continue;

                    for (int k = 0; k < table2.slots.Length; k++)
                    {
                        ZombieSlot tier = zombieTable.slots[k];
                        SpawnTierIdentifier id = new SpawnTierIdentifier(spawnType, (byte)i, (byte)k);
                        SpawnTierIdentifier idOld = new SpawnTierIdentifier(spawnType, (byte)(i + 1), (byte)k);

                        EventOnZombieSpawnTierIndexUpdated.TryInvoke(tier, idOld, id, HierarchicalEventSource.ParentObject);
                        EventOnSpawnTierIndexUpdated.TryInvoke(idOld, id, HierarchicalEventSource.ParentObject);

                        if (hasAssetRem || DevkitServerModule.IsDebug)
                        {
                            int raw = id.Raw;
                            int rawOld = idOld.Raw;
                            int maxAssetCt = Math.Min(byte.MaxValue - 1, tier.table.Count);

                            for (int j = 0; j < maxAssetCt; ++j)
                            {
                                SpawnAssetIdentifier assetId = new SpawnAssetIdentifier(raw | (j << 24));
                                SpawnAssetIdentifier assetOldId = new SpawnAssetIdentifier(rawOld | (j << 24));
                                EventOnZombieSpawnAssetIndexUpdated.TryInvoke(tier.table[j], assetOldId, assetId, HierarchicalEventSource.GrandparentObject);
                                EventOnSpawnAssetIndexUpdated.TryInvoke(assetOldId, assetId, HierarchicalEventSource.GrandparentObject);
                                Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Zombie spawn asset index updated: {assetOldId.Format()} -> {assetId.Format()} (parent updated).");
                            }
                        }

                        Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Zombie spawn tier index updated: {idOld.Format()} -> {id.Format()} (parent updated).");
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

                if (!EventOnAnimalSpawnAssetRemoved.IsEmpty || !EventOnSpawnAssetRemoved.IsEmpty || DevkitServerModule.IsDebug)
                {
                    for (int i = 0; i < maxCt; i++)
                    {
                        AnimalSpawn spawn = animalTier.table[i];
                        SpawnAssetIdentifier id = new SpawnAssetIdentifier(spawnType, (byte)tableIndex, (byte)tierIndex, (byte)i);
                        
                        EventOnAnimalSpawnAssetRemoved.TryInvoke(spawn, id, HierarchicalEventSource.ParentObject);
                        EventOnSpawnAssetRemoved.TryInvoke(id, HierarchicalEventSource.ParentObject);
                        Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Animal spawn asset removed: {spawn.animal.Format()} ({id.Format()}) (parent removed).");
                    }
                }
                
                EventOnAnimalSpawnTierRemoved.TryInvoke(animalTier, identifier, HierarchicalEventSource.ThisObject);
                EventOnSpawnTierRemoved.TryInvoke(identifier, HierarchicalEventSource.ThisObject);
                Logger.DevkitServer.LogDebug(nameof(RemoveSpawnTableLocal), $"Animal spawn tier removed: {animalTier.name.Format()} ({identifier.Format()}).");
                bool hasAssetRem = !EventOnAnimalSpawnAssetIndexUpdated.IsEmpty || !EventOnSpawnAssetIndexUpdated.IsEmpty;
                if (EventOnAnimalSpawnTierIndexUpdated.IsEmpty && EventOnSpawnTierIndexUpdated.IsEmpty && !hasAssetRem && !DevkitServerModule.IsDebug)
                    break;

                for (int i = tierIndex; i < animalTable.tiers.Count; ++i)
                {
                    AnimalTier tier2 = animalTable.tiers[i];
                    SpawnTierIdentifier oldTierIdentifier = new SpawnTierIdentifier(spawnType, (byte)tableIndex, (byte)(i + 1));
                    SpawnTierIdentifier newTierIdentifier = new SpawnTierIdentifier(spawnType, (byte)tableIndex, (byte)i);
                    EventOnAnimalSpawnTierIndexUpdated.TryInvoke(tier2, oldTierIdentifier, newTierIdentifier, HierarchicalEventSource.ThisObject);
                    EventOnSpawnTierIndexUpdated.TryInvoke(oldTierIdentifier, newTierIdentifier, HierarchicalEventSource.ThisObject);
                    Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Animal spawn tier index updated: {tier2.name.Format()} (# {oldTierIdentifier.Format()} -> {newTierIdentifier.Format()}).");

                    if (!hasAssetRem && !DevkitServerModule.IsDebug)
                        continue;

                    maxCt = Math.Min(byte.MaxValue - 1, tier2.table.Count);

                    for (int k = 0; k < maxCt; k++)
                    {
                        AnimalSpawn spawn = tier2.table[k];
                        SpawnAssetIdentifier id = new SpawnAssetIdentifier(spawnType, (byte)tableIndex, (byte)i, (byte)k);
                        SpawnAssetIdentifier idOld = new SpawnAssetIdentifier(spawnType, (byte)tableIndex, (byte)(i + 1), (byte)k);

                        EventOnAnimalSpawnAssetIndexUpdated.TryInvoke(spawn, idOld, id, HierarchicalEventSource.ParentObject);
                        EventOnSpawnAssetIndexUpdated.TryInvoke(idOld, id, HierarchicalEventSource.ParentObject);
                        Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Animal spawn asset index updated: {idOld.Format()} -> {id.Format()} (parent updated).");
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

                if (!EventOnVehicleSpawnAssetRemoved.IsEmpty || !EventOnSpawnAssetRemoved.IsEmpty || DevkitServerModule.IsDebug)
                {
                    for (int i = 0; i < maxCt; i++)
                    {
                        VehicleSpawn spawn = vehicleTier.table[i];
                        SpawnAssetIdentifier id = new SpawnAssetIdentifier(spawnType, (byte)tableIndex, (byte)tierIndex, (byte)i);

                        EventOnVehicleSpawnAssetRemoved.TryInvoke(spawn, id, HierarchicalEventSource.ParentObject);
                        EventOnSpawnAssetRemoved.TryInvoke(id, HierarchicalEventSource.ParentObject);
                        Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Vehicle spawn asset removed: {spawn.vehicle.Format()} ({id.Format()}) (parent removed).");
                    }
                }

                EventOnVehicleSpawnTierRemoved.TryInvoke(vehicleTier, identifier, HierarchicalEventSource.ThisObject);
                EventOnSpawnTierRemoved.TryInvoke(identifier, HierarchicalEventSource.ThisObject);
                Logger.DevkitServer.LogDebug(nameof(RemoveSpawnTableLocal), $"Vehicle spawn tier removed: {vehicleTable.name.Format()} ({identifier.Format()}).");
                hasAssetRem = !EventOnAnimalSpawnAssetIndexUpdated.IsEmpty || !EventOnSpawnAssetIndexUpdated.IsEmpty;
                if (EventOnVehicleSpawnTierIndexUpdated.IsEmpty && EventOnSpawnTierIndexUpdated.IsEmpty && !hasAssetRem && !DevkitServerModule.IsDebug)
                    break;

                for (int i = tierIndex; i < vehicleTable.tiers.Count; ++i)
                {
                    VehicleTier tier2 = vehicleTable.tiers[i];
                    SpawnTierIdentifier oldTierIdentifier = new SpawnTierIdentifier(spawnType, (byte)tableIndex, (byte)(i + 1));
                    SpawnTierIdentifier newTierIdentifier = new SpawnTierIdentifier(spawnType, (byte)tableIndex, (byte)i);
                    EventOnVehicleSpawnTierIndexUpdated.TryInvoke(tier2, oldTierIdentifier, newTierIdentifier, HierarchicalEventSource.ThisObject);
                    EventOnSpawnTierIndexUpdated.TryInvoke(oldTierIdentifier, newTierIdentifier, HierarchicalEventSource.ThisObject);
                    Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Vehicle spawn tier index updated: {tier2.name.Format()} (# {oldTierIdentifier.Format()} -> {newTierIdentifier.Format()}).");
                    if (!hasAssetRem && !DevkitServerModule.IsDebug)
                        continue;

                    maxCt = Math.Min(byte.MaxValue - 1, tier2.table.Count);

                    for (int k = 0; k < maxCt; k++)
                    {
                        VehicleSpawn spawn = tier2.table[k];
                        SpawnAssetIdentifier id = new SpawnAssetIdentifier(spawnType, (byte)tableIndex, (byte)i, (byte)k);
                        SpawnAssetIdentifier idOld = new SpawnAssetIdentifier(spawnType, (byte)tableIndex, (byte)(i + 1), (byte)k);

                        EventOnVehicleSpawnAssetIndexUpdated.TryInvoke(spawn, idOld, id, HierarchicalEventSource.ParentObject);
                        EventOnSpawnAssetIndexUpdated.TryInvoke(idOld, id, HierarchicalEventSource.ParentObject);
                        Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Vehicle spawn tier index updated: {idOld.Format()} -> {id.Format()} (parent updated).");
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

                if (!EventOnItemSpawnAssetRemoved.IsEmpty || !EventOnSpawnAssetRemoved.IsEmpty || DevkitServerModule.IsDebug)
                {
                    for (int i = 0; i < maxCt; i++)
                    {
                        ItemSpawn spawn = itemTier.table[i];
                        SpawnAssetIdentifier id = new SpawnAssetIdentifier(spawnType, (byte)tableIndex, (byte)tierIndex, (byte)i);

                        EventOnItemSpawnAssetRemoved.TryInvoke(spawn, id, HierarchicalEventSource.ParentObject);
                        EventOnSpawnAssetRemoved.TryInvoke(id, HierarchicalEventSource.ParentObject);
                        Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Item spawn asset removed: {spawn.item.Format()} ({id.Format()}) (parent removed).");
                    }
                }

                EventOnItemSpawnTierRemoved.TryInvoke(itemTier, identifier, HierarchicalEventSource.ThisObject);
                EventOnSpawnTierRemoved.TryInvoke(identifier, HierarchicalEventSource.ThisObject);
                Logger.DevkitServer.LogDebug(nameof(RemoveSpawnTableLocal), $"Item spawn tier removed: {itemTable.name.Format()} ({identifier.Format()}).");
                hasAssetRem = !EventOnAnimalSpawnAssetIndexUpdated.IsEmpty || !EventOnSpawnAssetIndexUpdated.IsEmpty;
                if (EventOnItemSpawnTierIndexUpdated.IsEmpty && EventOnSpawnTierIndexUpdated.IsEmpty && !hasAssetRem && !DevkitServerModule.IsDebug)
                    break;

                for (int i = tierIndex; i < itemTable.tiers.Count; ++i)
                {
                    ItemTier tier2 = itemTable.tiers[i];
                    SpawnTierIdentifier oldTierIdentifier = new SpawnTierIdentifier(spawnType, (byte)tableIndex, (byte)(i + 1));
                    SpawnTierIdentifier newTierIdentifier = new SpawnTierIdentifier(spawnType, (byte)tableIndex, (byte)i);
                    EventOnItemSpawnTierIndexUpdated.TryInvoke(tier2, oldTierIdentifier, newTierIdentifier, HierarchicalEventSource.ThisObject);
                    EventOnSpawnTierIndexUpdated.TryInvoke(oldTierIdentifier, newTierIdentifier, HierarchicalEventSource.ThisObject);
                    Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Item spawn tier index updated: {tier2.name.Format()} (# {oldTierIdentifier.Format()} -> {newTierIdentifier.Format()}).");

                    if (!hasAssetRem && !DevkitServerModule.IsDebug)
                        continue;

                    maxCt = Math.Min(byte.MaxValue - 1, tier2.table.Count);

                    for (int k = 0; k < maxCt; k++)
                    {
                        ItemSpawn spawn = tier2.table[k];
                        SpawnAssetIdentifier id = new SpawnAssetIdentifier(spawnType, (byte)tableIndex, (byte)i, (byte)k);
                        SpawnAssetIdentifier idOld = new SpawnAssetIdentifier(spawnType, (byte)tableIndex, (byte)(i + 1), (byte)k);

                        EventOnItemSpawnAssetIndexUpdated.TryInvoke(spawn, idOld, id, HierarchicalEventSource.ParentObject);
                        EventOnSpawnAssetIndexUpdated.TryInvoke(idOld, id, HierarchicalEventSource.ParentObject);
                        Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Item spawn asset index updated: {idOld.Format()} -> {id.Format()} (parent updated).");
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
                EventOnSpawnAssetRemoved.TryInvoke(identifier, HierarchicalEventSource.ThisObject);
                Logger.DevkitServer.LogDebug(nameof(RemoveSpawnTableLocal), $"Animal spawn table tier asset removed: {animalSpawn.animal.Format()} ({identifier.Format()}).");
                
                if (EventOnAnimalSpawnAssetIndexUpdated.IsEmpty && EventOnSpawnAssetIndexUpdated.IsEmpty && !DevkitServerModule.IsDebug)
                    break;

                for (int i = assetIndex; i < animalTier.table.Count; ++i)
                {
                    AnimalSpawn spawn2 = animalTier.table[i];
                    SpawnAssetIdentifier oldAssetIdentifier = new SpawnAssetIdentifier(spawnType, (byte)tableIndex, (byte)tierIndex, (byte)(i + 1));
                    SpawnAssetIdentifier newAssetIdentifier = new SpawnAssetIdentifier(spawnType, (byte)tableIndex, (byte)tierIndex, (byte)i);
                    EventOnAnimalSpawnAssetIndexUpdated.TryInvoke(spawn2, oldAssetIdentifier, newAssetIdentifier, HierarchicalEventSource.ThisObject);
                    EventOnSpawnAssetIndexUpdated.TryInvoke(oldAssetIdentifier, newAssetIdentifier, HierarchicalEventSource.ThisObject);
                    Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Animal spawn table tier asset index updated: {spawn2.animal.Format()} (# {oldAssetIdentifier.Format()} -> {newAssetIdentifier.Format()}).");
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
                EventOnSpawnAssetRemoved.TryInvoke(identifier, HierarchicalEventSource.ThisObject);
                Logger.DevkitServer.LogDebug(nameof(RemoveSpawnTableLocal), $"Vehicle spawn table tier asset removed: {vehicleSpawn.vehicle.Format()} ({identifier.Format()}).");
                
                if (EventOnVehicleSpawnAssetIndexUpdated.IsEmpty && EventOnSpawnAssetIndexUpdated.IsEmpty && !DevkitServerModule.IsDebug)
                    break;

                for (int i = assetIndex; i < vehicleTier.table.Count; ++i)
                {
                    VehicleSpawn spawn2 = vehicleTier.table[i];
                    SpawnAssetIdentifier oldAssetIdentifier = new SpawnAssetIdentifier(spawnType, (byte)tableIndex, (byte)tierIndex, (byte)(i + 1));
                    SpawnAssetIdentifier newAssetIdentifier = new SpawnAssetIdentifier(spawnType, (byte)tableIndex, (byte)tierIndex, (byte)i);
                    EventOnVehicleSpawnAssetIndexUpdated.TryInvoke(spawn2, oldAssetIdentifier, newAssetIdentifier, HierarchicalEventSource.ThisObject);
                    EventOnSpawnAssetIndexUpdated.TryInvoke(oldAssetIdentifier, newAssetIdentifier, HierarchicalEventSource.ThisObject);
                    Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Vehicle spawn table tier asset index updated: {spawn2.vehicle.Format()} (# {oldAssetIdentifier.Format()} -> {newAssetIdentifier.Format()}).");
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
                EventOnSpawnAssetRemoved.TryInvoke(identifier, HierarchicalEventSource.ThisObject);
                Logger.DevkitServer.LogDebug(nameof(RemoveSpawnTableLocal), $"Item spawn table tier asset removed: {itemSpawn.item.Format()} ({identifier.Format()}).");
                
                if (EventOnItemSpawnAssetIndexUpdated.IsEmpty && EventOnSpawnAssetIndexUpdated.IsEmpty && !DevkitServerModule.IsDebug)
                    break;

                for (int i = assetIndex; i < itemTier.table.Count; ++i)
                {
                    ItemSpawn spawn2 = itemTier.table[i];
                    SpawnAssetIdentifier oldAssetIdentifier = new SpawnAssetIdentifier(spawnType, (byte)tableIndex, (byte)tierIndex, (byte)(i + 1));
                    SpawnAssetIdentifier newAssetIdentifier = new SpawnAssetIdentifier(spawnType, (byte)tableIndex, (byte)tierIndex, (byte)i);
                    EventOnItemSpawnAssetIndexUpdated.TryInvoke(spawn2, oldAssetIdentifier, newAssetIdentifier, HierarchicalEventSource.ThisObject);
                    EventOnSpawnAssetIndexUpdated.TryInvoke(oldAssetIdentifier, newAssetIdentifier, HierarchicalEventSource.ThisObject);
                    Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Item spawn table tier asset index updated: {spawn2.item.Format()} (# {oldAssetIdentifier.Format()} -> {newAssetIdentifier.Format()}).");
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
                EventOnSpawnAssetRemoved.TryInvoke(identifier, HierarchicalEventSource.ThisObject);
                Logger.DevkitServer.LogDebug(nameof(RemoveSpawnTableLocal), $"Zombie spawn table tier asset removed: {zombieSpawn.item.Format()} ({identifier.Format()}).");
                
                if (EventOnZombieSpawnAssetIndexUpdated.IsEmpty && EventOnSpawnAssetIndexUpdated.IsEmpty && !DevkitServerModule.IsDebug)
                    break;

                for (int i = assetIndex; i < zombieTier.table.Count; ++i)
                {
                    ZombieCloth spawn2 = zombieTier.table[i];
                    SpawnAssetIdentifier oldAssetIdentifier = new SpawnAssetIdentifier(spawnType, (byte)tableIndex, (byte)tierIndex, (byte)(i + 1));
                    SpawnAssetIdentifier newAssetIdentifier = new SpawnAssetIdentifier(spawnType, (byte)tableIndex, (byte)tierIndex, (byte)i);
                    EventOnZombieSpawnAssetIndexUpdated.TryInvoke(spawn2, oldAssetIdentifier, newAssetIdentifier, HierarchicalEventSource.ThisObject);
                    EventOnSpawnAssetIndexUpdated.TryInvoke(oldAssetIdentifier, newAssetIdentifier, HierarchicalEventSource.ThisObject);
                    Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Zombie spawn table tier asset index updated: {spawn2.item.Format()} (# {oldAssetIdentifier.Format()} -> {newAssetIdentifier.Format()}).");
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

        EventOnSpawnTableNameUpdated.TryInvoke(spawnType, index);
        Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Set {spawnType.GetLowercaseText()} " +
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

        EventOnSpawnTableColorUpdated.TryInvoke(spawnType, index);
        Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Set {spawnType.GetLowercaseText()} " +
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
                if (Level.isEditor && IsTableSelected(spawnType, index))
                    UIExtensionManager.GetInstance<EditorSpawnsAnimalsUIExtension>()?.UpdateTableId();
#endif

                EventOnAnimalSpawnTableSpawnAssetUpdated.TryInvoke(animalTable, index);
                break;
            
            case SpawnType.Vehicle:
                VehicleTable vehicleTable = LevelVehicles.tables[index];
                vehicleTable.tableID = id;
#if CLIENT
                if (Level.isEditor && IsTableSelected(spawnType, index))
                    UIExtensionManager.GetInstance<EditorSpawnsVehiclesUIExtension>()?.UpdateTableId();
#endif

                EventOnVehicleSpawnTableSpawnAssetUpdated.TryInvoke(vehicleTable, index);
                break;
            
            case SpawnType.Item:
                ItemTable itemTable = LevelItems.tables[index];
                itemTable.tableID = id;
#if CLIENT
                if (Level.isEditor && IsTableSelected(spawnType, index))
                    UIExtensionManager.GetInstance<EditorSpawnsItemsUIExtension>()?.UpdateTableId();
#endif

                EventOnItemSpawnTableSpawnAssetUpdated.TryInvoke(itemTable, index);
                break;
            
            case SpawnType.Zombie:
                ZombieTable zombieTable = LevelZombies.tables[index];
                zombieTable.lootID = id;
#if CLIENT
                if (Level.isEditor && IsTableSelected(spawnType, index))
                    UIExtensionManager.GetInstance<EditorSpawnsZombiesUIExtension>()?.UpdateLootId();
#endif

                EventOnZombieSpawnTableSpawnAssetUpdated.TryInvoke(zombieTable, index);
                break;
        }

        EventOnSpawnTableSpawnAssetUpdated.TryInvoke(spawnType, index);
        Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Set {spawnType.GetLowercaseText()} " +
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
                if (Level.isEditor && IsTableSelected(identifier.Type, identifier.TableIndex))
                    UIExtensionManager.GetInstance<EditorSpawnsAnimalsUIExtension>()?.UpdateTierName(identifier.TierIndex);
#endif

                EventOnAnimalSpawnTierNameUpdated.TryInvoke(animalTier, identifier, HierarchicalEventSource.ThisObject);
                break;

            case SpawnType.Vehicle:
                VehicleTier vehicleTier = LevelVehicles.tables[identifier.TableIndex].tiers[identifier.TierIndex];
                vehicleTier.name = newName;
#if CLIENT
                if (Level.isEditor && IsTableSelected(identifier.Type, identifier.TableIndex))
                    UIExtensionManager.GetInstance<EditorSpawnsVehiclesUIExtension>()?.UpdateTierName(identifier.TierIndex);
#endif

                EventOnVehicleSpawnTierNameUpdated.TryInvoke(vehicleTier, identifier, HierarchicalEventSource.ThisObject);
                break;

            case SpawnType.Item:
                ItemTier itemTier = LevelItems.tables[identifier.TableIndex].tiers[identifier.TierIndex];
                itemTier.name = newName;
#if CLIENT
                if (Level.isEditor && IsTableSelected(identifier.Type, identifier.TableIndex))
                    UIExtensionManager.GetInstance<EditorSpawnsItemsUIExtension>()?.UpdateTierName(identifier.TierIndex);
#endif

                EventOnItemSpawnTierNameUpdated.TryInvoke(itemTier, identifier, HierarchicalEventSource.ThisObject);
                break;

            case SpawnType.Zombie:
                return false;
        }

        EventOnSpawnTierNameUpdated.TryInvoke(identifier, HierarchicalEventSource.ThisObject);
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
                if (Level.isEditor && IsTableSelected(identifier.Type, identifier.TableIndex))
                    UIExtensionManager.GetInstance<EditorSpawnsAnimalsUIExtension>()?.UpdateTierChance(identifier.TierIndex, updateSlider);
#endif

                EventOnAnimalSpawnTierChanceUpdated.TryInvoke(animalTier, identifier, HierarchicalEventSource.ThisObject);
                break;

            case SpawnType.Vehicle:
                VehicleTier vehicleTier = LevelVehicles.tables[identifier.TableIndex].tiers[identifier.TierIndex];
                vehicleTier.chance = chance;
#if CLIENT
                if (Level.isEditor && IsTableSelected(identifier.Type, identifier.TableIndex))
                    UIExtensionManager.GetInstance<EditorSpawnsVehiclesUIExtension>()?.UpdateTierChance(identifier.TierIndex, updateSlider);
#endif

                EventOnVehicleSpawnTierChanceUpdated.TryInvoke(vehicleTier, identifier, HierarchicalEventSource.ThisObject);
                break;

            case SpawnType.Item:
                ItemTier itemTier = LevelItems.tables[identifier.TableIndex].tiers[identifier.TierIndex];
                itemTier.chance = chance;
#if CLIENT
                if (Level.isEditor && IsTableSelected(identifier.Type, identifier.TableIndex))
                    UIExtensionManager.GetInstance<EditorSpawnsItemsUIExtension>()?.UpdateTierChance(identifier.TierIndex, updateSlider);
#endif

                EventOnItemSpawnTierChanceUpdated.TryInvoke(itemTier, identifier, HierarchicalEventSource.ThisObject);
                break;

            case SpawnType.Zombie:
                ZombieSlot zombieTier = LevelZombies.tables[identifier.TableIndex].slots[identifier.TierIndex];
                zombieTier.chance = chance;
#if CLIENT
                if (Level.isEditor && IsTableSelected(identifier.Type, identifier.TableIndex))
                    UIExtensionManager.GetInstance<EditorSpawnsZombiesUIExtension>()?.UpdateSlotChance(identifier.TierIndex, updateSlider);
#endif

                EventOnZombieSpawnTierChanceUpdated.TryInvoke(zombieTier, identifier, HierarchicalEventSource.ThisObject);
                break;
        }

        EventOnSpawnTierChanceUpdated.TryInvoke(identifier, HierarchicalEventSource.ThisObject);
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
                if (LevelAnimals.tables[tableIndex].tiers[tierIndex].table[assetIndex].animal == id)
                    return true;
                AnimalSpawn animalSpawn = new AnimalSpawn(id);
                LevelAnimals.tables[tableIndex].tiers[tierIndex].table[assetIndex] = animalSpawn;
#if CLIENT
                if (Level.isEditor && IsTableSelected(identifier.Type, tableIndex) &&
                    TryGetSelectedTier(SpawnType.Animal, out SpawnTierIdentifier? selectedTier) &&
                    selectedTier.HasValue && selectedTier.Value.TierIndex == tierIndex)
                {
                    UIExtensionManager.GetInstance<EditorSpawnsAnimalsUIExtension>()?.UpdateSpawnName(assetIndex);
                }
#endif

                EventOnAnimalSpawnTierAssetUpdated.TryInvoke(animalSpawn, identifier, HierarchicalEventSource.ThisObject);
                break;

            case SpawnType.Vehicle:
                if (LevelVehicles.tables[tableIndex].tiers[tierIndex].table[assetIndex].vehicle == id)
                    return true;
                VehicleSpawn vehicleSpawn = new VehicleSpawn(id);
                LevelVehicles.tables[tableIndex].tiers[tierIndex].table[assetIndex] = vehicleSpawn;
#if CLIENT
                if (Level.isEditor && IsTableSelected(identifier.Type, tableIndex) &&
                    TryGetSelectedTier(SpawnType.Vehicle, out selectedTier) &&
                    selectedTier.HasValue && selectedTier.Value.TierIndex == tierIndex)
                {
                    UIExtensionManager.GetInstance<EditorSpawnsVehiclesUIExtension>()?.UpdateSpawnName(assetIndex);
                }
#endif

                EventOnVehicleSpawnTierAssetUpdated.TryInvoke(vehicleSpawn, identifier, HierarchicalEventSource.ThisObject);
                break;

            case SpawnType.Item:
                if (LevelItems.tables[tableIndex].tiers[tierIndex].table[assetIndex].item == id)
                    return true;
                ItemSpawn itemSpawn = new ItemSpawn(id);
                LevelItems.tables[tableIndex].tiers[tierIndex].table[assetIndex] = itemSpawn;
#if CLIENT
                if (Level.isEditor && IsTableSelected(identifier.Type, tableIndex) &&
                    TryGetSelectedTier(SpawnType.Item, out selectedTier) &&
                    selectedTier.HasValue && selectedTier.Value.TierIndex == tierIndex)
                {
                    UIExtensionManager.GetInstance<EditorSpawnsItemsUIExtension>()?.UpdateSpawnName(assetIndex);
                }
#endif

                EventOnItemSpawnTierAssetUpdated.TryInvoke(itemSpawn, identifier, HierarchicalEventSource.ThisObject);
                break;

            case SpawnType.Zombie:
                if (LevelZombies.tables[tableIndex].slots[tierIndex].table[assetIndex].item == id)
                    return true;
                ZombieCloth zombieSpawn = new ZombieCloth(id);
                LevelZombies.tables[tableIndex].slots[tierIndex].table[assetIndex] = zombieSpawn;
#if CLIENT
                if (Level.isEditor && IsTableSelected(identifier.Type, tableIndex) &&
                    TryGetSelectedTier(SpawnType.Zombie, out selectedTier) &&
                    selectedTier.HasValue && selectedTier.Value.TierIndex == tierIndex)
                {
                    UIExtensionManager.GetInstance<EditorSpawnsZombiesUIExtension>()?.UpdateSpawnName(assetIndex);
                }
#endif

                EventOnZombieSpawnTierAssetUpdated.TryInvoke(zombieSpawn, identifier, HierarchicalEventSource.ThisObject);
                break;
        }

        EventOnSpawnTierAssetUpdated.TryInvoke(identifier, HierarchicalEventSource.ThisObject);

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
    /// Normalizes all chances so that they add up to 1 (100%). Doesn't apply to zombies.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <param name="index">Table index of the target table.</param>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    public static bool NormalizeChancesLocal(SpawnType spawnType, int index)
    {
        ThreadUtil.assertIsGameThread();

        if (spawnType == SpawnType.Zombie || !CheckSpawnTableSafe(spawnType, index))
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

                if (!EventOnAnimalSpawnTierChanceUpdated.IsEmpty || !EventOnSpawnTierChanceUpdated.IsEmpty)
                {
                    for (int i = 0; i < ct; ++i)
                    {
                        SpawnTierIdentifier identifier = new SpawnTierIdentifier(spawnType, (byte)index, (byte)i);
                        EventOnAnimalSpawnTierChanceUpdated.TryInvoke(animalTable.tiers[i], identifier, HierarchicalEventSource.ParentObject);
                        EventOnSpawnTierChanceUpdated.TryInvoke(identifier, HierarchicalEventSource.ParentObject);
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

                if (!EventOnVehicleSpawnTierChanceUpdated.IsEmpty || !EventOnSpawnTierChanceUpdated.IsEmpty)
                {
                    for (int i = 0; i < ct; ++i)
                    {
                        SpawnTierIdentifier identifier = new SpawnTierIdentifier(spawnType, (byte)index, (byte)i);
                        EventOnVehicleSpawnTierChanceUpdated.TryInvoke(vehicleTable.tiers[i], identifier, HierarchicalEventSource.ParentObject);
                        EventOnSpawnTierChanceUpdated.TryInvoke(identifier, HierarchicalEventSource.ParentObject);
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

                if (!EventOnItemSpawnTierChanceUpdated.IsEmpty || !EventOnSpawnTierChanceUpdated.IsEmpty)
                {
                    for (int i = 0; i < ct; ++i)
                    {
                        SpawnTierIdentifier identifier = new SpawnTierIdentifier(spawnType, (byte)index, (byte)i);
                        EventOnItemSpawnTierChanceUpdated.TryInvoke(itemTable.tiers[i], identifier, HierarchicalEventSource.ParentObject);
                        EventOnSpawnTierChanceUpdated.TryInvoke(identifier, HierarchicalEventSource.ParentObject);
                    }
                }

                break;
        }

        Logger.DevkitServer.LogConditional(nameof(RemoveSpawnTableLocal), $"Normalized {spawnType.GetLowercaseText()} spawn table (# {index.Format()}) chances");
        return true;
    }

    /// <summary>
    /// Locally adds a spawn table of the provided <paramref name="spawnType"/> with the given <paramref name="name"/>.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <returns>Table index of the created table.</returns>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="InvalidOperationException">Too many spawn tables of that type are already on the map.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Spawn type was not one of the following: Animal, Vehicle, Item, or Zombie.</exception>
    public static int AddSpawnTableLocal(SpawnType spawnType, string name)
    {
        ThreadUtil.assertIsGameThread();

        if (spawnType is not SpawnType.Animal and not SpawnType.Vehicle and not SpawnType.Item and not SpawnType.Zombie)
            throw new ArgumentOutOfRangeException(nameof(spawnType), "Spawn type must be one of the following: Animal, Vehicle, Item, or Zombie.");

        int tableCount = GetTableCountUnsafe(spawnType);
        if (tableCount >= byte.MaxValue - 1)
            throw new InvalidOperationException($"Too many {spawnType.GetLowercaseText()} spawn tables ({byte.MaxValue - 1}) already exist on the map.");

        int index;

        switch (spawnType)
        {
            case SpawnType.Animal:
                LevelAnimals.addTable(name);
                index = LevelAnimals.tables.Count - 1;
#if SERVER
                if (DevkitServerModule.IsEditing)
                {
                    NetId64 netId = SpawnsNetIdDatabase.AddSpawnTable(spawnType, index);
                    Logger.DevkitServer.LogDebug(nameof(AddSpawnTableLocal), $"Assigned animal spawn table {name.Format(true)} NetId: {netId.Format()}.");
                }
#endif
                AnimalTable animalTable = LevelAnimals.tables[index];
                EventOnAnimalSpawnTableAdded.TryInvoke(animalTable, index);
                EventOnSpawnTableAdded.TryInvoke(spawnType, index);
                break;

            case SpawnType.Vehicle:
                LevelVehicles.addTable(name);
                index = LevelVehicles.tables.Count - 1;
#if SERVER
                if (DevkitServerModule.IsEditing)
                {
                    NetId64 netId = SpawnsNetIdDatabase.AddSpawnTable(spawnType, index);
                    Logger.DevkitServer.LogDebug(nameof(AddSpawnTableLocal), $"Assigned vehicle spawn table {name.Format(true)} NetId: {netId.Format()}.");
                }
#endif
                VehicleTable vehicleTable = LevelVehicles.tables[index];
                EventOnVehicleSpawnTableAdded.TryInvoke(vehicleTable, index);
                EventOnSpawnTableAdded.TryInvoke(spawnType, index);
                break;

            case SpawnType.Item:
                LevelItems.addTable(name);
                index = LevelItems.tables.Count - 1;
#if SERVER
                if (DevkitServerModule.IsEditing)
                {
                    NetId64 netId = SpawnsNetIdDatabase.AddSpawnTable(spawnType, index);
                    Logger.DevkitServer.LogDebug(nameof(AddSpawnTableLocal), $"Assigned item spawn table {name.Format(true)} NetId: {netId.Format()}.");
                }
#endif
                ItemTable itemTable = LevelItems.tables[index];
                EventOnItemSpawnTableAdded.TryInvoke(itemTable, index);
                EventOnSpawnTableAdded.TryInvoke(spawnType, index);
                break;

            default: // Zombie
                LevelZombies.addTable(name);
                index = LevelZombies.tables.Count - 1;
#if SERVER
                if (DevkitServerModule.IsEditing)
                {
                    NetId64 netId = SpawnsNetIdDatabase.AddSpawnTable(spawnType, index);
                    Logger.DevkitServer.LogDebug(nameof(AddSpawnTableLocal), $"Assigned zombie spawn table {name.Format(true)} NetId: {netId.Format()}.");
                }
#endif
                ZombieTable zombieTable = LevelZombies.tables[index];
                EventOnZombieSpawnTableAdded.TryInvoke(zombieTable, index);
                EventOnSpawnTableAdded.TryInvoke(spawnType, index);

#if CLIENT
                if (EventOnZombieSpawnTierAdded.IsEmpty && EventOnSpawnTierAdded.IsEmpty)
                    break;
#endif

                for (int i = 0; i < zombieTable.slots.Length; ++i)
                {
                    ZombieSlot tier = zombieTable.slots[i];
                    SpawnTierIdentifier identifier = new SpawnTierIdentifier(spawnType, (byte)index, (byte)i);
                    EventOnZombieSpawnTierAdded.TryInvoke(tier, identifier, HierarchicalEventSource.ParentObject);
                    EventOnSpawnTierAdded.TryInvoke(identifier, HierarchicalEventSource.ParentObject);
#if SERVER
                    if (!DevkitServerModule.IsEditing)
                        continue;

                    NetId64 netId = SpawnsNetIdDatabase.AddSpawnTier(identifier);
                    Logger.DevkitServer.LogDebug(nameof(AddSpawnTableLocal), $"Assigned zombie spawn table tier {identifier.Format()} NetId: {netId.Format()}.");
#endif
                }

                break;
        }

#if CLIENT
        if (Level.isEditor)
        {
            UpdateUITable(spawnType);
            if (Level.isEditor && IsTableSelected(spawnType, index))
                UpdateUISelection(spawnType);
        }
#endif

        tableCount = GetTableCountUnsafe(spawnType);
        if (index >= tableCount)
        {
            Logger.DevkitServer.LogError(nameof(AddSpawnTableLocal), $"Unknown error adding {spawnType.GetLowercaseText()} spawn table: {index.Format()} {name.Format(true)}.");
            return -1;
        }

        Logger.DevkitServer.LogDebug(nameof(AddSpawnTableLocal), $"Added {spawnType.GetLowercaseText()} spawn table: {index.Format()} {name.Format(true)}.");

        return index;
    }

    /// <summary>
    /// Locally adds a spawn table tier to the provided <paramref name="spawnType"/> table at <paramref name="spawnTableIndex"/> with the given <paramref name="name"/>. You can not add slots to zombies.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <returns>Table index of the created table.</returns>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="InvalidOperationException">Too many spawn table tiers of that type are already on the table.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Spawn type was not one of the following: Animal, Vehicle, or Item.</exception>
    /// <exception cref="ArgumentException">Provided spawn table does not exist.</exception>
    public static SpawnTierIdentifier? AddSpawnTableTierLocal(SpawnType spawnType, int spawnTableIndex, string name)
    {
        ThreadUtil.assertIsGameThread();

        if (spawnType is not SpawnType.Animal and not SpawnType.Vehicle and not SpawnType.Item)
            throw new ArgumentOutOfRangeException(nameof(spawnType), "Spawn type must be one of the following: Animal, Vehicle, or Item.");

        if (!CheckSpawnTableSafe(spawnType, spawnTableIndex))
            throw new ArgumentException($"{spawnType} table does not exist.", nameof(spawnTableIndex));

        int tierCount = GetTierCountUnsafe(spawnTableIndex, spawnType);
        if (tierCount >= byte.MaxValue - 1)
            throw new InvalidOperationException($"Too many {spawnType.GetLowercaseText()} spawn table tiers ({byte.MaxValue - 1}) already exist on the table.");

        SpawnTierIdentifier identifier;

        switch (spawnType)
        {
            case SpawnType.Animal:
                AnimalTable animalTable = LevelAnimals.tables[spawnTableIndex];
                animalTable.addTier(name);
                byte index = (byte)(animalTable.tiers.Count - 1);
                identifier = new SpawnTierIdentifier(spawnType, (byte)spawnTableIndex, index);
#if SERVER
                if (DevkitServerModule.IsEditing)
                {
                    NetId64 netId = SpawnsNetIdDatabase.AddSpawnTier(identifier);
                    Logger.DevkitServer.LogDebug(nameof(AddSpawnTableTierLocal), $"Assigned animal spawn table tier {name.Format()} NetId: {netId.Format()}.");
                }
#endif
                AnimalTier animalTier = animalTable.tiers[index];

                EventOnAnimalSpawnTierAdded.TryInvoke(animalTier, identifier, HierarchicalEventSource.ThisObject);
                EventOnSpawnTierAdded.TryInvoke(identifier, HierarchicalEventSource.ThisObject);
                
                if (EventOnAnimalSpawnAssetAdded.IsEmpty && EventOnSpawnAssetAdded.IsEmpty)
                    break;

                for (int i = 0; i < animalTier.table.Count; ++i)
                {
                    AnimalSpawn asset = animalTier.table[i];
                    SpawnAssetIdentifier assetIdentifier = new SpawnAssetIdentifier(spawnType, (byte)spawnTableIndex, index, (byte)i);
                    EventOnAnimalSpawnAssetAdded.TryInvoke(asset, assetIdentifier, HierarchicalEventSource.ParentObject);
                    EventOnSpawnAssetAdded.TryInvoke(assetIdentifier, HierarchicalEventSource.ParentObject);
                }

                break;

            case SpawnType.Vehicle:
                VehicleTable vehicleTable = LevelVehicles.tables[spawnTableIndex];
                vehicleTable.addTier(name);
                index = (byte)(vehicleTable.tiers.Count - 1);
                identifier = new SpawnTierIdentifier(spawnType, (byte)spawnTableIndex, index);
#if SERVER
                if (DevkitServerModule.IsEditing)
                {
                    NetId64 netId = SpawnsNetIdDatabase.AddSpawnTier(identifier);
                    Logger.DevkitServer.LogDebug(nameof(AddSpawnTableTierLocal), $"Assigned vehicle spawn table tier {name.Format()} NetId: {netId.Format()}.");
                }
#endif
                VehicleTier vehicleTier = vehicleTable.tiers[index];

                EventOnVehicleSpawnTierAdded.TryInvoke(vehicleTier, identifier, HierarchicalEventSource.ThisObject);
                EventOnSpawnTierAdded.TryInvoke(identifier, HierarchicalEventSource.ThisObject);
                
                if (EventOnVehicleSpawnAssetAdded.IsEmpty && EventOnSpawnAssetAdded.IsEmpty)
                    break;

                for (int i = 0; i < vehicleTier.table.Count; ++i)
                {
                    VehicleSpawn asset = vehicleTier.table[i];
                    SpawnAssetIdentifier assetIdentifier = new SpawnAssetIdentifier(spawnType, (byte)spawnTableIndex, index, (byte)i);
                    EventOnVehicleSpawnAssetAdded.TryInvoke(asset, assetIdentifier, HierarchicalEventSource.ParentObject);
                    EventOnSpawnAssetAdded.TryInvoke(assetIdentifier, HierarchicalEventSource.ParentObject);
                }

                break;

            default: // Item
                ItemTable itemTable = LevelItems.tables[spawnTableIndex];
                itemTable.addTier(name);
                index = (byte)(itemTable.tiers.Count - 1);
                identifier = new SpawnTierIdentifier(spawnType, (byte)spawnTableIndex, index);
#if SERVER
                if (DevkitServerModule.IsEditing)
                {
                    NetId64 netId = SpawnsNetIdDatabase.AddSpawnTier(identifier);
                    Logger.DevkitServer.LogDebug(nameof(AddSpawnTableTierLocal), $"Assigned item spawn table tier {name.Format()} NetId: {netId.Format()}.");
                }
#endif
                ItemTier itemTier = itemTable.tiers[index];

                EventOnItemSpawnTierAdded.TryInvoke(itemTier, identifier, HierarchicalEventSource.ThisObject);
                EventOnSpawnTierAdded.TryInvoke(identifier, HierarchicalEventSource.ThisObject);
                
                if (EventOnItemSpawnAssetAdded.IsEmpty && EventOnSpawnAssetAdded.IsEmpty)
                    break;

                for (int i = 0; i < itemTier.table.Count; ++i)
                {
                    ItemSpawn asset = itemTier.table[i];
                    SpawnAssetIdentifier assetIdentifier = new SpawnAssetIdentifier(spawnType, (byte)spawnTableIndex, index, (byte)i);
                    EventOnItemSpawnAssetAdded.TryInvoke(asset, assetIdentifier, HierarchicalEventSource.ParentObject);
                    EventOnSpawnAssetAdded.TryInvoke(assetIdentifier, HierarchicalEventSource.ParentObject);
                }

                break;
        }

#if CLIENT
        if (Level.isEditor)
        {
            UpdateUITable(spawnType);
            if (Level.isEditor && IsTableSelected(spawnType, spawnTableIndex))
                UpdateUISelection(spawnType);
        }
#endif

        if (!identifier.CheckSafe())
        {
            Logger.DevkitServer.LogError(nameof(AddSpawnTableTierLocal), $"Unknown error adding {spawnType.GetLowercaseText()} spawn table: {identifier.Format()} {name.Format(true)}.");
            return null;
        }

        Logger.DevkitServer.LogDebug(nameof(AddSpawnTableTierLocal), $"Added {spawnType.GetLowercaseText()} spawn table: {identifier.Format()} {name.Format(true)}.");

        return identifier;
    }

    /// <summary>
    /// Locally adds a spawn table tier asset to the provided <paramref name="spawnType"/> tier at <paramref name="spawnTierIdentifier"/> with the given <paramref name="legacyId"/>.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <returns>Table index of the created table, or null if the ID already exists in the tier.</returns>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="InvalidOperationException">Too many spawn table tiers of that type are already on the tier.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Spawn type was not one of the following: Animal, Vehicle, Item, or Zombie.</exception>
    /// <exception cref="ArgumentException">Provided spawn table tier does not exist.</exception>
    public static SpawnAssetIdentifier? AddSpawnTableAssetLocal(SpawnTierIdentifier spawnTierIdentifier, ushort legacyId)
    {
        ThreadUtil.assertIsGameThread();

        SpawnType spawnType = spawnTierIdentifier.Type;
        if (spawnType is not SpawnType.Animal and not SpawnType.Vehicle and not SpawnType.Item and not SpawnType.Zombie)
            throw new ArgumentOutOfRangeException(nameof(spawnTierIdentifier), "Spawn type must be one of the following: Animal, Vehicle, Item, or Zombie.");

        if (!spawnTierIdentifier.CheckSafe())
            throw new ArgumentException($"{spawnType} table tier does not exist.", nameof(spawnTierIdentifier));

        int tableIndex = spawnTierIdentifier.TableIndex,
            tierIndex = spawnTierIdentifier.TierIndex;

        int assetCount = GetAssetCountUnsafe(tableIndex, tierIndex, spawnType);
        if (assetCount >= byte.MaxValue - 1)
            throw new InvalidOperationException($"Too many {spawnType.GetLowercaseText()} spawn table assets ({byte.MaxValue - 1}) already exist on the tier.");

        SpawnAssetIdentifier identifier;

        switch (spawnType)
        {
            case SpawnType.Animal:
                AnimalTable animalTable = LevelAnimals.tables[tableIndex];
                AnimalTier animalTier = animalTable.tiers[tierIndex];

                for (int i = 0; i < animalTier.table.Count; ++i)
                {
                    if (animalTier.table[i].animal == legacyId)
                        return null;
                }

                animalTier.addAnimal(legacyId);

                byte index = (byte)(animalTier.table.Count - 1);
                identifier = new SpawnAssetIdentifier(spawnType, (byte)tableIndex, (byte)tierIndex, index);
#if SERVER
                if (DevkitServerModule.IsEditing)
                {
                    NetId64 netId = SpawnsNetIdDatabase.AddSpawnAsset(identifier);
                    Logger.DevkitServer.LogDebug(nameof(AddSpawnTableAssetLocal), $"Assigned animal spawn table tier asset {legacyId.Format()} NetId: {netId.Format()}.");
                }
#endif
                EventOnAnimalSpawnAssetAdded.TryInvoke(animalTier.table[index], identifier, HierarchicalEventSource.ThisObject);
                EventOnSpawnAssetAdded.TryInvoke(identifier, HierarchicalEventSource.ThisObject);
                break;

            case SpawnType.Vehicle:
                VehicleTable vehicleTable = LevelVehicles.tables[tableIndex];
                VehicleTier vehicleTier = vehicleTable.tiers[tierIndex];

                for (int i = 0; i < vehicleTier.table.Count; ++i)
                {
                    if (vehicleTier.table[i].vehicle == legacyId)
                        return null;
                }

                vehicleTier.addVehicle(legacyId);

                index = (byte)(vehicleTier.table.Count - 1);
                identifier = new SpawnAssetIdentifier(spawnType, (byte)tableIndex, (byte)tierIndex, index);
#if SERVER
                if (DevkitServerModule.IsEditing)
                {
                    NetId64 netId = SpawnsNetIdDatabase.AddSpawnAsset(identifier);
                    Logger.DevkitServer.LogDebug(nameof(AddSpawnTableAssetLocal), $"Assigned vehicle spawn table tier asset {legacyId.Format()} NetId: {netId.Format()}.");
                }
#endif
                EventOnVehicleSpawnAssetAdded.TryInvoke(vehicleTier.table[index], identifier, HierarchicalEventSource.ThisObject);
                EventOnSpawnAssetAdded.TryInvoke(identifier, HierarchicalEventSource.ThisObject);
                break;

            case SpawnType.Item:
                ItemTable itemTable = LevelItems.tables[tableIndex];
                ItemTier itemTier = itemTable.tiers[tierIndex];

                for (int i = 0; i < itemTier.table.Count; ++i)
                {
                    if (itemTier.table[i].item == legacyId)
                        return null;
                }

                itemTier.addItem(legacyId);

                index = (byte)(itemTier.table.Count - 1);
                identifier = new SpawnAssetIdentifier(spawnType, (byte)tableIndex, (byte)tierIndex, index);
#if SERVER
                if (DevkitServerModule.IsEditing)
                {
                    NetId64 netId = SpawnsNetIdDatabase.AddSpawnAsset(identifier);
                    Logger.DevkitServer.LogDebug(nameof(AddSpawnTableAssetLocal), $"Assigned item spawn table tier asset {legacyId.Format()} NetId: {netId.Format()}.");
                }
#endif
                EventOnItemSpawnAssetAdded.TryInvoke(itemTier.table[index], identifier, HierarchicalEventSource.ThisObject);
                EventOnSpawnAssetAdded.TryInvoke(identifier, HierarchicalEventSource.ThisObject);
                break;

            default: // Zombie
                ZombieTable zombieTable = LevelZombies.tables[tableIndex];
                ZombieSlot zombieTier = zombieTable.slots[tierIndex];

                for (int i = 0; i < zombieTier.table.Count; ++i)
                {
                    if (zombieTier.table[i].item == legacyId)
                        return null;
                }

                zombieTier.addCloth(legacyId);

                index = (byte)(zombieTier.table.Count - 1);
                identifier = new SpawnAssetIdentifier(spawnType, (byte)tableIndex, (byte)tierIndex, index);
#if SERVER
                if (DevkitServerModule.IsEditing)
                {
                    NetId64 netId = SpawnsNetIdDatabase.AddSpawnAsset(identifier);
                    Logger.DevkitServer.LogDebug(nameof(AddSpawnTableAssetLocal), $"Assigned zombie spawn table tier asset {legacyId.Format()} NetId: {netId.Format()}.");
                }
#endif
                EventOnZombieSpawnAssetAdded.TryInvoke(zombieTier.table[index], identifier, HierarchicalEventSource.ThisObject);
                EventOnSpawnAssetAdded.TryInvoke(identifier, HierarchicalEventSource.ThisObject);
                break;
        }

#if CLIENT
        if (Level.isEditor)
        {
            UpdateUITable(spawnType);
            if (Level.isEditor && IsTableSelected(spawnType, identifier.TableIndex))
                UpdateUISelection(spawnType);
        }
#endif

        if (!identifier.CheckSafe())
        {
            Logger.DevkitServer.LogError(nameof(AddSpawnTableAssetLocal), $"Unknown error adding {spawnType.GetLowercaseText()} spawn table: {identifier.Format()} {legacyId.Format()}.");
            return null;
        }

        Logger.DevkitServer.LogDebug(nameof(AddSpawnTableAssetLocal), $"Added {spawnType.GetLowercaseText()} spawn table: {identifier.Format()} {legacyId.Format()}.");

        return identifier;
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
    internal static int GetTierCountUnsafe(int tableIndex, SpawnType spawnType)
    {
        return spawnType switch
        {
            SpawnType.Animal => LevelAnimals.tables[tableIndex].tiers.Count,
            SpawnType.Vehicle => LevelVehicles.tables[tableIndex].tiers.Count,
            SpawnType.Item => LevelItems.tables[tableIndex].tiers.Count,
            SpawnType.Zombie => LevelZombies.tables[tableIndex].slots.Length,
            _ => 0
        };
    }
    internal static int GetAssetCountUnsafe(int tableIndex, int tierIndex, SpawnType spawnType)
    {
        return spawnType switch
        {
            SpawnType.Animal => LevelAnimals.tables[tableIndex].tiers[tierIndex].table.Count,
            SpawnType.Vehicle => LevelVehicles.tables[tableIndex].tiers[tierIndex].table.Count,
            SpawnType.Item => LevelItems.tables[tableIndex].tiers[tierIndex].table.Count,
            SpawnType.Zombie => LevelZombies.tables[tableIndex].slots[tierIndex].table.Count,
            _ => 0
        };
    }
    internal static string GetNameUnsafe(SpawnType spawnType, int tableIndex)
    {
        return spawnType switch
        {
            SpawnType.Animal => LevelAnimals.tables[tableIndex].name,
            SpawnType.Vehicle => LevelVehicles.tables[tableIndex].name,
            SpawnType.Item => LevelItems.tables[tableIndex].name,
            SpawnType.Zombie => LevelZombies.tables[tableIndex].name,
            _ => string.Empty
        } ?? string.Empty;
    }
#if SERVER
    /// <summary>
    /// Adds a spawn table of the provided <paramref name="spawnType"/> with the given <paramref name="name"/>.
    /// </summary>
    /// <remarks>Replicates to clients.</remarks>
    /// <returns>Table index of the created table.</returns>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="InvalidOperationException">Too many spawn tables of that type are already on the map.</exception>
    public static int AddSpawnTable(SpawnType spawnType, string name, EditorUser? owner = null)
    {
        ThreadUtil.assertIsGameThread();

        return AddSpawnTable(spawnType, name, owner == null ? MessageContext.Nil : MessageContext.CreateFromCaller(owner));
    }
    internal static int AddSpawnTable(SpawnType spawnType, string name, MessageContext ctx)
    {
        DevkitServerModule.AssertIsDevkitServerClient();

        ulong owner = ctx.GetCaller() is { } user ? user.SteamId.m_SteamID : 0ul;

        int index = AddSpawnTableLocal(spawnType, name);

        if (index == -1)
            return -1;

        SpawnsNetIdDatabase.TryGetSpawnTableNetId(spawnType, index, out NetId64 spawnTableNetId);

        PooledTransportConnectionList list = ctx.IsRequest ? DevkitServerUtility.GetAllConnections(ctx.Connection) : DevkitServerUtility.GetAllConnections();

        if (spawnType == SpawnType.Zombie)
        {
            ZombieTable table = LevelZombies.tables[index];

            long packed = (table.isMega ? 1 : default(long))
                          | ((long)table.damage << 8)
                          | ((long)table.health << 16)
                          | ((long)table.lootID << 32);

            Guid difficultyAsset = table.difficulty.GUID;

            name = table.name ?? string.Empty;

            NetId64 lootTableNetId = NetId64.Invalid;
            
            if (table.lootIndex < LevelItems.tables.Count)
                SpawnsNetIdDatabase.TryGetSpawnTableNetId(SpawnType.Item, table.lootIndex, out lootTableNetId);

            ulong[] netIds = new ulong[table.slots.Length];
            for (int i = 0; i < netIds.Length; ++i)
            {
                SpawnsNetIdDatabase.TryGetSpawnTierNetId(new SpawnTierIdentifier(SpawnType.Zombie, (byte)index, (byte)i), out NetId64 netId);
                netIds[i] = netId.Id;
            }

            if (ctx.IsRequest)
                ctx.ReplyLayered(SendZombieSpawnTableInstantiation, spawnTableNetId, name, packed, table.xp, table.regen, lootTableNetId, difficultyAsset, table.color, netIds, owner);

            SendZombieSpawnTableInstantiation.Invoke(list, spawnTableNetId, name, packed, table.xp, table.regen, lootTableNetId, difficultyAsset, table.color, netIds, owner);
        }
        else
        {
            Color color;
            ushort tableId;
            switch (spawnType)
            {
                case SpawnType.Animal:
                    AnimalTable animalTable = LevelAnimals.tables[index];
                    color = animalTable.color;
                    name = animalTable.name ?? string.Empty;
                    tableId = animalTable.tableID;
                    break;

                case SpawnType.Vehicle:
                    VehicleTable vehicleTable = LevelVehicles.tables[index];
                    color = vehicleTable.color;
                    name = vehicleTable.name ?? string.Empty;
                    tableId = vehicleTable.tableID;
                    break;

                default: // Item
                    ItemTable itemTable = LevelItems.tables[index];
                    color = itemTable.color;
                    name = itemTable.name ?? string.Empty;
                    tableId = itemTable.tableID;
                    break;
            }

            if (ctx.IsRequest)
                ctx.ReplyLayered(SendBasicSpawnTableInstantiation, spawnTableNetId, (byte)spawnType, name, tableId, color, owner);

            SendBasicSpawnTableInstantiation.Invoke(list, spawnTableNetId, (byte)spawnType, name, tableId, color, owner);
        }

        // todo SyncIfAuthority(index);

        return index;
    }

    /// <summary>
    /// Adds a spawn table tier of the provided <paramref name="spawnType"/> to the spawn table at <paramref name="spawnTableIndex"/> with the given <paramref name="name"/>. You can not add slots to zombies.
    /// </summary>
    /// <remarks>Replicates to clients.</remarks>
    /// <returns>Identifier of the created tier.</returns>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="InvalidOperationException">Too many spawn table tiers of that type are already on the table.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Spawn type was not one of the following: Animal, Vehicle, or Item.</exception>
    /// <exception cref="ArgumentException">Provided spawn table does not exist.</exception>
    public static SpawnTierIdentifier? AddSpawnTableTier(SpawnType spawnType, int spawnTableIndex, string name, EditorUser? owner = null)
    {
        ThreadUtil.assertIsGameThread();

        return AddSpawnTableTier(spawnType, spawnTableIndex, name, owner == null ? MessageContext.Nil : MessageContext.CreateFromCaller(owner));
    }
    internal static SpawnTierIdentifier? AddSpawnTableTier(SpawnType spawnType, int spawnTableIndex, string name, MessageContext ctx)
    {
        DevkitServerModule.AssertIsDevkitServerClient();

        ulong owner = ctx.GetCaller() is { } user ? user.SteamId.m_SteamID : 0ul;

        SpawnTierIdentifier? identifier = AddSpawnTableTierLocal(spawnType, spawnTableIndex, name);

        if (!identifier.HasValue)
            return null;

        SpawnTierIdentifier id = identifier.Value;

        SpawnsNetIdDatabase.TryGetSpawnTableNetId(spawnType, spawnTableIndex, out NetId64 spawnTableNetId);
        SpawnsNetIdDatabase.TryGetSpawnTierNetId(id, out NetId64 netId);

        if (spawnTableNetId.IsNull())
        {
            Logger.DevkitServer.LogWarning(nameof(AddSpawnTableTier), $"Unable to get {spawnType.GetLowercaseText()} spawn table Net Id: {spawnTableIndex.Format()}.");
            return identifier;
        }

        PooledTransportConnectionList list = ctx.IsRequest ? DevkitServerUtility.GetAllConnections(ctx.Connection) : DevkitServerUtility.GetAllConnections();

        float chance;
        switch (spawnType)
        {
            case SpawnType.Animal:
                AnimalTier animalTier = LevelAnimals.tables[id.TableIndex].tiers[id.TierIndex];
                name = animalTier.name ?? string.Empty;
                chance = animalTier.chance;
                break;

            case SpawnType.Vehicle:
                VehicleTier vehicleTier = LevelVehicles.tables[id.TableIndex].tiers[id.TierIndex];
                name = vehicleTier.name ?? string.Empty;
                chance = vehicleTier.chance;
                break;

            default: // Item
                ItemTier itemTier = LevelItems.tables[id.TableIndex].tiers[id.TierIndex];
                name = itemTier.name ?? string.Empty;
                chance = itemTier.chance;
                break;
        }

        if (ctx.IsRequest)
            ctx.ReplyLayered(SendSpawnTierInstantiation, netId, spawnTableNetId, (byte)spawnType, chance, name, owner);

        SendSpawnTierInstantiation.Invoke(list, netId, spawnTableNetId, (byte)spawnType, chance, name, owner);

        // todo SyncIfAuthority(id);

        return identifier;
    }

    /// <summary>
    /// Adds a spawn table tier asset of the provided <paramref name="spawnType"/> to the spawn tier at <paramref name="spawnTierIdentifier"/> with the given <paramref name="name"/>. You can not add slots to zombies.
    /// </summary>
    /// <remarks>Replicates to clients.</remarks>
    /// <returns>Identifier of the created tier, or null if it already exists.</returns>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="InvalidOperationException">Too many spawn table tiers of that type are already on the table.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Spawn type was not one of the following: Animal, Vehicle, or Item.</exception>
    /// <exception cref="ArgumentException">Provided spawn table does not exist.</exception>
    public static SpawnAssetIdentifier? AddSpawnTableAsset(SpawnType spawnType, SpawnTierIdentifier spawnTierIdentifier, ushort legacyId, EditorUser? owner = null)
    {
        ThreadUtil.assertIsGameThread();

        return AddSpawnTableAsset(spawnType, spawnTierIdentifier, legacyId, owner == null ? MessageContext.Nil : MessageContext.CreateFromCaller(owner));
    }
    internal static SpawnAssetIdentifier? AddSpawnTableAsset(SpawnType spawnType, SpawnTierIdentifier spawnTierIdentifier, ushort legacyId, MessageContext ctx)
    {
        DevkitServerModule.AssertIsDevkitServerClient();

        ulong owner = ctx.GetCaller() is { } user ? user.SteamId.m_SteamID : 0ul;

        SpawnAssetIdentifier? identifier = AddSpawnTableAssetLocal(spawnTierIdentifier, legacyId);

        if (!identifier.HasValue)
            return null;

        SpawnAssetIdentifier id = identifier.Value;

        SpawnsNetIdDatabase.TryGetSpawnTierNetId(spawnTierIdentifier, out NetId64 spawnTierNetId);
        SpawnsNetIdDatabase.TryGetSpawnAssetNetId(id, out NetId64 netId);

        if (spawnTierNetId.IsNull())
        {
            Logger.DevkitServer.LogWarning(nameof(AddSpawnTableTier), $"Unable to get {spawnType.GetLowercaseText()} spawn table tier Net Id: {spawnTierIdentifier.Format()}.");
            return identifier;
        }

        PooledTransportConnectionList list = ctx.IsRequest ? DevkitServerUtility.GetAllConnections(ctx.Connection) : DevkitServerUtility.GetAllConnections();

        switch (spawnType)
        {
            case SpawnType.Animal:
                AnimalSpawn animalSpawn = LevelAnimals.tables[id.TableIndex].tiers[id.TierIndex].table[id.AssetIndex];
                legacyId = animalSpawn.animal;
                break;

            case SpawnType.Vehicle:
                VehicleSpawn vehicleSpawn = LevelVehicles.tables[id.TableIndex].tiers[id.TierIndex].table[id.AssetIndex];
                legacyId = vehicleSpawn.vehicle;
                break;

            case SpawnType.Item:
                ItemSpawn itemSpawn = LevelItems.tables[id.TableIndex].tiers[id.TierIndex].table[id.AssetIndex];
                legacyId = itemSpawn.item;
                break;

            case SpawnType.Zombie:
                ZombieCloth zombieSpawn = LevelZombies.tables[id.TableIndex].slots[id.TierIndex].table[id.AssetIndex];
                legacyId = zombieSpawn.item;
                break;
        }

        if (ctx.IsRequest)
            ctx.ReplyLayered(SendSpawnAssetInstantiation, netId, spawnTierNetId, (byte)spawnType, legacyId, owner);

        SendSpawnAssetInstantiation.Invoke(list, netId, spawnTierNetId, (byte)spawnType, legacyId, owner);

        // todo SyncIfAuthority(id);

        return identifier;
    }
#elif CLIENT
    /// <summary>
    /// Sends a request to the server to add a spawn table with the specified <paramref name="spawnType"/> and <paramref name="name"/>.
    /// </summary>
    /// <remarks>Thread-safe.</remarks>
    /// <exception cref="InvalidOperationException">Not connected to a DevkitServer server.</exception>
    /// <returns>The index of the new table, or -1.</returns>
    public static async UniTask<int> RequestAddSpawnTable(SpawnType spawnType, string name, CancellationToken token = default)
    {
        DevkitServerModule.AssertIsDevkitServerClient();

        await UniTask.SwitchToMainThread(token);

        bool shouldAllow = true;
        if (ClientEvents.ListeningOnRequestInstantiateSpawnTableRequested)
            ClientEvents.InvokeOnRequestInstantiateSpawnTableRequested(new RequestInstantiateSpawnTableProperties(spawnType, name), ref shouldAllow);

        if (!shouldAllow)
            return -1;

        NetTask netTask = RequestSpawnTableInstantiation.Request(spawnType == SpawnType.Zombie ? SendZombieSpawnTableInstantiation : SendBasicSpawnTableInstantiation, (byte)spawnType, name, 10000);

        if (!ClientEvents.EventOnRequestInstantiateSpawnTable.IsEmpty)
            ClientEvents.EventOnRequestInstantiateSpawnTable.TryInvoke(new RequestInstantiateSpawnTableProperties(spawnType, name));

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

        if (!response.TryGetParameter(0, out NetId64 netId))
        {
            Logger.DevkitServer.LogWarning(nameof(RequestAddSpawnTable), $"Failed to get NetId from incoming SendSpawnTableInstantiation ({spawnType.GetLowercaseText()}).");
            return -1;
        }

        if (!SpawnsNetIdDatabase.TryGetSpawnTable(spawnType, netId, out int index))
        {
            Logger.DevkitServer.LogWarning(nameof(RequestAddSpawnTable), $"Failed to resolve NetId in spawn table database from incoming SendSpawnTableInstantiation ({spawnType.GetLowercaseText()}).");
            return -1;
        }

        if (index < GetTableCountUnsafe(spawnType))
            return index;

        Logger.DevkitServer.LogWarning(nameof(RequestAddSpawnTable), $"Failed to resolve NetId (index out of range) in spawn table database from incoming SendSpawnTableInstantiation ({spawnType.GetLowercaseText()}).");
        return -1;
    }

    /// <summary>
    /// Sends a request to the server to add a spawn table tier with the specified <paramref name="spawnType"/>, <paramref name="name"/>, and <paramref name="parentTableIndex"/>.
    /// </summary>
    /// <param name="name">Name of the tier, not used for zombies.</param>
    /// <remarks>Thread-safe.</remarks>
    /// <exception cref="InvalidOperationException">Not connected to a DevkitServer server.</exception>
    /// <returns>The index of the new table, or -1.</returns>
    public static async UniTask<SpawnTierIdentifier?> RequestAddSpawnTableTier(SpawnType spawnType, int parentTableIndex, string name, CancellationToken token = default)
    {
        DevkitServerModule.AssertIsDevkitServerClient();

        await UniTask.SwitchToMainThread(token);

        if (!SpawnsNetIdDatabase.TryGetSpawnTableNetId(spawnType, parentTableIndex, out NetId64 spawnTableNetId))
        {
            Logger.DevkitServer.LogWarning(nameof(RequestAddSpawnTableTier), $"Failed to get NetId of index ({spawnType.GetLowercaseText()}, {parentTableIndex.Format()}).");
            EditorMessage.SendEditorMessage(TranslationSource.DevkitServerMessageLocalizationSource, "Error", [ "NetId Missing" ]);
            return null;
        }

        bool shouldAllow = true;
        if (ClientEvents.ListeningOnRequestInstantiateSpawnTierRequested)
            ClientEvents.InvokeOnRequestInstantiateSpawnTierRequested(new RequestInstantiateSpawnTierProperties(spawnTableNetId, spawnType, name), ref shouldAllow);

        if (!shouldAllow)
            return null;

        NetTask netTask = RequestSpawnTierInstantiation.Request(SendSpawnTierInstantiation, spawnTableNetId, (byte)spawnType, name, 10000);

        if (!ClientEvents.EventOnRequestInstantiateSpawnTier.IsEmpty)
            ClientEvents.EventOnRequestInstantiateSpawnTier.TryInvoke(new RequestInstantiateSpawnTierProperties(spawnTableNetId, spawnType, name));

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
            return null;

        if (!response.TryGetParameter(0, out NetId64 tierNetId) || !response.TryGetParameter(1, out NetId64 tableNetId))
        {
            Logger.DevkitServer.LogWarning(nameof(RequestAddSpawnTableTier), $"Failed to get NetId from incoming SendSpawnTierInstantiation ({spawnType.GetLowercaseText()}, {parentTableIndex.Format()}).");
            return null;
        }

        if (tableNetId.Id != spawnTableNetId.Id)
        {
            Logger.DevkitServer.LogWarning(nameof(RequestAddSpawnTableTier), $"Received unexpected NetId from incoming SendSpawnTierInstantiation ({spawnType.GetLowercaseText()}, {parentTableIndex.Format()}). Didn't match expected.");
            return null;
        }

        if (!SpawnsNetIdDatabase.TryGetSpawnTableTier(spawnType, tierNetId, out SpawnTierIdentifier identifier))
        {
            Logger.DevkitServer.LogWarning(nameof(RequestAddSpawnTableTier), $"Failed to resolve NetId in spawn table database from incoming SendSpawnTierInstantiation ({spawnType.GetLowercaseText()}, {parentTableIndex.Format()}).");
            return null;
        }

        if (identifier.CheckSafe())
            return identifier;

        Logger.DevkitServer.LogWarning(nameof(RequestAddSpawnTableTier), $"Failed to resolve NetId (index out of range) in spawn table database from incoming SendSpawnTierInstantiation ({identifier.Format()}).");
        return null;
    }

    /// <summary>
    /// Sends a request to the server to add a spawn table tier asset with the specified <paramref name="spawnType"/>, <paramref name="legacyId"/>, and <paramref name="parentTierIdentifier"/>.
    /// </summary>
    /// <param name="legacyId">ID of the asset used. <see cref="ItemSpawn"/> and <see cref="ZombieCloth"/> should be <see cref="ItemAsset"/>, <see cref="VehicleSpawn"/> should be <see cref="VehicleAsset"/>, and <see cref="AnimalSpawn"/> should be <see cref="AnimalAsset"/>.</param>
    /// <remarks>Thread-safe.</remarks>
    /// <exception cref="InvalidOperationException">Not connected to a DevkitServer server.</exception>
    /// <returns>The index of the new table, or -1.</returns>
    public static async UniTask<SpawnAssetIdentifier?> RequestAddSpawnTableAsset(SpawnTierIdentifier parentTierIdentifier, ushort legacyId, CancellationToken token = default)
    {
        DevkitServerModule.AssertIsDevkitServerClient();

        await UniTask.SwitchToMainThread(token);

        SpawnType spawnType = parentTierIdentifier.Type;
        if (!SpawnsNetIdDatabase.TryGetSpawnTierNetId(parentTierIdentifier, out NetId64 spawnTierNetId))
        {
            Logger.DevkitServer.LogWarning(nameof(RequestAddSpawnTableAsset), $"Failed to get NetId of spawn table tier ({parentTierIdentifier.Format()}).");
            EditorMessage.SendEditorMessage(TranslationSource.DevkitServerMessageLocalizationSource, "Error", [ "NetId Missing" ]);
            return null;
        }

        if (!SpawnsNetIdDatabase.TryGetSpawnTableNetId(spawnType, parentTierIdentifier.TableIndex, out NetId64 tableNetId))
        {
            Logger.DevkitServer.LogWarning(nameof(RequestAddSpawnTableAsset), $"Failed to get NetId of spawn table ({spawnType.Format()}, {parentTierIdentifier.TableIndex.Format()}).");
        }

        bool shouldAllow = true;
        if (ClientEvents.ListeningOnRequestInstantiateSpawnTierAssetRequested)
            ClientEvents.InvokeOnRequestInstantiateSpawnTierAssetRequested(new RequestInstantiateSpawnTierAssetProperties(tableNetId, spawnTierNetId, spawnType, legacyId), ref shouldAllow);

        if (!shouldAllow)
            return null;

        NetTask netTask = RequestSpawnAssetInstantiation.Request(SendSpawnAssetInstantiation, spawnTierNetId, (byte)spawnType, legacyId, 10000);

        if (!ClientEvents.EventOnRequestInstantiateSpawnTierAsset.IsEmpty)
            ClientEvents.EventOnRequestInstantiateSpawnTierAsset.TryInvoke(new RequestInstantiateSpawnTierAssetProperties(tableNetId, spawnTierNetId, spawnType, legacyId));

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
            return null;

        if (!response.TryGetParameter(0, out NetId64 assetNetId) || !response.TryGetParameter(1, out NetId64 tierNetId))
        {
            Logger.DevkitServer.LogWarning(nameof(RequestAddSpawnTableAsset), $"Failed to get NetId from incoming SendSpawnAssetInstantiation ({parentTierIdentifier.Format()}).");
            return null;
        }

        if (tierNetId.Id != spawnTierNetId.Id)
        {
            Logger.DevkitServer.LogWarning(nameof(RequestAddSpawnTableAsset), $"Received unexpected NetId from incoming SendSpawnAssetInstantiation ({parentTierIdentifier.Format()}). Didn't match expected.");
            return null;
        }

        if (!SpawnsNetIdDatabase.TryGetSpawnTableTierAsset(parentTierIdentifier.Type, assetNetId, out SpawnAssetIdentifier identifier))
        {
            Logger.DevkitServer.LogWarning(nameof(RequestAddSpawnTableAsset), $"Failed to resolve NetId in spawn table database from incoming SendSpawnAssetInstantiation ({parentTierIdentifier.Format()}).");
            return null;
        }

        if (identifier.CheckSafe())
            return identifier;

        Logger.DevkitServer.LogWarning(nameof(RequestAddSpawnTableAsset), $"Failed to resolve NetId (index out of range) in spawn table database from incoming SendSpawnAssetInstantiation ({identifier.Format()}).");
        return null;
    }
#endif

#if SERVER
    [NetCall(NetCallSource.FromClient, DevkitServerNetCall.RequestSpawnTableInstantiation)]
    internal static void ReceiveSpawnTableInstantiationRequest(MessageContext ctx, byte spawnTypePacked, string name)
    {
        EditorUser? user = ctx.GetCaller();
        SpawnType spawnType = (SpawnType)spawnTypePacked;
        if (user == null || !user.IsOnline)
        {
            Logger.DevkitServer.LogError(nameof(ReceiveSpawnTableInstantiationRequest), $"Unable to get user from {spawnType.GetLowercaseText()} spawn table instantiation request.");
            ctx.Acknowledge(StandardErrorCode.NoPermissions);
            return;
        }

        if (!VanillaPermissions.SpawnTablesAdd(spawnType).Has(user))
        {
            ctx.Acknowledge(StandardErrorCode.NoPermissions);
            EditorMessage.SendNoPermissionMessage(user, VanillaPermissions.SpawnTablesAdd(spawnType));
            return;
        }

        int index = AddSpawnTable(spawnType, name, ctx);

        if (index == -1)
        {
            Logger.DevkitServer.LogDebug(nameof(ReceiveSpawnTableInstantiationRequest), $"Failed to grant request for instantiation of {spawnType.GetLowercaseText()} spawn table {name.Format(true)} from {user.SteamId.Format()}.");
            ctx.Acknowledge(StandardErrorCode.GenericError);
            return;
        }

        Logger.DevkitServer.LogDebug(nameof(ReceiveSpawnTableInstantiationRequest), $"Granted request for instantiation of {spawnType.GetLowercaseText()} spawn table {name.Format(true)} #{index.Format()} from {user.SteamId.Format()}.");

        ctx.Acknowledge(StandardErrorCode.Success);
    }

    [NetCall(NetCallSource.FromClient, DevkitServerNetCall.RequestSpawnTierInstantiation)]
    internal static void ReceiveSpawnTierInstantiationRequest(MessageContext ctx, NetId64 spawnTableNetId, byte spawnTypePacked, string name)
    {
        EditorUser? user = ctx.GetCaller();
        SpawnType spawnType = (SpawnType)spawnTypePacked;
        if (user == null || !user.IsOnline)
        {
            Logger.DevkitServer.LogError(nameof(ReceiveSpawnTierInstantiationRequest), $"Unable to get user from {spawnType.GetLowercaseText()} spawn table tier instantiation request.");
            ctx.Acknowledge(StandardErrorCode.NoPermissions);
            return;
        }

        if (!VanillaPermissions.SpawnTablesEdit(spawnType).Has(user))
        {
            ctx.Acknowledge(StandardErrorCode.NoPermissions);
            EditorMessage.SendNoPermissionMessage(user, VanillaPermissions.SpawnTablesEdit(spawnType));
            return;
        }

        if (!SpawnsNetIdDatabase.TryGetSpawnTable(spawnType, spawnTableNetId, out int spawnTableIndex))
        {
            Logger.DevkitServer.LogDebug(nameof(ReceiveSpawnTierInstantiationRequest), $"Failed to resolve NetId of {spawnType.GetLowercaseText()} spawn table {spawnTableNetId.Format()} from {user.SteamId.Format()}.");
            ctx.Acknowledge(StandardErrorCode.NotFound);
            return;
        }

        SpawnTierIdentifier? identifier = AddSpawnTableTier(spawnType, spawnTableIndex, name, ctx);

        if (!identifier.HasValue)
        {
            Logger.DevkitServer.LogDebug(nameof(ReceiveSpawnTierInstantiationRequest), $"Failed to grant request for instantiation of {spawnType.GetLowercaseText()} spawn table tier {name.Format(true)} from {user.SteamId.Format()}.");
            ctx.Acknowledge(StandardErrorCode.GenericError);
            return;
        }

        Logger.DevkitServer.LogDebug(nameof(ReceiveSpawnTierInstantiationRequest), $"Granted request for instantiation of {spawnType.GetLowercaseText()} spawn table tier {name.Format(true)} #{identifier.Value.Format()} from {user.SteamId.Format()}.");

        ctx.Acknowledge(StandardErrorCode.Success);
    }

    [NetCall(NetCallSource.FromClient, DevkitServerNetCall.RequestSpawnAssetInstantiation)]
    internal static void ReceiveSpawnAssetInstantiationRequest(MessageContext ctx, NetId64 spawnTableNetId, byte spawnTypePacked, ushort legacyId)
    {
        EditorUser? user = ctx.GetCaller();
        SpawnType spawnType = (SpawnType)spawnTypePacked;
        if (user == null || !user.IsOnline)
        {
            Logger.DevkitServer.LogError(nameof(ReceiveSpawnAssetInstantiationRequest), $"Unable to get user from {spawnType.GetLowercaseText()} spawn table tier asset instantiation request.");
            ctx.Acknowledge(StandardErrorCode.NoPermissions);
            return;
        }

        if (!VanillaPermissions.SpawnTablesEdit(spawnType).Has(user))
        {
            ctx.Acknowledge(StandardErrorCode.NoPermissions);
            EditorMessage.SendNoPermissionMessage(user, VanillaPermissions.SpawnTablesEdit(spawnType));
            return;
        }

        if (!SpawnsNetIdDatabase.TryGetSpawnTableTier(spawnType, spawnTableNetId, out SpawnTierIdentifier spawnTierIdentifier))
        {
            Logger.DevkitServer.LogDebug(nameof(ReceiveSpawnAssetInstantiationRequest), $"Failed to resolve NetId of {spawnType.GetLowercaseText()} spawn table tier {spawnTableNetId.Format()} from {user.SteamId.Format()}.");
            ctx.Acknowledge(StandardErrorCode.NotFound);
            return;
        }

        SpawnAssetIdentifier? identifier = AddSpawnTableAsset(spawnType, spawnTierIdentifier, legacyId, ctx);

        if (!identifier.HasValue)
        {
            Logger.DevkitServer.LogDebug(nameof(ReceiveSpawnAssetInstantiationRequest), $"Failed to grant request for instantiation of {spawnType.GetLowercaseText()} spawn table tier asset {legacyId.Format()} from {user.SteamId.Format()}.");
            ctx.Acknowledge(StandardErrorCode.GenericError);
            return;
        }

        Logger.DevkitServer.LogDebug(nameof(ReceiveSpawnAssetInstantiationRequest), $"Granted request for instantiation of {spawnType.GetLowercaseText()} spawn table tier asset {legacyId.Format()} #{identifier.Value.Format()} from {user.SteamId.Format()}.");

        ctx.Acknowledge(StandardErrorCode.Success);
    }

#elif CLIENT

    [NetCall(NetCallSource.FromServer, DevkitServerNetCall.SendBasicSpawnTableInstantiation)]
    internal static StandardErrorCode ReceiveBasicSpawnTableInstantiation(MessageContext ctx, NetId64 netId, byte spawnTypePacked, string name, ushort tableId, Color color, ulong owner)
    {
        SpawnType spawnType = (SpawnType)spawnTypePacked;

        if (!EditorActions.HasProcessedPendingSpawnTables)
        {
            EditorActions.TemporaryEditorActions?.QueueBasicSpawnTableInstantiation(netId, spawnType, tableId, name, color, owner);
            return StandardErrorCode.Success;
        }

        try
        {
            int index = AddSpawnTableLocal(spawnType, name);

            if (index == -1)
                return StandardErrorCode.GenericError;

            SpawnsNetIdDatabase.RegisterSpawnTable(spawnType, index, netId);
            Logger.DevkitServer.LogDebug(nameof(ReceiveBasicSpawnTableInstantiation), $"Assigned {spawnType.GetLowercaseText()} spawn table NetId: {netId.Format()}.");

            if (netId == NetId64.Invalid)
                return StandardErrorCode.GenericError;

            switch (spawnType)
            {
                case SpawnType.Animal:
                    AnimalTable animalTable = LevelAnimals.tables[index];
                    
                    if (!string.Equals(animalTable.name, name, StringComparison.Ordinal))
                        SetSpawnTableNameLocal(spawnType, index, name);

                    if (animalTable.color != color)
                        SetSpawnTableColorLocal(spawnType, index, color);

                    if (animalTable.tableID != tableId)
                        SetSpawnTableSpawnAssetLocal(spawnType, index, tableId);

                    int ct = Math.Min(byte.MaxValue, animalTable.tiers.Count);
                    for (int i = 0; i < ct; ++i)
                        RemoveSpawnTableTierLocal(new SpawnTierIdentifier(spawnType, (byte)index, (byte)i));

                    break;

                case SpawnType.Vehicle:
                    VehicleTable vehicleTable = LevelVehicles.tables[index];
                    
                    if (!string.Equals(vehicleTable.name, name, StringComparison.Ordinal))
                        SetSpawnTableNameLocal(spawnType, index, name);

                    if (vehicleTable.color != color)
                        SetSpawnTableColorLocal(spawnType, index, color);

                    if (vehicleTable.tableID != tableId)
                        SetSpawnTableSpawnAssetLocal(spawnType, index, tableId);

                    ct = Math.Min(byte.MaxValue, vehicleTable.tiers.Count);
                    for (int i = 0; i < ct; ++i)
                        RemoveSpawnTableTierLocal(new SpawnTierIdentifier(spawnType, (byte)index, (byte)i));

                    break;

                case SpawnType.Item:
                    ItemTable itemTable = LevelItems.tables[index];
                    
                    if (!string.Equals(itemTable.name, name, StringComparison.Ordinal))
                        SetSpawnTableNameLocal(spawnType, index, name);

                    if (itemTable.color != color)
                        SetSpawnTableColorLocal(spawnType, index, color);

                    if (itemTable.tableID != tableId)
                        SetSpawnTableSpawnAssetLocal(spawnType, index, tableId);

                    ct = Math.Min(byte.MaxValue, itemTable.tiers.Count);
                    for (int i = 0; i < ct; ++i)
                        RemoveSpawnTableTierLocal(new SpawnTierIdentifier(spawnType, (byte)index, (byte)i));

                    break;
            }

            if (Level.isEditor)
            {
                UpdateUITable(spawnType);

                if (IsTableSelected(spawnType, index))
                    UpdateUISelection(spawnType);
            }

            if (owner == Provider.client.m_SteamID && IsEditingSpawns(spawnType))
                SelectTable(spawnType, index, false, false);

            // todo SyncIfAuthority(spawnType, index);
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError(nameof(ReceiveBasicSpawnTableInstantiation), ex, $"Failed to initialize {spawnType.GetLowercaseText()} spawn table: {name.Format(true)} - {netId.Format()}.");
            return StandardErrorCode.GenericError;
        }

        return StandardErrorCode.Success;
    }

    [NetCall(NetCallSource.FromServer, DevkitServerNetCall.SendZombieSpawnTableInstantiation)]
    internal static StandardErrorCode ReceiveZombieSpawnTableInstantiation(MessageContext ctx, NetId64 netId, string name, long packed, uint xp, float regen, NetId64 lootTableNetId, Guid difficultyAsset, Color color, ulong[] tierNetIds, ulong owner)
    {
        if (!EditorActions.HasProcessedPendingSpawnTables)
        {
            EditorActions.TemporaryEditorActions?.QueueZombieSpawnTableInstantiation(netId, name, color, owner, packed, lootTableNetId, xp, regen, difficultyAsset, tierNetIds);
            return StandardErrorCode.Success;
        }

        try
        {
            bool isMega = (packed & 1) != 0;
            byte damage = (byte)((packed >> 8) & 0xFF);
            ushort health = (ushort)((packed >> 16) & 0xFFFF);
            ushort lootId = (ushort)((packed >> 32) & 0xFFFF);

            int index = AddSpawnTableLocal(SpawnType.Zombie, name);

            if (index == -1)
                return StandardErrorCode.GenericError;

            SpawnsNetIdDatabase.RegisterSpawnTable(SpawnType.Zombie, index, netId);
            Logger.DevkitServer.LogDebug(nameof(ReceiveZombieSpawnTableInstantiation), $"Assigned zombie spawn table NetId: {netId.Format()}.");

            if (netId == NetId64.Invalid)
                return StandardErrorCode.GenericError;

            ZombieTable zombieTable = LevelZombies.tables[index];

            int ct = Math.Min(tierNetIds.Length, zombieTable.slots.Length);
            for (int i = 0; i < ct; ++i)
            {
                NetId64 tierNetId = new NetId64(tierNetIds[i]);
                SpawnsNetIdDatabase.RegisterSpawnTier(new SpawnTierIdentifier(SpawnType.Zombie, (byte)index, (byte)i), tierNetId);
                Logger.DevkitServer.LogConditional(nameof(ReceiveZombieSpawnTableInstantiation), $"Assigned zombie spawn table tier {i.Format()} NetId: {tierNetId.Format()}.");
            }

            if (!string.Equals(zombieTable.name, name, StringComparison.Ordinal))
                SetSpawnTableNameLocal(SpawnType.Zombie, index, name);

            if (zombieTable.color != color)
                SetSpawnTableColorLocal(SpawnType.Zombie, index, color);

            if (zombieTable.lootID != lootId)
                SetSpawnTableSpawnAssetLocal(SpawnType.Zombie, index, lootId);

            if (zombieTable.isMega != isMega)
                SetZombieSpawnTableIsMegaLocal(index, isMega);

            if (zombieTable.health != health)
                SetZombieSpawnTableHealthLocal(index, health);

            if (zombieTable.damage != damage)
                SetZombieSpawnTableHealthLocal(index, damage);

            if (zombieTable.xp != xp)
                SetZombieSpawnTableXPLocal(index, xp);

            if (zombieTable.regen != regen)
                SetZombieSpawnTableRegenLocal(index, regen);

            if (zombieTable.difficulty.GUID != difficultyAsset)
                SetZombieSpawnTableDifficultyAssetLocal(index, new AssetReference<ZombieDifficultyAsset>(difficultyAsset));

            if (Level.isEditor)
            {
                UpdateUITable(SpawnType.Zombie);

                if (IsTableSelected(SpawnType.Zombie, index))
                    UpdateUISelection(SpawnType.Zombie);
            }

            if (owner == Provider.client.m_SteamID && IsEditingSpawns(SpawnType.Zombie))
                SelectZombieTable(index, false, false);

            NetId64 existingLootTableNetId = NetId64.Invalid;
            if (zombieTable.lootIndex < LevelItems.tables.Count)
                SpawnsNetIdDatabase.TryGetSpawnTableNetId(SpawnType.Item, zombieTable.lootIndex, out existingLootTableNetId);

            if (existingLootTableNetId != lootTableNetId
                && SpawnsNetIdDatabase.TryGetSpawnTable(SpawnType.Item, lootTableNetId, out int lootIndex)
                && lootIndex < byte.MaxValue)
            {
                zombieTable.lootIndex = (byte)lootIndex;
            }

            // todo SyncIfAuthority(SpawnType.Zombie, index);
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError(nameof(ReceiveBasicSpawnTableInstantiation), ex, $"Failed to initialize zombie spawn table: {name.Format(true)} - {netId.Format()}.");
            return StandardErrorCode.GenericError;
        }

        return StandardErrorCode.Success;
    }

    [NetCall(NetCallSource.FromServer, DevkitServerNetCall.SendSpawnTierInstantiation)]
    internal static StandardErrorCode ReceiveSpawnTierInstantiation(MessageContext ctx, NetId64 netId, NetId64 tableNetId, byte spawnTypePacked, float chance, string name, ulong owner)
    {
        SpawnType spawnType = (SpawnType)spawnTypePacked;

        if (!EditorActions.HasProcessedPendingSpawnAssets)
        {
            EditorActions.TemporaryEditorActions?.QueueSpawnTierInstantiation(netId, tableNetId, spawnType, chance, name, owner);
            return StandardErrorCode.Success;
        }

        try
        {
            bool isMe = owner == Provider.client.m_SteamID && IsEditingSpawns(spawnType);

            if (!SpawnsNetIdDatabase.TryGetSpawnTable(spawnType, tableNetId, out int tableIndex) || !CheckSpawnTableSafe(spawnType, tableIndex))
            {
                Logger.DevkitServer.LogWarning(nameof(ReceiveSpawnTierInstantiation), $"Unable to find table for NetId {tableNetId.Format()} when instantiating {spawnType.GetLowercaseText()} tier: {name.Format(true)} - {netId.Format()}.");
                if (isMe)
                    EditorMessage.SendEditorMessage(TranslationSource.DevkitServerMessageLocalizationSource, "Error", [ "NetId Missing" ]);
                
                return StandardErrorCode.InvalidData;
            }

            SpawnTierIdentifier? identifier = AddSpawnTableTierLocal(spawnType, tableIndex, name);

            if (!identifier.HasValue)
                return StandardErrorCode.GenericError;

            SpawnTierIdentifier id = identifier.Value;

            SpawnsNetIdDatabase.RegisterSpawnTier(id, netId);
            Logger.DevkitServer.LogDebug(nameof(ReceiveSpawnTierInstantiation), $"Assigned {spawnType.GetLowercaseText()} spawn table tier NetId: {netId.Format()}.");

            if (netId == NetId64.Invalid)
                return StandardErrorCode.GenericError;

            switch (spawnType)
            {
                case SpawnType.Animal:
                    AnimalTier animalTable = LevelAnimals.tables[id.TableIndex].tiers[id.TierIndex];

                    if (!string.Equals(animalTable.name, name, StringComparison.Ordinal))
                        SetSpawnTableTierNameLocal(id, name);

                    if (animalTable.chance != chance)
                        SetSpawnTableTierChanceLocal(id, chance);

                    int ct = Math.Min(byte.MaxValue, animalTable.table.Count);
                    for (int i = 0; i < ct; ++i)
                        RemoveSpawnTableTierAssetLocal(new SpawnAssetIdentifier(spawnType, (byte)id.TableIndex, (byte)id.TierIndex, (byte)i));

                    break;

                case SpawnType.Vehicle:
                    VehicleTier vehicleTable = LevelVehicles.tables[id.TableIndex].tiers[id.TierIndex];

                    if (!string.Equals(vehicleTable.name, name, StringComparison.Ordinal))
                        SetSpawnTableTierNameLocal(id, name);

                    if (vehicleTable.chance != chance)
                        SetSpawnTableTierChanceLocal(id, chance);

                    ct = Math.Min(byte.MaxValue, vehicleTable.table.Count);
                    for (int i = 0; i < ct; ++i)
                        RemoveSpawnTableTierAssetLocal(new SpawnAssetIdentifier(spawnType, (byte)id.TableIndex, (byte)id.TierIndex, (byte)i));

                    break;

                case SpawnType.Item:
                    ItemTier itemTable = LevelItems.tables[id.TableIndex].tiers[id.TierIndex];

                    if (!string.Equals(itemTable.name, name, StringComparison.Ordinal))
                        SetSpawnTableTierNameLocal(id, name);

                    if (itemTable.chance != chance)
                        SetSpawnTableTierChanceLocal(id, chance);

                    ct = Math.Min(byte.MaxValue, itemTable.table.Count);
                    for (int i = 0; i < ct; ++i)
                        RemoveSpawnTableTierAssetLocal(new SpawnAssetIdentifier(spawnType, (byte)id.TableIndex, (byte)id.TierIndex, (byte)i));

                    break;
            }

            if (Level.isEditor)
            {
                UpdateUITable(spawnType);

                if (IsTableSelected(spawnType, tableIndex))
                    UpdateUISelection(spawnType);
            }

            if (isMe)
                TrySelectTier(id);

            // todo SyncIfAuthority(id);
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError(nameof(ReceiveSpawnTierInstantiation), ex, $"Failed to initialize {spawnType.GetLowercaseText()} spawn table tier: {name.Format(true)} - {netId.Format()}.");
            return StandardErrorCode.GenericError;
        }

        return StandardErrorCode.Success;
    }

    [NetCall(NetCallSource.FromServer, DevkitServerNetCall.SendSpawnAssetInstantiation)]
    internal static StandardErrorCode ReceiveSpawnAssetInstantiation(MessageContext ctx, NetId64 netId, NetId64 tierNetId, byte spawnTypePacked, ushort legacyId, ulong owner)
    {
        SpawnType spawnType = (SpawnType)spawnTypePacked;

        if (!EditorActions.HasProcessedPendingSpawnTiers)
        {
            EditorActions.TemporaryEditorActions?.QueueSpawnAssetInstantiation(netId, tierNetId, spawnType, legacyId, owner);
            return StandardErrorCode.Success;
        }

        try
        {
            bool isMe = owner == Provider.client.m_SteamID && IsEditingSpawns(spawnType);

            if (!SpawnsNetIdDatabase.TryGetSpawnTableTier(spawnType, tierNetId, out SpawnTierIdentifier tierId) || !tierId.CheckSafe())
            {
                Logger.DevkitServer.LogWarning(nameof(ReceiveSpawnAssetInstantiation), $"Unable to find tier for NetId {tierNetId.Format()} when instantiating {spawnType.GetLowercaseText()} asset: {legacyId.Format()} - {netId.Format()}.");
                if (isMe)
                    EditorMessage.SendEditorMessage(TranslationSource.DevkitServerMessageLocalizationSource, "Error", [ "NetId Missing" ]);
                
                return StandardErrorCode.InvalidData;
            }

            SpawnAssetIdentifier? identifier = AddSpawnTableAssetLocal(tierId, legacyId);

            if (!identifier.HasValue)
                return StandardErrorCode.GenericError;

            SpawnAssetIdentifier id = identifier.Value;

            SpawnsNetIdDatabase.RegisterSpawnAsset(id, netId);
            Logger.DevkitServer.LogDebug(nameof(ReceiveSpawnAssetInstantiation), $"Assigned {spawnType.GetLowercaseText()} spawn table tier asset NetId: {netId.Format()}.");

            if (netId == NetId64.Invalid)
                return StandardErrorCode.GenericError;

            switch (spawnType)
            {
                case SpawnType.Animal:
                    AnimalSpawn animalTable = LevelAnimals.tables[id.TableIndex].tiers[id.TierIndex].table[id.AssetIndex];

                    if (animalTable.animal != legacyId)
                        SetSpawnTableTierAssetLocal(id, legacyId);

                    break;

                case SpawnType.Vehicle:
                    VehicleSpawn vehicleTable = LevelVehicles.tables[id.TableIndex].tiers[id.TierIndex].table[id.AssetIndex];

                    if (vehicleTable.vehicle != legacyId)
                        SetSpawnTableTierAssetLocal(id, legacyId);

                    break;

                case SpawnType.Item:
                    ItemSpawn itemTable = LevelItems.tables[id.TableIndex].tiers[id.TierIndex].table[id.AssetIndex];

                    if (itemTable.item != legacyId)
                        SetSpawnTableTierAssetLocal(id, legacyId);

                    break;

                case SpawnType.Zombie:
                    ZombieCloth zombieTable = LevelZombies.tables[id.TableIndex].slots[id.TierIndex].table[id.AssetIndex];

                    if (zombieTable.item != legacyId)
                        SetSpawnTableTierAssetLocal(id, legacyId);

                    break;
            }

            if (Level.isEditor)
            {
                UpdateUITable(spawnType);

                if (IsTableSelected(spawnType, id.TableIndex))
                    UpdateUISelection(spawnType);
            }

            if (isMe)
                TrySelectTierAsset(id);

            // todo SyncIfAuthority(id);
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError(nameof(ReceiveSpawnAssetInstantiation), ex, $"Failed to initialize {spawnType.GetLowercaseText()} spawn table tier asset: {legacyId.Format()} - {netId.Format()}.");
            return StandardErrorCode.GenericError;
        }

        return StandardErrorCode.Success;
    }
#endif
}