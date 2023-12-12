using SDG.Framework.Water;

namespace DevkitServer.API.Cartography;
public class BundledStripChartColorProvider : SimpleChartColorProvider
{
    private readonly Color[] _heightPixels;
    private readonly Color[] _layerPixels;
    public BundledStripChartColorProvider() : this(Path.Combine(Level.info.path, "Charts.unity3d")) { }
    public BundledStripChartColorProvider(string bundlePath)
    {
        Bundle? bundle = Bundles.getBundle(bundlePath, false);

        if (bundle == null)
        {
            Logger.LogWarning("Unable to find Charts.unity3d bundle.", method: "RENDER CHART");
            _layerPixels = new Color[32];
            _heightPixels = [ Color.blue, default ];
            return;
        }

        Texture2D heightStrip = bundle.load<Texture2D>("Height_Strip");
        Texture2D layerStrip = bundle.load<Texture2D>("Layer_Strip");
        bundle.unload();

        if (heightStrip == null)
        {
            Logger.LogWarning("Unable to find Height_Strip image in Charts.unity3d bundle.", method: "RENDER CHART");
            _heightPixels = [ Color.blue, default ];
        }
        else
        {
            _heightPixels = new Color[heightStrip.width];
            for (int i = 0; i < _heightPixels.Length; ++i)
                _heightPixels[i] = heightStrip.GetPixel(i, 0);

            Object.Destroy(heightStrip);
        }

        _layerPixels = new Color[32];
        if (layerStrip == null)
        {
            Logger.LogWarning("Unable to find Layer_Strip image in Charts.unity3d bundle.", method: "RENDER CHART");
            return;
        }

        int size = Math.Min(_heightPixels.Length, layerStrip.width);
        for (int i = 0; i < size; ++i)
            _layerPixels[i] = layerStrip.GetPixel(i, 0);

        Object.Destroy(layerStrip);
    }
    public override Color GetColor(in ChartCaptureData data, EObjectChart chartType, Transform? transform, int layer, ref RaycastHit hit)
    {
        layer = chartType switch
        {
            EObjectChart.GROUND => LayerMasks.GROUND,
            EObjectChart.HIGHWAY => 0,
            EObjectChart.ROAD => 1,
            EObjectChart.STREET => 2,
            EObjectChart.PATH => 3,
            EObjectChart.CLIFF => 4,
            EObjectChart.LARGE => LayerMasks.LARGE,
            EObjectChart.MEDIUM => LayerMasks.MEDIUM,
            _ => layer
        };

        if (layer == LayerMasks.ENVIRONMENT)
            layer = GetRoadLayer(transform!);

        if (chartType == EObjectChart.WATER)
            return _heightPixels[0];

        if (layer != LayerMasks.GROUND)
            return _layerPixels[layer];

        Vector3 terrainPoint = hit.point with { y = LevelGround.getHeight(hit.point) };
        if (WaterUtility.isPointUnderwater(terrainPoint))
            return _heightPixels[0];

        float heightAlpha = Mathf.InverseLerp(data.MinHeight, data.MaxHeight, terrainPoint.y);
        return _heightPixels[(int)(heightAlpha * (_heightPixels.Length - 1) + 1)];
    }
}
