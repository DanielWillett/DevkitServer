using DevkitServer.Multiplayer.Networking;
using System.Collections.Concurrent;
using DanielWillett.SpeedBytes;
#if SERVER
using DevkitServer.Multiplayer.Levels;
#endif

namespace DevkitServer.API.Multiplayer;

/// <summary>
/// Extension methods for <see cref="ICustomNetMessageListener"/>.
/// </summary>
public static class CustomNetMessageListeners
{
    private static readonly NetCallCustom SendMappings = new NetCallCustom(ReceiveRemoteMappings);

#if SERVER
    /// <summary>
    /// Stores mappings between custom net message implementations of clients and their respective message indexes.
    /// </summary>
    internal static readonly ConcurrentDictionary<ITransportConnection, ConcurrentDictionary<Type, DevkitServerMessage>> RemoteMappings =
        new ConcurrentDictionary<ITransportConnection, ConcurrentDictionary<Type, DevkitServerMessage>>();
#elif CLIENT
    /// <summary>
    /// Stores mappings between the server's custom net message implementations and their respective message indexes.
    /// </summary>
    internal static readonly Dictionary<Type, DevkitServerMessage> RemoteMappings = new Dictionary<Type, DevkitServerMessage>();
#endif
    /// <summary>
    /// Stores mappings between custom net message implementations and their respective message indexes.
    /// </summary>
    internal static readonly ConcurrentDictionary<Type, DevkitServerMessage> LocalMappings = new ConcurrentDictionary<Type, DevkitServerMessage>();
    internal static readonly ConcurrentDictionary<DevkitServerMessage, Type> InverseLocalMappings = new ConcurrentDictionary<DevkitServerMessage, Type>();
    internal static bool AreLocalMappingsDirty = true;

#if CLIENT
    /// <summary>
    /// Invoke this message on the server.
    /// </summary>
    /// <param name="bytes">Raw data to send.</param>
    /// <param name="offset">What index to start reading from <paramref name="bytes"/> at.</param>
    /// <param name="length">Number of bytes to read from <paramref name="bytes"/>.</param>
    /// <param name="reliable">Should this message use the reliable steam networking buffer?</param>
    public static void Invoke(this ICustomNetMessageListener listener, byte[] bytes, int offset = 0, int length = -1, bool reliable = true)
    {
        if (listener == null)
            throw new ArgumentNullException(nameof(listener));

        if ((listener.ReceivingSide & ConnectionSide.Server) == 0)
            throw new ArgumentException($"{listener.GetType().Name} does not support sending from client build.");

        NetFactory.SendGeneric(listener.LocalMessageIndex, new ArraySegment<byte>(bytes, offset, length), reliable);
    }

#elif SERVER

    /// <summary>
    /// Invoke this message on a specific client (<paramref name="connection"/>).
    /// </summary>
    /// <param name="connection">User to send the data to.</param>
    /// <param name="bytes">Raw data to send.</param>
    /// <param name="reliable">Should this message use the reliable steam networking buffer?</param>
    /// <exception cref="ArgumentException">Length is too long (must be at most <see cref="ushort.MaxValue"/>) or a <see cref="HighSpeedConnection"/> is used with a <see cref="ICustomNetMessageListener"/>.</exception>
    public static void Invoke(this ICustomNetMessageListener listener, ArraySegment<byte> bytes, ITransportConnection connection, bool reliable = true)
    {
        if (listener == null)
            throw new ArgumentNullException(nameof(listener));

        if ((listener.ReceivingSide & ConnectionSide.Client) == 0)
            throw new ArgumentException($"{listener.GetType().Name} does not support sending from server build.");

        NetFactory.SendGeneric(listener.LocalMessageIndex, bytes, connection, reliable);
    }
    /// <summary>
    /// Invoke this message on multiple clients (<paramref name="connections"/>).
    /// </summary>
    /// <param name="connections">Users to send the data to, or <see langword="null"/> to select all users.</param>
    /// <param name="bytes">Raw data to send.</param>
    /// <param name="reliable">Should this message use the reliable steam networking buffer?</param>
    /// <exception cref="ArgumentException">Length is too long (must be at most <see cref="ushort.MaxValue"/>) or a <see cref="HighSpeedConnection"/> is used with a <see cref="ICustomNetMessageListener"/>.</exception>
    public static void Invoke(this ICustomNetMessageListener listener, ArraySegment<byte> bytes, IReadOnlyList<ITransportConnection>? connections = null, bool reliable = true)
    {
        if (listener == null)
            throw new ArgumentNullException(nameof(listener));

        if ((listener.ReceivingSide & ConnectionSide.Client) == 0)
            throw new ArgumentException($"{listener.GetType().Name} does not support sending from server build.");

        NetFactory.SendGeneric(listener.LocalMessageIndex, bytes, connections, reliable);
    }
#endif

    internal static void SendLocalMappings()
    {
        if (!AreLocalMappingsDirty)
        {
            Logger.DevkitServer.LogDebug(nameof(SendLocalMappings), "Skipping sending local mappings as they aren't dirty.");
            return;
        }

        AreLocalMappingsDirty = false;

#if CLIENT
        Logger.DevkitServer.LogDebug(nameof(SendLocalMappings), "Syncing updated local net message mappings to server.");
        SendMappings.Invoke(WriteLocalMappings);
#else
        IReadOnlyList<ITransportConnection> connections = EditorLevel.PendingToReceiveActions
            .Concat(Provider.clients.Select(x => x.transportConnection))
            .Concat(Provider.pending.Select(x => x.transportConnection))
            .Distinct()
            .ToList();

        if (connections.Count == 0)
            return;

        Logger.DevkitServer.LogDebug(nameof(SendLocalMappings), $"Syncing updated local net message mappings to {connections.Count.Format()} connection(s).");
        SendMappings.Invoke(connections, WriteLocalMappings);
#endif
    }
#if SERVER
    internal static void SendLocalMappings(ITransportConnection connection)
    {
        Logger.DevkitServer.LogDebug(nameof(SendLocalMappings), $"Syncing updated local net message mappings to connection {connection.Format()}.");
        SendMappings.Invoke(connection, WriteLocalMappings);
    }
#endif

    private static void WriteLocalMappings(ByteWriter writer)
    {
        const byte version = 0;

        writer.Write(version);
        KeyValuePair<Type, DevkitServerMessage>[] mappings = LocalMappings.ToArray();
        int len = Math.Min(ushort.MaxValue, mappings.Length);
        writer.Write((ushort)len);

        for (int i = 0; i < len; ++i)
        {
            writer.Write(mappings[i].Key);
            writer.Write((uint)mappings[i].Value);
        }
    }

    [NetCall(NetCallSource.FromEither, DevkitServerNetCall.SendRemoteNetMessageMappings)]
    private static void ReceiveRemoteMappings(MessageContext ctx, ByteReader reader)
    {
        _ = reader.ReadUInt8(); // version

        int len = reader.ReadUInt16();

#if CLIENT
        RemoteMappings.Clear();
#else
        RemoteMappings.AddOrUpdate(ctx.Connection,
            static _ => new ConcurrentDictionary<Type, DevkitServerMessage>(),
            static (_, old) =>
            {
                old.Clear();
                return old;
            });
#endif

        for (int i = 0; i < len; ++i)
        {
            Type? type = reader.ReadType();
            DevkitServerMessage message = (DevkitServerMessage)reader.ReadUInt32();
            if (type == null)
                continue;
#if CLIENT
            RemoteMappings[type] = message;
#else
            RemoteMappings[ctx.Connection][type] = message;
#endif
        }

#if CLIENT
        Logger.DevkitServer.LogDebug(nameof(ReceiveRemoteMappings), "Received server net message mappings.");
#elif SERVER
        Logger.DevkitServer.LogDebug(nameof(ReceiveRemoteMappings), $"Received client net message mappings from {ctx.Connection.Format()}.");

        // only resend before they're pending
        if (Provider.findPlayer(ctx.Connection) == null && Provider.findPendingPlayer(ctx.Connection) == null)
            SendLocalMappings(ctx.Connection);
#endif
    }
}
