using Unity.Collections;
using Unity.Jobs;

namespace DevkitServer.Core.Cartography.Jobs;

internal struct SetupChartRaycastsJob : IJobParallelFor
{
    private static readonly int ChartMask = RayMasks.CHART;

    public NativeArray<RaycastCommand> Commands;
    public NativeArray<Vector2> Casts;
    public Vector3 Direction;
    public float Length;
    public float Height;
    public void Execute(int index)
    {
        Vector2 castPoint = Casts[index];
        index *= 4;

        Vector3 dir = Direction;
        float len = Length;
        int chart = ChartMask;
        float height = Height;

        Commands[index]     = new RaycastCommand(new Vector3(castPoint.x + 0.75f, height, castPoint.y + 0.75f), dir, len, chart);
        Commands[index + 1] = new RaycastCommand(new Vector3(castPoint.x + 0.25f, height, castPoint.y + 0.75f), dir, len, chart);
        Commands[index + 2] = new RaycastCommand(new Vector3(castPoint.x + 0.75f, height, castPoint.y + 0.25f), dir, len, chart);
        Commands[index + 3] = new RaycastCommand(new Vector3(castPoint.x + 0.25f, height, castPoint.y + 0.25f), dir, len, chart);
    }
}
