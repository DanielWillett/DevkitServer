#if CLIENT
using System.Reflection;

namespace DevkitServer.API.UI;

/// <summary>
/// Stores information about a visibility method (open, close, initialize, or destroy).
/// </summary>
public readonly struct UIVisibilityMethodInfo
{
    /// <summary>
    /// The actual method.
    /// </summary>
    public MethodBase Method { get; }

    /// <summary>
    /// If it has parameters.
    /// </summary>
    public bool IsParameterized { get; }

    /// <summary>
    /// If it is static.
    /// </summary>
    public bool IsStatic { get; }

    internal UIVisibilityMethodInfo(MethodBase method, bool isParameterized, bool isStatic)
    {
        Method = method;
        IsParameterized = isParameterized;
        IsStatic = isStatic;
    }
}
#endif