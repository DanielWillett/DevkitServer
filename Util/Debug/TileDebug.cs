#if DEBUG
using JetBrains.Annotations;
using SDG.Framework.Landscapes;
using SDG.Framework.Rendering;

namespace DevkitServer.Util.Debug;
internal sealed class TileDebug : MonoBehaviour
{
    internal static bool Enabled { get; set; }
    [UsedImplicitly]
    private void Start()
    {
        GLRenderer.render += HandleGLRender;
    }
    [UsedImplicitly]
    private void OnDestroy()
    {
        GLRenderer.render -= HandleGLRender;
    }
    private static void HandleGLRender()
    {
        LandscapeUtil.GetAllTiles();
    }
}
#endif