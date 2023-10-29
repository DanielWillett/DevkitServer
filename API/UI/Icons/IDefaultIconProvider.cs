namespace DevkitServer.API.UI.Icons;

/// <summary>
/// Provides information about default objects offsets.
/// </summary>
public interface IDefaultIconProvider
{
    /// <summary>
    /// 0 = default, higher is ran before lower.
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Set the offsets of the object's default icon.
    /// </summary>
    void GetMetrics(ObjectAsset @object, out Vector3 position, out Quaternion rotation);

    /// <summary>
    /// Is this object affected by this provider?
    /// </summary>
    bool AppliesTo(ObjectAsset @object);
}
