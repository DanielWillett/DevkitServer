using DevkitServer.Configuration;
using DevkitServer.Models;
using DevkitServer.Multiplayer.Levels;
using DevkitServer.Multiplayer.Networking;
using DevkitServer.Util.Encoding;

namespace DevkitServer.Multiplayer;

public sealed class BuildableResponsibilities : IReplicatedLevelDataSource<BuildableResponsibilitiesReplicatedLevelData>
{
    public ushort CurrentDataVersion => 0;
#if SERVER
    private const string Source = "BUILDABLE RESPONSIBILITIES";
    private const ushort FileDataVersion = 0;

    private static readonly ByteWriter Writer = new ByteWriter(false);
    private static readonly ByteReader Reader = new ByteReader { LogOnError = false, ThrowOnError = true };
#endif

#nullable disable
    public static string SavePath { get; private set; }
#nullable restore

#if SERVER
    public static List<ulong>[,] Table = new List<ulong>[Regions.WORLD_SIZE, Regions.WORLD_SIZE];
#elif CLIENT
    public static List<bool>[,] Table = new List<bool>[Regions.WORLD_SIZE, Regions.WORLD_SIZE];
#endif

    public static readonly NetCallCustom SendResponsibilities = new NetCallCustom(NetCalls.SendBuildableResponsibilities);
    internal static void Init()
    {
        SavePath = Path.Combine(DevkitServerConfig.LevelDirectory, "Responsibilities", "buildable-responsibilities.dat");
#if SERVER
        Reload();
#endif
    }
    private BuildableResponsibilities() { }
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
            Writer.Write(FileDataVersion);

            WriteTable(Writer, Table);

            Writer.Flush();
        }
        finally
        {
            Thread.EndCriticalRegion();
        }
    }
#endif
#if CLIENT
    public void LoadData(BuildableResponsibilitiesReplicatedLevelData data)
    {
        int worldSize = Regions.WORLD_SIZE;
        List<ulong>[,] table = data.Table;
        ulong client = Provider.client.m_SteamID;
        for (int x = 0; x < worldSize; ++x)
        {
            for (int y = 0; y < worldSize; ++y)
            {
                List<ulong> serverData = table[x, y];
                ref List<bool> localData = ref Table[x, y];
                if (localData == null)
                    localData = new List<bool>(serverData.Count);
                else
                    localData.IncreaseCapacity(serverData.Count);
                List<bool> localData2 = localData;
                for (int i = 0; i < serverData.Count; ++i)
                {
                    if (localData2.Count > i)
                        localData2[i] = serverData[i] == client;
                    else
                        localData2.Add(serverData[i] == client);
                }
            }
        }
    }
#elif SERVER
    public BuildableResponsibilitiesReplicatedLevelData SaveData()
    {
        int worldSize = Regions.WORLD_SIZE;
        List<ulong>[,] data = new List<ulong>[worldSize, worldSize];
        for (int x = 0; x < worldSize; ++x)
        {
            for (int y = 0; y < worldSize; ++y)
            {
                data[x, y] = new List<ulong>(Table[x, y] ?? (IEnumerable<ulong>)Array.Empty<ulong>());
            }
        }

        return new BuildableResponsibilitiesReplicatedLevelData
        {
            Table = data
        };
    }
#endif

    public void WriteData(ByteWriter writer, BuildableResponsibilitiesReplicatedLevelData data)
    {
        WriteTable(writer, data.Table);
    }
    public BuildableResponsibilitiesReplicatedLevelData ReadData(ByteReader reader, ushort dataVersion)
    {
        int worldSize = Regions.WORLD_SIZE;
        List<ulong>[,] table = new List<ulong>[worldSize, worldSize];

        ReadToTable(reader, table);

        return new BuildableResponsibilitiesReplicatedLevelData
        {
            Table = table
        };
    }
}

#nullable disable
public class BuildableResponsibilitiesReplicatedLevelData
{
    public List<ulong>[,] Table { get; set; }
}
#nullable restore