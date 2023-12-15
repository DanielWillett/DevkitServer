using DevkitServer.Core.Cartography.ChartColorProviders;
using SDG.Framework.Landscapes;
using SDG.Framework.Water;

namespace DevkitServer.API.Cartography.ChartColorProviders;

/// <summary>
/// Chart color provider that uses raycasting like the base game for color picking.
/// </summary>
/// <remarks>Abstract. Default implementations: <see cref="BundledStripChartColorProvider"/>, <see cref="JsonChartColorProvider"/>.</remarks>
public abstract class SimpleChartColorProvider : IChartColorProvider
{
    private static readonly Vector3 RayDirection = Vector3.down;
    private Dictionary<LandscapeCoord, LandscapeTile> _allTiles = null!;
    private WaterVolume[] _allWaterVolumes = null!;
    private Vector3[] _waterVolumeCenters = null!;
    private Vector3[] _waterVolumeExtents = null!;
    private PhysicsScene _defaultPhysicsScene;

    public virtual bool TryInitialize(in CartographyCaptureData data)
    {
        _allTiles = LandscapeUtil.GetTileDictionary();
        _allWaterVolumes = WaterVolumeManager.Get().GetAllVolumes().ToArray();
        _waterVolumeCenters = new Vector3[_allWaterVolumes.Length];
        _waterVolumeExtents = new Vector3[_allWaterVolumes.Length];
        _defaultPhysicsScene = Physics.defaultPhysicsScene;

        for (int i = 0; i < _allWaterVolumes.Length; ++i)
        {
            WaterVolume volume = _allWaterVolumes[i];

            Bounds bounds = volume.CalculateWorldBounds();
            _waterVolumeCenters[i] = bounds.center;
            _waterVolumeExtents[i] = bounds.extents;
            Logger.LogDebug($"Water volume {i.Format()} : {bounds.Format("F2")}.");
        }

        return true;
    }
    public abstract Color32 GetColor(in CartographyCaptureData data, EObjectChart chartType, Transform? transform, int layer, ref RaycastHit hit);
    public Color32 SampleChartPosition(in CartographyCaptureData data, Vector2 worldCoordinates)
    {
        float x = worldCoordinates.x, y = worldCoordinates.y;
        Color32 tr = Sample(in data, x + 0.75f, y + 0.75f),
                br = Sample(in data, x + 0.25f, y + 0.75f),
                tl = Sample(in data, x + 0.75f, y + 0.25f),
                bl = Sample(in data, x + 0.25f, y + 0.25f);

        return new Color32(
            (byte)(tr.r / 4 + br.r / 4 + tl.r / 4 + bl.r / 4),
            (byte)(tr.g / 4 + br.g / 4 + tl.g / 4 + bl.g / 4),
            (byte)(tr.b / 4 + br.b / 4 + tl.b / 4 + bl.b / 4),
            255
        );
    }
    private Color32 Sample(in CartographyCaptureData data, float x, float y)
    {
        Vector3 rayOrigin = new Vector3(x, data.CaptureCenter.y + data.CaptureSize.y / 2f, y);

        while (true)
        {
            _defaultPhysicsScene.Raycast(rayOrigin, RayDirection, out RaycastHit hit, data.CaptureSize.y, RayMasks.CHART, QueryTriggerInteraction.Ignore);

            Transform? transform = hit.transform;

            if (transform == null)
                transform = null!;

            int layer = transform is null ? LayerMasks.DEFAULT : transform.gameObject.layer;

            if (layer == LayerMasks.ENVIRONMENT)
                layer = GetRoadLayer(transform!);

            EObjectChart chartType = GetChartType(ref hit, transform, layer);
            if (chartType != EObjectChart.IGNORE)
                return GetColor(in data, chartType, transform, layer, ref hit);

            rayOrigin = new Vector3(x, hit.point.y - 0.01f, y);
        }
    }

    public virtual EObjectChart GetChartType(ref RaycastHit hit, Transform? transform, int layer)
    {
        if (transform is null || layer == LayerMasks.GROUND)
            return EObjectChart.GROUND;

        if (layer == LayerMasks.WATER)
            return EObjectChart.WATER;
        
        if (layer is LayerMasks.SMALL or LayerMasks.MEDIUM or LayerMasks.LARGE)
        {
            LevelObject? obj = LevelObjectUtil.FindObject(hit.transform, false);
            if (obj != null)
                return obj.asset.chart;
        }

        if (layer != LayerMasks.RESOURCE || !Regions.tryGetCoordinate(hit.point, out byte x, out byte y))
            return EObjectChart.NONE;

        List<ResourceSpawnpoint> spawnpoints = LevelGround.trees[x, y];
        Transform t = transform.root;

        for (int i = 0; i < spawnpoints.Count; ++i)
        {
            if (ReferenceEquals(spawnpoints[i].model, t))
                return spawnpoints[i].asset.chart;
        }

        return EObjectChart.NONE;
    }

    internal static int GetRoadLayer(Transform transform)
    {
        RoadMaterial? material = null;

        Transform? parent = transform.parent;
        for (int i = 0; i < ushort.MaxValue; ++i)
        {
            Road? road = LevelRoads.getRoad(i);
            if (road == null)
                break;

            if (!ReferenceEquals(road.road, transform) && !ReferenceEquals(road.road, parent))
                continue;

            material = LevelRoads.materials[road.material];
            break;
        }

        if (material == null)
            return RayMasks.ENVIRONMENT;

        return material.isConcrete ? material.width <= 8d ? 1 : 0 : 3;
    }
    protected bool IsPointUnderwaterFast(Vector3 point)
    {
        // return WaterUtility.isPointUnderwater(point);
        for (int i = 0; i < _allWaterVolumes.Length; ++i)
        {
            ref Vector3 center = ref _waterVolumeCenters[i];
            ref Vector3 extents = ref _waterVolumeExtents[i];

            if (point.x >= center.x - extents.x &&
                point.x <= center.x + extents.x &&
                point.y >= center.y - extents.y &&
                point.y <= center.y + extents.y &&
                point.z >= center.z - extents.z &&
                point.z <= center.z + extents.z &&
                _allWaterVolumes[i].IsPositionInsideVolume(point))
            {
                return true;
            }
        }

        return false;
    }
}
