using DevkitServer.API;
using DevkitServer.Patches;
using HarmonyLib;
using StackCleaner;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

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
    private static ConstructorInfo? _nreExCtor;
    private static ConstructorInfo? _stackTraceIntCtor;

    internal static Type[]? FuncTypes;
    internal static Type[]? ActionTypes;
    private static bool _castExCtorCalc;
    private static bool _nreExCtorCalc;

    /// <summary>
    /// Generates a dynamic method that sets an instance field value. For value types use <see cref="GenerateInstanceSetter{TValue}"/> instead.
    /// </summary>
    /// <typeparam name="TInstance">Declaring type</typeparam>
    /// <typeparam name="TValue">Field type</typeparam>
    [Pure]
    public static InstanceSetter<TInstance, TValue>? GenerateInstanceSetter<TInstance, TValue>(string fieldName, bool throwOnError = false)
    {
        if (typeof(TInstance).IsValueType)
        {
            if (throwOnError)
                throw new Exception($"Unable to create instance setter for {typeof(TInstance).FullName}.{fieldName}, you must pass structs ({typeof(TInstance).Name}) as a boxed object.");

            Logger.LogError($"Unable to create instance setter for {typeof(TInstance).Format()}.{fieldName.Colorize(FormattingColorType.Property)}, you must pass structs ({typeof(TInstance).Format()}) as a boxed object.");
            return null;
        }

        FieldInfo? field = typeof(TInstance).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        field ??= typeof(TInstance).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
        if (field is null || field.IsStatic || (!field.FieldType.IsAssignableFrom(typeof(TValue)) && typeof(TValue) != typeof(object)))
        {
            if (throwOnError)
                throw new Exception($"Unable to find matching field: {typeof(TInstance).FullName}.{fieldName}.");
            Logger.LogError($"Unable to find matching property {typeof(TInstance).Format()}.{fieldName.Colorize(FormattingColorType.Property)}.");
            return null;
        }

        try
        {
            GetDynamicMethodFlags(false, out MethodAttributes attr, out CallingConventions convention);
            DynamicMethod method = new DynamicMethod("set_" + fieldName, attr, convention, typeof(void), new Type[] { typeof(TInstance), field.FieldType }, typeof(TInstance), true);
            method.DefineParameter(1, ParameterAttributes.None, "this");
            method.DefineParameter(2, ParameterAttributes.None, "value");
            ILGenerator il = method.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);

            if (!typeof(TValue).IsValueType && field.FieldType.IsValueType)
                il.Emit(OpCodes.Unbox_Any, field.FieldType);
            else if (typeof(TValue).IsValueType && !field.FieldType.IsValueType)
                il.Emit(OpCodes.Box, typeof(TValue));

            il.Emit(OpCodes.Stfld, field);
            il.Emit(OpCodes.Ret);
            return (InstanceSetter<TInstance, TValue>)method.CreateDelegate(typeof(InstanceSetter<TInstance, TValue>));
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error generating instance getter for {typeof(TInstance).Format()}.{fieldName.Colorize(FormattingColorType.Property)}.");
            if (throwOnError)
                throw;
            Logger.LogError(ex);
            return null;
        }
    }
    /// <summary>
    /// Generates a dynamic method that gets an instance field value. Works for reference or value types.
    /// </summary>
    /// <typeparam name="TInstance">Declaring type</typeparam>
    /// <typeparam name="TValue">Field type</typeparam>
    [Pure]
    public static InstanceGetter<TInstance, TValue>? GenerateInstanceGetter<TInstance, TValue>(string fieldName, bool throwOnError = false)
    {
        FieldInfo? field = typeof(TInstance).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        field ??= typeof(TInstance).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
        if (field is null || field.IsStatic || (!typeof(TValue).IsAssignableFrom(field.FieldType) && field.FieldType != typeof(object)))
        {
            if (throwOnError)
                throw new Exception($"Unable to find matching field: {typeof(TInstance).FullName}.{fieldName}.");
            Logger.LogError($"Unable to find matching property {typeof(TInstance).Format()}.{fieldName.Colorize(FormattingColorType.Property)}.");
            return null;
        }
        try
        {
            GetDynamicMethodFlags(false, out MethodAttributes attr, out CallingConventions convention);
            DynamicMethod method = new DynamicMethod("get_" + fieldName, attr, convention, typeof(TValue), new Type[] { typeof(TInstance) }, typeof(TInstance), true);
            method.DefineParameter(1, ParameterAttributes.None, "this");
            ILGenerator il = method.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, field);

            if (typeof(TValue).IsValueType && !field.FieldType.IsValueType)
                il.Emit(OpCodes.Unbox_Any, typeof(TValue));
            else if (!typeof(TValue).IsValueType && field.FieldType.IsValueType)
                il.Emit(OpCodes.Box, field.FieldType);

            il.Emit(OpCodes.Ret);
            return (InstanceGetter<TInstance, TValue>)method.CreateDelegate(typeof(InstanceGetter<TInstance, TValue>));
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error generating instance getter for {typeof(TInstance).Format()}.{fieldName.Colorize(FormattingColorType.Property)}.");
            if (throwOnError)
                throw;
            Logger.LogError(ex);
            return null;
        }
    }
    /// <summary>
    /// Generates a dynamic method that sets an instance field value.
    /// When using value types, you have to store the value type in a boxed variable before passing it. This allows you to pass it as a reference type.<br/><br/>
    /// <code>
    /// object instance = new CustomStruct();
    /// SetField.Invoke(instance, 3);
    /// CustomStruct result = (CustomStruct)instance;
    /// </code>
    /// </summary>
    /// <param name="instance">Declaring type</param>
    /// <typeparam name="TValue">Field type</typeparam>
    [Pure]
    public static InstanceSetter<object, TValue>? GenerateInstanceSetter<TValue>(Type instance, string fieldName, bool throwOnError = false)
    {
        if (instance == null)
        {
            if (throwOnError)
                throw new Exception($"Error generating instance setter for <unknown>.{fieldName}. Declaring type not found.");
            Logger.LogError($"Error generating instance setter for <unknown>.{fieldName.Colorize(FormattingColorType.Property)}. Declaring type not found.");
            return null;
        }
        FieldInfo? field = instance.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        field ??= instance.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
        if (field is null || field.IsStatic || (!field.FieldType.IsAssignableFrom(typeof(TValue)) && typeof(TValue) != typeof(object)))
        {
            if (throwOnError)
                throw new Exception($"Unable to find matching field: {instance.FullName}.{fieldName}.");
            Logger.LogError($"Unable to find matching property {instance.Format()}.{fieldName.Colorize(FormattingColorType.Property)}.");
            return null;
        }
        try
        {
            CheckExceptionConstructors();

            GetDynamicMethodFlags(false, out MethodAttributes attr, out CallingConventions convention);
            DynamicMethod method = new DynamicMethod("set_" + fieldName, attr, convention, typeof(void), new Type[] { typeof(object), field.FieldType }, instance, true);
            method.DefineParameter(1, ParameterAttributes.None, "this");
            method.DefineParameter(2, ParameterAttributes.None, "value");
            ILGenerator il = method.GetILGenerator();
            Label lbl = il.DefineLabel();
            il.Emit(OpCodes.Ldarg_0);

            bool isValueType = instance.IsValueType;
            if (_castExCtor != null || !isValueType && _nreExCtor != null)
            {
                Label lbl2 = il.DefineLabel();
                il.Emit(OpCodes.Isinst, instance);
                if (!isValueType)
                    il.Emit(OpCodes.Dup);
                il.Emit(OpCodes.Brtrue, lbl);
                if (!isValueType)
                    il.Emit(OpCodes.Pop);
                il.Emit(OpCodes.Ldnull);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Beq_S, lbl2);

                il.Emit(OpCodes.Ldstr, "Invalid instance type passed to getter for " + fieldName + ". Expected " + instance.FullName + ".");
                il.Emit(OpCodes.Newobj, _castExCtor ?? _nreExCtor!);
                il.Emit(OpCodes.Throw);
                il.MarkLabel(lbl2);
                ConstructorInfo ctor = _nreExCtor ?? _castExCtor!;
                if (ctor == _castExCtor)
                    il.Emit(OpCodes.Ldstr, "Null passed to getter for " + fieldName + ". Expected " + instance.FullName + ".");
                il.Emit(OpCodes.Newobj, ctor);
                il.Emit(OpCodes.Throw);
            }
            il.MarkLabel(lbl);
            if (isValueType)
            {
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Unbox, instance);
                il.Emit(OpCodes.Ldflda, field);
                il.Emit(OpCodes.Ldarg_1);

                if (!typeof(TValue).IsValueType && field.FieldType.IsValueType)
                    il.Emit(OpCodes.Unbox_Any, field.FieldType);
                else if (typeof(TValue).IsValueType && !field.FieldType.IsValueType)
                    il.Emit(OpCodes.Box, typeof(TValue));

                il.Emit(OpCodes.Stobj, field.FieldType);
            }
            else
            {
                il.Emit(OpCodes.Ldarg_1);

                if (!typeof(TValue).IsValueType && field.FieldType.IsValueType)
                    il.Emit(OpCodes.Unbox_Any, field.FieldType);
                else if (typeof(TValue).IsValueType && !field.FieldType.IsValueType)
                    il.Emit(OpCodes.Box, typeof(TValue));

                il.Emit(OpCodes.Stfld, field);
            }
            il.Emit(OpCodes.Ret);
            return (InstanceSetter<object, TValue>)method.CreateDelegate(typeof(InstanceSetter<object, TValue>));
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error generating instance setter for {instance.Format()}.{fieldName.Colorize(FormattingColorType.Property)}.");
            if (throwOnError)
                throw;
            Logger.LogError(ex);
            return null;
        }
    }
    /// <summary>
    /// Generates a dynamic method that gets an instance field value. Works for reference or value types.
    /// </summary>
    /// <param name="instance">Declaring type</param>
    /// <typeparam name="TValue">Field type</typeparam>
    [Pure]
    public static InstanceGetter<object, TValue>? GenerateInstanceGetter<TValue>(Type instance, string fieldName, bool throwOnError = false)
    {
        if (instance == null)
        {
            if (throwOnError)
                throw new Exception($"Error generating instance getter for <unknown>.{fieldName}. Declaring type not found.");
            Logger.LogError($"Error generating instance getter for <unknown>.{fieldName.Colorize(FormattingColorType.Property)}. Declaring type not found.");
            return null;
        }
        FieldInfo? field = instance.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        field ??= instance.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
        if (field is null || field.IsStatic || (!typeof(TValue).IsAssignableFrom(field.FieldType) && field.FieldType != typeof(object)))
        {
            if (throwOnError)
                throw new Exception($"Unable to find matching field: {instance.FullName}.{fieldName}.");
            Logger.LogError($"Unable to find matching property {instance.Format()}.{fieldName.Colorize(FormattingColorType.Property)}.");
            return null;
        }
        try
        {
            CheckExceptionConstructors();

            GetDynamicMethodFlags(false, out MethodAttributes attr, out CallingConventions convention);
            DynamicMethod method = new DynamicMethod("get_" + fieldName, attr, convention, typeof(TValue), new Type[] { typeof(object) }, instance, true);
            method.DefineParameter(1, ParameterAttributes.None, "this");
            ILGenerator il = method.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            Label lbl = il.DefineLabel();

            bool isValueType = instance.IsValueType;
            if (_castExCtor != null || !isValueType && _nreExCtor != null)
            {
                Label lbl2 = il.DefineLabel();
                il.Emit(OpCodes.Isinst, instance);
                il.Emit(OpCodes.Dup);
                il.Emit(OpCodes.Brtrue, lbl);
                il.Emit(OpCodes.Pop);
                il.Emit(OpCodes.Ldnull);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Beq_S, lbl2);
                il.Emit(OpCodes.Ldstr, "Invalid instance type passed to getter for " + fieldName + ". Expected " + instance.FullName + ".");
                il.Emit(OpCodes.Newobj, _castExCtor ?? _nreExCtor!);
                il.Emit(OpCodes.Throw);
                il.MarkLabel(lbl2);
                ConstructorInfo ctor = _nreExCtor ?? _castExCtor!;
                if (ctor == _castExCtor)
                    il.Emit(OpCodes.Ldstr, "Null passed to getter for " + fieldName + ". Expected " + instance.FullName + ".");
                il.Emit(OpCodes.Newobj, ctor);
                il.Emit(OpCodes.Throw);
            }
            il.MarkLabel(lbl);
            if (isValueType)
                il.Emit(OpCodes.Unbox, instance);
            il.Emit(OpCodes.Ldfld, field);

            if (typeof(TValue).IsValueType && !field.FieldType.IsValueType)
                il.Emit(OpCodes.Unbox_Any, typeof(TValue));
            else if (!typeof(TValue).IsValueType && field.FieldType.IsValueType)
                il.Emit(OpCodes.Box, field.FieldType);

            il.Emit(OpCodes.Ret);
            return (InstanceGetter<object, TValue>)method.CreateDelegate(typeof(InstanceGetter<object, TValue>));
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error generating instance getter for {instance.Format()}.{fieldName.Colorize(FormattingColorType.Property)}.");
            if (throwOnError)
                throw;
            Logger.LogError(ex);
            return null;
        }
    }
    /// <summary>
    /// Generates a delegate that sets an instance property value. For value types use <see cref="GenerateInstanceSetter{TValue}"/> instead.
    /// </summary>
    /// <typeparam name="TInstance">Declaring type</typeparam>
    /// <typeparam name="TValue">Property type</typeparam>
    [Pure]
    public static InstanceSetter<TInstance, TValue>? GenerateInstancePropertySetter<TInstance, TValue>(string propertyName, bool throwOnError = false)
    {
        if (typeof(TInstance).IsValueType)
        {
            if (throwOnError)
                throw new Exception($"Unable to create instance setter for {typeof(TInstance).FullName}.{propertyName}, you must pass structs ({typeof(TInstance).Name}) as a boxed object.");

            Logger.LogError($"Unable to create instance setter for {typeof(TInstance).Format()}.{propertyName.Colorize(FormattingColorType.Property)}, you must pass structs ({typeof(TInstance).Format()}) as a boxed object.");
            return null;
        }

        PropertyInfo? property = typeof(TInstance).GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        MethodInfo? setter = property?.GetSetMethod(true);
        if (setter is null || setter.IsStatic || setter.GetParameters() is not { Length: 1 } parameters || (!parameters[0].ParameterType.IsAssignableFrom(typeof(TValue)) && typeof(TValue) != typeof(object)))
        {
            property = typeof(TInstance).GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            setter = property?.GetSetMethod(true);

            if (setter is null || setter.IsStatic || setter.GetParameters() is not { Length: 1 } parameters2 || (!parameters2[0].ParameterType.IsAssignableFrom(typeof(TValue)) && typeof(TValue) != typeof(object)))
            {
                if (throwOnError)
                    throw new Exception($"Unable to find matching property: {typeof(TInstance).FullName}.{propertyName} with a setter.");
                Logger.LogError($"Unable to find matching property {typeof(TInstance).Format()}.{propertyName.Colorize(FormattingColorType.Property)} with a setter.");
                return null;
            }
        }

        return GenerateInstanceCaller<InstanceSetter<TInstance, TValue>>(setter, throwOnError, true);
    }
    /// <summary>
    /// Generates a delegate that gets an instance property value. Works for reference or value types.
    /// </summary>
    /// <typeparam name="TInstance">Declaring type</typeparam>
    /// <typeparam name="TValue">Property type</typeparam>
    [Pure]
    public static InstanceGetter<TInstance, TValue>? GenerateInstancePropertyGetter<TInstance, TValue>(string propertyName, bool throwOnError = false)
    {
        PropertyInfo? property = typeof(TInstance).GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        MethodInfo? getter = property?.GetGetMethod(true);
        if (getter is null || getter.IsStatic || (!typeof(TValue).IsAssignableFrom(getter.ReturnType) && getter.ReturnType != typeof(object)))
        {
            property = typeof(TInstance).GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            getter = property?.GetGetMethod(true);

            if (getter is null || getter.IsStatic || (!typeof(TValue).IsAssignableFrom(getter.ReturnType) && getter.ReturnType != typeof(object)))
            {
                if (throwOnError)
                    throw new Exception($"Unable to find matching property: {typeof(TInstance).FullName}.{propertyName} with a getter.");
                Logger.LogError($"Unable to find matching property {typeof(TInstance).Format()}.{propertyName.Colorize(FormattingColorType.Property)} with a getter.");
                return null;
            }
        }

        return GenerateInstanceCaller<InstanceGetter<TInstance, TValue>>(getter, throwOnError, true);
    }
    /// <summary>
    /// Generates a delegate if possible, otherwise a dynamic method, that sets an instance property value.
    /// When using value types, you have to store the value type in a boxed variable before passing it. This allows you to pass it as a reference type.<br/><br/>
    /// <code>
    /// object instance = new CustomStruct();
    /// SetProperty.Invoke(instance, 3);
    /// CustomStruct result = (CustomStruct)instance;
    /// </code>
    /// </summary>
    /// <param name="instance">Declaring type</param>
    /// <param name="allowUnsafeTypeBinding">Enables unsafe type binding to non-matching delegates, meaning classes of different
    /// types can be passed as parameters and an exception will not be thrown (may cause unintended behavior if the wrong type is passed).</param>
    /// <typeparam name="TValue">Property type</typeparam>
    [Pure]
    public static InstanceSetter<object, TValue>? GenerateInstancePropertySetter<TValue>(Type instance, string propertyName, bool throwOnError = false, bool allowUnsafeTypeBinding = false)
    {
        if (instance == null)
        {
            if (throwOnError)
                throw new Exception($"Error generating instance setter for <unknown>.{propertyName}. Declaring type not found.");
            Logger.LogError($"Error generating instance setter for <unknown>.{propertyName.Colorize(FormattingColorType.Property)}. Declaring type not found.");
            return null;
        }
        PropertyInfo? property = instance.GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        MethodInfo? setter = property?.GetSetMethod(true);
        if (setter is null || setter.IsStatic || setter.GetParameters() is not { Length: 1 } parameters || (!parameters[0].ParameterType.IsAssignableFrom(typeof(TValue)) && typeof(TValue) != typeof(object)))
        {
            property = instance.GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            setter = property?.GetSetMethod(true);

            if (setter is null || setter.IsStatic || setter.GetParameters() is not { Length: 1 } parameters2 || (!parameters2[0].ParameterType.IsAssignableFrom(typeof(TValue)) && typeof(TValue) != typeof(object)))
            {
                if (throwOnError)
                    throw new Exception($"Unable to find matching property: {instance.FullName}.{propertyName} with a setter.");
                Logger.LogError($"Unable to find matching property {instance.Format()}.{propertyName.Colorize(FormattingColorType.Property)} with a setter.");
                return null;
            }
        }

        return GenerateInstanceCaller<InstanceSetter<object, TValue>>(setter, throwOnError, allowUnsafeTypeBinding);
    }
    /// <summary>
    /// Generates a delegate if possible, otherwise a dynamic method, that gets an instance property value. Works for reference or value types.
    /// </summary>
    /// <param name="instance">Declaring type</param>
    /// <param name="allowUnsafeTypeBinding">Enables unsafe type binding to non-matching delegates, meaning classes of different
    /// types can be passed as parameters and an exception will not be thrown (may cause unintended behavior if the wrong type is passed).</param>
    /// <typeparam name="TValue">Property type</typeparam>
    [Pure]
    public static InstanceGetter<object, TValue>? GenerateInstancePropertyGetter<TValue>(Type instance, string propertyName, bool throwOnError = false, bool allowUnsafeTypeBinding = false)
    {
        if (instance == null)
        {
            if (throwOnError)
                throw new Exception($"Error generating instance getter for <unknown>.{propertyName}. Declaring type not found.");
            Logger.LogError($"Error generating instance getter for <unknown>.{propertyName.Colorize(FormattingColorType.Property)}. Declaring type not found.");
            return null;
        }

        PropertyInfo? property = instance.GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        MethodInfo? getter = property?.GetGetMethod(true);
        if (getter is null || getter.IsStatic || (!typeof(TValue).IsAssignableFrom(getter.ReturnType) && getter.ReturnType != typeof(object)))
        {
            property = instance.GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            getter = property?.GetGetMethod(true);

            if (getter is null || getter.IsStatic || (!typeof(TValue).IsAssignableFrom(getter.ReturnType) && getter.ReturnType != typeof(object)))
            {
                if (throwOnError)
                    throw new Exception($"Unable to find matching property: {instance.FullName}.{propertyName} with a getter.");
                Logger.LogError($"Unable to find matching property {instance.Format()}.{propertyName.Colorize(FormattingColorType.Property)} with a getter.");
                return null;
            }
        }

        return GenerateInstanceCaller<InstanceGetter<object, TValue>>(getter, throwOnError, allowUnsafeTypeBinding);
    }
    /// <summary>
    /// Generates a dynamic method that sets a static field value.
    /// </summary>
    /// <typeparam name="TDeclaringType">Declaring type</typeparam>
    /// <typeparam name="TValue">Field type</typeparam>
    [Pure]
    public static StaticSetter<TValue>? GenerateStaticSetter<TDeclaringType, TValue>(string fieldName, bool throwOnError = false)
        => GenerateStaticSetter<TValue>(typeof(TDeclaringType), fieldName, throwOnError);
    /// <summary>
    /// Generates a dynamic method that gets a static field value.
    /// </summary>
    /// <typeparam name="TDeclaringType">Declaring type</typeparam>
    /// <typeparam name="TValue">Field type</typeparam>
    [Pure]
    public static StaticGetter<TValue>? GenerateStaticGetter<TDeclaringType, TValue>(string fieldName, bool throwOnError = false)
        => GenerateStaticGetter<TValue>(typeof(TDeclaringType), fieldName, throwOnError);
    /// <summary>
    /// Generates a dynamic method that sets a static field value.
    /// </summary>
    /// <param name="declaringType">Declaring type</typeparam>
    /// <typeparam name="TValue">Field type</typeparam>
    [Pure]
    public static StaticSetter<TValue>? GenerateStaticSetter<TValue>(Type declaringType, string fieldName, bool throwOnError = false)
    {
        if (declaringType == null)
        {
            if (throwOnError)
                throw new Exception($"Error generating static setter for <unknown>.{fieldName}. Declaring type not found.");
            Logger.LogError($"Error generating static setter for <unknown>.{fieldName.Colorize(FormattingColorType.Property)}. Declaring type not found.");
            return null;
        }
        FieldInfo? field = declaringType.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);

        if (field is null || !field.IsStatic || (!field.FieldType.IsAssignableFrom(typeof(TValue)) && typeof(TValue) != typeof(object)))
        {
            if (throwOnError)
                throw new Exception($"Unable to find matching field: {declaringType.FullName}.{fieldName}.");
            Logger.LogError($"Unable to find matching field {declaringType.Format()}.{fieldName.Colorize(FormattingColorType.Property)}.");
            return null;
        }
        try
        {
            GetDynamicMethodFlags(true, out MethodAttributes attr, out CallingConventions convention);
            DynamicMethod method = new DynamicMethod("set_" + fieldName, attr, convention, typeof(void), new Type[] { typeof(TValue) }, declaringType, true);
            method.DefineParameter(1, ParameterAttributes.None, "value");
            ILGenerator il = method.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);

            if (!typeof(TValue).IsValueType && field.FieldType.IsValueType)
                il.Emit(OpCodes.Unbox_Any, field.FieldType);
            else if (typeof(TValue).IsValueType && !field.FieldType.IsValueType)
                il.Emit(OpCodes.Box, typeof(TValue));
            
            il.Emit(OpCodes.Stsfld, field);
            il.Emit(OpCodes.Ret);
            return (StaticSetter<TValue>)method.CreateDelegate(typeof(StaticSetter<TValue>));
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error generating static setter for {declaringType.Format()}.{fieldName.Colorize(FormattingColorType.Property)}.");
            if (throwOnError)
                throw;
            Logger.LogError(ex);
            return null;
        }
    }
    /// <summary>
    /// Generates a dynamic method that gets a static field value.
    /// </summary>
    /// <param name="declaringType">Declaring type</typeparam>
    /// <typeparam name="TValue">Field type</typeparam>
    [Pure]
    public static StaticGetter<TValue>? GenerateStaticGetter<TValue>(Type declaringType, string fieldName, bool throwOnError = false)
    {
        if (declaringType == null)
        {
            if (throwOnError)
                throw new Exception($"Error generating static getter for <unknown>.{fieldName}. Declaring type not found.");
            Logger.LogError($"Error generating static getter for <unknown>.{fieldName.Colorize(FormattingColorType.Property)}. Declaring type not found.");
            return null;
        }
        FieldInfo? field = declaringType.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);

        if (field is null || !field.IsStatic || (!typeof(TValue).IsAssignableFrom(field.FieldType) && field.FieldType != typeof(object)))
        {
            if (throwOnError)
                throw new Exception($"Unable to find matching field: {declaringType.FullName}.{fieldName}.");
            Logger.LogError($"Unable to find matching property {declaringType.Format()}.{fieldName.Colorize(FormattingColorType.Property)}.");
            return null;
        }
        try
        {
            GetDynamicMethodFlags(true, out MethodAttributes attr, out CallingConventions convention);
            DynamicMethod method = new DynamicMethod("get_" + fieldName, attr, convention, typeof(TValue), Array.Empty<Type>(), declaringType, true);
            ILGenerator il = method.GetILGenerator();
            il.Emit(OpCodes.Ldsfld, field);

            if (typeof(TValue).IsValueType && !field.FieldType.IsValueType)
                il.Emit(OpCodes.Unbox_Any, typeof(TValue));
            else if (!typeof(TValue).IsValueType && field.FieldType.IsValueType)
                il.Emit(OpCodes.Box, field.FieldType);

            il.Emit(OpCodes.Ret);
            return (StaticGetter<TValue>)method.CreateDelegate(typeof(StaticGetter<TValue>));
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error generating static getter for {declaringType.Format()}.{fieldName.Colorize(FormattingColorType.Property)}.");
            if (throwOnError)
                throw;
            Logger.LogError(ex);
            return null;
        }
    }
    /// <summary>
    /// Generates a delegate or dynamic method that sets a static property value.
    /// </summary>
    /// <typeparam name="TDeclaringType">Declaring type</typeparam>
    /// <typeparam name="TValue">Property type</typeparam>
    [Pure]
    public static StaticSetter<TValue>? GenerateStaticPropertySetter<TDeclaringType, TValue>(string propertyName, bool throwOnError = false)
        => GenerateStaticPropertySetter<TValue>(typeof(TDeclaringType), propertyName, throwOnError, true);
    /// <summary>
    /// Generates a delegate that gets a static property value.
    /// </summary>
    /// <typeparam name="TDeclaringType">Declaring type</typeparam>
    /// <typeparam name="TValue">Property type</typeparam>
    [Pure]
    public static StaticGetter<TValue>? GenerateStaticPropertyGetter<TDeclaringType, TValue>(string propertyName, bool throwOnError = false)
        => GenerateStaticPropertyGetter<TValue>(typeof(TDeclaringType), propertyName, throwOnError, true);
    /// <summary>
    /// Generates a delegate or dynamic method that sets a static property value.
    /// </summary>
    /// <param name="allowUnsafeTypeBinding">Enables unsafe type binding to non-matching delegates, meaning classes of different
    /// types can be passed as parameters and an exception will not be thrown (may cause unintended behavior if the wrong type is passed).</param>
    /// <param name="declaringType">Declaring type</typeparam>
    /// <typeparam name="TValue">Property type</typeparam>
    [Pure]
    public static StaticSetter<TValue>? GenerateStaticPropertySetter<TValue>(Type declaringType, string propertyName, bool throwOnError = false, bool allowUnsafeTypeBinding = false)
    {
        if (declaringType == null)
        {
            if (throwOnError)
                throw new Exception($"Error generating static setter for <unknown>.{propertyName}. Declaring type not found.");
            Logger.LogError($"Error generating static setter for <unknown>.{propertyName.Colorize(FormattingColorType.Property)}. Declaring type not found.");
            return null;
        }
        PropertyInfo? property = declaringType.GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
        MethodInfo? setter = property?.GetSetMethod(true);

        if (setter is null || !setter.IsStatic || setter.GetParameters() is not { Length: 1 } parameters || (!parameters[0].ParameterType.IsAssignableFrom(typeof(TValue)) && typeof(TValue) != typeof(object)))
        {
            if (throwOnError)
                throw new Exception($"Unable to find matching property: {declaringType.FullName}.{propertyName} with a setter.");
            Logger.LogError($"Unable to find matching property {declaringType.Format()}.{propertyName.Colorize(FormattingColorType.Property)} with a setter.");
            return null;
        }

        return GenerateStaticCaller<StaticSetter<TValue>>(setter, throwOnError, allowUnsafeTypeBinding);
    }
    /// <summary>
    /// Generates a delegate that gets a static property value.
    /// </summary>
    /// <param name="allowUnsafeTypeBinding">Enables unsafe type binding to non-matching delegates, meaning classes of different
    /// types can be passed as parameters and an exception will not be thrown (may cause unintended behavior if the wrong type is passed).</param>
    /// <param name="declaringType">Declaring type</typeparam>
    /// <typeparam name="TValue">Property type</typeparam>
    [Pure]
    public static StaticGetter<TValue>? GenerateStaticPropertyGetter<TValue>(Type declaringType, string propertyName, bool throwOnError = false, bool allowUnsafeTypeBinding = false)
    {
        if (declaringType == null)
        {
            if (throwOnError)
                throw new Exception($"Error generating static getter for <unknown>.{propertyName}. Declaring type not found.");
            Logger.LogError($"Error generating static getter for <unknown>.{propertyName.Colorize(FormattingColorType.Property)}. Declaring type not found.");
            return null;
        }
        PropertyInfo? property = declaringType.GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
        MethodInfo? getter = property?.GetGetMethod(true);

        if (getter is null || !getter.IsStatic || (!typeof(TValue).IsAssignableFrom(getter.ReturnType) && getter.ReturnType != typeof(object)))
        {
            if (throwOnError)
                throw new Exception($"Unable to find matching property: {declaringType.FullName}.{propertyName} with a getter.");
            Logger.LogError($"Unable to find matching property {declaringType.Format()}.{propertyName.Colorize(FormattingColorType.Property)} with a getter.");
            return null;
        }

        return GenerateStaticCaller<StaticGetter<TValue>>(getter, throwOnError, allowUnsafeTypeBinding);
    }
    /// <summary>
    /// Generates a delegate or dynamic method that calls an instance method.
    /// </summary>
    /// <remarks>The first parameter will be the instance.</remarks>
    /// <param name="allowUnsafeTypeBinding">Enables unsafe type binding to non-matching delegates, meaning classes of different
    /// types can be passed as parameters and an exception will not be thrown (may cause unintended behavior if the wrong type is passed).</param>
    /// <returns>A delegate of type <see cref="Action"/> or <see cref="Func{T}"/> (or one of their generic counterparts), depending on the method signature. The first parameter will be the instance.</returns>
    [Pure]
    public static Delegate? GenerateInstanceCaller<TInstance>(string methodName, Type[]? parameters = null, bool throwOnError = false, bool allowUnsafeTypeBinding = false)
    {
        MethodInfo? method = null;
        bool noneByName = false;
        try
        {
            method = typeof(TInstance).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public) ??
                     typeof(TInstance).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy);
            noneByName = true;
        }
        catch (AmbiguousMatchException)
        {
            // ignored
        }
        if (method == null)
        {
            if (!noneByName && parameters != null)
            {
                method = typeof(TInstance).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                             null, CallingConventions.Any, parameters, null) ??
                         typeof(TInstance).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy,
                             null, CallingConventions.Any, parameters, null);
            }
            if (method == null)
            {
                if (throwOnError)
                    throw new Exception($"Unable to find matching instance method: {typeof(TInstance).FullName}.{methodName}.");

                Logger.LogError($"Unable to find matching instance method: {FormattingUtil.FormatMethod(null, typeof(TInstance), methodName, null, parameters, null, isStatic: false)}.");
                return null;
            }
        }

        return GenerateInstanceCaller(method, throwOnError, allowUnsafeTypeBinding);
    }
    /// <summary>
    /// Generates a delegate or dynamic method that calls an instance method.
    /// </summary>
    /// <remarks>The first parameter will be the instance.</remarks>
    /// <param name="allowUnsafeTypeBinding">Enables unsafe type binding to non-matching delegates, meaning classes of different
    /// types can be passed as parameters and an exception will not be thrown (may cause unintended behavior if the wrong type is passed).</param>
    [Pure]
    public static TDelegate? GenerateInstanceCaller<TInstance, TDelegate>(string methodName, bool throwOnError = false, bool allowUnsafeTypeBinding = false, Type[]? parameterOverride = null) where TDelegate : Delegate
    {
        MethodInfo? method = null;
        bool noneByName = false;
        try
        {
            method = typeof(TInstance).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public) ??
                     typeof(TInstance).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy);
            noneByName = true;
        }
        catch (AmbiguousMatchException)
        {
            // ignored
        }
        if (method == null)
        {
            if (!noneByName)
            {
                if (parameterOverride == null)
                {
                    ParameterInfo[] parameters = GetParameters<TDelegate>();
                    parameterOverride = parameters.Length < 2 ? Array.Empty<Type>() : new Type[parameters.Length - 1];
                    for (int i = 0; i < parameterOverride.Length; ++i)
                        parameterOverride[i] = parameters[i + 1].ParameterType;
                }
                method = typeof(TInstance).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                             null, CallingConventions.Any, parameterOverride, null) ??
                         typeof(TInstance).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy,
                             null, CallingConventions.Any, parameterOverride, null);
            }
            if (method == null)
            {
                if (throwOnError)
                    throw new Exception($"Unable to find matching instance method: {typeof(TInstance).FullName}.{methodName}.");

                Logger.LogError($"Unable to find matching instance method: {FormattingUtil.FormatMethod<TDelegate>(methodName, declTypeOverride: typeof(TInstance))}.");
                return null;
            }
        }

        return GenerateInstanceCaller<TDelegate>(method, throwOnError, allowUnsafeTypeBinding);
    }
    /// <summary>
    /// Generates a delegate or dynamic method that calls an instance method.
    /// </summary>
    /// <remarks>The first parameter will be the instance.</remarks>
    /// <returns>A delegate of type <see cref="Action"/> or <see cref="Func{T}"/> (or one of their generic counterparts), depending on the method signature. The first parameter will be the instance.</returns>
    /// <param name="allowUnsafeTypeBinding">Enables unsafe type binding to non-matching delegates, meaning classes of different
    /// types can be passed as parameters and an exception will not be thrown (may cause unintended behavior if the wrong type is passed).</param>
    [Pure]
    public static Delegate? GenerateInstanceCaller(MethodInfo method, bool throwOnError = false, bool allowUnsafeTypeBinding = false)
    {
        if (method == null || method.IsStatic || method.DeclaringType == null)
        {
            if (throwOnError)
                throw new Exception("Unable to find instance method.");
            Logger.LogError("Unable to find instance method.");
            return null;
        }

        CheckFuncArrays();

        bool rtn = method.ReturnType != typeof(void);
        ParameterInfo[] p = method.GetParameters();
        int maxArgs = rtn ? FuncTypes!.Length : ActionTypes!.Length;
        if (p.Length + 1 > maxArgs)
        {
            if (throwOnError)
                throw new ArgumentException($"Method {method.DeclaringType.FullName}.{method.Name} can not have more than {maxArgs} arguments!", nameof(method));

            Logger.LogError($"Method {method.Format()} can not have more than {maxArgs.Format()} arguments!");
            return null;
        }

        Type deleType = GetDefaultDelegate(method.ReturnType, p, method.DeclaringType)!;
        return GenerateInstanceCaller(deleType, method, throwOnError, allowUnsafeTypeBinding);
    }
    /// <summary>
    /// Generates a delegate or dynamic method that calls an instance method.
    /// </summary>
    /// <remarks>The first parameter will be the instance.</remarks>
    /// <param name="allowUnsafeTypeBinding">Enables unsafe type binding to non-matching delegates, meaning classes of different
    /// types can be passed as parameters and an exception will not be thrown (may cause unintended behavior if the wrong type is passed).</param>
    [Pure]
    public static TDelegate? GenerateInstanceCaller<TDelegate>(MethodInfo info, bool throwOnError = false, bool allowUnsafeTypeBinding = false) where TDelegate : Delegate
    {
        return (TDelegate?)GenerateInstanceCaller(typeof(TDelegate), info, throwOnError, allowUnsafeTypeBinding);
    }
    /// <summary>
    /// Generates a delegate or dynamic method that calls an instance method.
    /// </summary>
    /// <remarks>The first parameter will be the instance.</remarks>
    /// <param name="delegateType">Type of delegate to return.</param>
    /// <param name="allowUnsafeTypeBinding">Enables unsafe type binding to non-matching delegates, meaning classes of different
    /// types can be passed as parameters and an exception will not be thrown (may cause unintended behavior if the wrong type is passed).</param>
    [Pure]
    public static Delegate? GenerateInstanceCaller(Type delegateType, MethodInfo method, bool throwOnError = false, bool allowUnsafeTypeBinding = false)
    {
        if (!typeof(Delegate).IsAssignableFrom(delegateType))
            throw new ArgumentException(delegateType.Name + " is not a delegate.", nameof(delegateType));
        if (method == null || method.IsStatic || method.DeclaringType == null)
        {
            if (throwOnError)
                throw new Exception($"Unable to find instance method for delegate: {delegateType.Name}.");
            Logger.LogError($"Unable to find matching instance method: {FormattingUtil.FormatMethod(delegateType, "<unknown-name>", isStatic: false)}.");
            return null;
        }
        
        ParameterInfo[] p = method.GetParameters();
        Type instance = method.DeclaringType;

        MethodInfo invokeMethod = delegateType.GetMethod("Invoke", BindingFlags.Instance | BindingFlags.Public)!;
        ParameterInfo[] delegateParameters = invokeMethod.GetParameters();
        Type delegateReturnType = invokeMethod.ReturnType;
        bool needsDynamicMethod = method.ShouldCallvirtRuntime() || method.ReturnType != typeof(void) && delegateReturnType == typeof(void);
        bool isInstanceForValueType = method is { DeclaringType.IsValueType: true };
        if (p.Length != delegateParameters.Length - 1)
        {
            if (throwOnError)
                throw new Exception("Unable to create instance caller for " + (method.DeclaringType?.Name ?? "<unknown-type>") + "." + (method.Name ?? "<unknown-name>") + $": incompatable delegate type: {delegateType.Name}.");

            Logger.LogError($"Unable to create instance caller for {method.Format()}: incompatable delegate type: {delegateType.Format()}.");
            return null;
        }

        if (needsDynamicMethod && method.DeclaringType.IsInterface && !delegateParameters[0].ParameterType.IsInterface)
            needsDynamicMethod = false;

        // exact match
        if (!isInstanceForValueType && !needsDynamicMethod && delegateReturnType == method.ReturnType && instance.IsAssignableFrom(delegateParameters[0].ParameterType))
        {
            bool mismatch = false;
            for (int i = 1; i < delegateParameters.Length; ++i)
            {
                if (delegateParameters[i].ParameterType != p[i - 1].ParameterType)
                {
                    mismatch = true;
                    break;
                }
            }
            if (!mismatch)
            {
                try
                {
                    Delegate basicDelegate = method.CreateDelegate(delegateType);
                    Logger.LogDebug($"Created instance delegate caller for {method.Format()} of type {delegateType.Format()}.");
                    return basicDelegate;
                }
                catch
                {
                    // ignored
                }
            }
        }

        if (isInstanceForValueType && delegateParameters[0].ParameterType != typeof(object) && !Attribute.IsDefined(method, typeof(IsReadOnlyAttribute)))
        {
            if (throwOnError)
                throw new Exception($"Unable to create instance caller for {method.DeclaringType?.Name ?? "<unknown-type>"}.{method.Name ?? "<unknown-name>"} (non-readonly), you must pass structs ({instance.FullName}) as a boxed object (in {delegateType.FullName}).");

            Logger.LogError($"Unable to create instance caller for {method.Format()} (non-readonly), you must pass structs ({instance.Format()}) as a boxed object (in {delegateType.Format()}).");
            return null;
        }

        // rough match, can unsafely cast to actual function arguments.
        if (allowUnsafeTypeBinding && !isInstanceForValueType && !needsDynamicMethod && !instance.IsValueType && delegateParameters[0].ParameterType.IsAssignableFrom(instance) && (method.ReturnType == typeof(void) && delegateReturnType == typeof(void) || !method.ReturnType.IsValueType && delegateReturnType.IsAssignableFrom(method.ReturnType)))
        {
            bool foundIncompatibleConversion = false;
            for (int i = 0; i < p.Length; ++i)
            {
                if (p[i].ParameterType.IsValueType && delegateParameters[i + 1].ParameterType != p[i].ParameterType)
                {
                    foundIncompatibleConversion = true;
                    break;
                }
            }
            if (!foundIncompatibleConversion)
            {
                try
                {
                    IntPtr ptr = method.MethodHandle.GetFunctionPointer();
                    // running the debugger here will crash the program so... don't.
                    object d2 = FormatterServices.GetUninitializedObject(delegateType);
                    delegateType.GetConstructors()[0].Invoke(d2, new object[] { null!, ptr });
                    Logger.LogDebug($"Created instance delegate caller for {method.Format()} (using unsafe type binding) of type {delegateType.Format()}.");
                    return (Delegate)d2;
                }
                catch
                {
                    // ignored
                }
            }
        }

        // generate dynamic method as a worst-case scenerio
        Type[] parameterTypes = new Type[delegateParameters.Length];
        for (int i = 0; i < delegateParameters.Length; ++i)
            parameterTypes[i] = delegateParameters[i].ParameterType;

        GetDynamicMethodFlags(false, out MethodAttributes attributes, out CallingConventions convention);
        DynamicMethod dynMethod = new DynamicMethod("Invoke" + method.Name, attributes, convention, delegateReturnType, parameterTypes, instance.IsInterface ? typeof(Accessor) : instance, true);
        dynMethod.DefineParameter(1, ParameterAttributes.None, "this");

        for (int i = 0; i < p.Length; ++i)
            dynMethod.DefineParameter(i + 2, p[i].Attributes, p[i].Name);

        ILGenerator generator = dynMethod.GetILGenerator();

        if (instance.IsValueType)
        {
            if (!delegateParameters[0].ParameterType.IsValueType)
            {
                generator.Emit(OpCodes.Ldarg_0);
                generator.Emit(OpCodes.Unbox, instance);
            }
            else generator.Emit(OpCodes.Ldarga_S, 0);
        }
        else generator.Emit(OpCodes.Ldarg_0);

        for (int i = 0; i < p.Length; ++i)
            generator.LoadParameter(i + 1, false, type: parameterTypes[i + 1], p[i].ParameterType);

        generator.Emit(method.GetCallRuntime(), method);
        if (method.ReturnType != typeof(void) && delegateReturnType == typeof(void))
            generator.Emit(OpCodes.Pop);
        else if (method.ReturnType != typeof(void))
        {
            if (method.ReturnType.IsValueType && !delegateReturnType.IsValueType)
                generator.Emit(OpCodes.Box, method.ReturnType);
            else if (!method.ReturnType.IsValueType && delegateReturnType.IsValueType)
                generator.Emit(OpCodes.Unbox_Any, delegateReturnType);
        }
        else if (delegateReturnType != typeof(void))
        {
            if (!delegateReturnType.IsValueType)
                generator.Emit(OpCodes.Ldnull);
            else
            {
                generator.DeclareLocal(delegateReturnType);
                generator.Emit(OpCodes.Ldloca_S, 0);
                generator.Emit(OpCodes.Initobj, delegateReturnType);
                generator.Emit(OpCodes.Ldloc_0);
            }
        }
        generator.Emit(OpCodes.Ret);

        try
        {
            Delegate dynamicDelegate = dynMethod.CreateDelegate(delegateType);
            Logger.LogDebug($"Created dynamic instance caller for {method.Format()} of type {delegateType.Format()}.");
            return dynamicDelegate;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Unable to create instance caller for {method.Format()}.");
            if (throwOnError)
                throw;

            Logger.LogError(ex);
            return null;
        }
    }
    /// <summary>
    /// Generates a delegate or dynamic method that calls a static method.
    /// </summary>
    /// <param name="allowUnsafeTypeBinding">Enables unsafe type binding to non-matching delegates, meaning classes of different
    /// types can be passed as parameters and an exception will not be thrown (may cause unintended behavior if the wrong type is passed).</param>
    /// <returns>A delegate of type <see cref="Action"/> or <see cref="Func{T}"/> (or one of their generic counterparts), depending on the method signature.</returns>
    [Pure]
    public static Delegate? GenerateStaticCaller<TDeclaringType>(string methodName, Type[]? parameters = null, bool throwOnError = false, bool allowUnsafeTypeBinding = false)
    {
        MethodInfo? method = null;
        try
        {
            method = typeof(TDeclaringType).GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        }
        catch (AmbiguousMatchException)
        {
            // ignored
        }
        if (method == null)
        {
            if (parameters != null)
            {
                method = typeof(TDeclaringType).GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
                    null, CallingConventions.Any, parameters, null);
            }
            if (method == null)
            {
                if (throwOnError)
                    throw new Exception($"Unable to find matching static method: {typeof(TDeclaringType).FullName}.{methodName}.");

                Logger.LogError($"Unable to find matching static method: {FormattingUtil.FormatMethod(null, typeof(TDeclaringType), methodName, null, parameters, null, isStatic: true)}.");
                return null;
            }
        }

        return GenerateStaticCaller(method, throwOnError, allowUnsafeTypeBinding);
    }
    /// <summary>
    /// Generates a delegate or dynamic method that calls a static method.
    /// </summary>
    /// <param name="allowUnsafeTypeBinding">Enables unsafe type binding to non-matching delegates, meaning classes of different
    /// types can be passed as parameters and an exception will not be thrown (may cause unintended behavior if the wrong type is passed).</param>
    [Pure]
    public static TDelegate? GenerateStaticCaller<TDeclaringType, TDelegate>(string methodName, bool throwOnError = false, bool allowUnsafeTypeBinding = false, Type[]? parameterOverride = null) where TDelegate : Delegate
    {
        MethodInfo? method = null;
        try
        {
            method = typeof(TDeclaringType).GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        }
        catch (AmbiguousMatchException)
        {
            // ignored
        }
        if (method == null)
        {
            if (parameterOverride == null)
            {
                ParameterInfo[] parameters = GetParameters<TDelegate>();
                parameterOverride = new Type[parameters.Length];
                for (int i = 0; i < parameters.Length; ++i)
                    parameterOverride[i] = parameters[i].ParameterType;
            }
            method = typeof(TDeclaringType).GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
                    null, CallingConventions.Any, parameterOverride, null);
            if (method == null)
            {
                if (throwOnError)
                    throw new Exception($"Unable to find matching static method: {typeof(TDeclaringType).FullName}.{methodName}.");

                Logger.LogError($"Unable to find matching static method: {FormattingUtil.FormatMethod<TDelegate>(methodName, declTypeOverride: typeof(TDeclaringType), isStatic: true)}.");
                return null;
            }
        }

        return GenerateStaticCaller<TDelegate>(method, throwOnError, allowUnsafeTypeBinding);
    }
    /// <summary>
    /// Generates a delegate or dynamic method that calls a static method.
    /// </summary>
    /// <param name="allowUnsafeTypeBinding">Enables unsafe type binding to non-matching delegates, meaning classes of different
    /// types can be passed as parameters and an exception will not be thrown (may cause unintended behavior if the wrong type is passed).</param>
    /// <returns>A delegate of type <see cref="Action"/> or <see cref="Func{T}"/> (or one of their generic counterparts), depending on the method signature.</returns>
    [Pure]
    public static Delegate? GenerateStaticCaller(MethodInfo method, bool throwOnError = false, bool allowUnsafeTypeBinding = false)
    {
        if (method == null || !method.IsStatic)
        {
            if (throwOnError)
                throw new Exception("Unable to find static method.");
            Logger.LogError("Unable to find static method.");
            return null;
        }

        CheckFuncArrays();

        bool rtn = method.ReturnType != typeof(void);
        ParameterInfo[] p = method.GetParameters();
        int maxArgs = rtn ? FuncTypes!.Length : ActionTypes!.Length;
        if (p.Length > maxArgs)
        {
            if (throwOnError)
                throw new ArgumentException("Method can not have more than " + maxArgs + " arguments!", nameof(method));

            Logger.LogError("Method " + method.Format() + " can not have more than " + maxArgs.Format() + " arguments!");
            return null;
        }

        Type deleType = GetDefaultDelegate(method.ReturnType, p, null)!;
        return GenerateStaticCaller(deleType, method, throwOnError, allowUnsafeTypeBinding);
    }
    /// <summary>
    /// Generates a delegate or dynamic method that calls a static method.
    /// </summary>
    /// <param name="allowUnsafeTypeBinding">Enables unsafe type binding to non-matching delegates, meaning classes of different
    /// types can be passed as parameters and an exception will not be thrown (may cause unintended behavior if the wrong type is passed).</param>
    [Pure]
    public static TDelegate? GenerateStaticCaller<TDelegate>(MethodInfo info, bool throwOnError = false, bool allowUnsafeTypeBinding = false) where TDelegate : Delegate
    {
        return (TDelegate?)GenerateStaticCaller(typeof(TDelegate), info, throwOnError, allowUnsafeTypeBinding);
    }
    /// <summary>
    /// Generates a delegate or dynamic method that calls a static method.
    /// </summary>
    /// <param name="delegateType">Type of delegate to return.</param>
    /// <param name="allowUnsafeTypeBinding">Enables unsafe type binding to non-matching delegates, meaning classes of different
    /// types can be passed as parameters and an exception will not be thrown (may cause unintended behavior if the wrong type is passed).</param>
    [Pure]
    public static Delegate? GenerateStaticCaller(Type delegateType, MethodInfo method, bool throwOnError = false, bool allowUnsafeTypeBinding = false)
    {
        if (!typeof(Delegate).IsAssignableFrom(delegateType))
            throw new ArgumentException(delegateType.FullName + " is not a delegate.", nameof(delegateType));

        if (method == null || !method.IsStatic)
        {
            if (throwOnError)
                throw new Exception($"Unable to find static method for delegate: {delegateType.Name}.");
            Logger.LogError($"Unable to find matching static method: {FormattingUtil.FormatMethod(delegateType, "<unknown-name>", isStatic: true)}.");
            return null;
        }

        ParameterInfo[] p = method.GetParameters();

        MethodInfo invokeMethod = delegateType.GetMethod("Invoke", BindingFlags.Instance | BindingFlags.Public)!;
        ParameterInfo[] delegateParameters = invokeMethod.GetParameters();
        Type delegateReturnType = invokeMethod.ReturnType;
        bool needsDynamicMethod = method.ReturnType != typeof(void) && delegateReturnType == typeof(void);
        if (p.Length != delegateParameters.Length)
        {
            if (throwOnError)
                throw new Exception("Unable to create static caller for " + (method.DeclaringType?.Name ?? "<unknown-type>") + "." + (method.Name ?? "<unknown-name>") + $": incompatable delegate type: {delegateType.Name}.");
            
            Logger.LogError($"Unable to create static caller for {method.Format()}: incompatable delegate type: {delegateType.Format()}.");
            return null;
        }

        // exact match
        if (!needsDynamicMethod && delegateReturnType == method.ReturnType)
        {
            bool mismatch = false;
            for (int i = 0; i < delegateParameters.Length; ++i)
            {
                if (delegateParameters[i].ParameterType != p[i].ParameterType)
                {
                    mismatch = true;
                    break;
                }
            }
            if (!mismatch)
            {
                try
                {
                    Delegate basicDelegate = method.CreateDelegate(delegateType);
                    Logger.LogDebug($"Created static delegate caller for {method.Format()} of type {delegateType.Format()}.");
                    return basicDelegate;
                }
                catch
                {
                    // ignored
                }
            }
        }

        // rough match, can unsafely cast to actual function arguments.
        if (!needsDynamicMethod && allowUnsafeTypeBinding && (method.ReturnType == typeof(void) && delegateReturnType == typeof(void) || !method.ReturnType.IsValueType && delegateReturnType.IsAssignableFrom(method.ReturnType)))
        {
            bool foundIncompatibleConversion = false;
            for (int i = 0; i < p.Length; ++i)
            {
                if (p[i].ParameterType.IsValueType && delegateParameters[i].ParameterType != p[i].ParameterType)
                {
                    foundIncompatibleConversion = true;
                    break;
                }
            }
            if (!foundIncompatibleConversion)
            {
                try
                {
                    IntPtr ptr = method.MethodHandle.GetFunctionPointer();
                    // running the debugger here will crash the program so... don't.
                    object d2 = FormatterServices.GetUninitializedObject(delegateType);
                    delegateType.GetConstructors()[0].Invoke(d2, new object[] { null!, ptr });
                    Logger.LogDebug($"Created static delegate caller for {method.Format()} (using unsafe type binding) of type {delegateType.Format()}.");
                    return (Delegate)d2;
                }
                catch
                {
                    // ignored
                }
            }
        }

        // generate dynamic method as a worst-case scenerio
        Type[] parameterTypes = new Type[delegateParameters.Length];
        for (int i = 0; i < delegateParameters.Length; ++i)
            parameterTypes[i] = delegateParameters[i].ParameterType;

        GetDynamicMethodFlags(true, out MethodAttributes attributes, out CallingConventions convention);
        DynamicMethod dynMethod = new DynamicMethod("Invoke" + method.Name, attributes, convention, delegateReturnType, parameterTypes, method.DeclaringType is not { IsInterface: false } ? typeof(Accessor) : method.DeclaringType, true);

        for (int i = 0; i < p.Length; ++i)
            dynMethod.DefineParameter(i + 1, p[i].Attributes, p[i].Name);

        ILGenerator generator = dynMethod.GetILGenerator();

        for (int i = 0; i < p.Length; ++i)
            generator.LoadParameter(i, false, type: parameterTypes[i], p[i].ParameterType);

        generator.Emit(OpCodes.Call, method);
        if (method.ReturnType != typeof(void) && delegateReturnType == typeof(void))
            generator.Emit(OpCodes.Pop);
        else if (method.ReturnType != typeof(void))
        {
            if (method.ReturnType.IsValueType && !delegateReturnType.IsValueType)
                generator.Emit(OpCodes.Box, method.ReturnType);
            else if (!method.ReturnType.IsValueType && delegateReturnType.IsValueType)
                generator.Emit(OpCodes.Unbox_Any, delegateReturnType);
        }
        else if (delegateReturnType != typeof(void))
        {
            if (!delegateReturnType.IsValueType)
                generator.Emit(OpCodes.Ldnull);
            else
            {
                generator.DeclareLocal(delegateReturnType);
                generator.Emit(OpCodes.Ldloca_S, 0);
                generator.Emit(OpCodes.Initobj, delegateReturnType);
                generator.Emit(OpCodes.Ldloc_0);
            }
        }

        generator.Emit(OpCodes.Ret);

        try
        {
            Delegate dynamicDelegate = dynMethod.CreateDelegate(delegateType);
            Logger.LogDebug($"Created dynamic static caller for {method.Format()} of type {delegateType.Format()}.");
            return dynamicDelegate;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Unable to create static caller for {method.Format()}.");
            if (throwOnError)
                throw;
            
            Logger.LogError(ex);
            return null;
        }
    }
    /// <summary>
    /// Gets platform-specific flags for creating dynamic methods.
    /// </summary>
    /// <param name="static">Whether or not the method has no 'instance', only considered when on mono.</param>
    public static void GetDynamicMethodFlags(bool @static, out MethodAttributes attributes, out CallingConventions convention)
    {
        // mono has less restrictions on dynamic method attributes and conventions
        if (DevkitServerModule.MonoLoaded)
        {
            attributes = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig | (@static ? MethodAttributes.Static : 0);
            convention = !@static ? CallingConventions.HasThis : CallingConventions.Standard;
        }
        else
        {
            attributes = MethodAttributes.Public | MethodAttributes.Static;
            convention = CallingConventions.Standard;
        }
    }
    [Pure]
    internal static IEnumerable<CodeInstruction> AddIsEditorCall(IEnumerable<CodeInstruction> instructions, MethodBase __method)
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
    /// <summary>
    /// Transpiles a method to add logging for each instruction.
    /// </summary>
    public static bool AddFunctionStepthrough(MethodBase method)
    {
        if (method == null)
        {
            Logger.LogError("Error adding function stepthrough to method, not found.");
            return false;
        }
        try
        {
            PatchesMain.Patcher.Patch(method,
                transpiler: new HarmonyMethod(typeof(Accessor).GetMethod(nameof(AddFunctionStepthroughTranspiler),
                    BindingFlags.NonPublic | BindingFlags.Static)));
            Logger.LogInfo($"Added stepthrough to: {method.Format()}.");
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error adding function stepthrough to {method.Format()}.");
            Logger.LogError(ex);
            return false;
        }
    }
    /// <summary>
    /// Transpiles a method to add logging for entry and exit of the method.
    /// </summary>
    public static bool AddFunctionIOLogging(MethodBase method)
    {
        if (method == null)
        {
            Logger.LogError("Error adding function IO logging to method, not found.");
            return false;
        }
        try
        {
            PatchesMain.Patcher.Patch(method,
                transpiler: new HarmonyMethod(typeof(Accessor).GetMethod(nameof(AddFunctionIOTranspiler),
                    BindingFlags.NonPublic | BindingFlags.Static)));
            Logger.LogInfo($"Added function IO logging to: {method.Format()}.");
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error adding function IO logging to {method.Format()}.");
            Logger.LogError(ex);
            return false;
        }
    }

    private static IEnumerable<CodeInstruction> AddFunctionIOTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase method)
    {
        yield return new CodeInstruction(OpCodes.Ldstr, "In method: " + method.Format() + " (basic entry)");
        yield return new CodeInstruction(OpCodes.Ldc_I4, (int)ConsoleColor.Green);
        yield return new CodeInstruction(OpCodes.Call, LogDebug);

        foreach (CodeInstruction instr in instructions)
        {
            if (instr.opcode == OpCodes.Ret || instr.opcode == OpCodes.Throw)
            {
                CodeInstruction logInstr = new CodeInstruction(OpCodes.Ldstr, "Out method: " + method.Format() + (instr.opcode == OpCodes.Ret ? " (returned)" : " (exception)"));
                PatchUtil.TransferStartingInstructionNeeds(instr, logInstr);
                yield return logInstr;
                yield return new CodeInstruction(OpCodes.Ldc_I4, (int)ConsoleColor.Green);
                yield return new CodeInstruction(OpCodes.Call, LogDebug);
            }
            yield return instr;
        }
    }
    private static IEnumerable<CodeInstruction> AddFunctionStepthroughTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase method)
    {
        List<CodeInstruction> ins = new List<CodeInstruction>(instructions);
        AddFunctionStepthrough(ins, method);
        return ins;
    }
    internal static void AddFunctionStepthrough(List<CodeInstruction> ins, MethodBase method)
    {
        ins.Add(new CodeInstruction(OpCodes.Ldstr, "Stepping through Method: " + method.Format() + ":"));
        ins.Add(new CodeInstruction(OpCodes.Ldc_I4, (int)ConsoleColor.Green));
        ins.Add(new CodeInstruction(OpCodes.Call, LogDebug));
        for (int i = 0; i < ins.Count; i++)
        {
            CodeInstruction instr = ins[i];
            CodeInstruction? start = null;
            foreach (ExceptionBlock block in instr.blocks)
            {
                CodeInstruction blockInst = new CodeInstruction(OpCodes.Ldstr, "  " + block.Format());
                start ??= blockInst;
                ins.Insert(i, blockInst);
                ins.Insert(i + 1, new CodeInstruction(OpCodes.Ldc_I4, (int)ConsoleColor.Green));
                ins.Insert(i + 2, new CodeInstruction(OpCodes.Call, LogDebug));
                i += 3;
            }

            foreach (Label label in instr.labels)
            {
                CodeInstruction lblInst = new CodeInstruction(OpCodes.Ldstr, "  " + label.Format() + ":");
                start ??= lblInst;
                ins.Insert(i, lblInst);
                ins.Insert(i + 1, new CodeInstruction(OpCodes.Ldc_I4, (int)ConsoleColor.Green));
                ins.Insert(i + 2, new CodeInstruction(OpCodes.Call, LogDebug));
                i += 3;
            }

            CodeInstruction mainInst = new CodeInstruction(OpCodes.Ldstr, "  " + instr.Format());
            start ??= mainInst;
            ins.Insert(i, mainInst);
            ins.Insert(i + 1, new CodeInstruction(OpCodes.Ldc_I4, (int)ConsoleColor.Green));
            ins.Insert(i + 2, new CodeInstruction(OpCodes.Call, LogDebug));
            i += 3;

            PatchUtil.TransferStartingInstructionNeeds(instr, start);
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

    /// <summary>
    /// Checks <paramref name="method"/> for the <see langword="extern"/> flag.
    /// </summary>
    [Pure]
    public static bool IsExtern(this MethodBase method) => (method.Attributes & MethodAttributes.PinvokeImpl) != 0;

    /// <summary>
    /// Checks <paramref name="field"/> for the <see langword="extern"/> flag.
    /// </summary>
    [Pure]
    public static bool IsExtern(this FieldInfo field) => field.IsPinvokeImpl;

    /// <summary>
    /// Checks <paramref name="property"/>'s getter and setter for the <see langword="extern"/> flag.
    /// </summary>
    [Pure]
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

    /// <summary>
    /// Checks for the <see cref="IgnoreAttribute"/> on <paramref name="type"/>.
    /// </summary>
    [Pure]
    public static bool IsIgnored(this Type type) => Attribute.IsDefined(type, typeof(IgnoreAttribute));

    /// <summary>
    /// Checks for the <see cref="IgnoreAttribute"/> on <paramref name="member"/>.
    /// </summary>
    [Pure]
    public static bool IsIgnored(this MemberInfo member) => Attribute.IsDefined(member, typeof(IgnoreAttribute));

    /// <summary>
    /// Checks for the <see cref="IgnoreAttribute"/> on <paramref name="assembly"/>.
    /// </summary>
    [Pure]
    public static bool IsIgnored(this Assembly assembly) => Attribute.IsDefined(assembly, typeof(IgnoreAttribute));

    /// <summary>
    /// Checks for the <see cref="IgnoreAttribute"/> on <paramref name="parameter"/>.
    /// </summary>
    [Pure]
    public static bool IsIgnored(this ParameterInfo parameter) => Attribute.IsDefined(parameter, typeof(IgnoreAttribute));

    /// <summary>
    /// Checks for the <see cref="IgnoreAttribute"/> on <paramref name="module"/>.
    /// </summary>
    [Pure]
    public static bool IsIgnored(this Module module) => Attribute.IsDefined(module, typeof(IgnoreAttribute));

    /// <summary>
    /// Checks for the <see cref="LoadPriorityAttribute"/> on <paramref name="type"/> and returns the priority (or zero if not found).
    /// </summary>
    [Pure]
    public static int GetPriority(this Type type) => Attribute.GetCustomAttribute(type, typeof(LoadPriorityAttribute)) is LoadPriorityAttribute attr ? attr.Priority : 0;

    /// <summary>
    /// Checks for the <see cref="LoadPriorityAttribute"/> on <paramref name="member"/> and returns the priority (or zero if not found).
    /// </summary>
    [Pure]
    public static int GetPriority(this MemberInfo member) => Attribute.GetCustomAttribute(member, typeof(LoadPriorityAttribute)) is LoadPriorityAttribute attr ? attr.Priority : 0;

    /// <summary>
    /// Checks for the <see cref="LoadPriorityAttribute"/> on <paramref name="assembly"/> and returns the priority (or zero if not found).
    /// </summary>
    [Pure]
    public static int GetPriority(this Assembly assembly) => Attribute.GetCustomAttribute(assembly, typeof(LoadPriorityAttribute)) is LoadPriorityAttribute attr ? attr.Priority : 0;

    /// <summary>
    /// Checks for the <see cref="LoadPriorityAttribute"/> on <paramref name="parameter"/> and returns the priority (or zero if not found).
    /// </summary>
    [Pure]
    public static int GetPriority(this ParameterInfo parameter) => Attribute.GetCustomAttribute(parameter, typeof(LoadPriorityAttribute)) is LoadPriorityAttribute attr ? attr.Priority : 0;

    /// <summary>
    /// Checks for the <see cref="LoadPriorityAttribute"/> on <paramref name="module"/> and returns the priority (or zero if not found).
    /// </summary>
    [Pure]
    public static int GetPriority(this Module module) => Attribute.GetCustomAttribute(module, typeof(LoadPriorityAttribute)) is LoadPriorityAttribute attr ? attr.Priority : 0;

    /// <summary>
    /// Created for <see cref="List{T}.Sort(Comparison{T})"/> to order by priority (highest to lowest).
    /// </summary>
    [Pure]
    public static int SortTypesByPriorityHandler(Type a, Type b)
    {
        return b.GetPriority().CompareTo(a.GetPriority());
    }

    /// <summary>
    /// Created for <see cref="List{T}.Sort(Comparison{T})"/> to order by priority (highest to lowest).
    /// </summary>
    [Pure]
    public static int SortMembersByPriorityHandler(MemberInfo a, MemberInfo b)
    {
        return b.GetPriority().CompareTo(a.GetPriority());
    }

    /// <summary>
    /// Safely gets the reflection method info of the passed method. Works best with static methods.<br/><br/>
    /// <code>
    /// MethodInfo? method = Accessor.GetMethod(Guid.Parse);
    /// </code>
    /// </summary>
    /// <returns>A method info of a passed delegate</returns>
    [Pure]
    public static MethodInfo? GetMethod(Delegate @delegate)
    {
        try
        {
            return @delegate.Method;
        }
        catch (MemberAccessException)
        {
            return null;
        }
    }

    /// <param name="returnType">Return type of the method.</param>
    /// <param name="parameters">Method parameters, not including the instance.</param>
    /// <param name="instanceType">The declaring type, or <see langword="null"/> for static methods.</param>
    /// <remarks>The first argument will be the instance.</remarks>
    /// <returns>A delegate of type <see cref="Action"/> or <see cref="Func{T}"/> (or one of their generic counterparts), depending on the method signature, or <see langword="null"/> if there are too many parameters.</returns>
    [Pure]
    public static Type? GetDefaultDelegate(Type returnType, IReadOnlyList<ParameterInfo> parameters, Type? instanceType)
    {
        CheckFuncArrays();
        
        if (instanceType == null)
        {
            if (returnType != typeof(void))
            {
                if (FuncTypes!.Length <= parameters.Count)
                    return null;
                Type[] p2 = new Type[parameters.Count + 1];
                for (int i = 1; i < p2.Length - 1; ++i)
                    p2[i] = parameters[i].ParameterType;
                p2[p2.Length - 1] = returnType;
                return FuncTypes[parameters.Count].MakeGenericType(p2);
            }
            else
            {
                if (ActionTypes!.Length <= parameters.Count)
                    return null;
                Type[] p2 = new Type[parameters.Count];
                for (int i = 1; i < p2.Length; ++i)
                    p2[i] = parameters[i].ParameterType;
                return ActionTypes[parameters.Count].MakeGenericType(p2);
            }
        }
        else
        {
            if (returnType != typeof(void))
            {
                if (FuncTypes!.Length <= parameters.Count)
                    return null;
                Type[] p2 = new Type[parameters.Count + 2];
                p2[0] = instanceType;
                for (int i = 1; i < p2.Length - 1; ++i)
                    p2[i] = parameters[i - 1].ParameterType;
                p2[p2.Length - 1] = returnType;
                return FuncTypes[parameters.Count].MakeGenericType(p2);
            }
            else
            {
                if (ActionTypes!.Length <= parameters.Count)
                    return null;
                Type[] p2 = new Type[parameters.Count + 1];
                p2[0] = instanceType;
                for (int i = 1; i < p2.Length; ++i)
                    p2[i] = parameters[i - 1].ParameterType;
                return ActionTypes[parameters.Count].MakeGenericType(p2);
            }
        }
    }

    /// <summary>
    /// Used to perform a repeated <paramref name="action"/> for each base type of a <paramref name="type"/>.
    /// </summary>
    /// <param name="action">Called optionally for <paramref name="type"/>, then for each base type in order from most related to least related.</param>
    /// <param name="includeParent">Call <paramref name="action"/> on <paramref name="type"/>. Overrides <paramref name="excludeSystemBase"/>.</param>
    /// <param name="excludeSystemBase">Excludes calling <paramref name="action"/> for <see cref="object"/> or <see cref="ValueType"/>.</param>
    public static void ForEachBaseType(this Type type, ForEachBaseType action, bool includeParent = true, bool excludeSystemBase = true)
    {
        Type? type2 = type;
        if (includeParent)
        {
            action(type2, 0);
        }

        type2 = type.BaseType;

        int level = 0;
        for (; type2 != null && (!excludeSystemBase || type2 != typeof(object) && type != typeof(ValueType)); type2 = type.BaseType)
        {
            ++level;
            action(type2, level);
        }
    }

    /// <summary>
    /// Used to perform a repeated <paramref name="action"/> for each base type of a <paramref name="type"/>.
    /// </summary>
    /// <remarks>Execution can be broken by returning <see langword="false"/>.</remarks>
    /// <param name="action">Called optionally for <paramref name="type"/>, then for each base type in order from most related to least related.</param>
    /// <param name="includeParent">Call <paramref name="action"/> on <paramref name="type"/>. Overrides <paramref name="excludeSystemBase"/>.</param>
    /// <param name="excludeSystemBase">Excludes calling <paramref name="action"/> for <see cref="object"/> or <see cref="ValueType"/>.</param>
    public static void ForEachBaseType(this Type type, ForEachBaseTypeWhile action, bool includeParent = true, bool excludeSystemBase = true)
    {
        Type? type2 = type;
        if (includeParent)
        {
            if (!action(type2, 0))
                return;
        }

        type2 = type.BaseType;

        int level = 0;
        for (; type2 != null && (!excludeSystemBase || type2 != typeof(object) && type != typeof(ValueType)); type2 = type.BaseType)
        {
            ++level;
            if (!action(type2, level))
                return;
        }
    }

    /// <returns>Every type defined in the calling assembly.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    [Pure]
    public static List<Type> GetTypesSafe(bool removeIgnored = false) => GetTypesSafe(Assembly.GetCallingAssembly(), removeIgnored);

    /// <returns>Every type defined in <paramref name="assembly"/>.</returns>
    [Pure]
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
    [Pure]
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
            types.RemoveAll(x => x == null || Attribute.IsDefined(x, typeof(IgnoreAttribute)));
        else
            types.RemoveAll(x => x == null);
        types.Sort(SortTypesByPriorityHandler);
        return types;
    }

    /// <summary>
    /// Takes a method declared in an interface and returns an implementation on <paramref name="type"/>. Useful for getting explicit implementations.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="interfaceMethod"/> is not defined in an interface or <paramref name="type"/> does not implement the interface it's defined in.</exception>
    [Pure]
    public static MethodInfo? GetImplementedMethod(Type type, MethodInfo interfaceMethod)
    {
        if (interfaceMethod is not { DeclaringType.IsInterface: true })
            throw new ArgumentException("Interface method is not defined within an interface.", nameof(interfaceMethod));
        if (!interfaceMethod.DeclaringType.IsAssignableFrom(type))
            throw new ArgumentException("Type does not implement the interface this interface method is defined in.", nameof(interfaceMethod));

        InterfaceMapping mapping = type.GetInterfaceMap(interfaceMethod.DeclaringType!);
        for (int i = 0; i < mapping.InterfaceMethods.Length; ++i)
        {
            MethodInfo explictlyImplementedMethod = mapping.InterfaceMethods[i];
            if (explictlyImplementedMethod.Equals(interfaceMethod))
            {
                return mapping.TargetMethods[i];
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the (cached) <paramref name="returnType"/> and <paramref name="parameters"/> of a <typeparamref name="TDelegate"/> delegate type.
    /// </summary>
    public static void GetDelegateSignature<TDelegate>(out Type returnType, out ParameterInfo[] parameters) where TDelegate : Delegate
    {
        returnType = DelegateInfo<TDelegate>.ReturnType;
        parameters = DelegateInfo<TDelegate>.Parameters;
    }

    /// <summary>
    /// Gets the (cached) return type of a <typeparamref name="TDelegate"/> delegate type.
    /// </summary>
    [Pure]
    public static Type GetReturnType<TDelegate>() where TDelegate : Delegate => DelegateInfo<TDelegate>.ReturnType;

    /// <summary>
    /// Gets the (cached) parameters of a <typeparamref name="TDelegate"/> delegate type.
    /// </summary>
    [Pure]
    public static ParameterInfo[] GetParameters<TDelegate>() where TDelegate : Delegate => DelegateInfo<TDelegate>.Parameters;

    /// <summary>
    /// Gets the (cached) <see langword="Invoke"/> method of a <typeparamref name="TDelegate"/> delegate type. All delegates have one by default.
    /// </summary>
    [Pure]
    public static MethodInfo GetInvokeMethod<TDelegate>() where TDelegate : Delegate => DelegateInfo<TDelegate>.InvokeMethod;

    /// <summary>
    /// Gets the <paramref name="returnType"/> and <paramref name="parameters"/> of a <paramref name="delegateType"/>.
    /// </summary>
    public static void GetDelegateSignature(Type delegateType, out Type returnType, out ParameterInfo[] parameters)
    {
        MethodInfo invokeMethod = GetInvokeMethod(delegateType);
        returnType = invokeMethod.ReturnType;
        parameters = invokeMethod.GetParameters();
    }

    /// <summary>
    /// Gets the return type of a <paramref name="delegateType"/>.
    /// </summary>
    [Pure]
    public static Type GetReturnType(Type delegateType)
    {
        if (!typeof(Delegate).IsAssignableFrom(delegateType))
            throw new ArgumentException(delegateType.Name + " is not a delegate type.", nameof(delegateType));

        return delegateType.GetMethod("Invoke", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.ReturnType;
    }

    /// <summary>
    /// Gets the parameters of a <paramref name="delegateType"/>.
    /// </summary>
    [Pure]
    public static ParameterInfo[] GetParameters(Type delegateType)
    {
        if (!typeof(Delegate).IsAssignableFrom(delegateType))
            throw new ArgumentException(delegateType.Name + " is not a delegate type.", nameof(delegateType));

        return delegateType.GetMethod("Invoke", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetParameters();
    }

    /// <summary>
    /// Gets the (cached) <see langword="Invoke"/> method of a <paramref name="delegateType"/>. All delegates have one by default.
    /// </summary>
    [Pure]
    public static MethodInfo GetInvokeMethod(Type delegateType)
    {
        if (!typeof(Delegate).IsAssignableFrom(delegateType))
            throw new ArgumentException(delegateType.Name + " is not a delegate type.", nameof(delegateType));

        return delegateType.GetMethod("Invoke", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
    }
    private static class DelegateInfo<TDelegate> where TDelegate : Delegate
    {
        public static MethodInfo InvokeMethod { get; }
        public static ParameterInfo[] Parameters { get; }
        public static Type ReturnType { get; }
        static DelegateInfo()
        {
            InvokeMethod = typeof(TDelegate).GetMethod("Invoke", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
            Parameters = InvokeMethod.GetParameters();
            ReturnType = InvokeMethod.ReturnType;
        }
    }
    private static void CheckExceptionConstructors()
    {
        if (!_castExCtorCalc)
        {
            _castExCtorCalc = true;
            _castExCtor = typeof(InvalidCastException).GetConstructor(BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(string) }, null)!;
        }
        if (!_nreExCtorCalc)
        {
            _nreExCtorCalc = true;
            _nreExCtor = typeof(NullReferenceException).GetConstructor(BindingFlags.Public | BindingFlags.Instance, null, Array.Empty<Type>(), null)!;
        }
    }

    /// <summary>
    /// Unturned primary assembly.
    /// </summary>
    /// <remarks>Lazily cached.</remarks>
    /// <exception cref="TypeLoadException"/>
    public static Assembly AssemblyCSharp => _sdgAssembly ??= typeof(Provider).Assembly;

    /// <summary>
    /// DevkitServer primary assembly.
    /// </summary>
    /// <remarks>Lazily cached.</remarks>
    /// <exception cref="TypeLoadException"/>
    public static Assembly DevkitServer => _devkitServerAssembly ??= Assembly.GetExecutingAssembly();

    /// <summary>
    /// System primary assembly.
    /// </summary>
    /// <remarks>Lazily cached.</remarks>
    /// <exception cref="TypeLoadException"/>
    public static Assembly MSCoreLib => _mscorlibAssembly ??= typeof(object).Assembly;

    /// <summary><see cref="Provider.isServer"/>.</summary>
    /// <remarks>Lazily cached.</remarks>
    /// <exception cref="MemberAccessException"/>
    public static MethodInfo IsServerGetter => _getIsServer ??=
        typeof(Provider).GetProperty(nameof(Provider.isServer), BindingFlags.Static | BindingFlags.Public)
            ?.GetMethod ?? throw new MemberAccessException("Unable to find Provider.isServer.");

    /// <summary><see cref="Level.isEditor"/>.</summary>
    /// <remarks>Lazily cached.</remarks>
    /// <exception cref="MemberAccessException"/>
    public static MethodInfo IsEditorGetter => _getIsEditor ??=
        typeof(Level).GetProperty(nameof(Level.isEditor), BindingFlags.Static | BindingFlags.Public)
            ?.GetMethod ?? throw new MemberAccessException("Unable to find Level.isEditor.");

    /// <summary><see cref="DevkitServerModule.IsEditing"/>.</summary>
    /// <remarks>Lazily cached.</remarks>
    /// <exception cref="MemberAccessException"/>
    public static MethodInfo IsDevkitServerGetter => _getIsDevkitServer ??=
        typeof(DevkitServerModule).GetProperty(nameof(DevkitServerModule.IsEditing), BindingFlags.Static | BindingFlags.Public)
            ?.GetMethod ?? throw new MemberAccessException("Unable to find DevkitServerModule.IsEditing.");

    /// <summary><see cref="Logger.LogDebug"/>.</summary>
    /// <remarks>Lazily cached.</remarks>
    /// <exception cref="MemberAccessException"/>
    public static MethodInfo LogDebug => _logDebug ??=
        typeof(Logger).GetMethod(nameof(Logger.LogDebug), BindingFlags.Public | BindingFlags.Static)
        ?? throw new MemberAccessException("Unable to find Logger.LogDebug.");

    /// <summary><see cref="CachedTime.RealtimeSinceStartup"/>.</summary>
    /// <remarks>Lazily cached.</remarks>
    /// <exception cref="MemberAccessException"/>
    public static MethodInfo GetRealtimeSinceStartup => _getRealtimeSinceStartup ??=
        typeof(CachedTime).GetProperty(nameof(CachedTime.RealtimeSinceStartup), BindingFlags.Static | BindingFlags.Public)
            ?.GetMethod ?? throw new MemberAccessException("Unable to find CachedTime.RealtimeSinceStartup.");

    /// <summary><see cref="Time.realtimeSinceStartupAsDouble"/>.</summary>
    /// <remarks>Lazily cached.</remarks>
    /// <exception cref="MemberAccessException"/>
    public static MethodInfo GetRealtimeSinceStartupAsDouble => _getRealtimeSinceStartupAsDouble ??=
        typeof(Time).GetProperty(nameof(Time.realtimeSinceStartupAsDouble), BindingFlags.Static | BindingFlags.Public)
            ?.GetMethod ?? throw new MemberAccessException("Unable to find Time.realtimeSinceStartupAsDouble.");

    /// <summary><see cref="Time.time"/>.</summary>
    /// <remarks>Lazily cached.</remarks>
    /// <exception cref="MemberAccessException"/>
    public static MethodInfo GetTime => _getTime ??=
        typeof(Time).GetProperty(nameof(Time.time), BindingFlags.Static | BindingFlags.Public)
            ?.GetMethod ?? throw new MemberAccessException("Unable to find Time.time.");

    /// <summary><see cref="CachedTime.DeltaTime"/>.</summary>
    /// <remarks>Lazily cached.</remarks>
    /// <exception cref="MemberAccessException"/>
    public static MethodInfo GetDeltaTime => _getDeltaTime ??=
        typeof(CachedTime).GetProperty(nameof(CachedTime.DeltaTime), BindingFlags.Static | BindingFlags.Public)
            ?.GetMethod ?? throw new MemberAccessException("Unable to find CachedTime.DeltaTime.");

    /// <summary><see cref="Time.fixedDeltaTime"/>.</summary>
    /// <remarks>Lazily cached.</remarks>
    /// <exception cref="MemberAccessException"/>
    public static MethodInfo GetFixedDeltaTime => _getFixedDeltaTime ??=
        typeof(Time).GetProperty(nameof(Time.fixedDeltaTime), BindingFlags.Static | BindingFlags.Public)
            ?.GetMethod ?? throw new MemberAccessException("Unable to find Time.fixedDeltaTime.");

    /// <summary><see cref="GameObject.transform"/>.</summary>
    /// <remarks>Lazily cached.</remarks>
    /// <exception cref="MemberAccessException"/>
    public static MethodInfo GetGameObjectTransform => _getGameObjectTransform ??=
        typeof(GameObject).GetProperty(nameof(GameObject.transform), BindingFlags.Instance | BindingFlags.Public)
            ?.GetMethod ?? throw new MemberAccessException("Unable to find GameObject.transform.");

    /// <summary><see cref="Component.transform"/>.</summary>
    /// <remarks>Lazily cached.</remarks>
    /// <exception cref="MemberAccessException"/>
    public static MethodInfo GetComponentTransform => _getComponentTransform ??=
        typeof(Component).GetProperty(nameof(Component.transform), BindingFlags.Instance | BindingFlags.Public)
            ?.GetMethod ?? throw new MemberAccessException("Unable to find Component.transform.");

    /// <summary><see cref="Component.gameObject"/>.</summary>
    /// <remarks>Lazily cached.</remarks>
    /// <exception cref="MemberAccessException"/>
    public static MethodInfo GetComponentGameObject => _getComponentGameObject ??=
        typeof(Component).GetProperty(nameof(Component.gameObject), BindingFlags.Instance | BindingFlags.Public)
            ?.GetMethod ?? throw new MemberAccessException("Unable to find Component.gameObject.");

    /// <summary><see cref="InputEx.GetKeyDown"/>.</summary>
    /// <remarks>Lazily cached.</remarks>
    /// <exception cref="MemberAccessException"/>
    public static MethodInfo GetKeyDown => _getKeyDown ??=
        typeof(InputEx).GetMethod(nameof(InputEx.GetKeyDown), BindingFlags.Public | BindingFlags.Static)
        ?? throw new MemberAccessException("Unable to find InputEx.GetKeyDown.");

    /// <summary><see cref="InputEx.GetKeyUp"/>.</summary>
    /// <remarks>Lazily cached.</remarks>
    /// <exception cref="MemberAccessException"/>
    public static MethodInfo GetKeyUp => _getKeyUp ??=
        typeof(InputEx).GetMethod(nameof(InputEx.GetKeyUp), BindingFlags.Public | BindingFlags.Static)
        ?? throw new MemberAccessException("Unable to find InputEx.GetKeyUp.");

    /// <summary><see cref="InputEx.GetKey"/>.</summary>
    /// <remarks>Lazily cached.</remarks>
    /// <exception cref="MemberAccessException"/>
    public static MethodInfo GetKey => _getKey ??=
        typeof(InputEx).GetMethod(nameof(InputEx.GetKey), BindingFlags.Public | BindingFlags.Static)
        ?? throw new MemberAccessException("Unable to find InputEx.GetKey.");

    /// <summary><see cref="StackTrace(int)"/>.</summary>
    /// <remarks>Lazily cached.</remarks>
    /// <exception cref="MemberAccessException"/>
    public static ConstructorInfo StackTraceIntConstructor => _stackTraceIntCtor ??= 
        typeof(StackTrace).GetConstructor(BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(int) },
            null) ?? throw new MemberAccessException("Unable to find StackTrace.StackTrace(int).");

    /// <summary><see cref="string.Concat(string, string)"/>.</summary>
    /// <remarks>Lazily cached.</remarks>
    /// <exception cref="MemberAccessException"/>
    public static MethodInfo Concat2StringsMethod => _concat2Strings ??=
        typeof(string).GetMethod(nameof(string.Concat), BindingFlags.Static | BindingFlags.Public, null,
            new Type[] { typeof(string), typeof(string) }, null)
        ?? throw new MemberAccessException("Unable to find string.Concat(string, string).");

    /// <summary><see cref="StackTraceCleaner.GetString(StackTrace)"/>.</summary>
    /// <remarks>Lazily cached.</remarks>
    /// <exception cref="MemberAccessException"/>
    public static MethodInfo StackTraceCleanerGetStringMethod => _getStackTraceString ??=
        typeof(StackTraceCleaner).GetMethod(nameof(StackTraceCleaner.GetString), BindingFlags.Instance | BindingFlags.Public, null,
            new Type[] { typeof(StackTrace) }, null)
        ?? throw new MemberAccessException("Unable to find StackTraceCleaner.GetString(StackTrace).");

    /// <summary><see cref="Logger.StackCleaner"/>.</summary>
    /// <remarks>Lazily cached.</remarks>
    /// <exception cref="MemberAccessException"/>
    public static FieldInfo LoggerStackCleanerField => _getStackCleaner ??=
        typeof(Logger).GetField(nameof(Logger.StackCleaner), BindingFlags.Static | BindingFlags.Public)
        ?? throw new MemberAccessException("Unable to find Logger.StackCleaner.");
}

public delegate void InstanceSetter<in TInstance, in T>(TInstance owner, T value);
public delegate T InstanceGetter<in TInstance, out T>(TInstance owner);
public delegate void StaticSetter<in T>(T value);
public delegate T StaticGetter<out T>();

/// <param name="depth">Number of types below the provided type this base type is. Will be zero if the type returned is the provided type, 1 for its base type, and so on.</param>
public delegate void ForEachBaseType(Type type, int depth);

/// <param name="depth">Number of types below the provided type this base type is. Will be zero if the type returned is the provided type, 1 for its base type, and so on.</param>
/// <returns><see langword="True"/> to continue, <see langword="false"/> to break.</returns>
public delegate bool ForEachBaseTypeWhile(Type type, int depth);

/// <summary>
/// Marks a class to have it's static constructor (type initializer) ran on load.
/// Helps ensure there are no errors hidden in your static members that will pop up later, and moves all load time to when the game/server actually loads.
/// </summary>
/// <remarks>Works in plugins as well.</remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class EarlyTypeInitAttribute : Attribute
{
    /// <summary>
    /// Defines the order in which type initializers run. Lower gets ran first. Default priority is zero.
    /// </summary>
    public int Priority { get; }

    /// <remarks>
    /// Irrelevant for plugins.
    /// </remarks>
    public bool RequiresUIAccessTools { get; set; }

    public EarlyTypeInitAttribute() : this (0) { }

    public EarlyTypeInitAttribute(int priority)
    {
        Priority = priority;
    }
}