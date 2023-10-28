#if CLIENT
namespace DevkitServer.API.UI.Extensions.Members;

/// <summary>
/// Describes the behavior of a member marked by the <see cref="ExistingMemberAttribute"/>.
/// </summary>
public enum ExistingMemberInitializeMode
{
    /// <summary>
    /// Fields get initialized, properties with no setters get patched, properties with setters get initialized.
    /// </summary>
    Default,

    /// <summary>
    /// The field or property is set when the class is created.
    /// </summary>
    InitializeOnConstruct,

    /// <summary>
    /// The property's getter is patched to refetch the element each time.
    /// </summary>
    PatchGetter
}
#endif