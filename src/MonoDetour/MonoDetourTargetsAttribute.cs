using System;

namespace MonoDetour;

/// <summary>
/// Specifies that:
/// <list type="bullet">
///   <item>
///     MonoDetour.HookGen will generate hooks for the targetTypes specified
///   </item>
///   <item>
///     <see cref="MonoDetourManager.InvokeHookInitializers(System.Reflection.Assembly)"/>
///     will hook methods marked with <see cref="MonoDetourHookInitAttribute"/> in types with this attribute
///   </item>
/// </list>
/// </summary>
/// <param name="targetType">The type to generate hook helpers for.</param>
/// <remarks>
/// Non-public members of the type may or may not be included.
/// It is recommended to use a publicizer with MonoDetour's hook generator.
/// </remarks>
[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class, Inherited = false)]
public class MonoDetourTargetsAttribute(Type? targetType = null) : Attribute
{
    /// <summary>
    /// The type to generate hook helpers for the members of.
    /// </summary>
    public Type? TargetType { get; } = targetType;

    // public DetourKind Kind { get; set; } = DetourKind.Hook;

    /// <summary>
    /// Whether to generate helpers for nested types. Defaults to <see langword="true"/>.
    /// </summary>
    public bool IncludeNestedTypes { get; set; } = true;

    /// <summary>
    /// Whether to differentiate between overloaded members by putting their (sanitized) signature in the generated name.
    /// Defaults to <see langword="false"/>.
    /// </summary>
    public bool DistinguishOverloadsByName { get; set; }

    /// <summary>
    /// A list of members to generate hook helpers for in the target type, by exact name.
    /// All members with the specified names (including overloads) will be generated.
    /// </summary>
    public string[]? Members { get; set; }

    /// <summary>
    /// A list of member name prefixes to match members against. Members whose names have one of these
    /// prefixes will be included.
    /// </summary>
    public string[]? MemberNamePrefixes { get; set; }

    /// <summary>
    /// A list of member name suffixes to match members against. Members whose names have one of these
    /// suffixes will be included.
    /// </summary>
    public string[]? MemberNameSuffixes { get; set; }
}
