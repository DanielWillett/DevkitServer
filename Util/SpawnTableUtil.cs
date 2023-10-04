namespace DevkitServer.Util;


public delegate void AnimalSpawnTableArgs(AnimalTable table, int index);
public delegate void VehicleSpawnTableArgs(VehicleTable table, int index);
public delegate void ItemSpawnTableArgs(ItemTable table, int index);
public delegate void ZombieSpawnTableArgs(ZombieTable table, int index);
public class SpawnTableUtil
{
    internal static readonly CachedMulticastEvent<AnimalSpawnTableArgs> EventOnAnimalSpawnTableNameUpdated = new CachedMulticastEvent<AnimalSpawnTableArgs>(typeof(SpawnTableUtil), nameof(OnAnimalSpawnTableNameUpdated));
    internal static readonly CachedMulticastEvent<VehicleSpawnTableArgs> EventOnVehicleSpawnTableNameUpdated = new CachedMulticastEvent<VehicleSpawnTableArgs>(typeof(SpawnTableUtil), nameof(OnVehicleSpawnTableNameUpdated));
    internal static readonly CachedMulticastEvent<ItemSpawnTableArgs> EventOnItemSpawnTableNameUpdated = new CachedMulticastEvent<ItemSpawnTableArgs>(typeof(SpawnTableUtil), nameof(OnItemSpawnTableNameUpdated));
    internal static readonly CachedMulticastEvent<ZombieSpawnTableArgs> EventOnZombieSpawnTableNameUpdated = new CachedMulticastEvent<ZombieSpawnTableArgs>(typeof(SpawnTableUtil), nameof(OnZombieSpawnTableNameUpdated));

    internal static readonly CachedMulticastEvent<AnimalSpawnTableArgs> EventOnAnimalSpawnTableNameSubmitted = new CachedMulticastEvent<AnimalSpawnTableArgs>(typeof(SpawnTableUtil), nameof(OnAnimalSpawnTableNameSubmitted));
    internal static readonly CachedMulticastEvent<VehicleSpawnTableArgs> EventOnVehicleSpawnTableNameSubmitted = new CachedMulticastEvent<VehicleSpawnTableArgs>(typeof(SpawnTableUtil), nameof(OnVehicleSpawnTableNameSubmitted));
    internal static readonly CachedMulticastEvent<ItemSpawnTableArgs> EventOnItemSpawnTableNameSubmitted = new CachedMulticastEvent<ItemSpawnTableArgs>(typeof(SpawnTableUtil), nameof(OnItemSpawnTableNameSubmitted));
    internal static readonly CachedMulticastEvent<ZombieSpawnTableArgs> EventOnZombieSpawnTableNameSubmitted = new CachedMulticastEvent<ZombieSpawnTableArgs>(typeof(SpawnTableUtil), nameof(OnZombieSpawnTableNameSubmitted));

    public static event AnimalSpawnTableArgs OnAnimalSpawnTableNameUpdated
    {
        add => EventOnAnimalSpawnTableNameUpdated.Add(value);
        remove => EventOnAnimalSpawnTableNameUpdated.Remove(value);
    }
    public static event VehicleSpawnTableArgs OnVehicleSpawnTableNameUpdated
    {
        add => EventOnVehicleSpawnTableNameUpdated.Add(value);
        remove => EventOnVehicleSpawnTableNameUpdated.Remove(value);
    }
    public static event ItemSpawnTableArgs OnItemSpawnTableNameUpdated
    {
        add => EventOnItemSpawnTableNameUpdated.Add(value);
        remove => EventOnItemSpawnTableNameUpdated.Remove(value);
    }
    public static event ZombieSpawnTableArgs OnZombieSpawnTableNameUpdated
    {
        add => EventOnZombieSpawnTableNameUpdated.Add(value);
        remove => EventOnZombieSpawnTableNameUpdated.Remove(value);
    }

    public static event AnimalSpawnTableArgs OnAnimalSpawnTableNameSubmitted
    {
        add => EventOnAnimalSpawnTableNameSubmitted.Add(value);
        remove => EventOnAnimalSpawnTableNameSubmitted.Remove(value);
    }
    public static event VehicleSpawnTableArgs OnVehicleSpawnTableNameSubmitted
    {
        add => EventOnVehicleSpawnTableNameSubmitted.Add(value);
        remove => EventOnVehicleSpawnTableNameSubmitted.Remove(value);
    }
    public static event ItemSpawnTableArgs OnItemSpawnTableNameSubmitted
    {
        add => EventOnItemSpawnTableNameSubmitted.Add(value);
        remove => EventOnItemSpawnTableNameSubmitted.Remove(value);
    }
    public static event ZombieSpawnTableArgs OnZombieSpawnTableNameSubmitted
    {
        add => EventOnZombieSpawnTableNameSubmitted.Add(value);
        remove => EventOnZombieSpawnTableNameSubmitted.Remove(value);
    }

    public static void DeselectAnimalTable()
    {
        EditorSpawns.selectedAnimal = byte.MaxValue;
        EditorSpawnsAnimalsUI.updateSelection();
    }
    public static bool SelectAnimalTable(AnimalTable table) => SelectAnimalTable(LevelAnimals.tables.IndexOf(table));
    public static bool SelectAnimalTable(int index)
    {
        if (index == byte.MaxValue)
        {
            DeselectAnimalTable();
            return true;
        }

        if (index < 0 || index >= LevelAnimals.tables.Count)
            return false;

        EditorSpawns.selectedAnimal = (byte)index;
        if (EditorSpawns.animalSpawn != null && EditorSpawns.animalSpawn.TryGetComponent(out Renderer renderer))
            renderer.material.color = LevelAnimals.tables[index].color;
        EditorSpawnsAnimalsUI.updateSelection();
        return true;
    }
    public static void DeselectVehicleTable()
    {
        EditorSpawns.selectedVehicle = byte.MaxValue;
        EditorSpawnsVehiclesUI.updateSelection();
    }
    public static bool SelectVehicleTable(VehicleTable table) => SelectVehicleTable(LevelVehicles.tables.IndexOf(table));
    public static bool SelectVehicleTable(int index)
    {
        if (index == byte.MaxValue)
        {
            DeselectVehicleTable();
            return true;
        }

        if (index < 0 || index >= LevelVehicles.tables.Count)
            return false;

        EditorSpawns.selectedVehicle = (byte)index;
        if (EditorSpawns.vehicleSpawn != null && EditorSpawns.vehicleSpawn.TryGetComponent(out Renderer renderer))
            renderer.material.color = LevelVehicles.tables[index].color;
        EditorSpawnsVehiclesUI.updateSelection();
        return true;
    }
    public static void DeselectItemTable()
    {
        EditorSpawns.selectedItem = byte.MaxValue;
        EditorSpawnsItemsUI.updateSelection();
    }
    public static bool SelectItemTable(ItemTable table) => SelectItemTable(LevelItems.tables.IndexOf(table));
    public static bool SelectItemTable(int index)
    {
        if (index == byte.MaxValue)
        {
            DeselectItemTable();
            return true;
        }

        if (index < 0 || index >= LevelItems.tables.Count)
            return false;

        EditorSpawns.selectedItem = (byte)index;
        if (EditorSpawns.itemSpawn != null && EditorSpawns.itemSpawn.TryGetComponent(out Renderer renderer))
            renderer.material.color = LevelItems.tables[index].color;
        EditorSpawnsItemsUI.updateSelection();
        return true;
    }
    public static void DeselectZombieTable()
    {
        EditorSpawns.selectedZombie = byte.MaxValue;
        EditorSpawnsZombiesUI.updateSelection();
    }
    public static bool SelectZombieTable(ZombieTable table) => SelectZombieTable(LevelZombies.tables.IndexOf(table));
    public static bool SelectZombieTable(int index)
    {
        if (index == byte.MaxValue)
        {
            DeselectZombieTable();
            return true;
        }

        if (index < 0 || index >= LevelZombies.tables.Count)
            return false;

        EditorSpawns.selectedZombie = (byte)index;
        if (EditorSpawns.zombieSpawn != null && EditorSpawns.zombieSpawn.TryGetComponent(out Renderer renderer))
            renderer.material.color = LevelZombies.tables[index].color;
        EditorSpawnsZombiesUI.updateSelection();
        return true;
    }
}