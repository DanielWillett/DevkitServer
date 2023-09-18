#if CLIENT
#nullable disable
using DevkitServer.API.UI;
using System.Diagnostics;

namespace DevkitServer.Core.Extensions.UI;

[UIExtension(typeof(MenuDashboardUI))]
internal class MenuDashboardUIExtension : UIExtension, IDisposable
{
    private readonly ISleekButton _githubButton;
    [ExistingUIMember("exitButton")]
    private SleekButtonIcon ExitButton { get; set; }

    [ExistingUIMember("container")]
    private SleekFullscreenBox Container { get; set; }

    public MenuDashboardUIExtension()
    {
        _githubButton = Glazier.Get().CreateButton();

        _githubButton.CopyTransformFrom(ExitButton);
        _githubButton.positionOffset_Y = ExitButton.positionOffset_Y - 60;
        _githubButton.text = "DevkitServer GitHub";
        _githubButton.fontSize = ESleekFontSize.Medium;
        _githubButton.onClickedButton += OnClickedDevkitServerGithubButton;
        _githubButton.backgroundColor = ESleekTint.BACKGROUND;

        Container.AddChild(_githubButton);
    }

    private static void OnClickedDevkitServerGithubButton(ISleekElement button)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = DevkitServerModule.RepositoryUrl,
            UseShellExecute = true
        });
    }

    public void Dispose()
    {
        _githubButton.onClickedButton -= OnClickedDevkitServerGithubButton;
        Container.RemoveChild(_githubButton);
    }
}
#nullable restore
#endif