namespace DevkitServer.API.Cartography;

/// <summary>
/// Distinguishes the two types of cartography workflows.
/// </summary>
public enum CartographyType
{
    /// <summary>
    /// No map, applicable in some usages.
    /// </summary>
    None,

    /// <summary>
    /// Top-down view of the map as-is. Also called GPS. Handled by <see cref="SatelliteCartography"/>.
    /// </summary>
    Satellite,

    /// <summary>
    /// Stylized 'drawing' of a top-down view of the map's roads, landmarks, and terrain. Handled by <see cref="ChartCartography"/>.
    /// </summary>
    Chart
}