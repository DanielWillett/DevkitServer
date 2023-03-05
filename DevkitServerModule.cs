using System.Reflection;
using SDG.Framework.Modules;
using System.Runtime.CompilerServices;
using DevkitServer.Multiplayer;
using DevkitServer.Patches;
using SDG.Framework.Utilities;

namespace DevkitServer;

public sealed class DevkitServerModule : IModuleNexus
{
    public static readonly NetCall<string> SayString2 = new NetCall<string>(1);
    public static GameObject GameObjectHost { get; private set; } = null!;
    public static DevkitServerModule Instance { get; private set; } = null!;
    public static void EarlyInitialize()
    {
        GameObjectHost = new GameObject("DevkitServer");
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

            NetFactory.Init();

#if SERVER
            Provider.onServerConnected += OnServerConnected;
            PlayerLife.onPlayerDied += OnPlayerDied;
#endif
        }
        catch (Exception ex)
        {
            Logger.LogError("Error loading...");
            Logger.LogError(ex);
        }
    }

#if CLIENT
    [NetCall(NetCallSource.FromServer, 1)]
    private static void OnSent(MessageContext ctx, string str)
    {
        Logger.LogInfo(str);
    }
#endif


#if SERVER
    private void OnPlayerDied(PlayerLife sender, EDeathCause cause, ELimb limb, CSteamID instigator)
    {
        SayString2.Invoke(sender.player.channel.owner.transportConnection, "lol you died");
    }
    private void OnServerConnected(CSteamID steamid)
    {
        ITransportConnection? t = Provider.findTransportConnection(steamid);
        if (t != null)
        {
            Logger.LogInfo("Sending saystring:");
            SayString2.Invoke(t, steamid.m_SteamID.ToString());
        }
    }
#endif

    public void shutdown()
    {
        Logger.CloseLogger();
        Object.Destroy(GameObjectHost);
        Logger.LogInfo("Shutting down...");
        PatchesMain.Unpatch();
#if SERVER
        Provider.onServerConnected -= OnServerConnected;
#endif

        Instance = null!;
        GameObjectHost = null!;
    }
    [ModuleInitializer]
    public static void ModuleInitializer()
    {
        EarlyInitialize();
    }
}