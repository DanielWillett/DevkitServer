#if CLIENT
using DevkitServer.API.Abstractions;
using DevkitServer.API.Multiplayer;
using DevkitServer.Multiplayer.Networking;
using DevkitServer.Util.Encoding;
using SDG.Framework.Utilities;

namespace DevkitServer.Multiplayer.Actions;
public class TemporaryEditorActions : IActionListener, IDisposable
{
    private const string Source = "TEMP EDITOR ACTIONS";

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
    private readonly List<PendingRoadInstantiation> _roadInstantiations = new List<PendingRoadInstantiation>();
    private readonly List<PendingRoadVertexInstantiation> _roadVertexInstantiations = new List<PendingRoadVertexInstantiation>();
    private readonly List<PendingFlagInstantiation> _flagInstantiations = new List<PendingFlagInstantiation>();
    private readonly List<IAction> _actions = new List<IAction>();
    public int QueueSize => _actions.Count;
    public ActionSettings Settings { get; }
    internal bool NeedsToFlush => _actions.Count > 0 || _hierarchyInstantiations.Count > 0 || _lvlObjectInstantiations.Count > 0 || _roadVertexInstantiations.Count > 0 || _roadInstantiations.Count > 0 || _flagInstantiations.Count > 0;
    private TemporaryEditorActions()
    {
        Settings = new ActionSettings(this);
        Instance = this;
#if PRINT_ACTION_SIMPLE
        Logger.DevkitServer.LogDebug(Source, "Initialized.");
#endif
    }
    internal void QueueHierarchyItemInstantiation(IHierarchyItemTypeIdentifier type, Vector3 position, Quaternion rotation, Vector3 scale, ulong owner, NetId netId)
    {
        if (type == null) throw new ArgumentNullException(nameof(type));

        _hierarchyInstantiations.Add(new PendingHierarchyInstantiation(type, position, rotation, scale, owner, netId));
#if PRINT_ACTION_SIMPLE
        Logger.DevkitServer.LogDebug(Source, $"Queued hierarchy item instantiation for {type.Format()} when the level loads.");
#endif
    }
    internal void QueueLevelObjectInstantiation(Asset asset, Vector3 position, Quaternion rotation, Vector3 scale, ulong owner, NetId netId)
    {
        if (asset is not ObjectAsset && asset is not ItemBarricadeAsset and not ItemStructureAsset)
            throw new ArgumentException("Must be either ObjectAsset (LevelObject) or ItemAsset (LevelBuildableObject).", nameof(asset));

        _lvlObjectInstantiations.Add(new PendingLevelObjectInstantiation(asset.getReferenceTo<Asset>(), position, rotation, scale, owner, netId));
#if PRINT_ACTION_SIMPLE
        Logger.DevkitServer.LogDebug(Source, $"Queued level object instantiation for {asset.Format()} when the level loads.");
#endif
    }
    internal void QueueRoadInstantiation(long netIds, ushort flags, Vector3 position, Vector3 tangent1, Vector3 tangent2, float offset, ulong owner)
    {
        _roadInstantiations.Add(new PendingRoadInstantiation(netIds, flags, position, tangent1, tangent2, offset, owner));
#if PRINT_ACTION_SIMPLE
        Logger.DevkitServer.LogDebug(Source, $"Queued road instantiation at {position.Format()} when the level loads.");
#endif
    }
    internal void QueueRoadVertexInstantiation(NetId roadNetId, Vector3 position, Vector3 tangent1, Vector3 tangent2, bool ignoreTerrain, float verticalOffset, int vertexIndex, ulong owner, NetId vertexNetId, ERoadMode mode)
    {
        _roadVertexInstantiations.Add(new PendingRoadVertexInstantiation(roadNetId, position, tangent1, tangent2, ignoreTerrain, verticalOffset, vertexIndex, owner, vertexNetId, mode));
#if PRINT_ACTION_SIMPLE
        Logger.DevkitServer.LogDebug(Source, $"Queued road vertex instantiation at {position.Format()} (from road {roadNetId.Format()} at #{vertexIndex}) when the level loads.");
#endif
    }
    internal void QueueFlagInstantiation(NetId netId, Vector3 position, Vector2 size, ulong owner, bool infiniteAgroDistance, bool shouldSpawnZombies, byte maxZombies, int maxBossZombies, Guid difficultyAsset)
    {
        _flagInstantiations.Add(new PendingFlagInstantiation(netId, position, size, owner, infiniteAgroDistance, shouldSpawnZombies, maxZombies, maxBossZombies, difficultyAsset));
#if PRINT_ACTION_SIMPLE
        Logger.DevkitServer.LogDebug(Source, $"Queued flag instantiation at {position.Format()} when the level loads.");
#endif
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

        int collIndex = -1;
#if PRINT_ACTION_SIMPLE
        int stInd = _actions.Count;
#endif
        for (int i = 0; i < ct; ++i)
        {
            if (c.Count > collIndex + 1 && c[collIndex + 1].StartIndex >= i)
                LoadCollection(collIndex + 1);
            DevkitServerActionType type = reader.ReadEnum<DevkitServerActionType>();
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
#if PRINT_ACTION_SIMPLE
        Logger.DevkitServer.LogDebug(Source, $"Received actions: {(_actions.Count - stInd).Format()}.");
#endif

        ListPool<ActionSettingsCollection>.release(c);

        void LoadCollection(int index)
        {
            if (c.Count <= index)
                return;
            collIndex = index;
            Settings.SetSettings(c[collIndex]);
#if PRINT_ACTION_SIMPLE
            Logger.DevkitServer.LogDebug(Source, $"Loading option collection: {c[collIndex].Format()}.");
#endif
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

        if (_lvlObjectInstantiations.Count > 20)
            yield return null;
        for (int i = 0; i < _roadInstantiations.Count; i++)
        {
            PendingRoadInstantiation roadInstantiation = _roadInstantiations[i];
            RoadUtil.ReceiveInstantiation(MessageContext.Nil, roadInstantiation.Position, roadInstantiation.Flags,
                roadInstantiation.Tangent1, roadInstantiation.Tangent2, roadInstantiation.Offset,
                roadInstantiation.NetIds, roadInstantiation.Owner);
        }
        for (int i = 0; i < _roadVertexInstantiations.Count; i++)
        {
            PendingRoadVertexInstantiation vertexInstantiation = _roadVertexInstantiations[i];
            RoadUtil.ReceiveVertexInstantiation(MessageContext.Nil, vertexInstantiation.RoadNetId, vertexInstantiation.Position,
                vertexInstantiation.Tangent1, vertexInstantiation.Tangent2, vertexInstantiation.VertexIndex, vertexInstantiation.Mode,
                vertexInstantiation.VerticalOffset, vertexInstantiation.IgnoreTerrain, vertexInstantiation.VertexNetId, vertexInstantiation.Owner);
        }
        EditorActions.HasProcessedPendingRoads = true;
        if (_roadInstantiations.Count + _roadVertexInstantiations.Count > 50)
            yield return null;
        for (int i = 0; i < _flagInstantiations.Count; i++)
        {
            PendingFlagInstantiation flagInstantiation = _flagInstantiations[i];
            NavigationUtil.ReceiveInstantiation(MessageContext.Nil, flagInstantiation.Position, flagInstantiation.Size, flagInstantiation.NetId, flagInstantiation.Owner,
                flagInstantiation.InfiniteAgroDistance, flagInstantiation.ShouldSpawnZombies, flagInstantiation.MaxZombies,
                flagInstantiation.MaxBossZombies, flagInstantiation.DifficultyAsset);
        }
        EditorActions.HasProcessedPendingFlags = true;
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
        EditorActions.HasProcessedPendingRoads = false;
        EditorActions.HasProcessedPendingFlags = false;
        EventOnStartListening.TryInvoke();
    }
    public void Dispose()
    {
        ((IDisposable)Settings).Dispose();
        _actions.Clear();
        Instance = null;
#if PRINT_ACTION_SIMPLE
        Logger.DevkitServer.LogDebug(Source, "Cleaned up.");
#endif
    }

    private class PendingHierarchyInstantiation(IHierarchyItemTypeIdentifier type, Vector3 position, Quaternion rotation, Vector3 scale, ulong owner, NetId netId)
    {
        public readonly IHierarchyItemTypeIdentifier Type = type;
        public readonly NetId NetId = netId;
        public readonly Vector3 Position = position;
        public readonly Quaternion Rotation = rotation;
        public readonly Vector3 Scale = scale;
        public readonly ulong Owner = owner;
    }
    private class PendingLevelObjectInstantiation(AssetReference<Asset> assetAsset, Vector3 position, Quaternion rotation, Vector3 scale, ulong owner, NetId netId)
    {
        public readonly AssetReference<Asset> Asset = assetAsset;
        public readonly NetId NetId = netId;
        public readonly Vector3 Position = position;
        public readonly Quaternion Rotation = rotation;
        public readonly Vector3 Scale = scale;
        public readonly ulong Owner = owner;
    }
    private class PendingRoadInstantiation(long netIds, ushort flags, Vector3 position, Vector3 tangent1, Vector3 tangent2, float offset, ulong owner)
    {
        public readonly long NetIds = netIds;
        public readonly ushort Flags = flags;
        public readonly Vector3 Position = position;
        public readonly Vector3 Tangent1 = tangent1;
        public readonly Vector3 Tangent2 = tangent2;
        public readonly float Offset = offset;
        public readonly ulong Owner = owner;
    }
    private class PendingRoadVertexInstantiation(NetId roadNetId, Vector3 position, Vector3 tangent1, Vector3 tangent2, bool ignoreTerrain,
        float verticalOffset, int vertexIndex, ulong owner, NetId vertexNetId, ERoadMode mode)
    {
        public readonly NetId RoadNetId = roadNetId;
        public readonly Vector3 Position = position;
        public readonly Vector3 Tangent1 = tangent1;
        public readonly Vector3 Tangent2 = tangent2;
        public readonly bool IgnoreTerrain = ignoreTerrain;
        public readonly float VerticalOffset = verticalOffset;
        public readonly int VertexIndex = vertexIndex;
        public readonly ulong Owner = owner;
        public readonly NetId VertexNetId = vertexNetId;
        public readonly ERoadMode Mode = mode;
    }
    private class PendingFlagInstantiation(NetId netId, Vector3 position, Vector2 size, ulong owner, bool infiniteAgroDistance, bool shouldSpawnZombies, byte maxZombies, int maxBossZombies, Guid difficultyAsset)
    {
        public readonly NetId NetId = netId;
        public readonly Vector3 Position = position;
        public readonly Vector2 Size = size;
        public readonly ulong Owner = owner;
        public readonly bool InfiniteAgroDistance = infiniteAgroDistance;
        public readonly bool ShouldSpawnZombies = shouldSpawnZombies;
        public readonly byte MaxZombies = maxZombies;
        public readonly int MaxBossZombies = maxBossZombies;
        public readonly Guid DifficultyAsset = difficultyAsset;
    }
}
#endif