using System.Reflection;
using System.Reflection.Emit;
using DevkitServer.Patches;

namespace DevkitServer.Util;
internal static class Accessor
{
    public static InstanceSetter<TInstance, TValue> GenerateInstanceSetter<TInstance, TValue>(string fieldName, BindingFlags flags)
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
    public static InstanceGetter<TInstance, TValue> GenerateInstanceGetter<TInstance, TValue>(string fieldName, BindingFlags flags)
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
    public static StaticSetter<TValue> GenerateStaticSetter<TInstance, TValue>(string fieldName, BindingFlags flags)
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
    public static StaticGetter<TValue> GenerateStaticGetter<TInstance, TValue>(string fieldName, BindingFlags flags)
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
}

public delegate void InstanceSetter<in TInstance, in T>(TInstance owner, T value);
public delegate T InstanceGetter<in TInstance, out T>(TInstance owner);
public delegate void StaticSetter<in T>(T value);
public delegate T StaticGetter<out T>();
