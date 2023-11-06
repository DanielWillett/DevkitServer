using System.Text.Json.Serialization;
using DevkitServer.Util.Encoding;

namespace DevkitServer.Multiplayer.Actions;

[Flags]
public enum ActionSetting
{
    None = 0,
    Radius = 1 << 0,
    Falloff = 1 << 1,
    Strength = 1 << 2,
    Sensitivity = 1 << 3,
    Target = 1 << 4,
    Asset = 1 << 5,
    TileCoordinates = 1 << 6,
    AutoSlopeData = 1 << 7,
    AutoFoundationData = 1 << 8,
    InstanceId = 1 << 9,

    Extended = 1 << 31
}

public enum DevkitServerActionType : byte
{
    /* Terrain */
    HeightmapRamp = 0,
    HeightmapAdjust = 1,
    HeightmapFlatten = 2,
    HeightmapSmooth = 3,
    SplatmapPaint = 4,
    SplatmapAutoPaint = 5,
    SplatmapSmooth = 6,
    HolesCut = 7,

    /* Tiles */
    AddTile = 8,
    DeleteTile = 9,
    UpdateSplatmapLayers = 10,

    /* Foliage */
    AddFoliageToSurface = 11,
    RemoveFoliageInstances = 12,
    RemoveResourceSpawnpoint = 13,

    /* Hierarchy Items */
    MoveHierarchyItems = 14,
    DeleteHierarchyItems = 15,

    /* Level Objects / Buildables */
    DeleteLevelObjects = 16,
    MoveLevelObjectsFinal = 17,
    InstantiateLevelObject = 18,
    UpdateObjectsCustomMaterialPaletteOverride = 19,
    UpdateObjectsMaterialIndexOverride = 20,

    /* Roads */
    MoveRoadVertex = 21,
    MoveRoadTangentHandle = 22,
    DeleteRoadVertex = 23,
    DeleteRoad = 24,
    SetRoadIsLoop = 25,
    SetRoadMaterial = 26,
    SetRoadVertexIgnoreTerrain = 27,
    SetRoadVertexVerticalOffset = 28,
    SetRoadVertexTangentHandleMode = 29,
    SetRoadMaterialWidth = 30,
    SetRoadMaterialHeight = 31,
    SetRoadMaterialDepth = 32,
    SetRoadMaterialVerticalOffset = 33,
    SetRoadMaterialIsConcrete = 34,

    /* Navigation Flags */
    MoveNavigation = 35,
    DeleteNavigation = 36,
    SetNavigationSize = 37,
    SetNavigationDifficulty = 38,
    SetNavigationMaximumZombies = 39,
    SetNavigationMaximumBossZombies = 40,
    SetNavigationShouldSpawnZombies = 41,
    SetNavigationInfiniteAgroDistance = 42,

    /* For future use */
    Extended = 255
}
public interface IReflectableAction : IAction
{
    void SendUndo(ITransportConnection sender);
}
public interface IServersideAction : IAction
{

}
public interface IReplacableAction : IAction
{
    bool TryReplaceFrom(IReplacableAction action);
}
public interface IAction
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    DevkitServerActionType Type { get; }
    float DeltaTime { get; set; }
    CSteamID Instigator { get; set; }
    void Apply();
#if SERVER
    bool CheckCanApply();
#endif
    void Read(ByteReader reader);
    void Write(ByteWriter writer);
    int CalculateSize();
}

[ActionSetting(ActionSetting.Asset)]
public interface IAssetAction
{
    Guid Asset { get; set; }
}
[ActionSetting(ActionSetting.TileCoordinates)]
public interface ICoordinatesAction
{
    int CoordinateX { get; set; }
    int CoordinateY { get; set; }
}
[ActionSetting(ActionSetting.Radius)]
public interface IBrushRadiusAction
{
    float BrushRadius { get; set; }
}
[ActionSetting(ActionSetting.Falloff)]
public interface IBrushFalloffAction
{
    float BrushFalloff { get; set; }
}
[ActionSetting(ActionSetting.Strength)]
public interface IBrushStrengthAction
{
    float BrushStrength { get; set; }
}
[ActionSetting(ActionSetting.InstanceId)]
public interface IInstanceIdAction
{
    uint InstanceId { get; set; }
}
[ActionSetting(ActionSetting.Sensitivity)]
public interface IBrushSensitivityAction
{
    float BrushSensitivity { get; set; }
}
[ActionSetting(ActionSetting.Target)]
public interface IBrushTargetAction
{
    float BrushTarget { get; set; }
}