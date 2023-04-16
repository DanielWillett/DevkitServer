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
        _listener = new TcpListener(IPAddress.Loopback, DevkitServerConfig.Config.TcpSettings.HighSpeedPort);
        _listener.Start(24);
        VerifiedConnections = _connections.AsReadOnly();
        PendingConnections = _pending.AsReadOnly();
        _listener.BeginAcceptTcpClient(EndAccept, _listener);
        Logger.LogInfo("[HIGH SPEED SERVER] Listening for connections on " + SteamGameServer.GetPublicIP().ToIPAddress().Format() + ":" + DevkitServerConfig.Config.TcpSettings.HighSpeedPort.Format() + ".");
    }
    private void EndAccept(IAsyncResult ar)
    {
        try
        {
            TcpClient client = (ar.AsyncState as TcpListener ?? _listener).EndAcceptTcpClient(ar);
            if (client.Client.RemoteEndPoint is not IPEndPoint ep)
            {
                Logger.LogWarning("[HIGH SPEED SERVER] Client connecting with unsupported end point type: " +
                                  client.Client.RemoteEndPoint.Format() + ".");
                Reject(client);
                return;
            }

            ITransportConnection? connection = null;
            ulong steam64 = 0ul;
            for (int i = 0; i < Provider.clients.Count; ++i)
            {
                if (Provider.clients[i].transportConnection.GetAddress().Equals(ep.Address))
                {
                    connection = Provider.clients[i].transportConnection;
                    steam64 = Provider.clients[i].playerID.steamID.m_SteamID;
                    break;
                }
            }

            if (connection == null)
            {
                for (int i = 0; i < Provider.pending.Count; ++i)
                {
                    if (Provider.pending[i].transportConnection.GetAddress().Equals(ep.Address))
                    {
                        connection = Provider.pending[i].transportConnection;
                        steam64 = Provider.pending[i].playerID.steamID.m_SteamID;
                        break;
                    }
                }

                if (connection == null)
                {
                    Logger.LogWarning("[HIGH SPEED SERVER] Unknown client connecting from " + ep.Format() + ".");
                    Reject(client);
                    return;
                }
            }

            client.SendBufferSize = HighSpeedNetFactory.BufferSize;
            client.ReceiveBufferSize = HighSpeedNetFactory.BufferSize;

            Logger.LogInfo("[HIGH SPEED SERVER] Client connecting to high-speed server: {" + steam64.Format() + "}.");
            HighSpeedConnection hsConn = new HighSpeedConnection(client, connection, steam64, this);
            _pending.Add(hsConn);
            HighSpeedNetFactory.StartVerifying(hsConn);
        }
        catch (Exception ex)
        {
            Logger.LogError("[HIGH SPEED SERVER] Error accepting high-speed client.");
            Logger.LogError(ex);
        }
        finally
        {
            _listener.BeginAcceptTcpClient(EndAccept, _listener);
        }
    }
    internal void ReceiveVerifyPacket(ITransportConnection connection, Guid sent)
    {
        for (int i = 0; i < _pending.Count; ++i)
        {
            HighSpeedConnection hs = _pending[i];
            if (connection.Equals(hs.SteamConnection))
            {
                if (sent != hs.SteamToken)
                {
                    Logger.LogInfo("[HIGH SPEED SERVER] High-speed connection not verified: incorrect token. (Sent: " + sent.Format() + ", Expected: " + hs.SteamToken.Format() + ".");
                }
                hs.Verified = true;
                _pending.RemoveAtFast(i);
                _connections.Add(hs);
                Logger.LogDebug("[HIGH SPEED SERVER] High-speed connection established to: " + hs.Steam64.Format() + " (" + hs.GetAddressString(true) + ").");
                HighSpeedNetFactory.HighSpeedVerifyConfirm.Invoke(hs);
                return;
            }
        }
        Logger.LogWarning("[HIGH SPEED SERVER] High-speed connection not verified, couldn't match steam connection (" + connection.GetAddress().Format() + ") to a high speed connection.");
    }
    internal void Disconnect(HighSpeedConnection connection)
    {
        _pending.Remove(connection);
        _connections.Remove(connection);
        Logger.LogDebug("[HIGH SPEED SERVER] Client disconnected: " + connection.GetAddress().Format() + ".");
    }
    private static void Reject(TcpClient client)
    {
        Logger.LogDebug("[HIGH SPEED SERVER] Client rejected: " + client.Client.RemoteEndPoint.Format() + ".");
        client.Dispose();
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}

#endif