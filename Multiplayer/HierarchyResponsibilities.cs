using DevkitServer.Configuration;
using DevkitServer.Util.Encoding;
using SDG.Framework.Devkit;

namespace DevkitServer.Multiplayer;
/// <summary>
/// Stores the user that's responsible for placing an <see cref="IDevkitHierarchyItem"/>. On the client it only stores a set of the ones you placed.
/// </summary>
public static class HierarchyResponsibilities
{
    private const string Source = "HIERARCHY RESPONSIBILITIES";
#nullable disable
    private static InstanceIdResponsibilityTable Table;
    public static string SavePath { get; private set; }
#nullable restore

#if SERVER
    public static ulong GetPlacer(uint instanceId) => Table != null ? Table.GetPlacer(instanceId) : 0ul;
    public static bool IsPlacer(uint instanceId, ulong user) => Table != null && Table.IsPlacer(instanceId, user);
#else
    public static bool IsPlacer(uint instanceId) => Table != null && Table.IsPlacer(instanceId);
#endif
    public static void Init()
    {
        SavePath = Path.Combine(DevkitServerConfig.LevelDirectory, "Responsibilities", "hierarchy-responsibilities.dat");
        Table = new InstanceIdResponsibilityTable(SavePath, Source);
        Reload();
    }

    /// <summary>Reload from config file.</summary>
    public static void Reload(bool resaveIfNeeded = true) => Table?.Reload(resaveIfNeeded);
    /// <summary>Remove <paramref name="instanceId"/> from save.</summary>
    public static void Remove(uint instanceId, bool save = true) => Table?.Remove(instanceId, save);

    /// <summary>Set <paramref name="instanceId"/> in save. Sets to owned on client and sets owner on server.</summary>
    public static void Set(uint instanceId
#if SERVER
        , ulong steam64
#endif
        , bool save = true) => Table?.Set(instanceId,
#if SERVER
        steam64,
#endif
        save
    );

    /// <summary>Save all responsibilities.</summary>
    public static void Save() => Table?.Save();
}