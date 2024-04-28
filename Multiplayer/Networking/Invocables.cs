using DanielWillett.ReflectionTools;
using DevkitServer.API;
using DevkitServer.Util.Encoding;
using System.Reflection;
using DanielWillett.SpeedBytes;

namespace DevkitServer.Multiplayer.Networking;

public abstract class BaseNetCall
{
    internal MessageFlags DefaultFlags;
    internal MessageFlags RequestFlags => DefaultFlags | MessageFlags.Request;
    internal MessageFlags AcknowledgeRequestFlags => DefaultFlags | MessageFlags.AcknowledgeRequest;
    public readonly ushort Id;
    public readonly Guid Guid;
    public IDevkitServerPlugin? Plugin { get; internal set; }
    public string Name { get; internal set; } = null!;
    public bool HighSpeed { get; }

    protected internal BaseNetCall(ushort method, bool highSpeed = false)
    {
        if (method == 0)
            throw new ArgumentException("Method Id must be greater than zero.", nameof(method));
        Id = method;
        HighSpeed = highSpeed;
    }
    protected BaseNetCall(Guid method, bool highSpeed = false)
    {
        if (method == Guid.Empty)
            throw new ArgumentException("Guid must not be empty.", nameof(method));
        DefaultFlags = MessageFlags.Guid;
        Guid = method;
        HighSpeed = highSpeed;
    }

    protected BaseNetCall(Delegate method)
    {
        MethodInfo info = method.GetMethodInfo();
        if (info.TryGetAttributeSafe(out NetCallAttribute attribute))
        {
            Id = attribute.MethodID;
            Guid = string.IsNullOrEmpty(attribute.GuidString) || !Guid.TryParse(attribute.GuidString, out Guid g) ? Guid.Empty : g;
            HighSpeed = attribute.HighSpeed;
        }

        if (Id == 0 && Guid == Guid.Empty)
            throw new ArgumentException($"Method provided for {info.Format()} does not contain a {typeof(NetCallAttribute).Format()} attribute, or has invalid data.", nameof(method));
        if (Guid != Guid.Empty)
            DefaultFlags = MessageFlags.Guid;
    }
    public abstract bool Read(ArraySegment<byte> message, out object[] parameters);
    public NetTask Listen(int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task = new NetTask(false, timeoutMs);
        NetFactory.RegisterListener(task, this);
        return task;
    }
    public NetTask ListenAck(int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task = new NetTask(true, timeoutMs);
        NetFactory.RegisterListener(task, this);
        return task;
    }
    internal virtual void SetThrowOnError(bool value) { }
    public override bool Equals(object? obj) => obj is BaseNetCall c2 && Equals(c2);
    public bool Equals(BaseNetCall other) => (Id != default && Id == other.Id || Guid != Guid.Empty && Guid == other.Guid) && HighSpeed == other.HighSpeed;
    public override int GetHashCode()
    {
        unchecked
        {
            int hashCode = Id.GetHashCode();
            hashCode = (hashCode * 397) ^ Guid.GetHashCode();
            hashCode = (hashCode * 397) ^ HighSpeed.GetHashCode();
            return hashCode;
        }
    }
}
public class NetCallCustom : BaseNetCall
{
    public delegate void WriterTask(ByteWriter writer);
    public delegate void Method(MessageContext context, ByteReader reader);
    public delegate Task MethodAsync(MessageContext context, ByteReader reader);
    private readonly PrependableWriter _writer;
    private bool _throwOnError;
    internal NetCallCustom(DevkitServerNetCall method, int capacity = 0, bool highSpeed = false) : this((ushort)method, capacity, highSpeed) { }
    internal NetCallCustom(ushort method, int capacity = 0, bool highSpeed = false) : base(method, highSpeed)
    {
        _writer = new PrependableWriter(true, capacity + MessageOverhead.MaximumSize);
    }
    public NetCallCustom(Guid method, int capacity = 0, bool highSpeed = false) : base(method, highSpeed)
    {
        _writer = new PrependableWriter(true, capacity + MessageOverhead.MaximumSize);
    }
    public NetCallCustom(Method method, int capacity = 0) : base(method)
    {
        _writer = new PrependableWriter(true, capacity + MessageOverhead.MaximumSize);
    }
    public NetCallCustom(MethodAsync method, int capacity = 0) : base(method)
    {
        _writer = new PrependableWriter(true, capacity + MessageOverhead.MaximumSize);
    }
    internal override void SetThrowOnError(bool value) => _throwOnError = value;
    public override bool Read(ArraySegment<byte> message, out object[] parameters)
    {
        ByteReader reader = new ByteReader { ThrowOnError = _throwOnError };
        reader.LoadNew(message);
        parameters = [ null!, reader ];
        return true;
    }
    public bool Read(ArraySegment<byte> message, out ByteReader reader)
    {
        reader = new ByteReader { ThrowOnError = _throwOnError };
        reader.LoadNew(message);
        return true;
    }

    public byte[] Write(WriterTask task)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags, Guid, Id, 0);
        return Write(ref overhead, task);
    }
    public byte[] Write(ref MessageOverhead overhead, WriterTask task)
    {
        lock (_writer)
        {
            _writer.Flush();
            task(_writer);
            _writer.PrependOverhead(ref overhead);
            return _writer.ToArray();
        }
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
            Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection.");
            return;
        }
#endif
        try
        {
            lock (_writer)
            {
                _writer.Flush();
                task(_writer);
                _writer.PrependOverhead(ref overhead);
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
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to connection {connection.Format()}.");
#else
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to server.");
#endif
        }
    }
#if SERVER
    public void Invoke(IReadOnlyList<ITransportConnection>? connections, WriterTask task)
    {
        if (connections is { Count: 0 }) return;
        MessageOverhead overhead = new MessageOverhead(DefaultFlags, Guid, Id, 0);
        try
        {
            byte[] bytes;
            lock (_writer)
            {
                _writer.Flush();
                task(_writer);
                _writer.PrependOverhead(ref overhead);
                bytes = _writer.ToArray();
            }

            NetFactory.Send(connections, bytes);
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to 1+ connection(s).");
        }
    }
#endif
    public void Invoke(
#if SERVER
        ITransportConnection connection, 
#endif
        WriterTask task)
    {
        MessageOverhead ovh = new MessageOverhead(DefaultFlags
#if SERVER
                                                  | (connection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None)
#endif
                                                  , Guid, Id, 0);
        Invoke(ref ovh,
#if SERVER
            connection, 
#endif
            task);
    }
#if CLIENT
    public void Invoke(ref MessageOverhead overhead, HighSpeedConnection connection, WriterTask task)
    {
        if (connection == null)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection.");
            return;
        }
        try
        {
            lock (_writer)
            {
                _writer.Flush();
                task(_writer);
                _writer.PrependOverhead(ref overhead);
                connection.Send(_writer.ToArray());
            }
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to server (HS).");
        }
    }
    public void Invoke(HighSpeedConnection connection, WriterTask task)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags | MessageFlags.HighSpeed, Guid, Id, 0);
        Invoke(ref overhead, connection, task);
    }
    public NetTask Request(BaseNetCall listener, HighSpeedConnection connection, WriterTask task, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task2 = listener.Listen(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(RequestFlags | MessageFlags.HighSpeed, Guid, Id, 0, task2.RequestId);
        Invoke(ref overhead, connection, task);
        return task2;
    }
    public NetTask RequestAck(HighSpeedConnection connection, WriterTask task, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task2 = ListenAck(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags | MessageFlags.HighSpeed, Guid, Id, 0, task2.RequestId);
        Invoke(ref overhead, connection, task);
        return task2;
    }
#endif
    public NetTask Request(BaseNetCall listener,
#if SERVER
        ITransportConnection connection,
#endif
        WriterTask task, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task2 = listener.Listen(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(RequestFlags
#if SERVER
                                                       | (connection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None)
#endif
                                                       , Guid, Id, 0, task2.RequestId);
        Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            task);
        return task2;
    }
    public NetTask RequestAck(
#if SERVER
        ITransportConnection connection,
#endif
        WriterTask task, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task2 = ListenAck(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags
#if SERVER
                                                       | (connection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None)
#endif
                                                        , Guid, Id, 0, task2.RequestId);
        Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            task);
        return task2;
    }
#if SERVER
    public NetTask[] Request(BaseNetCall listener, IReadOnlyList<ITransportConnection> connections, WriterTask task, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        int c = 0;
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection {i.Format()}.");
            else ++c;
        }
        NetTask[] tasks = new NetTask[c];
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                continue;

            tasks[i] = listener.Listen(timeoutMs);
        }

        try
        {
            lock (_writer)
            {
                _writer.Flush();
                task(_writer);
                for (int i = 0; i < connections.Count; ++i)
                {
                    ITransportConnection transportConnection = connections[i];
                    MessageOverhead overhead = new MessageOverhead(RequestFlags | (transportConnection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None), Guid, Id, 0, tasks[i].RequestId);
                    _writer.ReplaceOverhead(ref overhead);
                    transportConnection.Send(_writer.ToArray());
                }
            }
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to {connections.Count.Format()} connection(s).");
        }

        return tasks;
    }
    public NetTask[] RequestAck(IReadOnlyList<ITransportConnection> connections, WriterTask task, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        int c = 0;
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection {i.Format()}.");
            else ++c;
        }
        NetTask[] tasks = new NetTask[c];
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                continue;

            tasks[i] = ListenAck(timeoutMs);
        }

        try
        {
            lock (_writer)
            {
                _writer.Flush();
                task(_writer);
                for (int i = 0; i < connections.Count; ++i)
                {
                    ITransportConnection transportConnection = connections[i];
                    MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags | (transportConnection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None), Guid, Id, 0, tasks[i].RequestId);
                    _writer.ReplaceOverhead(ref overhead);
                    transportConnection.Send(_writer.ToArray());
                }
            }
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to {connections.Count.Format()} connection(s).");
        }

        return tasks;
    }
#endif
}
/// <summary> For querying only </summary>
public abstract class NetCallRaw : BaseNetCall
{
    protected internal NetCallRaw(ushort method, bool highSpeed = false) : base(method, highSpeed) { }
    protected NetCallRaw(Guid method, bool highSpeed = false) : base(method, highSpeed) { }
    protected NetCallRaw(Delegate method) : base(method) { }
}
/// <summary> For querying only </summary>
public abstract class DynamicNetCall : BaseNetCall
{
    protected internal DynamicNetCall(ushort method, bool highSpeed = false) : base(method, highSpeed) { }
    protected DynamicNetCall(Guid method, bool highSpeed = false) : base(method, highSpeed) { }
    protected DynamicNetCall(Delegate method) : base(method) { }
}
public sealed class NetCall : BaseNetCall
{
    public delegate void Method(MessageContext context);
    public delegate Task MethodAsync(MessageContext context);
    internal NetCall(DevkitServerNetCall method) : this((ushort)method) { }
    internal NetCall(ushort method, bool highSpeed = false) : base(method, highSpeed) { }
    public NetCall(Guid method, bool highSpeed = false) : base(method, highSpeed) { }
    public NetCall(Method method) : base(method) { }
    public NetCall(MethodAsync method) : base(method) { }
    private byte[]? _bytes;
    public byte[] Write() => _bytes ??= new MessageOverhead(DefaultFlags, Guid, Id, 0).GetBytes();
    public byte[] Write(ref MessageOverhead overhead) => overhead.GetBytes();
    public void Invoke(
#if SERVER
        ITransportConnection connection
#endif
        )
    {
#if SERVER
        if (connection is HighSpeedConnection)
        {
            connection.Send(new MessageOverhead(DefaultFlags | MessageFlags.HighSpeed, Guid, Id, 0).GetBytes());
            return;
        }
#endif

        _bytes ??= new MessageOverhead(DefaultFlags, Guid, Id, 0).GetBytes();
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
    public void Invoke(IReadOnlyList<ITransportConnection>? connections)
    {
        if (connections is { Count: 0 }) return;
        _bytes ??= new MessageOverhead(DefaultFlags, Guid, Id, 0).GetBytes();
        NetFactory.Send(connections, _bytes);
    }
#endif
    public bool Read(ArraySegment<byte> message) => true;
    public override bool Read(ArraySegment<byte> message, out object[] parameters)
    {
        parameters = new object[1];
        return true;
    }
#if CLIENT
    public void Invoke(ref MessageOverhead overhead, HighSpeedConnection connection)
    {
        if (connection == null)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection.");
            return;
        }
        connection.Send(overhead.GetBytes());
    }
    public void Invoke(HighSpeedConnection connection)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags | MessageFlags.HighSpeed, Guid, Id, 0);
        Invoke(ref overhead, connection);
    }
    public NetTask Request(BaseNetCall listener, HighSpeedConnection connection, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task2 = listener.Listen(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(RequestFlags | MessageFlags.HighSpeed, Guid, Id, 0, task2.RequestId);
        Invoke(ref overhead, connection);
        return task2;
    }
    public NetTask RequestAck(HighSpeedConnection connection, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task2 = ListenAck(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags | MessageFlags.HighSpeed, Guid, Id, 0, task2.RequestId);
        Invoke(ref overhead, connection);
        return task2;
    }
#endif
    public NetTask Request(BaseNetCall listener,
#if SERVER
        ITransportConnection connection, 
#endif
        int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task = listener.Listen(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(RequestFlags
#if SERVER
                                                       | (connection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None)
#endif
                                                        , Guid, Id, 0, task.RequestId);
        Invoke(ref overhead
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
        int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task = ListenAck(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags
#if SERVER
                                                       | (connection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None)
#endif
                                                        , Guid, Id, 0, task.RequestId);
        Invoke(ref overhead
#if SERVER
            , connection
#endif
            );
        return task;
    }
#if SERVER
    public NetTask[] Request(BaseNetCall listener, IReadOnlyList<ITransportConnection> connections, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        int c = 0;
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection {i.Format()}.");
            else ++c;
        }
        NetTask[] tasks = new NetTask[c];
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                continue;

            tasks[i] = listener.Listen(timeoutMs);
        }

        try
        {
            for (int i = 0; i < connections.Count; ++i)
            {
                ITransportConnection transportConnection = connections[i];
                MessageOverhead overhead = new MessageOverhead(RequestFlags | (transportConnection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None), Guid, Id, 0, tasks[i].RequestId);
                transportConnection.Send(overhead.GetBytes());
            }
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to {connections.Count.Format()} connection(s).");
        }

        return tasks;
    }
    public NetTask[] RequestAck(IReadOnlyList<ITransportConnection> connections, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        int c = 0;
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection {i.Format()}.");
            else ++c;
        }
        NetTask[] tasks = new NetTask[c];
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                continue;

            tasks[i] = ListenAck(timeoutMs);
        }

        try
        {
            for (int i = 0; i < connections.Count; ++i)
            {
                ITransportConnection transportConnection = connections[i];
                MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags | (transportConnection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None), Guid, Id, 0, tasks[i].RequestId);
                transportConnection.Send(overhead.GetBytes());
            }
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to {connections.Count.Format()} connection(s).");
        }

        return tasks;
    }
#endif
}
public sealed class NetCallRaw<T> : NetCallRaw
{
    private readonly ByteReaderRaw<T> _reader;
    private readonly ByteWriterRaw<T> _writer;
    public delegate void Method(MessageContext context, T arg1);
    public delegate Task MethodAsync(MessageContext context, T arg1);
    /// <summary>Leave <paramref name="reader"/> or <paramref name="writer"/> null to auto-fill.</summary>
    internal NetCallRaw(DevkitServerNetCall method, Reader<T>? reader, Writer<T>? writer, int capacity = 0) : this((ushort)method, reader, writer, capacity) { }
    internal NetCallRaw(ushort method, Reader<T>? reader, Writer<T>? writer, int capacity = 0, bool highSpeed = false) : base(method, highSpeed)
    {
        _writer = new ByteWriterRaw<T>(writer, capacity: capacity + MessageOverhead.MaximumSize);
        _reader = new ByteReaderRaw<T>(reader);
    }
    /// <summary>Leave <paramref name="reader"/> or <paramref name="writer"/> null to auto-fill.</summary>
    public NetCallRaw(Guid method, Reader<T>? reader, Writer<T>? writer, int capacity = 0, bool highSpeed = false) : base(method, highSpeed)
    {
        _writer = new ByteWriterRaw<T>(writer, capacity: capacity + MessageOverhead.MaximumSize);
        _reader = new ByteReaderRaw<T>(reader);
    }
    /// <summary>Leave <paramref name="reader"/> or <paramref name="writer"/> null to auto-fill.</summary>
    public NetCallRaw(Method method, Reader<T>? reader, Writer<T>? writer, int capacity = 0) : base(method)
    {
        _writer = new ByteWriterRaw<T>(writer, capacity: capacity + MessageOverhead.MaximumSize);
        _reader = new ByteReaderRaw<T>(reader);
    }
    /// <summary>Leave <paramref name="reader"/> or <paramref name="writer"/> null to auto-fill.</summary>
    public NetCallRaw(MethodAsync method, Reader<T>? reader, Writer<T>? writer, int capacity = 0) : base(method)
    {
        _writer = new ByteWriterRaw<T>(writer, capacity: capacity + MessageOverhead.MaximumSize);
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
            Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection.");
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
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to connection {connection.Format()}.");
#else
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to server.");
#endif
        }
    }
    public void Invoke(
#if SERVER
        ITransportConnection connection, 
#endif
        T arg)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags
#if SERVER
                                                       | (connection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None)
#endif
                                                        , Guid, Id, 0);
        Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg);
    }
#if SERVER
    public void Invoke(IReadOnlyList<ITransportConnection>? connections, T arg)
    {
        if (connections is { Count: 0 }) return;
        MessageOverhead overhead = new MessageOverhead(DefaultFlags, Guid, Id, 0);
        byte[] bytes = _writer.Get(ref overhead, arg);
        try
        {
            NetFactory.Send(connections, bytes);
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to 1+ connection(s).");
        }
    }
#endif
    public bool Read(ArraySegment<byte> message, out T arg)
    {
        try
        {
            return _reader.Read(message, out arg);
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error reading method {Id.Format()}.");
            arg = default!;
            return false;
        }
    }
    public override bool Read(ArraySegment<byte> message, out object[] parameters)
    {
        bool success = Read(message, out T a1);
        parameters = success ? new object[] { null!, a1! } : Array.Empty<object>();
        return success;
    }
#if CLIENT
    public void Invoke(ref MessageOverhead overhead, HighSpeedConnection connection, T arg)
    {
        if (connection == null)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection.");
            return;
        }
        try
        {
            connection.Send(_writer.Get(ref overhead, arg));
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to server.");
        }
    }
    public void Invoke(HighSpeedConnection connection, T arg)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags | MessageFlags.HighSpeed, Guid, Id, 0);
        Invoke(ref overhead, connection, arg);
    }
    public NetTask Request(BaseNetCall listener, HighSpeedConnection connection, T arg, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task2 = listener.Listen(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(RequestFlags | MessageFlags.HighSpeed, Guid, Id, 0, task2.RequestId);
        Invoke(ref overhead, connection, arg);
        return task2;
    }
    public NetTask RequestAck(HighSpeedConnection connection, T arg, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task2 = ListenAck(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags | MessageFlags.HighSpeed, Guid, Id, 0, task2.RequestId);
        Invoke(ref overhead, connection, arg);
        return task2;
    }
#endif
    public NetTask Request(BaseNetCall listener,
#if SERVER
        ITransportConnection connection, 
#endif
        T arg, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task = listener.Listen(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(RequestFlags
#if SERVER
                                                       | (connection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None)
#endif
                                                        , Guid, Id, 0, task.RequestId);
        Invoke(ref overhead,
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
        T arg, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task = ListenAck(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags
#if SERVER
                                                       | (connection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None)
#endif
                                                        , Guid, Id, 0, task.RequestId);
        Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg);
        return task;
    }
    public byte[] Write(T arg)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags, Guid, Id, 0);
        return Write(ref overhead, arg);
    }

    public byte[] Write(ref MessageOverhead overhead, T arg) => _writer.Get(ref overhead, arg);
#if SERVER
    public NetTask[] Request(BaseNetCall listener, IReadOnlyList<ITransportConnection> connections, T arg, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        int c = 0;
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection {i.Format()}.");
            else ++c;
        }
        NetTask[] tasks = new NetTask[c];
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                continue;

            tasks[i] = listener.Listen(timeoutMs);
        }

        try
        {
            for (int i = 0; i < connections.Count; ++i)
            {
                ITransportConnection transportConnection = connections[i];
                MessageOverhead overhead = new MessageOverhead(RequestFlags | (transportConnection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None), Guid, Id, 0, tasks[i].RequestId);
                transportConnection.Send(_writer.Get(ref overhead, arg));
            }
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to {connections.Count.Format()} connection(s).");
        }

        return tasks;
    }
    public NetTask[] RequestAck(IReadOnlyList<ITransportConnection> connections, T arg, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        int c = 0;
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection {i.Format()}.");
            else ++c;
        }
        NetTask[] tasks = new NetTask[c];
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                continue;

            tasks[i] = ListenAck(timeoutMs);
        }

        try
        {
            for (int i = 0; i < connections.Count; ++i)
            {
                ITransportConnection transportConnection = connections[i];
                MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags | (transportConnection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None), Guid, Id, 0, tasks[i].RequestId);
                transportConnection.Send(_writer.Get(ref overhead, arg));
            }
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to {connections.Count.Format()} connection(s).");
        }

        return tasks;
    }
#endif
}
/// <summary>Leave any reader or writer null to auto-fill.</summary>
public sealed class NetCallRaw<T1, T2> : NetCallRaw
{
    private readonly ByteReaderRaw<T1, T2> _reader;
    private readonly ByteWriterRaw<T1, T2> _writer;
    public delegate void Method(MessageContext context, T1 arg1, T2 arg2);
    public delegate Task MethodAsync(MessageContext context, T1 arg1, T2 arg2);
    internal NetCallRaw(DevkitServerNetCall method, Reader<T1>? reader1, Reader<T2>? reader2, Writer<T1>? writer1, Writer<T2>? writer2, int capacity = 0)
        : this((ushort)method, reader1, reader2, writer1, writer2, capacity) { }
    /// <summary>Leave any of the readers or writers null to auto-fill.</summary>
    internal NetCallRaw(ushort method, Reader<T1>? reader1, Reader<T2>? reader2, Writer<T1>? writer1, Writer<T2>? writer2, int capacity = 0, bool highSpeed = false) : base(method, highSpeed)
    {
        _writer = new ByteWriterRaw<T1, T2>(writer1, writer2, capacity: capacity + MessageOverhead.MaximumSize);
        _reader = new ByteReaderRaw<T1, T2>(reader1, reader2);
    }
    /// <summary>Leave any of the readers or writers null to auto-fill.</summary>
    public NetCallRaw(Guid method, Reader<T1>? reader1, Reader<T2>? reader2, Writer<T1>? writer1, Writer<T2>? writer2, int capacity = 0, bool highSpeed = false) : base(method, highSpeed)
    {
        _writer = new ByteWriterRaw<T1, T2>(writer1, writer2, capacity: capacity + MessageOverhead.MaximumSize);
        _reader = new ByteReaderRaw<T1, T2>(reader1, reader2);
    }
    /// <summary>Leave any of the readers or writers null to auto-fill.</summary>
    public NetCallRaw(Method method, Reader<T1>? reader1, Reader<T2>? reader2, Writer<T1>? writer1, Writer<T2>? writer2, int capacity = 0) : base(method)
    {
        _writer = new ByteWriterRaw<T1, T2>(writer1, writer2, capacity: capacity + MessageOverhead.MaximumSize);
        _reader = new ByteReaderRaw<T1, T2>(reader1, reader2);
    }
    /// <summary>Leave any of the readers or writers null to auto-fill.</summary>
    public NetCallRaw(MethodAsync method, Reader<T1>? reader1, Reader<T2>? reader2, Writer<T1>? writer1, Writer<T2>? writer2, int capacity = 0) : base(method)
    {
        _writer = new ByteWriterRaw<T1, T2>(writer1, writer2, capacity: capacity + MessageOverhead.MaximumSize);
        _reader = new ByteReaderRaw<T1, T2>(reader1, reader2);
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
            Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection.");
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
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to connection {connection.Format()}.");
#else
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to server.");
#endif
        }
    }
    public void Invoke(
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags
#if SERVER
                                                       | (connection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None)
#endif
                                                        , Guid, Id, 0);
        Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg1, arg2);
    }
#if SERVER
    public void Invoke(IReadOnlyList<ITransportConnection>? connections, T1 arg1, T2 arg2)
    {
        if (connections is { Count: 0 }) return;
        MessageOverhead overhead = new MessageOverhead(DefaultFlags, Guid, Id, 0);
        byte[] bytes = _writer.Get(ref overhead, arg1, arg2);
        try
        {
            NetFactory.Send(connections, bytes);
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to 1+ connection(s).");
        }
    }
#endif
    public bool Read(ArraySegment<byte> message, out T1 arg1, out T2 arg2)
    {
        try
        {
            return _reader.Read(message, out arg1, out arg2);
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error reading method {Id.Format()}.");
            arg1 = default!;
            arg2 = default!;
            return false;
        }
    }
    public override bool Read(ArraySegment<byte> message, out object[] parameters)
    {
        bool success = Read(message, out T1 a1, out T2 a2);
        parameters = success ? new object[] { null!, a1!, a2! } : Array.Empty<object>();
        return success;
    }
#if CLIENT
    public void Invoke(ref MessageOverhead overhead, HighSpeedConnection connection, T1 arg1, T2 arg2)
    {
        if (connection == null)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection.");
            return;
        }
        try
        {
            connection.Send(_writer.Get(ref overhead, arg1, arg2));
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to server.");
        }
    }
    public void Invoke(HighSpeedConnection connection, T1 arg1, T2 arg2)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags | MessageFlags.HighSpeed, Guid, Id, 0);
        Invoke(ref overhead, connection, arg1, arg2);
    }
    public NetTask Request(BaseNetCall listener, HighSpeedConnection connection, T1 arg1, T2 arg2, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task2 = listener.Listen(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(RequestFlags | MessageFlags.HighSpeed, Guid, Id, 0, task2.RequestId);
        Invoke(ref overhead, connection, arg1, arg2);
        return task2;
    }
    public NetTask RequestAck(HighSpeedConnection connection, T1 arg1, T2 arg2, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task2 = ListenAck(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags | MessageFlags.HighSpeed, Guid, Id, 0, task2.RequestId);
        Invoke(ref overhead, connection, arg1, arg2);
        return task2;
    }
#endif
    public NetTask Request(BaseNetCall listener,
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task = listener.Listen(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(RequestFlags
#if SERVER
                                                       | (connection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None)
#endif
                                                        , Guid, Id, 0, task.RequestId);
        Invoke(ref overhead,
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
        T1 arg1, T2 arg2, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task = ListenAck(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags
#if SERVER
                                                       | (connection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None)
#endif
                                                        , Guid, Id, 0, task.RequestId);
        Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg1, arg2);
        return task;
    }

    public byte[] Write(T1 arg1, T2 arg2)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags, Guid, Id, 0);
        return Write(ref overhead, arg1, arg2);
    }

    public byte[] Write(ref MessageOverhead overhead, T1 arg1, T2 arg2)
        => _writer.Get(ref overhead, arg1, arg2);
#if SERVER
    public NetTask[] Request(BaseNetCall listener, IReadOnlyList<ITransportConnection> connections, T1 arg1, T2 arg2, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        int c = 0;
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection {i.Format()}.");
            else ++c;
        }
        NetTask[] tasks = new NetTask[c];
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                continue;

            tasks[i] = listener.Listen(timeoutMs);
        }

        try
        {
            for (int i = 0; i < connections.Count; ++i)
            {
                ITransportConnection transportConnection = connections[i];
                MessageOverhead overhead = new MessageOverhead(RequestFlags | (transportConnection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None), Guid, Id, 0, tasks[i].RequestId);
                transportConnection.Send(_writer.Get(ref overhead, arg1, arg2));
            }
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to {connections.Count.Format()} connection(s).");
        }

        return tasks;
    }
    public NetTask[] RequestAck(IReadOnlyList<ITransportConnection> connections, T1 arg1, T2 arg2, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        int c = 0;
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection {i.Format()}.");
            else ++c;
        }
        NetTask[] tasks = new NetTask[c];
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                continue;

            tasks[i] = ListenAck(timeoutMs);
        }

        try
        {
            for (int i = 0; i < connections.Count; ++i)
            {
                ITransportConnection transportConnection = connections[i];
                MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags | (transportConnection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None), Guid, Id, 0, tasks[i].RequestId);
                transportConnection.Send(_writer.Get(ref overhead, arg1, arg2));
            }
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to {connections.Count.Format()} connection(s).");
        }

        return tasks;
    }
#endif
}
/// <summary>Leave any reader or writer null to auto-fill.</summary>
public sealed class NetCallRaw<T1, T2, T3> : NetCallRaw
{
    private readonly ByteReaderRaw<T1, T2, T3> _reader;
    private readonly ByteWriterRaw<T1, T2, T3> _writer;
    public delegate void Method(MessageContext context, T1 arg1, T2 arg2, T3 arg3);
    public delegate Task MethodAsync(MessageContext context, T1 arg1, T2 arg2, T3 arg3);
    internal NetCallRaw(DevkitServerNetCall method, Reader<T1>? reader1, Reader<T2>? reader2, Reader<T3>? reader3, Writer<T1>? writer1, Writer<T2>? writer2, Writer<T3>? writer3, int capacity = 0)
        : this((ushort)method, reader1, reader2, reader3, writer1, writer2, writer3, capacity) { }
    /// <summary>Leave any of the readers or writers null to auto-fill.</summary>
    internal NetCallRaw(ushort method, Reader<T1>? reader1, Reader<T2>? reader2, Reader<T3>? reader3, Writer<T1>? writer1, Writer<T2>? writer2, Writer<T3>? writer3, int capacity = 0, bool highSpeed = false) : base(method, highSpeed)
    {
        _writer = new ByteWriterRaw<T1, T2, T3>(writer1, writer2, writer3, capacity: capacity + MessageOverhead.MaximumSize);
        _reader = new ByteReaderRaw<T1, T2, T3>(reader1, reader2, reader3);
    }
    /// <summary>Leave any of the readers or writers null to auto-fill.</summary>
    public NetCallRaw(Guid method, Reader<T1>? reader1, Reader<T2>? reader2, Reader<T3>? reader3, Writer<T1>? writer1, Writer<T2>? writer2, Writer<T3>? writer3, int capacity = 0, bool highSpeed = false) : base(method, highSpeed)
    {
        _writer = new ByteWriterRaw<T1, T2, T3>(writer1, writer2, writer3, capacity: capacity + MessageOverhead.MaximumSize);
        _reader = new ByteReaderRaw<T1, T2, T3>(reader1, reader2, reader3);
    }
    /// <summary>Leave any of the readers or writers null to auto-fill.</summary>
    public NetCallRaw(Method method, Reader<T1>? reader1, Reader<T2>? reader2, Reader<T3>? reader3, Writer<T1>? writer1, Writer<T2>? writer2, Writer<T3>? writer3, int capacity = 0) : base(method)
    {
        _writer = new ByteWriterRaw<T1, T2, T3>(writer1, writer2, writer3, capacity: capacity + MessageOverhead.MaximumSize);
        _reader = new ByteReaderRaw<T1, T2, T3>(reader1, reader2, reader3);
    }
    /// <summary>Leave any of the readers or writers null to auto-fill.</summary>
    public NetCallRaw(MethodAsync method, Reader<T1>? reader1, Reader<T2>? reader2, Reader<T3>? reader3, Writer<T1>? writer1, Writer<T2>? writer2, Writer<T3>? writer3, int capacity = 0) : base(method)
    {
        _writer = new ByteWriterRaw<T1, T2, T3>(writer1, writer2, writer3, capacity: capacity + MessageOverhead.MaximumSize);
        _reader = new ByteReaderRaw<T1, T2, T3>(reader1, reader2, reader3);
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
            Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection.");
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
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to connection {connection.Format()}.");
#else
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to server.");
#endif
        }
    }
    public void Invoke(
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2, T3 arg3)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags
#if SERVER
                                                       | (connection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None)
#endif
                                                        , Guid, Id, 0);
        Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg1, arg2, arg3);
    }
#if SERVER
    public void Invoke(IReadOnlyList<ITransportConnection>? connections, T1 arg1, T2 arg2, T3 arg3)
    {
        if (connections is { Count: 0 }) return;
        MessageOverhead overhead = new MessageOverhead(DefaultFlags, Guid, Id, 0);
        byte[] bytes = _writer.Get(ref overhead, arg1, arg2, arg3);
        try
        {
            NetFactory.Send(connections, bytes);
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to 1+ connection(s).");
        }
    }
#endif
    public bool Read(ArraySegment<byte> message, out T1 arg1, out T2 arg2, out T3 arg3)
    {
        try
        {
            return _reader.Read(message, out arg1, out arg2, out arg3);
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error reading method {Id.Format()}.");
            arg1 = default!;
            arg2 = default!;
            arg3 = default!;
            return false;
        }
    }
    public override bool Read(ArraySegment<byte> message, out object[] parameters)
    {
        bool success = Read(message, out T1 a1, out T2 a2, out T3 a3);
        parameters = success ? new object[] { null!, a1!, a2!, a3! } : Array.Empty<object>();
        return success;
    }
#if CLIENT
    public void Invoke(ref MessageOverhead overhead, HighSpeedConnection connection, T1 arg1, T2 arg2, T3 arg3)
    {
        if (connection == null)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection.");
            return;
        }
        try
        {
            connection.Send(_writer.Get(ref overhead, arg1, arg2, arg3));
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to server.");
        }
    }
    public void Invoke(HighSpeedConnection connection, T1 arg1, T2 arg2, T3 arg3)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags | MessageFlags.HighSpeed, Guid, Id, 0);
        Invoke(ref overhead, connection, arg1, arg2, arg3);
    }
    public NetTask Request(BaseNetCall listener, HighSpeedConnection connection, T1 arg1, T2 arg2, T3 arg3, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task2 = listener.Listen(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(RequestFlags | MessageFlags.HighSpeed, Guid, Id, 0, task2.RequestId);
        Invoke(ref overhead, connection, arg1, arg2, arg3);
        return task2;
    }
    public NetTask RequestAck(HighSpeedConnection connection, T1 arg1, T2 arg2, T3 arg3, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task2 = ListenAck(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags | MessageFlags.HighSpeed, Guid, Id, 0, task2.RequestId);
        Invoke(ref overhead, connection, arg1, arg2, arg3);
        return task2;
    }
#endif
    public NetTask Request(BaseNetCall listener,
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2, T3 arg3, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task = listener.Listen(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(RequestFlags
#if SERVER
                                                       | (connection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None)
#endif
                                                        , Guid, Id, 0, task.RequestId);
        Invoke(ref overhead,
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
        T1 arg1, T2 arg2, T3 arg3, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task = ListenAck(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags
#if SERVER
                                                       | (connection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None)
#endif
                                                        , Guid, Id, 0, task.RequestId);
        Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg1, arg2, arg3);
        return task;
    }
    

    public byte[] Write(T1 arg1, T2 arg2, T3 arg3)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags, Guid, Id, 0);
        return Write(ref overhead, arg1, arg2, arg3);
    }

    public byte[] Write(ref MessageOverhead overhead, T1 arg1, T2 arg2, T3 arg3)
        => _writer.Get(ref overhead, arg1, arg2, arg3);
#if SERVER
    public NetTask[] Request(BaseNetCall listener, IReadOnlyList<ITransportConnection> connections, T1 arg1, T2 arg2, T3 arg3, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        int c = 0;
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection {i.Format()}.");
            else ++c;
        }
        NetTask[] tasks = new NetTask[c];
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                continue;

            tasks[i] = listener.Listen(timeoutMs);
        }

        try
        {
            for (int i = 0; i < connections.Count; ++i)
            {
                ITransportConnection transportConnection = connections[i];
                MessageOverhead overhead = new MessageOverhead(RequestFlags | (transportConnection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None), Guid, Id, 0, tasks[i].RequestId);
                transportConnection.Send(_writer.Get(ref overhead, arg1, arg2, arg3));
            }
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to {connections.Count.Format()} connection(s).");
        }

        return tasks;
    }
    public NetTask[] RequestAck(IReadOnlyList<ITransportConnection> connections, T1 arg1, T2 arg2, T3 arg3, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        int c = 0;
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection {i.Format()}.");
            else ++c;
        }
        NetTask[] tasks = new NetTask[c];
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                continue;

            tasks[i] = ListenAck(timeoutMs);
        }

        try
        {
            for (int i = 0; i < connections.Count; ++i)
            {
                ITransportConnection transportConnection = connections[i];
                MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags | (transportConnection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None), Guid, Id, 0, tasks[i].RequestId);
                transportConnection.Send(_writer.Get(ref overhead, arg1, arg2, arg3));
            }
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to {connections.Count.Format()} connection(s).");
        }

        return tasks;
    }
#endif
}
/// <summary>Leave any reader or writer null to auto-fill.</summary>
public sealed class NetCallRaw<T1, T2, T3, T4> : NetCallRaw
{
    private readonly ByteReaderRaw<T1, T2, T3, T4> _reader;
    private readonly ByteWriterRaw<T1, T2, T3, T4> _writer;
    public delegate void Method(MessageContext context, T1 arg1, T2 arg2, T3 arg3, T4 arg4);
    public delegate Task MethodAsync(MessageContext context, T1 arg1, T2 arg2, T3 arg3, T4 arg4);
    internal NetCallRaw(DevkitServerNetCall method, Reader<T1>? reader1, Reader<T2>? reader2, Reader<T3>? reader3, Reader<T4>? reader4, Writer<T1>? writer1, Writer<T2>? writer2, Writer<T3>? writer3, Writer<T4>? writer4, int capacity = 0)
        : this((ushort)method, reader1, reader2, reader3, reader4, writer1, writer2, writer3, writer4, capacity + MessageOverhead.MaximumSize) { }
    /// <summary>Leave any of the readers or writers null to auto-fill.</summary>
    internal NetCallRaw(ushort method, Reader<T1>? reader1, Reader<T2>? reader2, Reader<T3>? reader3, Reader<T4>? reader4, Writer<T1>? writer1, Writer<T2>? writer2, Writer<T3>? writer3, Writer<T4>? writer4, int capacity = 0, bool highSpeed = false) : base(method, highSpeed)
    {
        _writer = new ByteWriterRaw<T1, T2, T3, T4>(writer1, writer2, writer3, writer4, capacity: capacity + MessageOverhead.MaximumSize);
        _reader = new ByteReaderRaw<T1, T2, T3, T4>(reader1, reader2, reader3, reader4);
    }
    /// <summary>Leave any of the readers or writers null to auto-fill.</summary>
    public NetCallRaw(Guid method, Reader<T1>? reader1, Reader<T2>? reader2, Reader<T3>? reader3, Reader<T4>? reader4, Writer<T1>? writer1, Writer<T2>? writer2, Writer<T3>? writer3, Writer<T4>? writer4, int capacity = 0, bool highSpeed = false) : base(method, highSpeed)
    {
        _writer = new ByteWriterRaw<T1, T2, T3, T4>(writer1, writer2, writer3, writer4, capacity: capacity + MessageOverhead.MaximumSize);
        _reader = new ByteReaderRaw<T1, T2, T3, T4>(reader1, reader2, reader3, reader4);
    }
    /// <summary>Leave any of the readers or writers null to auto-fill.</summary>
    public NetCallRaw(Method method, Reader<T1>? reader1, Reader<T2>? reader2, Reader<T3>? reader3, Reader<T4>? reader4, Writer<T1>? writer1, Writer<T2>? writer2, Writer<T3>? writer3, Writer<T4>? writer4, int capacity = 0) : base(method)
    {
        _writer = new ByteWriterRaw<T1, T2, T3, T4>(writer1, writer2, writer3, writer4, capacity: capacity + MessageOverhead.MaximumSize);
        _reader = new ByteReaderRaw<T1, T2, T3, T4>(reader1, reader2, reader3, reader4);
    }
    /// <summary>Leave any of the readers or writers null to auto-fill.</summary>
    public NetCallRaw(MethodAsync method, Reader<T1>? reader1, Reader<T2>? reader2, Reader<T3>? reader3, Reader<T4>? reader4, Writer<T1>? writer1, Writer<T2>? writer2, Writer<T3>? writer3, Writer<T4>? writer4, int capacity = 0) : base(method)
    {
        _writer = new ByteWriterRaw<T1, T2, T3, T4>(writer1, writer2, writer3, writer4, capacity: capacity + MessageOverhead.MaximumSize);
        _reader = new ByteReaderRaw<T1, T2, T3, T4>(reader1, reader2, reader3, reader4);
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
            Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection.");
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
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to connection {connection.Format()}.");
#else
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to server.");
#endif
        }
    }
    public void Invoke(
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2, T3 arg3, T4 arg4)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags
#if SERVER
                                                       | (connection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None)
#endif
                                                        , Guid, Id, 0);
        Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg1, arg2, arg3, arg4);
    }
#if SERVER
    public void Invoke(IReadOnlyList<ITransportConnection>? connections, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
    {
        if (connections is { Count: 0 }) return;
        MessageOverhead overhead = new MessageOverhead(DefaultFlags, Guid, Id, 0);
        byte[] bytes = _writer.Get(ref overhead, arg1, arg2, arg3, arg4);
        try
        {
            NetFactory.Send(connections, bytes);
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to 1+ connection(s).");
        }
    }
#endif
    public bool Read(ArraySegment<byte> message, out T1 arg1, out T2 arg2, out T3 arg3, out T4 arg4)
    {
        try
        {
            return _reader.Read(message, out arg1, out arg2, out arg3, out arg4);
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error reading method {Id.Format()}.");
            arg1 = default!;
            arg2 = default!;
            arg3 = default!;
            arg4 = default!;
            return false;
        }
    }
    public override bool Read(ArraySegment<byte> message, out object[] parameters)
    {
        bool success = Read(message, out T1 a1, out T2 a2, out T3 a3, out T4 a4);
        parameters = success ? new object[] { null!, a1!, a2!, a3!, a4! } : Array.Empty<object>();
        return success;
    }
#if CLIENT
    public void Invoke(ref MessageOverhead overhead, HighSpeedConnection connection, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
    {
        if (connection == null)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection.");
            return;
        }
        try
        {
            connection.Send(_writer.Get(ref overhead, arg1, arg2, arg3, arg4));
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to server.");
        }
    }
    public void Invoke(HighSpeedConnection connection, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags | MessageFlags.HighSpeed, Guid, Id, 0);
        Invoke(ref overhead, connection, arg1, arg2, arg3, arg4);
    }
    public NetTask Request(BaseNetCall listener, HighSpeedConnection connection, T1 arg1, T2 arg2, T3 arg3, T4 arg4, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task2 = listener.Listen(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(RequestFlags | MessageFlags.HighSpeed, Guid, Id, 0, task2.RequestId);
        Invoke(ref overhead, connection, arg1, arg2, arg3, arg4);
        return task2;
    }
    public NetTask RequestAck(HighSpeedConnection connection, T1 arg1, T2 arg2, T3 arg3, T4 arg4, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task2 = ListenAck(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags | MessageFlags.HighSpeed, Guid, Id, 0, task2.RequestId);
        Invoke(ref overhead, connection, arg1, arg2, arg3, arg4);
        return task2;
    }
#endif
    public NetTask Request(BaseNetCall listener,
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2, T3 arg3, T4 arg4, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task = listener.Listen(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(RequestFlags
#if SERVER
                                                       | (connection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None)
#endif
                                                        , Guid, Id, 0, task.RequestId);
        Invoke(ref overhead,
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
        T1 arg1, T2 arg2, T3 arg3, T4 arg4, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task = ListenAck(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags
#if SERVER
                                                       | (connection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None)
#endif
                                                        , Guid, Id, 0, task.RequestId);
        Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg1, arg2, arg3, arg4);
        return task;
    }

    public byte[] Write(T1 arg1, T2 arg2, T3 arg3, T4 arg4)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags, Guid, Id, 0);
        return Write(ref overhead, arg1, arg2, arg3, arg4);
    }

    public byte[] Write(ref MessageOverhead overhead, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
        => _writer.Get(ref overhead, arg1, arg2, arg3, arg4);
#if SERVER
    public NetTask[] Request(BaseNetCall listener, IReadOnlyList<ITransportConnection> connections, T1 arg1, T2 arg2, T3 arg3, T4 arg4, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        int c = 0;
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection {i.Format()}.");
            else ++c;
        }
        NetTask[] tasks = new NetTask[c];
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                continue;

            tasks[i] = listener.Listen(timeoutMs);
        }

        try
        {
            for (int i = 0; i < connections.Count; ++i)
            {
                ITransportConnection transportConnection = connections[i];
                MessageOverhead overhead = new MessageOverhead(RequestFlags | (transportConnection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None), Guid, Id, 0, tasks[i].RequestId);
                transportConnection.Send(_writer.Get(ref overhead, arg1, arg2, arg3, arg4));
            }
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to {connections.Count.Format()} connection(s).");
        }

        return tasks;
    }
    public NetTask[] RequestAck(IReadOnlyList<ITransportConnection> connections, T1 arg1, T2 arg2, T3 arg3, T4 arg4, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        int c = 0;
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection {i.Format()}.");
            else ++c;
        }
        NetTask[] tasks = new NetTask[c];
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                continue;

            tasks[i] = ListenAck(timeoutMs);
        }

        try
        {
            for (int i = 0; i < connections.Count; ++i)
            {
                ITransportConnection transportConnection = connections[i];
                MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags | (transportConnection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None), Guid, Id, 0, tasks[i].RequestId);
                transportConnection.Send(_writer.Get(ref overhead, arg1, arg2, arg3, arg4));
            }
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to {connections.Count.Format()} connection(s).");
        }

        return tasks;
    }
#endif
}
/// <summary>Leave any reader or writer null to auto-fill.</summary>
public sealed class NetCallRaw<T1, T2, T3, T4, T5> : NetCallRaw
{
    private readonly ByteReaderRaw<T1, T2, T3, T4, T5> _reader;
    private readonly ByteWriterRaw<T1, T2, T3, T4, T5> _writer;
    public delegate void Method(MessageContext context, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5);
    public delegate Task MethodAsync(MessageContext context, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5);
    internal NetCallRaw(DevkitServerNetCall method, Reader<T1>? reader1, Reader<T2>? reader2, Reader<T3>? reader3, Reader<T4>? reader4, Reader<T5>? reader5, Writer<T1>? writer1, Writer<T2>? writer2, Writer<T3>? writer3, Writer<T4>? writer4, Writer<T5>? writer5, int capacity = 0)
        : this((ushort)method, reader1, reader2, reader3, reader4, reader5, writer1, writer2, writer3, writer4, writer5, capacity) { }
    /// <summary>Leave any of the readers or writers null to auto-fill.</summary>
    internal NetCallRaw(ushort method, Reader<T1>? reader1, Reader<T2>? reader2, Reader<T3>? reader3, Reader<T4>? reader4, Reader<T5>? reader5, Writer<T1>? writer1, Writer<T2>? writer2, Writer<T3>? writer3, Writer<T4>? writer4, Writer<T5>? writer5, int capacity = 0, bool highSpeed = false) : base(method, highSpeed)
    {
        _writer = new ByteWriterRaw<T1, T2, T3, T4, T5>(writer1, writer2, writer3, writer4, writer5, capacity: capacity + MessageOverhead.MaximumSize);
        _reader = new ByteReaderRaw<T1, T2, T3, T4, T5>(reader1, reader2, reader3, reader4, reader5);
    }
    /// <summary>Leave any of the readers or writers null to auto-fill.</summary>
    public NetCallRaw(Guid method, Reader<T1>? reader1, Reader<T2>? reader2, Reader<T3>? reader3, Reader<T4>? reader4, Reader<T5>? reader5, Writer<T1>? writer1, Writer<T2>? writer2, Writer<T3>? writer3, Writer<T4>? writer4, Writer<T5>? writer5, int capacity = 0, bool highSpeed = false) : base(method, highSpeed)
    {
        _writer = new ByteWriterRaw<T1, T2, T3, T4, T5>(writer1, writer2, writer3, writer4, writer5, capacity: capacity + MessageOverhead.MaximumSize);
        _reader = new ByteReaderRaw<T1, T2, T3, T4, T5>(reader1, reader2, reader3, reader4, reader5);
    }
    /// <summary>Leave any of the readers or writers null to auto-fill.</summary>
    public NetCallRaw(Method method, Reader<T1>? reader1, Reader<T2>? reader2, Reader<T3>? reader3, Reader<T4>? reader4, Reader<T5>? reader5, Writer<T1>? writer1, Writer<T2>? writer2, Writer<T3>? writer3, Writer<T4>? writer4, Writer<T5>? writer5, int capacity = 0) : base(method)
    {
        _writer = new ByteWriterRaw<T1, T2, T3, T4, T5>(writer1, writer2, writer3, writer4, writer5, capacity: capacity + MessageOverhead.MaximumSize);
        _reader = new ByteReaderRaw<T1, T2, T3, T4, T5>(reader1, reader2, reader3, reader4, reader5);
    }
    /// <summary>Leave any of the readers or writers null to auto-fill.</summary>
    public NetCallRaw(MethodAsync method, Reader<T1>? reader1, Reader<T2>? reader2, Reader<T3>? reader3, Reader<T4>? reader4, Reader<T5>? reader5, Writer<T1>? writer1, Writer<T2>? writer2, Writer<T3>? writer3, Writer<T4>? writer4, Writer<T5>? writer5, int capacity = 0) : base(method)
    {
        _writer = new ByteWriterRaw<T1, T2, T3, T4, T5>(writer1, writer2, writer3, writer4, writer5, capacity: capacity + MessageOverhead.MaximumSize);
        _reader = new ByteReaderRaw<T1, T2, T3, T4, T5>(reader1, reader2, reader3, reader4, reader5);
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
            Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection.");
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
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to connection {connection.Format()}.");
#else
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to server.");
#endif
        }
    }
    public void Invoke(
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags
#if SERVER
                                                       | (connection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None)
#endif
                                                        , Guid, Id, 0);
        Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg1, arg2, arg3, arg4, arg5);
    }
#if SERVER
    public void Invoke(IReadOnlyList<ITransportConnection>? connections, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
    {
        if (connections is { Count: 0 }) return;
        MessageOverhead overhead = new MessageOverhead(DefaultFlags, Guid, Id, 0);
        byte[] bytes = _writer.Get(ref overhead, arg1, arg2, arg3, arg4, arg5);
        try
        {
            NetFactory.Send(connections, bytes);
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to 1+ connection(s).");
        }
    }
#endif
    public bool Read(ArraySegment<byte> message, out T1 arg1, out T2 arg2, out T3 arg3, out T4 arg4, out T5 arg5)
    {
        try
        {
            return _reader.Read(message, out arg1, out arg2, out arg3, out arg4, out arg5);
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error reading method {Id.Format()}.");
            arg1 = default!;
            arg2 = default!;
            arg3 = default!;
            arg4 = default!;
            arg5 = default!;
            return false;
        }
    }
    public override bool Read(ArraySegment<byte> message, out object[] parameters)
    {
        bool success = Read(message, out T1 a1, out T2 a2, out T3 a3, out T4 a4, out T5 a5);
        parameters = success ? new object[] { null!, a1!, a2!, a3!, a4!, a5! } : Array.Empty<object>();
        return success;
    }
#if CLIENT
    public void Invoke(ref MessageOverhead overhead, HighSpeedConnection connection, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
    {
        if (connection == null)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection.");
            return;
        }
        try
        {
            connection.Send(_writer.Get(ref overhead, arg1, arg2, arg3, arg4, arg5));
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to server.");
        }
    }
    public void Invoke(HighSpeedConnection connection, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags | MessageFlags.HighSpeed, Guid, Id, 0);
        Invoke(ref overhead, connection, arg1, arg2, arg3, arg4, arg5);
    }
    public NetTask Request(BaseNetCall listener, HighSpeedConnection connection, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task2 = listener.Listen(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(RequestFlags | MessageFlags.HighSpeed, Guid, Id, 0, task2.RequestId);
        Invoke(ref overhead, connection, arg1, arg2, arg3, arg4, arg5);
        return task2;
    }
    public NetTask RequestAck(HighSpeedConnection connection, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task2 = ListenAck(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags | MessageFlags.HighSpeed, Guid, Id, 0, task2.RequestId);
        Invoke(ref overhead, connection, arg1, arg2, arg3, arg4, arg5);
        return task2;
    }
#endif
    public NetTask Request(BaseNetCall listener,
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task = listener.Listen(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(RequestFlags
#if SERVER
                                                       | (connection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None)
#endif
                                                        , Guid, Id, 0, task.RequestId);
        Invoke(ref overhead,
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
        T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task = ListenAck(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags
#if SERVER
                                                       | (connection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None)
#endif
                                                        , Guid, Id, 0, task.RequestId);
        Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg1, arg2, arg3, arg4, arg5);
        return task;
    }

    public byte[] Write(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags, Guid, Id, 0);
        return Write(ref overhead, arg1, arg2, arg3, arg4, arg5);
    }

    public byte[] Write(ref MessageOverhead overhead, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
        => _writer.Get(ref overhead, arg1, arg2, arg3, arg4, arg5);
#if SERVER
    public NetTask[] Request(BaseNetCall listener, IReadOnlyList<ITransportConnection> connections, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        int c = 0;
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection {i.Format()}.");
            else ++c;
        }
        NetTask[] tasks = new NetTask[c];
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                continue;

            tasks[i] = listener.Listen(timeoutMs);
        }

        try
        {
            for (int i = 0; i < connections.Count; ++i)
            {
                ITransportConnection transportConnection = connections[i];
                MessageOverhead overhead = new MessageOverhead(RequestFlags | (transportConnection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None), Guid, Id, 0, tasks[i].RequestId);
                transportConnection.Send(_writer.Get(ref overhead, arg1, arg2, arg3, arg4, arg5));
            }
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to {connections.Count.Format()} connection(s).");
        }

        return tasks;
    }
    public NetTask[] RequestAck(IReadOnlyList<ITransportConnection> connections, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        int c = 0;
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection {i.Format()}.");
            else ++c;
        }
        NetTask[] tasks = new NetTask[c];
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                continue;

            tasks[i] = ListenAck(timeoutMs);
        }

        try
        {
            for (int i = 0; i < connections.Count; ++i)
            {
                ITransportConnection transportConnection = connections[i];
                MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags | (transportConnection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None), Guid, Id, 0, tasks[i].RequestId);
                transportConnection.Send(_writer.Get(ref overhead, arg1, arg2, arg3, arg4, arg5));
            }
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to {connections.Count.Format()} connection(s).");
        }

        return tasks;
    }
#endif
}
/// <summary>Leave any reader or writer null to auto-fill.</summary>
public sealed class NetCallRaw<T1, T2, T3, T4, T5, T6> : NetCallRaw
{
    private readonly ByteReaderRaw<T1, T2, T3, T4, T5, T6> _reader;
    private readonly ByteWriterRaw<T1, T2, T3, T4, T5, T6> _writer;
    public delegate void Method(MessageContext context, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6);
    public delegate Task MethodAsync(MessageContext context, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6);
    internal NetCallRaw(DevkitServerNetCall method, Reader<T1>? reader1, Reader<T2>? reader2, Reader<T3>? reader3, Reader<T4>? reader4, Reader<T5>? reader5, Reader<T6>? reader6, Writer<T1>? writer1, Writer<T2>? writer2, Writer<T3>? writer3, Writer<T4>? writer4, Writer<T5>? writer5, Writer<T6>? writer6, int capacity = 0)
        : this((ushort)method, reader1, reader2, reader3, reader4, reader5, reader6, writer1, writer2, writer3, writer4, writer5, writer6, capacity) { }
    /// <summary>Leave any of the readers or writers null to auto-fill.</summary>
    internal NetCallRaw(ushort method, Reader<T1>? reader1, Reader<T2>? reader2, Reader<T3>? reader3, Reader<T4>? reader4, Reader<T5>? reader5, Reader<T6>? reader6, Writer<T1>? writer1, Writer<T2>? writer2, Writer<T3>? writer3, Writer<T4>? writer4, Writer<T5>? writer5, Writer<T6>? writer6, int capacity = 0, bool highSpeed = false) : base(method, highSpeed)
    {
        _writer = new ByteWriterRaw<T1, T2, T3, T4, T5, T6>(writer1, writer2, writer3, writer4, writer5, writer6, capacity: capacity + MessageOverhead.MaximumSize);
        _reader = new ByteReaderRaw<T1, T2, T3, T4, T5, T6>(reader1, reader2, reader3, reader4, reader5, reader6);
    }
    /// <summary>Leave any of the readers or writers null to auto-fill.</summary>
    public NetCallRaw(Guid method, Reader<T1>? reader1, Reader<T2>? reader2, Reader<T3>? reader3, Reader<T4>? reader4, Reader<T5>? reader5, Reader<T6>? reader6, Writer<T1>? writer1, Writer<T2>? writer2, Writer<T3>? writer3, Writer<T4>? writer4, Writer<T5>? writer5, Writer<T6>? writer6, int capacity = 0, bool highSpeed = false) : base(method, highSpeed)
    {
        _writer = new ByteWriterRaw<T1, T2, T3, T4, T5, T6>(writer1, writer2, writer3, writer4, writer5, writer6, capacity: capacity + MessageOverhead.MaximumSize);
        _reader = new ByteReaderRaw<T1, T2, T3, T4, T5, T6>(reader1, reader2, reader3, reader4, reader5, reader6);
    }
    /// <summary>Leave any of the readers or writers null to auto-fill.</summary>
    public NetCallRaw(Method method, Reader<T1>? reader1, Reader<T2>? reader2, Reader<T3>? reader3, Reader<T4>? reader4, Reader<T5>? reader5, Reader<T6>? reader6, Writer<T1>? writer1, Writer<T2>? writer2, Writer<T3>? writer3, Writer<T4>? writer4, Writer<T5>? writer5, Writer<T6>? writer6, int capacity = 0) : base(method)
    {
        _writer = new ByteWriterRaw<T1, T2, T3, T4, T5, T6>(writer1, writer2, writer3, writer4, writer5, writer6, capacity: capacity + MessageOverhead.MaximumSize);
        _reader = new ByteReaderRaw<T1, T2, T3, T4, T5, T6>(reader1, reader2, reader3, reader4, reader5, reader6);
    }
    /// <summary>Leave any of the readers or writers null to auto-fill.</summary>
    public NetCallRaw(MethodAsync method, Reader<T1>? reader1, Reader<T2>? reader2, Reader<T3>? reader3, Reader<T4>? reader4, Reader<T5>? reader5, Reader<T6>? reader6, Writer<T1>? writer1, Writer<T2>? writer2, Writer<T3>? writer3, Writer<T4>? writer4, Writer<T5>? writer5, Writer<T6>? writer6, int capacity = 0) : base(method)
    {
        _writer = new ByteWriterRaw<T1, T2, T3, T4, T5, T6>(writer1, writer2, writer3, writer4, writer5, writer6, capacity: capacity + MessageOverhead.MaximumSize);
        _reader = new ByteReaderRaw<T1, T2, T3, T4, T5, T6>(reader1, reader2, reader3, reader4, reader5, reader6);
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
            Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection.");
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
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to connection {connection.Format()}.");
#else
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to server.");
#endif
        }
    }
    public void Invoke(
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags
#if SERVER
                                                       | (connection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None)
#endif
                                                        , Guid, Id, 0);
        Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg1, arg2, arg3, arg4, arg5, arg6);
    }
#if SERVER
    public void Invoke(IReadOnlyList<ITransportConnection>? connections, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
    {
        if (connections is { Count: 0 }) return;
        MessageOverhead overhead = new MessageOverhead(DefaultFlags, Guid, Id, 0);
        byte[] bytes = _writer.Get(ref overhead, arg1, arg2, arg3, arg4, arg5, arg6);
        try
        {
            NetFactory.Send(connections, bytes);
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to 1+ connection(s).");
        }
    }
#endif
    public bool Read(ArraySegment<byte> message, out T1 arg1, out T2 arg2, out T3 arg3, out T4 arg4, out T5 arg5, out T6 arg6)
    {
        try
        {
            return _reader.Read(message, out arg1, out arg2, out arg3, out arg4, out arg5, out arg6);
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error reading method {Id.Format()}.");
            arg1 = default!;
            arg2 = default!;
            arg3 = default!;
            arg4 = default!;
            arg5 = default!;
            arg6 = default!;
            return false;
        }
    }
    public override bool Read(ArraySegment<byte> message, out object[] parameters)
    {
        bool success = Read(message, out T1 a1, out T2 a2, out T3 a3, out T4 a4, out T5 a5, out T6 a6);
        parameters = success ? new object[] { null!, a1!, a2!, a3!, a4!, a5!, a6! } : Array.Empty<object>();
        return success;
    }
#if CLIENT
    public void Invoke(ref MessageOverhead overhead, HighSpeedConnection connection, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
    {
        if (connection == null)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection.");
            return;
        }
        try
        {
            connection.Send(_writer.Get(ref overhead, arg1, arg2, arg3, arg4, arg5, arg6));
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to server.");
        }
    }
    public void Invoke(HighSpeedConnection connection, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags | MessageFlags.HighSpeed, Guid, Id, 0);
        Invoke(ref overhead, connection, arg1, arg2, arg3, arg4, arg5, arg6);
    }
    public NetTask Request(BaseNetCall listener, HighSpeedConnection connection, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task2 = listener.Listen(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(RequestFlags | MessageFlags.HighSpeed, Guid, Id, 0, task2.RequestId);
        Invoke(ref overhead, connection, arg1, arg2, arg3, arg4, arg5, arg6);
        return task2;
    }
    public NetTask RequestAck(HighSpeedConnection connection, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task2 = ListenAck(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags | MessageFlags.HighSpeed, Guid, Id, 0, task2.RequestId);
        Invoke(ref overhead, connection, arg1, arg2, arg3, arg4, arg5, arg6);
        return task2;
    }
#endif
    public NetTask Request(BaseNetCall listener,
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task = listener.Listen(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(RequestFlags
#if SERVER
                                                       | (connection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None)
#endif
                                                        , Guid, Id, 0, task.RequestId);
        Invoke(ref overhead,
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
        T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task = ListenAck(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags
#if SERVER
                                                       | (connection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None)
#endif
                                                        , Guid, Id, 0, task.RequestId);
        Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg1, arg2, arg3, arg4, arg5, arg6);
        return task;
    }

    public byte[] Write(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags, Guid, Id, 0);
        return Write(ref overhead, arg1, arg2, arg3, arg4, arg5, arg6);
    }

    public byte[] Write(ref MessageOverhead overhead, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
        => _writer.Get(ref overhead, arg1, arg2, arg3, arg4, arg5, arg6);
#if SERVER
    public NetTask[] Request(BaseNetCall listener, IReadOnlyList<ITransportConnection> connections, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        int c = 0;
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection {i.Format()}.");
            else ++c;
        }
        NetTask[] tasks = new NetTask[c];
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                continue;

            tasks[i] = listener.Listen(timeoutMs);
        }

        try
        {
            for (int i = 0; i < connections.Count; ++i)
            {
                ITransportConnection transportConnection = connections[i];
                MessageOverhead overhead = new MessageOverhead(RequestFlags | (transportConnection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None), Guid, Id, 0, tasks[i].RequestId);
                transportConnection.Send(_writer.Get(ref overhead, arg1, arg2, arg3, arg4, arg5, arg6));
            }
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to {connections.Count.Format()} connection(s).");
        }

        return tasks;
    }
    public NetTask[] RequestAck(IReadOnlyList<ITransportConnection> connections, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        int c = 0;
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection {i.Format()}.");
            else ++c;
        }
        NetTask[] tasks = new NetTask[c];
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                continue;

            tasks[i] = ListenAck(timeoutMs);
        }

        try
        {
            for (int i = 0; i < connections.Count; ++i)
            {
                ITransportConnection transportConnection = connections[i];
                MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags | (transportConnection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None), Guid, Id, 0, tasks[i].RequestId);
                transportConnection.Send(_writer.Get(ref overhead, arg1, arg2, arg3, arg4, arg5, arg6));
            }
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to {connections.Count.Format()} connection(s).");
        }

        return tasks;
    }
#endif
}
public sealed class NetCall<T> : DynamicNetCall
{
    private readonly DynamicByteReader<T> _reader;
    private readonly DynamicByteWriter<T> _writer;
    public delegate void Method(MessageContext context, T arg1);
    public delegate Task MethodAsync(MessageContext context, T arg1);
    internal NetCall(DevkitServerNetCall method, int capacity = 0) : this((ushort)method, capacity) { }
    internal NetCall(ushort method, int capacity = 0, bool highSpeed = false) : base(method, highSpeed)
    {
        _reader = new DynamicByteReader<T>();
        _writer = new DynamicByteWriter<T>(capacity: capacity);
    }
    public NetCall(Guid method, int capacity = 0, bool highSpeed = false) : base(method, highSpeed)
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
            Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection.");
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
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to connection {connection.Format()}.");
#else
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to server.");
#endif
        }
    }
    public void Invoke(
#if SERVER
        ITransportConnection connection,
#endif
        T arg)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags
#if SERVER
                                                       | (connection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None)
#endif
                                                        , Guid, Id, 0);
        Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg);
    }
#if SERVER
    public void Invoke(IReadOnlyList<ITransportConnection>? connections, T arg)
    {
        if (connections is { Count: 0 }) return;
        MessageOverhead overhead = new MessageOverhead(DefaultFlags, Guid, Id, 0);
        byte[] bytes = _writer.Get(ref overhead, arg);
        try
        {
            NetFactory.Send(connections, bytes);
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to 1+ connection(s).");
        }
    }
#endif
    public bool Read(ArraySegment<byte> message, out T arg)
    {
        try
        {
            return _reader.Read(message, out arg);
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error reading method {Id.Format()}.");
            arg = default!;
            return false;
        }
    }
    public override bool Read(ArraySegment<byte> message, out object[] parameters)
    {
        bool success = Read(message, out T a1);
        parameters = success ? new object[] { null!, a1! } : Array.Empty<object>();
        return success;
    }
#if CLIENT
    public void Invoke(ref MessageOverhead overhead, HighSpeedConnection connection, T arg)
    {
        if (connection == null)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection.");
            return;
        }
        try
        {
            connection.Send(_writer.Get(ref overhead, arg));
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to server.");
        }
    }
    public void Invoke(HighSpeedConnection connection, T arg)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags | MessageFlags.HighSpeed, Guid, Id, 0);
        Invoke(ref overhead, connection, arg);
    }
    public NetTask Request(BaseNetCall listener, HighSpeedConnection connection, T arg, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task2 = listener.Listen(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(RequestFlags | MessageFlags.HighSpeed, Guid, Id, 0, task2.RequestId);
        Invoke(ref overhead, connection, arg);
        return task2;
    }
    public NetTask RequestAck(HighSpeedConnection connection, T arg, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task2 = ListenAck(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags | MessageFlags.HighSpeed, Guid, Id, 0, task2.RequestId);
        Invoke(ref overhead, connection, arg);
        return task2;
    }
#endif
    public NetTask Request(BaseNetCall listener,
#if SERVER
        ITransportConnection connection, 
#endif
        T arg, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task = listener.Listen(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(RequestFlags
#if SERVER
                                                       | (connection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None)
#endif
                                                        , Guid, Id, 0, task.RequestId);
        Invoke(ref overhead,
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
        T arg, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task = ListenAck(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags
#if SERVER
                                                       | (connection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None)
#endif
                                                        , Guid, Id, 0, task.RequestId);
        Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg);
        return task;
    }

    public byte[] Write(T arg)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags, Guid, Id, 0);
        return Write(ref overhead, arg);
    }

    public byte[] Write(ref MessageOverhead overhead, T arg)
        => _writer.Get(ref overhead, arg);
#if SERVER
    public NetTask[] Request(BaseNetCall listener, IReadOnlyList<ITransportConnection> connections, T arg, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        int c = 0;
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection {i.Format()}.");
            else ++c;
        }
        NetTask[] tasks = new NetTask[c];
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                continue;

            tasks[i] = listener.Listen(timeoutMs);
        }

        try
        {
            for (int i = 0; i < connections.Count; ++i)
            {
                ITransportConnection transportConnection = connections[i];
                MessageOverhead overhead = new MessageOverhead(RequestFlags | (transportConnection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None), Guid, Id, 0, tasks[i].RequestId);
                transportConnection.Send(_writer.Get(ref overhead, arg));
            }
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to {connections.Count.Format()} connection(s).");
        }

        return tasks;
    }
    public NetTask[] RequestAck(IReadOnlyList<ITransportConnection> connections, T arg, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        int c = 0;
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection {i.Format()}.");
            else ++c;
        }
        NetTask[] tasks = new NetTask[c];
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                continue;

            tasks[i] = ListenAck(timeoutMs);
        }

        try
        {
            for (int i = 0; i < connections.Count; ++i)
            {
                ITransportConnection transportConnection = connections[i];
                MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags | (transportConnection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None), Guid, Id, 0, tasks[i].RequestId);
                transportConnection.Send(_writer.Get(ref overhead, arg));
            }
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to {connections.Count.Format()} connection(s).");
        }

        return tasks;
    }
#endif
}
public sealed class NetCall<T1, T2> : DynamicNetCall
{
    private readonly DynamicByteReader<T1, T2> _reader;
    private readonly DynamicByteWriter<T1, T2> _writer;
    public delegate void Method(MessageContext context, T1 arg1, T2 arg2);
    public delegate Task MethodAsync(MessageContext context, T1 arg1, T2 arg2);
    internal NetCall(DevkitServerNetCall method, int capacity = 0) : this((ushort)method, capacity) { }
    internal NetCall(ushort method, int capacity = 0, bool highSpeed = false) : base(method, highSpeed)
    {
        _reader = new DynamicByteReader<T1, T2>();
        _writer = new DynamicByteWriter<T1, T2>(capacity: capacity);
    }
    public NetCall(Guid method, int capacity = 0, bool highSpeed = false) : base(method, highSpeed)
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
            Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection.");
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
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to connection {connection.Format()}.");
#else
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to server.");
#endif
        }
    }
    public void Invoke(
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags
#if SERVER
                                                       | (connection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None)
#endif
                                                        , Guid, Id, 0);
        Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg1, arg2);
    }
#if SERVER
    public void Invoke(IReadOnlyList<ITransportConnection>? connections, T1 arg1, T2 arg2)
    {
        if (connections is { Count: 0 }) return;
        MessageOverhead overhead = new MessageOverhead(DefaultFlags, Guid, Id, 0);
        byte[] bytes = _writer.Get(ref overhead, arg1, arg2);
        try
        {
            NetFactory.Send(connections, bytes);
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to 1+ connection(s).");
        }
    }
#endif
    public bool Read(ArraySegment<byte> message, out T1 arg1, out T2 arg2)
    {
        try
        {
            return _reader.Read(message, out arg1, out arg2);
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error reading method {Id.Format()}.");
            arg1 = default!;
            arg2 = default!;
            return false;
        }
    }
    public override bool Read(ArraySegment<byte> message, out object[] parameters)
    {
        bool success = Read(message, out T1 a1, out T2 a2);
        parameters = success ? new object[] { null!, a1!, a2! } : Array.Empty<object>();
        return success;
    }
#if CLIENT
    public void Invoke(ref MessageOverhead overhead, HighSpeedConnection connection, T1 arg1, T2 arg2)
    {
        if (connection == null)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection.");
            return;
        }
        try
        {
            connection.Send(_writer.Get(ref overhead, arg1, arg2));
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to server.");
        }
    }
    public void Invoke(HighSpeedConnection connection, T1 arg1, T2 arg2)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags | MessageFlags.HighSpeed, Guid, Id, 0);
        Invoke(ref overhead, connection, arg1, arg2);
    }
    public NetTask Request(BaseNetCall listener, HighSpeedConnection connection, T1 arg1, T2 arg2, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task2 = listener.Listen(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(RequestFlags | MessageFlags.HighSpeed, Guid, Id, 0, task2.RequestId);
        Invoke(ref overhead, connection, arg1, arg2);
        return task2;
    }
    public NetTask RequestAck(HighSpeedConnection connection, T1 arg1, T2 arg2, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task2 = ListenAck(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags | MessageFlags.HighSpeed, Guid, Id, 0, task2.RequestId);
        Invoke(ref overhead, connection, arg1, arg2);
        return task2;
    }
#endif
    public NetTask Request(BaseNetCall listener,
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task = listener.Listen(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(RequestFlags
#if SERVER
                                                       | (connection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None)
#endif
                                                        , Guid, Id, 0, task.RequestId);
        Invoke(ref overhead,
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
        T1 arg1, T2 arg2, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task = ListenAck(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags
#if SERVER
                                                       | (connection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None)
#endif
                                                        , Guid, Id, 0, task.RequestId);
        Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg1, arg2);
        return task;
    }

    public byte[] Write(T1 arg1, T2 arg2)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags, Guid, Id, 0);
        return Write(ref overhead, arg1, arg2);
    }

    public byte[] Write(ref MessageOverhead overhead, T1 arg1, T2 arg2)
        => _writer.Get(ref overhead, arg1, arg2);
#if SERVER
    public NetTask[] Request(BaseNetCall listener, IReadOnlyList<ITransportConnection> connections, T1 arg1, T2 arg2, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        int c = 0;
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection {i.Format()}.");
            else ++c;
        }
        NetTask[] tasks = new NetTask[c];
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                continue;

            tasks[i] = listener.Listen(timeoutMs);
        }

        try
        {
            for (int i = 0; i < connections.Count; ++i)
            {
                ITransportConnection transportConnection = connections[i];
                MessageOverhead overhead = new MessageOverhead(RequestFlags | (transportConnection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None), Guid, Id, 0, tasks[i].RequestId);
                transportConnection.Send(_writer.Get(ref overhead, arg1, arg2));
            }
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to {connections.Count.Format()} connection(s).");
        }

        return tasks;
    }
    public NetTask[] RequestAck(IReadOnlyList<ITransportConnection> connections, T1 arg1, T2 arg2, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        int c = 0;
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection {i.Format()}.");
            else ++c;
        }
        NetTask[] tasks = new NetTask[c];
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                continue;

            tasks[i] = ListenAck(timeoutMs);
        }

        try
        {
            for (int i = 0; i < connections.Count; ++i)
            {
                ITransportConnection transportConnection = connections[i];
                MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags | (transportConnection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None), Guid, Id, 0, tasks[i].RequestId);
                transportConnection.Send(_writer.Get(ref overhead, arg1, arg2));
            }
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to {connections.Count.Format()} connection(s).");
        }

        return tasks;
    }
#endif
}
public sealed class NetCall<T1, T2, T3> : DynamicNetCall
{
    private readonly DynamicByteReader<T1, T2, T3> _reader;
    private readonly DynamicByteWriter<T1, T2, T3> _writer;
    public delegate void Method(MessageContext context, T1 arg1, T2 arg2, T3 arg3);
    public delegate Task MethodAsync(MessageContext context, T1 arg1, T2 arg2, T3 arg3);
    internal NetCall(DevkitServerNetCall method, int capacity = 0) : this((ushort)method, capacity) { }
    internal NetCall(ushort method, int capacity = 0, bool highSpeed = false) : base(method, highSpeed)
    {
        _reader = new DynamicByteReader<T1, T2, T3>();
        _writer = new DynamicByteWriter<T1, T2, T3>(capacity: capacity);
    }
    public NetCall(Guid method, int capacity = 0, bool highSpeed = false) : base(method, highSpeed)
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
            Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection.");
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
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to connection {connection.Format()}.");
#else
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to server.");
#endif
        }
    }
    public void Invoke(
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2, T3 arg3)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags
#if SERVER
                                                       | (connection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None)
#endif
                                                        , Guid, Id, 0);
        Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg1, arg2, arg3);
    }
#if SERVER
    public void Invoke(IReadOnlyList<ITransportConnection>? connections, T1 arg1, T2 arg2, T3 arg3)
    {
        if (connections is { Count: 0 }) return;
        MessageOverhead overhead = new MessageOverhead(DefaultFlags, Guid, Id, 0);
        byte[] bytes = _writer.Get(ref overhead, arg1, arg2, arg3);
        try
        {
            NetFactory.Send(connections, bytes);
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to 1+ connection(s).");
        }
    }
#endif
    public bool Read(ArraySegment<byte> message, out T1 arg1, out T2 arg2, out T3 arg3)
    {
        try
        {
            return _reader.Read(message, out arg1, out arg2, out arg3);
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error reading method {Id.Format()}.");
            arg1 = default!;
            arg2 = default!;
            arg3 = default!;
            return false;
        }
    }
    public override bool Read(ArraySegment<byte> message, out object[] parameters)
    {
        bool success = Read(message, out T1 arg1, out T2 arg2, out T3 arg3);
        parameters = success ? new object[] { null!, arg1!, arg2!, arg3! } : Array.Empty<object>();
        return success;
    }
#if CLIENT
    public void Invoke(ref MessageOverhead overhead, HighSpeedConnection connection, T1 arg1, T2 arg2, T3 arg3)
    {
        if (connection == null)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection.");
            return;
        }
        try
        {
            connection.Send(_writer.Get(ref overhead, arg1, arg2, arg3));
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to server.");
        }
    }
    public void Invoke(HighSpeedConnection connection, T1 arg1, T2 arg2, T3 arg3)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags | MessageFlags.HighSpeed, Guid, Id, 0);
        Invoke(ref overhead, connection, arg1, arg2, arg3);
    }
    public NetTask Request(BaseNetCall listener, HighSpeedConnection connection, T1 arg1, T2 arg2, T3 arg3, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task2 = listener.Listen(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(RequestFlags | MessageFlags.HighSpeed, Guid, Id, 0, task2.RequestId);
        Invoke(ref overhead, connection, arg1, arg2, arg3);
        return task2;
    }
    public NetTask RequestAck(HighSpeedConnection connection, T1 arg1, T2 arg2, T3 arg3, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task2 = ListenAck(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags | MessageFlags.HighSpeed, Guid, Id, 0, task2.RequestId);
        Invoke(ref overhead, connection, arg1, arg2, arg3);
        return task2;
    }
#endif
    public NetTask Request(BaseNetCall listener,
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2, T3 arg3, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task = listener.Listen(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(RequestFlags
#if SERVER
                                                       | (connection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None)
#endif
                                                        , Guid, Id, 0, task.RequestId);
        Invoke(ref overhead,
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
        T1 arg1, T2 arg2, T3 arg3, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task = ListenAck(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags
#if SERVER
                                                       | (connection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None)
#endif
                                                        , Guid, Id, 0, task.RequestId);
        Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg1, arg2, arg3);
        return task;
    }

    public byte[] Write(T1 arg1, T2 arg2, T3 arg3)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags, Guid, Id, 0);
        return Write(ref overhead, arg1, arg2, arg3);
    }

    public byte[] Write(ref MessageOverhead overhead, T1 arg1, T2 arg2, T3 arg3)
        => _writer.Get(ref overhead, arg1, arg2, arg3);
#if SERVER
    public NetTask[] Request(BaseNetCall listener, IReadOnlyList<ITransportConnection> connections, T1 arg1, T2 arg2, T3 arg3, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        int c = 0;
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection {i.Format()}.");
            else ++c;
        }
        NetTask[] tasks = new NetTask[c];
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                continue;

            tasks[i] = listener.Listen(timeoutMs);
        }

        try
        {
            for (int i = 0; i < connections.Count; ++i)
            {
                ITransportConnection transportConnection = connections[i];
                MessageOverhead overhead = new MessageOverhead(RequestFlags | (transportConnection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None), Guid, Id, 0, tasks[i].RequestId);
                transportConnection.Send(_writer.Get(ref overhead, arg1, arg2, arg3));
            }
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to {connections.Count.Format()} connection(s).");
        }

        return tasks;
    }
    public NetTask[] RequestAck(IReadOnlyList<ITransportConnection> connections, T1 arg1, T2 arg2, T3 arg3, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        int c = 0;
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection {i.Format()}.");
            else ++c;
        }
        NetTask[] tasks = new NetTask[c];
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                continue;

            tasks[i] = ListenAck(timeoutMs);
        }

        try
        {
            for (int i = 0; i < connections.Count; ++i)
            {
                ITransportConnection transportConnection = connections[i];
                MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags | (transportConnection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None), Guid, Id, 0, tasks[i].RequestId);
                transportConnection.Send(_writer.Get(ref overhead, arg1, arg2, arg3));
            }
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to {connections.Count.Format()} connection(s).");
        }

        return tasks;
    }
#endif
}
public sealed class NetCall<T1, T2, T3, T4> : DynamicNetCall
{
    private readonly DynamicByteReader<T1, T2, T3, T4> _reader;
    private readonly DynamicByteWriter<T1, T2, T3, T4> _writer;
    public delegate void Method(MessageContext context, T1 arg1, T2 arg2, T3 arg3, T4 arg4);
    public delegate Task MethodAsync(MessageContext context, T1 arg1, T2 arg2, T3 arg3, T4 arg4);
    internal NetCall(DevkitServerNetCall method, int capacity = 0) : this((ushort)method, capacity) { }
    internal NetCall(ushort method, int capacity = 0, bool highSpeed = false) : base(method, highSpeed)
    {
        _reader = new DynamicByteReader<T1, T2, T3, T4>();
        _writer = new DynamicByteWriter<T1, T2, T3, T4>(capacity: capacity);
    }
    public NetCall(Guid method, int capacity = 0, bool highSpeed = false) : base(method, highSpeed)
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
            Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection.");
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
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to connection {connection.Format()}.");
#else
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to server.");
#endif
        }
    }
    public void Invoke(
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2, T3 arg3, T4 arg4)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags
#if SERVER
                                                       | (connection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None)
#endif
                                                        , Guid, Id, 0);
        Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg1, arg2, arg3, arg4);
    }
#if SERVER
    public void Invoke(IReadOnlyList<ITransportConnection>? connections, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
    {
        if (connections is { Count: 0 }) return;
        MessageOverhead overhead = new MessageOverhead(DefaultFlags, Guid, Id, 0);
        byte[] bytes = _writer.Get(ref overhead, arg1, arg2, arg3, arg4);
        try
        {
            NetFactory.Send(connections, bytes);
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to 1+ connection(s).");
        }
    }
#endif
    public bool Read(ArraySegment<byte> message, out T1 arg1, out T2 arg2, out T3 arg3, out T4 arg4)
    {
        try
        {
            return _reader.Read(message, out arg1, out arg2, out arg3, out arg4);
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error reading method {Id.Format()}.");
            arg1 = default!;
            arg2 = default!;
            arg3 = default!;
            arg4 = default!;
            return false;
        }
    }
    public override bool Read(ArraySegment<byte> message, out object[] parameters)
    {
        bool success = Read(message, out T1 arg1, out T2 arg2, out T3 arg3, out T4 arg4);
        parameters = success ? new object[] { null!, arg1!, arg2!, arg3!, arg4! } : Array.Empty<object>();
        return success;
    }
#if CLIENT
    public void Invoke(ref MessageOverhead overhead, HighSpeedConnection connection, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
    {
        if (connection == null)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection.");
            return;
        }
        try
        {
            connection.Send(_writer.Get(ref overhead, arg1, arg2, arg3, arg4));
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to server.");
        }
    }
    public void Invoke(HighSpeedConnection connection, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags | MessageFlags.HighSpeed, Guid, Id, 0);
        Invoke(ref overhead, connection, arg1, arg2, arg3, arg4);
    }
    public NetTask Request(BaseNetCall listener, HighSpeedConnection connection, T1 arg1, T2 arg2, T3 arg3, T4 arg4, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task2 = listener.Listen(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(RequestFlags | MessageFlags.HighSpeed, Guid, Id, 0, task2.RequestId);
        Invoke(ref overhead, connection, arg1, arg2, arg3, arg4);
        return task2;
    }
    public NetTask RequestAck(HighSpeedConnection connection, T1 arg1, T2 arg2, T3 arg3, T4 arg4, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task2 = ListenAck(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags | MessageFlags.HighSpeed, Guid, Id, 0, task2.RequestId);
        Invoke(ref overhead, connection, arg1, arg2, arg3, arg4);
        return task2;
    }
#endif
    public NetTask Request(BaseNetCall listener,
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2, T3 arg3, T4 arg4, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task = listener.Listen(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(RequestFlags
#if SERVER
                                                       | (connection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None)
#endif
                                                        , Guid, Id, 0, task.RequestId);
        Invoke(ref overhead,
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
        T1 arg1, T2 arg2, T3 arg3, T4 arg4, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task = ListenAck(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags
#if SERVER
                                                       | (connection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None)
#endif
                                                        , Guid, Id, 0, task.RequestId);
        Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg1, arg2, arg3, arg4);
        return task;
    }

    public byte[] Write(T1 arg1, T2 arg2, T3 arg3, T4 arg4)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags, Guid, Id, 0);
        return Write(ref overhead, arg1, arg2, arg3, arg4);
    }

    public byte[] Write(ref MessageOverhead overhead, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
        => _writer.Get(ref overhead, arg1, arg2, arg3, arg4);
#if SERVER
    public NetTask[] Request(BaseNetCall listener, IReadOnlyList<ITransportConnection> connections, T1 arg1, T2 arg2, T3 arg3, T4 arg4, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        int c = 0;
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection {i.Format()}.");
            else ++c;
        }
        NetTask[] tasks = new NetTask[c];
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                continue;

            tasks[i] = listener.Listen(timeoutMs);
        }

        try
        {
            for (int i = 0; i < connections.Count; ++i)
            {
                ITransportConnection transportConnection = connections[i];
                MessageOverhead overhead = new MessageOverhead(RequestFlags | (transportConnection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None), Guid, Id, 0, tasks[i].RequestId);
                transportConnection.Send(_writer.Get(ref overhead, arg1, arg2, arg3, arg4));
            }
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to {connections.Count.Format()} connection(s).");
        }

        return tasks;
    }
    public NetTask[] RequestAck(IReadOnlyList<ITransportConnection> connections, T1 arg1, T2 arg2, T3 arg3, T4 arg4, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        int c = 0;
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection {i.Format()}.");
            else ++c;
        }
        NetTask[] tasks = new NetTask[c];
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                continue;

            tasks[i] = ListenAck(timeoutMs);
        }

        try
        {
            for (int i = 0; i < connections.Count; ++i)
            {
                ITransportConnection transportConnection = connections[i];
                MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags | (transportConnection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None), Guid, Id, 0, tasks[i].RequestId);
                transportConnection.Send(_writer.Get(ref overhead, arg1, arg2, arg3, arg4));
            }
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to {connections.Count.Format()} connection(s).");
        }

        return tasks;
    }
#endif
}
public sealed class NetCall<T1, T2, T3, T4, T5> : DynamicNetCall
{
    private readonly DynamicByteReader<T1, T2, T3, T4, T5> _reader;
    private readonly DynamicByteWriter<T1, T2, T3, T4, T5> _writer;
    public delegate void Method(MessageContext context, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5);
    public delegate Task MethodAsync(MessageContext context, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5);
    internal NetCall(DevkitServerNetCall method, int capacity = 0) : this((ushort)method, capacity) { }
    internal NetCall(ushort method, int capacity = 0, bool highSpeed = false) : base(method, highSpeed)
    {
        _reader = new DynamicByteReader<T1, T2, T3, T4, T5>();
        _writer = new DynamicByteWriter<T1, T2, T3, T4, T5>(capacity: capacity);
    }
    public NetCall(Guid method, int capacity = 0, bool highSpeed = false) : base(method, highSpeed)
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
            Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection.");
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
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to connection {connection.Format()}.");
#else
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to server.");
#endif
        }
    }
    public void Invoke(
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags
#if SERVER
                                                       | (connection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None)
#endif
                                                        , Guid, Id, 0);
        Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg1, arg2, arg3, arg4, arg5);
    }
#if SERVER
    public void Invoke(IReadOnlyList<ITransportConnection>? connections, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
    {
        if (connections is { Count: 0 }) return;
        MessageOverhead overhead = new MessageOverhead(DefaultFlags, Guid, Id, 0);
        byte[] bytes = _writer.Get(ref overhead, arg1, arg2, arg3, arg4, arg5);
        try
        {
            NetFactory.Send(connections, bytes);
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to 1+ connection(s).");
        }
    }
#endif
    public bool Read(ArraySegment<byte> message, out T1 arg1, out T2 arg2, out T3 arg3, out T4 arg4, out T5 arg5)
    {
        try
        {
            return _reader.Read(message, out arg1, out arg2, out arg3, out arg4, out arg5);
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error reading method {Id.Format()}.");
            arg1 = default!;
            arg2 = default!;
            arg3 = default!;
            arg4 = default!;
            arg5 = default!;
            return false;
        }
    }
    public override bool Read(ArraySegment<byte> message, out object[] parameters)
    {
        bool success = Read(message, out T1 arg1, out T2 arg2, out T3 arg3, out T4 arg4, out T5 arg5);
        parameters = success ? new object[] { null!, arg1!, arg2!, arg3!, arg4!, arg5! } : Array.Empty<object>();
        return success;
    }
#if CLIENT
    public void Invoke(ref MessageOverhead overhead, HighSpeedConnection connection, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
    {
        if (connection == null)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection.");
            return;
        }
        try
        {
            connection.Send(_writer.Get(ref overhead, arg1, arg2, arg3, arg4, arg5));
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to server.");
        }
    }
    public void Invoke(HighSpeedConnection connection, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags | MessageFlags.HighSpeed, Guid, Id, 0);
        Invoke(ref overhead, connection, arg1, arg2, arg3, arg4, arg5);
    }
    public NetTask Request(BaseNetCall listener, HighSpeedConnection connection, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task2 = listener.Listen(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(RequestFlags | MessageFlags.HighSpeed, Guid, Id, 0, task2.RequestId);
        Invoke(ref overhead, connection, arg1, arg2, arg3, arg4, arg5);
        return task2;
    }
    public NetTask RequestAck(HighSpeedConnection connection, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task2 = ListenAck(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags | MessageFlags.HighSpeed, Guid, Id, 0, task2.RequestId);
        Invoke(ref overhead, connection, arg1, arg2, arg3, arg4, arg5);
        return task2;
    }
#endif
    public NetTask Request(BaseNetCall listener,
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task = listener.Listen(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(RequestFlags
#if SERVER
                                                       | (connection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None)
#endif
                                                        , Guid, Id, 0, task.RequestId);
        Invoke(ref overhead,
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
        T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task = ListenAck(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags
#if SERVER
                                                       | (connection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None)
#endif
                                                        , Guid, Id, 0, task.RequestId);
        Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg1, arg2, arg3, arg4, arg5);
        return task;
    }

    public byte[] Write(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags, Guid, Id, 0);
        return Write(ref overhead, arg1, arg2, arg3, arg4, arg5);
    }

    public byte[] Write(ref MessageOverhead overhead, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
        => _writer.Get(ref overhead, arg1, arg2, arg3, arg4, arg5);
#if SERVER
    public NetTask[] Request(BaseNetCall listener, IReadOnlyList<ITransportConnection> connections, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        int c = 0;
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection {i.Format()}.");
            else ++c;
        }
        NetTask[] tasks = new NetTask[c];
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                continue;

            tasks[i] = listener.Listen(timeoutMs);
        }

        try
        {
            for (int i = 0; i < connections.Count; ++i)
            {
                ITransportConnection transportConnection = connections[i];
                MessageOverhead overhead = new MessageOverhead(RequestFlags | (transportConnection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None), Guid, Id, 0, tasks[i].RequestId);
                transportConnection.Send(_writer.Get(ref overhead, arg1, arg2, arg3, arg4, arg5));
            }
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to {connections.Count.Format()} connection(s).");
        }

        return tasks;
    }
    public NetTask[] RequestAck(IReadOnlyList<ITransportConnection> connections, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        int c = 0;
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection {i.Format()}.");
            else ++c;
        }
        NetTask[] tasks = new NetTask[c];
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                continue;

            tasks[i] = ListenAck(timeoutMs);
        }

        try
        {
            for (int i = 0; i < connections.Count; ++i)
            {
                ITransportConnection transportConnection = connections[i];
                MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags | (transportConnection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None), Guid, Id, 0, tasks[i].RequestId);
                transportConnection.Send(_writer.Get(ref overhead, arg1, arg2, arg3, arg4, arg5));
            }
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to {connections.Count.Format()} connection(s).");
        }

        return tasks;
    }
#endif
}
public sealed class NetCall<T1, T2, T3, T4, T5, T6> : DynamicNetCall
{
    private readonly DynamicByteReader<T1, T2, T3, T4, T5, T6> _reader;
    private readonly DynamicByteWriter<T1, T2, T3, T4, T5, T6> _writer;
    public delegate void Method(MessageContext context, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6);
    public delegate Task MethodAsync(MessageContext context, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6);
    internal NetCall(DevkitServerNetCall method, int capacity = 0) : this((ushort)method, capacity) { }
    internal NetCall(ushort method, int capacity = 0, bool highSpeed = false) : base(method, highSpeed)
    {
        _reader = new DynamicByteReader<T1, T2, T3, T4, T5, T6>();
        _writer = new DynamicByteWriter<T1, T2, T3, T4, T5, T6>(capacity: capacity);
    }
    public NetCall(Guid method, int capacity = 0, bool highSpeed = false) : base(method, highSpeed)
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
            Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection.");
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
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to connection {connection.Format()}.");
#else
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to server.");
#endif
        }
    }
    public void Invoke(
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags
#if SERVER
                                                       | (connection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None)
#endif
                                                        , Guid, Id, 0);
        Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg1, arg2, arg3, arg4, arg5, arg6);
    }
#if SERVER
    public void Invoke(IReadOnlyList<ITransportConnection>? connections, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
    {
        if (connections is { Count: 0 }) return;
        MessageOverhead overhead = new MessageOverhead(DefaultFlags, Guid, Id, 0);
        byte[] bytes = _writer.Get(ref overhead, arg1, arg2, arg3, arg4, arg5, arg6);
        try
        {
            NetFactory.Send(connections, bytes);
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to 1+ connection(s).");
        }
    }
#endif
    public bool Read(ArraySegment<byte> message, out T1 arg1, out T2 arg2, out T3 arg3, out T4 arg4, out T5 arg5, out T6 arg6)
    {
        try
        {
            return _reader.Read(message, out arg1, out arg2, out arg3, out arg4, out arg5, out arg6);
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error reading method {Id.Format()}.");
            arg1 = default!;
            arg2 = default!;
            arg3 = default!;
            arg4 = default!;
            arg5 = default!;
            arg6 = default!;
            return false;
        }
    }
    public override bool Read(ArraySegment<byte> message, out object[] parameters)
    {
        bool success = Read(message, out T1 arg1, out T2 arg2, out T3 arg3, out T4 arg4, out T5 arg5, out T6 arg6);
        parameters = success ? new object[] { null!, arg1!, arg2!, arg3!, arg4!, arg5!, arg6! } : Array.Empty<object>();
        return success;
    }
#if CLIENT
    public void Invoke(ref MessageOverhead overhead, HighSpeedConnection connection, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
    {
        if (connection == null)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection.");
            return;
        }
        try
        {
            connection.Send(_writer.Get(ref overhead, arg1, arg2, arg3, arg4, arg5, arg6));
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to server.");
        }
    }
    public void Invoke(HighSpeedConnection connection, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags | MessageFlags.HighSpeed, Guid, Id, 0);
        Invoke(ref overhead, connection, arg1, arg2, arg3, arg4, arg5, arg6);
    }
    public NetTask Request(BaseNetCall listener, HighSpeedConnection connection, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task2 = listener.Listen(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(RequestFlags | MessageFlags.HighSpeed, Guid, Id, 0, task2.RequestId);
        Invoke(ref overhead, connection, arg1, arg2, arg3, arg4, arg5, arg6);
        return task2;
    }
    public NetTask RequestAck(HighSpeedConnection connection, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task2 = ListenAck(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags | MessageFlags.HighSpeed, Guid, Id, 0, task2.RequestId);
        Invoke(ref overhead, connection, arg1, arg2, arg3, arg4, arg5, arg6);
        return task2;
    }
#endif
    public NetTask Request(BaseNetCall listener,
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task = listener.Listen(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(RequestFlags
#if SERVER
                                                       | (connection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None)
#endif
                                                        , Guid, Id, 0, task.RequestId);
        Invoke(ref overhead,
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
        T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task = ListenAck(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags
#if SERVER
                                                       | (connection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None)
#endif
                                                        , Guid, Id, 0, task.RequestId);
        Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg1, arg2, arg3, arg4, arg5, arg6);
        return task;
    }

    public byte[] Write(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags, Guid, Id, 0);
        return Write(ref overhead, arg1, arg2, arg3, arg4, arg5, arg6);
    }

    public byte[] Write(ref MessageOverhead overhead, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
        => _writer.Get(ref overhead, arg1, arg2, arg3, arg4, arg5, arg6);
#if SERVER
    public NetTask[] Request(BaseNetCall listener, IReadOnlyList<ITransportConnection> connections, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        int c = 0;
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection {i.Format()}.");
            else ++c;
        }
        NetTask[] tasks = new NetTask[c];
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                continue;

            tasks[i] = listener.Listen(timeoutMs);
        }

        try
        {
            for (int i = 0; i < connections.Count; ++i)
            {
                ITransportConnection transportConnection = connections[i];
                MessageOverhead overhead = new MessageOverhead(RequestFlags | (transportConnection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None), Guid, Id, 0, tasks[i].RequestId);
                transportConnection.Send(_writer.Get(ref overhead, arg1, arg2, arg3, arg4, arg5, arg6));
            }
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to {connections.Count.Format()} connection(s).");
        }

        return tasks;
    }
    public NetTask[] RequestAck(IReadOnlyList<ITransportConnection> connections, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        int c = 0;
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection {i.Format()}.");
            else ++c;
        }
        NetTask[] tasks = new NetTask[c];
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                continue;

            tasks[i] = ListenAck(timeoutMs);
        }

        try
        {
            for (int i = 0; i < connections.Count; ++i)
            {
                ITransportConnection transportConnection = connections[i];
                MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags | (transportConnection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None), Guid, Id, 0, tasks[i].RequestId);
                transportConnection.Send(_writer.Get(ref overhead, arg1, arg2, arg3, arg4, arg5, arg6));
            }
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to {connections.Count.Format()} connection(s).");
        }

        return tasks;
    }
#endif
}

public sealed class NetCall<T1, T2, T3, T4, T5, T6, T7> : DynamicNetCall
{
    private readonly DynamicByteReader<T1, T2, T3, T4, T5, T6, T7> _reader;
    private readonly DynamicByteWriter<T1, T2, T3, T4, T5, T6, T7> _writer;

    public delegate void Method(MessageContext context, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7);

    public delegate Task MethodAsync(MessageContext context, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6,
        T7 arg7);

    internal NetCall(DevkitServerNetCall method, int capacity = 0) : this((ushort)method, capacity) { }
    internal NetCall(ushort method, int capacity = 0, bool highSpeed = false) : base(method, highSpeed)
    {
        _reader = new DynamicByteReader<T1, T2, T3, T4, T5, T6, T7>();
        _writer = new DynamicByteWriter<T1, T2, T3, T4, T5, T6, T7>(capacity: capacity);
    }
    public NetCall(Guid method, int capacity = 0, bool highSpeed = false) : base(method, highSpeed)
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
            Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection.");
            return;
        }
#endif
        try
        {
#if SERVER
            connection.Send(_writer.Get(ref overhead, arg1, arg2, arg3, arg4, arg5, arg6, arg7));
#else
            NetFactory.GetPlayerTransportConnection()
                .Send(_writer.Get(ref overhead, arg1, arg2, arg3, arg4, arg5, arg6, arg7));
#endif
        }
        catch (Exception ex)
        {
#if SERVER
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to connection {connection.Format()}.");
#else
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to server.");
#endif
        }
    }

    public void Invoke(
#if SERVER
        ITransportConnection connection,
#endif
        T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags
#if SERVER
                                                       | (connection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None)
#endif
                                                        , Guid, Id, 0);
        Invoke(ref overhead,
#if SERVER
            connection,
#endif
            arg1, arg2, arg3, arg4, arg5, arg6, arg7);
    }
#if SERVER
    public void Invoke(IReadOnlyList<ITransportConnection>? connections, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7)
    {
        if (connections is { Count: 0 }) return;
        MessageOverhead overhead = new MessageOverhead(DefaultFlags, Guid, Id, 0);
        byte[] bytes = _writer.Get(ref overhead, arg1, arg2, arg3, arg4, arg5, arg6, arg7);
        try
        {
            NetFactory.Send(connections, bytes);
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to 1+ connection(s).");
        }
    }
#endif
    public bool Read(ArraySegment<byte> message, out T1 arg1, out T2 arg2, out T3 arg3, out T4 arg4, out T5 arg5, out T6 arg6,
        out T7 arg7)
    {
        try
        {
            return _reader.Read(message, out arg1, out arg2, out arg3, out arg4, out arg5, out arg6, out arg7);
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error reading method {Id.Format()}.");
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

    public override bool Read(ArraySegment<byte> message, out object[] parameters)
    {
        bool success = Read(message, out T1 arg1, out T2 arg2, out T3 arg3, out T4 arg4, out T5 arg5, out T6 arg6, out T7 arg7);
        parameters = success ? new object[] { null!, arg1!, arg2!, arg3!, arg4!, arg5!, arg6!, arg7! } : Array.Empty<object>();
        return success;
    }

#if CLIENT
    public void Invoke(ref MessageOverhead overhead, HighSpeedConnection connection, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7)
    {
        if (connection == null)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection.");
            return;
        }
        try
        {
            connection.Send(_writer.Get(ref overhead, arg1, arg2, arg3, arg4, arg5, arg6, arg7));
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to server.");
        }
    }
    public void Invoke(HighSpeedConnection connection, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags | MessageFlags.HighSpeed, Guid, Id, 0);
        Invoke(ref overhead, connection, arg1, arg2, arg3, arg4, arg5, arg6, arg7);
    }
    public NetTask Request(BaseNetCall listener, HighSpeedConnection connection, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task2 = listener.Listen(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(RequestFlags | MessageFlags.HighSpeed, Guid, Id, 0, task2.RequestId);
        Invoke(ref overhead, connection, arg1, arg2, arg3, arg4, arg5, arg6, arg7);
        return task2;
    }
    public NetTask RequestAck(HighSpeedConnection connection, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task2 = ListenAck(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags | MessageFlags.HighSpeed, Guid, Id, 0, task2.RequestId);
        Invoke(ref overhead, connection, arg1, arg2, arg3, arg4, arg5, arg6, arg7);
        return task2;
    }
#endif
    public NetTask Request(BaseNetCall listener,
#if SERVER
        ITransportConnection connection,
#endif
        T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task = listener.Listen(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(RequestFlags
#if SERVER
                                                       | (connection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None)
#endif
                                                        , Guid, Id, 0, task.RequestId);
        Invoke(ref overhead,
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
        T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task = ListenAck(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags
#if SERVER
                                                       | (connection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None)
#endif
                                                        , Guid, Id, 0, task.RequestId);
        Invoke(ref overhead,
#if SERVER
            connection,
#endif
            arg1, arg2, arg3, arg4, arg5, arg6, arg7);
        return task;

    }

    public byte[] Write(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags, Guid, Id, 0);
        return Write(ref overhead, arg1, arg2, arg3, arg4, arg5, arg6, arg7);
    }

    public byte[] Write(ref MessageOverhead overhead, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7)
        => _writer.Get(ref overhead, arg1, arg2, arg3, arg4, arg5, arg6, arg7);
#if SERVER
    public NetTask[] Request(BaseNetCall listener, IReadOnlyList<ITransportConnection> connections, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        int c = 0;
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection {i.Format()}.");
            else ++c;
        }
        NetTask[] tasks = new NetTask[c];
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                continue;

            tasks[i] = listener.Listen(timeoutMs);
        }

        try
        {
            for (int i = 0; i < connections.Count; ++i)
            {
                ITransportConnection transportConnection = connections[i];
                MessageOverhead overhead = new MessageOverhead(RequestFlags | (transportConnection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None), Guid, Id, 0, tasks[i].RequestId);
                transportConnection.Send(_writer.Get(ref overhead, arg1, arg2, arg3, arg4, arg5, arg6, arg7));
            }
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to {connections.Count.Format()} connection(s).");
        }

        return tasks;
    }
    public NetTask[] RequestAck(IReadOnlyList<ITransportConnection> connections, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        int c = 0;
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection {i.Format()}.");
            else ++c;
        }
        NetTask[] tasks = new NetTask[c];
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                continue;

            tasks[i] = ListenAck(timeoutMs);
        }

        try
        {
            for (int i = 0; i < connections.Count; ++i)
            {
                ITransportConnection transportConnection = connections[i];
                MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags | (transportConnection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None), Guid, Id, 0, tasks[i].RequestId);
                transportConnection.Send(_writer.Get(ref overhead, arg1, arg2, arg3, arg4, arg5, arg6, arg7));
            }
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to {connections.Count.Format()} connection(s).");
        }

        return tasks;
    }
#endif
}

public sealed class NetCall<T1, T2, T3, T4, T5, T6, T7, T8> : DynamicNetCall
{
    private readonly DynamicByteReader<T1, T2, T3, T4, T5, T6, T7, T8> _reader;
    private readonly DynamicByteWriter<T1, T2, T3, T4, T5, T6, T7, T8> _writer;
    public delegate void Method(MessageContext context, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8);
    public delegate Task MethodAsync(MessageContext context, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8);
    internal NetCall(DevkitServerNetCall method, int capacity = 0) : this((ushort)method, capacity) { }
    internal NetCall(ushort method, int capacity = 0, bool highSpeed = false) : base(method, highSpeed)
    {
        _reader = new DynamicByteReader<T1, T2, T3, T4, T5, T6, T7, T8>();
        _writer = new DynamicByteWriter<T1, T2, T3, T4, T5, T6, T7, T8>(capacity: capacity);
    }
    public NetCall(Guid method, int capacity = 0, bool highSpeed = false) : base(method, highSpeed)
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
            Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection.");
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
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to connection {connection.Format()}.");
#else
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to server.");
#endif
        }
    }
    public void Invoke(
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags
#if SERVER
                                                       | (connection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None)
#endif
                                                        , Guid, Id, 0);
        Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
    }
#if SERVER
    public void Invoke(IReadOnlyList<ITransportConnection>? connections, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8)
    {
        if (connections is { Count: 0 }) return;
        MessageOverhead overhead = new MessageOverhead(DefaultFlags, Guid, Id, 0);
        byte[] bytes = _writer.Get(ref overhead, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
        try
        {
            NetFactory.Send(connections, bytes);
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to 1+ connection(s).");
        }
    }
#endif
    public bool Read(ArraySegment<byte> message, out T1 arg1, out T2 arg2, out T3 arg3, out T4 arg4, out T5 arg5, out T6 arg6, out T7 arg7, out T8 arg8)
    {
        try
        {
            return _reader.Read(message, out arg1, out arg2, out arg3, out arg4, out arg5, out arg6, out arg7, out arg8);
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error reading method {Id.Format()}.");
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
    public override bool Read(ArraySegment<byte> message, out object[] parameters)
    {
        bool success = Read(message, out T1 arg1, out T2 arg2, out T3 arg3, out T4 arg4, out T5 arg5, out T6 arg6, out T7 arg7, out T8 arg8);
        parameters = success ? new object[] { null!, arg1!, arg2!, arg3!, arg4!, arg5!, arg6!, arg7!, arg8! } : Array.Empty<object>();
        return success;
    }
#if CLIENT
    public void Invoke(ref MessageOverhead overhead, HighSpeedConnection connection, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8)
    {
        if (connection == null)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection.");
            return;
        }
        try
        {
            connection.Send(_writer.Get(ref overhead, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8));
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to server.");
        }
    }
    public void Invoke(HighSpeedConnection connection, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags | MessageFlags.HighSpeed, Guid, Id, 0);
        Invoke(ref overhead, connection, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
    }
    public NetTask Request(BaseNetCall listener, HighSpeedConnection connection, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task2 = listener.Listen(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(RequestFlags | MessageFlags.HighSpeed, Guid, Id, 0, task2.RequestId);
        Invoke(ref overhead, connection, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
        return task2;
    }
    public NetTask RequestAck(HighSpeedConnection connection, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task2 = ListenAck(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags | MessageFlags.HighSpeed, Guid, Id, 0, task2.RequestId);
        Invoke(ref overhead, connection, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
        return task2;
    }
#endif
    public NetTask Request(BaseNetCall listener,
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task = listener.Listen(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(RequestFlags
#if SERVER
                                                       | (connection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None)
#endif
                                                        , Guid, Id, 0, task.RequestId);
        Invoke(ref overhead,
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
        T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task = ListenAck(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags
#if SERVER
                                                       | (connection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None)
#endif
                                                        , Guid, Id, 0, task.RequestId);
        Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
        return task;
    }
    public byte[] Write(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags, Guid, Id, 0);
        return Write(ref overhead, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
    }

    public byte[] Write(ref MessageOverhead overhead, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8)
        => _writer.Get(ref overhead, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
#if SERVER
    public NetTask[] Request(BaseNetCall listener, IReadOnlyList<ITransportConnection> connections, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        int c = 0;
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection {i.Format()}.");
            else ++c;
        }
        NetTask[] tasks = new NetTask[c];
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                continue;

            tasks[i] = listener.Listen(timeoutMs);
        }

        try
        {
            for (int i = 0; i < connections.Count; ++i)
            {
                ITransportConnection transportConnection = connections[i];
                MessageOverhead overhead = new MessageOverhead(RequestFlags | (transportConnection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None), Guid, Id, 0, tasks[i].RequestId);
                transportConnection.Send(_writer.Get(ref overhead, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8));
            }
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to {connections.Count.Format()} connection(s).");
        }

        return tasks;
    }
    public NetTask[] RequestAck(IReadOnlyList<ITransportConnection> connections, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        int c = 0;
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection {i.Format()}.");
            else ++c;
        }
        NetTask[] tasks = new NetTask[c];
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                continue;

            tasks[i] = ListenAck(timeoutMs);
        }

        try
        {
            for (int i = 0; i < connections.Count; ++i)
            {
                ITransportConnection transportConnection = connections[i];
                MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags | (transportConnection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None), Guid, Id, 0, tasks[i].RequestId);
                transportConnection.Send(_writer.Get(ref overhead, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8));
            }
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to {connections.Count.Format()} connection(s).");
        }

        return tasks;
    }
#endif
}
public sealed class NetCall<T1, T2, T3, T4, T5, T6, T7, T8, T9> : DynamicNetCall
{
    private readonly DynamicByteReader<T1, T2, T3, T4, T5, T6, T7, T8, T9> _reader;
    private readonly DynamicByteWriter<T1, T2, T3, T4, T5, T6, T7, T8, T9> _writer;
    public delegate void Method(MessageContext context, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9);
    public delegate Task MethodAsync(MessageContext context, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9);
    internal NetCall(DevkitServerNetCall method, int capacity = 0) : this((ushort)method, capacity) { }
    internal NetCall(ushort method, int capacity = 0, bool highSpeed = false) : base(method, highSpeed)
    {
        _reader = new DynamicByteReader<T1, T2, T3, T4, T5, T6, T7, T8, T9>();
        _writer = new DynamicByteWriter<T1, T2, T3, T4, T5, T6, T7, T8, T9>(capacity: capacity);
    }
    public NetCall(Guid method, int capacity = 0, bool highSpeed = false) : base(method, highSpeed)
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
            Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection.");
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
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to connection {connection.Format()}.");
#else
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to server.");
#endif
        }
    }
    public void Invoke(
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags
#if SERVER
                                                       | (connection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None)
#endif
                                                        , Guid, Id, 0);
        Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);
    }
#if SERVER
    public void Invoke(IReadOnlyList<ITransportConnection>? connections, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9)
    {
        if (connections is { Count: 0 }) return;
        MessageOverhead overhead = new MessageOverhead(DefaultFlags, Guid, Id, 0);
        byte[] bytes = _writer.Get(ref overhead, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);
        try
        {
            NetFactory.Send(connections, bytes);
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to 1+ connection(s).");
        }
    }
#endif
    public bool Read(ArraySegment<byte> message, out T1 arg1, out T2 arg2, out T3 arg3, out T4 arg4, out T5 arg5, out T6 arg6, out T7 arg7, out T8 arg8, out T9 arg9)
    {
        try
        {
            return _reader.Read(message, out arg1, out arg2, out arg3, out arg4, out arg5, out arg6, out arg7, out arg8, out arg9);
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error reading method {Id.Format()}.");
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
    public override bool Read(ArraySegment<byte> message, out object[] parameters)
    {
        bool success = Read(message, out T1 arg1, out T2 arg2, out T3 arg3, out T4 arg4, out T5 arg5, out T6 arg6, out T7 arg7, out T8 arg8, out T9 arg9);
        parameters = success ? new object[] { null!, arg1!, arg2!, arg3!, arg4!, arg5!, arg6!, arg7!, arg8!, arg9! } : Array.Empty<object>();
        return success;
    }
#if CLIENT
    public void Invoke(ref MessageOverhead overhead, HighSpeedConnection connection, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9)
    {
        if (connection == null)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection.");
            return;
        }
        try
        {
            connection.Send(_writer.Get(ref overhead, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9));
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to server.");
        }
    }
    public void Invoke(HighSpeedConnection connection, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags | MessageFlags.HighSpeed, Guid, Id, 0);
        Invoke(ref overhead, connection, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);
    }
    public NetTask Request(BaseNetCall listener, HighSpeedConnection connection, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task2 = listener.Listen(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(RequestFlags | MessageFlags.HighSpeed, Guid, Id, 0, task2.RequestId);
        Invoke(ref overhead, connection, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);
        return task2;
    }
    public NetTask RequestAck(HighSpeedConnection connection, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task2 = ListenAck(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags | MessageFlags.HighSpeed, Guid, Id, 0, task2.RequestId);
        Invoke(ref overhead, connection, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);
        return task2;
    }
#endif
    public NetTask Request(BaseNetCall listener,
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task = listener.Listen(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(RequestFlags
#if SERVER
                                                       | (connection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None)
#endif
                                                        , Guid, Id, 0, task.RequestId);
        Invoke(ref overhead,
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
        T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task = ListenAck(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags
#if SERVER
                                                       | (connection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None)
#endif
                                                        , Guid, Id, 0, task.RequestId);
        Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);
        return task;
    }
    public byte[] Write(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags, Guid, Id, 0);
        return Write(ref overhead, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);
    }

    public byte[] Write(ref MessageOverhead overhead, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9)
        => _writer.Get(ref overhead, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);
#if SERVER
    public NetTask[] Request(BaseNetCall listener, IReadOnlyList<ITransportConnection> connections, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        int c = 0;
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection {i.Format()}.");
            else ++c;
        }
        NetTask[] tasks = new NetTask[c];
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                continue;

            tasks[i] = listener.Listen(timeoutMs);
        }

        try
        {
            for (int i = 0; i < connections.Count; ++i)
            {
                ITransportConnection transportConnection = connections[i];
                MessageOverhead overhead = new MessageOverhead(RequestFlags | (transportConnection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None), Guid, Id, 0, tasks[i].RequestId);
                transportConnection.Send(_writer.Get(ref overhead, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9));
            }
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to {connections.Count.Format()} connection(s).");
        }

        return tasks;
    }
    public NetTask[] RequestAck(IReadOnlyList<ITransportConnection> connections, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        int c = 0;
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection {i.Format()}.");
            else ++c;
        }
        NetTask[] tasks = new NetTask[c];
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                continue;

            tasks[i] = ListenAck(timeoutMs);
        }

        try
        {
            for (int i = 0; i < connections.Count; ++i)
            {
                ITransportConnection transportConnection = connections[i];
                MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags | (transportConnection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None), Guid, Id, 0, tasks[i].RequestId);
                transportConnection.Send(_writer.Get(ref overhead, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9));
            }
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to {connections.Count.Format()} connection(s).");
        }

        return tasks;
    }
#endif
}
public sealed class NetCall<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> : DynamicNetCall
{
    private readonly DynamicByteReader<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> _reader;
    private readonly DynamicByteWriter<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> _writer;
    public delegate void Method(MessageContext context, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10);
    public delegate Task MethodAsync(MessageContext context, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10);
    internal NetCall(DevkitServerNetCall method, int capacity = 0) : this((ushort)method, capacity) { }
    internal NetCall(ushort method, int capacity = 0, bool highSpeed = false) : base(method, highSpeed)
    {
        _reader = new DynamicByteReader<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>();
        _writer = new DynamicByteWriter<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(capacity: capacity);
    }
    public NetCall(Guid method, int capacity = 0, bool highSpeed = false) : base(method, highSpeed)
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
            Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection.");
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
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to connection {connection.Format()}.");
#else
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to server.");
#endif
        }
    }
    public void Invoke(
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags
#if SERVER
                                                       | (connection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None)
#endif
                                                        , Guid, Id, 0);
        Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10);
    }
#if SERVER
    public void Invoke(IReadOnlyList<ITransportConnection>? connections, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10)
    {
        if (connections is { Count: 0 }) return;
        MessageOverhead overhead = new MessageOverhead(DefaultFlags, Guid, Id, 0);
        byte[] bytes = _writer.Get(ref overhead, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10);
        try
        {
            NetFactory.Send(connections, bytes);
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to 1+ connection(s).");
        }
    }
#endif
    public bool Read(ArraySegment<byte> message, out T1 arg1, out T2 arg2, out T3 arg3, out T4 arg4, out T5 arg5, out T6 arg6, out T7 arg7, out T8 arg8, out T9 arg9, out T10 arg10)
    {
        try
        {
            return _reader.Read(message, out arg1, out arg2, out arg3, out arg4, out arg5, out arg6, out arg7, out arg8, out arg9, out arg10);
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error reading method {Id.Format()}.");
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
    public override bool Read(ArraySegment<byte> message, out object[] parameters)
    {
        bool success = Read(message, out T1 arg1, out T2 arg2, out T3 arg3, out T4 arg4, out T5 arg5, out T6 arg6, out T7 arg7, out T8 arg8, out T9 arg9, out T10 arg10);
        parameters = success ? new object[] { null!, arg1!, arg2!, arg3!, arg4!, arg5!, arg6!, arg7!, arg8!, arg9!, arg10! } : Array.Empty<object>();
        return success;
    }
#if CLIENT
    public void Invoke(ref MessageOverhead overhead, HighSpeedConnection connection, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10)
    {
        if (connection == null)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection.");
            return;
        }
        try
        {
            connection.Send(_writer.Get(ref overhead, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10));
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to server.");
        }
    }
    public void Invoke(HighSpeedConnection connection, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags | MessageFlags.HighSpeed, Guid, Id, 0);
        Invoke(ref overhead, connection, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10);
    }
    public NetTask Request(BaseNetCall listener, HighSpeedConnection connection, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task2 = listener.Listen(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(RequestFlags | MessageFlags.HighSpeed, Guid, Id, 0, task2.RequestId);
        Invoke(ref overhead, connection, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10);
        return task2;
    }
    public NetTask RequestAck(HighSpeedConnection connection, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task2 = ListenAck(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags | MessageFlags.HighSpeed, Guid, Id, 0, task2.RequestId);
        Invoke(ref overhead, connection, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10);
        return task2;
    }
#endif
    public NetTask Request(BaseNetCall listener,
#if SERVER
        ITransportConnection connection, 
#endif
        T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task = listener.Listen(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(RequestFlags
#if SERVER
                                                       | (connection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None)
#endif
                                                        , Guid, Id, 0, task.RequestId);
        Invoke(ref overhead,
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
        T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        NetTask task = ListenAck(timeoutMs);
        MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags
#if SERVER
                                                       | (connection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None)
#endif
                                                        , Guid, Id, 0, task.RequestId);
        Invoke(ref overhead,
#if SERVER
            connection, 
#endif
            arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10);
        return task;
    }
    public byte[] Write(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10)
    {
        MessageOverhead overhead = new MessageOverhead(DefaultFlags, Guid, Id, 0);
        return Write(ref overhead, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10);
    }

    public byte[] Write(ref MessageOverhead overhead, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10)
        => _writer.Get(ref overhead, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10);
#if SERVER
    public NetTask[] Request(BaseNetCall listener, IReadOnlyList<ITransportConnection> connections, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        int c = 0;
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection {i.Format()}.");
            else ++c;
        }
        NetTask[] tasks = new NetTask[c];
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                continue;

            tasks[i] = listener.Listen(timeoutMs);
        }

        try
        {
            for (int i = 0; i < connections.Count; ++i)
            {
                ITransportConnection transportConnection = connections[i];
                MessageOverhead overhead = new MessageOverhead(RequestFlags | (transportConnection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None), Guid, Id, 0, tasks[i].RequestId);
                transportConnection.Send(_writer.Get(ref overhead, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10));
            }
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to {connections.Count.Format()} connection(s).");
        }

        return tasks;
    }
    public NetTask[] RequestAck(IReadOnlyList<ITransportConnection> connections, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, int timeoutMs = NetTask.DefaultTimeoutMilliseconds)
    {
        int c = 0;
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                Logger.DevkitServer.LogError("NET INVOCABLES", $"Error sending method {Id.Format()} to null connection {i.Format()}.");
            else ++c;
        }
        NetTask[] tasks = new NetTask[c];
        for (int i = 0; i < connections.Count; ++i)
        {
            if (connections[i] == null)
                continue;

            tasks[i] = ListenAck(timeoutMs);
        }

        try
        {
            for (int i = 0; i < connections.Count; ++i)
            {
                ITransportConnection transportConnection = connections[i];
                MessageOverhead overhead = new MessageOverhead(AcknowledgeRequestFlags | (transportConnection is HighSpeedConnection ? MessageFlags.HighSpeed : MessageFlags.None), Guid, Id, 0, tasks[i].RequestId);
                transportConnection.Send(_writer.Get(ref overhead, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10));
            }
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("NET INVOCABLES", ex, $"Error sending method {Id.Format()} to {connections.Count.Format()} connection(s).");
        }

        return tasks;
    }
#endif
}