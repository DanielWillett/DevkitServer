using DevkitServer.Core.Cartography;

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
    /// <remarks>After hitting an object, good implementations will use <see cref="LevelCartographyConfigData.TryGetObjectChartOverride"/> and <see cref="LevelCartographyConfigData.TryGetRoadMaterialChartOverride"/> to apply chart overrides from config.</remarks>
    Color32 SampleChartPosition(in CartographyCaptureData data, LevelCartographyConfigData? config, Vector2 worldCoordinates);
}
