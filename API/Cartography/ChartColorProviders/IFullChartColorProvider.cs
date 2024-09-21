using System.Diagnostics;
using DevkitServer.Core.Cartography;

namespace DevkitServer.API.Cartography.ChartColorProviders;

/// <summary>
/// Provides handling for capturing a chart image where all chart logic is handled by the provider.
/// </summary>
public interface IFullChartColorProvider : IChartColorProvider
{
    /// <summary>
    /// Capture the entire chart and write any output to the pointer <paramref name="rawRgb24Data"/>.
    /// <para>Start index of the 3 RGB bytes can be calculated by <c>(pixelX + pixelY * data.ImageSize.x) * 3</c>.</para>
    /// </summary>
    /// <param name="rawRgb24Data">Output pointer for raw RGB24 image data.</param>
    /// <remarks>After hitting an object, good implementations will use <see cref="LevelCartographyConfigData.TryGetObjectChartOverride"/> and <see cref="LevelCartographyConfigData.TryGetRoadMaterialChartOverride"/> to apply chart overrides from config.</remarks>
    unsafe void CaptureChart(in CartographyCaptureData data, LevelCartographyConfigData? config, byte* rawRgb24Data, Stopwatch jobStopwatch);
}
