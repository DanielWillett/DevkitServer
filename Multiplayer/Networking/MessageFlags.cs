namespace DevkitServer.Multiplayer.Networking;

[Flags]
public enum MessageFlags : byte
{
    /// <summary>Only look for base net methods.</summary>
    None = 0,
    /// <summary>Look for listeners and run them instead of net methods.</summary>
    /// <remarks>Will not run the base net methods unless a listener isn't found!</remarks>
    Request = 1,
    /// <summary>Look for listeners and run them instead of net methods.</summary>
    /// <remarks>Will not run the base net methods unless a listener isn't found!</remarks>
    GuidRequest = Guid | Request,
    /// <summary>Use with <see cref="Request"/> to use a request id while still running any net methods.</summary>
    RunOriginalMethodOnRequest = 2,
    /// <summary>Use with <see cref="Request"/> to use a request id while still running any net methods.</summary>
    GuidRunOriginalMethodOnRequest = Guid | RunOriginalMethodOnRequest,
    /// <summary>Runs any listeners and base net methods.</summary>
    LayeredRequest = Request | RunOriginalMethodOnRequest,
    /// <summary>Runs any listeners and base net methods.</summary>
    LayeredGuidRequest = Guid | Request | RunOriginalMethodOnRequest,
    /// <summary>Look for listeners and run them instead of net methods.</summary>
    /// <remarks>Will not run the base net methods unless a listener isn't found!</remarks>
    RequestResponse = 4,
    /// <summary>Runs any listeners and base net methods.</summary>
    LayeredResponse = RequestResponse | RunOriginalMethodOnRequest,
    /// <summary>Look for listeners and run them instead of net methods.</summary>
    /// <remarks>Will not run the base net methods unless a listener isn't found!</remarks>
    GuidRequestResponse = Guid | RequestResponse,
    /// <summary>Runs any listeners and base net methods.</summary>
    LayeredGuidResponse = Guid | LayeredResponse,
    /// <summary>Requests an ACK message to be returned with an optional error code.</summary>
    AcknowledgeRequest = 8,
    /// <summary>An ACK message with an optional error code.</summary>
    AcknowledgeResponse = 16,
    /// <summary>Requests an ACK message to be returned with an optional error code.</summary>
    GuidAcknowledgeRequest = Guid | AcknowledgeRequest,
    /// <summary>An ACK message with an optional error code.</summary>
    GuidAcknowledgeResponse = Guid | AcknowledgeResponse,
    /// <summary>Requests an ACK message to be returned from a response to a request.</summary>
    RequestResponseWithAcknowledgeRequest = 32,
    HighSpeed = 64,
    Guid = 128
}