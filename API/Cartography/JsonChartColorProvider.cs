using System;
using System.Collections.Generic;
using System.Text;

namespace DevkitServer.API.Cartography;
public class JsonChartColorProvider : SimpleChartColorProvider
{
    public override Color GetColor(in ChartCaptureData data, EObjectChart chartType, Transform? transform, int layer, ref RaycastHit hit)
    {

    }
}
