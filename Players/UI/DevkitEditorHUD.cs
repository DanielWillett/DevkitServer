#if CLIENT
using DevkitServer.Multiplayer;
using JetBrains.Annotations;

namespace DevkitServer.Players.UI;
public class DevkitEditorHUD : MonoBehaviour
{
    public List<KeyValuePair<ISleekLabel, ulong>> Nametags = new List<KeyValuePair<ISleekLabel, ulong>>(16);
#nullable disable
    public SleekFullscreenBox Container;
    public ISleekBox InfoBox;
    public ISleekLabel MapLabel;
    public ISleekLabel UsersLabel;
#nullable restore
    public List<KeyValuePair<ISleekLabel, ulong>> PlayerLabels = new List<KeyValuePair<ISleekLabel, ulong>>(16);
    public bool IsActive { get; private set; }
    public static DevkitEditorHUD? Instance { get; private set; }
    private void UpdateInfoHeight()
    {
        InfoBox.sizeOffset_Y = 70 + PlayerLabels.Count * 25;
        InfoBox.positionOffset_Y = -InfoBox.sizeOffset_Y;
    }
    private static ISleekLabel CreateListLabelForPlayer(EditorUser user, int index)
    {
        ISleekLabel label = Glazier.Get().CreateLabel();
        
        label.positionOffset_X = 10;
        label.positionOffset_Y = 65 + 25 * index;
        label.sizeOffset_Y = 20;
        label.sizeScale_X = 1f;
        label.text = "• " + user.DisplayName;

        return label;
    }

    [UsedImplicitly]
    private void Awake()
    {
        IsActive = false;
        UserManager.OnUserConnected += OnUserConnected;
        UserManager.OnUserDisconnected += OnUserDisconnected;
        UserInput.OnUserPositionUpdated += OnUserPositionUpdated;
        Instance = this;
        Container = new SleekFullscreenBox
        {
            positionScale_X = 1f,
            sizeScale_X = 1f,
            sizeScale_Y = 1f
        };
        EditorUI.window.AddChild(Container);
        InfoBox = Glazier.Get().CreateBox();
        InfoBox.positionScale_X = 0.8f;
        InfoBox.positionScale_Y = 1f;
        InfoBox.positionOffset_Y = -75;
        InfoBox.sizeOffset_Y = 75;
        InfoBox.sizeScale_X = 0.2f;
        MapLabel = Glazier.Get().CreateLabel();
        MapLabel.positionOffset_X = 5;
        MapLabel.positionOffset_Y = 5;
        MapLabel.sizeOffset_Y = 20;
        MapLabel.sizeScale_X = 1f;
        MapLabel.text = Level.info.getLocalizedName();
        UsersLabel = Glazier.Get().CreateLabel();
        UsersLabel.positionOffset_X = 5;
        UsersLabel.positionOffset_Y = 35;
        UsersLabel.sizeOffset_Y = 20;
        UsersLabel.sizeScale_X = 1f;
        UsersLabel.text = "<b>Users</b>";
        UsersLabel.enableRichText = true;
        InfoBox.AddChild(MapLabel);
        InfoBox.AddChild(UsersLabel);
        for (int i = 0; i < UserManager.Users.Count; ++i)
        {
            EditorUser user = UserManager.Users[i];
            ISleekLabel label = CreateListLabelForPlayer(user, i);
            PlayerLabels.Add(new KeyValuePair<ISleekLabel, ulong>(label, user.SteamId.m_SteamID));
            InfoBox.AddChild(label);
        }
        UpdateInfoHeight();
        UpdateAllNametags();
        Container.AddChild(InfoBox);
        Logger.LogDebug("Inited hud");
    }

    [UsedImplicitly]
    private void OnDestroy()
    {
        UserManager.OnUserConnected -= OnUserConnected;
        UserManager.OnUserDisconnected -= OnUserDisconnected;
        UserInput.OnUserPositionUpdated -= OnUserPositionUpdated;
        Container.RemoveAllChildren();
        Nametags.Clear();
        EditorUI.window.RemoveChild(Container);
        if (Instance == this)
            Instance = null;
    }

    internal void OnUserPositionUpdated(EditorUser user)
    {
        if (user == EditorUser.User)
        {
            UpdateAllNametags();
            return;
        }
        for (int i = 0; i < Nametags.Count; ++i)
        {
            if (Nametags[i].Value == user.SteamId.m_SteamID)
            {
                UpdateNametag(Nametags[i].Key, user);
                return;
            }
        }

        CreateNametag(user);
    }
    private void CreateNametag(EditorUser user)
    {
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
        Nametags.Add(new KeyValuePair<ISleekLabel, ulong>(label, user.SteamId.m_SteamID));
    }
    
    private void UpdateNametag(ISleekLabel nametag, EditorUser user)
    {
        if (!IsActive)
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
        try
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
            UpdateInfoHeight();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);
        }
    }

    private void OnUserDisconnected(EditorUser user)
    {
        for (int i = 0; i < Nametags.Count; ++i)
        {
            if (Nametags[i].Value == user.SteamId.m_SteamID)
            {
                ISleekLabel lbl = Nametags[i].Key;
                Nametags.RemoveAt(i);
                Container.RemoveChild(lbl);
                break;
            }
        }
        for (int i = 0; i < PlayerLabels.Count; ++i)
        {
            if (PlayerLabels[i].Value == user.SteamId.m_SteamID)
            {
                ISleekLabel lbl = PlayerLabels[i].Key;
                PlayerLabels.RemoveAt(i);
                InfoBox.RemoveChild(lbl);
                UpdateInfoHeight();
                break;
            }
        }
    }
    internal void UpdateAllNametags()
    {
        for (int p = 0; p < UserManager.Users.Count; ++p)
        {
            bool found = false;
            EditorUser u = UserManager.Users[p];
            if (u.IsOwner) continue;
            for (int i = 0; i < Nametags.Count; ++i)
            {
                if (Nametags[i].Value == u.SteamId.m_SteamID)
                {
                    UpdateNametag(Nametags[i].Key, u);
                    found = true;
                    break;
                }
            }

            if (!found)
                CreateNametag(u);
        }
    }

    internal static void Open()
    {
        Logger.LogDebug("Opening HUD");

        if (Instance == null)
        {
            DevkitServerModule.GameObjectHost.AddComponent<DevkitEditorHUD>();
        }
        else if (EditorUI.window == null || EditorUI.window.FindIndexOfChild(Instance.Container) == -1)
        {
            Logger.LogDebug("Expired container parent for editor HUD.");
            Close(true);
            DevkitServerModule.GameObjectHost.AddComponent<DevkitEditorHUD>();
        }
        if (Instance!.IsActive)
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
            Instance.Container.AnimateOutOfView(1f, 0f);
        }
        Logger.LogDebug(" Closed HUD");
        Instance.IsActive = false;
        Instance.UpdateAllNametags();
    }
}
#endif