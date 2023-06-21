using DevkitServer.Patches;
using DevkitServer.Util.Encoding;
using HarmonyLib;
using JetBrains.Annotations;
using SDG.Framework.Devkit;
using SDG.Framework.Devkit.Tools;
using SDG.Framework.Landscapes;
using System.Reflection;
using System.Reflection.Emit;
#if SERVER
using DevkitServer.API.Permissions;
using DevkitServer.Core.Permissions;
#endif
#if CLIENT
using DevkitServer.Players.UI;
#endif

namespace DevkitServer.Multiplayer.Actions;
public sealed class TerrainActions
{
    public EditorActions EditorActions { get; }
    internal TerrainActions(EditorActions actions)
    {
        EditorActions = actions;
    }
    public void Subscribe()
    {
#if CLIENT
        if (EditorActions.IsOwner)
        {
            ClientEvents.OnPaintRamp += OnPaintRamp;
            ClientEvents.OnAdjustHeightmap += OnAdjustHeightmap;
            ClientEvents.OnFlattenHeightmap += OnFlattenHeightmap;
            ClientEvents.OnSmoothHeightmap += OnSmoothHeightmap;
            ClientEvents.OnPaintSplatmap += OnPaintSplatmap;
            ClientEvents.OnAutoPaintSplatmap += OnAutoPaintSplatmap;
            ClientEvents.OnSmoothSplatmap += OnSmoothSplatmap;
            ClientEvents.OnPaintHoles += OnPaintHoles;
            ClientEvents.OnAddTile += OnAddTile;
            ClientEvents.OnDeleteTile += OnDeleteTile;
            ClientEvents.OnUpdateTileSplatmapLayers += OnUpdateTileSplatmapLayers;
        }
#endif
    }

    public void Unsubscribe()
    {
#if CLIENT
        if (EditorActions.IsOwner)
        {
            ClientEvents.OnPaintRamp -= OnPaintRamp;
            ClientEvents.OnAdjustHeightmap -= OnAdjustHeightmap;
            ClientEvents.OnFlattenHeightmap -= OnFlattenHeightmap;
            ClientEvents.OnSmoothHeightmap -= OnSmoothHeightmap;
            ClientEvents.OnPaintSplatmap -= OnPaintSplatmap;
            ClientEvents.OnAutoPaintSplatmap -= OnAutoPaintSplatmap;
            ClientEvents.OnSmoothSplatmap -= OnSmoothSplatmap;
            ClientEvents.OnPaintHoles -= OnPaintHoles;
            ClientEvents.OnAddTile -= OnAddTile;
            ClientEvents.OnDeleteTile -= OnDeleteTile;
            ClientEvents.OnUpdateTileSplatmapLayers -= OnUpdateTileSplatmapLayers;
        }
#endif
    }
#if CLIENT
    private void OnPaintRamp(in PaintRampProperties properties)
    {
        EditorActions.QueueAction(new HeightmapRampAction
        {
            Bounds = properties.Bounds,
            StartPosition = properties.Start,
            EndPosition = properties.End,
            BrushRadius = properties.Radius,
            BrushFalloff = properties.Falloff,
            DeltaTime = properties.DeltaTime
        });
    }
    private void OnAdjustHeightmap(in AdjustHeightmapProperties properties)
    {
        EditorActions.QueueAction(new HeightmapAdjustAction
        {
            Bounds = properties.Bounds,
            BrushPosition = properties.Position.ToVector2(),
            BrushRadius = properties.Radius,
            BrushFalloff = properties.Falloff,
            BrushStrength = properties.Strength,
            BrushSensitivity = properties.Sensitivity,
            Subtracting = properties.IsSubtracting,
            DeltaTime = properties.DeltaTime
        });
    }
    private void OnFlattenHeightmap(in FlattenHeightmapProperties properties)
    {
        EditorActions.QueueAction(new HeightmapFlattenAction
        {
            Bounds = properties.Bounds,
            BrushPosition = properties.Position.ToVector2(),
            BrushRadius = properties.Radius,
            BrushFalloff = properties.Falloff,
            BrushStrength = properties.Strength,
            BrushSensitivity = properties.Sensitivity,
            BrushTarget = properties.Target,
            FlattenMethod = properties.FlattenMethod,
            DeltaTime = properties.DeltaTime
        });
    }
    private void OnSmoothHeightmap(in SmoothHeightmapProperties properties)
    {
        EditorActions.QueueAction(new HeightmapSmoothAction
        {
            Bounds = properties.Bounds,
            BrushPosition = properties.Position.ToVector2(),
            BrushRadius = properties.Radius,
            BrushFalloff = properties.Falloff,
            BrushStrength = properties.Strength,
            SmoothTarget = properties.Target,
            SmoothMethod = properties.SmoothMethod,
            DeltaTime = properties.DeltaTime
        });
    }
    private void OnPaintSplatmap(in PaintSplatmapProperties properties)
    {
        EditorActions.QueueAction(new SplatmapPaintAction
        {
            Bounds = properties.Bounds,
            BrushPosition = properties.Position.ToVector2(),
            BrushRadius = properties.Radius,
            BrushFalloff = properties.Falloff,
            BrushStrength = properties.Strength,
            BrushSensitivity = properties.Sensitivity,
            BrushTarget = properties.Target,
            UseWeightTarget = properties.UseWeightTarget,
            UseAutoSlope = properties.UseAutoSlope,
            UseAutoFoundation = properties.UseAutoFoundation,
            AutoSlopeMinAngleBegin = properties.AutoSlopeProperties.MinimumAngleStart,
            AutoSlopeMinAngleEnd = properties.AutoSlopeProperties.MinimumAngleEnd,
            AutoSlopeMaxAngleBegin = properties.AutoSlopeProperties.MaximumAngleStart,
            AutoSlopeMaxAngleEnd = properties.AutoSlopeProperties.MaximumAngleEnd,
            AutoFoundationRayLength = properties.AutoFoundationProperties.Length,
            AutoFoundationRayRadius = properties.AutoFoundationProperties.Radius,
            AutoFoundationRayMask = properties.AutoFoundationProperties.Mask,
            IsRemoving = properties.IsRemoving,
            SplatmapMaterial = properties.Material,
            DeltaTime = properties.DeltaTime
        });
    }
    private void OnAutoPaintSplatmap(in PaintSplatmapProperties properties)
    {
        EditorActions.QueueAction(new SplatmapPaintAction
        {
            IsAuto = true,
            Bounds = properties.Bounds,
            BrushPosition = properties.Position.ToVector2(),
            BrushRadius = properties.Radius,
            BrushFalloff = properties.Falloff,
            BrushStrength = properties.Strength,
            BrushSensitivity = properties.Sensitivity,
            BrushTarget = properties.Target,
            UseWeightTarget = properties.UseWeightTarget,
            UseAutoSlope = properties.UseAutoSlope,
            UseAutoFoundation = properties.UseAutoFoundation,
            AutoSlopeMinAngleBegin = properties.AutoSlopeProperties.MinimumAngleStart,
            AutoSlopeMinAngleEnd = properties.AutoSlopeProperties.MinimumAngleEnd,
            AutoSlopeMaxAngleBegin = properties.AutoSlopeProperties.MaximumAngleStart,
            AutoSlopeMaxAngleEnd = properties.AutoSlopeProperties.MaximumAngleEnd,
            AutoFoundationRayLength = properties.AutoFoundationProperties.Length,
            AutoFoundationRayRadius = properties.AutoFoundationProperties.Radius,
            AutoFoundationRayMask = properties.AutoFoundationProperties.Mask,
            IsRemoving = properties.IsRemoving,
            SplatmapMaterial = properties.Material,
            DeltaTime = properties.DeltaTime
        });
    }
    private void OnSmoothSplatmap(in SmoothSplatmapProperties properties)
    {
        EditorActions.QueueAction(new SplatmapSmoothAction
        {
            Bounds = properties.Bounds,
            BrushPosition = properties.Position.ToVector2(),
            BrushRadius = properties.Radius,
            BrushFalloff = properties.Falloff,
            BrushStrength = properties.Strength,
            SmoothMethod = properties.SmoothMethod,
            DeltaTime = properties.DeltaTime
        });
    }
    private void OnPaintHoles(in PaintHolesProperties properties)
    {
        EditorActions.QueueAction(new HolemapPaintAction
        {
            Bounds = properties.Bounds,
            BrushPosition = properties.Position.ToVector2(),
            BrushRadius = properties.Radius,
            IsFilling = properties.IsFilling,
            DeltaTime = properties.DeltaTime
        });
    }
    private void OnAddTile(in UpdateLandscapeTileProperties properties)
    {
        EditorActions.QueueAction(new TileModifyAction
        {
            Coordinates = properties.Tile.coord,
            DeltaTime = properties.DeltaTime
        });
    }
    private void OnDeleteTile(in UpdateLandscapeTileProperties properties)
    {
        EditorActions.QueueAction(new TileModifyAction
        {
            IsDelete = true,
            Coordinates = properties.Tile.coord,
            DeltaTime = properties.DeltaTime
        });
    }
    private void OnUpdateTileSplatmapLayers(in UpdateLandscapeTileProperties properties)
    {
        EditorActions.QueueAction(new TileSplatmapLayersUpdateAction
        {
            Coordinates = properties.Tile.coord,
            DeltaTime = properties.DeltaTime,
            Layers = properties.Tile.materials.ToArray()
        });
    }
#endif

}

public enum TerrainEditorType
{
    Heightmap,
    Splatmap,
    Holes
}

public interface ITerrainAction : IAction
{
    TerrainEditorType EditorType { get; }
    Bounds Bounds { get; }
}

[ActionSetting(ActionSetting.AutoSlopeData)]
public interface IAutoSlope
{
    float AutoSlopeMinAngleBegin { get; set; }
    
    float AutoSlopeMinAngleEnd { get; set; }
    
    float AutoSlopeMaxAngleBegin { get; set; }
    
    float AutoSlopeMaxAngleEnd { get; set; }
}
[ActionSetting(ActionSetting.AutoFoundationData)]
public interface IAutoFoundation
{
    float AutoFoundationRayLength { get; set; }
    
    float AutoFoundationRayRadius { get; set; }
    
    ERayMask AutoFoundationRayMask { get; set; }
}

[Action(ActionType.HeightmapRamp, EncodingEx.MaxHeightmapBoundsSize + 28, 8)]
[EarlyTypeInit]
public sealed class HeightmapRampAction : ITerrainAction, IBrushRadius, IBrushFalloff
{
    public ActionType Type => ActionType.HeightmapRamp;
    public TerrainEditorType EditorType => TerrainEditorType.Heightmap;
    public CSteamID Instigator { get; set; }
    public Bounds Bounds { get; set; }
    public Vector3 StartPosition { get; set; }
    public Vector3 EndPosition { get; set; }
    public float BrushRadius { get; set; }
    public float BrushFalloff { get; set; }
    public float DeltaTime { get; set; }
    public void Apply()
    {
        LandscapeUtil.WriteHeightmapNoTransactions(Bounds, IntlHandleHeightmapWriteRamp);
    }
#if SERVER
    public bool CheckCanApply()
    {
        return VanillaPermissions.EditHeightmap.Has(Instigator.m_SteamID) ||
               VanillaPermissions.EditTerrain.Has(Instigator.m_SteamID);
    }
#endif
    public void Read(ByteReader reader)
    {
        Bounds = reader.ReadInflatedBounds();
        StartPosition = reader.ReadVector3();
        EndPosition = reader.ReadVector3();
        DeltaTime = reader.ReadFloat();
    }
    public void Write(ByteWriter writer)
    {
        writer.WriteHeightmapBounds(Bounds);
        writer.Write(StartPosition);
        writer.Write(EndPosition);
        writer.Write(DeltaTime);
    }
    private float IntlHandleHeightmapWriteRamp(LandscapeCoord tileCoord, HeightmapCoord heightmapCoord, Vector3 worldPosition, float currentHeight)
    {
        Vector2 difference = new Vector2(EndPosition.x - StartPosition.x, EndPosition.z - StartPosition.z);
        float length = difference.magnitude;
        Vector2 norm = difference / length;
        Vector2 rhs = norm.Cross();
        Vector2 ptDiff = new Vector2(worldPosition.x - StartPosition.x, worldPosition.z - StartPosition.z);
        float magnitude2 = ptDiff.magnitude;
        Vector2 lhs = ptDiff / magnitude2;
        float dot = Vector2.Dot(lhs, norm);
        if (dot < 0f) return currentHeight;
        float t = magnitude2 * dot / length;
        if (t > 1f) return currentHeight;
        dot = Vector2.Dot(lhs, rhs);
        float distance = Mathf.Abs(magnitude2 * dot / BrushRadius);
        if (distance > 1f)
            return currentHeight;
        float brushAlpha = this.GetBrushAlpha(distance);
        float a = (StartPosition.y + Landscape.TILE_HEIGHT / 2f) / Landscape.TILE_HEIGHT;
        float b = (EndPosition.y + Landscape.TILE_HEIGHT / 2f) / Landscape.TILE_HEIGHT;
        currentHeight = Mathf.Lerp(currentHeight, Mathf.Lerp(a, b, t), brushAlpha);
        return Mathf.Clamp01(currentHeight);
    }
}

[Action(ActionType.HeightmapAdjust, EncodingEx.MaxHeightmapBoundsSize + 12, 16)]
[EarlyTypeInit]
public sealed class HeightmapAdjustAction : ITerrainAction, IBrushRadius, IBrushFalloff, IBrushStrength, IBrushSensitivity
{
    public ActionType Type => ActionType.HeightmapAdjust;
    public TerrainEditorType EditorType => TerrainEditorType.Heightmap;
    public CSteamID Instigator { get; set; }
    public Bounds Bounds { get; set; }
    public Vector2 BrushPosition { get; set; }
    public float BrushRadius { get; set; }
    public float BrushFalloff { get; set; }
    public float BrushStrength { get; set; }
    public float BrushSensitivity { get; set; }
    public float DeltaTime { get; set; }
    public bool Subtracting { get; set; }

    public void Apply()
    {
        LandscapeUtil.WriteHeightmapNoTransactions(Bounds, IntlHandleHeightmapWriteAdjust);
    }
#if SERVER
    public bool CheckCanApply()
    {
        return VanillaPermissions.EditHeightmap.Has(Instigator.m_SteamID) ||
               VanillaPermissions.EditTerrain.Has(Instigator.m_SteamID);
    }
#endif
    public void Read(ByteReader reader)
    {
        Bounds = reader.ReadInflatedBounds(out bool sub);
        Subtracting = sub;
        BrushPosition = reader.ReadVector2();
        DeltaTime = reader.ReadFloat();
    }
    public void Write(ByteWriter writer)
    {
        writer.WriteHeightmapBounds(Bounds, Subtracting);
        writer.Write(BrushPosition);
        writer.Write(DeltaTime);
    }
    private float IntlHandleHeightmapWriteAdjust(LandscapeCoord tileCoord, HeightmapCoord heightmapCoord, Vector3 worldPosition, float currentHeight)
    {
        float distance = new Vector2(worldPosition.x - BrushPosition.x, worldPosition.z - BrushPosition.y).sqrMagnitude;
        if (distance > BrushRadius * BrushRadius)
        {
            return currentHeight;
        }
        distance = Mathf.Sqrt(distance) / BrushRadius;
        float num = DeltaTime * BrushStrength * this.GetBrushAlpha(distance) * BrushSensitivity;
        if (Subtracting)
            num = -num;
        currentHeight += num;
        return currentHeight;
    }
}

[Action(ActionType.HeightmapFlatten, EncodingEx.MaxHeightmapBoundsSize + 13, 20)]
[EarlyTypeInit]
public sealed class HeightmapFlattenAction : ITerrainAction, IBrushRadius, IBrushFalloff, IBrushStrength, IBrushSensitivity, IBrushTarget
{
    public ActionType Type => ActionType.HeightmapFlatten;
    public TerrainEditorType EditorType => TerrainEditorType.Heightmap;
    public EDevkitLandscapeToolHeightmapFlattenMethod FlattenMethod { get; set; }
    public CSteamID Instigator { get; set; }
    public Bounds Bounds { get; set; }
    public Vector2 BrushPosition { get; set; }
    public float BrushRadius { get; set; }
    public float BrushFalloff { get; set; }
    public float BrushStrength { get; set; }
    public float BrushSensitivity { get; set; }
    public float BrushTarget { get; set; }
    public float DeltaTime { get; set; }

    public void Apply()
    {
        LandscapeUtil.WriteHeightmapNoTransactions(Bounds, IntlHandleHeightmapWriteFlatten);
    }
#if SERVER
    public bool CheckCanApply()
    {
        return VanillaPermissions.EditHeightmap.Has(Instigator.m_SteamID) ||
               VanillaPermissions.EditTerrain.Has(Instigator.m_SteamID);
    }
#endif
    public void Read(ByteReader reader)
    {
        Bounds = reader.ReadInflatedBounds();
        FlattenMethod = (EDevkitLandscapeToolHeightmapFlattenMethod)reader.ReadUInt8();
        BrushPosition = reader.ReadVector2();
        DeltaTime = reader.ReadFloat();
    }
    public void Write(ByteWriter writer)
    {
        writer.WriteHeightmapBounds(Bounds);
        writer.Write((byte)FlattenMethod);
        writer.Write(BrushPosition);
        writer.Write(DeltaTime);
    }
    private float IntlHandleHeightmapWriteFlatten(LandscapeCoord tileCoord, HeightmapCoord heightmapCoord, Vector3 worldPosition, float currentHeight)
    {
        float distance = new Vector2(worldPosition.x - BrushPosition.x, worldPosition.z - BrushPosition.y).sqrMagnitude;
        if (distance > BrushRadius * BrushRadius)
            return currentHeight;
        distance = Mathf.Sqrt(distance) / BrushRadius;
        float brushAlpha = this.GetBrushAlpha(distance);
        float a = (BrushTarget + Landscape.TILE_HEIGHT / 2f) / Landscape.TILE_HEIGHT;
        a = FlattenMethod switch
        {
            EDevkitLandscapeToolHeightmapFlattenMethod.MIN => Mathf.Min(a, currentHeight),
            EDevkitLandscapeToolHeightmapFlattenMethod.MAX => Mathf.Max(a, currentHeight),
            _ => a
        };
        float amt = DeltaTime * BrushStrength * brushAlpha;
        amt = Mathf.Clamp(a - currentHeight, -amt, amt) * BrushSensitivity;
        return currentHeight + amt;
    }
}

[Action(ActionType.HeightmapSmooth, EncodingEx.MaxHeightmapBoundsSize + 16, 12)]
[EarlyTypeInit]
public sealed class HeightmapSmoothAction : ITerrainAction, IBrushRadius, IBrushFalloff, IBrushStrength
{
    private static readonly Dictionary<LandscapeCoord, float[,]> PixelSmoothBuffer = new Dictionary<LandscapeCoord, float[,]>(4);
    private static int _pixelSmoothSampleCount;
    private static float _pixelSmoothSampleTotal;
    public ActionType Type => ActionType.HeightmapSmooth;
    public TerrainEditorType EditorType => TerrainEditorType.Heightmap;
    public EDevkitLandscapeToolHeightmapSmoothMethod SmoothMethod { get; set; }
    public CSteamID Instigator { get; set; }
    public Bounds Bounds { get; set; }
    public Vector2 BrushPosition { get; set; }
    public float BrushRadius { get; set; }
    public float BrushFalloff { get; set; }
    public float BrushStrength { get; set; }
    public float DeltaTime { get; set; }
    public float SmoothTarget { get; set; }
    static HeightmapSmoothAction()
    {
        try
        {
            PatchesMain.Patcher.CreateReversePatcher(
                    typeof(TerrainEditor).GetMethod("SampleHeightPixelSmooth", BindingFlags.Instance | BindingFlags.NonPublic),
                    new HarmonyMethod(typeof(HeightmapSmoothAction).GetMethod(nameof(SampleHeightPixelSmooth), BindingFlags.Static | BindingFlags.NonPublic)))
                .Patch(HarmonyReversePatchType.Original);
        }
        catch (Exception ex)
        {
            Logger.LogError("Error reverse patching TerrainEditor.blendSplatmapWeights.");
            Logger.LogError(ex);
            DevkitServerModule.Fault();
        }

        try
        {
            int a = 0;
            float b = 0;
            SampleHeightPixelSmooth(null, new Vector3(float.NaN, float.NaN, float.NaN), ref a, ref b);
        }
        catch (NotImplementedException)
        {
            Logger.LogError("Failed to reverse patch " + Accessor.GetMethodInfo(SampleHeightPixelSmooth)?.Format() + ".");
            DevkitServerModule.Fault();
            return;
        }

        Logger.LogDebug("Reverse patched " + Accessor.GetMethodInfo(SampleHeightPixelSmooth)?.Format() + ".");
    }
    public void Apply()
    {
        if (SmoothMethod == EDevkitLandscapeToolHeightmapSmoothMethod.PIXEL_AVERAGE)
        {
            Bounds readBounds = Bounds;
            readBounds.Expand(Landscape.HEIGHTMAP_WORLD_UNIT * 2f);
            LandscapeBounds landscapeBounds = new LandscapeBounds(readBounds);
            LandscapeUtil.CopyHeightmapTo(landscapeBounds, PixelSmoothBuffer);
        }
        LandscapeUtil.WriteHeightmapNoTransactions(Bounds, IntlHandleHeightmapWriteSmooth);
        if (SmoothMethod == EDevkitLandscapeToolHeightmapSmoothMethod.PIXEL_AVERAGE)
            LandscapeUtil.ReleaseHeightmapBuffer(PixelSmoothBuffer);
    }
#if SERVER
    public bool CheckCanApply()
    {
        return VanillaPermissions.EditHeightmap.Has(Instigator.m_SteamID) ||
               VanillaPermissions.EditTerrain.Has(Instigator.m_SteamID);
    }
#endif
    public void Read(ByteReader reader)
    {
        Bounds = reader.ReadInflatedBounds(out bool pixel);
        SmoothMethod = pixel ? EDevkitLandscapeToolHeightmapSmoothMethod.PIXEL_AVERAGE : EDevkitLandscapeToolHeightmapSmoothMethod.BRUSH_AVERAGE;
        BrushPosition = reader.ReadVector2();
        DeltaTime = reader.ReadFloat();
        if (SmoothMethod != EDevkitLandscapeToolHeightmapSmoothMethod.PIXEL_AVERAGE)
            SmoothTarget = reader.ReadFloat();
    }
    public void Write(ByteWriter writer)
    {
        writer.WriteHeightmapBounds(Bounds, SmoothMethod == EDevkitLandscapeToolHeightmapSmoothMethod.PIXEL_AVERAGE);
        writer.Write(BrushPosition);
        writer.Write(DeltaTime);
        if (SmoothMethod != EDevkitLandscapeToolHeightmapSmoothMethod.PIXEL_AVERAGE)
            writer.Write(SmoothTarget);
    }
    private static void SampleHeightPixelSmooth(object? instance, Vector3 worldPosition, ref int sampleCount, ref float sampleAverage)
    {
        IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            FieldInfo? field = typeof(TerrainEditor).GetField("heightmapPixelSmoothBuffer", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null || typeof(Dictionary<LandscapeCoord, float[,]>) != field.FieldType)
                Logger.LogWarning("Unable to find field: TerrainEditor.heightmapPixelSmoothBuffer.");

            FieldInfo buffer = typeof(HeightmapSmoothAction).GetField(nameof(PixelSmoothBuffer), BindingFlags.Static | BindingFlags.NonPublic)!;

            List<CodeInstruction> ins = new List<CodeInstruction>(instructions);
            bool one = false;
            for (int i = 0; i < ins.Count; ++i)
            {
                CodeInstruction c = ins[i];
                if (c.opcode == OpCodes.Ldarg_0 && i != ins.Count - 1 && (field == null && ins[i + 1].opcode == OpCodes.Ldfld || ins[i + 1].LoadsField(field)))
                {
                    CodeInstruction c2 = new CodeInstruction(OpCodes.Ldsfld, buffer);
                    c2.labels.AddRange(c.labels);
                    c2.blocks.AddRange(c.blocks);
                    c2.labels.AddRange(ins[i + 1].labels);
                    c2.blocks.AddRange(ins[i + 1].blocks);
                    yield return c2;
                    one = true;
                    ++i;
                    Logger.LogDebug("Replaced " + ins[i].Format() + " with " + c2.Format() + ".");
                    continue;
                }
                yield return c;
            }
            if (!one)
            {
                Logger.LogWarning("Unable to replace load of " + field?.Format() + " with " + buffer.Format() + ".");
            }
        }

        _ = Transpiler(null!);
        throw new NotImplementedException();
    }
    private float IntlHandleHeightmapWriteSmooth(LandscapeCoord tileCoord, HeightmapCoord heightmapCoord, Vector3 worldPosition, float currentHeight)
    {
        float distance = new Vector2(worldPosition.x - BrushPosition.x, worldPosition.z - BrushPosition.y).sqrMagnitude;
        if (distance > BrushRadius * BrushRadius)
            return currentHeight;
        distance = Mathf.Sqrt(distance) / BrushRadius;
        float brushAlpha = this.GetBrushAlpha(distance);
        float target = SmoothTarget;
        if (SmoothMethod == EDevkitLandscapeToolHeightmapSmoothMethod.PIXEL_AVERAGE)
        {
            _pixelSmoothSampleCount = 0;
            _pixelSmoothSampleTotal = 0.0f;
            SampleHeightPixelSmooth(null, worldPosition + new Vector3(Landscape.HEIGHTMAP_WORLD_UNIT, 0.0f, 0.0f), ref _pixelSmoothSampleCount, ref _pixelSmoothSampleTotal);
            SampleHeightPixelSmooth(null, worldPosition + new Vector3(-Landscape.HEIGHTMAP_WORLD_UNIT, 0.0f, 0.0f), ref _pixelSmoothSampleCount, ref _pixelSmoothSampleTotal);
            SampleHeightPixelSmooth(null, worldPosition + new Vector3(0.0f, 0.0f, Landscape.HEIGHTMAP_WORLD_UNIT), ref _pixelSmoothSampleCount, ref _pixelSmoothSampleTotal);
            SampleHeightPixelSmooth(null, worldPosition + new Vector3(0.0f, 0.0f, -Landscape.HEIGHTMAP_WORLD_UNIT), ref _pixelSmoothSampleCount, ref _pixelSmoothSampleTotal);
            target = _pixelSmoothSampleCount <= 0 ? currentHeight : _pixelSmoothSampleTotal / _pixelSmoothSampleCount;
        }
        currentHeight = Mathf.Lerp(currentHeight, target, DeltaTime * BrushStrength * brushAlpha);
        return currentHeight;
    }
}

[Action(ActionType.SplatmapPaint, EncodingEx.MaxHeightmapBoundsSize + 13, 64)]
[Action(ActionType.SplatmapAutoPaint, EncodingEx.MaxHeightmapBoundsSize + 12, 64, CreateMethod = nameof(CreateAutoPaint))]
[EarlyTypeInit]
public sealed class SplatmapPaintAction : ITerrainAction, IBrushRadius, IBrushFalloff, IBrushStrength, IBrushTarget, IBrushSensitivity, IAutoSlope, IAutoFoundation, IAsset
{
    private static readonly RaycastHit[] FoundationHits;
    static SplatmapPaintAction()
    {
        // to sync with buffer length changes in the base game
        try
        {
            FoundationHits = (RaycastHit[])typeof(TerrainEditor).GetField("FOUNDATION_HITS", BindingFlags.Static | BindingFlags.NonPublic)?
                .GetValue(null)!;
            if (FoundationHits == null)
                Logger.LogWarning("Failed to get foundation buffer (not a huge deal).");
        }
        catch (Exception ex)
        {
            Logger.LogWarning("Failed to get foundation buffer (not a huge deal).");
            Logger.LogError(ex);
        }

        FoundationHits ??= new RaycastHit[4];
        try
        {
            PatchesMain.Patcher.CreateReversePatcher(
                    typeof(TerrainEditor).GetMethod("blendSplatmapWeights", BindingFlags.Instance | BindingFlags.NonPublic),
                    new HarmonyMethod(typeof(SplatmapPaintAction).GetMethod(nameof(BlendSplatmapWeights), BindingFlags.Static | BindingFlags.NonPublic)))
                .Patch(HarmonyReversePatchType.Original);
        }
        catch (Exception ex)
        {
            Logger.LogError("Error reverse patching TerrainEditor.blendSplatmapWeights.");
            Logger.LogError(ex);
            DevkitServerModule.Fault();
        }

        try
        {
            BlendSplatmapWeights(null, null!, 0, 0, 0);
        }
        catch (NotImplementedException)
        {
            Logger.LogError("Failed to reverse patch " + Accessor.GetMethodInfo(BlendSplatmapWeights)?.Format() + ".");
            DevkitServerModule.Fault();
        }
        catch (NullReferenceException)
        {
            Logger.LogDebug("Reverse patched " + Accessor.GetMethodInfo(BlendSplatmapWeights)?.Format() + ".");
        }

        try
        {
            PatchesMain.Patcher.CreateReversePatcher(
                    typeof(TerrainEditor).GetMethod("getSplatmapTargetMaterialLayerIndex", BindingFlags.Instance | BindingFlags.NonPublic),
                    new HarmonyMethod(typeof(SplatmapPaintAction).GetMethod(nameof(GetSplatmapTargetMaterialLayerIndex), BindingFlags.Static | BindingFlags.NonPublic)))
                .Patch(HarmonyReversePatchType.Original);
        }
        catch (Exception ex)
        {
            Logger.LogError("Error reverse patching TerrainEditor.getSplatmapTargetMaterialLayerIndex.");
            Logger.LogError(ex);
            DevkitServerModule.Fault();
        }

        try
        {
            int val = GetSplatmapTargetMaterialLayerIndex(null, null!, default); // invalid material returns -1
            if (val == -1)
                Logger.LogDebug("Reverse patched " + Accessor.GetMethodInfo(GetSplatmapTargetMaterialLayerIndex)?.Format() + ".");
        }
        catch (NotImplementedException)
        {
            Logger.LogError("Failed to reverse patch " + Accessor.GetMethodInfo(GetSplatmapTargetMaterialLayerIndex)?.Format() + ".");
            DevkitServerModule.Fault();
        }
        catch (NullReferenceException)
        {
            Logger.LogDebug("Reverse patched " + Accessor.GetMethodInfo(GetSplatmapTargetMaterialLayerIndex)?.Format() + ".");
        }
    }

    private LandscapeMaterialAsset? _asset;
    private bool _assetFound;

    public ActionType Type => IsAuto ? ActionType.SplatmapAutoPaint : ActionType.SplatmapPaint;
    public TerrainEditorType EditorType => TerrainEditorType.Splatmap;
    public CSteamID Instigator { get; set; }
    public Bounds Bounds { get; set; }
    public Vector2 BrushPosition { get; set; }
    public AssetReference<LandscapeMaterialAsset> SplatmapMaterial { get; set; }
    Guid IAsset.Asset
    {
        get => SplatmapMaterial.GUID;
        set => SplatmapMaterial = new AssetReference<LandscapeMaterialAsset>(value);
    }
    public bool IsAuto { get; set; }
    public float BrushRadius { get; set; }
    public float BrushFalloff { get; set; }
    public float BrushStrength { get; set; }
    public float BrushTarget { get; set; }
    public float DeltaTime { get; set; }
    public float BrushSensitivity { get; set; }
    public bool UseWeightTarget { get; set; }
    public bool UseAutoSlope { get; set; }
    public bool UseAutoFoundation { get; set; }
    public bool IsRemoving { get; set; }
    public float AutoSlopeMinAngleBegin { get; set; }
    public float AutoSlopeMinAngleEnd { get; set; }
    public float AutoSlopeMaxAngleBegin { get; set; }
    public float AutoSlopeMaxAngleEnd { get; set; }
    public float AutoFoundationRayLength { get; set; }
    public float AutoFoundationRayRadius { get; set; }
    public ERayMask AutoFoundationRayMask { get; set; }
    public void Apply()
    {
        LandscapeUtil.WriteSplatmapNoTransactions(Bounds, IntlHandleSplatmapWritePaint);
    }
#if SERVER
    public bool CheckCanApply()
    {
        return VanillaPermissions.EditSplatmap.Has(Instigator.m_SteamID) ||
               VanillaPermissions.EditTerrain.Has(Instigator.m_SteamID);
    }
#endif
    public void Read(ByteReader reader)
    {
        Bounds = reader.ReadInflatedBounds(out bool isAuto);
        IsAuto = isAuto;
        if (!isAuto)
        {
            byte flags = reader.ReadUInt8();
            UseWeightTarget = (flags & (1 << 0)) != 0;
            UseAutoSlope = (flags & (1 << 1)) != 0;
            UseAutoFoundation = (flags & (1 << 2)) != 0;
            IsRemoving = (flags & (1 << 3)) != 0;
        }
        BrushPosition = reader.ReadVector2();
        DeltaTime = reader.ReadFloat();
    }
    public void Write(ByteWriter writer)
    {
        writer.WriteHeightmapBounds(Bounds, IsAuto);
        if (!IsAuto)
        {
            byte flags = (byte)((UseWeightTarget ? 1 << 0 : 0) | (UseAutoSlope ? 1 << 1 : 0) | (UseAutoFoundation ? 1 << 2 : 0) | (IsRemoving ? 1 << 3 : 0));
            writer.Write(flags);
        }
        writer.Write(BrushPosition);
        writer.Write(DeltaTime);
    }
    private void IntlHandleSplatmapWritePaint(LandscapeCoord tileCoord, SplatmapCoord splatmapCoord, Vector3 worldPosition, float[] currentWeights)
    {
        if (_assetFound && _asset == null)
            return; // already errored
        if (!_assetFound)
        {
            _assetFound = true;
            _asset = SplatmapMaterial.Find();
        }
        if (_asset == null)
        {
            if (SplatmapMaterial.isValid)
                Logger.LogWarning($"[TERRAIN ACTIONS] Failed to find splatmap asset: {SplatmapMaterial.Format()} (from: {Instigator.Format()}).");
            return;
        }

        if (IsAuto && !_asset!.useAutoFoundation && !_asset.useAutoSlope)
            return;
        float distance = new Vector2(worldPosition.x - BrushPosition.x, worldPosition.z - BrushPosition.y).sqrMagnitude;
        if (distance > BrushRadius * BrushRadius)
            return;
        LandscapeTile? tile = Landscape.getTile(tileCoord);
        if (tile?.materials == null)
            return;

        int materialLayerIndex = GetSplatmapTargetMaterialLayerIndex(null, tile, SplatmapMaterial);
        if (materialLayerIndex == -1)
            return;

        distance = Mathf.Sqrt(distance) / BrushRadius;
        float targetWeight = IsAuto ? 1f : 0.5f;
        bool useAutoFoundation = IsAuto ? _asset!.useAutoFoundation : UseAutoFoundation;
        bool useAutoSlope = IsAuto ? _asset!.useAutoSlope : UseAutoSlope;
        if (useAutoFoundation || useAutoSlope)
        {
            bool needsApplying = false;
            if (useAutoFoundation)
            {
                float radius, length;
                int mask;
                if (IsAuto)
                {
                    mask = (int)_asset!.autoRayMask;
                    radius = _asset.autoRayRadius;
                    length = _asset!.autoRayLength;
                }
                else
                {
                    mask = (int)AutoFoundationRayMask;
                    radius = AutoFoundationRayRadius;
                    length = AutoFoundationRayLength;
                }
                int ct = Physics.SphereCastNonAlloc(
                    worldPosition + new Vector3(0.0f, _asset!.autoRayLength, 0.0f),
                    radius, Vector3.down, FoundationHits,
                    length, mask, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < ct; ++i)
                {
                    ObjectAsset? asset = LevelObjects.getAsset(FoundationHits[i].transform);
                    if (asset is not { isSnowshoe: true })
                    {
                        needsApplying = true;
                        targetWeight = !IsAuto && UseWeightTarget ? BrushTarget : 1f;
                        break;
                    }
                }
            }

            if (!needsApplying && useAutoSlope && Landscape.getNormal(worldPosition, out Vector3 normal))
            {
                float angle = Vector3.Angle(Vector3.up, normal);
                float minBegin, minEnd, maxBegin, maxEnd;
                if (IsAuto)
                {
                    minBegin = _asset!.autoMinAngleBegin;
                    minEnd = _asset!.autoMinAngleEnd;
                    maxBegin = _asset!.autoMaxAngleBegin;
                    maxEnd = _asset!.autoMaxAngleEnd;
                }
                else
                {
                    minBegin = AutoSlopeMinAngleBegin;
                    minEnd = AutoSlopeMinAngleEnd;
                    maxBegin = AutoSlopeMaxAngleBegin;
                    maxEnd = AutoSlopeMaxAngleEnd;
                }
                if (angle >= minBegin && angle <= maxEnd)
                {
                    targetWeight = angle >= minEnd
                        ? (angle <= maxBegin
                            ? 1f
                            : (1f - Mathf.InverseLerp(maxBegin, maxEnd, angle)))
                        : (Mathf.InverseLerp(minBegin, minEnd, angle));
                    needsApplying = true;
                }
            }
            if (!needsApplying)
                return;
        }
        else if (IsAuto)
            return;
        else
            targetWeight = UseWeightTarget ? BrushTarget : (IsRemoving ? 0f : 1f);

        float speed = DeltaTime * BrushStrength * this.GetBrushAlpha(distance) * BrushSensitivity;

        BlendSplatmapWeights(null, currentWeights, materialLayerIndex, targetWeight, speed);
    }
    private static void BlendSplatmapWeights(object? instance, float[] currentWeights, int targetMaterialLayer, float targetWeight, float speed)
    {
        throw new NotImplementedException();
    }
    private static int GetSplatmapTargetMaterialLayerIndex(object? instance, LandscapeTile tile, AssetReference<LandscapeMaterialAsset> targetMaterial)
    {
        throw new NotImplementedException();
    }

    [UsedImplicitly]
    private static SplatmapPaintAction CreateAutoPaint() => new SplatmapPaintAction { IsAuto = true };
}

[Action(ActionType.SplatmapSmooth, EncodingEx.MaxHeightmapBoundsSize + 8, 12)]
[EarlyTypeInit]
public sealed class SplatmapSmoothAction : ITerrainAction, IBrushRadius, IBrushFalloff, IBrushStrength
{
    private class SampleBufferEntry
    {
        public Guid Material;
        public float Weight;
    }

    private static readonly List<SampleBufferEntry> BrushSmoothSampleAverageBuffer = new List<SampleBufferEntry>(Landscape.SPLATMAP_LAYERS * 2);
    private static readonly Dictionary<LandscapeCoord, float[,,]> PixelSmoothBuffer = new Dictionary<LandscapeCoord, float[,,]>(4);
    private static int SmoothSampleAverageCount;
    private static int SmoothSampleCount;
    public ActionType Type => ActionType.SplatmapSmooth;
    public TerrainEditorType EditorType => TerrainEditorType.Splatmap;
    public CSteamID Instigator { get; set; }
    public Bounds Bounds { get; set; }
    public Vector2 BrushPosition { get; set; }
    public EDevkitLandscapeToolSplatmapSmoothMethod SmoothMethod { get; set; }
    public float BrushRadius { get; set; }
    public float BrushFalloff { get; set; }
    public float BrushStrength { get; set; }
    public float DeltaTime { get; set; }
    public void Apply()
    {
        if (SmoothMethod == EDevkitLandscapeToolSplatmapSmoothMethod.BRUSH_AVERAGE)
        {
            SmoothSampleAverageCount = 0;
            SmoothSampleCount = 0;
            Landscape.readSplatmap(Bounds, IntlHandleSplatmapReadBrushAverage);
        }
        else if (SmoothMethod == EDevkitLandscapeToolSplatmapSmoothMethod.PIXEL_AVERAGE)
        {
            Bounds bounds2 = Bounds;
            bounds2.Expand(Landscape.SPLATMAP_WORLD_UNIT * 2f);
            LandscapeBounds landscapeBounds = new LandscapeBounds(bounds2);
            LandscapeUtil.CopySplatmapTo(landscapeBounds, PixelSmoothBuffer);
        }
        LandscapeUtil.WriteSplatmapNoTransactions(Bounds, IntlHandleSplatmapWriteSmooth);
        if (SmoothMethod == EDevkitLandscapeToolSplatmapSmoothMethod.PIXEL_AVERAGE)
            LandscapeUtil.ReleaseSplatmapBuffer(PixelSmoothBuffer);
    }
#if SERVER
    public bool CheckCanApply()
    {
        return VanillaPermissions.EditSplatmap.Has(Instigator.m_SteamID) ||
               VanillaPermissions.EditTerrain.Has(Instigator.m_SteamID);
    }
#endif
    private static void AddOrIncrementSample(AssetReference<LandscapeMaterialAsset> material, float weight)
    {
        ++SmoothSampleCount;
        if (weight == 0)
            return;
        bool found = false;
        for (int j = 0; j < SmoothSampleAverageCount; ++j)
        {
            if (BrushSmoothSampleAverageBuffer[j].Material == material.GUID)
            {
                BrushSmoothSampleAverageBuffer[j].Weight += weight;
                found = true;
                break;
            }
        }
        if (!found)
        {
            if (BrushSmoothSampleAverageBuffer.Count > SmoothSampleAverageCount)
            {
                SampleBufferEntry e = BrushSmoothSampleAverageBuffer[SmoothSampleAverageCount];
                e.Material = material.GUID;
                e.Weight = weight;
            }
            else
            {
                BrushSmoothSampleAverageBuffer.Add(new SampleBufferEntry { Material = material.GUID, Weight = weight });
            }
            ++SmoothSampleAverageCount;
        }
    }
    public void Read(ByteReader reader)
    {
        Bounds = reader.ReadInflatedBounds(out bool pixel);
        SmoothMethod = pixel ? EDevkitLandscapeToolSplatmapSmoothMethod.PIXEL_AVERAGE : EDevkitLandscapeToolSplatmapSmoothMethod.BRUSH_AVERAGE;
        BrushPosition = reader.ReadVector2();
        DeltaTime = reader.ReadFloat();
    }
    public void Write(ByteWriter writer)
    {
        writer.WriteHeightmapBounds(Bounds, SmoothMethod == EDevkitLandscapeToolSplatmapSmoothMethod.PIXEL_AVERAGE);
        writer.Write(BrushPosition);
        writer.Write(DeltaTime);
    }
    private void IntlHandleSplatmapReadBrushAverage(LandscapeCoord tileCoord, SplatmapCoord splatmapCoord, Vector3 worldPosition, float[] currentWeights)
    {
        float distance = new Vector2(worldPosition.x - BrushPosition.x, worldPosition.z - BrushPosition.y).sqrMagnitude;
        if (distance > BrushRadius * BrushRadius)
            return;
        LandscapeTile? tile = Landscape.getTile(tileCoord);
        if (tile?.materials == null)
            return;
        for (int i = 0; i < Landscape.SPLATMAP_LAYERS; ++i)
        {
            AssetReference<LandscapeMaterialAsset> mat = tile.materials[i];
            if (mat.isValid)
                AddOrIncrementSample(mat, currentWeights[i]);
        }
    }
    private void IntlHandleSplatmapWriteSmooth(LandscapeCoord tileCoord, SplatmapCoord splatmapCoord, Vector3 worldPosition, float[] currentWeights)
    {
        if (SmoothMethod == EDevkitLandscapeToolSplatmapSmoothMethod.BRUSH_AVERAGE && SmoothSampleCount < 1)
            return;
        LandscapeTile? tile = Landscape.getTile(tileCoord);
        if (tile?.materials == null)
            return;
        float distance = new Vector2(worldPosition.x - BrushPosition.x, worldPosition.z - BrushPosition.y).sqrMagnitude;
        if (distance > BrushRadius * BrushRadius)
            return;
        distance = Mathf.Sqrt(distance) / BrushRadius;

        if (SmoothMethod == EDevkitLandscapeToolSplatmapSmoothMethod.PIXEL_AVERAGE)
        {
            SmoothSampleAverageCount = 0;
            SmoothSampleCount = 0;
            ScanTile(0, -1);
            ScanTile(1, 0);
            ScanTile(0, 1);
            ScanTile(-1, 0);

            void ScanTile(int dx, int dy)
            {
                SplatmapCoord splatmapCoord2 = new SplatmapCoord(splatmapCoord.x + dx, splatmapCoord.y + dy);
                LandscapeCoord tileCoord2 = tileCoord;
                IntlSampleSplatmapPixelSmooth(tileCoord2, splatmapCoord2);
            }

            if (SmoothSampleCount < 1)
                return;
        }

        float speed = DeltaTime * BrushStrength * this.GetBrushAlpha(distance);
        float total = 0f;
        for (int i = 0; i < Landscape.SPLATMAP_LAYERS; ++i)
        {
            Guid r = tile.materials[i].GUID;
            for (int j = 0; j < SmoothSampleAverageCount; ++j)
            {
                if (BrushSmoothSampleAverageBuffer[j].Material == r)
                {
                    total += BrushSmoothSampleAverageBuffer[j].Weight / SmoothSampleCount;
                    break;
                }
            }
        }
        
        for (int i = 0; i < Landscape.SPLATMAP_LAYERS; ++i)
        {
            Guid r = tile.materials[i].GUID;
            float weight = 0;
            for (int j = 0; j < SmoothSampleAverageCount; ++j)
            {
                if (BrushSmoothSampleAverageBuffer[j].Material == r)
                {
                    weight = BrushSmoothSampleAverageBuffer[j].Weight / SmoothSampleCount / total;
                    break;
                }
            }
            currentWeights[i] += (weight - currentWeights[i]) * speed;
        }
    }
    private static void IntlSampleSplatmapPixelSmooth(LandscapeCoord tileCoord, SplatmapCoord splatmapCoord)
    {
        LandscapeUtility.cleanSplatmapCoord(ref tileCoord, ref splatmapCoord);
        if (!PixelSmoothBuffer.TryGetValue(tileCoord, out float[,,] splatmap))
            return;

        LandscapeTile? tile2 = Landscape.getTile(tileCoord);
        if (tile2?.materials != null)
        {
            for (int i = 0; i < Landscape.SPLATMAP_LAYERS; ++i)
            {
                AssetReference<LandscapeMaterialAsset> mat = tile2.materials[i];
                if (mat.isValid)
                    AddOrIncrementSample(mat, splatmap[splatmapCoord.x, splatmapCoord.y, i]);
            }
        }
    }
}

[Action(ActionType.HolesCut, EncodingEx.MaxHeightmapBoundsSize + 8, 4)]
[EarlyTypeInit]
public sealed class HolemapPaintAction : ITerrainAction, IBrushRadius
{
    public ActionType Type => ActionType.HolesCut;
    public TerrainEditorType EditorType => TerrainEditorType.Holes;
    public CSteamID Instigator { get; set; }
    public Bounds Bounds { get; set; }
    public Vector2 BrushPosition { get; set; }
    public float BrushRadius { get; set; }
    public bool IsFilling { get; set; }
    public float DeltaTime { get; set; }
    public void Read(ByteReader reader)
    {
        Bounds = reader.ReadInflatedBounds(out bool put);
        IsFilling = put;
        BrushPosition = reader.ReadVector2();
        DeltaTime = reader.ReadFloat();
    }
    public void Write(ByteWriter writer)
    {
        writer.WriteHeightmapBounds(Bounds, IsFilling);
        writer.Write(BrushPosition);
        writer.Write(DeltaTime);
    }
    public void Apply()
    {
        LandscapeUtil.WriteHolesNoTransactions(Bounds, IntlHandleHolesWriteCut);
    }
#if SERVER
    public bool CheckCanApply()
    {
        return VanillaPermissions.EditHoles.Has(Instigator.m_SteamID) ||
               VanillaPermissions.EditTerrain.Has(Instigator.m_SteamID);
    }
#endif
    private bool IntlHandleHolesWriteCut(Vector3 worldPosition, bool currentlyVisible)
    {
        if (currentlyVisible == IsFilling || new Vector2(worldPosition.x - BrushPosition.x, worldPosition.z - BrushPosition.y).sqrMagnitude > BrushRadius * BrushRadius)
            return currentlyVisible;
        return IsFilling;
    }
}

[Action(ActionType.AddTile, 5, 8)]
[Action(ActionType.DeleteTile, 5, 8, CreateMethod = nameof(CreateDeleteTile))]
[EarlyTypeInit]
public sealed class TileModifyAction : IAction, ICoordinates
{
    public ActionType Type => IsDelete ? ActionType.DeleteTile : ActionType.AddTile;
    public CSteamID Instigator { get; set; }
    public float DeltaTime { get; set; }
    public bool IsDelete { get; set; }
    public LandscapeCoord Coordinates { get; set; }
    int ICoordinates.CoordinateX
    {
        get => Coordinates.x;
        set => Coordinates = Coordinates with { x = value };
    }
    int ICoordinates.CoordinateY
    {
        get => Coordinates.y;
        set => Coordinates = Coordinates with { y = value };
    }
    public void Apply()
    {
        if (IsDelete)
        {
            Landscape.removeTile(Coordinates);
            LevelHierarchy.MarkDirty();
            Logger.LogInfo("Tile deleted: " + Coordinates + ".", ConsoleColor.DarkRed);
            return;
        }

        LandscapeTile tile = Landscape.addTile(Coordinates);
        if (tile != null)
        {
            tile.readHeightmaps();
            tile.readSplatmaps();
            Landscape.linkNeighbors();
            Landscape.reconcileNeighbors(tile);
            Landscape.applyLOD();
            LevelHierarchy.MarkDirty();
            Logger.LogInfo("Tile added: " + Coordinates + ".", ConsoleColor.Green);
        }
    }
#if SERVER
    public bool CheckCanApply()
    {
        return (IsDelete ? VanillaPermissions.DeleteTile : VanillaPermissions.AddTile).Has(Instigator.m_SteamID) ||
               VanillaPermissions.EditTiles.Has(Instigator.m_SteamID);
    }
#endif
    public void Write(ByteWriter writer)
    {
        writer.Write(IsDelete);
        writer.Write(DeltaTime);
    }
    public void Read(ByteReader reader)
    {
        IsDelete = reader.ReadBool();
        DeltaTime = reader.ReadFloat();
    }

    [UsedImplicitly]
    private static TileModifyAction CreateDeleteTile() => new TileModifyAction { IsDelete = true };
}

[Action(ActionType.UpdateSplatmapLayers, 132, 8)]
[EarlyTypeInit]
public sealed class TileSplatmapLayersUpdateAction : IAction, ICoordinates
{
#if CLIENT
    private static readonly Func<Array?>? GetUILayers = UIAccessTools.CreateUIFieldGetterReturn<Array>(UI.EditorTerrainTiles, "layers", false);
    private static readonly Func<int>? GetSelectedLayer = UIAccessTools.CreateUIFieldGetterReturn<int>(UI.EditorTerrainTiles, "selectedLayerIndex", false);
    private static readonly Action<int>? SetSelectedLayerIndex = UIAccessTools.GenerateUICaller<Action<int>>(UI.EditorTerrainTiles, "SetSelectedLayerIndex", throwOnFailure: false);
    private static readonly Action<object>? CallUpdateSelectedTile =
        Accessor.GenerateInstanceCaller<Action<object>>(
            Accessor.AssemblyCSharp
                .GetType("SDG.Unturned.TerrainTileLayer")?
                .GetMethod("UpdateSelectedTile", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!
            , false, useFptrReconstruction: true);
#endif
    public ActionType Type => ActionType.UpdateSplatmapLayers;
    public CSteamID Instigator { get; set; }
    public float DeltaTime { get; set; }
    public LandscapeCoord Coordinates { get; set; }
    int ICoordinates.CoordinateX
    {
        get => Coordinates.x;
        set => Coordinates = Coordinates with { x = value };
    }
    int ICoordinates.CoordinateY
    {
        get => Coordinates.y;
        set => Coordinates = Coordinates with { y = value };
    }
    public AssetReference<LandscapeMaterialAsset>[]? Layers { get; set; }
    public void Apply()
    {
        if (Layers == null)
            return;
        LandscapeTile tile = Landscape.getTile(Coordinates);
        if (tile != null)
        {
            int diff = 0;
            int len = Math.Min(Layers.Length, Landscape.SPLATMAP_LAYERS);
            for (int i = 0; i < len; ++i)
            {
                AssetReference<LandscapeMaterialAsset> layer = Layers[i];
                if (tile.materials[i] == layer)
                    continue;

                tile.materials[i] = layer;
                diff |= 1 << i;
                Logger.LogDebug("Layer updated: " + Coordinates.Format() + ", #" + i.Format() + " (" + layer.Find()?.FriendlyName.Format() + ").");
            }

            if (diff == 0)
                return;
            tile.updatePrototypes();

#if CLIENT  // update tile UI
            if (CallUpdateSelectedTile != null)
            {
                Array? layers = GetUILayers?.Invoke();
                if (layers != null)
                {
                    for (int i = 0; i < layers.Length; ++i)
                    {
                        if ((diff & (1 << i)) != 0)
                            CallUpdateSelectedTile(layers.GetValue(i));
                    }
                }
            }
            if (GetSelectedLayer != null && SetSelectedLayerIndex != null)
            {
                int selectedIndex = GetSelectedLayer();
                if (selectedIndex > 0 && (diff & (1 << selectedIndex)) != 0)
                    SetSelectedLayerIndex(selectedIndex);
            }
#endif
            LevelHierarchy.MarkDirty();
        }
    }
#if SERVER
    public bool CheckCanApply()
    {
        return VanillaPermissions.EditSplatmap.Has(Instigator.m_SteamID) ||
               VanillaPermissions.EditTerrain.Has(Instigator.m_SteamID);
    }
#endif
    public void Write(ByteWriter writer)
    {
        writer.Write(DeltaTime);
        int amt = Math.Min(byte.MaxValue, Layers == null ? 0 : Layers.Length);
        writer.Write((byte)amt);
        for (int i = 0; i < amt; ++i)
            writer.Write(Layers![i].GUID);
    }
    public void Read(ByteReader reader)
    {
        DeltaTime = reader.ReadFloat();
        int amt = reader.ReadInt32();
        if (Layers == null || Layers.Length != amt)
            Layers = new AssetReference<LandscapeMaterialAsset>[amt];
        for (int i = 0; i < amt; ++i)
            Layers[i] = new AssetReference<LandscapeMaterialAsset>(reader.ReadGuid());
    }
}