using DevkitServer.API.Permissions;
using DevkitServer.Core.Permissions;
using HarmonyLib;
using System.Reflection;
using System.Reflection.Emit;

namespace DevkitServer.Patches;
internal class RoadsPatches
{
    private const string Source = "ROADS PATCHES";

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(EditorRoads), "Update")]
    private static IEnumerable<CodeInstruction> EditorRoadsUpdateTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase method, ILGenerator generator)
    {

        MethodInfo? removeRoad = typeof(LevelRoads).GetMethod(nameof(LevelRoads.removeRoad),
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(Road) },
            null);

        if (removeRoad == null)
        {
            Logger.LogWarning($"{method.Format()} - Unable to find method: LevelRoads.removeRoad.", method: Source);
            DevkitServerModule.Fault();
        }

        MethodInfo? addRoad = typeof(LevelRoads).GetMethod(nameof(LevelRoads.addRoad),
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(Vector3) },
            null);

        if (addRoad == null)
        {
            Logger.LogWarning($"{method.Format()} - Unable to find method: LevelRoads.addRoad.", method: Source);
            DevkitServerModule.Fault();
        }

        MethodInfo? removeVertex = typeof(Road).GetMethod(nameof(Road.removeVertex),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(int) },
            null);

        if (removeVertex == null)
        {
            Logger.LogWarning($"{method.Format()} - Unable to find method: Road.removeVertex.", method: Source);
            DevkitServerModule.Fault();
        }

        MethodInfo? moveTangent = typeof(Road).GetMethod(nameof(Road.moveTangent),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(int), typeof(int), typeof(Vector3) },
            null);

        if (moveTangent == null)
        {
            Logger.LogWarning($"{method.Format()} - Unable to find method: Road.moveTangent.", method: Source);
            DevkitServerModule.Fault();
        }

        MethodInfo? addVertex = typeof(Road).GetMethod(nameof(Road.addVertex),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(int), typeof(Vector3) },
            null);

        if (addVertex == null)
        {
            Logger.LogWarning($"{method.Format()} - Unable to find method: Road.addVertex.", method: Source);
            DevkitServerModule.Fault();
        }



        List<CodeInstruction> ins = new List<CodeInstruction>(instructions);

        for (int i = 0; i < ins.Count; i++)
        {
            // deleting roads by road
            if (PatchUtil.MatchPattern(ins, i,
                    x => x.opcode == OpCodes.Call || x.opcode == OpCodes.Callvirt,
                    x => removeRoad != null && x.Calls(removeRoad)))
            {
                Label continueLbl = generator.DefineLabel();
                ins[i + 2].labels.Add(continueLbl);
                PatchUtil.ReturnIfFalse(ins, generator, ref i, OnRemovingRoad, continueLbl);
            }

            // deleting roads by vertex
            if (PatchUtil.MatchPattern(ins, i,
                    x => x.opcode == OpCodes.Call || x.opcode == OpCodes.Callvirt,
                    null,
                    x => removeVertex != null && x.Calls(removeVertex)))
            {
                Label continueLbl = generator.DefineLabel();
                ins[i + 2].labels.Add(continueLbl);
                PatchUtil.ReturnIfFalse(ins, generator, ref i, OnRemovingRoad, continueLbl);
            }
        }

        return ins;
    }

    private static bool OnRemovingVertex()
    {
        if (!DevkitServerModule.IsEditing)
            return true;

        // todo new perm
        if (!VanillaPermissions.PlaceRoads.Has())
            return false;

        // invoke on road removed for EditorRoads.road
        Logger.LogDebug($"[{Source}] - Removing vertex: {EditorRoads.vertexIndex.Format()}.");
        return true;
    }
    private static bool OnRemovingRoad()
    {
        if (!DevkitServerModule.IsEditing)
            return true;

        // todo new perm
        if (!VanillaPermissions.PlaceRoads.Has())
            return false;

        // invoke on road removed for EditorRoads.road
        Logger.LogDebug($"[{Source}] - Removing road: {EditorRoads.road.roadIndex.Format()}.");
        return true;
    }
}
