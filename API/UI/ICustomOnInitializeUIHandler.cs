#if CLIENT
namespace DevkitServer.API.UI;

/// <summary>
/// UI Handler to trigger when the UI is initialized.
/// </summary>
public interface ICustomOnInitializeUIHandler : ICustomUIHandler
{
    /// <summary>
    /// Invoked when the UI is initialized.
    /// </summary>
    event Action<Type?, object?> OnInitialized;

    /// <summary>
    /// Checks if <see cref="OnInitialized"/> has been subscribed to.
    /// </summary>
    bool HasOnInitializeBeenInitialized { get; internal set; }
}
#endif