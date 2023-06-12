#if CLIENT
using System.Globalization;
using DevkitServer.Players;
using JetBrains.Annotations;
using SDG.Framework.Landscapes;
using SDG.Framework.Rendering;

namespace DevkitServer.Util.Debugging;
internal sealed class RegionDebug : MonoBehaviour
{
    private static bool _tilesEnabled = true;
    private static bool _regionsEnabled = true;
    private static Vector3[,]? _tileCorners;
    private static float _avgLineHeight;
    internal static bool TilesEnabled
    {
        get => _tilesEnabled;
        set
        {
            if (_tilesEnabled == value) return;
            _tilesEnabled = value;
            if (value && !_regionsEnabled)
                DevkitServerGLUtility.OnRenderAny += HandleGLRender;
            else if (!_regionsEnabled)
                DevkitServerGLUtility.OnRenderAny -= HandleGLRender;
        }
    }
    internal static bool RegionsEnabled
    {
        get => _regionsEnabled;
        set
        {
            if (_regionsEnabled == value) return;
            _regionsEnabled = value;
            if (value && !_tilesEnabled)
                DevkitServerGLUtility.OnRenderAny += HandleGLRender;
            else if (!_tilesEnabled)
                DevkitServerGLUtility.OnRenderAny -= HandleGLRender;
        }
    }

    [UsedImplicitly]
    private void Start()
    {
        if (_tilesEnabled || _regionsEnabled)
            DevkitServerGLUtility.OnRenderAny += HandleGLRender;
    }
    [UsedImplicitly]
    private void OnDestroy()
    {
        if (_tilesEnabled || _regionsEnabled)
            DevkitServerGLUtility.OnRenderAny -= HandleGLRender;
    }
    [UsedImplicitly]
    private void OnGUI()
    {
        if (!_tilesEnabled && !_regionsEnabled) return;
        if (!DevkitServerModule.IsEditing || EditorUser.User == null || EditorUser.User.Input == null || EditorUser.User.Input.ControllerObject == null)
            return;
        GUI.Label(new Rect(5, 80f, 1024f, 20f), "Position: " + EditorUser.User.Input.ControllerObject.transform.position.ToString("F2"));
        float y = 80f;
        Vector3 position = EditorUser.User.Input.ControllerObject.transform.position;
        if (_tilesEnabled)
        {
            if (Landscape.getTile(position) is { } tile)
                GUI.Label(new Rect(5, y += 25f, 1024f, 20f), "Tile: " + tile.coord);
        }

        if (_regionsEnabled)
        {
            if (Regions.tryGetCoordinate(position, out byte x2, out byte y2))
                GUI.Label(new Rect(5, y += 25f, 1024f, 20f), "Region: (" + x2.ToString(CultureInfo.InvariantCulture) + ", " + y2.ToString(CultureInfo.InvariantCulture) + ")");
        }
    }
    
    private static void HandleGLRender()
    {
        if (UserInput.LocalController != CameraController.Editor) return;
        GLUtility.matrix = Matrix4x4.identity;
        GLUtility.LINE_FLAT_COLOR.SetPass(0);
        GL.Begin(GL.LINES);
        float avgLineHeight = _avgLineHeight;
        int index = -1;
        IReadOnlyCollection<LandscapeTile> tiles = LandscapeUtil.EnumerateAllTiles();
        if (_tileCorners == null || _tileCorners.GetLength(0) != tiles.Count)
        {
            float y = 0;
            _tileCorners = new Vector3[tiles.Count, 4];
            foreach (LandscapeTile tile in tiles)
            {
                GetCornersTile(tile, out Vector3 corner1, out Vector3 corner2, out Vector3 corner3, out Vector3 corner4);
                _tileCorners[++index, 0] = corner1;
                _tileCorners[index, 1] = corner2;
                _tileCorners[index, 2] = corner3;
                _tileCorners[index, 3] = corner4;
                y += corner1.y + corner2.y + corner3.y + corner4.y;
            }
            _avgLineHeight = avgLineHeight = Mathf.RoundToInt(y / ((index + 1) * 4f));
            index = -1;
        }
        if (_tilesEnabled)
        {
            LandscapeTile? current = EditorUser.User!.Input.ControllerObject == null ? null : Landscape.getTile(EditorUser.User.Input.ControllerObject.transform.position);
            bool lastWasCurrent = false;

            foreach (LandscapeTile tile in tiles)
            {
                Vector3 corner1 = _tileCorners[++index, 0] with { y = avgLineHeight };
                Vector3 corner2 = _tileCorners[index, 1] with { y = avgLineHeight };
                Vector3 corner3 = _tileCorners[index, 2] with { y = avgLineHeight };
                Vector3 corner4 = _tileCorners[index, 3] with { y = avgLineHeight };
                if (!lastWasCurrent && current == tile)
                {
                    GL.Color(new Color(1, 1, 0));
                    lastWasCurrent = true;
                }
                else if (lastWasCurrent)
                {
                    lastWasCurrent = false;
                    GL.Color(new Color(1, 1, 1));
                }

                if (current == tile || Landscape.getTile(new LandscapeCoord(tile.coord.x - 1, tile.coord.y)) is null)
                {
                    GLUtility.line(corner1, corner4);
                }
                if (current == tile || Landscape.getTile(new LandscapeCoord(tile.coord.x, tile.coord.y - 1)) is null)
                {
                    GLUtility.line(corner1, corner2);
                }
                if (current == null || current.coord != new LandscapeCoord(tile.coord.x + 1, tile.coord.y))
                {
                    GLUtility.line(corner2, corner3);
                }
                if (current == null || current.coord != new LandscapeCoord(tile.coord.x, tile.coord.y + 1))
                {
                    GLUtility.line(corner4, corner3);
                }
            }
        }
        if (_regionsEnabled)
        {
            byte worldSize = Regions.WORLD_SIZE;
            byte cx = 0, cy = 0;
            bool current = EditorUser.User!.Input.ControllerObject != null;
            Vector3 position = !current ? Vector3.zero : EditorUser.User.Input.ControllerObject!.transform.position;
            current = current && Regions.tryGetCoordinate(position, out cx, out cy);
            float lvlSizeX = Mathf.Min(4096, Mathf.Max(Mathf.Abs(CartographyUtil.CaptureBounds.max.x), Mathf.Abs(CartographyUtil.CaptureBounds.min.x)));
            float lvlSizeZ = Mathf.Min(4096, Mathf.Max(Mathf.Abs(CartographyUtil.CaptureBounds.max.z), Mathf.Abs(CartographyUtil.CaptureBounds.min.z)));
            GL.Color(new Color(0.25f, 0.25f, 0.25f, 0.3f));
            float regionSize = Regions.REGION_SIZE;
            for (int i = 0; i <= worldSize; ++i)
            {
                if (!current || cx != i - 1)
                {
                    float xpos = i * regionSize - 4096f;
                    if (Mathf.Abs(xpos) < lvlSizeX)
                        GLUtility.line(new Vector3(xpos, avgLineHeight, -lvlSizeZ), new Vector3(xpos, avgLineHeight, lvlSizeZ));
                }
                if (!current || cy != i - 1)
                {
                    float zpos = i * regionSize - 4096f;
                    if (Mathf.Abs(zpos) < lvlSizeZ)
                        GLUtility.line(new Vector3(-lvlSizeX, avgLineHeight, zpos), new Vector3(lvlSizeX, avgLineHeight, zpos));
                }
            }
            if (current)
            {
                Regions.tryGetPoint(cx, cy, out Vector3 pos);
                float xpos = pos.x;
                float zpos = pos.z;
                if (Mathf.Abs(xpos) < lvlSizeX)
                {
                    if (cx > 0)
                        GLUtility.line(new Vector3(xpos + Regions.REGION_SIZE, avgLineHeight, -lvlSizeZ), new Vector3(xpos + Regions.REGION_SIZE, avgLineHeight, Mathf.Min(zpos, lvlSizeZ)));
                    if (cx < worldSize - 1)
                        GLUtility.line(new Vector3(xpos + Regions.REGION_SIZE, avgLineHeight, Mathf.Max(zpos + regionSize, -lvlSizeZ)), new Vector3(xpos + Regions.REGION_SIZE, avgLineHeight, lvlSizeZ));
                }
                if (Mathf.Abs(zpos) < lvlSizeZ)
                {
                    if (cy > 0)
                        GLUtility.line(new Vector3(-lvlSizeX, avgLineHeight, zpos + Regions.REGION_SIZE), new Vector3(Mathf.Min(xpos, lvlSizeX), avgLineHeight, zpos + Regions.REGION_SIZE));
                    if (cx < worldSize - 1)
                        GLUtility.line(new Vector3(Mathf.Max(xpos + regionSize, -lvlSizeX), avgLineHeight, zpos + Regions.REGION_SIZE), new Vector3(lvlSizeX, avgLineHeight, zpos + Regions.REGION_SIZE));
                }

                GL.Color(new Color(0.5f, 0.5f, 0f, 1f));
                GLUtility.line(new Vector3(xpos, avgLineHeight, zpos), new Vector3(xpos + regionSize, avgLineHeight, zpos));
                GLUtility.line(new Vector3(xpos, avgLineHeight, zpos), new Vector3(xpos, avgLineHeight, zpos + regionSize));
                GLUtility.line(new Vector3(xpos + regionSize, avgLineHeight, zpos),
                    new Vector3(xpos + regionSize, avgLineHeight, zpos + regionSize));
                GLUtility.line(new Vector3(xpos, avgLineHeight, zpos + regionSize),
                    new Vector3(xpos + regionSize, avgLineHeight, zpos + regionSize));
            }
        }
        GL.End();

        void GetCornersTile(LandscapeTile tile, out Vector3 corner1, out Vector3 corner2, out Vector3 corner3, out Vector3 corner4)
        {
            corner1 = Landscape.getWorldPosition(tile.coord, default, tile.heightmap[0, 0]);
            corner2 = Landscape.getWorldPosition(tile.coord, new HeightmapCoord(0, Landscape.HEIGHTMAP_RESOLUTION_MINUS_ONE), tile.heightmap[0, Landscape.HEIGHTMAP_RESOLUTION_MINUS_ONE]);
            corner3 = Landscape.getWorldPosition(tile.coord, new HeightmapCoord(Landscape.HEIGHTMAP_RESOLUTION_MINUS_ONE, Landscape.HEIGHTMAP_RESOLUTION_MINUS_ONE), tile.heightmap[Landscape.HEIGHTMAP_RESOLUTION_MINUS_ONE, Landscape.HEIGHTMAP_RESOLUTION_MINUS_ONE]);
            corner4 = Landscape.getWorldPosition(tile.coord, new HeightmapCoord(Landscape.HEIGHTMAP_RESOLUTION_MINUS_ONE, 0), tile.heightmap[Landscape.HEIGHTMAP_RESOLUTION_MINUS_ONE, 0]);
        }
    }
}
#endif