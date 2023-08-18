#if CLIENT
using DevkitServer.Players.UI;
using HarmonyLib;
using System.Reflection;
using System.Reflection.Emit;

namespace DevkitServer.API.UI;

/// <summary>
/// Optional base class for UI extensions (defined with <see cref="UIExtensionAttribute"/>). Provides OnOpened and OnClosed events, as well as overridable methods for the same events.
/// Also provides an <see cref="Instance"/> property for accessing the vanilla UI instance (will be <see langword="null"/> for static UIs).
/// </summary>
public abstract class UIExtension
{
    /// <summary>
    /// The vanilla UI instance, or <see langword="null"/> when the vanilla UI is a static UI.
    /// </summary>
    public object? Instance { get; internal set; }

    /// <summary>
    /// Called when the vanilla UI is opened.
    /// </summary>
    public event Action? OnOpened;

    /// <summary>
    /// Called when the vanilla UI is closed.
    /// </summary>
    public event Action? OnClosed;

    /// <summary>
    /// Called when the vanilla UI is opened, before <see cref="OnOpened"/>.
    /// </summary>
    protected virtual void Opened() { }

    /// <summary>
    /// Called when the vanilla UI is closed, after <see cref="OnClosed"/>.
    /// </summary>
    protected virtual void Closed() { }

    internal void InvokeOnOpened()
    {
        Opened();
        OnOpened?.Invoke();
    }

    internal void InvokeOnClosed()
    {
        OnClosed?.Invoke();
        Closed();
    }
}
/// <summary>
/// Optional base class for UI extensions (defined with <see cref="UIExtensionAttribute"/>). Provides OnOpened and OnClosed events, as well as overridable methods for the same events.
/// Also provides a typed <see cref="Instance"/> property for accessing the vanilla UI instance (will be <see langword="null"/> for static UIs).
/// </summary>
/// <typeparam name="T">The vanilla UI type.</typeparam>
public abstract class UIExtension<T> : UIExtension where T : class
{
    public new T? Instance => (T?)base.Instance;
}

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

/// <summary>
/// Stores information about a UI handler patch (open, close, destroy, or initialize).
/// </summary>
public class UIExtensionPatch
{
    /// <summary>
    /// Original method that was patched.
    /// </summary>
    public MethodBase Original { get; }

    /// <summary>
    /// Method that was patched onto <see cref="Original"/>.
    /// </summary>
    public MethodInfo Patch { get; }

    /// <summary>
    /// Type of patch used.
    /// </summary>
    public HarmonyPatchType Type { get; }
    internal UIExtensionPatch(MethodBase original, MethodInfo patch, HarmonyPatchType type)
    {
        Original = original;
        Patch = patch;
        Type = type;
    }
}

/// <summary>
/// Stores information about existing members (defined with <see cref="ExistingUIMemberAttribute"/>) on an extension.
/// </summary>
public class UIExistingMemberInfo
{
    /// <summary>
    /// Member to get or set in the extension.
    /// </summary>
    public MemberInfo Member { get; }

    /// <summary>
    /// Member to reference in the parent type.
    /// </summary>
    public MemberInfo Existing { get; }

    /// <summary>
    /// If the member in the parent type is static.
    /// </summary>
    public bool ExistingIsStatic { get; }

    /// <summary>
    /// If the member is set when the extension is created, instead of patching the extension member to get the value in realtime (customized by setting <see cref="ExistingUIMemberAttribute.InitializeMode"/>).
    /// </summary>
    public bool IsInitialized { get; }
    internal UIExistingMemberInfo(MemberInfo member, MemberInfo existing, bool existingIsStatic, bool isInitialized)
    {
        Member = member;
        Existing = existing;
        ExistingIsStatic = existingIsStatic;
        IsInitialized = isInitialized;
    }
    internal void EmitGet(DebuggableEmitter il, bool onlyRead = false)
    {
        switch (Existing)
        {
            case FieldInfo field:
                il.Emit(ExistingIsStatic ? OpCodes.Ldsfld : OpCodes.Ldfld, field);
                break;
            case MethodInfo method:
                il.Emit(method.GetCall(), method);
                break;
            case PropertyInfo property:
                MethodInfo getter = property.GetGetMethod(true);
                if (getter == null)
                    goto default;
                il.Emit(getter.GetCall(), getter);
                break;
            default:
                Logger.LogWarning($"Invalid accessor for existing member: {Existing.Format()}.", method: UIExtensionManager.Source);
                il.Emit(OpCodes.Ldnull);
                break;
        }
        if (!onlyRead)
        {
            switch (Member)
            {
                case FieldInfo field:
                    il.Emit(field.IsStatic ? OpCodes.Stsfld : OpCodes.Stfld, field);
                    break;
                case PropertyInfo property:
                    MethodInfo setter = property.GetSetMethod(true);
                    if (setter == null)
                        goto default;
                    il.Emit(setter.GetCall(), setter);
                    break;
                case MethodInfo method:
                    il.Emit(method.GetCall(), method);
                    break;
                default:
                    Logger.LogWarning($"Invalid accessor for implementing member: {Member.Format()}.", method: UIExtensionManager.Source);
                    break;
            }
        }
    }
}

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
    internal UIExtensionInstanceInfo(object instance, UIExtensionVanillaInstanceInfo vanillaInstance)
    {
        Instance = instance;
        VanillaInstance = vanillaInstance;
    }
}

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

    public override string ToString() => $"Instance: {Instance.Format()} ({(Instance == null ? 0 : Instance.GetHashCode()).Format("X8")}), static: {Static.Format()}, is open: {IsOpen.Format()}.";
}

/// <summary>
/// Stores cached information about a UI extension.
/// </summary>
public class UIExtensionInfo
{
    internal List<object> InstantiationsIntl { get; }
    internal List<UIExtensionPatch> PatchesIntl { get; }
    internal List<UIExistingMemberInfo> ExistingMembersIntl { get; }
    
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
    /// All existing UI members defined by the extension. These are defined using the <see cref="ExistingUIMemberAttribute"/>.
    /// </summary>
    public IReadOnlyList<UIExistingMemberInfo> ExistingMembers { get; }

    /// <summary>
    /// If the warning for not deriving your class from the <see cref="UIExtension"/> class is supressed, which can be done using the <see cref="UIExtensionAttribute.SuppressUIExtensionParentWarning"/> property.
    /// </summary>
    public bool SuppressUIExtensionParentWarning { get; internal set; }
#nullable disable
    /// <summary>
    /// UI type info for the parent type of the UI.
    /// </summary>
    public UITypeInfo TypeInfo { get; internal set; }
    internal UIExtensionManager.CreateUIExtension CreateCallback { get; set; }
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
        ExistingMembersIntl = new List<UIExistingMemberInfo>(8);
        ExistingMembers = ExistingMembersIntl.AsReadOnly();
    }
}

/// <summary>
/// Mark your extension to be auto-registered to <see cref="UIExtensionManager"/> when your plugin loads.
/// </summary>
[MeansImplicitUse]
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class UIExtensionAttribute : Attribute
{
    /// <summary>
    /// Type of the vanilla UI being extended.
    /// </summary>
    public Type ParentType { get; }

    /// <summary>
    /// Supress the warning for not deriving an extension from <see cref="UIExtension"/>.
    /// </summary>
    public bool SuppressUIExtensionParentWarning { get; set; }

    public UIExtensionAttribute(Type parentType)
    {
        ParentType = parentType;
    }

    /// <summary>
    /// Add type by name, mainly for internal types.<br/>
    /// Use assembly qualified name if the type is not from SDG (Assembly-CSharp.dll).
    /// </summary>
    public UIExtensionAttribute(string parentType)
    {
        ParentType = Type.GetType(parentType, false, true) ?? Accessor.AssemblyCSharp.GetType(parentType, false, true) ?? Accessor.AssemblyCSharp.GetType("SDG.Unturned." + parentType, true, true);
    }
}

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
public sealed class ExistingUIMemberAttribute : Attribute
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
    /// Type that owns the member if it isn't the parent type you're extending or a base type of it.
    /// </summary>
    public Type? OwningType { get; set; }

    public ExistingUIMemberAttribute(string memberName)
    {
        MemberName = memberName;
    }
}

/// <summary>
/// Describes the behavior of a member marked by the <see cref="ExistingUIMemberAttribute"/>.
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