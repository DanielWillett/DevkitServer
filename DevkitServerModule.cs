﻿#if CLIENT
#define TILE_DEBUG_GL
#endif
#define TILE_SYNC
using Cysharp.Threading.Tasks;
using DevkitServer.API;
using DevkitServer.Configuration;
using DevkitServer.Core;
using DevkitServer.Levels;
using DevkitServer.Multiplayer;
using DevkitServer.Multiplayer.Actions;
using DevkitServer.Multiplayer.Levels;
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
using DevkitServer.Framework;
using UnityEngine.SceneManagement;
using Module = SDG.Framework.Modules.Module;
using Version = System.Version;
#if CLIENT
using DevkitServer.API.Logging;
using DevkitServer.Core.Tools;
using DevkitServer.Players;
using DevkitServer.Players.UI;
using SDG.Framework.Utilities;
#if DEBUG
using DevkitServer.Util.Debugging;
#endif
#endif
#if SERVER
using HighlightingSystem;
#endif

namespace DevkitServer;

public sealed class DevkitServerModule : IModuleNexus
{
    public static readonly string RepositoryUrl = "https://github.com/DanielWillett/DevkitServer"; // don't suffix these with '/'
    public static readonly string RawRepositoryUrl = "https://raw.githubusercontent.com/DanielWillett/DevkitServer";
    public const string ModuleName = "DevkitServer";
    public static readonly string ServerRule = "DevkitServer";
    internal static readonly Color32 ModuleColor = new Color32(0, 255, 153, 255);
    internal static readonly Color32 UnturnedColor = new Color32(99, 123, 99, 255);
    internal static NetCall ClientAskSave = new NetCall(DevkitServerNetCall.AskSave);
    private static CancellationTokenSource? _tknSrc;
    private static string? _asmPath;
    private static IReadOnlyList<string>? _searchLocations;
    private static string? _commitIdShort;
    private static AssemblyResolver _asmResolver = null!;
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
    public static bool IsMainThread => ThreadUtil.gameThread == Thread.CurrentThread;
    public static CancellationToken UnloadToken => _tknSrc == null ? CancellationToken.None : _tknSrc.Token;
    public static Local MainLocalization { get; private set; } = null!;
    public static BackupManager? BackupManager { get; private set; }
    public static bool MonoLoaded { get; }
    public static bool UnityLoaded { get; }
    public static bool UnturnedLoaded { get; }
    public static bool InitializedLogging { get; private set; }
    public static bool InitializedPluginLoader { get; internal set; }
    public static string AssemblyPath => _asmPath ??= Accessor.DevkitServer.Location;
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
        { "WorkshopSubmitMenuExistingModIdLabel", "Override Mod ID" }
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
        { "NotDevkitServerClient", "<#ff8c69>You must be connected to a DevkitServer server." }
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
        { "AlreadyBakingNavigation", "Already Baking Navigation" }
    };

    public static CultureInfo CommandParseLocale { get; set; } = CultureInfo.InvariantCulture;
    public static AssetOrigin BundleOrigin { get; }
    static DevkitServerModule()
    {
        BundleOrigin = AssetUtil.CreateAssetOrigin(ModuleName, 0ul, true);

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
        Stopwatch watch = Stopwatch.StartNew();
        InitializedLogging = false;
        if (LoadFaulted)
            goto fault;
        try
        {
            _asmResolver = new AssemblyResolver();
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
            GameObjectHost = new GameObject(ModuleName);
            ComponentHost = GameObjectHost.AddComponent<DevkitServerModuleComponent>();
            GameObjectHost.hideFlags = HideFlags.DontSave;
            Provider.gameMode = new DevkitServerGamemode();
            Object.DontDestroyOnLoad(GameObjectHost);

            Logger.InitLogger();
            InitializedLogging = true;
            PatchesMain.Init();
#if CLIENT
            MovementUtil.Init();
            Logger.PostPatcherSetupInitLogger();
#endif
            GameObjectHost.AddComponent<CachedTime>();
        }
        catch (Exception ex)
        {
            if (InitializedLogging)
            {
                try
                {
                    Logger.LogError($"Error setting up {ModuleName.Colorize(ModuleColor)}");
                    Logger.LogError(ex);
                }
                catch
                {
                    CommandWindow.LogError($"Error setting up {ModuleName}.");
                    CommandWindow.LogError(ex);
                }
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

            DevkitServerConfig.ClearTempFolder();

            if (LoadFaulted)
            {
                Provider.shutdown(1, "Failed to load config.");
                goto fault;
            }

            if (!NetFactory.Init())
            {
                Fault();
                Logger.LogError($"Failed to load! Loading cancelled. Check for updates on {RepositoryUrl.Format()}.");
                goto fault;
            }
            
            foreach (Type type in Accessor.GetTypesSafe()
                         .Select(x => new KeyValuePair<Type, EarlyTypeInitAttribute?>(x,
                             (EarlyTypeInitAttribute?)Attribute.GetCustomAttribute(x, typeof(EarlyTypeInitAttribute))))
                         .Where(x => x.Value is { RequiresUIAccessTools: false })
                         .OrderByDescending(x => x.Value!.Priority)
                         .ThenBy(x => x.Key.Name)
                         .Select(x => x.Key))
            {
                try
                {
                    RuntimeHelpers.RunClassConstructor(type.TypeHandle);
                    CreateDirectoryAttribute.CreateInType(type, true);
                    Logger.LogDebug("Initialized static module " + type.Format() + ".");
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error while initializing static module " + type.Format() + ".");
                    Logger.LogError(ex);
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
                Component comp = editor.GetComponentInChildren(Accessor.AssemblyCSharp.GetType("SDG.Unturned.EditorInteract"));
                if (comp != null)
                    Object.DestroyImmediate(comp);
                else
                    Logger.LogWarning("Unable to destroy EditorInteract.");
                comp = editor.GetComponentInChildren(typeof(EditorMovement));
                if (comp != null)
                    Object.DestroyImmediate(comp);
                else
                    Logger.LogWarning("Unable to destroy EditorMovement.");
                comp = editor.GetComponentInChildren(typeof(EditorLook));
                if (comp != null)
                    Object.DestroyImmediate(comp);
                else
                    Logger.LogWarning("Unable to destroy EditorLook.");
                comp = editor.GetComponentInChildren(typeof(EditorArea));
                if (comp != null)
                    Object.DestroyImmediate(comp);
                else
                    Logger.LogWarning("Unable to destroy EditorArea.");
                comp = editor.GetComponentInChildren(typeof(HighlightingRenderer));
                if (comp != null)
                    Object.DestroyImmediate(comp);
                else
                    Logger.LogWarning("Unable to destroy HighlightingRenderer.");

                Logger.LogDebug("Destroyed client-side editor components.");
            }
            else
                Logger.LogWarning("Unable to destroy client-side editor components.");

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
            GameObject objectItemGeneratorHost = new GameObject("ObjectIconGenerator", typeof(Light), typeof(IconGenerator), typeof(Camera));
            objectItemGeneratorHost.transform.SetParent(GameObjectHost.transform, true);
            objectItemGeneratorHost.hideFlags = HideFlags.DontSave;
            Object.DontDestroyOnLoad(objectItemGeneratorHost);
#endif
            AssetUtil.OnBeginLevelLoading += OnLevelStartLoading;
            LevelObjectNetIdDatabase.Init();
            HierarchyItemNetIdDatabase.Init();
            SpawnpointNetIdDatabase.Init();
            ReplicatedLevelDataRegistry.RegisterFromAssembly(Accessor.DevkitServer, null, null);

            PluginLoader.LoadPlugins();
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
            string modName = ModuleName.Colorize(ModuleColor);
            string license = "GNU General Public License".Colorize(new Color32(255, 204, 102, 255));
            Color32 nameColor = new Color32(86, 98, 246, 255);
            Logger.LogInfo($"{modName} (by @{"blazingflame".Colorize(nameColor)} on {"Discord".Colorize(new Color32(116, 131, 196, 255))} or " +
                           $"{"https://github.com/DanielWillett".Format(false)}) initialized.");
            Logger.LogInfo($"Please create an Issue for any bugs at {(RepositoryUrl + "/issues").Format(false)} (one bug per issue please).");
            Logger.LogInfo($"Please give suggestions as a Discussion at {(RepositoryUrl + "/discussions/categories/ideas").Format(false)}.");
            Logger.LogInfo(string.Empty, ConsoleColor.White);
            Logger.LogInfo("================================================ Legal ================================================", ConsoleColor.White);
            Logger.LogInfo($" {modName} is licensed under the {"GNU General Public License v3.0".Colorize(new Color32(255, 204, 102, 255))}.", ConsoleColor.White);
            Logger.LogInfo("=======================================================================================================", ConsoleColor.White);
            Logger.LogInfo($" {modName} - Copyright (C) {2023.Format()} - {"Daniel Willett".Colorize(nameColor)}", ConsoleColor.White);
            Logger.LogInfo( " This program comes with ABSOLUTELY NO WARRANTY.", ConsoleColor.White);
            Logger.LogInfo( " This is free software, and you are welcome to redistribute it under certain conditions.", ConsoleColor.White);
            Logger.LogInfo("=======================================================================================================", ConsoleColor.White);
            Logger.LogInfo($" {modName} - Module for Unturned that enables multi-user map editing.", ConsoleColor.White);
            Logger.LogInfo(" This program is free software: you can redistribute it and / or modify", ConsoleColor.White);
            Logger.LogInfo(" it under the terms of the " + license + " as published by", ConsoleColor.White);
            Logger.LogInfo(" the Free Software Foundation, either version 3 of the License, or", ConsoleColor.White);
            Logger.LogInfo(" (at your option) any later version.", ConsoleColor.White);
            Logger.LogInfo(string.Empty, ConsoleColor.White);
            Logger.LogInfo(" This program is distributed in the hope that it will be useful,", ConsoleColor.White);
            Logger.LogInfo(" but WITHOUT ANY WARRANTY; without even the implied warranty of", ConsoleColor.White);
            Logger.LogInfo(" MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the", ConsoleColor.White);
            Logger.LogInfo(" " + license + " for more details.", ConsoleColor.White);
            Logger.LogInfo(string.Empty, ConsoleColor.White);
            Logger.LogInfo(" You should have received a copy of the " + license, ConsoleColor.White);
            Logger.LogInfo($" along with this program. If not, see {"https://www.gnu.org/licenses/".Format(false)}.", ConsoleColor.White);
            Logger.LogInfo("=======================================================================================================", ConsoleColor.White);
            Logger.LogInfo($" Read the full license agreement at {(RepositoryUrl + "/blob/master/LICENSE").Format(false)}.", ConsoleColor.White);
            Logger.LogInfo("=======================================================================================================", ConsoleColor.White);
            Logger.LogInfo(string.Empty, ConsoleColor.White);
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
                         .Select(x => new KeyValuePair<Type, EarlyTypeInitAttribute?>(x,
                             (EarlyTypeInitAttribute?)Attribute.GetCustomAttribute(x, typeof(EarlyTypeInitAttribute))))
                         .Where(x => x.Value is { RequiresUIAccessTools: true })
                         .OrderByDescending(x => x.Value!.Priority)
                         .ThenBy(x => x.Key.Name)
                         .Select(x => x.Key))
            {
                try
                {
                    RuntimeHelpers.RunClassConstructor(type.TypeHandle);
                    CreateDirectoryAttribute.CreateInType(type, true);
                    Logger.LogDebug("Initialized static module " + type.Format() + ".");
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error while initializing static module " + type.Format() + ".");
                    Logger.LogError(ex);
                    Fault();
                    break;
                }
            }

            UIExtensionManager.Reflect(Accessor.DevkitServer);
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to reflect DevkitServer UI extensions.");
            Logger.LogError(ex);
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
        if (ChatManager.receivedChatHistory.Count > 0)
        {
            ReceivedChatMessage msg = ChatManager.receivedChatHistory[0];
            Logger.CoreLog("[" + (msg.speaker?.playerID?.characterName ?? "SERVER") + " | " + msg.mode.ToString().ToUpperInvariant() + "] "
                           + (msg.useRichTextFormatting ? FormattingUtil.ConvertRichTextToANSI(msg.contents) : msg.contents),
                "UNTURNED", "CHAT", color: ConsoleColor.White, Severity.Info);
        }
    }
    private static void OnLevelExited()
    {
        if (BackupManager != null)
        {
            Object.Destroy(BackupManager);
            Logger.LogDebug("Destoryed backup manager.");
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
#elif CLIENT
            OptionsSettings.hints = true;
            if (Level.isEditor)
                DevkitServerSpawnsTool.CheckExistingSpawnsForNodeComponents();
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
        else if (level == Level.BUILD_INDEX_GAME)
        {
            ComponentHost.StartCoroutine(TryLoadBundle(null));
        }


        if (level == Level.BUILD_INDEX_GAME)
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
        Logger.LogDebug($"DevkitServer managed memory usage: {DevkitServerUtility.FormatBytes(GC.GetTotalMemory(true)).Format(false)}");

        if (!BitConverter.IsLittleEndian)
        {
            Logger.LogWarning(string.Empty);
            Logger.LogWarning("---- WARNING ----");
            Logger.LogWarning("Your machine has a big-endian byte order, you may face issues with improper data transmission, " +
                              "please report it as I am unable to test with these conditioins.");
            Logger.LogWarning("-----------------");
        }
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
        
        if (!string.IsNullOrEmpty(relativePath))
        {
            if (relativePath![0] == '/')
                return tree + relativePath;

            return tree + "/" + relativePath;
        }

        return tree;
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
        Logger.LogDebug("Assets in bundle:");
        foreach (string asset in Bundle.cfg.assetBundle.GetAllAssetNames())
            Logger.LogDebug("Asset: " + asset.Format() + ".");
#endif
        SharedResources.LoadFromBundle();
        callback?.Invoke();
    }

    internal static void Fault()
    {
        if (!LoadFaulted)
        {
            LoadFaulted = true;
            Logger.LogWarning("DevkitServer terminated.");
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
        Provider.hasCheats = true;
#endif
    }

    private static void OnPreSaved()
    {
        Logger.LogInfo("Saving...");
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
        Logger.LogInfo("Saved");
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
        DevkitServerUtility.CheckDirectory(false, false, DevkitServerConfig.LevelDirectory, typeof(DevkitServerConfig).GetProperty(nameof(DevkitServerConfig.LevelDirectory), BindingFlags.Public | BindingFlags.Static));

        LevelObjectNetIdDatabase.AssignExisting();
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
        CartographyUtil.Reset();
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

        DevkitServerConfig.ClearTempFolder();

        PluginLoader.Unload();
        _tknSrc?.Cancel();
        _tknSrc = null;
        TileSync.DestroyServersideAuthority();
        if (BackupManager != null)
            Object.Destroy(BackupManager);
        BackupManager = null;

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
        if (IconGenerator.Instance != null)
            Object.Destroy(IconGenerator.Instance.gameObject);
        ObjectIconPresets.Deinit();
        MovementUtil.Deinit();
        Level.onLevelExited -= OnLevelExited;
#endif
        AssetUtil.OnBeginLevelLoading -= OnLevelStartLoading;
        LevelObjectNetIdDatabase.Shutdown();
        HierarchyItemNetIdDatabase.Shutdown();
        SpawnpointNetIdDatabase.Shutdown();
        ReplicatedLevelDataRegistry.Shutdown();

        Instance = null!;
        GameObjectHost = null!;
        LoadFaulted = false;
        Logger.CloseLogger();

        _asmResolver.Dispose();
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
    public static bool IsCompatibleWith(Version otherVersion)
    {
        Version thisVersion = Accessor.DevkitServer.GetName().Version;
        return thisVersion.Major == otherVersion.Major && thisVersion.Minor == otherVersion.Minor;
    }
    /// <exception cref="InvalidOperationException">Thrown when <see cref="IsEditing"/> is not <see langword="true"/>.</exception>
    public static void AssertIsDevkitServerClient()
    {
        if (IsEditing)
            return;

        throw new InvalidOperationException("This operation can only be performed while a client on a DevkitServer server.");
    }
#if CLIENT
    public static void AskSave()
    {
        AssertIsDevkitServerClient();
        ClientAskSave.Invoke();
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