#if CLIENT
using HarmonyLib;

namespace DevkitServer.API.UI;

/// <summary>
/// Adds custom patch handling for UI types.
/// </summary>
public interface ICustomUIHandler
{
    /// <summary>
    /// Checks if the handler has been patched.
    /// </summary>
    bool HasBeenInitialized { get; internal set; }

    /// <summary>
    /// Patch any needed methods in the handler.
    /// </summary>
    void Patch(Harmony patcher);

    /// <summary>
    /// Fully unpatch any patched methods in the handler.
    /// </summary>
    void Unpatch(Harmony patcher);
}
#endif
