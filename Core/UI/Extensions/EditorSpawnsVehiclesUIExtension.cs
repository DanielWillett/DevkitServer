#if CLIENT
using DevkitServer.API.Devkit.Spawns;
using DevkitServer.API.UI.Extensions;
using DevkitServer.API.UI.Extensions.Members;
using DevkitServer.Models;

namespace DevkitServer.Core.UI.Extensions;
[UIExtension(typeof(EditorSpawnsVehiclesUI))]
internal class EditorSpawnsVehiclesUIExtension : BaseEditorSpawnsUIExtensionNormalTables<VehicleSpawnpoint>
{
    private const float DistanceMax = 200f;

    // base properties wont patch right because generics share patches.

    [ExistingMember("vehicleButtons", FailureBehavior = ExistingMemberFailureBehavior.IgnoreNoWarn)]
    protected ISleekButton[]? Assets2 { get; }

    [ExistingMember("tableButtons", FailureBehavior = ExistingMemberFailureBehavior.IgnoreNoWarn)]
    protected ISleekButton[]? Tables2 { get; }

    [ExistingMember("tierButtons", FailureBehavior = ExistingMemberFailureBehavior.IgnoreNoWarn)]
    protected ISleekButton[]? Tiers2 { get; }
    protected override ISleekButton[]? Assets => Assets2;
    protected override ISleekButton[]? Tables => Tables2;
    protected override ISleekButton[]? Tiers => Tiers2;
    protected override bool IsVisible
    {
        get => LevelVisibility.vehiclesVisible;
        set => LevelVisibility.vehiclesVisible = value;
    }
    public EditorSpawnsVehiclesUIExtension() : base(new Vector3(0f, 2.5f, 0f), 50f, DistanceMax, SpawnType.Vehicle)
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
    protected override bool CheckLabelAlive(VehicleSpawnpoint spawn) => spawn.node != null;
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
    internal void OnSpawnMoved(VehicleSpawnpoint point, int index, Vector3 fromPosition, Vector3 toPosition, float fromAngle, float toAngle)
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

    public override void UpdateSpawnName(int index)
    {
        if (!SpawnTableUtil.TryGetSelectedTier(SpawnType.Vehicle, out SpawnTierIdentifier? id) || !id.HasValue)
            return;

        VehicleSpawn spawn = LevelVehicles.tables[id.Value.TableIndex].tiers[id.Value.TierIndex].table[index];
        Asset asset = SDG.Unturned.Assets.find(EAssetType.VEHICLE, spawn.vehicle);
        string str = asset?.FriendlyName ?? asset?.name ?? "?";
        UpdateSpawnName(spawn.vehicle + " " + str, index);
    }
    public override void UpdateTableColor()
    {
        VehicleTable table = LevelVehicles.tables[EditorSpawns.selectedVehicle];
        UpdateTableColor(table.color);
    }
    public override void UpdateTableId()
    {
        VehicleTable table = LevelVehicles.tables[EditorSpawns.selectedVehicle];
        UpdateTableId(table.tableID);
    }
    public override void UpdateTierName(int index)
    {
        VehicleTier tier = LevelVehicles.tables[EditorSpawns.selectedVehicle].tiers[index];

        bool isSelected = SpawnTableUtil.TryGetSelectedTier(SpawnType.Vehicle, out SpawnTierIdentifier? id) && id.HasValue && id.Value.TierIndex == index;

        UpdateTierName(tier.name, index, isSelected);
    }
    public override void UpdateTableName(int index, bool updateField)
    {
        VehicleTable table = LevelVehicles.tables[index];
        UpdateTableName(table.name, index, EditorSpawns.selectedVehicle == index, updateField);
    }
    public override void UpdateTierChance(int index, bool updateSlider)
    {
        VehicleTier tier = LevelVehicles.tables[EditorSpawns.selectedVehicle].tiers[index];
        UpdateTierChance(tier.chance, index, updateSlider);
    }
}
#endif