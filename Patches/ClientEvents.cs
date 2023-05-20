#if CLIENT
using DevkitServer.API.Permissions;
using DevkitServer.Core.Permissions;
using DevkitServer.Multiplayer;
using DevkitServer.Players;
using DevkitServer.Players.UI;
using HarmonyLib;
using JetBrains.Annotations;
using SDG.Framework.Devkit;
using SDG.Framework.Devkit.Tools;
using SDG.Framework.Devkit.Transactions;
using SDG.Framework.Foliage;
using SDG.Framework.Landscapes;
using SDG.Framework.Utilities;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using DevkitServer.Multiplayer.Actions;
using DevkitServer.Util.Encoding;
using Action = System.Action;

namespace DevkitServer.Patches;

public delegate void PaintHoleAction(Bounds bounds, Vector3 position, float radius, bool isFilling);
public delegate void LandscapeTileAction(LandscapeTile bounds);
public delegate void AddFoliageAction(FoliageInfoAsset asset, Vector3 position, Quaternion rotation, Vector3 scale, bool clearWhenBaked);
public delegate void RemoveFoliageAction(Vector3 brushPosition, FoliageTile foliageTile, FoliageInstanceList list, float sqrBrushRadius, float sqrBrushFalloffRadius, bool allowRemoveBaked, int sampleCount);
public delegate void ResourceSpawnpointRemovedAction(ResourceSpawnpoint spawnpoint);
public delegate void LevelObjectRemovedAction(Vector3 pos, LevelObject obj);
public delegate void RampAction(Bounds bounds, Vector3 start, Vector3 end, float radius, float falloff);
public delegate void AdjustAction(Bounds bounds, Vector3 position, float radius, float falloff, float strength, float sensitivity, bool subtracting, float dt);
public delegate void FlattenAction(Bounds bounds, Vector3 position, float radius, float falloff, float strength, float sensitivity, float target, EDevkitLandscapeToolHeightmapFlattenMethod method, float dt);
public delegate void SmoothAction(Bounds bounds, Vector3 position, float radius, float falloff, float strength, float target, EDevkitLandscapeToolHeightmapSmoothMethod method, float dt);
public delegate void PaintAction(Bounds bounds, Vector3 position, float radius, float falloff, float strength, float sensitivity, float target, bool useWeightTarget, bool autoSlope, bool autoFoundation, float autoMinAngleBegin, float autoMinAngleEnd, float autoMaxAngleBegin, float autoMaxAngleEnd, float autoRayLength, float autoRayRadius, ERayMask autoRayMask, bool isRemove, AssetReference<LandscapeMaterialAsset> selectedMaterial, float dt);
public delegate void PaintSmoothAction(Bounds bounds, Vector3 position, float radius, float falloff, float strength, EDevkitLandscapeToolSplatmapSmoothMethod method, float dt);
public delegate void SplatmapLayerMaterialsUpdateAction(LandscapeTile tile);
public delegate void EditHeightmapRequest(TerrainEditor.EDevkitLandscapeToolHeightmapMode mode, ref bool allow);
public delegate void EditSplatmapRequest(TerrainEditor.EDevkitLandscapeToolSplatmapMode mode, ref bool allow);
public delegate void EditHolesRequest(bool isFilling, ref bool allow);

[HarmonyPatch]
[EarlyTypeInit]
public static class ClientEvents
{
    public static event RampAction? OnRampComplete;
    public static event AdjustAction? OnAdjusted;
    public static event FlattenAction? OnFlattened;
    public static event SmoothAction? OnSmoothed;
    public static event PaintAction? OnPainted;
    public static event PaintAction? OnAutoPainted;
    public static event PaintSmoothAction? OnPaintSmoothed;
    public static event PaintHoleAction? OnHolePainted;
    public static event LandscapeTileAction? OnTileAdded;
    public static event LandscapeTileAction? OnTileDeleted;
    public static event AddFoliageAction? OnFoliageAdded;
    public static event RemoveFoliageAction? OnFoliageRemoved;
    public static event ResourceSpawnpointRemovedAction? OnResourceSpawnpointRemoved;
    public static event LevelObjectRemovedAction? OnLevelObjectRemoved;
    public static event SplatmapLayerMaterialsUpdateAction? OnSplatmapLayerMaterialsUpdate;
    public static event EditHeightmapRequest? OnEditHeightmapPermissionDenied;
    public static event EditSplatmapRequest? OnEditSplatmapPermissionDenied;
    public static event EditHolesRequest? OnEditHolesPermissionDenied;
    public static event EditHeightmapRequest? OnEditHeightmapRequested;
    public static event EditSplatmapRequest? OnEditSplatmapRequested;
    public static event EditHolesRequest? OnEditHolesRequested;
    public static readonly Type FoliageEditor = typeof(Provider).Assembly.GetType("SDG.Unturned.FoliageEditor");

    #region TerrainEditor.update
    [HarmonyPatch(typeof(TerrainEditor), nameof(TerrainEditor.update))]
    [HarmonyTranspiler]
    [UsedImplicitly]
    private static IEnumerable<CodeInstruction> TerrainEditorUpdateTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        Type te = typeof(TerrainEditor);
        Type ce = typeof(ClientEvents);
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
        
        MethodInfo? rampInvoker = ce.GetMethod(nameof(OnRampConfirm), BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo? adjustInvoker = ce.GetMethod(nameof(OnAdjustConfirm), BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo? flattenInvoker = ce.GetMethod(nameof(OnFlattenConfirm), BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo? smoothInvoker = ce.GetMethod(nameof(OnSmoothConfirm), BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo? paintInvoker = ce.GetMethod(nameof(OnPaintConfirm), BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo? autoPaintInvoker = ce.GetMethod(nameof(OnAutoPaintConfirm), BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo? paintSmoothInvoker = ce.GetMethod(nameof(OnPaintSmoothConfirm), BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo? holesInvoker = ce.GetMethod(nameof(OnHoleConfirm), BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo? addTileInvoker = ce.GetMethod(nameof(OnAddTile), BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo? removeTileInvoker = ce.GetMethod(nameof(OnRemoveTile), BindingFlags.Static | BindingFlags.NonPublic);
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

        MethodInfo? onNoPermissionInvoker = ce.GetMethod(nameof(OnNoPermissionsInvoker), BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo? onPermissionInvoker = ce.GetMethod(nameof(OnPermissionsInvoker), BindingFlags.Static | BindingFlags.NonPublic);
        if (hasPermission == null)
            Logger.LogWarning("Unable to find method: ClientEvents.OnNoPermissionsInvoker.", method: "CLIENT EVENTS");
        LocalBuilder localBounds = generator.DeclareLocal(typeof(Bounds));
        List<CodeInstruction> ins = new List<CodeInstruction>(instructions);
        int addTileCt = 0;
        int addTileLcl = -1;
        LocalBuilder? addTileLcl2 = null;
        int pCt = 0;
        bool pAddTile = false, pRemoveTile = false;
        for (int i = 0; i < ins.Count; ++i)
        {
            CodeInstruction c = ins[i];
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
            if (OnEditHeightmapPermissionDenied == null)
                goto rtn;
            foreach (EditHeightmapRequest denied in OnEditHeightmapPermissionDenied.GetInvocationList().Cast<EditHeightmapRequest>())
            {
                try
                {
                    denied(TerrainEditor.heightmapMode, ref allow);
                }
                catch (Exception ex)
                {
                    Logger.LogError("Plugin threw an error in " + typeof(ClientEvents).Format() + "." + nameof(OnEditHeightmapPermissionDenied) + ".", method: "CLIENT EVENTS");
                    Logger.LogError(ex, method: "CLIENT EVENTS");
                    allow = false;
                }

                if (allow)
                    goto rtn;
            }
        }
        else if (TerrainEditor.toolMode == TerrainEditor.EDevkitLandscapeToolMode.SPLATMAP)
        {
            if (TerrainEditor.splatmapMode != TerrainEditor.EDevkitLandscapeToolSplatmapMode.CUT)
            {
                if (OnEditSplatmapPermissionDenied == null)
                    goto rtn;
                foreach (EditSplatmapRequest denied in OnEditSplatmapPermissionDenied.GetInvocationList().Cast<EditSplatmapRequest>())
                {
                    try
                    {
                        denied(TerrainEditor.splatmapMode, ref allow);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Plugin threw an error in " + typeof(ClientEvents).Format() + "." + nameof(OnEditSplatmapPermissionDenied) + ".", method: "CLIENT EVENTS");
                        Logger.LogError(ex, method: "CLIENT EVENTS");
                        allow = false;
                    }

                    if (allow)
                        goto rtn;
                }
            }
            else
            {
                if (OnEditHolesPermissionDenied == null)
                    goto rtn;
                bool isFilling = InputEx.GetKey(KeyCode.LeftShift);
                foreach (EditHolesRequest denied in OnEditHolesPermissionDenied.GetInvocationList().Cast<EditHolesRequest>())
                {
                    try
                    {
                        denied(isFilling, ref allow);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Plugin threw an error in " + typeof(ClientEvents).Format() + "." + nameof(OnEditHolesPermissionDenied) + ".", method: "CLIENT EVENTS");
                        Logger.LogError(ex, method: "CLIENT EVENTS");
                        allow = false;
                    }

                    if (allow)
                        goto rtn;
                }
            }
        }
        rtn:
        if (!allow)
            UIMessage.SendEditorMessage(DevkitServerModule.MessageLocalization.Translate("NoPermissions"));
        return allow;
    }
    [UsedImplicitly]
    private static bool OnPermissionsInvoker()
    {
        bool allow = true;
        if (EditorActions.IsPlayingCatchUp)
        {
            UIMessage.SendEditorMessage(DevkitServerModule.MessageLocalization.Translate("BeingSynced"));
            return false;
        }
        if (UserInput.ActiveTool is TerrainEditor editor && GetTerrainBrushWorldPosition != null)
        {
            TileSync? sync = TileSync.GetAuthority();
            if (sync != null && sync.Pending.HasValue)
            {
                float rad = TerrainEditor.toolMode switch
                {
                    TerrainEditor.EDevkitLandscapeToolMode.HEIGHTMAP => editor.heightmapBrushRadius,
                    TerrainEditor.EDevkitLandscapeToolMode.SPLATMAP => editor.splatmapBrushRadius,
                    _ => 0
                };
                if (sync.Pending.Value.Type == TileSync.DataType.Heightmap && TerrainEditor.toolMode == TerrainEditor.EDevkitLandscapeToolMode.HEIGHTMAP ||
                    sync.Pending.Value.Type == TileSync.DataType.Splatmap && TerrainEditor.toolMode == TerrainEditor.EDevkitLandscapeToolMode.SPLATMAP && TerrainEditor.splatmapMode != TerrainEditor.EDevkitLandscapeToolSplatmapMode.CUT ||
                    sync.Pending.Value.Type == TileSync.DataType.Holes && TerrainEditor.toolMode == TerrainEditor.EDevkitLandscapeToolMode.SPLATMAP && TerrainEditor.splatmapMode == TerrainEditor.EDevkitLandscapeToolSplatmapMode.CUT)
                {
                    if (sync.Pending.Value.CollidesWith2DCircle(GetTerrainBrushWorldPosition(editor), rad))
                    {
                        UIMessage.SendEditorMessage(DevkitServerModule.MessageLocalization.Translate("BeingSynced"));
                        return false;
                    }
                }
            }
        }
        if (TerrainEditor.toolMode == TerrainEditor.EDevkitLandscapeToolMode.HEIGHTMAP)
        {
            if (OnEditHeightmapRequested == null) return true;
            foreach (EditHeightmapRequest requested in OnEditHeightmapRequested.GetInvocationList().Cast<EditHeightmapRequest>())
            {
                try
                {
                    requested(TerrainEditor.heightmapMode, ref allow);
                }
                catch (Exception ex)
                {
                    Logger.LogError("Plugin threw an error in " + typeof(ClientEvents).Format() + "." + nameof(OnEditHeightmapRequested) + ".", method: "CLIENT EVENTS");
                    Logger.LogError(ex, method: "CLIENT EVENTS");
                    allow = false;
                }

                if (allow)
                    return allow;
            }
        }
        else if (TerrainEditor.toolMode == TerrainEditor.EDevkitLandscapeToolMode.SPLATMAP)
        {
            if (TerrainEditor.splatmapMode != TerrainEditor.EDevkitLandscapeToolSplatmapMode.CUT)
            {
                if (TerrainEditor.splatmapMode == TerrainEditor.EDevkitLandscapeToolSplatmapMode.SMOOTH && DevkitLandscapeToolSplatmapOptions.instance.smoothMethod == EDevkitLandscapeToolSplatmapSmoothMethod.PIXEL_AVERAGE && ClientInfo.Info is not { EnablePixelAverageSplatmapSmoothing: true })
                {
                    UIMessage.SendEditorMessage(DevkitServerModule.MessageLocalization.Translate("FeatureDisabled"));
                    return false;
                }
                if (OnEditSplatmapRequested == null) return true;
                foreach (EditSplatmapRequest requested in OnEditSplatmapRequested.GetInvocationList().Cast<EditSplatmapRequest>())
                {
                    try
                    {
                        requested(TerrainEditor.splatmapMode, ref allow);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Plugin threw an error in " + typeof(ClientEvents).Format() + "." + nameof(OnEditSplatmapRequested) + ".", method: "CLIENT EVENTS");
                        Logger.LogError(ex, method: "CLIENT EVENTS");
                        allow = false;
                    }

                    if (allow)
                        return allow;
                }
            }
            else
            {
                if (OnEditHolesRequested == null) return true;
                bool isFilling = InputEx.GetKey(KeyCode.LeftShift);
                foreach (EditHolesRequest requested in OnEditHolesRequested.GetInvocationList().Cast<EditHolesRequest>())
                {
                    try
                    {
                        requested(isFilling, ref allow);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("Plugin threw an error in " + typeof(ClientEvents).Format() + "." + nameof(OnEditHolesRequested) + ".", method: "CLIENT EVENTS");
                        Logger.LogError(ex, method: "CLIENT EVENTS");
                        allow = false;
                    }

                    if (allow)
                        return allow;
                }
            }
        }

        return allow;
    }

    [UsedImplicitly]
    private static void OnRampConfirm(Bounds bounds)
    {
        if (!DevkitServerModule.IsEditing || UserInput.ActiveTool is not TerrainEditor editor || GetRampStart == null || GetRampEnd == null) return;
        
        OnRampComplete?.Invoke(bounds, GetRampStart(editor), GetRampEnd(editor),
            editor.heightmapBrushRadius, editor.heightmapBrushFalloff);
    }
    [UsedImplicitly]
    private static void OnAdjustConfirm(Bounds bounds)
    {
        if (!DevkitServerModule.IsEditing || UserInput.ActiveTool is not TerrainEditor editor || GetTerrainBrushWorldPosition == null) return;
        
        OnAdjusted?.Invoke(bounds, GetTerrainBrushWorldPosition(editor),
            editor.heightmapBrushRadius, editor.heightmapBrushFalloff,
            editor.heightmapBrushStrength, editor.heightmapAdjustSensitivity,
            InputEx.GetKey(KeyCode.LeftShift), Time.deltaTime);
    }
    [UsedImplicitly]
    private static void OnFlattenConfirm(Bounds bounds)
    {
        if (!DevkitServerModule.IsEditing || UserInput.ActiveTool is not TerrainEditor editor || GetTerrainBrushWorldPosition == null) return;
        
        OnFlattened?.Invoke(bounds, GetTerrainBrushWorldPosition(editor),
            editor.heightmapBrushRadius, editor.heightmapBrushFalloff,
            editor.heightmapBrushStrength, editor.heightmapFlattenSensitivity,
            editor.heightmapFlattenTarget,
            DevkitLandscapeToolHeightmapOptions.instance.flattenMethod, Time.deltaTime);
    }
    [UsedImplicitly]
    private static void OnSmoothConfirm(Bounds bounds)
    {
        if (!DevkitServerModule.IsEditing || UserInput.ActiveTool is not TerrainEditor editor || GetTerrainBrushWorldPosition == null || GetHeightmapSmoothTarget == null) return;

        EDevkitLandscapeToolHeightmapSmoothMethod method = DevkitLandscapeToolHeightmapOptions.instance.smoothMethod;
        OnSmoothed?.Invoke(bounds, GetTerrainBrushWorldPosition(editor),
            editor.heightmapBrushRadius, editor.heightmapBrushFalloff,
            editor.heightmapBrushStrength, GetHeightmapSmoothTarget(editor),
            method, Time.deltaTime);
    }
    [UsedImplicitly]
    private static void OnPaintConfirm(Bounds bounds)
    {
        if (!DevkitServerModule.IsEditing || UserInput.ActiveTool is not TerrainEditor editor || GetTerrainBrushWorldPosition == null) return;

        DevkitLandscapeToolSplatmapOptions settings = DevkitLandscapeToolSplatmapOptions.instance;
        OnPainted?.Invoke(bounds, GetTerrainBrushWorldPosition(editor), editor.splatmapBrushRadius,
            editor.splatmapBrushFalloff, editor.splatmapBrushStrength, editor.splatmapPaintSensitivity,
            editor.splatmapWeightTarget,
            InputEx.GetKey(KeyCode.LeftControl) || editor.splatmapUseWeightTarget,
            settings.useAutoSlope, settings.useAutoFoundation,
            settings.autoMinAngleBegin, settings.autoMinAngleEnd,
            settings.autoMaxAngleBegin, settings.autoMaxAngleEnd,
            settings.autoRayLength, settings.autoRayRadius,
            settings.autoRayMask, InputEx.GetKey(KeyCode.LeftShift),
            TerrainEditor.splatmapMaterialTarget, Time.deltaTime);
    }
    [UsedImplicitly]
    private static void OnAutoPaintConfirm(Bounds bounds)
    {
        if (!DevkitServerModule.IsEditing || UserInput.ActiveTool is not TerrainEditor editor || GetTerrainBrushWorldPosition == null) return;

        DevkitLandscapeToolSplatmapOptions settings = DevkitLandscapeToolSplatmapOptions.instance;
        OnAutoPainted?.Invoke(bounds, GetTerrainBrushWorldPosition(editor), editor.splatmapBrushRadius,
            editor.splatmapBrushFalloff, editor.splatmapBrushStrength, editor.splatmapPaintSensitivity,
            editor.splatmapWeightTarget,
            InputEx.GetKey(KeyCode.LeftControl) || editor.splatmapUseWeightTarget,
            settings.useAutoSlope, settings.useAutoFoundation,
            settings.autoMinAngleBegin, settings.autoMinAngleEnd,
            settings.autoMaxAngleBegin, settings.autoMaxAngleEnd,
            settings.autoRayLength, settings.autoRayRadius,
            settings.autoRayMask, InputEx.GetKey(KeyCode.LeftShift),
            TerrainEditor.splatmapMaterialTarget, Time.deltaTime);
    }
    
    [UsedImplicitly]
    private static void OnPaintSmoothConfirm(Bounds bounds)
    {
        if (!DevkitServerModule.IsEditing || UserInput.ActiveTool is not TerrainEditor editor || GetTerrainBrushWorldPosition == null) return;
        
        OnPaintSmoothed?.Invoke(bounds, GetTerrainBrushWorldPosition(editor), editor.splatmapBrushRadius,
            editor.splatmapBrushFalloff, editor.splatmapBrushStrength, DevkitLandscapeToolSplatmapOptions.instance.smoothMethod, Time.deltaTime);
    }
    [UsedImplicitly]
    private static void OnHoleConfirm(Bounds bounds)
    {
        if (!DevkitServerModule.IsEditing || UserInput.ActiveTool is not TerrainEditor editor || GetTerrainBrushWorldPosition == null) return;
        
        OnHolePainted?.Invoke(bounds, GetTerrainBrushWorldPosition(editor), editor.splatmapBrushRadius, InputEx.GetKey(KeyCode.LeftShift));
    }
    [UsedImplicitly]
    private static void OnAddTile(LandscapeTile tile)
    {
        if (!DevkitServerModule.IsEditing) return;

        Logger.LogDebug("[CLIENT EVENTS] Tile added: " + tile.coord.Format() + ".");
        OnTileAdded?.Invoke(tile);
    }
    [UsedImplicitly]
    private static void OnRemoveTile()
    {
        if (!DevkitServerModule.IsEditing) return;

        LandscapeTile? tile = TerrainEditor.selectedTile;
        if (tile == null) return;
        Logger.LogDebug("[CLIENT EVENTS] Tile deleted: " + tile.coord.Format() + ".");
        OnTileDeleted?.Invoke(tile);
    }
    #endregion

    #region FoliageEditor.update

    [UsedImplicitly]
    [HarmonyPatch("FoliageEditor", "update")]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> FoliageEditorUpdateTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase method)
    {
        if (FoliageEditor == null)
        {
            Logger.LogWarning("Unable to find type: FoliageEditor.", method: "CLIENT EVENTS");
            foreach (CodeInstruction instruction in instructions)
                yield return instruction;
            DevkitServerModule.Fault();
            yield break;
        }
        Type ce = typeof(ClientEvents);

        MethodInfo removeInstancesInvoker = ce.GetMethod(nameof(OnRemoveInstances), BindingFlags.Static | BindingFlags.NonPublic)!;
        MethodInfo resourceSpawnpointDestroyedInvoker = ce.GetMethod(nameof(OnResourceSpawnpointDestroyed), BindingFlags.Static | BindingFlags.NonPublic)!;
        MethodInfo levelObjectRemovedInvoker = ce.GetMethod(nameof(OnLevelObjectRemovedInvoker), BindingFlags.Static | BindingFlags.NonPublic)!;
        MethodInfo findLevelObjectUtil = typeof(DevkitServerUtility).GetMethod(nameof(DevkitServerUtility.FindLevelObject), BindingFlags.Static | BindingFlags.Public)!;

        MethodInfo? removeInstances = FoliageEditor.GetMethod("removeInstances",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (removeInstances == null)
            Logger.LogWarning("Unable to find method: FoliageEditor.removeInstances.", method: "CLIENT EVENTS");
        // else
        //     CheckCopiedMethodPatchOutOfDate(ref removeInstances, removeInstancesInvoker);

        MethodInfo? rspDestroy = typeof(ResourceSpawnpoint).GetMethod(nameof(ResourceSpawnpoint.destroy),
            BindingFlags.Public | BindingFlags.Instance);
        if (removeInstances == null)
            Logger.LogWarning("Unable to find method: ResourceSpawnpoint.destroy.", method: "CLIENT EVENTS");

        MethodInfo? lvlObjRemove = typeof(LevelObjects).GetMethod(nameof(LevelObjects.removeObject),
            BindingFlags.Public | BindingFlags.Static);
        if (removeInstances == null)
            Logger.LogWarning("Unable to find method: LevelObjects.removeObject.", method: "CLIENT EVENTS");

        MethodInfo? lvlObjTransformGetter = typeof(LevelObject).GetProperty(nameof(LevelObject.transform), BindingFlags.Public | BindingFlags.Instance)?.GetMethod;
        if (lvlObjTransformGetter == null)
            Logger.LogWarning("Unable to find property getter: LevelObject.transform.", method: "CLIENT EVENTS");

        MethodInfo? transformPosGetter = typeof(Transform).GetProperty(nameof(Transform.position), BindingFlags.Public | BindingFlags.Instance)?.GetMethod;
        if (transformPosGetter == null)
            Logger.LogWarning("Unable to find property getter: Transform.position.", method: "CLIENT EVENTS");
        LocalBuilder sampleCount = generator.DeclareLocal(typeof(int));
        List<CodeInstruction> ins = new List<CodeInstruction>(instructions);
        LocalBuilder lvlObject = generator.DeclareLocal(typeof(LevelObject));
        LocalBuilder lvlObjectPos = generator.DeclareLocal(typeof(Vector3));
        int ri = 0, rspd = 0, lod = 0;
        for (int i = 0; i < ins.Count; ++i)
        {
            CodeInstruction c = ins[i];
            if (removeInstances != null && c.Calls(removeInstances))
            {
                ParameterInfo[] ps = removeInstances.GetParameters();
                if (i > ps.Length)
                {
                    if (ps[ps.Length - 1].ParameterType is { IsByRef: true } p && p.GetElementType() == typeof(int))
                    {
                        LocalBuilder? bld = PatchUtil.GetLocal(ins[i - 1], out int index, false);
                        yield return PatchUtil.GetLocalCodeInstruction(bld!, index, false);
                        yield return PatchUtil.GetLocalCodeInstruction(sampleCount, sampleCount.LocalIndex, true);
                        Logger.LogDebug("[CLIENT EVENTS] Inserted set sample count local instruction.");
                    }
                    yield return c;
                    for (int j = i - ps.Length; j < i; ++j)
                    {
                        CodeInstruction l = ins[j];
                        if (l.opcode == OpCodes.Ldloca_S || l.opcode == OpCodes.Ldloca)
                        {
                            if (l.operand is LocalBuilder lbl)
                                yield return PatchUtil.GetLocalCodeInstruction(lbl, lbl.LocalIndex, false,
                                    false);
                            else
                                yield return new CodeInstruction(l.opcode == OpCodes.Ldloca_S ? OpCodes.Ldarg_S : OpCodes.Ldarg, l.operand);
                            yield return PatchUtil.GetLocalCodeInstruction(sampleCount, sampleCount.LocalIndex, false);
                            Logger.LogDebug("[CLIENT EVENTS] Inserted get sample count local instruction.");
                        }
                        else
                            yield return ins[j];
                    }
                    yield return new CodeInstruction(OpCodes.Call, removeInstancesInvoker);
                    Logger.LogDebug("[CLIENT EVENTS] Patched invoker for " + removeInstances.Format() + ".");
                    ++ri;
                }
            }
            else if (rspDestroy != null && c.Calls(rspDestroy) && i > 0 && rspDestroy.GetParameters() is { Length: 0 } && !rspDestroy.IsStatic)
            {
                yield return new CodeInstruction(OpCodes.Dup);
                yield return c;
                yield return new CodeInstruction(OpCodes.Call, resourceSpawnpointDestroyedInvoker);
                Logger.LogDebug("[CLIENT EVENTS] Patched invoker for " + rspDestroy.Format() + ".");
                ++rspd;
            }
            else if (lvlObjRemove != null && c.Calls(lvlObjRemove) && lvlObjTransformGetter != null && transformPosGetter != null && i > 0 &&
                     lvlObjRemove.GetParameters() is { Length: 1 } pl && pl[0].ParameterType == typeof(Transform))
            {
                Label lbl = generator.DefineLabel();
                yield return new CodeInstruction(OpCodes.Dup);
                yield return new CodeInstruction(OpCodes.Call, findLevelObjectUtil);
                yield return new CodeInstruction(OpCodes.Brfalse, lbl);
                yield return new CodeInstruction(OpCodes.Dup);
                yield return PatchUtil.GetLocalCodeInstruction(lvlObject, lvlObject.LocalIndex, true);
                yield return new CodeInstruction(OpCodes.Call, lvlObjTransformGetter);
                yield return new CodeInstruction(OpCodes.Call, transformPosGetter);
                yield return PatchUtil.GetLocalCodeInstruction(lvlObjectPos, lvlObjectPos.LocalIndex, true);
                c.labels.Add(lbl);
                yield return c;
                yield return PatchUtil.GetLocalCodeInstruction(lvlObjectPos, lvlObjectPos.LocalIndex, false);
                yield return PatchUtil.GetLocalCodeInstruction(lvlObject, lvlObject.LocalIndex, false);
                yield return new CodeInstruction(OpCodes.Call, levelObjectRemovedInvoker);
                Logger.LogDebug("[CLIENT EVENTS] Patched invoker for " + lvlObjRemove.Format() + ".");
                ++lod;
            }
            else yield return c;
        }

        if (lod < 1)
        {
            Logger.LogError("Failed to patch " + ((lvlObjRemove ?? (object)"LevelObjects.removeObject").Format()) + " into " + method.Format() + ".", method: "CLIENT EVENTS");
            DevkitServerModule.Fault();
        }
        if (rspd < 1)
        {
            Logger.LogError("Failed to patch " + ((rspDestroy ?? (object)"ResourceSpawnpoint.destroy").Format()) + " into " + method.Format() + ".", method: "CLIENT EVENTS");
            DevkitServerModule.Fault();
        }
        if (ri < 3)
        {
            Logger.LogError("Failed to patch " + ((removeInstances ?? (object)"FoliageEditor.removeInstances").Format()) + " into " + method.Format() + " 3 times.", method: "CLIENT EVENTS");
            DevkitServerModule.Fault();
        }
    }
    
    [UsedImplicitly]
    [HarmonyPatch(typeof(FoliageInfoAsset), "addFoliageToSurface")]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> AddFoliageToSurfaceTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase method)
    {
        MethodInfo addFoliageInvoker = typeof(ClientEvents).GetMethod(nameof(OnAddFoliage), BindingFlags.NonPublic | BindingFlags.Static)!;

        List<CodeInstruction> ins = new List<CodeInstruction>(instructions);
        bool patched = false;
        for (int i = 0; i < ins.Count; ++i)
        {
            CodeInstruction c = ins[i];
            if (c.opcode == OpCodes.Callvirt && c.operand is MethodInfo method2 && method2.Name.Equals("addFoliage", StringComparison.Ordinal))
            {
                CheckCopiedMethodPatchOutOfDate(ref method2, addFoliageInvoker);
                if (method2 != null)
                {
                    ParameterInfo[] ps = method2.GetParameters();
                    if (i > ps.Length)
                    {
                        yield return c;
                        for (int j = i - ps.Length - 1; j < i; ++j)
                        {
                            yield return ins[j].CopyWithoutSpecial();
                        }
                        yield return new CodeInstruction(OpCodes.Call, addFoliageInvoker);
                        Logger.LogDebug("[CLIENT EVENTS] Patched " + method2.Format() + " call in " + method.Format() + ".");
                        patched = true;
                        continue;
                    }
                }
            }

            yield return c;
        }

        if (!patched)
        {
            Logger.LogError("Failed to patch " + method.Format() + ".", method: "CLIENT EVENTS");
            DevkitServerModule.Fault();
        }
    }


    internal static readonly InstanceGetter<object, Vector3>? GetFoliageBrushWorldPosition = Accessor.GenerateInstanceGetter<Vector3>(FoliageEditor, "brushWorldPosition");

    [UsedImplicitly]
    private static void OnAddFoliage(FoliageInfoAsset asset, Vector3 position, Quaternion rotation, Vector3 scale, bool clearWhenBaked)
    {
        if (!DevkitServerModule.IsEditing) return;

        Logger.LogDebug("Foliage " + asset.FriendlyName + " added at " +
                                          position.ToString("F2", CultureInfo.InvariantCulture) +
                                          " rot: " + rotation.eulerAngles.ToString("F2", CultureInfo.InvariantCulture) +
                                          " scale: " + scale.ToString("F2", CultureInfo.InvariantCulture) +
                                          " clear when baked: " + clearWhenBaked + "." );
        OnFoliageAdded?.Invoke(asset, position, rotation, scale, clearWhenBaked);
    }

    [UsedImplicitly]
    private static void OnRemoveInstances(FoliageTile foliageTile, FoliageInstanceList list, float sqrBrushRadius, float sqrBrushFalloffRadius, bool allowRemoveBaked, int sampleCount, int oldSampleCount)
    {
        if (sampleCount == oldSampleCount)
            return;
        if (!DevkitServerModule.IsEditing || !InputEx.GetKey(KeyCode.Mouse0) ||
            GetFoliageBrushWorldPosition == null || UserInput.ActiveTool is not { } tool) return;

        Logger.LogDebug("[CLIENT EVENTS] " + (oldSampleCount - sampleCount) + " foliage removed from " +
                         foliageTile.coord.ToString() +
                         " on list " + list.assetReference.Find()?.FriendlyName +
                         " in radius^2: " + sqrBrushRadius.ToString("F2", CultureInfo.InvariantCulture) +
                         " with falloff^2: " + sqrBrushFalloffRadius.ToString("F2", CultureInfo.InvariantCulture) +
                         " remove baked: " + allowRemoveBaked + ".");
        OnFoliageRemoved?.Invoke(GetFoliageBrushWorldPosition(tool), foliageTile, list, sqrBrushRadius, sqrBrushFalloffRadius, allowRemoveBaked, (oldSampleCount - sampleCount));
    }

    [UsedImplicitly]
    private static void OnResourceSpawnpointDestroyed(ResourceSpawnpoint sp)
    {
        if (!DevkitServerModule.IsEditing) return;

        Logger.LogDebug("[CLIENT EVENTS] Resource spawnpoint for " + sp.asset.FriendlyName + " at " +
                        sp.point.ToString("F2", CultureInfo.InvariantCulture) + " removed.");
        OnResourceSpawnpointRemoved?.Invoke(sp);
    }

    [UsedImplicitly]
    private static void OnLevelObjectRemovedInvoker(Vector3 position, LevelObject? obj)
    {
        if (!DevkitServerModule.IsEditing) return;

        if (obj == null)
        {
            Logger.LogWarning("Removed unknown level object.", method: "CLIENT EVENTS");
            return;
        }

        Logger.LogDebug("[CLIENT EVENTS] Level object removed: " + obj.asset.objectName.Format() + " #" +
                        obj.instanceID.Format() + " at " + position.Format() + ".");
        OnLevelObjectRemoved?.Invoke(position, obj);
    }

    #endregion

    #region Tiles
    [HarmonyPatch(typeof(LandscapeTile), nameof(LandscapeTile.updatePrototypes))]
    [HarmonyPostfix]
    [UsedImplicitly]
    private static void OnPrototypesUpdated(LandscapeTile __instance)
    {
        // was ran in the read method or during an apply call, no need to update.
        if (!LandscapeUtil.SaveTransactions || Landscape.getTile(__instance.coord) == null) return;

        OnSplatmapLayerMaterialsUpdate?.Invoke(__instance);
        Logger.LogDebug("[CLIENT EVENTS] Tile prototypes updated: " + __instance.coord.Format() + ".");
    }
    #endregion

    #region EditorInteract.Update
    [HarmonyPatch("EditorInteract", "Update")]
    [HarmonyTranspiler]
    [UsedImplicitly]
    private static IEnumerable<CodeInstruction> EditorInteractUpdateTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase method)
    {
        Type tm = typeof(DevkitTransactionManager);
        Type ce = typeof(ClientEvents);
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
        MethodInfo undoInvoker = ce.GetMethod(nameof(OnUndoRequested), BindingFlags.Static | BindingFlags.NonPublic)!;
        MethodInfo redoInvoker = ce.GetMethod(nameof(OnRedoRequested), BindingFlags.Static | BindingFlags.NonPublic)!;

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
    private static void OnUndoRequested()
    {
        if (EditorUser.User != null && EditorUser.User.Transactions != null)
            EditorUser.User.Transactions.RequestUndo();
    }
    private static void OnRedoRequested()
    {
        if (EditorUser.User != null && EditorUser.User.Transactions != null)
            EditorUser.User.Transactions.RequestRedo();
    }

    #endregion

    #region SelectionTool.update

    public static InstanceGetter<SelectionTool, TransformHandles>? GetSelectionHandles = Accessor.GenerateInstanceGetter<SelectionTool, TransformHandles>("handles");

    [HarmonyPatch(typeof(SelectionTool), "OnHandleTranslatedAndRotated")]
    [HarmonyTranspiler]
    [UsedImplicitly]
    private static IEnumerable<CodeInstruction> SelectionToolOnHandleTranslatedAndRotated(IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase method)
    {
        Type st = typeof(SelectionTool);
        MethodInfo tempMoveHandleInvoker = new Action<DevkitSelection, Vector3, Quaternion, Vector3, bool>(OnTempMoveRotateHandle).Method;

        MethodInfo? getComponent = typeof(GameObject).GetMethods(BindingFlags.Public | BindingFlags.Instance).FirstOrDefault(x =>
            x.Name.Equals("GetComponent") && x.GetParameters().Length == 0 && x.GetGenericArguments().Length == 1)?.MakeGenericMethod(typeof(ITransformedHandler));
        if (getComponent == null)
        {
            Logger.LogWarning("Unable to find method: GameObject.GetComponent<ITransformedHandler>.", method: "CLIENT EVENTS");
            DevkitServerModule.Fault();
        }

        MethodInfo? moveNext;
        using (IEnumerator<DevkitSelection> e = DevkitSelectionManager.selection.GetEnumerator())
            moveNext = e.GetType().GetMethod("MoveNext", BindingFlags.Public | BindingFlags.Instance);
        if (moveNext == null)
        {
            Logger.LogWarning("Unable to find method: IEnumerator<DevkitSelection>.MoveNext.", method: "CLIENT EVENTS");
            DevkitServerModule.Fault();
        }

        List<CodeInstruction> ins = new List<CodeInstruction>(instructions);
        StackTracker tracker = new StackTracker(ins);
        CodeInstruction? ldLocCodeInst = null;
        Label initLabel = generator.DefineLabel();
        for (int i = 0; i < ins.Count; ++i)
        {
            CodeInstruction c = ins[i];
            if (ldLocCodeInst == null && getComponent != null && c.Calls(getComponent) && i < ins.Count - 1 && ins[i + 1].opcode.IsStLoc())
            {
                int lastInd = tracker.GetLastUnconsumedIndex(i, c => c.IsLdLoc(), method);
                if (lastInd != -1)
                {
                    ldLocCodeInst = ins[lastInd].CopyWithoutSpecial();
                }
            }
            if (i > 0 && ldLocCodeInst != null && moveNext != null && ins[i].Calls(moveNext) && ins[i - 1].opcode.IsLdLoc(either: true))
            {
                ldLocCodeInst.labels.AddRange(ins[i - 1].labels);
                ins[i - 1].labels.Clear();
                ins[i - 1].labels.Add(initLabel);
                ins.Insert(i - 1, ldLocCodeInst);
                ins.Insert(i, new CodeInstruction(OpCodes.Brfalse, initLabel));
                ins.Insert(i + 1, ldLocCodeInst.CopyWithoutSpecial());
                ins.Insert(i + 2, new CodeInstruction(OpCodes.Ldarg_1));
                ins.Insert(i + 3, new CodeInstruction(OpCodes.Ldarg_2));
                ins.Insert(i + 4, new CodeInstruction(OpCodes.Ldarg_3));
                ins.Insert(i + 5, new CodeInstruction(OpCodes.Ldarg_S, 4));
                ins.Insert(i + 6, new CodeInstruction(OpCodes.Call, tempMoveHandleInvoker));
                i += 6;
                ldLocCodeInst = null;
                Logger.LogDebug($"[CLIENT EVENTS] Patched in {tempMoveHandleInvoker.Format()} to {method.Format()}.");
            }
        }

        return ins;
    }

    [HarmonyPatch(typeof(SelectionTool), nameof(SelectionTool.update))]
    [HarmonyTranspiler]
    [UsedImplicitly]
    private static IEnumerable<CodeInstruction> SelectionToolUpdateTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase method)
    {
        Type st = typeof(SelectionTool);
        Type dtu = typeof(DevkitTransactionUtility);
        Type vp = typeof(VanillaPermissions);

        MethodInfo moveHandleInvoker = new Action<Vector3, Quaternion, Vector3, bool, bool>(OnMoveHandle).Method;
        MethodInfo requestInstantiationInvoker = new Action<Vector3>(OnRequestInstantiaion).Method;
        MethodInfo recordDestructionInvoker = new Action<GameObject>(OnDestruction).Method;
        MethodInfo transformSelectionInvoker = new Action(OnFinishTransform).Method;
        
        MethodInfo? moveHandle = st.GetMethod("moveHandle", BindingFlags.Instance | BindingFlags.NonPublic, null, new Type[] { typeof(Vector3), typeof(Quaternion), typeof(Vector3), typeof(bool), typeof(bool) }, null);
        if (moveHandle == null)
        {
            Logger.LogWarning("Unable to find method: SelectionTool.moveHandle.", method: "CLIENT EVENTS");
            DevkitServerModule.Fault();
        }

        MethodInfo? requestInstantiation = st.GetMethod("RequestInstantiation", BindingFlags.Instance | BindingFlags.NonPublic, null, new Type[] { typeof(Vector3) }, null);
        if (requestInstantiation == null)
        {
            Logger.LogWarning("Unable to find method: SelectionTool.RequestInstantiation.", method: "CLIENT EVENTS");
            DevkitServerModule.Fault();
        }

        MethodInfo? transformSelection = st.GetMethod("transformSelection", BindingFlags.Instance | BindingFlags.NonPublic, null, Array.Empty<Type>(), null);
        if (transformSelection == null)
        {
            Logger.LogWarning("Unable to find method: SelectionTool.transformSelection.", method: "CLIENT EVENTS");
            DevkitServerModule.Fault();
        }

        MethodInfo? recordDestruction = dtu.GetMethod(nameof(DevkitTransactionUtility.recordDestruction), BindingFlags.Static | BindingFlags.Public, null, new Type[] { typeof(GameObject) }, null);
        if (recordDestruction == null)
        {
            Logger.LogWarning("Unable to find method: DevkitTransactionUtility.recordDestruction.", method: "CLIENT EVENTS");
            DevkitServerModule.Fault();
        }

        List<CodeInstruction> ins = new List<CodeInstruction>(instructions);
        StackTracker tracker = new StackTracker(ins);
        for (int i = 0; i < ins.Count; ++i)
        {
            CodeInstruction c = ins[i];
            MethodInfo? invoker = null;
            if (moveHandle != null && c.Calls(moveHandle))
                invoker = moveHandleInvoker;
            else if (requestInstantiation != null && c.Calls(requestInstantiation))
                invoker = requestInstantiationInvoker;
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
                    Logger.LogDebug($"[CLIENT EVENTS] Patched in {invoker.Format()} to {method.Format()}.");
                }
            }
            else if (recordDestruction != null && c.Calls(recordDestruction))
            {
                ins.Insert(i + 1, new CodeInstruction(OpCodes.Call, recordDestructionInvoker));
                List<CodeInstruction> ins2 = new List<CodeInstruction>(4);
                for (int j = i - 1; j >= 0; --j)
                {
                    ins2.Add(ins[j].CopyWithoutSpecial());
                    if (ins[j].operand is LocalBuilder bld && typeof(IEnumerator<DevkitSelection>).IsAssignableFrom(bld.LocalType))
                        break;
                }
                for (int j = 0; j < ins2.Count; ++j)
                    ins.Insert(i + 1, ins2[j]);
                Logger.LogDebug($"[CLIENT EVENTS] Patched in {recordDestructionInvoker.Format()} to {method.Format()}.");
            }
        }

        return ins;
    }
    [UsedImplicitly]
    private static void OnMoveHandle(Vector3 position, Quaternion rotation, Vector3 scale, bool doRotation, bool hasScale)
    {
        if (!DevkitServerModule.IsEditing) return;

        Logger.LogDebug("[CLIENT EVENTS] Handle moved: " + position.Format() + " Rot: " + doRotation.Format() + ", Scale: " + hasScale.Format() + ".");
    }

    [UsedImplicitly]
    private static void OnRequestInstantiaion(Vector3 position)
    {
        if (!DevkitServerModule.IsEditing) return;

        Logger.LogDebug("[CLIENT EVENTS] Instantiation requested at: " + position.Format() + ".");
    }

    [UsedImplicitly]
    private static void OnDestruction(GameObject obj)
    {
        if (!DevkitServerModule.IsEditing) return;

        Logger.LogDebug("[CLIENT EVENTS] Destruction requested at: " + obj.name.Format() + ".");
    }

    [UsedImplicitly]
    private static void OnTempMoveRotateHandle(DevkitSelection selection, Vector3 deltaPos, Quaternion deltaRot, Vector3 pivotPos, bool modifyRotation)
    {
        if (selection == null || selection.gameObject == null)
            return;

        Logger.LogDebug($"[CLIENT EVENTS] Temp move requested at: {selection.gameObject.name.Format()}: deltaPos: {deltaPos.Format()}, deltaRot: {deltaRot.eulerAngles.Format()}, pivotPos: {pivotPos.Format()}, modifyRotation: {modifyRotation}.");
    }

    private static readonly List<IDevkitHierarchyItem> WorkingHierarchyItems = new List<IDevkitHierarchyItem>(2);
    [UsedImplicitly]
    private static void OnFinishTransform()
    {
        if (!DevkitServerModule.IsEditing) return;
        HierarchyObjectTransformation[] translations = new HierarchyObjectTransformation[DevkitSelectionManager.selection.Count];
        int index = -1;
        foreach (DevkitSelection selection in DevkitSelectionManager.selection)
        {
            selection.gameObject.GetComponents(WorkingHierarchyItems);
            try
            {
                for (int i = 0; i < WorkingHierarchyItems.Count; ++i)
                {
                    IDevkitHierarchyItem item = WorkingHierarchyItems[i];
                    if (item.instanceID == 0u)
                        Logger.LogWarning($"Skipped item: {item.Format()} because it was missing an instance ID.", method: "CLIENT EVENTS");
                    else
                    {
                        translations[++index] = new HierarchyObjectTransformation(item.instanceID, HierarchyObjectTransformation.TransformFlags.All,
                            selection.gameObject.transform.position, selection.gameObject.transform.rotation,
                            selection.gameObject.transform.localScale);
                    }
                }
            }
            finally
            {
                WorkingHierarchyItems.Clear();
            }
        }

        ++index;
        if (index != translations.Length)
            Array.Resize(ref translations, index);

        Logger.LogDebug("[CLIENT EVENTS] Move completed for: " + string.Join(", ", translations) + ".");
    }

    #endregion


    private static void CheckCopiedMethodPatchOutOfDate(ref MethodInfo original, MethodInfo invoker)
    {
        ParameterInfo[] p = original.GetParameters();
        ParameterInfo[] p2 = invoker.GetParameters();
        bool instanceMatches = !original.IsStatic && original.DeclaringType != null && p2.Length > 0 &&
                               (p2[0].ParameterType.IsAssignableFrom(original.DeclaringType) ||
                                p2[0].ParameterType.IsByRef && p2[0].ParameterType.GetElementType()!
                                    .IsAssignableFrom(original.DeclaringType));
        if (p.Length != p2.Length)
        {
            if (!instanceMatches || p.Length != p2.Length - 1)
            {
                Logger.LogWarning("Method patch out of date: " + original.Format() + " vs. " + invoker.Format() + ".", method: "CLIENT EVENTS");

                original = null!;
                return;
            }
        }

        int invOffset = instanceMatches ? 1 : 0;

        for (int i = 0; i < p.Length; ++i)
        {
            if (!p2[i + invOffset].ParameterType.IsAssignableFrom(p[i].ParameterType))
            {
                if (p[i].ParameterType.IsByRef &&
                    p2[i + invOffset].ParameterType.IsAssignableFrom(p[i].ParameterType.GetElementType()))
                    continue;
                if (p2[i + invOffset].ParameterType.IsByRef &&
                    p2[i + invOffset].ParameterType.GetElementType()!.IsAssignableFrom(p[i].ParameterType))
                    continue;
                Logger.LogWarning("Method patch out of date: " + original.Format() + " vs. " + invoker.Format() + ".", method: "CLIENT EVENTS");
                original = null!;
                return;
            }
        }
    }

}

public readonly struct HierarchyObjectTransformation
{
    public TransformFlags Flags { get; }
    public uint InstanceId { get; }
    public Vector3 Position { get; }
    public Quaternion Rotation { get; }
    public Vector3 Scale { get; }

    public HierarchyObjectTransformation(ByteReader reader)
    {
        InstanceId = reader.ReadUInt32();
        TransformFlags flags = reader.ReadEnum<TransformFlags>();
        Flags = flags & ~(TransformFlags)8;
        if ((flags & TransformFlags.Position) != 0)
            Position = reader.ReadVector3();
        else
            Position = Vector3.zero;
        if ((flags & TransformFlags.Rotation) != 0)
            Rotation = reader.ReadQuaternion();
        else
            Rotation = Quaternion.identity;
        if ((flags & (TransformFlags)8) != 0)
            Scale = Vector3.one;
        else if ((flags & TransformFlags.Scale) != 0)
            Scale = reader.ReadVector3();
        else
            Scale = Vector3.zero;
    }
    public HierarchyObjectTransformation(uint instanceId, TransformFlags flags, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        InstanceId = instanceId;
        Flags = flags;
        Position = position;
        Rotation = rotation;
        Scale = scale;
    }

    [Flags]
    public enum TransformFlags : byte
    {
        Position = 1,
        Rotation = 2,
        Scale = 4,
        All = Position | Rotation | Scale
    }
    public void Write(ByteWriter writer)
    {
        writer.Write(InstanceId);
        TransformFlags flags = Flags;
        if ((flags & TransformFlags.Scale) != 0 && Scale.GetRoundedIfNearlyEqualToOne() == Vector3.one)
            flags |= (TransformFlags)8;
        writer.Write(flags);
        if ((Flags & TransformFlags.Position) != 0)
            writer.Write(Position);
        if ((Flags & TransformFlags.Rotation) != 0)
            writer.Write(Rotation);
        if ((flags & TransformFlags.Scale) != 0 && (flags & (TransformFlags)8) == 0)
            writer.Write(Scale);
    }

    public override string ToString() => $"Instance ID: {InstanceId.Format()} moved: {Flags.Format()} (pos: {Position.Format()}, rot: {Rotation.Format()}, scale: {Scale.Format()}).";
}

#endif