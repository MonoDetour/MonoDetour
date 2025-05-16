using System.Reflection;
using MonoDetour.DetourTypes;

namespace MonoDetour;

/// <summary>
/// A non-generic readonly MonoDetour Hook interface.
/// </summary>
public interface IReadOnlyMonoDetourHook
{
    /// <summary>
    /// The method to hook.
    /// </summary>
    public MethodBase Target { get; }

    /// <summary>
    /// The hook or manipulator method.
    /// </summary>
    public MethodBase Manipulator { get; }

    /// <summary>
    /// The owner <see cref="MonoDetourManager"/> of this hook.
    /// </summary>
    public MonoDetourManager Owner { get; }

    /// <inheritdoc cref="MonoDetourConfig"/>
    public MonoDetourConfig? Config { get; }
}

/// <summary>
/// A generic readonly MonoDetour Hook interface.
/// </summary>
public interface IReadOnlyMonoDetourHook<TApplier> : IReadOnlyMonoDetourHook
    where TApplier : IMonoDetourHookApplier;
