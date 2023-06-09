#if CLIENT
using DevkitServer.Core.Permissions;
using DevkitServer.Multiplayer.Actions;
using DevkitServer.Players.UI;
using HarmonyLib;
using JetBrains.Annotations;
using SDG.Framework.Devkit.Transactions;
using System.Reflection;
using System.Reflection.Emit;
using Action = System.Action;

namespace DevkitServer.Patches;
[HarmonyPatch]
internal static class LevelObjectPatches
{
    [HarmonyPatch(typeof(EditorObjects), "Update")]
    [HarmonyTranspiler]
    [UsedImplicitly]
    private static IEnumerable<CodeInstruction> LevelObjectsUpdateTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase method)
    {
        Type eo = typeof(EditorObjects);
        Type lo = typeof(LevelObjects);
        Type dtu = typeof(DevkitTransactionUtility);
        // Type vp = typeof(VanillaPermissions);

        MethodInfo pointSelectionInvoker = new Func<bool>(CheckMoveSelectionPermission).Method;
        MethodInfo undoInvoker = new Action(TransactionPatches.OnUndoRequested).Method;
        MethodInfo redoInvoker = new Action(TransactionPatches.OnRedoRequested).Method;

        MethodInfo? pointSelection = eo.GetMethod("pointSelection", BindingFlags.Static | BindingFlags.Public, null, Array.Empty<Type>(), null);
        if (pointSelection == null)
        {
            Logger.LogWarning($"{method.Format()} - Unable to find method: EditorObjects.pointSelection.", method: "CLIENT EVENTS");
            DevkitServerModule.Fault();
        }

        MethodInfo? clearSelection = eo.GetMethod("clearSelection", BindingFlags.Static | BindingFlags.NonPublic, null, Array.Empty<Type>(), null);
        if (clearSelection == null)
        {
            Logger.LogWarning($"{method.Format()} - Unable to find method: EditorObjects.clearSelection.", method: "CLIENT EVENTS");
            DevkitServerModule.Fault();
        }

        MethodInfo? undo = lo.GetMethod("undo", BindingFlags.Static | BindingFlags.Public, null, Array.Empty<Type>(), null);
        if (undo == null)
        {
            Logger.LogWarning($"{method.Format()} - Unable to find method: LevelObjects.undo.", method: "CLIENT EVENTS");
            DevkitServerModule.Fault();
        }

        MethodInfo? redo = lo.GetMethod("redo", BindingFlags.Static | BindingFlags.Public, null, Array.Empty<Type>(), null);
        if (redo == null)
        {
            Logger.LogWarning($"{method.Format()} - Unable to find method: LevelObjects.redo.", method: "CLIENT EVENTS");
            DevkitServerModule.Fault();
        }

        MethodInfo? recordDestruction = dtu.GetMethod(nameof(DevkitTransactionUtility.recordDestruction), BindingFlags.Static | BindingFlags.Public, null, new Type[] { typeof(GameObject) }, null);
        if (recordDestruction == null)
        {
            Logger.LogWarning($"{method.Format()} - Unable to find method: DevkitTransactionUtility.recordDestruction.", method: "CLIENT EVENTS");
            DevkitServerModule.Fault();
        }

        Label stLbl = generator.DefineLabel();

        List<CodeInstruction> ins = new List<CodeInstruction>(instructions);
        int index = 1;
        while (index <= ins.Count && ins[index - 1].opcode != OpCodes.Ret)
            ++index;
        // limits to 60 actions per second
        ins.Insert(index, new CodeInstruction(OpCodes.Call, Accessor.IsDevkitServerGetter));
        ins.Insert(index + 1, new CodeInstruction(OpCodes.Brfalse_S, stLbl));
        ins.Insert(index + 2, new CodeInstruction(OpCodes.Call, Accessor.GetRealtimeSinceStartup));
        ins.Insert(index + 3, new CodeInstruction(OpCodes.Ldsfld, EditorActions.LocalLastActionField));
        ins.Insert(index + 4, new CodeInstruction(OpCodes.Sub));
        ins.Insert(index + 5, new CodeInstruction(OpCodes.Ldc_R4, 1f / 60f));
        ins.Insert(index + 6, new CodeInstruction(OpCodes.Bge_S, stLbl));
        ins.Insert(index + 7, new CodeInstruction(OpCodes.Ret));
        for (int i = 0; i < ins.Count; ++i)
        {
            CodeInstruction c = ins[i];
            if (i == 0)
                c.labels.Add(stLbl);

            if (i < ins.Count - 1 && clearSelection != null && ins[i].Calls(clearSelection) && (redo != null && ins[i + 1].Calls(redo) || undo != null && ins[i + 1].Calls(undo)))
            {
                ins.RemoveAt(i + 1);
                ins[i] = new CodeInstruction(OpCodes.Call, redo != null && ins[i + 1].Calls(redo) ? redoInvoker : undoInvoker);
            }

            if (pointSelection != null && c.Calls(pointSelection))
            {
                ins.Insert(i, new CodeInstruction(OpCodes.Call, pointSelectionInvoker));
                Label lbl = generator.DefineLabel();
                ins.Insert(i + 1, new CodeInstruction(OpCodes.Brtrue, lbl));
                ins.Insert(i + 2, new CodeInstruction(OpCodes.Ret));
                i += 3;
                ins[i + 3].labels.Add(lbl);
            }
        }

        return ins;
    }
    private static readonly StaticGetter<List<EditorSelection>> GetEditorObjectSelection = Accessor.GenerateStaticGetter<EditorObjects, List<EditorSelection>>("selection", throwOnError: true)!;
    private static bool CheckMoveSelectionPermission()
    {
        List<EditorSelection> selection = GetEditorObjectSelection();
        for (int i = 0; i < selection.Count; ++i)
        {
            LevelObject? obj = LevelObjectUtil.FindObject(selection[i].transform);
            if (obj == null || !LevelObjectUtil.CheckMovePermission(obj.instanceID))
            {
                UIMessage.SendNoPermissionMessage(VanillaPermissions.MoveSavedObjects);
                return false;
            }
        }

        return true;
    }
}
#endif