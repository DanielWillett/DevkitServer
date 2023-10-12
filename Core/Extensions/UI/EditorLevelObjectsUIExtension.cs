#if CLIENT
using DevkitServer.API.UI;
using DevkitServer.Configuration;
using DevkitServer.Patches;
using DevkitServer.Players.UI;
using HarmonyLib;
using System.Reflection;
using DevkitServer.Players;

namespace DevkitServer.Core.Extensions.UI;
[UIExtension(typeof(EditorLevelObjectsUI))]
internal class EditorLevelObjectsUIExtension : UIExtension
{
    public static KeyCode EditToggleKey = KeyCode.F8;
    private const int Size = 158;
    private static bool _patched;
#nullable disable
    [ExistingUIMember("container")]
    private readonly SleekFullscreenBox _container;

    [ExistingUIMember("assetsScrollBox")]
    private readonly SleekList<Asset> _assetsScrollBox;

    [ExistingUIMember("selectedBox")]
    private ISleekBox SelectedBox { get; }

    private readonly ISleekBox _displayTitle;
    private readonly ISleekImage _preview;
    private bool _isGeneratingIcon;
    private bool _editorActive;
    
    private readonly ISleekToggle _isEditingToggle;
    private readonly ISleekButton _saveEditButton;
    private readonly ISleekButton _saveNewEditButton;
    private readonly ISleekButton _gotoOffsetButton;
    private readonly ISleekField _offsetField;
    private readonly ISleekLabel _editHint;

    internal EditorLevelObjectsUIExtension()
    {
        if (!DevkitServerConfig.Config.EnableObjectUIExtension)
            return;

        ISleekBox displayBox = Glazier.Get().CreateBox();
        displayBox.PositionScale_X = 1f;
        displayBox.PositionScale_Y = 1f;
        displayBox.PositionOffset_X = _assetsScrollBox.PositionOffset_X - (Size + 30);
        displayBox.PositionOffset_Y = -Size - 20;
        displayBox.SizeOffset_X = Size + 20;
        displayBox.SizeOffset_Y = Size + 20;
        _container.AddChild(displayBox);

        _displayTitle = Glazier.Get().CreateBox();
        _displayTitle.PositionScale_X = 1f;
        _displayTitle.PositionScale_Y = 1f;
        _displayTitle.PositionOffset_X = _assetsScrollBox.PositionOffset_X - (Size + 30);
        _displayTitle.PositionOffset_Y = -Size - 60;
        _displayTitle.SizeOffset_X = Size + 20;
        _displayTitle.SizeOffset_Y = 30;
        _displayTitle.Text = DevkitServerModule.MainLocalization.Translate("NoAssetSelected");

        _container.AddChild(_displayTitle);

        _preview = Glazier.Get().CreateImage();
        _preview.SizeScale_X = 0f;
        _preview.SizeScale_Y = 0f;
        _preview.PositionScale_X = 0.5f;
        _preview.PositionScale_Y = 0.5f;
        _preview.SizeOffset_X = Size;
        _preview.SizeOffset_Y = Size;
        _preview.ShouldDestroyTexture = true;
        displayBox.AddChild(_preview);

        _editHint = Glazier.Get().CreateLabel();
        _editHint.TextContrastContext = ETextContrastContext.ColorfulBackdrop;
        _editHint.Text = DevkitServerModule.MainLocalization.Translate("ObjectIconEditorToggleHint", MenuConfigurationControlsUI.getKeyCodeText(EditToggleKey));
        _editHint.PositionScale_X = 1f;
        _editHint.PositionScale_Y = 1f;
        _editHint.PositionOffset_X = _assetsScrollBox.PositionOffset_X - (Size + 30);
        _editHint.PositionOffset_Y = -20;
        _editHint.TextAlignment = TextAnchor.MiddleCenter;
        _editHint.TextColor = new SleekColor(ESleekTint.FOREGROUND);
        _editHint.SizeOffset_X = Size + 20;
        _editHint.SizeOffset_Y = 20;

        _container.AddChild(_editHint);

        _editorActive = false;

        _isEditingToggle = Glazier.Get().CreateToggle();
        _isEditingToggle.PositionScale_X = 1f;
        _isEditingToggle.PositionScale_Y = 1f;
        _isEditingToggle.PositionOffset_X = _displayTitle.PositionOffset_X - 30;
        _isEditingToggle.PositionOffset_Y = -Size - 55;
        _isEditingToggle.SizeOffset_X = 20;
        _isEditingToggle.SizeOffset_Y = 20;
        _isEditingToggle.AddLabel(DevkitServerModule.MainLocalization.Translate("ObjectIconEditorToggle"), new SleekColor(ESleekTint.FOREGROUND).Get(), ESleekSide.LEFT);
        _isEditingToggle.SideLabel.TextContrastContext = ETextContrastContext.ColorfulBackdrop;
        _isEditingToggle.IsVisible = false;
        _isEditingToggle.OnValueChanged += OnToggled;
        _container.AddChild(_isEditingToggle);

        _saveEditButton = Glazier.Get().CreateButton();
        _saveEditButton.PositionScale_X = 1f;
        _saveEditButton.PositionScale_Y = 1f;
        _saveEditButton.PositionOffset_X = _isEditingToggle.PositionOffset_X - Size + 10;
        _saveEditButton.PositionOffset_Y = -Size - 25;
        _saveEditButton.SizeOffset_X = Size / 2;
        _saveEditButton.SizeOffset_Y = 30;
        _saveEditButton.Text = DevkitServerModule.MainLocalization.Translate("ObjectIconEditorSave");
        _saveEditButton.OnClicked += OnSaveEdit;
        _saveEditButton.IsVisible = false;
        _container.AddChild(_saveEditButton);

        _saveNewEditButton = Glazier.Get().CreateButton();
        _saveNewEditButton.PositionScale_X = 1f;
        _saveNewEditButton.PositionScale_Y = 1f;
        _saveNewEditButton.PositionOffset_X = _isEditingToggle.PositionOffset_X + (Size + 30) / 2 - Size;
        _saveNewEditButton.PositionOffset_Y = -Size - 25;
        _saveNewEditButton.SizeOffset_X = Size / 2;
        _saveNewEditButton.SizeOffset_Y = 30;
        _saveNewEditButton.Text = DevkitServerModule.MainLocalization.Translate("ObjectIconEditorSaveNew");
        _saveNewEditButton.OnClicked += OnSaveNewEdit;
        _saveNewEditButton.IsVisible = false;
        _container.AddChild(_saveNewEditButton);

        _offsetField = Glazier.Get().CreateStringField();
        _offsetField.PositionScale_X = 1f;
        _offsetField.PositionScale_Y = 1f;
        _offsetField.PositionOffset_X = _saveEditButton.PositionOffset_X;
        _offsetField.PositionOffset_Y = _saveEditButton.PositionOffset_Y + 40;
        _offsetField.SizeOffset_X = 3 * Size / 4 - 5;
        _offsetField.SizeOffset_Y = 30;
        _offsetField.TooltipText = DevkitServerModule.MainLocalization.Translate("ObjectIconEditorOffsetAssetHint");
        _offsetField.IsVisible = false;
        _container.AddChild(_offsetField);

        _gotoOffsetButton = Glazier.Get().CreateButton();
        _gotoOffsetButton.PositionScale_X = 1f;
        _gotoOffsetButton.PositionScale_Y = 1f;
        _gotoOffsetButton.PositionOffset_X = _offsetField.PositionOffset_X + _offsetField.SizeOffset_X + 10;
        _gotoOffsetButton.PositionOffset_Y = _offsetField.PositionOffset_Y;
        _gotoOffsetButton.SizeOffset_X = Size / 4;
        _gotoOffsetButton.SizeOffset_Y = 30;
        _gotoOffsetButton.Text = DevkitServerModule.MainLocalization.Translate("ObjectIconEditorOffsetAssetButton");
        _gotoOffsetButton.IsVisible = false;
        _gotoOffsetButton.OnClicked += OnClickedGotoAsset;
        _gotoOffsetButton.OnRightClicked += OnRightClickedGotoAsset;
        _container.AddChild(_gotoOffsetButton);

        UpdateSelectedObject();
        if (!_patched)
            Patch();
    }
#nullable restore

    private void OnRightClickedGotoAsset(ISleekElement button)
    {
        _offsetField.Text = LevelObjectUtil.SelectedAsset is not ObjectAsset asset ? string.Empty : asset.GUID.ToString("N");
    }
    private void OnClickedGotoAsset(ISleekElement button)
    {
        if (!Guid.TryParse(_offsetField.Text, out Guid guid) || Assets.find(guid) is not ObjectAsset asset)
        {
            _offsetField.Text = string.Empty;
            return;
        }
        
        if (LevelObjectUtil.SelectedAsset is not ObjectAsset selectedAsset)
            return;
        
        List<EditorSelection> selections = LevelObjectUtil.EditorObjectSelection;
        LevelObject? target = null;
        foreach (EditorSelection selection in selections)
        {
            if (!LevelObjectUtil.TryFindObject(selection.transform, out LevelObject obj) || obj.GUID != selectedAsset.GUID)
                continue;

            if (target == null)
                target = obj;
            else
            {
                target = null;
                break;
            }
        }

        if (target == null)
            return;

        IconGenerator.ObjectIconMetrics metrics = IconGenerator.GetObjectIconMetrics(asset);
        IconGenerator.GetCameraPositionAndRotation(in metrics, target.transform, out Vector3 position, out Quaternion rotation);
        UserInput.SetEditorTransform(position, rotation);
    }

    private void OnToggled(ISleekToggle toggle, bool state)
    {
        if (!_editorActive)
            state = false;

        _saveNewEditButton.IsVisible = state;
        _saveEditButton.IsVisible = state;
        _offsetField.IsVisible = state;
        _gotoOffsetButton.IsVisible = state;
        _editHint.IsVisible = !state;
    }

    public bool EditorActive
    {
        get => _editorActive;
        private set
        {
            _editorActive = value;
            if (!value)
            {
                OnToggled(_isEditingToggle, false);
                _isEditingToggle.Value = false;
            }

            _isEditingToggle.IsVisible = value;
        }
    }
    private void OnSaveEdit(ISleekElement button)
    {
        Asset? asset = LevelObjectUtil.SelectedAsset;
        if (asset == null)
            return;
        ObjectIconPresets.SaveEditCache(false);
        UpdateSelectedObject();
    }
    private void OnSaveNewEdit(ISleekElement button)
    {
        Asset? asset = LevelObjectUtil.SelectedAsset;
        if (asset == null)
            return;
        ObjectIconPresets.SaveEditCache(true);
        UpdateSelectedObject();
    }
    internal static void OnUpdate()
    {
        EditorLevelObjectsUIExtension? inst = UIExtensionManager.GetInstance<EditorLevelObjectsUIExtension>();
        if (inst == null)
        {
            return;
        }

        if (InputEx.GetKeyDown(EditToggleKey))
            inst.EditorActive = !inst.EditorActive;
        if (inst._isGeneratingIcon)
        {
            return;
        }
        if (!inst._isEditingToggle.Value || LevelObjectUtil.SelectedAsset is not ObjectAsset asset)
        {
            goto clear;
        }
        LevelObject? selectedObject = null;
        foreach (EditorSelection selection in LevelObjectUtil.EditorObjectSelection)
        {
            if (!LevelObjectUtil.TryFindObject(selection.transform, out LevelObject lvlObj) || lvlObj.asset.GUID != asset.GUID)
            {
                continue;
            }
            if (selectedObject != null)
            {
                goto clear;
            }
            selectedObject = lvlObj;
        }

        if (selectedObject == null)
        {
            goto clear;
        }
        
        ObjectIconPresets.UpdateEditCache(selectedObject, asset);
        inst.UpdateSelectedObject();
        return;

        clear:
        if (ObjectIconPresets.ActivelyEditing != null)
        {
            ObjectIconPresets.ClearEditCache();
            inst.UpdateSelectedObject();
        }
    }
    public static void UpdateSelection(ObjectAsset? levelObject, ItemAsset? buildable)
    {
        if (levelObject == null && buildable == null)
            ObjectIconPresets.ClearEditCache();
        EditorLevelObjectsUIExtension? inst = UIExtensionManager.GetInstance<EditorLevelObjectsUIExtension>();
        try
        {
            ISleekBox? box = inst?.SelectedBox;
            if (box == null)
                return;
            if (levelObject == null && buildable is not ItemBarricadeAsset and not ItemStructureAsset)
                box.Text = string.Empty;
            else if (levelObject != null)
                box.Text = levelObject.FriendlyName;
            else
                box.Text = buildable!.FriendlyName;

            if (inst != null && DevkitServerConfig.Config.EnableObjectUIExtension)
                inst.UpdateSelectedObject();
        }
        catch (Exception ex)
        {
            Logger.LogError("Error updating selection.");
            Logger.LogError(ex);
        }
    }

    internal void UpdateSelectedObject()
    {
        _preview.Texture = null;
        Asset? asset = LevelObjectUtil.SelectedAsset;
        if (asset != null)
        {
            _isGeneratingIcon = true;
            if (asset is ObjectNPCAsset)
                OnIconReady(asset, null, false);
            else
                IconGenerator.GetIcon(asset, Size, Size, OnIconReady);
            string text = asset.FriendlyName;
            if (asset is ObjectAsset obj)
                text += " (" + obj.type switch
                {
                    EObjectType.LARGE => "Large",
                    EObjectType.MEDIUM => "Medium",
                    EObjectType.SMALL => "Small",
                    EObjectType.DECAL => "Decal",
                    EObjectType.NPC => "NPC",
                    _ => "Object"
                } + ")";
            else if (asset is ItemStructureAsset)
                text += " (Structure)";
            else if (asset is ItemBarricadeAsset)
                text += " (Barricade)";
            _displayTitle.Text = text;
            Color rarityColor = asset is ItemAsset item ? ItemTool.getRarityColorUI(item.rarity) : Color.white;
            _displayTitle.BackgroundColor = SleekColor.BackgroundIfLight(rarityColor);
            _displayTitle.TextColor = rarityColor;
        }
        else
        {
            _displayTitle.TextColor = ESleekTint.FOREGROUND;
            _displayTitle.Text = DevkitServerModule.MainLocalization.Translate("NoAssetSelected");
        }
    }

    private void OnIconReady(Asset asset, Texture? texture, bool destroy)
    {
        _isGeneratingIcon = false;
        if (EditorObjects.selectedItemAsset != asset && EditorObjects.selectedObjectAsset != asset)
            return;
        _preview.Texture = texture;
        _preview.ShouldDestroyTexture = destroy;
        if (texture != null)
        {
            float aspect = (float)texture.width / texture.height;
            if (Mathf.Approximately(aspect, 1f))
            {
                _preview.SizeOffset_X = Size;
                _preview.SizeOffset_Y = Size;
            }
            else if (aspect > 1f)
            {
                _preview.SizeOffset_X = Size;
                _preview.SizeOffset_Y = Mathf.RoundToInt(Size / aspect);
            }
            else
            {
                _preview.SizeOffset_X = Mathf.RoundToInt(Size * aspect);
                _preview.SizeOffset_Y = Size;
            }
        }
        else
        {
            _preview.SizeOffset_X = Size;
            _preview.SizeOffset_Y = Size;
        }

        _preview.PositionOffset_X = -Size / 2;
        _preview.PositionOffset_Y = -Size / 2;
    }
    private static void Patch()
    {
        MethodInfo? target = typeof(EditorLevelObjectsUI).GetMethod("onClickedAssetButton", BindingFlags.Static | BindingFlags.NonPublic);
        if (target == null)
        {
            Logger.LogError("Failed to find method: EditorLevelObjectsUI.onClickedAssetButton", method: "LVL OBJ UI EXT");
            return;
        }

        MethodInfo method = new Action<bool>(OnUpdatedElement).Method;
        PatchesMain.Patcher.Patch(target, finalizer: new HarmonyMethod(method));
        _patched = true;
    }
    private static void OnUpdatedElement(bool __runOriginal)
    {
        if (!__runOriginal || !DevkitServerConfig.Config.EnableObjectUIExtension)
            return;

        EditorLevelObjectsUIExtension? inst = UIExtensionManager.GetInstance<EditorLevelObjectsUIExtension>();
        inst?.UpdateSelectedObject();
    }
}
#endif