using System;
using System.Reflection;
using MonoMod.RuntimeDetour;

namespace MonoDetour;

/// <summary>
/// A MonoDetour Hook.
/// </summary>
/// <param name="target">The method to hook.</param>
/// <param name="manipulator">The hook or manipulator method.</param>
/// <param name="owner">The owner of this hook.</param>
/// <param name="config">The config which defines how to apply and treat this hook.</param>
/// <param name="applier">The <see cref="ILHook"/> which applies this <see cref="MonoDetourHook"/>.</param>
public class MonoDetourHook(
    MethodBase target,
    MethodBase manipulator,
    MonoDetourManager owner,
    MonoDetourConfig config,
    ILHook applier
) : IDisposable
{
    /// <summary>
    /// The method to hook.
    /// </summary>
    public MethodBase Target { get; } = Helpers.ThrowIfNull(target);

    /// <summary>
    /// The hook or manipulator method.
    /// </summary>
    /// <remarks>
    /// This differs from <see cref="ManipulatorApplier"/>'s <see cref="ILHook.Manipulator"/>,
    /// as that points to the <see cref="ManipulatorApplier"/> itself.
    /// </remarks>
    public MethodBase Manipulator { get; } = Helpers.ThrowIfNull(manipulator);

    /// <summary>
    /// The owner <see cref="MonoDetourManager"/> of this hook.
    /// </summary>
    public MonoDetourManager Owner { get; } = Helpers.ThrowIfNull(owner);

    /// <inheritdoc cref="MonoDetourConfig"/>
    public MonoDetourConfig Config { get; } = Helpers.ThrowIfNull(config);

    /// <summary>
    /// The <see cref="ILHook"/> that applies the <see cref="Manipulator"/> method.<br/>
    /// An applier comes from a class implementing <see cref="DetourTypes.IMonoDetourHookApplier"/>.
    /// </summary>
    public ILHook ManipulatorApplier { get; } = Helpers.ThrowIfNull(applier);

    bool isDisposed = false;

    /// <summary>
    /// Applies the <see cref="ManipulatorApplier"/> if it was not already applied.
    /// </summary>
    public void Apply() => ManipulatorApplier.Apply();

    /// <summary>
    /// Undoes the <see cref="ManipulatorApplier"/> if it was applied.
    /// </summary>
    public void Undo() => ManipulatorApplier.Undo();

    void Dispose(bool disposing)
    {
        if (isDisposed)
        {
            return;
        }

        if (disposing)
        {
            ManipulatorApplier.Dispose();
        }

        isDisposed = true;
    }

    /// <summary>
    /// Cleans up and undoes the hook, if needed.
    /// </summary>
    ~MonoDetourHook()
    {
        Dispose(disposing: false);
    }

    /// <summary>
    /// Disposes the <see cref="ManipulatorApplier"/> hook, disposing this <see cref="MonoDetourHook"/>.
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
