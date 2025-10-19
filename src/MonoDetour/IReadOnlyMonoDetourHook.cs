using System;
using System.Reflection;

namespace MonoDetour;

/// <summary>
/// A readonly MonoDetour Hook interface.
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
    /// A delegate of <see cref="Manipulator"/>; the hook or manipulator method.<br/>
    /// Not null if the MonoDetourHook
    /// was passed a <see cref="Delegate"/> instead of a <see cref="MethodBase"/>
    /// during construction.
    /// </summary>
    public Delegate? ManipulatorDelegate { get; }

    /// <summary>
    /// The owner <see cref="MonoDetourManager"/> of this hook.
    /// </summary>
    public MonoDetourManager Owner { get; }

    /// <inheritdoc cref="MonoDetourConfig"/>
    public MonoDetourConfig? Config { get; }
}
