using DevkitServer.Models;
using DevkitServer.Multiplayer.Networking;
using DevkitServer.Util.Encoding;

namespace DevkitServer.Multiplayer.Levels;
public sealed class RoadNetIdDatabase : IReplicatedLevelDataSource<RoadNetIdReplicatedLevelData>
{
    public ushort CurrentDataVersion => 0;

    private const string Source = "ROAD NET IDS";
    private static readonly Dictionary<int, NetId> RoadAssignments = new Dictionary<int, NetId>(32);
    private static readonly Dictionary<RoadVertexIdentifier, NetId> VertexAssignments = new Dictionary<RoadVertexIdentifier, NetId>(512);

    [UsedImplicitly]
    internal static NetCall<bool, int, NetId> SendBindRoadElement = new NetCall<bool, int, NetId>(DevkitServerNetCall.SendBindRoadElement);
    private RoadNetIdDatabase() { }
    internal static void Init()
    {
        RoadUtil.OnRoadRemoved += OnRoadRemoved;
        RoadUtil.OnVertexRemoved += OnVertexRemoved;
        RoadUtil.OnRoadIndexUpdated += OnRoadIndexUpdated;
        RoadUtil.OnVertexIndexUpdated += OnVertexIndexUpdated;
    }

    internal static void Shutdown()
    {
        RoadUtil.OnRoadRemoved -= OnRoadRemoved;
        RoadUtil.OnVertexRemoved -= OnVertexRemoved;
        RoadUtil.OnRoadIndexUpdated -= OnRoadIndexUpdated;
        RoadUtil.OnVertexIndexUpdated -= OnVertexIndexUpdated;
    }

    private static void OnRoadIndexUpdated(Road road, int fromIndex, int toIndex)
    {
        if (!DevkitServerModule.IsEditing)
            return;

        RoadAssignments.TryGetValue(toIndex, out NetId blockingNetId);
        RoadAssignments.TryGetValue(fromIndex, out NetId netId);
        if (!blockingNetId.IsNull() && blockingNetId == netId || netId.IsNull())
            return;

        if (!blockingNetId.IsNull())
        {
            Logger.LogDebug($"[{Source}] Released blocking net id to save road: # {fromIndex.Format()} ({netId.Format()}, # {toIndex.Format()}).");
            NetIdRegistry.Release(blockingNetId);
        }

        NetIdRegistry.Release(netId);
        NetIdRegistry.Assign(netId, toIndex);
        RoadAssignments.Remove(fromIndex);
        RoadAssignments[toIndex] = netId;
        Logger.LogDebug($"[{Source}] Moved road NetId: # {fromIndex.Format()} ({netId.Format()}, # {toIndex.Format()}).");
    }
    private static void OnVertexIndexUpdated(Road road, RoadVertexIdentifier fromIndex, RoadVertexIdentifier toIndex)
    {
        if (!DevkitServerModule.IsEditing)
            return;

        VertexAssignments.TryGetValue(toIndex, out NetId blockingNetId);
        VertexAssignments.TryGetValue(fromIndex, out NetId netId);
        if (!blockingNetId.IsNull() && blockingNetId == netId || netId.IsNull())
            return;

        if (!blockingNetId.IsNull())
        {
            Logger.LogDebug($"[{Source}] Released blocking net id to save vertex: {fromIndex.Format()} ({netId.Format()}, {toIndex.Format()}).");
            NetIdRegistry.Release(blockingNetId);
        }

        NetIdRegistry.Release(netId);
        NetIdRegistry.Assign(netId, toIndex);
        VertexAssignments.Remove(fromIndex);
        VertexAssignments[toIndex] = netId;
        Logger.LogDebug($"[{Source}] Moved vertex NetId: {fromIndex.Format()} ({netId.Format()}, {toIndex.Format()}).");
    }
    private static void OnRoadRemoved(Road road, int index)
    {
        if (!DevkitServerModule.IsEditing)
            return;

        RoadAssignments.TryGetValue(index, out NetId netId);
        if (netId.IsNull())
            return;

        NetIdRegistry.Release(netId);
        RoadAssignments.Remove(index);
        Logger.LogDebug($"[{Source}] Removed road NetId: ({netId.Format()}, # {index.Format()}).");
    }
    private static void OnVertexRemoved(Road road, RoadVertexIdentifier vertex)
    {
        if (!DevkitServerModule.IsEditing)
            return;

        VertexAssignments.TryGetValue(vertex, out NetId netId);
        if (netId.IsNull())
            return;

        NetIdRegistry.Release(netId);
        VertexAssignments.Remove(vertex);
        Logger.LogDebug($"[{Source}] Removed road vertex NetId: ({netId.Format()}, {vertex.Format()}).");
    }
    
#if CLIENT
    [NetCall(NetCallSource.FromServer, DevkitServerNetCall.SendBindRoadElement)]
    private static StandardErrorCode ReceiveBindRoadElement(MessageContext ctx, bool isVertex, int index, NetId netId)
    {
        if (isVertex)
        {
            RoadVertexIdentifier identifier = new RoadVertexIdentifier(index);
            if (LevelRoads.getRoad(identifier.Road) is { } road && road.joints.Count < identifier.Vertex)
                ClaimVertexNetId(identifier, netId);
            else
                return StandardErrorCode.NotFound;
        }
        else
        {
            if (LevelRoads.getRoad(index) != null)
                ClaimRoadNetId(index, netId);
            else
                return StandardErrorCode.NotFound;
        }
        
        return StandardErrorCode.Success;
    }
#endif
    public static void RemoveVertex(RoadVertexIdentifier vertex)
    {
        if (VertexAssignments.TryGetValue(vertex, out NetId netId))
        {
            NetIdRegistry.Release(netId);
            VertexAssignments.Remove(vertex);
            if (Level.isLoaded)
                Logger.LogDebug($"[{Source}] Released vertex NetId: {netId.Format()} ({vertex.Format()}).");
        }
        else
            Logger.LogWarning($"Unable to release NetId to vertex {vertex.Format()}, NetId not registered.", method: Source);
    }
    public static NetId AddVertex(RoadVertexIdentifier vertex)
    {
        if (LevelRoads.getRoad(vertex.Road) is not { } road || road.joints.Count <= vertex.Vertex)
            throw new ArgumentException($"Road and vertex pair '{vertex}' do not exist.");

        NetId netId = NetIdRegistry.Claim();

        ClaimVertexNetId(vertex, netId);

        return netId;
    }
    public static NetId AddVertex(Road road, int vertexIndex)
    {
        int roadIndex = road.GetRoadIndex();
        if (roadIndex < 0)
            throw new ArgumentException("Road is not present in LevelRoads list.", nameof(road));
        return AddVertex(new RoadVertexIdentifier(roadIndex, vertexIndex));
    }
    public static NetId AddVertex(int roadIndex, int vertexIndex)
    {
        return AddVertex(new RoadVertexIdentifier(roadIndex, vertexIndex));
    }
    public static NetId AddRoad(int roadIndex)
    {
        if (LevelRoads.getRoad(roadIndex) == null)
            throw new ArgumentException($"Road at index {roadIndex} does not exist.");

        NetId netId = NetIdRegistry.Claim();

        ClaimRoadNetId(roadIndex, netId);

        return netId;
    }
    public static NetId AddRoad(Road road)
    {
        int roadIndex = road.GetRoadIndex();
        if (roadIndex < 0)
            throw new ArgumentException("Road is not present in LevelRoads list.", nameof(road));
        return AddRoad(roadIndex);
    }
    public static bool TryGetVertex(NetId netId, out RoadVertexIdentifier vertex)
    {
        object? value = NetIdRegistry.Get(netId);
        if (value is RoadVertexIdentifier vertexId)
        {
            vertex = vertexId;
            return LevelRoads.getRoad(vertexId.Road) is { } road && road.joints.Count > vertexId.Vertex;
        }

        vertex = default;
        return false;
    }
    public static bool TryGetVertex(NetId netId, out Road road, out RoadVertexIdentifier vertex)
    {
        object? value = NetIdRegistry.Get(netId);
        if (value is RoadVertexIdentifier vertexId)
        {
            vertex = vertexId;
            road = LevelRoads.getRoad(vertexId.Road);
            return road != null && road.joints.Count > vertexId.Vertex;
        }

        vertex = default;
        road = null!;
        return false;
    }
    public static bool TryGetRoad(NetId netId, out int roadIndex)
    {
        object? value = NetIdRegistry.Get(netId);
        if (value is int index)
        {
            roadIndex = index;
            return LevelRoads.getRoad(index) != null;
        }

        roadIndex = -1;
        return false;
    }
    public static bool TryGetRoad(NetId netId, out Road road, out int roadIndex)
    {
        object? value = NetIdRegistry.Get(netId);
        if (value is int index)
        {
            roadIndex = index;
            road = LevelRoads.getRoad(index);
            return road != null;
        }

        roadIndex = -1;
        road = null!;
        return false;
    }
    public static bool TryGetRoad(NetId netId, out Road road)
    {
        object? value = NetIdRegistry.Get(netId);
        if (value is int index)
        {
            road = LevelRoads.getRoad(index);
            return road != null;
        }
        
        road = null!;
        return false;
    }
    public static bool TryGetRoadNetId(int roadIndex, out NetId netId)
    {
        return RoadAssignments.TryGetValue(roadIndex, out netId);
    }
    public static bool TryGetRoadNetId(Road road, out NetId netId)
    {
        int roadIndex = road.GetRoadIndex();
        if (roadIndex < 0)
            throw new ArgumentException("Road is not present in LevelRoads list.", nameof(road));
        return RoadAssignments.TryGetValue(roadIndex, out netId);
    }
    public static bool TryGetVertexNetId(RoadVertexIdentifier vertex, out NetId netId)
    {
        return VertexAssignments.TryGetValue(vertex, out netId);
    }
    public static bool TryGetVertexNetId(int roadIndex, int vertexIndex, out NetId netId)
    {
        return VertexAssignments.TryGetValue(new RoadVertexIdentifier(roadIndex, vertexIndex), out netId);
    }
    public static bool TryGetVertexNetId(Road road, int vertexIndex, out NetId netId)
    {
        int roadIndex = road.GetRoadIndex();
        if (roadIndex < 0)
            throw new ArgumentException("Road is not present in LevelRoads list.", nameof(road));
        return VertexAssignments.TryGetValue(new RoadVertexIdentifier(roadIndex, vertexIndex), out netId);
    }
#if SERVER
    internal static void AssignExisting()
    {
        RoadAssignments.Clear();
        VertexAssignments.Clear();

        int vertexCount = 0;
        
        for (int i = 0; i < ushort.MaxValue; ++i)
        {
            Road? road = LevelRoads.getRoad(i);
            if (road == null)
            {
                Logger.LogInfo($"[{Source}] Assigned NetIds for {i.Format()} road{i.S()}.");
                break;
            }

            AddRoad(i);
            int joints = Math.Min(ushort.MaxValue, road.joints.Count);
            for (int j = 0; j < joints; ++j)
            {
                AddVertex(new RoadVertexIdentifier((ushort)i, (ushort)j));
                ++vertexCount;
            }
        }

        Logger.LogInfo($"[{Source}] Assigned NetIds for {vertexCount.Format()} road {(vertexCount == 1 ? "vertex" : "verticies")}.");
    }
#endif
    private static void ClaimVertexNetId(RoadVertexIdentifier vertex, NetId netId)
    {
        if (!netId.IsNull() && NetIdRegistry.Release(netId))
        {
            if (Level.isLoaded)
                Logger.LogDebug($"[{Source}] Released old NetId pairing: {netId.Format()}.");
        }

        if (VertexAssignments.TryGetValue(vertex, out NetId old))
            VertexAssignments.Remove(vertex);

        if (NetIdRegistry.Release(old) && Level.isLoaded)
            Logger.LogDebug($"[{Source}] Released old vertex NetId pairing for {vertex.Format()}: {old.Format()}.");
        

        if (!netId.IsNull())
        {
            VertexAssignments[vertex] = netId;
            NetIdRegistry.Assign(netId, vertex);

            if (Level.isLoaded)
                Logger.LogDebug($"[{Source}] Claimed new vertex NetId: {netId.Format()} for {vertex.Format()}).");
        }
        else
        {
            Logger.LogDebug($"[{Source}] Released vertex NetId: {netId.Format()} for {vertex.Format()}.");
        }
    }
    private static void ClaimRoadNetId(int roadIndex, NetId netId)
    {
        if (!netId.IsNull() && NetIdRegistry.Release(netId))
        {
            if (Level.isLoaded)
                Logger.LogDebug($"[{Source}] Released old NetId pairing: {netId.Format()}.");
        }

        if (RoadAssignments.TryGetValue(roadIndex, out NetId old))
            RoadAssignments.Remove(roadIndex);

        if (NetIdRegistry.Release(old) && Level.isLoaded)
            Logger.LogDebug($"[{Source}] Released old road NetId pairing for #{roadIndex.Format()}: {old.Format()}.");
        

        if (!netId.IsNull())
        {
            RoadAssignments[roadIndex] = netId;
            NetIdRegistry.Assign(netId, roadIndex);

            if (Level.isLoaded)
                Logger.LogDebug($"[{Source}] Claimed new road NetId: {netId.Format()} for #{roadIndex.Format()}).");
        }
        else
        {
            Logger.LogDebug($"[{Source}] Released road NetId: {netId.Format()} for #{roadIndex.Format()}.");
        }
    }
    public static void RegisterRoad(int roadIndex, NetId netId) => ClaimRoadNetId(roadIndex, netId);
    public static void RegisterRoad(Road road, NetId netId)
    {
        int roadIndex = road.GetRoadIndex();
        if (roadIndex < 0)
            throw new ArgumentException("Road is not present in LevelRoads list.", nameof(road));
        ClaimRoadNetId(roadIndex, netId);
    }
    public static void RegisterVertex(RoadVertexIdentifier vertex, NetId netId) => ClaimVertexNetId(vertex, netId);
    public static void RegisterVertex(int roadIndex, int vertexIndex, NetId netId) => ClaimVertexNetId(new RoadVertexIdentifier(roadIndex, vertexIndex), netId);
    public static void RegisterVertex(Road road, int vertexIndex, NetId netId)
    {
        int roadIndex = road.GetRoadIndex();
        if (roadIndex < 0)
            throw new ArgumentException("Road is not present in LevelRoads list.", nameof(road));
        ClaimVertexNetId(new RoadVertexIdentifier(roadIndex, vertexIndex), netId);
    }
#if CLIENT
    public void LoadData(RoadNetIdReplicatedLevelData data)
    {
        NetId[] netIds = data.NetIds;
        RoadVertexIdentifier[] verticies = data.VertexIndexes;
        int roadCount = data.RoadCount;
        
        for (int i = 0; i < roadCount; ++i)
        {
            NetId netId = netIds[i];
            if (!netId.IsNull())
                ClaimRoadNetId(i, netId);
        }

        for (int i = roadCount; i < netIds.Length; ++i)
        {
            NetId netId = netIds[i];
            if (!netId.IsNull())
                ClaimVertexNetId(verticies[i - roadCount], netId);
        }
    }
#elif SERVER       
    public RoadNetIdReplicatedLevelData SaveData()
    {
        RoadNetIdReplicatedLevelData data = new RoadNetIdReplicatedLevelData();
        int vertexCount = Math.Min(int.MaxValue - ushort.MaxValue, VertexAssignments.Count);
        int roadCount = Math.Min(ushort.MaxValue, RoadAssignments.Count > 0 ? RoadAssignments.Keys.Max() + 1 : 0);
        NetId[] netIds = new NetId[vertexCount + roadCount];
        RoadVertexIdentifier[] verticies = new RoadVertexIdentifier[vertexCount];

        data.NetIds = netIds;
        data.RoadCount = (ushort)roadCount;
        data.VertexIndexes = verticies;

        foreach (KeyValuePair<int, NetId> lvlObject in RoadAssignments)
        {
            netIds[lvlObject.Key] = lvlObject.Value;
        }

        int index = -1;
        foreach (KeyValuePair<RoadVertexIdentifier, NetId> vertex in VertexAssignments)
        {
            verticies[++index] = vertex.Key;
            netIds[roadCount + index] = vertex.Value;
        }

        return data;
    }
#endif
    public RoadNetIdReplicatedLevelData ReadData(ByteReader reader, ushort version)
    {
        int vertexCount = reader.ReadInt32();
        RoadNetIdReplicatedLevelData data = new RoadNetIdReplicatedLevelData
        {
            RoadCount = reader.ReadUInt16()
        };
        int roadCount = data.RoadCount;

        NetId[] netIds = new NetId[roadCount + vertexCount];
        RoadVertexIdentifier[] verticies = new RoadVertexIdentifier[vertexCount];

        for (int i = 0; i < vertexCount; ++i)
            verticies[i] = new RoadVertexIdentifier(reader.ReadInt32());
        for (int i = 0; i < netIds.Length; ++i)
            netIds[i] = reader.ReadNetId();

        data.VertexIndexes = verticies;
        data.NetIds = netIds;

        return data;
    }
    public void WriteData(ByteWriter writer, RoadNetIdReplicatedLevelData data)
    {
        int ct = Math.Min(data.VertexIndexes.Length, int.MaxValue - ushort.MaxValue);
        writer.Write(ct);

        writer.Write(data.RoadCount);
        
        for (int i = 0; i < ct; ++i)
            writer.Write(data.VertexIndexes[i].RawData);

        ct += data.RoadCount;

        for (int i = 0; i < ct; ++i)
            writer.Write(data.NetIds[i]);
    }
}

#nullable disable
public class RoadNetIdReplicatedLevelData
{
    public ushort RoadCount { get; set; }
    public RoadVertexIdentifier[] VertexIndexes { get; set; }
    public NetId[] NetIds { get; set; }
}

#nullable restore