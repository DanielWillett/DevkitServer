using DevkitServer.Multiplayer.Actions;
using DevkitServer.Multiplayer.Levels;
using SDG.Framework.Devkit.Interactable;

namespace DevkitServer.API.Devkit.Spawns;

public class PlayerSpawnpointNode : IndexedSpawnpointNode, IRotatableNode, IDevkitSelectionCopyableHandler
{
    private static readonly Color32 SpawnpointColor = new Color32(204, 255, 102, 255);
    public PlayerSpawnpoint Spawnpoint { get; internal set; } = null!;
    public Renderer? ArrowRenderer { get; protected set; }
    public override bool ShouldBeVisible => LevelVisibility.playersVisible;
    public override SpawnType SpawnType => SpawnType.Player;
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
        SpawnUtil.AddPlayerSpawnpointLocal(Spawnpoint);
        return true;
    }
    protected override bool Remove(bool permanently)
    {
        return SanityCheck() &&
               SpawnUtil.RemoveSpawnLocal(SpawnType.Player, IndexIntl, permanently) == SpawnpointEventResult.Success;
    }
    protected override void Transform()
    {
        if (SanityCheck())
            SpawnUtil.TransformSpawnpointLocal(SpawnType.Player, IndexIntl, transform.position, transform.eulerAngles.y);
    }
    protected override NetId64 GetNetId()
    {
        NetId64 netId = default;
        if (SanityCheck())
            SpawnsNetIdDatabase.TryGetSpawnNetId(SpawnType.Player, IndexIntl, out netId);
        return netId;
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
    GameObject IDevkitSelectionCopyableHandler.copySelection()
    {
        PlayerSpawnpoint point = new PlayerSpawnpoint(Spawnpoint.point, Spawnpoint.angle, Spawnpoint.isAlt);
        SpawnUtil.AddPlayerSpawnpointLocal(point);
        return point.node.gameObject;
    }
    public override bool SanityCheck()
    {
        if (SpawnUtil.SanityCheckIndex(Spawnpoint, ref IndexIntl))
            return true;

        Logger.DevkitServer.LogWarning(nameof(PlayerSpawnpointNode), $"Failed sanity check: {this.Format()}.");
        return false;
    }
}