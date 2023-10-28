#if CLIENT
using DevkitServer.API;
using DevkitServer.API.UI;
using HarmonyLib;
using System.Reflection;
using System.Reflection.Emit;

namespace DevkitServer.Core.UI.Handlers;

internal class CustomNodeMenuHandler : CustomSelectionToolMenuHandler
{
    public CustomNodeMenuHandler() : base(UIAccessTools.EditorEnvironmentNodesUIType, "EditorEnvironmentNodesUI") { }
}
internal class CustomVolumeMenuHandler : CustomSelectionToolMenuHandler
{
    public CustomVolumeMenuHandler() : base(UIAccessTools.EditorVolumesUIType, "EditorVolumesUI") { }
}

internal abstract class CustomSelectionToolMenuHandler : ICustomOnCloseUIHandler, ICustomOnOpenUIHandler
{
    private const string Source = UIAccessTools.Source + ".SELECTION TOOL HANDLER";
    
    public event Action<Type?, object?>? OnClosed;
    public event Action<Type?, object?>? OnOpened;
    public bool HasBeenInitialized { get; set; }
    public bool HasOnCloseBeenInitialized { get; set; }
    public bool HasOnOpenBeenInitialized { get; set; }
    public bool HasOnDestroyBeenInitialized { get; set; }
    public Type? Type { get; }
    public string TypeName { get; }
    protected CustomSelectionToolMenuHandler(Type? type, string backupName)
    {
        Type = type;
        TypeName = type?.Name ?? backupName;
    }
    
    public void Patch(Harmony patcher)
    {
        if (Type == null)
        {
            Logger.LogWarning($"Unable to find type: {TypeName.Format(false)}.", method: Source);
            return;
        }

        MethodInfo? onUpdateMethod = Type.GetMethod("OnUpdate", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        if (onUpdateMethod == null)
        {
            Logger.LogWarning($"Unable to find method: {Type.Format()}.OnUpdate.", method: Source);
            return;
        }

        try
        {

            patcher.Patch(onUpdateMethod, transpiler: new HarmonyMethod(Accessor.GetMethod(TranspileUIOnUpdate)!));
        }
        catch (Exception ex)
        {
            Logger.LogError($"Unable to patch: {onUpdateMethod.Format()}.", method: Source);
            Logger.LogError(ex, method: Source);
            return;
        }

        MethodInfo? closeMethod = Type!.GetMethod("Close", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        if (closeMethod == null)
        {
            Logger.LogWarning($"Unable to find method: {Type.Format()}.Close.", method: Source);
            return;
        }

        try
        {

            patcher.Patch(closeMethod, postfix: new HarmonyMethod(Accessor.GetMethod(OnClosedAndDestroyedInvoker)!));
        }
        catch (Exception ex)
        {
            Logger.LogError($"Unable to patch: {closeMethod.Format()}.", method: Source);
            Logger.LogError(ex, method: Source);
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
            Logger.LogError("Unable to find method: SleekWrapper.AddChild.", method: Source);
        MethodInfo? removeMethod = typeof(SleekWrapper).GetMethod(nameof(SleekWrapper.RemoveChild), BindingFlags.Instance | BindingFlags.Public);
        if (removeMethod == null)
            Logger.LogError("Unable to find method: SleekWrapper.RemoveChild.", method: Source);

        List<CodeInstruction> ins = new List<CodeInstruction>(instructions);

        bool add = false, remove = false;
        for (int i = 0; i < ins.Count; ++i)
        {
            if (!add && PatchUtil.FollowPattern(ins, ref i,
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

            if (!remove && PatchUtil.MatchPattern(ins, i,
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
            Logger.LogError($"Failed to patch: {method.Format()}, unable to insert add child invoker (on opened).", method: Source);
        }
        if (!remove)
        {
            Logger.LogError($"Failed to patch: {method.Format()}, unable to insert remove child invoker (on closed).", method: Source);
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