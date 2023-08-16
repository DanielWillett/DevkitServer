#if CLIENT
#if DEBUG

/*
 * Uncomment this to enable UI Extension Debug Logging.
 */

#define UI_EXT_DEBUG
#endif

using DevkitServer.API;
using DevkitServer.API.UI;
using DevkitServer.Patches;
using DevkitServer.Plugins;
using HarmonyLib;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;

namespace DevkitServer.Players.UI;
public static class UIExtensionManager
{
    internal const string Source = "UI EXT MNGR";
    private static readonly List<UIExtensionInfo> ExtensionsIntl = new List<UIExtensionInfo>(8);
    private static readonly Dictionary<Type, UIExtensionInfo> ExtensionsDictIntl = new Dictionary<Type, UIExtensionInfo>(8);
    private static readonly Dictionary<Type, UIExtensionParentTypeInfo> ParentTypeInfoIntl = new Dictionary<Type, UIExtensionParentTypeInfo>(8);
    private static readonly Dictionary<MethodBase, UIExtensionPatch> Patches = new Dictionary<MethodBase, UIExtensionPatch>(64);
    private static readonly Dictionary<MethodBase, UIExtensionExistingMemberPatchInfo> PatchInfo = new Dictionary<MethodBase, UIExtensionExistingMemberPatchInfo>(64);
    private static Action<object>? _onDestroy;
    private static Action<object>? _onAdd;
    public static IReadOnlyList<UIExtensionInfo> Extensions { get; } = ExtensionsIntl.AsReadOnly();
    public static IReadOnlyDictionary<Type, UIExtensionParentTypeInfo> ParentTypeInfo { get; } = new ReadOnlyDictionary<Type, UIExtensionParentTypeInfo>(ParentTypeInfoIntl);

    public static T? GetInstance<T>() where T : class => InstanceCache<T>.Instance;

    [Conditional("UI_EXT_DEBUG")]
    internal static void LogDebug(string message, IDevkitServerPlugin? plugin = null, Assembly? assembly = null)
    {
        if (plugin == null)
            Logger.LogDebug(assembly == null ? "[" + Source + "] " + message : ("[" + Source + " | " + assembly.GetName().Name.ToUpperInvariant() + "] " + message));
        else
            plugin.LogDebug(message);
    }
    internal static void LogInfo(string message, IDevkitServerPlugin? plugin = null, Assembly? assembly = null)
    {
        if (plugin == null)
            Logger.LogWarning(message, method: assembly == null ? Source : (Source + " | " + assembly.GetName().Name.ToUpperInvariant()));
        else
            plugin.LogWarning(message);
    }
    internal static void LogWarning(string message, IDevkitServerPlugin? plugin = null, Assembly? assembly = null)
    {
        if (plugin == null)
            Logger.LogWarning(message, method: assembly == null ? Source : (Source + " | " + assembly.GetName().Name.ToUpperInvariant()));
        else
            plugin.LogWarning(message);
    }
    internal static void LogError(string message, IDevkitServerPlugin? plugin = null, Assembly? assembly = null)
    {
        if (plugin == null)
            Logger.LogError(message, method: assembly == null ? Source : (Source + " | " + assembly.GetName().Name.ToUpperInvariant()));
        else
            plugin.LogError(message);
    }
    internal static void LogError(Exception ex, IDevkitServerPlugin? plugin = null, Assembly? assembly = null)
    {
        if (plugin == null)
            Logger.LogError(ex, method: assembly == null ? Source : (Source + " | " + assembly.GetName().Name.ToUpperInvariant()));
        else
            plugin.LogError(ex);
    }

    internal static void Reflect(Assembly assembly)
    {
        ThreadUtil.assertIsGameThread();

        List<Type> types = Accessor.GetTypesSafe(assembly, true);
        foreach (Type type in types)
        {
            if (Attribute.GetCustomAttribute(type, typeof(UIExtensionAttribute)) is not UIExtensionAttribute attribute)
                continue;

            LoadPriorityAttribute? priority = Attribute.GetCustomAttribute(type, typeof(LoadPriorityAttribute)) as LoadPriorityAttribute;
            IDevkitServerPlugin? plugin = null;
            if (type.Assembly != Accessor.DevkitServer)
            {
                plugin = PluginLoader.FindPluginForMember(type);
                if (plugin == null)
                {
                    LogWarning($"Unable to link {type.Format()} UI extension to a plugin. Use the {typeof(PluginIdentifierAttribute).Format()} on the " +
                                      "field to link an invoker to a " +
                                      "plugin when multiple plugins are loaded from an assembly.", null, type.Assembly);
                    continue;
                }
            }

            UIExtensionInfo info = new UIExtensionInfo(type, attribute.ParentType, priority == null ? 0 : priority.Priority, plugin)
            {
                SuppressUIExtensionParentWarning = attribute.SuppressUIExtensionParentWarning
            };

            try
            {
                InitializeExtension(info);
            }
            catch (Exception ex)
            {
                LogError($"Error initializing UI extension: {type.Format()}.", plugin, type.Assembly);
                LogError(ex, plugin, type.Assembly);
                continue;
            }

            LogDebug($"Registered UI extension: {type.Format()}.", plugin, type.Assembly);
        }
    }

    private static void OnOpened(Type? type, object? instance)
    {
        if (instance != null)
            type = instance.GetType();
        LogDebug($"Opened: {type.Format()}.");
        if (!ParentTypeInfoIntl.TryGetValue(type!, out UIExtensionParentTypeInfo parentTypeInfo))
        {
            LogWarning($"Unable to find parent type info while opening {type.Format()}.");
            return;
        }

        bool found = false;
        for (int i = 0; i < parentTypeInfo.VanillaInstancesIntl.Count; ++i)
        {
            UIExtensionVanillaInstanceInfo instanceInfo = parentTypeInfo.VanillaInstancesIntl[i];
            if (ReferenceEquals(instanceInfo.Instance, instance))
            {
                if (instanceInfo.IsOpen)
                {
                    LogDebug($"Already open: {type.Format()}.");
                    return;
                }

                found = true;
                instanceInfo.IsOpen = true;
                break;
            }
        }

        if (!found)
        {
            LogWarning($"Unable to find vanilla instance info while opening {type.Format()}.");
            return;
        }
        for (int i = 0; i < ExtensionsIntl.Count; i++)
        {
            UIExtensionInfo info = ExtensionsIntl[i];
            if (info.ParentType != type) continue;

            for (int j = 0; j < info.InstantiationsIntl.Count; j++)
            {
                object instantiation = info.InstantiationsIntl[j];
                if (instantiation is UIExtension ext)
                {
                    try
                    {
                        ext.InvokeOnOpened();
                    }
                    catch (Exception ex)
                    {
                        LogError($"Error invoking OnOpened from {instantiation.GetType().Format()}.", info.Plugin, info.Assembly);
                        LogError(ex, info.Plugin, info.Assembly);
                    }
                }
            }
            LogDebug($"* Invoked Opened: {info.ImplementationType.Format()}.", info.Plugin, info.Assembly);
        }
    }
    private static void OnClosed(Type? type, object? instance)
    {
        if (instance != null)
            type = instance.GetType();
        LogDebug($"Closed: {type.Format()}.");
        if (!ParentTypeInfoIntl.TryGetValue(type!, out UIExtensionParentTypeInfo parentTypeInfo))
        {
            LogWarning($"Unable to find parent type info while closing {type.Format()}.");
            return;
        }

        bool found = false;
        for (int i = 0; i < parentTypeInfo.VanillaInstancesIntl.Count; ++i)
        {
            UIExtensionVanillaInstanceInfo instanceInfo = parentTypeInfo.VanillaInstancesIntl[i];
            if (ReferenceEquals(instanceInfo.Instance, instance))
            {
                if (!instanceInfo.IsOpen)
                {
                    LogDebug($"Already closed: {type.Format()}.");
                    return;
                }

                found = true;
                instanceInfo.IsOpen = false;
                break;
            }
        }
        if (!found)
        {
            LogWarning($"Unable to find vanilla instance info while closing {type.Format()}.");
            return;
        }

        for (int i = 0; i < ExtensionsIntl.Count; i++)
        {
            UIExtensionInfo info = ExtensionsIntl[i];
            if (info.ParentType != type) continue;

            for (int j = 0; j < info.InstantiationsIntl.Count; j++)
            {
                object instantiation = info.InstantiationsIntl[j];
                if (instantiation is UIExtension ext)
                {
                    try
                    {
                        ext.InvokeOnClosed();
                    }
                    catch (Exception ex)
                    {
                        LogError($"Error invoking OnClosed from {instantiation.GetType().Format()}.", info.Plugin, info.Assembly);
                        LogError(ex, info.Plugin, info.Assembly);
                    }
                }
            }
            LogDebug($"* Invoked Closed: {info.ImplementationType.Format()}.", info.Plugin, info.Assembly);
        }
    }
    private static void OnInitialized(Type? type, object? instance)
    {
        if (instance != null)
            type = instance.GetType();
        LogDebug($"Initialized: {type.Format()}.");
        for (int i = 0; i < ExtensionsIntl.Count; i++)
        {
            UIExtensionInfo info = ExtensionsIntl[i];
            if (info.ParentType != type) continue;
            object? ext = CreateExtension(info, instance);
            if (ext == null) continue;
            info.InstantiationsIntl.Add(ext);
            LogDebug($"* Initialized: {info.ImplementationType.Format()}.", info.Plugin, info.Assembly);
            if ((info.TypeInfo.OpenOnInitialize || info.TypeInfo.DefaultOpenState) && instance is UIExtension ext2)
            {
                try
                {
                    ext2.InvokeOnOpened();
                }
                catch (Exception ex)
                {
                    LogError($"Error invoking OnOpened from {ext.GetType().Format()}.", info.Plugin, info.Assembly);
                    LogError(ex, info.Plugin, info.Assembly);
                }
            }
        }
    }
    private static void OnDestroy(Type? type, object? instance)
    {
        if (instance != null)
            type = instance.GetType();
        
        LogDebug($"Destroyed: {type.Format()}.");
        foreach (UITypeInfo otherTypeInfo in UIAccessTools.TypeInfo.Values)
        {
            if (otherTypeInfo.DestroyedByParent && otherTypeInfo.Parent == type)
            {
                LogDebug($"* Destroying child: {otherTypeInfo.Type.Format()}.");
                OnDestroy(otherTypeInfo.Type, null);
            }
        }
        if (!ParentTypeInfoIntl.TryGetValue(type!, out UIExtensionParentTypeInfo parentTypeInfo))
        {
            LogWarning($"Unable to find parent type info while destroying {type.Format()}.");
            return;
        }

        bool close = parentTypeInfo.ParentTypeInfo.CloseOnDestroy;
        if (close)
        {
            bool found = false;
            for (int i = 0; i < parentTypeInfo.VanillaInstancesIntl.Count; ++i)
            {
                UIExtensionVanillaInstanceInfo instanceInfo = parentTypeInfo.VanillaInstancesIntl[i];
                if (ReferenceEquals(instanceInfo.Instance, instance))
                {
                    if (!instanceInfo.IsOpen)
                    {
                        LogDebug($"Already closed: {type.Format()}.");
                        close = false;
                    }
                    else instanceInfo.IsOpen = false;

                    found = true;
                    break;
                }
            }

            if (!found)
            {
                LogWarning($"Unable to find vanilla instance info while closing (destroying) {type.Format()}.");
                close = false;
            }
        }

        for (int i = 0; i < ExtensionsIntl.Count; i++)
        {
            UIExtensionInfo info = ExtensionsIntl[i];
            if (info.ParentType != type) continue;
            for (int j = 0; j < info.InstantiationsIntl.Count; j++)
            {
                object instantiation = info.InstantiationsIntl[j];
                if (close && instantiation is UIExtension ext)
                {
                    try
                    {
                        ext.InvokeOnClosed();
                    }
                    catch (Exception ex)
                    {
                        LogError($"Error invoking OnClosed from {instantiation.GetType().Format()}.", info.Plugin, info.Assembly);
                        LogError(ex, info.Plugin, info.Assembly);
                    }
                }
                if (instantiation is IDisposable disposable)
                {
                    try
                    {
                        disposable.Dispose();
                        LogDebug($"* Disposed: {info.ImplementationType.Format()}.", info.Plugin, info.Assembly);
                    }
                    catch (Exception ex)
                    {
                        LogError($"Error disposing UI extension: {info.ImplementationType.Format()}.", info.Plugin, info.Assembly);
                        LogError(ex, info.Plugin, info.Assembly);
                    }
                }

                _onDestroy?.Invoke(instantiation);
                if (ParentTypeInfo.TryGetValue(info.ParentType, out UIExtensionParentTypeInfo parentInfo))
                    parentInfo.InstancesIntl.RemoveAll(x => x.Instance == instance);
            }
            info.InstantiationsIntl.Clear();
            LogDebug($"* Destroyed Instances: {info.ImplementationType.Format()}.", info.Plugin, info.Assembly);
        }
    }
    internal static bool RegisterExtension(UIExtensionInfo info)
    {
        ThreadUtil.assertIsGameThread();

        try
        {
            InitializeExtension(info);
        }
        catch (Exception ex)
        {
            LogError($"Error initializing UI extension: {info.ImplementationType.Format()}.", info.Plugin, info.Assembly);
            LogError(ex, info.Plugin, info.Assembly);
            return false;
        }

        LogDebug($"Registered UI extension: {info.ImplementationType.Format()}.", info.Plugin, info.Assembly);
        return true;
    }

    internal static bool DeregisterExtension(Type implementation)
    {
        ThreadUtil.assertIsGameThread();
        
        if (ExtensionsDictIntl.TryGetValue(implementation, out UIExtensionInfo extInfo))
        {
            for (int i = 0; i < extInfo.InstantiationsIntl.Count; ++i)
            {
                object instance = extInfo.InstantiationsIntl[i];
                if (instance is IDisposable disposable)
                {
                    try
                    {
                        disposable.Dispose();
                    }
                    catch (Exception ex)
                    {
                        LogError($"Error disposing UI extension: {extInfo.ImplementationType.Format()}.", extInfo.Plugin, extInfo.Assembly);
                        LogError(ex, extInfo.Plugin, extInfo.Assembly);
                    }
                }
                ExtensionsIntl.Remove(extInfo);
                if (ParentTypeInfo.TryGetValue(extInfo.ParentType, out UIExtensionParentTypeInfo parentInfo))
                    parentInfo.InstancesIntl.RemoveAll(x => x.Instance == instance);
            }
            LogDebug($"Deregistered UI extension: {implementation.Format()}.", extInfo.Plugin);
        }

        return true;
    }

    private static void AddToList(UIExtensionInfo info)
    {
        int priority = info.Priority;
        int index = ExtensionsIntl.FindIndex(x => x.Priority <= priority);
        if (index == -1)
            ExtensionsIntl.Add(info);
        else
            ExtensionsIntl.Insert(index, info);
        ExtensionsDictIntl[info.ImplementationType] = info;
    }
    /// <exception cref="AggregateException"></exception>
    private static void InitializeExtension(UIExtensionInfo info)
    {
        if (!info.SuppressUIExtensionParentWarning && !typeof(UIExtension).IsAssignableFrom(info.ImplementationType))
        {
            LogWarning($"It's recommended to derive UI extensions from the {typeof(UIExtension).Format()} class (unlike {info.ImplementationType.Format()}).", info.Plugin, info.Assembly);
            LogInfo($"Alternatively set {"SuppressUIExtensionParentWarning".Colorize(FormattingColorType.Property)} to {true.Format()} in a {typeof(UIExtensionAttribute).Format()} on the extension class.", info.Plugin, info.Assembly);
        }

        if (!UIAccessTools.TryGetUITypeInfo(info.ParentType, out UITypeInfo? typeInfo))
        {
            LogWarning($"No type info for parent UI type: {info.ParentType.Format()}, {info.ImplementationType.Format()} UI extension may not behave as expected. Any warnings below:", info.Plugin, info.Assembly);
        }

        if (typeInfo == null)
        {
            typeInfo = new UITypeInfo(info.ParentType);
            LogInfo($"Created UI type info for {info.ParentType.Format()}: {typeInfo.OpenMethods.Length.Format()} open method(s), " +
                    $"{typeInfo.CloseMethods.Length.Format()} close method(s), {typeInfo.InitializeMethods.Length.Format()} initialize method(s), " +
                    $"{typeInfo.DestroyMethods.Length.Format()} destroy method(s).", info.Plugin, info.Assembly);
        }

        info.TypeInfo = typeInfo;

        bool staticUI = !info.ParentType.GetIsStatic() && typeInfo.IsStaticUI;
        info.IsEmittable = !staticUI;

        List<Exception>? exceptions = null;

        ConstructorInfo[] ctors = info.ImplementationType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        ConstructorInfo? constructor = staticUI ? null : ctors.FirstOrDefault(x => x.GetParameters().Length == 1 && x.GetParameters()[0].ParameterType.IsAssignableFrom(info.ParentType));
        if (constructor == null)
        {
            constructor = ctors.FirstOrDefault(x => x.GetParameters().Length == 0);
            if (constructor == null)
                (exceptions ??= new List<Exception>()).Add(new InvalidOperationException("Type " + info.ImplementationType.Name + " does not have a parameterless constructor or an instance input constructor."));
        }

        foreach (MemberInfo member in info.ImplementationType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                     .Concat<MemberInfo>(info.ImplementationType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                     .Concat(info.ImplementationType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Where(x => !x.IsSpecialName))
                     .OrderByDescending(Accessor.GetPriority)
                     .ThenBy(x => x.Name))
        {
            if (member.IsIgnored())
                continue;
            try
            {
                TryInitializeMember(info, member);
            }
            catch (Exception ex)
            {
                (exceptions ??= new List<Exception>()).Add(ex);
            }
        }

        if (exceptions != null)
            throw new AggregateException($"Failed to initialze UI extension: {info.ImplementationType.Name}.", exceptions);

        try
        {
            DynamicMethod dynMethod = new DynamicMethod("<DS_UIEXT>_CreateExtensionImpl",
                MethodAttributes.Static | MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName, CallingConventions.Standard, typeof(object),
                new Type[] { typeof(object) }, info.ImplementationType, true);
            dynMethod.DefineParameter(0, ParameterAttributes.None, "uiInstance");
            DebuggableEmitter il = new DebuggableEmitter(dynMethod);

            il.Comment("Type: " + info.ImplementationType.Format() + ".");

            il.DeclareLocal(info.ImplementationType); // 0
            il.DeclareLocal(info.ParentType); // 1

            Label useProvidedLocal1 = il.DefineLabel();
            Label setLocal1 = il.DefineLabel();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Brtrue_S, useProvidedLocal1);
            if (!staticUI)
            {
                try
                {
                    UIAccessTools.LoadUIToILGenerator(il, info.ParentType);
                }
                catch (InvalidOperationException)
                {
                    il.Emit(OpCodes.Ldnull);
                }
            }
            else
                il.Emit(OpCodes.Ldnull);
            il.Emit(OpCodes.Br, setLocal1);
            il.MarkLabel(useProvidedLocal1);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Castclass, info.ParentType);
            il.MarkLabel(setLocal1);
            il.Emit(OpCodes.Stloc_1);

            il.Emit(OpCodes.Ldtoken, info.ImplementationType);
            il.Emit(OpCodes.Call, new Func<RuntimeTypeHandle, Type>(Type.GetTypeFromHandle).Method);
            il.Emit(OpCodes.Call, new Func<Type, object>(FormatterServices.GetUninitializedObject).Method);
            il.Emit(OpCodes.Castclass, info.ImplementationType);
            il.Emit(OpCodes.Stloc_0);

            if (typeof(UIExtension).IsAssignableFrom(info.ImplementationType))
            {
                MethodInfo? setter = typeof(UIExtension).GetProperty(nameof(UIExtension.Instance), BindingFlags.Public | BindingFlags.Instance)?.GetSetMethod(true);
                if (setter != null)
                {
                    il.Emit(OpCodes.Ldloc_0);
                    il.Emit(OpCodes.Ldloc_1);
                    il.Emit(OpCodes.Call, setter);
                }
            }

            foreach (UIExistingMemberInfo member in info.ExistingMembers)
            {
                if (!member.IsInitialized) continue;
                if (!member.Member.GetIsStatic())
                    il.Emit(OpCodes.Ldloc_0);

                if (!member.ExistingIsStatic)
                    il.Emit(OpCodes.Ldloc_1);

                member.EmitGet(il);
            }

            il.Emit(OpCodes.Ldloc_0);
            if (constructor!.GetParameters().Length == 1)
                il.Emit(OpCodes.Ldloc_1);
            il.Emit(OpCodes.Call, constructor);
            
            il.Emit(OpCodes.Ldloc_1);
            il.Emit(OpCodes.Ldloc_0);
            il.Emit(OpCodes.Call, AddInstanceMethod);

            il.Emit(OpCodes.Ldloc_0);
            il.Emit(OpCodes.Ret);

            info.CreateCallback = (CreateUIExtension)dynMethod.CreateDelegate(typeof(CreateUIExtension));
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to create instantiation method for UI extension: {info.ImplementationType.Name}.", ex);
        }

        try
        {
            InitializeParentPatches(info);
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to patch for UI parent: {info.ParentType.Name} (while patching {info.ImplementationType.Name}).", ex);
        }

        AddToList(info);

        try
        {
            InitializeExtensionPatches(info);
        }
        catch (Exception ex)
        {
            try
            {
                DeregisterExtension(info.ImplementationType);
            }
            catch (Exception)
            {
                // ignored
            }
            throw new Exception($"Failed to patch for UI extension: {info.ImplementationType.Name}.", ex);
        }
    }
    private static readonly MethodInfo AddInstanceMethod = typeof(UIExtensionManager).GetMethod(nameof(AddInstance), BindingFlags.NonPublic | BindingFlags.Static)!;
    [UsedImplicitly]
    private static void AddInstance(object? uiInstance, object instance)
    {
        UIExtensionParentTypeInfo? parentInfo = null;
        Type? type;
        UIExtensionInfo? extInfo = null;
        if (uiInstance == null)
        {
            type = instance.GetType();
            for (; type != null && !ExtensionsDictIntl.TryGetValue(type, out extInfo); type = type.BaseType) { }
            type = extInfo?.ParentType;
        }
        else type = uiInstance.GetType();

        for (; type != null && !ParentTypeInfoIntl.TryGetValue(type, out parentInfo); type = type.BaseType) { }

        if (parentInfo == null)
        {
            LogWarning($"Failed to find parent info for extension: {instance.GetType().Format()}.", extInfo?.Plugin, extInfo?.Assembly);
            return;
        }

        UIExtensionVanillaInstanceInfo? info = null;
        for (int i = 0; i < parentInfo.VanillaInstancesIntl.Count; ++i)
        {
            if (ReferenceEquals(parentInfo.VanillaInstancesIntl[i].Instance, uiInstance))
            {
                info = parentInfo.VanillaInstancesIntl[i];
                LogDebug($"Found vanilla instance info: {parentInfo.ParentType.Format()}.");
                break;
            }
        }

        if (info == null)
        {
            info = new UIExtensionVanillaInstanceInfo(uiInstance, parentInfo.ParentTypeInfo.OpenOnInitialize || parentInfo.ParentTypeInfo.DefaultOpenState);
            parentInfo.VanillaInstancesIntl.Add(info);
            LogDebug($"Added vanilla instance info: {parentInfo.ParentType.Format()}.");
        }
        
        parentInfo.InstancesIntl.Add(new UIExtensionInstanceInfo(instance, info));
        _onAdd?.Invoke(instance);
    }
    internal static object? CreateExtension(UIExtensionInfo info, object? uiInstance)
    {
        try
        {
            object? instance = info.CreateCallback(uiInstance);

            LogDebug($"Created {instance.Format()} for {info.ParentType.Format()}.", info.Plugin, info.Assembly);

            return instance;
        }
        catch (Exception ex)
        {
            LogError($"Error initializing {info.ImplementationType.Format()}.", info.Plugin, info.Assembly);
            LogError(ex, info.Plugin, info.Assembly);
            return null;
        }
    }
    internal static void TellDestroyed(object instance)
    {
        OnDestroy(null, instance);
    }
    private static void TryInitializeMember(UIExtensionInfo info, MemberInfo member)
    {
        if (Attribute.GetCustomAttribute(member, typeof(ExistingUIMemberAttribute)) is not ExistingUIMemberAttribute existingMemberAttribute)
            return;

        FieldInfo? field = member as FieldInfo;
        PropertyInfo? property = member as PropertyInfo;
        MethodInfo? method = member as MethodInfo;

        if (field == null && property == null && method == null)
            return;

        bool isStatic = member.GetIsStatic();
        if (isStatic)
            throw new Exception($"UI extensions should not have static existing members, such as {member.Name}.");

        Type owningType = existingMemberAttribute.OwningType ?? info.ParentType;

        FieldInfo? existingField = owningType.GetField(existingMemberAttribute.MemberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy);
        PropertyInfo? existingProperty = owningType.GetProperty(existingMemberAttribute.MemberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy);
        MethodInfo? existingMethod = owningType.GetMethod(existingMemberAttribute.MemberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy,
            null, CallingConventions.Any, Array.Empty<Type>(), null);

        if (existingField == null &&
            (existingProperty == null ||
             existingProperty.GetSetMethod(true) == null && existingProperty.GetGetMethod(true) == null ||
             existingProperty.GetIndexParameters().Length > 0) &&
            (existingMethod == null || existingMethod.ReturnType == typeof(void) || existingMethod.GetParameters().Length > 0))
        {
            throw new MemberAccessException($"Unable to match \"{owningType.FullName}.{existingMemberAttribute.MemberName}\" to a field, get-able property, or no-argument, non-void-returning method.");
        }
        MemberInfo existingMember = ((MemberInfo?)existingField ?? existingProperty) ?? existingMethod!;

        Type existingMemberType = existingMember.GetMemberType()!;
        Type memberType = member.GetMemberType()!;

        if (method != null)
        {
            ParameterInfo[] parameters = method.GetParameters();
            if (method.ReturnType == typeof(void))
            {
                // setter
                if (parameters.Length == 0 || !parameters[0].ParameterType.IsAssignableFrom(existingMemberType))
                    throw new Exception($"Unable to assign existing method parameter type: \"{existingMemberType.FullName}\" to expected member type: \"{parameters[0].ParameterType.FullName}\".");
            }
            else
            {
                // getter
                if (parameters.Length != 0 || !memberType.IsAssignableFrom(existingMemberType))
                    throw new Exception($"Unable to assign existing method parameter type: \"{existingMemberType.FullName}\" to expected member type: \"{memberType.FullName}\".");
            }
        }
        else if (!memberType.IsAssignableFrom(existingMemberType))
            throw new Exception($"Unable to assign existing member type: \"{existingMemberType.FullName}\" to expected member type: \"{memberType.FullName}\".");
        
        bool existingIsStatic = existingMember.GetIsStatic();

        if (!existingIsStatic && info.TypeInfo.IsStaticUI)
            throw new InvalidOperationException($"Requeste instance variable ({existingMember.Name}) from static UI: {info.ParentType.Name}.");

        bool initialized;
        if (existingMemberAttribute.InitializeMode is ExistingMemberInitializeMode.InitializeOnConstruct or ExistingMemberInitializeMode.PatchGetter)
        {
            initialized = existingMemberAttribute.InitializeMode == ExistingMemberInitializeMode.InitializeOnConstruct;
            if (!initialized && field != null)
            {
                LogWarning($"Fields can not be non-initialized (as indicated for {field.Format()}).", info.Plugin, info.Assembly);
                initialized = true;
            }
            if (initialized && property != null && property.GetSetMethod(true) == null)
            {
                LogWarning($"Properties without a setter can not be initialized (as indicated for {property.Format()}).", info.Plugin, info.Assembly);
                initialized = false;
            }
            if (!initialized && method != null && method.ReturnType == typeof(void))
            {
                LogWarning($"Void-returning methods can not be non-initialized (as indicated for {method.Format()}).", info.Plugin, info.Assembly);
                initialized = true;
            }
            if (initialized && method != null && method.GetParameters().Length == 0)
            {
                LogWarning($"Parameterless methods can not be initialized (as indicated for {method.Format()}).", info.Plugin, info.Assembly);
                initialized = false;
            }
            LogDebug($"Set initialized setting for existing member: {member.Format()}: {initialized.Format()}.", info.Plugin, info.Assembly);
        }
        else
        {
            if (field != null)
                initialized = true;
            else if (property != null)
                initialized = property.GetSetMethod(true) != null;
            else if (method!.ReturnType == typeof(void))
                initialized = true;
            else
                initialized = false;
            LogDebug($"Assumed initialized setting for existing member: {member.Format()}: {initialized.Format()}.", info.Plugin, info.Assembly);
        }

        if (!initialized && existingProperty != null && existingProperty.GetSetMethod(true) != null)
            LogWarning($"Setter on {existingProperty.Format()} can not be used to set the original value.", info.Plugin, info.Assembly);

        info.ExistingMembersIntl.Add(new UIExistingMemberInfo(member, existingMember, existingIsStatic, initialized));
    }
    private static void InitializeExtensionPatches(UIExtensionInfo info)
    {
        for (int i = 0; i < info.ExistingMembersIntl.Count; ++i)
        {
            UIExistingMemberInfo member = info.ExistingMembersIntl[i];
            if (member.IsInitialized || member.Member is not PropertyInfo property)
            {
                LogDebug($"Skipping initialized member: {member.Member.Format()}.", info.Plugin, info.Assembly);
                continue;
            }
            MethodInfo? getter = property.GetGetMethod(true);
            MethodInfo? setter = property.GetSetMethod(true);
            if (getter == null)
            {
                LogWarning($"Unable to find getter for {property.Format()}.", info.Plugin, info.Assembly);
                continue;
            }
            if (setter == null)
                LogDebug($"Unable to find setter for {property.Format()}, not an issue.", info.Plugin, info.Assembly);
            if (Patches.ContainsKey(getter))
            {
                LogDebug($"{getter.Format()} has already been transpiled.", info.Plugin, info.Assembly);
            }
            else
            {
                UIExtensionExistingMemberPatchInfo patchInfo = new UIExtensionExistingMemberPatchInfo(info, member);
                PatchInfo[getter] = patchInfo;
                MethodInfo transpiler = TranspileGetterPropertyMethod;
                PatchesMain.Patcher.Patch(getter, transpiler: new HarmonyMethod(transpiler));
                Patches.Add(getter, new UIExtensionPatch(getter, transpiler, HarmonyPatchType.Transpiler));
            }
            
            if (setter != null)
            {
                if (Patches.ContainsKey(setter))
                {
                    LogDebug($"{setter.Format()} has already been transpiled.", info.Plugin, info.Assembly);
                }
                else
                {
                    UIExtensionExistingMemberPatchInfo patchInfo = new UIExtensionExistingMemberPatchInfo(info, member);
                    PatchInfo[setter] = patchInfo;
                    MethodInfo transpiler = TranspileSetterPropertyMethod;
                    PatchesMain.Patcher.Patch(setter, transpiler: new HarmonyMethod(transpiler));
                    Patches.Add(setter, new UIExtensionPatch(setter, transpiler, HarmonyPatchType.Transpiler));
                }
            }
        }
    }
    private static UIExtensionParentTypeInfo GetOrAddParentTypeInfo(Type parentType, UITypeInfo typeInfo)
    {
        if (!ParentTypeInfoIntl.TryGetValue(parentType, out UIExtensionParentTypeInfo parentTypeInfo))
        {
            parentTypeInfo = new UIExtensionParentTypeInfo(parentType, typeInfo);
            ParentTypeInfoIntl.Add(parentType, parentTypeInfo);
        }
        return parentTypeInfo;
    }
    private static void InitializeParentPatches(UIExtensionInfo info)
    {
        UIExtensionParentTypeInfo parentTypeInfo = GetOrAddParentTypeInfo(info.ParentType, info.TypeInfo);

        PatchParentOnOpen(info.TypeInfo, parentTypeInfo, info.Plugin, info.Assembly);
        PatchParentOnClose(info.TypeInfo, parentTypeInfo, info.Plugin, info.Assembly);
        PatchParentOnInitialize(info.TypeInfo, parentTypeInfo, info.Plugin, info.Assembly);
        PatchParentOnDestroy(info.TypeInfo, parentTypeInfo, info.Plugin, info.Assembly);
    }
    private static void PatchParentOnOpen(UITypeInfo typeInfo, UIExtensionParentTypeInfo parentTypeInfo, IDevkitServerPlugin? plugin, Assembly assembly)
    {
        if (typeInfo.CustomOnOpen != null)
        {
            if (!typeInfo.CustomOnOpen.HasBeenInitialized)
            {
                typeInfo.CustomOnOpen.Patch();
                typeInfo.CustomOnOpen.HasBeenInitialized = true;
            }
            if (!typeInfo.CustomOnOpen.HasOnOpenBeenInitialized)
            {
                typeInfo.CustomOnOpen.OnOpened += OnOpened;
                typeInfo.CustomOnOpen.HasOnOpenBeenInitialized = true;
            }
        }
        else if (!typeInfo.OpenOnInitialize)
        {
            foreach (UIVisibilityMethodInfo openMethod in typeInfo.OpenMethods)
            {
                if (parentTypeInfo.OpenPatchesIntl.Any(x => x.Original == openMethod.Method))
                {
                    LogDebug($"Skipped finalizer for {openMethod.Method.Format()}, already done from this extension.", plugin, assembly);
                    continue;
                }

                if (Patches.TryGetValue(openMethod.Method, out UIExtensionPatch patchInfo))
                {
                    LogDebug($"Skipped finalizer for {openMethod.Method.Format()}, already done from another extension.", plugin, assembly);
                    parentTypeInfo.OpenPatchesIntl.Add(patchInfo);
                    continue;
                }

                if (openMethod.Method.DeclaringType == typeInfo.Type)
                {
                    MethodInfo finalizer = openMethod.IsStatic
                        ? StaticOpenMethodFinalizerMethod
                        : InstanceOpenMethodFinalizerMethod;
                    PatchesMain.Patcher.Patch(openMethod.Method, finalizer: new HarmonyMethod(finalizer));
                    patchInfo = new UIExtensionPatch(openMethod.Method, finalizer, HarmonyPatchType.Finalizer);
                    parentTypeInfo.OpenPatchesIntl.Add(patchInfo);
                    Patches.Add(openMethod.Method, patchInfo);
                    LogDebug($"Added finalizer for {openMethod.Method.Format()}: {finalizer.Format()}.", plugin, assembly);
                }
                else if (openMethod.Method.IsStatic)
                {
                    throw new InvalidOperationException("Can't patch an open method from another class.");
                }
            }
        }
    }
    private static void PatchParentOnClose(UITypeInfo typeInfo, UIExtensionParentTypeInfo parentTypeInfo, IDevkitServerPlugin? plugin, Assembly assembly)
    {
        if (typeInfo.CustomOnClose != null)
        {
            if (!typeInfo.CustomOnClose.HasBeenInitialized)
            {
                typeInfo.CustomOnClose.Patch();
                typeInfo.CustomOnClose.HasBeenInitialized = true;
            }
            if (!typeInfo.CustomOnClose.HasOnCloseBeenInitialized)
            {
                typeInfo.CustomOnClose.OnClose += OnClosed;
                typeInfo.CustomOnClose.HasOnCloseBeenInitialized = true;
            }
        }
        else if (!typeInfo.CloseOnDestroy)
        {
            foreach (UIVisibilityMethodInfo closeMethod in typeInfo.CloseMethods)
            {
                if (parentTypeInfo.ClosePatchesIntl.Any(x => x.Original == closeMethod.Method))
                {
                    LogDebug($"Skipped finalizer for {closeMethod.Method.Format()}, already done from this extension.", plugin, assembly);
                    continue;
                }

                if (Patches.TryGetValue(closeMethod.Method, out UIExtensionPatch patchInfo))
                {
                    LogDebug($"Skipped finalizer for {closeMethod.Method.Format()}, already done from another extension.", plugin, assembly);
                    parentTypeInfo.ClosePatchesIntl.Add(patchInfo);
                    continue;
                }

                if (closeMethod.Method.DeclaringType == typeInfo.Type)
                {
                    MethodInfo finalizer = closeMethod.IsStatic
                        ? StaticCloseMethodFinalizerMethod
                        : InstanceCloseMethodFinalizerMethod;
                    PatchesMain.Patcher.Patch(closeMethod.Method, finalizer: new HarmonyMethod(finalizer));
                    patchInfo = new UIExtensionPatch(closeMethod.Method, finalizer, HarmonyPatchType.Finalizer);
                    parentTypeInfo.ClosePatchesIntl.Add(patchInfo);
                    Patches.Add(closeMethod.Method, patchInfo);
                    LogDebug($"Added finalizer for {closeMethod.Method.Format()}: {finalizer.Format()}.", plugin, assembly);
                }
                else if (closeMethod.Method.IsStatic)
                {
                    throw new InvalidOperationException("Can't patch a close method from another class.");
                }
            }
        }
    }
    private static void PatchParentOnInitialize(UITypeInfo typeInfo, UIExtensionParentTypeInfo parentTypeInfo, IDevkitServerPlugin? plugin, Assembly assembly)
    {
        if (typeInfo.CustomOnInitialize != null)
        {
            if (!typeInfo.CustomOnInitialize.HasBeenInitialized)
            {
                typeInfo.CustomOnInitialize.Patch();
                typeInfo.CustomOnInitialize.HasBeenInitialized = true;
            }
            if (!typeInfo.CustomOnInitialize.HasOnInitializeBeenInitialized)
            {
                typeInfo.CustomOnInitialize.OnInitialize += OnInitialized;
                typeInfo.CustomOnInitialize.HasOnInitializeBeenInitialized = true;
            }
        }
        else
        {
            foreach (UIVisibilityMethodInfo initializeMethod in typeInfo.InitializeMethods)
            {
                if (parentTypeInfo.InitializePatchesIntl.Any(x => x.Original == initializeMethod.Method))
                {
                    LogDebug($"Skipped finalizer for {initializeMethod.Method.Format()}, already done from this extension.", plugin, assembly);
                    continue;
                }

                if (Patches.TryGetValue(initializeMethod.Method, out UIExtensionPatch patchInfo))
                {
                    LogDebug($"Skipped finalizer for {initializeMethod.Method.Format()}, already done from another extension.", plugin, assembly);
                    parentTypeInfo.InitializePatchesIntl.Add(patchInfo);
                    continue;
                }

                if (initializeMethod.Method.DeclaringType == typeInfo.Type)
                {
                    MethodInfo finalizer = InstanceInitializeMethodFinalizerMethod;
                    PatchesMain.Patcher.Patch(initializeMethod.Method, finalizer: new HarmonyMethod(finalizer));
                    patchInfo = new UIExtensionPatch(initializeMethod.Method, finalizer, HarmonyPatchType.Finalizer);
                    parentTypeInfo.InitializePatchesIntl.Add(patchInfo);
                    Patches.Add(initializeMethod.Method, patchInfo);
                    LogDebug($"Added finalizer for {initializeMethod.Method.Format()}: {finalizer.Format()}.", plugin, assembly);
                }
                else if (initializeMethod.Method.IsStatic)
                {
                    throw new InvalidOperationException("Can't patch a initialize method from another class.");
                }
            }
        }
    }
    private static void PatchParentOnDestroy(UITypeInfo typeInfo, UIExtensionParentTypeInfo parentTypeInfo, IDevkitServerPlugin? plugin, Assembly assembly)
    {
        if (typeInfo.DestroyedByParent && typeInfo.Parent != null && UIAccessTools.TryGetUITypeInfo(typeInfo.Parent, out UITypeInfo parentUITypeInfo))
            PatchParentOnDestroy(parentUITypeInfo, GetOrAddParentTypeInfo(parentUITypeInfo.Type, typeInfo), plugin, assembly);
        
        if (typeInfo.CustomOnDestroy != null)
        {
            if (!typeInfo.CustomOnDestroy.HasBeenInitialized)
            {
                typeInfo.CustomOnDestroy.Patch();
                typeInfo.CustomOnDestroy.HasBeenInitialized = true;
            }
            if (!typeInfo.CustomOnDestroy.HasOnDestroyBeenInitialized)
            {
                typeInfo.CustomOnDestroy.OnDestroy += OnDestroy;
                typeInfo.CustomOnDestroy.HasOnDestroyBeenInitialized = true;
            }
        }
        else if (typeInfo is not { DestroyOnClose: true, CloseOnDestroy: false })
        {
            foreach (UIVisibilityMethodInfo destroyMethod in typeInfo.DestroyMethods)
            {
                if (parentTypeInfo.DestroyPatchesIntl.Any(x => x.Original == destroyMethod.Method))
                {
                    LogDebug($"[{Source}] Skipped finalizer for {destroyMethod.Method.Format()}, already done from this extension.", plugin, assembly);
                    continue;
                }

                if (Patches.TryGetValue(destroyMethod.Method, out UIExtensionPatch patchInfo))
                {
                    LogDebug($"[{Source}] Skipped finalizer for {destroyMethod.Method.Format()}, already done from another extension.", plugin, assembly);
                    parentTypeInfo.DestroyPatchesIntl.Add(patchInfo);
                    continue;
                }

                if (destroyMethod.Method.DeclaringType == typeInfo.Type)
                {
                    if (destroyMethod.Method.GetParameters().Length == 0)
                    {
                        MethodInfo prefix = destroyMethod.IsStatic
                            ? StaticDestroyMethodPrefixMethod
                            : InstanceDestroyMethodPrefixMethod;
                        PatchesMain.Patcher.Patch(destroyMethod.Method, prefix: new HarmonyMethod(prefix));
                        patchInfo = new UIExtensionPatch(destroyMethod.Method, prefix, HarmonyPatchType.Prefix);
                        LogDebug($"[{Source}] Added prefix for {destroyMethod.Method.Format()}: {prefix.Format()}.", plugin, assembly);
                    }
                    else
                    {
                        MethodInfo finalizer = destroyMethod.IsStatic
                            ? StaticDestroyMethodFinalizerMethod
                            : InstanceDestroyMethodFinalizerMethod;
                        PatchesMain.Patcher.Patch(destroyMethod.Method, finalizer: new HarmonyMethod(finalizer));
                        patchInfo = new UIExtensionPatch(destroyMethod.Method, finalizer, HarmonyPatchType.Finalizer);
                        LogDebug($"[{Source}] Added finalizer for {destroyMethod.Method.Format()}: {finalizer.Format()}.", plugin, assembly);
                    }
                    parentTypeInfo.DestroyPatchesIntl.Add(patchInfo);
                    Patches.Add(destroyMethod.Method, patchInfo);
                }
                else if (destroyMethod.Method.IsStatic)
                {
                    throw new InvalidOperationException("Can't patch a destroy method from another class.");
                }
            }
        }
    }

    private static readonly MethodInfo StaticOpenMethodFinalizerMethod = typeof(UIExtensionManager).GetMethod(nameof(StaticOpenMethodFinalizer), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static void StaticOpenMethodFinalizer(MethodBase __originalMethod, bool __runOriginal, Exception? __exception)
    {
        if (!__runOriginal || __exception != null)
            return;
        OnOpened(__originalMethod.DeclaringType!, null);
    }

    private static readonly MethodInfo StaticCloseMethodFinalizerMethod = typeof(UIExtensionManager).GetMethod(nameof(StaticCloseMethodFinalizer), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static void StaticCloseMethodFinalizer(MethodBase __originalMethod, bool __runOriginal, Exception? __exception)
    {
        if (!__runOriginal || __exception != null)
            return;
        OnClosed(__originalMethod.DeclaringType!, null);
    }

    private static readonly MethodInfo StaticDestroyMethodPrefixMethod = typeof(UIExtensionManager).GetMethod(nameof(StaticDestroyMethodPrefix), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static void StaticDestroyMethodPrefix(MethodBase __originalMethod, bool __runOriginal)
    {
        if (!__runOriginal)
            return;
        OnDestroy(__originalMethod.DeclaringType!, null);
    }

    private static readonly MethodInfo StaticDestroyMethodFinalizerMethod = typeof(UIExtensionManager).GetMethod(nameof(StaticDestroyMethodFinalizer), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static void StaticDestroyMethodFinalizer(MethodBase __originalMethod, bool __runOriginal, Exception? __exception)
    {
        if (!__runOriginal || __exception != null)
            return;
        OnDestroy(__originalMethod.DeclaringType!, null);
    }

    private static readonly MethodInfo InstanceOpenMethodFinalizerMethod = typeof(UIExtensionManager).GetMethod(nameof(InstanceOpenMethodFinalizer), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static void InstanceOpenMethodFinalizer(object __instance, bool __runOriginal, Exception? __exception)
    {
        if (!__runOriginal || __exception != null)
            return;
        OnOpened(null, __instance);
    }

    private static readonly MethodInfo InstanceCloseMethodFinalizerMethod = typeof(UIExtensionManager).GetMethod(nameof(InstanceCloseMethodFinalizer), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static void InstanceCloseMethodFinalizer(object __instance, bool __runOriginal, Exception? __exception)
    {
        if (!__runOriginal || __exception != null)
            return;
        OnClosed(null, __instance);
    }


    private static readonly MethodInfo InstanceDestroyMethodFinalizerMethod = typeof(UIExtensionManager).GetMethod(nameof(InstanceDestroyMethodFinalizer), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static void InstanceDestroyMethodFinalizer(object __instance, bool __runOriginal, Exception? __exception)
    {
        if (!__runOriginal || __exception != null)
            return;
        OnDestroy(null, __instance);
    }

    private static readonly MethodInfo InstanceDestroyMethodPrefixMethod = typeof(UIExtensionManager).GetMethod(nameof(InstanceDestroyMethodPrefix), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static void InstanceDestroyMethodPrefix(object __instance, bool __runOriginal)
    {
        if (!__runOriginal)
            return;
        OnDestroy(null, __instance);
    }

    private static readonly MethodInfo InstanceInitializeMethodFinalizerMethod = typeof(UIExtensionManager).GetMethod(nameof(InstanceInitializeMethodFinalizer), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static void InstanceInitializeMethodFinalizer(MethodBase __originalMethod, object __instance, bool __runOriginal, Exception? __exception)
    {
        if (!__runOriginal || __exception != null || __originalMethod.DeclaringType != __instance.GetType())
            return;
        OnInitialized(null, __instance);
    }
    
    private static readonly MethodInfo TranspileGetterPropertyMethod = typeof(UIExtensionManager).GetMethod(nameof(TranspileGetterProperty), BindingFlags.NonPublic | BindingFlags.Static)!;

    [UsedImplicitly]
    private static IEnumerable<CodeInstruction> TranspileGetterProperty(IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase method)
    {
        Type declType = method.DeclaringType!;
        if (!PatchInfo.TryGetValue(method, out UIExtensionExistingMemberPatchInfo info))
        {
            LogWarning($"Unable to patch {method.Format()}: Could not find existing member info for {declType.Format()}.", null, method.DeclaringType?.Assembly);
            return PatchUtil.Throw<InvalidOperationException>($"Could not find existing member info for {declType.Name}.");
        }
        List<CodeInstruction> inst = new List<CodeInstruction>();

        DebuggableEmitter il = new DebuggableEmitter(generator, method, inst);

        if (!info.MemberInfo.ExistingIsStatic)
        {
            if (typeof(UIExtension).IsAssignableFrom(info.Extension.ImplementationType) && typeof(UIExtension).GetProperty(nameof(UIExtension.Instance), BindingFlags.Instance | BindingFlags.Public)?.GetGetMethod(true) is { } prop)
            {
                // this.Instance
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Call, prop);
                il.Emit(OpCodes.Castclass, info.Extension.ParentType);
            }
            else
            {
                UIAccessTools.LoadUIToILGenerator(il, info.Extension.ParentType);
            }
        }
        
        info.MemberInfo.EmitGet(il, true);

        il.Emit(OpCodes.Ret);
        LogDebug($"[{Source}] Transpiled {method.Format()} for extension for {info.Extension.ParentType.Format()}.", info.Extension.Plugin, info.Extension.Assembly);
        return inst;
    }
    private static readonly MethodInfo TranspileSetterPropertyMethod = typeof(UIExtensionManager).GetMethod(nameof(TranspileSetterProperty), BindingFlags.NonPublic | BindingFlags.Static)!;

    [UsedImplicitly]
    private static IEnumerable<CodeInstruction> TranspileSetterProperty(IEnumerable<CodeInstruction> instructions, MethodBase method) => PatchUtil.Throw<NotImplementedException>($"{method.Name.Replace("set_", "")} can not have a setter, as it is a UI extension implementation.");
    private class UIExtensionExistingMemberPatchInfo
    {
        public UIExtensionInfo Extension { get; }
        public UIExistingMemberInfo MemberInfo { get; }
        public UIExtensionExistingMemberPatchInfo(UIExtensionInfo extension, UIExistingMemberInfo memberInfo)
        {
            Extension = extension;
            MemberInfo = memberInfo;
        }
    }
    private static class InstanceCache<T> where T : class
    {
        public static T? Instance;
        static InstanceCache()
        {
            Recache();
            _onDestroy += OnDestroyed;
            _onAdd += OnAdded;
        }
        private static void Recache()
        {
            UIExtensionInfo? info = Extensions.FirstOrDefault(x => x.ImplementationType == typeof(T));

            if (info == null)
                return;
            Instance = info.Instantiations.OfType<T>().LastOrDefault();
        }
        private static void OnDestroyed(object instance)
        {
            if (ReferenceEquals(Instance, instance))
                Instance = null;
            Recache();
        }
        private static void OnAdded(object obj)
        {
            if (obj is T)
                Recache();
        }
    }
}
#endif