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
    protected override void Load()
    {
        Instance = this;
        LogInfo("Loaded " + Name + ".");
        LogInfo("On Server: " + Provider.serverID);
        LogInfo("On Client: " + Provider.client);
        bool test = HtmlDocument.IsWhiteSpace(' ');
        _ = test;
        LogInfo("Config value: " + Configuration.TestArgumentOne + ".");
    }

    protected override void Unload()
    {
        Instance = null;
        LogInfo("Unloaded " + Name + ".");
    }
}