using DevkitServer.Multiplayer.Networking;
using DevkitServer.Players;
using DevkitServer.Util.Encoding;
using JetBrains.Annotations;
using SDG.Framework.Landscapes;
using SDG.Framework.Utilities;
using SDG.NetPak;
using System.Reflection;
using HarmonyLib;
#if CLIENT
using DevkitServer.Patches;
using SDG.Framework.Devkit.Tools;
#endif

namespace DevkitServer.Multiplayer;
public sealed partial class EditorTerrain : MonoBehaviour
{
    private static readonly ByteReader Reader = new ByteReader { ThrowOnError = true };
    private static readonly ByteWriter Writer = new ByteWriter(false, 8192);
    public const ushort DataVersion = 0;

    /// <summary>Ran before an action is applied.</summary>
    public static event ApplyingAction? OnApplyingAction;

    /// <summary>Ran after an action is applied.</summary>
    public static event AppliedAction? OnAppliedAction;

    internal static ushort ReadDataVersion = DataVersion;
    internal static bool SaveTransactions = true;
    internal static ITerrainAction? ActiveAction;
    internal static FieldInfo SaveTransactionsField = typeof(EditorTerrain).GetField(nameof(SaveTransactions), BindingFlags.Static | BindingFlags.NonPublic)!;
#if CLIENT
    private float _lastFlush;
#endif
    private float _nextApply;
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
        action.Instigator = Provider.client;
        _editBuffer.Add(action);
    }
#endif

    internal static void ReceiveTerrainRelay(
#if SERVER
        ITransportConnection transportConnection,
#endif
        NetPakReader reader)
    {
        if (!reader.ReadUInt16(out ushort len))
        {
            Logger.LogError("Failed to read incoming terrain packet length.");
            return;
        }

#if SERVER
        EditorUser? user = UserManager.FromConnection(transportConnection);
        if (user == null)
        {
            Logger.LogError("Failed to find user for terrain packet from transport connection: " + transportConnection.Format() + ".");
            return;
        }
#endif
        if (!reader.ReadBytesPtr(len, out byte[] buffer, out int offset))
        {
            Logger.LogError("Failed to read terrain packet.");
            return;
        }
        Reader.LoadNew(buffer);
        Reader.Skip(offset);
#if CLIENT
        ulong s64 = Reader.ReadUInt64();
        EditorUser? user = UserManager.FromId(s64);
        if (user == null)
        {
            Logger.LogError("Failed to find user for terrain packet from a steam id: " + s64.Format() + ".");
            return;
        }
#endif
        user.Terrain.HandleReadPackets(Reader);

#if SERVER
        if (Provider.clients.Count > 1)
        {
            byte[] sendBytes = new byte[sizeof(ulong) + len];
            Buffer.BlockCopy(buffer, offset, sendBytes, sizeof(ulong), len);
            UnsafeBitConverter.GetBytes(sendBytes, user.SteamId.m_SteamID);
            IList<ITransportConnection> list = NetFactory.GetPooledTransportConnectionList(Provider.clients.Count - 1);
            for (int i = 0; i < Provider.clients.Count; ++i)
            {
                SteamPlayer pl = Provider.clients[i];
                if (pl.playerID.steamID.m_SteamID != user.SteamId.m_SteamID)
                    list.Add(pl.transportConnection);
            }

            NetFactory.SendGeneric(NetFactory.DevkitMessage.TerrainEditRelay, sendBytes, list, reliable: false);
        }
#endif
    }
#if CLIENT
    internal void FlushEdits()
    {
        if (_editBuffer.Count > 0)
        {
            Logger.LogDebug("Flushing " + _editBuffer.Count.Format() + " action(s).");
            WriteEditBuffer(Writer);
            int len = Writer.Count;
            NetFactory.SendGeneric(NetFactory.DevkitMessage.TerrainEditRelay, Writer.FinishWrite(), 0, len, true);
        }
    }
#endif
    [UsedImplicitly]
    private void Update()
    {
        float t = Time.realtimeSinceStartup;
#if CLIENT
        if (IsOwner)
        {
            if (t - _lastFlush >= 1f)
            {
                _lastFlush = t;
                FlushEdits();
            }

            return;
        }
#endif
        while (_editBuffer.Count > 0 && t >= _nextApply)
        {
            ITerrainAction action = _editBuffer[_editBuffer.Count - 1];
            if (OnApplyingAction != null)
            {
                bool allow = true;
                OnApplyingAction.Invoke(action, ref allow);
                if (!allow) continue;
            }
            _nextApply += action.DeltaTime;
            ActiveAction = action;
            action.Apply();
            ActiveAction = null;
            if (action is IDisposable disposable)
                disposable.Dispose();
            _editBuffer.RemoveAt(_editBuffer.Count - 1);
            OnAppliedAction?.Invoke(action);
        }
    }
#if CLIENT
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
            if (action is IBrushRadius r && (GetBrushSettings(BrushValueFlags.Radius) is not { } v1 || !MathfEx.IsNearlyEqual(v1.Radius, r.BrushRadius, tol)))
                (toAdd ??= BrushCollectionPool.claim().Reset()).WithRadius(r.BrushRadius);
            if (action is IBrushFalloff f && (GetBrushSettings(BrushValueFlags.Falloff) is not { } v2 || !MathfEx.IsNearlyEqual(v2.Falloff, f.BrushFalloff, tol)))
                (toAdd ??= BrushCollectionPool.claim().Reset()).WithFalloff(f.BrushFalloff);
            if (action is IBrushStrength s1 && (GetBrushSettings(BrushValueFlags.Strength) is not { } v3 || !MathfEx.IsNearlyEqual(v3.Strength, s1.BrushStrength, tol)))
                (toAdd ??= BrushCollectionPool.claim().Reset()).WithStrength(s1.BrushStrength);
            if (action is IBrushSensitivity s2 && (GetBrushSettings(BrushValueFlags.Sensitivity) is not { } v4 || !MathfEx.IsNearlyEqual(v4.Sensitivity, s2.BrushSensitivity, tol)))
                (toAdd ??= BrushCollectionPool.claim().Reset()).WithSensitivity(s2.BrushSensitivity);
            if (action is IBrushTarget t && (GetBrushSettings(BrushValueFlags.Target) is not { } v5 || !MathfEx.IsNearlyEqual(v5.Target, t.BrushTarget, tol)))
                (toAdd ??= BrushCollectionPool.claim().Reset()).WithTarget(t.BrushTarget);
            if (action is IAutoSlope slope)
            {
                if (GetSplatmapSettings(SplatmapValueFlags.AutoSlopeMinAngleBegin) is not { } v6 || !MathfEx.IsNearlyEqual(v6.AutoSlopeMinAngleBegin, slope.AutoSlopeMinAngleBegin, tol))
                    (toAdd ??= BrushCollectionPool.claim().Reset()).WithSplatmapInfo((toAdd.Splatmap ?? SplatmapCollectionPool.claim().Reset()).WithAutoSlopeMinAngleBegin(slope.AutoSlopeMinAngleBegin));
                if (GetSplatmapSettings(SplatmapValueFlags.AutoSlopeMinAngleEnd) is not { } v7 || !MathfEx.IsNearlyEqual(v7.AutoSlopeMinAngleEnd, slope.AutoSlopeMinAngleEnd, tol))
                    (toAdd ??= BrushCollectionPool.claim().Reset()).WithSplatmapInfo((toAdd.Splatmap ?? SplatmapCollectionPool.claim().Reset()).WithAutoSlopeMinAngleEnd(slope.AutoSlopeMinAngleEnd));
                if (GetSplatmapSettings(SplatmapValueFlags.AutoSlopeMaxAngleBegin) is not { } v8 || !MathfEx.IsNearlyEqual(v8.AutoSlopeMaxAngleBegin, slope.AutoSlopeMaxAngleBegin, tol))
                    (toAdd ??= BrushCollectionPool.claim().Reset()).WithSplatmapInfo((toAdd.Splatmap ?? SplatmapCollectionPool.claim().Reset()).WithAutoSlopeMaxAngleBegin(slope.AutoSlopeMaxAngleBegin));
                if (GetSplatmapSettings(SplatmapValueFlags.AutoSlopeMaxAngleEnd) is not { } v9 || !MathfEx.IsNearlyEqual(v9.AutoSlopeMaxAngleEnd, slope.AutoSlopeMaxAngleEnd, tol))
                    (toAdd ??= BrushCollectionPool.claim().Reset()).WithSplatmapInfo((toAdd.Splatmap ?? SplatmapCollectionPool.claim().Reset()).WithAutoSlopeMaxAngleEnd(slope.AutoSlopeMaxAngleEnd));
            }
            if (action is IAutoFoundation found)
            {
                if (GetSplatmapSettings(SplatmapValueFlags.AutoFoundationRayLength) is not { } v6 || !MathfEx.IsNearlyEqual(v6.AutoFoundationRayLength, found.AutoFoundationRayLength, tol))
                    (toAdd ??= BrushCollectionPool.claim().Reset()).WithSplatmapInfo((toAdd.Splatmap ?? SplatmapCollectionPool.claim().Reset()).WithAutoFoundationRayLength(found.AutoFoundationRayLength));
                if (GetSplatmapSettings(SplatmapValueFlags.AutoFoundationRayRadius) is not { } v7 || !MathfEx.IsNearlyEqual(v7.AutoFoundationRayRadius, found.AutoFoundationRayRadius, tol))
                    (toAdd ??= BrushCollectionPool.claim().Reset()).WithSplatmapInfo((toAdd.Splatmap ?? SplatmapCollectionPool.claim().Reset()).WithAutoFoundationRayRadius(found.AutoFoundationRayRadius));
                if (GetSplatmapSettings(SplatmapValueFlags.AutoFoundationRayMask) is not { } v8 || v8.AutoFoundationRayMask != found.AutoFoundationRayMask)
                    (toAdd ??= BrushCollectionPool.claim().Reset()).WithSplatmapInfo((toAdd.Splatmap ?? SplatmapCollectionPool.claim().Reset()).WithAutoFoundationRayMask(found.AutoFoundationRayMask));
            }
            if (action is ISplatmapMaterial material && (GetSplatmapSettings(SplatmapValueFlags.SplatmapMaterial) is not { } v10 || v10.SplatmapMaterial.GUID != material.SplatmapMaterial.GUID))
                (toAdd ??= BrushCollectionPool.claim().Reset()).WithSplatmapInfo((toAdd.Splatmap ?? SplatmapCollectionPool.claim().Reset()).WithSplatmapMaterial(material.SplatmapMaterial));
            
            if (toAdd != null)
            {
                SetBrushSettings(toAdd);
                toAdd.StartIndex = (byte)i;
                c.Add(toAdd);
                Logger.LogDebug("Queued data: " + toAdd.Flags + " at index " + toAdd.StartIndex + ": " + toAdd + ".");
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
            if (action is IDisposable disposable)
                disposable.Dispose();
        }
        
        _editBuffer.Clear();
    }
#endif

    private void HandleReadPackets(ByteReader reader)
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
        if (stInd == 0)
            _nextApply = Time.realtimeSinceStartup;
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
                TerrainTransactionType.SplatmapPaint => new SplatmapPaintAction(),
                TerrainTransactionType.SplatmapAutoPaint => new SplatmapPaintAction { IsAuto = true },
                TerrainTransactionType.SplatmapSmooth => new SplatmapSmoothAction(),
                TerrainTransactionType.HolesCut => new HolemapPaintAction(),
                TerrainTransactionType.AddTile => new TileModifyAction(),
                TerrainTransactionType.DeleteTile => new TileModifyAction { IsDelete = true },
                TerrainTransactionType.UpdateSplatmapLayers => new TileSplatmapLayersUpdateAction(),
                _ => null
            };
            if (action != null)
            {
                action.Instigator = User.SteamId;
                action.Read(reader);
                if (action is IBrushRadius r && GetBrushSettings(BrushValueFlags.Radius) is { } st1)
                    r.BrushRadius = st1.Radius;
                if (action is IBrushFalloff f && GetBrushSettings(BrushValueFlags.Falloff) is { } st2)
                    f.BrushFalloff = st2.Falloff;
                if (action is IBrushStrength s1 && GetBrushSettings(BrushValueFlags.Strength) is { } st3)
                    s1.BrushStrength = st3.Strength;
                if (action is IBrushSensitivity s2 && GetBrushSettings(BrushValueFlags.Sensitivity) is { } st4)
                    s2.BrushSensitivity = st4.Sensitivity;
                if (action is IBrushTarget t && GetBrushSettings(BrushValueFlags.Target) is { } st5)
                    t.BrushTarget = st5.Target;
                if (action is IAutoSlope slope)
                {
                    if (GetSplatmapSettings(SplatmapValueFlags.AutoSlopeMinAngleBegin) is { } st6)
                        slope.AutoSlopeMinAngleBegin = st6.AutoSlopeMinAngleBegin;
                    if (GetSplatmapSettings(SplatmapValueFlags.AutoSlopeMinAngleEnd) is { } st7)
                        slope.AutoSlopeMinAngleEnd = st7.AutoSlopeMinAngleEnd;
                    if (GetSplatmapSettings(SplatmapValueFlags.AutoSlopeMaxAngleBegin) is { } st8)
                        slope.AutoSlopeMaxAngleBegin = st8.AutoSlopeMaxAngleBegin;
                    if (GetSplatmapSettings(SplatmapValueFlags.AutoSlopeMaxAngleEnd) is { } st9)
                        slope.AutoSlopeMaxAngleEnd = st9.AutoSlopeMaxAngleEnd;
                }
                if (action is IAutoFoundation found)
                {
                    if (GetSplatmapSettings(SplatmapValueFlags.AutoFoundationRayLength) is { } st6)
                        found.AutoFoundationRayLength = st6.AutoFoundationRayLength;
                    if (GetSplatmapSettings(SplatmapValueFlags.AutoFoundationRayRadius) is { } st7)
                        found.AutoFoundationRayRadius = st7.AutoFoundationRayRadius;
                    if (GetSplatmapSettings(SplatmapValueFlags.AutoFoundationRayMask) is { } st8)
                        found.AutoFoundationRayMask = st8.AutoFoundationRayMask;
                }
                if (action is ISplatmapMaterial material && GetSplatmapSettings(SplatmapValueFlags.SplatmapMaterial) is { } st10)
                    material.SplatmapMaterial = st10.SplatmapMaterial;
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

        ListPool<BrushSettingsCollection>.release(c);

        void LoadCollection(int index)
        {
            if (c.Count <= index)
                return;
            collIndex = index;
            BrushSettingsCollection collection = c[collIndex];
            SetBrushSettings(collection);
        }
    }
    private static float GetBrushAlpha(float sqrDistance)
    {
        if (ActiveAction is IBrushFalloff f)
        {
            return sqrDistance < f.BrushFalloff ? 1f : (1f - sqrDistance) / (1f - f.BrushFalloff);
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
    private static void WriteSplatmapNoTransactions(Bounds worldBounds, Landscape.LandscapeWriteSplatmapHandler callback)
    {
        ThreadUtil.assertIsGameThread();

        SaveTransactions = false;
        try
        {
            Landscape.writeSplatmap(worldBounds, callback);
        }
        catch (Exception ex)
        {
            Logger.LogError("Error writing to splatmap.");
            Logger.LogError(ex);
        }
        finally
        {
            SaveTransactions = true;
        }
    }
    private static void WriteHolesNoTransactions(Bounds worldBounds, Landscape.LandscapeWriteHolesHandler callback)
    {
        ThreadUtil.assertIsGameThread();

        SaveTransactions = false;
        try
        {
            Landscape.writeHoles(worldBounds, callback);
        }
        catch (Exception ex)
        {
            Logger.LogError("Error writing to holes.");
            Logger.LogError(ex);
        }
        finally
        {
            SaveTransactions = true;
        }
    }
    public static void Patch()
    {
        SplatmapPaintAction.Patch();
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
            ClientEvents.OnPainted += OnPaint;
            ClientEvents.OnAutoPainted += OnAutoPaint;
            ClientEvents.OnPaintSmoothed += OnPaintSmooth;
            ClientEvents.OnHolePainted += OnPaintHole;
            ClientEvents.OnTileAdded += OnTileAdded;
            ClientEvents.OnTileDeleted += OnTileDeleted;
            ClientEvents.OnSplatmapLayerMaterialsUpdate += OnSplatmapLayerMaterialsUpdate;
            //ClientEvents.OnFoliageAdded += OnFoliageAdded;
            //ClientEvents.OnFoliageRemoved += OnFoliageRemoved;
            //ClientEvents.OnResourceSpawnpointRemoved += OnResourceSpawnpointRemoved;
            //ClientEvents.OnLevelObjectRemoved += OnLevelObjectRemoved;
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
            ClientEvents.OnPainted -= OnPaint;
            ClientEvents.OnAutoPainted -= OnAutoPaint;
            ClientEvents.OnPaintSmoothed -= OnPaintSmooth;
            ClientEvents.OnHolePainted -= OnPaintHole;
            ClientEvents.OnTileAdded -= OnTileAdded;
            ClientEvents.OnTileDeleted -= OnTileDeleted;
            ClientEvents.OnSplatmapLayerMaterialsUpdate -= OnSplatmapLayerMaterialsUpdate;
            //ClientEvents.OnFoliageAdded -= OnFoliageAdded;
            //ClientEvents.OnFoliageRemoved -= OnFoliageRemoved;
            //ClientEvents.OnResourceSpawnpointRemoved -= OnResourceSpawnpointRemoved;
            //ClientEvents.OnLevelObjectRemoved -= OnLevelObjectRemoved;
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
            BrushFalloff = falloff,
            DeltaTime = Time.deltaTime
        });
    }
    private void OnHeightmapAdjust(Bounds bounds, Vector3 position, float radius, float falloff, float strength, float sensitivity, bool subtracting, float dt)
    {
        QueueAction(new HeightmapAdjustAction
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
        QueueAction(new HeightmapFlattenAction
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
        QueueAction(new HeightmapSmoothAction
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
        QueueAction(new SplatmapPaintAction
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
        QueueAction(new SplatmapPaintAction
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
        QueueAction(new SplatmapSmoothAction
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
        QueueAction(new HolemapPaintAction
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
        QueueAction(new TileModifyAction
        {
            Coordinates = tile.coord,
            DeltaTime = Time.deltaTime
        });
    }
    private void OnTileDeleted(LandscapeTile tile)
    {
        QueueAction(new TileModifyAction
        {
            IsDelete = true,
            Coordinates = tile.coord,
            DeltaTime = Time.deltaTime
        });
    }
    private void OnSplatmapLayerMaterialsUpdate(LandscapeTile tile)
    {
        QueueAction(new TileModifyAction
        {
            IsDelete = true,
            Coordinates = tile.coord,
            DeltaTime = Time.deltaTime
        });
    }
    //private void OnFoliageAdded(LandscapeTile tile)
    //{
    //    QueueAction(new FoliageAddAction
    //    {
    //        IsDelete = false,
    //        Coordinates = tile.coord,
    //        DeltaTime = Time.deltaTime
    //    });
    //}
#endif

    public delegate void AppliedAction(ITerrainAction action);
    public delegate void ApplyingAction(ITerrainAction action, ref bool execute);
}
