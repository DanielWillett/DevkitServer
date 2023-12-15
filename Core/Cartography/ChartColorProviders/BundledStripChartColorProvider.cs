using DevkitServer.API;
using DevkitServer.API.Cartography;
using DevkitServer.API.Cartography.ChartColorProviders;
using SDG.Framework.Landscapes;
using SDG.Framework.Water;

namespace DevkitServer.Core.Cartography.ChartColorProviders;

/// <summary>
/// The vanilla method of getting chart colors. Loads Height_Strip and Layer_Strip from the Charts.unity3d bundle.
/// </summary>
[LoadPriority(-2)]
public class BundledStripChartColorProvider : SimpleChartColorProvider
{
    private Color32[] _heightPixels = null!;
    private Color32[] _layerPixels = null!;

    /// <summary>
    /// Except for water at [0] this defines a gradient from pixel [1] to the length of the array.
    /// </summary>
    public Color32[] HeightPixels
    {
        get => _heightPixels;
        protected set => _heightPixels = value;
    }

    /// <summary>
    /// 32 element array which corresponds to the layers in <see cref="LayerMasks"/>.
    /// <para>
    /// Exceptions:<br/>
    /// [0] = Highways (width &gt;= 8m, concrete)<br/>
    /// [1] = Roads (width &lt; 8m, concrete)<br/>
    /// [2] = Object roads (like city blocks)<br/>
    /// [3] = Paths (not concrete)<br/>
    /// [4] = Cliffs<br/>
    /// </para>
    /// </summary>
    public Color32[] LayerPixels
    {
        get => _layerPixels;
        protected set => _layerPixels = value;
    }

    public override bool TryInitialize(in CartographyCaptureData data)
    {
        string path = Path.Combine(data.Level.path, "Charts.unity3d");

        if (!File.Exists(path))
        {
            Logger.LogDebug($"[{nameof(BundledStripChartColorProvider)}] Skipping because there is no chart data at {path.Format()}.");
            return false;
        }

        Bundle? bundle = Bundles.getBundle(path, false);

        if (bundle == null)
        {
            Logger.LogWarning($"Unable to find chart bundle at {path.Format()}.", method: nameof(BundledStripChartColorProvider));
            return false;
        }

        Texture2D heightStrip = bundle.load<Texture2D>("Height_Strip");
        Texture2D layerStrip = bundle.load<Texture2D>("Layer_Strip");
        bundle.unload();

        if (heightStrip == null || heightStrip.width < 2)
        {
            Logger.LogWarning($"Unable to find valid Height_Strip image in chart bundle at {path.Format()}. Using {"Washington".Colorize(DevkitServerModule.UnturnedColor)} defaults.", method: nameof(BundledStripChartColorProvider));
            _heightPixels = new Color32[32];
            _heightPixels[0] = new Color32(48, 90, 89, 255);
            _heightPixels[1] = new Color32(119, 119, 119, 255);
            for (int i = 2; i < 6; ++i)
                _heightPixels[i] = new Color32(85, 113, 78, 255);
            for (int i = 6; i < 10; ++i)
                _heightPixels[i] = new Color32(90, 119, 82, 255);
            for (int i = 6; i < 10; ++i)
                _heightPixels[i] = new Color32(90, 119, 82, 255);
            for (int i = 10; i < 14; ++i)
                _heightPixels[i] = new Color32(94, 125, 86, 255);
            for (int i = 14; i < 17; ++i)
                _heightPixels[i] = new Color32(100, 132, 91, 255);
            for (int i = 17; i < 21; ++i)
                _heightPixels[i] = new Color32(102, 135, 92, 255);
            for (int i = 21; i < 25; ++i)
                _heightPixels[i] = new Color32(109, 144, 99, 255);
            for (int i = 25; i < 29; ++i)
                _heightPixels[i] = new Color32(114, 151, 104, 255);
            for (int i = 29; i < 32; ++i)
                _heightPixels[i] = new Color32(126, 160, 117, 255);
        }
        else
        {
            Color[] heights = heightStrip.GetPixels(0, 0, heightStrip.width, 1);
            Object.DestroyImmediate(heightStrip);

            _heightPixels = new Color32[heights.Length];
            for (int i = 0; i < heights.Length; ++i)
                _heightPixels[i] = heights[i];
        }

        if (layerStrip == null)
        {
            _layerPixels = new Color32[32];
            Logger.LogWarning($"Unable to find Layer_Strip image in chart bundle at {path.Format()}. Using {"Washington".Colorize(DevkitServerModule.UnturnedColor)} defaults.", method: nameof(BundledStripChartColorProvider));

            _layerPixels[0] = new Color32(227, 119, 40, 255);
            _layerPixels[1] = new Color32(217, 162, 54, 255);
            _layerPixels[2] = new Color32(191, 191, 191, 255);
            _layerPixels[3] = new Color32(143, 132, 111, 255);
            _layerPixels[4] = new Color32(127, 127, 127, 255);
            _layerPixels[14] = new Color32(94, 74, 54, 255);
            _layerPixels[15] = new Color32(90, 90, 90, 255);
            _layerPixels[16] = new Color32(120, 120, 120, 255);

            for (int i = 5; i < 14; ++i)
                _layerPixels[i] = new Color32(255, 0, 255, 255);

            for (int i = 17; i < 32; ++i)
                _layerPixels[i] = new Color32(255, 0, 255, 255);

            return base.TryInitialize(in data);
        }

        Color[] layers = layerStrip.GetPixels(0, 0, layerStrip.width, 1);
        Object.DestroyImmediate(layerStrip);

        _layerPixels = new Color32[32];
        for (int i = 0; i < 32; ++i)
            _layerPixels[i] = i >= layers.Length ? new Color32(255, 0, 255, 255) : layers[i];

        return base.TryInitialize(in data);
    }
    public override Color32 GetColor(in CartographyCaptureData data, EObjectChart chartType, Transform? transform, int layer, ref RaycastHit hit)
    {
        if (chartType == EObjectChart.WATER)
            return HeightPixels[0];

        if (chartType == EObjectChart.GROUND)
        {
            Vector3 terrainPoint = hit.point;
            if (layer == LayerMasks.GROUND) // otherwise it hit a Chart GROUND object.
                Landscape.getWorldHeight(terrainPoint, out terrainPoint.y);

            if (IsPointUnderwaterFast(terrainPoint))
                return HeightPixels[0];

            float heightAlpha = Mathf.InverseLerp(data.MinHeight, data.MaxHeight, terrainPoint.y);
            return HeightPixels[(int)(heightAlpha * (HeightPixels.Length - 1) + 1)];
        }

        layer = chartType switch
        {
            EObjectChart.HIGHWAY => 0,
            EObjectChart.ROAD => 1,
            EObjectChart.STREET => 2,
            EObjectChart.PATH => 3,
            EObjectChart.CLIFF => 4,
            EObjectChart.LARGE => LayerMasks.LARGE,
            EObjectChart.MEDIUM => LayerMasks.MEDIUM,
            _ => layer
        };

        return LayerPixels[layer];
    }
}
