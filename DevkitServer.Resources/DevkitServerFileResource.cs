using SDG.Framework.Modules;
using SDG.Unturned;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Version = System.Version;

namespace DevkitServer.Resources;
internal class DevkitServerFileResource : IDevkitServerResource
{
    public string ResourceName { get; set; }
    public string FilePath { get; set; }
    public Version LastUpdated { get; set; }
    public bool AddToDiscoveredAssemblies { get; set; }
    public bool Delete { get; set; }
    public Side Side { get; set; } = Side.Both;

    public DevkitServerFileResource(string resourceName, string filePath, Version lastUpdated)
    {
        ResourceName = resourceName;
        FilePath = filePath;
        LastUpdated = lastUpdated;
        AddToDiscoveredAssemblies = Path.GetExtension(filePath).Equals(".dll", StringComparison.OrdinalIgnoreCase);
    }
    public DevkitServerFileResource(string filePath, Version lastUpdated) :
        this(Path.GetFileNameWithoutExtension(filePath), filePath, lastUpdated) { }

    public byte[]? GetResourceData() => Properties.Resources.ResourceManager.GetObject(ResourceName) as byte[];
    public bool Unapply(string moduleDirectory)
    {
        string path = Path.Combine(moduleDirectory, FilePath);

        if (Delete)
            return File.Exists(path);

        try
        {
            if (Path.GetExtension(path).Equals(".dll", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    AssemblyName name = AssemblyName.GetAssemblyName(path);
                    FieldInfo? dictionaryField = typeof(ModuleHook).GetField("discoveredNameToPath", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
                    if (dictionaryField == null || dictionaryField.GetValue(null) is not Dictionary<AssemblyName, string> discoveredNameToPath)
                    {
                        CommandWindow.LogError($"[DEVKITSERVER.RESOURCES] [ERROR]  Error un-pre-loading assembly resource at \"{FilePath}\". Reflection failure.");
                        return true;
                    }

                    if (discoveredNameToPath.ContainsKey(name))
                    {
                        discoveredNameToPath.Remove(name);
                        CommandWindow.Log($"[DEVKITSERVER.RESOURCES] [INFO]   Un-pre-loaded assembly resource at \"{FilePath}\".");
                    }
                    else
                    {
                        CommandWindow.LogWarning($"[DEVKITSERVER.RESOURCES] [WARN]   Deleted assembly {name} for resource at \"{FilePath}\" is not pre-loaded.");
                    }
                }
                catch (Exception ex)
                {
                    CommandWindow.LogError($"[DEVKITSERVER.RESOURCES] [ERROR]  Error un-preloading assembly resource at \"{FilePath}\".");
                    CommandWindow.LogError(ex);
                }
            }

            File.Delete(path);

            CommandWindow.Log($"[DEVKITSERVER.RESOURCES] [INFO]   Unadded resource at \"{FilePath}\".");
            return true;
        }
        catch (Exception ex)
        {
            CommandWindow.LogError($"[DEVKITSERVER.RESOURCES] [ERROR]  Unexpected error unadding resource at \"{FilePath}\".");
            CommandWindow.LogError(ex);
            return false;
        }
    }
    public bool Apply(string moduleDirectory)
    {
        string path = Path.Combine(moduleDirectory, FilePath);

        if (Delete)
        {
            if (File.Exists(path))
            {
                try
                {
                    if (Path.GetExtension(path).Equals(".dll", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            AssemblyName name = AssemblyName.GetAssemblyName(path);
                            FieldInfo? dictionaryField = typeof(ModuleHook).GetField("discoveredNameToPath", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
                            if (dictionaryField == null || dictionaryField.GetValue(null) is not Dictionary<AssemblyName, string> discoveredNameToPath)
                            {
                                CommandWindow.LogError($"[DEVKITSERVER.RESOURCES] [ERROR]  Error un-pre-loading assembly resource at \"{FilePath}\". Reflection failure.");
                                return true;
                            }

                            if (discoveredNameToPath.ContainsKey(name))
                            {
                                discoveredNameToPath.Remove(name);
                                CommandWindow.Log($"[DEVKITSERVER.RESOURCES] [INFO]   Un-pre-loaded assembly resource at \"{FilePath}\".");
                            }
                            else
                            {
                                CommandWindow.LogWarning($"[DEVKITSERVER.RESOURCES] [WARN]   Deleted assembly {name} for resource at \"{FilePath}\" is not pre-loaded.");
                            }
                        }
                        catch (Exception ex)
                        {
                            CommandWindow.LogError($"[DEVKITSERVER.RESOURCES] [ERROR]  Error un-preloading assembly resource at \"{FilePath}\".");
                            CommandWindow.LogError(ex);
                        }
                    }

                    File.Delete(path);

                    CommandWindow.Log($"[DEVKITSERVER.RESOURCES] [INFO]   Deleted resource at \"{FilePath}\".");
                }
                catch (Exception ex)
                {
                    CommandWindow.LogError($"[DEVKITSERVER.RESOURCES] [ERROR]  Unexpected error deleting resource at \"{FilePath}\".");
                    CommandWindow.LogError(ex);
                    return false;
                }
            }
            else
            {
                CommandWindow.Log($"[DEVKITSERVER.RESOURCES] [DEBUG]  Resource already deleted: \"{FilePath}\".");
            }

            return true;
        }

        byte[]? data = GetResourceData();
        if (data == null)
        {
            CommandWindow.LogError($"[DEVKITSERVER.RESOURCES] [ERROR]  Unable to find required resource: \"{FilePath}\".");
            return false;
        }
        
        try
        {

            string? dir = Path.GetDirectoryName(path);
            if (dir != null)
                Directory.CreateDirectory(dir);

            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                stream.Write(data, 0, data.Length);
                stream.Flush(true);
            }

            try
            {
                File.SetLastAccessTimeUtc(path, DateTime.UtcNow);
            }
            catch
            {
                // ignored
            }
            try
            {
                File.SetCreationTimeUtc(path, DateTime.UtcNow);
            }
            catch
            {
                // ignored
            }
            try
            {
                File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
            }
            catch
            {
                // ignored
            }

            if (AddToDiscoveredAssemblies && Path.GetExtension(path).Equals(".dll", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    AssemblyName name = AssemblyName.GetAssemblyName(path);
                    FieldInfo? dictionaryField = typeof(ModuleHook).GetField("discoveredNameToPath", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
                    if (dictionaryField == null || dictionaryField.GetValue(null) is not Dictionary<AssemblyName, string> discoveredNameToPath)
                    {
                        CommandWindow.LogError($"[DEVKITSERVER.RESOURCES] [ERROR]  Error pre-loading assembly resource at \"{FilePath}\". Reflection failure.");
                        return true;
                    }

                    if (!discoveredNameToPath.ContainsKey(name))
                    {
                        CommandWindow.Log($"[DEVKITSERVER.RESOURCES] [INFO]   Pre-loading assembly resource at \"{FilePath}\".");
                        discoveredNameToPath.Add(name, path);
                    }
                    else
                    {
                        discoveredNameToPath[name] = path;
                        CommandWindow.LogWarning($"[DEVKITSERVER.RESOURCES] [WARN]   Replaced existing dicsovered assembly {name} for resource at \"{FilePath}\".");
                    }
                }
                catch (Exception ex)
                {
                    CommandWindow.LogError($"[DEVKITSERVER.RESOURCES] [ERROR]  Error pre-loading assembly resource at \"{FilePath}\".");
                    CommandWindow.LogError(ex);
                }
            }
            return true;
        }
        catch (Exception ex)
        {
            CommandWindow.LogError($"[DEVKITSERVER.RESOURCES] [ERROR]  Unexpected error loading resource at \"{FilePath}\".");
            CommandWindow.LogError(ex);
            return false;
        }
    }
    public bool ShouldApplyAnyways(string moduleDirectory) => Delete ^ !File.Exists(Path.Combine(moduleDirectory, FilePath));
    public override string ToString() => $"{{ {(Delete ? "Delete File" : "File")} | Last Updated in {LastUpdated} | '{FilePath}' }}";
}

internal class DevkitServerDirectoryResource : IDevkitServerResource
{
    public string DirectoryName { get; set; }
    public Version LastUpdated { get; set; }
    public bool Delete { get; set; }
    public Side Side { get; set; } = Side.Both;

    public DevkitServerDirectoryResource(string directoryName, Version lastUpdated)
    {
        DirectoryName = directoryName;
        LastUpdated = lastUpdated;
    }
    public bool Unapply(string moduleDirectory)
    {
        string path = Path.Combine(moduleDirectory, DirectoryName);

        if (Delete)
            return Directory.Exists(path);

        try
        {
            Directory.Delete(path, Directory.GetFiles(path, "*.dll", SearchOption.AllDirectories).Length == 0);
            return true;
        }
        catch (IOException) // dll's need to be unloaded
        {
            CommandWindow.LogError($"[DEVKITSERVER.RESOURCES] [ERROR]  Error deleting resource at \"{DirectoryName}\". Directory is not empty.");
        }
        catch (Exception ex)
        {
            CommandWindow.LogError($"[DEVKITSERVER.RESOURCES] [ERROR]  Unexpected error deleting resource at \"{DirectoryName}\".");
            CommandWindow.LogError(ex);
        }

        return false;
    }
    public bool Apply(string moduleDirectory)
    {
        string path = Path.Combine(moduleDirectory, DirectoryName);

        if (Directory.Exists(path))
        {
            if (!Delete)
            {
                CommandWindow.Log($"[DEVKITSERVER.RESOURCES] [DEBUG]  Required directory: \"{DirectoryName}\" already exists.");
                return false;
            }

            if (!Directory.Exists(path))
            {
                CommandWindow.Log($"[DEVKITSERVER.RESOURCES] [DEBUG]  Resource already deleted: \"{DirectoryName}\".");
            }
            else
            {
                try
                {
                    Directory.Delete(path, Directory.GetFiles(path, "*.dll", SearchOption.AllDirectories).Length == 0);
                    return true;
                }
                catch (IOException) // dll's need to be unloaded
                {
                    CommandWindow.LogError($"[DEVKITSERVER.RESOURCES] [ERROR]  Error deleting resource at \"{DirectoryName}\". Directory is not empty.");
                }
                catch (Exception ex)
                {
                    CommandWindow.LogError($"[DEVKITSERVER.RESOURCES] [ERROR]  Unexpected error deleting resource at \"{DirectoryName}\".");
                    CommandWindow.LogError(ex);
                }

                return false;
            }
        }

        if (Delete)
            return true;
        try
        {
            Directory.CreateDirectory(path);

            try
            {
                Directory.SetLastAccessTimeUtc(path, DateTime.UtcNow);
            }
            catch
            {
                // ignored
            }
            try
            {
                Directory.SetCreationTimeUtc(path, DateTime.UtcNow);
            }
            catch
            {
                // ignored
            }
            try
            {
                Directory.SetLastWriteTimeUtc(path, DateTime.UtcNow);
            }
            catch
            {
                // ignored
            }
            return true;
        }
        catch (Exception ex)
        {
            CommandWindow.LogError("[DEVKITSERVER.RESOURCES] [ERROR]  Unexpected error.");
            CommandWindow.LogError(ex);
            return false;
        }
    }

    public bool ShouldApplyAnyways(string moduleDirectory) => Delete ^ !Directory.Exists(Path.Combine(moduleDirectory, DirectoryName));
    public override string ToString() => $"{{ Directory | Last Updated in {LastUpdated} | '{DirectoryName}' }}";
}

internal interface IDevkitServerResource
{
    Version LastUpdated { get; }
    Side Side { get; }
    bool Unapply(string moduleDirectory);
    bool Apply(string moduleDirectory);
    bool ShouldApplyAnyways(string moduleDirectory);
}