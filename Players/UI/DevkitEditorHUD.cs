#if CLIENT
using DevkitServer.Multiplayer;
using JetBrains.Annotations;

namespace DevkitServer.Players.UI;
public class DevkitEditorHUD : MonoBehaviour
{
    public List<KeyValuePair<ISleekLabel, ulong>> Nametags = new List<KeyValuePair<ISleekLabel, ulong>>(16);
    public SleekWrapper ViewportPlane = new SleekWrapper();
    public SleekFullscreenBox Container = new SleekFullscreenBox();
    public readonly ISleekBox InfoBox = Glazier.Get().CreateBox();
    public readonly ISleekLabel MapLabel = Glazier.Get().CreateLabel();
    public readonly ISleekLabel UsersLabel = Glazier.Get().CreateLabel();
    public readonly List<KeyValuePair<ISleekLabel, ulong>> PlayerLabels = new List<KeyValuePair<ISleekLabel, ulong>>(16);
    public bool IsActive { get; private set; }
    public static DevkitEditorHUD? Instance { get; private set; }

    public DevkitEditorHUD()
    {
        Instance = this;
        InfoBox.positionOffset_Y = -50;
        InfoBox.positionScale_X = 0.8f;
        InfoBox.positionScale_Y = 1f;
        UpdateInfoHeight();
        InfoBox.sizeScale_X = 0.2f;
        MapLabel.positionOffset_X = 5;
        MapLabel.positionOffset_Y = 5;
        MapLabel.sizeOffset_X = 20;
        MapLabel.sizeOffset_Y = 20;
        MapLabel.text = Level.info.getLocalizedName();
        UsersLabel.positionOffset_X = 5;
        UsersLabel.positionOffset_Y = 35;
        UsersLabel.sizeOffset_X = 20;
        UsersLabel.sizeOffset_Y = 20;
        UsersLabel.text = "Users:";
        InfoBox.AddChild(MapLabel);
        InfoBox.AddChild(UsersLabel);
        for (int i = 0; i < UserManager.Users.Count; ++i)
        {
            EditorUser user = UserManager.Users[i];
            if (user != EditorUser.User)
            {
                ISleekLabel label = CreateListLabelForPlayer(user, i);
                PlayerLabels.Add(new KeyValuePair<ISleekLabel, ulong>(label, user.SteamId.m_SteamID));
                InfoBox.AddChild(label);
            }
        }
        Container.AddChild(InfoBox);
        EditorUI.window.AddChild(Container);
        Logger.LogDebug("Inited hud");
    }

    private void UpdateInfoHeight()
    {
        InfoBox.sizeOffset_X = 60 + (UserManager.Users.Count - 1) * 20;
    }

    private ISleekLabel CreateListLabelForPlayer(EditorUser user, int index)
    {
        ISleekLabel label = Glazier.Get().CreateLabel();

        label.positionOffset_X = 10;
        label.positionOffset_Y = 35 + 30 * index;
        label.sizeOffset_X = 20;
        label.sizeOffset_Y = 20;
        label.text = user.DisplayName;

        return label;
    }

    [UsedImplicitly]
    private void Awake()
    {
        UserManager.OnUserConnected += OnUserConnected;
        UserManager.OnUserDisconnected += OnUserDisconnected;
        UserInput.OnUserPositionUpdated += OnUserPositionUpdated;
    }

    [UsedImplicitly]
    private void OnDestroy()
    {
        UserManager.OnUserConnected -= OnUserConnected;
        UserManager.OnUserDisconnected -= OnUserDisconnected;
        UserInput.OnUserPositionUpdated -= OnUserPositionUpdated;
        ViewportPlane.RemoveAllChildren();
        Container.RemoveAllChildren();
        Nametags.Clear();
        EditorUI.window.RemoveChild(Container);
        if (Instance == this)
            Instance = null;
    }

    internal void OnUserPositionUpdated(EditorUser user)
    {
        if (user == EditorUser.User)
            return;
        for (int i = 0; i < Nametags.Count; ++i)
        {
            if (Nametags[i].Value == user.SteamId.m_SteamID)
            {
                UpdateNametag(Nametags[i].Key, user);
                return;
            }
        }

        // PlayerGroupUI.addGroup

        ISleekLabel label = Glazier.Get().CreateLabel();
        label.positionOffset_X = -100;
        label.positionOffset_Y = -15;
        label.sizeOffset_X = 200;
        label.sizeOffset_Y = 30;
        label.shadowStyle = ETextContrastContext.ColorfulBackdrop;
        UpdateNametag(label, user);
        ViewportPlane.AddChild(label);
        Nametags.Add(new KeyValuePair<ISleekLabel, ulong>(label, user.SteamId.m_SteamID));
    }

    // PlayerUI.updateGroupLabels
    private void UpdateNametag(ISleekLabel nametag, EditorUser user)
    {
        Vector3 screenPos = MainCamera.instance.WorldToViewportPoint(user.transform.position);
        if (!IsActive && nametag.isVisible)
        {
            nametag.isVisible = false;
            return;
        }
        if (screenPos.z <= 0.0)
        {
            if (nametag.isVisible)
                nametag.isVisible = false;
        }
        else
        {
            Vector2 adjScreenPos = ViewportPlane.ViewportToNormalizedPosition(screenPos);
            nametag.positionScale_X = adjScreenPos.x;
            nametag.positionScale_Y = adjScreenPos.y;
            if (!nametag.isVisible && string.IsNullOrEmpty(nametag.text) && user.Player != null)
            {
                nametag.text = user.Player.playerID.playerName;
                nametag.isVisible = true;
            }
        }
    }

    private void OnUserConnected(EditorUser user)
    {
        OnUserPositionUpdated(user);
        for (int i = 0; i < PlayerLabels.Count; ++i)
        {
            if (PlayerLabels[i].Value == user.SteamId.m_SteamID)
            {
                PlayerLabels[i].Key.text = user.DisplayName;
                return;
            }
        }

        ISleekLabel label = CreateListLabelForPlayer(user, PlayerLabels.Count);
        PlayerLabels.Add(new KeyValuePair<ISleekLabel, ulong>(label, user.SteamId.m_SteamID));
        InfoBox.AddChild(label);
    }

    private void OnUserDisconnected(EditorUser user)
    {
        for (int i = 0; i < Nametags.Count; ++i)
        {
            if (Nametags[i].Value == user.SteamId.m_SteamID)
            {
                ISleekLabel lbl = Nametags[i].Key;
                Nametags.RemoveAt(i);
                if (lbl.isVisible)
                    lbl.isVisible = false;
                ViewportPlane.RemoveChild(lbl);
                return;
            }
        }
        for (int i = 0; i < PlayerLabels.Count; ++i)
        {
            if (PlayerLabels[i].Value == user.SteamId.m_SteamID)
            {
                ISleekLabel lbl = PlayerLabels[i].Key;
                PlayerLabels.RemoveAt(i);
                if (lbl.isVisible)
                    lbl.isVisible = false;
                InfoBox.RemoveChild(lbl);
                return;
            }
        }
    }
    internal void UpdateAllNametags()
    {
        for (int i = 0; i < Nametags.Count; ++i)
        {
            if (UserManager.FromId(Nametags[i].Value) is { } pl)
                UpdateNametag(Nametags[i].Key, pl);
        }
    }

    internal static void Open()
    {
        Logger.LogDebug("Opening HUD");

        if (Instance == null)
        {
            Instance = DevkitServerModule.GameObjectHost.AddComponent<DevkitEditorHUD>();
        }
        if (Instance.IsActive)
            return;
        Instance.IsActive = true;
        Instance.Container.AnimateIntoView();
        Instance.UpdateAllNametags();
        Logger.LogDebug(" Opened HUD");
    }
    internal static void Close(bool destroy)
    {
        Logger.LogDebug("Closing HUD");
        if (Instance == null || !Instance.IsActive && destroy)
            return;

        if (destroy)
        {
            Destroy(Instance);
        }
        else
        {
            Instance.Container.AnimateOutOfView(0f, 1f);
        }
        Logger.LogDebug(" Closed HUD");
        Instance.IsActive = false;
        Instance.UpdateAllNametags();
    }
}
#endif