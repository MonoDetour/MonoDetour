using System;
using MonoDetour.DetourTypes;

namespace MonoDetour;

/// <summary>
/// Configuration for a MonoDetour Hook.
/// </summary>
public class MonoDetourConfig
{
    /// <summary>
    /// Specifies how to apply and treat this hook.
    /// </summary>
    /// <remarks>
    /// Only types which implement <see cref="IMonoDetourHookApplier"/>
    /// are valid values for this property.
    /// </remarks>
    public Type DetourType { get; }

    /// <inheritdoc cref="MonoDetourPriority"/>
    public MonoDetourPriority? DetourPriority { get; }

    /// <summary>
    /// Constructs a <see cref="MonoDetourConfig"/> with a DetourType.
    /// </summary>
    /// <remarks>
    /// Use <see cref="Create{TDetourType}(MonoDetourPriority?)"/> for custom detour implementations.
    /// </remarks>
    /// <param name="detourType">An enum which specifies how to apply and treat this hook.</param>
    /// <inheritdoc cref="MonoDetourConfig(Type, MonoDetourPriority)"/>
    /// <param name="detourPriority"></param>
    public MonoDetourConfig(DetourType detourType, MonoDetourPriority? detourPriority = null)
        : this(GetTypeFromDetourType(detourType), detourPriority) { }

    /// <summary>
    /// Constructs a <see cref="MonoDetourConfig"/> specifying the type for the detour type.<br/>
    /// Any type which implement <see cref="IMonoDetourHookApplier"/> is valid.
    /// </summary>
    /// <param name="detourType">The type which specifies how to apply and treat this hook.</param>
    /// <param name="detourPriority">Configuration to define the priority of this hook.</param>
    private MonoDetourConfig(Type detourType, MonoDetourPriority? detourPriority = null)
    {
        MonoDetourUtils.ThrowIfInvalidDetourType(detourType);
        DetourType = detourType;
        DetourPriority = detourPriority;
    }

    /// <typeparam name="TDetourType">The detour type for this config.
    /// See <see cref="IMonoDetourHookApplier"/> for more details.</typeparam>
    /// <inheritdoc cref="MonoDetourConfig(Type, MonoDetourPriority?)"/>
    public static MonoDetourConfig Create<TDetourType>(MonoDetourPriority? detourPriority = null)
        where TDetourType : IMonoDetourHookApplier => new(typeof(TDetourType), detourPriority);

    internal static Type GetTypeFromDetourType(DetourType detourType) =>
        detourType switch
        {
            DetourTypes.DetourType.PrefixDetour => typeof(PrefixDetour),
            DetourTypes.DetourType.PostfixDetour => typeof(PostfixDetour),
            DetourTypes.DetourType.ILHookDetour => typeof(ILHookDetour),
            _ => throw new ArgumentOutOfRangeException(),
        };
}
