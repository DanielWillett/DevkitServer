#if CLIENT
using DevkitServer.API.Permissions;
using DevkitServer.Core.Permissions;
using DevkitServer.Models;
using DevkitServer.Multiplayer.Actions;
using DevkitServer.Players;
using HarmonyLib;
using JetBrains.Annotations;
using SDG.Framework.Devkit;
using SDG.Framework.Devkit.Tools;
using SDG.Framework.Landscapes;
using System.Reflection;
using System.Reflection.Emit;
using DevkitServer.Multiplayer.Sync;
using DevkitServer.API.UI;
using DevkitServer.API;

namespace DevkitServer.Patches;
[HarmonyPatch]
internal static class TerrainEditorPatches
{

    [HarmonyPatch(typeof(TerrainEditor), nameof(TerrainEditor.update))]
    [HarmonyTranspiler]
    [UsedImplicitly]
    private static IEnumerable<CodeInstruction> TerrainEditorUpdateTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        Type te = typeof(TerrainEditor);
        Type tep = typeof(TerrainEditorPatches);
        Type vp = typeof(VanillaPermissions);
        MethodInfo? rampHandler = te.GetMethod("handleHeightmapWriteRamp",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (rampHandler == null)
        {
            Logger.LogWarning("Unable to find method: TerrainEditor.handleHeightmapWriteRamp.", method: "CLIENT EVENTS");
            DevkitServerModule.Fault();
        }

        MethodInfo? adjustHandler = te.GetMethod("handleHeightmapWriteAdjust",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (adjustHandler == null)
        {
            Logger.LogWarning("Unable to find method: TerrainEditor.handleHeightmapWriteAdjust.", method: "CLIENT EVENTS");
            DevkitServerModule.Fault();
        }

        MethodInfo? flattenHandler = te.GetMethod("handleHeightmapWriteFlatten",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (flattenHandler == null)
        {
            Logger.LogWarning("Unable to find method: TerrainEditor.handleHeightmapWriteFlatten.", method: "CLIENT EVENTS");
            DevkitServerModule.Fault();
        }

        MethodInfo? smoothHandler = te.GetMethod("handleHeightmapWriteSmooth",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (smoothHandler == null)
        {
            Logger.LogWarning("Unable to find method: TerrainEditor.handleHeightmapWriteSmooth.", method: "CLIENT EVENTS");
            DevkitServerModule.Fault();
        }

        MethodInfo? paintHandler = te.GetMethod("handleSplatmapWritePaint",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (paintHandler == null)
        {
            Logger.LogWarning("Unable to find method: TerrainEditor.handleSplatmapWritePaint.", method: "CLIENT EVENTS");
            DevkitServerModule.Fault();
        }

        MethodInfo? autoPaintHandler = te.GetMethod("handleSplatmapWriteAuto",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (autoPaintHandler == null)
        {
            Logger.LogWarning("Unable to find method: TerrainEditor.handleSplatmapWriteAuto.", method: "CLIENT EVENTS");
            DevkitServerModule.Fault();
        }

        MethodInfo? paintSmoothHandler = te.GetMethod("handleSplatmapWriteSmooth",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (paintSmoothHandler == null)
        {
            Logger.LogWarning("Unable to find method: TerrainEditor.handleSplatmapWriteSmooth.", method: "CLIENT EVENTS");
            DevkitServerModule.Fault();
        }

        MethodInfo? holesHandler = te.GetMethod("handleSplatmapWriteCut",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (holesHandler == null)
        {
            Logger.LogWarning("Unable to find method: TerrainEditor.handleSplatmapWriteCut.", method: "CLIENT EVENTS");
            DevkitServerModule.Fault();
        }

        MethodInfo? addTile = typeof(Landscape).GetMethod(nameof(Landscape.addTile),
            BindingFlags.Public | BindingFlags.Static);
        if (addTile == null)
        {
            Logger.LogWarning("Unable to find method: Landscape.addTile.", method: "CLIENT EVENTS");
            DevkitServerModule.Fault();
        }

        MethodInfo? removeTile = typeof(Landscape).GetMethod(nameof(Landscape.removeTile),
            BindingFlags.Public | BindingFlags.Static);
        if (removeTile == null)
        {
            Logger.LogWarning("Unable to find method: Landscape.removeTile.", method: "CLIENT EVENTS");
            DevkitServerModule.Fault();
        }

        MethodInfo? lHMarkDirty = typeof(LevelHierarchy).GetMethod(nameof(LevelHierarchy.MarkDirty),
            BindingFlags.Public | BindingFlags.Static);
        if (lHMarkDirty == null)
        {
            Logger.LogWarning("Unable to find method: LevelHierarchy.MarkDirty.", method: "CLIENT EVENTS");
            DevkitServerModule.Fault();
        }

        MethodInfo? writeHeightmap = typeof(Landscape).GetMethod(nameof(Landscape.writeHeightmap),
            BindingFlags.Public | BindingFlags.Static);
        if (writeHeightmap == null)
        {
            Logger.LogWarning("Unable to find method: Landscape.writeHeightmap.", method: "CLIENT EVENTS");
            DevkitServerModule.Fault();
        }

        MethodInfo? writeSplatmap = typeof(Landscape).GetMethod(nameof(Landscape.writeSplatmap),
            BindingFlags.Public | BindingFlags.Static);
        if (writeSplatmap == null)
        {
            Logger.LogWarning("Unable to find method: Landscape.writeSplatmap.", method: "CLIENT EVENTS");
            DevkitServerModule.Fault();
        }

        MethodInfo? writeHoles = typeof(Landscape).GetMethod(nameof(Landscape.writeHoles),
            BindingFlags.Public | BindingFlags.Static);
        if (writeHoles == null)
        {
            Logger.LogWarning("Unable to find method: Landscape.writeHoles.", method: "CLIENT EVENTS");
            DevkitServerModule.Fault();
        }
        MethodInfo? rampInvoker = tep.GetMethod(nameof(OnRampConfirm), BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo? adjustInvoker = tep.GetMethod(nameof(OnAdjustConfirm), BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo? flattenInvoker = tep.GetMethod(nameof(OnFlattenConfirm), BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo? smoothInvoker = tep.GetMethod(nameof(OnSmoothConfirm), BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo? paintInvoker = tep.GetMethod(nameof(OnPaintConfirm), BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo? autoPaintInvoker = tep.GetMethod(nameof(OnAutoPaintConfirm), BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo? paintSmoothInvoker = tep.GetMethod(nameof(OnPaintSmoothConfirm), BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo? holesInvoker = tep.GetMethod(nameof(OnHoleConfirm), BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo? addTileInvoker = tep.GetMethod(nameof(OnAddTile), BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo? removeTileInvoker = tep.GetMethod(nameof(OnRemoveTile), BindingFlags.Static | BindingFlags.NonPublic);
        if (rampInvoker == null || adjustInvoker == null ||
            flattenInvoker == null || smoothInvoker == null ||
            paintInvoker == null || autoPaintInvoker == null ||
            paintSmoothInvoker == null || holesInvoker == null ||
            addTileInvoker == null || removeTileInvoker == null)
        {
            Logger.LogWarning("Unable to find one or more of the TerrainEditor patch trigger methods.");
            DevkitServerModule.Fault();
        }
        FieldInfo? permissionWriteHeightmap = vp.GetField(nameof(VanillaPermissions.EditHeightmap));
        FieldInfo? permissionWriteSplatmap = vp.GetField(nameof(VanillaPermissions.EditSplatmap));
        FieldInfo? permissionWriteHoles = vp.GetField(nameof(VanillaPermissions.EditHoles));
        FieldInfo? permissionAll = vp.GetField(nameof(VanillaPermissions.EditTerrain));
        if (permissionWriteHeightmap == null || permissionWriteSplatmap == null || permissionWriteHoles == null)
        {
            Logger.LogWarning("Unable to find one or more of the VanillaPermissions fields.", method: "CLIENT EVENTS");
            DevkitServerModule.Fault();
        }
        if (permissionAll == null)
        {
            Logger.LogWarning("Unable to find field: VanillaPermissions.EditTerrain.", method: "CLIENT EVENTS");
            DevkitServerModule.Fault();
        }

        MethodInfo? hasPermission = typeof(PermissionsEx).GetMethod(nameof(PermissionsEx.Has),
            BindingFlags.Static | BindingFlags.Public, null, new Type[] { typeof(Permission), typeof(bool) }, null);
        if (hasPermission == null)
        {
            Logger.LogWarning("Unable to find method: PermissionsEx.Has.", method: "CLIENT EVENTS");
            DevkitServerModule.Fault();
        }

        MethodInfo? onNoPermissionInvoker = tep.GetMethod(nameof(OnNoPermissionsInvoker), BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo? onPermissionInvoker = tep.GetMethod(nameof(OnPermissionsInvoker), BindingFlags.Static | BindingFlags.NonPublic);
        if (hasPermission == null)
            Logger.LogWarning("Unable to find method: ClientEvents.OnNoPermissionsInvoker.", method: "CLIENT EVENTS");
        LocalBuilder localBounds = generator.DeclareLocal(typeof(Bounds));
        List<CodeInstruction> ins = new List<CodeInstruction>(instructions);
        int addTileCt = 0;
        int addTileLcl = -1;
        LocalBuilder? addTileLcl2 = null;
        int pCt = 0;
        bool pAddTile = false, pRemoveTile = false;
        Label stLbl = generator.DefineLabel();
        int i = 0;
        PatchUtil.InsertActionRateLimiter(ref i, stLbl, ins);
        for (; i < ins.Count; ++i)
        {
            CodeInstruction c = ins[i];
            if (i == 0)
                c.labels.Add(stLbl);
            CodeInstruction? n = i == ins.Count - 1 ? null : ins[i + 1];
            // look for pattern: ldftn (load function ptr for handler), newobj (create delegate), call
            if (n != null && n.opcode == OpCodes.Ldftn && n.operand is MethodInfo method)
            {
                if (ins.Count > i + 3 && ins[i + 2].opcode == OpCodes.Newobj && writeHeightmap != null && (ins[i + 3].Calls(writeHeightmap) || ins[i + 3].Calls(writeSplatmap) || ins[i + 3].Calls(writeHoles)))
                {
                    FieldInfo? permission;
                    if (ins[i + 3].Calls(writeHeightmap))
                        permission = permissionWriteHeightmap;
                    else if (ins[i + 3].Calls(writeSplatmap))
                        permission = permissionWriteHoles;
                    else
                        permission = permissionWriteHoles;

                    Label? lbl = null;
                    if ((permission != null || permissionAll != null) && hasPermission != null)
                    {
                        lbl = generator.DefineLabel();
                        yield return new CodeInstruction(OpCodes.Call, Accessor.IsDevkitServerGetter);
                        yield return new CodeInstruction(OpCodes.Brfalse, lbl.Value);
                        if (permission != null)
                        {
                            yield return new CodeInstruction(OpCodes.Ldsfld, permission);
                            yield return new CodeInstruction(OpCodes.Ldc_I4_1);
                            yield return new CodeInstruction(OpCodes.Call, hasPermission);
                            yield return new CodeInstruction(OpCodes.Brtrue, lbl.Value);
                        }
                        if (permissionAll != null)
                        {
                            yield return new CodeInstruction(OpCodes.Ldsfld, permissionAll);
                            yield return new CodeInstruction(permission != null ? OpCodes.Ldc_I4_0 : OpCodes.Ldc_I4_1);
                            yield return new CodeInstruction(OpCodes.Call, hasPermission);
                            yield return new CodeInstruction(OpCodes.Brtrue, lbl.Value);
                        }
                        if (onNoPermissionInvoker != null)
                        {
                            yield return new CodeInstruction(OpCodes.Call, onNoPermissionInvoker);
                            yield return new CodeInstruction(OpCodes.Brtrue, lbl.Value);
                        }

                        yield return new CodeInstruction(OpCodes.Pop);
                        yield return new CodeInstruction(OpCodes.Ret);
                    }
                    if (onPermissionInvoker != null)
                    {
                        CodeInstruction c3 = new CodeInstruction(OpCodes.Call, onPermissionInvoker);
                        if (lbl.HasValue)
                            c3.labels.Add(lbl.Value);
                        lbl = generator.DefineLabel();
                        yield return c3;
                        yield return new CodeInstruction(OpCodes.Brtrue, lbl.Value);

                        yield return new CodeInstruction(OpCodes.Pop);
                        yield return new CodeInstruction(OpCodes.Ret);
                    }

                    CodeInstruction c2 = new CodeInstruction(OpCodes.Dup);
                    if (lbl.HasValue)
                        c2.labels.Add(lbl.Value);
                    yield return c2;
                    yield return PatchUtil.GetLocalCodeInstruction(localBounds, localBounds.LocalIndex, true);
                    yield return c;
                    yield return n;
                    yield return ins[i + 2];
                    yield return ins[i + 3];
                    yield return PatchUtil.GetLocalCodeInstruction(localBounds, localBounds.LocalIndex, false);
                    MethodInfo? invoker = null;
                    // heightmap
                    if (method == rampHandler)
                        invoker = rampInvoker;
                    else if (method == adjustHandler)
                        invoker = adjustInvoker;
                    else if (method == flattenHandler)
                        invoker = flattenInvoker;
                    else if (method == smoothHandler)
                        invoker = smoothInvoker;

                    // splatmap
                    else if (method == paintHandler)
                        invoker = paintInvoker;
                    else if (method == autoPaintHandler)
                        invoker = autoPaintInvoker;
                    else if (method == paintSmoothHandler)
                        invoker = paintSmoothInvoker;
                    else if (method == holesHandler)
                        invoker = holesInvoker;

                    if (invoker != null)
                    {
                        yield return new CodeInstruction(OpCodes.Call, invoker);
                        Logger.LogDebug("[CLIENT EVENTS] Patched in " + invoker.Format() + " call.");
                        ++pCt;
                    }
                    else
                    {
                        yield return new CodeInstruction(OpCodes.Pop);
                        Logger.LogWarning("Unknown function pointer-based method call: " + method.Format() + ".", method: "CLIENT EVENTS");
                    }

                    i += 3;
                    continue;
                }
            }
            // look for where a tile is added and start waiting until the landscape hierarchy is invalidated
            else if (addTileCt == 0 && addTile != null && c.Calls(addTile) && n != null)
            {
                addTileCt = lHMarkDirty == null ? 2 : 1;
                addTileLcl2 = PatchUtil.GetLocal(n, out addTileLcl, true);
                if (addTileLcl2 == null)
                    addTileCt = 2;
                if (addTileCt == 2)
                {
                    Logger.LogWarning("Unable to patch OnAddTile event.");
                }
            }
            // landscape hierarchy is invalidated
            else if (addTileCt == 1 && c.Calls(lHMarkDirty!))
            {
                yield return c;
                yield return PatchUtil.GetLocalCodeInstruction(addTileLcl2, addTileLcl, false);
                yield return new CodeInstruction(OpCodes.Call, addTileInvoker);
                Logger.LogDebug("[CLIENT EVENTS] Patched in OnAddTile call.");
                addTileCt = 2;
                pAddTile = true;
                continue;
            }
            // on removed tile
            else if (removeTile != null && c.Calls(removeTile))
            {
                yield return c;
                yield return new CodeInstruction(OpCodes.Call, removeTileInvoker);
                Logger.LogDebug("[CLIENT EVENTS] Patched in OnRemoveTile call.");
                pRemoveTile = true;
                continue;
            }

            yield return c;
        }
        if (!pRemoveTile || !pAddTile || pCt != 8)
        {
            Logger.LogWarning($"Patching error for TerrainEditor.update. Invalid transpiler operation: Remove Tile: {pRemoveTile}, Add Tile: {pAddTile}, invoker counts: {pCt} / 8.", method: "CLIENT EVENTS");
            DevkitServerModule.Fault();
        }
    }

    internal static readonly InstanceGetter<TerrainEditor, Vector3>? GetRampStart =
        Accessor.GenerateInstanceGetter<TerrainEditor, Vector3>("heightmapRampBeginPosition");

    internal static readonly InstanceGetter<TerrainEditor, Vector3>? GetRampEnd =
        Accessor.GenerateInstanceGetter<TerrainEditor, Vector3>("heightmapRampEndPosition");

    internal static readonly InstanceGetter<TerrainEditor, Vector3>? GetTerrainBrushWorldPosition =
        Accessor.GenerateInstanceGetter<TerrainEditor, Vector3>("brushWorldPosition");

    internal static readonly InstanceGetter<TerrainEditor, int>? GetSampleCount =
        Accessor.GenerateInstanceGetter<TerrainEditor, int>("splatmapSmoothSampleCount");

    internal static readonly InstanceGetter<TerrainEditor, Dictionary<AssetReference<LandscapeMaterialAsset>, float>>? GetSampleAverage =
        Accessor.GenerateInstanceGetter<TerrainEditor, Dictionary<AssetReference<LandscapeMaterialAsset>, float>>("splatmapSmoothSampleAverage");

    internal static readonly InstanceGetter<TerrainEditor, float>? GetHeightmapSmoothTarget =
        Accessor.GenerateInstanceGetter<TerrainEditor, float>("heightmapSmoothTarget");
    [UsedImplicitly]
    private static bool OnNoPermissionsInvoker()
    {
        bool allow = false;
        if (TerrainEditor.toolMode == TerrainEditor.EDevkitLandscapeToolMode.HEIGHTMAP)
        {
            if (!ClientEvents.ListeningOnEditHeightmapPermissionDenied)
                goto rtn;
            ClientEvents.EventOnEditHeightmapPermissionDenied.TryInvoke(TerrainEditor.heightmapMode, ref allow);
        }
        else if (TerrainEditor.toolMode == TerrainEditor.EDevkitLandscapeToolMode.SPLATMAP)
        {
            if (TerrainEditor.splatmapMode != TerrainEditor.EDevkitLandscapeToolSplatmapMode.CUT)
            {
                if (!ClientEvents.ListeningOnEditSplatmapPermissionDenied)
                    goto rtn;
                ClientEvents.EventOnEditSplatmapPermissionDenied.TryInvoke(TerrainEditor.splatmapMode, ref allow);
            }
            else
            {
                if (!ClientEvents.ListeningOnEditHolesPermissionDenied)
                    goto rtn;
                ClientEvents.EventOnEditHolesPermissionDenied.TryInvoke(InputEx.GetKey(KeyCode.LeftShift), ref allow);
            }
        }
        rtn:
        if (!allow)
            EditorMessage.SendNoPermissionMessage(null);
        return allow;
        
    }
    [UsedImplicitly]
    private static bool OnPermissionsInvoker()
    {
        bool allow = true;
        if (EditorActions.IsPlayingCatchUp)
        {
            EditorMessage.SendEditorMessage(DevkitServerModule.MessageLocalization.Translate("Syncing"));
            return false;
        }
        if (UserInput.ActiveTool is TerrainEditor editor && GetTerrainBrushWorldPosition != null)
        {
            TileSync? sync = TileSync.GetAuthority();
            TileSync.MapInvalidation? pendingSync = sync?.Pending;
            if (sync != null && pendingSync.HasValue)
            {
                float rad = TerrainEditor.toolMode switch
                {
                    TerrainEditor.EDevkitLandscapeToolMode.HEIGHTMAP => editor.heightmapBrushRadius,
                    TerrainEditor.EDevkitLandscapeToolMode.SPLATMAP => editor.splatmapBrushRadius,
                    _ => 0
                };
                if (pendingSync.Value.Type == TileSync.DataType.Heightmap && TerrainEditor.toolMode == TerrainEditor.EDevkitLandscapeToolMode.HEIGHTMAP ||
                    pendingSync.Value.Type == TileSync.DataType.Splatmap && TerrainEditor.toolMode == TerrainEditor.EDevkitLandscapeToolMode.SPLATMAP && TerrainEditor.splatmapMode != TerrainEditor.EDevkitLandscapeToolSplatmapMode.CUT ||
                    pendingSync.Value.Type == TileSync.DataType.Holes && TerrainEditor.toolMode == TerrainEditor.EDevkitLandscapeToolMode.SPLATMAP && TerrainEditor.splatmapMode == TerrainEditor.EDevkitLandscapeToolSplatmapMode.CUT)
                {
                    if (pendingSync.Value.CollidesWith2DCircle(GetTerrainBrushWorldPosition(editor), rad))
                    {
                        EditorMessage.SendEditorMessage(DevkitServerModule.MessageLocalization.Translate("Syncing"));
                        return false;
                    }
                }
            }
        }
        if (TerrainEditor.toolMode == TerrainEditor.EDevkitLandscapeToolMode.HEIGHTMAP)
        {
            if (!ClientEvents.ListeningOnEditHeightmapRequested) return true;
            ClientEvents.EventOnEditHeightmapRequested.TryInvoke(TerrainEditor.heightmapMode, ref allow);
            if (!allow)
                return allow;
        }
        else if (TerrainEditor.toolMode == TerrainEditor.EDevkitLandscapeToolMode.SPLATMAP)
        {
            if (TerrainEditor.splatmapMode != TerrainEditor.EDevkitLandscapeToolSplatmapMode.CUT)
            {
                if (!ClientEvents.ListeningOnEditSplatmapRequested) return true;
                ClientEvents.EventOnEditSplatmapRequested.TryInvoke(TerrainEditor.splatmapMode, ref allow);
                if (!allow)
                    return allow;
            }
            else
            {
                if (!ClientEvents.ListeningOnEditHolesRequested) return true;
                ClientEvents.EventOnEditHolesRequested.TryInvoke(InputEx.GetKey(KeyCode.LeftShift), ref allow);
                if (!allow)
                    return allow;
            }
        }

        return allow;
    }

    [UsedImplicitly]
    private static void OnRampConfirm(Bounds bounds)
    {
        if (!DevkitServerModule.IsEditing || UserInput.ActiveTool is not TerrainEditor editor || GetRampStart == null || GetRampEnd == null) return;

        ClientEvents.InvokeOnPaintRamp(new PaintRampProperties(bounds, GetRampStart(editor), GetRampEnd(editor), editor.heightmapBrushRadius, editor.heightmapBrushFalloff, CachedTime.DeltaTime));
    }
    [UsedImplicitly]
    private static void OnAdjustConfirm(Bounds bounds)
    {
        if (!DevkitServerModule.IsEditing || UserInput.ActiveTool is not TerrainEditor editor || GetTerrainBrushWorldPosition == null) return;

        ClientEvents.InvokeOnAdjustHeightmap(new AdjustHeightmapProperties(bounds, GetTerrainBrushWorldPosition(editor),
            editor.heightmapBrushRadius, editor.heightmapBrushFalloff,
            editor.heightmapBrushStrength, editor.heightmapAdjustSensitivity,
            CachedTime.DeltaTime, InputEx.GetKey(KeyCode.LeftShift)));
    }
    [UsedImplicitly]
    private static void OnFlattenConfirm(Bounds bounds)
    {
        if (!DevkitServerModule.IsEditing || UserInput.ActiveTool is not TerrainEditor editor || GetTerrainBrushWorldPosition == null) return;

        ClientEvents.InvokeOnFlattenHeightmap(new FlattenHeightmapProperties(bounds, GetTerrainBrushWorldPosition(editor), DevkitLandscapeToolHeightmapOptions.instance.flattenMethod,
            editor.heightmapBrushRadius, editor.heightmapBrushFalloff,
            editor.heightmapBrushStrength, editor.heightmapFlattenSensitivity,
            editor.heightmapFlattenTarget, CachedTime.DeltaTime));
    }
    [UsedImplicitly]
    private static void OnSmoothConfirm(Bounds bounds)
    {
        if (!DevkitServerModule.IsEditing || UserInput.ActiveTool is not TerrainEditor editor || GetTerrainBrushWorldPosition == null || GetHeightmapSmoothTarget == null) return;

        ClientEvents.InvokeOnSmoothHeightmap(new SmoothHeightmapProperties(bounds, GetTerrainBrushWorldPosition(editor), DevkitLandscapeToolHeightmapOptions.instance.smoothMethod,
            editor.heightmapBrushRadius, editor.heightmapBrushFalloff,
            editor.heightmapBrushStrength, GetHeightmapSmoothTarget(editor), CachedTime.DeltaTime));
    }
    [UsedImplicitly]
    private static void OnPaintConfirm(Bounds bounds)
    {
        if (!DevkitServerModule.IsEditing || UserInput.ActiveTool is not TerrainEditor editor || GetTerrainBrushWorldPosition == null) return;

        DevkitLandscapeToolSplatmapOptions settings = DevkitLandscapeToolSplatmapOptions.instance;

        ClientEvents.InvokeOnPaintSplatmap(new PaintSplatmapProperties(bounds, GetTerrainBrushWorldPosition(editor), editor.splatmapBrushRadius,
            editor.splatmapBrushFalloff, editor.splatmapBrushStrength, editor.splatmapPaintSensitivity,
            editor.splatmapWeightTarget, CachedTime.DeltaTime, InputEx.GetKey(KeyCode.LeftControl) || editor.splatmapUseWeightTarget,
            settings.useAutoSlope, settings.useAutoFoundation,
            InputEx.GetKey(KeyCode.LeftShift),
            new AutoSlopeProperties(settings.autoMinAngleBegin, settings.autoMinAngleEnd, settings.autoMaxAngleBegin, settings.autoMaxAngleEnd),
            new AutoFoundationProperties(settings.autoRayRadius, settings.autoRayLength, settings.autoRayMask),
            TerrainEditor.splatmapMaterialTarget));
    }
    [UsedImplicitly]
    private static void OnAutoPaintConfirm(Bounds bounds)
    {
        if (!DevkitServerModule.IsEditing || UserInput.ActiveTool is not TerrainEditor editor || GetTerrainBrushWorldPosition == null) return;

        DevkitLandscapeToolSplatmapOptions settings = DevkitLandscapeToolSplatmapOptions.instance;

        ClientEvents.InvokeOnAutoPaintSplatmap(new PaintSplatmapProperties(bounds, GetTerrainBrushWorldPosition(editor), editor.splatmapBrushRadius,
            editor.splatmapBrushFalloff, editor.splatmapBrushStrength, editor.splatmapPaintSensitivity,
            editor.splatmapWeightTarget, CachedTime.DeltaTime, InputEx.GetKey(KeyCode.LeftControl) || editor.splatmapUseWeightTarget,
            settings.useAutoSlope, settings.useAutoFoundation,
            InputEx.GetKey(KeyCode.LeftShift),
            new AutoSlopeProperties(settings.autoMinAngleBegin, settings.autoMinAngleEnd, settings.autoMaxAngleBegin, settings.autoMaxAngleEnd),
            new AutoFoundationProperties(settings.autoRayRadius, settings.autoRayLength, settings.autoRayMask),
            TerrainEditor.splatmapMaterialTarget));
    }

    [UsedImplicitly]
    private static void OnPaintSmoothConfirm(Bounds bounds)
    {
        if (!DevkitServerModule.IsEditing || UserInput.ActiveTool is not TerrainEditor editor || GetTerrainBrushWorldPosition == null) return;

        ClientEvents.InvokeOnSmoothSplatmap(new SmoothSplatmapProperties(bounds, GetTerrainBrushWorldPosition(editor), DevkitLandscapeToolSplatmapOptions.instance.smoothMethod, editor.splatmapBrushRadius,
            editor.splatmapBrushFalloff, editor.splatmapBrushStrength, CachedTime.DeltaTime));
    }
    [UsedImplicitly]
    private static void OnHoleConfirm(Bounds bounds)
    {
        if (!DevkitServerModule.IsEditing || UserInput.ActiveTool is not TerrainEditor editor || GetTerrainBrushWorldPosition == null) return;

        ClientEvents.InvokeOnPaintHoles(new PaintHolesProperties(bounds, GetTerrainBrushWorldPosition(editor), editor.splatmapBrushRadius, InputEx.GetKey(KeyCode.LeftShift), CachedTime.DeltaTime));
    }
    [UsedImplicitly]
    private static void OnAddTile(LandscapeTile tile)
    {
        if (!DevkitServerModule.IsEditing) return;

        Logger.LogDebug("[CLIENT EVENTS] Tile added: " + tile.coord.Format() + ".");
        ClientEvents.InvokeOnAddTile(new UpdateLandscapeTileProperties(tile, CachedTime.DeltaTime));
    }
    [UsedImplicitly]
    private static void OnRemoveTile()
    {
        if (!DevkitServerModule.IsEditing) return;

        LandscapeTile? tile = TerrainEditor.selectedTile;
        if (tile == null) return;
        ClientEvents.InvokeOnDeleteTile(new UpdateLandscapeTileProperties(tile, CachedTime.DeltaTime));
    }
}
#endif