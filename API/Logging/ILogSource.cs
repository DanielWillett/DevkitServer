namespace DevkitServer.API.Logging;

/// <summary>
/// Provides a source context for logging.
/// </summary>
public interface ILogSource
{
    /// <summary>
    /// Source context to hint at where logs came from.
    /// </summary>
    string Source { get; }

    /// <summary>
    /// Gets an override for whether a certain <paramref name="severity"/> is visible.
    /// </summary>
    bool? GetExplicitVisibilitySetting(Severity severity) => null;
}
