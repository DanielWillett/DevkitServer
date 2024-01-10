using DevkitServer.API;
using DevkitServer.API.Devkit.Spawns;
using System.Runtime.InteropServices;

namespace DevkitServer.Models;

/// <summary>
/// Represents a <see cref="AnimalSpawn"/>, <see cref="VehicleSpawn"/>, <see cref="ItemSpawn"/>, or <see cref="ZombieCloth"/>.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 4)]
public readonly struct SpawnAssetIdentifier :
    IEquatable<SpawnAssetIdentifier>,
    IComparable<SpawnAssetIdentifier>,
    IEquatable<SpawnTierIdentifier>,
    IComparable<SpawnTierIdentifier>,
    ITerminalFormattable
{
    [FieldOffset(0)]
    private readonly int _data;

    /// <summary>
    /// Type of the spawn table.
    /// </summary>
    public SpawnType Type => (SpawnType)((_data >> 16) & 0xFF);

    /// <summary>
    /// Index of the spawn table.
    /// </summary>
    public int TableIndex => (_data >> 8) & 0xFF;

    /// <summary>
    /// Index of the tier in the spawn table.
    /// </summary>
    public int TierIndex => _data & 0xFF;

    /// <summary>
    /// Index of the asset in the spawn tier.
    /// </summary>
    public int AssetIndex => (_data >> 24) & 0xFF;
    
    internal int Raw => _data;
    public SpawnAssetIdentifier(SpawnType type, int spawnTableIndex, int tierIndex, int assetIndex)
    {
        if (type is not SpawnType.Animal and not SpawnType.Vehicle and not SpawnType.Item and not SpawnType.Zombie)
            throw new ArgumentOutOfRangeException(nameof(type), "Spawn type must be 'Animal', 'Vehicle', 'Item', or 'Zombie'.");
        
        if (spawnTableIndex is > byte.MaxValue or < byte.MinValue)
            throw new ArgumentOutOfRangeException(nameof(spawnTableIndex), $"Must be <= {byte.MaxValue} and >= {byte.MinValue}.");
        
        if (tierIndex is > byte.MaxValue or < byte.MinValue)
            throw new ArgumentOutOfRangeException(nameof(tierIndex), $"Must be <= {byte.MaxValue} and >= {byte.MinValue}.");
        
        if (assetIndex is > byte.MaxValue or < byte.MinValue)
            throw new ArgumentOutOfRangeException(nameof(assetIndex), $"Must be <= {byte.MaxValue} and >= {byte.MinValue}.");
        
        _data = (assetIndex << 24) | ((byte)type << 16) | ((byte)spawnTableIndex << 8) | (ushort)tierIndex;
    }
    public SpawnAssetIdentifier(SpawnType type, byte spawnTableIndex, byte tierIndex, byte assetIndex)
    {
        if (type is not SpawnType.Animal and not SpawnType.Vehicle and not SpawnType.Item and not SpawnType.Zombie)
            throw new ArgumentOutOfRangeException(nameof(type), "Spawn type must be 'Animal', 'Vehicle', 'Item', or 'Zombie'.");

        _data = (assetIndex << 24) | ((byte)type << 16) | (spawnTableIndex << 16) | tierIndex;
    }
    internal SpawnAssetIdentifier(int data) => _data = data;

    /// <returns>
    /// An identifier for the tier of this asset.
    /// </returns>
    public SpawnTierIdentifier GetTier() => new SpawnTierIdentifier(_data & 0xFFFFFF);

    /// <summary>
    /// Checks all indexes to see if accessing by index is safe.
    /// </summary>
    public bool CheckSafe()
    {
        int tableIndex = TableIndex;
        int tierIndex = TierIndex;
        int assetIndex = AssetIndex;
        return Type switch
        {
            SpawnType.Animal =>  LevelAnimals.tables.Count > tableIndex &&
                                 LevelAnimals.tables[tableIndex].tiers.Count > tierIndex &&
                                 LevelAnimals.tables[tableIndex].tiers[tierIndex].table.Count > assetIndex,
            SpawnType.Vehicle => LevelVehicles.tables.Count > tableIndex &&
                                 LevelVehicles.tables[tableIndex].tiers.Count > tierIndex &&
                                 LevelVehicles.tables[tableIndex].tiers[tierIndex].table.Count > assetIndex,
            SpawnType.Item =>    LevelItems.tables.Count > tableIndex &&
                                 LevelItems.tables[tableIndex].tiers.Count > tierIndex &&
                                 LevelItems.tables[tableIndex].tiers[tierIndex].table.Count > assetIndex,
            SpawnType.Zombie =>  LevelZombies.tables.Count > tableIndex &&
                                 LevelZombies.tables[tableIndex].slots.Length > tierIndex &&
                                 LevelZombies.tables[tableIndex].slots[tierIndex].table.Count > assetIndex,
            _ => false
        };
    }
    public override bool Equals(object? obj) => obj is SpawnAssetIdentifier id && Equals(id);
    public override int GetHashCode() => _data;
    public static bool operator ==(SpawnAssetIdentifier left, SpawnAssetIdentifier right) => left.Equals(right);
    public static bool operator !=(SpawnAssetIdentifier left, SpawnAssetIdentifier right) => !left.Equals(right);
    public bool Equals(SpawnTierIdentifier other) => other.Raw == (_data & 0xFFFFFF);
    public int CompareTo(SpawnTierIdentifier other) => (_data & 0xFFFFFF).CompareTo(other.Raw);
    public bool Equals(SpawnAssetIdentifier other) => other._data == _data;
    public int CompareTo(SpawnAssetIdentifier other) => (((_data & 0xFFFFFF) << 8) | ((_data >> 24) & 0xFF)).CompareTo(((other._data & 0xFFFFFF) << 8) | ((other._data >> 24) & 0xFF));
    public override string ToString() => $"{Type} Spawn #{TableIndex} / Tier #{TierIndex} / Asset #{AssetIndex}";
    public string Format(ITerminalFormatProvider provider) => $"{Type} Spawn #{TableIndex.Format()} / Tier #{TierIndex.Format()} / Asset #{AssetIndex.Format()}".Colorize(FormattingColorType.Struct);
}