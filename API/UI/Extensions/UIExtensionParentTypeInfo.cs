#if CLIENT
namespace DevkitServer.API.UI.Extensions;

/// <summary>
/// Stores information about a parent type (vanilla UI type like <see cref="MenuPlayServerInfoUI"/>).
/// </summary>
public class UIExtensionParentTypeInfo
{
    internal readonly List<UIExtensionPatch> OpenPatchesIntl;
    internal readonly List<UIExtensionPatch> ClosePatchesIntl;
    internal readonly List<UIExtensionPatch> InitializePatchesIntl;
    internal readonly List<UIExtensionPatch> DestroyPatchesIntl;
    internal readonly List<UIExtensionInstanceInfo> InstancesIntl;
    internal readonly List<UIExtensionVanillaInstanceInfo> VanillaInstancesIntl;

    /// <summary>
    /// Type of the parent UI.
    /// </summary>
    public Type ParentType { get; }

    /// <summary>
    /// Information about the type of the parent UI.
    /// </summary>
    public UITypeInfo ParentTypeInfo { get; internal set; }

    /// <summary>
    /// All existing patches for open handlers.
    /// </summary>
    public IReadOnlyList<UIExtensionPatch> OpenPatches { get; }

    /// <summary>
    /// All existing patches for close handlers.
    /// </summary>
    public IReadOnlyList<UIExtensionPatch> ClosePatches { get; }

    /// <summary>
    /// All existing patches for initialize handlers.
    /// </summary>
    public IReadOnlyList<UIExtensionPatch> InitializePatches { get; }

    /// <summary>
    /// All existing patches for destroy handlers.
    /// </summary>
    public IReadOnlyList<UIExtensionPatch> DestroyPatches { get; }

    /// <summary>
    /// All active extension instances.
    /// </summary>
    public IReadOnlyList<UIExtensionInstanceInfo> Instances { get; }

    /// <summary>
    /// All active parent type instances.
    /// </summary>
    public IReadOnlyList<UIExtensionVanillaInstanceInfo> VanillaInstances { get; }

    internal UIExtensionParentTypeInfo(Type parentType, UITypeInfo typeInfo)
    {
        ParentType = parentType;
        ParentTypeInfo = typeInfo;
        OpenPatchesIntl = new List<UIExtensionPatch>(1);
        ClosePatchesIntl = new List<UIExtensionPatch>(1);
        InitializePatchesIntl = new List<UIExtensionPatch>(1);
        DestroyPatchesIntl = new List<UIExtensionPatch>(1);
        OpenPatches = OpenPatchesIntl.AsReadOnly();
        ClosePatches = ClosePatchesIntl.AsReadOnly();
        InitializePatches = InitializePatchesIntl.AsReadOnly();
        DestroyPatches = DestroyPatchesIntl.AsReadOnly();
        InstancesIntl = new List<UIExtensionInstanceInfo>(1);
        Instances = InstancesIntl.AsReadOnly();
        VanillaInstancesIntl = new List<UIExtensionVanillaInstanceInfo>(1);
        VanillaInstances = VanillaInstancesIntl.AsReadOnly();
    }
}
#endif