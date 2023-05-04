using DevkitServer.Multiplayer.Networking;
using DevkitServer.Players;
using DevkitServer.Util.Encoding;
using HarmonyLib;
using JetBrains.Annotations;
using SDG.Framework.Devkit;
using SDG.Framework.Landscapes;
using SDG.NetPak;

namespace DevkitServer.Multiplayer;
[HarmonyPatch]
public class TileSync : MonoBehaviour
{
    private static readonly NetCall<ulong> SendTileSyncAuthority = new NetCall<ulong>((ushort)NetCalls.TileSyncAuthority);
    private const int MaxPacketSize = (int)(NetFactory.MaxPacketSize * 0.75f); // leave some space for other packets
    private const int PacketOffset = sizeof(ushort) + 1 + 7 * sizeof(int) + sizeof(ulong);
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
#if SERVER
        set
#else
        private set
#endif
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
    private static readonly byte[] _packetBuffer = new byte[PacketOffset + MaxPacketSize];
    private int _packetId;
    private float _lastSent;
    private int _invalidateIndex;
    private static readonly byte[] _buffer;
    private static int _bufferLen;
    static TileSync()
    {
        TotalHeightmapLength = Landscape.HEIGHTMAP_RESOLUTION * Landscape.HEIGHTMAP_RESOLUTION * sizeof(float);
        TotalSplatmapLength = Landscape.SPLATMAP_RESOLUTION * Landscape.SPLATMAP_RESOLUTION * Landscape.SPLATMAP_LAYERS * sizeof(float);
        TotalHoleLength = Mathf.CeilToInt(Landscape.HOLES_RESOLUTION * Landscape.HOLES_RESOLUTION / 8f);
        HeightmapPacketCount = Mathf.CeilToInt((float)TotalHeightmapLength / MaxPacketSize);
        SplatmapPacketCount = Mathf.CeilToInt((float)TotalSplatmapLength / MaxPacketSize);
        HolePacketCount = Mathf.CeilToInt((float)TotalHoleLength / MaxPacketSize);
        _buffer = new byte[Math.Max(TotalHeightmapLength, Math.Max(TotalSplatmapLength, TotalHoleLength))];
    }
    private void Start()
    {
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
    private void ReceiveTileData(byte[] data, int offset)
    {
        if (!HasAuthority)
        {
            Logger.LogWarning("Received tile data while authority.");
            return;
        }
        _dataType = (DataType)data[offset];
        int packetId = BitConverter.ToUInt16(data, offset + 1);
        LandscapeCoord coords = new LandscapeCoord(BitConverter.ToInt32(data, offset + 1 + sizeof(ushort)), BitConverter.ToInt32(data, offset + sizeof(ushort) + 1 + sizeof(int)));
        int sx = BitConverter.ToInt32(data, offset + sizeof(ushort) + 1 + sizeof(int) * 2);
        int sy = BitConverter.ToInt32(data, offset + sizeof(ushort) + 1 + sizeof(int) * 3);
        int ox = BitConverter.ToInt32(data, offset + sizeof(ushort) + 1 + sizeof(int) * 4);
        int oy = BitConverter.ToInt32(data, offset + sizeof(ushort) + 1 + sizeof(int) * 5);
        int len = BitConverter.ToInt32(data, offset + sizeof(ushort) + 1 + sizeof(int) * 6);
        _index = packetId * MaxPacketSize;
        Logger.LogDebug($"#{packetId.Format()} Sizes: {sx.Format()}x{sy.Format()}, Offset: ({ox.Format()}, {oy.Format()}) Tile: {coords.Format()}.");
        _bufferLen = _dataType switch
        {
            DataType.Heightmap => sx * sy * sizeof(float),
            DataType.Splatmap => sx * sy * Landscape.SPLATMAP_LAYERS * sizeof(float),
            _ => Mathf.CeilToInt(sx * sy / 8f)
        };
        Logger.LogDebug($"[TILE SYNC] Receiving packet #{packetId} for tile {coords} ({DevkitServerUtility.FormatBytes(len)} @ {DevkitServerUtility.FormatBytes(_index)})");
        if (_index + len > _bufferLen)
            len = _bufferLen - _index;
        Logger.LogDebug($"[TILE SYNC] srcLen: {data.Length}, srcInd: {offset + PacketOffset}, dstLen: {_buffer.Length} ({_bufferLen}), dstInd: {_index}, count: {len}.");
        Buffer.BlockCopy(data, offset + PacketOffset, _buffer, _index, len);
        _index += len;
        if (len + _index >= _bufferLen)
        {
            ApplyBuffer(coords, sx, sy, ox, oy);
            Logger.LogDebug($"[TILE SYNC] Finalizing tile {coords}.");
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
            sync.ReceiveTileData(buffer, offset + sizeof(ulong));
        else
            Logger.LogWarning($"Received tile data from non-authoritive TileSync: {(sync.User == null ? "server-side authority" : sync.User.Format())}.");
    }
    private bool AddBounds(LandscapeCoord coord, MapInvalidation bounds, float time)
    {
        DataType type = bounds.Type;
        for (int i = _invalidations.Count - 1; i >= 0; --i)
        {
            MapInvalidation inv = _invalidations[i];
            if (inv.Type == type && inv.Tile == coord && inv.Overlaps(in bounds))
            {
                _invalidations.RemoveAt(i);
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
                        Logger.LogDebug("Added new bounds: " + inv.HeightmapBounds.Format() + ".");
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
        int sx = inv.XMax - inv.XMin + 1; // length in x direction
        int sy = inv.YMax - inv.YMin + 1; // length in y direction
        int ox = inv.XMin; // x offset from tileData
        int oy = inv.YMin; // y offset from tileData
        int tileSize = _dataType switch
        {
            DataType.Heightmap => Landscape.HEIGHTMAP_RESOLUTION,
            DataType.Splatmap => Landscape.SPLATMAP_RESOLUTION,
            _ => Landscape.HOLES_RESOLUTION
        } - 1;
        sy += 2;
        sx += 2;
        --ox;
        --oy;
        switch (_dataType)
        {
            case DataType.Heightmap:
                fixed (byte* buffer2 = _buffer)
                fixed (float* tileData = tile.heightmap)
                {
                    float* buffer = (float*)buffer2;
                    for (int x = 0; x < sx; ++x)
                        Buffer.MemoryCopy(tileData + (ox + x) * tileSize + oy, buffer + x * sy, sy * sizeof(float), sy * sizeof(float));
                }

                _bufferLen = sx * sy * sizeof(float);
                break;
            case DataType.Splatmap:
                fixed (byte* buffer2 = _buffer)
                fixed (float* tileData = tile.splatmap)
                {
                    float* buffer = (float*)buffer2;
                    for (int x = 0; x < sx; ++x)
                    {
                        Buffer.MemoryCopy(tileData + ((ox + x) * tileSize + oy) * Landscape.SPLATMAP_LAYERS,
                            buffer + (x * sy) * Landscape.SPLATMAP_LAYERS, sy * Landscape.SPLATMAP_LAYERS * sizeof(float),
                            sy * Landscape.SPLATMAP_LAYERS * sizeof(float));
                    }
                }

                _bufferLen = sx * sy * Landscape.SPLATMAP_LAYERS * sizeof(float);
                break;
            case DataType.Holes:
                fixed (byte* buffer = _buffer)
                fixed (bool* tileData = tile.holes)
                {
                    // compress the array to indivdual bits.
                    int bitCt = -1;
                    for (int x = 0; x < sx; ++x)
                    {
                        for (int y = 0; y < sy; ++y)
                            buffer[bitCt / 8] |= (byte)(tileData[(x + ox) * Landscape.HOLES_RESOLUTION + y + oy] ? 1 << (++bitCt % 8) : 0);
                    }
                    _bufferLen = Mathf.CeilToInt(bitCt / 8f);
                }

                fixed (byte* ptr2 = _buffer)
                fixed (bool* ptr3 = tile.holes)
                {
                    // compress the array to indivdual bits.
                    int bitCt = -1;
                    for (int x = inv.XMin; x <= inv.XMax; ++x)
                    {
                        for (int y = inv.YMin; y <= inv.YMax; ++y)
                            ptr2[bitCt / 8] |= (byte)(ptr3[x * Landscape.HOLES_RESOLUTION + y] ? 1 << (++bitCt % 8) : 0);
                    }

                    _bufferLen = Mathf.CeilToInt(bitCt / 8f);
                }

                break;
        }

        if (_dataType is DataType.Heightmap or DataType.Splatmap && !BitConverter.IsLittleEndian)
        {
            fixed (byte* ptr2 = _buffer)
            {
                for (int i = 0; i < _bufferLen; i += sizeof(float))
                    DevkitServerUtility.ReverseFloat(ptr2, i);
            }
        }

        Logger.LogDebug("[TILE SYNC]  Buffered " + DevkitServerUtility.FormatBytes(_bufferLen) + " (" + _dataType.ToString().ToUpperInvariant() + ").");
    }
    private unsafe void ApplyBuffer(LandscapeCoord coords, int sx, int sy, int ox, int oy)
    {
        _index = 0;
        LandscapeTile? tile = Landscape.getTile(coords);
        if (tile == null)
        {
            Logger.LogWarning("[TILE SYNC] Tile not found in ApplyBuffer: " + coords + ".");
            return;
        }
        if (_dataType is DataType.Heightmap or DataType.Splatmap && !BitConverter.IsLittleEndian)
        {
            fixed (byte* buffer = _buffer)
            {
                for (int i = 0; i < _buffer.Length; i += sizeof(float))
                    DevkitServerUtility.ReverseFloat(buffer, i);
            }
        }
        int tileSize = _dataType switch
        {
            DataType.Heightmap => Landscape.HEIGHTMAP_RESOLUTION,
            DataType.Splatmap => Landscape.SPLATMAP_RESOLUTION,
            _ => Landscape.HOLES_RESOLUTION
        } - 1;
        sy += 2;
        sx += 2;
        --ox;
        --oy;
        switch (_dataType)
        {
            case DataType.Heightmap:
                fixed (byte* buffer2 = _buffer)
                fixed (float* tileData = tile.heightmap)
                {
                    float* buffer = (float*)buffer2;
                    for (int x = 0; x < sx; ++x)
                        Buffer.MemoryCopy(buffer + x * sy, tileData + ((ox + x) * tileSize + oy), sy * sizeof(float), sy * sizeof(float));
                }

                Logger.LogDebug("[TILE SYNC] Applying heightmap to " + tile.coord + ".");
                tile.SetHeightsDelayLOD();
                LevelHierarchy.MarkDirty();
                break;
            case DataType.Splatmap:
                fixed (byte* buffer2 = _buffer)
                fixed (float* tileData = tile.splatmap)
                {
                    float* buffer = (float*)buffer2;
                    for (int x = 0; x < sx; ++x)
                        Buffer.MemoryCopy(buffer + (x * sy) * Landscape.SPLATMAP_LAYERS, tileData + ((ox + x) * tileSize + oy) * Landscape.SPLATMAP_LAYERS,
                            sy * Landscape.SPLATMAP_LAYERS * sizeof(float), sy * Landscape.SPLATMAP_LAYERS * sizeof(float));
                }

                Logger.LogDebug("[TILE SYNC] Applying splatmap to " + tile.coord + ".");
                tile.data.SetAlphamaps(0, 0, tile.splatmap);
                LevelHierarchy.MarkDirty();
                break;
            case DataType.Holes:
                fixed (byte* buffer = _buffer)
                fixed (bool* tileData = tile.holes)
                {
                    // decompress the array from indivdual bits.
                    int bitCt = -1;
                    for (int x = 0; x < sx; ++x)
                    {
                        for (int y = 0; y < sy; ++y)
                        {
                            bool val = (buffer[++bitCt / 8] & (1 << (bitCt % 8))) > 0;
                            tileData[(ox + x) * tileSize + oy + y] = val;
                            tile.hasAnyHolesData |= val;
                        }
                    }
                }

                Logger.LogDebug("[TILE SYNC] Applying holes to " + tile.coord + ".");
                tile.data.SetHoles(0, 0, tile.holes);
                LevelHierarchy.MarkDirty();
                break;
        }
    }
    [UsedImplicitly]
    private void Update()
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
        else if (_dataType != DataType.None && _index > -1 && time - _lastSent > 0.2f)
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

            int ox = inv.XMin;
            int oy = inv.YMin;
            int sx = inv.XMax - ox + 1;
            int sy = inv.YMax - oy + 1;
            ushort len = (ushort)Math.Min(_bufferLen - _index, MaxPacketSize);
            Buffer.BlockCopy(_buffer, _index, _packetBuffer, PacketOffset, len);
            UnsafeBitConverter.GetBytes(_packetBuffer,
#if SERVER
                0ul
#else
                Provider.client.m_SteamID
#endif
                , 0);
            _packetBuffer[sizeof(ulong)] = (byte)_dataType;
            UnsafeBitConverter.GetBytes(_packetBuffer, (ushort)_packetId, 1 + sizeof(ulong));
            ++_packetId;
            UnsafeBitConverter.GetBytes(_packetBuffer, tile.coord.x, sizeof(ushort) + 1 + sizeof(ulong));
            UnsafeBitConverter.GetBytes(_packetBuffer, tile.coord.y, sizeof(ushort) + 1 + sizeof(int) + sizeof(ulong));
            UnsafeBitConverter.GetBytes(_packetBuffer, sx, sizeof(ushort) + 1 + 2 * sizeof(int) + sizeof(ulong));
            UnsafeBitConverter.GetBytes(_packetBuffer, sy, sizeof(ushort) + 1 + 3 * sizeof(int) + sizeof(ulong));
            UnsafeBitConverter.GetBytes(_packetBuffer, ox, sizeof(ushort) + 1 + 4 * sizeof(int) + sizeof(ulong));
            UnsafeBitConverter.GetBytes(_packetBuffer, oy, sizeof(ushort) + 1 + 5 * sizeof(int) + sizeof(ulong));
            UnsafeBitConverter.GetBytes(_packetBuffer, len, sizeof(ushort) + 1 + 6 * sizeof(int) + sizeof(ulong));
            _index += len;
            NetFactory.SendGeneric(NetFactory.DevkitMessage.SendTileData, _packetBuffer, 
#if SERVER
                null,
#endif
                length: PacketOffset + len, reliable: true);
            Logger.LogDebug($"#{(_packetId - 1).Format()} Sizes: {sx.Format()}x{sy.Format()}, Offset: ({ox.Format()}, {oy.Format()}) Tile: {tile.coord.Format()}.");
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
        if (ServersideAuthorityTileSync.HasAuthority)
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
        if (ServersideAuthorityTileSync.HasAuthority)
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
        if (ServersideAuthorityTileSync.HasAuthority)
            ServersideAuthorityTileSync.InvalidateBounds(worldBounds, DataType.Holes, Time.realtimeSinceStartup);
#else
        if (EditorUser.User != null && EditorUser.User.TileSync != null && EditorUser.User.TileSync.HasAuthority)
            EditorUser.User.TileSync.InvalidateBounds(worldBounds, DataType.Holes, Time.realtimeSinceStartup);
#endif
    }
}