#if SERVER
using HarmonyLib;
using System.Reflection;
using System.Reflection.Emit;

namespace DevkitServer.Patches;
/*
 * After running for long enough, the server would run out of memory because the gizmo requests were not being dequeued.
 * This class removes all the queueing methods from RuntimeGizmos.
 */
internal static class ServerGizmoPatches
{
    private static readonly List<MethodInfo> Patched = new List<MethodInfo>();
    private static readonly MethodInfo TranspilerMethod = new Func<IEnumerable<CodeInstruction>, IEnumerable<CodeInstruction>>(Transpiler).Method;
    internal static void Patch()
    {
        Patched.Clear();
        Patched.AddRange(typeof(RuntimeGizmos).GetMethods(BindingFlags.Instance | BindingFlags.Public).Where(x => x.DeclaringType == typeof(RuntimeGizmos) && x.ReturnType == typeof(void)));
        foreach (MethodInfo method in Patched)
        {
            PatchesMain.Patcher.Patch(method, transpiler: new HarmonyMethod(TranspilerMethod));
            Logger.LogDebug($"Emptied method {method.Format()} on the server to help with performance.");
        }
    }
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> old) => new CodeInstruction[] { new CodeInstruction(OpCodes.Ret) }.Concat(old);
    internal static void Unpatch()
    {
        foreach (MethodInfo method in Patched)
            PatchesMain.Patcher.Unpatch(method, TranspilerMethod);

        Logger.LogDebug($"Unpatched {typeof(RuntimeGizmos).Format()} patches.");
        Patched.Clear();
    }
}
#endif