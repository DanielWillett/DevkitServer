using DevkitServer.Multiplayer.Networking;

namespace DevkitServer.Multiplayer.LevelData;
[EarlyTypeInit]
public sealed class LevelData
{
    public Folder LevelFolderContent { get; private set; }
    public byte[] Data { get; internal set; }
    public bool Compressed { get; internal set; }
    private LevelData() { }
    public static LevelData GatherLevelData()
    {
        ThreadUtil.assertIsGameThread();

        Level.save();
        Folder folder = new Folder(Level.info.path, ShouldSendFile, null);
#if DEBUG
        // Logger.LogInfo("Level data:");
        // Logger.LogInfo(folder.ToString());
#endif
        LevelData data = new LevelData
        {
            LevelFolderContent = folder
        };

        return data;
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