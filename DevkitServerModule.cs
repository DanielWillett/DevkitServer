#if CLIENT
#define TILE_DEBUG_GL
#endif
#define TILE_SYNC
using Cysharp.Threading.Tasks;
using DanielWillett.ReflectionTools;
using DevkitServer.API;
using DevkitServer.API.Cartography;
using DevkitServer.Configuration;
using DevkitServer.Core;
using DevkitServer.Framework;
using DevkitServer.Levels;
using DevkitServer.Multiplayer;
using DevkitServer.Multiplayer.Actions;
using DevkitServer.Multiplayer.Levels;
using DevkitServer.Multiplayer.Networking;
using DevkitServer.Multiplayer.Sync;
using DevkitServer.Patches;
using DevkitServer.Plugins;
using DevkitServer.Util.Encoding;
using SDG.Framework.Modules;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security;
using UnityEngine.SceneManagement;
using Module = SDG.Framework.Modules.Module;
using Version = System.Version;
#if CLIENT
using DevkitServer.API.UI;
using DevkitServer.API.UI.Icons;
using DevkitServer.API.UI.Extensions;
using DevkitServer.Core.Tools;
using DevkitServer.Multiplayer.Movement;
using DevkitServer.Players;
using SDG.Framework.Utilities;
#if DEBUG
using DevkitServer.Util.Debugging;
#endif
#endif
#if SERVER
using DevkitServer.Multiplayer.Cryptography;
using DevkitServer.Multiplayer.Movement;
using HighlightingSystem;
#endif

namespace DevkitServer;

public sealed class DevkitServerModule : IModuleNexus
{
#if DEBUG
    public static readonly bool IsDebug = true;
#else
    public static readonly bool IsDebug = false;
#endif
    public static readonly bool IsRelease = !IsDebug;

    public static readonly string RepositoryUrl = "https://github.com/DanielWillett/DevkitServer"; // don't suffix these with '/'
    public static readonly string RawRepositoryUrl = "https://raw.githubusercontent.com/DanielWillett/DevkitServer";
    public const string ModuleName = "DevkitServer";
    public static readonly string ServerRule = "DevkitServer";
    internal static readonly Color32 ModuleColor = new Color32(189, 167, 134, 255);
    internal static readonly Color32 UnturnedColor = new Color32(99, 123, 99, 255);

    private static CancellationTokenSource? _tknSrc;
    private static string? _asmPath;
    private static IReadOnlyList<string>? _searchLocations;
    private static string? _commitIdShort;
    internal static AssemblyResolver AssemblyResolver = null!;
    private static int? _mainThreadId;
    public static string ColorizedModuleName => ModuleName.Colorize(ModuleColor);

    internal static NetCall ClientAskSave = new NetCall(DevkitServerNetCall.AskSave);
    public static string CommitId => _commitIdShort ??= DevkitServer.CommitId.Commit.Length > 7 ? DevkitServer.CommitId.Commit.Substring(0, 7) : DevkitServer.CommitId.Commit;
    public static string LongCommitId => DevkitServer.CommitId.Commit;
    public Assembly Assembly { get; } = Assembly.GetExecutingAssembly();
    internal static string HelpMessage => CommandLocalization.format("Help");
    public static GameObject GameObjectHost { get; private set; } = null!;
    public static DevkitServerModuleComponent ComponentHost { get; private set; } = null!;
    public static DevkitServerModule Instance { get; private set; } = null!;
    public static bool IsEditing { get; internal set; }
    public static bool LoadFaulted { get; private set; }
    public static bool HasLoadedBundle { get; private set; }
    public static MasterBundle? Bundle { get; private set; }
    public static MasterBundleConfig? BundleConfig { get; private set; }
    public static Module Module { get; private set; } = null!;
    public static bool IsMainThread
    {
        get
        {
            _mainThreadId ??= ThreadUtil.gameThread.ManagedThreadId;
            return Environment.CurrentManagedThreadId == _mainThreadId;
        }
    }

    public static CancellationToken UnloadToken => _tknSrc?.Token ?? CancellationToken.None;
    public static Local MainLocalization { get; private set; } = null!;
    public static BackupManager? BackupManager { get; private set; }
    public static bool MonoLoaded { get; }
    public static bool UnityLoaded { get; }
    public static bool UnturnedLoaded { get; }
    public static bool DevkitServerLoaded => Module != null;
    public static bool InitializedLogging { get; private set; }
    public static bool InitializedPluginLoader { get; internal set; }
    public static string AssemblyPath => _asmPath ??= AccessorExtensions.DevkitServer.Location;
    public static bool IsAuthorityEditor =>
#if CLIENT
        Level.isEditor && !IsEditing;
#else
        IsEditing;
#endif
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
        { "NoAssetSelected", "No Asset Selected" },
        { "RefreshLevelsButton", "Refresh Levels" },
        { "ObjectIconEditorToggle", "Live Editor" },
        { "ObjectIconEditorSave", "Save" },
        { "ObjectIconEditorSaveNew", "Save New" },
        { "ObjectIconEditorToggleHint", "[{0}] to edit" },
        { "ObjectIconEditorOffsetAssetHint", "Goto offset" },
        { "ObjectIconEditorOffsetAssetButton", "Go" },
        { "VersionKickMessage", "This server is running version {0} of DevkitServer, whereas you are running incompatible version {1}."},
        { "WorkshopSubmitMenuExistingModIdLabel", "Override Mod ID" },
        { "RenderingChartInProgress", "Rendering chart..." },
        { "RenderingSatelliteInProgress", "Rendering map..." },
        { "SaveInProgressWillFreeze", "Saving... (saving again will freeze until done)" },
        { "BackingUpInProgress", "Backing up..." },
        { "BackupAndSaveButton", "Backup And Save" },
        { "TooManyPasswordAttempts", "Too many incorrect password attempts. Try again in {0} second(s)." },
        { "UnknownIPv4AndSteam64", "Unknown IPv4 and Steam64 ID of connecting user - can't join a password-protected server." },
        { "HighSpeedTip", $"Want to download the map faster? Have the server owner set up the <b>high speed</b> server settings in <color=#{
            ModuleColor.r.ToString("X2", CultureInfo.InvariantCulture)
            + ModuleColor.g.ToString("X2", CultureInfo.InvariantCulture)
            + ModuleColor.b.ToString("X2", CultureInfo.InvariantCulture)}>{ModuleName}</color>'s <#dddddd>server_config.json</color>." }
    };
    public static Local CommandLocalization { get; private set; } = null!;

    private static readonly LocalDatDictionary DefaultCommandLocalization = new LocalDatDictionary
    {
        { "CorrectUsage", "<#ff8c69>Correct usage: {0}." },
        { "UnknownCommand", "<#ff8c69>Unknown command. <#b3ffb3>Type <#fff>/help</color> to learn more." },
        { "ConsoleOnly", "<#ff8c69>This command can only be called from console." },
        { "PlayersOnly", "<#ff8c69>This command can not be called from console." },
        { "Exception", "<#ff8c69>Error executing command: <#4ec9b0>{0}</color>." },
        { "NoPermissions", "<#ff8c69>You do not have permission to use this command." },
        { "NotDevkitServerClient", "<#ff8c69>You must be connected to a DevkitServer server." },
        { "CommandDisabled", "<#ff8c69>This command is disabled." },
        { "CommandMustBePlayer", "<#ff8c69>This command can only be ran during play mode." },
        { "CommandMustBeEditorPlayer", "<#ff8c69>This command can only be ran during player control in editor mode." },
        { "CommandMustBeEditor", "<#ff8c69>This command can only be ran during edit mode." },
        { "CommandMustBeMultiplayer", "<#ff8c69>This command can only be ran in multiplayer." },
        { "CommandMustBeSingleplayer", "<#ff8c69>This command can only be ran in singleplayer." },
        { "CommandMustBeMenu", "<#ff8c69>This command can only be ran in menu." },
        { "CommandRequiresCheats", "<#ff8c69>This command can only be ran when cheats are enabled." },
        { "CommandRequiresNotMenu", "<#ff8c69>This command can not be ran in menu." },
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
        { "TooManySelections", "Too many items selected.\n{0}/{1}"},
        { "AlreadyBakingNavigationName", "Already Baking Navigation\nNavigation: {0} (# {1})" },
        { "AlreadyBakingNavigationIndex", "Already Baking Navigation\nNavigation # {0}" },
        { "TooManyNavigationFlags", "Too Many Navigation Flags\nMax: {0}" },
        { "TooManyRoads", "Too Many Roads\nMax: {0}" },
        { "TooManyRoadVerticies", "Too Many Verticies\nMax: {0}" },
        { "TooManyAnimalSpawnTables", "Too Many Animal Tables\nMax: {0}" },
        { "TooManyVehicleSpawnTables", "Too Many Vehicle Tables\nMax: {0}" },
        { "TooManyItemSpawnTables", "Too Many Item Tables\nMax: {0}" },
        { "TooManyZombieSpawnTables", "Too Many Zombie Tables\nMax: {0}" },
        { "SpawnTierNameTaken", "Name Already Taken" },
        { "TooManySpawnTiers", "Too Many Tiers\nMax: {0}" },
        { "TooManySpawnAssets", "Too Many Assets\nMax: {0}" },
        { "SpawnAssetNotFound", "Asset Not Found\n[{0}] {1}" },
        { "OutOfRegionBounds", "Out of Bounds" },
        { "NoAnimalTableSelected", "No Animal Table Selected" },
        { "NoVehicleTableSelected", "No Vehicle Table Selected" },
        { "NoItemTableSelected", "No Item Table Selected" },
        { "NoZombieTableSelected", "No Zombie Table Selected" },
        { "SpawnAlreadyInstantiating", "Please Wait...\nThe server is catching up" },
    };
    public static Local LevelLoadingLocalization { get; private set; } = null!;

    private static readonly LocalDatDictionary DefaultLevelLoadingLocalization = new LocalDatDictionary
    {
        // {0} = level name, {1} = amt downloaded, {2} = total size, {3} = download speed, {4} = time remaining
        { "CalculatingSpeed", "{0} [ {1} / {2} ] | Calculating Speed" },
        { "Downloading", "{0} [ {1} / {2} ] @ {3} / sec | Remaining: {4}" },
        { "Installing", "{0} | Installing" },
        { "RecoveringMissingPackets", "{0} [ {1} / {2} ] | Recovering Missing Packets" },
        { "DownloadFailed", "Level failed to download. Try joining again." },
        { "DownloadCancelled", "Level download cancelled." }
    };
#if CLIENT
    private static readonly LocalDatDictionary DefaultRichPresenceLocalization = new LocalDatDictionary
    {
        { "Rich_Presence_Editing", "Editing with DevkitServer: {0}" },
        { "Rich_Presence_Playing", "Playing: {0}" },
        { "Rich_Presence_Menu", "In Menu with DevkitServer" },
        { "Rich_Presence_Lobby", "In Lobby" },
    };
#endif

    public static CultureInfo CommandParseLocale { get; set; } = CultureInfo.InvariantCulture;
    public static AssetOrigin BundleOrigin { get; private set; } = new AssetOrigin
    {
        name = ModuleName,
        workshopFileId = 0ul
    };
    static DevkitServerModule()
    {
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
    void IModuleNexus.initialize()
    {
        CommandWindow.Log("Initializing DevkitServer.");
        Stopwatch watch = Stopwatch.StartNew();
        Module? module = null;
        InitializedLogging = false;
        if (LoadFaulted)
            goto fault;
        try
        {
            for (int i = ModuleHook.modules.Count - 1; i >= 0; i--)
            {
                Module mdl = ModuleHook.modules[i];
                if (!mdl.isEnabled || mdl.assemblies == null || mdl.assemblies.All(x => x != AccessorExtensions.DevkitServer))
                    continue;

                module = mdl;
                break;
            }

            if (module == null)
            {
                LoadFaulted = true;
                CommandWindow.LogError("Unable to find DevkitServer in ModuleHook.modules.");
                goto fault;
            }

            Module = module;

            PatchesMain.EarlyInitPatcher();

            AssemblyResolver = new AssemblyResolver();
            AssemblyResolver.ShutdownUnsupportedModules();

#if CLIENT
            if (AssemblyResolver.TriedToLoadUIExtensionModule)
            {
                Compat.UIExtensionManagerCompat.Init();
            }
#endif

#if SERVER
            if (!Dedicator.isStandaloneDedicatedServer)
            {
                CommandWindow.LogError("You are running a dedicated server build on a game client.");
                goto fault;
            }
#elif CLIENT
            if (Dedicator.isStandaloneDedicatedServer)
            {
                CommandWindow.LogError("You are running a client build on a dedicated server.");
                goto fault;
            }
#endif

            // Initialize UniTask
            if (!PlayerLoopHelper.HasBeenInitialized)
                PlayerLoopHelper.Init();

            DevkitServerEncodingExtensions.Register();

            GameObjectHost = new GameObject(ModuleName);
            ComponentHost = GameObjectHost.AddComponent<DevkitServerModuleComponent>();
            GameObjectHost.hideFlags = HideFlags.DontSave;
            Provider.gameMode = new DevkitServerGamemode();
            Object.DontDestroyOnLoad(GameObjectHost);

            Logger.InitForDevkitServer();
            InitializedLogging = true;
            BundleOrigin = AssetUtil.CreateAssetOrigin(ModuleName, 0ul, true);

            PatchesMain.Init();
#if SERVER
            UserCryptographyStore.Initialize();
#endif
#if CLIENT
            MovementUtil.Init();
            LoggerExtensions.ClientPatchUnturnedLogs();
#endif
            GameObjectHost.AddComponent<CachedTime>();
        }
        catch (Exception ex)
        {
            if (InitializedLogging)
            {
                Logger.DevkitServer.LogError("Init", ex, $"Error setting up {ColorizedModuleName}.");
            }
            else
            {
                CommandWindow.LogError($"Error setting up {ColorizedModuleName}.");
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
            ReloadLevelLoadingLocalization();
#if CLIENT
            ReloadRichPresenceLocalization();
#endif
            PluginAdvertising.Get().AddPlugin(MainLocalization.format("Name"));
            _tknSrc = new CancellationTokenSource();
            Logger.DevkitServer.LogInfo("Init", "DevkitServer loading...");

            DevkitServerConfig.Reload();

            if (!LaunchOptions.KeepTempFiles.value)
                DevkitServerConfig.ClearTempFolder();

            if (LoadFaulted)
            {
                Provider.shutdown(1, "Failed to load config.");
                goto fault;
            }

            if (!NetFactory.Init())
            {
                Fault();
                goto fault;
            }
            
            foreach (Type type in Accessor.GetTypesSafe()
                         .Select(x => new KeyValuePair<Type, EarlyTypeInitAttribute?>(x, x.GetAttributeSafe<EarlyTypeInitAttribute>()))
                         .Where(x => x.Value is { RequiresUIAccessTools: false })
                         .OrderByDescending(x => x.Value!.Priority)
                         .ThenBy(x => x.Key.Name)
                         .Select(x => x.Key))
            {
                try
                {
                    RuntimeHelpers.RunClassConstructor(type.TypeHandle);
                    CreateDirectoryAttribute.CreateInType(type, true);
                    Logger.DevkitServer.LogDebug("EarlyTypeInit", $"Initialized static module {type.Format()}.");
                }
                catch (Exception ex)
                {
                    Logger.DevkitServer.LogError("EarlyTypeInit", ex, $"Error while initializing static module {type.Format()}.");
                    Fault();
                    break;
                }
            }

            if (LoadFaulted)
                goto fault;

            CreateDirectoryAttribute.CreateInAssembly(Assembly, true);
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

            GameObject? editor = (GameObject?)Resources.Load("Edit/Editor");
            if (editor != null)
            {
                Component comp = editor.GetComponentInChildren(AccessorExtensions.AssemblyCSharp.GetType("SDG.Unturned.EditorInteract"));
                if (comp != null)
                    Object.DestroyImmediate(comp);
                else
                    Logger.DevkitServer.LogWarning("Init", "Unable to destroy EditorInteract.");
                comp = editor.GetComponentInChildren(typeof(EditorMovement));
                if (comp != null)
                    Object.DestroyImmediate(comp);
                else
                    Logger.DevkitServer.LogWarning("Init", "Unable to destroy EditorMovement.");
                comp = editor.GetComponentInChildren(typeof(EditorLook));
                if (comp != null)
                    Object.DestroyImmediate(comp);
                else
                    Logger.DevkitServer.LogWarning("Init", "Unable to destroy EditorLook.");
                comp = editor.GetComponentInChildren(typeof(EditorArea));
                if (comp != null)
                    Object.DestroyImmediate(comp);
                else
                    Logger.DevkitServer.LogWarning("Init", "Unable to destroy EditorArea.");
                comp = editor.GetComponentInChildren(typeof(HighlightingRenderer));
                if (comp != null)
                    Object.DestroyImmediate(comp);
                else
                    Logger.DevkitServer.LogWarning("Init", "Unable to destroy HighlightingRenderer.");

                Logger.DevkitServer.LogDebug("Init", "Destroyed client-side editor components.");
            }
            else
                Logger.DevkitServer.LogWarning("Init", "Unable to destroy client-side editor components.");

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
            Level.onLevelExited += OnLevelExited;

            ObjectIconPresets.Init();
            GameObject objectItemGeneratorHost = new GameObject("ObjectIconGenerator", typeof(Light), typeof(ObjectIconGenerator), typeof(Camera));
            objectItemGeneratorHost.transform.SetParent(GameObjectHost.transform, true);
            objectItemGeneratorHost.hideFlags = HideFlags.DontSave;
#endif
            AssetUtil.OnBeginLevelLoading += OnLevelStartLoading;
            LevelObjectNetIdDatabase.Init();
            HierarchyItemNetIdDatabase.Init();
            SpawnsNetIdDatabase.Init();
            RoadNetIdDatabase.Init();
            NavigationNetIdDatabase.Init();
            ReplicatedLevelDataRegistry.RegisterFromAssembly(AccessorExtensions.DevkitServer, null, null);

            PluginLoader.LoadPlugins();

            if (!NetFactory.ReclaimMessageBlock())
            {
                Fault();
                goto fault;
            }

            Logger.DevkitServer.LogInfo(NetFactory.Source, $"Hijacked {"Unturned".Colorize(UnturnedColor)}'s networking.");
            CreateDirectoryAttribute.DisposeLoadList();
        }
        catch (Exception ex)
        {
            Fault();
            Logger.DevkitServer.LogError("Init", ex, $"Error loading {ColorizedModuleName}");
        }
        fault:
        watch.Stop();
        if (UnturnedLoaded)
        {
            if (InitializedLogging)
                Logger.DevkitServer.LogInfo("Init", $"{ColorizedModuleName} initializer took {watch.GetElapsedMilliseconds().Format("F2")} ms.");
            else
                CommandWindow.Log($"{ModuleName} initializer took {watch.GetElapsedMilliseconds():F2} ms.");
        }

        if (LoadFaulted)
        {
            if (InitializedLogging)
                Logger.DevkitServer.LogError("Init", $"Failed to load! Loading cancelled. Check for updates on {RepositoryUrl.Format()}.");
            try
            {
                if (module != null)
                    module.isEnabled = false;
            }
            catch (Exception ex)
            {
                if (InitializedLogging)
                {
                    Logger.DevkitServer.LogError("Init", ex, $"Error unloading {ColorizedModuleName}.");
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
            string modName = ColorizedModuleName;
            string license = "GNU General Public License".Colorize(new Color32(255, 204, 102, 255));
            Color32 nameColor = new Color32(86, 98, 246, 255);
            Logger.DevkitServer.LogInfo("Legal", $"{modName} (by @{"blazingflame".Colorize(nameColor)} on {"Discord".Colorize(new Color32(116, 131, 196, 255))} or " +
                                                 $"{"https://github.com/DanielWillett".Format(false)}) initialized.");
            Logger.DevkitServer.LogInfo("Legal", $"Please create an Issue for any bugs at {(RepositoryUrl + "/issues").Format(false)} (one bug per issue please).");
            Logger.DevkitServer.LogInfo("Legal", $"Please give suggestions as a Discussion at {(RepositoryUrl + "/discussions/categories/ideas").Format(false)}.");
            Logger.DevkitServer.LogInfo("Legal", string.Empty, ConsoleColor.White);
            Logger.DevkitServer.LogInfo("Legal", "================================================ Legal ================================================", ConsoleColor.White);
            Logger.DevkitServer.LogInfo("Legal", $" {modName} is licensed under the {"GNU General Public License v3.0".Colorize(new Color32(255, 204, 102, 255))}.", ConsoleColor.White);
            Logger.DevkitServer.LogInfo("Legal", "=======================================================================================================", ConsoleColor.White);
            Logger.DevkitServer.LogInfo("Legal", $" {modName} - Copyright (C) {2024.Format()} - {"Daniel Willett".Colorize(nameColor)}", ConsoleColor.White);
            Logger.DevkitServer.LogInfo("Legal",  " This program comes with ABSOLUTELY NO WARRANTY.", ConsoleColor.White);
            Logger.DevkitServer.LogInfo("Legal",  " This is free software, and you are welcome to redistribute it under certain conditions.", ConsoleColor.White);
            Logger.DevkitServer.LogInfo("Legal", "=======================================================================================================", ConsoleColor.White);
            Logger.DevkitServer.LogInfo("Legal", $" {modName} - Module for Unturned that enables multi-user map editing.", ConsoleColor.White);
            Logger.DevkitServer.LogInfo("Legal", " This program is free software: you can redistribute it and / or modify", ConsoleColor.White);
            Logger.DevkitServer.LogInfo("Legal", " it under the terms of the " + license + " as published by", ConsoleColor.White);
            Logger.DevkitServer.LogInfo("Legal", " the Free Software Foundation, either version 3 of the License, or", ConsoleColor.White);
            Logger.DevkitServer.LogInfo("Legal", " (at your option) any later version.", ConsoleColor.White);
            Logger.DevkitServer.LogInfo("Legal", string.Empty, ConsoleColor.White);
            Logger.DevkitServer.LogInfo("Legal", " This program is distributed in the hope that it will be useful,", ConsoleColor.White);
            Logger.DevkitServer.LogInfo("Legal", " but WITHOUT ANY WARRANTY; without even the implied warranty of", ConsoleColor.White);
            Logger.DevkitServer.LogInfo("Legal", " MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the", ConsoleColor.White);
            Logger.DevkitServer.LogInfo("Legal", " " + license + " for more details.", ConsoleColor.White);
            Logger.DevkitServer.LogInfo("Legal", string.Empty, ConsoleColor.White);
            Logger.DevkitServer.LogInfo("Legal", " You should have received a copy of the " + license, ConsoleColor.White);
            Logger.DevkitServer.LogInfo("Legal", $" along with this program. If not, see {"https://www.gnu.org/licenses/".Format(false)}.", ConsoleColor.White);
            Logger.DevkitServer.LogInfo("Legal", "=======================================================================================================", ConsoleColor.White);
            Logger.DevkitServer.LogInfo("Legal", $" Read the full license agreement at {(RepositoryUrl + "/blob/master/LICENSE").Format(false)}.", ConsoleColor.White);
            Logger.DevkitServer.LogInfo("Legal", "=======================================================================================================", ConsoleColor.White);
            Logger.DevkitServer.LogInfo("Legal", string.Empty, ConsoleColor.White);
        }

        GC.Collect();
    }
    internal static void JustBeforePluginsReflect()
    {
#if CLIENT
        try
        {
            UIAccessTools.Init();

            foreach (Type type in Accessor.GetTypesSafe()
                         .Select(x => new KeyValuePair<Type, EarlyTypeInitAttribute?>(x, x.GetAttributeSafe<EarlyTypeInitAttribute>()))
                         .Where(x => x.Value is { RequiresUIAccessTools: true })
                         .OrderByDescending(x => x.Value!.Priority)
                         .ThenBy(x => x.Key.Name)
                         .Select(x => x.Key))
            {
                try
                {
                    RuntimeHelpers.RunClassConstructor(type.TypeHandle);
                    CreateDirectoryAttribute.CreateInType(type, true);
                    Logger.DevkitServer.LogDebug("EarlyTypeInit", $"Initialized static module {type.Format()}.");
                }
                catch (Exception ex)
                {
                    Logger.DevkitServer.LogError("EarlyTypeInit", ex, $"Error while initializing static module {type.Format()}.");
                    Fault();
                    break;
                }
            }

            UIExtensionManager.Reflect(AccessorExtensions.DevkitServer);
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("EarlyTypeInit", ex, "Failed to reflect DevkitServer UI extensions.");
        }
#endif
    }
    private static async UniTask OnLevelStartLoading(LevelInfo level, CancellationToken token)
    {
        if (!HasLoadedBundle)
        {
            await TryLoadBundle(null).ToUniTask(ComponentHost);
        }
    }
#if CLIENT
    private static void OnChatMessageReceived()
    {
        if (ChatManager.receivedChatHistory.Count <= 0)
            return;

        ReceivedChatMessage msg = ChatManager.receivedChatHistory[0];
        Logger.Unturned.AddLog(Logger.Terminal, (msg.speaker?.playerID?.characterName ?? "SERVER") + " | " + msg.mode.ToString().ToUpperInvariant() + " CHAT",
            Severity.Info, msg.useRichTextFormatting ? FormattingUtil.ConvertRichTextToVirtualTerminalSequences(msg.contents) : msg.contents, null,
            LoggerExtensions.DefaultChatColor);
    }
    private static void OnLevelExited()
    {
        NetId64Registry.Reset();
        
        if (BackupManager == null)
            return;

        Object.Destroy(BackupManager);
        Logger.DevkitServer.LogDebug(BackupManager.Source, "Destroyed backup manager.");
    }
#endif
    private static void OnLevelLoaded(int level)
    {
        if (level == Level.BUILD_INDEX_MENU)
        {
#if CLIENT
            LoadingUI? loadingUI = UIAccessTools.LoadingUI;
            if (loadingUI == null)
            {
                Logger.DevkitServer.LogWarning(nameof(OnLevelLoaded), "Unable to find LoadingUI.");
            }

            if (!LaunchOptions.KeepTempFiles.value)
                DevkitServerConfig.ClearTempFolder();

            ClientFPSLimiter.StopPlayingOnEditorServer();
            ClientUserMovement.StopPlayingOnEditorServer();
#endif
        }
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
        if (level == Level.BUILD_INDEX_GAME)
        {
            Logger.DevkitServer.LogInfo(nameof(OnLevelLoaded), $"Level loaded: {Level.info.getLocalizedName().Format(false)}.");
#if SERVER
            if (IsEditing)
            {
                if (DevkitServerConfig.Config.TcpSettings is { EnableHighSpeedSupport: true })
                    _ = HighSpeedServer.Instance;

                ServerUserMovement.StartPlayingOnEditorServer();
            }
#elif CLIENT
            OptionsSettings.hints = true;
            if (Level.isEditor)
                DevkitServerSpawnsTool.CheckExistingSpawnsForNodeComponents();
            if (IsEditing)
            {
                ClientFPSLimiter.StartPlayingOnEditorServer();
                ClientUserMovement.StartPlayingOnEditorServer();
            }
#endif
        }
        else
            Logger.DevkitServer.LogInfo(nameof(OnLevelLoaded), $"Scene loaded: {level.Format()}.");
    }
    private static void OnPostLevelLoaded(int level)
    {
        if (level == Level.BUILD_INDEX_GAME)
        {
            if (IsEditing)
            {
                TileSync.CreateServersideAuthority();
                ObjectSync.CreateServersideAuthority();
                HierarchySync.CreateServersideAuthority();
                NavigationSync.CreateServersideAuthority();
                RoadSync.CreateServersideAuthority();
                LightingManager.DisableWeather();
            }
            else
            {
                ComponentHost.StartCoroutine(TryLoadBundle(null));
            }
        }

        if (level == Level.BUILD_INDEX_GAME || level == Level.BUILD_INDEX_MENU)
            ComponentHost.StartCoroutine(ClearLoggingErrorsDelayed());

#if CLIENT
        EditorLevel.ServerPendingLevelData = null;
        GC.Collect();
#endif
    }
    private static IEnumerator<WaitForSeconds> ClearLoggingErrorsDelayed()
    {
        yield return new WaitForSeconds(2);
        if (Logger.HasLoadingErrors)
            Logger.ClearLoadingErrors();
        Logger.DevkitServer.LogDebug("Init", $"DevkitServer managed memory usage: {FormattingUtil.FormatCapacity(GC.GetTotalMemory(true), colorize: true)}");

        if (!BitConverter.IsLittleEndian)
        {
            Logger.DevkitServer.LogWarning("Init", string.Empty);
            Logger.DevkitServer.LogWarning("Init", "---- WARNING ----");
            Logger.DevkitServer.LogWarning("Init", "Your machine has a big-endian byte order, you may face issues with improper data transmission, " +
                                                   "please report it as I am unable to test with these conditioins.");
            Logger.DevkitServer.LogWarning("Init", "-----------------");
        }
    }
    private static void LoadSearchLocations()
    {
        string asmDir = Path.GetDirectoryName(AssemblyPath)!;
        List<string> locs = [ Path.GetFullPath(asmDir) ];

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
                Logger.DevkitServer.LogDebug("Init", $"Added module file search location: {directory.Format(true)}.");
            else
                CommandWindow.Log($"Added module file search location: \"{directory}\".");
        }
#endif

        _searchLocations = new ReadOnlyCollection<string>(locs.ToArray());
    }
    /// <summary>
    /// Returns a URL to the github repository with a path relative to its root.
    /// </summary>
    /// <param name="relativePath"></param>
    /// <param name="shortenCommitId">Uses the 7 digit commit ID instead of the full commit ID.</param>
    public static string GetRelativeRepositoryUrl(string? relativePath, bool rawFile, bool shortenCommitId = true)
    {
        string tree = shortenCommitId ? CommitId : LongCommitId;
        if (string.IsNullOrEmpty(tree) || CommitId.Equals("0000000", StringComparison.Ordinal))
            tree = "master";
        bool directory = relativePath == null || relativePath.IndexOf('.') == -1;
        rawFile &= !directory;
        tree = (rawFile ? RawRepositoryUrl : RepositoryUrl) + (directory ? "/tree/" : (rawFile ? "/" : "/blob/")) + tree;
        if (string.IsNullOrWhiteSpace(tree))
            return tree;

        if (string.IsNullOrEmpty(relativePath))
            return tree;

        if (relativePath[0] == '/')
            return tree + relativePath;

        return tree + "/" + relativePath;

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
            Logger.DevkitServer.LogError(nameof(UnloadBundle), ex, $"Error unloading {"devkitserver.masterbundle".Colorize(ModuleColor)} bundle.");
        }

        Bundle = null;
        HasLoadedBundle = false;
    }
    internal static IEnumerator TryLoadBundle(Action? callback)
    {
        ThreadUtil.assertIsGameThread();

        if (HasLoadedBundle)
        {
            callback?.Invoke();
            yield break;
        }

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
            Logger.DevkitServer.LogError(nameof(TryLoadBundle), $"Failed to find DevkitServer bundle folder near {asmDir.Format(false)}.");
            callback?.Invoke();
            yield break;
        }
        MasterBundleConfig? bundle = BundleConfig ?? Assets.findMasterBundleByPath(path);
        if (bundle == null || bundle.assetBundle == null)
        {
            Logger.DevkitServer.LogDebug(nameof(TryLoadBundle), $"Adding DevkitServer Bundle Search Location: {path.Format(false)}.");
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
                Logger.DevkitServer.LogError(nameof(TryLoadBundle), $"Failed to find DevkitServer bundle: {Path.Combine(path, "devkitserver.masterbundle").Format(false)}.");
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
        Logger.DevkitServer.LogInfo(nameof(TryLoadBundle), $"Loaded bundle: {bundle.assetBundleNameWithoutExtension.Format(false)} from {path.Format(false)}.");
        HasLoadedBundle = true;
#if false
        Logger.DevkitServer.LogDebug(nameof(TryLoadBundle), "Assets in bundle:");
        foreach (string asset in Bundle.cfg.assetBundle.GetAllAssetNames())
            Logger.DevkitServer.LogDebug(nameof(TryLoadBundle), "Asset: " + asset.Format() + ".");
#endif
        SharedResources.LoadFromBundle();
        callback?.Invoke();
    }

    internal static void Fault()
    {
        if (LoadFaulted)
            return;

        LoadFaulted = true;
        Logger.DevkitServer.LogWarning("Init", "DevkitServer terminated.");
        Assets? instance = AssetUtil.AssetsInstance;
        if (instance != null)
            instance.StopAllCoroutines();
        Logger.ClearLoadingErrors();
#if SERVER
        Provider.shutdown(10, "DevkitServer failed to load.");
#else
        TimeUtility.InvokeAfterDelay(() => Provider.QuitGame("DevkitServer failed to load."), 10f);
#endif
    }

    private static void OnEditorCreated()
    {
        Logger.DevkitServer.LogInfo(nameof(OnEditorCreated), "Editor loaded.");
#if SERVER
        Provider.modeConfigData.Gameplay.Group_Map = true;
        Provider.modeConfigData.Gameplay.Chart = true;
        Provider.modeConfigData.Gameplay.Satellite = true;
        Provider.modeConfigData.Gameplay.Compass = true;
        Provider.modeConfigData.Gameplay.Timer_Exit = 0;
        Provider.modeConfigData.Gameplay.Timer_Home = 0;
        Provider.hasCheats = true;
#endif
    }

    private static void OnPreSaved()
    {
        Logger.DevkitServer.LogInfo("Save Game", "Saving...");
        if (!Level.isEditor)
            return;
        Thread.BeginCriticalRegion();
        try
        {
            Level.save();
            if (IsEditing)
            {
                HierarchyResponsibilities.Save();
                LevelObjectResponsibilities.Save();
#if SERVER
                BuildableResponsibilities.Save();
#endif
            }
        }
        finally
        {
            Thread.EndCriticalRegion();
        }
    }
    private static void OnSaved()
    {
        Logger.DevkitServer.LogInfo("Save Game", "Saved");
    }
    private static void OnPrePreLevelLoaded(int level)
    {
        if (IsAuthorityEditor)
        {
            if (BackupManager != null)
                Object.Destroy(BackupManager);
            BackupManager = GameObjectHost.AddComponent<BackupManager>();
        }

#if SERVER
        FileUtil.CheckDirectory(false, false, DevkitServerConfig.LevelDirectory, typeof(DevkitServerConfig).GetProperty(nameof(DevkitServerConfig.LevelDirectory), BindingFlags.Public | BindingFlags.Static));

        RoadNetIdDatabase.AssignExisting();
        LevelObjectNetIdDatabase.AssignExisting();
        SpawnsNetIdDatabase.AssignExisting();
        HierarchyItemNetIdDatabase.AssignExisting();
        NavigationNetIdDatabase.AssignExisting();
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
        CartographyTool.Reset();
#if CLIENT
        if (IsEditing)
        {
            ReplicatedLevelDataRegistry.LoadFromLevelData();
        }
#endif
    }

    void IModuleNexus.shutdown()
    {
        /* SAVE DATA */
        OnSaved();

        if (!LaunchOptions.KeepTempFiles.value)
            DevkitServerConfig.ClearTempFolder();

#if CLIENT
        UIExtensionManager.Shutdown();
#endif

        PluginLoader.Unload();
        _tknSrc?.Cancel();
        _tknSrc = null;
        TileSync.DestroyServersideAuthority();
        ObjectSync.DestroyServersideAuthority();
        HierarchySync.DestroyServersideAuthority();
        NavigationSync.DestroyServersideAuthority();
        RoadSync.DestroyServersideAuthority();
        if (BackupManager != null)
            Object.Destroy(BackupManager);
        BackupManager = null;

        Object.Destroy(GameObjectHost);
        Logger.DevkitServer.LogInfo("Shutdown", "Shutting down...");
        PatchesMain.Unpatch();
        UnloadBundle();
        Editor.onEditorCreated -= OnEditorCreated;
        Level.onPostLevelLoaded -= OnPostLevelLoaded;
        Level.onPrePreLevelLoaded -= OnPrePreLevelLoaded;
        SaveManager.onPostSave -= OnSaved;
#if SERVER
        ServerUserMovement.StopPlayingOnEditorServer();
        Provider.onServerConnected -= UserManager.AddUser;
        Provider.onEnemyConnected -= UserManager.OnAccepted;
        Provider.onServerDisconnected -= UserManager.RemoveUser;
        Level.onLevelLoaded -= OnLevelLoaded;
        HighSpeedServer.Deinit();
        UserCryptographyStore.Shutdown();
#else
        DevkitServerGLUtility.Shutdown();
        Provider.onClientConnected -= EditorUser.OnClientConnected;
        Provider.onEnemyConnected -= EditorUser.OnEnemyConnected;
        Provider.onClientDisconnected -= EditorUser.OnClientDisconnected;
        Provider.onEnemyDisconnected -= EditorUser.OnEnemyDisconnected;
        ChatManager.onChatMessageReceived -= OnChatMessageReceived;
        UserTPVControl.Deinit();
        if (ObjectIconGenerator.Instance != null)
            Object.Destroy(ObjectIconGenerator.Instance.gameObject);
        ObjectIconPresets.Deinit();
        MovementUtil.Deinit();
        Level.onLevelExited -= OnLevelExited;
#endif
        AssetUtil.OnBeginLevelLoading -= OnLevelStartLoading;
        LevelObjectNetIdDatabase.Shutdown();
        HierarchyItemNetIdDatabase.Shutdown();
        SpawnsNetIdDatabase.Shutdown();
        RoadNetIdDatabase.Shutdown();
        NavigationNetIdDatabase.Shutdown();
        ReplicatedLevelDataRegistry.Shutdown();

        Instance = null!;
        GameObjectHost = null!;
        LoadFaulted = false;
        Logger.CloseLogger();

        AssemblyResolver.Dispose();
    }
#if CLIENT
    internal static void RegisterDisconnectFromEditingServer()
    {
        IsEditing = false;
        Logger.DevkitServer.LogInfo("Remote Detect", "No longer connected to a DevkitServer host.");
    }
#endif
    public static void ReloadMainLocalization()
    {
        string path = Path.Combine(DevkitServerConfig.LocalizationFilePath, "Main");
        Local lcl = DevkitServerUtility.ReadLocalFromFileOrFolder(path, out string? primaryPath, out string? englishWritePath);
        DevkitServerUtility.UpdateLocalizationFile(ref lcl, DefaultMainLocalization, path, primaryPath, englishWritePath);
        MainLocalization = lcl;
    }
    public static void ReloadCommandsLocalization()
    {
        string path = Path.Combine(DevkitServerConfig.LocalizationFilePath, "Commands");
        Local lcl = DevkitServerUtility.ReadLocalFromFileOrFolder(path, out string? primaryPath, out string? englishWritePath);
        DevkitServerUtility.UpdateLocalizationFile(ref lcl, DefaultCommandLocalization, path, primaryPath, englishWritePath);
        CommandLocalization = lcl;
    }
    public static void ReloadMessagesLocalization()
    {
        string path = Path.Combine(DevkitServerConfig.LocalizationFilePath, "Messages");
        Local lcl = DevkitServerUtility.ReadLocalFromFileOrFolder(path, out string? primaryPath, out string? englishWritePath);
        DevkitServerUtility.UpdateLocalizationFile(ref lcl, DefaultMessageLocalization, path, primaryPath, englishWritePath);
        MessageLocalization = lcl;
    }
    public static void ReloadLevelLoadingLocalization()
    {
        string path = Path.Combine(DevkitServerConfig.LocalizationFilePath, "Level Loading");
        Local lcl = DevkitServerUtility.ReadLocalFromFileOrFolder(path, out string? primaryPath, out string? englishWritePath);
        DevkitServerUtility.UpdateLocalizationFile(ref lcl, DefaultLevelLoadingLocalization, path, primaryPath, englishWritePath);
        LevelLoadingLocalization = lcl;
    }
#if CLIENT
    public static void ReloadRichPresenceLocalization()
    {
        string path = Path.Combine(DevkitServerConfig.LocalizationFilePath, "Rich Presence");
        PatchesMain.RichPresenceLocalizationOverride = DevkitServerUtility.ReadLocalFromFileOrFolder(path, out string? primaryPath, out string? englishWritePath);
        DevkitServerUtility.UpdateLocalizationFile(ref PatchesMain.RichPresenceLocalizationOverride, DefaultRichPresenceLocalization, path, primaryPath, englishWritePath);
    }
#endif
    public static bool IsCompatibleWith(Version otherVersion)
    {
        Version thisVersion = AccessorExtensions.DevkitServer.GetName().Version;
        return thisVersion.Major == otherVersion.Major && thisVersion.Minor == otherVersion.Minor;
    }
    /// <exception cref="InvalidOperationException">Thrown when <see cref="IsEditing"/> is not <see langword="true"/>.</exception>
    public static void AssertIsDevkitServerClient()
    {
        if (IsEditing)
            return;
#if CLIENT
        throw new InvalidOperationException("This operation can only be performed while a client on a DevkitServer server.");
#else
        throw new InvalidOperationException("This operation can only be performed while running a DevkitServer server.");
#endif
    }
#if CLIENT
    public static void AskSave()
    {
        AssertIsDevkitServerClient();
        ClientAskSave.Invoke();
    }
#elif SERVER
    [NetCall(NetCallSource.FromClient, DevkitServerNetCall.AskSave)]
    private static void ReceiveAskSave(MessageContext ctx)
    {
        SaveManager.save();
    }
#endif
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
            catch (OperationCanceledException) { Logger.DevkitServer.LogDebug("QueueOnMainThread", "Execution on update cancelled."); }
            catch (Exception ex)
            {
                Logger.DevkitServer.LogError("QueueOnMainThread", ex, "Error executing main thread operation.");
            }
            finally
            {
                res?.Complete();
            }
        }
#if CLIENT
        MovementUtil.OnUpdate();
#endif
    }

    [UsedImplicitly]
    private void Start()
    {
        _ticks = 0;
    }
}