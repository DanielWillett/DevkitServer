using DevkitServer.API;

namespace TestPlugin;

[PluginLoadPriority(1)]
internal class TestPluginSubmodule : Plugin
{
    public override string Name => "TestPlugin.Module";
    protected override void Load()
    {
        LogInfo("Loaded " + Name + ".");
        LogInfo("On Server: " + Provider.serverID);
        LogInfo("On Client: " + Provider.client);
    }
    protected override void Unload()
    {
        LogInfo("Unloaded " + Name + ".");
    }

    protected override LocalDatDictionary DefaultLocalization => new LocalDatDictionary
    {
        { "TestKey",  "Test Value" },
        { "TestKey2", "Test Value 2" }
    };
}
