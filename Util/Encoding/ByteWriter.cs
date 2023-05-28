using DevkitServer.Multiplayer.Networking;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// ReSharper disable AssignNullToNotNullAttribute

namespace DevkitServer.Util.Encoding;

public class ByteWriter
{
    public delegate void Writer<in T>(ByteWriter writer, T arg1);
    private static readonly bool IsBigEndian = !BitConverter.IsLittleEndian;
    private int _size;
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
    public bool ShouldPrepend { get; protected set; }
    public int BaseCapacity { get; set; }

    public ByteWriter(bool shouldPrepend = true, int capacity = 0)
    {
        this.BaseCapacity = capacity;
        _buffer = BaseCapacity < 1 ? Array.Empty<byte>() : new byte[BaseCapacity];
        this.ShouldPrepend = shouldPrepend;
    }
    /// <summary>The value of <paramref name="overhead"/>.<see cref="MessageOverhead.Size">Size</see> will be overwritten.</summary>
    public unsafe void PrependData(ref MessageOverhead overhead)
    {
        MessageOverhead.SetSize(ref overhead, _size);
        if (!ShouldPrepend)
            return;
        if (_streamMode)
            throw new NotSupportedException("Prepending is not supported in stream mode.");
        if (_size == 0)
        {
            _buffer = overhead.GetBytes();
            return;
        }
        int ttl = overhead.Length;
        byte* staloc = stackalloc byte[ttl];
        overhead.GetBytes(staloc, out _);
        byte[] old = _buffer;
        _buffer = new byte[_size + ttl];
        System.Buffer.BlockCopy(old, 0, _buffer, ttl, _size);
        fixed (byte* ptr = _buffer)
            System.Buffer.MemoryCopy(staloc, ptr, ttl, ttl);
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
    public void ExtendBuffer(int newsize)
    {
        if (_streamMode)
            throw new NotSupportedException("Resizing the buffer is not supported in stream mode.");
        ExtendBufferIntl(newsize);
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
    public void WriteStruct<T>(in T value) where T : unmanaged
    {
        WriteInternal(in value);
    }
    private static unsafe void Reverse(byte* litEndStrt, int size)
    {
        byte* stack = stackalloc byte[size];
        System.Buffer.MemoryCopy(litEndStrt, stack, size, size);
        for (int i = 0; i < size; i++)
            litEndStrt[i] = stack[size - i - 1];
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void EndianCheck(byte* litEndStrt, int size)
    {
        if (IsBigEndian && size > 1) Reverse(litEndStrt, size);
    }
    private unsafe void WriteInternal<T>(T value) where T : unmanaged
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
    private unsafe void WriteInternal<T>(in T value) where T : unmanaged
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

    private static readonly MethodInfo WriteInt32Method = typeof(ByteWriter).GetMethod(nameof(Write), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(int) }, null);
    public void Write(int n) => WriteInternal(n);


    private static readonly MethodInfo WriteNullableInt32Method = typeof(ByteWriter).GetMethod(nameof(WriteNullable), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(int?) }, null);
    public void WriteNullable(int? n)
    {
        if (n.HasValue)
        {
            Write(true);
            WriteInternal(n.Value);
        }
        else Write(false);
    }

    private static readonly MethodInfo WriteUInt32Method = typeof(ByteWriter).GetMethod(nameof(Write), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(uint) }, null);
    public void Write(uint n) => WriteInternal(n);


    private static readonly MethodInfo WriteNullableUInt32Method = typeof(ByteWriter).GetMethod(nameof(WriteNullable), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(uint?) }, null);
    public void WriteNullable(uint? n)
    {
        if (n.HasValue)
        {
            Write(true);
            WriteInternal(n.Value);
        }
        else Write(false);
    }


    private static readonly MethodInfo WriteUInt8Method = typeof(ByteWriter).GetMethod(nameof(Write), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(byte) }, null);
    public void Write(byte n) => WriteInternal(n);


    private static readonly MethodInfo WriteNullableUInt8Method = typeof(ByteWriter).GetMethod(nameof(WriteNullable), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(byte?) }, null);
    public void WriteNullable(byte? n)
    {
        if (n.HasValue)
        {
            Write(true);
            WriteInternal(n.Value);
        }
        else Write(false);
    }


    private static readonly MethodInfo WriteInt8Method = typeof(ByteWriter).GetMethod(nameof(Write), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(sbyte) }, null);
    public void Write(sbyte n) => WriteInternal(unchecked((byte)n));


    private static readonly MethodInfo WriteNullableInt8Method = typeof(ByteWriter).GetMethod(nameof(WriteNullable), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(sbyte?) }, null);
    public void WriteNullable(sbyte? n)
    {
        if (n.HasValue)
        {
            Write(true);
            WriteInternal(unchecked((byte)n.Value));
        }
        else Write(false);
    }


    private static readonly MethodInfo WriteBooleanMethod = typeof(ByteWriter).GetMethod(nameof(Write), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(bool) }, null);
    public void Write(bool n) => WriteInternal((byte)(n ? 1 : 0));


    private static readonly MethodInfo WriteNullableBooleanMethod = typeof(ByteWriter).GetMethod(nameof(WriteNullable), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(bool?) }, null);
    public void WriteNullable(bool? n)
    {
        if (n.HasValue)
        {
            Write(true);
            WriteInternal(n.Value);
        }
        else Write(false);
    }


    private static readonly MethodInfo WriteInt64Method = typeof(ByteWriter).GetMethod(nameof(Write), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(long) }, null);
    public void Write(long n) => WriteInternal(n);


    private static readonly MethodInfo WriteNullableInt64Method = typeof(ByteWriter).GetMethod(nameof(WriteNullable), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(long?) }, null);
    public void WriteNullable(long? n)
    {
        if (n.HasValue)
        {
            Write(true);
            WriteInternal(n.Value);
        }
        else Write(false);
    }


    private static readonly MethodInfo WriteUInt64Method = typeof(ByteWriter).GetMethod(nameof(Write), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(ulong) }, null);
    public void Write(ulong n) => WriteInternal(n);


    private static readonly MethodInfo WriteNullableUInt64Method = typeof(ByteWriter).GetMethod(nameof(WriteNullable), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(ulong?) }, null);
    public void WriteNullable(ulong? n)
    {
        if (n.HasValue)
        {
            Write(true);
            WriteInternal(n.Value);
        }
        else Write(false);
    }


    private static readonly MethodInfo WriteInt16Method = typeof(ByteWriter).GetMethod(nameof(Write), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(short) }, null);
    public void Write(short n) => WriteInternal(n);


    private static readonly MethodInfo WriteNullableInt16Method = typeof(ByteWriter).GetMethod(nameof(WriteNullable), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(short?) }, null);
    public void WriteNullable(short? n)
    {
        if (n.HasValue)
        {
            Write(true);
            WriteInternal(n.Value);
        }
        else Write(false);
    }


    private static readonly MethodInfo WriteUInt16Method = typeof(ByteWriter).GetMethod(nameof(Write), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(ushort) }, null);
    public void Write(ushort n) => WriteInternal(n);

    public void WriteInt24(int n)
    {
        if (n > DevkitServerUtility.Int24Bounds)
            n = DevkitServerUtility.Int24Bounds;
        if (n < -DevkitServerUtility.Int24Bounds)
            n = -DevkitServerUtility.Int24Bounds;
        n += DevkitServerUtility.Int24Bounds;
        // sign bit
        byte b = (byte)((n >> 16) & 0xFF);
        WriteInternal((ushort)(n & 0xFFFF));
        WriteInternal(b);
    }

    public void WriteUInt24(uint n) => WriteInt24(unchecked((int)n));

    private static readonly MethodInfo WriteNullableUInt16Method = typeof(ByteWriter).GetMethod(nameof(WriteNullable), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(ushort?) }, null);
    public void WriteNullable(ushort? n)
    {
        if (n.HasValue)
        {
            Write(true);
            WriteInternal(n.Value);
        }
        else Write(false);
    }


    private static readonly MethodInfo WriteFloatMethod = typeof(ByteWriter).GetMethod(nameof(Write), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(float) }, null);
    public void Write(float n) => WriteInternal(n);


    private static readonly MethodInfo WriteNullableFloatMethod = typeof(ByteWriter).GetMethod(nameof(WriteNullable), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(float?) }, null);
    public void WriteNullable(float? n)
    {
        if (n.HasValue)
        {
            Write(true);
            WriteInternal(n.Value);
        }
        else Write(false);
    }


    private static readonly MethodInfo WriteDecimalMethod = typeof(ByteWriter).GetMethod(nameof(Write), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(decimal) }, null);
    public void Write(decimal n) => WriteInternal(n);


    private static readonly MethodInfo WriteNullableDecimalMethod = typeof(ByteWriter).GetMethod(nameof(WriteNullable), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(decimal?) }, null);
    public void WriteNullable(decimal? n)
    {
        if (n.HasValue)
        {
            Write(true);
            WriteInternal(n.Value);
        }
        else Write(false);
    }


    private static readonly MethodInfo WriteDoubleMethod = typeof(ByteWriter).GetMethod(nameof(Write), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(double) }, null);
    public void Write(double n) => WriteInternal(n);


    private static readonly MethodInfo WriteNullableDoubleMethod = typeof(ByteWriter).GetMethod(nameof(WriteNullable), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(double?) }, null);
    public void WriteNullable(double? n)
    {
        if (n.HasValue)
        {
            Write(true);
            WriteInternal(n.Value);
        }
        else Write(false);
    }


    private static readonly MethodInfo WriteCharMethod = typeof(ByteWriter).GetMethod(nameof(Write), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(char) }, null);
    public void Write(char n) => WriteInternal(n);


    private static readonly MethodInfo WriteNullableCharMethod = typeof(ByteWriter).GetMethod(nameof(WriteNullable), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(char?) }, null);
    public void WriteNullable(char? n)
    {
        if (n.HasValue)
        {
            Write(true);
            WriteInternal(n.Value);
        }
        else Write(false);
    }


    private static readonly MethodInfo WriteStringMethod = typeof(ByteWriter).GetMethod(nameof(Write), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(string) }, null);
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

    private static readonly MethodInfo WriteTypeArrayMethod = typeof(ByteWriter).GetMethod(nameof(Write), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(Type[]) }, null);
    public void Write(Type?[] types)
    {
        ushort len = (ushort)Math.Min(types.Length, ushort.MaxValue);
        Write(len);
        for (int i = 0; i < len; ++i)
            Write(types[i]);
    }


    private static readonly MethodInfo WriteTypeMethod = typeof(ByteWriter).GetMethod(nameof(Write), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(Type) }, null);
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
        Write(ns + type.Name);
    }


    private static readonly MethodInfo WriteNullableStringMethod = typeof(ByteWriter).GetMethod(nameof(WriteNullable), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(string) }, null);
    public void WriteNullable(string? n)
    {
        if (n is not null)
        {
            Write(true);
            Write(n);
        }
        else Write(false);
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


    private static readonly MethodInfo WriteDateTimeMethod = typeof(ByteWriter).GetMethod(nameof(Write), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(DateTime) }, null);
    public void Write(DateTime n) => WriteInternal(n.ToBinary());

    private static readonly MethodInfo WriteNullableDateTimeMethod = typeof(ByteWriter).GetMethod(nameof(WriteNullable), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(DateTime?) }, null);
    public void WriteNullable(DateTime? n)
    {
        if (n.HasValue)
        {
            Write(true);
            Write(n.Value);
        }
        else Write(false);
    }

    private static readonly MethodInfo WriteDateTimeOffsetMethod = typeof(ByteWriter).GetMethod(nameof(Write), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(DateTimeOffset) }, null);
    public void Write(DateTimeOffset n)
    {
        Write(n.DateTime);
        Write((short)Math.Round(n.Offset.TotalMinutes));
    }

    private static readonly MethodInfo WriteNullableDateTimeOffsetMethod = typeof(ByteWriter).GetMethod(nameof(WriteNullable), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(DateTimeOffset?) }, null);
    public void WriteNullable(DateTimeOffset? n)
    {
        if (n.HasValue)
        {
            Write(true);
            Write(n.Value);
        }
        else Write(false);
    }


    private static readonly MethodInfo WriteTimeSpanMethod = typeof(ByteWriter).GetMethod(nameof(Write), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(TimeSpan) }, null);
    public void Write(TimeSpan n) => WriteInternal(n.Ticks);


    private static readonly MethodInfo WriteNullableTimeSpanMethod = typeof(ByteWriter).GetMethod(nameof(WriteNullable), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(TimeSpan?) }, null);
    public void WriteNullable(TimeSpan? n)
    {
        if (n.HasValue)
        {
            Write(true);
            Write(n.Value);
        }
        else Write(false);
    }

    private static readonly MethodInfo WriteGUIDMethod = typeof(ByteWriter).GetMethod(nameof(Write), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(Guid) }, null);
    public void Write(Guid n) => WriteInternal(n);


    private static readonly MethodInfo WriteNullableGUIDMethod = typeof(ByteWriter).GetMethod(nameof(WriteNullable), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(Guid?) }, null);
    public void WriteNullable(Guid? n)
    {
        if (n.HasValue)
        {
            Write(true);
            WriteInternal(n.Value);
        }
        else Write(false);
    }

    private static readonly MethodInfo WriteVector2Method = typeof(ByteWriter).GetMethod(nameof(Write), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(Vector2) }, null);
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


    private static readonly MethodInfo WriteNullableVector2Method = typeof(ByteWriter).GetMethod(nameof(WriteNullable), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(Vector2?) }, null);
    public void WriteNullable(Vector2? n)
    {
        if (n.HasValue)
        {
            Write(true);
            Write(n.Value);
        }
        else Write(false);
    }


    private static readonly MethodInfo WriteVector3Method = typeof(ByteWriter).GetMethod(nameof(Write), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(Vector3) }, null);
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


    private static readonly MethodInfo WriteNullableVector3Method = typeof(ByteWriter).GetMethod(nameof(WriteNullable), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(Vector3?) }, null);
    public void WriteNullable(Vector3? n)
    {
        if (n.HasValue)
        {
            Write(true);
            Write(n.Value);
        }
        else Write(false);
    }


    private static readonly MethodInfo WriteVector4Method = typeof(ByteWriter).GetMethod(nameof(Write), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(Vector4) }, null);
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


    private static readonly MethodInfo WriteNullableVector4Method = typeof(ByteWriter).GetMethod(nameof(WriteNullable), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(Vector4?) }, null);
    public void WriteNullable(Vector4? n)
    {
        if (n.HasValue)
        {
            Write(true);
            Write(n.Value);
        }
        else Write(false);
    }
    


    private static readonly MethodInfo WriteBoundsMethod = typeof(ByteWriter).GetMethod(nameof(Write), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(Bounds) }, null);
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


    private static readonly MethodInfo WriteNullableBoundsMethod = typeof(ByteWriter).GetMethod(nameof(WriteNullable), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(Bounds?) }, null);
    public void WriteNullable(Bounds? n)
    {
        if (n.HasValue)
        {
            Write(true);
            Write(n.Value);
        }
        else Write(false);
    }


    private static readonly MethodInfo WriteQuaternionMethod = typeof(ByteWriter).GetMethod(nameof(Write), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(Quaternion) }, null);
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


    private static readonly MethodInfo WriteNullableQuaternionMethod = typeof(ByteWriter).GetMethod(nameof(WriteNullable), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(Quaternion?) }, null);
    public void WriteNullable(Quaternion? n)
    {
        if (n.HasValue)
        {
            Write(true);
            Write(n.Value);
        }
        else Write(false);
    }


    private static readonly MethodInfo WriteColorMethod = typeof(ByteWriter).GetMethod(nameof(Write), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(Color) }, null);
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


    private static readonly MethodInfo WriteNullableColorMethod = typeof(ByteWriter).GetMethod(nameof(WriteNullable), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(Color?) }, null);
    public void WriteNullable(Color? n)
    {
        if (n.HasValue)
        {
            Write(true);
            Write(n.Value);
        }
        else Write(false);
    }


    private static readonly MethodInfo WriteColor32Method = typeof(ByteWriter).GetMethod(nameof(Write), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(Color32) }, null);
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


    private static readonly MethodInfo WriteNullableColor32Method = typeof(ByteWriter).GetMethod(nameof(WriteNullable), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(Color32?) }, null);
    public void WriteNullable(Color32? n)
    {
        if (n.HasValue)
        {
            Write(true);
            Write(n.Value);
        }
        else Write(false);
    }
    
    private static readonly MethodInfo WriteGuidArrayMethod = typeof(ByteWriter).GetMethod(nameof(Write), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(Guid[]) }, null);
    public void Write(Guid[] n) => WriteInternal(n);


    private static readonly MethodInfo WriteNullableGuidArrayMethod = typeof(ByteWriter).GetMethod(nameof(WriteNullable), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(Guid[]) }, null);
    public void WriteNullable(Guid[]? n)
    {
        if (n is not null)
        {
            Write(true);
            Write(n);
        }
        else Write(false);
    }
    private static readonly MethodInfo WriteDateTimeArrayMethod = typeof(ByteWriter).GetMethod(nameof(Write), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(DateTime[]) }, null);
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


    private static readonly MethodInfo WriteNullableDateTimeArrayMethod = typeof(ByteWriter).GetMethod(nameof(WriteNullable), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(DateTime[]) }, null);
    public void WriteNullable(DateTime[]? n)
    {
        if (n is not null)
        {
            Write(true);
            Write(n);
        }
        else Write(false);
    }
    private static readonly MethodInfo WriteDateTimeOffsetArrayMethod = typeof(ByteWriter).GetMethod(nameof(Write), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(DateTimeOffset[]) }, null);
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


    private static readonly MethodInfo WriteNullableDateTimeOffsetArrayMethod = typeof(ByteWriter).GetMethod(nameof(WriteNullable), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(DateTimeOffset[]) }, null);
    public void WriteNullable(DateTimeOffset[]? n)
    {
        if (n is not null)
        {
            Write(true);
            Write(n);
        }
        else Write(false);
    }

    private static readonly MethodInfo WriteEnumMethod = typeof(ByteWriter).GetMethod(nameof(WriteEnum), BindingFlags.Instance | BindingFlags.NonPublic);
    private void WriteEnum<TEnum>(TEnum o) where TEnum : unmanaged, Enum => WriteInternal(o);


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write<TEnum>(TEnum o) where TEnum : unmanaged, Enum => WriteEnum(o);


    private static readonly MethodInfo WriteNullableEnumMethod = typeof(ByteWriter).GetMethod(nameof(WriteNullableEnum), BindingFlags.Instance | BindingFlags.NonPublic);
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
    public void WriteNullable<TEnum>(TEnum? n) where TEnum : unmanaged, Enum => WriteNullableEnum(n);


    private static readonly MethodInfo WriteByteArrayMethod = typeof(ByteWriter).GetMethod(nameof(Write), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(byte[]) }, null);
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


    private static readonly MethodInfo WriteNullableUInt8ArrayMethod = typeof(ByteWriter).GetMethod(nameof(WriteNullable), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(byte[]) }, null);
    public void WriteNullable(byte[]? n)
    {
        if (n is not null)
        {
            Write(true);
            Write(n);
        }
        else Write(false);
    }

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
            _buffer = BaseCapacity < 1 ? Array.Empty<byte>() : new byte[BaseCapacity];
            _size = 0;
        }
    }


    private static readonly MethodInfo WriteInt32ArrayMethod = typeof(ByteWriter).GetMethod(nameof(Write), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(int[]) }, null);
    public void Write(int[] n) => WriteInternal(n);


    private static readonly MethodInfo WriteNullableInt32ArrayMethod = typeof(ByteWriter).GetMethod(nameof(WriteNullable), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(int[]) }, null);
    public void WriteNullable(int[]? n)
    {
        if (n is not null)
        {
            Write(true);
            Write(n);
        }
        else Write(false);
    }


    private static readonly MethodInfo WriteUInt32ArrayMethod = typeof(ByteWriter).GetMethod(nameof(Write), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(uint[]) }, null);
    public void Write(uint[] n) => WriteInternal(n);


    private static readonly MethodInfo WriteNullableUInt32ArrayMethod = typeof(ByteWriter).GetMethod(nameof(WriteNullable), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(uint[]) }, null);
    public void WriteNullable(uint[]? n)
    {
        if (n is not null)
        {
            Write(true);
            Write(n);
        }
        else Write(false);
    }


    private static readonly MethodInfo WriteInt8ArrayMethod = typeof(ByteWriter).GetMethod(nameof(Write), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(sbyte[]) }, null);
    public void Write(sbyte[] n) => WriteInternal(n);


    private static readonly MethodInfo WriteNullableInt8ArrayMethod = typeof(ByteWriter).GetMethod(nameof(WriteNullable), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(sbyte[]) }, null);
    public void WriteNullable(sbyte[]? n)
    {
        if (n is not null)
        {
            Write(true);
            Write(n);
        }
        else Write(false);
    }


    private static readonly MethodInfo WriteBooleanArrayMethod = typeof(ByteWriter).GetMethod(nameof(Write), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(bool[]) }, null);
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


    private static readonly MethodInfo WriteNullableBooleanArrayMethod = typeof(ByteWriter).GetMethod(nameof(WriteNullable), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(bool[]) }, null);
    public void WriteNullable(bool[]? n)
    {
        if (n is not null)
        {
            Write(true);
            Write(n);
        }
        else Write(false);
    }


    private static readonly MethodInfo WriteInt64ArrayMethod = typeof(ByteWriter).GetMethod(nameof(Write), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(long[]) }, null);
    public void Write(long[] n) => WriteInternal(n);


    private static readonly MethodInfo WriteNullableInt64ArrayMethod = typeof(ByteWriter).GetMethod(nameof(WriteNullable), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(long[]) }, null);
    public void WriteNullable(long[]? n)
    {
        if (n is not null)
        {
            Write(true);
            Write(n);
        }
        else Write(false);
    }


    private static readonly MethodInfo WriteUInt64ArrayMethod = typeof(ByteWriter).GetMethod(nameof(Write), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(ulong[]) }, null);
    public void Write(ulong[] n) => WriteInternal(n);


    private static readonly MethodInfo WriteNullableUInt64ArrayMethod = typeof(ByteWriter).GetMethod(nameof(WriteNullable), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(ulong[]) }, null);
    public void WriteNullable(ulong[]? n)
    {
        if (n is not null)
        {
            Write(true);
            Write(n);
        }
        else Write(false);
    }


    private static readonly MethodInfo WriteInt16ArrayMethod = typeof(ByteWriter).GetMethod(nameof(Write), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(short[]) }, null);
    public void Write(short[] n) => WriteInternal(n);


    private static readonly MethodInfo WriteNullableInt16ArrayMethod = typeof(ByteWriter).GetMethod(nameof(WriteNullable), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(short[]) }, null);
    public void WriteNullable(short[]? n)
    {
        if (n is not null)
        {
            Write(true);
            Write(n);
        }
        else Write(false);
    }


    private static readonly MethodInfo WriteUInt16ArrayMethod = typeof(ByteWriter).GetMethod(nameof(Write), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(ushort[]) }, null);
    public void Write(ushort[] n) => WriteInternal(n);


    private static readonly MethodInfo WriteNullableUInt16ArrayMethod = typeof(ByteWriter).GetMethod(nameof(WriteNullable), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(ushort[]) }, null);
    public void WriteNullable(ushort[]? n)
    {
        if (n is not null)
        {
            Write(true);
            Write(n);
        }
        else Write(false);
    }


    private static readonly MethodInfo WriteFloatArrayMethod = typeof(ByteWriter).GetMethod(nameof(Write), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(float[]) }, null);
    public void Write(float[] n) => WriteInternal(n);


    private static readonly MethodInfo WriteNullableFloatArrayMethod = typeof(ByteWriter).GetMethod(nameof(WriteNullable), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(float[]) }, null);
    public void WriteNullable(float[]? n)
    {
        if (n is not null)
        {
            Write(true);
            Write(n);
        }
        else Write(false);
    }


    private static readonly MethodInfo WriteDecimalArrayMethod = typeof(ByteWriter).GetMethod(nameof(Write), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(decimal[]) }, null);
    public void Write(decimal[] n) => WriteInternal(n);


    private static readonly MethodInfo WriteNullableDecimalArrayMethod = typeof(ByteWriter).GetMethod(nameof(WriteNullable), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(decimal[]) }, null);
    public void WriteNullable(decimal[]? n)
    {
        if (n is not null)
        {
            Write(true);
            Write(n);
        }
        else Write(false);
    }


    private static readonly MethodInfo WriteDoubleArrayMethod = typeof(ByteWriter).GetMethod(nameof(Write), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(double[]) }, null);
    public void Write(double[] n) => WriteInternal(n);


    private static readonly MethodInfo WriteNullableDoubleArrayMethod = typeof(ByteWriter).GetMethod(nameof(WriteNullable), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(double[]) }, null);
    public void WriteNullable(double[]? n)
    {
        if (n is not null)
        {
            Write(true);
            Write(n);
        }
        else Write(false);
    }
    


    private static readonly MethodInfo WriteCharArrayMethod = typeof(ByteWriter).GetMethod(nameof(Write), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(char[]) }, null);
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


    private static readonly MethodInfo WriteNullableCharArrayMethod = typeof(ByteWriter).GetMethod(nameof(WriteNullable), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(char[]) }, null);
    public void WriteNullable(char[]? n)
    {
        if (n is not null)
        {
            Write(true);
            Write(n);
        }
        else Write(false);
    }


    private static readonly MethodInfo WriteStringArrayMethod = typeof(ByteWriter).GetMethod(nameof(Write), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(string[]) }, null);
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


    private static readonly MethodInfo WriteNullableStringArrayMethod = typeof(ByteWriter).GetMethod(nameof(WriteNullable), BindingFlags.Instance | BindingFlags.Public, null, new Type[] { typeof(string[]) }, null);
    public void WriteNullable(string[]? n)
    {
        if (n is not null)
        {
            Write(true);
            Write(n);
        }
        else Write(false);
    }

    public byte[] FinishWrite()
    {
        byte[] rtn = _buffer;
        Flush();
        return rtn;
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
        Type origType = type;
        Type[] parameters = new Type[] { typeof(ByteWriter), type ?? throw new ArgumentNullException(nameof(type)) };
        DynamicMethod method = new DynamicMethod("Write" + type.Name, typeof(void), parameters, typeof(ByteWriter), false);
        ILGenerator il = method.GetILGenerator();
        method.DefineParameter(1, ParameterAttributes.None, "writer");
        method.DefineParameter(2, ParameterAttributes.None, "value");

    redo:
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        if (type.IsPrimitive)
        {
            if (type == typeof(ulong))
                il.EmitCall(OpCodes.Call, isNullable ? WriteNullableUInt64Method : WriteUInt64Method, null);
            else if (type == typeof(float))
                il.EmitCall(OpCodes.Call, isNullable ? WriteNullableFloatMethod : WriteFloatMethod, null);
            else if (type == typeof(long))
                il.EmitCall(OpCodes.Call, isNullable ? WriteNullableInt64Method : WriteInt64Method, null);
            else if (type == typeof(ushort))
                il.EmitCall(OpCodes.Call, isNullable ? WriteNullableUInt16Method : WriteUInt16Method, null);
            else if (type == typeof(short))
                il.EmitCall(OpCodes.Call, isNullable ? WriteNullableInt16Method : WriteInt16Method, null);
            else if (type == typeof(byte))
                il.EmitCall(OpCodes.Call, isNullable ? WriteNullableUInt8Method : WriteUInt8Method, null);
            else if (type == typeof(int))
                il.EmitCall(OpCodes.Call, isNullable ? WriteNullableInt32Method : WriteInt32Method, null);
            else if (type == typeof(uint))
                il.EmitCall(OpCodes.Call, isNullable ? WriteNullableUInt32Method : WriteUInt32Method, null);
            else if (type == typeof(bool))
                il.EmitCall(OpCodes.Call, isNullable ? WriteNullableBooleanMethod : WriteBooleanMethod, null);
            else if (type == typeof(char))
                il.EmitCall(OpCodes.Call, isNullable ? WriteNullableCharMethod : WriteCharMethod, null);
            else if (type == typeof(sbyte))
                il.EmitCall(OpCodes.Call, isNullable ? WriteNullableInt8Method : WriteInt8Method, null);
            else if (type == typeof(double))
                il.EmitCall(OpCodes.Call, isNullable ? WriteNullableDoubleMethod : WriteDoubleMethod, null);
            else throw new ArgumentException($"Can not convert that type ({type.Name})!", nameof(type));
        }
        else if (type == typeof(string))
        {
            il.EmitCall(OpCodes.Call, isNullable ? WriteNullableStringMethod : WriteStringMethod, null);
        }
        else if (type.IsEnum)
        {
            il.EmitCall(OpCodes.Call, isNullable ? WriteNullableEnumMethod.MakeGenericMethod(origType) : WriteEnumMethod.MakeGenericMethod(origType), null);
        }
        else if (!isNullable && type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            type = type.GenericTypeArguments[0];
            isNullable = true;
            goto redo;
        }
        else if (type == typeof(decimal))
        {
            il.EmitCall(OpCodes.Call, isNullable ? WriteNullableDecimalMethod : WriteDecimalMethod, null);
        }
        else if (typeof(Type).IsAssignableFrom(type))
        {
            il.EmitCall(OpCodes.Call, WriteTypeMethod, null);
        }
        else if (type == typeof(DateTime))
        {
            il.EmitCall(OpCodes.Call, isNullable ? WriteNullableDateTimeMethod : WriteDateTimeMethod, null);
        }
        else if (type == typeof(DateTimeOffset))
        {
            il.EmitCall(OpCodes.Call, isNullable ? WriteNullableDateTimeOffsetMethod : WriteDateTimeOffsetMethod, null);
        }
        else if (type == typeof(TimeSpan))
        {
            il.EmitCall(OpCodes.Call, isNullable ? WriteNullableTimeSpanMethod : WriteTimeSpanMethod, null);
        }
        else if (type == typeof(Guid))
        {
            il.EmitCall(OpCodes.Call, isNullable ? WriteNullableGUIDMethod : WriteGUIDMethod, null);
        }
        else if (type == typeof(Vector2))
        {
            il.EmitCall(OpCodes.Call, isNullable ? WriteNullableVector2Method : WriteVector2Method, null);
        }
        else if (type == typeof(Vector3))
        {
            il.EmitCall(OpCodes.Call, isNullable ? WriteNullableVector3Method : WriteVector3Method, null);
        }
        else if (type == typeof(Vector4))
        {
            il.EmitCall(OpCodes.Call, isNullable ? WriteNullableVector4Method : WriteVector4Method, null);
        }
        else if (type == typeof(Bounds))
        {
            il.EmitCall(OpCodes.Call, isNullable ? WriteNullableBoundsMethod : WriteBoundsMethod, null);
        }
        else if (type == typeof(Quaternion))
        {
            il.EmitCall(OpCodes.Call, isNullable ? WriteNullableQuaternionMethod : WriteQuaternionMethod, null);
        }
        else if (type == typeof(Color))
        {
            il.EmitCall(OpCodes.Call, isNullable ? WriteNullableColorMethod : WriteColorMethod, null);
        }
        else if (type == typeof(Color32))
        {
            il.EmitCall(OpCodes.Call, isNullable ? WriteNullableColor32Method : WriteColor32Method, null);
        }
        else if (type.IsArray)
        {
            Type elemType = type.GetElementType();
            if (elemType == typeof(ulong))
                il.EmitCall(OpCodes.Call, isNullable ? WriteNullableUInt64ArrayMethod : WriteUInt64ArrayMethod, null);
            else if (elemType == typeof(float))
                il.EmitCall(OpCodes.Call, isNullable ? WriteNullableFloatArrayMethod : WriteFloatArrayMethod, null);
            else if (elemType == typeof(long))
                il.EmitCall(OpCodes.Call, isNullable ? WriteNullableInt64ArrayMethod : WriteInt64ArrayMethod, null);
            else if (elemType == typeof(ushort))
                il.EmitCall(OpCodes.Call, isNullable ? WriteNullableUInt16ArrayMethod : WriteUInt16ArrayMethod, null);
            else if (elemType == typeof(short))
                il.EmitCall(OpCodes.Call, isNullable ? WriteNullableInt16ArrayMethod : WriteInt16ArrayMethod, null);
            else if (elemType == typeof(byte))
                il.EmitCall(OpCodes.Call, isNullable ? WriteNullableUInt8ArrayMethod : WriteByteArrayMethod, null);
            else if (elemType == typeof(int))
                il.EmitCall(OpCodes.Call, isNullable ? WriteNullableInt32ArrayMethod : WriteInt32ArrayMethod, null);
            else if (elemType == typeof(uint))
                il.EmitCall(OpCodes.Call, isNullable ? WriteNullableUInt32ArrayMethod : WriteUInt32ArrayMethod, null);
            else if (elemType == typeof(bool))
                il.EmitCall(OpCodes.Call, isNullable ? WriteNullableBooleanArrayMethod : WriteBooleanArrayMethod, null);
            else if (elemType == typeof(sbyte))
                il.EmitCall(OpCodes.Call, isNullable ? WriteNullableInt8ArrayMethod : WriteInt8ArrayMethod, null);
            else if (elemType == typeof(decimal))
                il.EmitCall(OpCodes.Call, isNullable ? WriteNullableDecimalArrayMethod : WriteDecimalArrayMethod, null);
            else if (elemType == typeof(char))
                il.EmitCall(OpCodes.Call, isNullable ? WriteNullableCharArrayMethod : WriteCharArrayMethod, null);
            else if (elemType == typeof(double))
                il.EmitCall(OpCodes.Call, isNullable ? WriteNullableDoubleArrayMethod : WriteDoubleArrayMethod, null);
            else if (elemType == typeof(string))
                il.EmitCall(OpCodes.Call, isNullable ? WriteNullableStringArrayMethod : WriteStringArrayMethod, null);
            else if (typeof(Type).IsAssignableFrom(elemType))
                il.EmitCall(OpCodes.Call, WriteTypeArrayMethod, null);
            else if (elemType == typeof(Guid))
                il.EmitCall(OpCodes.Call, isNullable ? WriteNullableGuidArrayMethod : WriteGuidArrayMethod, null);
            else if (elemType == typeof(DateTime))
                il.EmitCall(OpCodes.Call, isNullable ? WriteNullableDateTimeArrayMethod : WriteDateTimeArrayMethod, null);
            else if (elemType == typeof(DateTimeOffset))
                il.EmitCall(OpCodes.Call, isNullable ? WriteNullableDateTimeOffsetArrayMethod : WriteDateTimeOffsetArrayMethod, null);
            else throw new ArgumentException($"Can not convert that array type ({type.Name})!", nameof(type));
        }
        else throw new ArgumentException($"Can not convert that type ({type.Name})!", nameof(type));
        il.Emit(OpCodes.Ret);
        try
        {
            return method.CreateDelegate(typeof(Writer<>).MakeGenericType(origType));
        }
        catch (InvalidProgramException ex)
        {
            Logger.LogError(ex);
            return null;
        }
        catch (ArgumentException ex)
        {
            Logger.LogError("Failed to create writer delegate for type " + origType.FullName);
            Logger.LogError(ex);
            return null;
        }
    }
    public static MethodInfo? GetWriteMethod(Type type, bool isNullable = false)
    {
    redo:
        if (type.IsPrimitive)
        {
            if (type == typeof(ulong))
                return isNullable ? WriteNullableUInt64Method : WriteUInt64Method;
            if (type == typeof(float))
                return isNullable ? WriteNullableFloatMethod : WriteFloatMethod;
            if (type == typeof(long))
                return isNullable ? WriteNullableInt64Method : WriteInt64Method;
            if (type == typeof(ushort))
                return isNullable ? WriteNullableUInt16Method : WriteUInt16Method;
            if (type == typeof(short))
                return isNullable ? WriteNullableInt16Method : WriteInt16Method;
            if (type == typeof(byte))
                return isNullable ? WriteNullableUInt8Method : WriteUInt8Method;
            if (type == typeof(int))
                return isNullable ? WriteNullableInt32Method : WriteInt32Method;
            if (type == typeof(uint))
                return isNullable ? WriteNullableUInt32Method : WriteUInt32Method;
            if (type == typeof(bool))
                return isNullable ? WriteNullableBooleanMethod : WriteBooleanMethod;
            if (type == typeof(char))
                return isNullable ? WriteNullableCharMethod : WriteCharMethod;
            if (type == typeof(sbyte))
                return isNullable ? WriteNullableInt8Method : WriteInt8Method;
            if (type == typeof(double))
                return isNullable ? WriteNullableDoubleMethod : WriteDoubleMethod;
        }

        if (type == typeof(string))
            return isNullable ? WriteNullableStringMethod : WriteStringMethod;
        if (type.IsEnum)
            return isNullable ? WriteNullableEnumMethod.MakeGenericMethod(type) : WriteEnumMethod.MakeGenericMethod(type);
        if (!isNullable && type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            type = type.GenericTypeArguments[0];
            isNullable = true;
            goto redo;
        }
        if (type == typeof(decimal))
            return isNullable ? WriteNullableDecimalMethod : WriteDecimalMethod;
        if (type == typeof(DateTime))
            return isNullable ? WriteNullableDateTimeMethod : WriteDateTimeMethod;
        if (type == typeof(DateTimeOffset))
            return isNullable ? WriteNullableDateTimeOffsetMethod : WriteDateTimeOffsetMethod;
        if (type == typeof(TimeSpan))
            return isNullable ? WriteNullableTimeSpanMethod : WriteTimeSpanMethod;
        if (type == typeof(Guid))
            return isNullable ? WriteNullableGUIDMethod : WriteGUIDMethod;
        if (type == typeof(Vector2))
            return isNullable ? WriteNullableVector2Method : WriteVector2Method;
        if (type == typeof(Vector3))
            return isNullable ? WriteNullableVector3Method : WriteVector3Method;
        if (type == typeof(Vector4))
            return isNullable ? WriteNullableVector4Method : WriteVector4Method;
        if (type == typeof(Bounds))
            return isNullable ? WriteNullableBoundsMethod : WriteBoundsMethod;
        if (type == typeof(Quaternion))
            return isNullable ? WriteNullableQuaternionMethod : WriteQuaternionMethod;
        if (type == typeof(Color))
            return isNullable ? WriteNullableColorMethod : WriteColorMethod;
        if (type == typeof(Color32)) 
                return isNullable ? WriteNullableColor32Method : WriteColor32Method;
        if (type.IsArray)
        {
            Type elemType = type.GetElementType();
            if (elemType == typeof(ulong))
                return isNullable ? WriteNullableUInt64ArrayMethod : WriteUInt64ArrayMethod;
            if (elemType == typeof(float))
                return isNullable ? WriteNullableFloatArrayMethod : WriteFloatArrayMethod;
            if (elemType == typeof(long))
                return isNullable ? WriteNullableInt64ArrayMethod : WriteInt64ArrayMethod;
            if (elemType == typeof(ushort))
                return isNullable ? WriteNullableUInt16ArrayMethod : WriteUInt16ArrayMethod;
            if (elemType == typeof(short))
                return isNullable ? WriteNullableInt16ArrayMethod : WriteInt16ArrayMethod;
            if (elemType == typeof(byte))
                return isNullable ? WriteNullableUInt8ArrayMethod : WriteByteArrayMethod;
            if (elemType == typeof(int))
                return isNullable ? WriteNullableInt32ArrayMethod : WriteInt32ArrayMethod;
            if (elemType == typeof(uint))
                return isNullable ? WriteNullableUInt32ArrayMethod : WriteUInt32ArrayMethod;
            if (elemType == typeof(bool))
                return isNullable ? WriteNullableBooleanArrayMethod : WriteBooleanArrayMethod;
            if (elemType == typeof(sbyte))
                return isNullable ? WriteNullableInt8ArrayMethod : WriteInt8ArrayMethod;
            if (elemType == typeof(decimal))
                return isNullable ? WriteNullableDecimalArrayMethod : WriteDecimalArrayMethod;
            if (elemType == typeof(char))
                return isNullable ? WriteNullableCharArrayMethod : WriteCharArrayMethod;
            if (elemType == typeof(double))
                return isNullable ? WriteNullableDoubleArrayMethod : WriteDoubleArrayMethod;
            if (elemType == typeof(string))
                return isNullable ? WriteNullableStringArrayMethod : WriteStringArrayMethod;
            if (elemType == typeof(Guid))
                return isNullable ? WriteNullableGuidArrayMethod : WriteGuidArrayMethod;
            if (elemType == typeof(DateTime))
                return isNullable ? WriteNullableDateTimeArrayMethod : WriteDateTimeArrayMethod;
            if (elemType == typeof(DateTimeOffset))
                return isNullable ? WriteNullableDateTimeOffsetArrayMethod : WriteDateTimeOffsetArrayMethod;
        }

        return null;

    }
    public static Writer<T1> GetWriter<T1>(bool isNullable = false) => (Writer<T1>)GetWriter(typeof(T1), isNullable)!;
    public static int GetMinimumSize(Type type)
    {
        if (type.IsPointer) return IntPtr.Size;
        else if (type.IsArray || type == typeof(string)) return sizeof(ushort);
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
public sealed class ByteWriterRaw<T> : ByteWriter
{
    private readonly Writer<T> writer;
    /// <summary>Leave <paramref name="writer"/> null to auto-fill.</summary>
    public ByteWriterRaw(Writer<T>? writer, bool shouldPrepend = true, int capacity = 0) : base(shouldPrepend, capacity)
    {
        this.writer = writer ?? WriterHelper<T>.Writer;
    }
    public byte[] Get(ref MessageOverhead overhead, T obj)
    {
        lock (this)
        {
            Flush();
            writer.Invoke(this, obj);
            PrependData(ref overhead);
            return ToArray();
        }
    }
    public byte[] Get(T obj)
    {
        lock (this)
        {
            Flush();
            writer.Invoke(this, obj);
            return ToArray();
        }
    }
}
public sealed class ByteWriterRaw<T1, T2> : ByteWriter
{
    private readonly Writer<T1> writer1;
    private readonly Writer<T2> writer2;
    /// <summary>Leave any writer null to auto-fill.</summary>
    public ByteWriterRaw(Writer<T1>? writer1, Writer<T2>? writer2, bool shouldPrepend = true, int capacity = 0) : base(shouldPrepend, capacity)
    {
        this.writer1 = writer1 ?? WriterHelper<T1>.Writer;
        this.writer2 = writer2 ?? WriterHelper<T2>.Writer;
    }
    public byte[] Get(ref MessageOverhead overhead, T1 arg1, T2 arg2)
    {
        lock (this)
        {
            Flush();
            writer1.Invoke(this, arg1);
            writer2.Invoke(this, arg2);
            PrependData(ref overhead);
            return ToArray();
        }
    }
    public byte[] Get(T1 arg1, T2 arg2)
    {
        lock (this)
        {
            Flush();
            writer1.Invoke(this, arg1);
            writer2.Invoke(this, arg2);
            return ToArray();
        }
    }
}
public sealed class ByteWriterRaw<T1, T2, T3> : ByteWriter
{
    private readonly Writer<T1> writer1;
    private readonly Writer<T2> writer2;
    private readonly Writer<T3> writer3;
    /// <summary>Leave any writer null to auto-fill.</summary>
    public ByteWriterRaw(Writer<T1>? writer1, Writer<T2>? writer2, Writer<T3>? writer3, bool shouldPrepend = true, int capacity = 0) : base(shouldPrepend, capacity)
    {
        this.writer1 = writer1 ?? WriterHelper<T1>.Writer;
        this.writer2 = writer2 ?? WriterHelper<T2>.Writer;
        this.writer3 = writer3 ?? WriterHelper<T3>.Writer;
    }
    public byte[] Get(ref MessageOverhead overhead, T1 arg1, T2 arg2, T3 arg3)
    {
        lock (this)
        {
            Flush();
            writer1.Invoke(this, arg1);
            writer2.Invoke(this, arg2);
            writer3.Invoke(this, arg3);
            PrependData(ref overhead);
            return ToArray();
        }
    }
    public byte[] Get(T1 arg1, T2 arg2, T3 arg3)
    {
        lock (this)
        {
            Flush();
            writer1.Invoke(this, arg1);
            writer2.Invoke(this, arg2);
            writer3.Invoke(this, arg3);
            return ToArray();
        }
    }
}
public sealed class ByteWriterRaw<T1, T2, T3, T4> : ByteWriter
{
    private readonly Writer<T1> writer1;
    private readonly Writer<T2> writer2;
    private readonly Writer<T3> writer3;
    private readonly Writer<T4> writer4;
    /// <summary>Leave any writer null to auto-fill.</summary>
    public ByteWriterRaw(Writer<T1>? writer1, Writer<T2>? writer2, Writer<T3>? writer3, Writer<T4>? writer4, bool shouldPrepend = true, int capacity = 0) : base(shouldPrepend, capacity)
    {
        this.writer1 = writer1 ?? WriterHelper<T1>.Writer;
        this.writer2 = writer2 ?? WriterHelper<T2>.Writer;
        this.writer3 = writer3 ?? WriterHelper<T3>.Writer;
        this.writer4 = writer4 ?? WriterHelper<T4>.Writer;
    }
    public byte[] Get(ref MessageOverhead overhead, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
    {
        lock (this)
        {
            Flush();
            writer1.Invoke(this, arg1);
            writer2.Invoke(this, arg2);
            writer3.Invoke(this, arg3);
            writer4.Invoke(this, arg4);
            PrependData(ref overhead);
            return ToArray();
        }
    }
    public byte[] Get(T1 arg1, T2 arg2, T3 arg3, T4 arg4)
    {
        lock (this)
        {
            Flush();
            writer1.Invoke(this, arg1);
            writer2.Invoke(this, arg2);
            writer3.Invoke(this, arg3);
            writer4.Invoke(this, arg4);
            return ToArray();
        }
    }
}
public sealed class ByteWriterRaw<T1, T2, T3, T4, T5> : ByteWriter
{
    private readonly Writer<T1> writer1;
    private readonly Writer<T2> writer2;
    private readonly Writer<T3> writer3;
    private readonly Writer<T4> writer4;
    private readonly Writer<T5> writer5;
    /// <summary>Leave any writer null to auto-fill.</summary>
    public ByteWriterRaw(Writer<T1>? writer1, Writer<T2>? writer2, Writer<T3>? writer3, Writer<T4>? writer4, Writer<T5>? writer5, bool shouldPrepend = true, int capacity = 0) : base(shouldPrepend, capacity)
    {
        this.writer1 = writer1 ?? WriterHelper<T1>.Writer;
        this.writer2 = writer2 ?? WriterHelper<T2>.Writer;
        this.writer3 = writer3 ?? WriterHelper<T3>.Writer;
        this.writer4 = writer4 ?? WriterHelper<T4>.Writer;
        this.writer5 = writer5 ?? WriterHelper<T5>.Writer;
    }
    public byte[] Get(ref MessageOverhead overhead, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
    {
        lock (this)
        {
            Flush();
            writer1.Invoke(this, arg1);
            writer2.Invoke(this, arg2);
            writer3.Invoke(this, arg3);
            writer4.Invoke(this, arg4);
            writer5.Invoke(this, arg5);
            PrependData(ref overhead);
            return ToArray();
        }
    }
    public byte[] Get(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
    {
        lock (this)
        {
            Flush();
            writer1.Invoke(this, arg1);
            writer2.Invoke(this, arg2);
            writer3.Invoke(this, arg3);
            writer4.Invoke(this, arg4);
            writer5.Invoke(this, arg5);
            return ToArray();
        }
    }
}
public sealed class DynamicByteWriter<T1> : ByteWriter
{
    static DynamicByteWriter()
    {
        writer = WriterHelper<T1>.Writer;
    }
    private static readonly Writer<T1> writer;
    public DynamicByteWriter(bool shouldPrepend = true, int capacity = 0) : base(shouldPrepend, capacity < 1 ? GetMinimumSize<T1>() : capacity) { }
    public byte[] Get(ref MessageOverhead overhead, T1 obj)
    {
        lock (this)
        {
            Flush();
            writer.Invoke(this, obj);
            PrependData(ref overhead);
            return ToArray();
        }
    }
    public byte[] Get(T1 obj)
    {
        lock (this)
        {
            Flush();
            writer.Invoke(this, obj);
            return ToArray();
        }
    }
}
public sealed class DynamicByteWriter<T1, T2> : ByteWriter
{
    static DynamicByteWriter()
    {
        writer1 = WriterHelper<T1>.Writer;
        writer2 = WriterHelper<T2>.Writer;
    }
    private static readonly Writer<T1> writer1;
    private static readonly Writer<T2> writer2;
    public DynamicByteWriter(bool shouldPrepend = true, int capacity = 0) : base(shouldPrepend, capacity < 1 ? GetMinimumSize<T1>() + GetMinimumSize<T2>() : capacity) { }
    public byte[] Get(ref MessageOverhead overhead, T1 arg1, T2 arg2)
    {
        lock (this)
        {
            Flush();
            Write(writer1, arg1);
            Write(writer2, arg2);
            PrependData(ref overhead);
            return ToArray();
        }
    }
    public byte[] Get(T1 arg1, T2 arg2)
    {
        lock (this)
        {
            Flush();
            Write(writer1, arg1);
            Write(writer2, arg2);
            return ToArray();
        }
    }
}
public sealed class DynamicByteWriter<T1, T2, T3> : ByteWriter
{
    static DynamicByteWriter()
    {
        writer1 = WriterHelper<T1>.Writer;
        writer2 = WriterHelper<T2>.Writer;
        writer3 = WriterHelper<T3>.Writer;
    }
    private static readonly Writer<T1> writer1;
    private static readonly Writer<T2> writer2;
    private static readonly Writer<T3> writer3;
    public DynamicByteWriter(bool shouldPrepend = true, int capacity = 0) : base(shouldPrepend, capacity < 1 ? GetMinimumSize<T1>() + GetMinimumSize<T2>() + GetMinimumSize<T3>() : capacity) { }
    public byte[] Get(ref MessageOverhead overhead, T1 arg1, T2 arg2, T3 arg3)
    {
        lock (this)
        {
            Flush();
            Write(writer1, arg1);
            Write(writer2, arg2);
            Write(writer3, arg3);
            PrependData(ref overhead);
            return ToArray();
        }
    }
    public byte[] Get(T1 arg1, T2 arg2, T3 arg3)
    {
        lock (this)
        {
            Flush();
            Write(writer1, arg1);
            Write(writer2, arg2);
            Write(writer3, arg3);
            return ToArray();
        }
    }
}
public sealed class DynamicByteWriter<T1, T2, T3, T4> : ByteWriter
{
    static DynamicByteWriter()
    {
        writer1 = WriterHelper<T1>.Writer;
        writer2 = WriterHelper<T2>.Writer;
        writer3 = WriterHelper<T3>.Writer;
        writer4 = WriterHelper<T4>.Writer;
    }
    private static readonly Writer<T1> writer1;
    private static readonly Writer<T2> writer2;
    private static readonly Writer<T3> writer3;
    private static readonly Writer<T4> writer4;
    public DynamicByteWriter(bool shouldPrepend = true, int capacity = 0) : base(shouldPrepend, capacity < 1 ?
                                                                                                                               GetMinimumSize<T1>() + GetMinimumSize<T2>() + GetMinimumSize<T3>() +
                                                                                                                               GetMinimumSize<T4>() : capacity)
    { }
    public byte[] Get(ref MessageOverhead overhead, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
    {
        lock (this)
        {
            Flush();
            Write(writer1, arg1);
            Write(writer2, arg2);
            Write(writer3, arg3);
            Write(writer4, arg4);
            PrependData(ref overhead);
            return ToArray();
        }
    }
    public byte[] Get(T1 arg1, T2 arg2, T3 arg3, T4 arg4)
    {
        lock (this)
        {
            Flush();
            Write(writer1, arg1);
            Write(writer2, arg2);
            Write(writer3, arg3);
            Write(writer4, arg4);
            return ToArray();
        }
    }
}
public sealed class DynamicByteWriter<T1, T2, T3, T4, T5> : ByteWriter
{
    static DynamicByteWriter()
    {
        writer1 = WriterHelper<T1>.Writer;
        writer2 = WriterHelper<T2>.Writer;
        writer3 = WriterHelper<T3>.Writer;
        writer4 = WriterHelper<T4>.Writer;
        writer5 = WriterHelper<T5>.Writer;
    }
    private static readonly Writer<T1> writer1;
    private static readonly Writer<T2> writer2;
    private static readonly Writer<T3> writer3;
    private static readonly Writer<T4> writer4;
    private static readonly Writer<T5> writer5;
    public DynamicByteWriter(bool shouldPrepend = true, int capacity = 0) : base(shouldPrepend, capacity < 1 ?
                                                                                                                               GetMinimumSize<T1>() + GetMinimumSize<T2>() + GetMinimumSize<T3>() +
                                                                                                                               GetMinimumSize<T4>() + GetMinimumSize<T5>() : capacity)
    { }
    public byte[] Get(ref MessageOverhead overhead, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
    {
        lock (this)
        {
            Flush();
            Write(writer1, arg1);
            Write(writer2, arg2);
            Write(writer3, arg3);
            Write(writer4, arg4);
            Write(writer5, arg5);
            PrependData(ref overhead);
            return ToArray();
        }
    }
    public byte[] Get(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
    {
        lock (this)
        {
            Flush();
            Write(writer1, arg1);
            Write(writer2, arg2);
            Write(writer3, arg3);
            Write(writer4, arg4);
            Write(writer5, arg5);
            return ToArray();
        }
    }
}
public sealed class DynamicByteWriter<T1, T2, T3, T4, T5, T6> : ByteWriter
{
    static DynamicByteWriter()
    {
        writer1 = WriterHelper<T1>.Writer;
        writer2 = WriterHelper<T2>.Writer;
        writer3 = WriterHelper<T3>.Writer;
        writer4 = WriterHelper<T4>.Writer;
        writer5 = WriterHelper<T5>.Writer;
        writer6 = WriterHelper<T6>.Writer;
    }
    private static readonly Writer<T1> writer1;
    private static readonly Writer<T2> writer2;
    private static readonly Writer<T3> writer3;
    private static readonly Writer<T4> writer4;
    private static readonly Writer<T5> writer5;
    private static readonly Writer<T6> writer6;
    public DynamicByteWriter(bool shouldPrepend = true, int capacity = 0) : base(shouldPrepend, capacity < 1 ?
                                                                                                                               GetMinimumSize<T1>() + GetMinimumSize<T2>() + GetMinimumSize<T3>() +
                                                                                                                               GetMinimumSize<T4>() + GetMinimumSize<T5>() + GetMinimumSize<T6>() : capacity)
    { }
    public byte[] Get(ref MessageOverhead overhead, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
    {
        lock (this)
        {
            Flush();
            Write(writer1, arg1);
            Write(writer2, arg2);
            Write(writer3, arg3);
            Write(writer4, arg4);
            Write(writer5, arg5);
            Write(writer6, arg6);
            PrependData(ref overhead);
            return ToArray();
        }
    }
    public byte[] Get(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
    {
        lock (this)
        {
            Flush();
            Write(writer1, arg1);
            Write(writer2, arg2);
            Write(writer3, arg3);
            Write(writer4, arg4);
            Write(writer5, arg5);
            Write(writer6, arg6);
            return ToArray();
        }
    }
}
public sealed class DynamicByteWriter<T1, T2, T3, T4, T5, T6, T7> : ByteWriter
{
    static DynamicByteWriter()
    {
        writer1 = WriterHelper<T1>.Writer;
        writer2 = WriterHelper<T2>.Writer;
        writer3 = WriterHelper<T3>.Writer;
        writer4 = WriterHelper<T4>.Writer;
        writer5 = WriterHelper<T5>.Writer;
        writer6 = WriterHelper<T6>.Writer;
        writer7 = WriterHelper<T7>.Writer;
    }
    private static readonly Writer<T1> writer1;
    private static readonly Writer<T2> writer2;
    private static readonly Writer<T3> writer3;
    private static readonly Writer<T4> writer4;
    private static readonly Writer<T5> writer5;
    private static readonly Writer<T6> writer6;
    private static readonly Writer<T7> writer7;
    public DynamicByteWriter(bool shouldPrepend = true, int capacity = 0) : base(shouldPrepend, capacity < 1 ?
                                                                                                                               GetMinimumSize<T1>() + GetMinimumSize<T2>() + GetMinimumSize<T3>() +
                                                                                                                               GetMinimumSize<T4>() + GetMinimumSize<T5>() + GetMinimumSize<T6>() +
                                                                                                                               GetMinimumSize<T7>() : capacity)
    { }
    public byte[] Get(ref MessageOverhead overhead, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7)
    {
        lock (this)
        {
            Flush();
            Write(writer1, arg1);
            Write(writer2, arg2);
            Write(writer3, arg3);
            Write(writer4, arg4);
            Write(writer5, arg5);
            Write(writer6, arg6);
            Write(writer7, arg7);
            PrependData(ref overhead);
            return ToArray();
        }
    }
    public byte[] Get(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7)
    {
        lock (this)
        {
            Flush();
            Write(writer1, arg1);
            Write(writer2, arg2);
            Write(writer3, arg3);
            Write(writer4, arg4);
            Write(writer5, arg5);
            Write(writer6, arg6);
            Write(writer7, arg7);
            return ToArray();
        }
    }
}
public sealed class DynamicByteWriter<T1, T2, T3, T4, T5, T6, T7, T8> : ByteWriter
{
    static DynamicByteWriter()
    {
        writer1 = WriterHelper<T1>.Writer;
        writer2 = WriterHelper<T2>.Writer;
        writer3 = WriterHelper<T3>.Writer;
        writer4 = WriterHelper<T4>.Writer;
        writer5 = WriterHelper<T5>.Writer;
        writer6 = WriterHelper<T6>.Writer;
        writer7 = WriterHelper<T7>.Writer;
        writer8 = WriterHelper<T8>.Writer;
    }
    private static readonly Writer<T1> writer1;
    private static readonly Writer<T2> writer2;
    private static readonly Writer<T3> writer3;
    private static readonly Writer<T4> writer4;
    private static readonly Writer<T5> writer5;
    private static readonly Writer<T6> writer6;
    private static readonly Writer<T7> writer7;
    private static readonly Writer<T8> writer8;
    public DynamicByteWriter(bool shouldPrepend = true, int capacity = 0) : base(shouldPrepend, capacity < 1 ?
                                                                                                                               GetMinimumSize<T1>() + GetMinimumSize<T2>() + GetMinimumSize<T3>() +
                                                                                                                               GetMinimumSize<T4>() + GetMinimumSize<T5>() + GetMinimumSize<T6>() +
                                                                                                                               GetMinimumSize<T7>() + GetMinimumSize<T8>() : capacity)
    { }
    public byte[] Get(ref MessageOverhead overhead, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8)
    {
        lock (this)
        {
            Flush();
            Write(writer1, arg1);
            Write(writer2, arg2);
            Write(writer3, arg3);
            Write(writer4, arg4);
            Write(writer5, arg5);
            Write(writer6, arg6);
            Write(writer7, arg7);
            Write(writer8, arg8);
            PrependData(ref overhead);
            return ToArray();
        }
    }
    public byte[] Get(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8)
    {
        lock (this)
        {
            Flush();
            Write(writer1, arg1);
            Write(writer2, arg2);
            Write(writer3, arg3);
            Write(writer4, arg4);
            Write(writer5, arg5);
            Write(writer6, arg6);
            Write(writer7, arg7);
            Write(writer8, arg8);
            return ToArray();
        }
    }
}
public sealed class DynamicByteWriter<T1, T2, T3, T4, T5, T6, T7, T8, T9> : ByteWriter
{
    static DynamicByteWriter()
    {
        writer1 = WriterHelper<T1>.Writer;
        writer2 = WriterHelper<T2>.Writer;
        writer3 = WriterHelper<T3>.Writer;
        writer4 = WriterHelper<T4>.Writer;
        writer5 = WriterHelper<T5>.Writer;
        writer6 = WriterHelper<T6>.Writer;
        writer7 = WriterHelper<T7>.Writer;
        writer8 = WriterHelper<T8>.Writer;
        writer9 = WriterHelper<T9>.Writer;
    }
    private static readonly Writer<T1> writer1;
    private static readonly Writer<T2> writer2;
    private static readonly Writer<T3> writer3;
    private static readonly Writer<T4> writer4;
    private static readonly Writer<T5> writer5;
    private static readonly Writer<T6> writer6;
    private static readonly Writer<T7> writer7;
    private static readonly Writer<T8> writer8;
    private static readonly Writer<T9> writer9;
    public DynamicByteWriter(bool shouldPrepend = true, int capacity = 0) : base(shouldPrepend, capacity < 1 ?
                                                                                                                               GetMinimumSize<T1>() + GetMinimumSize<T2>() + GetMinimumSize<T3>() +
                                                                                                                               GetMinimumSize<T4>() + GetMinimumSize<T5>() + GetMinimumSize<T6>() +
                                                                                                                               GetMinimumSize<T7>() + GetMinimumSize<T8>() + GetMinimumSize<T9>() : capacity)
    { }
    public byte[] Get(ref MessageOverhead overhead, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9)
    {
        lock (this)
        {
            Flush();
            Write(writer1, arg1);
            Write(writer2, arg2);
            Write(writer3, arg3);
            Write(writer4, arg4);
            Write(writer5, arg5);
            Write(writer6, arg6);
            Write(writer7, arg7);
            Write(writer8, arg8);
            Write(writer9, arg9);
            PrependData(ref overhead);
            return ToArray();
        }
    }
    public byte[] Get(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9)
    {
        lock (this)
        {
            Flush();
            Write(writer1, arg1);
            Write(writer2, arg2);
            Write(writer3, arg3);
            Write(writer4, arg4);
            Write(writer5, arg5);
            Write(writer6, arg6);
            Write(writer7, arg7);
            Write(writer8, arg8);
            Write(writer9, arg9);
            return ToArray();
        }
    }
}
public sealed class DynamicByteWriter<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> : ByteWriter
{
    static DynamicByteWriter()
    {
        writer1 = WriterHelper<T1>.Writer;
        writer2 = WriterHelper<T2>.Writer;
        writer3 = WriterHelper<T3>.Writer;
        writer4 = WriterHelper<T4>.Writer;
        writer5 = WriterHelper<T5>.Writer;
        writer6 = WriterHelper<T6>.Writer;
        writer7 = WriterHelper<T7>.Writer;
        writer8 = WriterHelper<T8>.Writer;
        writer9 = WriterHelper<T9>.Writer;
        writer10 = WriterHelper<T10>.Writer;
    }
    private static readonly Writer<T1> writer1;
    private static readonly Writer<T2> writer2;
    private static readonly Writer<T3> writer3;
    private static readonly Writer<T4> writer4;
    private static readonly Writer<T5> writer5;
    private static readonly Writer<T6> writer6;
    private static readonly Writer<T7> writer7;
    private static readonly Writer<T8> writer8;
    private static readonly Writer<T9> writer9;
    private static readonly Writer<T10> writer10;
    public DynamicByteWriter(bool shouldPrepend = true, int capacity = 0) : base(shouldPrepend, capacity < 1 ?
                                                                                                                               GetMinimumSize<T1>() + GetMinimumSize<T2>() + GetMinimumSize<T3>() +
                                                                                                                               GetMinimumSize<T4>() + GetMinimumSize<T5>() + GetMinimumSize<T6>() +
                                                                                                                               GetMinimumSize<T7>() + GetMinimumSize<T8>() + GetMinimumSize<T9>() +
                                                                                                                               GetMinimumSize<T10>() : capacity)
    { }
    public byte[] Get(ref MessageOverhead overhead, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10)
    {
        lock (this)
        {
            Flush();
            Write(writer1, arg1);
            Write(writer2, arg2);
            Write(writer3, arg3);
            Write(writer4, arg4);
            Write(writer5, arg5);
            Write(writer6, arg6);
            Write(writer7, arg7);
            Write(writer8, arg8);
            Write(writer9, arg9);
            Write(writer10, arg10);
            PrependData(ref overhead);
            return ToArray();
        }
    }
    public byte[] Get(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10)
    {
        lock (this)
        {
            Flush();
            Write(writer1, arg1);
            Write(writer2, arg2);
            Write(writer3, arg3);
            Write(writer4, arg4);
            Write(writer5, arg5);
            Write(writer6, arg6);
            Write(writer7, arg7);
            Write(writer8, arg8);
            Write(writer9, arg9);
            Write(writer10, arg10);
            return ToArray();
        }
    }
}
