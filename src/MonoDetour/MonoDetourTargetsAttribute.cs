using System;

namespace MonoDetour;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class MonoDetourTargetsAttribute(params Type[] targetTypes) : Attribute
{
    public Type[] TargetTypes => targetTypes;
}
