#if CLIENT
using DevkitServer.Players;
using HarmonyLib;
using JetBrains.Annotations;
using SDG.Framework.Devkit.Transactions;
using System.Reflection;
using System.Reflection.Emit;

namespace DevkitServer.Patches;
[HarmonyPatch]
internal static class TransactionPatches
{
    [HarmonyPatch("EditorInteract", "Update")]
    [HarmonyTranspiler]
    [UsedImplicitly]
    private static IEnumerable<CodeInstruction> EditorInteractUpdateTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase method)
    {
        Type tm = typeof(DevkitTransactionManager);
        Type tp = typeof(TransactionPatches);
        MethodInfo? redoMethod = tm.GetMethod(nameof(DevkitTransactionManager.redo), BindingFlags.Public | BindingFlags.Static);
        if (redoMethod == null)
        {
            Logger.LogWarning("Unable to find method: DevkitTransactionManager.redo.", method: "CLIENT EVENTS");
            DevkitServerModule.Fault();
        }

        MethodInfo? undoMethod = tm.GetMethod(nameof(DevkitTransactionManager.undo), BindingFlags.Public | BindingFlags.Static);
        if (undoMethod == null)
        {
            Logger.LogWarning("Unable to find method: DevkitTransactionManager.undo.", method: "CLIENT EVENTS");
            DevkitServerModule.Fault();
        }
        MethodInfo undoInvoker = tp.GetMethod(nameof(OnUndoRequested), BindingFlags.Static | BindingFlags.NonPublic)!;
        MethodInfo redoInvoker = tp.GetMethod(nameof(OnRedoRequested), BindingFlags.Static | BindingFlags.NonPublic)!;

        List<CodeInstruction> ins = new List<CodeInstruction>(instructions);
        bool undo = false, redo = false;
        for (int i = 0; i < ins.Count; ++i)
        {
            CodeInstruction c = ins[i];
            if (!redo && redoMethod != null && c.Calls(redoMethod) && redoInvoker != null)
            {
                CodeInstruction c2 = new CodeInstruction(OpCodes.Call, redoInvoker);
                c2.labels.AddRange(c.labels);
                c2.blocks.AddRange(c.blocks);
                yield return c2;
                redo = true;
                if (redoMethod.ReturnType != typeof(void))
                    i++; // remove pop
                continue;
            }

            if (!undo && undoMethod != null && c.Calls(undoMethod) && undoInvoker != null)
            {
                CodeInstruction c2 = new CodeInstruction(OpCodes.Call, undoInvoker);
                c2.labels.AddRange(c.labels);
                c2.blocks.AddRange(c.blocks);
                yield return c2;
                undo = true;
                if (undoMethod.ReturnType != typeof(void))
                    i++; // remove pop
                continue;
            }

            yield return c;
        }
        if (!undo || !redo)
        {
            Logger.LogWarning("Patching error for " + method.Format() + $" Redo: {redo.Format()}, Undo: {undo.Format()}.", method: "CLIENT EVENTS");
            DevkitServerModule.Fault();
        }
    }
    internal static void OnUndoRequested()
    {
        if (EditorUser.User != null && EditorUser.User.Transactions != null)
            EditorUser.User.Transactions.RequestUndo();
    }
    internal static void OnRedoRequested()
    {
        if (EditorUser.User != null && EditorUser.User.Transactions != null)
            EditorUser.User.Transactions.RequestRedo();
    }

}
#endif