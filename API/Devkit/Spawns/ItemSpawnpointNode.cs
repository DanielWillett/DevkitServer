using DevkitServer.Multiplayer.Actions;
using DevkitServer.Multiplayer.Levels;
using SDG.Framework.Devkit.Interactable;

namespace DevkitServer.API.Devkit.Spawns;

public class ItemSpawnpointNode : RegionalSpawnpointNode, IDevkitSelectionCopyableHandler
{
    private static readonly Color32 SpawnpointColor = new Color32(204, 255, 255, 255);
    public ItemSpawnpoint Spawnpoint { get; internal set; } = null!;
    public override bool ShouldBeVisible => LevelVisibility.itemsVisible;
    public override SpawnType SpawnType => SpawnType.Item;
    protected override bool Add()
    {
        if (Regions.checkSafe(Spawnpoint.point))
        {
            SpawnUtil.AddItemSpawnpointLocal(Spawnpoint);
            return true;
        }

        return false;
    }
    protected override bool Remove()
    {
        return SanityCheck() &&
               SpawnUtil.RemoveSpawnLocal(SpawnType.Item, RegionIntl, false) == SpawnpointEventResult.Success;
    }
    protected override void Transform()
    {
        if (SanityCheck())
            SpawnUtil.MoveSpawnpointLocal(SpawnType.Item, RegionIntl, transform.position);
    }
    protected override NetId64 GetNetId()
    {
        NetId64 netId = default;
        if (SanityCheck())
            SpawnsNetIdDatabase.TryGetSpawnNetId(SpawnType.Item, RegionIntl, out netId);
        return netId;
    }
    public override string Format(ITerminalFormatProvider provider)
    {
        if (LevelItems.tables.Count > Spawnpoint.type)
            return "Item Spawnpoint".Colorize(SpawnpointColor) + $" ({LevelItems.tables[Spawnpoint.type].name.Format(false)})";
        return "Item Spawnpoint".Colorize(SpawnpointColor);
    }
    public override string ToString()
    {
        if (LevelItems.tables.Count > Spawnpoint.type)
            return $"Item Spawnpoint ({LevelItems.tables[Spawnpoint.type].name})";
        return "Item Spawnpoint";
    }
    GameObject IDevkitSelectionCopyableHandler.copySelection()
    {
        ItemSpawnpoint point = new ItemSpawnpoint(Spawnpoint.type, Spawnpoint.point);
        SpawnUtil.AddItemSpawnpointLocal(point);
        return point.node.gameObject;
    }
    public override bool SanityCheck() => SpawnUtil.SanityCheckRegion(Spawnpoint, ref RegionIntl);
}