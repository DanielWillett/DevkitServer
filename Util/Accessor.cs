using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using DevkitServer.Patches;
using HarmonyLib;

namespace DevkitServer.Util;
internal static class Accessor
{
    internal static Type[]? FuncTypes;
    internal static Type[]? ActionTypes;
    private static bool _castExCtorCalc;
    private static ConstructorInfo? _castExCtor;
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
            if (field is null || field.IsStatic || !field.FieldType.IsAssignableFrom(typeof(TValue)))
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
        try
        {
            flags |= BindingFlags.Instance;
            flags &= ~BindingFlags.Static;
            FieldInfo? field = instance.GetField(fieldName, flags);
            if (field is null || field.IsStatic || !field.FieldType.IsAssignableFrom(typeof(TValue)))
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
        try
        {
            flags |= BindingFlags.Static;
            flags &= ~BindingFlags.Instance;
            FieldInfo? field = instance.GetField(fieldName, flags);
            if (field is null || !field.IsStatic || !field.FieldType.IsAssignableFrom(typeof(TValue)))
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

    internal static readonly MethodInfo IsServerGetter = typeof(Provider).GetProperty(nameof(Provider.isServer), BindingFlags.Static | BindingFlags.Public)?.GetGetMethod()!;
    internal static readonly MethodInfo IsEditorGetter = typeof(Level).GetProperty(nameof(Level.isEditor), BindingFlags.Static | BindingFlags.Public)?.GetGetMethod()!;
    internal static readonly MethodInfo IsDevkitServerGetter = typeof(DevkitServerModule).GetProperty(nameof(DevkitServerModule.IsEditing), BindingFlags.Static | BindingFlags.Public)?.GetGetMethod()!;
    private static readonly MethodInfo LogDebug = typeof(Logger).GetMethod(nameof(Logger.LogDebug), BindingFlags.Public | BindingFlags.Static)!;
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
                    Logger.LogInfo("Inserted editor call to " + __method.Format() + ".");
                else
                    Logger.LogInfo("Inserted editor call to unknown method.");
            }
            else
                yield return instr;
        }
    }

    public static void AddFunctionBreakpoints(MethodBase method) => PatchesMain.Patcher.Patch(method,
        transpiler: new HarmonyMethod(typeof(Accessor).GetMethod(nameof(AddFunctionBreakpointsTranspiler),
            BindingFlags.NonPublic | BindingFlags.Static)));
    private static IEnumerable<CodeInstruction> AddFunctionBreakpointsTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase __method)
    {
        yield return new CodeInstruction(OpCodes.Ldstr, "Breakpointing Method: " + __method.Format() + ":");
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
        try
        {
            DynamicMethod dm = new DynamicMethod("Invoke" + method.Name, method.ReturnType, paramTypes, method.DeclaringType?.Module ?? typeof(Accessor).Module, true);
            ILGenerator generator = dm.GetILGenerator();
            for (int i = 0; i < paramTypes.Length; ++i)
            {
                generator.LoadParameter(i, paramTypes[i].IsByRef);
            }
            generator.Emit(method.IsVirtual || method.IsAbstract ? OpCodes.Callvirt : OpCodes.Call, method);
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
            Logger.LogWarning("Unable to create instance caller for " + (method.DeclaringType?.Name ?? "<unknown-type>") + "." + method.Name);
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