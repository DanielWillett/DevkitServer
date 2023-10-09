using DevkitServer.Multiplayer.Networking;
using HarmonyLib;
using Pathfinding;
using System.Reflection;
using System.Reflection.Emit;
#if CLIENT
using DevkitServer.API.Abstractions;
using DevkitServer.API.Permissions;
using DevkitServer.Core.Permissions;
using DevkitServer.Players.UI;
#endif

namespace DevkitServer.Patches;
[HarmonyPatch]
[HarmonyPriority(-1)]
internal class NavigationPatches
{
    private static readonly NetCall<int> SendBakeNavRequest = new NetCall<int>(NetCalls.SendBakeNavRequest);

    private const string Source = "NAV PATCHES";
    internal static bool BlockBake;
#if SERVER
    internal static bool CanBake => true;
#else
    internal static bool CanBake => !DevkitServerModule.IsEditing || (!BlockBake && (VanillaPermissions.AllNavigation.Has() || VanillaPermissions.BakeNavigation.Has(false)));
#endif

    private static readonly MethodInfo CurrentCanBakeGetter = typeof(NavigationPatches).GetProperty(nameof(CanBake), BindingFlags.Static | BindingFlags.NonPublic)!.GetMethod;

    [HarmonyPatch(typeof(Flag), nameof(Flag.bakeNavigation))]
    [HarmonyTranspiler]
    [UsedImplicitly]
    private static IEnumerable<CodeInstruction> TranspileBakeNavigation(IEnumerable<CodeInstruction> instructions, MethodBase method, ILGenerator generator)
    {
        MethodInfo? originalMethod = typeof(AstarPath).GetMethod(nameof(AstarPath.ScanSpecific),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(NavGraph) },
            null);
        if (originalMethod == null)
        {
            Logger.LogWarning($"{method.Format()} - Unable to find method: " +
                FormattingUtil.FormatMethod(typeof(void), typeof(AstarPath), nameof(AstarPath.ScanSpecific),
                    new (Type type, string? name)[] { (typeof(NavGraph), "graph") }) + ".", method: Source);
        }

        MethodInfo? replacementMethod = typeof(AstarPath).GetMethod(nameof(AstarPath.ScanSpecific),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(NavGraph), typeof(OnScanStatus) },
            null);
        if (replacementMethod == null)
        {
            Logger.LogWarning($"{method.Format()} - Unable to find method: " +
                FormattingUtil.FormatMethod(typeof(void), typeof(AstarPath), nameof(AstarPath.ScanSpecific),
                    new (Type type, string? name)[] { (typeof(NavGraph), "graph"), (typeof(OnScanStatus), "statusCallback") }) + ".", method: Source);
        }

        ConstructorInfo? delegateConstructor = typeof(OnScanStatus)
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(x => x.GetParameters() is { Length: 2 } p && !p[0].ParameterType.IsValueType && p[1].ParameterType == typeof(nint));
        if (delegateConstructor == null)
        {
            Logger.LogWarning($"{method.Format()} - Unable to find constructor for {typeof(OnScanStatus).Format()}.", method: Source);
        }

        List<CodeInstruction> ins = instructions.ToList();

        bool patched = false;
        Label lbl = generator.DefineLabel();
#if CLIENT
        Label lbl2 = generator.DefineLabel();
#endif
        ins.Insert(0, new CodeInstruction(OpCodes.Call, CurrentCanBakeGetter));
        ins.Insert(1, new CodeInstruction(OpCodes.Brtrue, lbl));
        ins.Insert(2, new CodeInstruction(OpCodes.Call, Accessor.GetMethod(OnBakeNavigationWhileAlreadyBaking)));
        ins.Insert(3, new CodeInstruction(OpCodes.Ret));
#if CLIENT
        ins.Insert(4, new CodeInstruction(OpCodes.Call, Accessor.IsDevkitServerGetter));
#endif
        ins[4].labels.Add(lbl);
#if CLIENT
        ins.Insert(5, new CodeInstruction(OpCodes.Brfalse, lbl2));
        ins.Insert(6, new CodeInstruction(OpCodes.Ldarg_0));
        ins.Insert(7, new CodeInstruction(OpCodes.Call, Accessor.GetMethod(OnBakeNavigationRequest)));
        ins.Insert(8, new CodeInstruction(OpCodes.Ret));
        ins[9].labels.Add(lbl2);
#endif

        if (replacementMethod != null && originalMethod != null && delegateConstructor != null)
        {
            for (int i = 4; i < ins.Count; ++i)
            {
                if (!ins[i].Calls(originalMethod))
                    continue;

                ins.Insert(i, new CodeInstruction(OpCodes.Ldnull).MoveLabelsFrom(ins[i]));
                ins.Insert(i + 1, new CodeInstruction(OpCodes.Ldftn, Accessor.GetMethod(OnBakeNavigationProgressUpdate)));
                ins.Insert(i + 2, new CodeInstruction(OpCodes.Newobj, delegateConstructor));
                ins[i + 3] = new CodeInstruction(replacementMethod.GetCallRuntime(), replacementMethod);
                patched = true;
            }
        }

        if (!patched)
        {
            Logger.LogWarning($"{method.Format()} - Failed to add a scan handler to A* scan method.", method: Source);
        }

        return ins;
    }

    private static void OnBakeNavigationProgressUpdate(Progress progress)
    {
        Logger.LogInfo("[A*] (" + progress.progress.Format("P") + ") " + progress.description.Colorize(ConsoleColor.Gray) + ".");
    }
#if CLIENT
    private static void OnBakeNavigationRequest(Flag flag)
    {
        DevkitServerModule.AssertIsDevkitServerClient();

        SendBakeNavRequest?.Invoke(flag.GetHashCode());
    }
#endif
    private static void OnBakeNavigationWhileAlreadyBaking()
    {
        Logger.LogWarning(BlockBake ? "Tried to bake navigation while it's already baking." : "You do not have permission to bake navigation.", method: Source);
#if CLIENT
        UIMessage.SendEditorMessage(TranslationSource.DevkitServerMessageLocalizationSource, "AlreadyBakingNavigation");
#endif
    }
}