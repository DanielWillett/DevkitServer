using DevkitServer.Configuration;
using DevkitServer.Models;
using DevkitServer.Multiplayer.Networking;
using DevkitServer.Util.Encoding;

namespace DevkitServer.Multiplayer;
// todo convert to NetIds on client-side, and just dont save this at all
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
    private static readonly ByteReader Reader = new ByteReader { LogOnError = false };

    public static readonly NetCallCustom SendResponsibilities = new NetCallCustom(NetCalls.SendBuildableResponsibilities);
    internal static void Init()
    {
        SavePath = Path.Combine(DevkitServerConfig.LevelDirectory, "Responsibilities", "buildable-responsibilities.dat");
        Reload();
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

        List<ulong> isPlacers = Table[id.X, id.Y];
        if (isPlacers != null)
        {
            if (id.Index < isPlacers.Count)
                isPlacers[id.Index] = user;
            else
            {
                for (int i = isPlacers.Count; i <= id.Index; ++i)
                    isPlacers.Add(i == id.Index ? user : 0ul);
            }
        }

        if (save)
            Save();
    }
#else
    public static void Set(RegionIdentifier id, bool state, bool save = true)
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

        if (save)
            Save();
    }
    public static bool IsPlacer(RegionIdentifier id)
    {
        if (Table == null || id.IsInvalid)
            return false;

        List<bool> isPlacers = Table[id.X, id.Y];
        return isPlacers != null && id.Index < isPlacers.Count && isPlacers[id.Index];
    }
#endif

#if SERVER
    internal static void SendResponsibilties(ITransportConnection connection, ulong steam64)
    {
         DevkitServerModule.ComponentHost.StartCoroutine(SendResponsibilitiesAndRetry(connection, steam64));
    }
    private static IEnumerator SendResponsibilitiesAndRetry(ITransportConnection connection, ulong steam64)
    {
        NetTask task = SendResponsibilities.RequestAck(connection, w => Write(w, steam64), 5000);
        yield return task;
        if (task.Parameters.Success)
        {
            Logger.LogDebug($"Sent buildable responsibilties to {steam64.Format()}.");
            yield break;
        }
        Logger.LogWarning($"{steam64.Format()} ({connection.Format()}) did not reply to buildable responsibilities message, retrying 0/1.");
        task = SendResponsibilities.RequestAck(connection, w => Write(w, steam64), 10000);
        yield return task;
        if (task.Parameters.Success)
        {
            Logger.LogDebug($"Sent buildable responsibilties to {steam64.Format()} (after 1 retry).");
            yield break;
        }
        Logger.LogWarning($"{steam64.Format()} ({connection.Format()}) did not reply to buildable responsibilities message, done retrying.");
    }
    private static void Write(ByteWriter writer, ulong target)
    {
        ThreadUtil.assertIsGameThread();

        int ct = 0;
        byte worldSize = Regions.WORLD_SIZE;
        ushort[] counts = new ushort[worldSize * worldSize];
        for (int x = 0; x < worldSize; ++x)
        {
            for (int y = 0; y < worldSize; ++y)
            {
                List<ulong> region = Table[x, y];
                int max = LevelObjects.buildables[x, y].Count;
                if (region.Count > max)
                    region.RemoveRange(max, region.Count - max + 1);
                max = Math.Min(region.Count, ushort.MaxValue);
                ct += max;
                counts[x * worldSize + y] = (ushort)max;
            }
        }
        bool[] data = new bool[ct];
        int index = -1;
        for (int x = 0; x < worldSize; ++x)
        {
            for (int y = 0; y < worldSize; ++y)
            {
                List<ulong> region = Table[x, y];
                for (int i = 0; i < region.Count; ++i)
                    data[++index] = region[i] == target;
            }
        }

        writer.WriteZeroCompressed(counts, false);
        writer.WriteLong(data);
    }
#elif CLIENT
    [NetCall(NetCallSource.FromServer, NetCalls.SendBuildableResponsibilities)]
    private static void Read(MessageContext ctx, ByteReader reader)
    {
        ThreadUtil.assertIsGameThread();

        bool[] data = reader.ReadBoolArray();
        ushort[] counts = reader.ReadZeroCompressedUInt16Array(false);
        byte worldSize = Regions.WORLD_SIZE;
        int index = -1;
        for (int x = 0; x < worldSize; ++x)
        {
            for (int y = 0; y < worldSize; ++y)
            {
                List<bool> region = Table[x, y];
                if (counts.Length <= x * worldSize + y)
                {
                    region.Clear();
                    continue;
                }
                int ct = counts[x * worldSize + y];

                if (region.Capacity < ct)
                    region.Capacity = ct;
                for (int i = 0; i < ct; ++i)
                {
                    if (i < region.Count)
                        region[i] = data[++index];
                    else region.Add(data[++index]);
                }
            }
        }

        ctx.Acknowledge(StandardErrorCode.Success);
        Save();
    }
#endif
    /// <summary>Reload from config file.</summary>
    public static void Reload()
    {
        ThreadUtil.assertIsGameThread();

        byte worldSize = Regions.WORLD_SIZE;
        if (File.Exists(SavePath))
        {
            using FileStream stream = new FileStream(SavePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            Reader.LoadNew(stream);
            Reader.Skip(sizeof(ushort)); // version
            ushort[] counts = Reader.ReadZeroCompressedUInt16Array(false);
#if SERVER
            ulong[] data = Reader.ReadZeroCompressedUInt64Array(true);
#elif CLIENT
            bool[] data = Reader.ReadLongBoolArray();
#endif
            int index = -1;
            for (int x = 0; x < worldSize; ++x)
            {
                for (int y = 0; y < worldSize; ++y)
                {
#if SERVER
                    List<ulong> region = Table[x, y] ??= new List<ulong>(8);
#elif CLIENT
                    List<bool> region = Table[x, y] ??= new List<bool>(8);
#endif
                    if (counts.Length <= x * worldSize + y)
                    {
                        region.Clear();
                        continue;
                    }
                    int ct = counts[x * worldSize + y];

                    if (region.Capacity < ct)
                        region.Capacity = ct;
                    for (int i = 0; i < ct; ++i)
                    {
                        if (i < region.Count)
                            region[i] = data[++index];
                        else region.Add(data[++index]);
                    }
                }
            }

            Logger.LogDebug($"[{Source}] Read {data.Length.Format()} responsiblities in table stored at {SavePath.Format()}.");
        }
        else
        {
            for (int x = 0; x < worldSize; ++x)
            {
                for (int y = 0; y < worldSize; ++y)
                {
                    if (Table[x, y] != null)
                        Table[x, y].Clear();
#if SERVER
                    else Table[x, y] = new List<ulong>();
#elif CLIENT
                    else Table[x, y] = new List<bool>();
#endif
                }
            }
            Logger.LogDebug($"[{Source}] Loaded new responsiblities table.");
        }
    }
    /// <summary>Save all responsibilities.</summary>
    public static void Save()
    {
        if (SavePath == null)
            return;
        ThreadUtil.assertIsGameThread();

        int removed = 0;
        DevkitServerUtility.CheckDirectory(false, Path.GetDirectoryName(SavePath)!);
        Thread.BeginCriticalRegion();
        try
        {
            using FileStream stream = new FileStream(SavePath, FileMode.Create, FileAccess.Write, FileShare.Read);
            Writer.Stream = stream;
            Writer.Write(DataVersion);

            int ct = 0;
            byte worldSize = Regions.WORLD_SIZE;
            ushort[] counts = new ushort[worldSize * worldSize];
            for (int x = 0; x < worldSize; ++x)
            {
                for (int y = 0; y < worldSize; ++y)
                {
#if SERVER
                    List<ulong> region = Table[x, y];
#elif CLIENT
                    List<bool> region = Table[x, y];
#endif
                    int max = LevelObjects.buildables[x, y].Count;
                    if (region.Count > max)
                    {
                        int len = region.Count - max;
                        removed += len;
                        region.RemoveRange(max, len);
                    }
                    max = Math.Min(region.Count, ushort.MaxValue);
                    ct += max;
                    counts[x * worldSize + y] = (ushort)max;
                }
            }
#if SERVER
            ulong[] data = new ulong[ct];
#elif CLIENT
            bool[] data = new bool[ct];
#endif
            int index = -1;
            for (int x = 0; x < worldSize; ++x)
            {
                for (int y = 0; y < worldSize; ++y)
                {
#if SERVER
                    List<ulong> region = Table[x, y];
#elif CLIENT
                    List<bool> region = Table[x, y];
#endif
                    for (int i = 0; i < region.Count; ++i)
                    {
                        data[++index] = region[i];
                    }
                }
            }

            Writer.WriteZeroCompressed(counts, false);
#if SERVER
            Writer.WriteZeroCompressed(data, true);
#elif CLIENT
            Writer.WriteLong(data);
#endif

            if (removed > 0)
                Logger.LogDebug($"[{Source}] Removed {removed.Format()} expired responsibilities.");

            Writer.Flush();
        }
        finally
        {
            Thread.EndCriticalRegion();
        }
    }
}
