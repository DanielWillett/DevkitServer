using DevkitServer.API.Permissions;

namespace DevkitServer.Core.Permissions;
/// <summary>
/// Contains default permissions for vanilla editor actions.
/// </summary>
public static class VanillaPermissions
{
    private static readonly Dictionary<Type, PermissionLeaf> PermissionsPlace  = new Dictionary<Type, PermissionLeaf>(32);
    private static readonly Dictionary<Type, PermissionLeaf> PermissionsRemove = new Dictionary<Type, PermissionLeaf>(32);
    private static readonly Dictionary<Type, PermissionLeaf> PermissionsMove   = new Dictionary<Type, PermissionLeaf>(32);

    public static readonly PermissionLeaf EditHeightmap             = new PermissionLeaf("level.terrain.heightmap", core: true);
    public static readonly PermissionLeaf EditSplatmap              = new PermissionLeaf("level.terrain.splatmap", core: true);
    public static readonly PermissionLeaf EditHoles                 = new PermissionLeaf("level.terrain.holemap", core: true);
    public static readonly PermissionLeaf EditTiles                 = new PermissionLeaf("level.terrain.tiles.edit", core: true);
    public static readonly PermissionLeaf AddTiles                  = new PermissionLeaf("level.terrain.tiles.add", core: true);
    public static readonly PermissionLeaf DeleteTiles               = new PermissionLeaf("level.terrain.tiles.delete", core: true);
    public static readonly PermissionLeaf ResetTiles                = new PermissionLeaf("level.terrain.tiles.reset", core: true);
    public static readonly PermissionLeaf EditTileMaterials         = new PermissionLeaf("level.terrain.tiles.materials", core: true);
    public static readonly PermissionLeaf EditFoliage               = new PermissionLeaf("level.terrain.foliage.edit", core: true);
    public static readonly PermissionLeaf BakeFoliage               = new PermissionLeaf("level.terrain.foliage.edit.bake.all", core: true);
    public static readonly PermissionLeaf BakeFoliageNearby         = new PermissionLeaf("level.terrain.foliage.edit.bake.nearby", core: true);
    public static readonly PermissionLeaf EditRoads                 = new PermissionLeaf("level.roads.paths.edit", core: true);
    public static readonly PermissionLeaf EditRoadMaterials         = new PermissionLeaf("level.roads.materials.edit", core: true);
    public static readonly PermissionLeaf BakeNavigation            = new PermissionLeaf("level.navigation.edit.bake", core: true);
    public static readonly PermissionLeaf EditNavigation            = new PermissionLeaf("level.navigation.edit", core: true);
    public static readonly PermissionLeaf PlaceObjects              = new PermissionLeaf("level.objects.place", core: true);
    public static readonly PermissionLeaf RemoveUnownedObjects      = new PermissionLeaf("level.objects.remove", core: true);
    public static readonly PermissionLeaf MoveUnownedObjects        = new PermissionLeaf("level.objects.transform", core: true);
    public static readonly PermissionLeaf BakeCartographyGPS        = new PermissionLeaf("level.cartography.bake.gps", core: true);
    public static readonly PermissionLeaf BakeCartographyChart      = new PermissionLeaf("level.cartography.bake.chart", core: true);
    public static readonly PermissionLeaf UsePlayerController       = new PermissionLeaf("input.controller.player", core: true);
    public static readonly PermissionLeaf UseEditorController       = new PermissionLeaf("input.controller.editor", core: true);

    public static readonly PermissionLeaf SpawnTablesAnimalEdit     = new PermissionLeaf("level.spawntables.animal.edit", core: true);
    public static readonly PermissionLeaf SpawnTablesAnimalAdd      = new PermissionLeaf("level.spawntables.animal.add", core: true);
    public static readonly PermissionLeaf SpawnTablesAnimalDelete   = new PermissionLeaf("level.spawntables.animal.delete", core: true);
    public static readonly PermissionLeaf SpawnTablesVehicleEdit    = new PermissionLeaf("level.spawntables.vehicle.edit", core: true);
    public static readonly PermissionLeaf SpawnTablesVehicleAdd     = new PermissionLeaf("level.spawntables.vehicle.add", core: true);
    public static readonly PermissionLeaf SpawnTablesVehicleDelete  = new PermissionLeaf("level.spawntables.vehicle.delete", core: true);
    public static readonly PermissionLeaf SpawnTablesZombieEdit     = new PermissionLeaf("level.spawntables.zombie.edit", core: true);
    public static readonly PermissionLeaf SpawnTablesZombieAdd      = new PermissionLeaf("level.spawntables.zombie.add", core: true);
    public static readonly PermissionLeaf SpawnTablesZombieDelete   = new PermissionLeaf("level.spawntables.zombie.delete", core: true);
    public static readonly PermissionLeaf SpawnTablesItemEdit       = new PermissionLeaf("level.spawntables.item.edit", core: true);
    public static readonly PermissionLeaf SpawnTablesItemAdd        = new PermissionLeaf("level.spawntables.item.edit", core: true);
    public static readonly PermissionLeaf SpawnTablesItemDelete     = new PermissionLeaf("level.spawntables.item.edit", core: true);

    public static readonly PermissionLeaf SpawnsAnimalMove          = new PermissionLeaf("level.spawns.animal.move", core: true);
    public static readonly PermissionLeaf SpawnsAnimalAdd           = new PermissionLeaf("level.spawns.animal.add", core: true);
    public static readonly PermissionLeaf SpawnsAnimalDelete        = new PermissionLeaf("level.spawns.animal.delete", core: true);
    public static readonly PermissionLeaf SpawnsVehicleMove         = new PermissionLeaf("level.spawns.vehicle.move", core: true);
    public static readonly PermissionLeaf SpawnsVehicleAdd          = new PermissionLeaf("level.spawns.vehicle.add", core: true);
    public static readonly PermissionLeaf SpawnsVehicleDelete       = new PermissionLeaf("level.spawns.vehicle.delete", core: true);
    public static readonly PermissionLeaf SpawnsPlayerEdit          = new PermissionLeaf("level.spawns.player.edit", core: true);
    public static readonly PermissionLeaf SpawnsPlayerMove          = new PermissionLeaf("level.spawns.player.move", core: true);
    public static readonly PermissionLeaf SpawnsPlayerAdd           = new PermissionLeaf("level.spawns.player.add", core: true);
    public static readonly PermissionLeaf SpawnsPlayerDelete        = new PermissionLeaf("level.spawns.player.delete", core: true);
    public static readonly PermissionLeaf SpawnsItemMove            = new PermissionLeaf("level.spawns.item.move", core: true);
    public static readonly PermissionLeaf SpawnsItemAdd             = new PermissionLeaf("level.spawns.item.add", core: true);
    public static readonly PermissionLeaf SpawnsItemDelete          = new PermissionLeaf("level.spawns.item.delete", core: true);
    public static readonly PermissionLeaf SpawnsZombieMove          = new PermissionLeaf("level.spawns.zombie.move", core: true);
    public static readonly PermissionLeaf SpawnsZombieAdd           = new PermissionLeaf("level.spawns.zombie.add", core: true);
    public static readonly PermissionLeaf SpawnsZombieDelete        = new PermissionLeaf("level.spawns.zombie.delete", core: true);

    public static PermissionLeaf GetNodeVolumePlace(Type type)
    {
        lock (PermissionsPlace)
        {
            if (PermissionsPlace.TryGetValue(type, out PermissionLeaf leaf))
                return leaf;

            leaf = GetVolumeNodePermission(type, "place");
            PermissionsPlace.Add(type, leaf);

            return leaf;
        }
    }
    public static PermissionLeaf GetNodeVolumeRemove(Type type)
    {
        lock (PermissionsRemove)
        {
            if (PermissionsRemove.TryGetValue(type, out PermissionLeaf leaf))
                return leaf;

            leaf = GetVolumeNodePermission(type, "remove");
            PermissionsRemove.Add(type, leaf);

            return leaf;
        }
    }
    public static PermissionLeaf GetNodeVolumeMove(Type type)
    {
        lock (PermissionsMove)
        {
            if (PermissionsMove.TryGetValue(type, out PermissionLeaf leaf))
                return leaf;

            leaf = GetVolumeNodePermission(type, "transform");
            PermissionsMove.Add(type, leaf);

            return leaf;
        }
    }

    public static class VolumePermission<T> where T : VolumeBase
    {
        public static readonly PermissionLeaf Place;
        public static readonly PermissionLeaf Remove;
        public static readonly PermissionLeaf Move;

        static VolumePermission()
        {
            Type type = typeof(T);
            Place = GetNodeVolumePlace(type);
            Remove = GetNodeVolumeRemove(type);
            Move = GetNodeVolumeMove(type);
        }
    }
    public static class NodePermission<T> where T : TempNodeBase
    {
        public static readonly PermissionLeaf Place;
        public static readonly PermissionLeaf Remove;
        public static readonly PermissionLeaf Move;
        static NodePermission()
        {
            Type type = typeof(T);
            Place = GetNodeVolumePlace(type);
            Remove = GetNodeVolumeRemove(type);
            Move = GetNodeVolumeMove(type);
        }
    }

    public static PermissionLeaf GetVolumeNodePermission(Type type, string leaf)
    {
        bool node = typeof(TempNodeBase).IsAssignableFrom(type);
        string name = type.Name;
        if (!node && name.EndsWith("Volume", StringComparison.Ordinal))
            name = name[..^6];
        else if (node && name.EndsWith("Node", StringComparison.Ordinal))
            name = name[..^4];
        name = FormattingUtil.SpaceProperCaseString(name, '_').ToLowerInvariant();
        return new PermissionLeaf("level.hierarchy." + (node ? "nodes" : "volumes") + "." + name + "." + leaf, core: true);
    }



}
