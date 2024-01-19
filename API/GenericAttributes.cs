using DevkitServer.API.Permissions;

namespace DevkitServer.API;


/// <summary>
/// Define a generic priority for a member.
/// </summary>
[AttributeUsage(AttributeTargets.All, Inherited = false)]
public sealed class LoadPriorityAttribute : Attribute
{
    /// <summary>
    /// Generic load priority. Higher values will be used first.
    /// </summary>
    public int Priority { get; }

    /// <summary>
    /// Define a generic priority for a member.
    /// </summary>
    /// <param name="priority">Higher values are considered first.</param>
    public LoadPriorityAttribute(int priority)
    {
        Priority = priority;
    }
}

/// <summary>
/// Define that a member should be ignored.
/// </summary>
[AttributeUsage(AttributeTargets.All, Inherited = false)]
public sealed class IgnoreAttribute : Attribute;

/// <summary>
/// Identifies which plugin a member belongs to.
/// </summary>
[AttributeUsage(AttributeTargets.All, Inherited = false)]
[BaseTypeRequired(typeof(IDevkitServerPlugin))]
public class PluginIdentifierAttribute : Attribute
{
    public Type? PluginType { get; set; }

    /// <summary>
    /// Identifies which plugin a member belongs to.
    /// </summary>
    public PluginIdentifierAttribute(Type? pluginType)
    {
        PluginType = pluginType;
    }
}

/// <summary>
/// Overrides the permission prefix for a plugin.
/// </summary>
/// <remarks>A permission prefix is the value that goes in front of permissions for a plugin. Used in <see cref="PermissionLeaf"/> and <see cref="PermissionBranch"/>.</remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Assembly, Inherited = false)]
[BaseTypeRequired(typeof(IDevkitServerPlugin))]
public sealed class PermissionPrefixAttribute : Attribute
{
    /// <summary>
    /// The value that goes in front of permissions for a plugin. Used in <see cref="PermissionLeaf"/> and <see cref="PermissionBranch"/>.
    /// </summary>
    public string Prefix { get; }

    /// <summary>
    /// Overrides the permission prefix for a plugin.
    /// </summary>
    /// <remarks>A permission prefix is the value that goes in front of permissions for a plugin. Used in <see cref="PermissionLeaf"/> and <see cref="PermissionBranch"/>.</remarks>
    public PermissionPrefixAttribute(string prefix) => Prefix = prefix;
}