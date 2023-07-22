using SDG.Framework.Foliage;

namespace DevkitServer.Util;

[EarlyTypeInit]
public static class FoliageUtil
{
    private static readonly StaticGetter<Dictionary<FoliageCoord, FoliageTile>> GetTiles =
        Accessor.GenerateStaticGetter<FoliageSystem, Dictionary<FoliageCoord, FoliageTile>>("tiles", throwOnError: true)!;

    /// <returns>A readonly value collection used to loop through all the existing tiles.</returns>
    public static IReadOnlyCollection<FoliageTile> Tiles => GetTiles().Values;

    /// <summary>
    /// If possible, use <see cref="Tiles"/> instead.
    /// </summary>
    /// <returns>A copy of all existing tiles.</returns>
    public static List<FoliageTile> GetAllTiles() => new List<FoliageTile>(Tiles);
    public static bool Encapsulates(this in FoliageBounds outer, in FoliageBounds inner) =>
        outer.min.x < inner.min.x && outer.min.y < inner.min.y && outer.max.x > inner.max.x && outer.max.y > inner.max.y;
    public static bool Overlaps(this in FoliageBounds left, in FoliageBounds right) =>
        !(left.max.x < right.min.x || left.max.y < right.min.y || left.min.x > right.max.x || left.min.y > right.max.y);
    public static void Encapsulate(this ref FoliageBounds left, in FoliageBounds right)
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
}
