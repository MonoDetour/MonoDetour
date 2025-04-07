using System;

namespace MonoDetour;

/// <summary>
/// Metadata configuration for a MonoDetour Hook.
/// </summary>
public class MonoDetourInfo
{
    /// <summary>
    /// Specifies how to apply and treat this hook.
    /// </summary>
    /// <remarks>
    /// Only types which implement <see cref="IMonoDetourHookEmitter"/>
    /// are valid values for this property.
    /// </remarks>
    public Type DetourType { get; set; }

    /// <inheritdoc cref="MonoDetourData"/>
    public MonoDetourData Data { get; } = new();

    /// <summary>
    /// Constructs a MonoDetourInfo with a DetourType.
    /// </summary>
    /// <remarks>
    /// Use <see cref="MonoDetourInfo(Type)"/> for custom detour implementations.
    /// </remarks>
    public MonoDetourInfo(DetourType detourType)
        : this(GetTypeFromDetourType(detourType)) { }

    /// <summary>
    /// Constructs a MonoDetourInfo with a type for the detour type.
    /// </summary>
    /// <remarks>
    /// Only types which implement <see cref="IMonoDetourHookEmitter"/>
    /// are valid values for this constructor.
    /// </remarks>
    /// <param name="detourType">The type which specifies how to apply and treat this hook.</param>
    public MonoDetourInfo(Type detourType)
    {
        MonoDetourUtils.ThrowIfInvalidDetourType(detourType);
        DetourType = detourType;
    }

    internal static Type GetTypeFromDetourType(DetourType detourType) =>
        detourType switch
        {
            MonoDetour.DetourType.Prefix => typeof(PrefixDetour),
            MonoDetour.DetourType.Postfix => typeof(PostfixDetour),
            MonoDetour.DetourType.ILHook => typeof(ILHookDetour),
            _ => throw new ArgumentOutOfRangeException(),
        };
}
