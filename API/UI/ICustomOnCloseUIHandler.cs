#if CLIENT
namespace DevkitServer.API.UI;

/// <summary>
/// UI Handler to trigger when the UI is closed.
/// </summary>
public interface ICustomOnCloseUIHandler : ICustomUIHandler
{
    /// <summary>
    /// Invoked when the UI is closed.
    /// </summary>
    event Action<Type?, object?> OnClosed;

    /// <summary>
    /// Checks if <see cref="OnClosed"/> has been subscribed to.
    /// </summary>
    bool HasOnCloseBeenInitialized { get; internal set; }
}
#endif