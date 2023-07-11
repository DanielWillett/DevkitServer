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

public delegate void MoveHierarchyObjectPreview(in MoveHierarchyObjectPreviewProperties properties);
public readonly struct MoveHierarchyObjectPreviewProperties
{
    public readonly DevkitSelection Selection;
    public readonly IDevkitHierarchyItem Item;
    public readonly TransformationDelta Transformation;
    public readonly Vector3 PivotPoint;
    public readonly float DeltaTime;
    public MoveHierarchyObjectPreviewProperties(DevkitSelection selection, IDevkitHierarchyItem item, TransformationDelta transformation, Vector3 pivotPoint, float deltaTime)
    {
        Selection = selection;
        Item = item;
        Transformation = transformation;
        PivotPoint = pivotPoint;
        DeltaTime = deltaTime;
    }
}

public delegate void MoveHierarchyObjectsPreview(in MoveHierarchyObjectsPreviewProperties properties);
public readonly struct MoveHierarchyObjectsPreviewProperties
{
    public readonly uint[] InstanceIds;
    public readonly TransformationDelta[] Transformations;
    public readonly Vector3 PivotPoint;
    public readonly float DeltaTime;
    public MoveHierarchyObjectsPreviewProperties(uint[] instanceIds, TransformationDelta[] transformations, Vector3 pivotPoint, float deltaTime)
    {
        InstanceIds = instanceIds;
        Transformations = transformations;
        PivotPoint = pivotPoint;
        DeltaTime = deltaTime;
    }
}

public delegate void MoveHierarchyObjectFinal(in MoveHierarchyObjectFinalProperties properties);
public readonly struct MoveHierarchyObjectFinalProperties
{
    public readonly DevkitSelection Selection;
    public readonly IDevkitHierarchyItem Item;
    public readonly TransformationDelta Transformation;
    public readonly Vector3 Scale;
    public readonly Vector3 OriginalScale;
    public readonly bool UseScale;
    public readonly float DeltaTime;
    public MoveHierarchyObjectFinalProperties(DevkitSelection selection, IDevkitHierarchyItem item, TransformationDelta transformation, Vector3 scale, Vector3 originalScale, bool useScale, float deltaTime)
    {
        Selection = selection;
        Item = item;
        Transformation = transformation;
        Scale = scale;
        OriginalScale = originalScale;
        UseScale = useScale;
        DeltaTime = deltaTime;
    }
}

public delegate void MoveHierarchyObjectsFinal(in MoveHierarchyObjectsFinalProperties properties);
public readonly struct MoveHierarchyObjectsFinalProperties
{
    public readonly uint[] InstanceIds;
    public readonly TransformationDelta[] Transformations;
    public readonly Vector3[]? Scales;
    public readonly Vector3[]? OriginalScales;
    public readonly float DeltaTime;
    public MoveHierarchyObjectsFinalProperties(uint[] instanceIds, TransformationDelta[] transformations, Vector3[]? scales, Vector3[]? originalScales, float deltaTime)
    {
        InstanceIds = instanceIds;
        Transformations = transformations;
        Scales = scales;
        OriginalScales = originalScales;
        DeltaTime = deltaTime;
    }
}

public delegate void DeleteHierarchyObject(in DeleteHierarchyObjectProperties properties);
public readonly struct DeleteHierarchyObjectProperties
{
    public readonly GameObject GameObject;
    public readonly IDevkitHierarchyItem Item;
    public DeleteHierarchyObjectProperties(GameObject gameObject, IDevkitHierarchyItem item)
    {
        GameObject = gameObject;
        Item = item;
    }
}

public delegate void DeleteHierarchyObjects(in DeleteHierarchyObjectsProperties properties);
public readonly struct DeleteHierarchyObjectsProperties
{
    public readonly uint[] InstanceIds;
    public readonly float DeltaTime;
    public DeleteHierarchyObjectsProperties(uint[] instanceIds, float deltaTime)
    {
        InstanceIds = instanceIds;
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

public delegate void MoveLevelObjectsPreview(in MoveLevelObjectsPreviewProperties properties);
public readonly struct MoveLevelObjectsPreviewProperties
{
    public readonly PreviewTransformation[] Transformations;
    public readonly float DeltaTime;
    public MoveLevelObjectsPreviewProperties(PreviewTransformation[] transformations, float deltaTime)
    {
        Transformations = transformations;
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