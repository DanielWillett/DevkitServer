#if CLIENT
using DanielWillett.ReflectionTools;
using DanielWillett.ReflectionTools.Emit;
using DevkitServer.Core.UI.Handlers;
using HarmonyLib;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Reflection.Emit;

namespace DevkitServer.API.UI;

[HarmonyPatch]
public static class UIAccessTools
{
    internal const string Source = "UI TOOLS";

    private static readonly CachedMulticastEvent<InitializingUIInfo> EventOnInitializingUIInfo = new CachedMulticastEvent<InitializingUIInfo>(typeof(UIAccessTools), nameof(OnInitializingUIInfo));

    private static readonly StaticGetter<EditorUI?> GetEditorUIInstance
        = Accessor.GenerateStaticGetter<EditorUI, EditorUI?>("instance", throwOnError: true)!;

    private static readonly StaticGetter<PlayerUI?> GetPlayerUIInstance
        = Accessor.GenerateStaticGetter<PlayerUI, PlayerUI?>("instance", throwOnError: true)!;

    private static readonly StaticGetter<MenuUI?>? GetMenuUIInstance
        = Accessor.GenerateStaticGetter<MenuUI, MenuUI?>("instance");

    private static readonly InstanceGetter<EditorUI, EditorDashboardUI?> GetEditorDashboardUIInstance
        = Accessor.GenerateInstanceGetter<EditorUI, EditorDashboardUI?>("dashboardUI", throwOnError: true)!;

    private static readonly InstanceGetter<EditorDashboardUI, EditorEnvironmentUI?> GetEditorEnvironmentUIInstance
        = Accessor.GenerateInstanceGetter<EditorDashboardUI, EditorEnvironmentUI?>("environmentUI", throwOnError: true)!;

    private static readonly InstanceGetter<EditorDashboardUI, EditorTerrainUI?> GetEditorTerrainUIInstance
        = Accessor.GenerateInstanceGetter<EditorDashboardUI, EditorTerrainUI?>("terrainMenu", throwOnError: true)!;

    private static readonly InstanceGetter<EditorDashboardUI, EditorLevelUI?> GetEditorLevelUIInstance
        = Accessor.GenerateInstanceGetter<EditorDashboardUI, EditorLevelUI?>("levelUI", throwOnError: true)!;

    private static readonly InstanceGetter<EditorLevelUI, EditorLevelObjectsUI?> GetEditorLevelObjectsUIInstance
        = Accessor.GenerateInstanceGetter<EditorLevelUI, EditorLevelObjectsUI?>("objectsUI", throwOnError: true)!;

    private static readonly InstanceGetter<PlayerUI, object?>? GetPlayerBrowserRequestUI
        = Accessor.GenerateInstanceGetter<PlayerUI, object?>("browserRequestUI");

    private static readonly InstanceGetter<PlayerUI, object?>? GetPlayerGroupUI
        = Accessor.GenerateInstanceGetter<PlayerUI, object?>("groupUI");

    private static readonly InstanceGetter<PlayerUI, PlayerBarricadeMannequinUI?>? GetPlayerBarricadeMannequinUI
        = Accessor.GenerateInstanceGetter<PlayerUI, PlayerBarricadeMannequinUI?>("mannequinUI");

    private static readonly InstanceGetter<PlayerUI, PlayerBarricadeStereoUI?>? GetPlayerBarricadeStereoUI
        = Accessor.GenerateInstanceGetter<PlayerUI, PlayerBarricadeStereoUI?>("boomboxUI");

    private static readonly InstanceGetter<PlayerUI, PlayerDashboardUI?>? GetPlayerDashboardUI
        = Accessor.GenerateInstanceGetter<PlayerUI, PlayerDashboardUI?>("dashboardUI");

    private static readonly InstanceGetter<PlayerUI, PlayerPauseUI?>? GetPlayerPauseUI
        = Accessor.GenerateInstanceGetter<PlayerUI, PlayerPauseUI?>("pauseUI");

    private static readonly InstanceGetter<PlayerUI, PlayerLifeUI?>? GetPlayerLifeUI
        = Accessor.GenerateInstanceGetter<PlayerUI, PlayerLifeUI?>("lifeUI");

    private static readonly InstanceGetter<PlayerDashboardUI, PlayerDashboardInformationUI?>? GetPlayerDashboardInformationUI
        = Accessor.GenerateInstanceGetter<PlayerDashboardUI, PlayerDashboardInformationUI?>("infoUI");

    private static readonly InstanceGetter<MenuDashboardUI, MenuPauseUI?>? GetMenuPauseUI
        = Accessor.GenerateInstanceGetter<MenuDashboardUI, MenuPauseUI?>("pauseUI");

    private static readonly InstanceGetter<MenuDashboardUI, MenuCreditsUI?>? GetMenuCreditsUI
        = Accessor.GenerateInstanceGetter<MenuDashboardUI, MenuCreditsUI?>("creditsUI");

    private static readonly InstanceGetter<MenuDashboardUI, MenuTitleUI?>? GetMenuTitleUI
        = Accessor.GenerateInstanceGetter<MenuDashboardUI, MenuTitleUI?>("titleUI");

    private static readonly InstanceGetter<MenuDashboardUI, MenuPlayUI?>? GetMenuPlayUI
        = Accessor.GenerateInstanceGetter<MenuDashboardUI, MenuPlayUI?>("playUI");

    private static readonly InstanceGetter<MenuDashboardUI, MenuSurvivorsUI?>? GetMenuSurvivorsUI
        = Accessor.GenerateInstanceGetter<MenuDashboardUI, MenuSurvivorsUI?>("survivorsUI");

    private static readonly InstanceGetter<MenuDashboardUI, MenuConfigurationUI?>? GetMenuConfigurationUI
        = Accessor.GenerateInstanceGetter<MenuDashboardUI, MenuConfigurationUI?>("configUI");

    private static readonly InstanceGetter<MenuDashboardUI, MenuWorkshopUI?>? GetMenuWorkshopUI
        = Accessor.GenerateInstanceGetter<MenuDashboardUI, MenuWorkshopUI?>("workshopUI");

    private static readonly InstanceGetter<MenuUI, MenuDashboardUI?>? GetMenuDashboardUI
        = Accessor.GenerateInstanceGetter<MenuUI, MenuDashboardUI?>("dashboard");

    private static readonly InstanceGetter<MenuPlayUI, MenuPlayConnectUI?>? GetMenuPlayConnectUI
        = Accessor.GenerateInstanceGetter<MenuPlayUI, MenuPlayConnectUI?>("connectUI");

    private static readonly InstanceGetter<MenuPlayUI, MenuPlayServerInfoUI?>? GetMenuPlayServerInfoUI
        = Accessor.GenerateInstanceGetter<MenuPlayUI, MenuPlayServerInfoUI?>("serverInfoUI");

    private static readonly InstanceGetter<MenuPlayUI, MenuPlaySingleplayerUI?>? GetMenuPlaySingleplayerUI
        = Accessor.GenerateInstanceGetter<MenuPlayUI, MenuPlaySingleplayerUI?>("singleplayerUI");

    private static readonly InstanceGetter<MenuPlayServerCurationUI, object?>? GetMenuPlayServerCurationRulesUI
        = Accessor.GenerateInstanceGetter<MenuPlayServerCurationUI, object?>("rulesUI");

    private static readonly InstanceGetter<MenuPlayUI, MenuPlayLobbiesUI?>? GetMenuPlayLobbiesUI
        = Accessor.GenerateInstanceGetter<MenuPlayUI, MenuPlayLobbiesUI?>("lobbiesUI");

    private static readonly StaticGetter<MenuServerPasswordUI?>? GetMenuServerPasswordUI
        = Accessor.GenerateStaticGetter<MenuPlayServerInfoUI, MenuServerPasswordUI?>("passwordUI");

    private static readonly InstanceGetter<MenuSurvivorsUI, MenuSurvivorsCharacterUI?>? GetMenuSurvivorsCharacterUI
        = Accessor.GenerateInstanceGetter<MenuSurvivorsUI, MenuSurvivorsCharacterUI?>("characterUI");

    private static readonly InstanceGetter<MenuSurvivorsUI, MenuSurvivorsAppearanceUI?>? GetMenuSurvivorsAppearanceUI
        = Accessor.GenerateInstanceGetter<MenuSurvivorsUI, MenuSurvivorsAppearanceUI?>("appearanceUI");

    private static readonly InstanceGetter<MenuSurvivorsUI, MenuSurvivorsGroupUI?>? GetMenuSurvivorsGroupUI
        = Accessor.GenerateInstanceGetter<MenuSurvivorsUI, MenuSurvivorsGroupUI?>("groupUI");

    private static readonly InstanceGetter<MenuSurvivorsUI, MenuSurvivorsClothingUI?>? GetMenuSurvivorsClothingUI
        = Accessor.GenerateInstanceGetter<MenuSurvivorsUI, MenuSurvivorsClothingUI?>("clothingUI");

    private static readonly InstanceGetter<MenuSurvivorsClothingUI, MenuSurvivorsClothingItemUI?>? GetMenuSurvivorsClothingItemUI
        = Accessor.GenerateInstanceGetter<MenuSurvivorsClothingUI, MenuSurvivorsClothingItemUI?>("itemUI");

    private static readonly InstanceGetter<MenuSurvivorsClothingUI, MenuSurvivorsClothingInspectUI?>? GetMenuSurvivorsClothingInspectUI
        = Accessor.GenerateInstanceGetter<MenuSurvivorsClothingUI, MenuSurvivorsClothingInspectUI?>("inspectUI");

    private static readonly InstanceGetter<MenuSurvivorsClothingUI, MenuSurvivorsClothingDeleteUI?>? GetMenuSurvivorsClothingDeleteUI
        = Accessor.GenerateInstanceGetter<MenuSurvivorsClothingUI, MenuSurvivorsClothingDeleteUI?>("deleteUI");

    private static readonly InstanceGetter<MenuSurvivorsClothingUI, MenuSurvivorsClothingBoxUI?>? GetMenuSurvivorsClothingBoxUI
        = Accessor.GenerateInstanceGetter<MenuSurvivorsClothingUI, MenuSurvivorsClothingBoxUI?>("boxUI");

    private static readonly InstanceGetter<MenuSurvivorsClothingUI, object?>? GetItemStoreMenu
        = Accessor.GenerateInstanceGetter<MenuSurvivorsClothingUI, object?>("itemStoreUI");

    private static readonly InstanceGetter<MenuWorkshopUI, MenuWorkshopSubmitUI?>? GetMenuWorkshopSubmitUI
        = Accessor.GenerateInstanceGetter<MenuWorkshopUI, MenuWorkshopSubmitUI?>("submitUI");

    private static readonly InstanceGetter<MenuWorkshopUI, MenuWorkshopEditorUI?>? GetMenuWorkshopEditorUI
        = Accessor.GenerateInstanceGetter<MenuWorkshopUI, MenuWorkshopEditorUI?>("editorUI");

    private static readonly InstanceGetter<MenuWorkshopUI, MenuWorkshopErrorUI?>? GetMenuWorkshopErrorUI
        = Accessor.GenerateInstanceGetter<MenuWorkshopUI, MenuWorkshopErrorUI?>("errorUI");

    private static readonly InstanceGetter<MenuWorkshopUI, MenuWorkshopLocalizationUI?>? GetMenuWorkshopLocalizationUI
        = Accessor.GenerateInstanceGetter<MenuWorkshopUI, MenuWorkshopLocalizationUI?>("localizationUI");

    private static readonly InstanceGetter<MenuWorkshopUI, MenuWorkshopSpawnsUI?>? GetMenuWorkshopSpawnsUI
        = Accessor.GenerateInstanceGetter<MenuWorkshopUI, MenuWorkshopSpawnsUI?>("spawnsUI");

    private static readonly InstanceGetter<MenuWorkshopUI, MenuWorkshopSubscriptionsUI?>? GetMenuWorkshopSubscriptionsUI
        = Accessor.GenerateInstanceGetter<MenuWorkshopUI, MenuWorkshopSubscriptionsUI?>("subscriptionsUI");

    private static readonly StaticGetter<ISleekButton>? GetLoadingUICancelButton
        = Accessor.GenerateStaticGetter<ISleekButton>(typeof(LoadingUI), "cancelButton", throwOnError: false);

    private static readonly StaticGetter<ISleekBox>? GetLoadingBarBox
        = Accessor.GenerateStaticGetter<ISleekBox>(typeof(LoadingUI), "loadingBarBox", throwOnError: false);

    private static readonly Dictionary<Type, Type> MenuTypes = new Dictionary<Type, Type>(32);

    private static readonly Func<object?>? GetEditorTerrainHeightUI;
    private static readonly Func<object?>? GetEditorTerrainMaterialsUI;
    private static readonly Func<object?>? GetEditorTerrainDetailsUI;
    private static readonly Func<object?>? GetEditorTerrainTilesUI;

    private static readonly Func<object?>? GetEditorEnvironmentNodesUI;

    private static readonly Func<object?>? GetEditorVolumesUI;

    private static readonly Func<object?>? GetItemStoreCartMenu;
    private static readonly Func<object?>? GetItemStoreDetailsMenu;
    private static readonly Func<object?>? GetItemStoreBundleContentsMenu;

    public static EditorUI? EditorUI
    {
        get
        {
            EditorUI? editorUI = GetEditorUIInstance();
            return editorUI == null ? null : editorUI;
        }
    }

    public static PlayerUI? PlayerUI
    {
        get
        {
            PlayerUI? playerUI = GetPlayerUIInstance();
            return playerUI == null ? null : playerUI;
        }
    }

    public static MenuUI? MenuUI
    {
        get
        {
            MenuUI? menuUi = GetMenuUIInstance?.Invoke();
            return menuUi == null ? null : menuUi;
        }
    }

    public static LoadingUI? LoadingUI
    {
        get
        {
            if (_loadingUI == null)
            {
                if (!DevkitServerModule.IsMainThread)
                    return null;
                GameObject? host = LoadingUI.loader;
                if (host != null)
                    _loadingUI = host.GetComponent<LoadingUI>();
            }

            return _loadingUI;
        }
    }

    public static event Action? EditorUIReady;
    public static event Action? PlayerUIReady;
    public static EditorDashboardUI? EditorDashboardUI
    {
        get
        {
            EditorUI? editorUi = EditorUI;
            return editorUi == null ? null : GetEditorDashboardUIInstance(editorUi);
        }
    }
    public static EditorEnvironmentUI? EditorEnvironmentUI
    {
        get
        {
            EditorDashboardUI? dashboard = EditorDashboardUI;
            return dashboard == null ? null : GetEditorEnvironmentUIInstance(dashboard);
        }
    }
    public static Type? EditorEnvironmentNodesUIType { get; }
    public static object? EditorEnvironmentNodesUI => GetEditorEnvironmentNodesUI?.Invoke();
    public static EditorTerrainUI? EditorTerrainUI
    {
        get
        {
            EditorDashboardUI? dashboard = EditorDashboardUI;
            return dashboard == null ? null : GetEditorTerrainUIInstance(dashboard);
        }
    }
    public static object? EditorTerrainHeightUI => GetEditorTerrainHeightUI?.Invoke();
    public static Type? EditorTerrainHeightUIType { get; }
    public static object? EditorTerrainMaterialsUI => GetEditorTerrainMaterialsUI?.Invoke();
    public static Type? EditorTerrainMaterialsUIType { get; }
    public static object? EditorTerrainDetailsUI => GetEditorTerrainDetailsUI?.Invoke();
    public static Type? EditorTerrainDetailsUIType { get; }
    public static object? EditorTerrainTilesUI => GetEditorTerrainTilesUI?.Invoke();
    public static Type? EditorTerrainTilesUIType { get; }
    public static EditorLevelUI? EditorLevelUI
    {
        get
        {
            EditorDashboardUI? dashboard = EditorDashboardUI;
            return dashboard == null ? null : GetEditorLevelUIInstance(dashboard);
        }
    }
    public static EditorLevelObjectsUI? EditorLevelObjectsUI
    {
        get
        {
            EditorLevelUI? level = EditorLevelUI;
            return level == null ? null : GetEditorLevelObjectsUIInstance(level);
        }
    }
    public static object? EditorVolumesUI => GetEditorVolumesUI?.Invoke();
    public static Type? EditorVolumesUIType { get; }
    public static object? PlayerBrowserRequestUI
    {
        get
        {
            PlayerUI? playerUI = PlayerUI;
            return playerUI != null ? GetPlayerBrowserRequestUI?.Invoke(playerUI) : null;
        }
    }

    public static Type? PlayerBrowserRequestUIType { get; } = AccessorExtensions.AssemblyCSharp.GetType("SDG.Unturned.PlayerBrowserRequestUI", false);
    public static PlayerBarricadeStereoUI? PlayerBarricadeStereoUI
    {
        get
        {
            PlayerUI? playerUI = PlayerUI;
            return playerUI != null ? GetPlayerBarricadeStereoUI?.Invoke(playerUI) : null;
        }
    }
    public static PlayerBarricadeMannequinUI? PlayerBarricadeMannequinUI
    {
        get
        {
            PlayerUI? playerUI = PlayerUI;
            return playerUI != null ? GetPlayerBarricadeMannequinUI?.Invoke(playerUI) : null;
        }
    }
    public static Type? PlayerGroupUIType { get; } = AccessorExtensions.AssemblyCSharp.GetType("SDG.Unturned.PlayerGroupUI", false);
    public static object? PlayerGroupUI
    {
        get
        {
            PlayerUI? playerUI = PlayerUI;
            return playerUI != null ? GetPlayerGroupUI?.Invoke(playerUI) : null;
        }
    }
    public static PlayerDashboardUI? PlayerDashboardUI
    {
        get
        {
            PlayerUI? playerUI = PlayerUI;
            return playerUI != null ? GetPlayerDashboardUI?.Invoke(playerUI) : null;
        }
    }
    public static PlayerPauseUI? PlayerPauseUI
    {
        get
        {
            PlayerUI? playerUI = PlayerUI;
            return playerUI != null ? GetPlayerPauseUI?.Invoke(playerUI) : null;
        }
    }
    public static PlayerLifeUI? PlayerLifeUI
    {
        get
        {
            PlayerUI? playerUI = PlayerUI;
            return playerUI != null ? GetPlayerLifeUI?.Invoke(playerUI) : null;
        }
    }
    public static PlayerDashboardInformationUI? PlayerDashboardInformationUI
    {
        get
        {
            PlayerDashboardUI? playerDashboardUI = PlayerDashboardUI;
            return playerDashboardUI != null ? GetPlayerDashboardInformationUI?.Invoke(playerDashboardUI) : null;
        }
    }
    public static MenuDashboardUI? MenuDashboardUI
    {
        get
        {
            MenuUI? menuUI = MenuUI;
            return menuUI != null ? GetMenuDashboardUI?.Invoke(menuUI) : null;
        }
    }
    public static MenuPauseUI? MenuPauseUI
    {
        get
        {
            MenuDashboardUI? menuDashboardUI = MenuDashboardUI;
            return menuDashboardUI != null ? GetMenuPauseUI?.Invoke(menuDashboardUI) : null;
        }
    }
    public static MenuCreditsUI? MenuCreditsUI
    {
        get
        {
            MenuDashboardUI? menuDashboardUI = MenuDashboardUI;
            return menuDashboardUI != null ? GetMenuCreditsUI?.Invoke(menuDashboardUI) : null;
        }
    }
    public static MenuTitleUI? MenuTitleUI
    {
        get
        {
            MenuDashboardUI? menuDashboardUI = MenuDashboardUI;
            return menuDashboardUI != null ? GetMenuTitleUI?.Invoke(menuDashboardUI) : null;
        }
    }
    public static MenuPlayUI? MenuPlayUI
    {
        get
        {
            MenuDashboardUI? menuDashboardUI = MenuDashboardUI;
            return menuDashboardUI != null ? GetMenuPlayUI?.Invoke(menuDashboardUI) : null;
        }
    }
    public static MenuSurvivorsUI? MenuSurvivorsUI
    {
        get
        {
            MenuDashboardUI? menuDashboardUI = MenuDashboardUI;
            return menuDashboardUI != null ? GetMenuSurvivorsUI?.Invoke(menuDashboardUI) : null;
        }
    }
    public static MenuConfigurationUI? MenuConfigurationUI
    {
        get
        {
            MenuDashboardUI? menuDashboardUI = MenuDashboardUI;
            return menuDashboardUI != null ? GetMenuConfigurationUI?.Invoke(menuDashboardUI) : null;
        }
    }
    public static MenuWorkshopUI? MenuWorkshopUI
    {
        get
        {
            MenuDashboardUI? menuDashboardUI = MenuDashboardUI;
            return menuDashboardUI != null ? GetMenuWorkshopUI?.Invoke(menuDashboardUI) : null;
        }
    }
    public static MenuPlayConnectUI? MenuPlayConnectUI
    {
        get
        {
            MenuPlayUI? menuPlayUI = MenuPlayUI;
            return menuPlayUI != null ? GetMenuPlayConnectUI?.Invoke(menuPlayUI) : null;
        }
    }

    [Obsolete("Just use MenuPlayUI.serverListUI to access.")]
    public static MenuPlayServersUI? MenuPlayServersUI => MenuPlayUI.serverListUI;

    public static MenuPlayServerInfoUI? MenuPlayServerInfoUI
    {
        get
        {
            MenuPlayUI? menuPlayUI = MenuPlayUI;
            return menuPlayUI != null ? GetMenuPlayServerInfoUI?.Invoke(menuPlayUI) : null;
        }
    }
    public static MenuPlaySingleplayerUI? MenuPlaySingleplayerUI
    {
        get
        {
            MenuPlayUI? menuPlayUI = MenuPlayUI;
            return menuPlayUI != null ? GetMenuPlaySingleplayerUI?.Invoke(menuPlayUI) : null;
        }
    }
    public static Type? MenuPlayServerCurationRulesUIType { get; } = typeof(Provider).Assembly.GetType("SDG.Unturned.MenuPlayServerCurationRulesUI", false);

    public static object? MenuPlayServerCurationRulesUI
    {
        get
        {
            MenuPlayServerCurationUI? curationUI = MenuPlayServersUI.serverCurationUI;
            return curationUI != null ? GetMenuPlayServerCurationRulesUI?.Invoke(curationUI) : null;
        }
    }
    public static MenuPlayLobbiesUI? MenuPlayLobbiesUI
    {
        get
        {
            MenuPlayUI? menuPlayUI = MenuPlayUI;
            return menuPlayUI != null ? GetMenuPlayLobbiesUI?.Invoke(menuPlayUI) : null;
        }
    }
    public static MenuServerPasswordUI? MenuServerPasswordUI => GetMenuServerPasswordUI?.Invoke();
    public static MenuSurvivorsCharacterUI? MenuSurvivorsCharacterUI
    {
        get
        {
            MenuSurvivorsUI? menuSurvivorsUI = MenuSurvivorsUI;
            return menuSurvivorsUI != null ? GetMenuSurvivorsCharacterUI?.Invoke(menuSurvivorsUI) : null;
        }
    }
    public static MenuSurvivorsAppearanceUI? MenuSurvivorsAppearanceUI
    {
        get
        {
            MenuSurvivorsUI? menuSurvivorsUI = MenuSurvivorsUI;
            return menuSurvivorsUI != null ? GetMenuSurvivorsAppearanceUI?.Invoke(menuSurvivorsUI) : null;
        }
    }
    public static MenuSurvivorsGroupUI? MenuSurvivorsGroupUI
    {
        get
        {
            MenuSurvivorsUI? menuSurvivorsUI = MenuSurvivorsUI;
            return menuSurvivorsUI != null ? GetMenuSurvivorsGroupUI?.Invoke(menuSurvivorsUI) : null;
        }
    }
    public static MenuSurvivorsClothingUI? MenuSurvivorsClothingUI
    {
        get
        {
            MenuSurvivorsUI? menuSurvivorsUI = MenuSurvivorsUI;
            return menuSurvivorsUI != null ? GetMenuSurvivorsClothingUI?.Invoke(menuSurvivorsUI) : null;
        }
    }
    public static MenuSurvivorsClothingItemUI? MenuSurvivorsClothingItemUI
    {
        get
        {
            MenuSurvivorsClothingUI? menuSurvivorsClothingUI = MenuSurvivorsClothingUI;
            return menuSurvivorsClothingUI != null ? GetMenuSurvivorsClothingItemUI?.Invoke(menuSurvivorsClothingUI) : null;
        }
    }
    public static MenuSurvivorsClothingInspectUI? MenuSurvivorsClothingInspectUI
    {
        get
        {
            MenuSurvivorsClothingUI? menuSurvivorsClothingUI = MenuSurvivorsClothingUI;
            return menuSurvivorsClothingUI != null ? GetMenuSurvivorsClothingInspectUI?.Invoke(menuSurvivorsClothingUI) : null;
        }
    }
    public static MenuSurvivorsClothingDeleteUI? MenuSurvivorsClothingDeleteUI
    {
        get
        {
            MenuSurvivorsClothingUI? menuSurvivorsClothingUI = MenuSurvivorsClothingUI;
            return menuSurvivorsClothingUI != null ? GetMenuSurvivorsClothingDeleteUI?.Invoke(menuSurvivorsClothingUI) : null;
        }
    }
    public static MenuSurvivorsClothingBoxUI? MenuSurvivorsClothingBoxUI
    {
        get
        {
            MenuSurvivorsClothingUI? menuSurvivorsClothingUI = MenuSurvivorsClothingUI;
            return menuSurvivorsClothingUI != null ? GetMenuSurvivorsClothingBoxUI?.Invoke(menuSurvivorsClothingUI) : null;
        }
    }
    public static Type? ItemStoreMenuType { get; } = AccessorExtensions.AssemblyCSharp.GetType("SDG.Unturned.ItemStoreMenu", false);
    public static object? ItemStoreMenu
    {
        get
        {
            MenuSurvivorsClothingUI? menuSurvivorsClothingUI = MenuSurvivorsClothingUI;
            return menuSurvivorsClothingUI != null ? GetItemStoreMenu?.Invoke(menuSurvivorsClothingUI) : null;
        }
    }
    public static Type? ItemStoreCartMenuType { get; } = AccessorExtensions.AssemblyCSharp.GetType("SDG.Unturned.ItemStoreCartMenu", false);
    public static Type? ItemStoreDetailsMenuType { get; } = AccessorExtensions.AssemblyCSharp.GetType("SDG.Unturned.ItemStoreDetailsMenu", false);
    public static Type? ItemStoreBundleContentsMenuType { get; } = AccessorExtensions.AssemblyCSharp.GetType("SDG.Unturned.ItemStoreBundleContentsMenu", false);
    public static object? ItemStoreCartMenu => GetItemStoreCartMenu?.Invoke();
    public static object? ItemStoreDetailsMenu => GetItemStoreDetailsMenu?.Invoke();
    public static object? ItemStoreBundleContentsMenu => GetItemStoreBundleContentsMenu?.Invoke();
    public static MenuWorkshopSubmitUI? MenuWorkshopSubmitUI
    {
        get
        {
            MenuWorkshopUI? menuWorkshopUI = MenuWorkshopUI;
            return menuWorkshopUI != null ? GetMenuWorkshopSubmitUI?.Invoke(menuWorkshopUI) : null;
        }
    }
    public static MenuWorkshopEditorUI? MenuWorkshopEditorUI
    {
        get
        {
            MenuWorkshopUI? menuWorkshopUI = MenuWorkshopUI;
            return menuWorkshopUI != null ? GetMenuWorkshopEditorUI?.Invoke(menuWorkshopUI) : null;
        }
    }
    public static MenuWorkshopErrorUI? MenuWorkshopErrorUI
    {
        get
        {
            MenuWorkshopUI? menuWorkshopUI = MenuWorkshopUI;
            return menuWorkshopUI != null ? GetMenuWorkshopErrorUI?.Invoke(menuWorkshopUI) : null;
        }
    }
    public static MenuWorkshopLocalizationUI? MenuWorkshopLocalizationUI
    {
        get
        {
            MenuWorkshopUI? menuWorkshopUI = MenuWorkshopUI;
            return menuWorkshopUI != null ? GetMenuWorkshopLocalizationUI?.Invoke(menuWorkshopUI) : null;
        }
    }
    public static MenuWorkshopSpawnsUI? MenuWorkshopSpawnsUI
    {
        get
        {
            MenuWorkshopUI? menuWorkshopUI = MenuWorkshopUI;
            return menuWorkshopUI != null ? GetMenuWorkshopSpawnsUI?.Invoke(menuWorkshopUI) : null;
        }
    }
    public static MenuWorkshopSubscriptionsUI? MenuWorkshopSubscriptionsUI
    {
        get
        {
            MenuWorkshopUI? menuWorkshopUI = MenuWorkshopUI;
            return menuWorkshopUI != null ? GetMenuWorkshopSubscriptionsUI?.Invoke(menuWorkshopUI) : null;
        }
    }

    private static Dictionary<Type, UITypeInfo> _typeInfoIntl = null!;
    private static LoadingUI? _loadingUI;

    /// <summary>
    /// Dictionary of vanilla UI types to their type info.
    /// </summary>
    public static IReadOnlyDictionary<Type, UITypeInfo> TypeInfo { get; private set; } = null!;

    /// <summary>
    /// Called on initialization to allow plugins to add their UI types to the <see cref="TypeInfo"/> dictionary before it's finalized (allowing them to be extended).<br/>
    /// This needs to be subscribed to on plugin load for it to be called.
    /// </summary>
    /// <remarks>While removing or replacing info is possible, it's not recommended as it may mess with DevkitServer features or features of other plugins.</remarks>
    public static event InitializingUIInfo OnInitializingUIInfo
    {
        add => EventOnInitializingUIInfo.Add(value);
        remove => EventOnInitializingUIInfo.Remove(value);
    }

    /// <summary>
    /// Get information about a vanilla UI type, or <see langword="null"/> if there's no information registered.
    /// </summary>
    public static UITypeInfo? GetTypeInfo(Type type) => _typeInfoIntl.GetValueOrDefault(type);

    /// <exception cref="ArgumentOutOfRangeException">Invalid enum.</exception>
    /// <exception cref="MemberAccessException">Type or field not found (or invalid).</exception>
    public static Func<TValue?>? CreateUIFieldGetterReturn<TValue, TVanillaUI>(string fieldName, bool throwOnFailure = true, string? altName = null) where TVanillaUI : class
        => CreateUIFieldGetterDelegate<Func<TValue?>>(typeof(TVanillaUI), fieldName, throwOnFailure, altName, typeof(TValue));

    /// <exception cref="ArgumentOutOfRangeException">Invalid enum.</exception>
    /// <exception cref="MemberAccessException">Type or field not found (or invalid).</exception>
    public static Func<TValue?>? CreateUIFieldGetterReturn<TValue>(Type? uiType, string fieldName, bool throwOnFailure = true, string? altName = null)
        => CreateUIFieldGetterDelegate<Func<TValue?>>(uiType, fieldName, throwOnFailure, altName, typeof(TValue));

    /// <exception cref="ArgumentOutOfRangeException">Invalid enum.</exception>
    /// <exception cref="MemberAccessException">Type or field not found (or invalid).</exception>
    public static TDelegate? CreateUIFieldGetterDelegate<TDelegate, TVanillaUI>(string fieldName, bool throwOnFailure = true, string? altName = null, Type? rtnType = null) where TDelegate : Delegate where TVanillaUI : class
        => CreateUIFieldGetterDelegate<TDelegate>(typeof(TVanillaUI), fieldName, throwOnFailure, altName, rtnType);

    /// <exception cref="ArgumentOutOfRangeException">Invalid enum.</exception>
    /// <exception cref="MemberAccessException">Type or field not found (or invalid).</exception>
    public static TDelegate? CreateUIFieldGetterDelegate<TDelegate>(Type? uiType, string fieldName, bool throwOnFailure = true, string? altName = null, Type? rtnType = null) where TDelegate : Delegate
    {
        MemberInfo? field = null;
        try
        {
            Type accessTools = typeof(UIAccessTools);
            const MethodAttributes attr = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig;
            if (uiType == null)
            {
                Logger.DevkitServer.LogWarning(Source, "Unable to find type for field " + fieldName.Format() + ".");
                DevkitServerModule.Fault();
                if (throwOnFailure)
                    throw new MemberAccessException("Unable to find type for field: \"" + fieldName + "\".");
                return null;
            }
            field = uiType.GetField(fieldName, BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null)
                field = uiType.GetProperty(fieldName, BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null && !string.IsNullOrEmpty(altName))
                field = uiType.GetField(altName, BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null && !string.IsNullOrEmpty(altName))
                field = uiType.GetProperty(altName, BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Type? memberType = field is FieldInfo f2 ? f2.FieldType : field is PropertyInfo p2 ? p2.PropertyType : null;
            if (memberType == null || field == null || field is PropertyInfo prop && (prop.GetIndexParameters() is { Length: > 0 } || prop.GetGetMethod(true) == null))
            {
                Logger.DevkitServer.LogWarning(Source, "Unable to find field or property: " + uiType.Format() + "." + fieldName.Colorize(Color.red) + ".");
                DevkitServerModule.Fault();
                if (throwOnFailure)
                    throw new MemberAccessException("Unable to find field or property: \"" + uiType.Name + "." + fieldName + "\".");
                return null;
            }
            if (rtnType != null && !rtnType.IsAssignableFrom(memberType))
            {
                Logger.DevkitServer.LogWarning(Source, "Field or property " + field.Format() + " is not assignable to " + rtnType.Format() + ".");
                DevkitServerModule.Fault();
                if (throwOnFailure)
                    throw new MemberAccessException("Field " + field.DeclaringType?.Name + "." + field.Name + " is not assignable to " + rtnType.Name + ".");
                return null;
            }

            MethodInfo? getter = (field as PropertyInfo)?.GetGetMethod(true);

            if (getter != null && getter.IsStatic)
            {
                try
                {
                    return (TDelegate)getter.CreateDelegate(typeof(TDelegate));
                }
                catch (Exception)
                {
                    Logger.DevkitServer.LogWarning(Source, $"Error creating simplified getter delegate for {((PropertyInfo)field).Format()} UI accessor.");
                    DevkitServerModule.Fault();
                    if (throwOnFailure)
                        throw;
                    return null;
                }
            }

            DynamicMethod method = new DynamicMethod("Get" + uiType.Name + "_Impl", attr,
                CallingConventions.Standard, rtnType ?? memberType,
                Type.EmptyTypes, accessTools, true);
            DebuggableEmitter il = new DebuggableEmitter(method);
            if (field is FieldInfo field2)
            {
                if (field2.IsStatic)
                    il.Emit(OpCodes.Ldsfld, field2);
                else
                {
                    LoadUIToILGenerator(il, uiType);
                    il.Emit(OpCodes.Ldfld, field2);
                }
            }
            else if (field is PropertyInfo property)
            {
                if (getter == null)
                {
                    Logger.DevkitServer.LogWarning(Source, "Property " + property.Format() + " does not have a getter.");
                    DevkitServerModule.Fault();
                    if (throwOnFailure)
                        throw new MemberAccessException("Property \"" + property.DeclaringType?.Name + "." + property.Name + "\" does not have a getter.");
                    return null;
                }
                LoadUIToILGenerator(il, uiType);
                il.Emit(getter.IsVirtual || getter.IsAbstract ? OpCodes.Callvirt : OpCodes.Call, getter);
            }
            else il.Emit(OpCodes.Ldnull);
            if (rtnType != null && rtnType.IsClass && memberType.IsValueType)
                il.Emit(OpCodes.Box, memberType);
            il.Emit(OpCodes.Ret);
            return (TDelegate)method.CreateDelegate(typeof(TDelegate));
        }
        catch (Exception ex)
        {
            if (throwOnFailure)
            {
                Logger.DevkitServer.LogWarning(Source, "Error creating " + ((object?)field ?? fieldName).Format() + " accessor.");
                throw;
            }

            Logger.DevkitServer.LogWarning(Source, ex, "Error creating " + ((object?)field ?? fieldName).Format() + " accessor.");
            return null;
        }
    }

    /// <summary>Gets the menu type from its parent <see cref="VolumeBase"/>.</summary>
    /// <remarks>Includes parent types.</remarks>
    public static bool TryGetVolumeMenuType<T>(out Type menuType) where T : VolumeBase => TryGetMenuType(typeof(T), out menuType);

    /// <summary>Gets the menu type from its parent <see cref="TempNodeBase"/>.</summary>
    /// <remarks>Includes parent types.</remarks>
    public static bool TryGetNodeMenuType<T>(out Type menuType) where T : TempNodeBase => TryGetMenuType(typeof(T), out menuType);

    /// <summary>Gets the menu type from its parent component. This currently only applies to <see cref="VolumeBase"/>'s and <see cref="TempNodeBase"/>'s.</summary>
    /// <remarks>Includes parent types.</remarks>
    public static bool TryGetMenuType(Type volumeType, out Type menuType)
    {
        Type? type2 = volumeType;
        for (; type2 != null; type2 = type2.BaseType)
        {
            if (MenuTypes.TryGetValue(volumeType, out menuType))
                return true;
        }

        menuType = null!;
        return false;
    }

    /// <remarks>Includes parent types.</remarks>
    public static bool TryGetUITypeInfo<T>(out UITypeInfo info) => TryGetUITypeInfo(typeof(T), out info);

    /// <remarks>Includes parent types.</remarks>
    public static bool TryGetUITypeInfo(Type uiType, out UITypeInfo info)
    {
        Type? type2 = uiType;
        for (; type2 != null; type2 = type2.BaseType)
        {
            if (TypeInfo.TryGetValue(uiType, out info))
                return true;
        }

        info = null!;
        return false;
    }
    internal static Type? FindUIType(string typeName)
    {
        if (typeName.IndexOf('.') != -1)
        {
            return typeName.IndexOf(',') != -1 ? Type.GetType(typeName, false, false) : typeof(Provider).Assembly.GetType(typeName, false, false);
        }

        return typeof(Provider).Assembly.GetType("SDG.Unturned." + typeName, false, false);
    }

    /// <summary>
    /// Read a UI type to a <see cref="DebuggableEmitter"/>.
    /// </summary>
    public static void LoadUIToILGenerator<TVanillaUI>(IOpCodeEmitter il) where TVanillaUI : class
        => LoadUIToILGenerator(il, typeof(TVanillaUI));

    /// <summary>
    /// Read a UI type to a <see cref="DebuggableEmitter"/>.
    /// </summary>
    public static void LoadUIToILGenerator(IOpCodeEmitter il, Type uiType)
    {
        UITypeInfo? info = GetTypeInfo(uiType);

        if (info == null)
            throw new ArgumentException(uiType.Name + " is not a valid UI type. If it's new, request it on the GitHub.");

        if (info.IsStaticUI || string.IsNullOrEmpty(info.EmitProperty) && info.CustomEmitter == null)
            throw new InvalidOperationException(uiType.Name + " is not an instanced UI.");

        if (info.CustomEmitter != null)
        {
            info.CustomEmitter(info, il);
            return;
        }

        try
        {
            PropertyInfo? property = typeof(UIAccessTools).GetProperty(info.EmitProperty!, BindingFlags.Public | BindingFlags.Static);
            if (property != null)
            {
                MethodInfo? getter = property.GetGetMethod(true);
                if (getter != null && (uiType.IsAssignableFrom(getter.ReturnType) || getter.ReturnType == typeof(object)))
                {
                    il.Emit(OpCodes.Call, getter);
                    return;
                }
            }
        }
        catch (AmbiguousMatchException)
        {
            // ignored
        }

        throw new Exception($"Unable to find an emittable property at {nameof(UIAccessTools)}.{info.EmitProperty}.");
    }

    public static Delegate? GenerateUICaller<TVanillaUI>(string methodName, Type[]? parameters = null, bool throwOnFailure = false) where TVanillaUI : class
        => GenerateUICaller(typeof(TVanillaUI), methodName, parameters, throwOnFailure);

    public static Delegate? GenerateUICaller(Type? uiType, string methodName, Type[]? parameters = null, bool throwOnFailure = false)
    {
        MethodInfo? method = null;
        if (uiType == null)
        {
            Logger.DevkitServer.LogWarning(Source, "Unable to find type for method " + methodName.Format() + ".");
            DevkitServerModule.Fault();
            if (throwOnFailure)
                throw new MemberAccessException("Unable to find type for method: \"" + methodName + "\".");
            return null;
        }
        if (parameters == null)
        {
            try
            {
                method = uiType.GetMethod(methodName, BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            }
            catch (AmbiguousMatchException)
            {
                // ignored
            }
        }
        else
        {
            method = uiType
                .GetMethod(methodName, BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                    null, CallingConventions.Any, parameters, null);
        }

        if (method == null)
        {
            Logger.DevkitServer.LogError(Source, "Unable to find matching method " + methodName.Format() + ".");
            if (throwOnFailure)
                throw new Exception("Unable to find matching method: " + methodName + ".");
            return null;
        }

        return GenerateUICaller(uiType, method, throwOnFailure);
    }
    public static TDelegate? GenerateUICaller<TDelegate, TVanillaUI>(string methodName, Type[]? parameters = null, bool throwOnFailure = false) where TDelegate : Delegate where TVanillaUI : class
        => GenerateUICaller<TDelegate>(typeof(TVanillaUI), methodName, parameters, throwOnFailure);
    public static TDelegate? GenerateUICaller<TDelegate>(Type? uiType, string methodName, Type[]? parameters = null, bool throwOnFailure = false) where TDelegate : Delegate
    {
        MethodInfo? method = null;
        if (uiType == null)
        {
            Logger.DevkitServer.LogWarning(Source, "Unable to find type for method " + methodName.Format() + ".");
            DevkitServerModule.Fault();
            if (throwOnFailure)
                throw new MemberAccessException("Unable to find type for method: \"" + methodName + "\".");
            return null;
        }
        if (parameters == null)
        {
            try
            {
                method = uiType.GetMethod(methodName, BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            }
            catch (AmbiguousMatchException)
            {
                // ignored
            }
        }
        else
        {
            method = uiType.GetMethod(methodName, BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                    null, CallingConventions.Any, parameters, null);
        }

        if (method == null)
        {
            Logger.DevkitServer.LogError(Source, "Unable to find matching method " + methodName.Format() + ".");
            if (throwOnFailure)
                throw new Exception("Unable to find matching method: " + methodName + ".");
            return null;
        }

        return GenerateUICaller<TDelegate>(uiType, method, throwOnFailure);
    }

    private static Type[] _funcTypes;
    private static Type[] _actionTypes;
    internal static void CheckFuncArrays()
    {
        _funcTypes ??= new Type[]
        {
            typeof (Func<,>),
            typeof (Func<,,>),
            typeof (Func<,,,>),
            typeof (Func<,,,,>),
            typeof (Func<,,,,,>),
            typeof (Func<,,,,,,>),
            typeof (Func<,,,,,,,>),
            typeof (Func<,,,,,,,,>),
            typeof (Func<,,,,,,,,,>),
            typeof (Func<,,,,,,,,,,>),
            typeof (Func<,,,,,,,,,,,>),
            typeof (Func<,,,,,,,,,,,,>),
            typeof (Func<,,,,,,,,,,,,,>),
            typeof (Func<,,,,,,,,,,,,,,>),
            typeof (Func<,,,,,,,,,,,,,,,>),
            typeof (Func<,,,,,,,,,,,,,,,,>)
        };

        _actionTypes ??= new Type[16]
        {
            typeof (Action<>),
            typeof (Action<,>),
            typeof (Action<,,>),
            typeof (Action<,,,>),
            typeof (Action<,,,,>),
            typeof (Action<,,,,,>),
            typeof (Action<,,,,,,>),
            typeof (Action<,,,,,,,>),
            typeof (Action<,,,,,,,,>),
            typeof (Action<,,,,,,,,,>),
            typeof (Action<,,,,,,,,,,>),
            typeof (Action<,,,,,,,,,,,>),
            typeof (Action<,,,,,,,,,,,,>),
            typeof (Action<,,,,,,,,,,,,,>),
            typeof (Action<,,,,,,,,,,,,,,>),
            typeof (Action<,,,,,,,,,,,,,,,>)
        };
    }
    public static Delegate? GenerateUICaller(Type? uiType, MethodInfo method, bool throwOnFailure = false)
    {
        if (_funcTypes == null || _actionTypes == null)
            CheckFuncArrays();

        bool rtn = method.ReturnType != typeof(void);
        ParameterInfo[] p = method.GetParameters();
        if (p.Length > (rtn ? _funcTypes!.Length : _actionTypes!.Length))
        {
            Logger.DevkitServer.LogWarning(Source, "Method " + method.Format() + " can not have more than " + (rtn ? _funcTypes!.Length : _actionTypes!.Length) + " arguments!");
            if (throwOnFailure)
                throw new ArgumentException("Method can not have more than " + (rtn ? _funcTypes!.Length : _actionTypes!.Length) + " arguments!", nameof(method));
            return null;
        }
        Type deleType;
        try
        {
            if (rtn)
            {
                Type[] p2 = new Type[p.Length + 1];
                for (int i = 1; i < p2.Length - 1; ++i)
                    p2[i] = p[i].ParameterType;
                p2[^1] = method.ReturnType;
                deleType = _funcTypes![p.Length].MakeGenericType(p2);
            }
            else
            {
                Type[] p2 = new Type[p.Length];
                for (int i = 1; i < p2.Length; ++i)
                    p2[i] = p[i].ParameterType;
                deleType = _actionTypes![p.Length];
                deleType = deleType.MakeGenericType(p2);
            }
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError(Source, ex, "Error generating UI caller for " + method.Format() + ".");
            if (throwOnFailure)
                throw;
            return null;
        }

        return GenerateUICaller(uiType, deleType, method, throwOnFailure);
    }
    public static TDelegate? GenerateUICaller<TDelegate>(Type? uiType, MethodInfo? info, bool throwOnFailure = false) where TDelegate : Delegate
    {
        if (info == null)
        {
            Logger.DevkitServer.LogError(Source, "Error generating UI caller of type " + typeof(TDelegate).Format() + ".");
            if (throwOnFailure)
                throw new MissingMethodException("Error generating UI caller of type " + typeof(TDelegate).Format() + ".");

            return null;
        }
        Delegate? d = GenerateUICaller(uiType, typeof(TDelegate), info);
        if (d is TDelegate dele)
        {
            return dele;
        }

        if (d != null)
        {
            Logger.DevkitServer.LogError(Source, "Error generating UI caller for " + info.Format() + ".");
            if (throwOnFailure)
                throw new InvalidCastException("Failed to convert from " + d.GetType() + " to " + typeof(TDelegate) + ".");
        }
        else if (throwOnFailure)
            throw new Exception("Error generating UI caller for " + info.Format() + ".");

        return null;
    }
    public static Delegate? GenerateUICaller(Type? uiType, Type delegateType, MethodInfo method, bool throwOnFailure = false)
    {
        if (uiType == null)
        {
            Logger.DevkitServer.LogWarning(Source, "Unable to find type for method " + method.Format() + ".");
            DevkitServerModule.Fault();
            if (throwOnFailure)
                throw new MemberAccessException("Unable to find type for method: \"" + method.FullDescription() + "\".");
            return null;
        }
        try
        {
            if (method.IsStatic)
                return method.CreateDelegate(delegateType);

            const MethodAttributes attr = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig;
            ParameterInfo[] parameters = method.GetParameters();
            Type[] types = new Type[parameters.Length];
            for (int i = 0; i < parameters.Length; ++i)
                types[i] = parameters[i].ParameterType;
            DynamicMethod dmethod = new DynamicMethod("Call" + method.DeclaringType?.Name + "_" + method.Name + "Impl", attr,
                CallingConventions.Standard, method.ReturnType,
                types, typeof(UIAccessTools), true);
            DebuggableEmitter il = new DebuggableEmitter(dmethod);
            LoadUIToILGenerator(il, uiType);
            for (int i = 0; i < types.Length; ++i)
                il.EmitParameter(i);
            il.Emit(method.IsAbstract || method.IsVirtual ? OpCodes.Callvirt : OpCodes.Call, method);
            il.Emit(OpCodes.Ret);
            return dmethod.CreateDelegate(delegateType);
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogWarning(Source, ex, "Unable to create UI caller for " + (method.DeclaringType?.Format() ?? "<unknown-type>") + "." + method.Name);
            if (throwOnFailure)
                throw;
            return null;
        }
    }

    /// <summary>
    /// Sets the visibility of LoadingUI.cancelButton.
    /// </summary>
    public static bool SetLoadingCancelVisibility(bool visibility)
    {
        ThreadUtil.assertIsGameThread();

        ISleekButton? button = GetLoadingUICancelButton?.Invoke();

        if (button != null)
            button.IsVisible = visibility;
        else
            visibility = false;
        ISleekBox? box = GetLoadingBarBox?.Invoke();
        if (box != null)
            box.SizeOffset_X = visibility ? -130f : -20f;
        return button != null;
    }

    static UIAccessTools()
    {
        try
        {
            Type accessTools = typeof(UIAccessTools);
            MethodInfo getEditorTerrainUI = accessTools.GetProperty(nameof(EditorTerrainUI), BindingFlags.Public | BindingFlags.Static)!.GetMethod;
            const MethodAttributes attr = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig;
            Assembly sdg = AccessorExtensions.AssemblyCSharp;

            /*
             * TERRAIN
             */
            Type containerType = typeof(EditorTerrainUI);

            /* HEIGHTS */
            Type? rtnType = sdg.GetType("SDG.Unturned.EditorTerrainHeightUI");
            EditorTerrainHeightUIType = rtnType;
            if (rtnType == null)
            {
                Logger.DevkitServer.LogWarning(Source, "Unable to find type: SDG.Unturned.EditorTerrainHeightUI.");
                DevkitServerModule.Fault();
                return;
            }
            FieldInfo? field = containerType.GetField("heightV2", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public) ??
                               containerType.GetField("heights", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null || field.IsStatic || !rtnType.IsAssignableFrom(field.FieldType))
            {
                Logger.DevkitServer.LogWarning(Source, "Unable to find field: EditorTerrainUI.heightV2.");
                DevkitServerModule.Fault();
                return;
            }
            DynamicMethod method = new DynamicMethod("GetEditorTerrainHeightsUI_Impl", attr,
                CallingConventions.Standard, rtnType,
                Type.EmptyTypes, accessTools, true);
            IOpCodeEmitter il = method.GetILGenerator().AsEmitter();
            il.Emit(OpCodes.Call, getEditorTerrainUI);
            il.Emit(OpCodes.Ldfld, field);
            il.Emit(OpCodes.Ret);
            GetEditorTerrainHeightUI = (Func<object?>)method.CreateDelegate(typeof(Func<object>));

            /* MATERIALS */
            rtnType = sdg.GetType("SDG.Unturned.EditorTerrainMaterialsUI");
            EditorTerrainMaterialsUIType = rtnType;
            if (rtnType == null)
            {
                Logger.DevkitServer.LogWarning(Source, "Unable to find type: SDG.Unturned.EditorTerrainMaterialsUI.");
                DevkitServerModule.Fault();
                return;
            }
            field = containerType.GetField("materialsV2", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public) ??
                    containerType.GetField("materials", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null || field.IsStatic || !rtnType.IsAssignableFrom(field.FieldType))
            {
                Logger.DevkitServer.LogWarning(Source, "Unable to find field: EditorTerrainUI.materialsV2.");
                DevkitServerModule.Fault();
                return;
            }
            method = new DynamicMethod("GetEditorTerrainMaterialsUI_Impl", attr,
                CallingConventions.Standard, rtnType,
                Type.EmptyTypes, accessTools, true);
            il = method.GetILGenerator().AsEmitter();
            il.Emit(OpCodes.Call, getEditorTerrainUI);
            il.Emit(OpCodes.Ldfld, field);
            il.Emit(OpCodes.Ret);
            GetEditorTerrainMaterialsUI = (Func<object?>)method.CreateDelegate(typeof(Func<object>));

            /* DETAILS */
            rtnType = sdg.GetType("SDG.Unturned.EditorTerrainDetailsUI");
            EditorTerrainDetailsUIType = rtnType;
            if (rtnType == null)
            {
                Logger.DevkitServer.LogWarning(Source, "Unable to find type: SDG.Unturned.EditorTerrainDetailsUI.");
                DevkitServerModule.Fault();
                return;
            }
            field = containerType.GetField("detailsV2", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public) ??
                    containerType.GetField("details", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null || field.IsStatic || !rtnType.IsAssignableFrom(field.FieldType))
            {
                Logger.DevkitServer.LogWarning(Source, "Unable to find field: EditorTerrainUI.detailsV2.");
                DevkitServerModule.Fault();
                return;
            }
            method = new DynamicMethod("GetEditorTerrainDetailsUI_Impl", attr,
                CallingConventions.Standard, rtnType,
                Type.EmptyTypes, accessTools, true);
            il = method.GetILGenerator().AsEmitter();
            il.Emit(OpCodes.Call, getEditorTerrainUI);
            il.Emit(OpCodes.Ldfld, field);
            il.Emit(OpCodes.Ret);
            GetEditorTerrainDetailsUI = (Func<object?>)method.CreateDelegate(typeof(Func<object>));

            /* TILES */
            rtnType = sdg.GetType("SDG.Unturned.EditorTerrainTilesUI");
            EditorTerrainTilesUIType = rtnType;
            if (rtnType == null)
            {
                Logger.DevkitServer.LogWarning(Source, "Unable to find type: SDG.Unturned.EditorTerrainTilesUI.");
                DevkitServerModule.Fault();
                return;
            }
            field = containerType.GetField("tiles", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public) ??
                    containerType.GetField("tilesV2", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null || field.IsStatic || !rtnType.IsAssignableFrom(field.FieldType))
            {
                Logger.DevkitServer.LogWarning(Source, "Unable to find field: EditorTerrainUI.tiles.");
                DevkitServerModule.Fault();
                return;
            }
            method = new DynamicMethod("GetEditorTerrainTilesUI_Impl", attr,
                CallingConventions.Standard, rtnType,
                Type.EmptyTypes, accessTools, true);
            il = method.GetILGenerator().AsEmitter();
            il.Emit(OpCodes.Call, getEditorTerrainUI);
            il.Emit(OpCodes.Ldfld, field);
            il.Emit(OpCodes.Ret);
            GetEditorTerrainTilesUI = (Func<object?>)method.CreateDelegate(typeof(Func<object>));

            /*
             * TERRAIN
             */
            containerType = typeof(EditorEnvironmentUI);

            /* NODES */
            rtnType = sdg.GetType("SDG.Unturned.EditorEnvironmentNodesUI");
            EditorEnvironmentNodesUIType = rtnType;
            if (rtnType == null)
            {
                Logger.DevkitServer.LogWarning(Source, "Unable to find type: SDG.Unturned.EditorEnvironmentNodesUI.");
                DevkitServerModule.Fault();
                return;
            }
            field = containerType.GetField("nodesUI", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public) ??
                    containerType.GetField("nodes", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null || !field.IsStatic || !rtnType.IsAssignableFrom(field.FieldType))
            {
                Logger.DevkitServer.LogWarning(Source, "Unable to find field: EditorEnvironmentUI.nodesUI.");
                DevkitServerModule.Fault();
                return;
            }
            method = new DynamicMethod("GetEditorEnvironmentNodesUI_Impl", attr,
                CallingConventions.Standard, rtnType,
                Type.EmptyTypes, accessTools, true);
            il = method.GetILGenerator().AsEmitter();
            il.Emit(OpCodes.Ldsfld, field);
            il.Emit(OpCodes.Ret);
            GetEditorEnvironmentNodesUI = (Func<object?>)method.CreateDelegate(typeof(Func<object>));

            /*
             * LEVEL
             */
            containerType = typeof(EditorLevelUI);

            /* VOLUMES */
            rtnType = sdg.GetType("SDG.Unturned.EditorVolumesUI");
            EditorVolumesUIType = rtnType;
            if (rtnType == null)
            {
                Logger.DevkitServer.LogWarning(Source, "Unable to find type: SDG.Unturned.EditorVolumesUI.");
                DevkitServerModule.Fault();
                return;
            }
            field = containerType.GetField("volumesUI", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public) ??
                    containerType.GetField("volumes", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null || !field.IsStatic || !rtnType.IsAssignableFrom(field.FieldType))
            {
                Logger.DevkitServer.LogWarning(Source, "Unable to find field: EditorLevelUI.volumesUI.");
                DevkitServerModule.Fault();
                return;
            }
            method = new DynamicMethod("GetEditorVolumesUI_Impl", attr,
                CallingConventions.Standard, rtnType,
                Type.EmptyTypes, accessTools, true);
            il = method.GetILGenerator().AsEmitter();
            il.Emit(OpCodes.Ldsfld, field);
            il.Emit(OpCodes.Ret);
            GetEditorVolumesUI = (Func<object?>)method.CreateDelegate(typeof(Func<object>));

            /*
             * ITEM STORE
             */
            containerType = ItemStoreMenuType!;
            if (containerType != null)
            {
                /* CART MENU */
                rtnType = ItemStoreCartMenuType;
                if (rtnType != null)
                {
                    field = containerType.GetField("cartMenu", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public) ??
                            containerType.GetField("cartMenuUI", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (field == null || field.IsStatic || !rtnType.IsAssignableFrom(field.FieldType))
                    {
                        Logger.DevkitServer.LogWarning(Source, "Unable to find field: ItemStoreMenu.cartMenu.");
                    }
                    else
                    {
                        method = new DynamicMethod("GetItemStoreCartMenu_Impl", attr,
                            CallingConventions.Standard, rtnType,
                            Type.EmptyTypes, accessTools, true);
                        il = method.GetILGenerator().AsEmitter();
                        Label lbl = il.DefineLabel();
                        il.Emit(OpCodes.Call, accessTools.GetProperty(nameof(ItemStoreMenu), BindingFlags.Public | BindingFlags.Static)!.GetMethod);
                        il.Emit(OpCodes.Dup);
                        il.Emit(OpCodes.Brfalse_S, lbl);
                        il.Emit(OpCodes.Ldfld, field);
                        il.MarkLabel(lbl);
                        il.Emit(OpCodes.Ret);
                        GetItemStoreCartMenu = (Func<object?>)method.CreateDelegate(typeof(Func<object>));
                    }
                }
                else
                {
                    Logger.DevkitServer.LogWarning(Source, "Unable to find type: SDG.Unturned.ItemStoreCartMenu.");
                }

                /* DETAILS MENU */
                rtnType = ItemStoreDetailsMenuType;
                if (rtnType != null)
                {
                    field = containerType.GetField("detailsMenu", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public) ??
                            containerType.GetField("detailsMenuUI", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (field == null || field.IsStatic || !rtnType.IsAssignableFrom(field.FieldType))
                    {
                        Logger.DevkitServer.LogWarning(Source, "Unable to find field: ItemStoreMenu.detailsMenu.");
                    }
                    else
                    {
                        method = new DynamicMethod("GetItemStoreDetailsMenu_Impl", attr,
                            CallingConventions.Standard, rtnType,
                            Type.EmptyTypes, accessTools, true);
                        il = method.GetILGenerator().AsEmitter();
                        Label lbl = il.DefineLabel();
                        il.Emit(OpCodes.Call, accessTools.GetProperty(nameof(ItemStoreMenu), BindingFlags.Public | BindingFlags.Static)!.GetMethod);
                        il.Emit(OpCodes.Dup);
                        il.Emit(OpCodes.Brfalse_S, lbl);
                        il.Emit(OpCodes.Ldfld, field);
                        il.MarkLabel(lbl);
                        il.Emit(OpCodes.Ret);
                        GetItemStoreDetailsMenu = (Func<object?>)method.CreateDelegate(typeof(Func<object>));
                    }
                }
                else
                {
                    Logger.DevkitServer.LogWarning(Source, "Unable to find type: SDG.Unturned.ItemStoreCartMenu.");
                }

                /* BUNDLE CONTENTS MENU */
                rtnType = ItemStoreBundleContentsMenuType;
                if (rtnType != null)
                {
                    field = containerType.GetField("bundleContentsMenu", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public) ??
                            containerType.GetField("bundleContentsMenuUI", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (field == null || field.IsStatic || !rtnType.IsAssignableFrom(field.FieldType))
                    {
                        Logger.DevkitServer.LogWarning(Source, "Unable to find field: ItemStoreMenu.bundleContentsMenu.");
                    }
                    else
                    {
                        method = new DynamicMethod("ItemStoreBundleContentsMenu_Impl", attr,
                            CallingConventions.Standard, rtnType,
                            Type.EmptyTypes, accessTools, true);
                        il = method.GetILGenerator().AsEmitter();
                        Label lbl = il.DefineLabel();
                        il.Emit(OpCodes.Call, accessTools.GetProperty(nameof(ItemStoreMenu), BindingFlags.Public | BindingFlags.Static)!.GetMethod);
                        il.Emit(OpCodes.Dup);
                        il.Emit(OpCodes.Brfalse_S, lbl);
                        il.Emit(OpCodes.Ldfld, field);
                        il.MarkLabel(lbl);
                        il.Emit(OpCodes.Ret);
                        GetItemStoreBundleContentsMenu = (Func<object?>)method.CreateDelegate(typeof(Func<object>));
                    }
                }
                else
                {
                    Logger.DevkitServer.LogWarning(Source, "Unable to find type: SDG.Unturned.ItemStoreBundleContentsMenu.");
                }
            }
            else
            {
                Logger.DevkitServer.LogWarning(Source, "Unable to find type: SDG.Unturned.ItemStoreMenu.");
            }
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError(Source, ex, "Error initializing UI access tools.");
            DevkitServerModule.Fault();
        }
    }

    internal static void Init()
    {
        try
        {
            MethodBase[] emptyMethods = Array.Empty<MethodBase>();
            Dictionary<Type, UITypeInfo> typeInfo = new Dictionary<Type, UITypeInfo>(32);

            void Add(UITypeInfo type)
            {
                if (type.Type == typeof(object))
                {
                    CommandWindow.LogError($"Missing UI: {type.ExpectedTypeName}.");
                    return;
                }

                if (type.Parent == typeof(object))
                {
                    CommandWindow.LogError($"Missing parent of UI: {type.Type.FullName}.");
                    return;
                }

                if (!typeInfo.TryAdd(type.Type, type))
                    CommandWindow.LogError($"Duplicate UI: {type.Type.FullName}.");
            }

            Add(new UITypeInfo(nameof(SDG.Unturned.EditorDashboardUI), emptyMethods, emptyMethods, hasActiveMember: false)
            {
                ParentName = nameof(SDG.Unturned.EditorUI),
                Scene = UIScene.Editor,
                EmitProperty = nameof(EditorDashboardUI),
                OpenOnInitialize = true,
                DefaultOpenState = true,
                CloseOnDestroy = true
            });

            Add(new UITypeInfo(nameof(EditorEnvironmentLightingUI))
            {
                ParentName = nameof(SDG.Unturned.EditorEnvironmentUI),
                Scene = UIScene.Editor,
                IsStaticUI = true
            });

            Add(new UITypeInfo(nameof(EditorEnvironmentNavigationUI))
            {
                ParentName = nameof(SDG.Unturned.EditorEnvironmentUI),
                Scene = UIScene.Editor,
                IsStaticUI = true
            });

            Add(new UITypeInfo(nameof(EditorEnvironmentRoadsUI))
            {
                ParentName = nameof(SDG.Unturned.EditorEnvironmentUI),
                Scene = UIScene.Editor,
                IsStaticUI = true
            });

            Add(new UITypeInfo(nameof(SDG.Unturned.EditorEnvironmentUI))
            {
                ParentName = nameof(SDG.Unturned.EditorDashboardUI),
                Scene = UIScene.Editor,
                EmitProperty = nameof(EditorEnvironmentUI)
            });

            Add(new UITypeInfo(nameof(SDG.Unturned.EditorLevelObjectsUI))
            {
                ParentName = nameof(SDG.Unturned.EditorLevelUI),
                Scene = UIScene.Editor,
                EmitProperty = nameof(EditorLevelObjectsUI)
            });

            Add(new UITypeInfo(nameof(EditorLevelPlayersUI))
            {
                ParentName = nameof(SDG.Unturned.EditorLevelUI),
                Scene = UIScene.Editor,
                IsStaticUI = true
            });

            Add(new UITypeInfo(nameof(SDG.Unturned.EditorLevelUI))
            {
                ParentName = nameof(SDG.Unturned.EditorDashboardUI),
                Scene = UIScene.Editor,
                EmitProperty = nameof(EditorLevelUI)
            });

            Add(new UITypeInfo(nameof(EditorLevelVisibilityUI))
            {
                ParentName = nameof(SDG.Unturned.EditorLevelUI),
                Scene = UIScene.Editor,
                IsStaticUI = true
            });

            Add(new UITypeInfo(nameof(EditorPauseUI))
            {
                ParentName = nameof(SDG.Unturned.EditorDashboardUI),
                Scene = UIScene.Editor,
                IsStaticUI = true
            });

            Add(new UITypeInfo(nameof(EditorSpawnsAnimalsUI))
            {
                ParentName = nameof(EditorSpawnsUI),
                Scene = UIScene.Editor,
                IsStaticUI = true
            });

            Add(new UITypeInfo(nameof(EditorSpawnsItemsUI))
            {
                ParentName = nameof(EditorSpawnsUI),
                Scene = UIScene.Editor,
                IsStaticUI = true
            });

            Add(new UITypeInfo(nameof(EditorSpawnsUI))
            {
                ParentName = nameof(SDG.Unturned.EditorDashboardUI),
                Scene = UIScene.Editor,
                IsStaticUI = true
            });

            Add(new UITypeInfo(nameof(EditorSpawnsVehiclesUI))
            {
                ParentName = nameof(EditorSpawnsUI),
                Scene = UIScene.Editor,
                IsStaticUI = true
            });

            Add(new UITypeInfo(nameof(EditorSpawnsZombiesUI))
            {
                ParentName = nameof(EditorSpawnsUI),
                Scene = UIScene.Editor,
                IsStaticUI = true
            });

            Add(new UITypeInfo(nameof(SDG.Unturned.EditorTerrainUI))
            {
                ParentName = nameof(SDG.Unturned.EditorDashboardUI),
                Scene = UIScene.Editor,
                EmitProperty = nameof(EditorTerrainUI)
            });

            Add(new UITypeInfo(nameof(SDG.Unturned.EditorUI), emptyMethods, emptyMethods,
                typeof(EditorUI).GetMethod("Start", BindingFlags.NonPublic | BindingFlags.Instance) is { } method1 ? new MethodBase[] { method1 } : emptyMethods,
                hasActiveMember: false)
            {
                Scene = UIScene.Editor,
                EmitProperty = nameof(EditorUI),
                OpenOnInitialize = true,
                DefaultOpenState = true,
                CloseOnDestroy = true,
                IsActiveMember = FindUIType(nameof(SDG.Unturned.EditorUI))?.GetProperty(nameof(EditorUI.window), BindingFlags.Static | BindingFlags.Public)
            });

            Add(new UITypeInfo(nameof(SDG.Unturned.LoadingUI), emptyMethods, emptyMethods, hasActiveMember: false)
            {
                Scene = UIScene.Loading,
                EmitProperty = nameof(LoadingUI),
                OpenOnInitialize = true,
                DefaultOpenState = true,
                CloseOnDestroy = true,
                IsActiveMember = FindUIType(nameof(SDG.Unturned.LoadingUI))?.GetProperty(nameof(LoadingUI.isBlocked), BindingFlags.Static | BindingFlags.Public)
            });

            Add(new UITypeInfo(nameof(MenuConfigurationControlsUI))
            {
                ParentName = nameof(SDG.Unturned.MenuConfigurationUI),
                Scene = UIScene.Menu,
                IsStaticUI = true
            });

            Add(new UITypeInfo(nameof(MenuConfigurationDisplayUI))
            {
                ParentName = nameof(SDG.Unturned.MenuConfigurationUI),
                Scene = UIScene.Menu,
                IsStaticUI = true
            });

            Add(new UITypeInfo(nameof(MenuConfigurationGraphicsUI))
            {
                ParentName = nameof(SDG.Unturned.MenuConfigurationUI),
                Scene = UIScene.Menu,
                IsStaticUI = true
            });

            Add(new UITypeInfo(nameof(MenuConfigurationOptionsUI))
            {
                ParentName = nameof(SDG.Unturned.MenuConfigurationUI),
                Scene = UIScene.Menu,
                IsStaticUI = true
            });

            Add(new UITypeInfo(nameof(SDG.Unturned.MenuConfigurationUI))
            {
                ParentName = nameof(SDG.Unturned.MenuDashboardUI),
                Scene = UIScene.Menu,
                EmitProperty = nameof(MenuConfigurationUI)
            });

            Add(new UITypeInfo(nameof(SDG.Unturned.MenuCreditsUI))
            {
                ParentName = nameof(SDG.Unturned.MenuDashboardUI),
                Scene = UIScene.Menu,
                EmitProperty = nameof(MenuCreditsUI)
            });

            Add(new UITypeInfo(nameof(SDG.Unturned.MenuDashboardUI))
            {
                ParentName = nameof(SDG.Unturned.MenuUI),
                Scene = UIScene.Menu,
                EmitProperty = nameof(MenuDashboardUI),
                DefaultOpenState = true
            });

            Add(new UITypeInfo(nameof(SDG.Unturned.MenuPauseUI))
            {
                ParentName = nameof(SDG.Unturned.MenuDashboardUI),
                Scene = UIScene.Menu,
                EmitProperty = nameof(MenuPauseUI)
            });

            Add(new UITypeInfo(nameof(MenuPlayConfigUI))
            {
                ParentName = nameof(SDG.Unturned.MenuDashboardUI),
                Scene = UIScene.Menu,
                IsStaticUI = true
            });

            Add(new UITypeInfo(nameof(SDG.Unturned.MenuPlayConnectUI))
            {
                ParentName = nameof(SDG.Unturned.MenuPlayUI),
                Scene = UIScene.Menu,
                EmitProperty = nameof(MenuPlayConnectUI)
            });

            Add(new UITypeInfo(nameof(SDG.Unturned.MenuPlayLobbiesUI))
            {
                ParentName = nameof(SDG.Unturned.MenuPlayUI),
                Scene = UIScene.Menu,
                EmitProperty = nameof(MenuPlayLobbiesUI)
            });

            Add(new UITypeInfo(nameof(MenuPlayServerListFiltersUI))
            {
                ParentName = nameof(SDG.Unturned.MenuPlayServersUI),
                Scene = UIScene.Menu,
                CustomEmitter = FindUIType(nameof(SDG.Unturned.MenuPlayServersUI))?.GetField("serverListFiltersUI", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) is { } field
                                ? (_, generator) => generator.Emit(OpCodes.Ldsfld, field)
                                : null
            });

            Add(new UITypeInfo(nameof(MenuPlayServerCurationUI))
            {
                ParentName = nameof(SDG.Unturned.MenuPlayServersUI),
                Scene = UIScene.Menu,
                CustomEmitter = FindUIType(nameof(SDG.Unturned.MenuPlayServersUI))?.GetField("serverCurationUI", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) is { } field3
                                ? (_, generator) => generator.Emit(OpCodes.Ldsfld, field3)
                                : null
            });

            Add(new UITypeInfo("MenuPlayServerCurationRulesUI")
            {
                ParentName = nameof(MenuPlayServerCurationUI),
                Scene = UIScene.Menu,
                EmitProperty = nameof(MenuPlayServerCurationRulesUI)
            });

            Add(new UITypeInfo(nameof(MenuPlayServerBookmarksUI))
            {
                ParentName = nameof(SDG.Unturned.MenuPlayUI),
                Scene = UIScene.Menu,
                EmitProperty = nameof(MenuPlayServerBookmarksUI)
            });

            Add(new UITypeInfo(nameof(SDG.Unturned.MenuPlayServerInfoUI))
            {
                ParentName = nameof(SDG.Unturned.MenuPlayUI),
                Scene = UIScene.Menu,
                CustomEmitter = FindUIType(nameof(SDG.Unturned.MenuPlayUI))?.GetField("serverBookmarksUI", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) is { } field4
                                ? (_, generator) => generator.Emit(OpCodes.Ldsfld, field4)
                                : null
            });

            Add(new UITypeInfo(nameof(SDG.Unturned.MenuPlayServersUI))
            {
                ParentName = nameof(SDG.Unturned.MenuPlayUI),
                Scene = UIScene.Menu,
                CustomEmitter = FindUIType(nameof(SDG.Unturned.MenuPlayUI))?.GetField("serverListUI", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) is { } field2
                                ? (_, generator) => generator.Emit(OpCodes.Ldsfld, field2)
                                : null
            });

            Add(new UITypeInfo(nameof(SDG.Unturned.MenuPlaySingleplayerUI))
            {
                ParentName = nameof(SDG.Unturned.MenuPlayUI),
                Scene = UIScene.Menu,
                EmitProperty = nameof(MenuPlaySingleplayerUI)
            });

            Add(new UITypeInfo(nameof(SDG.Unturned.MenuPlayUI))
            {
                ParentName = nameof(SDG.Unturned.MenuDashboardUI),
                Scene = UIScene.Menu,
                EmitProperty = nameof(MenuPlayUI)
            });

            Add(new UITypeInfo(nameof(SDG.Unturned.MenuServerPasswordUI))
            {
                ParentName = nameof(SDG.Unturned.MenuPlayServerInfoUI),
                Scene = UIScene.Menu,
                EmitProperty = nameof(MenuServerPasswordUI)
            });

            Add(new UITypeInfo(nameof(SDG.Unturned.MenuSurvivorsAppearanceUI))
            {
                ParentName = nameof(SDG.Unturned.MenuSurvivorsUI),
                Scene = UIScene.Menu,
                EmitProperty = nameof(MenuSurvivorsAppearanceUI)
            });

            Add(new UITypeInfo(nameof(SDG.Unturned.MenuSurvivorsCharacterUI))
            {
                ParentName = nameof(SDG.Unturned.MenuSurvivorsUI),
                Scene = UIScene.Menu,
                EmitProperty = nameof(MenuSurvivorsCharacterUI)
            });

            Add(new UITypeInfo(nameof(SDG.Unturned.MenuSurvivorsClothingBoxUI))
            {
                ParentName = nameof(SDG.Unturned.MenuSurvivorsClothingUI),
                Scene = UIScene.Menu,
                EmitProperty = nameof(MenuSurvivorsClothingBoxUI)
            });

            Add(new UITypeInfo(nameof(SDG.Unturned.MenuSurvivorsClothingDeleteUI))
            {
                ParentName = nameof(SDG.Unturned.MenuSurvivorsClothingUI),
                Scene = UIScene.Menu,
                EmitProperty = nameof(MenuSurvivorsClothingDeleteUI)
            });

            Add(new UITypeInfo(nameof(SDG.Unturned.MenuSurvivorsClothingInspectUI))
            {
                ParentName = nameof(SDG.Unturned.MenuSurvivorsClothingUI),
                Scene = UIScene.Menu,
                EmitProperty = nameof(MenuSurvivorsClothingInspectUI)
            });

            Add(new UITypeInfo(nameof(SDG.Unturned.MenuSurvivorsClothingItemUI))
            {
                ParentName = nameof(SDG.Unturned.MenuSurvivorsClothingUI),
                Scene = UIScene.Menu,
                EmitProperty = nameof(MenuSurvivorsClothingItemUI)
            });

            Add(new UITypeInfo(nameof(SDG.Unturned.MenuSurvivorsClothingUI))
            {
                ParentName = nameof(SDG.Unturned.MenuSurvivorsUI),
                Scene = UIScene.Menu,
                EmitProperty = nameof(MenuSurvivorsClothingUI)
            });

            Add(new UITypeInfo(nameof(SDG.Unturned.MenuSurvivorsGroupUI))
            {
                ParentName = nameof(SDG.Unturned.MenuSurvivorsUI),
                Scene = UIScene.Menu,
                EmitProperty = nameof(MenuSurvivorsGroupUI)
            });

            Add(new UITypeInfo(nameof(SDG.Unturned.MenuSurvivorsUI))
            {
                ParentName = nameof(SDG.Unturned.MenuDashboardUI),
                Scene = UIScene.Menu,
                EmitProperty = nameof(MenuSurvivorsUI)
            });

            Add(new UITypeInfo(nameof(SDG.Unturned.MenuTitleUI))
            {
                ParentName = nameof(SDG.Unturned.MenuDashboardUI),
                Scene = UIScene.Menu,
                EmitProperty = nameof(MenuTitleUI)
            });

            Add(new UITypeInfo(nameof(SDG.Unturned.MenuUI), emptyMethods, emptyMethods, hasActiveMember: false)
            {
                Scene = UIScene.Menu,
                EmitProperty = nameof(MenuUI),
                OpenOnInitialize = true,
                DefaultOpenState = true,
                CloseOnDestroy = true,
                IsActiveMember = FindUIType(nameof(SDG.Unturned.MenuUI))?.GetProperty(nameof(MenuUI.window), BindingFlags.Static | BindingFlags.Public)
            });

            Add(new UITypeInfo(nameof(SDG.Unturned.MenuWorkshopEditorUI))
            {
                ParentName = nameof(SDG.Unturned.MenuWorkshopUI),
                Scene = UIScene.Menu,
                EmitProperty = nameof(MenuWorkshopEditorUI)
            });

            Add(new UITypeInfo(nameof(SDG.Unturned.MenuWorkshopErrorUI))
            {
                ParentName = nameof(SDG.Unturned.MenuWorkshopUI),
                Scene = UIScene.Menu,
                EmitProperty = nameof(MenuWorkshopErrorUI)
            });

            Add(new UITypeInfo(nameof(SDG.Unturned.MenuWorkshopLocalizationUI))
            {
                ParentName = nameof(SDG.Unturned.MenuWorkshopUI),
                Scene = UIScene.Menu,
                EmitProperty = nameof(MenuWorkshopLocalizationUI)
            });

            Add(new UITypeInfo(nameof(SDG.Unturned.MenuWorkshopSpawnsUI))
            {
                ParentName = nameof(SDG.Unturned.MenuWorkshopUI),
                Scene = UIScene.Menu,
                EmitProperty = nameof(MenuWorkshopSpawnsUI)
            });

            Add(new UITypeInfo(nameof(SDG.Unturned.MenuWorkshopSubmitUI))
            {
                ParentName = nameof(SDG.Unturned.MenuWorkshopUI),
                Scene = UIScene.Menu,
                EmitProperty = nameof(MenuWorkshopSubmitUI)
            });

            Add(new UITypeInfo(nameof(SDG.Unturned.MenuWorkshopSubscriptionsUI))
            {
                ParentName = nameof(SDG.Unturned.MenuWorkshopUI),
                Scene = UIScene.Menu,
                EmitProperty = nameof(MenuWorkshopSubscriptionsUI)
            });

            Add(new UITypeInfo(nameof(SDG.Unturned.MenuWorkshopUI))
            {
                ParentName = nameof(SDG.Unturned.MenuDashboardUI),
                Scene = UIScene.Menu,
                EmitProperty = nameof(MenuWorkshopUI)
            });

            Add(new UITypeInfo(typeof(PlayerBarricadeLibraryUI))
            {
                ParentName = nameof(SDG.Unturned.PlayerUI),
                Scene = UIScene.Player,
                IsStaticUI = true
            });

            Add(new UITypeInfo(nameof(SDG.Unturned.PlayerBarricadeMannequinUI))
            {
                ParentName = nameof(SDG.Unturned.PlayerUI),
                Scene = UIScene.Player,
                EmitProperty = nameof(PlayerBarricadeMannequinUI)
            });

            Add(new UITypeInfo(nameof(PlayerBarricadeSignUI))
            {
                ParentName = nameof(SDG.Unturned.PlayerUI),
                Scene = UIScene.Player,
                IsStaticUI = true
            });

            Add(new UITypeInfo(nameof(SDG.Unturned.PlayerBarricadeStereoUI))
            {
                ParentName = nameof(SDG.Unturned.PlayerUI),
                Scene = UIScene.Player,
                EmitProperty = nameof(PlayerBarricadeStereoUI)
            });

            Add(new UITypeInfo(nameof(PlayerDashboardCraftingUI))
            {
                ParentName = nameof(SDG.Unturned.PlayerDashboardUI),
                Scene = UIScene.Player,
                IsStaticUI = true
            });

            Add(new UITypeInfo(nameof(SDG.Unturned.PlayerDashboardInformationUI))
            {
                ParentName = nameof(SDG.Unturned.PlayerDashboardUI),
                Scene = UIScene.Player,
                EmitProperty = nameof(PlayerDashboardInformationUI)
            });

            Add(new UITypeInfo(nameof(PlayerDashboardInventoryUI))
            {
                ParentName = nameof(SDG.Unturned.PlayerDashboardUI),
                Scene = UIScene.Player,
                IsStaticUI = true
            });

            Add(new UITypeInfo(nameof(PlayerDashboardSkillsUI))
            {
                ParentName = nameof(SDG.Unturned.PlayerDashboardUI),
                Scene = UIScene.Player,
                IsStaticUI = true
            });

            Add(new UITypeInfo(nameof(SDG.Unturned.PlayerDashboardUI))
            {
                ParentName = nameof(SDG.Unturned.PlayerUI),
                Scene = UIScene.Player,
                EmitProperty = nameof(PlayerDashboardUI)
            });

            Add(new UITypeInfo(nameof(PlayerDeathUI))
            {
                ParentName = nameof(SDG.Unturned.PlayerUI),
                Scene = UIScene.Player,
                IsStaticUI = true
            });

            Add(new UITypeInfo(nameof(SDG.Unturned.PlayerLifeUI))
            {
                ParentName = nameof(SDG.Unturned.PlayerUI),
                Scene = UIScene.Player,
                EmitProperty = nameof(PlayerLifeUI)
            });

            Add(new UITypeInfo(nameof(PlayerNPCDialogueUI))
            {
                ParentName = nameof(SDG.Unturned.PlayerUI),
                Scene = UIScene.Player,
                IsStaticUI = true
            });

            Add(new UITypeInfo(nameof(PlayerNPCQuestUI))
            {
                ParentName = nameof(SDG.Unturned.PlayerUI),
                Scene = UIScene.Player,
                IsStaticUI = true
            });

            Add(new UITypeInfo(nameof(PlayerNPCVendorUI))
            {
                ParentName = nameof(SDG.Unturned.PlayerUI),
                Scene = UIScene.Player,
                IsStaticUI = true
            });

            Add(new UITypeInfo(nameof(SDG.Unturned.PlayerPauseUI))
            {
                ParentName = nameof(SDG.Unturned.PlayerUI),
                Scene = UIScene.Player,
                EmitProperty = nameof(PlayerPauseUI)
            });

            Add(new UITypeInfo(nameof(SDG.Unturned.PlayerUI), emptyMethods, emptyMethods,
                typeof(PlayerUI).GetMethod("InitializePlayer", BindingFlags.NonPublic | BindingFlags.Instance) is { } method3 ? new MethodBase[] { method3 } : emptyMethods
                , hasActiveMember: false)
            {
                Scene = UIScene.Player,
                EmitProperty = nameof(PlayerUI),
                OpenOnInitialize = true,
                DefaultOpenState = true,
                CloseOnDestroy = true,
                IsActiveMember = FindUIType(nameof(SDG.Unturned.PlayerUI))?.GetProperty(nameof(PlayerUI.window), BindingFlags.Static | BindingFlags.Public)
            });

            Add(new UITypeInfo(nameof(PlayerWorkzoneUI))
            {
                ParentName = nameof(SDG.Unturned.PlayerUI),
                Scene = UIScene.Player,
                IsStaticUI = true
            });

            Add(new UITypeInfo("EditorEnvironmentNodesUI", hasActiveMember: false)
            {
                ParentName = nameof(SDG.Unturned.EditorEnvironmentUI),
                Scene = UIScene.Editor,
                EmitProperty = nameof(EditorEnvironmentNodesUI)
            });

            Add(new UITypeInfo("EditorVolumesUI", hasActiveMember: false)
            {
                ParentName = nameof(SDG.Unturned.EditorLevelUI),
                Scene = UIScene.Editor,
                EmitProperty = nameof(EditorVolumesUI)
            });

            Add(new UITypeInfo("EditorTerrainHeightUI", hasActiveMember: false)
            {
                ParentName = nameof(SDG.Unturned.EditorTerrainUI),
                Scene = UIScene.Editor,
                EmitProperty = nameof(EditorTerrainHeightUI),
                DestroyWhenParentDestroys = true
            });

            Add(new UITypeInfo("EditorTerrainMaterialsUI", hasActiveMember: false)
            {
                ParentName = nameof(SDG.Unturned.EditorTerrainUI),
                Scene = UIScene.Editor,
                EmitProperty = nameof(EditorTerrainMaterialsUI),
                DestroyWhenParentDestroys = true
            });

            Add(new UITypeInfo("EditorTerrainDetailsUI", hasActiveMember: false)
            {
                ParentName = nameof(SDG.Unturned.EditorTerrainUI),
                Scene = UIScene.Editor,
                EmitProperty = nameof(EditorTerrainDetailsUI),
                DestroyWhenParentDestroys = true
            });

            Add(new UITypeInfo("EditorTerrainTilesUI", hasActiveMember: false)
            {
                ParentName = nameof(SDG.Unturned.EditorTerrainUI),
                Scene = UIScene.Editor,
                EmitProperty = nameof(EditorTerrainTilesUI),
                DestroyWhenParentDestroys = true
            });

            Add(new UITypeInfo("PlayerBrowserRequestUI")
            {
                ParentName = nameof(SDG.Unturned.PlayerUI),
                Scene = UIScene.Player,
                EmitProperty = nameof(PlayerBrowserRequestUI)
            });

            Add(new UITypeInfo("PlayerGroupUI", emptyMethods, emptyMethods, hasActiveMember: false)
            {
                ParentName = nameof(SDG.Unturned.PlayerUI),
                Scene = UIScene.Player,
                EmitProperty = nameof(PlayerGroupUI),
                OpenOnInitialize = true,
                DefaultOpenState = true,
                CloseOnDestroy = true
            });

            Add(new UITypeInfo("ItemStoreMenu", hasActiveMember: false)
            {
                ParentName = nameof(SDG.Unturned.MenuSurvivorsClothingUI),
                Scene = UIScene.Menu,
                EmitProperty = nameof(ItemStoreMenu)
            });

            Add(new UITypeInfo("ItemStoreCartMenu", hasActiveMember: false)
            {
                Parent = ItemStoreMenuType ?? typeof(object),
                Scene = UIScene.Menu,
                EmitProperty = nameof(ItemStoreCartMenu)
            });

            Add(new UITypeInfo("ItemStoreDetailsMenu", hasActiveMember: false)
            {
                Parent = ItemStoreMenuType ?? typeof(object),
                Scene = UIScene.Menu,
                EmitProperty = nameof(ItemStoreDetailsMenu)
            });

            Add(new UITypeInfo("ItemStoreBundleContentsMenu", hasActiveMember: false)
            {
                Parent = ItemStoreMenuType ?? typeof(object),
                Scene = UIScene.Menu,
                EmitProperty = nameof(ItemStoreBundleContentsMenu)
            });

            try
            {
                CustomVolumeMenuHandler volumeHandler = new CustomVolumeMenuHandler();
                CustomNodeMenuHandler nodeHandler = new CustomNodeMenuHandler();
                CustomSleekWrapperDestroyHandler sleekWrapperHandler = new CustomSleekWrapperDestroyHandler();
                List<Type> types = Accessor.GetTypesSafe(AccessorExtensions.AssemblyCSharp);
                foreach (Type menuType in types
                             .Where(x =>
                                 x.Name.Equals("Menu", StringComparison.Ordinal) &&
                                 x.DeclaringType != null &&
                                 typeof(SleekWrapper).IsAssignableFrom(x)))
                {
                    MenuTypes[menuType.DeclaringType] = menuType;
                    if (typeof(VolumeBase).IsAssignableFrom(menuType.DeclaringType) && EditorVolumesUIType != null)
                    {
                        typeInfo[menuType] = new UITypeInfo(menuType, emptyMethods, emptyMethods, hasActiveMember: false)
                        {
                            Parent = EditorVolumesUIType,
                            Scene = UIScene.Editor,
                            CustomEmitter = EmitVolumeMenu,
                            CustomOnClose = volumeHandler,
                            CustomOnOpen = volumeHandler,
                            CustomOnDestroy = sleekWrapperHandler
                        };
                    }
                    else if (typeof(TempNodeBase).IsAssignableFrom(menuType.DeclaringType) && EditorEnvironmentNodesUIType != null)
                    {
                        typeInfo[menuType] = new UITypeInfo(menuType, emptyMethods, emptyMethods, hasActiveMember: false)
                        {
                            Parent = EditorEnvironmentNodesUIType,
                            Scene = UIScene.Editor,
                            CustomEmitter = EmitNodeMenu,
                            DefaultOpenState = true,
                            OpenOnInitialize = true,
                            CustomOnClose = nodeHandler,
                            CustomOnOpen = nodeHandler,
                            CustomOnDestroy = sleekWrapperHandler
                        };
                    }
                }
                Logger.DevkitServer.LogDebug(Source, $"Discovered {MenuTypes.Count.Format()} editor node/volume menu type(s).");
                int c = typeInfo.Count;
                foreach (Type sleekWrapper in types.Where(x => typeof(SleekWrapper).IsAssignableFrom(x)))
                {
                    if (typeInfo.ContainsKey(sleekWrapper))
                        continue;

                    typeInfo.Add(sleekWrapper, new UITypeInfo(sleekWrapper, emptyMethods, emptyMethods, hasActiveMember: false)
                    {
                        Parent = null,
                        Scene = UIScene.Global,
                        IsInstanceUI = false,
                        DefaultOpenState = true,
                        OpenOnInitialize = true,
                        CustomOnDestroy = sleekWrapperHandler,
                        CloseOnDestroy = true
                    });
                }
                Logger.DevkitServer.LogDebug(Source, $"Discovered {(typeInfo.Count - c).Format()} UI wrapper type(s).");
                c = typeInfo.Count;
                Type? playerUi = FindUIType(nameof(SDG.Unturned.PlayerUI));
                if (playerUi != null)
                {
                    CustomUseableHandler useableHandler = new CustomUseableHandler();
                    foreach (Type useable in types.Where(x => typeof(Useable).IsAssignableFrom(x)))
                    {
                        if (typeInfo.ContainsKey(useable))
                            continue;
                        FieldInfo[] fields = useable.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        PropertyInfo[] properties = useable.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (!fields.Any(x => typeof(ISleekElement).IsAssignableFrom(x.FieldType) && !properties.Any(x => typeof(ISleekElement).IsAssignableFrom(x.PropertyType))))
                            continue;

                        typeInfo.Add(useable, new UITypeInfo(useable, emptyMethods, emptyMethods, emptyMethods, hasActiveMember: false)
                        {
                            Parent = typeof(PlayerUI),
                            Scene = UIScene.Player,
                            CustomEmitter = EmitUseable,
                            CloseOnDestroy = true,
                            OpenOnInitialize = true,
                            CustomOnInitialize = useableHandler,
                            CustomOnDestroy = useableHandler
                        });
                    }
                    Logger.DevkitServer.LogDebug(Source, $"Discovered {(typeInfo.Count - c).Format()} useable UI type(s).");
                }
            }
            catch (Exception ex)
            {
                Logger.DevkitServer.LogWarning(Source, ex, "Error initializing volume and node UI info.");
            }

            int ct = typeInfo.Count;

            EventOnInitializingUIInfo.TryInvoke(typeInfo);

            ct = typeInfo.Count - ct;
            if (ct > 0)
                Logger.DevkitServer.LogDebug(Source, $"Plugins added {ct.Format()} UI type(s).");

            _typeInfoIntl = typeInfo;
            Logger.DevkitServer.LogDebug(Source, $"Discovered {typeInfo.Count.Format()} UI type(s).");
            TypeInfo = new ReadOnlyDictionary<Type, UITypeInfo>(typeInfo);
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError(Source, ex, $"Error initializing {typeof(UITypeInfo).Format()} records.");
        }
    }

    private static void EmitVolumeMenu(UITypeInfo info, IOpCodeEmitter generator)
    {
        FieldInfo? field = EditorVolumesUIType?.GetField("focusedItemMenu", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        if (field == null)
            throw new MemberAccessException("Unable to find field: EditorVolumesUI.focusedItemMenu.");
        Label lbl = generator.DefineLabel();
        generator.Emit(OpCodes.Call, typeof(UIAccessTools).GetProperty(nameof(EditorVolumesUI), BindingFlags.Public | BindingFlags.Static)!.GetMethod);
        generator.Emit(OpCodes.Dup);
        generator.Emit(OpCodes.Brfalse, lbl);
        generator.Emit(OpCodes.Ldfld, field);
        generator.Emit(OpCodes.Isinst, info.Type);
        generator.MarkLabel(lbl);
    }
    private static void EmitNodeMenu(UITypeInfo info, IOpCodeEmitter generator)
    {
        FieldInfo? field = EditorEnvironmentNodesUIType?.GetField("focusedItemMenu", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        if (field == null)
            throw new MemberAccessException("Unable to find field: EditorEnvironmentNodesUI.focusedItemMenu.");
        Label lbl = generator.DefineLabel();
        generator.Emit(OpCodes.Call, typeof(UIAccessTools).GetProperty(nameof(EditorEnvironmentNodesUI), BindingFlags.Public | BindingFlags.Static)!.GetMethod);
        generator.Emit(OpCodes.Dup);
        generator.Emit(OpCodes.Brfalse, lbl);
        generator.Emit(OpCodes.Ldfld, field);
        generator.Emit(OpCodes.Isinst, info.Type);
        generator.MarkLabel(lbl);
    }
    private static void EmitUseable(UITypeInfo info, IOpCodeEmitter generator)
    {
        MethodInfo? playerProp = typeof(Player).GetProperty(nameof(Player.player), BindingFlags.Public | BindingFlags.Static)?.GetGetMethod(true);
        MethodInfo? playerEquipmentProp = playerProp == null ? null : typeof(Player).GetProperty(nameof(Player.equipment), BindingFlags.Public | BindingFlags.Instance)?.GetGetMethod(true);
        MethodInfo? useableProp = playerEquipmentProp == null ? null : typeof(PlayerEquipment).GetProperty(nameof(PlayerEquipment.useable), BindingFlags.Public | BindingFlags.Instance)?.GetGetMethod(true);
        if (useableProp == null)
            throw new MemberAccessException("Unable to find at least one of the properties: Player.player, Player.equipment, PlayerEquipment.useable.");
        Label lbl = generator.DefineLabel();
        generator.Emit(playerProp!.GetCall(), playerProp!);
        generator.Emit(OpCodes.Dup);
        generator.Emit(OpCodes.Brfalse, lbl);
        generator.Emit(playerEquipmentProp!.GetCall(), playerEquipmentProp!);
        generator.Emit(useableProp.GetCall(), useableProp);
        generator.Emit(OpCodes.Isinst, info.Type);
        generator.MarkLabel(lbl);
    }

    [HarmonyPatch(typeof(EditorUI), "Start")]
    [HarmonyPostfix]
    [UsedImplicitly]
    private static void EditorUIStartPostfix()
    {
        EditorUIReady?.Invoke();
        Logger.DevkitServer.LogInfo("EditorUI.Start", "Editor UI ready.");
    }

    [HarmonyPatch(typeof(PlayerUI), "InitializePlayer")]
    [HarmonyPostfix]
    [UsedImplicitly]
    private static void PlayerUIStartPostfix()
    {
        PlayerUIReady?.Invoke();
        Logger.DevkitServer.LogInfo("PlayerUI.InitializePlayer", "Player UI ready.");
    }
}

public delegate void InitializingUIInfo(Dictionary<Type, UITypeInfo> typeInfo);
#endif