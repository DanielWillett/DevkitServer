using DevkitServer.Players;
using DevkitServer.Util.Encoding;

namespace DevkitServer.Multiplayer.Networking;

/// <summary>
/// Contains context for <see cref="DevkitServerMessage.InvokeMethod"/> messges, such as the overhead, connection source, and if it was sent over a High-Speed connection.
/// </summary>
/// <remarks>Simulate with <see cref="CreateFromCaller"/>.</remarks>
public readonly struct MessageContext
{
    public static readonly MessageContext Nil = new MessageContext(null!, new MessageOverhead(MessageFlags.None, 0, 0), false);
    public readonly
#if SERVER
        ITransportConnection
#else
        IClientTransport
#endif
        Connection;
    public readonly MessageOverhead Overhead;
    public bool IsHighSpeed { get; }
    public bool IsExternal => (Overhead.Flags & MessageFlags.Guid) != 0;
    public bool IsRequest => (Overhead.Flags & MessageFlags.Request) != 0;
    public bool IsLayered => (Overhead.Flags & MessageFlags.RunOriginalMethodOnRequest) != 0;
    public bool IsResponse => (Overhead.Flags & MessageFlags.RequestResponse) != 0;
    public bool IsAckRequest => (Overhead.Flags & MessageFlags.AcknowledgeRequest) != 0;
    internal MessageContext(
#if SERVER
        ITransportConnection
#else
        IClientTransport
#endif
            connection, MessageOverhead overhead, bool hs)
    {
        Connection = connection;
        Overhead = overhead;
        IsHighSpeed = hs;
    }
#if SERVER
    public EditorUser? GetCaller()
    {
        return Connection is not HighSpeedConnection hs ? UserManager.FromConnection(Connection) : UserManager.FromId(hs.Steam64);
    }
    public static MessageContext CreateFromCaller(EditorUser user)
    {
        return new MessageContext(user.Connection, new MessageOverhead(MessageFlags.None, 0, 0), false);
    }
#endif
    public MessageOverhead GetReplyOverhead(BaseNetCall call, bool layered, bool ack)
    {
        MessageFlags flags;
        if ((Overhead.Flags & MessageFlags.Request) == MessageFlags.Request)
        {
            flags = call.DefaultFlags;
            if (Overhead.RequestKey != 0)
                flags |= MessageFlags.RequestResponse;
            if (layered)
                flags |= MessageFlags.RunOriginalMethodOnRequest;
        }
        else flags = Overhead.Flags;

        if (ack)
            flags |= MessageFlags.AcknowledgeRequest;
        return new MessageOverhead(flags, call.Guid, call.Id, 0, Overhead.RequestKey);
    }
    public bool Acknowledge()
    {
        if (Connection is null)
            return false;

        if ((Overhead.Flags & MessageFlags.AcknowledgeRequest) == MessageFlags.AcknowledgeRequest)
        {
            MessageFlags flags = Overhead.Flags & ~MessageFlags.AcknowledgeRequest;
            long k = Overhead.ResponseKey != 0 ? Overhead.ResponseKey : Overhead.RequestKey;
            if (k != 0)
            {
                flags |= MessageFlags.AcknowledgeResponse;
                MessageOverhead overhead = new MessageOverhead(flags, Overhead.MessageId, 0, k);
                byte[] bytes = overhead.GetBytes();
                Connection.Send(bytes);
                return true;
            }
        }
        return false;
    }

    public bool Acknowledge(StandardErrorCode errorCode) => Acknowledge((int)errorCode);
    public unsafe bool Acknowledge(int errorCode)
    {
        if (Connection is null)
            return false;

        if ((Overhead.Flags & MessageFlags.AcknowledgeRequest) == MessageFlags.AcknowledgeRequest)
        {
            MessageFlags flags = Overhead.Flags & ~MessageFlags.AcknowledgeRequest;
            if (Overhead.RequestKey != 0)
                flags |= MessageFlags.AcknowledgeResponse;
            if (Overhead.RequestKey != 0)
            {
                MessageOverhead overhead = new MessageOverhead(flags, Overhead.MessageId, sizeof(int), Overhead.RequestKey);
                byte[] ttl = new byte[overhead.Length + sizeof(int)];

                fixed (byte* ttlPtr = ttl)
                {
                    overhead.GetBytes(ttlPtr, out _);
                    UnsafeBitConverter.GetBytes(ttlPtr, errorCode, overhead.Length);
                }

                Connection.Send(ttl);
                return true;
            }
        }
        return false;
    }
    private static NotSupportedException NilError() => new NotSupportedException("This message context instance is not linked to a connection. Perhaps it is Nil or was not initialized properly.");
    public void Reply(NetCall call)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, false, false);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
            );
    }
    public void Reply(NetCallCustom call, NetCallCustom.WriterTask task)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, false, false);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
            , task);
    }
    public void Reply<T1>(NetCallRaw<T1> call, T1 arg1)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, false, false);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
            , arg1);
    }
    public void Reply<T1, T2>(NetCallRaw<T1, T2> call, T1 arg1, T2 arg2)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, false, false);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
            , arg1, arg2);
    }
    public void Reply<T1, T2, T3>(NetCallRaw<T1, T2, T3> call, T1 arg1, T2 arg2, T3 arg3)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, false, false);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
            , arg1, arg2, arg3);
    }
    public void Reply<T1, T2, T3, T4>(NetCallRaw<T1, T2, T3, T4> call, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, false, false);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
            , arg1, arg2, arg3, arg4);
    }
    public void Reply<T1, T2, T3, T4, T5>(NetCallRaw<T1, T2, T3, T4, T5> call, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, false, false);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
            , arg1, arg2, arg3, arg4, arg5);
    }
    public void Reply<T1, T2, T3, T4, T5, T6>(NetCallRaw<T1, T2, T3, T4, T5, T6> call, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, false, false);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
            , arg1, arg2, arg3, arg4, arg5, arg6);
    }
    public void Reply<T1>(NetCall<T1> call, T1 arg1)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, false, false);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
            , arg1);
    }
    public void Reply<T1, T2>(NetCall<T1, T2> call, T1 arg1, T2 arg2)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, false, false);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
            , arg1, arg2);
    }
    public void Reply<T1, T2, T3>(NetCall<T1, T2, T3> call, T1 arg1, T2 arg2, T3 arg3)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, false, false);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
            , arg1, arg2, arg3);
    }
    public void Reply<T1, T2, T3, T4>(NetCall<T1, T2, T3, T4> call, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, false, false);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
            , arg1, arg2, arg3, arg4);
    }
    public void Reply<T1, T2, T3, T4, T5>(NetCall<T1, T2, T3, T4, T5> call, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, false, false);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
            , arg1, arg2, arg3, arg4, arg5);
    }
    public void Reply<T1, T2, T3, T4, T5, T6>(NetCall<T1, T2, T3, T4, T5, T6> call, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, false, false);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
            , arg1, arg2, arg3, arg4, arg5, arg6);
    }
    public void Reply<T1, T2, T3, T4, T5, T6, T7>(NetCall<T1, T2, T3, T4, T5, T6, T7> call, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, false, false);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
            , arg1, arg2, arg3, arg4, arg5, arg6, arg7);
    }
    public void Reply<T1, T2, T3, T4, T5, T6, T7, T8>(NetCall<T1, T2, T3, T4, T5, T6, T7, T8> call, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, false, false);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
            , arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
    }
    public void Reply<T1, T2, T3, T4, T5, T6, T7, T8, T9>(NetCall<T1, T2, T3, T4, T5, T6, T7, T8, T9> call, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, false, false);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
            , arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);
    }
    public void Reply<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(NetCall<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> call, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, false, false);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
            , arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10);
    }
    public void ReplyLayered(NetCall call)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, true, false);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
        );
    }
    public void ReplyLayered(NetCallCustom call, NetCallCustom.WriterTask task)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, true, false);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
            , task);
    }
    public void ReplyLayered<T1>(NetCallRaw<T1> call, T1 arg1)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, true, false);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
            , arg1);
    }
    public void ReplyLayered<T1, T2>(NetCallRaw<T1, T2> call, T1 arg1, T2 arg2)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, true, false);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
            , arg1, arg2);
    }
    public void ReplyLayered<T1, T2, T3>(NetCallRaw<T1, T2, T3> call, T1 arg1, T2 arg2, T3 arg3)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, true, false);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
            , arg1, arg2, arg3);
    }
    public void ReplyLayered<T1, T2, T3, T4>(NetCallRaw<T1, T2, T3, T4> call, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, true, false);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
            , arg1, arg2, arg3, arg4);
    }
    public void ReplyLayered<T1, T2, T3, T4, T5>(NetCallRaw<T1, T2, T3, T4, T5> call, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, true, false);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
            , arg1, arg2, arg3, arg4, arg5);
    }
    public void ReplyLayered<T1, T2, T3, T4, T5, T6>(NetCallRaw<T1, T2, T3, T4, T5, T6> call, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, true, false);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
            , arg1, arg2, arg3, arg4, arg5, arg6);
    }
    public void ReplyLayered<T1>(NetCall<T1> call, T1 arg1)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, true, false);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
            , arg1);
    }
    public void ReplyLayered<T1, T2>(NetCall<T1, T2> call, T1 arg1, T2 arg2)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, true, false);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
            , arg1, arg2);
    }
    public void ReplyLayered<T1, T2, T3>(NetCall<T1, T2, T3> call, T1 arg1, T2 arg2, T3 arg3)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, true, false);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
            , arg1, arg2, arg3);
    }
    public void ReplyLayered<T1, T2, T3, T4>(NetCall<T1, T2, T3, T4> call, T1 arg1, T2 arg2, T3 arg3, T4 arg4)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, true, false);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
            , arg1, arg2, arg3, arg4);
    }
    public void ReplyLayered<T1, T2, T3, T4, T5>(NetCall<T1, T2, T3, T4, T5> call, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, true, false);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
            , arg1, arg2, arg3, arg4, arg5);
    }
    public void ReplyLayered<T1, T2, T3, T4, T5, T6>(NetCall<T1, T2, T3, T4, T5, T6> call, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, true, false);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
            , arg1, arg2, arg3, arg4, arg5, arg6);
    }
    public void ReplyLayered<T1, T2, T3, T4, T5, T6, T7>(NetCall<T1, T2, T3, T4, T5, T6, T7> call, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, true, false);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
            , arg1, arg2, arg3, arg4, arg5, arg6, arg7);
    }
    public void ReplyLayered<T1, T2, T3, T4, T5, T6, T7, T8>(NetCall<T1, T2, T3, T4, T5, T6, T7, T8> call, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, true, false);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
            , arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
    }
    public void ReplyLayered<T1, T2, T3, T4, T5, T6, T7, T8, T9>(NetCall<T1, T2, T3, T4, T5, T6, T7, T8, T9> call, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, true, false);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
            , arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);
    }
    public void ReplyLayered<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(NetCall<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> call, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, true, false);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
            , arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10);
    }
    public NetTask ReplyAndRequestAck(NetCall call, int timeoutMs = 5000)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, false, true);
        NetTask task = call.ListenAck(timeoutMs);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
        );
        return task;
    }
    public NetTask ReplyAndRequestAck(NetCallCustom call, NetCallCustom.WriterTask task, int timeoutMs = 5000)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, false, true);
        NetTask task2 = call.ListenAck(timeoutMs);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
            , task);
        return task2;
    }
    public NetTask ReplyAndRequestAck<T1>(NetCallRaw<T1> call, T1 arg1, int timeoutMs = 5000)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, false, true);
        NetTask task = call.ListenAck(timeoutMs);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
            , arg1);
        return task;
    }
    public NetTask ReplyAndRequestAck<T1, T2>(NetCallRaw<T1, T2> call, T1 arg1, T2 arg2, int timeoutMs = 5000)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, false, true);
        NetTask task = call.ListenAck(timeoutMs);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
            , arg1, arg2);
        return task;
    }
    public NetTask ReplyAndRequestAck<T1, T2, T3>(NetCallRaw<T1, T2, T3> call, T1 arg1, T2 arg2, T3 arg3, int timeoutMs = 5000)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, false, true);
        NetTask task = call.ListenAck(timeoutMs);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
            , arg1, arg2, arg3);
        return task;
    }
    public NetTask ReplyAndRequestAck<T1, T2, T3, T4>(NetCallRaw<T1, T2, T3, T4> call, T1 arg1, T2 arg2, T3 arg3, T4 arg4, int timeoutMs = 5000)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, false, true);
        NetTask task = call.ListenAck(timeoutMs);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
            , arg1, arg2, arg3, arg4);
        return task;
    }
    public NetTask ReplyAndRequestAck<T1, T2, T3, T4, T5>(NetCallRaw<T1, T2, T3, T4, T5> call, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, int timeoutMs = 5000)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, false, true);
        NetTask task = call.ListenAck(timeoutMs);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
            , arg1, arg2, arg3, arg4, arg5);
        return task;
    }
    public NetTask ReplyAndRequestAck<T1, T2, T3, T4, T5, T6>(NetCallRaw<T1, T2, T3, T4, T5, T6> call, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, int timeoutMs = 5000)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, false, true);
        NetTask task = call.ListenAck(timeoutMs);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
            , arg1, arg2, arg3, arg4, arg5, arg6);
        return task;
    }
    public NetTask ReplyAndRequestAck<T1>(NetCall<T1> call, T1 arg1, int timeoutMs = 5000)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, false, true);
        NetTask task = call.ListenAck(timeoutMs);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
            , arg1);
        return task;
    }
    public NetTask ReplyAndRequestAck<T1, T2>(NetCall<T1, T2> call, T1 arg1, T2 arg2, int timeoutMs = 5000)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, false, true);
        NetTask task = call.ListenAck(timeoutMs);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
            , arg1, arg2);
        return task;
    }
    public NetTask ReplyAndRequestAck<T1, T2, T3>(NetCall<T1, T2, T3> call, T1 arg1, T2 arg2, T3 arg3, int timeoutMs = 5000)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, false, true);
        NetTask task = call.ListenAck(timeoutMs);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
            , arg1, arg2, arg3);
        return task;
    }
    public NetTask ReplyAndRequestAck<T1, T2, T3, T4>(NetCall<T1, T2, T3, T4> call, T1 arg1, T2 arg2, T3 arg3, T4 arg4, int timeoutMs = 5000)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, false, true);
        NetTask task = call.ListenAck(timeoutMs);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
            , arg1, arg2, arg3, arg4);
        return task;
    }
    public NetTask ReplyAndRequestAck<T1, T2, T3, T4, T5>(NetCall<T1, T2, T3, T4, T5> call, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, int timeoutMs = 5000)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, false, true);
        NetTask task = call.ListenAck(timeoutMs);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
            , arg1, arg2, arg3, arg4, arg5);
        return task;
    }
    public NetTask ReplyAndRequestAck<T1, T2, T3, T4, T5, T6>(NetCall<T1, T2, T3, T4, T5, T6> call, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, int timeoutMs = 5000)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, false, true);
        NetTask task = call.ListenAck(timeoutMs);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
            , arg1, arg2, arg3, arg4, arg5, arg6);
        return task;
    }
    public NetTask ReplyAndRequestAck<T1, T2, T3, T4, T5, T6, T7>(NetCall<T1, T2, T3, T4, T5, T6, T7> call, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, int timeoutMs = 5000)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, false, true);
        NetTask task = call.ListenAck(timeoutMs);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
            , arg1, arg2, arg3, arg4, arg5, arg6, arg7);
        return task;
    }
    public NetTask ReplyAndRequestAck<T1, T2, T3, T4, T5, T6, T7, T8>(NetCall<T1, T2, T3, T4, T5, T6, T7, T8> call, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, int timeoutMs = 5000)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, false, true);
        NetTask task = call.ListenAck(timeoutMs);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
            , arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
        return task;
    }
    public NetTask ReplyAndRequestAck<T1, T2, T3, T4, T5, T6, T7, T8, T9>(NetCall<T1, T2, T3, T4, T5, T6, T7, T8, T9> call, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, int timeoutMs = 5000)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, false, true);
        NetTask task = call.ListenAck(timeoutMs);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
            , arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);
        return task;
    }
    public NetTask ReplyAndRequestAck<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(NetCall<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> call, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, int timeoutMs = 5000)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, false, true);
        NetTask task = call.ListenAck(timeoutMs);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
            , arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10);
        return task;
    }
    public NetTask ReplyLayeredAndRequestAck(NetCall call, int timeoutMs = 5000)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, true, true);
        NetTask task = call.ListenAck(timeoutMs);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
        );
        return task;
    }
    public NetTask ReplyLayeredAndRequestAck(NetCallCustom call, NetCallCustom.WriterTask task, int timeoutMs = 5000)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, true, true);
        NetTask task2 = call.ListenAck(timeoutMs);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
            , task);
        return task2;
    }
    public NetTask ReplyLayeredAndRequestAck<T1>(NetCallRaw<T1> call, T1 arg1, int timeoutMs = 5000)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, true, true);
        NetTask task = call.ListenAck(timeoutMs);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
            , arg1);
        return task;
    }
    public NetTask ReplyLayeredAndRequestAck<T1, T2>(NetCallRaw<T1, T2> call, T1 arg1, T2 arg2, int timeoutMs = 5000)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, true, true);
        NetTask task = call.ListenAck(timeoutMs);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
            , arg1, arg2);
        return task;
    }
    public NetTask ReplyLayeredAndRequestAck<T1, T2, T3>(NetCallRaw<T1, T2, T3> call, T1 arg1, T2 arg2, T3 arg3, int timeoutMs = 5000)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, true, true);
        NetTask task = call.ListenAck(timeoutMs);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
            , arg1, arg2, arg3);
        return task;
    }
    public NetTask ReplyLayeredAndRequestAck<T1, T2, T3, T4>(NetCallRaw<T1, T2, T3, T4> call, T1 arg1, T2 arg2, T3 arg3, T4 arg4, int timeoutMs = 5000)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, true, true);
        NetTask task = call.ListenAck(timeoutMs);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
            , arg1, arg2, arg3, arg4);
        return task;
    }
    public NetTask ReplyLayeredAndRequestAck<T1, T2, T3, T4, T5>(NetCallRaw<T1, T2, T3, T4, T5> call, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, int timeoutMs = 5000)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, true, true);
        NetTask task = call.ListenAck(timeoutMs);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
            , arg1, arg2, arg3, arg4, arg5);
        return task;
    }
    public NetTask ReplyLayeredAndRequestAck<T1, T2, T3, T4, T5, T6>(NetCallRaw<T1, T2, T3, T4, T5, T6> call, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, int timeoutMs = 5000)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, true, true);
        NetTask task = call.ListenAck(timeoutMs);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
            , arg1, arg2, arg3, arg4, arg5, arg6);
        return task;
    }
    public NetTask ReplyLayeredAndRequestAck<T1>(NetCall<T1> call, T1 arg1, int timeoutMs = 5000)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, true, true);
        NetTask task = call.ListenAck(timeoutMs);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
            , arg1);
        return task;
    }
    public NetTask ReplyLayeredAndRequestAck<T1, T2>(NetCall<T1, T2> call, T1 arg1, T2 arg2, int timeoutMs = 5000)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, true, true);
        NetTask task = call.ListenAck(timeoutMs);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
            , arg1, arg2);
        return task;
    }
    public NetTask ReplyLayeredAndRequestAck<T1, T2, T3>(NetCall<T1, T2, T3> call, T1 arg1, T2 arg2, T3 arg3, int timeoutMs = 5000)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, true, true);
        NetTask task = call.ListenAck(timeoutMs);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
            , arg1, arg2, arg3);
        return task;
    }
    public NetTask ReplyLayeredAndRequestAck<T1, T2, T3, T4>(NetCall<T1, T2, T3, T4> call, T1 arg1, T2 arg2, T3 arg3, T4 arg4, int timeoutMs = 5000)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, true, true);
        NetTask task = call.ListenAck(timeoutMs);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
            , arg1, arg2, arg3, arg4);
        return task;
    }
    public NetTask ReplyLayeredAndRequestAck<T1, T2, T3, T4, T5>(NetCall<T1, T2, T3, T4, T5> call, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, int timeoutMs = 5000)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, true, true);
        NetTask task = call.ListenAck(timeoutMs);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
            , arg1, arg2, arg3, arg4, arg5);
        return task;
    }
    public NetTask ReplyLayeredAndRequestAck<T1, T2, T3, T4, T5, T6>(NetCall<T1, T2, T3, T4, T5, T6> call, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, int timeoutMs = 5000)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, true, true);
        NetTask task = call.ListenAck(timeoutMs);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
            , arg1, arg2, arg3, arg4, arg5, arg6);
        return task;
    }
    public NetTask ReplyLayeredAndRequestAck<T1, T2, T3, T4, T5, T6, T7>(NetCall<T1, T2, T3, T4, T5, T6, T7> call, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, int timeoutMs = 5000)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, true, true);
        NetTask task = call.ListenAck(timeoutMs);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
            , arg1, arg2, arg3, arg4, arg5, arg6, arg7);
        return task;
    }
    public NetTask ReplyLayeredAndRequestAck<T1, T2, T3, T4, T5, T6, T7, T8>(NetCall<T1, T2, T3, T4, T5, T6, T7, T8> call, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, int timeoutMs = 5000)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, true, true);
        NetTask task = call.ListenAck(timeoutMs);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
            , arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
        return task;
    }
    public NetTask ReplyLayeredAndRequestAck<T1, T2, T3, T4, T5, T6, T7, T8, T9>(NetCall<T1, T2, T3, T4, T5, T6, T7, T8, T9> call, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, int timeoutMs = 5000)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, true, true);
        NetTask task = call.ListenAck(timeoutMs);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
            , arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);
        return task;
    }
    public NetTask ReplyLayeredAndRequestAck<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(NetCall<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10> call, T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8, T9 arg9, T10 arg10, int timeoutMs = 5000)
    {
        if (Connection is null) throw NilError();
        MessageOverhead overhead = GetReplyOverhead(call, true, true);
        NetTask task = call.ListenAck(timeoutMs);
        call.Invoke(ref overhead
#if SERVER
            , Connection
#endif
            , arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10);
        return task;
    }
}

/// <summary>
/// Standard error codes for net messages. Negative numbers can be used for special error codes.
/// </summary>
public enum StandardErrorCode
{
    Success = 0,
    GenericError = 1,
    NotSupported = 2,
    InvalidData = 3,
    NotFound = 4,
    ModuleNotLoaded = 5,
    AccessViolation = 6,
    NoPermissions = 7
}