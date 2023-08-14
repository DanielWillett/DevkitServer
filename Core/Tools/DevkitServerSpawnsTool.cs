#if CLIENT
using DevkitServer.Players.UI;
using DevkitServer.Util.Region;
using SDG.Framework.Devkit.Interactable;
#endif
namespace DevkitServer.Core.Tools;
#if CLIENT
/*
 * I decided to rewrite (copy) the spawns tool for a few reasons.
 *
 * 1. The spawn tools kinda suck.
 * 2. Because of the way they're set up internally, it's going to be very challenging to network them without instability.
 */
internal class DevkitServerSpawnsTool : DevkitServerSelectionTool
{
    private const byte AreaSelectRegionDistance = 1;
    public SpawnType Type { get; set; }
    public override bool CanAreaSelect => true;
    internal static void CheckExistingSpawnsForNodeComponents()
    {
        for (int i = 0; i < LevelAnimals.spawns.Count; i++)
        {
            AnimalSpawnpoint point = LevelAnimals.spawns[i];
            if (point.node != null && !point.node.TryGetComponent(out AnimalSpawnpointNode _))
                point.node.gameObject.AddComponent<AnimalSpawnpointNode>().Spawnpoint = point;
        }

        for (int i = 0; i < LevelVehicles.spawns.Count; i++)
        {
            VehicleSpawnpoint point = LevelVehicles.spawns[i];
            if (point.node != null && !point.node.TryGetComponent(out VehicleSpawnpointNode _))
                point.node.gameObject.AddComponent<VehicleSpawnpointNode>().Spawnpoint = point;
        }

        for (int i = 0; i < LevelPlayers.spawns.Count; i++)
        {
            PlayerSpawnpoint point = LevelPlayers.spawns[i];
            if (point.node != null && !point.node.TryGetComponent(out PlayerSpawnpointNode _))
                point.node.gameObject.AddComponent<PlayerSpawnpointNode>().Spawnpoint = point;
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
                    if (point.node != null && !point.node.TryGetComponent(out ItemSpawnpointNode _))
                        point.node.gameObject.AddComponent<ItemSpawnpointNode>().Spawnpoint = point;
                }

                List<ZombieSpawnpoint> zombieSpawnpoints = LevelZombies.spawns[x, y];
                for (int i = 0; i < zombieSpawnpoints.Count; i++)
                {
                    ZombieSpawnpoint point = zombieSpawnpoints[i];
                    if (point.node != null && !point.node.TryGetComponent(out ZombieSpawnpointNode _))
                        point.node.gameObject.AddComponent<ZombieSpawnpointNode>().Spawnpoint = point;
                }
            }
        }
    }

    protected override bool TryRaycastSelectableItems(in Ray ray, out RaycastHit hit)
    {
        Vector3 pos = Level.editing == null ? Vector3.zero : Level.editing.position;
        foreach (BaseSpawnpointNode spawn in EnumerateSpawns().OrderBy(x => (pos - x.transform.position).sqrMagnitude))
        {
            Logger.LogDebug($"Checking {spawn.transform?.name.Format()}.");
            if (spawn.Collider != null && spawn.Collider.Raycast(ray, out hit, 8192f))
            {
                return true;
            }
        }

        hit = default;
        return false;
    }

    public override void RequestInstantiation(Vector3 position, Quaternion rotation, Vector3 scale)
    {
        switch (Type)
        {
            // todo add translations
            case SpawnType.Animal:
                if (EditorSpawns.selectedAnimal >= LevelAnimals.tables.Count)
                {
                    UIMessage.SendEditorMessage("No animal selected");
                    return;
                }

                AnimalSpawnpoint animalSpawnpoint = new AnimalSpawnpoint(EditorSpawns.selectedAnimal, position);
                LevelAnimals.spawns.Add(animalSpawnpoint);
                if (animalSpawnpoint.node != null && !animalSpawnpoint.node.TryGetComponent(out AnimalSpawnpointNode _))
                    animalSpawnpoint.node.gameObject.AddComponent<AnimalSpawnpointNode>().Spawnpoint = animalSpawnpoint;
                break;
            case SpawnType.Vehicle:
                if (EditorSpawns.selectedVehicle >= LevelVehicles.tables.Count)
                {
                    UIMessage.SendEditorMessage("No vehicle selected");
                    return;
                }

                VehicleSpawnpoint vehicleSpawnpoint = new VehicleSpawnpoint(EditorSpawns.selectedVehicle, position, rotation.eulerAngles.y);
                LevelVehicles.spawns.Add(vehicleSpawnpoint);
                if (vehicleSpawnpoint.node != null && !vehicleSpawnpoint.node.TryGetComponent(out VehicleSpawnpointNode _))
                    vehicleSpawnpoint.node.gameObject.AddComponent<VehicleSpawnpointNode>().Spawnpoint = vehicleSpawnpoint;
                break;
            case SpawnType.Player:
                PlayerSpawnpoint playerSpawnpoint = new PlayerSpawnpoint(position, rotation.eulerAngles.y, EditorSpawns.selectedAlt);
                LevelPlayers.spawns.Add(playerSpawnpoint);
                if (playerSpawnpoint.node != null && !playerSpawnpoint.node.TryGetComponent(out PlayerSpawnpointNode _))
                    playerSpawnpoint.node.gameObject.AddComponent<PlayerSpawnpointNode>().Spawnpoint = playerSpawnpoint;
                break;
            case SpawnType.Item:
                if (EditorSpawns.selectedItem >= LevelItems.tables.Count)
                {
                    UIMessage.SendEditorMessage("No item selected");
                    return;
                }

                if (!Regions.tryGetCoordinate(position, out byte x, out byte y))
                {
                    UIMessage.SendEditorMessage("Out of bounds");
                    return;
                }

                ItemSpawnpoint itemSpawnpoint = new ItemSpawnpoint(EditorSpawns.selectedItem, position);
                LevelItems.spawns[x, y].Add(itemSpawnpoint);
                break;
            case SpawnType.Zombie:
                if (EditorSpawns.selectedZombie >= LevelZombies.tables.Count)
                {
                    UIMessage.SendEditorMessage("No zombie selected");
                    return;
                }

                if (!Regions.tryGetCoordinate(position, out x, out y))
                {
                    UIMessage.SendEditorMessage("Out of bounds");
                    return;
                }

                ZombieSpawnpoint zombieSpawnpoint = new ZombieSpawnpoint(EditorSpawns.selectedZombie, position);
                LevelZombies.spawns[x, y].Add(zombieSpawnpoint);
                break;
        }
    }

    protected IEnumerable<BaseSpawnpointNode> EnumerateSpawns() => Type switch
    {
        SpawnType.Animal => LevelAnimals.spawns.Select(x => x.node?.GetComponent<AnimalSpawnpointNode>()).Where(x => x != null)!,
        SpawnType.Vehicle => LevelVehicles.spawns.Select(x => x.node?.GetComponent<VehicleSpawnpointNode>()).Where(x => x != null)!,
        SpawnType.Player => LevelPlayers.spawns.Select(x => x.node?.GetComponent<PlayerSpawnpointNode>()).Where(x => x != null)!,
        SpawnType.Item => LevelItems.spawns.Cast<List<ItemSpawnpoint>>().SelectMany(DevkitServerUtility.IdentitySelector).Select(x => x.node?.GetComponent<ItemSpawnpointNode>()).Where(x => x != null)!,
        SpawnType.Zombie => LevelZombies.spawns.Cast<List<ZombieSpawnpoint>>().SelectMany(DevkitServerUtility.IdentitySelector).Select(x => x.node?.GetComponent<ZombieSpawnpointNode>()).Where(x => x != null)!,
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
        float distance = Regions.REGION_SIZE * AreaSelectRegionDistance;
        distance *= distance;
        Vector3 position = Editor.editor.transform.position;
        switch (Type)
        {
            case SpawnType.Animal:
                List<AnimalSpawnpoint> animalSpawns = LevelAnimals.spawns;
                for (int i = 0; i < animalSpawns.Count; ++i)
                {
                    AnimalSpawnpoint spawnPoint = animalSpawns[i];
                    if ((position - spawnPoint.point).sqrMagnitude <= distance && spawnPoint.node != null)
                        yield return spawnPoint.node.gameObject;
                }
                break;
            case SpawnType.Vehicle:
                List<VehicleSpawnpoint> vehicleSpawns = LevelVehicles.spawns;
                for (int i = 0; i < vehicleSpawns.Count; ++i)
                {
                    VehicleSpawnpoint spawnPoint = vehicleSpawns[i];
                    if ((position - spawnPoint.point).sqrMagnitude <= distance && spawnPoint.node != null)
                        yield return spawnPoint.node.gameObject;
                }
                break;
            case SpawnType.Player:
                List<PlayerSpawnpoint> playerSpawns = LevelPlayers.spawns;
                for (int i = 0; i < playerSpawns.Count; ++i)
                {
                    PlayerSpawnpoint spawnPoint = playerSpawns[i];
                    if ((position - spawnPoint.point).sqrMagnitude <= distance && spawnPoint.node != null)
                        yield return spawnPoint.node.gameObject;
                }
                break;
        }
    }
    private IEnumerable<GameObject> EnumerateRegions()
    {
        foreach (RegionCoord regionCoord in RegionUtil.EnumerateRegions(Editor.editor.area.region_x, Editor.editor.area.region_y, AreaSelectRegionDistance))
        {
            Logger.LogDebug($"Checking region for {Type.Format()} spawns: {regionCoord.Format()}.");
            if (Type == SpawnType.Zombie)
            {
                List<ZombieSpawnpoint> region = LevelZombies.spawns[regionCoord.x, regionCoord.y];
                for (int i = 0; i < region.Count; ++i)
                {
                    ZombieSpawnpoint sp = region[i];
                    if (sp.node == null) continue;
                    yield return sp.node.gameObject;
                }
            }
            else if (Type == SpawnType.Item)
            {
                List<ItemSpawnpoint> region = LevelItems.spawns[regionCoord.x, regionCoord.y];
                for (int i = 0; i < region.Count; ++i)
                {
                    ItemSpawnpoint sp = region[i];
                    if (sp.node == null) continue;
                    yield return sp.node.gameObject;
                }
            }
        }
    }
    protected override void OnPasted(GameObject newObject) { }
    protected override void InputTick() { }
}

public abstract class BaseSpawnpointNode : MonoBehaviour, IDevkitInteractableBeginSelectionHandler, IDevkitInteractableEndSelectionHandler
{
    public bool IsSelected { get; private set; }
    public bool IsAdded { get; set; } = true;
    public Collider Collider { get; protected set; } = null!;

    [UsedImplicitly]
    private void Start()
    {
        SetupCollider();
        
        if (Collider != null)
        {
            Collider.isTrigger = true;
            Collider.tag = "Logic";
        }
    }

    [UsedImplicitly]
    private void OnEnable()
    {
        if (IsAdded) return;
        IsAdded = Add();
    }

    [UsedImplicitly]
    private void OnDisable()
    {
        if (!IsAdded) return;
        Remove();
        IsAdded = false;
    }

    public void beginSelection(InteractionData data)
    {
        IsSelected = true;
    }
    public void endSelection(InteractionData data)
    {
        IsSelected = false;
    }
    protected virtual void SetupCollider()
    {
        Collider = transform.GetOrAddComponent<SphereCollider>();
        ((SphereCollider)Collider).radius = 2.25f;
    }
    protected abstract bool Add();
    protected abstract bool Remove();
}
public class AnimalSpawnpointNode : BaseSpawnpointNode
{
    public AnimalSpawnpoint Spawnpoint { get; internal set; } = null!;
    protected override void SetupCollider()
    {
        BoxCollider collider = transform.GetOrAddComponent<BoxCollider>();
        Collider = collider;
        collider.size = new Vector3(2f, 2f, 2f);
        collider.center = new Vector3(0f, 1f, 0f);
    }
    protected override bool Add()
    {
        LevelAnimals.spawns.Add(Spawnpoint);
        return true;
    }
    protected override bool Remove()
    {
        return LevelAnimals.spawns.Remove(Spawnpoint);
    }
}
public class VehicleSpawnpointNode : BaseSpawnpointNode
{
    public VehicleSpawnpoint Spawnpoint { get; internal set; } = null!;
    protected override void SetupCollider()
    {
        BoxCollider collider = transform.GetOrAddComponent<BoxCollider>();
        Collider = collider;
        collider.size = new Vector3(3f, 2f, 4f);
        collider.center = new Vector3(0f, 1f, 0f);
    }
    protected override bool Add()
    {
        LevelVehicles.spawns.Add(Spawnpoint);
        return true;
    }
    protected override bool Remove()
    {
        return LevelVehicles.spawns.Remove(Spawnpoint);
    }
}
public class PlayerSpawnpointNode : BaseSpawnpointNode
{
    public PlayerSpawnpoint Spawnpoint { get; internal set; } = null!;
    protected override void SetupCollider()
    {
        BoxCollider collider = transform.GetOrAddComponent<BoxCollider>();
        Collider = collider;
        collider.size = new Vector3(2.25f, 2f, 0.35f);
        collider.center = new Vector3(0f, 1f, 0f);
    }

    protected override bool Add()
    {
        LevelPlayers.spawns.Add(Spawnpoint);
        return true;
    }
    protected override bool Remove()
    {
        return LevelPlayers.spawns.Remove(Spawnpoint);
    }
}
public class ZombieSpawnpointNode : BaseSpawnpointNode
{
    public ZombieSpawnpoint Spawnpoint { get; internal set; } = null!;
    protected override void SetupCollider()
    {
        BoxCollider collider = transform.GetOrAddComponent<BoxCollider>();
        Collider = collider;
        collider.size = new Vector3(0.5f, 2f, 0.5f);
        collider.center = new Vector3(0f, 1f, 0f);
    }
    protected override bool Add()
    {
        if (Regions.tryGetCoordinate(Spawnpoint.point, out byte x, out byte y))
        {
            LevelZombies.spawns[x, y].Add(Spawnpoint);
            return true;
        }

        return false;
    }
    protected override bool Remove()
    {
        if (Regions.tryGetCoordinate(Spawnpoint.point, out byte x, out byte y))
        {
            if (LevelZombies.spawns[x, y].Remove(Spawnpoint))
                return true;
        }

        int worldSize = Regions.WORLD_SIZE;

        for (int x2 = 0; x2 < worldSize; ++x2)
        {
            for (int y2 = 0; y2 < worldSize; ++y2)
            {
                if (LevelZombies.spawns[x2, y2].Remove(Spawnpoint))
                    return true;
            }
        }

        return false;
    }
}
public class ItemSpawnpointNode : BaseSpawnpointNode
{
    public ItemSpawnpoint Spawnpoint { get; internal set; } = null!;
    protected override void SetupCollider()
    {
        BoxCollider collider = transform.GetOrAddComponent<BoxCollider>();
        Collider = collider;
        collider.size = new Vector3(0.5f, 0.5f, 0.5f);
        collider.center = new Vector3(0f, 0f, 0f);
    }
    protected override bool Add()
    {
        if (Regions.tryGetCoordinate(Spawnpoint.point, out byte x, out byte y))
        {
            LevelItems.spawns[x, y].Add(Spawnpoint);
            return true;
        }

        return false;
    }
    protected override bool Remove()
    {
        if (Regions.tryGetCoordinate(Spawnpoint.point, out byte x, out byte y))
        {
            if (LevelItems.spawns[x, y].Remove(Spawnpoint))
                return true;
        }

        int worldSize = Regions.WORLD_SIZE;

        for (int x2 = 0; x2 < worldSize; ++x2)
        {
            for (int y2 = 0; y2 < worldSize; ++y2)
            {
                if (LevelItems.spawns[x2, y2].Remove(Spawnpoint))
                    return true;
            }
        }

        return false;
    }
}
#endif

public enum SpawnType
{
    None,
    Player,
    Animal,
    Zombie,
    Item,
    Vehicle
}