using System.Diagnostics.Eventing.Reader;

namespace DevkitServer.Core;
internal static class SharedResources
{
#if CLIENT
    public static Shader? LogicShader;
#endif

    internal static void LoadFromBundle()
    {
        Bundle? bundle = DevkitServerModule.Bundle;
        if (bundle == null)
        {
            Logger.LogWarning("Tried to setup shared resources without a loaded bundle.");
            return;
        }
#if CLIENT
        if (LogicShader == null || !LogicShader.name.Equals("Unlit/DS_Passthrough"))
        {
            Material mat = bundle.load<Material>("resources/mat_passthrough");
            LogicShader = mat == null ? Shader.Find("Unlit/Color") : mat.shader;
        }
        else
            LogicShader = Shader.Find("Unlit/Color");

        Logger.LogDebug($"Found logic shader: {LogicShader?.name.Format()}.");
#endif
    }
}
