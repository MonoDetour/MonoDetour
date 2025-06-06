using System;
using System.Reflection;
using MonoDetour.DetourTypes;
using MonoDetour.Interop.RuntimeDetour;
using MonoMod.RuntimeDetour;

namespace MonoDetour;

/// <summary>
/// A MonoDetour Hook.
/// </summary>
public class MonoDetourHook : IMonoDetourHook
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

    /// <summary>
    /// The <see cref="IMonoDetourHookApplier"/> type which defines how this
    /// <see cref="MonoDetourHook"/> is applied to the target method.
    /// </summary>
    public Type ApplierType { get; }

    bool isDisposed = false;

    /// <summary>
    /// Constructs a <see cref="MonoDetourHook"/> with <see cref="Applier"/> defined by
    /// <paramref name="applierType"/>.
    /// </summary>
    /// <param name="target">The method to hook.</param>
    /// <param name="manipulator">The hook or manipulator method.</param>
    /// <param name="applierType">The <see cref="IMonoDetourHookApplier"/> type.</param>
    /// <param name="owner">The owner of this hook.</param>
    /// <param name="config">The config which defines how to apply and treat this hook.</param>
    /// <param name="applyByDefault">Whether or not the hook should be applied
    /// after it has been constructed.</param>
    private MonoDetourHook(
        MethodBase target,
        MethodBase manipulator,
        Type applierType,
        MonoDetourManager owner,
        MonoDetourConfig? config = null,
        bool applyByDefault = true
    )
    {
        Target = Helpers.ThrowIfNull(target);
        Manipulator = Helpers.ThrowIfNull(manipulator);
        Owner = Helpers.ThrowIfNull(owner);
        ApplierType = applierType;
        Config = config;

        var applierInstance = (IMonoDetourHookApplier)Activator.CreateInstance(applierType);
        applierInstance.Hook = this;

        owner.Hooks.Add(this);

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

    /// <summary>
    /// Constructs a <see cref="MonoDetourHook"/> with <see cref="Applier"/> defined by
    /// <typeparamref name="TApplier"/>.
    /// </summary>
    /// <typeparam name="TApplier">The <see cref="IMonoDetourHookApplier"/>
    /// type to define how to apply this hook.</typeparam>
    /// <returns>
    /// A new <see cref="MonoDetourHook"/>.
    /// </returns>
    /// <inheritdoc cref="MonoDetourHook(MethodBase, MethodBase, Type, MonoDetourManager, MonoDetourConfig, bool)"/>
    public static MonoDetourHook Create<TApplier>(
        MethodBase target,
        MethodBase manipulator,
        MonoDetourManager owner,
        MonoDetourConfig? config = null,
        bool applyByDefault = true
    )
        where TApplier : IMonoDetourHookApplier =>
        new(target, manipulator, typeof(TApplier), owner, config, applyByDefault);

    /// <inheritdoc/>
    public void Apply() => Applier.Apply();

    /// <inheritdoc/>
    public void Undo() => Applier.Undo();

    /// <summary>
    /// Disposes the <see cref="Applier"/> hook, disposing this <see cref="MonoDetourHook"/>.
    /// </summary>
    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        Applier.Dispose();

        isDisposed = true;
    }
}
