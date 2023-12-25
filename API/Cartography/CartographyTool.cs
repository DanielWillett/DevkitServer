using SDG.Framework.Water;

namespace DevkitServer.API.Cartography;

/// <summary>
/// Tools for converting between map image coordinates and world coordinates.
/// </summary>
[EarlyTypeInit]
public static class CartographyTool
{
    private static readonly Func<object>? CallGetObjectState = Accessor.GenerateStaticCaller<Level, Func<object>>("GetObjectState", allowUnsafeTypeBinding: true);
    private static readonly Action<object>? CallRestorePreCaptureState = Accessor.GenerateStaticCaller<Level, Action<object>>("RestorePreCaptureState", allowUnsafeTypeBinding: false);

    private static CartographyData? _lvl;

    /// <summary>
    /// Calls <see cref="Level.GetObjectState"/> which saves the visability of objects, resources, etc then makes them all visible.
    /// </summary>
    /// <remarks>Load the state with <see cref="RestorePreCaptureState"/>.</remarks>
    /// <returns>A visibility state, or <see langword="null"/> in the case of a reflection failure.</returns>
    public static object? SavePreCaptureState() => CallGetObjectState?.Invoke();

    /// <summary>
    /// Calls <see cref="Level.RestorePreCaptureState"/> which restores the visability of objects, resources, etc.
    /// </summary>
    /// <remarks>Save the state with <see cref="SavePreCaptureState"/>.</remarks>
    /// <returns><see langword="true"/> if the capture state was valid and there wasn't a reflection failure, otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="preCaptureState"/> was not of type <see cref="Level.PreCaptureObjectState"/>.</exception>
    public static bool RestorePreCaptureState(object? preCaptureState)
    {
        if (preCaptureState == null || CallRestorePreCaptureState == null)
            return false;

        try
        {
            CallRestorePreCaptureState(preCaptureState);
        }
        catch (InvalidCastException ex)
        {
            throw new ArgumentException("State must be of type Level.PreCaptureObjectState.", nameof(preCaptureState), ex);
        }
        return true;
    }

    /// <summary>
    /// Size of the Map.png and Chart.png images in pixels.
    /// </summary>
    public static Vector2Int ImageSize => (_lvl ??= new CartographyData()).IntlMapImageSize;

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

        Vector3 n = new Vector3((worldPos.x / _lvl.IntlCaptureSize.x + 0.5f) * _lvl.IntlMapImageSize.x, 0f, (worldPos.z / _lvl.IntlCaptureSize.y + 0.5f) * _lvl.IntlMapImageSize.y);
        return _lvl.IntlTransformMatrixInverse.MultiplyPoint3x4(n);
    }

    /// <summary>
    /// Converts a position from map coordinates to world coordinates.
    /// </summary>
    public static Vector3 MapCoordsToWorldCoords(Vector2 mapPos)
    {
        _lvl ??= new CartographyData();

        Vector3 n = new Vector3((mapPos.x / _lvl.IntlMapImageSize.x - 0.5f) * _lvl.IntlCaptureSize.x, (mapPos.y / _lvl.IntlMapImageSize.y - 0.5f) * _lvl.IntlCaptureSize.y, 0f);
        return _lvl.IntlTransformMatrix.MultiplyPoint3x4(n);
    }

    /// <summary>
    /// Considering <see cref="SystemInfo.maxTextureSize"/>, returns the desirable size for a satellite or chart image.
    /// </summary>
    /// <param name="wasSizeOutOfBounds">Was the return value of this function clamped?</param>
    public static Vector2Int GetImageSizeCheckMaxTextureSize(out bool wasSizeOutOfBounds)
    {
        Vector2Int imgSize = ImageSize;

        int maxTextureSize = DevkitServerUtility.MaxTextureDimensionSize;

        if (imgSize.x > maxTextureSize || imgSize.y > maxTextureSize)
        {
            double aspect = (double)imgSize.x / imgSize.y;
            double x, y;
            if (imgSize.x <= imgSize.y)
            {
                x = aspect * maxTextureSize;
                y = x / aspect;
            }
            else
            {
                y = maxTextureSize / aspect;
                x = aspect * y;
            }

            imgSize.x = (int)Math.Round(x);
            imgSize.y = (int)Math.Round(y);
            wasSizeOutOfBounds = true;
        }
        else
            wasSizeOutOfBounds = false;

        return imgSize;
    }

    /// <summary>
    /// Considering <see cref="SystemInfo.maxTextureSize"/>, returns the desirable size for a satellite or chart image with extra info for satellite supersampling.
    /// </summary>
    /// <param name="superSampleSize">Used for satellite renders, returns the size for the supersampled texture (which will usually be double the return value) before it's scaled down.</param>
    /// <param name="wasSizeOutOfBounds">Was the return value of this function and <paramref name="superSampleSize"/> clamped?</param>
    /// <param name="wasSuperSampleOutOfBounds">Was <paramref name="wasSuperSampleOutOfBounds"/> clamped?</param>
    public static Vector2Int GetImageSizeCheckMaxTextureSize(out Vector2Int superSampleSize, out bool wasSizeOutOfBounds, out bool wasSuperSampleOutOfBounds)
    {
        Vector2Int imgSize = ImageSize;

        int superSampleX = imgSize.x * 2, superSampleY = imgSize.y * 2;

        int maxTextureSize = DevkitServerUtility.MaxTextureDimensionSize;

        if (imgSize.x > maxTextureSize || imgSize.y > maxTextureSize)
        {
            double aspect = (double)imgSize.x / imgSize.y;
            double x, y;
            if (imgSize.x <= imgSize.y)
            {
                x = aspect * maxTextureSize;
                y = x / aspect;
            }
            else
            {
                y = maxTextureSize / aspect;
                x = aspect * y;
            }

            imgSize.x = superSampleX = (int)Math.Round(x);
            imgSize.y = superSampleY = (int)Math.Round(y);
            wasSizeOutOfBounds = wasSuperSampleOutOfBounds = true;
        }
        else if (superSampleX > maxTextureSize || superSampleY > maxTextureSize)
        {
            double aspect = (double)superSampleX / superSampleY;
            double x, y;
            if (superSampleX <= superSampleY)
            {
                x = aspect * maxTextureSize;
                y = x / aspect;
            }
            else
            {
                y = maxTextureSize / aspect;
                x = aspect * y;
            }

            superSampleX = (int)Math.Round(x);
            superSampleY = (int)Math.Round(y);
            wasSuperSampleOutOfBounds = true;
            wasSizeOutOfBounds = false;
        }
        else
        {
            wasSuperSampleOutOfBounds = false;
            wasSizeOutOfBounds = false;
        }

        superSampleSize = new Vector2Int(superSampleX, superSampleY);
        return imgSize;
    }

    // todo call this when a cartography volume is updated (moved, added, deleted, etc).
    internal static void Reset() => _lvl = null;
    private sealed class CartographyData
    {
        public bool IntlLegacyMapping;
        public Matrix4x4 IntlTransformMatrix;
        public Matrix4x4 IntlTransformMatrixInverse;
        public Vector2Int IntlMapImageSize;
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
                IntlMapImageSize = new Vector2Int(Mathf.CeilToInt(size.x), Mathf.CeilToInt(size.z));
                size = IntlCaptureBounds.size;
                IntlCaptureSize = new Vector2(size.x, size.z);
                IntlDistanceScale = new Vector2(IntlCaptureSize.x / IntlMapImageSize.x, IntlCaptureSize.y / IntlMapImageSize.y);
            }
            else
            {
                IntlLegacyMapping = true;
                IntlTransformMatrix = Matrix4x4.TRS(new Vector3(0.0f, 1028f, 0.0f), Quaternion.Euler(90f, 0.0f, 0.0f), Vector3.one);
                IntlTransformMatrixInverse = IntlTransformMatrix.inverse;
                ushort s = Level.size;
                float w = s - Level.border * 2f;
                IntlMapImageSize = new Vector2Int(s, s);
                IntlCaptureSize = new Vector2(w, w);
                IntlDistanceScale = new Vector2(w / s, w / s);
                float minHeight = WaterVolumeManager.worldSeaLevel;
                float maxHeight = Level.TERRAIN;
                IntlCaptureBounds = new Bounds(new Vector3(0, minHeight + (maxHeight - minHeight) / 2f, 0), new Vector3(w, maxHeight - minHeight, w));
            }
        }
    }
}
