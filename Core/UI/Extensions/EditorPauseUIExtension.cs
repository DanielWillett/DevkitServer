#if CLIENT
using Cysharp.Threading.Tasks;
using DevkitServer.API;
using DevkitServer.API.Cartography;
using DevkitServer.API.UI.Extensions;
using DevkitServer.API.UI.Extensions.Members;
using DevkitServer.Configuration;
using DevkitServer.Levels;
using DevkitServer.Patches;
using System.Reflection;

namespace DevkitServer.Core.UI.Extensions;

[UIExtension(typeof(EditorPauseUI))]
internal class EditorPauseUIExtension : UIExtension, IUnpatchableUIExtension
{
    private int _isPatched;

    [ExistingMember("saveButton", FailureBehavior = ExistingMemberFailureBehavior.Ignore)]
    private readonly SleekButtonIcon? _saveButton;
    
    [ExistingMember("chartButton", FailureBehavior = ExistingMemberFailureBehavior.Ignore)]
    private readonly SleekButtonIcon? _chartButton;
    
    [ExistingMember("mapButton", FailureBehavior = ExistingMemberFailureBehavior.Ignore)]
    private readonly SleekButtonIcon? _mapButton;
    
    [ExistingMember("exitButton", FailureBehavior = ExistingMemberFailureBehavior.Ignore)]
    private readonly SleekButtonIconConfirm? _exitButton;
    
    [ExistingMember("quitButton", FailureBehavior = ExistingMemberFailureBehavior.Ignore)]
    private readonly SleekButtonIconConfirm? _quitButton;

#nullable disable

    [ExistingMember("container")]
    private readonly SleekFullscreenBox _container;

#nullable restore

    private readonly ISleekLabel? _chartingLabel;
    private readonly ISleekLabel? _satelliteLabel;
    private readonly ISleekLabel? _savingLabel;
    private readonly ISleekButton _saveAndBackupButton;

    public EditorPauseUIExtension()
    {
        if (Interlocked.Exchange(ref _isPatched, 1) == 0)
            Patch();

        Logger.DevkitServer.LogDebug(nameof(EditorPauseUIExtension), _container.Format());

        if (_chartButton != null)
        {
            _chartingLabel = Glazier.Get().CreateLabel();
            _chartingLabel.CopyTransformFrom(_chartButton);
            _chartingLabel.PositionOffset_X -= _chartButton.SizeOffset_X * 2 + 10;
            _chartingLabel.TextAlignment = TextAnchor.MiddleRight;
            _chartingLabel.SizeOffset_X = 300;
            _chartingLabel.SizeOffset_Y = 30;
            _chartingLabel.TextContrastContext = ETextContrastContext.ColorfulBackdrop;
            _chartingLabel.Text = DevkitServerModule.MainLocalization.Translate("RenderingChartInProgress");
            _chartingLabel.IsVisible = false;

            _container.AddChild(_chartingLabel);
        }

        if (_mapButton != null)
        {
            _satelliteLabel = Glazier.Get().CreateLabel();
            _satelliteLabel.CopyTransformFrom(_mapButton);
            _satelliteLabel.PositionOffset_X -= _mapButton.SizeOffset_X * 2 + 10;
            _satelliteLabel.TextAlignment = TextAnchor.MiddleRight;
            _satelliteLabel.SizeOffset_X = 300;
            _satelliteLabel.SizeOffset_Y = 30;
            _satelliteLabel.TextContrastContext = ETextContrastContext.ColorfulBackdrop;
            _satelliteLabel.Text = DevkitServerModule.MainLocalization.Translate("RenderingSatelliteInProgress");
            _satelliteLabel.IsVisible = false;

            _container.AddChild(_satelliteLabel);
        }

        _saveAndBackupButton = Glazier.Get().CreateButton();

        if (_saveButton != null)
        {
            _savingLabel = Glazier.Get().CreateLabel();
            _savingLabel.CopyTransformFrom(_saveButton);
            _savingLabel.PositionOffset_X -= _saveButton.SizeOffset_X * 2 + 10;
            _savingLabel.TextAlignment = TextAnchor.MiddleRight;
            _savingLabel.SizeOffset_X = 450;
            _savingLabel.SizeOffset_Y = 30;
            _savingLabel.TextContrastContext = ETextContrastContext.ColorfulBackdrop;
            OnLevelDataGatherStateUpdated(this);

            _container.AddChild(_savingLabel);

            _saveAndBackupButton.CopyTransformFrom(_saveButton);
            _saveAndBackupButton.PositionOffset_Y -= _saveAndBackupButton.SizeOffset_Y + 10;
        }
        else
        {
            _saveAndBackupButton.PositionOffset_X = -100f;
            _saveAndBackupButton.PositionOffset_Y = -155f;
            _saveAndBackupButton.PositionScale_X = 0.5f;
            _saveAndBackupButton.PositionScale_Y = 0.5f;
            _saveAndBackupButton.SizeOffset_X = 200f;
            _saveAndBackupButton.SizeOffset_Y = 30f;
        }

        _saveAndBackupButton.Text = DevkitServerModule.MainLocalization.Translate("BackupAndSaveButton");
        _saveAndBackupButton.IsVisible = true;
        _saveAndBackupButton.OnClicked += OnSaveAndBackupRequested;

        _container.AddChild(_saveAndBackupButton);
    }

    private static void OnSaveAndBackupRequested(ISleekElement button)
    {
        if (DevkitServerModule.IsEditing)
        {
            // todo check perms and send backup request to server
        }
        else if (DevkitServerModule.BackupManager == null)
        {
            Logger.DevkitServer.LogWarning(nameof(EditorPauseUIExtension), "BackupManager was never initialized.");
        }
        {
            DevkitServerModule.BackupManager?.Backup();
        }
    }
    internal static void OnLevelDataGatherStateUpdated()
    {
        try
        {
            EditorPauseUIExtension? extension = UIExtensionManager.GetInstance<EditorPauseUIExtension>();
            OnLevelDataGatherStateUpdated(extension);
        }
        catch (Exception ex) // if this code throws an error it will be very problematic
        {
            Logger.DevkitServer.LogWarning(nameof(EditorPauseUIExtension), ex, "Error updating save label in the pause menu.");
        }
    }
    internal static void OnLevelDataGatherStateUpdated(EditorPauseUIExtension? extension)
    {
        try
        {
            if (extension?._savingLabel == null)
                return;

            if (LevelData.IsGathering)
            {
                extension._savingLabel.Text = DevkitServerModule.MainLocalization.Translate("SaveInProgressWillFreeze");

                extension._savingLabel.IsVisible = true;

                if (extension._saveButton != null)
                    extension._saveButton.isClickable = false;
                if (extension._exitButton != null)
                    extension._exitButton.isClickable = false;
                if (extension._quitButton != null)
                    extension._quitButton.isClickable = false;
                extension._saveAndBackupButton.IsClickable = false;

                return;
            }
            
            if (DevkitServerModule.BackupManager != null && DevkitServerModule.BackupManager.IsBackingUp)
            {
                extension._savingLabel.Text = DevkitServerModule.MainLocalization.Translate("BackingUpInProgress");
                extension._savingLabel.IsVisible = true;
                extension._saveAndBackupButton.IsClickable = false;
            }
            else
            {
                extension._saveAndBackupButton.IsClickable = true;
                extension._savingLabel.IsVisible = false;
            }

            if (extension._saveButton != null)
                extension._saveButton.isClickable = true;
            if (extension._exitButton != null)
                extension._exitButton.isClickable = true;
            if (extension._quitButton != null)
                extension._quitButton.isClickable = true;
        }
        catch (Exception ex) // if this code throws an error it will be very problematic
        {
            Logger.DevkitServer.LogWarning(nameof(EditorPauseUIExtension), ex, "Error updating save label in the pause menu.");
        }
    }
    private static void Patch()
    {
        try
        {
            MethodInfo? method = typeof(EditorPauseUI).GetMethod("onClickedChartButton", BindingFlags.NonPublic | BindingFlags.Static);
            if (method != null)
            {
                PatchesMain.Patcher.Patch(method, Accessor.GetHarmonyMethod(OnClickingChartButton));
            }
            else Logger.DevkitServer.LogWarning(nameof(EditorPauseUIExtension),
                $"Method not found: {FormattingUtil.FormatMethod(typeof(ClickedButton), "onClickedChartButton", isStatic: true)}.");
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError(nameof(EditorPauseUIExtension), ex,
                $"Failed to patch method: {FormattingUtil.FormatMethod(typeof(ClickedButton), "onClickedChartButton", isStatic: true)}.");
        }

        try
        {
            MethodInfo? method = typeof(EditorPauseUI).GetMethod("onClickedMapButton", BindingFlags.NonPublic | BindingFlags.Static);
            if (method != null)
            {
                PatchesMain.Patcher.Patch(method, Accessor.GetHarmonyMethod(OnClickingSatelliteButton));
            }
            else Logger.DevkitServer.LogWarning(nameof(EditorPauseUIExtension),
                $"Method not found: {FormattingUtil.FormatMethod(typeof(ClickedButton), "onClickedMapButton", isStatic: true)}.");
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError(nameof(EditorPauseUIExtension), ex,
                $"Failed to patch method: {FormattingUtil.FormatMethod(typeof(ClickedButton), "onClickedMapButton", isStatic: true)}.");
        }
    }
    void IUnpatchableUIExtension.Unpatch()
    {
        if (Interlocked.Exchange(ref _isPatched, 0) == 0)
            return;

        try
        {
            MethodInfo? method = typeof(EditorPauseUI).GetMethod("onClickedChartButton", BindingFlags.NonPublic | BindingFlags.Static);
            if (method != null)
            {
                PatchesMain.Patcher.Unpatch(method, Accessor.GetMethod(OnClickingChartButton));
            }
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogWarning(nameof(EditorPauseUIExtension), ex,
                $"Failed to unpatch method: {FormattingUtil.FormatMethod(typeof(ClickedButton), "onClickedChartButton", isStatic: true)}.");
        }

        try
        {
            MethodInfo? method = typeof(EditorPauseUI).GetMethod("onClickedMapButton", BindingFlags.NonPublic | BindingFlags.Static);
            if (method != null)
            {
                PatchesMain.Patcher.Unpatch(method, Accessor.GetMethod(OnClickingSatelliteButton));
            }
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogWarning(nameof(EditorPauseUIExtension), ex,
                $"Failed to unpatch method: {FormattingUtil.FormatMethod(typeof(ClickedButton), "onClickedMapButton", isStatic: true)}.");
        }
    }

    private static bool OnClickingChartButton(ISleekElement button)
    {
        if (!DevkitServerModule.IsEditing && DevkitServerConfig.Config is { UseVanillaCartographyInSingleplayer: true })
        {
            Logger.DevkitServer.LogDebug(nameof(EditorPauseUIExtension), "Falling back to vanilla charting function.");
            return true;
        }

        UniTask.Create(async () =>
        {
            EditorPauseUIExtension? extension = UIExtensionManager.GetInstance<EditorPauseUIExtension>();

            SleekButtonIcon? btn = button as SleekButtonIcon;
            if (btn != null)
                btn.isClickable = false;
            if (extension?._chartingLabel != null)
                extension._chartingLabel.IsVisible = true;
            try
            {
                await UniTask.NextFrame();

                string? outputPath = await ChartCartography.CaptureChart(token: Level.editing == null ? default : Level.editing.GetCancellationTokenOnDestroy());
                if (outputPath != null)
                    Logger.DevkitServer.LogDebug(nameof(EditorPauseUIExtension), $"Captured satellite image to {outputPath.Format()}.");
                else
                    Logger.DevkitServer.LogWarning(nameof(EditorPauseUIExtension), "Failed to capture satellite image. See above.");
            }
            finally
            {
                if (btn != null)
                    btn.isClickable = true;
                if (extension?._chartingLabel != null)
                    extension._chartingLabel.IsVisible = false;
            }
        });

        return false;
    }
    private static bool OnClickingSatelliteButton(ISleekElement button)
    {
        if (!DevkitServerModule.IsEditing && DevkitServerConfig.Config is { UseVanillaCartographyInSingleplayer: true })
        {
            Logger.DevkitServer.LogDebug(nameof(EditorPauseUIExtension), "Falling back to vanilla satellite function.");
            return true;
        }

        UniTask.Create(async () =>
        {
            EditorPauseUIExtension? extension = UIExtensionManager.GetInstance<EditorPauseUIExtension>();

            SleekButtonIcon? btn = button as SleekButtonIcon;
            if (btn != null)
                btn.isClickable = false;
            if (extension?._satelliteLabel != null)
                extension._satelliteLabel.IsVisible = true;

            try
            {
                await UniTask.NextFrame();

                string? outputPath = await SatelliteCartography.CaptureSatellite(token: Level.editing == null ? default : Level.editing.GetCancellationTokenOnDestroy());
                if (outputPath != null)
                    Logger.DevkitServer.LogDebug(nameof(EditorPauseUIExtension), $"Captured satellite image to {outputPath.Format()}.");
                else
                    Logger.DevkitServer.LogWarning(nameof(EditorPauseUIExtension), "Failed to capture satellite image. See above.");
            }
            finally
            {
                if (btn != null)
                    btn.isClickable = true;
                if (extension?._satelliteLabel != null)
                    extension._satelliteLabel.IsVisible = false;
            }
        });

        return false;
    }
}
#endif