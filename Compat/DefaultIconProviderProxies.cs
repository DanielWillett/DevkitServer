#if CLIENT
using DevkitServer.API;
using DevkitServer.API.UI.Icons;

namespace DevkitServer.Compat;

[Ignore]
internal class DefaultIconProviderProxyToInternal : IDefaultIconProvider
{
    private readonly DanielWillett.LevelObjectIcons.API.IDefaultIconProvider _implementation;
    public DefaultIconProviderProxyToInternal(DanielWillett.LevelObjectIcons.API.IDefaultIconProvider implementation)
    {
        _implementation = implementation;
    }
    public void GetMetrics(ObjectAsset @object, out Vector3 position, out Quaternion rotation) => _implementation.GetMetrics(@object, out position, out rotation);
    public bool AppliesTo(ObjectAsset @object) => _implementation.AppliesTo(@object);
    public int Priority => _implementation.Priority;
}
#endif