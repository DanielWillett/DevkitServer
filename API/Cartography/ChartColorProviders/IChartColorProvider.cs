using System.Text.Json;

namespace DevkitServer.API.Cartography.ChartColorProviders;

/// <summary>
/// Provides handling for capturing a chart image.
/// </summary>
/// <remarks>Use <see cref="ISamplingChartColorProvider"/> for providers that enumerate every pixel on the chart.</remarks>
public interface IChartColorProvider
{
    /// <summary>
    /// Check for non-existant files or missing features to use this provider before moving on to the next one, or initialize any needed resources.
    /// </summary>
    /// <param name="isExplicitlyDefined">If this provider was explicitly requested by config.</param>
    /// <param name="configuration">Optionally supplied config. Check that <see cref="JsonElement.ValueKind"/> is not <see cref="JsonValueKind.Undefined"/>.</param>
    /// <remarks><see cref="IDisposable"/> can be used for cleanup.</remarks>
    /// <returns><see langword="true"/> if this provider can be used, otherwise <see langword="false"/>.</returns>
    bool TryInitialize(in CartographyCaptureData data, JsonElement configuration, bool isExplicitlyDefined);
}