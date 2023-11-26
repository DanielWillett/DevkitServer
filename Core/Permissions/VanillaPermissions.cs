using DevkitServer.API.Permissions;

namespace DevkitServer.Core.Permissions;
public static class VanillaPermissions
{
    [Permission]
    public static readonly PermissionLeaf EditTerrain = new PermissionLeaf("terrain.*", core: true);
    [Permission]
    public static readonly PermissionLeaf EditTiles = new PermissionLeaf("tiles.*", core: true);
    [Permission]
    public static readonly PermissionLeaf AddTile = new PermissionLeaf("tiles.add", core: true);
    [Permission]
    public static readonly PermissionLeaf DeleteTile = new PermissionLeaf("tiles.delete", core: true);
    [Permission]
    public static readonly PermissionLeaf ResetTile = new PermissionLeaf("tiles.reset", core: true);
    [Permission]
    public static readonly PermissionLeaf ReplicateTileSplatmap = new PermissionLeaf("tiles.replicate_splatmap", core: true);
    [Permission]
    public static readonly PermissionLeaf EditHeightmap = new PermissionLeaf("terrain.heightmap.edit", core: true);
    [Permission]
    public static readonly PermissionLeaf EditSplatmap = new PermissionLeaf("terrain.splatmap.edit", core: true);
    [Permission]
    public static readonly PermissionLeaf EditHoles = new PermissionLeaf("terrain.holemap.edit", core: true);
    [Permission]
    public static readonly PermissionLeaf EditFoliage = new PermissionLeaf("terrain.foliage.edit", core: true);
    [Permission]
    public static readonly PermissionLeaf BakeFoliage = new PermissionLeaf("terrain.foliage.bake", core: true);
    [Permission]
    public static readonly PermissionLeaf BakeFoliageNearby = new PermissionLeaf("terrain.foliage.bake.nearby", core: true);
    [Permission]
    public static readonly PermissionLeaf EditRoads = new PermissionLeaf("roads.edit", core: true);
    [Permission]
    public static readonly PermissionLeaf EditRoadMaterials = new PermissionLeaf("roads.materials.edit", core: true);
    [Permission]
    public static readonly PermissionLeaf BakeNavigation = new PermissionLeaf("navigation.bake", core: true);
    [Permission]
    public static readonly PermissionLeaf EditNavigation = new PermissionLeaf("navigation.edit", core: true);
    [Permission]
    public static readonly PermissionLeaf AllNavigation = new PermissionLeaf("navigation.*", core: true);
    [Permission]
    public static readonly PermissionLeaf EditLighting = new PermissionLeaf("lighting.edit", core: true);
    [Permission]
    public static readonly PermissionLeaf EditObjects = new PermissionLeaf("hierarchy.objects.*", core: true);
    [Permission]
    public static readonly PermissionLeaf PlaceObjects = new PermissionLeaf("hierarchy.objects.place", core: true);
    [Permission]
    public static readonly PermissionLeaf RemoveUnownedObjects = new PermissionLeaf("hierarchy.objects.remove", core: true);
    [Permission]
    public static readonly PermissionLeaf MoveUnownedObjects = new PermissionLeaf("hierarchy.objects.transform", core: true);
    [Permission]
    public static readonly PermissionLeaf PlaceVolumes = new PermissionLeaf("hierarchy.volumes.place", core: true);
    [Permission]
    public static readonly PermissionLeaf RemoveUnownedVolumes = new PermissionLeaf("hierarchy.volumes.remove", core: true);
    [Permission]
    public static readonly PermissionLeaf MoveUnownedVolumes = new PermissionLeaf("hierarchy.volumes.transform", core: true);
    [Permission]
    public static readonly PermissionLeaf EditVolumes = new PermissionLeaf("hierarchy.volumes.*", core: true);
    [Permission]
    public static readonly PermissionLeaf BakeCartography = new PermissionLeaf("hierarchy.cartography.bake", core: true);
    [Permission]
    public static readonly PermissionLeaf PlaceCartographyVolumes = new PermissionLeaf("hierarchy.cartography.volumes.place", core: true);
    [Permission]
    public static readonly PermissionLeaf RemoveUnownedCartographyVolumes = new PermissionLeaf("hierarchy.cartography.remove", core: true);
    [Permission]
    public static readonly PermissionLeaf MoveUnownedCartographyVolumes = new PermissionLeaf("hierarchy.cartography.transform", core: true);
    [Permission]
    public static readonly PermissionLeaf EditCartographyVolumes = new PermissionLeaf("hierarchy.cartography.*", core: true);
    [Permission]
    public static readonly PermissionLeaf PlaceNodes = new PermissionLeaf("hierarchy.nodes.place", core: true);
    [Permission]
    public static readonly PermissionLeaf RemoveUnownedNodes = new PermissionLeaf("hierarchy.nodes.remove", core: true);
    [Permission]
    public static readonly PermissionLeaf MoveUnownedNodes = new PermissionLeaf("hierarchy.nodes.transform", core: true);
    [Permission]
    public static readonly PermissionLeaf EditNodes = new PermissionLeaf("hierarchy.nodes.*", core: true);

    [Permission]
    public static readonly PermissionLeaf UsePlayerController = new PermissionLeaf("controller.player", core: true);
    [Permission]
    public static readonly PermissionLeaf UseEditorController = new PermissionLeaf("controller.editor", core: true);
}
