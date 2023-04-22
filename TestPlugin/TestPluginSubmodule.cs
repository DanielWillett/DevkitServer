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

    protected override DatDictionary DefaultLocalization => new DatDictionary
    {
        { "TestKey",  new DatValue("Test Value") },
        { "TestKey2", new DatValue("Test Value 2") }
    };
}
