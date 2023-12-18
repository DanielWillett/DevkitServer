using DevkitServer.Util.Encoding;

namespace DevkitServer.Multiplayer;
internal sealed class InstanceIdResponsibilityTable
{
    private const ushort DataVersion = 0;
    private bool _dirty = false;
#if SERVER
    private readonly Dictionary<uint, ulong> Responsibilities = new Dictionary<uint, ulong>(256);
#else
    private readonly HashSet<uint> Responsibilities = new HashSet<uint>(32);
#endif
    private static readonly ByteWriter Writer = new ByteWriter(false);
    private static readonly ByteReader Reader = new ByteReader { LogOnError = true };
    
    public string SavePath { get; }
    public string Source { get; }

    public InstanceIdResponsibilityTable(string savePath, string source)
    {
        SavePath = savePath;
        Source = source;
    }

#if SERVER
    public ulong GetPlacer(uint instanceId) => Responsibilities.TryGetValue(instanceId, out ulong placer) ? placer : 0ul;
    public bool IsPlacer(uint instanceId, ulong user) => Responsibilities.TryGetValue(instanceId, out ulong placer) && placer == user;
#else
    public bool IsPlacer(uint instanceId) => Responsibilities.Contains(instanceId);
#endif

    /// <summary>Reload from config file.</summary>
    public void Reload(bool resaveIfNeeded = true)
    {
        ThreadUtil.assertIsGameThread();

        Responsibilities.Clear();
        if (File.Exists(SavePath))
        {
            bool save = false;
            using FileStream stream = new FileStream(SavePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            Reader.LoadNew(stream);
            Reader.Skip(sizeof(ushort)); // version
            bool client = Reader.ReadBool();
            if (client == Dedicator.IsDedicatedServer)
            {
                Logger.LogWarning($"Invalid responsiblities file provided: {SavePath.Format(false)}. This file was created on the wrong platform (client vs. server).", method: Source);
                return;
            }
            while (Reader.BytesLeft > 0)
            {
                uint instanceId = Reader.ReadUInt32();
#if SERVER
                ulong steam64 = Reader.ReadUInt64();
#endif
                if (instanceId == 0
#if SERVER
                    || !steam64.UserSteam64()
#endif
                    )
                {
                    save = !Reader.HasFailed;
                    if (save)
                        continue;
                    break;
                }
#if SERVER
                if (Responsibilities.ContainsKey(instanceId))
                {
                    save = true;
                    Responsibilities[instanceId] = steam64;
                }
                else Responsibilities.Add(instanceId, steam64);
#else
                if (Responsibilities.Contains(instanceId))
                    save = true;
                else Responsibilities.Add(instanceId);
#endif
            }
            Logger.LogDebug($"[{Source}] Read {Responsibilities.Count.Format()} responsiblities in table stored at {SavePath.Format()}." + (save ? " Saving updates." : string.Empty));
            if (save && resaveIfNeeded)
                Save();
        }
        else
        {
            Logger.LogDebug($"[{Source}] Loaded new responsiblities table.");
        }

        _dirty = false;
    }
    /// <summary>Remove <paramref name="instanceId"/> from save.</summary>
    public void Remove(uint instanceId, bool save = true)
    {
        ThreadUtil.assertIsGameThread();

        _dirty = true;

        if (Responsibilities.Remove(instanceId) && save)
            Save();
    }
    /// <summary>Set <paramref name="instanceId"/> in save. Sets to owned on client and sets owner on server.</summary>
    public void Set(uint instanceId
#if SERVER
        , ulong steam64
#endif
        , bool save = true)
    {
        ThreadUtil.assertIsGameThread();

        _dirty = true;

#if SERVER
        if (!steam64.UserSteam64())
        {
            if (Responsibilities.Remove(instanceId) && save)
                Save();
            return;
        }
        if (Responsibilities.TryGetValue(instanceId, out ulong existing))
        {
            if (existing != steam64)
            {
                Responsibilities[instanceId] = steam64;
                if (save)
                    Save();
            }
            return;
        }

        Responsibilities.Add(instanceId, existing);
#else
        if (Responsibilities.Contains(instanceId))
            return;
        Responsibilities.Add(instanceId);
#endif
        if (!save) return;
        if (!File.Exists(SavePath))
        {
            Save();
            return;
        }
        using FileStream stream = new FileStream(SavePath, FileMode.Append, FileAccess.Write, FileShare.Read);
        Writer.Stream = stream;
        Writer.Write(instanceId);
#if SERVER
        Writer.Write(steam64);
#endif
        Writer.Flush();
        Writer.Stream = null;
        _dirty = false;
    }
    /// <summary>Save all responsibilities.</summary>
    public void Save()
    {
        ThreadUtil.assertIsGameThread();
        if (!_dirty && File.Exists(SavePath))
            return;
        _dirty = false;
        if (Responsibilities.Count > 0)
        {
            FileUtil.CheckDirectory(false, Path.GetDirectoryName(SavePath)!);
            Thread.BeginCriticalRegion();
            try
            {
                using FileStream stream = new FileStream(SavePath, FileMode.Create, FileAccess.Write, FileShare.Read);
                Writer.Stream = stream;
                Writer.Write(DataVersion);
                Writer.Write(!Dedicator.IsDedicatedServer);
#if SERVER
                foreach (KeyValuePair<uint, ulong> pair in Responsibilities)
                {
                    uint instanceId = pair.Key;
#else
                foreach (uint instanceId in Responsibilities)
                {
#endif
                    Writer.Write(instanceId);
#if SERVER
                    Writer.Write(pair.Value);
#endif
                }
                Writer.Flush();
            }
            finally
            {
                Thread.EndCriticalRegion();
            }
        }
        else if (File.Exists(SavePath))
            File.Delete(SavePath);
    }
}
