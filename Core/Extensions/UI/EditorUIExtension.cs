#if CLIENT
using DevkitServer.API.UI;
using DevkitServer.Multiplayer;
using DevkitServer.Players;

namespace DevkitServer.Core.Extensions.UI;

[UIExtension(typeof(EditorUI))]
internal class EditorUIExtension : ContainerUIExtension
{
    private readonly List<Nametag> _nametags = new List<Nametag>(16);
    private bool _subbed;
    protected override SleekWindow Parent => EditorUI.window;
    protected override void Opened()
    {
        if (!_subbed)
        {
            UserManager.OnUserConnected += OnUserConnected;
            UserManager.OnUserDisconnected += OnUserDisconnected;
            UserInput.OnUserEditorPositionUpdated += OnUserEditorPositionUpdated;
            _subbed = true;
        }
        UpdateAllNametags();
        base.Opened();
    }

    protected override void Closed()
    {
        base.Closed();
        if (_subbed)
        {
            UserManager.OnUserConnected -= OnUserConnected;
            UserManager.OnUserDisconnected -= OnUserDisconnected;
            UserInput.OnUserEditorPositionUpdated -= OnUserEditorPositionUpdated;
            _subbed = false;
        }
    }

    public override void Dispose()
    {
        if (_subbed)
        {
            UserManager.OnUserConnected -= OnUserConnected;
            UserManager.OnUserDisconnected -= OnUserDisconnected;
            UserInput.OnUserEditorPositionUpdated -= OnUserEditorPositionUpdated;
            _subbed = false;
        }
        base.Dispose();
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
        for (int i = 0; i < _nametags.Count; ++i)
        {
            if (_nametags[i].Player == user.SteamId.m_SteamID)
            {
                UpdateNametag(_nametags[i].Label, user);
                return;
            }
        }

        CreateNametag(user);
    }
    private void CreateNametag(EditorUser user)
    {
        if (Container == null)
            return;
        // PlayerGroupUI.addGroup
        ISleekLabel label = Glazier.Get().CreateLabel();
        label.positionOffset_X = -100;
        label.positionOffset_Y = -15;
        label.sizeOffset_X = 200;
        label.sizeOffset_Y = 30;
        label.shadowStyle = ETextContrastContext.ColorfulBackdrop;
        label.text = user.DisplayName;
        UpdateNametag(label, user);
        Container.AddChild(label);
        _nametags.Add(new Nametag(user.SteamId.m_SteamID, label));
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
            if (nametag.isVisible)
                nametag.isVisible = false;
        }
        else
        {
            Vector2 adjScreenPos = Container.ViewportToNormalizedPosition(screenPos);
            nametag.positionScale_X = adjScreenPos.x;
            nametag.positionScale_Y = adjScreenPos.y;
            if (!nametag.isVisible)
                nametag.isVisible = true;
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
        for (int i = 0; i < _nametags.Count; ++i)
        {
            if (_nametags[i].Player == user.SteamId.m_SteamID)
            {
                ISleekLabel lbl = _nametags[i].Label;
                _nametags.RemoveAt(i);
                Container.RemoveChild(lbl);
                break;
            }
        }
    }
    internal void UpdateAllNametags()
    {
        if (Container == null)
            return;
        for (int p = 0; p < UserManager.Users.Count; ++p)
        {
            bool found = false;
            EditorUser u = UserManager.Users[p];
            if (u.IsOwner) continue;
            for (int i = 0; i < _nametags.Count; ++i)
            {
                if (_nametags[i].Player == u.SteamId.m_SteamID)
                {
                    UpdateNametag(_nametags[i].Label, u);
                    found = true;
                    break;
                }
            }

            if (!found)
                CreateNametag(u);
        }
    }
    private readonly struct Nametag
    {
        public ulong Player { get; }
        public ISleekLabel Label { get; }
        public Nametag(ulong player, ISleekLabel label)
        {
            Player = player;
            Label = label;
        }
    }
}
#endif