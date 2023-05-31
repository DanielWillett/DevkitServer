using System.Reflection;
using DevkitServer.Multiplayer;
using SDG.Framework.Devkit;
using SDG.Framework.Landscapes;

namespace DevkitServer.Util;
public static class LandscapeUtil
{
    internal static bool SaveTransactions = true;
    internal static FieldInfo SaveTransactionsField = typeof(LandscapeUtil).GetField(nameof(SaveTransactions), BindingFlags.Static | BindingFlags.NonPublic)!;

    private static readonly StaticGetter<Dictionary<LandscapeCoord, LandscapeTile>> GetTiles =
        Accessor.GenerateStaticGetter<Landscape, Dictionary<LandscapeCoord, LandscapeTile>>("tiles", throwOnError: true)!;

    /// <returns>An enumerable used to loop through all the existing tiles.</returns>
    public static IReadOnlyCollection<LandscapeTile> EnumerateAllTiles() => GetTiles().Values;
    /// <summary>
    /// If possible, use <see cref="EnumerateAllTiles"/> instead.
    /// </summary>
    /// <returns>A copy of all existing tiles.</returns>
    public static List<LandscapeTile> GetAllTiles() => new List<LandscapeTile>(EnumerateAllTiles());
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
            Logger.LogDebug($"[TERRAIN COPY] Reading heightmap: Offset: ({offsetX.Format()}, {offsetY.Format()}). Size: ({sizeX.Format()}, {sizeY.Format()}). Tile Size: {tileSize.Format()}. Block Size: {blockSize.Format()}.");
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
            Logger.LogDebug($"[TERRAIN COPY] Writing heightmap: Offset: ({offsetX.Format()}, {offsetY.Format()}). Size: ({sizeX.Format()}, {sizeY.Format()}). Tile Size: {tileSize.Format()}. Block Size: {blockSize.Format()}.");
            for (int x = offsetX; x < offsetX + sizeX; ++x)
            {
                Buffer.MemoryCopy(buffer, tileData + x * tileSize + offsetY, blockSize, blockSize);
                buffer += sizeY;
            }
        }

        if (apply)
        {
            Logger.LogDebug("Applying heightmap to " + tile.coord.Format() + ".");
            tile.SetHeightsDelayLOD();
            tile.SyncHeightmap();
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
            Logger.LogDebug($"[TERRAIN COPY] Reading splatmap: Offset: ({offsetX.Format()}, {offsetY.Format()}). Size: ({sizeX.Format()}, {sizeY.Format()}). Tile Size: {tileSize.Format()}. Block Size: {blockSize.Format()}.");
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
            Logger.LogDebug($"[TERRAIN COPY] Writing splatmap: Offset: ({offsetX.Format()}, {offsetY.Format()}). Size: ({sizeX.Format()}, {sizeY.Format()}). Tile Size: {tileSize.Format()}. Block Size: {blockSize.Format()}.");
            for (int x = offsetX; x < offsetX + sizeX; ++x)
            {
                Buffer.MemoryCopy(buffer, tileData + x * tileSize + offsetY * layers, blockSize, blockSize);
                buffer += sizeY * layers;
            }
        }
        
        if (apply)
        {
            Logger.LogDebug("Applying splatmap to " + tile.coord.Format() + ".");
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
        const int tileSize = Landscape.HOLES_RESOLUTION;
        Logger.LogDebug($"[TERRAIN COPY] Reading holes: Offset: ({offsetX.Format()}, {offsetY.Format()}). Size: ({sizeX.Format()}, {sizeY.Format()}). Tile Size: {tileSize.Format()}.");
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
        const int tileSize = Landscape.HOLES_RESOLUTION;
        Logger.LogDebug($"[TERRAIN COPY] Writing holes: Offset: ({offsetX.Format()}, {offsetY.Format()}). Size: ({sizeX.Format()}, {sizeY.Format()}). Tile Size: {tileSize.Format()}.");
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
            Logger.LogDebug("Applying holes to " + tile.coord.Format() + ".");
            tile.data.SetHoles(0, 0, tile.holes);
            tile.hasAnyHolesData = true;
            LevelHierarchy.MarkDirty();
        }
    }
    public static void WriteHeightmapNoTransactions(Bounds worldBounds, Landscape.LandscapeWriteHeightmapHandler callback)
    {
        ThreadUtil.assertIsGameThread();

        SaveTransactions = false;
        try
        {
            Landscape.writeHeightmap(worldBounds, callback);
        }
        catch (Exception ex)
        {
            Logger.LogError("Error writing to heightmap.");
            Logger.LogError(ex);
        }
        finally
        {
            SaveTransactions = true;
        }
    }
    public static void WriteSplatmapNoTransactions(Bounds worldBounds, Landscape.LandscapeWriteSplatmapHandler callback)
    {
        ThreadUtil.assertIsGameThread();

        SaveTransactions = false;
        try
        {
            Landscape.writeSplatmap(worldBounds, callback);
        }
        catch (Exception ex)
        {
            Logger.LogError("Error writing to splatmap.");
            Logger.LogError(ex);
        }
        finally
        {
            SaveTransactions = true;
        }
    }
    public static void WriteHolesNoTransactions(Bounds worldBounds, Landscape.LandscapeWriteHolesHandler callback)
    {
        ThreadUtil.assertIsGameThread();

        SaveTransactions = false;
        try
        {
            Landscape.writeHoles(worldBounds, callback);
        }
        catch (Exception ex)
        {
            Logger.LogError("Error writing to holes.");
            Logger.LogError(ex);
        }
        finally
        {
            SaveTransactions = true;
        }
    }
    public static float GetBrushAlpha(this Multiplayer.Actions.IBrushFalloff action, float distance)
    {
        return distance < action.BrushFalloff ? 1f : (1f - distance) / (1f - action.BrushFalloff);
    }
}
