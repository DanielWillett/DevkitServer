//#define TIME_TRANSACTIONS
using DanielWillett.ReflectionTools;
using DevkitServer.API;
using DevkitServer.Multiplayer.Actions;
using DevkitServer.Multiplayer.Sync;
using SDG.Framework.Devkit;
using SDG.Framework.Landscapes;
using System.Globalization;
using System.Reflection;
#if DEBUG && TIME_TRANSACTIONS
using System.Diagnostics;
#endif
#if CLIENT
using DevkitServer.Players;
#endif

namespace DevkitServer.Util;

[EarlyTypeInit]
public static class LandscapeUtil
{
    internal static bool SaveTransactions = true;
    internal static FieldInfo SaveTransactionsField = typeof(LandscapeUtil).GetField(nameof(SaveTransactions), BindingFlags.Static | BindingFlags.NonPublic)!;

    private static readonly StaticGetter<Dictionary<LandscapeCoord, LandscapeTile>> GetTiles =
        Accessor.GenerateStaticGetter<Landscape, Dictionary<LandscapeCoord, LandscapeTile>>("tiles", throwOnError: true)!;

    private static readonly Action<LandscapeTile>? CallReadHoles =
        Accessor.GenerateInstanceCaller<LandscapeTile, Action<LandscapeTile>>("ReadHoles", allowUnsafeTypeBinding: true);

    /// <returns>A readonly value collection used to loop through all the existing tiles.</returns>
    public static IReadOnlyCollection<LandscapeTile> Tiles => GetTiles().Values;

    internal static Dictionary<LandscapeCoord, LandscapeTile> GetTileDictionary() => GetTiles();

    /// <summary>
    /// If possible, use <see cref="Tiles"/> instead.
    /// </summary>
    /// <returns>A copy of all existing tiles.</returns>
    public static LandscapeTile[] GetAllTiles()
    {
        ICollection<LandscapeTile> tiles = GetTiles().Values;
        LandscapeTile[] newTiles = new LandscapeTile[Tiles.Count];
        tiles.CopyTo(newTiles, 0);
        return newTiles;
    }

    public static bool Encapsulates(this in LandscapeBounds outer, in LandscapeBounds inner) =>
        outer.min.x < inner.min.x && outer.min.y < inner.min.y && outer.max.x > inner.max.x && outer.max.y > inner.max.y;
    public static bool Encapsulates(this in HeightmapBounds outer, in HeightmapBounds inner) =>
        outer.min.x < inner.min.x && outer.min.y < inner.min.y && outer.max.x > inner.max.x && outer.max.y > inner.max.y;
    public static bool Encapsulates(this in SplatmapBounds outer, in SplatmapBounds inner) =>
        outer.min.x < inner.min.x && outer.min.y < inner.min.y && outer.max.x > inner.max.x && outer.max.y > inner.max.y;
    public static bool Overlaps(this in LandscapeBounds left, in LandscapeBounds right) =>
        !(left.max.x < right.min.x || left.max.y < right.min.y || left.min.x > right.max.x || left.min.y > right.max.y);
    public static bool Overlaps(this in HeightmapBounds left, in HeightmapBounds right) =>
        !(left.max.x < right.min.x || left.max.y < right.min.y || left.min.x > right.max.x || left.min.y > right.max.y);
    public static bool Overlaps(this in SplatmapBounds left, in SplatmapBounds right) =>
        !(left.max.x < right.min.x || left.max.y < right.min.y || left.min.x > right.max.x || left.min.y > right.max.y);
    public static Vector3 GetWorldPosition(this in HeightmapCoord coord, in LandscapeCoord tile)
    {
        Vector3 pos = Landscape.getWorldPosition(tile, coord, 0f);
        return new Vector3(pos.x, LevelGround.getHeight(pos), pos.z);
    }
    public static Vector3 GetWorldPosition(this in SplatmapCoord coord, in LandscapeCoord tile) => Landscape.getWorldPosition(tile, coord);
    public static Vector3 GetWorldPositionNoHeight(this in HeightmapCoord coord, in LandscapeCoord tile, float height = 0) => Landscape.getWorldPosition(tile, coord, height);

    // SDG.Framework.Landscapes.Landscape.getWorldPosition
    public static Vector3 GetWorldPositionNoHeight(this in SplatmapCoord coord, in LandscapeCoord tile, float height = 0)
    {
        return new Vector3(
            Mathf.RoundToInt(tile.x * Landscape.TILE_SIZE + coord.y / (float)Landscape.SPLATMAP_RESOLUTION * Landscape.TILE_SIZE) + Landscape.HALF_SPLATMAP_WORLD_UNIT,
            height,
            Mathf.RoundToInt(tile.y * Landscape.TILE_SIZE + coord.x / (float)Landscape.SPLATMAP_RESOLUTION * Landscape.TILE_SIZE) + Landscape.HALF_SPLATMAP_WORLD_UNIT);
    }

    public static void Encapsulate(this ref LandscapeBounds left, in LandscapeBounds right)
    {
        if (left.min.x > right.min.x)
            left.min.x = right.min.x;
        if (left.min.y > right.min.y)
            left.min.y = right.min.y;
        if (left.max.x < right.max.x)
            left.max.x = right.max.x;
        if (left.max.y < right.max.y)
            left.max.y = right.max.y;
    }
    public static void Encapsulate(this ref HeightmapBounds left, in HeightmapBounds right)
    {
        if (left.min.x > right.min.x)
            left.min.x = right.min.x;
        if (left.min.y > right.min.y)
            left.min.y = right.min.y;
        if (left.max.x < right.max.x)
            left.max.x = right.max.x;
        if (left.max.y < right.max.y)
            left.max.y = right.max.y;
    }
    public static void Encapsulate(this ref SplatmapBounds left, in SplatmapBounds right)
    {
        if (left.min.x > right.min.x)
            left.min.x = right.min.x;
        if (left.min.y > right.min.y)
            left.min.y = right.min.y;
        if (left.max.x < right.max.x)
            left.max.x = right.max.x;
        if (left.max.y < right.max.y)
            left.max.y = right.max.y;
    }
    public static int GetHeightmapSize(in HeightmapBounds bounds) => (bounds.max.x - bounds.min.x + 1) * (bounds.max.y - bounds.min.y + 1) * sizeof(float);
    public static int GetSplatmapSize(in SplatmapBounds bounds) => (bounds.max.x - bounds.min.x + 1) * (bounds.max.y - bounds.min.y + 1) * Landscape.SPLATMAP_LAYERS * sizeof(float);
    public static int GetHolesSize(in SplatmapBounds bounds) => Mathf.CeilToInt((bounds.max.x - bounds.min.x + 1) * (bounds.max.y - bounds.min.y + 1) / 8f);
    public static float[,] CloneHeightmapFromPool(LandscapeTile tile)
    {
        float[,] hm = LandscapeHeightmapCopyPool.claim();
        int tileSize = Landscape.HEIGHTMAP_RESOLUTION;
        tileSize *= tileSize * sizeof(float);
        Buffer.BlockCopy(tile.heightmap, 0, hm, 0, tileSize);

        return hm;
    }
    public static float[,,] CloneSplatmapFromPool(LandscapeTile tile)
    {
        float[,,] sm = LandscapeSplatmapCopyPool.claim();
        int tileSize = Landscape.SPLATMAP_RESOLUTION;
        tileSize *= tileSize * Landscape.SPLATMAP_LAYERS * sizeof(float);
        Buffer.BlockCopy(tile.splatmap, 0, sm, 0, tileSize);

        return sm;
    }
    public static bool[,] CloneHolesFromPool(LandscapeTile tile)
    {
        bool[,] hm = LandscapeHoleCopyPool.claim();
        const int tileSize = Landscape.HOLES_RESOLUTION * Landscape.HOLES_RESOLUTION * sizeof(bool);
        Buffer.BlockCopy(tile.holes, 0, hm, 0, tileSize);

        return hm;
    }

    public static void ReleaseHeightmapBuffer(Dictionary<LandscapeCoord, float[,]> dict)
    {
        foreach (float[,] hm in dict.Values)
            LandscapeHeightmapCopyPool.release(hm);

        dict.Clear();
    }
    public static void ReleaseSplatmapBuffer(Dictionary<LandscapeCoord, float[,,]> dict)
    {
        foreach (float[,,] sm in dict.Values)
            LandscapeSplatmapCopyPool.release(sm);

        dict.Clear();
    }
    public static void ReleaseHoleBuffer(Dictionary<LandscapeCoord, bool[,]> dict)
    {
        foreach (bool[,] hm in dict.Values)
            LandscapeHoleCopyPool.release(hm);

        dict.Clear();
    }

    /// <summary>
    /// Locally delete a tile.
    /// </summary>
    /// <param name="deleteTerrainData">Deletes heightmap, splatmap, and hole data files from the map. The same as calling <see cref="CleanupTile(LandscapeCoord)"/></param>
    public static void RemoveTileLocal(LandscapeCoord coordinate, bool deleteTerrainData = false)
    {
        Landscape.removeTile(coordinate);
        LevelHierarchy.MarkDirty();
        if (deleteTerrainData)
            CleanupTile(coordinate);
    }

    /// <summary>
    /// Locally add a tile.
    /// </summary>
    /// <returns>The new tile, or if the tile already exists, the existing tile.</returns>
    public static LandscapeTile AddTileLocal(LandscapeCoord coordinate) => AddTileLocal(coordinate, out _);

    /// <summary>
    /// Locally add a tile.
    /// </summary>
    /// <param name="alreadyExisted">Whether the tile already existed.</param>
    /// <returns>The new tile, or if the tile already exists, the existing tile.</returns>
    public static LandscapeTile AddTileLocal(LandscapeCoord coordinate, out bool alreadyExisted)
    {
        LandscapeTile? tile = Landscape.addTile(coordinate);
        alreadyExisted = tile == null;
        if (alreadyExisted)
            return Landscape.getTile(coordinate)!;

        tile!.readHeightmaps();
        tile.readSplatmaps();
        tile.updatePrototypes();
        CallReadHoles?.Invoke(tile);
        Landscape.linkNeighbors();
        Landscape.reconcileNeighbors(tile);
        Landscape.applyLOD();
        LevelHierarchy.MarkDirty();
        return tile;
    }
    /// <summary>
    /// Deletes splatmap, heightmap, and hole files for the provided tile.
    /// </summary>
    public static void CleanupTile(LandscapeCoord coordinate)
    {
        if (Level.info?.name == null)
            return;

        string prefix = "Tile_" + coordinate.x.ToString(CultureInfo.InvariantCulture) + "_" +
                        coordinate.y.ToString(CultureInfo.InvariantCulture);
        string hmDir = Path.Combine(Level.info.path, "Landscape", "Heightmaps");
        try
        {
            if (Directory.Exists(hmDir))
            {
                foreach (string hmFile in Directory.GetFiles(hmDir, "*.heightmap", SearchOption.TopDirectoryOnly)
                             .Where(x => Path.GetFileName(x).StartsWith(prefix, StringComparison.Ordinal)))
                {
                    try
                    {
                        File.Delete(hmFile);
                        Logger.DevkitServer.LogDebug(nameof(CleanupTile), $"Cleaned up heightmap for tile {coordinate.Format()}: {hmFile.Format(true)}.");
                    }
                    catch (Exception ex)
                    {
                        Logger.DevkitServer.LogError(nameof(CleanupTile), ex, $"Error cleaning up heightmap for tile {coordinate.Format()}: {hmFile.Format(true)}.");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError(nameof(CleanupTile), ex, $"Error cleaning up heightmaps for tile {coordinate.Format()} in: {hmDir.Format(true)}.");
        }

        string smDir = Path.Combine(Level.info.path, "Landscape", "Splatmaps");
        try
        {
            if (Directory.Exists(smDir))
            {
                foreach (string smFile in Directory.GetFiles(smDir, "*.splatmap", SearchOption.TopDirectoryOnly)
                             .Where(x => Path.GetFileName(x).StartsWith(prefix, StringComparison.Ordinal)))
                {
                    try
                    {
                        File.Delete(smFile);
                        Logger.DevkitServer.LogDebug(nameof(CleanupTile), $"Cleaned up splatmap for tile {coordinate.Format()}: {smFile.Format(true)}.");
                    }
                    catch (Exception ex)
                    {
                        Logger.DevkitServer.LogError(nameof(CleanupTile), ex, $"Error cleaning up splatmap for tile {coordinate.Format()}: {smFile.Format(true)}.");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError(nameof(CleanupTile), ex, $"Error cleaning up splatmaps for tile {coordinate.Format()} in: {smDir.Format(true)}.");
        }

        string hlDir = Path.Combine(Level.info.path, "Landscape", "Holes");
        try
        {
            if (Directory.Exists(hlDir))
            {
                foreach (string hlFile in Directory.GetFiles(hlDir, "*.bin", SearchOption.TopDirectoryOnly)
                             .Where(x => Path.GetFileName(x).StartsWith(prefix, StringComparison.Ordinal)))
                {
                    try
                    {
                        File.Delete(hlFile);
                        Logger.DevkitServer.LogDebug(nameof(CleanupTile), $"Cleaned up holes for tile {coordinate.Format()}: {hlFile.Format(true)}.");
                    }
                    catch (Exception ex)
                    {
                        Logger.DevkitServer.LogError(nameof(CleanupTile), ex, $"Error cleaning up holes for tile {coordinate.Format()}: {hlFile.Format(true)}.");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError(nameof(CleanupTile), ex, $"Error cleaning up holes for tile {coordinate.Format()} in: {hlDir.Format(true)}.");
        }
    }
    /// <summary>
    /// Checks all heightmap, splatmap, and hole data files and deletes those without a matching tile.
    /// </summary>
    public static void DeleteUnusedTileData()
    {
        if (Level.info?.name == null)
            return;
        
        const string prefix = "Tile_";

        DeleteFilesIn(Path.Combine(Level.info.path, "Landscape", "Heightmaps"), "heightmaps", "heightmap", "heightmap");
        DeleteFilesIn(Path.Combine(Level.info.path, "Landscape", "Splatmaps"), "splatmaps", "splatmap", "splatmap");
        DeleteFilesIn(Path.Combine(Level.info.path, "Landscape", "Holes"), "holes", "holes", "bin");

        static void DeleteFilesIn(string dir, string category, string type, string extension)
        {
            try
            {
                if (!Directory.Exists(dir))
                    return;

                foreach (string file in Directory.GetFiles(dir, "*." + extension, SearchOption.TopDirectoryOnly))
                {
                    string fn = Path.GetFileNameWithoutExtension(file);
                    if (!fn.StartsWith(prefix, StringComparison.Ordinal))
                        continue;
                    int endX = fn.IndexOf('_', prefix.Length);
                    if (endX == -1 || endX == fn.Length - 1)
                        continue;
                    int endY = fn.IndexOf('_', endX + 1);
                    if (endY == -1)
                        endY = fn.Length;

                    if (!int.TryParse(fn.Substring(prefix.Length, endX - prefix.Length), NumberStyles.Number, CultureInfo.InvariantCulture, out int x) ||
                        !int.TryParse(fn.Substring(endX + 1, endY - (endX + 1)), NumberStyles.Number, CultureInfo.InvariantCulture, out int y))
                        continue;

                    LandscapeCoord coordinate = new LandscapeCoord(x, y);
                    if (Landscape.getTile(coordinate) != null)
                        continue;

                    try
                    {
                        File.Delete(file);
                        Logger.DevkitServer.LogDebug(nameof(DeleteUnusedTileData), $"Cleaned up {type} for tile {coordinate.Format()}: {file.Format(true)}.");
                    }
                    catch (Exception ex)
                    {
                        Logger.DevkitServer.LogError(nameof(DeleteUnusedTileData), ex, $"Error cleaning up {type} for tile {coordinate.Format()}: {file.Format(true)}.");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.DevkitServer.LogError(nameof(DeleteUnusedTileData), ex, $"Error cleaning up {category}.");
            }
        }
    }
    /// <summary>
    /// Calls <see cref="LandscapeTile.ReadHoles()"/>.
    /// </summary>
    /// <returns><see langword="true"/> if the method was successfully called.</returns>
    public static bool ReadHoles(LandscapeTile tile)
    {
        if (CallReadHoles == null)
            return false;

        CallReadHoles(tile);
        return true;
    }

    /// <returns>Number of tiles copied.</returns>
    public static int CopyHeightmapTo(LandscapeBounds bounds, Dictionary<LandscapeCoord, float[,]> dict)
    {
        int tiles = 0;
        for (int x = bounds.min.x; x <= bounds.max.x; ++x)
        {
            for (int y = bounds.min.y; y <= bounds.max.y; ++y)
            {
                LandscapeCoord coord = new LandscapeCoord(x, y);
                LandscapeTile? tile = Landscape.getTile(coord);
                float[,] hm;
                if (tile == null)
                {
                    if (dict.TryGetValue(coord, out hm))
                    {
                        LandscapeHeightmapCopyPool.release(hm);
                        dict.Remove(coord);
                    }
                    continue;
                }
                if (!dict.TryGetValue(coord, out hm))
                    dict.Add(coord, CloneHeightmapFromPool(tile));
                else
                {
                    LandscapeHeightmapCopyPool.release(hm);
                    dict[coord] = CloneHeightmapFromPool(tile);
                }

                ++tiles;
            }
        }

        return tiles;
    }

    /// <returns>Number of tiles copied.</returns>
    public static int CopySplatmapTo(LandscapeBounds bounds, Dictionary<LandscapeCoord, float[,,]> dict)
    {
        int tiles = 0;
        for (int x = bounds.min.x; x <= bounds.max.x; ++x)
        {
            for (int y = bounds.min.y; y <= bounds.max.y; ++y)
            {
                LandscapeCoord coord = new LandscapeCoord(x, y);
                LandscapeTile? tile = Landscape.getTile(coord);
                float[,,] sm;
                if (tile == null)
                {
                    if (dict.TryGetValue(coord, out sm))
                    {
                        LandscapeSplatmapCopyPool.release(sm);
                        dict.Remove(coord);
                    }
                    continue;
                }
                if (!dict.TryGetValue(coord, out sm))
                    dict.Add(coord, CloneSplatmapFromPool(tile));
                else
                {
                    LandscapeSplatmapCopyPool.release(sm);
                    dict[coord] = CloneSplatmapFromPool(tile);
                }

                ++tiles;
            }
        }

        return tiles;
    }

    /// <returns>Number of tiles copied.</returns>
    public static int CopyHolesTo(LandscapeBounds bounds, Dictionary<LandscapeCoord, bool[,]> dict)
    {
        int tiles = 0;
        for (int x = bounds.min.x; x <= bounds.max.x; ++x)
        {
            for (int y = bounds.min.y; y <= bounds.max.y; ++y)
            {
                LandscapeCoord coord = new LandscapeCoord(x, y);
                LandscapeTile? tile = Landscape.getTile(coord);
                bool[,] hm;
                if (tile == null)
                {
                    if (dict.TryGetValue(coord, out hm))
                    {
                        LandscapeHoleCopyPool.release(hm);
                        dict.Remove(coord);
                    }
                    continue;
                }
                if (!dict.TryGetValue(coord, out hm))
                    dict.Add(coord, CloneHolesFromPool(tile));
                else
                {
                    LandscapeHoleCopyPool.release(hm);
                    dict[coord] = CloneHolesFromPool(tile);
                }

                ++tiles;
            }
        }

        return tiles;
    }
    public static unsafe void ReadHeightmap(byte* output, LandscapeTile tile, in HeightmapBounds bounds)
    {
        float* buffer = (float*)output;
        int offsetX = bounds.min.x;
        int offsetY = bounds.min.y;
        int sizeX = bounds.max.x - offsetX + 1;
        int sizeY = bounds.max.y - offsetY + 1;

        int tileSize = Landscape.HEIGHTMAP_RESOLUTION;
        int blockSize = sizeY * sizeof(float);
        fixed (float* tileData = tile.heightmap)
        {
            for (int x = offsetX; x < offsetX + sizeX; ++x)
            {
                Buffer.MemoryCopy(tileData + x * tileSize + offsetY, buffer, blockSize, blockSize);
                buffer += sizeY;
            }
        }
    }
    public static unsafe void WriteHeightmap(byte* input, LandscapeTile tile, in HeightmapBounds bounds, bool apply = true)
    {
        float* buffer = (float*)input;
        int offsetX = bounds.min.x;
        int offsetY = bounds.min.y;
        int sizeX = bounds.max.x - offsetX + 1;
        int sizeY = bounds.max.y - offsetY + 1;

        int tileSize = Landscape.HEIGHTMAP_RESOLUTION;
        int blockSize = sizeY * sizeof(float);
        fixed (float* tileData = tile.heightmap)
        {
            for (int x = offsetX; x < offsetX + sizeX; ++x)
            {
                Buffer.MemoryCopy(buffer, tileData + x * tileSize + offsetY, blockSize, blockSize);
                buffer += sizeY;
            }
        }

        if (apply)
        {
            tile.SetHeightsDelayLOD();
            tile.SyncDelayedLOD();
            LevelHierarchy.MarkDirty();
        }
    }
    public static unsafe void ReadSplatmap(byte* output, LandscapeTile tile, in SplatmapBounds bounds)
    {
        float* buffer = (float*)output;
        int offsetX = bounds.min.x;
        int offsetY = bounds.min.y;
        int sizeX = bounds.max.x - offsetX + 1;
        int sizeY = bounds.max.y - offsetY + 1;
        int layers = Landscape.SPLATMAP_LAYERS;
        int tileSize = Landscape.SPLATMAP_RESOLUTION * layers;
        int blockSize = sizeY * layers * sizeof(float);
        fixed (float* tileData = tile.splatmap)
        {
            for (int x = offsetX; x < offsetX + sizeX; ++x)
            {
                Buffer.MemoryCopy(tileData + x * tileSize + offsetY * layers, buffer, blockSize, blockSize);
                buffer += sizeY * layers;
            }
        }
    }
    public static unsafe void WriteSplatmap(byte* input, LandscapeTile tile, in SplatmapBounds bounds, bool apply = true)
    {
        float* buffer = (float*)input;
        int offsetX = bounds.min.x;
        int offsetY = bounds.min.y;
        int sizeX = bounds.max.x - offsetX + 1;
        int sizeY = bounds.max.y - offsetY + 1;
        int layers = Landscape.SPLATMAP_LAYERS;
        int tileSize = Landscape.SPLATMAP_RESOLUTION * layers;
        int blockSize = sizeY * layers * sizeof(float);

        fixed (float* tileData = tile.splatmap)
        {
            for (int x = offsetX; x < offsetX + sizeX; ++x)
            {
                Buffer.MemoryCopy(buffer, tileData + x * tileSize + offsetY * layers, blockSize, blockSize);
                buffer += sizeY * layers;
            }
        }
        
        if (apply)
        {
            tile.data.SetAlphamaps(0, 0, tile.splatmap);
            LevelHierarchy.MarkDirty();
        }
    }
    public static unsafe void ReadHoles(byte* input, LandscapeTile tile, in SplatmapBounds bounds)
    {
        if (!tile.hasAnyHolesData)
        {
            int c = GetHolesSize(in bounds);
            for (int i = 0; i < c; ++i)
                input[i] = 0xFF;
            return;
        }
        int offsetX = bounds.min.x;
        int offsetY = bounds.min.y;
        int sizeX = bounds.max.x - offsetX + 1;
        int sizeY = bounds.max.y - offsetY + 1;
        int bitCt = 0;
        for (int x = offsetX; x < offsetX + sizeX; ++x)
        {
            for (int y = offsetY; y < offsetY + sizeY; ++y)
            {
                if (bitCt % 8 == 0)
                    input[bitCt / 8] = 0;

                if (tile.holes[x, y])
                    input[bitCt / 8] |= (byte)(1 << (bitCt % 8));

                ++bitCt;
            }
        }
    }
    public static unsafe void WriteHoles(byte* input, LandscapeTile tile, in SplatmapBounds bounds, bool apply = true)
    {
        int offsetX = bounds.min.x;
        int offsetY = bounds.min.y;
        int sizeX = bounds.max.x - offsetX + 1;
        int sizeY = bounds.max.y - offsetY + 1;
        int bitCt = 0;
        for (int x = offsetX; x < offsetX + sizeX; ++x)
        {
            for (int y = offsetY; y < offsetY + sizeY; ++y)
            {
                tile.holes[x, y] = (input[bitCt / 8] & (1 << (bitCt % 8))) > 0;
                ++bitCt;
            }
        }

        if (apply)
        {
            tile.SetHoles();
            tile.SyncDelayedLOD();
            tile.hasAnyHolesData = true;
            LevelHierarchy.MarkDirty();
        }
    }
    public static void WriteHeightmapNoTransactions(Bounds worldBounds, Landscape.LandscapeWriteHeightmapHandler callback)
    {
        ThreadUtil.assertIsGameThread();

        SaveTransactions = false;
#if DEBUG && TIME_TRANSACTIONS
        Stopwatch sw = new Stopwatch();
#endif
        try
        {
#if DEBUG && TIME_TRANSACTIONS
            sw.Start();
#endif
            Landscape.writeHeightmap(worldBounds, callback);
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError(nameof(WriteHeightmapNoTransactions), ex, "Error writing to heightmap.");
        }
        finally
        {
#if DEBUG && TIME_TRANSACTIONS
            sw.Stop();
#endif
            SaveTransactions = true;
        }
#if DEBUG && TIME_TRANSACTIONS
        Logger.DevkitServer.LogDebug(nameof(WriteHeightmapNoTransactions), $"Heightmap on {worldBounds.Format()} ({callback.Method.Name.Format(false)}): {sw.GetElapsedMilliseconds().Format("F2")} ms.");
#endif
    }
    public static void WriteSplatmapNoTransactions(Bounds worldBounds, Landscape.LandscapeWriteSplatmapHandler callback)
    {
        ThreadUtil.assertIsGameThread();

        SaveTransactions = false;
#if DEBUG && TIME_TRANSACTIONS
        Stopwatch sw = new Stopwatch();
#endif
        try
        {
#if DEBUG && TIME_TRANSACTIONS
            sw.Start();
#endif
            Landscape.writeSplatmap(worldBounds, callback);
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError(nameof(WriteSplatmapNoTransactions), ex, "Error writing to splatmap.");
        }
        finally
        {
#if DEBUG && TIME_TRANSACTIONS
            sw.Stop();
#endif
            SaveTransactions = true;
        }
#if DEBUG && TIME_TRANSACTIONS
        Logger.DevkitServer.LogDebug(nameof(WriteSplatmapNoTransactions), $"Splatmap on {worldBounds.Format()} ({callback.Method.Name.Format(false)}): {sw.GetElapsedMilliseconds().Format("F2")} ms.");
#endif
    }
    public static void WriteHolesNoTransactions(Bounds worldBounds, Landscape.LandscapeWriteHolesHandler callback)
    {
        ThreadUtil.assertIsGameThread();

        SaveTransactions = false;
#if DEBUG && TIME_TRANSACTIONS
        Stopwatch sw = new Stopwatch();
#endif
        try
        {
#if DEBUG && TIME_TRANSACTIONS
            sw.Start();
#endif
            Landscape.writeHoles(worldBounds, callback);
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError(nameof(WriteHolesNoTransactions), ex, "Error writing to holes.");
        }
        finally
        {
#if DEBUG && TIME_TRANSACTIONS
            sw.Stop();
#endif
            SaveTransactions = true;
        }
#if DEBUG && TIME_TRANSACTIONS
        Logger.DevkitServer.LogDebug(nameof(WriteHolesNoTransactions), $"Holes on {worldBounds.Format()} ({callback.Method.Name.Format(false)}): {sw.GetElapsedMilliseconds().Format("F2")} ms.");
#endif
    }
    public static float GetBrushAlpha(IBrushFalloffAction action, float distance)
    {
        return distance < action.BrushFalloff ? 1f : (1f - distance) / (1f - action.BrushFalloff);
    }
    internal static bool CheckSync(out TileSync sync)
    {
        sync = null!;
#if CLIENT
        if (!DevkitServerModule.IsEditing || EditorUser.User == null || EditorUser.User.TileSync == null || !EditorUser.User.TileSync.HasAuthority)
            return false;
        sync = EditorUser.User.TileSync;
#elif SERVER
        if (!DevkitServerModule.IsEditing || TileSync.ServersideAuthority == null || !TileSync.ServersideAuthority.HasAuthority)
            return false;
        sync = TileSync.ServersideAuthority;
#endif
        return true;
    }
    public static bool SyncIfAuthority(Bounds bounds, TileSync.DataType type)
    {
        if (!CheckSync(out TileSync sync))
            return false;
        sync.InvalidateBounds(bounds, type, CachedTime.RealtimeSinceStartup);
        return true;
    }
}
