#if CLIENT
using DevkitServer.API.Cartography;
using DevkitServer.Players;
using SDG.Framework.Landscapes;
using SDG.Framework.Rendering;
using System.Globalization;

namespace DevkitServer.Util.Debugging;
internal sealed class RegionDebug : MonoBehaviour
{
    private static readonly GUIStyle DebugStyle = new GUIStyle
    {
        normal = new GUIStyleState
        {
            textColor = new Color(0.9f, 0.9f, 0.9f)
        },
        wordWrap = false,
        richText = true,
        fontSize = 14,
        stretchHeight = true,
        stretchWidth = true,
        name = "DevkitServer Debug"
    };
    private static readonly GUIStyle DebugShadowStyle = new GUIStyle(DebugStyle)
    {
        normal = new GUIStyleState
        {
            textColor = new Color(0.1f, 0.1f, 0.1f)
        },
        fontSize = 14,
        stretchHeight = true,
        stretchWidth = true,
        richText = true
    };
    private static bool _tilesEnabled;
    private static bool _regionsEnabled;
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
    private static void Label(string content, ref float y)
    {
        float height = DebugStyle.CalcHeight(new GUIContent(content), 1024f);
        GUI.Label(new Rect(6, y + 1, 1024f, height + 5f), FormattingUtil.RemoveRichText(content,
            options: RemoveRichTextOptions.Color | RemoveRichTextOptions.Mark), DebugShadowStyle);
        GUI.Label(new Rect(5, y, 1024f, height + 5f), content, DebugStyle);
        y += height + 5f;
    }
    [UsedImplicitly]
    private void OnGUI()
    {
        if (!Level.isEditor || !_tilesEnabled && !_regionsEnabled || LoadingUI.isBlocked)
            return;

        Transform? ctrlTransform = !DevkitServerModule.IsEditing || EditorUser.User == null || EditorUser.User.Control == null || EditorUser.User.Control.ControllerObject == null ? MainCamera.instance.transform.parent : EditorUser.User.Control.ControllerObject.transform;
        if (ctrlTransform == null) return;
        float yaw = ctrlTransform.rotation.eulerAngles.y;
        yaw %= 360;
        if (yaw < 0) yaw += 360;
        float y = 80;
        Label("<b>Position</b>: " + ctrlTransform.position.ToString("F2") + ", Yaw: " + yaw.ToString("F2") + "°", ref y);
        Transform? aim = (!DevkitServerModule.IsEditing || EditorUser.User == null || EditorUser.User.Control == null || EditorUser.User.Control.ControllerObject == null ? MainCamera.instance.transform : EditorUser.User.Control.Aim);
        if (aim != null)
        {
            string facing;

            Vector3 forward = aim.forward;
            Vector3 abs = new Vector3(Mathf.Abs(forward.x), Mathf.Abs(forward.y), Mathf.Abs(forward.z));

            if (abs.x > abs.y && abs.x > abs.z)
                facing = forward.x >= 0 ? "+ X" : "- X";
            else if (abs.y > abs.x && abs.y > abs.z)
                facing = forward.y >= 0 ? "+ Y" : "- Y";
            else if (abs.z > abs.x && abs.z > abs.y)
                facing = forward.z >= 0 ? "+ Z" : "- Z";
            else
                facing = "0";

            Label("<b>Facing</b>: " + facing, ref y);
        }

        Vector3 position = ctrlTransform.position;
        if (_tilesEnabled)
        {
            if (Landscape.getTile(position) is { } tile)
                Label("<b>Tile</b>: " + tile.coord, ref y);
        }

        if (_regionsEnabled)
        {
            if (Regions.tryGetCoordinate(position, out byte x2, out byte y2))
                Label("<b>Region</b>: (" + x2.ToString(CultureInfo.InvariantCulture) + ", " + y2.ToString(CultureInfo.InvariantCulture) + ")", ref y);
        }
    }
    
    private static void HandleGLRender()
    {
        if (LoadingUI.isBlocked || !Level.isLoaded || UserControl.LocalController != CameraController.Editor)
            return;

        GLUtility.matrix = Matrix4x4.identity;
        GLUtility.LINE_FLAT_COLOR.SetPass(0);
        GL.Begin(GL.LINES);
        float avgLineHeight = _avgLineHeight;
        int index = -1;
        IReadOnlyCollection<LandscapeTile> tiles = LandscapeUtil.Tiles;
        if (_tileCorners == null || _tileCorners.GetLength(0) != tiles.Count)
        {
            float y = 0;
            _tileCorners = new Vector3[tiles.Count, 4];
            foreach (LandscapeTile tile in tiles)
            {
                Vector3 corner1 = Landscape.getWorldPosition(tile.coord, default, tile.heightmap[0, 0]);
                Vector3 corner2 = Landscape.getWorldPosition(tile.coord, new HeightmapCoord(0, Landscape.HEIGHTMAP_RESOLUTION_MINUS_ONE), tile.heightmap[0, Landscape.HEIGHTMAP_RESOLUTION_MINUS_ONE]);
                Vector3 corner3 = Landscape.getWorldPosition(tile.coord, new HeightmapCoord(Landscape.HEIGHTMAP_RESOLUTION_MINUS_ONE, Landscape.HEIGHTMAP_RESOLUTION_MINUS_ONE), tile.heightmap[Landscape.HEIGHTMAP_RESOLUTION_MINUS_ONE, Landscape.HEIGHTMAP_RESOLUTION_MINUS_ONE]);
                Vector3 corner4 = Landscape.getWorldPosition(tile.coord, new HeightmapCoord(Landscape.HEIGHTMAP_RESOLUTION_MINUS_ONE, 0), tile.heightmap[Landscape.HEIGHTMAP_RESOLUTION_MINUS_ONE, 0]);
                _tileCorners[++index, 0] = corner1;
                _tileCorners[index, 1] = corner2;
                _tileCorners[index, 2] = corner3;
                _tileCorners[index, 3] = corner4;
                y += corner1.y + corner2.y + corner3.y + corner4.y;
            }
            _avgLineHeight = avgLineHeight = Mathf.RoundToInt(y / ((index + 1) * 4f));
            index = -1;
        }
        Transform? ctrlTransform = !DevkitServerModule.IsEditing || EditorUser.User == null || EditorUser.User.Control == null || EditorUser.User.Control.ControllerObject == null ? MainCamera.instance.transform.parent : EditorUser.User.Control.ControllerObject.transform;
        if (_tilesEnabled)
        {
            LandscapeTile? current = ctrlTransform == null ? null : Landscape.getTile(ctrlTransform.position);
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
            bool isInRegion = ctrlTransform != null;
            Vector3 position = !isInRegion ? Vector3.zero : ctrlTransform!.position;
            isInRegion = isInRegion && Regions.tryGetCoordinate(position, out cx, out cy);
            float lvlSizeX = Mathf.Min(4096, Mathf.Max(Mathf.Abs(CartographyTool.CaptureBounds.max.x), Mathf.Abs(CartographyTool.CaptureBounds.min.x)));
            float lvlSizeZ = Mathf.Min(4096, Mathf.Max(Mathf.Abs(CartographyTool.CaptureBounds.max.z), Mathf.Abs(CartographyTool.CaptureBounds.min.z)));
            float regionSize = Regions.REGION_SIZE;
            bool outOfBoundsX = Mathf.Abs(position.x) > lvlSizeX;
            bool outOfBoundsZ = Mathf.Abs(position.z) > lvlSizeZ;
            GL.Color(new Color(0.25f, 0.25f, 0.25f, 0.3f));
            for (int i = 0; i <= worldSize; ++i)
            {
                if (!isInRegion || outOfBoundsZ || cx != i - 1)
                {
                    float xpos = i * regionSize - 4096f;
                    if (Mathf.Abs(xpos) < lvlSizeX)
                        GLUtility.line(new Vector3(xpos, avgLineHeight, -lvlSizeZ), new Vector3(xpos, avgLineHeight, lvlSizeZ));
                }
                if (!isInRegion || outOfBoundsX || cy != i - 1)
                {
                    float zpos = i * regionSize - 4096f;
                    if (Mathf.Abs(zpos) < lvlSizeZ)
                        GLUtility.line(new Vector3(-lvlSizeX, avgLineHeight, zpos), new Vector3(lvlSizeX, avgLineHeight, zpos));
                }
            }
            if (isInRegion)
            {
                Regions.tryGetPoint(cx, cy, out Vector3 pos);
                float regionPosX = pos.x;
                float regionPosZ = pos.z;
                if (Mathf.Abs(regionPosX) < lvlSizeX)
                {
                    if (cx > 0)
                        GLUtility.line(new Vector3(regionPosX + Regions.REGION_SIZE, avgLineHeight, -lvlSizeZ), new Vector3(regionPosX + Regions.REGION_SIZE, avgLineHeight, Mathf.Min(regionPosZ, lvlSizeZ)));
                    if (cx < worldSize)
                        GLUtility.line(new Vector3(regionPosX + Regions.REGION_SIZE, avgLineHeight, Mathf.Max(regionPosZ + regionSize, -lvlSizeZ)), new Vector3(regionPosX + Regions.REGION_SIZE, avgLineHeight, lvlSizeZ));
                }
                if (Mathf.Abs(regionPosZ) < lvlSizeZ)
                {
                    if (cy > 0)
                        GLUtility.line(new Vector3(-lvlSizeX, avgLineHeight, regionPosZ + Regions.REGION_SIZE), new Vector3(Mathf.Min(regionPosX, lvlSizeX), avgLineHeight, regionPosZ + Regions.REGION_SIZE));
                    if (cx < worldSize)
                        GLUtility.line(new Vector3(Mathf.Max(regionPosX + regionSize, -lvlSizeX), avgLineHeight, regionPosZ + Regions.REGION_SIZE), new Vector3(lvlSizeX, avgLineHeight, regionPosZ + Regions.REGION_SIZE));
                }

                GL.Color(new Color(0.5f, 0.5f, 0f, 1f));
                GLUtility.line(new Vector3(regionPosX, avgLineHeight, regionPosZ), new Vector3(regionPosX + regionSize, avgLineHeight, regionPosZ));
                GLUtility.line(new Vector3(regionPosX, avgLineHeight, regionPosZ), new Vector3(regionPosX, avgLineHeight, regionPosZ + regionSize));
                GLUtility.line(new Vector3(regionPosX + regionSize, avgLineHeight, regionPosZ),
                    new Vector3(regionPosX + regionSize, avgLineHeight, regionPosZ + regionSize));
                GLUtility.line(new Vector3(regionPosX, avgLineHeight, regionPosZ + regionSize),
                    new Vector3(regionPosX + regionSize, avgLineHeight, regionPosZ + regionSize));
            }
        }
        GL.End();
    }
}
#endif