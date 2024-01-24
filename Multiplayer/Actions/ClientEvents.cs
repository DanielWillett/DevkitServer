using DevkitServer.API;

#if CLIENT
namespace DevkitServer.Multiplayer.Actions;

/// <summary>
/// Contains client-side events for cancelling and invoking actions during only multiplayer editing.
/// </summary>
[EarlyTypeInit]
public static class ClientEvents
{
    internal static CachedMulticastEvent<EditHeightmapRequest> EventOnEditHeightmapPermissionDenied = new CachedMulticastEvent<EditHeightmapRequest>(typeof(ClientEvents), nameof(OnEditHeightmapPermissionDenied), false);
    internal static CachedMulticastEvent<EditSplatmapRequest> EventOnEditSplatmapPermissionDenied = new CachedMulticastEvent<EditSplatmapRequest>(typeof(ClientEvents), nameof(EventOnEditSplatmapPermissionDenied), false);
    internal static CachedMulticastEvent<EditHolesRequest> EventOnEditHolesPermissionDenied = new CachedMulticastEvent<EditHolesRequest>(typeof(ClientEvents), nameof(OnEditHolesPermissionDenied), false);

    internal static CachedMulticastEvent<EditHeightmapRequest> EventOnEditHeightmapRequested = new CachedMulticastEvent<EditHeightmapRequest>(typeof(ClientEvents), nameof(OnEditHeightmapRequested));
    internal static CachedMulticastEvent<EditSplatmapRequest> EventOnEditSplatmapRequested = new CachedMulticastEvent<EditSplatmapRequest>(typeof(ClientEvents), nameof(OnEditSplatmapRequested));
    internal static CachedMulticastEvent<EditHolesRequest> EventOnEditHolesRequested = new CachedMulticastEvent<EditHolesRequest>(typeof(ClientEvents), nameof(OnEditHolesRequested));
    
    internal static CachedMulticastEvent<TryInstantiateHierarchyObject> EventOnTryInstantiateHierarchyObject = new CachedMulticastEvent<TryInstantiateHierarchyObject>(typeof(ClientEvents), nameof(OnTryInstantiateHierarchyObject));
    internal static CachedMulticastEvent<RequestInstantiateHierarchyObject> EventOnRequestInstantiateHierarchyObject = new CachedMulticastEvent<RequestInstantiateHierarchyObject>(typeof(ClientEvents), nameof(OnRequestInstantiateHierarchyObject));
    internal static CachedMulticastEvent<RequestInstantiateRoad> EventOnRequestInstantiateRoad = new CachedMulticastEvent<RequestInstantiateRoad>(typeof(ClientEvents), nameof(OnRequestInstantiateRoad));
    internal static CachedMulticastEvent<RequestInstantiateRoadVertex> EventOnRequestInstantiateRoadVertex = new CachedMulticastEvent<RequestInstantiateRoadVertex>(typeof(ClientEvents), nameof(OnRequestInstantiateRoadVertex));
    internal static CachedMulticastEvent<RequestInstantiateNavigation> EventOnRequestInstantiateNavigation = new CachedMulticastEvent<RequestInstantiateNavigation>(typeof(ClientEvents), nameof(OnRequestInstantiateNavigation));
    internal static CachedMulticastEvent<RequestInstantiateSpawnpoint> EventOnRequestInstantiateSpawnpoint = new CachedMulticastEvent<RequestInstantiateSpawnpoint>(typeof(ClientEvents), nameof(OnRequestInstantiateSpawnpoint));
    internal static CachedMulticastEvent<RequestInstantiateSpawnTable> EventOnRequestInstantiateSpawnTable = new CachedMulticastEvent<RequestInstantiateSpawnTable>(typeof(ClientEvents), nameof(OnRequestInstantiateSpawnTable));
    internal static CachedMulticastEvent<RequestInstantiateSpawnTier> EventOnRequestInstantiateSpawnTier = new CachedMulticastEvent<RequestInstantiateSpawnTier>(typeof(ClientEvents), nameof(OnRequestInstantiateSpawnTier));
    internal static CachedMulticastEvent<RequestInstantiateSpawnTierAsset> EventOnRequestInstantiateSpawnTierAsset = new CachedMulticastEvent<RequestInstantiateSpawnTierAsset>(typeof(ClientEvents), nameof(OnRequestInstantiateSpawnTierAsset));

    public static event EditHeightmapRequest OnEditHeightmapPermissionDenied
    {
        add => EventOnEditHeightmapPermissionDenied.Add(value);
        remove => EventOnEditHeightmapPermissionDenied.Remove(value);
    }
    public static event EditSplatmapRequest OnEditSplatmapPermissionDenied
    {
        add => EventOnEditSplatmapPermissionDenied.Add(value);
        remove => EventOnEditSplatmapPermissionDenied.Remove(value);
    }
    public static event EditHolesRequest OnEditHolesPermissionDenied
    {
        add => EventOnEditHolesPermissionDenied.Add(value);
        remove => EventOnEditHolesPermissionDenied.Remove(value);
    }

    public static event EditHeightmapRequest OnEditHeightmapRequested
    {
        add => EventOnEditHeightmapRequested.Add(value);
        remove => EventOnEditHeightmapRequested.Remove(value);
    }
    public static event EditSplatmapRequest OnEditSplatmapRequested
    {
        add => EventOnEditSplatmapRequested.Add(value);
        remove => EventOnEditSplatmapRequested.Remove(value);
    }
    public static event EditHolesRequest OnEditHolesRequested
    {
        add => EventOnEditHolesRequested.Add(value);
        remove => EventOnEditHolesRequested.Remove(value);
    }

    public static event TryInstantiateHierarchyObject OnTryInstantiateHierarchyObject
    {
        add => EventOnTryInstantiateHierarchyObject.Add(value);
        remove => EventOnTryInstantiateHierarchyObject.Remove(value);
    }
    public static event RequestInstantiateHierarchyObject OnRequestInstantiateHierarchyObject
    {
        add => EventOnRequestInstantiateHierarchyObject.Add(value);
        remove => EventOnRequestInstantiateHierarchyObject.Remove(value);
    }
    public static event RequestInstantiateRoad OnRequestInstantiateRoad
    {
        add => EventOnRequestInstantiateRoad.Add(value);
        remove => EventOnRequestInstantiateRoad.Remove(value);
    }
    public static event RequestInstantiateRoadVertex OnRequestInstantiateRoadVertex
    {
        add => EventOnRequestInstantiateRoadVertex.Add(value);
        remove => EventOnRequestInstantiateRoadVertex.Remove(value);
    }
    public static event RequestInstantiateNavigation OnRequestInstantiateNavigation
    {
        add => EventOnRequestInstantiateNavigation.Add(value);
        remove => EventOnRequestInstantiateNavigation.Remove(value);
    }
    public static event RequestInstantiateSpawnpoint OnRequestInstantiateSpawnpoint
    {
        add => EventOnRequestInstantiateSpawnpoint.Add(value);
        remove => EventOnRequestInstantiateSpawnpoint.Remove(value);
    }
    public static event RequestInstantiateSpawnTable OnRequestInstantiateSpawnTable
    {
        add => EventOnRequestInstantiateSpawnTable.Add(value);
        remove => EventOnRequestInstantiateSpawnTable.Remove(value);
    }
    public static event RequestInstantiateSpawnTier OnRequestInstantiateSpawnTier
    {
        add => EventOnRequestInstantiateSpawnTier.Add(value);
        remove => EventOnRequestInstantiateSpawnTier.Remove(value);
    }
    public static event RequestInstantiateSpawnTierAsset OnRequestInstantiateSpawnTierAsset
    {
        add => EventOnRequestInstantiateSpawnTierAsset.Add(value);
        remove => EventOnRequestInstantiateSpawnTierAsset.Remove(value);
    }

    public static event PaintRamp? OnPaintRamp;
    public static event AdjustHeightmap? OnAdjustHeightmap;
    public static event FlattenHeightmap? OnFlattenHeightmap;
    public static event SmoothHeightmap? OnSmoothHeightmap;
    public static event PaintSplatmap? OnPaintSplatmap;
    public static event PaintSplatmap? OnAutoPaintSplatmap;
    public static event SmoothSplatmap? OnSmoothSplatmap;
    public static event PaintHoles? OnPaintHoles;
    public static event UpdateLandscapeTile? OnAddTile;
    public static event UpdateLandscapeTile? OnDeleteTile;
    public static event AddFoliage? OnAddFoliage;
    public static event RemoveFoliage? OnRemoveFoliage;
    public static event RemoveResourceSpawnpointFoliage? OnRemoveResourceSpawnpointFoliage;
    public static event RemoveLevelObjectFoliage? OnRemoveLevelObjectFoliage;
    public static event UpdateLandscapeTile? OnUpdateTileSplatmapLayers;
    public static event MoveHierarchyObjectFinal? OnMoveHierarchyObjectFinal;
    public static event MoveHierarchyObjectsFinal? OnMoveHierarchyObjectsFinal;
    public static event DeleteHierarchyObject? OnDeleteHierarchyObject;
    public static event DeleteHierarchyObjects? OnDeleteHierarchyObjects;
    public static event DeleteLevelObject? OnDeleteLevelObject;
    public static event DeleteLevelObjects? OnDeleteLevelObjects;
    public static event MoveLevelObjectFinal? OnMoveLevelObjectFinal;
    public static event MoveLevelObjectsFinal? OnMoveLevelObjectsFinal;
    public static event UpdateObjectsCustomMaterialPaletteOverride? OnUpdateObjectsCustomMaterialPaletteOverride;
    public static event UpdateObjectsMaterialIndexOverride? OnUpdateObjectsMaterialIndexOverride;
    public static event MoveRoadVertex? OnMoveRoadVertex;
    public static event MoveRoadTangentHandle? OnMoveRoadTangentHandle;
    public static event DeleteRoadVertex? OnDeleteRoadVertex;
    public static event DeleteRoad? OnDeleteRoad;
    public static event MoveRoadVertexRequested? OnMoveRoadVertexRequested;
    public static event MoveRoadTangentHandleRequested? OnMoveRoadTangentHandleRequested;
    public static event DeleteRoadVertexRequested? OnDeleteRoadVertexRequested;
    public static event DeleteRoadRequested? OnDeleteRoadRequested;
    public static event RequestInstantiateRoadVertexRequested? OnRequestInstantiateRoadVertexRequested;
    public static event RequestInstantiateRoadRequested? OnRequestInstantiateRoadRequested;
    public static event SetRoadIsLoop? OnSetRoadIsLoop;
    public static event SetRoadIsLoopRequested? OnSetRoadIsLoopRequested;
    public static event SetRoadMaterial? OnSetRoadMaterial;
    public static event SetRoadMaterialRequested? OnSetRoadMaterialRequested;
    public static event SetRoadVertexIgnoreTerrain? OnSetRoadVertexIgnoreTerrain;
    public static event SetRoadVertexIgnoreTerrainRequested? OnSetRoadVertexIgnoreTerrainRequested;
    public static event SetRoadVertexVerticalOffset? OnSetRoadVertexVerticalOffset;
    public static event SetRoadVertexVerticalOffsetRequested? OnSetRoadVertexVerticalOffsetRequested;
    public static event SetRoadVertexTangentHandleMode? OnSetRoadVertexTangentHandleMode;
    public static event SetRoadVertexTangentHandleModeRequested? OnSetRoadVertexTangentHandleModeRequested;
    public static event SetRoadMaterialWidth? OnSetRoadMaterialWidth;
    public static event SetRoadMaterialWidthRequested? OnSetRoadMaterialWidthRequested;
    public static event SetRoadMaterialHeight? OnSetRoadMaterialHeight;
    public static event SetRoadMaterialHeightRequested? OnSetRoadMaterialHeightRequested;
    public static event SetRoadMaterialDepth? OnSetRoadMaterialDepth;
    public static event SetRoadMaterialDepthRequested? OnSetRoadMaterialDepthRequested;
    public static event SetRoadMaterialVerticalOffset? OnSetRoadMaterialVerticalOffset;
    public static event SetRoadMaterialVerticalOffsetRequested? OnSetRoadMaterialVerticalOffsetRequested;
    public static event SetRoadMaterialIsConcrete? OnSetRoadMaterialIsConcrete;
    public static event SetRoadMaterialIsConcreteRequested? OnSetRoadMaterialIsConcreteRequested;
    public static event MoveNavigation? OnMoveNavigation;
    public static event MoveNavigationRequested? OnMoveNavigationRequested;
    public static event DeleteNavigation? OnDeleteNavigation;
    public static event DeleteNavigationRequested? OnDeleteNavigationRequested;
    public static event SetNavigationSize? OnSetNavigationSize;
    public static event SetNavigationSizeRequested? OnSetNavigationSizeRequested;
    public static event SetNavigationDifficulty? OnSetNavigationDifficulty;
    public static event SetNavigationDifficultyRequested? OnSetNavigationDifficultyRequested;
    public static event SetNavigationMaximumZombies? OnSetNavigationMaximumZombies;
    public static event SetNavigationMaximumZombiesRequested? OnSetNavigationMaximumZombiesRequested;
    public static event SetNavigationMaximumBossZombies? OnSetNavigationMaximumBossZombies;
    public static event SetNavigationMaximumBossZombiesRequested? OnSetNavigationMaximumBossZombiesRequested;
    public static event SetNavigationShouldSpawnZombies? OnSetNavigationShouldSpawnZombies;
    public static event SetNavigationShouldSpawnZombiesRequested? OnSetNavigationShouldSpawnZombiesRequested;
    public static event SetNavigationInfiniteAgroDistance? OnSetNavigationInfiniteAgroDistance;
    public static event SetNavigationInfiniteAgroDistanceRequested? OnSetNavigationInfiniteAgroDistanceRequested;
    public static event RequestInstantiateNavigationRequested? OnRequestInstantiateNavigationRequested;
    public static event SetSpawnpointType? OnSetSpawnpointType;
    public static event SetSpawnpointTypeRequested? OnSetSpawnpointTypeRequested;
    public static event SetSpawnpointsType? OnSetSpawnpointsType;
    public static event SetSpawnTableColor? OnSetSpawnTableColor;
    public static event SetSpawnTableColorRequested? OnSetSpawnTableColorRequested;
    public static event SetSpawnTableSpawnAsset? OnSetSpawnTableSpawnAsset;
    public static event SetSpawnTableSpawnAssetRequested? OnSetSpawnTableSpawnAssetRequested;
    public static event SetSpawnTableName? OnSetSpawnTableName;
    public static event SetSpawnTableNameRequested? OnSetSpawnTableNameRequested;
    public static event SetSpawnTableTierChance? OnSetSpawnTableTierChance;
    public static event SetSpawnTableTierChanceRequested? OnSetSpawnTableTierChanceRequested;
    public static event SetSpawnTableTierName? OnSetSpawnTableTierName;
    public static event SetSpawnTableTierNameRequested? OnSetSpawnTableTierNameRequested;
    public static event DeleteSpawnTable? OnDeleteSpawnTable;
    public static event DeleteSpawnTableRequested? OnDeleteSpawnTableRequested;
    public static event DeleteSpawnTableTier? OnDeleteSpawnTableTier;
    public static event DeleteSpawnTableTierRequested? OnDeleteSpawnTableTierRequested;
    public static event SetSpawnTableTierAsset? OnSetSpawnTableTierAsset;
    public static event SetSpawnTableTierAssetRequested? OnSetSpawnTableTierAssetRequested;
    public static event SetSpawnTableTierChances? OnSetSpawnTableTierChances;
    public static event SetSpawnTableTierChancesRequested? OnSetSpawnTableTierChancesRequested;
    public static event DeleteSpawnTableTierAsset? OnDeleteSpawnTableTierAsset;
    public static event DeleteSpawnTableTierAssetRequested? OnDeleteSpawnTableTierAssetRequested;
    public static event SetZombieSpawnTableDifficultyAsset? OnSetZombieSpawnTableDifficultyAsset;
    public static event SetZombieSpawnTableDifficultyAssetRequested? OnSetZombieSpawnTableDifficultyAssetRequested;
    public static event SetZombieSpawnTableIsMega? OnSetZombieSpawnTableIsMega;
    public static event SetZombieSpawnTableIsMegaRequested? OnSetZombieSpawnTableIsMegaRequested;
    public static event SetZombieSpawnTableHealth? OnSetZombieSpawnTableHealth;
    public static event SetZombieSpawnTableHealthRequested? OnSetZombieSpawnTableHealthRequested;
    public static event SetZombieSpawnTableDamage? OnSetZombieSpawnTableDamage;
    public static event SetZombieSpawnTableDamageRequested? OnSetZombieSpawnTableDamageRequested;
    public static event SetZombieSpawnTableLootIndex? OnSetZombieSpawnTableLootIndex;
    public static event SetZombieSpawnTableLootIndexRequested? OnSetZombieSpawnTableLootIndexRequested;
    public static event SetZombieSpawnTableXP? OnSetZombieSpawnTableXP;
    public static event SetZombieSpawnTableXPRequested? OnSetZombieSpawnTableXPRequested;
    public static event SetZombieSpawnTableRegen? OnSetZombieSpawnTableRegen;
    public static event SetZombieSpawnTableRegenRequested? OnSetZombieSpawnTableRegenRequested;
    public static event RequestInstantiateSpawnpointRequested? OnRequestInstantiateSpawnpointRequested;
    public static event RequestInstantiateSpawnTableRequested? OnRequestInstantiateSpawnTableRequested;
    public static event RequestInstantiateSpawnTierRequested? OnRequestInstantiateSpawnTierRequested;
    public static event RequestInstantiateSpawnTierAssetRequested? OnRequestInstantiateSpawnTierAssetRequested;
    public static event SetPlayerSpawnpointIsAlternate? OnSetPlayerSpawnpointIsAlternate;
    public static event SetPlayerSpawnpointIsAlternateRequested? OnSetPlayerSpawnpointIsAlternateRequested;
    public static event SetPlayerSpawnpointsIsAlternate? OnSetPlayerSpawnpointsIsAlternate;
    public static event DeleteSpawn? OnDeleteSpawn;
    public static event DeleteSpawnRequested? OnDeleteSpawnRequested;
    public static event DeleteSpawns? OnDeleteSpawns;

    public static bool ListeningOnEditHeightmapPermissionDenied => !EventOnEditHeightmapPermissionDenied.IsEmpty;
    public static bool ListeningOnEditSplatmapPermissionDenied => !EventOnEditSplatmapPermissionDenied.IsEmpty;
    public static bool ListeningOnEditHolesPermissionDenied => !EventOnEditHolesPermissionDenied.IsEmpty;
    
    public static bool ListeningOnEditHeightmapRequested => !EventOnEditHeightmapRequested.IsEmpty;
    public static bool ListeningOnEditSplatmapRequested => !EventOnEditSplatmapRequested.IsEmpty;
    public static bool ListeningOnEditHolesRequested => !EventOnEditHolesRequested.IsEmpty;

    public static bool ListeningOnTryInstantiateHierarchyObject => !EventOnTryInstantiateHierarchyObject.IsEmpty;
    public static bool ListeningOnRequestInstantiateHierarchyObject => !EventOnRequestInstantiateHierarchyObject.IsEmpty;

    public static bool ListeningOnPaintRamp => OnPaintRamp != null;
    public static bool ListeningOnAdjustHeightmap => OnAdjustHeightmap != null;
    public static bool ListeningOnFlattenHeightmap => OnFlattenHeightmap != null;
    public static bool ListeningOnSmoothHeightmap => OnSmoothHeightmap != null;
    public static bool ListeningOnPaintSplatmap => OnPaintSplatmap != null;
    public static bool ListeningOnAutoPaintSplatmap => OnAutoPaintSplatmap != null;
    public static bool ListeningOnSmoothSplatmap => OnSmoothSplatmap != null;
    public static bool ListeningOnPaintHoles => OnPaintHoles != null;
    public static bool ListeningOnAddTile => OnAddTile != null;
    public static bool ListeningOnDeleteTile => OnDeleteTile != null;
    public static bool ListeningOnAddFoliage => OnAddFoliage != null;
    public static bool ListeningOnRemoveFoliage => OnRemoveFoliage != null;
    public static bool ListeningOnRemoveResourceSpawnpointFoliage => OnRemoveResourceSpawnpointFoliage != null;
    public static bool ListeningOnRemoveLevelObjectFoliage => OnRemoveLevelObjectFoliage != null;
    public static bool ListeningOnUpdateTileSplatmapLayers => OnUpdateTileSplatmapLayers != null;
    public static bool ListeningOnMoveHierarchyObjectFinal => OnMoveHierarchyObjectFinal != null;
    public static bool ListeningOnMoveHierarchyObjectsFinal => OnMoveHierarchyObjectsFinal != null;
    public static bool ListeningOnDeleteHierarchyObject => OnDeleteHierarchyObject != null;
    public static bool ListeningOnDeleteHierarchyObjects => OnDeleteHierarchyObjects != null;
    public static bool ListeningOnDeleteLevelObject => OnDeleteLevelObject != null;
    public static bool ListeningOnDeleteLevelObjects => OnDeleteLevelObjects != null;
    public static bool ListeningOnMoveLevelObjectFinal => OnMoveLevelObjectFinal != null;
    public static bool ListeningOnMoveLevelObjectsFinal => OnMoveLevelObjectsFinal != null;
    public static bool ListeningOnUpdateObjectsCustomMaterialPaletteOverride => OnUpdateObjectsCustomMaterialPaletteOverride != null;
    public static bool ListeningOnUpdateObjectsMaterialIndexOverride => OnUpdateObjectsMaterialIndexOverride != null;
    public static bool ListeningOnMoveRoadVertex => OnMoveRoadVertex != null;
    public static bool ListeningOnMoveRoadTangentHandle => OnMoveRoadTangentHandle != null;
    public static bool ListeningOnDeleteRoadVertex => OnDeleteRoadVertex != null;
    public static bool ListeningOnDeleteRoad => OnDeleteRoad != null;
    public static bool ListeningOnMoveRoadVertexRequested => OnMoveRoadVertexRequested != null;
    public static bool ListeningOnMoveRoadTangentHandleRequested => OnMoveRoadTangentHandleRequested != null;
    public static bool ListeningOnDeleteRoadVertexRequested => OnDeleteRoadVertexRequested != null;
    public static bool ListeningOnDeleteRoadRequested => OnDeleteRoadRequested != null;
    public static bool ListeningOnRequestInstantiateRoadRequested => OnRequestInstantiateRoadRequested != null;
    public static bool ListeningOnRequestInstantiateRoadVertexRequested => OnRequestInstantiateRoadVertexRequested != null;
    public static bool ListeningOnSetRoadIsLoop => OnSetRoadIsLoop != null;
    public static bool ListeningOnSetRoadIsLoopRequested => OnSetRoadIsLoopRequested != null;
    public static bool ListeningOnSetRoadMaterial => OnSetRoadMaterial != null;
    public static bool ListeningOnSetRoadMaterialRequested => OnSetRoadMaterialRequested != null;
    public static bool ListeningOnSetRoadVertexIgnoreTerrain => OnSetRoadVertexIgnoreTerrain != null;
    public static bool ListeningOnSetRoadVertexIgnoreTerrainRequested => OnSetRoadVertexIgnoreTerrainRequested != null;
    public static bool ListeningOnSetRoadVertexVerticalOffset => OnSetRoadVertexVerticalOffset != null;
    public static bool ListeningOnSetRoadVertexVerticalOffsetRequested => OnSetRoadVertexVerticalOffsetRequested != null;
    public static bool ListeningOnSetRoadVertexTangentHandleMode => OnSetRoadVertexTangentHandleMode != null;
    public static bool ListeningOnSetRoadVertexTangentHandleModeRequested => OnSetRoadVertexTangentHandleModeRequested != null;
    public static bool ListeningOnSetRoadMaterialWidth => OnSetRoadMaterialWidth != null;
    public static bool ListeningOnSetRoadMaterialWidthRequested => OnSetRoadMaterialWidthRequested != null;
    public static bool ListeningOnSetRoadMaterialHeight => OnSetRoadMaterialHeight != null;
    public static bool ListeningOnSetRoadMaterialHeightRequested => OnSetRoadMaterialHeightRequested != null;
    public static bool ListeningOnSetRoadMaterialDepth => OnSetRoadMaterialDepth != null;
    public static bool ListeningOnSetRoadMaterialDepthRequested => OnSetRoadMaterialDepthRequested != null;
    public static bool ListeningOnSetRoadMaterialVerticalOffset => OnSetRoadMaterialVerticalOffset != null;
    public static bool ListeningOnSetRoadMaterialVerticalOffsetRequested => OnSetRoadMaterialVerticalOffsetRequested != null;
    public static bool ListeningOnSetRoadMaterialIsConcrete => OnSetRoadMaterialIsConcrete != null;
    public static bool ListeningOnSetRoadMaterialIsConcreteRequested => OnSetRoadMaterialIsConcreteRequested != null;
    public static bool ListeningOnDeleteNavigation => OnDeleteNavigation != null;
    public static bool ListeningOnDeleteNavigationRequested => OnDeleteNavigationRequested != null;
    public static bool ListeningOnMoveNavigation => OnMoveNavigation != null;
    public static bool ListeningOnMoveNavigationRequested => OnMoveNavigationRequested != null;
    public static bool ListeningOnSetNavigationSize => OnSetNavigationSize != null;
    public static bool ListeningOnSetNavigationSizeRequested => OnSetNavigationSizeRequested != null;
    public static bool ListeningOnSetNavigationDifficulty => OnSetNavigationDifficulty != null;
    public static bool ListeningOnSetNavigationDifficultyRequested => OnSetNavigationDifficultyRequested != null;
    public static bool ListeningOnSetNavigationMaximumZombies => OnSetNavigationMaximumZombies != null;
    public static bool ListeningOnSetNavigationMaximumZombiesRequested => OnSetNavigationMaximumZombiesRequested != null;
    public static bool ListeningOnSetNavigationMaximumBossZombies => OnSetNavigationMaximumBossZombies != null;
    public static bool ListeningOnSetNavigationMaximumBossZombiesRequested => OnSetNavigationMaximumBossZombiesRequested != null;
    public static bool ListeningOnSetNavigationShouldSpawnZombies => OnSetNavigationShouldSpawnZombies != null;
    public static bool ListeningOnSetNavigationShouldSpawnZombiesRequested => OnSetNavigationShouldSpawnZombiesRequested != null;
    public static bool ListeningOnSetNavigationInfiniteAgroDistance => OnSetNavigationInfiniteAgroDistance != null;
    public static bool ListeningOnSetNavigationInfiniteAgroDistanceRequested => OnSetNavigationInfiniteAgroDistanceRequested != null;
    public static bool ListeningOnRequestInstantiateNavigationRequested => OnRequestInstantiateNavigationRequested != null;
    public static bool ListeningOnSetSpawnpointType => OnSetSpawnpointType != null;
    public static bool ListeningOnSetSpawnpointTypeRequested => OnSetSpawnpointTypeRequested != null;
    public static bool ListeningOnSetSpawnpointsType => OnSetSpawnpointsType != null;
    public static bool ListeningOnSetSpawnTableColor => OnSetSpawnTableColor != null;
    public static bool ListeningOnSetSpawnTableColorRequested => OnSetSpawnTableColorRequested != null;
    public static bool ListeningOnSetSpawnTableSpawnAsset => OnSetSpawnTableSpawnAsset != null;
    public static bool ListeningOnSetSpawnTableSpawnAssetRequested => OnSetSpawnTableSpawnAssetRequested != null;
    public static bool ListeningOnSetSpawnTableName => OnSetSpawnTableName != null;
    public static bool ListeningOnSetSpawnTableNameRequested => OnSetSpawnTableNameRequested != null;
    public static bool ListeningOnSetSpawnTableTierChance => OnSetSpawnTableTierChance != null;
    public static bool ListeningOnSetSpawnTableTierChanceRequested => OnSetSpawnTableTierChanceRequested != null;
    public static bool ListeningOnSetSpawnTableTierName => OnSetSpawnTableTierName != null;
    public static bool ListeningOnSetSpawnTableTierNameRequested => OnSetSpawnTableTierNameRequested != null;
    public static bool ListeningOnDeleteSpawnTable => OnDeleteSpawnTable != null;
    public static bool ListeningOnDeleteSpawnTableRequested => OnDeleteSpawnTableRequested != null;
    public static bool ListeningOnDeleteSpawnTableTier => OnDeleteSpawnTableTier != null;
    public static bool ListeningOnDeleteSpawnTableTierRequested => OnDeleteSpawnTableTierRequested != null;
    public static bool ListeningOnSetSpawnTableTierAsset => OnSetSpawnTableTierAsset != null;
    public static bool ListeningOnSetSpawnTableTierAssetRequested => OnSetSpawnTableTierAssetRequested != null;
    public static bool ListeningOnSetSpawnTableTierChances => OnSetSpawnTableTierChances != null;
    public static bool ListeningOnSetSpawnTableTierChancesRequested => OnSetSpawnTableTierChancesRequested != null;
    public static bool ListeningOnDeleteSpawnTableTierAsset => OnDeleteSpawnTableTierAsset != null;
    public static bool ListeningOnDeleteSpawnTableTierAssetRequested => OnDeleteSpawnTableTierAssetRequested != null;
    public static bool ListeningOnSetZombieSpawnTableDifficultyAsset => OnSetZombieSpawnTableDifficultyAsset != null;
    public static bool ListeningOnSetZombieSpawnTableDifficultyAssetRequested => OnSetZombieSpawnTableDifficultyAssetRequested != null;
    public static bool ListeningOnSetZombieSpawnTableIsMega => OnSetZombieSpawnTableIsMega != null;
    public static bool ListeningOnSetZombieSpawnTableIsMegaRequested => OnSetZombieSpawnTableIsMegaRequested != null;
    public static bool ListeningOnSetZombieSpawnTableHealth => OnSetZombieSpawnTableHealth != null;
    public static bool ListeningOnSetZombieSpawnTableHealthRequested => OnSetZombieSpawnTableHealthRequested != null;
    public static bool ListeningOnSetZombieSpawnTableDamage => OnSetZombieSpawnTableDamage != null;
    public static bool ListeningOnSetZombieSpawnTableDamageRequested => OnSetZombieSpawnTableDamageRequested != null;
    public static bool ListeningOnSetZombieSpawnTableLootIndex => OnSetZombieSpawnTableLootIndex != null;
    public static bool ListeningOnSetZombieSpawnTableLootIndexRequested => OnSetZombieSpawnTableLootIndexRequested != null;
    public static bool ListeningOnSetZombieSpawnTableXP => OnSetZombieSpawnTableXP != null;
    public static bool ListeningOnSetZombieSpawnTableXPRequested => OnSetZombieSpawnTableXPRequested != null;
    public static bool ListeningOnSetZombieSpawnTableRegen => OnSetZombieSpawnTableRegen != null;
    public static bool ListeningOnSetZombieSpawnTableRegenRequested => OnSetZombieSpawnTableRegenRequested != null;
    public static bool ListeningOnRequestInstantiateSpawnpointRequested => OnRequestInstantiateSpawnpointRequested != null;
    public static bool ListeningOnRequestInstantiateSpawnTableRequested => OnRequestInstantiateSpawnTableRequested != null;
    public static bool ListeningOnRequestInstantiateSpawnTierRequested => OnRequestInstantiateSpawnTierRequested != null;
    public static bool ListeningOnRequestInstantiateSpawnTierAssetRequested => OnRequestInstantiateSpawnTierAssetRequested != null;
    public static bool ListeningOnSetPlayerSpawnpointIsAlternate => OnSetPlayerSpawnpointIsAlternate != null;
    public static bool ListeningOnSetPlayerSpawnpointIsAlternateRequested => OnSetPlayerSpawnpointIsAlternateRequested != null;
    public static bool ListeningOnSetPlayerSpawnpointsIsAlternate => OnSetPlayerSpawnpointsIsAlternate != null;
    public static bool ListeningOnDeleteSpawn => OnDeleteSpawn != null;
    public static bool ListeningOnDeleteSpawnRequested => OnDeleteSpawnRequested != null;
    public static bool ListeningOnDeleteSpawns => OnDeleteSpawns != null;

    internal static void InvokeOnPaintRamp(in PaintRampProperties properties) => OnPaintRamp?.Invoke(in properties);
    internal static void InvokeOnAdjustHeightmap(in AdjustHeightmapProperties properties) => OnAdjustHeightmap?.Invoke(in properties);
    internal static void InvokeOnFlattenHeightmap(in FlattenHeightmapProperties properties) => OnFlattenHeightmap?.Invoke(in properties);
    internal static void InvokeOnSmoothHeightmap(in SmoothHeightmapProperties properties) => OnSmoothHeightmap?.Invoke(in properties);
    internal static void InvokeOnPaintSplatmap(in PaintSplatmapProperties properties) => OnPaintSplatmap?.Invoke(in properties);
    internal static void InvokeOnAutoPaintSplatmap(in PaintSplatmapProperties properties) => OnAutoPaintSplatmap?.Invoke(in properties);
    internal static void InvokeOnSmoothSplatmap(in SmoothSplatmapProperties properties) => OnSmoothSplatmap?.Invoke(in properties);
    internal static void InvokeOnPaintHoles(in PaintHolesProperties properties) => OnPaintHoles?.Invoke(in properties);
    internal static void InvokeOnAddTile(in UpdateLandscapeTileProperties properties) => OnAddTile?.Invoke(in properties);
    internal static void InvokeOnDeleteTile(in UpdateLandscapeTileProperties properties) => OnDeleteTile?.Invoke(in properties);
    internal static void InvokeOnAddFoliage(in AddFoliageProperties properties) => OnAddFoliage?.Invoke(in properties);
    internal static void InvokeOnRemoveFoliage(in RemoveFoliageProperties properties) => OnRemoveFoliage?.Invoke(in properties);
    internal static void InvokeOnRemoveResourceSpawnpointFoliage(in RemoveResourceSpawnpointFoliageProperties properties) => OnRemoveResourceSpawnpointFoliage?.Invoke(in properties);
    internal static void InvokeOnRemoveLevelObjectFoliage(in RemoveLevelObjectFoliageProperties properties) => OnRemoveLevelObjectFoliage?.Invoke(in properties);
    internal static void InvokeOnUpdateTileSplatmapLayers(in UpdateLandscapeTileProperties properties) => OnUpdateTileSplatmapLayers?.Invoke(in properties);
    internal static void InvokeOnMoveHierarchyObjectFinal(in MoveHierarchyObjectFinalProperties properties) => OnMoveHierarchyObjectFinal?.Invoke(in properties);
    internal static void InvokeOnMoveHierarchyObjectsFinal(in MoveHierarchyObjectsFinalProperties properties) => OnMoveHierarchyObjectsFinal?.Invoke(in properties);
    internal static void InvokeOnDeleteHierarchyObject(in DeleteHierarchyObjectProperties properties) => OnDeleteHierarchyObject?.Invoke(in properties);
    internal static void InvokeOnDeleteHierarchyObjects(in DeleteHierarchyObjectsProperties properties) => OnDeleteHierarchyObjects?.Invoke(in properties);
    internal static void InvokeOnDeleteLevelObject(in DeleteLevelObjectProperties properties) => OnDeleteLevelObject?.Invoke(in properties);
    internal static void InvokeOnDeleteLevelObjects(in DeleteLevelObjectsProperties properties) => OnDeleteLevelObjects?.Invoke(in properties);
    internal static void InvokeOnMoveLevelObjectFinal(in MoveLevelObjectFinalProperties properties) => OnMoveLevelObjectFinal?.Invoke(in properties);
    internal static void InvokeOnMoveLevelObjectsFinal(in MoveLevelObjectsFinalProperties properties) => OnMoveLevelObjectsFinal?.Invoke(in properties);
    internal static void InvokeOnUpdateObjectsCustomMaterialPaletteOverride(in UpdateObjectsCustomMaterialPaletteOverrideProperties properties) => OnUpdateObjectsCustomMaterialPaletteOverride?.Invoke(in properties);
    internal static void InvokeOnUpdateObjectsMaterialIndexOverride(in UpdateObjectsMaterialIndexOverrideProperties properties) => OnUpdateObjectsMaterialIndexOverride?.Invoke(in properties);
    internal static void InvokeOnMoveRoadVertex(in MoveRoadVertexProperties properties) => OnMoveRoadVertex?.Invoke(in properties);
    internal static void InvokeOnMoveRoadTangentHandle(in MoveRoadTangentHandleProperties properties) => OnMoveRoadTangentHandle?.Invoke(in properties);
    internal static void InvokeOnDeleteRoadVertex(in DeleteRoadVertexProperties properties) => OnDeleteRoadVertex?.Invoke(in properties);
    internal static void InvokeOnDeleteRoad(in DeleteRoadProperties properties) => OnDeleteRoad?.Invoke(in properties);
    internal static void InvokeOnMoveRoadVertexRequested(in MoveRoadVertexProperties properties, ref bool shouldAllow) => OnMoveRoadVertexRequested?.Invoke(in properties, ref shouldAllow);
    internal static void InvokeOnMoveRoadTangentHandleRequested(in MoveRoadTangentHandleProperties properties, ref bool shouldAllow) => OnMoveRoadTangentHandleRequested?.Invoke(in properties, ref shouldAllow);
    internal static void InvokeOnDeleteRoadVertexRequested(in DeleteRoadVertexProperties properties, ref bool shouldAllow) => OnDeleteRoadVertexRequested?.Invoke(in properties, ref shouldAllow);
    internal static void InvokeOnDeleteRoadRequested(in DeleteRoadProperties properties, ref bool shouldAllow) => OnDeleteRoadRequested?.Invoke(in properties, ref shouldAllow);
    internal static void InvokeOnRequestInstantiateRoadRequested(in RequestInstantiateRoadProperties properties, ref bool shouldAllow) => OnRequestInstantiateRoadRequested?.Invoke(in properties, ref shouldAllow);
    internal static void InvokeOnRequestInstantiateRoadVertexRequested(in RequestInstantiateRoadVertexProperties properties, ref bool shouldAllow) => OnRequestInstantiateRoadVertexRequested?.Invoke(in properties, ref shouldAllow);
    internal static void InvokeOnSetRoadIsLoop(in SetRoadIsLoopProperties properties) => OnSetRoadIsLoop?.Invoke(in properties);
    internal static void InvokeOnSetRoadIsLoopRequested(in SetRoadIsLoopProperties properties, ref bool shouldAllow) => OnSetRoadIsLoopRequested?.Invoke(in properties, ref shouldAllow);
    internal static void InvokeOnSetRoadMaterial(in SetRoadMaterialProperties properties) => OnSetRoadMaterial?.Invoke(in properties);
    internal static void InvokeOnSetRoadMaterialRequested(in SetRoadMaterialProperties properties, ref bool shouldAllow) => OnSetRoadMaterialRequested?.Invoke(in properties, ref shouldAllow);
    internal static void InvokeOnSetRoadVertexIgnoreTerrain(in SetRoadVertexIgnoreTerrainProperties properties) => OnSetRoadVertexIgnoreTerrain?.Invoke(in properties);
    internal static void InvokeOnSetRoadVertexIgnoreTerrainRequested(in SetRoadVertexIgnoreTerrainProperties properties, ref bool shouldAllow) => OnSetRoadVertexIgnoreTerrainRequested?.Invoke(in properties, ref shouldAllow);
    internal static void InvokeOnSetRoadVertexVerticalOffset(in SetRoadVertexVerticalOffsetProperties properties) => OnSetRoadVertexVerticalOffset?.Invoke(in properties);
    internal static void InvokeOnSetRoadVertexVerticalOffsetRequested(in SetRoadVertexVerticalOffsetProperties properties, ref bool shouldAllow) => OnSetRoadVertexVerticalOffsetRequested?.Invoke(in properties, ref shouldAllow);
    internal static void InvokeOnSetRoadVertexTangentHandleMode(in SetRoadVertexTangentHandleModeProperties properties) => OnSetRoadVertexTangentHandleMode?.Invoke(in properties);
    internal static void InvokeOnSetRoadVertexTangentHandleModeRequested(in SetRoadVertexTangentHandleModeProperties properties, ref bool shouldAllow) => OnSetRoadVertexTangentHandleModeRequested?.Invoke(in properties, ref shouldAllow);
    internal static void InvokeOnSetRoadMaterialWidth(in SetRoadMaterialWidthProperties properties) => OnSetRoadMaterialWidth?.Invoke(in properties);
    internal static void InvokeOnSetRoadMaterialWidthRequested(in SetRoadMaterialWidthProperties properties, ref bool shouldAllow) => OnSetRoadMaterialWidthRequested?.Invoke(in properties, ref shouldAllow);
    internal static void InvokeOnSetRoadMaterialHeight(in SetRoadMaterialHeightProperties properties) => OnSetRoadMaterialHeight?.Invoke(in properties);
    internal static void InvokeOnSetRoadMaterialHeightRequested(in SetRoadMaterialHeightProperties properties, ref bool shouldAllow) => OnSetRoadMaterialHeightRequested?.Invoke(in properties, ref shouldAllow);
    internal static void InvokeOnSetRoadMaterialDepth(in SetRoadMaterialDepthProperties properties) => OnSetRoadMaterialDepth?.Invoke(in properties);
    internal static void InvokeOnSetRoadMaterialDepthRequested(in SetRoadMaterialDepthProperties properties, ref bool shouldAllow) => OnSetRoadMaterialDepthRequested?.Invoke(in properties, ref shouldAllow);
    internal static void InvokeOnSetRoadMaterialVerticalOffset(in SetRoadMaterialVerticalOffsetProperties properties) => OnSetRoadMaterialVerticalOffset?.Invoke(in properties);
    internal static void InvokeOnSetRoadMaterialVerticalOffsetRequested(in SetRoadMaterialVerticalOffsetProperties properties, ref bool shouldAllow) => OnSetRoadMaterialVerticalOffsetRequested?.Invoke(in properties, ref shouldAllow);
    internal static void InvokeOnSetRoadMaterialIsConcrete(in SetRoadMaterialIsConcreteProperties properties) => OnSetRoadMaterialIsConcrete?.Invoke(in properties);
    internal static void InvokeOnSetRoadMaterialIsConcreteRequested(in SetRoadMaterialIsConcreteProperties properties, ref bool shouldAllow) => OnSetRoadMaterialIsConcreteRequested?.Invoke(in properties, ref shouldAllow);
    internal static void InvokeOnDeleteNavigation(in DeleteNavigationProperties properties) => OnDeleteNavigation?.Invoke(in properties);
    internal static void InvokeOnDeleteNavigationRequested(in DeleteNavigationProperties properties, ref bool shouldAllow) => OnDeleteNavigationRequested?.Invoke(in properties, ref shouldAllow);
    internal static void InvokeOnMoveNavigation(in MoveNavigationProperties properties) => OnMoveNavigation?.Invoke(in properties);
    internal static void InvokeOnMoveNavigationRequested(in MoveNavigationProperties properties, ref bool shouldAllow) => OnMoveNavigationRequested?.Invoke(in properties, ref shouldAllow);
    internal static void InvokeOnSetNavigationSize(in SetNavigationSizeProperties properties) => OnSetNavigationSize?.Invoke(in properties);
    internal static void InvokeOnSetNavigationSizeRequested(in SetNavigationSizeProperties properties, ref bool shouldAllow) => OnSetNavigationSizeRequested?.Invoke(in properties, ref shouldAllow);
    internal static void InvokeOnSetNavigationDifficulty(in SetNavigationDifficultyProperties properties) => OnSetNavigationDifficulty?.Invoke(in properties);
    internal static void InvokeOnSetNavigationDifficultyRequested(in SetNavigationDifficultyProperties properties, ref bool shouldAllow) => OnSetNavigationDifficultyRequested?.Invoke(in properties, ref shouldAllow);
    internal static void InvokeOnSetNavigationMaximumZombies(in SetNavigationMaximumZombiesProperties properties) => OnSetNavigationMaximumZombies?.Invoke(in properties);
    internal static void InvokeOnSetNavigationMaximumZombiesRequested(in SetNavigationMaximumZombiesProperties properties, ref bool shouldAllow) => OnSetNavigationMaximumZombiesRequested?.Invoke(in properties, ref shouldAllow);
    internal static void InvokeOnSetNavigationMaximumBossZombies(in SetNavigationMaximumBossZombiesProperties properties) => OnSetNavigationMaximumBossZombies?.Invoke(in properties);
    internal static void InvokeOnSetNavigationMaximumBossZombiesRequested(in SetNavigationMaximumBossZombiesProperties properties, ref bool shouldAllow) => OnSetNavigationMaximumBossZombiesRequested?.Invoke(in properties, ref shouldAllow);
    internal static void InvokeOnSetNavigationShouldSpawnZombies(in SetNavigationShouldSpawnZombiesProperties properties) => OnSetNavigationShouldSpawnZombies?.Invoke(in properties);
    internal static void InvokeOnSetNavigationShouldSpawnZombiesRequested(in SetNavigationShouldSpawnZombiesProperties properties, ref bool shouldAllow) => OnSetNavigationShouldSpawnZombiesRequested?.Invoke(in properties, ref shouldAllow);
    internal static void InvokeOnSetNavigationInfiniteAgroDistance(in SetNavigationInfiniteAgroDistanceProperties properties) => OnSetNavigationInfiniteAgroDistance?.Invoke(in properties);
    internal static void InvokeOnSetNavigationInfiniteAgroDistanceRequested(in SetNavigationInfiniteAgroDistanceProperties properties, ref bool shouldAllow) => OnSetNavigationInfiniteAgroDistanceRequested?.Invoke(in properties, ref shouldAllow);
    internal static void InvokeOnRequestInstantiateNavigationRequested(in RequestInstantiateNavigationProperties properties, ref bool shouldAllow) => OnRequestInstantiateNavigationRequested?.Invoke(in properties, ref shouldAllow);
    internal static void InvokeOnSetSpawnpointType(in SetSpawnpointTypeProperties properties) => OnSetSpawnpointType?.Invoke(in properties);
    internal static void InvokeOnSetSpawnpointTypeRequested(in SetSpawnpointTypeProperties properties, ref bool shouldAllow) => OnSetSpawnpointTypeRequested?.Invoke(in properties, ref shouldAllow);
    internal static void InvokeOnSetSpawnpointsType(in SetSpawnpointsTypeProperties properties) => OnSetSpawnpointsType?.Invoke(in properties);
    internal static void InvokeOnSetSpawnTableColor(in SetSpawnTableColorProperties properties) => OnSetSpawnTableColor?.Invoke(in properties);
    internal static void InvokeOnSetSpawnTableColorRequested(in SetSpawnTableColorProperties properties, ref bool shouldAllow) => OnSetSpawnTableColorRequested?.Invoke(in properties, ref shouldAllow);
    internal static void InvokeOnSetSpawnTableSpawnAsset(in SetSpawnTableSpawnAssetProperties properties) => OnSetSpawnTableSpawnAsset?.Invoke(in properties);
    internal static void InvokeOnSetSpawnTableSpawnAssetRequested(in SetSpawnTableSpawnAssetProperties properties, ref bool shouldAllow) => OnSetSpawnTableSpawnAssetRequested?.Invoke(in properties, ref shouldAllow);
    internal static void InvokeOnSetSpawnTableName(in SetSpawnTableNameProperties properties) => OnSetSpawnTableName?.Invoke(in properties);
    internal static void InvokeOnSetSpawnTableNameRequested(in SetSpawnTableNameProperties properties, ref bool shouldAllow) => OnSetSpawnTableNameRequested?.Invoke(in properties, ref shouldAllow);
    internal static void InvokeOnSetSpawnTableTierChance(in SetSpawnTableTierChanceProperties properties) => OnSetSpawnTableTierChance?.Invoke(in properties);
    internal static void InvokeOnSetSpawnTableTierChanceRequested(in SetSpawnTableTierChanceProperties properties, ref bool shouldAllow) => OnSetSpawnTableTierChanceRequested?.Invoke(in properties, ref shouldAllow);
    internal static void InvokeOnSetSpawnTableTierName(in SetSpawnTableTierNameProperties properties) => OnSetSpawnTableTierName?.Invoke(in properties);
    internal static void InvokeOnSetSpawnTableTierNameRequested(in SetSpawnTableTierNameProperties properties, ref bool shouldAllow) => OnSetSpawnTableTierNameRequested?.Invoke(in properties, ref shouldAllow);
    internal static void InvokeOnDeleteSpawnTable(in DeleteSpawnTableProperties properties) => OnDeleteSpawnTable?.Invoke(in properties);
    internal static void InvokeOnDeleteSpawnTableRequested(in DeleteSpawnTableProperties properties, ref bool shouldAllow) => OnDeleteSpawnTableRequested?.Invoke(in properties, ref shouldAllow);
    internal static void InvokeOnDeleteSpawnTableTier(in DeleteSpawnTableTierProperties properties) => OnDeleteSpawnTableTier?.Invoke(in properties);
    internal static void InvokeOnDeleteSpawnTableTierRequested(in DeleteSpawnTableTierProperties properties, ref bool shouldAllow) => OnDeleteSpawnTableTierRequested?.Invoke(in properties, ref shouldAllow);
    internal static void InvokeOnSetSpawnTableTierAsset(in SetSpawnTableTierAssetProperties properties) => OnSetSpawnTableTierAsset?.Invoke(in properties);
    internal static void InvokeOnSetSpawnTableTierAssetRequested(in SetSpawnTableTierAssetProperties properties, ref bool shouldAllow) => OnSetSpawnTableTierAssetRequested?.Invoke(in properties, ref shouldAllow);
    internal static void InvokeOnSetSpawnTableTierChances(in SetSpawnTableTierChancesProperties properties) => OnSetSpawnTableTierChances?.Invoke(in properties);
    internal static void InvokeOnSetSpawnTableTierChancesRequested(in SetSpawnTableTierChancesProperties properties, ref bool shouldAllow) => OnSetSpawnTableTierChancesRequested?.Invoke(in properties, ref shouldAllow);
    internal static void InvokeOnDeleteSpawnTableTierAsset(in DeleteSpawnTableTierAssetProperties properties) => OnDeleteSpawnTableTierAsset?.Invoke(in properties);
    internal static void InvokeOnDeleteSpawnTableTierAssetRequested(in DeleteSpawnTableTierAssetProperties properties, ref bool shouldAllow) => OnDeleteSpawnTableTierAssetRequested?.Invoke(in properties, ref shouldAllow);
    internal static void InvokeOnSetZombieSpawnTableDifficultyAsset(in SetZombieSpawnTableDifficultyAssetProperties properties) => OnSetZombieSpawnTableDifficultyAsset?.Invoke(in properties);
    internal static void InvokeOnSetZombieSpawnTableDifficultyAssetRequested(in SetZombieSpawnTableDifficultyAssetProperties properties, ref bool shouldAllow) => OnSetZombieSpawnTableDifficultyAssetRequested?.Invoke(in properties, ref shouldAllow);
    internal static void InvokeOnSetZombieSpawnTableIsMega(in SetZombieSpawnTableIsMegaProperties properties) => OnSetZombieSpawnTableIsMega?.Invoke(in properties);
    internal static void InvokeOnSetZombieSpawnTableIsMegaRequested(in SetZombieSpawnTableIsMegaProperties properties, ref bool shouldAllow) => OnSetZombieSpawnTableIsMegaRequested?.Invoke(in properties, ref shouldAllow);
    internal static void InvokeOnSetZombieSpawnTableHealth(in SetZombieSpawnTableHealthProperties properties) => OnSetZombieSpawnTableHealth?.Invoke(in properties);
    internal static void InvokeOnSetZombieSpawnTableHealthRequested(in SetZombieSpawnTableHealthProperties properties, ref bool shouldAllow) => OnSetZombieSpawnTableHealthRequested?.Invoke(in properties, ref shouldAllow);
    internal static void InvokeOnSetZombieSpawnTableDamage(in SetZombieSpawnTableDamageProperties properties) => OnSetZombieSpawnTableDamage?.Invoke(in properties);
    internal static void InvokeOnSetZombieSpawnTableDamageRequested(in SetZombieSpawnTableDamageProperties properties, ref bool shouldAllow) => OnSetZombieSpawnTableDamageRequested?.Invoke(in properties, ref shouldAllow);
    internal static void InvokeOnSetZombieSpawnTableLootIndex(in SetZombieSpawnTableLootIndexProperties properties) => OnSetZombieSpawnTableLootIndex?.Invoke(in properties);
    internal static void InvokeOnSetZombieSpawnTableLootIndexRequested(in SetZombieSpawnTableLootIndexProperties properties, ref bool shouldAllow) => OnSetZombieSpawnTableLootIndexRequested?.Invoke(in properties, ref shouldAllow);
    internal static void InvokeOnSetZombieSpawnTableXP(in SetZombieSpawnTableXPProperties properties) => OnSetZombieSpawnTableXP?.Invoke(in properties);
    internal static void InvokeOnSetZombieSpawnTableXPRequested(in SetZombieSpawnTableXPProperties properties, ref bool shouldAllow) => OnSetZombieSpawnTableXPRequested?.Invoke(in properties, ref shouldAllow);
    internal static void InvokeOnSetZombieSpawnTableRegen(in SetZombieSpawnTableRegenProperties properties) => OnSetZombieSpawnTableRegen?.Invoke(in properties);
    internal static void InvokeOnSetZombieSpawnTableRegenRequested(in SetZombieSpawnTableRegenProperties properties, ref bool shouldAllow) => OnSetZombieSpawnTableRegenRequested?.Invoke(in properties, ref shouldAllow);
    internal static void InvokeOnRequestInstantiateSpawnpointRequested(in RequestInstantiateSpawnpointProperties properties, ref bool shouldAllow) => OnRequestInstantiateSpawnpointRequested?.Invoke(in properties, ref shouldAllow);
    internal static void InvokeOnRequestInstantiateSpawnTableRequested(in RequestInstantiateSpawnTableProperties properties, ref bool shouldAllow) => OnRequestInstantiateSpawnTableRequested?.Invoke(in properties, ref shouldAllow);
    internal static void InvokeOnRequestInstantiateSpawnTierRequested(in RequestInstantiateSpawnTierProperties properties, ref bool shouldAllow) => OnRequestInstantiateSpawnTierRequested?.Invoke(in properties, ref shouldAllow);
    internal static void InvokeOnRequestInstantiateSpawnTierAssetRequested(in RequestInstantiateSpawnTierAssetProperties properties, ref bool shouldAllow) => OnRequestInstantiateSpawnTierAssetRequested?.Invoke(in properties, ref shouldAllow);
    internal static void InvokeOnSetPlayerSpawnpointIsAlternate(in SetPlayerSpawnpointIsAlternateProperties properties) => OnSetPlayerSpawnpointIsAlternate?.Invoke(in properties);
    internal static void InvokeOnSetPlayerSpawnpointIsAlternateRequested(in SetPlayerSpawnpointIsAlternateProperties properties, ref bool shouldAllow) => OnSetPlayerSpawnpointIsAlternateRequested?.Invoke(in properties, ref shouldAllow);
    internal static void InvokeOnSetPlayerSpawnpointsIsAlternate(in SetPlayerSpawnpointsIsAlternateProperties properties) => OnSetPlayerSpawnpointsIsAlternate?.Invoke(in properties);
    internal static void InvokeOnDeleteSpawn(in DeleteSpawnProperties properties) => OnDeleteSpawn?.Invoke(in properties);
    internal static void InvokeOnDeleteSpawnRequested(in DeleteSpawnProperties properties, ref bool shouldAllow) => OnDeleteSpawnRequested?.Invoke(in properties, ref shouldAllow);
    internal static void InvokeOnDeleteSpawns(in DeleteSpawnsProperties properties) => OnDeleteSpawns?.Invoke(in properties);
}

public delegate void TryInstantiateHierarchyObject(ref InstantiateHierarchyObjectProperties properties, ref bool shouldAllow);

public delegate void EditHeightmapRequest(TerrainEditor.EDevkitLandscapeToolHeightmapMode mode, ref bool allow);
public delegate void EditSplatmapRequest(TerrainEditor.EDevkitLandscapeToolSplatmapMode mode, ref bool allow);
public delegate void EditHolesRequest(bool isFilling, ref bool allow);
#endif