using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using DevkitServer.Multiplayer.Networking;
using HarmonyLib;
using JetBrains.Annotations;

namespace DevkitServer.Players.UI;
#if CLIENT
[HarmonyPatch]
#endif
[EarlyTypeInit]
public static class UIMessage
{
    private static string? _customText;
    /// <summary>
    /// <see cref="EEditorMessage"/> value used for a custom message.
    /// </summary>
    public static EEditorMessage CustomMessage { get; } = (EEditorMessage)typeof(EEditorMessage).GetFields().Length;

    private static readonly NetCall<string> SendEditorUIMessage = new NetCall<string>((ushort)NetCalls.EditorUIMessage);
#if CLIENT
    [HarmonyPatch(typeof(EditorUI), nameof(EditorUI.message))]
    [HarmonyTranspiler]
    [UsedImplicitly]
    private static IEnumerable<CodeInstruction> TranspileMessage(IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase method)
    {
        FieldInfo? messageBox = typeof(EditorUI).GetField("messageBox", BindingFlags.Static | BindingFlags.NonPublic);
        if (messageBox == null)
        {
            Logger.LogWarning("Unable to find field: EditorUI.messageBox.");
        }
        FieldInfo? customText = typeof(UIMessage).GetField(nameof(_customText), BindingFlags.Static | BindingFlags.NonPublic);
        if (customText == null)
        {
            Logger.LogWarning("Unable to find field: UIMessage._customText.");
        }
        MethodInfo? setText = typeof(ISleekLabel).GetProperty(nameof(ISleekLabel.text), BindingFlags.Instance | BindingFlags.Public)?.SetMethod;
        if (setText == null)
        {
            Logger.LogWarning("Unable to find method: ISleekLabel.text.");
        }
        List<CodeInstruction> ins = new List<CodeInstruction>(instructions);
        for (int i = 0; i < ins.Count; ++i)
        {
            CodeInstruction instr = ins[i];
            if (i == ins.Count - 1 && messageBox != null && setText != null && customText != null)
            {
                CodeInstruction c = new CodeInstruction(OpCodes.Ldarg_0);
                c.labels.AddRange(instr.labels);
                yield return c;
                c = DevkitServerUtility.LoadConstantI4((int)CustomMessage);
                yield return c;
                Label lbl = generator.DefineLabel();
                Label lbl2 = generator.DefineLabel();
                yield return new CodeInstruction(OpCodes.Bne_Un, lbl);

                yield return new CodeInstruction(OpCodes.Ldsfld, messageBox);
                yield return new CodeInstruction(OpCodes.Ldsfld, customText);
                yield return new CodeInstruction(OpCodes.Dup);
                yield return new CodeInstruction(OpCodes.Brfalse, lbl2);
                yield return new CodeInstruction(OpCodes.Callvirt, setText);

                yield return new CodeInstruction(OpCodes.Ret);

                c = new CodeInstruction(OpCodes.Pop);
                c.labels.Add(lbl2);
                yield return c;
                yield return new CodeInstruction(OpCodes.Pop);

                instr.labels.Clear();
                instr.labels.Add(lbl);
                Logger.LogDebug("Added custom message check to " + method.Format() + ".");
            }
            
            yield return instr;
        }
    }
    [NetCall(NetCallSource.FromServer, (ushort)NetCalls.EditorUIMessage)]
    private static void ReceiveEditorUIMessage(MessageContext ctx, string message)
    {
        ctx.Acknowledge(SendEditorMessage(message) ? StandardErrorCode.Success : StandardErrorCode.GenericError);
    }
#endif
    /// <summary>
    /// Send a toast message to a user, or the current user when called client-side.
    /// </summary>
    /// <param name="message">Text to show. Not formatted at all.</param>
    /// <returns>Client-side or server-side. Must be ran on main thread.</returns>
    /// <exception cref="NotSupportedException">Ran on non-game thread.</exception>
    public static
#if CLIENT
        bool
#else
        void
#endif
        SendEditorMessage(
#if SERVER
        EditorUser user,
#endif
        string message)
    {
#if SERVER
        SendEditorUIMessage.Invoke(user.Connection, message);
#endif
#if CLIENT
        ThreadUtil.assertIsGameThread();

        try
        {
            _customText = message;
            EditorUI.message(CustomMessage);
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError("Error setting EditorUI message.");
            Logger.LogError(ex);
            return false;
        }
        finally
        {
            _customText = null;
        }
#endif
    }
}