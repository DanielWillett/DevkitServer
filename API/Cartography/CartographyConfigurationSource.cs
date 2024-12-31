using System.Text.Json;

namespace DevkitServer.API.Cartography;

public readonly struct CartographyConfigurationSource : IDisposable
{
    public readonly string? Path;
    public readonly JsonElement Configuraiton;
    public readonly IDisposable? Disposable;

    public CartographyConfigurationSource(string? path, JsonElement configuraiton, IDisposable? disposable = null)
    {
        Path = path;
        Configuraiton = configuraiton;
        Disposable = disposable;
    }

    /// <inheritdoc />
    public override string ToString() => Path ?? "<no configuration>";

    public void Dispose()
    {
        Disposable?.Dispose();
    }
}