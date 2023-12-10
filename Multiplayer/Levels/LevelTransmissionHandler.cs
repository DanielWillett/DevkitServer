using DevkitServer.API.Multiplayer;
#if CLIENT
using DevkitServer.Levels;
using DevkitServer.Multiplayer.Networking;
using SDG.Framework.Utilities;
#endif

namespace DevkitServer.Multiplayer.Levels;
internal class LevelTransmissionHandler : BaseLargeMessageTransmissionClientHandler, IDisposable
{
#if CLIENT
    private string _mapName = null!;
    private float _lastUpdate;
    private bool _updating;
    private bool _isDirty;

    protected internal override void OnStart()
    {
        _mapName = Provider.currentServerInfo?.map ?? "<unknown_map>";
        LoadingUI.SetIsDownloading(true);

        if (IsUsingPackets)
            return;
        
        TimeUtility.updated += OnUpdate;
        _updating = true;
    }

    protected internal override void OnFinished(LargeMessageTransmissionStatus status)
    {
        LoadingUI.SetIsDownloading(false);
        if (status != LargeMessageTransmissionStatus.Success)
        {
            DevkitServerUtility.CustomDisconnect(DevkitServerModule.LevelLoadingLocalization.Translate("DownloadFailed"));
            return;
        }

        string dir = EditorLevel.TempLevelPath;
        if (Directory.Exists(dir))
            Directory.Delete(dir, true);
        Directory.CreateDirectory(dir);
        Logger.LogDebug("[RECEIVE LEVEL] Reading level folder.");
        LevelData serverPendingLevelData = LevelData.Read(Transmission.Content);
        EditorLevel.ServerPendingLevelData = serverPendingLevelData;
        Folder folder = serverPendingLevelData.LevelFolderContent;
        Logger.LogDebug("[RECEIVE LEVEL] Writing level folder.");
        folder.WriteContentsToDisk(dir, true);
        LoadingUI.NotifyDownloadProgress(1f);
        Logger.LogInfo($"[RECEIVE LEVEL] Finished receiving level data ({DevkitServerUtility.FormatBytes(TotalBytes)}) for level {_mapName}.", ConsoleColor.DarkCyan);
        EditorLevel.OnLevelReady(Path.Combine(dir, _mapName));
    }

    public override bool IsDirty
    {
        get => _isDirty;
        internal set
        {
            _isDirty = value;
            OnDirtyUpdated();
        }
    }

    public void Dispose()
    {
        if (!_updating)
            return;
        
        TimeUtility.updated -= OnUpdate;
        _updating = false;
    }

    private void OnDirtyUpdated()
    {
        if (_updating && (IsUsingPackets || IsDownloaded))
        {
            TimeUtility.updated -= OnUpdate;
            _updating = false;
        }
        else if (!_updating && !IsUsingPackets)
        {
            TimeUtility.updated += OnUpdate;
            _updating = true;
        }

        if (_updating)
            return;

        if (TotalMissingPackets > 0)
        {
            Logger.LogDebug($"[{Transmission.LogSource}] ui updating 1...");
            LoadingUI.SetDownloadFileName(DevkitServerModule.LevelLoadingLocalization.Translate("RecoveringMissingPackets", _mapName, TotalMissingPackets, InitialMissingPackets));
            LoadingUI.NotifyDownloadProgress(InitialMissingPackets != 0 && InitialMissingPackets != TotalMissingPackets
                ? (float)(InitialMissingPackets - TotalMissingPackets) / InitialMissingPackets * 0.90f + 0.05f
                : 0.05f);
        }
        else if (ReceivedBytes >= TotalBytes || InitialMissingPackets > 0)
        {
            Logger.LogDebug($"[{Transmission.LogSource}] ui updating 2...");
            LoadingUI.SetDownloadFileName(DevkitServerModule.LevelLoadingLocalization.Translate("Installing", _mapName));
            LoadingUI.NotifyDownloadProgress(0.95f);
        }
        else
        {
            UpdateReceivedBytes();
        }
    }
    private void OnUpdate()
    {
        float time = CachedTime.RealtimeSinceStartup;
        if (time - _lastUpdate <= 0.25f)
            return;
         
        _lastUpdate = time;

        UpdateReceivedBytes();
    }
    private void UpdateReceivedBytes()
    {
        Logger.LogDebug($"[{Transmission.LogSource}] ui updating 3...");
        double elapsed = (DateTime.UtcNow - StartTimestamp).TotalSeconds;

        if (ReceivedBytes > 0)
        {
            double remainingTime = elapsed / ReceivedBytes * (TotalBytes - ReceivedBytes);
            string timeString = ((int)Math.Floor(remainingTime / 60)).ToString("00") + ":" + ((int)Math.Floor(remainingTime % 60)).ToString("00");
            long bytes = (long)Math.Round(ReceivedBytes / elapsed);

            LoadingUI.SetDownloadFileName(DevkitServerModule.LevelLoadingLocalization.Translate("Downloading",
                _mapName, ReceivedPackets, TotalPackets, DevkitServerUtility.FormatBytes(bytes), timeString));
        }
        else
        {
            LoadingUI.SetDownloadFileName(DevkitServerModule.LevelLoadingLocalization.Translate("CalculatingSpeed", _mapName, ReceivedPackets, TotalPackets));
        }

        LoadingUI.NotifyDownloadProgress(ReceivedBytes != 0 && TotalBytes != 0 ? (float)ReceivedBytes / TotalBytes * 0.90f + 0.05f : 0.05f);
    }
#endif
    void IDisposable.Dispose() { }
}
