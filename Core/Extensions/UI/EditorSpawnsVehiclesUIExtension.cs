#if CLIENT
using DevkitServer.API.UI;

namespace DevkitServer.Core.Extensions.UI;
[UIExtension(typeof(EditorSpawnsVehiclesUI))]
internal class EditorSpawnsVehiclesUIExtension : BaseEditorSpawnsUIExtension<VehicleSpawnpoint>
{
    private const float DistanceMax = 200f;
    public EditorSpawnsVehiclesUIExtension() : base(new Vector3(0f, 2.5f, 0f), 50f, DistanceMax)
    {
        SpawnUtil.OnVehicleSpawnpointAdded += OnSpawnAdded;
        SpawnUtil.OnVehicleSpawnpointRemoved += OnSpawnRemoved;
        SpawnUtil.OnVehicleSpawnpointMoved += OnSpawnMoved;
    }
    protected override void OnRegionUpdated(RegionCoord oldRegion, RegionCoord newRegion, bool isInRegion)
    {
        Regions.tryGetPoint(newRegion.x, newRegion.y, out Vector3 regionPos);
        float rad = DistanceMax + (Regions.REGION_SIZE / 2f);
        rad *= rad;
        foreach (VehicleSpawnpoint spawn in new List<VehicleSpawnpoint>(Labels.Keys))
        {
            if ((regionPos - spawn.point).sqrMagnitude >= rad)
                RemoveLabel(spawn);
        }

        foreach (VehicleSpawnpoint spawn in LevelVehicles.spawns)
        {
            if ((regionPos - spawn.point).sqrMagnitude < rad && !Labels.ContainsKey(spawn))
                CreateLabel(spawn, GetText(spawn));
        }
    }
    protected override void Opened()
    {
        base.Opened();
        OnRegionUpdated(default, MovementUtil.MainCameraRegion, MovementUtil.MainCameraIsInRegion);
    }

    protected override void Closed()
    {
        ClearLabels();
        base.Closed();
    }

    private static string GetText(VehicleSpawnpoint point) => LevelVehicles.tables.Count > point.type ? LevelVehicles.tables[point.type].name : point.type + " - Null";
    protected override Vector3 GetPosition(VehicleSpawnpoint spawn) => spawn.point;
    protected override bool ShouldShow(VehicleSpawnpoint spawn)
    {
        Vector3 lclPos = MainCamera.instance.transform.position;
        return (lclPos - spawn.point).sqrMagnitude < DistanceMax * DistanceMax && LevelVehicles.tables.Count > spawn.type;
    }

    private void OnSpawnAdded(VehicleSpawnpoint point, int index)
    {
        CreateLabel(point, GetText(point));
    }
    private void OnSpawnRemoved(VehicleSpawnpoint point, int index)
    {
        RemoveLabel(point);
    }
    private void OnSpawnMoved(VehicleSpawnpoint point, Vector3 fromPosition, Vector3 toPosition, float fromAngle, float toAngle)
    {
        if (fromPosition != toPosition)
            UpdateLabel(point);
    }
    public override void Dispose()
    {
        SpawnUtil.OnVehicleSpawnpointAdded -= OnSpawnAdded;
        SpawnUtil.OnVehicleSpawnpointRemoved -= OnSpawnRemoved;
        SpawnUtil.OnVehicleSpawnpointMoved -= OnSpawnMoved;
        base.Dispose();
    }
}
#endif