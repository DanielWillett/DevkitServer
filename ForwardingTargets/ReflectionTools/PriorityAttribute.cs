using System;
using System.Runtime.CompilerServices;

namespace DanielWillett.ReflectionTools;

[TypeForwardedFrom("ReflectionTools, Version=1.0.2.0, Culture=neutral, PublicKeyToken=6a3a944a5a8d6b8f")]
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class PriorityAttribute : Attribute
{
    public int Priority { get; }
    public PriorityAttribute(int priority)
    {
        Priority = priority;
    }
}