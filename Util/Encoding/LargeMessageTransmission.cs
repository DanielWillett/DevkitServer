using Cysharp.Threading.Tasks;
using DevkitServer.API.Multiplayer;
using DevkitServer.Multiplayer.Networking;
using System.IO.Compression;
using CompressionLevel = System.IO.Compression.CompressionLevel;
using DeflateStream = System.IO.Compression.DeflateStream;

namespace DevkitServer.Util.Encoding;
public class LargeMessageTransmission : IDisposable
{
    private volatile int _disposed;
    private const ushort Version = 0;
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
    /// Connection to use for sending data.
    /// </summary>
#if SERVER
    public ITransportConnection Connection { get; }
#elif CLIENT
    public IClientTransport Connection { get; }
#endif

    /// <summary>
    /// Original content sent.
    /// </summary>
    public ArraySegment<byte> Content { get; internal set; }

    /// <summary>
    /// Finalized (compressed) content.
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
    public bool CanCancel => (!Comms.IsServer || _hasSent) && !_hasFullSent;

    /// <summary>
    /// Client handler to add custom receive event handling.
    /// </summary>
    public BaseLargeMessageTransmissionClientHandler? Handler { get; set; }

    /// <summary>
    /// Cancellation token triggered when the transmission is cancelled or disposed.
    /// </summary>
    public CancellationToken CancellationToken => _tknSource.Token;

    internal byte Flags
    {
        get => (byte)(
            (AllowHighSpeed ? 1 : 0) |
            (AllowCompression ? 2 : 0) |
            (IsCompressed ? 4 : 0) |
            (IsHighSpeed ? 8 : 0));
        set
        {
            AllowHighSpeed = (value & 1) != 0;
            AllowCompression = (value & 2) != 0;
            IsCompressed = (value & 4) != 0;
            IsHighSpeed = (value & 8) != 0;
        }
    }

    internal int LowSpeedPacketCount => (int)Math.Ceiling(FinalSize / (double)Bandwidth);

    /// <summary>
    /// Create a new transmission for the given <see cref="ITransportConnection"/>.
    /// </summary>
    public LargeMessageTransmission(
#if SERVER
        ITransportConnection targetConnection,
#endif
        ArraySegment<byte> content, int bandwidth = NetFactory.MaxPacketSize)
    {
        if (bandwidth is > ushort.MaxValue or <= 0)
            throw new ArgumentOutOfRangeException(nameof(bandwidth), bandwidth, $"(0, {ushort.MaxValue}]");

#if SERVER
        Connection = targetConnection;
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
    }

    internal LargeMessageTransmission(
#if SERVER
        ITransportConnection sendingConnection,
#endif
        ByteReader reader)
    {
#if SERVER
        Connection = sendingConnection;
#elif CLIENT
        Connection = NetFactory.GetPlayerTransportConnection();
#endif

        _ = reader.ReadUInt16();

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
                    Handler = (BaseLargeMessageTransmissionClientHandler)Activator.CreateInstance(HandlerType);
                    Handler.Transmission = this;
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed to create a large transmission handler for {HandlerType.Format()}.", method: LogSource);
                    Logger.LogError(ex, method: LogSource);
                }
            }
            else
            {
                Logger.LogError($"Failed to create a large transmission handler for {HandlerType.Format()}, " +
                                $"must be assignable from {typeof(BaseLargeMessageTransmissionClientHandler).Format()}.", method: LogSource);
            }
        }

        _hasSent = true;
        _tknSource = new CancellationTokenSource();
        Comms = new LargeMessageTransmissionCommunications(this, false);

        if (Handler != null)
        {
            try
            {
                Handler.IsDirty = true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to set IsDirty = true on handler: {Handler.GetType().Format()}.", method: LogSource);
                Logger.LogError(ex, method: LogSource);
            }
        }
    }
    internal void WriteStart(ByteWriter writer)
    {
        writer.Write(Version);

        writer.Write(TransmissionId);
        writer.Write(Flags);
        writer.Write((ushort)Bandwidth);

        writer.WriteAsciiSmall(LogSource);

        writer.Write(OriginalSize);
        writer.Write(FinalSize);
        writer.Write(HandlerType);
    }

    internal void WriteEnd(ByteWriter writer, bool cancelled)
    {
        writer.Write(TransmissionId);

        writer.Write(Version);

        writer.Write(cancelled);
    }
    internal void WriteEnd(ByteWriter writer) => WriteEnd(writer, false);
    internal void WriteEndCancelled(ByteWriter writer) => WriteEnd(writer, true);
    public async UniTask<bool> Cancel(CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();

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
    public async UniTask Send(CancellationToken token = default)
    {
        token = token == default ? _tknSource.Token : CancellationTokenSource.CreateLinkedTokenSource(token, _tknSource.Token).Token;
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
                Logger.LogError($"Failed to set IsDirty = true on handler: {Handler.GetType().Format()}.", method: LogSource);
                Logger.LogError(ex, method: LogSource);
            }
        }

        try
        {
            await Comms.Send(token, Finalize(token));
        }
        catch (Exception ex)
        {
            Logger.LogError("Error sending transmission.", method: LogSource);
            Logger.LogError(ex, method: LogSource);
        }

        _hasSentData = true;
        _hasFullSent = true;

        if (Handler == null)
            return;

        try
        {
            Handler.IsDirty = true;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to set IsDirty = true on handler: {Handler.GetType().Format()}.", method: LogSource);
            Logger.LogError(ex, method: LogSource);
        }
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
            Logger.LogInfo($"[{LogSource}] Compresssed data from {DevkitServerUtility.FormatBytes(source.Count).Colorize(FormattingUtil.NumberColor)} " +
                           $"to {DevkitServerUtility.FormatBytes(FinalContent.Count).Colorize(FormattingUtil.NumberColor)}.");
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
            Logger.LogWarning($"Expected data of length {DevkitServerUtility.FormatBytes(OriginalSize).Colorize(FormattingUtil.NumberColor)} " +
                              $"but instead got data of length {DevkitServerUtility.FormatBytes(count).Colorize(FormattingUtil.NumberColor)}.", method: LogSource);
        }

        Content = new ArraySegment<byte>(buffer, 0, count);

        await UniTask.SwitchToMainThread(token);
    }
    private UniTask Unfinalize(CancellationToken token = default)
    {
        return IsCompressed ? Decompress(token) : UniTask.CompletedTask;
    }
    private UniTask Finalize(CancellationToken token = default)
    {
        return AllowCompression && !IsCompressed ? Compress(token) : UniTask.CompletedTask;
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
                Logger.LogError($"Failed to run OnFinished(Failure) on handler: {Handler.GetType().Format()}.", method: LogSource);
                Logger.LogError(ex, method: LogSource);
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
                Logger.LogError($"Failed to dispose handler: {Handler.GetType().Format()}.", method: LogSource);
                Logger.LogError(ex, method: LogSource);
            }
        }

        Comms.Dispose();
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
                return;
            }

            _hasFullSent = true;
            if (Handler == null)
                return;

            Handler.FinalizedTimestamp = DateTime.UtcNow;
            Handler.IsFinalized = true;
            try
            {
                Handler.IsDirty = true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to set IsDirty = true on handler: {Handler.GetType().Format()}.", method: LogSource);
                Logger.LogError(ex, method: LogSource);
            }
            try
            {
                Handler.OnFinished(LargeMessageTransmissionStatus.Success);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to run OnFinished(Success) on handler: {Handler.GetType().Format()}.", method: LogSource);
                Logger.LogError(ex, method: LogSource);
            }
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
