using DevkitServer.API;
using DevkitServer.Configuration;
using DevkitServer.Multiplayer.Levels;
using DevkitServer.Multiplayer.Networking;
using DevkitServer.Util.Encoding;
using System.Diagnostics;

namespace DevkitServer.Levels;
[EarlyTypeInit]
public sealed class LevelData
{
    private static readonly uint DataVersion = 0;

    private static readonly InstanceSetter<LevelInfo, string> SetFilePath = Accessor.GenerateInstancePropertySetter<LevelInfo, string>("path", throwOnError: true)!;

    private string? _lclPath;
    public uint Version { get; private set; }
    public ArraySegment<byte> Data { get; internal set; } = null!;
    internal List<object>? ReplicatedLevelData { get; set; }
    public Folder LevelFolderContent { get; private set; }
    public bool Compressed { get; internal set; }
    private LevelData() { }
    public LevelData(LevelData other)
    {
        LevelFolderContent = other.LevelFolderContent;
        _lclPath = other._lclPath;
        Data = other.Data;
        Compressed = other.Compressed;
        Version = DataVersion;
        ReplicatedLevelData = other.ReplicatedLevelData;
    }
    public static LevelData GatherLevelData(bool saveToTemp
#if SERVER
        , bool gatherLevelData = true
#endif
        )
    {
        ThreadUtil.assertIsGameThread();
#if DEBUG
        Stopwatch stopwatch = Stopwatch.StartNew();
#endif
        DirtyManagerState? dirtyState = null;
        string newPath, oldPath = newPath = Level.info.path;
        if (saveToTemp)
        {
            newPath = Path.Combine(DevkitServerConfig.TempFolder, "Level");
            try
            {
                DevkitServerUtility.CopyDirectory(oldPath, newPath);
                SetFilePath(Level.info, newPath);
            }
            catch (AggregateException ex)
            {
                Logger.LogError($"Error copying directory to avoid saving ({oldPath.Format()} -> {newPath.Format()}), saving to main directory.", method: "EDITOR LEVEL");
#if DEBUG
                Logger.LogError(ex, method: "EDITOR LEVEL");
#endif
                newPath = oldPath;
                saveToTemp = false;
            }

            dirtyState = DirtyManagerState.Create();
        }

        Level.save();

        if (saveToTemp)
            SetFilePath(Level.info, oldPath);

        dirtyState?.Apply();
        Folder folder = new Folder(newPath, ShouldSendFile, null);
        LevelData data = new LevelData
        {
            LevelFolderContent = folder,
            _lclPath = newPath
        };

#if SERVER
        if (gatherLevelData)
            ReplicatedLevelDataRegistry.SaveToLevelData(data);
#endif

#if DEBUG
        Logger.LogDebug($"[EDITOR LEVEL] GatherLevelData took {stopwatch.GetElapsedMilliseconds().Format("F2")} ms.");
#endif
        return data;
    }

    private static ByteWriter? _levelWriter;
    private static ByteReader? _levelReader;
    public static LevelData Read(ArraySegment<byte> payload)
    {
        ThreadUtil.assertIsGameThread();
        ByteReader reader = _levelReader ??= new ByteReader { ThrowOnError = true };
        reader.LoadNew(payload);

        LevelData data = new LevelData
        {
            Version = reader.ReadUInt32(),
            Data = payload
        };

        ReplicatedLevelDataRegistry.Read(data, reader);

        data.LevelFolderContent = Folder.Read(_levelReader);

        reader.LoadNew(Array.Empty<byte>());
        return data;
    }
    public void WriteToData(bool flush)
    {
        ThreadUtil.assertIsGameThread();
        ByteWriter writer = _levelWriter ??= new ByteWriter(false, 134217728); // 128 MiB
        writer.Write(DataVersion);
        Version = DataVersion;

        ReplicatedLevelDataRegistry.Write(this, writer);

        Folder folder = LevelFolderContent;
        if (_lclPath != null)
        {
            long dirSize = DevkitServerUtility.GetDirectorySize(_lclPath);
            if (dirSize <= int.MaxValue && dirSize > 0)
                writer.ExtendBuffer(writer.Count + (int)dirSize);
        }

        Folder.Write(writer, in folder);

        if (flush)
        {
            Data = writer.ToArray();
            writer.Flush();
        }
        else
        {
            Data = writer.ToArraySegmentAndDontFlush();
        }
    }

    internal static bool ShouldSendFile(FileInfo file)
    {
        string nm = file.Name;
        string? origDir = file.DirectoryName;
        if (nm.Equals("Level.png", StringComparison.Ordinal)
            || nm.Equals("Map.png", StringComparison.Ordinal)
            || nm.Equals("Preview.png", StringComparison.Ordinal)
            || nm.Equals("Icon.png", StringComparison.Ordinal)
            || nm.Equals("Chart.png", StringComparison.Ordinal)
            || nm.Equals("Camera.dat", StringComparison.Ordinal) && origDir != null && origDir.EndsWith("Editor")
            || nm.Equals("Height.dat", StringComparison.Ordinal) && origDir != null && origDir.EndsWith("Editor")
            || nm.Equals("Materials.dat", StringComparison.Ordinal) && origDir != null && origDir.EndsWith("Editor")
            || nm.Equals("Objects.dat", StringComparison.Ordinal) && origDir != null && origDir.EndsWith("Editor")
            || nm.Equals("Spawns.dat", StringComparison.Ordinal) && origDir != null && origDir.EndsWith("Editor"))
            return false;

        string? dir = Path.GetFileName(origDir);
        if (Path.GetExtension(nm).Equals(".png") && dir != null &&
            dir.Equals("Screenshots", StringComparison.Ordinal))
            return false;

        return true;
    }
}