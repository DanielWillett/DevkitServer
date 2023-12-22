#if CLIENT
using DevkitServer.API.Logging;
using DevkitServer.Patches;
using DevkitServer.Players;
using SDG.Framework.Devkit;
using SDG.Framework.Utilities;

namespace DevkitServer.Multiplayer;
internal static class ClientFPSLimiter
{
    // made to limit the amount of actions generated at once which helps with older PCs.

    private static int _oldFps = -1;
    private static int _oldVSyncCount;
    private static bool _isLimited;
    private static bool _subbed;
    private static bool _init;
    internal static void StartPlayingOnEditorServer()
    {
        if (_init)
            return;
        _init = true;
        _oldFps = Application.targetFrameRate;
        _oldVSyncCount = QualitySettings.vSyncCount;
        if (!_subbed)
        {
            TimeUtility.updated += OnUpdate;
            _subbed = true;
        }
        _isLimited = false;
    }
    internal static void StopPlayingOnEditorServer()
    {
        if (!_init)
            return;
        _init = false;
        if (_subbed)
        {
            TimeUtility.updated -= OnUpdate;
            _subbed = false;
        }
        if (_isLimited)
        {
            Application.targetFrameRate = _oldFps;
            QualitySettings.vSyncCount = _oldVSyncCount;
            _isLimited = false;
        }
    }
    private static void OnUpdate()
    {
        if (ClientInfo.Info == null || ClientInfo.Info.ServerMaxClientEditFPS <= 0 || ClientInfo.Info.ServerMaxClientEditFPS == _oldFps && _oldVSyncCount == 0)
            return;

        bool isEditing = false;
        IDevkitTool? activeTool = UserInput.ActiveTool;
        switch (activeTool)
        {
            case TerrainEditor:
                isEditing = TerrainEditorPatches.LastEditedTerrain;
                break;
            case null:
                break;
            default:
                if (FoliageEditorPatches.FoliageEditor.IsInstanceOfType(activeTool))
                {
                    isEditing = !(EditorInteractEx.IsFlying || !Glazier.Get().ShouldGameProcessInput) && InputEx.GetKey(KeyCode.Mouse0);
                    if (isEditing && FoliageEditorPatches.GetEditMode != null && FoliageEditorPatches.FoliageModeExact != null)
                    {
                        IComparable currentMode = FoliageEditorPatches.GetEditMode(activeTool);
                        if (currentMode != null && currentMode.CompareTo(FoliageEditorPatches.FoliageModeExact) == 0)
                            isEditing = false;
                    }
                }
                break;
        }

        if (isEditing)
        {
            if (_isLimited)
                return;

            _isLimited = true;
            Application.targetFrameRate = ClientInfo.Info.ServerMaxClientEditFPS;
            QualitySettings.vSyncCount = 0;
            Logger.DevkitServer.LogDebug(nameof(ClientFPSLimiter), $"Max FPS updated: {ClientInfo.Info.ServerMaxClientEditFPS.Format()}, vSync: {0.Format()}.");
        }
        else
        {
            if (!_isLimited)
                return;

            _isLimited = false;
            Application.targetFrameRate = _oldFps;
            QualitySettings.vSyncCount = _oldVSyncCount;
            Logger.DevkitServer.LogDebug(nameof(ClientFPSLimiter), $"Max FPS updated: {_oldFps.Format()}, vSync: {_oldVSyncCount.Format()}.");
        }
    }
}
#endif