using DanielWillett.SpeedBytes;
using System.Globalization;
using System.Runtime.InteropServices;

namespace DevkitServer.Models;
[StructLayout(LayoutKind.Explicit, Size = sizeof(int))]
public readonly struct RegionIdentifier : IEquatable<RegionIdentifier>, IComparable<RegionIdentifier>
{
    public static readonly RegionIdentifier Invalid;
    static unsafe RegionIdentifier()
    {
        int num = -1;
        Invalid = *(RegionIdentifier*)&num;
    }


    [FieldOffset(0)]
    private readonly int _data;
    public byte X => (byte)(_data >> 24);
    public byte Y => (byte)(_data >> 16);
    public ushort Index => (ushort)_data;
    public bool IsInvalid => _data == -1;
    public int Raw => _data;
    internal RegionIdentifier(int data)
    {
        _data = data;
        if (X >= Regions.WORLD_SIZE)
            throw new ArgumentOutOfRangeException(nameof(X), "Must be below " + Regions.WORLD_SIZE + ".");
        if (Y >= Regions.WORLD_SIZE)
            throw new ArgumentOutOfRangeException(nameof(Y), "Must be below " + Regions.WORLD_SIZE + ".");
    }

    public RegionIdentifier(RegionCoord coord, ushort index) : this((coord.x << 24) | (coord.y << 16) | index) { }
    public RegionIdentifier(RegionCoord coord, int index) : this((coord.x << 24) | (coord.y << 16) | (ushort)index) { }
    public RegionIdentifier(byte x, byte y, ushort index) : this((x << 24) | (y << 16) | index) { }
    public RegionIdentifier(int x, int y, int index) : this(((byte)x << 24) | ((byte)y << 16) | (ushort)index) { }
    public static unsafe RegionIdentifier CreateUnsafe(int raw) => *(RegionIdentifier*)&raw;
    public bool CheckSafe()
    {
        int wrldSize = Regions.WORLD_SIZE;
        return (byte)(_data >> 24) < wrldSize && (byte)(_data >> 16) < wrldSize;
    }

    public bool IsSameRegionAs(RegionIdentifier other) => (other._data & unchecked((int)0xFFFF0000)) == (_data & unchecked((int)0xFFFF0000));
    public bool IsSameRegionAs(byte x, byte y) => X == x && Y == y;
    public override bool Equals(object? other) => other is RegionIdentifier id && Equals(id);
    public bool Equals(RegionIdentifier other) => other._data == _data;
    public override int GetHashCode() => _data;
    public static bool operator ==(RegionIdentifier left, RegionIdentifier right) => left._data == right._data;
    public static bool operator !=(RegionIdentifier left, RegionIdentifier right) => left._data != right._data;
    public static bool operator <(RegionIdentifier left, RegionIdentifier right) => left._data < right._data;
    public static bool operator >(RegionIdentifier left, RegionIdentifier right) => left._data > right._data;
    public static bool operator <=(RegionIdentifier left, RegionIdentifier right) => left._data <= right._data;
    public static bool operator >=(RegionIdentifier left, RegionIdentifier right) => left._data >= right._data;
    /// <exception cref="OverflowException"/>
    public static RegionIdentifier operator +(RegionIdentifier obj, int amt)
    {
        if (checked((obj._data & 0xFFFF) + amt) <= ushort.MaxValue)
            return new RegionIdentifier(obj._data + amt);
        throw new OverflowException("Index can not go over " + ushort.MaxValue + ".");
    }
    /// <exception cref="OverflowException"/>
    public static RegionIdentifier operator -(RegionIdentifier obj, int amt)
    {
        if (checked((obj._data & 0xFFFF) - amt) >= 0)
            return new RegionIdentifier(obj._data - amt);
        throw new OverflowException("Index can not go under " + ushort.MinValue + ".");
    }

    public int CompareTo(RegionIdentifier other) => other._data.CompareTo(_data);
    public override string ToString() => "(" + X.ToString(CultureInfo.InvariantCulture) + "," +
                                         Y.ToString(CultureInfo.InvariantCulture) + ") # " + Index.ToString(CultureInfo.InvariantCulture);

    public static void Write(ByteWriter writer, RegionIdentifier region)
    {
        writer.Write(region._data);
    }
    public static RegionIdentifier Read(ByteReader reader)
    {
        return new RegionIdentifier(reader.ReadInt32());
    }

    /// <summary>
    /// Get the element from a region list array (<see cref="Regions.WORLD_SIZE"/> sqr) without bounds checks.
    /// </summary>
    /// <remarks>Use <see cref="CheckSafe{T}"/> for bounds checks.</remarks>
    public T FromList<T>(List<T>[,] regionList)
    {
        int data = _data;
        return regionList[(data >> 24) & 0xFF, (data >> 16) & 0xFF][data & 0xFFFF];
    }

    /// <summary>
    /// Get the list from a region list array (<see cref="Regions.WORLD_SIZE"/> sqr) without bounds checks.
    /// </summary>
    /// <remarks>Use <see cref="CheckSafe{T}"/> for bounds checks.</remarks>
    public List<T> GetList<T>(List<T>[,] regionList)
    {
        int data = _data;
        return regionList[(data >> 24) & 0xFF, (data >> 16) & 0xFF];
    }

    /// <summary>
    /// Check if this <see cref="RegionIdentifier"/> exists in the given region list array (<see cref="Regions.WORLD_SIZE"/> sqr).
    /// </summary>
    /// <remarks>Use <see cref="FromList{T}"/> to actually get the item.</remarks>
    public bool CheckSafe<T>(List<T>[,] regionList)
    {
        int wrldSize = Regions.WORLD_SIZE;
        int data = _data;
        int x = (data >> 24) & 0xFF,
            y = (data >> 16) & 0xFF;
        return x < wrldSize && y < wrldSize && (data & 0xFFFF) < regionList[x, y].Count;
    }

    /// <summary>
    /// Check if this <see cref="RegionIdentifier"/> exists in the given region list array (<see cref="Regions.WORLD_SIZE"/> sqr) and output the element at this region and index.
    /// </summary>
    public bool TryFromList<T>(List<T>[,] regionList, out T element)
    {
        int wrldSize = Regions.WORLD_SIZE;
        int data = _data;
        int x = (data >> 24) & 0xFF,
            y = (data >> 16) & 0xFF,
            index = data & 0xFFFF;

        if (x < wrldSize && y < wrldSize && index < regionList[x, y].Count)
        {
            element = regionList[x, y][index];
            return true;
        }

        element = default!;
        return false;
    }

}
