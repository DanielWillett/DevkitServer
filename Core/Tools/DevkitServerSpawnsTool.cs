#if CLIENT
using Cysharp.Threading.Tasks;
using DevkitServer.API.Abstractions;
using DevkitServer.API.Devkit;
using DevkitServer.API.Devkit.Spawns;
using DevkitServer.API.Iterators;
using DevkitServer.API.UI;
using DevkitServer.API.UI.Extensions;
using DevkitServer.Core.UI.Extensions;
using DevkitServer.Models;
using DevkitServer.Multiplayer.Actions;
using DevkitServer.Util.Region;
using SDG.Framework.Devkit;
using SDG.Framework.Devkit.Interactable;
using SDG.Framework.Utilities;

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
    private const byte AreaSelectRegionAnimalDistance = 2;
    private const byte AreaSelectRegionVehicleDistance = 1;
    private const byte AreaSelectRegionPlayerDistance = 2;
    private const byte AreaSelectRegionItemDistance = 1;
    private const byte AreaSelectRegionZombieDistance = 1;
    private float _lastAwaitingInstantiation;
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

    protected override void DeleteSelection()
    {
        if (!DevkitServerModule.IsEditing)
        {
            base.DeleteSelection();
            return;
        }

        bool listeningSingleReq = ClientEvents.ListeningOnDeleteSpawnRequested,
             listeningSingle = ClientEvents.ListeningOnDeleteSpawn,
             listeningBatch = ClientEvents.ListeningOnDeleteSpawns;

        if (!listeningSingleReq && !listeningSingle && !listeningBatch)
        {
            base.DeleteSelection();
            return;
        }

        float dt = CachedTime.DeltaTime;

        DeleteSpawnProperties singleProperties = default;
        List<NetId64>? toUpdate = listeningBatch ? ListPool<NetId64>.claim() : null;
        try
        {
            SpawnType spawnType = 0;
            foreach (DevkitSelection selection in DevkitSelectionManager.selection)
            {
                if (!selection.gameObject.TryGetComponent(out BaseSpawnpointNode node) || !node.SanityCheck())
                    continue;

                if (listeningSingleReq || listeningSingle)
                    singleProperties = new DeleteSpawnProperties(node.NetId, node.SpawnType, dt);

                if (listeningSingleReq)
                {
                    bool shouldAllow = true;
                    ClientEvents.InvokeOnDeleteSpawnRequested(in singleProperties, ref shouldAllow);
                    if (!shouldAllow)
                        continue;
                }

                bool destroy = true;
                ((IDevkitSelectionDeletableHandler)node).Delete(ref destroy);

                if (destroy)
                    Object.Destroy(selection.gameObject);

                if (listeningSingle)
                    ClientEvents.InvokeOnDeleteSpawn(in singleProperties);

                if (!listeningBatch)
                    continue;

                toUpdate!.Add(node.NetId);

                if (spawnType == 0)
                    spawnType = node.SpawnType;

                if (toUpdate.Count >= SpawnUtil.MaxDeleteSpawnCount || spawnType != node.SpawnType)
                    Flush(toUpdate, spawnType, dt);

                spawnType = node.SpawnType;
            }

            if (listeningBatch && toUpdate!.Count > 0)
                Flush(toUpdate, spawnType, dt);

            static void Flush(List<NetId64> toUpdate, SpawnType spawnType, float dt)
            {
                DeleteSpawnsProperties properties = new DeleteSpawnsProperties(toUpdate.ToSpan(), spawnType, dt);

                ClientEvents.InvokeOnDeleteSpawns(in properties);

                toUpdate.Clear();
            }
        }
        finally
        {
            if (listeningBatch)
                ListPool<NetId64>.release(toUpdate);
        }
    }
    protected override void TransformSelection()
    {
        if (!DevkitServerModule.IsEditing)
        {
            base.TransformSelection();
            return;
        }

        bool listeningSingleReq = ClientEvents.ListeningOnMoveSpawnFinalRequested,
             listeningSingle = ClientEvents.ListeningOnMoveSpawnFinal,
             listeningBatch = ClientEvents.ListeningOnMoveSpawnsFinal;

        if (!listeningSingleReq && !listeningSingle && !listeningBatch)
        {
            base.TransformSelection();
            return;
        }

        float dt = CachedTime.DeltaTime;

        MoveSpawnFinalProperties singleProperties = default;
        List<NetId64>? toUpdate = listeningBatch ? ListPool<NetId64>.claim() : null;
        List<TransformationDelta>? transforms = listeningBatch ? ListPool<TransformationDelta>.claim() : null;
        try
        {
            SpawnType spawnType = 0;
            bool useRotation = false;
            foreach (DevkitSelection selection in DevkitSelectionManager.selection)
            {
                if (!selection.gameObject.TryGetComponent(out BaseSpawnpointNode node) || !node.SanityCheck())
                    continue;

                Vector3 pos = node.transform.position;
                Quaternion rot = node.transform.rotation;

                TransformationDelta.TransformFlags flags = node.SpawnType is SpawnType.Player or SpawnType.Vehicle && selection.preTransformRotation != rot
                    ? TransformationDelta.TransformFlags.Rotation
                    : 0;

                if (selection.preTransformPosition != pos)
                    flags |= TransformationDelta.TransformFlags.Position;

                float yaw = (flags & TransformationDelta.TransformFlags.Rotation) != 0 ? rot.eulerAngles.y : 0f;

                TransformationDelta t = new TransformationDelta(flags, pos, yaw, selection.preTransformPosition, selection.preTransformRotation.eulerAngles.y);

                if (listeningSingleReq || listeningSingle)
                    singleProperties = new MoveSpawnFinalProperties(node.NetId, t, node.SpawnType, (flags & TransformationDelta.TransformFlags.Rotation) != 0, dt);

                if (!useRotation && (flags & TransformationDelta.TransformFlags.Rotation) != 0)
                    useRotation = true;

                if (listeningSingleReq)
                {
                    bool shouldAllow = true;
                    ClientEvents.InvokeOnMoveSpawnFinalRequested(in singleProperties, ref shouldAllow);
                    if (!shouldAllow)
                        continue;
                }

                ((IDevkitSelectionTransformableHandler)node).transformSelection();

                if (listeningSingle)
                    ClientEvents.InvokeOnMoveSpawnFinal(in singleProperties);

                if (!listeningBatch)
                    continue;

                toUpdate!.Add(node.NetId);
                transforms!.Add(t);

                if (spawnType == 0)
                    spawnType = node.SpawnType;

                if (toUpdate.Count >= SpawnUtil.MaxMoveSpawnCount || spawnType != node.SpawnType)
                {
                    Flush(toUpdate, transforms, spawnType, dt, useRotation);
                    useRotation = false;
                }

                spawnType = node.SpawnType;
            }

            if (listeningBatch && toUpdate!.Count > 0)
                Flush(toUpdate, transforms!, spawnType, dt, useRotation);

            static void Flush(List<NetId64> toUpdate, List<TransformationDelta> t, SpawnType spawnType, float dt, bool useRotation)
            {
                MoveSpawnsFinalProperties properties = new MoveSpawnsFinalProperties(toUpdate.ToSpan(), t.ToSpan(), spawnType, useRotation, dt);

                ClientEvents.InvokeOnMoveSpawnsFinal(in properties);

                toUpdate.Clear();
                t.Clear();
            }
        }
        finally
        {
            if (listeningBatch)
            {
                ListPool<NetId64>.release(toUpdate);
                ListPool<TransformationDelta>.release(transforms);
            }
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
        bool online = DevkitServerModule.IsEditing;
        if (online && CachedTime.RealtimeSinceStartup - _lastAwaitingInstantiation < 10f)
        {
            EditorMessage.SendEditorMessage(TranslationSource.DevkitServerMessageLocalizationSource, "SpawnAlreadyInstantiating");
            return;
        }
        
        if (Type is SpawnType.Zombie or SpawnType.Item && !Regions.checkSafe(position))
        {
            EditorMessage.SendEditorMessage(TranslationSource.DevkitServerMessageLocalizationSource, "OutOfRegionBounds");
            return;
        }

        switch (Type)
        {
            case SpawnType.Animal:
                if (EditorSpawns.selectedAnimal >= LevelAnimals.tables.Count)
                {
                    EditorMessage.SendEditorMessage(TranslationSource.DevkitServerMessageLocalizationSource, "NoAnimalTableSelected");
                    return;
                }

                if (online)
                {
                    UniTask.Create(async () =>
                    {
                        _lastAwaitingInstantiation = CachedTime.RealtimeSinceStartup;
                        try
                        {
                            await SpawnUtil.RequestAddAnimalSpawnpoint(EditorSpawns.selectedAnimal, position);
                        }
                        finally
                        {
                            _lastAwaitingInstantiation = float.MinValue;
                        }
                    });
                }
                else
                {
                    SpawnUtil.AddAnimalSpawnpointLocal(EditorSpawns.selectedAnimal, position);
                }
                break;
            case SpawnType.Vehicle:
                if (EditorSpawns.selectedVehicle >= LevelVehicles.tables.Count)
                {
                    EditorMessage.SendEditorMessage(TranslationSource.DevkitServerMessageLocalizationSource, "NoVehicleTableSelected");
                    return;
                }

                if (online)
                {
                    UniTask.Create(async () =>
                    {
                        _lastAwaitingInstantiation = CachedTime.RealtimeSinceStartup;
                        try
                        {
                            await SpawnUtil.RequestAddVehicleSpawnpoint(EditorSpawns.selectedVehicle, position, rotation.eulerAngles.y);
                        }
                        finally
                        {
                            _lastAwaitingInstantiation = float.MinValue;
                        }
                    });
                }
                else
                {
                    SpawnUtil.AddVehicleSpawnpointLocal(EditorSpawns.selectedVehicle, position, rotation.eulerAngles.y);
                }
                break;
            case SpawnType.Player:
                if (online)
                {
                    UniTask.Create(async () =>
                    {
                        _lastAwaitingInstantiation = CachedTime.RealtimeSinceStartup;
                        try
                        {
                            await SpawnUtil.RequestAddPlayerSpawnpoint(position, rotation.eulerAngles.y, EditorSpawns.selectedAlt);
                        }
                        finally
                        {
                            _lastAwaitingInstantiation = float.MinValue;
                        }
                    });
                }
                else
                {
                    SpawnUtil.AddPlayerSpawnpointLocal(position, rotation.eulerAngles.y, EditorSpawns.selectedAlt);
                }
                break;
            case SpawnType.Item:
                if (EditorSpawns.selectedItem >= LevelItems.tables.Count)
                {
                    EditorMessage.SendEditorMessage(TranslationSource.DevkitServerMessageLocalizationSource, "NoItemTableSelected");
                    return;
                }

                if (online)
                {
                    UniTask.Create(async () =>
                    {
                        _lastAwaitingInstantiation = CachedTime.RealtimeSinceStartup;
                        try
                        {
                            await SpawnUtil.RequestAddItemSpawnpoint(EditorSpawns.selectedItem, position);
                        }
                        finally
                        {
                            _lastAwaitingInstantiation = float.MinValue;
                        }
                    });
                }
                else
                {
                    SpawnUtil.AddItemSpawnpointLocal(EditorSpawns.selectedItem, position);
                }
                break;
            case SpawnType.Zombie:
                if (EditorSpawns.selectedZombie >= LevelZombies.tables.Count)
                {
                    EditorMessage.SendEditorMessage(TranslationSource.DevkitServerMessageLocalizationSource, "NoZombieTableSelected");
                    return;
                }

                if (online)
                {
                    UniTask.Create(async () =>
                    {
                        _lastAwaitingInstantiation = CachedTime.RealtimeSinceStartup;
                        try
                        {
                            await SpawnUtil.RequestAddZombieSpawnpoint(EditorSpawns.selectedZombie, position);
                        }
                        finally
                        {
                            _lastAwaitingInstantiation = float.MinValue;
                        }
                    });
                }
                else
                {
                    SpawnUtil.AddZombieSpawnpointLocal(EditorSpawns.selectedZombie, position);
                }
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
        float distance = Regions.REGION_SIZE * (Type switch
        {
            SpawnType.Animal => AreaSelectRegionAnimalDistance,
            SpawnType.Player => AreaSelectRegionPlayerDistance,
            _ => AreaSelectRegionVehicleDistance
        } + 0.5f);
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
    public void Select(int index, bool additive)
    {
        ThreadUtil.assertIsGameThread();

        if (Type is not SpawnType.Animal and not SpawnType.Vehicle and not SpawnType.Player)
            throw new InvalidOperationException($"Current spawn type ({Type.GetLowercaseText()}) does not support indexes.");

        if (!SpawnUtil.CheckSpawnpointSafe(Type, index))
            throw new ArgumentOutOfRangeException(nameof(index), $"{Type.GetPropercaseText()} spawn at index {index} does not exist.");

        if (!additive)
            DevkitSelectionManager.clear();

        Transform transform = Type switch
        {
            SpawnType.Animal => LevelAnimals.spawns[index].node,
            SpawnType.Player => LevelPlayers.spawns[index].node,
            _ => LevelVehicles.spawns[index].node
        };

        DevkitSelection selection = new DevkitSelection(transform != null ? RootSelections ? transform.root.gameObject : transform.gameObject : null, transform?.GetComponentInChildren<Collider>());
        if (selection.gameObject != null)
            DevkitSelectionManager.data.point = selection.gameObject.transform.position;

        DevkitSelectionManager.select(selection);
    }
    public void Select(RegionIdentifier id, bool additive)
    {
        ThreadUtil.assertIsGameThread();

        if (Type is not SpawnType.Animal and not SpawnType.Vehicle and not SpawnType.Player)
            throw new InvalidOperationException($"Current spawn type ({Type.GetLowercaseText()}) does not support indexes.");

        if (!SpawnUtil.CheckSpawnpointSafe(Type, id))
            throw new ArgumentOutOfRangeException(nameof(id), $"{Type.GetPropercaseText()} spawn at region identifier {id} does not exist.");

        if (!additive)
            DevkitSelectionManager.clear();

        Transform transform = Type switch
        {
            SpawnType.Animal => id.FromList(LevelItems.spawns).node,
            _ => id.FromList(LevelZombies.spawns).node
        };

        DevkitSelection selection = new DevkitSelection(transform != null ? RootSelections ? transform.root.gameObject : transform.gameObject : null, transform?.GetComponentInChildren<Collider>());
        if (selection.gameObject != null)
            DevkitSelectionManager.data.point = selection.gameObject.transform.position;

        DevkitSelectionManager.select(selection);
    }
    private IEnumerable<GameObject> EnumerateRegions()
    {
        if (Type == SpawnType.Item)
        {
            return new ListRegionsEnumerator<ItemSpawnpoint>(LevelItems.spawns, Editor.editor.area.region_x, Editor.editor.area.region_y, AreaSelectRegionItemDistance)
                .Select(x => x.node == null ? null : x.node.gameObject).Where(x => x != null)!;
        }
        
        if (Type == SpawnType.Zombie)
        {
            return new ListRegionsEnumerator<ZombieSpawnpoint>(LevelZombies.spawns, Editor.editor.area.region_x, Editor.editor.area.region_y, AreaSelectRegionZombieDistance)
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
                        .OnSpawnMoved(a.Spawnpoint, default, default, default);
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
                        .OnSpawnMoved(v.Spawnpoint, default, default, default, default, default);
                    break;
            }
        }
    }
}
#endif