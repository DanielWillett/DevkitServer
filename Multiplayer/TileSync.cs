using DevkitServer.Multiplayer.Networking;
using DevkitServer.Players;
using DevkitServer.Util.Encoding;
using HarmonyLib;
using JetBrains.Annotations;
using SDG.Framework.Landscapes;
using SDG.Framework.Rendering;
using SDG.Framework.Utilities;
using SDG.NetPak;
using TMPro;

namespace DevkitServer.Multiplayer;
[HarmonyPatch]
public class TileSync : MonoBehaviour
{
    private static readonly NetCall<ulong> SendTileSyncAuthority = new NetCall<ulong>((ushort)NetCalls.TileSyncAuthority);
    private const int MaxPacketSize = (int)(NetFactory.MaxPacketSize * 0.75f); // leave some space for other packets

    // Header structure
    // [1B: type] [1B: header size] [4B: total size] [4B: total packets] [16B: bounds (4B x 4)] [8B: tile (4B x 2)]

    private const byte HeaderSize = checked( 2 + sizeof(int) * 8 /* align to 64 bit */ + 6);

    // Packet Header structure
    // [1B: packet header size] [4B: packet id] [2B: packet length] 
    private const byte PacketHeaderSize = checked( 1 + sizeof(int) + sizeof(ushort) /* align to 64 bit */ + 1);
    public static int HeightmapPacketCount { get; }
    public static int SplatmapPacketCount { get; }
    public static int HolePacketCount { get; }
    public static int TotalHeightmapLength { get; }
    public static int TotalSplatmapLength { get; }
    public static int TotalHoleLength { get; }
    public static TileSync ServersideAuthorityTileSync { get; private set; } = null!;

    /// <remarks><see langword="null"/> for the server-side authority instance.</remarks>
    public EditorUser? User { get; internal set; }
    public bool IsOwner { get; private set; }
    public bool HasAuthority
    {
        get => _hasAuthority;
#if CLIENT
        private
#endif
        set
        {
            if (_hasAuthority == value)
                return;
            if (value)
            {
#if SERVER
                SendTileSyncAuthority?.Invoke(Provider.GatherRemoteClientConnections(), User == null ? 0 : User.SteamId.m_SteamID);
#endif
                if (ServersideAuthorityTileSync != null)
                    ServersideAuthorityTileSync.HasAuthority = false;

                for (int i = 0; i < UserManager.Users.Count; ++i)
                {
                    EditorUser u = UserManager.Users[i];
                    if (u.TileSync != null)
                        u.TileSync.HasAuthority = false;
                }
            }
            else
            {
                _invalidations.Clear();
                _invalidateIndex = 0;
            }
            _hasAuthority = value;
            if (User == null)
                Logger.LogDebug($"Server-side authority TileSync {(value ? "gained" : "lost")} authority.");
            else
                Logger.LogDebug($"{User.Format()} TileSync {(value ? "gained" : "lost")} authority.");
        }
    }

    private DataType _dataType;
    public enum DataType
    {
        None,
        Heightmap,
        Splatmap,
        Holes
    }
    private bool _hasAuthority;
    private int _index = -1;
    private static readonly byte[] PacketBuffer = new byte[PacketHeaderSize + sizeof(ulong) + MaxPacketSize];
    private int _packetId;
    private float _lastSent;
    private int _invalidateIndex;
    private readonly byte[] _buffer;
    private int _bufferLen;
    private int _ttlPackets;
    private MapInvalidation _receiving;
#if CLIENT && DEBUG
    private MapInvalidation _gl;
    private bool _renderGl;
#endif
    private BitArray? _packetMask;
    static TileSync()
    {
        TotalHeightmapLength = Landscape.HEIGHTMAP_RESOLUTION * Landscape.HEIGHTMAP_RESOLUTION * sizeof(float);
        TotalSplatmapLength = Landscape.SPLATMAP_RESOLUTION * Landscape.SPLATMAP_RESOLUTION * Landscape.SPLATMAP_LAYERS * sizeof(float);
        TotalHoleLength = Mathf.CeilToInt(Landscape.HOLES_RESOLUTION * Landscape.HOLES_RESOLUTION / 8f);
        HeightmapPacketCount = Mathf.CeilToInt((float)TotalHeightmapLength / MaxPacketSize);
        SplatmapPacketCount = Mathf.CeilToInt((float)TotalSplatmapLength / MaxPacketSize);
        HolePacketCount = Mathf.CeilToInt((float)TotalHoleLength / MaxPacketSize);
    }
    private TileSync()
    {
        _buffer = new byte[HeaderSize + Math.Max(TotalHeightmapLength, Math.Max(TotalSplatmapLength, TotalHoleLength))];
    }
    [UsedImplicitly]
    private void Start()
    {
#if CLIENT&& DEBUG
        GLRenderer.render += HandleGLRender;
#endif
        if (User == null)
        {
#if SERVER
            IsOwner = true;
#endif
#if CLIENT
            IsOwner = false;
#endif
            Logger.LogDebug("Server-side authority TileSync initialized.");
            if (ServersideAuthorityTileSync != null)
                Destroy(ServersideAuthorityTileSync);
            ServersideAuthorityTileSync = this;
            HasAuthority = true;
        }
        else
        {
            Logger.LogDebug($"Client {User.Format()} TileSync initialized.");
#if CLIENT
            IsOwner = User == EditorUser.User;
#endif
#if SERVER
            IsOwner = false;
#endif
        }
    }
#if CLIENT && DEBUG
    private unsafe void HandleGLRender()
    {
        if (HasAuthority)
        {
            if (_invalidateIndex > -1 && _invalidations.Count > _invalidateIndex)
            {
                MapInvalidation inv = _invalidations[_invalidateIndex];
                DevkitServerGLUtility.DrawTerrainBounds(inv.Tile, inv.XMin, inv.XMax, inv.YMin, inv.YMax, inv.Type != DataType.Heightmap, Color.magenta);
            }
            float time = Time.realtimeSinceStartup;
            Color pink = new Color(1f, 0.7f, 0.7f);
            Color red = Color.red;
            for (int i = 0; i < _invalidations.Count; ++i)
            {
                MapInvalidation inv = _invalidations[i];
                DevkitServerGLUtility.DrawTerrainBounds(inv.Tile, inv.XMin, inv.XMax, inv.YMin, inv.YMax, inv.Type != DataType.Heightmap,
                    i == _invalidateIndex ? Color.magenta : Color.Lerp(pink, red, (time - inv.Time) / 10f));
            }
        }

        if (!_renderGl)
            return;
        DevkitServerGLUtility.DrawTerrainBounds(_gl.Tile, _gl.XMin, _gl.XMax, _gl.YMin, _gl.YMax, _gl.Type != DataType.Heightmap);
#if true
        LandscapeCoord tile = _gl.Tile;
        GL.Begin(GL.TRIANGLES);
        fixed (byte* ptr = _buffer)
        {
            float num = 0.5f;//Mathf.Lerp(0.1f, 1f, Mathf.Min(_gl.YMax - _gl.YMin, _gl.XMax - _gl.XMin) / 256f);
            Vector3 size = new Vector3(num, num, num);
            int offsetX = _gl.XMin;
            int offsetY = _gl.YMin;
            int sizeX = _gl.XMax - offsetX + 1;
            int sizeY = _gl.YMax - offsetY + 1;

            switch (_dataType)
            {
                case DataType.Heightmap:
                    float* buffer = (float*)ptr;
                    for (int x = offsetX; x < offsetX + sizeX; ++x)
                    {
                        for (int y = offsetY; y < offsetY + sizeY; ++y)
                        {
                            GL.Color(Color.Lerp(Color.red, Color.green, *buffer));
                            GLUtility.boxSolid(Landscape.getWorldPosition(tile, new HeightmapCoord(x, y), *buffer), size);
                            ++buffer;
                        }
                    }
                    break;
                case DataType.Splatmap:
                    buffer = (float*)ptr;
                    AssetReference<LandscapeMaterialAsset> mat = TerrainEditor.splatmapMaterialTarget;
                    LandscapeTile? tile2 = Landscape.getTile(tile);
                    int index = tile2 == null || !mat.isValid ? -1 : tile2.materials.FindIndex(x => x.GUID == mat.GUID);
                    if (index >= 0)
                    {
                        for (int x = offsetX; x < offsetX + sizeX; ++x)
                        {
                            for (int y = offsetY; y < offsetY + sizeY; ++y)
                            {
                                GL.Color(Color.Lerp(Color.red, Color.green, buffer[index]));
                                GLUtility.boxSolid(Landscape.getWorldPosition(tile, new SplatmapCoord(x, y)), size);
                                buffer += Landscape.SPLATMAP_LAYERS;
                            }
                        }
                    }
                    break;
                case DataType.Holes:
                    int c = 0;
                    for (int x = offsetX; x < offsetX + sizeX; ++x)
                    {
                        for (int y = offsetY; y < offsetY + sizeY; ++y)
                        {
                            bool val = (ptr[c / 8] & (1 << (c % 8))) > 0;
                            GL.Color(val ? Color.green : Color.red);
                            GLUtility.boxSolid(Landscape.getWorldPosition(tile, new SplatmapCoord(x, y)), size);
                            ++c;
                        }
                    }
                    break;
            }
        }

        GL.End();
#endif
    }
#endif

    private readonly List<MapInvalidation> _invalidations = new List<MapInvalidation>(32);
    private struct MapInvalidation
    {
        public readonly LandscapeCoord Tile;
        public readonly DataType Type;
        public int XMin;
        public int XMax;
        public int YMin;
        public int YMax;
        public float Time;
        public HeightmapBounds HeightmapBounds => new HeightmapBounds(new HeightmapCoord(XMin, YMin), new HeightmapCoord(XMax, YMax));
        public SplatmapBounds SplatmapBounds => new SplatmapBounds(new SplatmapCoord(XMin, YMin), new SplatmapCoord(XMax, YMax));
        public MapInvalidation(LandscapeCoord tile, SplatmapBounds bounds, float time, bool hole = false)
        {
            Tile = tile;
            XMin = bounds.min.x;
            YMin = bounds.min.y;
            XMax = bounds.max.x;
            YMax = bounds.max.y;
            Time = time;
            Type = hole ? DataType.Holes : DataType.Splatmap;
        }
        public MapInvalidation(LandscapeCoord tile, HeightmapBounds bounds, float time)
        {
            Tile = tile;
            XMin = bounds.min.x;
            YMin = bounds.min.y;
            XMax = bounds.max.x;
            YMax = bounds.max.y;
            Time = time;
            Type = DataType.Heightmap;
        }
        public MapInvalidation(LandscapeCoord tile, int minX, int minY, int maxX, int maxY, DataType type, float time)
        {
            Tile = tile;
            XMin = minX;
            YMin = minY;
            XMax = maxX;
            YMax = maxY;
            Time = time;
            Type = type;
        }
        public bool Overlaps(in MapInvalidation other) => !(XMax < other.XMin || YMax < other.YMin || XMin > other.XMax || YMin > other.YMax);
        public void Encapsulate(in MapInvalidation other)
        {
            if (XMin > other.XMin)
                XMin = other.XMin;
            if (YMin > other.YMin)
                YMin = other.YMin;
            if (XMax < other.XMax)
                XMax = other.XMax;
            if (YMax < other.YMax)
                YMax = other.YMax;
        }
    }
#if CLIENT
    [NetCall(NetCallSource.FromServer, (ushort)NetCalls.TileSyncAuthority)]
    [UsedImplicitly]
    private static void ReceiveTileSyncAuthority(MessageContext ctx, ulong s64)
    {
        if (s64 == 0)
        {
            if (ServersideAuthorityTileSync != null)
                ServersideAuthorityTileSync.HasAuthority = true;
            return;
        }
        EditorUser? user = UserManager.FromId(s64);
        if (user != null && user.TileSync != null)
        {
            user.TileSync.HasAuthority = true;
        }
    }
#endif
    private void ReceiveTileData(byte[] data, int offset, int length)
    {
        if (!HasAuthority || IsOwner)
        {
            Logger.LogWarning("[TILE SYNC] Received tile data from non-authority tile sync.");
            return;
        }

        byte hdrSize = data[offset];
        ++offset;
        if (hdrSize != PacketHeaderSize)
        {
            Logger.LogWarning($"[TILE SYNC] Out of date packet header: {hdrSize.Format()}B.");
            return;
        }

        int packetId = BitConverter.ToInt32(data, offset);
        offset += sizeof(int);
        int len = BitConverter.ToUInt16(data, offset);
        offset += sizeof(ushort);
        Logger.LogDebug($"Packet received: #{packetId.Format()}, len: {len.Format()}.");
        if (packetId == 0)
        {
            _index = 0;
            _dataType = (DataType)data[offset];
            ++offset;
            hdrSize = data[offset];
            ++offset;
            if (hdrSize != HeaderSize)
            {
                Logger.LogWarning($"[TILE SYNC] Out of date header: {hdrSize.Format()}B.");
                return;
            }
            _bufferLen = BitConverter.ToInt32(data, offset) - hdrSize;
            offset += sizeof(int);
            _ttlPackets = BitConverter.ToInt32(data, offset);
            offset += sizeof(int);

            if (_packetMask == null || _packetMask.Length < _ttlPackets)
                _packetMask = new BitArray(Mathf.CeilToInt(_ttlPackets / 32f) * 32);
            else
                _packetMask.SetAll(false);

            _packetMask[0] = true;

            int xmin = BitConverter.ToInt32(data, offset);
            offset += sizeof(int);
            int ymin = BitConverter.ToInt32(data, offset);
            offset += sizeof(int);
            int xmax = BitConverter.ToInt32(data, offset);
            offset += sizeof(int);
            int ymax = BitConverter.ToInt32(data, offset);
            offset += sizeof(int);

            int xtile = BitConverter.ToInt32(data, offset);
            offset += sizeof(int);
            int ytile = BitConverter.ToInt32(data, offset);
            offset += sizeof(int);

            _receiving = new MapInvalidation(new LandscapeCoord(xtile, ytile), xmin, ymin, xmax, ymax, _dataType, Time.realtimeSinceStartup);
            Logger.LogDebug($"Received starting packet: Len: {_bufferLen}. Packets: {_ttlPackets}. Tile: {_receiving.Tile.Format()}, Bounds: ({xmin.Format()}-{xmax.Format()}, {ymin.Format()}-{ymax.Format()}), Type: {_dataType}.");
#if CLIENT && DEBUG
            _gl = _receiving;
            _renderGl = true;
#endif
        }
        else if (packetId >= _ttlPackets)
        {
            Logger.LogWarning($"[TILE SYNC] Received out of bounds packet: {(packetId + 1).Format()}/{_ttlPackets}.");
        }
        else _packetMask![packetId] = true;
        Logger.LogDebug($"[TILE SYNC] Offset: {offset.Format()}, copying {len} B to {_index}.");
        Buffer.BlockCopy(data, offset, _buffer, _index, len);
        Logger.LogDebug($"[TILE SYNC] Receiving packet #{packetId} for tile {_receiving.Tile.Format()} ({DevkitServerUtility.FormatBytes(len)} @ {DevkitServerUtility.FormatBytes(_index)})");
        
        _index += len;
        int missingCt = 0;
        for (int i = 0; i < _ttlPackets; ++i)
        {
            if (!_packetMask![i])
                ++missingCt;
        }
        if (missingCt < 1 || packetId == _ttlPackets - 1)
        {
            if (missingCt > 0)
            {
                Logger.LogWarning($"[TILE SYNC] Missing packets: {missingCt}/{_ttlPackets} ({missingCt / _ttlPackets:P0}.");
                _receiving = default;
                _index = 0;
                _packetId = 0;
                return;
            }
            ApplyBuffer();
        }
    }

    [UsedImplicitly]
    internal static void ReceiveTileData(NetPakReader reader)
    {
        if (!reader.ReadUInt16(out ushort len))
        {
            Logger.LogError("[TILE SYNC] Failed to read incoming tile data packet length.");
            return;
        }

        if (!reader.ReadBytesPtr(len, out byte[] buffer, out int offset))
        {
            Logger.LogError("[TILE SYNC] Failed to read tile data packet.");
            return;
        }

        ulong fromPlayer = BitConverter.ToUInt64(buffer, offset);

        TileSync sync;
        if (fromPlayer != 0)
        {
            EditorUser? user = UserManager.FromId(fromPlayer);
            if (user == null || user.TileSync == null)
            {
                Logger.LogWarning($"Unable to find a player to receive tile data on: {fromPlayer.Format()}.");
                return;
            }
            sync = user.TileSync;
        }
        else
        {
            sync = ServersideAuthorityTileSync;
        }
        if (sync == null)
        {
            Logger.LogWarning("Unable to find TileSync for tile data.");
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
                    if (sync.IsOwner || sp.playerID.steamID.m_SteamID == sync.User!.SteamId.m_SteamID)
                        list.Add(sp.transportConnection);
                }
                NetFactory.SendGeneric(NetFactory.DevkitMessage.SendTileData, buffer, list, length: len, reliable: true, offset: offset);
            }
#endif
            sync.ReceiveTileData(buffer, offset + sizeof(ulong), len);
        }
        else
            Logger.LogWarning($"Received tile data from non-authoritive TileSync: {(sync.User == null ? "server-side authority" : sync.User.Format())}.");
    }
    private bool AddBounds(LandscapeCoord coord, MapInvalidation bounds, float time)
    {
        // it works i swear
        DataType type = bounds.Type;
        for (int i = _invalidations.Count - 1; i >= 0; --i)
        {
            MapInvalidation inv = _invalidations[i];
            if (inv.Type == type && inv.Tile == coord && inv.Overlaps(in bounds))
            {
                _invalidations.RemoveAtFast(i);
                MapInvalidation b3 = inv;
                b3.Encapsulate(bounds);
                if (!AddBounds(coord, b3, time))
                {
                    b3.Time = time;
                    _invalidations.Add(b3);
                }

                return true;
            }
        }
        
        return false;
    }

    /// <summary>
    /// Merges bounds with already existing bounds and
    /// updates the last edited time so it doesn't get updated
    /// until the user stops working on it.
    /// 
    /// </summary>
    /// <remarks>Encapsulates existing bounds or adds a new one.</remarks>
    public void InvalidateBounds(Bounds bounds, DataType type, float time)
    {
        LandscapeBounds b2 = new LandscapeBounds(bounds);
        for (int x1 = b2.min.x; x1 <= b2.max.x; ++x1)
        {
            for (int y1 = b2.min.y; y1 <= b2.max.y; ++y1)
            {
                LandscapeCoord c2 = new LandscapeCoord(x1, y1);
                if (Landscape.getTile(c2) != null)
                {
                    MapInvalidation inv;
                    switch (type)
                    {
                        case DataType.Heightmap:
                            inv = new MapInvalidation(c2, new HeightmapBounds(c2, bounds), time);
                            break;
                        case DataType.Holes:
                        case DataType.Splatmap:
                            inv = new MapInvalidation(c2, new SplatmapBounds(c2, bounds), time, type == DataType.Holes);
                            break;
                        default:
                            continue;
                    }
                    if (!AddBounds(c2, inv, time))
                    {
                        _invalidations.Add(inv);
                    }
                }
            }
        }
    }
    private unsafe void BufferData()
    {
        if (_dataType is not DataType.Heightmap and not DataType.Splatmap and not DataType.Holes)
            return;

        _index = 0;
        Logger.LogDebug("[TILE SYNC] Buffering " + _dataType + "[" + _invalidateIndex + "].");
        MapInvalidation inv = _invalidations[_invalidateIndex];
        LandscapeTile? tile = Landscape.getTile(inv.Tile);
        if (tile == null)
        {
            Logger.LogWarning("[TILE SYNC]  Tile not found in BufferData: " + inv.Tile + ".");
            return;
        }

        fixed (byte* ptr = _buffer)
        {
            switch (_dataType)
            {
                case DataType.Heightmap:
                    HeightmapBounds heightmapBounds = inv.HeightmapBounds;
                    LandscapeUtil.ReadHeightmap(ptr + HeaderSize, tile, in heightmapBounds);
                    _bufferLen = HeaderSize + LandscapeUtil.GetHeightmapSize(in heightmapBounds);
                    break;
                case DataType.Splatmap:
                    SplatmapBounds splatmapBounds = inv.SplatmapBounds;
                    LandscapeUtil.ReadSplatmap(ptr + HeaderSize, tile, in splatmapBounds);
                    _bufferLen = HeaderSize + LandscapeUtil.GetSplatmapSize(in splatmapBounds);
                    break;
                case DataType.Holes:
                    splatmapBounds = inv.SplatmapBounds;
                    LandscapeUtil.ReadHoles(ptr + HeaderSize, tile, in splatmapBounds);
                    _bufferLen = HeaderSize + LandscapeUtil.GetHolesSize(in splatmapBounds);
                    break;
            }

            int offset = 2;
            ptr[0] = (byte)_dataType;
            ptr[1] = HeaderSize;
            UnsafeBitConverter.GetBytes(ptr, _bufferLen, offset);
            offset += sizeof(int);
            int totalPackets = (int)Math.Ceiling(_bufferLen / (double)MaxPacketSize);
            UnsafeBitConverter.GetBytes(ptr, totalPackets, offset);
            offset += sizeof(int);

            UnsafeBitConverter.GetBytes(ptr, inv.XMin, offset);
            offset += sizeof(int);
            UnsafeBitConverter.GetBytes(ptr, inv.YMin, offset);
            offset += sizeof(int);
            UnsafeBitConverter.GetBytes(ptr, inv.XMax, offset);
            offset += sizeof(int);
            UnsafeBitConverter.GetBytes(ptr, inv.YMax, offset);
            offset += sizeof(int);

            UnsafeBitConverter.GetBytes(ptr, tile.coord.x, offset);
            offset += sizeof(int);
            UnsafeBitConverter.GetBytes(ptr, tile.coord.y, offset);
        }

        Logger.LogDebug("[TILE SYNC]  Buffered " + DevkitServerUtility.FormatBytes(_bufferLen - HeaderSize) + " (" + _dataType.ToString().ToUpperInvariant() + ").");
    }
    private unsafe void ApplyBuffer()
    {
        LandscapeTile? tile = Landscape.getTile(_receiving.Tile);
        if (tile == null)
        {
            Logger.LogWarning($"[TILE SYNC] Tile not found in ApplyBuffer: {_receiving.Tile.Format()}.");
            return;
        }

        fixed (byte* ptr = _buffer)
        {
            //if (_dataType is DataType.Heightmap or DataType.Splatmap && !BitConverter.IsLittleEndian)
            //{
            //    for (int i = 0; i < _buffer.Length; i += sizeof(float))
            //        DevkitServerUtility.ReverseFloat(ptr, i);
            //}
            switch (_dataType)
            {
                case DataType.Heightmap:
                    HeightmapBounds heightmapBounds = _receiving.HeightmapBounds;
                    LandscapeUtil.WriteHeightmap(ptr, tile, in heightmapBounds);
                    break;
                case DataType.Splatmap:
                    SplatmapBounds splatmapBounds = _receiving.SplatmapBounds;
                    LandscapeUtil.WriteSplatmap(ptr, tile, in splatmapBounds);
                    break;
                case DataType.Holes:
                    splatmapBounds = _receiving.SplatmapBounds;
                    LandscapeUtil.WriteHoles(ptr, tile, in splatmapBounds);
                    break;
            }
        }

        _receiving = default;
        _index = 0;
        _packetId = 0;
        // Array.Clear(_buffer, 0, _bufferLen);
    }
    [UsedImplicitly]
    private unsafe void Update()
    {
#if SERVER
        if (Provider.clients.Count == 0)
            return;
#endif
        if (!HasAuthority || !IsOwner)
            return;
        float time = Time.realtimeSinceStartup;
        if (_dataType == DataType.None && time - _lastSent > 5f)
        {
            if (_invalidations.Count == 0)
            {
                _invalidateIndex = 0;
                _dataType = DataType.None;
                return;
            }
            while (_invalidateIndex < _invalidations.Count && time - _invalidations[_invalidateIndex].Time < 2f)
                ++_invalidateIndex;
            if (_invalidateIndex >= _invalidations.Count)
            {
                _invalidateIndex = 0;
                _dataType = DataType.None;
                return;
            }
            _lastSent = time;
            _dataType = _invalidations[_invalidateIndex].Type;

            BufferData();
        }
        else if (_dataType != DataType.None && _index > -1 && time - _lastSent > (_packetId == 1 ? 0.5f : 0.2f))
        {
            _lastSent = time;
            MapInvalidation inv = _invalidations[_invalidateIndex];
            LandscapeCoord coord = inv.Tile;
            LandscapeTile? tile = Landscape.getTile(coord);
            if (tile == null)
            {
                Logger.LogWarning("[TILE SYNC] Tile not found in BufferData: " + coord + ".");
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
                UnsafeBitConverter.GetBytes(ptr, (ushort)(_packetId == 0 ? len - HeaderSize : len), offset);
            }
            
            Buffer.BlockCopy(_buffer, _index, PacketBuffer, PacketHeaderSize + sizeof(ulong), len);
            
            NetFactory.SendGeneric(NetFactory.DevkitMessage.SendTileData, PacketBuffer,
#if SERVER
                null,
#endif
                length: PacketHeaderSize + sizeof(ulong) + len, reliable: true);
            Logger.LogDebug($"#{_packetId.Format()} Tile: {tile.coord.Format()} Length: {len.Format()}.");
            
            _index += len;
            ++_packetId;
            flush:
            // flush buffer
            if (_index >= _bufferLen || tile == null)
            {
                _invalidations.RemoveAt(_invalidateIndex);
                _dataType = DataType.None;
                _bufferLen = 0;
                _index = -1;
                _packetId = 0;
            }
        }
    }

    [HarmonyPatch(typeof(Landscape), nameof(Landscape.writeHeightmap))]
    [HarmonyPostfix]
    [UsedImplicitly]
    private static void OnWriteHeightmap(Bounds worldBounds, Landscape.LandscapeWriteHeightmapHandler callback)
    {
#if SERVER
        if (ServersideAuthorityTileSync != null && ServersideAuthorityTileSync.HasAuthority)
            ServersideAuthorityTileSync.InvalidateBounds(worldBounds, DataType.Heightmap, Time.realtimeSinceStartup);
#else
        if (EditorUser.User != null && EditorUser.User.TileSync != null && EditorUser.User.TileSync.HasAuthority)
            EditorUser.User.TileSync.InvalidateBounds(worldBounds, DataType.Heightmap, Time.realtimeSinceStartup);
#endif
    }

    [HarmonyPatch(typeof(Landscape), nameof(Landscape.writeSplatmap))]
    [HarmonyPostfix]
    [UsedImplicitly]
    private static void OnWriteSplatmap(Bounds worldBounds, Landscape.LandscapeWriteSplatmapHandler callback)
    {
#if SERVER
        if (ServersideAuthorityTileSync != null && ServersideAuthorityTileSync.HasAuthority)
            ServersideAuthorityTileSync.InvalidateBounds(worldBounds, DataType.Splatmap, Time.realtimeSinceStartup);
#else
        if (EditorUser.User != null && EditorUser.User.TileSync != null && EditorUser.User.TileSync.HasAuthority)
            EditorUser.User.TileSync.InvalidateBounds(worldBounds, DataType.Splatmap, Time.realtimeSinceStartup);
#endif
    }

    [HarmonyPatch(typeof(Landscape), nameof(Landscape.writeHoles))]
    [HarmonyPostfix]
    [UsedImplicitly]
    private static void OnWriteHoles(Bounds worldBounds, Landscape.LandscapeWriteHolesHandler callback)
    {
#if SERVER
        if (ServersideAuthorityTileSync != null && ServersideAuthorityTileSync.HasAuthority)
            ServersideAuthorityTileSync.InvalidateBounds(worldBounds, DataType.Holes, Time.realtimeSinceStartup);
#else
        if (EditorUser.User != null && EditorUser.User.TileSync != null && EditorUser.User.TileSync.HasAuthority)
            EditorUser.User.TileSync.InvalidateBounds(worldBounds, DataType.Holes, Time.realtimeSinceStartup);
#endif
    }
#if CLIENT&& DEBUG
    [UsedImplicitly]
    private void OnDestroy()
    {
        GLRenderer.render -= HandleGLRender;
    }
#endif
}