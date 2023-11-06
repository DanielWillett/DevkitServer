using DevkitServer.API;
using DevkitServer.API.Permissions;
using DevkitServer.Core.Permissions;
using DevkitServer.Multiplayer.Actions;
using DevkitServer.Multiplayer.Levels;
using DevkitServer.Multiplayer.Networking;
using DevkitServer.Multiplayer.Sync;
using DevkitServer.Players;
using DevkitServer.Util.Encoding;
using Pathfinding;

#if SERVER
using DevkitServer.API.UI;
#endif

namespace DevkitServer.Util;
public delegate void FlagArgs(Flag flag, byte nav);
public delegate void FlagIndexUpdated(Flag flag, byte fromNav, byte toNav);
public delegate void FlagMoved(Flag flag, byte nav, Vector3 fromPosition, Vector3 toPosition);
public delegate void FlagSizeUpdated(Flag flag, byte nav, Vector2 fromSize, Vector2 toSize);
public delegate void FlagDifficultyUpdated(Flag flag, byte nav, AssetReference<ZombieDifficultyAsset> fromDifficulty, AssetReference<ZombieDifficultyAsset> toDifficulty);
public delegate void FlagZombiesUpdated(Flag flag, byte nav, int fromZombies, int toZombies);
public delegate void FlagBooleanUpdated(Flag flag, byte nav, bool value);
public static class NavigationUtil
{
    private const string Source = "NAV UTIL";
#if CLIENT
    internal static bool HoldUIUpdate;

    private static readonly Action<Transform?>? CallSelect = Accessor.GenerateStaticCaller<EditorNavigation, Action<Transform?>>("select", false);
    private static readonly StaticSetter<Transform?>? SetSelection = Accessor.GenerateStaticSetter<EditorNavigation, Transform?>("selection", false);
#endif

    private static readonly StaticGetter<List<Flag>> GetNavigationFlags = Accessor.GenerateStaticGetter<LevelNavigation, List<Flag>>("flags", throwOnError: true)!;
    private static readonly Action<Flag>? UpdateNavmesh = Accessor.GenerateInstanceCaller<Flag, Action<Flag>>("updateNavmesh", false);

    private static readonly CachedMulticastEvent<FlagArgs> EventOnFlagAdded = new CachedMulticastEvent<FlagArgs>(typeof(NavigationUtil), nameof(OnFlagAdded));
    private static readonly CachedMulticastEvent<FlagArgs> EventOnFlagRemoved = new CachedMulticastEvent<FlagArgs>(typeof(NavigationUtil), nameof(OnFlagRemoved));
    private static readonly CachedMulticastEvent<FlagIndexUpdated> EventOnFlagIndexUpdated = new CachedMulticastEvent<FlagIndexUpdated>(typeof(NavigationUtil), nameof(OnFlagIndexUpdated));
    private static readonly CachedMulticastEvent<FlagMoved> EventOnFlagMoved = new CachedMulticastEvent<FlagMoved>(typeof(NavigationUtil), nameof(OnFlagMoved));
    private static readonly CachedMulticastEvent<FlagSizeUpdated> EventOnFlagSizeUpdated = new CachedMulticastEvent<FlagSizeUpdated>(typeof(NavigationUtil), nameof(OnFlagSizeUpdated));
    private static readonly CachedMulticastEvent<FlagDifficultyUpdated> EventOnFlagDifficultyUpdated = new CachedMulticastEvent<FlagDifficultyUpdated>(typeof(NavigationUtil), nameof(OnFlagDifficultyUpdated));
    private static readonly CachedMulticastEvent<FlagZombiesUpdated> EventOnFlagMaximumZombiesUpdated = new CachedMulticastEvent<FlagZombiesUpdated>(typeof(NavigationUtil), nameof(OnFlagMaximumZombiesUpdated));
    private static readonly CachedMulticastEvent<FlagZombiesUpdated> EventOnFlagMaximumBossZombiesUpdated = new CachedMulticastEvent<FlagZombiesUpdated>(typeof(NavigationUtil), nameof(OnFlagMaximumBossZombiesUpdated));
    private static readonly CachedMulticastEvent<FlagBooleanUpdated> EventOnFlagShouldSpawnZombiesUpdated = new CachedMulticastEvent<FlagBooleanUpdated>(typeof(NavigationUtil), nameof(OnFlagShouldSpawnZombiesUpdated));
    private static readonly CachedMulticastEvent<FlagBooleanUpdated> EventOnFlagInfiniteAgroDistanceUpdated = new CachedMulticastEvent<FlagBooleanUpdated>(typeof(NavigationUtil), nameof(OnFlagInfiniteAgroDistanceUpdated));

    /// <summary>
    /// Called when a <see cref="Flag"/> is added locally.
    /// </summary>
    public static event FlagArgs OnFlagAdded
    {
        add => EventOnFlagAdded.Add(value);
        remove => EventOnFlagAdded.Remove(value);
    }

    /// <summary>
    /// Called when a <see cref="Flag"/> is removed locally.
    /// </summary>
    public static event FlagArgs OnFlagRemoved
    {
        add => EventOnFlagRemoved.Add(value);
        remove => EventOnFlagRemoved.Remove(value);
    }

    /// <summary>
    /// Called when a <see cref="Flag"/>'s index (or 'nav') changes locally.
    /// </summary>
    public static event FlagIndexUpdated OnFlagIndexUpdated
    {
        add => EventOnFlagIndexUpdated.Add(value);
        remove => EventOnFlagIndexUpdated.Remove(value);
    }

    /// <summary>
    /// Called when a <see cref="Flag"/>'s position changes locally.
    /// </summary>
    public static event FlagMoved OnFlagMoved
    {
        add => EventOnFlagMoved.Add(value);
        remove => EventOnFlagMoved.Remove(value);
    }

    /// <summary>
    /// Called when a <see cref="Flag"/>'s width and/or height changes locally.
    /// </summary>
    public static event FlagSizeUpdated OnFlagSizeUpdated
    {
        add => EventOnFlagSizeUpdated.Add(value);
        remove => EventOnFlagSizeUpdated.Remove(value);
    }

    /// <summary>
    /// Called when a <see cref="Flag"/>'s difficulty asset changes locally.
    /// </summary>
    public static event FlagDifficultyUpdated OnFlagDifficultyUpdated
    {
        add => EventOnFlagDifficultyUpdated.Add(value);
        remove => EventOnFlagDifficultyUpdated.Remove(value);
    }

    /// <summary>
    /// Called when a <see cref="Flag"/>'s max zombie count changes locally.
    /// </summary>
    public static event FlagZombiesUpdated OnFlagMaximumZombiesUpdated
    {
        add => EventOnFlagMaximumZombiesUpdated.Add(value);
        remove => EventOnFlagMaximumZombiesUpdated.Remove(value);
    }

    /// <summary>
    /// Called when a <see cref="Flag"/>'s max boss zombie count changes locally.
    /// </summary>
    public static event FlagZombiesUpdated OnFlagMaximumBossZombiesUpdated
    {
        add => EventOnFlagMaximumBossZombiesUpdated.Add(value);
        remove => EventOnFlagMaximumBossZombiesUpdated.Remove(value);
    }

    /// <summary>
    /// Called when a <see cref="Flag"/>'s should spawn zombies changes locally.
    /// </summary>
    public static event FlagBooleanUpdated OnFlagShouldSpawnZombiesUpdated
    {
        add => EventOnFlagShouldSpawnZombiesUpdated.Add(value);
        remove => EventOnFlagShouldSpawnZombiesUpdated.Remove(value);
    }

    /// <summary>
    /// Called when a <see cref="Flag"/>'s infinite agro distance changes locally.
    /// </summary>
    public static event FlagBooleanUpdated OnFlagInfiniteAgroDistanceUpdated
    {
        add => EventOnFlagInfiniteAgroDistanceUpdated.Add(value);
        remove => EventOnFlagInfiniteAgroDistanceUpdated.Remove(value);
    }

    /// <summary>
    /// Largest possible size a navigation flag can be in bytes.
    /// </summary>
    /// <remarks>Around 55.6 MiB.</remarks>
    public static readonly long MaxNavigationPacketSize = 59660047377L;

    [UsedImplicitly]
    private static readonly NetCall<Vector3> SendRequestInstantiation = new NetCall<Vector3>(DevkitServerNetCall.RequestFlagInstantiation);

    [UsedImplicitly]
    private static readonly NetCall<Vector3, Vector2, NetId, ulong, bool, bool, byte, int, Guid> SendInstantiation = new NetCall<Vector3, Vector2, NetId, ulong, bool, bool, byte, int, Guid>(DevkitServerNetCall.SendFlagInstantiation);

#if CLIENT
    internal static bool ClearSelection()
    {
        if (SetSelection == null)
            return false;

        SetSelection(null);
        return true;
    }

    /// <summary>
    /// Deselect the flag currently selected.
    /// </summary>
    /// <returns><see langword="false"/> in the case of a reflection failure, otherwise <see langword="true"/>.</returns>
    public static bool Deselect() => Select((Transform?)null);

    /// <summary>
    /// Select <paramref name="flag"/>. This can be a vertex or tangent handle editor object.
    /// </summary>
    /// <remarks>Will do nothing if <paramref name="flag"/> is already selected. Passing <see langword="null"/> is the same as calling <see cref="Deselect"/>.</remarks>
    /// <returns><see langword="false"/> in the case of a reflection failure, otherwise <see langword="true"/>.</returns>
    public static bool Select(Flag? flag) => Select(flag?.model);

    /// <summary>
    /// Select the flag belonging to the element <paramref name="flagModel"/>. This can be a vertex or tangent handle editor object.
    /// </summary>
    /// <remarks>Will do nothing if <paramref name="flagModel"/> is already selected. Passing <see langword="null"/> is the same as calling <see cref="Deselect"/>.</remarks>
    /// <returns><see langword="false"/> in the case of a reflection failure, otherwise <see langword="true"/>.</returns>
    public static bool Select(Transform? flagModel)
    {
        ThreadUtil.assertIsGameThread();

        if (CallSelect == null)
            return false;

        if (flagModel != null && EditorNavigation.flag?.model != flagModel)
            CallSelect(flagModel);

        return true;
    }

    /// <summary>
    /// Sends a request to the server to instantiate a navigation flag.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when not on a DevkitServer server.</exception>
    public static void RequestFlagInstantiation(Vector3 firstVertexWorldPosition)
    {
        DevkitServerModule.AssertIsDevkitServerClient();
        SendRequestInstantiation.Invoke(firstVertexWorldPosition);
    }

    [NetCall(NetCallSource.FromServer, DevkitServerNetCall.SendFlagInstantiation)]
    internal static StandardErrorCode ReceiveInstantiation(MessageContext ctx, Vector3 position, Vector2 size, NetId netId, ulong owner, bool infiniteAgroDistance, bool shouldSpawnZombies, byte maxZombies, int maxBossZombies, Guid difficultyAsset)
    {
        if (!EditorActions.HasProcessedPendingFlags)
        {
            EditorActions.TemporaryEditorActions?.QueueFlagInstantiation(netId, position, size, owner, infiniteAgroDistance, shouldSpawnZombies, maxZombies, maxBossZombies, difficultyAsset);
            return StandardErrorCode.Success;
        }

        HoldUIUpdate = true;
        try
        {
            Flag flag = AddFlagLocal(position);

            InitializeFlag(flag, out int flagIndex, netId);

            if (flag == null)
                return StandardErrorCode.GenericError;

            byte nav = (byte)flagIndex;
            
            if (flag.width != size.x || flag.height != size.y)
                SetFlagSizeLocal(nav, size);

            if (flag.data.hyperAgro != infiniteAgroDistance)
                SetFlagInfiniteAgroDistanceLocal(nav, infiniteAgroDistance);

            if (flag.data.spawnZombies != shouldSpawnZombies)
                SetFlagShouldSpawnZombiesLocal(nav, shouldSpawnZombies);

            if (flag.data.maxZombies != maxZombies)
                SetFlagMaximumZombiesLocal(nav, maxZombies);

            if (flag.data.maxBossZombies != maxBossZombies)
                SetFlagMaximumBossZombiesLocal(nav, maxBossZombies);

            if (flag.data.difficulty.GUID != difficultyAsset)
                SetFlagDifficultyLocal(nav, new AssetReference<ZombieDifficultyAsset>(difficultyAsset));

            Select(flag);

            // todo SyncIfAuthority(nav);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to initialize road: {netId.Format()}.", method: Source);
            Logger.LogError(ex, method: Source);
            HoldUIUpdate = false;
            return StandardErrorCode.GenericError;
        }

        return StandardErrorCode.Success;
    }
#endif
#if SERVER
    [NetCall(NetCallSource.FromClient, DevkitServerNetCall.RequestFlagInstantiation)]
    internal static void ReceiveFlagInstantiationRequest(MessageContext ctx, Vector3 position)
    {
        EditorUser? user = ctx.GetCaller();
        if (user == null || !user.IsOnline)
        {
            Logger.LogError("Unable to get user from flag instantiation request.", method: Source);
            ctx.Acknowledge(StandardErrorCode.NoPermissions);
            return;
        }

        if (!VanillaPermissions.EditNavigation.Has(user))
        {
            ctx.Acknowledge(StandardErrorCode.NoPermissions);
            EditorMessage.SendNoPermissionMessage(user, VanillaPermissions.EditNavigation);
            return;
        }

        AddFlag(position, ctx);
        
        Logger.LogDebug($"[{Source}] Granted request for instantiation of flag at {position.Format()} from {user.SteamId.Format()}.");

        ctx.Acknowledge(StandardErrorCode.Success);
    }
#endif

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
        ThreadUtil.assertIsGameThread();

        if (UpdateNavmesh == null)
            return false;

        UpdateNavmesh(flag);
        return true;
    }

    /// <summary>
    /// Tries to find the index of the <see cref="Flag"/> in <c><see cref="LevelNavigation"/>.flags</c>.
    /// </summary>
    public static bool TryGetIndex(this Flag flag, out byte nav)
    {
        int index2 = flag.GetIndex();
        if (index2 < 0)
        {
            nav = byte.MaxValue;
            return false;
        }

        nav = (byte)index2;
        return true;
    }

    /// <summary>
    /// Gets the index of the <see cref="Flag"/> in <c><see cref="LevelNavigation"/>.flags</c>, or -1 if it's not found or in the case of a reflection failure.
    /// </summary>
    public static int GetIndex(this Flag flag)
    {
        if (GetNavigationFlags == null)
            return -1;

        int index = GetNavigationFlags().IndexOf(flag);
        return index is < 0 or >= byte.MaxValue ? -1 : index;
    }

    /// <summary>
    /// Gets the <see cref="Flag"/> at index <paramref name="nav"/>, or returns <see langword="false"/> if it's out of range or in the case of a reflection failure.
    /// </summary>
    /// <param name="nav">Flag index in <c><see cref="LevelNavigation"/>.flags</c>.</param>
    public static bool TryGetFlag(byte nav, out Flag flag)
    {
        flag = GetFlag(nav)!;
        return flag != null;
    }

    /// <summary>
    /// Gets the <see cref="Flag"/> at index <paramref name="nav"/>, or <see langword="null"/> if it's out of range or in the case of a reflection failure.
    /// </summary>
    /// <param name="nav">Flag index in <c><see cref="LevelNavigation"/>.flags</c>.</param>
    public static Flag? GetFlag(byte nav)
    {
        ThreadUtil.assertIsGameThread();

        if (GetNavigationFlags == null)
            return null;

        IReadOnlyList<Flag> flagList = GetNavigationFlags();
        if (nav == byte.MaxValue || nav >= flagList.Count)
            return null;

        return flagList[nav];
    }
    internal static Flag GetFlagUnsafe(byte nav) => NavigationFlags[nav];
    internal static bool CheckSync(out NavigationSync sync)
    {
        sync = null!;
#if CLIENT
        if (!DevkitServerModule.IsEditing || EditorUser.User == null || EditorUser.User.NavigationSync == null || !EditorUser.User.NavigationSync.HasAuthority)
            return false;
        sync = EditorUser.User.NavigationSync;
#elif SERVER
        if (!DevkitServerModule.IsEditing || NavigationSync.ServersideAuthority == null || !NavigationSync.ServersideAuthority.HasAuthority)
            return false;
        sync = NavigationSync.ServersideAuthority;
#endif
        return true;
    }

    /// <summary>
    /// Queues the navigation mesh for the provided <see cref="Flag"/> to sync if local has authority over <see cref="NavigationSync"/>.
    /// </summary>
    public static bool SyncGraphIfAuthority(Flag flag)
    {
        ThreadUtil.assertIsGameThread();

        if (!CheckSync(out NavigationSync sync) || !flag.TryGetIndex(out byte nav) || !NavigationNetIdDatabase.TryGetNavigationNetId(nav, out NetId netId))
            return false;

        sync.EnqueueSync(netId);
        return true;
    }

    /// <summary>
    /// Queues the navigation mesh at the provided index to sync if local has authority over <see cref="NavigationSync"/>.
    /// </summary>
    public static bool SyncGraphIfAuthority(byte nav)
    {
        ThreadUtil.assertIsGameThread();

        if (!CheckSync(out NavigationSync sync) || !NavigationNetIdDatabase.TryGetNavigationNetId(nav, out NetId netId))
            return false;

        sync.EnqueueSync(netId);
        return true;
    }

    /// <summary>
    /// Queues the navigation mesh at the provided index to sync if local has authority over <see cref="NavigationSync"/>.
    /// </summary>
    public static bool SyncGraphIfAuthority(NetId netId)
    {
        ThreadUtil.assertIsGameThread();

        if (!CheckSync(out NavigationSync sync))
            return false;

        sync.EnqueueSync(netId);
        return true;
    }

    /// <summary>
    /// Calculates the amount of bytes that will be written by <see cref="WriteRecastGraphData"/>.
    /// </summary>
    public static int CalculateTotalWriteSize(RecastGraph graph)
    {
        ThreadUtil.assertIsGameThread();

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
        ThreadUtil.assertIsGameThread();

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
        ThreadUtil.assertIsGameThread();

        AstarPath.active.BlockUntilPathQueueBlocked();

        reader.ReadUInt8(); // version

        TriangleMeshNode.SetNavmeshHolder((int)graph.graphIndex, graph);

        uint graphIndex = (uint)AstarPath.active.astarData.GetGraphIndex(graph);

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

                    node.GraphIndex = graphIndex;
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


    /// <summary>
    /// Locally remove a flag and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <param name="flag">Flag in <see cref="NavigationFlags"/> to remove.</param>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="MemberAccessException">Failed to fetch <see cref="NavigationFlags"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="flag"/> is not found in <see cref="NavigationFlags"/>.</exception>
    public static void RemoveFlagLocal(Flag flag)
    {
        ThreadUtil.assertIsGameThread();

        int index = flag.GetIndex();
        if (index < 0)
            throw new ArgumentException("Navigation flag is not present in LevelNavigation list.", nameof(flag));

        RemoveFlagLocal((byte)index);
    }

    /// <summary>
    /// Remove a flag and call the necessary events.
    /// </summary>
    /// <remarks>Replicates to remote.</remarks>
    /// <param name="flag">Flag in <see cref="NavigationFlags"/> to remove.</param>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="NoPermissionsException">Missing client-side permission for <see cref="VanillaPermissions.EditNavigation"/>.</exception>
    /// <exception cref="MemberAccessException">Failed to fetch <see cref="NavigationFlags"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="flag"/> is not found in <see cref="NavigationFlags"/>.</exception>
    public static void RemoveFlag(Flag flag)
    {
        ThreadUtil.assertIsGameThread();

        int index = flag.GetIndex();
        if (index < 0)
            throw new ArgumentException("Navigation flag is not present in LevelNavigation list.", nameof(flag));

        RemoveFlag((byte)index);
    }

    /// <summary>
    /// Locally remove a flag and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <param name="nav">Flag index in <see cref="NavigationFlags"/>.</param>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="MemberAccessException">Failed to fetch <see cref="NavigationFlags"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">No flag found at index <paramref name="nav"/>.</exception>
    public static void RemoveFlagLocal(byte nav)
    {
        ThreadUtil.assertIsGameThread();

        if (GetNavigationFlags == null)
            throw new MemberAccessException("Unable to find field: LevelNavigation.flags.");

        List<Flag> flags = GetNavigationFlags();

        if (nav >= flags.Count)
            throw new ArgumentOutOfRangeException(nameof(nav), $"Navigation flag #{nav} does not exist.");

        Flag flag = flags[nav];

        LevelNavigation.removeFlag(flag.model);

        EventOnFlagRemoved.TryInvoke(flag, nav);
        for (int i = nav; i < flags.Count; ++i)
            EventOnFlagIndexUpdated.TryInvoke(flags[i], (byte)(i + 1), (byte)i);
    }

    /// <summary>
    /// Locally move a flag and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <param name="flag">Flag in <see cref="NavigationFlags"/> to remove.</param>
    /// <param name="position">New position to move the flag to.</param>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="MemberAccessException">Failed to fetch <see cref="NavigationFlags"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="flag"/> is not found in <see cref="NavigationFlags"/>.</exception>
    public static void SetFlagPositionLocal(Flag flag, Vector3 position)
    {
        ThreadUtil.assertIsGameThread();

        int index = flag.GetIndex();
        if (index < 0)
            throw new ArgumentException("Navigation flag is not present in LevelNavigation list.", nameof(flag));

        SetFlagPositionLocal((byte)index, position);
    }

    /// <summary>
    /// Move a flag and call the necessary events.
    /// </summary>
    /// <remarks>Replicates to remote.</remarks>
    /// <param name="flag">Flag in <see cref="NavigationFlags"/> to remove.</param>
    /// <param name="position">New position to move the flag to.</param>
    /// <exception cref="NoPermissionsException">Missing client-side permission for <see cref="VanillaPermissions.EditNavigation"/>.</exception>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="MemberAccessException">Failed to fetch <see cref="NavigationFlags"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="flag"/> is not found in <see cref="NavigationFlags"/>.</exception>
    public static void SetFlagPosition(Flag flag, Vector3 position)
    {
        ThreadUtil.assertIsGameThread();

        int index = flag.GetIndex();
        if (index < 0)
            throw new ArgumentException("Navigation flag is not present in LevelNavigation list.", nameof(flag));

        SetFlagPosition((byte)index, position);
    }

    /// <summary>
    /// Locally move a flag and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <param name="nav">Flag index in <see cref="NavigationFlags"/>.</param>
    /// <param name="position">New position to move the flag to.</param>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="MemberAccessException">Failed to fetch <see cref="NavigationFlags"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">No flag found at index <paramref name="nav"/>.</exception>
    public static void SetFlagPositionLocal(byte nav, Vector3 position)
    {
        ThreadUtil.assertIsGameThread();

        if (GetNavigationFlags == null)
            throw new MemberAccessException("Unable to find field: LevelNavigation.flags.");

        List<Flag> flags = GetNavigationFlags();

        if (nav >= flags.Count)
            throw new ArgumentOutOfRangeException(nameof(nav), $"Navigation flag #{nav} does not exist.");

        Flag flag = flags[nav];

        Vector3 oldPosition = flag.point;

        flag.move(position);

        EventOnFlagMoved.TryInvoke(flag, nav, oldPosition, flag.point);
    }

    /// <summary>
    /// Locally add a flag and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <param name="position">New position to move the flag to.</param>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="MemberAccessException">Failed to fetch <see cref="NavigationFlags"/>.</exception>
    /// <exception cref="InvalidOperationException">There are already too many flags in the level.</exception>
    public static Flag AddFlagLocal(Vector3 position)
    {
        ThreadUtil.assertIsGameThread();

        if (GetNavigationFlags == null)
            throw new MemberAccessException("Unable to find field: LevelNavigation.flags.");

        List<Flag> flags = GetNavigationFlags();

        if (flags.Count >= byte.MaxValue - 1)
            throw new InvalidOperationException($"There are already too many flags in the level ({byte.MaxValue - 1}).");

        Transform flagTransform = LevelNavigation.addFlag(position);
        Flag flag = LevelNavigation.getFlag(flagTransform);

        byte nav = (byte)flags.LastIndexOf(flag);

        EventOnFlagAdded.TryInvoke(flag, nav);

        Logger.LogDebug($"[{Source}] Flag added: {nav.Format()}.");

        return flag;
    }

    /// <summary>
    /// Locally resize a flag and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <param name="flag">Flag in <see cref="NavigationFlags"/> to resize.</param>
    /// <param name="size">Size of the bounds of the flag.</param>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="MemberAccessException">Failed to fetch <see cref="NavigationFlags"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="flag"/> is not found in <see cref="NavigationFlags"/>.</exception>
    /// <returns><see langword="true"/> if <paramref name="size"/> was different than the current value and the value was changed, otherwise <see langword="false"/>.</returns>
    public static bool SetFlagSizeLocal(Flag flag, Vector2 size)
    {
        ThreadUtil.assertIsGameThread();

        int index = flag.GetIndex();
        if (index < 0)
            throw new ArgumentException("Navigation flag is not present in LevelNavigation list.", nameof(flag));

        return SetFlagSizeLocal((byte)index, size);
    }

    /// <summary>
    /// Resize a flag and call the necessary events.
    /// </summary>
    /// <remarks>Replicates to remote.</remarks>
    /// <param name="flag">Flag in <see cref="NavigationFlags"/> to resize.</param>
    /// <param name="size">Size of the bounds of the flag.</param>
    /// <exception cref="NoPermissionsException">Missing client-side permission for <see cref="VanillaPermissions.EditNavigation"/>.</exception>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="MemberAccessException">Failed to fetch <see cref="NavigationFlags"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="flag"/> is not found in <see cref="NavigationFlags"/>.</exception>
    /// <returns><see langword="true"/> if <paramref name="size"/> was different than the current value and the value was changed, otherwise <see langword="false"/>.</returns>
    public static bool SetFlagSize(Flag flag, Vector2 size)
    {
        ThreadUtil.assertIsGameThread();

        int index = flag.GetIndex();
        if (index < 0)
            throw new ArgumentException("Navigation flag is not present in LevelNavigation list.", nameof(flag));

        return SetFlagSize((byte)index, size);
    }

    /// <summary>
    /// Locally resize a flag and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <param name="nav">Flag index in <see cref="NavigationFlags"/>.</param>
    /// <param name="size">Size of the bounds of the flag.</param>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="MemberAccessException">Failed to fetch <see cref="NavigationFlags"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">No flag found at index <paramref name="nav"/>.</exception>
    /// <returns><see langword="true"/> if <paramref name="size"/> was different than the current value and the value was changed, otherwise <see langword="false"/>.</returns>
    public static bool SetFlagSizeLocal(byte nav, Vector2 size)
    {
        ThreadUtil.assertIsGameThread();

        if (GetNavigationFlags == null)
            throw new MemberAccessException("Unable to find field: LevelNavigation.flags.");

        List<Flag> flags = GetNavigationFlags();

        if (nav >= flags.Count)
            throw new ArgumentOutOfRangeException(nameof(nav), $"Navigation flag #{nav} does not exist.");

        Flag flag = flags[nav];

        if (flag.width == size.x && flag.height == size.y)
            return false;

        Vector2 oldSize = new Vector2(flag.width, flag.height);

        flag.width = size.x;
        flag.height = size.y;
        flag.buildMesh();

#if CLIENT
        UpdateUIWhenSelected(flag);
#endif

        EventOnFlagSizeUpdated.TryInvoke(flag, nav, oldSize, size);
        return true;
    }

    /// <summary>
    /// Locally set the difficulty asset for a flag and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <param name="flag">Flag in <see cref="NavigationFlags"/> to resize.</param>
    /// <param name="difficulty">Specifies zombie difficulty properties for zombies spawned in <paramref name="flag"/>.</param>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="MemberAccessException">Failed to fetch <see cref="NavigationFlags"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="flag"/> is not found in <see cref="NavigationFlags"/>.</exception>
    /// <returns><see langword="true"/> if <paramref name="difficulty"/> was different than the current value and the value was changed, otherwise <see langword="false"/>.</returns>
    public static bool SetFlagDifficultyLocal(Flag flag, AssetReference<ZombieDifficultyAsset> difficulty)
    {
        ThreadUtil.assertIsGameThread();

        int index = flag.GetIndex();
        if (index < 0)
            throw new ArgumentException("Navigation flag is not present in LevelNavigation list.", nameof(flag));

        return SetFlagDifficultyLocal((byte)index, difficulty);
    }

    /// <summary>
    /// Set the difficulty asset for a flag and call the necessary events.
    /// </summary>
    /// <remarks>Replicates to remote.</remarks>
    /// <param name="flag">Flag in <see cref="NavigationFlags"/> to resize.</param>
    /// <param name="difficulty">Specifies zombie difficulty properties for zombies spawned in <paramref name="flag"/>.</param>
    /// <exception cref="NoPermissionsException">Missing client-side permission for <see cref="VanillaPermissions.EditNavigation"/>.</exception>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="MemberAccessException">Failed to fetch <see cref="NavigationFlags"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="flag"/> is not found in <see cref="NavigationFlags"/>.</exception>
    /// <returns><see langword="true"/> if <paramref name="difficulty"/> was different than the current value and the value was changed, otherwise <see langword="false"/>.</returns>
    public static bool SetFlagDifficulty(Flag flag, AssetReference<ZombieDifficultyAsset> difficulty)
    {
        ThreadUtil.assertIsGameThread();

        int index = flag.GetIndex();
        if (index < 0)
            throw new ArgumentException("Navigation flag is not present in LevelNavigation list.", nameof(flag));

        return SetFlagDifficulty((byte)index, difficulty);
    }

    /// <summary>
    /// Locally resize a flag and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <param name="nav">Flag index in <see cref="NavigationFlags"/>.</param>
    /// <param name="difficulty">Specifies zombie difficulty properties for zombies spawned in <paramref name="flag"/>.</param>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="MemberAccessException">Failed to fetch <see cref="NavigationFlags"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">No flag found at index <paramref name="nav"/>.</exception>
    /// <returns><see langword="true"/> if <paramref name="difficulty"/> was different than the current value and the value was changed, otherwise <see langword="false"/>.</returns>
    public static bool SetFlagDifficultyLocal(byte nav, AssetReference<ZombieDifficultyAsset> difficulty)
    {
        ThreadUtil.assertIsGameThread();

        if (GetNavigationFlags == null)
            throw new MemberAccessException("Unable to find field: LevelNavigation.flags.");

        List<Flag> flags = GetNavigationFlags();

        if (nav >= flags.Count)
            throw new ArgumentOutOfRangeException(nameof(nav), $"Navigation flag #{nav} does not exist.");

        Flag flag = flags[nav];

        AssetReference<ZombieDifficultyAsset> oldDifficulty = flag.data.difficulty;

        if (oldDifficulty == difficulty)
            return false;

        flag.data.difficultyGUID = difficulty.GUID.ToString("N");

#if CLIENT
        UpdateUIWhenSelected(flag);
#endif

        EventOnFlagDifficultyUpdated.TryInvoke(flag, nav, oldDifficulty, difficulty);
        return true;
    }

    /// <summary>
    /// Locally set the maximum zombie count for a flag and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <param name="flag">Flag in <see cref="NavigationFlags"/> to resize.</param>
    /// <param name="maximumZombies">Maximum number of zombies that can be alive in <paramref name="flag"/> at once. Default is 64.</param>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="MemberAccessException">Failed to fetch <see cref="NavigationFlags"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="flag"/> is not found in <see cref="NavigationFlags"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maximumZombies"/> is not within <c>byte</c> range.</exception>
    /// <returns><see langword="true"/> if <paramref name="maximumZombies"/> was different than the current value and the value was changed, otherwise <see langword="false"/>.</returns>
    public static bool SetFlagMaximumZombiesLocal(Flag flag, int maximumZombies)
    {
        ThreadUtil.assertIsGameThread();

        int index = flag.GetIndex();
        if (index < 0)
            throw new ArgumentException("Navigation flag is not present in LevelNavigation list.", nameof(flag));

        return SetFlagMaximumZombiesLocal((byte)index, maximumZombies);
    }

    /// <summary>
    /// Set the maximum zombie count for a flag and call the necessary events.
    /// </summary>
    /// <remarks>Replicates to remote.</remarks>
    /// <param name="flag">Flag in <see cref="NavigationFlags"/> to resize.</param>
    /// <param name="maximumZombies">Maximum number of zombies that can be alive in <paramref name="flag"/> at once. Default is 64.</param>
    /// <exception cref="NoPermissionsException">Missing client-side permission for <see cref="VanillaPermissions.EditNavigation"/>.</exception>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="MemberAccessException">Failed to fetch <see cref="NavigationFlags"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="flag"/> is not found in <see cref="NavigationFlags"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maximumZombies"/> is not within <c>byte</c> range.</exception>
    /// <returns><see langword="true"/> if <paramref name="maximumZombies"/> was different than the current value and the value was changed, otherwise <see langword="false"/>.</returns>
    public static bool SetFlagMaximumZombies(Flag flag, int maximumZombies)
    {
        ThreadUtil.assertIsGameThread();

        int index = flag.GetIndex();
        if (index < 0)
            throw new ArgumentException("Navigation flag is not present in LevelNavigation list.", nameof(flag));

        return SetFlagMaximumZombies((byte)index, maximumZombies);
    }

    /// <summary>
    /// Locally set the maximum zombie count for a flag and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <param name="nav">Flag index in <see cref="NavigationFlags"/>.</param>
    /// <param name="maximumZombies">Maximum number of zombies that can be alive in <paramref name="flag"/> at once. Default is 64.</param>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="MemberAccessException">Failed to fetch <see cref="NavigationFlags"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">No flag found at index <paramref name="nav"/> or <paramref name="maximumZombies"/> is not within <c>byte</c> range.</exception>
    /// <returns><see langword="true"/> if <paramref name="maximumZombies"/> was different than the current value and the value was changed, otherwise <see langword="false"/>.</returns>
    public static bool SetFlagMaximumZombiesLocal(byte nav, int maximumZombies)
    {
        ThreadUtil.assertIsGameThread();

        if (GetNavigationFlags == null)
            throw new MemberAccessException("Unable to find field: LevelNavigation.flags.");

        List<Flag> flags = GetNavigationFlags();

        if (nav >= flags.Count)
            throw new ArgumentOutOfRangeException(nameof(nav), $"Navigation flag #{nav} does not exist.");

        if (maximumZombies is < 0 or > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(maximumZombies), $"Maximum zombie count must be between 0 and {byte.MaxValue} inclusively.");

        Flag flag = flags[nav];

        byte oldMaxZombies = flag.data.maxZombies;

        if (oldMaxZombies == maximumZombies)
            return false;

        flag.data.maxZombies = (byte)maximumZombies;

#if CLIENT
        UpdateUIWhenSelected(flag);
#endif

        EventOnFlagMaximumZombiesUpdated.TryInvoke(flag, nav, oldMaxZombies, maximumZombies);
        return true;
    }

    /// <summary>
    /// Locally set the maximum boss zombie count for a flag and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <param name="flag">Flag in <see cref="NavigationFlags"/> to resize.</param>
    /// <param name="maximumBossZombies">Maximum number of boss zombies that can be alive in <paramref name="flag"/> at once (or -1 for infinite). Default is -1.</param>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="MemberAccessException">Failed to fetch <see cref="NavigationFlags"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="flag"/> is not found in <see cref="NavigationFlags"/>.</exception>
    /// <returns><see langword="true"/> if <paramref name="maximumBossZombies"/> was different than the current value and the value was changed, otherwise <see langword="false"/>.</returns>
    public static bool SetFlagMaximumBossZombiesLocal(Flag flag, int maximumBossZombies)
    {
        ThreadUtil.assertIsGameThread();

        int index = flag.GetIndex();
        if (index < 0)
            throw new ArgumentException("Navigation flag is not present in LevelNavigation list.", nameof(flag));

        return SetFlagMaximumBossZombiesLocal((byte)index, maximumBossZombies);
    }

    /// <summary>
    /// Set the maximum boss zombie count for a flag and call the necessary events.
    /// </summary>
    /// <remarks>Replicates to remote.</remarks>
    /// <param name="flag">Flag in <see cref="NavigationFlags"/> to resize.</param>
    /// <param name="maximumBossZombies">Maximum number of boss zombies that can be alive in <paramref name="flag"/> at once (or -1 for infinite). Default is -1.</param>
    /// <exception cref="NoPermissionsException">Missing client-side permission for <see cref="VanillaPermissions.EditNavigation"/>.</exception>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="MemberAccessException">Failed to fetch <see cref="NavigationFlags"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="flag"/> is not found in <see cref="NavigationFlags"/>.</exception>
    /// <returns><see langword="true"/> if <paramref name="maximumBossZombies"/> was different than the current value and the value was changed, otherwise <see langword="false"/>.</returns>
    public static bool SetFlagMaximumBossZombies(Flag flag, int maximumBossZombies)
    {
        ThreadUtil.assertIsGameThread();

        int index = flag.GetIndex();
        if (index < 0)
            throw new ArgumentException("Navigation flag is not present in LevelNavigation list.", nameof(flag));

        return SetFlagMaximumBossZombies((byte)index, maximumBossZombies);
    }

    /// <summary>
    /// Locally set the maximum boss zombie count for a flag and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <param name="nav">Flag index in <see cref="NavigationFlags"/>.</param>
    /// <param name="maximumBossZombies">Maximum number of boss zombies that can be alive in <paramref name="flag"/> at once (or -1 for infinite). Default is -1.</param>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="MemberAccessException">Failed to fetch <see cref="NavigationFlags"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">No flag found at index <paramref name="nav"/>.</exception>
    /// <returns><see langword="true"/> if <paramref name="maximumBossZombies"/> was different than the current value and the value was changed, otherwise <see langword="false"/>.</returns>
    public static bool SetFlagMaximumBossZombiesLocal(byte nav, int maximumBossZombies)
    {
        ThreadUtil.assertIsGameThread();

        if (GetNavigationFlags == null)
            throw new MemberAccessException("Unable to find field: LevelNavigation.flags.");

        List<Flag> flags = GetNavigationFlags();

        if (nav >= flags.Count)
            throw new ArgumentOutOfRangeException(nameof(nav), $"Navigation flag #{nav} does not exist.");

        if (maximumBossZombies < -1)
            maximumBossZombies = -1;

        Flag flag = flags[nav];

        int oldMaxBossZombies = flag.data.maxBossZombies;

        if (oldMaxBossZombies == maximumBossZombies)
            return false;

        flag.data.maxBossZombies = maximumBossZombies;

#if CLIENT
        UpdateUIWhenSelected(flag);
#endif

        EventOnFlagMaximumBossZombiesUpdated.TryInvoke(flag, nav, oldMaxBossZombies, maximumBossZombies);
        return true;
    }

    /// <summary>
    /// Locally set if a flag should spawn zombies and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <param name="flag">Flag in <see cref="NavigationFlags"/> to resize.</param>
    /// <param name="shouldSpawnZombies">If zombies should spawn in <paramref name="flag"/>.</param>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="MemberAccessException">Failed to fetch <see cref="NavigationFlags"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="flag"/> is not found in <see cref="NavigationFlags"/>.</exception>
    /// <returns><see langword="true"/> if <paramref name="shouldSpawnZombies"/> was different than the current value and the value was changed, otherwise <see langword="false"/>.</returns>
    public static bool SetFlagShouldSpawnZombiesLocal(Flag flag, bool shouldSpawnZombies)
    {
        ThreadUtil.assertIsGameThread();

        int index = flag.GetIndex();
        if (index < 0)
            throw new ArgumentException("Navigation flag is not present in LevelNavigation list.", nameof(flag));

        return SetFlagShouldSpawnZombiesLocal((byte)index, shouldSpawnZombies);
    }

    /// <summary>
    /// Locally set if a flag should spawn zombies and call the necessary events.
    /// </summary>
    /// <remarks>Replicates to remote.</remarks>
    /// <param name="flag">Flag in <see cref="NavigationFlags"/> to resize.</param>
    /// <param name="shouldSpawnZombies">If zombies should spawn in <paramref name="flag"/>.</param>
    /// <exception cref="NoPermissionsException">Missing client-side permission for <see cref="VanillaPermissions.EditNavigation"/>.</exception>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="MemberAccessException">Failed to fetch <see cref="NavigationFlags"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="flag"/> is not found in <see cref="NavigationFlags"/>.</exception>
    /// <returns><see langword="true"/> if <paramref name="shouldSpawnZombies"/> was different than the current value and the value was changed, otherwise <see langword="false"/>.</returns>
    public static bool SetFlagShouldSpawnZombies(Flag flag, bool shouldSpawnZombies)
    {
        ThreadUtil.assertIsGameThread();

        int index = flag.GetIndex();
        if (index < 0)
            throw new ArgumentException("Navigation flag is not present in LevelNavigation list.", nameof(flag));

        return SetFlagShouldSpawnZombies((byte)index, shouldSpawnZombies);
    }

    /// <summary>
    /// Locally set if a flag should spawn zombies and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <param name="nav">Flag index in <see cref="NavigationFlags"/>.</param>
    /// <param name="shouldSpawnZombies">If zombies should spawn in <paramref name="flag"/>.</param>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="MemberAccessException">Failed to fetch <see cref="NavigationFlags"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">No flag found at index <paramref name="nav"/>.</exception>
    /// <returns><see langword="true"/> if <paramref name="shouldSpawnZombies"/> was different than the current value and the value was changed, otherwise <see langword="false"/>.</returns>
    public static bool SetFlagShouldSpawnZombiesLocal(byte nav, bool shouldSpawnZombies)
    {
        ThreadUtil.assertIsGameThread();

        if (GetNavigationFlags == null)
            throw new MemberAccessException("Unable to find field: LevelNavigation.flags.");

        List<Flag> flags = GetNavigationFlags();

        if (nav >= flags.Count)
            throw new ArgumentOutOfRangeException(nameof(nav), $"Navigation flag #{nav} does not exist.");

        Flag flag = flags[nav];
        
        if (flag.data.spawnZombies == shouldSpawnZombies)
            return false;

        flag.data.spawnZombies = shouldSpawnZombies;

#if CLIENT
        UpdateUIWhenSelected(flag);
#endif

        EventOnFlagShouldSpawnZombiesUpdated.TryInvoke(flag, nav, shouldSpawnZombies);
        return true;
    }

    /// <summary>
    /// Locally set if players in a flag should agro zombies from an infinite distance and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <param name="flag">Flag in <see cref="NavigationFlags"/> to resize.</param>
    /// <param name="infiniteAgroDistance">If zombies should be able to see players from any distance within <paramref name="flag"/>.</param>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="MemberAccessException">Failed to fetch <see cref="NavigationFlags"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="flag"/> is not found in <see cref="NavigationFlags"/>.</exception>
    /// <returns><see langword="true"/> if <paramref name="infiniteAgroDistance"/> was different than the current value and the value was changed, otherwise <see langword="false"/>.</returns>
    public static bool SetFlagInfiniteAgroDistanceLocal(Flag flag, bool infiniteAgroDistance)
    {
        ThreadUtil.assertIsGameThread();

        int index = flag.GetIndex();
        if (index < 0)
            throw new ArgumentException("Navigation flag is not present in LevelNavigation list.", nameof(flag));

        return SetFlagInfiniteAgroDistanceLocal((byte)index, infiniteAgroDistance);
    }

    /// <summary>
    /// Set if players in a flag should agro zombies from an infinite distance and call the necessary events.
    /// </summary>
    /// <remarks>Replicates to remote.</remarks>
    /// <param name="flag">Flag in <see cref="NavigationFlags"/> to resize.</param>
    /// <param name="infiniteAgroDistance">If zombies should be able to see players from any distance within <paramref name="flag"/>.</param>
    /// <exception cref="NoPermissionsException">Missing client-side permission for <see cref="VanillaPermissions.EditNavigation"/>.</exception>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="MemberAccessException">Failed to fetch <see cref="NavigationFlags"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="flag"/> is not found in <see cref="NavigationFlags"/>.</exception>
    /// <returns><see langword="true"/> if <paramref name="infiniteAgroDistance"/> was different than the current value and the value was changed, otherwise <see langword="false"/>.</returns>
    public static bool SetFlagInfiniteAgroDistance(Flag flag, bool infiniteAgroDistance)
    {
        ThreadUtil.assertIsGameThread();

        int index = flag.GetIndex();
        if (index < 0)
            throw new ArgumentException("Navigation flag is not present in LevelNavigation list.", nameof(flag));

        return SetFlagInfiniteAgroDistance((byte)index, infiniteAgroDistance);
    }

    /// <summary>
    /// Locally set if players in a flag should agro zombies from an infinite distance and call the necessary events.
    /// </summary>
    /// <remarks>Non-replicating.</remarks>
    /// <param name="nav">Flag index in <see cref="NavigationFlags"/>.</param>
    /// <param name="infiniteAgroDistance">If zombies should be able to see players from any distance within <paramref name="flag"/>.</param>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="MemberAccessException">Failed to fetch <see cref="NavigationFlags"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">No flag found at index <paramref name="nav"/>.</exception>
    /// <returns><see langword="true"/> if <paramref name="infiniteAgroDistance"/> was different than the current value and the value was changed, otherwise <see langword="false"/>.</returns>
    public static bool SetFlagInfiniteAgroDistanceLocal(byte nav, bool infiniteAgroDistance)
    {
        ThreadUtil.assertIsGameThread();

        if (GetNavigationFlags == null)
            throw new MemberAccessException("Unable to find field: LevelNavigation.flags.");

        List<Flag> flags = GetNavigationFlags();

        if (nav >= flags.Count)
            throw new ArgumentOutOfRangeException(nameof(nav), $"Navigation flag #{nav} does not exist.");

        Flag flag = flags[nav];
        
        if (flag.data.hyperAgro == infiniteAgroDistance)
            return false;

        flag.data.hyperAgro = infiniteAgroDistance;

#if CLIENT
        UpdateUIWhenSelected(flag);
#endif

        EventOnFlagInfiniteAgroDistanceUpdated.TryInvoke(flag, nav, infiniteAgroDistance);
        return true;
    }

    /// <summary>
    /// Remove a flag and call the necessary events.
    /// </summary>
    /// <remarks>Replicates to remote.</remarks>
    /// <param name="nav">Flag index in <see cref="NavigationFlags"/>.</param>
    /// <exception cref="NoPermissionsException">Missing client-side permission for <see cref="VanillaPermissions.EditNavigation"/>.</exception>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="MemberAccessException">Failed to fetch <see cref="NavigationFlags"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">No flag found at index <paramref name="nav"/>.</exception>
    public static void RemoveFlag(byte nav)
    {
        ThreadUtil.assertIsGameThread();

        if (GetNavigationFlags == null)
            throw new MemberAccessException("Unable to find field: LevelNavigation.flags.");

        List<Flag> flags = GetNavigationFlags();

        if (nav >= flags.Count)
            throw new ArgumentOutOfRangeException(nameof(nav), $"Navigation flag #{nav} does not exist.");
        
#if CLIENT
        CheckEditFlags();
        Vector3 oldPosition = flags[nav].point;
#endif
        RemoveFlagLocal(nav);

        if (DevkitServerModule.IsEditing)
        {
            if (!NavigationNetIdDatabase.TryGetNavigationNetId(nav, out NetId netId))
            {
                Logger.LogWarning($"Failed to find NetId for flag {nav.Format()}. Did not replicate in RemoveFlag({nav.Format()}).", method: Source);
                return;
            }

#if CLIENT
            ClientEvents.InvokeOnDeleteNavigation(new DeleteNavigationProperties(netId, oldPosition, CachedTime.DeltaTime));
#else
            EditorActions.QueueServerAction(new DeleteNavigationAction
            {
                DeltaTime = CachedTime.DeltaTime,
                InstanceId = netId.id
            });
#endif
        }
    }

    /// <summary>
    /// Move a flag and call the necessary events.
    /// </summary>
    /// <remarks>Replicates to remote.</remarks>
    /// <param name="nav">Flag index in <see cref="NavigationFlags"/>.</param>
    /// <param name="position">New position to move the flag to.</param>
    /// <exception cref="NoPermissionsException">Missing client-side permission for <see cref="VanillaPermissions.EditNavigation"/>.</exception>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="MemberAccessException">Failed to fetch <see cref="NavigationFlags"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">No flag found at index <paramref name="nav"/>.</exception>
    public static void SetFlagPosition(byte nav, Vector3 position)
    {
        ThreadUtil.assertIsGameThread();

        if (GetNavigationFlags == null)
            throw new MemberAccessException("Unable to find field: LevelNavigation.flags.");

        List<Flag> flags = GetNavigationFlags();

        if (nav >= flags.Count)
            throw new ArgumentOutOfRangeException(nameof(nav), $"Navigation flag #{nav} does not exist.");

#if CLIENT
        CheckEditFlags();
        Vector3 oldPosition = flags[nav].point;
#endif
        SetFlagPositionLocal(nav, position);

        if (DevkitServerModule.IsEditing)
        {
            if (!NavigationNetIdDatabase.TryGetNavigationNetId(nav, out NetId netId))
            {
                Logger.LogWarning($"Failed to find NetId for flag {nav.Format()}. Did not replicate in SetFlagPosition({nav.Format()}, {position.Format()}).", method: Source);
                return;
            }

#if CLIENT
            ClientEvents.InvokeOnMoveNavigation(new MoveNavigationProperties(netId, position, oldPosition, CachedTime.DeltaTime));
#else
            EditorActions.QueueServerAction(new MoveNavigationAction
            {
                DeltaTime = CachedTime.DeltaTime,
                InstanceId = netId.id,
                Position = position
            });
#endif
        }
    }

    /// <summary>
    /// Resize a flag and call the necessary events.
    /// </summary>
    /// <remarks>Replicates to remote.</remarks>
    /// <param name="nav">Flag index in <see cref="NavigationFlags"/>.</param>
    /// <param name="size">Size of the bounds of the flag.</param>
    /// <exception cref="NoPermissionsException">Missing client-side permission for <see cref="VanillaPermissions.EditNavigation"/>.</exception>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="MemberAccessException">Failed to fetch <see cref="NavigationFlags"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">No flag found at index <paramref name="nav"/>.</exception>
    /// <returns><see langword="true"/> if <paramref name="size"/> was different than the current value and the value was changed, otherwise <see langword="false"/>.</returns>
    public static bool SetFlagSize(byte nav, Vector2 size)
    {
        ThreadUtil.assertIsGameThread();

        if (GetNavigationFlags == null)
            throw new MemberAccessException("Unable to find field: LevelNavigation.flags.");

        List<Flag> flags = GetNavigationFlags();

        if (nav >= flags.Count)
            throw new ArgumentOutOfRangeException(nameof(nav), $"Navigation flag #{nav} does not exist.");

#if CLIENT
        CheckEditFlags();
        Flag flag = flags[nav];
        Vector2 oldSize = new Vector2(flag.width, flag.height);
#endif
        if (!SetFlagSizeLocal(nav, size))
            return false;

        if (DevkitServerModule.IsEditing)
        {
            if (!NavigationNetIdDatabase.TryGetNavigationNetId(nav, out NetId netId))
            {
                Logger.LogWarning($"Failed to find NetId for flag {nav.Format()}. Did not replicate in SetFlagSize({nav.Format()}, {size.Format()}).", method: Source);
                return true;
            }

#if CLIENT
            ClientEvents.InvokeOnSetNavigationSize(new SetNavigationSizeProperties(netId, size, oldSize, CachedTime.DeltaTime));
#else
            EditorActions.QueueServerAction(new SetNavigationSizeAction
            {
                DeltaTime = CachedTime.DeltaTime,
                InstanceId = netId.id,
                Size = size
            });
#endif
        }

        return true;
    }

    /// <summary>
    /// Set the difficulty asset for a flag and call the necessary events.
    /// </summary>
    /// <remarks>Replicates to remote.</remarks>
    /// <param name="nav">Flag index in <see cref="NavigationFlags"/>.</param>
    /// <param name="difficulty">Specifies zombie difficulty properties for zombies spawned in <paramref name="flag"/>.</param>
    /// <exception cref="NoPermissionsException">Missing client-side permission for <see cref="VanillaPermissions.EditNavigation"/>.</exception>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="MemberAccessException">Failed to fetch <see cref="NavigationFlags"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">No flag found at index <paramref name="nav"/>.</exception>
    /// <returns><see langword="true"/> if <paramref name="difficulty"/> was different than the current value and the value was changed, otherwise <see langword="false"/>.</returns>
    public static bool SetFlagDifficulty(byte nav, AssetReference<ZombieDifficultyAsset> difficulty)
    {
        ThreadUtil.assertIsGameThread();

        if (GetNavigationFlags == null)
            throw new MemberAccessException("Unable to find field: LevelNavigation.flags.");

        List<Flag> flags = GetNavigationFlags();

        if (nav >= flags.Count)
            throw new ArgumentOutOfRangeException(nameof(nav), $"Navigation flag #{nav} does not exist.");

#if CLIENT
        CheckEditFlags();
        AssetReference<ZombieDifficultyAsset> oldDifficulty = flags[nav].data.difficulty;
#endif
        if (!SetFlagDifficultyLocal(nav, difficulty))
            return false;

        if (DevkitServerModule.IsEditing)
        {
            if (!NavigationNetIdDatabase.TryGetNavigationNetId(nav, out NetId netId))
            {
                Logger.LogWarning($"Failed to find NetId for flag {nav.Format()}. Did not replicate in SetFlagDifficulty({nav.Format()}, {difficulty.Format()}).", method: Source);
                return true;
            }

#if CLIENT
            ClientEvents.InvokeOnSetNavigationDifficulty(new SetNavigationDifficultyProperties(netId, difficulty, oldDifficulty, CachedTime.DeltaTime));
#else
            EditorActions.QueueServerAction(new SetNavigationDifficultyAction
            {
                DeltaTime = CachedTime.DeltaTime,
                InstanceId = netId.id,
                Difficulty = difficulty
            });
#endif
        }

        return true;
    }

    /// <summary>
    /// Set the maximum zombie count for a flag and call the necessary events.
    /// </summary>
    /// <remarks>Replicates to remote.</remarks>
    /// <param name="nav">Flag index in <see cref="NavigationFlags"/>.</param>
    /// <param name="maximumZombies">Maximum number of zombies that can be alive in <paramref name="flag"/> at once. Default is 64.</param>
    /// <exception cref="NoPermissionsException">Missing client-side permission for <see cref="VanillaPermissions.EditNavigation"/>.</exception>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="MemberAccessException">Failed to fetch <see cref="NavigationFlags"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="flag"/> is not found in <see cref="NavigationFlags"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">No flag found at index <paramref name="nav"/> or <paramref name="maximumZombies"/> is not within <c>byte</c> range.</exception>
    /// <returns><see langword="true"/> if <paramref name="maximumZombies"/> was different than the current value and the value was changed, otherwise <see langword="false"/>.</returns>
    public static bool SetFlagMaximumZombies(byte nav, int maximumZombies)
    {
        ThreadUtil.assertIsGameThread();

        if (GetNavigationFlags == null)
            throw new MemberAccessException("Unable to find field: LevelNavigation.flags.");

        List<Flag> flags = GetNavigationFlags();

        if (nav >= flags.Count)
            throw new ArgumentOutOfRangeException(nameof(nav), $"Navigation flag #{nav} does not exist.");

        if (maximumZombies is < 0 or > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(maximumZombies), $"Maximum zombie count must be between 0 and {byte.MaxValue} inclusively.");

#if CLIENT
        CheckEditFlags();
        byte oldMaxZombies = flags[nav].data.maxZombies;
#endif
        if (!SetFlagMaximumZombiesLocal(nav, maximumZombies))
            return false;

        if (DevkitServerModule.IsEditing)
        {
            if (!NavigationNetIdDatabase.TryGetNavigationNetId(nav, out NetId netId))
            {
                Logger.LogWarning($"Failed to find NetId for flag {nav.Format()}. Did not replicate in SetFlagMaximumZombies({nav.Format()}, {maximumZombies.Format()}).", method: Source);
                return true;
            }

#if CLIENT
            ClientEvents.InvokeOnSetNavigationMaximumZombies(new SetNavigationMaximumZombiesProperties(netId, (byte)maximumZombies, oldMaxZombies, CachedTime.DeltaTime));
#else
            EditorActions.QueueServerAction(new SetNavigationMaximumZombiesAction
            {
                DeltaTime = CachedTime.DeltaTime,
                InstanceId = netId.id,
                MaximumZombies = (byte)maximumZombies
            });
#endif
        }

        return true;
    }

    /// <summary>
    /// Set the maximum boss zombie count for a flag and call the necessary events.
    /// </summary>
    /// <remarks>Replicates to remote.</remarks>
    /// <param name="nav">Flag index in <see cref="NavigationFlags"/>.</param>
    /// <param name="maximumBossZombies">Maximum number of boss zombies that can be alive in <paramref name="flag"/> at once (or -1 for infinite). Default is -1.</param>
    /// <exception cref="NoPermissionsException">Missing client-side permission for <see cref="VanillaPermissions.EditNavigation"/>.</exception>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="MemberAccessException">Failed to fetch <see cref="NavigationFlags"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">No flag found at index <paramref name="nav"/>.</exception>
    /// <returns><see langword="true"/> if <paramref name="maximumBossZombies"/> was different than the current value and the value was changed, otherwise <see langword="false"/>.</returns>
    public static bool SetFlagMaximumBossZombies(byte nav, int maximumBossZombies)
    {
        ThreadUtil.assertIsGameThread();

        if (GetNavigationFlags == null)
            throw new MemberAccessException("Unable to find field: LevelNavigation.flags.");

        List<Flag> flags = GetNavigationFlags();

        if (nav >= flags.Count)
            throw new ArgumentOutOfRangeException(nameof(nav), $"Navigation flag #{nav} does not exist.");

#if CLIENT
        CheckEditFlags();
        int oldMaxBossZombies = flags[nav].data.maxBossZombies;
#endif
        if (!SetFlagMaximumBossZombiesLocal(nav, maximumBossZombies))
            return false;

        if (DevkitServerModule.IsEditing)
        {
            if (!NavigationNetIdDatabase.TryGetNavigationNetId(nav, out NetId netId))
            {
                Logger.LogWarning($"Failed to find NetId for flag {nav.Format()}. Did not replicate in SetFlagMaximumBossZombies({nav.Format()}, {maximumBossZombies.Format()}).", method: Source);
                return true;
            }

#if CLIENT
            ClientEvents.InvokeOnSetNavigationMaximumBossZombies(new SetNavigationMaximumBossZombiesProperties(netId, maximumBossZombies, oldMaxBossZombies, CachedTime.DeltaTime));
#else
            EditorActions.QueueServerAction(new SetNavigationMaximumBossZombiesAction
            {
                DeltaTime = CachedTime.DeltaTime,
                InstanceId = netId.id,
                MaximumBossZombies = maximumBossZombies
            });
#endif
        }

        return true;
    }

    /// <summary>
    /// Locally set if a flag should spawn zombies and call the necessary events.
    /// </summary>
    /// <remarks>Replicates to remote.</remarks>
    /// <param name="nav">Flag index in <see cref="NavigationFlags"/>.</param>
    /// <param name="shouldSpawnZombies">If zombies should spawn in <paramref name="flag"/>.</param>
    /// <exception cref="NoPermissionsException">Missing client-side permission for <see cref="VanillaPermissions.EditNavigation"/>.</exception>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="MemberAccessException">Failed to fetch <see cref="NavigationFlags"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">No flag found at index <paramref name="nav"/>.</exception>
    /// <returns><see langword="true"/> if <paramref name="shouldSpawnZombies"/> was different than the current value and the value was changed, otherwise <see langword="false"/>.</returns>
    public static bool SetFlagShouldSpawnZombies(byte nav, bool shouldSpawnZombies)
    {
        ThreadUtil.assertIsGameThread();

        if (GetNavigationFlags == null)
            throw new MemberAccessException("Unable to find field: LevelNavigation.flags.");

        List<Flag> flags = GetNavigationFlags();

        if (nav >= flags.Count)
            throw new ArgumentOutOfRangeException(nameof(nav), $"Navigation flag #{nav} does not exist.");

#if CLIENT
        CheckEditFlags();
#endif
        if (!SetFlagShouldSpawnZombiesLocal(nav, shouldSpawnZombies))
            return false;

        if (DevkitServerModule.IsEditing)
        {
            if (!NavigationNetIdDatabase.TryGetNavigationNetId(nav, out NetId netId))
            {
                Logger.LogWarning($"Failed to find NetId for flag {nav.Format()}. Did not replicate in SetFlagShouldSpawnZombies({nav.Format()}, {shouldSpawnZombies.Format()}).", method: Source);
                return true;
            }

#if CLIENT
            ClientEvents.InvokeOnSetNavigationShouldSpawnZombies(new SetNavigationShouldSpawnZombiesProperties(netId, shouldSpawnZombies, CachedTime.DeltaTime));
#else
            EditorActions.QueueServerAction(new SetNavigationShouldSpawnZombiesAction
            {
                DeltaTime = CachedTime.DeltaTime,
                InstanceId = netId.id,
                ShouldSpawnZombies = shouldSpawnZombies
            });
#endif
        }

        return true;
    }

    /// <summary>
    /// Set if players in a flag should agro zombies from an infinite distance and call the necessary events.
    /// </summary>
    /// <remarks>Replicates to remote.</remarks>
    /// <param name="nav">Flag index in <see cref="NavigationFlags"/>.</param>
    /// <param name="infiniteAgroDistance">If zombies should be able to see players from any distance within <paramref name="flag"/>.</param>
    /// <exception cref="NoPermissionsException">Missing client-side permission for <see cref="VanillaPermissions.EditNavigation"/>.</exception>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="MemberAccessException">Failed to fetch <see cref="NavigationFlags"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">No flag found at index <paramref name="nav"/>.</exception>
    /// <returns><see langword="true"/> if <paramref name="infiniteAgroDistance"/> was different than the current value and the value was changed, otherwise <see langword="false"/>.</returns>
    public static bool SetFlagInfiniteAgroDistance(byte nav, bool infiniteAgroDistance)
    {
        ThreadUtil.assertIsGameThread();

        if (GetNavigationFlags == null)
            throw new MemberAccessException("Unable to find field: LevelNavigation.flags.");

        List<Flag> flags = GetNavigationFlags();

        if (nav >= flags.Count)
            throw new ArgumentOutOfRangeException(nameof(nav), $"Navigation flag #{nav} does not exist.");

#if CLIENT
        CheckEditFlags();
#endif
        if (!SetFlagInfiniteAgroDistanceLocal(nav, infiniteAgroDistance))
            return false;

        if (DevkitServerModule.IsEditing)
        {
            if (!NavigationNetIdDatabase.TryGetNavigationNetId(nav, out NetId netId))
            {
                Logger.LogWarning($"Failed to find NetId for flag {nav.Format()}. Did not replicate in SetFlagInfiniteAgroDistance({nav.Format()}, {infiniteAgroDistance.Format()}).", method: Source);
                return true;
            }

#if CLIENT
            ClientEvents.InvokeOnSetNavigationInfiniteAgroDistance(new SetNavigationInfiniteAgroDistanceProperties(netId, infiniteAgroDistance, CachedTime.DeltaTime));
#else
            EditorActions.QueueServerAction(new SetNavigationInfiniteAgroDistanceAction
            {
                DeltaTime = CachedTime.DeltaTime,
                InstanceId = netId.id,
                InfiniteAgroDistance = infiniteAgroDistance
            });
#endif
        }

        return true;
    }

#if SERVER
    /// <summary>
    /// Locally add a flag and call the necessary events.
    /// </summary>
    /// <remarks>Replicates to clients.</remarks>
    /// <param name="position">New position to move the flag to.</param>
    /// <exception cref="NotSupportedException">Not on main thread.</exception>
    /// <exception cref="MemberAccessException">Failed to fetch <see cref="NavigationFlags"/>.</exception>
    /// <exception cref="InvalidOperationException">There are already too many flags in the level.</exception>
    /// <returns>The newly added <see cref="Flag"/>.</returns>
    public static Flag AddFlag(Vector3 position, EditorUser? owner = null)
    {
        ThreadUtil.assertIsGameThread();

        return AddFlag(position, owner == null ? MessageContext.Nil : MessageContext.CreateFromCaller(owner));
    }
    internal static Flag AddFlag(Vector3 position, MessageContext ctx)
    {
        if (GetNavigationFlags == null)
            throw new MemberAccessException("Unable to find field: LevelNavigation.flags.");

        List<Flag> flags = GetNavigationFlags();

        if (flags.Count >= byte.MaxValue - 1)
            throw new InvalidOperationException($"There are already too many flags in the level ({byte.MaxValue - 1}).");

        ulong owner = ctx.GetCaller() is { } user ? user.SteamId.m_SteamID : 0ul;

        Transform flagTransform = LevelNavigation.addFlag(position);
        Flag flag = LevelNavigation.getFlag(flagTransform);

        InitializeFlag(flag, out int flagIndex, out NetId netId);
        if (flagIndex < 0)
            return null!;

        byte nav = (byte)flagIndex;

        EventOnFlagAdded.TryInvoke(flag, nav);

        Logger.LogDebug($"[{Source}] Flag added: {nav.Format()}.");

        position = flag.point;

        Vector2 size = new Vector2(flag.width, flag.height);

        PooledTransportConnectionList list;
        if (!ctx.IsRequest)
            list = DevkitServerUtility.GetAllConnections();
        else
        {
            ctx.ReplyLayered(SendInstantiation, position, size, netId, owner, flag.data.hyperAgro, flag.data.spawnZombies, flag.data.maxZombies, flag.data.maxBossZombies, flag.data.difficulty.GUID);
            list = DevkitServerUtility.GetAllConnections(ctx.Connection);
        }

        SendInstantiation.Invoke(list, position, size, netId, owner, flag.data.hyperAgro, flag.data.spawnZombies, flag.data.maxZombies, flag.data.maxBossZombies, flag.data.difficulty.GUID);

        // todo SyncIfAuthority(nav);

        return flag;
    }
#endif

    internal static void InitializeFlag(Flag flag, out int flagIndex,
#if SERVER
        out
#endif
        NetId flagNetId)
    {
#if SERVER
        flagNetId = NetId.INVALID;
#endif
        flagIndex = flag == null ? -1 : flag.GetIndex();
        if (flagIndex != -1)
        {
#if SERVER
            flagNetId = NavigationNetIdDatabase.AddNavigation((byte)flagIndex);
#else
            NavigationNetIdDatabase.RegisterNavigation((byte)flagIndex, flagNetId);
#endif
            Logger.LogDebug($"[{Source}] Assigned flag NetId: {flagNetId.Format()}.");
            return;
        }

        Logger.LogWarning($"Did not find flag at {(flag == null ? ((object?)null).Format() : flag.point.Format())} in list.", method: Source);
    }
#if CLIENT
    private static void UpdateUIWhenSelected(Flag flag)
    {
        if (HoldUIUpdate || EditorNavigation.flag != flag || !EditorNavigation.isPathfinding || !EditorEnvironmentNavigationUI.active)
            return;

        EditorEnvironmentNavigationUI.updateSelection(flag);
    }
    internal static void CheckEditFlags()
    {
        if (DevkitServerModule.IsEditing && !VanillaPermissions.EditNavigation.Has() && !VanillaPermissions.AllNavigation.Has(false))
            throw new NoPermissionsException(VanillaPermissions.EditNavigation);
    }
#endif
}
