﻿#if CLIENT
using DanielWillett.ReflectionTools;
using DanielWillett.ReflectionTools.Formatting;
using DevkitServer.API.Abstractions;
using DevkitServer.API.Permissions;
using DevkitServer.API.UI;
using DevkitServer.Core.Permissions;
using DevkitServer.Models;
using DevkitServer.Multiplayer.Actions;
using DevkitServer.Multiplayer.Levels;
using DevkitServer.Players;
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
    private const string Source = "SELECT TOOL PATCHES";
    public static InstanceGetter<SelectionTool, TransformHandles>? GetSelectionHandles = Accessor.GenerateInstanceGetter<SelectionTool, TransformHandles>("handles");

    [HarmonyPatch(typeof(SelectionTool), nameof(SelectionTool.update))]
    [HarmonyTranspiler]
    [UsedImplicitly]
    private static IEnumerable<CodeInstruction> SelectionToolUpdateTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase method)
    {
        Type st = typeof(SelectionTool);
        Type dtu = typeof(DevkitTransactionUtility);
        TranspileContext ctx = new TranspileContext(method, generator, instructions);

        MethodInfo moveHandleInvoker = new Action<Vector3, Quaternion, Vector3, bool, bool>(OnMoveHandle).Method;
        MethodInfo requestInstantiationInvoker = new Action<Vector3>(OnRequestInstantiaion).Method;
        MethodInfo recordDestructionInvoker = new Action<GameObject>(OnDestruction).Method;
        MethodInfo transformSelectionInvoker = new Action(OnFinishTransform).Method;

        MethodInfo? moveHandle = st.GetMethod("moveHandle", BindingFlags.Instance | BindingFlags.NonPublic, null, [ typeof(Vector3), typeof(Quaternion), typeof(Vector3), typeof(bool), typeof(bool) ], null);
        if (moveHandle == null)
        {
            ctx.Fail(new MethodDefinition("moveHandle")
                .DeclaredIn(st, isStatic: false)
                .ReturningVoid()
                .WithParameter<Vector3>("position")
                .WithParameter<Quaternion>("rotation")
                .WithParameter<Vector3>("scale")
                .WithParameter<bool>("doRotation")
                .WithParameter<bool>("hasScale")
            );
            DevkitServerModule.Fault();
            return instructions;
        }

        MethodInfo? requestInstantiation = st.GetMethod("RequestInstantiation", BindingFlags.Instance | BindingFlags.NonPublic, null, [ typeof(Vector3) ], null);
        if (requestInstantiation == null)
        {
            ctx.Fail(new MethodDefinition("RequestInstantiation")
                .DeclaredIn(st, isStatic: false)
                .ReturningVoid()
                .WithParameter<Vector3>("position")
            );
            DevkitServerModule.Fault();
            return instructions;
        }

        MethodInfo? transformSelection = st.GetMethod("transformSelection", BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
        if (transformSelection == null)
        {
            ctx.Fail(new MethodDefinition("transformSelection")
                .DeclaredIn(st, isStatic: false)
                .ReturningVoid()
                .WithNoParameters()
            );
            DevkitServerModule.Fault();
            return instructions;
        }

        MethodInfo? recordDestruction = dtu.GetMethod(nameof(DevkitTransactionUtility.recordDestruction), BindingFlags.Static | BindingFlags.Public, null, [ typeof(GameObject) ], null);
        if (recordDestruction == null)
        {
            ctx.Fail(new MethodDefinition("recordDestruction")
                .DeclaredIn(typeof(DevkitTransactionUtility), isStatic: true)
                .ReturningVoid()
                .WithParameter<GameObject>("go")
            );
            DevkitServerModule.Fault();
            return instructions;
        }

        while (ctx.MoveNext())
        {
            CodeInstruction c = ctx.Instruction;
            MethodInfo? invoker = null;
            if (moveHandle != null && c.Calls(moveHandle))
                invoker = moveHandleInvoker;
            else if (transformSelection != null && c.Calls(transformSelection))
                invoker = transformSelectionInvoker;

            if (invoker != null)
            {
                int lastLdArg0 = ctx.GetLastUnconsumedIndex(OpCodes.Ldarg_0, method);

                if (lastLdArg0 == -1)
                    continue;

                int i = ctx.CaretIndex;
                ctx.CaretIndex = i + 1;

                for (int j = lastLdArg0 + 1; j < i; ++j)
                {
                    ctx.Emit(ctx[j].CopyWithoutSpecial());
                }

                ctx.Emit(invoker.GetCallRuntime(), invoker);
                ctx.LogDebug($"Patched in {Accessor.Formatter.Format(invoker)}.");
            }
            else if (recordDestruction != null && c.Calls(recordDestruction))
            {
                int i = ctx.CaretIndex;
                ctx.CaretIndex = i + 1;

                int stopIndex = -1;
                for (int j = i - 1; j >= 0; --j)
                {
                    if (ctx[j].operand is not LocalBuilder bld || !typeof(IEnumerator<DevkitSelection>).IsAssignableFrom(bld.LocalType))
                        continue;

                    stopIndex = j;
                    break;
                }

                for (int j = stopIndex; j < i; ++j)
                {
                    ctx.Emit(ctx[j].CopyWithoutSpecial());
                }

                ctx.Emit(recordDestructionInvoker.GetCallRuntime(), recordDestructionInvoker);

                ctx.LogDebug($"Patched in {Accessor.Formatter.Format(recordDestructionInvoker)}.");
            }
            else if (requestInstantiation != null && c.Calls(requestInstantiation))
            {
                int lastLdArg0 = ctx.GetLastUnconsumedIndex(OpCodes.Ldarg_0, method);

                if (lastLdArg0 == -1)
                    continue;

                CodeInstruction newCall = new CodeInstruction(OpCodes.Call, requestInstantiationInvoker);

                int i = ctx.CaretIndex;
                ctx.CaretIndex = lastLdArg0;

                BlockInfo blk = ctx.Remove(1);
                blk.SetupBlockStart(ctx.Instruction);

                ctx.CaretIndex = i - 1;

                blk = ctx.Remove(1);
                blk.SetupBlockStart(newCall);
                blk.SetupBlockEnd(newCall);

                ctx.Emit(newCall);

                ctx.LogDebug($"Replaced instantiation request {Accessor.Formatter.Format(requestInstantiation)} with {Accessor.Formatter.Format(requestInstantiationInvoker)}.");
            }
        }

        return ctx;
    }

    [HarmonyPatch(typeof(SelectionTool), "OnHandleTranslatedAndRotated")]
    [HarmonyPostfix]
    [UsedImplicitly]
    private static void SelectionToolOnHandleTranslatedAndRotated(Vector3 worldPositionDelta, Quaternion worldRotationDelta, Vector3 pivotPosition, bool modifyRotation)
    {
        if (!DevkitServerModule.IsEditing || !_movingHandle)
            return;

        // todo (E with multiple selections)
        Logger.DevkitServer.LogDebug(Source, $"Set transform move requested at: {string.Join(",", DevkitSelectionManager.selection.Select(selection => selection.gameObject.name.Format()))}: deltaPos: {worldPositionDelta.Format()}, deltaRot: {worldRotationDelta.eulerAngles.Format()}, pivotPos: {pivotPosition.Format()}, modifyRotation: {modifyRotation}.");
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
        if (!DevkitServerModule.IsEditing)
            return true;
        if (DevkitSelectionManager.selection.Count <= 0)
        {
            EditorMessage.SendEditorMessage(TranslationSource.DevkitServerMessageLocalizationSource, "UnknownError");
            _skippedMoveHandle = true;
            return false;
        }

        DevkitSelection? sel = DevkitSelectionManager.selection.FirstOrDefault();
        if (sel == null || sel.gameObject == null)
        {
            EditorMessage.SendEditorMessage(TranslationSource.DevkitServerMessageLocalizationSource, "UnknownError");
            _skippedMoveHandle = true;
            return false;
        }

        sel.gameObject.GetComponents(HierarchyUtil.HierarchyItemBuffer);
        try
        {
            for (int i = 0; i < HierarchyUtil.HierarchyItemBuffer.Count; ++i)
            {
                PermissionLeaf leaf = VanillaPermissions.GetNodeVolumeMove(HierarchyUtil.HierarchyItemBuffer[i].GetType());
                if (!leaf.Has())
                {
                    EditorMessage.SendNoPermissionMessage(leaf);
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

        Logger.DevkitServer.LogDebug(Source, "Handle moved: " + position.Format() + " Rot: " + doRotation.Format() + ", Scale: " + hasScale.Format() + ".");
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
                            Logger.DevkitServer.LogWarning(Source, $"Skipped item: {item.Format()} because it was missing a NetId.");
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
                ClientEvents.InvokeOnMoveHierarchyObjectsFinal(new MoveHierarchyObjectsFinalProperties(transformations.ToSpan(), hasScale, dt));
        }
        finally
        {
            ListPool<FinalTransformation>.release(transformations);
        }
    }

    private static readonly Func<TempNodeSystemBase, Type> GetNodeComponentType =
        Accessor.GenerateInstanceCaller<TempNodeSystemBase, Func<TempNodeSystemBase, Type>>("GetComponentType", throwOnError: true, allowUnsafeTypeBinding: true)!;

    private static readonly Action<SelectionTool, Vector3>? CallRequestInstantiation =
        Accessor.GenerateInstanceCaller<SelectionTool, Action<SelectionTool, Vector3>>("RequestInstantiation", throwOnError: false, allowUnsafeTypeBinding: true);

    [UsedImplicitly]
    private static void OnRequestInstantiaion(Vector3 position)
    {
        IDevkitTool? tool = UserControl.ActiveTool;
        if (!DevkitServerModule.IsEditing)
        {
            if (tool is SelectionTool tool2 && CallRequestInstantiation != null)
                CallRequestInstantiation(tool2, position);
            
            return;
        }

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
                        EditorMessage.SendEditorMessage(DevkitServerModule.MessageLocalization.Translate("FeatureDisabled"));
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
            PermissionLeaf leaf = VanillaPermissions.GetNodeVolumePlace(id.Type);
            if (!leaf.Has())
            {
                EditorMessage.SendNoPermissionMessage(leaf);
                return;
            }
            bool allow = true;
            InstantiateHierarchyObjectProperties instantiation = new InstantiateHierarchyObjectProperties(id, position);
            ClientEvents.EventOnTryInstantiateHierarchyObject.TryInvoke(ref instantiation, ref allow);
            if (allow)
                ClientEvents.EventOnRequestInstantiateHierarchyObject.TryInvoke(in instantiation);
        }
        else
            EditorMessage.SendEditorMessage(DevkitServerModule.MessageLocalization.Translate("UnknownError"));

        Logger.DevkitServer.LogDebug(Source, "Instantiation requested at: " + position.Format() + " of " + (id == null ? ((object?)null).Format() : id.Format()) + ".");
    }

    [UsedImplicitly]
    private static void OnDestruction(GameObject obj)
    {
        if (!DevkitServerModule.IsEditing) return;

        Logger.DevkitServer.LogDebug(Source, "Destruction requested at: " + obj.name.Format() + ".");
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
                    Logger.DevkitServer.LogWarning(Source, $"Skipped item: {item.Format()} because it was missing a NetId.");
                    continue;
                }

                netIds[++index] = netId;
                ClientEvents.InvokeOnDeleteHierarchyObject(new DeleteHierarchyObjectProperties(obj, item, netId));
            }

            if (ClientEvents.ListeningOnDeleteHierarchyObjects)
            {
                ++index;
                if (index < HierarchyUtil.HierarchyItemBuffer.Count)
                    Array.Resize(ref netIds, index);

                ClientEvents.InvokeOnDeleteHierarchyObjects(new DeleteHierarchyObjectsProperties(netIds, CachedTime.DeltaTime));
            }
        }
        finally
        {
            HierarchyUtil.HierarchyItemBuffer.Clear();
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
                            Logger.DevkitServer.LogWarning(Source, $"Skipped item: {item.Format()} because it was missing a NetId.");
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
                ClientEvents.InvokeOnMoveHierarchyObjectsFinal(new MoveHierarchyObjectsFinalProperties(transformations.ToSpan(), globalUseScale, dt));
        }
        finally
        {
            ListPool<FinalTransformation>.release(transformations);
        }

        Logger.DevkitServer.LogDebug(Source, "Move completed for: " + string.Join(",", selections.Select(x => x.transform.name.Format(false))) + ".");
    }
}
#endif