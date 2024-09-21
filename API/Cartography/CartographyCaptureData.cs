namespace DevkitServer.API.Cartography;

/// <summary>
/// Stores data for capturing a chart.
/// </summary>
public readonly ref struct CartographyCaptureData
{
    /// <summary>
    /// Level being charted.
    /// </summary>
    public readonly LevelInfo Level;

    /// <summary>
    /// Full path to the output file after rendering.
    /// </summary>
    public readonly string OutputPath;

    /// <summary>
    /// Map coordinates.
    /// </summary>
    public readonly Vector2Int ImageSize;

    /// <summary>
    /// World coordinates, size of the bounds to capture.
    /// </summary>
    public readonly Vector3 CaptureSize;

    /// <summary>
    /// World coordinates, center of the bounds to capture.
    /// </summary>
    public readonly Vector3 CaptureCenter;

    /// <summary>
    /// Max y value to capture.
    /// </summary>
    public readonly float MaxHeight;

    /// <summary>
    /// Min y value to capture.
    /// </summary>
    public readonly float MinHeight;

    /// <summary>
    /// Min y value for coloring terrain.
    /// </summary>
    public readonly float SeaLevel;

    /// <summary>
    /// Is this a chart or a satellite render?
    /// </summary>
    public readonly bool IsChart;

    internal CartographyCaptureData(LevelInfo level, string outputPath, Vector2Int imageSize, Vector3 captureSize, Vector3 captureCenter, float seaLevel, bool isChart)
    {
        Level = level;
        OutputPath = outputPath;
        ImageSize = imageSize;
        CaptureSize = captureSize;
        CaptureCenter = captureCenter;
        MaxHeight = captureCenter.y + captureSize.y / 2f;
        MinHeight = captureCenter.y - captureSize.y / 2f;
        IsChart = isChart;
        SeaLevel = seaLevel;
    }
}
