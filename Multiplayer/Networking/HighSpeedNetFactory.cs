using DevkitServer.Configuration;
#if CLIENT
using System.Net;
using System.Net.Sockets;
using Action = System.Action;
#endif

namespace DevkitServer.Multiplayer.Networking;
public static class HighSpeedNetFactory
{
    public const int BufferSize = 8192;

    internal static readonly NetCall<Guid> HighSpeedVerify = new NetCall<Guid>((ushort)HighSpeedNetCall.Verify) { HighSpeed = true };
    internal static readonly NetCall HighSpeedVerifyConfirm = new NetCall((ushort)HighSpeedNetCall.VerifyConfirm) { HighSpeed = true };
    internal static readonly NetCall<Guid> SteamVerify = new NetCall<Guid>((ushort)NetCalls.SendSteamVerificationToken);
    internal static readonly NetCall<ushort> OpenHighSpeedClient = new NetCall<ushort>((ushort)NetCalls.OpenHighSpeedClient);
#if SERVER
    internal static void StartVerifying(HighSpeedConnection pending)
    {
        pending.SteamToken = Guid.NewGuid();
        Logger.LogDebug("Verifying high speed connection: " + pending.GetAddress().Format() + ".");
        HighSpeedVerify.Invoke(pending, pending.SteamToken);
    }

    [NetCall(NetCallSource.FromClient, (ushort)NetCalls.SendSteamVerificationToken)]
    private static void ReceiveSteamToken(MessageContext ctx, Guid received)
    {
        HighSpeedServer server = HighSpeedServer.Instance;
        if (server == null)
        {
            Logger.LogWarning("Received steam high-speed verify packet before server was initialized.");
            return;
        }

        server.ReceiveVerifyPacket(ctx.Connection, received);
        Logger.LogDebug("Received steam token from: " + ctx.Connection.GetAddress().Format() + ".");
    }

    public static NetTask TryCreate(ITransportConnection connection, out bool failedConfigCheck)
    {
        failedConfigCheck = true;
        if (DevkitServerConfig.Config.TcpSettings is not { EnableHighSpeedSupport: true })
            return NetTask.Completed;
        failedConfigCheck = false;

        return OpenHighSpeedClient.RequestAck(connection, DevkitServerConfig.Config.TcpSettings.HighSpeedPort);
    }
#endif
#if CLIENT
    private static readonly List<MessageContext> PendingConnects = new List<MessageContext>();
    [NetCall(NetCallSource.FromServer, (ushort)NetCalls.OpenHighSpeedClient)]
    private static void ReceiveOpenHighSpeedClient(MessageContext ctx, ushort port)
    {
        HighSpeedConnection? connection = HighSpeedConnection.Instance;
        if (connection == null)
        {
            PendingConnects.Add(ctx);
            BeginGetConnectionToServer(port);
        }
        else ctx.Acknowledge(StandardErrorCode.Success);
    }

    [NetCall(NetCallSource.FromServer, (ushort)HighSpeedNetCall.Verify, HighSpeed = true, HighSpeedAllowUnverified = true)]
    private static void ReceiveSteamToken(MessageContext ctx, Guid received)
    {
        if (ctx.Connection is not HighSpeedConnection conn)
        {
            Logger.LogWarning("Received steam token on non-high-speed connection: " + ctx.Connection.GetType().Format() + ".");
            return;
        }
        
        conn.SteamToken = received;
        Logger.LogDebug("Send steam token for high-speed connection.");
        SteamVerify.Invoke(received);
    }
    [NetCall(NetCallSource.FromServer, (ushort)HighSpeedNetCall.VerifyConfirm, HighSpeed = true, HighSpeedAllowUnverified = true)]
    private static void ReceiveVerified(MessageContext ctx)
    {
        if (ctx.Connection is not HighSpeedConnection conn)
        {
            Logger.LogWarning("Received steam token on non-high-speed connection: " + ctx.Connection.GetType().Format() + ".");
            return;
        }

        conn.Verified = true;
        Logger.LogDebug("Verified high-speed connection.");
        DevkitServerUtility.QueueOnMainThread(() =>
        {
            for (int i = 0; i < PendingConnects.Count; i++)
                PendingConnects[i].Acknowledge(StandardErrorCode.Success);

            PendingConnects.Clear();
        });
    }

    public static void BeginGetConnectionToServer(ushort port)
    {
        IPAddress ip = new IPAddress(DevkitServerUtility.ReverseUInt32(Provider.currentServerInfo.ip));
        Logger.LogInfo("Connecting to server at " + ip.Format() + ":" + port.Format() + ".");
        TcpClient client = new TcpClient();
        try
        {
            client.BeginConnect(ip, port, EndConnect, client);
            return;
        }
        catch (SocketException ex)
        {
            Logger.LogInfo("Unable to connect to server: ");
            Logger.LogError(ex);
        }
        catch (Exception ex)
        {
            Logger.LogError("Error connecting: ");
            Logger.LogError(ex);
        }
        
        for (int i = 0; i < PendingConnects.Count; i++)
            PendingConnects[i].Acknowledge(StandardErrorCode.GenericError);

        PendingConnects.Clear();
    }
    private static void EndConnect(IAsyncResult ar)
    {
        if (ar.AsyncState is TcpClient client)
        {
            try
            {
                client.EndConnect(ar);
                Logger.LogInfo("Finished connecting to " + client.Client.RemoteEndPoint.Format() + ".");
                _ = new HighSpeedConnection(client, Provider.client.m_SteamID);
            }
            catch (SocketException ex)
            {
                Logger.LogInfo("Unable to connect to server: ");
                Logger.LogError(ex);
            }
            catch (Exception ex)
            {
                Logger.LogError("Error connecting: ");
                Logger.LogError(ex);
            }
        }

        DevkitServerUtility.QueueOnMainThread(() =>
        {
            for (int i = 0; i < PendingConnects.Count; i++)
                PendingConnects[i].Acknowledge(StandardErrorCode.GenericError);

            PendingConnects.Clear();
        });
    }

#endif
    internal static void Receive(HighSpeedConnection connection, byte[] bytes, in MessageOverhead overhead)
    {
        NetFactory.OnReceived(bytes, connection, overhead, true);
    }
}

public enum HighSpeedNetCall : ushort
{
    Verify = 1,
    VerifyConfirm = 2,
    SendWholeLevel = 3
}