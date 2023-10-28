#if CLIENT
namespace DevkitServer.API.UI;

/// <summary>
/// UI Handler to trigger when the UI is opened.
/// </summary>
public interface ICustomOnOpenUIHandler : ICustomUIHandler
{
    /// <summary>
    /// Invoked when the UI is opened.
    /// </summary>
    event Action<Type?, object?> OnOpened;

    /// <summary>
    /// Checks if <see cref="OnOpened"/> has been subscribed to.
    /// </summary>
    bool HasOnOpenBeenInitialized { get; internal set; }
}
#endif