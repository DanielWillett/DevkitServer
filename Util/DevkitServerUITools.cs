using DanielWillett.ReflectionTools.Emit;
using DanielWillett.UITools.Util;
using HarmonyLib;
using System.Reflection;
using System.Reflection.Emit;
using DanielWillett.ReflectionTools;

namespace DevkitServer.Util;

/// <summary>
/// Extra methods relating to <see cref="UIAccessor"/>.
/// </summary>
public static class DevkitServerUITools
{
    private const string Source = nameof(DevkitServerUITools);
    private static Type[]? _funcTypes;
    private static Type[]? _actionTypes;

    private static readonly StaticGetter<ISleekButton>? GetLoadingUICancelButton
        = Accessor.GenerateStaticGetter<ISleekButton>(typeof(LoadingUI), "cancelButton", throwOnError: false);

    private static readonly StaticGetter<ISleekBox>? GetLoadingBarBox
        = Accessor.GenerateStaticGetter<ISleekBox>(typeof(LoadingUI), "loadingBarBox", throwOnError: false);


    /// <summary>
    /// Sets the visibility of LoadingUI.cancelButton.
    /// </summary>
    public static bool SetLoadingCancelVisibility(bool visibility)
    {
        ThreadUtil.assertIsGameThread();

        ISleekButton? button = GetLoadingUICancelButton?.Invoke();

        if (button != null)
            button.IsVisible = visibility;
        else
            visibility = false;
        ISleekBox? box = GetLoadingBarBox?.Invoke();
        if (box != null)
            box.SizeOffset_X = visibility ? -130f : -20f;
        return button != null;
    }

    /// <exception cref="ArgumentOutOfRangeException">Invalid enum.</exception>
    /// <exception cref="MemberAccessException">Type or field not found (or invalid).</exception>
    public static Func<TValue?>? CreateUIFieldGetterReturn<TValue, TVanillaUI>(string fieldName, bool throwOnFailure = true, string? altName = null) where TVanillaUI : class
        => CreateUIFieldGetterDelegate<Func<TValue?>>(typeof(TVanillaUI), fieldName, throwOnFailure, altName, typeof(TValue));

    /// <exception cref="ArgumentOutOfRangeException">Invalid enum.</exception>
    /// <exception cref="MemberAccessException">Type or field not found (or invalid).</exception>
    public static Func<TValue?>? CreateUIFieldGetterReturn<TValue>(Type? uiType, string fieldName, bool throwOnFailure = true, string? altName = null)
        => CreateUIFieldGetterDelegate<Func<TValue?>>(uiType, fieldName, throwOnFailure, altName, typeof(TValue));

    /// <exception cref="ArgumentOutOfRangeException">Invalid enum.</exception>
    /// <exception cref="MemberAccessException">Type or field not found (or invalid).</exception>
    public static TDelegate? CreateUIFieldGetterDelegate<TDelegate, TVanillaUI>(string fieldName, bool throwOnFailure = true, string? altName = null, Type? rtnType = null) where TDelegate : Delegate where TVanillaUI : class
        => CreateUIFieldGetterDelegate<TDelegate>(typeof(TVanillaUI), fieldName, throwOnFailure, altName, rtnType);

    /// <exception cref="ArgumentOutOfRangeException">Invalid enum.</exception>
    /// <exception cref="MemberAccessException">Type or field not found (or invalid).</exception>
    public static TDelegate? CreateUIFieldGetterDelegate<TDelegate>(Type? uiType, string fieldName, bool throwOnFailure = true, string? altName = null, Type? rtnType = null) where TDelegate : Delegate
    {
        MemberInfo? field = null;
        try
        {
            Type accessTools = typeof(UIAccessor);
            const MethodAttributes attr = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig;
            if (uiType == null)
            {
                Logger.DevkitServer.LogWarning(Source, "Unable to find type for field " + fieldName.Format() + ".");
                DevkitServerModule.Fault();
                if (throwOnFailure)
                    throw new MemberAccessException("Unable to find type for field: \"" + fieldName + "\".");
                return null;
            }
            
            field = uiType.GetField(fieldName, BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null)
                field = uiType.GetProperty(fieldName, BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            
            if (field == null && !string.IsNullOrEmpty(altName))
                field = uiType.GetField(altName, BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            
            if (field == null && !string.IsNullOrEmpty(altName))
                field = uiType.GetProperty(altName, BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            
            Type? memberType = field is FieldInfo f2 ? f2.FieldType : field is PropertyInfo p2 ? p2.PropertyType : null;
            if (memberType == null || field == null || field is PropertyInfo prop && (prop.GetIndexParameters() is { Length: > 0 } || prop.GetGetMethod(true) == null))
            {
                Logger.DevkitServer.LogWarning(Source, "Unable to find field or property: " + uiType.Format() + "." + fieldName.Colorize(Color.red) + ".");
                DevkitServerModule.Fault();
                if (throwOnFailure)
                    throw new MemberAccessException("Unable to find field or property: \"" + uiType.Name + "." + fieldName + "\".");
                return null;
            }

            if (rtnType != null && !rtnType.IsAssignableFrom(memberType))
            {
                Logger.DevkitServer.LogWarning(Source, "Field or property " + field.Format() + " is not assignable to " + rtnType.Format() + ".");
                DevkitServerModule.Fault();
                if (throwOnFailure)
                    throw new MemberAccessException("Field " + field.DeclaringType?.Name + "." + field.Name + " is not assignable to " + rtnType.Name + ".");
                return null;
            }

            MethodInfo? getter = (field as PropertyInfo)?.GetGetMethod(true);

            if (getter != null && getter.IsStatic)
            {
                try
                {
                    return (TDelegate)getter.CreateDelegate(typeof(TDelegate));
                }
                catch (Exception)
                {
                    Logger.DevkitServer.LogWarning(Source, $"Error creating simplified getter delegate for {((PropertyInfo)field).Format()} UI accessor.");
                    DevkitServerModule.Fault();
                    if (throwOnFailure)
                        throw;
                    return null;
                }
            }

            DynamicMethod method = new DynamicMethod("Get" + uiType.Name + "_Impl", attr,
                CallingConventions.Standard, rtnType ?? memberType,
                Type.EmptyTypes, accessTools, true);
            ILGenerator il = method.GetILGenerator();
            
            if (field is FieldInfo field2)
            {
                if (field2.IsStatic)
                    il.Emit(OpCodes.Ldsfld, field2);
                else
                {
                    UIAccessor.LoadUIToILGenerator(il, uiType);
                    il.Emit(OpCodes.Ldfld, field2);
                }
            }
            else if (field is PropertyInfo property)
            {
                if (getter == null)
                {
                    Logger.DevkitServer.LogWarning(Source, "Property " + property.Format() + " does not have a getter.");
                    DevkitServerModule.Fault();
                    if (throwOnFailure)
                        throw new MemberAccessException("Property \"" + property.DeclaringType?.Name + "." + property.Name + "\" does not have a getter.");
                    return null;
                }
                UIAccessor.LoadUIToILGenerator(il, uiType);
                il.Emit(getter.IsVirtual || getter.IsAbstract ? OpCodes.Callvirt : OpCodes.Call, getter);
            }
            else
            {
                il.Emit(OpCodes.Ldnull);
            }

            if (rtnType != null && rtnType.IsClass && memberType.IsValueType)
                il.Emit(OpCodes.Box, memberType);

            il.Emit(OpCodes.Ret);
            return (TDelegate)method.CreateDelegate(typeof(TDelegate));
        }
        catch (Exception ex)
        {
            if (throwOnFailure)
            {
                Logger.DevkitServer.LogWarning(Source, "Error creating " + ((object?)field ?? fieldName).Format() + " accessor.");
                throw;
            }

            Logger.DevkitServer.LogWarning(Source, ex, "Error creating " + ((object?)field ?? fieldName).Format() + " accessor.");
            return null;
        }
    }

    public static Delegate? GenerateUICaller<TVanillaUI>(string methodName, Type[]? parameters = null, bool throwOnFailure = false) where TVanillaUI : class
        => GenerateUICaller(typeof(TVanillaUI), methodName, parameters, throwOnFailure);

    public static Delegate? GenerateUICaller(Type? uiType, string methodName, Type[]? parameters = null, bool throwOnFailure = false)
    {
        MethodInfo? method = null;
        if (uiType == null)
        {
            Logger.DevkitServer.LogWarning(Source, "Unable to find type for method " + methodName.Format() + ".");
            DevkitServerModule.Fault();
            if (throwOnFailure)
                throw new MemberAccessException("Unable to find type for method: \"" + methodName + "\".");
            return null;
        }
        if (parameters == null)
        {
            try
            {
                method = uiType.GetMethod(methodName, BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            }
            catch (AmbiguousMatchException)
            {
                // ignored
            }
        }
        else
        {
            method = uiType
                .GetMethod(methodName, BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                    null, CallingConventions.Any, parameters, null);
        }

        if (method == null)
        {
            Logger.DevkitServer.LogError(Source, "Unable to find matching method " + methodName.Format() + ".");
            if (throwOnFailure)
                throw new Exception("Unable to find matching method: " + methodName + ".");
            return null;
        }

        return GenerateUICaller(uiType, method, throwOnFailure);
    }
    public static TDelegate? GenerateUICaller<TDelegate, TVanillaUI>(string methodName, Type[]? parameters = null, bool throwOnFailure = false) where TDelegate : Delegate where TVanillaUI : class
        => GenerateUICaller<TDelegate>(typeof(TVanillaUI), methodName, parameters, throwOnFailure);
    public static TDelegate? GenerateUICaller<TDelegate>(Type? uiType, string methodName, Type[]? parameters = null, bool throwOnFailure = false) where TDelegate : Delegate
    {
        MethodInfo? method = null;
        if (uiType == null)
        {
            Logger.DevkitServer.LogWarning(Source, "Unable to find type for method " + methodName.Format() + ".");
            DevkitServerModule.Fault();
            if (throwOnFailure)
                throw new MemberAccessException("Unable to find type for method: \"" + methodName + "\".");
            return null;
        }
        if (parameters == null)
        {
            try
            {
                method = uiType.GetMethod(methodName, BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            }
            catch (AmbiguousMatchException)
            {
                // ignored
            }
        }
        else
        {
            method = uiType.GetMethod(methodName, BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                    null, CallingConventions.Any, parameters, null);
        }

        if (method == null)
        {
            Logger.DevkitServer.LogError(Source, "Unable to find matching method " + methodName.Format() + ".");
            if (throwOnFailure)
                throw new Exception("Unable to find matching method: " + methodName + ".");
            return null;
        }

        return GenerateUICaller<TDelegate>(uiType, method, throwOnFailure);
    }

    internal static void CheckFuncArrays()
    {
        _funcTypes ??=
        [
            typeof (Func<,>),
            typeof (Func<,,>),
            typeof (Func<,,,>),
            typeof (Func<,,,,>),
            typeof (Func<,,,,,>),
            typeof (Func<,,,,,,>),
            typeof (Func<,,,,,,,>),
            typeof (Func<,,,,,,,,>),
            typeof (Func<,,,,,,,,,>),
            typeof (Func<,,,,,,,,,,>),
            typeof (Func<,,,,,,,,,,,>),
            typeof (Func<,,,,,,,,,,,,>),
            typeof (Func<,,,,,,,,,,,,,>),
            typeof (Func<,,,,,,,,,,,,,,>),
            typeof (Func<,,,,,,,,,,,,,,,>),
            typeof (Func<,,,,,,,,,,,,,,,,>)
        ];

        _actionTypes ??=
        [
            typeof (Action<>),
            typeof (Action<,>),
            typeof (Action<,,>),
            typeof (Action<,,,>),
            typeof (Action<,,,,>),
            typeof (Action<,,,,,>),
            typeof (Action<,,,,,,>),
            typeof (Action<,,,,,,,>),
            typeof (Action<,,,,,,,,>),
            typeof (Action<,,,,,,,,,>),
            typeof (Action<,,,,,,,,,,>),
            typeof (Action<,,,,,,,,,,,>),
            typeof (Action<,,,,,,,,,,,,>),
            typeof (Action<,,,,,,,,,,,,,>),
            typeof (Action<,,,,,,,,,,,,,,>),
            typeof (Action<,,,,,,,,,,,,,,,>)
        ];
    }

    public static Delegate? GenerateUICaller(Type? uiType, MethodInfo method, bool throwOnFailure = false)
    {
        if (_funcTypes == null || _actionTypes == null)
            CheckFuncArrays();

        bool rtn = method.ReturnType != typeof(void);
        ParameterInfo[] p = method.GetParameters();
        if (p.Length > (rtn ? _funcTypes!.Length : _actionTypes!.Length))
        {
            Logger.DevkitServer.LogWarning(Source, "Method " + method.Format() + " can not have more than " + (rtn ? _funcTypes!.Length : _actionTypes!.Length) + " arguments!");
            if (throwOnFailure)
                throw new ArgumentException("Method can not have more than " + (rtn ? _funcTypes!.Length : _actionTypes!.Length) + " arguments!", nameof(method));
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
                p2[^1] = method.ReturnType;
                deleType = _funcTypes![p.Length].MakeGenericType(p2);
            }
            else
            {
                Type[] p2 = new Type[p.Length];
                for (int i = 1; i < p2.Length; ++i)
                    p2[i] = p[i].ParameterType;
                deleType = _actionTypes![p.Length];
                deleType = deleType.MakeGenericType(p2);
            }
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError(Source, ex, "Error generating UI caller for " + method.Format() + ".");
            if (throwOnFailure)
                throw;
            return null;
        }

        return GenerateUICaller(uiType, deleType, method, throwOnFailure);
    }

    public static TDelegate? GenerateUICaller<TDelegate>(Type? uiType, MethodInfo? info, bool throwOnFailure = false) where TDelegate : Delegate
    {
        if (info == null)
        {
            Logger.DevkitServer.LogError(Source, "Error generating UI caller of type " + typeof(TDelegate).Format() + ".");
            if (throwOnFailure)
                throw new MissingMethodException("Error generating UI caller of type " + typeof(TDelegate).Format() + ".");

            return null;
        }
        Delegate? d = GenerateUICaller(uiType, typeof(TDelegate), info);
        if (d is TDelegate dele)
        {
            return dele;
        }

        if (d != null)
        {
            Logger.DevkitServer.LogError(Source, "Error generating UI caller for " + info.Format() + ".");
            if (throwOnFailure)
                throw new InvalidCastException("Failed to convert from " + d.GetType() + " to " + typeof(TDelegate) + ".");
        }
        else if (throwOnFailure)
            throw new Exception("Error generating UI caller for " + info.Format() + ".");

        return null;
    }

    public static Delegate? GenerateUICaller(Type? uiType, Type delegateType, MethodInfo method, bool throwOnFailure = false)
    {
        if (uiType == null)
        {
            Logger.DevkitServer.LogWarning(Source, "Unable to find type for method " + method.Format() + ".");
            DevkitServerModule.Fault();
            if (throwOnFailure)
                throw new MemberAccessException("Unable to find type for method: \"" + method.FullDescription() + "\".");
            return null;
        }
        try
        {
            if (method.IsStatic)
                return method.CreateDelegate(delegateType);

            const MethodAttributes attr = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig;
            ParameterInfo[] parameters = method.GetParameters();
            Type[] types = new Type[parameters.Length];
            for (int i = 0; i < parameters.Length; ++i)
                types[i] = parameters[i].ParameterType;
            DynamicMethod dmethod = new DynamicMethod("Call" + method.DeclaringType?.Name + "_" + method.Name + "Impl", attr,
                CallingConventions.Standard, method.ReturnType,
                types, typeof(DevkitServerUITools), true);
            ILGenerator il = dmethod.GetILGenerator();
            UIAccessor.LoadUIToILGenerator(il, uiType);
            for (int i = 0; i < types.Length; ++i)
                il.EmitParameter(i);
            il.Emit(method.IsAbstract || method.IsVirtual ? OpCodes.Callvirt : OpCodes.Call, method);
            il.Emit(OpCodes.Ret);
            return dmethod.CreateDelegate(delegateType);
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogWarning(Source, ex, "Unable to create UI caller for " + (method.DeclaringType?.Format() ?? "<unknown-type>") + "." + method.Name);
            if (throwOnFailure)
                throw;
            return null;
        }
    }
}