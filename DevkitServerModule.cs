using System.Reflection;
using System.Reflection.Emit;
using DevkitServer.Multiplayer;
using DevkitServer.Patches;
using DevkitServer.Players;
using SDG.Framework.Modules;
using System.Runtime.CompilerServices;
using DevkitServer.Configuration;
using DevkitServer.Multiplayer.Networking;

namespace DevkitServer;

public sealed class DevkitServerModule : IModuleNexus
{
    public static readonly string ServerRule = "DevkitServer";
    public static GameObject GameObjectHost { get; private set; } = null!;
    public static DevkitServerModuleComponent ComponentHost { get; private set; } = null!;
    public static DevkitServerModule Instance { get; private set; } = null!;
    public static bool IsEditing { get; internal set; }
    public static bool LoadFaulted { get; private set; }
    public static LevelInfo? PendingLevelInfo { get; internal set; }
    public static void EarlyInitialize()
    {
        LoadFaulted = false;
        GameObjectHost = new GameObject("DevkitServer");
        ComponentHost = GameObjectHost.AddComponent<DevkitServerModuleComponent>();
        Provider.gameMode = new DevkitServerGamemode();
        Object.DontDestroyOnLoad(GameObjectHost);

        Logger.InitLogger();
        Logger.LogInfo("DevkitServer by BlazingFlame#0001 (https://github.com/DanielWillett) initialized.");
        PatchesMain.Init();
#if CLIENT
        Logger.PostPatcherSetupInitLogger();
#endif
    }

    public void initialize()
    {
        try
        {
            Instance = this;
            Logger.LogInfo("DevkitServer loading...");
            DevkitServerConfig.Reload();

            if (LoadFaulted)
            {
                Provider.shutdown(1, "Failed to load config.");
                return;
            }

            if (!NetFactory.Init())
            {
                Fault();
                Logger.LogError("Failed to load! Loading cancelled. Check for updates on https://github.com/DanielWillett/DevkitServer.");
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

            Editor.onEditorCreated += OnEditorCreated;
#if SERVER
            Provider.onServerConnected += UserManager.AddPlayer;
            Provider.onServerDisconnected += UserManager.RemovePlayer;
            Level.onLevelLoaded += OnLevelLoaded;
#else
            Provider.onClientConnected += EditorUser.OnClientConnected;
            Provider.onEnemyConnected += EditorUser.OnEnemyConnected;
            Provider.onClientDisconnected += EditorUser.OnClientDisconnected;
            Provider.onEnemyDisconnected += EditorUser.OnEnemyDisconnected;
#endif
        }
        catch (Exception ex)
        {
            Logger.LogError("Error loading...");
            Logger.LogError(ex);
            Fault();
        }
    }

    internal static void Fault() => LoadFaulted = false;

    private static void OnEditorCreated()
    {
        Logger.LogInfo("Editor loaded.");
#if SERVER
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
#if DEBUG
        Logger.DumpGameObject(Level.editing.gameObject);
#endif
#endif
    }
#if SERVER
    private void OnLevelLoaded(int level)
    {
        Logger.LogInfo("Level loaded: " + level + ".");
        if (level != Level.BUILD_INDEX_GAME)
            return;
        
    }
#endif

    public void shutdown()
    {
        Logger.CloseLogger();
        Object.Destroy(GameObjectHost);
        Logger.LogInfo("Shutting down...");
        PatchesMain.Unpatch();
#if SERVER
        Provider.onServerConnected -= UserManager.AddPlayer;
        Provider.onServerDisconnected -= UserManager.RemovePlayer;
        Level.onLevelLoaded -= OnLevelLoaded;
#else
        Provider.onClientConnected -= EditorUser.OnClientConnected;
        Provider.onEnemyConnected -= EditorUser.OnEnemyConnected;
        Provider.onClientDisconnected -= EditorUser.OnClientDisconnected;
        Provider.onEnemyDisconnected -= EditorUser.OnEnemyDisconnected;
#endif

        Instance = null!;
        GameObjectHost = null!;
        LoadFaulted = false;
    }
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
}

public sealed class DevkitServerModuleComponent : MonoBehaviour
{

}