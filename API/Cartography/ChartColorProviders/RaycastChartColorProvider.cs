using DevkitServer.Core.Cartography;
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
        int l = _allWaterVolumes.Count(x => x != null);
        if (l != _allWaterVolumes.Length)
        {
            WaterVolume[] newWtrVlms = new WaterVolume[l];
            int index = -1;
            for (int i = 0; i < _allWaterVolumes.Length; ++i)
            {
                WaterVolume volume = _allWaterVolumes[i];
                
                if (volume == null)
                    continue;

                newWtrVlms[++index] = volume;
            }

            _allWaterVolumes = newWtrVlms;
        }

        _waterVolumeCenters = new Vector3[l];
        _waterVolumeExtents = new Vector3[l];

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
    public Color32 SampleChartPosition(in CartographyCaptureData data, LevelCartographyConfigData? configData, Vector2 worldCoordinates) => throw new NotImplementedException();

    /// <summary>
    /// Gets the chart type based on transform and layer.
    /// For maximum performance do not use <see cref="RaycastHit.transform"/> or most other properties in <see cref="RaycastHit"/> or <see cref="Transform"/>.
    /// </summary>
    public virtual EObjectChart GetChartType(ref RaycastHit hit, LevelCartographyConfigData? configData, Transform? transform, int layer)
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
                LevelObject obj = objects[i];
                if (!ReferenceEquals(obj.transform, transform))
                    continue;

                if (obj.asset == null)
                    return EObjectChart.IGNORE;

                if (configData != null && configData.TryGetObjectChartOverride(obj.asset.GUID, out EObjectChart chart))
                    return chart;

                return obj.asset.chart;
            }

            transform = transform.root;
            for (int i = 0; i < ct; ++i)
            {
                LevelObject obj = objects[i];
                if (!ReferenceEquals(obj.transform, transform))
                    continue;

                if (obj.asset == null)
                    return EObjectChart.IGNORE;

                if (configData != null && configData.TryGetObjectChartOverride(obj.asset.GUID, out EObjectChart chart))
                    return chart;

                return obj.asset.chart;
            }

            return EObjectChart.NONE;
        }

        if (layer != LayerMasks.RESOURCE || !Regions.tryGetCoordinate(transform.position, out x, out y))
            return EObjectChart.NONE;

        List<ResourceSpawnpoint> spawnpoints = LevelGround.trees[x, y];

        ct = spawnpoints.Count;
        for (int i = 0; i < ct; ++i)
        {
            ResourceSpawnpoint sp = spawnpoints[i];
            if (!ReferenceEquals(sp.model, transform))
                continue;

            if (sp.asset == null)
                return EObjectChart.IGNORE;

            if (configData != null && configData.TryGetObjectChartOverride(sp.asset.GUID, out EObjectChart chart))
                return chart;

            return sp.asset.chart;
        }

        transform = transform.root;
        for (int i = 0; i < ct; ++i)
        {
            ResourceSpawnpoint sp = spawnpoints[i];
            if (!ReferenceEquals(sp.model, transform))
                continue;

            if (sp.asset == null)
                return EObjectChart.IGNORE;

            if (configData != null && configData.TryGetObjectChartOverride(sp.asset.GUID, out EObjectChart chart))
                return chart;

            return sp.asset.chart;
        }

        return EObjectChart.NONE;
    }

    /// <summary>
    /// Gets the new layer of the road at <paramref name="transform"/>.
    /// </summary>
    protected internal static int GetRoadLayer(Transform transform, LevelCartographyConfigData? config, out EObjectChart chartType)
    {
        RoadMaterial? material = null;

        Transform? parent = transform.parent;
        byte materialIndex = byte.MaxValue;
        for (int i = 0; i < ushort.MaxValue; ++i)
        {
            Road? road = LevelRoads.getRoad(i);
            if (road == null)
                break;

            if (!ReferenceEquals(road.road, transform) && !ReferenceEquals(road.road, parent))
                continue;

            material = LevelRoads.materials[road.material];
            materialIndex = road.material;
            break;
        }

        if (material == null)
        {
            chartType = EObjectChart.NONE;
            return LayerMasks.ENVIRONMENT;
        }

        if (config != null && config.TryGetRoadMaterialChartOverride(materialIndex, out chartType))
        {
            return LayerMasks.ENVIRONMENT;
        }

        chartType = EObjectChart.NONE;
        return material.isConcrete ? material.width <= 8d ? 1 : 0 : 3;
    }

    /// <summary>
    /// Faster implementation of <see cref="WaterUtility.isPointUnderwater(Vector3)"/>.
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