#if CLIENT
using DevkitServer.API.Devkit.Spawns;
using DevkitServer.API.UI.Extensions;
using DevkitServer.API.UI.Extensions.Members;
using DevkitServer.Models;
using DevkitServer.Util.Region;

namespace DevkitServer.Core.UI.Extensions;
[UIExtension(typeof(EditorSpawnsZombiesUI))]
internal class EditorSpawnsZombiesUIExtension : BaseEditorSpawnsUIExtension<ZombieSpawnpoint>
{
    private const float DistanceMax = 60f;
    private const int RegionDistance = 2;
    protected override bool IsVisible
    {
        get => LevelVisibility.zombiesVisible;
        set => LevelVisibility.zombiesVisible = value;
    }

    [ExistingMember("tableButtons", FailureBehavior = ExistingMemberFailureBehavior.IgnoreNoWarn)]
    protected ISleekButton[]? Tables { get; }

    [ExistingMember("slotButtons", FailureBehavior = ExistingMemberFailureBehavior.IgnoreNoWarn)]
    protected ISleekButton[]? Tiers { get; }

    [ExistingMember("clothButtons", FailureBehavior = ExistingMemberFailureBehavior.IgnoreNoWarn)]
    protected ISleekButton[]? Assets { get; }

    [ExistingMember("selectedBox", FailureBehavior = ExistingMemberFailureBehavior.IgnoreNoWarn)]
    protected readonly ISleekBox? SelectedBox;

    [ExistingMember("tableNameField", FailureBehavior = ExistingMemberFailureBehavior.IgnoreNoWarn)]
    protected readonly ISleekField? TableNameField;

    [ExistingMember("lootIDField", FailureBehavior = ExistingMemberFailureBehavior.IgnoreNoWarn)]
    protected readonly ISleekUInt16Field? LootIdField;

    [ExistingMember("tableColorPicker", FailureBehavior = ExistingMemberFailureBehavior.IgnoreNoWarn)]
    protected readonly SleekColorPicker? TableColorPicker;

    [ExistingMember("megaToggle", FailureBehavior = ExistingMemberFailureBehavior.IgnoreNoWarn)]
    protected readonly ISleekToggle? MegaToggle;

    [ExistingMember("healthField", FailureBehavior = ExistingMemberFailureBehavior.IgnoreNoWarn)]
    protected readonly ISleekUInt16Field? HealthField;

    [ExistingMember("damageField", FailureBehavior = ExistingMemberFailureBehavior.IgnoreNoWarn)]
    protected readonly ISleekUInt8Field? DamageField;

    [ExistingMember("lootIndexField", FailureBehavior = ExistingMemberFailureBehavior.IgnoreNoWarn)]
    protected readonly ISleekUInt8Field? LootIndexField;

    [ExistingMember("xpField", FailureBehavior = ExistingMemberFailureBehavior.IgnoreNoWarn)]
    protected readonly ISleekUInt32Field? XPField;

    [ExistingMember("regenField", FailureBehavior = ExistingMemberFailureBehavior.IgnoreNoWarn)]
    protected readonly ISleekFloat32Field? RegenField;

    [ExistingMember("difficultyGUIDField", FailureBehavior = ExistingMemberFailureBehavior.IgnoreNoWarn)]
    protected readonly ISleekField? DifficultyAssetField;

    public EditorSpawnsZombiesUIExtension() : base(new Vector3(0f, 2.125f, 0f), 20f, DistanceMax, SpawnType.Zombie)
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
    protected override Vector3 GetPosition(ZombieSpawnpoint spawn) => spawn.node.position;
    protected override bool CheckLabelAlive(ZombieSpawnpoint spawn) => spawn.node != null;
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
    internal void OnSpawnMoved(ZombieSpawnpoint point, RegionIdentifier region, Vector3 fromPosition, Vector3 toPosition)
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
    
    public void UpdateSpawnName(int index)
    {
        ISleekButton[]? assets = Assets;
        if (assets == null)
            return;

        if (assets.Length <= index || assets[index] == null)
        {
            EditorSpawnsZombiesUI.updateSelection();
            return;
        }

        if (!SpawnTableUtil.TryGetSelectedTier(SpawnType.Zombie, out SpawnTierIdentifier? id) || !id.HasValue)
            return;

        ZombieCloth cloth = LevelZombies.tables[id.Value.TableIndex].slots[id.Value.TierIndex].table[index];
        Asset asset = SDG.Unturned.Assets.find(EAssetType.ITEM, cloth.item);
        string name = asset?.FriendlyName ?? asset?.name ?? "?";
        assets[index].Text = name;
    }
    public void UpdateTableName(int index, bool updateField)
    {
        ZombieTable table = LevelZombies.tables[index];
        bool isSelected = EditorSpawns.selectedZombie == index;
        bool fail = false;

        if (isSelected)
        {
            ISleekBox? selectedBox = SelectedBox;
            if (selectedBox != null)
                selectedBox.Text = table.name;
            else fail = true;
            if (updateField)
            {
                ISleekField? tableNameField = TableNameField;
                if (tableNameField != null)
                    tableNameField.Text = table.name;
                else fail = true;
            }
        }

        ISleekButton[]? tables = Tables;
        if (tables != null && tables.Length > index && tables[index] != null)
            tables[index].Text = index + " " + table.name;
        else fail = true;

        if (fail)
        {
            if (isSelected)
                EditorSpawnsZombiesUI.updateSelection();
            EditorSpawnsZombiesUI.updateTables();
        }
    }
    public void UpdateSlotChance(int index, bool updateSlider)
    {
        ZombieSlot slot = LevelZombies.tables[EditorSpawns.selectedZombie].slots[index];

        ISleekButton[]? tiers = Tiers;

        bool fail = false;
        if (tiers != null)
        {
            if (tiers.Length > index && tiers[index] != null)
            {
                try
                {
                    ISleekElement? child = tiers[index].GetChildAtIndex(0);
                    if (child is ISleekSlider slider)
                    {
                        if (updateSlider)
                            slider.Value = slot.chance;
                        slider.UpdateLabel(Mathf.RoundToInt(slot.chance * 100f) + "%");
                    }
                    else fail = true;
                }
                catch (ArgumentOutOfRangeException)
                {
                    fail = true;
                }
            }
            else fail = true;
        }

        if (fail)
            EditorSpawnsZombiesUI.updateSelection();
    }
    public void UpdateTableColor()
    {
        ZombieTable table = LevelZombies.tables[EditorSpawns.selectedZombie];

        SleekColorPicker? tableColorPicker = TableColorPicker;

        if (tableColorPicker != null)
            tableColorPicker.state = table.color;
        else
            EditorSpawnsZombiesUI.updateSelection();
    }
    public void UpdateIsMega()
    {
        ZombieTable table = LevelZombies.tables[EditorSpawns.selectedZombie];

        ISleekToggle? megaToggle = MegaToggle;

        if (megaToggle != null)
            megaToggle.Value = table.isMega;
        else
            EditorSpawnsZombiesUI.updateSelection();
    }
    public void UpdateHealth()
    {
        ZombieTable table = LevelZombies.tables[EditorSpawns.selectedZombie];

        ISleekUInt16Field? healthField = HealthField;

        if (healthField != null)
            healthField.Value = table.health;
        else
            EditorSpawnsZombiesUI.updateSelection();
    }
    public void UpdateDamage()
    {
        ZombieTable table = LevelZombies.tables[EditorSpawns.selectedZombie];

        ISleekUInt8Field? damageField = DamageField;

        if (damageField != null)
            damageField.Value = table.damage;
        else
            EditorSpawnsZombiesUI.updateSelection();
    }
    public void UpdateLootIndex()
    {
        ZombieTable table = LevelZombies.tables[EditorSpawns.selectedZombie];

        ISleekUInt8Field? lootIndexField = LootIndexField;

        if (lootIndexField != null)
            lootIndexField.Value = table.lootIndex;
        else
            EditorSpawnsZombiesUI.updateSelection();
    }
    public void UpdateLootId()
    {
        ZombieTable table = LevelZombies.tables[EditorSpawns.selectedZombie];

        ISleekUInt16Field? lootIdField = LootIdField;

        if (lootIdField != null)
            lootIdField.Value = table.lootID;
        else
            EditorSpawnsZombiesUI.updateSelection();
    }
    public void UpdateXP()
    {
        ZombieTable table = LevelZombies.tables[EditorSpawns.selectedZombie];

        ISleekUInt32Field? xpField = XPField;

        if (xpField != null)
            xpField.Value = table.xp;
        else
            EditorSpawnsZombiesUI.updateSelection();
    }
    public void UpdateRegen()
    {
        ZombieTable table = LevelZombies.tables[EditorSpawns.selectedZombie];

        ISleekFloat32Field? regenField = RegenField;

        if (regenField != null)
            regenField.Value = table.regen;
        else
            EditorSpawnsZombiesUI.updateSelection();
    }
    public void UpdateDifficultyAsset()
    {
        ZombieTable table = LevelZombies.tables[EditorSpawns.selectedZombie];

        ISleekField? difficultyAssetField = DifficultyAssetField;

        if (difficultyAssetField != null)
            difficultyAssetField.Text = table.difficultyGUID;
        else
            EditorSpawnsZombiesUI.updateSelection();
    }
}
#endif