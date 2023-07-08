#if CLIENT
using DevkitServer.Players.UI;
using HarmonyLib;
using System.Reflection;
using System.Reflection.Emit;
using Action = System.Action;

namespace DevkitServer.API.UI;
public abstract class UIExtension
{
    public object? Instance { get; internal set; }

    public event Action? OnOpened;
    public event Action? OnClosed;

    internal void InvokeOnOpened() => OnOpened?.Invoke();
    internal void InvokeOnClosed() => OnClosed?.Invoke();
}
public abstract class UIExtension<T> : UIExtension where T : class
{
    public new T? Instance => (T?)base.Instance;
}

public class UIExtensionParentTypeInfo
{
    internal readonly List<UIExtensionPatch> OpenPatchesIntl;
    internal readonly List<UIExtensionPatch> ClosePatchesIntl;
    internal readonly List<UIExtensionPatch> InitializePatchesIntl;
    internal readonly List<UIExtensionPatch> DestroyPatchesIntl;
    internal readonly List<UIExtensionInstanceInfo> InstancesIntl;
    public Type ParentType { get; }
    public IReadOnlyList<UIExtensionPatch> OpenPatches { get; }
    public IReadOnlyList<UIExtensionPatch> ClosePatches { get; }
    public IReadOnlyList<UIExtensionPatch> InitializePatches { get; }
    public IReadOnlyList<UIExtensionPatch> DestroyPatches { get; }
    public IReadOnlyList<UIExtensionInstanceInfo> Instances { get; }

    public UIExtensionParentTypeInfo(Type parentType)
    {
        ParentType = parentType;
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
    }
}

public class UIExtensionPatch
{
    public MethodBase Original { get; }
    public MethodInfo Patch { get; }
    public HarmonyPatchType Type { get; }
    public UIExtensionPatch(MethodBase original, MethodInfo patch, HarmonyPatchType type)
    {
        Original = original;
        Patch = patch;
        Type = type;
    }
}

public class UIExistingMemberInfo
{
    public MemberInfo Member { get; }
    public MemberInfo Existing { get; }
    public bool ExistingIsStatic { get; }
    public bool IsInitialized { get; }
    public UIExistingMemberInfo(MemberInfo member, MemberInfo existing, bool existingIsStatic, bool isInitialized)
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

public class UIExtensionInstanceInfo
{
    public object Instance { get; }
    public object? VanillaInstance { get; }
    public bool Static { get; }
    public UIExtensionInstanceInfo(object instance, object? vanillaInstance)
    {
        Instance = instance;
        VanillaInstance = vanillaInstance;
        Static = ReferenceEquals(vanillaInstance, null);
    }
}

internal delegate object? CreateUIExtension(object? uiInstance);

public class UIExtensionInfo
{
    internal List<object> InstantiationsIntl { get; }
    internal List<UIExtensionPatch> PatchesIntl { get; }
    internal List<UIExistingMemberInfo> ExistingMembersIntl { get; }
    public Type ImplementationType { get; }
    public Type ParentType { get; }
    public int Priority { get; }
    public IDevkitServerPlugin? Plugin { get; }
    public bool IsBaseType { get; }
    public IReadOnlyList<object> Instantiations { get; }
    public IReadOnlyList<UIExtensionPatch> Patches { get; }
    public IReadOnlyList<UIExistingMemberInfo> ExistingMembers { get; }
    public bool IsEmittable { get; internal set; }
    public bool SuppressUIExtensionParentWarning { get; internal set; }
#nullable disable
    public UITypeInfo TypeInfo { get; internal set; }
    internal CreateUIExtension CreateCallback { get; set; }
#nullable restore
    public UIExtensionInfo(Type implementationType, Type parentType, int priority, IDevkitServerPlugin? plugin)
    {
        ImplementationType = implementationType;
        ParentType = parentType;
        Priority = priority;
        Plugin = plugin;
        IsBaseType = typeof(UIExtension).IsAssignableFrom(implementationType);
        InstantiationsIntl = new List<object>(1);
        Instantiations = InstantiationsIntl.AsReadOnly();
        PatchesIntl = new List<UIExtensionPatch>(3);
        Patches = PatchesIntl.AsReadOnly();
        ExistingMembersIntl = new List<UIExistingMemberInfo>(8);
        ExistingMembers = ExistingMembersIntl.AsReadOnly();
    }
}

[MeansImplicitUse]
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class UIExtensionAttribute : Attribute
{
    public Type ParentType { get; }
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

[MeansImplicitUse]
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
public sealed class ExistingUIMemberAttribute : Attribute
{
    public string MemberName { get; }
    /// <summary>
    /// Influences how values are cached, if at all.
    /// </summary>
    public ExistingMemberInitializeMode InitializeMode { get; set; }
    public Type? OwningType { get; set; }
    public ExistingUIMemberAttribute(string memberName)
    {
        MemberName = memberName;
    }
}

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