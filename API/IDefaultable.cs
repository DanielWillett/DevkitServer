namespace DevkitServer.API;

/// <summary>
/// Adds a <see cref="SetDefaults"/> method to set default values for a configuration class.
/// </summary>
public interface IDefaultable
{
    /// <summary>
    /// Set default values for a configuration class.
    /// </summary>
    void SetDefaults();
}
