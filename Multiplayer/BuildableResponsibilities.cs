using System.Globalization;
using DevkitServer.Configuration;
using DevkitServer.Levels;
using DevkitServer.Models;
using DevkitServer.Multiplayer.Networking;
using DevkitServer.Util.Encoding;

namespace DevkitServer.Multiplayer;
// todo send as level data
public static class BuildableResponsibilities
{
    private const string Source = "BUILDABLE RESPONSIBILITIES";
    private const ushort DataVersion = 0;
#nullable disable
    public static string SavePath { get; private set; }
#nullable restore
#if SERVER
    public static List<ulong>[,] Table = new List<ulong>[Regions.WORLD_SIZE, Regions.WORLD_SIZE];
#elif CLIENT
    public static List<bool>[,] Table = new List<bool>[Regions.WORLD_SIZE, Regions.WORLD_SIZE];
#endif
    private static readonly ByteWriter Writer = new ByteWriter(false);
    private static readonly ByteReader Reader = new ByteReader { LogOnError = false, ThrowOnError = true };

    public static readonly NetCallCustom SendResponsibilities = new NetCallCustom(NetCalls.SendBuildableResponsibilities);
    internal static void Init()
    {
        SavePath = Path.Combine(DevkitServerConfig.LevelDirectory, "Responsibilities", "buildable-responsibilities.dat");
#if SERVER
        Reload();
#endif
    }
    static BuildableResponsibilities()
    {
        for (int x = 0; x < Regions.WORLD_SIZE; ++x)
        {
            for (int y = 0; y < Regions.WORLD_SIZE; ++y)
            {
#if SERVER
                Table[x, y] = new List<ulong>(8);
#else
                Table[x, y] = new List<bool>(8);
#endif
            }
        }
    }

#if SERVER
    public static ulong GetPlacer(RegionIdentifier id)
    {
        if (Table == null || id.IsInvalid)
            return 0ul;

        List<ulong> placers = Table[id.X, id.Y];
        return placers != null && id.Index < placers.Count ? placers[id.Index] : 0ul;
    }
    public static bool IsPlacer(RegionIdentifier id, ulong user) => GetPlacer(id) == user;
    public static void Set(RegionIdentifier id, ulong user, bool save = true)
    {
        if (Table == null || id.IsInvalid)
            return;

        List<ulong> placers = Table[id.X, id.Y];
        if (placers != null)
        {
            if (id.Index < placers.Count)
                placers[id.Index] = user;
            else
            {
                for (int i = placers.Count; i <= id.Index; ++i)
                    placers.Add(i == id.Index ? user : 0ul);
            }
        }

        if (save)
            Save();
    }
#else
    public static void Set(RegionIdentifier id, bool state)
    {
        if (Table == null || id.IsInvalid)
            return;

        List<bool> isPlacers = Table[id.X, id.Y];
        if (isPlacers != null)
        {
            if (id.Index < isPlacers.Count)
                isPlacers[id.Index] = state;
            else
            {
                for (int i = isPlacers.Count; i <= id.Index; ++i)
                    isPlacers.Add(i == id.Index ? state : !state);
            }
        }
    }
    public static bool IsPlacer(RegionIdentifier id)
    {
        if (Table == null || id.IsInvalid)
            return false;

        List<bool> isPlacers = Table[id.X, id.Y];
        return isPlacers != null && id.Index < isPlacers.Count && isPlacers[id.Index];
    }
#endif

    internal static int WriteTable(ByteWriter writer, List<ulong>[,] table)
    {
        ThreadUtil.assertIsGameThread();
        byte worldSize = Regions.WORLD_SIZE;
        int ct = 0;
        ushort[] counts = new ushort[worldSize * worldSize];

        for (int x = 0; x < worldSize; ++x)
        {
            for (int y = 0; y < worldSize; ++y)
            {
                List<ulong>? region = table[x, y];
                int max = region == null ? 0 : Math.Min(region.Count, ushort.MaxValue);
                ct += max;
                counts[x * worldSize + y] = (ushort)max;
            }
        }
        writer.WriteZeroCompressed(counts, false);
        ulong[] data = new ulong[ct];
        int index = -1;
        for (int x = 0; x < worldSize; ++x)
        {
            for (int y = 0; y < worldSize; ++y)
            {
                int ct2 = counts[x * worldSize + y];
                List<ulong> region = table[x, y];
                for (int i = 0; i < ct2; ++i)
                {
                    data[++index] = region.Count > i ? region[i] : 0ul;
                }
            }
        }

        writer.WriteZeroCompressed(data, true);
        return ct;
    }
    internal static int ReadToTable(ByteReader reader, List<ulong>[,] table)
    {
        ThreadUtil.assertIsGameThread();
        
        ushort[] counts = reader.ReadZeroCompressedUInt16Array(false);
        ulong[] data = reader.ReadZeroCompressedUInt64Array(true);
        byte worldSize = Regions.WORLD_SIZE;
        int index = -1;
        for (int x = 0; x < worldSize; ++x)
        {
            for (int y = 0; y < worldSize; ++y)
            {
                List<ulong>? region = table[x, y];
                if (counts.Length <= x * worldSize + y)
                {
                    if (region == null)
                        table[x, y] = new List<ulong>();
                    else
                        region.Clear();
                    continue;
                }
                int ct = counts[x * worldSize + y];
                if (region == null)
                {
                    region = new List<ulong>(ct);
                    table[x, y] = region;
                }
                if (region.Count > ct)
                    region.RemoveRange(ct, region.Count - ct);
                if (region.Capacity < ct)
                    region.Capacity = ct;
                for (int i = 0; i < ct; ++i)
                {
                    ulong value = data[++index];

                    if (i < region.Count)
                        region[i] = value;
                    else region.Add(value);
                }
            }
        }

        return data.Length;
    }
    public static void GatherData(LevelData data)
    {
        int worldSize = Regions.WORLD_SIZE;
        data.BuildableData = new List<ulong>[worldSize, worldSize];
        for (int x = 0; x < worldSize; ++x)
        {
            for (int y = 0; y < worldSize; ++y)
            {
#if SERVER
                data.BuildableData[x, y] = new List<ulong>(Table[x, y] ?? (IEnumerable<ulong>)Array.Empty<ulong>());
#elif CLIENT
                List<bool> region = Table[x, y];
                if (region == null)
                    data.BuildableData[x, y] = new List<ulong>(0);
                else
                {
                    List<ulong> sendRegion = new List<ulong>(region.Count);

                    for (int i = 0; i < region.Count; ++i)
                        sendRegion.Add(region[i] ? Provider.client.m_SteamID : 0ul);

                    data.BuildableData[x, y] = sendRegion;
                }
#endif
            }
        }
    }
#if SERVER
    /// <summary>Reload from config file.</summary>
    public static void Reload()
    {
        ThreadUtil.assertIsGameThread();

        byte worldSize = Regions.WORLD_SIZE;
        try
        {
            if (File.Exists(SavePath))
            {
                using FileStream stream = new FileStream(SavePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                Reader.LoadNew(stream);
                Reader.Skip(sizeof(ushort)); // version

                int ct = ReadToTable(Reader, Table);

                Logger.LogDebug($"[{Source}] Read {ct.Format()} responsiblities in table stored at {SavePath.Format()}.");
            }
            else
            {
                for (int x = 0; x < worldSize; ++x)
                {
                    for (int y = 0; y < worldSize; ++y)
                    {
                        if (Table[x, y] != null)
                            Table[x, y].Clear();
                        else Table[x, y] = new List<ulong>();
                    }
                }
                Logger.LogDebug($"[{Source}] Loaded new responsiblities table.");
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning("Error reading buildable responsibilities.", method: Source);
            Logger.LogError(ex, method: Source);
            try
            {
                if (File.Exists(SavePath))
                {
                    string backup = DevkitServerUtility.BackupFile(SavePath, true);
                    Logger.LogInfo($"[{Source}] Backed up {SavePath.Format()} to {backup.Format()}.");
                }
            }
            catch (Exception ex2)
            {
                Logger.LogWarning("Error backing up buildable responsibilities.", method: Source);
                Logger.LogError(ex2, method: Source);
            }
            for (int x = 0; x < worldSize; ++x)
            {
                for (int y = 0; y < worldSize; ++y)
                {
                    if (Table[x, y] != null)
                        Table[x, y].Clear();
                    else Table[x, y] = new List<ulong>();
                }
            }
        }
    }
    /// <summary>Save all responsibilities.</summary>
    public static void Save()
    {
        if (SavePath == null)
            return;
        ThreadUtil.assertIsGameThread();
        
        DevkitServerUtility.CheckDirectory(false, Path.GetDirectoryName(SavePath)!);
        Thread.BeginCriticalRegion();
        try
        {
            using FileStream stream = new FileStream(SavePath, FileMode.Create, FileAccess.Write, FileShare.Read);
            Writer.Stream = stream;
            Writer.Write(DataVersion);

            WriteTable(Writer, Table);

            Writer.Flush();
        }
        finally
        {
            Thread.EndCriticalRegion();
        }
    }
#endif
}
