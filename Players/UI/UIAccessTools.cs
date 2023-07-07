#if CLIENT
using HarmonyLib;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Reflection.Emit;
using Action = System.Action;

namespace DevkitServer.Players.UI;

[EarlyTypeInit]
[HarmonyPatch]
public static class UIAccessTools
{
    internal const string Source = "UI TOOLS";

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

    private static readonly InstanceGetter<MenuPlayUI, MenuPlayServersUI?>? GetMenuPlayServersUI
        = Accessor.GenerateInstanceGetter<MenuPlayUI, MenuPlayServersUI?>("serverListUI");

    private static readonly InstanceGetter<MenuPlayUI, MenuPlayServerInfoUI?>? GetMenuPlayServerInfoUI
        = Accessor.GenerateInstanceGetter<MenuPlayUI, MenuPlayServerInfoUI?>("serverInfoUI");

    private static readonly InstanceGetter<MenuPlayUI, MenuPlaySingleplayerUI?>? GetMenuPlaySingleplayerUI
        = Accessor.GenerateInstanceGetter<MenuPlayUI, MenuPlaySingleplayerUI?>("singleplayerUI");

    private static readonly InstanceGetter<MenuPlayUI, MenuPlayMatchmakingUI?>? GetMenuPlayMatchmakingUI
        = Accessor.GenerateInstanceGetter<MenuPlayUI, MenuPlayMatchmakingUI?>("matchmakingUI");

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

    private static readonly Dictionary<Type, Type> MenuTypes = new Dictionary<Type, Type>(32);

    private static readonly Func<object?>? GetEditorTerrainHeightUI;
    private static readonly Func<object?>? GetEditorTerrainMaterialsUI;
    private static readonly Func<object?>? GetEditorTerrainDetailsUI;
    private static readonly Func<object?>? GetEditorTerrainTilesUI;

    private static readonly Func<object?>? GetEditorEnvironmentNodesUI;

    private static readonly Func<object?>? GetEditorVolumesUI;

    private static readonly Func<object?>? GetItemStoreCartMenu;
    private static readonly Func<object?>? GetItemStoreDetailsMenu;

    public static EditorUI? EditorUI => GetEditorUIInstance();
    public static PlayerUI? PlayerUI => GetPlayerUIInstance();
    public static MenuUI? MenuUI => GetMenuUIInstance?.Invoke();
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

    public static Type? PlayerBrowserRequestUIType { get; } = Accessor.AssemblyCSharp.GetType("SDG.Unturned.PlayerBrowserRequestUI", false);
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
    public static Type? PlayerGroupUIType { get; } = Accessor.AssemblyCSharp.GetType("SDG.Unturned.PlayerGroupUI", false);
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
    public static MenuPlayServersUI? MenuPlayServersUI
    {
        get
        {
            MenuPlayUI? menuPlayUI = MenuPlayUI;
            return menuPlayUI != null ? GetMenuPlayServersUI?.Invoke(menuPlayUI) : null;
        }
    }
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
    public static MenuPlayMatchmakingUI? MenuPlayMatchmakingUI
    {
        get
        {
            MenuPlayUI? menuPlayUI = MenuPlayUI;
            return menuPlayUI != null ? GetMenuPlayMatchmakingUI?.Invoke(menuPlayUI) : null;
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
    public static Type? ItemStoreMenuType { get; } = Accessor.AssemblyCSharp.GetType("SDG.Unturned.ItemStoreMenu", false);
    public static object? ItemStoreMenu
    {
        get
        {
            MenuSurvivorsClothingUI? menuSurvivorsClothingUI = MenuSurvivorsClothingUI;
            return menuSurvivorsClothingUI != null ? GetItemStoreMenu?.Invoke(menuSurvivorsClothingUI) : null;
        }
    }
    public static Type? ItemStoreCartMenuType { get; } = Accessor.AssemblyCSharp.GetType("SDG.Unturned.ItemStoreCartMenu", false);
    public static Type? ItemStoreDetailsMenuType { get; } = Accessor.AssemblyCSharp.GetType("SDG.Unturned.ItemStoreDetailsMenu", false);
    public static object? ItemStoreCartMenu => GetItemStoreCartMenu?.Invoke();
    public static object? ItemStoreDetailsMenu => GetItemStoreDetailsMenu?.Invoke();
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

    private static readonly Dictionary<Type, UITypeInfo> TypeInfoIntl;
    private static LoadingUI? _loadingUI;
    public static IReadOnlyDictionary<Type, UITypeInfo> TypeInfo { get; }

    public static UITypeInfo? GetTypeInfo(Type type) => TypeInfoIntl.TryGetValue(type, out UITypeInfo typeInfo) ? typeInfo : null;

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
                Logger.LogWarning("Unable to find type for field " + fieldName.Format() + ".", method: Source);
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
            Type? memberType = field is FieldInfo f2 ? f2.FieldType : (field is PropertyInfo p2 ? p2.PropertyType : null);
            if (memberType == null || field == null || field is PropertyInfo prop && (prop.GetIndexParameters() is { Length: > 0 } || prop.GetGetMethod(true) == null))
            {
                Logger.LogWarning("Unable to find field or property: " + uiType.Format() + "." + fieldName.Colorize(Color.red) + ".", method: Source);
                DevkitServerModule.Fault();
                if (throwOnFailure)
                    throw new MemberAccessException("Unable to find field or property: \"" + uiType.Name + "." + fieldName + "\".");
                return null;
            }
            if (rtnType != null && !rtnType.IsAssignableFrom(memberType))
            {
                Logger.LogWarning("Field or property " + field.Format() + " is not assignable to " + rtnType.Format() + ".", method: Source);
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
                    Logger.LogWarning($"Error creating simplified getter delegate for {((PropertyInfo)field).Format()} UI accessor.", method: Source);
                    DevkitServerModule.Fault();
                    if (throwOnFailure)
                        throw;
                    return null;
                }
            }

            DynamicMethod method = new DynamicMethod("Get" + uiType.Name + "_Impl", attr,
                CallingConventions.Standard, rtnType ?? memberType,
                Array.Empty<Type>(), accessTools, true);
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
                    Logger.LogWarning("Property " + property.Format() + " does not have a getter.", method: Source);
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
            Logger.LogWarning("Error creating " + ((object?)field ?? fieldName).Format() + " accessor.", method: Source);
            if (throwOnFailure)
                throw;
            Logger.LogError(ex, method: Source);
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

    public static void LoadUIToILGenerator<TVanillaUI>(DebuggableEmitter il) where TVanillaUI : class
        => LoadUIToILGenerator(il, typeof(TVanillaUI));
    public static void LoadUIToILGenerator(DebuggableEmitter il, Type uiType)
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
            Logger.LogWarning("Unable to find type for method " + methodName.Format() + ".", method: Source);
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
            Logger.LogError("Unable to find matching method " + methodName.Format() + ".", method: Source);
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
            Logger.LogWarning("Unable to find type for method " + methodName.Format() + ".", method: Source);
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
            Logger.LogError("Unable to find matching method " + methodName.Format() + ".", method: Source);
            if (throwOnFailure)
                throw new Exception("Unable to find matching method: " + methodName + ".");
            return null;
        }

        return GenerateUICaller<TDelegate>(uiType, method, throwOnFailure);
    }
    public static Delegate? GenerateUICaller(Type? uiType, MethodInfo method, bool throwOnFailure = false)
    {
        Accessor.CheckFuncArrays();

        bool rtn = method.ReturnType != typeof(void);
        ParameterInfo[] p = method.GetParameters();
        if (p.Length > (rtn ? Accessor.FuncTypes!.Length : Accessor.ActionTypes!.Length))
        {
            Logger.LogWarning("Method " + method.Format() + " can not have more than " + (rtn ? Accessor.FuncTypes!.Length : Accessor.ActionTypes!.Length) + " arguments!", method: Source);
            if (throwOnFailure)
                throw new ArgumentException("Method can not have more than " + (rtn ? Accessor.FuncTypes!.Length : Accessor.ActionTypes!.Length) + " arguments!", nameof(method));
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
                p2[p2.Length - 1] = method.ReturnType;
                deleType = Accessor.FuncTypes![p.Length].MakeGenericType(p2);
            }
            else
            {
                Type[] p2 = new Type[p.Length];
                for (int i = 1; i < p2.Length; ++i)
                    p2[i] = p[i].ParameterType;
                deleType = Accessor.ActionTypes![p.Length];
                deleType = deleType.MakeGenericType(p2);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Error generating UI caller for " + method.Format() + ".", method: Source);
            Logger.LogError(ex, method: Source);
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
            Logger.LogError("Error generating UI caller of type " + typeof(TDelegate).Format() + ".", method: Source);
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
            Logger.LogError("Error generating UI caller for " + info.Format() + ".", method: Source);
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
            Logger.LogWarning("Unable to find type for method " + method.Format() + ".", method: Source);
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
                il.LoadParameter(i);
            il.Emit(method.IsAbstract || method.IsVirtual ? OpCodes.Callvirt : OpCodes.Call, method);
            il.Emit(OpCodes.Ret);
            return dmethod.CreateDelegate(delegateType);
        }
        catch (Exception ex)
        {
            Logger.LogWarning("Unable to create UI caller for " + (method.DeclaringType?.Format() ?? "<unknown-type>") + "." + method.Name, method: Source);
            Logger.LogError(ex, method: Source);
            if (throwOnFailure)
                throw;
            return null;
        }
    }
    static UIAccessTools()
    {
        try
        {
            Type accessTools = typeof(UIAccessTools);
            MethodInfo getEditorTerrainUI = accessTools.GetProperty(nameof(EditorTerrainUI), BindingFlags.Public | BindingFlags.Static)!.GetMethod;
            const MethodAttributes attr = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig;
            Assembly sdg = Accessor.AssemblyCSharp;

            /*
             * TERRAIN
             */
            Type containerType = typeof(EditorTerrainUI);

            /* HEIGHTS */
            Type? rtnType = sdg.GetType("SDG.Unturned.EditorTerrainHeightUI");
            EditorTerrainHeightUIType = rtnType;
            if (rtnType == null)
            {
                Logger.LogWarning("Unable to find type: SDG.Unturned.EditorTerrainHeightUI.", method: Source);
                DevkitServerModule.Fault();
                return;
            }
            FieldInfo? field = containerType.GetField("heightV2", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public) ??
                               containerType.GetField("heights", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null || field.IsStatic || !rtnType.IsAssignableFrom(field.FieldType))
            {
                Logger.LogWarning("Unable to find field: EditorTerrainUI.heightV2.", method: Source);
                DevkitServerModule.Fault();
                return;
            }
            DynamicMethod method = new DynamicMethod("GetEditorTerrainHeightsUI_Impl", attr,
                CallingConventions.Standard, rtnType,
                Array.Empty<Type>(), accessTools, true);
            ILGenerator il = method.GetILGenerator();
            il.Emit(OpCodes.Call, getEditorTerrainUI);
            il.Emit(OpCodes.Ldfld, field);
            il.Emit(OpCodes.Ret);
            GetEditorTerrainHeightUI = (Func<object?>)method.CreateDelegate(typeof(Func<object>));

            /* MATERIALS */
            rtnType = sdg.GetType("SDG.Unturned.EditorTerrainMaterialsUI");
            EditorTerrainMaterialsUIType = rtnType;
            if (rtnType == null)
            {
                Logger.LogWarning("Unable to find type: SDG.Unturned.EditorTerrainMaterialsUI.", method: Source);
                DevkitServerModule.Fault();
                return;
            }
            field = containerType.GetField("materialsV2", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public) ??
                    containerType.GetField("materials", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null || field.IsStatic || !rtnType.IsAssignableFrom(field.FieldType))
            {
                Logger.LogWarning("Unable to find field: EditorTerrainUI.materialsV2.", method: Source);
                DevkitServerModule.Fault();
                return;
            }
            method = new DynamicMethod("GetEditorTerrainMaterialsUI_Impl", attr,
                CallingConventions.Standard, rtnType,
                Array.Empty<Type>(), accessTools, true);
            il = method.GetILGenerator();
            il.Emit(OpCodes.Call, getEditorTerrainUI);
            il.Emit(OpCodes.Ldfld, field);
            il.Emit(OpCodes.Ret);
            GetEditorTerrainMaterialsUI = (Func<object?>)method.CreateDelegate(typeof(Func<object>));

            /* DETAILS */
            rtnType = sdg.GetType("SDG.Unturned.EditorTerrainDetailsUI");
            EditorTerrainDetailsUIType = rtnType;
            if (rtnType == null)
            {
                Logger.LogWarning("Unable to find type: SDG.Unturned.EditorTerrainDetailsUI.", method: Source);
                DevkitServerModule.Fault();
                return;
            }
            field = containerType.GetField("detailsV2", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public) ??
                    containerType.GetField("details", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null || field.IsStatic || !rtnType.IsAssignableFrom(field.FieldType))
            {
                Logger.LogWarning("Unable to find field: EditorTerrainUI.detailsV2.", method: Source);
                DevkitServerModule.Fault();
                return;
            }
            method = new DynamicMethod("GetEditorTerrainDetailsUI_Impl", attr,
                CallingConventions.Standard, rtnType,
                Array.Empty<Type>(), accessTools, true);
            il = method.GetILGenerator();
            il.Emit(OpCodes.Call, getEditorTerrainUI);
            il.Emit(OpCodes.Ldfld, field);
            il.Emit(OpCodes.Ret);
            GetEditorTerrainDetailsUI = (Func<object?>)method.CreateDelegate(typeof(Func<object>));

            /* TILES */
            rtnType = sdg.GetType("SDG.Unturned.EditorTerrainTilesUI");
            EditorTerrainTilesUIType = rtnType;
            if (rtnType == null)
            {
                Logger.LogWarning("Unable to find type: SDG.Unturned.EditorTerrainTilesUI.", method: Source);
                DevkitServerModule.Fault();
                return;
            }
            field = containerType.GetField("tiles", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public) ??
                    containerType.GetField("tilesV2", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null || field.IsStatic || !rtnType.IsAssignableFrom(field.FieldType))
            {
                Logger.LogWarning("Unable to find field: EditorTerrainUI.tiles.", method: Source);
                DevkitServerModule.Fault();
                return;
            }
            method = new DynamicMethod("GetEditorTerrainTilesUI_Impl", attr,
                CallingConventions.Standard, rtnType,
                Array.Empty<Type>(), accessTools, true);
            il = method.GetILGenerator();
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
                Logger.LogWarning("Unable to find type: SDG.Unturned.EditorEnvironmentNodesUI.", method: Source);
                DevkitServerModule.Fault();
                return;
            }
            field = containerType.GetField("nodesUI", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public) ??
                    containerType.GetField("nodes", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null || !field.IsStatic || !rtnType.IsAssignableFrom(field.FieldType))
            {
                Logger.LogWarning("Unable to find field: EditorEnvironmentUI.nodesUI.", method: Source);
                DevkitServerModule.Fault();
                return;
            }
            method = new DynamicMethod("GetEditorEnvironmentNodesUI_Impl", attr,
                CallingConventions.Standard, rtnType,
                Array.Empty<Type>(), accessTools, true);
            il = method.GetILGenerator();
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
                Logger.LogWarning("Unable to find type: SDG.Unturned.EditorVolumesUI.", method: Source);
                DevkitServerModule.Fault();
                return;
            }
            field = containerType.GetField("volumesUI", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public) ??
                    containerType.GetField("volumes", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null || !field.IsStatic || !rtnType.IsAssignableFrom(field.FieldType))
            {
                Logger.LogWarning("Unable to find field: EditorLevelUI.volumesUI.", method: Source);
                DevkitServerModule.Fault();
                return;
            }
            method = new DynamicMethod("GetEditorVolumesUI_Impl", attr,
                CallingConventions.Standard, rtnType,
                Array.Empty<Type>(), accessTools, true);
            il = method.GetILGenerator();
            il.Emit(OpCodes.Ldsfld, field);
            il.Emit(OpCodes.Ret);
            GetEditorVolumesUI = (Func<object?>)method.CreateDelegate(typeof(Func<object>));

            /*
             * ITEM STORE
             */
            containerType = ItemStoreMenuType;
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
                        Logger.LogWarning("Unable to find field: ItemStoreMenu.cartMenu.", method: Source);
                    }
                    else
                    {
                        method = new DynamicMethod("GetItemStoreCartMenu_Impl", attr,
                            CallingConventions.Standard, rtnType,
                            Array.Empty<Type>(), accessTools, true);
                        il = method.GetILGenerator();
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
                    Logger.LogWarning("Unable to find type: SDG.Unturned.ItemStoreCartMenu.", method: Source);
                }

                /* DETAILS MENU */
                rtnType = ItemStoreDetailsMenuType;
                if (rtnType != null)
                {
                    field = containerType.GetField("detailsMenu", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public) ??
                            containerType.GetField("detailsMenuUI", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (field == null || field.IsStatic || !rtnType.IsAssignableFrom(field.FieldType))
                    {
                        Logger.LogWarning("Unable to find field: ItemStoreMenu.detailsMenu.", method: Source);
                    }
                    else
                    {
                        method = new DynamicMethod("GetItemStoreDetailsMenu_Impl", attr,
                            CallingConventions.Standard, rtnType,
                            Array.Empty<Type>(), accessTools, true);
                        il = method.GetILGenerator();
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
                    Logger.LogWarning("Unable to find type: SDG.Unturned.ItemStoreCartMenu.", method: Source);
                }
            }
            else
            {
                Logger.LogWarning("Unable to find type: SDG.Unturned.ItemStoreMenu.", method: Source);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Error initializing UI access tools.", method: Source);
            Logger.LogError(ex, method: Source);
            DevkitServerModule.Fault();
        }
        finally
        {
            Dictionary<Type, UITypeInfo> typeInfo = new Dictionary<Type, UITypeInfo>(32)
            {
                {
                    typeof(EditorDashboardUI),
                    new UITypeInfo(typeof(EditorDashboardUI), Array.Empty<MethodBase>(), Array.Empty<MethodBase>())
                    {
                        Parent = typeof(EditorUI),
                        Scene = UIScene.Editor,
                        EmitProperty = nameof(EditorDashboardUI),
                        OpenOnInitialize = true,
                        DefaultOpenState = true,
                        CloseOnDestroy = true
                    }
                },
                {
                    typeof(EditorEnvironmentLightingUI),
                    new UITypeInfo(typeof(EditorEnvironmentLightingUI))
                    {
                        Parent = typeof(EditorEnvironmentUI),
                        Scene = UIScene.Editor,
                        IsStaticUI = true
                    }
                },
                {
                    typeof(EditorEnvironmentNavigationUI),
                    new UITypeInfo(typeof(EditorEnvironmentNavigationUI))
                    {
                        Parent = typeof(EditorEnvironmentUI),
                        Scene = UIScene.Editor,
                        IsStaticUI = true
                    }
                },
                {
                    typeof(EditorEnvironmentRoadsUI),
                    new UITypeInfo(typeof(EditorEnvironmentRoadsUI))
                    {
                        Parent = typeof(EditorEnvironmentUI),
                        Scene = UIScene.Editor,
                        IsStaticUI = true
                    }
                },
                {
                    typeof(EditorEnvironmentUI),
                    new UITypeInfo(typeof(EditorEnvironmentUI))
                    {
                        Parent = typeof(EditorDashboardUI),
                        Scene = UIScene.Editor,
                        EmitProperty = nameof(EditorEnvironmentUI)
                    }
                },
                {
                    typeof(EditorLevelObjectsUI),
                    new UITypeInfo(typeof(EditorLevelObjectsUI))
                    {
                        Parent = typeof(EditorLevelUI),
                        Scene = UIScene.Editor,
                        EmitProperty = nameof(EditorLevelObjectsUI)
                    }
                },
                {
                    typeof(EditorLevelPlayersUI),
                    new UITypeInfo(typeof(EditorLevelPlayersUI))
                    {
                        Parent = typeof(EditorLevelUI),
                        Scene = UIScene.Editor,
                        IsStaticUI = true
                    }
                },
                {
                    typeof(EditorLevelUI),
                    new UITypeInfo(typeof(EditorLevelUI))
                    {
                        Parent = typeof(EditorDashboardUI),
                        Scene = UIScene.Editor,
                        EmitProperty = nameof(EditorLevelUI)
                    }
                },
                {
                    typeof(EditorLevelVisibilityUI),
                    new UITypeInfo(typeof(EditorLevelVisibilityUI))
                    {
                        Parent = typeof(EditorLevelUI),
                        Scene = UIScene.Editor,
                        IsStaticUI = true
                    }
                },
                {
                    typeof(EditorPauseUI),
                    new UITypeInfo(typeof(EditorPauseUI))
                    {
                        Parent = typeof(EditorDashboardUI),
                        Scene = UIScene.Editor,
                        IsStaticUI = true
                    }
                },
                {
                    typeof(EditorSpawnsAnimalsUI),
                    new UITypeInfo(typeof(EditorSpawnsAnimalsUI))
                    {
                        Parent = typeof(EditorSpawnsUI),
                        Scene = UIScene.Editor,
                        IsStaticUI = true
                    }
                },
                {
                    typeof(EditorSpawnsItemsUI),
                    new UITypeInfo(typeof(EditorSpawnsItemsUI))
                    {
                        Parent = typeof(EditorSpawnsUI),
                        Scene = UIScene.Editor,
                        IsStaticUI = true
                    }
                },
                {
                    typeof(EditorSpawnsUI),
                    new UITypeInfo(typeof(EditorSpawnsUI))
                    {
                        Parent = typeof(EditorDashboardUI),
                        Scene = UIScene.Editor,
                        IsStaticUI = true
                    }
                },
                {
                    typeof(EditorSpawnsVehiclesUI),
                    new UITypeInfo(typeof(EditorSpawnsVehiclesUI))
                    {
                        Parent = typeof(EditorSpawnsUI),
                        Scene = UIScene.Editor,
                        IsStaticUI = true
                    }
                },
                {
                    typeof(EditorSpawnsZombiesUI),
                    new UITypeInfo(typeof(EditorSpawnsZombiesUI))
                    {
                        Parent = typeof(EditorSpawnsUI),
                        Scene = UIScene.Editor,
                        IsStaticUI = true
                    }
                },
                {
                    typeof(EditorTerrainUI),
                    new UITypeInfo(typeof(EditorTerrainUI))
                    {
                        Parent = typeof(EditorDashboardUI),
                        Scene = UIScene.Editor,
                        EmitProperty = nameof(EditorTerrainUI)
                    }
                },
                {
                    typeof(EditorUI),
                    new UITypeInfo(typeof(EditorUI), Array.Empty<MethodBase>(), Array.Empty<MethodBase>())
                    {
                        Scene = UIScene.Editor,
                        EmitProperty = nameof(EditorUI),
                        OpenOnInitialize = true,
                        DefaultOpenState = true,
                        CloseOnDestroy = true
                    }
                },
                {
                    typeof(LoadingUI),
                    new UITypeInfo(typeof(LoadingUI), Array.Empty<MethodBase>(), Array.Empty<MethodBase>())
                    {
                        Scene = UIScene.Loading,
                        EmitProperty = nameof(LoadingUI),
                        OpenOnInitialize = true,
                        DefaultOpenState = true,
                        CloseOnDestroy = true
                    }
                },
                {
                    typeof(MenuConfigurationControlsUI),
                    new UITypeInfo(typeof(MenuConfigurationControlsUI))
                    {
                        Parent = typeof(MenuConfigurationUI),
                        Scene = UIScene.Menu,
                        IsStaticUI = true
                    }
                },
                {
                    typeof(MenuConfigurationDisplayUI),
                    new UITypeInfo(typeof(MenuConfigurationDisplayUI))
                    {
                        Parent = typeof(MenuConfigurationUI),
                        Scene = UIScene.Menu,
                        IsStaticUI = true
                    }
                },
                {
                    typeof(MenuConfigurationGraphicsUI),
                    new UITypeInfo(typeof(MenuConfigurationGraphicsUI))
                    {
                        Parent = typeof(MenuConfigurationUI),
                        Scene = UIScene.Menu,
                        IsStaticUI = true
                    }
                },
                {
                    typeof(MenuConfigurationOptionsUI),
                    new UITypeInfo(typeof(MenuConfigurationOptionsUI))
                    {
                        Parent = typeof(MenuConfigurationUI),
                        Scene = UIScene.Menu,
                        IsStaticUI = true
                    }
                },
                {
                    typeof(MenuConfigurationUI),
                    new UITypeInfo(typeof(MenuConfigurationUI))
                    {
                        Parent = typeof(MenuDashboardUI),
                        Scene = UIScene.Menu,
                        EmitProperty = nameof(MenuConfigurationUI)
                    }
                },
                {
                    typeof(MenuCreditsUI),
                    new UITypeInfo(typeof(MenuCreditsUI))
                    {
                        Parent = typeof(MenuDashboardUI),
                        Scene = UIScene.Menu,
                        EmitProperty = nameof(MenuCreditsUI)
                    }
                },
                {
                    typeof(MenuDashboardUI),
                    new UITypeInfo(typeof(MenuDashboardUI))
                    {
                        Parent = typeof(MenuUI),
                        Scene = UIScene.Menu,
                        EmitProperty = nameof(MenuDashboardUI)
                    }
                },
                {
                    typeof(MenuPauseUI),
                    new UITypeInfo(typeof(MenuPauseUI))
                    {
                        Parent = typeof(MenuDashboardUI),
                        Scene = UIScene.Menu,
                        EmitProperty = nameof(MenuPauseUI)
                    }
                },
                {
                    typeof(MenuPlayConfigUI),
                    new UITypeInfo(typeof(MenuPlayConfigUI))
                    {
                        Parent = typeof(MenuDashboardUI),
                        Scene = UIScene.Menu,
                        IsStaticUI = true
                    }
                },
                {
                    typeof(MenuPlayConnectUI),
                    new UITypeInfo(typeof(MenuPlayConnectUI))
                    {
                        Parent = typeof(MenuPlayUI),
                        Scene = UIScene.Menu,
                        EmitProperty = nameof(MenuPlayConnectUI)
                    }
                },
                {
                    typeof(MenuPlayLobbiesUI),
                    new UITypeInfo(typeof(MenuPlayLobbiesUI))
                    {
                        Parent = typeof(MenuPlayUI),
                        Scene = UIScene.Menu,
                        EmitProperty = nameof(MenuPlayLobbiesUI)
                    }
                },
                {
                    typeof(MenuPlayMatchmakingUI),
                    new UITypeInfo(typeof(MenuPlayMatchmakingUI))
                    {
                        Parent = typeof(MenuPlayUI),
                        Scene = UIScene.Menu,
                        EmitProperty = nameof(MenuPlayMatchmakingUI)
                    }
                },
                {
                    typeof(MenuPlayServerInfoUI),
                    new UITypeInfo(typeof(MenuPlayServerInfoUI))
                    {
                        Parent = typeof(MenuPlayUI),
                        Scene = UIScene.Menu,
                        EmitProperty = nameof(MenuPlayServerInfoUI)
                    }
                },
                {
                    typeof(MenuPlayServersUI),
                    new UITypeInfo(typeof(MenuPlayServersUI))
                    {
                        Parent = typeof(MenuPlayUI),
                        Scene = UIScene.Menu,
                        EmitProperty = nameof(MenuPlayServersUI)
                    }
                },
                {
                    typeof(MenuPlaySingleplayerUI),
                    new UITypeInfo(typeof(MenuPlaySingleplayerUI))
                    {
                        Parent = typeof(MenuPlayUI),
                        Scene = UIScene.Menu,
                        EmitProperty = nameof(MenuPlaySingleplayerUI)
                    }
                },
                {
                    typeof(MenuPlayUI),
                    new UITypeInfo(typeof(MenuPlayUI))
                    {
                        Parent = typeof(MenuDashboardUI),
                        Scene = UIScene.Menu,
                        EmitProperty = nameof(MenuPlayUI)
                    }
                },
                {
                    typeof(MenuServerPasswordUI),
                    new UITypeInfo(typeof(MenuServerPasswordUI))
                    {
                        Parent = typeof(MenuPlayServerInfoUI),
                        Scene = UIScene.Menu,
                        EmitProperty = nameof(MenuServerPasswordUI)
                    }
                },
                {
                    typeof(MenuSurvivorsAppearanceUI),
                    new UITypeInfo(typeof(MenuSurvivorsAppearanceUI))
                    {
                        Parent = typeof(MenuSurvivorsUI),
                        Scene = UIScene.Menu,
                        EmitProperty = nameof(MenuSurvivorsAppearanceUI)
                    }
                },
                {
                    typeof(MenuSurvivorsCharacterUI),
                    new UITypeInfo(typeof(MenuSurvivorsCharacterUI))
                    {
                        Parent = typeof(MenuSurvivorsUI),
                        Scene = UIScene.Menu,
                        EmitProperty = nameof(MenuSurvivorsCharacterUI)
                    }
                },
                {
                    typeof(MenuSurvivorsClothingBoxUI),
                    new UITypeInfo(typeof(MenuSurvivorsClothingBoxUI))
                    {
                        Parent = typeof(MenuSurvivorsClothingUI),
                        Scene = UIScene.Menu,
                        EmitProperty = nameof(MenuSurvivorsClothingBoxUI)
                    }
                },
                {
                    typeof(MenuSurvivorsClothingDeleteUI),
                    new UITypeInfo(typeof(MenuSurvivorsClothingDeleteUI))
                    {
                        Parent = typeof(MenuSurvivorsClothingUI),
                        Scene = UIScene.Menu,
                        EmitProperty = nameof(MenuSurvivorsClothingDeleteUI)
                    }
                },
                {
                    typeof(MenuSurvivorsClothingInspectUI),
                    new UITypeInfo(typeof(MenuSurvivorsClothingInspectUI))
                    {
                        Parent = typeof(MenuSurvivorsClothingUI),
                        Scene = UIScene.Menu,
                        EmitProperty = nameof(MenuSurvivorsClothingInspectUI)
                    }
                },
                {
                    typeof(MenuSurvivorsClothingItemUI),
                    new UITypeInfo(typeof(MenuSurvivorsClothingItemUI))
                    {
                        Parent = typeof(MenuSurvivorsClothingUI),
                        Scene = UIScene.Menu,
                        EmitProperty = nameof(MenuSurvivorsClothingItemUI)
                    }
                },
                {
                    typeof(MenuSurvivorsClothingUI),
                    new UITypeInfo(typeof(MenuSurvivorsClothingUI))
                    {
                        Parent = typeof(MenuSurvivorsUI),
                        Scene = UIScene.Menu,
                        EmitProperty = nameof(MenuSurvivorsClothingUI)
                    }
                },
                {
                    typeof(MenuSurvivorsGroupUI),
                    new UITypeInfo(typeof(MenuSurvivorsGroupUI))
                    {
                        Parent = typeof(MenuSurvivorsUI),
                        Scene = UIScene.Menu,
                        EmitProperty = nameof(MenuSurvivorsGroupUI)
                    }
                },
                {
                    typeof(MenuSurvivorsUI),
                    new UITypeInfo(typeof(MenuSurvivorsUI))
                    {
                        Parent = typeof(MenuDashboardUI),
                        Scene = UIScene.Menu,
                        EmitProperty = nameof(MenuSurvivorsUI)
                    }
                },
                {
                    typeof(MenuTitleUI),
                    new UITypeInfo(typeof(MenuTitleUI))
                    {
                        Parent = typeof(MenuDashboardUI),
                        Scene = UIScene.Menu,
                        EmitProperty = nameof(MenuTitleUI)
                    }
                },
                {
                    typeof(MenuUI),
                    new UITypeInfo(typeof(MenuUI), Array.Empty<MethodBase>(), Array.Empty<MethodBase>())
                    {
                        Scene = UIScene.Menu,
                        EmitProperty = nameof(MenuUI),
                        OpenOnInitialize = true,
                        DefaultOpenState = true,
                        CloseOnDestroy = true
                    }
                },
                {
                    typeof(MenuWorkshopEditorUI),
                    new UITypeInfo(typeof(MenuWorkshopEditorUI))
                    {
                        Parent = typeof(MenuWorkshopUI),
                        Scene = UIScene.Menu,
                        EmitProperty = nameof(MenuWorkshopEditorUI)
                    }
                },
                {
                    typeof(MenuWorkshopErrorUI),
                    new UITypeInfo(typeof(MenuWorkshopErrorUI))
                    {
                        Parent = typeof(MenuWorkshopUI),
                        Scene = UIScene.Menu,
                        EmitProperty = nameof(MenuWorkshopErrorUI)
                    }
                },
                {
                    typeof(MenuWorkshopLocalizationUI),
                    new UITypeInfo(typeof(MenuWorkshopLocalizationUI))
                    {
                        Parent = typeof(MenuWorkshopUI),
                        Scene = UIScene.Menu,
                        EmitProperty = nameof(MenuWorkshopLocalizationUI)
                    }
                },
                {
                    typeof(MenuWorkshopSpawnsUI),
                    new UITypeInfo(typeof(MenuWorkshopSpawnsUI))
                    {
                        Parent = typeof(MenuWorkshopUI),
                        Scene = UIScene.Menu,
                        EmitProperty = nameof(MenuWorkshopSpawnsUI)
                    }
                },
                {
                    typeof(MenuWorkshopSubmitUI),
                    new UITypeInfo(typeof(MenuWorkshopSubmitUI))
                    {
                        Parent = typeof(MenuWorkshopUI),
                        Scene = UIScene.Menu,
                        EmitProperty = nameof(MenuWorkshopSubmitUI)
                    }
                },
                {
                    typeof(MenuWorkshopSubscriptionsUI),
                    new UITypeInfo(typeof(MenuWorkshopSubscriptionsUI))
                    {
                        Parent = typeof(MenuWorkshopUI),
                        Scene = UIScene.Menu,
                        EmitProperty = nameof(MenuWorkshopSubscriptionsUI)
                    }
                },
                {
                    typeof(MenuWorkshopUI),
                    new UITypeInfo(typeof(MenuWorkshopUI))
                    {
                        Parent = typeof(MenuDashboardUI),
                        Scene = UIScene.Menu,
                        EmitProperty = nameof(MenuWorkshopUI)
                    }
                },
                {
                    typeof(PlayerBarricadeLibraryUI),
                    new UITypeInfo(typeof(PlayerBarricadeLibraryUI))
                    {
                        Parent = typeof(PlayerUI),
                        Scene = UIScene.Player,
                        IsStaticUI = true
                    }
                },
                {
                    typeof(PlayerBarricadeMannequinUI),
                    new UITypeInfo(typeof(PlayerBarricadeMannequinUI))
                    {
                        Parent = typeof(PlayerUI),
                        Scene = UIScene.Player,
                        EmitProperty = nameof(PlayerBarricadeMannequinUI)
                    }
                },
                {
                    typeof(PlayerBarricadeSignUI),
                    new UITypeInfo(typeof(PlayerBarricadeSignUI))
                    {
                        Parent = typeof(PlayerUI),
                        Scene = UIScene.Player,
                        IsStaticUI = true
                    }
                },
                {
                    typeof(PlayerBarricadeStereoUI),
                    new UITypeInfo(typeof(PlayerBarricadeStereoUI))
                    {
                        Parent = typeof(PlayerUI),
                        Scene = UIScene.Player,
                        EmitProperty = nameof(PlayerBarricadeStereoUI)
                    }
                },
                {
                    typeof(PlayerDashboardCraftingUI),
                    new UITypeInfo(typeof(PlayerDashboardCraftingUI))
                    {
                        Parent = typeof(PlayerDashboardUI),
                        Scene = UIScene.Player,
                        IsStaticUI = true
                    }
                },
                {
                    typeof(PlayerDashboardInformationUI),
                    new UITypeInfo(typeof(PlayerDashboardInformationUI))
                    {
                        Parent = typeof(PlayerDashboardUI),
                        Scene = UIScene.Player,
                        EmitProperty = nameof(PlayerDashboardInformationUI)
                    }
                },
                {
                    typeof(PlayerDashboardInventoryUI),
                    new UITypeInfo(typeof(PlayerDashboardInventoryUI))
                    {
                        Parent = typeof(PlayerDashboardUI),
                        Scene = UIScene.Player,
                        IsStaticUI = true
                    }
                },
                {
                    typeof(PlayerDashboardSkillsUI),
                    new UITypeInfo(typeof(PlayerDashboardSkillsUI))
                    {
                        Parent = typeof(PlayerDashboardUI),
                        Scene = UIScene.Player,
                        IsStaticUI = true
                    }
                },
                {
                    typeof(PlayerDashboardUI),
                    new UITypeInfo(typeof(PlayerDashboardUI))
                    {
                        Parent = typeof(PlayerUI),
                        Scene = UIScene.Player,
                        EmitProperty = nameof(PlayerDashboardUI)
                    }
                },
                {
                    typeof(PlayerDeathUI),
                    new UITypeInfo(typeof(PlayerDeathUI))
                    {
                        Parent = typeof(PlayerUI),
                        Scene = UIScene.Player,
                        IsStaticUI = true
                    }
                },
                {
                    typeof(PlayerLifeUI),
                    new UITypeInfo(typeof(PlayerLifeUI))
                    {
                        Parent = typeof(PlayerUI),
                        Scene = UIScene.Player,
                        EmitProperty = nameof(PlayerLifeUI)
                    }
                },
                {
                    typeof(PlayerNPCDialogueUI),
                    new UITypeInfo(typeof(PlayerNPCDialogueUI))
                    {
                        Parent = typeof(PlayerUI),
                        Scene = UIScene.Player,
                        IsStaticUI = true
                    }
                },
                {
                    typeof(PlayerNPCQuestUI),
                    new UITypeInfo(typeof(PlayerNPCQuestUI))
                    {
                        Parent = typeof(PlayerUI),
                        Scene = UIScene.Player,
                        IsStaticUI = true
                    }
                },
                {
                    typeof(PlayerNPCVendorUI),
                    new UITypeInfo(typeof(PlayerNPCVendorUI))
                    {
                        Parent = typeof(PlayerUI),
                        Scene = UIScene.Player,
                        IsStaticUI = true
                    }
                },
                {
                    typeof(PlayerPauseUI),
                    new UITypeInfo(typeof(PlayerPauseUI))
                    {
                        Parent = typeof(PlayerUI),
                        Scene = UIScene.Player,
                        EmitProperty = nameof(PlayerPauseUI)
                    }
                },
                {
                    typeof(PlayerUI),
                    new UITypeInfo(typeof(PlayerUI), Array.Empty<MethodBase>(), Array.Empty<MethodBase>())
                    {
                        Scene = UIScene.Player,
                        EmitProperty = nameof(PlayerUI),
                        OpenOnInitialize = true,
                        DefaultOpenState = true,
                        CloseOnDestroy = true
                    }
                },
                {
                    typeof(PlayerWorkzoneUI),
                    new UITypeInfo(typeof(PlayerWorkzoneUI))
                    {
                        Parent = typeof(PlayerUI),
                        Scene = UIScene.Player,
                        IsStaticUI = true
                    }
                },
            };
            if (EditorEnvironmentNodesUIType != null)
            {
                typeInfo.Add(
                    EditorEnvironmentNodesUIType, 
                    new UITypeInfo(EditorEnvironmentNodesUIType)
                    {
                        Parent = typeof(EditorEnvironmentUI),
                        Scene = UIScene.Editor,
                        EmitProperty = nameof(EditorEnvironmentNodesUI)
                    });
            }
            if (EditorVolumesUIType != null)
            {
                typeInfo.Add(
                    EditorVolumesUIType,
                    new UITypeInfo(EditorVolumesUIType)
                    {
                        Parent = typeof(EditorLevelUI),
                        Scene = UIScene.Editor,
                        EmitProperty = nameof(EditorVolumesUI)
                    });
            }
            if (EditorTerrainDetailsUIType != null)
            {
                typeInfo.Add(
                    EditorTerrainDetailsUIType,
                    new UITypeInfo(EditorTerrainDetailsUIType)
                    {
                        Parent = typeof(EditorTerrainUI),
                        Scene = UIScene.Editor,
                        EmitProperty = nameof(EditorTerrainDetailsUI)
                    });
            }
            if (EditorTerrainHeightUIType != null)
            {
                typeInfo.Add(
                    EditorTerrainHeightUIType,
                    new UITypeInfo(EditorTerrainHeightUIType)
                    {
                        Parent = typeof(EditorTerrainUI),
                        Scene = UIScene.Editor,
                        EmitProperty = nameof(EditorTerrainHeightUI)
                    });
            }
            if (EditorTerrainTilesUIType != null)
            {
                typeInfo.Add(
                    EditorTerrainTilesUIType,
                    new UITypeInfo(EditorTerrainTilesUIType)
                    {
                        Parent = typeof(EditorTerrainUI),
                        Scene = UIScene.Editor,
                        EmitProperty = nameof(EditorTerrainTilesUI)
                    });
            }
            if (PlayerBrowserRequestUIType != null)
            {
                typeInfo.Add(
                    PlayerBrowserRequestUIType,
                    new UITypeInfo(PlayerBrowserRequestUIType)
                    {
                        Parent = typeof(PlayerUI),
                        Scene = UIScene.Player,
                        EmitProperty = nameof(PlayerBrowserRequestUI)
                    });
            }
            else
                Logger.LogWarning("Unable to find type: SDG.Unturned.PlayerBrowserRequestUI.", method: Source);
            if (PlayerGroupUIType != null)
            {
                typeInfo.Add(
                    PlayerGroupUIType,
                    new UITypeInfo(PlayerGroupUIType, Array.Empty<MethodBase>(), Array.Empty<MethodBase>())
                    {
                        Parent = typeof(PlayerUI),
                        Scene = UIScene.Player,
                        EmitProperty = nameof(PlayerGroupUI),
                        OpenOnInitialize = true,
                        DefaultOpenState = true,
                        CloseOnDestroy = true
                    });
            }
            else
                Logger.LogWarning("Unable to find type: SDG.Unturned.PlayerGroupUI.", method: Source);
            if (ItemStoreMenuType != null)
            {
                typeInfo.Add(
                    ItemStoreMenuType,
                    new UITypeInfo(ItemStoreMenuType)
                    {
                        Parent = typeof(MenuSurvivorsClothingUI),
                        Scene = UIScene.Menu,
                        EmitProperty = nameof(ItemStoreMenu)
                    });
            }
            else
                Logger.LogWarning("Unable to find type: SDG.Unturned.ItemStoreMenu.", method: Source);
            if (ItemStoreCartMenuType != null)
            {
                typeInfo.Add(
                    ItemStoreCartMenuType,
                    new UITypeInfo(ItemStoreCartMenuType)
                    {
                        Parent = ItemStoreMenuType,
                        Scene = UIScene.Menu,
                        EmitProperty = nameof(ItemStoreCartMenu)
                    });
            }
            else
                Logger.LogWarning("Unable to find type: SDG.Unturned.ItemStoreCartMenu.", method: Source);
            if (ItemStoreDetailsMenuType != null)
            {
                typeInfo.Add(
                    ItemStoreDetailsMenuType,
                    new UITypeInfo(ItemStoreDetailsMenuType)
                    {
                        Parent = ItemStoreMenuType,
                        Scene = UIScene.Menu,
                        EmitProperty = nameof(ItemStoreDetailsMenu)
                    });
            }
            else
                Logger.LogWarning("Unable to find type: SDG.Unturned.ItemStoreDetailsMenu.", method: Source);
            
            try
            {
                List<Type> types = Accessor.GetTypesSafe(Accessor.AssemblyCSharp);
                foreach (Type menuType in types
                             .Where(x =>
                                 x.Name.Equals("Menu", StringComparison.Ordinal) &&
                                 x.DeclaringType != null &&
                                 typeof(SleekWrapper).IsAssignableFrom(x)))
                {
                    MenuTypes[menuType.DeclaringType] = menuType;
                    if (typeof(VolumeBase).IsAssignableFrom(menuType.DeclaringType))
                    {
                        typeInfo[menuType] = new UITypeInfo(menuType, Array.Empty<MethodBase>(), Array.Empty<MethodBase>(), destroyMethods: Array.Empty<MethodBase>())
                        {
                            Parent = EditorVolumesUIType,
                            Scene = UIScene.Editor,
                            CustomEmitter = EmitVolumeMenu,
                            DefaultOpenState = true,
                            OpenOnInitialize = true,
                            /* todo
                            CustomOnClose = true,
                            CustomOnOpen = true,
                            CustomOnDestroy = true */
                        };
                    }
                    else if (typeof(TempNodeBase).IsAssignableFrom(menuType.DeclaringType))
                    {
                        typeInfo[menuType] = new UITypeInfo(menuType, Array.Empty<MethodBase>(), Array.Empty<MethodBase>())
                        {
                            Parent = EditorEnvironmentNodesUIType,
                            Scene = UIScene.Editor,
                            CustomEmitter = EmitNodeMenu,
                            DefaultOpenState = true,
                            OpenOnInitialize = true,
                            /* todo
                            CustomOnClose = true,
                            CustomOnOpen = true,
                            CustomOnDestroy = true */
                        };
                    }
                }
                Logger.LogDebug($"[{Source}] Discovered {MenuTypes.Count} editor node/volume menu type(s).");
                int c = typeInfo.Count;
                foreach (Type sleekWrapper in types.Where(x => typeof(SleekWrapper).IsAssignableFrom(x)))
                {
                    if (typeInfo.ContainsKey(sleekWrapper))
                        continue;

                    typeInfo.Add(sleekWrapper, new UITypeInfo(sleekWrapper, Array.Empty<MethodBase>(), Array.Empty<MethodBase>())
                    {
                        Parent = null,
                        Scene = UIScene.Global,
                        IsInstanceUI = false,
                        DefaultOpenState = true,
                        OpenOnInitialize = true,
                        /* todo
                        CustomOnClose = true,
                        CustomOnOpen = true,
                        CustomOnDestroy = true */
                    });
                }
                Logger.LogDebug($"[{Source}] Discovered {typeInfo.Count - c} UI wrapper type(s).");
                c = typeInfo.Count;
                foreach (Type useable in types.Where(x => typeof(Useable).IsAssignableFrom(x)))
                {
                    if (typeInfo.ContainsKey(useable))
                        continue;
                    FieldInfo[] fields = useable.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    PropertyInfo[] properties = useable.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (!fields.Any(x => typeof(ISleekElement).IsAssignableFrom(x.FieldType) && !properties.Any(x => typeof(ISleekElement).IsAssignableFrom(x.PropertyType))))
                        continue;

                    typeInfo.Add(useable, new UITypeInfo(useable, Array.Empty<MethodBase>(), Array.Empty<MethodBase>())
                    {
                        Parent = typeof(PlayerUI),
                        Scene = UIScene.Player,
                        CustomEmitter = EmitUseable,
                        /* todo
                        CustomOnClose = true,
                        CustomOnOpen = true,
                        CustomOnDestroy = true */
                    });
                }
                Logger.LogDebug($"[{Source}] Discovered {typeInfo.Count - c} useable UI type(s).");
            }
            catch (Exception ex)
            {
                Logger.LogWarning("Error initializing volume and node UI info.", method: Source);
                Logger.LogError(ex, method: Source);
            }

            TypeInfoIntl = typeInfo;
            Logger.LogDebug($"[{Source}] Discovered {typeInfo.Count} UI type(s).");
            TypeInfo = new ReadOnlyDictionary<Type, UITypeInfo>(typeInfo);
        }
    }

    private static void EmitVolumeMenu(UITypeInfo info, DebuggableEmitter generator)
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
    private static void EmitNodeMenu(UITypeInfo info, DebuggableEmitter generator)
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
    private static void EmitUseable(UITypeInfo info, DebuggableEmitter generator)
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
        Logger.LogInfo("Editor UI ready.");
    }

    [HarmonyPatch(typeof(PlayerUI), "InitializePlayer")]
    [HarmonyPostfix]
    [UsedImplicitly]
    private static void PlayerUIStartPostfix()
    {
        PlayerUIReady?.Invoke();
        Logger.LogInfo("Player UI ready.");
    }
}
public class UITypeInfo
{
    public Type Type { get; }
    public Type? Parent { get; internal set; }
    public bool IsStaticUI { get; internal set; }
    public bool IsInstanceUI { get; internal set; } = true;
    public UIScene Scene { get; internal set; }
    public string? EmitProperty { get; internal set; }
    public Action<UITypeInfo, DebuggableEmitter>? CustomEmitter { get; internal set; }
    public UIVisibilityMethodInfo[] OpenMethods { get; }
    public UIVisibilityMethodInfo[] CloseMethods { get; }
    public UIVisibilityMethodInfo[] InitializeMethods { get; }
    public UIVisibilityMethodInfo[] DestroyMethods { get; }
    public ICustomOnOpen? CustomOnOpen { get; internal set; }
    public ICustomOnClose? CustomOnClose { get; internal set; }
    public ICustomOnInitialize? CustomOnInitialize { get; internal set; }
    public ICustomOnDestroy? CustomOnDestroy { get; internal set; }
    public bool DefaultOpenState { get; internal set; }
    public bool OpenOnInitialize { get; internal set; }
    public bool CloseOnDestroy { get; internal set; }
    public bool DestroyOnClose { get; internal set; }
    
    internal UITypeInfo(Type type, MethodBase[]? closeMethods = null, MethodBase[]? openMethods = null, MethodBase[]? initializeMethods = null, MethodBase[]? destroyMethods = null)
    {
        Type = type;
        MethodInfo[]? methods = closeMethods != null && openMethods != null && destroyMethods != null ? null : type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
        // ReSharper disable CoVariantArrayConversion
        if (openMethods == null)
        {
            try
            {
                List<MethodInfo> matches = methods!.Where(x => x.Name.Equals("Open", StringComparison.InvariantCultureIgnoreCase)).ToList();
                RemoveMatchesFromBaseClasses(type, matches);
                OpenMethods = new UIVisibilityMethodInfo[matches.Count];
                for (int i = 0; i < matches.Count; ++i)
                {
                    MethodBase method = matches[i];
                    OpenMethods[i] = new UIVisibilityMethodInfo(method, method.GetParameters().Length > 0, method.IsStatic);
                }

                if (OpenMethods.Length == 0)
                    Logger.LogWarning($"Failed to find any open methods for UI: {type.Format()}.", method: UIAccessTools.Source);

            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Error finding any open methods for UI: {type.Format()}.", method: UIAccessTools.Source);
                Logger.LogError(ex, method: UIAccessTools.Source);
                OpenMethods = Array.Empty<UIVisibilityMethodInfo>();
            }
        }
        else
        {
            OpenMethods = new UIVisibilityMethodInfo[openMethods.Length];
            for (int i = 0; i < openMethods.Length; ++i)
            {
                MethodBase method = openMethods[i];
                OpenMethods[i] = new UIVisibilityMethodInfo(method, method.GetParameters().Length > 0, method.IsStatic);
            }
        }
        if (closeMethods == null)
        {
            try
            {
                List<MethodInfo> matches = methods!.Where(x => x.Name.Equals("Close", StringComparison.InvariantCultureIgnoreCase)).ToList();
                RemoveMatchesFromBaseClasses(type, matches);
                CloseMethods = new UIVisibilityMethodInfo[matches.Count];
                for (int i = 0; i < matches.Count; ++i)
                {
                    MethodBase method = matches[i];
                    CloseMethods[i] = new UIVisibilityMethodInfo(method, method.GetParameters().Length > 0, method.IsStatic);
                }

                if (CloseMethods.Length == 0)
                    Logger.LogWarning($"Failed to find any close methods for UI: {type.Format()}.", method: UIAccessTools.Source);
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Error finding any close methods for UI: {type.Format()}.", method: UIAccessTools.Source);
                Logger.LogError(ex, method: UIAccessTools.Source);
                CloseMethods = Array.Empty<UIVisibilityMethodInfo>();
            }
        }
        else
        {
            CloseMethods = new UIVisibilityMethodInfo[closeMethods.Length];
            for (int i = 0; i < closeMethods.Length; ++i)
            {
                MethodBase method = closeMethods[i];
                CloseMethods[i] = new UIVisibilityMethodInfo(method, method.GetParameters().Length > 0, method.IsStatic);
            }
        }
        if (initializeMethods == null)
        {
            Thread.BeginCriticalRegion();
            try
            {
                ConstructorInfo[] constructors = type.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                Logger.LogDebug($"[{UIAccessTools.Source}] Found constructors for UI type: {type.Format()}, {constructors.Length.Format()} ctor(s).");

                InitializeMethods = new UIVisibilityMethodInfo[constructors.Length];
                Logger.LogDebug($"[{UIAccessTools.Source}] Made array: {InitializeMethods.Length.Format()}.");

                for (int i = 0; i < constructors.Length; ++i)
                {
                    ConstructorInfo ctor = constructors[i];
                    Logger.LogDebug($"[{UIAccessTools.Source}] #{(i + 1).Format()}, Found constructor {ctor.Format()}.");

                    InitializeMethods[i] = new UIVisibilityMethodInfo(ctor, ctor.GetParameters().Length > 0, ctor.IsStatic);
                }

                int length = InitializeMethods.Length;
                Logger.LogDebug($"[{UIAccessTools.Source}] Array: {length.Format()}.");

                if (length == 0)
                {
                    Logger.LogWarning($"Failed to find any initialize constructors for UI: {type.Format()}.", method: UIAccessTools.Source);
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Error finding any initialize constructors for UI: {type.Format()}.", method: UIAccessTools.Source);
                Logger.LogError(ex, method: UIAccessTools.Source);
                InitializeMethods = Array.Empty<UIVisibilityMethodInfo>();
            }
            finally
            {
                Thread.EndCriticalRegion();
            }
        }
        else
        {
            InitializeMethods = new UIVisibilityMethodInfo[initializeMethods.Length];
            for (int i = 0; i < initializeMethods.Length; ++i)
            {
                MethodBase method = initializeMethods[i];
                InitializeMethods[i] = new UIVisibilityMethodInfo(method, method.GetParameters().Length > 0, method.IsStatic);
            }
        }
        if (destroyMethods == null)
        {
            try
            {
                List<MethodInfo> matches = methods!.Where(x => x.Name.Equals("OnDestroy", StringComparison.InvariantCultureIgnoreCase) || x.Name.Equals("destroy", StringComparison.InvariantCultureIgnoreCase)).ToList();
                RemoveMatchesFromBaseClasses(type, matches);
                DestroyMethods = new UIVisibilityMethodInfo[matches.Count];
                for (int i = 0; i < matches.Count; ++i)
                {
                    MethodBase method = matches[i];
                    DestroyMethods[i] = new UIVisibilityMethodInfo(method, method.GetParameters().Length > 0, method.IsStatic);
                }

                if (DestroyMethods.Length == 0)
                    Logger.LogWarning($"Failed to find any initialize constructors for UI: {type.Format()}.", method: UIAccessTools.Source);
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Error finding any initialize constructors for UI: {type.Format()}.", method: UIAccessTools.Source);
                Logger.LogError(ex, method: UIAccessTools.Source);
                DestroyMethods = Array.Empty<UIVisibilityMethodInfo>();
            }
        }
        else
        {
            DestroyMethods = new UIVisibilityMethodInfo[destroyMethods.Length];
            for (int i = 0; i < destroyMethods.Length; ++i)
            {
                MethodBase method = destroyMethods[i];
                DestroyMethods[i] = new UIVisibilityMethodInfo(method, method.GetParameters().Length > 0, method.IsStatic);
            }
        }
        // ReSharper restore CoVariantArrayConversion
    }
    private static void RemoveMatchesFromBaseClasses(Type type, List<MethodInfo> matches)
    {
        if (matches.Any(x => x.DeclaringType == type))
        {
            matches.RemoveAll(x => x.DeclaringType != type);
        }
    }
}
public readonly struct UIVisibilityMethodInfo
{
    public MethodBase Method { get; }
    public bool IsParameterized { get; }
    public bool IsStatic { get; }
    public UIVisibilityMethodInfo(MethodBase method, bool isParameterized, bool isStatic)
    {
        Method = method;
        IsParameterized = isParameterized;
        IsStatic = isStatic;
    }
}

public enum UIScene
{
    Global,
    Loading,
    Menu,
    Player,
    Editor
}
#endif