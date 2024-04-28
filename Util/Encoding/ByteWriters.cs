using DanielWillett.SpeedBytes;
using DevkitServer.Multiplayer.Networking;

namespace DevkitServer.Util.Encoding;

public class PrependableWriter : ByteWriter
{
    private int _overheadSize;

    public bool AllowPrepending { get; }
    internal PrependableWriter(bool allowPrepending = true, int capacity = 0) : base(capacity)
    {
        AllowPrepending = allowPrepending;
    }

    public new void Flush()
    {
        _overheadSize = 0;
        base.Flush();
    }

    /// <summary>
    /// The value of <paramref name="overhead"/>.<see cref="MessageOverhead.Size">Size</see> will be overwritten.
    /// </summary>
    public unsafe void PrependOverhead(ref MessageOverhead overhead)
    {
        MessageOverhead.SetSize(ref overhead, Count);
        if (!AllowPrepending)
            return;

        if (Stream != null)
            throw new NotSupportedException("Prepending is not supported in stream mode.");

        if (Count == 0)
        {
            Buffer = overhead.GetBytes();
            return;
        }

        int ttl = overhead.Length;
        _overheadSize = ttl;
        byte* overheadBytes = stackalloc byte[ttl];
        overhead.GetBytes(overheadBytes, out _);

        if (Count + ttl <= Buffer.Length)
        {
            fixed (byte* ptr = Buffer)
            {
                System.Buffer.MemoryCopy(ptr, ptr + ttl, Buffer.Length - ttl, Count);
                System.Buffer.MemoryCopy(overheadBytes, ptr, ttl, ttl);
            }
        }
        else
        {
            byte[] old = Buffer;
            Buffer = new byte[Count + ttl];
            System.Buffer.BlockCopy(old, 0, Buffer, ttl, Count);
            for (int i = 0; i < ttl; ++i)
                Buffer[i] = overheadBytes[i];
        }

        Count += ttl;
    }

    /// <summary>
    /// The value of <paramref name="overhead"/>.<see cref="MessageOverhead.Size">Size</see> will be overwritten.
    /// </summary>
    public unsafe void ReplaceOverhead(ref MessageOverhead overhead)
    {
        if (_overheadSize == 0)
        {
            PrependOverhead(ref overhead);
            return;
        }

        MessageOverhead.SetSize(ref overhead, Count);
        if (!AllowPrepending)
            return;

        if (Stream != null)
            throw new NotSupportedException("Prepending is not supported in stream mode.");

        if (Count == 0)
        {
            Buffer = overhead.GetBytes();
            return;
        }

        int ttl = overhead.Length;

        byte* overheadBytes = stackalloc byte[ttl];
        overhead.GetBytes(overheadBytes, out _);

        int dif = _overheadSize - ttl;
        if (dif == 0)
        {
            fixed (byte* ptr = Buffer)
                System.Buffer.MemoryCopy(overheadBytes, ptr, ttl, ttl);
        }
        else
        {
            if (dif > 0 || Count - dif <= Buffer.Length)
            {
                fixed (byte* ptr = Buffer)
                {
                    System.Buffer.MemoryCopy(ptr + _overheadSize, ptr + ttl, Buffer.Length - ttl, Count - _overheadSize);
                    System.Buffer.MemoryCopy(overheadBytes, ptr, ttl, ttl);
                }
            }
            else
            {
                byte[] old = Buffer;
                Buffer = new byte[Count - dif];
                System.Buffer.BlockCopy(old, _overheadSize, Buffer, ttl, Count - _overheadSize);
                for (int i = 0; i < ttl; ++i)
                    Buffer[i] = overheadBytes[i];
            }
        }

        Count -= dif;
    }
}

public sealed class ByteWriterRaw<T> : PrependableWriter
{
    private readonly Writer<T> _writer;
    /// <summary>Leave <paramref name="writer"/> null to auto-fill.</summary>
    public ByteWriterRaw(Writer<T>? writer, bool allowPrepending = true, int capacity = 0) : base(allowPrepending, capacity)
    {
        _writer = writer ?? GetWriteMethodDelegate<T>();
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
public sealed class ByteWriterRaw<T1, T2> : PrependableWriter
{
    private readonly Writer<T1> _writer1;
    private readonly Writer<T2> _writer2;
    /// <summary>Leave any writer null to auto-fill.</summary>
    public ByteWriterRaw(Writer<T1>? writer1, Writer<T2>? writer2, bool shouldPrepend = true, int capacity = 0) : base(shouldPrepend, capacity)
    {
        _writer1 = writer1 ?? GetWriteMethodDelegate<T1>();
        _writer2 = writer2 ?? GetWriteMethodDelegate<T2>();
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
public sealed class ByteWriterRaw<T1, T2, T3> : PrependableWriter
{
    private readonly Writer<T1> _writer1;
    private readonly Writer<T2> _writer2;
    private readonly Writer<T3> _writer3;
    /// <summary>Leave any writer null to auto-fill.</summary>
    public ByteWriterRaw(Writer<T1>? writer1, Writer<T2>? writer2, Writer<T3>? writer3, bool shouldPrepend = true, int capacity = 0) : base(shouldPrepend, capacity)
    {
        _writer1 = writer1 ?? GetWriteMethodDelegate<T1>();
        _writer2 = writer2 ?? GetWriteMethodDelegate<T2>();
        _writer3 = writer3 ?? GetWriteMethodDelegate<T3>();
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
public sealed class ByteWriterRaw<T1, T2, T3, T4> : PrependableWriter
{
    private readonly Writer<T1> _writer1;
    private readonly Writer<T2> _writer2;
    private readonly Writer<T3> _writer3;
    private readonly Writer<T4> _writer4;
    /// <summary>Leave any writer null to auto-fill.</summary>
    public ByteWriterRaw(Writer<T1>? writer1, Writer<T2>? writer2, Writer<T3>? writer3, Writer<T4>? writer4, bool shouldPrepend = true, int capacity = 0) : base(shouldPrepend, capacity)
    {
        _writer1 = writer1 ?? GetWriteMethodDelegate<T1>();
        _writer2 = writer2 ?? GetWriteMethodDelegate<T2>();
        _writer3 = writer3 ?? GetWriteMethodDelegate<T3>();
        _writer4 = writer4 ?? GetWriteMethodDelegate<T4>();
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
public sealed class ByteWriterRaw<T1, T2, T3, T4, T5> : PrependableWriter
{
    private readonly Writer<T1> _writer1;
    private readonly Writer<T2> _writer2;
    private readonly Writer<T3> _writer3;
    private readonly Writer<T4> _writer4;
    private readonly Writer<T5> _writer5;
    /// <summary>Leave any writer null to auto-fill.</summary>
    public ByteWriterRaw(Writer<T1>? writer1, Writer<T2>? writer2, Writer<T3>? writer3, Writer<T4>? writer4, Writer<T5>? writer5, bool shouldPrepend = true, int capacity = 0) : base(shouldPrepend, capacity)
    {
        _writer1 = writer1 ?? GetWriteMethodDelegate<T1>();
        _writer2 = writer2 ?? GetWriteMethodDelegate<T2>();
        _writer3 = writer3 ?? GetWriteMethodDelegate<T3>();
        _writer4 = writer4 ?? GetWriteMethodDelegate<T4>();
        _writer5 = writer5 ?? GetWriteMethodDelegate<T5>();
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
public sealed class ByteWriterRaw<T1, T2, T3, T4, T5, T6> : PrependableWriter
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
        _writer1 = writer1 ?? GetWriteMethodDelegate<T1>();
        _writer2 = writer2 ?? GetWriteMethodDelegate<T2>();
        _writer3 = writer3 ?? GetWriteMethodDelegate<T3>();
        _writer4 = writer4 ?? GetWriteMethodDelegate<T4>();
        _writer5 = writer5 ?? GetWriteMethodDelegate<T5>();
        _writer6 = writer6 ?? GetWriteMethodDelegate<T6>();
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
public sealed class DynamicByteWriter<T1> : PrependableWriter
{
    static DynamicByteWriter()
    {
        Writer = GetWriteMethodDelegate<T1>();
    }
    private static readonly Writer<T1> Writer;
    public DynamicByteWriter(bool shouldPrepend = true, int capacity = 0) : base(shouldPrepend, capacity < 1 ? ByteEncoders.GetMinimumSize<T1>() + MessageOverhead.MaximumSize : capacity) { }
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
public sealed class DynamicByteWriter<T1, T2> : PrependableWriter
{
    static DynamicByteWriter()
    {
        Writer1 = GetWriteMethodDelegate<T1>();
        Writer2 = GetWriteMethodDelegate<T2>();
    }
    private static readonly Writer<T1> Writer1;
    private static readonly Writer<T2> Writer2;
    public DynamicByteWriter(bool shouldPrepend = true, int capacity = 0) : base(shouldPrepend, capacity < 1 ? ByteEncoders.GetMinimumSize<T1>() + ByteEncoders.GetMinimumSize<T2>() + MessageOverhead.MaximumSize : capacity) { }
    public byte[] Get(ref MessageOverhead overhead, T1 arg1, T2 arg2)
    {
        lock (this)
        {
            Flush();
            Writer1(this, arg1);
            Writer2(this, arg2);
            PrependOverhead(ref overhead);
            return ToArray();
        }
    }
    public byte[] Get(T1 arg1, T2 arg2)
    {
        lock (this)
        {
            Flush();
            Writer1(this, arg1);
            Writer2(this, arg2);
            return ToArray();
        }
    }
}
public sealed class DynamicByteWriter<T1, T2, T3> : PrependableWriter
{
    static DynamicByteWriter()
    {
        Writer1 = GetWriteMethodDelegate<T1>();
        Writer2 = GetWriteMethodDelegate<T2>();
        Writer3 = GetWriteMethodDelegate<T3>();
    }
    private static readonly Writer<T1> Writer1;
    private static readonly Writer<T2> Writer2;
    private static readonly Writer<T3> Writer3;
    public DynamicByteWriter(bool shouldPrepend = true, int capacity = 0) : base(shouldPrepend, capacity < 1 ? ByteEncoders.GetMinimumSize<T1>() + ByteEncoders.GetMinimumSize<T2>() + ByteEncoders.GetMinimumSize<T3>()
                                                                                                               + MessageOverhead.MaximumSize : capacity)
    { }
    public byte[] Get(ref MessageOverhead overhead, T1 arg1, T2 arg2, T3 arg3)
    {
        lock (this)
        {
            Flush();
            Writer1(this, arg1);
            Writer2(this, arg2);
            Writer3(this, arg3);
            PrependOverhead(ref overhead);
            return ToArray();
        }
    }
    public byte[] Get(T1 arg1, T2 arg2, T3 arg3)
    {
        lock (this)
        {
            Flush();
            Writer1(this, arg1);
            Writer2(this, arg2);
            Writer3(this, arg3);
            return ToArray();
        }
    }
}
public sealed class DynamicByteWriter<T1, T2, T3, T4> : PrependableWriter
{
    static DynamicByteWriter()
    {
        Writer1 = GetWriteMethodDelegate<T1>();
        Writer2 = GetWriteMethodDelegate<T2>();
        Writer3 = GetWriteMethodDelegate<T3>();
        Writer4 = GetWriteMethodDelegate<T4>();
    }
    private static readonly Writer<T1> Writer1;
    private static readonly Writer<T2> Writer2;
    private static readonly Writer<T3> Writer3;
    private static readonly Writer<T4> Writer4;
    public DynamicByteWriter(bool shouldPrepend = true, int capacity = 0) : base(shouldPrepend, capacity < 1 ?
                                                                                                                               ByteEncoders.GetMinimumSize<T1>() + ByteEncoders.GetMinimumSize<T2>() + ByteEncoders.GetMinimumSize<T3>() +
                                                                                                                               ByteEncoders.GetMinimumSize<T4>() + MessageOverhead.MaximumSize : capacity)
    { }
    public byte[] Get(ref MessageOverhead overhead, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
    {
        lock (this)
        {
            Flush();
            Writer1(this, arg1);
            Writer2(this, arg2);
            Writer3(this, arg3);
            Writer4(this, arg4);
            PrependOverhead(ref overhead);
            return ToArray();
        }
    }
    public byte[] Get(T1 arg1, T2 arg2, T3 arg3, T4 arg4)
    {
        lock (this)
        {
            Flush();
            Writer1(this, arg1);
            Writer2(this, arg2);
            Writer3(this, arg3);
            Writer4(this, arg4);
            return ToArray();
        }
    }
}
public sealed class DynamicByteWriter<T1, T2, T3, T4, T5> : PrependableWriter
{
    static DynamicByteWriter()
    {
        Writer1 = GetWriteMethodDelegate<T1>();
        Writer2 = GetWriteMethodDelegate<T2>();
        Writer3 = GetWriteMethodDelegate<T3>();
        Writer4 = GetWriteMethodDelegate<T4>();
        Writer5 = GetWriteMethodDelegate<T5>();
    }
    private static readonly Writer<T1> Writer1;
    private static readonly Writer<T2> Writer2;
    private static readonly Writer<T3> Writer3;
    private static readonly Writer<T4> Writer4;
    private static readonly Writer<T5> Writer5;
    public DynamicByteWriter(bool shouldPrepend = true, int capacity = 0) : base(shouldPrepend, capacity < 1 ?
                                                                                                                               ByteEncoders.GetMinimumSize<T1>() + ByteEncoders.GetMinimumSize<T2>() + ByteEncoders.GetMinimumSize<T3>() +
                                                                                                                               ByteEncoders.GetMinimumSize<T4>() + ByteEncoders.GetMinimumSize<T5>() + MessageOverhead.MaximumSize : capacity)
    { }
    public byte[] Get(ref MessageOverhead overhead, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
    {
        lock (this)
        {
            Flush();
            Writer1(this, arg1);
            Writer2(this, arg2);
            Writer3(this, arg3);
            Writer4(this, arg4);
            Writer5(this, arg5);
            PrependOverhead(ref overhead);
            return ToArray();
        }
    }
    public byte[] Get(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
    {
        lock (this)
        {
            Flush();
            Writer1(this, arg1);
            Writer2(this, arg2);
            Writer3(this, arg3);
            Writer4(this, arg4);
            Writer5(this, arg5);
            return ToArray();
        }
    }
}
public sealed class DynamicByteWriter<T1, T2, T3, T4, T5, T6> : PrependableWriter
{
    static DynamicByteWriter()
    {
        Writer1 = GetWriteMethodDelegate<T1>();
        Writer2 = GetWriteMethodDelegate<T2>();
        Writer3 = GetWriteMethodDelegate<T3>();
        Writer4 = GetWriteMethodDelegate<T4>();
        Writer5 = GetWriteMethodDelegate<T5>();
        Writer6 = GetWriteMethodDelegate<T6>();
    }
    private static readonly Writer<T1> Writer1;
    private static readonly Writer<T2> Writer2;
    private static readonly Writer<T3> Writer3;
    private static readonly Writer<T4> Writer4;
    private static readonly Writer<T5> Writer5;
    private static readonly Writer<T6> Writer6;
    public DynamicByteWriter(bool shouldPrepend = true, int capacity = 0) : base(shouldPrepend, capacity < 1 ?
                                                                                                                               ByteEncoders.GetMinimumSize<T1>() + ByteEncoders.GetMinimumSize<T2>() + ByteEncoders.GetMinimumSize<T3>() +
                                                                                                                               ByteEncoders.GetMinimumSize<T4>() + ByteEncoders.GetMinimumSize<T5>() + ByteEncoders.GetMinimumSize<T6>()
                                                                                                                               + MessageOverhead.MaximumSize : capacity)
    { }
    public byte[] Get(ref MessageOverhead overhead, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
    {
        lock (this)
        {
            Flush();
            Writer1(this, arg1);
            Writer2(this, arg2);
            Writer3(this, arg3);
            Writer4(this, arg4);
            Writer5(this, arg5);
            Writer6(this, arg6);
            PrependOverhead(ref overhead);
            return ToArray();
        }
    }
    public byte[] Get(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
    {
        lock (this)
        {
            Flush();
            Writer1(this, arg1);
            Writer2(this, arg2);
            Writer3(this, arg3);
            Writer4(this, arg4);
            Writer5(this, arg5);
            Writer6(this, arg6);
            return ToArray();
        }
    }
}
public sealed class DynamicByteWriter<T1, T2, T3, T4, T5, T6, T7> : PrependableWriter
{
    static DynamicByteWriter()
    {
        Writer1 = GetWriteMethodDelegate<T1>();
        Writer2 = GetWriteMethodDelegate<T2>();
        Writer3 = GetWriteMethodDelegate<T3>();
        Writer4 = GetWriteMethodDelegate<T4>();
        Writer5 = GetWriteMethodDelegate<T5>();
        Writer6 = GetWriteMethodDelegate<T6>();
        Writer7 = GetWriteMethodDelegate<T7>();
    }
    private static readonly Writer<T1> Writer1;
    private static readonly Writer<T2> Writer2;
    private static readonly Writer<T3> Writer3;
    private static readonly Writer<T4> Writer4;
    private static readonly Writer<T5> Writer5;
    private static readonly Writer<T6> Writer6;
    private static readonly Writer<T7> Writer7;
    public DynamicByteWriter(bool shouldPrepend = true, int capacity = 0) : base(shouldPrepend, capacity < 1 ?
                                                                                                                               ByteEncoders.GetMinimumSize<T1>() + ByteEncoders.GetMinimumSize<T2>() + ByteEncoders.GetMinimumSize<T3>() +
                                                                                                                               ByteEncoders.GetMinimumSize<T4>() + ByteEncoders.GetMinimumSize<T5>() + ByteEncoders.GetMinimumSize<T6>() +
                                                                                                                               ByteEncoders.GetMinimumSize<T7>() + MessageOverhead.MaximumSize : capacity)
    { }
    public byte[] Get(ref MessageOverhead overhead, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7)
    {
        lock (this)
        {
            Flush();
            Writer1(this, arg1);
            Writer2(this, arg2);
            Writer3(this, arg3);
            Writer4(this, arg4);
            Writer5(this, arg5);
            Writer6(this, arg6);
            Writer7(this, arg7);
            PrependOverhead(ref overhead);
            return ToArray();
        }
    }
    public byte[] Get(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7)
    {
        lock (this)
        {
            Flush();
            Writer1(this, arg1);
            Writer2(this, arg2);
            Writer3(this, arg3);
            Writer4(this, arg4);
            Writer5(this, arg5);
            Writer6(this, arg6);
            Writer7(this, arg7);
            return ToArray();
        }
    }
}
public sealed class DynamicByteWriter<T1, T2, T3, T4, T5, T6, T7, T8> : PrependableWriter
{
    static DynamicByteWriter()
    {
        Writer1 = GetWriteMethodDelegate<T1>();
        Writer2 = GetWriteMethodDelegate<T2>();
        Writer3 = GetWriteMethodDelegate<T3>();
        Writer4 = GetWriteMethodDelegate<T4>();
        Writer5 = GetWriteMethodDelegate<T5>();
        Writer6 = GetWriteMethodDelegate<T6>();
        Writer7 = GetWriteMethodDelegate<T7>();
        Writer8 = GetWriteMethodDelegate<T8>();
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
                                                                                                                               ByteEncoders.GetMinimumSize<T1>() + ByteEncoders.GetMinimumSize<T2>() + ByteEncoders.GetMinimumSize<T3>() +
                                                                                                                               ByteEncoders.GetMinimumSize<T4>() + ByteEncoders.GetMinimumSize<T5>() + ByteEncoders.GetMinimumSize<T6>() +
                                                                                                                               ByteEncoders.GetMinimumSize<T7>() + ByteEncoders.GetMinimumSize<T8>() + MessageOverhead.MaximumSize : capacity)
    { }
    public byte[] Get(ref MessageOverhead overhead, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8)
    {
        lock (this)
        {
            Flush();
            Writer1(this, arg1);
            Writer2(this, arg2);
            Writer3(this, arg3);
            Writer4(this, arg4);
            Writer5(this, arg5);
            Writer6(this, arg6);
            Writer7(this, arg7);
            Writer8(this, arg8);
            PrependOverhead(ref overhead);
            return ToArray();
        }
    }
    public byte[] Get(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8)
    {
        lock (this)
        {
            Flush();
            Writer1(this, arg1);
            Writer2(this, arg2);
            Writer3(this, arg3);
            Writer4(this, arg4);
            Writer5(this, arg5);
            Writer6(this, arg6);
            Writer7(this, arg7);
            Writer8(this, arg8);
            return ToArray();
        }
    }
}
public sealed class DynamicByteWriter<T1, T2, T3, T4, T5, T6, T7, T8, T9> : PrependableWriter
{
    static DynamicByteWriter()
    {
        Writer1 = GetWriteMethodDelegate<T1>();
        Writer2 = GetWriteMethodDelegate<T2>();
        Writer3 = GetWriteMethodDelegate<T3>();
        Writer4 = GetWriteMethodDelegate<T4>();
        Writer5 = GetWriteMethodDelegate<T5>();
        Writer6 = GetWriteMethodDelegate<T6>();
        Writer7 = GetWriteMethodDelegate<T7>();
        Writer8 = GetWriteMethodDelegate<T8>();
        Writer9 = GetWriteMethodDelegate<T9>();
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
                                                                                                                               ByteEncoders.GetMinimumSize<T1>() + ByteEncoders.GetMinimumSize<T2>() + ByteEncoders.GetMinimumSize<T3>() +
                                                                                                                               ByteEncoders.GetMinimumSize<T4>() + ByteEncoders.GetMinimumSize<T5>() + ByteEncoders.GetMinimumSize<T6>() +
                                                                                                                               ByteEncoders.GetMinimumSize<T7>() + ByteEncoders.GetMinimumSize<T8>() + ByteEncoders.GetMinimumSize<T9>()
                                                                                                                               + MessageOverhead.MaximumSize : capacity)
    { }
    public byte[] Get(ref MessageOverhead overhead, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9)
    {
        lock (this)
        {
            Flush();
            Writer1(this, arg1);
            Writer2(this, arg2);
            Writer3(this, arg3);
            Writer4(this, arg4);
            Writer5(this, arg5);
            Writer6(this, arg6);
            Writer7(this, arg7);
            Writer8(this, arg8);
            Writer9(this, arg9);
            PrependOverhead(ref overhead);
            return ToArray();
        }
    }
    public byte[] Get(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9)
    {
        lock (this)
        {
            Flush();
            Writer1(this, arg1);
            Writer2(this, arg2);
            Writer3(this, arg3);
            Writer4(this, arg4);
            Writer5(this, arg5);
            Writer6(this, arg6);
            Writer7(this, arg7);
            Writer8(this, arg8);
            Writer9(this, arg9);
            return ToArray();
        }
    }
}
public sealed class DynamicByteWriter<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> : PrependableWriter
{
    static DynamicByteWriter()
    {
        Writer1 = GetWriteMethodDelegate<T1>();
        Writer2 = GetWriteMethodDelegate<T2>();
        Writer3 = GetWriteMethodDelegate<T3>();
        Writer4 = GetWriteMethodDelegate<T4>();
        Writer5 = GetWriteMethodDelegate<T5>();
        Writer6 = GetWriteMethodDelegate<T6>();
        Writer7 = GetWriteMethodDelegate<T7>();
        Writer8 = GetWriteMethodDelegate<T8>();
        Writer9 = GetWriteMethodDelegate<T9>();
        Writer10 = GetWriteMethodDelegate<T10>();
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
                                                                                                           ByteEncoders.GetMinimumSize<T1>() + ByteEncoders.GetMinimumSize<T2>() + ByteEncoders.GetMinimumSize<T3>() +
                                                                                                           ByteEncoders.GetMinimumSize<T4>() + ByteEncoders.GetMinimumSize<T5>() + ByteEncoders.GetMinimumSize<T6>() +
                                                                                                           ByteEncoders.GetMinimumSize<T7>() + ByteEncoders.GetMinimumSize<T8>() + ByteEncoders.GetMinimumSize<T9>() +
                                                                                                           ByteEncoders.GetMinimumSize<T10>() + MessageOverhead.MaximumSize : capacity)
    { }
    public byte[] Get(ref MessageOverhead overhead, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10)
    {
        lock (this)
        {
            Flush();
            Writer1(this, arg1);
            Writer2(this, arg2);
            Writer3(this, arg3);
            Writer4(this, arg4);
            Writer5(this, arg5);
            Writer6(this, arg6);
            Writer7(this, arg7);
            Writer8(this, arg8);
            Writer9(this, arg9);
            Writer10(this, arg10);
            PrependOverhead(ref overhead);
            return ToArray();
        }
    }
    public byte[] Get(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10)
    {
        lock (this)
        {
            Flush();
            Writer1(this, arg1);
            Writer2(this, arg2);
            Writer3(this, arg3);
            Writer4(this, arg4);
            Writer5(this, arg5);
            Writer6(this, arg6);
            Writer7(this, arg7);
            Writer8(this, arg8);
            Writer9(this, arg9);
            Writer10(this, arg10);
            return ToArray();
        }
    }
}