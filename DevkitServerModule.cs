using System.Reflection;
using DevkitServer.Multiplayer;
using DevkitServer.Patches;
using DevkitServer.Players;
using SDG.Framework.Modules;
using System.Runtime.CompilerServices;

namespace DevkitServer;

public sealed class DevkitServerModule : IModuleNexus
{
    public static GameObject GameObjectHost { get; private set; } = null!;
    public static DevkitServerModule Instance { get; private set; } = null!;
    public static bool IsEditing { get; internal set; }
    public static void EarlyInitialize()
    {
        GameObjectHost = new GameObject("DevkitServer");
        Provider.gameMode = new DevkitServerGamemode();
        Object.DontDestroyOnLoad(GameObjectHost);

        Logger.InitLogger();
        Logger.LogInfo("DevkitServer by BlazingFlame#0001 (https://github.com/DanielWillett) initialized.");
        PatchesMain.Init();
    }
    public void initialize()
    {
        try
        {
            Instance = this;
            Logger.LogInfo("DevkitServer loading...");

            if (!NetFactory.Init())
            {
                Logger.LogError("Failed to load! Loading cancelled. Check for updates on https://github.com/DanielWillett/DevkitServer.");
                return;
            }

#if SERVER
            Provider.onServerConnected += UserManager.AddPlayer;
            Provider.onServerDisconnected += UserManager.RemovePlayer;
            Level.onLevelLoaded += OnLevelLoaded;
            Editor.onEditorCreated += OnEditorCreated;
#else
            Provider.onClientConnected += EditorUser.OnClientConnected;
            Provider.onClientDisconnected += EditorUser.OnClientDisconnected;
#endif
        }
        catch (Exception ex)
        {
            Logger.LogError("Error loading...");
            Logger.LogError(ex);
        }
    }

#if SERVER
    private static void OnEditorCreated()
    {
        Logger.LogInfo("Editor loaded.");
        Assembly sdg = typeof(Provider).Assembly;
        Component comp = Level.editing.GetComponentInChildren(sdg.GetType("SDG.Unturned.EditorInteract"));
        Object.Destroy(comp);
        comp = Level.editing.GetComponentInChildren(typeof(EditorMovement));
        Object.Destroy(comp);
        comp = Level.editing.GetComponentInChildren(typeof(EditorLook));
        Object.Destroy(comp);
        comp = Level.editing.GetComponentInChildren(typeof(EditorArea));
        Object.Destroy(comp);
    }
    private void OnLevelLoaded(int level)
    {
        Logger.LogInfo("Level loaded: " + level + ".");
        if (level != Level.BUILD_INDEX_GAME)
            return;
        
        Logger.DumpGameObject(Level.editing.gameObject);
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
        Provider.onClientDisconnected -= EditorUser.OnClientDisconnected;
#endif

        Instance = null!;
        GameObjectHost = null!;
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