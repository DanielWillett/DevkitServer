using System.Runtime.InteropServices;
using DevkitServer.API;

namespace DevkitServer.Models;

/// <summary>
/// Represents a road vertex or joint.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 4)]
public readonly struct RoadVertexIdentifier :
    IEquatable<RoadVertexIdentifier>,
    IComparable<RoadVertexIdentifier>,
    IEquatable<RoadTangentHandleIdentifier>,
    IComparable<RoadTangentHandleIdentifier>,
    ITerminalFormattable
{
    [FieldOffset(0)]
    private readonly int _data;

    /// <summary>
    /// Index of the road.
    /// </summary>
    public int Road => (_data >> 16) & 0xFFFF;

    /// <summary>
    /// Index of the vertex in the road.
    /// </summary>
    public int Vertex => _data & 0xFFFF;
    internal int RawData => _data;
    public RoadVertexIdentifier(int roadIndex, int vertexIndex)
    {
        if (roadIndex is > ushort.MaxValue or < ushort.MinValue)
            throw new ArgumentOutOfRangeException(nameof(roadIndex), $"Must be <= {ushort.MaxValue} and >= {ushort.MinValue}.");
        if (vertexIndex is > ushort.MaxValue or < ushort.MinValue)
            throw new ArgumentOutOfRangeException(nameof(vertexIndex), $"Must be <= {ushort.MaxValue} and >= {ushort.MinValue}.");
        _data = ((ushort)roadIndex << 16) | (ushort)vertexIndex;
    }
    public RoadVertexIdentifier(ushort roadIndex, ushort vertexIndex)
    {
        _data = (roadIndex << 16) | vertexIndex;
    }
    public RoadTangentHandleIdentifier GetHandle(TangentHandle handle) => new RoadTangentHandleIdentifier(_data, handle);
    internal RoadVertexIdentifier(int data) => _data = data;
    public override bool Equals(object obj) => obj is RoadVertexIdentifier id && Equals(id);
    public override int GetHashCode() => _data;

    public static bool operator ==(RoadVertexIdentifier left, RoadVertexIdentifier right) => left.Equals(right);
    public static bool operator ==(RoadTangentHandleIdentifier left, RoadVertexIdentifier right) => left.Equals(right);
    public static bool operator ==(RoadVertexIdentifier left, RoadTangentHandleIdentifier right) => right.Equals(left);
    public static bool operator !=(RoadVertexIdentifier left, RoadVertexIdentifier right) => !left.Equals(right);
    public static bool operator !=(RoadVertexIdentifier left, RoadTangentHandleIdentifier right) => !right.Equals(left);
    public static bool operator !=(RoadTangentHandleIdentifier left, RoadVertexIdentifier right) => !left.Equals(right);
    public bool Equals(RoadVertexIdentifier other) => other._data == _data;
    public int CompareTo(RoadVertexIdentifier other) => _data.CompareTo(other._data);
    public bool Equals(RoadTangentHandleIdentifier other) => other.RawData == _data;
    public int CompareTo(RoadTangentHandleIdentifier other) => _data.CompareTo(other.RawData);
    public override string ToString() => $"Road #{Road} / Vertex #{Vertex}";
    public string Format(ITerminalFormatProvider provider) => $"Road #{Road.Format()} / Vertex #{Vertex.Format()}".Colorize(FormattingColorType.Struct);
}

/// <summary>
/// Represents a road vertex or joint's tangent handle.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 8)]
public readonly struct RoadTangentHandleIdentifier : IEquatable<RoadTangentHandleIdentifier>, IComparable<RoadTangentHandleIdentifier>, IEquatable<RoadVertexIdentifier>, IComparable<RoadVertexIdentifier>
{
    [FieldOffset(0)]
    private readonly int _data;
    [FieldOffset(4)]
    private readonly TangentHandle _handle;

    /// <summary>
    /// Index of the road.
    /// </summary>
    public int Road => (_data >> 16) & 0xFFFF;

    /// <summary>
    /// Index of the vertex in the road.
    /// </summary>
    public int Vertex => _data & 0xFFFF;

    /// <summary>
    /// The side of the vertex the referenced tangent handle is on.
    /// </summary>
    public TangentHandle Handle => _handle;
    internal int RawData => _data;
    public RoadTangentHandleIdentifier(int roadIndex, int vertexIndex, TangentHandle handle)
    {
        if (roadIndex is > ushort.MaxValue or < ushort.MinValue)
            throw new ArgumentOutOfRangeException(nameof(roadIndex), $"Must be <= {ushort.MaxValue} and >= {ushort.MinValue}.");
        if (vertexIndex is > ushort.MaxValue or < ushort.MinValue)
            throw new ArgumentOutOfRangeException(nameof(vertexIndex), $"Must be <= {ushort.MaxValue} and >= {ushort.MinValue}.");
        if (handle is not TangentHandle.Negative and not TangentHandle.Positive)
            throw new ArgumentOutOfRangeException(nameof(handle), "Handle must be negative (0) or positive (1).");
        _data = ((ushort)roadIndex << 16) | (ushort)vertexIndex;
        _handle = handle;
    }
    public RoadTangentHandleIdentifier(ushort roadIndex, ushort vertexIndex, TangentHandle handle)
    {
        if (handle is not TangentHandle.Negative and not TangentHandle.Positive)
            throw new ArgumentOutOfRangeException(nameof(handle), "Handle must be negative (0) or positive (1).");
        _data = (roadIndex << 16) | vertexIndex;
        _handle = handle;
    }
    internal RoadTangentHandleIdentifier(int data, TangentHandle handle)
    {
        _data = data;
        _handle = handle;
    }
    public RoadTangentHandleIdentifier OtherHandle() => new RoadTangentHandleIdentifier(_data, (TangentHandle)(1 - (int)Handle));
    public bool Equals(RoadVertexIdentifier other) => other.RawData == _data;
    public bool Equals(RoadTangentHandleIdentifier other) => other._data == _data && other._handle == _handle;
    public int CompareTo(RoadVertexIdentifier other) => _data.CompareTo(other.RawData);
    public int CompareTo(RoadTangentHandleIdentifier other)
    {
        int c = _data.CompareTo(other._data);
        return c == 0 ? _handle.CompareTo(other._handle) : c;
    }
    public override bool Equals(object obj) => obj is RoadTangentHandleIdentifier id && Equals(id);
    public override int GetHashCode() => _data | ((int)_handle << 15);
    public static bool operator ==(RoadTangentHandleIdentifier left, RoadTangentHandleIdentifier right) => left.Equals(right);
    public static bool operator !=(RoadTangentHandleIdentifier left, RoadTangentHandleIdentifier right) => !left.Equals(right);

    public static implicit operator RoadVertexIdentifier(RoadTangentHandleIdentifier identifier) => new RoadVertexIdentifier(identifier._data);
    public static implicit operator TangentHandle(RoadTangentHandleIdentifier identifier) => identifier._handle;
    public override string ToString() => $"Road #{Road} / Vertex #{Vertex} / {Handle} Handle";
    public string Format(ITerminalFormatProvider provider) => $"Road #{Road.Format()} / Vertex #{Vertex.Format()} / {Handle.Format()} Handle".Colorize(FormattingColorType.Struct);
}