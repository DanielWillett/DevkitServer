using SDG.NetPak;

using DevkitServer.Multiplayer.Networking;
using DevkitServer.Plugins;

namespace DevkitServer.API.Multiplayer;

/// <summary>
/// Auto-registed singleton to receive messages from the other side.
/// </summary>
/// <remarks>All of these MUST be present on both client and server. This is a low-level feature not expected to be used very often, as net calls provide a much more stable alternative.</remarks>
public interface ICustomNetMessageListener
{
    /// <summary>
    /// The side on which this message is received. Can be <see cref="ConnectionSide.Both"/>, <see cref="ConnectionSide.Server"/>, or <see cref="ConnectionSide.Client"/>.
    /// </summary>
    ConnectionSide ReceivingSide { get; }

    /// <summary>
    /// Set by DevkitServer when your plugin loads. Represents the value of type <see cref="DevkitServerMessage"/> to use for this message.
    /// </summary>
    uint MessageIndex { get; set; }

    /// <summary>
    /// Set by DevkitServer when your plugin loads. The owning assembly of this <see cref="ICustomNetMessageListener"/>.
    /// </summary>
    PluginAssembly Assembly { get; set; }

    /// <summary>
    /// Invoked when the message is received from either a client or the connected server.
    /// </summary>
    /// <param name="reader"></param>
    void OnInvoked(
#if SERVER
        ITransportConnection connection,
#endif
        ArraySegment<byte> data)
    {
        
    }

    void OnInvokedListener(
#if SERVER
        ITransportConnection connection,
#endif
        NetPakReader reader)
    {
        ThreadUtil.assertIsGameThread();

        if (!reader.ReadUInt16(out ushort len))
        {
            Logger.LogWarning("Received invalid message, can't read length"
#if SERVER
                              + $" from {transportConnection.Format()}"
#endif
                              + "."
                , method: "Invoke " + GetType().Name);
            return;
        }
        if (!reader.ReadBytesPtr(len, out byte[] buffer, out int offset))
        {
            Logger.LogWarning($"Received invalid message, can't read bytes of length {len.Format()} B"
#if SERVER
                              + $" from {transportConnection.Format()}"
#endif
                              + $". Expected length <= {reader.RemainingSegmentLength.Format()}."
                , method: "Invoke " + GetType().Name);
            return;
        }

        try
        {
            OnInvoked(
#if SERVER
            connection,
#endif
                new ArraySegment<byte>(buffer, offset, len));
        }
        catch (Exception ex)
        {

        }
    }
}