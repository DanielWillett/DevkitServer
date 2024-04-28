#if CLIENT
using DanielWillett.ReflectionTools;
using DevkitServer.API.UI;
using HarmonyLib;
using System.Reflection;
using System.Reflection.Emit;

namespace DevkitServer.Core.UI.Handlers;

internal class CustomNodeMenuHandler() : CustomSelectionToolMenuHandler(UIAccessTools.EditorEnvironmentNodesUIType, "EditorEnvironmentNodesUI");
internal class CustomVolumeMenuHandler() : CustomSelectionToolMenuHandler(UIAccessTools.EditorVolumesUIType, "EditorVolumesUI");
internal abstract class CustomSelectionToolMenuHandler(Type? type, string backupName) : ICustomOnCloseUIHandler, ICustomOnOpenUIHandler
{
    private const string Source = UIAccessTools.Source + ".SELECTION TOOL HANDLER";
    
    public event Action<Type?, object?>? OnClosed;
    public event Action<Type?, object?>? OnOpened;
    public bool HasBeenInitialized { get; set; }
    public bool HasOnCloseBeenInitialized { get; set; }
    public bool HasOnOpenBeenInitialized { get; set; }
    public bool HasOnDestroyBeenInitialized { get; set; }
    public Type? Type { get; } = type;
    public string TypeName { get; } = type?.Name ?? backupName;

    public void Patch(Harmony patcher)
    {
        if (Type == null)
        {
            Logger.DevkitServer.LogWarning(Source, $"Unable to find type: {TypeName.Format(false)}.");
            return;
        }

        MethodInfo? onUpdateMethod = Type.GetMethod("OnUpdate", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        if (onUpdateMethod == null)
        {
            Logger.DevkitServer.LogWarning(Source, $"Unable to find method: {FormattingUtil.FormatMethod(typeof(void), Type, "OnUpdate", arguments: Type.EmptyTypes)}.");
            return;
        }

        try
        {

            patcher.Patch(onUpdateMethod, transpiler: new HarmonyMethod(Accessor.GetMethod(TranspileUIOnUpdate)!));
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError(Source, ex, $"Unable to patch: {onUpdateMethod.Format()}.");
            return;
        }

        MethodInfo? closeMethod = Type!.GetMethod("Close", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        if (closeMethod == null)
        {
            Logger.DevkitServer.LogWarning(Source, $"Unable to find method: {FormattingUtil.FormatMethod(typeof(void), Type, "Close", arguments: Type.EmptyTypes)}.");
            return;
        }

        try
        {

            patcher.Patch(closeMethod, postfix: new HarmonyMethod(Accessor.GetMethod(OnClosedAndDestroyedInvoker)!));
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError(Source, ex, $"Unable to patch: {closeMethod.Format()}.");
        }
    }

    public void Unpatch(Harmony patcher)
    {
        MethodInfo? onUpdateMethod = UIAccessTools.EditorVolumesUIType?.GetMethod("OnUpdate", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        MethodInfo? closeMethod = UIAccessTools.EditorVolumesUIType!.GetMethod("Close", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

        if (onUpdateMethod != null)
            patcher.Unpatch(onUpdateMethod, Accessor.GetMethod(TranspileUIOnUpdate)!);

        if (closeMethod != null)
            patcher.Unpatch(closeMethod, Accessor.GetMethod(OnClosedAndDestroyedInvoker)!);
    }

    private static IEnumerable<CodeInstruction> TranspileUIOnUpdate(IEnumerable<CodeInstruction> instructions, MethodBase method)
    {
        MethodInfo addInvoker = Accessor.GetMethod(OnOpenedInvoker)!;
        MethodInfo removeInvoker = Accessor.GetMethod(OnClosedAndDestroyedInvoker)!;
        MethodInfo? addMethod = typeof(SleekWrapper).GetMethod(nameof(SleekWrapper.AddChild), BindingFlags.Instance | BindingFlags.Public);
        if (addMethod == null)
            Logger.DevkitServer.LogError(Source, $"Unable to find method: {FormattingUtil.FormatMethod(typeof(void), typeof(SleekWrapper), nameof(SleekWrapper.AddChild), [ (typeof(ISleekElement), "sleek") ])}.");
        MethodInfo? removeMethod = typeof(SleekWrapper).GetMethod(nameof(SleekWrapper.RemoveChild), BindingFlags.Instance | BindingFlags.Public);
        if (removeMethod == null)
            Logger.DevkitServer.LogError(Source, $"Unable to find method: {FormattingUtil.FormatMethod(typeof(void), typeof(SleekWrapper), nameof(SleekWrapper.RemoveChild), [ (typeof(ISleekElement), "sleek") ])}.");

        List<CodeInstruction> ins = [..instructions];

        bool add = false, remove = false;
        for (int i = 0; i < ins.Count; ++i)
        {
            if (!add && PatchUtility.FollowPattern(ins, ref i,
                    x => x.opcode == OpCodes.Ldfld,
                    x => addMethod != null && x.Calls(addMethod)
                    ))
            {
                add = true;
                ins.Insert(i, ins[i - 3].CopyWithoutSpecial());
                ins.Insert(i + 1, ins[i - 2].CopyWithoutSpecial());
                ins.Insert(i + 2, new CodeInstruction(OpCodes.Call, addInvoker));
                i += 3;
            }

            if (!remove && PatchUtility.MatchPattern(ins, i,
                    x => x.opcode == OpCodes.Ldfld,
                    x => removeMethod != null && x.Calls(removeMethod)
                ))
            {
                remove = true;
                ins.Insert(i + 1, new CodeInstruction(OpCodes.Dup));
                ins.Insert(i + 2, new CodeInstruction(OpCodes.Call, removeInvoker));
                i += 4;
            }
        }
        if (!add)
        {
            Logger.DevkitServer.LogError(Source, $"Failed to patch: {method.Format()}, unable to insert add child invoker (on opened).");
        }
        if (!remove)
        {
            Logger.DevkitServer.LogError(Source, $"Failed to patch: {method.Format()}, unable to insert remove child invoker (on closed).");
        }

        return ins;
    }
    private static void OnOpenedInvoker(object __instance)
    {
        Type type = __instance.GetType();

        if (!UIAccessTools.TryGetUITypeInfo(type, out UITypeInfo typeInfo) || typeInfo.CustomOnOpen is not CustomSelectionToolMenuHandler customHandler)
            return;

        customHandler.OnOpened?.Invoke(null, __instance);
    }
    private static void OnClosedAndDestroyedInvoker(object __instance)
    {
        Type type = __instance.GetType();

        if (!UIAccessTools.TryGetUITypeInfo(type, out UITypeInfo typeInfo) || typeInfo.CustomOnClose is not CustomSelectionToolMenuHandler customHandler)
            return;

        customHandler.OnClosed?.Invoke(null, __instance);
    }
}
#endif