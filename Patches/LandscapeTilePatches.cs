#if CLIENT
using DevkitServer.Multiplayer.Actions;
using HarmonyLib;
using JetBrains.Annotations;
using SDG.Framework.Landscapes;

namespace DevkitServer.Patches;
[HarmonyPatch]
internal class LandscapeTilePatches
{
    [HarmonyPatch(typeof(LandscapeTile), nameof(LandscapeTile.updatePrototypes))]
    [HarmonyPostfix]
    [UsedImplicitly]
    private static void OnPrototypesUpdated(LandscapeTile __instance)
    {
        // was ran in the read method or during an apply call, no need to update.
        if (!LandscapeUtil.SaveTransactions || Landscape.getTile(__instance.coord) == null) return;

        ClientEvents.InvokeOnUpdateTileSplatmapLayers(new UpdateLandscapeTileProperties(__instance, Time.deltaTime));
        Logger.LogDebug("[CLIENT EVENTS] Tile prototypes updated: " + __instance.coord.Format() + ".");
    }
}
#endif