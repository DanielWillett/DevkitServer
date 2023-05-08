using DevkitServer.Multiplayer.Networking;
using DevkitServer.Players;
using DevkitServer.Util.Encoding;
using JetBrains.Annotations;
using SDG.Framework.Utilities;
using SDG.NetPak;

namespace DevkitServer.Multiplayer.Actions;

[EarlyTypeInit]
public sealed class EditorActions : MonoBehaviour
{
    public const ushort DataVersion = 0;

    private static readonly ByteReader Reader = new ByteReader { ThrowOnError = true };
    private static readonly ByteWriter Writer = new ByteWriter(false, 8192);

    /// <remarks>Reversed for everyone but the owner. </remarks>
    private readonly List<IAction> _pendingActions = new List<IAction>();

    /// <summary>Ran before an action is applied, with the ability to cancel processing.</summary>
    public static event ApplyingAction? OnApplyingAction;

    /// <summary>Ran after an action is applied.</summary>
    public static event AppliedAction? OnAppliedAction;

    internal static ushort ReadDataVersion = DataVersion;

#if CLIENT
    private float _lastFlush;
    private bool _queuedThisFrame;
#endif

    private float _nextApply;
    public EditorUser User { get; internal set; } = null!;
    public bool IsOwner { get; private set; }
    public IAction? ActiveAction { get; private set; }
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
            Logger.LogError("Invalid EditorActions setup; EditorUser not found!");
            return;
        }

#if CLIENT
        IsOwner = User == EditorUser.User;
#endif
        Subscribe();
        Logger.LogDebug("Editor actions module created for " + User.SteamId.m_SteamID + " ( owner: " + IsOwner + " ).");
    }

    [UsedImplicitly]
    private void OnDestroy()
    {
        Unsubscribe();
        User = null!;
        IsOwner = false;
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
        user.Actions.HandleReadPackets(Reader);

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

            NetFactory.SendGeneric(NetFactory.DevkitMessage.ActionRelay, sendBytes, list, reliable: false);
        }
#endif
    }
#if CLIENT
    internal void FlushEdits()
    {
        if (_pendingActions.Count > 0)
        {
            Logger.LogDebug("Flushing " + _pendingActions.Count.Format() + " action(s).");
            WriteEditBuffer(Writer);
            int len = Writer.Count;
            NetFactory.SendGeneric(NetFactory.DevkitMessage.ActionRelay, Writer.FinishWrite(), 0, len, true);
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
            _queuedThisFrame = false;
            if (t - _lastFlush >= 1f)
            {
                _lastFlush = t;
                FlushEdits();
            }

            return;
        }
#endif
        while (_pendingActions.Count > 0 && t >= _nextApply)
        {
            IAction action = _pendingActions[_pendingActions.Count - 1];
            _nextApply += action.DeltaTime;
            if (OnApplyingAction != null)
            {
                bool allow = true;
                OnApplyingAction.Invoke(this, action, ref allow);
                if (!allow) continue;
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
                _pendingActions.RemoveAt(_pendingActions.Count - 1);
                OnAppliedAction?.Invoke(this, action);
            }
        }
    }
#if CLIENT
    private void WriteEditBuffer(ByteWriter writer)
    {
        if (!EditorActionsCodeGeneration.Init)
            return;
        ThreadUtil.assertIsGameThread();

        writer.Write(DataVersion);
        byte ct = (byte)Math.Min(_pendingActions.Count, 96);
        writer.Write(ct);
        List<ActionSettingsCollection> c = ListPool<ActionSettingsCollection>.claim();
        for (int i = 0; i < ct; ++i)
        {
            IAction action = _pendingActions[i];
            ActionSettingsCollection? toAdd = null;

            EditorActionsCodeGeneration.OnWritingAction!(this, ref toAdd, action);

            if (toAdd != null)
            {
                Settings.SetSettings(toAdd);
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
            ActionSettingsCollection collection = c[i];
            collection.Write(writer);
        }

        ListPool<ActionSettingsCollection>.release(c);

        Logger.LogDebug("Writing " + ct + " actions.");
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
#endif

    private void HandleReadPackets(ByteReader reader)
    {
        if (!EditorActionsCodeGeneration.Init)
            return;
        ThreadUtil.assertIsGameThread();

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
        int collIndex = -1;
        LoadCollection(0);
        int stInd = _pendingActions.Count;
        if (stInd == 0)
            _nextApply = Time.realtimeSinceStartup;
        for (int i = 0; i < ct; ++i)
        {
            if (c.Count > collIndex + 1 && c[collIndex + 1].StartIndex >= i)
                LoadCollection(collIndex + 1);
            ActionType type = reader.ReadEnum<ActionType>();
            IAction? action = EditorActionsCodeGeneration.CreateAction!(type);
            if (action != null)
            {
                action.Instigator = User.SteamId;
                EditorActionsCodeGeneration.OnReadingAction!(this, action);
                action.Read(reader);
                _pendingActions.Add(action);
            }
        }
        if (stInd != _pendingActions.Count)
        {
            // reverse, queue at beginning
            IAction[] tempBuffer = new IAction[_pendingActions.Count - stInd];
            _pendingActions.CopyTo(stInd, tempBuffer, 0, tempBuffer.Length);
            _pendingActions.RemoveRange(stInd, tempBuffer.Length);
            Array.Reverse(tempBuffer);
            _pendingActions.InsertRange(0, tempBuffer);
            Logger.LogDebug("Received actions: " + tempBuffer.Length + ".");
        }

        ListPool<ActionSettingsCollection>.release(c);

        void LoadCollection(int index)
        {
            if (c.Count <= index)
                return;
            collIndex = index;
            ActionSettingsCollection collection = c[collIndex];
            Settings.SetSettings(collection);
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

public delegate void AppliedAction(EditorActions caller, IAction action);
public delegate void ApplyingAction(EditorActions caller, IAction action, ref bool execute);