#if CLIENT
using SDG.Framework.Landscapes;
using SDG.Framework.Rendering;
using SDG.Framework.Utilities;

namespace DevkitServer.Util;
public static class DevkitServerGLUtility
{
    public static event GLRenderHandler? OnRenderAny;
    internal static void Init()
    {
        GLRenderer.render += OnRender;
        GLRenderer.OnGameRender += OnRender;
    }
    internal static void Shutdown()
    {
        GLRenderer.render -= OnRender;
        GLRenderer.OnGameRender -= OnRender;
    }
    private static void OnRender()
    {
        if (DevkitServerModule.IsEditing)
            OnRenderAny?.Invoke();
    }
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
    public static void BoxSolidIdentityMatrix(Vector3 center, Vector3 size, bool topOnly, bool quads)
    {
        float cx = center.x, cy = center.y, cz = center.z;
        float sx = size.x / 2f, sy = size.y / 2f, sz = size.z / 2f;
        if (!quads)
        {
            GL.Vertex3(cx - sx, cy + sy, cz + sz);
            GL.Vertex3(cx + sx, cy + sy, cz - sz);
            GL.Vertex3(cx - sx, cy + sy, cz - sz);

            GL.Vertex3(cx + sx, cy + sy, cz - sz);
            GL.Vertex3(cx - sx, cy + sy, cz + sz);
            GL.Vertex3(cx + sx, cy + sy, cz + sz);

            if (!topOnly)
            {
                GL.Vertex3(cx - sx, cy + sy, cz + sz);
                GL.Vertex3(cx + sx, cy + sy, cz - sz);
                GL.Vertex3(cx - sx, cy + sy, cz - sz);

                GL.Vertex3(cx + sx, cy + sy, cz - sz);
                GL.Vertex3(cx - sx, cy + sy, cz + sz);
                GL.Vertex3(cx + sx, cy + sy, cz + sz);

                GL.Vertex3(cx + sx, cy - sy, cz - sz);
                GL.Vertex3(cx + sx, cy + sy, cz - sz);
                GL.Vertex3(cx + sx, cy - sy, cz + sz);

                GL.Vertex3(cx + sx, cy + sy, cz + sz);
                GL.Vertex3(cx + sx, cy - sy, cz + sz);
                GL.Vertex3(cx + sx, cy + sy, cz - sz);

                GL.Vertex3(cx - sx, cy - sy, cz - sz);
                GL.Vertex3(cx - sx, cy + sy, cz - sz);
                GL.Vertex3(cx + sx, cy - sy, cz - sz);

                GL.Vertex3(cx + sx, cy + sy, cz - sz);
                GL.Vertex3(cx + sx, cy - sy, cz - sz);
                GL.Vertex3(cx - sx, cy + sy, cz - sz);

                GL.Vertex3(cx + sx, cy - sy, cz + sz);
                GL.Vertex3(cx - sx, cy + sy, cz + sz);
                GL.Vertex3(cx - sx, cy - sy, cz + sz);

                GL.Vertex3(cx - sx, cy + sy, cz + sz);
                GL.Vertex3(cx + sx, cy - sy, cz + sz);
                GL.Vertex3(cx + sx, cy + sy, cz + sz);
            }
        }
        else
        {
            GL.Vertex3(cx - sx, cy + sy, cz - sz);
            GL.Vertex3(cx + sx, cy + sy, cz - sz);
            GL.Vertex3(cx + sx, cy + sy, cz + sz);
            GL.Vertex3(cx - sx, cy + sy, cz + sz);

            if (!topOnly)
            {
                GL.Vertex3(cx + sx, cy + sy, cz - sz);
                GL.Vertex3(cx + sx, cy - sy, cz - sz);
                GL.Vertex3(cx + sx, cy - sy, cz + sz);
                GL.Vertex3(cx + sx, cy + sy, cz + sz);

                GL.Vertex3(cx - sx, cy + sy, cz - sz);
                GL.Vertex3(cx - sx, cy - sy, cz - sz);
                GL.Vertex3(cx - sx, cy - sy, cz + sz);
                GL.Vertex3(cx - sx, cy + sy, cz + sz);

                GL.Vertex3(cx - sx, cy + sy, cz + sz);
                GL.Vertex3(cx - sx, cy - sy, cz + sz);
                GL.Vertex3(cx + sx, cy - sy, cz + sz);
                GL.Vertex3(cx + sx, cy + sy, cz + sz);

                GL.Vertex3(cx - sx, cy + sy, cz - sz);
                GL.Vertex3(cx - sx, cy - sy, cz - sz);
                GL.Vertex3(cx + sx, cy - sy, cz - sz);
                GL.Vertex3(cx + sx, cy + sy, cz - sz);
            }
        }
    }
}
#endif