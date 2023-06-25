using DevkitServer.Models;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;

// ReSharper disable AssignNullToNotNullAttribute

namespace DevkitServer.Util.Encoding;

public class ByteReader
{
    private const int MinStreamBufferSize = 32;
    private const int GuidSize = 16;

    private static readonly bool IsBigEndian = !BitConverter.IsLittleEndian;
    private static Dictionary<Type, MethodInfo>? _nonNullableReaders;
    private static Dictionary<Type, MethodInfo>? _nullableReaders;
    private static readonly MethodInfo ReadEnumMethod = typeof(ByteReader).GetMethod(nameof(ReadEnum), BindingFlags.Instance | BindingFlags.Public)
                                                         ?? throw new MemberAccessException("Unable to find read enum method.");
    private static readonly MethodInfo ReadNullableEnumMethod = typeof(ByteReader).GetMethod(nameof(ReadNullableEnum), BindingFlags.Instance | BindingFlags.Public)
                                                                 ?? throw new MemberAccessException("Unable to find read nullable enum method.");

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

    private static void PrepareMethods()
    {
        _nonNullableReaders ??= new Dictionary<Type, MethodInfo>(45)
        {
            { typeof(int), GetMethod(nameof(ReadInt32)) },
            { typeof(uint), GetMethod(nameof(ReadUInt32)) },
            { typeof(byte), GetMethod(nameof(ReadUInt8)) },
            { typeof(sbyte), GetMethod(nameof(ReadInt8)) },
            { typeof(bool), GetMethod(nameof(ReadBool)) },
            { typeof(long), GetMethod(nameof(ReadInt64)) },
            { typeof(ulong), GetMethod(nameof(ReadUInt64)) },
            { typeof(short), GetMethod(nameof(ReadInt16)) },
            { typeof(ushort), GetMethod(nameof(ReadUInt16)) },
            { typeof(float), GetMethod(nameof(ReadFloat)) },
            { typeof(decimal), GetMethod(nameof(ReadDecimal)) },
            { typeof(double), GetMethod(nameof(ReadDouble)) },
            { typeof(char), GetMethod(nameof(ReadChar)) },
            { typeof(string), GetMethod(nameof(ReadString)) },
            { typeof(Type), GetMethod(nameof(ReadType)) },
            { typeof(Type[]), GetMethod(nameof(ReadTypeArray)) },
            { typeof(RegionIdentifier), typeof(RegionIdentifier).GetMethod(nameof(RegionIdentifier.Read), BindingFlags.Static | BindingFlags.Public) },
            { typeof(DateTime), GetMethod(nameof(ReadDateTime)) },
            { typeof(DateTimeOffset), GetMethod(nameof(ReadDateTimeOffset)) },
            { typeof(TimeSpan), GetMethod(nameof(ReadTimeSpan)) },
            { typeof(Guid), GetMethod(nameof(ReadGuid)) },
            { typeof(Vector2), GetMethod(nameof(ReadVector2)) },
            { typeof(Vector3), GetMethod(nameof(ReadVector3)) },
            { typeof(Vector4), GetMethod(nameof(ReadVector4)) },
            { typeof(Bounds), GetMethod(nameof(ReadBounds)) },
            { typeof(Quaternion), GetMethod(nameof(ReadQuaternion)) },
            { typeof(Color), GetMethod(nameof(ReadColor)) },
            { typeof(Color32), GetMethod(nameof(ReadColor32)) },
            { typeof(Guid[]), GetMethod(nameof(ReadGuidArray)) },
            { typeof(DateTime[]), GetMethod(nameof(ReadDateTimeArray)) },
            { typeof(DateTimeOffset[]), GetMethod(nameof(ReadDateTimeOffsetArray)) },
            { typeof(byte[]), GetMethod(nameof(ReadUInt8Array)) },
            { typeof(sbyte[]), GetMethod(nameof(ReadInt8Array)) },
            { typeof(int[]), GetMethod(nameof(ReadInt32Array)) },
            { typeof(uint[]), GetMethod(nameof(ReadUInt32Array)) },
            { typeof(bool[]), GetMethod(nameof(ReadBoolArray)) },
            { typeof(long[]), GetMethod(nameof(ReadInt64Array)) },
            { typeof(ulong[]), GetMethod(nameof(ReadUInt64Array)) },
            { typeof(short[]), GetMethod(nameof(ReadInt16Array)) },
            { typeof(ushort[]), GetMethod(nameof(ReadUInt16Array)) },
            { typeof(float[]), GetMethod(nameof(ReadFloatArray)) },
            { typeof(double[]), GetMethod(nameof(ReadDoubleArray)) },
            { typeof(decimal[]), GetMethod(nameof(ReadDecimalArray)) },
            { typeof(char[]), GetMethod(nameof(ReadCharArray)) },
            { typeof(string[]), GetMethod(nameof(ReadStringArray)) },
        };

        _nullableReaders ??= new Dictionary<Type, MethodInfo>(44)
        {
            { typeof(int), GetMethod(nameof(ReadNullableInt32)) },
            { typeof(uint), GetMethod(nameof(ReadNullableUInt32)) },
            { typeof(byte), GetMethod(nameof(ReadNullableUInt8)) },
            { typeof(sbyte), GetMethod(nameof(ReadNullableInt8)) },
            { typeof(bool), GetMethod(nameof(ReadNullableBool)) },
            { typeof(long), GetMethod(nameof(ReadNullableInt64)) },
            { typeof(ulong), GetMethod(nameof(ReadNullableUInt64)) },
            { typeof(short), GetMethod(nameof(ReadNullableInt16)) },
            { typeof(ushort), GetMethod(nameof(ReadNullableUInt16)) },
            { typeof(float), GetMethod(nameof(ReadNullableFloat)) },
            { typeof(decimal), GetMethod(nameof(ReadNullableDecimal)) },
            { typeof(double), GetMethod(nameof(ReadNullableDouble)) },
            { typeof(char), GetMethod(nameof(ReadNullableChar)) },
            { typeof(string), GetMethod(nameof(ReadNullableString)) },
            { typeof(Type), GetMethod(nameof(ReadType)) },
            { typeof(Type[]), GetMethod(nameof(ReadTypeArray)) },
            { typeof(DateTime), GetMethod(nameof(ReadNullableDateTime)) },
            { typeof(DateTimeOffset), GetMethod(nameof(ReadNullableDateTimeOffset)) },
            { typeof(TimeSpan), GetMethod(nameof(ReadNullableTimeSpan)) },
            { typeof(Guid), GetMethod(nameof(ReadNullableGuid)) },
            { typeof(Vector2), GetMethod(nameof(ReadNullableVector2)) },
            { typeof(Vector3), GetMethod(nameof(ReadNullableVector3)) },
            { typeof(Vector4), GetMethod(nameof(ReadNullableVector4)) },
            { typeof(Bounds), GetMethod(nameof(ReadNullableBounds)) },
            { typeof(Quaternion), GetMethod(nameof(ReadNullableQuaternion)) },
            { typeof(Color), GetMethod(nameof(ReadNullableColor)) },
            { typeof(Color32), GetMethod(nameof(ReadNullableColor32)) },
            { typeof(Guid[]), GetMethod(nameof(ReadNullableGuidArray)) },
            { typeof(DateTime[]), GetMethod(nameof(ReadNullableDateTimeArray)) },
            { typeof(DateTimeOffset[]), GetMethod(nameof(ReadNullableDateTimeOffsetArray)) },
            { typeof(byte[]), GetMethod(nameof(ReadNullableUInt8Array)) },
            { typeof(sbyte[]), GetMethod(nameof(ReadNullableInt8Array)) },
            { typeof(int[]), GetMethod(nameof(ReadNullableInt32Array)) },
            { typeof(uint[]), GetMethod(nameof(ReadNullableUInt32Array)) },
            { typeof(bool[]), GetMethod(nameof(ReadNullableBoolArray)) },
            { typeof(long[]), GetMethod(nameof(ReadNullableInt64Array)) },
            { typeof(ulong[]), GetMethod(nameof(ReadNullableUInt64Array)) },
            { typeof(short[]), GetMethod(nameof(ReadNullableInt16Array)) },
            { typeof(ushort[]), GetMethod(nameof(ReadNullableUInt16Array)) },
            { typeof(float[]), GetMethod(nameof(ReadNullableFloatArray)) },
            { typeof(double[]), GetMethod(nameof(ReadNullableDoubleArray)) },
            { typeof(decimal[]), GetMethod(nameof(ReadNullableDecimalArray)) },
            { typeof(char[]), GetMethod(nameof(ReadNullableCharArray)) },
            { typeof(string[]), GetMethod(nameof(ReadNullableStringArray)) }
        };

        MethodInfo GetMethod(string name) => typeof(ByteReader).GetMethod(name, BindingFlags.Instance | BindingFlags.Public)
                                                ?? throw new MemberAccessException("Unable to find read method: " + name + ".");
    }

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
        _buffer = bytes ?? throw new ArgumentNullException(nameof(bytes));
        _length = _buffer.Length;
        _streamMode = false;
        _index = 0;
        _position = 0;
        failure = false;
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
                    if (_length == 0)
                        goto fail;
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
    public byte[] ReadUInt8Array()
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
    public byte[] ReadZeroCompressedUInt8Array(bool @long = false)
    {
        int len = @long ? ReadInt32() : ReadUInt16();
        if (len == 0) return Array.Empty<byte>();
        byte[] output = new byte[len];
        for (int i = 0; i < len; ++i)
        {
            byte b = ReadUInt8();
            if (b == 255)
            {
                for (int j = ReadUInt8(); j > 0; --j)
                    output[i++] = ReadUInt8();
            }
            else i += b;
        }
        return output;
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
    public byte[]? ReadNullableUInt8Array()
    {
        if (!ReadBool()) return null;
        return ReadUInt8Array();
    }
    public byte[] ReadLongUInt8Array()
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
        return ReadLongUInt8Array();
    }
    public int ReadInt32() => !EnsureMoreLength(sizeof(int)) ? default : Read<int>();
    public int? ReadNullableInt32()
    {
        if (!ReadBool()) return null;
        return ReadInt32();
    }
    public uint ReadUInt32() => !EnsureMoreLength(sizeof(uint)) ? default : Read<uint>();
    public uint? ReadNullableUInt32()
    {
        if (!ReadBool()) return null;
        return ReadUInt32();
    }
    public byte ReadUInt8()
    {
        if (!EnsureMoreLength(1))
            return default;
        byte rtn = _buffer![_index];
        ++_index;
        return rtn;
    }
    public byte? ReadNullableUInt8()
    {
        if (!ReadBool()) return null;
        return ReadUInt8();
    }
    public sbyte ReadInt8()
    {
        if (!EnsureMoreLength(1))
            return default;
        sbyte rtn = unchecked((sbyte)_buffer![_index]);
        ++_index;
        return rtn;
    }
    public sbyte? ReadNullableInt8()
    {
        if (!ReadBool()) return null;
        return ReadInt8();
    }
    public bool ReadBool()
    {
        if (!EnsureMoreLength(1))
            return default;
        bool rtn = _buffer![_index] > 0;
        ++_index;
        return rtn;
    }
    public bool? ReadNullableBool()
    {
        if (!ReadBool()) return null;
        return ReadBool();
    }
    public long ReadInt64() => !EnsureMoreLength(sizeof(long)) ? default : Read<long>();
    public long? ReadNullableInt64()
    {
        if (!ReadBool()) return null;
        return ReadInt64();
    }
    public ulong ReadUInt64() => !EnsureMoreLength(sizeof(ulong)) ? default : Read<ulong>();
    public ulong? ReadNullableUInt64()
    {
        if (!ReadBool()) return null;
        return ReadUInt64();
    }
    public short ReadInt16() => !EnsureMoreLength(sizeof(short)) ? default : Read<short>();
    public int ReadInt24()
    {
        if (!EnsureMoreLength(3))
            return default;
        ushort sh = Read<ushort>();
        byte bt = _buffer![_index];
        ++_index;
        return (sh | (bt << 16)) - DevkitServerUtility.Int24MaxValue;
    }
    public uint ReadUInt24()
    {
        int i = ReadInt24();
        if (i < 0)
            return (uint)-(i - DevkitServerUtility.Int24MaxValue);
        return (uint)i;
    }
    public int? ReadNullableInt24()
    {
        if (!ReadBool()) return null;
        return ReadInt24();
    }
    public short? ReadNullableInt16()
    {
        if (!ReadBool()) return null;
        return ReadInt16();
    }
    public ushort ReadUInt16() => !EnsureMoreLength(sizeof(ushort)) ? default : Read<ushort>();
    public ushort? ReadNullableUInt16()
    {
        if (!ReadBool()) return null;
        return ReadUInt16();
    }
    public float ReadFloat() => !EnsureMoreLength(sizeof(float)) ? default : Read<float>();
    public float ReadHalfPrecisionFloat() => !EnsureMoreLength(sizeof(ushort)) ? default : Mathf.HalfToFloat(Read<ushort>());
    public float? ReadNullableFloat()
    {
        if (!ReadBool()) return null;
        return ReadFloat();
    }
    public decimal ReadDecimal() => !EnsureMoreLength(sizeof(decimal)) ? default : Read<decimal>();
    public decimal? ReadNullableDecimal()
    {
        if (!ReadBool()) return null;
        return ReadDecimal();
    }
    public double ReadDouble() => !EnsureMoreLength(sizeof(double)) ? default : Read<double>();
    public double? ReadNullableDouble()
    {
        if (!ReadBool()) return null;
        return ReadDouble();
    }
    public char ReadChar() => !EnsureMoreLength(sizeof(char)) ? default : Read<char>();
    public char? ReadNullableChar()
    {
        if (!ReadBool()) return null;
        return ReadChar();
    }
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
    public string ReadAsciiSmall()
    {
        bool hf = HasFailed;
        byte length = ReadUInt8();
        if (length == 0) return !hf && HasFailed ? null! : string.Empty;
        byte[]? ascii = ReadBlock(length);
        if (ascii != null)
            return System.Text.Encoding.ASCII.GetString(ascii);

        return null!;
    }
    public string? ReadNullableAsciiSmall()
    {
        if (!ReadBool()) return null;
        return ReadAsciiSmall();
    }
    public Type?[] ReadTypeArray()
    {
        int len = ReadUInt16();
        Type?[] rtn = new Type?[len];
        for (int i = 0; i < len; ++i)
        {
            rtn[i] = ReadType();
        }

        return rtn;
    }
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
    public string? ReadNullableString()
    {
        return !ReadBool() ? null : ReadString();
    }
    public string? ReadNullableShortString()
    {
        return !ReadBool() ? null : ReadShortString();
    }
    public DateTime ReadDateTime() => !EnsureMoreLength(sizeof(long)) ? default : DateTime.FromBinary(Read<long>());
    public DateTime? ReadNullableDateTime()
    {
        if (!ReadBool()) return null;
        return ReadDateTime();
    }
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
    public DateTimeOffset? ReadNullableDateTimeOffset()
    {
        if (!ReadBool()) return null;
        return ReadDateTimeOffset();
    }
    public unsafe TimeSpan ReadTimeSpan()
    {
        if (!EnsureMoreLength(sizeof(long)))
            return default;
        long ticks = Read<long>();
        return *(TimeSpan*)&ticks;
    }
    public TimeSpan? ReadNullableTimeSpan()
    {
        if (!ReadBool()) return null;
        return ReadTimeSpan();
    }
    public Guid ReadGuid() => !EnsureMoreLength(GuidSize) ? default : Read<Guid>();
    public Guid? ReadNullableGuid() => !ReadBool() ? null : ReadGuid();

    private static readonly Vector2 V2NaN = new Vector2(float.NaN, float.NaN);
    private static readonly Vector3 V3NaN = new Vector3(float.NaN, float.NaN, float.NaN);
    private static readonly Vector4 V4NaN = new Vector4(float.NaN, float.NaN, float.NaN, float.NaN);
    private static readonly Bounds BoundsNaN = new Bounds(V3NaN, V3NaN);
    private static readonly Color32 C32NaN = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);
    public Vector2 ReadVector2() => !EnsureMoreLength(sizeof(float) * 2) ? V2NaN : new Vector2(Read<float>(), Read<float>());
    public Vector2 ReadHalfPrecisionVector2() => !EnsureMoreLength(sizeof(ushort) * 2) ? V2NaN : new Vector2(Mathf.HalfToFloat(Read<ushort>()), Mathf.HalfToFloat(Read<ushort>()));
    public Vector2? ReadNullableVector2()
    {
        if (!ReadBool()) return null;
        return ReadVector2();
    }
    public Vector3 ReadVector3() => !EnsureMoreLength(sizeof(float) * 3) ? V3NaN : new Vector3(Read<float>(), Read<float>(), Read<float>());
    public Vector3 ReadHalfPrecisionVector3() => !EnsureMoreLength(sizeof(ushort) * 3)
        ? V3NaN
        : new Vector3(Mathf.HalfToFloat(Read<ushort>()), Mathf.HalfToFloat(Read<ushort>()), Mathf.HalfToFloat(Read<ushort>()));
    public Vector3? ReadNullableVector3()
    {
        if (!ReadBool()) return null;
        return ReadVector3();
    }
    public Vector4 ReadVector4() => !EnsureMoreLength(sizeof(float) * 4) ? V4NaN : new Vector4(Read<float>(), Read<float>(), Read<float>(), Read<float>());
    public Vector4 ReadHalfPrecisionVector4() => !EnsureMoreLength(sizeof(ushort) * 4)
        ? V4NaN
        : new Vector4(Mathf.HalfToFloat(Read<ushort>()), Mathf.HalfToFloat(Read<ushort>()), Mathf.HalfToFloat(Read<ushort>()), Mathf.HalfToFloat(Read<ushort>()));
    public Vector4? ReadNullableVector4()
    {
        if (!ReadBool()) return null;
        return ReadVector4();
    }
    public Bounds ReadBounds() =>
        !EnsureMoreLength(sizeof(float) * 6)
            ? BoundsNaN
            : new Bounds(new Vector3(Read<float>(), Read<float>(), Read<float>()), new Vector3(Read<float>(), Read<float>(), Read<float>()));
    public Bounds? ReadNullableBounds()
    {
        if (!ReadBool()) return null;
        return ReadBounds();
    }
    public Quaternion ReadQuaternion()
    {
        Vector4 f = ReadVector4();
        return new Quaternion(f.x, f.y, f.z, f.w);
    }
    public Quaternion ReadHalfPrecisionQuaternion()
    {
        Vector4 f = ReadHalfPrecisionVector4();
        return new Quaternion(f.x, f.y, f.z, f.w);
    }
    public Quaternion? ReadNullableQuaternion()
    {
        if (!ReadBool()) return null;
        return ReadQuaternion();
    }
    public Color ReadColor()
    {
        Vector4 f = ReadVector4();
        return new Color(f.y, f.z, f.w, f.x);
    }
    public Color? ReadNullableColor()
    {
        if (!ReadBool()) return null;
        return ReadColor();
    }
    public Color32 ReadColor32()
    {
        if (!EnsureMoreLength(4))
            return C32NaN;

        Color32 q = new Color32(_buffer![_index], _buffer[_index + 1], _buffer[_index + 2], _buffer[_index + 3]);

        _index += 4;
        return q;
    }
    public Color32? ReadNullableColor32()
    {
        return !ReadBool() ? null : ReadColor32();
    }
    public unsafe TEnum ReadEnum<TEnum>() where TEnum : unmanaged, Enum => !EnsureMoreLength(sizeof(TEnum)) ? default : Read<TEnum>();
    public TEnum? ReadNullableEnum<TEnum>() where TEnum : unmanaged, Enum => !ReadBool() ? null : ReadEnum<TEnum>();
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
    public int[] ReadZeroCompressedInt32Array(bool @long = false)
    {
        int len = @long ? ReadInt32() : ReadUInt16();
        if (len == 0) return Array.Empty<int>();
        int[] output = new int[len];
        for (int i = 0; i < len; ++i)
        {
            byte b = ReadUInt8();
            if (b == 255)
            {
                for (int j = ReadUInt8(); j > 0; --j)
                    output[i++] = ReadInt32();
            }
            else i += b;
        }
        return output;
    }
    public int[]? ReadNullableInt32Array() => !ReadBool() ? null : ReadInt32Array();
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
    public Guid[]? ReadNullableGuidArray() => !ReadBool() ? null : ReadGuidArray();
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
    public DateTime[]? ReadNullableDateTimeArray() => !ReadBool() ? null : ReadDateTimeArray();
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
    public DateTimeOffset[]? ReadNullableDateTimeOffsetArray() => !ReadBool() ? null : ReadDateTimeOffsetArray();
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
    public uint[] ReadZeroCompressedUInt32Array(bool @long = false)
    {
        int len = @long ? ReadInt32() : ReadUInt16();
        if (len == 0) return Array.Empty<uint>();
        uint[] output = new uint[len];
        for (int i = 0; i < len; ++i)
        {
            byte b = ReadUInt8();
            if (b == 255)
            {
                for (int j = ReadUInt8(); j > 0; --j)
                    output[i++] = ReadUInt32();
            }
            else i += b;
        }
        return output;
    }
    public uint[]? ReadNullableUInt32Array() => !ReadBool() ? null : ReadUInt32Array();
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
    public sbyte[] ReadZeroCompressedInt8Array(bool @long = false)
    {
        int len = @long ? ReadInt32() : ReadUInt16();
        if (len == 0) return Array.Empty<sbyte>();
        sbyte[] output = new sbyte[len];
        for (int i = 0; i < len; ++i)
        {
            byte b = ReadUInt8();
            if (b == 255)
            {
                for (int j = ReadUInt8(); j > 0; --j)
                    output[i++] = ReadInt8();
            }
            else i += b;
        }
        return output;
    }
    public sbyte[]? ReadNullableInt8Array() => !ReadBool() ? null : ReadInt8Array();
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
    public unsafe bool[] ReadLongBoolArray()
    {
        int len = ReadInt32();
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
    public bool[]? ReadNullableBoolArray() => !ReadBool() ? null : ReadBoolArray();
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
    public long[] ReadZeroCompressedInt64Array(bool @long = false)
    {
        int len = @long ? ReadInt32() : ReadUInt16();
        if (len == 0) return Array.Empty<long>();
        long[] output = new long[len];
        for (int i = 0; i < len; ++i)
        {
            byte b = ReadUInt8();
            if (b == 255)
            {
                for (int j = ReadUInt8(); j > 0; --j)
                    output[i++] = ReadInt64();
            }
            else i += b;
        }
        return output;
    }
    public long[]? ReadNullableInt64Array() => !ReadBool() ? null : ReadInt64Array();
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
    public ulong[] ReadZeroCompressedUInt64Array(bool @long = false)
    {
        int len = @long ? ReadInt32() : ReadUInt16();
        if (len == 0) return Array.Empty<ulong>();
        ulong[] output = new ulong[len];
        for (int i = 0; i < len; ++i)
        {
            byte b = ReadUInt8();
            if (b == 255)
            {
                for (int j = ReadUInt8(); j > 0; --j)
                    output[i++] = ReadUInt64();
            }
            else i += b;
        }
        return output;
    }
    public ulong[]? ReadNullableUInt64Array() => !ReadBool() ? null : ReadUInt64Array();
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
    public short[] ReadZeroCompressedInt16Array(bool @long = false)
    {
        int len = @long ? ReadInt32() : ReadUInt16();
        if (len == 0) return Array.Empty<short>();
        short[] output = new short[len];
        for (int i = 0; i < len; ++i)
        {
            byte b = ReadUInt8();
            if (b == 255)
            {
                for (int j = ReadUInt8(); j > 0; --j)
                    output[i++] = ReadInt16();
            }
            else i += b;
        }
        return output;
    }
    public short[]? ReadNullableInt16Array() => !ReadBool() ? null : ReadInt16Array();
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
    public ushort[] ReadZeroCompressedUInt16Array(bool @long = false)
    {
        int len = @long ? ReadInt32() : ReadUInt16();
        if (len == 0) return Array.Empty<ushort>();
        ushort[] output = new ushort[len];
        for (int i = 0; i < len; ++i)
        {
            byte b = ReadUInt8();
            if (b == 255)
            {
                for (int j = ReadUInt8(); j > 0; --j)
                    output[i++] = ReadUInt16();
            }
            else i += b;
        }
        return output;
    }
    public ushort[]? ReadNullableUInt16Array() => !ReadBool() ? null : ReadUInt16Array();
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
    public float[]? ReadNullableFloatArray() => !ReadBool() ? null : ReadFloatArray();
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
    public decimal[]? ReadNullableDecimalArray() => !ReadBool() ? null : ReadDecimalArray();
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
    public double[]? ReadNullableDoubleArray() => !ReadBool() ? null : ReadDoubleArray();
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
    public char[]? ReadNullableCharArray() => !ReadBool() ? null : ReadCharArray();
    public string[] ReadStringArray()
    {
        string[] rtn = new string[ReadUInt16()];
        for (int i = 0; i < rtn.Length; i++)
            rtn[i] = ReadString();
        return rtn;
    }
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
        MethodInfo? method = GetReadMethod(type, isNullable);
        if (method == null)
            throw new InvalidDynamicTypeException(type, -1, true);
        try
        {
            return method.CreateDelegate(typeof(Reader<>).MakeGenericType(type));
        }
        catch (ArgumentException ex)
        {
            Logger.LogError("Failed to create reader delegate for type " + type.FullName);
            Logger.LogError(ex);
            return null;
        }
    }
    public static MethodInfo? GetReadMethod(Type type, bool isNullable = false)
    {
        MethodInfo? method;
        if (type.IsEnum)
        {
            method = (isNullable ? ReadNullableEnumMethod : ReadEnumMethod).MakeGenericMethod(type);
        }
        else
        {
            if (_nullableReaders == null || _nonNullableReaders == null)
                PrepareMethods();
            if (isNullable)
            {
                _nullableReaders!.TryGetValue(type, out method);
            }
            else if (!_nonNullableReaders!.TryGetValue(type, out method) && type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
                _nullableReaders!.TryGetValue(type, out method);
        }

        return method;
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
public sealed class ByteReaderRaw<T1, T2, T3, T4, T5> : ByteReader
{
    private readonly Reader<T1> reader1;
    private readonly Reader<T2> reader2;
    private readonly Reader<T3> reader3;
    private readonly Reader<T4> reader4;
    private readonly Reader<T5> reader5;
    /// <summary>Leave any reader null to auto-fill.</summary>
    public ByteReaderRaw(Reader<T1>? reader1, Reader<T2>? reader2, Reader<T3>? reader3, Reader<T4>? reader4, Reader<T5>? reader5)
    {
        this.reader1 = reader1 ?? ReaderHelper<T1>.Reader!;
        this.reader2 = reader2 ?? ReaderHelper<T2>.Reader!;
        this.reader3 = reader3 ?? ReaderHelper<T3>.Reader!;
        this.reader4 = reader4 ?? ReaderHelper<T4>.Reader!;
        this.reader5 = reader5 ?? ReaderHelper<T5>.Reader!;
    }
    public bool Read(byte[]? bytes, out T1 arg1, out T2 arg2, out T3 arg3, out T4 arg4, out T5 arg5)
    {
        if (bytes != null)
            LoadNew(bytes);
        arg1 = reader1.Invoke(this);
        arg2 = reader2.Invoke(this);
        arg3 = reader3.Invoke(this);
        arg4 = reader4.Invoke(this);
        arg5 = reader5.Invoke(this);
        return !HasFailed;
    }
}
public sealed class ByteReaderRaw<T1, T2, T3, T4, T5, T6> : ByteReader
{
    private readonly Reader<T1> reader1;
    private readonly Reader<T2> reader2;
    private readonly Reader<T3> reader3;
    private readonly Reader<T4> reader4;
    private readonly Reader<T5> reader5;
    private readonly Reader<T6> reader6;
    /// <summary>Leave any reader null to auto-fill.</summary>
    public ByteReaderRaw(Reader<T1>? reader1, Reader<T2>? reader2, Reader<T3>? reader3, Reader<T4>? reader4, Reader<T5>? reader5, Reader<T6>? reader6)
    {
        this.reader1 = reader1 ?? ReaderHelper<T1>.Reader!;
        this.reader2 = reader2 ?? ReaderHelper<T2>.Reader!;
        this.reader3 = reader3 ?? ReaderHelper<T3>.Reader!;
        this.reader4 = reader4 ?? ReaderHelper<T4>.Reader!;
        this.reader5 = reader5 ?? ReaderHelper<T5>.Reader!;
        this.reader6 = reader6 ?? ReaderHelper<T6>.Reader!;
    }
    public bool Read(byte[]? bytes, out T1 arg1, out T2 arg2, out T3 arg3, out T4 arg4, out T5 arg5, out T6 arg6)
    {
        if (bytes != null)
            LoadNew(bytes);
        arg1 = reader1.Invoke(this);
        arg2 = reader2.Invoke(this);
        arg3 = reader3.Invoke(this);
        arg4 = reader4.Invoke(this);
        arg5 = reader5.Invoke(this);
        arg6 = reader6.Invoke(this);
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
