using System.Diagnostics;
using DevkitServer.Models;
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
#nullable restore
    private LevelData() { }

    public LevelData(LevelData other)
    {
        LevelFolderContent = other.LevelFolderContent;
        Data = other.Data;
        Compressed = other.Compressed;
        Objects = other.Objects;
        Buildables = other.Buildables;
        LevelObjectNetIds = other.LevelObjectNetIds;
        Version = DataVersion;
    }
    public static LevelData GatherLevelData()
    {
        ThreadUtil.assertIsGameThread();
#if DEBUG
        Stopwatch stopwatch = Stopwatch.StartNew();
#endif
        Level.save();
        Folder folder = new Folder(Level.info.path, ShouldSendFile, null);
        LevelData data = new LevelData
        {
            LevelFolderContent = folder,
        };
        LevelObjectNetIdDatabase.GatherData(data);
        HierarchyItemNetIdDatabase.GatherData(data);
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
            Data = payload,
            LevelFolderContent = Folder.Read(_levelReader)
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

        reader.LoadNew(Array.Empty<byte>());
        return data;
    }
    public void WriteToData()
    {
        ThreadUtil.assertIsGameThread();
        ByteWriter writer = _levelWriter ??= new ByteWriter(false, 134217728); // 128 MiB
        writer.Write(DataVersion);
        Version = DataVersion;
        Folder folder = LevelFolderContent;
        Folder.Write(writer, in folder);
        NetId[] levelObjectNetIds = LevelObjectNetIds;
        NetId[] hierarchyItemNetIds = HierarchyItemNetIds;
        RegionIdentifier[] buildables = Buildables;
        uint[] objects = Objects;
        uint[] hierarchyItems = HierarchyItems;
        writer.ExtendBuffer(writer.Buffer.Length + sizeof(int) * 3 + sizeof(uint) * levelObjectNetIds.Length + sizeof(int) * buildables.Length + sizeof(uint) * objects.Length + sizeof(uint) * 2 * hierarchyItems.Length);
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
        for (int i = 0; i < hierarchyItemNetIds.Length; ++i)
            writer.Write(hierarchyItemNetIds[i].id);
        for (int i = 0; i < hierarchyItems.Length; ++i)
            writer.Write(hierarchyItems[i]);

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