namespace DevkitServer.Util;
/// <summary>
/// Tools for converting between map image measurements and world measurements.
/// </summary>
public static class CartographyUtil
{
    private static CartographyData? _lvl;
    public static int ImageWidth => (_lvl ??= new CartographyData()).IntlMapImageWidth;
    public static int ImageHeight => (_lvl ??= new CartographyData()).IntlMapImageHeight;
    public static bool LegacyMapping => (_lvl ??= new CartographyData()).IntlLegacyMapping;
    public static Vector2 CaptureSize => (_lvl ??= new CartographyData()).IntlCaptureSize;
    public static Vector2 DistanceScale => (_lvl ??= new CartographyData()).IntlDistanceScale;
    public static Bounds CaptureBounds => (_lvl ??= new CartographyData()).IntlCaptureBounds;
    public static Matrix4x4 TransformMatrix => (_lvl ??= new CartographyData()).IntlTransformMatrix;
    public static Matrix4x4 TransformMatrixInverse => (_lvl ??= new CartographyData()).IntlTransformMatrixInverse;
    public static float WorldDistanceToMapDistanceX(float x)
    {
        _lvl ??= new CartographyData();

        return x / _lvl.IntlDistanceScale.x;
    }
    public static float WorldDistanceToMapDistanceY(float y)
    {
        _lvl ??= new CartographyData();

        return y / _lvl.IntlDistanceScale.x;
    }
    public static float MapDistanceToWorldDistanceX(float x)
    {
        _lvl ??= new CartographyData();

        return x * _lvl.IntlDistanceScale.x;
    }
    public static float MapDistanceToWorldDistanceY(float y)
    {
        _lvl ??= new CartographyData();

        return y * _lvl.IntlDistanceScale.x;
    }
    public static Vector2 WorldCoordsToMapCoords(Vector3 worldPos)
    {
        _lvl ??= new CartographyData();

        Vector3 n = new Vector3((worldPos.x / _lvl.IntlCaptureSize.x + 0.5f) * _lvl.IntlMapImageWidth, 0f, (worldPos.z / _lvl.IntlCaptureSize.y + 0.5f) * _lvl.IntlMapImageHeight);
        return _lvl.IntlTransformMatrixInverse.MultiplyPoint3x4(n);
    }
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
                IntlCaptureBounds = new Bounds(Vector3.zero, new Vector3(w, Level.HEIGHT, w));
            }
        }
    }
}
