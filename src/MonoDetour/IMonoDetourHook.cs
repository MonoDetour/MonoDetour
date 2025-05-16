using System;
using MonoDetour.DetourTypes;
using MonoMod.RuntimeDetour;

namespace MonoDetour;

/// <summary>
/// A non-generic MonoDetour Hook interface.
/// </summary>
public interface IMonoDetourHook : IReadOnlyMonoDetourHook, IDisposable
{
    /// <summary>
    /// The <see cref="ILHook"/> that applies the <see cref="IReadOnlyMonoDetourHook.Manipulator"/> method.<br/>
    /// An applier comes from a class implementing <see cref="IMonoDetourHookApplier"/>.
    /// </summary>
    public ILHook Applier { get; }

    /// <summary>
    /// Applies the <see cref="Applier"/> if it was not already applied.
    /// </summary>
    public void Apply();

    /// <summary>
    /// Undoes the <see cref="Applier"/> if it was applied.
    /// </summary>
    public void Undo();
}

/// <summary>
/// A generic MonoDetour Hook interface.
/// </summary>
public interface IMonoDetourHook<TApplier> : IMonoDetourHook, IReadOnlyMonoDetourHook<TApplier>
    where TApplier : IMonoDetourHookApplier;
