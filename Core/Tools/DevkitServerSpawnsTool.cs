#if CLIENT
using DevkitServer.API;
using DevkitServer.Models;
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
        }
    }

    public DevkitServerSpawnsTool()
    {
        CanTranslate = true;
        CanScale = false;
        Type = SpawnType.None;
    }
    
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
        foreach (BaseSpawnpointNode spawn in EnumerateSpawns())
        {
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
                SpawnUtil.AddAnimalSpawn(animalSpawnpoint);
                break;
            case SpawnType.Vehicle:
                if (EditorSpawns.selectedVehicle >= LevelVehicles.tables.Count)
                {
                    UIMessage.SendEditorMessage("No vehicle selected");
                    return;
                }

                VehicleSpawnpoint vehicleSpawnpoint = new VehicleSpawnpoint(EditorSpawns.selectedVehicle, position, rotation.eulerAngles.y);
                SpawnUtil.AddVehicleSpawn(vehicleSpawnpoint);
                break;
            case SpawnType.Player:
                PlayerSpawnpoint playerSpawnpoint = new PlayerSpawnpoint(position, rotation.eulerAngles.y, EditorSpawns.selectedAlt);
                SpawnUtil.AddPlayerSpawn(playerSpawnpoint);
                break;
            case SpawnType.Item:
                if (EditorSpawns.selectedItem >= LevelItems.tables.Count)
                {
                    UIMessage.SendEditorMessage("No item selected");
                    return;
                }

                if (!Regions.checkSafe(position))
                {
                    UIMessage.SendEditorMessage("Out of bounds");
                    return;
                }

                ItemSpawnpoint itemSpawnpoint = new ItemSpawnpoint(EditorSpawns.selectedItem, position);
                SpawnUtil.AddItemSpawn(itemSpawnpoint);
                break;
            case SpawnType.Zombie:
                if (EditorSpawns.selectedZombie >= LevelZombies.tables.Count)
                {
                    UIMessage.SendEditorMessage("No zombie selected");
                    return;
                }

                if (!Regions.checkSafe(position))
                {
                    UIMessage.SendEditorMessage("Out of bounds");
                    return;
                }

                ZombieSpawnpoint zombieSpawnpoint = new ZombieSpawnpoint(EditorSpawns.selectedZombie, position);
                SpawnUtil.AddZombieSpawn(zombieSpawnpoint);
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
        switch (Type)
        {
            case SpawnType.Animal:
                List<AnimalSpawnpoint> animalSpawns = LevelAnimals.spawns;
                for (int i = 0; i < animalSpawns.Count; ++i)
                {
                    AnimalSpawnpoint spawnPoint = animalSpawns[i];
                    if (position.SqrDist2D(spawnPoint.point) <= distance && spawnPoint.node != null)
                        yield return spawnPoint.node.gameObject;
                }
                break;
            case SpawnType.Vehicle:
                List<VehicleSpawnpoint> vehicleSpawns = LevelVehicles.spawns;
                for (int i = 0; i < vehicleSpawns.Count; ++i)
                {
                    VehicleSpawnpoint spawnPoint = vehicleSpawns[i];
                    if (position.SqrDist2D(spawnPoint.point) <= distance && spawnPoint.node != null)
                        yield return spawnPoint.node.gameObject;
                }
                break;
            case SpawnType.Player:
                List<PlayerSpawnpoint> playerSpawns = LevelPlayers.spawns;
                for (int i = 0; i < playerSpawns.Count; ++i)
                {
                    PlayerSpawnpoint spawnPoint = playerSpawns[i];
                    if (position.SqrDist2D(spawnPoint.point) <= distance && spawnPoint.node != null)
                        yield return spawnPoint.node.gameObject;
                }
                break;
        }
    }
    private IEnumerable<GameObject> EnumerateRegions()
    {
        foreach (RegionCoord regionCoord in RegionUtil.EnumerateRegions(Editor.editor.area.region_x, Editor.editor.area.region_y, AreaSelectRegionDistance))
        {
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
    protected override void InputTick()
    {
        if (!InputEx.GetKeyDown(KeyCode.F8))
            return;
        
        int ct = 0;
        foreach (BaseSpawnpointNode node in EnumerateSpawns())
        {
            ++ct;
            Logger.LogDebug($"{ct,3} {node.transform.position.Format()} - {node.Format()}");
        }
    }
}

public abstract class BaseSpawnpointNode : MonoBehaviour, IDevkitInteractableBeginSelectionHandler, IDevkitInteractableEndSelectionHandler, ITerminalFormattable
{
    private bool _init;
    public bool IsSelected { get; private set; }
    public bool IsAdded { get; internal set; } = true;
    public Collider Collider { get; protected set; } = null!;
    internal bool IgnoreDestroy { get; set; }

    [UsedImplicitly]
    private void Start()
    {
        SetupCollider();
        
        if (Collider != null)
        {
            Collider.isTrigger = true;
            Collider.tag = "Logic";
        }

        _init = true;
    }

    [UsedImplicitly]
    private void OnEnable()
    {
        if (!_init || IsAdded) return;
        IsAdded = Add();
    }

    [UsedImplicitly]
    private void OnDisable()
    {
        if (!IsAdded || Level.isExiting || !Level.isLoaded || !Level.isEditor || IgnoreDestroy) return;
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
        BoxCollider collider = transform.GetOrAddComponent<BoxCollider>();
        Collider = collider;
        collider.size = new Vector3(1f, 1f, 1f);
        collider.center = new Vector3(0f, 0f, 0f);
    }
    protected abstract bool Add();
    protected abstract bool Remove();

    public abstract string Format(ITerminalFormatProvider provider);
}
public abstract class RegionalSpawnpointNode : BaseSpawnpointNode
{
    public RegionIdentifier Region { get; internal set; }
}

public abstract class IndexedSpawnpointNode : BaseSpawnpointNode
{
    public int Index { get; internal set; }
}

public class AnimalSpawnpointNode : IndexedSpawnpointNode, IDevkitSelectionTransformableHandler, IDevkitSelectionCopyableHandler
{
    private static readonly Color32 SpawnpointColor = new Color32(255, 204, 102, 255);
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
        SpawnUtil.AddAnimalSpawn(Spawnpoint);
        return true;
    }
    protected override bool Remove()
    {
        return SpawnUtil.RemoveAnimalSpawn(Spawnpoint, false);
    }
    public override string Format(ITerminalFormatProvider provider)
    {
        if (LevelAnimals.tables.Count > Spawnpoint.type)
            return "Animal Spawnpoint".Colorize(SpawnpointColor) + $" ({LevelAnimals.tables[Spawnpoint.type].name.Format(false)})";
        return "Animal Spawnpoint".Colorize(SpawnpointColor);
    }

    public override string ToString()
    {
        if (LevelAnimals.tables.Count > Spawnpoint.type)
            return $"Animal Spawnpoint ({LevelAnimals.tables[Spawnpoint.type].name})";
        return "Animal Spawnpoint";
    }
    void IDevkitSelectionTransformableHandler.transformSelection()
    {
        SpawnUtil.MoveSpawnpoint(Spawnpoint, transform.position);
    }
    GameObject IDevkitSelectionCopyableHandler.copySelection()
    {
        AnimalSpawnpoint point = new AnimalSpawnpoint(Spawnpoint.type, Spawnpoint.point);
        SpawnUtil.AddAnimalSpawn(point);
        return point.node.gameObject;
    }
}
public class VehicleSpawnpointNode : IndexedSpawnpointNode, IDevkitSelectionTransformableHandler, IDevkitSelectionCopyableHandler
{
    private static readonly Color32 SpawnpointColor = new Color32(148, 184, 184, 255);
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
        SpawnUtil.AddVehicleSpawn(Spawnpoint);
        return true;
    }
    protected override bool Remove()
    {
        return SpawnUtil.RemoveVehicleSpawn(Spawnpoint, false);
    }
    public override string Format(ITerminalFormatProvider provider)
    {
        if (LevelVehicles.tables.Count > Spawnpoint.type)
            return "Vehicle Spawnpoint".Colorize(SpawnpointColor) + $" ({LevelVehicles.tables[Spawnpoint.type].name.Format(false)})";
        return "Vehicle Spawnpoint".Colorize(SpawnpointColor);
    }

    public override string ToString()
    {
        if (LevelVehicles.tables.Count > Spawnpoint.type)
            return $"Vehicle Spawnpoint ({LevelVehicles.tables[Spawnpoint.type].name})";
        return "Vehicle Spawnpoint";
    }
    void IDevkitSelectionTransformableHandler.transformSelection()
    {
        SpawnUtil.TransformSpawnpoint(Spawnpoint, transform.position, transform.rotation.eulerAngles.y);
    }
    GameObject IDevkitSelectionCopyableHandler.copySelection()
    {
        VehicleSpawnpoint point = new VehicleSpawnpoint(Spawnpoint.type, Spawnpoint.point, Spawnpoint.angle);
        SpawnUtil.AddVehicleSpawn(point);
        return point.node.gameObject;
    }
}
public class PlayerSpawnpointNode : IndexedSpawnpointNode, IDevkitSelectionTransformableHandler, IDevkitSelectionCopyableHandler
{
    private static readonly Color32 SpawnpointColor = new Color32(204, 255, 102, 255);
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
        SpawnUtil.AddPlayerSpawn(Spawnpoint);
        return true;
    }
    protected override bool Remove()
    {
        return SpawnUtil.RemovePlayerSpawn(Spawnpoint, false);
    }
    public override string Format(ITerminalFormatProvider provider)
    {
        return Spawnpoint.isAlt ? "Player Spawnpoint".Colorize(SpawnpointColor) + " (Alternate)" : "Player Spawnpoint".Colorize(SpawnpointColor);
    }

    public override string ToString()
    {
        return Spawnpoint.isAlt ? "Player Spawnpoint (Alternate)" : "Player Spawnpoint";
    }
    void IDevkitSelectionTransformableHandler.transformSelection()
    {
        SpawnUtil.TransformSpawnpoint(Spawnpoint, transform.position, transform.rotation.eulerAngles.y);
    }
    GameObject IDevkitSelectionCopyableHandler.copySelection()
    {
        PlayerSpawnpoint point = new PlayerSpawnpoint(Spawnpoint.point, Spawnpoint.angle, Spawnpoint.isAlt);
        SpawnUtil.AddPlayerSpawn(point);
        return point.node.gameObject;
    }
}
public class ItemSpawnpointNode : RegionalSpawnpointNode, IDevkitSelectionTransformableHandler, IDevkitSelectionCopyableHandler
{
    private static readonly Color32 SpawnpointColor = new Color32(204, 255, 255, 255);
    public ItemSpawnpoint Spawnpoint { get; internal set; } = null!;
    protected override bool Add()
    {
        if (Regions.checkSafe(Spawnpoint.point))
        {
            SpawnUtil.AddItemSpawn(Spawnpoint);
            return true;
        }

        return false;
    }
    protected override bool Remove()
    {
        return SpawnUtil.RemoveItemSpawn(Spawnpoint, false);
    }
    public override string Format(ITerminalFormatProvider provider)
    {
        if (LevelItems.tables.Count > Spawnpoint.type)
            return "Item Spawnpoint".Colorize(SpawnpointColor) + $" ({LevelItems.tables[Spawnpoint.type].name.Format(false)})";
        return "Item Spawnpoint".Colorize(SpawnpointColor);
    }

    public override string ToString()
    {
        if (LevelItems.tables.Count > Spawnpoint.type)
            return $"Item Spawnpoint ({LevelItems.tables[Spawnpoint.type].name})";
        return "Item Spawnpoint";
    }
    void IDevkitSelectionTransformableHandler.transformSelection()
    {
        SpawnUtil.MoveSpawnpoint(Spawnpoint, transform.position, out _);
    }
    GameObject IDevkitSelectionCopyableHandler.copySelection()
    {
        ItemSpawnpoint point = new ItemSpawnpoint(Spawnpoint.type, Spawnpoint.point);
        SpawnUtil.AddItemSpawn(point);
        return point.node.gameObject;
    }
}
public class ZombieSpawnpointNode : RegionalSpawnpointNode, IDevkitSelectionTransformableHandler, IDevkitSelectionCopyableHandler
{
    private static readonly Color32 SpawnpointColor = new Color32(41, 163, 41, 255);
    public ZombieSpawnpoint Spawnpoint { get; internal set; } = null!;
    protected override bool Add()
    {
        if (Regions.checkSafe(Spawnpoint.point))
        {
            SpawnUtil.AddZombieSpawn(Spawnpoint);
            return true;
        }

        return false;
    }
    protected override bool Remove()
    {
        return SpawnUtil.RemoveZombieSpawn(Spawnpoint, false);
    }
    public override string Format(ITerminalFormatProvider provider)
    {
        if (LevelZombies.tables.Count > Spawnpoint.type)
            return "Zombie Spawnpoint".Colorize(SpawnpointColor) + $" ({LevelZombies.tables[Spawnpoint.type].name.Format(false)})";
        return "Zombie Spawnpoint".Colorize(SpawnpointColor);
    }

    public override string ToString()
    {
        if (LevelZombies.tables.Count > Spawnpoint.type)
            return $"Zombie Spawnpoint ({LevelZombies.tables[Spawnpoint.type].name})";
        return "Zombie Spawnpoint";
    }
    void IDevkitSelectionTransformableHandler.transformSelection()
    {
        SpawnUtil.MoveSpawnpoint(Spawnpoint, transform.position, out _);
    }
    GameObject IDevkitSelectionCopyableHandler.copySelection()
    {
        ZombieSpawnpoint point = new ZombieSpawnpoint(Spawnpoint.type, Spawnpoint.point);
        SpawnUtil.AddZombieSpawn(point);
        return point.node.gameObject;
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