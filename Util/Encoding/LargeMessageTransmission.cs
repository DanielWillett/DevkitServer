using Cysharp.Threading.Tasks;
using DevkitServer.Multiplayer.Networking;
using System.IO.Compression;
using CompressionLevel = System.IO.Compression.CompressionLevel;
using DeflateStream = System.IO.Compression.DeflateStream;

namespace DevkitServer.Util.Encoding;
public class LargeMessageTransmission
{
    private const ushort Version = 0;
    internal readonly LargeMessageTransmissionCommunications Comms;
    public const int HeaderCapacity = 64;
    public const int FooterCapacity = 16;
    public const int PacketCapacity = ushort.MaxValue;

    public bool AllowHighSpeed { get; set; } = true;
    public bool AllowCompression { get; set; } = true;
    public Guid TransmissionId { get; }
#if SERVER
    public ITransportConnection Connection { get; set; }
#elif CLIENT
    public IClientTransport Connection { get; set; }
#endif
    public ArraySegment<byte> Content { get; private set; }
    public ArraySegment<byte> FinalContent { get; private set; }
    public string LogSource { get; set; } = "LARGE MSG";
    public bool IsCompressed { get; private set; }
    public int OriginalSize { get; set; }
    public int Bandwidth { get; set; }
    public IProgressTracker? ProgressTracker { get; set; }

    internal byte Flags
    {
        get => (byte)(
            (AllowHighSpeed ? 1 : 0) |
            (AllowCompression ? 2 : 0) |
            (IsCompressed ? 4 : 0));
        set
        {
            AllowHighSpeed = (value & 1) != 0;
            AllowCompression = (value & 2) != 0;
            IsCompressed = (value & 4) != 0;
        }
    }

    public LargeMessageTransmission(
#if SERVER
        ITransportConnection? targetConnection,
#endif
        ArraySegment<byte> content, int bandwidth)
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
        Comms = new LargeMessageTransmissionCommunications(this, true);
    }

    public LargeMessageTransmission(
#if SERVER
        ITransportConnection? sendingConnection,
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
        Comms = new LargeMessageTransmissionCommunications(this, false);
    }

    public void Write(ByteWriter writer)
    {
        writer.Write(Version);

        writer.Write(TransmissionId);
        writer.Write(Flags);
        writer.Write((ushort)Bandwidth);

        writer.WriteAsciiSmall(LogSource);

        writer.Write(OriginalSize);
    }

    public async UniTask Unfinalize(CancellationToken token = default)
    {
        if (IsCompressed)
        {
            await Decompress(token);
        }

        await UniTask.SwitchToMainThread(PlayerLoopTiming.EarlyUpdate, token);
    }

    public async UniTask Finalize(CancellationToken token = default)
    {
        if (IsCompressed)
        {
            await Compress(token);
        }

        await UniTask.SwitchToMainThread(PlayerLoopTiming.EarlyUpdate, token);
    }

    private async UniTask Compress(CancellationToken token = default)
    {
        ArraySegment<byte> content = Content;
        using MemoryStream mem = new MemoryStream(content.Count);

        bool disposed = false;
        DeflateStream stream = new DeflateStream(mem, CompressionLevel.Optimal);
        try
        {
            await stream.WriteAsync(content, token).ConfigureAwait(false);
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

        byte[] compressedContent = mem.GetBuffer();
        int length = (int)mem.Position;
        token.ThrowIfCancellationRequested();

        if (length < content.Count)
        {
            IsCompressed = true;
            FinalContent = new ArraySegment<byte>(compressedContent, 0, length);
            Logger.LogInfo($"[{LogSource}] Compresssed data from {DevkitServerUtility.FormatBytes(content.Count).Colorize(FormattingUtil.NumberColor)} " +
                           $"to {DevkitServerUtility.FormatBytes(FinalContent.Count).Colorize(FormattingUtil.NumberColor)}.");
        }
        else
        {
            IsCompressed = false;
            FinalContent = content;
        }
    }

    private async UniTask Decompress(CancellationToken token = default)
    {
        if (!IsCompressed)
        {
            Content = FinalContent;
            return;
        }

        using MemoryStream mem = new MemoryStream(FinalContent.Count);

        byte[] buffer = new byte[OriginalSize];

        await using DeflateStream stream = new DeflateStream(mem, CompressionMode.Decompress);

        int count = await stream.ReadAsync(buffer, token).ConfigureAwait(false);

        await UniTask.SwitchToMainThread(PlayerLoopTiming.EarlyUpdate, token);
        if (OriginalSize != count)
        {
            Logger.LogWarning($"Expected data of length {DevkitServerUtility.FormatBytes(OriginalSize).Colorize(FormattingUtil.NumberColor)} " +
                              $"but instead got data of length {DevkitServerUtility.FormatBytes(count).Colorize(FormattingUtil.NumberColor)}.", method: LogSource);

        }

        Content = new ArraySegment<byte>(buffer, 0, count);
    }
}
