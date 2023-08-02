using DevkitServer.API;

namespace TestPlugin;

[LoadPriority(1)]
internal class TestPluginSubmodule : Plugin
{
    public override string Name => "TestPlugin.Module";

#if DEBUG
    public override bool DeveloperMode => true;
#else
    public override bool DeveloperMode => false;
#endif

    protected override void Load()
    {
        LogInfo("Loaded " + Name + ".");
#if SERVER
        LogInfo("On Server: " + Provider.serverID);
#elif CLIENT
        LogInfo("On Client: " + Provider.client);
#endif
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
