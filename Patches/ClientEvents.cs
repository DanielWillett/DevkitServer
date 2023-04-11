#if CLIENT
using HarmonyLib;
using JetBrains.Annotations;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using DevkitServer.Multiplayer;
using SDG.Framework.Devkit.Tools;
using SDG.Framework.Landscapes;
using SDG.Framework.Devkit;
using SDG.Framework.Foliage;
using DevkitServer.Players;

namespace DevkitServer.Patches;

public delegate void PaintHoleAction(Bounds bounds, bool put);
public delegate void LandscapeTileAction(LandscapeTile bounds);
public delegate void AddFoliageAction(Vector3 position, Quaternion rotation, Vector3 scale, bool clearWhenBaked);
public delegate void RemoveFoliageAction(FoliageTile foliageTile, FoliageInstanceList list, float sqrBrushRadius, float sqrBrushFalloffRadius, bool allowRemoveBaked, int sampleCount);
public delegate void ResourceSpawnpointRemovedAction(ResourceSpawnpoint spawnpoint);
public delegate void LevelObjectRemovedAction(Transform objectTransform);
public delegate void RampAction(Bounds bounds, Vector3 start, Vector3 end, float radius, float falloff);
public delegate void AdjustAction(Bounds bounds, Vector3 position, float radius, float falloff, float strength, float sensitivity, bool subtracting, float dt);
public delegate void FlattenAction(Bounds bounds, Vector3 position, float radius, float falloff, float strength, float sensitivity, float target, EDevkitLandscapeToolHeightmapFlattenMethod method, float dt);
public delegate void SmoothAction(Bounds bounds, Vector3 position, float radius, float falloff, float strength, float target, EDevkitLandscapeToolHeightmapSmoothMethod method, float dt);
public delegate void PaintAction(Bounds bounds, Vector3 position, float radius, float falloff, float strength, float target, bool useWeightTarget, bool autoSlope, bool autoFoundation, float autoMinAngleBegin, float autoMinAngleEnd, float autoMaxAngleBegin, float autoMaxAngleEnd, float autoRayLength, float autoRayRadius, ERayMask autoRayMask, float dt);
public delegate void PaintSmoothAction(Bounds bounds, Vector3 position, float radius, float falloff, float strength, EDevkitLandscapeToolSplatmapSmoothMethod method, float dt);

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

    #region TerrainEditor.update
    [HarmonyPatch(typeof(TerrainEditor), nameof(TerrainEditor.update))]
    [HarmonyTranspiler]
    [UsedImplicitly]
    private static IEnumerable<CodeInstruction> TerrainEditorUpdateTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        Type te = typeof(TerrainEditor);
        Type ce = typeof(ClientEvents);
        MethodInfo? rampHandler = te.GetMethod("handleHeightmapWriteRamp",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (rampHandler == null)
        {
            Logger.LogWarning("Unable to find method: TerrainEditor.handleHeightmapWriteRamp.");
            DevkitServerModule.Fault();
        }
        
        MethodInfo? adjustHandler = te.GetMethod("handleHeightmapWriteAdjust",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (adjustHandler == null)
        {
            Logger.LogWarning("Unable to find method: TerrainEditor.handleHeightmapWriteAdjust.");
            DevkitServerModule.Fault();
        }
        
        MethodInfo? flattenHandler = te.GetMethod("handleHeightmapWriteFlatten",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (flattenHandler == null)
        {
            Logger.LogWarning("Unable to find method: TerrainEditor.handleHeightmapWriteFlatten.");
            DevkitServerModule.Fault();
        }
        
        MethodInfo? smoothHandler = te.GetMethod("handleHeightmapWriteSmooth",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (smoothHandler == null)
        {
            Logger.LogWarning("Unable to find method: TerrainEditor.handleHeightmapWriteSmooth.");
            DevkitServerModule.Fault();
        }

        MethodInfo? paintHandler = te.GetMethod("handleSplatmapWritePaint",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (paintHandler == null)
        {
            Logger.LogWarning("Unable to find method: TerrainEditor.handleSplatmapWritePaint.");
            DevkitServerModule.Fault();
        }

        MethodInfo? autoPaintHandler = te.GetMethod("handleSplatmapWriteAuto",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (autoPaintHandler == null)
        {
            Logger.LogWarning("Unable to find method: TerrainEditor.handleSplatmapWriteAuto.");
            DevkitServerModule.Fault();
        }

        MethodInfo? paintSmoothHandler = te.GetMethod("handleSplatmapWriteSmooth",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (paintSmoothHandler == null)
        {
            Logger.LogWarning("Unable to find method: TerrainEditor.handleSplatmapWriteSmooth.");
            DevkitServerModule.Fault();
        }

        MethodInfo? holesHandler = te.GetMethod("handleSplatmapWriteCut",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (holesHandler == null)
        {
            Logger.LogWarning("Unable to find method: TerrainEditor.handleSplatmapWriteCut.");
            DevkitServerModule.Fault();
        }

        MethodInfo? addTile = typeof(Landscape).GetMethod(nameof(Landscape.addTile),
            BindingFlags.Public | BindingFlags.Static);
        if (addTile == null)
        {
            Logger.LogWarning("Unable to find method: Landscape.addTile.");
            DevkitServerModule.Fault();
        }

        MethodInfo? removeTile = typeof(Landscape).GetMethod(nameof(Landscape.removeTile),
            BindingFlags.Public | BindingFlags.Static);
        if (removeTile == null)
        {
            Logger.LogWarning("Unable to find method: Landscape.removeTile.");
            DevkitServerModule.Fault();
        }

        MethodInfo? lHMarkDirty = typeof(LevelHierarchy).GetMethod(nameof(LevelHierarchy.MarkDirty),
            BindingFlags.Public | BindingFlags.Static);
        if (lHMarkDirty == null)
        {
            Logger.LogWarning("Unable to find method: LevelHierarchy.MarkDirty.");
            DevkitServerModule.Fault();
        }

        MethodInfo? writeHeightmap = typeof(Landscape).GetMethod(nameof(Landscape.writeHeightmap),
            BindingFlags.Public | BindingFlags.Static);
        if (writeHeightmap == null)
        {
            Logger.LogWarning("Unable to find method: Landscape.writeHeightmap.");
            DevkitServerModule.Fault();
        }

        MethodInfo? writeSplatmap = typeof(Landscape).GetMethod(nameof(Landscape.writeSplatmap),
            BindingFlags.Public | BindingFlags.Static);
        if (writeSplatmap == null)
        {
            Logger.LogWarning("Unable to find method: Landscape.writeSplatmap.");
            DevkitServerModule.Fault();
        }

        MethodInfo? writeHoles = typeof(Landscape).GetMethod(nameof(Landscape.writeHoles),
            BindingFlags.Public | BindingFlags.Static);
        if (writeHoles == null)
        {
            Logger.LogWarning("Unable to find method: Landscape.writeHoles.");
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
                    yield return new CodeInstruction(OpCodes.Dup);
                    yield return new CodeInstruction(OpCodes.Stloc_S, localBounds.LocalIndex);
                    yield return c;
                    yield return n;
                    yield return ins[i + 2];
                    yield return ins[i + 3];
                    yield return new CodeInstruction(OpCodes.Ldloc_S, localBounds.LocalIndex);
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
                        Logger.LogDebug("Patched in " + invoker.Format() + " call.");
                        ++pCt;
                    }
                    else
                    {
                        yield return new CodeInstruction(OpCodes.Pop);
                        Logger.LogWarning("Unknown function pointer-based method call: " + method.Format() + ".");
                    }

                    i += 3;
                    continue;
                }
            }
            // look for where a tile is added and start waiting until the landscape hierarchy is invalidated
            else if (addTileCt == 0 && addTile != null && c.Calls(addTile) && n != null)
            {
                addTileCt = lHMarkDirty == null ? 2 : 1;
                addTileLcl2 = DevkitServerUtility.GetLocal(n, out addTileLcl, true);
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
                yield return DevkitServerUtility.GetLocalCodeInstruction(addTileLcl2, addTileLcl, false);
                yield return new CodeInstruction(OpCodes.Call, addTileInvoker);
                Logger.LogDebug("Patched in OnAddTile call.");
                addTileCt = 2;
                pAddTile = true;
                continue;
            }
            // on removed tile
            else if (removeTile != null && c.Calls(removeTile))
            {
                yield return c;
                yield return new CodeInstruction(OpCodes.Call, removeTileInvoker);
                Logger.LogDebug("Patched in OnRemoveTile call.");
                pRemoveTile = true;
                continue;
            }

            yield return c;
        }
        if (!pRemoveTile || !pAddTile || pCt != 8)
        {
            Logger.LogWarning($"Patching error for TerrainEditor.update. Invalid transpiler operation: Remove Tile: {pRemoveTile}, Add Tile: {pAddTile}, invoker counts: {pCt} / 8.");
            DevkitServerModule.Fault();
        }
    }

    internal static readonly InstanceGetter<TerrainEditor, Vector3>? GetRampStart =
        Accessor.GenerateInstanceGetter<TerrainEditor, Vector3>("heightmapRampBeginPosition");

    internal static readonly InstanceGetter<TerrainEditor, Vector3>? GetRampEnd =
        Accessor.GenerateInstanceGetter<TerrainEditor, Vector3>("heightmapRampEndPosition");

    internal static readonly InstanceGetter<TerrainEditor, Vector3>? GetBrushWorldPosition =
        Accessor.GenerateInstanceGetter<TerrainEditor, Vector3>("brushWorldPosition");

    internal static readonly InstanceGetter<TerrainEditor, float>? GetHeightmapSmoothTarget =
        Accessor.GenerateInstanceGetter<TerrainEditor, float>("heightmapSmoothTarget");

    [UsedImplicitly]
    private static void OnRampConfirm(Bounds bounds)
    {
        if (!DevkitServerModule.IsEditing || UserInput.ActiveTool is not TerrainEditor editor || GetRampStart == null || GetRampEnd == null) return;

        Logger.LogDebug("Ramp confirmed on bounds " + bounds.ToString("F2", CultureInfo.InvariantCulture) + ".");
        OnRampComplete?.Invoke(bounds, GetRampStart(editor), GetRampEnd(editor),
            editor.heightmapBrushRadius, editor.heightmapBrushFalloff);
    }
    [UsedImplicitly]
    private static void OnAdjustConfirm(Bounds bounds)
    {
        if (!DevkitServerModule.IsEditing || UserInput.ActiveTool is not TerrainEditor editor || GetBrushWorldPosition == null) return;

        Logger.LogDebug("Adjustment performed on bounds " + bounds.ToString("F2", CultureInfo.InvariantCulture) + ".");
        OnAdjusted?.Invoke(bounds, GetBrushWorldPosition(editor),
            editor.heightmapBrushRadius, editor.heightmapBrushFalloff,
            editor.heightmapBrushStrength, editor.heightmapAdjustSensitivity,
            InputEx.GetKey(KeyCode.LeftShift), Time.deltaTime);
    }
    [UsedImplicitly]
    private static void OnFlattenConfirm(Bounds bounds)
    {
        if (!DevkitServerModule.IsEditing || UserInput.ActiveTool is not TerrainEditor editor || GetBrushWorldPosition == null) return;

        Logger.LogDebug("Flatten performed on bounds " + bounds.ToString("F2", CultureInfo.InvariantCulture) + ".");
        OnFlattened?.Invoke(bounds, GetBrushWorldPosition(editor),
            editor.heightmapBrushRadius, editor.heightmapBrushFalloff,
            editor.heightmapBrushStrength, editor.heightmapAdjustSensitivity,
            editor.heightmapFlattenTarget,
            DevkitLandscapeToolHeightmapOptions.instance.flattenMethod, Time.deltaTime);
    }
    [UsedImplicitly]
    private static void OnSmoothConfirm(Bounds bounds)
    {
        if (!DevkitServerModule.IsEditing || UserInput.ActiveTool is not TerrainEditor editor || GetBrushWorldPosition == null || GetHeightmapSmoothTarget == null) return;

        EDevkitLandscapeToolHeightmapSmoothMethod method = DevkitLandscapeToolHeightmapOptions.instance.smoothMethod;
        Logger.LogDebug("Smooth (" + method + ") performed on bounds " + bounds.ToString("F2", CultureInfo.InvariantCulture) + ".");
        OnSmoothed?.Invoke(bounds, GetBrushWorldPosition(editor),
            editor.heightmapBrushRadius, editor.heightmapBrushFalloff,
            editor.heightmapBrushStrength, GetHeightmapSmoothTarget(editor),
            method, Time.deltaTime);
    }
    [UsedImplicitly]
    private static void OnPaintConfirm(Bounds bounds)
    {
        if (!DevkitServerModule.IsEditing || UserInput.ActiveTool is not TerrainEditor editor || GetBrushWorldPosition == null) return;

        DevkitLandscapeToolSplatmapOptions settings = DevkitLandscapeToolSplatmapOptions.instance;
        Logger.LogDebug("Paint performed on bounds " + bounds.ToString("F2", CultureInfo.InvariantCulture) + ".");
        OnPainted?.Invoke(bounds, GetBrushWorldPosition(editor), editor.splatmapBrushRadius,
            editor.splatmapBrushFalloff, editor.splatmapBrushStrength, editor.splatmapWeightTarget,
            InputEx.GetKey(KeyCode.LeftControl) || editor.splatmapUseWeightTarget,
            settings.useAutoSlope, settings.useAutoFoundation,
            settings.autoMinAngleBegin, settings.autoMinAngleEnd,
            settings.autoMaxAngleBegin, settings.autoMaxAngleEnd,
            settings.autoRayLength, settings.autoRayRadius,
            settings.autoRayMask, Time.deltaTime);
    }
    [UsedImplicitly]
    private static void OnAutoPaintConfirm(Bounds bounds)
    {
        if (!DevkitServerModule.IsEditing || UserInput.ActiveTool is not TerrainEditor editor || GetBrushWorldPosition == null) return;

        DevkitLandscapeToolSplatmapOptions settings = DevkitLandscapeToolSplatmapOptions.instance;
        Logger.LogDebug("Auto-paint performed on bounds " + bounds.ToString("F2", CultureInfo.InvariantCulture) + ".");
        OnAutoPainted?.Invoke(bounds, GetBrushWorldPosition(editor), editor.splatmapBrushRadius,
            editor.splatmapBrushFalloff, editor.splatmapBrushStrength, editor.splatmapWeightTarget,
            InputEx.GetKey(KeyCode.LeftControl) || editor.splatmapUseWeightTarget,
            settings.useAutoSlope, settings.useAutoFoundation,
            settings.autoMinAngleBegin, settings.autoMinAngleEnd,
            settings.autoMaxAngleBegin, settings.autoMaxAngleEnd,
            settings.autoRayLength, settings.autoRayRadius,
            settings.autoRayMask, Time.deltaTime);
    }
    [UsedImplicitly]
    private static void OnPaintSmoothConfirm(Bounds bounds)
    {
        if (!DevkitServerModule.IsEditing || UserInput.ActiveTool is not TerrainEditor editor || GetBrushWorldPosition == null) return;

        EDevkitLandscapeToolSplatmapSmoothMethod method = DevkitLandscapeToolSplatmapOptions.instance.smoothMethod;
        Logger.LogDebug("Paint smooth (" + method + " performed on bounds " + bounds.ToString("F2", CultureInfo.InvariantCulture) + ".");
        OnPaintSmoothed?.Invoke(bounds, GetBrushWorldPosition(editor), editor.splatmapBrushRadius,
            editor.splatmapBrushFalloff, editor.splatmapBrushStrength, method, Time.deltaTime);
    }
    [UsedImplicitly]
    private static void OnHoleConfirm(Bounds bounds)
    {
        if (!DevkitServerModule.IsEditing || UserInput.ActiveTool is not TerrainEditor editor) return;

        Logger.LogDebug("Hole paint performed on bounds " + bounds.ToString("F2", CultureInfo.InvariantCulture) + ".");
        OnHolePainted?.Invoke(bounds, InputEx.GetKey(KeyCode.LeftShift));
    }
    [UsedImplicitly]
    private static void OnAddTile(LandscapeTile tile)
    {
        if (!DevkitServerModule.IsEditing || UserInput.ActiveTool is not TerrainEditor editor) return;

        Logger.LogDebug("Tile added: " + tile.coord.ToString() + ".");
        OnTileAdded?.Invoke(tile);
    }
    [UsedImplicitly]
    private static void OnRemoveTile()
    {
        if (!DevkitServerModule.IsEditing || UserInput.ActiveTool is not TerrainEditor editor) return;

        LandscapeTile? tile = TerrainEditor.selectedTile;
        if (tile == null) return;
        Logger.LogDebug("Tile deleted: " + tile.coord.ToString() + ".");
        OnTileDeleted?.Invoke(tile);
    }
    #endregion

    #region Landscape.writeHeightmap
    [HarmonyPatch(typeof(Landscape), nameof(Landscape.writeHeightmap))]
    [HarmonyTranspiler]
    [UsedImplicitly]
    private static IEnumerable<CodeInstruction> LandscapeWriteHeightmapTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        FieldInfo? hmTransactions = typeof(Landscape).GetField("heightmapTransactions", BindingFlags.NonPublic | BindingFlags.Static);
        if (hmTransactions == null)
        {
            Logger.LogWarning("Unable to find field: Landscape.heightmapTransactions.");
            DevkitServerModule.Fault();
        }

        List<CodeInstruction> ins = new List<CodeInstruction>(instructions);
        bool ld = false;
        bool st = false;
        for (int i = 0; i < ins.Count; ++i)
        {
            CodeInstruction c = ins[i];
            if (!ld && c.LoadsField(hmTransactions))
            {
                for (int j = i + 1; j < ins.Count; ++j)
                {
                    if (ins[j].Branches(out Label? lbl) && lbl.HasValue)
                    {
                        st = true;
                        yield return new CodeInstruction(OpCodes.Ldsfld, EditorTerrain.SaveTransactionsField);
                        yield return new CodeInstruction(OpCodes.Not);
                        yield return ins[j];
                        Logger.LogDebug("Inserted save transactions check in writeLandscape.");
                        break;
                    }
                }
                    
                ld = true;
            }

            yield return c;
        }
        if (!st)
        {
            Logger.LogWarning("Patching error for Landscape.writeHeightmap. Invalid transpiler operation.");
            DevkitServerModule.Fault();
        }
    }
    #endregion

    #region FoliageEditor.update

    [UsedImplicitly]
    [HarmonyPatch("FoliageEditor", "update")]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> FoliageEditorUpdateTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        Type? foliageEditor = typeof(Provider).Assembly.GetType("SDG.Unturned.FoliageEditor");
        if (foliageEditor == null)
        {
            Logger.LogWarning("Unable to find type: FoliageEditor.");
            foreach (CodeInstruction instruction in instructions)
                yield return instruction;
            yield break;
        }
        Type ce = typeof(ClientEvents);

        MethodInfo removeInstancesInvoker = ce.GetMethod(nameof(OnRemoveInstances), BindingFlags.Static | BindingFlags.NonPublic)!;
        MethodInfo resourceSpawnpointDestroyedInvoker = ce.GetMethod(nameof(OnResourceSpawnpointDestroyed), BindingFlags.Static | BindingFlags.NonPublic)!;
        MethodInfo levelObjectRemovedInvoker = ce.GetMethod(nameof(OnLevelObjectRemovedInvoker), BindingFlags.Static | BindingFlags.NonPublic)!;

        MethodInfo? removeInstances = foliageEditor.GetMethod("removeInstances",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (removeInstances == null)
            Logger.LogWarning("Unable to find method: FoliageEditor.removeInstances.");
        else
            CheckCopiedMethodPatchOutOfDate(ref removeInstances, removeInstancesInvoker);

        MethodInfo? rspDestroy = typeof(ResourceSpawnpoint).GetMethod(nameof(ResourceSpawnpoint.destroy),
            BindingFlags.Public | BindingFlags.Instance);
        if (removeInstances == null)
            Logger.LogWarning("Unable to find method: ResourceSpawnpoint.destroy.");

        MethodInfo? lvlObjRemove = typeof(LevelObjects).GetMethod(nameof(LevelObjects.removeObject),
            BindingFlags.Public | BindingFlags.Static);
        if (removeInstances == null)
            Logger.LogWarning("Unable to find method: LevelObjects.removeObject.");
        LocalBuilder sampleCount = generator.DeclareLocal(typeof(int));
        List<CodeInstruction> ins = new List<CodeInstruction>(instructions);
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
                        LocalBuilder? bld = DevkitServerUtility.GetLocal(ins[i - 1], out int index, false);
                        yield return DevkitServerUtility.GetLocalCodeInstruction(bld!, index, false);
                        yield return DevkitServerUtility.GetLocalCodeInstruction(sampleCount, sampleCount.LocalIndex, true);
                        Logger.LogDebug("Inserted set sample count local instruction.");
                    }
                    yield return c;
                    for (int j = i - ps.Length; j < i; ++j)
                    {
                        CodeInstruction l = ins[j];
                        if (l.opcode == OpCodes.Ldloca_S || l.opcode == OpCodes.Ldloca)
                        {
                            yield return DevkitServerUtility.GetLocalCodeInstruction(sampleCount, sampleCount.LocalIndex, false);
                            Logger.LogDebug("Inserted get sample count local instruction.");
                        }
                        else
                            yield return ins[j];
                    }
                    yield return new CodeInstruction(OpCodes.Call, removeInstancesInvoker);
                    Logger.LogDebug("Patched RemoveInstances.");
                }
            }
            else if (rspDestroy != null && c.Calls(rspDestroy) && i > 0 && rspDestroy.GetParameters() is { Length: 0 } && !rspDestroy.IsStatic)
            {
                yield return new CodeInstruction(OpCodes.Dup);
                yield return c;
                yield return new CodeInstruction(OpCodes.Call, resourceSpawnpointDestroyedInvoker);
                Logger.LogDebug("Patched ResourceSpawnpointDestroyed.");
            }
            else if (lvlObjRemove != null && c.Calls(lvlObjRemove) && i > 0 &&
                     lvlObjRemove.GetParameters() is { Length: 1 } pl && pl[0].ParameterType == typeof(Transform))
            {
                yield return new CodeInstruction(OpCodes.Dup);
                yield return c;
                yield return new CodeInstruction(OpCodes.Call, lvlObjRemove);
                Logger.LogDebug("Patched LevelObjectRemoved.");
            }
            else yield return c;
        }
    }

    [UsedImplicitly]
    [HarmonyPatch(typeof(FoliageInfoAsset), "addFoliageToSurface")]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> AddFoliageToSurfaceTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        MethodInfo addFoliageInvoker = typeof(ClientEvents).GetMethod(nameof(OnAddFoliage), BindingFlags.NonPublic | BindingFlags.Static)!;

        List<CodeInstruction> ins = new List<CodeInstruction>(instructions);
        for (int i = 0; i < ins.Count; ++i)
        {
            CodeInstruction c = ins[i];
            if (c.opcode == OpCodes.Callvirt && c.operand is MethodInfo method && method.Name.Equals("addFoliage", StringComparison.Ordinal))
            {
                CheckCopiedMethodPatchOutOfDate(ref method, addFoliageInvoker);
                if (method != null)
                {
                    ParameterInfo[] ps = method.GetParameters();
                    if (i > ps.Length)
                    {
                        yield return c;
                        for (int j = i - ps.Length; j < i; ++j)
                        {
                            yield return ins[j];
                        }
                        yield return new CodeInstruction(OpCodes.Call, addFoliageInvoker);
                        Logger.LogDebug("Patched OnAddFoliage.");
                        continue;
                    }
                }
            }

            yield return c;
        }
    }


    [UsedImplicitly]
    private static void OnAddFoliage(Vector3 position, Quaternion rotation, Vector3 scale, bool clearWhenBaked)
    {
        if (!DevkitServerModule.IsEditing) return;

        Logger.LogDebug("Foliage added at " +
                        position.ToString("F2", CultureInfo.InvariantCulture) +
                        " rot: " + rotation.eulerAngles.ToString("F2", CultureInfo.InvariantCulture) +
                        " scale: " + scale.ToString("F2", CultureInfo.InvariantCulture) +
                        " clear when baked: " + clearWhenBaked + "." );
        OnFoliageAdded?.Invoke(position, rotation, scale, clearWhenBaked);
    }

    [UsedImplicitly]
    private static void OnRemoveInstances(FoliageTile foliageTile, FoliageInstanceList list, float sqrBrushRadius, float sqrBrushFalloffRadius, bool allowRemoveBaked, int sampleCount)
    {
        if (!DevkitServerModule.IsEditing) return;

        Logger.LogDebug(sampleCount + " foliage removed from " +
                        foliageTile.coord.ToString() +
                        " on list " + list.assetReference.Find()?.FriendlyName +
                        " in radius^2: " + sqrBrushRadius.ToString("F2", CultureInfo.InvariantCulture) +
                        " with falloff^2: " + sqrBrushFalloffRadius.ToString("F2", CultureInfo.InvariantCulture) +
                        " remove baked: " + allowRemoveBaked + ".");
        OnFoliageRemoved?.Invoke(foliageTile, list, sqrBrushRadius, sqrBrushFalloffRadius, allowRemoveBaked, sampleCount);
    }

    [UsedImplicitly]
    private static void OnResourceSpawnpointDestroyed(ResourceSpawnpoint sp)
    {
        if (!DevkitServerModule.IsEditing) return;

        Logger.LogDebug("Resource spawnpoint for " + sp.asset.FriendlyName + " at " +
                        sp.point.ToString("F2", CultureInfo.InvariantCulture) + " removed.");
        OnResourceSpawnpointRemoved?.Invoke(sp);
    }

    [UsedImplicitly]
    private static void OnLevelObjectRemovedInvoker(Transform obj)
    {
        if (!DevkitServerModule.IsEditing) return;

        Logger.LogDebug("Level object for " + obj.name + " at " +
                        obj.position.ToString("F2", CultureInfo.InvariantCulture) + " removed.");
        OnLevelObjectRemoved?.Invoke(obj);
    }

    #endregion

    private static void CheckCopiedMethodPatchOutOfDate(ref MethodInfo original, MethodInfo invoker)
    {
        ParameterInfo[] p = original.GetParameters();
        ParameterInfo[] p2 = invoker.GetParameters();
        if (p.Length != p2.Length)
        {
            Logger.LogWarning("Method patch out of date: " + original.Format() + ".");
            original = null!;
        }
        else
        {
            for (int i = 0; i < p.Length; ++i)
            {
                if (!p2[i].ParameterType.IsAssignableFrom(p[i].ParameterType))
                {
                    if (p[i].ParameterType.IsByRef &&
                        p2[i].ParameterType.IsAssignableFrom(p[i].ParameterType.GetElementType()))
                        continue;
                    Logger.LogWarning("Method patch out of date: " + original.Format() + ".");
                    original = null!;
                    return;
                }
            }
        }
    }

}
#endif