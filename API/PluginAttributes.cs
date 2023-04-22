using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DevkitServer.API;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class PluginLoadPriorityAttribute : Attribute
{
    public int Priority { get; }

    public PluginLoadPriorityAttribute(int priority)
    {
        Priority = priority;
    }
}
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class IgnorePluginAttribute : Attribute { }
