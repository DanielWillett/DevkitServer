#if CLIENT
//#define GL_SAMPLES
#endif
using DevkitServer.Configuration;
using DevkitServer.Multiplayer.Levels;
using DevkitServer.Multiplayer.Networking;
using DevkitServer.Players;
using DevkitServer.Util.Encoding;
using HarmonyLib;
using SDG.Framework.Landscapes;
using SDG.NetPak;
#if CLIENT
using SDG.Framework.Rendering;
#endif

namespace DevkitServer.Multiplayer.Sync;

public sealed class NavigationSync : AuthoritativeSync<NavigationSync>
{
    private const string Source = "NAVIGATION SYNC";

    private const int MaxPacketSize = (int)(NetFactory.MaxPacketSize * 0.2f);

    private static readonly byte[] Buffer = new byte[MaxPacketSize + HeaderSize];

    // Header structure
    // [1B: header size] [4B: total size] [4B: total packets] [4B: nav net id]

    private const byte HeaderSize = checked(1 + sizeof(int) + sizeof(int) + sizeof(uint) /* align to 64 bit */ + 3);

    // Packet Header structure
    // [1B: packet header size] [4B: packet id] [2B: packet length] 
    private const byte PacketHeaderSize = checked(1 + sizeof(int) + sizeof(ushort) /* align to 64 bit */ + 1);
    
    private int _index = -1;
    private static readonly byte[] PacketBuffer = new byte[PacketHeaderSize + sizeof(ulong) + MaxPacketSize];
    private int _packetId;
    private float _lastSent;
    private int _queueIndex;
    private FileStream? _fs;
    private int _bufferLen;
    private int _ttlPackets;
    private byte _receiving;
    private BitArray? _packetMask;

    private readonly List<NetId> _queue = new List<NetId>(4);

    protected override void Init() { }
    protected override void OnAuthorityUpdated(bool authority)
    {
        if (!authority)
        {
            _queue.Clear();
            _queueIndex = 0;
        }
    }
    /*
    private void ReceiveNavigationData(byte[] data, int offset)
    {
        if (!HasAuthority || IsOwner)
        {
            Logger.LogWarning("Received navigation data from non-authority tile sync.", method: Source);
            return;
        }

        byte hdrSize = data[offset];
        if (hdrSize != PacketHeaderSize)
        {
            Logger.LogWarning($"Out of date packet header: {hdrSize.Format()}B.", method: Source);
            return;
        }

        int packetId = BitConverter.ToInt32(data, offset + 1);
        int len = BitConverter.ToUInt16(data, offset + 1 + sizeof(int));
        Logger.LogDebug($"[{Source}] Packet received: #{packetId.Format()}, len: {len.Format()}.");
        offset += PacketHeaderSize;
        if (packetId == 0)
        {
            _index = 0;
            hdrSize = data[offset + 1];
            if (hdrSize != HeaderSize)
            {
                Logger.LogWarning($"Out of date header: {hdrSize.Format()}B.", method: Source);
                return;
            }
            _bufferLen = BitConverter.ToInt32(data, offset + 2) - hdrSize;
            _ttlPackets = BitConverter.ToInt32(data, offset + 2 + sizeof(int));

            if (_packetMask == null || _packetMask.Length < _ttlPackets)
                _packetMask = new BitArray(Mathf.CeilToInt(_ttlPackets / 32f) * 32);
            else
                _packetMask.SetAll(false);

            _packetMask[0] = true;

            int xmin = BitConverter.ToInt32(data, offset + 2 + sizeof(int) * 2);
            int ymin = BitConverter.ToInt32(data, offset + 2 + sizeof(int) * 3);
            int xmax = BitConverter.ToInt32(data, offset + 2 + sizeof(int) * 4);
            int ymax = BitConverter.ToInt32(data, offset + 2 + sizeof(int) * 5);

            int xtile = BitConverter.ToInt32(data, offset + 2 + sizeof(int) * 6);
            int ytile = BitConverter.ToInt32(data, offset + 2 + sizeof(int) * 7);
            offset += HeaderSize;
            len -= HeaderSize;

            _receiving = new MapInvalidation(new LandscapeCoord(xtile, ytile), xmin, ymin, xmax, ymax, _dataType, CachedTime.RealtimeSinceStartup);
#if CLIENT
            _renderGl = true;
#if GL_SAMPLES
            _gl = _receiving;
#endif
#endif
            Logger.LogDebug($"[{Source}] Received starting packet: Len: {_bufferLen}. Packets: {_ttlPackets}. {_receiving.Format()}.");
        }
        else if (packetId >= _ttlPackets)
        {
            Logger.LogWarning($"Received out of bounds packet: {(packetId + 1).Format()}/{_ttlPackets}.", method: Source);
        }
        else _packetMask![packetId] = true;
        Logger.LogDebug($"[{Source}] Offset: {offset.Format()}, copying {len} B to {_index}.");
        Buffer.BlockCopy(data, offset, _fs, _index, len);
        Logger.LogDebug($"[{Source}] Receiving packet #{packetId} for tile {_receiving.Tile.Format()} ({DevkitServerUtility.FormatBytes(len)} @ {DevkitServerUtility.FormatBytes(_index)})");

        _index += len;
        int missingCt = 0;

#if GL_SAMPLES
        _invalSamples = true;
#endif

        for (int i = 0; i < _ttlPackets; ++i)
        {
            if (!_packetMask![i])
                ++missingCt;
        }
        if (missingCt < 1 || packetId == _ttlPackets - 1)
        {
            if (missingCt > 0)
            {
                Logger.LogWarning($"Missing packets: {missingCt}/{_ttlPackets} ({missingCt / _ttlPackets:P0}.", method: Source);
                _receiving = default;
                _index = 0;
                _packetId = 0;
                return;
            }
            ApplyBuffer();
        }
    }
    */
    [UsedImplicitly]
    internal static void ReceiveNavigationData(NetPakReader reader)
    {
        if (!reader.ReadUInt16(out ushort len))
        {
            Logger.LogError("Failed to read incoming navigation data packet length.", method: Source);
            return;
        }
        NetFactory.IncrementByteCount(false, DevkitServerMessage.MovementRelay, len + sizeof(ushort));

        if (!reader.ReadBytesPtr(len, out byte[] buffer, out int offset))
        {
            Logger.LogError("Failed to read navigation data packet.", method: Source);
            return;
        }

        ulong fromPlayer = BitConverter.ToUInt64(buffer, offset);

        NavigationSync? sync;
        if (fromPlayer != 0)
        {
            EditorUser? user = UserManager.FromId(fromPlayer);
            if (user == null || user.NavigationSync == null)
            {
                Logger.LogWarning($"Unable to find a player to receive navigation data on: {fromPlayer.Format()}.", method: Source);
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
            Logger.LogWarning("Unable to find NavigationSync for navigation data.", method: Source);
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
                NetFactory.SendGeneric(DevkitServerMessage.SendTileData, buffer, list, length: len, reliable: true, offset: offset);
            }
#endif
            // sync.ReceiveNavigationData(buffer, offset + sizeof(ulong));
        }
        else
            Logger.LogWarning($"Received tile data from non-authoritive NavigationSync: {(sync.User == null ? "server-side authority" : sync.User.Format())}.", method: Source);
    }
    private void TryDisposeFileStream()
    {
        if (_fs == null)
            return;

        try
        {
            _fs.Flush();
            _fs.Dispose();
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
        Logger.LogDebug($"[{Source}] Buffering queue[" + _queueIndex + "].");
        NetId netId = _queue[_queueIndex];
        IReadOnlyList<Flag> flags = NavigationUtil.NavigationFlags;
        if (!NavigationNetIdDatabase.TryGetNavigation(netId, out byte nav) || nav >= flags.Count)
        {
            Logger.LogWarning($" Navigation flag not found in BufferData: {netId.Format()}.", method: Source);
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
        writer.Write(netId);

        NavigationUtil.WriteRecastGraphData(writer, flag.graph);
        Logger.LogDebug($"[{Source}]  Buffered {DevkitServerUtility.FormatBytes(_bufferLen - HeaderSize)}. Total packets: {_ttlPackets.Format()}.");

        _fs!.Seek(0L, SeekOrigin.Begin);
    }
    private void ApplyBuffer()
    {
        if (_fs == null)
        {
            Logger.LogError("Unable to apply buffer, FileStream is null.", method: Source);
            return;
        }

        try
        {
            ByteReader reader = new ByteReader();
            reader.LoadNew(_fs!);

            reader.Skip(HeaderSize - sizeof(uint));
            NetId netId = reader.ReadNetId();

            IReadOnlyList<Flag> flags = NavigationUtil.NavigationFlags;
            if (!NavigationNetIdDatabase.TryGetNavigation(netId, out byte nav) || nav >= flags.Count)
            {
                Logger.LogWarning($" Navigation flag not found in ApplyBuffer: {netId.Format()}.", method: Source);
                TryDisposeFileStream();
                return;
            }

            Flag flag = flags[nav];
            NavigationUtil.ReadRecastGraphDataTo(reader, flag.graph);

            _receiving = default;
            _index = 0;
            _packetId = 0;
        }
        finally
        {
            TryDisposeFileStream();
        }
    }
    /*
    [UsedImplicitly]
    private unsafe void Update()
    {
#if SERVER
        if (Provider.clients.Count == 0)
            return;
#endif
        if (!HasAuthority || !IsOwner)
            return;
        float time = CachedTime.RealtimeSinceStartup;
        if (_dataType == DataType.None && time - _lastSent > 1.5f)
        {
            if (_invalidations.Count == 0)
            {
                _queueIndex = 0;
                _dataType = DataType.None;
                return;
            }
            while (_queueIndex < _invalidations.Count && time - _invalidations[_queueIndex].Time < 3f)
                ++_queueIndex;
            if (_queueIndex >= _invalidations.Count)
            {
                _queueIndex = 0;
                _dataType = DataType.None;
                return;
            }
            _lastSent = time;
            _dataType = _invalidations[_queueIndex].Type;

            BufferData();
        }
        else if (_dataType != DataType.None && _index > -1 && time - _lastSent > (_packetId == 0 ? 0.5f : 0.2f))
        {
            _lastSent = time;
            MapInvalidation inv = _invalidations[_queueIndex];
            LandscapeCoord coord = inv.Tile;
            LandscapeTile? tile = Landscape.getTile(coord);
            if (tile == null)
            {
                Logger.LogWarning("Tile not found in BufferData: " + coord + ".", method: Source);
                goto flush;
            }

            int len = MaxPacketSize;
            if (_index + len > _bufferLen)
                len = _bufferLen - _index;

            fixed (byte* ptr = PacketBuffer)
            {
                UnsafeBitConverter.GetBytes(ptr, User == null ? 0ul : User.SteamId.m_SteamID);
                ptr[sizeof(ulong)] = PacketHeaderSize;
                int offset = 1 + sizeof(ulong);
                UnsafeBitConverter.GetBytes(ptr, _packetId, offset);
                offset += sizeof(int);
                UnsafeBitConverter.GetBytes(ptr, (ushort)len, offset);
            }

            Buffer.BlockCopy(_fs, _index, PacketBuffer, PacketHeaderSize + sizeof(ulong), len);

            NetFactory.SendGeneric(DevkitServerMessage.SendTileData, PacketBuffer,
#if SERVER
                null,
#endif
                length: PacketHeaderSize + sizeof(ulong) + len, reliable: true);
            // DevkitServerUtility.PrintBytesHex(PacketBuffer, len: PacketHeaderSize + sizeof(ulong) + len);
            Logger.LogDebug($"[{Source}] #{_packetId.Format()} Tile: {tile.coord.Format()} Length: {len.Format()}.");

            _index += len;
            ++_packetId;
        flush:
            // flush buffer
            if (_index >= _bufferLen || tile == null)
            {
                _invalidations.RemoveAt(_queueIndex);
                _dataType = DataType.None;
                _bufferLen = 0;
                _index = -1;
                _packetId = 0;
            }
        }
    }*/

    protected override void Deinit() { }
}