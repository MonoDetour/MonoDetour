using System;

namespace MonoDetour;

/// <summary>
/// A MonoDetour Hook interface.
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
