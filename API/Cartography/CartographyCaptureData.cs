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
    /// Full path to the file which drives the configuration for this compositing pipeline if a configuration file was used.
    /// </summary>
    public readonly string? ConfigurationFilePath;

    /// <summary>
    /// Map coordinates.
    /// </summary>
    public readonly Vector2Int ImageSize;

    /// <summary>
    /// Area of the image that is captured to.
    /// </summary>
    public readonly RectInt ImageCaptureArea;

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
    public readonly CartographyType Type;

    internal CartographyCaptureData(LevelInfo level, string outputPath, Vector2Int imageSize, Vector3 captureSize, Vector3 captureCenter, float seaLevel, CartographyType type, string? configurationFilePath, RectInt imageCaptureArea)
    {
        Level = level;
        OutputPath = outputPath;
        ImageSize = imageSize;
        CaptureSize = captureSize;
        CaptureCenter = captureCenter;
        MaxHeight = captureCenter.y + captureSize.y / 2f;
        MinHeight = captureCenter.y - captureSize.y / 2f;
        Type = type;
        ConfigurationFilePath = configurationFilePath;
        ImageCaptureArea = imageCaptureArea;
        SeaLevel = seaLevel;
    }
}
