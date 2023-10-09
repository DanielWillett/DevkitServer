using DevkitServer.Models;
using DevkitServer.Multiplayer.Levels;
using DevkitServer.Multiplayer.Networking;
using DevkitServer.Players;
using DevkitServer.Util.Encoding;
using SDG.Framework.Devkit;

namespace DevkitServer.Multiplayer.Sync;
public class HierarchySync : AuthoritativeSync<HierarchySync>
{
    private const string Source = "HIERARCHY SYNC";
    private const float Delay = 0.5f;
    private const float SendDelay = 3f;
    private static readonly NetCallRaw<ulong, HierarchyItemInfo> SendHierarchySyncData = new NetCallRaw<ulong, HierarchyItemInfo>(DevkitServerNetCall.SendHierarchySyncData, null, reader => new HierarchyItemInfo(reader), null, (writer, obj) => obj.Write(writer));

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
        HierarchyItemInfo obj;
        if (!HierarchyItemNetIdDatabase.TryGetHierarchyItem(netId, out IDevkitHierarchyItem item))
        {
            obj = new HierarchyItemInfo(netId, false, default, default, Vector3.one);
        }
        else
        {
            if (item is Component component)
            {
                Transform? transform = component.transform;
                if (transform == null)
                    return;
                obj = new HierarchyItemInfo(netId, true, transform.position, transform.rotation, transform.localScale);
            }
            else
            {
                obj = new HierarchyItemInfo(netId, true, Vector3.zero, Quaternion.identity, Vector3.one);
            }
        }
        Logger.LogDebug($"[{Source}] Syncing {netId.Format()}.");
#if SERVER
        SendHierarchySyncData.Invoke(Provider.GatherClientConnections(), 0ul, obj);
#elif CLIENT
        SendHierarchySyncData.Invoke(0ul, obj);
#endif
    }

    [NetCall(NetCallSource.FromEither, DevkitServerNetCall.SendHierarchySyncData)]
    private static void ReceiveHierarchySyncData(MessageContext ctx, ulong relaySource, HierarchyItemInfo obj)
    {
        HierarchySync? sync;
        if (relaySource == 0ul)
            sync = ServersideAuthority;
        else
        {
            EditorUser? user = UserManager.FromId(relaySource);
            sync = user == null ? null : user.HierarchySync;
        }

        if (sync == null)
        {
            Logger.LogError($"Unable to find hierarchy item sync source for relay ID: {relaySource.Format()}.", method: Source);
            return;
        }
        if (!sync.HasAuthority)
        {
            Logger.LogError($"Found hierarchy item sync relay source, but it didn't have authority: {relaySource.Format()}, {sync.Format()}.", method: Source);
            return;
        }

        ReceiveHierarchyItem(in obj);
    }
    
#if CLIENT
    private void HandleGLRender()
    {
        if (!HasAuthority || _syncQueue.Count == 0)
            return;
        if (!HierarchyItemNetIdDatabase.TryGetHierarchyItem(_syncQueue[_syncQueue.Count - 1], out IDevkitHierarchyItem item) || item is not Component comp)
            return;
        Transform? transform = comp.transform;
        if (transform == null)
            return;
        GL.Begin(GL.QUADS);
        GL.Color(AuthColor with { a = 0.25f });
        DevkitServerGLUtility.BoxSolidIdentityMatrix(transform.position, new Vector3(0.3f, 0.3f, 0.3f), false, true);
        GL.End();
    }
#endif
    private static void ReceiveHierarchyItem(in HierarchyItemInfo obj)
    {
        Logger.LogDebug($"[{Source}] Received hierarchy item sync data for NetID: {obj.NetId.Format()}.");

        if (!HierarchyItemNetIdDatabase.TryGetHierarchyItem(obj.NetId, out IDevkitHierarchyItem item))
        {
            if (!obj.IsAlive)
                return;
            Logger.LogWarning($"Expected hierarchy item ({obj.NetId.Format()}) not found.");
        }
        else if (!obj.IsAlive)
        {
            Logger.LogDebug($"[{Source}] Deleting invalid hierarchy item: {item.Format()} ({item.instanceID.Format()}).");
            HierarchyUtil.LocalRemoveItem(item);
        }
        else if (item is Component comp)
        {
            Transform? transform = comp.transform;

            if (transform != null && (!transform.position.IsNearlyEqual(obj.Position) ||
                                      !transform.rotation.IsNearlyEqual(obj.Rotation) ||
                                      !transform.localScale.IsNearlyEqual(obj.Scale)))
            {
                Logger.LogDebug($"[{Source}] Transforming invalid hierarchy item: {item.Format()} ({item.instanceID.Format()}).");
                HierarchyUtil.LocalTranslate(item, new FinalTransformation(obj.NetId,
                    new TransformationDelta(TransformationDelta.TransformFlags.All, obj.Position, obj.Rotation, transform.position, transform.rotation),
                    obj.Scale, transform.localScale), true);
            }
        }
    }
    public void EnqueueSync(IDevkitHierarchyItem item)
    {
        if (HierarchyItemNetIdDatabase.TryGetHierarchyItemNetId(item, out NetId netId))
            EnqueueSync(netId);
    }
    public void EnqueueSync(uint instanceId)
    {
        if (HierarchyItemNetIdDatabase.TryGetHierarchyItemNetId(instanceId, out NetId netId))
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

    private readonly struct HierarchyItemInfo
    {
        public NetId NetId { get; }
        public bool IsAlive { get; }
        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public Vector3 Scale { get; }
        public HierarchyItemInfo(NetId netId, bool isAlive, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            NetId = netId;
            IsAlive = isAlive;
            Position = position;
            Rotation = rotation;
            Scale = scale;
        }
        public HierarchyItemInfo(ByteReader reader)
        {
            byte flags = reader.ReadUInt8();
            NetId = reader.ReadNetId();
            IsAlive = (flags & 1) != 0;
            if (!IsAlive)
                return;
            Position = reader.ReadVector3();
            Rotation = reader.ReadQuaternion();
            Scale = (flags & 2) != 0 ? reader.ReadVector3() : Vector3.one;
        }
        public void Write(ByteWriter writer)
        {
            byte flags = (byte)((IsAlive ? 1 : 0) | (Scale.IsNearlyEqual(Vector3.one) ? 0 : 2));
            writer.Write(flags);
            writer.Write(NetId);
            if (!IsAlive)
                return;
            writer.Write(Position);
            writer.Write(Rotation);
            if ((flags & 2) != 0)
                writer.Write(Scale);
        }
    }
}
