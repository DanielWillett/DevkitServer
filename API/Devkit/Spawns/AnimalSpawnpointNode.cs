using SDG.Framework.Devkit.Interactable;

namespace DevkitServer.API.Devkit.Spawns;

public class AnimalSpawnpointNode : IndexedSpawnpointNode, IDevkitSelectionTransformableHandler, IDevkitSelectionCopyableHandler
{
    private static readonly Color32 SpawnpointColor = new Color32(255, 204, 102, 255);
    public AnimalSpawnpoint Spawnpoint { get; internal set; } = null!;
    public override bool ShouldBeVisible => LevelVisibility.animalsVisible;
    protected override void SetupCollider()
    {
        BoxCollider collider = transform.GetOrAddComponent<BoxCollider>();
        Collider = collider;
        collider.size = new Vector3(2f, 2f, 2f);
        collider.center = new Vector3(0f, 1f, 0f);
    }
    protected override bool Add()
    {
        SpawnUtil.AddAnimalSpawnLocal(Spawnpoint);
        return true;
    }
    protected override bool Remove()
    {
        return SpawnUtil.RemoveAnimalSpawnLocal(Spawnpoint, false);
    }

    protected override void Transform()
    {
        SpawnUtil.MoveSpawnpointLocal(Spawnpoint, transform.position);
    }

    public override string Format(ITerminalFormatProvider provider)
    {
        if (LevelAnimals.tables.Count > Spawnpoint.type)
            return "Animal Spawnpoint".Colorize(SpawnpointColor) + $" ({LevelAnimals.tables[Spawnpoint.type].name.Format(false)})";
        return "Animal Spawnpoint".Colorize(SpawnpointColor);
    }

    public override string ToString()
    {
        if (LevelAnimals.tables.Count > Spawnpoint.type)
            return $"Animal Spawnpoint ({LevelAnimals.tables[Spawnpoint.type].name})";
        return "Animal Spawnpoint";
    }
    void IDevkitSelectionTransformableHandler.transformSelection()
    {
        SpawnUtil.MoveSpawnpointLocal(Spawnpoint, transform.position);
    }
    GameObject IDevkitSelectionCopyableHandler.copySelection()
    {
        AnimalSpawnpoint point = new AnimalSpawnpoint(Spawnpoint.type, Spawnpoint.point);
        SpawnUtil.AddAnimalSpawnLocal(point);
        return point.node.gameObject;
    }
}