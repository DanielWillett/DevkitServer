namespace DevkitServer.API.UI.Icons.DefaultProviders;
internal class NoteDefaultProvider : IDefaultIconProvider
{
    private static readonly Vector3 DefaultPosition = new Vector3(0f, 0f, 0.8f);
    private static readonly Quaternion DefaultRotation = Quaternion.Euler(0f, 180f, 180f);

    /// <inheritdoc/>
    public int Priority => int.MinValue;

    /// <inheritdoc/>
    public void GetMetrics(ObjectAsset @object, out Vector3 position, out Quaternion rotation)
    {
        position = DefaultPosition;
        rotation = DefaultRotation;
    }

    /// <inheritdoc/>
    public bool AppliesTo(ObjectAsset @object) => @object.type is EObjectType.MEDIUM or EObjectType.LARGE or EObjectType.SMALL && @object.interactability == EObjectInteractability.NOTE;
}
