﻿using DevkitServer.API.Abstractions;
using DevkitServer.Patches;
using HarmonyLib;
using StackCleaner;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

namespace DevkitServer.API;

/// <summary>
/// Reflection utilities for accessing private or internal members.
/// </summary>
public static class Accessor
{
    private static bool _isMonoCached;
    private static bool _isMono;

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
    private static MethodInfo? _logInfo;
    private static MethodInfo? _logWarning;
    private static MethodInfo? _logError;
    private static MethodInfo? _logFatal;
    private static MethodInfo? _logException;
    private static MethodInfo? _getKeyDown;
    private static MethodInfo? _getKeyUp;
    private static MethodInfo? _getKey;
    private static MethodInfo? _concat2Strings;
    private static MethodInfo? _getStackTraceString;

    private static FieldInfo? _getStackCleaner;

    internal static ConstructorInfo? CastExCtor;
    internal static ConstructorInfo? NreExCtor;
    private static ConstructorInfo? _stackTraceIntCtor;

    internal static Type[]? FuncTypes;
    internal static Type[]? ActionTypes;
    private static Type? _readonlyAttribute;
    private static Type? _ignoreAttribute;
    private static Type? _priorityAttribute;
    private static bool _castExCtorCalc;
    private static bool _nreExCtorCalc;
    internal static bool LogILTraceMessages { get; set; } = false;
    internal static bool LogDebugMessages { get; set; } = true;
    internal static bool LogInfoMessages { get; set; } = true;
    internal static bool LogWarningMessages { get; set; } = true;
    internal static bool LogErrorMessages { get; set; } = true;

    /// <summary>
    /// Whether or not the <c>Mono.Runtime</c> class is available. Indicates if the current runtime is Mono.
    /// </summary>
    public static bool IsMono
    {
        get
        {
            if (_isMonoCached)
                return _isMono;

            _isMono = Type.GetType("Mono.Runtime", false, false) != null;
            _isMonoCached = true;
            return _isMono;
        }
    }

    private static IOpCodeEmitter CreateEmitter(DynamicMethod method, string logSource)
    {
        IOpCodeEmitter emitter = method.GetILGenerator().AsEmitter();
        return LogILTraceMessages ? new DebuggableEmitter(emitter, method) { DebugLog = true, LogSource = logSource } : emitter;
    }

    /// <summary>
    /// Generates a dynamic method that sets an instance field value. For value types use <see cref="GenerateInstanceSetter{TValue}"/> instead.
    /// </summary>
    /// <typeparam name="TInstance">Declaring type of the field.</typeparam>
    /// <typeparam name="TValue">Field return type.</typeparam>
    /// <param name="fieldName">Name of field that will be referenced.</param>
    /// <param name="throwOnError">Throw an error instead of writing to console and returning <see langword="null"/>.</param>
    /// <remarks>Will never return <see langword="null"/> if <paramref name="throwOnError"/> is <see langword="true"/>.</remarks>
    [Pure]
    public static InstanceSetter<TInstance, TValue>? GenerateInstanceSetter<TInstance, TValue>(string fieldName, bool throwOnError = false)
    {
        const string source = "Accessor.GenerateInstanceSetter";
        if (typeof(TInstance).IsValueType)
        {
            if (throwOnError)
                throw new Exception($"Unable to create instance setter for {typeof(TInstance).FullName}.{fieldName}, you must pass structs ({typeof(TInstance).Name}) as a boxed object.");

            if (LogErrorMessages)
                Logger.DevkitServer.LogError(source, $"Unable to create instance setter for {typeof(TInstance).Format()}.{fieldName.Colorize(FormattingColorType.Property)}, you must pass structs ({typeof(TInstance).Format()}) as a boxed object.");
            return null;
        }

        FieldInfo? field = typeof(TInstance).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        field ??= typeof(TInstance).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
        if (field is null || field.IsStatic || !field.FieldType.IsAssignableFrom(typeof(TValue)) && typeof(TValue) != typeof(object))
        {
            if (throwOnError)
                throw new Exception($"Unable to find matching field: {typeof(TInstance).FullName}.{fieldName}.");
            if (LogErrorMessages)
                Logger.DevkitServer.LogError(source, $"Unable to find matching property {typeof(TInstance).Format()}.{fieldName.Colorize(FormattingColorType.Property)}.");
            return null;
        }

        fieldName = field.Name;

        try
        {
            CheckExceptionConstructors();

            GetDynamicMethodFlags(false, out MethodAttributes attr, out CallingConventions convention);
            DynamicMethod method = new DynamicMethod("set_" + fieldName, attr, convention, typeof(void), new Type[] { typeof(TInstance), typeof(TValue) }, typeof(TInstance), true);
            method.DefineParameter(1, ParameterAttributes.None, "this");
            method.DefineParameter(2, ParameterAttributes.None, "value");
            IOpCodeEmitter il = CreateEmitter(method, source);
            Label? typeLbl = null;
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);

            if (!typeof(TValue).IsValueType && field.FieldType.IsValueType)
                il.Emit(OpCodes.Unbox_Any, field.FieldType);
            else if (typeof(TValue).IsValueType && !field.FieldType.IsValueType)
                il.Emit(OpCodes.Box, typeof(TValue));
            else if (!field.FieldType.IsAssignableFrom(typeof(TValue)) && (CastExCtor != null || NreExCtor != null))
            {
                typeLbl = il.DefineLabel();
                il.Emit(OpCodes.Isinst, field.FieldType);
                il.Emit(OpCodes.Dup);
                il.Emit(OpCodes.Brtrue_S, typeLbl.Value);
                il.Emit(OpCodes.Pop);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Dup);
                il.Emit(OpCodes.Brfalse_S, typeLbl.Value);
                il.Emit(OpCodes.Pop);
                il.Emit(OpCodes.Pop);
                if (CastExCtor != null)
                    il.Emit(OpCodes.Ldstr, "Invalid argument type passed to setter for " + fieldName + ". Expected " + field.FieldType.FullName + ".");
                il.Emit(OpCodes.Newobj, CastExCtor ?? NreExCtor!);
                il.Emit(OpCodes.Throw);
            }

            if (typeLbl.HasValue)
                il.MarkLabel(typeLbl.Value);

            il.Emit(OpCodes.Stfld, field);
            il.Emit(OpCodes.Ret);
            InstanceSetter<TInstance, TValue> setter = (InstanceSetter<TInstance, TValue>)method.CreateDelegate(typeof(InstanceSetter<TInstance, TValue>));

            if (LogDebugMessages || LogILTraceMessages)
                Logger.DevkitServer.LogDebug(source, $"Created dynamic method instance setter for {field.Format()}.");

            return setter;
        }
        catch (Exception ex)
        {
            if (throwOnError)
                throw new Exception($"Error generating instance getter for {field.DeclaringType!.FullName}.{fieldName}.", ex);
            if (LogErrorMessages)
                Logger.DevkitServer.LogError(source, ex, $"Error generating instance getter for {typeof(TInstance).Format()}.{fieldName.Colorize(FormattingColorType.Property)}.");
            return null;
        }
    }

    /// <summary>
    /// Generates a dynamic method that gets an instance field value. Works for reference or value types.
    /// </summary>
    /// <typeparam name="TInstance">Declaring type of the field.</typeparam>
    /// <typeparam name="TValue">Field return type.</typeparam>
    /// <param name="fieldName">Name of field that will be referenced.</param>
    /// <param name="throwOnError">Throw an error instead of writing to console and returning <see langword="null"/>.</param>
    /// <remarks>Will never return <see langword="null"/> if <paramref name="throwOnError"/> is <see langword="true"/>.</remarks>
    [Pure]
    public static InstanceGetter<TInstance, TValue>? GenerateInstanceGetter<TInstance, TValue>(string fieldName, bool throwOnError = false)
    {
        const string source = "Accessor.GenerateInstanceGetter";
        FieldInfo? field = typeof(TInstance).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        field ??= typeof(TInstance).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
        if (field is null || field.IsStatic || !typeof(TValue).IsAssignableFrom(field.FieldType) && field.FieldType != typeof(object))
        {
            if (throwOnError)
                throw new Exception($"Unable to find matching field: {typeof(TInstance).FullName}.{fieldName}.");
            if (LogErrorMessages)
                Logger.DevkitServer.LogError(source, $"Unable to find matching property {typeof(TInstance).Format()}.{fieldName.Colorize(FormattingColorType.Property)}.");
            return null;
        }

        fieldName = field.Name;
        
        try
        {
            GetDynamicMethodFlags(false, out MethodAttributes attr, out CallingConventions convention);
            DynamicMethod method = new DynamicMethod("get_" + fieldName, attr, convention, typeof(TValue), new Type[] { typeof(TInstance) }, typeof(TInstance), true);
            method.DefineParameter(1, ParameterAttributes.None, "this");
            IOpCodeEmitter il = CreateEmitter(method, source);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, field);

            if (typeof(TValue).IsValueType && !field.FieldType.IsValueType)
                il.Emit(OpCodes.Unbox_Any, typeof(TValue));
            else if (!typeof(TValue).IsValueType && field.FieldType.IsValueType)
                il.Emit(OpCodes.Box, field.FieldType);

            il.Emit(OpCodes.Ret);

            InstanceGetter<TInstance, TValue> getter = (InstanceGetter<TInstance, TValue>)method.CreateDelegate(typeof(InstanceGetter<TInstance, TValue>));

            if (LogDebugMessages || LogILTraceMessages)
                Logger.DevkitServer.LogDebug(source, $"Created dynamic method instance getter for {field.Format()}.");

            return getter;
        }
        catch (Exception ex)
        {
            if (throwOnError)
                throw new Exception($"Error generating instance getter for {typeof(TInstance).FullName}.{fieldName}.", ex);

            if (LogErrorMessages)
            {
                Logger.DevkitServer.LogError(source, ex, $"Error generating instance getter for {typeof(TInstance).Format()}.{fieldName.Colorize(FormattingColorType.Property)}.");
            }
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
    /// <param name="declaringType">Declaring type of the field.</param>
    /// <typeparam name="TValue">Field return type.</typeparam>
    /// <param name="fieldName">Name of field that will be referenced.</param>
    /// <param name="throwOnError">Throw an error instead of writing to console and returning <see langword="null"/>.</param>
    /// <remarks>Will never return <see langword="null"/> if <paramref name="throwOnError"/> is <see langword="true"/>.</remarks>
    [Pure]
    public static InstanceSetter<object, TValue>? GenerateInstanceSetter<TValue>(Type declaringType, string fieldName, bool throwOnError = false)
    {
        const string source = "Accessor.GenerateInstanceSetter";
        if (declaringType == null)
        {
            if (throwOnError)
                throw new Exception($"Error generating instance setter for <unknown>.{fieldName}. Declaring type not found.");
            if (LogErrorMessages)
                Logger.DevkitServer.LogError(source, $"Error generating instance setter for <unknown>.{fieldName.Colorize(FormattingColorType.Property)}. Declaring type not found.");
            return null;
        }
        FieldInfo? field = declaringType.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        field ??= declaringType.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
        if (field is null || field.IsStatic || !field.FieldType.IsAssignableFrom(typeof(TValue)) && typeof(TValue) != typeof(object))
        {
            if (throwOnError)
                throw new Exception($"Unable to find matching field: {declaringType.FullName}.{fieldName}.");
            if (LogErrorMessages)
                Logger.DevkitServer.LogError(source, $"Unable to find matching property {declaringType.Format()}.{fieldName.Colorize(FormattingColorType.Property)}.");
            return null;
        }

        fieldName = field.Name;

        try
        {
            CheckExceptionConstructors();

            GetDynamicMethodFlags(false, out MethodAttributes attr, out CallingConventions convention);
            DynamicMethod method = new DynamicMethod("set_" + fieldName, attr, convention, typeof(void), new Type[] { typeof(object), typeof(TValue) }, declaringType, true);
            method.DefineParameter(1, ParameterAttributes.None, "this");
            method.DefineParameter(2, ParameterAttributes.None, "value");
            IOpCodeEmitter il = CreateEmitter(method, source);
            Label lbl = il.DefineLabel();
            Label? typeLbl = null;
            il.Emit(OpCodes.Ldarg_0);

            bool isValueType = declaringType.IsValueType;
            if (CastExCtor != null || !isValueType && NreExCtor != null)
            {
                Label lbl2 = il.DefineLabel();
                il.Emit(OpCodes.Isinst, declaringType);
                if (!isValueType)
                    il.Emit(OpCodes.Dup);
                il.Emit(OpCodes.Brtrue_S, lbl);
                if (!isValueType)
                    il.Emit(OpCodes.Pop);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Brfalse_S, lbl2);
                if (CastExCtor != null)
                    il.Emit(OpCodes.Ldstr, "Invalid instance type passed to getter for " + fieldName + ". Expected " + declaringType.FullName + ".");
                il.Emit(OpCodes.Newobj, CastExCtor ?? NreExCtor!);
                il.Emit(OpCodes.Throw);
                il.MarkLabel(lbl2);
                ConstructorInfo ctor = NreExCtor ?? CastExCtor!;
                if (ctor == CastExCtor)
                    il.Emit(OpCodes.Ldstr, "Null passed to getter for " + fieldName + ". Expected " + declaringType.FullName + ".");
                il.Emit(OpCodes.Newobj, ctor);
                il.Emit(OpCodes.Throw);
            }
            il.MarkLabel(lbl);
            if (isValueType)
            {
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Unbox, declaringType);
                il.Emit(OpCodes.Ldflda, field);
                il.Emit(OpCodes.Ldarg_1);

                if (!typeof(TValue).IsValueType && field.FieldType.IsValueType)
                    il.Emit(OpCodes.Unbox_Any, field.FieldType);
                else if (typeof(TValue).IsValueType && !field.FieldType.IsValueType)
                    il.Emit(OpCodes.Box, typeof(TValue));
                else if (!field.FieldType.IsAssignableFrom(typeof(TValue)) && (CastExCtor != null || NreExCtor != null))
                {
                    typeLbl = il.DefineLabel();
                    il.Emit(OpCodes.Isinst, field.FieldType);
                    il.Emit(OpCodes.Dup);
                    il.Emit(OpCodes.Brtrue_S, typeLbl.Value);
                    il.Emit(OpCodes.Pop);
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Dup);
                    il.Emit(OpCodes.Brfalse_S, typeLbl.Value);
                    il.Emit(OpCodes.Pop);
                    il.Emit(OpCodes.Pop);
                    if (CastExCtor != null)
                        il.Emit(OpCodes.Ldstr, "Invalid argument type passed to setter for " + fieldName + ". Expected " + field.FieldType.FullName + ".");
                    il.Emit(OpCodes.Newobj, CastExCtor ?? NreExCtor!);
                    il.Emit(OpCodes.Throw);
                }

                if (typeLbl.HasValue)
                    il.MarkLabel(typeLbl.Value);

                il.Emit(OpCodes.Stobj, field.FieldType);
            }
            else
            {
                il.Emit(OpCodes.Ldarg_1);

                if (!typeof(TValue).IsValueType && field.FieldType.IsValueType)
                    il.Emit(OpCodes.Unbox_Any, field.FieldType);
                else if (typeof(TValue).IsValueType && !field.FieldType.IsValueType)
                    il.Emit(OpCodes.Box, typeof(TValue));
                else if (!field.FieldType.IsAssignableFrom(typeof(TValue)) && (CastExCtor != null || NreExCtor != null))
                {
                    typeLbl = il.DefineLabel();
                    il.Emit(OpCodes.Isinst, field.FieldType);
                    il.Emit(OpCodes.Dup);
                    il.Emit(OpCodes.Brtrue_S, typeLbl.Value);
                    il.Emit(OpCodes.Pop);
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Dup);
                    il.Emit(OpCodes.Brfalse_S, typeLbl.Value);
                    il.Emit(OpCodes.Pop);
                    il.Emit(OpCodes.Pop);
                    if (CastExCtor != null)
                        il.Emit(OpCodes.Ldstr, "Invalid argument type passed to setter for " + fieldName + ". Expected " + field.FieldType.FullName + ".");
                    il.Emit(OpCodes.Newobj, CastExCtor ?? NreExCtor!);
                    il.Emit(OpCodes.Throw);
                }

                il.Emit(OpCodes.Stfld, field);
            }
            il.Emit(OpCodes.Ret);
            InstanceSetter<object, TValue> setter = (InstanceSetter<object, TValue>)method.CreateDelegate(typeof(InstanceSetter<object, TValue>));
            
            if (LogDebugMessages || LogILTraceMessages)
                Logger.DevkitServer.LogDebug(source, $"Created dynamic method instance setter for {field.Format()}.");

            return setter;
        }
        catch (Exception ex)
        {
            if (throwOnError)
                throw new Exception($"Error generating instance setter for {declaringType.FullName}.{fieldName}.", ex);
            if (LogErrorMessages)
            {
                Logger.DevkitServer.LogError(source, ex, $"Error generating instance setter for {declaringType.Format()}.{fieldName.Colorize(FormattingColorType.Property)}.");
            }
            return null;
        }
    }

    /// <summary>
    /// Generates a dynamic method that gets an instance field value. Works for reference or value types.
    /// </summary>
    /// <param name="declaringType">Declaring type of the field.</param>
    /// <typeparam name="TValue">Field return type.</typeparam>
    /// <param name="fieldName">Name of field that will be referenced.</param>
    /// <param name="throwOnError">Throw an error instead of writing to console and returning <see langword="null"/>.</param>
    /// <remarks>Will never return <see langword="null"/> if <paramref name="throwOnError"/> is <see langword="true"/>.</remarks>
    [Pure]
    public static InstanceGetter<object, TValue>? GenerateInstanceGetter<TValue>(Type declaringType, string fieldName, bool throwOnError = false)
    {
        const string source = "Accessor.GenerateInstanceGetter";
        if (declaringType == null)
        {
            if (throwOnError)
                throw new Exception($"Error generating instance getter for <unknown>.{fieldName}. Declaring type not found.");
            if (LogErrorMessages)
                Logger.DevkitServer.LogError(source, $"Error generating instance getter for <unknown>.{fieldName.Colorize(FormattingColorType.Property)}. Declaring type not found.");
            return null;
        }
        FieldInfo? field = declaringType.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        field ??= declaringType.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
        if (field is null || field.IsStatic || !typeof(TValue).IsAssignableFrom(field.FieldType) && field.FieldType != typeof(object))
        {
            if (throwOnError)
                throw new Exception($"Unable to find matching field: {declaringType.FullName}.{fieldName}.");
            if (LogErrorMessages)
                Logger.DevkitServer.LogError(source, $"Unable to find matching property {declaringType.Format()}.{fieldName.Colorize(FormattingColorType.Property)}.");
            return null;
        }

        fieldName = field.Name;

        try
        {
            CheckExceptionConstructors();

            GetDynamicMethodFlags(false, out MethodAttributes attr, out CallingConventions convention);
            DynamicMethod method = new DynamicMethod("get_" + fieldName, attr, convention, typeof(TValue), new Type[] { typeof(object) }, declaringType, true);
            method.DefineParameter(1, ParameterAttributes.None, "this");
            IOpCodeEmitter il = CreateEmitter(method, source);
            il.Emit(OpCodes.Ldarg_0);
            Label? lbl = null;

            bool isValueType = declaringType.IsValueType;
            if (CastExCtor != null || !isValueType && NreExCtor != null)
            {
                lbl = il.DefineLabel();
                Label lbl2 = il.DefineLabel();
                il.Emit(OpCodes.Isinst, declaringType);
                il.Emit(OpCodes.Dup);
                il.Emit(OpCodes.Brtrue_S, lbl.Value);
                il.Emit(OpCodes.Pop);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Brfalse_S, lbl2);
                if (CastExCtor != null)
                    il.Emit(OpCodes.Ldstr, "Invalid instance type passed to getter for " + fieldName + ". Expected " + declaringType.FullName + ".");
                il.Emit(OpCodes.Newobj, CastExCtor ?? NreExCtor!);
                il.Emit(OpCodes.Throw);
                il.MarkLabel(lbl2);
                ConstructorInfo ctor = NreExCtor ?? CastExCtor!;
                if (ctor == CastExCtor)
                    il.Emit(OpCodes.Ldstr, "Null passed to getter for " + fieldName + ". Expected " + declaringType.FullName + ".");
                il.Emit(OpCodes.Newobj, ctor);
                il.Emit(OpCodes.Throw);
            }
            if (lbl.HasValue)
                il.MarkLabel(lbl.Value);
            if (isValueType)
                il.Emit(OpCodes.Unbox, declaringType);
            il.Emit(OpCodes.Ldfld, field);

            if (typeof(TValue).IsValueType && !field.FieldType.IsValueType)
                il.Emit(OpCodes.Unbox_Any, typeof(TValue));
            else if (!typeof(TValue).IsValueType && field.FieldType.IsValueType)
                il.Emit(OpCodes.Box, field.FieldType);

            il.Emit(OpCodes.Ret);
            InstanceGetter<object, TValue> getter = (InstanceGetter<object, TValue>)method.CreateDelegate(typeof(InstanceGetter<object, TValue>));

            if (LogDebugMessages || LogILTraceMessages)
                Logger.DevkitServer.LogDebug(source, $"Created dynamic method instance getter for {field.Format()}.");

            return getter;
        }
        catch (Exception ex)
        {
            if (throwOnError)
                throw new Exception($"Error generating instance getter for {declaringType.FullName}.{fieldName}.", ex);
            if (LogErrorMessages)
            {
                Logger.DevkitServer.LogError(source, ex, $"Error generating instance getter for {declaringType.Format()}.{fieldName.Colorize(FormattingColorType.Property)}.");
            }
            return null;
        }
    }

    /// <summary>
    /// Generates a delegate that sets an instance property value. For value types use <see cref="GenerateInstanceSetter{TValue}"/> instead.
    /// </summary>
    /// <typeparam name="TInstance">Declaring type of the property.</typeparam>
    /// <typeparam name="TValue">Property return type.</typeparam>
    /// <param name="propertyName">Name of property that will be referenced.</param>
    /// <param name="throwOnError">Throw an error instead of writing to console and returning <see langword="null"/>.</param>
    /// <remarks>Will never return <see langword="null"/> if <paramref name="throwOnError"/> is <see langword="true"/>.</remarks>
    [Pure]
    public static InstanceSetter<TInstance, TValue>? GenerateInstancePropertySetter<TInstance, TValue>(string propertyName, bool throwOnError = false)
    {
        const string source = "Accessor.GenerateInstancePropertySetter";
        if (typeof(TInstance).IsValueType)
        {
            if (throwOnError)
                throw new Exception($"Unable to create instance setter for {typeof(TInstance).FullName}.{propertyName}, you must pass structs ({typeof(TInstance).Name}) as a boxed object.");

            if (LogErrorMessages)
                Logger.DevkitServer.LogError(source, $"Unable to create instance setter for {typeof(TInstance).Format()}.{propertyName.Colorize(FormattingColorType.Property)}, you must pass structs ({typeof(TInstance).Format()}) as a boxed object.");
            return null;
        }

        PropertyInfo? property = typeof(TInstance).GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        MethodInfo? setter = property?.GetSetMethod(true);
        if (setter is null || setter.IsStatic || setter.GetParameters() is not { Length: 1 } parameters || !parameters[0].ParameterType.IsAssignableFrom(typeof(TValue)) && typeof(TValue) != typeof(object))
        {
            property = typeof(TInstance).GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            setter = property?.GetSetMethod(true);

            if (setter is null || setter.IsStatic || setter.GetParameters() is not { Length: 1 } parameters2 || !parameters2[0].ParameterType.IsAssignableFrom(typeof(TValue)) && typeof(TValue) != typeof(object))
            {
                if (throwOnError)
                    throw new Exception($"Unable to find matching property: {typeof(TInstance).FullName}.{propertyName} with a setter.");
                if (LogErrorMessages)
                    Logger.DevkitServer.LogError(source, $"Unable to find matching property {typeof(TInstance).Format()}.{propertyName.Colorize(FormattingColorType.Property)} with a setter.");
                return null;
            }
        }

        return GenerateInstanceCaller<InstanceSetter<TInstance, TValue>>(setter, throwOnError, true);
    }

    /// <summary>
    /// Generates a delegate that gets an instance property value. Works for reference or value types.
    /// </summary>
    /// <typeparam name="TInstance">Declaring type of the property.</typeparam>
    /// <typeparam name="TValue">Property return type.</typeparam>
    /// <param name="propertyName">Name of property that will be referenced.</param>
    /// <param name="throwOnError">Throw an error instead of writing to console and returning <see langword="null"/>.</param>
    /// <remarks>Will never return <see langword="null"/> if <paramref name="throwOnError"/> is <see langword="true"/>.</remarks>
    [Pure]
    public static InstanceGetter<TInstance, TValue>? GenerateInstancePropertyGetter<TInstance, TValue>(string propertyName, bool throwOnError = false)
    {
        const string source = "Accessor.GenerateInstancePropertyGetter";
        PropertyInfo? property = typeof(TInstance).GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        MethodInfo? getter = property?.GetGetMethod(true);
        if (getter is null || getter.IsStatic || !typeof(TValue).IsAssignableFrom(getter.ReturnType) && getter.ReturnType != typeof(object))
        {
            property = typeof(TInstance).GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            getter = property?.GetGetMethod(true);

            if (getter is null || getter.IsStatic || !typeof(TValue).IsAssignableFrom(getter.ReturnType) && getter.ReturnType != typeof(object))
            {
                if (throwOnError)
                    throw new Exception($"Unable to find matching property: {typeof(TInstance).FullName}.{propertyName} with a getter.");
                if (LogErrorMessages)
                    Logger.DevkitServer.LogError(source, $"Unable to find matching property {typeof(TInstance).Format()}.{propertyName.Colorize(FormattingColorType.Property)} with a getter.");
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
    /// <param name="allowUnsafeTypeBinding">Enables unsafe type binding to non-matching delegates, meaning classes of different
    /// types can be passed as parameters and an exception will not be thrown (may cause unintended behavior if the wrong type is passed).
    /// This also must be <see langword="true"/> to not null-check instance methods of parameter-less reference types with a dynamic method.</param>
    /// <param name="declaringType">Declaring type of the property.</param>
    /// <typeparam name="TValue">Property return type.</typeparam>
    /// <param name="propertyName">Name of property that will be referenced.</param>
    /// <param name="throwOnError">Throw an error instead of writing to console and returning <see langword="null"/>.</param>
    /// <remarks>Will never return <see langword="null"/> if <paramref name="throwOnError"/> is <see langword="true"/>.</remarks>
    [Pure]
    public static InstanceSetter<object, TValue>? GenerateInstancePropertySetter<TValue>(Type declaringType, string propertyName, bool throwOnError = false, bool allowUnsafeTypeBinding = false)
    {
        const string source = "Accessor.GenerateInstancePropertySetter";
        if (declaringType == null)
        {
            if (throwOnError)
                throw new Exception($"Error generating instance setter for <unknown>.{propertyName}. Declaring type not found.");
            if (LogErrorMessages)
                Logger.DevkitServer.LogError(source, $"Error generating instance setter for <unknown>.{propertyName.Colorize(FormattingColorType.Property)}. Declaring type not found.");
            return null;
        }
        PropertyInfo? property = declaringType.GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        MethodInfo? setter = property?.GetSetMethod(true);
        if (setter is null || setter.IsStatic || setter.GetParameters() is not { Length: 1 } parameters || !parameters[0].ParameterType.IsAssignableFrom(typeof(TValue)) && typeof(TValue) != typeof(object))
        {
            property = declaringType.GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            setter = property?.GetSetMethod(true);

            if (setter is null || setter.IsStatic || setter.GetParameters() is not { Length: 1 } parameters2 || !parameters2[0].ParameterType.IsAssignableFrom(typeof(TValue)) && typeof(TValue) != typeof(object))
            {
                if (throwOnError)
                    throw new Exception($"Unable to find matching property: {declaringType.FullName}.{propertyName} with a setter.");
                if (LogErrorMessages)
                    Logger.DevkitServer.LogError(source, $"Unable to find matching property {declaringType.Format()}.{propertyName.Colorize(FormattingColorType.Property)} with a setter.");
                return null;
            }
        }

        return GenerateInstanceCaller<InstanceSetter<object, TValue>>(setter, throwOnError, allowUnsafeTypeBinding);
    }

    /// <summary>
    /// Generates a delegate if possible, otherwise a dynamic method, that gets an instance property value. Works for reference or value types.
    /// </summary>
    /// <param name="allowUnsafeTypeBinding">Enables unsafe type binding to non-matching delegates, meaning classes of different
    /// types can be passed as parameters and an exception will not be thrown (may cause unintended behavior if the wrong type is passed).
    /// This also must be <see langword="true"/> to not null-check instance methods of parameter-less reference types with a dynamic method.</param>
    /// <param name="declaringType">Declaring type of the property.</param>
    /// <typeparam name="TValue">Property return type.</typeparam>
    /// <param name="propertyName">Name of property that will be referenced.</param>
    /// <param name="throwOnError">Throw an error instead of writing to console and returning <see langword="null"/>.</param>
    /// <remarks>Will never return <see langword="null"/> if <paramref name="throwOnError"/> is <see langword="true"/>.</remarks>
    [Pure]
    public static InstanceGetter<object, TValue>? GenerateInstancePropertyGetter<TValue>(Type declaringType, string propertyName, bool throwOnError = false, bool allowUnsafeTypeBinding = false)
    {
        const string source = "Accessor.GenerateInstancePropertyGetter";
        if (declaringType == null)
        {
            if (throwOnError)
                throw new Exception($"Error generating instance getter for <unknown>.{propertyName}. Declaring type not found.");
            if (LogErrorMessages)
                Logger.DevkitServer.LogError(source, $"Error generating instance getter for <unknown>.{propertyName.Colorize(FormattingColorType.Property)}. Declaring type not found.");
            return null;
        }

        PropertyInfo? property = declaringType.GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        MethodInfo? getter = property?.GetGetMethod(true);
        if (getter is null || getter.IsStatic || !typeof(TValue).IsAssignableFrom(getter.ReturnType) && getter.ReturnType != typeof(object))
        {
            property = declaringType.GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            getter = property?.GetGetMethod(true);

            if (getter is null || getter.IsStatic || !typeof(TValue).IsAssignableFrom(getter.ReturnType) && getter.ReturnType != typeof(object))
            {
                if (throwOnError)
                    throw new Exception($"Unable to find matching property: {declaringType.FullName}.{propertyName} with a getter.");
                if (LogErrorMessages)
                    Logger.DevkitServer.LogError(source, $"Unable to find matching property {declaringType.Format()}.{propertyName.Colorize(FormattingColorType.Property)} with a getter.");
                return null;
            }
        }

        return GenerateInstanceCaller<InstanceGetter<object, TValue>>(getter, throwOnError, allowUnsafeTypeBinding);
    }

    /// <summary>
    /// Generates a dynamic method that sets a static field value.
    /// </summary>
    /// <typeparam name="TDeclaringType">Declaring type of the field.</typeparam>
    /// <typeparam name="TValue">Field return type.</typeparam>
    /// <param name="fieldName">Name of the field that will be referenced.</param>
    /// <param name="throwOnError">Throw an error instead of writing to console and returning <see langword="null"/>.</param>
    /// <remarks>Will never return <see langword="null"/> if <paramref name="throwOnError"/> is <see langword="true"/>.</remarks>
    [Pure]
    public static StaticSetter<TValue>? GenerateStaticSetter<TDeclaringType, TValue>(string fieldName, bool throwOnError = false)
        => GenerateStaticSetter<TValue>(typeof(TDeclaringType), fieldName, throwOnError);

    /// <summary>
    /// Generates a dynamic method that gets a static field value.
    /// </summary>
    /// <typeparam name="TDeclaringType">Declaring type of the field.</typeparam>
    /// <typeparam name="TValue">Field return type.</typeparam>
    /// <param name="fieldName">Name of the field that will be referenced.</param>
    /// <param name="throwOnError">Throw an error instead of writing to console and returning <see langword="null"/>.</param>
    /// <remarks>Will never return <see langword="null"/> if <paramref name="throwOnError"/> is <see langword="true"/>.</remarks>
    [Pure]
    public static StaticGetter<TValue>? GenerateStaticGetter<TDeclaringType, TValue>(string fieldName, bool throwOnError = false)
        => GenerateStaticGetter<TValue>(typeof(TDeclaringType), fieldName, throwOnError);

    /// <summary>
    /// Generates a dynamic method that sets a static field value.
    /// </summary>
    /// <param name="declaringType">Declaring type of the field.</param>
    /// <typeparam name="TValue">Field return type.</typeparam>
    /// <param name="fieldName">Name of the field that will be referenced.</param>
    /// <param name="throwOnError">Throw an error instead of writing to console and returning <see langword="null"/>.</param>
    /// <remarks>Will never return <see langword="null"/> if <paramref name="throwOnError"/> is <see langword="true"/>.</remarks>
    [Pure]
    public static StaticSetter<TValue>? GenerateStaticSetter<TValue>(Type declaringType, string fieldName, bool throwOnError = false)
    {
        const string source = "Accessor.GenerateStaticSetter";
        if (declaringType == null)
        {
            if (throwOnError)
                throw new Exception($"Error generating static setter for <unknown>.{fieldName}. Declaring type not found.");
            if (LogErrorMessages)
                Logger.DevkitServer.LogError(source, $"Error generating static setter for <unknown>.{fieldName.Colorize(FormattingColorType.Property)}. Declaring type not found.");
            return null;
        }
        FieldInfo? field = declaringType.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);

        if (field is null || !field.IsStatic || !field.FieldType.IsAssignableFrom(typeof(TValue)) && typeof(TValue) != typeof(object))
        {
            if (throwOnError)
                throw new Exception($"Unable to find matching field: {declaringType.FullName}.{fieldName}.");
            if (LogErrorMessages)
                Logger.DevkitServer.LogError(source, $"Unable to find matching field {declaringType.Format()}.{fieldName.Colorize(FormattingColorType.Property)}.");
            return null;
        }

        fieldName = field.Name;

        try
        {
            CheckExceptionConstructors();

            GetDynamicMethodFlags(true, out MethodAttributes attr, out CallingConventions convention);
            DynamicMethod method = new DynamicMethod("set_" + fieldName, attr, convention, typeof(void), new Type[] { typeof(TValue) }, declaringType, true);
            method.DefineParameter(1, ParameterAttributes.None, "value");
            IOpCodeEmitter il = CreateEmitter(method, source);
            Label? lbl = null;
            il.Emit(OpCodes.Ldarg_0);

            if (!typeof(TValue).IsValueType && field.FieldType.IsValueType)
                il.Emit(OpCodes.Unbox_Any, field.FieldType);
            else if (typeof(TValue).IsValueType && !field.FieldType.IsValueType)
                il.Emit(OpCodes.Box, typeof(TValue));
            else if (!field.FieldType.IsAssignableFrom(typeof(TValue)) && (CastExCtor != null || NreExCtor != null))
            {
                lbl = il.DefineLabel();
                il.Emit(OpCodes.Isinst, field.FieldType);
                il.Emit(OpCodes.Dup);
                il.Emit(OpCodes.Brtrue_S, lbl.Value);
                il.Emit(OpCodes.Pop);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Dup);
                il.Emit(OpCodes.Brfalse_S, lbl.Value);
                il.Emit(OpCodes.Pop);
                if (CastExCtor != null)
                    il.Emit(OpCodes.Ldstr, "Invalid argument type passed to getter for " + fieldName + ". Expected " + field.FieldType.FullName + ".");
                il.Emit(OpCodes.Newobj, CastExCtor ?? NreExCtor!);
                il.Emit(OpCodes.Throw);
            }

            if (lbl.HasValue)
                il.MarkLabel(lbl.Value);

            il.Emit(OpCodes.Stsfld, field);
            il.Emit(OpCodes.Ret);
            StaticSetter<TValue> setter = (StaticSetter<TValue>)method.CreateDelegate(typeof(StaticSetter<TValue>));

            if (LogDebugMessages || LogErrorMessages)
                Logger.DevkitServer.LogDebug(source, $"Created dynamic method static setter for {field.Format()}.");

            return setter;
        }
        catch (Exception ex)
        {
            if (throwOnError)
                throw new Exception($"Error generating static setter for {declaringType.FullName}.{fieldName}.", ex);
            if (LogErrorMessages)
            {
                Logger.DevkitServer.LogError(source, ex, $"Error generating static setter for {declaringType.Format()}.{fieldName.Colorize(FormattingColorType.Property)}.");
            }
            return null;
        }
    }

    /// <summary>
    /// Generates a dynamic method that gets a static field value.
    /// </summary>
    /// <param name="declaringType">Declaring type of the field.</param>
    /// <typeparam name="TValue">Field return type.</typeparam>
    /// <param name="fieldName">Name of the field that will be referenced.</param>
    /// <param name="throwOnError">Throw an error instead of writing to console and returning <see langword="null"/>.</param>
    /// <remarks>Will never return <see langword="null"/> if <paramref name="throwOnError"/> is <see langword="true"/>.</remarks>
    [Pure]
    public static StaticGetter<TValue>? GenerateStaticGetter<TValue>(Type declaringType, string fieldName, bool throwOnError = false)
    {
        const string source = "Accessor.GenerateStaticGetter";
        if (declaringType == null)
        {
            if (throwOnError)
                throw new Exception($"Error generating static getter for <unknown>.{fieldName}. Declaring type not found.");
            if (LogErrorMessages)
                Logger.DevkitServer.LogError(source, $"Error generating static getter for <unknown>.{fieldName.Colorize(FormattingColorType.Property)}. Declaring type not found.");
            return null;
        }
        FieldInfo? field = declaringType.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);

        if (field is null || !field.IsStatic || !typeof(TValue).IsAssignableFrom(field.FieldType) && field.FieldType != typeof(object))
        {
            if (throwOnError)
                throw new Exception($"Unable to find matching field: {declaringType.FullName}.{fieldName}.");
            if (LogErrorMessages)
                Logger.DevkitServer.LogError(source, $"Unable to find matching property {declaringType.Format()}.{fieldName.Colorize(FormattingColorType.Property)}.");
            return null;
        }

        fieldName = field.Name;

        try
        {
            GetDynamicMethodFlags(true, out MethodAttributes attr, out CallingConventions convention);
            DynamicMethod method = new DynamicMethod("get_" + fieldName, attr, convention, typeof(TValue), Type.EmptyTypes, declaringType, true);
            IOpCodeEmitter il = CreateEmitter(method, source);
            il.Emit(OpCodes.Ldsfld, field);

            if (typeof(TValue).IsValueType && !field.FieldType.IsValueType)
                il.Emit(OpCodes.Unbox_Any, typeof(TValue));
            else if (!typeof(TValue).IsValueType && field.FieldType.IsValueType)
                il.Emit(OpCodes.Box, field.FieldType);

            il.Emit(OpCodes.Ret);
            StaticGetter<TValue> getter = (StaticGetter<TValue>)method.CreateDelegate(typeof(StaticGetter<TValue>));

            if (LogDebugMessages || LogILTraceMessages)
                Logger.DevkitServer.LogDebug(source, $"Created dynamic method static getter for {field.Format()}.");

            return getter;
        }
        catch (Exception ex)
        {
            if (throwOnError)
                throw new Exception($"Error generating static getter for {declaringType.FullName}.{fieldName}.", ex);
            if (LogErrorMessages)
            {
                Logger.DevkitServer.LogError(source, ex, $"Error generating static getter for {declaringType.Format()}.{fieldName.Colorize(FormattingColorType.Property)}.");
            }
            return null;
        }
    }

    /// <summary>
    /// Generates a delegate or dynamic method that sets a static property value.
    /// </summary>
    /// <typeparam name="TDeclaringType">Declaring type of the property.</typeparam>
    /// <typeparam name="TValue">Property return type.</typeparam>
    /// <param name="propertyName">Name of property that will be referenced.</param>
    /// <param name="throwOnError">Throw an error instead of writing to console and returning <see langword="null"/>.</param>
    /// <param name="allowUnsafeTypeBinding">Enables unsafe type binding to non-matching delegates, meaning classes of different
    /// types can be passed as parameters and an exception will not be thrown (may cause unintended behavior if the wrong type is passed).
    /// This also must be <see langword="true"/> to not null-check instance methods of parameter-less reference types with a dynamic method.</param>
    /// <remarks>Will never return <see langword="null"/> if <paramref name="throwOnError"/> is <see langword="true"/>.</remarks>
    [Pure]
    public static StaticSetter<TValue>? GenerateStaticPropertySetter<TDeclaringType, TValue>(string propertyName, bool throwOnError = false, bool allowUnsafeTypeBinding = false)
        => GenerateStaticPropertySetter<TValue>(typeof(TDeclaringType), propertyName, throwOnError, allowUnsafeTypeBinding);

    /// <summary>
    /// Generates a delegate that gets a static property value.
    /// </summary>
    /// <typeparam name="TDeclaringType">Declaring type of the property.</typeparam>
    /// <typeparam name="TValue">Property return type.</typeparam>
    /// <param name="propertyName">Name of property that will be referenced.</param>
    /// <param name="throwOnError">Throw an error instead of writing to console and returning <see langword="null"/>.</param>
    /// <param name="allowUnsafeTypeBinding">Enables unsafe type binding to non-matching delegates, meaning classes of different
    /// types can be passed as parameters and an exception will not be thrown (may cause unintended behavior if the wrong type is passed).
    /// This also must be <see langword="true"/> to not null-check instance methods of parameter-less reference types with a dynamic method.</param>
    /// <remarks>Will never return <see langword="null"/> if <paramref name="throwOnError"/> is <see langword="true"/>.</remarks>
    [Pure]
    public static StaticGetter<TValue>? GenerateStaticPropertyGetter<TDeclaringType, TValue>(string propertyName, bool throwOnError = false, bool allowUnsafeTypeBinding = true)
        => GenerateStaticPropertyGetter<TValue>(typeof(TDeclaringType), propertyName, throwOnError, allowUnsafeTypeBinding);

    /// <summary>
    /// Generates a delegate or dynamic method that sets a static property value.
    /// </summary>
    /// <param name="allowUnsafeTypeBinding">Enables unsafe type binding to non-matching delegates, meaning classes of different
    /// types can be passed as parameters and an exception will not be thrown (may cause unintended behavior if the wrong type is passed).
    /// This also must be <see langword="true"/> to not null-check instance methods of parameter-less reference types with a dynamic method.</param>
    /// <param name="declaringType">Declaring type of the property.</param>
    /// <typeparam name="TValue">Property return type.</typeparam>
    /// <param name="propertyName">Name of property that will be referenced.</param>
    /// <param name="throwOnError">Throw an error instead of writing to console and returning <see langword="null"/>.</param>
    /// <remarks>Will never return <see langword="null"/> if <paramref name="throwOnError"/> is <see langword="true"/>.</remarks>
    [Pure]
    public static StaticSetter<TValue>? GenerateStaticPropertySetter<TValue>(Type declaringType, string propertyName, bool throwOnError = false, bool allowUnsafeTypeBinding = false)
    {
        const string source = "Accessor.GenerateStaticPropertySetter";
        if (declaringType == null)
        {
            if (throwOnError)
                throw new Exception($"Error generating static setter for <unknown>.{propertyName}. Declaring type not found.");
            if (LogErrorMessages)
                Logger.DevkitServer.LogError(source, $"Error generating static setter for <unknown>.{propertyName.Colorize(FormattingColorType.Property)}. Declaring type not found.");
            return null;
        }
        PropertyInfo? property = declaringType.GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
        MethodInfo? setter = property?.GetSetMethod(true);

        if (setter is null || !setter.IsStatic || setter.GetParameters() is not { Length: 1 } parameters || !parameters[0].ParameterType.IsAssignableFrom(typeof(TValue)) && typeof(TValue) != typeof(object))
        {
            if (throwOnError)
                throw new Exception($"Unable to find matching property: {declaringType.FullName}.{propertyName} with a setter.");
            if (LogErrorMessages)
                Logger.DevkitServer.LogError(source, $"Unable to find matching property {declaringType.Format()}.{propertyName.Colorize(FormattingColorType.Property)} with a setter.");
            return null;
        }

        return GenerateStaticCaller<StaticSetter<TValue>>(setter, throwOnError, allowUnsafeTypeBinding);
    }

    /// <summary>
    /// Generates a delegate that gets a static property value.
    /// </summary>
    /// <param name="allowUnsafeTypeBinding">Enables unsafe type binding to non-matching delegates, meaning classes of different
    /// types can be passed as parameters and an exception will not be thrown (may cause unintended behavior if the wrong type is passed).
    /// This also must be <see langword="true"/> to not null-check instance methods of parameter-less reference types with a dynamic method.</param>
    /// <param name="declaringType">Declaring type of the property.</param>
    /// <typeparam name="TValue">Property return type.</typeparam>
    /// <param name="propertyName">Name of property that will be referenced.</param>
    /// <param name="throwOnError">Throw an error instead of writing to console and returning <see langword="null"/>.</param>
    /// <remarks>Will never return <see langword="null"/> if <paramref name="throwOnError"/> is <see langword="true"/>.</remarks>
    [Pure]
    public static StaticGetter<TValue>? GenerateStaticPropertyGetter<TValue>(Type declaringType, string propertyName, bool throwOnError = false, bool allowUnsafeTypeBinding = false)
    {
        const string source = "Accessor.GenerateStaticPropertyGetter";
        if (declaringType == null)
        {
            if (throwOnError)
                throw new Exception($"Error generating static getter for <unknown>.{propertyName}. Declaring type not found.");
            if (LogErrorMessages)
                Logger.DevkitServer.LogError(source, $"Error generating static getter for <unknown>.{propertyName.Colorize(FormattingColorType.Property)}. Declaring type not found.");
            return null;
        }
        PropertyInfo? property = declaringType.GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
        MethodInfo? getter = property?.GetGetMethod(true);

        if (getter is null || !getter.IsStatic || !typeof(TValue).IsAssignableFrom(getter.ReturnType) && getter.ReturnType != typeof(object))
        {
            if (throwOnError)
                throw new Exception($"Unable to find matching property: {declaringType.FullName}.{propertyName} with a getter.");
            if (LogErrorMessages)
                Logger.DevkitServer.LogError(source, $"Unable to find matching property {declaringType.Format()}.{propertyName.Colorize(FormattingColorType.Property)} with a getter.");
            return null;
        }

        return GenerateStaticCaller<StaticGetter<TValue>>(getter, throwOnError, allowUnsafeTypeBinding);
    }

    /// <summary>
    /// Generates a delegate or dynamic method that calls an instance method.
    /// </summary>
    /// <remarks>The first parameter will be the instance.</remarks>
    /// <param name="allowUnsafeTypeBinding">Enables unsafe type binding to non-matching delegates, meaning classes of different
    /// types can be passed as parameters and an exception will not be thrown (may cause unintended behavior if the wrong type is passed).
    /// This also must be <see langword="true"/> to not null-check instance methods of parameter-less reference types with a dynamic method.</param>
    /// <returns>A delegate of type <see cref="Action"/> or <see cref="Func{T}"/> (or one of their generic counterparts), depending on the method signature. The first parameter will be the instance.</returns>
    /// <param name="methodName">Name of method that will be called.</param>
    /// <param name="parameters">Optional parameter list for resolving ambiguous methods.</param>
    /// <param name="throwOnError">Throw an error instead of writing to console and returning <see langword="null"/>.</param>
    /// <remarks>Will never return <see langword="null"/> if <paramref name="throwOnError"/> is <see langword="true"/>.</remarks>
    [Pure]
    public static Delegate? GenerateInstanceCaller<TInstance>(string methodName, Type[]? parameters = null, bool throwOnError = false, bool allowUnsafeTypeBinding = false)
    {
        const string source = "Accessor.GenerateInstanceCaller";
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

                if (LogErrorMessages)
                    Logger.DevkitServer.LogError(source, $"Unable to find matching instance method: {FormattingUtil.FormatMethod(null, typeof(TInstance), methodName, null, parameters, null, isStatic: false)}.");
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
    /// types can be passed as parameters and an exception will not be thrown (may cause unintended behavior if the wrong type is passed).
    /// This also must be <see langword="true"/> to not null-check instance methods of parameter-less reference types with a dynamic method.</param>
    /// <param name="methodName">Name of method that will be called.</param>
    /// <param name="parameters">Optional parameter list for resolving ambiguous methods.</param>
    /// <param name="throwOnError">Throw an error instead of writing to console and returning <see langword="null"/>.</param>
    /// <remarks>Will never return <see langword="null"/> if <paramref name="throwOnError"/> is <see langword="true"/>.</remarks>
    [Pure]
    public static TDelegate? GenerateInstanceCaller<TInstance, TDelegate>(string methodName, bool throwOnError = false, bool allowUnsafeTypeBinding = false, Type[]? parameters = null) where TDelegate : Delegate
    {
        const string source = "Accessor.GenerateInstanceCaller";
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
                if (parameters == null)
                {
                    ParameterInfo[] paramInfo = GetParameters<TDelegate>();
                    parameters = paramInfo.Length < 2 ? Type.EmptyTypes : new Type[paramInfo.Length - 1];
                    for (int i = 0; i < parameters.Length; ++i)
                        parameters[i] = paramInfo[i + 1].ParameterType;
                }
                method = typeof(TInstance).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                             null, CallingConventions.Any, parameters, null) ??
                         typeof(TInstance).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy,
                             null, CallingConventions.Any, parameters, null);
            }
            if (method == null)
            {
                if (throwOnError)
                    throw new Exception($"Unable to find matching instance method: {typeof(TInstance).FullName}.{methodName}.");

                if (LogErrorMessages)
                    Logger.DevkitServer.LogError(source, $"Unable to find matching instance method: {FormattingUtil.FormatMethod<TDelegate>(methodName, declTypeOverride: typeof(TInstance))}.");
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
    /// types can be passed as parameters and an exception will not be thrown (may cause unintended behavior if the wrong type is passed).
    /// This also must be <see langword="true"/> to not null-check instance methods of parameter-less reference types with a dynamic method.</param>
    /// <param name="method">Method that will be called.</param>
    /// <param name="throwOnError">Throw an error instead of writing to console and returning <see langword="null"/>.</param>
    /// <remarks>Will never return <see langword="null"/> if <paramref name="throwOnError"/> is <see langword="true"/>.</remarks>
    [Pure]
    public static Delegate? GenerateInstanceCaller(MethodInfo method, bool throwOnError = false, bool allowUnsafeTypeBinding = false)
    {
        const string source = "Accessor.GenerateInstanceCaller";
        if (method == null || method.IsStatic || method.DeclaringType == null)
        {
            if (throwOnError)
                throw new Exception("Unable to find instance method.");
            if (LogErrorMessages)
                Logger.DevkitServer.LogError(source, "Unable to find instance method.");
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

            if (LogErrorMessages)
                Logger.DevkitServer.LogError(source, $"Method {method.Format()} can not have more than {maxArgs.Format()} arguments!");
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
    /// types can be passed as parameters and an exception will not be thrown (may cause unintended behavior if the wrong type is passed).
    /// This also must be <see langword="true"/> to not null-check instance methods of parameter-less reference types with a dynamic method.</param>
    /// <param name="method">Method that will be called.</param>
    /// <param name="throwOnError">Throw an error instead of writing to console and returning <see langword="null"/>.</param>
    /// <remarks>Will never return <see langword="null"/> if <paramref name="throwOnError"/> is <see langword="true"/>.</remarks>
    [Pure]
    public static TDelegate? GenerateInstanceCaller<TDelegate>(MethodInfo method, bool throwOnError = false, bool allowUnsafeTypeBinding = false) where TDelegate : Delegate
    {
        return (TDelegate?)GenerateInstanceCaller(typeof(TDelegate), method, throwOnError, allowUnsafeTypeBinding);
    }

    /// <summary>
    /// Generates a delegate or dynamic method that calls an instance method.
    /// </summary>
    /// <remarks>The first parameter will be the instance.</remarks>
    /// <param name="delegateType">Type of delegate to return.</param>
    /// <param name="allowUnsafeTypeBinding">Enables unsafe type binding to non-matching delegates, meaning classes of different
    /// types can be passed as parameters and an exception will not be thrown (may cause unintended behavior if the wrong type is passed).
    /// This also must be <see langword="true"/> to not null-check instance methods of parameter-less reference types with a dynamic method.</param>
    /// <param name="method">Method that will be called.</param>
    /// <param name="throwOnError">Throw an error instead of writing to console and returning <see langword="null"/>.</param>
    /// <remarks>Will never return <see langword="null"/> if <paramref name="throwOnError"/> is <see langword="true"/>.</remarks>
    [Pure]
    public static Delegate? GenerateInstanceCaller(Type delegateType, MethodInfo method, bool throwOnError = false, bool allowUnsafeTypeBinding = false)
    {
        const string source = "Accessor.GenerateInstanceCaller";
        if (!typeof(Delegate).IsAssignableFrom(delegateType))
            throw new ArgumentException(delegateType.FullName + " is not a delegate.", nameof(delegateType));
        if (method == null || method.IsStatic || method.DeclaringType == null)
        {
            if (throwOnError)
                throw new Exception($"Unable to find instance method for delegate: {delegateType.FullName}.");
            if (LogErrorMessages)
                Logger.DevkitServer.LogError(source, $"Unable to find matching instance method: {FormattingUtil.FormatMethod(delegateType, "<unknown-name>", isStatic: false)}.");
            return null;
        }

        ParameterInfo[] p = method.GetParameters();
        Type instance = method.DeclaringType;

        MethodInfo invokeMethod = delegateType.GetMethod("Invoke", BindingFlags.Instance | BindingFlags.Public)!;
        ParameterInfo[] delegateParameters = invokeMethod.GetParameters();
        Type delegateReturnType = invokeMethod.ReturnType;
        bool shouldCallvirt = method.ShouldCallvirtRuntime();
        bool needsDynamicMethod = shouldCallvirt || (!instance.IsValueType && !allowUnsafeTypeBinding) || method.ReturnType != typeof(void) && delegateReturnType == typeof(void);
        bool isInstanceForValueType = method is { DeclaringType.IsValueType: true };
        shouldCallvirt |= !instance.IsValueType;

        // for some reason invoking a delegate with zero parameters and a null instance does not throw an exception.
        // Adding parameters changes this behavior.
        if (!isInstanceForValueType && p.Length == 0 && !allowUnsafeTypeBinding)
            needsDynamicMethod = true;

        if (p.Length != delegateParameters.Length - 1)
        {
            if (throwOnError)
                throw new Exception($"Unable to create instance caller for {instance.FullName}.{method.Name}: incompatable delegate type: {delegateType.FullName}.");

            if (LogErrorMessages)
                Logger.DevkitServer.LogError(source, $"Unable to create instance caller for {method.Format()}: incompatable delegate type: {delegateType.Format()}.");
            return null;
        }

        if (needsDynamicMethod && instance.IsInterface && !delegateParameters[0].ParameterType.IsInterface)
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
                    if (LogDebugMessages || LogILTraceMessages)
                        Logger.DevkitServer.LogDebug(source, $"Created instance delegate caller for {method.Format()} of type {delegateType.Format()}.");
                    return basicDelegate;
                }
                catch (Exception ex)
                {
                    if (LogDebugMessages || LogILTraceMessages)
                    {
                        Logger.DevkitServer.LogDebug(source, $"Unable to create basic delegate binding instance caller for {method.Format()}.");
                        Logger.DevkitServer.LogDebug(source, $"{ex.GetType().Format()} - {ex.Message.Format(false)}");
                    }
                }
            }
        }

        if (isInstanceForValueType && delegateParameters[0].ParameterType != typeof(object) && !method.IsReadOnly())
        {
            if (throwOnError)
                throw new Exception($"Unable to create instance caller for {instance}.{method.Name} (non-readonly), you must pass structs ({instance.FullName}) as a boxed object (in {delegateType.FullName}).");

            if (LogErrorMessages)
                Logger.DevkitServer.LogError(source, $"Unable to create instance caller for {method.Format()} (non-{"readonly".Colorize(FormattingColorType.Keyword)}), you must pass structs ({instance.Format()}) as a boxed object (in {delegateType.Format()}).");
            return null;
        }

#if !NET6_0_OR_GREATER // unsafe type binding doesn't work past .NET 5.0
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
#line hidden
                try
                {
                    IntPtr ptr = method.MethodHandle.GetFunctionPointer();
                    // running the debugger here will crash the program so... don't.
                    object d2 = FormatterServices.GetUninitializedObject(delegateType);
                    delegateType.GetConstructors()[0].Invoke(d2, new object[] { null!, ptr });
                    if (LogDebugMessages || LogILTraceMessages)
                        Logger.DevkitServer.LogDebug(source, $"Created instance delegate caller for {method.Format()} (using unsafe type binding) of type {delegateType.Format()}.");
                    return (Delegate)d2;
                }
                catch (Exception ex)
                {
                    if (LogDebugMessages || LogILTraceMessages)
                    {
                        Logger.DevkitServer.LogDebug(source, $"Unable to create unsafely binded delegate binding instance caller for {method.Format()}.");
                        Logger.DevkitServer.LogDebug(source, $"{ex.GetType().Format()} - {ex.Message.Format(false)}");
                    }
                }
#line default
            }
        }
#endif

        // generate dynamic method as a worst-case scenerio
        Type[] parameterTypes = new Type[delegateParameters.Length];
        for (int i = 0; i < delegateParameters.Length; ++i)
            parameterTypes[i] = delegateParameters[i].ParameterType;

        GetDynamicMethodFlags(false, out MethodAttributes attributes, out CallingConventions convention);
        DynamicMethod dynMethod = new DynamicMethod("Invoke" + method.Name, attributes, convention, delegateReturnType, parameterTypes, instance.IsInterface ? typeof(Accessor) : instance, true);
        dynMethod.DefineParameter(1, ParameterAttributes.None, "this");

        for (int i = 0; i < p.Length; ++i)
            dynMethod.DefineParameter(i + 2, p[i].Attributes, p[i].Name);

        IOpCodeEmitter generator = CreateEmitter(dynMethod, source);

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
        {
            generator.EmitParameter(LogILTraceMessages ? source : null, i + 1, $"Invalid argument type passed to instance caller for {instance.FullName}.{method.Name} at parameter {i.ToString(CultureInfo.InvariantCulture)} ({p[i].Name}). " +
                                                                               $"Expected {p[i].ParameterType.FullName}.", false, type: parameterTypes[i + 1], p[i].ParameterType);
        }

        generator.Emit(shouldCallvirt ? OpCodes.Callvirt : OpCodes.Call, method);
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
            if (LogDebugMessages || LogILTraceMessages)
                Logger.DevkitServer.LogDebug(source, $"Created dynamic instance caller for {method.Format()} of type {delegateType.Format()}.");
            return dynamicDelegate;
        }
        catch (Exception ex)
        {
            if (throwOnError)
                throw;

            if (LogErrorMessages)
            {
                Logger.DevkitServer.LogError(source, ex, $"Unable to create instance caller for {method.Format()}.");
            }
            return null;
        }
    }

    /// <summary>
    /// Generates a delegate or dynamic method that calls a static method.
    /// </summary>
    /// <param name="allowUnsafeTypeBinding">Enables unsafe type binding to non-matching delegates, meaning classes of different
    /// types can be passed as parameters and an exception will not be thrown (may cause unintended behavior if the wrong type is passed).
    /// This also must be <see langword="true"/> to not null-check instance methods of parameter-less reference types with a dynamic method.</param>
    /// <returns>A delegate of type <see cref="Action"/> or <see cref="Func{T}"/> (or one of their generic counterparts), depending on the method signature.</returns>
    /// <param name="methodName">Name of method that will be called.</param>
    /// <param name="parameters">Optional parameter list for resolving ambiguous methods.</param>
    /// <param name="throwOnError">Throw an error instead of writing to console and returning <see langword="null"/>.</param>
    /// <remarks>Will never return <see langword="null"/> if <paramref name="throwOnError"/> is <see langword="true"/>.</remarks>
    [Pure]
    public static Delegate? GenerateStaticCaller<TDeclaringType>(string methodName, Type[]? parameters = null, bool throwOnError = false, bool allowUnsafeTypeBinding = false)
    {
        const string source = "Accessor.GenerateStaticCaller";
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

                Logger.DevkitServer.LogError(source, $"Unable to find matching static method: {FormattingUtil.FormatMethod(null, typeof(TDeclaringType), methodName, null, parameters, null, isStatic: true)}.");
                return null;
            }
        }

        return GenerateStaticCaller(method, throwOnError, allowUnsafeTypeBinding);
    }

    /// <summary>
    /// Generates a delegate or dynamic method that calls a static method.
    /// </summary>
    /// <param name="allowUnsafeTypeBinding">Enables unsafe type binding to non-matching delegates, meaning classes of different
    /// types can be passed as parameters and an exception will not be thrown (may cause unintended behavior if the wrong type is passed).
    /// This also must be <see langword="true"/> to not null-check instance methods of parameter-less reference types with a dynamic method.</param>
    /// <param name="methodName">Name of method that will be called.</param>
    /// <param name="parameters">Optional parameter list for resolving ambiguous methods.</param>
    /// <param name="throwOnError">Throw an error instead of writing to console and returning <see langword="null"/>.</param>
    /// <remarks>Will never return <see langword="null"/> if <paramref name="throwOnError"/> is <see langword="true"/>.</remarks>
    [Pure]
    public static TDelegate? GenerateStaticCaller<TDeclaringType, TDelegate>(string methodName, bool throwOnError = false, bool allowUnsafeTypeBinding = false, Type[]? parameters = null) where TDelegate : Delegate
    {
        const string source = "Accessor.GenerateStaticCaller";
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
            if (parameters == null)
            {
                ParameterInfo[] paramInfo = GetParameters<TDelegate>();
                parameters = new Type[paramInfo.Length];
                for (int i = 0; i < paramInfo.Length; ++i)
                    parameters[i] = paramInfo[i].ParameterType;
            }
            method = typeof(TDeclaringType).GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
                    null, CallingConventions.Any, parameters, null);
            if (method == null)
            {
                if (throwOnError)
                    throw new Exception($"Unable to find matching static method: {typeof(TDeclaringType).FullName}.{methodName}.");

                Logger.DevkitServer.LogError(source, $"Unable to find matching static method: {FormattingUtil.FormatMethod<TDelegate>(methodName, declTypeOverride: typeof(TDeclaringType), isStatic: true)}.");
                return null;
            }
        }

        return GenerateStaticCaller<TDelegate>(method, throwOnError, allowUnsafeTypeBinding);
    }

    /// <summary>
    /// Generates a delegate or dynamic method that calls a static method.
    /// </summary>
    /// <param name="allowUnsafeTypeBinding">Enables unsafe type binding to non-matching delegates, meaning classes of different
    /// types can be passed as parameters and an exception will not be thrown (may cause unintended behavior if the wrong type is passed).
    /// This also must be <see langword="true"/> to not null-check instance methods of parameter-less reference types with a dynamic method.</param>
    /// <returns>A delegate of type <see cref="Action"/> or <see cref="Func{T}"/> (or one of their generic counterparts), depending on the method signature.</returns>
    /// <param name="method">Method that will be called.</param>
    /// <param name="throwOnError">Throw an error instead of writing to console and returning <see langword="null"/>.</param>
    /// <remarks>Will never return <see langword="null"/> if <paramref name="throwOnError"/> is <see langword="true"/>.</remarks>
    [Pure]
    public static Delegate? GenerateStaticCaller(MethodInfo method, bool throwOnError = false, bool allowUnsafeTypeBinding = false)
    {
        const string source = "Accessor.GenerateStaticCaller";
        if (method == null || !method.IsStatic)
        {
            if (throwOnError)
                throw new Exception("Unable to find static method.");
            Logger.DevkitServer.LogError(source, "Unable to find static method.");
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

            Logger.DevkitServer.LogError(source, $"Method {method.Format()} can not have more than {maxArgs.Format()} arguments!");
            return null;
        }

        Type deleType = GetDefaultDelegate(method.ReturnType, p, null)!;
        return GenerateStaticCaller(deleType, method, throwOnError, allowUnsafeTypeBinding);
    }

    /// <summary>
    /// Generates a delegate or dynamic method that calls a static method.
    /// </summary>
    /// <param name="allowUnsafeTypeBinding">Enables unsafe type binding to non-matching delegates, meaning classes of different
    /// types can be passed as parameters and an exception will not be thrown (may cause unintended behavior if the wrong type is passed).
    /// This also must be <see langword="true"/> to not null-check instance methods of parameter-less reference types with a dynamic method.</param>
    /// <param name="method">Method that will be called.</param>
    /// <param name="throwOnError">Throw an error instead of writing to console and returning <see langword="null"/>.</param>
    /// <remarks>Will never return <see langword="null"/> if <paramref name="throwOnError"/> is <see langword="true"/>.</remarks>
    [Pure]
    public static TDelegate? GenerateStaticCaller<TDelegate>(MethodInfo method, bool throwOnError = false, bool allowUnsafeTypeBinding = false) where TDelegate : Delegate
    {
        return (TDelegate?)GenerateStaticCaller(typeof(TDelegate), method, throwOnError, allowUnsafeTypeBinding);
    }

    /// <summary>
    /// Generates a delegate or dynamic method that calls a static method.
    /// </summary>
    /// <param name="delegateType">Type of delegate to return.</param>
    /// <param name="allowUnsafeTypeBinding">Enables unsafe type binding to non-matching delegates, meaning classes of different
    /// types can be passed as parameters and an exception will not be thrown (may cause unintended behavior if the wrong type is passed).</param>
    /// <param name="method">Method that will be called.</param>
    /// <param name="throwOnError">Throw an error instead of writing to console and returning <see langword="null"/>.</param>
    /// <remarks>Will never return <see langword="null"/> if <paramref name="throwOnError"/> is <see langword="true"/>.</remarks>
    [Pure]
    public static Delegate? GenerateStaticCaller(Type delegateType, MethodInfo method, bool throwOnError = false, bool allowUnsafeTypeBinding = false)
    {
        const string source = "Accessor.GenerateStaticCaller";
        if (!typeof(Delegate).IsAssignableFrom(delegateType))
            throw new ArgumentException(delegateType.FullName + " is not a delegate.", nameof(delegateType));

        if (method == null || !method.IsStatic)
        {
            if (throwOnError)
                throw new Exception($"Unable to find static method for delegate: {delegateType.Name}.");

            if (LogErrorMessages)
                Logger.DevkitServer.LogError(source, $"Unable to find matching static method: {FormattingUtil.FormatMethod(delegateType, "<unknown-name>", isStatic: true)}.");
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
                throw new Exception("Unable to create static caller for " + (method.DeclaringType?.FullName ?? "<unknown-type>") + "." + (method.Name ?? "<unknown-name>") + $": incompatable delegate type: {delegateType.FullName}.");

            if (LogErrorMessages)
                Logger.DevkitServer.LogError(source, $"Unable to create static caller for {method.Format()}: incompatable delegate type: {delegateType.Format()}.");
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
                    if (LogDebugMessages || LogILTraceMessages)
                        Logger.DevkitServer.LogDebug(source, $"Created static delegate caller for {method.Format()} of type {delegateType.Format()}.");
                    return basicDelegate;
                }
                catch (Exception ex)
                {
                    if (LogDebugMessages || LogILTraceMessages)
                    {
                        Logger.DevkitServer.LogDebug(source, $"Unable to create basic delegate binding static caller for {method.DeclaringType?.FullName ?? "<unknown-type>"}.{method.Name}.");
                        Logger.DevkitServer.LogDebug(source, $"{ex.GetType().Format()} - {ex.Message.Format(false)}");
                    }
                }
            }
        }
#if !NET6_0_OR_GREATER // unsafe type binding doesn't work past .NET 5.0
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
#line hidden
                try
                {
                    IntPtr ptr = method.MethodHandle.GetFunctionPointer();
                    // running the debugger here will crash the program so... don't.
                    object d2 = FormatterServices.GetUninitializedObject(delegateType);
                    delegateType.GetConstructors()[0].Invoke(d2, new object[] { null!, ptr });
                    if (LogDebugMessages || LogILTraceMessages)
                        Logger.DevkitServer.LogDebug(source, $"Created static delegate caller for {method.Format()} (using unsafe type binding) of type {delegateType.Format()}.");
                    return (Delegate)d2;
                }
                catch (Exception ex)
                {
                    if (LogDebugMessages || LogILTraceMessages)
                    {
                        Logger.DevkitServer.LogDebug(source, $"Unable to create unsafely binded delegate binding static caller for {method.DeclaringType?.Name ?? "<unknown-type>"}.{method.Name}.");
                        Logger.DevkitServer.LogDebug(source, $"{ex.GetType().Format()} - {ex.Message.Format()}");
                    }
                }
#line default
            }
        }
#endif

        // generate dynamic method as a worst-case scenerio
        Type[] parameterTypes = new Type[delegateParameters.Length];
        for (int i = 0; i < delegateParameters.Length; ++i)
            parameterTypes[i] = delegateParameters[i].ParameterType;

        GetDynamicMethodFlags(true, out MethodAttributes attributes, out CallingConventions convention);
        DynamicMethod dynMethod = new DynamicMethod("Invoke" + method.Name, attributes, convention, delegateReturnType, parameterTypes, method.DeclaringType is not { IsInterface: false } ? typeof(Accessor) : method.DeclaringType, true);

        for (int i = 0; i < p.Length; ++i)
            dynMethod.DefineParameter(i + 1, p[i].Attributes, p[i].Name);

        IOpCodeEmitter generator = CreateEmitter(dynMethod, source);

        for (int i = 0; i < p.Length; ++i)
            generator.EmitParameter(LogILTraceMessages ? source : null, i, $"Invalid argument type passed to static caller for {method.DeclaringType?.Name ?? "<unknown-type>"}.{method.Name} at parameter {i.ToString(CultureInfo.InvariantCulture)} ({p[i].Name}). " +
                                                                           $"Expected {p[i].ParameterType.FullName}.", false, type: parameterTypes[i], p[i].ParameterType);

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
            if (LogDebugMessages)
                Logger.DevkitServer.LogDebug(source, $"Created dynamic static caller for {method.Format()} of type {delegateType.Format()}.");
            return dynamicDelegate;
        }
        catch (Exception ex)
        {
            if (throwOnError)
                throw new Exception($"Unable to create static caller for {method.DeclaringType?.FullName ?? "<unknown-type>"}.{method.Name}.", ex);

            if (LogErrorMessages)
                Logger.DevkitServer.LogError(source, ex, $"Unable to create static caller for {method.Format()}.");
            
            return null;
        }
    }

    /// <summary>
    /// Gets platform-specific flags for creating dynamic methods.
    /// </summary>
    /// <param name="static">Whether or not the method has no 'instance', only considered when on mono.</param>
    /// <param name="attributes">Method attributes to pass to <see cref="DynamicMethod"/> constructor.</param>
    /// <param name="convention">Method convention to pass to <see cref="DynamicMethod"/> constructor.</param>
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
    internal static IEnumerable<CodeInstruction> AddIsEditorCall([InstantHandle] IEnumerable<CodeInstruction> instructions, MethodBase __method)
    {
        if (IsServerGetter == null || IsEditorGetter == null)
        {
            foreach (CodeInstruction instr in instructions)
                yield return instr;

            yield break;
        }
        foreach (CodeInstruction instr in instructions)
        {
            if (instr.Calls(IsServerGetter))
            {
                yield return instr;
                yield return new CodeInstruction(OpCodes.Not);
                yield return new CodeInstruction(IsEditorGetter.GetCallRuntime(), IsEditorGetter);
                yield return new CodeInstruction(OpCodes.Or);
                yield return new CodeInstruction(OpCodes.Not);
                if (__method != null)
                    Logger.DevkitServer.LogDebug("Accessor.AddIsEditorCall", $"Inserted editor call to {__method.Format()}.");
                else
                    Logger.DevkitServer.LogDebug("Accessor.AddIsEditorCall", "Inserted editor call to unknown method.");
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
            Logger.DevkitServer.LogError("Accessor.AddFunctionStepthrough", "Error adding function stepthrough to method, not found.");
            return false;
        }
        try
        {
            PatchesMain.Patcher.Patch(method,
                transpiler: new HarmonyMethod(typeof(Accessor).GetMethod(nameof(AddFunctionStepthroughTranspiler),
                    BindingFlags.NonPublic | BindingFlags.Static)));
            Logger.DevkitServer.LogInfo("Accessor.AddFunctionStepthrough", $"Added stepthrough to: {method.Format()}.");
            return true;
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("Accessor.AddFunctionStepthrough", ex, $"Error adding function stepthrough to {method.Format()}.");
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
            Logger.DevkitServer.LogError("Accessor.AddFunctionIOLogging", "Error adding function IO logging to method, not found.");
            return false;
        }
        try
        {
            PatchesMain.Patcher.Patch(method,
                transpiler: new HarmonyMethod(typeof(Accessor).GetMethod(nameof(AddFunctionIOTranspiler),
                    BindingFlags.NonPublic | BindingFlags.Static)));
            Logger.DevkitServer.LogInfo("Accessor.AddFunctionIOLogging", $"Added function IO logging to: {method.Format()}.");
            return true;
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("Accessor.AddFunctionIOLogging", ex, $"Error adding function IO logging to {method.Format()}.");
            return false;
        }
    }

    private static IEnumerable<CodeInstruction> AddFunctionIOTranspiler([InstantHandle] IEnumerable<CodeInstruction> instructions, MethodBase method)
    {
        yield return new CodeInstruction(OpCodes.Ldstr, "In method: " + method.Format() + " (basic entry)");
        yield return new CodeInstruction(OpCodes.Ldc_I4, (int)ConsoleColor.Green);
        yield return new CodeInstruction(LogInfo.GetCallRuntime(), LogInfo);

        foreach (CodeInstruction instr in instructions)
        {
            if (instr.opcode == OpCodes.Ret || instr.opcode == OpCodes.Throw)
            {
                CodeInstruction logInstr = new CodeInstruction(OpCodes.Ldstr, "Out method: " + method.Format() + (instr.opcode == OpCodes.Ret ? " (returned)" : " (exception)"));
                PatchUtil.TransferStartingInstructionNeeds(instr, logInstr);
                yield return logInstr;
                yield return new CodeInstruction(OpCodes.Ldc_I4, (int)ConsoleColor.Green);
                yield return new CodeInstruction(LogInfo.GetCallRuntime(), LogInfo);
            }
            yield return instr;
        }
    }
    private static IEnumerable<CodeInstruction> AddFunctionStepthroughTranspiler([InstantHandle] IEnumerable<CodeInstruction> instructions, MethodBase method)
    {
        List<CodeInstruction> ins = [..instructions];
        AddFunctionStepthrough(ins, method);
        return ins;
    }
    internal static void AddFunctionStepthrough(List<CodeInstruction> ins, MethodBase method)
    {
        ins.Insert(0, new CodeInstruction(OpCodes.Ldstr, "Stepping through Method: " + method.Format() + ":"));
        ins.Insert(1, new CodeInstruction(OpCodes.Ldc_I4, (int)ConsoleColor.Green));
        ins.Insert(2, new CodeInstruction(LogInfo.GetCallRuntime(), LogInfo));
        ins[0].WithStartingInstructionNeeds(ins[3]);
        for (int i = 3; i < ins.Count; i++)
        {
            CodeInstruction instr = ins[i];
            CodeInstruction? start = null;
            foreach (ExceptionBlock block in instr.blocks)
            {
                CodeInstruction blockInst = new CodeInstruction(OpCodes.Ldstr, "  " + block.Format());
                start ??= blockInst;
                ins.Insert(i, blockInst);
                ins.Insert(i + 1, new CodeInstruction(OpCodes.Ldc_I4, (int)ConsoleColor.Green));
                ins.Insert(i + 2, new CodeInstruction(LogInfo.GetCallRuntime(), LogInfo));
                i += 3;
            }

            foreach (Label label in instr.labels)
            {
                CodeInstruction lblInst = new CodeInstruction(OpCodes.Ldstr, "  " + label.Format() + ":");
                start ??= lblInst;
                ins.Insert(i, lblInst);
                ins.Insert(i + 1, new CodeInstruction(OpCodes.Ldc_I4, (int)ConsoleColor.Green));
                ins.Insert(i + 2, new CodeInstruction(LogInfo.GetCallRuntime(), LogInfo));
                i += 3;
            }

            CodeInstruction mainInst = new CodeInstruction(OpCodes.Ldstr, "  " + instr.Format());
            start ??= mainInst;
            ins.Insert(i, mainInst);
            ins.Insert(i + 1, new CodeInstruction(OpCodes.Ldc_I4, (int)ConsoleColor.Green));
            ins.Insert(i + 2, new CodeInstruction(LogInfo.GetCallRuntime(), LogInfo));
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
    /// Checks for the the attribute of type <typeparamref name="TAttribute"/> on <paramref name="member"/>.
    /// </summary>
    /// <remarks>Implementation of <see cref="ICustomAttributeProvider.IsDefined"/>.</remarks>
    [Pure]
    public static bool HasAttributeSafe<TAttribute>(this ICustomAttributeProvider member, bool inherit = false) where TAttribute : Attribute => member.HasAttributeSafe(typeof(TAttribute), inherit);

    /// <summary>
    /// Checks for the attribute of type <paramref name="attributeType"/> on <paramref name="member"/>.
    /// </summary>
    /// <param name="member">Member to check for attributes. This can be <see cref="Module"/>, <see cref="Assembly"/>, <see cref="MemberInfo"/>, or <see cref="ParameterInfo"/>.</param>
    /// <param name="attributeType">Type of the attribute to check for.</param>
    /// <param name="inherit">Also check parent members.</param>
    /// <exception cref="ArgumentException"><paramref name="attributeType"/> did not derive from <see cref="Attribute"/>.</exception>
    /// <remarks>Implementation of <see cref="ICustomAttributeProvider.IsDefined"/>.</remarks>
    [Pure]
    public static bool HasAttributeSafe(this ICustomAttributeProvider member, Type attributeType, bool inherit = false)
    {
        try
        {
            return member.IsDefined(attributeType, inherit);
        }
        catch (TypeLoadException ex)
        {
            if (LogDebugMessages)
                LogTypeLoadException(ex, "Accessor.HasAttributeSafe", $"Failed to check {member.Format()} for attribute {attributeType.Format()}.");

            return false;
        }
        catch (FileNotFoundException ex)
        {
            if (LogDebugMessages)
                LogFileNotFoundException(ex, "Accessor.HasAttributeSafe", $"Failed to check {member.Format()} for attribute {attributeType.Format()}.");

            return false;
        }
    }

    /// <summary>
    /// Checks for and returns the the attribute of type <typeparamref name="TAttribute"/> on <paramref name="member"/>.
    /// </summary>
    /// <param name="inherit">Also check parent members.</param>
    /// <param name="member">Member to check for attributes. This can be <see cref="Module"/>, <see cref="Assembly"/>, <see cref="MemberInfo"/>, or <see cref="ParameterInfo"/>.</param>
    /// <typeparam name="TAttribute">Type of the attribute to check for.</typeparam>
    /// <exception cref="AmbiguousMatchException">There are more than one attributes of type <typeparamref name="TAttribute"/>.</exception>
    /// <remarks>Implementation of <see cref="Attribute.GetCustomAttribute(MemberInfo, Type)"/>.</remarks>
    [Pure]
    public static TAttribute? GetAttributeSafe<TAttribute>(this ICustomAttributeProvider member, bool inherit = false) where TAttribute : Attribute
        => member.GetAttributeSafe(typeof(TAttribute), inherit) as TAttribute;

    /// <summary>
    /// Checks for and returns the attribute of type <paramref name="attributeType"/> on <paramref name="member"/>.
    /// </summary>
    /// <param name="member">Member to check for attributes. This can be <see cref="Module"/>, <see cref="Assembly"/>, <see cref="MemberInfo"/>, or <see cref="ParameterInfo"/>.</param>
    /// <param name="attributeType">Type of the attribute to check for.</param>
    /// <param name="inherit">Also check parent members.</param>
    /// <exception cref="ArgumentException"><paramref name="attributeType"/> did not derive from <see cref="Attribute"/>.</exception>
    /// <exception cref="AmbiguousMatchException">There are more than one attributes of type <paramref name="attributeType"/>.</exception>
    /// <remarks>Implementation of <see cref="Attribute.GetCustomAttribute(MemberInfo, Type)"/>.</remarks>
    [Pure]
    public static Attribute? GetAttributeSafe(this ICustomAttributeProvider member, Type attributeType, bool inherit = false)
    {
        try
        {
            switch (member)
            {
                case MemberInfo memberInfo:
                    return Attribute.GetCustomAttribute(memberInfo, attributeType, inherit);
                case Module module:
                    return Attribute.GetCustomAttribute(module, attributeType, inherit);
                case Assembly assembly:
                    return Attribute.GetCustomAttribute(assembly, attributeType, inherit);
                case ParameterInfo parameterInfo:
                    return Attribute.GetCustomAttribute(parameterInfo, attributeType, inherit);
                default:
                    object[] attributes = member.GetCustomAttributes(attributeType, inherit);
                    if (attributes is not { Length: > 0 })
                        return null;
                    if (attributes.Length > 1)
                        throw new AmbiguousMatchException($"Multiple attributes of type {attributeType.FullName}.");

                    return attributes[0] as Attribute;
            }
        }
        catch (TypeLoadException ex)
        {
            if (LogDebugMessages)
                LogTypeLoadException(ex, "Accessor.GetAttributeSafe", $"Failed to get an attribute of type {attributeType.Format()} from {member.Format()}.");

            return null;
        }
        catch (FileNotFoundException ex)
        {
            if (LogDebugMessages)
                LogFileNotFoundException(ex, "Accessor.GetAttributeSafe", $"Failed to get an attribute of type {attributeType.Format()} from {member.Format()}.");

            return null;
        }
    }

    /// <summary>
    /// Checks for and returns the the attribute of type <typeparamref name="TAttribute"/> on <paramref name="member"/>.
    /// </summary>
    /// <param name="inherit">Also check parent members.</param>
    /// <param name="member">Member to check for attributes. This can be <see cref="Module"/>, <see cref="Assembly"/>, <see cref="MemberInfo"/>, or <see cref="ParameterInfo"/>.</param>
    /// <typeparam name="TAttribute">Type of the attribute to check for.</typeparam>
    /// <remarks>Implementation of <see cref="ICustomAttributeProvider.GetCustomAttributes(Type, bool)"/>.</remarks>
    [Pure]
    public static TAttribute[] GetAttributesSafe<TAttribute>(this ICustomAttributeProvider member, bool inherit = false) where TAttribute : Attribute
        => (TAttribute[])member.GetAttributesSafe(typeof(TAttribute), inherit);

    /// <summary>
    /// Checks for and returns the attribute of type <paramref name="attributeType"/> on <paramref name="member"/>.
    /// </summary>
    /// <param name="member">Member to check for attributes. This can be <see cref="Module"/>, <see cref="Assembly"/>, <see cref="MemberInfo"/>, or <see cref="ParameterInfo"/>.</param>
    /// <param name="attributeType">Type of the attribute to check for.</param>
    /// <param name="inherit">Also check parent members.</param>
    /// <exception cref="ArgumentException"><paramref name="attributeType"/> did not derive from <see cref="Attribute"/>.</exception>
    /// <remarks>Implementation of <see cref="ICustomAttributeProvider.GetCustomAttributes(Type, bool)"/>.</remarks>
    [Pure]
    public static Attribute[] GetAttributesSafe(this ICustomAttributeProvider member, Type attributeType, bool inherit = false)
    {
        try
        {
            object[] array = member.GetCustomAttributes(attributeType, inherit);
            if (array is { Length: > 0 })
            {
                if (array.GetType().GetElementType() == attributeType)
                    return (Attribute[])array;

                int ct = 0;
                for (int i = 0; i < array.Length; ++i)
                {
                    if (array[i] is Attribute attr && attributeType.IsInstanceOfType(attr))
                        ++ct;
                }

                Attribute[] array2 = (Attribute[])Array.CreateInstance(attributeType, ct);
                for (int i = 0; i < array.Length; ++i)
                {
                    if (array[i] is Attribute attr && attributeType.IsInstanceOfType(attr))
                        array2[array2.Length - --ct - 1] = attr;
                }

                return array2;
            }
        }
        catch (TypeLoadException ex)
        {
            if (LogDebugMessages)
                LogTypeLoadException(ex, "Accessor.GetAttributesSafe", $"Failed to get attributes of type {attributeType.Format()} from {member.Format()}.");
        }
        catch (FileNotFoundException ex)
        {
            if (LogDebugMessages)
                LogFileNotFoundException(ex, "Accessor.GetAttributesSafe", $"Failed to get attributes of type {attributeType.Format()} from {member.Format()}.");
        }

        return attributeType == typeof(Attribute)
#if NET461_OR_GREATER || !NETFRAMEWORK
            ? Array.Empty<Attribute>()
#else
            ? new Attribute[0]
#endif
            : (Attribute[])Array.CreateInstance(attributeType, 0);
    }

    /// <summary>
    /// Checks for and outputs the the attribute of type <typeparamref name="TAttribute"/> on <paramref name="member"/>.
    /// </summary>
    /// <param name="inherit">Also check parent members.</param>
    /// <param name="member">Member to check for attributes. This can be <see cref="Module"/>, <see cref="Assembly"/>, <see cref="MemberInfo"/>, or <see cref="ParameterInfo"/>.</param>
    /// <typeparam name="TAttribute">Type of the attribute to check for.</typeparam>
    /// <returns><see langword="true"/> if the attribute was found, otherwise <see langword="false"/>.</returns>
    /// <remarks>Implementation of <see cref="ICustomAttributeProvider.GetCustomAttribute"/>.</remarks>
    [Pure]
    public static bool TryGetAttributeSafe<TAttribute>(this ICustomAttributeProvider member, out TAttribute attribute, bool inherit = false) where TAttribute : Attribute
    {
        attribute = (member.GetAttributeSafe(typeof(TAttribute), inherit) as TAttribute)!;
        return attribute != null;
    }
    private static void LogTypeLoadException(TypeLoadException ex, string source, string context)
    {
        string msg = context + $" Can't load type: {ex.TypeName}.";
        if (ex.InnerException != null)
            msg += "(" + ex.InnerException.GetType().Name + " | " + ex.InnerException.Message + ")";
        Logger.DevkitServer.LogDebug(source, msg);
    }
    private static void LogFileNotFoundException(FileNotFoundException ex, string source, string context)
    {
        string msg = context + $" Missing assembly: {ex.FileName}.";
        Logger.DevkitServer.LogDebug(source, msg);
    }
#pragma warning disable CS0419

    /// <summary>
    /// Checks for the <see cref="IsReadOnlyAttribute"/> on <paramref name="member"/>, which signifies the readonly value.
    /// <remarks>This behavior is overridden on fields to check <see cref="FieldInfo.IsInitOnly"/>.</remarks>
    /// </summary>
    [Pure]
    public static bool IsReadOnly(this ICustomAttributeProvider member)
    {
        if (member is FieldInfo field)
        {
            return field.IsInitOnly;
        }
        return member.HasAttributeSafe(_readonlyAttribute ??= typeof(IsReadOnlyAttribute));
    }

#pragma warning restore CS0419

    /// <summary>
    /// Checks for the <see cref="IgnoreAttribute"/> on <paramref name="member"/>.
    /// </summary>
    /// <param name="inherit">Also check parent members.</param>
    [Pure]
    public static bool IsIgnored(this ICustomAttributeProvider member, bool inherit = false) => member.HasAttributeSafe(_ignoreAttribute ??= typeof(IgnoreAttribute), inherit);
    private static bool IsIgnored(this Type type) => type.HasAttributeSafe(_ignoreAttribute ??= typeof(IgnoreAttribute));

    /// <summary>
    /// Checks for the <see cref="LoadPriorityAttribute"/> on <paramref name="member"/> and returns the priority (or zero if not found).
    /// </summary>
    [Pure]
    public static int GetPriority(this ICustomAttributeProvider member, bool inherit = true) => member.GetAttributeSafe(_priorityAttribute ??= typeof(LoadPriorityAttribute), inherit) is LoadPriorityAttribute attr ? attr.Priority : 0;
    
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
    /// <returns>A method info of a passed delegate.</returns>
    [Pure]
    public static MethodInfo? GetMethod([InstantHandle] Delegate @delegate)
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

    /// <summary>
    /// Safely gets the reflection method info of the passed method. Works best with static methods.<br/><br/>
    /// <code>
    /// HarmonyMethod? method = Accessor.GetHarmonyMethod(Guid.Parse);
    /// </code>
    /// </summary>
    /// <returns>A harmony method info of a passed delegate.</returns>
    [Pure]
    public static HarmonyMethod? GetHarmonyMethod([InstantHandle] Delegate @delegate)
    {
        MethodInfo? method = GetMethod(@delegate);
        return method == null ? null : new HarmonyMethod(method);
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
                p2[^1] = returnType;
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
                p2[^1] = returnType;
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
    /// <param name="type">Highest (most derived) type in the hierarchy.</param>
    /// <param name="action">Called optionally for <paramref name="type"/>, then for each base type in order from most related to least related.</param>
    /// <param name="includeParent">Call <paramref name="action"/> on <paramref name="type"/>. Overrides <paramref name="excludeSystemBase"/>.</param>
    /// <param name="excludeSystemBase">Excludes calling <paramref name="action"/> for <see cref="object"/> or <see cref="ValueType"/>.</param>
    public static void ForEachBaseType(this Type type, [InstantHandle] ForEachBaseType action, bool includeParent = true, bool excludeSystemBase = true)
    {
        Type? type2 = type;
        if (includeParent)
        {
            action(type2, 0);
        }

        type2 = type.BaseType;

        int level = 0;
        for (; type2 != null && (!excludeSystemBase || type2 != typeof(object) && type2 != typeof(ValueType)); type2 = type2.BaseType)
        {
            ++level;
            action(type2, level);
        }
    }

    /// <summary>
    /// Used to perform a repeated <paramref name="action"/> for each base type of a <paramref name="type"/>.
    /// </summary>
    /// <remarks>Execution can be broken by returning <see langword="false"/>.</remarks>
    /// <param name="type">Highest (most derived) type in the hierarchy.</param>
    /// <param name="action">Called optionally for <paramref name="type"/>, then for each base type in order from most related to least related.</param>
    /// <param name="includeParent">Call <paramref name="action"/> on <paramref name="type"/>. Overrides <paramref name="excludeSystemBase"/>.</param>
    /// <param name="excludeSystemBase">Excludes calling <paramref name="action"/> for <see cref="object"/> or <see cref="ValueType"/>.</param>
    public static void ForEachBaseType(this Type type, [InstantHandle] ForEachBaseTypeWhile action, bool includeParent = true, bool excludeSystemBase = true)
    {
        Type? type2 = type;
        if (includeParent)
        {
            if (!action(type2, 0))
                return;
        }

        type2 = type.BaseType;

        int level = 0;
        for (; type2 != null && (!excludeSystemBase || type2 != typeof(object) && type2 != typeof(ValueType)); type2 = type2.BaseType)
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
        List<Type?> types;
        bool removeNulls = false;
        try
        {
            types = [..assembly.GetTypes()];
        }
        catch (FileNotFoundException ex)
        {
            if (LogDebugMessages)
                LogFileNotFoundException(ex, "Accessor.GetTypesSafe", $"Unable to get any types from assembly {assembly.FullName.Format()}.");

            return [];
        }
        catch (ReflectionTypeLoadException ex)
        {
            if (LogDebugMessages && ex.LoaderExceptions != null)
            {
                for (int i = 0; i < ex.LoaderExceptions.Length; ++i)
                {
                    if (ex.LoaderExceptions[i] is not TypeLoadException tle)
                        continue;
                    
                    LogTypeLoadException(tle, "Accessor.GetTypesSafe", "Skipped type.");
                }
            }
            types = ex.Types == null ? [] : [..ex.Types];
            removeNulls = true;
        }

        if (removeNulls)
        {
            if (removeIgnored)
                types.RemoveAll(x => x is null || IsIgnored(x));
            else
                types.RemoveAll(x => x is null);
        }
        else if (removeIgnored)
            types.RemoveAll(IsIgnored!);

        types.Sort(SortTypesByPriorityHandler!);
        return types!;
    }

    /// <returns>Every type defined in the provided <paramref name="assmeblies"/>.</returns>
    [Pure]
    public static List<Type> GetTypesSafe([InstantHandle] IEnumerable<Assembly> assmeblies, bool removeIgnored = false)
    {
        List<Type?> types = new List<Type?>();
        bool removeNulls = false;
        foreach (Assembly assembly in assmeblies)
        {
            try
            {
                types.AddRange(assembly.GetTypes());
            }
            catch (FileNotFoundException ex)
            {
                if (LogDebugMessages)
                    LogFileNotFoundException(ex, "Accessor.GetTypesSafe", $"Unable to get any types from assembly \"{assembly.FullName}\".");
            }
            catch (ReflectionTypeLoadException ex)
            {
                if (LogDebugMessages && ex.LoaderExceptions != null)
                {
                    for (int i = 0; i < ex.LoaderExceptions.Length; ++i)
                    {
                        if (ex.LoaderExceptions[i] is not TypeLoadException tle)
                            continue;

                        LogTypeLoadException(tle, "Accessor.GetTypesSafe", "Skipped type.");
                    }
                }
                if (ex.Types != null)
                    types.AddRange(ex.Types);
                removeNulls = true;
            }
        }

        if (removeNulls)
        {
            if (removeIgnored)
                types.RemoveAll(x => x is null || IsIgnored(x));
            else
                types.RemoveAll(x => x is null);
        }
        else if (removeIgnored)
            types.RemoveAll(IsIgnored!);

        types.Sort(SortTypesByPriorityHandler!);
        return types!;
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

        // default implementation
        if (interfaceMethod.IsVirtual)
            return interfaceMethod;

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
    /// <exception cref="NotSupportedException">Reflection failure.</exception>
    [Pure]
    public static Type GetReturnType(Type delegateType)
    {
        if (!typeof(Delegate).IsAssignableFrom(delegateType))
            throw new ArgumentException(delegateType.Name + " is not a delegate type.", nameof(delegateType));

        return delegateType.GetMethod("Invoke", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.ReturnType
               ?? throw new NotSupportedException($"Unable to find Invoke method in delegate {delegateType.Name}.");
    }

    /// <summary>
    /// Gets the parameters of a <paramref name="delegateType"/>.
    /// </summary>
    /// <exception cref="NotSupportedException">Reflection failure.</exception>
    [Pure]
    public static ParameterInfo[] GetParameters(Type delegateType)
    {
        if (!typeof(Delegate).IsAssignableFrom(delegateType))
            throw new ArgumentException(delegateType.Name + " is not a delegate type.", nameof(delegateType));

        return delegateType.GetMethod("Invoke", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetParameters()
               ?? throw new NotSupportedException($"Unable to find Invoke method in delegate {delegateType.Name}.");
    }

    /// <summary>
    /// Gets the (cached) <see langword="Invoke"/> method of a <paramref name="delegateType"/>. All delegates have one by default.
    /// </summary>
    /// <exception cref="NotSupportedException">Reflection failure.</exception>
    [Pure]
    public static MethodInfo GetInvokeMethod(Type delegateType)
    {
        if (!typeof(Delegate).IsAssignableFrom(delegateType))
            throw new ArgumentException(delegateType.Name + " is not a delegate type.", nameof(delegateType));

        return delegateType.GetMethod("Invoke", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
               ?? throw new NotSupportedException($"Unable to find Invoke method in delegate {delegateType.Name}.");
    }

    /// <summary>
    /// Get the value of a field or property.
    /// </summary>
    /// <exception cref="NotSupportedException">Property does not have a getter.</exception>
    /// <exception cref="ArgumentException">Not a field or property.</exception>
    public static object? Get(this MemberInfo member, object? instance)
    {
        if (member is PropertyInfo property)
        {
            MethodInfo? getter = property.GetGetMethod(true);
            if (getter == null)
                throw new NotSupportedException(property.Name + " does not have a getter.");
            return getter.Invoke(instance, Array.Empty<object>());
        }

        if (member is FieldInfo field)
            return field.GetValue(instance);

        throw new ArgumentException("Member must be a FieldInfo or PropertyInfo.", nameof(member));
    }

    /// <summary>
    /// Set the value of a field or property.
    /// </summary>
    /// <exception cref="NotSupportedException">Property does not have a setter.</exception>
    /// <exception cref="ArgumentException">Not a field or property.</exception>
    public static void Set(this MemberInfo member, object? instance, object? value)
    {
        if (member is PropertyInfo property)
        {
            MethodInfo? setter = property.GetSetMethod(true);
            if (setter == null)
                throw new NotSupportedException(property.Name + " does not have a setter.");

            setter.Invoke(instance, new object?[] { value });
        }

        if (member is FieldInfo field)
            field.SetValue(instance, value);

        throw new ArgumentException("Member must be a FieldInfo or PropertyInfo.", nameof(member));
    }

    /// <summary>
    /// Get the 'type' of a member, returns <see cref="FieldInfo.FieldType"/> or <see cref="PropertyInfo.PropertyType"/> or
    /// <see cref="MethodInfo.ReturnType"/> or <see cref="EventInfo.EventHandlerType"/> or <see cref="MemberInfo.DeclaringType"/> for constructors.
    /// </summary>
    [Pure]
    public static Type? GetMemberType(this MemberInfo member) => member switch
    {
        MethodInfo a => a.ReturnType,
        FieldInfo a => a.FieldType,
        PropertyInfo a => a.PropertyType,
        ConstructorInfo a => a.DeclaringType,
        EventInfo a => a.EventHandlerType,
        _ => throw new ArgumentException($"Member type {member.GetType().Name} does not have a member type.", nameof(member))
    };

    /// <summary>
    /// Check any member for being static.
    /// </summary>
    [Pure]
    public static bool GetIsStatic(this MemberInfo member) => member switch
    {
        MethodBase a => a.IsStatic,
        FieldInfo a => a.IsStatic,
        PropertyInfo a => a.GetGetMethod(true) is { } getter ? getter.IsStatic : a.GetSetMethod(true) is { IsStatic: true },
        EventInfo a => a.GetAddMethod(true) is { } adder ? adder.IsStatic : a.GetRemoveMethod(true) is { } remover ? remover.IsStatic : a.GetRaiseMethod(true) is { IsStatic: true },
        Type a => (a.Attributes & (TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit | TypeAttributes.Class)) != 0,
        _ => throw new ArgumentException($"Member type {member.GetType().Name} is not static-able.", nameof(member))
    };

    /// <summary>
    /// Decide if a method should be callvirt'd instead of call'd. Usually you will use <see cref="ShouldCallvirtRuntime"/> instead as it doesn't account for possible future keyword changes.
    /// </summary>
    /// <remarks>Note that not using call instead of callvirt may remove the check for a null instance.</remarks>
    [Pure]
    public static bool ShouldCallvirt(this MethodBase method)
    {
        return method is { IsFinal: false, IsVirtual: true } || method.IsAbstract || method is { IsStatic: false, DeclaringType: not { IsValueType: true }, IsFinal: false } || method.DeclaringType is { IsInterface: true };
    }

    /// <summary>
    /// Decide if a method should be callvirt'd instead of call'd at runtime. Doesn't account for future changes.
    /// </summary>
    /// <remarks>Note that not using call instead of callvirt may remove the check for a null instance.</remarks>
    [Pure]
    public static bool ShouldCallvirtRuntime(this MethodBase method)
    {
        return method is { IsFinal: false, IsVirtual: true } || method.IsAbstract || method.DeclaringType is { IsInterface: true };
    }

    /// <summary>
    /// Get the underlying array from a list.
    /// </summary>
    /// <exception cref="NotSupportedException">Reflection failure.</exception>
    /// <exception cref="ArgumentNullException"/>
    [Pure]
    public static TElementType[] GetUnderlyingArray<TElementType>(this List<TElementType> list) => ListInfo<TElementType>.GetUnderlyingArray(list);

    /// <summary>
    /// Get the underlying array from a list, or in the case of a reflection failure calls <see cref="List{TElementType}.ToArray"/> on <paramref name="list"/> and returns that.
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
    [Pure]
    public static TElementType[] GetUnderlyingArrayOrCopy<TElementType>(this List<TElementType> list) => ListInfo<TElementType>.TryGetUnderlyingArray(list, out TElementType[] array) ? array : list.ToArray();

    /// <summary>
    /// Get the version of a list, which is incremented each time the list is updated.
    /// </summary>
    /// <exception cref="NotSupportedException">Reflection failure.</exception>
    /// <exception cref="ArgumentNullException"/>
    [Pure]
    public static int GetListVersion<TElementType>(this List<TElementType> list) => ListInfo<TElementType>.GetListVersion(list);

    /// <summary>
    /// Get the underlying array from a list.
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
    public static bool TryGetUnderlyingArray<TElementType>(List<TElementType> list, out TElementType[] underlyingArray) => ListInfo<TElementType>.TryGetUnderlyingArray(list, out underlyingArray);

    /// <summary>
    /// Get the version of a list, which is incremented each time the list is updated.
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
    public static bool TryGetListVersion<TElementType>(List<TElementType> list, out int version) => ListInfo<TElementType>.TryGetListVersion(list, out version);

    /// <summary>
    /// Checks if it's possible for a variable of type <paramref name="actualType"/> to have a value of type <paramref name="queriedType"/>. 
    /// </summary>
    /// <returns><see langword="true"/> if <paramref name="actualType"/> is assignable from <paramref name="queriedType"/> or if <paramref name="queriedType"/> is assignable from <paramref name="actualType"/>.</returns>
    public static bool CouldBeAssignedTo(this Type actualType, Type queriedType) => actualType.IsAssignableFrom(queriedType) || queriedType.IsAssignableFrom(actualType);

    /// <summary>
    /// Checks if it's possible for a variable of type <paramref name="actualType"/> to have a value of type <typeparamref name="T"/>. 
    /// </summary>
    /// <returns><see langword="true"/> if <paramref name="actualType"/> is assignable from <typeparamref name="T"/> or if <typeparamref name="T"/> is assignable from <paramref name="actualType"/>.</returns>
    public static bool CouldBeAssignedTo<T>(this Type actualType) => actualType.CouldBeAssignedTo(typeof(T));

    /// <summary>
    /// Tries to find a lambda method that's defined in <paramref name="definingMethod"/>. Optinally define a type and parameter array to be more specific.
    /// </summary>
    /// <remarks>The effectiveness of this method depends on the how the compiler of the original code implements lambda methods.</remarks>
    public static bool TryGetLambdaMethod(MethodInfo definingMethod, out MethodInfo method, Type[]? types = null, ParameterModifier[]? parameters = null)
    {
        method = null!;

        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance;

        Type? proxyType = definingMethod.DeclaringType?.GetNestedType("<>c", flags) ?? definingMethod.DeclaringType;

        if (proxyType == null)
            return false;

        if (LogDebugMessages)
            Logger.DevkitServer.LogDebug(nameof(TryGetLambdaMethod), $"Looking for lambda match in {proxyType.Format()}.");

        string methodName = "<" + definingMethod.Name + ">";

        MethodInfo[] methods = proxyType.GetMethods(flags);

        List<MethodBase> matches = new List<MethodBase>(3);
        for (int i = 0; i < methods.Length; ++i)
        {
            if (!methods[i].Name.Contains(methodName, StringComparison.Ordinal))
                continue;

            matches.Add(methods[i]);
        }

        if (matches.Count == 0)
        {
            if (LogDebugMessages)
                Logger.DevkitServer.LogDebug(nameof(TryGetLambdaMethod), "No match when binding to lambda method.");
            return false;
        }

        if (types == null)
        {
            method = (matches[0] as MethodInfo)!;
            if (matches.Count == 1)
                return true;

            if (LogDebugMessages)
                Logger.DevkitServer.LogDebug(nameof(TryGetLambdaMethod), "Ambiguous match when binding to lambda method. Consider passing types.");
            
            return false;
        }

        try
        {
            method = (Type.DefaultBinder!.SelectMethod(flags, matches.ToArray(), types, parameters) as MethodInfo)!;
            return method != null;
        }
        catch (Exception ex)
        {
            if (LogDebugMessages)
                Logger.DevkitServer.LogDebug(nameof(TryGetLambdaMethod), ex, "Exception binding to methods.");
        }

        return false;
    }

    private static class DelegateInfo<TDelegate> where TDelegate : Delegate
    {
        public static MethodInfo InvokeMethod { get; }
        public static ParameterInfo[] Parameters { get; }
        public static Type ReturnType { get; }
        static DelegateInfo()
        {
            InvokeMethod = typeof(TDelegate).GetMethod("Invoke", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
            if (InvokeMethod == null)
                throw new NotSupportedException($"Unable to find Invoke method in delegate {typeof(TDelegate).Name}.");

            Parameters = InvokeMethod.GetParameters();
            ReturnType = InvokeMethod.ReturnType;
        }
    }
    internal static void CheckExceptionConstructors()
    {
        if (!_castExCtorCalc)
        {
            _castExCtorCalc = true;
            CastExCtor = typeof(InvalidCastException).GetConstructor(BindingFlags.Public | BindingFlags.Instance, null, new Type[] { typeof(string) }, null)!;
        }
        if (!_nreExCtorCalc)
        {
            _nreExCtorCalc = true;
            NreExCtor = typeof(NullReferenceException).GetConstructor(BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null)!;
        }
    }
    private static class ListInfo<TElementType>
    {
        private static InstanceGetter<List<TElementType>, TElementType[]>? _underlyingArrayGetter;
        private static InstanceGetter<List<TElementType>, int>? _versionGetter;
        private static bool _checkUndArr;
        private static bool _checkVer;
        private static bool CheckUndArr()
        {
            if (!_checkUndArr)
            {
                _underlyingArrayGetter = GenerateInstanceGetter<List<TElementType>, TElementType[]>("_items", false);
                _checkUndArr = true;
            }
            return _underlyingArrayGetter != null;
        }
        private static bool CheckVer()
        {
            if (!_checkVer)
            {
                _versionGetter = GenerateInstanceGetter<List<TElementType>, int>("_version", true);
                _checkVer = true;
            }
            return _versionGetter != null;
        }
        public static TElementType[] GetUnderlyingArray(List<TElementType> list)
        {
            if (list == null)
                throw new ArgumentNullException(nameof(list));

            if (_underlyingArrayGetter != null || CheckUndArr())
                return _underlyingArrayGetter!(list);

            throw new NotSupportedException($"Unable to find '_items' in {list.GetType().Format()}.");
        }
        public static int GetListVersion(List<TElementType> list)
        {
            if (list == null)
                throw new ArgumentNullException(nameof(list));

            if (_versionGetter != null || CheckVer())
                return _versionGetter!(list);

            throw new NotSupportedException($"Unable to find '_version' in {list.GetType().Format()}.");
        }
        public static bool TryGetUnderlyingArray(List<TElementType> list, out TElementType[] underlyingArray)
        {
            if (list == null)
                throw new ArgumentNullException(nameof(list));

            if (_underlyingArrayGetter == null && !CheckUndArr())
            {
                underlyingArray = null!;
                return false;
            }

            underlyingArray = _underlyingArrayGetter!(list);
            return true;
        }
        public static bool TryGetListVersion(List<TElementType> list, out int version)
        {
            if (list == null)
                throw new ArgumentNullException(nameof(list));

            if (_versionGetter == null && !CheckVer())
            {
                version = 0;
                return false;
            }

            version = _versionGetter!(list);
            return true;
        }
    }

    private static void ReflectionLogDebug(string message, ConsoleColor color) => Logger.DevkitServer.AddLog(Logger.Terminal, null, Severity.Debug, message, null, (int)color);
    private static void ReflectionLogInfo(string message, ConsoleColor color) => Logger.DevkitServer.AddLog(Logger.Terminal, null, Severity.Info, message, null, (int)color);
    private static void ReflectionLogWarning(string message, ConsoleColor color, string source) => Logger.DevkitServer.AddLog(Logger.Terminal, null, Severity.Warning, message, null, (int)color);
    private static void ReflectionLogError(string message, ConsoleColor color, string source) => Logger.DevkitServer.AddLog(Logger.Terminal, source, Severity.Error, message, null, (int)color);
    private static void ReflectionLogErrorException(Exception ex, string message, ConsoleColor color, string source) => Logger.DevkitServer.AddLog(Logger.Terminal, source, Severity.Error, message, ex, (int)color);
    private static void ReflectionLogFatal(string message, ConsoleColor color, string source) => Logger.DevkitServer.AddLog(Logger.Terminal, source, Severity.Fatal, message, null, (int)color);
    private static void ReflectionLogException(Exception ex, string source) => Logger.DevkitServer.AddLog(Logger.Terminal, source, Severity.Error, null, ex, LoggerExtensions.DefaultErrorColor);

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

    /// <summary>Logs a debug message with no source. Signature: (string message, ConsoleColor color).</summary>
    /// <remarks>Lazily cached.</remarks>
    /// <exception cref="MemberAccessException"/>
    public static MethodInfo LogDebug => _logDebug ??= GetMethod(ReflectionLogDebug) ?? throw new MemberAccessException("Unable to find method.");

    /// <summary>Logs an info message with no source. Signature: (string message, ConsoleColor color).</summary>
    /// <remarks>Lazily cached.</remarks>
    /// <exception cref="MemberAccessException"/>
    public static MethodInfo LogInfo => _logInfo ??= GetMethod(ReflectionLogInfo) ?? throw new MemberAccessException("Unable to find method.");

    /// <summary>Logs a warning message with no source. Signature: (string message, ConsoleColor color, string source).</summary>
    /// <remarks>Lazily cached.</remarks>
    /// <exception cref="MemberAccessException"/>
    public static MethodInfo LogWarning => _logWarning ??= GetMethod(ReflectionLogWarning) ?? throw new MemberAccessException("Unable to find method.");

    /// <summary>Logs an error message with no source. Signature: (string message, ConsoleColor color, string source).</summary>
    /// <remarks>Lazily cached.</remarks>
    /// <exception cref="MemberAccessException"/>
    public static MethodInfo LogError => _logError ??= GetMethod(ReflectionLogError) ?? throw new MemberAccessException("Unable to find method.");

    /// <summary>Logs a fatal message with no source. Signature: (string message, ConsoleColor color, string source).</summary>
    /// <remarks>Lazily cached.</remarks>
    /// <exception cref="MemberAccessException"/>
    public static MethodInfo LogFatal => _logFatal ??= GetMethod(ReflectionLogFatal) ?? throw new MemberAccessException("Unable to find method.");

    /// <summary>Logs an exception with no source. Signature: (Exception ex, string source).</summary>
    /// <remarks>Lazily cached.</remarks>
    /// <exception cref="MemberAccessException"/>
    public static MethodInfo LogException => _logException ??= GetMethod(ReflectionLogException) ?? throw new MemberAccessException("Unable to find method.");

    /// <summary>Logs an exception with no source. Signature: (Exception ex, string message, ConsoleColor color, string source).</summary>
    /// <remarks>Lazily cached.</remarks>
    /// <exception cref="MemberAccessException"/>
    public static MethodInfo LogErrorException => _logException ??= GetMethod(ReflectionLogErrorException) ?? throw new MemberAccessException("Unable to find method.");

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

/// <summary>
/// Represents a setter for an instance field or property.
/// </summary>
/// <typeparam name="TInstance">The declaring type of the member.</typeparam>
/// <typeparam name="T">The return type of the member</typeparam>
public delegate void InstanceSetter<in TInstance, in T>(TInstance owner, T value);

/// <summary>
/// Represents a getter for an instance field or property.
/// </summary>
/// <typeparam name="TInstance">The declaring type of the member.</typeparam>
/// <typeparam name="T">The return type of the member</typeparam>
public delegate T InstanceGetter<in TInstance, out T>(TInstance owner);

/// <summary>
/// Represents a setter for a static field or property.
/// </summary>
/// <typeparam name="T">The return type of the member</typeparam>
public delegate void StaticSetter<in T>(T value);

/// <summary>
/// Represents a getter for a static field or property.
/// </summary>
/// <typeparam name="T">The return type of the member</typeparam>
public delegate T StaticGetter<out T>();

/// <summary>
/// Used with <see cref="Accessor.ForEachBaseType(Type, ForEachBaseType, bool, bool)"/>
/// </summary>
/// <param name="depth">Number of types below the provided type this base type is. Will be zero if the type returned is the provided type, 1 for its base type, and so on.</param>
public delegate void ForEachBaseType(Type type, int depth);

/// <summary>
/// Used with <see cref="Accessor.ForEachBaseType(Type, ForEachBaseTypeWhile, bool, bool)"/>
/// </summary>
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
    internal bool RequiresUIAccessTools { get; set; }

    public EarlyTypeInitAttribute() : this(0) { }

    public EarlyTypeInitAttribute(int priority)
    {
        Priority = priority;
    }
    internal EarlyTypeInitAttribute(bool requiresUIAccessTools)
    {
        RequiresUIAccessTools = requiresUIAccessTools;
    }
    internal EarlyTypeInitAttribute(int priority, bool requiresUIAccessTools)
    {
        Priority = priority;
        RequiresUIAccessTools = requiresUIAccessTools;
    }
}