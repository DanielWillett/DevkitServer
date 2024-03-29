using Cysharp.Threading.Tasks;
using DevkitServer.API.Multiplayer;
using DevkitServer.Multiplayer.Networking;
using System.IO.Compression;
using CompressionLevel = System.IO.Compression.CompressionLevel;
using DeflateStream = System.IO.Compression.DeflateStream;

namespace DevkitServer.Util.Encoding;

/// <summary>
/// Represents a large transmission that has to be split over multiple packets on low-speed connections. Implements <see cref="IDisposable"/>.
/// </summary>
/// <remarks>Uses high-speed unless specified otherwise.</remarks>
public class LargeMessageTransmission : IDisposable
{
    private volatile int _disposed;
    internal const ushort GlobalProtocolVersion = 1;
    private bool _hasSent;
    private bool _hasSentData;
    private bool _hasFullSent;
    private bool _isCancelled;
    private readonly CancellationTokenSource _tknSource;
    public const int HeaderCapacity = 64;
    public const int FooterCapacity = 16;
    public const int PacketCapacity = ushort.MaxValue;

    internal readonly LargeMessageTransmissionCommunications Comms;
    private Type? _handlerType;
    private bool _isCancellable;

    /// <summary>
    /// Unique ID of the transmission.
    /// </summary>
    public Guid TransmissionId { get; }

    /// <summary>
    /// If using a TCP connection is allowed.
    /// </summary>
    public bool AllowHighSpeed { get; set; } = true;

    /// <summary>
    /// If compression using a <see cref="DeflateStream"/> is allowed.
    /// </summary>
    public bool AllowCompression { get; set; } = true;

    /// <summary>
    /// How sent or received data should be logged as binary. Must be set server-side (like in the handler) to log received data.
    /// </summary>
    public BinaryStringFormat LoggingType { get; set; } = BinaryStringFormat.NoLogging;

    /// <summary>
    /// Is this a cancellable transmission?
    /// </summary>
    /// <exception cref="InvalidOperationException">Can not make a transmission with more than one connection cancellable (server build only).</exception>
    public bool IsCancellable
    {
        get => _isCancellable;
        set
        {
#if SERVER
            if (value && Connections.Count > 1)
                throw new InvalidOperationException("Can not make a transmission with more than one connection cancellable.");
#endif
            _isCancellable = value;
        }
    }

    /// <summary>
    /// Connection to use for sending data.
    /// </summary>
#if SERVER
    public IReadOnlyList<ITransportConnection> Connections { get; }
#elif CLIENT
    public IClientTransport Connection { get; }
#endif

    /// <summary>
    /// Original content sent.
    /// </summary>
    public ArraySegment<byte> Content { get; internal set; }

    /// <summary>
    /// Finalized (usually compressed) content.
    /// </summary>
    public ArraySegment<byte> FinalContent { get; internal set; }

    /// <summary>
    /// Log source to use for debug and warning logs.
    /// </summary>
    public string LogSource { get; set; } = "LARGE MSG";

    /// <summary>
    /// Was the data compressed? Will be set after beginning sending for server-side.
    /// </summary>
    public bool IsCompressed { get; private set; }

    /// <summary>
    /// Was the data sent over a high-speed (TCP) connection? Will be set after beginning sending for server-side.
    /// </summary>
    public bool IsHighSpeed { get; internal set; }

    /// <summary>
    /// Was the transmission cancelled by either this party or the other one.
    /// </summary>
    public bool WasCancelled { get; internal set; }

    /// <summary>
    /// Expected size of <see cref="Content"/>.
    /// </summary>
    public int OriginalSize { get; set; }

    /// <summary>
    /// Expected size of <see cref="FinalContent"/>.
    /// </summary>
    public int FinalSize { get; set; }

    /// <summary>
    /// Max packet size for low-speed (Steam Networking) connections.
    /// </summary>
    public int Bandwidth { get; set; }

    /// <summary>
    /// The last reported amount of bytes received by the client.
    /// </summary>
    public int ApproximateBytesReceivedByClient => Comms.ClientHintBytesProcessed;

    /// <summary>
    /// Type to use to handle data on the client.
    /// </summary>
    public Type? HandlerType
    {
        get => _handlerType;
        set
        {
            if (value != null && !typeof(BaseLargeMessageTransmissionClientHandler).IsAssignableFrom(value))
                throw new ArgumentException("Handler must derive from BaseLargeMessageTransmissionClientHandler.", nameof(value));

            _handlerType = value;
        }
    }

    /// <summary>
    /// If this message is being sent instead of received.
    /// </summary>
    public bool IsServer => Comms.IsServer;

    /// <summary>
    /// If the message is at a point where it can be cancelled.
    /// </summary>
    public bool CanCancel => IsCancellable && (!Comms.IsServer || _hasSent) && !_hasFullSent;

    /// <summary>
    /// Client handler to add custom receive event handling.
    /// </summary>
    public BaseLargeMessageTransmissionClientHandler? Handler { get; set; }

    /// <summary>
    /// Cancellation token triggered when the transmission is cancelled or disposed.
    /// </summary>
    public CancellationToken CancellationToken => _tknSource.Token;

    /// <summary>
    /// Protocol version of the other side of the transmission, or -1 if it has not been received yet.
    /// </summary>
    public int ProtocolVersion { get; internal set; }

    internal byte Flags
    {
        get => (byte)(
            (AllowHighSpeed ? 1 : 0) |
            (AllowCompression ? 2 : 0) |
            (IsCompressed ? 4 : 0) |
            (IsHighSpeed ? 8 : 0) |
            (IsCancellable ? 16 : 0));
        set
        {
            AllowHighSpeed = (value & 1) != 0;
            AllowCompression = (value & 2) != 0;
            IsCompressed = (value & 4) != 0;
            IsHighSpeed = (value & 8) != 0;
            IsCancellable = (value & 16) != 0;
        }
    }

    internal int LowSpeedPacketCount => (int)Math.Ceiling(FinalSize / (double)Bandwidth);

    /// <summary>
    /// Create a new transmission for the given <see cref="ITransportConnection"/>.
    /// </summary>
    public LargeMessageTransmission(
#if SERVER
        IReadOnlyList<ITransportConnection> targetConnections,
#endif
        ArraySegment<byte> content, int bandwidth = NetFactory.MaxPacketSize)
    {
        if (bandwidth is > ushort.MaxValue or <= 0)
            throw new ArgumentOutOfRangeException(nameof(bandwidth), bandwidth, $"(0, {ushort.MaxValue}]");

#if SERVER
        if (targetConnections.Count == 0)
            throw new ArgumentException("No transport connections provided.", nameof(targetConnections));
#endif

#if SERVER
        // pooled lists will be cleared later
        Connections = targetConnections is PooledTransportConnectionList ? targetConnections.ToList() : targetConnections;
#elif CLIENT
        Connection = NetFactory.GetPlayerTransportConnection();
#endif
        Content = content;
        FinalContent = content;
        TransmissionId = Guid.NewGuid();
        OriginalSize = content.Count;
        Bandwidth = bandwidth;
        _tknSource = new CancellationTokenSource();
        Comms = new LargeMessageTransmissionCommunications(this, true);
        ProtocolVersion = -1;
#if SERVER
        _isCancellable = targetConnections.Count == 1;
#else
        _isCancellable = true;
#endif
    }

    internal LargeMessageTransmission(
#if SERVER
        ITransportConnection sendingConnection,
#endif
        ByteReader reader)
    {
#if SERVER
        Connections = [ sendingConnection ];
#elif CLIENT
        Connection = NetFactory.GetPlayerTransportConnection();
#endif

        ProtocolVersion = reader.ReadUInt16();

        TransmissionId = reader.ReadGuid();
        Flags = reader.ReadUInt8();
        Bandwidth = reader.ReadUInt16();

        LogSource = reader.ReadAsciiSmall();

        OriginalSize = reader.ReadInt32();
        FinalSize = reader.ReadInt32();
        HandlerType = reader.ReadType();

        if (HandlerType != null)
        {
            if (typeof(BaseLargeMessageTransmissionClientHandler).IsAssignableFrom(HandlerType))
            {
                try
                {
                    Handler = (BaseLargeMessageTransmissionClientHandler)Activator.CreateInstance(HandlerType, nonPublic: true);
                    Handler.Transmission = this;
                }
                catch (Exception ex)
                {
                    Logger.DevkitServer.LogError(LogSource, ex, $"Failed to create a large transmission handler for {HandlerType.Format()}.");
                }
            }
            else
            {
                Logger.DevkitServer.LogError(LogSource, $"Failed to create a large transmission handler for {HandlerType.Format()}, " +
                                                      $"must be assignable from {typeof(BaseLargeMessageTransmissionClientHandler).Format()}.");
            }
        }

        _hasSent = true;
        _tknSource = new CancellationTokenSource();
        Comms = new LargeMessageTransmissionCommunications(this, false);

        if (Handler == null) return;

        try
        {
            Handler.IsDirty = true;
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError(LogSource, ex, $"Failed to set IsDirty = true on handler: {Handler.GetType().Format()}.");
        }
    }
    internal void WriteStart(ByteWriter writer)
    {
        writer.Write(GlobalProtocolVersion);

        writer.Write(TransmissionId);
        writer.Write(Flags);
        writer.Write((ushort)Bandwidth);

        writer.WriteAsciiSmall(LogSource);

        writer.Write(OriginalSize);
        writer.Write(FinalSize);
        writer.Write(HandlerType);
        Logger.DevkitServer.LogDebug(LogSource, $"Handler type: {HandlerType.Format()}.");
    }

    internal void WriteEnd(ByteWriter writer, bool cancelled)
    {
        writer.Write(TransmissionId);

        writer.Write(cancelled);
    }
    internal void WriteEnd(ByteWriter writer) => WriteEnd(writer, false);
    internal void WriteEndCancelled(ByteWriter writer) => WriteEnd(writer, true);

    /// <summary>
    /// Cancels the large transmission from either side.
    /// </summary>
    /// <returns><see langword="false"/> if the message has either already sent or already been cancelled, otherwise <see langword="true"/>.</returns>
    /// <exception cref="InvalidOperationException">Message has yet to be sent - OR - <see cref="IsCancellable"/> is <see langword="false"/>.</exception>
    public async UniTask<bool> Cancel(CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();

        if (!IsCancellable)
            throw new InvalidOperationException("This message does not support cancellation.");

        if (token == _tknSource.Token)
            token = default;
        else
            _tknSource.Token.ThrowIfCancellationRequested();

        if (Comms.IsServer)
        {
            if (!_hasSent)
                throw new InvalidOperationException("Can not cancel an unsent message.");

            if (_hasFullSent)
                return false;

            UniTask<bool> task = Comms.Cancel(token);
            WasCancelled = true;
            _tknSource.Cancel();
            return await task;
        }

        if (_hasFullSent)
            return false;

        if (!_hasSentData)
        {
            UniTask<bool> task = Comms.Cancel(token);
            WasCancelled = true;
            _tknSource.Cancel();
            return await task;
        }
        
        _tknSource.Cancel();
        await UniTask.WaitUntil(() => _isCancelled, PlayerLoopTiming.Update, new CancellationTokenSource(250).Token);
        return true;
    }

    /// <summary>
    /// Begins sending the large transmission.
    /// </summary>
    /// <returns><see langword="false"/> if the message sending failed somehow, otherwise <see langword="true"/>.</returns>
    /// <exception cref="InvalidOperationException">Tried to send from the wrong side.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="token"/> is cancelled.</exception>
    /// <exception cref="FormatException"><see cref="Content"/> is <see langword="default"/> or empty.</exception>
#if CLIENT
    public async UniTask<bool> Send(CancellationToken token = default)
#else
    public async UniTask<bool[]> Send(CancellationToken token = default)
#endif
    {
        if (Content.Array == null || Content.Count == 0)
            throw new FormatException("No content supplied to large transaction. Ensure the 'Content' property has a value and a non-zero count.");

        if (LoggingType != BinaryStringFormat.NoLogging)
            PrintLogging();
        
        token = !token.CanBeCanceled ? _tknSource.Token : CancellationTokenSource.CreateLinkedTokenSource(token, _tknSource.Token).Token;
        token.ThrowIfCancellationRequested();

        if (!Comms.IsServer)
            throw new InvalidOperationException("Can not send from the client-side.");

        _hasSent = true;

        if (Handler != null)
        {
            try
            {
                Handler.IsDirty = true;
            }
            catch (Exception ex)
            {
                Logger.DevkitServer.LogError(LogSource, ex, $"Failed to set IsDirty = true on handler: {Handler.GetType().Format()}.");
            }
        }
#if CLIENT
        bool sent;
#else
        bool[] sent;
#endif
        try
        {
            sent = await Comms.Send(token, new LargeMessageTransmissionCommunications.UniTaskWrapper(Finalize(token)));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError(LogSource, ex, "Error sending transmission.");
#if CLIENT
            sent = false;
#else
            sent = new bool[Connections.Count];
#endif
        }

        _hasSentData = true;
        _hasFullSent = true;

        if (Handler == null)
            return sent;

        try
        {
            Handler.IsDirty = true;
        }
        catch (Exception ex)
        {
            Logger.DevkitServer.LogError(LogSource, ex, $"Failed to set IsDirty = true on handler: {Handler.GetType().Format()}.");
        }

        return sent;
    }
    private async UniTask Compress(CancellationToken token = default)
    {
        ArraySegment<byte> source = Content;

        int length;
        byte[] compressedContent;

        using (MemoryStream mem = new MemoryStream(source.Count))
        {
            bool disposed = false;
            DeflateStream stream = new DeflateStream(mem, CompressionLevel.Optimal, leaveOpen: true);
            try
            {
                await stream.WriteAsync(source, token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();

                disposed = true;
                await stream.DisposeAsync(); // flush just doesn't work here for some reason
                token.ThrowIfCancellationRequested();
            }
            finally
            {
                if (!disposed)
                    await stream.DisposeAsync();
            }

            compressedContent = mem.GetBuffer();
            length = (int)mem.Position;
        }

        token.ThrowIfCancellationRequested();

        if (length < source.Count)
        {
            IsCompressed = true;
            FinalContent = new ArraySegment<byte>(compressedContent, 0, length);
            FinalSize = length;
            Logger.DevkitServer.LogInfo(LogSource, $"Compresssed data from {FormattingUtil.FormatCapacity(source.Count, colorize: true)} " +
                                                   $"to {FormattingUtil.FormatCapacity(FinalContent.Count, colorize: true)}.");
        }
        else
        {
            IsCompressed = false;
            FinalContent = source;
            FinalSize = source.Count;
        }

        await UniTask.SwitchToMainThread(token);
    }
    private async UniTask Decompress(CancellationToken token = default)
    {
        ArraySegment<byte> source = FinalContent;
        if (!IsCompressed)
        {
            Content = source;
            return;
        }

        using MemoryStream mem = new MemoryStream(source.Array!, source.Offset, source.Count);

        byte[] buffer = new byte[OriginalSize];

        using DeflateStream stream = new DeflateStream(mem, CompressionMode.Decompress);

        await UniTask.SwitchToThreadPool();

        int count = await stream.ReadAsync(buffer, token).ConfigureAwait(false);
        await stream.FlushAsync(token).ConfigureAwait(false);

        await UniTask.SwitchToMainThread(PlayerLoopTiming.EarlyUpdate, token);
        if (OriginalSize != count)
        {
            Logger.DevkitServer.LogWarning(LogSource, $"Expected data of length {FormattingUtil.FormatCapacity(OriginalSize, colorize: true)} " +
                                                      $"but instead got data of length {FormattingUtil.FormatCapacity(count, colorize: true)}.");
        }

        Content = new ArraySegment<byte>(buffer, 0, count);

        Logger.DevkitServer.LogDebug(LogSource, $"Decompressed content from {FormattingUtil.FormatCapacity(source.Count, colorize: true)} to {FormattingUtil.FormatCapacity(count, colorize: true)}.");

        await UniTask.SwitchToMainThread(token);
    }
    private UniTask Unfinalize(CancellationToken token = default)
    {
        return Decompress(token);
    }
    private UniTask Finalize(CancellationToken token = default)
    {
        return AllowCompression && !IsCompressed ? Compress(token) : UniTask.CompletedTask;
    }
    private void PrintLogging()
    {
        BinaryStringFormat format = LoggingType | BinaryStringFormat.NewLineAtBeginning;

        int len = FormattingUtil.GetBinarySize(Content.Count, format);

        Span<char> data = len > 384 ? new char[len] : stackalloc char[len];
        FormattingUtil.FormatBinary(Content, data, format);

        Logger.DevkitServer.LogInfo(LogSource, data);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        if (Handler != null && !WasCancelled && !Handler.IsFinalized)
        {
            try
            {
                Handler.OnFinished(LargeMessageTransmissionStatus.Failure);
            }
            catch (Exception ex)
            {
                Logger.DevkitServer.LogError(LogSource, ex, $"Failed to run OnFinished(Failure) on handler: {Handler.GetType().Format()}.");
            }
        }

        _tknSource.Cancel();
        _tknSource.Dispose();

        if (Handler is IDisposable handler)
        {
            try
            {
                handler.Dispose();
            }
            catch (Exception ex)
            {
                Logger.DevkitServer.LogError(LogSource, ex, $"Failed to dispose handler: {Handler.GetType().Format()}.");
            }
        }

        Comms.Dispose();

        if (LoggingType != BinaryStringFormat.NoLogging && Content.Array != null && Content.Count != 0)
            PrintLogging();
    }
    internal void OnFinalContentCompleted()
    {
        _hasSentData = true;
        UniTask.Create(async () =>
        {
            try
            {
                _tknSource.Token.ThrowIfCancellationRequested();
                await Unfinalize(_tknSource.Token);
            }
            catch (OperationCanceledException) when (_tknSource.IsCancellationRequested)
            {
                _isCancelled = true;
                Dispose();
                return;
            }

            _hasFullSent = true;
            if (Handler != null)
            {
                Handler.FinalizedTimestamp = DateTime.UtcNow;
                Handler.IsFinalized = true;
                try
                {
                    Handler.IsDirty = true;
                }
                catch (Exception ex)
                {
                    Logger.DevkitServer.LogError(LogSource, ex, $"Failed to set IsDirty = true on handler: {Handler.GetType().Format()}.");
                }
                try
                {
                    Handler.OnFinished(LargeMessageTransmissionStatus.Success);
                }
                catch (Exception ex)
                {
                    Logger.DevkitServer.LogError(LogSource, ex, $"Failed to run OnFinished(Success) on handler: {Handler.GetType().Format()}.");
                }
            }

            Dispose();
        });
    }

    /// <summary>
    /// Returns a copied collection of all messages currently registered where the data is being received from another connection.
    /// </summary>
    /// <remarks>Also see <seealso cref="GetSendingMessages"/>.</remarks>
    public static IReadOnlyList<LargeMessageTransmission> GetReceivingMessages() => LargeMessageTransmissionCommunications.GetReceivingMessages();

    /// <summary>
    /// Returns a copied collection of all messages currently registered where the data is being sent to another connection.
    /// </summary>
    /// <remarks>Also see <seealso cref="GetReceivingMessages"/>.</remarks>
    public static IReadOnlyList<LargeMessageTransmission> GetSendingMessages() => LargeMessageTransmissionCommunications.GetSendingMessages();
}
