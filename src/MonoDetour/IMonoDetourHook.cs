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
    /// Applies the hook if it was not already applied.
    /// </summary>
    public void Apply();

    /// <summary>
    /// Undoes the hook if it was applied.
    /// </summary>
    public void Undo();
}

/// <summary>
/// A generic MonoDetour Hook interface.
/// </summary>
public interface IMonoDetourHook<TApplier> : IMonoDetourHook, IReadOnlyMonoDetourHook<TApplier>
    where TApplier : IMonoDetourHookApplier;
