using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

// ReSharper disable AssignNullToNotNullAttribute

namespace DevkitServer.Util.Encoding;

public class ByteReader
{
    private const int MinStreamBufferSize = 32;
    private const int GuidSize = 16;
    private static readonly bool IsBigEndian = !BitConverter.IsLittleEndian;
    private static readonly Type[] Parameters = { typeof(ByteReader) };
    public delegate T Reader<out T>(ByteReader reader);
    private byte[]? _buffer;
    private Stream? _stream;
    private int _index;
    private bool failure;
    private bool _streamMode;
    private bool _streamLengthSupport;
    private int _position;
    private int _length;
    public byte[]? InternalBuffer { get => _buffer; set => _buffer = value; }
    public Stream? Stream
    {
        get => _stream;
        private set
        {
            if (value is not null && !value.CanRead)
                throw new ArgumentException("Stream must be able to read.");
            _stream = value;
        }
    }
    public bool HasFailed => failure;
    public int Position => _streamMode ? _position : _index;
    public int BytesLeft => _streamMode ? (_streamLengthSupport ? (int)Math.Min(_stream!.Length - _stream!.Position, int.MaxValue) : (_buffer is not null ? _buffer.Length - _index : 0)) : _buffer!.Length - _index;
    public bool ThrowOnError { get; set; }
    public bool LogOnError { get; set; } = true;
    public int StreamBufferSize { get; set; } = 128;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetStreamBufferLength() => !_streamLengthSupport
        ? StreamBufferSize
        : (int)Math.Min(StreamBufferSize, Math.Max(_stream!.Length - _stream.Position, MinStreamBufferSize));
    
    public void LoadNew(Stream stream)
    {
        failure = false;
        Stream = stream ?? throw new ArgumentNullException(nameof(stream));
        if (!_stream!.CanSeek)
        {
            _streamLengthSupport = false;
            if (_buffer is null || _buffer.Length < StreamBufferSize)
                _buffer = new byte[StreamBufferSize];
            else
                Unsafe.InitBlock(ref _buffer[0], 0, (uint)_buffer.Length);
        }
        else
        {
            _ = stream.Length;
            _ = stream.Position;
            _streamLengthSupport = true;
            int min = GetStreamBufferLength();
            if (_buffer is not { Length: > 0 })
            {
                _buffer = new byte[min];
            }
            else if (_buffer.Length > min)
            {
                Unsafe.InitBlock(ref _buffer[0], 0, (uint)_buffer.Length);
            }
            else
            {
                _buffer = new byte[min];
            }
        }

        _length = 0;
        _position = 0;
        _index = 0;
        _streamMode = true;
    }
    public void LoadNew(byte[] bytes)
    {
        this._buffer = bytes ?? throw new ArgumentNullException(nameof(bytes));
        _length = _buffer.Length;
        _streamMode = false;
        _index = 0;
        _position = 0;
        this.failure = false;
    }
    private unsafe void Reverse(byte* litEndStrt, int size)
    {
        byte* stack = stackalloc byte[size];
        Buffer.MemoryCopy(litEndStrt, stack, size, size);
        for (int i = 0; i < size; i++)
            litEndStrt[i] = stack[size - i - 1];
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private unsafe void EndianCheck(byte* litEndStrt, int size)
    {
        if (IsBigEndian && size > 1)
            Reverse(litEndStrt, size);
    }
    private unsafe T Read<T>() where T : unmanaged
    {
        int size = sizeof(T);
        T rtn;
        fixed (byte* ptr = &_buffer![_index])
        {
            EndianCheck(ptr, size);
            rtn = *(T*)ptr;
        }
        _index += size;
        return rtn;
    }
    private unsafe T Read<T>(byte* ptr) where T : unmanaged
    {
        int size = sizeof(T);
        ptr += _index;
        EndianCheck(ptr, size);
        T rtn = *(T*)ptr;
        _index += size;
        return rtn;
    }

    public unsafe bool EnsureMoreLength(int byteCt)
    {
        if (_streamMode)
        {
            if (_buffer is not null && _buffer.Length > _index + byteCt)
            {
                if (_length == 0)
                {
                    _length = _stream!.Read(_buffer, 0, _buffer.Length - _length);
                    _position += _length;
                }
                return _length >= _index + byteCt - 1;
            }
            if (_stream is null)
                goto fail;
            int l = GetStreamBufferLength();
            if (byteCt > l) l = byteCt;
            int rl;
            // buffer not initialized
            if (_buffer is not { Length: > 0 })
            {
                _buffer = new byte[l];
                if (!_stream.CanRead)
                    goto fail;
                rl = _stream.Read(_buffer, 0, l);
                _index = 0;
                _position += rl;
                _length = rl;
                if (rl < byteCt)
                    goto fail;
            }
            else // partially or fully processed buffer
            {
                int remaining = _buffer.Length - _index;
                if (byteCt < _buffer.Length) // space for remaining and needed bytes in a new buffer
                {
                    fixed (byte* ptr = _buffer)
                        Buffer.MemoryCopy(ptr + _index, ptr, _buffer.Length, remaining);
                }
                else // not enough space for needed bytes
                {
                    byte* st = stackalloc byte[remaining];
                    fixed (byte* ptr = _buffer)
                        Buffer.MemoryCopy(ptr + _index, st, remaining, remaining);
                    _buffer = new byte[byteCt];
                    fixed (byte* ptr = _buffer)
                        Buffer.MemoryCopy(st, ptr, remaining, remaining);
                }
                _index = 0;
                if (!_stream.CanRead)
                    goto fail;
                rl = _stream.Read(_buffer, remaining, _buffer.Length - remaining);
                _length = remaining + rl;
                _position += rl;
                if (rl < remaining - byteCt)
                    goto fail;
            }
            return true;
        }

        if (_buffer is not null && _index + byteCt <= _length) return true;

        fail:
        failure = true;
        string ex = "Failed to read " + byteCt.ToString(CultureInfo.InvariantCulture) +
                    " B at offset " + _index.ToString(CultureInfo.InvariantCulture) + " / " + _length.ToString(CultureInfo.InvariantCulture) + ".";
        if (ThrowOnError)
            throw new ByteBufferOverflowException(ex);
        if (LogOnError)
            Logger.LogWarning(ex);
        return false;
    }
    public byte[] ReadBlock(int length)
    {
        if (!EnsureMoreLength(length)) return null!;
        byte[] rtn = new byte[length];
        Buffer.BlockCopy(_buffer, _index, rtn, 0, length);
        _index += length;
        return rtn;
    }
    public unsafe T ReadStruct<T>() where T : unmanaged
    {
        return !EnsureMoreLength(sizeof(T)) ? default(T) : Read<T>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] ReadUInt8Array() => ReadBytes();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[]? ReadNullableUInt8Array() => ReadNullableBytes();

    private static readonly MethodInfo ReadByteArrayMethod = typeof(ByteReader).GetMethod(nameof(ReadBytes), BindingFlags.Instance | BindingFlags.Public);
    public byte[] ReadBytes()
    {
        ushort length = ReadUInt16();
        if (length == 0) return Array.Empty<byte>();
        if (!EnsureMoreLength(length))
            return null!;
        byte[] rtn = new byte[length];
        Buffer.BlockCopy(_buffer, _index, rtn, 0, length);
        _index += length;
        return rtn;
    }
    public void Skip(int bytes)
    {
        EnsureMoreLength(bytes);
        _index += bytes;
    }
    public void Goto(int toPosition)
    {
        if (_streamMode)
            throw new NotSupportedException("Not supported when using stream mode.");
        if (Position < toPosition)
            Skip(Position - toPosition);
        else
        {
            _index = toPosition;
        }
    }

    private static readonly MethodInfo ReadNullableByteArrayMethod = typeof(ByteReader).GetMethod(nameof(ReadNullableBytes), BindingFlags.Instance | BindingFlags.Public);
    public byte[]? ReadNullableBytes()
    {
        if (!ReadBool()) return null;
        return ReadBytes();
    }
    public byte[] ReadLongBytes()
    {
        int length = ReadInt32();
        if (length == 0) return Array.Empty<byte>();
        if (!EnsureMoreLength(length))
            return null!;
        byte[] rtn = new byte[length];
        Buffer.BlockCopy(_buffer, _index, rtn, 0, length);
        _index += length;
        return rtn;
    }
    public byte[]? ReadNullableLongBytes()
    {
        if (!ReadBool()) return null;
        return ReadLongBytes();
    }

    private static readonly MethodInfo ReadInt32Method = typeof(ByteReader).GetMethod(nameof(ReadInt32), BindingFlags.Instance | BindingFlags.Public);
    public int ReadInt32() => !EnsureMoreLength(sizeof(int)) ? default : Read<int>();


    private static readonly MethodInfo ReadNullableInt32Method = typeof(ByteReader).GetMethod(nameof(ReadNullableInt32), BindingFlags.Instance | BindingFlags.Public);
    public int? ReadNullableInt32()
    {
        if (!ReadBool()) return null;
        return ReadInt32();
    }


    private static readonly MethodInfo ReadUInt32Method = typeof(ByteReader).GetMethod(nameof(ReadUInt32), BindingFlags.Instance | BindingFlags.Public);
    public uint ReadUInt32() => !EnsureMoreLength(sizeof(uint)) ? default : Read<uint>();


    private static readonly MethodInfo ReadNullableUInt32Method = typeof(ByteReader).GetMethod(nameof(ReadNullableUInt32), BindingFlags.Instance | BindingFlags.Public);
    public uint? ReadNullableUInt32()
    {
        if (!ReadBool()) return null;
        return ReadUInt32();
    }


    private static readonly MethodInfo ReadUInt8Method = typeof(ByteReader).GetMethod(nameof(ReadUInt8), BindingFlags.Instance | BindingFlags.Public);
    public byte ReadUInt8()
    {
        if (!EnsureMoreLength(1))
            return default;
        byte rtn = _buffer![_index];
        ++_index;
        return rtn;
    }


    private static readonly MethodInfo ReadNullableUInt8Method = typeof(ByteReader).GetMethod(nameof(ReadNullableUInt8), BindingFlags.Instance | BindingFlags.Public);
    public byte? ReadNullableUInt8()
    {
        if (!ReadBool()) return null;
        return ReadUInt8();
    }


    private static readonly MethodInfo ReadInt8Method = typeof(ByteReader).GetMethod(nameof(ReadInt8), BindingFlags.Instance | BindingFlags.Public);
    public sbyte ReadInt8()
    {
        if (!EnsureMoreLength(1))
            return default;
        sbyte rtn = unchecked((sbyte)_buffer![_index]);
        ++_index;
        return rtn;
    }


    private static readonly MethodInfo ReadNullableInt8Method = typeof(ByteReader).GetMethod(nameof(ReadNullableInt8), BindingFlags.Instance | BindingFlags.Public);
    public sbyte? ReadNullableInt8()
    {
        if (!ReadBool()) return null;
        return ReadInt8();
    }


    private static readonly MethodInfo ReadBoolMethod = typeof(ByteReader).GetMethod(nameof(ReadBool), BindingFlags.Instance | BindingFlags.Public);
    public bool ReadBool()
    {
        if (!EnsureMoreLength(1))
            return default;
        bool rtn = _buffer![_index] > 0;
        ++_index;
        return rtn;
    }


    private static readonly MethodInfo ReadNullableBoolMethod = typeof(ByteReader).GetMethod(nameof(ReadNullableBool), BindingFlags.Instance | BindingFlags.Public);
    public bool? ReadNullableBool()
    {
        if (!ReadBool()) return null;
        return ReadBool();
    }


    private static readonly MethodInfo ReadInt64Method = typeof(ByteReader).GetMethod(nameof(ReadInt64), BindingFlags.Instance | BindingFlags.Public);
    public long ReadInt64() => !EnsureMoreLength(sizeof(long)) ? default : Read<long>();


    private static readonly MethodInfo ReadNullableInt64Method = typeof(ByteReader).GetMethod(nameof(ReadNullableInt64), BindingFlags.Instance | BindingFlags.Public);
    public long? ReadNullableInt64()
    {
        if (!ReadBool()) return null;
        return ReadInt64();
    }


    private static readonly MethodInfo ReadUInt64Method = typeof(ByteReader).GetMethod(nameof(ReadUInt64), BindingFlags.Instance | BindingFlags.Public);
    public ulong ReadUInt64() => !EnsureMoreLength(sizeof(ulong)) ? default : Read<ulong>();


    private static readonly MethodInfo ReadNullableUInt64Method = typeof(ByteReader).GetMethod(nameof(ReadNullableUInt64), BindingFlags.Instance | BindingFlags.Public);
    public ulong? ReadNullableUInt64()
    {
        if (!ReadBool()) return null;
        return ReadUInt64();
    }


    private static readonly MethodInfo ReadInt16Method = typeof(ByteReader).GetMethod(nameof(ReadInt16), BindingFlags.Instance | BindingFlags.Public);
    public short ReadInt16() => !EnsureMoreLength(sizeof(short)) ? default : Read<short>();
    
    public int ReadInt24()
    {
        if (!EnsureMoreLength(3))
            return default;
        ushort sh = Read<ushort>();
        byte bt = _buffer![_index];
        ++_index;
        return (sh | (bt << 16)) - DevkitServerUtility.Int24Bounds;
    }

    public uint ReadUInt24() => unchecked((uint)ReadInt24());

    public int? ReadNullableInt24()
    {
        if (!ReadBool()) return null;
        return ReadInt24();
    }

    private static readonly MethodInfo ReadNullableInt16Method = typeof(ByteReader).GetMethod(nameof(ReadNullableInt16), BindingFlags.Instance | BindingFlags.Public);
    public short? ReadNullableInt16()
    {
        if (!ReadBool()) return null;
        return ReadInt16();
    }


    private static readonly MethodInfo ReadUInt16Method = typeof(ByteReader).GetMethod(nameof(ReadUInt16), BindingFlags.Instance | BindingFlags.Public);
    public ushort ReadUInt16() => !EnsureMoreLength(sizeof(ushort)) ? default : Read<ushort>();


    private static readonly MethodInfo ReadNullableUInt16Method = typeof(ByteReader).GetMethod(nameof(ReadNullableUInt16), BindingFlags.Instance | BindingFlags.Public);
    public ushort? ReadNullableUInt16()
    {
        if (!ReadBool()) return null;
        return ReadUInt16();
    }


    private static readonly MethodInfo ReadFloatMethod = typeof(ByteReader).GetMethod(nameof(ReadFloat), BindingFlags.Instance | BindingFlags.Public);
    public float ReadFloat() => !EnsureMoreLength(sizeof(float)) ? default : Read<float>();


    private static readonly MethodInfo ReadNullableFloatMethod = typeof(ByteReader).GetMethod(nameof(ReadNullableFloat), BindingFlags.Instance | BindingFlags.Public);
    public float? ReadNullableFloat()
    {
        if (!ReadBool()) return null;
        return ReadFloat();
    }


    private static readonly MethodInfo ReadDecimalMethod = typeof(ByteReader).GetMethod(nameof(ReadDecimal), BindingFlags.Instance | BindingFlags.Public);
    public decimal ReadDecimal() => !EnsureMoreLength(sizeof(decimal)) ? default : Read<decimal>();


    private static readonly MethodInfo ReadNullableDecimalMethod = typeof(ByteReader).GetMethod(nameof(ReadNullableDecimal), BindingFlags.Instance | BindingFlags.Public);
    public decimal? ReadNullableDecimal()
    {
        if (!ReadBool()) return null;
        return ReadDecimal();
    }


    private static readonly MethodInfo ReadDoubleMethod = typeof(ByteReader).GetMethod(nameof(ReadDouble), BindingFlags.Instance | BindingFlags.Public);
    public double ReadDouble() => !EnsureMoreLength(sizeof(double)) ? default : Read<double>();


    private static readonly MethodInfo ReadNullableDoubleMethod = typeof(ByteReader).GetMethod(nameof(ReadNullableDouble), BindingFlags.Instance | BindingFlags.Public);
    public double? ReadNullableDouble()
    {
        if (!ReadBool()) return null;
        return ReadDouble();
    }


    private static readonly MethodInfo ReadCharMethod = typeof(ByteReader).GetMethod(nameof(ReadChar), BindingFlags.Instance | BindingFlags.Public);
    public char ReadChar() => !EnsureMoreLength(sizeof(char)) ? default : Read<char>();


    private static readonly MethodInfo ReadNullableCharMethod = typeof(ByteReader).GetMethod(nameof(ReadNullableChar), BindingFlags.Instance | BindingFlags.Public);
    public char? ReadNullableChar()
    {
        if (!ReadBool()) return null;
        return ReadChar();
    }


    private static readonly MethodInfo ReadStringMethod = typeof(ByteReader).GetMethod(nameof(ReadString), BindingFlags.Instance | BindingFlags.Public);
    public string ReadString()
    {
        ushort length = ReadUInt16();
        if (length == 0) return string.Empty;
        if (!EnsureMoreLength(length))
            return null!;
        string str = System.Text.Encoding.UTF8.GetString(_buffer, _index, length);
        _index += length;
        return str;
    }

    private static readonly MethodInfo ReadTypeMethod = typeof(ByteReader).GetMethod(nameof(ReadType), BindingFlags.Instance | BindingFlags.Public);
    public string? ReadTypeInfo() => ReadTypeInfo(out _);
    public string? ReadTypeInfo(out byte flag)
    {
        const string nsSdgUnturned = "SDG.Unturned";
        const string nsSdgFrameworkDevkit = "SDG.Framework.Devkit";
        const string nsSdgFramework = "SDG.Framework";
        const string nsDevkitServer = "DevkitServer";
        const string nsSystem = "System";

        flag = ReadUInt8();
        if ((flag & 128) != 0)
            return null;

        string ns = ReadString();
        if ((flag & 1) != 0)
        {
            // AssemblyCSharp
            if ((flag & 8) != 0)
                ns = ns.Length == 0 ? nsSdgUnturned : nsSdgUnturned + "." + ns;
            else if ((flag & 16) != 0)
                ns = ns.Length == 0 ? nsSdgFrameworkDevkit : nsSdgFrameworkDevkit + "." + ns;
            else if ((flag & 32) != 0)
                ns = ns.Length == 0 ? nsSdgFramework : nsSdgFramework + "." + ns;
            ns = "[Assembly-CSharp.dll] " + ns;
        }
        else if ((flag & 2) != 0)
        {
            // DevkitServer
            if ((flag & 4) != 0)
                ns = ns.Length == 0 ? nsDevkitServer : nsDevkitServer + "." + ns;
            ns = "[DevkitServer.dll] " + ns;
        }
        if ((flag & 64) != 0)
        {
            // mscorlib
            ns = "[mscorlib.dll] " + (ns.Length == 0 ? nsSystem : nsSystem + "." + ns);
        }
        return ns;
    }
    public Type? ReadType()
    {
        const string nsSdgUnturned = "SDG.Unturned";
        const string nsSdgFrameworkDevkit = "SDG.Framework.Devkit";
        const string nsSdgFramework = "SDG.Framework";
        const string nsDevkitServer = "DevkitServer";
        const string nsSystem = "System";

        byte flag = ReadUInt8();
        if ((flag & 128) != 0)
            return null;

        string ns = ReadString();
        if ((flag & 1) != 0)
        {
            // AssemblyCSharp
            if ((flag & 8) != 0)
                ns = ns.Length == 0 ? nsSdgUnturned : nsSdgUnturned + "." + ns;
            else if ((flag & 16) != 0)
                ns = ns.Length == 0 ? nsSdgFrameworkDevkit : nsSdgFrameworkDevkit + "." + ns;
            else if ((flag & 32) != 0)
                ns = ns.Length == 0 ? nsSdgFramework : nsSdgFramework + "." + ns;
            return Accessor.AssemblyCSharp.GetType(ns);
        }
        if ((flag & 2) != 0)
        {
            // DevkitServer
            if ((flag & 4) != 0)
                ns = ns.Length == 0 ? nsDevkitServer : nsDevkitServer + "." + ns;
            return Accessor.DevkitServer.GetType(ns);
        }
        if ((flag & 64) != 0)
        {
            // mscorlib
            return Accessor.MSCoreLib.GetType(ns.Length == 0 ? nsSystem : nsSystem + "." + ns);
        }

        return Type.GetType(ns);
    }
    public string ReadShortString()
    {
        byte length = ReadUInt8();
        if (length == 0) return string.Empty;
        if (!EnsureMoreLength(length))
            return null!;
        string str = System.Text.Encoding.UTF8.GetString(_buffer, _index, length);
        _index += length;
        return str;
    }
    private static readonly MethodInfo ReadNullableStringMethod = typeof(ByteReader).GetMethod(nameof(ReadNullableString), BindingFlags.Instance | BindingFlags.Public);
    public string? ReadNullableString()
    {
        return !ReadBool() ? null : ReadString();
    }
    public string? ReadNullableShortString()
    {
        return !ReadBool() ? null : ReadShortString();
    }
    private static readonly MethodInfo ReadDateTimeMethod = typeof(ByteReader).GetMethod(nameof(ReadDateTime), BindingFlags.Instance | BindingFlags.Public);
    public DateTime ReadDateTime() => !EnsureMoreLength(sizeof(long)) ? default : DateTime.FromBinary(Read<long>());


    private static readonly MethodInfo ReadNullableDateTimeMethod = typeof(ByteReader).GetMethod(nameof(ReadNullableDateTime), BindingFlags.Instance | BindingFlags.Public);
    public DateTime? ReadNullableDateTime()
    {
        if (!ReadBool()) return null;
        return ReadDateTime();
    }


    private static readonly MethodInfo ReadDateTimeOffsetMethod = typeof(ByteReader).GetMethod(nameof(ReadDateTimeOffset), BindingFlags.Instance | BindingFlags.Public);
    public unsafe DateTimeOffset ReadDateTimeOffset()
    {
        if (!EnsureMoreLength(sizeof(long) + sizeof(short)))
            return default;
        fixed (byte* ptr = _buffer)
        {
            long v = Read<long>(ptr);
            long offset = Read<short>(ptr) * 600000000L;
            return new DateTimeOffset(DateTime.FromBinary(v), *(TimeSpan*)&offset);
        }
    }


    private static readonly MethodInfo ReadNullableDateTimeOffsetMethod = typeof(ByteReader).GetMethod(nameof(ReadNullableDateTimeOffset), BindingFlags.Instance | BindingFlags.Public);
    public DateTimeOffset? ReadNullableDateTimeOffset()
    {
        if (!ReadBool()) return null;
        return ReadDateTimeOffset();
    }


    private static readonly MethodInfo ReadTimeSpanMethod = typeof(ByteReader).GetMethod(nameof(ReadTimeSpan), BindingFlags.Instance | BindingFlags.Public);
    public unsafe TimeSpan ReadTimeSpan()
    {
        if (!EnsureMoreLength(sizeof(long)))
            return default;
        long ticks = Read<long>();
        return *(TimeSpan*)&ticks;
    }


    private static readonly MethodInfo ReadNullableTimeSpanMethod = typeof(ByteReader).GetMethod(nameof(ReadNullableTimeSpan), BindingFlags.Instance | BindingFlags.Public);
    public TimeSpan? ReadNullableTimeSpan()
    {
        if (!ReadBool()) return null;
        return ReadTimeSpan();
    }


    private static readonly MethodInfo ReadGuidMethod = typeof(ByteReader).GetMethod(nameof(ReadGuid), BindingFlags.Instance | BindingFlags.Public);
    public Guid ReadGuid() => !EnsureMoreLength(GuidSize) ? default : Read<Guid>();


    private static readonly MethodInfo ReadNullableGuidMethod = typeof(ByteReader).GetMethod(nameof(ReadNullableGuid), BindingFlags.Instance | BindingFlags.Public);
    public Guid? ReadNullableGuid() => !ReadBool() ? null : ReadGuid();

    
    private static readonly Vector2 V2NaN = new Vector2(float.NaN, float.NaN);
    private static readonly Vector3 V3NaN = new Vector3(float.NaN, float.NaN, float.NaN);
    private static readonly Vector4 V4NaN = new Vector4(float.NaN, float.NaN, float.NaN, float.NaN);
    private static readonly Bounds BoundsNaN = new Bounds(V3NaN, V3NaN);
    private static readonly Color32 C32NaN = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);

    private static readonly MethodInfo ReadVector2Method = typeof(ByteReader).GetMethod(nameof(ReadVector2), BindingFlags.Instance | BindingFlags.Public);
    public Vector2 ReadVector2() => !EnsureMoreLength(sizeof(float) * 2) ? V2NaN : new Vector2(Read<float>(), Read<float>());


    private static readonly MethodInfo ReadNullableVector2Method = typeof(ByteReader).GetMethod(nameof(ReadNullableVector2), BindingFlags.Instance | BindingFlags.Public);
    public Vector2? ReadNullableVector2()
    {
        if (!ReadBool()) return null;
        return ReadVector2();
    }


    private static readonly MethodInfo ReadVector3Method = typeof(ByteReader).GetMethod(nameof(ReadVector3), BindingFlags.Instance | BindingFlags.Public);
    public Vector3 ReadVector3() => !EnsureMoreLength(sizeof(float) * 3) ? V3NaN : new Vector3(Read<float>(), Read<float>(), Read<float>());


    private static readonly MethodInfo ReadNullableVector3Method = typeof(ByteReader).GetMethod(nameof(ReadNullableVector3), BindingFlags.Instance | BindingFlags.Public);
    public Vector3? ReadNullableVector3()
    {
        if (!ReadBool()) return null;
        return ReadVector3();
    }


    private static readonly MethodInfo ReadVector4Method = typeof(ByteReader).GetMethod(nameof(ReadVector4), BindingFlags.Instance | BindingFlags.Public);
    public Vector4 ReadVector4() => !EnsureMoreLength(sizeof(float) * 4) ? V4NaN : new Vector4(Read<float>(), Read<float>(), Read<float>(), Read<float>());


    private static readonly MethodInfo ReadNullableVector4Method = typeof(ByteReader).GetMethod(nameof(ReadNullableVector4), BindingFlags.Instance | BindingFlags.Public);
    public Vector4? ReadNullableVector4()
    {
        if (!ReadBool()) return null;
        return ReadVector4();
    }


    private static readonly MethodInfo ReadBoundsMethod = typeof(ByteReader).GetMethod(nameof(ReadVector4), BindingFlags.Instance | BindingFlags.Public);
    public Bounds ReadBounds() =>
        !EnsureMoreLength(sizeof(float) * 6)
            ? BoundsNaN
            : new Bounds(new Vector3(Read<float>(), Read<float>(), Read<float>()), new Vector3(Read<float>(), Read<float>(), Read<float>()));


    private static readonly MethodInfo ReadNullableBoundsMethod = typeof(ByteReader).GetMethod(nameof(ReadNullableBounds), BindingFlags.Instance | BindingFlags.Public);
    public Bounds? ReadNullableBounds()
    {
        if (!ReadBool()) return null;
        return ReadBounds();
    }


    private static readonly MethodInfo ReadQuaternionMethod = typeof(ByteReader).GetMethod(nameof(ReadQuaternion), BindingFlags.Instance | BindingFlags.Public);

    public Quaternion ReadQuaternion()
    {
        Vector4 f = ReadVector4();
        return new Quaternion(f.x, f.y, f.z, f.w);
    }


    private static readonly MethodInfo ReadNullableQuaternionMethod = typeof(ByteReader).GetMethod(nameof(ReadNullableQuaternion), BindingFlags.Instance | BindingFlags.Public);
    public Quaternion? ReadNullableQuaternion()
    {
        if (!ReadBool()) return null;
        return ReadQuaternion();
    }


    private static readonly MethodInfo ReadColorMethod = typeof(ByteReader).GetMethod(nameof(ReadColor), BindingFlags.Instance | BindingFlags.Public);
    public Color ReadColor()
    {
        Vector4 f = ReadVector4();
        return new Color(f.y, f.z, f.w, f.x);
    }


    private static readonly MethodInfo ReadNullableColorMethod = typeof(ByteReader).GetMethod(nameof(ReadNullableColor), BindingFlags.Instance | BindingFlags.Public);
    public Color? ReadNullableColor()
    {
        if (!ReadBool()) return null;
        return ReadColor();
    }


    private static readonly MethodInfo ReadColor32Method = typeof(ByteReader).GetMethod(nameof(ReadColor32), BindingFlags.Instance | BindingFlags.Public);
    public Color32 ReadColor32()
    {
        if (!EnsureMoreLength(4))
            return C32NaN;

        Color32 q = new Color32(_buffer![_index], _buffer[_index + 1], _buffer[_index + 2], _buffer[_index + 3]);

        _index += 4;
        return q;
    }


    private static readonly MethodInfo ReadNullableColor32Method = typeof(ByteReader).GetMethod(nameof(ReadNullableColor32), BindingFlags.Instance | BindingFlags.Public);
    public Color32? ReadNullableColor32()
    {
        return !ReadBool() ? null : ReadColor32();
    }

    
    private static readonly MethodInfo ReadEnumMethod = typeof(ByteReader).GetMethod(nameof(ReadEnum), BindingFlags.Instance | BindingFlags.Public);
    public unsafe TEnum ReadEnum<TEnum>() where TEnum : unmanaged, Enum => !EnsureMoreLength(sizeof(TEnum)) ? default : Read<TEnum>();


    private static readonly MethodInfo ReadNullableEnumMethod = typeof(ByteReader).GetMethod(nameof(ReadNullableEnum), BindingFlags.Instance | BindingFlags.Public);
    public TEnum? ReadNullableEnum<TEnum>() where TEnum : unmanaged, Enum => !ReadBool() ? null : ReadEnum<TEnum>();


    private static readonly MethodInfo ReadInt32ArrayMethod = typeof(ByteReader).GetMethod(nameof(ReadInt32Array), BindingFlags.Instance | BindingFlags.Public);
    public int[] ReadInt32Array()
    {
        int len = ReadUInt16();
        int size = len * sizeof(int);
        if (len == 0) return Array.Empty<int>();
        if (!EnsureMoreLength(size))
            return null!;
        int[] rtn = new int[len];
        for (int i = 0; i < len; i++)
            rtn[i] = BitConverter.ToInt32(_buffer, _index + i * sizeof(int));
        _index += size;
        return rtn;
    }


    private static readonly MethodInfo ReadNullableInt32ArrayMethod = typeof(ByteReader).GetMethod(nameof(ReadNullableInt32Array), BindingFlags.Instance | BindingFlags.Public);
    public int[]? ReadNullableInt32Array() => !ReadBool() ? null : ReadInt32Array();


    private static readonly MethodInfo ReadGuidArrayMethod = typeof(ByteReader).GetMethod(nameof(ReadGuidArray), BindingFlags.Instance | BindingFlags.Public);
    public unsafe Guid[] ReadGuidArray()
    {
        ushort len = ReadUInt16();
        int size = len * GuidSize;
        if (len == 0) return Array.Empty<Guid>();
        if (!EnsureMoreLength(size))
            return null!;
        Guid[] rtn = new Guid[len];
        fixed (byte* ptr = &_buffer![_index])
        {
            for (int i = 0; i < len; i++)
            {
                byte* b = ptr + i * GuidSize;
                EndianCheck(b, GuidSize);
                rtn[i] = *(Guid*)b;
            }
        }
        _index += size;
        return rtn;
    }


    private static readonly MethodInfo ReadNullableGuidArrayMethod = typeof(ByteReader).GetMethod(nameof(ReadNullableGuidArray), BindingFlags.Instance | BindingFlags.Public);
    public Guid[]? ReadNullableGuidArray() => !ReadBool() ? null : ReadGuidArray();


    private static readonly MethodInfo ReadDateTimeArrayMethod = typeof(ByteReader).GetMethod(nameof(ReadDateTimeArray), BindingFlags.Instance | BindingFlags.Public);
    public unsafe DateTime[] ReadDateTimeArray()
    {
        ushort len = ReadUInt16();
        int size = len * sizeof(long);
        if (len == 0) return Array.Empty<DateTime>();
        if (!EnsureMoreLength(size))
            return null!;
        DateTime[] rtn = new DateTime[len];
        fixed (byte* ptr = _buffer)
        {
            for (int i = 0; i < len; i++)
            {
                rtn[i] = DateTime.FromBinary(Read<long>(ptr));
            }
        }
        return rtn;
    }


    private static readonly MethodInfo ReadNullableDateTimeArrayMethod = typeof(ByteReader).GetMethod(nameof(ReadNullableDateTimeArray), BindingFlags.Instance | BindingFlags.Public);
    public DateTime[]? ReadNullableDateTimeArray() => !ReadBool() ? null : ReadDateTimeArray();


    private static readonly MethodInfo ReadDateTimeOffsetArrayMethod = typeof(ByteReader).GetMethod(nameof(ReadDateTimeOffsetArray), BindingFlags.Instance | BindingFlags.Public);
    public unsafe DateTimeOffset[] ReadDateTimeOffsetArray()
    {
        ushort len = ReadUInt16();
        int size = len * (sizeof(long) + sizeof(short));
        if (len == 0) return Array.Empty<DateTimeOffset>();
        if (!EnsureMoreLength(size))
            return null!;
        DateTimeOffset[] rtn = new DateTimeOffset[len];
        fixed (byte* ptr = _buffer)
        {
            for (int i = 0; i < len; i++)
            {
                DateTime dt = DateTime.FromBinary(Read<long>(ptr));
                long offset = Read<short>(ptr) * 600000000L;
                rtn[i] = new DateTimeOffset(dt, *(TimeSpan*)&offset);
            }
        }
        return rtn;
    }


    private static readonly MethodInfo ReadNullableDateTimeOffsetArrayMethod = typeof(ByteReader).GetMethod(nameof(ReadNullableDateTimeOffsetArray), BindingFlags.Instance | BindingFlags.Public);
    public DateTimeOffset[]? ReadNullableDateTimeOffsetArray() => !ReadBool() ? null : ReadDateTimeOffsetArray();

    private static readonly MethodInfo ReadUInt32ArrayMethod = typeof(ByteReader).GetMethod(nameof(ReadUInt32Array), BindingFlags.Instance | BindingFlags.Public);
    public uint[] ReadUInt32Array()
    {
        ushort len = ReadUInt16();
        int size = len * sizeof(uint);
        if (len == 0) return Array.Empty<uint>();
        if (!EnsureMoreLength(size))
            return null!;
        uint[] rtn = new uint[len];
        for (int i = 0; i < len; i++)
            rtn[i] = BitConverter.ToUInt32(_buffer, _index + i * sizeof(uint));
        _index += size;
        return rtn;
    }


    private static readonly MethodInfo ReadNullableUInt32ArrayMethod = typeof(ByteReader).GetMethod(nameof(ReadNullableUInt32Array), BindingFlags.Instance | BindingFlags.Public);
    public uint[]? ReadNullableUInt32Array() => !ReadBool() ? null : ReadUInt32Array();


    private static readonly MethodInfo ReadInt8ArrayMethod = typeof(ByteReader).GetMethod(nameof(ReadInt8Array), BindingFlags.Instance | BindingFlags.Public);
    public sbyte[] ReadInt8Array()
    {
        ushort len = ReadUInt16();
        if (len == 0) return Array.Empty<sbyte>();
        if (!EnsureMoreLength(len))
            return null!;
        sbyte[] rtn = new sbyte[len];
        for (int i = 0; i < len; i++)
            rtn[i] = unchecked((sbyte)_buffer![_index + i]);
        _index += len;
        return rtn;
    }


    private static readonly MethodInfo ReadNullableInt8ArrayMethod = typeof(ByteReader).GetMethod(nameof(ReadNullableInt8Array), BindingFlags.Instance | BindingFlags.Public);
    public sbyte[]? ReadNullableInt8Array() => !ReadBool() ? null : ReadInt8Array();


    private static readonly MethodInfo ReadBoolArrayMethod = typeof(ByteReader).GetMethod(nameof(ReadBoolArray), BindingFlags.Instance | BindingFlags.Public);
    public unsafe bool[] ReadBoolArray()
    {
        ushort len = ReadUInt16();
        if (len < 1) return Array.Empty<bool>();
        int blen = (int)Math.Ceiling(len / 8d);
        if (!EnsureMoreLength(blen))
            return null!;
        bool[] rtn = new bool[len];
        fixed (byte* ptr = _buffer)
        {
            byte* ptr2 = ptr + _index;
            byte current = *ptr2;
            for (int i = 0; i < len; i++)
            {
                byte mod = (byte)(i % 8);
                if (mod == 0 & i != 0)
                {
                    ptr2++;
                    current = *ptr2;
                }
                rtn[i] = (1 & (current >> mod)) == 1;
            }
        }

        _index += blen;
        return rtn;
    }


    private static readonly MethodInfo ReadNullableBoolArrayMethod = typeof(ByteReader).GetMethod(nameof(ReadNullableBoolArray), BindingFlags.Instance | BindingFlags.Public);
    public bool[]? ReadNullableBoolArray() => !ReadBool() ? null : ReadBoolArray();


    private static readonly MethodInfo ReadInt64ArrayMethod = typeof(ByteReader).GetMethod(nameof(ReadInt64Array), BindingFlags.Instance | BindingFlags.Public);
    public long[] ReadInt64Array()
    {
        ushort len = ReadUInt16();
        int size = len * sizeof(long);
        if (len == 0) return Array.Empty<long>();
        if (!EnsureMoreLength(size))
            return null!;
        long[] rtn = new long[len];
        for (int i = 0; i < len; i++)
            rtn[i] = BitConverter.ToInt64(_buffer, _index + i * sizeof(long));
        _index += size;
        return rtn;
    }


    private static readonly MethodInfo ReadNullableInt64ArrayMethod = typeof(ByteReader).GetMethod(nameof(ReadNullableInt64Array), BindingFlags.Instance | BindingFlags.Public);
    public long[]? ReadNullableInt64Array() => !ReadBool() ? null : ReadInt64Array();


    private static readonly MethodInfo ReadUInt64ArrayMethod = typeof(ByteReader).GetMethod(nameof(ReadUInt64Array), BindingFlags.Instance | BindingFlags.Public);
    public ulong[] ReadUInt64Array()
    {
        ushort len = ReadUInt16();
        int size = len * sizeof(ulong);
        if (len == 0) return Array.Empty<ulong>();
        if (!EnsureMoreLength(size))
            return null!;
        ulong[] rtn = new ulong[len];
        for (int i = 0; i < len; i++)
            rtn[i] = BitConverter.ToUInt64(_buffer, _index + i * sizeof(ulong));
        _index += size;
        return rtn;
    }


    private static readonly MethodInfo ReadNullableUInt64ArrayMethod = typeof(ByteReader).GetMethod(nameof(ReadNullableUInt64Array), BindingFlags.Instance | BindingFlags.Public);
    public ulong[]? ReadNullableUInt64Array() => !ReadBool() ? null : ReadUInt64Array();


    private static readonly MethodInfo ReadInt16ArrayMethod = typeof(ByteReader).GetMethod(nameof(ReadInt16Array), BindingFlags.Instance | BindingFlags.Public);
    public short[] ReadInt16Array()
    {
        ushort len = ReadUInt16();
        int size = len * sizeof(short);
        if (len == 0) return Array.Empty<short>();
        if (!EnsureMoreLength(size))
            return null!;
        short[] rtn = new short[len];
        for (int i = 0; i < len; i++)
            rtn[i] = BitConverter.ToInt16(_buffer, _index + i * sizeof(short));
        _index += size;
        return rtn;
    }


    private static readonly MethodInfo ReadNullableInt16ArrayMethod = typeof(ByteReader).GetMethod(nameof(ReadNullableInt16Array), BindingFlags.Instance | BindingFlags.Public);
    public short[]? ReadNullableInt16Array() => !ReadBool() ? null : ReadInt16Array();


    private static readonly MethodInfo ReadUInt16ArrayMethod = typeof(ByteReader).GetMethod(nameof(ReadUInt16Array), BindingFlags.Instance | BindingFlags.Public);
    public ushort[] ReadUInt16Array()
    {
        ushort len = ReadUInt16();
        int size = len * sizeof(ushort);
        if (len == 0) return Array.Empty<ushort>();
        if (!EnsureMoreLength(size))
            return null!;
        ushort[] rtn = new ushort[len];
        for (int i = 0; i < len; i++)
            rtn[i] = BitConverter.ToUInt16(_buffer, _index + i * sizeof(ushort));
        _index += size;
        return rtn;
    }


    private static readonly MethodInfo ReadNullableUInt16ArrayMethod = typeof(ByteReader).GetMethod(nameof(ReadNullableUInt16Array), BindingFlags.Instance | BindingFlags.Public);
    public ushort[]? ReadNullableUInt16Array() => !ReadBool() ? null : ReadUInt16Array();


    private static readonly MethodInfo ReadFloatArrayMethod = typeof(ByteReader).GetMethod(nameof(ReadFloatArray), BindingFlags.Instance | BindingFlags.Public);
    public float[] ReadFloatArray()
    {
        ushort len = ReadUInt16();
        int size = len * sizeof(float);
        if (len == 0) return Array.Empty<float>();
        if (!EnsureMoreLength(size))
            return null!;
        float[] rtn = new float[len];
        for (int i = 0; i < len; i++)
            rtn[i] = BitConverter.ToSingle(_buffer, _index + i * sizeof(float));
        _index += size;
        return rtn;
    }


    private static readonly MethodInfo ReadNullableFloatArrayMethod = typeof(ByteReader).GetMethod(nameof(ReadNullableFloatArray), BindingFlags.Instance | BindingFlags.Public);
    public float[]? ReadNullableFloatArray() => !ReadBool() ? null : ReadFloatArray();


    private static readonly MethodInfo ReadDecimalArrayMethod = typeof(ByteReader).GetMethod(nameof(ReadDecimalArray), BindingFlags.Instance | BindingFlags.Public);
    public unsafe decimal[] ReadDecimalArray()
    {
        ushort len = ReadUInt16();
        int size = len * sizeof(decimal);
        if (len == 0) return Array.Empty<decimal>();
        if (!EnsureMoreLength(size))
            return null!;
        decimal[] rtn = new decimal[len];
        const int size2 = sizeof(decimal);
        fixed (byte* ptr = &_buffer![_index])
        {
            for (int i = 0; i < len; ++i)
            {
                byte* ptr2 = ptr + i * size2;
                EndianCheck(ptr2, size2);
                rtn[i] = *(decimal*)ptr2;
            }
        }
        _index += size;
        return rtn;
    }


    private static readonly MethodInfo ReadNullableDecimalArrayMethod = typeof(ByteReader).GetMethod(nameof(ReadNullableDecimalArray), BindingFlags.Instance | BindingFlags.Public);
    public decimal[]? ReadNullableDecimalArray() => !ReadBool() ? null : ReadDecimalArray();


    private static readonly MethodInfo ReadDoubleArrayMethod = typeof(ByteReader).GetMethod(nameof(ReadDoubleArray), BindingFlags.Instance | BindingFlags.Public);
    public double[] ReadDoubleArray()
    {
        ushort len = ReadUInt16();
        int size = len * sizeof(double);
        if (len == 0) return Array.Empty<double>();
        if (!EnsureMoreLength(size))
            return null!;
        double[] rtn = new double[len];
        for (int i = 0; i < len; i++)
            rtn[i] = BitConverter.ToDouble(_buffer, _index + i * sizeof(double));
        _index += size;
        return rtn;
    }


    private static readonly MethodInfo ReadNullableDoubleArrayMethod = typeof(ByteReader).GetMethod(nameof(ReadNullableDoubleArray), BindingFlags.Instance | BindingFlags.Public);
    public double[]? ReadNullableDoubleArray() => !ReadBool() ? null : ReadDoubleArray();


    private static readonly MethodInfo ReadCharArrayMethod = typeof(ByteReader).GetMethod(nameof(ReadCharArray), BindingFlags.Instance | BindingFlags.Public);
    public char[] ReadCharArray()
    {
        ushort length = ReadUInt16();
        if (length == 0) return Array.Empty<char>();
        if (!EnsureMoreLength(length))
            return null!;
        char[] rtn = System.Text.Encoding.UTF8.GetChars(_buffer, _index, length);
        _index += length;
        return rtn;
    }


    private static readonly MethodInfo ReadNullableCharArrayMethod = typeof(ByteReader).GetMethod(nameof(ReadNullableCharArray), BindingFlags.Instance | BindingFlags.Public);
    public char[]? ReadNullableCharArray() => !ReadBool() ? null : ReadCharArray();


    private static readonly MethodInfo ReadStringArrayMethod = typeof(ByteReader).GetMethod(nameof(ReadStringArray), BindingFlags.Instance | BindingFlags.Public);
    public string[] ReadStringArray()
    {
        string[] rtn = new string[ReadUInt16()];
        for (int i = 0; i < rtn.Length; i++)
            rtn[i] = ReadString();
        return rtn;
    }


    private static readonly MethodInfo ReadNullableStringArrayMethod = typeof(ByteReader).GetMethod(nameof(ReadNullableStringArray), BindingFlags.Instance | BindingFlags.Public);
    public string[] ReadNullableStringArray() => !ReadBool() ? null! : ReadStringArray();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static void VerifyType<T>(int typeIndex = -1) => VerifyType(typeof(T), typeIndex);
    protected static void VerifyType(Type type, int typeIndex = -1)
    {
        if (!EncodingEx.IsValidAutoType(type))
            throw new InvalidDynamicTypeException(type, typeIndex, true);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T InvokeReader<T>(Reader<T> reader) => reader.Invoke(this);
    public static Reader<T1>? GetReader<T1>(bool isNullable = false) => (Reader<T1>?)GetReader(typeof(T1), isNullable);
    
    public static Delegate? GetReader(Type type, bool isNullable = false)
    {
        if (type == null) throw new ArgumentNullException(nameof(type));
        Type origType = type;
        DynamicMethod method = new DynamicMethod("Read" + type.Name, type, Parameters, typeof(ByteReader).Module, false);
        ILGenerator il = method.GetILGenerator();
        method.DefineParameter(1, ParameterAttributes.None, "value");

        il.Emit(OpCodes.Ldarg_0);
    redo:
        if (type.IsPrimitive)
        {
            if (type == typeof(ulong))
                il.EmitCall(OpCodes.Call, isNullable ? ReadNullableUInt64Method : ReadUInt64Method, null);
            else if (type == typeof(float))
                il.EmitCall(OpCodes.Call, isNullable ? ReadNullableFloatMethod : ReadFloatMethod, null);
            else if (type == typeof(long))
                il.EmitCall(OpCodes.Call, isNullable ? ReadNullableInt64Method : ReadInt64Method, null);
            else if (type == typeof(ushort))
                il.EmitCall(OpCodes.Call, isNullable ? ReadNullableUInt16Method : ReadUInt16Method, null);
            else if (type == typeof(short))
                il.EmitCall(OpCodes.Call, isNullable ? ReadNullableInt16Method : ReadInt16Method, null);
            else if (type == typeof(byte))
                il.EmitCall(OpCodes.Call, isNullable ? ReadNullableUInt8Method : ReadUInt8Method, null);
            else if (type == typeof(int))
                il.EmitCall(OpCodes.Call, isNullable ? ReadNullableInt32Method : ReadInt32Method, null);
            else if (type == typeof(uint))
                il.EmitCall(OpCodes.Call, isNullable ? ReadNullableUInt32Method : ReadUInt32Method, null);
            else if (type == typeof(bool))
                il.EmitCall(OpCodes.Call, isNullable ? ReadNullableBoolMethod : ReadBoolMethod, null);
            else if (type == typeof(char))
                il.EmitCall(OpCodes.Call, isNullable ? ReadNullableCharMethod : ReadCharMethod, null);
            else if (type == typeof(sbyte))
                il.EmitCall(OpCodes.Call, isNullable ? ReadNullableInt8Method : ReadInt8Method, null);
            else if (type == typeof(double))
                il.EmitCall(OpCodes.Call, isNullable ? ReadNullableDoubleMethod : ReadDoubleMethod, null);
            else throw new ArgumentException($"Can not convert that type ({type.Name})!", nameof(type));
        }
        else if (type == typeof(string))
        {
            il.EmitCall(OpCodes.Call, isNullable ? ReadNullableStringMethod : ReadStringMethod, null);
        }
        else if (type.IsEnum)
        {
            il.EmitCall(OpCodes.Call, isNullable ? ReadNullableEnumMethod.MakeGenericMethod(origType) : ReadEnumMethod.MakeGenericMethod(origType), null);
        }
        else if (!isNullable && type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            type = type.GenericTypeArguments[0];
            isNullable = true;
            goto redo;
        }
        else if (type == typeof(decimal))
        {
            il.EmitCall(OpCodes.Call, isNullable ? ReadNullableDecimalMethod : ReadDecimalMethod, null);
        }
        else if (type == typeof(DateTime))
        {
            il.EmitCall(OpCodes.Call, isNullable ? ReadNullableDateTimeMethod : ReadDateTimeMethod, null);
        }
        else if (typeof(Type).IsAssignableFrom(type))
        {
            il.EmitCall(OpCodes.Call, ReadTypeMethod, null);
        }
        else if (type == typeof(DateTimeOffset))
        {
            il.EmitCall(OpCodes.Call, isNullable ? ReadNullableDateTimeOffsetMethod : ReadDateTimeOffsetMethod, null);
        }
        else if (type == typeof(TimeSpan))
        {
            il.EmitCall(OpCodes.Call, isNullable ? ReadNullableTimeSpanMethod : ReadTimeSpanMethod, null);
        }
        else if (type == typeof(Guid))
        {
            il.EmitCall(OpCodes.Call, isNullable ? ReadNullableGuidMethod : ReadGuidMethod, null);
        }
        else if (type == typeof(Vector2))
        {
            il.EmitCall(OpCodes.Call, isNullable ? ReadNullableVector2Method : ReadVector2Method, null);
        }
        else if (type == typeof(Vector3))
        {
            il.EmitCall(OpCodes.Call, isNullable ? ReadNullableVector3Method : ReadVector3Method, null);
        }
        else if (type == typeof(Vector4))
        {
            il.EmitCall(OpCodes.Call, isNullable ? ReadNullableVector4Method : ReadVector4Method, null);
        }
        else if (type == typeof(Bounds))
        {
            il.EmitCall(OpCodes.Call, isNullable ? ReadNullableBoundsMethod : ReadBoundsMethod, null);
        }
        else if (type == typeof(Quaternion))
        {
            il.EmitCall(OpCodes.Call, isNullable ? ReadNullableQuaternionMethod : ReadQuaternionMethod, null);
        }
        else if (type == typeof(Color))
        {
            il.EmitCall(OpCodes.Call, isNullable ? ReadNullableColorMethod : ReadColorMethod, null);
        }
        else if (type == typeof(Color32))
        {
            il.EmitCall(OpCodes.Call, isNullable ? ReadNullableColor32Method : ReadColor32Method, null);
        }
        else if (type.IsArray)
        {
            Type elemType = type.GetElementType();
            if (elemType == typeof(ulong))
                il.EmitCall(OpCodes.Call, isNullable ? ReadNullableUInt64ArrayMethod : ReadUInt64ArrayMethod, null);
            else if (elemType == typeof(float))
                il.EmitCall(OpCodes.Call, isNullable ? ReadNullableFloatArrayMethod : ReadFloatArrayMethod, null);
            else if (elemType == typeof(long))
                il.EmitCall(OpCodes.Call, isNullable ? ReadNullableInt64ArrayMethod : ReadInt64ArrayMethod, null);
            else if (elemType == typeof(ushort))
                il.EmitCall(OpCodes.Call, isNullable ? ReadNullableUInt16ArrayMethod : ReadUInt16ArrayMethod, null);
            else if (elemType == typeof(short))
                il.EmitCall(OpCodes.Call, isNullable ? ReadNullableInt16ArrayMethod : ReadInt16ArrayMethod, null);
            else if (elemType == typeof(byte))
                il.EmitCall(OpCodes.Call, isNullable ? ReadNullableByteArrayMethod : ReadByteArrayMethod, null);
            else if (elemType == typeof(int))
                il.EmitCall(OpCodes.Call, isNullable ? ReadNullableInt32ArrayMethod : ReadInt32ArrayMethod, null);
            else if (elemType == typeof(uint))
                il.EmitCall(OpCodes.Call, isNullable ? ReadNullableUInt32ArrayMethod : ReadUInt32ArrayMethod, null);
            else if (elemType == typeof(bool))
                il.EmitCall(OpCodes.Call, isNullable ? ReadNullableBoolArrayMethod : ReadBoolArrayMethod, null);
            else if (elemType == typeof(sbyte))
                il.EmitCall(OpCodes.Call, isNullable ? ReadNullableInt8ArrayMethod : ReadInt8ArrayMethod, null);
            else if (elemType == typeof(decimal))
                il.EmitCall(OpCodes.Call, isNullable ? ReadNullableDecimalArrayMethod : ReadDecimalArrayMethod, null);
            else if (elemType == typeof(char))
                il.EmitCall(OpCodes.Call, isNullable ? ReadNullableCharArrayMethod : ReadCharArrayMethod, null);
            else if (elemType == typeof(double))
                il.EmitCall(OpCodes.Call, isNullable ? ReadNullableDoubleArrayMethod : ReadDoubleArrayMethod, null);
            else if (elemType == typeof(string))
                il.EmitCall(OpCodes.Call, isNullable ? ReadNullableStringArrayMethod : ReadStringArrayMethod, null);
            else if (elemType == typeof(Guid))
                il.EmitCall(OpCodes.Call, isNullable ? ReadNullableGuidArrayMethod : ReadGuidArrayMethod, null);
            else if (elemType == typeof(DateTime))
                il.EmitCall(OpCodes.Call, isNullable ? ReadNullableDateTimeArrayMethod : ReadDateTimeArrayMethod, null);
            else if (elemType == typeof(DateTimeOffset))
                il.EmitCall(OpCodes.Call, isNullable ? ReadNullableDateTimeOffsetArrayMethod : ReadDateTimeOffsetArrayMethod, null);
            else throw new ArgumentException($"Can not convert that array type ({type.Name})!", nameof(type));
        }
        else throw new ArgumentException($"Can not convert that type ({type.Name})!", nameof(type));
        il.Emit(OpCodes.Ret);
        try
        {
            return method.CreateDelegate(typeof(Reader<>).MakeGenericType(origType));
        }
        catch (InvalidProgramException ex)
        {
            Logger.LogError(ex);
            return null;
        }
        catch (ArgumentException ex)
        {
            Logger.LogError("Failed to create reader delegate for type " + origType.FullName);
            Logger.LogError(ex);
            return null;
        }
    }
    public static MethodInfo? GetReadMethod(Type type, bool isNullable = false)
    {
    redo:
        if (type.IsPrimitive)
        {
            if (type == typeof(ulong))
                return isNullable ? ReadNullableUInt64Method : ReadUInt64Method;
            if (type == typeof(float))
                return isNullable ? ReadNullableFloatMethod : ReadFloatMethod;
            if (type == typeof(long))
                return isNullable ? ReadNullableInt64Method : ReadInt64Method;
            if (type == typeof(ushort))
                return isNullable ? ReadNullableUInt16Method : ReadUInt16Method;
            if (type == typeof(short))
                return isNullable ? ReadNullableInt16Method : ReadInt16Method;
            if (type == typeof(byte))
                return isNullable ? ReadNullableUInt8Method : ReadUInt8Method;
            if (type == typeof(int))
                return isNullable ? ReadNullableInt32Method : ReadInt32Method;
            if (type == typeof(uint))
                return isNullable ? ReadNullableUInt32Method : ReadUInt32Method;
            if (type == typeof(bool))
                return isNullable ? ReadNullableBoolMethod : ReadBoolMethod;
            if (type == typeof(char))
                return isNullable ? ReadNullableCharMethod : ReadCharMethod;
            if (type == typeof(sbyte))
                return isNullable ? ReadNullableInt8Method : ReadInt8Method;
            if (type == typeof(double))
                return isNullable ? ReadNullableDoubleMethod : ReadDoubleMethod;
        }

        if (type == typeof(string))
            return isNullable ? ReadNullableStringMethod : ReadStringMethod;
        if (type.IsEnum)
            return isNullable ? ReadNullableEnumMethod.MakeGenericMethod(type) : ReadEnumMethod.MakeGenericMethod(type);
        
        if (!isNullable && type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            type = type.GenericTypeArguments[0];
            isNullable = true;
            goto redo;
        }
        if (type == typeof(decimal))
            return isNullable ? ReadNullableDecimalMethod : ReadDecimalMethod;
        if (type == typeof(DateTime))
            return isNullable ? ReadNullableDateTimeMethod : ReadDateTimeMethod;
        if (type == typeof(DateTimeOffset))
            return isNullable ? ReadNullableDateTimeOffsetMethod : ReadDateTimeOffsetMethod;
        if (type == typeof(TimeSpan))
            return isNullable ? ReadNullableTimeSpanMethod : ReadTimeSpanMethod;
        if (type == typeof(Guid))
            return isNullable ? ReadNullableGuidMethod : ReadGuidMethod;
        if (type == typeof(Vector2))
            return isNullable ? ReadNullableVector2Method : ReadVector2Method;
        if (type == typeof(Vector3))
            return isNullable ? ReadNullableVector3Method : ReadVector3Method;
        if (type == typeof(Vector4))
            return isNullable ? ReadNullableVector4Method : ReadVector4Method;
        if (type == typeof(Bounds))
            return isNullable ? ReadNullableBoundsMethod : ReadBoundsMethod;
        if (type == typeof(Quaternion))
            return isNullable ? ReadNullableQuaternionMethod : ReadQuaternionMethod;
        if (type == typeof(Color))
            return isNullable ? ReadNullableColorMethod : ReadColorMethod;
        if (type == typeof(Color32))
            return isNullable ? ReadNullableColor32Method : ReadColor32Method;
        if (type.IsArray)
        {
            Type elemType = type.GetElementType();
            if (elemType == typeof(ulong))
                return isNullable ? ReadNullableUInt64ArrayMethod : ReadUInt64ArrayMethod;
            if (elemType == typeof(float))
                return isNullable ? ReadNullableFloatArrayMethod : ReadFloatArrayMethod;
            if (elemType == typeof(long))
                return isNullable ? ReadNullableInt64ArrayMethod : ReadInt64ArrayMethod;
            if (elemType == typeof(ushort))
                return isNullable ? ReadNullableUInt16ArrayMethod : ReadUInt16ArrayMethod;
            if (elemType == typeof(short))
                return isNullable ? ReadNullableInt16ArrayMethod : ReadInt16ArrayMethod;
            if (elemType == typeof(byte))
                return isNullable ? ReadNullableByteArrayMethod : ReadByteArrayMethod;
            if (elemType == typeof(int))
                return isNullable ? ReadNullableInt32ArrayMethod : ReadInt32ArrayMethod;
            if (elemType == typeof(uint))
                return isNullable ? ReadNullableUInt32ArrayMethod : ReadUInt32ArrayMethod;
            if (elemType == typeof(bool))
                return isNullable ? ReadNullableBoolArrayMethod : ReadBoolArrayMethod;
            if (elemType == typeof(sbyte))
                return isNullable ? ReadNullableInt8ArrayMethod : ReadInt8ArrayMethod;
            if (elemType == typeof(decimal))
                return isNullable ? ReadNullableDecimalArrayMethod : ReadDecimalArrayMethod;
            if (elemType == typeof(char))
                return isNullable ? ReadNullableCharArrayMethod : ReadCharArrayMethod;
            if (elemType == typeof(double))
                return isNullable ? ReadNullableDoubleArrayMethod : ReadDoubleArrayMethod;
            if (elemType == typeof(string))
                return isNullable ? ReadNullableStringArrayMethod : ReadStringArrayMethod;
            if (elemType == typeof(Guid))
                return isNullable ? ReadNullableGuidArrayMethod : ReadGuidArrayMethod;
            if (elemType == typeof(DateTime))
                return isNullable ? ReadNullableDateTimeArrayMethod : ReadDateTimeArrayMethod;
            if (elemType == typeof(DateTimeOffset))
                return isNullable ? ReadNullableDateTimeOffsetArrayMethod : ReadDateTimeOffsetArrayMethod;
        }

        return null;
    }
    public static class ReaderHelper<T>
    {
        public static readonly Reader<T>? Reader;
        static ReaderHelper()
        {
            VerifyType<T>();
            Reader = GetReader<T>();
        }
    }
    public static class NullableReaderHelper<T>
    {
        public static readonly Reader<T>? Reader;
        static NullableReaderHelper()
        {
            VerifyType<T>();
            Reader = GetReader<T>(true);
        }
    }
}
public sealed class ByteReaderRaw<T> : ByteReader
{
    private readonly Reader<T> reader;
    /// <summary>Leave <paramref name="reader"/> null to auto-fill.</summary>
    public ByteReaderRaw(Reader<T>? reader)
    {
        this.reader = reader ?? ReaderHelper<T>.Reader!;
    }
    public bool Read(byte[]? bytes, out T arg)
    {
        if (bytes != null)
            LoadNew(bytes);
        arg = reader.Invoke(this);
        return !HasFailed;
    }
}
public sealed class ByteReaderRaw<T1, T2> : ByteReader
{
    private readonly Reader<T1> reader1;
    private readonly Reader<T2> reader2;
    /// <summary>Leave any reader null to auto-fill.</summary>
    public ByteReaderRaw(Reader<T1>? reader1, Reader<T2>? reader2)
    {
        this.reader1 = reader1 ?? ReaderHelper<T1>.Reader!;
        this.reader2 = reader2 ?? ReaderHelper<T2>.Reader!;
    }
    public bool Read(byte[]? bytes, out T1 arg1, out T2 arg2)
    {
        if (bytes != null)
            LoadNew(bytes);
        arg1 = reader1.Invoke(this);
        arg2 = reader2.Invoke(this);
        return !HasFailed;
    }
}
public sealed class ByteReaderRaw<T1, T2, T3> : ByteReader
{
    private readonly Reader<T1> reader1;
    private readonly Reader<T2> reader2;
    private readonly Reader<T3> reader3;
    /// <summary>Leave any reader null to auto-fill.</summary>
    public ByteReaderRaw(Reader<T1>? reader1, Reader<T2>? reader2, Reader<T3>? reader3)
    {
        this.reader1 = reader1 ?? ReaderHelper<T1>.Reader!;
        this.reader2 = reader2 ?? ReaderHelper<T2>.Reader!;
        this.reader3 = reader3 ?? ReaderHelper<T3>.Reader!;
    }
    public bool Read(byte[]? bytes, out T1 arg1, out T2 arg2, out T3 arg3)
    {
        if (bytes != null)
            LoadNew(bytes);
        arg1 = reader1.Invoke(this);
        arg2 = reader2.Invoke(this);
        arg3 = reader3.Invoke(this);
        return !HasFailed;
    }
}
public sealed class ByteReaderRaw<T1, T2, T3, T4> : ByteReader
{
    private readonly Reader<T1> reader1;
    private readonly Reader<T2> reader2;
    private readonly Reader<T3> reader3;
    private readonly Reader<T4> reader4;
    /// <summary>Leave any reader null to auto-fill.</summary>
    public ByteReaderRaw(Reader<T1>? reader1, Reader<T2>? reader2, Reader<T3>? reader3, Reader<T4>? reader4)
    {
        this.reader1 = reader1 ?? ReaderHelper<T1>.Reader!;
        this.reader2 = reader2 ?? ReaderHelper<T2>.Reader!;
        this.reader3 = reader3 ?? ReaderHelper<T3>.Reader!;
        this.reader4 = reader4 ?? ReaderHelper<T4>.Reader!;
    }
    public bool Read(byte[]? bytes, out T1 arg1, out T2 arg2, out T3 arg3, out T4 arg4)
    {
        if (bytes != null)
            LoadNew(bytes);
        arg1 = reader1.Invoke(this);
        arg2 = reader2.Invoke(this);
        arg3 = reader3.Invoke(this);
        arg4 = reader4.Invoke(this);
        return !HasFailed;
    }
}
public sealed class DynamicByteReader<T1> : ByteReader
{
    static DynamicByteReader()
    {
        reader1 = ReaderHelper<T1>.Reader!;
    }
    private static readonly Reader<T1> reader1;

    public bool Read(byte[]? bytes, out T1 arg1)
    {
        if (bytes != null)
            LoadNew(bytes);
        arg1 = InvokeReader(reader1);
        return !HasFailed;
    }
}
public sealed class DynamicByteReader<T1, T2> : ByteReader
{
    static DynamicByteReader()
    {
        reader1 = ReaderHelper<T1>.Reader!;
        reader2 = ReaderHelper<T2>.Reader!;
    }
    private static readonly Reader<T1> reader1;
    private static readonly Reader<T2> reader2;

    public bool Read(byte[]? bytes, out T1 arg1, out T2 arg2)
    {
        if (bytes != null)
            LoadNew(bytes);
        arg1 = InvokeReader(reader1);
        arg2 = InvokeReader(reader2);
        return !HasFailed;
    }
}
public sealed class DynamicByteReader<T1, T2, T3> : ByteReader
{
    static DynamicByteReader()
    {
        reader1 = ReaderHelper<T1>.Reader!;
        reader2 = ReaderHelper<T2>.Reader!;
        reader3 = ReaderHelper<T3>.Reader!;
    }
    private static readonly Reader<T1> reader1;
    private static readonly Reader<T2> reader2;
    private static readonly Reader<T3> reader3;

    public bool Read(byte[]? bytes, out T1 arg1, out T2 arg2, out T3 arg3)
    {
        if (bytes != null)
            LoadNew(bytes);
        arg1 = InvokeReader(reader1);
        arg2 = InvokeReader(reader2);
        arg3 = InvokeReader(reader3);
        return !HasFailed;
    }
}
public sealed class DynamicByteReader<T1, T2, T3, T4> : ByteReader
{
    static DynamicByteReader()
    {
        reader1 = ReaderHelper<T1>.Reader!;
        reader2 = ReaderHelper<T2>.Reader!;
        reader3 = ReaderHelper<T3>.Reader!;
        reader4 = ReaderHelper<T4>.Reader!;
    }
    private static readonly Reader<T1> reader1;
    private static readonly Reader<T2> reader2;
    private static readonly Reader<T3> reader3;
    private static readonly Reader<T4> reader4;

    public bool Read(byte[]? bytes, out T1 arg1, out T2 arg2, out T3 arg3, out T4 arg4)
    {
        if (bytes != null)
            LoadNew(bytes);
        arg1 = InvokeReader(reader1);
        arg2 = InvokeReader(reader2);
        arg3 = InvokeReader(reader3);
        arg4 = InvokeReader(reader4);
        return !HasFailed;
    }
}
public sealed class DynamicByteReader<T1, T2, T3, T4, T5> : ByteReader
{
    static DynamicByteReader()
    {
        reader1 = ReaderHelper<T1>.Reader!;
        reader2 = ReaderHelper<T2>.Reader!;
        reader3 = ReaderHelper<T3>.Reader!;
        reader4 = ReaderHelper<T4>.Reader!;
        reader5 = ReaderHelper<T5>.Reader!;
    }
    private static readonly Reader<T1> reader1;
    private static readonly Reader<T2> reader2;
    private static readonly Reader<T3> reader3;
    private static readonly Reader<T4> reader4;
    private static readonly Reader<T5> reader5;

    public bool Read(byte[]? bytes, out T1 arg1, out T2 arg2, out T3 arg3, out T4 arg4, out T5 arg5)
    {
        if (bytes != null)
            LoadNew(bytes);
        arg1 = InvokeReader(reader1);
        arg2 = InvokeReader(reader2);
        arg3 = InvokeReader(reader3);
        arg4 = InvokeReader(reader4);
        arg5 = InvokeReader(reader5);
        return !HasFailed;
    }
}
public sealed class DynamicByteReader<T1, T2, T3, T4, T5, T6> : ByteReader
{
    static DynamicByteReader()
    {
        reader1 = ReaderHelper<T1>.Reader!;
        reader2 = ReaderHelper<T2>.Reader!;
        reader3 = ReaderHelper<T3>.Reader!;
        reader4 = ReaderHelper<T4>.Reader!;
        reader5 = ReaderHelper<T5>.Reader!;
        reader6 = ReaderHelper<T6>.Reader!;
    }
    private static readonly Reader<T1> reader1;
    private static readonly Reader<T2> reader2;
    private static readonly Reader<T3> reader3;
    private static readonly Reader<T4> reader4;
    private static readonly Reader<T5> reader5;
    private static readonly Reader<T6> reader6;

    public bool Read(byte[]? bytes, out T1 arg1, out T2 arg2, out T3 arg3, out T4 arg4, out T5 arg5, out T6 arg6)
    {
        if (bytes != null)
            LoadNew(bytes);
        arg1 = InvokeReader(reader1);
        arg2 = InvokeReader(reader2);
        arg3 = InvokeReader(reader3);
        arg4 = InvokeReader(reader4);
        arg5 = InvokeReader(reader5);
        arg6 = InvokeReader(reader6);
        return !HasFailed;
    }
}
public sealed class DynamicByteReader<T1, T2, T3, T4, T5, T6, T7> : ByteReader
{
    static DynamicByteReader()
    {
        reader1 = ReaderHelper<T1>.Reader!;
        reader2 = ReaderHelper<T2>.Reader!;
        reader3 = ReaderHelper<T3>.Reader!;
        reader4 = ReaderHelper<T4>.Reader!;
        reader5 = ReaderHelper<T5>.Reader!;
        reader6 = ReaderHelper<T6>.Reader!;
        reader7 = ReaderHelper<T7>.Reader!;
    }
    private static readonly Reader<T1> reader1;
    private static readonly Reader<T2> reader2;
    private static readonly Reader<T3> reader3;
    private static readonly Reader<T4> reader4;
    private static readonly Reader<T5> reader5;
    private static readonly Reader<T6> reader6;
    private static readonly Reader<T7> reader7;

    public bool Read(byte[]? bytes, out T1 arg1, out T2 arg2, out T3 arg3, out T4 arg4, out T5 arg5, out T6 arg6, out T7 arg7)
    {
        if (bytes != null)
            LoadNew(bytes);
        arg1 = InvokeReader(reader1);
        arg2 = InvokeReader(reader2);
        arg3 = InvokeReader(reader3);
        arg4 = InvokeReader(reader4);
        arg5 = InvokeReader(reader5);
        arg6 = InvokeReader(reader6);
        arg7 = InvokeReader(reader7);
        return !HasFailed;
    }
}
public sealed class DynamicByteReader<T1, T2, T3, T4, T5, T6, T7, T8> : ByteReader
{
    static DynamicByteReader()
    {
        reader1 = ReaderHelper<T1>.Reader!;
        reader2 = ReaderHelper<T2>.Reader!;
        reader3 = ReaderHelper<T3>.Reader!;
        reader4 = ReaderHelper<T4>.Reader!;
        reader5 = ReaderHelper<T5>.Reader!;
        reader6 = ReaderHelper<T6>.Reader!;
        reader7 = ReaderHelper<T7>.Reader!;
        reader8 = ReaderHelper<T8>.Reader!;
    }
    private static readonly Reader<T1> reader1;
    private static readonly Reader<T2> reader2;
    private static readonly Reader<T3> reader3;
    private static readonly Reader<T4> reader4;
    private static readonly Reader<T5> reader5;
    private static readonly Reader<T6> reader6;
    private static readonly Reader<T7> reader7;
    private static readonly Reader<T8> reader8;

    public bool Read(byte[]? bytes, out T1 arg1, out T2 arg2, out T3 arg3, out T4 arg4, out T5 arg5, out T6 arg6, out T7 arg7, out T8 arg8)
    {
        if (bytes != null)
            LoadNew(bytes);
        arg1 = InvokeReader(reader1);
        arg2 = InvokeReader(reader2);
        arg3 = InvokeReader(reader3);
        arg4 = InvokeReader(reader4);
        arg5 = InvokeReader(reader5);
        arg6 = InvokeReader(reader6);
        arg7 = InvokeReader(reader7);
        arg8 = InvokeReader(reader8);
        return !HasFailed;
    }
}
public sealed class DynamicByteReader<T1, T2, T3, T4, T5, T6, T7, T8, T9> : ByteReader
{
    static DynamicByteReader()
    {
        reader1 = ReaderHelper<T1>.Reader!;
        reader2 = ReaderHelper<T2>.Reader!;
        reader3 = ReaderHelper<T3>.Reader!;
        reader4 = ReaderHelper<T4>.Reader!;
        reader5 = ReaderHelper<T5>.Reader!;
        reader6 = ReaderHelper<T6>.Reader!;
        reader7 = ReaderHelper<T7>.Reader!;
        reader8 = ReaderHelper<T8>.Reader!;
        reader9 = ReaderHelper<T9>.Reader!;
    }
    private static readonly Reader<T1> reader1;
    private static readonly Reader<T2> reader2;
    private static readonly Reader<T3> reader3;
    private static readonly Reader<T4> reader4;
    private static readonly Reader<T5> reader5;
    private static readonly Reader<T6> reader6;
    private static readonly Reader<T7> reader7;
    private static readonly Reader<T8> reader8;
    private static readonly Reader<T9> reader9;

    public bool Read(byte[]? bytes, out T1 arg1, out T2 arg2, out T3 arg3, out T4 arg4, out T5 arg5, out T6 arg6, out T7 arg7, out T8 arg8, out T9 arg9)
    {
        if (bytes != null)
            LoadNew(bytes);
        arg1 = InvokeReader(reader1);
        arg2 = InvokeReader(reader2);
        arg3 = InvokeReader(reader3);
        arg4 = InvokeReader(reader4);
        arg5 = InvokeReader(reader5);
        arg6 = InvokeReader(reader6);
        arg7 = InvokeReader(reader7);
        arg8 = InvokeReader(reader8);
        arg9 = InvokeReader(reader9);
        return !HasFailed;
    }
}
public sealed class DynamicByteReader<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> : ByteReader
{
    static DynamicByteReader()
    {
        reader1 = ReaderHelper<T1>.Reader!;
        reader2 = ReaderHelper<T2>.Reader!;
        reader3 = ReaderHelper<T3>.Reader!;
        reader4 = ReaderHelper<T4>.Reader!;
        reader5 = ReaderHelper<T5>.Reader!;
        reader6 = ReaderHelper<T6>.Reader!;
        reader7 = ReaderHelper<T7>.Reader!;
        reader8 = ReaderHelper<T8>.Reader!;
        reader9 = ReaderHelper<T9>.Reader!;
        reader10 = ReaderHelper<T10>.Reader!;
    }
    private static readonly Reader<T1> reader1;
    private static readonly Reader<T2> reader2;
    private static readonly Reader<T3> reader3;
    private static readonly Reader<T4> reader4;
    private static readonly Reader<T5> reader5;
    private static readonly Reader<T6> reader6;
    private static readonly Reader<T7> reader7;
    private static readonly Reader<T8> reader8;
    private static readonly Reader<T9> reader9;
    private static readonly Reader<T10> reader10;

    public bool Read(byte[]? bytes, out T1 arg1, out T2 arg2, out T3 arg3, out T4 arg4, out T5 arg5, out T6 arg6, out T7 arg7, out T8 arg8, out T9 arg9, out T10 arg10)
    {
        if (bytes != null)
            LoadNew(bytes);
        arg1 = InvokeReader(reader1);
        arg2 = InvokeReader(reader2);
        arg3 = InvokeReader(reader3);
        arg4 = InvokeReader(reader4);
        arg5 = InvokeReader(reader5);
        arg6 = InvokeReader(reader6);
        arg7 = InvokeReader(reader7);
        arg8 = InvokeReader(reader8);
        arg9 = InvokeReader(reader9);
        arg10 = InvokeReader(reader10);
        return !HasFailed;
    }
}
public sealed class InvalidDynamicTypeException : Exception
{
    public InvalidDynamicTypeException() { }
    public InvalidDynamicTypeException(Type arg, int typeNumber, bool reader) :
        base("Generic argument " + arg.Name + ": T" + typeNumber + " is not able to be " + (reader ? "read" : "written") + " automatically.")
    { }
}
