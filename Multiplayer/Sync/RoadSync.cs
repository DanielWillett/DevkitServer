using DanielWillett.SpeedBytes;
using DanielWillett.SpeedBytes.Unity;
using DevkitServer.API;
using DevkitServer.Configuration;
using DevkitServer.Models;
using DevkitServer.Multiplayer.Levels;
using DevkitServer.Multiplayer.Networking;
using DevkitServer.Players;
using DevkitServer.Util.Encoding;

namespace DevkitServer.Multiplayer.Sync;
public class RoadSync : AuthoritativeSync<RoadSync>
{
    private readonly List<NetId> _syncQueue = new List<NetId>(2);
    private readonly List<byte> _dirtyMaterials = new List<byte>(8);
    private const float Delay = 0.5f;
    private const float SendDelay = 3f;
    private static readonly NetCallRaw<ulong, RoadSyncData> SendRoadSyncData = new NetCallRaw<ulong, RoadSyncData>(DevkitServerNetCall.SendRoadSyncData, null, reader => new RoadSyncData(reader), null, (writer, obj) => obj.Write(writer));
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
        NetId netId = _syncQueue[^1];
        _syncQueue.RemoveAt(_syncQueue.Count - 1);
        RoadSyncData obj;
        if (!RoadNetIdDatabase.TryGetRoad(netId, out Road road, out int roadIndex))
        {
            obj = new RoadSyncData(netId, _dirtyMaterials.Count > 0 ? _dirtyMaterials[^1] : null);
            if (_dirtyMaterials.Count > 0)
                _dirtyMaterials.RemoveAt(_dirtyMaterials.Count - 1);
        }
        else
        {
            obj = new RoadSyncData(netId, road, roadIndex, _dirtyMaterials.Contains(road.material));
            _dirtyMaterials.Remove(road.material);
        }
        Logger.DevkitServer.LogDebug(Source, $"Syncing {netId.Format()}.");
#if SERVER
        SendRoadSyncData.Invoke(Provider.GatherClientConnections(), 0ul, obj);
#elif CLIENT
        SendRoadSyncData.Invoke(0ul, obj);
#endif
    }

    [NetCall(NetCallSource.FromEither, DevkitServerNetCall.SendRoadSyncData)]
    private static void ReceiveRoadSyncData(MessageContext ctx, ulong relaySource, RoadSyncData obj)
    {
        RoadSync? sync;
        if (relaySource == 0ul)
            sync = ServersideAuthority;
        else
        {
            EditorUser? user = UserManager.FromId(relaySource);
            sync = user == null ? null : user.RoadSync;
        }

        if (sync == null)
        {
            Logger.DevkitServer.LogError(Source, $"Unable to find road sync source for relay ID: {relaySource.Format()}.");
            return;
        }
        if (!sync.HasAuthority)
        {
            Logger.DevkitServer.LogError(Source, $"Found road sync relay source, but it didn't have authority: {relaySource.Format()}, {sync.Format()}.");
            return;
        }

        ReceiveRoadSyncData(in obj);
    }

#if CLIENT
    private void HandleGLRender()
    {
        if (!HasAuthority || _syncQueue.Count == 0)
            return;
        if (!RoadNetIdDatabase.TryGetRoad(_syncQueue[^1], out Road road))
            return;
        if (road.joints.Count <= 0)
            return;

        GL.Begin(GL.QUADS);
        GL.Color(AuthColor with { a = 0.25f });

        for (int i = 0; i < road.joints.Count; ++i)
            DevkitServerGLUtility.BoxSolidIdentityMatrix(road.joints[i].vertex, new Vector3(0.3f, 0.3f, 0.3f), false, true);

        GL.End();
    }
#endif
    private static void ReceiveRoadSyncData(in RoadSyncData obj)
    {
        Logger.DevkitServer.LogDebug(Source, $"Received road sync data for NetID: {obj.NetId.Format()}.");

        if (!RoadNetIdDatabase.TryGetRoad(obj.NetId, out Road road, out int roadIndex))
        {
            if (!obj.IsAlive)
            {
                obj.SyncMaterial(false);
                return;
            }
            Logger.DevkitServer.LogWarning(Source, $"Expected road ({obj.NetId.Format()}) not found.");
            obj.CreateRoad();
            obj.SyncMaterial(false);
        }
        else if (!obj.IsAlive)
        {
            Logger.DevkitServer.LogDebug(Source, $"Deleting road that should've already been deleted: {roadIndex.Format()}.");
            RoadUtil.RemoveRoadLocal(roadIndex);
            obj.SyncMaterial(false);
        }
        else
        {
            RoadUtil.HoldBake = true;
            bool needsBake = false;
            try
            {
                needsBake = obj.SyncRoad(road, roadIndex);
                needsBake |= obj.SyncMaterial(true);
            }
            finally
            {
                RoadUtil.HoldBake = false;
                if (needsBake && !DevkitServerConfig.RemoveCosmeticImprovements)
                    road.buildMesh();
            }
        }
    }

    public void EnqueueSync(Road road) => EnqueueSync(road.GetRoadIndex());
    public void EnqueueSync(int roadIndex)
    {
        if (RoadNetIdDatabase.TryGetRoadNetId(roadIndex, out NetId netId))
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
                if (i != 0)
                    _syncQueue.RemoveAt(i);
                break;
            }
        }
        if (_syncQueue.Count == 0 || _syncQueue[0].id != netId.id)
            _syncQueue.Insert(0, netId);
        _lastSent = CachedTime.RealtimeSinceStartup + Math.Max(-Delay, SendDelay - _syncQueue.Count * Delay);
        Logger.DevkitServer.LogDebug(Source, $"Requested sync for: {netId.Format()}.");
    }
    public void EnqueueMaterialSync(byte material)
    {
        if (!HasAuthority)
            return;

        for (int i = _dirtyMaterials.Count - 1; i >= 0; --i)
        {
            if (_dirtyMaterials[i] == material)
            {
                if (i != 0)
                    _syncQueue.RemoveAt(i);
                break;
            }
        }

        if (_dirtyMaterials.Count == 0 || _dirtyMaterials[0] != material)
            _dirtyMaterials.Insert(0, material);
        Logger.DevkitServer.LogDebug(Source, $"Marked material {RoadUtil.MaterialToString(material).Format()} dirty.");
    }
    private sealed class RoadSyncData
    {
        public sealed class Vertex
        {
            public readonly NetId NetId;
            public readonly Vector3 Position;
            public readonly Vector3 Tangent1;
            public readonly Vector3 Tangent2;
            public readonly ERoadMode Mode;
            public readonly bool IgnoreTerrain;
            public readonly float VerticalOffset;
            public Vertex(NetId netId, Vector3 position, Vector3 tangent1, Vector3 tangent2, ERoadMode mode, bool ignoreTerrain, float verticalOffset)
            {
                NetId = netId;
                Position = position;
                Tangent1 = tangent1;
                Tangent2 = tangent2;
                Mode = mode;
                IgnoreTerrain = ignoreTerrain;
                VerticalOffset = verticalOffset;
            }
            public Vertex(ByteReader reader)
            {
                NetId = reader.ReadNetId();
                byte flag = reader.ReadUInt8();
                Mode = (ERoadMode)(flag & 0b00111111);
                IgnoreTerrain = (flag & (1 << 6)) != 0;
                Tangent1 = reader.ReadVector3();
                Tangent2 = Mode switch
                {
                    ERoadMode.ALIGNED => -Tangent1.normalized * reader.ReadFloat(),
                    ERoadMode.MIRROR => -Tangent1,
                    _ => reader.ReadVector3()
                };
                Position = reader.ReadVector3();
                VerticalOffset = (flag & (1 << 7)) != 0 ? reader.ReadFloat() : 0f;
            }
            public void Write(ByteWriter writer)
            {
                writer.Write(NetId);
                byte flag = (byte)(((int)Mode & 0b00111111) | (IgnoreTerrain ? 1 << 6 : 0) | (VerticalOffset > -0.001 && VerticalOffset < 0.001 ? 1 << 7 : 0));
                writer.Write(flag);
                writer.Write(Tangent1);
                if (Mode == ERoadMode.ALIGNED)
                    writer.Write(Tangent2.magnitude);
                else if (Mode != ERoadMode.MIRROR)
                    writer.Write(Tangent2);
                writer.Write(Position);
                if ((flag & (1 << 7)) != 0)
                    writer.Write(VerticalOffset);
            }
        }

        public readonly NetId NetId;
        public readonly bool IsAlive;
        private readonly Vertex[] _vertices;
        private readonly byte _materialIndex;
        private readonly bool _isLoop;
        private readonly bool _hasMaterialData;
        private readonly Vector4 _materialDimensions;
        private readonly bool _materialIsConcrete;
        public RoadSyncData(NetId netId, byte? materialIndex)
        {
            NetId = netId;
            IsAlive = false;
            _vertices = Array.Empty<Vertex>();

            if (materialIndex.HasValue)
            {
                RoadMaterial mat = LevelRoads.materials[materialIndex.Value];
                _hasMaterialData = true;
                _materialDimensions = new Vector4(mat.width, mat.height, mat.depth, mat.offset);
                _materialIsConcrete = mat.isConcrete;
                _materialIndex = materialIndex.Value;
            }
        }
        public RoadSyncData(NetId netId, Road road, int roadIndex, bool materialData)
        {
            NetId = netId;
            IsAlive = true;
            _vertices = new Vertex[road.joints.Count];
            _materialIndex = road.material;
            _isLoop = road.isLoop;

            if (materialData && LevelRoads.materials.Length > road.material)
            {
                RoadMaterial mat = LevelRoads.materials[road.material];
                _hasMaterialData = true;
                _materialDimensions = new Vector4(mat.width, mat.height, mat.depth, mat.offset);
                _materialIsConcrete = mat.isConcrete;
            }
            for (int i = 0; i < road.joints.Count; ++i)
            {
                RoadJoint joint = road.joints[i];
                RoadNetIdDatabase.TryGetVertexNetId(new RoadVertexIdentifier(roadIndex, i), out NetId vNetId);
                _vertices[i] = new Vertex(vNetId, joint.vertex, joint.getTangent(0), joint.getTangent(1), joint.mode, joint.ignoreTerrain, joint.offset);
            }
        }
        public RoadSyncData(ByteReader reader)
        {
            NetId = reader.ReadNetId();
            byte flags = reader.ReadUInt8();
            IsAlive = (flags & 1) != 0;
            _hasMaterialData = (flags & 4) != 0;
            if (_hasMaterialData)
            {
                _materialDimensions = reader.ReadVector4();
                _materialIsConcrete = (flags & 8) != 0;
            }

            if (!IsAlive)
            {
                _vertices = Array.Empty<Vertex>();
                if (_hasMaterialData)
                    _materialIndex = reader.ReadUInt8();
                return;
            }

            _isLoop = (flags & 2) != 0;
            _materialIndex = reader.ReadUInt8();
            int len = reader.ReadUInt16();
            _vertices = new Vertex[len];
            for (int i = 0; i < len; ++i)
                _vertices[i] = new Vertex(reader);
        }
        public void Write(ByteWriter writer)
        {
            writer.Write(NetId);
            byte flags = (byte)((IsAlive ? 1 : 0) | (_isLoop ? 2 : 0) | (_hasMaterialData ? 4 : 0) | (_materialIsConcrete ? 8 : 0));
            writer.Write(flags);
            if (_hasMaterialData)
            {
                writer.Write(_materialDimensions);
                writer.Write(_materialIsConcrete);
            }

            if (!IsAlive)
            {
                if (_hasMaterialData)
                    writer.Write(_materialIndex);
                return;
            }

            writer.Write(_materialIndex);
            writer.Write((ushort)_vertices.Length);
            for (int i = 0; i < _vertices.Length; ++i)
                _vertices[i].Write(writer);
        }
        public void CreateRoad()
        {
            RoadUtil.HoldBake = true;
            Road? road = null;
            bool needsBake = true;
            try
            {
                Vertex vertex = _vertices[0];
                Transform vertexTransform = RoadUtil.AddRoadLocal(vertex.Position, _materialIndex);

                if (vertexTransform == null)
                    return;
#if CLIENT
                RoadUtil.InitializeRoad(vertexTransform, out road, NetId, vertex.NetId);

                if (road == null)
                    return;

                int roadIndex = road.GetRoadIndex();

                for (int i = 1; i < _vertices.Length; ++i)
                    RoadNetIdDatabase.RegisterVertex(roadIndex, i, _vertices[i].NetId);
#else
                road = LevelRoads.getRoad(vertexTransform, out _, out _);
                if (road == null)
                    return;

                int roadIndex = road.GetRoadIndex();

                RoadNetIdDatabase.RegisterRoad(roadIndex, NetId);
                RoadNetIdDatabase.RegisterVertex(roadIndex, 0, vertex.NetId);
#endif

                if (road.isLoop != _isLoop)
                {
                    RoadUtil.SetIsLoopLocal(road, roadIndex, _isLoop);
                    needsBake = true;
                }

                RoadJoint joint = road.joints[0];
                if (!joint.getTangent(0).IsNearlyEqual(vertex.Tangent1, 0.01f) || !joint.getTangent(1).IsNearlyEqual(vertex.Tangent2, 0.01f))
                {
                    ERoadMode old = joint.mode;
                    joint.mode = ERoadMode.FREE;
                    RoadUtil.SetTangentHandlePositionLocal(road, roadIndex, 0, TangentHandle.Negative, vertex.Tangent1);
                    RoadUtil.SetTangentHandlePositionLocal(road, roadIndex, 0, TangentHandle.Positive, vertex.Tangent2);
                    joint.mode = old;
                    needsBake = true;
                }

                if (joint.mode != vertex.Mode)
                {
                    RoadUtil.SetVertexTangentHandleModeLocal(road, roadIndex, joint, 0, vertex.Mode);
                    needsBake = true;
                }

                if (joint.ignoreTerrain != vertex.IgnoreTerrain)
                {
                    RoadUtil.SetVertexIgnoreTerrainLocal(road, roadIndex, joint, 0, vertex.IgnoreTerrain);
                    needsBake = true;
                }

                if (joint.offset != vertex.VerticalOffset)
                {
                    RoadUtil.SetVertexVerticalOffsetLocal(road, roadIndex, joint, 0, vertex.VerticalOffset);
                    needsBake = true;
                }
                
                SyncRoad(road, roadIndex);
            }
            finally
            {
                RoadUtil.HoldBake = false;
                if (needsBake && road != null && !DevkitServerConfig.RemoveCosmeticImprovements)
                    road.buildMesh();
            }
        }
        public bool SyncMaterial(bool holdIsAlreadyDone)
        {
            if (!_hasMaterialData || _materialIndex >= LevelRoads.materials.Length)
                return false;

            RoadMaterial mat = LevelRoads.materials[_materialIndex];
            bool anyUpdates = false;
            if (!holdIsAlreadyDone)
                RoadUtil.HoldBake = true;
            try
            {
                if (Mathf.Abs(mat.width - _materialDimensions.x) > 0.001f)
                {
                    Logger.DevkitServer.LogInfo(Source, $"[ROAD MAT {_materialIndex.Format()}] " +
                                                        $"Existing width ({mat.width.Format("F2")}) does not match synced width ({_materialDimensions.x.Format("F2")}).");
                    RoadUtil.SetMaterialWidthLocal(_materialIndex, _materialDimensions.x);
                    anyUpdates = true;
                }
                if (Mathf.Abs(mat.height - _materialDimensions.y) > 0.001f)
                {
                    Logger.DevkitServer.LogInfo(Source, $"[ROAD MAT {_materialIndex.Format()}] " +
                                                        $"Existing height ({mat.height.Format("F2")}) does not match synced height ({_materialDimensions.y.Format("F2")}).");
                    RoadUtil.SetMaterialHeightLocal(_materialIndex, _materialDimensions.y);
                    anyUpdates = true;
                }
                if (Mathf.Abs(mat.depth - _materialDimensions.z) > 0.001f)
                {
                    Logger.DevkitServer.LogInfo(Source, $"[ROAD MAT {_materialIndex.Format()}] " +
                                                        $"Existing depth ({mat.depth.Format("F2")}) does not match synced depth ({_materialDimensions.z.Format("F2")}).");
                    RoadUtil.SetMaterialDepthLocal(_materialIndex, _materialDimensions.z);
                    anyUpdates = true;
                }
                if (Mathf.Abs(mat.offset - _materialDimensions.w) > 0.001f)
                {
                    Logger.DevkitServer.LogInfo(Source, $"[ROAD MAT {_materialIndex.Format()}] " +
                                                        $"Existing vertical offset ({mat.offset.Format("F2")}) does not match synced vertical offset ({_materialDimensions.w.Format("F2")}).");
                    RoadUtil.SetMaterialVerticalOffsetLocal(_materialIndex, _materialDimensions.w);
                    anyUpdates = true;
                }
                if (mat.isConcrete != _materialIsConcrete)
                {
                    Logger.DevkitServer.LogInfo(Source, $"[ROAD MAT {_materialIndex.Format()}] " +
                                                        $"Existing isConcrete ({mat.isConcrete.Format()}) does not match synced isConcrete ({_materialIsConcrete.Format()}).");
                    RoadUtil.SetMaterialIsConcreteLocal(_materialIndex, _materialIsConcrete);
                    anyUpdates = true;
                }
            }
            finally
            {
                if (!holdIsAlreadyDone)
                {
                    RoadUtil.HoldBake = false;
                    if (anyUpdates && !DevkitServerConfig.RemoveCosmeticImprovements)
                        RoadUtil.BakeRoadsWithMaterial(_materialIndex);
                }
            }
            return anyUpdates;
        }
        public bool SyncRoad(Road road, int roadIndex)
        {
            bool anyUpdates = false;
            if (road.isLoop != _isLoop)
            {
                Logger.DevkitServer.LogInfo(Source, $"[ROAD {roadIndex.Format()}] " +
                                                    $"Existing isLoop ({road.isLoop.Format()}) does not match synced isLoop ({_isLoop.Format()}).");
                RoadUtil.SetIsLoopLocal(roadIndex, _isLoop);
                anyUpdates = true;
            }
            if (road.material != _materialIndex)
            {
                Logger.DevkitServer.LogInfo(Source, $"[ROAD {roadIndex.Format()}] " +
                                                    $"Existing material ({RoadUtil.MaterialToString(road.material).Format()}) does not match synced material ({RoadUtil.MaterialToString(_materialIndex).Format()}).");
                RoadUtil.SetMaterialLocal(roadIndex, _materialIndex);
                anyUpdates = true;
            }

            for (int j = road.joints.Count - 1; j >= 0; --j)
            {
                if (!RoadNetIdDatabase.TryGetVertexNetId(new RoadVertexIdentifier(roadIndex, j), out NetId netId))
                {
                    Logger.DevkitServer.LogDebug(Source, $"[ROAD {roadIndex.Format()}] [VERTEX {j.Format()}] " +
                                                         $"Existing vertex does not have a NetId.");
                    anyUpdates = true;
                    continue;
                }

                bool exists = false;
                for (int i = 0; i < _vertices.Length; ++i)
                {
                    if (_vertices[i].NetId.id == netId.id)
                    {
                        exists = true;
                        break;
                    }
                }
                if (!exists)
                {
                    RoadUtil.RemoveVertexLocal(roadIndex, j);
                    Logger.DevkitServer.LogInfo(Source, $"[ROAD {roadIndex.Format()}] [VERTEX {j.Format()}] " +
                                                        $"Existing vertex is not supposed to exist.");
                    anyUpdates = true;
                }
            }

            for (int i = 0; i < _vertices.Length; ++i)
            {
                int index = -1;
                NetId vNetId = _vertices[i].NetId;
                for (int j = 0; j < road.joints.Count; ++j)
                {
                    if (!RoadNetIdDatabase.TryGetVertexNetId(new RoadVertexIdentifier(roadIndex, j), out NetId netId) || netId.id != vNetId.id)
                        continue;

                    index = j;
                    break;
                }
                if (index == -1)
                {
                    Logger.DevkitServer.LogInfo(Source, $"[ROAD {roadIndex.Format()}] [VERTEX {i.Format()}] " +
                                                        $"Syncing vertex does not exist on current road.");
                    RoadUtil.AddVertexLocal(roadIndex, i, _vertices[i].Position);
                    anyUpdates = true;
                    if (!vNetId.IsNull())
                        RoadNetIdDatabase.RegisterVertex(roadIndex, i, vNetId);
                    index = i;
                }
                RoadJoint existing = road.joints[index];
                Vertex sync = _vertices[i];
                if (Mathf.Abs(existing.offset - sync.VerticalOffset) > 0.01f)
                {
                    Logger.DevkitServer.LogInfo(Source, $"[ROAD {roadIndex.Format()}] [VERTEX {index.Format()}] " +
                                                        $"Existing vertical offset ({existing.offset.Format("F2")}) does not match synced vertical offset ({sync.VerticalOffset.Format("F2")}).");
                    RoadUtil.SetVertexVerticalOffsetLocal(roadIndex, index, sync.VerticalOffset);
                    anyUpdates = true;
                }
                if (existing.ignoreTerrain != sync.IgnoreTerrain)
                {
                    Logger.DevkitServer.LogInfo(Source, $"[ROAD {roadIndex.Format()}] [VERTEX {index.Format()}] " +
                                                        $"Existing ignoreTerrain ({existing.ignoreTerrain.Format()}) does not match synced ignoreTerrain ({sync.IgnoreTerrain.Format()}).");
                    RoadUtil.SetVertexIgnoreTerrain(roadIndex, index, sync.IgnoreTerrain);
                    anyUpdates = true;
                }
                if (!existing.getTangent(0).IsNearlyEqual(sync.Tangent1, 0.01f) || !existing.getTangent(1).IsNearlyEqual(sync.Tangent2, 0.01f))
                {
                    Logger.DevkitServer.LogInfo(Source, $"[ROAD {roadIndex.Format()}] [VERTEX {index.Format()}] " +
                                                        $"Existing tangent handles ({existing.getTangent(0).Format()}, {existing.getTangent(1).Format()}) do not match synced tangent handles ({sync.Tangent1.Format()}, {sync.Tangent2.Format()}).");
                    ERoadMode old = existing.mode;
                    existing.mode = ERoadMode.FREE;
                    RoadUtil.SetTangentHandlePositionLocal(roadIndex, index, TangentHandle.Negative, sync.Tangent1);
                    RoadUtil.SetTangentHandlePositionLocal(roadIndex, index, TangentHandle.Positive, sync.Tangent2);
                    existing.mode = old;
                    anyUpdates = true;
                }
                if (existing.mode != sync.Mode)
                {
                    Logger.DevkitServer.LogInfo(Source, $"[ROAD {roadIndex.Format()}] [VERTEX {index.Format()}] " +
                                                        $"Existing handle mode ({existing.mode.Format()}) does not match synced handle mode ({sync.Mode.Format()}).");
                    RoadUtil.SetVertexTangentHandleModeLocal(roadIndex, i, sync.Mode);
                    anyUpdates = true;
                }
                if (!existing.vertex.IsNearlyEqual(sync.Position, 0.01f))
                {
                    Logger.DevkitServer.LogInfo(Source, $"[ROAD {roadIndex.Format()}] [VERTEX {index.Format()}] " +
                                                        $"Existing position ({existing.vertex.Format()}) does not match synced position ({sync.Position.Format()}).");
                    RoadUtil.SetVertexTangentHandleModeLocal(roadIndex, index, sync.Mode);
                    anyUpdates = true;
                }
            }

            return anyUpdates;
        }

    }
}
