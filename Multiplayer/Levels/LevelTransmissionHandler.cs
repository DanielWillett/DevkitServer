using DevkitServer.API.Multiplayer;
#if CLIENT
using System.Reflection;
using Cysharp.Threading.Tasks;
using DevkitServer.API.Storage;
using DevkitServer.API.UI;
using DevkitServer.Levels;
using SDG.Framework.Utilities;
#endif

namespace DevkitServer.Multiplayer.Levels;
internal class LevelTransmissionHandler : BaseLargeMessageTransmissionClientHandler, IDisposable
{
#if CLIENT
    private string _mapName = null!;
    private float _lastUpdate;
    private volatile int _updating;
    private bool _isDirty;
    private bool _lastIsCancellable;

    protected internal override void OnStart()
    {
        _mapName = Provider.CurrentServerAdvertisement?.map ?? Provider.map ?? "Map";
        LoadingUI.SetIsDownloading(true);

        if (IsUsingPackets && typeof(LoadingUI).GetField("tipLabel", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)?.GetValue(null) is ISleekLabel tipLabel)
        {
            tipLabel.Text = DevkitServerModule.MainLocalization.Translate("HighSpeedTip");
        }

        if (IsUsingPackets)
            return;

        if (Interlocked.Exchange(ref _updating, 1) == 0)
            TimeUtility.updated += OnUpdate;
    }

    protected internal override void OnFinished(LargeMessageTransmissionStatus status)
    {
        LoadingUI.SetIsDownloading(false);
        if (status != LargeMessageTransmissionStatus.Success)
        {
            string disconnectMessage = DevkitServerModule.LevelLoadingLocalization.Translate(status == LargeMessageTransmissionStatus.Cancelled ? "DownloadCancelled" : "DownloadFailed");
            DevkitServerUtility.CustomDisconnect(disconnectMessage);
            return;
        }

        string dir = EditorLevel.TempLevelPath;
        if (Directory.Exists(dir))
            Directory.Delete(dir, true);
        Directory.CreateDirectory(dir);
        Logger.DevkitServer.LogDebug("RECEIVE LEVEL", "Reading level folder.");
        LevelData serverPendingLevelData = LevelData.Read(Transmission.Content);
        EditorLevel.ServerPendingLevelData = serverPendingLevelData;
        VirtualDirectoryRoot folder = serverPendingLevelData.LevelFolderContent;
        Logger.DevkitServer.LogDebug("RECEIVE LEVEL", "Writing level folder.");
        LoadingUI.NotifyDownloadProgress(0.95f);

        UniTask.Create(async () =>
        {
            await folder.SaveAsync(dir, true);

            await UniTask.SwitchToMainThread();

            LoadingUI.NotifyDownloadProgress(1f);
            Logger.DevkitServer.LogInfo("RECEIVE LEVEL", $"Finished receiving level data ({FormattingUtil.FormatCapacity(TotalBytes, colorize: true)}) for level {_mapName.Colorize(DevkitServerModule.UnturnedColor)}.", ConsoleColor.DarkCyan);
            
            EditorLevel.OnLevelReady(dir);
        });
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
        int old = Interlocked.Exchange(ref _updating, 0);
        if (old != 0)
            TimeUtility.updated -= OnUpdate;
    }

    private void OnDirtyUpdated()
    {
        bool canCancel = Transmission.CanCancel;
        if (canCancel != _lastIsCancellable)
        {
            _lastIsCancellable = canCancel;

            if (DevkitServerModule.IsMainThread)
                DevkitServerUITools.SetLoadingCancelVisibility(canCancel);
            else
                DevkitServerUtility.QueueOnMainThread(() => DevkitServerUITools.SetLoadingCancelVisibility(canCancel));
        }

        int upd;
        if (IsUsingPackets || IsDownloaded)
        {
            int old = Interlocked.Exchange(ref _updating, 0);
            if (old != 0)
                TimeUtility.updated -= OnUpdate;
            upd = 0;
        }
        else if (!IsUsingPackets)
        {
            int old = Interlocked.Exchange(ref _updating, 1);
            if (old == 0)
                TimeUtility.updated += OnUpdate;
            upd = 1;
        }
        else upd = _updating;

        if (upd > 0)
            return;

        if (DevkitServerModule.IsMainThread)
            UpdateUIOnDirty();
        else
            DevkitServerUtility.QueueOnMainThread(UpdateUIOnDirty);
    }
    private void UpdateUIOnDirty()
    {
        if (TotalMissingPackets > 0)
        {
            LoadingUI.SetDownloadFileName(DevkitServerModule.LevelLoadingLocalization.Translate("RecoveringMissingPackets", _mapName, TotalMissingPackets, InitialMissingPackets));
            LoadingUI.NotifyDownloadProgress(InitialMissingPackets != 0 && InitialMissingPackets != TotalMissingPackets
                ? (float)(InitialMissingPackets - TotalMissingPackets) / InitialMissingPackets * 0.90f + 0.05f
                : 0.05f);
        }
        else if (ReceivedBytes >= TotalBytes || InitialMissingPackets > 0)
        {
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
        double elapsed = (DateTime.UtcNow - StartTimestamp).TotalSeconds;

        if (ReceivedBytes > 0)
        {
            double remainingTime = elapsed / ReceivedBytes * (TotalBytes - ReceivedBytes);
            string timeString = ((int)Math.Floor(remainingTime / 60)).ToString("00") + ":" + ((int)Math.Floor(remainingTime % 60)).ToString("00");
            long bytes = (long)Math.Round(ReceivedBytes / elapsed);

            LoadingUI.SetDownloadFileName(DevkitServerModule.LevelLoadingLocalization.Translate("Downloading",
                _mapName, FormattingUtil.FormatCapacity(ReceivedBytes), FormattingUtil.FormatCapacity(TotalBytes), FormattingUtil.FormatCapacity(bytes), timeString));
        }
        else
        {
            LoadingUI.SetDownloadFileName(DevkitServerModule.LevelLoadingLocalization.Translate("CalculatingSpeed", _mapName,
                FormattingUtil.FormatCapacity(ReceivedBytes), FormattingUtil.FormatCapacity(TotalBytes)));
        }

        LoadingUI.NotifyDownloadProgress(ReceivedBytes != 0 && TotalBytes != 0 ? (float)ReceivedBytes / TotalBytes * 0.90f + 0.05f : 0.05f);
    }
#endif
    void IDisposable.Dispose() { }
}
