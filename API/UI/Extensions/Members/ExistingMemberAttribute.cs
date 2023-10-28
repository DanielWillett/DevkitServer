#if CLIENT
namespace DevkitServer.API.UI.Extensions.Members;

/// <summary>
/// Mark a field, property, or method as an accessor for a field, property, or method in the parent type. The member can be static or instance.
/// </summary>
/// <remarks>For fields, the value is cached on initialization.
/// For properties with setters (on default <see cref="InitializeMode"/>), the value is cached on initialization,
/// for properties without setters, the getter is patched to get the value in realtime each time it's called.
/// For methods, they will be patched to get the value in realtime each time they're called.
///
/// <br/><br/>Usage of methods isn't really recommended for existing fields or properties, just because it's not very practical, but it does work.</remarks>
[MeansImplicitUse]
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
public sealed class ExistingMemberAttribute : Attribute
{
    /// <summary>
    /// Name of the member.
    /// </summary>
    public string MemberName { get; }

    /// <summary>
    /// Influences how values are cached, if at all.
    /// </summary>
    public ExistingMemberInitializeMode InitializeMode { get; set; }

    /// <summary>
    /// Influences how reflection failures are handled (member can't be found or is the wrong type).
    /// </summary>
    public ExistingMemberFailureBehavior FailureBehavior { get; set; }

    /// <summary>
    /// Type that owns the member if it isn't the parent type you're extending or a base type of it.
    /// </summary>
    public Type? OwningType { get; set; }

    /// <summary>
    /// Defines an existing member by it's field, property, or method name.
    /// </summary>
    /// <param name="memberName">The name of the field, property, or method.</param>
    public ExistingMemberAttribute(string memberName)
    {
        MemberName = memberName;
    }
}
#endif