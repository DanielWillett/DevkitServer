using System.Text;
using DevkitServer.Patches;
using DevkitServer.Players;
using DevkitServer.Util.Encoding;
using SDG.Framework.Devkit.Tools;
using SDG.Framework.Landscapes;
using SDG.Framework.Utilities;

namespace DevkitServer.Multiplayer;

public partial class EditorTerrain
{
    private static readonly Pool<BrushSettingsCollection> BrushCollectionPool = new Pool<BrushSettingsCollection>();
    private static readonly Pool<SplatmapSettingsCollection> SplatmapCollectionPool = new Pool<SplatmapSettingsCollection>();
    private readonly BrushSettingsCollection?[] ReadBrushSettingsMask = new BrushSettingsCollection?[BrushFlagLength];
    private readonly SplatmapSettingsCollection?[] ReadSplatmapSettingsMask = new SplatmapSettingsCollection?[SplatmapFlagLength];
    static EditorTerrain()
    {
        ListPool<BrushSettingsCollection>.warmup((uint)(Provider.isServer ? 16 : 2));
        BrushCollectionPool.warmup((uint)(Provider.isServer ? 240 : 30));
        SplatmapCollectionPool.warmup((uint)(Provider.isServer ? 240 : 30));
        HeightmapRampAction.Pool.warmup((uint)(Provider.isServer ? 16 : 2));
    }
    private BrushSettingsCollection? GetBrushSettings(BrushValueFlags value)
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
                ReadBrushSettingsMask[i] = collection;
        }
        if ((collection.Flags & BrushValueFlags.SplatmapPaintInfo) != 0 && collection.Splatmap != null)
        {
            SplatmapSettingsCollection sc = collection.Splatmap;
            for (int i = 0; i < SplatmapFlagLength; ++i)
            {
                if ((sc.Flags & (SplatmapValueFlags)(1 << i)) != 0)
                    ReadSplatmapSettingsMask[i] = sc;
            }
        }
    }
    private void SetBrushSettings(BrushValueFlags value, BrushSettingsCollection? settings)
    {
        for (int i = 0; i < BrushFlagLength; ++i)
        {
            if ((value & (BrushValueFlags)(1 << i)) != 0)
            {
                if (i is not (< 0 or >= BrushFlagLength))
                    ReadBrushSettingsMask[i] = settings;
                break;
            }
        }
    }
    private SplatmapSettingsCollection? GetSplatmapSettings(SplatmapValueFlags value)
    {
        for (int i = 0; i < SplatmapFlagLength; ++i)
            if ((value & (SplatmapValueFlags)(1 << i)) != 0)
                return i is < 0 or >= SplatmapFlagLength ? null : ReadSplatmapSettingsMask[i];

        return null;
    }
    private void SetSplatmapSettings(SplatmapValueFlags value, SplatmapSettingsCollection? settings)
    {
        for (int i = 0; i < SplatmapFlagLength; ++i)
        {
            if ((value & (SplatmapValueFlags)(1 << i)) != 0)
            {
                if (i is not (< 0 or >= SplatmapFlagLength))
                    ReadSplatmapSettingsMask[i] = settings;
                break;
            }
        }
    }
    public enum TerrainTransactionType : byte
    {
        HeightmapRamp,
        HeightmapAdjust,
        HeightmapFlatten,
        HeightmapSmooth,
        SplatmapPaint,
        SplatmapAutoPaint,
        SplatmapSmooth
    }
    private const int BrushFlagLength = 5;
    [Flags]
    public enum BrushValueFlags : byte
    {
        None = 0,
        Radius = 1 << 0,
        Falloff = 1 << 1,
        Strength = 1 << 2,
        Sensitivity = 1 << 3,
        Target = 1 << 4,
        SplatmapPaintInfo = 1 << 7
    }
    private const int SplatmapFlagLength = 4;
    [Flags]
    public enum SplatmapValueFlags : byte
    {
        None = 0,
        AutoFoundationInfo = 1 << 0,
        AutoSlopeInfo = 1 << 1,
        UseAutoFoundation = 1 << 6,
        UseAutoSlope = 1 << 7
    }

    public interface ITerrainAction
    {
        TerrainTransactionType Type { get; }
        TerrainEditorType EditorType { get; }
        void Apply();
        void Write(ByteWriter writer);
        void Read(ByteReader reader);
    }

    public enum TerrainEditorType
    {
        Heightmap,
        Splatmap,
        Resources
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
    public interface IDeltaTime : ITerrainAction
    {
        float DeltaTime { get; set; }
    }
    public interface IBrushPosition : ITerrainAction
    {
        Vector2 BrushPosition { get; set; }
    }

    public sealed class HeightmapRampAction : IBrushRadius, IBrushFalloff, IBounds
    {
        internal static readonly Pool<HeightmapRampAction> Pool = new Pool<HeightmapRampAction>();

        public TerrainTransactionType Type => TerrainTransactionType.HeightmapRamp;
        public TerrainEditorType EditorType => TerrainEditorType.Heightmap;
        public Bounds Bounds { get; set; }
        public Vector3 StartPosition { get; set; }
        public Vector3 EndPosition { get; set; }
        public float BrushRadius { get; set; }
        public float BrushFalloff { get; set; }

        public void Apply()
        {
            WriteHeightmapNoTransactions(Bounds, IntlHandleHeightmapWriteRamp);
        }
        public void Read(ByteReader reader)
        {
            Bounds = reader.ReadInflatedBounds();
            StartPosition = reader.ReadVector3();
            EndPosition = reader.ReadVector3();
        }
        public void Write(ByteWriter writer)
        {
            writer.WriteHeightmapBounds(Bounds);
            writer.Write(StartPosition);
            writer.Write(EndPosition);
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
    public sealed class HeightmapAdjustAction : IBrushRadius, IBrushFalloff, IBrushStrength, IBrushSensitivity, IDeltaTime, IBrushPosition, IBounds
    {
        internal static readonly Pool<HeightmapAdjustAction> Pool = new Pool<HeightmapAdjustAction>();

        public TerrainTransactionType Type => TerrainTransactionType.HeightmapAdjust;
        public TerrainEditorType EditorType => TerrainEditorType.Heightmap;
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
                return currentHeight;
            distance = Mathf.Sqrt(distance / BrushRadius);
            float num = DeltaTime * BrushStrength * GetBrushAlpha(distance) * BrushSensitivity;
            if (InputEx.GetKey(KeyCode.LeftShift))
                num = -num;
            currentHeight += num;
            return currentHeight;
        }
    }
    public sealed class HeightmapFlattenAction : IBrushRadius, IBrushFalloff, IBrushStrength, IBrushSensitivity, IBrushTarget, IBrushPosition, IDeltaTime, IBounds
    {
        internal static readonly Pool<HeightmapFlattenAction> Pool = new Pool<HeightmapFlattenAction>();

        public TerrainTransactionType Type => TerrainTransactionType.HeightmapFlatten;
        public TerrainEditorType EditorType => TerrainEditorType.Heightmap;
        public EDevkitLandscapeToolHeightmapFlattenMethod FlattenMethod { get; set; }
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
            BrushPosition = reader.ReadVector2();
            DeltaTime = reader.ReadFloat();
            FlattenMethod = (EDevkitLandscapeToolHeightmapFlattenMethod)reader.ReadUInt8();
        }
        public void Write(ByteWriter writer)
        {
            writer.WriteHeightmapBounds(Bounds);
            writer.Write(BrushPosition);
            writer.Write(DeltaTime);
            writer.Write((byte)FlattenMethod);
        }
        private float IntlHandleHeightmapWriteFlatten(LandscapeCoord tileCoord, HeightmapCoord heightmapCoord, Vector3 worldPosition, float currentHeight)
        {
            float distance = new Vector2(worldPosition.x - BrushPosition.x, worldPosition.z - BrushPosition.y).sqrMagnitude;
            if (distance > BrushRadius * BrushRadius)
                return currentHeight;
            distance = Mathf.Sqrt(distance / BrushRadius);
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
    public sealed class HeightmapSmoothAction : IBrushRadius, IBrushFalloff, IBrushStrength, IBrushPosition, IDeltaTime, IBounds
    {
        internal static readonly Pool<HeightmapSmoothAction> Pool = new Pool<HeightmapSmoothAction>();

        public TerrainTransactionType Type => TerrainTransactionType.HeightmapFlatten;
        public TerrainEditorType EditorType => TerrainEditorType.Heightmap;
        public EDevkitLandscapeToolHeightmapSmoothMethod SmoothMethod { get; set; }
        public Bounds Bounds { get; set; }
        public Vector2 BrushPosition { get; set; }
        public float BrushRadius { get; set; }
        public float BrushFalloff { get; set; }
        public float BrushStrength { get; set; }
        public float DeltaTime { get; set; }
        public float SmoothTarget { get; set; }

        public void Apply()
        {
            WriteHeightmapNoTransactions(Bounds, IntlHandleHeightmapWriteSmooth);
        }
        public void Read(ByteReader reader)
        {
            Bounds = reader.ReadInflatedBounds(out bool pixel);
            SmoothMethod = pixel ? EDevkitLandscapeToolHeightmapSmoothMethod.PIXEL_AVERAGE : EDevkitLandscapeToolHeightmapSmoothMethod.BRUSH_AVERAGE;
            BrushPosition = reader.ReadVector2();
            DeltaTime = reader.ReadFloat();
            SmoothTarget = reader.ReadFloat();
        }
        public void Write(ByteWriter writer)
        {
            writer.WriteHeightmapBounds(Bounds, SmoothMethod == EDevkitLandscapeToolHeightmapSmoothMethod.PIXEL_AVERAGE);
            writer.Write(BrushPosition);
            writer.Write(DeltaTime);
            writer.Write(SmoothTarget);
        }
        private float IntlHandleHeightmapWriteSmooth(LandscapeCoord tileCoord, HeightmapCoord heightmapCoord, Vector3 worldPosition, float currentHeight)
        {
            float distance = new Vector2(worldPosition.x - BrushPosition.x, worldPosition.z - BrushPosition.y).sqrMagnitude;
            if (distance > BrushRadius * BrushRadius)
                return currentHeight;
            distance = Mathf.Sqrt(distance / BrushRadius);
            float brushAlpha = GetBrushAlpha(distance);
            currentHeight = Mathf.Lerp(currentHeight, SmoothTarget, DeltaTime * BrushStrength * brushAlpha);
            return currentHeight;
        }
    }
    private sealed class BrushSettingsCollection
    {
        public BrushValueFlags Flags { get; set; }
        public byte StartIndex { get; set; }
        public float Radius { get; set; }
        public float Falloff { get; set; }
        public float Strength { get; set; }
        public float Sensitivity { get; set; }
        public float Target { get; set; }
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
        public BrushSettingsCollection WithSplatmapInfo(SplatmapSettingsCollection? collection)
        {
            if (collection == null)
                Flags &= ~BrushValueFlags.SplatmapPaintInfo;
            else
                Flags |= BrushValueFlags.SplatmapPaintInfo;

            Splatmap = collection;
            Flags |= BrushValueFlags.Target;
            return this;
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
            if ((Flags & BrushValueFlags.SplatmapPaintInfo) != 0)
            {
                Splatmap ??= SplatmapCollectionPool.claim().Reset();
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
            if ((Flags & BrushValueFlags.SplatmapPaintInfo) != 0 && Splatmap != null)
                bld.Append(" Splatmap: ").Append(Splatmap);

            return bld.ToString();
        }
    }
    private sealed class SplatmapSettingsCollection
    {
        public SplatmapValueFlags Flags { get; set; }
        public bool AutoFoundation { get; set; }
        public bool AutoSlope { get; set; }
        public float AutoFoundationRayLength { get; set; }
        public float AutoFoundationRayRadius { get; set; }
        public ERayMask AutoFoundationRayMask { get; set; }
        public float AutoSlopeMinAngleStart { get; set; }
        public float AutoSlopeMinAngleEnd { get; set; }
        public float AutoSlopeMaxAngleStart { get; set; }
        public float AutoSlopeMaxAngleEnd { get; set; }
        public SplatmapSettingsCollection Reset()
        {
            Flags = default;
            return this;
        }
        public SplatmapSettingsCollection WithAutoSlope()
        {
            Flags |= SplatmapValueFlags.UseAutoSlope;
            return this;
        }
        public SplatmapSettingsCollection WithAutoFoundation()
        {
            Flags |= SplatmapValueFlags.UseAutoFoundation;
            return this;
        }
        public SplatmapSettingsCollection WithAutoFoundationSettings(float rayLength, float rayRadius, ERayMask mask)
        {
            Flags |= SplatmapValueFlags.AutoFoundationInfo;
            AutoFoundationRayLength = rayLength;
            AutoFoundationRayRadius = rayRadius;
            AutoFoundationRayMask = mask;
            return this;
        }
        public SplatmapSettingsCollection WithAutoSlopeSettings(float minAngleStart, float minAngleEnd, float maxAngleStart, float maxAngleEnd)
        {
            Flags |= SplatmapValueFlags.AutoSlopeInfo;
            AutoSlopeMinAngleStart = minAngleStart;
            AutoSlopeMinAngleEnd = maxAngleEnd;
            AutoSlopeMaxAngleStart = maxAngleStart;
            AutoSlopeMaxAngleEnd = maxAngleEnd;
            return this;
        }
        public static SplatmapSettingsCollection ReadToPool(ByteReader reader)
        {
            SplatmapSettingsCollection collection = SplatmapCollectionPool.claim();
            collection.Read(reader);
            return collection;
        }
        public void Read(ByteReader reader)
        {
            Flags = reader.ReadEnum<SplatmapValueFlags>();
            AutoFoundation = (Flags & SplatmapValueFlags.UseAutoFoundation) != 0;
            AutoSlope = (Flags & SplatmapValueFlags.UseAutoSlope) != 0;
            if ((Flags & SplatmapValueFlags.AutoFoundationInfo) != 0)
            {
                AutoFoundationRayLength = reader.ReadFloat();
                AutoFoundationRayRadius = reader.ReadFloat();
                AutoFoundationRayMask = reader.ReadEnum<ERayMask>();
            }
            else
            {
                AutoFoundationRayLength = 0;
                AutoFoundationRayRadius = 0;
                AutoFoundationRayMask = 0;
            }
            if ((Flags & SplatmapValueFlags.AutoSlopeInfo) != 0)
            {
                AutoSlopeMinAngleStart = reader.ReadFloat();
                AutoSlopeMaxAngleStart = reader.ReadFloat();
                AutoSlopeMinAngleEnd = reader.ReadFloat();
                AutoSlopeMaxAngleEnd = reader.ReadFloat();
            }
            else
            {
                AutoSlopeMinAngleStart = 0;
                AutoSlopeMaxAngleStart = 0;
                AutoSlopeMinAngleEnd = 0;
                AutoSlopeMaxAngleEnd = 0;
            }
        }

        public void Write(ByteWriter writer)
        {
            if (AutoFoundation)
                Flags |= SplatmapValueFlags.UseAutoFoundation;
            if (AutoSlope)
                Flags |= SplatmapValueFlags.UseAutoSlope;
            writer.Write(Flags);
            if ((Flags & SplatmapValueFlags.AutoFoundationInfo) != 0)
            {
                writer.Write(AutoFoundationRayLength);
                writer.Write(AutoFoundationRayRadius);
                writer.Write(AutoFoundationRayMask);
            }
            if ((Flags & SplatmapValueFlags.AutoSlopeInfo) != 0)
            {
                writer.Write(AutoSlopeMinAngleStart);
                writer.Write(AutoSlopeMaxAngleStart);
                writer.Write(AutoSlopeMinAngleEnd);
                writer.Write(AutoSlopeMaxAngleEnd);
            }
        }
    }
}
