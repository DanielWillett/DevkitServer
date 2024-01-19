#if CLIENT
using DevkitServer.API.Devkit.Spawns;
using DevkitServer.API.UI.Extensions;
using DevkitServer.API.UI.Extensions.Members;
using DevkitServer.Models;
using DevkitServer.Util.Region;

namespace DevkitServer.Core.UI.Extensions;
[UIExtension(typeof(EditorSpawnsItemsUI))]
internal class EditorSpawnsItemsUIExtension : BaseEditorSpawnsUIExtensionNormalTables<ItemSpawnpoint>
{
    private const float DistanceMax = 48f;
    private const int RegionDistance = 2;

    [ExistingMember("itemButtons", FailureBehavior = ExistingMemberFailureBehavior.IgnoreNoWarn)]
    // virtual properties wont patch right
    protected ISleekButton[]? Assets2 { get; }
    protected override ISleekButton[]? Assets => Assets2;
    protected override bool IsVisible
    {
        get => LevelVisibility.itemsVisible;
        set => LevelVisibility.itemsVisible = value;
    }
    public EditorSpawnsItemsUIExtension() : base(new Vector3(0f, 0.5f, 0f), 16f, DistanceMax, SpawnType.Item)
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
            if (spawnpoint.type == index)
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
    protected override Vector3 GetPosition(ItemSpawnpoint spawn) => spawn.node.position;
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
    internal void OnSpawnMoved(ItemSpawnpoint point, RegionIdentifier region, Vector3 fromPosition, Vector3 toPosition)
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

    public override void UpdateSpawnName(int index)
    {
        if (!SpawnTableUtil.TryGetSelectedTier(SpawnType.Item, out SpawnTierIdentifier? id) || !id.HasValue)
            return;

        ItemSpawn spawn = LevelItems.tables[id.Value.TableIndex].tiers[id.Value.TierIndex].table[index];
        Asset asset = SDG.Unturned.Assets.find(EAssetType.ITEM, spawn.item);
        string str = asset?.FriendlyName ?? asset?.name ?? "?";
        UpdateSpawnName(spawn.item + " " + str, index);
    }
    public override void UpdateTableColor()
    {
        ItemTable table = LevelItems.tables[EditorSpawns.selectedItem];
        UpdateTableColor(table.color);
    }
    public override void UpdateTableId()
    {
        ItemTable table = LevelItems.tables[EditorSpawns.selectedItem];
        UpdateTableId(table.tableID);
    }
    public override void UpdateTierName(int index)
    {
        ItemTier tier = LevelItems.tables[EditorSpawns.selectedItem].tiers[index];

        bool isSelected = SpawnTableUtil.TryGetSelectedTier(SpawnType.Item, out SpawnTierIdentifier? id) && id.HasValue && id.Value.TierIndex == index;

        UpdateTierName(tier.name, index, isSelected);
    }
    public override void UpdateTableName(int index, bool updateField)
    {
        ItemTable table = LevelItems.tables[index];
        UpdateTableName(table.name, index, EditorSpawns.selectedItem == index, updateField);
    }
    public override void UpdateTierChance(int index, bool updateSlider)
    {
        ItemTier tier = LevelItems.tables[EditorSpawns.selectedItem].tiers[index];
        UpdateTierChance(tier.chance, index, updateSlider);
    }
}
#endif