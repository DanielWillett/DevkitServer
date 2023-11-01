#if CLIENT
using DevkitServer.API;
using DevkitServer.API.Permissions;
using DevkitServer.API.UI;
using DevkitServer.Core.Permissions;
using DevkitServer.Multiplayer.Actions;
using HarmonyLib;
using System.Reflection;
using System.Reflection.Emit;

namespace DevkitServer.Patches;

[HarmonyPatch]
internal static class RoadsPatches
{
    private const string Source = "ROADS PATCHES";

    [UsedImplicitly]
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(EditorRoads), "Update")]
    private static IEnumerable<CodeInstruction> EditorRoadsUpdateTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase method, ILGenerator generator)
    {
        MethodInfo? getRoad = typeof(EditorRoads).GetProperty(nameof(EditorRoads.road), typeof(Road))?.GetGetMethod(true);

        if (getRoad == null)
        {
            Logger.LogWarning($"{method.Format()} - Unable to find property getter: EditorRoads.road.", method: Source);
        }

        MethodInfo? removeRoad = typeof(LevelRoads).GetMethod(nameof(LevelRoads.removeRoad),
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(Road) },
            null);

        if (removeRoad == null)
        {
            Logger.LogWarning($"{method.Format()} - Unable to find method: LevelRoads.removeRoad.", method: Source);
        }

        MethodInfo? addRoad = typeof(LevelRoads).GetMethod(nameof(LevelRoads.addRoad),
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(Vector3) },
            null);

        if (addRoad == null)
        {
            Logger.LogWarning($"{method.Format()} - Unable to find method: LevelRoads.addRoad.", method: Source);
        }

        MethodInfo? removeVertex = typeof(Road).GetMethod(nameof(Road.removeVertex),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(int) },
            null);

        if (removeVertex == null)
        {
            Logger.LogWarning($"{method.Format()} - Unable to find method: Road.removeVertex.", method: Source);
        }

        MethodInfo? moveTangent = typeof(Road).GetMethod(nameof(Road.moveTangent),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(int), typeof(int), typeof(Vector3) },
            null);

        if (moveTangent == null)
        {
            Logger.LogWarning($"{method.Format()} - Unable to find method: Road.moveTangent.", method: Source);
        }

        MethodInfo? moveVertex = typeof(Road).GetMethod(nameof(Road.moveVertex),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(int), typeof(Vector3) },
            null);

        if (moveVertex == null)
        {
            Logger.LogWarning($"{method.Format()} - Unable to find method: Road.moveVertex.", method: Source);
        }

        MethodInfo? addVertex = typeof(Road).GetMethod(nameof(Road.addVertex),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(int), typeof(Vector3) },
            null);

        if (addVertex == null)
        {
            Logger.LogWarning($"{method.Format()} - Unable to find method: Road.addVertex.", method: Source);
        }

        List<CodeInstruction> ins = new List<CodeInstruction>(instructions);
        
        bool removingRoad = false, removingVertex = false, movingTangent = false, movingVertex = false, addingRoad = false;
        int addingVertex = 0;

        for (int i = 0; i < ins.Count; i++)
        {
            // deleting roads by road
            if (!removingRoad && PatchUtil.MatchPattern(ins, i,
                    x => removeRoad != null && x.Calls(removeRoad)))
            {
                ins[i] = new CodeInstruction(OpCodes.Call, Accessor.GetMethod(RemoveRoad));
                removingRoad = true;
                Logger.LogDebug($"[{Source}] {method.Format()} - Patched on removing road callers.");
            }

            // deleting road verticies
            if (!removingVertex && PatchUtil.MatchPattern(ins, i,
                    x => removeVertex != null && x.Calls(removeVertex)))
            {
                ins[i] = new CodeInstruction(OpCodes.Call, Accessor.GetMethod(RemoveVertex));
                removingVertex = true;
                Logger.LogDebug($"[{Source}] {method.Format()} - Patched on removing vertex callers.");
            }

            // move tangent with 'E'
            if (!movingTangent && PatchUtil.MatchPattern(ins, i,
                    x => moveTangent != null && x.Calls(moveTangent)))
            {
                ins[i] = new CodeInstruction(OpCodes.Call, Accessor.GetMethod(MoveTangent));
                movingTangent = true;
                Logger.LogDebug($"[{Source}] {method.Format()} - Patched on moving tangent handle callers.");
            }

            // move vertex with 'E'
            if (!movingVertex && PatchUtil.MatchPattern(ins, i,
                    x => moveVertex != null && x.Calls(moveVertex)))
            {
                ins[i] = new CodeInstruction(OpCodes.Call, Accessor.GetMethod(MoveVertex));
                movingVertex = true;
                Logger.LogDebug($"[{Source}] {method.Format()} - Patched on moving vertex callers.");
            }

            // add vertex by left clicking
            if (PatchUtil.MatchPattern(ins, i,
                    x => addVertex != null && x.Calls(addVertex)))
            {
                ins[i] = new CodeInstruction(OpCodes.Call, Accessor.GetMethod(AddVertex));
                ++addingVertex;
                Logger.LogDebug($"[{Source}] {method.Format()} - Patched on adding vertex callers.");
            }

            // add road by left clicking
            if (!addingRoad && PatchUtil.MatchPattern(ins, i,
                    x => addRoad != null && x.Calls(addRoad)))
            {
                ins[i] = new CodeInstruction(OpCodes.Call, Accessor.GetMethod(AddRoad));
                Logger.LogDebug($"[{Source}] {method.Format()} - Patched on adding road callers.");
                addingRoad = true;
            }
        }

        if (!removingRoad)
        {
            Logger.LogWarning($"{method.Format()} - Unable to patch {removeRoad.Format()} call.", method: Source);
        }
        if (!removingVertex)
        {
            Logger.LogWarning($"{method.Format()} - Unable to patch {removeVertex.Format()} call.", method: Source);
        }
        if (!movingTangent)
        {
            Logger.LogWarning($"{method.Format()} - Unable to patch {moveTangent.Format()} call.", method: Source);
        }
        if (!movingVertex)
        {
            Logger.LogWarning($"{method.Format()} - Unable to patch {moveVertex.Format()} call.", method: Source);
        }
        if (addingVertex < 3)
        {
            Logger.LogWarning($"{method.Format()} - Unable to patch at least 3 {addVertex.Format()} calls.", method: Source);
        }
        if (!movingVertex)
        {
            Logger.LogWarning($"{method.Format()} - Unable to patch {addRoad.Format()} call.", method: Source);
        }

        return ins;
    }
    private static Transform? AddRoad(Vector3 point)
    {
        if (DevkitServerModule.IsEditing && !CanEditRoads())
            return null;

        RequestInstantiateRoadProperties properties = new RequestInstantiateRoadProperties(point);
        if (ClientEvents.ListeningOnRequestInstantiateRoadRequested)
        {
            bool shouldAllow = true;
            ClientEvents.InvokeOnRequestInstantiateRoadRequested(in properties, ref shouldAllow);
            if (!shouldAllow)
                return null;
        }

        Logger.LogDebug($"[{Source}] - Added road at: {point.Format("F1")}.");
        if (DevkitServerModule.IsEditing)
        {
            ClientEvents.EventOnRequestInstantiateRoad.TryInvoke(in properties);
            
            return null;
        }

        return LevelRoads.addRoad(point);
    }
    private static Transform? AddVertex(Road road, int vertexIndex, Vector3 point)
    {
        if (DevkitServerModule.IsEditing && !CanEditRoads())
        {
            if (RoadUtil.SelectedRoadElement != null)
            {
                RoadUtil.Deselect();
                return RoadUtil.SelectedRoadElement;
            }

            return null;
        }

        RequestInstantiateRoadVertexProperties properties = new RequestInstantiateRoadVertexProperties(NetId.INVALID, point);
        if (ClientEvents.ListeningOnRequestInstantiateRoadVertexRequested)
        {
            bool shouldAllow = true;
            ClientEvents.InvokeOnRequestInstantiateRoadVertexRequested(in properties, ref shouldAllow);
            if (!shouldAllow)
            {
                if (RoadUtil.SelectedRoadElement != null)
                {
                    RoadUtil.Deselect();
                    return RoadUtil.SelectedRoadElement;
                }

                return null;
            }
        }

        Logger.LogDebug($"[{Source}] - Added vertex: {road.GetRoadIndex().Format()} - at: {point.Format("F1")}.");
        if (DevkitServerModule.IsEditing)
        {
            ClientEvents.EventOnRequestInstantiateRoadVertex.TryInvoke(in properties);

            if (RoadUtil.SelectedRoadElement != null)
            {
                RoadUtil.Deselect();
                return RoadUtil.SelectedRoadElement;
            }

            return null;
        }

        return road.addVertex(vertexIndex, point);
    }
    private static void RemoveRoad(Road road)
    {
        if (DevkitServerModule.IsEditing && !CanEditRoads())
            return;

        Vector3 oldPoint = road.joints.Count > 0 ? road.joints[0].vertex : Vector3.zero;
        DeleteRoadProperties properties = new DeleteRoadProperties(NetId.INVALID, oldPoint, CachedTime.DeltaTime);
        if (ClientEvents.ListeningOnDeleteRoadRequested)
        {
            bool shouldAllow = true;
            ClientEvents.InvokeOnDeleteRoadRequested(in properties, ref shouldAllow);
            if (!shouldAllow)
                return;
        }

        Logger.LogDebug($"[{Source}] - Removed road: {road.GetRoadIndex().Format()} - from: {oldPoint.Format("F1")}.");
        LevelRoads.removeRoad(road);

        ClientEvents.InvokeOnDeleteRoad(in properties);
    }
    private static void RemoveVertex(Road road, int vertexIndex)
    {
        if (DevkitServerModule.IsEditing && !CanEditRoads())
            return;

        Vector3 oldPoint = road.joints[vertexIndex].vertex;
        DeleteRoadVertexProperties properties = new DeleteRoadVertexProperties(NetId.INVALID, NetId.INVALID, oldPoint, CachedTime.DeltaTime);
        if (ClientEvents.ListeningOnDeleteRoadVertexRequested)
        {
            bool shouldAllow = true;
            ClientEvents.InvokeOnDeleteRoadVertexRequested(in properties, ref shouldAllow);
            if (!shouldAllow)
                return;
        }

        Logger.LogDebug($"[{Source}] - Moved tangent handle: {road.GetRoadIndex().Format()}/{vertexIndex.Format()} - from: {oldPoint.Format("F1")}.");
        road.removeVertex(vertexIndex);

        ClientEvents.InvokeOnDeleteRoadVertex(in properties);
    }
    private static void MoveTangent(Road road, int vertexIndex, int tangentIndex, Vector3 point)
    {
        if (DevkitServerModule.IsEditing && !CanEditRoads())
            return;

        Vector3 oldPoint = road.joints[vertexIndex].getTangent(tangentIndex);
        MoveRoadTangentHandleProperties properties = new MoveRoadTangentHandleProperties(NetId.INVALID, NetId.INVALID, oldPoint, (TangentHandle)tangentIndex, point, CachedTime.DeltaTime);
        if (ClientEvents.ListeningOnMoveRoadTangentHandleRequested)
        {
            bool shouldAllow = true;
            ClientEvents.InvokeOnMoveRoadTangentHandleRequested(in properties, ref shouldAllow);
            if (!shouldAllow)
                return;
        }

        Logger.LogDebug($"[{Source}] - Moved tangent handle: {road.GetRoadIndex().Format()}/{vertexIndex.Format()}/{tangentIndex.Format()} - relative: {oldPoint.Format("F1")} -> {point.Format("F1")}.");
        road.moveTangent(vertexIndex, tangentIndex, point);

        ClientEvents.InvokeOnMoveRoadTangentHandle(in properties);
    }
    private static void MoveVertex(Road road, int vertexIndex, Vector3 point)
    {
        if (DevkitServerModule.IsEditing && !CanEditRoads())
            return;

        Vector3 oldPoint = road.joints[vertexIndex].vertex;
        MoveRoadVertexProperties properties = new MoveRoadVertexProperties(NetId.INVALID, NetId.INVALID, oldPoint, point, CachedTime.DeltaTime);
        if (ClientEvents.ListeningOnMoveRoadVertexRequested)
        {
            bool shouldAllow = true;
            ClientEvents.InvokeOnMoveRoadVertexRequested(in properties, ref shouldAllow);
            if (!shouldAllow)
                return;
        }

        Logger.LogDebug($"[{Source}] - Moved vertex: {road.GetRoadIndex().Format()}/{vertexIndex.Format()} - {oldPoint.Format("F1")} -> {point.Format("F1")}.");
        road.moveVertex(vertexIndex, point);

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
}
#endif