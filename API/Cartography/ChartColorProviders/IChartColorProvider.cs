﻿namespace DevkitServer.API.Cartography.ChartColorProviders;

/// <summary>
/// Provides logic for converting world coordinates to chart colors.
/// </summary>
/// <remarks>Should handle interpolating sub-pixels internally.</remarks>
public interface IChartColorProvider
{
    /// <summary>
    /// Check for non-existant files or missing features to use this provider before moving on to the next one, or initialize any needed resources.
    /// </summary>
    /// <remarks><see cref="IDisposable"/> can be used for cleanup.</remarks>
    /// <returns><see langword="true"/> if this provider can be used, otherwise <see langword="false"/>.-</returns>
    bool TryInitialize(in CartographyCaptureData data);

    /// <summary>
    /// Sample a position on the chart in world coordinates. This method will be called for every pixel on the map image.
    /// </summary>
    Color32 SampleChartPosition(in CartographyCaptureData data, Vector2 worldCoordinates);
}