#if CLIENT
using DevkitServer.API.UI;

namespace DevkitServer.Core.Extensions.UI;
[UIExtension(typeof(EditorSpawnsAnimalsUI))]
internal class EditorSpawnsAnimalsUIExtension : BaseEditorSpawnsUIExtension<AnimalSpawnpoint>
{
    private const float DistanceMax = 240f;
    public EditorSpawnsAnimalsUIExtension() : base(new Vector3(0f, 2.5f, 0f), 60f, DistanceMax)
    {
        SpawnUtil.OnAnimalSpawnpointAdded += OnSpawnAdded;
        SpawnUtil.OnAnimalSpawnpointRemoved += OnSpawnRemoved;
        SpawnUtil.OnAnimalSpawnpointMoved += OnSpawnMoved;
    }
    protected override void OnRegionUpdated(RegionCoord oldRegion, RegionCoord newRegion, bool isInRegion)
    {
        Regions.tryGetPoint(newRegion.x, newRegion.y, out Vector3 regionPos);
        foreach (AnimalSpawnpoint spawn in new List<AnimalSpawnpoint>(Labels.Keys))
        {
            if ((regionPos - spawn.point).sqrMagnitude >= DistanceMax * DistanceMax + 10f)
                RemoveLabel(spawn);
        }

        foreach (AnimalSpawnpoint spawn in LevelAnimals.spawns)
        {
            if ((regionPos - spawn.point).sqrMagnitude < DistanceMax * DistanceMax + 10f && !Labels.ContainsKey(spawn))
                CreateLabel(spawn, GetText(spawn));
        }
    }
    protected override void Opened()
    {
        base.Opened();
        OnRegionUpdated(default, MovementUtil.MainCameraRegion, MovementUtil.MainCameraIsInRegion);
    }

    protected override void Closed()
    {
        ClearLabels();
        base.Closed();
    }

    private static string GetText(AnimalSpawnpoint point) => LevelAnimals.tables.Count > point.type ? LevelAnimals.tables[point.type].name : point.type + " - Null";
    protected override Vector3 GetPosition(AnimalSpawnpoint spawn) => spawn.point;
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
    private void OnSpawnMoved(AnimalSpawnpoint point, Vector3 fromPosition, Vector3 toPosition)
    {
        UpdateLabel(point);
    }
    public override void Dispose()
    {
        SpawnUtil.OnAnimalSpawnpointAdded -= OnSpawnAdded;
        SpawnUtil.OnAnimalSpawnpointRemoved -= OnSpawnRemoved;
        SpawnUtil.OnAnimalSpawnpointMoved -= OnSpawnMoved;
        base.Dispose();
    }
}
#endif