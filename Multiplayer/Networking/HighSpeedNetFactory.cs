#if SERVER
using Cysharp.Threading.Tasks;
using DevkitServer.Configuration;
#endif
#if CLIENT
using System.Net;
using System.Net.Sockets;
#endif

namespace DevkitServer.Multiplayer.Networking;

/// <summary>
/// Interface for creating and using high-speed (TCP) connections.
/// </summary>
public static class HighSpeedNetFactory
{
    internal const int BufferSize = 4194304; // 4 MiB

    internal static readonly NetCall<Guid> HighSpeedVerify = new NetCall<Guid>((ushort)HighSpeedNetCall.Verify, highSpeed: true);
    internal static readonly NetCall HighSpeedVerifyConfirm = new NetCall((ushort)HighSpeedNetCall.VerifyConfirm, highSpeed: true);
    internal static readonly NetCall<Guid, ulong> SteamVerify = new NetCall<Guid, ulong>((ushort)DevkitServerNetCall.SendSteamVerificationToken);
    internal static readonly NetCall<ushort> OpenHighSpeedClient = new NetCall<ushort>((ushort)DevkitServerNetCall.OpenHighSpeedClient);
    internal static readonly NetCall RequestHighSpeedServer = new NetCall((ushort)DevkitServerNetCall.RequestOpenHighSpeedConnection);
    internal static readonly NetCall<bool> RequestReleaseOrTakeHighSpeedServer = new NetCall<bool>((ushort)DevkitServerNetCall.RequestReleaseOrTakeHighSpeedConnection);
#if SERVER
    internal static void StartVerifying(HighSpeedConnection pending)
    {
        pending.SteamToken = Guid.NewGuid();
        Logger.DevkitServer.LogDebug("HIGH SPEED NETWORKING", "Verifying high speed connection: " + pending.GetAddress().Format() + ".");
        HighSpeedVerify.Invoke(pending, pending.SteamToken);
    }
    /// <summary>Call <see cref="ReleaseConnection"/> when you're done.</summary>
    /// <returns>Number of takes on the connection after taking one.</returns>
    public static int TakeConnection(HighSpeedConnection connection)
    {
        return Interlocked.Increment(ref connection.IntlTakeCounter);
    }
    /// <summary>Call <see cref="TakeConnection"/> before this. Closes the connection if there are no remaining takes.</summary>
    /// <returns>Number of takes on the connection after releasing one.</returns>
    public static int ReleaseConnection(HighSpeedConnection connection)
    {
        int c = Interlocked.Decrement(ref connection.IntlTakeCounter);
        if (c == 0)
            connection.CloseConnection();
        return c;
    }

    /// <summary>Coroutine compatible. Find or create a connection for <paramref name="connection"/>. If it doesn't exist it will be created, verified.
    /// Then if <paramref name="take"/> is <see langword="true"/>, <see cref="TakeConnection"/> will be ran on it.</summary>
    /// <exception cref="OperationCanceledException"/>
    /// <remarks>If you <paramref name="take"/> the connection or call <see cref="TakeConnection"/> on it, make sure you call <see cref="ReleaseConnection"/> when you're done.</remarks>
    public static TaskYieldInstruction TryGetOrCreateAndVerifyYield(ITransportConnection connection, bool take, CancellationToken token = default) =>
        new TaskYieldInstruction(TryGetOrCreateAndVerify(connection, take, token), token);

    /// <summary>Find or create a connection for <paramref name="connection"/>. If it doesn't exist it will be created, verified.
    /// Then if <paramref name="take"/> is <see langword="true"/>, <see cref="TakeConnection"/> will be ran on it.</summary>
    /// <exception cref="OperationCanceledException"/>
    /// <remarks>If you <paramref name="take"/> the connection or call <see cref="TakeConnection"/> on it, make sure you call <see cref="ReleaseConnection"/> when you're done.</remarks>
    public static async Task<HighSpeedConnection?> TryGetOrCreateAndVerify(ITransportConnection connection, bool take, CancellationToken token = default)
    {
        await UniTask.SwitchToMainThread(token);

        HighSpeedConnection? hsConn = FindHighSpeedConnection(connection);
        if (hsConn is not { Client.Connected: true })
            hsConn = null;

        if (hsConn == null)
        {
            NetTask hsTask = TryCreate(connection, out bool failure);
            if (!failure)
            {
                RequestResponse response = await hsTask;
                if (!response.Responded || response.ErrorCode is not (int)StandardErrorCode.Success)
                {
                    if (response.ErrorCode is (int)StandardErrorCode.NotSupported)
                    {
                        Logger.DevkitServer.LogInfo("HIGH SPEED NETWORKING", $"Can't create a high-speed connection for {connection.Format()} since they connected using a server code.");
                    }
                    else
                    {
                        Logger.DevkitServer.LogWarning("HIGH SPEED NETWORKING", $"Failed to create high-speed connection for {connection.Format()}.");
                    }
                    return null;
                }
            }
            else return null;


            hsConn = connection.FindHighSpeedConnection();
            if (hsConn == null)
            {
                Logger.DevkitServer.LogWarning("HIGH SPEED NETWORKING", $"Unable to find created high-speed connection for {connection.Format()}.");
                return null;
            }
        }
        
        if (!hsConn.Verified)
        {
            try
            {
                CancellationTokenSource tknSrc = CancellationTokenSource.CreateLinkedTokenSource(token, new CancellationTokenSource(5000).Token);
                while (!hsConn.Verified)
                {
                    await UniTask.NextFrame(PlayerLoopTiming.Update, cancellationToken: token);
                    tknSrc.Token.ThrowIfCancellationRequested();
                }
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested) // only for timeout token.
            {
                Logger.DevkitServer.LogWarning("HIGH SPEED NETWORKING", $"Timed out trying to verify connection (5 seconds) for {connection.Format()}.");
                return null;
            }
        }

        if (take)
            TakeConnection(hsConn);
        return hsConn;
    }

    /// <summary>Coroutine compatible. Find or create connections for <paramref name="connections"/>. If they don't exist they will be created, verified.
    /// Then if <paramref name="take"/> is <see langword="true"/>, <see cref="TakeConnection"/> will be ran on them.</summary>
    /// <exception cref="OperationCanceledException"/>
    /// <remarks>If you <paramref name="take"/> the connections or call <see cref="TakeConnection"/> on them, make sure you call <see cref="ReleaseConnection"/> when you're done.</remarks>
    public static TaskYieldInstruction TryGetOrCreateAndVerifyYield(IReadOnlyList<ITransportConnection> connections, bool take, CancellationToken token = default) =>
        new TaskYieldInstruction(TryGetOrCreateAndVerify(connections, take, token), token);

    /// <summary>Find or create connections for <paramref name="connections"/>. If they don't exist they will be created, verified.
    /// Then if <paramref name="take"/> is <see langword="true"/>, <see cref="TakeConnection"/> will be ran on them.</summary>
    /// <exception cref="OperationCanceledException"/>
    /// <remarks>If you <paramref name="take"/> the connections or call <see cref="TakeConnection"/> on them, make sure you call <see cref="ReleaseConnection"/> when you're done.</remarks>
    public static async Task<HighSpeedConnection?[]> TryGetOrCreateAndVerify(IReadOnlyList<ITransportConnection> connections, bool take, CancellationToken token = default)
    {
        if (connections.Count == 1)
            return [ await TryGetOrCreateAndVerify(connections[0], take, token) ];

        if (connections.Count == 0)
            return Array.Empty<HighSpeedConnection>();
        
        UniTask[] tasks = new UniTask[connections.Count];
        HighSpeedConnection?[] results = new HighSpeedConnection?[connections.Count];

        for (int i = 0; i < connections.Count; i++)
        {
            ITransportConnection connection = connections[i];
            int index = i;
            tasks[i] = UniTask.Create(async () =>
            {
                await UniTask.SwitchToMainThread(token);
                HighSpeedConnection? hsConn = FindHighSpeedConnection(connection);

                if (hsConn is not { Client.Connected: true })
                    hsConn = null;

                if (hsConn == null)
                {
                    NetTask hsTask = TryCreate(connection, out bool failure);
                    if (!failure)
                    {
                        RequestResponse response = await hsTask;
                        if (!response.Responded || response.ErrorCode is not (int)StandardErrorCode.Success)
                        {
                            if (response.ErrorCode is (int)StandardErrorCode.NotSupported)
                            {
                                Logger.DevkitServer.LogInfo("HIGH SPEED NETWORKING", $"Can't create a high-speed connection ({i.Format()}) for {connection.Format()} since they connected using a server code.");
                            }
                            else
                            {
                                Logger.DevkitServer.LogWarning("HIGH SPEED NETWORKING", $"Failed to create high-speed connection ({i.Format()}) for {connection.Format()}.");
                            }
                            return;
                        }
                    }
                    else return;


                    hsConn = connection.FindHighSpeedConnection();
                    if (hsConn == null)
                    {
                        Logger.DevkitServer.LogWarning("HIGH SPEED NETWORKING", $"Unable to find created high-speed connection ({i.Format()}) for {connection.Format()}.");
                        return;
                    }
                }

                if (!hsConn.Verified)
                {
                    try
                    {
                        CancellationTokenSource tknSrc = CancellationTokenSource.CreateLinkedTokenSource(token, new CancellationTokenSource(5000).Token);
                        while (!hsConn.Verified)
                        {
                            await UniTask.NextFrame(PlayerLoopTiming.Update, cancellationToken: token);
                            tknSrc.Token.ThrowIfCancellationRequested();
                        }
                    }
                    catch (OperationCanceledException) when (!token.IsCancellationRequested) // only for timeout token.
                    {
                        Logger.DevkitServer.LogWarning("HIGH SPEED NETWORKING", $"Timed out trying to verify connection (5 seconds) ({i.Format()}) for {connection.Format()}.");
                        return;
                    }
                }

                if (take)
                    TakeConnection(hsConn);

                results[index] = hsConn;
            });
        }

        await UniTask.WhenAll(tasks);
        return results;
    }
    public static HighSpeedConnection? FindHighSpeedConnection(this ITransportConnection connection)
    {
        HighSpeedConnection? hs = connection as HighSpeedConnection;
        bool Filter(HighSpeedConnection x) => x is { Client.Connected: true, SteamConnection: not null } && x.SteamConnection.Equals(connection);
        hs ??= HighSpeedServer.Instance.VerifiedConnections.FirstOrDefault(Filter) ?? HighSpeedServer.Instance.PendingConnections.FirstOrDefault(Filter);
        return hs;
    }
    public static HighSpeedConnection? FindHighSpeedConnection(ulong steam64)
    {
        if (!steam64.UserSteam64())
            return null;
        bool Filter(HighSpeedConnection x) => x is { Client.Connected: true } && x.Steam64 == steam64;
        return HighSpeedServer.Instance.VerifiedConnections.FirstOrDefault(Filter) ?? HighSpeedServer.Instance.PendingConnections.FirstOrDefault(Filter);
    }

    [NetCall(NetCallSource.FromClient, (ushort)DevkitServerNetCall.SendSteamVerificationToken)]
    private static void ReceiveSteamToken(MessageContext ctx, Guid received, ulong steam64)
    {
        HighSpeedServer server = HighSpeedServer.Instance;
        if (server == null)
        {
            Logger.DevkitServer.LogWarning("HIGH SPEED NETWORKING", "Received steam high-speed verify packet before server was initialized.");
            return;
        }

        server.ReceiveVerifyPacket(ctx.Connection, received, steam64);
        Logger.DevkitServer.LogDebug("HIGH SPEED NETWORKING", "Received steam token from: " + ctx.Connection.GetAddress().Format() + ".");
    }

    public static NetTask TryCreate(ITransportConnection connection, out bool failedConfigCheck)
    {
        failedConfigCheck = true;
        if (DevkitServerConfig.Config.TcpSettings is not { EnableHighSpeedSupport: true })
            return NetTask.Completed;
        
        if (Provider.configData.Server.Use_FakeIP)
        {
            Logger.DevkitServer.LogWarning("HIGH SPEED NETWORKING", $"Can not use a high-speed server with \"{"Use_FakeIP".Colorize(DevkitServerModule.UnturnedColor)}\" enabled in Config.json.");
            Logger.DevkitServer.LogWarning("HIGH SPEED NETWORKING", $"Either disable {"FakeIP".Colorize(DevkitServerModule.UnturnedColor)} or disable high speed support in " +
                                                                    $"Servers/{Provider.serverID}/DevkitServer/server_config.json".Colorize(ConsoleColor.White) + " to hide this warning.");
            return NetTask.Completed;
        }

        failedConfigCheck = false;

        return OpenHighSpeedClient.RequestAck(connection, DevkitServerConfig.Config.TcpSettings.HighSpeedPort);
    }

    [NetCall(NetCallSource.FromClient, DevkitServerNetCall.RequestOpenHighSpeedConnection)]
    private static async Task<StandardErrorCode> ReceiveOpenHighSpeedConnectionRequest(MessageContext ctx)
    {
        if (DevkitServerConfig.Config.TcpSettings is not { EnableHighSpeedSupport: true })
            return StandardErrorCode.NotSupported;

        HighSpeedConnection? connection = await TryGetOrCreateAndVerify(ctx.Connection, true);
        return connection != null ? StandardErrorCode.Success : StandardErrorCode.GenericError;
    }

    [NetCall(NetCallSource.FromClient, DevkitServerNetCall.RequestReleaseOrTakeHighSpeedConnection)]
    private static StandardErrorCode ReceiveRequestReleaseOrTakeHighSpeedConnection(MessageContext ctx, bool isTake)
    {
        HighSpeedConnection? connection = ctx.Connection.FindHighSpeedConnection();

        if (connection == null)
            return StandardErrorCode.NotFound;

        if (isTake)
            TakeConnection(connection);
        else
            ReleaseConnection(connection);
        return StandardErrorCode.Success;
    }
#endif
#if CLIENT
    private static readonly List<MessageContext> PendingConnects = new List<MessageContext>();

    public static async Task<HighSpeedConnection?> TryGetOrCreateAndVerify(CancellationToken token = default)
    {
        HighSpeedConnection? instance = HighSpeedConnection.Instance;

        if (instance is { Client.Connected: true })
            return instance;

        if (ClientInfo.Info is { ServerHasHighSpeedSupport: false })
            return null;

        RequestResponse response = await RequestHighSpeedServer.RequestAck();
        return response.ErrorCode is not (int)StandardErrorCode.Success ? null : HighSpeedConnection.Instance;
    }

    public static void TakeConnection()
    {
        RequestReleaseOrTakeHighSpeedServer.Invoke(true);
    }
    public static void ReleaseConnection()
    {
        RequestReleaseOrTakeHighSpeedServer.Invoke(false);
    }

    [NetCall(NetCallSource.FromServer, DevkitServerNetCall.OpenHighSpeedClient)]
    private static void ReceiveOpenHighSpeedClient(MessageContext ctx, ushort port)
    {
        if (Provider.CurrentServerAdvertisement == null)
        {
            ctx.Acknowledge(StandardErrorCode.NotSupported);
            return;
        }
        HighSpeedConnection? connection = HighSpeedConnection.Instance;
        if (connection == null)
        {
            lock (PendingConnects)
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
            Logger.DevkitServer.LogWarning("HIGH SPEED NETWORKING", "Received steam token on non-high-speed connection: " + ctx.Connection.GetType().Format() + ".");
            return;
        }
        
        conn.SteamToken = received;
        Logger.DevkitServer.LogDebug("HIGH SPEED NETWORKING", "Sent steam token for high-speed connection.");
        SteamVerify.Invoke(received, Provider.client.m_SteamID);
    }

    [NetCall(NetCallSource.FromServer, (ushort)HighSpeedNetCall.VerifyConfirm, HighSpeed = true, HighSpeedAllowUnverified = true)]
    private static void ReceiveVerified(MessageContext ctx)
    {
        if (ctx.Connection is not HighSpeedConnection conn)
        {
            Logger.DevkitServer.LogWarning("HIGH SPEED NETWORKING", "Received steam token on non-high-speed connection: " + ctx.Connection.GetType().Format() + ".");
            return;
        }

        conn.Verified = true;
        Logger.DevkitServer.LogDebug("HIGH SPEED NETWORKING", "Verified high-speed connection.");
        lock (PendingConnects)
        {
            for (int i = 0; i < PendingConnects.Count; i++)
                PendingConnects[i].Acknowledge(StandardErrorCode.Success);

            PendingConnects.Clear();
        }
    }

    private static void BeginGetConnectionToServer(ushort port)
    {
        IPAddress ip = new IPAddress(DevkitServerUtility.ReverseUInt32(Provider.CurrentServerAdvertisement.ip));
        Logger.DevkitServer.LogInfo("HIGH SPEED NETWORKING", "Connecting to server at " + ip.Format() + ":" + port.Format() + ".");
        TcpClient client = new TcpClient
        {
            ReceiveBufferSize = BufferSize,
            SendBufferSize = BufferSize
        };
        try
        {
            client.BeginConnect(ip, port, EndConnect, client);
            return;
        }
        catch (SocketException ex)
        {
            Logger.DevkitServer.LogError("HIGH SPEED NETWORKING", ex, "Unable to connect to server: ");
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError("HIGH SPEED NETWORKING", ex, "Error connecting: ");
        }

        lock (PendingConnects)
        {
            for (int i = 0; i < PendingConnects.Count; i++)
                PendingConnects[i].Acknowledge(StandardErrorCode.GenericError);

            PendingConnects.Clear();
        }
    }
    private static void EndConnect(IAsyncResult ar)
    {
        if (ar.AsyncState is TcpClient client)
        {
            try
            {
                client.EndConnect(ar);
                Logger.DevkitServer.LogInfo("HIGH SPEED NETWORKING", "Finished connecting to " + client.Client.RemoteEndPoint.Format() + ".");
                HighSpeedConnection conn = new HighSpeedConnection(client, Provider.client.m_SteamID);
                conn.Listen();
                return;
            }
            catch (SocketException ex)
            {
                Logger.DevkitServer.LogError("HIGH SPEED NETWORKING", ex, "Unable to connect to server: ");
            }
            catch (Exception ex)
            {
                Logger.DevkitServer.LogError("HIGH SPEED NETWORKING", ex, "Error connecting: ");
            }
        }

        DevkitServerUtility.QueueOnMainThread(() =>
        {
            lock (PendingConnects)
            {
                for (int i = 0; i < PendingConnects.Count; i++)
                    PendingConnects[i].Acknowledge(StandardErrorCode.GenericError);

                PendingConnects.Clear();
            }
        });
    }

#endif
    internal static void Receive(HighSpeedConnection connection, ArraySegment<byte> bytes, in MessageOverhead overhead)
    {
        NetFactory.OnReceived(bytes, connection, in overhead, true);
    }
}

internal enum HighSpeedNetCall : ushort
{
    Verify = 1,
    VerifyConfirm = 2,
    SendWholeLevel = 3,
    SendFullLargeTransmission = 4
}