using DevkitServer.API.Permissions;

namespace DevkitServer.Core.Permissions;
public static class VanillaPermissions
{
    [Permission]
    public static readonly Permission EditTerrain = new Permission("terrain.*", core: true);
    [Permission]
    public static readonly Permission EditTiles = new Permission("tiles.*", core: true);
    [Permission]
    public static readonly Permission AddTile = new Permission("tiles.add", core: true);
    [Permission]
    public static readonly Permission DeleteTile = new Permission("tiles.delete", core: true);
    [Permission]
    public static readonly Permission ResetTile = new Permission("tiles.reset", core: true);
    [Permission]
    public static readonly Permission ReplicateTileSplatmap = new Permission("tiles.replicate_splatmap", core: true);
    [Permission]
    public static readonly Permission EditHeightmap = new Permission("terrain.heightmap.edit", core: true);
    [Permission]
    public static readonly Permission EditSplatmap = new Permission("terrain.splatmap.edit", core: true);
    [Permission]
    public static readonly Permission EditHoles = new Permission("terrain.holemap.edit", core: true);
    [Permission]
    public static readonly Permission EditFoliage = new Permission("terrain.foliage.edit", core: true);
    [Permission]
    public static readonly Permission BakeFoliage = new Permission("terrain.foliage.bake", core: true);
    [Permission]
    public static readonly Permission BakeFoliageNearby = new Permission("terrain.foliage.bake.nearby", core: true);
    [Permission]
    public static readonly Permission EditRoads = new Permission("roads.edit", core: true);
    [Permission]
    public static readonly Permission EditRoadMaterials = new Permission("roads.materials.edit", core: true);
    [Permission]
    public static readonly Permission BakeNavigation = new Permission("navigation.bake", core: true);
    [Permission]
    public static readonly Permission MoveUnownedNavigation = new Permission("navigation.edit.transform", core: true);
    [Permission]
    public static readonly Permission RemoveUnownedNavigation = new Permission("navigation.edit.remove", core: true);
    [Permission]
    public static readonly Permission PlaceNavigation = new Permission("navigation.edit.place", core: true);
    [Permission]
    public static readonly Permission EditNavigation = new Permission("navigation.edit.*", core: true);
    [Permission]
    public static readonly Permission AllNavigation = new Permission("navigation.*", core: true);
    [Permission]
    public static readonly Permission EditLighting = new Permission("lighting.edit", core: true);
    [Permission]
    public static readonly Permission EditObjects = new Permission("hierarchy.objects.*", core: true);
    [Permission]
    public static readonly Permission PlaceObjects = new Permission("hierarchy.objects.place", core: true);
    [Permission]
    public static readonly Permission RemoveUnownedObjects = new Permission("hierarchy.objects.remove", core: true);
    [Permission]
    public static readonly Permission MoveUnownedObjects = new Permission("hierarchy.objects.transform", core: true);
    [Permission]
    public static readonly Permission PlaceVolumes = new Permission("hierarchy.volumes.place", core: true);
    [Permission]
    public static readonly Permission RemoveUnownedVolumes = new Permission("hierarchy.volumes.remove", core: true);
    [Permission]
    public static readonly Permission MoveUnownedVolumes = new Permission("hierarchy.volumes.transform", core: true);
    [Permission]
    public static readonly Permission EditVolumes = new Permission("hierarchy.volumes.*", core: true);
    [Permission]
    public static readonly Permission BakeCartography = new Permission("hierarchy.cartography.bake", core: true);
    [Permission]
    public static readonly Permission PlaceCartographyVolumes = new Permission("hierarchy.cartography.volumes.place", core: true);
    [Permission]
    public static readonly Permission RemoveUnownedCartographyVolumes = new Permission("hierarchy.cartography.remove", core: true);
    [Permission]
    public static readonly Permission MoveUnownedCartographyVolumes = new Permission("hierarchy.cartography.transform", core: true);
    [Permission]
    public static readonly Permission EditCartographyVolumes = new Permission("hierarchy.cartography.*", core: true);
    [Permission]
    public static readonly Permission PlaceNodes = new Permission("hierarchy.nodes.place", core: true);
    [Permission]
    public static readonly Permission RemoveUnownedNodes = new Permission("hierarchy.nodes.remove", core: true);
    [Permission]
    public static readonly Permission MoveUnownedNodes = new Permission("hierarchy.nodes.transform", core: true);
    [Permission]
    public static readonly Permission EditNodes = new Permission("hierarchy.nodes.*", core: true);

    [Permission]
    public static readonly Permission UsePlayerController = new Permission("controller.player", core: true);
    [Permission]
    public static readonly Permission UseEditorController = new Permission("controller.editor", core: true);
}
