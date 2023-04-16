using JetBrains.Annotations;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using SDG.Framework.Utilities;

namespace DevkitServer.Multiplayer.Networking;
public class HighSpeedConnection : ITransportConnection
#if CLIENT
    , IClientTransport
#endif
{
#if CLIENT
    public static HighSpeedConnection? Instance { get; private set; }
#endif
    private readonly ConcurrentQueue<byte[]> _messageQueue = new ConcurrentQueue<byte[]>();
    private readonly byte[] _buffer = new byte[HighSpeedNetFactory.BufferSize];
    private readonly NetworkBuffer _netBuffer;
    public event NetworkBufferProgressUpdate? BufferProgressUpdated;
    private bool _disposed;
    public bool Verified { get; internal set; }
    public Guid SteamToken { get; internal set; }
    public TcpClient Client { get; }
#if SERVER
    public HighSpeedServer Server { get; }
    public ITransportConnection SteamConnection { get; }
#else
    public IClientTransport SteamConnection { get; }
#endif
    public ulong Steam64 { get; }
    public HighSpeedConnection(TcpClient client,
#if SERVER
        ITransportConnection steamConnection, 
#endif
        ulong steam64
#if SERVER
        , HighSpeedServer server
#endif
        )
    {
        Client = client;
#if SERVER
        SteamConnection = steamConnection;
        Server = server;
#else
        SteamConnection = NetFactory.GetPlayerTransportConnection();
        if (Instance != null)
            Instance.Dispose();
        Instance = this;
#endif
        Steam64 = steam64;
        _netBuffer = new NetworkBuffer(MessageReady, HighSpeedNetFactory.BufferSize, this);
        _netBuffer.BufferProgressUpdated += BufferProgressUpdated;
        TimeUtility.updated += Update;
    }

    public void Dispose()
    {
        if (_disposed) return;
        TimeUtility.updated -= Update;
#if SERVER
        if (Server != null)
        {
            Server.Disconnect(this);
        }
#endif

#if CLIENT
        if (Instance == this)
            Instance = null;
#endif
        _netBuffer.BufferProgressUpdated -= BufferProgressUpdated;
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
                address = (uint)ip.Address.MapToIPv4().Address;
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

    [UsedImplicitly]
    private void Update()
    {
        while (_messageQueue.TryDequeue(out byte[] msg))
        {
            _netBuffer.Buffer = msg;
            _netBuffer.ProcessBuffer(msg.Length);
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
            ar.AsyncWaitHandle.Dispose();
            byte[] bytes = new byte[ct];
            Buffer.BlockCopy(_buffer, 0, bytes, 0, ct);
            Listen();
            _messageQueue.Enqueue(bytes);
        }
        catch (IOException ex)
        {
            NetworkExceptionCaught(ex);
        }
        catch (ObjectDisposedException ex)
        {
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
        MessageOverhead overhead = new MessageOverhead(payload);
        HighSpeedNetFactory.Receive(this, payload, in overhead);
        Logger.LogDebug("[HIGH SPEED CONNECTION] HS message received: " + overhead.Format() + ".");
    }

#if CLIENT
    void IClientTransport.Initialize(ClientTransportReady callback, ClientTransportFailure failureCallback) => throw new NotImplementedException();
    bool IClientTransport.Receive(byte[] buffer, out long size) => throw new NotImplementedException();
    void IClientTransport.TearDown() => CloseConnection();
#endif
}
