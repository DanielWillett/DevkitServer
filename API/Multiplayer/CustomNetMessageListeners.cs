using DevkitServer.Multiplayer.Networking;

namespace DevkitServer.API.Multiplayer;

/// <summary>
/// Extension methods for <see cref="ICustomNetMessageListener"/>.
/// </summary>
public static class CustomNetMessageListeners
{
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

        if (!DevkitServerModule.IsEditing)
            throw new InvalidOperationException("Not currently connected to a server running DevkitServer.");

        NetFactory.SendGeneric((DevkitServerMessage)listener.MessageIndex, bytes, offset, length, reliable);
    }

#elif SERVER

    /// <summary>
    /// Invoke this message on a specific client (<paramref name="connection"/>).
    /// </summary>
    /// <param name="connection">User to send the data to.</param>
    /// <param name="bytes">Raw data to send.</param>
    /// <param name="offset">What index to start reading from <paramref name="bytes"/> at.</param>
    /// <param name="length">Number of bytes to read from <paramref name="bytes"/>.</param>
    /// <param name="reliable">Should this message use the reliable steam networking buffer?</param>
    public static void Invoke(this ICustomNetMessageListener listener, ITransportConnection connection, byte[] bytes, int offset = 0, int length = -1, bool reliable = true)
    {
        if (listener == null)
            throw new ArgumentNullException(nameof(listener));

        if ((listener.ReceivingSide & ConnectionSide.Client) == 0)
            throw new ArgumentException($"{listener.GetType().Name} does not support sending from server build.");

        if (!DevkitServerModule.IsEditing)
            throw new InvalidOperationException("Not currently using DevkitServer.");

        NetFactory.SendGeneric((DevkitServerMessage)listener.MessageIndex, connection, bytes, offset, length, reliable);
    }
    /// <summary>
    /// Invoke this message on multiple clients (<paramref name="connections"/>).
    /// </summary>
    /// <param name="connections">Users to send the data to, or <see langword="null"/> to select all users.</param>
    /// <param name="bytes">Raw data to send.</param>
    /// <param name="offset">What index to start reading from <paramref name="bytes"/> at.</param>
    /// <param name="length">Number of bytes to read from <paramref name="bytes"/>.</param>
    /// <param name="reliable">Should this message use the reliable steam networking buffer?</param>
    public static void Invoke(this ICustomNetMessageListener listener, byte[] bytes, IReadOnlyList<ITransportConnection>? connections = null, int offset = 0, int length = -1, bool reliable = true)
    {
        if (listener == null)
            throw new ArgumentNullException(nameof(listener));

        if ((listener.ReceivingSide & ConnectionSide.Client) == 0)
            throw new ArgumentException($"{listener.GetType().Name} does not support sending from server build.");

        if (!DevkitServerModule.IsEditing)
            throw new InvalidOperationException("Not currently using DevkitServer.");

        NetFactory.SendGeneric((DevkitServerMessage)listener.MessageIndex, bytes, connections, offset, length, reliable);
    }
#endif
}
