#if CLIENT
using DevkitServer.API;
using DevkitServer.API.UI.Extensions;
using DevkitServer.Multiplayer;
using DevkitServer.Multiplayer.Movement;
using DevkitServer.Players;
using Version = System.Version;

namespace DevkitServer.Core.UI.Extensions;

[UIExtension(typeof(EditorUI))]
public class EditorUIExtension : ContainerUIExtension
{
    private readonly Dictionary<ulong, ISleekLabel> _nametags = new Dictionary<ulong, ISleekLabel>(16);
    private ISleekLabel? _testLabel;
    private SleekLoadingScreenProgressBar? _loadingProgress;
    private ISleekBox? _loadingBox;
    private bool _subbed;
    private bool _lastFadeSetting;
    protected override SleekWindow Parent => EditorUI.window;
    protected override void OnShown()
    {
        if (!_subbed && DevkitServerModule.IsEditing)
        {
            UserManager.OnUserConnected += OnUserConnected;
            UserManager.OnUserDisconnected += OnUserDisconnected;
            UserMovement.OnUserMoved += OnUserEditorPositionUpdated;
            _subbed = true;
        }

        Version version = AccessorExtensions.DevkitServer.GetName().Version;

        _testLabel = Glazier.Get().CreateLabel();
        _testLabel.TextAlignment = TextAnchor.LowerLeft;
        _testLabel.FontSize = version.Major <= 0 ? ESleekFontSize.Small : ESleekFontSize.Tiny;
        _testLabel.SizeScale_X = 0.5f;
        _testLabel.SizeOffset_X = -5;
        _testLabel.SizeOffset_Y = 20;
        _testLabel.PositionScale_Y = 1f;
        _testLabel.PositionOffset_X = version.Major <= 0 ? 1 : 2;
        _testLabel.PositionOffset_Y = -20;
        _testLabel.IsVisible = true;
        _testLabel.TextContrastContext = ETextContrastContext.ColorfulBackdrop;

        string debugText = DevkitServerModule.MainLocalization.format("Name") + " v" + version.ToString(3) + "-client, Src: " + DevkitServerModule.GetRelativeRepositoryUrl(null, false) + ".";
        if (version.Major <= 0)
            debugText += " [WIP]";

        _testLabel.Text = debugText;

        _loadingBox = Glazier.Get().CreateBox();

        _loadingBox.IsVisible = false;
        _loadingBox.PositionScale_X = 0.375f;
        _loadingBox.PositionScale_Y = 0.25f;
        _loadingBox.PositionOffset_X = -10f;
        _loadingBox.PositionOffset_Y = -10f - 30f;
        _loadingBox.SizeScale_X = 0.25f;
        _loadingBox.SizeOffset_X = 20f;
        _loadingBox.SizeOffset_Y = 60f;

        _loadingProgress = new SleekLoadingScreenProgressBar
        {
            DescriptionText = string.Empty,
            ProgressPercentage = 0,

            IsVisible = false,
            PositionScale_X = 0.375f,
            PositionScale_Y = 0.25f,
            PositionOffset_Y = -10f - 20f,
            SizeScale_X = 0.25f,
            SizeOffset_Y = 40f
        };

        Container.AddChild(_testLabel);
        Container.AddChild(_loadingBox);
        Container.AddChild(_loadingProgress);
        if (DevkitServerModule.IsEditing)
        {
            UpdateAllNametags();
        }
        Logger.DevkitServer.LogDebug(nameof(EditorUIExtension), "Shown editor extension");
    }
    protected override void OnHidden()
    {
        if (_subbed)
        {
            UserManager.OnUserConnected -= OnUserConnected;
            UserManager.OnUserDisconnected -= OnUserDisconnected;
            UserMovement.OnUserMoved -= OnUserEditorPositionUpdated;
            _subbed = false;
        }
        if (_testLabel != null)
        {
            Container.TryRemoveChild(_testLabel);
            _testLabel = null;
        }
        if (_loadingBox != null)
        {
            Container.TryRemoveChild(_loadingBox);
            _loadingBox = null;
        }
        if (_loadingProgress != null)
        {
            Container.TryRemoveChild(_loadingProgress);
            _loadingProgress = null;
        }
    }

    protected override void OnDestroyed()
    {
        OnHidden();
    }

    private void OnUserEditorPositionUpdated(EditorUser user)
    {
        if (Container == null)
            return;
        if (user == EditorUser.User || _lastFadeSetting != OptionsSettings.shouldNametagFadeOut)
        {
            UpdateAllNametags();
            return;
        }

        if (!_nametags.TryGetValue(user.SteamId.m_SteamID, out ISleekLabel label))
            CreateNametag(user);
        else
            UpdateNametag(label, user);
    }
    private void CreateNametag(EditorUser user)
    {
        if (Container == null)
            return;
        // PlayerGroupUI.addGroup
        ISleekLabel label = Glazier.Get().CreateLabel();
        label.PositionOffset_X = -100;
        label.PositionOffset_Y = -15;
        label.SizeOffset_X = 200;
        label.SizeOffset_Y = 30;
        label.TextContrastContext = ETextContrastContext.ColorfulBackdrop;
        label.Text = user.DisplayName;
        UpdateNametag(label, user);
        Container.AddChild(label);
        _nametags.Add(user.SteamId.m_SteamID, label);
    }

    private void UpdateNametag(ISleekLabel nametag, EditorUser user)
    {
        if (Container == null)
            return;
        GameObject? ctrl = user.Control.ControllerObject;
        if (ctrl == null)
            return;
        Vector3 screenPos = MainCamera.instance.WorldToViewportPoint(ctrl.transform.position + Vector3.up);
        if (screenPos.z <= 0.0)
        {
            if (nametag.IsVisible)
                nametag.IsVisible = false;
        }
        else
        {
            Vector2 adjScreenPos = Container.ViewportToNormalizedPosition(screenPos);
            nametag.PositionScale_X = adjScreenPos.x;
            nametag.PositionScale_Y = adjScreenPos.y;

            if (!nametag.IsVisible)
                nametag.IsVisible = true;

            float alpha;
            if (OptionsSettings.shouldNametagFadeOut)
            {
                float magnitude = new Vector2(adjScreenPos.x - 0.5f, adjScreenPos.y - 0.5f).magnitude;
                float t = Mathf.InverseLerp(0.0125f, 0.1f, magnitude);
                alpha = Mathf.Lerp(0.1f, 0.75f, t);
            }
            else
            {
                alpha = 0.75f;
            }

            nametag.TextColor = new SleekColor(ESleekTint.FONT, alpha);
        }
    }

    private void OnUserConnected(EditorUser user)
    {
        OnUserEditorPositionUpdated(user);
    }

    private void OnUserDisconnected(EditorUser user)
    {
        if (Container == null)
            return;

        if (!_nametags.TryGetValue(user.SteamId.m_SteamID, out ISleekLabel lbl))
            return;

        Container.TryRemoveChild(lbl);
        _nametags.Remove(user.SteamId.m_SteamID);
    }
    internal void UpdateAllNametags()
    {
        if (Container == null)
            return;
        _lastFadeSetting = OptionsSettings.shouldNametagFadeOut;
        foreach (EditorUser u in UserManager.Users)
        {
            if (u.IsOwner)
                continue;

            if (!_nametags.TryGetValue(u.SteamId.m_SteamID, out ISleekLabel label))
                CreateNametag(u);
            else
                UpdateNametag(label, u);
        }
    }
    public void UpdateLoadingBarVisibility(bool visibility)
    {
        if (_loadingBox != null && _loadingBox.IsVisible != visibility)
            _loadingBox.IsVisible = visibility;
        if (_loadingProgress != null && _loadingProgress.IsVisible != visibility)
            _loadingProgress.IsVisible = visibility;
    }
    public void UpdateLoadingBarDescription(string description)
    {
        if (_loadingProgress != null && !string.Equals(_loadingProgress.DescriptionText, description, StringComparison.Ordinal))
            _loadingProgress.DescriptionText = description;
    }
    public void UpdateLoadingBarProgress(float progress)
    {
        if (_loadingProgress != null && progress != _loadingProgress.ProgressPercentage)
            _loadingProgress.ProgressPercentage = progress;
    }
}
#endif