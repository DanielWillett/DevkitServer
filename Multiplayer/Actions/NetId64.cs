namespace DevkitServer.Multiplayer.Actions;

/// <summary>
/// 64-bit version of <see cref="NetId"/> to use with some types that have a lot of elements.
/// </summary>
public readonly struct NetId64(ulong id) : IEquatable<NetId64>, IComparable<NetId64>
{
    public static readonly NetId64 Invalid = default;
    public readonly ulong Id = id;
    public bool IsNull() => Id == 0ul;
    public bool Equals(NetId64 netId) => unchecked( (long)Id == (long)netId.Id );
    public int CompareTo(NetId64 other) => Id.CompareTo(other.Id);
    public override bool Equals(object? obj) => obj is NetId64 netId && unchecked( (long)Id == (long)netId.Id );
    public override int GetHashCode() => Id.GetHashCode();
    public string ToString(string format, IFormatProvider formatProvider) => Id.ToString(format ?? "X8", formatProvider);
    public string ToString(string format) => Id.ToString(format ?? "X8");
    public string ToString(IFormatProvider formatProvider) => Id.ToString("X8", formatProvider);
    public override string ToString() => Id.ToString("X8");
    public static bool operator ==(NetId64 lhs, NetId64 rhs) => unchecked( (long)lhs.Id == (long)rhs.Id );
    public static bool operator !=(NetId64 lhs, NetId64 rhs) => unchecked( (long)lhs.Id != (long)rhs.Id );
    public static NetId64 operator +(NetId64 lhs, NetId64 rhs) => new NetId64(lhs.Id + rhs.Id);
    public static NetId64 operator +(NetId64 lhs, ulong rhs) => new NetId64(lhs.Id + rhs);
    public static NetId64 operator ++(NetId64 lhs) => new NetId64(lhs.Id + 1ul);
    public static NetId64 operator -(NetId64 lhs, NetId64 rhs) => checked( new NetId64(lhs.Id - rhs.Id) );
    public static NetId64 operator -(NetId64 lhs, ulong rhs) => checked( new NetId64(lhs.Id - rhs) );
    public static NetId64 operator --(NetId64 lhs) => checked( new NetId64(lhs.Id - 1ul) );
    public static NetId64 operator *(NetId64 lhs, NetId64 rhs) => new NetId64(lhs.Id * rhs.Id);
    public static NetId64 operator *(NetId64 lhs, ulong rhs) => new NetId64(lhs.Id * rhs);
    public static NetId64 operator /(NetId64 lhs, NetId64 rhs) => new NetId64(lhs.Id / rhs.Id);
    public static NetId64 operator /(NetId64 lhs, ulong rhs) => new NetId64(lhs.Id / rhs);
}
