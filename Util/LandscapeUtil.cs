using SDG.Framework.Devkit;
using SDG.Framework.Landscapes;

namespace DevkitServer.Util;
public static class LandscapeUtil
{
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
                Logger.LogDebug($"{x}: {Environment.NewLine}{DevkitServerUtility.GetBytesHex(tileData, sizeX, 3, x * tileSize + offsetY)}");
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
                Logger.LogDebug($"{x}: {Environment.NewLine}{DevkitServerUtility.GetBytesHex(tileData, sizeX, 3, x * tileSize + offsetY)}");
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
        int offsetX = bounds.min.x;
        int offsetY = bounds.min.y;
        int sizeX = bounds.max.x - offsetX + 1;
        int sizeY = bounds.max.y - offsetY + 1;
        const int tileSize = Landscape.HOLES_RESOLUTION;
        fixed (bool* tileData = tile.holes)
        {
            Logger.LogDebug($"[TERRAIN COPY] Reading holes: Offset: ({offsetX.Format()}, {offsetY.Format()}). Size: ({sizeX.Format()}, {sizeY.Format()}). Tile Size: {tileSize.Format()}.");
            int bitCt = 0;
            for (int x = offsetX; x < offsetX + sizeX; ++x)
            {
                for (int y = offsetY; y < offsetY + sizeY; ++y)
                {
                    // ReSharper disable once RedundantCast (literally just incorrect)
                    input[bitCt / 8] |= tileData[x * tileSize + y] ? (byte)(1 << (bitCt % 8)) : (byte)0;
                    ++bitCt;
                }
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
        fixed (bool* tileData = tile.holes)
        {
            Logger.LogDebug($"[TERRAIN COPY] Writing holes: Offset: ({offsetX.Format()}, {offsetY.Format()}). Size: ({sizeX.Format()}, {sizeY.Format()}). Tile Size: {tileSize.Format()}.");
            int bitCt = 0;
            for (int x = offsetX; x < offsetX + sizeX; ++x)
            {
                for (int y = offsetY; y < offsetY + sizeY; ++y)
                {
                    tileData[x * tileSize + y] = (input[bitCt / 8] & (1 << (bitCt % 8))) > 0;
                    ++bitCt;
                }
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
}
