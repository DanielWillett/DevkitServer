#if CLIENT
using DevkitServer.API.UI;

namespace DevkitServer.Core.Extensions.UI;

[UIExtension(typeof(MenuPlaySingleplayerUI), SuppressUIExtensionParentWarning = true)]
internal class MenuPlaySingleplayerUIExtension
{
#nullable disable
    [ExistingUIMember("container")]
    private SleekFullscreenBox Container { get; set; }
#nullable restore
    public MenuPlaySingleplayerUIExtension()
    {
        ISleekButton refreshButton = Glazier.Get().CreateButton();

        refreshButton.positionOffset_X = -305;
        refreshButton.positionOffset_Y = 520;
        refreshButton.positionScale_X = 0.5f;
        refreshButton.sizeOffset_X = 200;
        refreshButton.sizeOffset_Y = 30;
        refreshButton.text = DevkitServerModule.MainLocalization.Translate("RefreshLevelsButton");
        refreshButton.onClickedButton += OnClickedRefresh;

        Container.AddChild(refreshButton);
    }

    private void OnClickedRefresh(ISleekElement button)
    {
        AssetUtil.RefreshLevelsUI();
    }
}
#endif