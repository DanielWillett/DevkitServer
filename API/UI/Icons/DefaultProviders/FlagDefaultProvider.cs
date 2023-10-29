namespace DevkitServer.API.UI.Icons.DefaultProviders;
internal class FlagDefaultProvider : IDefaultIconProvider
{
    private static readonly Vector3 DefaultPosition = new Vector3(-4.95f, 1.97f, 10.74f);
    private static readonly Quaternion DefaultRotation = Quaternion.Euler(0.71f, 92.61f, 77.94f);

    /// <inheritdoc/>
    public int Priority => int.MinValue;

    /// <inheritdoc/>
    public void GetMetrics(ObjectAsset @object, out Vector3 position, out Quaternion rotation)
    {
        position = DefaultPosition;
        rotation = DefaultRotation;
    }

    /// <inheritdoc/>
    public bool AppliesTo(ObjectAsset @object) => @object is
    {
        isSnowshoe: true,
        interactability: EObjectInteractability.RUBBLE,
        type: EObjectType.MEDIUM or EObjectType.LARGE or EObjectType.SMALL
    } && @object.name.StartsWith("Flag_", StringComparison.Ordinal);
}
