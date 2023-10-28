#if CLIENT
namespace DevkitServer.API.UI;

/// <summary>
/// UI Handler to trigger when the UI is destroyed.
/// </summary>
public interface ICustomOnDestroyUIHandler : ICustomUIHandler
{
    /// <summary>
    /// Invoked when the UI is destroyed.
    /// </summary>
    event Action<Type?, object?> OnDestroyed;

    /// <summary>
    /// Checks if <see cref="OnDestroyed"/> has been subscribed to.
    /// </summary>
    bool HasOnDestroyBeenInitialized { get; internal set; }
}
#endif