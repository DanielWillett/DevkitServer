namespace DevkitServer.API.Cartography;

/// <summary>
/// Provides logic for converting world coordinates to chart colors.
/// </summary>
/// <remarks>Should handle interpolating sub-pixels internally.</remarks>
public interface IChartColorProvider
{
    /// <summary>
    /// Sample a position on the chart in world coordinates. This method will be called for every pixel on the map image.
    /// </summary>
    Color SampleChartPosition(in ChartCaptureData data, IChartColorProvider colorProvider, Vector2 worldCoordinates);
}