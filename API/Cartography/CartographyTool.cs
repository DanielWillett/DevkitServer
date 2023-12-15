using SDG.Framework.Water;

namespace DevkitServer.API.Cartography;

/// <summary>
/// Tools for converting between map image coordinates and world coordinates.
/// </summary>
public static class CartographyTool
{
    private static CartographyData? _lvl;

    /// <summary>
    /// Width of the Map.png and Chart.png images.
    /// </summary>
    public static int ImageWidth => (_lvl ??= new CartographyData()).IntlMapImageWidth;

    /// <summary>
    /// Height of the Map.png and Chart.png images.
    /// </summary>
    public static int ImageHeight => (_lvl ??= new CartographyData()).IntlMapImageHeight;

    /// <summary>
    /// Is the map position/size based on <see cref="ELevelSize"/> instead of a <see cref="CartographyVolume"/>?
    /// </summary>
    public static bool LegacyMapping => (_lvl ??= new CartographyData()).IntlLegacyMapping;

    /// <summary>
    /// World size of the image captured.
    /// </summary>
    public static Vector2 CaptureSize => (_lvl ??= new CartographyData()).IntlCaptureSize;

    /// <summary>
    /// Value to divide world vectors by to get map coordinates and vice versa.
    /// </summary>
    public static Vector2 DistanceScale => (_lvl ??= new CartographyData()).IntlDistanceScale;

    /// <summary>
    /// World bounds of the image captured.
    /// </summary>
    public static Bounds CaptureBounds => (_lvl ??= new CartographyData()).IntlCaptureBounds;

    /// <summary>
    /// Matrix to scale map coordinates to world coordinates.
    /// </summary>
    public static Matrix4x4 TransformMatrix => (_lvl ??= new CartographyData()).IntlTransformMatrix;

    /// <summary>
    /// Matrix to scale world coordinates to map coordinates.
    /// </summary>
    public static Matrix4x4 TransformMatrixInverse => (_lvl ??= new CartographyData()).IntlTransformMatrixInverse;

    /// <summary>
    /// Converts a length in the X direction from world coordinates to map coordinates.
    /// </summary>
    public static float WorldDistanceToMapDistanceX(float x)
    {
        _lvl ??= new CartographyData();

        return x / _lvl.IntlDistanceScale.x;
    }

    /// <summary>
    /// Converts a length in the Y direction from world coordinates to map coordinates.
    /// </summary>
    public static float WorldDistanceToMapDistanceY(float y)
    {
        _lvl ??= new CartographyData();

        return y / _lvl.IntlDistanceScale.y;
    }

    /// <summary>
    /// Converts a length in the X direction from map coordinates to world coordinates.
    /// </summary>
    public static float MapDistanceToWorldDistanceX(float x)
    {
        _lvl ??= new CartographyData();

        return x * _lvl.IntlDistanceScale.x;
    }

    /// <summary>
    /// Converts a length in the Y direction from map coordinates to world coordinates.
    /// </summary>
    public static float MapDistanceToWorldDistanceY(float y)
    {
        _lvl ??= new CartographyData();

        return y * _lvl.IntlDistanceScale.y;
    }

    /// <summary>
    /// Converts a position from world coordinates to map coordinates.
    /// </summary>
    public static Vector2 WorldCoordsToMapCoords(Vector3 worldPos)
    {
        _lvl ??= new CartographyData();

        Vector3 n = new Vector3((worldPos.x / _lvl.IntlCaptureSize.x + 0.5f) * _lvl.IntlMapImageWidth, 0f, (worldPos.z / _lvl.IntlCaptureSize.y + 0.5f) * _lvl.IntlMapImageHeight);
        return _lvl.IntlTransformMatrixInverse.MultiplyPoint3x4(n);
    }

    /// <summary>
    /// Converts a position from map coordinates to world coordinates.
    /// </summary>
    public static Vector3 MapCoordsToWorldCoords(Vector2 mapPos)
    {
        _lvl ??= new CartographyData();

        Vector3 n = new Vector3((mapPos.x / _lvl.IntlMapImageWidth - 0.5f) * _lvl.IntlCaptureSize.x, (mapPos.y / _lvl.IntlMapImageHeight - 0.5f) * _lvl.IntlCaptureSize.y, 0f);
        return _lvl.IntlTransformMatrix.MultiplyPoint3x4(n);
    }

    internal static void Reset() => (_lvl ??= new CartographyData()).Calculate();
    private sealed class CartographyData
    {
        public bool IntlLegacyMapping;
        public Matrix4x4 IntlTransformMatrix;
        public Matrix4x4 IntlTransformMatrixInverse;
        public int IntlMapImageWidth;
        public int IntlMapImageHeight;
        public Vector2 IntlCaptureSize;
        public Vector2 IntlDistanceScale; // mult = map to world, div = world to map
        public Bounds IntlCaptureBounds;
        public CartographyData()
        {
            Calculate();
        }
        internal void Calculate()
        {
            CartographyVolumeManager? manager = CartographyVolumeManager.Get();
            if (manager == null)
                return;
            CartographyVolume vol = manager.GetMainVolume();
            if (vol != null)
            {
                IntlLegacyMapping = false;
                IntlTransformMatrix = Matrix4x4.TRS(vol.transform.position, vol.transform.rotation * Quaternion.Euler(90f, 0.0f, 0.0f), Vector3.one);
                IntlTransformMatrixInverse = IntlTransformMatrix.inverse;
                IntlCaptureBounds = vol.CalculateWorldBounds();
                Vector3 size = vol.CalculateLocalBounds().size;
                IntlMapImageWidth = Mathf.CeilToInt(size.x);
                IntlMapImageHeight = Mathf.CeilToInt(size.z);
                size = IntlCaptureBounds.size;
                IntlCaptureSize = new Vector2(size.x, size.z);
                IntlDistanceScale = new Vector2(IntlCaptureSize.x / IntlMapImageWidth, IntlCaptureSize.y / IntlMapImageHeight);
            }
            else
            {
                IntlLegacyMapping = true;
                IntlTransformMatrix = Matrix4x4.TRS(new Vector3(0.0f, 1028f, 0.0f), Quaternion.Euler(90f, 0.0f, 0.0f), Vector3.one);
                IntlTransformMatrixInverse = IntlTransformMatrix.inverse;
                ushort s = Level.size;
                float w = s - Level.border * 2f;
                IntlMapImageWidth = s;
                IntlMapImageHeight = s;
                IntlCaptureSize = new Vector2(w, w);
                IntlDistanceScale = new Vector2(w / s, w / s);
                float minHeight = WaterVolumeManager.worldSeaLevel;
                float maxHeight = Level.TERRAIN;
                IntlCaptureBounds = new Bounds(new Vector3(0, minHeight + (maxHeight - minHeight) / 2f, 0), new Vector3(w, maxHeight - minHeight, w));
            }
        }
    }
}
