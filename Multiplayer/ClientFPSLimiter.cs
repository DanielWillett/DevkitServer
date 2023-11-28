#if CLIENT
using DevkitServer.Patches;
using DevkitServer.Players;
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
        switch (UserInput.ActiveTool)
        {
            // todo add others, although terrain is the main one

            case TerrainEditor:
                isEditing = TerrainEditorPatches.LastEditedTerrain;
                break;
        }

        if (isEditing)
        {
            if (_isLimited)
                return;

            _isLimited = true;
            Application.targetFrameRate = ClientInfo.Info.ServerMaxClientEditFPS;
            QualitySettings.vSyncCount = 0;
            Logger.LogDebug($"Max FPS updated: {ClientInfo.Info.ServerMaxClientEditFPS.Format()}, vSync: {0.Format()}.");
        }
        else
        {
            if (!_isLimited)
                return;

            _isLimited = false;
            Application.targetFrameRate = _oldFps;
            QualitySettings.vSyncCount = _oldVSyncCount;
            Logger.LogDebug($"Max FPS updated: {_oldFps.Format()}, vSync: {_oldVSyncCount.Format()}.");
        }
    }
}
#endif