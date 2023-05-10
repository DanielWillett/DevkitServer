using DevkitServer.Patches;
using DevkitServer.Util.Encoding;
using HarmonyLib;
using JetBrains.Annotations;
using SDG.Framework.Devkit;
using SDG.Framework.Devkit.Tools;
using SDG.Framework.Landscapes;
using SDG.Framework.Utilities;
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
            ClientEvents.OnRampComplete += OnHeightmapRampConfirmed;
            ClientEvents.OnAdjusted += OnHeightmapAdjust;
            ClientEvents.OnFlattened += OnHeightmapFlatten;
            ClientEvents.OnSmoothed += OnHeightmapSmooth;
            ClientEvents.OnPainted += OnPaint;
            ClientEvents.OnAutoPainted += OnAutoPaint;
            ++ClientEvents.TakeReleaseAveragesList;
            ClientEvents.OnPaintSmoothed += OnPaintSmooth;
            ClientEvents.OnHolePainted += OnPaintHole;
            ClientEvents.OnTileAdded += OnTileAdded;
            ClientEvents.OnTileDeleted += OnTileDeleted;
            ClientEvents.OnSplatmapLayerMaterialsUpdate += OnSplatmapLayerMaterialsUpdate;
        }
#endif
    }

    public void Unsubscribe()
    {
#if CLIENT
        if (EditorActions.IsOwner)
        {
            ClientEvents.OnRampComplete -= OnHeightmapRampConfirmed;
            ClientEvents.OnAdjusted -= OnHeightmapAdjust;
            ClientEvents.OnFlattened -= OnHeightmapFlatten;
            ClientEvents.OnSmoothed -= OnHeightmapSmooth;
            ClientEvents.OnPainted -= OnPaint;
            ClientEvents.OnAutoPainted -= OnAutoPaint;
            ClientEvents.OnPaintSmoothed -= OnPaintSmooth;
            --ClientEvents.TakeReleaseAveragesList;
            ClientEvents.OnHolePainted -= OnPaintHole;
            ClientEvents.OnTileAdded -= OnTileAdded;
            ClientEvents.OnTileDeleted -= OnTileDeleted;
            ClientEvents.OnSplatmapLayerMaterialsUpdate -= OnSplatmapLayerMaterialsUpdate;
        }
#endif
    }
#if CLIENT
    private void OnHeightmapRampConfirmed(Bounds bounds, Vector3 start, Vector3 end, float radius, float falloff)
    {
        EditorActions.QueueAction(new HeightmapRampAction
        {
            Bounds = bounds,
            StartPosition = start,
            EndPosition = end,
            BrushRadius = radius,
            BrushFalloff = falloff,
            DeltaTime = Time.deltaTime
        });
    }
    private void OnHeightmapAdjust(Bounds bounds, Vector3 position, float radius, float falloff, float strength, float sensitivity, bool subtracting, float dt)
    {
        EditorActions.QueueAction(new HeightmapAdjustAction
        {
            Bounds = bounds,
            BrushPosition = position.ToVector2(),
            BrushRadius = radius,
            BrushFalloff = falloff,
            BrushStrength = strength,
            BrushSensitivity = sensitivity,
            Subtracting = subtracting,
            DeltaTime = dt
        });
    }
    private void OnHeightmapFlatten(Bounds bounds, Vector3 position, float radius, float falloff, float strength, float sensitivity, float target, EDevkitLandscapeToolHeightmapFlattenMethod method, float dt)
    {
        EditorActions.QueueAction(new HeightmapFlattenAction
        {
            Bounds = bounds,
            BrushPosition = position.ToVector2(),
            BrushRadius = radius,
            BrushFalloff = falloff,
            BrushStrength = strength,
            BrushSensitivity = sensitivity,
            BrushTarget = target,
            FlattenMethod = method,
            DeltaTime = dt
        });
    }
    private void OnHeightmapSmooth(Bounds bounds, Vector3 position, float radius, float falloff, float strength, float target, EDevkitLandscapeToolHeightmapSmoothMethod method, float dt)
    {
        EditorActions.QueueAction(new HeightmapSmoothAction
        {
            Bounds = bounds,
            BrushPosition = position.ToVector2(),
            BrushRadius = radius,
            BrushFalloff = falloff,
            BrushStrength = strength,
            SmoothTarget = target,
            SmoothMethod = method,
            DeltaTime = dt
        });
    }
    private void OnPaint(Bounds bounds, Vector3 position, float radius, float falloff, float strength, float sensitivity, float target,
        bool useWeightTarget, bool autoSlope, bool autoFoundation, float autoMinAngleBegin, float autoMinAngleEnd,
        float autoMaxAngleBegin, float autoMaxAngleEnd, float autoRayLength, float autoRayRadius, ERayMask autoRayMask,
        bool isRemoving, AssetReference<LandscapeMaterialAsset> selectedMaterial, float dt)
    {
        EditorActions.QueueAction(new SplatmapPaintAction
        {
            Bounds = bounds,
            BrushPosition = position.ToVector2(),
            BrushRadius = radius,
            BrushFalloff = falloff,
            BrushStrength = strength,
            BrushSensitivity = sensitivity,
            BrushTarget = target,
            UseWeightTarget = useWeightTarget,
            UseAutoSlope = autoSlope,
            UseAutoFoundation = autoFoundation,
            AutoSlopeMinAngleBegin = autoMinAngleBegin,
            AutoSlopeMinAngleEnd = autoMinAngleEnd,
            AutoSlopeMaxAngleBegin = autoMaxAngleBegin,
            AutoSlopeMaxAngleEnd = autoMaxAngleEnd,
            AutoFoundationRayLength = autoRayLength,
            AutoFoundationRayRadius = autoRayRadius,
            AutoFoundationRayMask = autoRayMask,
            IsRemoving = isRemoving,
            SplatmapMaterial = selectedMaterial,
            DeltaTime = dt
        });
    }
    private void OnAutoPaint(Bounds bounds, Vector3 position, float radius, float falloff, float strength, float sensitivity, float target,
        bool useWeightTarget, bool autoSlope, bool autoFoundation, float autoMinAngleBegin, float autoMinAngleEnd,
        float autoMaxAngleBegin, float autoMaxAngleEnd, float autoRayLength, float autoRayRadius, ERayMask autoRayMask,
        bool isRemoving, AssetReference<LandscapeMaterialAsset> selectedMaterial, float dt)
    {
        EditorActions.QueueAction(new SplatmapPaintAction
        {
            IsAuto = true,
            Bounds = bounds,
            BrushPosition = position.ToVector2(),
            BrushRadius = radius,
            BrushFalloff = falloff,
            BrushStrength = strength,
            BrushSensitivity = sensitivity,
            BrushTarget = target,
            UseWeightTarget = useWeightTarget,
            UseAutoSlope = autoSlope,
            UseAutoFoundation = autoFoundation,
            AutoSlopeMinAngleBegin = autoMinAngleBegin,
            AutoSlopeMinAngleEnd = autoMinAngleEnd,
            AutoSlopeMaxAngleBegin = autoMaxAngleBegin,
            AutoSlopeMaxAngleEnd = autoMaxAngleEnd,
            AutoFoundationRayLength = autoRayLength,
            AutoFoundationRayRadius = autoRayRadius,
            AutoFoundationRayMask = autoRayMask,
            IsRemoving = isRemoving,
            SplatmapMaterial = selectedMaterial,
            DeltaTime = dt
        });
    }
    private void OnPaintSmooth(Bounds bounds, Vector3 position, float radius, float falloff, float strength, EDevkitLandscapeToolSplatmapSmoothMethod method, List<KeyValuePair<AssetReference<LandscapeMaterialAsset>, float>> averages, int sampleCount, AssetReference<LandscapeMaterialAsset> selectedMaterial, float dt)
    {
        EditorActions.QueueAction(new SplatmapSmoothAction
        {
            Bounds = bounds,
            BrushPosition = position.ToVector2(),
            BrushRadius = radius,
            BrushFalloff = falloff,
            BrushStrength = strength,
            SmoothMethod = method,
            Averages = averages,
            SampleCount = sampleCount,
            SplatmapMaterial = selectedMaterial,
            DeltaTime = dt
        });
    }
    private void OnPaintHole(Bounds bounds, Vector3 position, float radius, bool put)
    {
        EditorActions.QueueAction(new HolemapPaintAction
        {
            Bounds = bounds,
            BrushPosition = position.ToVector2(),
            BrushRadius = radius,
            IsFilling = put,
            DeltaTime = Time.deltaTime
        });
    }
    private void OnTileAdded(LandscapeTile tile)
    {
        EditorActions.QueueAction(new TileModifyAction
        {
            Coordinates = tile.coord,
            DeltaTime = Time.deltaTime
        });
    }
    private void OnTileDeleted(LandscapeTile tile)
    {
        EditorActions.QueueAction(new TileModifyAction
        {
            IsDelete = true,
            Coordinates = tile.coord,
            DeltaTime = Time.deltaTime
        });
    }
    private void OnSplatmapLayerMaterialsUpdate(LandscapeTile tile)
    {
        EditorActions.QueueAction(new TileModifyAction
        {
            IsDelete = true,
            Coordinates = tile.coord,
            DeltaTime = Time.deltaTime
        });
    }
#endif

}

public enum TerrainEditorType
{
    Heightmap,
    Splatmap,
    Holes,
    Tiles
}

public interface ITerrainAction : IAction
{
    TerrainEditorType EditorType { get; }
}

public interface IAutoSlope
{
    [ActionSetting(ActionSetting.AutoSlopeMinAngleBegin)]
    float AutoSlopeMinAngleBegin { get; set; }

    [ActionSetting(ActionSetting.AutoSlopeMinAngleEnd)]
    float AutoSlopeMinAngleEnd { get; set; }

    [ActionSetting(ActionSetting.AutoSlopeMaxAngleBegin)]
    float AutoSlopeMaxAngleBegin { get; set; }

    [ActionSetting(ActionSetting.AutoSlopeMaxAngleEnd)]
    float AutoSlopeMaxAngleEnd { get; set; }
}
public interface IAutoFoundation
{
    [ActionSetting(ActionSetting.AutoFoundationRayLength)]
    float AutoFoundationRayLength { get; set; }

    [ActionSetting(ActionSetting.AutoFoundationRayRadius)]
    float AutoFoundationRayRadius { get; set; }

    [ActionSetting(ActionSetting.AutoFoundationRayMask)]
    ERayMask AutoFoundationRayMask { get; set; }
}

[Action(ActionType.HeightmapRamp)]
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

[Action(ActionType.HeightmapAdjust)]
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

[Action(ActionType.HeightmapFlatten)]
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

[Action(ActionType.HeightmapSmooth)]
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

        Logger.LogInfo("Reverse patched " + Accessor.GetMethodInfo(SampleHeightPixelSmooth)?.Format() + ".");
    }
    public void Apply()
    {
        if (SmoothMethod == EDevkitLandscapeToolHeightmapSmoothMethod.PIXEL_AVERAGE)
        {
            Bounds readBounds = Bounds;
            readBounds.Expand(Landscape.HEIGHTMAP_WORLD_UNIT * 2f);
            Landscape.readHeightmap(readBounds, IntlHandleHeightmapReadSmoothPixelAverage);
        }
        LandscapeUtil.WriteHeightmapNoTransactions(Bounds, IntlHandleHeightmapWriteSmooth);
        if (SmoothMethod == EDevkitLandscapeToolHeightmapSmoothMethod.PIXEL_AVERAGE)
            ReleasePixelSmoothBuffer();
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
    private static void IntlHandleHeightmapReadSmoothPixelAverage(LandscapeCoord tileCoord, HeightmapCoord heightmapCoord, Vector3 worldPosition, float currentHeight)
    {
        if (!PixelSmoothBuffer.TryGetValue(tileCoord, out float[,] hm))
        {
            hm = LandscapeHeightmapCopyPool.claim();
            PixelSmoothBuffer.Add(tileCoord, hm);
        }
        hm[heightmapCoord.x, heightmapCoord.y] = currentHeight;
    }
    private static void ReleasePixelSmoothBuffer()
    {
        foreach (KeyValuePair<LandscapeCoord, float[,]> hmVal in PixelSmoothBuffer)
            LandscapeHeightmapCopyPool.release(hmVal.Value);
        PixelSmoothBuffer.Clear();
    }
    private static void SampleHeightPixelSmooth(object? instance, Vector3 worldPosition, ref int sampleCount, ref float sampleAverage)
    {
        IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            FieldInfo? field = typeof(TerrainEditor).GetField("pixelSmoothBuffer", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
                Logger.LogWarning("Unable to find field: TerrainEditor.pixelSmoothBuffer.");

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

[Action(ActionType.SplatmapPaint)]
[Action(ActionType.SplatmapAutoPaint, CreateMethod = nameof(CreateAutoPaint))]
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
            Logger.LogInfo("Reverse patched " + Accessor.GetMethodInfo(BlendSplatmapWeights)?.Format() + ".");
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
                Logger.LogInfo("Reverse patched " + Accessor.GetMethodInfo(GetSplatmapTargetMaterialLayerIndex)?.Format() + ".");
        }
        catch (NotImplementedException)
        {
            Logger.LogError("Failed to reverse patch " + Accessor.GetMethodInfo(GetSplatmapTargetMaterialLayerIndex)?.Format() + ".");
            DevkitServerModule.Fault();
        }
        catch (NullReferenceException)
        {
            Logger.LogInfo("Reverse patched " + Accessor.GetMethodInfo(GetSplatmapTargetMaterialLayerIndex)?.Format() + ".");
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
        Bounds = reader.ReadInflatedBounds();
        byte flags = reader.ReadUInt8();
        IsAuto = (flags & 1) != 0;
        if (!IsAuto)
        {
            UseWeightTarget = (flags & (1 << 1)) != 0;
            UseAutoSlope = (flags & (1 << 2)) != 0;
            UseAutoFoundation = (flags & (1 << 3)) != 0;
        }
        IsRemoving = (flags & (1 << 4)) != 0;
        BrushPosition = reader.ReadVector2();
        DeltaTime = reader.ReadFloat();
    }
    public void Write(ByteWriter writer)
    {
        writer.WriteHeightmapBounds(Bounds);
        byte flags = (byte)((IsAuto ? 1 : 0) | (IsRemoving ? 1 << 4 : 0));
        if (!IsAuto)
            flags |= (byte)((UseWeightTarget ? 1 << 1 : 0) | (UseAutoSlope ? 1 << 2 : 0) | (UseAutoFoundation ? 1 << 3 : 0));
        writer.Write(flags);
        writer.Write(BrushPosition);
        writer.Write(DeltaTime);
    }
    private void IntlHandleSplatmapWritePaint(LandscapeCoord tileCoord, SplatmapCoord splatmapCoord, Vector3 worldPosition, float[] currentWeights)
    {
        if (IsAuto && _assetFound && _asset == null)
            return;
        if (IsAuto && !_assetFound)
        {
            _assetFound = true;
            _asset = SplatmapMaterial.Find();
            if (_asset == null)
            {
                Logger.LogWarning($"[TERRAIN ACTIONS] Failed to find splatmap asset: {SplatmapMaterial.Format()} (from: {Instigator.Format()}).");
                return;
            }
        }
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
            bool consumedAuto = false;
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
                    worldPosition + new Vector3(0.0f, length, 0.0f),
                    radius, Vector3.down, FoundationHits,
                    length, mask, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < ct; ++i)
                {
                    ObjectAsset? asset = LevelObjects.getAsset(FoundationHits[i].transform);
                    if (asset is not { isSnowshoe: true })
                    {
                        consumedAuto = true;
                        targetWeight = !IsAuto && UseWeightTarget ? BrushTarget : 1f;
                        break;
                    }
                }
            }

            if (!consumedAuto && useAutoSlope && Landscape.getNormal(worldPosition, out Vector3 normal))
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
                    consumedAuto = true;
                }

                if (!consumedAuto)
                    return;

            }
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

[Action(ActionType.SplatmapSmooth)]
[EarlyTypeInit]
public sealed class SplatmapSmoothAction : ITerrainAction, IBrushRadius, IBrushFalloff, IBrushStrength, IDisposable, IAsset
{
    public ActionType Type => ActionType.SplatmapSmooth;
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
    public EDevkitLandscapeToolSplatmapSmoothMethod SmoothMethod { get; set; }
    public float BrushRadius { get; set; }
    public float BrushFalloff { get; set; }
    public float BrushStrength { get; set; }
    public float DeltaTime { get; set; }
    public List<KeyValuePair<AssetReference<LandscapeMaterialAsset>, float>>? Averages { get; set; }
    public int SampleCount { get; set; }
    public void Apply()
    {
        LandscapeUtil.WriteSplatmapNoTransactions(Bounds, IntlHandleSplatmapWriteSmooth);
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
        Bounds = reader.ReadInflatedBounds(out bool pixel);
        SmoothMethod = pixel ? EDevkitLandscapeToolSplatmapSmoothMethod.PIXEL_AVERAGE : EDevkitLandscapeToolSplatmapSmoothMethod.BRUSH_AVERAGE;
        BrushPosition = reader.ReadVector2();
        DeltaTime = reader.ReadFloat();
        Averages ??= ListPool<KeyValuePair<AssetReference<LandscapeMaterialAsset>, float>>.claim();
        List<KeyValuePair<byte, float>> proxies = ListPool<KeyValuePair<byte, float>>.claim();
        int len = reader.ReadUInt8();
        if (Averages.Capacity < len)
            Averages.Capacity = len;
        if (proxies.Capacity < len)
            proxies.Capacity = len;
        for (int i = 0; i < len; ++i)
            proxies.Add(new KeyValuePair<byte, float>(reader.ReadUInt8(), reader.ReadFloat()));

        List<Guid> index = ListPool<Guid>.claim();
        len = reader.ReadUInt8();
        if (index.Capacity < len)
            index.Capacity = len;
        for (int i = 0; i < len; ++i)
            index.Add(reader.ReadGuid());

        for (int i = 0; i < proxies.Count; ++i)
        {
            KeyValuePair<byte, float> proxy = proxies[i];
            Averages.Add(new KeyValuePair<AssetReference<LandscapeMaterialAsset>, float>(new AssetReference<LandscapeMaterialAsset>(index[proxy.Key]), proxy.Value));
        }

        ListPool<Guid>.release(index);
        ListPool<KeyValuePair<byte, float>>.release(proxies);
    }
    public void Write(ByteWriter writer)
    {
        writer.WriteHeightmapBounds(Bounds, SmoothMethod == EDevkitLandscapeToolSplatmapSmoothMethod.PIXEL_AVERAGE);
        writer.Write(BrushPosition);
        writer.Write(DeltaTime);
        int len = Math.Min(byte.MaxValue, Averages == null ? 0 : Averages.Count);
        writer.Write((byte)len);
        List<Guid> index = ListPool<Guid>.claim();
        for (int i = 0; i < len; ++i)
        {
            KeyValuePair<AssetReference<LandscapeMaterialAsset>, float> v = Averages![i];
            int ind = index.IndexOf(v.Key.GUID);
            if (ind == -1)
            {
                ind = index.Count;
                index.Add(v.Key.GUID);
            }
            writer.Write((byte)ind);
            writer.Write(v.Value);
        }
        len = Math.Min(byte.MaxValue, index.Count);
        writer.Write((byte)len);
        for (int i = 0; i < len; ++i)
            writer.Write(index[i]);
        ListPool<Guid>.release(index);
    }
    private void IntlHandleSplatmapWriteSmooth(LandscapeCoord tileCoord, SplatmapCoord splatmapCoord, Vector3 worldPosition, float[] currentWeights)
    {
        if (SampleCount < 1 || Averages == null)
            return;
        LandscapeTile? tile = Landscape.getTile(tileCoord);
        if (tile?.materials == null)
            return;
        float distance = new Vector2(worldPosition.x - BrushPosition.x, worldPosition.z - BrushPosition.y).sqrMagnitude;
        if (distance > BrushRadius * BrushRadius)
            return;
        distance = Mathf.Sqrt(distance) / BrushRadius;
        float speed = DeltaTime * BrushStrength * this.GetBrushAlpha(distance);
        float total = 0f;
        for (int i = 0; i < Landscape.SPLATMAP_LAYERS; ++i)
        {
            AssetReference<LandscapeMaterialAsset> r = tile.materials[i];
            for (int j = 0; j < Averages.Count; ++j)
            {
                if (Averages[j].Key == r)
                {
                    total += Averages[j].Value;
                    break;
                }
            }
        }

        total = 1f / total;
        for (int i = 0; i < Landscape.SPLATMAP_LAYERS; ++i)
        {
            AssetReference<LandscapeMaterialAsset> r = tile.materials[i];
            float weight = 0;
            for (int j = 0; j < Averages.Count; ++j)
            {
                if (Averages[j].Key == r)
                {
                    weight = Averages[j].Value / SampleCount * total;
                    break;
                }
            }
            currentWeights[i] += (weight - currentWeights[i]) * speed;
        }
    }

    public void Dispose()
    {
        if (Averages != null)
            ListPool<KeyValuePair<AssetReference<LandscapeMaterialAsset>, float>>.release(Averages);
    }
}

[Action(ActionType.HolesCut)]
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

[Action(ActionType.AddTile)]
[Action(ActionType.DeleteTile, CreateMethod = nameof(CreateDeleteTile))]
[EarlyTypeInit]
public sealed class TileModifyAction : ITerrainAction, ICoordinates
{
    public ActionType Type => IsDelete ? ActionType.DeleteTile : ActionType.AddTile;
    public TerrainEditorType EditorType => TerrainEditorType.Tiles;
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
        writer.Write(Coordinates.x);
        writer.Write(Coordinates.y);
    }
    public void Read(ByteReader reader)
    {
        IsDelete = reader.ReadBool();
        DeltaTime = reader.ReadFloat();
        Coordinates = new LandscapeCoord(reader.ReadInt32(), reader.ReadInt32());
    }

    [UsedImplicitly]
    private static TileModifyAction CreateDeleteTile() => new TileModifyAction { IsDelete = true };
}

[Action(ActionType.UpdateSplatmapLayers)]
[EarlyTypeInit]
public sealed class TileSplatmapLayersUpdateAction : ITerrainAction, ICoordinates
{
#if CLIENT
    private static readonly Func<Array?>? GetUILayers = UIAccessTools.CreateUIFieldGetterReturn<Array>(UI.EditorTerrainTiles, "layers", false);
    private static readonly Func<int>? GetSelectedLayer = UIAccessTools.CreateUIFieldGetterReturn<int>(UI.EditorTerrainTiles, "selectedLayerIndex", false);
    private static readonly Action<int>? SetSelectedLayerIndex = UIAccessTools.GenerateUICaller<Action<int>>(UI.EditorTerrainTiles, "SetSelectedLayerIndex", throwOnFailure: false);
    private static readonly Action<object>? CallUpdateSelectedTile =
        Accessor.GenerateInstanceCaller<Action<object>>(
            typeof(Provider).Assembly
                .GetType("SDG.Unturned.TerrainTileLayer")?
                .GetMethod("UpdateSelectedTile", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!
            , false, useFptrReconstruction: true);
#endif
    public ActionType Type => ActionType.UpdateSplatmapLayers;
    public TerrainEditorType EditorType => TerrainEditorType.Tiles;
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
        writer.Write(Coordinates.x);
        writer.Write(Coordinates.y);
        int amt = Math.Min(byte.MaxValue, Layers == null ? 0 : Layers.Length);
        writer.Write((byte)amt);
        for (int i = 0; i < amt; ++i)
            writer.Write(Layers![i].GUID);
    }
    public void Read(ByteReader reader)
    {
        DeltaTime = reader.ReadFloat();
        Coordinates = new LandscapeCoord(reader.ReadInt32(), reader.ReadInt32());
        int amt = reader.ReadInt32();
        if (Layers == null || Layers.Length != amt)
            Layers = new AssetReference<LandscapeMaterialAsset>[amt];
        for (int i = 0; i < amt; ++i)
            Layers[i] = new AssetReference<LandscapeMaterialAsset>(reader.ReadGuid());
    }
}