using Cysharp.Threading.Tasks;
using DevkitServer.API;
using DevkitServer.API.Storage;
using DevkitServer.Configuration;
using DevkitServer.Multiplayer.Levels;
using DevkitServer.Util.Encoding;
using System.Diagnostics;

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
        bool gatherReplicatedLevelData = false,
#endif
        CancellationToken token = default)
    {
        if (!DevkitServerModule.IsMainThread)
            await UniTask.SwitchToMainThread(token);
#if DEBUG
        Stopwatch stopwatch = Stopwatch.StartNew();
#endif
        SaveLock.Wait(token); // this is not async to prevent deadlocks (makes sense ik)
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
                        if (sys is FileInfo file)
                            file.Delete();
                        else if (sys is DirectoryInfo dir)
                            dir.Delete(true);
                    }
                    Logger.DevkitServer.LogDebug(nameof(GatherLevelData), "  Done deleting existing...");
                }

                ts = new TaskCompletionSource<int>();
                copyTask = Task.Run(async () =>
                {
                    await ts.Task.ConfigureAwait(false);
                    try
                    {
                        FileUtil.CopyDirectory(oldPath, newPath, overwrite: false, skipExisting: true);
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
            }
            finally
            {
                ShouldActivateSaveLockOnLevelSave = true;
                ts?.TrySetResult(0);
            }

            if (saveToTemp)
                SetFilePath(Level.info, oldPath);

            dirtyState?.Apply();

#if SERVER
            if (gatherReplicatedLevelData)
                ReplicatedLevelDataRegistry.SaveToLevelData(data);
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
                    data.LevelFolderContent = await VirtualDirectories.CreateAsync(newPath, ShouldSendFile, null, token).ConfigureAwait(false);
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
            SaveLock.Release();
        }

#if DEBUG
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