namespace DevkitServer.Util;
public static class NavigationUtil
{
    private static readonly StaticGetter<List<Flag>> GetNavigationFlags = Accessor.GenerateStaticGetter<LevelNavigation, List<Flag>>("flags", throwOnError: true)!;
    private static readonly Action<Flag>? UpdateNavmesh = Accessor.GenerateInstanceCaller<Flag, Action<Flag>>("updateNavmesh", false);

    /// <returns>A readonly value collection used to loop through all the existing navigations.</returns>
    public static IReadOnlyList<Flag> NavigationFlags => GetNavigationFlags();
    /// <summary>
    /// If possible, use <see cref="NavigationFlags"/> instead.
    /// </summary>
    /// <returns>A copy of all existing navigation flags.</returns>
    public static List<Flag> GetAllNavigationFlags() => new List<Flag>(NavigationFlags);

    /// <summary>
    /// Calls <see cref="Flag"/>.updateNavmesh if in editor mode.
    /// </summary>
    /// <returns><see langword="false"/> in the case of a reflection failure.</returns>
    public static bool UpdateEditorNavmesh(this Flag flag)
    {
        if (UpdateNavmesh == null)
            return false;

        UpdateNavmesh(flag);
        return true;
    }
}
