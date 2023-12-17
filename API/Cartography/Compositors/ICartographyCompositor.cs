#if CLIENT
namespace DevkitServer.API.Cartography.Compositors;

/// <summary>
/// Exposes functions for post-processing on chart and satellite renders.
/// </summary>
/// <remarks>This will be initialized on load to collect the values of <see cref="SupportsSatellite"/> and <see cref="SupportsChart"/>.</remarks>
public interface ICartographyCompositor
{
    /// <summary>
    /// Is this <see cref="ICartographyCompositor"/> enabled for satellite renders?
    /// </summary>
    public bool SupportsSatellite { get; }

    /// <summary>
    /// Is this <see cref="ICartographyCompositor"/> enabled for chart renders?
    /// </summary>
    public bool SupportsChart { get; }

    /// <summary>
    /// Apply any compositing to a <see cref="RenderTexture"/>. The render texture is not created until it's first used.
    /// </summary>
    /// <param name="isExplicitlyDefined">If this compositor was explicitly requested by config.</param>
    /// <returns><see langword="true"/> if any changes were made, otherwise <see langword="false"/>.</returns>
    bool Composite(in CartographyCaptureData data, Lazy<RenderTexture> texture, bool isExplicitlyDefined);
}
#endif