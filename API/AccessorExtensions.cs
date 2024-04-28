using DanielWillett.ReflectionTools;
using DanielWillett.ReflectionTools.Emit;
using DevkitServer.Patches;
using HarmonyLib;
using StackCleaner;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;

namespace DevkitServer.API;

/// <summary>
/// Reflection utilities for accessing private or internal members, acts as an extension to <see cref="Accessor"/>.
/// </summary>
public static class AccessorExtensions
{
    private static Assembly? _sdgAssembly;
    private static Assembly? _devkitServerAssembly;

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

    public static bool TryMarkLabel(this IOpCodeEmitter emitter, Label? label)
    {
        if (!label.HasValue)
            return false;
        
        emitter.MarkLabel(label.Value);
        return true;

    }

    /// <summary>
    /// The instance of harmony used for patching in <see cref="DevkitServerModule"/>. This shouldn't be used for patching.
    /// </summary>
    public static Harmony DevkitServerModulePatcher => PatchesMain.Patcher;

    /// <summary>
    /// Safely gets the reflection method info of the passed method. Works best with static methods.<br/><br/>
    /// <code>
    /// HarmonyMethod? method = Accessor.GetHarmonyMethod(Guid.Parse);
    /// </code>
    /// </summary>
    /// <returns>A harmony method info of a passed delegate.</returns>
    [Pure]
    public static HarmonyMethod? GetHarmonyMethod(this IAccessor accessor, [InstantHandle] Delegate @delegate)
    {
        MethodInfo? method = accessor.GetMethod(@delegate);
        return method == null ? null : new HarmonyMethod(method);
    }

    /// <summary>
    /// Tries to find a lambda method that's defined in <paramref name="definingMethod"/>. Optinally define a type and parameter array to be more specific.
    /// </summary>
    /// <remarks>The effectiveness of this method depends on the how the compiler of the original code implements lambda methods.</remarks>
    public static bool TryGetLambdaMethod(this IAccessor accessor, MethodInfo definingMethod, out MethodInfo method, Type[]? types = null, ParameterModifier[]? parameters = null)
    {
        method = null!;

        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance;

        Type? proxyType = definingMethod.DeclaringType?.GetNestedType("<>c", flags) ?? definingMethod.DeclaringType;

        if (proxyType == null)
            return false;

        if (accessor.LogDebugMessages)
            accessor.Logger?.LogDebug(nameof(TryGetLambdaMethod), $"Looking for lambda match in {proxyType.Format()}.");

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
            if (accessor.LogDebugMessages)
                accessor.Logger?.LogDebug(nameof(TryGetLambdaMethod), "No match when binding to lambda method.");
            return false;
        }

        if (types == null)
        {
            method = (matches[0] as MethodInfo)!;
            if (matches.Count == 1)
                return true;

            if (accessor.LogDebugMessages)
                accessor.Logger?.LogDebug(nameof(TryGetLambdaMethod), "Ambiguous match when binding to lambda method. Consider passing types.");
            
            return false;
        }

        try
        {
            method = (Type.DefaultBinder!.SelectMethod(flags, matches.ToArray(), types, parameters) as MethodInfo)!;
            return method != null;
        }
        catch (Exception ex)
        {
            if (accessor.LogDebugMessages)
                accessor.Logger?.LogDebug(nameof(TryGetLambdaMethod), "Exception binding to methods - " + ex.GetType().Name + " " + ex.Message + ".");
        }

        return false;
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
    public static MethodInfo LogDebug => _logDebug ??= Accessor.GetMethod(ReflectionLogDebug) ?? throw new MemberAccessException("Unable to find method.");

    /// <summary>Logs an info message with no source. Signature: (string message, ConsoleColor color).</summary>
    /// <remarks>Lazily cached.</remarks>
    /// <exception cref="MemberAccessException"/>
    public static MethodInfo LogInfo => _logInfo ??= Accessor.GetMethod(ReflectionLogInfo) ?? throw new MemberAccessException("Unable to find method.");

    /// <summary>Logs a warning message with no source. Signature: (string message, ConsoleColor color, string source).</summary>
    /// <remarks>Lazily cached.</remarks>
    /// <exception cref="MemberAccessException"/>
    public static MethodInfo LogWarning => _logWarning ??= Accessor.GetMethod(ReflectionLogWarning) ?? throw new MemberAccessException("Unable to find method.");

    /// <summary>Logs an error message with no source. Signature: (string message, ConsoleColor color, string source).</summary>
    /// <remarks>Lazily cached.</remarks>
    /// <exception cref="MemberAccessException"/>
    public static MethodInfo LogError => _logError ??= Accessor.GetMethod(ReflectionLogError) ?? throw new MemberAccessException("Unable to find method.");

    /// <summary>Logs a fatal message with no source. Signature: (string message, ConsoleColor color, string source).</summary>
    /// <remarks>Lazily cached.</remarks>
    /// <exception cref="MemberAccessException"/>
    public static MethodInfo LogFatal => _logFatal ??= Accessor.GetMethod(ReflectionLogFatal) ?? throw new MemberAccessException("Unable to find method.");

    /// <summary>Logs an exception with no source. Signature: (Exception ex, string source).</summary>
    /// <remarks>Lazily cached.</remarks>
    /// <exception cref="MemberAccessException"/>
    public static MethodInfo LogException => _logException ??= Accessor.GetMethod(ReflectionLogException) ?? throw new MemberAccessException("Unable to find method.");

    /// <summary>Logs an exception with no source. Signature: (Exception ex, string message, ConsoleColor color, string source).</summary>
    /// <remarks>Lazily cached.</remarks>
    /// <exception cref="MemberAccessException"/>
    public static MethodInfo LogErrorException => _logException ??= Accessor.GetMethod(ReflectionLogErrorException) ?? throw new MemberAccessException("Unable to find method.");

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
        typeof(StackTrace).GetConstructor(BindingFlags.Instance | BindingFlags.Public, null, [ typeof(int) ],
            null) ?? throw new MemberAccessException("Unable to find StackTrace.StackTrace(int).");

    /// <summary><see cref="string.Concat(string, string)"/>.</summary>
    /// <remarks>Lazily cached.</remarks>
    /// <exception cref="MemberAccessException"/>
    public static MethodInfo Concat2StringsMethod => _concat2Strings ??=
        typeof(string).GetMethod(nameof(string.Concat), BindingFlags.Static | BindingFlags.Public, null,
            [ typeof(string), typeof(string) ], null)
        ?? throw new MemberAccessException("Unable to find string.Concat(string, string).");

    /// <summary><see cref="StackTraceCleaner.GetString(StackTrace)"/>.</summary>
    /// <remarks>Lazily cached.</remarks>
    /// <exception cref="MemberAccessException"/>
    public static MethodInfo StackTraceCleanerGetStringMethod => _getStackTraceString ??=
        typeof(StackTraceCleaner).GetMethod(nameof(StackTraceCleaner.GetString), BindingFlags.Instance | BindingFlags.Public, null,
            [ typeof(StackTrace) ], null)
        ?? throw new MemberAccessException("Unable to find StackTraceCleaner.GetString(StackTrace).");

    /// <summary><see cref="Logger.StackCleaner"/>.</summary>
    /// <remarks>Lazily cached.</remarks>
    /// <exception cref="MemberAccessException"/>
    public static FieldInfo LoggerStackCleanerField => _getStackCleaner ??=
        typeof(Logger).GetField(nameof(Logger.StackCleaner), BindingFlags.Static | BindingFlags.Public)
        ?? throw new MemberAccessException("Unable to find Logger.StackCleaner.");
}