using DevkitServer.API;
using DevkitServer.API.Abstractions;
using DevkitServer.API.Devkit.Spawns;
using DevkitServer.API.Lighting;
using DevkitServer.Models;
using SDG.Framework.Devkit;
using SDG.Framework.Devkit.Tools;
using SDG.Framework.Foliage;
using SDG.Framework.Landscapes;

namespace DevkitServer.Multiplayer.Actions;

public delegate void PaintHoles(in PaintHolesProperties properties);
public readonly ref struct PaintHolesProperties(
    Bounds bounds,
    Vector3 position,
    float radius,
    bool isFilling,
    float deltaTime)
{
    public readonly Bounds Bounds = bounds;
    public readonly Vector3 Position = position;
    public readonly float Radius = radius;
    public readonly float DeltaTime = deltaTime;
    public readonly bool IsFilling = isFilling;
}

public delegate void UpdateLandscapeTile(in UpdateLandscapeTileProperties properties);
public readonly ref struct UpdateLandscapeTileProperties(LandscapeTile tile, float deltaTime)
{
    public readonly LandscapeTile Tile = tile;
    public readonly float DeltaTime = deltaTime;
}

public delegate void AddFoliage(in AddFoliageProperties properties);
public readonly ref struct AddFoliageProperties(
    FoliageInfoAsset asset,
    Vector3 position,
    Quaternion rotation,
    Vector3 scale,
    bool clearWhenBaked,
    float deltaTime,
    uint? objectInstanceId)
{
    public readonly FoliageInfoAsset Asset = asset;
    public readonly Vector3 Position = position;
    public readonly Quaternion Rotation = rotation;
    public readonly Vector3 Scale = scale;
    public readonly bool ClearWhenBaked = clearWhenBaked;
    public readonly float DeltaTime = deltaTime;
    public readonly uint? ObjectInstanceId = objectInstanceId;
}

public delegate void RemoveFoliage(in RemoveFoliageProperties properties);
public readonly ref struct RemoveFoliageProperties(
    Vector3 brushPosition,
    FoliageTile tile,
    FoliageInstanceList foliageInstances,
    float brushRadius,
    float brushFalloff,
    float deltaTime,
    bool allowRemovingBakedFoliage,
    int sampleCount)
{
    public readonly Vector3 BrushPosition = brushPosition;
    public readonly FoliageTile Tile = tile;
    public readonly FoliageInstanceList FoliageInstances = foliageInstances;
    public readonly float BrushRadius = brushRadius;
    public readonly float BrushFalloff = brushFalloff;
    public readonly float DeltaTime = deltaTime;
    public readonly bool AllowRemovingBakedFoliage = allowRemovingBakedFoliage;
    public readonly int SampleCount = sampleCount;
}

public delegate void RemoveResourceSpawnpointFoliage(in RemoveResourceSpawnpointFoliageProperties foliageProperties);
public readonly ref struct RemoveResourceSpawnpointFoliageProperties(ResourceSpawnpoint spawnpoint, float deltaTime)
{
    public readonly ResourceSpawnpoint Spawnpoint = spawnpoint;
    public readonly float DeltaTime = deltaTime;
}

public delegate void RemoveLevelObjectFoliage(in RemoveLevelObjectFoliageProperties foliageProperties);
public readonly ref struct RemoveLevelObjectFoliageProperties(Vector3 position, LevelObject levelObject, float deltaTime)
{
    public readonly Vector3 Position = position;
    public readonly LevelObject LevelObject = levelObject;
    public readonly float DeltaTime = deltaTime;
}

public delegate void PaintRamp(in PaintRampProperties properties);
public readonly ref struct PaintRampProperties(
    Bounds bounds,
    Vector3 start,
    Vector3 end,
    float radius,
    float falloff,
    float deltaTime)
{
    public readonly Bounds Bounds = bounds;
    public readonly Vector3 Start = start;
    public readonly Vector3 End = end;
    public readonly float Radius = radius;
    public readonly float Falloff = falloff;
    public readonly float DeltaTime = deltaTime;
}

public delegate void AdjustHeightmap(in AdjustHeightmapProperties properties);
public readonly ref struct AdjustHeightmapProperties(
    Bounds bounds,
    Vector3 position,
    float radius,
    float falloff,
    float strength,
    float sensitivity,
    float deltaTime,
    bool isSubtracting)
{
    public readonly Bounds Bounds = bounds;
    public readonly Vector3 Position = position;
    public readonly float Radius = radius;
    public readonly float Falloff = falloff;
    public readonly float Strength = strength;
    public readonly float Sensitivity = sensitivity;
    public readonly float DeltaTime = deltaTime;
    public readonly bool IsSubtracting = isSubtracting;
}

public delegate void FlattenHeightmap(in FlattenHeightmapProperties properties);

public readonly ref struct FlattenHeightmapProperties(
    Bounds bounds,
    Vector3 position,
    EDevkitLandscapeToolHeightmapFlattenMethod flattenMethod,
    float radius,
    float falloff,
    float strength,
    float sensitivity,
    float target,
    float deltaTime)
{
    public readonly Bounds Bounds = bounds;
    public readonly Vector3 Position = position;
    public readonly EDevkitLandscapeToolHeightmapFlattenMethod FlattenMethod = flattenMethod;
    public readonly float Radius = radius;
    public readonly float Falloff = falloff;
    public readonly float Strength = strength;
    public readonly float Sensitivity = sensitivity;
    public readonly float Target = target;
    public readonly float DeltaTime = deltaTime;
}

public delegate void SmoothHeightmap(in SmoothHeightmapProperties properties);
public readonly ref struct SmoothHeightmapProperties(
    Bounds bounds,
    Vector3 position,
    EDevkitLandscapeToolHeightmapSmoothMethod smoothMethod,
    float radius,
    float falloff,
    float strength,
    float target,
    float deltaTime)
{
    public readonly Bounds Bounds = bounds;
    public readonly Vector3 Position = position;
    public readonly EDevkitLandscapeToolHeightmapSmoothMethod SmoothMethod = smoothMethod;
    public readonly float Radius = radius;
    public readonly float Falloff = falloff;
    public readonly float Strength = strength;
    public readonly float Target = target;
    public readonly float DeltaTime = deltaTime;
}

public delegate void PaintSplatmap(in PaintSplatmapProperties properties);
public readonly ref struct PaintSplatmapProperties(
    Bounds bounds,
    Vector3 position,
    float radius,
    float falloff,
    float strength,
    float sensitivity,
    float target,
    float deltaTime,
    bool useWeightTarget,
    bool useAutoSlope,
    bool useAutoFoundation,
    bool isRemoving,
    AutoSlopeProperties autoSlopeProperties,
    AutoFoundationProperties autoFoundationProperties,
    AssetReference<LandscapeMaterialAsset> material)
{
    public readonly Bounds Bounds = bounds;
    public readonly Vector3 Position = position;
    public readonly float Radius = radius;
    public readonly float Falloff = falloff;
    public readonly float Strength = strength;
    public readonly float Sensitivity = sensitivity;
    public readonly float Target = target;
    public readonly float DeltaTime = deltaTime;
    public readonly bool UseWeightTarget = useWeightTarget;
    public readonly bool UseAutoSlope = useAutoSlope;
    public readonly bool UseAutoFoundation = useAutoFoundation;
    public readonly bool IsRemoving = isRemoving;
    public readonly AutoSlopeProperties AutoSlopeProperties = autoSlopeProperties;
    public readonly AutoFoundationProperties AutoFoundationProperties = autoFoundationProperties;
    public readonly AssetReference<LandscapeMaterialAsset> Material = material;
}

public delegate void SmoothSplatmap(in SmoothSplatmapProperties properties);
public readonly ref struct SmoothSplatmapProperties(
    Bounds bounds,
    Vector3 position,
    EDevkitLandscapeToolSplatmapSmoothMethod smoothMethod,
    float radius,
    float falloff,
    float strength,
    float deltaTime)
{
    public readonly Bounds Bounds = bounds;
    public readonly Vector3 Position = position;
    public readonly EDevkitLandscapeToolSplatmapSmoothMethod SmoothMethod = smoothMethod;
    public readonly float Radius = radius;
    public readonly float Falloff = falloff;
    public readonly float Strength = strength;
    public readonly float DeltaTime = deltaTime;
}

public delegate void MoveHierarchyObjectFinal(in MoveHierarchyObjectFinalProperties properties);
public readonly ref struct MoveHierarchyObjectFinalProperties(
    DevkitSelection selection,
    IDevkitHierarchyItem item,
    FinalTransformation transformation,
    bool useScale,
    float deltaTime)
{
    public readonly DevkitSelection Selection = selection;
    public readonly IDevkitHierarchyItem Item = item;
    public readonly FinalTransformation Transformation = transformation;
    public readonly bool UseScale = useScale;
    public readonly float DeltaTime = deltaTime;
}

public delegate void MoveHierarchyObjectsFinal(in MoveHierarchyObjectsFinalProperties properties);
public readonly ref struct MoveHierarchyObjectsFinalProperties(
    ReadOnlySpan<FinalTransformation> transformations,
    bool useScale,
    float deltaTime)
{
    public readonly ReadOnlySpan<FinalTransformation> Transformations = transformations;
    public readonly bool UseScale = useScale;
    public readonly float DeltaTime = deltaTime;
}

public delegate void DeleteHierarchyObject(in DeleteHierarchyObjectProperties properties);
public readonly ref struct DeleteHierarchyObjectProperties(GameObject gameObject, IDevkitHierarchyItem item, NetId netId)
{
    public readonly GameObject GameObject = gameObject;
    public readonly IDevkitHierarchyItem Item = item;
    public readonly NetId NetId = netId;
}

public delegate void DeleteHierarchyObjects(in DeleteHierarchyObjectsProperties properties);
public readonly ref struct DeleteHierarchyObjectsProperties(ReadOnlySpan<NetId> netIds, float deltaTime)
{
    public readonly ReadOnlySpan<NetId> NetIds = netIds;
    public readonly float DeltaTime = deltaTime;
}

public delegate void RequestInstantiateHierarchyObject(in InstantiateHierarchyObjectProperties properties);
public readonly ref struct InstantiateHierarchyObjectProperties(IHierarchyItemTypeIdentifier type, Vector3 position)
{
    public readonly IHierarchyItemTypeIdentifier Type = type;
    public readonly Vector3 Position = position;
}

public delegate void DeleteLevelObject(in DeleteLevelObjectProperties properties);
public readonly ref struct DeleteLevelObjectProperties(NetId netId, float deltaTime)
{
    public readonly NetId NetId = netId;
    public readonly float DeltaTime = deltaTime;
}

public delegate void DeleteLevelObjects(in DeleteLevelObjectsProperties properties);
public readonly ref struct DeleteLevelObjectsProperties(ReadOnlySpan<NetId> netIds, float deltaTime)
{
    public readonly ReadOnlySpan<NetId> NetIds = netIds;
    public readonly float DeltaTime = deltaTime;
}

public delegate void MoveLevelObjectFinal(in MoveLevelObjectFinalProperties properties);
public readonly ref struct MoveLevelObjectFinalProperties(
    FinalTransformation transformation,
    bool useScale,
    float deltaTime)
{
    public readonly FinalTransformation Transformation = transformation;
    public readonly bool UseScale = useScale;
    public readonly float DeltaTime = deltaTime;
}

public delegate void MoveLevelObjectsFinal(in MoveLevelObjectsFinalProperties properties);
public readonly ref struct MoveLevelObjectsFinalProperties(
    ReadOnlySpan<FinalTransformation> transformations,
    bool useScale,
    float deltaTime)
{
    public readonly ReadOnlySpan<FinalTransformation> Transformations = transformations;
    public readonly bool UseScale = useScale;
    public readonly float DeltaTime = deltaTime;
}

public delegate void UpdateObjectsCustomMaterialPaletteOverride(in UpdateObjectsCustomMaterialPaletteOverrideProperties properties);
public readonly ref struct UpdateObjectsCustomMaterialPaletteOverrideProperties(
    ReadOnlySpan<NetId> netIds,
    AssetReference<MaterialPaletteAsset> material,
    float deltaTime)
{
    public readonly ReadOnlySpan<NetId> NetIds = netIds;
    public readonly AssetReference<MaterialPaletteAsset> Material = material;
    public readonly float DeltaTime = deltaTime;
}

public delegate void UpdateObjectsMaterialIndexOverride(in UpdateObjectsMaterialIndexOverrideProperties properties);
public readonly ref struct UpdateObjectsMaterialIndexOverrideProperties(ReadOnlySpan<NetId> netIds, int index, float deltaTime)
{
    public readonly ReadOnlySpan<NetId> NetIds = netIds;
    public readonly int Index = index;
    public readonly float DeltaTime = deltaTime;
}

public delegate void MoveRoadVertex(in MoveRoadVertexProperties properties);
public delegate void MoveRoadVertexRequested(in MoveRoadVertexProperties properties, ref bool shouldAllow);
public readonly ref struct MoveRoadVertexProperties(
    NetId roadNetId,
    NetId vertexNetId,
    Vector3 oldPosition,
    Vector3 position,
    float deltaTime)
{
    public readonly NetId RoadNetId = roadNetId;
    public readonly NetId VertexNetId = vertexNetId;
    public readonly Vector3 Position = position;
    public readonly Vector3 OldPosition = oldPosition;
    public readonly float DeltaTime = deltaTime;
}

public delegate void MoveRoadTangentHandle(in MoveRoadTangentHandleProperties properties);
public delegate void MoveRoadTangentHandleRequested(in MoveRoadTangentHandleProperties properties, ref bool shouldAllow);
public readonly ref struct MoveRoadTangentHandleProperties(
    NetId roadNetId,
    NetId vertexNetId,
    Vector3 oldPosition,
    TangentHandle handle,
    Vector3 position,
    float deltaTime)
{
    public readonly NetId RoadNetId = roadNetId;
    public readonly NetId VertexNetId = vertexNetId;
    public readonly TangentHandle Handle = handle;
    public readonly Vector3 Position = position;
    public readonly Vector3 OldPosition = oldPosition;
    public readonly float DeltaTime = deltaTime;
}

public delegate void DeleteRoadVertex(in DeleteRoadVertexProperties properties);
public delegate void DeleteRoadVertexRequested(in DeleteRoadVertexProperties properties, ref bool shouldAllow);
public readonly ref struct DeleteRoadVertexProperties(
    NetId roadNetId,
    NetId vertexNetId,
    Vector3 oldPosition,
    float deltaTime)
{
    public readonly NetId RoadNetId = roadNetId;
    public readonly NetId VertexNetId = vertexNetId;
    public readonly Vector3 OldPosition = oldPosition;
    public readonly float DeltaTime = deltaTime;
}

public delegate void DeleteRoad(in DeleteRoadProperties properties);
public delegate void DeleteRoadRequested(in DeleteRoadProperties properties, ref bool shouldAllow);
public readonly ref struct DeleteRoadProperties(NetId roadNetId, Vector3 oldPosition, float deltaTime)
{
    public readonly NetId RoadNetId = roadNetId;
    public readonly Vector3 OldPosition = oldPosition;
    public readonly float DeltaTime = deltaTime;
}

public delegate void RequestInstantiateRoad(in RequestInstantiateRoadProperties properties);
public delegate void RequestInstantiateRoadRequested(in RequestInstantiateRoadProperties properties, ref bool shouldAllow);
public readonly ref struct RequestInstantiateRoadProperties(Vector3 firstVertexPosition, byte materialIndex)
{
    public readonly Vector3 FirstVertexPosition = firstVertexPosition;
    public readonly byte MaterialIndex = materialIndex;
}

public delegate void RequestInstantiateRoadVertex(in RequestInstantiateRoadVertexProperties properties);
public delegate void RequestInstantiateRoadVertexRequested(in RequestInstantiateRoadVertexProperties properties, ref bool shouldAllow);
public readonly ref struct RequestInstantiateRoadVertexProperties(NetId roadNetId, Vector3 vertexPosition, int vertexIndex)
{
    public readonly NetId RoadNetId = roadNetId;
    public readonly Vector3 VertexPosition = vertexPosition;
    public readonly int VertexIndex = vertexIndex;
}

public delegate void SetRoadMaterialVerticalOffset(in SetRoadMaterialVerticalOffsetProperties properties);
public delegate void SetRoadMaterialVerticalOffsetRequested(in SetRoadMaterialVerticalOffsetProperties properties, ref bool shouldAllow);
public readonly ref struct SetRoadMaterialVerticalOffsetProperties(
    byte materialIndex,
    float verticalOffset,
    float oldVerticalOffset,
    float deltaTime)
{
    public readonly byte MaterialIndex = materialIndex;
    public readonly float OldVerticalOffset = oldVerticalOffset;
    public readonly float VerticalOffset = verticalOffset;
    public readonly float DeltaTime = deltaTime;
}

public delegate void SetRoadMaterialHeight(in SetRoadMaterialHeightProperties properties);
public delegate void SetRoadMaterialHeightRequested(in SetRoadMaterialHeightProperties properties, ref bool shouldAllow);
public readonly ref struct SetRoadMaterialHeightProperties(
    byte materialIndex,
    float height,
    float oldHeight,
    float deltaTime)
{
    public readonly byte MaterialIndex = materialIndex;
    public readonly float OldHeight = oldHeight;
    public readonly float Height = height;
    public readonly float DeltaTime = deltaTime;
}

public delegate void SetRoadMaterialWidth(in SetRoadMaterialWidthProperties properties);
public delegate void SetRoadMaterialWidthRequested(in SetRoadMaterialWidthProperties properties, ref bool shouldAllow);
public readonly ref struct SetRoadMaterialWidthProperties(byte materialIndex, float width, float oldWidth, float deltaTime)
{
    public readonly byte MaterialIndex = materialIndex;
    public readonly float OldWidth = oldWidth;
    public readonly float Width = width;
    public readonly float DeltaTime = deltaTime;
}

public delegate void SetRoadMaterialDepth(in SetRoadMaterialDepthProperties properties);
public delegate void SetRoadMaterialDepthRequested(in SetRoadMaterialDepthProperties properties, ref bool shouldAllow);
public readonly ref struct SetRoadMaterialDepthProperties(byte materialIndex, float depth, float oldDepth, float deltaTime)
{
    public readonly byte MaterialIndex = materialIndex;
    public readonly float OldDepth = oldDepth;
    public readonly float Depth = depth;
    public readonly float DeltaTime = deltaTime;
}

public delegate void SetRoadMaterialIsConcrete(in SetRoadMaterialIsConcreteProperties properties);
public delegate void SetRoadMaterialIsConcreteRequested(in SetRoadMaterialIsConcreteProperties properties, ref bool shouldAllow);
public readonly ref struct SetRoadMaterialIsConcreteProperties(byte materialIndex, bool isConcrete, float deltaTime)
{
    public readonly byte MaterialIndex = materialIndex;
    public readonly bool IsConcrete = isConcrete;
    public readonly float DeltaTime = deltaTime;
}

public delegate void SetRoadIsLoop(in SetRoadIsLoopProperties properties);
public delegate void SetRoadIsLoopRequested(in SetRoadIsLoopProperties properties, ref bool shouldAllow);
public readonly ref struct SetRoadIsLoopProperties(NetId roadNetId, bool isLoop, float deltaTime)
{
    public readonly NetId RoadNetId = roadNetId;
    public readonly bool IsLoop = isLoop;
    public readonly float DeltaTime = deltaTime;
}

public delegate void SetRoadVertexIgnoreTerrain(in SetRoadVertexIgnoreTerrainProperties properties);
public delegate void SetRoadVertexIgnoreTerrainRequested(in SetRoadVertexIgnoreTerrainProperties properties, ref bool shouldAllow);
public readonly ref struct SetRoadVertexIgnoreTerrainProperties(
    NetId roadNetId,
    NetId vertexNetId,
    bool ignoreTerrain,
    float deltaTime)
{
    public readonly NetId RoadNetId = roadNetId;
    public readonly NetId VertexNetId = vertexNetId;
    public readonly bool IgnoreTerrain = ignoreTerrain;
    public readonly float DeltaTime = deltaTime;
}

public delegate void SetRoadVertexVerticalOffset(in SetRoadVertexVerticalOffsetProperties properties);
public delegate void SetRoadVertexVerticalOffsetRequested(in SetRoadVertexVerticalOffsetProperties properties, ref bool shouldAllow);
public readonly ref struct SetRoadVertexVerticalOffsetProperties(
    NetId roadNetId,
    NetId vertexNetId,
    float verticalOffset,
    float oldVerticalOffset,
    float deltaTime)
{
    public readonly NetId RoadNetId = roadNetId;
    public readonly NetId VertexNetId = vertexNetId;
    public readonly float VerticalOffset = verticalOffset;
    public readonly float OldVerticalOffset = oldVerticalOffset;
    public readonly float DeltaTime = deltaTime;
}

public delegate void SetRoadVertexTangentHandleMode(in SetRoadVertexTangentHandleModeProperties properties);
public delegate void SetRoadVertexTangentHandleModeRequested(in SetRoadVertexTangentHandleModeProperties properties, ref bool shouldAllow);
public readonly ref struct SetRoadVertexTangentHandleModeProperties(
    NetId roadNetId,
    NetId vertexNetId,
    ERoadMode tangentHandleMode,
    ERoadMode oldTangentHandleMode,
    float deltaTime)
{
    public readonly NetId RoadNetId = roadNetId;
    public readonly NetId VertexNetId = vertexNetId;
    public readonly ERoadMode TangentHandleMode = tangentHandleMode;
    public readonly ERoadMode OldTangentHandleMode = oldTangentHandleMode;
    public readonly float DeltaTime = deltaTime;
}

public delegate void SetRoadMaterial(in SetRoadMaterialProperties properties);
public delegate void SetRoadMaterialRequested(in SetRoadMaterialProperties properties, ref bool shouldAllow);
public readonly ref struct SetRoadMaterialProperties(NetId roadNetId, RoadMaterialOrAsset material, float deltaTime)
{
    public readonly NetId RoadNetId = roadNetId;
    public readonly RoadMaterialOrAsset Material = material;
    [Obsolete("Use Material instead.")]
    public readonly byte MaterialIndex = material.LegacyIndex;
    public readonly float DeltaTime = deltaTime;
}

public delegate void RequestInstantiateNavigation(in RequestInstantiateNavigationProperties properties);
public delegate void RequestInstantiateNavigationRequested(in RequestInstantiateNavigationProperties properties, ref bool shouldAllow);
public readonly ref struct RequestInstantiateNavigationProperties(Vector3 position)
{
    public readonly Vector3 Position = position;
}

public delegate void DeleteNavigation(in DeleteNavigationProperties properties);
public delegate void DeleteNavigationRequested(in DeleteNavigationProperties properties, ref bool shouldAllow);
public readonly ref struct DeleteNavigationProperties(NetId navigationNetId, Vector3 oldPosition, float deltaTime)
{
    public readonly NetId NavigationNetId = navigationNetId;
    public readonly Vector3 OldPosition = oldPosition;
    public readonly float DeltaTime = deltaTime;
}

public delegate void MoveNavigation(in MoveNavigationProperties properties);
public delegate void MoveNavigationRequested(in MoveNavigationProperties properties, ref bool shouldAllow);
public readonly ref struct MoveNavigationProperties(
    NetId navigationNetId,
    Vector3 position,
    Vector3 oldPosition,
    float deltaTime)
{
    public readonly NetId NavigationNetId = navigationNetId;
    public readonly Vector3 Position = position;
    public readonly Vector3 OldPosition = oldPosition;
    public readonly float DeltaTime = deltaTime;
}

public delegate void SetNavigationSize(in SetNavigationSizeProperties properties);
public delegate void SetNavigationSizeRequested(in SetNavigationSizeProperties properties, ref bool shouldAllow);
public readonly ref struct SetNavigationSizeProperties(
    NetId navigationNetId,
    Vector2 size,
    Vector2 oldSize,
    float deltaTime)
{
    public readonly NetId NavigationNetId = navigationNetId;
    public readonly Vector2 Size = size;
    public readonly Vector2 OldSize = oldSize;
    public readonly float DeltaTime = deltaTime;
}

public delegate void SetNavigationDifficulty(in SetNavigationDifficultyProperties properties);
public delegate void SetNavigationDifficultyRequested(in SetNavigationDifficultyProperties properties, ref bool shouldAllow);
public readonly ref struct SetNavigationDifficultyProperties(
    NetId navigationNetId,
    AssetReference<ZombieDifficultyAsset> difficulty,
    AssetReference<ZombieDifficultyAsset> oldDifficulty,
    float deltaTime)
{
    public readonly NetId NavigationNetId = navigationNetId;
    public readonly AssetReference<ZombieDifficultyAsset> Difficulty = difficulty;
    public readonly AssetReference<ZombieDifficultyAsset> OldDifficulty = oldDifficulty;
    public readonly float DeltaTime = deltaTime;
}

public delegate void SetNavigationMaximumZombies(in SetNavigationMaximumZombiesProperties properties);
public delegate void SetNavigationMaximumZombiesRequested(in SetNavigationMaximumZombiesProperties properties, ref bool shouldAllow);
public readonly ref struct SetNavigationMaximumZombiesProperties(
    NetId navigationNetId,
    byte maximumZombies,
    byte oldMaximumZombies,
    float deltaTime)
{
    public readonly NetId NavigationNetId = navigationNetId;
    public readonly byte MaximumZombies = maximumZombies;
    public readonly byte OldMaximumZombies = oldMaximumZombies;
    public readonly float DeltaTime = deltaTime;
}

public delegate void SetNavigationMaximumBossZombies(in SetNavigationMaximumBossZombiesProperties properties);
public delegate void SetNavigationMaximumBossZombiesRequested(in SetNavigationMaximumBossZombiesProperties properties, ref bool shouldAllow);
public readonly ref struct SetNavigationMaximumBossZombiesProperties(
    NetId navigationNetId,
    int maximumBossZombies,
    int oldMaximumBossZombies,
    float deltaTime)
{
    public readonly NetId NavigationNetId = navigationNetId;
    public readonly int MaximumBossZombies = maximumBossZombies;
    public readonly int OldMaximumBossZombies = oldMaximumBossZombies;
    public readonly float DeltaTime = deltaTime;
}

public delegate void SetNavigationShouldSpawnZombies(in SetNavigationShouldSpawnZombiesProperties properties);
public delegate void SetNavigationShouldSpawnZombiesRequested(in SetNavigationShouldSpawnZombiesProperties properties, ref bool shouldAllow);
public readonly ref struct SetNavigationShouldSpawnZombiesProperties(
    NetId navigationNetId,
    bool shouldSpawnZombies,
    float deltaTime)
{
    public readonly NetId NavigationNetId = navigationNetId;
    public readonly bool ShouldSpawnZombies = shouldSpawnZombies;
    public readonly float DeltaTime = deltaTime;
}

public delegate void SetNavigationInfiniteAgroDistance(in SetNavigationInfiniteAgroDistanceProperties properties);
public delegate void SetNavigationInfiniteAgroDistanceRequested(in SetNavigationInfiniteAgroDistanceProperties properties, ref bool shouldAllow);
public readonly ref struct SetNavigationInfiniteAgroDistanceProperties(
    NetId navigationNetId,
    bool infiniteAgroDistance,
    float deltaTime)
{
    public readonly NetId NavigationNetId = navigationNetId;
    public readonly bool InfiniteAgroDistance = infiniteAgroDistance;
    public readonly float DeltaTime = deltaTime;
}

public delegate void MoveSpawnFinal(in MoveSpawnFinalProperties properties);
public delegate void MoveSpawnFinalRequested(in MoveSpawnFinalProperties properties, ref bool shouldAllow);
public readonly ref struct MoveSpawnFinalProperties(NetId64 spawnNetId, TransformationDelta transformation, SpawnType spawnType, bool useRotation, float deltaTime)
{
    public readonly NetId64 SpawnNetId = spawnNetId;
    public readonly SpawnType SpawnType = spawnType;
    public readonly TransformationDelta Transformation = transformation;
    public readonly bool UseRotation = useRotation;
    public readonly float DeltaTime = deltaTime;
}

public delegate void MoveSpawnsFinal(in MoveSpawnsFinalProperties properties);
public readonly ref struct MoveSpawnsFinalProperties(ReadOnlySpan<NetId64> spawnNetIds, ReadOnlySpan<TransformationDelta> transformations, SpawnType spawnType, bool useRotation, float deltaTime)
{
    public readonly ReadOnlySpan<NetId64> SpawnNetIds = spawnNetIds;
    public readonly SpawnType SpawnType = spawnType;
    public readonly ReadOnlySpan<TransformationDelta> Transformations = transformations;
    public readonly bool UseRotation = useRotation;
    public readonly float DeltaTime = deltaTime;
}

public delegate void DeleteSpawn(in DeleteSpawnProperties properties);
public delegate void DeleteSpawnRequested(in DeleteSpawnProperties properties, ref bool shouldAllow);
public readonly ref struct DeleteSpawnProperties(NetId64 spawnNetId, SpawnType spawnType, float deltaTime)
{
    public readonly NetId64 SpawnNetId = spawnNetId;
    public readonly SpawnType SpawnType = spawnType;
    public readonly float DeltaTime = deltaTime;
}

public delegate void DeleteSpawns(in DeleteSpawnsProperties properties);
public readonly ref struct DeleteSpawnsProperties(ReadOnlySpan<NetId64> spawnNetIds, SpawnType spawnType, float deltaTime)
{
    public readonly ReadOnlySpan<NetId64> SpawnNetIds = spawnNetIds;
    public readonly SpawnType SpawnType = spawnType;
    public readonly float DeltaTime = deltaTime;
}

public delegate void SetSpawnpointType(in SetSpawnpointTypeProperties properties);
public delegate void SetSpawnpointTypeRequested(in SetSpawnpointTypeProperties properties, ref bool shouldAllow);
public readonly ref struct SetSpawnpointTypeProperties(NetId64 spawnpointId, NetId64 spawnTableId, SpawnType spawnType, float deltaTime)
{
    public readonly NetId64 SpawnpointId = spawnpointId;
    public readonly NetId64 SpawnTableId = spawnTableId;
    public readonly SpawnType SpawnType = spawnType;
    public readonly float DeltaTime = deltaTime;
}

public delegate void SetSpawnpointsType(in SetSpawnpointsTypeProperties properties);
public readonly ref struct SetSpawnpointsTypeProperties(ReadOnlySpan<NetId64> spawnpointIds, NetId64 spawnTableId, SpawnType spawnType, float deltaTime)
{
    public readonly ReadOnlySpan<NetId64> SpawnpointIds = spawnpointIds;
    public readonly NetId64 SpawnTableId = spawnTableId;
    public readonly SpawnType SpawnType = spawnType;
    public readonly float DeltaTime = deltaTime;
}

public delegate void SetSpawnTableColor(in SetSpawnTableColorProperties properties);
public delegate void SetSpawnTableColorRequested(in SetSpawnTableColorProperties properties, ref bool shouldAllow);
public readonly ref struct SetSpawnTableColorProperties(NetId64 spawnTableId, SpawnType spawnType, Color color, float deltaTime)
{
    public readonly NetId64 SpawnTableId = spawnTableId;
    public readonly SpawnType SpawnType = spawnType;
    public readonly Color Color = color;
    public readonly float DeltaTime = deltaTime;
}

public delegate void SetSpawnTableSpawnAsset(in SetSpawnTableSpawnAssetProperties properties);
public delegate void SetSpawnTableSpawnAssetRequested(in SetSpawnTableSpawnAssetProperties properties, ref bool shouldAllow);
public readonly ref struct SetSpawnTableSpawnAssetProperties(NetId64 spawnTableId, SpawnType spawnType, AssetReference<SpawnAsset> asset, float deltaTime)
{
    public readonly NetId64 SpawnTableId = spawnTableId;
    public readonly SpawnType SpawnType = spawnType;
    public readonly AssetReference<SpawnAsset> Asset = asset;
    public readonly float DeltaTime = deltaTime;
}

public delegate void SetSpawnTableName(in SetSpawnTableNameProperties properties);
public delegate void SetSpawnTableNameRequested(in SetSpawnTableNameProperties properties, ref bool shouldAllow);
public readonly ref struct SetSpawnTableNameProperties(NetId64 spawnTableId, SpawnType spawnType, string name, float deltaTime)
{
    public readonly NetId64 SpawnTableId = spawnTableId;
    public readonly SpawnType SpawnType = spawnType;
    public readonly string Name = name;
    public readonly float DeltaTime = deltaTime;
}

public delegate void SetSpawnTableTierChance(in SetSpawnTableTierChanceProperties properties);
public delegate void SetSpawnTableTierChanceRequested(in SetSpawnTableTierChanceProperties properties, ref bool shouldAllow);
public readonly ref struct SetSpawnTableTierChanceProperties(NetId64 spawnTableId, NetId64 spawnTierId, SpawnType spawnType, float chance, float deltaTime)
{
    public readonly NetId64 SpawnTableId = spawnTableId;
    public readonly NetId64 SpawnTierId = spawnTierId;
    public readonly SpawnType SpawnType = spawnType;
    public readonly float Chance = chance;
    public readonly float DeltaTime = deltaTime;
}

public delegate void SetSpawnTableTierName(in SetSpawnTableTierNameProperties properties);
public delegate void SetSpawnTableTierNameRequested(in SetSpawnTableTierNameProperties properties, ref bool shouldAllow);
public readonly ref struct SetSpawnTableTierNameProperties(NetId64 spawnTableId, NetId64 spawnTierId, SpawnType spawnType, string name, float deltaTime)
{
    public readonly NetId64 SpawnTableId = spawnTableId;
    public readonly NetId64 SpawnTierId = spawnTierId;
    public readonly SpawnType SpawnType = spawnType;
    public readonly string Name = name;
    public readonly float DeltaTime = deltaTime;
}

public delegate void DeleteSpawnTable(in DeleteSpawnTableProperties properties);
public delegate void DeleteSpawnTableRequested(in DeleteSpawnTableProperties properties, ref bool shouldAllow);
public readonly ref struct DeleteSpawnTableProperties(NetId64 spawnTableId, SpawnType spawnType, float deltaTime)
{
    public readonly NetId64 SpawnTableId = spawnTableId;
    public readonly SpawnType SpawnType = spawnType;
    public readonly float DeltaTime = deltaTime;
}

public delegate void DeleteSpawnTableTier(in DeleteSpawnTableTierProperties properties);
public delegate void DeleteSpawnTableTierRequested(in DeleteSpawnTableTierProperties properties, ref bool shouldAllow);
public readonly ref struct DeleteSpawnTableTierProperties(NetId64 spawnTableId, NetId64 spawnTierId, SpawnType spawnType, float deltaTime)
{
    public readonly NetId64 SpawnTableId = spawnTableId;
    public readonly NetId64 SpawnTierId = spawnTierId;
    public readonly SpawnType SpawnType = spawnType;
    public readonly float DeltaTime = deltaTime;
}

public delegate void SetSpawnTableTierAsset(in SetSpawnTableTierAssetProperties properties);
public delegate void SetSpawnTableTierAssetRequested(in SetSpawnTableTierAssetProperties properties, ref bool shouldAllow);
public readonly ref struct SetSpawnTableTierAssetProperties(NetId64 spawnTableId, NetId64 spawnTierId, NetId64 spawnAssetId, SpawnType spawnType, AssetReference<Asset> asset, float deltaTime)
{
    public readonly NetId64 SpawnTableId = spawnTableId;
    public readonly NetId64 SpawnTierId = spawnTierId;
    public readonly NetId64 SpawnAssetId = spawnAssetId;
    public readonly SpawnType SpawnType = spawnType;
    public readonly AssetReference<Asset> Asset = asset;
    public readonly float DeltaTime = deltaTime;
}

public delegate void SetSpawnTableTierChances(in SetSpawnTableTierChancesProperties properties);
public delegate void SetSpawnTableTierChancesRequested(in SetSpawnTableTierChancesProperties properties, ref bool shouldAllow);
public readonly ref struct SetSpawnTableTierChancesProperties(NetId64 spawnTableId, ArraySegment<NetId64> spawnTierIds, SpawnType spawnType, ArraySegment<float> chances, float deltaTime)
{
    public readonly NetId64 SpawnTableId = spawnTableId;
    public readonly ArraySegment<NetId64> SpawnTierIds = spawnTierIds;
    public readonly SpawnType SpawnType = spawnType;
    public readonly ArraySegment<float> Chances = chances;
    public readonly float DeltaTime = deltaTime;
}

public delegate void DeleteSpawnTableTierAsset(in DeleteSpawnTableTierAssetProperties properties);
public delegate void DeleteSpawnTableTierAssetRequested(in DeleteSpawnTableTierAssetProperties properties, ref bool shouldAllow);
public readonly ref struct DeleteSpawnTableTierAssetProperties(NetId64 spawnTableId, NetId64 spawnTierId, NetId64 spawnAssetId, SpawnType spawnType, float deltaTime)
{
    public readonly NetId64 SpawnTableId = spawnTableId;
    public readonly NetId64 SpawnTierId = spawnTierId;
    public readonly NetId64 SpawnAssetId = spawnAssetId;
    public readonly SpawnType SpawnType = spawnType;
    public readonly float DeltaTime = deltaTime;
}

public delegate void SetZombieSpawnTableDifficultyAsset(in SetZombieSpawnTableDifficultyAssetProperties properties);
public delegate void SetZombieSpawnTableDifficultyAssetRequested(in SetZombieSpawnTableDifficultyAssetProperties properties, ref bool shouldAllow);
public readonly ref struct SetZombieSpawnTableDifficultyAssetProperties(NetId64 spawnTableId, AssetReference<ZombieDifficultyAsset> difficultyAsset, float deltaTime)
{
    public readonly NetId64 SpawnTableId = spawnTableId;
    public readonly AssetReference<ZombieDifficultyAsset> DifficultyAsset = difficultyAsset;
    public readonly float DeltaTime = deltaTime;
}

public delegate void SetZombieSpawnTableIsMega(in SetZombieSpawnTableIsMegaProperties properties);
public delegate void SetZombieSpawnTableIsMegaRequested(in SetZombieSpawnTableIsMegaProperties properties, ref bool shouldAllow);
public readonly ref struct SetZombieSpawnTableIsMegaProperties(NetId64 spawnTableId, bool isMega, float deltaTime)
{
    public readonly NetId64 SpawnTableId = spawnTableId;
    public readonly bool IsMega = isMega;
    public readonly float DeltaTime = deltaTime;
}

public delegate void SetZombieSpawnTableHealth(in SetZombieSpawnTableHealthProperties properties);
public delegate void SetZombieSpawnTableHealthRequested(in SetZombieSpawnTableHealthProperties properties, ref bool shouldAllow);
public readonly ref struct SetZombieSpawnTableHealthProperties(NetId64 spawnTableId, ushort health, float deltaTime)
{
    public readonly NetId64 SpawnTableId = spawnTableId;
    public readonly ushort Health = health;
    public readonly float DeltaTime = deltaTime;
}

public delegate void SetZombieSpawnTableDamage(in SetZombieSpawnTableDamageProperties properties);
public delegate void SetZombieSpawnTableDamageRequested(in SetZombieSpawnTableDamageProperties properties, ref bool shouldAllow);
public readonly ref struct SetZombieSpawnTableDamageProperties(NetId64 spawnTableId, byte damage, float deltaTime)
{
    public readonly NetId64 SpawnTableId = spawnTableId;
    public readonly byte Damage = damage;
    public readonly float DeltaTime = deltaTime;
}

public delegate void SetZombieSpawnTableLootIndex(in SetZombieSpawnTableLootIndexProperties properties);
public delegate void SetZombieSpawnTableLootIndexRequested(in SetZombieSpawnTableLootIndexProperties properties, ref bool shouldAllow);
public readonly ref struct SetZombieSpawnTableLootIndexProperties(NetId64 spawnTableId, NetId64 lootTableNetId, float deltaTime)
{
    public readonly NetId64 SpawnTableId = spawnTableId;
    public readonly NetId64 LootTableNetId = lootTableNetId;
    public readonly float DeltaTime = deltaTime;
}

public delegate void SetZombieSpawnTableXP(in SetZombieSpawnTableXPProperties properties);
public delegate void SetZombieSpawnTableXPRequested(in SetZombieSpawnTableXPProperties properties, ref bool shouldAllow);
public readonly ref struct SetZombieSpawnTableXPProperties(NetId64 spawnTableId, uint xp, float deltaTime)
{
    public readonly NetId64 SpawnTableId = spawnTableId;
    public readonly uint XP = xp;
    public readonly float DeltaTime = deltaTime;
}

public delegate void SetZombieSpawnTableRegen(in SetZombieSpawnTableRegenProperties properties);
public delegate void SetZombieSpawnTableRegenRequested(in SetZombieSpawnTableRegenProperties properties, ref bool shouldAllow);
public readonly ref struct SetZombieSpawnTableRegenProperties(NetId64 spawnTableId, float regen, float deltaTime)
{
    public readonly NetId64 SpawnTableId = spawnTableId;
    public readonly float Regen = regen;
    public readonly float DeltaTime = deltaTime;
}

public delegate void RequestInstantiateSpawnpoint(in RequestInstantiateSpawnpointProperties properties);
public delegate void RequestInstantiateSpawnpointRequested(in RequestInstantiateSpawnpointProperties properties, ref bool shouldAllow);
public readonly ref struct RequestInstantiateSpawnpointProperties(TransformationDelta transform, NetId64 spawnTableId, SpawnType spawnType)
{
    public readonly TransformationDelta Transform = transform;
    public readonly NetId64 SpawnTableId = spawnTableId;
    public readonly SpawnType SpawnType = spawnType;
}

public delegate void RequestInstantiateSpawnTable(in RequestInstantiateSpawnTableProperties properties);
public delegate void RequestInstantiateSpawnTableRequested(in RequestInstantiateSpawnTableProperties properties, ref bool shouldAllow);
public readonly ref struct RequestInstantiateSpawnTableProperties(SpawnType spawnType, string name)
{
    public readonly string Name = name;
    public readonly SpawnType SpawnType = spawnType;
}

public delegate void RequestInstantiateSpawnTier(in RequestInstantiateSpawnTierProperties properties);
public delegate void RequestInstantiateSpawnTierRequested(in RequestInstantiateSpawnTierProperties properties, ref bool shouldAllow);
public readonly ref struct RequestInstantiateSpawnTierProperties(NetId64 spawnTableId, SpawnType spawnType, string name)
{
    public readonly NetId64 SpawnTableId = spawnTableId;
    public readonly SpawnType SpawnType = spawnType;
    public readonly string? Name = name;
}

public delegate void RequestInstantiateSpawnTierAsset(in RequestInstantiateSpawnTierAssetProperties properties);
public delegate void RequestInstantiateSpawnTierAssetRequested(in RequestInstantiateSpawnTierAssetProperties properties, ref bool shouldAllow);
public readonly ref struct RequestInstantiateSpawnTierAssetProperties(NetId64 spawnTableId, NetId64 spawnTierId, SpawnType spawnType, ushort legacyId)
{
    public readonly NetId64 SpawnTableId = spawnTableId;
    public readonly NetId64 SpawnTierId = spawnTierId;
    public readonly SpawnType SpawnType = spawnType;
    public readonly ushort LegacyId = legacyId;
}

public delegate void SetPlayerSpawnpointIsAlternate(in SetPlayerSpawnpointIsAlternateProperties properties);
public delegate void SetPlayerSpawnpointIsAlternateRequested(in SetPlayerSpawnpointIsAlternateProperties properties, ref bool shouldAllow);
public readonly ref struct SetPlayerSpawnpointIsAlternateProperties(NetId64 spawnpointId, bool isAlternate, float deltaTime)
{
    public readonly NetId64 SpawnNetIds = spawnpointId;
    public readonly bool IsAlternate = isAlternate;
    public readonly float DeltaTime = deltaTime;
}

public delegate void SetPlayerSpawnpointsIsAlternate(in SetPlayerSpawnpointsIsAlternateProperties properties);
public readonly ref struct SetPlayerSpawnpointsIsAlternateProperties(ReadOnlySpan<NetId64> spawnpointId, bool isAlternate, float deltaTime)
{
    public readonly ReadOnlySpan<NetId64> SpawnNetIds = spawnpointId;
    public readonly bool IsAlternate = isAlternate;
    public readonly float DeltaTime = deltaTime;
}

public delegate void SetLightingFloat(in SetLightingFloatProperties properties);
public delegate void SetLightingFloatRequested(in SetLightingFloatProperties properties, ref bool shouldAllow);
public readonly ref struct SetLightingFloatProperties(LightingValue valueType, float value, float deltaTime)
{
    public readonly LightingValue ValueType = valueType;
    public readonly float Value = value;
    public readonly float DeltaTime = deltaTime;
}

public delegate void SetLightingByte(in SetLightingByteProperties properties);
public delegate void SetLightingByteRequested(in SetLightingByteProperties properties, ref bool shouldAllow);
public readonly ref struct SetLightingByteProperties(LightingValue valueType, byte value, float deltaTime)
{
    public readonly LightingValue ValueType = valueType;
    public readonly byte Value = value;
    public readonly float DeltaTime = deltaTime;
}

public delegate void SetPreviewWeatherAsset(in SetPreviewWeatherAssetProperties properties);
public delegate void SetPreviewWeatherAssetRequested(in SetPreviewWeatherAssetProperties properties, ref bool shouldAllow);
public readonly ref struct SetPreviewWeatherAssetProperties(AssetReference<WeatherAssetBase> asset, float deltaTime)
{
    public readonly AssetReference<WeatherAssetBase> Asset = asset;
    public readonly float DeltaTime = deltaTime;
}

public delegate void SetTimeColor(in SetTimeColorProperties properties);
public delegate void SetTimeColorRequested(in SetTimeColorProperties properties, ref bool shouldAllow);
public readonly ref struct SetTimeColorProperties(ELightingTime time, byte index, Color color, float deltaTime)
{
    public readonly ELightingTime Time = time;
    public readonly byte Index = index;
    public readonly Color Color = color;
    public readonly float DeltaTime = deltaTime;
}

public delegate void SetTimeSingle(in SetTimeSingleProperties properties);
public delegate void SetTimeSingleRequested(in SetTimeSingleProperties properties, ref bool shouldAllow);
public readonly ref struct SetTimeSingleProperties(ELightingTime time, byte index, float value, float deltaTime)
{
    public readonly ELightingTime Time = time;
    public readonly byte Index = index;
    public readonly float Value = value;
    public readonly float DeltaTime = deltaTime;
}