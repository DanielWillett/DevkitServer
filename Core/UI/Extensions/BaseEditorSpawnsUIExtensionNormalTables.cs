#if CLIENT
using DevkitServer.API.Devkit.Spawns;
using DevkitServer.API.UI.Extensions.Members;

namespace DevkitServer.Core.UI.Extensions;

internal abstract class BaseEditorSpawnsUIExtensionNormalTables<T>(Vector3 offset, float distanceCurveMin, float distanceCurveMax, SpawnType spawnType)
    : BaseEditorSpawnsUIExtension<T>(offset, distanceCurveMin, distanceCurveMax, spawnType) where T : class
{
    protected abstract ISleekButton[]? Assets { get; }
    protected abstract ISleekButton[]? Tiers { get; }
    protected abstract ISleekButton[]? Tables { get; }

    [ExistingMember("selectedBox", FailureBehavior = ExistingMemberFailureBehavior.IgnoreNoWarn)]
    protected readonly ISleekBox? SelectedBox;

    [ExistingMember("tableNameField", FailureBehavior = ExistingMemberFailureBehavior.IgnoreNoWarn)]
    protected readonly ISleekField? TableNameField;

    [ExistingMember("tableIDField", FailureBehavior = ExistingMemberFailureBehavior.IgnoreNoWarn)]
    protected readonly ISleekUInt16Field? TableIdField;

    [ExistingMember("tableColorPicker", FailureBehavior = ExistingMemberFailureBehavior.IgnoreNoWarn)]
    protected readonly SleekColorPicker? TableColorPicker;

    [ExistingMember("tierNameField", FailureBehavior = ExistingMemberFailureBehavior.IgnoreNoWarn)]
    protected readonly ISleekField? TierName;
    protected void UpdateSpawnName(string name, int index)
    {
        ISleekButton[]? assets = Assets;
        if (assets == null)
            return;

        if (assets.Length > index && assets[index] != null)
            assets[index].Text = name;
        else
            SpawnTableUtil.UpdateUISelection(SpawnType);
    }
    protected void UpdateTierName(string name, int index, bool isSelected)
    {
        bool fail = false;

        if (isSelected)
        {
            ISleekField? tierName = TierName;
            if (tierName != null)
                tierName.Text = name;
            else fail = true;
        }

        if (!fail && Tiers is { } tiers)
        {
            if (tiers.Length > index && tiers[index] != null)
                tiers[index].Text = name;
            else fail = true;
        }

        if (fail)
            SpawnTableUtil.UpdateUISelection(SpawnType);
    }
    protected void UpdateTierChance(float chance, int index, bool updateSlider)
    {
        bool fail = false;
        if (Tiers is { } tiers)
        {
            if (tiers.Length > index && tiers[index] != null)
            {
                try
                {
                    ISleekElement? child = tiers[index].GetChildAtIndex(0);
                    if (child is ISleekSlider slider)
                    {
                        if (updateSlider)
                            slider.Value = chance;
                        
                        slider.UpdateLabel(Mathf.RoundToInt(chance * 100f) + "%");
                    }
                    else
                        fail = true;
                }
                catch (ArgumentOutOfRangeException)
                {
                    fail = true;
                }
            }
            else fail = true;
        }

        if (fail)
            SpawnTableUtil.UpdateUISelection(SpawnType);
    }
    protected void UpdateTableName(string name, int index, bool isSelected, bool updateField)
    {
        bool fail = false;

        if (isSelected)
        {
            ISleekBox? selectedBox = SelectedBox;
            if (selectedBox != null)
                selectedBox.Text = name;
            else fail = true;
            if (!fail && updateField)
            {
                ISleekField? tableNameField = TableNameField;
                if (tableNameField != null)
                    tableNameField.Text = name;
                else fail = true;
            }
        }

        if (!fail && Tables is { } tables && tables.Length > index && tables[index] != null)
            tables[index].Text = index + " " + name;
        else fail = true;

        if (fail)
            SpawnTableUtil.UpdateUISelection(SpawnType);
    }
    protected void UpdateTableColor(Color color)
    {
        SleekColorPicker? tableColorPicker = TableColorPicker;

        if (tableColorPicker != null)
            tableColorPicker.state = color;
        else
            SpawnTableUtil.UpdateUISelection(SpawnType);
    }
    protected void UpdateTableId(ushort id)
    {
        ISleekUInt16Field? tableIdField = TableIdField;

        if (tableIdField != null)
            tableIdField.Value = id;
        else
            SpawnTableUtil.UpdateUISelection(SpawnType);
    }

    public abstract void UpdateTierName(int index);
    public abstract void UpdateTierChance(int index, bool updateSlider);
    public abstract void UpdateSpawnName(int index);
    public abstract void UpdateTableName(int index, bool updateField);
    public abstract void UpdateTableColor();
    public abstract void UpdateTableId();
}
#endif