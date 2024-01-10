using DevkitServer.Multiplayer.Actions;
using DevkitServer.Multiplayer.Levels;
using SDG.Framework.Devkit.Interactable;

namespace DevkitServer.API.Devkit.Spawns;

public class ItemSpawnpointNode : RegionalSpawnpointNode, IDevkitSelectionTransformableHandler, IDevkitSelectionCopyableHandler
{
    private static readonly Color32 SpawnpointColor = new Color32(204, 255, 255, 255);
    public ItemSpawnpoint Spawnpoint { get; internal set; } = null!;
    public override bool ShouldBeVisible => LevelVisibility.itemsVisible;
    protected override bool Add()
    {
        if (Regions.checkSafe(Spawnpoint.point))
        {
            SpawnUtil.AddItemSpawnLocal(Spawnpoint);
            return true;
        }

        return false;
    }
    protected override bool Remove()
    {
        return SpawnUtil.RemoveItemSpawnLocal(Spawnpoint, false);
    }
    protected override void Transform()
    {
        SpawnUtil.MoveSpawnpointLocal(Spawnpoint, transform.position, out _);
    }
    protected override NetId64 GetNetId()
    {
        SpawnsNetIdDatabase.TryGetItemSpawnNetId(Region, out NetId64 netId);
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
    void IDevkitSelectionTransformableHandler.transformSelection()
    {
        SpawnUtil.MoveSpawnpointLocal(Spawnpoint, transform.position, out _);
    }
    GameObject IDevkitSelectionCopyableHandler.copySelection()
    {
        ItemSpawnpoint point = new ItemSpawnpoint(Spawnpoint.type, Spawnpoint.point);
        SpawnUtil.AddItemSpawnLocal(point);
        return point.node.gameObject;
    }
}