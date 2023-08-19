using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
}