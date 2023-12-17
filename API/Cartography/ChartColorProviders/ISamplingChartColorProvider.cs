namespace DevkitServer.API.Cartography.ChartColorProviders;

/// <summary>
/// Provides logic for converting world coordinates to chart colors.
/// </summary>
/// <remarks>Should handle interpolating sub-pixels internally.</remarks>
public interface ISamplingChartColorProvider : IChartColorProvider
{
    /// <summary>
    /// Sample a position on the chart in world coordinates. This method will be called for every pixel on the map image.
    /// </summary>
    Color32 SampleChartPosition(in CartographyCaptureData data, Vector2 worldCoordinates);
}
