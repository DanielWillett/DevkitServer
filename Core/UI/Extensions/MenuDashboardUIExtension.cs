#if CLIENT
#nullable disable
using DevkitServer.API.UI.Extensions;
using DevkitServer.API.UI.Extensions.Members;
using System.Diagnostics;

namespace DevkitServer.Core.UI.Extensions;

[UIExtension(typeof(MenuDashboardUI))]
internal class MenuDashboardUIExtension : UIExtension, IDisposable
{
    private readonly ISleekButton _githubButton;
    [ExistingMember("exitButton")]
    private SleekButtonIcon ExitButton { get; set; }

    [ExistingMember("container")]
    private SleekFullscreenBox Container { get; set; }

    public MenuDashboardUIExtension()
    {
        _githubButton = Glazier.Get().CreateButton();

        _githubButton.CopyTransformFrom(ExitButton);
        _githubButton.PositionOffset_Y = ExitButton.PositionOffset_Y - 60;
        _githubButton.Text = "DevkitServer GitHub";
        _githubButton.FontSize = ESleekFontSize.Medium;
        _githubButton.OnClicked += OnClickedDevkitServerGithubButton;
        _githubButton.BackgroundColor = ESleekTint.BACKGROUND;

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
        _githubButton.OnClicked -= OnClickedDevkitServerGithubButton;
        Container.TryRemoveChild(_githubButton);
    }
}
#nullable restore
#endif