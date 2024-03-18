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
        this.LogInfo("Loaded " + Name + ".");
#if SERVER
        this.LogInfo("On Server: " + Provider.serverID);
#elif CLIENT
        this.LogInfo("On Client: " + Provider.client);
#endif
    }
    protected override void Unload()
    {
        this.LogInfo("Unloaded " + Name + ".");
    }

    protected override LocalDatDictionary DefaultLocalization => new LocalDatDictionary
    {
        { "TestKey",  "Test Value" },
        { "TestKey2", "Test Value 2" }
    };
}
