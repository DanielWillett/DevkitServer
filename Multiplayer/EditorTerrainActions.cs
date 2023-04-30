using System.Reflection;
using System.Reflection.Emit;
using DevkitServer.Util.Encoding;
using SDG.Framework.Devkit.Tools;
using SDG.Framework.Landscapes;
using SDG.Framework.Utilities;
using System.Text;
using DevkitServer.Patches;
#if CLIENT
using DevkitServer.Players.UI;
#endif
using HarmonyLib;
using SDG.Framework.Devkit;
using SDG.Framework.Foliage;

namespace DevkitServer.Multiplayer;

public partial class EditorTerrain
{
    private readonly BrushSettingsCollection?[] ReadBrushSettingsMask = new BrushSettingsCollection?[BrushFlagLength];
    private readonly SplatmapSettingsCollection?[] ReadSplatmapSettingsMask = new SplatmapSettingsCollection?[SplatmapFlagLength];
#if CLIENT
    private static readonly Pool<BrushSettingsCollection> BrushCollectionPool = new Pool<BrushSettingsCollection>();
    private static readonly Pool<SplatmapSettingsCollection> SplatmapCollectionPool = new Pool<SplatmapSettingsCollection>();
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
    static EditorTerrain()
    {
        ListPool<BrushSettingsCollection>.warmup((uint)(Provider.isServer ? 16 : 2));
#if CLIENT
        BrushCollectionPool.warmup((uint)(Provider.isServer ? 32 : 5));
        SplatmapCollectionPool.warmup((uint)(Provider.isServer ? 32 : 5));
#endif
    }
    public BrushSettingsCollection? GetBrushSettings(BrushValueFlags value)
    {
        for (int i = 0; i < BrushFlagLength; ++i)
            if ((value & (BrushValueFlags)(1 << i)) != 0)
                return i is < 0 or >= BrushFlagLength ? null : ReadBrushSettingsMask[i];

        return null;
    }
    private void SetBrushSettings(BrushSettingsCollection collection)
    {
        for (int i = 0; i < BrushFlagLength; ++i)
        {
            if ((collection.Flags & (BrushValueFlags)(1 << i)) != 0)
            {
                ref BrushSettingsCollection? col = ref ReadBrushSettingsMask[i];
#if CLIENT
                if (col != null)
                {
                    bool stillUsed = false;
                    for (int j = 0; j < BrushFlagLength; ++j)
                    {
                        if (i != j && ReadBrushSettingsMask[j] == col)
                        {
                            stillUsed = true;
                            break;
                        }
                    }
                    if (!stillUsed)
                        BrushCollectionPool.release(col);
                }
#endif
                col = collection;
            }
        }
        if ((collection.Flags & BrushValueFlags.SplatmapPaintInfo) != 0 && collection.Splatmap != null)
        {
            SplatmapSettingsCollection sc = collection.Splatmap;
            for (int i = 0; i < SplatmapFlagLength; ++i)
            {
                if ((sc.Flags & (SplatmapValueFlags)(1 << i)) != 0)
                {
                    ref SplatmapSettingsCollection? col = ref ReadSplatmapSettingsMask[i];
#if CLIENT
                    if (col != null)
                    {
                        bool stillUsed = false;
                        for (int j = 0; j < SplatmapFlagLength; ++j)
                        {
                            if (i != j && ReadSplatmapSettingsMask[j] == col)
                            {
                                stillUsed = true;
                                break;
                            }
                        }
                        if (!stillUsed)
                            SplatmapCollectionPool.release(col);
                    }
#endif
                    col = sc;
                }
            }
        }
    }
    public SplatmapSettingsCollection? GetSplatmapSettings(SplatmapValueFlags value)
    {
        for (int i = 0; i < SplatmapFlagLength; ++i)
            if ((value & (SplatmapValueFlags)(1 << i)) != 0)
                return i is < 0 or >= SplatmapFlagLength ? null : ReadSplatmapSettingsMask[i];

        return null;
    }
    public enum TerrainTransactionType : byte
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
        RemoveLevelObject
    }
    private const int BrushFlagLength = 7;
    [Flags]
    public enum BrushValueFlags : byte
    {
        None = 0,
        Radius = 1 << 0,
        Falloff = 1 << 1,
        Strength = 1 << 2,
        Sensitivity = 1 << 3,
        Target = 1 << 4,
        FoliageAsset = 1 << 5,
        TileCoordinates = 1 << 6,
        SplatmapPaintInfo = 1 << 7
    }
    private const int SplatmapFlagLength = 8;
    [Flags]
    public enum SplatmapValueFlags : byte
    {
        None = 0,
        AutoSlopeMinAngleBegin = 1 << 0,
        AutoSlopeMinAngleEnd = 1 << 1,
        AutoSlopeMaxAngleBegin = 1 << 2,
        AutoSlopeMaxAngleEnd = 1 << 3,
        AutoFoundationRayLength = 1 << 4,
        AutoFoundationRayRadius = 1 << 5,
        AutoFoundationRayMask = 1 << 6,
        SplatmapMaterial = 1 << 7
    }

    public interface ITerrainAction
    {
        TerrainTransactionType Type { get; }
        TerrainEditorType EditorType { get; }
        float DeltaTime { get; set; }
        CSteamID Instigator { get; set; }
        void Apply();
        void Write(ByteWriter writer);
        void Read(ByteReader reader);
    }

    public enum TerrainEditorType
    {
        Heightmap,
        Splatmap,
        Holes,
        Tiles,
        Resources,
        Foliage
    }
    public interface IBounds : ITerrainAction
    {
        Bounds Bounds { get; set; }
    }
    public interface IBrushRadius : ITerrainAction
    {
        float BrushRadius { get; set; }
    }
    public interface IBrushFalloff : ITerrainAction
    {
        float BrushFalloff { get; set; }
    }
    public interface IBrushStrength : ITerrainAction
    {
        float BrushStrength { get; set; }
    }
    public interface IBrushSensitivity : ITerrainAction
    {
        float BrushSensitivity { get; set; }
    }
    public interface IBrushTarget : ITerrainAction
    {
        float BrushTarget { get; set; }
    }
    public interface IAutoSlope : ITerrainAction
    {
        float AutoSlopeMinAngleBegin { get; set; }
        float AutoSlopeMinAngleEnd { get; set; }
        float AutoSlopeMaxAngleBegin { get; set; }
        float AutoSlopeMaxAngleEnd { get; set; }
    }
    public interface IAutoFoundation : ITerrainAction
    {
        float AutoFoundationRayLength { get; set; }
        float AutoFoundationRayRadius { get; set; }
        ERayMask AutoFoundationRayMask { get; set; }
    }
    public interface IBrushPosition : ITerrainAction
    {
        Vector2 BrushPosition { get; set; }
    }
    public interface ISplatmapMaterial : ITerrainAction
    {
        AssetReference<LandscapeMaterialAsset> SplatmapMaterial { get; set; }
    }
    public interface IFoliageAsset : ITerrainAction
    {
        AssetReference<FoliageInfoAsset> FoliageAsset { get; set; }
    }
    public interface ICoordinates : ITerrainAction
    {
        int CoordinateX { get; set; }
        int CoordinateY { get; set; }
    }

    public sealed class HeightmapRampAction : IBrushRadius, IBrushFalloff, IBounds
    {
        public TerrainTransactionType Type => TerrainTransactionType.HeightmapRamp;
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
            WriteHeightmapNoTransactions(Bounds, IntlHandleHeightmapWriteRamp);
        }
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
            float brushAlpha = GetBrushAlpha(distance);
            float a = (StartPosition.y + Landscape.TILE_HEIGHT / 2f) / Landscape.TILE_HEIGHT;
            float b = (EndPosition.y + Landscape.TILE_HEIGHT / 2f) / Landscape.TILE_HEIGHT;
            currentHeight = Mathf.Lerp(currentHeight, Mathf.Lerp(a, b, t), brushAlpha);
            return Mathf.Clamp01(currentHeight);
        }
    }
    public sealed class HeightmapAdjustAction : IBrushRadius, IBrushFalloff, IBrushStrength, IBrushSensitivity, IBrushPosition, IBounds
    {
        public TerrainTransactionType Type => TerrainTransactionType.HeightmapAdjust;
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
            WriteHeightmapNoTransactions(Bounds, IntlHandleHeightmapWriteAdjust);
        }
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
            float num = DeltaTime * BrushStrength * GetBrushAlpha(distance) * BrushSensitivity;
            if (Subtracting)
                num = -num;
            currentHeight += num;
            return currentHeight;
        }
    }
    public sealed class HeightmapFlattenAction : IBrushRadius, IBrushFalloff, IBrushStrength, IBrushSensitivity, IBrushTarget, IBrushPosition, IBounds
    {
        public TerrainTransactionType Type => TerrainTransactionType.HeightmapFlatten;
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
            WriteHeightmapNoTransactions(Bounds, IntlHandleHeightmapWriteFlatten);
        }
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
            float brushAlpha = GetBrushAlpha(distance);
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
    public sealed class HeightmapSmoothAction : IBrushRadius, IBrushFalloff, IBrushStrength, IBrushPosition, IBounds
    {
        private static readonly Dictionary<LandscapeCoord, float[,]> PixelSmoothBuffer = new Dictionary<LandscapeCoord, float[,]>(4);
        private static int _pixelSmoothSampleCount;
        private static float _pixelSmoothSampleTotal;
        public TerrainTransactionType Type => TerrainTransactionType.HeightmapSmooth;
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

        internal static void Patch()
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
                Landscape.readHeightmap(Bounds, IntlHandleHeightmapReadSmoothPixelAverage);
            WriteHeightmapNoTransactions(Bounds, IntlHandleHeightmapWriteSmooth);
            if (SmoothMethod == EDevkitLandscapeToolHeightmapSmoothMethod.PIXEL_AVERAGE)
                ReleasePixelSmoothBuffer();
        }
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
            IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
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
                        Logger.LogDebug("Replaced " + ins[i + 1].Format() + " with " + c2.Format() + ".");
                        continue;
                    }
                    yield return c;
                }
                if (!one)
                {
                    Logger.LogWarning("Unable to replace load of " + field?.Format() + " with " + buffer.Format() + ".");
                }
            }

            // make compiler happy
            _ = Transpiler(null!, null!);
            throw new NotImplementedException();
        }
        private float IntlHandleHeightmapWriteSmooth(LandscapeCoord tileCoord, HeightmapCoord heightmapCoord, Vector3 worldPosition, float currentHeight)
        {
            float distance = new Vector2(worldPosition.x - BrushPosition.x, worldPosition.z - BrushPosition.y).sqrMagnitude;
            if (distance > BrushRadius * BrushRadius)
                return currentHeight;
            distance = Mathf.Sqrt(distance) / BrushRadius;
            float brushAlpha = GetBrushAlpha(distance);
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
    public sealed class SplatmapPaintAction : IBrushRadius, IBrushFalloff, IBrushStrength, IBrushPosition, IBounds, IBrushTarget, IBrushSensitivity, IAutoSlope, IAutoFoundation, ISplatmapMaterial
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
        }
        internal static void Patch()
        {
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
        public TerrainTransactionType Type => IsAuto ? TerrainTransactionType.SplatmapAutoPaint : TerrainTransactionType.SplatmapPaint;
        public TerrainEditorType EditorType => TerrainEditorType.Splatmap;
        public CSteamID Instigator { get; set; }
        public Bounds Bounds { get; set; }
        public Vector2 BrushPosition { get; set; }
        public AssetReference<LandscapeMaterialAsset> SplatmapMaterial { get; set; }
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
            WriteSplatmapNoTransactions(Bounds, IntlHandleSplatmapWritePaint);
        }
        public void Read(ByteReader reader)
        {
            Bounds = reader.ReadInflatedBounds();
            byte flags = reader.ReadUInt8();
            IsAuto = (flags & 1) != 0;
            UseWeightTarget = (flags & (1 << 1)) != 0;
            UseAutoSlope = (flags & (1 << 2)) != 0;
            UseAutoFoundation = (flags & (1 << 3)) != 0;
            IsRemoving = (flags & (1 << 4)) != 0;
            BrushPosition = reader.ReadVector2();
            DeltaTime = reader.ReadFloat();
        }
        public void Write(ByteWriter writer)
        {
            writer.WriteHeightmapBounds(Bounds);
            byte flags = (byte)((IsAuto ? 1 : 0) | (UseWeightTarget ? 1 << 1 : 0) | (UseAutoSlope ? 1 << 2 : 0) | (UseAutoFoundation ? 1 << 3 : 0) | (IsRemoving ? 1 << 4 : 0));
            writer.Write(flags);
            writer.Write(BrushPosition);
            writer.Write(DeltaTime);
        }
        private void IntlHandleSplatmapWritePaint(LandscapeCoord tileCoord, SplatmapCoord splatmapCoord, Vector3 worldPosition, float[] currentWeights)
        {
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
            float targetWeight = 0.5f;
            if (UseAutoFoundation || UseAutoSlope)
            {
                bool consumedAuto = false;
                if (UseAutoFoundation)
                {
                    int ct = Physics.SphereCastNonAlloc(
                        worldPosition + new Vector3(0.0f, AutoFoundationRayLength, 0.0f),
                        AutoFoundationRayRadius, Vector3.down, FoundationHits,
                        AutoFoundationRayLength, (int)AutoFoundationRayMask, QueryTriggerInteraction.Ignore);
                    for (int i = 0; i < ct; ++i)
                    {
                        ObjectAsset? asset = LevelObjects.getAsset(FoundationHits[i].transform);
                        if (asset is not { isSnowshoe: true })
                        {
                            consumedAuto = true;
                            targetWeight = UseWeightTarget ? BrushTarget : 1f;
                            break;
                        }
                    }
                }

                if (!consumedAuto && UseAutoSlope && Landscape.getNormal(worldPosition, out Vector3 normal))
                {
                    float angle = Vector3.Angle(Vector3.up, normal);
                    if (angle >= AutoSlopeMinAngleBegin && angle <= AutoSlopeMaxAngleEnd)
                    {
                        targetWeight = angle >= AutoSlopeMinAngleEnd
                            ? (angle <= AutoSlopeMaxAngleBegin
                                ? 1f
                                : (1f - Mathf.InverseLerp(AutoSlopeMaxAngleBegin, AutoSlopeMaxAngleEnd, angle)))
                            : (Mathf.InverseLerp(AutoSlopeMinAngleBegin, AutoSlopeMinAngleEnd, angle));
                        consumedAuto = true;
                    }

                    if (!consumedAuto)
                        return;
                    
                }
            }
            else if (IsAuto)
                return;
            else
                targetWeight = !UseWeightTarget ? BrushTarget : (IsRemoving ? 0f : 1f);

            float speed = DeltaTime * BrushStrength * GetBrushAlpha(distance) * BrushSensitivity;
            
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
    }
    public sealed class SplatmapSmoothAction : IBounds, IBrushPosition, IBrushRadius, IBrushFalloff, IBrushStrength, IDisposable, ISplatmapMaterial
    {
        public TerrainTransactionType Type => TerrainTransactionType.SplatmapSmooth;
        public TerrainEditorType EditorType => TerrainEditorType.Splatmap;
        public CSteamID Instigator { get; set; }
        public Bounds Bounds { get; set; }
        public Vector2 BrushPosition { get; set; }
        public AssetReference<LandscapeMaterialAsset> SplatmapMaterial { get; set; }
        public EDevkitLandscapeToolSplatmapSmoothMethod SmoothMethod { get; set; }
        public float BrushRadius { get; set; }
        public float BrushFalloff { get; set; }
        public float BrushStrength { get; set; }
        public float DeltaTime { get; set; }
        public List<KeyValuePair<AssetReference<LandscapeMaterialAsset>, float>>? Averages { get; set; }
        public int SampleCount { get; set; }
        public void Apply()
        {
            WriteSplatmapNoTransactions(Bounds, IntlHandleSplatmapWriteSmooth);
        }
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
            float speed = DeltaTime * BrushStrength * GetBrushAlpha(distance);
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
    public sealed class HolemapPaintAction : IBounds, IBrushPosition, IBrushRadius
    {
        public TerrainTransactionType Type => TerrainTransactionType.HolesCut;
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
            WriteHolesNoTransactions(Bounds, IntlHandleHolesWriteCut);
        }
        private bool IntlHandleHolesWriteCut(Vector3 worldPosition, bool currentlyVisible)
        {
            if (currentlyVisible == IsFilling || new Vector2(worldPosition.x - BrushPosition.x, worldPosition.z - BrushPosition.y).sqrMagnitude > BrushRadius * BrushRadius)
                return currentlyVisible;
            return IsFilling;
        }
    }
    public sealed class TileModifyAction : ICoordinates
    {
        public TerrainTransactionType Type => IsDelete ? TerrainTransactionType.DeleteTile : TerrainTransactionType.AddTile;
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
    }
    public sealed class TileSplatmapLayersUpdateAction : ICoordinates
    {
        public TerrainTransactionType Type => TerrainTransactionType.UpdateSplatmapLayers;
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

#if CLIENT      // update tile UI
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
    [EarlyTypeInit]
    public sealed class AddFoliageToSurfaceAction : IFoliageAsset
    {
        private static readonly Action<FoliageInfoAsset, Vector3, Quaternion, Vector3, bool>? ExecuteAddFoliage
            = Accessor.GenerateInstanceCaller<FoliageInfoAsset, Action<FoliageInfoAsset, Vector3, Quaternion, Vector3, bool>>("addFoliage", throwOnError: false);
        public TerrainTransactionType Type => TerrainTransactionType.AddFoliageToSurface;
        public TerrainEditorType EditorType => TerrainEditorType.Foliage;
        public CSteamID Instigator { get; set; }
        public float DeltaTime { get; set; }
        public AssetReference<FoliageInfoAsset> FoliageAsset { get; set; }
        public Vector3 Position { get; set; }
        public Quaternion Rotation { get; set; }
        public Vector3 Scale { get; set; }
        public bool ClearWhenBaked { get; set; }
        public void Apply()
        {
            if (ExecuteAddFoliage == null)
            {
                Logger.LogWarning("Unable to add foliage asset: " + FoliageAsset.Format() + ", reflection failure on load.");
                return;
            }
            if (FoliageAsset.Find() is { } asset)
            {
                ExecuteAddFoliage(asset, Position, Rotation, Scale, ClearWhenBaked);
            }
            else
            {
                Logger.LogWarning("Unknown foliage asset: " + FoliageAsset.Format() + ".");
            }
        }
        public void Write(ByteWriter writer)
        {
            writer.Write(DeltaTime);
            bool nonUniformScale = Scale == Vector3.one;
            byte flag = (byte)((nonUniformScale ? 2 : 0) | (ClearWhenBaked ? 1 : 0));
            writer.Write(flag);
            writer.Write(Position);
            writer.Write(Rotation);
            if (nonUniformScale)
                writer.Write(Scale);
        }
        public void Read(ByteReader reader)
        {
            DeltaTime = reader.ReadFloat();
            byte flag = reader.ReadUInt8();
            ClearWhenBaked = (flag & 1) != 0;
            bool nonUniformScale = (flag & 2) != 0;
            Position = reader.ReadVector3();
            Rotation = reader.ReadQuaternion();
            Scale = nonUniformScale ? reader.ReadVector3() : Vector3.one;
        }
    }
    public sealed class RemoveFoliageInstancesAction : ICoordinates, IBrushRadius, IBrushFalloff, IFoliageAsset
    {
        public TerrainTransactionType Type => TerrainTransactionType.RemoveFoliageInstances;
        public TerrainEditorType EditorType => TerrainEditorType.Foliage;
        public CSteamID Instigator { get; set; }
        public Vector3 BrushPosition { get; set; }
        public AssetReference<FoliageInstancedMeshInfoAsset> FoliageAsset { get; set; }
        AssetReference<FoliageInfoAsset> IFoliageAsset.FoliageAsset
        {
            get => new AssetReference<FoliageInfoAsset>(FoliageAsset.GUID);
            set => FoliageAsset = new AssetReference<FoliageInstancedMeshInfoAsset>(value.GUID);
        }
        public float DeltaTime { get; set; }
        public float BrushRadius { get; set; }
        public float BrushFalloff { get; set; }
        public FoliageCoord Coordinates { get; set; }
        public int SampleCount { get; set; }
        public bool AllowRemoveBaked { get; set; }
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
            FoliageTile tile = FoliageSystem.getTile(Coordinates);

            if (tile == null)
            {
                Logger.LogWarning("Unknown foliage tile: " + Coordinates + ".");
                return;
            }
            if (!tile.instances.TryGetValue(FoliageAsset, out FoliageInstanceList list))
            {
                Logger.LogWarning("Tile missing foliage " + (FoliageAsset.Find()?.FriendlyName ?? "NULL") + ": " + Coordinates.Format() + ".");
                return;
            }
            bool flag1 = false;
            float sqrBrushRadius = BrushRadius * BrushRadius;
            float sqrBrushFalloffRadius = sqrBrushRadius * BrushFalloff * BrushFalloff;
            int sampleCount = SampleCount;

            // FoliageEditor.removeInstances
            for (int index1 = list.matrices.Count - 1; index1 >= 0; --index1)
            {
                List<Matrix4x4> matrix = list.matrices[index1];
                List<bool> boolList = list.clearWhenBaked[index1];
                for (int index2 = matrix.Count - 1; index2 >= 0; --index2)
                {
                    if (!boolList[index2] || AllowRemoveBaked)
                    {
                        Vector3 position = matrix[index2].GetPosition();
                        float sqrMagnitude = (position - BrushPosition).sqrMagnitude;
                        if (sqrMagnitude < sqrBrushRadius)
                        {
                            bool outOfFalloff = sqrMagnitude < sqrBrushFalloffRadius;
                            if (outOfFalloff && sampleCount > 0)
                            {
                                tile.removeInstance(list, index1, index2);
                                --sampleCount;
                                flag1 = true;
                            }
                        }
                    }
                }
            }
            if (!flag1)
                return;
            LevelHierarchy.MarkDirty();
        }
        public void Write(ByteWriter writer)
        {
            writer.Write(DeltaTime);
        }
        public void Read(ByteReader reader)
        {
            DeltaTime = reader.ReadFloat();
        }
    }
    public sealed class RemoveResourceSpawnpointAction : IFoliageAsset
    {
        public TerrainTransactionType Type => TerrainTransactionType.RemoveResourceSpawnpoint;
        public TerrainEditorType EditorType => TerrainEditorType.Foliage;
        public CSteamID Instigator { get; set; }
        public float DeltaTime { get; set; }
        public Vector3 ResourcePosition { get; set; }
        public AssetReference<ResourceAsset> FoliageAsset { get; set; }
        AssetReference<FoliageInfoAsset> IFoliageAsset.FoliageAsset
        {
            get => new AssetReference<FoliageInfoAsset>(FoliageAsset.GUID);
            set => FoliageAsset = new AssetReference<ResourceAsset>(value.GUID);
        }
        public void Apply()
        {
            Vector3 pos = ResourcePosition;
            if (Regions.tryGetCoordinate(pos, out byte x, out byte y))
            {
                List<ResourceSpawnpoint> region = LevelGround.trees[x, y];
                for (int i = 0; i < region.Count; ++i)
                {
                    ResourceSpawnpoint sp = region[i];
                    if (sp.point.IsNearlyEqual(pos) && sp.asset.GUID == FoliageAsset.GUID)
                    {
                        sp.destroy();
                        region.RemoveAt(i);
                        return;
                    }
                }

                Logger.LogWarning("Resource not found: " + (FoliageAsset.Find()?.FriendlyName ?? "NULL").Format() + " at " + ResourcePosition.Format() + ".");
            }
            else
            {
                Logger.LogWarning("Resource out of bounds: " + (FoliageAsset.Find()?.FriendlyName ?? "NULL").Format() + " at " + ResourcePosition.Format() + ".");
            }
        }

        public void Write(ByteWriter writer)
        {
            writer.Write(DeltaTime);
            writer.Write(ResourcePosition);
        }
        public void Read(ByteReader reader)
        {
            DeltaTime = reader.ReadFloat();
            ResourcePosition = reader.ReadVector3();
        }
    }
    public sealed class RemoveLevelObjectAction : ICoordinates
    {
        public TerrainTransactionType Type => TerrainTransactionType.RemoveLevelObject;
        public TerrainEditorType EditorType => TerrainEditorType.Foliage;
        public CSteamID Instigator { get; set; }
        public float DeltaTime { get; set; }
        public uint InstanceId { get; set; }
        public int CoordinateX { get; set; }
        public int CoordinateY { get; set; }
        public void Apply()
        {
            List<LevelObject> region = LevelObjects.objects[CoordinateX, CoordinateY];
            for (int i = 0; i < region.Count; ++i)
            {
                LevelObject obj = region[i];
                if (obj.instanceID == InstanceId)
                {
                    LevelObjects.removeObject(obj.transform);
                    return;
                }
            }

            Logger.LogWarning("Object not found: #" + InstanceId.Format() + " (" + CoordinateX.Format() + ", " + CoordinateY.Format() + ").");
        }

        public void Write(ByteWriter writer)
        {
            writer.Write(DeltaTime);
            writer.Write(InstanceId);
        }
        public void Read(ByteReader reader)
        {
            DeltaTime = reader.ReadFloat();
            InstanceId = reader.ReadUInt32();
        }
    }

    public sealed class BrushSettingsCollection
    {
        public BrushValueFlags Flags { get; set; }
        public byte StartIndex { get; set; }
        public float Radius { get; set; }
        public float Falloff { get; set; }
        public float Strength { get; set; }
        public float Sensitivity { get; set; }
        public float Target { get; set; }
        public int CoordinateX { get; set; }
        public int CoordinateY { get; set; }
        public AssetReference<FoliageInfoAsset> FoliageAsset { get; set; }
        public SplatmapSettingsCollection? Splatmap { get; set; }
        public BrushSettingsCollection Reset()
        {
            Flags = default;
            return this;
        }
        public void WithRadius(float radius)
        {
            Flags |= BrushValueFlags.Radius;
            Radius = radius;
        }
        public void WithFalloff(float falloff)
        {
            Flags |= BrushValueFlags.Falloff;
            Falloff = falloff;
        }
        public void WithStrength(float strength)
        {
            Flags |= BrushValueFlags.Strength;
            Strength = strength;
        }
        public void WithSensitivity(float sensitivity)
        {
            Flags |= BrushValueFlags.Sensitivity;
            Sensitivity = sensitivity;
        }
        public void WithTarget(float target)
        {
            Flags |= BrushValueFlags.Target;
            Target = target;
        }
        public void WithFoliageAsset(AssetReference<FoliageInfoAsset> material)
        {
            Flags |= BrushValueFlags.FoliageAsset;
            FoliageAsset = material.isValid ? material : default;
        }
        public void WithCoordinates(int x, int y)
        {
            Flags |= BrushValueFlags.TileCoordinates;
            CoordinateX = x;
            CoordinateY = y;
        }
        public void WithSplatmapInfo(SplatmapSettingsCollection? collection)
        {
            if (collection == null)
                Flags &= ~BrushValueFlags.SplatmapPaintInfo;
            else
                Flags |= BrushValueFlags.SplatmapPaintInfo;
#if CLIENT
            if (collection != Splatmap && Splatmap != null)
                SplatmapCollectionPool.release(Splatmap);
#endif
            Splatmap = collection;
        }
        public void Read(ByteReader reader)
        {
            StartIndex = reader.ReadUInt8();
            Flags = reader.ReadEnum<BrushValueFlags>();
            if ((Flags & BrushValueFlags.Radius) != 0)
                Radius = reader.ReadFloat();
            else
                Radius = 0;
            if ((Flags & BrushValueFlags.Falloff) != 0)
                Falloff = reader.ReadFloat();
            else
                Falloff = 0;
            if ((Flags & BrushValueFlags.Strength) != 0)
                Strength = reader.ReadFloat();
            else
                Strength = 0;
            if ((Flags & BrushValueFlags.Sensitivity) != 0)
                Sensitivity = reader.ReadFloat();
            else
                Sensitivity = 0;
            if ((Flags & BrushValueFlags.Target) != 0)
                Target = reader.ReadFloat();
            else
                Target = 0;
            if ((Flags & BrushValueFlags.FoliageAsset) != 0)
                FoliageAsset = new AssetReference<FoliageInfoAsset>(reader.ReadGuid());
            else
                FoliageAsset = default;
            if ((Flags & BrushValueFlags.TileCoordinates) != 0)
            {
                CoordinateX = reader.ReadInt32();
                CoordinateY = reader.ReadInt32();
            }
            else CoordinateX = CoordinateY = 0;
            if ((Flags & BrushValueFlags.SplatmapPaintInfo) != 0)
            {
#if CLIENT
                Splatmap ??= SplatmapCollectionPool.claim().Reset();
#else
                Splatmap ??= new SplatmapSettingsCollection();
#endif
                Splatmap.Read(reader);
            }
        }
        public void Write(ByteWriter writer)
        {
            writer.Write(StartIndex);
            writer.Write(Flags);
            if ((Flags & BrushValueFlags.Radius) != 0)
                writer.Write(Radius);
            if ((Flags & BrushValueFlags.Falloff) != 0)
                writer.Write(Falloff);
            if ((Flags & BrushValueFlags.Strength) != 0)
                writer.Write(Strength);
            if ((Flags & BrushValueFlags.Sensitivity) != 0)
                writer.Write(Sensitivity);
            if ((Flags & BrushValueFlags.Target) != 0)
                writer.Write(Target);
            if ((Flags & BrushValueFlags.FoliageAsset) != 0)
                writer.Write(FoliageAsset.GUID);
            if ((Flags & BrushValueFlags.TileCoordinates) != 0)
            {
                writer.Write(CoordinateX);
                writer.Write(CoordinateY);
            }
            if ((Flags & BrushValueFlags.SplatmapPaintInfo) != 0)
            {
                if (Splatmap == null)
                    writer.Write(SplatmapValueFlags.None);
                else
                    Splatmap.Write(writer);
            }
        }
        public override string ToString()
        {
            StringBuilder bld = new StringBuilder();
            if ((Flags & BrushValueFlags.Radius) != 0)
                bld.Append(" Radius: ").Append(Radius);
            if ((Flags & BrushValueFlags.Falloff) != 0)
                bld.Append(" Falloff: ").Append(Falloff);
            if ((Flags & BrushValueFlags.Strength) != 0)
                bld.Append(" Strength: ").Append(Strength);
            if ((Flags & BrushValueFlags.Sensitivity) != 0)
                bld.Append(" Sensitivity: ").Append(Sensitivity);
            if ((Flags & BrushValueFlags.Target) != 0)
                bld.Append(" Target: ").Append(Target);
            if ((Flags & BrushValueFlags.FoliageAsset) != 0)
                bld.Append(" Foliage Asset: ").Append(FoliageAsset.GUID.ToString("N"));
            if ((Flags & BrushValueFlags.TileCoordinates) != 0)
                bld.Append(" Tile Coordinates: (").Append(CoordinateX).Append(", ").Append(CoordinateY).Append(')');
            if ((Flags & BrushValueFlags.SplatmapPaintInfo) != 0 && Splatmap != null)
                bld.Append(" Splatmap: ").Append(Splatmap);

            return bld.ToString();
        }
    }
    public sealed class SplatmapSettingsCollection
    {
        public SplatmapValueFlags Flags { get; set; }
        public float AutoFoundationRayLength { get; set; }
        public float AutoFoundationRayRadius { get; set; }
        public float AutoSlopeMinAngleBegin { get; set; }
        public float AutoSlopeMinAngleEnd { get; set; }
        public float AutoSlopeMaxAngleBegin { get; set; }
        public float AutoSlopeMaxAngleEnd { get; set; }
        public ERayMask AutoFoundationRayMask { get; set; }
        public AssetReference<LandscapeMaterialAsset> SplatmapMaterial { get; set; }
        public SplatmapSettingsCollection Reset()
        {
            Flags = default;
            return this;
        }
        public SplatmapSettingsCollection WithAutoFoundationRayLength(float rayLength)
        {
            Flags |= SplatmapValueFlags.AutoFoundationRayLength;
            AutoFoundationRayLength = rayLength;
            return this;
        }
        public SplatmapSettingsCollection WithAutoFoundationRayRadius(float rayRadius)
        {
            Flags |= SplatmapValueFlags.AutoFoundationRayRadius;
            AutoFoundationRayRadius = rayRadius;
            return this;
        }
        public SplatmapSettingsCollection WithAutoFoundationRayMask(ERayMask mask)
        {
            Flags |= SplatmapValueFlags.AutoFoundationRayMask;
            AutoFoundationRayMask = mask;
            return this;
        }
        public SplatmapSettingsCollection WithAutoSlopeMinAngleBegin(float minAngleBegin)
        {
            Flags |= SplatmapValueFlags.AutoSlopeMinAngleBegin;
            AutoSlopeMinAngleBegin = minAngleBegin;
            return this;
        }
        public SplatmapSettingsCollection WithAutoSlopeMinAngleEnd(float minAngleEnd)
        {
            Flags |= SplatmapValueFlags.AutoSlopeMinAngleEnd;
            AutoSlopeMinAngleEnd = minAngleEnd;
            return this;
        }
        public SplatmapSettingsCollection WithAutoSlopeMaxAngleBegin(float maxAngleBegin)
        {
            Flags |= SplatmapValueFlags.AutoSlopeMaxAngleBegin;
            AutoSlopeMaxAngleBegin = maxAngleBegin;
            return this;
        }
        public SplatmapSettingsCollection WithAutoSlopeMaxAngleEnd(float maxAngleEnd)
        {
            Flags |= SplatmapValueFlags.AutoSlopeMaxAngleEnd;
            AutoSlopeMaxAngleEnd = maxAngleEnd;
            return this;
        }
        public SplatmapSettingsCollection WithSplatmapMaterial(AssetReference<LandscapeMaterialAsset> material)
        {
            Flags |= SplatmapValueFlags.SplatmapMaterial;
            SplatmapMaterial = material.isValid ? material : default;
            return this;
        }
        public void Read(ByteReader reader)
        {
            Flags = reader.ReadEnum<SplatmapValueFlags>();

            if ((Flags & SplatmapValueFlags.SplatmapMaterial) != 0)
                SplatmapMaterial = new AssetReference<LandscapeMaterialAsset>(reader.ReadGuid());
            else SplatmapMaterial = default;

            if ((Flags & SplatmapValueFlags.AutoFoundationRayLength) != 0)
                AutoFoundationRayLength = reader.ReadFloat();
            else AutoFoundationRayLength = 0;
            if ((Flags & SplatmapValueFlags.AutoFoundationRayRadius) != 0)
                AutoFoundationRayRadius = reader.ReadFloat();
            else AutoFoundationRayRadius = 0;
            if ((Flags & SplatmapValueFlags.AutoFoundationRayMask) != 0)
                AutoFoundationRayMask = (ERayMask)reader.ReadInt32();
            else AutoFoundationRayMask = 0;

            if ((Flags & SplatmapValueFlags.AutoSlopeMinAngleBegin) != 0)
                AutoSlopeMinAngleBegin = reader.ReadFloat();
            else AutoSlopeMinAngleBegin = 0;
            if ((Flags & SplatmapValueFlags.AutoSlopeMinAngleEnd) != 0)
                AutoSlopeMinAngleEnd = reader.ReadFloat();
            else AutoSlopeMinAngleEnd = 0;
            if ((Flags & SplatmapValueFlags.AutoSlopeMaxAngleBegin) != 0)
                AutoSlopeMaxAngleBegin = reader.ReadFloat();
            else AutoSlopeMaxAngleBegin = 0;
            if ((Flags & SplatmapValueFlags.AutoSlopeMaxAngleEnd) != 0)
                AutoSlopeMaxAngleEnd = reader.ReadFloat();
            else AutoSlopeMaxAngleEnd = 0;
        }
        public void Write(ByteWriter writer)
        {
            writer.Write(Flags);

            if ((Flags & SplatmapValueFlags.SplatmapMaterial) != 0)
                writer.Write(SplatmapMaterial.GUID);

            if ((Flags & SplatmapValueFlags.AutoFoundationRayLength) != 0)
                writer.Write(AutoFoundationRayLength);
            if ((Flags & SplatmapValueFlags.AutoFoundationRayRadius) != 0)
                writer.Write(AutoFoundationRayRadius);
            if ((Flags & SplatmapValueFlags.AutoFoundationRayMask) != 0)
                writer.Write((int)AutoFoundationRayMask);

            if ((Flags & SplatmapValueFlags.AutoSlopeMinAngleBegin) != 0)
                writer.Write(AutoSlopeMinAngleBegin);
            if ((Flags & SplatmapValueFlags.AutoSlopeMinAngleEnd) != 0)
                writer.Write(AutoSlopeMinAngleEnd);
            if ((Flags & SplatmapValueFlags.AutoSlopeMaxAngleBegin) != 0)
                writer.Write(AutoSlopeMaxAngleBegin);
            if ((Flags & SplatmapValueFlags.AutoSlopeMaxAngleEnd) != 0)
                writer.Write(AutoSlopeMaxAngleEnd);
        }
        public override string ToString()
        {
            StringBuilder bld = new StringBuilder();
            if ((Flags & SplatmapValueFlags.AutoSlopeMinAngleBegin) != 0)
                bld.Append(" Min Angle Begin: ").Append(AutoSlopeMinAngleBegin);
            if ((Flags & SplatmapValueFlags.AutoSlopeMinAngleEnd) != 0)
                bld.Append(" Min Angle End: ").Append(AutoSlopeMinAngleEnd);
            if ((Flags & SplatmapValueFlags.AutoSlopeMaxAngleBegin) != 0)
                bld.Append(" Max Angle Begin: ").Append(AutoSlopeMaxAngleBegin);
            if ((Flags & SplatmapValueFlags.AutoSlopeMaxAngleEnd) != 0)
                bld.Append(" Max Angle End: ").Append(AutoSlopeMaxAngleEnd);
            if ((Flags & SplatmapValueFlags.AutoFoundationRayLength) != 0)
                bld.Append(" Ray Length: ").Append(AutoFoundationRayLength);
            if ((Flags & SplatmapValueFlags.AutoFoundationRayRadius) != 0)
                bld.Append(" Ray Radius: ").Append(AutoFoundationRayRadius);
            if ((Flags & SplatmapValueFlags.AutoFoundationRayMask) != 0)
                bld.Append(" Ray Mask: ").Append(AutoFoundationRayMask.ToString());
            if ((Flags & SplatmapValueFlags.SplatmapMaterial) != 0)
                bld.Append(" Material: ").Append(SplatmapMaterial);

            return bld.ToString();
        }
    }
}
