using System.Text.Json;

namespace DevkitServer.API.Cartography.Compositors;

public struct CartographyCompositorConfigurationInfo
{
    /// <summary>
    /// The name of the <see cref="ICartographyCompositor"/> type to use for compositing this layer.
    /// </summary>
    public string? TypeName;

    /// <summary>
    /// Optional extra configuration usually supplied through the object in the compositor list.
    /// </summary>
    public JsonElement ExtraConfig;
}
