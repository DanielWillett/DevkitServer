#if CLIENT
using DevkitServer.API.Devkit.Spawns;
using DevkitServer.API.Iterators;
using DevkitServer.API.UI;
using DevkitServer.API.UI.Extensions;
using DevkitServer.Core.UI.Extensions;
using DevkitServer.Models;
using DevkitServer.Util.Region;
using SDG.Framework.Devkit;

namespace DevkitServer.Core.Tools;
/*
 * I decided to rewrite the spawns tool for a few reasons.
 *
 * 1. The spawn tools kinda suck.
 * 2. Because of the way they're set up internally, it's going to be very challenging to network them without instability.
 */
public class DevkitServerSpawnsTool : DevkitServerSelectionTool
{
    private SpawnType _type;
    private const byte AreaSelectRegionDistance = 1;
    public bool IsSpawnTypeSelected { get; private set; }
    public SpawnType Type
    {
        get => _type;
        set
        {
            _type = value;
            IsSpawnTypeSelected = value is SpawnType.Animal or SpawnType.Item or SpawnType.Player or SpawnType.Vehicle or SpawnType.Zombie;
            CanAreaSelect = IsSpawnTypeSelected;
            CanRotate = value is SpawnType.Player or SpawnType.Vehicle;
            CanMiddleClickPick = value is not SpawnType.Player;
        }
    }

    public DevkitServerSpawnsTool()
    {
        CanTranslate = true;
        CanScale = false;
        RootSelections = true;
        Type = SpawnType.None;
    }

    protected override void OnMiddleClickPicked(ref RaycastHit hit)
    {
        if (!hit.transform.TryGetComponent(out BaseSpawnpointNode node))
            return;

        switch (node)
        {
            case AnimalSpawnpointNode animal:
                SpawnTableUtil.SelectAnimalTable(animal.Spawnpoint.type, true, true);
                break;
            case VehicleSpawnpointNode vehicle:
                SpawnTableUtil.SelectVehicleTable(vehicle.Spawnpoint.type, true, true);
                break;
            case ItemSpawnpointNode item:
                SpawnTableUtil.SelectItemTable(item.Spawnpoint.type, true, true);
                break;
            case ZombieSpawnpointNode zombie:
                SpawnTableUtil.SelectZombieTable(zombie.Spawnpoint.type, true, true);
                break;
        }
    }

    internal static void CheckExistingSpawnsForNodeComponents()
    {
        for (int i = 0; i < LevelAnimals.spawns.Count; i++)
        {
            AnimalSpawnpoint point = LevelAnimals.spawns[i];
            if (point.node == null)
                continue;
            if (!point.node.TryGetComponent(out AnimalSpawnpointNode node))
                node = point.node.gameObject.AddComponent<AnimalSpawnpointNode>();
            node.Spawnpoint = point;
            node.Index = i;
            SpawnUtil.SetNodeShaders(point.node);
        }

        for (int i = 0; i < LevelVehicles.spawns.Count; i++)
        {
            VehicleSpawnpoint point = LevelVehicles.spawns[i];
            if (point.node == null)
                continue;
            if (!point.node.TryGetComponent(out VehicleSpawnpointNode node))
                node = point.node.gameObject.AddComponent<VehicleSpawnpointNode>();
            node.Spawnpoint = point;
            node.Index = i;
            SpawnUtil.SetNodeShaders(point.node);
        }

        for (int i = 0; i < LevelPlayers.spawns.Count; i++)
        {
            PlayerSpawnpoint point = LevelPlayers.spawns[i];
            if (point.node == null)
                continue;
            if (!point.node.TryGetComponent(out PlayerSpawnpointNode node))
                node = point.node.gameObject.AddComponent<PlayerSpawnpointNode>();
            node.Spawnpoint = point;
            node.Index = i;
            SpawnUtil.SetNodeShaders(point.node);
        }

        int worldSize = Regions.WORLD_SIZE;
        for (int x = 0; x < worldSize; ++x)
        {
            for (int y = 0; y < worldSize; ++y)
            {
                List<ItemSpawnpoint> itemSpawnpoints = LevelItems.spawns[x, y];
                for (int i = 0; i < itemSpawnpoints.Count; i++)
                {
                    ItemSpawnpoint point = itemSpawnpoints[i];
                    if (point.node == null)
                        continue;
                    if (!point.node.TryGetComponent(out ItemSpawnpointNode node))
                        node = point.node.gameObject.AddComponent<ItemSpawnpointNode>();
                    node.Spawnpoint = point;
                    node.Region = new RegionIdentifier(x, y, i);
                    SpawnUtil.SetNodeShaders(point.node);
                }

                List<ZombieSpawnpoint> zombieSpawnpoints = LevelZombies.spawns[x, y];
                for (int i = 0; i < zombieSpawnpoints.Count; i++)
                {
                    ZombieSpawnpoint point = zombieSpawnpoints[i];
                    if (point.node == null)
                        continue;
                    if (!point.node.TryGetComponent(out ZombieSpawnpointNode node))
                        node = point.node.gameObject.AddComponent<ZombieSpawnpointNode>();
                    node.Spawnpoint = point;
                    node.Region = new RegionIdentifier(x, y, i);
                    SpawnUtil.SetNodeShaders(point.node);
                }
            }
        }
    }

    protected override bool TryRaycastSelectableItems(ref Ray ray, out RaycastHit hit)
    {
        return Physics.Raycast(ray, out hit, 8192f, 1 << 3, QueryTriggerInteraction.Collide);
    }
    public override void RequestInstantiation(Vector3 position, Quaternion rotation, Vector3 scale)
    {
        switch (Type)
        {
            // todo add translations
            case SpawnType.Animal:
                if (EditorSpawns.selectedAnimal >= LevelAnimals.tables.Count)
                {
                    EditorMessage.SendEditorMessage("No animal selected");
                    return;
                }

                AnimalSpawnpoint animalSpawnpoint = new AnimalSpawnpoint(EditorSpawns.selectedAnimal, position);
                SpawnUtil.AddAnimalSpawnLocal(animalSpawnpoint);
                break;
            case SpawnType.Vehicle:
                if (EditorSpawns.selectedVehicle >= LevelVehicles.tables.Count)
                {
                    EditorMessage.SendEditorMessage("No vehicle selected");
                    return;
                }

                VehicleSpawnpoint vehicleSpawnpoint = new VehicleSpawnpoint(EditorSpawns.selectedVehicle, position, rotation.eulerAngles.y);
                SpawnUtil.AddVehicleSpawnLocal(vehicleSpawnpoint);
                break;
            case SpawnType.Player:
                PlayerSpawnpoint playerSpawnpoint = new PlayerSpawnpoint(position, rotation.eulerAngles.y, EditorSpawns.selectedAlt);
                SpawnUtil.AddPlayerSpawnLocal(playerSpawnpoint);
                break;
            case SpawnType.Item:
                if (EditorSpawns.selectedItem >= LevelItems.tables.Count)
                {
                    EditorMessage.SendEditorMessage("No item selected");
                    return;
                }

                if (!Regions.checkSafe(position))
                {
                    EditorMessage.SendEditorMessage("Out of bounds");
                    return;
                }

                ItemSpawnpoint itemSpawnpoint = new ItemSpawnpoint(EditorSpawns.selectedItem, position);
                SpawnUtil.AddItemSpawnLocal(itemSpawnpoint);
                break;
            case SpawnType.Zombie:
                if (EditorSpawns.selectedZombie >= LevelZombies.tables.Count)
                {
                    EditorMessage.SendEditorMessage("No zombie selected");
                    return;
                }

                if (!Regions.checkSafe(position))
                {
                    EditorMessage.SendEditorMessage("Out of bounds");
                    return;
                }

                ZombieSpawnpoint zombieSpawnpoint = new ZombieSpawnpoint(EditorSpawns.selectedZombie, position);
                SpawnUtil.AddZombieSpawnLocal(zombieSpawnpoint);
                break;
        }
    }
    protected IEnumerable<BaseSpawnpointNode> EnumerateSpawns() => Type switch
    {
        SpawnType.Animal => LevelAnimals.spawns.Select(x => x.node?.GetComponent<AnimalSpawnpointNode>()).Where(x => x != null)!,
        SpawnType.Vehicle => LevelVehicles.spawns.Select(x => x.node?.GetComponent<VehicleSpawnpointNode>()).Where(x => x != null)!,
        SpawnType.Player => LevelPlayers.spawns.Select(x => x.node?.GetComponent<PlayerSpawnpointNode>()).Where(x => x != null)!,
        SpawnType.Item => LevelItems.spawns.CastFrom(Level.isEditor ? MainCamera.instance.transform.parent.position : Vector3.zero)
            .Select(x => x.node?.GetComponent<ItemSpawnpointNode>()).Where(x => x != null)!,
        SpawnType.Zombie => LevelZombies.spawns.CastFrom(Level.isEditor ? MainCamera.instance.transform.parent.position : Vector3.zero)
            .Select(x => x.node?.GetComponent<ZombieSpawnpointNode>()).Where(x => x != null)!,
        _ => Array.Empty<BaseSpawnpointNode>()
    };
    protected override IEnumerable<GameObject> EnumerateAreaSelectableObjects()
    {
        if (!Level.isEditor)
            return Array.Empty<GameObject>();

        return Type is SpawnType.Item or SpawnType.Zombie ? EnumerateRegions() : EnumerateDistance();
    }
    private IEnumerable<GameObject> EnumerateDistance()
    {
        float distance = Regions.REGION_SIZE * (AreaSelectRegionDistance + 0.5f);
        distance *= distance;
        Vector3 position = Editor.editor.transform.position;
        return Type switch
        {
            SpawnType.Animal => new DistanceListIterator<AnimalSpawnpoint>(LevelAnimals.spawns, x => x.point, Editor.editor.transform.position, useXZAxisOnly: true)
                .Select(x => x.node == null ? null! : x.node.gameObject)
                .Where(x => x != null)
                .TakeWhile(x => x.gameObject.transform.position.SqrDist2D(in position) <= distance),

            SpawnType.Vehicle => new DistanceListIterator<VehicleSpawnpoint>(LevelVehicles.spawns, x => x.point, Editor.editor.transform.position, useXZAxisOnly: true)
                .Select(x => x.node == null ? null! : x.node.gameObject)
                .Where(x => x != null)
                .TakeWhile(x => x.gameObject.transform.position.SqrDist2D(in position) <= distance),

            SpawnType.Player => new DistanceListIterator<PlayerSpawnpoint>(LevelPlayers.spawns, x => x.point, Editor.editor.transform.position, useXZAxisOnly: true)
                .Select(x => x.node == null ? null! : x.node.gameObject)
                .Where(x => x != null)
                .TakeWhile(x => x.gameObject.transform.position.SqrDist2D(in position) <= distance),

            _ => Array.Empty<GameObject>()
        };
    }
    private IEnumerable<GameObject> EnumerateRegions()
    {
        if (Type == SpawnType.Item)
        {
            return new ListRegionsEnumerator<ItemSpawnpoint>(LevelItems.spawns, Editor.editor.area.region_x, Editor.editor.area.region_y, AreaSelectRegionDistance)
                .Select(x => x.node == null ? null : x.node.gameObject).Where(x => x != null)!;
        }
        
        if (Type == SpawnType.Zombie)
        {
            return new ListRegionsEnumerator<ZombieSpawnpoint>(LevelZombies.spawns, Editor.editor.area.region_x, Editor.editor.area.region_y, AreaSelectRegionDistance)
                .Select(x => x.node == null ? null : x.node.gameObject).Where(x => x != null)!;
        }
        
        return Array.Empty<GameObject>();
    }
    protected override void OnTempMoved()
    {
        foreach (DevkitSelection selection in DevkitSelectionManager.selection)
        {
            if (!selection.gameObject.TryGetComponent(out BaseSpawnpointNode spawnpointNode))
                continue;

            switch (spawnpointNode)
            {
                case AnimalSpawnpointNode a:
                    UIExtensionManager.GetInstance<EditorSpawnsAnimalsUIExtension>()?
                        .OnSpawnMoved(a.Spawnpoint, default, default);
                    break;
                case ItemSpawnpointNode i:
                    UIExtensionManager.GetInstance<EditorSpawnsItemsUIExtension>()?
                        .OnSpawnMoved(i.Spawnpoint, default, default, default);
                    break;
                case ZombieSpawnpointNode z:
                    UIExtensionManager.GetInstance<EditorSpawnsZombiesUIExtension>()?
                        .OnSpawnMoved(z.Spawnpoint, default, default, default);
                    break;
                case VehicleSpawnpointNode v:
                    UIExtensionManager.GetInstance<EditorSpawnsVehiclesUIExtension>()?
                        .OnSpawnMoved(v.Spawnpoint, default, default, default, default);
                    break;
            }
        }
    }
}
#endif