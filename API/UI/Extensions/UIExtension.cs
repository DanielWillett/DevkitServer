#if CLIENT
namespace DevkitServer.API.UI.Extensions;

/// <summary>
/// Optional base class for UI extensions (defined with <see cref="UIExtensionAttribute"/>). Provides OnOpened and OnClosed events, as well as overridable methods for the same events.
/// Also provides an <see cref="Instance"/> property for accessing the vanilla UI instance (will be <see langword="null"/> for static UIs).
/// </summary>
public abstract class UIExtension
{
    /// <summary>
    /// The vanilla UI instance, or <see langword="null"/> when the vanilla UI is a static UI.
    /// </summary>
    public object? Instance { get; internal set; }

    /// <summary>
    /// Called when the vanilla UI is opened.
    /// </summary>
    public event Action? OnOpened;

    /// <summary>
    /// Called when the vanilla UI is closed.
    /// </summary>
    public event Action? OnClosed;

    /// <summary>
    /// Called when the vanilla UI is opened, before <see cref="OnOpened"/>.
    /// </summary>
    protected virtual void Opened() { }

    /// <summary>
    /// Called when the vanilla UI is closed, after <see cref="OnClosed"/>.
    /// </summary>
    protected virtual void Closed() { }

    internal void InvokeOnOpened()
    {
        Opened();
        OnOpened?.Invoke();
    }

    internal void InvokeOnClosed()
    {
        OnClosed?.Invoke();
        Closed();
    }
}

/// <summary>
/// Optional base class for UI extensions (defined with <see cref="UIExtensionAttribute"/>). Provides OnOpened and OnClosed events, as well as overridable methods for the same events.
/// Also provides a typed <see cref="Instance"/> property for accessing the vanilla UI instance (will be <see langword="null"/> for static UIs).
/// </summary>
/// <typeparam name="T">The vanilla UI type.</typeparam>
public abstract class UIExtension<T> : UIExtension where T : class
{
    /// <summary>
    /// The vanilla UI instance, or <see langword="null"/> when the vanilla UI is a static UI.
    /// </summary>
    public new T? Instance => (T?)base.Instance;
}
#endif