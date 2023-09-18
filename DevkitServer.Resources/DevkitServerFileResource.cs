using System;
using System.IO;
using SDG.Unturned;
using Version = System.Version;

namespace DevkitServer.Resources;
internal class DevkitServerFileResource : IDevkitServerResource
{
    public string ResourceName { get; set; }
    public string FilePath { get; set; }
    public Version LastUpdated { get; set; }

    public DevkitServerFileResource(string resourceName, string filePath, Version lastUpdated)
    {
        ResourceName = resourceName;
        FilePath = filePath;
        LastUpdated = lastUpdated;
    }
    public DevkitServerFileResource(string filePath, Version lastUpdated) :
        this(Path.GetFileNameWithoutExtension(filePath), filePath, lastUpdated) { }

    public byte[]? GetResourceData() => Properties.Resources.ResourceManager.GetObject(ResourceName) as byte[];
    public void Apply(string moduleDirectory)
    {
        byte[]? data = GetResourceData();
        if (data == null)
        {
            CommandWindow.LogError($"[DEVKITSERVER.RESOURCES] [ERROR] Unable to find required resource: \"{FilePath}\".");
            return;
        }

        CommandWindow.Log($"[DEVKITSERVER.RESOURCES] [INFO]  Writing required resource: \"{FilePath}\"...");

        try
        {
            string path = Path.Combine(moduleDirectory, FilePath);

            string? dir = Path.GetDirectoryName(path);
            if (dir != null)
                Directory.CreateDirectory(dir);

            using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            stream.Write(data, 0, data.Length);
            stream.Flush(true);
            CommandWindow.Log("[DEVKITSERVER.RESOURCES] [INFO]    Done.");
        }
        catch (Exception ex)
        {
            CommandWindow.LogError("[DEVKITSERVER.RESOURCES] [ERROR]   Unexpected error.");
            CommandWindow.LogError(ex);
        }
    }
}

internal class DevkitServerDirectoryResource : IDevkitServerResource
{
    public string DirectoryName { get; set; }
    public Version LastUpdated { get; set; }

    public DevkitServerDirectoryResource(string directoryName, Version lastUpdated)
    {
        DirectoryName = directoryName;
        LastUpdated = lastUpdated;
    }
    public void Apply(string moduleDirectory)
    {
        string path = Path.Combine(moduleDirectory, DirectoryName);

        if (Directory.Exists(path))
        {
            CommandWindow.Log($"[DEVKITSERVER.RESOURCES] [DEBUG] Required directory: \"{DirectoryName}\" already exists.");
            return;
        }

        CommandWindow.Log($"[DEVKITSERVER.RESOURCES] [INFO]  Creating required directory: \"{DirectoryName}\"...");

        try
        {
            Directory.CreateDirectory(path);
            CommandWindow.Log("[DEVKITSERVER.RESOURCES] [INFO]    Done.");
        }
        catch (Exception ex)
        {
            CommandWindow.LogError("[DEVKITSERVER.RESOURCES] [ERROR]   Unexpected error.");
            CommandWindow.LogError(ex);
        }
    }
}

internal interface IDevkitServerResource
{
    Version LastUpdated { get; }
    void Apply(string moduleDirectory);
}