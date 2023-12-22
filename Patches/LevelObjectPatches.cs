using DevkitServer.Models;
using DevkitServer.Multiplayer.Levels;
using HarmonyLib;
using System.Reflection;
using System.Reflection.Emit;
#if CLIENT
using DevkitServer.API;
using DevkitServer.API.Permissions;
using DevkitServer.API.UI;
using DevkitServer.Configuration;
using DevkitServer.Core.Permissions;
using DevkitServer.Core.UI.Extensions;
using DevkitServer.Multiplayer.Actions;
using DevkitServer.Players;
using SDG.Framework.Devkit.Transactions;
using SDG.Framework.Utilities;
#endif

namespace DevkitServer.Patches;

[HarmonyPatch]
internal static class LevelObjectPatches
{
    private const string Source = "OBJECT PATCHES";
#if CLIENT

    internal static bool IsSyncing;
    private static bool IsFinalTransform;
    private static readonly Action? CallCalculateHandleOffsets =
        Accessor.GenerateStaticCaller<EditorObjects, Action>("calculateHandleOffsets", allowUnsafeTypeBinding: true);

    private static readonly List<LevelObject> PendingMaterialUpdates = new List<LevelObject>(LevelObjectUtil.MaxUpdateObjectsPacketSize);


    internal static void OptionalPatches()
    {
        try
        {
            MethodInfo? onTypedMaterialPalletteOverride = typeof(EditorLevelObjectsUI).GetMethod(
                "OnTypedMaterialPaletteOverride", BindingFlags.NonPublic | BindingFlags.Static, null,
                CallingConventions.Any, new Type[] { typeof(ISleekField), typeof(string) }, null);
            if (onTypedMaterialPalletteOverride == null || !onTypedMaterialPalletteOverride.IsStatic)
            {
                Logger.DevkitServer.LogWarning(Source, "Method not found: static EditorLevelObjectsUI.OnTypedMaterialPaletteOverride. Custom material palettes will not replicate.");
            }
            else
            {
                PatchesMain.Patcher.Patch(onTypedMaterialPalletteOverride, transpiler: 
                    new HarmonyMethod(new Func<IEnumerable<CodeInstruction>, ILGenerator, MethodBase, IEnumerable<CodeInstruction>>(OnTypedCustomMaterialPaletteOverridePrefix).Method));
            }
            MethodInfo? onTypedMaterialIndexOverride = typeof(EditorLevelObjectsUI).GetMethod(
                "OnTypedMaterialIndexOverride", BindingFlags.NonPublic | BindingFlags.Static, null,
                CallingConventions.Any, new Type[] { typeof(ISleekInt32Field), typeof(int) }, null);
            if (onTypedMaterialIndexOverride == null || !onTypedMaterialIndexOverride.IsStatic)
            {
                Logger.DevkitServer.LogWarning(Source, "Method not found: static EditorLevelObjectsUI.OnTypedMaterialIndexOverride. Material index overrides will not replicate.");
            }
            else
            {
                PatchesMain.Patcher.Patch(onTypedMaterialIndexOverride, transpiler: 
                    new HarmonyMethod(new Func<IEnumerable<CodeInstruction>, ILGenerator, MethodBase, IEnumerable<CodeInstruction>>(OnTypedMaterialIndexOverridePrefix).Method));
            }
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogWarning(Source, ex, "Failed to patch recommended patches for Level Objects.");
        }
    }

    [HarmonyPatch(typeof(EditorObjects), "Update")]
    [HarmonyTranspiler]
    [UsedImplicitly]
    private static IEnumerable<CodeInstruction> EditorObjectsUpdateTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase method)
    {
        Type eo = typeof(EditorObjects);
        Type lo = typeof(LevelObjects);
        Type dtu = typeof(DevkitTransactionUtility);

        MethodInfo? pointSelection = eo.GetMethod("pointSelection", BindingFlags.Static | BindingFlags.Public, null, Type.EmptyTypes, null);
        if (pointSelection == null)
        {
            Logger.DevkitServer.LogWarning(Source, $"{method.Format()} - Unable to find method: EditorObjects.pointSelection.");
            DevkitServerModule.Fault();
        }
        FieldInfo? selectedObjects = eo.GetField("selection", BindingFlags.Static | BindingFlags.NonPublic);
        if (selectedObjects == null)
        {
            Logger.DevkitServer.LogWarning(Source, $"{method.Format()} - Unable to find field: EditorObjects.selection.");
            DevkitServerModule.Fault();
        }
        FieldInfo? handles = eo.GetField("handles", BindingFlags.Static | BindingFlags.NonPublic);
        if (handles == null)
        {
            Logger.DevkitServer.LogWarning(Source, $"{method.Format()} - Unable to find field: EditorObjects.handles.");
            DevkitServerModule.Fault();
        }
        MethodInfo? selectedObjectsCount = selectedObjects?.FieldType.GetProperty(nameof(List<object>.Count), BindingFlags.Instance | BindingFlags.Public)?.GetMethod;
        if (selectedObjectsCount == null)
        {
            Logger.DevkitServer.LogWarning(Source, $"{method.Format()} - Unable to find property: EditorObjects.selection.Count.");
            DevkitServerModule.Fault();
        }
        FieldInfo? copiedObjects = eo.GetField("copies", BindingFlags.Static | BindingFlags.NonPublic);
        if (copiedObjects == null)
        {
            Logger.DevkitServer.LogWarning(Source, $"{method.Format()} - Unable to find field: EditorObjects.copies.");
            DevkitServerModule.Fault();
        }
        MethodInfo? copiedObjectsCount = copiedObjects?.FieldType.GetProperty(nameof(List<object>.Count), BindingFlags.Instance | BindingFlags.Public)?.GetMethod;
        if (copiedObjectsCount == null)
        {
            Logger.DevkitServer.LogWarning(Source, $"{method.Format()} - Unable to find property: EditorObjects.copies.Count.");
            DevkitServerModule.Fault();
        }
        MethodInfo? clearCopiedObjects = copiedObjects?.FieldType.GetMethod(nameof(List<object>.Clear), BindingFlags.Instance | BindingFlags.Public);
        if (clearCopiedObjects == null)
        {
            Logger.DevkitServer.LogWarning(Source, $"{method.Format()} - Unable to find method: EditorObjects.copies.Clear.");
            DevkitServerModule.Fault();
        }

        MethodInfo? clearSelection = eo.GetMethod("clearSelection", BindingFlags.Static | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
        if (clearSelection == null)
        {
            Logger.DevkitServer.LogWarning(Source, $"{method.Format()} - Unable to find method: EditorObjects.clearSelection.");
            DevkitServerModule.Fault();
        }

        MethodInfo? calculateHandleOffsets = eo.GetMethod("calculateHandleOffsets", BindingFlags.Static | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
        if (calculateHandleOffsets == null)
        {
            Logger.DevkitServer.LogWarning(Source, $"{method.Format()} - Unable to find method: EditorObjects.calculateHandleOffsets.");
            DevkitServerModule.Fault();
        }

        MethodInfo? undo = lo.GetMethod("undo", BindingFlags.Static | BindingFlags.Public, null, Type.EmptyTypes, null);
        if (undo == null)
        {
            Logger.DevkitServer.LogWarning(Source, $"{method.Format()} - Unable to find method: LevelObjects.undo.");
            DevkitServerModule.Fault();
        }

        MethodInfo? redo = lo.GetMethod("redo", BindingFlags.Static | BindingFlags.Public, null, Type.EmptyTypes, null);
        if (redo == null)
        {
            Logger.DevkitServer.LogWarning(Source, $"{method.Format()} - Unable to find method: LevelObjects.redo.");
            DevkitServerModule.Fault();
        }

        MethodInfo? recordDestruction = dtu.GetMethod(nameof(DevkitTransactionUtility.recordDestruction), BindingFlags.Static | BindingFlags.Public, null, new Type[] { typeof(GameObject) }, null);
        if (recordDestruction == null)
        {
            Logger.DevkitServer.LogWarning(Source, $"{method.Format()} - Unable to find method: DevkitTransactionUtility.recordDestruction.");
            DevkitServerModule.Fault();
        }

        MethodInfo? registerAddObject = lo.GetMethod(nameof(LevelObjects.registerAddObject), BindingFlags.Static | BindingFlags.Public, null, new Type[] { typeof(Vector3), typeof(Quaternion), typeof(Vector3), typeof(ObjectAsset), typeof(ItemAsset) }, null);
        if (registerAddObject == null)
        {
            Logger.DevkitServer.LogWarning(Source, $"{method.Format()} - Unable to find method: LevelObjects.registerAddObject.");
            DevkitServerModule.Fault();
        }

        MethodInfo? addSelection = eo.GetMethod(nameof(EditorObjects.addSelection), BindingFlags.Static | BindingFlags.Public, null, new Type[] { typeof(Transform) }, null);
        if (registerAddObject == null)
        {
            Logger.DevkitServer.LogWarning(Source, $"{method.Format()} - Unable to find method: EditorObjects.addSelection.");
            DevkitServerModule.Fault();
        }

        int patchedReun = 0;
        bool patchedCopy = false;
        bool patchedPaste = false;
        bool patchedDelete = false;
        bool patchedHandleDown = false;
        bool patchedPasteTransform = false;
        bool patchedInstantiate = false;
        bool patchedTranslate = false;

        List<CodeInstruction> ins = [..instructions];
        ins.Insert(0, new CodeInstruction(OpCodes.Call, Accessor.GetMethod(OnUpdate)));
        for (int i = 1; i < ins.Count; ++i)
        {
            // cancel reun operation
            if (i < ins.Count - 1 && clearSelection != null && ins[i].Calls(clearSelection) && (redo != null && ins[i + 1].Calls(redo) || undo != null && ins[i + 1].Calls(undo)))
            {
                MethodInfo? invoker = redo != null && ins[i + 1].Calls(redo) ? Accessor.GetMethod(TransactionPatches.OnRedoRequested) : Accessor.GetMethod(TransactionPatches.OnUndoRequested);
                MethodBase? original = ins[i + 1].operand as MethodBase;
                ins.RemoveAt(i + 1);
                ins[i] = new CodeInstruction(OpCodes.Call, invoker);
                Logger.DevkitServer.LogDebug(Source, $"Removed call to {original.Format()} in place of {invoker.Format()}.");
                ++patchedReun;
                continue;
            }

            // start using handle
            if (!patchedHandleDown && PatchUtil.MatchPattern(ins, i,

                    // EditorObjects.pointSelection()
                    x => pointSelection != null && x.Calls(pointSelection),
                    // EditorObjects.handles.[...]
                    x => x.LoadsField(handles)
                ))
            {
                Label? @goto = PatchUtil.GetNextBranchTarget(ins, i);
                PatchUtil.ReturnIfFalse(ins, generator, ref i, CheckCanMove, @goto);
                patchedHandleDown = true;
                continue;
            }

            // translate with E
            if (!patchedTranslate && patchedHandleDown && patchedPasteTransform && pointSelection != null && ins[i].Calls(pointSelection))
            {
                ins.Insert(i + 1, new CodeInstruction(OpCodes.Call, Accessor.GetMethod(PreIsFinalExternallyTransforming)));
                patchedTranslate = true;
            }

            // delete selection
            if (!patchedDelete && PatchUtil.FollowPattern(ins, ref i,

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
                Logger.DevkitServer.LogDebug(Source, "Patched deleting objects.");
                patchedDelete = true;
                continue;
            }

            // copy
            if (!patchedCopy && PatchUtil.MatchPattern(ins, i,
                    x => x.LoadsField(copiedObjects),
                    x => x.Calls(clearCopiedObjects)
                ))
            {
                Label? @goto = PatchUtil.GetNextBranchTarget(ins, i - 1);
                PatchUtil.ReturnIfFalse(ins, generator, ref i, CheckCanCopy, @goto);
                Logger.DevkitServer.LogDebug(Source, "Patched copying objects.");
                patchedCopy = true;
                continue;
            }

            // paste
            if (!patchedPaste && PatchUtil.FollowPattern(ins, ref i,

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
                Logger.DevkitServer.LogDebug(Source, "Patched pasteing objects.");
                patchedPaste = true;
                int index2 = PatchUtil.FindLabelDestinationIndex(ins, @goto.Value, i);
                if (index2 >= 0)
                    i = index2;
                continue;
            }

            // ctrl n (paste transform)
            if (!patchedPasteTransform && PatchUtil.MatchPattern(ins, i,

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
                ins[i].labels.AddRange(ins[i + 1].labels);
                ins[i + 1].labels.Clear();
                i += 6;
                PatchUtil.ContinueUntil(ins, ref i, x => x.Calls(calculateHandleOffsets), false);
                CodeInstruction instr = new CodeInstruction(OpCodes.Call, Accessor.GetMethod(OnMoveOneObject));
                ins.Insert(i++, instr);
                instr.labels.AddRange(ins[i].labels);
                ins[i].labels.Clear();
                PatchUtil.ContinueUntil(ins, ref i, x => x.opcode.IsBrAny(), false);
                instr = new CodeInstruction(OpCodes.Call, Accessor.GetMethod(PreIsFinalExternallyTransforming));
                ins.Insert(i++, instr);
                instr.labels.AddRange(ins[i].labels);
                ins[i].labels.Clear();
                Logger.DevkitServer.LogDebug(Source, "Patched pasteing transform.");
                patchedPasteTransform = true;
                continue;
            }

            // instantiate object
            if (!patchedInstantiate && PatchUtil.MatchPattern(ins, i,
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
            }
        }

        if (!patchedCopy)
        {
            Logger.DevkitServer.LogWarning(Source, $"Unable to patch copy operations in {method.Format()}.");
            DevkitServerModule.Fault();
        }
        if (!patchedPaste)
        {
            Logger.DevkitServer.LogWarning(Source, $"Unable to replace paste operations in {method.Format()}.");
            DevkitServerModule.Fault();
        }
        if (!patchedDelete)
        {
            Logger.DevkitServer.LogWarning(Source, $"Unable to patch delete operations in {method.Format()}.");
            DevkitServerModule.Fault();
        }
        if (!patchedPasteTransform)
        {
            Logger.DevkitServer.LogWarning(Source, $"Unable to patch paste transform (Ctrl + N) operations in {method.Format()}.");
            DevkitServerModule.Fault();
        }
        if (!patchedInstantiate)
        {
            Logger.DevkitServer.LogWarning(Source, $"Unable to patch instantiate (E) operations in {method.Format()}.");
            DevkitServerModule.Fault();
        }
        if (!patchedHandleDown)
        {
            Logger.DevkitServer.LogWarning(Source, $"Unable to patch handle down operation in {method.Format()}.");
            DevkitServerModule.Fault();
        }
        if (!patchedTranslate)
        {
            Logger.DevkitServer.LogWarning(Source, $"Unable to patch translate (E) operation in {method.Format()}.");
            DevkitServerModule.Fault();
        }
        if (patchedReun < 2)
        {
            Logger.DevkitServer.LogWarning(Source, $"Patched {patchedReun.Format()}/{2.Format()} undo/redo operations in {method.Format()}.");
            DevkitServerModule.Fault();
        }
        
        return ins;
    }

    [HarmonyPatch(typeof(EditorObjects), "releaseHandle")]
    [HarmonyPrefix]
    [UsedImplicitly]
    private static bool EditorObjectsOnReleaseHandlePrefix() => CheckCanMove();

    [HarmonyPatch(typeof(EditorObjects), "releaseHandle")]
    [HarmonyPostfix]
    [UsedImplicitly]
    private static void EditorObjectsOnReleaseHandlePostfix(bool __runOriginal, ref bool ___isUsingHandle)
    {
        if (!DevkitServerModule.IsEditing)
            return;
        if (__runOriginal)
            MoveFinalSelection();
        else
        {
            CancelMove();
            ___isUsingHandle = false;
        }
    }
    private static void CancelMove()
    {
        List<EditorSelection> selections = LevelObjectUtil.EditorObjectSelection;

        for (int i = 0; i < selections.Count; ++i)
        {
            EditorSelection editorSelection = selections[i];
            editorSelection.transform.SetPositionAndRotation(editorSelection.fromPosition, editorSelection.fromRotation);
            editorSelection.transform.localScale = editorSelection.fromScale;
        }
        LevelObjectUtil.EditorObjectHandles.MouseUp();
        CallCalculateHandleOffsets?.Invoke();
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

        if (isFinal)
            MoveFinalSelection();
    }
    private static void MoveFinalSelection()
    {
        List<EditorSelection> selections = LevelObjectUtil.EditorObjectSelection;
        if (selections.Count == 1)
        {
            OnMoveOneObject();
            return;
        }

        Logger.DevkitServer.LogDebug(Source, $"Move final requested at: {string.Join(",", selections.Select(selection => selection.transform.gameObject.name.Format()))}.");

        const TransformationDelta.TransformFlags flags = TransformationDelta.TransformFlags.All;
        
        List<FinalTransformation> transformations = ListPool<FinalTransformation>.claim();

        float dt = CachedTime.DeltaTime;
        try
        {
            int count = selections.Count;
            transformations.IncreaseCapacity(Math.Min(count, LevelObjectUtil.MaxMovePacketSize));

            bool globalUseScale = false;
            for (int i = 0; i < selections.Count; i++)
            {
                EditorSelection selection = selections[i];
                if (!NetIdRegistry.GetTransformNetId(selection.transform, out NetId netId, out _))
                    continue;

                Vector3 scale = selection.transform.localScale;
                bool useScale = !selection.fromScale.IsNearlyEqual(scale);
                globalUseScale |= useScale;

                FinalTransformation transformation = new FinalTransformation(netId,
                    new TransformationDelta(flags, selection.transform.position, selection.transform.rotation,
                        selection.fromPosition, selection.fromRotation),
                    scale, selection.fromScale);

                transformations.Add(transformation);

                if (ClientEvents.ListeningOnMoveLevelObjectFinal)
                    ClientEvents.InvokeOnMoveLevelObjectFinal(new MoveLevelObjectFinalProperties(transformation, useScale, dt));

                if (transformations.Count % LevelObjectUtil.MaxMovePacketSize == 0)
                    Flush();

                LevelObjectUtil.SyncIfAuthority(netId);
            }

            Flush();

            for (int i = 0; i < selections.Count; ++i)
            {
                EditorSelection selection = selections[i];
                if (LevelObjectUtil.TryFindObject(selection.transform, out RegionIdentifier id))
                {
                    if (LevelObjectUtil.GetObjectUnsafe(id) is { isSpeciallyCulled: true })
                        LevelObjectUtil.UpdateContainingCullingVolumesForMove(selection.fromPosition, selection.transform.position);
                    else
                        LevelObjectUtil.UpdateContainingCullingVolumes(selection.transform.position);
                }
            }

            void Flush()
            {
                if (ClientEvents.ListeningOnMoveLevelObjectsFinal)
                    ClientEvents.InvokeOnMoveLevelObjectsFinal(new MoveLevelObjectsFinalProperties(transformations.ToArrayFast(), globalUseScale, dt));

                transformations.Clear();
                globalUseScale = false;
            }
        }
        finally
        {
            ListPool<FinalTransformation>.release(transformations);
        }
    }

    private static Transform? _hover;
    private static void OnUpdate()
    {
        // highlight fade time seconds
        const float fade = 0.1f;

        if (!EditorObjects.isBuilding)
        {
            if (_hover != null)
            {
                HighlighterUtil.Unhighlight(_hover, fade);
                _hover = null;
            }
            return;
        }

        if (DevkitServerConfig.Config.EnableObjectUIExtension)
            EditorLevelObjectsUIExtension.OnUpdate();

        bool midClick = InputEx.GetKeyDown(KeyCode.Mouse2);

        if ((!DevkitServerConfig.Config.RemoveCosmeticImprovements || midClick) && EditorInteractEx.TryGetWorldHit(out RaycastHit hit) && (hit.transform.CompareTag("Large") || hit.transform.CompareTag("Medium") ||
                hit.transform.CompareTag("Small") || hit.transform.CompareTag("Barricade") ||
                hit.transform.CompareTag("Structure")) &&
            LevelObjectUtil.TryFindObjectOrBuildable(hit.transform, out LevelObject? @object, out LevelBuildableObject? buildable, true))
        {
            Asset? asset = @object?.asset ?? (Asset?)buildable?.asset;

            if (midClick)
                LevelObjectUtil.SelectObjectType(asset);

            if (DevkitServerConfig.Config.RemoveCosmeticImprovements)
                return;
            // object hover highlights
            bool dif = _hover != hit.transform;
            if (_hover is not null && dif)
            {
                if (_hover != null && !LevelObjectUtil.IsSelected(_hover))
                    HighlighterUtil.Unhighlight(_hover, fade);
                
                _hover = null;
            }

            if (dif)
            {
                if (!LevelObjectUtil.IsSelected(hit.transform))
                    HighlighterUtil.Highlight(hit.transform, Color.black, fade);
                
                _hover = hit.transform;
            }

            return;
        }
        
        if (_hover != null)
        {
            if (!LevelObjectUtil.IsSelected(_hover))
                HighlighterUtil.Unhighlight(_hover, fade);

            _hover = null!;
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

        Logger.DevkitServer.LogDebug(Source, "Checking instantiate.");
        if (IsSyncing || EditorActions.HasLargeQueue())
        {
            EditorMessage.SendEditorMessage(DevkitServerModule.MessageLocalization.Translate("Syncing"));
            return;
        }

        if (!VanillaPermissions.PlaceObjects.Has())
        {
            EditorMessage.SendNoPermissionMessage(VanillaPermissions.PlaceObjects);
            return;
        }

        LevelObjectUtil.RequestInstantiation(objectAsset?.GUID ?? buildableAsset.GUID, position, rotation, scale);
    }
    
    private static bool OnDeleteSelection()
    {
        if (!DevkitServerModule.IsEditing)
            return true;
        if (IsSyncing || EditorActions.HasLargeQueue())
        {
            EditorMessage.SendEditorMessage(DevkitServerModule.MessageLocalization.Translate("Syncing"));
            return false;
        }
        List<EditorSelection> selections = LevelObjectUtil.EditorObjectSelection;
        Logger.DevkitServer.LogDebug(Source, "Deleting selection.");

        List<NetId> netIds = ListPool<NetId>.claim();
        netIds.IncreaseCapacity(selections.Count);
        
        float dt = CachedTime.DeltaTime;
        try
        {
            for (int i = 0; i < selections.Count; ++i)
            {
                EditorSelection selection = selections[i];
                if (!NetIdRegistry.GetTransformNetId(selection.transform, out NetId netId, out _))
                    continue;
                
                // check permissions
                if (!LevelObjectUtil.TryFindObject(selection.transform, out RegionIdentifier id))
                {
                    if (!LevelObjectUtil.TryFindBuildable(selection.transform, out id))
                        continue;
                    if (!LevelObjectUtil.CheckDeleteBuildablePermission(id))
                    {
                        EditorMessage.SendNoPermissionMessage(VanillaPermissions.RemoveUnownedObjects);
                        return false;
                    }
                }
                else if (!LevelObjectUtil.CheckDeletePermission(LevelObjectUtil.GetObjectUnsafe(id).instanceID))
                {
                    EditorMessage.SendNoPermissionMessage(VanillaPermissions.RemoveUnownedObjects);
                    return false;
                }

                netIds.Add(netId);
                if (netIds.Count % LevelObjectUtil.MaxDeletePacketSize == 0)
                    Flush();

                LevelObjectUtil.SyncIfAuthority(netId);
            }

            Flush();

            void Flush()
            {
                if (ClientEvents.ListeningOnDeleteLevelObjects)
                    ClientEvents.InvokeOnDeleteLevelObjects(new DeleteLevelObjectsProperties(netIds.ToArrayFast(), dt));

                if (ClientEvents.ListeningOnDeleteLevelObject)
                {
                    for (int i = 0; i < netIds.Count; ++i)
                        ClientEvents.InvokeOnDeleteLevelObject(new DeleteLevelObjectProperties(netIds[i], dt));
                }

                netIds.Clear();
            }
        }
        finally
        {
            ListPool<NetId>.release(netIds);
        }

        return true;
    }
    private static bool CheckCanCopy()
    {
        if (!DevkitServerModule.IsEditing)
            return true;
        if (IsSyncing || EditorActions.HasLargeQueue())
        {
            EditorMessage.SendEditorMessage(DevkitServerModule.MessageLocalization.Translate("Syncing"));
            return false;
        }
        if (LevelObjectUtil.EditorObjectSelection.Count > LevelObjectUtil.MaxCopySelectionSize)
        {
            EditorMessage.SendEditorMessage(DevkitServerModule.MessageLocalization.Translate("TooManySelections", LevelObjectUtil.EditorObjectSelection.Count, LevelObjectUtil.MaxCopySelectionSize));
            return false;
        }
        Logger.DevkitServer.LogDebug(Source, "Copying.");

        return true;
    }
    private static void Paste()
    {
        if (!DevkitServerModule.IsEditing || IsSyncing)
            return;
        Logger.DevkitServer.LogDebug(Source, "Pasteing.");
        LevelObjectUtil.ClearSelection();
        LevelObjectUtil.ClientInstantiateObjectsAndLock(LevelObjectUtil.EditorObjectCopies.ToArrayFast());
    }
    private static bool CheckCanPaste()
    {
        if (!DevkitServerModule.IsEditing)
            return true;
        Logger.DevkitServer.LogDebug(Source, "Checking paste.");
        if (IsSyncing || EditorActions.HasLargeQueue())
        {
            EditorMessage.SendEditorMessage(DevkitServerModule.MessageLocalization.Translate("Syncing"));
            return false;
        }

        List<EditorCopy> copies = LevelObjectUtil.EditorObjectCopies;
        if (copies.Count > LevelObjectUtil.MaxCopySelectionSize)
        {
            EditorMessage.SendEditorMessage(DevkitServerModule.MessageLocalization.Translate("TooManySelections", copies.Count, LevelObjectUtil.MaxCopySelectionSize));
            return false;
        }

        if (VanillaPermissions.PlaceObjects.Has()) return true;
        EditorMessage.SendNoPermissionMessage(VanillaPermissions.PlaceObjects);
        return false;
    }
    private static bool CheckCanMove()
    {
        if (!DevkitServerModule.IsEditing)
            return true;
        if (IsSyncing)
        {
            EditorMessage.SendEditorMessage(DevkitServerModule.MessageLocalization.Translate("Syncing"));
            return false;
        }
        List<EditorSelection> selection = LevelObjectUtil.EditorObjectSelection;
        Logger.DevkitServer.LogDebug(Source, "Checking move.");

        for (int i = 0; i < selection.Count; ++i)
        {
            LevelObject? obj = LevelObjectUtil.FindObject(selection[i].transform);
            if (obj == null)
            {
                if (!LevelObjectUtil.TryFindBuildable(selection[i].transform, out RegionIdentifier id) ||
                    !LevelObjectUtil.CheckMoveBuildablePermission(id))
                {
                    EditorMessage.SendNoPermissionMessage(VanillaPermissions.MoveUnownedObjects);
                    return false;
                }
                continue;
            }
            if (!LevelObjectUtil.CheckMovePermission(obj.instanceID))
            {
                EditorMessage.SendNoPermissionMessage(VanillaPermissions.MoveUnownedObjects);
                return false;
            }
        }

        Logger.DevkitServer.LogDebug(Source, "Can move.");
        return true;
    }
    private static void PreIsFinalExternallyTransforming()
    {
        if (!DevkitServerModule.IsEditing)
            return;
        Logger.DevkitServer.LogDebug(Source, "Setting next ExternallyTransformPoint to final.");

        IsFinalTransform = true;
    }
    private static void OnMoveOneObject()
    {
        if (!DevkitServerModule.IsEditing || LevelObjectUtil.EditorObjectSelection.Count != 1)
            return;
        Logger.DevkitServer.LogDebug(Source, "Moving single.");

        EditorSelection selection = LevelObjectUtil.EditorObjectSelection[0];
        Transform transform = selection.transform;

        if (!NetIdRegistry.GetTransformNetId(transform, out NetId netId, out _))
            return;

        Vector3 scale = transform.localScale;
        bool useScale = !scale.IsNearlyEqual(selection.fromScale);

        FinalTransformation transformation = new FinalTransformation(netId,
            new TransformationDelta(TransformationDelta.TransformFlags.All, transform.position, transform.rotation,
                selection.fromPosition, selection.fromRotation), scale, selection.fromScale);

        if (LevelObjectUtil.TryFindObject(selection.transform, out RegionIdentifier id))
        {
            if (LevelObjectUtil.GetObjectUnsafe(id) is { isSpeciallyCulled: true })
                LevelObjectUtil.UpdateContainingCullingVolumesForMove(selection.fromPosition, transformation.Transformation.Position);
            else
                LevelObjectUtil.UpdateContainingCullingVolumes(transformation.Transformation.Position);
        }

        float dt = CachedTime.DeltaTime;

        if (ClientEvents.ListeningOnMoveLevelObjectsFinal)
            ClientEvents.InvokeOnMoveLevelObjectsFinal(new MoveLevelObjectsFinalProperties(new FinalTransformation[] { transformation }, useScale, dt));

        if (ClientEvents.ListeningOnMoveLevelObjectFinal)
            ClientEvents.InvokeOnMoveLevelObjectFinal(new MoveLevelObjectFinalProperties(transformation, useScale, dt));

        LevelObjectUtil.SyncIfAuthority(netId);
    }

    private static IEnumerable<CodeInstruction> OnTypedCustomMaterialPaletteOverridePrefix(IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase method)
    {
        FieldInfo? field = typeof(LevelObject).GetField("customMaterialOverride", BindingFlags.NonPublic | BindingFlags.Instance);
        if (field == null || !typeof(AssetReference<MaterialPaletteAsset>).IsAssignableFrom(field.FieldType))
        {
            Logger.DevkitServer.LogWarning(Source, $"Field not found: LevelObject.customMaterialOverride in method {method.Format()}. Custom material palettes will not replicate.");
            return instructions;
        }

        List<CodeInstruction> ins = [..instructions];
        bool patchedInner = false;
        bool patchedOuter = false;
        LocalBuilder lcl = generator.DeclareLocal(typeof(AssetReference<MaterialPaletteAsset>));
        int indexRangeSt = -1;
        LocalBuilder? valueLcl = null;
        int valueLclIndex = -1;
        for (int i = 2; i < ins.Count; i++)
        {
            if (!patchedInner && ins[i].StoresField(field))
            {
                indexRangeSt = i;
                ins.Insert(i, ins[i - 2].CopyWithoutSpecial());
                ins.Insert(i + 1, new CodeInstruction(OpCodes.Ldfld, field));
                ins.Insert(i + 2, PatchUtil.GetLocalCodeInstruction(lcl, -1, true, false));
                ins.Insert(i + 4, ins[i - 2].CopyWithoutSpecial());
                ins.Insert(i + 5, ins[i - 1].CopyWithoutSpecial());
                valueLcl = PatchUtil.GetLocal(ins[i - 1], out valueLclIndex, false);
                ins.Insert(i + 6, PatchUtil.GetLocalCodeInstruction(lcl, -1, false, false));
                ins.Insert(i + 7, new CodeInstruction(OpCodes.Call, new Action<LevelObject, AssetReference<MaterialPaletteAsset>, AssetReference<MaterialPaletteAsset>>(OnCustomMaterialPaletteOverrideUpdated).Method));
                i += 7;
                patchedInner = true;
                Logger.DevkitServer.LogDebug(Source, $"Patched {method.Format()}.");
            }
            else if (patchedInner && !patchedOuter && ins[i].opcode == OpCodes.Endfinally)
            {
                ins.Insert(i, PatchUtil.GetLocalCodeInstruction(valueLcl, valueLclIndex, false));
                ins.Insert(i + 1, new CodeInstruction(OpCodes.Call, new Action<AssetReference<MaterialPaletteAsset>>(EndCustomMaterialPaletteOverrideChangeGroup).Method));
                i += 2;
                patchedOuter = true;
            }
        }
        if (patchedInner && !patchedOuter)
        {
            ins.RemoveRange(indexRangeSt + 4, 4);
            ins.RemoveRange(indexRangeSt, 3);
            Logger.DevkitServer.LogWarning(Source, $"Unpatched {method.Format()} to avoid a memory leak because patch was not successful.");
        }
        else if (!patchedInner)
            Logger.DevkitServer.LogWarning(Source, $"Failed to patch {method.Format()} loop. Custom material palettes will not replicate.");
        if (!patchedOuter)
            Logger.DevkitServer.LogWarning(Source, $"Failed to patch {method.Format()} finally. Custom material palettes will not replicate.");

        return ins;
    }
    private static void OnCustomMaterialPaletteOverrideUpdated(LevelObject obj, AssetReference<MaterialPaletteAsset> @new, AssetReference<MaterialPaletteAsset> old)
    {
        if (@new.GUID == old.GUID || !DevkitServerModule.IsEditing)
            return;
        if (!LevelObjectUtil.CheckMovePermission(obj.instanceID))
        {
            EditorMessage.SendNoPermissionMessage(VanillaPermissions.MoveUnownedObjects);
            obj.SetCustomMaterialPaletteOverride(old);
            return;
        }

        Logger.DevkitServer.LogDebug(Source, $"Custom material updated: {obj.asset.Format()}, {old.GUID.Format()} -> {@new.GUID.Format()}");
        PendingMaterialUpdates.Add(obj);
    }
    private static void EndCustomMaterialPaletteOverrideChangeGroup(AssetReference<MaterialPaletteAsset> value)
    {
        if (PendingMaterialUpdates.Count == 0 || !DevkitServerModule.IsEditing)
            return;
        List<NetId> netIds = ListPool<NetId>.claim();
        try
        {
            netIds.IncreaseCapacity(PendingMaterialUpdates.Count);
            for (int i = 0; i < PendingMaterialUpdates.Count; i++)
            {
                LevelObject obj = PendingMaterialUpdates[i];
                if (LevelObjectNetIdDatabase.TryGetObjectNetId(obj, out NetId netId))
                {
                    netIds.Add(netId);

                    if (netIds.Count % LevelObjectUtil.MaxUpdateObjectsPacketSize == 0)
                        Flush();
                }
            }

            Flush();

            void Flush()
            {
                if (ClientEvents.ListeningOnUpdateObjectsCustomMaterialPaletteOverride)
                    ClientEvents.InvokeOnUpdateObjectsCustomMaterialPaletteOverride(new UpdateObjectsCustomMaterialPaletteOverrideProperties(netIds.ToArrayFast(), value, CachedTime.DeltaTime));

                netIds.Clear();
            }
        }
        finally
        {
            ListPool<NetId>.release(netIds);
            PendingMaterialUpdates.Clear();
        }
    }
    private static IEnumerable<CodeInstruction> OnTypedMaterialIndexOverridePrefix(IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase method)
    {
        FieldInfo? field = typeof(LevelObject).GetField("materialIndexOverride", BindingFlags.NonPublic | BindingFlags.Instance);
        if (field == null || !typeof(int).IsAssignableFrom(field.FieldType))
        {
            Logger.DevkitServer.LogWarning(Source, $"Field not found: LevelObject.materialIndexOverride in method {method.Format()}. Material index overrides will not replicate.");
            return instructions;
        }

        List<CodeInstruction> ins = [..instructions];
        bool patchedInner = false;
        bool patchedOuter = false;
        LocalBuilder lcl = generator.DeclareLocal(typeof(int));
        int indexRangeSt = -1;
        for (int i = 2; i < ins.Count; i++)
        {
            if (!patchedInner && ins[i].StoresField(field))
            {
                indexRangeSt = i;
                ins.Insert(i, ins[i - 2].CopyWithoutSpecial());
                ins.Insert(i + 1, new CodeInstruction(OpCodes.Ldfld, field));
                ins.Insert(i + 2, PatchUtil.GetLocalCodeInstruction(lcl, -1, true, false));
                ins.Insert(i + 4, ins[i - 2].CopyWithoutSpecial());
                ins.Insert(i + 5, ins[i - 1].CopyWithoutSpecial());
                ins.Insert(i + 6, PatchUtil.GetLocalCodeInstruction(lcl, -1, false, false));
                ins.Insert(i + 7, new CodeInstruction(OpCodes.Call, new Action<LevelObject, int, int>(OnMaterialIndexOverrideUpdated).Method));
                i += 7;
                patchedInner = true;
                Logger.DevkitServer.LogDebug(Source, $"Patched {method.Format()}.");
            }
            else if (patchedInner && !patchedOuter && ins[i].opcode == OpCodes.Endfinally)
            {
                ins.Insert(i, new CodeInstruction(OpCodes.Ldarg_1));
                ins.Insert(i + 1, new CodeInstruction(OpCodes.Call, new Action<int>(EndMaterialIndexOverrideChangeGroup).Method));
                i += 2;
                patchedOuter = true;
            }
        }
        if (patchedInner && !patchedOuter)
        {
            ins.RemoveRange(indexRangeSt + 4, 4);
            ins.RemoveRange(indexRangeSt, 3);
            Logger.DevkitServer.LogWarning(Source, $"Unpatched {method.Format()} to avoid a memory leak because patch was not successful.");
        }
        else if (!patchedInner)
            Logger.DevkitServer.LogWarning(Source, $"Failed to patch {method.Format()} loop. Material index overrides will not replicate.");
        if (!patchedOuter)
            Logger.DevkitServer.LogWarning(Source, $"Failed to patch {method.Format()} finally. Material index overrides will not replicate.");

        return ins;
    }
    private static void OnMaterialIndexOverrideUpdated(LevelObject obj, int @new, int old)
    {
        if (@new == old || !DevkitServerModule.IsEditing)
            return;
        if (!LevelObjectUtil.CheckMovePermission(obj.instanceID))
        {
            EditorMessage.SendNoPermissionMessage(VanillaPermissions.MoveUnownedObjects);
            obj.SetMaterialIndexOverride(old);
            return;
        }

        Logger.DevkitServer.LogDebug(Source, $"Material index updated: {obj.asset.Format()}, {old.Format()} -> {@new.Format()}");
        PendingMaterialUpdates.Add(obj);
    }
    private static void EndMaterialIndexOverrideChangeGroup(int value)
    {
        if (PendingMaterialUpdates.Count == 0 || !DevkitServerModule.IsEditing)
            return;
        List<NetId> netIds = ListPool<NetId>.claim();
        try
        {
            netIds.IncreaseCapacity(PendingMaterialUpdates.Count);
            for (int i = 0; i < PendingMaterialUpdates.Count; i++)
            {
                LevelObject obj = PendingMaterialUpdates[i];
                if (LevelObjectNetIdDatabase.TryGetObjectNetId(obj, out NetId netId))
                {
                    netIds.Add(netId);

                    if (netIds.Count % LevelObjectUtil.MaxUpdateObjectsPacketSize == 0)
                        Flush();
                }
            }

            Flush();

            void Flush()
            {
                if (ClientEvents.ListeningOnUpdateObjectsMaterialIndexOverride)
                    ClientEvents.InvokeOnUpdateObjectsMaterialIndexOverride(new UpdateObjectsMaterialIndexOverrideProperties(netIds.ToArrayFast(), value, CachedTime.DeltaTime));

                netIds.Clear();
            }
        }
        finally
        {
            ListPool<NetId>.release(netIds);
            PendingMaterialUpdates.Clear();
        }
    }
#endif
    [HarmonyPatch(typeof(LevelObjects), nameof(LevelObjects.transformObject))]
    [HarmonyPrefix]
    [UsedImplicitly]
    private static bool LevelObjectsTransformObjectPrefix(Transform select,
        Vector3 toPosition, Quaternion toRotation, Vector3 toScale,
        Vector3 fromPosition, Quaternion fromRotation, Vector3 fromScale,
        out RegionIdentifier __state)
    {
        if (!LevelObjectUtil.TryFindObject(select, out __state) && !LevelObjectUtil.TryFindBuildable(select, out __state))
        {
            __state = RegionIdentifier.Invalid;
            return false;
        }

        return true;
    }

    [HarmonyPatch(typeof(LevelObjects), nameof(LevelObjects.transformObject))]
    [HarmonyPostfix]
    [UsedImplicitly]
    private static void LevelObjectsTransformObjectPostfix(Transform select,
        Vector3 toPosition, Quaternion toRotation, Vector3 toScale,
        Vector3 fromPosition, Quaternion fromRotation, Vector3 fromScale,
        RegionIdentifier __state)
    {
        if (__state.IsInvalid)
            return;
        if (!Regions.tryGetCoordinate(fromPosition, out byte fromX, out byte fromY) || !Regions.tryGetCoordinate(toPosition, out byte toX, out byte toY))
            return;
        NetId netId;
        if (!LevelObjectUtil.TryFindObject(select, out RegionIdentifier toId))
        {
            if (LevelObjectUtil.TryFindBuildable(select, out toId))
            {
                LevelBuildableObject buildable = LevelObjectUtil.GetBuildableUnsafe(toId);
                LevelObjectNetIdDatabase.TryGetBuildableNetId(__state, out netId);
                if (toX != fromX || toY != fromY)
                {
                    Logger.DevkitServer.LogDebug(Source, $"Buildable moved regions: {__state.Format()} -> {toId.Format()}.");
                    LevelObjectUtil.EventOnBuildableRegionUpdated.TryInvoke(buildable, __state, toId);
                }

                LevelObjectUtil.EventOnBuildableMoved.TryInvoke(buildable, fromPosition, fromRotation, fromScale, toPosition, toRotation, toScale);
                if (!netId.IsNull())
                    LevelObjectUtil.SyncIfAuthority(netId);
            }

            return;
        }

        LevelObject obj = LevelObjectUtil.GetObjectUnsafe(toId);
        LevelObjectNetIdDatabase.TryGetObjectNetId(obj, out netId);
        if (toX != fromX || toY != fromY)
        {
            Logger.DevkitServer.LogDebug(Source, $"Object moved regions: {__state.Format()} -> {toId.Format()}.");
            LevelObjectUtil.EventOnLevelObjectRegionUpdated.TryInvoke(obj, __state, toId);
        }

        LevelObjectUtil.EventOnLevelObjectMoved.TryInvoke(obj, fromPosition, fromRotation, fromScale, toPosition, toRotation, toScale);
        if (!netId.IsNull())
            LevelObjectUtil.SyncIfAuthority(netId);
    }
    [HarmonyPatch(typeof(LevelObjects), nameof(LevelObjects.removeBuildable))]
    [HarmonyTranspiler]
    [UsedImplicitly]
    private static IEnumerable<CodeInstruction> LevelObjectsRemoveBuildableTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase method)
    {
        MethodInfo? destroyMethod = typeof(LevelBuildableObject).GetMethod(nameof(LevelBuildableObject.destroy), BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
        if (destroyMethod == null)
        {
            Logger.DevkitServer.LogWarning(Source, $"{method.Format()} - Unable to find method: LevelBuildableObject.destroy.");
            DevkitServerModule.Fault();
        }
        MethodInfo? removeAtMethod = typeof(List<LevelBuildableObject>).GetMethod(nameof(List<LevelBuildableObject>.RemoveAt), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(int) }, null);
        if (removeAtMethod == null)
        {
            Logger.DevkitServer.LogWarning(Source, $"{method.Format()} - Unable to find method: List<LevelBuildableObject>.RemoveAt.");
            DevkitServerModule.Fault();
        }
        List<CodeInstruction> ins = [..instructions];
        TranspileRemoveObject(ins, generator, method, destroyMethod, removeAtMethod, new Action<LevelBuildableObject, byte, byte, int>(BuildableRemovedInvoker).Method);
        return ins;
    }
    [HarmonyPatch(typeof(LevelObjects), nameof(LevelObjects.removeObject))]
    [HarmonyTranspiler]
    [UsedImplicitly]
    private static IEnumerable<CodeInstruction> LevelObjectsRemoveObjectsTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase method)
    {
        MethodInfo? destroyMethod = typeof(LevelObject).GetMethod(nameof(LevelObject.destroy), BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
        if (destroyMethod == null)
        {
            Logger.DevkitServer.LogWarning(Source, $"{method.Format()} - Unable to find method: LevelObject.destroy.");
            DevkitServerModule.Fault();
        }
        MethodInfo? removeAtMethod = typeof(List<LevelObject>).GetMethod(nameof(List<LevelObject>.RemoveAt), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(int) }, null);
        if (removeAtMethod == null)
        {
            Logger.DevkitServer.LogWarning(Source, $"{method.Format()} - Unable to find method: List<LevelObject>.RemoveAt.");
            DevkitServerModule.Fault();
        }
        List<CodeInstruction> ins = [..instructions];
        TranspileRemoveObject(ins, generator, method, destroyMethod, removeAtMethod, new Action<LevelObject, byte, byte, int>(ObjectRemovedInvoker).Method);
        return ins;
    }
    private static void TranspileRemoveObject(List<CodeInstruction> ins, ILGenerator generator, MethodBase method, MethodInfo? destroyMethod, MethodInfo? removeAtMethod, MethodInfo invoker)
    {
        LocalBuilder lcl = generator.DeclareLocal(typeof(LevelBuildableObject));
        bool destroyPatch = false;
        bool removePatch = false;
        for (int i = 0; i < ins.Count; ++i)
        {
            if (destroyMethod != null && ins[i].Calls(destroyMethod))
            {
                ins.Insert(i, new CodeInstruction(OpCodes.Dup));
                ins.Insert(i + 1, PatchUtil.GetLocalCodeInstruction(lcl, -1, true, false));
                i += 2;
                destroyPatch = true;
            }
            else if (destroyPatch && removeAtMethod != null && ins[i].Calls(removeAtMethod))
            {
                int lclIndex = -1, lclX = -1, lclY = -1, ct = -1;
                LocalBuilder? lclIndexB = null, lclXB = null, lclYB = null;
                for (int j = i - 1; j >= 0; --j)
                {
                    int index = PatchUtil.GetLocalIndex(ins[j], false);
                    if (index == -1) continue;
                    switch (++ct)
                    {
                        case 0:
                            lclIndex = index;
                            lclIndexB = ins[j].operand as LocalBuilder;
                            break;
                        case 1:
                            lclY = index;
                            lclYB = ins[j].operand as LocalBuilder;
                            break;
                        case 2:
                            lclX = index;
                            lclXB = ins[j].operand as LocalBuilder;
                            break;
                    }

                    if (ct >= 2)
                        break;
                }
                ins.Insert(i + 1, PatchUtil.GetLocalCodeInstruction(lcl, -1, false, false));
                ins.Insert(i + 2, PatchUtil.GetLocalCodeInstruction(lclXB, lclX, false, false));
                ins.Insert(i + 3, PatchUtil.GetLocalCodeInstruction(lclYB, lclY, false, false));
                ins.Insert(i + 4, PatchUtil.GetLocalCodeInstruction(lclIndexB, lclIndex, false, false));
                ins.Insert(i + 5, new CodeInstruction(invoker.GetCallRuntime(), invoker));
                removePatch = true;
                break;
            }
        }
        if (!removePatch)
        {
            Logger.DevkitServer.LogWarning(Source, $"Unable to patch in an event in {method.Format()}.");
            DevkitServerModule.Fault();
        }
    }
    private static void BuildableRemovedInvoker(LevelBuildableObject buildable, byte x, byte y, int index)
    {
        RegionIdentifier id = new RegionIdentifier(x, y, index);
        LevelObjectNetIdDatabase.TryGetBuildableNetId(buildable, out NetId netId);
        LevelObjectUtil.EventOnBuildableRemoved.TryInvoke(buildable, id);
        Logger.DevkitServer.LogDebug(Source, $"Buildable removed: {id.Format()}.");

        if (!netId.IsNull())
            LevelObjectUtil.SyncIfAuthority(netId);

#if CLIENT
        LevelObjectUtil.EditorObjectSelection.RemoveAll(x => x.transform == null);
        CallCalculateHandleOffsets?.Invoke();
#endif
    }
    private static void ObjectRemovedInvoker(LevelObject levelObject, byte x, byte y, int index)
    {
        RegionIdentifier id = new RegionIdentifier(x, y, index);
        LevelObjectNetIdDatabase.TryGetObjectNetId(levelObject, out NetId netId);
        LevelObjectUtil.EventOnLevelObjectRemoved.TryInvoke(levelObject, id);
        Logger.DevkitServer.LogDebug(Source, $"Object removed: {id.Format()}.");

        if (!netId.IsNull())
            LevelObjectUtil.SyncIfAuthority(netId);

#if CLIENT
        LevelObjectUtil.EditorObjectSelection.RemoveAll(x => x.transform == null);
        CallCalculateHandleOffsets?.Invoke();
#endif
    }
}