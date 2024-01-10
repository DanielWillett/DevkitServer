using DevkitServer.Models;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Runtime.Serialization;
using DevkitServer.Multiplayer.Actions;

namespace DevkitServer.Util.Encoding;

[Serializable]
public class ByteEncoderException : Exception
{
    public ByteEncoderException() { }
    public ByteEncoderException(string message) : base(message) { }
    public ByteEncoderException(string message, Exception inner) : base(message, inner) { }
    protected ByteEncoderException(SerializationInfo info, StreamingContext context) : base(info, context) { }
}
[Serializable]
public class ByteBufferOverflowException : ByteEncoderException
{
    public ByteBufferOverflowException() { }
    public ByteBufferOverflowException(string message) : base(message) { }
    public ByteBufferOverflowException(string message, Exception inner) : base(message, inner) { }
    protected ByteBufferOverflowException(SerializationInfo info, StreamingContext context) : base(info, context) { }
}
public static class EncodingEx
{
    internal static readonly Type[] ValidTypes =
    {
        typeof(ulong), typeof(float), typeof(long), typeof(ushort), typeof(short), typeof(byte), typeof(int), typeof(uint), typeof(bool), typeof(char), typeof(sbyte), typeof(double),
        typeof(string), typeof(decimal), typeof(DateTime), typeof(DateTimeOffset), typeof(TimeSpan), typeof(Guid),
        typeof(Vector2), typeof(Vector3), typeof(Vector4), typeof(Quaternion), typeof(Color), typeof(Color32), typeof(Bounds), typeof(Type),
        typeof(RegionIdentifier), typeof(NetId), typeof(NetId64),
        typeof(ulong?), typeof(float?), typeof(long?), typeof(ushort?), typeof(short?), typeof(byte?), typeof(int?), typeof(uint?), typeof(bool?), typeof(char?), typeof(sbyte?),
        typeof(double?), typeof(decimal?), typeof(DateTime?), typeof(DateTimeOffset?), typeof(TimeSpan?), typeof(Guid?),
        typeof(Vector2?), typeof(Vector3?), typeof(Vector4?), typeof(Quaternion?), typeof(Color?), typeof(Color32?), typeof(Bounds?), typeof(NetId?), typeof(NetId64?)
    };
    internal static readonly Type[] ValidArrayTypes =
    {
        typeof(ulong), typeof(float), typeof(long), typeof(ushort), typeof(short), typeof(byte), typeof(int), typeof(uint), typeof(bool), typeof(sbyte), typeof(decimal), typeof(char),
        typeof(double), typeof(string), typeof(DateTime), typeof(DateTimeOffset), typeof(Guid), typeof(Type)
    };

    /// <summary>
    /// Max size of a terrain tool <see cref="Bounds"/> written with <see cref="WriteTerrainToolBounds"/>.
    /// </summary>
    public const int MaxTerrainToolBoundsSize = MaxInflatedBoundsSizeNoYComponents;

    /// <summary>
    /// Max size of a <see cref="Bounds"/> written with <see cref="WriteInflatedBounds"/>.
    /// </summary>
    public const int MaxInflatedBoundsSize = 25;

    /// <summary>
    /// Max size of a <see cref="Bounds"/> written with <see cref="WriteInflatedBounds"/> with either a dropped size or position for the y axis.
    /// </summary>
    public const int MaxInflatedBoundsSizeMinusOneYComponent = 21;

    /// <summary>
    /// Max size of a <see cref="Bounds"/> written with <see cref="WriteInflatedBounds"/> with either a dropped size and position for the y axis.
    /// </summary>
    public const int MaxInflatedBoundsSizeNoYComponents = 17;

    /// <summary>
    /// Max value of an int24 (used with <see cref="ByteWriter.WriteInt24"/> and <see cref="ByteReader.ReadInt24"/>).
    /// </summary>
    /// <remarks>Minimum is just this but negative.</remarks>
    public const int Int24MaxValue = 8388607;

    /// <summary>
    /// Min value of an int24 (used with <see cref="ByteWriter.WriteInt24"/> and <see cref="ByteReader.ReadInt24"/>).
    /// </summary>
    /// <remarks>Maximum is just this but positive.</remarks>
    public const int Int24MinValue = -Int24MaxValue;

    /// <summary>
    /// A list of all (non-array) types that can be automatically read and written by <see cref="ByteReader"/> and <see cref="ByteWriter"/>. 
    /// </summary>
    /// <remarks>Includes nullable types as separate entries. Enums are not included but are all also valid.</remarks>
    public static readonly IReadOnlyList<Type> ValidReadWriteTypes = new ReadOnlyCollection<Type>(ValidTypes);

    /// <summary>
    /// A list of all array types that can be automatically read and written by <see cref="ByteReader"/> and <see cref="ByteWriter"/>. 
    /// </summary>
    /// <remarks>The types in this array represent the element type, not the actual array type. Enums are not included but are all also valid.</remarks>
    public static readonly IReadOnlyList<Type> ValidArrayReadWriteTypes = new ReadOnlyCollection<Type>(ValidArrayTypes);

    /// <summary>
    /// Checks if a type can be automatically read and written by <see cref="ByteReader"/> and <see cref="ByteWriter"/>.
    /// </summary>
    public static bool IsValidAutoType(Type type)
    {
        if (type.IsEnum) return true;
        if (type.IsArray)
        {
            type = type.GetElementType()!;
            return type != null && ValidArrayTypes.Contains(type);
        }
        return ValidTypes.Contains(type);
    }

    /// <summary>
    /// Reads a bounds inflated to be written as an integer in a compressed manner.
    /// </summary>
    public static Bounds ReadInflatedBounds(this ByteReader reader) => ReadInflatedBounds(reader, out _);

    /// <summary>
    /// Reads a bounds inflated to be written as an integer in a compressed manner. Includes an extra bit that you can use for anything.
    /// </summary>
    public static Bounds ReadInflatedBounds(this ByteReader reader, out bool extraFlag)
    {
        // 1 = sz full, 2 = sz ushort, 4 = pos full, 8 = pos ushort, 16 = ignore center y, 32 = ignore size y, 64 = preserve size signs, 128 = extra flag
        byte flags = reader.ReadUInt8();
        extraFlag = (flags & 128) != 0;
        bool ignoreCenterY = (flags & 16) == 16, ignoreSizeY = (flags & 32) == 32, preserveSizeSigns = (flags & 64) == 64;
        Vector3 center;
        Vector3 extents;
        if ((flags & 4) == 4)
        {
            center = !ignoreCenterY ? reader.ReadVector3() : new Vector3(reader.ReadFloat(), 0f, reader.ReadFloat());
        }
        else if ((flags & 8) == 8)
        {
            center = new Vector3(reader.ReadInt16(), ignoreCenterY ? 0 : reader.ReadInt16(), reader.ReadInt16());
        }
        else
        {
            center = new Vector3(reader.ReadInt8(), ignoreCenterY ? 0 : reader.ReadInt8(), reader.ReadInt8());
        }

        if ((flags & 1) == 1)
        {
            extents = !ignoreSizeY ? reader.ReadVector3() : new Vector3(reader.ReadFloat(), 0f, reader.ReadFloat());
        }
        else if ((flags & 2) == 2)
        {
            if (preserveSizeSigns)
                extents = new Vector3(reader.ReadInt16(), ignoreSizeY ? 0 : reader.ReadInt16(), reader.ReadInt16());
            else
                extents = new Vector3(reader.ReadUInt16(), ignoreSizeY ? 0 : reader.ReadUInt16(), reader.ReadUInt16());
        }
        else
        {
            if (preserveSizeSigns)
                extents = new Vector3(reader.ReadInt8(), ignoreSizeY ? 0 : reader.ReadInt8(), reader.ReadInt8());
            else
                extents = new Vector3(reader.ReadUInt8(), ignoreSizeY ? 0 : reader.ReadUInt8(), reader.ReadUInt8());
        }
        
        return new Bounds(center, extents * 2);
    }

    /// <summary>
    /// Uses <see cref="WriteInflatedBounds"/> with settings meant for bounds used with terrain brushes. Includes an extra bit that you can use for anything.
    /// </summary>
    /// <remarks>Read with <see cref="ReadInflatedBounds(ByteReader,out bool)"/> or <see cref="ReadInflatedBounds(ByteReader)"/>.</remarks>
    public static void WriteTerrainToolBounds(this ByteWriter writer, Bounds bounds, bool extraFlag = false)
    {
        writer.WriteInflatedBounds(bounds, true, true, false, extraFlag);
    }

    /// <summary>
    /// Writes bounds inflated to be written as an integer in a compressed manner. Includes an extra bit that you can use for anything, and options about ignoring certain axis and signs, which compresses it further.
    /// </summary>
    public static void WriteInflatedBounds(this ByteWriter writer, Bounds bounds, bool ignoreCenterY = false, bool ignoreSizeY = false, bool preserveSizeSigns = false, bool extraFlag = false)
    {
        // 1 = sz full, 2 = sz ushort, 4 = pos full, 8 = pos ushort, 16 = ignore center y, 32 = ignore size y, 64 = preserve size signs, 128 = extra flag
        byte flags = (byte)(extraFlag ? 128 : 0);
        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;
        extents.Expand(0.5f);
        if (!preserveSizeSigns)
        {
            extents = new Vector3(Mathf.Abs(extents.x), Mathf.Abs(extents.y), Mathf.Abs(extents.z));
            if (extents.x > ushort.MaxValue || (!ignoreSizeY && extents.y > ushort.MaxValue) || extents.z > ushort.MaxValue)
                flags = 1;
            else if (extents.x > byte.MaxValue || (!ignoreSizeY && extents.y > byte.MaxValue) || extents.z > byte.MaxValue)
                flags = 2;
        }
        else
        {
            if (extents.x is > short.MaxValue or < short.MinValue || (!ignoreSizeY && extents.y is > short.MaxValue or < short.MinValue) || extents.z is > short.MaxValue or < short.MinValue)
                flags = 1;
            else if (extents.x is > sbyte.MaxValue or < sbyte.MinValue || (!ignoreSizeY && extents.y is > sbyte.MaxValue or < sbyte.MinValue) || extents.z is > sbyte.MaxValue or < sbyte.MinValue)
                flags = 2;
        }

        if (center.x is > short.MaxValue or < short.MinValue || (!ignoreCenterY && center.y is > short.MaxValue or < short.MinValue) || center.z is > short.MaxValue or < short.MinValue)
            flags |= 4;
        else if (center.x is > sbyte.MaxValue or < sbyte.MinValue || (!ignoreCenterY && center.y is > sbyte.MaxValue or < sbyte.MinValue) || center.z is > sbyte.MaxValue or < sbyte.MinValue)
            flags |= 8;

        if (ignoreCenterY)
            flags |= 16;

        if (ignoreSizeY)
            flags |= 32;

        if (preserveSizeSigns)
            flags |= 64;

        writer.Write(flags);
        if ((flags & 4) == 4)
        {
            if (!ignoreCenterY)
                writer.Write(center);
            else
            {
                writer.Write(center.x);
                writer.Write(center.z);
            }
        }
        else if ((flags & 8) == 8)
        {
            writer.Write((short)Mathf.RoundToInt(center.x));
            if (!ignoreCenterY)
                writer.Write((short)Mathf.RoundToInt(center.y));
            writer.Write((short)Mathf.RoundToInt(center.z));
        }
        else
        {
            writer.Write((sbyte)Mathf.RoundToInt(center.x));
            if (!ignoreCenterY)
                writer.Write((sbyte)Mathf.RoundToInt(center.y));
            writer.Write((sbyte)Mathf.RoundToInt(center.z));
        }

        if ((flags & 1) == 1)
        {
            if (!ignoreSizeY)
                writer.Write(extents);
            else
            {
                writer.Write(extents.x);
                writer.Write(extents.z);
            }
        }
        else if ((flags & 2) == 2)
        {
            extents.Expand(0.5f);
            if (preserveSizeSigns)
            {
                writer.Write((short)Mathf.Clamp(extents.x.CeilToIntIgnoreSign(), short.MinValue, short.MaxValue));
                if (!ignoreCenterY)
                    writer.Write((short)Mathf.Clamp(extents.y.CeilToIntIgnoreSign(), short.MinValue, short.MaxValue));
                writer.Write((short)Mathf.Clamp(extents.z.CeilToIntIgnoreSign(), short.MinValue, short.MaxValue));
            }
            else
            {
                writer.Write((ushort)Mathf.Clamp(Mathf.CeilToInt(extents.x), 0, ushort.MaxValue));
                if (!ignoreCenterY)
                    writer.Write((ushort)Mathf.Clamp(Mathf.CeilToInt(extents.y), 0, ushort.MaxValue));
                writer.Write((ushort)Mathf.Clamp(Mathf.CeilToInt(extents.z), 0, ushort.MaxValue));
            }
        }
        else
        {
            extents.Expand(0.5f);
            if (preserveSizeSigns)
            {
                writer.Write((sbyte)Mathf.Clamp(extents.x.CeilToIntIgnoreSign(), sbyte.MinValue, sbyte.MaxValue));
                if (!ignoreCenterY)
                    writer.Write((sbyte)Mathf.Clamp(extents.y.CeilToIntIgnoreSign(), sbyte.MinValue, sbyte.MaxValue));
                writer.Write((sbyte)Mathf.Clamp(extents.z.CeilToIntIgnoreSign(), sbyte.MinValue, sbyte.MaxValue));
            }
            else
            {
                writer.Write((byte)Mathf.Clamp(Mathf.CeilToInt(extents.x), 0, byte.MaxValue));
                if (!ignoreCenterY)
                    writer.Write((byte)Mathf.Clamp(Mathf.CeilToInt(extents.y), 0, byte.MaxValue));
                writer.Write((byte)Mathf.Clamp(Mathf.CeilToInt(extents.z), 0, byte.MaxValue));
            }
        }
    }

    /// <summary>
    /// Reads an object array from a <see cref="ByteReader"/> as accurately as possible. Works with all primitive types and some other types valid in <see cref="IsValidAutoType"/>.
    /// </summary>
    public static object?[] ReadFormattingParameters(this ByteReader reader)
    {
        int ct = reader.ReadUInt8();
        if (ct == 0)
            return Array.Empty<object>();

        object?[] parameters = new object?[ct];
        for (int i = 0; i < ct; ++i)
        {
            TypeCode code = (TypeCode)reader.ReadUInt8();
            if (code == TypeCode.Object)
            {
                Type? type = reader.ReadType();
                if (type == null)
                    continue;
                MethodInfo? readerMethod = ByteReader.GetReadMethod(type);

                parameters[i] = readerMethod?.Invoke(reader, Array.Empty<object>());
                continue;
            }

            parameters[i] = code switch
            {
                TypeCode.DBNull => DBNull.Value,
                TypeCode.Boolean => reader.ReadBool(),
                TypeCode.Char => reader.ReadChar(),
                TypeCode.SByte => reader.ReadInt8(),
                TypeCode.Byte => reader.ReadUInt8(),
                TypeCode.Int16 => reader.ReadInt16(),
                TypeCode.UInt16 => reader.ReadUInt16(),
                TypeCode.Int32 => reader.ReadInt32(),
                TypeCode.UInt32 => reader.ReadUInt32(),
                TypeCode.Int64 => reader.ReadInt64(),
                TypeCode.UInt64 => reader.ReadUInt64(),
                TypeCode.Single => reader.ReadFloat(),
                TypeCode.Double => reader.ReadDouble(),
                TypeCode.Decimal => reader.ReadDecimal(),
                TypeCode.DateTime => reader.ReadDateTimeOffset().DateTime,
                TypeCode.String => reader.ReadString(),
                _ => parameters[i]
            };
        }

        return parameters;
    }

    /// <summary>
    /// Writes an object array to a <see cref="ByteWriter"/> as accurately as possible. Works with all primitive types and some other types valid in <see cref="IsValidAutoType"/>.
    /// </summary>
    /// <remarks>If the type can't be converted automatically, <see cref="object.ToString"/> is called on it and it's sent as a string.</remarks>
    public static void WriteFormattingParameters(this ByteWriter writer, object?[]? parameters)
    {
        if (parameters is not { Length: > 0 })
        {
            writer.Write((byte)0);
            return;
        }

        int ct = Math.Min(byte.MaxValue, parameters.Length);
        writer.Write((byte)ct);
        for (int i = 0; i < ct; ++i)
        {
            object? value = parameters[i];
            TypeCode typeCode = Convert.GetTypeCode(value);
            if (typeCode is not TypeCode.Object)
            {
                writer.Write((byte)typeCode);
                switch (typeCode)
                {
                    case TypeCode.Boolean:
                        writer.Write((bool)value!);
                        break;
                    case TypeCode.Char:
                        writer.Write((char)value!);
                        break;
                    case TypeCode.SByte:
                        writer.Write((sbyte)value!);
                        break;
                    case TypeCode.Byte:
                        writer.Write((byte)value!);
                        break;
                    case TypeCode.Int16:
                        writer.Write((short)value!);
                        break;
                    case TypeCode.UInt16:
                        writer.Write((ushort)value!);
                        break;
                    case TypeCode.Int32:
                        writer.Write((int)value!);
                        break;
                    case TypeCode.UInt32:
                        writer.Write((uint)value!);
                        break;
                    case TypeCode.Int64:
                        writer.Write((long)value!);
                        break;
                    case TypeCode.UInt64:
                        writer.Write((ulong)value!);
                        break;
                    case TypeCode.Single:
                        writer.Write((float)value!);
                        break;
                    case TypeCode.Double:
                        writer.Write((double)value!);
                        break;
                    case TypeCode.Decimal:
                        writer.Write((decimal)value!);
                        break;
                    case TypeCode.DateTime:
                        DateTime dt = (DateTime)value!;
                        writer.Write(new DateTimeOffset(dt));
                        break;
                    case TypeCode.String:
                        writer.Write((string)value!);
                        break;
                }
            }
            else
            {
                Type type = value!.GetType();
                MethodInfo? writerMethod = ByteWriter.GetWriteMethod(type);
                if (writerMethod == null)
                {
                    writer.Write((byte)TypeCode.String);
                    writer.Write(value.ToString());
                }
                else
                {
                    writer.Write((byte)TypeCode.Object);
                    writer.Write(type);
                    writerMethod.Invoke(writer, new object[] { value });
                }
            }
        }
    }
}