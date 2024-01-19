namespace DevkitServer.API.Devkit.Spawns;

/// <summary>
/// When events are chain-invoked for child objects in a hierarchy, defines how many generations of parent objects away the object that invoked the event is.
/// </summary>
public enum HierarchicalEventSource
{
    /// <summary>
    /// The object raising the event is on the same hierarchical level as the one that was removed.
    /// </summary>
    ThisObject,

    /// <summary>
    /// The object raising the event's parent is on the same hierarchical level as the one that was removed.
    /// </summary>
    ParentObject,

    /// <summary>
    /// The object raising the event's parent's parent is on the same hierarchical level as the one that was removed.
    /// </summary>
    GrandParentObject,

    /// <summary>
    /// The object raising the event's parent's parent's parent is on the same hierarchical level as the one that was removed.
    /// </summary>
    GreatGrandParentObject
}
