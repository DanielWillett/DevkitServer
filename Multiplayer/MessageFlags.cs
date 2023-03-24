namespace DevkitServer.Multiplayer;

[Flags]
public enum MessageFlags : byte
{
    /// <summary>Only look for base net methods.</summary>
    None = 0,
    /// <summary>Look for listeners and run them instead of net methods.</summary>
    /// <remarks>Will not run the base net methods unless a listener isn't found!</remarks>
    Request = 1,
    /// <summary>Use with <see cref="Request"/> to use a request id while still running any net methods.</summary>
    RunOriginalMethodOnRequest = 2,
    /// <summary>Runs any listeners and base net methods.</summary>
    LayeredRequest = Request | RunOriginalMethodOnRequest,
    /// <summary>Look for listeners and run them instead of net methods.</summary>
    /// <remarks>Will not run the base net methods unless a listener isn't found!</remarks>
    RequestResponse = 4,
    /// <summary>Runs any listeners and base net methods.</summary>
    LayeredResponse = RequestResponse | RunOriginalMethodOnRequest,
    /// <summary>Requests an ACK message to be returned with an optional error code.</summary>
    AcknowledgeRequest = 8,
    /// <summary>An ACK message with an optional error code.</summary>
    AcknowledgeResponse = 16,
    /// <summary>Requests an ACK message to be returned from a response to a request.</summary>
    RequestResponseWithAcknowledgeRequest = 32,
    Relay = 64
}