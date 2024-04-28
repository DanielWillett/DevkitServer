using DevkitServer.API.Permissions;

namespace DevkitServer.API;

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