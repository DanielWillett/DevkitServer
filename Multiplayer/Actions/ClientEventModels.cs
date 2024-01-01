using DevkitServer.API;
using DevkitServer.API.Abstractions;
using DevkitServer.Models;
using SDG.Framework.Devkit;
using SDG.Framework.Devkit.Tools;
using SDG.Framework.Foliage;
using SDG.Framework.Landscapes;

namespace DevkitServer.Multiplayer.Actions;

public delegate void PaintHoles(in PaintHolesProperties properties);
public readonly struct PaintHolesProperties
{
    public readonly Bounds Bounds;
    public readonly Vector3 Position;
    public readonly float Radius;
    public readonly float DeltaTime;
    public readonly bool IsFilling;
    public PaintHolesProperties(Bounds bounds, Vector3 position, float radius, bool isFilling, float deltaTime)
    {
        Bounds = bounds;
        Position = position;
        Radius = radius;
        IsFilling = isFilling;
        DeltaTime = deltaTime;
    }
}

public delegate void UpdateLandscapeTile(in UpdateLandscapeTileProperties properties);
public readonly struct UpdateLandscapeTileProperties
{
    public readonly LandscapeTile Tile;
    public readonly float DeltaTime;
    public UpdateLandscapeTileProperties(LandscapeTile tile, float deltaTime)
    {
        Tile = tile;
        DeltaTime = deltaTime;
    }
}

public delegate void AddFoliage(in AddFoliageProperties properties);
public readonly struct AddFoliageProperties
{
    public readonly FoliageInfoAsset Asset;
    public readonly Vector3 Position;
    public readonly Quaternion Rotation;
    public readonly Vector3 Scale;
    public readonly bool ClearWhenBaked;
    public readonly float DeltaTime;
    public readonly uint? ObjectInstanceId;
    public AddFoliageProperties(FoliageInfoAsset asset, Vector3 position, Quaternion rotation, Vector3 scale, bool clearWhenBaked, float deltaTime, uint? objectInstanceId)
    {
        Asset = asset;
        Position = position;
        Rotation = rotation;
        Scale = scale;
        ClearWhenBaked = clearWhenBaked;
        DeltaTime = deltaTime;
        ObjectInstanceId = objectInstanceId;
    }
}

public delegate void RemoveFoliage(in RemoveFoliageProperties properties);
public readonly struct RemoveFoliageProperties
{
    public readonly Vector3 BrushPosition;
    public readonly FoliageTile Tile;
    public readonly FoliageInstanceList FoliageInstances;
    public readonly float BrushRadius;
    public readonly float BrushFalloff;
    public readonly float DeltaTime;
    public readonly bool AllowRemovingBakedFoliage;
    public readonly int SampleCount;
    public RemoveFoliageProperties(Vector3 brushPosition, FoliageTile tile, FoliageInstanceList foliageInstances, float brushRadius, float brushFalloff, float deltaTime, bool allowRemovingBakedFoliage, int sampleCount)
    {
        BrushPosition = brushPosition;
        Tile = tile;
        FoliageInstances = foliageInstances;
        BrushRadius = brushRadius;
        BrushFalloff = brushFalloff;
        DeltaTime = deltaTime;
        AllowRemovingBakedFoliage = allowRemovingBakedFoliage;
        SampleCount = sampleCount;
    }
}

public delegate void RemoveResourceSpawnpointFoliage(in RemoveResourceSpawnpointFoliageProperties foliageProperties);
public readonly struct RemoveResourceSpawnpointFoliageProperties
{
    public readonly ResourceSpawnpoint Spawnpoint;
    public readonly float DeltaTime;
    public RemoveResourceSpawnpointFoliageProperties(ResourceSpawnpoint spawnpoint, float deltaTime)
    {
        Spawnpoint = spawnpoint;
        DeltaTime = deltaTime;
    }
}

public delegate void RemoveLevelObjectFoliage(in RemoveLevelObjectFoliageProperties foliageProperties);
public readonly struct RemoveLevelObjectFoliageProperties
{
    public readonly Vector3 Position;
    public readonly LevelObject LevelObject;
    public readonly float DeltaTime;
    public RemoveLevelObjectFoliageProperties(Vector3 position, LevelObject levelObject, float deltaTime)
    {
        Position = position;
        LevelObject = levelObject;
        DeltaTime = deltaTime;
    }
}

public delegate void PaintRamp(in PaintRampProperties properties);
public readonly struct PaintRampProperties
{
    public readonly Bounds Bounds;
    public readonly Vector3 Start;
    public readonly Vector3 End;
    public readonly float Radius;
    public readonly float Falloff;
    public readonly float DeltaTime;
    public PaintRampProperties(Bounds bounds, Vector3 start, Vector3 end, float radius, float falloff, float deltaTime)
    {
        Bounds = bounds;
        Start = start;
        End = end;
        Radius = radius;
        Falloff = falloff;
        DeltaTime = deltaTime;
    }
}

public delegate void AdjustHeightmap(in AdjustHeightmapProperties properties);
public readonly struct AdjustHeightmapProperties
{
    public readonly Bounds Bounds;
    public readonly Vector3 Position;
    public readonly float Radius;
    public readonly float Falloff;
    public readonly float Strength;
    public readonly float Sensitivity;
    public readonly float DeltaTime;
    public readonly bool IsSubtracting;
    public AdjustHeightmapProperties(Bounds bounds, Vector3 position, float radius, float falloff, float strength, float sensitivity, float deltaTime, bool isSubtracting)
    {
        Bounds = bounds;
        Position = position;
        Radius = radius;
        Falloff = falloff;
        Strength = strength;
        Sensitivity = sensitivity;
        DeltaTime = deltaTime;
        IsSubtracting = isSubtracting;
    }
}

public delegate void FlattenHeightmap(in FlattenHeightmapProperties properties);

public readonly struct FlattenHeightmapProperties
{
    public readonly Bounds Bounds;
    public readonly Vector3 Position;
    public readonly EDevkitLandscapeToolHeightmapFlattenMethod FlattenMethod;
    public readonly float Radius;
    public readonly float Falloff;
    public readonly float Strength;
    public readonly float Sensitivity;
    public readonly float Target;
    public readonly float DeltaTime;
    public FlattenHeightmapProperties(Bounds bounds, Vector3 position, EDevkitLandscapeToolHeightmapFlattenMethod flattenMethod, float radius, float falloff, float strength, float sensitivity, float target, float deltaTime)
    {
        Bounds = bounds;
        Position = position;
        FlattenMethod = flattenMethod;
        Radius = radius;
        Falloff = falloff;
        Strength = strength;
        Sensitivity = sensitivity;
        Target = target;
        DeltaTime = deltaTime;
    }
}

public delegate void SmoothHeightmap(in SmoothHeightmapProperties properties);
public readonly struct SmoothHeightmapProperties
{
    public readonly Bounds Bounds;
    public readonly Vector3 Position;
    public readonly EDevkitLandscapeToolHeightmapSmoothMethod SmoothMethod;
    public readonly float Radius;
    public readonly float Falloff;
    public readonly float Strength;
    public readonly float Target;
    public readonly float DeltaTime;
    public SmoothHeightmapProperties(Bounds bounds, Vector3 position, EDevkitLandscapeToolHeightmapSmoothMethod smoothMethod, float radius, float falloff, float strength, float target, float deltaTime)
    {
        Bounds = bounds;
        Position = position;
        SmoothMethod = smoothMethod;
        Radius = radius;
        Falloff = falloff;
        Strength = strength;
        Target = target;
        DeltaTime = deltaTime;
    }
}

public delegate void PaintSplatmap(in PaintSplatmapProperties properties);
public readonly struct PaintSplatmapProperties
{
    public readonly Bounds Bounds;
    public readonly Vector3 Position;
    public readonly float Radius;
    public readonly float Falloff;
    public readonly float Strength;
    public readonly float Sensitivity;
    public readonly float Target;
    public readonly float DeltaTime;
    public readonly bool UseWeightTarget;
    public readonly bool UseAutoSlope;
    public readonly bool UseAutoFoundation;
    public readonly bool IsRemoving;
    public readonly AutoSlopeProperties AutoSlopeProperties;
    public readonly AutoFoundationProperties AutoFoundationProperties;
    public readonly AssetReference<LandscapeMaterialAsset> Material;
    public PaintSplatmapProperties(Bounds bounds, Vector3 position, float radius, float falloff, float strength, float sensitivity, float target, float deltaTime, bool useWeightTarget, bool useAutoSlope, bool useAutoFoundation, bool isRemoving, AutoSlopeProperties autoSlopeProperties, AutoFoundationProperties autoFoundationProperties, AssetReference<LandscapeMaterialAsset> material)
    {
        Bounds = bounds;
        Position = position;
        Radius = radius;
        Falloff = falloff;
        Strength = strength;
        Sensitivity = sensitivity;
        Target = target;
        DeltaTime = deltaTime;
        UseWeightTarget = useWeightTarget;
        UseAutoSlope = useAutoSlope;
        UseAutoFoundation = useAutoFoundation;
        IsRemoving = isRemoving;
        AutoSlopeProperties = autoSlopeProperties;
        AutoFoundationProperties = autoFoundationProperties;
        Material = material;
    }
}

public delegate void SmoothSplatmap(in SmoothSplatmapProperties properties);
public readonly struct SmoothSplatmapProperties
{
    public readonly Bounds Bounds;
    public readonly Vector3 Position;
    public readonly EDevkitLandscapeToolSplatmapSmoothMethod SmoothMethod;
    public readonly float Radius;
    public readonly float Falloff;
    public readonly float Strength;
    public readonly float DeltaTime;
    public SmoothSplatmapProperties(Bounds bounds, Vector3 position, EDevkitLandscapeToolSplatmapSmoothMethod smoothMethod, float radius, float falloff, float strength, float deltaTime)
    {
        Bounds = bounds;
        Position = position;
        SmoothMethod = smoothMethod;
        Radius = radius;
        Falloff = falloff;
        Strength = strength;
        DeltaTime = deltaTime;
    }
}

public delegate void MoveHierarchyObjectFinal(in MoveHierarchyObjectFinalProperties properties);
public readonly struct MoveHierarchyObjectFinalProperties
{
    public readonly DevkitSelection Selection;
    public readonly IDevkitHierarchyItem Item;
    public readonly FinalTransformation Transformation;
    public readonly bool UseScale;
    public readonly float DeltaTime;
    public MoveHierarchyObjectFinalProperties(DevkitSelection selection, IDevkitHierarchyItem item, FinalTransformation transformation, bool useScale, float deltaTime)
    {
        Selection = selection;
        Item = item;
        Transformation = transformation;
        UseScale = useScale;
        DeltaTime = deltaTime;
    }
}

public delegate void MoveHierarchyObjectsFinal(in MoveHierarchyObjectsFinalProperties properties);
public readonly struct MoveHierarchyObjectsFinalProperties
{
    public readonly FinalTransformation[] Transformations;
    public readonly bool UseScale;
    public readonly float DeltaTime;
    public MoveHierarchyObjectsFinalProperties(FinalTransformation[] transformations, bool useScale, float deltaTime)
    {
        Transformations = transformations;
        UseScale = useScale;
        DeltaTime = deltaTime;
    }
}

public delegate void DeleteHierarchyObject(in DeleteHierarchyObjectProperties properties);
public readonly struct DeleteHierarchyObjectProperties
{
    public readonly GameObject GameObject;
    public readonly IDevkitHierarchyItem Item;
    public readonly NetId NetId;
    public DeleteHierarchyObjectProperties(GameObject gameObject, IDevkitHierarchyItem item, NetId netId)
    {
        GameObject = gameObject;
        Item = item;
        NetId = netId;
    }
}

public delegate void DeleteHierarchyObjects(in DeleteHierarchyObjectsProperties properties);
public readonly struct DeleteHierarchyObjectsProperties
{
    public readonly NetId[] NetIds;
    public readonly float DeltaTime;
    public DeleteHierarchyObjectsProperties(NetId[] netIds, float deltaTime)
    {
        NetIds = netIds;
        DeltaTime = deltaTime;
    }
}

public delegate void RequestInstantiateHierarchyObject(in InstantiateHierarchyObjectProperties properties);
public readonly struct InstantiateHierarchyObjectProperties
{
    public readonly IHierarchyItemTypeIdentifier Type;
    public readonly Vector3 Position;
    public InstantiateHierarchyObjectProperties(IHierarchyItemTypeIdentifier type, Vector3 position)
    {
        Type = type;
        Position = position;
    }
}

public delegate void DeleteLevelObject(in DeleteLevelObjectProperties properties);
public readonly struct DeleteLevelObjectProperties
{
    public readonly NetId NetId;
    public readonly float DeltaTime;
    public DeleteLevelObjectProperties(NetId netId, float deltaTime)
    {
        NetId = netId;
        DeltaTime = deltaTime;
    }
}

public delegate void DeleteLevelObjects(in DeleteLevelObjectsProperties properties);
public readonly struct DeleteLevelObjectsProperties
{
    public readonly NetId[] NetIds;
    public readonly float DeltaTime;
    public DeleteLevelObjectsProperties(NetId[] netIds, float deltaTime)
    {
        NetIds = netIds;
        DeltaTime = deltaTime;
    }
}

public delegate void MoveLevelObjectFinal(in MoveLevelObjectFinalProperties properties);
public readonly struct MoveLevelObjectFinalProperties
{
    public readonly FinalTransformation Transformation;
    public readonly bool UseScale;
    public readonly float DeltaTime;
    public MoveLevelObjectFinalProperties(FinalTransformation transformation, bool useScale, float deltaTime)
    {
        Transformation = transformation;
        UseScale = useScale;
        DeltaTime = deltaTime;
    }
}

public delegate void MoveLevelObjectsFinal(in MoveLevelObjectsFinalProperties properties);
public readonly struct MoveLevelObjectsFinalProperties
{
    public readonly FinalTransformation[] Transformations;
    public readonly bool UseScale;
    public readonly float DeltaTime;
    public MoveLevelObjectsFinalProperties(FinalTransformation[] transformations, bool useScale, float deltaTime)
    {
        Transformations = transformations;
        UseScale = useScale;
        DeltaTime = deltaTime;
    }
}

public delegate void UpdateObjectsCustomMaterialPaletteOverride(in UpdateObjectsCustomMaterialPaletteOverrideProperties properties);
public readonly struct UpdateObjectsCustomMaterialPaletteOverrideProperties
{
    public readonly NetId[] NetIds;
    public readonly AssetReference<MaterialPaletteAsset> Material;
    public readonly float DeltaTime;
    public UpdateObjectsCustomMaterialPaletteOverrideProperties(NetId[] netIds, AssetReference<MaterialPaletteAsset> material, float deltaTime)
    {
        NetIds = netIds;
        Material = material;
        DeltaTime = deltaTime;
    }
}

public delegate void UpdateObjectsMaterialIndexOverride(in UpdateObjectsMaterialIndexOverrideProperties properties);
public readonly struct UpdateObjectsMaterialIndexOverrideProperties
{
    public readonly NetId[] NetIds;
    public readonly int Index;
    public readonly float DeltaTime;
    public UpdateObjectsMaterialIndexOverrideProperties(NetId[] netIds, int index, float deltaTime)
    {
        NetIds = netIds;
        Index = index;
        DeltaTime = deltaTime;
    }
}

public delegate void MoveRoadVertex(in MoveRoadVertexProperties properties);
public delegate void MoveRoadVertexRequested(in MoveRoadVertexProperties properties, ref bool shouldAllow);
public readonly struct MoveRoadVertexProperties
{
    public readonly NetId RoadNetId;
    public readonly NetId VertexNetId;
    public readonly Vector3 Position;
    public readonly Vector3 OldPosition;
    public readonly float DeltaTime;
    public MoveRoadVertexProperties(NetId roadNetId, NetId vertexNetId, Vector3 oldPosition, Vector3 position, float deltaTime)
    {
        RoadNetId = roadNetId;
        VertexNetId = vertexNetId;
        Position = position;
        OldPosition = oldPosition;
        DeltaTime = deltaTime;
    }
}

public delegate void MoveRoadTangentHandle(in MoveRoadTangentHandleProperties properties);
public delegate void MoveRoadTangentHandleRequested(in MoveRoadTangentHandleProperties properties, ref bool shouldAllow);
public readonly struct MoveRoadTangentHandleProperties
{
    public readonly NetId RoadNetId;
    public readonly NetId VertexNetId;
    public readonly TangentHandle Handle;
    public readonly Vector3 Position;
    public readonly Vector3 OldPosition;
    public readonly float DeltaTime;
    public MoveRoadTangentHandleProperties(NetId roadNetId, NetId vertexNetId, Vector3 oldPosition, TangentHandle handle, Vector3 position, float deltaTime)
    {
        RoadNetId = roadNetId;
        VertexNetId = vertexNetId;
        Handle = handle;
        Position = position;
        OldPosition = oldPosition;
        DeltaTime = deltaTime;
    }
}

public delegate void DeleteRoadVertex(in DeleteRoadVertexProperties properties);
public delegate void DeleteRoadVertexRequested(in DeleteRoadVertexProperties properties, ref bool shouldAllow);
public readonly struct DeleteRoadVertexProperties
{
    public readonly NetId RoadNetId;
    public readonly NetId VertexNetId;
    public readonly Vector3 OldPosition;
    public readonly float DeltaTime;
    public DeleteRoadVertexProperties(NetId roadNetId, NetId vertexNetId, Vector3 oldPosition, float deltaTime)
    {
        RoadNetId = roadNetId;
        VertexNetId = vertexNetId;
        OldPosition = oldPosition;
        DeltaTime = deltaTime;
    }
}

public delegate void DeleteRoad(in DeleteRoadProperties properties);
public delegate void DeleteRoadRequested(in DeleteRoadProperties properties, ref bool shouldAllow);
public readonly struct DeleteRoadProperties
{
    public readonly NetId RoadNetId;
    public readonly Vector3 OldPosition;
    public readonly float DeltaTime;
    public DeleteRoadProperties(NetId roadNetId, Vector3 oldPosition, float deltaTime)
    {
        RoadNetId = roadNetId;
        OldPosition = oldPosition;
        DeltaTime = deltaTime;
    }
}

public delegate void RequestInstantiateRoad(in RequestInstantiateRoadProperties properties);
public delegate void RequestInstantiateRoadRequested(in RequestInstantiateRoadProperties properties, ref bool shouldAllow);
public readonly struct RequestInstantiateRoadProperties
{
    public readonly Vector3 FirstVertexPosition;
    public readonly byte MaterialIndex;
    public RequestInstantiateRoadProperties(Vector3 firstVertexPosition, byte materialIndex)
    {
        FirstVertexPosition = firstVertexPosition;
        MaterialIndex = materialIndex;
    }
}

public delegate void RequestInstantiateRoadVertex(in RequestInstantiateRoadVertexProperties properties);
public delegate void RequestInstantiateRoadVertexRequested(in RequestInstantiateRoadVertexProperties properties, ref bool shouldAllow);
public readonly struct RequestInstantiateRoadVertexProperties
{
    public readonly NetId RoadNetId;
    public readonly Vector3 VertexPosition;
    public readonly int VertexIndex;
    public RequestInstantiateRoadVertexProperties(NetId roadNetId, Vector3 vertexPosition, int vertexIndex)
    {
        RoadNetId = roadNetId;
        VertexPosition = vertexPosition;
        VertexIndex = vertexIndex;
    }
}

public delegate void SetRoadMaterialVerticalOffset(in SetRoadMaterialVerticalOffsetProperties properties);
public delegate void SetRoadMaterialVerticalOffsetRequested(in SetRoadMaterialVerticalOffsetProperties properties, ref bool shouldAllow);
public readonly struct SetRoadMaterialVerticalOffsetProperties
{
    public readonly byte MaterialIndex;
    public readonly float OldVerticalOffset;
    public readonly float VerticalOffset;
    public readonly float DeltaTime;
    public SetRoadMaterialVerticalOffsetProperties(byte materialIndex, float verticalOffset, float oldVerticalOffset, float deltaTime)
    {
        MaterialIndex = materialIndex;
        VerticalOffset = verticalOffset;
        OldVerticalOffset = oldVerticalOffset;
        DeltaTime = deltaTime;
    }
}

public delegate void SetRoadMaterialHeight(in SetRoadMaterialHeightProperties properties);
public delegate void SetRoadMaterialHeightRequested(in SetRoadMaterialHeightProperties properties, ref bool shouldAllow);
public readonly struct SetRoadMaterialHeightProperties
{
    public readonly byte MaterialIndex;
    public readonly float OldHeight;
    public readonly float Height;
    public readonly float DeltaTime;
    public SetRoadMaterialHeightProperties(byte materialIndex, float height, float oldHeight, float deltaTime)
    {
        MaterialIndex = materialIndex;
        Height = height;
        OldHeight = oldHeight;
        DeltaTime = deltaTime;
    }
}

public delegate void SetRoadMaterialWidth(in SetRoadMaterialWidthProperties properties);
public delegate void SetRoadMaterialWidthRequested(in SetRoadMaterialWidthProperties properties, ref bool shouldAllow);
public readonly struct SetRoadMaterialWidthProperties
{
    public readonly byte MaterialIndex;
    public readonly float OldWidth;
    public readonly float Width;
    public readonly float DeltaTime;
    public SetRoadMaterialWidthProperties(byte materialIndex, float width, float oldWidth, float deltaTime)
    {
        MaterialIndex = materialIndex;
        Width = width;
        OldWidth = oldWidth;
        DeltaTime = deltaTime;
    }
}

public delegate void SetRoadMaterialDepth(in SetRoadMaterialDepthProperties properties);
public delegate void SetRoadMaterialDepthRequested(in SetRoadMaterialDepthProperties properties, ref bool shouldAllow);
public readonly struct SetRoadMaterialDepthProperties
{
    public readonly byte MaterialIndex;
    public readonly float OldDepth;
    public readonly float Depth;
    public readonly float DeltaTime;
    public SetRoadMaterialDepthProperties(byte materialIndex, float depth, float oldDepth, float deltaTime)
    {
        MaterialIndex = materialIndex;
        Depth = depth;
        OldDepth = oldDepth;
        DeltaTime = deltaTime;
    }
}

public delegate void SetRoadMaterialIsConcrete(in SetRoadMaterialIsConcreteProperties properties);
public delegate void SetRoadMaterialIsConcreteRequested(in SetRoadMaterialIsConcreteProperties properties, ref bool shouldAllow);
public readonly struct SetRoadMaterialIsConcreteProperties
{
    public readonly byte MaterialIndex;
    public readonly bool IsConcrete;
    public readonly float DeltaTime;
    public SetRoadMaterialIsConcreteProperties(byte materialIndex, bool isConcrete, float deltaTime)
    {
        MaterialIndex = materialIndex;
        IsConcrete = isConcrete;
        DeltaTime = deltaTime;
    }
}

public delegate void SetRoadIsLoop(in SetRoadIsLoopProperties properties);
public delegate void SetRoadIsLoopRequested(in SetRoadIsLoopProperties properties, ref bool shouldAllow);
public readonly struct SetRoadIsLoopProperties
{
    public readonly NetId RoadNetId;
    public readonly bool IsLoop;
    public readonly float DeltaTime;
    public SetRoadIsLoopProperties(NetId roadNetId, bool isLoop, float deltaTime)
    {
        RoadNetId = roadNetId;
        IsLoop = isLoop;
        DeltaTime = deltaTime;
    }
}

public delegate void SetRoadVertexIgnoreTerrain(in SetRoadVertexIgnoreTerrainProperties properties);
public delegate void SetRoadVertexIgnoreTerrainRequested(in SetRoadVertexIgnoreTerrainProperties properties, ref bool shouldAllow);
public readonly struct SetRoadVertexIgnoreTerrainProperties
{
    public readonly NetId RoadNetId;
    public readonly NetId VertexNetId;
    public readonly bool IgnoreTerrain;
    public readonly float DeltaTime;
    public SetRoadVertexIgnoreTerrainProperties(NetId roadNetId, NetId vertexNetId, bool ignoreTerrain, float deltaTime)
    {
        RoadNetId = roadNetId;
        VertexNetId = vertexNetId;
        IgnoreTerrain = ignoreTerrain;
        DeltaTime = deltaTime;
    }
}

public delegate void SetRoadVertexVerticalOffset(in SetRoadVertexVerticalOffsetProperties properties);
public delegate void SetRoadVertexVerticalOffsetRequested(in SetRoadVertexVerticalOffsetProperties properties, ref bool shouldAllow);
public readonly struct SetRoadVertexVerticalOffsetProperties
{
    public readonly NetId RoadNetId;
    public readonly NetId VertexNetId;
    public readonly float VerticalOffset;
    public readonly float OldVerticalOffset;
    public readonly float DeltaTime;
    public SetRoadVertexVerticalOffsetProperties(NetId roadNetId, NetId vertexNetId, float verticalOffset, float oldVerticalOffset, float deltaTime)
    {
        RoadNetId = roadNetId;
        VertexNetId = vertexNetId;
        VerticalOffset = verticalOffset;
        OldVerticalOffset = oldVerticalOffset;
        DeltaTime = deltaTime;
    }
}

public delegate void SetRoadVertexTangentHandleMode(in SetRoadVertexTangentHandleModeProperties properties);
public delegate void SetRoadVertexTangentHandleModeRequested(in SetRoadVertexTangentHandleModeProperties properties, ref bool shouldAllow);
public readonly struct SetRoadVertexTangentHandleModeProperties
{
    public readonly NetId RoadNetId;
    public readonly NetId VertexNetId;
    public readonly ERoadMode TangentHandleMode;
    public readonly ERoadMode OldTangentHandleMode;
    public readonly float DeltaTime;
    public SetRoadVertexTangentHandleModeProperties(NetId roadNetId, NetId vertexNetId, ERoadMode tangentHandleMode, ERoadMode oldTangentHandleMode, float deltaTime)
    {
        RoadNetId = roadNetId;
        VertexNetId = vertexNetId;
        TangentHandleMode = tangentHandleMode;
        OldTangentHandleMode = oldTangentHandleMode;
        DeltaTime = deltaTime;
    }
}

public delegate void SetRoadMaterial(in SetRoadMaterialProperties properties);
public delegate void SetRoadMaterialRequested(in SetRoadMaterialProperties properties, ref bool shouldAllow);
public readonly struct SetRoadMaterialProperties
{
    public readonly NetId RoadNetId;
    public readonly byte MaterialIndex;
    public readonly float DeltaTime;
    public SetRoadMaterialProperties(NetId roadNetId, byte materialIndex, float deltaTime)
    {
        RoadNetId = roadNetId;
        MaterialIndex = materialIndex;
        DeltaTime = deltaTime;
    }
}

public delegate void RequestInstantiateNavigation(in RequestInstantiateNavigationProperties properties);
public delegate void RequestInstantiateNavigationRequested(in RequestInstantiateNavigationProperties properties, ref bool shouldAllow);
public readonly struct RequestInstantiateNavigationProperties
{
    public readonly Vector3 Position;
    public RequestInstantiateNavigationProperties(Vector3 position)
    {
        Position = position;
    }
}

public delegate void DeleteNavigation(in DeleteNavigationProperties properties);
public delegate void DeleteNavigationRequested(in DeleteNavigationProperties properties, ref bool shouldAllow);
public readonly struct DeleteNavigationProperties
{
    public readonly NetId NavigationNetId;
    public readonly Vector3 OldPosition;
    public readonly float DeltaTime;
    public DeleteNavigationProperties(NetId navigationNetId, Vector3 oldPosition, float deltaTime)
    {
        NavigationNetId = navigationNetId;
        OldPosition = oldPosition;
        DeltaTime = deltaTime;
    }
}

public delegate void MoveNavigation(in MoveNavigationProperties properties);
public delegate void MoveNavigationRequested(in MoveNavigationProperties properties, ref bool shouldAllow);
public readonly struct MoveNavigationProperties
{
    public readonly NetId NavigationNetId;
    public readonly Vector3 Position;
    public readonly Vector3 OldPosition;
    public readonly float DeltaTime;
    public MoveNavigationProperties(NetId navigationNetId, Vector3 position, Vector3 oldPosition, float deltaTime)
    {
        NavigationNetId = navigationNetId;
        Position = position;
        OldPosition = oldPosition;
        DeltaTime = deltaTime;
    }
}

public delegate void SetNavigationSize(in SetNavigationSizeProperties properties);
public delegate void SetNavigationSizeRequested(in SetNavigationSizeProperties properties, ref bool shouldAllow);
public readonly struct SetNavigationSizeProperties
{
    public readonly NetId NavigationNetId;
    public readonly Vector2 Size;
    public readonly Vector2 OldSize;
    public readonly float DeltaTime;
    public SetNavigationSizeProperties(NetId navigationNetId, Vector2 size, Vector2 oldSize, float deltaTime)
    {
        NavigationNetId = navigationNetId;
        Size = size;
        OldSize = oldSize;
        DeltaTime = deltaTime;
    }
}

public delegate void SetNavigationDifficulty(in SetNavigationDifficultyProperties properties);
public delegate void SetNavigationDifficultyRequested(in SetNavigationDifficultyProperties properties, ref bool shouldAllow);
public readonly struct SetNavigationDifficultyProperties
{
    public readonly NetId NavigationNetId;
    public readonly AssetReference<ZombieDifficultyAsset> Difficulty;
    public readonly AssetReference<ZombieDifficultyAsset> OldDifficulty;
    public readonly float DeltaTime;
    public SetNavigationDifficultyProperties(NetId navigationNetId, AssetReference<ZombieDifficultyAsset> difficulty, AssetReference<ZombieDifficultyAsset> oldDifficulty, float deltaTime)
    {
        NavigationNetId = navigationNetId;
        Difficulty = difficulty;
        OldDifficulty = oldDifficulty;
        DeltaTime = deltaTime;
    }
}

public delegate void SetNavigationMaximumZombies(in SetNavigationMaximumZombiesProperties properties);
public delegate void SetNavigationMaximumZombiesRequested(in SetNavigationMaximumZombiesProperties properties, ref bool shouldAllow);
public readonly struct SetNavigationMaximumZombiesProperties
{
    public readonly NetId NavigationNetId;
    public readonly byte MaximumZombies;
    public readonly byte OldMaximumZombies;
    public readonly float DeltaTime;
    public SetNavigationMaximumZombiesProperties(NetId navigationNetId, byte maximumZombies, byte oldMaximumZombies, float deltaTime)
    {
        NavigationNetId = navigationNetId;
        MaximumZombies = maximumZombies;
        OldMaximumZombies = oldMaximumZombies;
        DeltaTime = deltaTime;
    }
}

public delegate void SetNavigationMaximumBossZombies(in SetNavigationMaximumBossZombiesProperties properties);
public delegate void SetNavigationMaximumBossZombiesRequested(in SetNavigationMaximumBossZombiesProperties properties, ref bool shouldAllow);
public readonly struct SetNavigationMaximumBossZombiesProperties
{
    public readonly NetId NavigationNetId;
    public readonly int MaximumBossZombies;
    public readonly int OldMaximumBossZombies;
    public readonly float DeltaTime;
    public SetNavigationMaximumBossZombiesProperties(NetId navigationNetId, int maximumBossZombies, int oldMaximumBossZombies, float deltaTime)
    {
        NavigationNetId = navigationNetId;
        MaximumBossZombies = maximumBossZombies;
        OldMaximumBossZombies = oldMaximumBossZombies;
        DeltaTime = deltaTime;
    }
}

public delegate void SetNavigationShouldSpawnZombies(in SetNavigationShouldSpawnZombiesProperties properties);
public delegate void SetNavigationShouldSpawnZombiesRequested(in SetNavigationShouldSpawnZombiesProperties properties, ref bool shouldAllow);
public readonly struct SetNavigationShouldSpawnZombiesProperties
{
    public readonly NetId NavigationNetId;
    public readonly bool ShouldSpawnZombies;
    public readonly float DeltaTime;
    public SetNavigationShouldSpawnZombiesProperties(NetId navigationNetId, bool shouldSpawnZombies, float deltaTime)
    {
        NavigationNetId = navigationNetId;
        ShouldSpawnZombies = shouldSpawnZombies;
        DeltaTime = deltaTime;
    }
}

public delegate void SetNavigationInfiniteAgroDistance(in SetNavigationInfiniteAgroDistanceProperties properties);
public delegate void SetNavigationInfiniteAgroDistanceRequested(in SetNavigationInfiniteAgroDistanceProperties properties, ref bool shouldAllow);
public readonly struct SetNavigationInfiniteAgroDistanceProperties
{
    public readonly NetId NavigationNetId;
    public readonly bool InfiniteAgroDistance;
    public readonly float DeltaTime;
    public SetNavigationInfiniteAgroDistanceProperties(NetId navigationNetId, bool infiniteAgroDistance, float deltaTime)
    {
        NavigationNetId = navigationNetId;
        InfiniteAgroDistance = infiniteAgroDistance;
        DeltaTime = deltaTime;
    }
}

public delegate void MoveSpawnFinal(in MoveSpawnFinalProperties properties);
public readonly struct MoveSpawnFinalProperties
{
    public readonly NetId SpawnNetId;
    public readonly TransformationDelta Transformation;
    public readonly bool UseRotation;
    public readonly float DeltaTime;
    public MoveSpawnFinalProperties(NetId spawnNetId, TransformationDelta transformation, bool useRotation, float deltaTime)
    {
        SpawnNetId = spawnNetId;
        Transformation = transformation;
        UseRotation = useRotation;
        DeltaTime = deltaTime;
    }
}

public delegate void MoveSpawnsFinal(in MoveSpawnsFinalProperties properties);
public readonly struct MoveSpawnsFinalProperties
{
    public readonly NetId[] SpawnNetIds;
    public readonly TransformationDelta[] Transformations;
    public readonly bool UseRotation;
    public readonly float DeltaTime;
    public MoveSpawnsFinalProperties(NetId[] spawnNetIds, TransformationDelta[] transformations, bool useRotation, float deltaTime)
    {
        SpawnNetIds = spawnNetIds;
        Transformations = transformations;
        UseRotation = useRotation;
        DeltaTime = deltaTime;
    }
}

public delegate void DeleteSpawns(in DeleteSpawnsProperties properties);
public readonly struct DeleteSpawnsProperties
{
    public readonly NetId[] SpawnNetIds;
    public readonly float DeltaTime;
    public DeleteSpawnsProperties(NetId[] spawnNetIds, float deltaTime)
    {
        SpawnNetIds = spawnNetIds;
        DeltaTime = deltaTime;
    }
}