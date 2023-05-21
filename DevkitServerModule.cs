#if SERVER
// #define USERINPUT_DEBUG
#endif

#define TILE_SYNC

using DevkitServer.API;
using DevkitServer.Configuration;
using DevkitServer.Multiplayer;
using DevkitServer.Multiplayer.Networking;
using DevkitServer.Patches;
using DevkitServer.Plugins;
using JetBrains.Annotations;
using SDG.Framework.Modules;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using Module = SDG.Framework.Modules.Module;
#if CLIENT
using DevkitServer.Players;
using DevkitServer.Players.UI;
#endif

namespace DevkitServer;

public sealed class DevkitServerModule : IModuleNexus
{
    public static readonly string RepositoryUrl = "https://github.com/DanielWillett/DevkitServer"; // don't end with '/'
    private static StaticGetter<Assets>? GetAssetsInstance;
    private static InstanceSetter<AssetOrigin, bool>? SetOverrideIDs;
    public const string ModuleName = "DevkitServer";
    public static readonly string ServerRule = "DevkitServer";
    private static CancellationTokenSource? _tknSrc;
    private static string? _helpCache;
    internal static readonly Color PluginColor = new Color32(0, 255, 153, 255);
    internal static readonly Color UnturnedColor = new Color32(99, 123, 99, 255);
    public Assembly Assembly { get; } = Assembly.GetExecutingAssembly();
    internal static string HelpCache => _helpCache ??= CommandLocalization.format("Help");
    public static GameObject GameObjectHost { get; private set; } = null!;
    public static DevkitServerModuleComponent ComponentHost { get; private set; } = null!;
    public static DevkitServerModule Instance { get; private set; } = null!;
    public static bool IsEditing { get; internal set; }
    public static bool LoadFaulted { get; private set; }
    public static LevelInfo? PendingLevelInfo { get; internal set; }
    public static bool HasLoadedBundle { get; private set; }
    public static MasterBundle? Bundle { get; private set; }
    public static MasterBundleConfig? BundleConfig { get; private set; }
    public static bool IsMainThread => ThreadUtil.gameThread == Thread.CurrentThread;
    public static CancellationToken UnloadToken => _tknSrc == null ? CancellationToken.None : _tknSrc.Token;
    public static Local MainLocalization { get; private set; } = null!;
    public static Local CommandLocalization { get; private set; } = null!;
    public static Local MessageLocalization { get; private set; } = null!;
    public static CultureInfo CommandParseLocale { get; set; } = CultureInfo.InvariantCulture;
    public static AssetOrigin BundleOrigin { get; }
    static DevkitServerModule()
    {
        BundleOrigin = new AssetOrigin { name = ModuleName, workshopFileId = 0ul };
        SetOverrideIDs?.Invoke(BundleOrigin, true);
    }
    public static void EarlyInitialize()
    {
        LoadFaulted = false;
        GameObjectHost = new GameObject(ModuleName);
        ComponentHost = GameObjectHost.AddComponent<DevkitServerModuleComponent>();
        Provider.gameMode = new DevkitServerGamemode();
        Object.DontDestroyOnLoad(GameObjectHost);

        Logger.InitLogger();
        Logger.LogInfo($"{ModuleName.Colorize(PluginColor)} by {"BlazingFlame#0001".Colorize(new Color32(86, 98, 246, 255))} ({"https://github.com/DanielWillett".Format(false)}) initialized.");
        Logger.LogInfo($"Please create an Issue for any bugs at {(RepositoryUrl + "/issues").Format(false)} (one bug per issue please).");
        Logger.LogInfo($"Please give suggestions as a Discussion at {(RepositoryUrl + "/discussions/categories/ideas").Format(false)}.");
        PatchesMain.Init();
#if CLIENT
        Logger.PostPatcherSetupInitLogger();
#endif
        GetAssetsInstance = Accessor.GenerateStaticGetter<Assets, Assets>("instance");
        SetOverrideIDs = Accessor.GenerateInstanceSetter<AssetOrigin, bool>("shouldAssetsOverrideExistingIds");
    }

    public void initialize()
    {
        Stopwatch watch = Stopwatch.StartNew();
        try
        {
            Instance = this;
            ReloadMainLocalization();
            ReloadCommandsLocalization();
            ReloadMessagesLocalization();
            PluginAdvertising.Get().AddPlugin(MainLocalization.format("Name"));
            _tknSrc = new CancellationTokenSource();
            Logger.LogInfo("DevkitServer loading...");
            if (!BitConverter.IsLittleEndian)
            {
                Logger.LogWarning("Your machine is big-endian, you may face issues with improper data transmission, " +
                                  "please report it as I am unable to test with these conditioins.");
            }

            DevkitServerConfig.Reload();

            if (LoadFaulted)
            {
                Provider.shutdown(1, "Failed to load config.");
                return;
            }

            if (!NetFactory.Init())
            {
                Fault();
                Logger.LogError(
                    "Failed to load! Loading cancelled. Check for updates on https://github.com/DanielWillett/DevkitServer.");
                return;
            }

            foreach (Type type in Assembly.GetExecutingAssembly()
                         .GetTypes()
                         .Select(x => new KeyValuePair<Type, EarlyTypeInitAttribute?>(x,
                             (EarlyTypeInitAttribute?)Attribute.GetCustomAttribute(x, typeof(EarlyTypeInitAttribute))))
                         .Where(x => x.Value != null)
                         .OrderByDescending(x => x.Value!.Priority)
                         .ThenBy(x => x.Key.Name)
                         .Select(x => x.Key))
            {
                try
                {
                    RuntimeHelpers.RunClassConstructor(type.TypeHandle);
                    Logger.LogDebug("Initialized static module \"" + type.Name + "\".");
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error while initializing static module \"" + type.Name + "\".");
                    Logger.LogError(ex);
                    Fault();
                    break;
                }
            }

            if (LoadFaulted)
                return;
            CreateDirectoryAttribute.CreateInAssembly(Assembly, true);

            Level.onPostLevelLoaded += OnPostLevelLoaded;
            Editor.onEditorCreated += OnEditorCreated;
#if SERVER
            Provider.onServerConnected += UserManager.AddPlayer;
            Provider.onServerDisconnected += UserManager.RemovePlayer;
            Level.onLevelLoaded += OnLevelLoaded;
#if USERINPUT_DEBUG
            Players.UserInput.OnUserPositionUpdated += OnUserPositionUpdated;
#endif
#else
            Provider.onClientConnected += EditorUser.OnClientConnected;
            Provider.onEnemyConnected += EditorUser.OnEnemyConnected;
            Provider.onClientDisconnected += EditorUser.OnClientDisconnected;
            Provider.onEnemyDisconnected += EditorUser.OnEnemyDisconnected;
            ChatManager.onChatMessageReceived += OnChatMessageReceived;
            UserTPVControl.Init();
            UIAccessTools.EditorUIReady += EditorUIReady;
#endif
            PluginLoader.Load();
        }
        catch (Exception ex)
        {
            Logger.LogError("Error loading...");
            Logger.LogError(ex);
            Fault();
        }
        finally
        {
            watch.Stop();
            Logger.LogInfo($"{ModuleName} initializer took {watch.ElapsedMilliseconds} ms.");
        }

        if (LoadFaulted)
        {
            try
            {
                Module module = ModuleHook.getModuleByName(ModuleName);
                if (module != null)
                    module.isEnabled = false;
            }
            catch (Exception ex)
            {
                Logger.LogError("Error unloading...");
                Logger.LogError(ex);
            }

        }
    }

#if CLIENT
    private void EditorUIReady()
    {
        DevkitEditorHUD.Open();
    }

    private static void OnChatMessageReceived()
    {
        if (ChatManager.receivedChatHistory.Count > 0)
        {
            ReceivedChatMessage msg = ChatManager.receivedChatHistory[0];
            Logger.CoreLog("[" + (msg.speaker?.playerID?.characterName ?? "SERVER") + " | " + msg.mode.ToString().ToUpperInvariant() + "] "
                           + DevkitServerUtility.RemoveRichText(msg.contents),
                "UNTURNED", "CHAT", color: ConsoleColor.White, Severity.Info);
        }
    }
#endif

    private void OnPostLevelLoaded(int level)
    {
        if (IsEditing && level == Level.BUILD_INDEX_GAME)
        {
#if TILE_SYNC
            GameObjectHost.AddComponent<TileSync>();
#endif
        }
        else if (GameObjectHost.TryGetComponent(out TileSync sync))
            Object.Destroy(sync);
    }
    internal void UnloadBundle()
    {
        if (!HasLoadedBundle || Bundle == null)
        {
            Bundle = null;
            HasLoadedBundle = false;
            return;
        }

        try
        {
            Bundle.unload();
        }
        catch (Exception ex)
        {
            Logger.LogError("Error unloading " + "devkitserver.masterbundle".Format() + " bundle.");
            Logger.LogError(ex);
        }
        Bundle = null;
        HasLoadedBundle = false;
    }
    internal IEnumerator TryLoadBundle(System.Action? callback)
    {
        ThreadUtil.assertIsGameThread();
        if (HasLoadedBundle)
        {
            callback?.Invoke();
            yield break;
        }

        while (Assets.isLoading)
            yield return null;
        
        string path = Path.Combine(ReadWrite.PATH, "Modules", "DevkitServer", "Bundles");
        if (!Directory.Exists(path))
        {
            Logger.LogError("Failed to find DevkitServer bundle folder: " + path.Format() + ".");
            callback?.Invoke();
            yield break;
        }
        MasterBundleConfig? bundle = BundleConfig ?? Assets.findMasterBundleByPath(path);
        if (bundle == null || bundle.assetBundle == null)
        {
            Logger.LogDebug($"Adding DevkitServer Bundle Search Location: {path.Format(false)}.");
            Assets.RequestAddSearchLocation(path, BundleOrigin);
            yield return null;
            yield return new WaitForEndOfFrame();
            while (Assets.isLoading)
            {
                yield return null;
            }

            bundle = Assets.findMasterBundleByPath(path);
            if (bundle == null)
            {
#if DEBUG
                foreach (MasterBundleConfig config in (IEnumerable<MasterBundleConfig>?)typeof(Assets).GetField("allMasterBundles", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null) ?? Array.Empty<MasterBundleConfig>())
                    Logger.LogDebug("Bundle: " + config.assetBundleName.Format() + " (" + config.directoryPath.Format() + ").");
#endif
                Logger.LogError("Failed to find DevkitServer bundle: " + Path.Combine(path, "devkitserver.masterbundle").Format() + ".");
                callback?.Invoke();
                yield break;
            }
        }

        if (Bundle != null)
        {
            Bundle.unload();
            Bundle = null;
        }
        Bundle = new MasterBundle(bundle, string.Empty, "DevkitServer Base");
        BundleConfig = bundle;
        Logger.LogInfo("Loaded bundle: " + bundle.assetBundleNameWithoutExtension.Format() + " from " + path.Format() + ".");
        HasLoadedBundle = true;
#if false
        Logger.LogDebug("Assets:");
        foreach (string asset in Bundle.cfg.assetBundle.GetAllAssetNames())
            Logger.LogDebug("Asset: " + asset.Format() + ".");
#endif
        callback?.Invoke();
    }

    internal static void Fault()
    {
        if (!LoadFaulted)
        {
            LoadFaulted = true;
            Logger.LogWarning("DevkitServer terminated.");
            Assets? instance = GetAssetsInstance?.Invoke();
            if (instance != null)
                instance.StopAllCoroutines();
            Provider.shutdown(10, "DevkitServer failed to load.");
        }
    }

    private static void OnEditorCreated()
    {
        Logger.LogInfo("Editor loaded.");
#if SERVER
        Provider.modeConfigData.Gameplay.Group_Map = true;
        Provider.modeConfigData.Gameplay.Chart = true;
        Provider.modeConfigData.Gameplay.Satellite = true;
        Provider.modeConfigData.Gameplay.Compass = true;
        Provider.modeConfigData.Gameplay.Timer_Exit = 0;
        Provider.modeConfigData.Gameplay.Timer_Home = 0;
        Provider.modeConfigData.Gameplay.Timer_Home = 0;
        Assembly sdg = typeof(Provider).Assembly;
        Component comp = Level.editing.GetComponentInChildren(sdg.GetType("SDG.Unturned.EditorInteract"));
        if (comp != null)
            Object.Destroy(comp);
        if (comp != null)
            comp = Level.editing.GetComponentInChildren(typeof(EditorMovement));
        Object.Destroy(comp);
        if (comp != null)
            comp = Level.editing.GetComponentInChildren(typeof(EditorLook));
        Object.Destroy(comp);
        if (comp != null)
            comp = Level.editing.GetComponentInChildren(typeof(EditorArea));
        if (comp != null)
            Object.Destroy(comp);
#endif
    }
#if SERVER
    private void OnLevelLoaded(int level)
    {
        Logger.LogInfo("Level loaded: " + level + ".");
        if (level != Level.BUILD_INDEX_GAME)
            return;
        if (DevkitServerConfig.Config.TcpSettings is { EnableHighSpeedSupport: true })
            _ = HighSpeedServer.Instance;
    }
#endif

    public void shutdown()
    {
        PluginLoader.Unload();
        _tknSrc?.Cancel();
        _tknSrc = null;
        Object.Destroy(TileSync.ServersideAuthorityTileSync);
        Object.Destroy(GameObjectHost);
        Logger.LogInfo("Shutting down...");
        PatchesMain.Unpatch();
        UnloadBundle();
        Editor.onEditorCreated -= OnEditorCreated;
        Level.onPostLevelLoaded -= OnPostLevelLoaded;
#if SERVER
        Provider.onServerConnected -= UserManager.AddPlayer;
        Provider.onServerDisconnected -= UserManager.RemovePlayer;
        Level.onLevelLoaded -= OnLevelLoaded;
#if USERINPUT_DEBUG
        Players.UserInput.OnUserPositionUpdated -= OnUserPositionUpdated;
#endif
        HighSpeedServer.Deinit();
#else
        Provider.onClientConnected -= EditorUser.OnClientConnected;
        Provider.onEnemyConnected -= EditorUser.OnEnemyConnected;
        Provider.onClientDisconnected -= EditorUser.OnClientDisconnected;
        Provider.onEnemyDisconnected -= EditorUser.OnEnemyDisconnected;
        ChatManager.onChatMessageReceived -= OnChatMessageReceived;
        UserTPVControl.Deinit();
#endif

        Instance = null!;
        GameObjectHost = null!;
        LoadFaulted = false;
        Logger.CloseLogger();
    }

#if USERINPUT_DEBUG
    private static EffectAsset? _debugEffectAsset;
    private static void OnUserPositionUpdated(Players.EditorUser obj)
    {
        _debugEffectAsset ??= Assets.find<EffectAsset>(new Guid("5e2a0073025849d39322932d88609777"));
        if (_debugEffectAsset != null && obj.Input != null)
        {
            TriggerEffectParameters p = new TriggerEffectParameters(_debugEffectAsset)
            {
                position = obj.Input.transform.position,
                direction = obj.Input.transform.forward,
                relevantDistance = Level.size
            };
            EffectManager.triggerEffect(p);
        }
    }
#endif

    [ModuleInitializer]
    public static void ModuleInitializer()
    {
        EarlyInitialize();
    }
#if CLIENT
    internal static void RegisterDisconnectFromEditingServer()
    {
        IsEditing = false;
        Logger.LogInfo("No longer connected to a DevkitServer host.");
    }
#endif
    public static void ReloadMainLocalization()
    {
        string path = Path.Combine(DevkitServerConfig.LocalizationFilePath, "Main");
        Local lcl = Localization.tryRead(path, false);
        DevkitServerUtility.UpdateLocalizationFile(ref lcl, DefaultMainLocalization, path);
        MainLocalization = lcl;
    }
    public static void ReloadCommandsLocalization()
    {
        string path = Path.Combine(DevkitServerConfig.LocalizationFilePath, "Commands");
        Local lcl = Localization.tryRead(path, false);
        DevkitServerUtility.UpdateLocalizationFile(ref lcl, DefaultCommandLocalization, path);
        CommandLocalization = lcl;
    }
    public static void ReloadMessagesLocalization()
    {
        string path = Path.Combine(DevkitServerConfig.LocalizationFilePath, "Messages");
        Local lcl = Localization.tryRead(path, false);
        DevkitServerUtility.UpdateLocalizationFile(ref lcl, DefaultMessageLocalization, path);
        MessageLocalization = lcl;
    }
    private static readonly LocalDatDictionary DefaultMainLocalization = new LocalDatDictionary
    {
        { "Name", "Devkit Server" },
        { "Help", "help" }
    };
    private static readonly LocalDatDictionary DefaultCommandLocalization = new LocalDatDictionary
    {
        { "CorrectUsage", "<#ff8c69>Correct usage: {0}." },
        { "UnknownCommand", "<#ff8c69>Unknown command. <#b3ffb3>Type <#fff>/help</color> to learn more." },
        { "ConsoleOnly", "<#ff8c69>This command can only be called from console." },
        { "PlayersOnly", "<#ff8c69>This command can not be called from console." },
        { "Exception", "<#ff8c69>Error executing command: <#4ec9b0>{0}</color>." },
        { "NoPermissions", "<#ff8c69>You do not have permission to use this command." }
    };
    private static readonly LocalDatDictionary DefaultMessageLocalization = new LocalDatDictionary
    {
        { "NoPermissions", "No Permission" },
        { "BeingSynced", "Syncing" },
        { "FeatureDisabled", "Disabled" },
        { "UndoNotSupported", "Undo Not Supported" },
        { "RedoNotSupported", "Redo Not Supported" },
    };
}

public sealed class DevkitServerModuleComponent : MonoBehaviour
{
    internal static ConcurrentQueue<MainThreadTask.MainThreadResult> ThreadActionRequests = new ConcurrentQueue<MainThreadTask.MainThreadResult>();
    [UsedImplicitly]
    private void Update()
    {
        while (ThreadActionRequests.TryDequeue(out MainThreadTask.MainThreadResult res))
        {
            try
            {
                res.Task.Token.ThrowIfCancellationRequested();
                res.Continuation?.Invoke();
            }
            catch (OperationCanceledException) { Logger.LogDebug("Execution on update cancelled."); }
            catch (Exception ex)
            {
                Logger.LogError("Error executing main thread operation.");
                Logger.LogError(ex);
            }
            finally
            {
                res?.Complete();
            }
        }
    }
}