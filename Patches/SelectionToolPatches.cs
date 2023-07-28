#if CLIENT
using DevkitServer.API.Abstractions;
using DevkitServer.API.Permissions;
using DevkitServer.Models;
using DevkitServer.Multiplayer.Actions;
using DevkitServer.Multiplayer.Levels;
using DevkitServer.Players;
using DevkitServer.Players.UI;
using HarmonyLib;
using SDG.Framework.Devkit;
using SDG.Framework.Devkit.Transactions;
using SDG.Framework.Landscapes;
using SDG.Framework.Utilities;
using System.Reflection;
using System.Reflection.Emit;

namespace DevkitServer.Patches;

[HarmonyPatch]
internal static class SelectionToolPatches
{
    public static InstanceGetter<SelectionTool, TransformHandles>? GetSelectionHandles = Accessor.GenerateInstanceGetter<SelectionTool, TransformHandles>("handles");

    [HarmonyPatch(typeof(SelectionTool), nameof(SelectionTool.update))]
    [HarmonyTranspiler]
    [UsedImplicitly]
    private static IEnumerable<CodeInstruction> SelectionToolUpdateTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase method)
    {
        Type st = typeof(SelectionTool);
        Type dtu = typeof(DevkitTransactionUtility);

        MethodInfo moveHandleInvoker = new Action<Vector3, Quaternion, Vector3, bool, bool>(OnMoveHandle).Method;
        MethodInfo requestInstantiationInvoker = new Action<Vector3>(OnRequestInstantiaion).Method;
        MethodInfo recordDestructionInvoker = new Action<GameObject>(OnDestruction).Method;
        MethodInfo transformSelectionInvoker = new Action(OnFinishTransform).Method;

        MethodInfo? moveHandle = st.GetMethod("moveHandle", BindingFlags.Instance | BindingFlags.NonPublic, null, new Type[] { typeof(Vector3), typeof(Quaternion), typeof(Vector3), typeof(bool), typeof(bool) }, null);
        if (moveHandle == null)
        {
            Logger.LogWarning($"{method.Format()} - Unable to find method: SelectionTool.moveHandle.", method: "CLIENT EVENTS");
            DevkitServerModule.Fault();
        }

        MethodInfo? requestInstantiation = st.GetMethod("RequestInstantiation", BindingFlags.Instance | BindingFlags.NonPublic, null, new Type[] { typeof(Vector3) }, null);
        if (requestInstantiation == null)
        {
            Logger.LogWarning($"{method.Format()} - Unable to find method: SelectionTool.RequestInstantiation.", method: "CLIENT EVENTS");
            DevkitServerModule.Fault();
        }

        MethodInfo? transformSelection = st.GetMethod("transformSelection", BindingFlags.Instance | BindingFlags.NonPublic, null, Array.Empty<Type>(), null);
        if (transformSelection == null)
        {
            Logger.LogWarning($"{method.Format()} - Unable to find method: SelectionTool.transformSelection.", method: "CLIENT EVENTS");
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
        int i = 0;
        PatchUtil.InsertActionRateLimiter(ref i, stLbl, ins);
        StackTracker tracker = new StackTracker(ins);
        for (; i < ins.Count; ++i)
        {
            CodeInstruction c = ins[i];
            if (i == 0)
                c.labels.Add(stLbl);
            MethodInfo? invoker = null;
            if (moveHandle != null && c.Calls(moveHandle))
                invoker = moveHandleInvoker;
            else if (transformSelection != null && c.Calls(transformSelection))
                invoker = transformSelectionInvoker;

            if (invoker != null)
            {
                int lastLdArg0 = tracker.GetLastUnconsumedIndex(i, OpCodes.Ldarg_0, method);
                if (lastLdArg0 != -1)
                {
                    ins.Insert(i + 1, new CodeInstruction(OpCodes.Call, invoker));

                    for (int j = i - 1; j > lastLdArg0; --j)
                    {
                        ins.Insert(i + 1, ins[j].CopyWithoutSpecial());
                    }

                    i += i - lastLdArg0;
                    Logger.LogDebug($"[CLIENT EVENTS] {method.Format()} - Patched in {invoker.Format()}.");
                }
            }
            else if (recordDestruction != null && c.Calls(recordDestruction))
            {
                ins.Insert(i + 1, new CodeInstruction(OpCodes.Call, recordDestructionInvoker));
                for (int j = i - 1; j >= 0; --j)
                {
                    ins.Insert(i + 1, ins[j].CopyWithoutSpecial());
                    if (ins[j].operand is LocalBuilder bld && typeof(IEnumerator<DevkitSelection>).IsAssignableFrom(bld.LocalType))
                        break;
                }
                Logger.LogDebug($"[CLIENT EVENTS] {method.Format()} - Patched in {recordDestructionInvoker.Format()}.");
            }
            else if (requestInstantiation != null && c.Calls(requestInstantiation))
            {
                int lastLdArg0 = tracker.GetLastUnconsumedIndex(i, OpCodes.Ldarg_0, method);
                if (lastLdArg0 != -1)
                {
                    CodeInstruction ldarg0 = ins[lastLdArg0];
                    ins.RemoveAt(lastLdArg0);
                    ins[lastLdArg0].labels.AddRange(ldarg0.labels);
                    ins[lastLdArg0].blocks.AddRange(ldarg0.blocks);
                    --i;
                    ins[i] = new CodeInstruction(OpCodes.Call, requestInstantiationInvoker);
                    ins[i].labels.AddRange(c.labels);
                    ins[i].blocks.AddRange(c.blocks);
                }
                Logger.LogDebug($"[CLIENT EVENTS] {method.Format()} - Replaced instantiation request {requestInstantiation.Format()} with {requestInstantiationInvoker.Format()}.");
            }
        }

        return ins;
    }

    [HarmonyPatch(typeof(SelectionTool), "OnHandleTranslatedAndRotated")]
    [HarmonyPostfix]
    [UsedImplicitly]
    private static void SelectionToolOnHandleTranslatedAndRotated(Vector3 worldPositionDelta, Quaternion worldRotationDelta, Vector3 pivotPosition, bool modifyRotation)
    {
        if (!DevkitServerModule.IsEditing || !_movingHandle)
            return;

        // todo (E with multiple selections)
        Logger.LogDebug($"[CLIENT EVENTS] Set transform move requested at: {string.Join(",", DevkitSelectionManager.selection.Select(selection => selection.gameObject.name.Format()))}: deltaPos: {worldPositionDelta.Format()}, deltaRot: {worldRotationDelta.eulerAngles.Format()}, pivotPos: {pivotPosition.Format()}, modifyRotation: {modifyRotation}.");
    }

    /// <summary>Skipped for no permissions.</summary>
    private static bool _skippedMoveHandle;

    /// <summary>Is currently in <see cref="SelectionTool"/>.moveHandle.</summary>
    private static bool _movingHandle;

    [HarmonyPatch(typeof(SelectionTool), "moveHandle")]
    [HarmonyPrefix]
    [UsedImplicitly]
    private static bool MoveHandlePrefix(Vector3 position, Quaternion rotation, Vector3 scale, bool doRotation, bool hasScale)
    {
        _skippedMoveHandle = false;
        if (DevkitSelectionManager.selection.Count <= 0)
        {
            UIMessage.SendEditorMessage(DevkitServerModule.MessageLocalization.Translate("UnknownError"));
            _skippedMoveHandle = true;
            return false;
        }

        DevkitSelection? sel = DevkitSelectionManager.selection.FirstOrDefault();
        if (sel == null || sel.gameObject == null)
        {
            UIMessage.SendEditorMessage(DevkitServerModule.MessageLocalization.Translate("UnknownError"));
            _skippedMoveHandle = true;
            return false;
        }

        sel.gameObject.GetComponents(HierarchyUtil.HierarchyItemBuffer);
        try
        {
            for (int i = 0; i < HierarchyUtil.HierarchyItemBuffer.Count; ++i)
            {
                if (!HierarchyUtil.CheckMovePermission(HierarchyUtil.HierarchyItemBuffer[i]))
                {
                    UIMessage.SendNoPermissionMessage(null);
                    _skippedMoveHandle = true;
                    return false;
                }
            }
        }
        finally
        {
            HierarchyUtil.HierarchyItemBuffer.Clear();
        }
        
        _movingHandle = true;
        return true;
    }
    [HarmonyPatch(typeof(SelectionTool), "moveHandle")]
    [HarmonyPostfix]
    [UsedImplicitly]
    private static void MoveHandlePostfix(Vector3 position, Quaternion rotation, Vector3 scale, bool doRotation, bool hasScale)
    {
        _movingHandle = false;
    }

    [UsedImplicitly]
    private static void OnMoveHandle(Vector3 position, Quaternion rotation, Vector3 scale, bool doRotation, bool hasScale)
    {
        if (!DevkitServerModule.IsEditing) return;

        Logger.LogDebug("[CLIENT EVENTS] Handle moved: " + position.Format() + " Rot: " + doRotation.Format() + ", Scale: " + hasScale.Format() + ".");
        List<FinalTransformation> transformations = ListPool<FinalTransformation>.claim();
        float dt = CachedTime.DeltaTime;
        try
        {
            TransformationDelta.TransformFlags flags = TransformationDelta.TransformFlags.Position | TransformationDelta.TransformFlags.OriginalPosition;
            if (doRotation) flags |= TransformationDelta.TransformFlags.Rotation | TransformationDelta.TransformFlags.OriginalRotation;
            int c = DevkitSelectionManager.selection.Count;
            transformations.IncreaseCapacity(c);
            foreach (DevkitSelection selection in DevkitSelectionManager.selection)
            {
                TransformationDelta t = new TransformationDelta(flags, position, rotation, selection.preTransformPosition, selection.preTransformRotation);
                selection.gameObject.GetComponents(HierarchyUtil.HierarchyItemBuffer);
                try
                {
                    for (int i = 0; i < HierarchyUtil.HierarchyItemBuffer.Count; ++i)
                    {
                        IDevkitHierarchyItem item = HierarchyUtil.HierarchyItemBuffer[i];
                        if (!HierarchyItemNetIdDatabase.TryGetHierarchyItemNetId(item, out NetId netId))
                        {
                            Logger.LogWarning($"Skipped item: {item.Format()} because it was missing a NetId.", method: "CLIENT EVENTS");
                            continue;
                        }

                        FinalTransformation transformation = new FinalTransformation(netId, t, selection.gameObject.transform.localScale, selection.preTransformLocalScale);

                        transformations.Add(transformation);
                        if (ClientEvents.ListeningOnMoveHierarchyObjectFinal)
                            ClientEvents.InvokeOnMoveHierarchyObjectFinal(new MoveHierarchyObjectFinalProperties(selection, item, transformation, hasScale, dt));
                    }
                }
                finally
                {
                    HierarchyUtil.HierarchyItemBuffer.Clear();
                }
            }
            
            if (ClientEvents.ListeningOnMoveHierarchyObjectsFinal)
                ClientEvents.InvokeOnMoveHierarchyObjectsFinal(new MoveHierarchyObjectsFinalProperties(transformations.ToArrayFast(), hasScale, dt));
        }
        finally
        {
            ListPool<FinalTransformation>.release(transformations);
        }
    }

    private static readonly Func<TempNodeSystemBase, Type>? GetNodeComponentType =
        Accessor.GenerateInstanceCaller<TempNodeSystemBase, Func<TempNodeSystemBase, Type>>("GetComponentType",
            Array.Empty<Type>(), throwOnError: true);

    [UsedImplicitly]
    private static void OnRequestInstantiaion(Vector3 position)
    {
        if (!DevkitServerModule.IsEditing) return;

        IDevkitTool? tool = UserInput.ActiveTool;
        IHierarchyItemTypeIdentifier? id = null;
        switch (tool)
        {
            case NodesEditor n:
                if (GetNodeComponentType == null)
                    return;
                if (n.activeNodeSystem != null)
                {
                    Type? t = GetNodeComponentType(n.activeNodeSystem);
                    if (t != null)
                        id = new NodeItemTypeIdentifier(t);
                }
                break;
            case VolumesEditor v:
                if (GetNodeComponentType == null)
                    return;
                if (v.activeVolumeManager != null)
                {
                    if (v.activeVolumeManager is LandscapeHoleVolumeManager)
                    {
                        UIMessage.SendEditorMessage(DevkitServerModule.MessageLocalization.Translate("FeatureDisabled"));
                        return;
                    }

                    Type? t = VolumeItemTypeIdentifier.TryGetComponentType(v.activeVolumeManager);
                    if (t != null)
                        id = new VolumeItemTypeIdentifier(t);
                }
                break;
        }
        if (id != null)
        {
            // todo events
            if (!HierarchyUtil.CheckPlacePermission(id))
            {
                Permission? place = HierarchyUtil.GetPlacePermission(id);
                UIMessage.SendEditorMessage(place == null
                    ? DevkitServerModule.MessageLocalization.Translate("NoPermissions")
                    : DevkitServerModule.MessageLocalization.Translate("NoPermissionsWithPermission", place.ToString()));
                return;
            }
            bool allow = true;
            InstantiateHierarchyObjectProperties instantiation = new InstantiateHierarchyObjectProperties(id, position);
            ClientEvents.EventOnTryInstantiateHierarchyObject.TryInvoke(ref instantiation, ref allow);
            if (allow)
                ClientEvents.EventOnRequestInstantiateHierarchyObject.TryInvoke(in instantiation);
        }
        else
            UIMessage.SendEditorMessage(DevkitServerModule.MessageLocalization.Translate("UnknownError"));

        Logger.LogDebug("[CLIENT EVENTS] Instantiation requested at: " + position.Format() + " of " + (id == null ? ((object?)null).Format() : id.Format()) + ".");
    }

    [UsedImplicitly]
    private static void OnDestruction(GameObject obj)
    {
        if (!DevkitServerModule.IsEditing) return;

        Logger.LogDebug("[CLIENT EVENTS] Destruction requested at: " + obj.name.Format() + ".");
        obj.GetComponents(HierarchyUtil.HierarchyItemBuffer);
        NetId[] netIds = new NetId[HierarchyUtil.HierarchyItemBuffer.Count];
        int index = -1;
        try
        {
            for (int i = 0; i < HierarchyUtil.HierarchyItemBuffer.Count; ++i)
            {
                IDevkitHierarchyItem item = HierarchyUtil.HierarchyItemBuffer[i];
                if (!HierarchyItemNetIdDatabase.TryGetHierarchyItemNetId(item, out NetId netId))
                {
                    Logger.LogWarning($"Skipped item: {item.Format()} because it was missing a NetId.", method: "CLIENT EVENTS");
                    continue;
                }

                netIds[++index] = netId;
                ClientEvents.InvokeOnDeleteHierarchyObject(new DeleteHierarchyObjectProperties(obj, item, netId));
            }
        }
        finally
        {
            HierarchyUtil.HierarchyItemBuffer.Clear();
        }

        if (ClientEvents.ListeningOnDeleteHierarchyObjects)
        {
            ++index;
            if (index < HierarchyUtil.HierarchyItemBuffer.Count)
                Array.Resize(ref netIds, index);

            ClientEvents.InvokeOnDeleteHierarchyObjects(new DeleteHierarchyObjectsProperties(netIds, CachedTime.DeltaTime));
        }
    }

    [UsedImplicitly]
    private static void OnFinishTransform()
    {
        HashSet<DevkitSelection> selections = DevkitSelectionManager.selection;

        if (!DevkitServerModule.IsEditing || _skippedMoveHandle || selections.Count < 1)
            return;

        float dt = CachedTime.DeltaTime;
        bool globalUseScale = false;
        List<FinalTransformation> transformations = ListPool<FinalTransformation>.claim();
        transformations.IncreaseCapacity(selections.Count);
        try
        {
            foreach (DevkitSelection selection in selections)
            {
                selection.gameObject.GetComponents(HierarchyUtil.HierarchyItemBuffer);
                try
                {
                    for (int i = 0; i < HierarchyUtil.HierarchyItemBuffer.Count; ++i)
                    {
                        Transform transform = selection.gameObject.transform;
                        IDevkitHierarchyItem item = HierarchyUtil.HierarchyItemBuffer[i];
                        if (!HierarchyItemNetIdDatabase.TryGetHierarchyItemNetId(item, out NetId netId))
                        {
                            Logger.LogWarning($"Skipped item: {item.Format()} because it was missing a NetId.", method: "CLIENT EVENTS");
                            continue;
                        }

                        Vector3 scale = transform.localScale;

                        bool useScale = !scale.IsNearlyEqual(selection.preTransformLocalScale);
                        globalUseScale |= useScale;
                        
                        FinalTransformation transformation = new FinalTransformation(netId,
                            new TransformationDelta(TransformationDelta.TransformFlags.All, transform.position, transform.rotation, selection.preTransformPosition, selection.preTransformRotation),
                            selection.gameObject.transform.localScale, selection.preTransformLocalScale);
                        transformations.Add(transformation);
                        if (ClientEvents.ListeningOnMoveHierarchyObjectFinal)
                            ClientEvents.InvokeOnMoveHierarchyObjectFinal(new MoveHierarchyObjectFinalProperties(selection, item, transformation, useScale, dt));
                    }
                }
                finally
                {
                    HierarchyUtil.HierarchyItemBuffer.Clear();
                }
            }
            if (ClientEvents.ListeningOnMoveHierarchyObjectsFinal)
                ClientEvents.InvokeOnMoveHierarchyObjectsFinal(new MoveHierarchyObjectsFinalProperties(transformations.ToArrayFast(), globalUseScale, dt));
        }
        finally
        {
            ListPool<FinalTransformation>.release(transformations);
        }

        Logger.LogDebug("[CLIENT EVENTS] Move completed for: " + string.Join(",", selections.Select(x => x.transform.name.Format(false))) + ".");
    }
}
#endif