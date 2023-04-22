using DevkitServer.API;
using HtmlAgilityPack;

namespace TestPlugin;

public class TestPlugin : Plugin<TestPluginConfig>
{
    public override string Name => "TestPlugin.Core";
    protected override void Load()
    {
        LogInfo("Loaded " + Name + ".");
        LogInfo("On Server: " + Provider.serverID);
        LogInfo("On Client: " + Provider.client);
        bool test = HtmlDocument.IsWhiteSpace(' ');
        _ = test;
        LogInfo("Config value: " + Configuration.TestArgumentOne + ".");
    }

    protected override void Unload()
    {
        LogInfo("Unloaded " + Name + ".");
    }
}