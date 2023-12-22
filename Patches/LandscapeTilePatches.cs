using HarmonyLib;
using SDG.Framework.Landscapes;
#if CLIENT
using DevkitServer.Multiplayer.Actions;
#endif

namespace DevkitServer.Patches;

[HarmonyPatch]
internal class LandscapeTilePatches
{
#if CLIENT
    [HarmonyPatch(typeof(LandscapeTile), nameof(LandscapeTile.updatePrototypes))]
    [HarmonyPostfix]
    [UsedImplicitly]
    private static void OnPrototypesUpdated(LandscapeTile __instance)
    {
        if (!DevkitServerModule.IsEditing) return;
        // was ran in the read method or during an apply call, no need to update.
        if (!LandscapeUtil.SaveTransactions || Landscape.getTile(__instance.coord) == null) return;

        ClientEvents.InvokeOnUpdateTileSplatmapLayers(new UpdateLandscapeTileProperties(__instance, CachedTime.DeltaTime));
        Logger.DevkitServer.LogDebug("TILE PATCHES", "Tile prototypes updated: " + __instance.coord.Format() + ".");
    }
#endif

#if SERVER
    [HarmonyPatch(typeof(Landscape), nameof(Landscape.linkNeighbors))]
    [HarmonyPrefix]
    [UsedImplicitly]
    private static bool OnLinkingNeighbors()
    {
        bool supportsInstancing = SystemInfo.supportsInstancing;

        foreach (LandscapeTile tile in LandscapeUtil.Tiles)
            tile.terrain.drawInstanced = supportsInstancing;

        foreach (LandscapeTile tile in LandscapeUtil.Tiles)
        {
            LandscapeTile tile2 = Landscape.getTile(new LandscapeCoord(tile.coord.x - 1, tile.coord.y));
            LandscapeTile tile3 = Landscape.getTile(new LandscapeCoord(tile.coord.x, tile.coord.y + 1));
            LandscapeTile tile4 = Landscape.getTile(new LandscapeCoord(tile.coord.x + 1, tile.coord.y));
            LandscapeTile tile5 = Landscape.getTile(new LandscapeCoord(tile.coord.x, tile.coord.y - 1));
            Terrain? terrain1 = tile2?.terrain;
            Terrain? terrain2 = tile3?.terrain;
            Terrain? terrain3 = tile4?.terrain;
            Terrain? terrain4 = tile5?.terrain;
            tile.terrain.SetNeighbors(terrain1, terrain2, terrain3, terrain4);
        }

        foreach (LandscapeTile tile in LandscapeUtil.Tiles)
            tile.terrain.Flush();

        return false;
    }
#endif
}