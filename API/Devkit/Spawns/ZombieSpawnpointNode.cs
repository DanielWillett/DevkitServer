using DevkitServer.Multiplayer.Actions;
using DevkitServer.Multiplayer.Levels;
using SDG.Framework.Devkit.Interactable;

namespace DevkitServer.API.Devkit.Spawns;

public class ZombieSpawnpointNode : RegionalSpawnpointNode, IDevkitSelectionCopyableHandler
{
    private static readonly Color32 SpawnpointColor = new Color32(41, 163, 41, 255);
    public ZombieSpawnpoint Spawnpoint { get; internal set; } = null!;
    public override bool ShouldBeVisible => LevelVisibility.zombiesVisible;
    public override SpawnType SpawnType => SpawnType.Zombie;
    protected override bool Add()
    {
        if (Regions.checkSafe(Spawnpoint.point))
        {
            SpawnUtil.AddZombieSpawnpointLocal(Spawnpoint);
            return true;
        }

        return false;
    }
    protected override bool Remove()
    {
        return SanityCheck() &&
               SpawnUtil.RemoveSpawnLocal(SpawnType.Zombie, RegionIntl, false) == SpawnpointEventResult.Success;
    }
    protected override void Transform()
    {
        if (SanityCheck())
            SpawnUtil.MoveSpawnpointLocal(SpawnType.Zombie, RegionIntl, transform.position);
    }
    protected override NetId64 GetNetId()
    {
        NetId64 netId = default;
        if (SanityCheck())
            SpawnsNetIdDatabase.TryGetSpawnNetId(SpawnType.Zombie, RegionIntl, out netId);
        return netId;
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
    GameObject IDevkitSelectionCopyableHandler.copySelection()
    {
        ZombieSpawnpoint point = new ZombieSpawnpoint(Spawnpoint.type, Spawnpoint.point);
        SpawnUtil.AddZombieSpawnpointLocal(point);
        return point.node.gameObject;
    }
    public override bool SanityCheck() => SpawnUtil.SanityCheckRegion(Spawnpoint, ref RegionIntl);
}