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
        displayBox.positionScale_X = 1f;
        displayBox.positionScale_Y = 1f;
        displayBox.positionOffset_X = _assetsScrollBox.positionOffset_X - (Size + 30);
        displayBox.positionOffset_Y = -Size - 20;
        displayBox.sizeOffset_X = Size + 20;
        displayBox.sizeOffset_Y = Size + 20;
        _container.AddChild(displayBox);

        _displayTitle = Glazier.Get().CreateBox();
        _displayTitle.positionScale_X = 1f;
        _displayTitle.positionScale_Y = 1f;
        _displayTitle.positionOffset_X = _assetsScrollBox.positionOffset_X - (Size + 30);
        _displayTitle.positionOffset_Y = -Size - 60;
        _displayTitle.sizeOffset_X = Size + 20;
        _displayTitle.sizeOffset_Y = 30;
        _displayTitle.text = DevkitServerModule.MainLocalization.Translate("NoAssetSelected");

        _container.AddChild(_displayTitle);

        _preview = Glazier.Get().CreateImage();
        _preview.sizeScale_X = 0f;
        _preview.sizeScale_Y = 0f;
        _preview.positionScale_X = 0.5f;
        _preview.positionScale_Y = 0.5f;
        _preview.sizeOffset_X = Size;
        _preview.sizeOffset_Y = Size;
        _preview.shouldDestroyTexture = true;
        displayBox.AddChild(_preview);

        _editHint = Glazier.Get().CreateLabel();
        _editHint.shadowStyle = ETextContrastContext.ColorfulBackdrop;
        _editHint.text = DevkitServerModule.MainLocalization.Translate("ObjectIconEditorToggleHint", MenuConfigurationControlsUI.getKeyCodeText(EditToggleKey));
        _editHint.positionScale_X = 1f;
        _editHint.positionScale_Y = 1f;
        _editHint.positionOffset_X = _assetsScrollBox.positionOffset_X - (Size + 30);
        _editHint.positionOffset_Y = -20;
        _editHint.fontAlignment = TextAnchor.MiddleCenter;
        _editHint.textColor = new SleekColor(ESleekTint.FOREGROUND);
        _editHint.sizeOffset_X = Size + 20;
        _editHint.sizeOffset_Y = 20;

        _container.AddChild(_editHint);

        _editorActive = false;

        _isEditingToggle = Glazier.Get().CreateToggle();
        _isEditingToggle.positionScale_X = 1f;
        _isEditingToggle.positionScale_Y = 1f;
        _isEditingToggle.positionOffset_X = _displayTitle.positionOffset_X - 30;
        _isEditingToggle.positionOffset_Y = -Size - 55;
        _isEditingToggle.sizeOffset_X = 20;
        _isEditingToggle.sizeOffset_Y = 20;
        _isEditingToggle.addLabel(DevkitServerModule.MainLocalization.Translate("ObjectIconEditorToggle"), new SleekColor(ESleekTint.FOREGROUND).Get(), ESleekSide.LEFT);
        _isEditingToggle.sideLabel.shadowStyle = ETextContrastContext.ColorfulBackdrop;
        _isEditingToggle.isVisible = false;
        _isEditingToggle.onToggled += OnToggled;
        _container.AddChild(_isEditingToggle);

        _saveEditButton = Glazier.Get().CreateButton();
        _saveEditButton.positionScale_X = 1f;
        _saveEditButton.positionScale_Y = 1f;
        _saveEditButton.positionOffset_X = _isEditingToggle.positionOffset_X - Size + 10;
        _saveEditButton.positionOffset_Y = -Size - 25;
        _saveEditButton.sizeOffset_X = Size / 2;
        _saveEditButton.sizeOffset_Y = 30;
        _saveEditButton.text = DevkitServerModule.MainLocalization.Translate("ObjectIconEditorSave");
        _saveEditButton.onClickedButton += OnSaveEdit;
        _saveEditButton.isVisible = false;
        _container.AddChild(_saveEditButton);

        _saveNewEditButton = Glazier.Get().CreateButton();
        _saveNewEditButton.positionScale_X = 1f;
        _saveNewEditButton.positionScale_Y = 1f;
        _saveNewEditButton.positionOffset_X = _isEditingToggle.positionOffset_X + (Size + 30) / 2 - Size;
        _saveNewEditButton.positionOffset_Y = -Size - 25;
        _saveNewEditButton.sizeOffset_X = Size / 2;
        _saveNewEditButton.sizeOffset_Y = 30;
        _saveNewEditButton.text = DevkitServerModule.MainLocalization.Translate("ObjectIconEditorSaveNew");
        _saveNewEditButton.onClickedButton += OnSaveNewEdit;
        _saveNewEditButton.isVisible = false;
        _container.AddChild(_saveNewEditButton);

        _offsetField = Glazier.Get().CreateStringField();
        _offsetField.positionScale_X = 1f;
        _offsetField.positionScale_Y = 1f;
        _offsetField.positionOffset_X = _saveEditButton.positionOffset_X;
        _offsetField.positionOffset_Y = _saveEditButton.positionOffset_Y + 40;
        _offsetField.sizeOffset_X = 3 * Size / 4 - 5;
        _offsetField.sizeOffset_Y = 30;
        _offsetField.hint = DevkitServerModule.MainLocalization.Translate("ObjectIconEditorOffsetAssetHint");
        _offsetField.isVisible = false;
        _container.AddChild(_offsetField);

        _gotoOffsetButton = Glazier.Get().CreateButton();
        _gotoOffsetButton.positionScale_X = 1f;
        _gotoOffsetButton.positionScale_Y = 1f;
        _gotoOffsetButton.positionOffset_X = _offsetField.positionOffset_X + _offsetField.sizeOffset_X + 10;
        _gotoOffsetButton.positionOffset_Y = _offsetField.positionOffset_Y;
        _gotoOffsetButton.sizeOffset_X = Size / 4;
        _gotoOffsetButton.sizeOffset_Y = 30;
        _gotoOffsetButton.text = DevkitServerModule.MainLocalization.Translate("ObjectIconEditorOffsetAssetButton");
        _gotoOffsetButton.isVisible = false;
        _gotoOffsetButton.onClickedButton += OnClickedGotoAsset;
        _gotoOffsetButton.onRightClickedButton += OnRightClickedGotoAsset;
        _container.AddChild(_gotoOffsetButton);

        UpdateSelectedObject();
        if (!_patched)
            Patch();
    }
#nullable restore

    private void OnRightClickedGotoAsset(ISleekElement button)
    {
        _offsetField.text = LevelObjectUtil.SelectedAsset is not ObjectAsset asset ? string.Empty : asset.GUID.ToString("N");
    }
    private void OnClickedGotoAsset(ISleekElement button)
    {
        if (!Guid.TryParse(_offsetField.text, out Guid guid) || Assets.find(guid) is not ObjectAsset asset)
        {
            _offsetField.text = string.Empty;
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

        _saveNewEditButton.isVisible = state;
        _saveEditButton.isVisible = state;
        _offsetField.isVisible = state;
        _gotoOffsetButton.isVisible = state;
        _editHint.isVisible = !state;
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
                _isEditingToggle.state = false;
            }

            _isEditingToggle.isVisible = value;
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
        if (!inst._isEditingToggle.state || LevelObjectUtil.SelectedAsset is not ObjectAsset asset)
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
                box.text = string.Empty;
            else if (levelObject != null)
                box.text = levelObject.FriendlyName;
            else
                box.text = buildable!.FriendlyName;

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
        _preview.texture = null;
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
            _displayTitle.text = text;
            Color rarityColor = asset is ItemAsset item ? ItemTool.getRarityColorUI(item.rarity) : Color.white;
            _displayTitle.backgroundColor = SleekColor.BackgroundIfLight(rarityColor);
            _displayTitle.textColor = rarityColor;
        }
        else
        {
            _displayTitle.textColor = ESleekTint.FOREGROUND;
            _displayTitle.text = DevkitServerModule.MainLocalization.Translate("NoAssetSelected");
        }
    }

    private void OnIconReady(Asset asset, Texture? texture, bool destroy)
    {
        _isGeneratingIcon = false;
        if (EditorObjects.selectedItemAsset != asset && EditorObjects.selectedObjectAsset != asset)
            return;
        _preview.texture = texture;
        _preview.shouldDestroyTexture = destroy;
        if (texture != null)
        {
            float aspect = (float)texture.width / texture.height;
            if (Mathf.Approximately(aspect, 1f))
            {
                _preview.sizeOffset_X = Size;
                _preview.sizeOffset_Y = Size;
            }
            else if (aspect > 1f)
            {
                _preview.sizeOffset_X = Size;
                _preview.sizeOffset_Y = Mathf.RoundToInt(Size / aspect);
            }
            else
            {
                _preview.sizeOffset_X = Mathf.RoundToInt(Size * aspect);
                _preview.sizeOffset_Y = Size;
            }
        }
        else
        {
            _preview.sizeOffset_X = Size;
            _preview.sizeOffset_Y = Size;
        }

        _preview.positionOffset_X = -Size / 2;
        _preview.positionOffset_Y = -Size / 2;
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