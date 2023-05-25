using DevkitServer.Configuration;
using DevkitServer.Util.Encoding;
using SDG.Framework.Devkit;

namespace DevkitServer.Multiplayer;
/// <summary>
/// Stores the user that's responsible for placing an <see cref="LevelObject"/>. On the client it only stores a set of the ones you placed.
/// </summary>
public static class LevelObjectResponsibilities
{
    private const string Source = "LEVEL OBJECT RESPONSIBILITIES";

    private static readonly InstanceIdResponsibilityTable Table = new InstanceIdResponsibilityTable(Path.Combine(DevkitServerConfig.LevelDirectory, "level-object-responsibilities.dat"), Source);
    
#if SERVER
    public static ulong GetPlacer(uint instanceId) => Table.GetPlacer(instanceId);
    public static bool IsPlacer(uint instanceId, ulong user) => Table.IsPlacer(instanceId, user);
#else
    public static bool IsPlacer(uint instanceId) => Table.IsPlacer(instanceId);
#endif

    /// <summary>Reload from config file.</summary>
    public static void Reload(bool resaveIfNeeded = true) => Table.Reload(resaveIfNeeded);
    /// <summary>Remove <paramref name="instanceId"/> from save.</summary>
    public static void Remove(uint instanceId, bool save = true) => Table.Remove(instanceId, save);

    /// <summary>Set <paramref name="instanceId"/> in save. Sets to owned on client and sets owner on server.</summary>
    public static void Set(uint instanceId
#if SERVER
        , ulong steam64
#endif
        , bool save = true) => Table.Set(instanceId,
#if SERVER
        steam64,
#endif
        save
    );

    /// <summary>Save all responsibilities.</summary>
    public static void Save() => Table.Save();
}