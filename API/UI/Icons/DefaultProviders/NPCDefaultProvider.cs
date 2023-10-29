namespace DevkitServer.API.UI.Icons.DefaultProviders;
internal class NPCDefaultProvider : IDefaultIconProvider
{
    private static readonly Vector3 DefaultPosition = new Vector3(1.48f, -2.46f, 1.69f);
    private static readonly Quaternion DefaultRotation = Quaternion.Euler(301.81f, 243.1f, 293.33f);

    /// <inheritdoc/>
    public int Priority => int.MinValue;

    /// <inheritdoc/>
    public void GetMetrics(ObjectAsset @object, out Vector3 position, out Quaternion rotation)
    {
        position = DefaultPosition;
        rotation = DefaultRotation;
    }

    /// <inheritdoc/>
    public bool AppliesTo(ObjectAsset @object) => @object.type == EObjectType.NPC;
}
