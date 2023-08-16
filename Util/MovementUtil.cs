using DevkitServer.API;
#if CLIENT
using DevkitServer.Util.Debugging;
#endif

namespace DevkitServer.Util;

public delegate void RegionCoordUpdated(RegionCoord oldRegion, RegionCoord newRegion, bool isInRegion);
public static class MovementUtil
{
#if CLIENT
    private static readonly CachedMulticastEvent<RegionCoordUpdated> EventOnMainCameraRegionUpdated = new CachedMulticastEvent<RegionCoordUpdated>(typeof(MovementUtil), nameof(OnMainCameraRegionUpdated));
    private static TransformUpdateTracker _mainCameraTracker = new TransformUpdateTracker(MainCamera.instance == null ? null : MainCamera.instance.transform);
    public static RegionCoord MainCameraRegion { get; private set; }
    public static bool MainCameraIsInRegion { get; private set; }

    public static event TransformUpdateTrackerUpdated OnMainCameraTransformUpdated
    {
        add => _mainCameraTracker.OnTransformUpdated += value;
        remove => _mainCameraTracker.OnTransformUpdated -= value;
    }

    public static event RegionCoordUpdated OnMainCameraRegionUpdated
    {
        add => EventOnMainCameraRegionUpdated.Add(value);
        remove => EventOnMainCameraRegionUpdated.Remove(value);
    }
    internal static void Init()
    {
        MainCamera.instanceChanged += OnInstanceChanged;
    }
    internal static void Deinit()
    {
        MainCamera.instanceChanged -= OnInstanceChanged;
    }
    internal static void OnInstanceChanged()
    {
        if (_mainCameraTracker.Transform == MainCamera.instance.transform)
            return;
        
        TransformUpdateTracker old = _mainCameraTracker;
        _mainCameraTracker = new TransformUpdateTracker(MainCamera.instance.transform);
        old.TransferEventsTo(_mainCameraTracker);
    }
    internal static void OnUpdate()
    {
        _mainCameraTracker.OnUpdate();
        if (_mainCameraTracker.HasPositionChanged && LoadingUI.window != MenuUI.window)
        {
            Vector3 position = MainCamera.instance.transform.position;
            bool wasInRegion = MainCameraIsInRegion;
            if (!Regions.checkSafe(position))
            {
                MainCameraIsInRegion = false;
                if (position.x < -4096f)
                    position.x = -4096f;
                else if (position.x >= 4096f)
                    position.x = 4096f - float.Epsilon;

                if (position.y < -4096f)
                    position.y = -4096f;
                else if (position.y >= 4096f)
                    position.y = 4096f - float.Epsilon;
            }
            else MainCameraIsInRegion = true;
            RegionCoord r = new RegionCoord(position);
            RegionCoord r2 = MainCameraRegion;
            if (r.x != r2.x || r.y != r2.y || wasInRegion != MainCameraIsInRegion)
            {
                MainCameraRegion = r;
                EventOnMainCameraRegionUpdated.TryInvoke(r2, r, MainCameraIsInRegion);
                if (RegionDebug.RegionsEnabled)
                    Logger.LogDebug($"Region updated: {r2.Format()} -> {r.Format()}.");
            }
        }
    }
#endif
}
