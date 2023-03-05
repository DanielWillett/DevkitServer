using DevkitServer.Util.Encoding;
using System.Reflection;

namespace DevkitServer.Multiplayer;

public abstract class BaseNetCall
{
    internal const MessageFlags DefaultFlags = MessageFlags.None;
    internal const MessageFlags RequestFlags = DefaultFlags | MessageFlags.Request;
    internal const MessageFlags AcknowledgeRequestFlags = DefaultFlags | MessageFlags.AcknowledgeRequest;
    public readonly ushort ID;
    public string Name { get; internal set; } = null!;

    protected BaseNetCall(ushort method)
    {
        this.ID = method;
    }

    protected BaseNetCall(Delegate method)
    {
        MethodInfo info = method.GetMethodInfo();
        if (Attribute.GetCustomAttribute(info, typeof(NetCallAttribute)) is NetCallAttribute attribute)
            this.ID = attribute.MethodID;

        if (this.ID == 0)
            throw new ArgumentException($"Method provided for {info.Name} does not contain " +
                                        $"a {nameof(NetCallAttribute)} attribute.", nameof(method));
    }
    public abstract bool Read(byte[] message, out object[] parameters);
    public NetTask Listen(int timeoutMs = NetTask.DEFAULT_TIMEOUT_MS)
    {
        NetTask task = new NetTask(false, timeoutMs);
        NetFactory.RegisterListener(task, this);
        return task;
    }
    public NetTask ListenAck(int timeoutMs = NetTask.DEFAULT_TIMEOUT_MS)
    {
        NetTask task = new NetTask(false, timeoutMs);
        NetFactory.RegisterAckListener(task, this);
        return task;
    }
    internal virtual void SetThrowOnError(bool value) { }
}
public class NetCallCustom : BaseNetCall
{
    public delegate void WriterTask(ByteWriter writer);
    public delegate void Method(MessageContext context, ByteReader reader);
    public delegate Task MethodAsync(MessageContext context, ByteReader reader);
    private readonly ByteWriter _writer;
    private bool throwOnError;
    public NetCallCustom(ushort method, int capacity = 0) : base(method)
    {
        _writer = new ByteWriter(true, capacity);
    }
    public NetCallCustom(Method method, int capacity = 0) : base(method)
    {
        _writer = new ByteWriter(true, capacity);
    }
    public NetCallCustom(MethodAsync method, int capacity = 0) : base(method)
    {
        _writer = new ByteWriter(true, capacity);
    }
    internal override void SetThrowOnError(bool value) => throwOnError = value;
    public override bool Read(byte[] message, out object[] parameters)
    {
        ByteReader reader = new ByteReader { ThrowOnError = throwOnError };
        reader.LoadNew(message);
        parameters = new object[] { reader };
        return true;
    }
    public bool Read(byte[] message, out ByteReader reader)
    {
        reader = new ByteReader { ThrowOnError = throwOnError };
        reader.LoadNew(message);
        return true;
    }
    public void Invoke(ref MessageOverhead overhead,
#if SERVER
        ITransportConnection connection, 
#endif
        WriterTask task)
    {
#if SERVER
        if (connection == null)
        {
            Logger.LogError($"Error sending method {ID} to null connection.");
            return;
        }
#endif
        try
        {
            lock (_writer)
            {
                _writer.Flush();
                task(_writer);
                _writer.PrependData(ref overhead);
#if SERVER
                connection.Send(_writer.ToArray());
#else
                NetFactory.GetPlayerTransportConnection().Send(_writer.ToArray());
#endif
            }
        }
        catch (Exception ex)
        {
#if SERVER
            Logger.LogError($"Error sending method {ID} to connection {connection.GetAddressString(true)}.");
#else
            Logger.LogError($"Error sending method {ID} to server.");
#endif
            Logger.LogError(ex);
        }
    }
#if SERVER
    public void Invoke(IEnumerable<ITransportConnection> connections, WriterTask task)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags, ID, 0);
        try
        {
            byte[] bytes;
            lock (_writer)
            {
                _writer.Flush();
                task(_writer);
                _writer.PrependData(ref overhead);
                bytes = _writer.ToArray();
            }

            connections.Send(bytes);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error sending method {ID} to 1+ connection(s).");
            Logger.LogError(ex);
        }
    }
#endif
    public void Invoke(
#if SERVER
        ITransportConnection connection, 
#endif
        WriterTask task)
    {
        MessageOverhead ovh = new MessageOverhead(DefaultFlags, ID, 0);
        Invoke(ref ovh,
#if SERVER
            connection, 
#endif
            task);
    }
    public NetTask Request(BaseNetCall listener, ITransportConnection connection, WriterTask task, int timeoutMs = NetTask.DEFAULT_TIMEOUT_MS)
    {
        NetTask task2 = listener.Listen(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(RequestFlags, ID, 0, task2.requestId);
        this.Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            task);
        return task2;
    }
    public NetTask RequestAck(ITransportConnection connection, WriterTask task, int timeoutMs = NetTask.DEFAULT_TIMEOUT_MS)
    {
        NetTask task2 = ListenAck(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags, ID, 0, task2.requestId);
        this.Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            task);
        return task2;
    }
}
/// <summary> For querying only </summary>
public abstract class NetCallRaw : BaseNetCall
{
    protected NetCallRaw(ushort method) : base(method) { }
    protected NetCallRaw(Delegate method) : base(method) { }
}
/// <summary> For querying only </summary>
public abstract class DynamicNetCall : BaseNetCall
{
    protected DynamicNetCall(ushort method) : base(method) { }
    protected DynamicNetCall(Delegate method) : base(method) { }
}
public sealed class NetCall : BaseNetCall
{
    public delegate void Method(MessageContext context);
    public delegate Task MethodAsync(MessageContext context);
    public NetCall(ushort method) : base(method) { }
    public NetCall(Method method) : base(method) { }
    public NetCall(MethodAsync method) : base(method) { }
    private byte[]? _bytes;
    public void Invoke(
#if SERVER
        ITransportConnection connection
#endif
        )
    {
        _bytes ??= new MessageOverhead(DefaultFlags, ID, 0).GetBytes();
#if SERVER
        connection.Send(_bytes);
#else
        NetFactory.GetPlayerTransportConnection().Send(_bytes);
#endif
    }
    public void Invoke(ref MessageOverhead overhead
#if SERVER
        , ITransportConnection connection
#endif
        )
    {
#if SERVER
        connection.Send(overhead.GetBytes());
#else
        NetFactory.GetPlayerTransportConnection().Send(overhead.GetBytes());
#endif
    }
#if SERVER
    public void Invoke(IEnumerable<ITransportConnection> connections)
    {
        _bytes ??= new MessageOverhead(DefaultFlags, ID, 0).GetBytes();
        connections.Send(_bytes);
    }
#endif
    public bool Read(byte[] message) => true;
    public override bool Read(byte[] message, out object[] parameters)
    {
        parameters = Array.Empty<object>();
        return true;
    }
    public NetTask Request(BaseNetCall listener,
#if SERVER
        ITransportConnection connection, 
#endif
        int TimeoutMS = NetTask.DEFAULT_TIMEOUT_MS)
    {
        NetTask task = listener.Listen(TimeoutMS);
        MessageOverhead overhead = new MessageOverhead(RequestFlags, ID, 0, task.requestId);
        this.Invoke(ref overhead
#if SERVER
            , connection
#endif
        );
        return task;
    }
    public NetTask RequestAck(
#if SERVER
        ITransportConnection connection, 
#endif
        int TimeoutMS = NetTask.DEFAULT_TIMEOUT_MS)
    {
        NetTask task = ListenAck(TimeoutMS);
        MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags, ID, 0, task.requestId);
        this.Invoke(ref overhead
#if SERVER
            , connection
#endif
            );
        return task;
    }
}
public sealed class NetCallRaw<T> : NetCallRaw
{
    private readonly ByteReaderRaw<T> _reader;
    private readonly ByteWriterRaw<T> _writer;
    public delegate void Method(MessageContext context, T arg1);
    public delegate Task MethodAsync(MessageContext context, T arg1);
    /// <summary>Leave <paramref name="reader"/> or <paramref name="writer"/> null to auto-fill.</summary>
    public NetCallRaw(ushort method, ByteReader.Reader<T>? reader, ByteWriter.Writer<T>? writer, int capacity = 0) : base(method)
    {
        this._writer = new ByteWriterRaw<T>(writer, capacity: capacity);
        this._reader = new ByteReaderRaw<T>(reader);
    }
    /// <summary>Leave <paramref name="reader"/> or <paramref name="writer"/> null to auto-fill.</summary>
    public NetCallRaw(Method method, ByteReader.Reader<T>? reader, ByteWriter.Writer<T>? writer, int capacity = 0) : base(method)
    {
        _writer = new ByteWriterRaw<T>(writer, capacity: capacity);
        _reader = new ByteReaderRaw<T>(reader);
    }
    /// <summary>Leave <paramref name="reader"/> or <paramref name="writer"/> null to auto-fill.</summary>
    public NetCallRaw(MethodAsync method, ByteReader.Reader<T>? reader, ByteWriter.Writer<T>? writer, int capacity = 0) : base(method)
    {
        _writer = new ByteWriterRaw<T>(writer, capacity: capacity);
        _reader = new ByteReaderRaw<T>(reader);
    }
    internal override void SetThrowOnError(bool value) => _reader.ThrowOnError = value;
    public void Invoke(ref MessageOverhead overhead,
#if SERVER
        ITransportConnection connection, 
#endif
        T arg)
    {
#if SERVER
        if (connection == null)
        {
            Logger.LogError($"Error sending method {ID} to null connection.");
            return;
        }
#endif
        try
        {
#if SERVER
            connection.Send(_writer.Get(ref overhead, arg));
#else
            NetFactory.GetPlayerTransportConnection().Send(_writer.Get(ref overhead, arg));
#endif
        }
        catch (Exception ex)
        {
#if SERVER
            Logger.LogError($"Error sending method {ID} to connection {connection.GetAddressString(true)}.");
#else
            Logger.LogError($"Error sending method {ID} to server.");
#endif
            Logger.LogError(ex);
            Logger.LogError(ex);
        }
    }
    public void Invoke(
#if SERVER
        ITransportConnection connection, 
#endif
        T arg)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags, ID, 0);
        Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg);
    }
#if SERVER
    public void Invoke(IEnumerable<ITransportConnection> connections, T arg)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags, ID, 0);
        byte[] bytes = _writer.Get(ref overhead, arg);
        try
        {
            connections.Send(bytes);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error sending method {ID} to 1+ connection(s).");
            Logger.LogError(ex);
        }
    }
#endif
    public bool Read(byte[] message, out T arg)
    {
        try
        {
            return _reader.Read(message, out arg);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error reading method {ID}.");
            Logger.LogError(ex);
            arg = default!;
            return false;
        }
    }
    public override bool Read(byte[] message, out object[] parameters)
    {
        bool success = Read(message, out T a1);
        parameters = success ? new object[] { a1! } : Array.Empty<object>();
        return success;
    }
    public NetTask Request(BaseNetCall listener,
#if SERVER
        ITransportConnection connection, 
#endif
        T arg, int TimeoutMS = NetTask.DEFAULT_TIMEOUT_MS)
    {
        NetTask task = listener.Listen(TimeoutMS);
        MessageOverhead overhead = new MessageOverhead(RequestFlags, ID, 0, task.requestId);
        this.Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg);
        return task;
    }
    public NetTask RequestAck(
#if SERVER
        ITransportConnection connection, 
#endif
        T arg, int TimeoutMS = NetTask.DEFAULT_TIMEOUT_MS)
    {
        NetTask task = ListenAck(TimeoutMS);
        MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags, ID, 0, task.requestId);
        this.Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg);
        return task;
    }
}
/// <summary>Leave any reader or writer null to auto-fill.</summary>
public sealed class NetCallRaw<T1, T2> : NetCallRaw
{
    private readonly ByteReaderRaw<T1, T2> _reader;
    private readonly ByteWriterRaw<T1, T2> _writer;
    public delegate void Method(MessageContext context, T1 arg1, T2 arg2);
    public delegate Task MethodAsync(MessageContext context, T1 arg1, T2 arg2);
    /// <summary>Leave any of the readers or writers null to auto-fill.</summary>
    public NetCallRaw(ushort method, ByteReader.Reader<T1>? reader1, ByteReader.Reader<T2>? reader2, ByteWriter.Writer<T1>? writer1, ByteWriter.Writer<T2>? writer2, int capacity = 0) : base(method)
    {
        this._writer = new ByteWriterRaw<T1, T2>(writer1, writer2, capacity: capacity);
        this._reader = new ByteReaderRaw<T1, T2>(reader1, reader2);
    }
    /// <summary>Leave any of the readers or writers null to auto-fill.</summary>
    public NetCallRaw(Method method, ByteReader.Reader<T1>? reader1, ByteReader.Reader<T2>? reader2, ByteWriter.Writer<T1>? writer1, ByteWriter.Writer<T2>? writer2, int capacity = 0) : base(method)
    {
        this._writer = new ByteWriterRaw<T1, T2>(writer1, writer2, capacity: capacity);
        this._reader = new ByteReaderRaw<T1, T2>(reader1, reader2);
    }
    /// <summary>Leave any of the readers or writers null to auto-fill.</summary>
    public NetCallRaw(MethodAsync method, ByteReader.Reader<T1>? reader1, ByteReader.Reader<T2>? reader2, ByteWriter.Writer<T1>? writer1, ByteWriter.Writer<T2>? writer2, int capacity = 0) : base(method)
    {
        this._writer = new ByteWriterRaw<T1, T2>(writer1, writer2, capacity: capacity);
        this._reader = new ByteReaderRaw<T1, T2>(reader1, reader2);
    }
    internal override void SetThrowOnError(bool value) => _reader.ThrowOnError = value;
    public void Invoke(ref MessageOverhead overhead,
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2)
    {
#if SERVER
        if (connection == null)
        {
            Logger.LogError($"Error sending method {ID} to null connection.");
            return;
        }
#endif
        try
        {
#if SERVER
            connection.Send(_writer.Get(ref overhead, arg1, arg2));
#else
            NetFactory.GetPlayerTransportConnection().Send(_writer.Get(ref overhead, arg1, arg2));
#endif
        }
        catch (Exception ex)
        {
#if SERVER
            Logger.LogError($"Error sending method {ID} to connection {connection.GetAddressString(true)}.");
#else
            Logger.LogError($"Error sending method {ID} to server.");
#endif
            Logger.LogError(ex);
            Logger.LogError(ex);
        }
    }
    public void Invoke(
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags, ID, 0);
        Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg1, arg2);
    }
#if SERVER
    public void Invoke(IEnumerable<ITransportConnection> connections, T1 arg1, T2 arg2)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags, ID, 0);
        byte[] bytes = _writer.Get(ref overhead, arg1, arg2);
        try
        {
            connections.Send(bytes);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error sending method {ID} to 1+ connection(s).");
            Logger.LogError(ex);
        }
    }
#endif
    public bool Read(byte[] message, out T1 arg1, out T2 arg2)
    {
        try
        {
            return _reader.Read(message, out arg1, out arg2);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error reading method {ID}.");
            Logger.LogError(ex);
            arg1 = default!;
            arg2 = default!;
            return false;
        }
    }
    public override bool Read(byte[] message, out object[] parameters)
    {
        bool success = Read(message, out T1 a1, out T2 a2);
        parameters = success ? new object[] { a1!, a2! } : Array.Empty<object>();
        return success;
    }
    public NetTask Request(BaseNetCall listener,
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2, int TimeoutMS = NetTask.DEFAULT_TIMEOUT_MS)
    {
        NetTask task = listener.Listen(TimeoutMS);
        MessageOverhead overhead = new MessageOverhead(RequestFlags, ID, 0, task.requestId);
        this.Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg1, arg2);
        return task;
    }
    public NetTask RequestAck(
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2, int TimeoutMS = NetTask.DEFAULT_TIMEOUT_MS)
    {
        NetTask task = ListenAck(TimeoutMS);
        MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags, ID, 0, task.requestId);
        this.Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg1, arg2);
        return task;
    }
}
/// <summary>Leave any reader or writer null to auto-fill.</summary>
public sealed class NetCallRaw<T1, T2, T3> : NetCallRaw
{
    private readonly ByteReaderRaw<T1, T2, T3> _reader;
    private readonly ByteWriterRaw<T1, T2, T3> _writer;
    public delegate void Method(MessageContext context, T1 arg1, T2 arg2, T3 arg3);
    public delegate Task MethodAsync(MessageContext context, T1 arg1, T2 arg2, T3 arg3);
    /// <summary>Leave any of the readers or writers null to auto-fill.</summary>
    public NetCallRaw(ushort method, ByteReader.Reader<T1>? reader1, ByteReader.Reader<T2>? reader2, ByteReader.Reader<T3>? reader3, ByteWriter.Writer<T1>? writer1, ByteWriter.Writer<T2>? writer2, ByteWriter.Writer<T3>? writer3, int capacity = 0) : base(method)
    {
        this._writer = new ByteWriterRaw<T1, T2, T3>(writer1, writer2, writer3, capacity: capacity);
        this._reader = new ByteReaderRaw<T1, T2, T3>(reader1, reader2, reader3);
    }
    /// <summary>Leave any of the readers or writers null to auto-fill.</summary>
    public NetCallRaw(Method method, ByteReader.Reader<T1>? reader1, ByteReader.Reader<T2>? reader2, ByteReader.Reader<T3>? reader3, ByteWriter.Writer<T1>? writer1, ByteWriter.Writer<T2>? writer2, ByteWriter.Writer<T3>? writer3, int capacity = 0) : base(method)
    {
        this._writer = new ByteWriterRaw<T1, T2, T3>(writer1, writer2, writer3, capacity: capacity);
        this._reader = new ByteReaderRaw<T1, T2, T3>(reader1, reader2, reader3);
    }
    /// <summary>Leave any of the readers or writers null to auto-fill.</summary>
    public NetCallRaw(MethodAsync method, ByteReader.Reader<T1>? reader1, ByteReader.Reader<T2>? reader2, ByteReader.Reader<T3>? reader3, ByteWriter.Writer<T1>? writer1, ByteWriter.Writer<T2>? writer2, ByteWriter.Writer<T3>? writer3, int capacity = 0) : base(method)
    {
        this._writer = new ByteWriterRaw<T1, T2, T3>(writer1, writer2, writer3, capacity: capacity);
        this._reader = new ByteReaderRaw<T1, T2, T3>(reader1, reader2, reader3);
    }
    internal override void SetThrowOnError(bool value) => _reader.ThrowOnError = value;
    public void Invoke(ref MessageOverhead overhead,
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2, T3 arg3)
    {
#if SERVER
        if (connection == null)
        {
            Logger.LogError($"Error sending method {ID} to null connection.");
            return;
        }
#endif
        try
        {
#if SERVER
            connection.Send(_writer.Get(ref overhead, arg1, arg2, arg3));
#else
            NetFactory.GetPlayerTransportConnection().Send(_writer.Get(ref overhead, arg1, arg2, arg3));
#endif
        }
        catch (Exception ex)
        {
#if SERVER
            Logger.LogError($"Error sending method {ID} to connection {connection.GetAddressString(true)}.");
#else
            Logger.LogError($"Error sending method {ID} to server.");
#endif
            Logger.LogError(ex);
            Logger.LogError(ex);
        }
    }
    public void Invoke(
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2, T3 arg3)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags, ID, 0);
        Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg1, arg2, arg3);
    }
#if SERVER
    public void Invoke(IEnumerable<ITransportConnection> connections, T1 arg1, T2 arg2, T3 arg3)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags, ID, 0);
        byte[] bytes = _writer.Get(ref overhead, arg1, arg2, arg3);
        try
        {
            connections.Send(bytes);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error sending method {ID} to 1+ connection(s).");
            Logger.LogError(ex);
        }
    }
#endif
    public bool Read(byte[] message, out T1 arg1, out T2 arg2, out T3 arg3)
    {
        try
        {
            return _reader.Read(message, out arg1, out arg2, out arg3);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error reading method {ID}.");
            Logger.LogError(ex);
            arg1 = default!;
            arg2 = default!;
            arg3 = default!;
            return false;
        }
    }
    public override bool Read(byte[] message, out object[] parameters)
    {
        bool success = Read(message, out T1 a1, out T2 a2, out T3 a3);
        parameters = success ? new object[] { a1!, a2!, a3! } : Array.Empty<object>();
        return success;
    }
    public NetTask Request(BaseNetCall listener,
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2, T3 arg3, int TimeoutMS = NetTask.DEFAULT_TIMEOUT_MS)
    {
        NetTask task = listener.Listen(TimeoutMS);
        MessageOverhead overhead = new MessageOverhead(RequestFlags, ID, 0, task.requestId);
        this.Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg1, arg2, arg3);
        return task;
    }
    public NetTask RequestAck(
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2, T3 arg3, int TimeoutMS = NetTask.DEFAULT_TIMEOUT_MS)
    {
        NetTask task = ListenAck(TimeoutMS);
        MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags, ID, 0, task.requestId);
        this.Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg1, arg2, arg3);
        return task;
    }
}
/// <summary>Leave any reader or writer null to auto-fill.</summary>
public sealed class NetCallRaw<T1, T2, T3, T4> : NetCallRaw
{
    private readonly ByteReaderRaw<T1, T2, T3, T4> _reader;
    private readonly ByteWriterRaw<T1, T2, T3, T4> _writer;
    public delegate void Method(MessageContext context, T1 arg1, T2 arg2, T3 arg3, T4 arg4);
    public delegate Task MethodAsync(MessageContext context, T1 arg1, T2 arg2, T3 arg3, T4 arg4);
    /// <summary>Leave any of the readers or writers null to auto-fill.</summary>
    public NetCallRaw(ushort method, ByteReader.Reader<T1>? reader1, ByteReader.Reader<T2>? reader2, ByteReader.Reader<T3>? reader3, ByteReader.Reader<T4>? reader4, ByteWriter.Writer<T1>? writer1, ByteWriter.Writer<T2>? writer2, ByteWriter.Writer<T3>? writer3, ByteWriter.Writer<T4>? writer4, int capacity = 0) : base(method)
    {
        this._writer = new ByteWriterRaw<T1, T2, T3, T4>(writer1, writer2, writer3, writer4, capacity: capacity);
        this._reader = new ByteReaderRaw<T1, T2, T3, T4>(reader1, reader2, reader3, reader4);
    }
    /// <summary>Leave any of the readers or writers null to auto-fill.</summary>
    public NetCallRaw(Method method, ByteReader.Reader<T1>? reader1, ByteReader.Reader<T2>? reader2, ByteReader.Reader<T3>? reader3, ByteReader.Reader<T4>? reader4, ByteWriter.Writer<T1>? writer1, ByteWriter.Writer<T2>? writer2, ByteWriter.Writer<T3>? writer3, ByteWriter.Writer<T4>? writer4, int capacity = 0) : base(method)
    {
        this._writer = new ByteWriterRaw<T1, T2, T3, T4>(writer1, writer2, writer3, writer4, capacity: capacity);
        this._reader = new ByteReaderRaw<T1, T2, T3, T4>(reader1, reader2, reader3, reader4);
    }
    /// <summary>Leave any of the readers or writers null to auto-fill.</summary>
    public NetCallRaw(MethodAsync method, ByteReader.Reader<T1>? reader1, ByteReader.Reader<T2>? reader2, ByteReader.Reader<T3>? reader3, ByteReader.Reader<T4>? reader4, ByteWriter.Writer<T1>? writer1, ByteWriter.Writer<T2>? writer2, ByteWriter.Writer<T3>? writer3, ByteWriter.Writer<T4>? writer4, int capacity = 0) : base(method)
    {
        this._writer = new ByteWriterRaw<T1, T2, T3, T4>(writer1, writer2, writer3, writer4, capacity: capacity);
        this._reader = new ByteReaderRaw<T1, T2, T3, T4>(reader1, reader2, reader3, reader4);
    }
    internal override void SetThrowOnError(bool value) => _reader.ThrowOnError = value;
    public void Invoke(ref MessageOverhead overhead,
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2, T3 arg3, T4 arg4)
    {
#if SERVER
        if (connection == null)
        {
            Logger.LogError($"Error sending method {ID} to null connection.");
            return;
        }
#endif
        try
        {
#if SERVER
            connection.Send(_writer.Get(ref overhead, arg1, arg2, arg3, arg4));
#else
            NetFactory.GetPlayerTransportConnection().Send(_writer.Get(ref overhead, arg1, arg2, arg3, arg4));
#endif
        }
        catch (Exception ex)
        {
#if SERVER
            Logger.LogError($"Error sending method {ID} to connection {connection.GetAddressString(true)}.");
#else
            Logger.LogError($"Error sending method {ID} to server.");
#endif
            Logger.LogError(ex);
            Logger.LogError(ex);
        }
    }
    public void Invoke(
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2, T3 arg3, T4 arg4)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags, ID, 0);
        Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg1, arg2, arg3, arg4);
    }
#if SERVER
    public void Invoke(IEnumerable<ITransportConnection> connections, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags, ID, 0);
        byte[] bytes = _writer.Get(ref overhead, arg1, arg2, arg3, arg4);
        try
        {
            connections.Send(bytes);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error sending method {ID} to 1+ connection(s).");
            Logger.LogError(ex);
        }
    }
#endif
    public bool Read(byte[] message, out T1 arg1, out T2 arg2, out T3 arg3, out T4 arg4)
    {
        try
        {
            return _reader.Read(message, out arg1, out arg2, out arg3, out arg4);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error reading method {ID}.");
            Logger.LogError(ex);
            arg1 = default!;
            arg2 = default!;
            arg3 = default!;
            arg4 = default!;
            return false;
        }
    }
    public override bool Read(byte[] message, out object[] parameters)
    {
        bool success = Read(message, out T1 a1, out T2 a2, out T3 a3, out T4 a4);
        parameters = success ? new object[] { a1!, a2!, a3!, a4! } : Array.Empty<object>();
        return success;
    }
    public NetTask Request(BaseNetCall listener,
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2, T3 arg3, T4 arg4, int TimeoutMS = NetTask.DEFAULT_TIMEOUT_MS)
    {
        NetTask task = listener.Listen(TimeoutMS);
        MessageOverhead overhead = new MessageOverhead(RequestFlags, ID, 0, task.requestId);
        this.Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg1, arg2, arg3, arg4);
        return task;
    }
    public NetTask RequestAck(
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2, T3 arg3, T4 arg4, int TimeoutMS = NetTask.DEFAULT_TIMEOUT_MS)
    {
        NetTask task = ListenAck(TimeoutMS);
        MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags, ID, 0, task.requestId);
        this.Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg1, arg2, arg3, arg4);
        return task;
    }
}
public sealed class NetCall<T> : DynamicNetCall
{
    private readonly DynamicByteReader<T> _reader;
    private readonly DynamicByteWriter<T> _writer;
    public delegate void Method(MessageContext context, T arg1);
    public delegate Task MethodAsync(MessageContext context, T arg1);
    public NetCall(ushort method, int capacity = 0) : base(method)
    {
        _reader = new DynamicByteReader<T>();
        _writer = new DynamicByteWriter<T>(capacity: capacity);
    }
    public NetCall(Method method, int capacity = 0) : base(method)
    {
        _reader = new DynamicByteReader<T>();
        _writer = new DynamicByteWriter<T>(capacity: capacity);
    }
    public NetCall(MethodAsync method, int capacity = 0) : base(method)
    {
        _reader = new DynamicByteReader<T>();
        _writer = new DynamicByteWriter<T>(capacity: capacity);
    }
    internal override void SetThrowOnError(bool value) => _reader.ThrowOnError = value;
    public void Invoke(ref MessageOverhead overhead,
#if SERVER
        ITransportConnection connection, 
#endif
        T arg)
    {
#if SERVER
        if (connection == null)
        {
            Logger.LogError($"Error sending method {ID} to null connection.");
            return;
        }
#endif
        try
        {
#if SERVER
            connection.Send(_writer.Get(ref overhead, arg));
#else
            NetFactory.GetPlayerTransportConnection().Send(_writer.Get(ref overhead, arg));
#endif
        }
        catch (Exception ex)
        {
#if SERVER
            Logger.LogError($"Error sending method {ID} to connection {connection.GetAddressString(true)}.");
#else
            Logger.LogError($"Error sending method {ID} to server.");
#endif
            Logger.LogError(ex);
        }
    }
    public void Invoke(
#if SERVER
        ITransportConnection connection,
#endif
        T arg)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags, ID, 0);
        Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg);
    }
#if SERVER
    public void Invoke(IEnumerable<ITransportConnection> connections, T arg)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags, ID, 0);
        byte[] bytes = _writer.Get(ref overhead, arg);
        try
        {
            connections.Send(bytes);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error sending method {ID} to 1+ connection(s).");
            Logger.LogError(ex);
        }
    }
#endif
    public bool Read(byte[] message, out T arg1)
    {
        try
        {
            return _reader.Read(message, out arg1);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error reading method {ID}.");
            Logger.LogError(ex);
            arg1 = default!;
            return false;
        }
    }
    public override bool Read(byte[] message, out object[] parameters)
    {
        bool success = Read(message, out T a1);
        parameters = success ? new object[] { a1! } : Array.Empty<object>();
        return success;
    }
    public NetTask Request(BaseNetCall listener,
#if SERVER
        ITransportConnection connection, 
#endif
        T arg, int TimeoutMS = NetTask.DEFAULT_TIMEOUT_MS)
    {
        NetTask task = listener.Listen(TimeoutMS);
        MessageOverhead overhead = new MessageOverhead(RequestFlags, ID, 0, task.requestId);
        this.Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg);
        return task;
    }
    public NetTask RequestAck(
#if SERVER
        ITransportConnection connection, 
#endif
        T arg, int TimeoutMS = NetTask.DEFAULT_TIMEOUT_MS)
    {
        NetTask task = ListenAck(TimeoutMS);
        MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags, ID, 0, task.requestId);
        this.Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg);
        return task;
    }
}
public sealed class NetCall<T1, T2> : DynamicNetCall
{
    private readonly DynamicByteReader<T1, T2> _reader;
    private readonly DynamicByteWriter<T1, T2> _writer;
    public delegate void Method(MessageContext context, T1 arg1, T2 arg2);
    public delegate Task MethodAsync(MessageContext context, T1 arg1, T2 arg2);
    public NetCall(ushort method, int capacity = 0) : base(method)
    {
        _reader = new DynamicByteReader<T1, T2>();
        _writer = new DynamicByteWriter<T1, T2>(capacity: capacity);
    }
    public NetCall(Method method, int capacity = 0) : base(method)
    {
        _reader = new DynamicByteReader<T1, T2>();
        _writer = new DynamicByteWriter<T1, T2>(capacity: capacity);
    }
    public NetCall(MethodAsync method, int capacity = 0) : base(method)
    {
        _reader = new DynamicByteReader<T1, T2>();
        _writer = new DynamicByteWriter<T1, T2>(capacity: capacity);
    }
    internal override void SetThrowOnError(bool value) => _reader.ThrowOnError = value;
    public void Invoke(ref MessageOverhead overhead,
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2)
    {
#if SERVER
        if (connection == null)
        {
            Logger.LogError($"Error sending method {ID} to null connection.");
            return;
        }
#endif
        try
        {
#if SERVER
            connection.Send(_writer.Get(ref overhead, arg1, arg2));
#else
            NetFactory.GetPlayerTransportConnection().Send(_writer.Get(ref overhead, arg1, arg2));
#endif
        }
        catch (Exception ex)
        {
#if SERVER
            Logger.LogError($"Error sending method {ID} to connection {connection.GetAddressString(true)}.");
#else
            Logger.LogError($"Error sending method {ID} to server.");
#endif
            Logger.LogError(ex);
        }
    }
    public void Invoke(
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags, ID, 0);
        Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg1, arg2);
    }
#if SERVER
    public void Invoke(IEnumerable<ITransportConnection> connections, T1 arg1, T2 arg2)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags, ID, 0);
        byte[] bytes = _writer.Get(ref overhead, arg1, arg2);
        try
        {
            connections.Send(bytes);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error sending method {ID} to 1+ connection(s).");
            Logger.LogError(ex);
        }
    }
#endif
    public bool Read(byte[] message, out T1 arg1, out T2 arg2)
    {
        try
        {
            return _reader.Read(message, out arg1, out arg2);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error reading method {ID}.");
            Logger.LogError(ex);
            arg1 = default!;
            arg2 = default!;
            return false;
        }
    }
    public override bool Read(byte[] message, out object[] parameters)
    {
        bool success = Read(message, out T1 a1, out T2 a2);
        parameters = success ? new object[] { a1!, a2! } : Array.Empty<object>();
        return success;
    }
    public NetTask Request(BaseNetCall listener,
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2, int TimeoutMS = NetTask.DEFAULT_TIMEOUT_MS)
    {
        NetTask task = listener.Listen(TimeoutMS);
        MessageOverhead overhead = new MessageOverhead(RequestFlags, ID, 0, task.requestId);
        this.Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg1, arg2);
        return task;
    }
    public NetTask RequestAck(
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2, int TimeoutMS = NetTask.DEFAULT_TIMEOUT_MS)
    {
        NetTask task = ListenAck(TimeoutMS);
        MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags, ID, 0, task.requestId);
        this.Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg1, arg2);
        return task;
    }
}
public sealed class NetCall<T1, T2, T3> : DynamicNetCall
{
    private readonly DynamicByteReader<T1, T2, T3> _reader;
    private readonly DynamicByteWriter<T1, T2, T3> _writer;
    public delegate void Method(MessageContext context, T1 arg1, T2 arg2, T3 arg3);
    public delegate Task MethodAsync(MessageContext context, T1 arg1, T2 arg2, T3 arg3);
    public NetCall(ushort method, int capacity = 0) : base(method)
    {
        _reader = new DynamicByteReader<T1, T2, T3>();
        _writer = new DynamicByteWriter<T1, T2, T3>(capacity: capacity);
    }
    public NetCall(Method method, int capacity = 0) : base(method)
    {
        _reader = new DynamicByteReader<T1, T2, T3>();
        _writer = new DynamicByteWriter<T1, T2, T3>(capacity: capacity);
    }
    public NetCall(MethodAsync method, int capacity = 0) : base(method)
    {
        _reader = new DynamicByteReader<T1, T2, T3>();
        _writer = new DynamicByteWriter<T1, T2, T3>(capacity: capacity);
    }
    internal override void SetThrowOnError(bool value) => _reader.ThrowOnError = value;
    public void Invoke(ref MessageOverhead overhead,
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2, T3 arg3)
    {
#if SERVER
        if (connection == null)
        {
            Logger.LogError($"Error sending method {ID} to null connection.");
            return;
        }
#endif
        try
        {
#if SERVER
            connection.Send(_writer.Get(ref overhead, arg1, arg2, arg3));
#else
            NetFactory.GetPlayerTransportConnection().Send(_writer.Get(ref overhead, arg1, arg2, arg3));
#endif
        }
        catch (Exception ex)
        {
#if SERVER
            Logger.LogError($"Error sending method {ID} to connection {connection.GetAddressString(true)}.");
#else
            Logger.LogError($"Error sending method {ID} to server.");
#endif
            Logger.LogError(ex);
        }
    }
    public void Invoke(
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2, T3 arg3)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags, ID, 0);
        Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg1, arg2, arg3);
    }
#if SERVER
    public void Invoke(IEnumerable<ITransportConnection> connections, T1 arg1, T2 arg2, T3 arg3)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags, ID, 0);
        byte[] bytes = _writer.Get(ref overhead, arg1, arg2, arg3);
        try
        {
            connections.Send(bytes);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error sending method {ID} to 1+ connection(s).");
            Logger.LogError(ex);
        }
    }
#endif
    public bool Read(byte[] message, out T1 arg1, out T2 arg2, out T3 arg3)
    {
        try
        {
            return _reader.Read(message, out arg1, out arg2, out arg3);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error reading method {ID}.");
            Logger.LogError(ex);
            arg1 = default!;
            arg2 = default!;
            arg3 = default!;
            return false;
        }
    }
    public override bool Read(byte[] message, out object[] parameters)
    {
        bool success = Read(message, out T1 arg1, out T2 arg2, out T3 arg3);
        parameters = success ? new object[] { arg1!, arg2!, arg3! } : Array.Empty<object>();
        return success;
    }
    public NetTask Request(BaseNetCall listener,
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2, T3 arg3, int TimeoutMS = NetTask.DEFAULT_TIMEOUT_MS)
    {
        NetTask task = listener.Listen(TimeoutMS);
        MessageOverhead overhead = new MessageOverhead(RequestFlags, ID, 0, task.requestId);
        this.Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg1, arg2, arg3);
        return task;
    }
    public NetTask RequestAck(
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2, T3 arg3, int TimeoutMS = NetTask.DEFAULT_TIMEOUT_MS)
    {
        NetTask task = ListenAck(TimeoutMS);
        MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags, ID, 0, task.requestId);
        this.Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg1, arg2, arg3);
        return task;
    }
}
public sealed class NetCall<T1, T2, T3, T4> : DynamicNetCall
{
    private readonly DynamicByteReader<T1, T2, T3, T4> _reader;
    private readonly DynamicByteWriter<T1, T2, T3, T4> _writer;
    public delegate void Method(MessageContext context, T1 arg1, T2 arg2, T3 arg3, T4 arg4);
    public delegate Task MethodAsync(MessageContext context, T1 arg1, T2 arg2, T3 arg3, T4 arg4);
    public NetCall(ushort method, int capacity = 0) : base(method)
    {
        _reader = new DynamicByteReader<T1, T2, T3, T4>();
        _writer = new DynamicByteWriter<T1, T2, T3, T4>(capacity: capacity);
    }
    public NetCall(Method method, int capacity = 0) : base(method)
    {
        _reader = new DynamicByteReader<T1, T2, T3, T4>();
        _writer = new DynamicByteWriter<T1, T2, T3, T4>(capacity: capacity);
    }
    public NetCall(MethodAsync method, int capacity = 0) : base(method)
    {
        _reader = new DynamicByteReader<T1, T2, T3, T4>();
        _writer = new DynamicByteWriter<T1, T2, T3, T4>(capacity: capacity);
    }
    internal override void SetThrowOnError(bool value) => _reader.ThrowOnError = value;
    public void Invoke(ref MessageOverhead overhead,
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2, T3 arg3, T4 arg4)
    {
#if SERVER
        if (connection == null)
        {
            Logger.LogError($"Error sending method {ID} to null connection.");
            return;
        }
#endif
        try
        {
#if SERVER
            connection.Send(_writer.Get(ref overhead, arg1, arg2, arg3, arg4));
#else
            NetFactory.GetPlayerTransportConnection().Send(_writer.Get(ref overhead, arg1, arg2, arg3, arg4));
#endif
        }
        catch (Exception ex)
        {
#if SERVER
            Logger.LogError($"Error sending method {ID} to connection {connection.GetAddressString(true)}.");
#else
            Logger.LogError($"Error sending method {ID} to server.");
#endif
            Logger.LogError(ex);
        }
    }
    public void Invoke(
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2, T3 arg3, T4 arg4)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags, ID, 0);
        Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg1, arg2, arg3, arg4);
    }
#if SERVER
    public void Invoke(IEnumerable<ITransportConnection> connections, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags, ID, 0);
        byte[] bytes = _writer.Get(ref overhead, arg1, arg2, arg3, arg4);
        try
        {
            connections.Send(bytes);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error sending method {ID} to 1+ connection(s).");
            Logger.LogError(ex);
        }
    }
#endif
    public bool Read(byte[] message, out T1 arg1, out T2 arg2, out T3 arg3, out T4 arg4)
    {
        try
        {
            return _reader.Read(message, out arg1, out arg2, out arg3, out arg4);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error reading method {ID}.");
            Logger.LogError(ex);
            arg1 = default!;
            arg2 = default!;
            arg3 = default!;
            arg4 = default!;
            return false;
        }
    }
    public override bool Read(byte[] message, out object[] parameters)
    {
        bool success = Read(message, out T1 arg1, out T2 arg2, out T3 arg3, out T4 arg4);
        parameters = success ? new object[] { arg1!, arg2!, arg3!, arg4! } : Array.Empty<object>();
        return success;
    }
    public NetTask Request(BaseNetCall listener,
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2, T3 arg3, T4 arg4, int TimeoutMS = NetTask.DEFAULT_TIMEOUT_MS)
    {
        NetTask task = listener.Listen(TimeoutMS);
        MessageOverhead overhead = new MessageOverhead(RequestFlags, ID, 0, task.requestId);
        this.Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg1, arg2, arg3, arg4);
        return task;
    }
    public NetTask RequestAck(
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2, T3 arg3, T4 arg4, int TimeoutMS = NetTask.DEFAULT_TIMEOUT_MS)
    {
        NetTask task = ListenAck(TimeoutMS);
        MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags, ID, 0, task.requestId);
        this.Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg1, arg2, arg3, arg4);
        return task;
    }
}
public sealed class NetCall<T1, T2, T3, T4, T5> : DynamicNetCall
{
    private readonly DynamicByteReader<T1, T2, T3, T4, T5> _reader;
    private readonly DynamicByteWriter<T1, T2, T3, T4, T5> _writer;
    public delegate void Method(MessageContext context, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5);
    public delegate Task MethodAsync(MessageContext context, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5);
    public NetCall(ushort method, int capacity = 0) : base(method)
    {
        _reader = new DynamicByteReader<T1, T2, T3, T4, T5>();
        _writer = new DynamicByteWriter<T1, T2, T3, T4, T5>(capacity: capacity);
    }
    public NetCall(Method method, int capacity = 0) : base(method)
    {
        _reader = new DynamicByteReader<T1, T2, T3, T4, T5>();
        _writer = new DynamicByteWriter<T1, T2, T3, T4, T5>(capacity: capacity);
    }
    public NetCall(MethodAsync method, int capacity = 0) : base(method)
    {
        _reader = new DynamicByteReader<T1, T2, T3, T4, T5>();
        _writer = new DynamicByteWriter<T1, T2, T3, T4, T5>(capacity: capacity);
    }
    internal override void SetThrowOnError(bool value) => _reader.ThrowOnError = value;
    public void Invoke(ref MessageOverhead overhead,
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
    {
#if SERVER
        if (connection == null)
        {
            Logger.LogError($"Error sending method {ID} to null connection.");
            return;
        }
#endif
        try
        {
#if SERVER
            connection.Send(_writer.Get(ref overhead, arg1, arg2, arg3, arg4, arg5));
#else
            NetFactory.GetPlayerTransportConnection().Send(_writer.Get(ref overhead, arg1, arg2, arg3, arg4, arg5));
#endif
        }
        catch (Exception ex)
        {
#if SERVER
            Logger.LogError($"Error sending method {ID} to connection {connection.GetAddressString(true)}.");
#else
            Logger.LogError($"Error sending method {ID} to server.");
#endif
            Logger.LogError(ex);
        }
    }
    public void Invoke(
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags, ID, 0);
        Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg1, arg2, arg3, arg4, arg5);
    }
#if SERVER
    public void Invoke(IEnumerable<ITransportConnection> connections, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags, ID, 0);
        byte[] bytes = _writer.Get(ref overhead, arg1, arg2, arg3, arg4, arg5);
        try
        {
            connections.Send(bytes);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error sending method {ID} to 1+ connection(s).");
            Logger.LogError(ex);
        }
    }
#endif
    public bool Read(byte[] message, out T1 arg1, out T2 arg2, out T3 arg3, out T4 arg4, out T5 arg5)
    {
        try
        {
            return _reader.Read(message, out arg1, out arg2, out arg3, out arg4, out arg5);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error reading method {ID}.");
            Logger.LogError(ex);
            arg1 = default!;
            arg2 = default!;
            arg3 = default!;
            arg4 = default!;
            arg5 = default!;
            return false;
        }
    }
    public override bool Read(byte[] message, out object[] parameters)
    {
        bool success = Read(message, out T1 arg1, out T2 arg2, out T3 arg3, out T4 arg4, out T5 arg5);
        parameters = success ? new object[] { arg1!, arg2!, arg3!, arg4!, arg5! } : Array.Empty<object>();
        return success;
    }
    public NetTask Request(BaseNetCall listener,
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, int TimeoutMS = NetTask.DEFAULT_TIMEOUT_MS)
    {
        NetTask task = listener.Listen(TimeoutMS);
        MessageOverhead overhead = new MessageOverhead(RequestFlags, ID, 0, task.requestId);
        this.Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg1, arg2, arg3, arg4, arg5);
        return task;
    }
    public NetTask RequestAck(
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, int TimeoutMS = NetTask.DEFAULT_TIMEOUT_MS)
    {
        NetTask task = ListenAck(TimeoutMS);
        MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags, ID, 0, task.requestId);
        this.Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg1, arg2, arg3, arg4, arg5);
        return task;
    }
}
public sealed class NetCall<T1, T2, T3, T4, T5, T6> : DynamicNetCall
{
    private readonly DynamicByteReader<T1, T2, T3, T4, T5, T6> _reader;
    private readonly DynamicByteWriter<T1, T2, T3, T4, T5, T6> _writer;
    public delegate void Method(MessageContext context, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6);
    public delegate Task MethodAsync(MessageContext context, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6);
    public NetCall(ushort method, int capacity = 0) : base(method)
    {
        _reader = new DynamicByteReader<T1, T2, T3, T4, T5, T6>();
        _writer = new DynamicByteWriter<T1, T2, T3, T4, T5, T6>(capacity: capacity);
    }
    public NetCall(Method method, int capacity = 0) : base(method)
    {
        _reader = new DynamicByteReader<T1, T2, T3, T4, T5, T6>();
        _writer = new DynamicByteWriter<T1, T2, T3, T4, T5, T6>(capacity: capacity);
    }
    public NetCall(MethodAsync method, int capacity = 0) : base(method)
    {
        _reader = new DynamicByteReader<T1, T2, T3, T4, T5, T6>();
        _writer = new DynamicByteWriter<T1, T2, T3, T4, T5, T6>(capacity: capacity);
    }
    internal override void SetThrowOnError(bool value) => _reader.ThrowOnError = value;
    public void Invoke(ref MessageOverhead overhead,
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
    {
#if SERVER
        if (connection == null)
        {
            Logger.LogError($"Error sending method {ID} to null connection.");
            return;
        }
#endif
        try
        {
#if SERVER
            connection.Send(_writer.Get(ref overhead, arg1, arg2, arg3, arg4, arg5, arg6));
#else
            NetFactory.GetPlayerTransportConnection().Send(_writer.Get(ref overhead, arg1, arg2, arg3, arg4, arg5, arg6));
#endif
        }
        catch (Exception ex)
        {
#if SERVER
            Logger.LogError($"Error sending method {ID} to connection {connection.GetAddressString(true)}.");
#else
            Logger.LogError($"Error sending method {ID} to server.");
#endif
            Logger.LogError(ex);
        }
    }
    public void Invoke(
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags, ID, 0);
        Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg1, arg2, arg3, arg4, arg5, arg6);
    }
#if SERVER
    public void Invoke(IEnumerable<ITransportConnection> connections, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags, ID, 0);
        byte[] bytes = _writer.Get(ref overhead, arg1, arg2, arg3, arg4, arg5, arg6);
        try
        {
            connections.Send(bytes);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error sending method {ID} to 1+ connection(s).");
            Logger.LogError(ex);
        }
    }
#endif
    public bool Read(byte[] message, out T1 arg1, out T2 arg2, out T3 arg3, out T4 arg4, out T5 arg5, out T6 arg6)
    {
        try
        {
            return _reader.Read(message, out arg1, out arg2, out arg3, out arg4, out arg5, out arg6);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error reading method {ID}.");
            Logger.LogError(ex);
            arg1 = default!;
            arg2 = default!;
            arg3 = default!;
            arg4 = default!;
            arg5 = default!;
            arg6 = default!;
            return false;
        }
    }
    public override bool Read(byte[] message, out object[] parameters)
    {
        bool success = Read(message, out T1 arg1, out T2 arg2, out T3 arg3, out T4 arg4, out T5 arg5, out T6 arg6);
        parameters = success ? new object[] { arg1!, arg2!, arg3!, arg4!, arg5!, arg6! } : Array.Empty<object>();
        return success;
    }
    public NetTask Request(BaseNetCall listener,
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, int TimeoutMS = NetTask.DEFAULT_TIMEOUT_MS)
    {
        NetTask task = listener.Listen(TimeoutMS);
        MessageOverhead overhead = new MessageOverhead(RequestFlags, ID, 0, task.requestId);
        this.Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg1, arg2, arg3, arg4, arg5, arg6);
        return task;
    }
    public NetTask RequestAck(
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, int TimeoutMS = NetTask.DEFAULT_TIMEOUT_MS)
    {
        NetTask task = ListenAck(TimeoutMS);
        MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags, ID, 0, task.requestId);
        this.Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg1, arg2, arg3, arg4, arg5, arg6);
        return task;
    }
}
public sealed class NetCall<T1, T2, T3, T4, T5, T6, T7> : DynamicNetCall
{
    private readonly DynamicByteReader<T1, T2, T3, T4, T5, T6, T7> _reader;
    private readonly DynamicByteWriter<T1, T2, T3, T4, T5, T6, T7> _writer;
    public delegate void Method(MessageContext context, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7);
    public delegate Task MethodAsync(MessageContext context, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7);
    public NetCall(ushort method, int capacity = 0) : base(method)
    {
        _reader = new DynamicByteReader<T1, T2, T3, T4, T5, T6, T7>();
        _writer = new DynamicByteWriter<T1, T2, T3, T4, T5, T6, T7>(capacity: capacity);
    }
    public NetCall(Method method, int capacity = 0) : base(method)
    {
        _reader = new DynamicByteReader<T1, T2, T3, T4, T5, T6, T7>();
        _writer = new DynamicByteWriter<T1, T2, T3, T4, T5, T6, T7>(capacity: capacity);
    }
    public NetCall(MethodAsync method, int capacity = 0) : base(method)
    {
        _reader = new DynamicByteReader<T1, T2, T3, T4, T5, T6, T7>();
        _writer = new DynamicByteWriter<T1, T2, T3, T4, T5, T6, T7>(capacity: capacity);
    }
    internal override void SetThrowOnError(bool value) => _reader.ThrowOnError = value;
    public void Invoke(ref MessageOverhead overhead,
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7)
    {
#if SERVER
        if (connection == null)
        {
            Logger.LogError($"Error sending method {ID} to null connection.");
            return;
        }
#endif
        try
        {
#if SERVER
            connection.Send(_writer.Get(ref overhead, arg1, arg2, arg3, arg4, arg5, arg6, arg7));
#else
            NetFactory.GetPlayerTransportConnection().Send(_writer.Get(ref overhead, arg1, arg2, arg3, arg4, arg5, arg6, arg7));
#endif
        }
        catch (Exception ex)
        {
#if SERVER
            Logger.LogError($"Error sending method {ID} to connection {connection.GetAddressString(true)}.");
#else
            Logger.LogError($"Error sending method {ID} to server.");
#endif
            Logger.LogError(ex);
        }
    }
    public void Invoke(
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags, ID, 0);
        Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg1, arg2, arg3, arg4, arg5, arg6, arg7);
    }
#if SERVER
    public void Invoke(IEnumerable<ITransportConnection> connections, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags, ID, 0);
        byte[] bytes = _writer.Get(ref overhead, arg1, arg2, arg3, arg4, arg5, arg6, arg7);
        try
        {
            connections.Send(bytes);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error sending method {ID} to 1+ connection(s).");
            Logger.LogError(ex);
        }
    }
#endif
    public bool Read(byte[] message, out T1 arg1, out T2 arg2, out T3 arg3, out T4 arg4, out T5 arg5, out T6 arg6, out T7 arg7)
    {
        try
        {
            return _reader.Read(message, out arg1, out arg2, out arg3, out arg4, out arg5, out arg6, out arg7);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error reading method {ID}.");
            Logger.LogError(ex);
            arg1 = default!;
            arg2 = default!;
            arg3 = default!;
            arg4 = default!;
            arg5 = default!;
            arg6 = default!;
            arg7 = default!;
            return false;
        }
    }
    public override bool Read(byte[] message, out object[] parameters)
    {
        bool success = Read(message, out T1 arg1, out T2 arg2, out T3 arg3, out T4 arg4, out T5 arg5, out T6 arg6, out T7 arg7);
        parameters = success ? new object[] { arg1!, arg2!, arg3!, arg4!, arg5!, arg6!, arg7! } : Array.Empty<object>();
        return success;
    }
    public NetTask Request(BaseNetCall listener,
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, int TimeoutMS = NetTask.DEFAULT_TIMEOUT_MS)
    {
        NetTask task = listener.Listen(TimeoutMS);
        MessageOverhead overhead = new MessageOverhead(RequestFlags, ID, 0, task.requestId);
        this.Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg1, arg2, arg3, arg4, arg5, arg6, arg7);
        return task;
    }
    public NetTask RequestAck(
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, int TimeoutMS = NetTask.DEFAULT_TIMEOUT_MS)
    {
        NetTask task = ListenAck(TimeoutMS);
        MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags, ID, 0, task.requestId);
        this.Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg1, arg2, arg3, arg4, arg5, arg6, arg7);
        return task;
    }
}
public sealed class NetCall<T1, T2, T3, T4, T5, T6, T7, T8> : DynamicNetCall
{
    private readonly DynamicByteReader<T1, T2, T3, T4, T5, T6, T7, T8> _reader;
    private readonly DynamicByteWriter<T1, T2, T3, T4, T5, T6, T7, T8> _writer;
    public delegate void Method(MessageContext context, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8);
    public delegate Task MethodAsync(MessageContext context, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8);
    public NetCall(ushort method, int capacity = 0) : base(method)
    {
        _reader = new DynamicByteReader<T1, T2, T3, T4, T5, T6, T7, T8>();
        _writer = new DynamicByteWriter<T1, T2, T3, T4, T5, T6, T7, T8>(capacity: capacity);
    }
    public NetCall(Method method, int capacity = 0) : base(method)
    {
        _reader = new DynamicByteReader<T1, T2, T3, T4, T5, T6, T7, T8>();
        _writer = new DynamicByteWriter<T1, T2, T3, T4, T5, T6, T7, T8>(capacity: capacity);
    }
    public NetCall(MethodAsync method, int capacity = 0) : base(method)
    {
        _reader = new DynamicByteReader<T1, T2, T3, T4, T5, T6, T7, T8>();
        _writer = new DynamicByteWriter<T1, T2, T3, T4, T5, T6, T7, T8>(capacity: capacity);
    }
    internal override void SetThrowOnError(bool value) => _reader.ThrowOnError = value;
    public void Invoke(ref MessageOverhead overhead,
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8)
    {
#if SERVER
        if (connection == null)
        {
            Logger.LogError($"Error sending method {ID} to null connection.");
            return;
        }
#endif
        try
        {
#if SERVER
            connection.Send(_writer.Get(ref overhead, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8));
#else
            NetFactory.GetPlayerTransportConnection().Send(_writer.Get(ref overhead, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8));
#endif
        }
        catch (Exception ex)
        {
#if SERVER
            Logger.LogError($"Error sending method {ID} to connection {connection.GetAddressString(true)}.");
#else
            Logger.LogError($"Error sending method {ID} to server.");
#endif
            Logger.LogError(ex);
        }
    }
    public void Invoke(
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags, ID, 0);
        Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
    }
#if SERVER
    public void Invoke(IEnumerable<ITransportConnection> connections, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags, ID, 0);
        byte[] bytes = _writer.Get(ref overhead, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
        try
        {
            connections.Send(bytes);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error sending method {ID} to 1+ connection(s).");
            Logger.LogError(ex);
        }
    }
#endif
    public bool Read(byte[] message, out T1 arg1, out T2 arg2, out T3 arg3, out T4 arg4, out T5 arg5, out T6 arg6, out T7 arg7, out T8 arg8)
    {
        try
        {
            return _reader.Read(message, out arg1, out arg2, out arg3, out arg4, out arg5, out arg6, out arg7, out arg8);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error reading method {ID}.");
            Logger.LogError(ex);
            arg1 = default!;
            arg2 = default!;
            arg3 = default!;
            arg4 = default!;
            arg5 = default!;
            arg6 = default!;
            arg7 = default!;
            arg8 = default!;
            return false;
        }
    }
    public override bool Read(byte[] message, out object[] parameters)
    {
        bool success = Read(message, out T1 arg1, out T2 arg2, out T3 arg3, out T4 arg4, out T5 arg5, out T6 arg6, out T7 arg7, out T8 arg8);
        parameters = success ? new object[] { arg1!, arg2!, arg3!, arg4!, arg5!, arg6!, arg7!, arg8! } : Array.Empty<object>();
        return success;
    }
    public NetTask Request(BaseNetCall listener,
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, int TimeoutMS = NetTask.DEFAULT_TIMEOUT_MS)
    {
        NetTask task = listener.Listen(TimeoutMS);
        MessageOverhead overhead = new MessageOverhead(RequestFlags, ID, 0, task.requestId);
        this.Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
        return task;
    }
    public NetTask RequestAck(
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, int TimeoutMS = NetTask.DEFAULT_TIMEOUT_MS)
    {
        NetTask task = ListenAck(TimeoutMS);
        MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags, ID, 0, task.requestId);
        this.Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
        return task;
    }
}
public sealed class NetCall<T1, T2, T3, T4, T5, T6, T7, T8, T9> : DynamicNetCall
{
    private readonly DynamicByteReader<T1, T2, T3, T4, T5, T6, T7, T8, T9> _reader;
    private readonly DynamicByteWriter<T1, T2, T3, T4, T5, T6, T7, T8, T9> _writer;
    public delegate void Method(MessageContext context, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9);
    public delegate Task MethodAsync(MessageContext context, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9);
    public NetCall(ushort method, int capacity = 0) : base(method)
    {
        _reader = new DynamicByteReader<T1, T2, T3, T4, T5, T6, T7, T8, T9>();
        _writer = new DynamicByteWriter<T1, T2, T3, T4, T5, T6, T7, T8, T9>(capacity: capacity);
    }
    public NetCall(Method method, int capacity = 0) : base(method)
    {
        _reader = new DynamicByteReader<T1, T2, T3, T4, T5, T6, T7, T8, T9>();
        _writer = new DynamicByteWriter<T1, T2, T3, T4, T5, T6, T7, T8, T9>(capacity: capacity);
    }
    public NetCall(MethodAsync method, int capacity = 0) : base(method)
    {
        _reader = new DynamicByteReader<T1, T2, T3, T4, T5, T6, T7, T8, T9>();
        _writer = new DynamicByteWriter<T1, T2, T3, T4, T5, T6, T7, T8, T9>(capacity: capacity);
    }
    internal override void SetThrowOnError(bool value) => _reader.ThrowOnError = value;
    public void Invoke(ref MessageOverhead overhead,
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9)
    {
#if SERVER
        if (connection == null)
        {
            Logger.LogError($"Error sending method {ID} to null connection.");
            return;
        }
#endif
        try
        {
#if SERVER
            connection.Send(_writer.Get(ref overhead, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9));
#else
            NetFactory.GetPlayerTransportConnection().Send(_writer.Get(ref overhead, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9));
#endif
        }
        catch (Exception ex)
        {
#if SERVER
            Logger.LogError($"Error sending method {ID} to connection {connection.GetAddressString(true)}.");
#else
            Logger.LogError($"Error sending method {ID} to server.");
#endif
            Logger.LogError(ex);
        }
    }
    public void Invoke(
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags, ID, 0);
        Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);
    }
#if SERVER
    public void Invoke(IEnumerable<ITransportConnection> connections, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags, ID, 0);
        byte[] bytes = _writer.Get(ref overhead, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);
        try
        {
            connections.Send(bytes);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error sending method {ID} to 1+ connection(s).");
            Logger.LogError(ex);
        }
    }
#endif
    public bool Read(byte[] message, out T1 arg1, out T2 arg2, out T3 arg3, out T4 arg4, out T5 arg5, out T6 arg6, out T7 arg7, out T8 arg8, out T9 arg9)
    {
        try
        {
            return _reader.Read(message, out arg1, out arg2, out arg3, out arg4, out arg5, out arg6, out arg7, out arg8, out arg9);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error reading method {ID}.");
            Logger.LogError(ex);
            arg1 = default!;
            arg2 = default!;
            arg3 = default!;
            arg4 = default!;
            arg5 = default!;
            arg6 = default!;
            arg7 = default!;
            arg8 = default!;
            arg9 = default!;
            return false;
        }
    }
    public override bool Read(byte[] message, out object[] parameters)
    {
        bool success = Read(message, out T1 arg1, out T2 arg2, out T3 arg3, out T4 arg4, out T5 arg5, out T6 arg6, out T7 arg7, out T8 arg8, out T9 arg9);
        parameters = success ? new object[] { arg1!, arg2!, arg3!, arg4!, arg5!, arg6!, arg7!, arg8!, arg9! } : Array.Empty<object>();
        return success;
    }
    public NetTask Request(BaseNetCall listener,
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, int TimeoutMS = NetTask.DEFAULT_TIMEOUT_MS)
    {
        NetTask task = listener.Listen(TimeoutMS);
        MessageOverhead overhead = new MessageOverhead(RequestFlags, ID, 0, task.requestId);
        this.Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);
        return task;
    }
    public NetTask RequestAck(
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, int TimeoutMS = NetTask.DEFAULT_TIMEOUT_MS)
    {
        NetTask task = ListenAck(TimeoutMS);
        MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags, ID, 0, task.requestId);
        this.Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);
        return task;
    }
}
public sealed class NetCall<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> : DynamicNetCall
{
    private readonly DynamicByteReader<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> _reader;
    private readonly DynamicByteWriter<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> _writer;
    public delegate void Method(MessageContext context, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10);
    public delegate Task MethodAsync(MessageContext context, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10);
    public NetCall(ushort method, int capacity = 0) : base(method)
    {
        _reader = new DynamicByteReader<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>();
        _writer = new DynamicByteWriter<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(capacity: capacity);
    }
    public NetCall(Method method, int capacity = 0) : base(method)
    {
        _reader = new DynamicByteReader<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>();
        _writer = new DynamicByteWriter<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(capacity: capacity);
    }
    public NetCall(MethodAsync method, int capacity = 0) : base(method)
    {
        _reader = new DynamicByteReader<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>();
        _writer = new DynamicByteWriter<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(capacity: capacity);
    }
    internal override void SetThrowOnError(bool value) => _reader.ThrowOnError = value;
    public void Invoke(ref MessageOverhead overhead,
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10)
    {
#if SERVER
        if (connection == null)
        {
            Logger.LogError($"Error sending method {ID} to null connection.");
            return;
        }
#endif
        try
        {
#if SERVER
            connection.Send(_writer.Get(ref overhead, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10));
#else
            NetFactory.GetPlayerTransportConnection().Send(_writer.Get(ref overhead, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10));
#endif
        }
        catch (Exception ex)
        {
#if SERVER
            Logger.LogError($"Error sending method {ID} to connection {connection.GetAddressString(true)}.");
#else
            Logger.LogError($"Error sending method {ID} to server.");
#endif
            Logger.LogError(ex);
        }
    }
    public void Invoke(
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags, ID, 0);
        Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10);
    }
#if SERVER
    public void Invoke(IEnumerable<ITransportConnection> connections, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags, ID, 0);
        byte[] bytes = _writer.Get(ref overhead, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10);
        try
        {
            connections.Send(bytes);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error sending method {ID} to 1+ connection(s).");
            Logger.LogError(ex);
        }
    }
#endif
    public bool Read(byte[] message, out T1 arg1, out T2 arg2, out T3 arg3, out T4 arg4, out T5 arg5, out T6 arg6, out T7 arg7, out T8 arg8, out T9 arg9, out T10 arg10)
    {
        try
        {
            return _reader.Read(message, out arg1, out arg2, out arg3, out arg4, out arg5, out arg6, out arg7, out arg8, out arg9, out arg10);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error reading method {ID}.");
            Logger.LogError(ex);
            arg1 = default!;
            arg2 = default!;
            arg3 = default!;
            arg4 = default!;
            arg5 = default!;
            arg6 = default!;
            arg7 = default!;
            arg8 = default!;
            arg9 = default!;
            arg10 = default!;
            return false;
        }
    }
    public override bool Read(byte[] message, out object[] parameters)
    {
        bool success = Read(message, out T1 arg1, out T2 arg2, out T3 arg3, out T4 arg4, out T5 arg5, out T6 arg6, out T7 arg7, out T8 arg8, out T9 arg9, out T10 arg10);
        parameters = success ? new object[] { arg1!, arg2!, arg3!, arg4!, arg5!, arg6!, arg7!, arg8!, arg9!, arg10! } : Array.Empty<object>();
        return success;
    }
    public NetTask Request(BaseNetCall listener,
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, int TimeoutMS = NetTask.DEFAULT_TIMEOUT_MS)
    {
        NetTask task = listener.Listen(TimeoutMS);
        MessageOverhead overhead = new MessageOverhead(RequestFlags, ID, 0, task.requestId);
        this.Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10);
        return task;
    }
    public NetTask RequestAck(
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, int TimeoutMS = NetTask.DEFAULT_TIMEOUT_MS)
    {
        NetTask task = ListenAck(TimeoutMS);
        MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags, ID, 0, task.requestId);
        this.Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10);
        return task;
    }
}