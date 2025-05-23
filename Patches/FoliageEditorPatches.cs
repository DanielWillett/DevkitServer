#if CLIENT
using DanielWillett.ReflectionTools;
using DevkitServer.API;
using DevkitServer.Multiplayer;
using DevkitServer.Multiplayer.Actions;
using DevkitServer.Players;
using HarmonyLib;
using SDG.Framework.Devkit.Tools;
using SDG.Framework.Foliage;
using System.Reflection;
using System.Reflection.Emit;

namespace DevkitServer.Patches;
[HarmonyPatch]
internal static class FoliageEditorPatches
{
    internal static readonly Type FoliageEditor = AccessorExtensions.AssemblyCSharp.GetType("SDG.Unturned.FoliageEditor");

    internal static InstanceGetter<object, IComparable>? GetEditMode = Accessor.GenerateInstanceGetter<IComparable>(FoliageEditor!, "mode", throwOnError: false);

    internal static IComparable? FoliageModeExact;
    static FoliageEditorPatches()
    {
        Type? enumType = FoliageEditor?.GetNestedType("EFoliageMode", BindingFlags.NonPublic | BindingFlags.Public);
        if (enumType != null)
            FoliageModeExact = Enum.ToObject(enumType, 1) as IComparable;

        if (FoliageModeExact != null && FoliageModeExact.ToString().Equals("EXACT", StringComparison.OrdinalIgnoreCase))
            return;

        Logger.DevkitServer.LogWarning("FOLIAGE PATCHES", "Unable to find foliage mode EXACT.");
        FoliageModeExact = null;
    }

    [UsedImplicitly]
    [HarmonyPatch("FoliageEditor", "update")]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> FoliageEditorUpdateTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase method)
    {
        if (FoliageEditor == null)
        {
            Logger.DevkitServer.LogWarning("FOLIAGE PATCHES", "Unable to find type: FoliageEditor.");
            foreach (CodeInstruction instruction in instructions)
                yield return instruction;
            DevkitServerModule.Fault();
            yield break;
        }
        Type fep = typeof(FoliageEditorPatches);

        MethodInfo removeInstancesInvoker = fep.GetMethod(nameof(OnRemoveInstances), BindingFlags.Static | BindingFlags.NonPublic)!;
        MethodInfo resourceSpawnpointDestroyedInvoker = fep.GetMethod(nameof(OnResourceSpawnpointDestroyed), BindingFlags.Static | BindingFlags.NonPublic)!;
        MethodInfo levelObjectRemovedInvoker = fep.GetMethod(nameof(OnLevelObjectRemovedInvoker), BindingFlags.Static | BindingFlags.NonPublic)!;
        MethodInfo findLevelObjectUtil = typeof(LevelObjectUtil).GetMethod(nameof(LevelObjectUtil.FindObject),
            BindingFlags.Static | BindingFlags.Public, null, CallingConventions.Any, new Type[] { typeof(Transform), typeof(bool) }, null)!;

        MethodInfo? removeInstances = FoliageEditor.GetMethod("removeInstances",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (removeInstances == null)
            Logger.DevkitServer.LogWarning("FOLIAGE PATCHES", "Unable to find method: FoliageEditor.removeInstances.");
        // else
        //     CheckCopiedMethodPatchOutOfDate(ref removeInstances, removeInstancesInvoker);

        MethodInfo? rspDestroy = typeof(ResourceSpawnpoint).GetMethod(nameof(ResourceSpawnpoint.destroy),
            BindingFlags.Public | BindingFlags.Instance);
        if (removeInstances == null)
            Logger.DevkitServer.LogWarning("FOLIAGE PATCHES", "Unable to find method: ResourceSpawnpoint.destroy.");

        MethodInfo? lvlObjRemove = typeof(LevelObjects).GetMethod(nameof(LevelObjects.removeObject),
            BindingFlags.Public | BindingFlags.Static);
        if (removeInstances == null)
            Logger.DevkitServer.LogWarning("FOLIAGE PATCHES", "Unable to find method: LevelObjects.removeObject.");

        MethodInfo? lvlObjTransformGetter = typeof(LevelObject).GetProperty(nameof(LevelObject.transform), BindingFlags.Public | BindingFlags.Instance)?.GetMethod;
        if (lvlObjTransformGetter == null)
            Logger.DevkitServer.LogWarning("FOLIAGE PATCHES", "Unable to find property getter: LevelObject.transform.");

        MethodInfo? transformPosGetter = typeof(Transform).GetProperty(nameof(Transform.position), BindingFlags.Public | BindingFlags.Instance)?.GetMethod;
        if (transformPosGetter == null)
            Logger.DevkitServer.LogWarning("FOLIAGE PATCHES", "Unable to find property getter: Transform.position.");
        LocalBuilder sampleCount = generator.DeclareLocal(typeof(int));
        List<CodeInstruction> ins = new List<CodeInstruction>(instructions);
        LocalBuilder lvlObject = generator.DeclareLocal(typeof(LevelObject));
        LocalBuilder lvlObjectPos = generator.DeclareLocal(typeof(Vector3));
        Label stLbl = generator.DefineLabel();

        int ri = 0, rspd = 0, lod = 0;
        for (int i = 0; i < ins.Count; ++i)
        {
            CodeInstruction c = ins[i];
            if (i == 0)
                c.labels.Add(stLbl);
            if (removeInstances != null && c.Calls(removeInstances))
            {
                ParameterInfo[] ps = removeInstances.GetParameters();
                if (i > ps.Length)
                {
                    if (ps[^1].ParameterType is { IsByRef: true } p && p.GetElementType() == typeof(int))
                    {
                        LocalBuilder? bld = PatchUtility.GetLocal(ins[i - 1], out int index, false);
                        yield return PatchHelpers.GetLocalCodeInstruction(bld!, index, false);
                        yield return PatchHelpers.GetLocalCodeInstruction(sampleCount, sampleCount.LocalIndex, true);
                        Logger.DevkitServer.LogDebug("FOLIAGE PATCHES", "Inserted set sample count local instruction.");
                    }
                    yield return c;
                    for (int j = i - ps.Length; j < i; ++j)
                    {
                        CodeInstruction l = ins[j];
                        if (l.opcode == OpCodes.Ldloca_S || l.opcode == OpCodes.Ldloca)
                        {
                            if (l.operand is LocalBuilder lbl)
                                yield return PatchHelpers.GetLocalCodeInstruction(lbl, lbl.LocalIndex, false,
                                    false);
                            else
                                yield return new CodeInstruction(l.opcode == OpCodes.Ldloca_S ? OpCodes.Ldarg_S : OpCodes.Ldarg, l.operand);
                            yield return PatchHelpers.GetLocalCodeInstruction(sampleCount, sampleCount.LocalIndex, false);
                            Logger.DevkitServer.LogDebug("FOLIAGE PATCHES", "Inserted get sample count local instruction.");
                        }
                        else
                            yield return ins[j];
                    }
                    yield return new CodeInstruction(OpCodes.Call, removeInstancesInvoker);
                    Logger.DevkitServer.LogDebug("FOLIAGE PATCHES", "Patched invoker for " + removeInstances.Format() + ".");
                    ++ri;
                }
            }
            else if (rspDestroy != null && c.Calls(rspDestroy) && i > 0 && rspDestroy.GetParameters() is { Length: 0 } && !rspDestroy.IsStatic)
            {
                yield return new CodeInstruction(OpCodes.Dup);
                yield return c;
                yield return new CodeInstruction(OpCodes.Call, resourceSpawnpointDestroyedInvoker);
                Logger.DevkitServer.LogDebug("FOLIAGE PATCHES", "Patched invoker for " + rspDestroy.Format() + ".");
                ++rspd;
            }
            else if (lvlObjRemove != null && c.Calls(lvlObjRemove) && lvlObjTransformGetter != null && transformPosGetter != null && i > 0 &&
                     lvlObjRemove.GetParameters() is { Length: 1 } pl && pl[0].ParameterType == typeof(Transform))
            {
                Label lbl = generator.DefineLabel();
                yield return new CodeInstruction(OpCodes.Dup);
                yield return new CodeInstruction(OpCodes.Ldc_I4_0);
                yield return new CodeInstruction(OpCodes.Call, findLevelObjectUtil);
                yield return new CodeInstruction(OpCodes.Brfalse, lbl);
                yield return new CodeInstruction(OpCodes.Dup);
                yield return PatchHelpers.GetLocalCodeInstruction(lvlObject, lvlObject.LocalIndex, true);
                yield return new CodeInstruction(OpCodes.Call, lvlObjTransformGetter);
                yield return new CodeInstruction(OpCodes.Call, transformPosGetter);
                yield return PatchHelpers.GetLocalCodeInstruction(lvlObjectPos, lvlObjectPos.LocalIndex, true);
                c.labels.Add(lbl);
                yield return c;
                yield return PatchHelpers.GetLocalCodeInstruction(lvlObjectPos, lvlObjectPos.LocalIndex, false);
                yield return PatchHelpers.GetLocalCodeInstruction(lvlObject, lvlObject.LocalIndex, false);
                yield return new CodeInstruction(OpCodes.Call, levelObjectRemovedInvoker);
                Logger.DevkitServer.LogDebug("FOLIAGE PATCHES", "Patched invoker for " + lvlObjRemove.Format() + ".");
                ++lod;
            }
            else yield return c;
        }

        if (lod < 1)
        {
            Logger.DevkitServer.LogError("FOLIAGE PATCHES", "Failed to patch " + ((lvlObjRemove ?? (object)"LevelObjects.removeObject").Format()) + " into " + method.Format() + ".");
            DevkitServerModule.Fault();
        }
        if (rspd < 1)
        {
            Logger.DevkitServer.LogError("FOLIAGE PATCHES", "Failed to patch " + ((rspDestroy ?? (object)"ResourceSpawnpoint.destroy").Format()) + " into " + method.Format() + ".");
            DevkitServerModule.Fault();
        }
        if (ri < 3)
        {
            Logger.DevkitServer.LogError("FOLIAGE PATCHES", "Failed to patch " + ((removeInstances ?? (object)"FoliageEditor.removeInstances").Format()) + " into " + method.Format() + " 3 times.");
            DevkitServerModule.Fault();
        }
    }

    [UsedImplicitly]
    [HarmonyPatch(typeof(FoliageInfoAsset), "addFoliageToSurface")]
    [HarmonyPatch([ typeof(Vector3), typeof(Vector3), typeof(bool), typeof(bool), typeof(bool) ])]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> AddFoliageToSurfaceTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase method)
    {
        MethodInfo addFoliageInvoker = typeof(FoliageEditorPatches).GetMethod(nameof(OnAddFoliage), BindingFlags.NonPublic | BindingFlags.Static)!;

        List<CodeInstruction> ins = new List<CodeInstruction>(instructions);
        bool patched = false;
        for (int i = 0; i < ins.Count; ++i)
        {
            CodeInstruction c = ins[i];
            if (c.opcode == OpCodes.Callvirt && c.operand is MethodInfo method2 && method2.Name.Equals("addFoliage", StringComparison.Ordinal))
            {
                PatchHelpers.CheckCopiedMethodPatchOutOfDate(ref method2, addFoliageInvoker);
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
                        Logger.DevkitServer.LogDebug("FOLIAGE PATCHES", "Patched " + method2.Format() + " call in " + method.Format() + ".");
                        patched = true;
                        continue;
                    }
                }
            }

            yield return c;
        }

        if (!patched)
        {
            Logger.DevkitServer.LogError("FOLIAGE PATCHES", "Failed to patch " + method.Format() + ".");
            DevkitServerModule.Fault();
        }
    }


    internal static readonly InstanceGetter<object, Vector3>? GetFoliageBrushWorldPosition = Accessor.GenerateInstanceGetter<Vector3>(FoliageEditor, "brushWorldPosition");

    [UsedImplicitly]
    private static void OnAddFoliage(FoliageInfoAsset asset, Vector3 position, Quaternion rotation, Vector3 scale, bool clearWhenBaked)
    {
        if (!DevkitServerModule.IsEditing) return;
        uint? objId = null;
        if (asset is FoliageObjectInfoAsset objAsset && Regions.tryGetCoordinate(position, out byte x, out byte y) && LevelObjects.objects[x, y] is { Count: > 0 and <= ushort.MaxValue + 1 } region)
        {
            LevelObject obj = ObjectManager.getObject(x, y, (ushort)(region.Count - 1));
            if (obj.asset.GUID != objAsset.obj.GUID)
                Logger.DevkitServer.LogWarning("FOLIAGE PATCHES", "Unable to find recently placed foliage object.");
            else
                LevelObjectResponsibilities.Set(obj.instanceID, false);
            objId = obj.instanceID;
        }

        ClientEvents.InvokeOnAddFoliage(new AddFoliageProperties(asset, position, rotation, scale, clearWhenBaked, CachedTime.DeltaTime, objId));
    }

    [UsedImplicitly]
    private static void OnRemoveInstances(FoliageTile foliageTile, FoliageInstanceList list, float sqrBrushRadius, float sqrBrushFalloffRadius, bool allowRemoveBaked, int sampleCount, int oldSampleCount)
    {
        if (sampleCount == oldSampleCount || !DevkitServerModule.IsEditing || !InputEx.GetKey(KeyCode.Mouse0) ||
            GetFoliageBrushWorldPosition == null || UserControl.ActiveTool is not { } tool) return;

        ClientEvents.InvokeOnRemoveFoliage(new RemoveFoliageProperties(GetFoliageBrushWorldPosition(tool), foliageTile,
            list, DevkitFoliageToolOptions.instance.brushRadius, DevkitFoliageToolOptions.instance.brushFalloff,
            CachedTime.DeltaTime, allowRemoveBaked, oldSampleCount - sampleCount));
    }

    [UsedImplicitly]
    private static void OnResourceSpawnpointDestroyed(ResourceSpawnpoint sp)
    {
        if (!DevkitServerModule.IsEditing) return;
        
        ClientEvents.InvokeOnRemoveResourceSpawnpointFoliage(new RemoveResourceSpawnpointFoliageProperties(sp, CachedTime.DeltaTime));
    }

    [UsedImplicitly]
    private static void OnLevelObjectRemovedInvoker(Vector3 position, LevelObject? obj)
    {
        if (!DevkitServerModule.IsEditing) return;

        if (obj == null)
        {
            Logger.DevkitServer.LogWarning("FOLIAGE PATCHES", "Removed unknown level object.");
            return;
        }
        
        ClientEvents.InvokeOnRemoveLevelObjectFoliage(new RemoveLevelObjectFoliageProperties(position, obj, CachedTime.DeltaTime));
    }

}
#endif