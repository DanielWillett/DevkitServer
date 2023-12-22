using Cysharp.Threading.Tasks;
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
    internal static readonly NetCall Ping = new NetCall(ReceiveSlowPing);

    private static readonly ConcurrentDictionary<Guid, LargeMessageTransmission> ActiveMessages = new ConcurrentDictionary<Guid, LargeMessageTransmission>();

    private readonly int _pendingSlowPacketCount;
    private int _initialMissingSlowPackets;
    private int _missingSlowPackets;
    private int _receivedSlowPackets;
    private BitArray? _clientSlowReceiveMask;
    private byte[]? _clientSlowReceiveBuffer;
#if CLIENT
    private readonly HighSpeedConnection? _connectionSubbed;
#endif

    public bool IsServer { get; }
    public LargeMessageTransmission Transmission { get; }
#if SERVER
    public ITransportConnection Connection { get; }
#else
    public IClientTransport Connection { get; }
#endif
    public LargeMessageTransmissionCommunications(LargeMessageTransmission transmission, bool isServer)
    {
        Transmission = transmission;
        IsServer = isServer;
        Connection = transmission.Connection;

        if (isServer)
        {
            ActiveMessages.TryAdd(transmission.TransmissionId, transmission);
            Logger.DevkitServer.LogDebug(Transmission.LogSource, "Registered transmission as server.");
            return;
        }
        
        _pendingSlowPacketCount = Transmission.LowSpeedPacketCount;
        Transmission.Bandwidth = _pendingSlowPacketCount <= 1 ? Math.Min(Transmission.Bandwidth, Transmission.FinalContent.Count) : Transmission.Bandwidth;

#if CLIENT
        if (Transmission.IsHighSpeed && HighSpeedConnection.Instance is { } instance)
        {
            _connectionSubbed = instance;
            instance.BufferProgressUpdated += OnHighSpeedProgressUpdate;
        }
#endif

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

#if CLIENT
    private void OnHighSpeedProgressUpdate(int bytesDownloaded, int totalBytes)
    {
        if (Transmission.Handler == null)
            return;
        
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
#endif

    internal async UniTask<bool> Send(CancellationToken token, UniTask finalize, bool forceLowSpeed = false)
    {
        bool forceHighSpeed = Connection is HighSpeedConnection;
#if SERVER
        HighSpeedConnection? highSpeedConnection = !forceLowSpeed ? await HighSpeedNetFactory.TryGetOrCreateAndVerify(Connection, true, token).AsUniTask() : null;
#else
        HighSpeedConnection? highSpeedConnection = !forceLowSpeed ? await HighSpeedNetFactory.TryGetOrCreateAndVerify(token).AsUniTask() : null;
#endif
        bool lowSpeedUpload = highSpeedConnection is not { Verified: true, Client.Connected: true };

        Logger.DevkitServer.LogDebug(Transmission.LogSource, lowSpeedUpload
            ? "Using low-speed (Steamworks) upload option."
            : "Using high-speed (TCP) upload option.");

        float startTime = CachedTime.RealtimeSinceStartup;

        bool success = false;
        try
        {
            if (forceHighSpeed)
            {
#if SERVER
                HighSpeedNetFactory.TakeConnection((HighSpeedConnection)Connection);
#else
                HighSpeedNetFactory.TakeConnection();
#endif
                if (!await HighSpeedDownload((HighSpeedConnection)Connection, token, finalize))
                    return false;

                success = true;
            }
            else
            {
                if (lowSpeedUpload)
                    success = await LowSpeedSend(token, finalize);
                else if (!(success = await HighSpeedDownload(highSpeedConnection!, token, finalize)))
                    return false;
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError(Transmission.LogSource, ex, "Error sending transmission.");

            if (highSpeedConnection != null)
            {
#if SERVER
                HighSpeedNetFactory.ReleaseConnection((HighSpeedConnection)Connection);
#else
                HighSpeedNetFactory.ReleaseConnection();
#endif
            }
        }

        if (success)
            Logger.DevkitServer.LogDebug(Transmission.LogSource, $"Sent data ({Transmission.OriginalSize.Format()} B -> {Transmission.FinalSize.Format()} B) in {(CachedTime.RealtimeSinceStartup - startTime).Format("F2")} seconds.", ConsoleColor.DarkCyan);

        return success;
    }
    private async UniTask<bool> HighSpeedDownload(HighSpeedConnection highSpeedConnection, CancellationToken token, UniTask finalize)
    {
        await finalize;

        // listens for pings to keep the download alive if it takes too long.
        NetTask pingListenerTask = Ping.Listen(15000);

#if SERVER
        RequestResponse sendStartResponse = await SendStart.RequestAck(Connection, Transmission.WriteStart);
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

            if (!ReferenceEquals(Connection, highSpeedConnection))
                await Send(token, UniTask.CompletedTask, true);

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
                    Logger.DevkitServer.LogDebug(Transmission.LogSource, "Received keep-alive from client while downloading, refreshed timeout.");
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

        if (!ReferenceEquals(Connection, highSpeedConnection))
            await Send(token, UniTask.CompletedTask, true);

        return false;

    }
    private async UniTask<bool> LowSpeedSend(CancellationToken token, UniTask finalize)
    {
        bool isLocalConnection = false;

#if SERVER
        string? ip = Connection.GetAddressString(false);
#else
        string? ip = Provider.isConnected ? Provider.CurrentServerConnectParameters.address.ToString() : "127.0.0.1";
#endif

        if (ip != null && ip.Equals("127.0.0.1", StringComparison.Ordinal))
            isLocalConnection = true;

        float averagePing = await PingLowSpeed(token); // todo isLocalConnection ? -1f : await PingLowSpeed(token);

        await finalize;
        await UniTask.SwitchToMainThread(token);

#if SERVER
        RequestResponse sendStartResponse = await SendStart.RequestAck(Connection, Transmission.WriteStart);
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

            Connection.Send(dataBuffer, false, offset: 0, count: headerSize + packetSize);

            // run checkups to see if we need to slow down our send interval
            if (packetIndex - 1 == nextCheckupPacketIndex)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(Math.Min(maxPacketDelay, packetDelay * 1.5f)), true, cancellationToken: token);

                RequestResponse checkupResponse = default;
                for (int retry = 0; retry < 2; ++retry)
                {
#if SERVER
                    checkupResponse = await SendSlowCheckup.RequestAck(Connection, Transmission.TransmissionId, lastCheckupPacketIndex, packetIndex - 1, timeoutMs: 15000);
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
                    if (!Connection.IsConnected())
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
                        Logger.DevkitServer.LogDebug(Transmission.LogSource, $"Packet delay decreased from {old.Format()} -> {packetDelay.Format()}.");
                    }
                    if (misses > 0)
                    {
                        float old = packetDelay;
                        packetDelay = Math.Min(1f, packetDelay / ((float)misses / ((packetIndex - lastCheckupPacketIndex) * 2)));
                        Logger.DevkitServer.LogDebug(Transmission.LogSource, $"Packet delay increasesd from {old.Format()} -> {packetDelay.Format()} after missing {misses.Format()} / {(packetIndex - lastCheckupPacketIndex).Format()} packet(s).");
                    }

                    nextCheckupPacketIndex += firstCheckupPosition;
                    lastCheckupPacketIndex = packetIndex;
                }
            }

            await UniTask.Delay(TimeSpan.FromSeconds(packetDelay), true, cancellationToken: token);

#if SERVER
            if (Connection.IsConnected())
                continue;

            Logger.DevkitServer.LogInfo(Transmission.LogSource, $"User disconnected at packet {packetIndex.Format()} / {packetCount.Format()} before finalizing transmission.", ConsoleColor.DarkCyan);
            await Transmission.Cancel(token);
            return false;
#endif
        }

        await UniTask.Delay(TimeSpan.FromSeconds(0.25), true, cancellationToken: token);

#if SERVER
        if (!Connection.IsConnected())
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
            RequestResponse sendEndResponse = await SendSlowEnd.Request(SendSlowMissedPackets, Connection, Transmission.WriteEnd);
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
                    Connection.Send(dataBuffer, true, count: headerSize + packetSize);
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
    private async UniTask<float> PingLowSpeed(CancellationToken token)
    {
        const int pingCt = 5;
        const int pingSpacingMs = 500;

        float avgPing = 0f;
        for (int i = 0; i < pingCt; ++i)
        {
            float start = CachedTime.RealtimeSinceStartup;
#if SERVER
            RequestResponse response = await Ping.RequestAck(Connection, 2000);
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
            if (Connection.IsConnected())
                continue;

            Logger.DevkitServer.LogInfo(Transmission.LogSource, $"User disconected during at ping check #{(i + 1).Format()}.", ConsoleColor.DarkCyan);
            return -1f;
#endif
        }

        avgPing /= pingCt;

        Logger.DevkitServer.LogDebug(Transmission.LogSource, $"Average ping: {avgPing * 1000:F2} ms (n = {pingCt.Format()}, t = {pingSpacingMs.Format("0.##")} ms).");
        return avgPing;
    }

    [UsedImplicitly]
    private void HandleSlowPacket(in MessageContext ctx, ByteReader reader)
    {
        int packetIndex = reader.ReadInt32();
        if (_pendingSlowPacketCount <= 0)
        {
            Logger.DevkitServer.LogError(Transmission.LogSource, "Received data before a start level message.");
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
        reader.Skip(packetLength);
        if (_initialMissingSlowPackets > 0)
        {
            if (_missingSlowPackets > 0)
                --_missingSlowPackets;
            Logger.DevkitServer.LogDebug(Transmission.LogSource, $"Recovered ({FormattingUtil.FormatCapacity(packetLength, colorize: true)}) (packet #{(packetIndex + 1).Format()}).", ConsoleColor.DarkCyan);
        }
        else
        {
            Logger.DevkitServer.LogDebug(Transmission.LogSource, $"Received ({FormattingUtil.FormatCapacity(packetLength, colorize: true)}) (packet #{(packetIndex + 1).Format()}).", ConsoleColor.DarkCyan);
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

            Logger.DevkitServer.LogDebug(Transmission.LogSource, $"All packets missing: * / {_pendingSlowPacketCount.Format()}.");

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
                    Logger.DevkitServer.LogDebug(Transmission.LogSource, $"Packet missing: {(i + 1).Format()} / {_pendingSlowPacketCount.Format()}.");
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
            RequestResponse sendEndResponse = await SendSlowEnd.Request(SendSlowMissedPackets, Connection, Transmission.WriteEndCancelled);
#else
            RequestResponse sendEndResponse = await SendSlowEnd.Request(SendSlowMissedPackets, Transmission.WriteEndCancelled);
#endif
            return sendEndResponse.Responded && sendEndResponse.TryGetParameter(0, out int[] arr) && arr.Length == 0;
        }
        else
        {
#if SERVER
            RequestResponse sendEndResponse = await SendSlowEnd.RequestAck(Connection, Transmission.WriteEndCancelled);
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


        Logger.DevkitServer.LogDebug(Transmission.LogSource, $"Received checkup: {checkupStartIndex.Format()} -> {checkupEndIndex.Format()}.");
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

#if CLIENT
        if (_connectionSubbed != null)
            _connectionSubbed.BufferProgressUpdated -= OnHighSpeedProgressUpdate;
#endif

        ActiveMessages.Remove(Transmission.TransmissionId, out _);

        Logger.DevkitServer.LogDebug(Transmission.LogSource, "Disposed.");
    }

    #region Receivers
    [NetCall(NetCallSource.FromEither, DevkitServerNetCall.Ping)]
    private static void ReceiveSlowPing(MessageContext ctx)
    {
        ctx.Acknowledge();
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
            Logger.DevkitServer.LogDebug(transmission.LogSource, "== Large Message Transmission ==");
            Logger.DevkitServer.LogDebug(transmission.LogSource, $"  ID: {transmission.TransmissionId.Format()}");
            Logger.DevkitServer.LogDebug(transmission.LogSource, $"  Is Server: {transmission.Comms.IsServer.Format()}");
            Logger.DevkitServer.LogDebug(transmission.LogSource, $"  Was Cancelled: {transmission.WasCancelled.Format()}");
            Logger.DevkitServer.LogDebug(transmission.LogSource, $"  Allow; High Speed: {transmission.AllowHighSpeed.Format()}, Compression: {transmission.AllowCompression.Format()}");
            Logger.DevkitServer.LogDebug(transmission.LogSource, $"  Connection: {transmission.Connection.Format()}");
            Logger.DevkitServer.LogDebug(transmission.LogSource, $"  Compressed: {transmission.IsCompressed.Format()}, High Speed: {transmission.IsHighSpeed.Format()}");
            Logger.DevkitServer.LogDebug(transmission.LogSource, $"  Original Size: {transmission.OriginalSize.Format()} B, Final Size: {transmission.FinalSize.Format()} B");
            Logger.DevkitServer.LogDebug(transmission.LogSource, $"  Bandwidth: {transmission.Bandwidth.Format()} B");
            Logger.DevkitServer.LogDebug(transmission.LogSource, $"  Handler type: {transmission.HandlerType.Format()}");
            Logger.DevkitServer.LogDebug(transmission.LogSource, $"   Projected packet count: {transmission.LowSpeedPacketCount.Format()}");
            Logger.DevkitServer.LogDebug(transmission.LogSource, $"   Flags: {("0b" + Convert.ToString(transmission.Flags, 2)).Colorize(FormattingUtil.NumberColor)}");

            if (transmission.Handler != null)
            {
                Logger.DevkitServer.LogDebug(transmission.LogSource, $"Handler ({transmission.Handler.GetType().Format()}: {Environment.NewLine}" +
                                                                     JsonSerializer.Serialize(transmission.Handler, transmission.HandlerType,
                                                                         DevkitServerConfig.SerializerSettings));
            }

            Logger.DevkitServer.LogDebug(string.Empty, string.Empty);
            Logger.DevkitServer.LogDebug(string.Empty, string.Empty);
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
}
