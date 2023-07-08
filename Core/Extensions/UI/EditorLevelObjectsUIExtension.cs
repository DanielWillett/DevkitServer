#if CLIENT
using System.Reflection;
using DevkitServer.API.UI;
using DevkitServer.Patches;
using DevkitServer.Players.UI;
using HarmonyLib;

namespace DevkitServer.Core.Extensions.UI;
[UIExtension(typeof(EditorLevelObjectsUI))]
internal class EditorLevelObjectsUIExtension : UIExtension
{
    private static bool _patched;
#nullable disable
    [ExistingUIMember("container")]
    private readonly SleekFullscreenBox _container;

    [ExistingUIMember("assetsScrollBox")]
    private readonly SleekList<Asset> _assetsScrollBox;

    private readonly ISleekBox _displayBox;
    private readonly ISleekImage _preview;
    internal EditorLevelObjectsUIExtension()
    {
        _displayBox = Glazier.Get().CreateBox();
        _displayBox.positionScale_X = 1f;
        _displayBox.positionScale_Y = 1f;
        _displayBox.positionOffset_X = _assetsScrollBox.positionOffset_X - 148;
        _displayBox.positionOffset_Y = -138;
        _displayBox.sizeOffset_X = 138;
        _displayBox.sizeOffset_Y = 138;
        _container.AddChild(_displayBox);

        _preview = Glazier.Get().CreateImage();
        _preview.sizeScale_X = 1f;
        _preview.sizeScale_Y = 1f;
        _preview.positionOffset_X = 5;
        _preview.positionOffset_Y = 5;
        _preview.sizeOffset_X = -10;
        _preview.sizeOffset_Y = -10;

        UpdateSelectedObject();

        _displayBox.AddChild(_preview);

        if (!_patched)
            Patch();
    }
#nullable restore

    internal void UpdateSelectedObject()
    {
        _preview.texture = null;
        ItemAsset? buildable = EditorObjects.selectedItemAsset;
        if (buildable != null)
        {
            ItemTool.getIcon(buildable.id, 0, 100, buildable.getState(true), buildable, null, string.Empty, string.Empty, 128, 128, true, false, texture => OnBuildableIconReady(texture, buildable));
            return;
        }

        ObjectAsset? levelObject = EditorObjects.selectedObjectAsset;

        if (levelObject == null)
            return;
    }

    private void OnBuildableIconReady(Texture texture, ItemAsset asset)
    {
        if (EditorObjects.selectedItemAsset != asset)
            return;
        _preview.texture = texture;
    }
    private void OnObjectIconReady(Texture texture, ObjectAsset asset)
    {
        if (EditorObjects.selectedObjectAsset != asset)
            return;
        _preview.texture = texture;
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
    }
    private static void OnUpdatedElement(bool __runOriginal)
    {
        if (!__runOriginal)
            return;

        UIExtensionInfo? info = UIExtensionManager.Extensions.FirstOrDefault(x => x.ImplementationType == typeof(EditorLevelObjectsUIExtension));

        if (info == null)
            return;

        foreach (EditorLevelObjectsUIExtension inst in info.Instantiations.OfType<EditorLevelObjectsUIExtension>())
            inst.UpdateSelectedObject();
    }
}
#endif