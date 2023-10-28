#if CLIENT
namespace DevkitServer.Util;

/// <summary>
/// Random utilities for working with Glazier elements.
/// </summary>
public static class UIExtensions
{
    /// <summary>
    /// Copies all the basic transform values from one <see cref="ISleekElement"/> to another.
    /// </summary>
    public static void CopyTransformFrom(this ISleekElement element, ISleekElement other)
    {
        element.PositionOffset_X = other.PositionOffset_X;
        element.PositionOffset_Y = other.PositionOffset_Y;
        element.PositionScale_X = other.PositionScale_X;
        element.PositionScale_Y = other.PositionScale_Y;
        element.SizeOffset_X = other.SizeOffset_X;
        element.SizeOffset_Y = other.SizeOffset_Y;
        element.SizeScale_X = other.SizeScale_X;
        element.SizeScale_Y = other.SizeScale_Y;
    }

    /// <summary>
    /// Attempt to remove <paramref name="child"/> from <paramref name="parent"/> if it's a child.
    /// </summary>
    /// <returns><see langword="false"/> if either <paramref name="child"/> or <paramref name="parent"/> is <see langword="null"/> or <paramref name="child"/> is not a child of <paramref name="parent"/> or <paramref name="parent"/> has been destroyed, otherwise <see langword="true"/>.</returns>
    public static bool TryRemoveChild(this ISleekElement? parent, ISleekElement? child)
    {
        if (parent == null || child == null)
            return false;

        if (parent.FindIndexOfChild(child) == -1)
            return false;

        try
        {
            parent.RemoveChild(child);
            return true;
        }
        catch (NullReferenceException)
        {
            // parent was destroyed already.
            return false;
        }
    }
}
#endif
