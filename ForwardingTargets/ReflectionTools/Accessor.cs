using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace DanielWillett.ReflectionTools;

[TypeForwardedFrom("ReflectionTools, Version=1.0.2.0, Culture=neutral, PublicKeyToken=6a3a944a5a8d6b8f")]
public static class Accessor
{
    public static Assembly MSCoreLib => DevkitServer.API.Accessor.MSCoreLib;
    public static bool IsMono => DevkitServer.API.Accessor.IsMono;

    [Pure]
    public static InstanceSetter<TInstance, TValue>? GenerateInstanceSetter<TInstance, TValue>(string fieldName, bool throwOnError = false)
    {
        DevkitServer.API.InstanceSetter<TInstance, TValue>? val = DevkitServer.API.Accessor.GenerateInstanceSetter<TInstance, TValue>(fieldName, throwOnError);
        return (InstanceSetter<TInstance, TValue>?)val?.Method.CreateDelegate(typeof(InstanceSetter<TInstance, TValue>));
    }

    [Pure]
    public static InstanceGetter<TInstance, TValue>? GenerateInstanceGetter<TInstance, TValue>(string fieldName, bool throwOnError = false)
    {
        DevkitServer.API.InstanceGetter<TInstance, TValue>? val = DevkitServer.API.Accessor.GenerateInstanceGetter<TInstance, TValue>(fieldName, throwOnError);
        return (InstanceGetter<TInstance, TValue>?)val?.Method.CreateDelegate(typeof(InstanceGetter<TInstance, TValue>));
    }

    [Pure]
    public static InstanceSetter<object, TValue>? GenerateInstanceSetter<TValue>(Type declaringType, string fieldName, bool throwOnError = false)
    {
        DevkitServer.API.InstanceSetter<object, TValue>? val = DevkitServer.API.Accessor.GenerateInstanceSetter<TValue>(declaringType, fieldName, throwOnError);
        return (InstanceSetter<object, TValue>?)val?.Method.CreateDelegate(typeof(InstanceSetter<object, TValue>));
    }

    [Pure]
    public static InstanceGetter<object, TValue>? GenerateInstanceGetter<TValue>(Type declaringType, string fieldName, bool throwOnError = false)
    {
        DevkitServer.API.InstanceGetter<object, TValue>? val = DevkitServer.API.Accessor.GenerateInstanceGetter<TValue>(declaringType, fieldName, throwOnError);
        return (InstanceGetter<object, TValue>?)val?.Method.CreateDelegate(typeof(InstanceGetter<object, TValue>));
    }

    [Pure]
    public static InstanceSetter<TInstance, TValue>? GenerateInstancePropertySetter<TInstance, TValue>(string propertyName, bool throwOnError = false)
    {
        DevkitServer.API.InstanceSetter<TInstance, TValue>? val = DevkitServer.API.Accessor.GenerateInstancePropertySetter<TInstance, TValue>(propertyName, throwOnError);
        return (InstanceSetter<TInstance, TValue>?)val?.Method.CreateDelegate(typeof(InstanceSetter<TInstance, TValue>));
    }

    [Pure]
    public static InstanceGetter<TInstance, TValue>? GenerateInstancePropertyGetter<TInstance, TValue>(string propertyName, bool throwOnError = false)
    {
        DevkitServer.API.InstanceGetter<TInstance, TValue>? val = DevkitServer.API.Accessor.GenerateInstancePropertyGetter<TInstance, TValue>(propertyName, throwOnError);
        return (InstanceGetter<TInstance, TValue>?)val?.Method.CreateDelegate(typeof(InstanceGetter<TInstance, TValue>));
    }

    [Pure]
    public static InstanceSetter<object, TValue>? GenerateInstancePropertySetter<TValue>(Type declaringType, string propertyName, bool throwOnError = false)
    {
        DevkitServer.API.InstanceSetter<object, TValue>? val = DevkitServer.API.Accessor.GenerateInstancePropertySetter<TValue>(declaringType, propertyName, throwOnError);
        return (InstanceSetter<object, TValue>?)val?.Method.CreateDelegate(typeof(InstanceSetter<object, TValue>));
    }

    [Pure]
    public static InstanceGetter<object, TValue>? GenerateInstancePropertyGetter<TValue>(Type declaringType, string propertyName, bool throwOnError = false)
    {
        DevkitServer.API.InstanceGetter<object, TValue>? val = DevkitServer.API.Accessor.GenerateInstancePropertyGetter<TValue>(declaringType, propertyName, throwOnError);
        return (InstanceGetter<object, TValue>?)val?.Method.CreateDelegate(typeof(InstanceGetter<object, TValue>));
    }

    [Pure]
    public static StaticSetter<TValue>? GenerateStaticSetter<TDeclaringType, TValue>(string fieldName, bool throwOnError = false)
    {
        DevkitServer.API.StaticSetter<TValue>? val = DevkitServer.API.Accessor.GenerateStaticSetter<TDeclaringType, TValue>(fieldName, throwOnError);
        return (StaticSetter<TValue>?)val?.Method.CreateDelegate(typeof(StaticSetter<TValue>));
    }

    [Pure]
    public static StaticGetter<TValue>? GenerateStaticGetter<TDeclaringType, TValue>(string fieldName, bool throwOnError = false)
    {
        DevkitServer.API.StaticGetter<TValue>? val = DevkitServer.API.Accessor.GenerateStaticGetter<TDeclaringType, TValue>(fieldName, throwOnError);
        return (StaticGetter<TValue>?)val?.Method.CreateDelegate(typeof(StaticGetter<TValue>));
    }

    [Pure]
    public static StaticSetter<TValue>? GenerateStaticSetter<TValue>(Type declaringType, string fieldName, bool throwOnError = false)
    {
        DevkitServer.API.StaticSetter<TValue>? val = DevkitServer.API.Accessor.GenerateStaticSetter<TValue>(declaringType, fieldName, throwOnError);
        return (StaticSetter<TValue>?)val?.Method.CreateDelegate(typeof(StaticSetter<TValue>));
    }

    [Pure]
    public static StaticGetter<TValue>? GenerateStaticGetter<TValue>(Type declaringType, string fieldName, bool throwOnError = false)
    {
        DevkitServer.API.StaticGetter<TValue>? val = DevkitServer.API.Accessor.GenerateStaticGetter<TValue>(declaringType, fieldName, throwOnError);
        return (StaticGetter<TValue>?)val?.Method.CreateDelegate(typeof(StaticGetter<TValue>));
    }

    [Pure]
    public static StaticSetter<TValue>? GenerateStaticPropertySetter<TDeclaringType, TValue>(string propertyName, bool throwOnError = false)
    {
        DevkitServer.API.StaticSetter<TValue>? val = DevkitServer.API.Accessor.GenerateStaticPropertySetter<TDeclaringType, TValue>(propertyName, throwOnError);
        return (StaticSetter<TValue>?)val?.Method.CreateDelegate(typeof(StaticSetter<TValue>));
    }

    [Pure]
    public static StaticGetter<TValue>? GenerateStaticPropertyGetter<TDeclaringType, TValue>(string propertyName, bool throwOnError = false)
    {
        DevkitServer.API.StaticGetter<TValue>? val = DevkitServer.API.Accessor.GenerateStaticPropertyGetter<TDeclaringType, TValue>(propertyName, throwOnError);
        return (StaticGetter<TValue>?)val?.Method.CreateDelegate(typeof(StaticGetter<TValue>));
    }

    [Pure]
    public static StaticSetter<TValue>? GenerateStaticPropertySetter<TValue>(Type declaringType, string propertyName, bool throwOnError = false)
    {
        DevkitServer.API.StaticSetter<TValue>? val = DevkitServer.API.Accessor.GenerateStaticPropertySetter<TValue>(declaringType, propertyName, throwOnError);
        return (StaticSetter<TValue>?)val?.Method.CreateDelegate(typeof(StaticSetter<TValue>));
    }

    [Pure]
    public static StaticGetter<TValue>? GenerateStaticPropertyGetter<TValue>(Type declaringType, string propertyName, bool throwOnError = false)
    {
        DevkitServer.API.StaticGetter<TValue>? val = DevkitServer.API.Accessor.GenerateStaticPropertyGetter<TValue>(declaringType, propertyName, throwOnError);
        return (StaticGetter<TValue>?)val?.Method.CreateDelegate(typeof(StaticGetter<TValue>));
    }

    [Pure]
    public static Delegate? GenerateInstanceCaller<TInstance>(string methodName, Type[]? parameters = null, bool throwOnError = false, bool allowUnsafeTypeBinding = false)
    {
        return DevkitServer.API.Accessor.GenerateInstanceCaller<TInstance>(methodName, parameters, throwOnError, allowUnsafeTypeBinding);
    }

    [Pure]
    public static TDelegate? GenerateInstanceCaller<TInstance, TDelegate>(string methodName, bool throwOnError = false, bool allowUnsafeTypeBinding = false, Type[]? parameters = null) where TDelegate : Delegate
    {
        return DevkitServer.API.Accessor.GenerateInstanceCaller<TInstance, TDelegate>(methodName, throwOnError, allowUnsafeTypeBinding, parameters);
    }

    [Pure]
    public static Delegate? GenerateInstanceCaller(MethodInfo method, bool throwOnError = false, bool allowUnsafeTypeBinding = false)
    {
        return DevkitServer.API.Accessor.GenerateInstanceCaller(method, throwOnError, allowUnsafeTypeBinding);
    }

    [Pure]
    public static TDelegate? GenerateInstanceCaller<TDelegate>(MethodInfo method, bool throwOnError = false, bool allowUnsafeTypeBinding = false) where TDelegate : Delegate
    {
        return DevkitServer.API.Accessor.GenerateInstanceCaller<TDelegate>(method, throwOnError, allowUnsafeTypeBinding);
    }

    [Pure]
    public static Delegate? GenerateInstanceCaller(Type delegateType, MethodInfo method, bool throwOnError = false, bool allowUnsafeTypeBinding = false)
    {
        return DevkitServer.API.Accessor.GenerateInstanceCaller(delegateType, method, throwOnError, allowUnsafeTypeBinding);
    }

    [Pure]
    public static Delegate? GenerateStaticCaller<TDeclaringType>(string methodName, Type[]? parameters = null, bool throwOnError = false, bool allowUnsafeTypeBinding = false)
    {
        return DevkitServer.API.Accessor.GenerateStaticCaller<TDeclaringType>(methodName, parameters, throwOnError, allowUnsafeTypeBinding);
    }

    [Pure]
    public static TDelegate? GenerateStaticCaller<TDeclaringType, TDelegate>(string methodName, bool throwOnError = false, bool allowUnsafeTypeBinding = false, Type[]? parameters = null) where TDelegate : Delegate
    {
        return DevkitServer.API.Accessor.GenerateStaticCaller<TDeclaringType, TDelegate>(methodName, throwOnError, allowUnsafeTypeBinding, parameters);
    }

    [Pure]
    public static Delegate? GenerateStaticCaller(MethodInfo method, bool throwOnError = false, bool allowUnsafeTypeBinding = false)
    {
        return DevkitServer.API.Accessor.GenerateStaticCaller(method, throwOnError, allowUnsafeTypeBinding);
    }

    [Pure]
    public static TDelegate? GenerateStaticCaller<TDelegate>(MethodInfo method, bool throwOnError = false, bool allowUnsafeTypeBinding = false) where TDelegate : Delegate
    {
        return DevkitServer.API.Accessor.GenerateStaticCaller<TDelegate>(method, throwOnError, allowUnsafeTypeBinding);
    }

    [Pure]
    public static Delegate? GenerateStaticCaller(Type delegateType, MethodInfo method, bool throwOnError = false, bool allowUnsafeTypeBinding = false)
    {
        return DevkitServer.API.Accessor.GenerateStaticCaller(delegateType, method, throwOnError, allowUnsafeTypeBinding);
    }

    public static void GetDynamicMethodFlags(bool @static, out MethodAttributes attributes, out CallingConventions convention)
    {
        DevkitServer.API.Accessor.GetDynamicMethodFlags(@static, out attributes, out convention);
    }

    [Pure]
    public static bool IsExtern(this MethodBase method)
    {
        return DevkitServer.API.Accessor.IsExtern(method);
    }

    [Pure]
    public static bool IsExtern(this FieldInfo field)
    {
        return DevkitServer.API.Accessor.IsExtern(field);
    }

    [Pure]
    public static bool IsExtern(this PropertyInfo property, bool checkGetterFirst = true)
    {
        return DevkitServer.API.Accessor.IsExtern(property, checkGetterFirst);
    }

    [Pure]
    public static bool IsIgnored(this Type type)
    {
        return Attribute.IsDefined(type, typeof(IgnoreAttribute)) || DevkitServer.API.Accessor.IsIgnored(type);
    }

    [Pure]
    public static bool IsIgnored(this MemberInfo member)
    {
        return Attribute.IsDefined(member, typeof(IgnoreAttribute)) || DevkitServer.API.Accessor.IsIgnored(member);
    }

    [Pure]
    public static bool IsIgnored(this Assembly assembly)
    {
        return Attribute.IsDefined(assembly, typeof(IgnoreAttribute)) || DevkitServer.API.Accessor.IsIgnored(assembly);
    }

    [Pure]
    public static bool IsIgnored(this ParameterInfo parameter)
    {
        return Attribute.IsDefined(parameter, typeof(IgnoreAttribute)) || DevkitServer.API.Accessor.IsIgnored(parameter);
    }

    [Pure]
    public static bool IsIgnored(this Module module)
    {
        return Attribute.IsDefined(module, typeof(IgnoreAttribute)) || DevkitServer.API.Accessor.IsIgnored(module);
    }

    [Pure]
    public static int GetPriority(this Type type)
    {
        int p = DevkitServer.API.Accessor.GetPriority(type);
        if (p == 0)
            p = Attribute.GetCustomAttribute(type, typeof(PriorityAttribute)) is PriorityAttribute attr ? attr.Priority : 0;
        return p;
    }

    [Pure]
    public static int GetPriority(this MemberInfo member)
    {
        int p = DevkitServer.API.Accessor.GetPriority(member);
        if (p == 0)
            p = Attribute.GetCustomAttribute(member, typeof(PriorityAttribute)) is PriorityAttribute attr ? attr.Priority : 0;
        return p;
    }

    [Pure]
    public static int GetPriority(this Assembly assembly)
    {
        int p = DevkitServer.API.Accessor.GetPriority(assembly);
        if (p == 0)
            p = Attribute.GetCustomAttribute(assembly, typeof(PriorityAttribute)) is PriorityAttribute attr ? attr.Priority : 0;
        return p;
    }

    [Pure]
    public static int GetPriority(this ParameterInfo parameter)
    {
        int p = DevkitServer.API.Accessor.GetPriority(parameter);
        if (p == 0)
            p = Attribute.GetCustomAttribute(parameter, typeof(PriorityAttribute)) is PriorityAttribute attr ? attr.Priority : 0;
        return p;
    }

    [Pure]
    public static int GetPriority(this Module module)
    {
        int p = DevkitServer.API.Accessor.GetPriority(module);
        if (p == 0)
            p = Attribute.GetCustomAttribute(module, typeof(PriorityAttribute)) is PriorityAttribute attr ? attr.Priority : 0;
        return p;
    }

    [Pure]
    public static int SortTypesByPriorityHandler(Type a, Type b) => DevkitServer.API.Accessor.SortTypesByPriorityHandler(a, b);

    [Pure]
    public static int SortMembersByPriorityHandler(MemberInfo a, MemberInfo b) => DevkitServer.API.Accessor.SortMembersByPriorityHandler(a, b);

    [Pure]
    public static MethodInfo? GetMethod(Delegate @delegate) => DevkitServer.API.Accessor.GetMethod(@delegate);

    [Pure]
    public static Type? GetDefaultDelegate(Type returnType, IReadOnlyList<ParameterInfo> parameters, Type? instanceType)
    {
        return DevkitServer.API.Accessor.GetDefaultDelegate(returnType, parameters, instanceType);
    }
    public static void ForEachBaseType(this Type type, ForEachBaseType action, bool includeParent = true, bool excludeSystemBase = true)
    {
        DevkitServer.API.ForEachBaseType newDele = (DevkitServer.API.ForEachBaseType)action.Method.CreateDelegate(typeof(DevkitServer.API.ForEachBaseType));
        DevkitServer.API.Accessor.ForEachBaseType(type, newDele, includeParent, excludeSystemBase);
    }
    public static void ForEachBaseType(this Type type, ForEachBaseTypeWhile action, bool includeParent = true, bool excludeSystemBase = true)
    {
        DevkitServer.API.ForEachBaseTypeWhile newDele = (DevkitServer.API.ForEachBaseTypeWhile)action.Method.CreateDelegate(typeof(DevkitServer.API.ForEachBaseTypeWhile));
        DevkitServer.API.Accessor.ForEachBaseType(type, newDele, includeParent, excludeSystemBase);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    [Pure]
    public static List<Type> GetTypesSafe(bool removeIgnored = false)
    {
        Assembly asm = Assembly.GetCallingAssembly();
        return DevkitServer.API.Accessor.GetTypesSafe(asm, removeIgnored);
    }
    
    [Pure]
    public static List<Type> GetTypesSafe(Assembly assembly, bool removeIgnored = false)
    {
        return DevkitServer.API.Accessor.GetTypesSafe(assembly, removeIgnored);
    }
    
    [Pure]
    public static List<Type> GetTypesSafe(IEnumerable<Assembly> assmeblies, bool removeIgnored = false)
    {
        return DevkitServer.API.Accessor.GetTypesSafe(assmeblies, removeIgnored);
    }

    [Pure]
    public static MethodInfo? GetImplementedMethod(Type type, MethodInfo interfaceMethod)
    {
        return DevkitServer.API.Accessor.GetImplementedMethod(type, interfaceMethod);
    }
    
    public static void GetDelegateSignature<TDelegate>(out Type returnType, out ParameterInfo[] parameters) where TDelegate : Delegate
    {
        DevkitServer.API.Accessor.GetDelegateSignature<TDelegate>(out returnType, out parameters);
    }

    [Pure]
    public static Type GetReturnType<TDelegate>() where TDelegate : Delegate
    {
        return DevkitServer.API.Accessor.GetReturnType<TDelegate>();
    }

    [Pure]
    public static ParameterInfo[] GetParameters<TDelegate>() where TDelegate : Delegate
    {
        return DevkitServer.API.Accessor.GetParameters<TDelegate>();
    }

    [Pure]
    public static MethodInfo GetInvokeMethod<TDelegate>() where TDelegate : Delegate
    {
        return DevkitServer.API.Accessor.GetInvokeMethod<TDelegate>();
    }

    public static void GetDelegateSignature(Type delegateType, out Type returnType, out ParameterInfo[] parameters)
    {
        DevkitServer.API.Accessor.GetDelegateSignature(delegateType, out returnType, out parameters);
    }

    [Pure]
    public static Type GetReturnType(Type delegateType)
    {
        return DevkitServer.API.Accessor.GetReturnType(delegateType);
    }

    [Pure]
    public static ParameterInfo[] GetParameters(Type delegateType)
    {
        return DevkitServer.API.Accessor.GetParameters(delegateType);
    }

    [Pure]
    public static MethodInfo GetInvokeMethod(Type delegateType)
    {
        return DevkitServer.API.Accessor.GetInvokeMethod(delegateType);
    }

    [Pure]
    public static Type? GetMemberType(this MemberInfo member)
    {
        return DevkitServer.API.Accessor.GetMemberType(member);
    }

    [Pure]
    public static bool GetIsStatic(this MemberInfo member)
    {
        return DevkitServer.API.Accessor.GetIsStatic(member);
    }

    [Pure]
    public static bool ShouldCallvirt(this MethodBase method)
    {
        return DevkitServer.API.Accessor.ShouldCallvirt(method);
    }

    [Pure]
    public static bool ShouldCallvirtRuntime(this MethodBase method)
    {
        return DevkitServer.API.Accessor.ShouldCallvirtRuntime(method);
    }
}

public delegate void InstanceSetter<in TInstance, in T>(TInstance owner, T value);
public delegate T InstanceGetter<in TInstance, out T>(TInstance owner);
public delegate void StaticSetter<in T>(T value);
public delegate T StaticGetter<out T>();
public delegate void ForEachBaseType(Type type, int depth);
public delegate bool ForEachBaseTypeWhile(Type type, int depth);