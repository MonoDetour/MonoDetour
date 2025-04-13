using System;

namespace MonoDetour;

/// <summary>
/// Specifies that:
/// <list type="bullet">
///   <item>
///   <see cref="MonoDetourManager.InvokeHookInitializers(System.Reflection.Assembly)"/>
///   will hook methods marked with <see cref="MonoDetourHookInitAttribute"/> in types with this attribute
///   </item>
///   <item>
///   MonoDetour will generate hooks for the targetTypes specified
///   </item>
/// </list>
/// </summary>
/// <param name="targetTypes">Types which MonoDetour will generate hooks for.</param>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class MonoDetourTargetsAttribute(params Type[] targetTypes) : Attribute
{
    /// <summary>
    /// Types which MonoDetour will generate hooks for.
    /// </summary>
    public Type[] TargetTypes => targetTypes;
}
