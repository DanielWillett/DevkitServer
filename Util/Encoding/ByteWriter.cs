using DevkitServer.Multiplayer.Networking;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DevkitServer.Models;
using DevkitServer.API;

namespace DevkitServer.Util.Encoding;

/// <summary>
/// Fast encoding to a byte array to data. Also works with <see cref="System.IO.Stream"/>s. Similar to the <see cref="River"/> class.
/// </summary>
public class ByteWriter
{
    private static readonly bool IsBigEndian = !BitConverter.IsLittleEndian;
    private static Dictionary<Type, MethodInfo>? _nonNullableWriters;
    private static Dictionary<Type, MethodInfo>? _nullableWriters;
    private static readonly MethodInfo WriteEnumMethod = typeof(ByteWriter).GetMethod(nameof(WriteEnum), BindingFlags.Instance | BindingFlags.NonPublic)
                                                            ?? throw new MemberAccessException("Unable to find write enum method.");
    private static readonly MethodInfo WriteNullableEnumMethod = typeof(ByteWriter).GetMethod(nameof(WriteNullableEnum), BindingFlags.Instance | BindingFlags.NonPublic)
                                                                 ?? throw new MemberAccessException("Unable to find write nullable enum method.");
    private int _size;
    private int _maxSize;
    private byte[] _buffer;
    private bool _streamMode;
    private Stream? _stream;
    public byte[] Buffer { get => _buffer; set => _buffer = value; }
    public Stream? Stream
    {
        get => _stream;
        set
        {
            if (value is not null)
            {
                if (!value.CanWrite)
                    throw new ArgumentException("Stream can not write.", nameof(value));
                _stream = value;
                _streamMode = true;
                _size = 0;
            }
            else
            {
                _stream = null;
                _streamMode = false;
                _size = 0;
            }
        }
    }
    public int Count { get => _size; set => _size = value; }
    public int BaseCapacity { get; set; }
    public bool AllowPrepending { get; set; }
    internal ByteWriter(bool allowPrepending = true, int capacity = 0)
    {
        BaseCapacity = capacity;
        _buffer = BaseCapacity < 1 ? Array.Empty<byte>() : new byte[BaseCapacity];
        AllowPrepending = allowPrepending;
    }
    public ByteWriter(int capacity = 0) : this(false, capacity) { }
    private static void PrepareMethods()
    {
        _nonNullableWriters ??= new Dictionary<Type, MethodInfo>(44)
        {
            { typeof(int), GetMethod(typeof(int)) },
            { typeof(uint), GetMethod(typeof(uint)) },
            { typeof(byte), GetMethod(typeof(byte)) },
            { typeof(sbyte), GetMethod(typeof(sbyte)) },
            { typeof(bool), GetMethod(typeof(bool)) },
            { typeof(long), GetMethod(typeof(long)) },
            { typeof(ulong), GetMethod(typeof(ulong)) },
            { typeof(short), GetMethod(typeof(short)) },
            { typeof(ushort), GetMethod(typeof(ushort)) },
            { typeof(float), GetMethod(typeof(float)) },
            { typeof(decimal), GetMethod(typeof(decimal)) },
            { typeof(double), GetMethod(typeof(double)) },
            { typeof(char), GetMethod(typeof(char)) },
            { typeof(string), GetMethod(typeof(string)) },
            { typeof(Type), GetMethod(typeof(Type)) },
            { typeof(Type[]), GetMethod(typeof(Type[])) },
            { typeof(RegionIdentifier), typeof(RegionIdentifier).GetMethod(nameof(RegionIdentifier.Write), BindingFlags.Static | BindingFlags.Public) },
            { typeof(DateTime), GetMethod(typeof(DateTime)) },
            { typeof(DateTimeOffset), GetMethod(typeof(DateTimeOffset)) },
            { typeof(TimeSpan), GetMethod(typeof(TimeSpan)) },
            { typeof(Guid), GetMethod(typeof(Guid)) },
            { typeof(Vector2), GetMethod(typeof(Vector2)) },
            { typeof(Vector3), GetMethod(typeof(Vector3)) },
            { typeof(Vector4), GetMethod(typeof(Vector4)) },
            { typeof(Bounds), GetMethod(typeof(Bounds)) },
            { typeof(Quaternion), GetMethod(typeof(Quaternion)) },
            { typeof(Color), GetMethod(typeof(Color)) },
            { typeof(Color32), GetMethod(typeof(Color32)) },
            { typeof(Guid[]), GetMethod(typeof(Guid[])) },
            { typeof(DateTime[]), GetMethod(typeof(DateTime[])) },
            { typeof(DateTimeOffset[]), GetMethod(typeof(DateTimeOffset[])) },
            { typeof(byte[]), GetMethod(typeof(byte[])) },
            { typeof(sbyte[]), GetMethod(typeof(sbyte[])) },
            { typeof(int[]), GetMethod(typeof(int[])) },
            { typeof(uint[]), GetMethod(typeof(uint[])) },
            { typeof(bool[]), GetMethod(typeof(bool[])) },
            { typeof(long[]), GetMethod(typeof(long[])) },
            { typeof(ulong[]), GetMethod(typeof(ulong[])) },
            { typeof(short[]), GetMethod(typeof(short[])) },
            { typeof(ushort[]), GetMethod(typeof(ushort[])) },
            { typeof(float[]), GetMethod(typeof(float[])) },
            { typeof(double[]), GetMethod(typeof(double[])) },
            { typeof(decimal[]), GetMethod(typeof(decimal[])) },
            { typeof(char[]), GetMethod(typeof(char[])) },
            { typeof(string[]), GetMethod(typeof(string[])) },
            { typeof(NetId), GetMethod(typeof(NetId)) }
        };

        _nullableWriters ??= new Dictionary<Type, MethodInfo>(44)
        {
            { typeof(int?), GetNullableMethod(typeof(int?)) },
            { typeof(uint?), GetNullableMethod(typeof(uint?)) },
            { typeof(byte?), GetNullableMethod(typeof(byte?)) },
            { typeof(sbyte?), GetNullableMethod(typeof(sbyte?)) },
            { typeof(bool?), GetNullableMethod(typeof(bool?)) },
            { typeof(long?), GetNullableMethod(typeof(long?)) },
            { typeof(ulong?), GetNullableMethod(typeof(ulong?)) },
            { typeof(short?), GetNullableMethod(typeof(short?)) },
            { typeof(ushort?), GetNullableMethod(typeof(ushort?)) },
            { typeof(float?), GetNullableMethod(typeof(float?)) },
            { typeof(decimal?), GetNullableMethod(typeof(decimal?)) },
            { typeof(double?), GetNullableMethod(typeof(double?)) },
            { typeof(char?), GetNullableMethod(typeof(char?)) },
            { typeof(string), GetNullableMethod(typeof(string)) },
            { typeof(Type), GetMethod(typeof(Type)) },
            { typeof(Type[]), GetMethod(typeof(Type[])) },
            { typeof(DateTime?), GetNullableMethod(typeof(DateTime?)) },
            { typeof(DateTimeOffset?), GetNullableMethod(typeof(DateTimeOffset?)) },
            { typeof(TimeSpan?), GetNullableMethod(typeof(TimeSpan?)) },
            { typeof(Guid?), GetNullableMethod(typeof(Guid?)) },
            { typeof(Vector2?), GetNullableMethod(typeof(Vector2?)) },
            { typeof(Vector3?), GetNullableMethod(typeof(Vector3?)) },
            { typeof(Vector4?), GetNullableMethod(typeof(Vector4?)) },
            { typeof(Bounds?), GetNullableMethod(typeof(Bounds?)) },
            { typeof(Quaternion?), GetNullableMethod(typeof(Quaternion?)) },
            { typeof(Color?), GetNullableMethod(typeof(Color?)) },
            { typeof(Color32?), GetNullableMethod(typeof(Color32?)) },
            { typeof(Guid[]), GetNullableMethod(typeof(Guid[])) },
            { typeof(DateTime[]), GetNullableMethod(typeof(DateTime[])) },
            { typeof(DateTimeOffset[]), GetNullableMethod(typeof(DateTimeOffset[])) },
            { typeof(byte[]), GetNullableMethod(typeof(byte[])) },
            { typeof(sbyte[]), GetNullableMethod(typeof(sbyte[])) },
            { typeof(int[]), GetNullableMethod(typeof(int[])) },
            { typeof(uint[]), GetNullableMethod(typeof(uint[])) },
            { typeof(bool[]), GetNullableMethod(typeof(bool[])) },
            { typeof(long[]), GetNullableMethod(typeof(long[])) },
            { typeof(ulong[]), GetNullableMethod(typeof(ulong[])) },
            { typeof(short[]), GetNullableMethod(typeof(short[])) },
            { typeof(ushort[]), GetNullableMethod(typeof(ushort[])) },
            { typeof(float[]), GetNullableMethod(typeof(float[])) },
            { typeof(double[]), GetNullableMethod(typeof(double[])) },
            { typeof(decimal[]), GetNullableMethod(typeof(decimal[])) },
            { typeof(char[]), GetNullableMethod(typeof(char[])) },
            { typeof(string[]), GetNullableMethod(typeof(string[])) },
            { typeof(NetId?), GetNullableMethod(typeof(NetId?)) }
        };
        
        return;

        MethodInfo GetMethod(Type writeType) => typeof(ByteWriter).GetMethod("Write", BindingFlags.Instance | BindingFlags.Public, null, new Type[] { writeType }, null)
                                                ?? throw new MemberAccessException("Unable to find write method for " + writeType.Name + ".");

        MethodInfo GetNullableMethod(Type writeType) => typeof(ByteWriter).GetMethod("WriteNullable", BindingFlags.Instance | BindingFlags.Public, null, new Type[] { writeType }, null)
                                                        ?? throw new MemberAccessException("Unable to find nullable write method for " + writeType.Name + ".");
    }
    /// <summary>The value of <paramref name="overhead"/>.<see cref="MessageOverhead.Size">Size</see> will be overwritten.</summary>
    public unsafe void PrependOverhead(ref MessageOverhead overhead)
    {
        MessageOverhead.SetSize(ref overhead, _size);
        if (!AllowPrepending)
            return;

        if (_streamMode)
            throw new NotSupportedException("Prepending is not supported in stream mode.");

        if (_size == 0)
        {
            _buffer = overhead.GetBytes();
            return;
        }

        int ttl = overhead.Length;
        byte* overheadBytes = stackalloc byte[ttl];
        overhead.GetBytes(overheadBytes, out _);

        if (_size + ttl <= _buffer.Length)
        {
            fixed (byte* ptr = _buffer)
            {
                System.Buffer.MemoryCopy(ptr, ptr + ttl, _buffer.Length - ttl, _size);
                System.Buffer.MemoryCopy(overheadBytes, ptr, ttl, ttl);
            }
        }
        else
        {
            byte[] old = _buffer;
            _buffer = new byte[_size + ttl];
            System.Buffer.BlockCopy(old, 0, _buffer, ttl, _size);
            for (int i = 0; i < ttl; ++i)
                _buffer[i] = overheadBytes[i];
        }

        _size += ttl;
    }
    public byte[] ToArray()
    {
        if (_streamMode)
            throw new NotSupportedException("Exporting to an array is not supported in stream mode.");
        byte[] rtn = new byte[_size];
        System.Buffer.BlockCopy(_buffer, 0, rtn, 0, _size);
        return rtn;
    }

    /// <summary>
    /// This can be more resource-intensive when used on a reusable writer as the buffer will have to be reallocated afterwards.
    /// </summary>
    public ArraySegment<byte> ToArraySegmentAndFlush()
    {
        if (_streamMode)
            throw new NotSupportedException("Exporting to an array segment is not supported in stream mode.");

        ArraySegment<byte> segment = new ArraySegment<byte>(_buffer, 0, _size);
        _buffer = Array.Empty<byte>();
        _size = 0;
        return segment;
    }

    /// <summary>
    /// This can be dangerous if the data is used after the next time the writer is used.
    /// </summary>
    public ArraySegment<byte> ToArraySegmentAndDontFlush()
    {
        if (_streamMode)
            throw new NotSupportedException("Exporting to an array segment is not supported in stream mode.");

        return new ArraySegment<byte>(_buffer, 0, _size);
    }
    public void ExtendBuffer(int newsize)
    {
        if (_streamMode)
            throw new NotSupportedException("Resizing the buffer is not supported in stream mode.");
        ExtendBufferIntl(newsize);
    }
    public void ExtendBufferFor(int byteCount)
    {
        if (_streamMode)
            throw new NotSupportedException("Resizing the buffer is not supported in stream mode.");
        ExtendBufferIntl(byteCount + _size);
    }
    private void ExtendBufferIntl(int newsize)
    {
        if (newsize <= _buffer.Length)
            return;
        if (_size == 0)
            _buffer = new byte[newsize];
        else
        {
            byte[] old = _buffer;
            int sz2 = old.Length;
            int sz = sz2 + sz2 / 2;
            if (sz < newsize) sz = newsize;
            _buffer = new byte[sz];
            System.Buffer.BlockCopy(old, 0, _buffer, 0, _size);
        }
    }

    public void BackTrack(int position)
    {
        if (_streamMode)
            throw new NotSupportedException("BackTrack and Return are not supported in stream mode.");
        if (position < 0)
            position = 0;
        if (position > _size)
            throw new ArgumentOutOfRangeException(nameof(position));
        if (position == _size)
            return;

        _maxSize = _size;
        _size = position;
    }
    public void Return()
    {
        if (_streamMode)
            throw new NotSupportedException("BackTrack and Return are not supported in stream mode.");
        if (_maxSize == 0)
            throw new InvalidOperationException("You must call BackTrack before calling Return.");

        if (_size < _maxSize)
            _size = _maxSize;
        _maxSize = 0;
    }

    /// <summary>Use with caution, may not be consistant with structs that do not have an explicit layout.</summary>
    public unsafe void WriteStruct<T>(in T value) where T : unmanaged
    {
        if (_streamMode)
        {
            int size = sizeof(T);
            if (_buffer.Length < size)
                _buffer = new byte[size];
            fixed (byte* ptr = _buffer)
            {
                *(T*)ptr = value;
                EndianCheck(ptr, sizeof(T));
            }
            _stream!.Write(_buffer, 0, size);
            _size += size;
            return;
        }
        int newsize = _size + sizeof(T);
        if (newsize > _buffer.Length)
            ExtendBufferIntl(newsize);
        fixed (byte* ptr = &_buffer[_size])
        {
            *(T*)ptr = value;
            EndianCheck(ptr, sizeof(T));
        }
        _size = newsize;
    }
    public unsafe void WriteBlock(byte value, int count)
    {
        if (count == 0)
            return;
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count));
        if (_streamMode)
        {
            if (_buffer == null || _buffer.Length < 1)
                _buffer = new byte[BaseCapacity <= 0 ? Math.Min(128, count) : 0];

            if (value != 0)
            {
                fixed (byte* ptr = _buffer)
                {
                    int c = Math.Min(count, _buffer.Length);
                    for (int i = 0; i < c; ++i)
                        ptr[i] = value;
                }
            }

            _size += count;
            while (count > 0)
            {
                int c = Math.Min(count, _buffer.Length);
                _stream!.Write(_buffer, 0, c);
                count -= c;
            }
            return;
        }
        int newsize = _size + count;
        if (newsize > _buffer.Length)
            ExtendBufferIntl(newsize);
        if (value != 0)
        {
            fixed (byte* ptr = &_buffer[_size])
            {
                for (int i = 0; i < count; ++i)
                    ptr[i] = value;
            }
        }
        _size = newsize;
    }
    private static unsafe void Reverse(byte* litEndStrt, int size)
    {
        byte* stack = stackalloc byte[size];
        for (int i = 0; i < size; ++i)
            stack[i] = litEndStrt[i];
        for (int i = 0; i < size; i++)
            litEndStrt[i] = stack[size - i - 1];
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static unsafe void EndianCheck(byte* litEndStrt, int size)
    {
        if (size > 1 && IsBigEndian) Reverse(litEndStrt, size);
    }
    private unsafe void WriteInternal<T>(T value) where T : unmanaged
    {
        int size = sizeof(T);
        if (size == 1)
        {
            if (_streamMode)
            {
                _stream!.WriteByte(*(byte*)&value);
                ++_size;
                return;
            }

            int newsize = _size + 1;
            if (newsize > _buffer.Length)
                ExtendBufferIntl(newsize);

            _buffer[_size] = *(byte*)&value;
            _size = newsize;
        }
        else
        {
            if (_streamMode)
            {
                if (_buffer.Length < size)
                    _buffer = new byte[size];
                fixed (byte* ptr = _buffer)
                {
                    *(T*)ptr = value;
                    EndianCheck(ptr, size);
                }
                _stream!.Write(_buffer, 0, size);
                _size += size;
                return;
            }
            int newsize = _size + size;
            if (newsize > _buffer.Length)
                ExtendBufferIntl(newsize);
            fixed (byte* ptr = &_buffer[_size])
            {
                *(T*)ptr = value;
                EndianCheck(ptr, size);
            }
            _size = newsize;
        }
    }
    private unsafe void WriteInternal<T>(T[] value) where T : unmanaged
    {
        int objSize = sizeof(T);
        ushort len = (ushort)Math.Min(value.Length, ushort.MaxValue);
        if (_streamMode)
        {
            int size = sizeof(ushort) + objSize * len;
            if (_buffer.Length < size)
                _buffer = new byte[size];
            fixed (byte* ptr = _buffer)
            {
                *(ushort*)ptr = len;
                EndianCheck(ptr, sizeof(ushort));
                for (int i = 0; i < len; ++i)
                {
                    byte* ptr2 = ptr + sizeof(ushort) + i * objSize;
                    *(T*)ptr2 = value[i];
                    EndianCheck(ptr2, objSize);
                }
            }
            _stream!.Write(_buffer, 0, size);
            _size += size;
            return;
        }
        int newsize = _size + sizeof(ushort) + objSize * len;
        if (newsize > _buffer.Length)
            ExtendBufferIntl(newsize);
        fixed (byte* ptr = &_buffer[_size])
        {
            *(ushort*)ptr = len;
            EndianCheck(ptr, sizeof(ushort));
            for (int i = 0; i < len; ++i)
            {
                byte* ptr2 = ptr + sizeof(ushort) + i * objSize;
                *(T*)ptr2 = value[i];
                EndianCheck(ptr2, objSize);
            }
        }
        _size = newsize;
    }
    public void Write(int n) => WriteInternal(n);
    public void WriteNullable(int? n)
    {
        if (n.HasValue)
        {
            Write(true);
            WriteInternal(n.Value);
        }
        else Write(false);
    }
    public void Write(uint n) => WriteInternal(n);
    public void WriteNullable(uint? n)
    {
        if (n.HasValue)
        {
            Write(true);
            WriteInternal(n.Value);
        }
        else Write(false);
    }
    public void Write(NetId n) => WriteInternal(n.id);
    public void WriteNullable(NetId? n) => WriteNullable(n?.id);
    public void Write(byte n) => WriteInternal(n);
    public void WriteNullable(byte? n)
    {
        if (n.HasValue)
        {
            Write(true);
            WriteInternal(n.Value);
        }
        else Write(false);
    }
    public void Write(sbyte n) => WriteInternal(unchecked((byte)n));
    public void WriteNullable(sbyte? n)
    {
        if (n.HasValue)
        {
            Write(true);
            WriteInternal(unchecked((byte)n.Value));
        }
        else Write(false);
    }
    public void Write(bool n) => WriteInternal(n ? (byte)1 : (byte)0);
    public void WriteNullable(bool? n)
    {
        if (n.HasValue)
        {
            Write(true);
            WriteInternal(n.Value);
        }
        else Write(false);
    }
    public void Write(long n) => WriteInternal(n);
    public void WriteNullable(long? n)
    {
        if (n.HasValue)
        {
            Write(true);
            WriteInternal(n.Value);
        }
        else Write(false);
    }
    public void Write(ulong n) => WriteInternal(n);
    public void WriteNullable(ulong? n)
    {
        if (n.HasValue)
        {
            Write(true);
            WriteInternal(n.Value);
        }
        else Write(false);
    }
    public void Write(short n) => WriteInternal(n);
    public void WriteNullable(short? n)
    {
        if (n.HasValue)
        {
            Write(true);
            WriteInternal(n.Value);
        }
        else Write(false);
    }
    public void Write(ushort n) => WriteInternal(n);
    public void WriteNullable(ushort? n)
    {
        if (n.HasValue)
        {
            Write(true);
            WriteInternal(n.Value);
        }
        else Write(false);
    }
    public void WriteInt24(int n)
    {
        if (n > EncodingEx.Int24MaxValue)
            n = EncodingEx.Int24MaxValue;
        if (n < -EncodingEx.Int24MaxValue)
            n = -EncodingEx.Int24MaxValue;
        n += EncodingEx.Int24MaxValue;
        // sign bit
        byte b = (byte)((n >> 16) & 0xFF);
        WriteInternal((ushort)(n & 0xFFFF));
        WriteInternal(b);
    }
    public void WriteUInt24(uint n)
    {
        if (n > EncodingEx.Int24MaxValue)
        {
            WriteInt24((int)-(n - EncodingEx.Int24MaxValue));
        }
        else WriteInt24((int)n);
    }
    public void Write(float n) => WriteInternal(n);
    public void WriteHalfPrecision(float n) => WriteInternal(Mathf.FloatToHalf(n));
    public void WriteNullable(float? n)
    {
        if (n.HasValue)
        {
            Write(true);
            WriteInternal(n.Value);
        }
        else Write(false);
    }
    public void Write(decimal n) => WriteInternal(n);
    public void WriteNullable(decimal? n)
    {
        if (n.HasValue)
        {
            Write(true);
            WriteInternal(n.Value);
        }
        else Write(false);
    }
    public void Write(double n) => WriteInternal(n);
    public void WriteNullable(double? n)
    {
        if (n.HasValue)
        {
            Write(true);
            WriteInternal(n.Value);
        }
        else Write(false);
    }
    public void Write(char n) => WriteInternal(n);
    public void WriteNullable(char? n)
    {
        if (n.HasValue)
        {
            Write(true);
            WriteInternal(n.Value);
        }
        else Write(false);
    }
    public void Write(string n)
    {
        byte[] str = System.Text.Encoding.UTF8.GetBytes(n);
        ushort l = (ushort)Math.Min(ushort.MaxValue, str.Length);
        WriteInternal(l);
        if (_streamMode)
        {
            _stream!.Write(str, 0, l);
            _size += l;
            return;
        }
        int newsize = _size + l;
        if (newsize > _buffer.Length)
            ExtendBufferIntl(newsize);

        System.Buffer.BlockCopy(str, 0, _buffer, _size, l);
        _size = newsize;
    }
    public void WriteNullable(string? n)
    {
        if (n is not null)
        {
            Write(true);
            Write(n);
        }
        else Write(false);
    }
    public void Write(Type?[] types)
    {
        ushort len = (ushort)Math.Min(types.Length, ushort.MaxValue);
        Write(len);
        for (int i = 0; i < len; ++i)
            Write(types[i]);
    }
    public void Write(Type? type)
    {
        const string nsSdgUnturned = "SDG.Unturned";
        const string nsSdgFrameworkDevkit = "SDG.Framework.Devkit";
        const string nsSdgFramework = "SDG.Framework";
        const string nsDevkitServer = "DevkitServer";
        const string nsSystem = "System";
        byte flag = 0;
        Assembly? typeAssembly = type?.Assembly;
        if (type == null)
            flag = 128;
        else if (typeAssembly == Accessor.AssemblyCSharp)
            flag = 1;
        else if (typeAssembly == Accessor.DevkitServer)
            flag = 2;
        if (flag == 128)
        {
            Write(flag);
            return;
        }

        string ns = type!.Namespace ?? string.Empty;
        if (flag == 1)
        {
            if (ns.StartsWith(nsSdgUnturned, StringComparison.Ordinal))
            {
                flag |= 8;
                ns = ns.Length > nsSdgUnturned.Length ? ns.Substring(nsSdgUnturned.Length + 1) : string.Empty;
            }
            else if (ns.StartsWith(nsSdgFrameworkDevkit, StringComparison.Ordinal))
            {
                flag |= 16;
                ns = ns.Length > nsSdgFrameworkDevkit.Length ? ns.Substring(nsSdgFrameworkDevkit.Length + 1) : string.Empty;
            }
            else if (ns.StartsWith(nsSdgFramework, StringComparison.Ordinal))
            {
                flag |= 32;
                ns = ns.Length > nsSdgFramework.Length ? ns.Substring(nsSdgFramework.Length + 1) : string.Empty;
            }
        }
        else if (flag == 2 && ns.StartsWith(nsDevkitServer, StringComparison.Ordinal))
        {
            flag |= 4;
            ns = ns.Length > nsDevkitServer.Length ? ns.Substring(nsDevkitServer.Length + 1) : string.Empty;
        }
        else if (typeAssembly == Accessor.MSCoreLib && ns.StartsWith(nsSystem, StringComparison.Ordinal))
        {
            flag |= 64;
            ns = ns.Length > nsSystem.Length ? ns.Substring(nsSystem.Length + 1) : string.Empty;
        }

        if (ns.Length > 0)
            ns += ".";
        WriteInternal(flag);
        string str = ns + type.Name;
        if (flag == 0)
            str += ", " + type.Assembly.GetName().Name;
        Write(str);
    }
    public void WriteShort(string n)
    {
        byte[] str = System.Text.Encoding.UTF8.GetBytes(n);
        byte l = (byte)Math.Min(byte.MaxValue, str.Length);
        WriteInternal(l);
        if (_streamMode)
        {
            _stream!.Write(str, 0, l);
            _size += l;
            return;
        }
        int newsize = _size + l;
        if (newsize > _buffer.Length)
            ExtendBufferIntl(newsize);

        System.Buffer.BlockCopy(str, 0, _buffer, _size, l);
        _size = newsize;
    }
    public void WriteNullableShort(string? n)
    {
        if (n is not null)
        {
            Write(true);
            WriteShort(n);
        }
        else Write(false);
    }
    public void WriteAsciiSmall(string n)
    {
        byte[] str = System.Text.Encoding.ASCII.GetBytes(n);
        byte l = (byte)Math.Min(byte.MaxValue, str.Length);
        WriteInternal(l);
        if (_streamMode)
        {
            _stream!.Write(str, 0, l);
            _size += l;
            return;
        }
        int newsize = _size + l;
        if (newsize > _buffer.Length)
            ExtendBufferIntl(newsize);

        System.Buffer.BlockCopy(str, 0, _buffer, _size, l);
        _size = newsize;
    }
    public void WriteNullableAsciiSmall(string? n)
    {
        if (n is not null)
        {
            Write(true);
            WriteAsciiSmall(n);
        }
        else Write(false);
    }
    public void Write(DateTime n) => WriteInternal(n.ToBinary());
    public void WriteNullable(DateTime? n)
    {
        if (n.HasValue)
        {
            Write(true);
            Write(n.Value);
        }
        else Write(false);
    }
    public void Write(DateTimeOffset n)
    {
        Write(n.DateTime);
        Write((short)Math.Round(n.Offset.TotalMinutes));
    }
    public void WriteNullable(DateTimeOffset? n)
    {
        if (n.HasValue)
        {
            Write(true);
            Write(n.Value);
        }
        else Write(false);
    }
    public void Write(TimeSpan n) => WriteInternal(n.Ticks);
    public void WriteNullable(TimeSpan? n)
    {
        if (n.HasValue)
        {
            Write(true);
            Write(n.Value);
        }
        else Write(false);
    }
    public void Write(Guid n) => WriteInternal(n);
    public void WriteNullable(Guid? n)
    {
        if (n.HasValue)
        {
            Write(true);
            WriteInternal(n.Value);
        }
        else Write(false);
    }
    public void Write(Vector2 n)
    {
        if (!_streamMode)
        {
            int newsize = _size + sizeof(float) * 2;
            if (newsize > _buffer.Length)
                ExtendBufferIntl(newsize);
        }
        WriteInternal(n.x);
        WriteInternal(n.y);
    }
    public void WriteHalfPrecision(Vector2 n)
    {
        if (!_streamMode)
        {
            int newsize = _size + sizeof(ushort) * 2;
            if (newsize > _buffer.Length)
                ExtendBufferIntl(newsize);
        }
        WriteInternal(Mathf.FloatToHalf(n.x));
        WriteInternal(Mathf.FloatToHalf(n.y));
    }
    public void WriteNullable(Vector2? n)
    {
        if (n.HasValue)
        {
            Write(true);
            Write(n.Value);
        }
        else Write(false);
    }
    public void Write(Vector3 n)
    {
        if (!_streamMode)
        {
            int newsize = _size + sizeof(float) * 3;
            if (newsize > _buffer.Length)
                ExtendBufferIntl(newsize);
        }
        WriteInternal(n.x);
        WriteInternal(n.y);
        WriteInternal(n.z);
    }
    public void WriteHalfPrecision(Vector3 n)
    {
        if (!_streamMode)
        {
            int newsize = _size + sizeof(ushort) * 3;
            if (newsize > _buffer.Length)
                ExtendBufferIntl(newsize);
        }
        WriteInternal(Mathf.FloatToHalf(n.x));
        WriteInternal(Mathf.FloatToHalf(n.y));
        WriteInternal(Mathf.FloatToHalf(n.z));
    }
    public void WriteNullable(Vector3? n)
    {
        if (n.HasValue)
        {
            Write(true);
            Write(n.Value);
        }
        else Write(false);
    }
    public void Write(Vector4 n)
    {
        if (!_streamMode)
        {
            int newsize = _size + sizeof(float) * 4;
            if (newsize > _buffer.Length)
                ExtendBufferIntl(newsize);
        }
        WriteInternal(n.x);
        WriteInternal(n.y);
        WriteInternal(n.z);
        WriteInternal(n.w);
    }
    public void WriteHalfPrecision(Vector4 n)
    {
        if (!_streamMode)
        {
            int newsize = _size + sizeof(ushort) * 4;
            if (newsize > _buffer.Length)
                ExtendBufferIntl(newsize);
        }
        WriteInternal(Mathf.FloatToHalf(n.x));
        WriteInternal(Mathf.FloatToHalf(n.y));
        WriteInternal(Mathf.FloatToHalf(n.z));
        WriteInternal(Mathf.FloatToHalf(n.w));
    }
    public void WriteNullable(Vector4? n)
    {
        if (n.HasValue)
        {
            Write(true);
            Write(n.Value);
        }
        else Write(false);
    }
    public void Write(Bounds n)
    {
        if (!_streamMode)
        {
            int newsize = _size + sizeof(float) * 6;
            if (newsize > _buffer.Length)
                ExtendBufferIntl(newsize);
        }
        Vector3 c = n.center;
        Vector3 s = n.size;
        WriteInternal(c.x);
        WriteInternal(c.y);
        WriteInternal(c.z);
        WriteInternal(s.x);
        WriteInternal(s.y);
        WriteInternal(s.z);
    }
    public void WriteNullable(Bounds? n)
    {
        if (n.HasValue)
        {
            Write(true);
            Write(n.Value);
        }
        else Write(false);
    }
    public void Write(Quaternion n)
    {
        if (!_streamMode)
        {
            int newsize = _size + sizeof(float) * 4;
            if (newsize > _buffer.Length)
                ExtendBufferIntl(newsize);
        }
        WriteInternal(n.x);
        WriteInternal(n.y);
        WriteInternal(n.z);
        WriteInternal(n.w);
    }
    public void WriteHalfPrecision(Quaternion n)
    {
        if (!_streamMode)
        {
            int newsize = _size + sizeof(ushort) * 4;
            if (newsize > _buffer.Length)
                ExtendBufferIntl(newsize);
        }
        WriteInternal(Mathf.FloatToHalf(n.x));
        WriteInternal(Mathf.FloatToHalf(n.y));
        WriteInternal(Mathf.FloatToHalf(n.z));
        WriteInternal(Mathf.FloatToHalf(n.w));
    }
    public void WriteNullable(Quaternion? n)
    {
        if (n.HasValue)
        {
            Write(true);
            Write(n.Value);
        }
        else Write(false);
    }
    public void Write(Color n)
    {
        if (!_streamMode)
        {
            int newsize = _size + sizeof(float) * 4;
            if (newsize > _buffer.Length)
                ExtendBufferIntl(newsize);
        }
        WriteInternal(n.a);
        WriteInternal(n.r);
        WriteInternal(n.g);
        WriteInternal(n.b);
    }
    public void WriteNullable(Color? n)
    {
        if (n.HasValue)
        {
            Write(true);
            Write(n.Value);
        }
        else Write(false);
    }
    public void Write(Color32 n)
    {
        if (!_streamMode)
        {
            int newsize = _size + 4;
            if (newsize > _buffer.Length)
                ExtendBufferIntl(newsize);
        }
        WriteInternal(n.r);
        WriteInternal(n.g);
        WriteInternal(n.b);
        WriteInternal(n.a);
    }
    public void WriteNullable(Color32? n)
    {
        if (n.HasValue)
        {
            Write(true);
            Write(n.Value);
        }
        else Write(false);
    }
    public void Write(Guid[] n) => WriteInternal(n);
    public void WriteNullable(Guid[]? n)
    {
        if (n is not null)
        {
            Write(true);
            Write(n);
        }
        else Write(false);
    }
    public unsafe void Write(DateTime[] n)
    {
        const int objSize = sizeof(long);
        ushort len = (ushort)Math.Min(n.Length, ushort.MaxValue);
        if (_streamMode)
        {
            int size = sizeof(ushort) + objSize * len;
            if (_buffer.Length < size)
                _buffer = new byte[size];
            fixed (byte* ptr = _buffer)
            {
                *(ushort*)ptr = len;
                EndianCheck(ptr, sizeof(ushort));
                for (int i = 0; i < len; ++i)
                {
                    byte* ptr2 = ptr + sizeof(ushort) + i * objSize;
                    *(long*)ptr2 = n[i].ToBinary();
                    EndianCheck(ptr2, objSize);
                }
            }
            _stream!.Write(_buffer, 0, size);
            _size += size;
            return;
        }
        int newsize = _size + sizeof(ushort) + objSize * len;
        if (newsize > _buffer.Length)
            ExtendBufferIntl(newsize);
        fixed (byte* ptr = &_buffer[_size])
        {
            *(ushort*)ptr = len;
            EndianCheck(ptr, sizeof(ushort));
            for (int i = 0; i < len; ++i)
            {
                byte* ptr2 = ptr + sizeof(ushort) + i * objSize;
                *(long*)ptr2 = n[i].ToBinary();
                EndianCheck(ptr2, objSize);
            }
        }
        _size = newsize;
    }
    public void WriteNullable(DateTime[]? n)
    {
        if (n is not null)
        {
            Write(true);
            Write(n);
        }
        else Write(false);
    }
    public unsafe void Write(DateTimeOffset[] n)
    {
        const int objSize = sizeof(long) + sizeof(short);
        ushort len = (ushort)Math.Min(n.Length, ushort.MaxValue);
        if (_streamMode)
        {
            int size = sizeof(ushort) + objSize * len;
            if (_buffer.Length < size)
                _buffer = new byte[size];
            fixed (byte* ptr = _buffer)
            {
                *(ushort*)ptr = len;
                EndianCheck(ptr, sizeof(ushort));
                for (int i = 0; i < len; ++i)
                {
                    byte* ptr2 = ptr + sizeof(ushort) + i * objSize;
                    ref DateTimeOffset dt = ref n[i];
                    *(long*)ptr2 = dt.DateTime.ToBinary();
                    *(short*)(ptr2 + sizeof(long)) = (short)Math.Round(dt.Offset.TotalMinutes);
                    EndianCheck(ptr2, sizeof(long));
                    EndianCheck(ptr2 + sizeof(long), sizeof(short));
                }
            }
            _stream!.Write(_buffer, 0, size);
            _size += size;
            return;
        }
        int newsize = _size + sizeof(ushort) + objSize * len;
        if (newsize > _buffer.Length)
            ExtendBufferIntl(newsize);
        fixed (byte* ptr = &_buffer[_size])
        {
            *(ushort*)ptr = len;
            EndianCheck(ptr, sizeof(ushort));
            for (int i = 0; i < len; ++i)
            {
                byte* ptr2 = ptr + sizeof(ushort) + i * objSize;
                ref DateTimeOffset dt = ref n[i];
                *(long*)ptr2 = dt.DateTime.ToBinary();
                *(short*)(ptr2 + sizeof(long)) = (short)Math.Round(dt.Offset.TotalMinutes);
                EndianCheck(ptr2, sizeof(long));
                EndianCheck(ptr2 + sizeof(long), sizeof(short));
            }
        }
        _size = newsize;
    }
    public void WriteNullable(DateTimeOffset[]? n)
    {
        if (n is not null)
        {
            Write(true);
            Write(n);
        }
        else Write(false);
    }
    private void WriteEnum<TEnum>(TEnum o) where TEnum : unmanaged, Enum => WriteInternal(o);
    private void WriteNullableEnum<TEnum>(TEnum? o) where TEnum : unmanaged, Enum
    {
        if (o.HasValue)
        {
            Write(true);
            Write(o.Value);
        }
        else Write(false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write<TEnum>(TEnum o) where TEnum : unmanaged, Enum => WriteEnum(o);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteNullable<TEnum>(TEnum? n) where TEnum : unmanaged, Enum => WriteNullableEnum(n);
    public unsafe void Write(byte[] n)
    {
        ushort len = (ushort)Math.Min(n.Length, ushort.MaxValue);
        if (_streamMode)
        {
            WriteInternal(len);
            _stream!.Write(n, 0, len);
            _size += len;
            return;
        }
        int newsize = _size + sizeof(ushort) + len;
        if (newsize > _buffer.Length)
            ExtendBufferIntl(newsize);
        fixed (byte* ptr = &_buffer[_size])
        {
            *(ushort*)ptr = len;
            EndianCheck(ptr, sizeof(ushort));
        }
        System.Buffer.BlockCopy(n, 0, _buffer, _size + sizeof(ushort), len);
        _size = newsize;
    }
    public void WriteZeroCompressed(byte[] n, bool @long = false)
    {
        int len = n.Length;
        if (!@long)
        {
            len = Math.Min(len, ushort.MaxValue);
            WriteInternal((ushort)len);
        }
        else
            WriteInternal(len);

        int valuesWriting = 0;
        for (int i = 0; i < len; ++i)
        {
            byte c = n[i];

            if (valuesWriting == 0)
            {
                if (c == 0)
                {
                    int ct = 0;
                    for (int j = i + 1; j < len; ++j)
                    {
                        if (n[j] != 0 || j - i > 254 || j == len - 1)
                        {
                            ct = j - i - 1;
                            break;
                        }
                    }
                    WriteInternal((byte)ct);
                    i += ct;
                    continue;
                }

                WriteInternal((byte)255);
                valuesWriting = Math.Min(len - i, 255);
                for (int j = i + 1; j < len; ++j)
                {
                    if (j > len - 3)
                    {
                        valuesWriting = Math.Min(len - i, 255);
                        break;
                    }
                    if (j - i > 254 || n[j] == 0 && n[j + 1] == 0 && n[j + 2] == 0)
                    {
                        valuesWriting = j - i;
                        break;
                    }
                }

                WriteInternal((byte)(valuesWriting - 1));
            }

            WriteInternal(c);
            --valuesWriting;
        }
    }
    public void WriteNullable(byte[]? n)
    {
        if (n is not null)
        {
            Write(true);
            Write(n);
        }
        else Write(false);
    }
    /// <summary>
    /// Does not write length.
    /// </summary>
    public void WriteBlock(byte[] n)
    {
        if (_streamMode)
        {
            _stream!.Write(n, 0, n.Length);
            _size += n.Length;
            return;
        }
        int newsize = _size + n.Length;
        if (newsize > _buffer.Length)
            ExtendBufferIntl(newsize);
        System.Buffer.BlockCopy(n, 0, _buffer, _size, n.Length);
        _size = newsize;
    }

    /// <summary>
    /// Does not write length.
    /// </summary>
    public void WriteBlock(byte[] n, int index, int count = -1)
    {
        if (count == -1)
            count = n.Length - index;
        if (index < 0)
            index = 0;
        if (index + count > n.Length)
            throw new ArgumentOutOfRangeException(nameof(index));
        if (_streamMode)
        {
            _stream!.Write(n, index, count);
            _size += n.Length;
            return;
        }
        int newsize = _size + count;
        if (newsize > _buffer.Length)
            ExtendBufferIntl(newsize);
        System.Buffer.BlockCopy(n, index, _buffer, _size, count);
        _size = newsize;
    }

    /// <summary>
    /// Does not write length.
    /// </summary>
    public void WriteBlock(ReadOnlySpan<byte> n)
    {
        if (_streamMode)
        {
            _stream!.Write(n);
            _size += n.Length;
            return;
        }
        int newsize = _size + n.Length;
        if (newsize > _buffer.Length)
            ExtendBufferIntl(newsize);

        n.TryCopyTo(_buffer.AsSpan(_size));
        _size = newsize;
    }
    public unsafe void WriteLong(byte[] n)
    {
        int len = n.Length;
        if (_streamMode)
        {
            WriteInternal(len);
            _stream!.Write(n, 0, len);
            _size += len;
            return;
        }
        int newsize = _size + sizeof(int) + len;
        if (newsize > _buffer.Length)
            ExtendBufferIntl(newsize);
        fixed (byte* ptr = &_buffer[_size])
        {
            *(int*)ptr = len;
            EndianCheck(ptr, sizeof(int));
        }
        System.Buffer.BlockCopy(n, 0, _buffer, _size + sizeof(int), len);
        _size = newsize;
    }
    public void WriteNullableLong(byte[]? n)
    {
        if (n is not null)
        {
            Write(true);
            WriteLong(n);
        }
        else Write(false);
    }
    public void Flush()
    {
        if (_streamMode)
        {
            _stream!.Flush();
        }
        else if (_buffer.Length != 0)
        {
            Array.Clear(_buffer, 0, _buffer.Length);
            _size = 0;
        }
    }
    public void Write(int[] n) => WriteInternal(n);
    public void WriteZeroCompressed(int[] n, bool @long = false)
    {
        int len = n.Length;
        if (!@long)
        {
            len = Math.Min(len, ushort.MaxValue);
            WriteInternal((ushort)len);
        }
        else
            WriteInternal(len);

        int valuesWriting = 0;
        for (int i = 0; i < len; ++i)
        {
            int c = n[i];

            if (valuesWriting == 0)
            {
                if (c == 0)
                {
                    int ct = 0;
                    for (int j = i + 1; j < len; ++j)
                    {
                        if (n[j] != 0 || j - i > 254 || j == len - 1)
                        {
                            ct = j - i - 1;
                            break;
                        }
                    }
                    WriteInternal((byte)ct);
                    i += ct;
                    continue;
                }

                WriteInternal((byte)255);
                valuesWriting = Math.Min(len - i, 255);
                for (int j = i + 1; j < len; ++j)
                {
                    if (j > len - 3)
                    {
                        valuesWriting = Math.Min(len - i, 255);
                        break;
                    }
                    if (j - i > 254 || n[j] == 0 && n[j + 1] == 0 && n[j + 2] == 0)
                    {
                        valuesWriting = j - i;
                        break;
                    }
                }

                WriteInternal((byte)(valuesWriting - 1));
            }

            WriteInternal(c);
            --valuesWriting;
        }
    }
    public void WriteNullable(int[]? n)
    {
        if (n is not null)
        {
            Write(true);
            Write(n);
        }
        else Write(false);
    }
    public void Write(uint[] n) => WriteInternal(n);
    public void WriteZeroCompressed(uint[] n, bool @long = false)
    {
        int len = n.Length;
        if (!@long)
        {
            len = Math.Min(len, ushort.MaxValue);
            WriteInternal((ushort)len);
        }
        else
            WriteInternal(len);

        int valuesWriting = 0;
        for (int i = 0; i < len; ++i)
        {
            uint c = n[i];

            if (valuesWriting == 0)
            {
                if (c == 0)
                {
                    int ct = 0;
                    for (int j = i + 1; j < len; ++j)
                    {
                        if (n[j] != 0 || j - i > 254 || j == len - 1)
                        {
                            ct = j - i - 1;
                            break;
                        }
                    }
                    WriteInternal((byte)ct);
                    i += ct;
                    continue;
                }

                WriteInternal((byte)255);
                valuesWriting = Math.Min(len - i, 255);
                for (int j = i + 1; j < len; ++j)
                {
                    if (j > len - 3)
                    {
                        valuesWriting = Math.Min(len - i, 255);
                        break;
                    }
                    if (j - i > 254 || n[j] == 0 && n[j + 1] == 0 && n[j + 2] == 0)
                    {
                        valuesWriting = j - i;
                        break;
                    }
                }

                WriteInternal((byte)(valuesWriting - 1));
            }

            WriteInternal(c);
            --valuesWriting;
        }
    }
    public void WriteNullable(uint[]? n)
    {
        if (n is not null)
        {
            Write(true);
            Write(n);
        }
        else Write(false);
    }
    public void Write(sbyte[] n) => WriteInternal(n);
    public void WriteZeroCompressed(sbyte[] n, bool @long = false)
    {
        int len = n.Length;
        if (!@long)
        {
            len = Math.Min(len, ushort.MaxValue);
            WriteInternal((ushort)len);
        }
        else
            WriteInternal(len);

        int valuesWriting = 0;
        for (int i = 0; i < len; ++i)
        {
            sbyte c = n[i];

            if (valuesWriting == 0)
            {
                if (c == 0)
                {
                    int ct = 0;
                    for (int j = i + 1; j < len; ++j)
                    {
                        if (n[j] != 0 || j - i > 254 || j == len - 1)
                        {
                            ct = j - i - 1;
                            break;
                        }
                    }
                    WriteInternal((byte)ct);
                    i += ct;
                    continue;
                }

                WriteInternal((byte)255);
                valuesWriting = Math.Min(len - i, 255);
                for (int j = i + 1; j < len; ++j)
                {
                    if (j > len - 3)
                    {
                        valuesWriting = Math.Min(len - i, 255);
                        break;
                    }
                    if (j - i > 254 || n[j] == 0 && n[j + 1] == 0 && n[j + 2] == 0)
                    {
                        valuesWriting = j - i;
                        break;
                    }
                }

                WriteInternal((byte)(valuesWriting - 1));
            }

            WriteInternal(c);
            --valuesWriting;
        }
    }
    public void WriteNullable(sbyte[]? n)
    {
        if (n is not null)
        {
            Write(true);
            Write(n);
        }
        else Write(false);
    }
    public unsafe void Write(bool[] n)
    {
        if (n.Length > ushort.MaxValue)
        {
            Console.WriteLine($"Boolean array too long for writing, must be below {ushort.MaxValue} elements, it was {n.Length} elements long.");
            Console.WriteLine(Environment.StackTrace);
            return;
        }
        
        int size;
        if (!_streamMode)
        {
            int newsize = _size + (int)Math.Ceiling(n.Length / 8f) + sizeof(ushort);
            if (newsize > _buffer.Length)
                ExtendBufferIntl(newsize);
            size = newsize;
        }
        else
        {
            size = (int)Math.Ceiling(n.Length / 8d) + sizeof(ushort);
            if (_buffer.Length < size) _buffer = new byte[size];
        }

        fixed (byte* ptr = _buffer)
        {
            byte* ptr2 = _streamMode ? ptr : ptr + _size;
            *(ushort*)ptr2 = (ushort)n.Length;
            EndianCheck(ptr, sizeof(ushort));
            ptr2 += sizeof(ushort);
            byte current = 0;
            int cutoff = n.Length - 1;
            for (int i = 0; i < n.Length; i++)
            {
                bool c = n[i];
                int mod = i % 8;
                if (mod == 0 && i != 0)
                {
                    *ptr2 = current;
                    ptr2++;
                    current = (byte)(c ? 1 : 0);
                }
                else if (c) current |= (byte)(1 << mod);
                if (i == cutoff)
                    *ptr2 = current;
            }
        }

        if (!_streamMode)
            _size = size;
        else
        {
            _stream!.Write(_buffer, 0, size);
            _size += size;
        }
    }
    public unsafe void WriteLong(bool[] n)
    {
        int size;
        if (!_streamMode)
        {
            int newsize = _size + (int)Math.Ceiling(n.Length / 8f) + sizeof(int);
            if (newsize > _buffer.Length)
                ExtendBufferIntl(newsize);
            size = newsize;
        }
        else
        {
            size = (int)Math.Ceiling(n.Length / 8d) + sizeof(int);
            if (_buffer.Length < size) _buffer = new byte[size];
        }

        fixed (byte* ptr = _buffer)
        {
            byte* ptr2 = _streamMode ? ptr : ptr + _size;
            *(int*)ptr2 = n.Length;
            EndianCheck(ptr, sizeof(int));
            ptr2 += sizeof(int);
            byte current = 0;
            int cutoff = n.Length - 1;
            for (int i = 0; i < n.Length; i++)
            {
                bool c = n[i];
                int mod = i % 8;
                if (mod == 0 && i != 0)
                {
                    *ptr2 = current;
                    ptr2++;
                    current = (byte)(c ? 1 : 0);
                }
                else if (c) current |= (byte)(1 << mod);
                if (i == cutoff)
                    *ptr2 = current;
            }
        }

        if (!_streamMode)
            _size = size;
        else
        {
            _stream!.Write(_buffer, 0, size);
            _size += size;
        }
    }
    public void WriteNullable(bool[]? n)
    {
        if (n is not null)
        {
            Write(true);
            Write(n);
        }
        else Write(false);
    }
    public void Write(long[] n) => WriteInternal(n);
    public void WriteZeroCompressed(long[] n, bool @long = false)
    {
        int len = n.Length;
        if (!@long)
        {
            len = Math.Min(len, ushort.MaxValue);
            WriteInternal((ushort)len);
        }
        else
            WriteInternal(len);

        int valuesWriting = 0;
        for (int i = 0; i < len; ++i)
        {
            long c = n[i];

            if (valuesWriting == 0)
            {
                if (c == 0)
                {
                    int ct = 0;
                    for (int j = i + 1; j < len; ++j)
                    {
                        if (n[j] != 0 || j - i > 254 || j == len - 1)
                        {
                            ct = j - i - 1;
                            break;
                        }
                    }
                    WriteInternal((byte)ct);
                    i += ct;
                    continue;
                }

                WriteInternal((byte)255);
                valuesWriting = Math.Min(len - i, 255);
                for (int j = i + 1; j < len; ++j)
                {
                    if (j > len - 3)
                    {
                        valuesWriting = Math.Min(len - i, 255);
                        break;
                    }
                    if (j - i > 254 || n[j] == 0 && n[j + 1] == 0 && n[j + 2] == 0)
                    {
                        valuesWriting = j - i;
                        break;
                    }
                }

                WriteInternal((byte)(valuesWriting - 1));
            }

            WriteInternal(c);
            --valuesWriting;
        }
    }
    public void WriteNullable(long[]? n)
    {
        if (n is not null)
        {
            Write(true);
            Write(n);
        }
        else Write(false);
    }
    public void Write(ulong[] n) => WriteInternal(n);
    public void WriteZeroCompressed(ulong[] n, bool @long = false)
    {
        int len = n.Length;
        if (!@long)
        {
            len = Math.Min(len, ushort.MaxValue);
            WriteInternal((ushort)len);
        }
        else
            WriteInternal(len);

        int valuesWriting = 0;
        for (int i = 0; i < len; ++i)
        {
            ulong c = n[i];

            if (valuesWriting == 0)
            {
                if (c == 0)
                {
                    int ct = 0;
                    for (int j = i + 1; j < len; ++j)
                    {
                        if (n[j] != 0 || j - i > 254 || j == len - 1)
                        {
                            ct = j - i - 1;
                            break;
                        }
                    }
                    WriteInternal((byte)ct);
                    i += ct;
                    continue;
                }

                WriteInternal((byte)255);
                valuesWriting = Math.Min(len - i, 255);
                for (int j = i + 1; j < len; ++j)
                {
                    if (j > len - 3)
                    {
                        valuesWriting = Math.Min(len - i, 255);
                        break;
                    }
                    if (j - i > 254 || n[j] == 0 && n[j + 1] == 0 && n[j + 2] == 0)
                    {
                        valuesWriting = j - i;
                        break;
                    }
                }

                WriteInternal((byte)(valuesWriting - 1));
            }

            WriteInternal(c);
            --valuesWriting;
        }
    }
    public void WriteNullable(ulong[]? n)
    {
        if (n is not null)
        {
            Write(true);
            Write(n);
        }
        else Write(false);
    }
    public void Write(short[] n) => WriteInternal(n);
    public void WriteZeroCompressed(short[] n, bool @long)
    {
        int len = n.Length;
        if (!@long)
        {
            len = Math.Min(len, ushort.MaxValue);
            WriteInternal((ushort)len);
        }
        else
            WriteInternal(len);

        int valuesWriting = 0;
        for (int i = 0; i < len; ++i)
        {
            short c = n[i];

            if (valuesWriting == 0)
            {
                if (c == 0)
                {
                    int ct = 0;
                    for (int j = i + 1; j < len; ++j)
                    {
                        if (n[j] != 0 || j - i > 254 || j == len - 1)
                        {
                            ct = j - i - 1;
                            break;
                        }
                    }
                    WriteInternal((byte)ct);
                    i += ct;
                    continue;
                }

                WriteInternal((byte)255);
                valuesWriting = Math.Min(len - i, 255);
                for (int j = i + 1; j < len; ++j)
                {
                    if (j > len - 3)
                    {
                        valuesWriting = Math.Min(len - i, 255);
                        break;
                    }
                    if (j - i > 254 || n[j] == 0 && n[j + 1] == 0 && n[j + 2] == 0)
                    {
                        valuesWriting = j - i;
                        break;
                    }
                }

                WriteInternal((byte)(valuesWriting - 1));
            }

            WriteInternal(c);
            --valuesWriting;
        }
    }
    public void WriteNullable(short[]? n)
    {
        if (n is not null)
        {
            Write(true);
            Write(n);
        }
        else Write(false);
    }
    public void Write(ushort[] n) => WriteInternal(n);
    public void WriteZeroCompressed(ushort[] n, bool @long)
    {
        int len = n.Length;
        if (!@long)
        {
            len = Math.Min(len, ushort.MaxValue);
            WriteInternal((ushort)len);
        }
        else
            WriteInternal(len);

        int valuesWriting = 0;
        for (int i = 0; i < len; ++i)
        {
            ushort c = n[i];

            if (valuesWriting == 0)
            {
                if (c == 0)
                {
                    int ct = 0;
                    for (int j = i + 1; j < len; ++j)
                    {
                        if (n[j] != 0 || j - i > 254 || j == len - 1)
                        {
                            ct = j - i - 1;
                            break;
                        }
                    }
                    WriteInternal((byte)ct);
                    i += ct;
                    continue;
                }

                WriteInternal((byte)255);
                valuesWriting = Math.Min(len - i, 255);
                for (int j = i + 1; j < len; ++j)
                {
                    if (j > len - 3)
                    {
                        valuesWriting = Math.Min(len - i, 255);
                        break;
                    }
                    if (j - i > 254 || n[j] == 0 && n[j + 1] == 0 && n[j + 2] == 0)
                    {
                        valuesWriting = j - i;
                        break;
                    }
                }

                WriteInternal((byte)(valuesWriting - 1));
            }

            WriteInternal(c);
            --valuesWriting;
        }
    }
    public void WriteNullable(ushort[]? n)
    {
        if (n is not null)
        {
            Write(true);
            Write(n);
        }
        else Write(false);
    }
    public void Write(float[] n) => WriteInternal(n);
    public void WriteNullable(float[]? n)
    {
        if (n is not null)
        {
            Write(true);
            Write(n);
        }
        else Write(false);
    }
    public void Write(decimal[] n) => WriteInternal(n);
    public void WriteNullable(decimal[]? n)
    {
        if (n is not null)
        {
            Write(true);
            Write(n);
        }
        else Write(false);
    }
    public void Write(double[] n) => WriteInternal(n);
    public void WriteNullable(double[]? n)
    {
        if (n is not null)
        {
            Write(true);
            Write(n);
        }
        else Write(false);
    }
    public unsafe void Write(char[] n)
    {
        byte[] str = System.Text.Encoding.UTF8.GetBytes(n);
        ushort len = (ushort)Math.Min(str.Length, ushort.MaxValue);
        if (_streamMode)
        {
            WriteInternal(len);
            _stream!.Write(str, 0, len);
            _size += len;
            return;
        }
        int newsize = _size + sizeof(ushort) + len;
        if (newsize > _buffer.Length)
            ExtendBufferIntl(newsize);
        fixed (byte* ptr = &_buffer[_size])
        {
            *(ushort*)ptr = len;
            EndianCheck(ptr, sizeof(ushort));
        }
        System.Buffer.BlockCopy(str, 0, _buffer, _size + sizeof(ushort), len);
        _size = newsize;
    }
    public void WriteNullable(char[]? n)
    {
        if (n is not null)
        {
            Write(true);
            Write(n);
        }
        else Write(false);
    }
    public void Write(string[] n)
    {
        ushort len = (ushort)Math.Min(n.Length, ushort.MaxValue);
        WriteInternal(len);
        for (int i = 0; i < len; i++)
        {
            byte[] str = System.Text.Encoding.UTF8.GetBytes(n[i]);
            ushort len1 = (ushort)Math.Min(str.Length, ushort.MaxValue);
            WriteInternal(len1);
            if (_streamMode)
            {
                _stream!.Write(str, 0, len1);
                _size += len1;
                continue;
            }
            int newsize = _size + len1;
            if (newsize > _buffer.Length)
                ExtendBufferIntl(newsize);

            System.Buffer.BlockCopy(str, 0, _buffer, _size, len1);
            _size = newsize;
        }
    }
    public void WriteNullable(string[]? n)
    {
        if (n is not null)
        {
            Write(true);
            Write(n);
        }
        else Write(false);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write<T1>(Writer<T1> writer, T1 arg)
    {
        writer.Invoke(this, arg);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static void VerifyType<T>(int typeIndex = -1) => VerifyType(typeof(T), typeIndex);
    protected static void VerifyType(Type type, int typeIndex = -1)
    {
        if (!EncodingEx.IsValidAutoType(type))
            throw new InvalidDynamicTypeException(type, typeIndex, false);
    }
    public static Delegate? GetWriter(Type type, bool isNullable = false)
    {
        if (type == null) throw new ArgumentNullException(nameof(type));
        MethodInfo? method = GetWriteMethod(type, isNullable);
        if (method == null)
            throw new InvalidDynamicTypeException(type, -1, false);
        try
        {
            return method.CreateDelegate(typeof(Writer<>).MakeGenericType(type));
        }
        catch (ArgumentException ex)
        {
            Logger.LogError("Failed to create writer delegate for type " + type.FullName);
            Logger.LogError(ex);
            return null;
        }
    }
    public static MethodInfo? GetWriteMethod(Type type, bool isNullable = false)
    {
        MethodInfo? method;
        if (type.IsEnum)
        {
            method = (isNullable ? WriteNullableEnumMethod : WriteEnumMethod).MakeGenericMethod(type);
        }
        else
        {
            if (_nullableWriters == null || _nonNullableWriters == null)
                PrepareMethods();
            if (isNullable)
            {
                _nullableWriters!.TryGetValue(type, out method);
            }
            else if (!_nonNullableWriters!.TryGetValue(type, out method) && type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
                _nullableWriters!.TryGetValue(type, out method);
        }

        return method;
    }
    public static Writer<T1> GetWriter<T1>(bool isNullable = false) => (Writer<T1>)GetWriter(typeof(T1), isNullable)!;
    public static int GetMinimumSize(Type type)
    {
        if (type.IsPointer) return IntPtr.Size;
        
        if (type.IsArray || type == typeof(string)) return sizeof(ushort);
        
        try
        {
            return Marshal.SizeOf(type);
        }
        catch (ArgumentException)
        {
            return 0;
        }
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetMinimumSize<T>() => GetMinimumSize(typeof(T));
    public static class WriterHelper<T>
    {
        public static readonly Writer<T> Writer;
        static WriterHelper()
        {
            VerifyType<T>();
            Writer = GetWriter<T>();
        }
    }
    public static class NullableWriterHelper<T>
    {
        public static readonly Writer<T> Writer;
        static NullableWriterHelper()
        {
            VerifyType<T>();
            Writer = GetWriter<T>(true);
        }
    }
}

public delegate void Writer<in T>(ByteWriter writer, T arg1);

public sealed class ByteWriterRaw<T> : ByteWriter
{
    private readonly Writer<T> _writer;
    /// <summary>Leave <paramref name="writer"/> null to auto-fill.</summary>
    public ByteWriterRaw(Writer<T>? writer, bool allowPrepending = true, int capacity = 0) : base(allowPrepending, capacity)
    {
        _writer = writer ?? WriterHelper<T>.Writer;
    }
    public byte[] Get(ref MessageOverhead overhead, T obj)
    {
        lock (this)
        {
            Flush();
            _writer.Invoke(this, obj);
            PrependOverhead(ref overhead);
            return ToArray();
        }
    }
    public byte[] Get(T obj)
    {
        lock (this)
        {
            Flush();
            _writer.Invoke(this, obj);
            return ToArray();
        }
    }
}
public sealed class ByteWriterRaw<T1, T2> : ByteWriter
{
    private readonly Writer<T1> _writer1;
    private readonly Writer<T2> _writer2;
    /// <summary>Leave any writer null to auto-fill.</summary>
    public ByteWriterRaw(Writer<T1>? writer1, Writer<T2>? writer2, bool shouldPrepend = true, int capacity = 0) : base(shouldPrepend, capacity)
    {
        _writer1 = writer1 ?? WriterHelper<T1>.Writer;
        _writer2 = writer2 ?? WriterHelper<T2>.Writer;
    }
    public byte[] Get(ref MessageOverhead overhead, T1 arg1, T2 arg2)
    {
        lock (this)
        {
            Flush();
            _writer1.Invoke(this, arg1);
            _writer2.Invoke(this, arg2);
            PrependOverhead(ref overhead);
            return ToArray();
        }
    }
    public byte[] Get(T1 arg1, T2 arg2)
    {
        lock (this)
        {
            Flush();
            _writer1.Invoke(this, arg1);
            _writer2.Invoke(this, arg2);
            return ToArray();
        }
    }
}
public sealed class ByteWriterRaw<T1, T2, T3> : ByteWriter
{
    private readonly Writer<T1> _writer1;
    private readonly Writer<T2> _writer2;
    private readonly Writer<T3> _writer3;
    /// <summary>Leave any writer null to auto-fill.</summary>
    public ByteWriterRaw(Writer<T1>? writer1, Writer<T2>? writer2, Writer<T3>? writer3, bool shouldPrepend = true, int capacity = 0) : base(shouldPrepend, capacity)
    {
        _writer1 = writer1 ?? WriterHelper<T1>.Writer;
        _writer2 = writer2 ?? WriterHelper<T2>.Writer;
        _writer3 = writer3 ?? WriterHelper<T3>.Writer;
    }
    public byte[] Get(ref MessageOverhead overhead, T1 arg1, T2 arg2, T3 arg3)
    {
        lock (this)
        {
            Flush();
            _writer1.Invoke(this, arg1);
            _writer2.Invoke(this, arg2);
            _writer3.Invoke(this, arg3);
            PrependOverhead(ref overhead);
            return ToArray();
        }
    }
    public byte[] Get(T1 arg1, T2 arg2, T3 arg3)
    {
        lock (this)
        {
            Flush();
            _writer1.Invoke(this, arg1);
            _writer2.Invoke(this, arg2);
            _writer3.Invoke(this, arg3);
            return ToArray();
        }
    }
}
public sealed class ByteWriterRaw<T1, T2, T3, T4> : ByteWriter
{
    private readonly Writer<T1> _writer1;
    private readonly Writer<T2> _writer2;
    private readonly Writer<T3> _writer3;
    private readonly Writer<T4> _writer4;
    /// <summary>Leave any writer null to auto-fill.</summary>
    public ByteWriterRaw(Writer<T1>? writer1, Writer<T2>? writer2, Writer<T3>? writer3, Writer<T4>? writer4, bool shouldPrepend = true, int capacity = 0) : base(shouldPrepend, capacity)
    {
        _writer1 = writer1 ?? WriterHelper<T1>.Writer;
        _writer2 = writer2 ?? WriterHelper<T2>.Writer;
        _writer3 = writer3 ?? WriterHelper<T3>.Writer;
        _writer4 = writer4 ?? WriterHelper<T4>.Writer;
    }
    public byte[] Get(ref MessageOverhead overhead, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
    {
        lock (this)
        {
            Flush();
            _writer1.Invoke(this, arg1);
            _writer2.Invoke(this, arg2);
            _writer3.Invoke(this, arg3);
            _writer4.Invoke(this, arg4);
            PrependOverhead(ref overhead);
            return ToArray();
        }
    }
    public byte[] Get(T1 arg1, T2 arg2, T3 arg3, T4 arg4)
    {
        lock (this)
        {
            Flush();
            _writer1.Invoke(this, arg1);
            _writer2.Invoke(this, arg2);
            _writer3.Invoke(this, arg3);
            _writer4.Invoke(this, arg4);
            return ToArray();
        }
    }
}
public sealed class ByteWriterRaw<T1, T2, T3, T4, T5> : ByteWriter
{
    private readonly Writer<T1> _writer1;
    private readonly Writer<T2> _writer2;
    private readonly Writer<T3> _writer3;
    private readonly Writer<T4> _writer4;
    private readonly Writer<T5> _writer5;
    /// <summary>Leave any writer null to auto-fill.</summary>
    public ByteWriterRaw(Writer<T1>? writer1, Writer<T2>? writer2, Writer<T3>? writer3, Writer<T4>? writer4, Writer<T5>? writer5, bool shouldPrepend = true, int capacity = 0) : base(shouldPrepend, capacity)
    {
        _writer1 = writer1 ?? WriterHelper<T1>.Writer;
        _writer2 = writer2 ?? WriterHelper<T2>.Writer;
        _writer3 = writer3 ?? WriterHelper<T3>.Writer;
        _writer4 = writer4 ?? WriterHelper<T4>.Writer;
        _writer5 = writer5 ?? WriterHelper<T5>.Writer;
    }
    public byte[] Get(ref MessageOverhead overhead, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
    {
        lock (this)
        {
            Flush();
            _writer1.Invoke(this, arg1);
            _writer2.Invoke(this, arg2);
            _writer3.Invoke(this, arg3);
            _writer4.Invoke(this, arg4);
            _writer5.Invoke(this, arg5);
            PrependOverhead(ref overhead);
            return ToArray();
        }
    }
    public byte[] Get(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
    {
        lock (this)
        {
            Flush();
            _writer1.Invoke(this, arg1);
            _writer2.Invoke(this, arg2);
            _writer3.Invoke(this, arg3);
            _writer4.Invoke(this, arg4);
            _writer5.Invoke(this, arg5);
            return ToArray();
        }
    }
}
public sealed class ByteWriterRaw<T1, T2, T3, T4, T5, T6> : ByteWriter
{
    private readonly Writer<T1> _writer1;
    private readonly Writer<T2> _writer2;
    private readonly Writer<T3> _writer3;
    private readonly Writer<T4> _writer4;
    private readonly Writer<T5> _writer5;
    private readonly Writer<T6> _writer6;
    /// <summary>Leave any writer null to auto-fill.</summary>
    public ByteWriterRaw(Writer<T1>? writer1, Writer<T2>? writer2, Writer<T3>? writer3, Writer<T4>? writer4, Writer<T5>? writer5, Writer<T6>? writer6, bool shouldPrepend = true, int capacity = 0) : base(shouldPrepend, capacity)
    {
        _writer1 = writer1 ?? WriterHelper<T1>.Writer;
        _writer2 = writer2 ?? WriterHelper<T2>.Writer;
        _writer3 = writer3 ?? WriterHelper<T3>.Writer;
        _writer4 = writer4 ?? WriterHelper<T4>.Writer;
        _writer5 = writer5 ?? WriterHelper<T5>.Writer;
        _writer6 = writer6 ?? WriterHelper<T6>.Writer;
    }
    public byte[] Get(ref MessageOverhead overhead, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
    {
        lock (this)
        {
            Flush();
            _writer1.Invoke(this, arg1);
            _writer2.Invoke(this, arg2);
            _writer3.Invoke(this, arg3);
            _writer4.Invoke(this, arg4);
            _writer5.Invoke(this, arg5);
            _writer6.Invoke(this, arg6);
            PrependOverhead(ref overhead);
            return ToArray();
        }
    }
    public byte[] Get(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
    {
        lock (this)
        {
            Flush();
            _writer1.Invoke(this, arg1);
            _writer2.Invoke(this, arg2);
            _writer3.Invoke(this, arg3);
            _writer4.Invoke(this, arg4);
            _writer5.Invoke(this, arg5);
            _writer6.Invoke(this, arg6);
            return ToArray();
        }
    }
}
// ReSharper disable ConvertToPrimaryConstructor
public sealed class DynamicByteWriter<T1> : ByteWriter
{
    static DynamicByteWriter()
    {
        Writer = WriterHelper<T1>.Writer;
    }
    private static readonly Writer<T1> Writer;
    public DynamicByteWriter(bool shouldPrepend = true, int capacity = 0) : base(shouldPrepend, capacity < 1 ? GetMinimumSize<T1>() + MessageOverhead.MaximumSize : capacity) { }
    public byte[] Get(ref MessageOverhead overhead, T1 obj)
    {
        lock (this)
        {
            Flush();
            Writer.Invoke(this, obj);
            PrependOverhead(ref overhead);
            return ToArray();
        }
    }
    public byte[] Get(T1 obj)
    {
        lock (this)
        {
            Flush();
            Writer.Invoke(this, obj);
            return ToArray();
        }
    }
}
public sealed class DynamicByteWriter<T1, T2> : ByteWriter
{
    static DynamicByteWriter()
    {
        Writer1 = WriterHelper<T1>.Writer;
        Writer2 = WriterHelper<T2>.Writer;
    }
    private static readonly Writer<T1> Writer1;
    private static readonly Writer<T2> Writer2;
    public DynamicByteWriter(bool shouldPrepend = true, int capacity = 0) : base(shouldPrepend, capacity < 1 ? GetMinimumSize<T1>() + GetMinimumSize<T2>() + MessageOverhead.MaximumSize : capacity) { }
    public byte[] Get(ref MessageOverhead overhead, T1 arg1, T2 arg2)
    {
        lock (this)
        {
            Flush();
            Write(Writer1, arg1);
            Write(Writer2, arg2);
            PrependOverhead(ref overhead);
            return ToArray();
        }
    }
    public byte[] Get(T1 arg1, T2 arg2)
    {
        lock (this)
        {
            Flush();
            Write(Writer1, arg1);
            Write(Writer2, arg2);
            return ToArray();
        }
    }
}
public sealed class DynamicByteWriter<T1, T2, T3> : ByteWriter
{
    static DynamicByteWriter()
    {
        Writer1 = WriterHelper<T1>.Writer;
        Writer2 = WriterHelper<T2>.Writer;
        Writer3 = WriterHelper<T3>.Writer;
    }
    private static readonly Writer<T1> Writer1;
    private static readonly Writer<T2> Writer2;
    private static readonly Writer<T3> Writer3;
    public DynamicByteWriter(bool shouldPrepend = true, int capacity = 0) : base(shouldPrepend, capacity < 1 ? GetMinimumSize<T1>() + GetMinimumSize<T2>() + GetMinimumSize<T3>()
                                                                                                               + MessageOverhead.MaximumSize : capacity) { }
    public byte[] Get(ref MessageOverhead overhead, T1 arg1, T2 arg2, T3 arg3)
    {
        lock (this)
        {
            Flush();
            Write(Writer1, arg1);
            Write(Writer2, arg2);
            Write(Writer3, arg3);
            PrependOverhead(ref overhead);
            return ToArray();
        }
    }
    public byte[] Get(T1 arg1, T2 arg2, T3 arg3)
    {
        lock (this)
        {
            Flush();
            Write(Writer1, arg1);
            Write(Writer2, arg2);
            Write(Writer3, arg3);
            return ToArray();
        }
    }
}
public sealed class DynamicByteWriter<T1, T2, T3, T4> : ByteWriter
{
    static DynamicByteWriter()
    {
        Writer1 = WriterHelper<T1>.Writer;
        Writer2 = WriterHelper<T2>.Writer;
        Writer3 = WriterHelper<T3>.Writer;
        Writer4 = WriterHelper<T4>.Writer;
    }
    private static readonly Writer<T1> Writer1;
    private static readonly Writer<T2> Writer2;
    private static readonly Writer<T3> Writer3;
    private static readonly Writer<T4> Writer4;
    public DynamicByteWriter(bool shouldPrepend = true, int capacity = 0) : base(shouldPrepend, capacity < 1 ?
                                                                                                                               GetMinimumSize<T1>() + GetMinimumSize<T2>() + GetMinimumSize<T3>() +
                                                                                                                               GetMinimumSize<T4>() + MessageOverhead.MaximumSize : capacity)
    { }
    public byte[] Get(ref MessageOverhead overhead, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
    {
        lock (this)
        {
            Flush();
            Write(Writer1, arg1);
            Write(Writer2, arg2);
            Write(Writer3, arg3);
            Write(Writer4, arg4);
            PrependOverhead(ref overhead);
            return ToArray();
        }
    }
    public byte[] Get(T1 arg1, T2 arg2, T3 arg3, T4 arg4)
    {
        lock (this)
        {
            Flush();
            Write(Writer1, arg1);
            Write(Writer2, arg2);
            Write(Writer3, arg3);
            Write(Writer4, arg4);
            return ToArray();
        }
    }
}
public sealed class DynamicByteWriter<T1, T2, T3, T4, T5> : ByteWriter
{
    static DynamicByteWriter()
    {
        Writer1 = WriterHelper<T1>.Writer;
        Writer2 = WriterHelper<T2>.Writer;
        Writer3 = WriterHelper<T3>.Writer;
        Writer4 = WriterHelper<T4>.Writer;
        Writer5 = WriterHelper<T5>.Writer;
    }
    private static readonly Writer<T1> Writer1;
    private static readonly Writer<T2> Writer2;
    private static readonly Writer<T3> Writer3;
    private static readonly Writer<T4> Writer4;
    private static readonly Writer<T5> Writer5;
    public DynamicByteWriter(bool shouldPrepend = true, int capacity = 0) : base(shouldPrepend, capacity < 1 ?
                                                                                                                               GetMinimumSize<T1>() + GetMinimumSize<T2>() + GetMinimumSize<T3>() +
                                                                                                                               GetMinimumSize<T4>() + GetMinimumSize<T5>() + MessageOverhead.MaximumSize : capacity)
    { }
    public byte[] Get(ref MessageOverhead overhead, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
    {
        lock (this)
        {
            Flush();
            Write(Writer1, arg1);
            Write(Writer2, arg2);
            Write(Writer3, arg3);
            Write(Writer4, arg4);
            Write(Writer5, arg5);
            PrependOverhead(ref overhead);
            return ToArray();
        }
    }
    public byte[] Get(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
    {
        lock (this)
        {
            Flush();
            Write(Writer1, arg1);
            Write(Writer2, arg2);
            Write(Writer3, arg3);
            Write(Writer4, arg4);
            Write(Writer5, arg5);
            return ToArray();
        }
    }
}
public sealed class DynamicByteWriter<T1, T2, T3, T4, T5, T6> : ByteWriter
{
    static DynamicByteWriter()
    {
        Writer1 = WriterHelper<T1>.Writer;
        Writer2 = WriterHelper<T2>.Writer;
        Writer3 = WriterHelper<T3>.Writer;
        Writer4 = WriterHelper<T4>.Writer;
        Writer5 = WriterHelper<T5>.Writer;
        Writer6 = WriterHelper<T6>.Writer;
    }
    private static readonly Writer<T1> Writer1;
    private static readonly Writer<T2> Writer2;
    private static readonly Writer<T3> Writer3;
    private static readonly Writer<T4> Writer4;
    private static readonly Writer<T5> Writer5;
    private static readonly Writer<T6> Writer6;
    public DynamicByteWriter(bool shouldPrepend = true, int capacity = 0) : base(shouldPrepend, capacity < 1 ?
                                                                                                                               GetMinimumSize<T1>() + GetMinimumSize<T2>() + GetMinimumSize<T3>() +
                                                                                                                               GetMinimumSize<T4>() + GetMinimumSize<T5>() + GetMinimumSize<T6>()
                                                                                                                               + MessageOverhead.MaximumSize : capacity)
    { }
    public byte[] Get(ref MessageOverhead overhead, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
    {
        lock (this)
        {
            Flush();
            Write(Writer1, arg1);
            Write(Writer2, arg2);
            Write(Writer3, arg3);
            Write(Writer4, arg4);
            Write(Writer5, arg5);
            Write(Writer6, arg6);
            PrependOverhead(ref overhead);
            return ToArray();
        }
    }
    public byte[] Get(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
    {
        lock (this)
        {
            Flush();
            Write(Writer1, arg1);
            Write(Writer2, arg2);
            Write(Writer3, arg3);
            Write(Writer4, arg4);
            Write(Writer5, arg5);
            Write(Writer6, arg6);
            return ToArray();
        }
    }
}
public sealed class DynamicByteWriter<T1, T2, T3, T4, T5, T6, T7> : ByteWriter
{
    static DynamicByteWriter()
    {
        Writer1 = WriterHelper<T1>.Writer;
        Writer2 = WriterHelper<T2>.Writer;
        Writer3 = WriterHelper<T3>.Writer;
        Writer4 = WriterHelper<T4>.Writer;
        Writer5 = WriterHelper<T5>.Writer;
        Writer6 = WriterHelper<T6>.Writer;
        Writer7 = WriterHelper<T7>.Writer;
    }
    private static readonly Writer<T1> Writer1;
    private static readonly Writer<T2> Writer2;
    private static readonly Writer<T3> Writer3;
    private static readonly Writer<T4> Writer4;
    private static readonly Writer<T5> Writer5;
    private static readonly Writer<T6> Writer6;
    private static readonly Writer<T7> Writer7;
    public DynamicByteWriter(bool shouldPrepend = true, int capacity = 0) : base(shouldPrepend, capacity < 1 ?
                                                                                                                               GetMinimumSize<T1>() + GetMinimumSize<T2>() + GetMinimumSize<T3>() +
                                                                                                                               GetMinimumSize<T4>() + GetMinimumSize<T5>() + GetMinimumSize<T6>() +
                                                                                                                               GetMinimumSize<T7>() + MessageOverhead.MaximumSize : capacity)
    { }
    public byte[] Get(ref MessageOverhead overhead, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7)
    {
        lock (this)
        {
            Flush();
            Write(Writer1, arg1);
            Write(Writer2, arg2);
            Write(Writer3, arg3);
            Write(Writer4, arg4);
            Write(Writer5, arg5);
            Write(Writer6, arg6);
            Write(Writer7, arg7);
            PrependOverhead(ref overhead);
            return ToArray();
        }
    }
    public byte[] Get(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7)
    {
        lock (this)
        {
            Flush();
            Write(Writer1, arg1);
            Write(Writer2, arg2);
            Write(Writer3, arg3);
            Write(Writer4, arg4);
            Write(Writer5, arg5);
            Write(Writer6, arg6);
            Write(Writer7, arg7);
            return ToArray();
        }
    }
}
public sealed class DynamicByteWriter<T1, T2, T3, T4, T5, T6, T7, T8> : ByteWriter
{
    static DynamicByteWriter()
    {
        Writer1 = WriterHelper<T1>.Writer;
        Writer2 = WriterHelper<T2>.Writer;
        Writer3 = WriterHelper<T3>.Writer;
        Writer4 = WriterHelper<T4>.Writer;
        Writer5 = WriterHelper<T5>.Writer;
        Writer6 = WriterHelper<T6>.Writer;
        Writer7 = WriterHelper<T7>.Writer;
        Writer8 = WriterHelper<T8>.Writer;
    }
    private static readonly Writer<T1> Writer1;
    private static readonly Writer<T2> Writer2;
    private static readonly Writer<T3> Writer3;
    private static readonly Writer<T4> Writer4;
    private static readonly Writer<T5> Writer5;
    private static readonly Writer<T6> Writer6;
    private static readonly Writer<T7> Writer7;
    private static readonly Writer<T8> Writer8;
    public DynamicByteWriter(bool shouldPrepend = true, int capacity = 0) : base(shouldPrepend, capacity < 1 ?
                                                                                                                               GetMinimumSize<T1>() + GetMinimumSize<T2>() + GetMinimumSize<T3>() +
                                                                                                                               GetMinimumSize<T4>() + GetMinimumSize<T5>() + GetMinimumSize<T6>() +
                                                                                                                               GetMinimumSize<T7>() + GetMinimumSize<T8>() + MessageOverhead.MaximumSize : capacity)
    { }
    public byte[] Get(ref MessageOverhead overhead, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8)
    {
        lock (this)
        {
            Flush();
            Write(Writer1, arg1);
            Write(Writer2, arg2);
            Write(Writer3, arg3);
            Write(Writer4, arg4);
            Write(Writer5, arg5);
            Write(Writer6, arg6);
            Write(Writer7, arg7);
            Write(Writer8, arg8);
            PrependOverhead(ref overhead);
            return ToArray();
        }
    }
    public byte[] Get(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8)
    {
        lock (this)
        {
            Flush();
            Write(Writer1, arg1);
            Write(Writer2, arg2);
            Write(Writer3, arg3);
            Write(Writer4, arg4);
            Write(Writer5, arg5);
            Write(Writer6, arg6);
            Write(Writer7, arg7);
            Write(Writer8, arg8);
            return ToArray();
        }
    }
}
public sealed class DynamicByteWriter<T1, T2, T3, T4, T5, T6, T7, T8, T9> : ByteWriter
{
    static DynamicByteWriter()
    {
        Writer1 = WriterHelper<T1>.Writer;
        Writer2 = WriterHelper<T2>.Writer;
        Writer3 = WriterHelper<T3>.Writer;
        Writer4 = WriterHelper<T4>.Writer;
        Writer5 = WriterHelper<T5>.Writer;
        Writer6 = WriterHelper<T6>.Writer;
        Writer7 = WriterHelper<T7>.Writer;
        Writer8 = WriterHelper<T8>.Writer;
        Writer9 = WriterHelper<T9>.Writer;
    }
    private static readonly Writer<T1> Writer1;
    private static readonly Writer<T2> Writer2;
    private static readonly Writer<T3> Writer3;
    private static readonly Writer<T4> Writer4;
    private static readonly Writer<T5> Writer5;
    private static readonly Writer<T6> Writer6;
    private static readonly Writer<T7> Writer7;
    private static readonly Writer<T8> Writer8;
    private static readonly Writer<T9> Writer9;
    public DynamicByteWriter(bool shouldPrepend = true, int capacity = 0) : base(shouldPrepend, capacity < 1 ?
                                                                                                                               GetMinimumSize<T1>() + GetMinimumSize<T2>() + GetMinimumSize<T3>() +
                                                                                                                               GetMinimumSize<T4>() + GetMinimumSize<T5>() + GetMinimumSize<T6>() +
                                                                                                                               GetMinimumSize<T7>() + GetMinimumSize<T8>() + GetMinimumSize<T9>()
                                                                                                                               + MessageOverhead.MaximumSize : capacity)
    { }
    public byte[] Get(ref MessageOverhead overhead, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9)
    {
        lock (this)
        {
            Flush();
            Write(Writer1, arg1);
            Write(Writer2, arg2);
            Write(Writer3, arg3);
            Write(Writer4, arg4);
            Write(Writer5, arg5);
            Write(Writer6, arg6);
            Write(Writer7, arg7);
            Write(Writer8, arg8);
            Write(Writer9, arg9);
            PrependOverhead(ref overhead);
            return ToArray();
        }
    }
    public byte[] Get(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9)
    {
        lock (this)
        {
            Flush();
            Write(Writer1, arg1);
            Write(Writer2, arg2);
            Write(Writer3, arg3);
            Write(Writer4, arg4);
            Write(Writer5, arg5);
            Write(Writer6, arg6);
            Write(Writer7, arg7);
            Write(Writer8, arg8);
            Write(Writer9, arg9);
            return ToArray();
        }
    }
}
public sealed class DynamicByteWriter<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> : ByteWriter
{
    static DynamicByteWriter()
    {
        Writer1 = WriterHelper<T1>.Writer;
        Writer2 = WriterHelper<T2>.Writer;
        Writer3 = WriterHelper<T3>.Writer;
        Writer4 = WriterHelper<T4>.Writer;
        Writer5 = WriterHelper<T5>.Writer;
        Writer6 = WriterHelper<T6>.Writer;
        Writer7 = WriterHelper<T7>.Writer;
        Writer8 = WriterHelper<T8>.Writer;
        Writer9 = WriterHelper<T9>.Writer;
        Writer10 = WriterHelper<T10>.Writer;
    }
    private static readonly Writer<T1> Writer1;
    private static readonly Writer<T2> Writer2;
    private static readonly Writer<T3> Writer3;
    private static readonly Writer<T4> Writer4;
    private static readonly Writer<T5> Writer5;
    private static readonly Writer<T6> Writer6;
    private static readonly Writer<T7> Writer7;
    private static readonly Writer<T8> Writer8;
    private static readonly Writer<T9> Writer9;
    private static readonly Writer<T10> Writer10;
    public DynamicByteWriter(bool shouldPrepend = true, int capacity = 0) : base(shouldPrepend, capacity < 1 ?
                                                                                                                               GetMinimumSize<T1>() + GetMinimumSize<T2>() + GetMinimumSize<T3>() +
                                                                                                                               GetMinimumSize<T4>() + GetMinimumSize<T5>() + GetMinimumSize<T6>() +
                                                                                                                               GetMinimumSize<T7>() + GetMinimumSize<T8>() + GetMinimumSize<T9>() +
                                                                                                                               GetMinimumSize<T10>() + MessageOverhead.MaximumSize : capacity)
    { }
    public byte[] Get(ref MessageOverhead overhead, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10)
    {
        lock (this)
        {
            Flush();
            Write(Writer1, arg1);
            Write(Writer2, arg2);
            Write(Writer3, arg3);
            Write(Writer4, arg4);
            Write(Writer5, arg5);
            Write(Writer6, arg6);
            Write(Writer7, arg7);
            Write(Writer8, arg8);
            Write(Writer9, arg9);
            Write(Writer10, arg10);
            PrependOverhead(ref overhead);
            return ToArray();
        }
    }
    public byte[] Get(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10)
    {
        lock (this)
        {
            Flush();
            Write(Writer1, arg1);
            Write(Writer2, arg2);
            Write(Writer3, arg3);
            Write(Writer4, arg4);
            Write(Writer5, arg5);
            Write(Writer6, arg6);
            Write(Writer7, arg7);
            Write(Writer8, arg8);
            Write(Writer9, arg9);
            Write(Writer10, arg10);
            return ToArray();
        }
    }
}
// ReSharper restore ConvertToPrimaryConstructor