using SDG.Framework.Devkit.Interactable;

namespace DevkitServer.API.Devkit.Spawns;

public class PlayerSpawnpointNode : IndexedSpawnpointNode, IRotatableNode, IDevkitSelectionTransformableHandler, IDevkitSelectionCopyableHandler
{
    private static readonly Color32 SpawnpointColor = new Color32(204, 255, 102, 255);
    public PlayerSpawnpoint Spawnpoint { get; internal set; } = null!;
    public Renderer? ArrowRenderer { get; protected set; }
    public override bool ShouldBeVisible => LevelVisibility.playersVisible;
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
        collider.size = new Vector3(2.25f, 2f, 0.35f);
        collider.center = new Vector3(0f, 1f, 0f);
    }

    protected override bool Add()
    {
        SpawnUtil.AddPlayerSpawnLocal(Spawnpoint);
        return true;
    }
    protected override bool Remove()
    {
        return SpawnUtil.RemovePlayerSpawnLocal(Spawnpoint, false);
    }
    protected override void Transform()
    {
        SpawnUtil.TransformSpawnpointLocal(Spawnpoint, transform.position, transform.eulerAngles.y);
    }
    protected override void Init()
    {
        Transform arrow = transform.Find("Arrow");
        ArrowRenderer = arrow == null ? null : arrow.GetComponent<Renderer>();
    }
    public override string Format(ITerminalFormatProvider provider)
    {
        return Spawnpoint.isAlt ? "Player Spawnpoint".Colorize(SpawnpointColor) + " (Alternate)" : "Player Spawnpoint".Colorize(SpawnpointColor);
    }

    public override string ToString()
    {
        return Spawnpoint.isAlt ? "Player Spawnpoint (Alternate)" : "Player Spawnpoint";
    }
    void IDevkitSelectionTransformableHandler.transformSelection()
    {
        SpawnUtil.TransformSpawnpointLocal(Spawnpoint, transform.position, transform.rotation.eulerAngles.y);
    }
    GameObject IDevkitSelectionCopyableHandler.copySelection()
    {
        PlayerSpawnpoint point = new PlayerSpawnpoint(Spawnpoint.point, Spawnpoint.angle, Spawnpoint.isAlt);
        SpawnUtil.AddPlayerSpawnLocal(point);
        return point.node.gameObject;
    }
}