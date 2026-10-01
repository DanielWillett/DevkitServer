using SDG.Framework.Water;

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
    public readonly Vector2Int TextureSize;

    /// <summary>
    /// Area of the map coordinates to be captured.
    /// </summary>
    public readonly Rect ImageCaptureArea;

    /// <summary>
    /// The area on the texture where the map should be written.
    /// </summary>
    public readonly RectInt ImageWriteArea;

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

    /// <summary>
    /// X and Y scale multiplied by image pixel coordinates to get the corresponding world coordinate (before map transformations).
    /// </summary>
    /// <remarks>Without any size overrides this is <c>1, 1</c>, even with cartography volumes.</remarks>
    public readonly Vector2 CaptureScale;

    internal CartographyCaptureData(
        LevelInfo level,
        string outputPath,
        Vector2Int textureSize,
        Vector3 captureSize,
        Vector3 captureCenter,
        CartographyType type,
        string? configurationFilePath,
        Rect imageCaptureArea,
        RectInt imageWriteArea
    )
    {
        Level = level;
        OutputPath = outputPath;
        TextureSize = textureSize;
        CaptureSize = captureSize;
        CaptureCenter = captureCenter;
        MaxHeight = captureCenter.y + captureSize.y / 2f;
        MinHeight = captureCenter.y - captureSize.y / 2f;
        Type = type;
        ConfigurationFilePath = configurationFilePath;
        ImageCaptureArea = imageCaptureArea;
        CaptureScale = new Vector2(imageCaptureArea.width / imageWriteArea.width, imageCaptureArea.height / imageWriteArea.height);
        SeaLevel = WaterVolumeManager.worldSeaLevel;
        ImageWriteArea = imageWriteArea;
    }
}
