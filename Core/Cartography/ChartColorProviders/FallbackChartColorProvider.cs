using DevkitServer.API.Cartography;
using DevkitServer.API.Cartography.ChartColorProviders;
using SDG.Framework.Landscapes;

namespace DevkitServer.Core.Cartography.ChartColorProviders;

/// <summary>
/// Provides the Washington default chart colors.
/// </summary>
internal class FallbackChartColorProvider : RaycastChartColorProvider
{
    private Color32[] _heightPixels = null!;
    private Color32[] _layerPixels = null!;

    /// <summary>
    /// Except for water at [0] this defines a gradient from pixel [1] to the end of the array.
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
    /// Any objects/resources with custom 'Chart' values.
    /// </para>
    /// </summary>
    public Color32[] LayerPixels
    {
        get => _layerPixels;
        protected set => _layerPixels = value;
    }

    public override bool TryInitialize(in CartographyCaptureData data, bool isExplicitlyDefined)
    {
        _heightPixels = new Color32[32];
        _layerPixels = new Color32[32];

        _heightPixels[0] = new Color32(48, 90, 89, 255);
        _heightPixels[1] = new Color32(119, 119, 119, 255);
        _heightPixels.AsSpan(2, 4).Fill(new Color32(85, 113, 78, 255));
        _heightPixels.AsSpan(6, 4).Fill(new Color32(90, 119, 82, 255));
        _heightPixels.AsSpan(10, 4).Fill(new Color32(94, 125, 86, 255));
        _heightPixels.AsSpan(14, 3).Fill(new Color32(100, 132, 91, 255));
        _heightPixels.AsSpan(17, 4).Fill(new Color32(102, 135, 92, 255));
        _heightPixels.AsSpan(21, 4).Fill(new Color32(109, 144, 99, 255));
        _heightPixels.AsSpan(25, 4).Fill(new Color32(114, 151, 104, 255));
        _heightPixels.AsSpan(29, 3).Fill(new Color32(126, 160, 117, 255));

        _layerPixels[0] = new Color32(227, 119, 40, 255);
        _layerPixels[1] = new Color32(217, 162, 54, 255);
        _layerPixels[2] = new Color32(191, 191, 191, 255);
        _layerPixels[3] = new Color32(143, 132, 111, 255);
        _layerPixels[4] = new Color32(127, 127, 127, 255);
        _layerPixels[14] = new Color32(94, 74, 54, 255);
        _layerPixels[15] = new Color32(90, 90, 90, 255);
        _layerPixels[16] = new Color32(120, 120, 120, 255);
        _layerPixels.AsSpan(5, 9).Fill(new Color32(255, 0, 255, 255));
        _layerPixels.AsSpan(17, 15).Fill(new Color32(255, 0, 255, 255));

        return base.TryInitialize(in data, isExplicitlyDefined);
    }

    /// <inheritdoc />
    public override Color32 GetColor(in CartographyCaptureData data, EObjectChart chartType, Transform? transform, int layer, ref RaycastHit hit)
    {
        if (chartType == EObjectChart.WATER)
            return _heightPixels[0];

        if (chartType == EObjectChart.GROUND)
        {
            Vector3 terrainPoint = hit.point;
            if (layer == LayerMasks.GROUND) // otherwise it hit a 'Chart GROUND' object or resource.
                Landscape.getWorldHeight(terrainPoint, out terrainPoint.y);

            if (IsPointUnderwaterFast(terrainPoint))
                return _heightPixels[0];

            float heightAlpha = Mathf.InverseLerp(data.MinHeight, data.MaxHeight, terrainPoint.y);
            return _heightPixels[(int)(heightAlpha * (_heightPixels.Length - 1) + 1)];
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

        return _layerPixels[layer];
    }
}
