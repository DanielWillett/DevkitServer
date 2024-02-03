using System.Reflection;
using DevkitServer.Core;
using DevkitServer.Patches;
using HarmonyLib;

namespace DevkitServer.API.Abstractions;
public static class OptionalPatches
{
    /// <summary>
    /// Unpatches a patch and sets the reference to <see langword="null"/>.
    /// </summary>
    public static void Unpatch(ref IOptionalPatch? patch)
    {
        patch?.Unpatch();
        patch = null;
    }
    internal static IOptionalPatch? Prefix(object source, MethodInfo? originalMethod, MethodInfo prefix, Func<string> formatMethod, string? failExplaination = null)
    {
        return Prefix(PatchesMain.Patcher, Logger.DevkitServer, source, originalMethod, prefix, formatMethod, failExplaination);
    }

    /// <summary>
    /// Add a prefix to a method and return an <see cref="IOptionalPatch"/> object.
    /// </summary>
    public static IOptionalPatch? Prefix(Harmony patcher, IDevkitServerLogger logger, object source, MethodInfo? originalMethod, MethodInfo prefix, Func<string> formatMethod, string? failExplaination = null)
    {
        if (prefix == null)
            throw new InvalidOperationException($"Prefix not available for method {originalMethod}.");

        if (originalMethod == null)
        {
            string log = $"Unable to find {formatMethod()} for prefixing with {prefix.Format()}.";
            if (!string.IsNullOrEmpty(failExplaination))
                log += " " + failExplaination;

            if (source is not ILogSource logSource)
                Logger.DevkitServer.LogWarning(source?.ToString() ?? nameof(Prefix), log);
            else
                Logger.DevkitServer.LogWarning(logSource, log);
            return null;
        }

        try
        {
            patcher.Patch(originalMethod, prefix: new HarmonyMethod(prefix));
            if (Logger.Debug)
            {
                string log = $"Prefixed {formatMethod()} with {prefix.Format()}.";
                if (source is not ILogSource logSource)
                    Logger.DevkitServer.LogDebug(source?.ToString() ?? nameof(Prefix), log);
                else
                    Logger.DevkitServer.LogDebug(logSource, log);
            }
            return new OptionalPatch(patcher, originalMethod, prefix, logger, source);
        }
        catch (Exception ex)
        {
            string log = $"Failed to prefix {formatMethod()} with {prefix.Format()}.";
            if (!string.IsNullOrEmpty(failExplaination))
                log += " " + failExplaination;

            if (source is not ILogSource logSource)
                Logger.DevkitServer.LogWarning(source?.ToString() ?? nameof(Prefix), ex, log);
            else
                Logger.DevkitServer.LogWarning(logSource, ex, log);
            return null;
        }
    }
    internal static IOptionalPatch? Postfix(object source, MethodInfo? originalMethod, MethodInfo postfix, Func<string> formatMethod, string? failExplaination = null)
    {
        return Postfix(PatchesMain.Patcher, Logger.DevkitServer, source, originalMethod, postfix, formatMethod, failExplaination);
    }

    /// <summary>
    /// Add a postfix to a method and return an <see cref="IOptionalPatch"/> object.
    /// </summary>
    public static IOptionalPatch? Postfix(Harmony patcher, IDevkitServerLogger logger, object source, MethodInfo? originalMethod, MethodInfo postfix, Func<string> formatMethod, string? failExplaination = null)
    {
        if (postfix == null)
            throw new InvalidOperationException($"Postfix not available for method {originalMethod}.");

        if (originalMethod == null)
        {
            string log = $"Unable to find {formatMethod()} for postfixing with {postfix.Format()}.";
            if (!string.IsNullOrEmpty(failExplaination))
                log += " " + failExplaination;

            if (source is not ILogSource logSource)
                Logger.DevkitServer.LogWarning(source?.ToString() ?? nameof(Postfix), log);
            else
                Logger.DevkitServer.LogWarning(logSource, log);
            return null;
        }

        try
        {
            patcher.Patch(originalMethod, postfix: new HarmonyMethod(postfix));
            if (Logger.Debug)
            {
                string log = $"Postfixed {formatMethod()} with {postfix.Format()}.";
                if (source is not ILogSource logSource)
                    Logger.DevkitServer.LogDebug(source?.ToString() ?? nameof(Prefix), log);
                else
                    Logger.DevkitServer.LogDebug(logSource, log);
            }
            return new OptionalPatch(patcher, originalMethod, postfix, logger, source);
        }
        catch (Exception ex)
        {
            string log = $"Failed to postfix {formatMethod()} with {postfix.Format()}.";
            if (!string.IsNullOrEmpty(failExplaination))
                log += " " + failExplaination;

            if (source is not ILogSource logSource)
                Logger.DevkitServer.LogWarning(source?.ToString() ?? nameof(Postfix), ex, log);
            else
                Logger.DevkitServer.LogWarning(logSource, ex, log);
            return null;
        }
    }
    internal static IOptionalPatch? Transpile(object source, MethodInfo? originalMethod, MethodInfo transpiler, Func<string> formatMethod, string? failExplaination = null)
    {
        return Transpile(PatchesMain.Patcher, Logger.DevkitServer, source, originalMethod, transpiler, formatMethod, failExplaination);
    }

    /// <summary>
    /// Add a transpiler to a method and return an <see cref="IOptionalPatch"/> object.
    /// </summary>
    public static IOptionalPatch? Transpile(Harmony patcher, IDevkitServerLogger logger, object source, MethodInfo? originalMethod, MethodInfo transpiler, Func<string> formatMethod, string? failExplaination = null)
    {
        if (transpiler == null)
            throw new InvalidOperationException($"Transpiler not available for method {originalMethod}.");

        if (originalMethod == null)
        {
            string log = $"Unable to find {formatMethod()} for transpiling with {transpiler.Format()}.";
            if (!string.IsNullOrEmpty(failExplaination))
                log += " " + failExplaination;

            if (source is not ILogSource logSource)
                Logger.DevkitServer.LogWarning(source?.ToString() ?? nameof(Transpile), log);
            else
                Logger.DevkitServer.LogWarning(logSource, log);
            return null;
        }

        try
        {
            patcher.Patch(originalMethod, transpiler: new HarmonyMethod(transpiler));
            if (Logger.Debug)
            {
                string log = $"Transpiled {formatMethod()} with {transpiler.Format()}.";
                if (source is not ILogSource logSource)
                    Logger.DevkitServer.LogDebug(source?.ToString() ?? nameof(Prefix), log);
                else
                    Logger.DevkitServer.LogDebug(logSource, log);
            }
            return new OptionalPatch(patcher, originalMethod, transpiler, logger, source);
        }
        catch (Exception ex)
        {
            string log = $"Failed to transpile {formatMethod()} with {transpiler.Format()}.";
            if (!string.IsNullOrEmpty(failExplaination))
                log += " " + failExplaination;

            if (source is not ILogSource logSource)
                Logger.DevkitServer.LogWarning(source?.ToString() ?? nameof(Transpile), ex, log);
            else
                Logger.DevkitServer.LogWarning(logSource, ex, log);
            return null;
        }
    }
    internal static IOptionalPatch? Finalize(object source, MethodInfo? originalMethod, MethodInfo finalizer, Func<string> formatMethod, string? failExplaination = null)
    {
        return Finalize(PatchesMain.Patcher, Logger.DevkitServer, source, originalMethod, finalizer, formatMethod, failExplaination);
    }

    /// <summary>
    /// Add a finalizer to a method and return an <see cref="IOptionalPatch"/> object.
    /// </summary>
    public static IOptionalPatch? Finalize(Harmony patcher, IDevkitServerLogger logger, object source, MethodInfo? originalMethod, MethodInfo finalizer, Func<string> formatMethod, string? failExplaination = null)
    {
        if (finalizer == null)
            throw new InvalidOperationException($"Finalizer not available for method {originalMethod}.");

        if (originalMethod == null)
        {
            string log = $"Unable to find {formatMethod()} for transpiling with {finalizer.Format()}.";
            if (!string.IsNullOrEmpty(failExplaination))
                log += " " + failExplaination;

            if (source is not ILogSource logSource)
                Logger.DevkitServer.LogWarning(source?.ToString() ?? nameof(Finalize), log);
            else
                Logger.DevkitServer.LogWarning(logSource, log);
            return null;
        }

        try
        {
            patcher.Patch(originalMethod, finalizer: new HarmonyMethod(finalizer));
            if (Logger.Debug)
            {
                string log = $"Finalized {formatMethod()} with {finalizer.Format()}.";
                if (source is not ILogSource logSource)
                    Logger.DevkitServer.LogDebug(source?.ToString() ?? nameof(Prefix), log);
                else
                    Logger.DevkitServer.LogDebug(logSource, log);
            }
            return new OptionalPatch(patcher, originalMethod, finalizer, logger, source);
        }
        catch (Exception ex)
        {
            string log = $"Failed to finalize {formatMethod()} with {finalizer.Format()}.";
            if (!string.IsNullOrEmpty(failExplaination))
                log += " " + failExplaination;

            if (source is not ILogSource logSource)
                Logger.DevkitServer.LogWarning(source?.ToString() ?? nameof(Finalize), ex, log);
            else
                Logger.DevkitServer.LogWarning(logSource, ex, log);
            return null;
        }
    }
}
