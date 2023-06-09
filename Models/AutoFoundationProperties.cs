namespace DevkitServer.Models;
public readonly struct AutoFoundationProperties
{
    public readonly float Radius;
    public readonly float Length;
    public readonly ERayMask Mask;
    public AutoFoundationProperties(float radius, float length, ERayMask mask)
    {
        Radius = radius;
        Length = length;
        Mask = mask;
    }
}