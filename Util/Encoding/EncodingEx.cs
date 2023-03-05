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
}