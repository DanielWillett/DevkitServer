namespace DevkitServer.API.Devkit.Spawns;

/// <summary>
/// Represents a type of spawnpoint or spawn table.
/// </summary>
public enum SpawnType : byte
{
    // do not rename these

    None,

    /// <summary>
    /// <see cref="PlayerSpawnpoint"/> and <see cref="PlayerSpawnpointNode"/>.
    /// </summary>
    /// <remarks>Global index based.</remarks>
    Player,

    /// <summary>
    /// <see cref="AnimalSpawnpoint"/>, <see cref="AnimalSpawnpointNode"/>, <see cref="AnimalTable"/>, <see cref="AnimalTier"/>, and <see cref="AnimalSpawn"/>.
    /// </summary>
    /// <remarks>Global index based.</remarks>
    Animal,

    /// <summary>
    /// <see cref="ZombieSpawnpoint"/>, <see cref="ZombieSpawnpointNode"/>, <see cref="ZombieTable"/>, <see cref="ZombieSlot"/>, and <see cref="ZombieCloth"/>.
    /// </summary>
    /// <remarks>Regional index based.</remarks>
    Zombie,

    /// <summary>
    /// <see cref="ItemSpawnpoint"/>, <see cref="ItemSpawnpointNode"/>, <see cref="ItemTable"/>, <see cref="ItemTier"/>, and <see cref="ItemSpawn"/>.
    /// </summary>
    /// <remarks>Regional index based.</remarks>
    Item,

    /// <summary>
    /// <see cref="VehicleSpawnpoint"/>, <see cref="VehicleSpawnpointNode"/>, <see cref="VehicleTable"/>, <see cref="VehicleTier"/>, and <see cref="VehicleSpawn"/>.
    /// </summary>
    /// <remarks>Global index based.</remarks>
    Vehicle
}