namespace DevkitServer.API.Cartography;
public abstract class SimpleChartColorProvider : IChartColorProvider
{
    private static readonly Vector3 RayDirection = Vector3.down;

    // cache this to reduce p/invoke calls
    private PhysicsScene _defaultPhysicsScene = Physics.defaultPhysicsScene;
    public Color SampleChartPosition(in ChartCaptureData data, IChartColorProvider colorProvider, Vector2 worldCoordinates)
    {
        Vector3 rayOrigin = new Vector3(worldCoordinates.x, data.CaptureCenter.y + data.CaptureSize.y / 2f, worldCoordinates.y);

        while (true)
        {
            _defaultPhysicsScene.Raycast(rayOrigin, RayDirection, out RaycastHit hit, data.CaptureSize.y, RayMasks.CHART, QueryTriggerInteraction.Ignore);

            Transform transform = hit.transform;
            int layer = transform == null ? LayerMasks.DEFAULT : transform.gameObject.layer;

            EObjectChart chartType = GetChartType(ref hit, transform, layer);
            if (chartType != EObjectChart.IGNORE)
                return GetColor(in data, chartType, transform, layer, ref hit);

            rayOrigin = hit.point + new Vector3(0f, -0.01f, 0f);
        }
    }
    public abstract Color GetColor(in ChartCaptureData data, EObjectChart chartType, Transform? transform, int layer, ref RaycastHit hit);
    public virtual EObjectChart GetChartType(ref RaycastHit hit, Transform? transform, int layer)
    {
        if (transform == null)
            return EObjectChart.GROUND;

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

    protected static int GetRoadLayer(Transform transform)
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

        return material.isConcrete ? (material.width <= 8d ? 1 : 0) : 3;
    }
}
