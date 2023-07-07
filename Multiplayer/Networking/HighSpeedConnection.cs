using JetBrains.Annotations;
using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace DevkitServer.Multiplayer.Networking;
public class HighSpeedConnection : ITransportConnection
#if CLIENT
    , IClientTransport
#endif
{
#if CLIENT
    public static HighSpeedConnection? Instance => _instance;
#endif
    private readonly byte[] _buffer = new byte[HighSpeedNetFactory.BufferSize];
    private readonly NetworkBuffer _netBuffer;
    public event NetworkBufferProgressUpdate? BufferProgressUpdated;
    private bool _disposed;
    private static HighSpeedConnection? _instance;
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

    private void OnBufferProgressUpdatedIntl(int bytesDownloaded, int totalBytes) => BufferProgressUpdated?.Invoke(bytesDownloaded, totalBytes);

    public void Dispose()
    {
        if (_disposed) return;
        Logger.LogDebug("[HIGH SPEED CONNECTION] Closing high-speed connection.");
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
        _disposed = true;
    }

    public bool Equals(ITransportConnection other) => ReferenceEquals(other, this);
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
            Logger.LogDebug("Sending message: " + ovh.Format() + ".");
#endif
            str.BeginWrite(buffer, 0, (int)size, EndWrite, str);
        }
        catch (IOException ex)
        {
            NetworkExceptionCaught(ex);
        }
        catch (ObjectDisposedException ex)
        {
            Logger.LogError("[HIGH SPEED CONNECTION] ObjectDisposedException in Send.");
            Logger.LogError(ex);
            CloseConnection();
        }
        catch (Exception ex)
        {
            Logger.LogError("[HIGH SPEED CONNECTION] Error reading in Send.");
            Logger.LogError(ex);
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
        catch (ObjectDisposedException ex)
        {
            if (_disposed)
                return;
            Logger.LogError("[HIGH SPEED CONNECTION] ObjectDisposedException in EndWrite.");
            Logger.LogError(ex);
            CloseConnection();
        }
        catch (Exception ex)
        {
            Logger.LogError("[HIGH SPEED CONNECTION] Error reading in EndWrite.");
            Logger.LogError(ex);
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
            Logger.LogError("[HIGH SPEED CONNECTION] ObjectDisposedException in Listen.");
            Logger.LogError(ex);
            CloseConnection();
        }
        catch (Exception ex)
        {
            Logger.LogError("[HIGH SPEED CONNECTION] Error reading in Listen.");
            Logger.LogError(ex);
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
        catch (ObjectDisposedException ex)
        {
            if (_disposed)
                return;
            Logger.LogError("[HIGH SPEED CONNECTION] ObjectDisposedException in EndRead.");
            Logger.LogError(ex);
            CloseConnection();
        }
        catch (Exception ex)
        {
            Logger.LogError("[HIGH SPEED CONNECTION] Error reading in EndRead.");
            Logger.LogError(ex);
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
        if (ex is SocketException)
            Logger.LogError("[HIGH SPEED CONNECTION] Socket error:");
        else
            Logger.LogError("[HIGH SPEED CONNECTION] IO error:");
        Logger.LogError(ex);
        CloseConnection();
    }
    private void MessageReady(byte[] payload)
    {
        DevkitServerUtility.QueueOnMainThread(() =>
        {
            MessageOverhead overhead = new MessageOverhead(payload);
            Logger.LogDebug("[HIGH SPEED CONNECTION] HS message received: " + overhead.Format() + ".");
            HighSpeedNetFactory.Receive(this, payload, in overhead);
        });
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
#endif
}
