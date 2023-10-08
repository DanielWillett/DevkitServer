#if CLIENT
using DevkitServer.API.UI;
using DevkitServer.Models;
using DevkitServer.Util.Region;

namespace DevkitServer.Core.Extensions.UI;
[UIExtension(typeof(EditorSpawnsZombiesUI))]
internal class EditorSpawnsZombiesUIExtension : BaseEditorSpawnsUIExtension<ZombieSpawnpoint>
{
    private const float DistanceMax = 60f;
    private const int RegionDistance = 2;
    public EditorSpawnsZombiesUIExtension() : base(new Vector3(0f, 2.5f, 0f), 20f, DistanceMax)
    {
        SpawnUtil.OnZombieSpawnpointAdded += OnSpawnAdded;
        SpawnUtil.OnZombieSpawnpointRemoved += OnSpawnRemoved;
        SpawnUtil.OnZombieSpawnpointMoved += OnSpawnMoved;
        SpawnUtil.OnZombieSpawnTableChanged += OnSpawnTableChanged;
        SpawnTableUtil.OnZombieSpawnTableNameUpdated += OnNameUpdated;
    }

    private void OnNameUpdated(ZombieTable table, int index)
    {
        foreach (ZombieSpawnpoint spawnpoint in SpawnUtil.EnumerateZombieSpawns(MovementUtil.MainCameraRegion, RegionDistance))
        {
            if (spawnpoint.type == index)
                UpdateLabel(spawnpoint, table.name);
        }
    }
    protected override void OnRegionUpdated(RegionCoord oldRegion, RegionCoord newRegion, bool isInRegion)
    {
        foreach (ZombieSpawnpoint spawn in new List<ZombieSpawnpoint>(Labels.Keys))
        {
            if (!Regions.tryGetCoordinate(spawn.point, out byte x, out byte y) || !Regions.checkArea(newRegion.x, newRegion.y, x, y, RegionDistance))
                RemoveLabel(spawn);
        }
        foreach (ZombieSpawnpoint spawn in LevelZombies.spawns.CastFrom(newRegion, RegionDistance))
        {
            if (!Labels.ContainsKey(spawn))
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

    private static string GetText(ZombieSpawnpoint point) => LevelZombies.tables.Count > point.type ? LevelZombies.tables[point.type].name : point.type + " - Null";
    protected override Vector3 GetPosition(ZombieSpawnpoint spawn) => spawn.point;
    protected override bool ShouldShow(ZombieSpawnpoint spawn)
    {
        Vector3 lclPos = MainCamera.instance.transform.position;
        return (lclPos - spawn.point).sqrMagnitude < DistanceMax * DistanceMax && LevelZombies.tables.Count > spawn.type;
    }


    private void OnSpawnAdded(ZombieSpawnpoint point, RegionIdentifier region)
    {
        CreateLabel(point, GetText(point));
    }
    private void OnSpawnRemoved(ZombieSpawnpoint point, RegionIdentifier region)
    {
        RemoveLabel(point);
    }
    private void OnSpawnMoved(ZombieSpawnpoint point, RegionIdentifier region, Vector3 fromPosition, Vector3 toPosition)
    {
        UpdateLabel(point);
    }
    private void OnSpawnTableChanged(ZombieSpawnpoint point, RegionIdentifier region)
    {
        UpdateLabel(point, GetText(point));
    }
    protected override void OnDestroyed()
    {
        SpawnUtil.OnZombieSpawnpointAdded -= OnSpawnAdded;
        SpawnUtil.OnZombieSpawnpointRemoved -= OnSpawnRemoved;
        SpawnUtil.OnZombieSpawnpointMoved -= OnSpawnMoved;
        SpawnUtil.OnZombieSpawnTableChanged -= OnSpawnTableChanged;
        SpawnTableUtil.OnZombieSpawnTableNameUpdated -= OnNameUpdated;
        base.OnDestroyed();
    }
}
#endif