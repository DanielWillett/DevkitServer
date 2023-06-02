#if CLIENT
using DevkitServer.Players;
using JetBrains.Annotations;
using SDG.Framework.Landscapes;
using SDG.Framework.Rendering;

namespace DevkitServer.Util.Debugging;
internal sealed class TileDebug : MonoBehaviour
{
    private static bool _enabled = true;
    private static Vector3[,]? corners;
    private static float avgHeight;
    internal static bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value) return;
            _enabled = value;
            if (value)
                DevkitServerGLUtility.OnRenderAny += HandleGLRender;
            else
                DevkitServerGLUtility.OnRenderAny -= HandleGLRender;
        }
    }

    [UsedImplicitly]
    private void Start()
    {
        if (_enabled)
            DevkitServerGLUtility.OnRenderAny += HandleGLRender;
    }
    [UsedImplicitly]
    private void OnDestroy()
    {
        if (_enabled)
            DevkitServerGLUtility.OnRenderAny -= HandleGLRender;
    }
    [UsedImplicitly]
    private void OnGUI()
    {
        if (!_enabled) return;
        if (!DevkitServerModule.IsEditing || EditorUser.User == null || EditorUser.User.Input == null || EditorUser.User.Input.ControllerObject == null)
            return;
        GUI.Label(new Rect(5, 105f, 1024f, 20f), "Position: " + EditorUser.User.Input.ControllerObject.transform.position.ToString("F2"));
        if (Landscape.getTile(EditorUser.User.Input.ControllerObject.transform.position) is { } tile)
            GUI.Label(new Rect(5, 80f, 1024f, 20f), "Tile: " + tile.coord);
    }
    private static void HandleGLRender()
    {
        if (UserInput.LocalController != CameraController.Editor) return;
        GLUtility.matrix = Matrix4x4.identity;
        GLUtility.LINE_FLAT_COLOR.SetPass(0);
        GL.Begin(GL.LINES);
        LandscapeTile? current = EditorUser.User!.Input.ControllerObject == null ? null : Landscape.getTile(EditorUser.User.Input.ControllerObject.transform.position);
        bool lastWasCurrent = false;
        IReadOnlyCollection<LandscapeTile> tiles = LandscapeUtil.EnumerateAllTiles();
        int index = -1;
        if (corners == null || corners.GetLength(0) != tiles.Count)
        {
            float y = 0;
            corners = new Vector3[tiles.Count, 4];
            foreach (LandscapeTile tile in tiles)
            {
                GetCorners(tile, out Vector3 corner1, out Vector3 corner2, out Vector3 corner3, out Vector3 corner4);
                corners[++index, 0] = corner1;
                corners[index, 1] = corner2;
                corners[index, 2] = corner3;
                corners[index, 3] = corner4;
                y += corner1.y + corner2.y + corner3.y + corner4.y;
            }
            avgHeight = Mathf.RoundToInt(y / ((index + 1) * 4f));
            index = -1;
        }
        
        foreach (LandscapeTile tile in tiles)
        {
            Vector3 corner1 = corners[++index, 0] with { y = avgHeight };
            Vector3 corner2 = corners[index, 1] with { y = avgHeight };
            Vector3 corner3 = corners[index, 2] with { y = avgHeight };
            Vector3 corner4 = corners[index, 3] with { y = avgHeight };
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
        GL.End();

        void GetCorners(LandscapeTile tile, out Vector3 corner1, out Vector3 corner2, out Vector3 corner3, out Vector3 corner4)
        {
            corner1 = Landscape.getWorldPosition(tile.coord, default, tile.heightmap[0, 0]);
            corner2 = Landscape.getWorldPosition(tile.coord, new HeightmapCoord(0, Landscape.HEIGHTMAP_RESOLUTION_MINUS_ONE), tile.heightmap[0, Landscape.HEIGHTMAP_RESOLUTION_MINUS_ONE]);
            corner3 = Landscape.getWorldPosition(tile.coord, new HeightmapCoord(Landscape.HEIGHTMAP_RESOLUTION_MINUS_ONE, Landscape.HEIGHTMAP_RESOLUTION_MINUS_ONE), tile.heightmap[Landscape.HEIGHTMAP_RESOLUTION_MINUS_ONE, Landscape.HEIGHTMAP_RESOLUTION_MINUS_ONE]);
            corner4 = Landscape.getWorldPosition(tile.coord, new HeightmapCoord(Landscape.HEIGHTMAP_RESOLUTION_MINUS_ONE, 0), tile.heightmap[Landscape.HEIGHTMAP_RESOLUTION_MINUS_ONE, 0]);
        }
    }
}
#endif