using System;
using System.Runtime.Serialization;

namespace DevkitServer.Util.Encoding;

[Serializable]
public class ByteBufferOverflowException : Exception
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
        typeof(Vector2), typeof(Vector3), typeof(Vector4), typeof(Quaternion), typeof(Color), typeof(Color32)
    };
    internal static readonly Type[] ValidArrayTypes =
    {
        typeof(ulong), typeof(float), typeof(long), typeof(ushort), typeof(short), typeof(byte), typeof(int), typeof(uint), typeof(bool), typeof(sbyte), typeof(decimal), typeof(char),
        typeof(double), typeof(string), typeof(DateTime), typeof(DateTimeOffset), typeof(Guid)
    };
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

    public static Bounds ReadInflatedBounds(this ByteReader reader) => ReadInflatedBounds(reader, out _);
    public static Bounds ReadInflatedBounds(this ByteReader reader, out bool extraFlag)
    {
        // 1 = sz full, 2 = sz ushort, 4 = pos full, 8 = pos ushort, 16 = ignore center y, 32 = ignore size y, 64 = preserve size signs
        byte flags = reader.ReadUInt8();
        extraFlag = (flags & 128) == 128;
        bool ignoreCenterY = (flags & 16) == 16, ignoreSizeY = (flags & 32) == 32, preserveSizeSigns = (flags & 64) == 64;
        Vector3 center;
        Vector3 size;
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
            size = !ignoreSizeY ? reader.ReadVector3() : new Vector3(reader.ReadFloat(), 0f, reader.ReadFloat());
        }
        else if ((flags & 2) == 2)
        {
            if (preserveSizeSigns)
                size = new Vector3(reader.ReadInt16(), ignoreSizeY ? 0 : reader.ReadInt16(), reader.ReadInt16());
            else
                size = new Vector3(reader.ReadUInt16(), ignoreSizeY ? 0 : reader.ReadUInt16(), reader.ReadUInt16());
        }
        else
        {
            if (preserveSizeSigns)
                size = new Vector3(reader.ReadInt8(), ignoreSizeY ? 0 : reader.ReadInt8(), reader.ReadInt8());
            else
                size = new Vector3(reader.ReadUInt8(), ignoreSizeY ? 0 : reader.ReadUInt8(), reader.ReadUInt8());
        }

        return new Bounds(center, size);
    }
    public static void WriteHeightmapBounds(this ByteWriter writer, Bounds bounds, bool extraFlag = false) => writer.WriteInflatedBounds(bounds, true, true, false, extraFlag);
    public static void WriteInflatedBounds(this ByteWriter writer, Bounds bounds, bool ignoreCenterY = false, bool ignoreSizeY = false, bool preserveSizeSigns = false, bool extraFlag = false)
    {
        // 1 = sz full, 2 = sz ushort, 4 = pos full, 8 = pos ushort, 16 = ignore center y, 32 = ignore size y, 64 = preserve size signs
        byte flags = 0;
        Vector3 c = bounds.center;
        Vector3 s = bounds.size;
        Expand(ref s, 0.5f);
        if (!preserveSizeSigns)
        {
            s = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
            if (s.x > ushort.MaxValue || s.y > ushort.MaxValue || s.z > ushort.MaxValue)
                flags = 1;
            else if (s.x > byte.MaxValue || s.y > byte.MaxValue || s.z > byte.MaxValue)
                flags = 2;
        }
        else
        {
            if (s.x > short.MaxValue || s.y > short.MaxValue || s.z > short.MaxValue)
                flags = 1;
            else if (s.x > sbyte.MaxValue || s.y > sbyte.MaxValue || s.z > sbyte.MaxValue)
                flags = 2;
        }

        if (c.x is > short.MaxValue or < short.MinValue || c.y is > short.MaxValue or < short.MinValue || s.z is > short.MaxValue or < short.MinValue)
            flags |= 4;
        else if (c.x is > sbyte.MaxValue or < sbyte.MinValue || c.y is > sbyte.MaxValue or < sbyte.MinValue || s.z is > sbyte.MaxValue or < sbyte.MinValue)
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
                writer.Write(c);
            else
            {
                writer.Write(c.x);
                writer.Write(c.z);
            }
        }
        else if ((flags & 8) == 8)
        {
            writer.Write((short)Mathf.RoundToInt(c.x));
            if (!ignoreCenterY)
                writer.Write((short)Mathf.RoundToInt(c.y));
            writer.Write((short)Mathf.RoundToInt(c.z));
        }
        else
        {
            writer.Write((sbyte)Mathf.RoundToInt(c.x));
            if (!ignoreCenterY)
                writer.Write((sbyte)Mathf.RoundToInt(c.y));
            writer.Write((sbyte)Mathf.RoundToInt(c.z));
        }

        if ((flags & 1) == 1)
        {
            s = bounds.size;
            if (!ignoreSizeY)
                writer.Write(s);
            else
            {
                writer.Write(s.x);
                writer.Write(s.z);
            }
        }
        else if ((flags & 2) == 2)
        {
            Expand(ref s, 0.5f);
            if (preserveSizeSigns)
            {
                writer.Write((short)Mathf.Clamp(CeilToIntIgnoreSign(s.x), short.MinValue, short.MaxValue));
                if (!ignoreCenterY)
                    writer.Write((short)Mathf.Clamp(CeilToIntIgnoreSign(s.y), short.MinValue, short.MaxValue));
                writer.Write((short)Mathf.Clamp(CeilToIntIgnoreSign(s.z), short.MinValue, short.MaxValue));
            }
            else
            {
                writer.Write((ushort)Mathf.Clamp(Mathf.CeilToInt(s.x), 0, ushort.MaxValue));
                if (!ignoreCenterY)
                    writer.Write((ushort)Mathf.Clamp(Mathf.CeilToInt(s.y), 0, ushort.MaxValue));
                writer.Write((ushort)Mathf.Clamp(Mathf.CeilToInt(s.z), 0, ushort.MaxValue));
            }
        }
        else
        {
            Expand(ref s, 0.5f);
            if (preserveSizeSigns)
            {
                writer.Write((sbyte)Mathf.Clamp(CeilToIntIgnoreSign(s.x), sbyte.MinValue, sbyte.MaxValue));
                if (!ignoreCenterY)
                    writer.Write((sbyte)Mathf.Clamp(CeilToIntIgnoreSign(s.y), sbyte.MinValue, sbyte.MaxValue));
                writer.Write((sbyte)Mathf.Clamp(CeilToIntIgnoreSign(s.z), sbyte.MinValue, sbyte.MaxValue));
            }
            else
            {
                writer.Write((byte)Mathf.Clamp(Mathf.CeilToInt(s.x), 0, byte.MaxValue));
                if (!ignoreCenterY)
                    writer.Write((byte)Mathf.Clamp(Mathf.CeilToInt(s.y), 0, byte.MaxValue));
                writer.Write((byte)Mathf.Clamp(Mathf.CeilToInt(s.z), 0, byte.MaxValue));
            }
        }
    }

    public static int CeilToIntIgnoreSign(float val) => val < 0 ? Mathf.FloorToInt(val) : Mathf.CeilToInt(val);
    public static int FloorToIntIgnoreSign(float val) => val < 0 ? Mathf.CeilToInt(val) : Mathf.FloorToInt(val);
    public static void Expand(ref Vector3 v3, float by)
    {
        if (v3.x < 0)
            v3.x -= by;
        else if (v3.x > 0)
            v3.x += by;

        if (v3.y < 0)
            v3.y -= by;
        else if (v3.y > 0)
            v3.y += by;

        if (v3.z < 0)
            v3.z -= by;
        else if (v3.z > 0)
            v3.z += by;
    }
}