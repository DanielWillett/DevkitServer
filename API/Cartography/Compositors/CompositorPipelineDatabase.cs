#if CLIENT
using Cysharp.Threading.Tasks;
using DevkitServer.Configuration;
using System.Text.Json;

namespace DevkitServer.API.Cartography.Compositors;

public static class CompositorPipelineDatabase
{
    private static FileSystemWatcher? _watcher;
    private static List<CompositorPipelineReference>? _files;
    private static bool _holdUpdate;

    public static event Action? Updated;

    /// <summary>
    /// List of all files available.
    /// </summary>
    public static IReadOnlyList<CompositorPipelineReference> Files { get; private set; } = Array.Empty<CompositorPipelineReference>();

    internal static void OnLevelStarted()
    {
        _files = new List<CompositorPipelineReference>(4);
        Files = _files.AsReadOnly();
        _holdUpdate = true;
        try
        {
            foreach (string file in Directory.EnumerateFiles(Level.info.path, "*.json", SearchOption.AllDirectories))
            {
                AnalyzeFile(file);
            }
        }
        finally
        {
            _holdUpdate = false;
        }

        _watcher?.Dispose();
        _watcher = new FileSystemWatcher(Level.info.path, "*.json")
        {
            EnableRaisingEvents = true,
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Attributes
        };

        _watcher.Created += FileCreated;
        _watcher.Deleted += FileRemoved;
        _watcher.Changed += FileChanged;

        Updated?.Invoke();
    }

    private static void FileChanged(object sender, FileSystemEventArgs e)
    {
        Logger.DevkitServer.LogConditional(nameof(CompositorPipelineDatabase), $"File changed: {e.FullPath.Format()}: {e.ChangeType}.");
        if ((e.ChangeType & (WatcherChangeTypes.Renamed | WatcherChangeTypes.Deleted)) != 0)
        {
            FileRemoved(sender, e);
        }
        else if ((e.ChangeType & WatcherChangeTypes.Changed) != 0)
        {
            UniTask.Create(async () =>
            {
                await Task.Delay(2000);
                await UniTask.SwitchToMainThread();
                if (File.Exists(e.FullPath))
                {
                    Logger.DevkitServer.LogConditional(nameof(CompositorPipelineDatabase), $"File changed: {e.FullPath.Format()}.");
                    AnalyzeFile(e.FullPath);
                }
            });
        }
    }

    internal static void Shutdown()
    {
        _watcher?.Dispose();
        _files = null;
        Files = Array.Empty<CompositorPipelineReference>();
        Updated?.Invoke();
    }

    private static void FileCreated(object sender, FileSystemEventArgs e)
    {
        UniTask.Create(async () =>
        {
            await UniTask.SwitchToMainThread();
            Logger.DevkitServer.LogConditional(nameof(CompositorPipelineDatabase), $"File created: {e.FullPath.Format()}.");
            AnalyzeFile(e.FullPath);
        });
    }

    private static void FileRemoved(object sender, FileSystemEventArgs e)
    {
        UniTask.Create(async () =>
        {
            await UniTask.SwitchToMainThread();
            int amt = _files?.RemoveAll(x => x.FileName.Equals(e.FullPath)) ?? 0;
            Logger.DevkitServer.LogConditional(nameof(CompositorPipelineDatabase), $"File deleted: {e.FullPath.Format()}, removed {amt.Format()} files.");
            if (amt > 0)
                Updated?.Invoke();
        });
    }

    private static void AnalyzeFile(string file)
    {
        if (_files == null)
            return;

        Utf8JsonReader reader = new Utf8JsonReader(FileUtil.ReadAllBytesUtf8(file), DevkitServerConfig.ReaderOptions);
        AnalyzeResult res = new AnalyzeResult { Type = (CartographyType)(-1) };
        reader.ReadTopLevelProperties(ref res, static (ref Utf8JsonReader reader, string propertyName, ref AnalyzeResult result) =>
        {
            if (propertyName.Equals("type", StringComparison.Ordinal))
            {
                if (reader.TokenType != JsonTokenType.String)
                    return false;
                
                string? type = reader.GetString();
                if (!Enum.TryParse(type, ignoreCase: true, out result.Type) || result.Type is < CartographyType.None or > CartographyType.Chart)
                {
                    if (!string.Equals(type, "GPS", StringComparison.InvariantCultureIgnoreCase))
                    {
                        result.Type = (CartographyType)(-1);
                        return false;
                    }

                    result.Type = CartographyType.Satellite;
                }
            }
            else if (propertyName.Equals("name", StringComparison.Ordinal))
            {
                if (reader.TokenType != JsonTokenType.String)
                    return false;

                string? name = reader.GetString();
                if (!string.IsNullOrWhiteSpace(name))
                    result.Name = name;
            }
            return true;
        });

        if (res.Type < 0)
        {
            if (_files.RemoveAll(x => x.FileName.Equals(file, StringComparison.Ordinal)) > 0)
            {
                Logger.DevkitServer.LogConditional(nameof(CompositorPipelineDatabase), $"Removed {file.Format()}, no longer pipeline file.");
                Updated?.Invoke();
            }
            else
            {
                Logger.DevkitServer.LogConditional(nameof(CompositorPipelineDatabase), $"Skipping unlikely pipeline file: {file.Format()}.");
            }
            return;
        }

        string name = res.Name ?? Path.GetFileNameWithoutExtension(file);
        _files.RemoveAll(x => x.FileName.Equals(file, StringComparison.Ordinal));
        _files.Add(new CompositorPipelineReference { Name = name, FileName = file, Type = res.Type });
        Logger.DevkitServer.LogConditional(nameof(CompositorPipelineDatabase), $"Adding pipeline file: {file.Format()}.");
        if (!_holdUpdate)
            Updated?.Invoke();
    }

    private struct AnalyzeResult
    {
        public CartographyType Type;
        public string? Name;
    }
}

public struct CompositorPipelineReference
{
    public string FileName;
    public string Name;
    public CartographyType Type;
}
#endif