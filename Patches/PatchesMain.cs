using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using JetBrains.Annotations;

namespace DevkitServer.Patches;
[HarmonyPatch]
internal static class PatchesMain
{
    // private static readonly MethodInfo logMethod = typeof(Logger).GetMethod(nameof(Logger.LogInfo))!;
    public const string HarmonyId = "dw.devkitserver";
    internal static Harmony Patcher { get; private set; } = null!;
    internal static void Init()
    {
        try
        {
            Patcher = new Harmony(HarmonyId);
            Patcher.PatchAll();
            Logger.LogInfo("Patched");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);
        }
    }
    internal static void Unpatch()
    {
        try
        {
            Patcher.UnpatchAll(HarmonyId);
            Logger.LogInfo("Unpatched");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);
        }
    }
#if SERVER
    /*
    [HarmonyPatch(typeof(LevelLighting), nameof(LevelLighting.updateLocal), typeof(Vector3), typeof(float), typeof(IAmbianceNode))]
    [HarmonyPrefix]
    private static bool UpdateLighting(Vector3 point, float windOverride, IAmbianceNode effectNode) => false;
    [HarmonyPatch("EditorInteract", "Update", MethodType.Normal)]
    [HarmonyPrefix]
    private static bool EditorInteractUpdate() => false;
    [HarmonyPatch("EditorUI", "Start", MethodType.Normal)]
    [HarmonyPrefix]
    private static bool EditorUIStart() => false;
    [HarmonyPatch("MainCamera", "Awake", MethodType.Normal)]
    [HarmonyPrefix]
    private static bool MainCameraAwake() => false;
    */

    //[HarmonyPatch(typeof(Provider), "onDedicatedUGCInstalled")]
    [UsedImplicitly]
    private static class DedicatedUGCTranspiler
    {
        private static readonly MethodInfo lvlLoad = typeof(Level).GetMethod(nameof(Level.load))!;
        private static readonly MethodInfo lvlEdit = typeof(Level).GetMethod(nameof(Level.edit))!;
        //[HarmonyTranspiler]
        [UsedImplicitly]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (CodeInstruction instr in instructions)
            {
                if (instr.Calls(lvlLoad))
                {
                    yield return new CodeInstruction(OpCodes.Pop);
                    yield return new CodeInstruction(OpCodes.Call, lvlEdit);
                }
                else
                    yield return instr;
            }
        }
    }
#endif
}
