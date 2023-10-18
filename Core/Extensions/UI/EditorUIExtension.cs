﻿#if CLIENT
using DevkitServer.API.UI;
using DevkitServer.Multiplayer;
using DevkitServer.Players;

namespace DevkitServer.Core.Extensions.UI;

[UIExtension(typeof(EditorUI))]
internal class EditorUIExtension : ContainerUIExtension
{
    private readonly Dictionary<ulong, ISleekLabel> _nametags = new Dictionary<ulong, ISleekLabel>(16);
    private ISleekLabel? _testLabel;
    private SleekLoadingScreenProgressBar? _loadingProgress;
    private ISleekBox? _loadingBox;
    private bool _subbed;
    protected override SleekWindow Parent => EditorUI.window;
    protected override void OnShown()
    {
        if (!_subbed && DevkitServerModule.IsEditing)
        {
            UserManager.OnUserConnected += OnUserConnected;
            UserManager.OnUserDisconnected += OnUserDisconnected;
            UserInput.OnUserEditorPositionUpdated += OnUserEditorPositionUpdated;
            _subbed = true;
        }

        _testLabel = Glazier.Get().CreateLabel();
        _testLabel.TextAlignment = TextAnchor.LowerLeft;
        _testLabel.FontSize = ESleekFontSize.Tiny;
        _testLabel.SizeScale_X = 0.5f;
        _testLabel.SizeOffset_X = -5;
        _testLabel.SizeOffset_Y = 20;
        _testLabel.PositionScale_Y = 1f;
        _testLabel.PositionOffset_X = 5;
        _testLabel.PositionOffset_Y = -20;
        _testLabel.IsVisible = true;
        _testLabel.TextContrastContext = ETextContrastContext.ColorfulBackdrop;
        _testLabel.Text = DevkitServerModule.MainLocalization.format("Name") + " v" + Accessor.DevkitServer.GetName().Version.ToString(3) + "-client, Src: " + DevkitServerModule.GetRelativeRepositoryUrl(null, false) + ".";

        _loadingBox = Glazier.Get().CreateBox();

        _loadingBox.IsVisible = false;
        _loadingBox.PositionScale_X = 0.5f;
        _loadingBox.PositionScale_Y = 0.25f;
        _loadingBox.SizeScale_X = 0.25f;
        _loadingBox.SizeOffset_X = 10f;
        _loadingBox.SizeOffset_Y = 50f;

        // probably ugly af but ill change it later.
        _loadingProgress = new SleekLoadingScreenProgressBar
        {
            DescriptionText = string.Empty,
            ProgressPercentage = 0,

            IsVisible = false,
            PositionScale_X = 0.5f,
            PositionScale_Y = 0.25f,
            SizeScale_X = 0.25f,
            SizeOffset_Y = 40f
        };

        Container.AddChild(_testLabel);
        Container.AddChild(_loadingProgress);
        if (DevkitServerModule.IsEditing)
            UpdateAllNametags();
        Logger.LogDebug("Shown editor extension");
    }
    protected override void OnHidden()
    {
        if (_subbed)
        {
            UserManager.OnUserConnected -= OnUserConnected;
            UserManager.OnUserDisconnected -= OnUserDisconnected;
            UserInput.OnUserEditorPositionUpdated -= OnUserEditorPositionUpdated;
            _subbed = false;
        }
        if (_testLabel != null)
        {
            Container.RemoveChild(_testLabel);
            _testLabel = null;
        }
        if (_loadingProgress != null)
        {
            Container.RemoveChild(_loadingProgress);
            _loadingProgress = null;
        }
        if (_loadingBox != null)
        {
            Container.RemoveChild(_loadingBox);
            _loadingBox = null;
        }
        Logger.LogDebug("hidden editor extension");
    }

    protected override void OnDestroyed()
    {
        OnHidden();
        Logger.LogDebug("Destroyed editor extension");
    }

    private void OnUserEditorPositionUpdated(EditorUser user)
    {
        if (Container == null)
            return;
        if (user == EditorUser.User)
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
        Logger.LogDebug($"Created nametag: {user.Format()}.");
    }

    private void UpdateNametag(ISleekLabel nametag, EditorUser user)
    {
        if (Container == null)
            return;
        GameObject? ctrl = user.Input.ControllerObject;
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

        Container.RemoveChild(lbl);
        _nametags.Remove(user.SteamId.m_SteamID);
    }
    internal void UpdateAllNametags()
    {
        if (Container == null)
            return;
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