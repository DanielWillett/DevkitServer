namespace DevkitServer.API;

/// <summary>
/// Defines server/client side relationships.
/// </summary>
[Flags]
public enum ConnectionSide : byte
{
    /// <summary>
    /// This component will not be used at all.
    /// </summary>
    Neither = 0,

    /// <summary>
    /// This component will only be used on the dedicated server build.
    /// </summary>
    Server = 0b01,

    /// <summary>
    /// This component will only be used on the client build.
    /// </summary>
    Client = 0b10,

    /// <summary>
    /// This component will always be used.
    /// </summary>
    Both = Server | Client
}
