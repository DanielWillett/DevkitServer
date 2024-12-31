using System.Diagnostics;
using Cysharp.Threading.Tasks;
using DevkitServer.API.Cartography;
using DevkitServer.API.Cartography.Compositors;

namespace DevkitServer.Core.UI;

public class SleekCompositorPipeline : SleekWrapper
{
    private readonly SleekButtonIcon _buttonWithIcon;
    public string FileName { get; }
    public CartographyType Type { get; }
    public SleekCompositorPipeline(string fileName, string displayName, CartographyType type, Texture2D? texture)
    {
        FileName = fileName;
        Type = type;

        _buttonWithIcon = new SleekButtonIcon(texture, 20, false)
        {
            text = displayName,
            tooltip = fileName,
            SizeScale_X = 1f,
            SizeScale_Y = 1f
        };

        _buttonWithIcon.onClickedButton += OnClicked;
        _buttonWithIcon.onRightClickedButton += OnRightClicked;

        AddChild(_buttonWithIcon);
    }

    private void OnRightClicked(ISleekElement button)
    {
        if (!File.Exists(FileName))
            return;

        Process.Start(new ProcessStartInfo
        {
            FileName = FileName,
            UseShellExecute = true
        });
    }

    private void OnClicked(ISleekElement button)
    {
        CompositorPipeline? pipeline = CompositorPipeline.FromFile(FileName);

        if (pipeline == null)
            return;

        UniTask.Create(async () =>
        {
            string? path = await pipeline.Composite();
            if (path == null || !pipeline.AutoOpen)
                return;

            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        });
    }
}
