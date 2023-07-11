using DevkitServer.Models;
using DevkitServer.Multiplayer.Levels;
using DevkitServer.Multiplayer.Networking;
using DevkitServer.Players;
using DevkitServer.Util.Encoding;

namespace DevkitServer.Multiplayer.Sync;
public class ObjectSync : AuthoritativeSync<ObjectSync>
{
    private const string Source = "OBJECT SYNC";
    private const float Delay = 0.5f;
    private const float SendDelay = 3f;
    private static readonly NetCallRaw<ulong, ObjectInfo> SendObjectSyncData = new NetCallRaw<ulong, ObjectInfo>(NetCalls.SendObjectSyncData, null, reader => new ObjectInfo(reader), null, (writer, obj) => obj.Write(writer));

    private readonly List<NetId> _syncQueue = new List<NetId>();
    private float _lastSent;
    protected override void Init()
    {
#if CLIENT
        DevkitServerGLUtility.OnRenderAny += HandleGLRender;
#endif
    }
    protected override void Deinit()
    {
#if CLIENT
        DevkitServerGLUtility.OnRenderAny -= HandleGLRender;
#endif
    }
    [UsedImplicitly]
    private void Update()
    {
        if (!HasAuthority || _syncQueue.Count == 0)
            return;
        float time = CachedTime.RealtimeSinceStartup;
        if (time - _lastSent < Delay)
            return;
        _lastSent = time;
        NetId netId = _syncQueue[_syncQueue.Count - 1];
        _syncQueue.RemoveAt(_syncQueue.Count - 1);
        ObjectInfo obj;
        if (!LevelObjectNetIdDatabase.TryGetObjectOrBuildable(netId, out LevelObject? levelObject, out LevelBuildableObject? buildable, out _))
        {
            obj = new ObjectInfo(netId, false, default, default, Vector3.one, default, -1);
        }
        else if (levelObject != null)
        {
            Transform? transform = levelObject.GetTransform();
            if (transform == null)
                return;
            obj = new ObjectInfo(netId, true, transform.position, transform.rotation, transform.localScale, levelObject.GetCustomMaterialOverride().GUID, levelObject.GetMaterialIndexOverride());
        }
        else
        {
            Transform? transform = buildable!.transform;
            if (transform == null)
                return;
            obj = new ObjectInfo(netId, true, transform.position, transform.rotation, transform.localScale, default, -1);
        }
        Logger.LogDebug($"[{Source}] Syncing {netId.Format()}.");
#if SERVER
        SendObjectSyncData.Invoke(Provider.GatherClientConnections(), 0ul, obj);
#elif CLIENT
        SendObjectSyncData.Invoke(0ul, obj);
#endif
    }

    [NetCall(NetCallSource.FromEither, NetCalls.SendObjectSyncData)]
    private static void ReceiveObjectSyncData(MessageContext ctx, ulong relaySource, ObjectInfo obj)
    {
        ObjectSync? sync;
        if (relaySource == 0ul)
            sync = ServersideAuthority;
        else
        {
            EditorUser? user = UserManager.FromId(relaySource);
            sync = user == null ? null : user.ObjectSync;
        }

        if (sync == null)
        {
            Logger.LogError($"Unable to find object sync source for relay ID: {relaySource.Format()}.", method: Source);
            return;
        }
        if (!sync.HasAuthority)
        {
            Logger.LogError($"Found relay source, but it didn't have authority: {relaySource.Format()}, {sync.Format()}.", method: Source);
            return;
        }

        ReceiveObject(in obj);
    }
    
#if CLIENT
    private void HandleGLRender()
    {
        if (!HasAuthority || _syncQueue.Count == 0)
            return;
        if (!LevelObjectNetIdDatabase.TryGetObjectOrBuildable(_syncQueue[_syncQueue.Count - 1], out LevelObject? levelObject, out LevelBuildableObject? buildable, out _))
            return;
        Transform? transform = levelObject == null ? buildable!.transform : levelObject.GetTransform();
        if (transform == null)
            return;
        GL.Begin(GL.QUADS);
        GL.Color(AuthColor with { a = 0.25f });
        DevkitServerGLUtility.BoxSolidIdentityMatrix(transform.position, new Vector3(0.3f, 0.3f, 0.3f), false, true);
        GL.End();
    }
#endif
    private static void ReceiveObject(in ObjectInfo obj)
    {
        Logger.LogDebug($"[{Source}] Received object sync data for NetID: {obj.NetId.Format()}.");

        if (!LevelObjectNetIdDatabase.TryGetObjectOrBuildable(obj.NetId, out LevelObject? levelObject, out LevelBuildableObject? buildable, out _))
        {
            if (!obj.IsAlive)
                return;
            Logger.LogWarning($"Expected object ({obj.NetId.Format()}) not found.");
        }
        else if (levelObject != null)
        {
            Transform? transform = levelObject.GetTransform();
            if (transform == null)
                return;

            if (!transform.position.IsNearlyEqual(obj.Position) ||
                !transform.rotation.IsNearlyEqual(obj.Rotation) ||
                !transform.localScale.IsNearlyEqual(obj.Scale))
            {
                Logger.LogDebug($"[{Source}] Transforming invalid object: {levelObject.asset.Format()}.");
                LevelObjects.registerTransformObject(transform, obj.Position, obj.Rotation, obj.Scale, transform.position, transform.rotation, transform.localScale);
            }

            bool reapply = false;
            if (levelObject.GetCustomMaterialOverride().GUID != obj.MaterialPaletteGuid)
            {
                LevelObjectUtil.SetCustomMaterialPaletteOverrideLocal(levelObject, new AssetReference<MaterialPaletteAsset>(obj.MaterialPaletteGuid), false);
                reapply = true;
            }
            if (levelObject.GetMaterialIndexOverride() != obj.MaterialIndex)
            {
                LevelObjectUtil.SetMaterialIndexOverrideLocal(levelObject, obj.MaterialIndex, false);
                reapply = true;
            }

            if (reapply)
                levelObject.ReapplyMaterialOverrides();
        }
        else
        {
            Transform? transform = buildable!.transform;
            
            if (transform != null && (!transform.position.IsNearlyEqual(obj.Position) ||
                                      !transform.rotation.IsNearlyEqual(obj.Rotation) ||
                                      !transform.localScale.IsNearlyEqual(obj.Scale)))
            {
                Logger.LogDebug($"[{Source}] Transforming invalid buildable: {buildable.asset.Format()}.");
                LevelObjects.registerTransformObject(transform, obj.Position, obj.Rotation, obj.Scale, transform.position, transform.rotation, transform.localScale);
            }
        }
    }
    public void EnqueueSync(LevelObject obj)
    {
        if (LevelObjectNetIdDatabase.TryGetObjectNetId(obj, out NetId netId))
            EnqueueSync(netId);
    }
    public void EnqueueSync(LevelBuildableObject buildable)
    {
        if (LevelObjectNetIdDatabase.TryGetBuildableNetId(buildable, out NetId netId))
            EnqueueSync(netId);
    }
    public void EnqueueSync(RegionIdentifier buildable)
    {
        if (LevelObjectNetIdDatabase.TryGetBuildableNetId(buildable, out NetId netId))
            EnqueueSync(netId);
    }
    public void EnqueueSync(NetId netId)
    {
        if (!HasAuthority)
            return;

        for (int i = _syncQueue.Count - 1; i >= 0; --i)
        {
            if (_syncQueue[i].id == netId.id)
            {
                _syncQueue.RemoveAt(i);
                break;
            }
        }
        _syncQueue.Insert(0, netId);
        _lastSent = CachedTime.RealtimeSinceStartup + Math.Max(-Delay, SendDelay - _syncQueue.Count * Delay);
        Logger.LogDebug($"[{Source}] Requested sync for: {netId.Format()}.");
    }

    private readonly struct ObjectInfo
    {
        public NetId NetId { get; }
        public bool IsAlive { get; }
        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public Vector3 Scale { get; }
        public Guid MaterialPaletteGuid { get; }
        public int MaterialIndex { get; }
        public ObjectInfo(NetId netId, bool isAlive, Vector3 position, Quaternion rotation, Vector3 scale, Guid materialPaletteGuid, int materialIndex)
        {
            NetId = netId;
            IsAlive = isAlive;
            Position = position;
            Rotation = rotation;
            Scale = scale;
            MaterialPaletteGuid = materialPaletteGuid;
            MaterialIndex = materialIndex;
        }
        public ObjectInfo(ByteReader reader)
        {
            byte flags = reader.ReadUInt8();
            NetId = reader.ReadNetId();
            IsAlive = (flags & 1) != 0;
            if (!IsAlive)
                return;
            Position = reader.ReadVector3();
            Rotation = reader.ReadQuaternion();
            Scale = (flags & 2) != 0 ? reader.ReadVector3() : Vector3.one;
            MaterialPaletteGuid = (flags & 4) != 0 ? reader.ReadGuid() : default;
            MaterialIndex = (flags & 8) != 0 ? reader.ReadInt32() : -1;
        }
        public void Write(ByteWriter writer)
        {
            byte flags = (byte)((IsAlive ? 1 : 0) | (Scale.IsNearlyEqual(Vector3.one) ? 0 : 2) | (MaterialPaletteGuid == Guid.Empty ? 0 : 4) | (MaterialIndex < 0 ? 0 : 8));
            writer.Write(flags);
            writer.Write(NetId);
            if (!IsAlive)
                return;
            writer.Write(Position);
            writer.Write(Rotation);
            if ((flags & 2) != 0)
                writer.Write(Scale);
            if ((flags & 4) != 0)
                writer.Write(MaterialPaletteGuid);
            if ((flags & 8) != 0)
                writer.Write(MaterialIndex);
        }
    }
}
