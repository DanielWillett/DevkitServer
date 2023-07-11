using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using DevkitServer.API;
using DevkitServer.Patches;
using HarmonyLib;
using StackCleaner;
using Action = System.Action;

namespace DevkitServer.Util;
internal static class Accessor
{
    private static Assembly? _sdgAssembly;
    private static Assembly? _devkitServerAssembly;
    private static Assembly? _mscorlibAssembly;

    private static MethodInfo? _getRealtimeSinceStartup;
    private static MethodInfo? _getRealtimeSinceStartupAsDouble;
    private static MethodInfo? _getTime;
    private static MethodInfo? _getDeltaTime;
    private static MethodInfo? _getFixedDeltaTime;
    private static MethodInfo? _getGameObjectTransform;
    private static MethodInfo? _getComponentTransform;
    private static MethodInfo? _getComponentGameObject;
    private static MethodInfo? _getIsServer;
    private static MethodInfo? _getIsEditor;
    private static MethodInfo? _getIsDevkitServer;
    private static MethodInfo? _logDebug;
    private static MethodInfo? _getKeyDown;
    private static MethodInfo? _getKeyUp;
    private static MethodInfo? _getKey;
    private static MethodInfo? _concat2Strings;
    private static MethodInfo? _getStackTraceString;

    private static FieldInfo? _getStackCleaner;

    private static ConstructorInfo? _castExCtor;
    private static ConstructorInfo? _stackTraceIntCtor;

    internal static Type[]? FuncTypes;
    internal static Type[]? ActionTypes;
    private static bool _castExCtorCalc;
    public static InstanceSetter<TInstance, TValue>? GenerateInstanceSetter<TInstance, TValue>(string fieldName, BindingFlags flags = BindingFlags.NonPublic, bool throwOnError = false)
    {
        try
        {
            flags |= BindingFlags.Instance;
            flags &= ~BindingFlags.Static;
            FieldInfo? field = typeof(TInstance).GetField(fieldName, flags);
            if (field is null || field.IsStatic || !field.FieldType.IsAssignableFrom(typeof(TValue)))
                throw new FieldAccessException("Field not found or invalid.");
            const MethodAttributes attr = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig;
            DynamicMethod method = new DynamicMethod("set_" + fieldName, attr, CallingConventions.HasThis, typeof(void), new Type[] { typeof(TInstance), field.FieldType }, typeof(TInstance), true);
            method.DefineParameter(1, ParameterAttributes.None, "value");
            ILGenerator il = method.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Stfld, field);
            il.Emit(OpCodes.Ret);
            return (InstanceSetter<TInstance, TValue>)method.CreateDelegate(typeof(InstanceSetter<TInstance, TValue>));
        }
        catch (Exception ex)
        {
            Logger.LogError("Error generating instance setter for " + typeof(TInstance).Format() + "." + fieldName.Colorize(Color.red) + ".");
            Logger.LogError(ex);
            if (throwOnError)
                throw;
            return null;
        }
    }
    public static InstanceGetter<TInstance, TValue>? GenerateInstanceGetter<TInstance, TValue>(string fieldName, BindingFlags flags = BindingFlags.NonPublic, bool throwOnError = false)
    {
        try
        {
            flags |= BindingFlags.Instance;
            flags &= ~BindingFlags.Static;
            FieldInfo? field = typeof(TInstance).GetField(fieldName, flags);
            if (field is null || field.IsStatic || !typeof(TValue).IsAssignableFrom(field.FieldType))
                throw new FieldAccessException("Field not found or invalid.");
            const MethodAttributes attr = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig;
            DynamicMethod method = new DynamicMethod("get_" + fieldName, attr, CallingConventions.HasThis, typeof(TValue), new Type[] { typeof(TInstance) }, typeof(TInstance), true);
            ILGenerator il = method.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, field);
            il.Emit(OpCodes.Ret);
            return (InstanceGetter<TInstance, TValue>)method.CreateDelegate(typeof(InstanceGetter<TInstance, TValue>));
        }
        catch (Exception ex)
        {
            Logger.LogError("Error generating instance getter for " + typeof(TInstance).Format() + "." + fieldName.Colorize(Color.red) + ".");
            Logger.LogError(ex);
            if (throwOnError)
                throw;
            return null;
        }
    }
    public static StaticSetter<TValue>? GenerateStaticSetter<TInstance, TValue>(string fieldName, BindingFlags flags = BindingFlags.NonPublic, bool throwOnError = false)
        => GenerateStaticSetter<TValue>(typeof(TInstance), fieldName, flags, throwOnError);
    public static StaticGetter<TValue>? GenerateStaticGetter<TInstance, TValue>(string fieldName, BindingFlags flags = BindingFlags.NonPublic, bool throwOnError = false)
        => GenerateStaticGetter<TValue>(typeof(TInstance), fieldName, flags, throwOnError);
    public static InstanceSetter<object, TValue>? GenerateInstanceSetter<TValue>(Type instance, string fieldName, BindingFlags flags = BindingFlags.NonPublic, bool throwOnError = false)
    {
        if (instance == null)
        {
            string message = $"Error generating instance setter for <unknown>.{fieldName}. Instance type not found.";
            Logger.LogError(message);
            if (throwOnError)
                throw new Exception(message);
            return null;
        }
        try
        {
            flags |= BindingFlags.Instance;
            flags &= ~BindingFlags.Static;
            FieldInfo? field = instance.GetField(fieldName, flags);
            if (field is null || field.IsStatic || !field.FieldType.IsAssignableFrom(typeof(TValue)))
                throw new FieldAccessException("Field not found or invalid.");
            const MethodAttributes attr = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig;
            DynamicMethod method = new DynamicMethod("set_" + fieldName, attr, CallingConventions.HasThis, typeof(void), new Type[] { typeof(object), field.FieldType }, instance, true);
            method.DefineParameter(1, ParameterAttributes.None, "value");
            ILGenerator il = method.GetILGenerator();
            Label lbl = il.DefineLabel();
            il.Emit(OpCodes.Ldarg_0);
            if (_castExCtorCalc)
            {
                _castExCtorCalc = true;
                _castExCtor = typeof(InvalidCastException).GetConstructor(BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(string) }, null)!;
            }
            if (_castExCtor != null)
            {
                il.Emit(OpCodes.Isinst, instance);
                il.Emit(OpCodes.Dup);
                il.Emit(OpCodes.Brtrue, field);
                il.Emit(OpCodes.Pop);
                il.Emit(OpCodes.Ldstr, "Invalid instance type passed to setter for " + fieldName + ". Expected " + instance.FullName + ".");
                il.Emit(OpCodes.Newobj, _castExCtor);
                il.Emit(OpCodes.Throw);
            }
            il.MarkLabel(lbl);
            if (instance.IsValueType)
                il.Emit(OpCodes.Unbox, instance);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Stfld, field);
            il.Emit(OpCodes.Ret);
            return (InstanceSetter<object, TValue>)method.CreateDelegate(typeof(InstanceSetter<object, TValue>));
        }
        catch (Exception ex)
        {
            Logger.LogError("Error generating instance setter for " + instance.Format() + "." + fieldName.Colorize(Color.red) + ".");
            Logger.LogError(ex);
            if (throwOnError)
                throw;
            return null;
        }
    }
    public static InstanceGetter<object, TValue>? GenerateInstanceGetter<TValue>(Type instance, string fieldName, BindingFlags flags = BindingFlags.NonPublic, bool throwOnError = false)
    {
        if (instance == null)
        {
            string message = $"Error generating instance getter for <unknown>.{fieldName}. Instance type not found.";
            Logger.LogError(message);
            if (throwOnError)
                throw new Exception(message);
            return null;
        }
        try
        {
            flags |= BindingFlags.Instance;
            flags &= ~BindingFlags.Static;
            FieldInfo? field = instance.GetField(fieldName, flags);
            if (field is null || field.IsStatic || !typeof(TValue).IsAssignableFrom(field.FieldType))
                throw new FieldAccessException("Field not found or invalid.");
            const MethodAttributes attr = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig;
            DynamicMethod method = new DynamicMethod("get_" + fieldName, attr, CallingConventions.HasThis, typeof(TValue), new Type[] { typeof(object) }, instance, true);
            ILGenerator il = method.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            Label lbl = il.DefineLabel();
            if (_castExCtorCalc)
            {
                _castExCtorCalc = true;
                _castExCtor = typeof(InvalidCastException).GetConstructor(BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(string) }, null)!;
            }
            
            if (_castExCtor != null)
            {
                il.Emit(OpCodes.Isinst, instance);
                il.Emit(OpCodes.Dup);
                il.Emit(OpCodes.Brtrue, lbl);
                il.Emit(OpCodes.Pop);
                il.Emit(OpCodes.Ldstr, "Invalid instance type passed to getter for " + fieldName + ". Expected " + instance.FullName + ".");
                il.Emit(OpCodes.Newobj, _castExCtor);
                il.Emit(OpCodes.Throw);
            }
            il.MarkLabel(lbl);
            if (instance.IsValueType)
                il.Emit(OpCodes.Unbox, instance);
            il.Emit(OpCodes.Ldfld, field);
            il.Emit(OpCodes.Ret);
            return (InstanceGetter<object, TValue>)method.CreateDelegate(typeof(InstanceGetter<object, TValue>));
        }
        catch (Exception ex)
        {
            Logger.LogError("Error generating instance getter for " + instance.Format() + "." + fieldName.Colorize(Color.red) + ".");
            Logger.LogError(ex);
            if (throwOnError)
                throw;
            return null;
        }
    }
    public static StaticSetter<TValue>? GenerateStaticSetter<TValue>(Type instance, string fieldName, BindingFlags flags = BindingFlags.NonPublic, bool throwOnError = false)
    {
        if (instance == null)
        {
            string message = $"Error generating static setter for <unknown>.{fieldName}. Instance type not found.";
            Logger.LogError(message);
            if (throwOnError)
                throw new Exception(message);
            return null;
        }
        try
        {
            flags |= BindingFlags.Static;
            flags &= ~BindingFlags.Instance;
            FieldInfo? field = instance.GetField(fieldName, flags);
            if (field is null || !field.IsStatic || !field.FieldType.IsAssignableFrom(typeof(TValue)))
                throw new FieldAccessException("Field not found or invalid.");
            const MethodAttributes attr = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig;
            DynamicMethod method = new DynamicMethod("set_" + fieldName, attr, CallingConventions.Standard, typeof(void), new Type[] { field.FieldType }, instance, true);
            method.DefineParameter(1, ParameterAttributes.None, "value");
            ILGenerator il = method.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Stsfld, field);
            il.Emit(OpCodes.Ret);
            return (StaticSetter<TValue>)method.CreateDelegate(typeof(StaticSetter<TValue>));
        }
        catch (Exception ex)
        {
            Logger.LogError("Error generating static setter for " + instance.Format() + "." + fieldName.Colorize(Color.red) + ".");
            Logger.LogError(ex);
            if (throwOnError)
                throw;
            return null;
        }
    }
    public static StaticGetter<TValue>? GenerateStaticGetter<TValue>(Type instance, string fieldName, BindingFlags flags = BindingFlags.NonPublic, bool throwOnError = false)
    {
        if (instance == null)
        {
            string message = $"Error generating static getter for <unknown>.{fieldName}. Instance type not found.";
            Logger.LogError(message);
            if (throwOnError)
                throw new Exception(message);
            return null;
        }
        try
        {
            flags |= BindingFlags.Static;
            flags &= ~BindingFlags.Instance;
            FieldInfo? field = instance.GetField(fieldName, flags);
            if (field is null || !field.IsStatic || !typeof(TValue).IsAssignableFrom(field.FieldType))
                throw new FieldAccessException("Field not found or invalid.");
            const MethodAttributes attr = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig;
            DynamicMethod method = new DynamicMethod("get_" + fieldName, attr, CallingConventions.Standard, typeof(TValue), Array.Empty<Type>(), instance, true);
            ILGenerator il = method.GetILGenerator();
            il.Emit(OpCodes.Ldsfld, field);
            il.Emit(OpCodes.Ret);
            return (StaticGetter<TValue>)method.CreateDelegate(typeof(StaticGetter<TValue>));
        }
        catch (Exception ex)
        {
            Logger.LogError("Error generating static getter for " + instance.Format() + "." + fieldName.Colorize(Color.red) + ".");
            Logger.LogError(ex);
            if (throwOnError)
                throw;
            return null;
        }
    }
    internal static MethodInfo? GetMethodInfo(Delegate method)
    {
        try
        {
            return method.GetMethodInfo();
        }
        catch (MemberAccessException)
        {
            return null!;
        }
    }
    public static IEnumerable<CodeInstruction> AddIsEditorCall(IEnumerable<CodeInstruction> instructions, MethodBase __method)
    {
        if (IsServerGetter == null || IsEditorGetter == null)
        {
            Logger.LogError("IsServer: " + (IsServerGetter != null) + ", IsEditor: " + (IsEditorGetter != null) + ".");
            foreach (CodeInstruction instr in instructions)
                yield return instr;
        }
        foreach (CodeInstruction instr in instructions)
        {
            if (instr.Calls(IsServerGetter))
            {
                yield return instr;
                yield return new CodeInstruction(OpCodes.Not);
                yield return new CodeInstruction(OpCodes.Call, IsEditorGetter);
                yield return new CodeInstruction(OpCodes.Or);
                yield return new CodeInstruction(OpCodes.Not);
                if (__method != null)
                    Logger.LogDebug("Inserted editor call to " + __method.Format() + ".");
                else
                    Logger.LogDebug("Inserted editor call to unknown method.");
            }
            else
                yield return instr;
        }
    }
    public static void AddFunctionBreakpoints(MethodBase method)
    {
        PatchesMain.Patcher.Patch(method,
            transpiler: new HarmonyMethod(typeof(Accessor).GetMethod(nameof(AddFunctionBreakpointsTranspiler),
                BindingFlags.NonPublic | BindingFlags.Static)));
        Logger.LogInfo($"Added breakpoints to: {method.Format()}.");
    }

    private static IEnumerable<CodeInstruction> AddFunctionBreakpointsTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase method)
    {
        yield return new CodeInstruction(OpCodes.Ldstr, "Breakpointing Method: " + method.Format() + ":");
        yield return new CodeInstruction(OpCodes.Ldc_I4, (int)ConsoleColor.Green);
        yield return new CodeInstruction(OpCodes.Call, LogDebug);
        foreach (CodeInstruction instr in instructions)
        {
            foreach (ExceptionBlock block in instr.blocks)
            {
                yield return new CodeInstruction(OpCodes.Ldstr, "  " + block.Format());
                yield return new CodeInstruction(OpCodes.Ldc_I4, (int)ConsoleColor.Green);
                yield return new CodeInstruction(OpCodes.Call, LogDebug);
            }
            foreach (Label label in instr.labels)
            {
                yield return new CodeInstruction(OpCodes.Ldstr, "  " + label.Format() + ":");
                yield return new CodeInstruction(OpCodes.Ldc_I4, (int)ConsoleColor.Green);
                yield return new CodeInstruction(OpCodes.Call, LogDebug);
            }

            yield return new CodeInstruction(OpCodes.Ldstr, "  " + instr.Format());
            yield return new CodeInstruction(OpCodes.Ldc_I4, (int)ConsoleColor.Green);
            yield return new CodeInstruction(OpCodes.Call, LogDebug);

            yield return instr;
        }
    }
    internal static void CheckFuncArrays()
    {
        FuncTypes ??= new Type[]
        {
            typeof(Func<,>),
            typeof(Func<,,>),
            typeof(Func<,,,>),
            typeof(Func<,,,,>),
            typeof(Func<,,,,,>),
            typeof(Func<,,,,,,>),
            typeof(Func<,,,,,,,>),
            typeof(Func<,,,,,,,,>),
            typeof(Func<,,,,,,,,,>),
            typeof(Func<,,,,,,,,,,>),
            typeof(Func<,,,,,,,,,,,>),
            typeof(Func<,,,,,,,,,,,,>),
            typeof(Func<,,,,,,,,,,,,,>),
            typeof(Func<,,,,,,,,,,,,,,>),
            typeof(Func<,,,,,,,,,,,,,,,>),
            typeof(Func<,,,,,,,,,,,,,,,,>)
        };
        ActionTypes ??= new Type[]
        {
            typeof(Action<>),
            typeof(Action<,>),
            typeof(Action<,,>),
            typeof(Action<,,,>),
            typeof(Action<,,,,>),
            typeof(Action<,,,,,>),
            typeof(Action<,,,,,,>),
            typeof(Action<,,,,,,,>),
            typeof(Action<,,,,,,,,>),
            typeof(Action<,,,,,,,,,>),
            typeof(Action<,,,,,,,,,,>),
            typeof(Action<,,,,,,,,,,,>),
            typeof(Action<,,,,,,,,,,,,>),
            typeof(Action<,,,,,,,,,,,,,>),
            typeof(Action<,,,,,,,,,,,,,,>),
            typeof(Action<,,,,,,,,,,,,,,,>)
        };
    }
    public static bool IsExtern(this MethodBase method) => (method.Attributes & MethodAttributes.PinvokeImpl) != 0;
    public static bool IsExtern(this FieldInfo field) => field.IsPinvokeImpl;
    public static bool IsExtern(this PropertyInfo property, bool checkGetterFirst = true)
    {
        MethodInfo? method = checkGetterFirst ? property.GetGetMethod(true) : property.GetSetMethod(true);
        if (method == null)
        {
            method = checkGetterFirst ? property.GetSetMethod(true) : property.GetGetMethod(true);
            if (method == null)
                return false;
        }

        if ((method.Attributes & MethodAttributes.PinvokeImpl) != 0)
            return true;
        if (method.IsAbstract || method.IsVirtual || method.DeclaringType!.IsInterface)
            return false;
        try
        {
            method.GetMethodBody();
            return false;
        }
        catch (BadImageFormatException)
        {
            return true;
        }
    }
    public static bool IsIgnored(this Type type) => Attribute.IsDefined(type, typeof(IgnoreAttribute));
    public static bool IsIgnored(this MemberInfo member) => Attribute.IsDefined(member, typeof(IgnoreAttribute));
    public static bool IsIgnored(this Assembly assembly) => Attribute.IsDefined(assembly, typeof(IgnoreAttribute));
    public static bool IsIgnored(this ParameterInfo parameter) => Attribute.IsDefined(parameter, typeof(IgnoreAttribute));
    public static bool IsIgnored(this Module module) => Attribute.IsDefined(module, typeof(IgnoreAttribute));
    public static int GetPriority(this Type type) => Attribute.GetCustomAttribute(type, typeof(LoadPriorityAttribute)) is LoadPriorityAttribute attr ? attr.Priority : 0;
    public static int GetPriority(this MemberInfo member) => Attribute.GetCustomAttribute(member, typeof(LoadPriorityAttribute)) is LoadPriorityAttribute attr ? attr.Priority : 0;
    public static int GetPriority(this Assembly assembly) => Attribute.GetCustomAttribute(assembly, typeof(LoadPriorityAttribute)) is LoadPriorityAttribute attr ? attr.Priority : 0;
    public static int GetPriority(this ParameterInfo parameter) => Attribute.GetCustomAttribute(parameter, typeof(LoadPriorityAttribute)) is LoadPriorityAttribute attr ? attr.Priority : 0;
    public static int GetPriority(this Module module) => Attribute.GetCustomAttribute(module, typeof(LoadPriorityAttribute)) is LoadPriorityAttribute attr ? attr.Priority : 0;
    public static int SortTypesByPriorityHandler(Type a, Type b)
    {
        return (Attribute.GetCustomAttribute(b, typeof(LoadPriorityAttribute)) is LoadPriorityAttribute pB
            ? pB.Priority
            : 0).CompareTo(Attribute.GetCustomAttribute(a, typeof(LoadPriorityAttribute)) is LoadPriorityAttribute pA
            ? pA.Priority
            : 0);
    }
    public static int SortMembersByPriorityHandler(MemberInfo a, MemberInfo b)
    {
        return (Attribute.GetCustomAttribute(b, typeof(LoadPriorityAttribute)) is LoadPriorityAttribute pB
            ? pB.Priority
            : 0).CompareTo(Attribute.GetCustomAttribute(a, typeof(LoadPriorityAttribute)) is LoadPriorityAttribute pA
            ? pA.Priority
            : 0);
    }
    public static Delegate? GenerateInstanceCaller<TInstance>(string methodName, Type[]? parameters = null, bool throwOnError = false, bool useFptrReconstruction = false)
    {
        MethodInfo? method = null;
        if (parameters == null)
        {
            try
            {
                method = typeof(TInstance).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            }
            catch (AmbiguousMatchException)
            {
                // ignored
            }
        }
        if (parameters != null)
        {
            method = typeof(TInstance)
                .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                    null, CallingConventions.Any, parameters, null);
        }

        if (method == null)
        {
            Logger.LogError("Unable to find matching method " + methodName + ".");
            if (throwOnError)
                throw new Exception("Unable to find matching method: " + methodName + ".");
            return null;
        }

        return GenerateInstanceCaller(method, throwOnError, useFptrReconstruction);
    }
    public static TDelegate? GenerateInstanceCaller<TInstance, TDelegate>(string methodName, Type[]? parameters = null, bool throwOnError = false, bool useFptrReconstruction = false) where TDelegate : Delegate
    {
        MethodInfo? method = null;
        if (parameters == null)
        {
            try
            {
                method = typeof(TInstance).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            }
            catch (AmbiguousMatchException)
            {
                // ignored
            }
        }
        if (parameters != null)
        {
            method = typeof(TInstance)
                .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                    null, CallingConventions.Any, parameters, null);
        }

        if (method == null)
        {
            Logger.LogError("Unable to find matching method " + methodName + ".");
            if (throwOnError)
                throw new Exception("Unable to find matching method: " + methodName + ".");
            return null;
        }

        return GenerateInstanceCaller<TDelegate>(method, throwOnError, useFptrReconstruction);
    }
    public static Delegate? GenerateInstanceCaller(MethodInfo method, bool throwOnError = false, bool useFptrReconstruction = false)
    {
        CheckFuncArrays();

        bool rtn = method.ReturnType != typeof(void);
        ParameterInfo[] p = method.GetParameters();
        if (p.Length + 1 > (rtn ? FuncTypes!.Length : ActionTypes!.Length))
        {
            if (throwOnError)
                throw new ArgumentException("Method can not have more than " + ((rtn ? FuncTypes!.Length : ActionTypes!.Length) - 1) + " arguments!", nameof(method));
            Logger.LogWarning("Method " + method.Format() + " can not have more than " + ((rtn ? FuncTypes!.Length : ActionTypes!.Length) - 1) + " arguments!");
            return null;
        }
        Type deleType;
        try
        {
            deleType = GetDefaultDelegate(method.ReturnType, p, method.DeclaringType);
        }
        catch (Exception ex)
        {
            Logger.LogError("Error generating instance caller for " + method.Format() + ".");
            Logger.LogError(ex);
            if (throwOnError)
                throw;
            return null;
        }

        return GenerateInstanceCaller(deleType, method, throwOnError, useFptrReconstruction);
    }
    public static TDelegate? GenerateInstanceCaller<TDelegate>(MethodInfo info, bool throwOnError = false, bool useFptrReconstruction = false) where TDelegate : Delegate
    {
        Delegate? d = GenerateInstanceCaller(typeof(TDelegate), info, throwOnError, useFptrReconstruction);
        if (d is TDelegate dele)
        {
            return dele;
        }

        if (d != null)
        {
            Logger.LogError("Error generating instance caller for " + info.Format() + ".");
            if (throwOnError)
                throw new InvalidCastException("Failed to convert from " + d.GetType() + " to " + typeof(TDelegate) + ".");
        }
        else if (throwOnError)
            throw new Exception("Error generating instance caller for " + info.Format() + ".");

        return null;
    }
    public static Delegate? GenerateInstanceCaller(Type delegateType, MethodInfo method, bool throwOnError = false, bool useFptrReconstruction = false)
    {
        if (method == null)
            throw new ArgumentNullException(nameof(method));
        ParameterInfo[] p = method.GetParameters();
        Type[] paramTypes = new Type[p.Length + 1];
        paramTypes[0] = method.DeclaringType!;
        for (int i = 1; i < paramTypes.Length; ++i)
            paramTypes[i] = p[i - 1].ParameterType;
        if (!method.ShouldCallvirt())
        {
            try
            {
                // try to create a delegate without allocating a method first
                Delegate? rtn = method.CreateDelegate(delegateType);
                if (rtn != null)
                {
                    Logger.LogDebug("Created instance calling delegate for " + method.Format() + " of type " + delegateType.Format() + ".");
                    return rtn;
                }
            }
            catch
            {
                // ignored
            }
        }
        try
        {
            DynamicMethod dm = new DynamicMethod("Invoke" + method.Name, method.ReturnType, paramTypes, method.DeclaringType?.Module ?? typeof(Accessor).Module, true);
            ILGenerator generator = dm.GetILGenerator();
            for (int i = 0; i < paramTypes.Length; ++i)
            {
                generator.LoadParameter(i, paramTypes[i].IsByRef);
            }
            generator.Emit(method.GetCall(), method);
            generator.Emit(OpCodes.Ret);

            try
            {
                if (useFptrReconstruction)
                {
                    Type t = GetDefaultDelegate(method.ReturnType, method.GetParameters(), method.DeclaringType);
                    if (t != delegateType)
                    {
                        Delegate d = method.CreateDelegate(t);
                        IntPtr ptr = d.Method.MethodHandle.GetFunctionPointer();
                        Delegate d2 = (Delegate)delegateType.GetConstructors()[0].Invoke(FormatterServices.GetUninitializedObject(delegateType), new object[] { null!, ptr });
                        Logger.LogDebug("Created instance caller for " + method.Format() + " (using function pointer reconstruction) of type " + delegateType.Format() + ".");
                        return d2;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning("Error trying to use function pointer reconstruction.");
                Logger.LogError(ex);
            }
            Delegate d3 = dm.CreateDelegate(delegateType);
            Logger.LogDebug("Created instance caller for " + method.Format() + " of type " + delegateType.Format() + ".");
            return d3;
        }
        catch (Exception ex)
        {
            Logger.LogWarning("Unable to create instance caller for " + (method.DeclaringType?.Name ?? "<unknown-type>").Format(false) + "." + (method.Name ?? "<unknown-name>").Format(false) + ".");
            Logger.LogError(ex);
            if (throwOnError)
                throw;
            return null;
        }
    }
    public static Delegate? GenerateStaticCaller<TInstance>(string methodName, Type[]? parameters = null, bool throwOnError = false, bool useFptrReconstruction = false)
    {
        MethodInfo? method = null;
        if (parameters == null)
        {
            try
            {
                method = typeof(TInstance).GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            }
            catch (AmbiguousMatchException)
            {
                // ignored
            }
        }
        if (parameters != null)
        {
            method = typeof(TInstance)
                .GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
                    null, CallingConventions.Any, parameters, null);
        }

        if (method == null)
        {
            Logger.LogError("Unable to find matching method " + methodName + ".");
            if (throwOnError)
                throw new Exception("Unable to find matching method: " + methodName + ".");
            return null;
        }

        return GenerateStaticCaller(method, throwOnError, useFptrReconstruction);
    }
    public static TDelegate? GenerateStaticCaller<TInstance, TDelegate>(string methodName, Type[]? parameters = null, bool throwOnError = false, bool useFptrReconstruction = false) where TDelegate : Delegate
    {
        MethodInfo? method = null;
        if (parameters == null)
        {
            try
            {
                method = typeof(TInstance).GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            }
            catch (AmbiguousMatchException)
            {
                // ignored
            }
        }
        if (parameters != null)
        {
            method = typeof(TInstance)
                .GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
                    null, CallingConventions.Any, parameters, null);
        }

        if (method == null)
        {
            Logger.LogError("Unable to find matching method " + methodName + ".");
            if (throwOnError)
                throw new Exception("Unable to find matching method: " + methodName + ".");
            return null;
        }

        return GenerateStaticCaller<TDelegate>(method, throwOnError, useFptrReconstruction);
    }
    public static Delegate? GenerateStaticCaller(MethodInfo method, bool throwOnError = false, bool useFptrReconstruction = false)
    {
        if (method == null)
            throw new ArgumentNullException(nameof(method));
        CheckFuncArrays();

        bool rtn = method.ReturnType != typeof(void);
        ParameterInfo[] p = method.GetParameters();
        if (p.Length > (rtn ? FuncTypes!.Length : ActionTypes!.Length))
        {
            if (throwOnError)
                throw new ArgumentException("Method can not have more than " + (rtn ? FuncTypes!.Length : ActionTypes!.Length) + " arguments!", nameof(method));
            Logger.LogWarning("Method " + method.Format() + " can not have more than " + (rtn ? FuncTypes!.Length : ActionTypes!.Length) + " arguments!");
            return null;
        }
        Type deleType;
        try
        {
            deleType = GetDefaultDelegate(method.ReturnType, p, null);
        }
        catch (Exception ex)
        {
            Logger.LogError("Error generating static caller for " + method.Format() + ".");
            Logger.LogError(ex);
            if (throwOnError)
                throw;
            return null;
        }

        return GenerateInstanceCaller(deleType, method, throwOnError, useFptrReconstruction);
    }
    public static TDelegate? GenerateStaticCaller<TDelegate>(MethodInfo info, bool throwOnError = false, bool useFptrReconstruction = false) where TDelegate : Delegate
    {
        if (info == null)
        {
            Logger.LogError("Error generating static caller of type " + typeof(TDelegate).Format() + ".");
            if (throwOnError)
                throw new MissingMethodException("Error generating static caller of type " + typeof(TDelegate).Format() + ".");
            
            return null;
        }
        Delegate? d = GenerateStaticCaller(typeof(TDelegate), info, throwOnError, useFptrReconstruction);
        if (d is TDelegate dele)
        {
            return dele;
        }

        if (d != null)
        {
            Logger.LogError("Error generating static caller for " + info.Format() + ".");
            if (throwOnError)
                throw new InvalidCastException("Failed to convert from " + d.GetType() + " to " + typeof(TDelegate) + ".");
        }
        else if (throwOnError)
            throw new Exception("Error generating static caller for " + info.Format() + ".");

        return null;
    }
    public static Delegate? GenerateStaticCaller(Type delegateType, MethodInfo method, bool throwOnError = false, bool useFptrReconstruction = false)
    {
        try
        {
            try
            {
                if (useFptrReconstruction)
                {
                    Type t = GetDefaultDelegate(method.ReturnType, method.GetParameters(), null);
                    if (t != delegateType)
                    {
                        Delegate d = method.CreateDelegate(t);
                        IntPtr ptr = d.Method.MethodHandle.GetFunctionPointer();
                        Delegate d2 = (Delegate)delegateType.GetConstructors()[0].Invoke(FormatterServices.GetUninitializedObject(delegateType), new object[] { null!, ptr });
                        Logger.LogDebug("Created caller for " + method.Format() + " (using function pointer reconstruction) of type " + delegateType.Format() + ".");
                        return d2;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning("Error trying to use function pointer reconstruction.");
                Logger.LogError(ex);
            }
            Delegate d3 = method.CreateDelegate(delegateType);
            Logger.LogDebug("Created caller for " + method.Format() + " of type " + delegateType.Format() + ".");
            return d3;
        }
        catch (Exception ex)
        {
            Logger.LogWarning("Unable to create static caller for " + (method.DeclaringType?.Format() ?? "<unknown-type>") + "." + method.Name);
            Logger.LogError(ex);
            if (throwOnError)
                throw;
            return null;
        }
    }
    private static Type GetDefaultDelegate(Type rtn, ParameterInfo[] p, Type? inst)
    {
        CheckFuncArrays();
        
        if (inst == null)
        {
            if (rtn != typeof(void))
            {
                Type[] p2 = new Type[p.Length + 1];
                for (int i = 1; i < p2.Length - 1; ++i)
                    p2[i] = p[i].ParameterType;
                p2[p2.Length - 1] = rtn;
                return FuncTypes![p.Length].MakeGenericType(p2);
            }
            else
            {
                Type[] p2 = new Type[p.Length];
                for (int i = 1; i < p2.Length; ++i)
                    p2[i] = p[i].ParameterType;
                return ActionTypes![p.Length].MakeGenericType(p2);
            }
        }
        else
        {
            if (rtn != typeof(void))
            {
                Type[] p2 = new Type[p.Length + 2];
                p2[0] = inst;
                for (int i = 1; i < p2.Length - 1; ++i)
                    p2[i] = p[i - 1].ParameterType;
                p2[p2.Length - 1] = rtn;
                return FuncTypes![p.Length].MakeGenericType(p2);
            }
            else
            {
                Type[] p2 = new Type[p.Length + 1];
                p2[0] = inst;
                for (int i = 1; i < p2.Length; ++i)
                    p2[i] = p[i - 1].ParameterType;
                return ActionTypes![p.Length].MakeGenericType(p2);
            }
        }
    }

    /// <returns>Every type defined in the calling assembly.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static List<Type> GetTypesSafe(bool removeIgnored = false) => GetTypesSafe(Assembly.GetCallingAssembly(), removeIgnored);

    /// <returns>Every type defined in <paramref name="assembly"/>.</returns>
    public static List<Type> GetTypesSafe(Assembly assembly, bool removeIgnored = false)
    {
        List<Type> types;
        try
        {
            types = new List<Type>(assembly.GetTypes());
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = new List<Type>(ex.Types);
        }

        if (removeIgnored)
            types.RemoveAll(x => Attribute.IsDefined(x, typeof(IgnoreAttribute)));

        types.RemoveAll(x => x == null);
        types.Sort(SortTypesByPriorityHandler);
        return types;
    }
    /// <returns>Every type defined in the provided <paramref name="assmeblies"/>.</returns>
    public static List<Type> GetTypesSafe(IEnumerable<Assembly> assmeblies, bool removeIgnored = false)
    {
        List<Type> types = new List<Type>();
        foreach (Assembly assembly in assmeblies)
        {
            try
            {
                types.AddRange(assembly.GetTypes());
            }
            catch (ReflectionTypeLoadException ex)
            {
                types.AddRange(ex.Types);
            }
        }

        if (removeIgnored)
            types.RemoveAll(x => Attribute.IsDefined(x, typeof(IgnoreAttribute)));
        types.RemoveAll(x => x == null);
        types.Sort(SortTypesByPriorityHandler);
        return types;
    }

    /// <exception cref="TypeLoadException"/>
    public static Assembly AssemblyCSharp => _sdgAssembly ??= typeof(Provider).Assembly;
    public static Assembly DevkitServer => _devkitServerAssembly ??= Assembly.GetExecutingAssembly();
    public static Assembly MSCoreLib => _mscorlibAssembly ??= typeof(object).Assembly;

    /// <summary><see cref="Provider.isServer"/>.</summary>
    /// <exception cref="MemberAccessException"/>
    public static MethodInfo IsServerGetter => _getIsServer ??=
        typeof(Provider).GetProperty(nameof(Provider.isServer), BindingFlags.Static | BindingFlags.Public)
            ?.GetMethod ?? throw new MemberAccessException("Unable to find Provider.isServer.");

    /// <summary><see cref="Level.isEditor"/>.</summary>
    /// <exception cref="MemberAccessException"/>
    public static MethodInfo IsEditorGetter => _getIsEditor ??=
        typeof(Level).GetProperty(nameof(Level.isEditor), BindingFlags.Static | BindingFlags.Public)
            ?.GetMethod ?? throw new MemberAccessException("Unable to find Level.isEditor.");

    /// <summary><see cref="DevkitServerModule.IsEditing"/>.</summary>
    /// <exception cref="MemberAccessException"/>
    public static MethodInfo IsDevkitServerGetter => _getIsDevkitServer ??=
        typeof(DevkitServerModule).GetProperty(nameof(DevkitServerModule.IsEditing), BindingFlags.Static | BindingFlags.Public)
            ?.GetMethod ?? throw new MemberAccessException("Unable to find DevkitServerModule.IsEditing.");

    /// <summary><see cref="Logger.LogDebug"/>.</summary>
    /// <exception cref="MemberAccessException"/>
    public static MethodInfo LogDebug => _logDebug ??=
        typeof(Logger).GetMethod(nameof(Logger.LogDebug), BindingFlags.Public | BindingFlags.Static)
        ?? throw new MemberAccessException("Unable to find Logger.LogDebug.");

    /// <summary><see cref="CachedTime.RealtimeSinceStartup"/>.</summary>
    /// <exception cref="MemberAccessException"/>
    public static MethodInfo GetRealtimeSinceStartup => _getRealtimeSinceStartup ??=
        typeof(CachedTime).GetProperty(nameof(CachedTime.RealtimeSinceStartup), BindingFlags.Static | BindingFlags.Public)
            ?.GetMethod ?? throw new MemberAccessException("Unable to find CachedTime.RealtimeSinceStartup.");

    /// <summary><see cref="Time.realtimeSinceStartupAsDouble"/>.</summary>
    /// <exception cref="MemberAccessException"/>
    public static MethodInfo GetRealtimeSinceStartupAsDouble => _getRealtimeSinceStartupAsDouble ??=
        typeof(Time).GetProperty(nameof(Time.realtimeSinceStartupAsDouble), BindingFlags.Static | BindingFlags.Public)
            ?.GetMethod ?? throw new MemberAccessException("Unable to find Time.realtimeSinceStartupAsDouble.");

    /// <summary><see cref="Time.time"/>.</summary>
    /// <exception cref="MemberAccessException"/>
    public static MethodInfo GetTime => _getTime ??=
        typeof(Time).GetProperty(nameof(Time.time), BindingFlags.Static | BindingFlags.Public)
            ?.GetMethod ?? throw new MemberAccessException("Unable to find Time.time.");

    /// <summary><see cref="CachedTime.DeltaTime"/>.</summary>
    /// <exception cref="MemberAccessException"/>
    public static MethodInfo GetDeltaTime => _getDeltaTime ??=
        typeof(CachedTime).GetProperty(nameof(CachedTime.DeltaTime), BindingFlags.Static | BindingFlags.Public)
            ?.GetMethod ?? throw new MemberAccessException("Unable to find CachedTime.DeltaTime.");

    /// <summary><see cref="Time.fixedDeltaTime"/>.</summary>
    /// <exception cref="MemberAccessException"/>
    public static MethodInfo GetFixedDeltaTime => _getFixedDeltaTime ??=
        typeof(Time).GetProperty(nameof(Time.fixedDeltaTime), BindingFlags.Static | BindingFlags.Public)
            ?.GetMethod ?? throw new MemberAccessException("Unable to find Time.fixedDeltaTime.");

    /// <summary><see cref="GameObject.transform"/>.</summary>
    /// <exception cref="MemberAccessException"/>
    public static MethodInfo GetGameObjectTransform => _getGameObjectTransform ??=
        typeof(GameObject).GetProperty(nameof(GameObject.transform), BindingFlags.Instance | BindingFlags.Public)
            ?.GetMethod ?? throw new MemberAccessException("Unable to find GameObject.transform.");

    /// <summary><see cref="Component.transform"/>.</summary>
    /// <exception cref="MemberAccessException"/>
    public static MethodInfo GetComponentTransform => _getComponentTransform ??=
        typeof(Component).GetProperty(nameof(Component.transform), BindingFlags.Instance | BindingFlags.Public)
            ?.GetMethod ?? throw new MemberAccessException("Unable to find Component.transform.");

    /// <summary><see cref="Component.gameObject"/>.</summary>
    /// <exception cref="MemberAccessException"/>
    public static MethodInfo GetComponentGameObject => _getComponentGameObject ??=
        typeof(Component).GetProperty(nameof(Component.gameObject), BindingFlags.Instance | BindingFlags.Public)
            ?.GetMethod ?? throw new MemberAccessException("Unable to find Component.gameObject.");

    /// <summary><see cref="InputEx.GetKeyDown"/>.</summary>
    /// <exception cref="MemberAccessException"/>
    public static MethodInfo GetKeyDown => _getKeyDown ??=
        typeof(InputEx).GetMethod(nameof(InputEx.GetKeyDown), BindingFlags.Public | BindingFlags.Static)
        ?? throw new MemberAccessException("Unable to find InputEx.GetKeyDown.");

    /// <summary><see cref="InputEx.GetKeyUp"/>.</summary>
    /// <exception cref="MemberAccessException"/>
    public static MethodInfo GetKeyUp => _getKeyUp ??=
        typeof(InputEx).GetMethod(nameof(InputEx.GetKeyUp), BindingFlags.Public | BindingFlags.Static)
        ?? throw new MemberAccessException("Unable to find InputEx.GetKeyUp.");

    /// <summary><see cref="InputEx.GetKey"/>.</summary>
    /// <exception cref="MemberAccessException"/>
    public static MethodInfo GetKey => _getKey ??=
        typeof(InputEx).GetMethod(nameof(InputEx.GetKey), BindingFlags.Public | BindingFlags.Static)
        ?? throw new MemberAccessException("Unable to find InputEx.GetKey.");

    /// <summary><see cref="StackTrace(int)"/>.</summary>
    /// <exception cref="MemberAccessException"/>
    public static ConstructorInfo StackTraceIntConstructor => _stackTraceIntCtor ??= 
        typeof(StackTrace).GetConstructor(BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(int) },
            null) ?? throw new MemberAccessException("Unable to find StackTrace.StackTrace(int).");

    /// <summary><see cref="string.Concat(string, string)"/>.</summary>
    /// <exception cref="MemberAccessException"/>
    public static MethodInfo Concat2StringsMethod => _concat2Strings ??=
        typeof(string).GetMethod(nameof(string.Concat), BindingFlags.Static | BindingFlags.Public, null,
            new Type[] { typeof(string), typeof(string) }, null)
        ?? throw new MemberAccessException("Unable to find string.Concat(string, string).");

    /// <summary><see cref="StackTraceCleaner.GetString(StackTrace)"/>.</summary>
    /// <exception cref="MemberAccessException"/>
    public static MethodInfo StackTraceCleanerGetStringMethod => _getStackTraceString ??=
        typeof(StackTraceCleaner).GetMethod(nameof(StackTraceCleaner.GetString), BindingFlags.Instance | BindingFlags.Public, null,
            new Type[] { typeof(StackTrace) }, null)
        ?? throw new MemberAccessException("Unable to find StackTraceCleaner.GetString(StackTrace).");

    /// <summary><see cref="Logger.StackCleaner"/>.</summary>
    /// <exception cref="MemberAccessException"/>
    public static FieldInfo LoggerStackCleanerField => _getStackCleaner ??=
        typeof(Logger).GetField(nameof(Logger.StackCleaner), BindingFlags.Static | BindingFlags.Public)
        ?? throw new MemberAccessException("Unable to find Logger.StackCleaner.");
}

public delegate void InstanceSetter<in TInstance, in T>(TInstance owner, T value);
public delegate T InstanceGetter<in TInstance, out T>(TInstance owner);
public delegate void StaticSetter<in T>(T value);
public delegate T StaticGetter<out T>();

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
internal sealed class EarlyTypeInitAttribute : Attribute
{
    public int Priority { get; }

    public EarlyTypeInitAttribute() : this (0) { }

    public EarlyTypeInitAttribute(int priority)
    {
        Priority = priority;
    }
}