using DevkitServer.API.Abstractions;
using HarmonyLib;
using System.Reflection;

namespace DevkitServer.Core;
public class OptionalPatch(Harmony patcher, MethodBase originalMethod, MethodInfo patch, IDevkitServerLogger logger, object? source) : IOptionalPatch
{
    public MethodBase Method { get; } = originalMethod;
    public MethodInfo Patch { get; } = patch;
    public bool Unpatch()
    {
        try
        {
            patcher.Unpatch(Method, Patch);
            return true;
        }
        catch (Exception ex)
        {
            if (source is not ILogSource logSource)
                logger.LogWarning(source?.ToString() ?? nameof(Unpatch), ex, $"Failed to unpatch {Method.Format()}.");
            else
                logger.LogWarning(logSource, ex, $"Failed to unpatch {Method.Format()}.");
            return false;
        }
    }
}
