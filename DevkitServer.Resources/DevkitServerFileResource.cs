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
    public bool Apply(string moduleDirectory)
    {
        byte[]? data = GetResourceData();
        if (data == null)
        {
            CommandWindow.LogError($"[DEVKITSERVER.RESOURCES] [ERROR] Unable to find required resource: \"{FilePath}\".");
            return false;
        }
        
        try
        {
            string path = Path.Combine(moduleDirectory, FilePath);

            string? dir = Path.GetDirectoryName(path);
            if (dir != null)
                Directory.CreateDirectory(dir);

            using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            stream.Write(data, 0, data.Length);
            stream.Flush(true);
            return true;
        }
        catch (Exception ex)
        {
            CommandWindow.LogError("[DEVKITSERVER.RESOURCES] [ERROR]   Unexpected error.");
            CommandWindow.LogError(ex);
            return false;
        }
    }
    public bool ShouldApplyAnyways(string moduleDirectory) => !File.Exists(Path.Combine(moduleDirectory, FilePath));
    public override string ToString() => $"{{ File | Last Updated in {LastUpdated} | '{FilePath}' }}";
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
    public bool Apply(string moduleDirectory)
    {
        string path = Path.Combine(moduleDirectory, DirectoryName);

        if (Directory.Exists(path))
        {
            CommandWindow.Log($"[DEVKITSERVER.RESOURCES] [DEBUG] Required directory: \"{DirectoryName}\" already exists.");
            return false;
        }
        
        try
        {
            Directory.CreateDirectory(path);
            return true;
        }
        catch (Exception ex)
        {
            CommandWindow.LogError("[DEVKITSERVER.RESOURCES] [ERROR]   Unexpected error.");
            CommandWindow.LogError(ex);
            return false;
        }
    }

    public bool ShouldApplyAnyways(string moduleDirectory) => !Directory.Exists(Path.Combine(moduleDirectory, DirectoryName));
    public override string ToString() => $"{{ Directory | Last Updated in {LastUpdated} | '{DirectoryName}' }}";
}

internal interface IDevkitServerResource
{
    Version LastUpdated { get; }
    bool Apply(string moduleDirectory);
    bool ShouldApplyAnyways(string moduleDirectory);
}