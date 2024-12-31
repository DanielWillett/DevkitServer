using System.Text.Json;

#if CLIENT
namespace DevkitServer.API.Cartography.Compositors;

/// <summary>
/// Defines a plugin-implemented cartography compositor.
/// </summary>
public struct CartographyCompositorInfo
{
    /// <summary>
    /// Type of the color provider. Implements <see cref="ICartographyCompositor"/>.
    /// </summary>
    public Type Type { get; }

    /// <summary>
    /// Custom configuration that can optionally be provded to a compositor.
    /// </summary>
    public JsonElement Config { get; internal set; }

    /// <summary>
    /// Plugin that implements the provider.
    /// </summary>
    public IDevkitServerPlugin Plugin { get; }

    /// <summary>
    /// Priority for this provider.
    /// </summary>
    public int Priority { get; }

    /// <summary>
    /// Is this <see cref="ICartographyCompositor"/> enabled for satellite renders?
    /// </summary>
    public bool SupportsSatellite { get; }

    /// <summary>
    /// Is this <see cref="ICartographyCompositor"/> enabled for chart renders?
    /// </summary>
    public bool SupportsChart { get; }

    internal CartographyCompositorInfo(Type type, IDevkitServerPlugin plugin, int priority, bool supportsSatellite, bool supportsChart)
    {
        Type = type;
        Plugin = plugin;
        Priority = priority;
        SupportsSatellite = supportsSatellite;
        SupportsChart = supportsChart;
    }
}
#endif