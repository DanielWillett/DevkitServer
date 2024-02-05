using DevkitServer.Configuration;
using DevkitServer.Multiplayer.Levels;
using DevkitServer.Multiplayer.Networking;
using DevkitServer.Players;
using DevkitServer.Util.Encoding;
using SDG.NetPak;
using System.Globalization;
#if CLIENT
using DevkitServer.API.UI.Extensions;
using DevkitServer.Core.UI.Extensions;
#endif

namespace DevkitServer.Multiplayer.Sync;

/*
 * This class uses a temporary file because the maximum possible size of a navigation volume is over the maximum size of an array.
 * As unlikely as this is it's still unnecessary to use that much memory.
 */

public sealed class NavigationSync : AuthoritativeSync<NavigationSync>
{
    private const float GraphDelay = 0.75f;
    private const float GraphSendDelay = 0.25f;

    private const int MaxPacketSize = (int)(NetFactory.MaxPacketSize * 0.2f);

    private static readonly byte[] Buffer = new byte[MaxPacketSize + PacketHeaderSize + sizeof(ulong)];
    public override bool SupportsClientsideAuthority => false;

    // Header structure
    // [1B: header size] [8B: total size] [4B: total packets] [4B: nav net id]

    private const byte HeaderSize = checked(1 + sizeof(long) + sizeof(int) + sizeof(uint) /* align to 64 bit */ + 7);

    // Packet Header structure
    // [1B: packet header size] [4B: packet id] [2B: packet length] 
    private const byte PacketHeaderSize = checked(1 + sizeof(int) + sizeof(ushort) /* align to 64 bit */ + 1);

    private int _index = -1;
    private int _packetId = -1;
    private float _lastSent;
    private float _shouldMoveOnAfter;
    private FileStream? _fs;
    private long _bufferLen;
    private int _ttlPackets;
    private NetId _pendingNetId;
    private BitArray? _packetMask;
    private volatile int _queued;
    private string? _navMeshDisplay;
#if CLIENT
    private NetId _pendingUINetId;
    private NetId _timingOutNetId;
    private float _pendingUINetIdStartTime;
#endif

    private readonly List<NetId> _graphSyncQueue = new List<NetId>(4);
    private List<PacketInfo>? _packets;

    private readonly struct PacketInfo(int packetId, byte[] data, NetId nav)
    {
        public readonly int PacketId = packetId;
        public readonly byte[] Data = data;
        public readonly NetId Nav = nav;
    }

    protected override void Init() { }
    protected override void OnAuthorityUpdated(bool authority)
    {
        if (!authority)
        {
            _graphSyncQueue.Clear();
        }
    }
    private void ReceiveNavigationData(byte[] data, int offset)
    {
        if (!HasAuthority || IsOwner)
        {
            Logger.DevkitServer.LogWarning(Source, "Received navigation data from non-authority tile sync.");
            return;
        }

        byte hdrSize = data[offset];
        if (hdrSize != PacketHeaderSize)
        {
            Logger.DevkitServer.LogWarning(Source, $"Possibly out of date packet header: {hdrSize.Format()}B.");
        }

        int packetId = BitConverter.ToInt32(data, offset + 1);
        int len = BitConverter.ToUInt16(data, offset + 1 + sizeof(int));
        Logger.DevkitServer.LogDebug(Source, $"Packet received: #{packetId.Format()}, len: {len.Format()}.");
        offset += hdrSize;
        if (packetId == 0)
        {
            _index = 0;
            hdrSize = data[offset];
            if (hdrSize != HeaderSize)
            {
                Logger.DevkitServer.LogWarning(Source, $"Possibly out of date header: {hdrSize.Format()}B.");
                return;
            }
            _bufferLen = BitConverter.ToInt64(data, offset + 1) - hdrSize;
            _ttlPackets = BitConverter.ToInt32(data, offset + 1 + sizeof(long));
            _pendingNetId = new NetId(BitConverter.ToUInt32(data, offset + 1 + sizeof(long) + sizeof(int)));

            if (NavigationNetIdDatabase.TryGetNavigation(_pendingNetId, out byte nav) && NavigationUtil.TryGetFlag(nav, out Flag flag))
            {
                string? navName = HierarchyUtil.GetNearestNode<LocationDevkitNode>(flag.point)?.locationName;
                _navMeshDisplay = string.IsNullOrEmpty(navName)
                    ? $"Navigation # {nav.ToString(CultureInfo.InvariantCulture)}"
                    : $"Navigation at {navName} (# {nav.ToString(CultureInfo.InvariantCulture)})";
            }
            else
            {
                _navMeshDisplay = $"Navigation {_pendingNetId.id.ToString("X8", CultureInfo.InvariantCulture)}";
            }

            if (_packetMask == null || _packetMask.Length < _ttlPackets)
                _packetMask = new BitArray((int)Math.Ceiling(_ttlPackets / 32d) * 32);
            else
                _packetMask.SetAll(false);

            offset += hdrSize;
            len -= HeaderSize;

            Logger.DevkitServer.LogDebug(Source, $"Received starting packet of {_navMeshDisplay}: Len: {_bufferLen.Format()}. Packets: {_ttlPackets.Format()}. {_pendingNetId.Format()}.");
            CreateFileStream();
        }
        else if (packetId >= _ttlPackets)
        {
            Logger.DevkitServer.LogWarning(Source, $"Received out of bounds packet for {_navMeshDisplay}: {(packetId + 1).Format()}/{_ttlPackets.Format()}.");
        }

        _index += len;

        if (_fs == null)
        {
            Logger.DevkitServer.LogError(Source, "Unable to append to buffer, FileStream is null.");
            return;
        }

        byte[] newData = new byte[len];
        System.Buffer.BlockCopy(data, offset, newData, 0, newData.Length);
        data = newData;

        StartProcessingPacket(new PacketInfo(packetId, data, _pendingNetId), true);
    }

    private void StartProcessingPacket(PacketInfo packet, bool isNew)
    {
        // asynchronously write to temp nav file to help with file locking.

        bool isNext = packet.PacketId == _packetId + 1;
        int val = -1;
        if (isNext)
            val = Interlocked.CompareExchange(ref _queued, 1, 0);

        if (val == 0)
        {
            _packetId = packet.PacketId;
            try
            {
                _fs!.BeginWrite(packet.Data, 0, packet.Data.Length, WriteComplete, _fs);
            }
            catch (Exception ex)
            {
                Logger.DevkitServer.LogError(Source, ex, "Failed to write to navigation file.");
                Interlocked.Exchange(ref _queued, 0);
            }
        }
        else
        {
            _packets ??= new List<PacketInfo>();
            if (isNew)
                _packets.Add(packet);
            else
                _packets.Insert(0, packet);
        }
    }

    private void FinishReceive()
    {
        Interlocked.Exchange(ref _queued, 0);

        Logger.DevkitServer.LogDebug(Source, $"Received packet #{_packetId.Format()} for {_navMeshDisplay} @ {FormattingUtil.FormatCapacity(_index, colorize: true)}.");

        int missingCt = 0;
        _packetMask![_packetId] = true;
        _shouldMoveOnAfter = CachedTime.RealtimeSinceStartup + 15f;

#if CLIENT
        if (_pendingUINetId == _pendingNetId)
        {
            EditorUIExtension? uiExt = UIExtensionManager.GetInstance<EditorUIExtension>();
            if (uiExt != null)
            {
                uiExt.UpdateLoadingBarProgress((float)(_packetId + 1) / _ttlPackets);
                uiExt.UpdateLoadingBarDescription($"Downloading {_navMeshDisplay} | " + FormattingUtil.FormatCapacity(_index) + " / " + FormattingUtil.FormatCapacity(_bufferLen));
            }

            _pendingUINetIdStartTime = CachedTime.RealtimeSinceStartup;
        }
#endif

        if (_packets != null && _packets.Any(x => x.Nav.id == _pendingNetId.id))
        {
            if (_queued == 0 && _fs != null)
            {
                int index = -1;
                int id = -1;
                for (int i = 0; i < _packets.Count; ++i)
                {
                    PacketInfo info = _packets[i];
                    if (info.Nav.id != _pendingNetId.id)
                        return;

                    if (index == -1 || id > info.PacketId)
                    {
                        id = info.PacketId;
                        index = i;
                    }
                }

                if (index == -1)
                    return;

                PacketInfo newPacket = _packets[index];
                _packets.RemoveAt(index);

                StartProcessingPacket(newPacket, false);
            }

            return;
        }

        for (int i = 0; i < _ttlPackets; ++i)
        {
            if (!_packetMask![i])
                ++missingCt;
        }
        if (missingCt < 1 || _packetId == _ttlPackets - 1)
        {
            _fs!.Seek(0L, SeekOrigin.Begin);
            if (missingCt > 0)
            {
                Logger.DevkitServer.LogWarning(Source, $"Missing packets: {missingCt.Format()}/{_ttlPackets.Format()} ({(missingCt / _ttlPackets).Format("P0")}).");
                _pendingNetId = NetId.INVALID;
                _navMeshDisplay = null;
                _index = -1;
                _packetId = -1;
                return;
            }
            ApplyBuffer(_pendingNetId);
        }
    }
    private void WriteComplete(IAsyncResult ar)
    {
        if (ar.AsyncState is not FileStream str)
            return;

        try
        {
            str.EndWrite(ar);
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError(Source, ex, "Error writing navigation to buffer.");
            Interlocked.Exchange(ref _queued, 0);
            return;
        }

        DevkitServerUtility.QueueOnMainThread(FinishReceive);
    }

    [UsedImplicitly]
    internal static void ReceiveNavigationData(NetPakReader reader)
    {
        if (!reader.ReadUInt16(out ushort len))
        {
            Logger.DevkitServer.LogError(Source, "Failed to read incoming navigation data packet length.");
            return;
        }
        NetFactory.IncrementByteCount(DevkitServerMessage.MovementRelay, false, len + sizeof(ushort));

        if (!reader.ReadBytesPtr(len, out byte[] buffer, out int offset))
        {
            Logger.DevkitServer.LogError(Source, "Failed to read navigation data packet.");
            return;
        }

        ulong fromPlayer = BitConverter.ToUInt64(buffer, offset);

        NavigationSync? sync;
        if (fromPlayer != 0)
        {
            EditorUser? user = UserManager.FromId(fromPlayer);
            if (user == null || user.NavigationSync == null)
            {
                Logger.DevkitServer.LogWarning(Source, $"Unable to find a player to receive navigation data on: {fromPlayer.Format()}.");
                return;
            }
            sync = user.NavigationSync;
        }
        else
        {
            sync = ServersideAuthority;
        }
        if (sync == null)
        {
            Logger.DevkitServer.LogWarning(Source, "Unable to find NavigationSync for navigation data.");
            return;
        }

        if (sync.HasAuthority)
        {
#if SERVER
            if (Provider.clients.Count > 1)
            {
                PooledTransportConnectionList list = NetFactory.GetPooledTransportConnectionList(!sync.IsOwner ? Provider.clients.Count - 1 : Provider.clients.Count);
                for (int i = 0; i < Provider.clients.Count; ++i)
                {
                    SteamPlayer sp = Provider.clients[i];
                    if (sync.IsOwner || sp.playerID.steamID.m_SteamID != sync.User!.SteamId.m_SteamID)
                        list.Add(sp.transportConnection);
                }
                NetFactory.SendGeneric(DevkitServerMessage.SendTileData, new ArraySegment<byte>(buffer, offset, len), list, reliable: true);
            }
#endif
            sync.ReceiveNavigationData(buffer, offset + sizeof(ulong));
        }
        else
            Logger.DevkitServer.LogWarning(Source, $"Received tile data from non-authoritive NavigationSync: {(sync.User == null ? "server-side authority" : sync.User.Format())}.");
    }
    private void TryDisposeFileStream()
    {
        if (_fs == null)
            return;

        try
        {
            _fs.Flush();
            _fs.Dispose();
            File.Delete(Path.Combine(DevkitServerConfig.TempFolder, "nav.temp"));
        }
        catch
        {
            // ignored
        }
        finally
        {
            _fs = null;
        }
    }
    private void CreateFileStream()
    {
        TryDisposeFileStream();
        string path = DevkitServerConfig.TempFolder;
        Directory.CreateDirectory(path);
        path = Path.Combine(path, "nav.temp");
        _fs = new FileStream(path, FileMode.Create, FileAccess.ReadWrite);
    }
    private void BufferData()
    {
        _index = 0;
        NetId netId = _graphSyncQueue[^1];
        _graphSyncQueue.RemoveAt(_graphSyncQueue.Count - 1);
        _pendingNetId = netId;
        Logger.DevkitServer.LogDebug(Source, $"Buffering navigation data: {netId.Format()}.");
        IReadOnlyList<Flag> flags = NavigationUtil.NavigationFlags;
        if (!NavigationNetIdDatabase.TryGetNavigation(netId, out byte nav) || nav >= flags.Count)
        {
            Logger.DevkitServer.LogWarning(Source, $" Navigation flag not found in BufferData: {netId.Format()}.");
            return;
        }

        CreateFileStream();

        Flag flag = flags[nav];
        ByteWriter writer = new ByteWriter(false)
        {
            Stream = _fs,
            Buffer = Buffer
        };

        _bufferLen = NavigationUtil.CalculateTotalWriteSize(flag.graph);
        _ttlPackets = (int)Math.Ceiling(_bufferLen / (double)MaxPacketSize);

        writer.Write(HeaderSize);
        writer.Write(_bufferLen);
        writer.Write(_ttlPackets);
        writer.Write(netId.id);
        if (HeaderSize > writer.Count)
            writer.WriteBlock(0, HeaderSize - writer.Count);


        NavigationUtil.WriteRecastGraphData(writer, flag.graph);
        Logger.DevkitServer.LogDebug(Source, $" Buffered {FormattingUtil.FormatCapacity(_bufferLen - HeaderSize, colorize: true)}. Total packets: {_ttlPackets.Format()}.");

        _fs!.Flush();
        _fs.Seek(0L, SeekOrigin.Begin);
    }
    private void ApplyBuffer(NetId netId)
    {
        if (_fs == null)
        {
            Logger.DevkitServer.LogError(Source, "Unable to apply buffer, FileStream is null.");
            return;
        }

        try
        {
            ByteReader reader = new ByteReader();
            reader.LoadNew(_fs!);

            IReadOnlyList<Flag> flags = NavigationUtil.NavigationFlags;
            if (!NavigationNetIdDatabase.TryGetNavigation(netId, out byte nav) || nav >= flags.Count)
            {
                Logger.DevkitServer.LogWarning(Source, $" Navigation flag not found in ApplyBuffer: {netId.Format()}.");
                return;
            }

            Flag flag = flags[nav];
            NavigationUtil.ReadRecastGraphDataTo(reader, flag.graph);
            flag.UpdateEditorNavmesh();

#if CLIENT
            if (_pendingUINetId == netId)
            {
                EditorUIExtension? uiExt = UIExtensionManager.GetInstance<EditorUIExtension>();
                if (uiExt != null)
                {
                    uiExt.UpdateLoadingBarProgress(1f);
                    uiExt.UpdateLoadingBarDescription($"Done Syncing {_navMeshDisplay}");

                    // roll back so it auto-times out.
                    _pendingUINetIdStartTime = CachedTime.RealtimeSinceStartup - 5.5f;
                    _timingOutNetId = _pendingUINetId;
                }
            }
#endif

            _pendingNetId = NetId.INVALID;
            _index = -1;
            _navMeshDisplay = null;
            _packetId = -1;
            Logger.DevkitServer.LogInfo(Source, $"Synced navigation #{nav.Format()}'s nav-mesh from the server.");
        }
        finally
        {
            TryDisposeFileStream();
        }
    }

    [UsedImplicitly]
    private unsafe void Update()
    {
        float time = CachedTime.RealtimeSinceStartup;
#if CLIENT
        if (!IsOwner && !_pendingUINetId.IsNull())
        {
            if (time - _pendingUINetIdStartTime > 7.5f)
            {
                EditorUIExtension? uiExt = UIExtensionManager.GetInstance<EditorUIExtension>();
                uiExt?.UpdateLoadingBarVisibility(false);
                _pendingUINetId = NetId.INVALID;
                _timingOutNetId = NetId.INVALID;
            }
            else if (time - _pendingUINetIdStartTime > 5f && _timingOutNetId != _pendingUINetId)
            {
                EditorUIExtension? uiExt = UIExtensionManager.GetInstance<EditorUIExtension>();
                uiExt?.UpdateLoadingBarProgress(0f);
                uiExt?.UpdateLoadingBarDescription("Timed Out.");
                _timingOutNetId = _pendingUINetId;
            }
        }
#endif
        if (!HasAuthority && _packets is { Count: > 0 } && _fs != null && _queued == 0)
        {
            int index = -1;
            int id = -1;
            for (int i = 0; i < _packets.Count; ++i)
            {
                PacketInfo info = _packets[i];
                if (!_pendingNetId.IsNull() && info.Nav.id != _pendingNetId.id)
                    return;

                if (index == -1 || id > info.PacketId)
                {
                    id = info.PacketId;
                    index = i;
                }
            }

            if (index == -1)
            {
                if (_shouldMoveOnAfter < time)
                {
                    for (int i = 0; i < _packets.Count; ++i)
                    {
                        PacketInfo info = _packets[i];

                        if (index == -1 || id > info.PacketId)
                        {
                            id = info.PacketId;
                            index = i;
                        }
                    }
                }

                if (index == -1)
                    return;
            }

            PacketInfo newPacket = _packets[index];
            _packets.RemoveAt(index);

            StartProcessingPacket(newPacket, false);
            _shouldMoveOnAfter = time + 15f;
        }

#if SERVER
        if (Provider.clients.Count == 0)
            return;
#endif
        if (!HasAuthority || !IsOwner || (_graphSyncQueue.Count == 0 && _packetId == -1))
            return;

        if (_packetId == -1 && time - _lastSent > GraphDelay)
        {
            if (_graphSyncQueue.Count == 0)
                return;

            BufferData();
            _packetId = 0;
        }

        if (_packetId != -1 && time - _lastSent > (_packetId == 0 ? 0.5f : 0.2f) && _fs != null)
        {
            _lastSent = time;

            const int index = sizeof(ulong) + PacketHeaderSize;

            int len = _fs.Read(Buffer, index, MaxPacketSize);
            Logger.DevkitServer.LogDebug(Source, $"Read from file: {len.Format()} B. Index: {index.Format()} B, max: {MaxPacketSize.Format()} B.");
            if (len <= 0)
            {
                _bufferLen = 0;
                _index = -1;
                _packetId = -1;
                TryDisposeFileStream();
                return;
            }

            int offset = sizeof(ulong);
            fixed (byte* ptr = Buffer)
            {
                UnsafeBitConverter.GetBytes(ptr, User == null ? 0ul : User.SteamId.m_SteamID);
                ptr[offset] = PacketHeaderSize;
                ++offset;
                UnsafeBitConverter.GetBytes(ptr, _packetId, offset);
                offset += sizeof(int);
                UnsafeBitConverter.GetBytes(ptr, (ushort)len, offset);
            }
            NetFactory.SendGeneric(DevkitServerMessage.SendNavigationData, new ArraySegment<byte>(Buffer, 0, index + len),
#if SERVER
                default(IReadOnlyList<ITransportConnection>),
#endif
                reliable: true);

            Logger.DevkitServer.LogDebug(Source, $"Sent nav data packet #{_packetId.Format()} {FormattingUtil.FormatCapacity(len, colorize: true)} ({FormattingUtil.FormatCapacity(_index, colorize: true)} / {FormattingUtil.FormatCapacity(_bufferLen, colorize: true)}).");

            _index += len;
            ++_packetId;
            if (_packetId == _ttlPackets)
            {
                Logger.DevkitServer.LogInfo(Source, $"Synced navigation #{(NavigationNetIdDatabase.TryGetNavigation(_pendingNetId, out byte nav) ? nav.Format() : _pendingNetId.Format())}'s nav-mesh to all clients.");
            }
        }
    }

    protected override void Deinit() { }

    /// <summary>
    /// Queues a navigation flag to sync if this has authority.
    /// </summary>
    /// <param name="netId">The <see cref="NetId"/> of the navigation flag.</param>
    public void EnqueueGraphSync(NetId netId)
    {
        if (!HasAuthority)
            return;

        for (int i = _graphSyncQueue.Count - 1; i >= 0; --i)
        {
            if (_graphSyncQueue[i].id == netId.id)
            {
                _graphSyncQueue.RemoveAt(i);
                break;
            }
        }

        _graphSyncQueue.Insert(0, netId);
        _lastSent = CachedTime.RealtimeSinceStartup + Math.Max(-GraphDelay, GraphSendDelay - _graphSyncQueue.Count * GraphDelay);
        Logger.DevkitServer.LogDebug(Source, $"Requested sync for: {netId.Format()}.");
    }
#if CLIENT
    internal void StartWaitingToUpdateLoadingBar(EditorUIExtension extension, NetId netId)
    {
        extension.UpdateLoadingBarVisibility(true);
        extension.UpdateLoadingBarProgress(0f);

        // all the A* output is in English so I think it's more consistant to not use translations here.
        extension.UpdateLoadingBarDescription("Syncing...");

        _pendingUINetId = netId;
        _pendingUINetIdStartTime = CachedTime.RealtimeSinceStartup;
    }
#endif
}