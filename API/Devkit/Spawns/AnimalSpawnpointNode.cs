using DevkitServer.Multiplayer.Actions;
using DevkitServer.Multiplayer.Levels;
using SDG.Framework.Devkit.Interactable;

namespace DevkitServer.API.Devkit.Spawns;

public class AnimalSpawnpointNode : IndexedSpawnpointNode, IDevkitSelectionCopyableHandler
{
    private static readonly Color32 SpawnpointColor = new Color32(255, 204, 102, 255);
    public AnimalSpawnpoint Spawnpoint { get; internal set; } = null!;
    public override bool ShouldBeVisible => LevelVisibility.animalsVisible;
    public override SpawnType SpawnType => SpawnType.Animal;
    protected override void SetupCollider()
    {
        BoxCollider collider = transform.GetOrAddComponent<BoxCollider>();
        Collider = collider;
        collider.size = new Vector3(2f, 2f, 2f);
        collider.center = new Vector3(0f, 1f, 0f);
    }
    protected override bool Add()
    {
        SpawnUtil.AddAnimalSpawnpointLocal(Spawnpoint);
        return true;
    }
    protected override bool Remove(bool permanently)
    {
        return SanityCheck() &&
               SpawnUtil.RemoveSpawnLocal(SpawnType.Animal, IndexIntl, permanently) == SpawnpointEventResult.Success;
    }
    protected override void Transform()
    {
        if (SanityCheck())
            SpawnUtil.MoveSpawnpointLocal(SpawnType.Animal, IndexIntl, transform.position);
    }
    protected override NetId64 GetNetId()
    {
        NetId64 netId = default;
        if (SanityCheck())
            SpawnsNetIdDatabase.TryGetSpawnNetId(SpawnType.Animal, IndexIntl, out netId);
        return netId;
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
    GameObject IDevkitSelectionCopyableHandler.copySelection()
    {
        AnimalSpawnpoint point = new AnimalSpawnpoint(Spawnpoint.type, Spawnpoint.point);
        SpawnUtil.AddAnimalSpawnpointLocal(point);
        return point.node.gameObject;
    }
    public override bool SanityCheck()
    {
        if (SpawnUtil.SanityCheckIndex(Spawnpoint, ref IndexIntl))
            return true;

        Logger.DevkitServer.LogWarning(nameof(AnimalSpawnpointNode), $"Failed sanity check: {this.Format()}.");
        return false;
    }
}