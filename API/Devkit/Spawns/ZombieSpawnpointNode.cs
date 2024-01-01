using SDG.Framework.Devkit.Interactable;

namespace DevkitServer.API.Devkit.Spawns;

public class ZombieSpawnpointNode : RegionalSpawnpointNode, IDevkitSelectionTransformableHandler, IDevkitSelectionCopyableHandler
{
    private static readonly Color32 SpawnpointColor = new Color32(41, 163, 41, 255);
    public ZombieSpawnpoint Spawnpoint { get; internal set; } = null!;
    public override bool ShouldBeVisible => LevelVisibility.zombiesVisible;
    protected override bool Add()
    {
        if (Regions.checkSafe(Spawnpoint.point))
        {
            SpawnUtil.AddZombieSpawnLocal(Spawnpoint);
            return true;
        }

        return false;
    }
    protected override bool Remove()
    {
        return SpawnUtil.RemoveZombieSpawnLocal(Spawnpoint, false);
    }
    protected override void Transform()
    {
        SpawnUtil.MoveSpawnpointLocal(Spawnpoint, transform.position, out _);
    }
    public override string Format(ITerminalFormatProvider provider)
    {
        if (LevelZombies.tables.Count > Spawnpoint.type)
            return "Zombie Spawnpoint".Colorize(SpawnpointColor) + $" ({LevelZombies.tables[Spawnpoint.type].name.Format(false)})";
        return "Zombie Spawnpoint".Colorize(SpawnpointColor);
    }

    public override string ToString()
    {
        if (LevelZombies.tables.Count > Spawnpoint.type)
            return $"Zombie Spawnpoint ({LevelZombies.tables[Spawnpoint.type].name})";
        return "Zombie Spawnpoint";
    }
    void IDevkitSelectionTransformableHandler.transformSelection()
    {
        SpawnUtil.MoveSpawnpointLocal(Spawnpoint, transform.position, out _);
    }
    GameObject IDevkitSelectionCopyableHandler.copySelection()
    {
        ZombieSpawnpoint point = new ZombieSpawnpoint(Spawnpoint.type, Spawnpoint.point);
        SpawnUtil.AddZombieSpawnLocal(point);
        return point.node.gameObject;
    }
}