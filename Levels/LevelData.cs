﻿using Cysharp.Threading.Tasks;
using DanielWillett.ReflectionTools;
using DanielWillett.SpeedBytes;
using DevkitServer.API;
using DevkitServer.API.Storage;
using DevkitServer.Configuration;
using DevkitServer.Multiplayer.Levels;
using System.Diagnostics;
#if CLIENT
using DevkitServer.Core.UI.Extensions;
#endif

namespace DevkitServer.Levels;

/// <summary>
/// In-memory representation of a level save. Can also include replicated level data from <see cref="ReplicatedLevelDataRegistry"/>.
/// </summary>
[EarlyTypeInit]
public sealed class LevelData
{
    private static readonly uint DataVersion = 0;
    internal static readonly SemaphoreSlim SaveLock = new SemaphoreSlim(1, 1);
    internal static bool ShouldActivateSaveLockOnLevelSave = true;

    private static readonly InstanceSetter<LevelInfo, string> SetFilePath = Accessor.GenerateInstancePropertySetter<LevelInfo, string>("path", throwOnError: true)!;

    /// <summary>
    /// If <see cref="LevelData"/> is currently being gathered. Part of this gathering will happen on another thread.
    /// </summary>
    public static bool IsGathering { get; private set; }

    private string? _lclPath;
    public uint Version { get; private set; }
    public ArraySegment<byte> Data { get; internal set; }
    internal List<object>? ReplicatedLevelData { get; set; }
    public VirtualDirectoryRoot LevelFolderContent { get; private set; } = null!;
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

    /// <summary>
    /// Safely read save and read the level folder into memory.
    /// </summary>
    /// <param name="saveToTemp">Should the world be resaved elsewhere instead of saving to the main save directory.</param>
    public static async Task<LevelData> GatherLevelData(bool saveToTemp,
#if SERVER
        CSteamID replicatedLevelDataUser = default,
#endif
        CancellationToken token = default)
    {
        await UniTask.SwitchToMainThread(token);

#if DEBUG
        Stopwatch stopwatch = Stopwatch.StartNew();
#endif

        SaveLock.Wait(token); // this is not async to prevent deadlocks (makes sense ik)
        IsGathering = true;
#if CLIENT
        EditorPauseUIExtension.OnLevelDataGatherStateUpdated();
#endif
        LevelData data;
        try
        {
            DirtyManagerState? dirtyState = null;
            string newPath, oldPath = newPath = Level.info.path;
            Task? copyTask = null;
            TaskCompletionSource<int>? ts = null;
            if (saveToTemp)
            {
                newPath = Path.Combine(DevkitServerConfig.TempFolder, "Level");
                dirtyState = DirtyManagerState.Create();
                SetFilePath(Level.info, newPath);
                if (Directory.Exists(newPath))
                {
                    Logger.DevkitServer.LogDebug(nameof(GatherLevelData), "Deleting existing...");
                    foreach (FileSystemInfo sys in new DirectoryInfo(newPath).EnumerateFileSystemInfos("*", SearchOption.TopDirectoryOnly))
                    {
                        try
                        {
                            if (sys is FileInfo file)
                                file.Delete();
                            else if (sys is DirectoryInfo dir)
                                dir.Delete(true);
                        }
                        catch (Exception ex)
                        {
                            Logger.DevkitServer.LogWarning(nameof(GatherLevelData), ex, $"  Error deleting {(sys is FileInfo ? "file" : "directory")} {sys.FullName.Format()}.");
                        }
                    }

                    Logger.DevkitServer.LogDebug(nameof(GatherLevelData), "  Done deleting existing...");
                }

                ts = new TaskCompletionSource<int>();
                copyTask = Task.Run(async () =>
                {
                    await ts.Task.ConfigureAwait(false);
                    try
                    {
                        DirectoryInfo gitFolder = new DirectoryInfo(Path.Combine(oldPath, ".git"));
                        string gitAttributes = Path.Combine(oldPath, ".gitattributes");
                        string gitIgnore = Path.Combine(oldPath, ".gitignore");
                        FileUtil.CopyDirectory(oldPath, newPath, overwrite: false, skipExisting: true, shouldInclude:
                            file =>
                            {
                                if (file.FullName.Equals(gitAttributes, StringComparison.Ordinal) ||
                                    file.FullName.Equals(gitIgnore,     StringComparison.Ordinal))
                                {
                                    return false;
                                }

                                DirectoryInfo? dir = file.Directory;
                                return !(dir != null && (string.Equals(dir.FullName, gitFolder.FullName, StringComparison.Ordinal) || FileUtil.IsChildOf(gitFolder, dir)));
                            }, shouldIncludeDirectory:
                            dir => !string.Equals(dir.FullName, gitFolder.FullName, StringComparison.Ordinal) && !FileUtil.IsChildOf(gitFolder, dir));
                    }
                    catch (Exception ex)
                    {
                        Logger.DevkitServer.LogError(nameof(GatherLevelData), ex, $"Error copying directory to avoid saving ({oldPath.Format()} -> {newPath.Format()}).");
                    }

                }, token);
            }

            data = new LevelData
            {
                _lclPath = newPath
            };

            ShouldActivateSaveLockOnLevelSave = false;
            try
            {
                Level.save();

                ShouldActivateSaveLockOnLevelSave = true;
                ts?.TrySetResult(0);

                if (saveToTemp)
                    SetFilePath(Level.info, oldPath);

                dirtyState?.Apply();
            }
            catch (Exception ex)
            {
                // not using finally in case log throws error
                ShouldActivateSaveLockOnLevelSave = true;
                ts?.TrySetResult(0);

                if (saveToTemp)
                    SetFilePath(Level.info, oldPath);

                dirtyState?.Apply();

                Logger.DevkitServer.LogError(nameof(GatherLevelData), ex, $"Error saving level {Level.info.getLocalizedName().Format(false)} to {newPath.Format()}.");
            }

#if SERVER
            if (replicatedLevelDataUser.GetEAccountType() == EAccountType.k_EAccountTypeIndividual)
                ReplicatedLevelDataRegistry.SaveToLevelData(data, replicatedLevelDataUser);
#endif
            if (copyTask is { IsCompleted: false })
            {
                Logger.DevkitServer.LogDebug(nameof(GatherLevelData), "  Waiting for copy...");
                await copyTask.ConfigureAwait(false);
            }

            Logger.DevkitServer.LogDebug(nameof(GatherLevelData), "Reading...");
            await Task.Run(async () =>
            {
                try
                {
                    data.LevelFolderContent = await VirtualDirectories.CreateAsync(newPath, ShouldSendFile, ShouldSendDirectory, token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Logger.DevkitServer.LogError(nameof(GatherLevelData), ex, "Error trying to read the level into memory.");
                }
            }, token).ConfigureAwait(false);
            Logger.DevkitServer.LogDebug(nameof(GatherLevelData), "  Done reading.");
        }
        finally
        {
            IsGathering = false;
#if CLIENT
            DevkitServerUtility.QueueOnMainThread(EditorPauseUIExtension.OnLevelDataGatherStateUpdated);
#endif
            SaveLock.Release();
        }

#if DEBUG
        stopwatch.Stop();
        Logger.DevkitServer.LogDebug(nameof(GatherLevelData), $"GatherLevelData took {stopwatch.GetElapsedMilliseconds().Format("F2")} ms.");
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

        data.LevelFolderContent = VirtualDirectories.Read(_levelReader);

        reader.LoadNew(ArraySegment<byte>.Empty);
        return data;
    }
    public void WriteToData(bool flush)
    {
        ThreadUtil.assertIsGameThread();

        ByteWriter writer = _levelWriter ??= new ByteWriter(134217728); // 128 MiB
        writer.Write(DataVersion);
        Version = DataVersion;

        ReplicatedLevelDataRegistry.Write(this, writer);

        LevelFolderContent.Write(writer);

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

    internal static bool ShouldSendFile(FileInfo file, DirectoryInfo rootDir)
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

        if (file.Name.Equals(".gitignore", StringComparison.Ordinal) ||
            file.Name.Equals(".gitattributes", StringComparison.Ordinal))
        {
            return file.Directory == null || !rootDir.FullName.Equals(file.Directory.FullName);
        }

        // ignore .git folder
        if (file.FullName.Contains(".git", StringComparison.OrdinalIgnoreCase))
        {
            DirectoryInfo gitFolder = new DirectoryInfo(Path.Combine(rootDir.FullName, ".git"));
            DirectoryInfo? fileDir = file.Directory;
            if (fileDir != null && (gitFolder.FullName.Equals(fileDir.FullName) || FileUtil.IsChildOf(gitFolder, fileDir)))
                return false;
        }

        return true;
    }

    internal static bool ShouldSendDirectory(DirectoryInfo directory, DirectoryInfo rootDir)
    {
        // ignore .git folder
        if (!directory.FullName.Contains(".git", StringComparison.OrdinalIgnoreCase))
            return true;

        DirectoryInfo gitFolder = new DirectoryInfo(Path.Combine(rootDir.FullName, ".git"));
        return !gitFolder.FullName.Equals(directory.FullName) && !FileUtil.IsChildOf(gitFolder, directory);
    }
}