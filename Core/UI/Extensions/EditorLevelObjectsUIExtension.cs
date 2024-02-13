#if CLIENT
using DevkitServer.API;
using DevkitServer.API.UI.Extensions;
using DevkitServer.API.UI.Extensions.Members;
using DevkitServer.API.UI.Icons;
using DevkitServer.Configuration;
using DevkitServer.Multiplayer.Movement;
using DevkitServer.Patches;
using HarmonyLib;
using System.Reflection;

namespace DevkitServer.Core.UI.Extensions;
[UIExtension(typeof(EditorLevelObjectsUI))]
internal class EditorLevelObjectsUIExtension : UIExtension
{
    private const int Size = 158;
    private static bool _patched;
#nullable disable
#pragma warning disable CS0649
    [ExistingMember("container")]
    private readonly SleekFullscreenBox _container;

    [ExistingMember("assetsScrollBox")]
    private readonly SleekList<Asset> _assetsScrollBox;

    [ExistingMember("selectedBox")]
    private ISleekBox SelectedBox { get; }

#pragma warning restore CS0649

    private readonly ISleekBox _displayTitle;
    private readonly ISleekImage _preview;
    private bool _isGeneratingIcon;
    private bool _editorActive;
    private int _materialIndex;
    private int _materialTtl;
    private float _nextIcon;
    private float _lastUpdate;

    private readonly ISleekToggle _isEditingToggle;
    private readonly ISleekButton _saveEditButton;
    private readonly ISleekButton _saveNewEditButton;
    private readonly ISleekButton _gotoOffsetButton;
    private readonly ISleekField _offsetField;
    private readonly ISleekLabel _editHint;
    private readonly ISleekLabel _materialIndexLbl;
    private readonly ISleekLabel _noteText;

    /// <summary>
    /// If the live editor is enabled.
    /// </summary>
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
        _preview.SizeScale_X = 1f;
        _preview.SizeScale_Y = 1f;
        _preview.SizeOffset_X = -20;
        _preview.SizeOffset_Y = -20;
        _preview.PositionOffset_X = 10;
        _preview.PositionOffset_Y = 10;
        _preview.ShouldDestroyTexture = true;
        displayBox.AddChild(_preview);

        _editHint = Glazier.Get().CreateLabel();
        _editHint.TextContrastContext = ETextContrastContext.ColorfulBackdrop;
        _editHint.Text = DevkitServerModule.MainLocalization.Translate("ObjectIconEditorToggleHint", MenuConfigurationControlsUI.getKeyCodeText(DevkitServerConfig.Config.LevelObjectEditKeybind));
        _editHint.PositionScale_X = 1f;
        _editHint.PositionScale_Y = 1f;
        _editHint.PositionOffset_X = _assetsScrollBox.PositionOffset_X - (Size + 30);
        _editHint.PositionOffset_Y = -20;
        _editHint.TextAlignment = TextAnchor.MiddleCenter;
        _editHint.TextColor = new SleekColor(ESleekTint.FOREGROUND);
        _editHint.SizeOffset_X = Size + 20;
        _editHint.SizeOffset_Y = 20;

        _container.AddChild(_editHint);

        _materialIndexLbl = Glazier.Get().CreateLabel();
        _materialIndexLbl.TextContrastContext = ETextContrastContext.ColorfulBackdrop;
        _materialIndexLbl.PositionScale_X = 1f;
        _materialIndexLbl.PositionScale_Y = 1f;
        _materialIndexLbl.PositionOffset_X = _assetsScrollBox.PositionOffset_X - (Size + 20);
        _materialIndexLbl.PositionOffset_Y = -Size - 10;
        _materialIndexLbl.TextAlignment = TextAnchor.MiddleLeft;
        _materialIndexLbl.TextColor = new SleekColor(ESleekTint.FOREGROUND);
        _materialIndexLbl.SizeOffset_X = Size + 20;
        _materialIndexLbl.SizeOffset_Y = 20;
        _materialIndexLbl.IsVisible = true;
        _materialIndexLbl.Text = string.Empty;

        _container.AddChild(_materialIndexLbl);

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

        _noteText = Glazier.Get().CreateLabel();
        _noteText.AllowRichText = true;
        _noteText.TextColor = ESleekTint.FOREGROUND;
        _noteText.FontSize = ESleekFontSize.Small;
        _noteText.TextAlignment = TextAnchor.LowerCenter;
        _noteText.TextContrastContext = ETextContrastContext.ColorfulBackdrop;
        _noteText.Text = string.Empty;
        _noteText.IsVisible = false;
        _noteText.PositionScale_X = 1f;
        _noteText.PositionScale_Y = 0f;
        _noteText.SizeScale_Y = 1f;
        _noteText.SizeOffset_X = Size * 3f + 90f;
        _noteText.SizeOffset_Y = -(Size + 160);
        _noteText.PositionOffset_X = displayBox.PositionOffset_X - _noteText.SizeOffset_X + displayBox.SizeOffset_X;
        _noteText.PositionOffset_Y = 90f;
        _container.AddChild(_noteText);

        UpdateSelectedObject(true);
        if (!_patched)
            Patch();
    }
#nullable restore

    /// <summary>
    /// Call this after manually updating the selected object or buildable type.
    /// </summary>
    public static void UpdateSelection(ObjectAsset? levelObject, ItemAsset? buildable)
    {
        if (levelObject == null && buildable == null)
            ObjectIconPresets.ClearEditCache();
        EditorLevelObjectsUIExtension? inst = UIExtensionManager.GetInstance<EditorLevelObjectsUIExtension>();
        if (inst == null)
            return;
        try
        {
            ISleekBox? box = inst.SelectedBox;
            if (box == null)
                return;
            if (levelObject == null && buildable is not ItemBarricadeAsset and not ItemStructureAsset)
                box.Text = string.Empty;
            else if (levelObject != null)
                box.Text = levelObject.FriendlyName;
            else
                box.Text = buildable!.FriendlyName;
            
            inst._materialIndex = -1;
            inst.UpdateSelectedObject(true);
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError(ObjectIconGenerator.Source, ex, "Error updating selection.");
        }
    }

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
        {
            Logger.DevkitServer.LogWarning(ObjectIconGenerator.Source, $"Tried to goto asset {selectedAsset.FriendlyName} but not selected.");
            return;
        }

        bool editing = ObjectIconPresets.ActivelyEditing != null && ObjectIconPresets.ActivelyEditing.Asset.GUID == target.GUID;
        if (editing)
            ObjectIconPresets.ClearEditCache();

        ObjectIconGenerator.ObjectIconMetrics metrics = ObjectIconGenerator.GetObjectIconMetrics(asset);
        ObjectIconGenerator.GetCameraPositionAndRotation(in metrics, target.transform, out Vector3 position, out Quaternion rotation);
        UserMovement.SetEditorTransform(position, rotation);

        if (editing)
            ObjectIconPresets.UpdateEditCache(target, target.asset);
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
    private void OnSaveEdit(ISleekElement button)
    {
        Asset? asset = LevelObjectUtil.SelectedAsset;
        if (asset == null)
            return;
        ObjectIconPresets.SaveEditCache(false);
        UpdateSelectedObject(false);
    }
    private void OnSaveNewEdit(ISleekElement button)
    {
        Asset? asset = LevelObjectUtil.SelectedAsset;
        if (asset == null)
            return;
        ObjectIconPresets.SaveEditCache(true);
        UpdateSelectedObject(false);
    }
    internal static void OnUpdate()
    {
        EditorLevelObjectsUIExtension? inst = UIExtensionManager.GetInstance<EditorLevelObjectsUIExtension>();
        if (inst == null)
            return;

        if (InputEx.GetKeyDown(DevkitServerConfig.Config.LevelObjectEditKeybind))
            inst.EditorActive = !inst.EditorActive;

        if (InputEx.GetKeyDown(DevkitServerConfig.Config.LogMissingLevelObjectKeybind))
            LogMissingOffsets();

        if (inst._isGeneratingIcon)
            return;
        
        if (!inst._isEditingToggle.Value || LevelObjectUtil.SelectedAsset is not ObjectAsset asset)
            goto clear;
        
        LevelObject? selectedObject = null;
        foreach (EditorSelection selection in LevelObjectUtil.EditorObjectSelection)
        {
            if (!LevelObjectUtil.TryFindObject(selection.transform, out LevelObject lvlObj) || lvlObj.asset.GUID != asset.GUID)
                continue;
            
            if (selectedObject != null)
                goto clear;
            
            selectedObject = lvlObj;
        }

        if (selectedObject == null)
        {
            goto clear;
        }

        ObjectIconPresets.UpdateEditCache(selectedObject, asset);
        inst.UpdateSelectedObject(false);
        return;

    clear:
        if (ObjectIconPresets.ActivelyEditing != null)
        {
            ObjectIconPresets.ClearEditCache();
            inst.UpdateSelectedObject(true);
        }

        if (DevkitServerConfig.Config.ShouldCycleLevelObjectMaterialPalette && inst is { _materialIndex: >= 0, _nextIcon: > 0f } && inst._nextIcon < Time.realtimeSinceStartup)
            inst.UpdateSelectedObject(true);
    }

    internal void UpdateSelectedObject(bool updateMat)
    {
        _preview.Texture = null;
        Asset? asset = LevelObjectUtil.SelectedAsset;
        if (asset != null)
        {
            _isGeneratingIcon = true;
            _nextIcon = -1f;
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

            if (asset is ObjectAsset obj2)
            {
                if (obj2.interactability != EObjectInteractability.NOTE)
                {
                    if (_noteText.IsVisible)
                    {
                        _noteText.IsVisible = false;
                        _noteText.Text = string.Empty;
                    }
                }
                else
                {
                    _noteText.IsVisible = true;
                    _noteText.Text = obj2.interactabilityText;
                }

                if (updateMat && obj2.materialPalette.isValid && obj2.materialPalette.Find() is { materials.Count: > 0 } palette)
                {
                    _materialTtl = palette.materials.Count;
                    _materialIndex = _materialTtl == 1 || !DevkitServerConfig.Config.ShouldCycleLevelObjectMaterialPalette ? -1 : (_materialIndex == -1 ? UnityEngine.Random.Range(0, _materialTtl) : (_materialIndex + 1) % _materialTtl);
                }
                else if (updateMat) _materialTtl = 0;
                else _materialIndex = -1;
            }

            ObjectIconRenderOptions? options;
            if ((_materialIndex == -1 || _materialTtl == 0) && Time.realtimeSinceStartup - _lastUpdate > 1f)
                options = null;
            else
                options = new ObjectIconRenderOptions
                {
                    MaterialIndexOverride = !updateMat && _materialIndex == -1 ? 0 : _materialIndex
                };

            ObjectIconGenerator.GetIcon(asset, Size, Size, options, OnIconReady);
        }
        else
        {
            if (_noteText.IsVisible)
            {
                _noteText.IsVisible = false;
                _noteText.Text = string.Empty;
            }

            _displayTitle.TextColor = ESleekTint.FOREGROUND;
            _displayTitle.Text = DevkitServerModule.MainLocalization.Translate("NoAssetSelected");
            _materialIndexLbl.Text = string.Empty;
        }

        _lastUpdate = Time.realtimeSinceStartup;
    }

    private void OnIconReady(Asset asset, Texture? texture, bool destroy, ObjectIconRenderOptions? options)
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
                _preview.SizeOffset_X = -20;
                _preview.SizeOffset_Y = -20;
                _preview.PositionOffset_X = 10;
                _preview.PositionOffset_Y = 10;
            }
            else if (aspect > 1f)
            {
                _preview.SizeOffset_X = -20f;
                _preview.SizeOffset_Y = -(1f - 1f / aspect) * Size - 20f;
                _preview.PositionOffset_X = 10f;
                _preview.PositionOffset_Y = (1f - 1f / aspect) * Size / 2f + 10f;
            }
            else
            {
                _preview.PositionOffset_X = (1f - aspect) * Size / 2f + 10f;
                _preview.PositionOffset_Y = 10f;
                _preview.SizeOffset_X = -(1f - aspect) * Size - 20f;
                _preview.SizeOffset_Y = -20f;
            }

            _materialIndexLbl.Text = _materialIndex == -1 || !DevkitServerConfig.Config.ShouldCycleLevelObjectMaterialPalette ? string.Empty : $"{_materialIndex} / {_materialTtl - 1}";
        }
        else
        {
            _preview.SizeOffset_X = -20;
            _preview.SizeOffset_Y = -20;
            _preview.PositionOffset_X = 10;
            _preview.PositionOffset_Y = 10;
            _materialIndexLbl.Text = string.Empty;
        }

        _nextIcon = Time.realtimeSinceStartup + 1f;
    }
    private static void Patch()
    {
        MethodInfo? target = typeof(EditorLevelObjectsUI).GetMethod("onClickedAssetButton", BindingFlags.Static | BindingFlags.NonPublic);
        if (target == null)
        {
            Logger.DevkitServer.LogError(nameof(EditorLevelObjectsUIExtension), "Failed to find method: EditorLevelObjectsUI.onClickedAssetButton");
            return;
        }
        
        PatchesMain.Patcher.Patch(target, finalizer: new HarmonyMethod(Accessor.GetMethod(OnUpdatedElement)));
        _patched = true;
    }
    private static void OnUpdatedElement(bool __runOriginal)
    {
        if (!__runOriginal || !DevkitServerConfig.Config.EnableObjectUIExtension)
            return;

        EditorLevelObjectsUIExtension? inst = UIExtensionManager.GetInstance<EditorLevelObjectsUIExtension>();

        if (inst == null)
            return;

        inst._materialIndex = -1;
        inst.UpdateSelectedObject(true);
    }

    /// <summary>
    /// Logs any objects missing offsets to console.
    /// </summary>
    public static void LogMissingOffsets()
    {
        List<ObjectAsset> objects = new List<ObjectAsset>(4096);
        Assets.find(objects);

        string sboxPath = Path.Combine(UnturnedPaths.RootDirectory.FullName, "Sandbox") + Path.DirectorySeparatorChar;
        string mapPath = Path.Combine(UnturnedPaths.RootDirectory.FullName, "Maps") + Path.DirectorySeparatorChar;

        ulong lastMod = ulong.MaxValue;
        foreach (ObjectAsset obj in objects
                     .Where(x => x.type is not EObjectType.DECAL and not EObjectType.NPC)
                     .OrderByDescending(x => x.GetOrigin()?.workshopFileId ?? 0ul)
                     .ThenBy(x => x.getFilePath()))
        {
            if (ObjectIconPresets.ActivePresets.ContainsKey(obj.GUID))
                continue;

            AssetOrigin? assetOrigin = obj.GetOrigin();
            ulong modId = assetOrigin?.workshopFileId ?? 0ul;

            string path = Path.GetFullPath(obj.getFilePath());

            if (modId == 0ul && path.IndexOf(sboxPath, StringComparison.Ordinal) != -1)
                modId = 1ul;
            else if (modId == 0ul && path.IndexOf(mapPath, StringComparison.Ordinal) != -1)
                modId = 2ul;

            if (modId != lastMod)
            {
                lastMod = modId;
                Logger.DevkitServer.LogInfo(ObjectIconGenerator.Source, default(ReadOnlySpan<char>));
                string? modName;
                if (modId == 0ul)
                {
                    modName = "Vanilla Content";
                    Logger.DevkitServer.LogInfo(ObjectIconGenerator.Source, $"=== {modName.Format(false)} ===");
                }
                else if (modId == 1ul)
                {
                    modName = "Sandbox Content";
                    Logger.DevkitServer.LogInfo(ObjectIconGenerator.Source, $"=== {modName.Format(false)} ===");
                }
                else if (modId == 2ul)
                {
                    modName = "Map Bundles";
                    Logger.DevkitServer.LogInfo(ObjectIconGenerator.Source, $"=== {modName.Format(false)} ===");
                }
                else
                {
                    modName = null;
                    if (assetOrigin != null)
                    {
                        int index = assetOrigin.name.IndexOf('"');
                        if (index != -1 && index < assetOrigin.name.Length - 1)
                        {
                            int index2 = assetOrigin.name.IndexOf('"', index + 1);
                            modName = assetOrigin.name.Substring(index + 1, index2 - index - 1);
                        }
                    }

                    Logger.DevkitServer.LogInfo(ObjectIconGenerator.Source, (modName == null ? $"=== Mod: {modId.Format()} ===" : $"=== Mod: {modName.Format(false)} ({modId.Format()}) ==="));
                }
            }

            if (modId == 0ul)
                path = Path.GetRelativePath(UnturnedPaths.RootDirectory.FullName, path);
            else if (modId == 1ul)
                path = Path.GetRelativePath(sboxPath, path);
            else if (modId == 2ul)
                path = Path.GetRelativePath(mapPath, path);
            else
            {
                SteamContent? ugc = Provider.provider?.workshopService?.ugc?.Find(x => x.publishedFileID.m_PublishedFileId == modId);
                if (ugc != null)
                    path = Path.GetRelativePath(ugc.path, path);
            }

            Logger.DevkitServer.LogInfo(ObjectIconGenerator.Source, $"Missing Object: {$"{obj.FriendlyName,-33}".Colorize(new Color32(255, 204, 102, 255))} @ {path.Colorize(ConsoleColor.DarkGray)}");
        }
    }
}
#endif