#if CLIENT
using DevkitServer.API.UI;
using DevkitServer.Models;
using DevkitServer.Util.Region;

namespace DevkitServer.Core.Extensions.UI;
[UIExtension(typeof(EditorSpawnsItemsUI))]
internal class EditorSpawnsItemsUIExtension : BaseEditorSpawnsUIExtension<ItemSpawnpoint>
{
    private const float DistanceMax = 48f;
    public EditorSpawnsItemsUIExtension() : base(new Vector3(0f, 0.5f, 0f), 16f, DistanceMax)
    {
        SpawnUtil.OnItemSpawnpointAdded += OnSpawnAdded;
        SpawnUtil.OnItemSpawnpointRemoved += OnSpawnRemoved;
        SpawnUtil.OnItemSpawnpointMoved += OnSpawnMoved;
    }
    protected override void OnRegionUpdated(RegionCoord oldRegion, RegionCoord newRegion, bool isInRegion)
    {
        foreach (ItemSpawnpoint spawn in new List<ItemSpawnpoint>(Labels.Keys))
        {
            if (!Regions.tryGetCoordinate(spawn.point, out byte x, out byte y) || !Regions.checkArea(newRegion.x, newRegion.y, x, y, 2))
                RemoveLabel(spawn);
        }
        foreach (ItemSpawnpoint spawn in LevelItems.spawns.CastFrom(newRegion, 2))
        {
            if (!Labels.ContainsKey(spawn))
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
    public override void Dispose()
    {
        SpawnUtil.OnItemSpawnpointAdded -= OnSpawnAdded;
        SpawnUtil.OnItemSpawnpointRemoved -= OnSpawnRemoved;
        SpawnUtil.OnItemSpawnpointMoved -= OnSpawnMoved;
        base.Dispose();
    }
}
#endif