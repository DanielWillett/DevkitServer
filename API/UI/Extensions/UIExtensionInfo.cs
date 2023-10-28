#if CLIENT
using DevkitServer.API.UI.Extensions.Members;
using System.Reflection;

namespace DevkitServer.API.UI.Extensions;

/// <summary>
/// Stores cached information about a UI extension.
/// </summary>
public class UIExtensionInfo
{
    internal List<object> InstantiationsIntl { get; }
    internal List<UIExtensionPatch> PatchesIntl { get; }
    internal List<Members.UIExistingMemberInfo> ExistingMembersIntl { get; }

    /// <summary>
    /// Type of the extension.
    /// </summary>
    public Type ImplementationType { get; }

    /// <summary>
    /// Type of the vanilla parent UI.
    /// </summary>
    public Type ParentType { get; }

    /// <summary>
    /// Priority of the extension.
    /// </summary>
    public int Priority { get; }

    /// <summary>
    /// Assembly the extension is from.
    /// </summary>
    public Assembly Assembly { get; }

    /// <summary>
    /// Plugin the extension is from.
    /// </summary>
    public IDevkitServerPlugin? Plugin { get; }

    /// <summary>
    /// If the extension derives from <see cref="UIExtension"/>.
    /// </summary>
    public bool IsBaseType { get; }

    /// <summary>
    /// All instantiations of the extension.
    /// </summary>
    public IReadOnlyList<object> Instantiations { get; }

    /// <summary>
    /// All patches used by the extension's existing UI members.
    /// </summary>
    public IReadOnlyList<UIExtensionPatch> Patches { get; }

    /// <summary>
    /// All existing UI members defined by the extension. These are defined using the <see cref="ExistingMemberAttribute"/>.
    /// </summary>
    public IReadOnlyList<Members.UIExistingMemberInfo> ExistingMembers { get; }

    /// <summary>
    /// If the warning for not deriving your class from the <see cref="UIExtension"/> class is supressed, which can be done using the <see cref="UIExtensionAttribute.SuppressUIExtensionParentWarning"/> property.
    /// </summary>
    public bool SuppressUIExtensionParentWarning { get; internal set; }
#nullable disable
    /// <summary>
    /// UI type info for the parent type of the UI.
    /// </summary>
    public UITypeInfo TypeInfo { get; internal set; }

    /// <summary>
    /// Defined by <see cref="UIExtensionManager"/> as a method to create the extension. Don't recommend using this unless you're making your own implementation.
    /// </summary>
    public UIExtensionManager.CreateUIExtension CreateCallback { get; set; }
#nullable restore
    internal UIExtensionInfo(Type implementationType, Type parentType, int priority, IDevkitServerPlugin? plugin)
    {
        ImplementationType = implementationType;
        ParentType = parentType;
        Priority = priority;
        Plugin = plugin;
        Assembly = implementationType.Assembly;
        IsBaseType = typeof(UIExtension).IsAssignableFrom(implementationType);
        InstantiationsIntl = new List<object>(1);
        Instantiations = InstantiationsIntl.AsReadOnly();
        PatchesIntl = new List<UIExtensionPatch>(3);
        Patches = PatchesIntl.AsReadOnly();
        ExistingMembersIntl = new List<Members.UIExistingMemberInfo>(8);
        ExistingMembers = ExistingMembersIntl.AsReadOnly();
    }
}
#endif