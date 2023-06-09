namespace DevkitServer.Models;
public readonly struct AutoSlopeProperties
{
    public readonly float MinimumAngleStart;
    public readonly float MinimumAngleEnd;
    public readonly float MaximumAngleStart;
    public readonly float MaximumAngleEnd;
    public AutoSlopeProperties(float minimumAngleStart, float minimumAngleEnd, float maximumAngleStart, float maximumAngleEnd)
    {
        MinimumAngleStart = minimumAngleStart;
        MinimumAngleEnd = minimumAngleEnd;
        MaximumAngleStart = maximumAngleStart;
        MaximumAngleEnd = maximumAngleEnd;
    }
}