#if CLIENT
using HarmonyLib;
using System.Reflection;

namespace DevkitServer.API.UI.Extensions;

/// <summary>
/// Stores information about a UI handler patch (open, close, destroy, or initialize).
/// </summary>
public class UIExtensionPatch
{
    /// <summary>
    /// Original method that was patched.
    /// </summary>
    public MethodBase Original { get; }

    /// <summary>
    /// Method that was patched onto <see cref="Original"/>.
    /// </summary>
    public MethodInfo Patch { get; }

    /// <summary>
    /// Type of patch used.
    /// </summary>
    public HarmonyPatchType Type { get; }
    internal UIExtensionPatch(MethodBase original, MethodInfo patch, HarmonyPatchType type)
    {
        Original = original;
        Patch = patch;
        Type = type;
    }
}
#endif