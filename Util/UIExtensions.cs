#if CLIENT
namespace DevkitServer.Util;
public static class UIExtensions
{
    public static void CopyTransformFrom(this ISleekElement element, ISleekElement other)
    {
        element.positionOffset_X = other.positionOffset_X;
        element.positionOffset_Y = other.positionOffset_Y;
        element.positionScale_X = other.positionScale_X;
        element.positionScale_Y = other.positionScale_Y;
        element.sizeOffset_X = other.sizeOffset_X;
        element.sizeOffset_Y = other.sizeOffset_Y;
        element.sizeScale_X = other.sizeScale_X;
        element.sizeScale_Y = other.sizeScale_Y;
    }
}
#endif
