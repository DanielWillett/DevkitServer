#if CLIENT
using DanielWillett.UITools.API.Extensions;
using DanielWillett.UITools.API.Extensions.Members;
using DevkitServer.API.Devkit.Spawns;
using DevkitServer.Models;

namespace DevkitServer.Core.UI.Extensions;

[UIExtension(typeof(EditorSpawnsAnimalsUI))]
internal class EditorSpawnsAnimalsUIExtension : BaseEditorSpawnsUIExtensionNormalTables<AnimalSpawnpoint>
{
    private const float DistanceMax = 240f;
    // base properties wont patch right because generics share patches.

    [ExistingMember("animalButtons", FailureBehavior = ExistingMemberFailureBehavior.Ignore)]
    protected ISleekButton[]? Assets2 { get; }

    [ExistingMember("tableButtons", FailureBehavior = ExistingMemberFailureBehavior.Ignore)]
    protected ISleekButton[]? Tables2 { get; }

    [ExistingMember("tierButtons", FailureBehavior = ExistingMemberFailureBehavior.Ignore)]
    protected ISleekButton[]? Tiers2 { get; }
    protected override ISleekButton[]? Assets => Assets2;
    protected override ISleekButton[]? Tables => Tables2;
    protected override ISleekButton[]? Tiers => Tiers2;
    protected override bool IsVisible
    {
        get => LevelVisibility.animalsVisible;
        set => LevelVisibility.animalsVisible = value;
    }

    public EditorSpawnsAnimalsUIExtension() : base(new Vector3(0f, 2.5f, 0f), 60f, DistanceMax, SpawnType.Animal)
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
    protected override bool CheckLabelAlive(AnimalSpawnpoint spawn) => spawn.node != null;
    protected override bool ShouldShow(AnimalSpawnpoint spawn)
    {
        Vector3 lclPos = MainCamera.instance.transform.position;
        return (lclPos - spawn.point).sqrMagnitude < DistanceMax * DistanceMax && LevelAnimals.tables.Count > spawn.type;
    }
    private void OnSpawnAdded(AnimalSpawnpoint point, int index)
    {
        if (IsVisible)
            CreateLabel(point, GetText(point));
    }
    private void OnSpawnRemoved(AnimalSpawnpoint point, int index)
    {
        RemoveLabel(point);
    }
    private void OnSpawnTableChanged(AnimalSpawnpoint point, int index)
    {
        UpdateLabel(point, GetText(point));
    }
    internal void OnSpawnMoved(AnimalSpawnpoint point, int index, Vector3 fromPosition, Vector3 toPosition)
    {
        UpdateLabel(point);
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
    public override void UpdateSpawnName(int index)
    {
        if (!SpawnTableUtil.TryGetSelectedTier(SpawnType.Animal, out SpawnTierIdentifier? id) || !id.HasValue)
            return;

        AnimalSpawn spawn = LevelAnimals.tables[id.Value.TableIndex].tiers[id.Value.TierIndex].table[index];
        Asset asset = SDG.Unturned.Assets.find(EAssetType.ANIMAL, spawn.animal);
        string str = asset?.FriendlyName ?? asset?.name ?? "?";
        UpdateSpawnName(spawn.animal + " " + str, index);
    }
    public override void UpdateTableColor()
    {
        AnimalTable table = LevelAnimals.tables[EditorSpawns.selectedAnimal];
        UpdateTableColor(table.color);
    }
    public override void UpdateTableId()
    {
        AnimalTable table = LevelAnimals.tables[EditorSpawns.selectedAnimal];
        UpdateTableId(table.tableID);
    }
    public override void UpdateTierName(int index)
    {
        AnimalTier tier = LevelAnimals.tables[EditorSpawns.selectedAnimal].tiers[index];

        bool isSelected = SpawnTableUtil.TryGetSelectedTier(SpawnType.Animal, out SpawnTierIdentifier? id) && id.HasValue && id.Value.TierIndex == index;

        UpdateTierName(tier.name, index, isSelected);
    }
    public override void UpdateTableName(int index, bool updateField)
    {
        AnimalTable table = LevelAnimals.tables[index];
        UpdateTableName(table.name, index, EditorSpawns.selectedAnimal == index, updateField);
    }
    public override void UpdateTierChance(int index, bool updateSlider)
    {
        AnimalTier tier = LevelAnimals.tables[EditorSpawns.selectedAnimal].tiers[index];
        UpdateTierChance(tier.chance, index, updateSlider);
    }
}
#endif