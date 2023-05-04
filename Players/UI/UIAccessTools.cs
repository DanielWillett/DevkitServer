#if CLIENT
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using JetBrains.Annotations;

namespace DevkitServer.Players.UI;
[EarlyTypeInit]
[HarmonyPatch]
public static class UIAccessTools
{
    public static int MessageBlockOffset { get; private set; }
    public static int MessageBlockSize { get; private set; }

    private static readonly StaticGetter<EditorUI?> GetEditorUIInstance
        = Accessor.GenerateStaticGetter<EditorUI, EditorUI?>("instance", throwOnError: true)!;

    private static readonly StaticGetter<PlayerUI?> GetPlayerUIInstance
        = Accessor.GenerateStaticGetter<PlayerUI, PlayerUI?>("instance", throwOnError: true)!;

    private static readonly InstanceGetter<EditorUI, EditorDashboardUI?> GetEditorDashboardUIInstance
        = Accessor.GenerateInstanceGetter<EditorUI, EditorDashboardUI?>("dashboardUI", throwOnError: true)!;

    private static readonly InstanceGetter<EditorDashboardUI, EditorEnvironmentUI?> GetEditorEnvironmentUIInstance
        = Accessor.GenerateInstanceGetter<EditorDashboardUI, EditorEnvironmentUI?>("environmentUI", throwOnError: true)!;

    private static readonly InstanceGetter<EditorDashboardUI, EditorTerrainUI?> GetEditorTerrainUIInstance
        = Accessor.GenerateInstanceGetter<EditorDashboardUI, EditorTerrainUI?>("terrainMenu", throwOnError: true)!;

    private static readonly InstanceGetter<EditorDashboardUI, EditorLevelUI?> GetEditorLevelUIInstance
        = Accessor.GenerateInstanceGetter<EditorDashboardUI, EditorLevelUI?>("levelUI", throwOnError: true)!;

    private static readonly InstanceGetter<EditorLevelUI, EditorLevelObjectsUI?> GetEditorLevelObjectsUIInstance
        = Accessor.GenerateInstanceGetter<EditorLevelUI, EditorLevelObjectsUI?>("objectsUI", throwOnError: true)!;

    private static readonly Func<object?>? GetEditorTerrainHeightsUI;
    private static readonly Func<object?>? GetEditorTerrainMaterialsUI;
    private static readonly Func<object?>? GetEditorTerrainDetailsUI;
    private static readonly Func<object?>? GetEditorTerrainTilesUI;

    private static readonly Func<object?>? GetEditorEnvironmentNodesUI;

    private static readonly Func<object?>? GetEditorVolumesUI;

    public static EditorUI? EditorUI => GetEditorUIInstance();
    public static PlayerUI? PlayerUI => GetPlayerUIInstance();

    public static event System.Action? EditorUIReady;
    public static event System.Action? PlayerUIReady;
    public static EditorDashboardUI? EditorDashboardUI
    {
        get
        {
            EditorUI? editorUi = EditorUI;
            return editorUi == null ? null : GetEditorDashboardUIInstance(editorUi);
        }
    }
    public static EditorEnvironmentUI? EditorEnvironmentUI
    {
        get
        {
            EditorDashboardUI? dashboard = EditorDashboardUI;
            return dashboard == null ? null : GetEditorEnvironmentUIInstance(dashboard);
        }
    }
    public static object? EditorEnvironmentNodesUI => GetEditorEnvironmentNodesUI?.Invoke();
    public static EditorTerrainUI? EditorTerrainUI
    {
        get
        {
            EditorDashboardUI? dashboard = EditorDashboardUI;
            return dashboard == null ? null : GetEditorTerrainUIInstance(dashboard);
        }
    }
    public static object? EditorTerrainHeightsUI => GetEditorTerrainHeightsUI?.Invoke();
    public static object? EditorTerrainMaterialsUI => GetEditorTerrainMaterialsUI?.Invoke();
    public static object? EditorTerrainDetailsUI => GetEditorTerrainDetailsUI?.Invoke();
    public static object? EditorTerrainTilesUI => GetEditorTerrainTilesUI?.Invoke();
    public static EditorLevelUI? EditorLevelUI
    {
        get
        {
            EditorDashboardUI? dashboard = EditorDashboardUI;
            return dashboard == null ? null : GetEditorLevelUIInstance(dashboard);
        }
    }
    public static EditorLevelObjectsUI? EditorLevelObjectsUI
    {
        get
        {
            EditorLevelUI? level = EditorLevelUI;
            return level == null ? null : GetEditorLevelObjectsUIInstance(level);
        }
    }
    public static object? EditorVolumesUI => GetEditorVolumesUI?.Invoke();
    
    /// <exception cref="ArgumentOutOfRangeException">Invalid enum.</exception>
    /// <exception cref="MemberAccessException">Type or field not found (or invalid).</exception>
    public static Func<T?>? CreateUIFieldGetterReturn<T>(UI type, string fieldName, bool throwOnFailure = true, string? altName = null)
        => CreateUIFieldGetterDelegate<Func<T?>>(type, fieldName, throwOnFailure, altName, typeof(T));
    public static TDelegate? CreateUIFieldGetterDelegate<TDelegate>(UI type, string fieldName, bool throwOnFailure = true, string? altName = null, Type? rtnType = null) where TDelegate : Delegate
    {
        MemberInfo? field = null;
        try
        {
            Type accessTools = typeof(UIAccessTools);
            const MethodAttributes attr = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig;
            Type? containerType = GetUIType(type);
            if (containerType == null)
            {
                Logger.LogWarning("Unable to find type for field " + fieldName.Format() + ".");
                DevkitServerModule.Fault();
                if (throwOnFailure)
                    throw new MemberAccessException("Unable to find type for field: \"" + fieldName + "\".");
                return null;
            }
            field = containerType.GetField(fieldName, BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null && !string.IsNullOrEmpty(altName))
                field = containerType.GetField(altName, BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null)
                field = containerType.GetProperty(fieldName, BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null && !string.IsNullOrEmpty(altName))
                field = containerType.GetProperty(altName, BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Type? memberType = field is FieldInfo f2 ? f2.FieldType : (field is PropertyInfo p2 ? p2.PropertyType : null);
            if (memberType == null || field == null || field is PropertyInfo prop && prop.GetIndexParameters() is { Length: > 0 })
            {
                Logger.LogWarning("Unable to find field or property: " + containerType.Format() + "." + fieldName.Format(false) + ".");
                DevkitServerModule.Fault();
                if (throwOnFailure)
                    throw new MemberAccessException("Unable to find field or property: \"" + containerType.Name + "." + fieldName + "\".");
                return null;
            }
            if (rtnType != null && !rtnType.IsAssignableFrom(memberType))
            {
                Logger.LogWarning("Field or property " + field.Format() + " is not assignable to " + rtnType.Format() + ".");
                DevkitServerModule.Fault();
                if (throwOnFailure)
                    throw new MemberAccessException("Field " + field.DeclaringType?.Name + "." + field.Name + " is not assignable to " + rtnType.Name + ".");
                return null;
            }
            
            DynamicMethod method = new DynamicMethod("GetEditorTerrainHeightsUI_Impl", attr,
                CallingConventions.Standard, rtnType ?? memberType,
                Array.Empty<Type>(), accessTools, true);
            ILGenerator il = method.GetILGenerator();
            if (field is FieldInfo field2)
            {
                if (field2.IsStatic)
                    il.Emit(OpCodes.Ldsfld, field2);
                else
                {
                    LoadUIToILGenerator(il, type);
                    il.Emit(OpCodes.Ldfld, field2);
                }
            }
            else if (field is PropertyInfo property)
            {
                MethodInfo getter = property.GetGetMethod(true);
                if (getter == null)
                {
                    Logger.LogWarning("Property " + property.Format() + " does not have a getter.");
                    DevkitServerModule.Fault();
                    if (throwOnFailure)
                        throw new MemberAccessException("Property \"" + property.DeclaringType?.Name + "." + property.Name + "\" does not have a getter.");
                    return null;
                }
                if (getter.IsStatic)
                    il.Emit(OpCodes.Call, getter);
                else
                {
                    LoadUIToILGenerator(il, type);
                    il.Emit(getter.IsVirtual || getter.IsAbstract ? OpCodes.Callvirt : OpCodes.Call, getter);
                }
            }
            else il.Emit(OpCodes.Ldnull);
            if (rtnType != null && rtnType.IsClass && memberType.IsValueType)
                il.Emit(OpCodes.Box, memberType);
            il.Emit(OpCodes.Ret);
            return (TDelegate)method.CreateDelegate(typeof(TDelegate));
        }
        catch (Exception ex)
        {
            Logger.LogWarning("Error creating " + ((object?)field ?? fieldName).Format() + " accessor.");
            if (throwOnFailure)
                throw;
            Logger.LogError(ex);
            return null;
        }
    }
    private static Type? GetUIType(UI type)
    {
        return type switch
        {
            UI.Editor => typeof(EditorUI),
            UI.Player => typeof(PlayerUI),
            UI.EditorDashboard => typeof(EditorDashboardUI),
            UI.EditorEnvironment => typeof(EditorEnvironmentUI),
            UI.EditorTerrain => typeof(EditorTerrainUI),
            UI.EditorLevel => typeof(EditorLevelUI),
            UI.EditorTerrainHeights => typeof(Provider).Assembly.GetType("SDG.Unturned.EditorTerrainHeightUI"),
            UI.EditorTerrainMaterials => typeof(Provider).Assembly.GetType("SDG.Unturned.EditorTerrainMaterialsUI"),
            UI.EditorTerrainDetails => typeof(Provider).Assembly.GetType("SDG.Unturned.EditorTerrainDetailsUI"),
            UI.EditorTerrainTiles => typeof(Provider).Assembly.GetType("SDG.Unturned.EditorTerrainTilesUI"),
            UI.EditorEnvironmentNodes => typeof(Provider).Assembly.GetType("SDG.Unturned.EditorEnvironmentNodesUI"),
            UI.EditorEnvironmentLighting => typeof(EditorEnvironmentLightingUI),
            UI.EditorEnvironmentRoads => typeof(EditorEnvironmentRoadsUI),
            UI.EditorEnvironmentNavigation => typeof(EditorEnvironmentNavigationUI),
            UI.EditorLevelVolumes => typeof(Provider).Assembly.GetType("SDG.Unturned.EditorVolumesUI"),
            UI.EditorLevelObjects => typeof(EditorLevelObjectsUI),
            UI.EditorLevelVisibility => typeof(EditorLevelVisibilityUI),
            UI.EditorLevelPlayers => typeof(EditorLevelPlayersUI),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }
    private static void LoadUIToILGenerator(ILGenerator il, UI type)
    {
        Type accessTools = typeof(UIAccessTools);
        switch (type)
        {
            case UI.Editor:
                il.Emit(OpCodes.Call, accessTools.GetProperty(nameof(EditorUI), BindingFlags.Static | BindingFlags.Public)!.GetMethod);
                break;
            case UI.Player:
                il.Emit(OpCodes.Call, accessTools.GetProperty(nameof(PlayerUI), BindingFlags.Static | BindingFlags.Public)!.GetMethod);
                break;
            case UI.EditorDashboard:
                il.Emit(OpCodes.Call, accessTools.GetProperty(nameof(EditorDashboardUI), BindingFlags.Static | BindingFlags.Public)!.GetMethod);
                break;
            case UI.EditorEnvironment:
                il.Emit(OpCodes.Call, accessTools.GetProperty(nameof(EditorEnvironmentUI), BindingFlags.Static | BindingFlags.Public)!.GetMethod);
                break;
            case UI.EditorTerrain:
                il.Emit(OpCodes.Call, accessTools.GetProperty(nameof(EditorTerrainUI), BindingFlags.Static | BindingFlags.Public)!.GetMethod);
                break;
            case UI.EditorLevel:
                il.Emit(OpCodes.Call, accessTools.GetProperty(nameof(EditorLevelUI), BindingFlags.Static | BindingFlags.Public)!.GetMethod);
                break;
            case UI.EditorTerrainHeights:
                il.Emit(OpCodes.Call, accessTools.GetProperty(nameof(EditorTerrainHeightsUI), BindingFlags.Static | BindingFlags.Public)!.GetMethod);
                break;
            case UI.EditorTerrainMaterials:
                il.Emit(OpCodes.Call, accessTools.GetProperty(nameof(EditorTerrainMaterialsUI), BindingFlags.Static | BindingFlags.Public)!.GetMethod);
                break;
            case UI.EditorTerrainDetails:
                il.Emit(OpCodes.Call, accessTools.GetProperty(nameof(EditorTerrainDetailsUI), BindingFlags.Static | BindingFlags.Public)!.GetMethod);
                break;
            case UI.EditorTerrainTiles:
                il.Emit(OpCodes.Call, accessTools.GetProperty(nameof(EditorTerrainTilesUI), BindingFlags.Static | BindingFlags.Public)!.GetMethod);
                break;
            case UI.EditorEnvironmentNodes:
                il.Emit(OpCodes.Call, accessTools.GetProperty(nameof(EditorEnvironmentNodesUI), BindingFlags.Static | BindingFlags.Public)!.GetMethod);
                break;
            case UI.EditorLevelVolumes:
                il.Emit(OpCodes.Call, accessTools.GetProperty(nameof(EditorVolumesUI), BindingFlags.Static | BindingFlags.Public)!.GetMethod);
                break;
            case UI.EditorLevelObjects:
                il.Emit(OpCodes.Call, accessTools.GetProperty(nameof(EditorLevelObjectsUI), BindingFlags.Static | BindingFlags.Public)!.GetMethod);
                break;
            case UI.EditorEnvironmentLighting:
                throw new InvalidOperationException(nameof(EditorEnvironmentLightingUI) + " is not instanced.");
            case UI.EditorEnvironmentRoads:
                throw new InvalidOperationException(nameof(EditorEnvironmentRoadsUI) + " is not instanced.");
            case UI.EditorEnvironmentNavigation:
                throw new InvalidOperationException(nameof(EditorEnvironmentNavigationUI) + " is not instanced.");
            case UI.EditorLevelVisibility:
                throw new InvalidOperationException(nameof(EditorLevelVisibilityUI) + " is not instanced.");
            case UI.EditorLevelPlayers:
                throw new InvalidOperationException(nameof(EditorLevelPlayersUI) + " is not instanced.");
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }
    }
    public static Delegate? GenerateUICaller(UI type, string methodName, Type[]? parameters = null, bool throwOnFailure = false)
    {
        MethodInfo? method = null;
        Type? containerType = GetUIType(type);
        if (containerType == null)
        {
            Logger.LogWarning("Unable to find type for method " + methodName.Format() + ".");
            DevkitServerModule.Fault();
            if (throwOnFailure)
                throw new MemberAccessException("Unable to find type for method: \"" + methodName + "\".");
            return null;
        }
        if (parameters == null)
        {
            try
            {
                method = containerType.GetMethod(methodName, BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            }
            catch (AmbiguousMatchException)
            {
                // ignored
            }
        }
        else
        {
            method = containerType
                .GetMethod(methodName, BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                    null, CallingConventions.Any, parameters, null);
        }

        if (method == null)
        {
            Logger.LogError("Unable to find matching method " + methodName.Format() + ".");
            if (throwOnFailure)
                throw new Exception("Unable to find matching method: " + methodName + ".");
            return null;
        }

        return GenerateUICaller(type, method);
    }
    public static TDelegate? GenerateUICaller<TDelegate>(UI type, string methodName, Type[]? parameters = null, bool throwOnFailure = false) where TDelegate : Delegate
    {
        MethodInfo? method = null;
        Type? containerType = GetUIType(type);
        if (containerType == null)
        {
            Logger.LogWarning("Unable to find type for method " + methodName.Format() + ".");
            DevkitServerModule.Fault();
            if (throwOnFailure)
                throw new MemberAccessException("Unable to find type for method: \"" + methodName + "\".");
            return null;
        }
        if (parameters == null)
        {
            try
            {
                method = containerType.GetMethod(methodName, BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            }
            catch (AmbiguousMatchException)
            {
                // ignored
            }
        }
        else
        {
            method = containerType.GetMethod(methodName, BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                    null, CallingConventions.Any, parameters, null);
        }

        if (method == null)
        {
            Logger.LogError("Unable to find matching method " + methodName.Format() + ".");
            if (throwOnFailure)
                throw new Exception("Unable to find matching method: " + methodName + ".");
            return null;
        }

        return GenerateUICaller<TDelegate>(type, method);
    }
    public static Delegate? GenerateUICaller(UI type, MethodInfo method, bool throwOnFailure = false)
    {
        Accessor.CheckFuncArrays();

        bool rtn = method.ReturnType != typeof(void);
        ParameterInfo[] p = method.GetParameters();
        if (p.Length > (rtn ? Accessor.FuncTypes!.Length : Accessor.ActionTypes!.Length))
        {
            if (throwOnFailure)
                throw new ArgumentException("Method can not have more than " + (rtn ? Accessor.FuncTypes!.Length : Accessor.ActionTypes!.Length) + " arguments!", nameof(method));
            Logger.LogWarning("Method " + method.Format() + " can not have more than " + (rtn ? Accessor.FuncTypes!.Length : Accessor.ActionTypes!.Length) + " arguments!");
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
                deleType = Accessor.FuncTypes![p.Length].MakeGenericType(p2);
            }
            else
            {
                Type[] p2 = new Type[p.Length];
                for (int i = 1; i < p2.Length; ++i)
                    p2[i] = p[i].ParameterType;
                deleType = Accessor.ActionTypes![p.Length];
                deleType = deleType.MakeGenericType(p2);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Error generating UI caller for " + method.Format() + ".");
            Logger.LogError(ex);
            if (throwOnFailure)
                throw;
            return null;
        }

        return GenerateUICaller(type, deleType, method, throwOnFailure);
    }
    public static TDelegate? GenerateUICaller<TDelegate>(UI type, MethodInfo info, bool throwOnFailure = false) where TDelegate : Delegate
    {
        if (info == null)
        {
            Logger.LogError("Error generating UI caller of type " + typeof(TDelegate).Format() + ".");
            if (throwOnFailure)
                throw new MissingMethodException("Error generating UI caller of type " + typeof(TDelegate).Format() + ".");

            return null;
        }
        Delegate? d = GenerateUICaller(type, typeof(TDelegate), info);
        if (d is TDelegate dele)
        {
            return dele;
        }

        if (d != null)
        {
            Logger.LogError("Error generating UI caller for " + info.Format() + ".");
            if (throwOnFailure)
                throw new InvalidCastException("Failed to convert from " + d.GetType() + " to " + typeof(TDelegate) + ".");
        }
        else if (throwOnFailure)
            throw new Exception("Error generating UI caller for " + info.Format() + ".");

        return null;
    }
    public static Delegate? GenerateUICaller(UI type, Type delegateType, MethodInfo method, bool throwOnFailure = false)
    {
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
                types, typeof(UIAccessTools), true);
            ILGenerator il = dmethod.GetILGenerator();
            LoadUIToILGenerator(il, type);
            for (int i = 0; i < types.Length; ++i)
                il.LoadParameter(i);
            il.Emit(method.IsAbstract || method.IsVirtual ? OpCodes.Callvirt : OpCodes.Call, method);
            il.Emit(OpCodes.Ret);
            return dmethod.CreateDelegate(delegateType);
        }
        catch (Exception ex)
        {
            Logger.LogWarning("Unable to create UI caller for " + (method.DeclaringType?.Format() ?? "<unknown-type>") + "." + method.Name);
            Logger.LogError(ex);
            if (throwOnFailure)
                throw;
            return null;
        }
    }
    static UIAccessTools()
    {
        try
        {
            Type accessTools = typeof(UIAccessTools);
            MethodInfo getEditorTerrainUI = accessTools.GetProperty(nameof(EditorTerrainUI), BindingFlags.Public | BindingFlags.Static)!.GetMethod;
            const MethodAttributes attr = MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig;
            Assembly sdg = typeof(Provider).Assembly;

            /*
             * TERRAIN
             */
            Type containerType = typeof(EditorTerrainUI);

            /* HEIGHTS */
            Type? rtnType = sdg.GetType("SDG.Unturned.EditorTerrainHeightUI");
            if (rtnType == null)
            {
                Logger.LogWarning("Unable to find type: SDG.Unturned.EditorTerrainHeightUI.");
                DevkitServerModule.Fault();
                return;
            }
            FieldInfo? field = containerType.GetField("heightV2", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public) ??
                               containerType.GetField("heights", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null || field.IsStatic || !rtnType.IsAssignableFrom(field.FieldType))
            {
                Logger.LogWarning("Unable to find field: EditorTerrainUI.heightV2.");
                DevkitServerModule.Fault();
                return;
            }
            DynamicMethod method = new DynamicMethod("GetEditorTerrainHeightsUI_Impl", attr,
                CallingConventions.Standard, rtnType,
                Array.Empty<Type>(), accessTools, true);
            ILGenerator il = method.GetILGenerator();
            il.Emit(OpCodes.Call, getEditorTerrainUI);
            il.Emit(OpCodes.Ldfld, field);
            il.Emit(OpCodes.Ret);
            GetEditorTerrainHeightsUI = (Func<object?>)method.CreateDelegate(typeof(Func<object>));

            /* MATERIALS */
            rtnType = sdg.GetType("SDG.Unturned.EditorTerrainMaterialsUI");
            if (rtnType == null)
            {
                Logger.LogWarning("Unable to find type: SDG.Unturned.EditorTerrainMaterialsUI.");
                DevkitServerModule.Fault();
                return;
            }
            field = containerType.GetField("materialsV2", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public) ??
                    containerType.GetField("materials", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null || field.IsStatic || !rtnType.IsAssignableFrom(field.FieldType))
            {
                Logger.LogWarning("Unable to find field: EditorTerrainUI.materialsV2.");
                DevkitServerModule.Fault();
                return;
            }
            method = new DynamicMethod("GetEditorTerrainMaterialsUI_Impl", attr,
                CallingConventions.Standard, rtnType,
                Array.Empty<Type>(), accessTools, true);
            il = method.GetILGenerator();
            il.Emit(OpCodes.Call, getEditorTerrainUI);
            il.Emit(OpCodes.Ldfld, field);
            il.Emit(OpCodes.Ret);
            GetEditorTerrainMaterialsUI = (Func<object?>)method.CreateDelegate(typeof(Func<object>));

            /* DETAILS */
            rtnType = sdg.GetType("SDG.Unturned.EditorTerrainDetailsUI");
            if (rtnType == null)
            {
                Logger.LogWarning("Unable to find type: SDG.Unturned.EditorTerrainDetailsUI.");
                DevkitServerModule.Fault();
                return;
            }
            field = containerType.GetField("detailsV2", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public) ??
                    containerType.GetField("details", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null || field.IsStatic || !rtnType.IsAssignableFrom(field.FieldType))
            {
                Logger.LogWarning("Unable to find field: EditorTerrainUI.detailsV2.");
                DevkitServerModule.Fault();
                return;
            }
            method = new DynamicMethod("GetEditorTerrainDetailsUI_Impl", attr,
                CallingConventions.Standard, rtnType,
                Array.Empty<Type>(), accessTools, true);
            il = method.GetILGenerator();
            il.Emit(OpCodes.Call, getEditorTerrainUI);
            il.Emit(OpCodes.Ldfld, field);
            il.Emit(OpCodes.Ret);
            GetEditorTerrainDetailsUI = (Func<object?>)method.CreateDelegate(typeof(Func<object>));

            /* TILES */
            rtnType = sdg.GetType("SDG.Unturned.EditorTerrainTilesUI");
            if (rtnType == null)
            {
                Logger.LogWarning("Unable to find type: SDG.Unturned.EditorTerrainTilesUI.");
                DevkitServerModule.Fault();
                return;
            }
            field = containerType.GetField("tiles", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public) ??
                    containerType.GetField("tilesV2", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null || field.IsStatic || !rtnType.IsAssignableFrom(field.FieldType))
            {
                Logger.LogWarning("Unable to find field: EditorTerrainUI.tiles.");
                DevkitServerModule.Fault();
                return;
            }
            method = new DynamicMethod("GetEditorTerrainTilesUI_Impl", attr,
                CallingConventions.Standard, rtnType,
                Array.Empty<Type>(), accessTools, true);
            il = method.GetILGenerator();
            il.Emit(OpCodes.Call, getEditorTerrainUI);
            il.Emit(OpCodes.Ldfld, field);
            il.Emit(OpCodes.Ret);
            GetEditorTerrainTilesUI = (Func<object?>)method.CreateDelegate(typeof(Func<object>));

            /*
             * TERRAIN
             */
            containerType = typeof(EditorEnvironmentUI);

            /* NODES */
            rtnType = sdg.GetType("SDG.Unturned.EditorEnvironmentNodesUI");
            if (rtnType == null)
            {
                Logger.LogWarning("Unable to find type: SDG.Unturned.EditorEnvironmentNodesUI.");
                DevkitServerModule.Fault();
                return;
            }
            field = containerType.GetField("nodesUI", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public) ??
                    containerType.GetField("nodes", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null || !field.IsStatic || !rtnType.IsAssignableFrom(field.FieldType))
            {
                Logger.LogWarning("Unable to find field: EditorEnvironmentUI.nodesUI.");
                DevkitServerModule.Fault();
                return;
            }
            method = new DynamicMethod("GetEditorEnvironmentNodesUI_Impl", attr,
                CallingConventions.Standard, rtnType,
                Array.Empty<Type>(), accessTools, true);
            il = method.GetILGenerator();
            il.Emit(OpCodes.Ldsfld, field);
            il.Emit(OpCodes.Ret);
            GetEditorEnvironmentNodesUI = (Func<object?>)method.CreateDelegate(typeof(Func<object>));

            /*
             * LEVEL
             */
            containerType = typeof(EditorLevelUI);

            /* VOLUMES */
            rtnType = sdg.GetType("SDG.Unturned.EditorVolumesUI");
            if (rtnType == null)
            {
                Logger.LogWarning("Unable to find type: SDG.Unturned.EditorVolumesUI.");
                DevkitServerModule.Fault();
                return;
            }
            field = containerType.GetField("volumesUI", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public) ??
                    containerType.GetField("volumes", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null || !field.IsStatic || !rtnType.IsAssignableFrom(field.FieldType))
            {
                Logger.LogWarning("Unable to find field: EditorLevelUI.volumesUI.");
                DevkitServerModule.Fault();
                return;
            }
            method = new DynamicMethod("GetEditorVolumesUI_Impl", attr,
                CallingConventions.Standard, rtnType,
                Array.Empty<Type>(), accessTools, true);
            il = method.GetILGenerator();
            il.Emit(OpCodes.Ldsfld, field);
            il.Emit(OpCodes.Ret);
            GetEditorVolumesUI = (Func<object?>)method.CreateDelegate(typeof(Func<object>));
        }
        catch (Exception ex)
        {
            Logger.LogError("Error initializing UI access tools.");
            Logger.LogError(ex);
            DevkitServerModule.Fault();
        }
    }
    
    [HarmonyPatch(typeof(EditorUI), "Start")]
    [HarmonyPostfix]
    [UsedImplicitly]
    private static void EditorUIStartPostfix()
    {
        EditorUIReady?.Invoke();
        Logger.LogInfo("Editor UI ready.");
    }

    [HarmonyPatch(typeof(PlayerUI), "InitializePlayer")]
    [HarmonyPostfix]
    [UsedImplicitly]
    private static void PlayerUIStartPostfix()
    {
        PlayerUIReady?.Invoke();
        Logger.LogInfo("Player UI ready.");
    }
}
public enum UI
{
    Editor,
    Player,
    EditorDashboard,
    EditorEnvironment,
    EditorTerrain,
    EditorLevel,
    EditorTerrainHeights,
    EditorTerrainMaterials,
    EditorTerrainDetails,
    EditorTerrainTiles,
    EditorEnvironmentNodes,
    EditorEnvironmentLighting,
    EditorEnvironmentRoads,
    EditorEnvironmentNavigation,
    EditorLevelVolumes,
    EditorLevelObjects,
    EditorLevelVisibility,
    EditorLevelPlayers
}
#endif