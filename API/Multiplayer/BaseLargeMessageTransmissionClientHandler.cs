using System.Text.Json.Serialization;
using DevkitServer.Util.Encoding;

namespace DevkitServer.API.Multiplayer;

/// <summary>
/// Handles updates from <see cref="LargeMessageTransmission"/>s.
/// </summary>
public abstract class BaseLargeMessageTransmissionClientHandler
{
    [JsonIgnore]
    /// <summary>
    /// The transmission linked to this handler.
    /// </summary>
    public LargeMessageTransmission Transmission { get; internal set; } = null!;

    /// <summary>
    /// If low-speed (Steam Networking) is being used, which sends data in small packets instead of all at once.
    /// </summary>
    public bool IsUsingPackets { get; internal set; }

    /// <summary>
    /// Total number of bytes expected to be sent.
    /// </summary>
    public int TotalBytes { get; internal set; }

    /// <summary>
    /// Time at which the download first started.
    /// </summary>
    public DateTime StartTimestamp { get; internal set; }

    /// <summary>
    /// Time at which the download ended.
    /// </summary>
    public DateTime? EndTimestamp { get; internal set; }

    /// <summary>
    /// Time at which the download finalized (decompressed).
    /// </summary>
    public DateTime? FinalizedTimestamp { get; internal set; }

    /// <summary>
    /// Has initial data been sent?
    /// </summary>
    public bool IsStarted { get; internal set; }

    /// <summary>
    /// Has all data been transfered?
    /// </summary>
    public bool IsDownloaded { get; internal set; }

    /// <summary>
    /// Has all data been transfered and verified?
    /// </summary>
    public bool IsFinalized { get; internal set; }

    /// <summary>
    /// Total number of bytes that have been received
    /// </summary>
    public int ReceivedBytes { get; internal set; }

    /// <summary>
    /// Total packets expected to be sent.
    /// </summary>
    /// <remarks>Only relevant when <see cref="IsUsingPackets"/> is <see langword="true"/>.</remarks>
    public int TotalPackets { get; internal set; }

    /// <summary>
    /// Number of packets that have been fully received.
    /// </summary>
    /// <remarks>Only relevant when <see cref="IsUsingPackets"/> is <see langword="true"/>.</remarks>
    public int ReceivedPackets { get; internal set; }

    /// <summary>
    /// Remaining packets left to send after missing packets were detected.
    /// </summary>
    /// <remarks>Only relevant when <see cref="IsUsingPackets"/> is <see langword="true"/>.</remarks>
    public int TotalMissingPackets { get; internal set; }

    /// <summary>
    /// If any, the amount of packets that were missing after the first batch.
    /// </summary>
    /// <remarks>Only relevant when <see cref="IsUsingPackets"/> is <see langword="true"/>.</remarks>
    public int InitialMissingPackets { get; internal set; }

    /// <summary>
    /// Last time a 'keep-alive' is received during a high-speed data transfer.
    /// </summary>
    /// <remarks>Only relevant when <see cref="IsUsingPackets"/> is <see langword="false"/>.</remarks>
    public DateTime LastKeepAlive { get; internal set; }

    /// <summary>
    /// Set to true whenever something is changed. You must set this to false after flushing changes or override to use this property.
    /// </summary>
    public virtual bool IsDirty { get; internal set; }

    /// <summary>
    /// Called when the message is first initialized (before any data has been received).
    /// </summary>
    protected internal virtual void OnStart() { }

    /// <summary>
    /// Called when the message is fully sent or fails. If this derives from <see cref="IDisposable"/>, <see cref="IDisposable.Dispose"/> will be called soon after.
    /// </summary>
    protected internal virtual void OnFinished(LargeMessageTransmissionStatus status) { }
}

public enum LargeMessageTransmissionStatus
{
    /// <summary>
    /// The transmission completed successfully.
    /// </summary>
    Success,

    /// <summary>
    /// The transmission failed but wasn't cancelled.
    /// </summary>
    Failure,

    /// <summary>
    /// The transmission was cancelled.
    /// </summary>
    Cancelled
}