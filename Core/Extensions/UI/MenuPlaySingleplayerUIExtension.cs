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

        refreshButton.PositionOffset_X = -305;
        refreshButton.PositionOffset_Y = 520;
        refreshButton.PositionScale_X = 0.5f;
        refreshButton.SizeOffset_X = 200;
        refreshButton.SizeOffset_Y = 30;
        refreshButton.Text = DevkitServerModule.MainLocalization.Translate("RefreshLevelsButton");
        refreshButton.OnClicked += OnClickedRefresh;

        Container.AddChild(refreshButton);
    }

    private void OnClickedRefresh(ISleekElement button)
    {
        AssetUtil.RefreshLevelsUI();
    }
}
#endif