using DevkitServer.Multiplayer.Networking;
using DevkitServer.Patches;
using DevkitServer.Players;
using DevkitServer.Util.Encoding;
using JetBrains.Annotations;
using SDG.Framework.Devkit.Tools;
using SDG.Framework.Landscapes;
using SDG.Framework.Utilities;
using System.Reflection;

namespace DevkitServer.Multiplayer;
public sealed partial class EditorTerrain : MonoBehaviour
{
    public static readonly NetCallCustom FlushEditBuffer = new NetCallCustom((ushort)NetCalls.FlushEditBuffer, short.MaxValue);
    public const ushort DataVersion = 0;
    internal static ushort ReadDataVersion = DataVersion;
    internal static bool SaveTransactions = true;
    internal static ITerrainAction? ActiveAction;
    internal static FieldInfo SaveTransactionsField = typeof(EditorTerrain).GetField("SaveTransactions", BindingFlags.Static | BindingFlags.NonPublic)!;
    private float _lastFlush;

    // edit buffer is reversed for everyone but the owner.
    private readonly List<ITerrainAction> _editBuffer = new List<ITerrainAction>();
    public EditorUser User { get; internal set; } = null!;
    public bool IsOwner { get; private set; }

    [UsedImplicitly]
    private void Start()
    {
        if (User == null)
        {
            Destroy(this);
            Logger.LogError("Invalid EditorTerrain setup; EditorUser not found!");
            return;
        }
        
#if CLIENT
        IsOwner = User == EditorUser.User;
#endif
        Init();
        Logger.LogDebug("Editor terrain module created for " + User.SteamId.m_SteamID + " ( owner: " + IsOwner + " ).");
    }

    [UsedImplicitly]
    private void OnDestroy()
    {
        Deinit();
        User = null!;
        IsOwner = false;
    }
#if CLIENT
    private void QueueAction(ITerrainAction action)
    {
        _editBuffer.Add(action);

        Logger.LogDebug("Queued action: " + action.GetType().Name);
    }
#endif

    [NetCall(NetCallSource.FromEither, (ushort)NetCalls.FlushEditBuffer)]
    private static void ReceiveEditBuffer(MessageContext ctx, ByteReader reader)
    {
#if CLIENT
        EditorUser? user = UserManager.FromId(ctx.Overhead.Sender);
#else
        EditorUser? user = UserManager.FromConnection(ctx.Connection);
#endif
        if (user != null && user.IsOnline)
        {
            user.Terrain.ReadEditBuffer(reader);
        }
    }
#if CLIENT
    internal void FlushEdits()
    {
        if (_editBuffer.Count > 0)
        {
            MessageOverhead overhead = new MessageOverhead(MessageFlags.LayeredRequest, (int)NetCalls.FlushEditBuffer, 0);
            Logger.LogDebug("Flushing " + _editBuffer.Count + " action(s).");
            FlushEditBuffer.Invoke(ref overhead, WriteEditBuffer);
        }
    }
#endif
    [UsedImplicitly]
    private void Update()
    {
#if CLIENT
        if (IsOwner)
        {
            float t = Time.realtimeSinceStartup;
            if (t - _lastFlush >= 1f)
            {
                _lastFlush = t;
                FlushEdits();
            }
        }
        else
#endif
        if (_editBuffer.Count > 0)
        {
            ITerrainAction action = _editBuffer[_editBuffer.Count - 1];
            ActiveAction = action;
            action.Apply();
            ActiveAction = null;
            _editBuffer.RemoveAt(_editBuffer.Count - 1);
        }
    }

    private void WriteEditBuffer(ByteWriter writer)
    {
        ThreadUtil.assertIsGameThread();

        writer.Write(DataVersion);
        byte ct = (byte)Math.Min(_editBuffer.Count, byte.MaxValue);
        writer.Write(ct);
        List<BrushSettingsCollection> c = ListPool<BrushSettingsCollection>.claim();
        for (int i = 0; i < ct; ++i)
        {
            ITerrainAction action = _editBuffer[i];
            BrushSettingsCollection? toAdd = null;
            const float tol = 0.0001f;
            switch (action.EditorType)
            {
                case TerrainEditorType.Heightmap:
                    if (action is IBrushRadius r && (GetBrushSettings(BrushValueFlags.Radius) == null || !MathfEx.IsNearlyEqual(GetBrushSettings(BrushValueFlags.Radius)!.Radius, r.BrushRadius, tol)))
                        (toAdd ??= BrushCollectionPool.claim().Reset()).WithRadius(r.BrushRadius);
                    if (action is IBrushFalloff f && (GetBrushSettings(BrushValueFlags.Falloff) == null || !MathfEx.IsNearlyEqual(GetBrushSettings(BrushValueFlags.Falloff)!.Falloff, f.BrushFalloff, tol)))
                        (toAdd ??= BrushCollectionPool.claim().Reset()).WithFalloff(f.BrushFalloff);
                    if (action is IBrushStrength s1 && (GetBrushSettings(BrushValueFlags.Strength) == null || !MathfEx.IsNearlyEqual(GetBrushSettings(BrushValueFlags.Strength)!.Strength, s1.BrushStrength, tol)))
                        (toAdd ??= BrushCollectionPool.claim().Reset()).WithStrength(s1.BrushStrength);
                    if (action is IBrushSensitivity s2 && (GetBrushSettings(BrushValueFlags.Sensitivity) == null || !MathfEx.IsNearlyEqual(GetBrushSettings(BrushValueFlags.Sensitivity)!.Sensitivity, s2.BrushSensitivity, tol)))
                        (toAdd ??= BrushCollectionPool.claim().Reset()).WithSensitivity(s2.BrushSensitivity);
                    if (action is IBrushTarget t && (GetBrushSettings(BrushValueFlags.Target) == null || !MathfEx.IsNearlyEqual(GetBrushSettings(BrushValueFlags.Target)!.Target, t.BrushTarget, tol)))
                        (toAdd ??= BrushCollectionPool.claim().Reset()).WithTarget(t.BrushTarget);
                    break;
                case TerrainEditorType.Splatmap:
                    // todo
                    break;
            }
            if (toAdd != null)
            {
                SetBrushSettings(toAdd);
                toAdd.StartIndex = (byte)i;
                c.Add(toAdd);
                Logger.LogDebug("Queued " + toAdd.Flags + "@" + toAdd.StartIndex + ": " + toAdd + ".");
            }
        }

        byte ct2 = (byte)c.Count;
        writer.Write(ct2);
        Logger.LogDebug("Writing " + ct2 + " collections.");
        for (int i = 0; i < ct2; ++i)
        {
            BrushSettingsCollection collection = c[i];
            collection.Write(writer);
            if (collection.Splatmap != null)
            {
                collection.Splatmap.Write(writer);
                SplatmapCollectionPool.release(collection.Splatmap);
                collection.Splatmap = null;
            }

            BrushCollectionPool.release(collection);
        }

        ListPool<BrushSettingsCollection>.release(c);

        Logger.LogDebug("Writing " + ct + " actions.");
        for (int i = 0; i < ct; ++i)
        {
            ITerrainAction action = _editBuffer[i];
            writer.Write(action.Type);
            action.Write(writer);
        }
        
        _editBuffer.Clear();
    }
    private void ReadEditBuffer(ByteReader reader)
    {
        ThreadUtil.assertIsGameThread();

        ReadDataVersion = reader.ReadUInt16();
        int ct = reader.ReadUInt8();
        int ct2 = reader.ReadUInt8();
        List<BrushSettingsCollection> c = ListPool<BrushSettingsCollection>.claim();
        for (int i = 0; i < ct2; ++i)
        {
            BrushSettingsCollection collection = new BrushSettingsCollection();
            collection.Read(reader);
            c.Add(collection);
        }
        int collIndex = -1;
        LoadCollection(0);
        int stInd = _editBuffer.Count;
        for (int i = 0; i < ct; ++i)
        {
            if (c.Count > collIndex + 1 && c[collIndex + 1].StartIndex >= i)
                LoadCollection(collIndex + 1);
            TerrainTransactionType type = reader.ReadEnum<TerrainTransactionType>();
            ITerrainAction? action = type switch
            {
                // todo Keep Updated
                TerrainTransactionType.HeightmapRamp => new HeightmapRampAction(),
                TerrainTransactionType.HeightmapFlatten => new HeightmapFlattenAction(),
                TerrainTransactionType.HeightmapAdjust => new HeightmapAdjustAction(),
                TerrainTransactionType.HeightmapSmooth => new HeightmapSmoothAction(),
                _ => null
            };
            if (action != null)
            {
                action.Read(reader);
                if (action is IBrushRadius r && GetBrushSettings(BrushValueFlags.Radius) is { } st1)
                    r.BrushRadius = st1.Radius;
                if (action is IBrushFalloff f && GetBrushSettings(BrushValueFlags.Falloff) is { } st2)
                    f.BrushFalloff = st2.Radius;
                if (action is IBrushStrength s1 && GetBrushSettings(BrushValueFlags.Strength) is { } st3)
                    s1.BrushStrength = st3.Strength;
                if (action is IBrushSensitivity s2 && GetBrushSettings(BrushValueFlags.Sensitivity) is { } st4)
                    s2.BrushSensitivity = st4.Sensitivity;
                if (action is IBrushTarget t && GetBrushSettings(BrushValueFlags.Target) is { } st5)
                    t.BrushTarget = st5.Target;
                _editBuffer.Add(action);
            }
        }
        if (stInd != _editBuffer.Count)
        {
            // reverse, queue at beginning
            ITerrainAction[] tempBuffer = new ITerrainAction[_editBuffer.Count - stInd];
            _editBuffer.CopyTo(stInd, tempBuffer, 0, tempBuffer.Length);
            _editBuffer.RemoveRange(stInd, tempBuffer.Length);
            Array.Reverse(tempBuffer);
            _editBuffer.InsertRange(0, tempBuffer);
            Logger.LogDebug("Received actions: " + tempBuffer.Length + ".");
        }

        void LoadCollection(int index)
        {
            if (c.Count <= index)
                return;
            collIndex = index;
            BrushSettingsCollection collection = c[collIndex];
            SetBrushSettings(collection);
        }
    }
    private static float GetBrushAlpha(float distance)
    {
        if (ActiveAction is IBrushFalloff f)
        {
            return distance < f.BrushFalloff ? 1f : (1f - distance) / (1f - f.BrushFalloff);
        }

        return 1f;
    }
    private static void WriteHeightmapNoTransactions(Bounds worldBounds, Landscape.LandscapeWriteHeightmapHandler callback)
    {
        ThreadUtil.assertIsGameThread();

        SaveTransactions = false;
        try
        {
            Landscape.writeHeightmap(worldBounds, callback);
        }
        catch (Exception ex)
        {
            Logger.LogError("Error writing to heightmap.");
            Logger.LogError(ex);
        }
        finally
        {
            SaveTransactions = true;
        }
    }
    
    public void Init()
    {
#if CLIENT
        if (IsOwner)
        {
            ClientEvents.OnRampComplete += OnHeightmapRampConfirmed;
            ClientEvents.OnAdjusted += OnHeightmapAdjust;
            ClientEvents.OnFlattened += OnHeightmapFlatten;
            ClientEvents.OnSmoothed += OnHeightmapSmooth;
        }
#endif
    }

    public void Deinit()
    {
#if CLIENT

        if (IsOwner)
        {
            ClientEvents.OnRampComplete -= OnHeightmapRampConfirmed;
            ClientEvents.OnAdjusted -= OnHeightmapAdjust;
            ClientEvents.OnFlattened -= OnHeightmapFlatten;
            ClientEvents.OnSmoothed -= OnHeightmapSmooth;
        }
#endif
    }

#if CLIENT
    private void OnHeightmapRampConfirmed(Bounds bounds, Vector3 start, Vector3 end, float radius, float falloff)
    {
        QueueAction(new HeightmapRampAction
        {
            Bounds = bounds,
            StartPosition = start,
            EndPosition = end,
            BrushRadius = radius,
            BrushFalloff = falloff
        });
    }
    private void OnHeightmapAdjust(Bounds bounds, Vector3 position, float radius, float falloff, float strength, float sensitivity, bool subtracting, float dt)
    {
        QueueAction(new HeightmapAdjustAction
        {
            Bounds = bounds,
            BrushPosition = position,
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
        QueueAction(new HeightmapFlattenAction
        {
            Bounds = bounds,
            BrushPosition = position,
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
        QueueAction(new HeightmapSmoothAction
        {
            Bounds = bounds,
            BrushPosition = position,
            BrushRadius = radius,
            BrushFalloff = falloff,
            BrushStrength = strength,
            SmoothTarget = target,
            SmoothMethod = method,
            DeltaTime = dt
        });
    }
#endif
}
