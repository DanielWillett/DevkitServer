﻿#if CLIENT
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DevkitServer.Multiplayer;
using SDG.Framework.Landscapes;
using SDG.Framework.Rendering;
using SDG.Framework.Utilities;

namespace DevkitServer.Util;
public static class DevkitServerGLUtility
{
    public static void DrawTerrainBounds(LandscapeCoord tile, int xMin, int xMax, int yMin, int yMax, bool splatmap, Color? constColor = null)
    {
        GLUtility.matrix = MathUtility.IDENTITY_MATRIX;
        GLUtility.LINE_FLAT_COLOR.SetPass(0);
        GL.Begin(GL.LINES);
        for (int x = xMin; x < xMax; ++x)
        {
            GL.Color(constColor ?? Color.Lerp(Color.black, Color.red, (x - xMin) / (float)(xMax - xMin)));
            if (!splatmap)
                GLUtility.line(new HeightmapCoord(x, yMin).GetWorldPosition(tile), new HeightmapCoord(x + 1, yMin).GetWorldPosition(tile));
            else
                GLUtility.line(new SplatmapCoord(x, yMin).GetWorldPosition(tile), new SplatmapCoord(x + 1, yMin).GetWorldPosition(tile));
        }
        for (int y = yMin; y < yMax; ++y)
        {
            GL.Color(constColor ?? Color.Lerp(Color.black, Color.red, (y - yMin) / (float)(yMax - yMin)));
            if (!splatmap)
                GLUtility.line(new HeightmapCoord(xMax, y).GetWorldPosition(tile), new HeightmapCoord(xMax, y + 1).GetWorldPosition(tile));
            else
                GLUtility.line(new SplatmapCoord(xMax, y).GetWorldPosition(tile), new SplatmapCoord(xMax, y + 1).GetWorldPosition(tile));
        }
        for (int x = xMax; x > xMin; --x)
        {
            GL.Color(constColor ?? Color.Lerp(Color.black, Color.red, (x - xMin) / (float)(xMax - xMin)));
            if (!splatmap)
                GLUtility.line(new HeightmapCoord(x, yMax).GetWorldPosition(tile), new HeightmapCoord(x - 1, yMax).GetWorldPosition(tile));
            else
                GLUtility.line(new SplatmapCoord(x, yMax).GetWorldPosition(tile), new SplatmapCoord(x - 1, yMax).GetWorldPosition(tile));
        }
        for (int y = yMax; y > yMin; --y)
        {
            GL.Color(constColor ?? Color.Lerp(Color.black, Color.red, (y - yMin) / (float)(yMax - yMin)));
            if (!splatmap)
                GLUtility.line(new HeightmapCoord(xMin, y).GetWorldPosition(tile), new HeightmapCoord(xMin, y - 1).GetWorldPosition(tile));
            else
                GLUtility.line(new SplatmapCoord(xMin, y).GetWorldPosition(tile), new SplatmapCoord(xMin, y - 1).GetWorldPosition(tile));
        }
        GL.End();
    }
}
#endif