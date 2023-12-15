namespace DevkitServer.API.Cartography.ChartColorProviders;

/// <summary>
/// Defines a plugin-implemented color provider.
/// </summary>
public readonly struct ChartColorProviderInfo
{
    /// <summary>
    /// Type of the color provider. Implements <see cref="IChartColorProvider"/>.
    /// </summary>
    public Type Type { get; }

    /// <summary>
    /// Plugin that implements the provider.
    /// </summary>
    public IDevkitServerPlugin Plugin { get; }

    /// <summary>
    /// Priority for this provider.
    /// </summary>
    public int Priority { get; }

    internal ChartColorProviderInfo(Type type, IDevkitServerPlugin plugin, int priority)
    {
        Type = type;
        Plugin = plugin;
        Priority = priority;
    }
}
