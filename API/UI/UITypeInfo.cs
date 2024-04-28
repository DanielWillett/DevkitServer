#if CLIENT
using DanielWillett.ReflectionTools.Emit;
using System.Reflection;
using System.Reflection.Emit;

namespace DevkitServer.API.UI;

/// <summary>
/// Contains information about a vanilla UI type.
/// </summary>
public class UITypeInfo
{
    internal readonly string? ExpectedTypeName;

    /// <summary>
    /// Type of the UI.
    /// </summary>
    public Type Type { get; }

    /// <summary>
    /// Type that 'owns' the UI, or <see langword="null"/> for root types (<see cref="LoadingUI"/>, <see cref="MenuUI"/>, <see cref="PlayerUI"/>, and <see cref="EditorUI"/>).
    /// </summary>
    public Type? Parent { get; set; }

    internal string? ParentName
    {
        get => Parent?.FullName;
        set => Parent = value == null ? null : (UIAccessTools.FindUIType(value) ?? typeof(object));
    }

    /// <summary>
    /// If all the fields and methods (except maybe the constructor) for the UI is static.
    /// </summary>
    public bool IsStaticUI { get; set; }

    /// <summary>
    /// If all the fields and methods (except maybe the constructor) for the UI are instance but there is only ever one instance.
    /// </summary>
    public bool IsInstanceUI { get; set; } = true;

    /// <summary>
    /// Category the UI is in.
    /// </summary>
    public UIScene Scene { get; set; }

    /// <summary>
    /// Name of the property to get the UI in <see cref="DanielWillett.UITools.Util.UIAccessor"/>.
    /// </summary>
    public string? EmitProperty { get; set; }

    /// <summary>
    /// The member that returns a boolean for if the UI is active or not, or an object that can be null-checked.
    /// </summary>
    public MemberInfo? IsActiveMember { get; set; }

    /// <summary>
    /// A custom method used to emit the UI instance to a <see cref="ILGenerator"/>.
    /// </summary>
    public Action<UITypeInfo, IOpCodeEmitter>? CustomEmitter { get; set; }

    /// <summary>
    /// All methods that trigger the UI being opened.
    /// </summary>
    public UIVisibilityMethodInfo[] OpenMethods { get; }

    /// <summary>
    /// All methods that trigger the UI being closed.
    /// </summary>
    public UIVisibilityMethodInfo[] CloseMethods { get; }

    /// <summary>
    /// All methods that trigger the UI being initialized.
    /// </summary>
    public UIVisibilityMethodInfo[] InitializeMethods { get; }

    /// <summary>
    /// All methods that trigger the UI being destroyed.
    /// </summary>
    public UIVisibilityMethodInfo[] DestroyMethods { get; }

    /// <summary>
    /// Custom listener that triggers the UI being opened.
    /// </summary>
    public ICustomOnOpenUIHandler? CustomOnOpen { get; set; }

    /// <summary>
    /// Custom listener that triggers the UI being closed.
    /// </summary>
    public ICustomOnCloseUIHandler? CustomOnClose { get; set; }

    /// <summary>
    /// Custom listener that triggers the UI being initialized.
    /// </summary>
    public ICustomOnInitializeUIHandler? CustomOnInitialize { get; set; }

    /// <summary>
    /// Custom listener that triggers the UI being destroyed.
    /// </summary>
    public ICustomOnDestroyUIHandler? CustomOnDestroy { get; set; }

    /// <summary>
    /// Default opened state when the UI is initialized.
    /// </summary>
    public bool DefaultOpenState { get; set; }

    /// <summary>
    /// This UI only opens on initialize, open methods will not be looked for if this is <see langword="true"/>.
    /// </summary>
    public bool OpenOnInitialize { get; set; }

    /// <summary>
    /// This UI only closed on destroy, close methods will not be looked for if this is <see langword="true"/>.
    /// </summary>
    public bool CloseOnDestroy { get; set; }

    /// <summary>
    /// This UI always destroys on close, destroy methods will not be looked for if this is <see langword="true"/> and <see cref="CloseOnDestroy"/> is <see langword="false"/>.
    /// </summary>
    public bool DestroyOnClose { get; set; }

    /// <summary>
    /// This UI is destroyed when its parent is destroyed.
    /// </summary>
    public bool DestroyWhenParentDestroys { get; set; }

    internal UITypeInfo(string typeName,
        IReadOnlyList<MethodBase>? closeMethods = null,
        IReadOnlyList<MethodBase>? openMethods = null,
        IReadOnlyList<MethodBase>? initializeMethods = null,
        IReadOnlyList<MethodBase>? destroyMethods = null,
        bool hasActiveMember = true) : this(UIAccessTools.FindUIType(typeName) ?? typeof(object), closeMethods, openMethods, initializeMethods, destroyMethods, hasActiveMember)
    {
        ExpectedTypeName = Type == typeof(object) ? typeName : Type.FullName!;
    }

    /// <summary>
    /// Create a new <see cref="UITypeInfo"/> and prep the <see cref="OpenMethods"/>, <see cref="CloseMethods"/>, <see cref="InitializeMethods"/>, and <see cref="DestroyMethods"/> properties.
    /// </summary>
    /// <param name="type">Parent UI type.</param>
    /// <param name="closeMethods">Override list of close methods. By default it looks for all methods named (case-insensitive) 'close'.</param>
    /// <param name="openMethods">Override list of open methods. By default it looks for all methods named (case-insensitive) 'open'.</param>
    /// <param name="initializeMethods">Override list of initialize methods. By default it looks for all instance constructors.</param>
    /// <param name="destroyMethods">Override list of destroy methods. By default it looks for all methods named (case-insensitive) 'destroy' or 'OnDestroy'.</param>
    /// <param name="hasActiveMember">If the UI has an 'isActive' <see cref="bool"/> member, which stores the 'open' status. Looks for fields or properties named (case-insensitive) 'active' or 'isActive'.</param>
    public UITypeInfo(Type type,
        IReadOnlyList<MethodBase>? closeMethods = null,
        IReadOnlyList<MethodBase>? openMethods = null,
        IReadOnlyList<MethodBase>? initializeMethods = null,
        IReadOnlyList<MethodBase>? destroyMethods = null,
        bool hasActiveMember = true)
    {
        Type = type;
        MethodInfo[]? methods = closeMethods != null && openMethods != null && destroyMethods != null ? null : type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
        // ReSharper disable CoVariantArrayConversion
        if (openMethods == null)
        {
            try
            {
                List<MethodInfo> matches = methods!.Where(x => x.Name.Equals("Open", StringComparison.InvariantCultureIgnoreCase)).ToList();
                RemoveMatchesFromBaseClasses(type, matches);
                OpenMethods = new UIVisibilityMethodInfo[matches.Count];
                for (int i = 0; i < matches.Count; ++i)
                {
                    MethodBase method = matches[i];
                    OpenMethods[i] = new UIVisibilityMethodInfo(method, method.GetParameters().Length > 0, method.IsStatic);
                }

                if (OpenMethods.Length == 0)
                    Logger.DevkitServer.LogWarning(UIAccessTools.Source, $"Failed to find any open methods for UI: {type.Format()}.");

            }
            catch (Exception ex)
            {
                Logger.DevkitServer.LogWarning(UIAccessTools.Source, ex, $"Error finding any open methods for UI: {type.Format()}.");
                OpenMethods = Array.Empty<UIVisibilityMethodInfo>();
            }
        }
        else
        {
            OpenMethods = new UIVisibilityMethodInfo[openMethods.Count];
            for (int i = 0; i < openMethods.Count; ++i)
            {
                MethodBase method = openMethods[i];
                OpenMethods[i] = new UIVisibilityMethodInfo(method, method.GetParameters().Length > 0, method.IsStatic);
            }
        }
        if (closeMethods == null)
        {
            try
            {
                List<MethodInfo> matches = methods!.Where(x => x.Name.Equals("Close", StringComparison.InvariantCultureIgnoreCase)).ToList();
                RemoveMatchesFromBaseClasses(type, matches);
                CloseMethods = new UIVisibilityMethodInfo[matches.Count];
                for (int i = 0; i < matches.Count; ++i)
                {
                    MethodBase method = matches[i];
                    CloseMethods[i] = new UIVisibilityMethodInfo(method, method.GetParameters().Length > 0, method.IsStatic);
                }

                if (CloseMethods.Length == 0)
                    Logger.DevkitServer.LogWarning(UIAccessTools.Source, $"Failed to find any close methods for UI: {type.Format()}.");
            }
            catch (Exception ex)
            {
                Logger.DevkitServer.LogWarning(UIAccessTools.Source, ex, $"Error finding any close methods for UI: {type.Format()}.");
                CloseMethods = Array.Empty<UIVisibilityMethodInfo>();
            }
        }
        else
        {
            CloseMethods = new UIVisibilityMethodInfo[closeMethods.Count];
            for (int i = 0; i < closeMethods.Count; ++i)
            {
                MethodBase method = closeMethods[i];
                CloseMethods[i] = new UIVisibilityMethodInfo(method, method.GetParameters().Length > 0, method.IsStatic);
            }
        }
        if (initializeMethods == null)
        {
            try
            {
                ConstructorInfo[] constructors = type.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                InitializeMethods = new UIVisibilityMethodInfo[constructors.Length];

                for (int i = 0; i < constructors.Length; ++i)
                {
                    ConstructorInfo ctor = constructors[i];

                    InitializeMethods[i] = new UIVisibilityMethodInfo(ctor, ctor.GetParameters().Length > 0, ctor.IsStatic);
                }

                if (InitializeMethods.Length == 0)
                    Logger.DevkitServer.LogWarning(UIAccessTools.Source, $"Failed to find any initialize constructors for UI: {type.Format()}.");
            }
            catch (Exception ex)
            {
                Logger.DevkitServer.LogWarning(UIAccessTools.Source, ex, $"Error finding any initialize constructors for UI: {type.Format()}.");
                InitializeMethods = Array.Empty<UIVisibilityMethodInfo>();
            }
        }
        else
        {
            InitializeMethods = new UIVisibilityMethodInfo[initializeMethods.Count];
            for (int i = 0; i < initializeMethods.Count; ++i)
            {
                MethodBase method = initializeMethods[i];
                InitializeMethods[i] = new UIVisibilityMethodInfo(method, method.GetParameters().Length > 0, method.IsStatic);
            }
        }
        if (destroyMethods == null)
        {
            try
            {
                List<MethodInfo> matches = methods!.Where(x => x.Name.Equals("OnDestroy", StringComparison.InvariantCultureIgnoreCase) || x.Name.Equals("destroy", StringComparison.InvariantCultureIgnoreCase)).ToList();
                RemoveMatchesFromBaseClasses(type, matches);
                DestroyMethods = new UIVisibilityMethodInfo[matches.Count];
                for (int i = 0; i < matches.Count; ++i)
                {
                    MethodBase method = matches[i];
                    DestroyMethods[i] = new UIVisibilityMethodInfo(method, method.GetParameters().Length > 0, method.IsStatic);
                }

                if (DestroyMethods.Length == 0)
                    Logger.DevkitServer.LogDebug(UIAccessTools.Source, $"Failed to find any destroy methods for UI: {type.Format()}.");
            }
            catch (Exception ex)
            {
                Logger.DevkitServer.LogWarning(UIAccessTools.Source, ex, $"Error finding any destroy methods for UI: {type.Format()}.");
                DestroyMethods = Array.Empty<UIVisibilityMethodInfo>();
            }
        }
        else
        {
            DestroyMethods = new UIVisibilityMethodInfo[destroyMethods.Count];
            for (int i = 0; i < destroyMethods.Count; ++i)
            {
                MethodBase method = destroyMethods[i];
                DestroyMethods[i] = new UIVisibilityMethodInfo(method, method.GetParameters().Length > 0, method.IsStatic);
            }
        }

        DestroyWhenParentDestroys = DestroyMethods.Length == 0;

        if (hasActiveMember)
        {
            try
            {
                IsActiveMember = type
                    .GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                    .Where(x => x.FieldType == typeof(bool)).SingleOrDefaultSafe(x =>
                        x.Name.Equals("active", StringComparison.InvariantCultureIgnoreCase) || x.Name.Equals("isActive", StringComparison.InvariantCultureIgnoreCase));
                IsActiveMember ??= type
                    .GetProperties(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                    .Where(x => x.PropertyType == typeof(bool) && x.GetGetMethod(true) != null).SingleOrDefaultSafe(x =>
                        x.Name.Equals("active", StringComparison.InvariantCultureIgnoreCase) || x.Name.Equals("isActive", StringComparison.InvariantCultureIgnoreCase))?
                    .GetGetMethod(true);
                if (IsActiveMember == null)
                    Logger.DevkitServer.LogWarning(UIAccessTools.Source, $"Failed to find any 'active' or 'isActive' member for {type.Format()}.");
            }
            catch (Exception ex)
            {
                Logger.DevkitServer.LogWarning(UIAccessTools.Source, ex, $"Error finding any destroy methods for UI: {type.Format()}.");
                IsActiveMember = null;
            }
        }

        // ReSharper restore CoVariantArrayConversion
    }
    private static void RemoveMatchesFromBaseClasses(Type type, List<MethodInfo> matches)
    {
        if (matches.Any(x => x.DeclaringType == type))
        {
            matches.RemoveAll(x => x.DeclaringType != type);
        }
    }
}
#endif