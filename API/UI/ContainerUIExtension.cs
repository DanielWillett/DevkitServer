#if CLIENT
namespace DevkitServer.API.UI;
/// <summary>
/// Implements a fullscreen container base for a UI.
/// </summary>
public abstract class ContainerUIExtension : UIExtension, IDisposable
{
    protected abstract SleekWindow Parent { get; }
    protected virtual float SizeScaleX => 1f;
    protected virtual float SizeScaleY => 1f;
    protected virtual float PositionScaleX => 0f;
    protected virtual float PositionScaleY => 0f;
    protected virtual int SizeOffsetX => 0;
    protected virtual int SizeOffsetY => 0;
    protected virtual int PositionOffsetX => 0;
    protected virtual int PositionOffsetY => 0;
    public SleekFullscreenBox Container { get; set; }
    protected ContainerUIExtension()
    {
        Container = new SleekFullscreenBox
        {
            sizeScale_X = 1f,
            sizeScale_Y = 1f,
            isVisible = false
        };
    }

    protected override void Opened()
    {
        if (Parent == null) return;
        if (Parent.FindIndexOfChild(Container) == -1)
        {
            Container = new SleekFullscreenBox
            {
                sizeScale_X = 1f,
                sizeScale_Y = 1f
            };
            Parent.AddChild(Container);
        }

        Container.sizeScale_X = SizeScaleX;
        Container.sizeScale_Y = SizeScaleY;
        Container.sizeOffset_X = SizeOffsetX;
        Container.sizeOffset_Y = SizeOffsetY;
        Container.positionScale_X = PositionScaleX;
        Container.positionScale_Y = PositionScaleY;
        Container.positionOffset_X = PositionOffsetX;
        Container.positionOffset_Y = PositionOffsetY;
        Container.isVisible = true;
        base.Opened();
    }
    protected override void Closed()
    {
        base.Closed();
        Container.isVisible = false;
    }

    public virtual void Dispose()
    {
        if (Parent != null && Parent.FindIndexOfChild(Container) >= 0)
            Parent.RemoveChild(Container);
        Container = null!;
    }
}
#endif