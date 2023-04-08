using DevkitServer.Multiplayer.Networking;

namespace DevkitServer.Multiplayer.LevelData;
[EarlyTypeInit]
public sealed class LevelData
{
    public Folder LevelFolderContent { get; private set; }
    
    private LevelData() { }
    public static LevelData GatherLevelData()
    {
        ThreadUtil.assertIsGameThread();

        Level.save();
        Folder folder = new Folder(Level.info.path, ShouldSendFile, null);
#if DEBUG
        Logger.LogInfo("Level data:");
        Logger.LogInfo(folder.ToString());
#endif
        LevelData data = new LevelData
        {
            LevelFolderContent = folder
        };

        return data;
    }
    
    private static bool ShouldSendFile(FileInfo file)
    {
        if (file.Name.Equals("Level.png", StringComparison.Ordinal)
            || file.Name.Equals("Map.png", StringComparison.Ordinal)
            || file.Name.Equals("Preview.png", StringComparison.Ordinal)
            || file.Name.Equals("Icon.png", StringComparison.Ordinal)
            || file.Name.Equals("Chart.png", StringComparison.Ordinal)
            || file.Name.Equals("Camera.dat", StringComparison.Ordinal)
            || file.Name.Equals("Height.dat", StringComparison.Ordinal)
            || file.Name.Equals("Materials.dat", StringComparison.Ordinal)
            || file.Name.Equals("Objects.dat", StringComparison.Ordinal)
            || file.Name.Equals("Spawns.dat", StringComparison.Ordinal))
            return false;

        string? dir = Path.GetFileName(file.DirectoryName);
        if (Path.GetExtension(file.Name).Equals(".png") && dir != null &&
            dir.Equals("Screenshots", StringComparison.Ordinal))
            return false;

        return true;
    }
}