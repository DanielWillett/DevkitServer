using DanielWillett.SpeedBytes;

namespace DevkitServer.Util.Encoding;

/// <summary>
/// Raw byte readers can be given custom reader methods (or leave them <see langword="null"/> to auto-fill them.
/// </summary>
public sealed class ByteReaderRaw<T> : ByteReader
{
    private readonly Reader<T> _reader;

    /// <summary>Leave <paramref name="reader"/> <see langword="null"/> to auto-fill.</summary>
    public ByteReaderRaw(Reader<T>? reader)
    {
        _reader = reader ?? GetReadMethodDelegate<T>();
    }
    public bool Read(ArraySegment<byte> message, out T arg)
    {
        lock (this)
        {
            if (message.Array != null)
                LoadNew(message);
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
        _reader1 = reader1 ?? GetReadMethodDelegate<T1>();
        _reader2 = reader2 ?? GetReadMethodDelegate<T2>();
    }
    public bool Read(ArraySegment<byte> message, out T1 arg1, out T2 arg2)
    {
        lock (this)
        {
            if (message.Array != null)
                LoadNew(message);
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
        _reader1 = reader1 ?? GetReadMethodDelegate<T1>();
        _reader2 = reader2 ?? GetReadMethodDelegate<T2>();
        _reader3 = reader3 ?? GetReadMethodDelegate<T3>();
    }
    public bool Read(ArraySegment<byte> message, out T1 arg1, out T2 arg2, out T3 arg3)
    {
        lock (this)
        {
            if (message.Array != null)
                LoadNew(message);
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
        _reader1 = reader1 ?? GetReadMethodDelegate<T1>();
        _reader2 = reader2 ?? GetReadMethodDelegate<T2>();
        _reader3 = reader3 ?? GetReadMethodDelegate<T3>();
        _reader4 = reader4 ?? GetReadMethodDelegate<T4>();
    }
    public bool Read(ArraySegment<byte> message, out T1 arg1, out T2 arg2, out T3 arg3, out T4 arg4)
    {
        lock (this)
        {
            if (message.Array != null)
                LoadNew(message);
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
        _reader1 = reader1 ?? GetReadMethodDelegate<T1>();
        _reader2 = reader2 ?? GetReadMethodDelegate<T2>();
        _reader3 = reader3 ?? GetReadMethodDelegate<T3>();
        _reader4 = reader4 ?? GetReadMethodDelegate<T4>();
        _reader5 = reader5 ?? GetReadMethodDelegate<T5>();
    }
    public bool Read(ArraySegment<byte> message, out T1 arg1, out T2 arg2, out T3 arg3, out T4 arg4, out T5 arg5)
    {
        lock (this)
        {
            if (message.Array != null)
                LoadNew(message);
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
        _reader1 = reader1 ?? GetReadMethodDelegate<T1>();
        _reader2 = reader2 ?? GetReadMethodDelegate<T2>();
        _reader3 = reader3 ?? GetReadMethodDelegate<T3>();
        _reader4 = reader4 ?? GetReadMethodDelegate<T4>();
        _reader5 = reader5 ?? GetReadMethodDelegate<T5>();
        _reader6 = reader6 ?? GetReadMethodDelegate<T6>();
    }
    public bool Read(ArraySegment<byte> message, out T1 arg1, out T2 arg2, out T3 arg3, out T4 arg4, out T5 arg5, out T6 arg6)
    {
        lock (this)
        {
            if (message.Array != null)
                LoadNew(message);
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
        Reader1 = GetReadMethodDelegate<T1>();
    }
    private static readonly Reader<T1> Reader1;

    public bool Read(ArraySegment<byte> message, out T1 arg1)
    {
        lock (this)
        {
            if (message.Array != null)
                LoadNew(message);
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
        Reader1 = GetReadMethodDelegate<T1>();
        Reader2 = GetReadMethodDelegate<T2>();
    }
    private static readonly Reader<T1> Reader1;
    private static readonly Reader<T2> Reader2;

    public bool Read(ArraySegment<byte> message, out T1 arg1, out T2 arg2)
    {
        lock (this)
        {
            if (message.Array != null)
                LoadNew(message);
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
        Reader1 = GetReadMethodDelegate<T1>();
        Reader2 = GetReadMethodDelegate<T2>();
        Reader3 = GetReadMethodDelegate<T3>();
    }
    private static readonly Reader<T1> Reader1;
    private static readonly Reader<T2> Reader2;
    private static readonly Reader<T3> Reader3;

    public bool Read(ArraySegment<byte> message, out T1 arg1, out T2 arg2, out T3 arg3)
    {
        lock (this)
        {
            if (message.Array != null)
                LoadNew(message);
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
        Reader1 = GetReadMethodDelegate<T1>();
        Reader2 = GetReadMethodDelegate<T2>();
        Reader3 = GetReadMethodDelegate<T3>();
        Reader4 = GetReadMethodDelegate<T4>();
    }
    private static readonly Reader<T1> Reader1;
    private static readonly Reader<T2> Reader2;
    private static readonly Reader<T3> Reader3;
    private static readonly Reader<T4> Reader4;

    public bool Read(ArraySegment<byte> message, out T1 arg1, out T2 arg2, out T3 arg3, out T4 arg4)
    {
        lock (this)
        {
            if (message.Array != null)
                LoadNew(message);
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
        Reader1 = GetReadMethodDelegate<T1>();
        Reader2 = GetReadMethodDelegate<T2>();
        Reader3 = GetReadMethodDelegate<T3>();
        Reader4 = GetReadMethodDelegate<T4>();
        Reader5 = GetReadMethodDelegate<T5>();
    }
    private static readonly Reader<T1> Reader1;
    private static readonly Reader<T2> Reader2;
    private static readonly Reader<T3> Reader3;
    private static readonly Reader<T4> Reader4;
    private static readonly Reader<T5> Reader5;

    public bool Read(ArraySegment<byte> message, out T1 arg1, out T2 arg2, out T3 arg3, out T4 arg4, out T5 arg5)
    {
        lock (this)
        {
            if (message.Array != null)
                LoadNew(message);
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
        Reader1 = GetReadMethodDelegate<T1>();
        Reader2 = GetReadMethodDelegate<T2>();
        Reader3 = GetReadMethodDelegate<T3>();
        Reader4 = GetReadMethodDelegate<T4>();
        Reader5 = GetReadMethodDelegate<T5>();
        Reader6 = GetReadMethodDelegate<T6>();
    }
    private static readonly Reader<T1> Reader1;
    private static readonly Reader<T2> Reader2;
    private static readonly Reader<T3> Reader3;
    private static readonly Reader<T4> Reader4;
    private static readonly Reader<T5> Reader5;
    private static readonly Reader<T6> Reader6;

    public bool Read(ArraySegment<byte> message, out T1 arg1, out T2 arg2, out T3 arg3, out T4 arg4, out T5 arg5, out T6 arg6)
    {
        lock (this)
        {
            if (message.Array != null)
                LoadNew(message);
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
        Reader1 = GetReadMethodDelegate<T1>();
        Reader2 = GetReadMethodDelegate<T2>();
        Reader3 = GetReadMethodDelegate<T3>();
        Reader4 = GetReadMethodDelegate<T4>();
        Reader5 = GetReadMethodDelegate<T5>();
        Reader6 = GetReadMethodDelegate<T6>();
        Reader7 = GetReadMethodDelegate<T7>();
    }
    private static readonly Reader<T1> Reader1;
    private static readonly Reader<T2> Reader2;
    private static readonly Reader<T3> Reader3;
    private static readonly Reader<T4> Reader4;
    private static readonly Reader<T5> Reader5;
    private static readonly Reader<T6> Reader6;
    private static readonly Reader<T7> Reader7;

    public bool Read(ArraySegment<byte> message, out T1 arg1, out T2 arg2, out T3 arg3, out T4 arg4, out T5 arg5, out T6 arg6, out T7 arg7)
    {
        lock (this)
        {
            if (message.Array != null)
                LoadNew(message);
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
        Reader1 = GetReadMethodDelegate<T1>();
        Reader2 = GetReadMethodDelegate<T2>();
        Reader3 = GetReadMethodDelegate<T3>();
        Reader4 = GetReadMethodDelegate<T4>();
        Reader5 = GetReadMethodDelegate<T5>();
        Reader6 = GetReadMethodDelegate<T6>();
        Reader7 = GetReadMethodDelegate<T7>();
        Reader8 = GetReadMethodDelegate<T8>();
    }
    private static readonly Reader<T1> Reader1;
    private static readonly Reader<T2> Reader2;
    private static readonly Reader<T3> Reader3;
    private static readonly Reader<T4> Reader4;
    private static readonly Reader<T5> Reader5;
    private static readonly Reader<T6> Reader6;
    private static readonly Reader<T7> Reader7;
    private static readonly Reader<T8> Reader8;

    public bool Read(ArraySegment<byte> message, out T1 arg1, out T2 arg2, out T3 arg3, out T4 arg4, out T5 arg5, out T6 arg6, out T7 arg7, out T8 arg8)
    {
        lock (this)
        {
            if (message.Array != null)
                LoadNew(message);
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
        Reader1 = GetReadMethodDelegate<T1>();
        Reader2 = GetReadMethodDelegate<T2>();
        Reader3 = GetReadMethodDelegate<T3>();
        Reader4 = GetReadMethodDelegate<T4>();
        Reader5 = GetReadMethodDelegate<T5>();
        Reader6 = GetReadMethodDelegate<T6>();
        Reader7 = GetReadMethodDelegate<T7>();
        Reader8 = GetReadMethodDelegate<T8>();
        Reader9 = GetReadMethodDelegate<T9>();
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

    public bool Read(ArraySegment<byte> message, out T1 arg1, out T2 arg2, out T3 arg3, out T4 arg4, out T5 arg5, out T6 arg6, out T7 arg7, out T8 arg8, out T9 arg9)
    {
        lock (this)
        {
            if (message.Array != null)
                LoadNew(message);
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
        Reader1 = GetReadMethodDelegate<T1>();
        Reader2 = GetReadMethodDelegate<T2>();
        Reader3 = GetReadMethodDelegate<T3>();
        Reader4 = GetReadMethodDelegate<T4>();
        Reader5 = GetReadMethodDelegate<T5>();
        Reader6 = GetReadMethodDelegate<T6>();
        Reader7 = GetReadMethodDelegate<T7>();
        Reader8 = GetReadMethodDelegate<T8>();
        Reader9 = GetReadMethodDelegate<T9>();
        Reader10 = GetReadMethodDelegate<T10>();
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

    public bool Read(ArraySegment<byte> message, out T1 arg1, out T2 arg2, out T3 arg3, out T4 arg4, out T5 arg5, out T6 arg6, out T7 arg7, out T8 arg8, out T9 arg9, out T10 arg10)
    {
        lock (this)
        {
            if (message.Array != null)
                LoadNew(message);
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