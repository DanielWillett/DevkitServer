using DevkitServer.Core.Cartography.ChartColorProviders;
using SDG.Framework.Water;

namespace DevkitServer.API.Cartography.ChartColorProviders;

/// <summary>
/// Chart color provider that uses multi-threaded effecient raycasting with Unity's Job System, similar to how vanilla does it but much faster.
/// </summary>
/// <remarks>Abstract. Default implementations: <see cref="BundledStripChartColorProvider"/>, <see cref="JsonChartColorProvider"/>.</remarks>
public abstract class RaycastChartColorProvider : ISamplingChartColorProvider
{
    private WaterVolume[] _allWaterVolumes = null!;
    private Vector3[] _waterVolumeCenters = null!;
    private Vector3[] _waterVolumeExtents = null!;

    public virtual bool TryInitialize(in CartographyCaptureData data, bool isExplicitlyDefined)
    {
        _allWaterVolumes = WaterVolumeManager.Get().GetAllVolumes().ToArray();
        _waterVolumeCenters = new Vector3[_allWaterVolumes.Length];
        _waterVolumeExtents = new Vector3[_allWaterVolumes.Length];

        for (int i = 0; i < _allWaterVolumes.Length; ++i)
        {
            WaterVolume volume = _allWaterVolumes[i];

            Bounds bounds = volume.CalculateWorldBounds();
            _waterVolumeCenters[i] = bounds.center;
            _waterVolumeExtents[i] = bounds.extents;
        }

        return true;
    }

    /// <summary>
    /// Converts chart type and layer data to a raw <see cref="Color32"/>.
    /// </summary>
    public abstract Color32 GetColor(in CartographyCaptureData data, EObjectChart chartType, Transform? transform, int layer, ref RaycastHit hit);
    
    /// <exception cref="NotImplementedException"/>
    public Color32 SampleChartPosition(in CartographyCaptureData data, Vector2 worldCoordinates) => throw new NotImplementedException();

    /// <summary>
    /// Gets the chart type based on transform and layer.
    /// For maximum performance do not use <see cref="RaycastHit.transform"/> or most other properties in <see cref="RaycastHit"/> or <see cref="Transform"/>.
    /// </summary>
    public virtual EObjectChart GetChartType(ref RaycastHit hit, Transform? transform, int layer)
    {
        if (transform is null || layer == LayerMasks.GROUND)
            return EObjectChart.GROUND;

        if (layer == LayerMasks.WATER)
            return EObjectChart.WATER;

        byte x, y;
        int ct;
        if (layer is LayerMasks.SMALL or LayerMasks.MEDIUM or LayerMasks.LARGE)
        {
            if (!Regions.tryGetCoordinate(transform.position, out x, out y))
                return EObjectChart.NONE;

            List<LevelObject> objects = LevelObjects.objects[x, y];
            ct = objects.Count;
            for (int i = 0; i < ct; ++i)
            {
                if (ReferenceEquals(objects[i].transform, transform))
                    return objects[i].asset.chart;
            }

            transform = transform.root;
            for (int i = 0; i < ct; ++i)
            {
                if (ReferenceEquals(objects[i].transform, transform))
                    return objects[i].asset.chart;
            }

            return EObjectChart.NONE;
        }

        if (layer != LayerMasks.RESOURCE || !Regions.tryGetCoordinate(transform.position, out x, out y))
            return EObjectChart.NONE;

        List<ResourceSpawnpoint> spawnpoints = LevelGround.trees[x, y];

        ct = spawnpoints.Count;
        for (int i = 0; i < ct; ++i)
        {
            if (ReferenceEquals(spawnpoints[i].model, transform))
                return spawnpoints[i].asset.chart;
        }

        transform = transform.root;
        for (int i = 0; i < ct; ++i)
        {
            if (ReferenceEquals(spawnpoints[i].model, transform))
                return spawnpoints[i].asset.chart;
        }

        return EObjectChart.NONE;
    }

    /// <summary>
    /// Gets the new layer of the road at <paramref name="transform"/>.
    /// </summary>
    protected internal static int GetRoadLayer(Transform transform)
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

    /// <summary>
    /// Faster implementation of <see cref="WaterUtility.isPointUnderwater"/>.
    /// </summary>
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
