namespace DevkitServer.Util.Region;
public static class RegionUtil
{
    public static SurroundingRegionsIterator EnumerateRegions(byte centerX, byte centerY) => new SurroundingRegionsIterator(centerX, centerY);
    public static SurroundingRegionsIterator EnumerateRegions(Vector3 center)
    {
        if (!Regions.tryGetCoordinate(center, out byte centerX, out byte centerY))
            centerX = centerY = (byte)(Regions.WORLD_SIZE / 2);

        return new SurroundingRegionsIterator(centerX, centerY);
    }

    public static RegionsIterator LinearEnumerateRegions(bool yPrimary) => new RegionsIterator(yPrimary);
    public static RegionsIterator LinearEnumerateRegions() => new RegionsIterator();

    public static void ForEach(RegionAction action)
    {
        int worldSize = Regions.WORLD_SIZE;
        SurroundingRegionsIterator iterator = new SurroundingRegionsIterator((byte)(worldSize / 2), (byte)(worldSize / 2));
        while (iterator.MoveNext())
        {
            RegionCoord coord = iterator.Current;
            action(coord);
        }
    }
    public static void ForEach(byte centerX, byte centerY, RegionAction action)
    {
        SurroundingRegionsIterator iterator = new SurroundingRegionsIterator(centerX, centerY);
        while (iterator.MoveNext())
        {
            RegionCoord coord = iterator.Current;
            action(coord);
        }
    }
    public static void ForEach(Vector3 center, RegionAction action)
    {
        if (!Regions.tryGetCoordinate(center, out byte centerX, out byte centerY))
            centerX = centerY = (byte)(Regions.WORLD_SIZE / 2);

        ForEach(centerX, centerY, action);
    }
    public static void ForEach(RegionActionWhile action)
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
    public static void ForEach(byte centerX, byte centerY, RegionActionWhile action)
    {
        SurroundingRegionsIterator iterator = new SurroundingRegionsIterator(centerX, centerY);
        while (iterator.MoveNext())
        {
            RegionCoord coord = iterator.Current;
            if (!action(coord))
                break;
        }
    }
    public static void ForEach(Vector3 center, RegionActionWhile action)
    {
        if (!Regions.tryGetCoordinate(center, out byte centerX, out byte centerY))
            centerX = centerY = (byte)(Regions.WORLD_SIZE / 2);

        ForEach(centerX, centerY, action);
    }
}

public delegate void RegionAction(RegionCoord coord);
public delegate bool RegionActionWhile(RegionCoord coord);