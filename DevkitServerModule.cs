#if CLIENT
#define TILE_DEBUG_GL
#endif
#define TILE_SYNC

using DevkitServer.API;
using DevkitServer.Configuration;
using DevkitServer.Multiplayer;
using DevkitServer.Multiplayer.Networking;
using DevkitServer.Multiplayer.Sync;
using DevkitServer.Patches;
using DevkitServer.Plugins;
using SDG.Framework.Modules;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security;
using Cysharp.Threading.Tasks;
using DevkitServer.Multiplayer.Actions;
using DevkitServer.Multiplayer.Levels;
using UnityEngine.SceneManagement;
using Module = SDG.Framework.Modules.Module;
#if CLIENT
#if DEBUG
using DevkitServer.Util.Debugging;
#endif
using DevkitServer.Players;
using DevkitServer.Players.UI;
#endif
#if SERVER
using DevkitServer.Levels;
#endif

namespace DevkitServer;

public sealed class DevkitServerModule : IModuleNexus
{
    public static readonly string RepositoryUrl = "https://github.com/DanielWillett/DevkitServer"; // don't suffix this with '/'
    private static StaticGetter<Assets>? GetAssetsInstance;
    private static InstanceSetter<AssetOrigin, bool>? SetOverrideIDs;
    public const string ModuleName = "DevkitServer";
    public static readonly string ServerRule = "DevkitServer";
    private static CancellationTokenSource? _tknSrc;
    internal static readonly Color ModuleColor = new Color32(0, 255, 153, 255);
    internal static readonly Color UnturnedColor = new Color32(99, 123, 99, 255);
    private static string? _asmPath;
    private static IReadOnlyList<string>? _searchLocations;
    public Assembly Assembly { get; } = Assembly.GetExecutingAssembly();
    internal static string HelpMessage => CommandLocalization.format("Help");
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
#if SERVER
    public static BackupManager? BackupManager { get; private set; }
#endif
    public static bool MonoLoaded { get; }
    public static bool UnityLoaded { get; }
    public static bool UnturnedLoaded { get; }
    public static bool InitializedLogging { get; set; }
    public static string AssemblyPath => _asmPath ??= Accessor.DevkitServer.Location;
    public static IReadOnlyList<string> AssemblyFileSearchLocations
    {
        get
        {
            if (_searchLocations != null)
                return _searchLocations;
            LoadSearchLocations();
            return _searchLocations!;
        }
    }

    private static readonly LocalDatDictionary DefaultMainLocalization = new LocalDatDictionary
    {
        { "Name", "Devkit Server" },
        { "Help", "help" },
        { "No_Asset_Selected", "No Asset Selected" }
    };
    public static Local CommandLocalization { get; private set; } = null!;

    private static readonly LocalDatDictionary DefaultCommandLocalization = new LocalDatDictionary
    {
        { "CorrectUsage", "<#ff8c69>Correct usage: {0}." },
        { "UnknownCommand", "<#ff8c69>Unknown command. <#b3ffb3>Type <#fff>/help</color> to learn more." },
        { "ConsoleOnly", "<#ff8c69>This command can only be called from console." },
        { "PlayersOnly", "<#ff8c69>This command can not be called from console." },
        { "Exception", "<#ff8c69>Error executing command: <#4ec9b0>{0}</color>." },
        { "NoPermissions", "<#ff8c69>You do not have permission to use this command." }
    };
    public static Local MessageLocalization { get; private set; } = null!;

    private static readonly LocalDatDictionary DefaultMessageLocalization = new LocalDatDictionary
    {
        { "NoPermissions", "No Permission" },
        { "NoPermissionsWithPermission", "Missing Permission\n{0}" },
        { "Error", "Error\n{0}" },
        { "UnknownError", "Unknown Error" },
        { "Syncing", "Syncing" },
        { "FeatureDisabled", "Disabled" },
        { "UndoNotSupported", "Undo Not Supported" },
        { "RedoNotSupported", "Redo Not Supported" },
        { "TooManySelections", "Too many items selected.\n{0}/{1}"}
    };

    public static CultureInfo CommandParseLocale { get; set; } = CultureInfo.InvariantCulture;
    public static AssetOrigin BundleOrigin { get; }
    static DevkitServerModule()
    {
        BundleOrigin = new AssetOrigin { name = ModuleName, workshopFileId = 0ul };
        SetOverrideIDs?.Invoke(BundleOrigin, true);

        MonoLoaded = Type.GetType("Mono.Runtime", false, false) != null;
        if (!MonoLoaded)
        {
            LoadFaulted = true;
            UnityLoaded = false;
            UnturnedLoaded = false;
            return;
        }
        try
        {
            _ = SceneManager.GetActiveScene();
            UnityLoaded = true;
        }
        catch (SecurityException)
        {
            UnityLoaded = false;
            UnturnedLoaded = false;
            LoadFaulted = true;
            return;
        }
        if (Type.GetType("SDG.Unturned.Provider, Assembly-CSharp", false, false) == null)
        {
            UnturnedLoaded = false;
            LoadFaulted = true;
            return;
        }

        UnturnedLoaded = true;
    }
    public void initialize()
    {
        Stopwatch watch = Stopwatch.StartNew();
        InitializedLogging = false;
        if (LoadFaulted)
            goto fault;
        try
        {
            // Initialize UniTask
            if (!PlayerLoopHelper.HasBeenInitialized)
                PlayerLoopHelper.Init();

            GameObjectHost = new GameObject(ModuleName);
            ComponentHost = GameObjectHost.AddComponent<DevkitServerModuleComponent>();
            GameObjectHost.AddComponent<CachedTime>();
            GameObjectHost.hideFlags = HideFlags.DontSave;
            Provider.gameMode = new DevkitServerGamemode();
            Object.DontDestroyOnLoad(GameObjectHost);
#if CLIENT
            GameObject objectItemGeneratorHost = new GameObject("ObjectIconGenerator", typeof(Light), typeof(IconGenerator), typeof(Camera));
            objectItemGeneratorHost.transform.SetParent(GameObjectHost.transform, true);
            objectItemGeneratorHost.hideFlags = HideFlags.DontSave;
            Object.DontDestroyOnLoad(objectItemGeneratorHost);
#endif

            Logger.InitLogger();
            InitializedLogging = true;
            PatchesMain.Init();
#if CLIENT
            Logger.PostPatcherSetupInitLogger();
#endif
            GetAssetsInstance = Accessor.GenerateStaticGetter<Assets, Assets>("instance");
            SetOverrideIDs = Accessor.GenerateInstanceSetter<AssetOrigin, bool>("shouldAssetsOverrideExistingIds");
        }
        catch (Exception ex)
        {
            if (InitializedLogging)
            {
                Logger.LogError($"Error setting up {ModuleName.Colorize(ModuleColor)}");
                Logger.LogError(ex);
            }
            else
            {
                CommandWindow.LogError($"Error setting up {ModuleName}.");
                CommandWindow.LogError(ex);
            }
            Fault();
            goto fault;
        }
        try
        {
            Instance = this;
            ReloadMainLocalization();
            ReloadCommandsLocalization();
            ReloadMessagesLocalization();
            PluginAdvertising.Get().AddPlugin(MainLocalization.format("Name"));
            _tknSrc = new CancellationTokenSource();
            Logger.LogInfo("DevkitServer loading...");

            DevkitServerConfig.Reload();

            if (LoadFaulted)
            {
                Provider.shutdown(1, "Failed to load config.");
                goto fault;
            }

            if (!NetFactory.Init())
            {
                Fault();
                Logger.LogError("Failed to load! Loading cancelled. Check for updates on https://github.com/DanielWillett/DevkitServer.");
                goto fault;
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
                    CreateDirectoryAttribute.CreateInType(type, true);
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
                goto fault;

            CreateDirectoryAttribute.CreateInAssembly(Assembly, true);
#if CLIENT
            UIExtensionManager.Reflect(Accessor.DevkitServer);
#endif
            if (LoadFaulted)
                goto fault;

            Level.onPostLevelLoaded += OnPostLevelLoaded;
            Level.onLevelLoaded += OnLevelLoaded;
            Editor.onEditorCreated += OnEditorCreated;
            Level.onPrePreLevelLoaded += OnPrePreLevelLoaded;
            SaveManager.onPreSave += OnPreSaved;
            SaveManager.onPostSave += OnSaved;
#if SERVER
            Provider.onServerConnected += UserManager.AddUser;
            Provider.onEnemyConnected += UserManager.OnAccepted;
            Provider.onServerDisconnected += UserManager.RemoveUser;
#if USERINPUT_DEBUG
            Players.UserInput.OnUserPositionUpdated += OnUserPositionUpdated;
#endif
#else
#if DEBUG
            GameObjectHost.AddComponent<RegionDebug>();
#endif
            DevkitServerGLUtility.Init();
            Provider.onClientConnected += EditorUser.OnClientConnected;
            Provider.onEnemyConnected += EditorUser.OnEnemyConnected;
            Provider.onClientDisconnected += EditorUser.OnClientDisconnected;
            Provider.onEnemyDisconnected += EditorUser.OnEnemyDisconnected;
            ChatManager.onChatMessageReceived += OnChatMessageReceived;
            UserTPVControl.Init();
#endif
            LevelObjectNetIdDatabase.Init();
            HierarchyItemNetIdDatabase.Init();

            PluginLoader.Load();
            CreateDirectoryAttribute.DisposeLoadList();
        }
        catch (Exception ex)
        {
            Fault();
            Logger.LogError($"Error loading {ModuleName.Colorize(ModuleColor)}");
            Logger.LogError(ex);
        }
        fault:
        watch.Stop();
        if (UnturnedLoaded)
            Logger.LogInfo($"{ModuleName} initializer took {watch.ElapsedMilliseconds} ms.");
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
                if (InitializedLogging)
                {
                    Logger.LogError($"Error unloading {ModuleName.Colorize(ModuleColor)}.");
                    Logger.LogError(ex);
                }
                else
                {
                    CommandWindow.LogError($"Error unloading {ModuleName}.");
                    CommandWindow.LogError(ex);
                }
            }
        }
        else
        {
            Logger.LogInfo($"{ModuleName.Colorize(ModuleColor)} (by @{"blazingflame".Colorize(new Color32(86, 98, 246, 255))} on {"Discord".Colorize(new Color32(116, 131, 196, 255))} or " +
                           $"{"https://github.com/DanielWillett".Format(false)}) initialized.");
            Logger.LogInfo($"Please create an Issue for any bugs at {(RepositoryUrl + "/issues").Format(false)} (one bug per issue please).");
            Logger.LogInfo($"Please give suggestions as a Discussion at {(RepositoryUrl + "/discussions/categories/ideas").Format(false)}.");
        }
        GC.Collect();
    }

#if CLIENT
    private static void OnChatMessageReceived()
    {
        if (ChatManager.receivedChatHistory.Count > 0)
        {
            ReceivedChatMessage msg = ChatManager.receivedChatHistory[0];
            Logger.CoreLog("[" + (msg.speaker?.playerID?.characterName ?? "SERVER") + " | " + msg.mode.ToString().ToUpperInvariant() + "] "
                           + (msg.useRichTextFormatting ? FormattingUtil.ConvertRichTextToANSI(msg.contents) : msg.contents),
                "UNTURNED", "CHAT", color: ConsoleColor.White, Severity.Info);
        }
    }
#endif
    private static void OnLevelLoaded(int level)
    {
#if CLIENT
        if (level == Level.BUILD_INDEX_MENU)
        {
            LoadingUI? loadingUI = UIAccessTools.LoadingUI;
            if (loadingUI == null)
            {
                Logger.LogWarning("Unable to find LoadingUI.");
            }

            string dsPath = DevkitServerConfig.Directory;
            Logger.LogDebug("Clearing temporary folders.");
            foreach (string folder in Directory.EnumerateDirectories(dsPath, "Temp_*", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    Directory.Delete(folder, true);
                    Logger.LogDebug($"Removed temporary folder: {folder.Format()}.");
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Unable to delete temporary folder: {folder.Format()}.");
                    Logger.LogError(ex);
                }
            }
        }
#endif
        if (!IsEditing || level != Level.BUILD_INDEX_GAME)
        {
            if (GameObjectHost.TryGetComponent(out TileSync tileSync))
                Object.Destroy(tileSync);
            if (GameObjectHost.TryGetComponent(out ObjectSync objectSync))
                Object.Destroy(objectSync);
            if (GameObjectHost.TryGetComponent(out HierarchySync hierarchySync))
                Object.Destroy(hierarchySync);
            if (GameObjectHost.TryGetComponent(out EditorActions actions))
                Object.Destroy(actions);
        }
        Logger.LogInfo("Level loaded: " + level + ".");
        if (level == Level.BUILD_INDEX_GAME)
        {
#if SERVER
            if (DevkitServerConfig.Config.TcpSettings is { EnableHighSpeedSupport: true })
                _ = HighSpeedServer.Instance;
#endif
        }
    }
    private static void OnPostLevelLoaded(int level)
    {
        if (IsEditing && level == Level.BUILD_INDEX_GAME)
        {
            TileSync.CreateServersideAuthority();
            ObjectSync.CreateServersideAuthority();
            HierarchySync.CreateServersideAuthority();
        }

        if (!BitConverter.IsLittleEndian)
        {
            Logger.LogWarning("Your machine is big-endian, you may face issues with improper data transmission, " +
                              "please report it as I am unable to test with these conditioins.");
        }

        ComponentHost.StartCoroutine(ClearLoggingErrorsDelayed());
#if CLIENT
        EditorLevel.ServerPendingLevelData = null;
        GC.Collect();
#endif
    }
    private static IEnumerator<WaitForSeconds> ClearLoggingErrorsDelayed()
    {
        yield return new WaitForSeconds(2);
        Logger.ClearLoadingErrors();
        Logger.LogDebug($"DevkitServer managed memory usage: {DevkitServerUtility.FormatBytes(GC.GetTotalMemory(true)).Format(false)}");
    }
    private static void LoadSearchLocations()
    {
        string asmDir = Path.GetDirectoryName(AssemblyPath)!;
        List<string> locs = new List<string>
        {
            Path.GetFullPath(asmDir)
        };
        string dir = Path.GetFullPath(Path.Combine(asmDir, ".."));
        if (!Path.GetFileName(dir).Equals("Modules", StringComparison.OrdinalIgnoreCase))
        {
            locs.Add(dir);
            string dir2 = Path.GetFullPath(Path.Combine(asmDir, "..", ".."));
            if (!Path.GetFileName(dir2).Equals("Modules", StringComparison.OrdinalIgnoreCase))
            {
                locs.Add(dir2);
                foreach (string directory in Directory.EnumerateDirectories(dir2))
                {
                    dir2 = Path.GetFullPath(directory);
                    if (!locs.Contains(dir2))
                        locs.Add(dir2);
                }
            }

            foreach (string directory in Directory.EnumerateDirectories(dir))
            {
                dir = Path.GetFullPath(directory);
                if (!locs.Contains(dir))
                    locs.Add(dir);
            }
        }

#if DEBUG
        foreach (string directory in locs)
        {
            if (InitializedLogging)
                Logger.LogDebug($"Added module file search location: {directory.Format(true)}.");
            else
                CommandWindow.Log($"Added module file search location: \"{directory}\".");
        }
#endif

        _searchLocations = new ReadOnlyCollection<string>(locs.ToArray());
    }
    public static string? FindModuleFile(string name)
    {
        foreach (string location in AssemblyFileSearchLocations)
        {
            string path = Path.Combine(location, name);
            if (File.Exists(path))
                return path;
        }

        return null;
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

        while (Assets.isLoading)
            yield return null;
        string asmDir = Path.GetDirectoryName(AssemblyPath)!;
        string[] searchLocations =
        {
            asmDir,
            Path.Combine(asmDir, "..", "Bundles"),
            Path.Combine(asmDir, "Bundles"),
            Path.Combine(asmDir, "..", "..", "Bundles")
        };
        string? path = null;
        for (int i = 0; i < searchLocations.Length; ++i)
        {
            string searchLocation = searchLocations[i];
            if (File.Exists(Path.Combine(searchLocation, "MasterBundle.dat")) &&
                File.Exists(Path.Combine(searchLocation, "devkitserver.masterbundle")))
            {
                path = Path.GetFullPath(searchLocation);
                break;
            }
        }
        if (path == null)
        {
            Logger.LogError("Failed to find DevkitServer bundle folder near " + asmDir.Format() + ".");
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
            Logger.ClearLoadingErrors();
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
        Provider.hasCheats = true;
        Assembly sdg = Accessor.AssemblyCSharp;
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
    private static void OnPreSaved()
    {
        Logger.LogInfo("Saving...");
        if (IsEditing)
        {
            Thread.BeginCriticalRegion();
            try
            {
                Level.save();

                HierarchyResponsibilities.Save();
                LevelObjectResponsibilities.Save();
                BuildableResponsibilities.Save();
            }
            finally
            {
                Thread.EndCriticalRegion();
            }
        }
    }
    private static void OnSaved()
    {
        Logger.LogInfo("Saved");
    }
    private static void OnPrePreLevelLoaded(int level)
    {
#if SERVER
        DevkitServerUtility.CheckDirectory(false, false, DevkitServerConfig.LevelDirectory, typeof(DevkitServerConfig).GetProperty(nameof(DevkitServerConfig.LevelDirectory), BindingFlags.Public | BindingFlags.Static));
        BackupManager = GameObjectHost.AddComponent<BackupManager>();

        LevelObjectNetIdDatabase.AssignExisting();
        HierarchyItemNetIdDatabase.AssignExisting();
#endif
        if (IsEditing)
        {
            BuildableResponsibilities.Init();
            HierarchyResponsibilities.Init();
            LevelObjectResponsibilities.Init();
            if (GameObjectHost.TryGetComponent(out EditorActions actions))
                Object.Destroy(actions);
            EditorActions.ServerActions = GameObjectHost.AddComponent<EditorActions>();
#if SERVER
            EditorActions.ServerActions.IsOwner = true;
#endif
        }
        CartographyUtil.Reset();
#if CLIENT
        if (IsEditing)
        {
            LevelObjectNetIdDatabase.LoadFromLevelData();
            HierarchyItemNetIdDatabase.LoadFromLevelData();
        }
#endif
    }

    public void shutdown()
    {
        /* SAVE DATA */
        OnSaved();

        PluginLoader.Unload();
        _tknSrc?.Cancel();
        _tknSrc = null;
        TileSync.DestroyServersideAuthority();
#if SERVER
        Object.Destroy(BackupManager);
        BackupManager = null;
#endif
        Object.Destroy(GameObjectHost);
        Logger.LogInfo("Shutting down...");
        PatchesMain.Unpatch();
        UnloadBundle();
        Editor.onEditorCreated -= OnEditorCreated;
        Level.onPostLevelLoaded -= OnPostLevelLoaded;
        Level.onPrePreLevelLoaded -= OnPrePreLevelLoaded;
        SaveManager.onPostSave -= OnSaved;
#if SERVER
        Provider.onServerConnected -= UserManager.AddUser;
        Provider.onEnemyConnected -= UserManager.OnAccepted;
        Provider.onServerDisconnected -= UserManager.RemoveUser;
        Level.onLevelLoaded -= OnLevelLoaded;
        HighSpeedServer.Deinit();
#else
        DevkitServerGLUtility.Shutdown();
        Provider.onClientConnected -= EditorUser.OnClientConnected;
        Provider.onEnemyConnected -= EditorUser.OnEnemyConnected;
        Provider.onClientDisconnected -= EditorUser.OnClientDisconnected;
        Provider.onEnemyDisconnected -= EditorUser.OnEnemyDisconnected;
        ChatManager.onChatMessageReceived -= OnChatMessageReceived;
        UserTPVControl.Deinit();
#endif
        LevelObjectNetIdDatabase.Shutdown();
        HierarchyItemNetIdDatabase.Shutdown();

        Instance = null!;
        GameObjectHost = null!;
        LoadFaulted = false;
        Logger.CloseLogger();
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
}

public sealed class DevkitServerModuleComponent : MonoBehaviour
{
    internal static ConcurrentQueue<MainThreadTask.MainThreadResult> ThreadActionRequests = new ConcurrentQueue<MainThreadTask.MainThreadResult>();
    private static int _ticks;
    public static int Ticks => _ticks;

    [UsedImplicitly]
    private void Update()
    {
        ++_ticks;
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

    [UsedImplicitly]
    private void Start()
    {
        _ticks = 0;
    }
}