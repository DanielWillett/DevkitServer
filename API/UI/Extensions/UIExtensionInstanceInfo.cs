#if CLIENT
namespace DevkitServer.API.UI.Extensions;

/// <summary>
/// Stores information about an extension instance.
/// </summary>
public class UIExtensionInstanceInfo
{
    /// <summary>
    /// A reference to the actual extension.
    /// </summary>
    public object Instance { get; }

    /// <summary>
    /// The vanilla type the instance is extending.
    /// </summary>
    public UIExtensionVanillaInstanceInfo VanillaInstance { get; }

    /// <summary>
    /// The extension the instance belongs to.
    /// </summary>
    public UIExtensionInfo Extension { get; }
    internal UIExtensionInstanceInfo(object instance, UIExtensionVanillaInstanceInfo vanillaInstance, UIExtensionInfo extension)
    {
        Instance = instance;
        VanillaInstance = vanillaInstance;
        Extension = extension;
    }
}
#endif