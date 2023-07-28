#if CLIENT
using DevkitServer.API.UI;
using DevkitServer.Configuration;
using DevkitServer.Patches;
using DevkitServer.Players.UI;
using HarmonyLib;
using System.Reflection;

namespace DevkitServer.Core.Extensions.UI;
[UIExtension(typeof(EditorLevelObjectsUI))]
internal class EditorLevelObjectsUIExtension : UIExtension
{
    private const int Size = 158;
    private static bool _patched;
#nullable disable
    [ExistingUIMember("container")]
    private readonly SleekFullscreenBox _container;

    [ExistingUIMember("assetsScrollBox")]
    private readonly SleekList<Asset> _assetsScrollBox;

    private readonly ISleekBox _displayTitle;
    private readonly ISleekImage _preview;
    internal EditorLevelObjectsUIExtension()
    {
        if (DevkitServerConfig.Config.EnableObjectUIExtension)
        {
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

            UpdateSelectedObject();

            displayBox.AddChild(_preview);

            if (!_patched)
                Patch();
        }
    }
#nullable restore

    internal void UpdateSelectedObject()
    {
        _preview.texture = null;
        Asset? asset = (Asset?)EditorObjects.selectedObjectAsset ?? EditorObjects.selectedItemAsset;
        if (asset != null)
        {
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
        if (!__runOriginal)
            return;

        UIExtensionInfo? info = UIExtensionManager.Extensions.FirstOrDefault(x => x.ImplementationType == typeof(EditorLevelObjectsUIExtension));

        if (info == null)
            return;
        EditorLevelObjectsUIExtension? inst = info.Instantiations.OfType<EditorLevelObjectsUIExtension>().LastOrDefault();
        inst?.UpdateSelectedObject();
    }
}
#endif