#if CLIENT
namespace DevkitServer.API.UI.Extensions.Members;

/// <summary>
/// Describes the behavior of a member marked by the <see cref="ExistingMemberAttribute"/> when it's unable to be found or doesn't match the expected type.
/// </summary>
public enum ExistingMemberFailureBehavior
{
    /// <summary>
    /// Do not load the extension if the member can not be found or doesn't match the expected type.
    /// </summary>
    FailToLoad,

    /// <summary>
    /// If the member can't be found or doesn't match the expected type, the value will not be initialized (will be default/null).
    /// </summary>
    Ignore
}
#endif