using DevkitServer.Multiplayer.Actions;
using DevkitServer.Multiplayer.Levels;
using SDG.Framework.Devkit.Interactable;

namespace DevkitServer.API.Devkit.Spawns;

public class VehicleSpawnpointNode : IndexedSpawnpointNode, IRotatableNode, IDevkitSelectionCopyableHandler
{
    private static readonly Color32 SpawnpointColor = new Color32(148, 184, 184, 255);
    public VehicleSpawnpoint Spawnpoint { get; internal set; } = null!;
    public Renderer? ArrowRenderer { get; protected set; }
    public override bool ShouldBeVisible => LevelVisibility.vehiclesVisible;
    public override SpawnType SpawnType => SpawnType.Vehicle;
    public override Color Color
    {
        set
        {
            if (ArrowRenderer != null)
                ArrowRenderer.material.color = value;
            base.Color = value;
        }
    }
    protected override void SetupCollider()
    {
        BoxCollider collider = transform.GetOrAddComponent<BoxCollider>();
        Collider = collider;
        collider.size = new Vector3(3f, 2f, 4f);
        collider.center = new Vector3(0f, 1f, 0f);
    }
    protected override bool Add()
    {
        SpawnUtil.AddVehicleSpawnpointLocal(Spawnpoint);
        return true;
    }
    protected override bool Remove()
    {
        return SanityCheck() &&
               SpawnUtil.RemoveSpawnLocal(SpawnType.Vehicle, IndexIntl, false) == SpawnpointEventResult.Success;
    }
    protected override void Transform()
    {
        if (SanityCheck())
            SpawnUtil.TransformSpawnpointLocal(SpawnType.Vehicle, IndexIntl, transform.position, transform.eulerAngles.y);
    }
    protected override NetId64 GetNetId()
    {
        NetId64 netId = default;
        if (SanityCheck())
            SpawnsNetIdDatabase.TryGetSpawnNetId(SpawnType.Vehicle, IndexIntl, out netId);
        return netId;
    }
    protected override void Init()
    {
        Transform arrow = transform.Find("Arrow");
        ArrowRenderer = arrow == null ? null : arrow.GetComponent<Renderer>();
    }
    public override string Format(ITerminalFormatProvider provider)
    {
        if (LevelVehicles.tables.Count > Spawnpoint.type)
            return "Vehicle Spawnpoint".Colorize(SpawnpointColor) + $" ({LevelVehicles.tables[Spawnpoint.type].name.Format(false)})";
        return "Vehicle Spawnpoint".Colorize(SpawnpointColor);
    }
    public override string ToString()
    {
        if (LevelVehicles.tables.Count > Spawnpoint.type)
            return $"Vehicle Spawnpoint ({LevelVehicles.tables[Spawnpoint.type].name})";
        return "Vehicle Spawnpoint";
    }
    GameObject IDevkitSelectionCopyableHandler.copySelection()
    {
        VehicleSpawnpoint point = new VehicleSpawnpoint(Spawnpoint.type, Spawnpoint.point, Spawnpoint.angle);
        SpawnUtil.AddVehicleSpawnpointLocal(point);
        return point.node.gameObject;
    }
    public override bool SanityCheck() => SpawnUtil.SanityCheckIndex(Spawnpoint, ref IndexIntl);
}
