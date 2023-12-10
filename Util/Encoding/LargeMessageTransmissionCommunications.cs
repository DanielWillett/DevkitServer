using Cysharp.Threading.Tasks;
using DevkitServer.Configuration;
using DevkitServer.Multiplayer.Networking;
using System.Collections.Concurrent;
using System.Text.Json;
using DevkitServer.API.Multiplayer;

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
            Logger.LogDebug($"[{Transmission.LogSource}] Registered transmission as server.");
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
        try
        {
            Transmission.Handler.IsDirty = true;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to set IsDirty = true on handler: {Transmission.Handler.GetType().Format()}.", method: Transmission.LogSource);
            Logger.LogError(ex, method: Transmission.LogSource);
        }
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
            Logger.LogError($"Failed to set IsDirty = true on handler: {Transmission.Handler.GetType().Format()}.", method: Transmission.LogSource);
            Logger.LogError(ex, method: Transmission.LogSource);
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

        Logger.LogDebug(lowSpeedUpload
            ? $"[{Transmission.LogSource}] Using low-speed (Steamworks) upload option."
            : $"[{Transmission.LogSource}] Using high-speed (TCP) upload option.");

        float startTime = CachedTime.RealtimeSinceStartup;

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
            }
            else
            {
                if (lowSpeedUpload)
                    await LowSpeedSend(token, finalize);
                else if (!await HighSpeedDownload(highSpeedConnection!, token, finalize))
                    return false;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Error uploading connection.", method: Transmission.LogSource);
            Logger.LogError(ex, method: Transmission.LogSource);

            if (highSpeedConnection != null)
            {
#if SERVER
                HighSpeedNetFactory.TakeConnection((HighSpeedConnection)Connection);
#else
                HighSpeedNetFactory.TakeConnection();
#endif
            }
        }
        Logger.LogInfo($"[{Transmission.LogSource}] Sent data ({Transmission.OriginalSize.Format()} B -> {Transmission.FinalSize.Format()} B) in {(CachedTime.RealtimeSinceStartup - startTime).Format("F2")} seconds.", ConsoleColor.DarkCyan);

        return true;
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
        if (!sendStartResponse.Responded || sendStartResponse.ErrorCode is not (int)StandardErrorCode.Success)
        {
            Logger.LogWarning("Failed to initialize a high-speed connection.", ConsoleColor.DarkCyan, method: Transmission.LogSource);
#if SERVER
            HighSpeedNetFactory.ReleaseConnection(highSpeedConnection);
#else
            HighSpeedNetFactory.ReleaseConnection();
#endif

            if (!ReferenceEquals(Connection, highSpeedConnection))
                await Send(token, UniTask.CompletedTask, true);

            return false;
        }

        Logger.LogInfo($"[{Transmission.LogSource}] Ready to upload to high-speed connection.", ConsoleColor.DarkCyan);

        NetTask uploadTask = SendFullData.RequestAck(highSpeedConnection, WriteFullData, 20000);
        while (!uploadTask.IsCompleted)
        {
            if (pingListenerTask.IsCompleted)
            {
                if (pingListenerTask.Parameters.Responded)
                {
                    uploadTask.KeepAlive();
                    Logger.LogDebug($"[{Transmission.LogSource}] Received keep-alive from client while downloading, refreshed timeout.");
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


        Logger.LogWarning($"Failed to send {DevkitServerUtility.FormatBytes(Transmission.FinalContent.Count)} of data over a high-speed connection, trying low speed.",
            ConsoleColor.DarkCyan, method: Transmission.LogSource);

        if (!ReferenceEquals(Connection, highSpeedConnection))
            await Send(token, UniTask.CompletedTask, true);

        return false;

    }
    private async UniTask<bool> LowSpeedSend(CancellationToken token, UniTask finalize)
    {
        const byte packetVersion = 0;

        bool isLocalConnection = false;

#if SERVER
        string? ip = Connection.GetAddressString(false);
#else
        string? ip = Provider.isConnected ? Parser.getIPFromUInt32(Provider.currentServerInfo.ip) : "127.0.0.1";
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

        if (!sendStartResponse.Responded || sendStartResponse.ErrorCode is not (int)StandardErrorCode.Success)
        {
            Logger.LogWarning("Failed to start sending data.", ConsoleColor.DarkCyan, method: Transmission.LogSource);
            return false;
        }

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

        byte[] dataBuffer = new byte[MessageOverhead.MaximumSize + sizeof(int) + 17 + bandwidth];

        int currentDataIndex = 0;

        int packetIndex = 0;
        while (true)
        {
            int packetSize = Math.Min(bandwidth, Transmission.FinalContent.Count - currentDataIndex);
            if (packetSize <= 0)
                break;

            overhead = new MessageOverhead(MessageFlags.None, (ushort)DevkitServerNetCall.SendLargeTransmissionPacket, sizeof(int) + 17 + packetSize);

            int headerSize = overhead.Length + sizeof(int) + 17;

            Buffer.BlockCopy(Transmission.FinalContent.Array!, Transmission.FinalContent.Offset + currentDataIndex, dataBuffer, headerSize, packetSize);
            overhead.GetBytes(dataBuffer, 0);
            UnsafeBitConverter.GetBytes(dataBuffer, Transmission.TransmissionId, overhead.Length);
            dataBuffer[overhead.Length + 16] = packetVersion;
            UnsafeBitConverter.GetBytes(dataBuffer, packetIndex, overhead.Length + 17);

            ++packetIndex;
            currentDataIndex += packetSize;

            Connection.Send(dataBuffer, true, offset: 0, count: headerSize + packetSize);
            Logger.LogDebug($"[{Transmission.LogSource}] Sent packet {packetIndex}/{packetCount}: packetSize: {packetSize}, headerSize: {headerSize} transmission: {Transmission.TransmissionId}.");
            Logger.LogDebug($"[{Transmission.LogSource}] Header:{Environment.NewLine}{FormattingUtil.GetBytesHex(dataBuffer, 16, 0, headerSize, formatMessageOverhead: true)}");

            // run checkups to see if we need to slow down our send interval
            if (packetIndex - 1 == nextCheckupPacketIndex)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(Math.Min(maxPacketDelay, packetDelay * 1.5f)), true, cancellationToken: token);

                RequestResponse checkupResponse = default;
                for (int retry = 0; retry < 2; ++retry)
                {
#if SERVER
                    checkupResponse = await SendSlowCheckup.RequestAck(Connection, Transmission.TransmissionId, lastCheckupPacketIndex, packetIndex - 1);
#else
                    checkupResponse = await SendSlowCheckup.RequestAck(Transmission.TransmissionId, lastCheckupPacketIndex, packetIndex - 1);
#endif
                    await UniTask.SwitchToMainThread(token);

                    if (checkupResponse is { Responded: true, ErrorCode: not null })
                        break;

                    if (retry > 0)
                    {
                        Logger.LogInfo($"[{Transmission.LogSource}] User failed to respond to checkup at packet {packetIndex.Format()} / {packetCount.Format()}.", ConsoleColor.DarkCyan);
                        await Transmission.Cancel(token);
                        return false;
                    }

                    await UniTask.Delay(TimeSpan.FromSeconds(0.5), true, cancellationToken: token);
                }

                if (!checkupResponse.Responded)
                {
                    Logger.LogInfo($"[{Transmission.LogSource}] User didn't respond to checkup at packet {packetIndex.Format()} / {packetCount.Format()}.", ConsoleColor.DarkCyan);
                    await Transmission.Cancel(token);
                    return false;
                }

                if (packetDelay < maxPacketDelay)
                {
                    int misses = checkupResponse.ErrorCode!.Value;
                    if (misses < 0)
                    {
                        Logger.LogInfo($"[{Transmission.LogSource}] User errored during checkup at packet {packetIndex.Format()} / {packetCount.Format()}.", ConsoleColor.DarkCyan);
                        await Transmission.Cancel(token);
                        return false;
                    }

                    if (misses == 0 && packetDelay > 0.06f)
                    {
                        float old = packetDelay;
                        packetDelay *= 0.75f;
                        Logger.LogDebug($"[{Transmission.LogSource}] Packet delay decreased from {old.Format()} -> {packetDelay.Format()}.");
                    }
                    if (misses > 0)
                    {
                        float old = packetDelay;
                        packetDelay = Math.Min(1f, packetDelay / ((float)misses / ((packetIndex - lastCheckupPacketIndex) * 2)));
                        Logger.LogDebug($"[{Transmission.LogSource}] Packet delay increasesd from {old.Format()} -> {packetDelay.Format()} after missing {misses.Format()} / {(packetIndex - lastCheckupPacketIndex).Format()} packet(s).");
                    }

                    nextCheckupPacketIndex += firstCheckupPosition;
                    lastCheckupPacketIndex = packetIndex;
                }
            }

            await UniTask.Delay(TimeSpan.FromSeconds(packetDelay), true, cancellationToken: token);

#if SERVER
            if (Connection.IsConnected())
                continue;

            Logger.LogInfo($"[{Transmission.LogSource}] User disconnected at packet {packetIndex.Format()} / {packetCount.Format()}.", ConsoleColor.DarkCyan);
            await Transmission.Cancel(token);
            return false;
#endif
        }

        await UniTask.Delay(TimeSpan.FromSeconds(0.25), true, cancellationToken: token);

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
                Logger.LogInfo($"[{Transmission.LogSource}] User disconnected before finalizing transmission.", ConsoleColor.DarkCyan);
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
                    sizeof(int) + 17 + packetSize, slowPacketListener.RequestId);

                int headerSize = overhead.Length + sizeof(int) + 17;

                Buffer.BlockCopy(Transmission.FinalContent.Array!, Transmission.FinalContent.Offset + currentDataIndex, dataBuffer, headerSize, packetSize);

                overhead.GetBytes(dataBuffer, 0);
                UnsafeBitConverter.GetBytes(dataBuffer, Transmission.TransmissionId, overhead.Length);
                dataBuffer[overhead.Length + 16] = packetVersion;
                UnsafeBitConverter.GetBytes(dataBuffer, packetIndex, overhead.Length + 17);

                for (int retry = 0; retry < 2; ++retry)
                {
                    Connection.Send(dataBuffer, true, count: headerSize + packetSize);
                    RequestResponse response = await slowPacketListener;
                    await UniTask.SwitchToMainThread(token);

                    if (response.ErrorCode is (int)StandardErrorCode.Success)
                        break;
                    
                    if (response.ErrorCode.HasValue)
                        Logger.LogWarning($"Recovery packet failed with error code: {response.ErrorCode.Value.Format("X8")}.", method: Transmission.LogSource);

                    if (i == 0)
                    {
                        Logger.LogWarning($"Retrying recovery packet {(packetIndex + 1).Format()}.", method: Transmission.LogSource);
                        continue;
                    }

                    Logger.LogError($"Failed retry of sending recovery packet {(packetIndex + 1).Format()}.", method: Transmission.LogSource);
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
                Logger.LogInfo($"[{Transmission.LogSource}] User failed to respond to ping check #{(i + 1).Format()}.", ConsoleColor.DarkCyan);
                return -1f;
            }

            avgPing += CachedTime.RealtimeSinceStartup - start;
            await UniTask.Delay(TimeSpan.FromMilliseconds(pingSpacingMs), ignoreTimeScale: true, cancellationToken: token);

#if SERVER
            if (Connection.IsConnected())
                continue;

            Logger.LogInfo($"[{Transmission.LogSource}] User disconected during at ping check #{(i + 1).Format()}.", ConsoleColor.DarkCyan);
            return -1f;
#endif
        }

        avgPing /= pingCt;

        Logger.LogDebug($"[{Transmission.LogSource}] Average ping: {avgPing * 1000:F2} ms (n = {pingCt.Format()}, t = {pingSpacingMs.Format("0.##")} ms).");
        return avgPing;
    }

    [UsedImplicitly]
    private void HandleSlowPacket(in MessageContext ctx, ByteReader reader, byte verison)
    {
        int packetIndex = reader.ReadInt32();
        if (_pendingSlowPacketCount <= 0)
        {
            Logger.LogError($"[{Transmission.LogSource}] Received data before a start level message.");
            ctx.Acknowledge(StandardErrorCode.AccessViolation);
            return;
        }

        if (packetIndex < 0 || packetIndex >= _pendingSlowPacketCount)
        {
            Logger.LogError($"[{Transmission.LogSource}] Received data packet out of range of expected ({(packetIndex + 1).Format()} / {_pendingSlowPacketCount.Format()}), ignoring.");
            ctx.Acknowledge(StandardErrorCode.InvalidData);
            return;
        }

        _clientSlowReceiveMask ??= new BitArray(_pendingSlowPacketCount);
        _clientSlowReceiveBuffer ??= new byte[Transmission.FinalSize];

        if (_clientSlowReceiveMask[packetIndex])
        {
            Logger.LogError($"[{Transmission.LogSource}] Packet already downloaded ({(packetIndex + 1).Format()} / {_pendingSlowPacketCount.Format()}), replacing.");
        }
        else
        {
            _clientSlowReceiveMask[packetIndex] = true;
        }

        int dataIndex = packetIndex * Transmission.Bandwidth;
        int packetLength = Math.Min(Transmission.Bandwidth, reader.InternalBuffer!.Length - reader.Position);
        
        Buffer.BlockCopy(reader.InternalBuffer!, reader.Position, _clientSlowReceiveBuffer, dataIndex, packetLength);
        reader.Skip(packetLength);
        if (_initialMissingSlowPackets > 0)
        {
            if (_missingSlowPackets > 0)
                --_missingSlowPackets;
            Logger.LogDebug($"[{Transmission.LogSource}] Recovered ({DevkitServerUtility.FormatBytes(packetLength)}) (packet #{(packetIndex + 1).Format()}).", ConsoleColor.DarkCyan);
        }
        else
        {
            Logger.LogDebug($"[{Transmission.LogSource}] Received ({DevkitServerUtility.FormatBytes(packetLength)}) (packet #{(packetIndex + 1).Format()}).", ConsoleColor.DarkCyan);
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
                Logger.LogError($"Failed to set IsDirty = true on handler: {Transmission.Handler.GetType().Format()}.", method: Transmission.LogSource);
                Logger.LogError(ex, method: Transmission.LogSource);
            }
        }

        ctx.Acknowledge(StandardErrorCode.Success);
    }

    [UsedImplicitly]
    private void HandleSlowEnd(in MessageContext ctx, ByteReader reader, byte verison)
    {
        bool cancelled = reader.ReadBool();

        if (IsServer)
        {
            if (!cancelled)
            {
                Logger.LogWarning($"Received server transmission: {Transmission.TransmissionId.Format()}, but expected client.", method: Transmission.LogSource);
                return;
            }

            ctx.Acknowledge();
            Logger.LogInfo($"[{Transmission.LogSource}] Cancelled level load.");
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
                    Logger.LogWarning($"Received cancel message when invalid for transmission: {Transmission.TransmissionId.Format()}.", method: Transmission.LogSource);
                    ctx2.Acknowledge(StandardErrorCode.NotSupported);
                    return;
                }

                ctx2.Acknowledge(val ? StandardErrorCode.Success : StandardErrorCode.GenericError);
            });
            if (Transmission.Handler == null)
                return;

            try
            {
                Transmission.Handler.IsDirty = true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to set IsDirty = true on handler: {Transmission.Handler.GetType().Format()}.", method: Transmission.LogSource);
                Logger.LogError(ex, method: Transmission.LogSource);
            }
            try
            {
                Transmission.Handler.OnFinished(LargeMessageTransmissionStatus.Cancelled);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to run OnFinished(Cancelled) on handler: {Transmission.Handler.GetType().Format()}.", method: Transmission.LogSource);
                Logger.LogError(ex, method: Transmission.LogSource);
            }
            return;
        }

        if (cancelled)
        {
            ctx.Reply(SendSlowMissedPackets, Array.Empty<int>());
            Logger.LogInfo($"[{Transmission.LogSource}] Cancelled level load.");
            Transmission.Dispose();
            return;
        }

        int[]? missingPackets = null;
        if (_clientSlowReceiveMask == null)
        {
            missingPackets = new int[_pendingSlowPacketCount];

            Logger.LogDebug($"[{Transmission.LogSource}] All packets missing: * / {_pendingSlowPacketCount.Format()}.");

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
                    Logger.LogDebug($"[{Transmission.LogSource}] Packet missing: {(i + 1).Format()} / {_pendingSlowPacketCount.Format()}.");
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
                    Logger.LogError($"Failed to set IsDirty = true on handler: {Transmission.Handler.GetType().Format()}.", method: Transmission.LogSource);
                    Logger.LogError(ex, method: Transmission.LogSource);
                }
            }

            ctx.Reply(SendSlowMissedPackets, missingPackets);
            Logger.LogWarning($"[{Transmission.LogSource}] Missing {missingPackets.Length.Format()} / {_pendingSlowPacketCount.Format()} ({((float)missingPackets.Length / _pendingSlowPacketCount).Format("P1")}).");
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
                Logger.LogError($"Failed to set IsDirty = true on handler: {Transmission.Handler.GetType().Format()}.", method: Transmission.LogSource);
                Logger.LogError(ex, method: Transmission.LogSource);
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

            return sendEndResponse.ErrorCode is (int)StandardErrorCode.Success;
        }
    }

    [UsedImplicitly]
    private int HandleSlowCheckup(in MessageContext ctx, int checkupStartIndex, int checkupEndIndex)
    {
        if (_clientSlowReceiveMask == null)
            return checkupEndIndex - checkupStartIndex;


        Logger.LogDebug($"[{Transmission.LogSource}] Received checkup: {checkupStartIndex.Format()} -> {checkupEndIndex.Format()}.");
        int missing = 0;
        for (int i = checkupStartIndex; i <= checkupEndIndex; ++i)
        {
            if (!_clientSlowReceiveMask[checkupStartIndex])
                ++missing;
        }

        return missing;
    }

    [UsedImplicitly]
    private void HandleFullData(in MessageContext ctx, ByteReader reader, byte verison)
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
                Logger.LogError($"Failed to set IsDirty = true on handler: {Transmission.Handler.GetType().Format()}.", method: Transmission.LogSource);
                Logger.LogError(ex, method: Transmission.LogSource);
            }
        }

        Transmission.OnFinalContentCompleted();
    }

    private void WriteFullData(ByteWriter writer)
    {
        const byte v = 0;

        writer.Write(Transmission.TransmissionId);
        writer.Write(v);

        ArraySegment<byte> content = Transmission.FinalContent;
        writer.Write(content.Count);
        writer.WriteBlock(content.Array!, content.Offset, content.Count);
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

        Logger.LogDebug($"[{Transmission.LogSource}] Disposed.");
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
            Logger.LogWarning($"Received duplicate transmission: {transmission.TransmissionId.Format()}, disposing.", method: transmission.LogSource);
            old.Dispose();
        }

        ActiveMessages[transmission.TransmissionId] = transmission;
        Logger.LogDebug($"[{transmission.LogSource}] Started transmission as client.");
        transmission.Handler?.OnStart();
        ctx.Acknowledge(StandardErrorCode.Success);
    }

    [NetCall(NetCallSource.FromEither, DevkitServerNetCall.SendLargeTransmissionPacket)]
    private static void ReceiveSlowPacket(MessageContext ctx, ByteReader reader)
    {
        Guid guid = reader.ReadGuid();
        byte v = reader.ReadUInt8();

        if (!ActiveMessages.TryGetValue(guid, out LargeMessageTransmission transmission))
        {
            Logger.LogWarning($"Received unknown transmission: {guid.Format()}.", method: "LARGE MSG");
            ctx.Acknowledge(StandardErrorCode.NotFound);
            return;
        }

        if (transmission.Comms.IsServer)
        {
            Logger.LogWarning($"Received server transmission: {guid.Format()}, but expected client.", method: transmission.LogSource);
            ctx.Acknowledge(StandardErrorCode.AccessViolation);
            return;
        }

        transmission.Comms.HandleSlowPacket(in ctx, reader, v);
    }

    [NetCall(NetCallSource.FromEither, DevkitServerNetCall.SendEndLargeTransmission)]
    private static void ReceiveSlowEnd(MessageContext ctx, ByteReader reader)
    {
        Guid guid = reader.ReadGuid();
        byte v = reader.ReadUInt8();

        if (!ActiveMessages.TryGetValue(guid, out LargeMessageTransmission transmission))
        {
            Logger.LogWarning($"Received unknown transmission: {guid.Format()}.", method: "LARGE MSG");
            return;
        }

        transmission.Comms.HandleSlowEnd(in ctx, reader, v);
    }

    [NetCall(NetCallSource.FromEither, DevkitServerNetCall.SendLargeTransmissionCheckup)]
    private static void ReceiveSlowCheckup(MessageContext ctx, Guid guid, int start, int end)
    {
        if (!ActiveMessages.TryGetValue(guid, out LargeMessageTransmission transmission))
        {
            Logger.LogWarning($"Received unknown transmission: {guid.Format()}.", method: "LARGE MSG");
            ctx.Acknowledge(-1);
            return;
        }

        if (transmission.Comms.IsServer)
        {
            Logger.LogWarning($"Received server transmission: {guid.Format()}, but expected client.", method: transmission.LogSource);
            ctx.Acknowledge(-2);
            return;
        }

        ctx.Acknowledge(transmission.Comms.HandleSlowCheckup(in ctx, start, end));
    }

    [NetCall(NetCallSource.FromEither, (ushort)HighSpeedNetCall.SendFullLargeTransmission, HighSpeed = true)]
    private static void ReceiveFullData(MessageContext ctx, ByteReader reader)
    {
        Guid guid = reader.ReadGuid();
        byte v = reader.ReadUInt8();

        if (!ActiveMessages.TryGetValue(guid, out LargeMessageTransmission transmission))
        {
            Logger.LogWarning($"Received unknown transmission: {guid.Format()}.", method: "LARGE MSG");
            ctx.Acknowledge(StandardErrorCode.NotFound);
            return;
        }

        if (transmission.Comms.IsServer)
        {
            Logger.LogWarning($"Received server transmission: {guid.Format()}, but expected client.", method: transmission.LogSource);
            ctx.Acknowledge(StandardErrorCode.AccessViolation);
            return;
        }

        transmission.Comms.HandleFullData(in ctx, reader, v);
    }
    #endregion

    internal static void DumpDebug()
    {
        foreach (LargeMessageTransmission transmission in ActiveMessages.Values)
        {
            Logger.LogDebug("== Large Message Transmission ==");
            Logger.LogDebug($"  ID: {transmission.TransmissionId.Format()}");
            Logger.LogDebug($"  Is Server: {transmission.Comms.IsServer.Format()}");
            Logger.LogDebug($"  Allow; High Speed: {transmission.AllowHighSpeed.Format()}, Compression: {transmission.AllowCompression.Format()}");
            Logger.LogDebug($"  Connection: {transmission.Connection.Format()}");
            Logger.LogDebug($"  Compressed: {transmission.IsCompressed.Format()}, High Speed: {transmission.IsHighSpeed.Format()}");
            Logger.LogDebug($"  Original Size: {transmission.OriginalSize.Format()} B, Final Size: {transmission.FinalSize.Format()} B");
            Logger.LogDebug($"  Bandwidth: {transmission.Bandwidth.Format()} B");
            Logger.LogDebug($"  Handler type: {transmission.HandlerType.Format()}");
            Logger.LogDebug($"   Projected packet count: {transmission.LowSpeedPacketCount.Format()}");
            Logger.LogDebug($"   Flags: 0b{Convert.ToString(transmission.Flags, 2)}");
            if (transmission.Handler != null)
            {
                Logger.LogDebug("Handler: " + Environment.NewLine + JsonSerializer.Serialize(transmission.Handler, transmission.HandlerType, DevkitServerConfig.SerializerSettings));
            }

            Logger.LogDebug(string.Empty);
            Logger.LogDebug(string.Empty);
        }
    }
}
