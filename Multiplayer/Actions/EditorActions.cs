#if SERVER
using DevkitServer.Multiplayer.LevelData;
#endif
using DevkitServer.Multiplayer.Networking;
using DevkitServer.Players;
using DevkitServer.Util.Encoding;
using JetBrains.Annotations;
using SDG.Framework.Utilities;
using SDG.NetPak;

namespace DevkitServer.Multiplayer.Actions;

[EarlyTypeInit]
public sealed class EditorActions : MonoBehaviour, IActionListener
{
    public const ushort DataVersion = 1;

    private static readonly ByteReader Reader = new ByteReader { ThrowOnError = true };
    private static readonly ByteWriter Writer = new ByteWriter(false, 8192);

    /// <remarks>Reversed for everyone but the owner. </remarks>
    private readonly List<IAction> _pendingActions = new List<IAction>();

    /// <summary>Ran before an action is applied, with the ability to cancel processing.</summary>
    public static event ApplyingAction? OnApplyingAction;

    /// <summary>Ran after an action is applied.</summary>
    public static event AppliedAction? OnAppliedAction;


    internal static ushort ReadDataVersion;

#if CLIENT
    internal static TemporaryEditorActions? TemporaryEditorActions;
    internal static bool CanProcess;
    private float _lastFlush;
    private bool _queuedThisFrame;
    /// <summary>
    /// Is the player catching up after downloading the map.
    /// </summary>
    public static bool IsPlayingCatchUp { get; private set; }

    internal static Coroutine? CatchUpCoroutine;
    private bool _isRunningCatchUpCoroutine;
#endif

    private float _nextApply;
    public EditorUser User { get; internal set; } = null!;
    public bool IsOwner { get; private set; }
    public static IAction? ActiveAction { get; private set; }
    public ActionSettings Settings { get; }
    public TerrainActions TerrainActions { get; }
    public FoliageActions FoliageActions { get; }

    private EditorActions()
    {
        Settings = new ActionSettings(this);
        TerrainActions = new TerrainActions(this);
        FoliageActions = new FoliageActions(this);
    }

    [UsedImplicitly]
    private void Start()
    {
        if (User == null)
        {
            Destroy(this);
            Logger.LogError("Invalid EditorActions setup; EditorUser not found!", method: "EDITOR ACTIONS");
            return;
        }

#if CLIENT
        IsOwner = User == EditorUser.User;
#endif
        Subscribe();
        Logger.LogDebug("[EDITOR ACTIONS] Editor actions module created for " + User.SteamId.m_SteamID + " ( owner: " + IsOwner + " ).");

#if CLIENT
        if (IsOwner)
        {
            if (TemporaryEditorActions is { QueueSize: > 0 })
            {
                IsPlayingCatchUp = true;
                CanProcess = false;
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
#if CLIENT
    internal void QueueAction(IAction action)
    {
        action.Instigator = Provider.client;
        if (_queuedThisFrame)
            action.DeltaTime = 0f;
        else
            _queuedThisFrame = true;
        _pendingActions.Add(action);
    }
#endif

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
            Logger.LogError("Failed to find user for action packet from a steam id: " + s64.Format() + ".", method: "EDITOR ACTIONS");
            return;
        }
#endif
        user.Actions.HandleReadPackets(Reader
#if SERVER
            , len
#endif
            );
    }
#if CLIENT
    internal void FlushEdits()
    {
        if (_pendingActions.Count > 0)
        {
            Logger.LogDebug("[EDITOR ACTIONS] Flushing " + _pendingActions.Count.Format() + " action(s).");
            WriteEditBuffer(Writer, 0, _pendingActions.Count);
            int len = Writer.Count;
            NetFactory.SendGeneric(NetFactory.DevkitMessage.ActionRelay, Writer.FinishWrite(), 0, len, true);
        }
    }
#endif

    [UsedImplicitly]
    private void Update()
    {
#if CLIENT
        if (!CanProcess)
            return;
#endif
        float t = Time.realtimeSinceStartup;
#if CLIENT
        if (IsOwner)
        {
            _queuedThisFrame = false;
            if (t - _lastFlush >= 1f)
            {
                _lastFlush = t;
                FlushEdits();
            }

            if (IsPlayingCatchUp)
            {
                IsPlayingCatchUp = false;
                Logger.LogDebug("[EDITOR ACTIONS] Done playing catch-up.");
            }
            return;
        }
#endif
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
    private void WriteEditBuffer(ByteWriter writer, int index, int length)
    {
        if (!EditorActionsCodeGeneration.Init)
            return;
        ThreadUtil.assertIsGameThread();
#if SERVER
        writer.Write(User.SteamId.m_SteamID);
#endif
        writer.Write(DataVersion);
        byte ct = (byte)Math.Min(length, 96);
        writer.Write(ct);
        List<ActionSettingsCollection> c = ListPool<ActionSettingsCollection>.claim();
        int count2 = ct + index;
        for (int i = index; i < count2; ++i)
        {
            IAction action = _pendingActions[i];
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
        for (int i = 0; i < ct; ++i)
        {
            IAction action = _pendingActions[i];
            writer.Write(action.Type);
            action.Write(writer);
            if (action is IDisposable disposable)
                disposable.Dispose();
        }

        _pendingActions.RemoveRange(0, ct);
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
        if (stInd == 0)
            _nextApply = Time.realtimeSinceStartup;
#if SERVER
        bool anyInvalid = false;
#endif
        for (int i = 0; i < ct; ++i)
        {
            while (c.Count > collIndex + 1 && c[collIndex + 1].StartIndex >= i)
                LoadCollection(collIndex + 1);
            ActionType type = reader.ReadEnum<ActionType>();
            IAction? action = EditorActionsCodeGeneration.CreateAction!(type);
            if (action != null)
            {
                action.Instigator = User.SteamId;
                EditorActionsCodeGeneration.OnReadingAction!(this, action);
                action.Read(reader);
#if SERVER
                if (action.CheckCanApply())
                    _pendingActions.Add(action);
                else
                {
                    anyInvalid = true;
                    Logger.LogDebug($"[EDITOR ACTIONS] Invalid action request filtered from {action.Instigator.Format()}: {action}.");
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
            if (list.Capacity < capacity)
                list.Capacity = capacity;
            for (int i = 0; i < Provider.clients.Count; ++i)
            {
                SteamPlayer pl = Provider.clients[i];
                if (pl.playerID.steamID.m_SteamID != User.SteamId.m_SteamID)
                    list.Add(pl.transportConnection);
            }

            // also send to pending users
            list.AddRange(EditorLevel.PendingToReceiveActions);
            if (anyInvalid)
            {
                WriteEditBuffer(Writer, stInd, _pendingActions.Count - stInd);
                int len = Writer.Count;
                NetFactory.SendGeneric(NetFactory.DevkitMessage.ActionRelay, Writer.FinishWrite(), list, 0, len, true);
            }

            // faster to just copy the array so do that when possible
            else
            {
                byte[] sendBytes = new byte[sizeof(ulong) + bufferLength];
                Buffer.BlockCopy(buffer, offset, sendBytes, sizeof(ulong), bufferLength);
                UnsafeBitConverter.GetBytes(sendBytes, User.SteamId.m_SteamID);
                NetFactory.SendGeneric(NetFactory.DevkitMessage.ActionRelay, sendBytes, list, reliable: false);
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

        void LoadCollection(int index)
        {
            if (c.Count <= index)
                return;
            collIndex = index;
            ActionSettingsCollection collection = c[collIndex];
            Settings.SetSettings(collection);
            Logger.LogDebug($"[EDITOR ACTIONS] Loading option collection at index {collection.StartIndex}: {collection.Format()}.");
        }
    }

    public void Subscribe()
    {
        TerrainActions.Subscribe();
        FoliageActions.Subscribe();
    }

    public void Unsubscribe()
    {
        TerrainActions.Unsubscribe();
        FoliageActions.Unsubscribe();
    }
}
public interface IActionListener
{
    ActionSettings Settings { get; }
}
#if CLIENT
public class TemporaryEditorActions : IActionListener, IDisposable
{
    public static TemporaryEditorActions? Instance { get; private set; }
    private readonly List<IAction> _actions = new List<IAction>();
    public ActionSettings Settings { get; }
    public int QueueSize => _actions.Count;
    public TemporaryEditorActions()
    {
        Settings = new ActionSettings(this);
        Instance = this;
        Logger.LogDebug("[TEMP EDITOR ACTIONS] Initialized.");
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
        for (int i = 0; i < _actions.Count; ++i)
        {
            if (i != 0 && i % 30 == 0)
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
    }
    public void Dispose()
    {
        ((IDisposable)Settings).Dispose();
        _actions.Clear();
        Instance = null;
        Logger.LogDebug("[TEMP EDITOR ACTIONS] Cleaned up.");
    }
}
#endif
public delegate void AppliedAction(IActionListener caller, IAction action);
public delegate void ApplyingAction(IActionListener caller, IAction action, ref bool execute);