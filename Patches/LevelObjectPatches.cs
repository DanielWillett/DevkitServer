#if CLIENT
using DevkitServer.Core.Permissions;
using DevkitServer.Models;
using DevkitServer.Multiplayer.Actions;
using DevkitServer.Players.UI;
using HarmonyLib;
using JetBrains.Annotations;
using SDG.Framework.Devkit.Transactions;
using SDG.Framework.Utilities;
using System.Reflection;
using System.Reflection.Emit;
using DevkitServer.API.Permissions;
using DevkitServer.Multiplayer;
using Action = System.Action;

namespace DevkitServer.Patches;

[HarmonyPatch]
internal static class LevelObjectPatches
{
    private const string Source = "OBJECT PATCHES";

    internal static bool IsSyncing;
    private static bool IsFinalTransform;
    private static RegionIdentifier? _lastDeleting;
    private static List<EditorSelection>? _selections;
    private static List<EditorCopy>? _copies;

    private static List<EditorCopy> EditorObjectCopies => _copies ??=
        (List<EditorCopy>?)typeof(EditorObjects).GetField("copies", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null)
        ?? throw new MemberAccessException("Unable to find field: EditorObjects.copies.");
    private static List<EditorSelection> EditorObjectSelection => _selections ??=
        (List<EditorSelection>?)typeof(EditorObjects).GetField("selection", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null)
        ?? throw new MemberAccessException("Unable to find field: EditorObjects.selection.");

    [HarmonyPatch(typeof(EditorObjects), "Update")]
    [HarmonyTranspiler]
    [UsedImplicitly]
    private static IEnumerable<CodeInstruction> EditorObjectsUpdateTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase method)
    {
        Type eo = typeof(EditorObjects);
        Type lo = typeof(LevelObjects);
        Type dtu = typeof(DevkitTransactionUtility);
        // Type vp = typeof(VanillaPermissions);
        

        MethodInfo? pointSelection = eo.GetMethod("pointSelection", BindingFlags.Static | BindingFlags.Public, null, Array.Empty<Type>(), null);
        if (pointSelection == null)
        {
            Logger.LogWarning($"{method.Format()} - Unable to find method: EditorObjects.pointSelection.", method: Source);
            DevkitServerModule.Fault();
        }
        FieldInfo? selectedObjects = eo.GetField("selection", BindingFlags.Static | BindingFlags.NonPublic);
        if (selectedObjects == null)
        {
            Logger.LogWarning($"{method.Format()} - Unable to find field: EditorObjects.selection.", method: Source);
            DevkitServerModule.Fault();
        }
        MethodInfo? selectedObjectsCount = selectedObjects?.FieldType.GetProperty(nameof(List<object>.Count), BindingFlags.Instance | BindingFlags.Public)?.GetMethod;
        if (selectedObjectsCount == null)
        {
            Logger.LogWarning($"{method.Format()} - Unable to find property: EditorObjects.selection.Count.", method: Source);
            DevkitServerModule.Fault();
        }
        FieldInfo? copiedObjects = eo.GetField("copies", BindingFlags.Static | BindingFlags.NonPublic);
        if (copiedObjects == null)
        {
            Logger.LogWarning($"{method.Format()} - Unable to find field: EditorObjects.copies.", method: Source);
            DevkitServerModule.Fault();
        }
        MethodInfo? copiedObjectsCount = copiedObjects?.FieldType.GetProperty(nameof(List<object>.Count), BindingFlags.Instance | BindingFlags.Public)?.GetMethod;
        if (copiedObjectsCount == null)
        {
            Logger.LogWarning($"{method.Format()} - Unable to find property: EditorObjects.copies.Count.", method: Source);
            DevkitServerModule.Fault();
        }
        MethodInfo? clearCopiedObjects = copiedObjects?.FieldType.GetMethod(nameof(List<object>.Clear), BindingFlags.Instance | BindingFlags.Public);
        if (clearCopiedObjects == null)
        {
            Logger.LogWarning($"{method.Format()} - Unable to find method: EditorObjects.copies.Clear.", method: Source);
            DevkitServerModule.Fault();
        }

        MethodInfo? clearSelection = eo.GetMethod("clearSelection", BindingFlags.Static | BindingFlags.NonPublic, null, Array.Empty<Type>(), null);
        if (clearSelection == null)
        {
            Logger.LogWarning($"{method.Format()} - Unable to find method: EditorObjects.clearSelection.", method: Source);
            DevkitServerModule.Fault();
        }

        MethodInfo? calculateHandleOffsets = eo.GetMethod("calculateHandleOffsets", BindingFlags.Static | BindingFlags.NonPublic, null, Array.Empty<Type>(), null);
        if (calculateHandleOffsets == null)
        {
            Logger.LogWarning($"{method.Format()} - Unable to find method: EditorObjects.calculateHandleOffsets.", method: Source);
            DevkitServerModule.Fault();
        }

        MethodInfo? undo = lo.GetMethod("undo", BindingFlags.Static | BindingFlags.Public, null, Array.Empty<Type>(), null);
        if (undo == null)
        {
            Logger.LogWarning($"{method.Format()} - Unable to find method: LevelObjects.undo.", method: Source);
            DevkitServerModule.Fault();
        }

        MethodInfo? redo = lo.GetMethod("redo", BindingFlags.Static | BindingFlags.Public, null, Array.Empty<Type>(), null);
        if (redo == null)
        {
            Logger.LogWarning($"{method.Format()} - Unable to find method: LevelObjects.redo.", method: Source);
            DevkitServerModule.Fault();
        }

        MethodInfo? recordDestruction = dtu.GetMethod(nameof(DevkitTransactionUtility.recordDestruction), BindingFlags.Static | BindingFlags.Public, null, new Type[] { typeof(GameObject) }, null);
        if (recordDestruction == null)
        {
            Logger.LogWarning($"{method.Format()} - Unable to find method: DevkitTransactionUtility.recordDestruction.", method: Source);
            DevkitServerModule.Fault();
        }

        MethodInfo? registerAddObject = lo.GetMethod(nameof(LevelObjects.registerAddObject), BindingFlags.Static | BindingFlags.Public, null, new Type[] { typeof(Vector3), typeof(Quaternion), typeof(Vector3), typeof(ObjectAsset), typeof(ItemAsset) }, null);
        if (registerAddObject == null)
        {
            Logger.LogWarning($"{method.Format()} - Unable to find method: LevelObjects.registerAddObject.", method: Source);
            DevkitServerModule.Fault();
        }

        MethodInfo? addSelection = eo.GetMethod(nameof(EditorObjects.addSelection), BindingFlags.Static | BindingFlags.Public, null, new Type[] { typeof(Transform) }, null);
        if (registerAddObject == null)
        {
            Logger.LogWarning($"{method.Format()} - Unable to find method: EditorObjects.addSelection.", method: Source);
            DevkitServerModule.Fault();
        }

        Label stLbl = generator.DefineLabel();

        int patchedReun = 0;
        bool patchedCopy = false;
        bool patchedPaste = false;
        bool patchedDelete = false;
        bool patchedPasteTransform = false;
        bool patchedInstantiate = false;

        List<CodeInstruction> ins = new List<CodeInstruction>(instructions);
        int i = 0;
        PatchUtil.InsertActionRateLimiter(ref i, stLbl, ins);
        for (; i < ins.Count; ++i)
        {
            // cancel reun operation
            if (i < ins.Count - 1 && clearSelection != null && ins[i].Calls(clearSelection) && (redo != null && ins[i + 1].Calls(redo) || undo != null && ins[i + 1].Calls(undo)))
            {
                MethodInfo invoker = (redo != null && ins[i + 1].Calls(redo) ? new Action(TransactionPatches.OnRedoRequested) : TransactionPatches.OnUndoRequested).Method;
                MethodBase? original = ins[i + 1].operand as MethodBase;
                ins.RemoveAt(i + 1);
                ins[i] = new CodeInstruction(OpCodes.Call, invoker);
                Logger.LogDebug($"[{Source}] Removed call to {original.Format()} in place of {invoker.Format()}.");
                ++patchedReun;
                continue;
            }

            // delete selection
            if (PatchUtil.FollowPattern(ins, ref i,

                    // ( if pressed delete
                    x => x.LoadsConstant(KeyCode.Delete),
                    x => x.Calls(Accessor.GetKeyDown),
                    x => x.opcode.IsBr(brtrue: true),

                    // or pressed backspace )
                    x => x.LoadsConstant(KeyCode.Backspace),
                    x => x.Calls(Accessor.GetKeyDown),
                    x => x.opcode.IsBr(brfalse: true),

                    // and selection count is greater than zero
                    x => selectedObjects != null && x.LoadsField(selectedObjects),
                    x => selectedObjectsCount != null && x.Calls(selectedObjectsCount),
                    null,
                    x => x.opcode.IsBrAny()))
            {
                Label? @goto = PatchUtil.GetNextBranchTarget(ins, i - 1);
                PatchUtil.ReturnIfFalse(ins, generator, ref i, OnDeleteSelection, @goto);
                Logger.LogDebug($"[{Source}] Patched deleting objects.");
                patchedDelete = true;
                continue;
            }

            // copy
            if (PatchUtil.MatchPattern(ins, i,
                    x => x.LoadsField(copiedObjects),
                    x => x.Calls(clearCopiedObjects)
                ))
            {
                Label? @goto = PatchUtil.GetNextBranchTarget(ins, i - 1);
                PatchUtil.ReturnIfFalse(ins, generator, ref i, CheckCanCopy, @goto);
                Logger.LogDebug($"[{Source}] Patched copying objects.");
                patchedCopy = true;
                continue;
            }

            // paste
            if (PatchUtil.FollowPattern(ins, ref i,

                    // if pressed v
                    x => x.LoadsConstant(KeyCode.V),
                    x => x.Calls(Accessor.GetKeyDown),
                    x => x.opcode.IsBr(brfalse: true),

                    // and copies count is greater than zero
                    x => copiedObjects != null && x.LoadsField(copiedObjects),
                    x => copiedObjectsCount != null && x.Calls(copiedObjectsCount),
                    null,
                    x => x.opcode.IsBrAny(),

                    // and holding control
                    x => x.LoadsConstant(KeyCode.LeftControl),
                    x => x.Calls(Accessor.GetKey),
                    x => x.opcode.IsBr(brfalse: true)
                ))
            {
                Label? @goto = PatchUtil.GetNextBranchTarget(ins, i - 1);
                if (!@goto.HasValue)
                    continue;

                Label notDevkitServerLabel = generator.DefineLabel();
                ins[i].labels.Add(notDevkitServerLabel);
                ins.Insert(i, new CodeInstruction(OpCodes.Call, Accessor.IsDevkitServerGetter));
                ins.Insert(i + 1, new CodeInstruction(OpCodes.Brfalse, notDevkitServerLabel));
                i += 2;
                ins.Insert(i, new CodeInstruction(OpCodes.Call, new Action(Paste).Method));
                ins.Insert(i + 1, new CodeInstruction(OpCodes.Br, @goto.Value));
                PatchUtil.ReturnIfFalse(ins, generator, ref i, CheckCanPaste, @goto);
                i += 2;
                Logger.LogDebug($"[{Source}] Patched pasteing objects.");
                patchedPaste = true;
                continue;
            }

            // ctrl n (paste transform)
            if (PatchUtil.MatchPattern(ins, i,

                    // pointSelection()
                    x => pointSelection != null && x.Calls(pointSelection),

                    // if ( selection.Count == 1 )
                    x => selectedObjects != null && x.LoadsField(selectedObjects),
                    x => selectedObjectsCount != null && x.Calls(selectedObjectsCount),
                    null,
                    x => x.opcode.IsBrAny())
                )
            {
                Label? @goto = PatchUtil.GetNextBranchTarget(ins, i - 1);
                PatchUtil.ReturnIfFalse(ins, generator, ref i, CheckCanMove, @goto);
                ins.Insert(i, new CodeInstruction(OpCodes.Call, new Action(SaveOneSelectedId).Method));
                ins[i].labels.AddRange(ins[i + 1].labels);
                ins[i + 1].labels.Clear();
                i += 6;
                PatchUtil.ContinueUntil(ins, ref i, x => x.Calls(calculateHandleOffsets), true);
                ins.Insert(i++, new CodeInstruction(OpCodes.Call, new Action(OnMoveOneObject).Method));
                PatchUtil.ContinueUntil(ins, ref i, x => x.opcode.IsBrAny(), true);
                ins.Insert(i++, new CodeInstruction(OpCodes.Call, new Action(PreIsPasteingTransform).Method));
                Logger.LogDebug($"[{Source}] Patched pasteing transform.");
                patchedPasteTransform = true;
                continue;
            }

            // instantiate object
            if (PatchUtil.MatchPattern(ins, i,
                    x => registerAddObject != null && x.Calls(registerAddObject),
                    x => x.opcode.IsStLoc()
                    ))
            {
                CodeInstruction callReplacement = new CodeInstruction(OpCodes.Call, new Action<Vector3, Quaternion, Vector3, ObjectAsset, ItemAsset>(OnInstantiate).Method);
                callReplacement.labels.AddRange(ins[i].labels);
                callReplacement.blocks.AddRange(ins[i].blocks);
                ins[i] = callReplacement;
                ++i;

                if (addSelection != null)
                    PatchUtil.RemoveUntil(ins, i, x => x.Calls(addSelection), true);
                
                patchedInstantiate = true;
                // continue; todo
            }
        }

        if (!patchedCopy)
        {
            Logger.LogWarning($"Unable to patch copy operations in {method.Format()}.", method: Source);
            DevkitServerModule.Fault();
        }
        if (!patchedPaste)
        {
            Logger.LogWarning($"Unable to replace paste operations in {method.Format()}.", method: Source);
            DevkitServerModule.Fault();
        }
        if (!patchedDelete)
        {
            Logger.LogWarning($"Unable to patch delete operations in {method.Format()}.", method: Source);
            DevkitServerModule.Fault();
        }
        if (!patchedPasteTransform)
        {
            Logger.LogWarning($"Unable to patch paste transform (Ctrl + N) operations in {method.Format()}.", method: Source);
            DevkitServerModule.Fault();
        }
        if (!patchedInstantiate)
        {
            Logger.LogWarning($"Unable to patch instantiate (E) operations in {method.Format()}.", method: Source);
            DevkitServerModule.Fault();
        }
        if (patchedReun < 2)
        {
            Logger.LogWarning($"Patched {patchedReun.Format()}/{2.Format()} undo/redo operations in {method.Format()}.", method: Source);
            DevkitServerModule.Fault();
        }

        return ins;
    }

    [HarmonyPatch(typeof(EditorObjects), "OnHandleTranslatedAndRotated")]
    [HarmonyPostfix]
    [UsedImplicitly]
    private static void EditorObjectsOnHandleTranslatedAndRotated(Vector3 worldPositionDelta, Quaternion worldRotationDelta, Vector3 pivotPosition, bool modifyRotation)
    {
        bool isFinal = IsFinalTransform;
        IsFinalTransform = false;
        if (!DevkitServerModule.IsEditing)
            return;

        List<EditorSelection> selections = EditorObjectSelection;

        Logger.LogDebug($"[CLIENT EVENTS] Move preview requested at: {string.Join(",", selections.Select(selection => selection.transform.gameObject.name.Format()))}: deltaPos: {worldPositionDelta.Format()}, deltaRot: {worldRotationDelta.eulerAngles.Format()}, pivotPos: {pivotPosition.Format()}, modifyRotation: {modifyRotation}.");

        TransformationDelta.TransformFlags flags = 0;
        if (!worldPositionDelta.IsNearlyZero())
            flags |= TransformationDelta.TransformFlags.Position | TransformationDelta.TransformFlags.OriginalPosition;
        if (modifyRotation && !worldRotationDelta.IsNearlyIdentity())
            flags |= TransformationDelta.TransformFlags.Rotation | TransformationDelta.TransformFlags.OriginalRotation;

        // no there's not enough lists
        List<uint> instanceIds = ListPool<uint>.claim();
        List<RegionIdentifier> buildableIds = ListPool<RegionIdentifier>.claim();
        List<TransformationDelta> translations = ListPool<TransformationDelta>.claim();
        List<TransformationDelta> buildableTranslations = ListPool<TransformationDelta>.claim();
        List<Vector3>? objScales = isFinal ? ListPool<Vector3>.claim() : null;
        List<Vector3>? objOriginalScales = isFinal ? ListPool<Vector3>.claim() : null;
        List<Vector3>? buildableScales = isFinal ? ListPool<Vector3>.claim() : null;
        List<Vector3>? buildableOriginalScales = isFinal ? ListPool<Vector3>.claim() : null;

        float dt = CachedTime.DeltaTime;
        try
        {
            int count = selections.Count;
            if (translations.Capacity < count)
                translations.Capacity = count;
            if (instanceIds.Capacity < count)
                instanceIds.Capacity = count;
            if (isFinal)
            {
                if (objScales!.Capacity < count)
                    objScales.Capacity = count;
                if (objOriginalScales!.Capacity < count)
                    objOriginalScales.Capacity = count;
            }

            bool globalUseScale = false;
            foreach (EditorSelection selection in selections)
            {
                TransformationDelta t = isFinal
                    ? new TransformationDelta(TransformationDelta.TransformFlags.All, selection.transform.position, selection.transform.rotation, selection.fromPosition, selection.fromRotation)
                    : new TransformationDelta(flags, worldPositionDelta, worldRotationDelta, selection.fromPosition, selection.fromRotation);
                bool useScale = isFinal && !selection.fromScale.IsNearlyEqual(selection.transform.localScale);
                globalUseScale |= useScale;
                if (LevelObjectUtil.TryFindObject(selection.transform, out RegionIdentifier id))
                {
                    LevelObject? levelObject = ObjectManager.getObject(id.X, id.Y, id.Index);
                    if (levelObject == null) continue;
                    instanceIds.Add(levelObject.instanceID);
                    translations.Add(t);
                    if (isFinal)
                    {
                        objScales!.Add(selection.transform.localScale);
                        objOriginalScales!.Add(selection.fromScale);
                        if (ClientEvents.ListeningOnMoveObjectFinal)
                            ClientEvents.InvokeOnMoveObjectFinal(new MoveObjectFinalProperties(levelObject.instanceID, t, selection.transform.localScale, selection.fromScale, useScale, dt));
                    }
                    else if (ClientEvents.ListeningOnMoveObjectPreview)
                        ClientEvents.InvokeOnMoveObjectPreview(new MoveObjectPreviewProperties(levelObject.instanceID, t, pivotPosition, false, dt));
                }
                else if (LevelObjectUtil.TryFindBuildable(selection.transform, out id))
                {
                    LevelBuildableObject? buildable = LevelObjectUtil.GetBuildable(id);
                    if (buildable == null) continue;
                    buildableIds.Add(id);
                    buildableTranslations.Add(t);
                    if (isFinal)
                    {
                        buildableScales!.Add(selection.transform.localScale);
                        buildableOriginalScales!.Add(selection.fromScale);
                        if (ClientEvents.ListeningOnMoveBuildableFinal)
                            ClientEvents.InvokeOnMoveBuildableFinal(new MoveBuildableFinalProperties(id, t, selection.transform.localScale, selection.fromScale, useScale, dt));
                    }
                    else if (ClientEvents.ListeningOnMoveBuildablePreview)
                        ClientEvents.InvokeOnMoveBuildablePreview(new MoveBuildablePreviewProperties(id, t, pivotPosition, false, dt));
                }

                if (instanceIds.Count + buildableIds.Count >= LevelObjectUtil.MaxMoveSelectionSize && !HasPermissionToOverrideSelectionLimit())
                    break;
            }

            if (isFinal)
            {
                ClientEvents.InvokeOnMoveLevelObjectsFinal(new MoveLevelObjectsFinalProperties(instanceIds.ToArrayFast(), translations.ToArrayFast(),
                    globalUseScale ? objOriginalScales!.ToArrayFast() : null, globalUseScale ? objScales!.ToArrayFast() : null,
                    buildableIds.ToArrayFast(), buildableTranslations.ToArrayFast(),
                    globalUseScale ? buildableOriginalScales!.ToArrayFast() : null, globalUseScale ? buildableScales!.ToArrayFast() : null
                    , dt));
            }
            else if (ClientEvents.ListeningOnMoveHierarchyObjectsPreview)
            {
                ClientEvents.InvokeOnMoveLevelObjectsPreview(new MoveLevelObjectsPreviewProperties(instanceIds.ToArrayFast(), translations.ToArrayFast(),
                    buildableIds.ToArrayFast(), buildableTranslations.ToArrayFast(), pivotPosition, dt));
            }
        }
        finally
        {
            ListPool<uint>.release(instanceIds);
            ListPool<RegionIdentifier>.release(buildableIds);
            ListPool<TransformationDelta>.release(translations);
            ListPool<TransformationDelta>.release(buildableTranslations);
            if (isFinal)
            {
                ListPool<Vector3>.release(objScales);
                ListPool<Vector3>.release(objOriginalScales);
                ListPool<Vector3>.release(buildableScales);
                ListPool<Vector3>.release(buildableOriginalScales);
            }
        }
    }
    private static void OnInstantiate(Vector3 position, Quaternion rotation, Vector3 scale, ObjectAsset objectAsset, ItemAsset buildableAsset)
    {
        if (!DevkitServerModule.IsEditing)
        {
            Transform? t = LevelObjects.registerAddObject(position, rotation, scale, objectAsset, buildableAsset);
            if (t != null)
                EditorObjects.addSelection(t);
            return;
        }

        if (objectAsset == null && buildableAsset == null)
            return;

        Logger.LogDebug($"[{Source}] Checking instantiate.");
        if (IsSyncing)
        {
            UIMessage.SendEditorMessage(DevkitServerModule.MessageLocalization.Translate("Syncing"));
            return;
        }

        if (!LevelObjectUtil.CheckPlacePermission())
        {
            UIMessage.SendNoPermissionMessage(VanillaPermissions.PlaceObjects);
            return;
        }

        LevelObjectUtil.RequestInstantiation(objectAsset == null ? buildableAsset.GUID : objectAsset.GUID, position, rotation, scale);
    }

    private static bool HasPermissionToOverrideSelectionLimit()
    {
        return ClientInfo.Info != null && ClientInfo.Info.ServerUsesBypassingObjectSelectionLimitPermission && VanillaPermissions.BypassObjectSelectionLimits.Has(false);
    }
    private static bool OnDeleteSelection()
    {
        if (!DevkitServerModule.IsEditing)
            return true;
        if (IsSyncing)
        {
            UIMessage.SendEditorMessage(DevkitServerModule.MessageLocalization.Translate("Syncing"));
            return false;
        }
        List<EditorSelection> selection = EditorObjectSelection;
        if (selection.Count > LevelObjectUtil.MaxSelectionSize && !HasPermissionToOverrideSelectionLimit())
        {
            UIMessage.SendEditorMessage(DevkitServerModule.MessageLocalization.Translate("TooManySelections", selection.Count, LevelObjectUtil.MaxSelectionSize));
            return false;
        }
        Logger.LogDebug($"[{Source}] Deleting selection.");
        List<uint> objects = ListPool<uint>.claim();
        if (objects.Capacity < selection.Count)
            objects.Capacity = selection.Count;
        List<RegionIdentifier> buildables = ListPool<RegionIdentifier>.claim();
        float dt = CachedTime.DeltaTime;
        try
        {
            for (int i = 0; i < selection.Count; ++i)
            {
                LevelObject? obj = LevelObjectUtil.FindObject(selection[i].transform);
                if (obj == null)
                {
                    if (!LevelObjectUtil.TryFindBuildable(selection[i].transform, out RegionIdentifier id) ||
                        !LevelObjectUtil.CheckDeleteBuildablePermission(id))
                    {
                        UIMessage.SendNoPermissionMessage(VanillaPermissions.RemoveSavedObjects);
                        return false;
                    }

                    buildables.Add(id);
                    continue;
                }
                if (!LevelObjectUtil.CheckDeletePermission(obj.instanceID))
                {
                    UIMessage.SendNoPermissionMessage(VanillaPermissions.RemoveSavedObjects);
                    return false;
                }
                objects.Add(obj.instanceID);
            }

            if (ClientEvents.ListeningOnDeleteLevelObjects)
                ClientEvents.InvokeOnDeleteLevelObjects(new DeleteLevelObjectsProperties(objects.ToArrayFast(), buildables.ToArrayFast(), dt));
            if (ClientEvents.ListeningOnDeleteObject)
            {
                for (int i = 0; i < objects.Count; ++i)
                    ClientEvents.InvokeOnDeleteObject(new DeleteObjectProperties(objects[i], dt));
            }
            if (ClientEvents.ListeningOnDeleteBuildable)
            {
                for (int i = 0; i < buildables.Count; ++i)
                    ClientEvents.InvokeOnDeleteBuildable(new DeleteBuildableProperties(buildables[i], dt));
            }
        }
        finally
        {
            ListPool<RegionIdentifier>.release(buildables);
            ListPool<uint>.release(objects);
        }

        return true;
    }
    private static bool CheckCanCopy()
    {
        if (!DevkitServerModule.IsEditing)
            return true;
        if (IsSyncing)
        {
            UIMessage.SendEditorMessage(DevkitServerModule.MessageLocalization.Translate("Syncing"));
            return false;
        }
        if (EditorObjectSelection.Count > LevelObjectUtil.MaxCopySelectionSize && !HasPermissionToOverrideSelectionLimit())
        {
            UIMessage.SendEditorMessage(DevkitServerModule.MessageLocalization.Translate("TooManySelections", EditorObjectSelection.Count, LevelObjectUtil.MaxCopySelectionSize));
            return false;
        }
        Logger.LogDebug($"[{Source}] Copying.");

        return true;
    }
    private static void Paste()
    {
        if (!DevkitServerModule.IsEditing || IsSyncing)
            return;
        Logger.LogDebug($"[{Source}] Pasteing.");
        LevelObjectUtil.ClientInstantiateObjectsAndLock(EditorObjectCopies.ToArrayFast());
    }
    private static bool CheckCanPaste()
    {
        if (!DevkitServerModule.IsEditing)
            return true;
        Logger.LogDebug($"[{Source}] Checking paste.");
        if (IsSyncing)
        {
            UIMessage.SendEditorMessage(DevkitServerModule.MessageLocalization.Translate("Syncing"));
            return false;
        }
        if (EditorObjectCopies.Count > LevelObjectUtil.MaxCopySelectionSize && !HasPermissionToOverrideSelectionLimit())
        {
            UIMessage.SendEditorMessage(DevkitServerModule.MessageLocalization.Translate("TooManySelections", EditorObjectCopies.Count, LevelObjectUtil.MaxCopySelectionSize));
            return false;
        }

        if (LevelObjectUtil.CheckPlacePermission()) return true;
        UIMessage.SendNoPermissionMessage(VanillaPermissions.PlaceObjects);
        return false;
    }
    private static bool CheckCanMove()
    {
        if (!DevkitServerModule.IsEditing)
            return true;
        if (IsSyncing)
        {
            UIMessage.SendEditorMessage(DevkitServerModule.MessageLocalization.Translate("Syncing"));
            return false;
        }
        List<EditorSelection> selection = EditorObjectSelection;
        if (selection.Count > LevelObjectUtil.MaxMoveSelectionSize && !HasPermissionToOverrideSelectionLimit())
        {
            UIMessage.SendEditorMessage(DevkitServerModule.MessageLocalization.Translate("TooManySelections", selection.Count, LevelObjectUtil.MaxMoveSelectionSize));
            return false;
        }
        Logger.LogDebug($"[{Source}] Checking move.");

        for (int i = 0; i < selection.Count; ++i)
        {
            LevelObject? obj = LevelObjectUtil.FindObject(selection[i].transform);
            if (obj == null)
            {
                if (!LevelObjectUtil.TryFindBuildable(selection[i].transform, out RegionIdentifier id) ||
                    !LevelObjectUtil.CheckMoveBuildablePermission(id))
                {
                    UIMessage.SendNoPermissionMessage(VanillaPermissions.RemoveSavedObjects);
                    return false;
                }
                continue;
            }
            if (!LevelObjectUtil.CheckMovePermission(obj.instanceID))
            {
                UIMessage.SendNoPermissionMessage(VanillaPermissions.RemoveSavedObjects);
                return false;
            }
        }

        Logger.LogDebug($"[{Source}] Can move.");
        return true;
    }
    private static void SaveOneSelectedId()
    {
        Logger.LogDebug($"[{Source}] Pre-moving single.");
        if (!DevkitServerModule.IsEditing || EditorObjectSelection.Count != 1)
            return;
        EditorSelection selection = EditorObjectSelection[0];
        Transform transform = selection.transform;
        if (!LevelObjectUtil.TryFindBuildable(transform, out RegionIdentifier id))
            return;
        _lastDeleting = id;
    }
    private static void PreIsPasteingTransform()
    {
        if (!DevkitServerModule.IsEditing || EditorObjectSelection.Count <= 1)
            return;
        Logger.LogDebug($"[{Source}] Pasteing transform for more than one.");

        IsFinalTransform = true;
    }
    private static void OnMoveOneObject()
    {
        if (!DevkitServerModule.IsEditing || EditorObjectSelection.Count != 1)
            return;
        Logger.LogDebug($"[{Source}] Moving single.");

        EditorSelection selection = EditorObjectSelection[0];
        Transform transform = selection.transform;
        LevelObject? obj = LevelObjectUtil.FindObject(transform);
        TransformationDelta delta = new TransformationDelta(TransformationDelta.TransformFlags.All, transform.position, transform.rotation, selection.fromPosition, selection.fromRotation);
        Vector3 scale = transform.localScale;
        bool useScale = scale.IsNearlyEqual(selection.fromScale);
        float dt = CachedTime.DeltaTime;
        if (obj == null)
        {
            if (!LevelObjectUtil.TryFindBuildable(transform, out RegionIdentifier id) || LevelObjectUtil.GetBuildable(id) is not { } buildable)
                return;
            
            if (Regions.tryGetCoordinate(delta.OriginalPosition, out byte x, out byte y) && (id.X != x || id.Y != y))
            {
                /*
                 * Replace the buildable if it was put in a different region.
                 * This is much simpler and more reliable than hoping two people
                 * don't move something into the same region at the same time.
                 *
                 * plz instance ids for buildables
                 */
                if (_lastDeleting.HasValue && ClientEvents.ListeningOnDeleteLevelObjects)
                {
                    EditorObjects.removeSelection(transform);
                    LevelObjects.registerRemoveObject(transform);
                    ClientEvents.InvokeOnDeleteLevelObjects(new DeleteLevelObjectsProperties(Array.Empty<uint>(), new RegionIdentifier[]
                    {
                        _lastDeleting.Value
                    }, dt));
                    LevelObjectUtil.ClientInstantiateObjectsAndLock(new EditorCopy[] { new EditorCopy(delta.Position, delta.Rotation, transform.localScale, null, buildable.asset) });
                    _lastDeleting = null;
                }
            }
            else
            {
                if (ClientEvents.ListeningOnMoveBuildableFinal)
                {
                    ClientEvents.InvokeOnMoveBuildableFinal(new MoveBuildableFinalProperties(id, delta, scale, selection.fromScale, useScale, dt));
                }
                if (ClientEvents.ListeningOnMoveLevelObjectsFinal)
                {
                    ClientEvents.InvokeOnMoveLevelObjectsFinal(new MoveLevelObjectsFinalProperties(Array.Empty<uint>(), Array.Empty<TransformationDelta>(), null, null,
                        new RegionIdentifier[] { id }, new TransformationDelta[] { delta },
                        useScale ? new Vector3[] { scale } : null, useScale ? new Vector3[] { selection.fromScale } : null, dt));
                }
            }
            return;
        }

        if (ClientEvents.ListeningOnMoveObjectFinal)
        {
            ClientEvents.InvokeOnMoveObjectFinal(new MoveObjectFinalProperties(obj.instanceID, delta, scale, selection.fromScale, useScale, dt));
        }
        if (ClientEvents.ListeningOnMoveLevelObjectsFinal)
        {
            ClientEvents.InvokeOnMoveLevelObjectsFinal(new MoveLevelObjectsFinalProperties(new uint[] { obj.instanceID }, new TransformationDelta[] { delta },
                useScale ? new Vector3[] { scale } : null, useScale ? new Vector3[] { selection.fromScale } : null,
                Array.Empty<RegionIdentifier>(), Array.Empty<TransformationDelta>(), null, null, dt));
        }
    }
}
#endif