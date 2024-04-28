#if DEBUG
#define PRINT_ACTION_DETAIL
#define PRINT_ACTION_SIMPLE
#endif

using DevkitServer.API;
using DevkitServer.API.Multiplayer;
using DevkitServer.Multiplayer.Networking;
using DevkitServer.Players;
using DevkitServer.Util.Encoding;
using SDG.Framework.Utilities;
using SDG.NetPak;
using System.Reflection;
using DanielWillett.SpeedBytes;

#if SERVER
using DevkitServer.API.UI;
using DevkitServer.Multiplayer.Levels;
using DevkitServer.Multiplayer.Sync;
#endif

#if CLIENT
#endif

#if PRINT_ACTION_DETAIL
using System.Text.Json;
using DevkitServer.Configuration;
#endif

namespace DevkitServer.Multiplayer.Actions;

[EarlyTypeInit]
public sealed class EditorActions : MonoBehaviour, IActionListener
{
    internal const string Source = "EDITOR ACTIONS";

    internal static readonly FieldInfo LocalLastActionField = typeof(EditorActions).GetField(nameof(LocalLastAction), BindingFlags.NonPublic | BindingFlags.Static)!;
    internal static float LocalLastAction;
    public const ushort DataVersion = 1;
    public const ushort ActionBaseSize = 1;
    public const int MaxPacketSize = 16384;
    private const float ActionFlushInterval = 0.125f;

    private static readonly ByteReader Reader = new ByteReader { ThrowOnError = true };
    private static readonly ByteWriter Writer = new ByteWriter(8192);

    /// <remarks>Reversed for everyone but the owner. </remarks>
    private readonly List<IAction> _pendingActions = new List<IAction>();

    /// <summary>Ran before an action is applied, with the ability to cancel processing.</summary>
    public static event ApplyingAction? OnApplyingAction;

    /// <summary>Ran after an action is applied.</summary>
    public static event AppliedAction? OnAppliedAction;


    internal static ushort ReadDataVersion;
#if SERVER
    private float _lastNoPermissionMessage;
    public void DontSendPermissionMesssage() => _lastNoPermissionMessage = CachedTime.RealtimeSinceStartup;
#endif
    public static EditorActions? ServerActions { get; internal set; }
    private float _lastFlush;
    private bool _queuedThisFrame;
#if CLIENT
    internal static TemporaryEditorActions? TemporaryEditorActions;
    internal static bool CanProcess;
    internal static bool HasProcessedPendingHierarchyObjects;
    internal static bool HasProcessedPendingLevelObjects;
    internal static bool HasProcessedPendingRoads;
    internal static bool HasProcessedPendingFlags;
    internal static bool HasProcessedPendingSpawnTables;
    internal static bool HasProcessedPendingSpawnTiers;
    internal static bool HasProcessedPendingSpawnAssets;
    internal static bool HasProcessedPendingSpawns;
    /// <summary>
    /// Is the player catching up after downloading the map.
    /// </summary>
    public static bool IsPlayingCatchUp { get; private set; }

    internal static Coroutine? CatchUpCoroutine;
    private bool _isRunningCatchUpCoroutine;
    public static bool HasLargeQueue(EditorUser? user = null)
    {
        user ??= EditorUser.User;
        if (user == null || user.Actions == null)
            return false;
        EditorActions actions = user.Actions;
        // if the number of actions that can fit in a packet is less than 50% of the total queued actions
        return actions.CountNumActionsInPacket(out _) / (float)actions._pendingActions.Count < 0.5;
    }
#endif

    private float _nextApply;
    public EditorUser? User { get; internal set; }
    public bool IsOwner { get; internal set; }
    public static IAction? ActiveAction { get; private set; }
    public ActionSettings Settings { get; }
    internal TerrainActions TerrainActions { get; }
    internal FoliageActions FoliageActions { get; }
    internal HierarchyActions HierarchyActions { get; }
    internal ObjectActions ObjectActions { get; }
    internal SpawnActions SpawnActions { get; }
    internal SpawnTableActions SpawnTableActions { get; }
    internal RoadActions RoadActions { get; }
    internal NavigationActions NavigationActions { get; }
    internal LightingActions LightingActions { get; }
    public int QueueSize => _pendingActions.Count;

    private EditorActions()
    {
        Settings = new ActionSettings(this);
        TerrainActions = new TerrainActions(this);
        FoliageActions = new FoliageActions(this);
        HierarchyActions = new HierarchyActions(this);
        ObjectActions = new ObjectActions(this);
        SpawnActions = new SpawnActions(this);
        SpawnTableActions = new SpawnTableActions(this);
        RoadActions = new RoadActions(this);
        NavigationActions = new NavigationActions(this);
        LightingActions = new LightingActions(this);
    }
    public void Subscribe()
    {
        TerrainActions.Subscribe();
        FoliageActions.Subscribe();
        HierarchyActions.Subscribe();
        ObjectActions.Subscribe();
        SpawnActions.Subscribe();
        SpawnTableActions.Subscribe();
        RoadActions.Subscribe();
        NavigationActions.Subscribe();
        LightingActions.Subscribe();
    }

    public void Unsubscribe()
    {
        TerrainActions.Unsubscribe();
        FoliageActions.Unsubscribe();
        HierarchyActions.Unsubscribe();
        ObjectActions.Unsubscribe();
        SpawnActions.Unsubscribe();
        SpawnTableActions.Unsubscribe();
        RoadActions.Unsubscribe();
        NavigationActions.Unsubscribe();
        LightingActions.Unsubscribe();
    }

    [UsedImplicitly]
    private void Start()
    {
        if (User == null && ServerActions != this)
        {
            Destroy(this);
            Logger.DevkitServer.LogError(Source, "Invalid EditorActions setup; EditorUser not found!");
            return;
        }
        
        Subscribe();
        Logger.DevkitServer.LogDebug(Source, $"Editor actions module created for {(User == null ? "Server".Colorize(Color.cyan) : User.SteamId.Format())} ( owner: {IsOwner.Format()} ).");
        
#if CLIENT
        if (IsOwner)
        {
            if (TemporaryEditorActions is { NeedsToFlush: true })
            {
                IsPlayingCatchUp = true;
                CatchUpCoroutine = StartCoroutine(TemporaryEditorActions.Flush());
                _isRunningCatchUpCoroutine = true;
                if (CanProcess)
                {
#if PRINT_ACTION_SIMPLE
                    Logger.DevkitServer.LogDebug(Source, "Catch-up completed in one frame.");
#endif
                    IsPlayingCatchUp = false;
                }
            }
            else
            {
                CanProcess = true;
                HasProcessedPendingHierarchyObjects = true;
                HasProcessedPendingLevelObjects = true;
                HasProcessedPendingFlags = true;
                HasProcessedPendingRoads = true;
                HasProcessedPendingSpawnTables = true;
                HasProcessedPendingSpawnTiers = true;
                HasProcessedPendingSpawnAssets = true;
                HasProcessedPendingSpawns = true;
                if (TemporaryEditorActions != null)
                {
                    TemporaryEditorActions.Dispose();
                    TemporaryEditorActions = null;
                }
                IsPlayingCatchUp = false;
#if PRINT_ACTION_SIMPLE
                Logger.DevkitServer.LogDebug(Source, "Catch-up not needed.");
#endif
            }
        }
#endif
    }

    [UsedImplicitly]
    private void OnDestroy()
    {
        ((IDisposable)Settings).Dispose();
        Unsubscribe();
        User = null!;
        IsOwner = false;
#if CLIENT
        if (_isRunningCatchUpCoroutine && CatchUpCoroutine != null)
        {
            StopCoroutine(CatchUpCoroutine);
            CatchUpCoroutine = null;
        }
#endif
    }
    [Pure]
    internal TAction? FindFirstPendingAction<TAction>() where TAction : class, IAction
    {
        for (int i = 0; i < _pendingActions.Count; ++i)
        {
            if (_pendingActions[i] is TAction t)
                return t;
        }

        return null;
    }
    internal void QueueAction<TAction>(TAction action, bool delayWithDeltaTime = false) where TAction : class, IAction
    {
        action.Instigator = Provider.client;
        if (IsOwner)
            LocalLastAction = CachedTime.RealtimeSinceStartup;
        if (!delayWithDeltaTime)
        {
            if (_queuedThisFrame)
                action.DeltaTime = 0f;
            else
                _queuedThisFrame = true;
        }
        if (action is IReplacableAction repl)
        {
            for (int i = 0; i < _pendingActions.Count; ++i)
            {
                if (_pendingActions[i] is TAction t && ((IReplacableAction)t).TryReplaceFrom(repl))
                {
#if PRINT_ACTION_DETAIL
                    Logger.DevkitServer.LogDebug(Source, $"Action {action.Format()} replaced to {t.Format()}" +
                                                         $", time: {CachedTime.RealtimeSinceStartup:F2}" +
                                                         $", fps: {1f / CachedTime.DeltaTime:F0}" +
                                                         $", queue: {_pendingActions.Count}.{Environment.NewLine}" +
                                                         JsonSerializer.Serialize(t, t.GetType(), DevkitServerConfig.SerializerSettings));
#elif PRINT_ACTION_SIMPLE
                    Logger.DevkitServer.LogDebug(Source, $"Action {action.Format()} replaced to {t.Format()}.");
#endif
                    return;
                }
            }
        }

        _pendingActions.Add(action);
#if PRINT_ACTION_DETAIL
        Logger.DevkitServer.LogDebug(Source, $"Action queued to write: {action.Format()}" +
                                             $", time: {CachedTime.RealtimeSinceStartup:F2}" +
                                             $", fps: {1f / CachedTime.DeltaTime:F0}" +
                                             $", queue: {_pendingActions.Count}.{Environment.NewLine}" +
                                             JsonSerializer.Serialize(action, action.GetType(), DevkitServerConfig.SerializerSettings));
#elif PRINT_ACTION_SIMPLE
        Logger.DevkitServer.LogDebug(Source, $"Action queued to write: {action.Format()}.");
#endif
    }

    internal static void ReceiveActionRelay(
#if SERVER
        ITransportConnection transportConnection,
#endif
        NetPakReader reader)
    {
        if (!reader.ReadUInt16(out ushort len))
        {
            Logger.DevkitServer.LogError(Source, "Failed to read incoming action packet length.");
            return;
        }
        NetFactory.IncrementByteCount(DevkitServerMessage.ActionRelay, false, len + sizeof(ushort));

#if SERVER
        EditorUser? user = UserManager.FromConnection(transportConnection);
        if (user == null)
        {
            Logger.DevkitServer.LogError(Source, $"Failed to find user for action packet from transport connection: {transportConnection.Format()}.");
            return;
        }
#endif
        if (!reader.ReadBytesPtr(len, out byte[] buffer, out int offset))
        {
            Logger.DevkitServer.LogError(Source, "Failed to read action packet.");
            return;
        }
        Reader.LoadNew(new ArraySegment<byte>(buffer, offset, len));
#if CLIENT
        ulong s64 = Reader.ReadUInt64();
        if (!CanProcess && TemporaryEditorActions.Instance != null)
        {
            TemporaryEditorActions.Instance.HandleReadPackets(new CSteamID(s64), Reader);
            return;
        }
        EditorUser? user = UserManager.FromId(s64);
        if (user == null)
        {
            if (s64 == 0ul && ServerActions != null)
            {
                ServerActions.HandleReadPackets(Reader);
            }
            Logger.DevkitServer.LogError(Source, $"Failed to find user for action packet from a steam id: {s64.Format()}.");
            return;
        }

        if (user.IsOwner)
        {
            Logger.DevkitServer.LogError(Source, "Received action packet relay back from self.");
            return;
        }
#endif
        user.Actions.HandleReadPackets(Reader
#if SERVER
            , len
#endif
            );
    }
    internal void FlushEdits()
    {
        if (_pendingActions.Count < 1)
            return;

#if PRINT_ACTION_SIMPLE
        Logger.DevkitServer.LogDebug(Source, $"Flushing {_pendingActions.Count.Format()} action(s).");
#endif
        WriteEditBuffer(Writer, 0, _pendingActions.Count);
#if CLIENT
        NetFactory.SendGeneric(DevkitServerMessage.ActionRelay, Writer.ToArraySegmentAndDontFlush(), true);
#else
        NetFactory.SendGeneric(DevkitServerMessage.ActionRelay, Writer.ToArraySegmentAndDontFlush(), DevkitServerUtility.GetAllConnections(), true);
#endif
        Writer.Flush();
    }
    public static (int Data, int Settings) GetMaxSizes(IAction action) =>
        EditorActionsCodeGeneration.Attributes.TryGetValue(action.Type, out ActionAttribute attr)
            ? (attr.Capacity + ActionBaseSize, attr.OptionCapacity + ActionSettingsCollection.BaseSize)
            : (sizeof(float) + ActionBaseSize, ActionSettingsCollection.BaseSize);
               // DeltaTime

    [UsedImplicitly]
    private void Update()
    {
#if CLIENT
        if (!CanProcess)
            return;
#endif
        float t = CachedTime.RealtimeSinceStartup;
        _queuedThisFrame = false;
        if (IsOwner)
        {
            if (t - _lastFlush >= ActionFlushInterval)
            {
                _lastFlush = t;
                FlushEdits();
            }
#if CLIENT
            if (IsPlayingCatchUp)
            {
                IsPlayingCatchUp = false;
#if PRINT_ACTION_SIMPLE
                Logger.DevkitServer.LogDebug(Source, "Done playing catch-up.");
#endif
            }
#endif
            return;
        }
        while (_pendingActions.Count > 0 && t >= _nextApply)
        {
            IAction action = _pendingActions[^1];
            _nextApply += action.DeltaTime;
            try
            {
                ApplyAction(action, this);
            }
            finally
            {
                _pendingActions.RemoveAt(_pendingActions.Count - 1);
                if (action is IDisposable d)
                    d.Dispose();
            }
        }
    }
    internal static bool ApplyAction(IAction action, IActionListener listener)
    {
        if (OnApplyingAction != null)
        {
            bool allow = true;
            OnApplyingAction.Invoke(listener, action, ref allow);
            if (!allow) return false;
        }
        ActiveAction = action;
        try
        {
            action.Apply();
#if PRINT_ACTION_DETAIL
            Logger.DevkitServer.LogDebug(Source, $"Action applied: {action.Format()}" +
                                                 $", time: {CachedTime.RealtimeSinceStartup:F2}" +
                                                 $", fps: {1f / CachedTime.DeltaTime:F0}" +
                                                 $", queue: {listener.QueueSize}.{Environment.NewLine}" +
                                                 JsonSerializer.Serialize(action, action.GetType(), DevkitServerConfig.SerializerSettings));
#elif PRINT_ACTION_SIMPLE
            Logger.DevkitServer.LogDebug(Source, $"Action applied: {action.Format()}.");
#endif
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError(Source, ex, $"Error applying action: {action.Format()}.");
            return false;
        }
        finally
        {
            ActiveAction = null!;
            if (action is IDisposable disposable)
                disposable.Dispose();
            OnAppliedAction?.Invoke(listener, action);
        }

        return true;
    }
    private int CountNumActionsInPacket(out int skipped, int index = 0, int length = -1)
    {
        skipped = 0;
        if (length < 0)
            length = _pendingActions.Count - index;
        int ct = 0;
        int size = 0;
        length += index;
        for (int i = index; i < length; ++i)
        {
            if (ct >= byte.MaxValue)
                break;
            IAction pendingAction = _pendingActions[i];
#if SERVER
            if (pendingAction is IServersideAction)
            {
                ++skipped;
                continue;
            }
#endif
            int s2 = pendingAction.CalculateSize();
            if (size + s2 > MaxPacketSize)
            {
                if (ct == 0)
                    ++ct;
                break;
            }
            size += s2;
            ++ct;
        }

        return Math.Min(byte.MaxValue, ct);
    }
    private void WriteEditBuffer(ByteWriter writer, int index, int length)
    {
        if (!EditorActionsCodeGeneration.Init)
            return;
        ThreadUtil.assertIsGameThread();
#if SERVER
        writer.Write(User == null ? 0ul : User.SteamId.m_SteamID);
#endif
        writer.Write(DataVersion);
        if (index + length > _pendingActions.Count)
            length = _pendingActions.Count - index;
        int ct = CountNumActionsInPacket(out int skipped, index, length);
        writer.Write((byte)ct);
        List<ActionSettingsCollection> c = ListPool<ActionSettingsCollection>.claim();
        int count2 = ct + index + skipped;
        for (int i = index; i < count2; ++i)
        {
            IAction action = _pendingActions[i];
#if SERVER
            if (action is IServersideAction)
                continue;
#endif
#if PRINT_ACTION_SIMPLE
            Logger.DevkitServer.LogDebug(Source, $"Queued action at index {(i - index).Format()}: {action.Format()}.");
#endif
            ActionSettingsCollection? toAdd = null;

            EditorActionsCodeGeneration.OnWritingAction!(this, ref toAdd, action);

            if (toAdd != null)
            {
                Settings.SetSettings(toAdd);
                toAdd.StartIndex = (byte)(i - index);
                c.Add(toAdd);
#if PRINT_ACTION_SIMPLE
                Logger.DevkitServer.LogDebug(Source, $"Queued data at index {toAdd.StartIndex.Format()}: {toAdd.Format()}.");
#endif
            }
        }

        byte ct2 = (byte)c.Count;
        writer.Write(ct2);
#if PRINT_ACTION_SIMPLE
        Logger.DevkitServer.LogDebug(Source, $"Writing {ct2.Format()} collections.");
#endif
        for (int i = 0; i < ct2; ++i)
        {
            ActionSettingsCollection collection = c[i];
            collection.Write(writer);
        }

        ListPool<ActionSettingsCollection>.release(c);

#if PRINT_ACTION_SIMPLE
        Logger.DevkitServer.LogDebug(Source, $"Writing {ct.Format()} actions.");
#endif
        for (int i = index; i < count2; ++i)
        {
            IAction action = _pendingActions[i];
#if SERVER
            if (action is IServersideAction)
                continue;
#endif
            writer.Write(action.Type);
            action.Write(writer);
#if CLIENT
            if (action is IDisposable disposable)
                disposable.Dispose();
#endif
        }
#if CLIENT
        _pendingActions.RemoveRange(0, ct);
#endif
    }

    private void HandleReadPackets(ByteReader reader
#if SERVER
        , int bufferLength
#endif
        )
    {
        if (!EditorActionsCodeGeneration.Init)
            return;
        ThreadUtil.assertIsGameThread();
#if SERVER
        int offset = reader.Position;
        byte[] buffer = reader.InternalBuffer!;
#endif
        ReadDataVersion = reader.ReadUInt16();
        int ct = reader.ReadUInt8();
        int ct2 = reader.ReadUInt8();
        List<ActionSettingsCollection> c = ListPool<ActionSettingsCollection>.claim();
        for (int i = 0; i < ct2; ++i)
        {
            ActionSettingsCollection collection = ActionSettings.CollectionPool.claim();
            collection.Read(reader);
            c.Add(collection);
        }
        int collIndex = -1, stInd = _pendingActions.Count;
        float t = CachedTime.RealtimeSinceStartup;
        if (stInd == 0)
            _nextApply = t;
#if SERVER
        bool anyInvalid = false;
#endif
        for (int i = 0; i < ct; ++i)
        {
            while (c.Count > collIndex + 1 && c[collIndex + 1].StartIndex <= i)
            {
                ++collIndex;
                ActionSettingsCollection collection = c[collIndex];
                Settings.SetSettings(collection);
#if PRINT_ACTION_SIMPLE
                Logger.DevkitServer.LogDebug(Source, $"Loading option collection at index {collection.StartIndex.Format()}: {collection.Format()}.");
#endif
            }
            DevkitServerActionType type = reader.ReadEnum<DevkitServerActionType>();
            IAction? action = EditorActionsCodeGeneration.CreateAction!(type);
#if PRINT_ACTION_SIMPLE
            Logger.DevkitServer.LogDebug(Source, $"Loading action #{i.Format()} {action.Format()}, collection index: {collIndex.Format()}.");
#endif
            if (action != null)
            {
                action.Instigator = User == null ? Provider.server : User.SteamId;
                EditorActionsCodeGeneration.OnReadingAction!(this, action);
                action.Read(reader);
#if SERVER
                if (action is IServersideAction)
                {
                    _pendingActions.Add(action);
                    anyInvalid = true;
                }
                else if (action.CheckCanApply())
                {
                    _pendingActions.Add(action);
                }
                else
                {
                    anyInvalid = true;
                    Logger.DevkitServer.LogDebug(Source, $"Invalid action request filtered from {action.Instigator.Format()}: {action.Format()}.");
                    if (action is ITerrainAction tAction && TileSync.ServersideAuthority != null && TileSync.ServersideAuthority.HasAuthority)
                    {
                        TileSync.ServersideAuthority.InvalidateBounds(tAction.Bounds, tAction.EditorType switch
                        {
                            TerrainEditorType.Heightmap => TileSync.DataType.Heightmap,
                            TerrainEditorType.Splatmap => TileSync.DataType.Splatmap,
                            _ => TileSync.DataType.Holes
                        }, t);
                    }
                    if (CachedTime.RealtimeSinceStartup - _lastNoPermissionMessage > 5f)
                    {
                        if (User != null)
                            EditorMessage.SendNoPermissionMessage(User);
                        _lastNoPermissionMessage = CachedTime.RealtimeSinceStartup;
                    }
                }
#else
                _pendingActions.Add(action);
#endif
            }
        }
#if SERVER
        if (Provider.clients.Count + EditorLevel.PendingToReceiveActions.Count > 1)
        {
            Logger.DevkitServer.LogDebug(Source, $"Relaying {(_pendingActions.Count - stInd).Format()} action(s).");
            int capacity = Provider.clients.Count - 1 + EditorLevel.PendingToReceiveActions.Count;
            PooledTransportConnectionList list = NetFactory.GetPooledTransportConnectionList(capacity);
            list.IncreaseCapacity(capacity);
            for (int i = 0; i < Provider.clients.Count; ++i)
            {
                SteamPlayer pl = Provider.clients[i];
                if (User == null || pl.playerID.steamID.m_SteamID != User.SteamId.m_SteamID)
                    list.Add(pl.transportConnection);
            }

            // also send to pending users
            list.AddRange(EditorLevel.PendingToReceiveActions);
            for (int i = list.Count - 1; i >= 0; --i)
            {
                ITransportConnection conn = list[i];
                for (int j = i - 1; j >= 0; --j)
                {
                    if (ReferenceEquals(conn, list[j]))
                        list.RemoveAt(j);
                }
            } 
            if (anyInvalid)
            {
                WriteEditBuffer(Writer, stInd, _pendingActions.Count - stInd);
                NetFactory.SendGeneric(DevkitServerMessage.ActionRelay, Writer.ToArraySegmentAndDontFlush(), list, true);
                Writer.Flush();
            }

            // faster to just copy the array so do that when possible
            else
            {
                byte[] sendBytes = new byte[sizeof(ulong) + bufferLength];
                Buffer.BlockCopy(buffer, offset, sendBytes, sizeof(ulong), bufferLength);
                UnsafeBitConverter.GetBytes(sendBytes, User == null ? 0ul : User.SteamId.m_SteamID);
                NetFactory.SendGeneric(DevkitServerMessage.ActionRelay, sendBytes, list, reliable: false);
            }
        }
#endif
        ReadDataVersion = 0;
        if (stInd != _pendingActions.Count)
        {
            // reverse, queue at beginning
            IAction[] tempBuffer = new IAction[_pendingActions.Count - stInd];
            _pendingActions.CopyTo(stInd, tempBuffer, 0, tempBuffer.Length);
            _pendingActions.RemoveRange(stInd, tempBuffer.Length);
            Array.Reverse(tempBuffer);
            _pendingActions.InsertRange(0, tempBuffer);
#if PRINT_ACTION_SIMPLE
            Logger.DevkitServer.LogDebug(Source, $"Received actions: {tempBuffer.Length.Format()}.");
#endif
        }

        ListPool<ActionSettingsCollection>.release(c);
    }

#if SERVER
    /// <summary>
    /// Queues an action not linked to any individual player to be executed on all clients.
    /// </summary>
    /// <param name="delayWithDeltaTime">Should use deltaTime to space out actions. Used for actions that get sent every frame.</param>
    /// <returns><see langword="false"/> if <see cref="ServerActions"/> hasn't been initialized yet, otherwise <see langword="true"/>.</returns>
    public static bool QueueServerAction(IAction action, bool delayWithDeltaTime = false)
    {
        action.Instigator = Provider.server;
        if (ServerActions == null)
            return false;

        ServerActions.QueueAction(action, delayWithDeltaTime);
        return true;
    }
#endif
}