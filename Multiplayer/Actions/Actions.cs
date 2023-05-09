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
    AutoSlopeMinAngleBegin = 1 << 7,
    AutoSlopeMinAngleEnd = 1 << 8,
    AutoSlopeMaxAngleBegin = 1 << 9,
    AutoSlopeMaxAngleEnd = 1 << 10,
    AutoFoundationRayLength = 1 << 11,
    AutoFoundationRayRadius = 1 << 12,
    AutoFoundationRayMask = 1 << 13,

    Extended = 1 << 31
}

public enum ActionType : byte
{
    HeightmapRamp,
    HeightmapAdjust,
    HeightmapFlatten,
    HeightmapSmooth,
    SplatmapPaint,
    SplatmapAutoPaint,
    SplatmapSmooth,
    HolesCut,
    AddTile,
    DeleteTile,
    UpdateSplatmapLayers,
    AddFoliageToSurface,
    RemoveFoliageInstances,
    RemoveResourceSpawnpoint,
    RemoveFoliageLevelObject,
    RemoveLevelObject,

    // for future use
    Extended = 255
}

public interface IAction
{
    ActionType Type { get; }
    float DeltaTime { get; set; }
    CSteamID Instigator { get; set; }
    void Apply();
#if SERVER
    bool CheckCanApply();
#endif
    void Read(ByteReader reader);
    void Write(ByteWriter writer);
}

public interface IAsset
{
    [ActionSetting(ActionSetting.Asset)]
    Guid Asset { get; set; }
}
public interface ICoordinates
{
    [ActionSetting(ActionSetting.TileCoordinates)]
    int CoordinateX { get; set; }
    [ActionSetting(ActionSetting.TileCoordinates)]
    int CoordinateY { get; set; }
}
public interface IBrushRadius
{
    [ActionSetting(ActionSetting.Radius)]
    float BrushRadius { get; set; }
}
public interface IBrushFalloff
{
    [ActionSetting(ActionSetting.Falloff)]
    float BrushFalloff { get; set; }
}
public interface IBrushStrength
{
    [ActionSetting(ActionSetting.Strength)]
    float BrushStrength { get; set; }
}
public interface IBrushSensitivity
{
    [ActionSetting(ActionSetting.Sensitivity)]
    float BrushSensitivity { get; set; }
}
public interface IBrushTarget
{
    [ActionSetting(ActionSetting.Target)]
    float BrushTarget { get; set; }
}