using Cysharp.Threading.Tasks;
using DanielWillett.ReflectionTools;
using DevkitServer.Core.Cartography.Jobs;
using SDG.Framework.Modules;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using Unity.Collections;
using Unity.Jobs;
#if CLIENT
using DevkitServer.Multiplayer.Networking;
#endif
#if SERVER
using DevkitServer.Configuration;
#endif

namespace DevkitServer.Util;

/// <summary>
/// Utilities for working with files and paths.
/// </summary>
public static class FileUtil
{
    /// <summary>
    /// Returns the desktop on machines that support it, otherwise returns <see cref="Environment.CurrentDirectory"/>.
    /// </summary>
    public static string DesktopOrCurrentDir
    {
        get
        {
            try
            {
                return Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            }
            catch
            {
                return Environment.CurrentDirectory;
            }
        }
    }
#if SERVER
    [Pure]
    public static string GetUserSavedataLocation(ulong s64, string path, int characterId = 0)
    {
        string basePath;
        if (!string.IsNullOrEmpty(DevkitServerConfig.Config.UserSavedataLocationOverride))
        {
            basePath = DevkitServerConfig.Config.UserSavedataLocationOverride!;
            if (!Path.IsPathRooted(basePath))
                basePath = Path.Combine(ReadWrite.PATH, basePath);
        }
        else if (PlayerSavedata.hasSync)
            basePath = Path.Combine(ReadWrite.PATH, "Sync");
        else
            basePath = Path.Combine(ReadWrite.PATH, ServerSavedata.directoryName, Provider.serverID, "Players");

        // intentionally using cultured toString here since the base game also does
        return Path.Combine(basePath, s64 + "_" + characterId, Level.info.name, path);
    }
#endif
    /// <summary>
    /// Tries to create a directory.
    /// </summary>
    /// <remarks>Will not throw an exception. Use <see cref="Directory.CreateDirectory(string)"/> if you want an exception.</remarks>
    /// <param name="relative">If the path is relative to <see cref="ReadWrite.PATH"/>.</param>
    /// <param name="path">Relative or absolute path to a directory.</param>
    /// <returns><see langword="true"/> if the directory is created or already existed, otherwise false.</returns>
    public static bool CheckDirectory(bool relative, string path) => CheckDirectory(relative, false, path, null);
    internal static bool CheckDirectory(bool relative, bool fault, string path, MemberInfo? member)
    {
        if (path == null)
            return false;
        try
        {
            if (relative)
                path = Path.Combine(UnturnedPaths.RootDirectory.FullName, path);
            if (Directory.Exists(path))
            {
                if (member == null)
                    Logger.DevkitServer.LogDebug("CHECK DIR", $"Directory checked: {path.Format(false)}.");
                else
                    Logger.DevkitServer.LogDebug("CHECK DIR", $"Directory checked: {path.Format(false)} from {member.Format()}.");
                return true;
            }

            Directory.CreateDirectory(path);
            if (member == null)
                Logger.DevkitServer.LogInfo("CHECK DIR", $"Directory created: {path.Format(false)}.");
            else
                Logger.DevkitServer.LogInfo("CHECK DIR", $"Directory created: {path.Format(false)} from {member.Format()}.");
            return true;
        }
        catch (Exception ex)
        {
            if (member == null)
                Logger.DevkitServer.LogError("CHECK DIR", ex, $"Unable to create directory: {path.Format(false)}.");
            else
                Logger.DevkitServer.LogError("CHECK DIR", ex, $"Unable to create directory: {path.Format(false)} from {member.Format()}.");
            if (fault)
                DevkitServerModule.Fault();
            return false;
        }
    }

    /// <summary>
    /// Gets the size in bytes of a directory and all it's subfiles recursively.
    /// </summary>
    [Pure]
    public static long GetDirectorySize(string directory)
    {
        DirectoryInfo dir = new DirectoryInfo(directory);
        return GetDirectorySize(dir);
    }

    /// <summary>
    /// Gets the size in bytes of a directory and all it's subfiles recursively.
    /// </summary>
    [Pure]
    public static long GetDirectorySize(DirectoryInfo directory)
    {
        if (!directory.Exists)
            return 0L;
        FileSystemInfo[] files = directory.GetFileSystemInfos("*", SearchOption.TopDirectoryOnly);
        long ttl = 0;
        for (int i = 0; i < files.Length; ++i)
        {
            switch (files[i])
            {
                case FileInfo f:
                    ttl += f.Length;
                    break;
                case DirectoryInfo d:
                    ttl += GetDirectorySize(d);
                    break;
            }
        }

        return ttl;
    }

    /// <summary>
    /// Creates a copy or moves a file, for example, 'OriginalName' to 'OriginalName Backup', and optionally assigns a number if there are duplicate files.
    /// </summary>
    /// <param name="originalFile">Path to the file to copy or move.</param>
    /// <param name="overwrite">Allows the copy or move operation to just overwrite existing files instead of incrementing a number.</param>
    /// <returns>The path to the newly created or moved file.</returns>
    public static string BackupFile(string originalFile, bool moveInsteadOfCopy, bool overwrite = true)
    {
        string ext = Path.GetExtension(originalFile);
        string? dir = Path.GetDirectoryName(originalFile);
        string fn = Path.GetFileNameWithoutExtension(originalFile) + " Backup";
        if (dir != null)
            fn = Path.Combine(dir, fn);
        if (File.Exists(fn + ext) && !overwrite)
        {
            int num = 0;
            fn += " ";
            while (File.Exists(fn + num.ToString(CultureInfo.InvariantCulture) + ext))
                ++num;
            fn += num.ToString(CultureInfo.InvariantCulture);
        }

        DateTime? lastModified = null;
        try
        {
            lastModified = File.GetLastWriteTimeUtc(originalFile);
        }
        catch
        {
            // ignored
        }

        fn += ext;
        if (moveInsteadOfCopy)
        {
            if (overwrite && File.Exists(fn))
                File.Delete(fn);

            File.Move(originalFile, fn);
        }
        else
        {
            File.Copy(originalFile, fn, overwrite);
        }

        try
        {
            File.SetCreationTimeUtc(fn, DateTime.UtcNow);

            if (lastModified.HasValue)
                File.SetLastWriteTimeUtc(fn, lastModified.Value);
        }
        catch
        {
            // ignored
        }

        return fn;
    }

    /// <summary>
    /// Checks to see if <paramref name="longerPath"/> is a child folder or file of the directory <paramref name="shorterPath"/>.
    /// </summary>
    [Pure]
    public static bool IsChildOf(string? shorterPath, string longerPath, bool includeSubDirectories = true)
    {
        if (string.IsNullOrEmpty(shorterPath))
            return true;
        if (string.IsNullOrEmpty(longerPath))
            return false;
        DirectoryInfo parent = new DirectoryInfo(shorterPath);
        DirectoryInfo child = new DirectoryInfo(longerPath);
        return IsChildOf(parent, child, includeSubDirectories);
    }

    /// <summary>
    /// Checks to see if <paramref name="longerPath"/> is a child folder or file of the directory <paramref name="shorterPath"/>.
    /// </summary>
    [Pure]
    public static bool IsChildOf(DirectoryInfo shorterPath, DirectoryInfo longerPath, bool includeSubDirectories = true)
    {
        string shortFullname = shorterPath.FullName;
        if (!includeSubDirectories)
            return longerPath.Parent != null && longerPath.Parent.FullName.Equals(shortFullname, StringComparison.Ordinal);
        while (longerPath.Parent != null)
        {
            if (longerPath.Parent.FullName.Equals(shortFullname, StringComparison.Ordinal))
                return true;
            longerPath = longerPath.Parent;
        }

        return false;
    }

    /// <summary>
    /// Recursively copy a directory from <paramref name="source"/> to <paramref name="destination"/>.
    /// </summary>
    /// <exception cref="AggregateException">Errors reading or writing files.</exception>
    public static void CopyDirectory(string source, string destination, bool overwrite = true, bool skipExisting = false, Predicate<FileInfo>? shouldInclude = null, Predicate<DirectoryInfo>? shouldIncludeDirectory = null)
    {
        DirectoryInfo sourceInfo = new DirectoryInfo(source);
        if (!sourceInfo.Exists)
            return;
        DirectoryInfo dstInfo = new DirectoryInfo(destination);
        if (!dstInfo.Exists)
            dstInfo.Create();

        List<Exception>? exceptions = null;
        try
        {
            foreach (FileSystemInfo info in sourceInfo.GetFileSystemInfos("*", SearchOption.AllDirectories))
            {
                if (info is FileInfo file)
                {
                    try
                    {
                        if (shouldInclude != null && !shouldInclude(file))
                            continue;
                        string path = Path.Combine(dstInfo.FullName, Path.GetRelativePath(sourceInfo.FullName, file.FullName));
                        if (!overwrite && skipExisting && File.Exists(path))
                            continue;
                        string? dir = Path.GetDirectoryName(path);
                        if (dir != null)
                            Directory.CreateDirectory(dir);
                        file.CopyTo(path, overwrite);
                    }
                    catch (Exception ex)
                    {
                        (exceptions ??= new List<Exception>(1)).Add(ex);
                    }
                }
                else if (info is DirectoryInfo dir)
                {
                    try
                    {
                        if (shouldIncludeDirectory != null && !shouldIncludeDirectory(dir))
                            continue;
                        string path = Path.Combine(dstInfo.FullName, Path.GetRelativePath(sourceInfo.FullName, dir.FullName));
                        Directory.CreateDirectory(path);
                    }
                    catch (Exception ex)
                    {
                        (exceptions ??= new List<Exception>(1)).Add(ex);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            throw new AggregateException(ex);
        }

        if (exceptions is { Count: > 0 })
        {
            throw new AggregateException(exceptions);
        }
    }

    /// <summary>
    /// Changes directory separators to back slashes if they aren't already.
    /// </summary>
    [Pure]
    public static string FormatUniversalPath(string path) => Path.DirectorySeparatorChar == '\\' ? path : path.Replace(Path.DirectorySeparatorChar, '\\');

    /// <summary>
    /// Changes directory separators to forward slashes if they aren't supposed to be back slashes.
    /// </summary>
    [Pure]
    public static string UnformatUniversalPath(string path) => Path.DirectorySeparatorChar == '\\' ? path : path.Replace('\\', Path.DirectorySeparatorChar);

    /// <summary>
    /// Get the full path to a <see cref="ModuleAssembly"/> from a module.
    /// </summary>
    [Pure]
    public static string GetModuleAssemblyPath(ModuleConfig config, ModuleAssembly assembly)
    {
        string path = Path.GetFullPath(config.DirectoryPath);
        return string.IsNullOrEmpty(assembly.Path) ? path : Path.Combine(path, assembly.Path[0] == '/' ? assembly.Path.Substring(1) : assembly.Path);
    }

    [Pure]
    public static string GetServerUniqueFileName(bool includeMap = false, bool clientIncludesCharacter = false)
    {
#if CLIENT
        if (NetFactory.GetPlayerTransportConnection() != null)
        {
            string mapCharPart = (includeMap ? "_" + Provider.map : string.Empty) + (clientIncludesCharacter ? "_" + Characters.selected.ToString(CultureInfo.InvariantCulture) : string.Empty);

            if (Provider.CurrentServerConnectParameters.steamId.IsValid())
                return Provider.CurrentServerConnectParameters.steamId.m_SteamID.ToString("D17", CultureInfo.InvariantCulture) + mapCharPart;

            return Provider.CurrentServerConnectParameters.address + "_" + Provider.CurrentServerConnectParameters.connectionPort.ToString(CultureInfo.InvariantCulture) + mapCharPart;
        }
#endif

        return Provider.serverID + (includeMap ? "_" + Provider.map : string.Empty);
    }

    [Pure]
    public static bool TryGetFreeSpaceOnDrive(string folderOnDrive, out long bytesFree)
    {
        try
        {
            bytesFree = new DriveInfo(folderOnDrive).AvailableFreeSpace;
            return true;
        }
        catch (ArgumentException ex)
        {
            Logger.DevkitServer.LogWarning(nameof(TryGetFreeSpaceOnDrive), $"Failed to get drive info ({ex.Message}).");
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogWarning(nameof(TryGetFreeSpaceOnDrive), ex, "Failed to get drive info.");
        }

        bytesFree = 0;
        return false;
    }

    /// <summary>
    /// Asynchronously encodes an image and writes it to disk in either PNG or JPEG, depending on the extension of <paramref name="outputFile"/>.
    /// </summary>
    public static async UniTask EncodeAndSaveTexture(Texture2D texture, string outputFile, int jpegQuality = 85, CancellationToken token = default)
    {
        await UniTask.SwitchToMainThread(token);

#if DEBUG
        Stopwatch sw = Stopwatch.StartNew();
#endif
        byte[] rawData = texture.GetRawTextureData();
        string extension = Path.GetExtension(outputFile);
        EncodeImageJob encodeJob = new EncodeImageJob
        {
            // using Allocator.Persistant because this job takes more than 4 frames sometimes (TempJob throws a warning).
            InputTexture = new NativeArray<byte>(rawData, Allocator.Persistent),
            GraphicsFormat = texture.graphicsFormat,
            OutputPNG = new NativeArray<byte>(rawData.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory),
            Size = new Vector2Int(texture.width, texture.height),
            OutputSize = new NativeArray<int>(1, Allocator.Persistent, NativeArrayOptions.UninitializedMemory),
            UseJpeg = extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                      || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase),
            JpegQuality = jpegQuality
        };
        if (!encodeJob.UseJpeg && !extension.Equals(".png", StringComparison.OrdinalIgnoreCase))
            outputFile += ".png";

        JobHandle handle = encodeJob.Schedule();
#if DEBUG
        sw.Stop();
        Logger.DevkitServer.LogDebug(nameof(EncodeImageJob), $"Setup encode: {sw.GetElapsedMilliseconds().Format("0.##")} ms.");

        sw.Restart();
#endif
        await handle;

#if DEBUG
        sw.Stop();
        Logger.DevkitServer.LogDebug(nameof(EncodeImageJob), $"Await encode: {sw.GetElapsedMilliseconds().Format("0.##")} ms.");

        sw.Restart();
#endif
        byte[] pngData = new byte[encodeJob.OutputSize[0]];
        NativeArray<byte>.Copy(encodeJob.OutputPNG, pngData, pngData.Length);
        encodeJob.InputTexture.Dispose();
        encodeJob.OutputPNG.Dispose();
        encodeJob.OutputSize.Dispose();
#if DEBUG
        sw.Stop();
        Logger.DevkitServer.LogDebug(nameof(EncodeImageJob), $"Cleanup encode: {sw.GetElapsedMilliseconds().Format("0.##")} ms.");

        sw.Restart();
#endif
        string? dir = Path.GetDirectoryName(outputFile);

        if (dir != null)
            Directory.CreateDirectory(dir);

        await File.WriteAllBytesAsync(outputFile, pngData, token).ConfigureAwait(false);
#if DEBUG
        sw.Stop();
        Logger.DevkitServer.LogDebug(nameof(EncodeImageJob), $"Write: {sw.GetElapsedMilliseconds().Format("0.##")} ms.");
#endif
        await UniTask.SwitchToMainThread(token);
    }
}
