#if CLIENT
namespace DevkitServer.API.UI.Extensions;

/// <summary>
/// Implements a fullscreen container base for a UI.
/// </summary>
public abstract class ContainerUIExtension : UIExtension, IDisposable
{
    private bool _containerHasBeenParented;

    /// <summary>
    /// The parent of the container. Usually <see cref="EditorUI.window"/>, <see cref="PlayerUI.window"/>, <see cref="MenuUI.window"/>, or <see cref="LoadingUI.window"/>.
    /// </summary>
    protected abstract SleekWindow Parent { get; }

    /// <summary>
    /// Width of the container (from 0-1, 0 being 0px and 1 being the size of the monitor or parent object).
    /// </summary>
    protected virtual float SizeScaleX => 1f;

    /// <summary>
    /// Height of the container (from 0-1, 0 being 0px and 1 being the size of the monitor or parent object).
    /// </summary>
    protected virtual float SizeScaleY => 1f;

    /// <summary>
    /// X Position of the top left corner of the container from the left side of the screen (from 0-1, 0 being 0px and 1 being the size of the monitor or parent object).
    /// </summary>
    protected virtual float PositionScaleX => 0f;

    /// <summary>
    /// X Position of the top left corner of the container from the top of the screen (from 0-1, 0 being 0px and 1 being the size of the monitor or parent object).
    /// </summary>
    protected virtual float PositionScaleY => 0f;

    /// <summary>
    /// How much bigger the container is outside of its bounds in the x direction (in pixels). Use negative values to shrink the container.
    /// </summary>
    protected virtual int SizeOffsetX => 0;

    /// <summary>
    /// How much bigger the container is outside of its bounds in the y direction (in pixels). Use negative values to shrink the container.
    /// </summary>
    protected virtual int SizeOffsetY => 0;

    /// <summary>
    /// How offset the container is from its bounds in the x direction (in pixels). Positive values move the container right, negative values move it left.
    /// </summary>
    protected virtual int PositionOffsetX => 0;

    /// <summary>
    /// How offset the container is from its bounds in the y direction (in pixels). Positive values move the container down, negative values move it up.
    /// </summary>
    protected virtual int PositionOffsetY => 0;

    /// <summary>
    /// Base container to add all elements to.
    /// </summary>
    /// <remarks>The value of this property may change over time.</remarks>
    public SleekFullscreenBox Container { get; set; }

    /// <summary>
    /// Creates a new <see cref="ContainerUIExtension"/> and a new <see cref="SleekFullscreenBox"/> without parenting it.
    /// </summary>
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

    /// <summary>
    /// Add all your components in this method.
    /// </summary>
    protected abstract void OnShown();

    /// <summary>
    /// Remove all your components in this method by calling <see cref="UIExtensions.TryRemoveChild"/> on <see cref="Container"/>.
    /// </summary>
    protected abstract void OnHidden();

    /// <summary>
    /// Unsubscribe from events, etc here. This is basically the <see cref="IDisposable.Dispose"/> method of your extension.
    /// </summary>
    protected abstract void OnDestroyed();

    /// <summary>
    /// Don't call this.
    /// </summary>
    protected sealed override void Opened()
    {
        if (Parent == null)
        {
            Logger.LogError($"Parent null trying to add container: {GetType().Name}.", method: "CONTAINER EXTENSION");
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
                    Logger.LogDebug("[CONTAINER EXTENSION] Error destroying container.");
#if DEBUG
                    Logger.LogError(ex, method: "CONTAINER EXTENSION");
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

    /// <summary>
    /// Don't call this.
    /// </summary>
    protected sealed override void Closed()
    {
        OnHidden();
        try
        {
            Container.IsVisible = false;
        }
        catch (NullReferenceException)
        {
            // ignored
        }
    }

    /// <summary>
    /// Don't call or implement this.
    /// </summary>
    public void Dispose()
    {
        OnDestroyed();
        Parent.TryRemoveChild(Container);
        _containerHasBeenParented = true;
        Container = null!;
    }
}
#endif