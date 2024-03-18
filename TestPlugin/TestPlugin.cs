using DevkitServer.API;
using DevkitServer.Multiplayer.Networking;
using HtmlAgilityPack;

namespace TestPlugin;

public class TestPlugin : Plugin<TestPluginConfig>
{
    internal static readonly NetCall<int> TestNetCall = new NetCall<int>(ReceiveTestNetCall);
    public static TestPlugin? Instance { get; private set; }

    [NetCall(NetCallSource.FromEither, "96603f5950cc4460825763ac77286a1a")]
    private static void ReceiveTestNetCall(MessageContext ctx, int val)
    {
        Instance?.LogInfo($"Received test net call: {val.Format()}.");
    }
    public override string Name => "TestPlugin.Core";

#if DEBUG
    public override bool DeveloperMode => true;
#else
    public override bool DeveloperMode => false;
#endif

    protected override void Load()
    {
        Instance = this;
        this.LogInfo("Loaded " + Name + ".");
        this.LogInfo("On Server: " + Provider.serverID);
        this.LogInfo("On Client: " + Provider.client);
        bool test = HtmlDocument.IsWhiteSpace(' ');
        _ = test;
        this.LogInfo("Config value: " + Configuration.TestArgumentOne + ".");
    }

    protected override void Unload()
    {
        Instance = null;
        this.LogInfo("Unloaded " + Name + ".");
    }
}