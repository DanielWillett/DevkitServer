#if CLIENT
using DevkitServer.API.UI;
using DevkitServer.Models;
using DevkitServer.Util.Region;

namespace DevkitServer.Core.Extensions.UI;
[UIExtension(typeof(EditorSpawnsItemsUI))]
internal class EditorSpawnsItemsUIExtension : BaseEditorSpawnsUIExtension<ItemSpawnpoint>
{
    private const float DistanceMax = 48f;
    private const int RegionDistance = 2;
    public EditorSpawnsItemsUIExtension() : base(new Vector3(0f, 0.5f, 0f), 16f, DistanceMax)
    {
        SpawnUtil.OnItemSpawnpointAdded += OnSpawnAdded;
        SpawnUtil.OnItemSpawnpointRemoved += OnSpawnRemoved;
        SpawnUtil.OnItemSpawnpointMoved += OnSpawnMoved;
        SpawnUtil.OnItemSpawnTableChanged += OnSpawnTableChanged;
        SpawnTableUtil.OnItemSpawnTableNameUpdated += OnNameUpdated;
    }

    private void OnNameUpdated(ItemTable table, int index)
    {
        foreach (ItemSpawnpoint spawnpoint in SpawnUtil.EnumerateItemSpawns(MovementUtil.MainCameraRegion, RegionDistance))
        {
            UpdateLabel(spawnpoint, table.name);
        }
    }

    protected override void OnRegionUpdated(RegionCoord oldRegion, RegionCoord newRegion, bool isInRegion)
    {
        foreach (ItemSpawnpoint spawn in new List<ItemSpawnpoint>(Labels.Keys))
        {
            if (!Regions.tryGetCoordinate(spawn.point, out byte x, out byte y) || !Regions.checkArea(newRegion.x, newRegion.y, x, y, RegionDistance))
                RemoveLabel(spawn);
        }
        foreach (ItemSpawnpoint spawn in LevelItems.spawns.CastFrom(newRegion, RegionDistance))
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

    private static string GetText(ItemSpawnpoint point) => LevelItems.tables.Count > point.type ? LevelItems.tables[point.type].name : point.type + " - Null";
    protected override Vector3 GetPosition(ItemSpawnpoint spawn) => spawn.point;
    protected override bool ShouldShow(ItemSpawnpoint spawn)
    {
        Vector3 lclPos = MainCamera.instance.transform.position;
        return (lclPos - spawn.point).sqrMagnitude < DistanceMax * DistanceMax && LevelItems.tables.Count > spawn.type;
    }


    private void OnSpawnAdded(ItemSpawnpoint point, RegionIdentifier region)
    {
        CreateLabel(point, GetText(point));
    }
    private void OnSpawnRemoved(ItemSpawnpoint point, RegionIdentifier region)
    {
        RemoveLabel(point);
    }
    private void OnSpawnMoved(ItemSpawnpoint point, RegionIdentifier region, Vector3 fromPosition, Vector3 toPosition)
    {
        UpdateLabel(point);
    }
    private void OnSpawnTableChanged(ItemSpawnpoint point, RegionIdentifier region)
    {
        UpdateLabel(point, GetText(point));
    }
    protected override void OnDestroyed()
    {
        SpawnUtil.OnItemSpawnpointAdded -= OnSpawnAdded;
        SpawnUtil.OnItemSpawnpointRemoved -= OnSpawnRemoved;
        SpawnUtil.OnItemSpawnpointMoved -= OnSpawnMoved;
        SpawnUtil.OnItemSpawnTableChanged -= OnSpawnTableChanged;
        SpawnTableUtil.OnItemSpawnTableNameUpdated -= OnNameUpdated;
        base.OnDestroyed();
    }
}
#endif