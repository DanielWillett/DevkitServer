using System;
using System.Collections.Generic;
using System.Text;
using DevkitServer.API;
using DevkitServer.API.Cartography;

namespace DevkitServer.Patches;

[EarlyTypeInit]
internal static class CartographyPatches
{
    private static void BetterChartify()
    {
        int imgX = CartographyTool.ImageWidth;
        int imgY = CartographyTool.ImageHeight;


    }
}
