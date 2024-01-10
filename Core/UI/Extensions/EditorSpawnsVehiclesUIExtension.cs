#if CLIENT
using DevkitServer.API.UI.Extensions;

namespace DevkitServer.Core.UI.Extensions;
[UIExtension(typeof(EditorSpawnsVehiclesUI))]
internal class EditorSpawnsVehiclesUIExtension : BaseEditorSpawnsUIExtension<VehicleSpawnpoint>
{
    private const float DistanceMax = 200f;
    protected override bool IsVisible
    {
        get => LevelVisibility.vehiclesVisible;
        set => LevelVisibility.vehiclesVisible = value;
    }
    public EditorSpawnsVehiclesUIExtension() : base(new Vector3(0f, 2.5f, 0f), 50f, DistanceMax)
    {
        SpawnUtil.OnVehicleSpawnpointAdded += OnSpawnAdded;
        SpawnUtil.OnVehicleSpawnpointRemoved += OnSpawnRemoved;
        SpawnUtil.OnVehicleSpawnpointMoved += OnSpawnMoved;
        SpawnUtil.OnVehicleSpawnTableChanged += OnSpawnTableChanged;
        SpawnTableUtil.OnVehicleSpawnTableNameUpdated += OnNameUpdated;
    }
    private void OnNameUpdated(VehicleTable table, int index)
    {
        foreach (VehicleSpawnpoint spawnpoint in SpawnUtil.EnumerateVehicleSpawns(MovementUtil.MainCameraRegion))
        {
            if (spawnpoint.type == index)
                UpdateLabel(spawnpoint, table.name);
        }
    }
    protected override void OnRegionUpdated(RegionCoord oldRegion, RegionCoord newRegion, bool isInRegion)
    {
        Regions.tryGetPoint(newRegion.x, newRegion.y, out Vector3 regionPos);
        regionPos.x += Regions.WORLD_SIZE / 2f;
        regionPos.y += Regions.WORLD_SIZE / 2f;
        float rad = DistanceMax + Regions.REGION_SIZE / 2f;
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
    protected override void OnShown()
    {
        base.OnShown();
        OnRegionUpdated(default, MovementUtil.MainCameraRegion, MovementUtil.MainCameraIsInRegion);
    }
    protected override void OnHidden()
    {
        ClearLabels();
        base.OnHidden();
    }
    private static string GetText(VehicleSpawnpoint point) => LevelVehicles.tables.Count > point.type ? LevelVehicles.tables[point.type].name : point.type + " - Null";
    protected override Vector3 GetPosition(VehicleSpawnpoint spawn) => spawn.node.position;
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
    private void OnSpawnTableChanged(VehicleSpawnpoint point, int index)
    {
        UpdateLabel(point, GetText(point));
    }
    internal void OnSpawnMoved(VehicleSpawnpoint point, Vector3 fromPosition, Vector3 toPosition, float fromAngle, float toAngle)
    {
        if (fromPosition != toPosition)
            UpdateLabel(point);
    }
    protected override void OnDestroyed()
    {
        SpawnUtil.OnVehicleSpawnpointAdded -= OnSpawnAdded;
        SpawnUtil.OnVehicleSpawnpointRemoved -= OnSpawnRemoved;
        SpawnUtil.OnVehicleSpawnpointMoved -= OnSpawnMoved;
        SpawnUtil.OnVehicleSpawnTableChanged -= OnSpawnTableChanged;
        SpawnTableUtil.OnVehicleSpawnTableNameUpdated -= OnNameUpdated;
        base.OnDestroyed();
    }
}
#endif