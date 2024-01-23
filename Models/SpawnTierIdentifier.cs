using DevkitServer.API;
using DevkitServer.API.Devkit.Spawns;
using System.Runtime.InteropServices;

namespace DevkitServer.Models;

/// <summary>
/// Represents a <see cref="AnimalTier"/>, <see cref="VehicleTier"/>, <see cref="ItemTier"/>, or <see cref="ZombieSlot"/>.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 4)]
public readonly struct SpawnTierIdentifier :
    IEquatable<SpawnTierIdentifier>,
    IComparable<SpawnTierIdentifier>,
    IEquatable<SpawnAssetIdentifier>,
    IComparable<SpawnAssetIdentifier>,
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
    internal int Raw => _data;
    public SpawnTierIdentifier(SpawnType type, int spawnTableIndex, int tierIndex)
    {
        if (type is not SpawnType.Animal and not SpawnType.Vehicle and not SpawnType.Item and not SpawnType.Zombie)
            throw new ArgumentOutOfRangeException(nameof(type), "Spawn type must be 'Animal', 'Vehicle', 'Item', or 'Zombie'.");
        
        if (spawnTableIndex is > byte.MaxValue or < byte.MinValue)
            throw new ArgumentOutOfRangeException(nameof(spawnTableIndex), $"Must be <= {byte.MaxValue} and >= {byte.MinValue}.");
        
        if (tierIndex is > byte.MaxValue or < byte.MinValue)
            throw new ArgumentOutOfRangeException(nameof(tierIndex), $"Must be <= {byte.MaxValue} and >= {byte.MinValue}.");
        
        _data = ((byte)type << 16) | ((byte)spawnTableIndex << 8) | (byte)tierIndex;
    }
    public SpawnTierIdentifier(SpawnType type, byte spawnTableIndex, byte tierIndex)
    {
        if (type is not SpawnType.Animal and not SpawnType.Vehicle and not SpawnType.Item and not SpawnType.Zombie)
            throw new ArgumentOutOfRangeException(nameof(type), "Spawn type must be 'Animal', 'Vehicle', 'Item', or 'Zombie'.");

        _data = ((byte)type << 16) | (spawnTableIndex << 8) | tierIndex;
    }
    internal SpawnTierIdentifier(int data) => _data = data;
    public SpawnAssetIdentifier GetAsset(int index)
    {
        if (index is > byte.MaxValue or < byte.MinValue)
            throw new ArgumentOutOfRangeException(nameof(index), $"Must be <= {byte.MaxValue} and >= {byte.MinValue}.");

        return new SpawnAssetIdentifier(_data | (index << 24));
    }
    
    /// <summary>
    /// Checks all indexes to see if accessing by index is safe.
    /// </summary>
    public bool CheckSafe()
    {
        int tableIndex = TableIndex;
        int tierIndex = TierIndex;
        return Type switch
        {
            SpawnType.Animal => LevelAnimals.tables.Count > tableIndex &&
                                LevelAnimals.tables[tableIndex].tiers.Count > tierIndex,
            SpawnType.Vehicle => LevelVehicles.tables.Count > tableIndex &&
                                 LevelVehicles.tables[tableIndex].tiers.Count > tierIndex,
            SpawnType.Item => LevelItems.tables.Count > tableIndex &&
                              LevelItems.tables[tableIndex].tiers.Count > tierIndex,
            SpawnType.Zombie => LevelZombies.tables.Count > tableIndex &&
                                LevelZombies.tables[tableIndex].slots.Length > tierIndex,
            _ => false
        };
    }
    public override bool Equals(object? obj) => obj is SpawnTierIdentifier id && Equals(id);
    public override int GetHashCode() => _data;

    public static bool operator ==(SpawnTierIdentifier left, SpawnTierIdentifier right) => left.Equals(right);
    public static bool operator ==(SpawnAssetIdentifier left, SpawnTierIdentifier right) => left.Equals(right);
    public static bool operator ==(SpawnTierIdentifier left, SpawnAssetIdentifier right) => right.Equals(left);
    public static bool operator !=(SpawnTierIdentifier left, SpawnTierIdentifier right) => !left.Equals(right);
    public static bool operator !=(SpawnTierIdentifier left, SpawnAssetIdentifier right) => !right.Equals(left);
    public static bool operator !=(SpawnAssetIdentifier left, SpawnTierIdentifier right) => !left.Equals(right);
    public bool Equals(SpawnTierIdentifier other) => other._data == _data;
    public int CompareTo(SpawnTierIdentifier other) => _data.CompareTo(other._data);
    public bool Equals(SpawnAssetIdentifier other) => (other.Raw & 0xFFFFFF) == _data;
    public int CompareTo(SpawnAssetIdentifier other) => _data.CompareTo(other.Raw & 0xFFFFFF);
    public override string ToString() => $"{Type} Spawn #{TableIndex} / Tier #{TierIndex}";
    public string Format(ITerminalFormatProvider provider) => $"{Type} Spawn #{TableIndex.Format()} / Tier #{TierIndex.Format()}".Colorize(FormattingColorType.Struct);
}