#if SERVER
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;

namespace DevkitServer.Patches;
internal static class ServerGizmoPatches
{
    private static readonly List<MethodInfo> Patched = new List<MethodInfo>();
    private static readonly MethodInfo TranspilerMethod = new Func<IEnumerable<CodeInstruction>>(Transpiler).Method;
    internal static void Patch()
    {
        Patched.Clear();
        Patched.AddRange(typeof(RuntimeGizmos).GetMethods(BindingFlags.Instance | BindingFlags.Public).Where(x => x.ReturnType == typeof(void)));
        foreach (MethodInfo method in Patched)
        {
            PatchesMain.Patcher.Patch(method, transpiler: new HarmonyMethod(TranspilerMethod));
            Logger.LogDebug($"Emptied method {method.Format()} on the server to help with performance.");
        }
    }
    private static IEnumerable<CodeInstruction> Transpiler() => Array.Empty<CodeInstruction>();
    internal static void Unpatch()
    {
        foreach (MethodInfo method in Patched)
            PatchesMain.Patcher.Unpatch(method, TranspilerMethod);

        Logger.LogDebug($"Unpatched {typeof(RuntimeGizmos).Format()} patches.");
        Patched.Clear();
    }
}
#endif