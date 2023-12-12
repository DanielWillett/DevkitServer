namespace DevkitServer.API.Cartography;

/// <summary>
/// Stores data for capturing a chart.
/// </summary>
public readonly struct ChartCaptureData
{
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
    /// 'Camera' that captures the chart.
    /// </summary>
    public readonly Transform CaptureOrigin;

    /// <summary>
    /// Max y value to capture.
    /// </summary>
    public readonly float MaxHeight;

    /// <summary>
    /// Min y value to capture.
    /// </summary>
    public readonly float MinHeight;

    internal ChartCaptureData(Vector2Int imageSize, Vector3 captureSize, Vector3 captureCenter, Transform captureOrigin)
    {
        ImageSize = imageSize;
        CaptureSize = captureSize;
        CaptureCenter = captureCenter;
        CaptureOrigin = captureOrigin;
        MaxHeight = captureCenter.y + captureSize.y / 2f;
        MinHeight = captureCenter.y - captureSize.y / 2f;
    }
}
