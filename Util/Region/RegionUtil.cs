using SDG.Framework.Foliage;
using SDG.Framework.Landscapes;

namespace DevkitServer.Util.Region;
public static class RegionUtil
{
    /// <summary>
    /// Gets the region of a position, or throws an error if it's out of range. Pass an argument name when the name is not 'position'.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"/>
    public static void AssertGetRegion(Vector3 position, out byte x, out byte y, string argumentName)
    {
        if (!Regions.tryGetCoordinate(position, out x, out y))
            throw new ArgumentOutOfRangeException(argumentName, "Position is out of range of the region system.");
    }
    /// <summary>
    /// Gets the region of a position, or throws an error if it's out of range.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"/>
    public static void AssertGetRegion(Vector3 position, out byte x, out byte y)
    {
        if (!Regions.tryGetCoordinate(position, out x, out y))
            throw new ArgumentOutOfRangeException(nameof(position), "Position is out of range of the region system.");
    }
    public static SurroundingRegionsIterator EnumerateRegions() => new SurroundingRegionsIterator((byte)(Regions.WORLD_SIZE / 2), (byte)(Regions.WORLD_SIZE / 2), 255);
    public static SurroundingRegionsIterator EnumerateRegions(byte centerX, byte centerY, byte maxRegionDistance) => new SurroundingRegionsIterator(centerX, centerY, maxRegionDistance);
    public static SurroundingRegionsIterator EnumerateRegions(byte centerX, byte centerY) => new SurroundingRegionsIterator(centerX, centerY);
    public static SurroundingRegionsIterator EnumerateRegions(RegionCoord center, byte maxRegionDistance) => new SurroundingRegionsIterator(center.x, center.y, maxRegionDistance);
    public static SurroundingRegionsIterator EnumerateRegions(RegionCoord center) => new SurroundingRegionsIterator(center.x, center.y);
    public static SurroundingRegionsIterator EnumerateRegions(Vector3 center)
    {
        if (!Regions.tryGetCoordinate(center, out byte centerX, out byte centerY))
            centerX = centerY = (byte)(Regions.WORLD_SIZE / 2);

        return new SurroundingRegionsIterator(centerX, centerY);
    }
    public static RegionsIterator LinearEnumerateRegions(bool yPrimary) => new RegionsIterator(yPrimary);
    public static RegionsIterator LinearEnumerateRegions() => new RegionsIterator();

    public static ListRegionsEnumerator<T> CastFrom<T>(this List<T>[,] regions, RegionCoord center, byte maxRegionDistance = 255)
        => new ListRegionsEnumerator<T>(regions, center.x, center.y, maxRegionDistance);
    public static ListRegionsEnumerator<T> CastFrom<T>(this List<T>[,] regions, byte centerX, byte centerY, byte maxRegionDistance = 255)
        => new ListRegionsEnumerator<T>(regions, centerX, centerY, maxRegionDistance);
    public static ListRegionsEnumerator<T> CastFrom<T>(this List<T>[,] regions, Vector3 position, byte maxRegionDistance = 255)
    {
        if (Regions.tryGetCoordinate(position, out byte x, out byte y))
            return new ListRegionsEnumerator<T>(regions, x, y, maxRegionDistance);
        
        return new ListRegionsEnumerator<T>(regions, (byte)(Regions.WORLD_SIZE / 2), (byte)(Regions.WORLD_SIZE / 2), maxRegionDistance);
    }
    public static ListRegionsEnumerator<T> CastFrom<T>(this List<T>[,] regions) => new ListRegionsEnumerator<T>(regions);

    public static SurroundingTilesIterator EnumerateTiles(int centerX, int centerY, TileIteratorMode mode) => new SurroundingTilesIterator(centerX, centerY, mode);
    public static SurroundingTilesIterator EnumerateTiles(Vector3 center, TileIteratorMode mode)
    {
        int centerX, centerY;
        switch (mode)
        {
            case TileIteratorMode.Other:
                throw new ArgumentException("Can not use other without a upper and lower limit.", nameof(mode));
            case TileIteratorMode.FoliageCoord:
                FoliageCoord coord = new FoliageCoord(center);
                centerX = coord.x;
                centerY = coord.y;
                break;
            case TileIteratorMode.HeightmapCoord:
                HeightmapCoord coord2 = new HeightmapCoord(new LandscapeCoord(center), center);
                centerX = coord2.x;
                centerY = coord2.y;
                break;
            case TileIteratorMode.SplatmapCoord:
                SplatmapCoord coord3 = new SplatmapCoord(new LandscapeCoord(center), center);
                centerX = coord3.x;
                centerY = coord3.y;
                break;
            default:
            case TileIteratorMode.LandscapeCoord:
                LandscapeCoord coord4 = new LandscapeCoord(center);
                centerX = coord4.x;
                centerY = coord4.y;
                break;
        }
        return new SurroundingTilesIterator(centerX, centerY, mode);
    }

    public static void ForEachRegion([InstantHandle] RegionAction action)
    {
        int worldSize = Regions.WORLD_SIZE;
        SurroundingRegionsIterator iterator = new SurroundingRegionsIterator((byte)(worldSize / 2), (byte)(worldSize / 2));
        while (iterator.MoveNext())
        {
            RegionCoord coord = iterator.Current;
            action(coord);
        }
    }
    public static void ForEachRegion(RegionCoord center, [InstantHandle] RegionAction action)
        => ForEachRegion(center.x, center.y, action);
    public static void ForEachRegion(byte centerX, byte centerY, [InstantHandle] RegionAction action)
    {
        SurroundingRegionsIterator iterator = new SurroundingRegionsIterator(centerX, centerY);
        while (iterator.MoveNext())
        {
            RegionCoord coord = iterator.Current;
            action(coord);
        }
    }
    public static void ForEachRegion(Vector3 center, [InstantHandle] RegionAction action)
    {
        if (!Regions.tryGetCoordinate(center, out byte centerX, out byte centerY))
            centerX = centerY = (byte)(Regions.WORLD_SIZE / 2);

        ForEachRegion(centerX, centerY, action);
    }
    public static void ForEachRegion([InstantHandle] RegionActionWhile action)
    {
        int worldSize = Regions.WORLD_SIZE;
        SurroundingRegionsIterator iterator = new SurroundingRegionsIterator((byte)(worldSize / 2), (byte)(worldSize / 2));
        while (iterator.MoveNext())
        {
            RegionCoord coord = iterator.Current;
            if (!action(coord))
                break;
        }
    }
    public static void ForEachRegion(RegionCoord center, [InstantHandle] RegionActionWhile action)
        => ForEachRegion(center.x, center.y, action);
    public static void ForEachRegion(byte centerX, byte centerY, [InstantHandle] RegionActionWhile action)
    {
        SurroundingRegionsIterator iterator = new SurroundingRegionsIterator(centerX, centerY);
        while (iterator.MoveNext())
        {
            RegionCoord coord = iterator.Current;
            if (!action(coord))
                break;
        }
    }
    public static void ForEachRegion(Vector3 center, [InstantHandle] RegionActionWhile action)
    {
        if (!Regions.tryGetCoordinate(center, out byte centerX, out byte centerY))
            centerX = centerY = (byte)(Regions.WORLD_SIZE / 2);

        ForEachRegion(centerX, centerY, action);
    }
    public static void ForEachTile([InstantHandle] LandscapeCoordAction action)
    {
        SurroundingTilesIterator iterator = new SurroundingTilesIterator(0, 0, TileIteratorMode.LandscapeCoord);
        while (iterator.MoveNext())
        {
            LandscapeCoord coord = iterator.Current;
            action(coord);
        }
    }
    public static void ForEachTile(int centerX, int centerY, [InstantHandle] LandscapeCoordAction action)
    {
        SurroundingTilesIterator iterator = new SurroundingTilesIterator(centerX, centerY, TileIteratorMode.LandscapeCoord);
        while (iterator.MoveNext())
        {
            LandscapeCoord coord = iterator.Current;
            action(coord);
        }
    }
    public static void ForEachTile(LandscapeCoord center, [InstantHandle] LandscapeCoordAction action)
        => ForEachTile(center.x, center.y, action);
    public static void ForEachTile(Vector3 center, [InstantHandle] LandscapeCoordAction action)
    {
        LandscapeCoord coord = new LandscapeCoord(center);

        ForEachTile(coord.x, coord.y, action);
    }
    public static void ForEachTile([InstantHandle] LandscapeCoordActionWhile action)
    {
        SurroundingTilesIterator iterator = new SurroundingTilesIterator(0, 0, TileIteratorMode.LandscapeCoord);
        while (iterator.MoveNext())
        {
            LandscapeCoord coord = iterator.Current;
            if (!action(coord))
                break;
        }
    }
    public static void ForEachTile(int centerX, int centerY, [InstantHandle] LandscapeCoordActionWhile action)
    {
        SurroundingTilesIterator iterator = new SurroundingTilesIterator(centerX, centerY, TileIteratorMode.LandscapeCoord);
        while (iterator.MoveNext())
        {
            LandscapeCoord coord = iterator.Current;
            if (!action(coord))
                break;
        }
    }
    public static void ForEachTile(LandscapeCoord center, [InstantHandle] LandscapeCoordActionWhile action)
        => ForEachTile(center.x, center.y, action);
    public static void ForEachTile(Vector3 center, [InstantHandle] LandscapeCoordActionWhile action)
    {
        LandscapeCoord coord = new LandscapeCoord(center);

        ForEachTile(coord.x, coord.y, action);
    }
    public static void ForEachHeightmapCoord([InstantHandle] HeightmapCoordAction action)
    {
        int maxSize = Landscape.HEIGHTMAP_RESOLUTION;
        SurroundingTilesIterator iterator = new SurroundingTilesIterator(maxSize / 2, maxSize / 2, TileIteratorMode.HeightmapCoord);
        while (iterator.MoveNext())
        {
            HeightmapCoord coord = new HeightmapCoord(iterator.CurrentX, iterator.CurrentY);
            action(coord);
        }
    }
    public static void ForEachHeightmapCoord(int centerX, int centerY, [InstantHandle] HeightmapCoordAction action)
    {
        SurroundingTilesIterator iterator = new SurroundingTilesIterator(centerX, centerY, TileIteratorMode.HeightmapCoord);
        while (iterator.MoveNext())
        {
            HeightmapCoord coord = new HeightmapCoord(iterator.CurrentX, iterator.CurrentY);
            action(coord);
        }
    }
    public static void ForEachHeightmapCoord(HeightmapCoord center, [InstantHandle] HeightmapCoordAction action)
        => ForEachHeightmapCoord(center.x, center.y, action);
    public static void ForEachHeightmapCoord(Vector3 center, [InstantHandle] HeightmapCoordAction action)
    {
        HeightmapCoord coord = new HeightmapCoord(new LandscapeCoord(center), center);

        ForEachHeightmapCoord(coord.x, coord.y, action);
    }
    public static void ForEachHeightmapCoord([InstantHandle] HeightmapCoordActionWhile action)
    {
        int maxSize = Landscape.HEIGHTMAP_RESOLUTION;
        SurroundingTilesIterator iterator = new SurroundingTilesIterator(maxSize / 2, maxSize / 2, TileIteratorMode.HeightmapCoord);
        while (iterator.MoveNext())
        {
            HeightmapCoord coord = new HeightmapCoord(iterator.CurrentX, iterator.CurrentY);
            if (!action(coord))
                break;
        }
    }
    public static void ForEachHeightmapCoord(int centerX, int centerY, [InstantHandle] HeightmapCoordActionWhile action)
    {
        SurroundingTilesIterator iterator = new SurroundingTilesIterator(centerX, centerY, TileIteratorMode.HeightmapCoord);
        while (iterator.MoveNext())
        {
            HeightmapCoord coord = new HeightmapCoord(iterator.CurrentX, iterator.CurrentY);
            if (!action(coord))
                break;
        }
    }
    public static void ForEachHeightmapCoord(HeightmapCoord center, [InstantHandle] HeightmapCoordActionWhile action)
        => ForEachHeightmapCoord(center.x, center.y, action);
    public static void ForEachHeightmapCoord(Vector3 center, [InstantHandle] HeightmapCoordActionWhile action)
    {
        HeightmapCoord coord = new HeightmapCoord(new LandscapeCoord(center), center);

        ForEachHeightmapCoord(coord.x, coord.y, action);
    }
    public static void ForEachSplatmapCoord([InstantHandle] SplatmapCoordAction action)
    {
        int maxSize = Landscape.SPLATMAP_RESOLUTION;
        SurroundingTilesIterator iterator = new SurroundingTilesIterator(maxSize / 2, maxSize / 2, TileIteratorMode.SplatmapCoord);
        while (iterator.MoveNext())
        {
            SplatmapCoord coord = new SplatmapCoord(iterator.CurrentX, iterator.CurrentY);
            action(coord);
        }
    }
    public static void ForEachSplatmapCoord(int centerX, int centerY, [InstantHandle] SplatmapCoordAction action)
    {
        SurroundingTilesIterator iterator = new SurroundingTilesIterator(centerX, centerY, TileIteratorMode.SplatmapCoord);
        while (iterator.MoveNext())
        {
            SplatmapCoord coord = new SplatmapCoord(iterator.CurrentX, iterator.CurrentY);
            action(coord);
        }
    }
    public static void ForEachSplatmapCoord(SplatmapCoord center, [InstantHandle] SplatmapCoordAction action)
        => ForEachSplatmapCoord(center.x, center.y, action);
    public static void ForEachSplatmapCoord(Vector3 center, [InstantHandle] SplatmapCoordAction action)
    {
        SplatmapCoord coord = new SplatmapCoord(new LandscapeCoord(center), center);

        ForEachSplatmapCoord(coord.x, coord.y, action);
    }
    public static void ForEachSplatmapCoord([InstantHandle] SplatmapCoordActionWhile action)
    {
        int maxSize = Landscape.SPLATMAP_RESOLUTION;
        SurroundingTilesIterator iterator = new SurroundingTilesIterator(maxSize / 2, maxSize / 2, TileIteratorMode.SplatmapCoord);
        while (iterator.MoveNext())
        {
            SplatmapCoord coord = new SplatmapCoord(iterator.CurrentX, iterator.CurrentY);
            if (!action(coord))
                break;
        }
    }
    public static void ForEachSplatmapCoord(int centerX, int centerY, [InstantHandle] SplatmapCoordActionWhile action)
    {
        SurroundingTilesIterator iterator = new SurroundingTilesIterator(centerX, centerY, TileIteratorMode.SplatmapCoord);
        while (iterator.MoveNext())
        {
            SplatmapCoord coord = new SplatmapCoord(iterator.CurrentX, iterator.CurrentY);
            if (!action(coord))
                break;
        }
    }
    public static void ForEachSplatmapCoord(SplatmapCoord center, [InstantHandle] SplatmapCoordActionWhile action)
        => ForEachSplatmapCoord(center.x, center.y, action);
    public static void ForEachSplatmapCoord(Vector3 center, [InstantHandle] SplatmapCoordActionWhile action)
    {
        SplatmapCoord coord = new SplatmapCoord(new LandscapeCoord(center), center);

        ForEachSplatmapCoord(coord.x, coord.y, action);
    }
    public static void ForEachFoliageTile([InstantHandle] FoliageCoordAction action)
    {
        SurroundingTilesIterator iterator = new SurroundingTilesIterator(0, 0, TileIteratorMode.FoliageCoord);
        while (iterator.MoveNext())
        {
            FoliageCoord coord = new FoliageCoord(iterator.CurrentX, iterator.CurrentY);
            action(coord);
        }
    }
    public static void ForEachFoliageTile(int centerX, int centerY, [InstantHandle] FoliageCoordAction action)
    {
        SurroundingTilesIterator iterator = new SurroundingTilesIterator(centerX, centerY, TileIteratorMode.FoliageCoord);
        while (iterator.MoveNext())
        {
            FoliageCoord coord = new FoliageCoord(iterator.CurrentX, iterator.CurrentY);
            action(coord);
        }
    }
    public static void ForEachFoliageTile(FoliageCoord center, [InstantHandle] FoliageCoordAction action)
        => ForEachFoliageTile(center.x, center.y, action);
    public static void ForEachFoliageTile(Vector3 center, [InstantHandle] FoliageCoordAction action)
    {
        FoliageCoord coord = new FoliageCoord(center);

        ForEachFoliageTile(coord.x, coord.y, action);
    }
    public static void ForEachFoliageTile([InstantHandle] FoliageCoordActionWhile action)
    {
        SurroundingTilesIterator iterator = new SurroundingTilesIterator(0, 0, TileIteratorMode.FoliageCoord);
        while (iterator.MoveNext())
        {
            FoliageCoord coord = new FoliageCoord(iterator.CurrentX, iterator.CurrentY);
            if (!action(coord))
                break;
        }
    }
    public static void ForEachFoliageTile(int centerX, int centerY, [InstantHandle] FoliageCoordActionWhile action)
    {
        SurroundingTilesIterator iterator = new SurroundingTilesIterator(centerX, centerY, TileIteratorMode.FoliageCoord);
        while (iterator.MoveNext())
        {
            FoliageCoord coord = new FoliageCoord(iterator.CurrentX, iterator.CurrentY);
            if (!action(coord))
                break;
        }
    }
    public static void ForEachFoliageTile(FoliageCoord center, [InstantHandle] FoliageCoordActionWhile action)
        => ForEachFoliageTile(center.x, center.y, action);
    public static void ForEachFoliageTile(Vector3 center, [InstantHandle] FoliageCoordActionWhile action)
    {
        FoliageCoord coord = new FoliageCoord(center);

        ForEachFoliageTile(coord.x, coord.y, action);
    }
}

public delegate void RegionAction(RegionCoord coord);
public delegate bool RegionActionWhile(RegionCoord coord);
public delegate void LandscapeCoordAction(LandscapeCoord coord);
public delegate bool LandscapeCoordActionWhile(LandscapeCoord coord);
public delegate void HeightmapCoordAction(HeightmapCoord coord);
public delegate bool HeightmapCoordActionWhile(HeightmapCoord coord);
public delegate void SplatmapCoordAction(SplatmapCoord coord);
public delegate bool SplatmapCoordActionWhile(SplatmapCoord coord);
public delegate void FoliageCoordAction(FoliageCoord coord);
public delegate bool FoliageCoordActionWhile(FoliageCoord coord);