#if SERVER
using DevkitServer.Multiplayer.Levels;
using DevkitServer.Multiplayer.Sync;
using DevkitServer.Players.UI;
#endif
#if CLIENT
using DevkitServer.API.Abstractions;
#endif
using System.Reflection;
using DevkitServer.Multiplayer.Networking;
using DevkitServer.Players;
using DevkitServer.Util.Encoding;
using SDG.Framework.Utilities;
using SDG.NetPak;

namespace DevkitServer.Multiplayer.Actions;

[EarlyTypeInit]
public sealed class EditorActions : MonoBehaviour, IActionListener
{
    internal static readonly FieldInfo LocalLastActionField = typeof(EditorActions).GetField(nameof(LocalLastAction), BindingFlags.NonPublic | BindingFlags.Static)!;
    internal static float LocalLastAction;
    public const ushort DataVersion = 1;
    public const ushort ActionBaseSize = 1;
    public const int MaxPacketSize = 16384;
    private const float ActionFlushInterval = 0.125f;

    private static readonly ByteReader Reader = new ByteReader { ThrowOnError = true };
    private static readonly ByteWriter Writer = new ByteWriter(false, 8192);

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
    public TerrainActions TerrainActions { get; }
    public FoliageActions FoliageActions { get; }
    public HierarchyActions HierarchyActions { get; }
    public ObjectActions ObjectActions { get; }
    public SpawnActions SpawnActions { get; }

    private EditorActions()
    {
        Settings = new ActionSettings(this);
        TerrainActions = new TerrainActions(this);
        FoliageActions = new FoliageActions(this);
        HierarchyActions = new HierarchyActions(this);
        ObjectActions = new ObjectActions(this);
        SpawnActions = new SpawnActions(this);
    }

    [UsedImplicitly]
    private void Start()
    {
        if (User == null && ServerActions != this)
        {
            Destroy(this);
            Logger.LogError("Invalid EditorActions setup; EditorUser not found!", method: "EDITOR ACTIONS");
            return;
        }
        
        Subscribe();
        Logger.LogDebug($"[EDITOR ACTIONS] Editor actions module created for {(User == null ? "Server".Colorize(Color.cyan) : User.SteamId.Format())} ( owner: {IsOwner.Format()} ).");
        
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
                    Logger.LogDebug("[EDITOR ACTIONS] Catch-up completed in one frame.");
                    IsPlayingCatchUp = false;
                }
            }
            else
            {
                CanProcess = true;
                HasProcessedPendingHierarchyObjects = true;
                HasProcessedPendingLevelObjects = true;
                if (TemporaryEditorActions != null)
                {
                    TemporaryEditorActions.Dispose();
                    TemporaryEditorActions = null;
                }
                IsPlayingCatchUp = false;
                Logger.LogDebug("[EDITOR ACTIONS] Catch-up not needed.");
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
    internal void QueueAction(IAction action, bool delayWithDeltaTime = false)
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
        _pendingActions.Add(action);
    }

    internal static void ReceiveActionRelay(
#if SERVER
        ITransportConnection transportConnection,
#endif
        NetPakReader reader)
    {
        if (!reader.ReadUInt16(out ushort len))
        {
            Logger.LogError("Failed to read incoming action packet length.", method: "EDITOR ACTIONS");
            return;
        }
        NetFactory.IncrementByteCount(false, DevkitMessage.ActionRelay, len + sizeof(ushort));

#if SERVER
        EditorUser? user = UserManager.FromConnection(transportConnection);
        if (user == null)
        {
            Logger.LogError("Failed to find user for action packet from transport connection: " + transportConnection.Format() + ".", method: "EDITOR ACTIONS");
            return;
        }
#endif
        if (!reader.ReadBytesPtr(len, out byte[] buffer, out int offset))
        {
            Logger.LogError("Failed to read action packet.", method: "EDITOR ACTIONS");
            return;
        }
        Reader.LoadNew(buffer);
        Reader.Skip(offset);
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
            Logger.LogError("Failed to find user for action packet from a steam id: " + s64.Format() + ".", method: "EDITOR ACTIONS");
            return;
        }

        if (user.IsOwner)
        {
            Logger.LogError("Received action packet relay back from self.", method: "EDITOR ACTIONS");
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

        Logger.LogDebug("[EDITOR ACTIONS] Flushing " + _pendingActions.Count.Format() + " action(s).");
        WriteEditBuffer(Writer, 0, _pendingActions.Count);
        int len = Writer.Count;
#if CLIENT
        NetFactory.SendGeneric(DevkitMessage.ActionRelay, Writer.FinishWrite(), 0, len, true);
#else
        NetFactory.SendGeneric(DevkitMessage.ActionRelay, Writer.FinishWrite(), DevkitServerUtility.GetAllConnections(), 0, len, true);
#endif
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
                Logger.LogDebug("[EDITOR ACTIONS] Done playing catch-up.");
            }
#endif
            return;
        }
        while (_pendingActions.Count > 0 && t >= _nextApply)
        {
            IAction action = _pendingActions[_pendingActions.Count - 1];
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
    internal static void ApplyAction(IAction action, IActionListener listener)
    {
        if (OnApplyingAction != null)
        {
            bool allow = true;
            OnApplyingAction.Invoke(listener, action, ref allow);
            if (!allow) return;
        }
        ActiveAction = action;
        try
        {
            action.Apply();
        }
        finally
        {
            ActiveAction = null!;
            if (action is IDisposable disposable)
                disposable.Dispose();
            OnAppliedAction?.Invoke(listener, action);
        }
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
            Logger.LogDebug("[EDITOR ACTIONS] Queued action at index " + (i - index).Format() + ": " + action.Format() + ".");
            ActionSettingsCollection? toAdd = null;

            EditorActionsCodeGeneration.OnWritingAction!(this, ref toAdd, action);

            if (toAdd != null)
            {
                Settings.SetSettings(toAdd);
                toAdd.StartIndex = (byte)(i - index);
                c.Add(toAdd);
                Logger.LogDebug("[EDITOR ACTIONS] Queued data at index " + toAdd.StartIndex.Format() + ": " + toAdd.Format() + ".");
            }
        }

        byte ct2 = (byte)c.Count;
        writer.Write(ct2);
        Logger.LogDebug("[EDITOR ACTIONS] Writing " + ct2 + " collections.");
        for (int i = 0; i < ct2; ++i)
        {
            ActionSettingsCollection collection = c[i];
            collection.Write(writer);
        }

        ListPool<ActionSettingsCollection>.release(c);

        Logger.LogDebug("[EDITOR ACTIONS] Writing " + ct + " actions.");
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
                Logger.LogDebug($"[EDITOR ACTIONS] Loading option collection at index {collection.StartIndex}: {collection.Format()}.");
            }
            ActionType type = reader.ReadEnum<ActionType>();
            IAction? action = EditorActionsCodeGeneration.CreateAction!(type);
            Logger.LogDebug($"[EDITOR ACTIONS] Loading action #{i.Format()} {action.Format()}, collection index: {collIndex.Format()}.");
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
                    Logger.LogDebug($"[EDITOR ACTIONS] Invalid action request filtered from {action.Instigator.Format()}: {action}.");
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
                            UIMessage.SendNoPermissionMessage(User);
                        _lastNoPermissionMessage = CachedTime.RealtimeSinceStartup;
                    }
                }
#else
                _pendingActions.Add(action);
#endif
            }
        }
#if SERVER
        if (Provider.clients.Count > 1)
        {
            Logger.LogDebug("[EDITOR ACTIONS] Relaying " + (_pendingActions.Count - stInd).Format() + " action(s).");
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
            if (anyInvalid)
            {
                WriteEditBuffer(Writer, stInd, _pendingActions.Count - stInd);
                int len = Writer.Count;
                NetFactory.SendGeneric(DevkitMessage.ActionRelay, Writer.FinishWrite(), list, 0, len, true);
            }

            // faster to just copy the array so do that when possible
            else
            {
                byte[] sendBytes = new byte[sizeof(ulong) + bufferLength];
                Buffer.BlockCopy(buffer, offset, sendBytes, sizeof(ulong), bufferLength);
                UnsafeBitConverter.GetBytes(sendBytes, User == null ? 0ul : User.SteamId.m_SteamID);
                NetFactory.SendGeneric(DevkitMessage.ActionRelay, sendBytes, list, reliable: false);
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
            Logger.LogDebug("[EDITOR ACTIONS] Received actions: " + tempBuffer.Length + ".");
        }

        ListPool<ActionSettingsCollection>.release(c);
    }

#if SERVER
    public static bool QueueServerAction(IAction action, bool delayWithDeltaTime = false)
    {
        action.Instigator = Provider.server;
        if (ServerActions == null)
            return false;

        ServerActions.QueueAction(action, delayWithDeltaTime);
        return true;
    }
#endif

    public void Subscribe()
    {
        TerrainActions.Subscribe();
        FoliageActions.Subscribe();
        HierarchyActions.Subscribe();
        ObjectActions.Subscribe();
        SpawnActions.Subscribe();
    }

    public void Unsubscribe()
    {
        TerrainActions.Unsubscribe();
        FoliageActions.Unsubscribe();
        HierarchyActions.Unsubscribe();
        ObjectActions.Unsubscribe();
        SpawnActions.Unsubscribe();
    }
}
public interface IActionListener
{
    ActionSettings Settings { get; }
}
#if CLIENT
public class TemporaryEditorActions : IActionListener, IDisposable
{
    private static readonly CachedMulticastEvent<Action> EventOnStartListening = new CachedMulticastEvent<Action>(typeof(TemporaryEditorActions), nameof(OnStartListening));
    private static readonly CachedMulticastEvent<Action> EventOnStopListening = new CachedMulticastEvent<Action>(typeof(TemporaryEditorActions), nameof(OnStopListening));
    public static event Action OnStartListening
    {
        add => EventOnStartListening.Add(value);
        remove => EventOnStartListening.Remove(value);
    }
    public static event Action OnStopListening
    {
        add => EventOnStopListening.Add(value);
        remove => EventOnStopListening.Remove(value);
    }
    public static TemporaryEditorActions? Instance { get; private set; }
    private readonly List<PendingHierarchyInstantiation> _hierarchyInstantiations = new List<PendingHierarchyInstantiation>();
    private readonly List<PendingLevelObjectInstantiation> _lvlObjectInstantiations = new List<PendingLevelObjectInstantiation>();
    private readonly List<IAction> _actions = new List<IAction>();
    public ActionSettings Settings { get; }
    public int QueueSize => _actions.Count;
    internal bool NeedsToFlush => _actions.Count > 0 || _hierarchyInstantiations.Count > 0 || _lvlObjectInstantiations.Count > 0;
    private TemporaryEditorActions()
    {
        Settings = new ActionSettings(this);
        Instance = this;
        Logger.LogDebug("[TEMP EDITOR ACTIONS] Initialized.");
    }
    public void QueueInstantiation(IHierarchyItemTypeIdentifier type, Vector3 position, Quaternion rotation, Vector3 scale, ulong owner, NetId netId)
    {
        if (type == null) throw new ArgumentNullException(nameof(type));

        _hierarchyInstantiations.Add(new PendingHierarchyInstantiation(type, position, rotation, scale, owner, netId));
        Logger.LogDebug($"[TEMP EDITOR ACTIONS] Queued hierarchy item instantiation for {type.Format()} when the level loads.");
    }
    public void QueueInstantiation(Asset asset, Vector3 position, Quaternion rotation, Vector3 scale, ulong owner, NetId netId)
    {
        if (asset is not ObjectAsset && asset is not ItemBarricadeAsset and not ItemStructureAsset)
            throw new ArgumentException("Must be either ObjectAsset (LevelObject) or ItemAsset (LevelBuildableObject).", nameof(asset));

        _lvlObjectInstantiations.Add(new PendingLevelObjectInstantiation(asset.getReferenceTo<Asset>(), position, rotation, scale, owner, netId));
        Logger.LogDebug($"[TEMP EDITOR ACTIONS] Queued level object instantiation for {asset.Format()} when the level loads.");
    }
    internal void HandleReadPackets(CSteamID user, ByteReader reader)
    {
        if (!EditorActionsCodeGeneration.Init)
            return;
        ThreadUtil.assertIsGameThread();
        EditorActions.ReadDataVersion = reader.ReadUInt16();
        int ct = reader.ReadUInt8();
        int ct2 = reader.ReadUInt8();
        List<ActionSettingsCollection> c = ListPool<ActionSettingsCollection>.claim();
        for (int i = 0; i < ct2; ++i)
        {
            ActionSettingsCollection collection = ActionSettings.CollectionPool.claim();
            collection.Read(reader);
            c.Add(collection);
        }
        int collIndex = -1, stInd = _actions.Count;
        for (int i = 0; i < ct; ++i)
        {
            if (c.Count > collIndex + 1 && c[collIndex + 1].StartIndex >= i)
                LoadCollection(collIndex + 1);
            ActionType type = reader.ReadEnum<ActionType>();
            IAction? action = EditorActionsCodeGeneration.CreateAction!(type);
            if (action != null)
            {
                action.Instigator = user;
                EditorActionsCodeGeneration.OnReadingAction!(this, action);
                action.Read(reader);
                _actions.Add(action);
            }
        }
        EditorActions.ReadDataVersion = 0;
        Logger.LogDebug("[TEMP EDITOR ACTIONS] Received actions: " + (_actions.Count - stInd) + ".");

        ListPool<ActionSettingsCollection>.release(c);

        void LoadCollection(int index)
        {
            if (c.Count <= index)
                return;
            collIndex = index;
            Settings.SetSettings(c[collIndex]);
            Logger.LogDebug("[TEMP EDITOR ACTIONS] Loading option collection: " + c[collIndex].Format() + ".");
        }
    }
    internal IEnumerator Flush()
    {
        for (int i = 0; i < _hierarchyInstantiations.Count; i++)
        {
            PendingHierarchyInstantiation hierarchyItemInstantiation = _hierarchyInstantiations[i];
            HierarchyUtil.ReceiveHierarchyInstantiation(MessageContext.Nil, hierarchyItemInstantiation.Type, hierarchyItemInstantiation.Position, hierarchyItemInstantiation.Rotation,
                hierarchyItemInstantiation.Scale, hierarchyItemInstantiation.Owner, hierarchyItemInstantiation.NetId);
        }

        EditorActions.HasProcessedPendingHierarchyObjects = true;

        if (_hierarchyInstantiations.Count > 20)
            yield return null;
        for (int i = 0; i < _lvlObjectInstantiations.Count; i++)
        {
            PendingLevelObjectInstantiation lvlObjectInstantiation = _lvlObjectInstantiations[i];
            LevelObjectUtil.ReceiveInstantiation(MessageContext.Nil, lvlObjectInstantiation.Asset.GUID, lvlObjectInstantiation.Position, lvlObjectInstantiation.Rotation,
                lvlObjectInstantiation.Scale, lvlObjectInstantiation.Owner, lvlObjectInstantiation.NetId);
        }

        EditorActions.HasProcessedPendingLevelObjects = true;
        if (_lvlObjectInstantiations.Count > 10)
            yield return null;
        for (int i = 0; i < _actions.Count; ++i)
        {
            if (i % 30 == 0 && i > 0)
                yield return null;
            IAction action = _actions[i];
            EditorActions.ApplyAction(action, this);
        }
        _actions.Clear();
        EditorActions.CanProcess = true;
        Dispose();
        if (EditorActions.TemporaryEditorActions == this)
            EditorActions.TemporaryEditorActions = null;
        EditorActions.CatchUpCoroutine = null;
        EventOnStopListening.TryInvoke();
    }
    internal static void BeginListening()
    {
        _ = new TemporaryEditorActions();
        EditorActions.CanProcess = false;
        EditorActions.HasProcessedPendingHierarchyObjects = false;
        EditorActions.HasProcessedPendingLevelObjects = false;
        EventOnStartListening.TryInvoke();
    }
    public void Dispose()
    {
        ((IDisposable)Settings).Dispose();
        _actions.Clear();
        Instance = null;
        Logger.LogDebug("[TEMP EDITOR ACTIONS] Cleaned up.");
    }

    private readonly struct PendingHierarchyInstantiation
    {
        public readonly IHierarchyItemTypeIdentifier Type;
        public readonly NetId NetId;
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public readonly Vector3 Scale;
        public readonly ulong Owner;
        public PendingHierarchyInstantiation(IHierarchyItemTypeIdentifier type, Vector3 position, Quaternion rotation, Vector3 scale, ulong owner, NetId netId)
        {
            Type = type;
            NetId = netId;
            Position = position;
            Rotation = rotation;
            Scale = scale;
            Owner = owner;
        }
    }
    private readonly struct PendingLevelObjectInstantiation
    {
        public readonly AssetReference<Asset> Asset;
        public readonly NetId NetId;
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public readonly Vector3 Scale;
        public readonly ulong Owner;
        public PendingLevelObjectInstantiation(AssetReference<Asset> assetAsset, Vector3 position, Quaternion rotation, Vector3 scale, ulong owner, NetId netId)
        {
            Asset = assetAsset;
            Position = position;
            Rotation = rotation;
            Scale = scale;
            Owner = owner;
            NetId = netId;
        }
    }
}
#endif
public delegate void AppliedAction(IActionListener caller, IAction action);
public delegate void ApplyingAction(IActionListener caller, IAction action, ref bool execute);