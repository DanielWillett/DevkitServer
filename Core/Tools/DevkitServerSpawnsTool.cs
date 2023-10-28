
using DevkitServer.API.Iterators;
using DevkitServer.API.UI;

#if CLIENT
using DevkitServer.API;
using DevkitServer.Models;
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

    protected override void OnMiddleClickPicked(in RaycastHit hit)
    {
        if (!hit.transform.TryGetComponent(out BaseSpawnpointNode node))
            return;

        switch (node)
        {
            case AnimalSpawnpointNode animal:
                SpawnTableUtil.SelectAnimalTable(animal.Spawnpoint.type);
                break;
            case VehicleSpawnpointNode vehicle:
                SpawnTableUtil.SelectVehicleTable(vehicle.Spawnpoint.type);
                break;
            case ItemSpawnpointNode item:
                SpawnTableUtil.SelectItemTable(item.Spawnpoint.type);
                break;
            case ZombieSpawnpointNode zombie:
                SpawnTableUtil.SelectItemTable(zombie.Spawnpoint.type);
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
                    EditorMessage.SendEditorMessage("No animal selected");
                    return;
                }

                AnimalSpawnpoint animalSpawnpoint = new AnimalSpawnpoint(EditorSpawns.selectedAnimal, position);
                SpawnUtil.AddAnimalSpawn(animalSpawnpoint);
                break;
            case SpawnType.Vehicle:
                if (EditorSpawns.selectedVehicle >= LevelVehicles.tables.Count)
                {
                    EditorMessage.SendEditorMessage("No vehicle selected");
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
                    EditorMessage.SendEditorMessage("No item selected");
                    return;
                }

                if (!Regions.checkSafe(position))
                {
                    EditorMessage.SendEditorMessage("Out of bounds");
                    return;
                }

                ItemSpawnpoint itemSpawnpoint = new ItemSpawnpoint(EditorSpawns.selectedItem, position);
                SpawnUtil.AddItemSpawn(itemSpawnpoint);
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
        return Type switch
        {
            SpawnType.Animal => new DistanceListIterator<AnimalSpawnpoint>(LevelAnimals.spawns, x => x.point, Editor.editor.transform.position, useXZAxisOnly: true)
                .Select(x => x.node == null ? null! : x.node.gameObject)
                .Where(x => x != null)
                .TakeWhile(x => x.gameObject.transform.position.SqrDist2D(position) <= distance),

            SpawnType.Vehicle => new DistanceListIterator<VehicleSpawnpoint>(LevelVehicles.spawns, x => x.point, Editor.editor.transform.position, useXZAxisOnly: true)
                .Select(x => x.node == null ? null! : x.node.gameObject)
                .Where(x => x != null)
                .TakeWhile(x => x.gameObject.transform.position.SqrDist2D(position) <= distance),

            SpawnType.Player => new DistanceListIterator<PlayerSpawnpoint>(LevelPlayers.spawns, x => x.point, Editor.editor.transform.position, useXZAxisOnly: true)
                .Select(x => x.node == null ? null! : x.node.gameObject)
                .Where(x => x != null)
                .TakeWhile(x => x.gameObject.transform.position.SqrDist2D(position) <= distance),

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
    protected override void EarlyInputTick()
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

public abstract class BaseSpawnpointNode : MonoBehaviour, ISpawnpointNode, IDevkitInteractableBeginSelectionHandler, IDevkitInteractableEndSelectionHandler
{
    private bool _init;
    public bool IsSelected { get; private set; }
    public bool IsAdded { get; internal set; } = true;
    public Collider Collider { get; protected set; } = null!;
    public Renderer? Renderer { get; protected set; }
    internal bool IgnoreDestroy { get; set; }
    public virtual Color Color
    {
        set
        {
            if (Renderer != null)
                Renderer.material.color = value;
        }
    }

    [UsedImplicitly]
    private void Start()
    {
        Renderer = GetComponent<Renderer>();
        Init();
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
    void IDevkitInteractableBeginSelectionHandler.beginSelection(InteractionData data)
    {
        IsSelected = true;
    }
    void IDevkitInteractableEndSelectionHandler.endSelection(InteractionData data)
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
    protected virtual void Init() { }
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
public class VehicleSpawnpointNode : IndexedSpawnpointNode, IRotatableNode, IDevkitSelectionTransformableHandler, IDevkitSelectionCopyableHandler
{
    private static readonly Color32 SpawnpointColor = new Color32(148, 184, 184, 255);
    public VehicleSpawnpoint Spawnpoint { get; internal set; } = null!;
    public Renderer? ArrowRenderer { get; protected set; }
    public override Color Color
    {
        set
        {
            if (ArrowRenderer != null)
                ArrowRenderer.material.color = value;
            base.Color = value;
        }
    }

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

    protected override void Init()
    {
        Transform arrow = transform.Find("Arrow");
        ArrowRenderer = arrow == null ? null : arrow.GetComponent<Renderer>();
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
public class PlayerSpawnpointNode : IndexedSpawnpointNode, IRotatableNode, IDevkitSelectionTransformableHandler, IDevkitSelectionCopyableHandler
{
    private static readonly Color32 SpawnpointColor = new Color32(204, 255, 102, 255);
    public PlayerSpawnpoint Spawnpoint { get; internal set; } = null!;
    public Renderer? ArrowRenderer { get; protected set; }
    public override Color Color
    {
        set
        {
            if (ArrowRenderer != null)
                ArrowRenderer.material.color = value;
            base.Color = value;
        }
    }
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
    protected override void Init()
    {
        Transform arrow = transform.Find("Arrow");
        ArrowRenderer = arrow == null ? null : arrow.GetComponent<Renderer>();
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

public interface ISpawnpointNode : ITerminalFormattable
{
    // ReSharper disable InconsistentNaming
    GameObject gameObject { get; }
    Transform transform { get; }

    // ReSharper restore InconsistentNaming
    bool IsSelected { get; }
    bool IsAdded { get; }
    Collider Collider { get; }
    Renderer? Renderer { get; }
    Color Color { set; }
}
public interface IRotatableNode : ISpawnpointNode
{
    Renderer? ArrowRenderer { get; }
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