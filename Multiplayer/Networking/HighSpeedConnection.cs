using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Unturned.SystemEx;

namespace DevkitServer.Multiplayer.Networking;
public class HighSpeedConnection : ITransportConnection
#if CLIENT
    , IClientTransport
#endif
{
#if CLIENT
    public static HighSpeedConnection? Instance => _instance;
    private static HighSpeedConnection? _instance;
#endif
    private readonly byte[] _buffer = new byte[HighSpeedNetFactory.BufferSize];
    private readonly NetworkBuffer _netBuffer;
    public event NetworkBufferProgressUpdate? BufferProgressUpdated;
    private int _disposed;
    public bool Verified { get; internal set; }
    public Guid SteamToken { get; internal set; }
    public TcpClient Client { get; }
#if SERVER
    internal int IntlTakeCounter;
    public int TakeCounter => IntlTakeCounter;
    public HighSpeedServer Server { get; }
    public ITransportConnection? SteamConnection { get; private set; }
    public List<ITransportConnection>? SteamConnectionCandidates { get; private set; }
#else
    public IClientTransport SteamConnection { get; }
#endif
    public ulong Steam64 { get; private set; }
    public HighSpeedConnection(TcpClient client
#if SERVER
        , List<ITransportConnection> steamConnections
#endif
#if CLIENT
        , ulong steam64
#endif
#if SERVER
        , HighSpeedServer server
#endif
        )
    {
        Client = client;
#if SERVER
        if (steamConnections.Count == 1)
        {
            SteamConnection = steamConnections[0];
            Steam64 = Provider.findTransportConnectionSteamId(SteamConnection).m_SteamID;
        }
        else
            SteamConnectionCandidates = steamConnections;
        Server = server;
#else
        SteamConnection = NetFactory.GetPlayerTransportConnection();
        HighSpeedConnection? old = Interlocked.Exchange(ref _instance, this);
        old?.Dispose();
        Steam64 = steam64;
#endif
        _netBuffer = new NetworkBuffer(MessageReady, this, _buffer);
        _netBuffer.BufferProgressUpdated += OnBufferProgressUpdatedIntl;
    }

    private void OnBufferProgressUpdatedIntl(long bytesDownloaded, long totalBytes) => BufferProgressUpdated?.Invoke(bytesDownloaded, totalBytes);

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        Logger.DevkitServer.LogDebug("HIGH SPEED CONNECTION", "Closing high-speed connection.");
#if SERVER
        if (Server != null)
            Server.Disconnect(this);
#endif

#if CLIENT
        Interlocked.CompareExchange(ref _instance, null, this);
#endif
        _netBuffer.BufferProgressUpdated -= OnBufferProgressUpdatedIntl;
        _netBuffer.Buffer = Array.Empty<byte>();
        Client.Dispose();
    }

    public bool Equals(ITransportConnection? other) => ReferenceEquals(other, this);
    public bool TryGetIPv4Address(out uint address)
    {
        if (Client.Client.RemoteEndPoint is IPEndPoint ip)
        {
            try
            {
#pragma warning disable CS0618
                address = DevkitServerUtility.ReverseUInt32((uint)ip.Address.MapToIPv4().Address);
#pragma warning restore CS0618
                return true;
            }
            catch (SocketException)
            {
                address = 0;
                return false;
            }
        }

        address = 0;
        return false;
    }
    public bool TryGetPort(out ushort port)
    {
        switch (Client.Client.RemoteEndPoint)
        {
            case IPEndPoint ip:
                port = (ushort)ip.Port;
                return true;
            case DnsEndPoint dns:
                port = (ushort)dns.Port;
                return true;
        }

        port = 0;
        return false;
    }

    public bool TryGetSteamId(out ulong steamId)
    {
        steamId = Steam64;
        return true;
    }

    public IPAddress GetAddress() => (Client.Client.RemoteEndPoint as IPEndPoint)?.Address ?? IPAddress.None;
    public string GetAddressString(bool withPort)
    {
        if (Client.Client.RemoteEndPoint is IPEndPoint ip)
        {
            IPAddress addr = ip.Address.MapToIPv4();
            return addr + (withPort ? (":" + ip.Port.ToString(CultureInfo.InvariantCulture)) : string.Empty);
        }
        if (Client.Client.RemoteEndPoint is DnsEndPoint dns)
            return dns.AddressFamily + "/" + dns.Host + (withPort ? (":" + dns.Port.ToString(CultureInfo.InvariantCulture)) : string.Empty);

        return Client.Client.RemoteEndPoint.ToString();
    }
    public void CloseConnection() => Dispose();

    public void Send(byte[] buffer, long size, ENetReliability reliability)
    {
        if (size > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(size));

        NetworkStream str = Client.GetStream();
        try
        {
#if DEBUG
            MessageOverhead ovh = new MessageOverhead(buffer);
            Logger.DevkitServer.LogDebug("HIGH SPEED CONNECTION", "Sending message: " + ovh.Format() + ".");
#endif
            str.BeginWrite(buffer, 0, (int)size, EndWrite, str);
        }
        catch (IOException ex)
        {
            NetworkExceptionCaught(ex);
        }
        catch (ObjectDisposedException)
        {
            CloseConnection();
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("HIGH SPEED CONNECTION", ex, "Error reading in Send.");
            CloseConnection();
        }
    }
    
    private void EndWrite(IAsyncResult ar)
    {
        try
        {
            (ar.AsyncState as NetworkStream ?? Client.GetStream()).EndWrite(ar);
            ar.AsyncWaitHandle.Dispose();
        }
        catch (IOException ex)
        {
            NetworkExceptionCaught(ex);
        }
        catch (ObjectDisposedException)
        {
            CloseConnection();
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("HIGH SPEED CONNECTION", ex, "Error reading in EndWrite.");
            CloseConnection();
        }
    }

    internal void Listen()
    {
        NetworkStream str = Client.GetStream();
        try
        {
            str.BeginRead(_buffer, 0, HighSpeedNetFactory.BufferSize, EndRead, str);
        }
        catch (IOException ex)
        {
            NetworkExceptionCaught(ex);
        }
        catch (ObjectDisposedException ex)
        {
            Logger.DevkitServer.LogError("HIGH SPEED CONNECTION", ex, "ObjectDisposedException in Listen.");
            CloseConnection();
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("HIGH SPEED CONNECTION", ex, "Error reading in Listen.");
            CloseConnection();
        }
    }
    private void EndRead(IAsyncResult ar)
    {
        try
        {
            int ct = (ar.AsyncState as NetworkStream ?? Client.GetStream()).EndRead(ar);
            if (ct < 1)
            {
                CloseConnection();
                return;
            }
            ar.AsyncWaitHandle.Dispose();
            _netBuffer.ProcessBuffer(ct);
            Listen();
        }
        catch (IOException ex)
        {
            NetworkExceptionCaught(ex);
        }
        catch (ObjectDisposedException)
        {
            CloseConnection();
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("HIGH SPEED CONNECTION", ex, "Error reading in EndRead.");
            CloseConnection();
        }
    }
    private void NetworkExceptionCaught(Exception ex)
    {
        if (ex.Message.IndexOf("forcibly closed", StringComparison.Ordinal) != -1 || ex.Message.IndexOf("WSACancelBlockingCall", StringComparison.Ordinal) != -1)
        {
            CloseConnection();
            return;
        }

        Logger.DevkitServer.LogError("HIGH SPEED CONNECTION", ex, ex is SocketException ? "Socket error:" : "IO error:");
        CloseConnection();
    }
    private void MessageReady(ArraySegment<byte> payload)
    {
        MessageOverhead overhead = new MessageOverhead(payload);
        Logger.DevkitServer.LogDebug("HIGH SPEED CONNECTION", "HS message received: " + overhead.Format() + ".");
        HighSpeedNetFactory.Receive(this, payload, in overhead);
    }
#if SERVER
    internal void Verify(ITransportConnection connection, ulong steam64)
    {
        Verified = true;
        SteamConnection = connection;
        SteamConnectionCandidates = null;
        Steam64 = steam64;
    }
#endif

#if CLIENT
    void IClientTransport.Initialize(ClientTransportReady callback, ClientTransportFailure failureCallback) => throw new NotImplementedException();
    bool IClientTransport.Receive(byte[] buffer, out long size) => throw new NotImplementedException();
    void IClientTransport.TearDown() => CloseConnection();
    public bool TryGetIPv4Address(out IPv4Address address)
    {
        if (Client.Client.RemoteEndPoint is IPEndPoint endPoint)
        {
            address = DevkitServerUtility.PackToIPv4(endPoint.Address);
            return true;
        }

        address = default;
        return false;
    }
    public bool TryGetConnectionPort(out ushort connectionPort)
    {
        if (Client.Client.RemoteEndPoint is IPEndPoint endPoint)
        {
            connectionPort = (ushort)endPoint.Port;
            return true;
        }

        connectionPort = default;
        return false;
    }
    public bool TryGetQueryPort(out ushort queryPort) => TryGetConnectionPort(out queryPort);

    public bool TryGetPing(out int pingMs)
    {
        pingMs = 0;
        return false;
    }
#endif
}
