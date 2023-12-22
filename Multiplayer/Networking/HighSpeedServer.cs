#if SERVER
using DevkitServer.Configuration;
using System.Net;
using System.Net.Sockets;

namespace DevkitServer.Multiplayer.Networking;
public class HighSpeedServer : IDisposable
{
    private bool _disposed;
    private static HighSpeedServer? _instance;
    private readonly List<HighSpeedConnection> _connections = new List<HighSpeedConnection>(8);
    private readonly List<HighSpeedConnection> _pending = new List<HighSpeedConnection>();
    private readonly TcpListener _listener;
    public IReadOnlyList<HighSpeedConnection> VerifiedConnections { get; }
    public IReadOnlyList<HighSpeedConnection> PendingConnections { get; }
    public TcpListener Listener => _listener;
    public static HighSpeedServer Instance
    {
        get
        {
            if (_instance == null || _instance._disposed)
                _instance = new HighSpeedServer();

            return _instance;
        }
    }
    internal static void Deinit()
    {
        if (_instance != null)
            _instance.Dispose();
    }
    private HighSpeedServer()
    {
        if (DevkitServerConfig.Config.TcpSettings == null)
            throw new Exception("High-speed settings not initialized to a value.");
        _listener = new TcpListener(IPAddress.Any, DevkitServerConfig.Config.TcpSettings.HighSpeedPort);
        _listener.Start(24);
        VerifiedConnections = _connections.AsReadOnly();
        PendingConnections = _pending.AsReadOnly();
        _listener.BeginAcceptTcpClient(EndAccept, _listener);
        Logger.DevkitServer.LogInfo("HIGH SPEED SERVER", "Listening for connections on " + SteamGameServer.GetPublicIP().ToIPAddress().Format() + ":" + DevkitServerConfig.Config.TcpSettings.HighSpeedPort.Format() + ".");
    }
    private void EndAccept(IAsyncResult ar)
    {
        try
        {
            TcpClient client = (ar.AsyncState as TcpListener ?? _listener).EndAcceptTcpClient(ar);
            if (client.Client.RemoteEndPoint is not IPEndPoint ep)
            {
                Logger.DevkitServer.LogWarning("HIGH SPEED SERVER", "Client connecting with unsupported end point type: " +
                                                                    client.Client.RemoteEndPoint.Format() + ".");
                Reject(client);
                return;
            }
            
            if (!DevkitServerUtility.TryGetConnections(ep.Address, out List<ITransportConnection> results))
            {
                Logger.DevkitServer.LogWarning("HIGH SPEED SERVER", "Unknown client connecting from " + ep.Format() + ".");
                Reject(client);
                return;
            }

            client.SendBufferSize = HighSpeedNetFactory.BufferSize;
            client.ReceiveBufferSize = HighSpeedNetFactory.BufferSize;

            Logger.DevkitServer.LogInfo("HIGH SPEED SERVER", "Client connecting to high-speed server: " +
                                                             string.Join(" or ", results.Select(x => x.Format())));
            HighSpeedConnection hsConn = new HighSpeedConnection(client, results, this);
            hsConn.Listen();
            _pending.Add(hsConn);
            DevkitServerUtility.QueueOnMainThread(() => HighSpeedNetFactory.StartVerifying(hsConn));
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("HIGH SPEED SERVER", ex, "Error accepting high-speed client.");
        }
        finally
        {
            _listener.BeginAcceptTcpClient(EndAccept, _listener);
        }
    }
    internal void ReceiveVerifyPacket(ITransportConnection connection, Guid sent, ulong steam64)
    {
        for (int i = 0; i < _pending.Count; ++i)
        {
            HighSpeedConnection hs = _pending[i];
            if (hs.SteamToken == sent && !hs.Verified)
            {
                hs.Verify(connection, steam64);

                _pending.RemoveAtFast(i);
                _connections.Add(hs);
                Logger.DevkitServer.LogDebug("HIGH SPEED SERVER", "High-speed connection established to: " + hs.Steam64.Format() + " (" + hs.Format() + ").");
                HighSpeedNetFactory.HighSpeedVerifyConfirm.Invoke(hs);
                return;
            }
        }
        Logger.DevkitServer.LogWarning("HIGH SPEED SERVER", "High-speed connection not verified, couldn't match steam connection (" + connection.GetAddress().Format() + ") to a high speed connection.");
    }
    internal void Disconnect(HighSpeedConnection connection)
    {
        _pending.Remove(connection);
        _connections.Remove(connection);
        Logger.DevkitServer.LogDebug("HIGH SPEED SERVER", "Client disconnected: " + connection.GetAddress().Format() + ".");
    }
    internal static void Disconnect(CSteamID player)
    {
        if (_instance != null)
        {
            foreach (HighSpeedConnection connection in _instance._connections
                         .Concat(_instance._pending)
                         .Where(x => x.Steam64 == player.m_SteamID || x.Verified && (x.SteamConnection == null || x.SteamConnection.GetAddress() == null)))
            {
                connection.CloseConnection();
            }
        }
    }
    private static void Reject(TcpClient client)
    {
        Logger.DevkitServer.LogDebug("HIGH SPEED SERVER", "Client rejected: " + client.Client.RemoteEndPoint.Format() + ".");
        client.Dispose();
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}

#endif