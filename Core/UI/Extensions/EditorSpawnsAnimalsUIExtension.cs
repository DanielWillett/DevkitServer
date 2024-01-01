#if CLIENT
using DevkitServer.API.UI.Extensions;

namespace DevkitServer.Core.UI.Extensions;
[UIExtension(typeof(EditorSpawnsAnimalsUI))]
internal class EditorSpawnsAnimalsUIExtension : BaseEditorSpawnsUIExtension<AnimalSpawnpoint>
{
    private const float DistanceMax = 240f;
    public EditorSpawnsAnimalsUIExtension() : base(new Vector3(0f, 2.5f, 0f), 60f, DistanceMax)
    {
        SpawnUtil.OnAnimalSpawnpointAdded += OnSpawnAdded;
        SpawnUtil.OnAnimalSpawnpointRemoved += OnSpawnRemoved;
        SpawnUtil.OnAnimalSpawnpointMoved += OnSpawnMoved;
        SpawnUtil.OnAnimalSpawnTableChanged += OnSpawnTableChanged;
        SpawnTableUtil.OnAnimalSpawnTableNameUpdated += OnNameUpdated;
    }

    private void OnNameUpdated(AnimalTable table, int index)
    {
        foreach (AnimalSpawnpoint spawnpoint in SpawnUtil.EnumerateAnimalSpawns(MovementUtil.MainCameraRegion))
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
        foreach (AnimalSpawnpoint spawn in new List<AnimalSpawnpoint>(Labels.Keys))
        {
            if ((regionPos - spawn.point).sqrMagnitude >= rad)
                RemoveLabel(spawn);
        }

        foreach (AnimalSpawnpoint spawn in LevelAnimals.spawns)
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

    private static string GetText(AnimalSpawnpoint point) => LevelAnimals.tables.Count > point.type ? LevelAnimals.tables[point.type].name : point.type + " - Null";
    protected override Vector3 GetPosition(AnimalSpawnpoint spawn) => spawn.node.position;
    protected override bool ShouldShow(AnimalSpawnpoint spawn)
    {
        Vector3 lclPos = MainCamera.instance.transform.position;
        return (lclPos - spawn.point).sqrMagnitude < DistanceMax * DistanceMax && LevelAnimals.tables.Count > spawn.type;
    }


    private void OnSpawnAdded(AnimalSpawnpoint point, int index)
    {
        CreateLabel(point, GetText(point));
    }
    private void OnSpawnRemoved(AnimalSpawnpoint point, int index)
    {
        RemoveLabel(point);
    }
    internal void OnSpawnMoved(AnimalSpawnpoint point, Vector3 fromPosition, Vector3 toPosition)
    {
        UpdateLabel(point);
    }
    private void OnSpawnTableChanged(AnimalSpawnpoint point, int index)
    {
        UpdateLabel(point, GetText(point));
    }
    protected override void OnDestroyed()
    {
        SpawnUtil.OnAnimalSpawnpointAdded -= OnSpawnAdded;
        SpawnUtil.OnAnimalSpawnpointRemoved -= OnSpawnRemoved;
        SpawnUtil.OnAnimalSpawnpointMoved -= OnSpawnMoved;
        SpawnUtil.OnAnimalSpawnTableChanged -= OnSpawnTableChanged;
        SpawnTableUtil.OnAnimalSpawnTableNameUpdated -= OnNameUpdated;
        base.OnDestroyed();
    }
}
#endif