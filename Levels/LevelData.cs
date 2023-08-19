using System.Diagnostics;
using DevkitServer.Models;
using DevkitServer.Multiplayer;
using DevkitServer.Multiplayer.Levels;
using DevkitServer.Multiplayer.Networking;
using DevkitServer.Util.Encoding;

namespace DevkitServer.Levels;
[EarlyTypeInit]
public sealed class LevelData
{
    private static readonly uint DataVersion = 0;
    public uint Version { get; private set; }
#nullable disable
    public Folder LevelFolderContent { get; private set; }
    public NetId[] HierarchyItemNetIds { get; internal set; }
    public uint[] HierarchyItems { get; internal set; }
    public NetId[] LevelObjectNetIds { get; internal set; }
    public uint[] Objects { get; internal set; }
    public RegionIdentifier[] Buildables { get; internal set; }
    public byte[] Data { get; internal set; }
    public bool Compressed { get; internal set; }
    public List<ulong>[,] BuildableData { get; internal set; }

    public int SpawnIndexPlayer { get; internal set; }
    public int SpawnIndexVehicle { get; internal set; }
    public int SpawnIndexItem { get; internal set; }
    public int SpawnIndexZombie { get; internal set; }
    public int SpawnCount { get; internal set; }
    public NetId[] SpawnNetIds { get; internal set; }
    public int[] SpawnIndexes { get; internal set; }
#nullable restore
    private string? _lclPath;
    private LevelData() { }

    public LevelData(LevelData other)
    {
        LevelFolderContent = other.LevelFolderContent;
        _lclPath = other._lclPath;
        Data = other.Data;
        Compressed = other.Compressed;
        Objects = other.Objects;
        Buildables = other.Buildables;
        LevelObjectNetIds = other.LevelObjectNetIds;
        HierarchyItemNetIds = other.HierarchyItemNetIds;
        HierarchyItems = other.HierarchyItems;
        BuildableData = other.BuildableData;
        Version = DataVersion;
    }
    public static LevelData GatherLevelData()
    {
        ThreadUtil.assertIsGameThread();
#if DEBUG
        Stopwatch stopwatch = Stopwatch.StartNew();
#endif
        Level.save();
        string lclPath = Level.info.path;
        Folder folder = new Folder(lclPath, ShouldSendFile, null);
        LevelData data = new LevelData
        {
            LevelFolderContent = folder,
            _lclPath = lclPath
        };
        LevelObjectNetIdDatabase.GatherData(data);
        HierarchyItemNetIdDatabase.GatherData(data);
        BuildableResponsibilities.GatherData(data);
        SpawnpointNetIdDatabase.GatherData(data);
#if DEBUG
        Logger.LogDebug($"[EDITOR LEVEL] GatherLevelData took {stopwatch.GetElapsedMilliseconds().Format("F2")} ms.");
#endif
        return data;
    }

    private static ByteWriter? _levelWriter;
    private static ByteReader? _levelReader;
    public static LevelData Read(byte[] payload)
    {
        ThreadUtil.assertIsGameThread();
        ByteReader reader = _levelReader ??= new ByteReader { ThrowOnError = true };
        reader.LoadNew(payload);

        LevelData data = new LevelData
        {
            Version = reader.ReadUInt32(),
            Data = payload
        };
        int netIdCount = reader.ReadInt32();
        int buildableCount = reader.ReadInt32();
        int hierarchyItemCount = reader.ReadInt32();

        NetId[] levelObjectNetIds = new NetId[netIdCount];
        NetId[] hierarchyItemNetIds = new NetId[hierarchyItemCount];
        RegionIdentifier[] buildables = buildableCount == 0 ? Array.Empty<RegionIdentifier>() : new RegionIdentifier[buildableCount];
        uint[] objects = new uint[netIdCount - buildableCount];
        uint[] hierarchyItems = new uint[hierarchyItemCount];

        // LevelObjectNetIdDatabase
        for (int i = 0; i < netIdCount; ++i)
            levelObjectNetIds[i] = new NetId(reader.ReadUInt32());
        for (int i = 0; i < buildableCount; ++i)
            buildables[i] = RegionIdentifier.Read(reader);
        for (int i = 0; i < objects.Length; ++i)
            objects[i] = reader.ReadUInt32();

        data.LevelObjectNetIds = levelObjectNetIds;
        data.Buildables = buildables;
        data.Objects = objects;

        // HierarchyItemNetIdDatabase
        for (int i = 0; i < hierarchyItemCount; ++i)
            hierarchyItemNetIds[i] = new NetId(reader.ReadUInt32());
        for (int i = 0; i < hierarchyItemCount; ++i)
            hierarchyItems[i] = reader.ReadUInt32();

        data.HierarchyItemNetIds = hierarchyItemNetIds;
        data.HierarchyItems = hierarchyItems;

        data.BuildableData = new List<ulong>[Regions.WORLD_SIZE, Regions.WORLD_SIZE];
        BuildableResponsibilities.ReadToTable(reader, data.BuildableData);

        // SpawnpointNetIdDatabase
        SpawnpointNetIdDatabase.ReadToDatabase(reader, data);

        data.LevelFolderContent = Folder.Read(_levelReader);

        reader.LoadNew(Array.Empty<byte>());
        return data;
    }
    public void WriteToData()
    {
        ThreadUtil.assertIsGameThread();
        ByteWriter writer = _levelWriter ??= new ByteWriter(false, 134217728); // 128 MiB
        writer.Write(DataVersion);
        Version = DataVersion;
        NetId[] levelObjectNetIds = LevelObjectNetIds;
        NetId[] hierarchyItemNetIds = HierarchyItemNetIds;
        RegionIdentifier[] buildables = Buildables;
        uint[] objects = Objects;
        uint[] hierarchyItems = HierarchyItems;
        writer.Write(levelObjectNetIds.Length);
        writer.Write(buildables.Length);
        writer.Write(hierarchyItems.Length);

        // LevelObjectNetIdDatabase
        for (int i = 0; i < levelObjectNetIds.Length; ++i)
            writer.Write(levelObjectNetIds[i].id);
        for (int i = 0; i < buildables.Length; ++i)
            RegionIdentifier.Write(writer, buildables[i]);
        for (int i = 0; i < objects.Length; ++i)
            writer.Write(objects[i]);

        // HierarchyItemNetIdDatabase
        for (int i = 0; i < hierarchyItems.Length; ++i)
            writer.Write(hierarchyItemNetIds[i].id);
        for (int i = 0; i < hierarchyItems.Length; ++i)
            writer.Write(hierarchyItems[i]);

        BuildableResponsibilities.WriteTable(writer, BuildableData);

        Folder folder = LevelFolderContent;
        if (_lclPath != null)
        {
            long dirSize = DevkitServerUtility.GetDirectorySize(_lclPath);
            if (dirSize <= int.MaxValue && dirSize > 0)
                writer.ExtendBuffer(writer.Count + (int)dirSize);
        }

        Folder.Write(writer, in folder);

        byte[] data = writer.ToArray();
        Data = data;
        writer.FinishWrite();
    }

    private static bool ShouldSendFile(FileInfo file)
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