using Cysharp.Threading.Tasks;
using DanielWillett.SpeedBytes;
using DevkitServer.API.Multiplayer;
using DevkitServer.Configuration;
using DevkitServer.Multiplayer.Networking;
using System.Collections.Concurrent;
using System.Text.Json;

namespace DevkitServer.Util.Encoding;
internal class LargeMessageTransmissionCommunications : IDisposable
{
    internal static readonly NetCallCustom SendStart = new NetCallCustom(ReceiveStart, capacity: LargeMessageTransmission.HeaderCapacity);
    internal static readonly NetCallCustom SendSlowPacket = new NetCallCustom(ReceiveSlowPacket, capacity: LargeMessageTransmission.PacketCapacity);
    internal static readonly NetCallCustom SendSlowEnd = new NetCallCustom(ReceiveSlowEnd, capacity: LargeMessageTransmission.FooterCapacity);
    internal static readonly NetCall<int[]> SendSlowMissedPackets = new NetCall<int[]>(DevkitServerNetCall.SendMissedLargeTransmissionPackets);
    internal static readonly NetCall<Guid, int, int> SendSlowCheckup = new NetCall<Guid, int, int>(ReceiveSlowCheckup);
    internal static readonly NetCallCustom SendFullData = new NetCallCustom(ReceiveFullData, capacity: 8388608 /* 8 MiB */);
    internal static readonly NetCall Ping = new NetCall(ReceiveSlowHighSpeedPing);
    internal static readonly NetCall<Guid, long> SendProgressHint = new NetCall<Guid, long>(ReceiveProgressHint);

    private static readonly ConcurrentDictionary<Guid, LargeMessageTransmission> ActiveMessages = new ConcurrentDictionary<Guid, LargeMessageTransmission>();

    internal int ClientHintBytesProcessed;

    private readonly int _pendingSlowPacketCount;
    private int _initialMissingSlowPackets;
    private int _missingSlowPackets;
    private int _receivedSlowPackets;
    private BitArray? _clientSlowReceiveMask;
    private DateTime _lastNeedsToSendProgressUpdate = DateTime.MinValue;
    private byte[]? _clientSlowReceiveBuffer;
    private readonly HighSpeedConnection? _connectionSubbed;

    public bool IsServer { get; }
    public LargeMessageTransmission Transmission { get; }
#if SERVER
    public IReadOnlyList<ITransportConnection> Connections { get; }
#else
    public IClientTransport Connection { get; }
#endif
    public LargeMessageTransmissionCommunications(LargeMessageTransmission transmission, bool isServer)
    {
        Transmission = transmission;
        IsServer = isServer;
#if SERVER
        Connections = transmission.Connections;
#else
        Connection = transmission.Connection;
#endif

        if (isServer)
        {
            ActiveMessages.TryAdd(transmission.TransmissionId, transmission);
            Logger.DevkitServer.LogDebug(Transmission.LogSource, "Registered transmission as server.");
            return;
        }

        ClientHintBytesProcessed = 0;
        _pendingSlowPacketCount = Transmission.LowSpeedPacketCount;
        Transmission.Bandwidth = _pendingSlowPacketCount <= 1 ? Math.Min(Transmission.Bandwidth, Transmission.FinalContent.Count) : Transmission.Bandwidth;

        if (!isServer && Transmission.IsHighSpeed)
        {
#if CLIENT
            if (HighSpeedConnection.Instance is { } instance)
            {
                _connectionSubbed = instance;
                instance.BufferProgressUpdated += OnHighSpeedProgressUpdate;
            }
#else
            HighSpeedConnection? connection = Transmission.Connections[0].FindHighSpeedConnection();
            if (connection != null)
            {
                _connectionSubbed = connection;
                connection.BufferProgressUpdated += OnHighSpeedProgressUpdate;
            }
#endif
        }


        if (Transmission.Handler == null)
            return;

        if (!Transmission.IsHighSpeed)
        {
            Transmission.Handler.IsUsingPackets = true;
            Transmission.Handler.ReceivedPackets = 0;
            Transmission.Handler.TotalMissingPackets = 0;
            Transmission.Handler.InitialMissingPackets = 0;
            Transmission.Handler.TotalPackets = _pendingSlowPacketCount;
        }
        else
        {
            Transmission.Handler.IsUsingPackets = false;
            Transmission.Handler.ReceivedPackets = -1;
            Transmission.Handler.TotalMissingPackets = -1;
            Transmission.Handler.InitialMissingPackets = -1;
            Transmission.Handler.TotalPackets = -1;
        }

        Transmission.Handler.StartTimestamp = DateTime.UtcNow;
        Transmission.Handler.ReceivedBytes = 0;
        Transmission.Handler.TotalBytes = Transmission.FinalSize;
        Transmission.Handler.IsStarted = true;
    }

    private void OnHighSpeedProgressUpdate(long bytesDownloaded, long totalBytes)
    {
        if (Transmission.Handler != null)
        {
            Transmission.Handler.ReceivedBytes = bytesDownloaded;
            Transmission.Handler.TotalBytes = totalBytes;

            try
            {
                Transmission.Handler.IsDirty = true;
            }
            catch (Exception ex)
            {
                Logger.DevkitServer.LogError(Transmission.LogSource, ex, $"Failed to set IsDirty = true on handler: {Transmission.Handler.GetType().Format()}.");
            }
        }

        lock (this)
        {
            DateTime now = DateTime.UtcNow;
            if (!((now - _lastNeedsToSendProgressUpdate).TotalSeconds > 1d))
                return;

            _lastNeedsToSendProgressUpdate = now;
        }

        DevkitServerUtility.QueueOnMainThread(() =>
        {
#if CLIENT
            SendProgressHint.Invoke(Transmission.TransmissionId, ClientHintBytesProcessed);
#else
            SendProgressHint.Invoke(Connections[0], Transmission.TransmissionId, ClientHintBytesProcessed);
#endif
        }, token: Transmission.CancellationToken);
    }
    
#if SERVER
    internal async UniTask<bool[]> Send(CancellationToken token, UniTaskWrapper finalize, bool forceLowSpeed = false, int connectionIndex = -1)
#else
    internal async UniTask<bool> Send(CancellationToken token, UniTaskWrapper finalize, bool forceLowSpeed = false)
#endif
    {
#if SERVER

        forceLowSpeed &= !Provider.configData.Server.Use_FakeIP;

        int connCt = connectionIndex == -1 ? Connections.Count : 1;
        BitArray forceHighSpeed = new BitArray(connCt);
        for (int i = 0; i < connCt; ++i)
            forceHighSpeed[i] = Connections[i] is HighSpeedConnection;
        
        HighSpeedConnection?[]? highSpeedConnections = !forceLowSpeed ? await HighSpeedNetFactory.TryGetOrCreateAndVerify(connectionIndex == -1 ? Connections : [ Connections[connectionIndex] ], true, token).AsUniTask() : null;

        BitArray lowSpeedUpload = new BitArray(connCt);
        if (highSpeedConnections != null)
        {
            for (int i = 0; i < connCt; ++i)
            {
                if (highSpeedConnections[i] is not { Verified: true, Client.Connected: true })
                    lowSpeedUpload[i] = true;
            }
        }
        else
        {
            for (int i = 0; i < connCt; ++i)
                lowSpeedUpload[i] = true;
        }

        if (connCt > 1)
        {
            for (int i = 0; i < connCt; ++i)
            {
                Logger.DevkitServer.LogDebug(Transmission.LogSource, $"(Connection {i.Format()} {Connections[i].Format()}): " + (lowSpeedUpload[i]
                    ? "Using low-speed (Steamworks) upload option."
                    : "Using high-speed (TCP) upload option."));
            }
        }
        else
        {
            Logger.DevkitServer.LogDebug(Transmission.LogSource, lowSpeedUpload[0]
                ? "Using low-speed (Steamworks) upload option."
                : "Using high-speed (TCP) upload option.");
        }
#else
        bool forceHighSpeed = Connection is HighSpeedConnection;
        HighSpeedConnection? highSpeedConnection = !forceLowSpeed ? await HighSpeedNetFactory.TryGetOrCreateAndVerify(token).AsUniTask() : null;

        bool lowSpeedUpload = highSpeedConnection is not { Verified: true, Client.Connected: true };

        Logger.DevkitServer.LogDebug(Transmission.LogSource, lowSpeedUpload
            ? "Using low-speed (Steamworks) upload option."
            : "Using high-speed (TCP) upload option.");
#endif

        float startTime = CachedTime.RealtimeSinceStartup;
#if CLIENT
        bool success = false;
#else
        bool[]? successes = null;
#endif
        try
        {
#if SERVER
            UniTask<bool>[] tasks = new UniTask<bool>[connCt];
            for (int i = 0; i < connCt; ++i)
            {
                int index = connectionIndex == -1 ? i : connectionIndex;
                tasks[i] = UniTask.Create(async () =>
                {
                    bool success;
                    if (forceHighSpeed[index])
                    {
                        HighSpeedNetFactory.TakeConnection((HighSpeedConnection)Connections[index]);
                        if (!await HighSpeedDownload((HighSpeedConnection)Connections[index], index, token, finalize))
                            return false;

                        success = true;
                    }
                    else
                    {
                        if (lowSpeedUpload[index])
                            success = await LowSpeedSend(token, index, finalize);
                        else if (!(success = await HighSpeedDownload(highSpeedConnections![index]!, index, token, finalize)))
                            return false;
                    }

                    return success;
                });
            }

            successes = await UniTask.WhenAll(tasks);
#else
            if (forceHighSpeed)
            {
                HighSpeedNetFactory.TakeConnection();
                if (!await HighSpeedDownload((HighSpeedConnection)Connection, 0, token, finalize))
                    return false;

                success = true;
            }
            else
            {
                if (lowSpeedUpload)
                    success = await LowSpeedSend(token, 0, finalize);
                else if (!(success = await HighSpeedDownload(highSpeedConnection!, 0, token, finalize)))
                    return false;
            }
#endif
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError(Transmission.LogSource, ex, "Error sending transmission.");
#if CLIENT
            if (highSpeedConnection != null)
                HighSpeedNetFactory.ReleaseConnection();
#else
            if (highSpeedConnections != null)
            {
                for (int i = 0; i < highSpeedConnections.Length; ++i)
                {
                    HighSpeedConnection? c = highSpeedConnections[i];
                    if (c == null)
                        continue;
                    HighSpeedNetFactory.ReleaseConnection(c);
                }
            }
#endif
        }

#if CLIENT
        if (success)
            Logger.DevkitServer.LogDebug(Transmission.LogSource, $"Sent data ({Transmission.OriginalSize.Format()} B -> {Transmission.FinalSize.Format()} B) in {(CachedTime.RealtimeSinceStartup - startTime).Format("F2")} seconds.", ConsoleColor.DarkCyan);

        return success;
#else
        if (successes == null)
            return new bool[connCt];

        for (int i = 0; i < connCt; ++i)
        {
            Logger.DevkitServer.LogDebug(Transmission.LogSource, $"Sent data ({Transmission.OriginalSize.Format()} B -> {Transmission.FinalSize.Format()} B) in {(CachedTime.RealtimeSinceStartup - startTime).Format("F2")} seconds to {Connections[(connectionIndex == -1 ? i : connectionIndex)].Format()}.", ConsoleColor.DarkCyan);
        }

        return successes;
#endif
    }
    // ReSharper disable once UnusedParameter.Local
    private async UniTask<bool> HighSpeedDownload(HighSpeedConnection highSpeedConnection, int index, CancellationToken token, UniTaskWrapper finalize)
    {
        await finalize.FinalizeTask;

        // listens for pings to keep the download alive if it takes too long.
        NetTask pingListenerTask = Ping.Listen(15000);

#if SERVER
        RequestResponse sendStartResponse = await SendStart.RequestAck(Connections[index], Transmission.WriteStart);
#else
        RequestResponse sendStartResponse = await SendStart.RequestAck(Transmission.WriteStart);
#endif
        if (!sendStartResponse.Responded || !sendStartResponse.ErrorCode.HasValue || (sendStartResponse.ErrorCode & 0xFFFF) != (int)StandardErrorCode.Success)
        {
            Logger.DevkitServer.LogWarning(Transmission.LogSource, "Failed to initialize a high-speed connection.", ConsoleColor.DarkCyan);
#if SERVER
            HighSpeedNetFactory.ReleaseConnection(highSpeedConnection);
#else
            HighSpeedNetFactory.ReleaseConnection();
#endif

#if SERVER
            if (!ReferenceEquals(Connections[index], highSpeedConnection))
                await Send(token, new UniTaskWrapper(UniTask.CompletedTask), true, index);
#else
            if (!ReferenceEquals(Connection, highSpeedConnection))
                await Send(token, new UniTaskWrapper(UniTask.CompletedTask), true);
#endif

            return false;
        }

        ushort protocolVersion = (ushort)((sendStartResponse.ErrorCode.Value >> 16) & 0xFFFF);
        Transmission.ProtocolVersion = protocolVersion;

        Logger.DevkitServer.LogInfo(Transmission.LogSource, "Ready to upload to high-speed connection.", ConsoleColor.DarkCyan);

        NetTask uploadTask = SendFullData.RequestAck(highSpeedConnection, WriteFullData, 20000);
        while (!uploadTask.IsCompleted)
        {
            if (pingListenerTask.IsCompleted)
            {
                if (pingListenerTask.Parameters.Responded)
                {
                    uploadTask.KeepAlive();
                    Logger.DevkitServer.LogConditional(Transmission.LogSource, "Received keep-alive from client while downloading, refreshed timeout.");
                }

                pingListenerTask = new NetTask(false, pingListenerTask.RequestId, 10000);
                NetFactory.RegisterListener(pingListenerTask, Ping);
            }

            await UniTask.Yield();
        }

#if SERVER
        HighSpeedNetFactory.ReleaseConnection(highSpeedConnection);
#else
        HighSpeedNetFactory.ReleaseConnection();
#endif

        if (uploadTask.Parameters is { Responded: true, ErrorCode: (int)StandardErrorCode.Success })
            return true;

        Logger.DevkitServer.LogWarning(Transmission.LogSource, $"Failed to send {FormattingUtil.FormatCapacity(Transmission.FinalContent.Count, colorize: true)} of data over a high-speed connection, trying low speed.",
            ConsoleColor.DarkCyan);

#if SERVER
        if (!ReferenceEquals(Connections[index], highSpeedConnection))
            await Send(token, new UniTaskWrapper(UniTask.CompletedTask), true);
#else
        if (!ReferenceEquals(Connection, highSpeedConnection))
            await Send(token, new UniTaskWrapper(UniTask.CompletedTask), true);
#endif

        return false;

    }
    private async UniTask<bool> LowSpeedSend(CancellationToken token, int index, UniTaskWrapper finalize)
    {
        bool isLocalConnection = false;

#if SERVER
        string? ip = Connections[index].GetAddressString(false);
#else
        string? ip = Provider.isConnected ? Provider.CurrentServerConnectParameters.address.ToString() : "127.0.0.1";
#endif

        if (ip != null && ip.Equals("127.0.0.1", StringComparison.Ordinal))
            isLocalConnection = true;

        float averagePing = await PingLowSpeed(token, index); // todo isLocalConnection ? -1f : await PingLowSpeed(token);

        await finalize.FinalizeTask;
        await UniTask.SwitchToMainThread(token);

#if SERVER
        RequestResponse sendStartResponse = await SendStart.RequestAck(Connections[index], Transmission.WriteStart);
#else
        RequestResponse sendStartResponse = await SendStart.RequestAck(Transmission.WriteStart);
#endif
        await UniTask.SwitchToMainThread(token);

        if (!sendStartResponse.Responded || !sendStartResponse.ErrorCode.HasValue || (sendStartResponse.ErrorCode & 0xFFFF) != (int)StandardErrorCode.Success)
        {
            Logger.DevkitServer.LogWarning(Transmission.LogSource, "Failed to start sending data.", ConsoleColor.DarkCyan);
            return false;
        }

        ushort protocolVersion = (ushort)((sendStartResponse.ErrorCode.Value >> 16) & 0xFFFF);
        Transmission.ProtocolVersion = protocolVersion;

        float packetDelay = averagePing;
        if (isLocalConnection)
        {
            // local connections can download much faster
            packetDelay = 0.05f;
        }

        MessageOverhead overhead;

        int packetCount = Transmission.LowSpeedPacketCount;

        const int maxPacketCheckupInterval = 24;
        const float maxPacketDelay = 1f;

        int firstCheckupPosition = Math.Min(maxPacketCheckupInterval, (int)Math.Round(packetCount / 11d * 4d)); // idk it just fit
        int nextCheckupPacketIndex = firstCheckupPosition;
        int lastCheckupPacketIndex = 0;

        int bandwidth = packetCount <= 1 ? Math.Min(Transmission.Bandwidth, Transmission.FinalContent.Count) : Transmission.Bandwidth;

        byte[] dataBuffer = new byte[MessageOverhead.MaximumSize + sizeof(int) + 16 + bandwidth];

        int currentDataIndex = 0;

        int packetIndex = 0;
        while (true)
        {
            int packetSize = Math.Min(bandwidth, Transmission.FinalContent.Count - currentDataIndex);
            if (packetSize <= 0)
                break;

            overhead = new MessageOverhead(MessageFlags.None, (ushort)DevkitServerNetCall.SendLargeTransmissionPacket, sizeof(int) + 16 + packetSize);

            int headerSize = overhead.Length + sizeof(int) + 16;

            Buffer.BlockCopy(Transmission.FinalContent.Array!, Transmission.FinalContent.Offset + currentDataIndex, dataBuffer, headerSize, packetSize);
            overhead.GetBytes(dataBuffer, 0);
            UnsafeBitConverter.GetBytes(dataBuffer, Transmission.TransmissionId, overhead.Length);
            UnsafeBitConverter.GetBytes(dataBuffer, packetIndex, overhead.Length + 16);

            ++packetIndex;
            currentDataIndex += packetSize;
#if SERVER
            Connections[index].Send(dataBuffer, false, offset: 0, count: headerSize + packetSize);
#else
            Connection.Send(dataBuffer, false, offset: 0, count: headerSize + packetSize);
#endif

            // run checkups to see if we need to slow down our send interval
            if (packetIndex - 1 == nextCheckupPacketIndex)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(Math.Min(maxPacketDelay, packetDelay * 1.5f)), true, cancellationToken: token);

                RequestResponse checkupResponse = default;
                for (int retry = 0; retry < 2; ++retry)
                {
#if SERVER
                    checkupResponse = await SendSlowCheckup.RequestAck(Connections[index], Transmission.TransmissionId, lastCheckupPacketIndex, packetIndex - 1, timeoutMs: 15000);
#else
                    checkupResponse = await SendSlowCheckup.RequestAck(Transmission.TransmissionId, lastCheckupPacketIndex, packetIndex - 1, timeoutMs: 15000);
#endif
                    await UniTask.SwitchToMainThread(token);

                    if (checkupResponse is { Responded: true, ErrorCode: not null })
                        break;

                    if (retry > 0)
                    {
                        Logger.DevkitServer.LogInfo(Transmission.LogSource, $"User failed to respond to checkup at packet {packetIndex.Format()} / {packetCount.Format()}.", ConsoleColor.DarkCyan);
                        await Transmission.Cancel(token);
                        return false;
                    }
#if SERVER
                    if (!Connections[index].IsConnected())
                    {
                        Logger.DevkitServer.LogInfo(Transmission.LogSource, $"User disconnected at packet {packetIndex.Format()} / {packetCount.Format()} before finalizing transmission.", ConsoleColor.DarkCyan);
                        await Transmission.Cancel(token);
                        return false;
                    }
#endif

                    await UniTask.Delay(TimeSpan.FromSeconds(0.5), true, cancellationToken: token);
                }

                if (!checkupResponse.Responded)
                {
                    Logger.DevkitServer.LogInfo(Transmission.LogSource, $"User didn't respond to checkup at packet {packetIndex.Format()} / {packetCount.Format()}.", ConsoleColor.DarkCyan);
                    await Transmission.Cancel(token);
                    return false;
                }

                if (packetDelay < maxPacketDelay)
                {
                    int misses = checkupResponse.ErrorCode!.Value;
                    if (misses < 0)
                    {
                        Logger.DevkitServer.LogInfo(Transmission.LogSource, $"User errored during checkup at packet {packetIndex.Format()} / {packetCount.Format()}.", ConsoleColor.DarkCyan);
                        await Transmission.Cancel(token);
                        return false;
                    }

                    if (misses == 0 && packetDelay > 0.06f)
                    {
                        float old = packetDelay;
                        packetDelay *= 0.75f;
                        Logger.DevkitServer.LogConditional(Transmission.LogSource, $"Packet delay decreased from {old.Format()} -> {packetDelay.Format()}.");
                    }
                    if (misses > 0)
                    {
                        float old = packetDelay;
                        packetDelay = Math.Min(1f, packetDelay / ((float)misses / ((packetIndex - lastCheckupPacketIndex) * 2)));
                        Logger.DevkitServer.LogConditional(Transmission.LogSource, $"Packet delay increasesd from {old.Format()} -> {packetDelay.Format()} after missing {misses.Format()} / {(packetIndex - lastCheckupPacketIndex).Format()} packet(s).");
                    }

                    nextCheckupPacketIndex += firstCheckupPosition;
                    lastCheckupPacketIndex = packetIndex;
                }
            }

            await UniTask.Delay(TimeSpan.FromSeconds(packetDelay), true, cancellationToken: token);

#if SERVER
            if (Connections[index].IsConnected())
                continue;

            Logger.DevkitServer.LogInfo(Transmission.LogSource, $"User disconnected at packet {packetIndex.Format()} / {packetCount.Format()} before finalizing transmission.", ConsoleColor.DarkCyan);
            await Transmission.Cancel(token);
            return false;
#endif
        }

        await UniTask.Delay(TimeSpan.FromSeconds(0.25), true, cancellationToken: token);

#if SERVER
        if (!Connections[index].IsConnected())
        {
            Logger.DevkitServer.LogInfo(Transmission.LogSource, "User disconnected before finalizing transmission.", ConsoleColor.DarkCyan);
            await Transmission.Cancel(token);
            return false;
        }
#endif

        // recover missed packets
        while (true)
        {
#if SERVER
            RequestResponse sendEndResponse = await SendSlowEnd.Request(SendSlowMissedPackets, Connections[index], Transmission.WriteEnd);
#else
            RequestResponse sendEndResponse = await SendSlowEnd.Request(SendSlowMissedPackets, Transmission.WriteEnd);
#endif
            await UniTask.SwitchToMainThread(token);

            if (!sendEndResponse.Responded)
            {
                await Transmission.Cancel(token);
                Logger.DevkitServer.LogInfo(Transmission.LogSource, "User disconnected before finalizing transmission.", ConsoleColor.DarkCyan);
                return false;
            }

            if (!sendEndResponse.TryGetParameter(0, out int[] packets) || packets.Length <= 0)
                break;

            Array.Sort(packets);

            for (int i = 0; i < packets.Length; ++i)
            {
                packetIndex = packets[i];

                currentDataIndex = bandwidth * packetIndex;

                int packetSize = Math.Min(bandwidth, Transmission.FinalContent.Count - currentDataIndex);
                if (packetSize <= 0)
                    continue;
                NetTask slowPacketListener = SendSlowPacket.ListenAck(1500);
                overhead = new MessageOverhead(MessageFlags.AcknowledgeRequest, (ushort)DevkitServerNetCall.SendLargeTransmissionPacket,
                    sizeof(int) + 16 + packetSize, slowPacketListener.RequestId);

                int headerSize = overhead.Length + sizeof(int) + 16;

                Buffer.BlockCopy(Transmission.FinalContent.Array!, Transmission.FinalContent.Offset + currentDataIndex, dataBuffer, headerSize, packetSize);

                overhead.GetBytes(dataBuffer, 0);
                UnsafeBitConverter.GetBytes(dataBuffer, Transmission.TransmissionId, overhead.Length);
                UnsafeBitConverter.GetBytes(dataBuffer, packetIndex, overhead.Length + 16);

                for (int retry = 0; retry < 2; ++retry)
                {
#if SERVER
                    Connections[index].Send(dataBuffer, true, count: headerSize + packetSize);
#else
                    Connection.Send(dataBuffer, true, count: headerSize + packetSize);
#endif
                    RequestResponse response = await slowPacketListener;
                    await UniTask.SwitchToMainThread(token);

                    if (response.ErrorCode is (int)StandardErrorCode.Success)
                        break;
                    
                    if (response.ErrorCode.HasValue)
                        Logger.DevkitServer.LogWarning(Transmission.LogSource, $"Recovery packet failed with error code: {response.ErrorCode.Value.Format("X8")}.");

                    if (i == 0)
                    {
                        Logger.DevkitServer.LogWarning(Transmission.LogSource, $"Retrying recovery packet {(packetIndex + 1).Format()}.");
                        continue;
                    }

                    Logger.DevkitServer.LogError(Transmission.LogSource, $"Failed retry of sending recovery packet {(packetIndex + 1).Format()}.");
                    return false;
                }
            }
        }

        return true;
    }
    // ReSharper disable once UnusedParameter.Local
    private async UniTask<float> PingLowSpeed(CancellationToken token, int index)
    {
        const int pingCt = 5;
        const int pingSpacingMs = 500;

        float avgPing = 0f;
        for (int i = 0; i < pingCt; ++i)
        {
            float start = CachedTime.RealtimeSinceStartup;
#if SERVER
            RequestResponse response = await Ping.RequestAck(Connections[index], 2000);
#else
            RequestResponse response = await Ping.RequestAck(2000);
#endif
            await UniTask.SwitchToMainThread(token);
            if (!response.Responded)
            {
                Logger.DevkitServer.LogInfo(Transmission.LogSource, $"User failed to respond to ping check #{(i + 1).Format()}.", ConsoleColor.DarkCyan);
                return -1f;
            }

            avgPing += CachedTime.RealtimeSinceStartup - start;
            await UniTask.Delay(TimeSpan.FromMilliseconds(pingSpacingMs), ignoreTimeScale: true, cancellationToken: token);

#if SERVER
            if (Connections[index].IsConnected())
                continue;

            Logger.DevkitServer.LogInfo(Transmission.LogSource, $"User disconected during at ping check #{(i + 1).Format()}.", ConsoleColor.DarkCyan);
            return -1f;
#endif
        }

        avgPing /= pingCt;

        Logger.DevkitServer.LogConditional(Transmission.LogSource, $"Average ping: {avgPing * 1000:F2} ms (n = {pingCt.Format()}, t = {pingSpacingMs.Format("0.##")} ms).");
        return avgPing;
    }

    [UsedImplicitly]
    private void HandleSlowPacket(in MessageContext ctx, ByteReader reader)
    {
        int packetIndex = reader.ReadInt32();
        if (_pendingSlowPacketCount <= 0)
        {
            Logger.DevkitServer.LogError(Transmission.LogSource, "Received data before a start packet.");
            ctx.Acknowledge(StandardErrorCode.AccessViolation);
            return;
        }

        if (packetIndex < 0 || packetIndex >= _pendingSlowPacketCount)
        {
            Logger.DevkitServer.LogError(Transmission.LogSource, $"Received data packet out of range of expected ({(packetIndex + 1).Format()} / {_pendingSlowPacketCount.Format()}), ignoring.");
            ctx.Acknowledge(StandardErrorCode.InvalidData);
            return;
        }

        _clientSlowReceiveMask ??= new BitArray(_pendingSlowPacketCount);
        _clientSlowReceiveBuffer ??= new byte[Transmission.FinalSize];

        if (_clientSlowReceiveMask[packetIndex])
        {
            Logger.DevkitServer.LogError(Transmission.LogSource, $"Packet already downloaded ({(packetIndex + 1).Format()} / {_pendingSlowPacketCount.Format()}), replacing.");
        }
        else
        {
            _clientSlowReceiveMask[packetIndex] = true;
        }

        int dataIndex = packetIndex * Transmission.Bandwidth;
        int packetLength = Math.Min(Transmission.Bandwidth, reader.Length - reader.Position);
        
        Buffer.BlockCopy(reader.InternalBuffer!, reader.Position, _clientSlowReceiveBuffer, dataIndex, packetLength);
        ClientHintBytesProcessed += packetLength;
        reader.Skip(packetLength);
        if (_initialMissingSlowPackets > 0)
        {
            if (_missingSlowPackets > 0)
                --_missingSlowPackets;
            Logger.DevkitServer.LogConditional(Transmission.LogSource, $"Recovered ({FormattingUtil.FormatCapacity(packetLength, colorize: true)}) (packet #{(packetIndex + 1).Format()}).", ConsoleColor.DarkCyan);
        }
        else
        {
            Logger.DevkitServer.LogConditional(Transmission.LogSource, $"Received ({FormattingUtil.FormatCapacity(packetLength, colorize: true)}) (packet #{(packetIndex + 1).Format()}).", ConsoleColor.DarkCyan);
        }

        ++_receivedSlowPackets;

        if (Transmission.Handler != null)
        {
            Transmission.Handler.InitialMissingPackets = _initialMissingSlowPackets;
            Transmission.Handler.TotalMissingPackets = _missingSlowPackets;
            Transmission.Handler.ReceivedBytes += packetLength;
            Transmission.Handler.ReceivedPackets = _receivedSlowPackets;
            try
            {
                Transmission.Handler.IsDirty = true;
            }
            catch (Exception ex)
            {
                Logger.DevkitServer.LogError(Transmission.LogSource, ex, $"Failed to set IsDirty = true on handler: {Transmission.Handler.GetType().Format()}.");
            }
        }
        ctx.Acknowledge(StandardErrorCode.Success);

        DateTime now = DateTime.UtcNow;
        if (!((now - _lastNeedsToSendProgressUpdate).TotalSeconds > 1d))
            return;

        _lastNeedsToSendProgressUpdate = now;

#if CLIENT
        SendProgressHint.Invoke(Transmission.TransmissionId, ClientHintBytesProcessed);
#else
        SendProgressHint.Invoke(Connections[0], Transmission.TransmissionId, ClientHintBytesProcessed);
#endif
    }

    [UsedImplicitly]
    private void HandleSlowEnd(in MessageContext ctx, ByteReader reader)
    {
        bool cancelled = reader.ReadBool();

        if (IsServer)
        {
            if (!cancelled)
            {
                Logger.DevkitServer.LogWarning(Transmission.LogSource, $"Received server transmission: {Transmission.TransmissionId.Format()}, but expected client for a non-cancelling end message.");
                return;
            }

            ctx.Acknowledge();
            Logger.DevkitServer.LogInfo(Transmission.LogSource, "Cancelled transmission by request.");
            MessageContext ctx2 = ctx;
            UniTask.Create(async () =>
            {
                bool val;
                try
                {
                    val = await Transmission.Cancel();
                }
                catch (InvalidOperationException)
                {
                    Logger.DevkitServer.LogWarning(Transmission.LogSource, $"Received cancel message when invalid for transmission: {Transmission.TransmissionId.Format()}.");
                    ctx2.Acknowledge(StandardErrorCode.NotSupported);
                    return;
                }

                ctx2.Acknowledge(val ? StandardErrorCode.Success : StandardErrorCode.GenericError);
            });
            return;
        }

        if (cancelled)
        {
            ctx.Reply(SendSlowMissedPackets, Array.Empty<int>());
            Logger.DevkitServer.LogInfo(Transmission.LogSource, "Cancelled transmission by request.");
            if (Transmission.Handler != null)
            {
                try
                {
                    Transmission.Handler.IsDirty = true;
                }
                catch (Exception ex)
                {
                    Logger.DevkitServer.LogError(Transmission.LogSource, ex, $"Failed to set IsDirty = true on handler: {Transmission.Handler.GetType().Format()}.");
                }

                try
                {
                    Transmission.Handler.OnFinished(LargeMessageTransmissionStatus.Cancelled);
                }
                catch (Exception ex)
                {
                    Logger.DevkitServer.LogError(Transmission.LogSource, ex, $"Failed to run OnFinished(Cancelled) on handler: {Transmission.Handler.GetType().Format()}.");
                }
            }
            Transmission.Dispose();
            return;
        }

        int[]? missingPackets = null;
        if (_clientSlowReceiveMask == null)
        {
            missingPackets = new int[_pendingSlowPacketCount];

            Logger.DevkitServer.LogConditional(Transmission.LogSource, $"All packets missing: * / {_pendingSlowPacketCount.Format()}.");

            for (int i = 0; i < _pendingSlowPacketCount; ++i)
                missingPackets[i] = i;

        }
        else
        {
            int missing = 0;
            for (int i = 0; i < _pendingSlowPacketCount; ++i)
            {
                if (!_clientSlowReceiveMask[i])
                    ++missing;
            }
            if (missing > 0)
            {
                missingPackets = new int[missing];
                for (int i = _pendingSlowPacketCount - 1; i >= 0; --i)
                {
                    if (_clientSlowReceiveMask[i])
                        continue;

                    missingPackets[--missing] = i;
                    Logger.DevkitServer.LogConditional(Transmission.LogSource, $"Packet missing: {(i + 1).Format()} / {_pendingSlowPacketCount.Format()}.");
                    break;
                }
            }
        }

        if (missingPackets != null)
        {
            _initialMissingSlowPackets = _missingSlowPackets = missingPackets.Length;

            if (Transmission.Handler != null)
            {
                Transmission.Handler.InitialMissingPackets = _initialMissingSlowPackets;
                Transmission.Handler.TotalMissingPackets = _missingSlowPackets;
                try
                {
                    Transmission.Handler.IsDirty = true;
                }
                catch (Exception ex)
                {
                    Logger.DevkitServer.LogError(Transmission.LogSource, ex, $"Failed to set IsDirty = true on handler: {Transmission.Handler.GetType().Format()}.");
                }
            }

            ctx.Reply(SendSlowMissedPackets, missingPackets);
            Logger.DevkitServer.LogWarning(Transmission.LogSource, $"Missing {missingPackets.Length.Format()} / {_pendingSlowPacketCount.Format()} ({((float)missingPackets.Length / _pendingSlowPacketCount).Format("P1")}).");
            return;
        }

        ctx.Reply(SendSlowMissedPackets, Array.Empty<int>());
        Transmission.FinalContent = _clientSlowReceiveBuffer;
        ctx.Acknowledge(StandardErrorCode.Success);

        if (Transmission.Handler != null)
        {
            Transmission.Handler.InitialMissingPackets = 0;
            Transmission.Handler.TotalMissingPackets = 0;
            Transmission.Handler.IsDownloaded = true;
            Transmission.Handler.EndTimestamp = DateTime.UtcNow;
            Transmission.Handler.ReceivedBytes = Transmission.FinalSize;
            Transmission.Handler.ReceivedPackets = _pendingSlowPacketCount;
            try
            {
                Transmission.Handler.IsDirty = true;
            }
            catch (Exception ex)
            {
                Logger.DevkitServer.LogError(Transmission.LogSource, ex, $"Failed to set IsDirty = true on handler: {Transmission.Handler.GetType().Format()}.");
            }
        }

        Transmission.OnFinalContentCompleted();
    }

    internal async UniTask<bool> Cancel(CancellationToken token = default)
    {
        if (IsServer)
        {
#if SERVER
            RequestResponse sendEndResponse = await SendSlowEnd.Request(SendSlowMissedPackets, Connections[0], Transmission.WriteEndCancelled);
#else
            RequestResponse sendEndResponse = await SendSlowEnd.Request(SendSlowMissedPackets, Transmission.WriteEndCancelled);
#endif
            return sendEndResponse.Responded && sendEndResponse.TryGetParameter(0, out int[] arr) && arr.Length == 0;
        }
        else
        {
#if SERVER
            RequestResponse sendEndResponse = await SendSlowEnd.RequestAck(Connections[0], Transmission.WriteEndCancelled);
#else
            RequestResponse sendEndResponse = await SendSlowEnd.RequestAck(Transmission.WriteEndCancelled);
#endif
            if (sendEndResponse.ErrorCode is not (int)StandardErrorCode.Success)
                return false;

            if (Transmission.Handler == null)
                return true;

            try
            {
                Transmission.Handler.IsDirty = true;
            }
            catch (Exception ex)
            {
                Logger.DevkitServer.LogError(Transmission.LogSource, ex, $"Failed to set IsDirty = true on handler: {Transmission.Handler.GetType().Format()}.");
            }
            try
            {
                Transmission.Handler.OnFinished(LargeMessageTransmissionStatus.Cancelled);
            }
            catch (Exception ex)
            {
                Logger.DevkitServer.LogError(Transmission.LogSource, ex, $"Failed to run OnFinished(Cancelled) on handler: {Transmission.Handler.GetType().Format()}.");
            }

            return true;
        }
    }

    [UsedImplicitly]
    private int HandleSlowCheckup(in MessageContext ctx, int checkupStartIndex, int checkupEndIndex)
    {
        if (_clientSlowReceiveMask == null)
            return checkupEndIndex - checkupStartIndex;


        Logger.DevkitServer.LogConditional(Transmission.LogSource, $"Received checkup: {checkupStartIndex.Format()} -> {checkupEndIndex.Format()}.");
        int missing = 0;
        for (int i = checkupStartIndex; i <= checkupEndIndex; ++i)
        {
            if (!_clientSlowReceiveMask[checkupStartIndex])
                ++missing;
        }

        return missing;
    }

    [UsedImplicitly]
    private void HandleFullData(in MessageContext ctx, ByteReader reader)
    {
        byte[] bytes = reader.ReadBlock(reader.ReadInt32());
        Transmission.FinalContent = new ArraySegment<byte>(bytes);
            
        ctx.Acknowledge(StandardErrorCode.Success);

        ClientHintBytesProcessed = Transmission.FinalSize;

        if (Transmission.Handler != null)
        {
            Transmission.Handler.IsDownloaded = true;
            Transmission.Handler.EndTimestamp = DateTime.UtcNow;
            Transmission.Handler.ReceivedBytes = Transmission.FinalSize;
            try
            {
                Transmission.Handler.IsDirty = true;
            }
            catch (Exception ex)
            {
                Logger.DevkitServer.LogError(Transmission.LogSource, ex, $"Failed to set IsDirty = true on handler: {Transmission.Handler.GetType().Format()}.");
            }
        }

        Transmission.OnFinalContentCompleted();
    }

    private void WriteFullData(ByteWriter writer)
    {
        writer.Write(Transmission.TransmissionId);

        ArraySegment<byte> content = Transmission.FinalContent;
        writer.Write(content.Count);
        writer.WriteBlock(content);
    }
    public void Dispose()
    {
        _clientSlowReceiveMask = null;
        _clientSlowReceiveBuffer = null;
        _initialMissingSlowPackets = 0;
        _missingSlowPackets = 0;
        _receivedSlowPackets = 0;

        if (_connectionSubbed != null)
            _connectionSubbed.BufferProgressUpdated -= OnHighSpeedProgressUpdate;

        ActiveMessages.Remove(Transmission.TransmissionId, out _);

        Logger.DevkitServer.LogDebug(Transmission.LogSource, "Disposed.");
    }

    #region Receivers
    [NetCall(NetCallSource.FromEither, DevkitServerNetCall.Ping)]
    private static void ReceiveSlowHighSpeedPing(MessageContext ctx)
    {
        ctx.Acknowledge();
    }
    [NetCall(NetCallSource.FromEither, DevkitServerNetCall.SendProgressHint)]
    private static void ReceiveProgressHint(MessageContext ctx, Guid guid, long bytesProcessed)
    {
        if (!ActiveMessages.TryGetValue(guid, out LargeMessageTransmission transmission))
        {
            Logger.DevkitServer.LogWarning("LARGE MSG", $"Received unknown transmission: {guid.Format()}.");
            ctx.Acknowledge(StandardErrorCode.NotFound);
            return;
        }

        if (!transmission.Comms.IsServer)
        {
            Logger.DevkitServer.LogWarning(transmission.LogSource, $"Received server transmission: {guid.Format()}, but expected client.");
            ctx.Acknowledge(StandardErrorCode.AccessViolation);
            return;
        }

        transmission.Comms.ClientHintBytesProcessed = checked((int)bytesProcessed);

        Logger.DevkitServer.LogConditional(transmission.LogSource, $"Received progress hint from client: {FormattingUtil.FormatCapacity(bytesProcessed, colorize: true)} / {FormattingUtil.FormatCapacity(transmission.FinalSize, colorize: true)}.");

        if (transmission.Handler == null)
            return;

        transmission.Handler.ReceivedBytes = bytesProcessed;
        try
        {
            transmission.Handler.IsDirty = true;
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError(transmission.LogSource, ex, $"Failed to set IsDirty = true on handler: {transmission.Handler.GetType().Format()}.");
        }
    }

    [NetCall(NetCallSource.FromEither, DevkitServerNetCall.SendStartLargeTransmission)]
    private static void ReceiveStart(MessageContext ctx, ByteReader reader)
    {
        LargeMessageTransmission transmission = new LargeMessageTransmission(
#if SERVER
            ctx.Connection, 
#endif
            reader);
        
        if (ActiveMessages.TryRemove(transmission.TransmissionId, out LargeMessageTransmission old))
        {
            Logger.DevkitServer.LogWarning(transmission.LogSource, $"Received duplicate transmission: {transmission.TransmissionId.Format()}, disposing.");
            old.Dispose();
        }

        ActiveMessages[transmission.TransmissionId] = transmission;
        Logger.DevkitServer.LogDebug(transmission.LogSource, "Started transmission as client.");
        transmission.Handler?.OnStart();

        // ReSharper disable once ShiftExpressionZeroLeftOperand
        ctx.Acknowledge((int)StandardErrorCode.Success | (LargeMessageTransmission.GlobalProtocolVersion << 16));
    }

    [NetCall(NetCallSource.FromEither, DevkitServerNetCall.SendLargeTransmissionPacket)]
    private static void ReceiveSlowPacket(MessageContext ctx, ByteReader reader)
    {
        Guid guid = reader.ReadGuid();

        if (!ActiveMessages.TryGetValue(guid, out LargeMessageTransmission transmission))
        {
            Logger.DevkitServer.LogWarning("LARGE MSG", $"Received unknown transmission: {guid.Format()}.");
            ctx.Acknowledge(StandardErrorCode.NotFound);
            return;
        }

        if (transmission.Comms.IsServer)
        {
            Logger.DevkitServer.LogWarning(transmission.LogSource, $"Received server transmission: {guid.Format()}, but expected client.");
            ctx.Acknowledge(StandardErrorCode.AccessViolation);
            return;
        }

        transmission.Comms.HandleSlowPacket(in ctx, reader);
    }

    [NetCall(NetCallSource.FromEither, DevkitServerNetCall.SendEndLargeTransmission)]
    private static void ReceiveSlowEnd(MessageContext ctx, ByteReader reader)
    {
        Guid guid = reader.ReadGuid();

        if (!ActiveMessages.TryGetValue(guid, out LargeMessageTransmission transmission))
        {
            Logger.DevkitServer.LogWarning("LARGE MSG", $"Received unknown transmission: {guid.Format()}.");
            return;
        }

        transmission.Comms.HandleSlowEnd(in ctx, reader);
    }

    [NetCall(NetCallSource.FromEither, DevkitServerNetCall.SendLargeTransmissionCheckup)]
    private static void ReceiveSlowCheckup(MessageContext ctx, Guid guid, int start, int end)
    {
        if (!ActiveMessages.TryGetValue(guid, out LargeMessageTransmission transmission))
        {
            Logger.DevkitServer.LogWarning("LARGE MSG", $"Received unknown transmission: {guid.Format()}.");
            ctx.Acknowledge(-1);
            return;
        }

        if (transmission.Comms.IsServer)
        {
            Logger.DevkitServer.LogWarning(transmission.LogSource, $"Received server transmission: {guid.Format()}, but expected client.");
            ctx.Acknowledge(-2);
            return;
        }

        ctx.Acknowledge(transmission.Comms.HandleSlowCheckup(in ctx, start, end));
    }

    [NetCall(NetCallSource.FromEither, (ushort)HighSpeedNetCall.SendFullLargeTransmission, HighSpeed = true)]
    private static void ReceiveFullData(MessageContext ctx, ByteReader reader)
    {
        Guid guid = reader.ReadGuid();

        if (!ActiveMessages.TryGetValue(guid, out LargeMessageTransmission transmission))
        {
            Logger.DevkitServer.LogWarning("LARGE MSG", $"Received unknown transmission: {guid.Format()}.");
            ctx.Acknowledge(StandardErrorCode.NotFound);
            return;
        }

        if (transmission.Comms.IsServer)
        {
            Logger.DevkitServer.LogWarning(transmission.LogSource, $"Received server transmission: {guid.Format()}, but expected client.");
            ctx.Acknowledge(StandardErrorCode.AccessViolation);
            return;
        }

        transmission.Comms.HandleFullData(in ctx, reader);
    }
    #endregion

    internal static void DumpDebug()
    {
        foreach (LargeMessageTransmission transmission in ActiveMessages.Values)
        {
            Logger.DevkitServer.LogInfo(transmission.LogSource, "== Large Message Transmission ==");
            Logger.DevkitServer.LogInfo(transmission.LogSource, $"  ID: {transmission.TransmissionId.Format()}");
            Logger.DevkitServer.LogInfo(transmission.LogSource, $"  Is Server: {transmission.Comms.IsServer.Format()}");
            Logger.DevkitServer.LogInfo(transmission.LogSource, $"  Was Cancelled: {transmission.WasCancelled.Format()}");
            Logger.DevkitServer.LogInfo(transmission.LogSource, $"  Allow; High Speed: {transmission.AllowHighSpeed.Format()}, Compression: {transmission.AllowCompression.Format()}");
#if SERVER
            Logger.DevkitServer.LogInfo(transmission.LogSource, $"  Connection: {transmission.Connections.FormatList()}");
#else
            Logger.DevkitServer.LogInfo(transmission.LogSource, $"  Connection: {transmission.Connection.Format()}");
#endif
            Logger.DevkitServer.LogInfo(transmission.LogSource, $"  Compressed: {transmission.IsCompressed.Format()}, High Speed: {transmission.IsHighSpeed.Format()}");
            Logger.DevkitServer.LogInfo(transmission.LogSource, $"  Original Size: {transmission.OriginalSize.Format()} B, Final Size: {transmission.FinalSize.Format()} B");
            Logger.DevkitServer.LogInfo(transmission.LogSource, $"  Bandwidth: {transmission.Bandwidth.Format()} B");
            Logger.DevkitServer.LogInfo(transmission.LogSource, $"  Handler type: {transmission.HandlerType.Format()}");
            Logger.DevkitServer.LogInfo(transmission.LogSource, $"   Projected packet count: {transmission.LowSpeedPacketCount.Format()}");
            Logger.DevkitServer.LogInfo(transmission.LogSource, $"   Flags: {("0b" + Convert.ToString(transmission.Flags, 2)).Colorize(FormattingUtil.NumberColor)}");

            if (transmission.Handler != null)
            {
                Logger.DevkitServer.LogInfo(transmission.LogSource, $"Handler ({transmission.Handler.GetType().Format()}: {Environment.NewLine}" +
                                                                     JsonSerializer.Serialize(transmission.Handler, transmission.HandlerType!,
                                                                         DevkitServerConfig.SerializerSettings));
            }

            Logger.DevkitServer.LogInfo(string.Empty, string.Empty);
            Logger.DevkitServer.LogInfo(string.Empty, string.Empty);
        }
    }
    internal static IReadOnlyList<LargeMessageTransmission> GetReceivingMessages()
    {
        return ActiveMessages.Values.Where(x => !x.Comms.IsServer).ToArray();
    }
    internal static IReadOnlyList<LargeMessageTransmission> GetSendingMessages()
    {
        return ActiveMessages.Values.Where(x => x.Comms.IsServer).ToArray();
    }

    internal class UniTaskWrapper
    {
        public readonly UniTask FinalizeTask;
        public UniTaskWrapper(UniTask finalizeTask)
        {
            FinalizeTask = finalizeTask;
        }
    }
}