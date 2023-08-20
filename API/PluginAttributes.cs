namespace DevkitServer.API;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class LoadPriorityAttribute : Attribute
{
    public int Priority { get; }

    public LoadPriorityAttribute(int priority)
    {
        Priority = priority;
    }
}
[AttributeUsage(AttributeTargets.All, Inherited = false)]
public sealed class IgnoreAttribute : Attribute { }

[AttributeUsage(AttributeTargets.All, Inherited = false)]
public class PluginIdentifierAttribute : Attribute
{
    public Type? PluginType { get; set; }
}