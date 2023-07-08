#if CLIENT
using DevkitServer.API.UI;
using DevkitServer.Patches;
using DevkitServer.Players.UI;
using HarmonyLib;
using System.Reflection;
using System.Reflection.Emit;

namespace DevkitServer.Players.UI.Handlers;

internal abstract class CustomSelectionToolMenuHandler : ICustomOnClose, ICustomOnOpen
{
    private const string Source = UIAccessTools.Source + ".SELECTION TOOL HANDLER";

    private static CustomNodeMenuHandler? _nodes;
    private static CustomVolumeMenuHandler? _volumes;
    public event Action<Type?, object?>? OnClose;
    public event Action<Type?, object?>? OnOpened;
    private readonly bool _isNodes;
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
        if (this is CustomNodeMenuHandler nodeHandler)
        {
            _nodes = nodeHandler;
            _isNodes = true;
        }
        else if (this is CustomVolumeMenuHandler volumeHandler)
            _volumes = volumeHandler;
    }

    private MethodInfo GetTranspiler() => _isNodes
        ? new Func<IEnumerable<CodeInstruction>, MethodBase, IEnumerable<CodeInstruction>>(TranspileNodeUIOnUpdate).Method
        : new Func<IEnumerable<CodeInstruction>, MethodBase, IEnumerable<CodeInstruction>>(TranspileVolumeUIOnUpdate).Method;
    private MethodInfo GetOnClosedAndDestroyedInvoker() => _isNodes
        ? new Action<object>(OnNodeClosedAndDestroyedInvoker).Method
        : new Action<object>(OnVolumeClosedAndDestroyedInvoker).Method;
    public void Patch()
    {
        MethodInfo? onUpdateMethod = Type?.GetMethod("OnUpdate", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        if (onUpdateMethod == null)
        {
            Logger.LogWarning("Unable to find method: " + TypeName + ".OnUpdate.", method: Source);
            return;
        }

        PatchesMain.Patcher.Patch(onUpdateMethod, transpiler: new HarmonyMethod(GetTranspiler()));

        MethodInfo? closeMethod = Type!.GetMethod("Close", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        if (closeMethod == null)
        {
            Logger.LogWarning("Unable to find method: " + TypeName + ".Close.", method: Source);
            return;
        }

        PatchesMain.Patcher.Patch(closeMethod, postfix: new HarmonyMethod(GetOnClosedAndDestroyedInvoker()));
    }

    public void Unpatch()
    {
        MethodInfo? onUpdateMethod = UIAccessTools.EditorVolumesUIType?.GetMethod("OnUpdate", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        MethodInfo? closeMethod = UIAccessTools.EditorVolumesUIType!.GetMethod("Close", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

        if (onUpdateMethod != null)
            PatchesMain.Patcher.Unpatch(onUpdateMethod, GetTranspiler());

        if (closeMethod != null)
            PatchesMain.Patcher.Unpatch(closeMethod, GetOnClosedAndDestroyedInvoker());
    }

    private static IEnumerable<CodeInstruction> TranspileNodeUIOnUpdate(IEnumerable<CodeInstruction> instructions, MethodBase method)
    {
        MethodInfo addInvoker = new Action<object>(OnNodeOpenedInvoker).Method;
        MethodInfo removeInvoker = new Action<object>(OnNodeClosedAndDestroyedInvoker).Method;
        return TranspileUIOnUpdate(instructions, method, addInvoker, removeInvoker);
    }
    private static IEnumerable<CodeInstruction> TranspileVolumeUIOnUpdate(IEnumerable<CodeInstruction> instructions, MethodBase method)
    {
        MethodInfo addInvoker = new Action<object>(OnVolumeOpenedInvoker).Method;
        MethodInfo removeInvoker = new Action<object>(OnVolumeClosedAndDestroyedInvoker).Method;
        return TranspileUIOnUpdate(instructions, method, addInvoker, removeInvoker);
    }

    private static IEnumerable<CodeInstruction> TranspileUIOnUpdate(IEnumerable<CodeInstruction> instructions, MethodBase method, MethodInfo addInvoker, MethodInfo removeInvoker)
    {
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
                ins.Insert(i, ins[i - 2].CopyWithoutSpecial());
                ins.Insert(i + 1, new CodeInstruction(OpCodes.Call, addInvoker));
                i += 2;
            }

            if (!remove && PatchUtil.MatchPattern(ins, i,
                    x => x.opcode == OpCodes.Ldfld,
                    x => removeMethod != null && x.Calls(removeMethod)
                ))
            {
                remove = true;
                ins.Insert(i, new CodeInstruction(OpCodes.Dup));
                ins.Insert(i + 1, new CodeInstruction(OpCodes.Call, removeInvoker));
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
    private static void OnNodeOpenedInvoker(object __instance)
    {
        _nodes?.OnOpened?.Invoke(null, __instance);
    }
    private static void OnNodeClosedAndDestroyedInvoker(object __instance)
    {
        if (_nodes == null) return;
        _nodes.OnClose?.Invoke(null, __instance);
    }
    private static void OnVolumeOpenedInvoker(object __instance)
    {
        _volumes?.OnOpened?.Invoke(null, __instance);
    }
    private static void OnVolumeClosedAndDestroyedInvoker(object __instance)
    {
        if (_volumes == null) return;
        _volumes.OnClose?.Invoke(null, __instance);
    }
}
internal class CustomNodeMenuHandler : CustomSelectionToolMenuHandler
{
    public CustomNodeMenuHandler() : base(UIAccessTools.EditorEnvironmentNodesUIType, "EditorEnvironmentNodesUI") { }
}

internal class CustomVolumeMenuHandler : CustomSelectionToolMenuHandler
{
    public CustomVolumeMenuHandler() : base(UIAccessTools.EditorVolumesUIType, "EditorVolumesUI") { }
}
#endif