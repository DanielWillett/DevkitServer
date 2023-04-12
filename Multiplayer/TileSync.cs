﻿using JetBrains.Annotations;
using SDG.Framework.Landscapes;
using DevkitServer.Multiplayer.Networking;
#if CLIENT
using SDG.Framework.Devkit;
using SDG.NetPak;
#endif
#if SERVER
using HarmonyLib;
using DevkitServer.Util.Encoding;
#endif

namespace DevkitServer.Multiplayer;
#if SERVER
[HarmonyPatch]
#endif
public class TileSync : MonoBehaviour
{
    private const int MaxPacketSize = (int)(NetFactory.MaxPacketSize * 0.75f); // leave some space for other packets
    private const int PacketOffset = sizeof(ushort) + 1 + 7 * sizeof(int);
    public static int HeightmapPacketCount { get; }
    public static int SplatmapPacketCount { get; }
    public static int HolePacketCount { get; }
    public static int TotalHeightmapLength { get; }
    public static int TotalSplatmapLength { get; }
    public static int TotalHoleLength { get; }
     
    private DataType _dataType;
    public enum DataType
    {
        None,
        Heightmap,
        Splatmap,
        Holes
    }
#if CLIENT
    private int _index;
#else
    private int _index = -1;
#endif
#if SERVER
    private static readonly byte[] _packetBuffer = new byte[PacketOffset + MaxPacketSize];
    private int _packetId;
    private DataType _lastDataType;
    private float _lastSent;
    private int _hmIndex;
    private int _smIndex;
    private int _holeIndex;
#endif
    private static readonly byte[] _buffer;
    private static int _bufferLen;
    public static TileSync? Instance { get; private set; }
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

    [UsedImplicitly]
    private void Awake()
    {
        if (Instance != null)
            Destroy(Instance);
        
        Instance = this;
    }
#if SERVER
    private readonly List<MapInvalidation<HeightmapBounds>> _hmInvalidated = new List<MapInvalidation<HeightmapBounds>>(32);
    private readonly List<MapInvalidation<SplatmapBounds>> _smInvalidated = new List<MapInvalidation<SplatmapBounds>>(32);
    private readonly List<MapInvalidation<SplatmapBounds>> _holeInvalidated = new List<MapInvalidation<SplatmapBounds>>(32);
    private struct MapInvalidation<T> where T : unmanaged
    {
        public readonly LandscapeCoord Tile;
        public T Bounds;
        public float Time;
        public MapInvalidation(LandscapeCoord tile, T bounds, float time)
        {
            Tile = tile;
            Bounds = bounds;
            Time = time;
        }
    }
#endif
#if CLIENT
    private void ReceiveTileData(byte[] data, int offset)
    {
        _dataType = (DataType)data[offset];
        int packetId = BitConverter.ToUInt16(data, offset + 1);
        LandscapeCoord coords = new LandscapeCoord(BitConverter.ToInt32(data, offset + 1 + sizeof(ushort)), BitConverter.ToInt32(data, offset + sizeof(ushort) + 1 + sizeof(int)));
        int sx = BitConverter.ToInt32(data, offset + sizeof(ushort) + 1 + sizeof(int) * 2);
        int sy = BitConverter.ToInt32(data, offset + sizeof(ushort) + 1 + sizeof(int) * 3);
        int ox = BitConverter.ToInt32(data, offset + sizeof(ushort) + 1 + sizeof(int) * 4);
        int oy = BitConverter.ToInt32(data, offset + sizeof(ushort) + 1 + sizeof(int) * 5);
        int len = BitConverter.ToInt32(data, offset + sizeof(ushort) + 1 + sizeof(int) * 6);
        _bufferLen = _dataType switch
        {
            DataType.Heightmap => (sx + 1) * (sy + 1) * sizeof(float),
            DataType.Splatmap => (sx + 1) * (sy + 1) * Landscape.SPLATMAP_LAYERS * sizeof(float),
            _ => Mathf.CeilToInt((sx + 1) * (sy + 1) / 8f)
        };
        Logger.LogDebug($"[TILE SYNC] Receiving packet #{packetId} for tile {coords} ({DevkitServerUtility.FormatBytes(len)} @ {DevkitServerUtility.FormatBytes(_index)})");
        if (_index + len > _bufferLen)
            len = _bufferLen - _index;
        Logger.LogDebug($"[TILE SYNC] srcLen: {data.Length}, srcInd: {offset + PacketOffset}, dstLen: {_buffer.Length} ({_bufferLen}), dstInd: {_index}, count: {len}.");
        Buffer.BlockCopy(data, offset + PacketOffset, _buffer, _index, len);
        _index += len;
        if (len + _index >= _bufferLen)
        {
            FinalizeBuffer(coords, sx, sy, ox, oy);
            Logger.LogDebug($"[TILE SYNC] Finalizing tile {coords}.");
        }
    }
    private unsafe void FinalizeBuffer(LandscapeCoord coords, int sx, int sy, int ox, int oy)
    {
        _index = 0;
        LandscapeTile? tile = Landscape.getTile(coords);
        if (tile == null)
        {
            Logger.LogWarning("[TILE SYNC] Tile not found in FinalizeBuffer: " + coords + ".");
            return;
        }

        switch (_dataType)
        {
            case DataType.Heightmap:
                fixed (byte* ptr2 = _buffer)
                fixed (float* ptr3 = tile.heightmap)
                {
                    if (!BitConverter.IsLittleEndian)
                    {
                        for (int i = 0; i < _buffer.Length; i += sizeof(float))
                            DevkitServerUtility.ReverseFloat(ptr2, i);
                    }
                    int len = (sy + 1) * sizeof(float);
                    for (int x = ox; x <= ox + sx; ++x)
                    {
                        int offset = (x - ox) * (sy + 1) * sizeof(float);
                        Buffer.MemoryCopy(ptr2 + offset, ptr3 + x * (sy + 1) * sizeof(float), _buffer.Length - offset, len);
                    }
                }

                Logger.LogDebug("[TILE SYNC] Applying heightmap to " + tile.coord + ".");
                tile.SetHeightsDelayLOD();
                LevelHierarchy.MarkDirty();
                break;
            case DataType.Splatmap:
                fixed (byte* ptr2 = _buffer)
                fixed (float* ptr3 = tile.splatmap)
                {
                    if (!BitConverter.IsLittleEndian)
                    {
                        for (int i = 0; i < _buffer.Length; i += sizeof(float))
                            DevkitServerUtility.ReverseFloat(ptr2, i);
                    }
                    int len = (sy + 1) * Landscape.SPLATMAP_LAYERS * sizeof(float);
                    for (int x = ox; x <= ox + sx; ++x)
                    {
                        int offset = (x - ox) * (sy + 1) * Landscape.SPLATMAP_LAYERS * sizeof(float);
                        Buffer.MemoryCopy(ptr2 + offset, ptr3 + x * (sy + 1) * Landscape.SPLATMAP_LAYERS * sizeof(float), _buffer.Length - offset, len);
                    }
                }

                Logger.LogDebug("[TILE SYNC] Applying splatmap to " + tile.coord + ".");
                tile.data.SetAlphamaps(0, 0, tile.splatmap);
                LevelHierarchy.MarkDirty();
                break;
            case DataType.Holes:
                fixed (byte* ptr2 = _buffer)
                fixed (bool* ptr3 = tile.holes)
                {
                    // decompress the array from indivdual bits.
                    int bitCt = -1;
                    for (int x = ox; x <= ox + sx; ++x)
                    {
                        for (int y = oy; y <= oy + sy; ++y)
                        {
                            bool val = (ptr2[++bitCt / 8] & (1 << (bitCt % 8))) > 0;
                            ptr3[x * Landscape.HOLES_RESOLUTION + y] = val;
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
    internal static void ReceiveTileData(NetPakReader reader)
    {
        if (Instance == null)
            return;
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

        Instance?.ReceiveTileData(buffer, offset);
    }
#endif
#if SERVER
    private static bool AddHeightmapBounds(List<MapInvalidation<HeightmapBounds>> list, LandscapeCoord coord, HeightmapBounds bounds, float time)
    {
        for (int i = list.Count - 1; i >= 0; --i)
        {
            MapInvalidation<HeightmapBounds> inv = list[i];
            if (inv.Tile == coord && inv.Bounds.Overlaps(in bounds))
            {
                list.RemoveAt(i);
                HeightmapBounds b3 = inv.Bounds;
                Logger.LogDebug("[TILE SYNC]   Combined bounds " + b3 + " <- " + bounds + ".");
                b3.Encapsulate(bounds);
                Logger.LogDebug("[TILE SYNC]    Result: " + b3 + ".");
                if (AddHeightmapBounds(list, coord, b3, time))
                    return true;

                inv.Bounds = b3;
                inv.Time = time;
                list.Add(inv);
                Logger.LogDebug("[TILE SYNC]    Re-added: " + b3 + ".");
                return true;
            }
        }

        return false;
    }
    private static bool AddSplatmapBounds(List<MapInvalidation<SplatmapBounds>> list, LandscapeCoord coord, SplatmapBounds bounds, float time)
    {
        for (int i = list.Count - 1; i >= 0; --i)
        {
            MapInvalidation<SplatmapBounds> inv = list[i];
            if (inv.Tile == coord && inv.Bounds.Overlaps(in bounds))
            {
                list.RemoveAt(i);
                SplatmapBounds b3 = inv.Bounds;
                Logger.LogDebug("[TILE SYNC]   Combined bounds " + b3 + " <- " + bounds + ".");
                b3.Encapsulate(bounds);
                Logger.LogDebug("[TILE SYNC]    Result: " + b3 + ".");
                if (AddSplatmapBounds(list, coord, b3, time))
                    return true;

                inv.Bounds = b3;
                inv.Time = time;
                list[i] = inv;
                list.Add(inv);
                Logger.LogDebug("[TILE SYNC]    Re-added: " + b3 + ".");
                return true;
            }
        }

        return false;
    }
    public void InvalidateBounds(Bounds bounds, DataType type, float time)
    {
        Logger.LogDebug("[TILE SYNC] Invalidating bounds " + bounds + " for " + type + ".");
        LandscapeBounds b2 = new LandscapeBounds(bounds);
        for (int x1 = b2.min.x; x1 <= b2.max.x; ++x1)
        {
            for (int y1 = b2.min.y; y1 <= b2.max.y; ++y1)
            {
                LandscapeCoord c2 = new LandscapeCoord(x1, y1);
                Logger.LogDebug("[TILE SYNC]  Checking tile " + c2 + ".");
                if (Landscape.getTile(c2) != null)
                {
                    switch (type)
                    {
                        case DataType.Heightmap:
                            HeightmapBounds hmBounds = new HeightmapBounds(c2, bounds);
                            if (!AddHeightmapBounds(_hmInvalidated, c2, hmBounds, time))
                            {
                                _hmInvalidated.Add(new MapInvalidation<HeightmapBounds>(c2, hmBounds, time));
                                Logger.LogDebug("[TILE SYNC]   Added bounds: " + _hmInvalidated.TailOrDefault().Bounds + ".");
                            }
                            
                            break;
                        case DataType.Splatmap:
                            SplatmapBounds smBounds = new SplatmapBounds(c2, bounds);

                            if (!AddSplatmapBounds(_smInvalidated, c2, smBounds, time))
                            {
                                _smInvalidated.Add(new MapInvalidation<SplatmapBounds>(c2, smBounds, time));
                                Logger.LogDebug("[TILE SYNC]   Added bounds: " + _smInvalidated.TailOrDefault().Bounds + ".");
                            }
                            
                            break;
                        case DataType.Holes:
                            smBounds = new SplatmapBounds(c2, bounds);

                            if (!AddSplatmapBounds(_holeInvalidated, c2, smBounds, time))
                            {
                                _holeInvalidated.Add(new MapInvalidation<SplatmapBounds>(c2, smBounds, time));
                                Logger.LogDebug("[TILE SYNC]   Added bounds: " + _holeInvalidated.TailOrDefault().Bounds + ".");
                            }
                            
                            break;
                    }
                }
            }
        }
    }
    private unsafe void BufferData()
    {
        _index = 0;
        switch (_dataType)
        {
            case DataType.Heightmap:
                Logger.LogDebug("[TILE SYNC] Buffering " + _dataType + "[" + _hmIndex + "].");
                MapInvalidation<HeightmapBounds> inv = _hmInvalidated[_hmIndex];
                LandscapeTile? tile = Landscape.getTile(inv.Tile);
                if (tile == null)
                {
                    Logger.LogWarning("[TILE SYNC]  Tile not found in BufferData (HM): " + inv.Tile + ".");
                    return;
                }
                int sy = inv.Bounds.max.y - inv.Bounds.min.y;
                fixed (byte* ptr2 = _buffer)
                fixed (float* ptr3 = tile.heightmap)
                {
                    int len = (sy + 1) * sizeof(float);
                    for (int x = inv.Bounds.min.x; x <= inv.Bounds.max.x; ++x)
                    {
                        int offset = (x - inv.Bounds.min.x) * (sy + 1) * sizeof(float);
                        Logger.LogDebug($"[TILE SYNC]   Offset: {offset}, Length: {len}, BufferLen: {offset + len}, hm offset = {x * (sy + 1) * sizeof(float)}.");
                        Buffer.MemoryCopy(ptr3 + x * (sy + 1) * sizeof(float), ptr2 + offset, _buffer.Length - offset, len);
                        _bufferLen = offset + len;
                    }
                    Logger.LogDebug("[TILE SYNC]  Buffered " + DevkitServerUtility.FormatBytes(_bufferLen) + " (HM).");
                    if (!BitConverter.IsLittleEndian)
                    {
                        for (int i = 0; i < _bufferLen; i += sizeof(float))
                            DevkitServerUtility.ReverseFloat(ptr2, i);
                    }
                }
                break;
            case DataType.Splatmap:
                Logger.LogDebug("[TILE SYNC]  Buffering " + _dataType + "[" + _smIndex + "].");
                MapInvalidation<SplatmapBounds> inv2 = _smInvalidated[_smIndex];
                tile = Landscape.getTile(inv2.Tile);
                if (tile == null)
                {
                    Logger.LogWarning("[TILE SYNC]  Tile not found in BufferData (SM): " + inv2.Tile + ".");
                    return;
                }
                sy = inv2.Bounds.max.y - inv2.Bounds.min.y;
                fixed (byte* ptr2 = _buffer)
                fixed (float* ptr3 = tile.splatmap)
                {
                    int len = (sy + 1) * Landscape.SPLATMAP_LAYERS * sizeof(float);
                    for (int x = inv2.Bounds.min.x; x <= inv2.Bounds.max.x; ++x)
                    {
                        int offset = (x - inv2.Bounds.min.x) * (sy + 1) * Landscape.SPLATMAP_LAYERS * sizeof(float);
                        Buffer.MemoryCopy(ptr3 + x * (sy + 1) * Landscape.SPLATMAP_LAYERS * sizeof(float), ptr2 + offset, _buffer.Length - offset, len);
                        _bufferLen = offset + len;
                    }
                    Logger.LogDebug("[TILE SYNC]  Buffered " + DevkitServerUtility.FormatBytes(_bufferLen) + " (SM).");
                    if (!BitConverter.IsLittleEndian)
                    {
                        for (int i = 0; i < _bufferLen; i += sizeof(float))
                            DevkitServerUtility.ReverseFloat(ptr2, i);
                    }
                }
                break;
            case DataType.Holes:
                Logger.LogDebug("[TILE SYNC] Buffering " + _dataType + "[" + _holeIndex + "].");
                inv2 = _holeInvalidated[_holeIndex];
                tile = Landscape.getTile(inv2.Tile);
                if (tile == null)
                {
                    Logger.LogWarning("[TILE SYNC]  Tile not found in BufferData (HOLES): " + inv2.Tile + ".");
                    return;
                }
                fixed (byte* ptr2 = _buffer)
                fixed (bool* ptr3 = tile.holes)
                {
                    // compress the array to indivdual bits.
                    int bitCt = -1;
                    for (int x = inv2.Bounds.min.x; x <= inv2.Bounds.max.x; ++x)
                    {
                        for (int y = inv2.Bounds.min.y; y <= inv2.Bounds.max.y; ++y)
                            ptr2[bitCt / 8] |= (byte)(ptr3[x * Landscape.HOLES_RESOLUTION + y] ? 1 << (++bitCt % 8) : 0);
                    }

                    _bufferLen = Mathf.CeilToInt(bitCt / 8f);
                    Logger.LogDebug("[TILE SYNC]  Buffered " + DevkitServerUtility.FormatBytes(_bufferLen) + " (HOLES).");
                }

                break;
        }
    }
    [UsedImplicitly]
    private void Update()
    {
        if (Provider.clients.Count == 0)
            return;
        float time = Time.realtimeSinceStartup;
        if (_dataType == DataType.None && time - _lastSent > 5f)
        {
            try
            {
                _lastSent = time;
                _lastDataType = _dataType = (DataType)(((int)_lastDataType % 3) + 1);
                Logger.LogDebug("[TILE SYNC] New data type: " + _dataType + ".");
                switch (_dataType)
                {
                    case DataType.Heightmap:
                        if (_hmInvalidated.Count == 0)
                        {
                            Logger.LogDebug("[TILE SYNC] No heightmaps.");
                            _dataType = DataType.None;
                            return;
                        }

                        while (_hmIndex < _hmInvalidated.Count && time - _hmInvalidated[_hmIndex].Time < 5f)
                            ++_hmIndex;
                        if (_hmIndex >= _hmInvalidated.Count)
                        {
                            _hmIndex = 0;
                            Logger.LogDebug("[TILE SYNC] No available heightmaps.");
                            _dataType = DataType.None;
                            return;
                        }
                        Logger.LogDebug("[TILE SYNC] Next heightmap: " + _hmIndex + ".");
                        break;
                    case DataType.Splatmap:
                        if (_smInvalidated.Count == 0)
                        {
                            Logger.LogDebug("[TILE SYNC] No splatmaps.");
                            _dataType = DataType.None;
                            return;
                        }

                        while (_smIndex < _smInvalidated.Count && time - _smInvalidated[_smIndex].Time < 5f)
                            ++_smIndex;
                        if (_smIndex >= _smInvalidated.Count)
                        {
                            _smIndex = 0;
                            Logger.LogDebug("[TILE SYNC] No available splatmaps.");
                            _dataType = DataType.None;
                            return;
                        }
                        Logger.LogDebug("[TILE SYNC] Next splatmap: " + _smIndex + ".");
                        break;
                    case DataType.Holes:
                        if (_holeInvalidated.Count == 0)
                        {
                            Logger.LogDebug("[TILE SYNC] No holes.");
                            _dataType = DataType.None;
                            return;
                        }

                        while (_holeIndex < _holeInvalidated.Count && time - _holeInvalidated[_holeIndex].Time < 5f)
                            ++_holeIndex;
                        if (_holeIndex >= _holeInvalidated.Count)
                        {
                            _holeIndex = 0;
                            Logger.LogDebug("[TILE SYNC] No available holes.");
                            _dataType = DataType.None;
                            return;
                        }
                        Logger.LogDebug("[TILE SYNC] Next holes: " + _holeIndex + ".");
                        break;
                }

                BufferData();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
                _dataType = DataType.None;
            }
        }
        else if (_dataType != DataType.None && _index > -1 && time - _lastSent > 0.2)
        {
            LandscapeCoord coord = _dataType switch
            {
                DataType.Heightmap => _hmInvalidated[_hmIndex].Tile,
                DataType.Splatmap => _smInvalidated[_smIndex].Tile,
                _ => _holeInvalidated[_holeIndex].Tile
            };
            LandscapeTile? tile = Landscape.getTile(coord);
            if (tile == null)
            {
                Logger.LogWarning("[TILE SYNC] Tile not found in BufferData (HOLES): " + coord + ".");
            }
            else
            {
                int ox = _dataType switch
                {
                    DataType.Heightmap => _hmInvalidated[_hmIndex].Bounds.min.x,
                    DataType.Splatmap => _smInvalidated[_smIndex].Bounds.min.x,
                    _ => _holeInvalidated[_holeIndex].Bounds.min.x
                };
                int oy = _dataType switch
                {
                    DataType.Heightmap => _hmInvalidated[_hmIndex].Bounds.min.y,
                    DataType.Splatmap => _smInvalidated[_smIndex].Bounds.min.y,
                    _ => _holeInvalidated[_holeIndex].Bounds.min.y
                };
                int sx = _dataType switch
                {
                    DataType.Heightmap => _hmInvalidated[_hmIndex].Bounds.max.x - ox,
                    DataType.Splatmap => _smInvalidated[_smIndex].Bounds.max.x - ox,
                    _ => _holeInvalidated[_holeIndex].Bounds.max.x - ox
                };
                int sy = _dataType switch
                {
                    DataType.Heightmap => _hmInvalidated[_hmIndex].Bounds.max.y - oy,
                    DataType.Splatmap => _smInvalidated[_smIndex].Bounds.max.y - oy,
                    _ => _holeInvalidated[_holeIndex].Bounds.max.y - oy
                };
                _lastSent = time;
                ushort len = (ushort)Math.Min(_bufferLen - _index, MaxPacketSize);
                Buffer.BlockCopy(_buffer, _index, _packetBuffer, PacketOffset, len);
                _packetBuffer[0] = (byte)_dataType;
                UnsafeBitConverter.GetBytes(_packetBuffer, (ushort)_packetId, 1);
                ++_packetId;
                UnsafeBitConverter.GetBytes(_packetBuffer, tile.coord.x, sizeof(ushort) + 1);
                UnsafeBitConverter.GetBytes(_packetBuffer, tile.coord.y, sizeof(ushort) + 1 + sizeof(int));
                UnsafeBitConverter.GetBytes(_packetBuffer, sx, sizeof(ushort) + 1 + 2 * sizeof(int));
                UnsafeBitConverter.GetBytes(_packetBuffer, sy, sizeof(ushort) + 1 + 3 * sizeof(int));
                UnsafeBitConverter.GetBytes(_packetBuffer, ox, sizeof(ushort) + 1 + 4 * sizeof(int));
                UnsafeBitConverter.GetBytes(_packetBuffer, oy, sizeof(ushort) + 1 + 5 * sizeof(int));
                UnsafeBitConverter.GetBytes(_packetBuffer, len, sizeof(ushort) + 1 + 6 * sizeof(int));
                _index += len;
                NetFactory.SendGeneric(NetFactory.DevkitMessage.SendTileData, _packetBuffer, null, length: PacketOffset + len);
            }

            // flush buffer
            if (_index >= _bufferLen || tile == null)
            {
                _lastDataType = _dataType;
                if (_dataType == DataType.Heightmap)
                {
                    _hmInvalidated.RemoveAt(_hmIndex);
                }
                else if (_dataType == DataType.Splatmap)
                {
                    _smInvalidated.RemoveAt(_smIndex);
                }
                else
                {
                    _holeInvalidated.RemoveAt(_holeIndex);
                }
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
        Instance?.InvalidateBounds(worldBounds, DataType.Heightmap, Time.realtimeSinceStartup);
    }

    /*
    [HarmonyPatch(typeof(Landscape), nameof(Landscape.writeHeightmap))]
    [HarmonyTranspiler]
    [UsedImplicitly]
    private static IEnumerable<CodeInstruction> WriteHeightmapTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase method)
    {
        Type arrType = typeof(float).MakeArrayType(2);

        MethodInfo onUpdatedMethod = typeof(TileSync).GetMethod(nameof(OnTileUpdated), BindingFlags.Static | BindingFlags.NonPublic)!;

        MethodInfo? setMethod = arrType.GetMethod("Set", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (setMethod == null)
        {
            Logger.LogWarning("[TILE SYNC] Unable to find float[,].Set.");
            DevkitServerModule.Fault();
        }
        
        MethodInfo? getMethod = arrType.GetMethod("Get", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (getMethod == null)
        {
            Logger.LogWarning("[TILE SYNC] Unable to find float[,].Get.");
            DevkitServerModule.Fault();
        }
        
        FieldInfo? heightmapField = typeof(LandscapeTile).GetField("heightmap", BindingFlags.Instance | BindingFlags.Public);
        if (heightmapField == null)
        {
            Logger.LogWarning("[TILE SYNC] Unable to find LanscapeTile.heightmap.");
            DevkitServerModule.Fault();
        }

        List<CodeInstruction> list = new List<CodeInstruction>(instructions);
        bool first = false;
        int num = 0;
        Label? nextLbl = null;
        for (int i = 0; i < list.Count; ++i)
        {
            CodeInstruction c = list[i];
            if (nextLbl.HasValue)
            {
                c.labels.Add(nextLbl.Value);
                nextLbl = null;
            }
            yield return c;
            if (setMethod != null && heightmapField != null)
            {
                if (c.Calls(setMethod))
                {
                    if (!first)
                    {
                        LocalBuilder? oldHeight = null;
                        int oldHeightInd = -1;
                        LocalBuilder? x = null;
                        int xInd = -1;
                        LocalBuilder? y = null;
                        int yInd = -1;
                        LocalBuilder? tile = null;
                        int tileInd = -1;
                        // find locals
                        for (int j = i - 1; j >= 4; --j)
                        {
                            if (list[j].Calls(getMethod))
                            {
                                if (list[j + 1].IsStloc())
                                    oldHeight = DevkitServerUtility.GetLocal(list[j + 1], out oldHeightInd, true);
                                
                                if (list[j - 2].IsLdloc())
                                    x = DevkitServerUtility.GetLocal(list[j - 2], out xInd, false);
                                
                                if (list[j - 1].IsLdloc())
                                    y = DevkitServerUtility.GetLocal(list[j - 1], out yInd, false);
                                
                                if (list[j - 4].IsLdloc())
                                    tile = DevkitServerUtility.GetLocal(list[j - 4], out tileInd, false);
                                
                                break;
                            }
                        }

                        if ((oldHeight != null || oldHeightInd >= 0) && (x != null || xInd >= 0) && (y != null || yInd >= 0) && (tile != null || tileInd >= 0))
                        {
                            Label label = generator.DefineLabel();
                            yield return DevkitServerUtility.GetLocalCodeInstruction(tile, tileInd, false);
                            yield return new CodeInstruction(OpCodes.Ldfld, heightmapField);
                            yield return DevkitServerUtility.GetLocalCodeInstruction(x, xInd, false);
                            yield return DevkitServerUtility.GetLocalCodeInstruction(y, yInd, false);
                            yield return new CodeInstruction(OpCodes.Call, getMethod);
                            yield return DevkitServerUtility.GetLocalCodeInstruction(oldHeight, oldHeightInd, false);
                            yield return new CodeInstruction(OpCodes.Beq, label);
                            yield return DevkitServerUtility.GetLocalCodeInstruction(tile, tileInd, false);
                            yield return DevkitServerUtility.LoadConstantI4((int)DataType.Heightmap);
                            yield return new CodeInstruction(OpCodes.Call, onUpdatedMethod);
                            nextLbl = label;
                            Logger.LogInfo("[TILE SYNC] Inserted OnUpdated call to original edit method in " + method.Format() + ".");
                            first = true;
                        }
                        else
                        {
                            Logger.LogWarning($"[TILE SYNC] Locals not found: oldHeight: {oldHeight != null || oldHeightInd >= 0}, x: {x != null || xInd >= 0}, y: {y != null || yInd >= 0}, tile: {tile != null || tileInd >= 0}.");
                        }
                    }
                }
                else if (first && i > 8 && i < list.Count - 2 && c.Calls(getMethod) && list[i + 1].Calls(setMethod))
                {
                    LocalBuilder? tile = null;
                    int tileInd = -1;
                    LocalBuilder? index = null;
                    int indexInd = -1;
                    if (list[i - 8].IsLdloc())
                        tile = DevkitServerUtility.GetLocal(list[i - 8], out tileInd, false);
                    
                    if (num is 0 or 1)
                        index = DevkitServerUtility.GetLocal(list[i - num switch { 0 => 2, 1 => 1, _ => 0 }], out indexInd, false);

                    if ((tile != null || tileInd >= 0) && (num == 2 || index != null || indexInd >= 0) && num < 3)
                    {
                        Label label = generator.DefineLabel();
                        Label label2 = generator.DefineLabel();
                        yield return new CodeInstruction(OpCodes.Dup); // new
                        yield return DevkitServerUtility.GetLocalCodeInstruction(tile, tileInd, false);
                        yield return new CodeInstruction(OpCodes.Ldfld, heightmapField);
                        if (num == 0)
                        {
                            yield return DevkitServerUtility.GetLocalCodeInstruction(index!, indexInd, false);
                            yield return new CodeInstruction(OpCodes.Ldc_I4, Landscape.HEIGHTMAP_RESOLUTION_MINUS_ONE);
                        }
                        else if (num == 1)
                        {
                            yield return new CodeInstruction(OpCodes.Ldc_I4, Landscape.HEIGHTMAP_RESOLUTION_MINUS_ONE);
                            yield return DevkitServerUtility.GetLocalCodeInstruction(index!, indexInd, false);
                        }
                        else
                        {
                            yield return new CodeInstruction(OpCodes.Ldc_I4, Landscape.HEIGHTMAP_RESOLUTION_MINUS_ONE);
                            yield return new CodeInstruction(OpCodes.Ldc_I4, Landscape.HEIGHTMAP_RESOLUTION_MINUS_ONE);
                        }
                        yield return new CodeInstruction(OpCodes.Call, getMethod); // old
                        yield return new CodeInstruction(OpCodes.Beq, label);
                        yield return list[i + 1];
                        ++i;
                        yield return DevkitServerUtility.GetLocalCodeInstruction(tile, tileInd, false);
                        yield return DevkitServerUtility.LoadConstantI4((int)DataType.Heightmap);
                        yield return new CodeInstruction(OpCodes.Call, onUpdatedMethod);
                        yield return new CodeInstruction(OpCodes.Br, label2);
                        CodeInstruction pop = new CodeInstruction(OpCodes.Pop);
                        pop.labels.Add(label);
                        yield return pop;
                        yield return new CodeInstruction(OpCodes.Pop);
                        yield return new CodeInstruction(OpCodes.Pop);
                        yield return new CodeInstruction(OpCodes.Pop);
                        nextLbl = label2;
                        ++num;
                        Logger.LogInfo("[TILE SYNC] Inserted OnUpdated call to neighbor #" + num + " edit method in " + method.Format() + ".");
                    }
                    else
                    {
                        Logger.LogWarning($"[TILE SYNC] Locals not found (#{num}): index: {index != null || indexInd >= 0}, tile: {tile != null || tileInd >= 0}.");
                    }
                }
            }
        }
        if (!first)
        {
            Logger.LogWarning("[TILE SYNC] Unable to insert original OnUpdated call in " + method.Format() + ".");
            DevkitServerModule.Fault();
        }
        if (num != 3)
        {
            Logger.LogWarning("[TILE SYNC] Unable to insert OnUpdated call to neighbors in " + method.Format() + ".");
            DevkitServerModule.Fault();
        }
    }
    */
#endif
}