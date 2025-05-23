#if CLIENT
using DanielWillett.ReflectionTools;
using DevkitServer.API;
using DevkitServer.API.Abstractions;
using DevkitServer.API.Permissions;
using DevkitServer.API.UI;
using DevkitServer.Core.Permissions;
using DevkitServer.Models;
using DevkitServer.Multiplayer.Actions;
using DevkitServer.Multiplayer.Levels;
using HarmonyLib;
using System.Reflection;
using System.Reflection.Emit;

namespace DevkitServer.Patches;

[HarmonyPatch]
internal static class RoadsPatches
{
    private const string Source = "ROADS PATCHES";
    private static bool _changing;

    private static void Change(Action action)
    {
        _changing = true;
        try
        {
            action();
        }
        finally
        {
            _changing = false;
        }
    }

    [UsedImplicitly]
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(EditorRoads), "Update")]
    private static IEnumerable<CodeInstruction> EditorRoadsUpdateTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase method, ILGenerator generator)
    {
        MethodInfo? deselect = typeof(EditorRoads).GetMethod("deselect", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
        if (deselect == null)
        {
            Logger.DevkitServer.LogWarning(Source, $"{method.Format()} - Unable to find method: EditorRoads.deselect.");
        }

        MethodInfo? removeRoad = typeof(LevelRoads).GetMethod(nameof(LevelRoads.removeRoad),
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(Road) },
            null);

        if (removeRoad == null)
        {
            Logger.DevkitServer.LogWarning(Source, $"{method.Format()} - Unable to find method: LevelRoads.removeRoad.");
        }

        MethodInfo? addRoad = typeof(LevelRoads).GetMethod(nameof(LevelRoads.addRoad),
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(Vector3) },
            null);

        if (addRoad == null)
        {
            Logger.DevkitServer.LogWarning(Source, $"{method.Format()} - Unable to find method: LevelRoads.addRoad.");
        }

        MethodInfo? removeVertex = typeof(Road).GetMethod(nameof(Road.removeVertex),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(int) },
            null);

        if (removeVertex == null)
        {
            Logger.DevkitServer.LogWarning(Source, $"{method.Format()} - Unable to find method: Road.removeVertex.");
        }

        MethodInfo? moveTangent = typeof(Road).GetMethod(nameof(Road.moveTangent),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(int), typeof(int), typeof(Vector3) },
            null);

        if (moveTangent == null)
        {
            Logger.DevkitServer.LogWarning(Source, $"{method.Format()} - Unable to find method: Road.moveTangent.");
        }

        MethodInfo? moveVertex = typeof(Road).GetMethod(nameof(Road.moveVertex),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(int), typeof(Vector3) },
            null);

        if (moveVertex == null)
        {
            Logger.DevkitServer.LogWarning(Source, $"{method.Format()} - Unable to find method: Road.moveVertex.");
        }

        MethodInfo? addVertex = typeof(Road).GetMethod(nameof(Road.addVertex),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(int), typeof(Vector3) },
            null);

        if (addVertex == null)
        {
            Logger.DevkitServer.LogWarning(Source, $"{method.Format()} - Unable to find method: Road.addVertex.");
        }

        List<CodeInstruction> ins = [..instructions];
        
        bool removingRoad = false, removingVertex = false, movingTangent = false, movingVertex = false, addingRoad = false;
        int addingVertex = 0;

        for (int i = 0; i < ins.Count; i++)
        {
            // deleting roads by road
            if (!removingRoad && PatchUtility.MatchPattern(ins, i,
                    x => removeRoad != null && x.Calls(removeRoad)))
            {
                ins[i] = new CodeInstruction(OpCodes.Call, Accessor.GetMethod(RemoveRoad));
                removingRoad = true;
                Logger.DevkitServer.LogDebug(Source, $"{method.Format()} - Patched on removing road callers.");
            }

            // deleting road verticies
            if (!removingVertex && PatchUtility.MatchPattern(ins, i,
                    x => removeVertex != null && x.Calls(removeVertex)))
            {
                ins[i] = new CodeInstruction(OpCodes.Call, Accessor.GetMethod(RemoveVertex));
                removingVertex = true;
                Logger.DevkitServer.LogDebug(Source, $"{method.Format()} - Patched on removing vertex callers.");

                if (deselect == null)
                    continue;

                int i2 = i;
                if (PatchUtility.ContinueUntil(ins, ref i2, x => x.Calls(deselect), true) >= 0)
                {
                    ins[i2] = new CodeInstruction(OpCodes.Call, Accessor.GetMethod(PostRemoveElement)).MoveLabelsFrom(ins[i2]);
                }
            }

            // move tangent with 'E'
            if (!movingTangent && PatchUtility.MatchPattern(ins, i,
                    x => moveTangent != null && x.Calls(moveTangent)))
            {
                ins[i] = new CodeInstruction(OpCodes.Call, Accessor.GetMethod(MoveTangent));
                movingTangent = true;
                Logger.DevkitServer.LogDebug(Source, $"{method.Format()} - Patched on moving tangent handle callers.");
            }

            // move vertex with 'E'
            if (!movingVertex && PatchUtility.MatchPattern(ins, i,
                    x => moveVertex != null && x.Calls(moveVertex)))
            {
                ins[i] = new CodeInstruction(OpCodes.Call, Accessor.GetMethod(MoveVertex));
                movingVertex = true;
                Logger.DevkitServer.LogDebug(Source, $"{method.Format()} - Patched on moving vertex callers.");
            }

            // add vertex by left clicking
            if (PatchUtility.MatchPattern(ins, i,
                    x => addVertex != null && x.Calls(addVertex)))
            {
                ins[i] = new CodeInstruction(OpCodes.Call, Accessor.GetMethod(AddVertex));
                ++addingVertex;
                Logger.DevkitServer.LogDebug(Source, $"{method.Format()} - Patched on adding vertex callers.");
            }

            // add road by left clicking
            if (!addingRoad && PatchUtility.MatchPattern(ins, i,
                    x => addRoad != null && x.Calls(addRoad)))
            {
                ins[i] = new CodeInstruction(OpCodes.Call, Accessor.GetMethod(AddRoad));
                Logger.DevkitServer.LogDebug(Source, $"{method.Format()} - Patched on adding road callers.");
                addingRoad = true;
            }
        }

        if (!removingRoad)
        {
            Logger.DevkitServer.LogWarning(Source, $"{method.Format()} - Unable to patch {removeRoad.Format()} call.");
        }
        if (!removingVertex)
        {
            Logger.DevkitServer.LogWarning(Source, $"{method.Format()} - Unable to patch {removeVertex.Format()} call.");
        }
        if (!movingTangent)
        {
            Logger.DevkitServer.LogWarning(Source, $"{method.Format()} - Unable to patch {moveTangent.Format()} call.");
        }
        if (!movingVertex)
        {
            Logger.DevkitServer.LogWarning(Source, $"{method.Format()} - Unable to patch {moveVertex.Format()} call.");
        }
        if (addingVertex < 3)
        {
            Logger.DevkitServer.LogWarning(Source, $"{method.Format()} - Unable to patch at least 3 {addVertex.Format()} calls.");
        }
        if (!movingVertex)
        {
            Logger.DevkitServer.LogWarning(Source, $"{method.Format()} - Unable to patch {addRoad.Format()} call.");
        }

        return ins;
    }

    internal static void OptionalPatches()
    {
        MethodInfo? method = typeof(EditorEnvironmentRoadsUI).GetMethod("onSwappedStateMode", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
        if (method == null)
        {
            Logger.DevkitServer.LogWarning(Source, "Unable to find method: EditorEnvironmentRoadsUI.onSwappedStateMode.");
        }
        else
        {
            try
            {
                PatchesMain.Patcher.Patch(method, prefix: new HarmonyMethod(Accessor.GetMethod(OnToggleMode)!));
            }
            catch (Exception ex)
            {
                Logger.DevkitServer.LogError(Source, ex, "Error patching EditorEnvironmentRoadsUI.onSwappedStateMode.");
            }
        }
        method = typeof(EditorEnvironmentRoadsUI).GetMethod("onToggledIgnoreTerrainToggle", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
        if (method == null)
        {
            Logger.DevkitServer.LogWarning(Source, "Unable to find method: EditorEnvironmentRoadsUI.onToggledIgnoreTerrainToggle.");
        }
        else
        {
            try
            {
                PatchesMain.Patcher.Patch(method, prefix: new HarmonyMethod(Accessor.GetMethod(OnToggleIgnoreTerrain)!));
            }
            catch (Exception ex)
            {
                Logger.DevkitServer.LogError(Source, ex, "Error patching EditorEnvironmentRoadsUI.onToggledIgnoreTerrainToggle.");
            }
        }
        method = typeof(EditorEnvironmentRoadsUI).GetMethod("onToggledLoopToggle", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
        if (method == null)
        {
            Logger.DevkitServer.LogWarning(Source, "Unable to find method: EditorEnvironmentRoadsUI.onToggledLoopToggle.");
        }
        else
        {
            try
            {
                PatchesMain.Patcher.Patch(method, prefix: new HarmonyMethod(Accessor.GetMethod(OnToggleIsLoop)!));
            }
            catch (Exception ex)
            {
                Logger.DevkitServer.LogError(Source, ex, "Error patching EditorEnvironmentRoadsUI.onToggledLoopToggle.");
            }
        }
        method = typeof(EditorEnvironmentRoadsUI).GetMethod("onToggledConcreteToggle", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
        if (method == null)
        {
            Logger.DevkitServer.LogWarning(Source, "Unable to find method: EditorEnvironmentRoadsUI.onToggledConcreteToggle.");
        }
        else
        {
            try
            {
                PatchesMain.Patcher.Patch(method, prefix: new HarmonyMethod(Accessor.GetMethod(OnToggleMaterialIsConcrete)!));
            }
            catch (Exception ex)
            {
                Logger.DevkitServer.LogError(Source, ex, "Error patching EditorEnvironmentRoadsUI.onToggledConcreteToggle.");
            }
        }
        method = typeof(EditorEnvironmentRoadsUI).GetMethod("onTypedOffsetField", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
        if (method == null)
        {
            Logger.DevkitServer.LogWarning(Source, "Unable to find method: EditorEnvironmentRoadsUI.onTypedOffsetField.");
        }
        else
        {
            try
            {
                PatchesMain.Patcher.Patch(method, prefix: new HarmonyMethod(Accessor.GetMethod(OnTypedOffset)!));
            }
            catch (Exception ex)
            {
                Logger.DevkitServer.LogError(Source, ex, "Error patching EditorEnvironmentRoadsUI.onTypedOffsetField.");
            }
        }
        method = typeof(EditorEnvironmentRoadsUI).GetMethod("onTypedWidthField", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
        if (method == null)
        {
            Logger.DevkitServer.LogWarning(Source, "Unable to find method: EditorEnvironmentRoadsUI.onTypedWidthField.");
        }
        else
        {
            try
            {
                PatchesMain.Patcher.Patch(method, prefix: new HarmonyMethod(Accessor.GetMethod(OnTypedMaterialWidth)!));
            }
            catch (Exception ex)
            {
                Logger.DevkitServer.LogError(Source, ex, "Error patching EditorEnvironmentRoadsUI.onTypedWidthField.");
            }
        }
        method = typeof(EditorEnvironmentRoadsUI).GetMethod("onTypedHeightField", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
        if (method == null)
        {
            Logger.DevkitServer.LogWarning(Source, "Unable to find method: EditorEnvironmentRoadsUI.onTypedHeightField.");
        }
        else
        {
            try
            {
                PatchesMain.Patcher.Patch(method, prefix: new HarmonyMethod(Accessor.GetMethod(OnTypedMaterialHeight)!));
            }
            catch (Exception ex)
            {
                Logger.DevkitServer.LogError(Source, ex, "Error patching EditorEnvironmentRoadsUI.onTypedHeightField.");
            }
        }
        method = typeof(EditorEnvironmentRoadsUI).GetMethod("onTypedDepthField", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
        if (method == null)
        {
            Logger.DevkitServer.LogWarning(Source, "Unable to find method: EditorEnvironmentRoadsUI.onTypedDepthField.");
        }
        else
        {
            try
            {
                PatchesMain.Patcher.Patch(method, prefix: new HarmonyMethod(Accessor.GetMethod(OnTypedMaterialDepth)!));
            }
            catch (Exception ex)
            {
                Logger.DevkitServer.LogError(Source, ex, "Error patching EditorEnvironmentRoadsUI.onTypedDepthField.");
            }
        }
        method = typeof(EditorEnvironmentRoadsUI).GetMethod("onTypedOffset2Field", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
        if (method == null)
        {
            Logger.DevkitServer.LogWarning(Source, "Unable to find method: EditorEnvironmentRoadsUI.onTypedOffset2Field.");
        }
        else
        {
            try
            {
                PatchesMain.Patcher.Patch(method, prefix: new HarmonyMethod(Accessor.GetMethod(OnTypedMaterialVerticalOffset)!));
            }
            catch (Exception ex)
            {
                Logger.DevkitServer.LogError(Source, ex, "Error patching EditorEnvironmentRoadsUI.onTypedOffset2Field.");
            }
        }
        method = typeof(EditorEnvironmentRoadsUI).GetMethod("onClickedRoadButton", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
        if (method == null)
        {
            Logger.DevkitServer.LogWarning(Source, "Unable to find method: EditorEnvironmentRoadsUI.onClickedRoadButton.");
        }
        else
        {
            try
            {
                PatchesMain.Patcher.Patch(method, prefix: new HarmonyMethod(Accessor.GetMethod(OnClickedMaterial)!));
            }
            catch (Exception ex)
            {
                Logger.DevkitServer.LogError(Source, ex, "Error patching EditorEnvironmentRoadsUI.onClickedRoadButton.");
            }
        }
    }
    [UsedImplicitly]
    private static bool OnToggleMaterialIsConcrete(ISleekToggle toggle, bool state)
    {
        if (_changing)
            return false;
        int materialIndex = EditorRoads.selected;
        if (materialIndex >= LevelRoads.materials.Length)
            return false;

        if (DevkitServerModule.IsEditing && !CanEditRoadMaterials())
        {
            Change(() => toggle.Value = LevelRoads.materials[materialIndex].isConcrete);
            return false;
        }

        SetRoadMaterialIsConcreteProperties properties = new SetRoadMaterialIsConcreteProperties((byte)materialIndex, state, CachedTime.DeltaTime);
        if (DevkitServerModule.IsEditing)
        {
            if (ClientEvents.ListeningOnSetRoadMaterialIsConcreteRequested)
            {
                bool shouldAllow = true;
                ClientEvents.InvokeOnSetRoadMaterialIsConcreteRequested(in properties, ref shouldAllow);
                if (!shouldAllow)
                {
                    Change(() => toggle.Value = LevelRoads.materials[materialIndex].isConcrete);
                    return false;
                }
            }
        }

        RoadUtil.SetMaterialIsConcreteLocal(materialIndex, state);

        if (DevkitServerModule.IsEditing)
            ClientEvents.InvokeOnSetRoadMaterialIsConcrete(in properties);
        return false;
    }
    [UsedImplicitly]
    private static bool OnToggleMode(SleekButtonState button, int index)
    {
        if (_changing || EditorRoads.road == null)
            return false;

        int vertexIndex = RoadUtil.SelectedVertexIndex;
        if (vertexIndex == -1)
        {
            vertexIndex = EditorRoads.road.joints.IndexOf(EditorRoads.joint);
            if (vertexIndex == -1)
                return false;
        }

        if (DevkitServerModule.IsEditing && !CanEditRoads())
        {
            Change(() => button.state = (int)EditorRoads.road.joints[vertexIndex].mode);
            return false;
        }

        int roadId = EditorRoads.road.GetRoadIndex();
        SetRoadVertexTangentHandleModeProperties properties = new SetRoadVertexTangentHandleModeProperties(GetNetIdOrInvalid(roadId), GetNetIdOrInvalid(roadId, vertexIndex), (ERoadMode)index, EditorRoads.road.joints[vertexIndex].mode, CachedTime.DeltaTime);
        if (DevkitServerModule.IsEditing && properties.RoadNetId.id != 0 && properties.VertexNetId.id != 0)
        {
            if (ClientEvents.ListeningOnSetRoadVertexTangentHandleModeRequested)
            {
                bool shouldAllow = true;
                ClientEvents.InvokeOnSetRoadVertexTangentHandleModeRequested(in properties, ref shouldAllow);
                if (!shouldAllow)
                {
                    Change(() => button.state = (int)EditorRoads.road.joints[vertexIndex].mode);
                    return false;
                }
            }
        }

        RoadUtil.SetVertexTangentHandleModeLocal(EditorRoads.road, vertexIndex, (ERoadMode)index);
        if (DevkitServerModule.IsEditing && properties.RoadNetId.id != 0 && properties.VertexNetId.id != 0)
            ClientEvents.InvokeOnSetRoadVertexTangentHandleMode(in properties);
        return false;
    }
    [UsedImplicitly]
    private static bool OnToggleIgnoreTerrain(ISleekToggle toggle, bool state)
    {
        if (_changing || EditorRoads.road == null)
            return false;
        int vertexIndex = RoadUtil.SelectedVertexIndex;
        if (vertexIndex == -1)
        {
            vertexIndex = EditorRoads.road.joints.IndexOf(EditorRoads.joint);
            if (vertexIndex == -1)
                return false;
        }

        if (DevkitServerModule.IsEditing && !CanEditRoads())
        {
            Change(() => toggle.Value = EditorRoads.road.joints[vertexIndex].ignoreTerrain);
            return false;
        }

        int roadIndex = EditorRoads.road.GetRoadIndex();

        SetRoadVertexIgnoreTerrainProperties properties = new SetRoadVertexIgnoreTerrainProperties(GetNetIdOrInvalid(roadIndex), GetNetIdOrInvalid(roadIndex, vertexIndex), state, CachedTime.DeltaTime);
        if (DevkitServerModule.IsEditing && properties.RoadNetId.id != 0 && properties.VertexNetId.id != 0)
        {
            if (ClientEvents.ListeningOnSetRoadVertexIgnoreTerrainRequested)
            {
                bool shouldAllow = true;
                ClientEvents.InvokeOnSetRoadVertexIgnoreTerrainRequested(in properties, ref shouldAllow);
                if (!shouldAllow)
                {
                    Change(() => toggle.Value = EditorRoads.road.joints[vertexIndex].ignoreTerrain);
                    return false;
                }
            }
        }

        RoadUtil.SetVertexIgnoreTerrainLocal(EditorRoads.road, vertexIndex, state);
        if (DevkitServerModule.IsEditing && properties.RoadNetId.id != 0 && properties.VertexNetId.id != 0)
            ClientEvents.InvokeOnSetRoadVertexIgnoreTerrain(in properties);
        return false;
    }
    [UsedImplicitly]
    private static bool OnTypedOffset(ISleekFloat32Field field, float state)
    {
        if (_changing || EditorRoads.road == null)
            return false;
        int vertexIndex = RoadUtil.SelectedVertexIndex;
        if (vertexIndex == -1)
        {
            vertexIndex = EditorRoads.road.joints.IndexOf(EditorRoads.joint);
            if (vertexIndex == -1)
                return false;
        }

        float oldVerticalOffset = EditorRoads.road.joints[vertexIndex].offset;
        if (DevkitServerModule.IsEditing && !CanEditRoads())
        {
            Change(() => field.Value = oldVerticalOffset);
            return false;
        }

        int roadIndex = EditorRoads.road.GetRoadIndex();

        SetRoadVertexVerticalOffsetProperties properties = new SetRoadVertexVerticalOffsetProperties(GetNetIdOrInvalid(roadIndex), GetNetIdOrInvalid(roadIndex, vertexIndex), state, oldVerticalOffset, CachedTime.DeltaTime);
        if (DevkitServerModule.IsEditing && properties.RoadNetId.id != 0 && properties.VertexNetId.id != 0)
        {
            if (ClientEvents.ListeningOnSetRoadVertexVerticalOffsetRequested)
            {
                bool shouldAllow = true;
                ClientEvents.InvokeOnSetRoadVertexVerticalOffsetRequested(in properties, ref shouldAllow);
                if (!shouldAllow)
                {
                    Change(() => field.Value = oldVerticalOffset);
                    return false;
                }
            }
        }

        RoadUtil.SetVertexVerticalOffsetLocal(roadIndex, vertexIndex, state);
        if (DevkitServerModule.IsEditing && properties.RoadNetId.id != 0 && properties.VertexNetId.id != 0)
            ClientEvents.InvokeOnSetRoadVertexVerticalOffset(in properties);
        return false;
    }
    [UsedImplicitly]
    private static bool OnTypedMaterialWidth(ISleekFloat32Field field, float state)
    {
        if (_changing)
            return false;
        int materialIndex = EditorRoads.selected;
        if (materialIndex >= LevelRoads.materials.Length)
            return false;

        float oldWidth = LevelRoads.materials[materialIndex].width;
        if (DevkitServerModule.IsEditing && !CanEditRoadMaterials())
        {
            Change(() => field.Value = oldWidth);
            return false;
        }

        SetRoadMaterialWidthProperties properties = new SetRoadMaterialWidthProperties((byte)materialIndex, state, oldWidth, CachedTime.DeltaTime);
        if (DevkitServerModule.IsEditing)
        {
            if (ClientEvents.ListeningOnSetRoadMaterialWidthRequested)
            {
                bool shouldAllow = true;
                ClientEvents.InvokeOnSetRoadMaterialWidthRequested(in properties, ref shouldAllow);
                if (!shouldAllow)
                {
                    Change(() => field.Value = oldWidth);
                    return false;
                }
            }
        }

        RoadUtil.SetMaterialWidthLocal(materialIndex, state);

        if (DevkitServerModule.IsEditing)
            ClientEvents.InvokeOnSetRoadMaterialWidth(in properties);
        return false;
    }
    [UsedImplicitly]
    private static bool OnTypedMaterialHeight(ISleekFloat32Field field, float state)
    {
        if (_changing)
            return false;
        int materialIndex = EditorRoads.selected;
        if (materialIndex >= LevelRoads.materials.Length)
            return false;

        float oldHeight = LevelRoads.materials[materialIndex].height;
        if (DevkitServerModule.IsEditing && !CanEditRoadMaterials())
        {
            Change(() => field.Value = oldHeight);
            return false;
        }

        SetRoadMaterialHeightProperties properties = new SetRoadMaterialHeightProperties((byte)materialIndex, state, oldHeight, CachedTime.DeltaTime);
        if (DevkitServerModule.IsEditing)
        {
            if (ClientEvents.ListeningOnSetRoadMaterialHeightRequested)
            {
                bool shouldAllow = true;
                ClientEvents.InvokeOnSetRoadMaterialHeightRequested(in properties, ref shouldAllow);
                if (!shouldAllow)
                {
                    Change(() => field.Value = oldHeight);
                    return false;
                }
            }
        }

        RoadUtil.SetMaterialHeightLocal(materialIndex, state);

        if (DevkitServerModule.IsEditing)
            ClientEvents.InvokeOnSetRoadMaterialHeight(in properties);
        return false;
    }
    [UsedImplicitly]
    private static bool OnTypedMaterialDepth(ISleekFloat32Field field, float state)
    {
        if (_changing)
            return false;
        int materialIndex = EditorRoads.selected;
        if (materialIndex >= LevelRoads.materials.Length)
            return false;

        float oldDepth = LevelRoads.materials[materialIndex].depth;
        if (DevkitServerModule.IsEditing && !CanEditRoadMaterials())
        {
            Change(() => field.Value = oldDepth);
            return false;
        }

        SetRoadMaterialDepthProperties properties = new SetRoadMaterialDepthProperties((byte)materialIndex, state, oldDepth, CachedTime.DeltaTime);
        if (DevkitServerModule.IsEditing)
        {
            if (ClientEvents.ListeningOnSetRoadMaterialDepthRequested)
            {
                bool shouldAllow = true;
                ClientEvents.InvokeOnSetRoadMaterialDepthRequested(in properties, ref shouldAllow);
                if (!shouldAllow)
                {
                    Change(() => field.Value = oldDepth);
                    return false;
                }
            }
        }

        RoadUtil.SetMaterialDepthLocal(materialIndex, state);

        if (DevkitServerModule.IsEditing)
            ClientEvents.InvokeOnSetRoadMaterialDepth(in properties);
        return false;
    }
    [UsedImplicitly]
    private static bool OnTypedMaterialVerticalOffset(ISleekFloat32Field field, float state)
    {
        if (_changing)
            return false;
        int materialIndex = EditorRoads.selected;
        if (materialIndex >= LevelRoads.materials.Length)
            return false;

        float oldVerticalOffset = LevelRoads.materials[materialIndex].offset;
        if (DevkitServerModule.IsEditing && !CanEditRoadMaterials())
        {
            Change(() => field.Value = oldVerticalOffset);
            return false;
        }

        SetRoadMaterialVerticalOffsetProperties properties = new SetRoadMaterialVerticalOffsetProperties((byte)materialIndex, state, oldVerticalOffset, CachedTime.DeltaTime);
        if (DevkitServerModule.IsEditing)
        {
            if (ClientEvents.ListeningOnSetRoadMaterialVerticalOffsetRequested)
            {
                bool shouldAllow = true;
                ClientEvents.InvokeOnSetRoadMaterialVerticalOffsetRequested(in properties, ref shouldAllow);
                if (!shouldAllow)
                {
                    Change(() => field.Value = oldVerticalOffset);
                    return false;
                }
            }
        }

        RoadUtil.SetMaterialVerticalOffsetLocal(materialIndex, state);

        if (DevkitServerModule.IsEditing)
            ClientEvents.InvokeOnSetRoadMaterialVerticalOffset(in properties);
        return false;
    }
    [UsedImplicitly]
    private static bool OnToggleIsLoop(ISleekToggle toggle, bool state)
    {
        if (_changing || EditorRoads.road == null)
            return false;

        if (DevkitServerModule.IsEditing && !CanEditRoads())
        {
            Change(() => toggle.Value = !state);
            return false;
        }

        int roadIndex = EditorRoads.road.GetRoadIndex();

        SetRoadIsLoopProperties properties = new SetRoadIsLoopProperties(GetNetIdOrInvalid(roadIndex), state, CachedTime.DeltaTime);
        if (DevkitServerModule.IsEditing && properties.RoadNetId.id != 0)
        {
            if (ClientEvents.ListeningOnSetRoadIsLoopRequested)
            {
                bool shouldAllow = true;
                ClientEvents.InvokeOnSetRoadIsLoopRequested(in properties, ref shouldAllow);
                if (!shouldAllow)
                {
                    Change(() => toggle.Value = !state);
                    return false;
                }
            }
        }

        RoadUtil.SetIsLoopLocal(EditorRoads.road, state);

        if (DevkitServerModule.IsEditing && properties.RoadNetId.id != 0)
            ClientEvents.InvokeOnSetRoadIsLoop(in properties);
        return false;
    }
    [UsedImplicitly]
    private static bool OnClickedMaterial(ISleekElement button)
    {
        if (DevkitServerModule.IsEditing && !CanEditRoads())
            return false;

        ISleekElement? scrollBox = button.Parent?.Parent;
        if (scrollBox == null)
            return true;

        int matIndex = scrollBox.FindIndexOfChild(button.Parent!);
        if (matIndex < 0 || matIndex >= LevelRoads.materials.Length || matIndex > byte.MaxValue)
            return true;

        if (EditorRoads.road != null)
        {
            int roadIndex = EditorRoads.road.GetRoadIndex();

            SetRoadMaterialProperties properties = new SetRoadMaterialProperties(GetNetIdOrInvalid(roadIndex), (byte)matIndex, CachedTime.DeltaTime);
            if (DevkitServerModule.IsEditing && properties.RoadNetId.id != 0)
            {
                if (ClientEvents.ListeningOnSetRoadMaterialRequested)
                {
                    bool shouldAllow = true;
                    ClientEvents.InvokeOnSetRoadMaterialRequested(in properties, ref shouldAllow);
                    if (!shouldAllow)
                        return false;
                }
            }

            RoadUtil.SetMaterialLocal(EditorRoads.road, matIndex);
            if (DevkitServerModule.IsEditing && properties.RoadNetId.id != 0)
                ClientEvents.InvokeOnSetRoadMaterial(in properties);
        }

        return true;
    }
    private static Transform? AddRoad(Vector3 point)
    {
        if (LevelRoads.getRoad(ushort.MaxValue - 1) != null)
        {
            EditorMessage.SendEditorMessage(TranslationSource.DevkitServerMessageLocalizationSource, "TooManyRoads", new object[] { (ushort)(ushort.MaxValue - 1) });
            return null!;
        }

        if (DevkitServerModule.IsEditing)
        {
            if (!CanEditRoads())
                return null;

            RequestInstantiateRoadProperties properties = new RequestInstantiateRoadProperties(point, EditorRoads.selected);
            if (ClientEvents.ListeningOnRequestInstantiateRoadRequested)
            {
                bool shouldAllow = true;
                ClientEvents.InvokeOnRequestInstantiateRoadRequested(in properties, ref shouldAllow);
                if (!shouldAllow)
                    return null;
            }

            RoadUtil.RequestRoadInstantiation(point, EditorRoads.selected);

            ClientEvents.EventOnRequestInstantiateRoad.TryInvoke(in properties);
            
            return null;
        }

        return RoadUtil.AddRoadLocal(point, EditorRoads.selected);
    }
    private static Transform? AddVertex(Road road, int vertexIndex, Vector3 point)
    {
        if (road.joints.Count >= ushort.MaxValue - 1)
        {
            EditorMessage.SendEditorMessage(TranslationSource.DevkitServerMessageLocalizationSource, "TooManyRoadVerticies", new object[] { (ushort)(ushort.MaxValue - 1) });
            return null!;
        }

        Transform? selectedRoadElement;
        if (DevkitServerModule.IsEditing && !CanEditRoads())
        {
            selectedRoadElement = RoadUtil.SelectedRoadElement;
            if (selectedRoadElement != null)
            {
                RoadUtil.Deselect();
                return selectedRoadElement;
            }

            return null;
        }

        if (DevkitServerModule.IsEditing)
        {
            int roadIndex = road.GetRoadIndex();
            NetId roadNetId = GetNetIdOrInvalid(roadIndex);

            if (roadNetId.id == 0)
            {
                Logger.DevkitServer.LogWarning(Source, "Unable to add vertex: road net ID not found.");
                EditorMessage.SendEditorMessage(TranslationSource.DevkitServerMessageLocalizationSource, "UnknownError");
                selectedRoadElement = RoadUtil.SelectedRoadElement;
                if (selectedRoadElement != null)
                {
                    RoadUtil.Deselect();
                    return selectedRoadElement;
                }
                return null;
            }

            RequestInstantiateRoadVertexProperties properties = new RequestInstantiateRoadVertexProperties(roadNetId, point, vertexIndex);
            if (ClientEvents.ListeningOnRequestInstantiateRoadVertexRequested)
            {
                bool shouldAllow = true;
                ClientEvents.InvokeOnRequestInstantiateRoadVertexRequested(in properties, ref shouldAllow);
                if (!shouldAllow)
                {
                    selectedRoadElement = RoadUtil.SelectedRoadElement;
                    if (selectedRoadElement != null)
                    {
                        RoadUtil.Deselect();
                        return selectedRoadElement;
                    }

                    return null;
                }
            }

            RoadUtil.RequestVertexInstantiation(roadIndex, point, vertexIndex);

            ClientEvents.EventOnRequestInstantiateRoadVertex.TryInvoke(in properties);

            selectedRoadElement = RoadUtil.SelectedRoadElement;
            if (selectedRoadElement != null)
            {
                RoadUtil.Deselect();
                return selectedRoadElement;
            }

            return null;
        }

        return RoadUtil.AddVertexLocal(road, vertexIndex, point);
    }
    private static void RemoveRoad(Road road)
    {
        if (DevkitServerModule.IsEditing && !CanEditRoads())
            return;

        int roadIndex = road.GetRoadIndex();

        Vector3 oldPoint = road.joints.Count > 0 ? road.joints[0].vertex : Vector3.zero;
        DeleteRoadProperties properties = new DeleteRoadProperties(GetNetIdOrInvalid(roadIndex), oldPoint, CachedTime.DeltaTime);
        if (DevkitServerModule.IsEditing && properties.RoadNetId.id != 0)
        {
            if (ClientEvents.ListeningOnDeleteRoadRequested)
            {
                bool shouldAllow = true;
                ClientEvents.InvokeOnDeleteRoadRequested(in properties, ref shouldAllow);
                if (!shouldAllow)
                    return;
            }
        }

        RoadUtil.RemoveRoadLocal(road);

        if (DevkitServerModule.IsEditing && properties.RoadNetId.id != 0)
            ClientEvents.InvokeOnDeleteRoad(in properties);
    }
    private static void RemoveVertex(Road road, int vertexIndex)
    {
        if (road.joints.Count == 1)
        {
            RemoveRoad(road);
            return;
        }

        if (DevkitServerModule.IsEditing && !CanEditRoads())
            return;

        int roadIndex = road.GetRoadIndex();

        Vector3 oldPoint = road.joints[vertexIndex].vertex;
        DeleteRoadVertexProperties properties = new DeleteRoadVertexProperties(GetNetIdOrInvalid(roadIndex), GetNetIdOrInvalid(roadIndex, vertexIndex), oldPoint, CachedTime.DeltaTime);
        if (DevkitServerModule.IsEditing && properties.RoadNetId.id != 0 && properties.VertexNetId.id != 0)
        {
            if (ClientEvents.ListeningOnDeleteRoadVertexRequested)
            {
                bool shouldAllow = true;
                ClientEvents.InvokeOnDeleteRoadVertexRequested(in properties, ref shouldAllow);
                if (!shouldAllow)
                    return;
            }
        }

        RoadUtil.RemoveVertexLocal(road, vertexIndex);

        if (DevkitServerModule.IsEditing && properties.RoadNetId.id != 0 && properties.VertexNetId.id != 0)
            ClientEvents.InvokeOnDeleteRoadVertex(in properties);
    }
    private static void PostRemoveElement()
    {
        Road road = EditorRoads.road;
        int index = road.GetRoadIndex();
        if (index == -1 || road.joints.Count == 0)
        {
            RoadUtil.Deselect();
            return;
        }

        int vertexIndex = RoadUtil.SelectedVertexIndex;
        if (vertexIndex > 0 && road.joints.Count > vertexIndex - 1)
            RoadUtil.Select(road.paths[vertexIndex - 1].vertex);
        else if (vertexIndex >= 0 && road.joints.Count > vertexIndex)
            RoadUtil.Select(road.paths[vertexIndex].vertex);
        else
            RoadUtil.Deselect();
    }
    private static void MoveTangent(Road road, int vertexIndex, int tangentIndex, Vector3 point)
    {
        if (DevkitServerModule.IsEditing && !CanEditRoads())
            return;

        int roadIndex = road.GetRoadIndex();

        Vector3 oldPoint = road.joints[vertexIndex].getTangent(tangentIndex);
        MoveRoadTangentHandleProperties properties = new MoveRoadTangentHandleProperties(GetNetIdOrInvalid(roadIndex), GetNetIdOrInvalid(roadIndex, vertexIndex), oldPoint, (TangentHandle)tangentIndex, point, CachedTime.DeltaTime);
        if (DevkitServerModule.IsEditing && properties.RoadNetId.id != 0 && properties.VertexNetId.id != 0)
        {
            if (ClientEvents.ListeningOnMoveRoadTangentHandleRequested)
            {
                bool shouldAllow = true;
                ClientEvents.InvokeOnMoveRoadTangentHandleRequested(in properties, ref shouldAllow);
                if (!shouldAllow)
                    return;
            }
        }

        RoadUtil.SetTangentHandlePositionLocal(road, vertexIndex, (TangentHandle)tangentIndex, point);

        if (DevkitServerModule.IsEditing && properties.RoadNetId.id != 0 && properties.VertexNetId.id != 0)
            ClientEvents.InvokeOnMoveRoadTangentHandle(in properties);
    }
    private static void MoveVertex(Road road, int vertexIndex, Vector3 point)
    {
        if (DevkitServerModule.IsEditing && !CanEditRoads())
            return;

        int roadIndex = road.GetRoadIndex();

        Vector3 oldPoint = road.joints[vertexIndex].vertex;
        MoveRoadVertexProperties properties = new MoveRoadVertexProperties(GetNetIdOrInvalid(roadIndex), GetNetIdOrInvalid(roadIndex, vertexIndex), oldPoint, point, CachedTime.DeltaTime);
        if (DevkitServerModule.IsEditing && properties.RoadNetId.id != 0 && properties.VertexNetId.id != 0)
        {
            if (ClientEvents.ListeningOnMoveRoadVertexRequested)
            {
                bool shouldAllow = true;
                ClientEvents.InvokeOnMoveRoadVertexRequested(in properties, ref shouldAllow);
                if (!shouldAllow)
                    return;
            }
        }
        
        RoadUtil.SetVertexPositionLocal(road, vertexIndex, point);

        if (DevkitServerModule.IsEditing && properties.RoadNetId.id != 0 && properties.VertexNetId.id != 0)
            ClientEvents.InvokeOnMoveRoadVertex(in properties);
    }
    private static bool CanEditRoads()
    {
        if (!VanillaPermissions.EditRoads.Has())
        {
            EditorMessage.SendNoPermissionMessage(VanillaPermissions.EditRoads);
            return false;
        }

        return true;
    }
    private static bool CanEditRoadMaterials()
    {
        if (!VanillaPermissions.EditRoadMaterials.Has())
        {
            EditorMessage.SendNoPermissionMessage(VanillaPermissions.EditRoadMaterials);
            return false;
        }

        return true;
    }
    private static NetId GetNetIdOrInvalid(int roadIndex)
    {
        if (roadIndex < 0 || !DevkitServerModule.IsEditing)
            return NetId.INVALID;
        if (!RoadNetIdDatabase.TryGetRoadNetId(roadIndex, out NetId netId))
        {
            Logger.DevkitServer.LogWarning(Source, $"Unable to find NetId for road: {roadIndex.Format()}.");
            return NetId.INVALID;
        }

        return netId;
    }
    private static NetId GetNetIdOrInvalid(int roadIndex, int vertexIndex)
    {
        if (roadIndex < 0 || !DevkitServerModule.IsEditing)
            return NetId.INVALID;
        if (!RoadNetIdDatabase.TryGetVertexNetId(new RoadVertexIdentifier(roadIndex, vertexIndex), out NetId netId))
        {
            Logger.DevkitServer.LogWarning(Source, $"Unable to find NetId for road: {roadIndex.Format()}, vertex: {vertexIndex.Format()}.");
            return NetId.INVALID;
        }

        return netId;
    }
}
#endif