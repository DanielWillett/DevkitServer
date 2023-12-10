using DevkitServer.API;
using DevkitServer.Models;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;

// ReSharper disable AssignNullToNotNullAttribute

namespace DevkitServer.Util.Encoding;

/// <summary>
/// Fast decoding from a byte array to data. Has experimental support for <see cref="System.IO.Stream"/>s. Similar to the <see cref="River"/> class.
/// </summary>
public class ByteReader
{
    private const string InvalidZeroCompressedMessage = "Invalid zero-compressed format.";

    private const int MinStreamBufferSize = 32;
    private const int GuidSize = 16;

    private static readonly bool IsBigEndian = !BitConverter.IsLittleEndian;
    private static Dictionary<Type, MethodInfo>? _nonNullableReaders;
    private static Dictionary<Type, MethodInfo>? _nullableReaders;
    private static readonly MethodInfo ReadEnumMethod = typeof(ByteReader).GetMethod(nameof(ReadEnum), BindingFlags.Instance | BindingFlags.Public)
                                                         ?? throw new MemberAccessException("Unable to find read enum method.");
    private static readonly MethodInfo ReadNullableEnumMethod = typeof(ByteReader).GetMethod(nameof(ReadNullableEnum), BindingFlags.Instance | BindingFlags.Public)
                                                                 ?? throw new MemberAccessException("Unable to find read nullable enum method.");

    private byte[]? _buffer;
    private Stream? _stream;
    private int _index;
    private bool _hasFailed;
    private bool _streamMode;
    private bool _streamLengthSupport;
    private int _position;
    private int _length;

    /// <summary>
    /// Internal buffer. In stream mode not very useful, but in buffer mode can be used to get the data without allocating a new array. Use <see cref="Position"/> for the length.
    /// </summary>
    public byte[]? InternalBuffer { get => _buffer; set => _buffer = value; }

    /// <summary>
    /// Stream to read from. Setter does not reset the buffer, recommnded to use <see cref="LoadNew(Stream)"/> instead.
    /// </summary>
    /// <remarks>Stream mode is still in beta.</remarks>
    public Stream? Stream
    {
        get => _stream;
        private set
        {
            if (value is not null && !value.CanRead)
                throw new ArgumentException("Stream must be able to read.", nameof(value));
            _stream = value;
        }
    }

    /// <summary>
    /// If the reader has failed yet. Useful if <see cref="ThrowOnError"/> is set to <see langword="false"/>.
    /// </summary>
    public bool HasFailed => _hasFailed;

    /// <summary>
    /// Index of the read. In buffer mode it represents the length of data in the buffer, in stream mode it represents the position of the stream minus what hasn't been read from the buffer.
    /// </summary>
    public int Position => _streamMode ? (_position - (_length - _index)) : _index;

    /// <summary>
    /// Number of bytes left in the stream or buffer.
    /// </summary>
    public int BytesLeft => _streamMode ? (_streamLengthSupport ? (int)Math.Min(_stream!.Length - _stream!.Position, int.MaxValue) : (_buffer is not null ? _buffer.Length - _index : 0)) : _buffer!.Length - _index;

    /// <summary>
    /// When <see langword="true"/>, will throw a <see cref="ByteEncoderException"/> or <see cref="ByteBufferOverflowException"/> when there's a read failure. Otherwise it will just set <see cref="HasFailed"/> to <see langword="true"/>.
    /// </summary>
    public bool ThrowOnError { get; set; } = true;

    /// <summary>
    /// When <see langword="true"/>, will log a warning when there's a read failure. Otherwise it will just set <see cref="HasFailed"/> to <see langword="true"/>.
    /// </summary>
    public bool LogOnError { get; set; } = true;

    /// <summary>
    /// Influences the size of the buffer in stream mode (how much is read at once).
    /// </summary>
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
            { typeof(Type), typeof(ByteReader).GetMethod(nameof(ReadType), BindingFlags.Instance | BindingFlags.Public, null, CallingConventions.Any, Type.EmptyTypes, null) },
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
            { typeof(NetId), GetMethod(nameof(ReadNetId)) },
        };

        _nullableReaders ??= new Dictionary<Type, MethodInfo>(44)
        {
            { typeof(int?), GetMethod(nameof(ReadNullableInt32)) },
            { typeof(uint?), GetMethod(nameof(ReadNullableUInt32)) },
            { typeof(byte?), GetMethod(nameof(ReadNullableUInt8)) },
            { typeof(sbyte?), GetMethod(nameof(ReadNullableInt8)) },
            { typeof(bool?), GetMethod(nameof(ReadNullableBool)) },
            { typeof(long?), GetMethod(nameof(ReadNullableInt64)) },
            { typeof(ulong?), GetMethod(nameof(ReadNullableUInt64)) },
            { typeof(short?), GetMethod(nameof(ReadNullableInt16)) },
            { typeof(ushort?), GetMethod(nameof(ReadNullableUInt16)) },
            { typeof(float?), GetMethod(nameof(ReadNullableFloat)) },
            { typeof(decimal?), GetMethod(nameof(ReadNullableDecimal)) },
            { typeof(double?), GetMethod(nameof(ReadNullableDouble)) },
            { typeof(char?), GetMethod(nameof(ReadNullableChar)) },
            { typeof(string), GetMethod(nameof(ReadNullableString)) },
            { typeof(Type), typeof(ByteReader).GetMethod(nameof(ReadType), BindingFlags.Instance | BindingFlags.Public, null, CallingConventions.Any, Type.EmptyTypes, null) },
            { typeof(Type[]), GetMethod(nameof(ReadTypeArray)) },
            { typeof(DateTime?), GetMethod(nameof(ReadNullableDateTime)) },
            { typeof(DateTimeOffset?), GetMethod(nameof(ReadNullableDateTimeOffset)) },
            { typeof(TimeSpan?), GetMethod(nameof(ReadNullableTimeSpan)) },
            { typeof(Guid?), GetMethod(nameof(ReadNullableGuid)) },
            { typeof(Vector2?), GetMethod(nameof(ReadNullableVector2)) },
            { typeof(Vector3?), GetMethod(nameof(ReadNullableVector3)) },
            { typeof(Vector4?), GetMethod(nameof(ReadNullableVector4)) },
            { typeof(Bounds?), GetMethod(nameof(ReadNullableBounds)) },
            { typeof(Quaternion?), GetMethod(nameof(ReadNullableQuaternion)) },
            { typeof(Color?), GetMethod(nameof(ReadNullableColor)) },
            { typeof(Color32?), GetMethod(nameof(ReadNullableColor32)) },
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
            { typeof(string[]), GetMethod(nameof(ReadNullableStringArray)) },
            { typeof(NetId?), GetMethod(nameof(ReadNullableNetId)) }
        };

        MethodInfo GetMethod(string name) => typeof(ByteReader).GetMethod(name, BindingFlags.Instance | BindingFlags.Public)
                                             ?? throw new MemberAccessException("Unable to find read method: " + name + ".");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetStreamBufferLength() => !_streamLengthSupport
        ? StreamBufferSize
        : (int)Math.Min(StreamBufferSize, Math.Max(_stream!.Length - _stream.Position, MinStreamBufferSize));

    private void Fail(string message)
    {
        _hasFailed = true;
        if (LogOnError)
            Logger.LogWarning(message, method: "BYTE READER");
        if (ThrowOnError)
            throw new ByteEncoderException(message);
    }
    private void Overflow(string message)
    {
        _hasFailed = true;
        if (LogOnError)
            Logger.LogWarning(message, method: "BYTE READER");
        if (ThrowOnError)
            throw new ByteBufferOverflowException(message);
    }

    /// <summary>
    /// Loads a <see cref="System.IO.Stream"/> to be read from. Stream must be able to read.
    /// </summary>
    /// <remarks>Seek the stream to where you want to start before passing it here. Stream mode is still in beta.</remarks>
    /// <exception cref="ArgumentNullException"/>
    public void LoadNew(Stream stream)
    {
        _hasFailed = false;
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

    /// <summary>
    /// Loads a new byte array to be read from with an offset.
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
    /// <exception cref="ArgumentOutOfRangeException"/>
    public void LoadNew(byte[] bytes, int index)
    {
        if (index >= bytes.Length)
            throw new ArgumentOutOfRangeException(nameof(index));
        if (index < 0)
            index = 0;
        LoadNew(bytes);
        if (index > 0)
            Skip(index);
    }
    /// <summary>
    /// Loads a new byte array to be read from.
    /// </summary>
    /// <exception cref="ArgumentNullException"/>
    public void LoadNew(ArraySegment<byte> bytes)
    {
        _buffer = bytes.Array ?? throw new ArgumentNullException(nameof(bytes));
        _length = bytes.Count;
        _streamMode = false;
        _index = bytes.Offset;
        _position = bytes.Offset;
        _hasFailed = false;
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
    private static unsafe void EndianCheck(byte* litEndStrt, int size)
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
    
    protected unsafe bool EnsureMoreLength(int byteCt)
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
                if (byteCt <= _buffer.Length) // space for remaining and needed bytes in a new buffer
                {
                    if (remaining != 0)
                    {
                        fixed (byte* ptr = _buffer)
                            Buffer.MemoryCopy(ptr + _index, ptr, _buffer.Length, remaining);
                    }
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
        Overflow("Failed to read " + byteCt.ToString(CultureInfo.InvariantCulture) +
                    " B at offset " + _index.ToString(CultureInfo.InvariantCulture) + " / " + _length.ToString(CultureInfo.InvariantCulture) + ".");
        return false;
    }

    /// <summary>
    /// Reads a byte array of length <paramref name="length"/>, without reading a length.
    /// </summary>
    public byte[] ReadBlock(int length)
    {
        if (_streamMode)
        {
            byte[] output = new byte[length];
            if (_buffer is { Length: > 0 })
            {
                if (_buffer.Length - _index >= length)
                {
                    Buffer.BlockCopy(_buffer, _index, output, 0, length);
                    _index += length;
                    return output;
                }

                int offset = _length - _index;
                _index = _buffer.Length;
                Buffer.BlockCopy(_buffer, _index, output, 0, offset);
                length = Stream!.Read(output, offset, length - offset);
                _position += length;
                if (length < output.Length)
                {
                    Overflow("Failed to read " + length.ToString(CultureInfo.InvariantCulture) +
                             " B at offset " + _index.ToString(CultureInfo.InvariantCulture) + " / " + _length.ToString(CultureInfo.InvariantCulture) + ".");
                }
                return output;
            }

            length = Stream!.Read(output, 0, length);
            _position += length;
            if (length < output.Length)
            {
                Overflow("Failed to read " + length.ToString(CultureInfo.InvariantCulture) +
                         " B at offset " + _index.ToString(CultureInfo.InvariantCulture) + " / " + _length.ToString(CultureInfo.InvariantCulture) + ".");
            }
            return output;
        }

        if (!EnsureMoreLength(length)) return null!;
        byte[] rtn = new byte[length];
        Buffer.BlockCopy(_buffer, _index, rtn, 0, length);
        _index += length;
        return rtn;
    }

    /// <summary>
    /// Reads a generic unmanaged struct. Not recommended as there are no endianness checks, only use for local storage.
    /// </summary>
    public unsafe T ReadStruct<T>() where T : unmanaged
    {
        return !EnsureMoreLength(sizeof(T)) ? default : Read<T>();
    }

    /// <summary>
    /// Reads a byte array and its length (as a UInt16).
    /// </summary>
    public byte[] ReadUInt8Array()
    {
        ushort length = ReadUInt16();
        return length == 0 ? Array.Empty<byte>() : ReadBlock(length);
    }

    /// <summary>
    /// Reads a byte array which compresses repeating zero elements.
    /// </summary>
    /// <param name="long">Write length as an Int32 instead of UInt16.</param>
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
                byte next;
                for (int j = ReadUInt8(); j > 0; --j)
                {
                    next = ReadUInt8();
                    if (i < len)
                        output[i] = next;
                    else
                        Fail(InvalidZeroCompressedMessage);
                    ++i;
                }
                next = ReadUInt8();
                if (i < len)
                    output[i] = next;
                else
                    Fail(InvalidZeroCompressedMessage);
            }
            else
            {
                i += b;
                if (i >= len)
                    Fail(InvalidZeroCompressedMessage);
            }
        }
        return output;
    }

    /// <summary>
    /// Skips a certain number of bytes without reading them.
    /// </summary>
    public void Skip(int bytes)
    {
        EnsureMoreLength(bytes);
        _index += bytes;
    }

    /// <summary>
    /// Goto an index in the buffer.
    /// </summary>
    /// <exception cref="NotSupportedException">Not supported in stream mode.</exception>
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

    /// <summary>
    /// Reads a byte array that can be null. Length is read as a UInt16.
    /// </summary>
    public byte[]? ReadNullableUInt8Array()
    {
        if (!ReadBool()) return null;
        return ReadUInt8Array();
    }

    /// <summary>
    /// Reads a byte array. Length is read as a Int32.
    /// </summary>
    public byte[] ReadLongUInt8Array()
    {
        int length = ReadInt32();
        if (length == 0) return Array.Empty<byte>();
        return ReadBlock(length);
    }

    /// <summary>
    /// Reads a byte array that can be null. Length is read as a Int32.
    /// </summary>
    public byte[]? ReadNullableLongBytes()
    {
        if (!ReadBool()) return null;
        return ReadLongUInt8Array();
    }

    /// <summary>
    /// Reads an <see cref="int"/> from the buffer.
    /// </summary>
    public int ReadInt32() => !EnsureMoreLength(sizeof(int)) ? default : Read<int>();

    /// <summary>
    /// Reads an <see cref="int?"/> from the buffer.
    /// </summary>
    public int? ReadNullableInt32()
    {
        if (!ReadBool()) return null;
        return ReadInt32();
    }

    /// <summary>
    /// Reads a <see cref="uint"/> from the buffer.
    /// </summary>
    public uint ReadUInt32() => !EnsureMoreLength(sizeof(uint)) ? default : Read<uint>();

    /// <summary>
    /// Reads a <see cref="uint?"/> from the buffer.
    /// </summary>
    public uint? ReadNullableUInt32()
    {
        if (!ReadBool()) return null;
        return ReadUInt32();
    }

    /// <summary>
    /// Reads a <see cref="NetId"/> (<see cref="uint"/>) from the buffer.
    /// </summary>
    public NetId ReadNetId() => !EnsureMoreLength(sizeof(uint)) ? default : new NetId(Read<uint>());

    /// <summary>
    /// Reads a <see cref="NetId?"/> (<see cref="uint?"/>) from the buffer.
    /// </summary>
    public NetId? ReadNullableNetId()
    {
        if (!ReadBool()) return null;
        return new NetId(ReadUInt32());
    }

    /// <summary>
    /// Reads a <see cref="byte"/> from the buffer.
    /// </summary>
    public byte ReadUInt8()
    {
        if (!EnsureMoreLength(1))
            return default;
        byte rtn = _buffer![_index];
        ++_index;
        return rtn;
    }

    /// <summary>
    /// Reads a <see cref="byte?"/> from the buffer.
    /// </summary>
    public byte? ReadNullableUInt8()
    {
        if (!ReadBool()) return null;
        return ReadUInt8();
    }

    /// <summary>
    /// Reads a <see cref="sbyte"/> from the buffer.
    /// </summary>
    public sbyte ReadInt8()
    {
        if (!EnsureMoreLength(1))
            return default;
        sbyte rtn = unchecked((sbyte)_buffer![_index]);
        ++_index;
        return rtn;
    }

    /// <summary>
    /// Reads a <see cref="sbyte?"/> from the buffer.
    /// </summary>
    public sbyte? ReadNullableInt8()
    {
        if (!ReadBool()) return null;
        return ReadInt8();
    }

    /// <summary>
    /// Reads a <see cref="bool"/> from the buffer.
    /// </summary>
    public bool ReadBool()
    {
        if (!EnsureMoreLength(1))
            return default;
        bool rtn = _buffer![_index] > 0;
        ++_index;
        return rtn;
    }

    /// <summary>
    /// Reads a <see cref="bool?"/> from the buffer.
    /// </summary>
    public bool? ReadNullableBool()
    {
        if (!ReadBool()) return null;
        return ReadBool();
    }

    /// <summary>
    /// Reads a <see cref="long"/> from the buffer.
    /// </summary>
    public long ReadInt64() => !EnsureMoreLength(sizeof(long)) ? default : Read<long>();

    /// <summary>
    /// Reads a <see cref="long?"/> from the buffer.
    /// </summary>
    public long? ReadNullableInt64()
    {
        if (!ReadBool()) return null;
        return ReadInt64();
    }

    /// <summary>
    /// Reads a <see cref="ulong"/> from the buffer.
    /// </summary>
    public ulong ReadUInt64() => !EnsureMoreLength(sizeof(ulong)) ? default : Read<ulong>();

    /// <summary>
    /// Reads a <see cref="ulong?"/> from the buffer.
    /// </summary>
    public ulong? ReadNullableUInt64()
    {
        if (!ReadBool()) return null;
        return ReadUInt64();
    }

    /// <summary>
    /// Reads a <see cref="short"/> from the buffer.
    /// </summary>
    public short ReadInt16() => !EnsureMoreLength(sizeof(short)) ? default : Read<short>();

    /// <summary>
    /// Reads a 24-bit (3 byte) <see cref="int"/> from the buffer.
    /// </summary>
    /// <remarks>Range: <seealso cref="EncodingEx.Int24MinValue"/>-<seealso cref="EncodingEx.Int24MaxValue"/>.</remarks>
    public int ReadInt24()
    {
        if (!EnsureMoreLength(3))
            return default;
        ushort sh = Read<ushort>();
        byte bt = _buffer![_index];
        ++_index;
        return (sh | (bt << 16)) - EncodingEx.Int24MaxValue;
    }

    /// <summary>
    /// Reads a 24-bit (3 byte) <see cref="uint"/> from the buffer.
    /// </summary>
    /// <remarks>Range: 0-2*<seealso cref="EncodingEx.Int24MinValue"/>.</remarks>
    public uint ReadUInt24()
    {
        int i = ReadInt24();
        if (i < 0)
            return (uint)-(i - EncodingEx.Int24MaxValue);
        return (uint)i;
    }

    /// <summary>
    /// Reads a 24-bit (3 byte) <see cref="int?"/> from the buffer.
    /// </summary>
    /// <remarks>Range: <seealso cref="EncodingEx.Int24MinValue"/>-<seealso cref="EncodingEx.Int24MaxValue"/>.</remarks>
    public int? ReadNullableInt24()
    {
        if (!ReadBool()) return null;
        return ReadInt24();
    }

    /// <summary>
    /// Reads a <see cref="short?"/> from the buffer.
    /// </summary>
    public short? ReadNullableInt16()
    {
        if (!ReadBool()) return null;
        return ReadInt16();
    }

    /// <summary>
    /// Reads a <see cref="ushort"/> from the buffer.
    /// </summary>
    public ushort ReadUInt16() => !EnsureMoreLength(sizeof(ushort)) ? default : Read<ushort>();

    /// <summary>
    /// Reads a <see cref="ushort?"/> from the buffer.
    /// </summary>
    public ushort? ReadNullableUInt16()
    {
        if (!ReadBool()) return null;
        return ReadUInt16();
    }

    /// <summary>
    /// Reads a <see cref="float"/> from the buffer.
    /// </summary>
    public float ReadFloat() => !EnsureMoreLength(sizeof(float)) ? default : Read<float>();

    /// <summary>
    /// Reads a half-precision <see cref="float"/> from the buffer (using <see cref="Mathf.HalfToFloat"/>).
    /// </summary>
    public float ReadHalfPrecisionFloat() => !EnsureMoreLength(sizeof(ushort)) ? default : Mathf.HalfToFloat(Read<ushort>());

    /// <summary>
    /// Reads a <see cref="float?"/> from the buffer.
    /// </summary>
    public float? ReadNullableFloat()
    {
        if (!ReadBool()) return null;
        return ReadFloat();
    }

    /// <summary>
    /// Reads a <see cref="decimal"/> from the buffer.
    /// </summary>
    public decimal ReadDecimal() => !EnsureMoreLength(sizeof(decimal)) ? default : Read<decimal>();

    /// <summary>
    /// Reads a <see cref="decimal?"/> from the buffer.
    /// </summary>
    public decimal? ReadNullableDecimal()
    {
        if (!ReadBool()) return null;
        return ReadDecimal();
    }

    /// <summary>
    /// Reads a <see cref="double"/> from the buffer.
    /// </summary>
    public double ReadDouble() => !EnsureMoreLength(sizeof(double)) ? default : Read<double>();

    /// <summary>
    /// Reads a <see cref="double?"/> from the buffer.
    /// </summary>
    public double? ReadNullableDouble()
    {
        if (!ReadBool()) return null;
        return ReadDouble();
    }

    /// <summary>
    /// Reads a <see cref="char"/> from the buffer.
    /// </summary>
    public char ReadChar() => !EnsureMoreLength(sizeof(char)) ? default : Read<char>();

    /// <summary>
    /// Reads a <see cref="char?"/> from the buffer.
    /// </summary>
    public char? ReadNullableChar()
    {
        if (!ReadBool()) return null;
        return ReadChar();
    }

    /// <summary>
    /// Reads a <see cref="Type"/> array from the buffer with nullable elements.
    /// </summary>
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

    /// <summary>
    /// Follows the template for reading a type but only outputs the string value with assembly info prepended <see cref="Type"/>.
    /// </summary>
    public string? ReadTypeInfo() => ReadTypeInfo(out _);

    /// <summary>
    /// Follows the template for reading a type but only outputs the string value with assembly info prepended <see cref="Type"/>.
    /// </summary>
    /// <remarks>Also outputs the internal flag used for compression.</remarks>
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

    /// <summary>
    /// Reads a nullable <see cref="Type"/> from the buffer.
    /// </summary>
    public Type? ReadType() => ReadType(out _);

    /// <summary>
    /// Reads a nullable <see cref="Type"/> from the buffer.
    /// </summary>
    /// <param name="wasPassedNull"><see langword="True"/> if the written type was <see langword="null"/>, otherwise the type was just not found.</param>
    public Type? ReadType(out bool wasPassedNull)
    {
        const string nsSdgUnturned = "SDG.Unturned";
        const string nsSdgFrameworkDevkit = "SDG.Framework.Devkit";
        const string nsSdgFramework = "SDG.Framework";
        const string nsDevkitServer = "DevkitServer";
        const string nsSystem = "System";
        wasPassedNull = false;
        byte flag = ReadUInt8();
        if ((flag & 128) != 0)
        {
            wasPassedNull = true;
            return null;
        }

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

    /// <summary>
    /// Reads a <see cref="string"/> from the buffer.
    /// </summary>
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

    /// <summary>
    /// Reads a nullable <see cref="string"/> from the buffer.
    /// </summary>
    public string? ReadNullableString()
    {
        return !ReadBool() ? null : ReadString();
    }

    /// <summary>
    /// Reads an ASCII <see cref="string"/> from the buffer.
    /// </summary>
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

    /// <summary>
    /// Reads a nullable ASCII <see cref="string"/> from the buffer.
    /// </summary>
    public string? ReadNullableAsciiSmall()
    {
        if (!ReadBool()) return null;
        return ReadAsciiSmall();
    }

    /// <summary>
    /// Reads a <see cref="string"/> from the buffer with a max length of <see cref="byte.MaxValue"/>.
    /// </summary>
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

    /// <summary>
    /// Reads a nullable <see cref="string"/> from the buffer with a max length of <see cref="byte.MaxValue"/>.
    /// </summary>
    public string? ReadNullableShortString()
    {
        return !ReadBool() ? null : ReadShortString();
    }

    /// <summary>
    /// Reads a <see cref="DateTime"/> from the buffer. Keeps <see cref="DateTimeKind"/> information.
    /// </summary>
    public DateTime ReadDateTime() => !EnsureMoreLength(sizeof(long)) ? default : DateTime.FromBinary(Read<long>());

    /// <summary>
    /// Reads a <see cref="DateTime?"/> from the buffer. Keeps <see cref="DateTimeKind"/> information.
    /// </summary>
    public DateTime? ReadNullableDateTime()
    {
        if (!ReadBool()) return null;
        return ReadDateTime();
    }

    /// <summary>
    /// Reads a <see cref="DateTimeOffset"/> from the buffer. Keeps <see cref="DateTimeKind"/> and offset information.
    /// </summary>
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

    /// <summary>
    /// Reads a <see cref="DateTimeOffset?"/> from the buffer. Keeps <see cref="DateTimeKind"/> and offset information.
    /// </summary>
    public DateTimeOffset? ReadNullableDateTimeOffset()
    {
        if (!ReadBool()) return null;
        return ReadDateTimeOffset();
    }

    /// <summary>
    /// Reads a <see cref="TimeSpan"/> from the buffer.
    /// </summary>
    public unsafe TimeSpan ReadTimeSpan()
    {
        if (!EnsureMoreLength(sizeof(long)))
            return default;
        long ticks = Read<long>();
        return *(TimeSpan*)&ticks;
    }

    /// <summary>
    /// Reads a <see cref="TimeSpan?"/> from the buffer.
    /// </summary>
    public TimeSpan? ReadNullableTimeSpan()
    {
        if (!ReadBool()) return null;
        return ReadTimeSpan();
    }

    /// <summary>
    /// Reads a <see cref="Guid"/> from the buffer.
    /// </summary>
    public Guid ReadGuid() => !EnsureMoreLength(GuidSize) ? default : Read<Guid>();

    /// <summary>
    /// Reads a <see cref="Guid?"/> from the buffer.
    /// </summary>
    public Guid? ReadNullableGuid() => !ReadBool() ? null : ReadGuid();

    private static readonly Vector2 V2NaN = new Vector2(float.NaN, float.NaN);
    private static readonly Vector3 V3NaN = new Vector3(float.NaN, float.NaN, float.NaN);
    private static readonly Vector4 V4NaN = new Vector4(float.NaN, float.NaN, float.NaN, float.NaN);
    private static readonly Bounds BoundsNaN = new Bounds(V3NaN, V3NaN);
    private static readonly Color32 C32NaN = new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);

    /// <summary>
    /// Reads a <see cref="Vector2"/> from the buffer.
    /// </summary>
    public Vector2 ReadVector2() => !EnsureMoreLength(sizeof(float) * 2) ? V2NaN : new Vector2(Read<float>(), Read<float>());

    /// <summary>
    /// Reads a half-precision <see cref="Vector2"/> from the buffer.
    /// </summary>
    public Vector2 ReadHalfPrecisionVector2() => !EnsureMoreLength(sizeof(ushort) * 2) ? V2NaN : new Vector2(Mathf.HalfToFloat(Read<ushort>()), Mathf.HalfToFloat(Read<ushort>()));

    /// <summary>
    /// Reads a <see cref="Vector2?"/> from the buffer.
    /// </summary>
    public Vector2? ReadNullableVector2()
    {
        if (!ReadBool()) return null;
        return ReadVector2();
    }

    /// <summary>
    /// Reads a <see cref="Vector3"/> from the buffer.
    /// </summary>
    public Vector3 ReadVector3() => !EnsureMoreLength(sizeof(float) * 3) ? V3NaN : new Vector3(Read<float>(), Read<float>(), Read<float>());

    /// <summary>
    /// Reads a half-precision <see cref="Vector3"/> from the buffer.
    /// </summary>
    public Vector3 ReadHalfPrecisionVector3() => !EnsureMoreLength(sizeof(ushort) * 3)
        ? V3NaN
        : new Vector3(Mathf.HalfToFloat(Read<ushort>()), Mathf.HalfToFloat(Read<ushort>()), Mathf.HalfToFloat(Read<ushort>()));

    /// <summary>
    /// Reads a <see cref="Vector3?"/> from the buffer.
    /// </summary>
    public Vector3? ReadNullableVector3()
    {
        if (!ReadBool()) return null;
        return ReadVector3();
    }

    /// <summary>
    /// Reads a <see cref="Vector4"/> from the buffer.
    /// </summary>
    public Vector4 ReadVector4() => !EnsureMoreLength(sizeof(float) * 4) ? V4NaN : new Vector4(Read<float>(), Read<float>(), Read<float>(), Read<float>());

    /// <summary>
    /// Reads a half-precision <see cref="Vector4"/> from the buffer.
    /// </summary>
    public Vector4 ReadHalfPrecisionVector4() => !EnsureMoreLength(sizeof(ushort) * 4)
        ? V4NaN
        : new Vector4(Mathf.HalfToFloat(Read<ushort>()), Mathf.HalfToFloat(Read<ushort>()), Mathf.HalfToFloat(Read<ushort>()), Mathf.HalfToFloat(Read<ushort>()));

    /// <summary>
    /// Reads a <see cref="Vector4?"/> from the buffer.
    /// </summary>
    public Vector4? ReadNullableVector4()
    {
        if (!ReadBool()) return null;
        return ReadVector4();
    }

    /// <summary>
    /// Reads a <see cref="Bounds"/> from the buffer.
    /// </summary>
    public Bounds ReadBounds() =>
        !EnsureMoreLength(sizeof(float) * 6)
            ? BoundsNaN
            : new Bounds(new Vector3(Read<float>(), Read<float>(), Read<float>()), new Vector3(Read<float>(), Read<float>(), Read<float>()));

    /// <summary>
    /// Reads a <see cref="Bounds?"/> from the buffer.
    /// </summary>
    public Bounds? ReadNullableBounds()
    {
        if (!ReadBool()) return null;
        return ReadBounds();
    }

    /// <summary>
    /// Reads a <see cref="Quaternion"/> from the buffer.
    /// </summary>
    public Quaternion ReadQuaternion()
    {
        Vector4 f = ReadVector4();
        return new Quaternion(f.x, f.y, f.z, f.w);
    }

    /// <summary>
    /// Reads a half-precision <see cref="Quaternion"/> from the buffer.
    /// </summary>
    public Quaternion ReadHalfPrecisionQuaternion()
    {
        Vector4 f = ReadHalfPrecisionVector4();
        return new Quaternion(f.x, f.y, f.z, f.w);
    }

    /// <summary>
    /// Reads a <see cref="Quaternion?"/> from the buffer.
    /// </summary>
    public Quaternion? ReadNullableQuaternion()
    {
        if (!ReadBool()) return null;
        return ReadQuaternion();
    }

    /// <summary>
    /// Reads a <see cref="Color"/> (16 bytes) from the buffer.
    /// </summary>
    public Color ReadColor()
    {
        Vector4 f = ReadVector4();
        return new Color(f.y, f.z, f.w, f.x);
    }

    /// <summary>
    /// Reads a <see cref="Color?"/> (16 bytes) from the buffer.
    /// </summary>
    public Color? ReadNullableColor()
    {
        if (!ReadBool()) return null;
        return ReadColor();
    }

    /// <summary>
    /// Reads a <see cref="Color32"/> (4 bytes) from the buffer.
    /// </summary>
    public Color32 ReadColor32()
    {
        if (!EnsureMoreLength(4))
            return C32NaN;

        Color32 q = new Color32(_buffer![_index], _buffer[_index + 1], _buffer[_index + 2], _buffer[_index + 3]);

        _index += 4;
        return q;
    }

    /// <summary>
    /// Reads a <see cref="Color32?"/> (4 bytes) from the buffer.
    /// </summary>
    public Color32? ReadNullableColor32()
    {
        return !ReadBool() ? null : ReadColor32();
    }

    /// <summary>
    /// Reads a <typeparamref name="TEnum"/> from the buffer. Size is based on the underlying type.
    /// </summary>
    /// <remarks>Don't use this if the underlying type is subject to change when data vesioning matters.</remarks>
    public unsafe TEnum ReadEnum<TEnum>() where TEnum : unmanaged, Enum => !EnsureMoreLength(sizeof(TEnum)) ? default : Read<TEnum>();

    /// <summary>
    /// Reads a nullable <typeparamref name="TEnum"/> from the buffer. Size is based on the underlying type.
    /// </summary>
    /// <remarks>Don't use this if the underlying type is subject to change when data vesioning matters.</remarks>
    public TEnum? ReadNullableEnum<TEnum>() where TEnum : unmanaged, Enum => !ReadBool() ? null : ReadEnum<TEnum>();

    /// <summary>
    /// Reads a <see cref="int"/> array and its length (as a UInt16).
    /// </summary>
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

    /// <summary>
    /// Reads a <see cref="int"/> array which compresses repeating zero elements.
    /// </summary>
    /// <param name="long">Write length as an Int32 instead of UInt16.</param>
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
                int next;
                for (int j = ReadUInt8(); j > 0; --j)
                {
                    next = ReadInt32();
                    if (i < len)
                        output[i] = next;
                    else
                        Fail(InvalidZeroCompressedMessage);
                    ++i;
                }
                next = ReadInt32();
                if (i < len)
                    output[i] = next;
                else
                    Fail(InvalidZeroCompressedMessage);
            }
            else
            {
                i += b;
                if (i >= len)
                    Fail(InvalidZeroCompressedMessage);
            }
        }
        return output;
    }

    /// <summary>
    /// Reads a <see cref="int"/> array that can be null and its length (as a UInt16).
    /// </summary>
    public int[]? ReadNullableInt32Array() => !ReadBool() ? null : ReadInt32Array();

    /// <summary>
    /// Reads a <see cref="Guid"/> array and its length (as a UInt16).
    /// </summary>
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

    /// <summary>
    /// Reads a <see cref="Guid"/> array that can be null and its length (as a UInt16).
    /// </summary>
    public Guid[]? ReadNullableGuidArray() => !ReadBool() ? null : ReadGuidArray();

    /// <summary>
    /// Reads a <see cref="DateTime"/> array and its length (as a UInt16). Keeps <see cref="DateTimeKind"/> information.
    /// </summary>
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

    /// <summary>
    /// Reads a <see cref="DateTime"/> array that can be null and its length (as a UInt16). Keeps <see cref="DateTimeKind"/> information.
    /// </summary>
    public DateTime[]? ReadNullableDateTimeArray() => !ReadBool() ? null : ReadDateTimeArray();

    /// <summary>
    /// Reads a <see cref="DateTimeOffset"/> array and its length (as a UInt16). Keeps <see cref="DateTimeKind"/> and offset information.
    /// </summary>
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

    /// <summary>
    /// Reads a <see cref="DateTimeOffset"/> array that can be null and its length (as a UInt16). Keeps <see cref="DateTimeKind"/> and offset information.
    /// </summary>
    public DateTimeOffset[]? ReadNullableDateTimeOffsetArray() => !ReadBool() ? null : ReadDateTimeOffsetArray();

    /// <summary>
    /// Reads a <see cref="uint"/> array and its length (as a UInt16).
    /// </summary>
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

    /// <summary>
    /// Reads a <see cref="uint"/> array which compresses repeating zero elements.
    /// </summary>
    /// <param name="long">Write length as an Int32 instead of UInt16.</param>
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
                uint next;
                for (int j = ReadUInt8(); j > 0; --j)
                {
                    next = ReadUInt32();
                    if (i < len)
                        output[i] = next;
                    else
                        Fail(InvalidZeroCompressedMessage);
                    ++i;
                }
                next = ReadUInt32();
                if (i < len)
                    output[i] = next;
                else
                    Fail(InvalidZeroCompressedMessage);
            }
            else
            {
                i += b;
                if (i >= len)
                    Fail(InvalidZeroCompressedMessage);
            }
        }
        return output;
    }

    /// <summary>
    /// Reads a <see cref="uint"/> array that can be null and its length (as a UInt16).
    /// </summary>
    public uint[]? ReadNullableUInt32Array() => !ReadBool() ? null : ReadUInt32Array();

    /// <summary>
    /// Reads a <see cref="sbyte"/> array and its length (as a UInt16).
    /// </summary>
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

    /// <summary>
    /// Reads a <see cref="sbyte"/> array which compresses repeating zero elements.
    /// </summary>
    /// <param name="long">Write length as an Int32 instead of UInt16.</param>
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
                sbyte next;
                for (int j = ReadUInt8(); j > 0; --j)
                {
                    next = ReadInt8();
                    if (i < len)
                        output[i] = next;
                    else
                        Fail(InvalidZeroCompressedMessage);
                    ++i;
                }
                next = ReadInt8();
                if (i < len)
                    output[i] = next;
                else
                    Fail(InvalidZeroCompressedMessage);
            }
            else
            {
                i += b;
                if (i >= len)
                    Fail(InvalidZeroCompressedMessage);
            }
        }
        return output;
    }

    /// <summary>
    /// Reads a <see cref="sbyte"/> array that can be null and its length (as a UInt16).
    /// </summary>
    public sbyte[]? ReadNullableInt8Array() => !ReadBool() ? null : ReadInt8Array();

    /// <summary>
    /// Reads a <see cref="bool"/> array and its length (as a UInt16).
    /// </summary>
    /// <remarks>Compresses into bits.</remarks>
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

    /// <summary>
    /// Reads a <see cref="bool"/> array and its length (as a UInt32).
    /// </summary>
    /// <remarks>Compresses into bits.</remarks>
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

    /// <summary>
    /// Reads a <see cref="bool"/> array that can be null and its length (as a UInt16).
    /// </summary>
    public bool[]? ReadNullableBoolArray() => !ReadBool() ? null : ReadBoolArray();

    /// <summary>
    /// Reads a <see cref="long"/> array and its length (as a UInt16).
    /// </summary>
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

    /// <summary>
    /// Reads a <see cref="long"/> array which compresses repeating zero elements.
    /// </summary>
    /// <param name="long">Write length as an Int32 instead of UInt16.</param>
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
                long next;
                for (int j = ReadUInt8(); j > 0; --j)
                {
                    next = ReadInt64();
                    if (i < len)
                        output[i] = next;
                    else
                        Fail(InvalidZeroCompressedMessage);
                    ++i;
                }
                next = ReadInt64();
                if (i < len)
                    output[i] = next;
                else
                    Fail(InvalidZeroCompressedMessage);
            }
            else
            {
                i += b;
                if (i >= len)
                    Fail(InvalidZeroCompressedMessage);
            }
        }
        return output;
    }

    /// <summary>
    /// Reads a <see cref="long"/> array that can be null and its length (as a UInt16).
    /// </summary>
    public long[]? ReadNullableInt64Array() => !ReadBool() ? null : ReadInt64Array();

    /// <summary>
    /// Reads a <see cref="ulong"/> array and its length (as a UInt16).
    /// </summary>
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

    /// <summary>
    /// Reads a <see cref="ulong"/> array which compresses repeating zero elements.
    /// </summary>
    /// <param name="long">Write length as an Int32 instead of UInt16.</param>
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
                ulong next;
                for (int j = ReadUInt8(); j > 0; --j)
                {
                    next = ReadUInt64();
                    if (i < len)
                        output[i] = next;
                    else
                        Fail(InvalidZeroCompressedMessage);
                    ++i;
                }
                next = ReadUInt64();
                if (i < len)
                    output[i] = next;
                else
                    Fail(InvalidZeroCompressedMessage);
            }
            else
            {
                i += b;
                if (i >= len)
                    Fail(InvalidZeroCompressedMessage);
            }
        }
        return output;
    }

    /// <summary>
    /// Reads a <see cref="ulong"/> array that can be null and its length (as a UInt16).
    /// </summary>
    public ulong[]? ReadNullableUInt64Array() => !ReadBool() ? null : ReadUInt64Array();

    /// <summary>
    /// Reads a <see cref="short"/> array and its length (as a UInt16).
    /// </summary>
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

    /// <summary>
    /// Reads a <see cref="short"/> array which compresses repeating zero elements.
    /// </summary>
    /// <param name="long">Write length as an Int32 instead of UInt16.</param>
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
                short next;
                for (int j = ReadUInt8(); j > 0; --j)
                {
                    next = ReadInt16();
                    if (i < len)
                        output[i] = next;
                    else
                        Fail(InvalidZeroCompressedMessage);
                    ++i;
                }
                next = ReadInt16();
                if (i < len)
                    output[i] = next;
                else
                    Fail(InvalidZeroCompressedMessage);
            }
            else
            {
                i += b;
                if (i >= len)
                    Fail(InvalidZeroCompressedMessage);
            }
        }
        return output;
    }

    /// <summary>
    /// Reads a <see cref="short"/> array that can be null and its length (as a UInt16).
    /// </summary>
    public short[]? ReadNullableInt16Array() => !ReadBool() ? null : ReadInt16Array();

    /// <summary>
    /// Reads a <see cref="ushort"/> array and its length (as a UInt16).
    /// </summary>
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

    /// <summary>
    /// Reads a <see cref="ushort"/> array which compresses repeating zero elements.
    /// </summary>
    /// <param name="long">Write length as an Int32 instead of UInt16.</param>
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
                ushort next;
                for (int j = ReadUInt8(); j > 0; --j)
                {
                    next = ReadUInt16();
                    if (i < len)
                        output[i] = next;
                    else
                        Fail(InvalidZeroCompressedMessage);
                    ++i;
                }
                next = ReadUInt16();
                if (i < len)
                    output[i] = next;
                else
                    Fail(InvalidZeroCompressedMessage);
            }
            else
            {
                i += b;
                if (i >= len)
                    Fail(InvalidZeroCompressedMessage);
            }
        }
        return output;
    }

    /// <summary>
    /// Reads a <see cref="ushort"/> array that can be null and its length (as a UInt16).
    /// </summary>
    public ushort[]? ReadNullableUInt16Array() => !ReadBool() ? null : ReadUInt16Array();

    /// <summary>
    /// Reads a <see cref="float"/> array and its length (as a UInt16).
    /// </summary>
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

    /// <summary>
    /// Reads a <see cref="float"/> array that can be null and its length (as a UInt16).
    /// </summary>
    public float[]? ReadNullableFloatArray() => !ReadBool() ? null : ReadFloatArray();

    /// <summary>
    /// Reads a <see cref="decimal"/> array and its length (as a UInt16).
    /// </summary>
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

    /// <summary>
    /// Reads a <see cref="decimal"/> array that can be null and its length (as a UInt16).
    /// </summary>
    public decimal[]? ReadNullableDecimalArray() => !ReadBool() ? null : ReadDecimalArray();

    /// <summary>
    /// Reads a <see cref="double"/> array and its length (as a UInt16).
    /// </summary>
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

    /// <summary>
    /// Reads a <see cref="double"/> array that can be null and its length (as a UInt16).
    /// </summary>
    public double[]? ReadNullableDoubleArray() => !ReadBool() ? null : ReadDoubleArray();

    /// <summary>
    /// Reads a <see cref="char"/> array and its length (as a UInt16).
    /// </summary>
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

    /// <summary>
    /// Reads a <see cref="char"/> array that can be null and its length (as a UInt16).
    /// </summary>
    public char[]? ReadNullableCharArray() => !ReadBool() ? null : ReadCharArray();

    /// <summary>
    /// Reads a <see cref="string"/> array and its length (as a UInt16).
    /// </summary>
    public string[] ReadStringArray()
    {
        string[] rtn = new string[ReadUInt16()];
        for (int i = 0; i < rtn.Length; i++)
            rtn[i] = ReadString();
        return rtn;
    }

    /// <summary>
    /// Reads a <see cref="string"/> array that can be null and its length (as a UInt16).
    /// </summary>
    public string[] ReadNullableStringArray() => !ReadBool() ? null! : ReadStringArray();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static void VerifyType<T>(int typeIndex = -1) => VerifyType(typeof(T), typeIndex);
    protected static void VerifyType(Type type, int typeIndex = -1)
    {
        if (!EncodingEx.IsValidAutoType(type))
            throw new InvalidDynamicTypeException(type, typeIndex, true);
    }

    /// <summary>
    /// Returns a delegate for reading any supported type. Use <see cref="EncodingEx.IsValidAutoType"/> to check if a type is valid, or <see langword="null"/> if the type is not supported.
    /// </summary>
    /// <remarks>Caches when possible, also works for enum types.</remarks>
    /// <param name="isNullable">Uses the nullable variant when possible.</param>
    public static Reader<T1>? GetReader<T1>(bool isNullable = false)
    {
        Type type = typeof(T1);
        if (!EncodingEx.IsValidAutoType(type))
            return GetReaderIntl<T1>();

        return isNullable ? NullableReaderHelper<T1>.Reader : ReaderHelper<T1>.Reader;
    }
    private static Reader<T1>? GetReaderIntl<T1>(bool isNullable = false) => (Reader<T1>?)GetReader(typeof(T1), isNullable);

    /// <summary>
    /// Returns a delegate of type <see cref="Reader{T}"/> for reading any supported type, or <see langword="null"/> if the type is not supported. Use <see cref="EncodingEx.IsValidAutoType"/> to check if a type is valid.
    /// </summary>
    /// <remarks>Also works for enum types.</remarks>
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

    /// <summary>
    /// Returns a <see cref="MethodInfo"/> for reading any supported type, or <see langword="null"/> if the type is not supported. Use <see cref="EncodingEx.IsValidAutoType"/> to check if a type is valid
    /// </summary>
    /// <remarks>Also works for enum types.</remarks>
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
    internal static class ReaderHelper<T>
    {
        public static readonly Reader<T>? Reader;
        static ReaderHelper()
        {
            VerifyType<T>();
            Reader = GetReaderIntl<T>();
        }
    }
    internal static class NullableReaderHelper<T>
    {
        public static readonly Reader<T>? Reader;
        static NullableReaderHelper()
        {
            VerifyType<T>();
            Reader = GetReaderIntl<T>(true);
        }
    }
}

/// <summary>
/// Represents a static read method for <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The type of value to read.</typeparam>
/// <param name="reader">The reader.</param>
/// <returns>The read value.</returns>
public delegate T Reader<out T>(ByteReader reader);

/// <summary>
/// Raw byte readers can be given custom reader methods (or leave them <see langword="null"/> to auto-fill them.
/// </summary>
public sealed class ByteReaderRaw<T> : ByteReader
{
    private readonly Reader<T> _reader;

    /// <summary>Leave <paramref name="reader"/> <see langword="null"/> to auto-fill.</summary>
    public ByteReaderRaw(Reader<T>? reader)
    {
        _reader = reader ?? ReaderHelper<T>.Reader!;
    }
    public bool Read(byte[]? bytes, int index, out T arg)
    {
        lock (this)
        {
            if (bytes != null)
                LoadNew(bytes, index);
            arg = _reader.Invoke(this);
            return !HasFailed;
        }
    }
}

/// <summary>
/// Raw byte readers can be given custom reader methods (or leave them <see langword="null"/> to auto-fill them.
/// </summary>
public sealed class ByteReaderRaw<T1, T2> : ByteReader
{
    private readonly Reader<T1> _reader1;
    private readonly Reader<T2> _reader2;

    /// <summary>Leave any reader <see langword="null"/> to auto-fill.</summary>
    public ByteReaderRaw(Reader<T1>? reader1, Reader<T2>? reader2)
    {
        _reader1 = reader1 ?? ReaderHelper<T1>.Reader!;
        _reader2 = reader2 ?? ReaderHelper<T2>.Reader!;
    }
    public bool Read(byte[]? bytes, int index, out T1 arg1, out T2 arg2)
    {
        lock (this)
        {
            if (bytes != null)
                LoadNew(bytes, index);
            arg1 = _reader1.Invoke(this);
            arg2 = _reader2.Invoke(this);
            return !HasFailed;
        }
    }
}

/// <summary>
/// Raw byte readers can be given custom reader methods (or leave them <see langword="null"/> to auto-fill them.
/// </summary>
public sealed class ByteReaderRaw<T1, T2, T3> : ByteReader
{
    private readonly Reader<T1> _reader1;
    private readonly Reader<T2> _reader2;
    private readonly Reader<T3> _reader3;

    /// <summary>Leave any reader <see langword="null"/> to auto-fill.</summary>
    public ByteReaderRaw(Reader<T1>? reader1, Reader<T2>? reader2, Reader<T3>? reader3)
    {
        _reader1 = reader1 ?? ReaderHelper<T1>.Reader!;
        _reader2 = reader2 ?? ReaderHelper<T2>.Reader!;
        _reader3 = reader3 ?? ReaderHelper<T3>.Reader!;
    }
    public bool Read(byte[]? bytes, int index, out T1 arg1, out T2 arg2, out T3 arg3)
    {
        lock (this)
        {
            if (bytes != null)
                LoadNew(bytes, index);
            arg1 = _reader1.Invoke(this);
            arg2 = _reader2.Invoke(this);
            arg3 = _reader3.Invoke(this);
            return !HasFailed;
        }
    }
}

/// <summary>
/// Raw byte readers can be given custom reader methods (or leave them <see langword="null"/> to auto-fill them.
/// </summary>
public sealed class ByteReaderRaw<T1, T2, T3, T4> : ByteReader
{
    private readonly Reader<T1> _reader1;
    private readonly Reader<T2> _reader2;
    private readonly Reader<T3> _reader3;
    private readonly Reader<T4> _reader4;

    /// <summary>Leave any reader <see langword="null"/> to auto-fill.</summary>
    public ByteReaderRaw(Reader<T1>? reader1, Reader<T2>? reader2, Reader<T3>? reader3, Reader<T4>? reader4)
    {
        _reader1 = reader1 ?? ReaderHelper<T1>.Reader!;
        _reader2 = reader2 ?? ReaderHelper<T2>.Reader!;
        _reader3 = reader3 ?? ReaderHelper<T3>.Reader!;
        _reader4 = reader4 ?? ReaderHelper<T4>.Reader!;
    }
    public bool Read(byte[]? bytes, int index, out T1 arg1, out T2 arg2, out T3 arg3, out T4 arg4)
    {
        lock (this)
        {
            if (bytes != null)
                LoadNew(bytes, index);
            arg1 = _reader1.Invoke(this);
            arg2 = _reader2.Invoke(this);
            arg3 = _reader3.Invoke(this);
            arg4 = _reader4.Invoke(this);
            return !HasFailed;
        }
    }
}

/// <summary>
/// Raw byte readers can be given custom reader methods (or leave them <see langword="null"/> to auto-fill them.
/// </summary>
public sealed class ByteReaderRaw<T1, T2, T3, T4, T5> : ByteReader
{
    private readonly Reader<T1> _reader1;
    private readonly Reader<T2> _reader2;
    private readonly Reader<T3> _reader3;
    private readonly Reader<T4> _reader4;
    private readonly Reader<T5> _reader5;

    /// <summary>Leave any reader <see langword="null"/> to auto-fill.</summary>
    public ByteReaderRaw(Reader<T1>? reader1, Reader<T2>? reader2, Reader<T3>? reader3, Reader<T4>? reader4, Reader<T5>? reader5)
    {
        _reader1 = reader1 ?? ReaderHelper<T1>.Reader!;
        _reader2 = reader2 ?? ReaderHelper<T2>.Reader!;
        _reader3 = reader3 ?? ReaderHelper<T3>.Reader!;
        _reader4 = reader4 ?? ReaderHelper<T4>.Reader!;
        _reader5 = reader5 ?? ReaderHelper<T5>.Reader!;
    }
    public bool Read(byte[]? bytes, int index, out T1 arg1, out T2 arg2, out T3 arg3, out T4 arg4, out T5 arg5)
    {
        lock (this)
        {
            if (bytes != null)
                LoadNew(bytes, index);
            arg1 = _reader1.Invoke(this);
            arg2 = _reader2.Invoke(this);
            arg3 = _reader3.Invoke(this);
            arg4 = _reader4.Invoke(this);
            arg5 = _reader5.Invoke(this);
            return !HasFailed;
        }
    }
}

/// <summary>
/// Raw byte readers can be given custom reader methods (or leave them <see langword="null"/> to auto-fill them.
/// </summary>
public sealed class ByteReaderRaw<T1, T2, T3, T4, T5, T6> : ByteReader
{
    private readonly Reader<T1> _reader1;
    private readonly Reader<T2> _reader2;
    private readonly Reader<T3> _reader3;
    private readonly Reader<T4> _reader4;
    private readonly Reader<T5> _reader5;
    private readonly Reader<T6> _reader6;

    /// <summary>Leave any reader <see langword="null"/> to auto-fill.</summary>
    public ByteReaderRaw(Reader<T1>? reader1, Reader<T2>? reader2, Reader<T3>? reader3, Reader<T4>? reader4, Reader<T5>? reader5, Reader<T6>? reader6)
    {
        _reader1 = reader1 ?? ReaderHelper<T1>.Reader!;
        _reader2 = reader2 ?? ReaderHelper<T2>.Reader!;
        _reader3 = reader3 ?? ReaderHelper<T3>.Reader!;
        _reader4 = reader4 ?? ReaderHelper<T4>.Reader!;
        _reader5 = reader5 ?? ReaderHelper<T5>.Reader!;
        _reader6 = reader6 ?? ReaderHelper<T6>.Reader!;
    }
    public bool Read(byte[]? bytes, int index, out T1 arg1, out T2 arg2, out T3 arg3, out T4 arg4, out T5 arg5, out T6 arg6)
    {
        lock (this)
        {
            if (bytes != null)
                LoadNew(bytes, index);
            arg1 = _reader1.Invoke(this);
            arg2 = _reader2.Invoke(this);
            arg3 = _reader3.Invoke(this);
            arg4 = _reader4.Invoke(this);
            arg5 = _reader5.Invoke(this);
            arg6 = _reader6.Invoke(this);
            return !HasFailed;
        }
    }
}

/// <summary>
/// Dynamic byte readers auto-fill their reader methods.
/// </summary>
public sealed class DynamicByteReader<T1> : ByteReader
{
    static DynamicByteReader()
    {
        Reader1 = ReaderHelper<T1>.Reader!;
    }
    private static readonly Reader<T1> Reader1;

    public bool Read(byte[]? bytes, int index, out T1 arg1)
    {
        lock (this)
        {
            if (bytes != null)
                LoadNew(bytes, index);
            arg1 = Reader1(this);
            return !HasFailed;
        }
    }
}

/// <summary>
/// Dynamic byte readers auto-fill their reader methods.
/// </summary>
public sealed class DynamicByteReader<T1, T2> : ByteReader
{
    static DynamicByteReader()
    {
        Reader1 = ReaderHelper<T1>.Reader!;
        Reader2 = ReaderHelper<T2>.Reader!;
    }
    private static readonly Reader<T1> Reader1;
    private static readonly Reader<T2> Reader2;

    public bool Read(byte[]? bytes, int index, out T1 arg1, out T2 arg2)
    {
        lock (this)
        {
            if (bytes != null)
                LoadNew(bytes, index);
            arg1 = Reader1(this);
            arg2 = Reader2(this);
            return !HasFailed;
        }
    }
}

/// <summary>
/// Dynamic byte readers auto-fill their reader methods.
/// </summary>
public sealed class DynamicByteReader<T1, T2, T3> : ByteReader
{
    static DynamicByteReader()
    {
        Reader1 = ReaderHelper<T1>.Reader!;
        Reader2 = ReaderHelper<T2>.Reader!;
        Reader3 = ReaderHelper<T3>.Reader!;
    }
    private static readonly Reader<T1> Reader1;
    private static readonly Reader<T2> Reader2;
    private static readonly Reader<T3> Reader3;

    public bool Read(byte[]? bytes, int index, out T1 arg1, out T2 arg2, out T3 arg3)
    {
        lock (this)
        {
            if (bytes != null)
                LoadNew(bytes, index);
            arg1 = Reader1(this);
            arg2 = Reader2(this);
            arg3 = Reader3(this);
            return !HasFailed;
        }
    }
}

/// <summary>
/// Dynamic byte readers auto-fill their reader methods.
/// </summary>
public sealed class DynamicByteReader<T1, T2, T3, T4> : ByteReader
{
    static DynamicByteReader()
    {
        Reader1 = ReaderHelper<T1>.Reader!;
        Reader2 = ReaderHelper<T2>.Reader!;
        Reader3 = ReaderHelper<T3>.Reader!;
        Reader4 = ReaderHelper<T4>.Reader!;
    }
    private static readonly Reader<T1> Reader1;
    private static readonly Reader<T2> Reader2;
    private static readonly Reader<T3> Reader3;
    private static readonly Reader<T4> Reader4;

    public bool Read(byte[]? bytes, int index, out T1 arg1, out T2 arg2, out T3 arg3, out T4 arg4)
    {
        lock (this)
        {
            if (bytes != null)
                LoadNew(bytes, index);
            arg1 = Reader1(this);
            arg2 = Reader2(this);
            arg3 = Reader3(this);
            arg4 = Reader4(this);
            return !HasFailed;
        }
    }
}

/// <summary>
/// Dynamic byte readers auto-fill their reader methods.
/// </summary>
public sealed class DynamicByteReader<T1, T2, T3, T4, T5> : ByteReader
{
    static DynamicByteReader()
    {
        Reader1 = ReaderHelper<T1>.Reader!;
        Reader2 = ReaderHelper<T2>.Reader!;
        Reader3 = ReaderHelper<T3>.Reader!;
        Reader4 = ReaderHelper<T4>.Reader!;
        Reader5 = ReaderHelper<T5>.Reader!;
    }
    private static readonly Reader<T1> Reader1;
    private static readonly Reader<T2> Reader2;
    private static readonly Reader<T3> Reader3;
    private static readonly Reader<T4> Reader4;
    private static readonly Reader<T5> Reader5;

    public bool Read(byte[]? bytes, int index, out T1 arg1, out T2 arg2, out T3 arg3, out T4 arg4, out T5 arg5)
    {
        lock (this)
        {
            if (bytes != null)
                LoadNew(bytes, index);
            arg1 = Reader1(this);
            arg2 = Reader2(this);
            arg3 = Reader3(this);
            arg4 = Reader4(this);
            arg5 = Reader5(this);
            return !HasFailed;
        }
    }
}

/// <summary>
/// Dynamic byte readers auto-fill their reader methods.
/// </summary>
public sealed class DynamicByteReader<T1, T2, T3, T4, T5, T6> : ByteReader
{
    static DynamicByteReader()
    {
        Reader1 = ReaderHelper<T1>.Reader!;
        Reader2 = ReaderHelper<T2>.Reader!;
        Reader3 = ReaderHelper<T3>.Reader!;
        Reader4 = ReaderHelper<T4>.Reader!;
        Reader5 = ReaderHelper<T5>.Reader!;
        Reader6 = ReaderHelper<T6>.Reader!;
    }
    private static readonly Reader<T1> Reader1;
    private static readonly Reader<T2> Reader2;
    private static readonly Reader<T3> Reader3;
    private static readonly Reader<T4> Reader4;
    private static readonly Reader<T5> Reader5;
    private static readonly Reader<T6> Reader6;

    public bool Read(byte[]? bytes, int index, out T1 arg1, out T2 arg2, out T3 arg3, out T4 arg4, out T5 arg5, out T6 arg6)
    {
        lock (this)
        {
            if (bytes != null)
                LoadNew(bytes, index);
            arg1 = Reader1(this);
            arg2 = Reader2(this);
            arg3 = Reader3(this);
            arg4 = Reader4(this);
            arg5 = Reader5(this);
            arg6 = Reader6(this);
            return !HasFailed;
        }
    }
}

/// <summary>
/// Dynamic byte readers auto-fill their reader methods.
/// </summary>
public sealed class DynamicByteReader<T1, T2, T3, T4, T5, T6, T7> : ByteReader
{
    static DynamicByteReader()
    {
        Reader1 = ReaderHelper<T1>.Reader!;
        Reader2 = ReaderHelper<T2>.Reader!;
        Reader3 = ReaderHelper<T3>.Reader!;
        Reader4 = ReaderHelper<T4>.Reader!;
        Reader5 = ReaderHelper<T5>.Reader!;
        Reader6 = ReaderHelper<T6>.Reader!;
        Reader7 = ReaderHelper<T7>.Reader!;
    }
    private static readonly Reader<T1> Reader1;
    private static readonly Reader<T2> Reader2;
    private static readonly Reader<T3> Reader3;
    private static readonly Reader<T4> Reader4;
    private static readonly Reader<T5> Reader5;
    private static readonly Reader<T6> Reader6;
    private static readonly Reader<T7> Reader7;

    public bool Read(byte[]? bytes, int index, out T1 arg1, out T2 arg2, out T3 arg3, out T4 arg4, out T5 arg5, out T6 arg6, out T7 arg7)
    {
        lock (this)
        {
            if (bytes != null)
                LoadNew(bytes, index);
            arg1 = Reader1(this);
            arg2 = Reader2(this);
            arg3 = Reader3(this);
            arg4 = Reader4(this);
            arg5 = Reader5(this);
            arg6 = Reader6(this);
            arg7 = Reader7(this);
            return !HasFailed;
        }
    }
}

/// <summary>
/// Dynamic byte readers auto-fill their reader methods.
/// </summary>
public sealed class DynamicByteReader<T1, T2, T3, T4, T5, T6, T7, T8> : ByteReader
{
    static DynamicByteReader()
    {
        Reader1 = ReaderHelper<T1>.Reader!;
        Reader2 = ReaderHelper<T2>.Reader!;
        Reader3 = ReaderHelper<T3>.Reader!;
        Reader4 = ReaderHelper<T4>.Reader!;
        Reader5 = ReaderHelper<T5>.Reader!;
        Reader6 = ReaderHelper<T6>.Reader!;
        Reader7 = ReaderHelper<T7>.Reader!;
        Reader8 = ReaderHelper<T8>.Reader!;
    }
    private static readonly Reader<T1> Reader1;
    private static readonly Reader<T2> Reader2;
    private static readonly Reader<T3> Reader3;
    private static readonly Reader<T4> Reader4;
    private static readonly Reader<T5> Reader5;
    private static readonly Reader<T6> Reader6;
    private static readonly Reader<T7> Reader7;
    private static readonly Reader<T8> Reader8;

    public bool Read(byte[]? bytes, int index, out T1 arg1, out T2 arg2, out T3 arg3, out T4 arg4, out T5 arg5, out T6 arg6, out T7 arg7, out T8 arg8)
    {
        lock (this)
        {
            if (bytes != null)
                LoadNew(bytes, index);
            arg1 = Reader1(this);
            arg2 = Reader2(this);
            arg3 = Reader3(this);
            arg4 = Reader4(this);
            arg5 = Reader5(this);
            arg6 = Reader6(this);
            arg7 = Reader7(this);
            arg8 = Reader8(this);
            return !HasFailed;
        }
    }
}

/// <summary>
/// Dynamic byte readers auto-fill their reader methods.
/// </summary>
public sealed class DynamicByteReader<T1, T2, T3, T4, T5, T6, T7, T8, T9> : ByteReader
{
    static DynamicByteReader()
    {
        Reader1 = ReaderHelper<T1>.Reader!;
        Reader2 = ReaderHelper<T2>.Reader!;
        Reader3 = ReaderHelper<T3>.Reader!;
        Reader4 = ReaderHelper<T4>.Reader!;
        Reader5 = ReaderHelper<T5>.Reader!;
        Reader6 = ReaderHelper<T6>.Reader!;
        Reader7 = ReaderHelper<T7>.Reader!;
        Reader8 = ReaderHelper<T8>.Reader!;
        Reader9 = ReaderHelper<T9>.Reader!;
    }
    private static readonly Reader<T1> Reader1;
    private static readonly Reader<T2> Reader2;
    private static readonly Reader<T3> Reader3;
    private static readonly Reader<T4> Reader4;
    private static readonly Reader<T5> Reader5;
    private static readonly Reader<T6> Reader6;
    private static readonly Reader<T7> Reader7;
    private static readonly Reader<T8> Reader8;
    private static readonly Reader<T9> Reader9;

    public bool Read(byte[]? bytes, int index, out T1 arg1, out T2 arg2, out T3 arg3, out T4 arg4, out T5 arg5, out T6 arg6, out T7 arg7, out T8 arg8, out T9 arg9)
    {
        lock (this)
        {
            if (bytes != null)
                LoadNew(bytes, index);
            arg1 = Reader1(this);
            arg2 = Reader2(this);
            arg3 = Reader3(this);
            arg4 = Reader4(this);
            arg5 = Reader5(this);
            arg6 = Reader6(this);
            arg7 = Reader7(this);
            arg8 = Reader8(this);
            arg9 = Reader9(this);
            return !HasFailed;
        }
    }
}

/// <summary>
/// Dynamic byte readers auto-fill their reader methods.
/// </summary>
public sealed class DynamicByteReader<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> : ByteReader
{
    static DynamicByteReader()
    {
        Reader1 = ReaderHelper<T1>.Reader!;
        Reader2 = ReaderHelper<T2>.Reader!;
        Reader3 = ReaderHelper<T3>.Reader!;
        Reader4 = ReaderHelper<T4>.Reader!;
        Reader5 = ReaderHelper<T5>.Reader!;
        Reader6 = ReaderHelper<T6>.Reader!;
        Reader7 = ReaderHelper<T7>.Reader!;
        Reader8 = ReaderHelper<T8>.Reader!;
        Reader9 = ReaderHelper<T9>.Reader!;
        Reader10 = ReaderHelper<T10>.Reader!;
    }
    private static readonly Reader<T1> Reader1;
    private static readonly Reader<T2> Reader2;
    private static readonly Reader<T3> Reader3;
    private static readonly Reader<T4> Reader4;
    private static readonly Reader<T5> Reader5;
    private static readonly Reader<T6> Reader6;
    private static readonly Reader<T7> Reader7;
    private static readonly Reader<T8> Reader8;
    private static readonly Reader<T9> Reader9;
    private static readonly Reader<T10> Reader10;

    public bool Read(byte[]? bytes, int index, out T1 arg1, out T2 arg2, out T3 arg3, out T4 arg4, out T5 arg5, out T6 arg6, out T7 arg7, out T8 arg8, out T9 arg9, out T10 arg10)
    {
        lock (this)
        {
            if (bytes != null)
                LoadNew(bytes, index);
            arg1 = Reader1(this);
            arg2 = Reader2(this);
            arg3 = Reader3(this);
            arg4 = Reader4(this);
            arg5 = Reader5(this);
            arg6 = Reader6(this);
            arg7 = Reader7(this);
            arg8 = Reader8(this);
            arg9 = Reader9(this);
            arg10 = Reader10(this);
            return !HasFailed;
        }
    }
}

/// <summary>
/// Thrown when a type is given to a <see cref="ByteReader"/> that can't be read automatically.
/// </summary>
public sealed class InvalidDynamicTypeException : Exception
{
    public InvalidDynamicTypeException() { }
    public InvalidDynamicTypeException(Type arg, int typeNumber, bool reader) :
        base("Generic argument " + arg.Name + ": T" + typeNumber + " is not able to be " + (reader ? "read" : "written") + " automatically.")
    { }
}
