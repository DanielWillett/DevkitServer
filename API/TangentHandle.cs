namespace DevkitServer.API;

/// <summary>
/// Represents the two tangent handles for roads.
/// </summary>
public enum TangentHandle : byte
{
    /// <summary>
    /// Faces towards the joint one index below the owning joint.
    /// </summary>
    Negative = 0,
    /// <summary>
    /// Faces towards the joint one index above the owning joint.
    /// </summary>
    Positive = 1
}