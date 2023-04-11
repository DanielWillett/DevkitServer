using JetBrains.Annotations;
using SDG.Framework.Landscapes;
using DevkitServer.Multiplayer.Networking;
using SDG.Framework.Devkit;
using SDG.NetPak;
#if SERVER
using HarmonyLib;
using DevkitServer.Util.Encoding;
using System.Reflection;
using System.Reflection.Emit;
#endif

namespace DevkitServer.Multiplayer;
#if SERVER
[HarmonyPatch]
#endif
public class TileSync : MonoBehaviour
{
    private const int MaxPacketSize = (int)(NetFactory.MaxPacketSize * 0.75f); // leave some space for other packets
    private const int PacketOffset = sizeof(ushort) + sizeof(ushort) + 1 + 2 * sizeof(int);
    public static int HeightmapPacketCount { get; }
    public static int SplatmapPacketCount { get; }
    public static int HolePacketCount { get; }
    public static int TotalHeightmapLength { get; }
    public static int TotalSplatmapLength { get; }
    public static int TotalHoleLength { get; }

    private Syncing _syncing;
    private enum Syncing
    {
        None,
        Splatmap,
        Heightmap,
        Holes
    }
#if SERVER
    private static readonly byte[] _packetBuffer = new byte[PacketOffset + MaxPacketSize];
    private int _index = -1;
    private int _packetId;
    private Syncing _lastSyncing;
    private float _lastSent;
    private int _hmIndex = -1;
    private int _smIndex = -1;
    private int _holeIndex = -1;
#endif
    private static readonly byte[] _hmBuffer = new byte[TotalHeightmapLength];
    private static readonly byte[] _smBuffer = new byte[TotalSplatmapLength];
    private static readonly byte[] _holeBuffer = new byte[TotalHoleLength];
    public static TileSync? Instance { get; private set; }
    static TileSync()
    {
        TotalHeightmapLength = Landscape.HEIGHTMAP_RESOLUTION * Landscape.HEIGHTMAP_RESOLUTION * sizeof(float);
        TotalSplatmapLength = Landscape.SPLATMAP_RESOLUTION * Landscape.SPLATMAP_RESOLUTION * Landscape.SPLATMAP_LAYERS * sizeof(float);
        TotalHoleLength = Mathf.CeilToInt(Landscape.HOLES_RESOLUTION * Landscape.HOLES_RESOLUTION / 8f);
        HeightmapPacketCount = Mathf.CeilToInt((float)TotalHeightmapLength / MaxPacketSize);
        SplatmapPacketCount = Mathf.CeilToInt((float)TotalSplatmapLength / MaxPacketSize);
        HolePacketCount = Mathf.CeilToInt((float)TotalHoleLength / MaxPacketSize);
    }

    [UsedImplicitly]
    private void Awake()
    {
        if (Instance != null)
            Destroy(Instance);
        
        Instance = this;
    }
#if SERVER
    private readonly List<KeyValuePair<LandscapeTile, float>> _hmTiles = new List<KeyValuePair<LandscapeTile, float>>(8);
    private readonly List<KeyValuePair<LandscapeTile, float>> _smTiles = new List<KeyValuePair<LandscapeTile, float>>(8);
    private readonly List<KeyValuePair<LandscapeTile, float>> _holeTiles = new List<KeyValuePair<LandscapeTile, float>>(2);
#endif
#if CLIENT
    private void ReceiveTileData(byte[] data, int offset)
    {
        _syncing = (Syncing)data[offset];
        int packetId = BitConverter.ToInt16(data, offset + 1);
        LandscapeCoord coords = new LandscapeCoord(BitConverter.ToInt32(data, offset + 1 + sizeof(ushort)), BitConverter.ToInt32(data, offset + sizeof(ushort) + 1 + sizeof(int)));
        int len = BitConverter.ToInt32(data, PacketOffset - sizeof(ushort));
        int index = packetId * MaxPacketSize;
        Logger.LogDebug($"Receiving packet #{packetId} for tile {coords} ({DevkitServerUtility.FormatBytes(len)} @ {DevkitServerUtility.FormatBytes(index)})");
        byte[] buffer = _syncing switch
        {
            Syncing.Heightmap => _hmBuffer,
            Syncing.Splatmap => _smBuffer,
            _ => _holeBuffer
        };
        if (index + len > buffer.Length)
            len = buffer.Length - index;
        Buffer.BlockCopy(data, offset + PacketOffset, buffer, index, len);
        if (len + index >= buffer.Length)
        {
            FinalizeBuffer(coords);
            Logger.LogDebug($"Finalizing tile {coords}.");
        }
    }
    private unsafe void FinalizeBuffer(LandscapeCoord coords)
    {
        byte[] buffer = _syncing switch
        {
            Syncing.Heightmap => _hmBuffer,
            Syncing.Splatmap => _smBuffer,
            _ => _holeBuffer
        };
        LandscapeTile? tile = Landscape.getTile(coords);
        if (tile == null)
        {
            Logger.LogWarning("[TILE SYNC] Tile not found in FinalizeBuffer: " + coords + ".");
            return;
        }
        switch (_syncing)
        {
            case Syncing.Heightmap:
                fixed (byte* ptr2 = buffer)
                fixed (float* ptr3 = tile.heightmap)
                {
                    if (!BitConverter.IsLittleEndian)
                    {
                        for (int i = 0; i < buffer.Length; i += sizeof(float))
                            DevkitServerUtility.ReverseFloat(ptr2, i);
                    }
                    Buffer.MemoryCopy(ptr2, ptr3, TotalHeightmapLength, buffer.Length);
                }

                tile.SetHeightsDelayLOD();
                LevelHierarchy.MarkDirty();
                break;
            case Syncing.Splatmap:
                fixed (byte* ptr2 = buffer)
                fixed (float* ptr3 = tile.splatmap)
                {
                    if (!BitConverter.IsLittleEndian)
                    {
                        for (int i = 0; i < buffer.Length; i += sizeof(float))
                            DevkitServerUtility.ReverseFloat(ptr2, i);
                    }
                    Buffer.MemoryCopy(ptr2, ptr3, TotalSplatmapLength, buffer.Length);
                }

                tile.data.SetAlphamaps(0, 0, tile.splatmap);
                LevelHierarchy.MarkDirty();
                break;
            case Syncing.Holes:
                fixed (byte* ptr2 = buffer)
                fixed (bool* ptr3 = tile.holes)
                {
                    // decompress the array from indivdual bits.
                    const int c = Landscape.HOLES_RESOLUTION * Landscape.HOLES_RESOLUTION;
                    for (int i = 0; i < c; ++i)
                        ptr3[i] = (ptr2[i / 8] & (1 << (i % 8))) > 0;
                }
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
    [UsedImplicitly]
    private unsafe void Update()
    {
        float time = Time.realtimeSinceStartup;

        if (_syncing == Syncing.None && time - _lastSent > 60f)
        {
            _syncing = (Syncing)(((int)_lastSyncing % 3) + 1);
            int c = 0;
            int index;
            List<KeyValuePair<LandscapeTile, float>> tiles;
            byte[] buffer;
            do
            {
                switch (_syncing)
                {
                    case Syncing.Heightmap:
                        if (_hmTiles.Count <= _hmIndex)
                        {
                            if (_hmTiles.Count > 0)
                                _hmTiles.Clear();
                            _hmIndex = 0;
                            _syncing = Syncing.Splatmap;
                            if (++c < 3)
                                goto case Syncing.Splatmap;
                            return;
                        }

                        index = _hmIndex;
                        break;
                    case Syncing.Splatmap:
                        if (_smTiles.Count <= _smIndex)
                        {
                            if (_smTiles.Count > 0)
                                _smTiles.Clear();
                            _smIndex = 0;
                            _syncing = Syncing.Holes;
                            if (++c < 3)
                                goto case Syncing.Holes;
                            return;
                        }

                        index = _smIndex;
                        break;
                    case Syncing.Holes:
                        if (_holeTiles.Count <= _holeIndex)
                        {
                            if (_holeTiles.Count > 0)
                                _holeTiles.Clear();
                            _holeIndex = 0;
                            _syncing = Syncing.Heightmap;
                            if (++c < 3)
                                goto case Syncing.Heightmap;
                            return;
                        }

                        index = _holeIndex;
                        break;
                    default: return;
                }
                buffer = GetBuffer(_syncing);
                tiles = GetTiles(_syncing);
            }
            while (time < tiles[index].Value);
            LandscapeTile tile = tiles[index].Key;
            switch (_syncing)
            {
                case Syncing.Heightmap:
                    fixed (byte* ptr2 = buffer)
                    fixed (float* ptr3 = tile.heightmap)
                    {
                        Buffer.MemoryCopy(ptr3, ptr2, buffer.Length, TotalHeightmapLength);
                        if (!BitConverter.IsLittleEndian)
                        {
                            for (int i = 0; i < buffer.Length; i += sizeof(float))
                                DevkitServerUtility.ReverseFloat(ptr2, i);
                        }
                    }
                    break;
                case Syncing.Splatmap:
                    fixed (byte* ptr2 = buffer)
                    fixed (float* ptr3 = tile.splatmap)
                    {
                        Buffer.MemoryCopy(ptr3, ptr2, buffer.Length, TotalSplatmapLength);
                        if (!BitConverter.IsLittleEndian)
                        {
                            for (int i = 0; i < buffer.Length; i += sizeof(float))
                                DevkitServerUtility.ReverseFloat(ptr2, i);
                        }
                    }
                    break;
                case Syncing.Holes:
                    fixed (byte* ptr2 = buffer)
                    fixed (bool* ptr3 = tile.holes)
                    {
                        // compress the array to indivdual bits.
                        int bitCt = -1;
                        for (int i = 0; i < Landscape.HOLES_RESOLUTION * Landscape.HOLES_RESOLUTION; ++i)
                            ptr2[bitCt / 8] |= (byte)(ptr3[i] ? 1 << (++bitCt % 8) : 0);
                    }

                    break;
            }

            _index = 0;
        }
        else if (_syncing != Syncing.None && _index > -1 && time - _lastSent > 0.1)
        {
            byte[] buffer = GetBuffer(_syncing);
            int index = GetIndex(_syncing);
            List<KeyValuePair<LandscapeTile, float>> tiles = GetTiles(_syncing);
            LandscapeTile? tile = index < 0 || index >= tiles.Count ? null : tiles[index].Key;
            int ttl = _syncing switch
            {
                Syncing.Heightmap => TotalHeightmapLength,
                Syncing.Splatmap => TotalSplatmapLength,
                _ => TotalHoleLength
            };
            if (tile != null)
            {
                _lastSent = time;
                ushort len = (ushort)Math.Min(ttl - _index, MaxPacketSize);
                Buffer.BlockCopy(buffer, _index, _packetBuffer, PacketOffset, len);
                _packetBuffer[0] = (byte)_syncing;
                UnsafeBitConverter.GetBytes(_packetBuffer, (ushort)_packetId, 1);
                ++_packetId;
                UnsafeBitConverter.GetBytes(_packetBuffer, tile.coord.x, sizeof(ushort) + 1);
                UnsafeBitConverter.GetBytes(_packetBuffer, tile.coord.y, sizeof(ushort) + 1 + sizeof(int));
                UnsafeBitConverter.GetBytes(_packetBuffer, len, PacketOffset - sizeof(ushort));
                _index += len;
                NetFactory.SendGeneric(NetFactory.DevkitMessage.SendTileData, _packetBuffer, null, length: PacketOffset + len);
            }
            if (_index >= ttl || tile == null)
            {
                _lastSyncing = _syncing;
                if (_syncing == Syncing.Heightmap)
                    ++_hmIndex;
                else if (_syncing == Syncing.Splatmap)
                    ++_smIndex;
                else
                    ++_holeIndex;
                _syncing = Syncing.None;
                _index = -1;
            }
        }
        List<KeyValuePair<LandscapeTile, float>> GetTiles(Syncing sync) => sync switch
        {
            Syncing.Heightmap => _hmTiles,
            Syncing.Splatmap => _smTiles,
            _ => _holeTiles
        };
        byte[] GetBuffer(Syncing sync) => sync switch
        {
            Syncing.Heightmap => _hmBuffer,
            Syncing.Splatmap => _smBuffer,
            _ => _holeBuffer
        };
        int GetIndex(Syncing sync) => sync switch
        {
            Syncing.Heightmap => _hmIndex,
            Syncing.Splatmap => _smIndex,
            _ => _holeIndex
        };
    }
    private void TryQueueHeightmapUpdate(LandscapeTile tile)
    {
        Logger.LogDebug("[TILE SYNC] Tile heightmap updated: " + tile.coord + ".");
        for (int i = _hmTiles.Count - 1; i > _hmIndex; --i)
        {
            if (_hmTiles[i].Key.coord == tile.coord)
            {
                _hmTiles.RemoveAt(i);
            }
        }

        _hmTiles.Add(new KeyValuePair<LandscapeTile, float>(tile, Time.realtimeSinceStartup + 10f));
    }
    private void TryQueueSplatmapUpdate(LandscapeTile tile)
    {
        Logger.LogDebug("[TILE SYNC] Tile splatmap updated: " + tile.coord + ".");
        for (int i = _smTiles.Count - 1; i > _smIndex; --i)
        {
            if (_smTiles[i].Key.coord == tile.coord)
            {
                _smTiles.RemoveAt(i);
            }
        }

        _smTiles.Add(new KeyValuePair<LandscapeTile, float>(tile, Time.realtimeSinceStartup + 10f));
    }
    private void TryQueueHoleUpdate(LandscapeTile tile)
    {
        Logger.LogDebug("[TILE SYNC] Tile holes updated: " + tile.coord + ".");
        for (int i = _holeTiles.Count - 1; i > _holeIndex; --i)
        {
            if (_holeTiles[i].Key.coord == tile.coord)
            {
                _holeTiles.RemoveAt(i);
            }
        }

        _holeTiles.Add(new KeyValuePair<LandscapeTile, float>(tile, Time.realtimeSinceStartup + 10f));
    }
    private static void OnTileUpdated(LandscapeTile tile, Syncing type)
    {
        if (type == Syncing.Heightmap)
            Instance?.TryQueueHeightmapUpdate(tile);
    }

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
                            yield return DevkitServerUtility.LoadConstantI4((int)Syncing.Heightmap);
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
                        yield return DevkitServerUtility.LoadConstantI4((int)Syncing.Heightmap);
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
#endif
}