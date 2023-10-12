using DevkitServer.Util.Encoding;
using Pathfinding;

namespace DevkitServer.Util;
public static class NavigationUtil
{
    private static readonly StaticGetter<List<Flag>> GetNavigationFlags = Accessor.GenerateStaticGetter<LevelNavigation, List<Flag>>("flags", throwOnError: true)!;
    private static readonly Action<Flag>? UpdateNavmesh = Accessor.GenerateInstanceCaller<Flag, Action<Flag>>("updateNavmesh", false);

    /// <summary>
    /// Largest possible size a navigation flag can be in bytes.
    /// </summary>
    /// <remarks>Around 55.6 MiB.</remarks>
    public static readonly long MaxNavigationPacketSize = 59660047377L;

    /// <returns>A readonly value collection used to loop through all the existing navigations.</returns>
    public static IReadOnlyList<Flag> NavigationFlags => GetNavigationFlags();
    /// <summary>
    /// If possible, use <see cref="NavigationFlags"/> instead.
    /// </summary>
    /// <returns>A copy of all existing navigation flags.</returns>
    public static List<Flag> GetAllNavigationFlags() => new List<Flag>(NavigationFlags);

    /// <summary>
    /// Calls <see cref="Flag"/>.updateNavmesh if in editor mode.
    /// </summary>
    /// <returns><see langword="false"/> in the case of a reflection failure.</returns>
    public static bool UpdateEditorNavmesh(this Flag flag)
    {
        if (UpdateNavmesh == null)
            return false;

        UpdateNavmesh(flag);
        return true;
    }

    /// <summary>
    /// Calculates the amount of bytes that will be written by <see cref="WriteRecastGraphData"/>.
    /// </summary>
    public static int CalculateTotalWriteSize(RecastGraph graph)
    {
        int ct = 27 + sizeof(ushort) * 2 * graph.tileZCount * graph.tileXCount;

        RecastGraph.NavmeshTile[] tiles = graph.GetTiles();

        for (int z = 0; z < graph.tileZCount; ++z)
        {
            for (int x = 0; x < graph.tileXCount; ++x)
            {
                int offset = x + z * graph.tileXCount;

                RecastGraph.NavmeshTile tile = tiles[offset];

                ct += tile.tris.Length * sizeof(ushort);
                ct += tile.verts.Length * sizeof(int) * 3;
            }
        }

        return ct;
    }

    /// <summary>
    /// Write graph data to a <see cref="ByteWriter"/>.
    /// </summary>
    /// <remarks>Does the same thing as <see cref="LevelNavigation.save"/></remarks>
    public static void WriteRecastGraphData(ByteWriter writer, RecastGraph graph)
    {
        const byte version = 0;

        writer.Write(version);

        writer.Write(graph.forcedBoundsCenter);
        writer.Write(graph.forcedBoundsSize);
        writer.Write((byte)graph.tileXCount);
        writer.Write((byte)graph.tileZCount);

        RecastGraph.NavmeshTile[] tiles = graph.GetTiles();

        for (int z = 0; z < graph.tileZCount; ++z)
        {
            for (int x = 0; x < graph.tileXCount; ++x)
            {
                int offset = x + z * graph.tileXCount;

                RecastGraph.NavmeshTile tile = tiles[offset];

                writer.Write((ushort)tile.tris.Length);
                for (int i = 0; i < tile.tris.Length; ++i)
                    writer.Write((ushort)tile.tris[i]);
                
                writer.Write((ushort)tile.verts.Length);
                for (int i = 0; i < tile.verts.Length; ++i)
                {
                    Int3 vert = tile.verts[i];
                    writer.Write(vert.x);
                    writer.Write(vert.y);
                    writer.Write(vert.z);
                }
            }
        }
    }

    /// <summary>
    /// Read graph data to an existing recast graph from a <see cref="ByteReader"/>.
    /// </summary>
    /// <remarks>Does the same thing as <see cref="LevelNavigation.buildGraph"/>.</remarks>
    public static void ReadRecastGraphDataTo(ByteReader reader, RecastGraph graph)
    {
        AstarPath.active.BlockUntilPathQueueBlocked();

        reader.ReadUInt8(); // version

        TriangleMeshNode.SetNavmeshHolder((int)graph.graphIndex, graph);

        graph.forcedBoundsCenter = reader.ReadVector3();
        graph.forcedBoundsSize = reader.ReadVector3();
        graph.tileXCount = reader.ReadUInt8();
        graph.tileZCount = reader.ReadUInt8();

        RecastGraph.NavmeshTile[] tiles = new RecastGraph.NavmeshTile[graph.tileXCount * graph.tileZCount];
        graph.SetTiles(tiles);

        for (int z = 0; z < graph.tileZCount; ++z)
        {
            for (int x = 0; x < graph.tileXCount; ++x)
            {
                RecastGraph.NavmeshTile tile = new RecastGraph.NavmeshTile
                {
                    x = x,
                    z = z,
                    w = 1,
                    d = 1
                };

                graph.bbTree = new BBTree(graph);
                
                int offset = x + z * graph.tileXCount;
                tiles[offset] = tile;

                tile.tris = new int[reader.ReadUInt16()];
                for (int i = 0; i < tile.tris.Length; ++i)
                    tile.tris[i] = reader.ReadUInt16();

                tile.verts = new Int3[reader.ReadUInt16()];
                for (int i = 0; i < tile.verts.Length; ++i)
                    tile.verts[i] = new Int3(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());

                tile.nodes = new TriangleMeshNode[tile.tris.Length / 3];

                int offsetBits = offset << 12;

                for (int i = 0; i < tile.nodes.Length; ++i)
                {
                    TriangleMeshNode node = new TriangleMeshNode(AstarPath.active);
                    tile.nodes[i] = node;

                    node.GraphIndex = graph.graphIndex;
                    node.Penalty = 0u;
                    node.Walkable = true;
                    node.v0 = tile.tris[i * 3] | offsetBits;
                    node.v1 = tile.tris[i * 3 + 1] | offsetBits;
                    node.v2 = tile.tris[i * 3 + 2] | offsetBits;
                    
                    node.UpdatePositionFromVertices();
                    graph.bbTree.Insert(node);
                }

                graph.CreateNodeConnections(tile.nodes);
            }
        }

        for (int z = 0; z < graph.tileZCount; ++z)
        {
            for (int x = 0; x < graph.tileXCount; ++x)
            {
                int offset = x + z * graph.tileXCount;
                graph.ConnectTileWithNeighbours(tiles[offset]);
            }
        }
    }
}
