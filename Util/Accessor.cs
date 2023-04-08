using System.Reflection;
using System.Reflection.Emit;
using DevkitServer.Patches;
using HarmonyLib;

namespace DevkitServer.Util;
internal static class Accessor
{
    private static Type[]? _funcTypeList;
    private static Type[]? _actionTypeList;
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
            Logger.LogError("Error generating instance setter for " + typeof(TInstance).Name + "." + fieldName + ".");
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
            Logger.LogError("Error generating instance getter for " + typeof(TInstance).Name + "." + fieldName + ".");
            Logger.LogError(ex);
            if (throwOnError)
                throw;
            return null;
        }
    }
    public static StaticSetter<TValue>? GenerateStaticSetter<TInstance, TValue>(string fieldName, BindingFlags flags = BindingFlags.NonPublic, bool throwOnError = false)
    {
        try
        {
            flags |= BindingFlags.Static;
            flags &= ~BindingFlags.Instance;
            FieldInfo? field = typeof(TInstance).GetField(fieldName, flags);
            if (field is null || !field.IsStatic || !field.FieldType.IsAssignableFrom(typeof(TValue)))
                throw new FieldAccessException("Field not found or invalid.");
            const MethodAttributes attr = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig;
            DynamicMethod method = new DynamicMethod("set_" + fieldName, attr, CallingConventions.Standard, typeof(void), new Type[] { field.FieldType }, typeof(TInstance), true);
            method.DefineParameter(1, ParameterAttributes.None, "value");
            ILGenerator il = method.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Stsfld, field);
            il.Emit(OpCodes.Ret);
            return (StaticSetter<TValue>)method.CreateDelegate(typeof(StaticSetter<TValue>));
        }
        catch (Exception ex)
        {
            Logger.LogError("Error generating static setter for " + typeof(TInstance).Name + "." + fieldName + ".");
            Logger.LogError(ex);
            if (throwOnError)
                throw;
            return null;
        }
    }
    public static StaticGetter<TValue>? GenerateStaticGetter<TInstance, TValue>(string fieldName, BindingFlags flags = BindingFlags.NonPublic, bool throwOnError = false)
    {
        try
        {
            flags |= BindingFlags.Static;
            flags &= ~BindingFlags.Instance;
            FieldInfo? field = typeof(TInstance).GetField(fieldName, flags);
            if (field is null || !field.IsStatic || !field.FieldType.IsAssignableFrom(typeof(TValue)))
                throw new FieldAccessException("Field not found or invalid.");
            const MethodAttributes attr = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig;
            DynamicMethod method = new DynamicMethod("get_" + fieldName, attr, CallingConventions.Standard, typeof(TValue), Array.Empty<Type>(), typeof(TInstance), true);
            ILGenerator il = method.GetILGenerator();
            il.Emit(OpCodes.Ldsfld, field);
            il.Emit(OpCodes.Ret);
            return (StaticGetter<TValue>)method.CreateDelegate(typeof(StaticGetter<TValue>));
        }
        catch (Exception ex)
        {
            Logger.LogError("Error generating static getter for " + typeof(TInstance).Name + "." + fieldName + ".");
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
    
    private static readonly MethodInfo isServerGetter = typeof(Provider).GetProperty(nameof(Provider.isServer), BindingFlags.Static | BindingFlags.Public)?.GetGetMethod()!;
    private static readonly MethodInfo isEditorGetter = typeof(Level).GetProperty(nameof(Level.isEditor), BindingFlags.Static | BindingFlags.Public)?.GetGetMethod()!;
    public static IEnumerable<CodeInstruction> AddIsEditorCall(IEnumerable<CodeInstruction> instructions, MethodBase __method)
    {
        if (isServerGetter == null || isEditorGetter == null)
        {
            Logger.LogError("IsServer: " + (isServerGetter != null) + ", IsEditor: " + (isEditorGetter != null) + ".");
            foreach (CodeInstruction instr in instructions)
                yield return instr;
        }
        foreach (CodeInstruction instr in instructions)
        {
            if (instr.Calls(isServerGetter))
            {
                yield return instr;
                yield return new CodeInstruction(OpCodes.Not);
                yield return new CodeInstruction(OpCodes.Call, isEditorGetter);
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
    private static void CheckFuncArrays()
    {
        _funcTypeList ??= new Type[]
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
        _actionTypeList ??= new Type[]
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

    public static Delegate? GenerateInstanceCaller<TInstance>(string methodName, Type[]? parameters = null, bool throwOnError = false)
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

        return GenerateInstanceCaller(method);
    }
    public static TDelegate? GenerateInstanceCaller<TInstance, TDelegate>(string methodName, Type[]? parameters = null, bool throwOnError = false) where TDelegate : Delegate
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

        return GenerateInstanceCaller<TDelegate>(method);
    }
    public static Delegate? GenerateInstanceCaller(MethodInfo method, bool throwOnError = false)
    {
        CheckFuncArrays();

        bool rtn = method.ReturnType != typeof(void);
        ParameterInfo[] p = method.GetParameters();
        if (p.Length + 1 > (rtn ? _funcTypeList!.Length : _actionTypeList!.Length))
        {
            if (throwOnError)
                throw new ArgumentException("Method can not have more than " + ((rtn ? _funcTypeList!.Length : _actionTypeList!.Length) - 1) + " arguments!", nameof(method));
            Logger.LogWarning("Method " + method.Format() + " can not have more than " + ((rtn ? _funcTypeList!.Length : _actionTypeList!.Length) - 1) + " arguments!");
            return null;
        }
        Type deleType;
        try
        {
            if (rtn)
            {
                Type[] p2 = new Type[p.Length + 2];
                p2[0] = method.DeclaringType!;
                for (int i = 1; i < p2.Length - 1; ++i)
                    p2[i] = p[i - 1].ParameterType;
                p2[p2.Length - 1] = method.ReturnType;
                deleType = _funcTypeList![p.Length].MakeGenericType(p2);
            }
            else
            {
                Type[] p2 = new Type[p.Length + 1];
                p2[0] = method.DeclaringType!;
                for (int i = 1; i < p2.Length; ++i)
                    p2[i] = p[i - 1].ParameterType;
                deleType = _actionTypeList![p.Length];
                deleType = deleType.MakeGenericType(p2);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Error generating instance caller for " + method.Format() + ".");
            Logger.LogError(ex);
            if (throwOnError)
                throw;
            return null;
        }

        return GenerateInstanceCaller(deleType, method);
    }
    public static TDelegate? GenerateInstanceCaller<TDelegate>(MethodInfo info, bool throwOnError = false) where TDelegate : Delegate
    {
        Delegate? d = GenerateInstanceCaller(typeof(TDelegate), info);
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
    public static Delegate? GenerateInstanceCaller(Type delegateType, MethodInfo method, bool throwOnError = false)
    {
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
                if (paramTypes[i].IsByRef)
                {
                    generator.Emit(OpCodes.Ldarga_S, (byte)i);
                }
                else
                {
                    OpCode c = i switch
                    {
                        0 => OpCodes.Ldarg_0,
                        1 => OpCodes.Ldarg_1,
                        2 => OpCodes.Ldarg_2,
                        3 => OpCodes.Ldarg_3,
                        _ => OpCodes.Ldarg_S
                    };
                    if (i > 3)
                        generator.Emit(c, (byte)i);
                    else
                        generator.Emit(c);
                }
            }
            generator.Emit(OpCodes.Callvirt, method);
            generator.Emit(OpCodes.Ret);
            return dm.CreateDelegate(delegateType);
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

    public static Delegate? GenerateStaticCaller<TInstance>(string methodName, Type[]? parameters = null, bool throwOnError = false)
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

        return GenerateStaticCaller(method);
    }
    public static TDelegate? GenerateStaticCaller<TInstance, TDelegate>(string methodName, Type[]? parameters = null, bool throwOnError = false) where TDelegate : Delegate
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

        return GenerateStaticCaller<TDelegate>(method);
    }
    public static Delegate? GenerateStaticCaller(MethodInfo method, bool throwOnError = false)
    {
        CheckFuncArrays();

        bool rtn = method.ReturnType != typeof(void);
        ParameterInfo[] p = method.GetParameters();
        if (p.Length > (rtn ? _funcTypeList!.Length : _actionTypeList!.Length))
        {
            if (throwOnError)
                throw new ArgumentException("Method can not have more than " + (rtn ? _funcTypeList!.Length : _actionTypeList!.Length) + " arguments!", nameof(method));
            Logger.LogWarning("Method " + method.Format() + " can not have more than " + (rtn ? _funcTypeList!.Length : _actionTypeList!.Length) + " arguments!");
            return null;
        }
        Type deleType;
        try
        {
            if (rtn)
            {
                Type[] p2 = new Type[p.Length + 1];
                for (int i = 1; i < p2.Length - 1; ++i)
                    p2[i] = p[i].ParameterType;
                p2[p2.Length - 1] = method.ReturnType;
                deleType = _funcTypeList![p.Length].MakeGenericType(p2);
            }
            else
            {
                Type[] p2 = new Type[p.Length];
                for (int i = 1; i < p2.Length; ++i)
                    p2[i] = p[i].ParameterType;
                deleType = _actionTypeList![p.Length];
                deleType = deleType.MakeGenericType(p2);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Error generating static caller for " + method.Format() + ".");
            Logger.LogError(ex);
            if (throwOnError)
                throw;
            return null;
        }

        return GenerateInstanceCaller(deleType, method);
    }
    public static TDelegate? GenerateStaticCaller<TDelegate>(MethodInfo info, bool throwOnError = false) where TDelegate : Delegate
    {
        Delegate? d = GenerateStaticCaller(typeof(TDelegate), info);
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
    public static Delegate? GenerateStaticCaller(Type delegateType, MethodInfo method, bool throwOnError = false)
    {
        ParameterInfo[] p = method.GetParameters();
        Type[] paramTypes = new Type[p.Length + 1];
        paramTypes[0] = method.DeclaringType!;
        for (int i = 1; i < paramTypes.Length; ++i)
            paramTypes[i] = p[i - 1].ParameterType;
        try
        {
            return method.CreateDelegate(delegateType);
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