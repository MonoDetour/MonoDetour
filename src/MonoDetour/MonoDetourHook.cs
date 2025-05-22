using System;
using System.Reflection;
using MonoDetour.DetourTypes;
using MonoDetour.Interop.RuntimeDetour;
using MonoMod.RuntimeDetour;

namespace MonoDetour;

/// <summary>
/// A MonoDetour Hook.
/// </summary>
/// <typeparam name="TApplier">The <see cref="IMonoDetourHookApplier"/>
/// type to define how to apply this hook.</typeparam>
public class MonoDetourHook<TApplier> : IMonoDetourHook<TApplier>
    where TApplier : IMonoDetourHookApplier
{
    /// <inheritdoc/>
    public MethodBase Target { get; }

    /// <remarks>
    /// This differs from <see cref="Applier"/>'s <see cref="ILHook.Manipulator"/>,
    /// as that points to the <see cref="Applier"/> itself.
    /// </remarks>
    /// <inheritdoc/>
    public MethodBase Manipulator { get; }

    /// <inheritdoc/>
    public MonoDetourManager Owner { get; }

    /// <inheritdoc cref="MonoDetourConfig"/>
    public MonoDetourConfig? Config { get; }

    /// <summary>
    /// The <see cref="ILHook"/> that applies the <see cref="Manipulator"/> method.<br/>
    /// An applier comes from a class implementing <see cref="IMonoDetourHookApplier"/>.
    /// </summary>
    public ILHook Applier { get; }

    bool isDisposed = false;

    /// <summary>
    /// Constructs a <see cref="MonoDetourHook{TApplier}"/> with <see cref="Applier"/> defined by
    /// <typeparamref name="TApplier"/>.
    /// </summary>
    /// <param name="target">The method to hook.</param>
    /// <param name="manipulator">The hook or manipulator method.</param>
    /// <param name="owner">The owner of this hook.</param>
    /// <param name="config">The config which defines how to apply and treat this hook.</param>
    /// <param name="applyByDefault">Whether or not the hook should be applied
    /// after it has been constructed.</param>
    public MonoDetourHook(
        MethodBase target,
        MethodBase manipulator,
        MonoDetourManager owner,
        MonoDetourConfig? config = null,
        bool applyByDefault = true
    )
    {
        Target = Helpers.ThrowIfNull(target);
        Manipulator = Helpers.ThrowIfNull(manipulator);
        Owner = Helpers.ThrowIfNull(owner);
        Config = config;

        var applierInstance = Activator.CreateInstance<TApplier>();
        applierInstance.Hook = this;

        owner.MonoDetourHooks.Add(this);

        Applier = ProxyILHookConstructor.ConstructILHook(
            target,
            applierInstance.ApplierManipulator,
            config,
            owner.Id
        );

        if (applyByDefault)
        {
            Applier.Apply();
        }
    }

    /// <inheritdoc/>
    public void Apply() => Applier.Apply();

    /// <inheritdoc/>
    public void Undo() => Applier.Undo();

    void Dispose(bool disposing)
    {
        if (isDisposed)
        {
            return;
        }

        if (disposing)
        {
            Applier.Dispose();
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
    /// Disposes the <see cref="Applier"/> hook, disposing this <see cref="MonoDetourHook{TApplier}"/>.
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
