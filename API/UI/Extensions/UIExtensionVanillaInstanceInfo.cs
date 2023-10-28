#if CLIENT
namespace DevkitServer.API.UI.Extensions;

/// <summary>
/// Stores information about a vanilla UI type.
/// </summary>
public class UIExtensionVanillaInstanceInfo
{
    /// <summary>
    /// The instance of the vanilla UI type, or <see langword="null"/> for static UIs.
    /// </summary>
    public object? Instance { get; }

    /// <summary>
    /// If the vanilla UI type is a static UI.
    /// </summary>
    public bool Static { get; }

    /// <summary>
    /// Open state of the vanilla UI.
    /// </summary>
    public bool IsOpen { get; internal set; }
    internal UIExtensionVanillaInstanceInfo(object? instance, bool isOpen)
    {
        Instance = instance;
        Static = ReferenceEquals(instance, null);
        IsOpen = isOpen;
    }

    /// <inheritdoc />
    public override string ToString() => $"Instance: {(Instance == null ? "null" : "instance of " + Instance.GetType().Name)} (hash: {(Instance == null ? 0 : Instance.GetHashCode()):X8}), Static: {Static}, IsOpen: {IsOpen}.";
}
#endif