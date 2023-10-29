namespace DevkitServer.API.UI.Icons.DefaultProviders;
internal class BillboardDefaultProvider : IDefaultIconProvider
{
    private static readonly Vector3 DefaultPosition = new Vector3(-3f, -10f, 9f);
    private static readonly Quaternion DefaultRotation = Quaternion.Euler(-107f, -80f, 260f);

    /// <inheritdoc/>
    public int Priority => int.MinValue;

    /// <inheritdoc/>
    public void GetMetrics(ObjectAsset @object, out Vector3 position, out Quaternion rotation)
    {
        position = DefaultPosition;
        rotation = DefaultRotation;
    }

    /// <inheritdoc/>
    public bool AppliesTo(ObjectAsset @object) => @object.type == EObjectType.LARGE && @object.name.StartsWith("Billboard_", StringComparison.Ordinal);
}
