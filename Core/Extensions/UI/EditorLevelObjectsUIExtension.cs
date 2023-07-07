#if CLIENT
using DevkitServer.API.Extensions.UI;
using DevkitServer.Players.UI;

namespace DevkitServer.Core.Extensions.UI;
#nullable disable
[UIExtension(typeof(EditorLevelObjectsUI))]
internal class EditorLevelObjectsUIExtension : UIExtension
{
    // nothing will happen
    private SleekButtonIcon _transformButton;

    // will be set on instantiation
    [ExistingUIMember("materialIndexOverrideField")]
    internal ISleekInt32Field MaterialIndexOverrideField = null!;

    // will be patched over
    [ExistingUIMember("rotateButton")]
    internal SleekButtonIcon RotateButton { get; }

    // will be called on instantiation
    [ExistingUIMember("transformButton")]
    internal SleekButtonIcon TransformButton
    {
        get => _transformButton;
        set => _transformButton = value;
    }

    // will be patched over
    [ExistingUIMember("scaleButton", InitializeMode = ExistingMemberInitializeMode.PatchGetter)]
    internal SleekButtonIcon ScaleButton
    {
        get => throw new NotImplementedException();
        set => throw new NotImplementedException();
    }

    // will be patched over
    [ExistingUIMember("coordinateButton")]
    internal SleekButtonState GetCoordinateButton() => throw new NotImplementedException();

    // called right after EditorLevelObjectsUI constructor.
    internal EditorLevelObjectsUIExtension()
    {
        Logger.LogInfo("GOT TEXT INFO: " + RotateButton.text);
    }
}
#nullable restore
#endif