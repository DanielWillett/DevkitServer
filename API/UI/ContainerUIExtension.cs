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
    private bool _containerHasBeenParented;
    protected ContainerUIExtension()
    {
        Container = new SleekFullscreenBox
        {
            SizeScale_X = 1f,
            SizeScale_Y = 1f,
            IsVisible = false
        };
        _containerHasBeenParented = false;
    }
    
    protected abstract void OnShown();
    protected abstract void OnHidden();
    protected abstract void OnDestroyed();
    protected sealed override void Opened()
    {
        if (Parent == null)
        {
            Logger.LogError($"Parent null trying to add container: {this.Format()}.");
            return;
        }
        if (!_containerHasBeenParented || Parent.FindIndexOfChild(Container) == -1)
        {
            if (_containerHasBeenParented)
            {
                try
                {
                    Container.InternalDestroy();
                }
                catch (Exception ex)
                {
#if DEBUG
                    Logger.LogDebug("Error destroying container.");
                    Logger.LogError(ex);
#endif
                }

                Container = new SleekFullscreenBox
                {
                    SizeScale_X = 1f,
                    SizeScale_Y = 1f
                };
            }

            Parent.AddChild(Container);
            _containerHasBeenParented = true;
        }

        Container.SizeScale_X = SizeScaleX;
        Container.SizeScale_Y = SizeScaleY;
        Container.SizeOffset_X = SizeOffsetX;
        Container.SizeOffset_Y = SizeOffsetY;
        Container.PositionScale_X = PositionScaleX;
        Container.PositionScale_Y = PositionScaleY;
        Container.PositionOffset_X = PositionOffsetX;
        Container.PositionOffset_Y = PositionOffsetY;
        Container.IsVisible = true;
        OnShown();
    }
    protected sealed override void Closed()
    {
        OnHidden();
        Container.IsVisible = false;
    }

    public void Dispose()
    {
        OnDestroyed();
        if (Parent != null && Parent.FindIndexOfChild(Container) >= 0)
        {
            Parent.RemoveChild(Container);
            _containerHasBeenParented = true;
        }
        Container = null!;
    }
}
#endif