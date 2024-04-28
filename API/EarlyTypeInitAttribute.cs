namespace DevkitServer.API;

/// <summary>
/// Marks a class to have it's static constructor (type initializer) ran on load.
/// Helps ensure there are no errors hidden in your static members that will pop up later, and moves all load time to when the game/server actually loads.
/// </summary>
/// <remarks>Works in plugins as well.</remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class EarlyTypeInitAttribute : Attribute
{
    /// <summary>
    /// Defines the order in which type initializers run. Lower gets ran first. Default priority is zero.
    /// </summary>
    public int Priority { get; }
    internal bool RequiresUIAccessTools { get; set; }

    public EarlyTypeInitAttribute() : this(0) { }

    public EarlyTypeInitAttribute(int priority)
    {
        Priority = priority;
    }
    internal EarlyTypeInitAttribute(bool requiresUIAccessTools)
    {
        RequiresUIAccessTools = requiresUIAccessTools;
    }
    internal EarlyTypeInitAttribute(int priority, bool requiresUIAccessTools)
    {
        Priority = priority;
        RequiresUIAccessTools = requiresUIAccessTools;
    }
}