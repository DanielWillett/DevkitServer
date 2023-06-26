using DevkitServer.Util.Encoding;
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

    public RegionIdentifier(byte x, byte y, ushort index) : this((x << 24) | (y << 16) | index) { }
    public RegionIdentifier(int x, int y, int index) : this(((byte)x << 24) | ((byte)y << 16) | (ushort)index) { }

    public bool IsSameRegionAs(RegionIdentifier other) => (other._data & unchecked((int)0xFFFF0000)) == (_data & unchecked((int)0xFFFF0000));
    public bool IsSameRegionAs(byte x, byte y) => X == x && Y == y;
    public override bool Equals(object other) => other is RegionIdentifier id && Equals(id);
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
}
