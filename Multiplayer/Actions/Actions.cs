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
    [JsonConverter(typeof(JsonStringEnumConverter))]
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

[ActionSetting(ActionSetting.Asset)]
public interface IAsset
{
    Guid Asset { get; set; }
}
[ActionSetting(ActionSetting.TileCoordinates)]
public interface ICoordinates
{
    int CoordinateX { get; set; }
    int CoordinateY { get; set; }
}
[ActionSetting(ActionSetting.Radius)]
public interface IBrushRadius
{
    float BrushRadius { get; set; }
}
[ActionSetting(ActionSetting.Falloff)]
public interface IBrushFalloff
{
    float BrushFalloff { get; set; }
}
[ActionSetting(ActionSetting.Strength)]
public interface IBrushStrength
{
    float BrushStrength { get; set; }
}
[ActionSetting(ActionSetting.Sensitivity)]
public interface IBrushSensitivity
{
    float BrushSensitivity { get; set; }
}
[ActionSetting(ActionSetting.Target)]
public interface IBrushTarget
{
    float BrushTarget { get; set; }
}