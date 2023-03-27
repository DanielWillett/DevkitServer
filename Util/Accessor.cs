using System.Reflection;
using System.Reflection.Emit;
using DevkitServer.Patches;
using HarmonyLib;

namespace DevkitServer.Util;
internal static class Accessor
{
    public static InstanceSetter<TInstance, TValue> GenerateInstanceSetter<TInstance, TValue>(string fieldName, BindingFlags flags = BindingFlags.NonPublic)
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
    public static InstanceGetter<TInstance, TValue> GenerateInstanceGetter<TInstance, TValue>(string fieldName, BindingFlags flags = BindingFlags.NonPublic)
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
    public static StaticSetter<TValue> GenerateStaticSetter<TInstance, TValue>(string fieldName, BindingFlags flags = BindingFlags.NonPublic)
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
    public static StaticGetter<TValue> GenerateStaticGetter<TInstance, TValue>(string fieldName, BindingFlags flags = BindingFlags.NonPublic)
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
    
    /// <exception cref="ArgumentException"/>
    public static TDelegate GetStaticMethod<TInstance, TDelegate>(string methodName, Type[]? parameterTypes = null, BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static) where TDelegate : Delegate
    {
        Type type = typeof(TInstance);
        MethodInfo? method;
        try
        {
            method = parameterTypes == null ? type.GetMethod(methodName, flags) : type.GetMethod(methodName, flags, null, parameterTypes, null);
        }
        catch (AmbiguousMatchException ex)
        {
            if (parameterTypes == null)
                throw new ArgumentException("Multiple methods match \"" + methodName + "\", try specifying parameter types.", nameof(parameterTypes), ex);
            
            throw;
        }
        if (method == null)
            throw new ArgumentException("No methods match \"" + type.Name + "." + methodName + "\".", nameof(methodName));
        if (!method.IsStatic)
            throw new ArgumentException("Method \"" + type.Name + "." + methodName + "\" is not a static method.", nameof(methodName));

        try
        {
            TDelegate dele = (TDelegate)method.CreateDelegate(typeof(TDelegate), null);
            return dele;
        }
        catch (Exception ex)
        {
            throw new ArgumentException("Failed to create a delegate of type " + typeof(TDelegate).Name + ". Should match signature of: " + method.FullDescription() + ".", nameof(TDelegate), ex);
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
                    Logger.LogInfo("Inserted editor call to " + __method.FullDescription() + ".");
                else
                    Logger.LogInfo("Inserted editor call to unknown method.");
            }
            else
                yield return instr;
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