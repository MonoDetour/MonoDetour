using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using MonoDetour.DetourTypes;
using MonoDetour.Interop.RuntimeDetour;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace MonoDetour;

/// <summary>
/// A MonoDetour Hook.
/// </summary>
public class MonoDetourHook : IMonoDetourHook
{
    static readonly ConditionalWeakTable<
        ILContext.Manipulator,
        MonoDetourHook
    > s_ManipulatorToHook = new();

#if NETSTANDARD2_0
    static readonly object tableLock = new();
#else
    static readonly System.Threading.Lock tableLock = new();
#endif

    /// <inheritdoc/>
    public MethodBase Target { get; }

    /// <remarks>
    /// This differs from <see cref="Applier"/>'s <see cref="ILHook.Manipulator"/>,
    /// as that points to the <see cref="Applier"/> itself.
    /// </remarks>
    /// <inheritdoc/>
    public MethodBase Manipulator { get; }

    /// <inheritdoc/>
    public Delegate? ManipulatorDelegate { get; }

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

    /// <inheritdoc/>
    public bool IsValid => Applier.IsValid;

    /// <inheritdoc/>
    public bool IsApplied => Applier.IsApplied;

    bool isDisposed;

    /// <summary>
    /// Constructs a <see cref="MonoDetourHook"/> with <see cref="Applier"/> defined by
    /// <paramref name="applierType"/>.
    /// </summary>
    /// <param name="target">The method to hook.</param>
    /// <param name="manipulator">The hook or manipulator method.</param>
    /// <param name="manipulatorDelegate">A <see cref="Delegate"/> of the hook or manipulator method.</param>
    /// <param name="applierType">The <see cref="IMonoDetourHookApplier"/> type.</param>
    /// <param name="owner">The owner of this hook.</param>
    /// <param name="config">The config which defines how to apply and treat this hook.</param>
    /// <param name="applyByDefault">Whether or not the hook should be applied
    /// after it has been constructed.</param>
    private MonoDetourHook(
        MethodBase target,
        MethodBase manipulator,
        Delegate? manipulatorDelegate,
        Type applierType,
        MonoDetourManager owner,
        MonoDetourConfig? config = null,
        bool applyByDefault = true
    )
    {
        Target = Helpers.ThrowIfNull(target);
        Manipulator = Helpers.ThrowIfNull(manipulator);
        Owner = Helpers.ThrowIfNull(owner);
        ManipulatorDelegate = manipulatorDelegate;
        ApplierType = applierType;
        Config = config;

        var applierInstance = (IMonoDetourHookApplier)Activator.CreateInstance(applierType)!;
        applierInstance.Hook = this;

        owner.Hooks.Add(this);

        ILContext.Manipulator applierManipulator = applierInstance.ApplierManipulator;

        Applier = ProxyILHookConstructor.ConstructILHook(
            target,
            applierManipulator,
            config,
            owner.Id
        );

        lock (tableLock)
        {
            s_ManipulatorToHook.Add(applierManipulator, this);
        }

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
    /// <inheritdoc cref="MonoDetourHook(MethodBase, MethodBase, Delegate, Type, MonoDetourManager, MonoDetourConfig, bool)"/>
    public static MonoDetourHook Create<TApplier>(
        MethodBase target,
        Delegate manipulator,
        MonoDetourManager owner,
        MonoDetourConfig? config = null,
        bool applyByDefault = true
    )
        where TApplier : class, IMonoDetourHookApplier =>
        new(
            target,
            Helpers.ThrowIfNull(manipulator).Method,
            manipulator,
            typeof(TApplier),
            owner,
            config,
            applyByDefault
        );

    /// <inheritdoc cref="Create{TApplier}(MethodBase, Delegate, MonoDetourManager, MonoDetourConfig?, bool)"/>
    public static MonoDetourHook Create<TApplier>(
        MethodBase target,
        MethodBase manipulator,
        MonoDetourManager owner,
        MonoDetourConfig? config = null,
        bool applyByDefault = true
    )
        where TApplier : class, IMonoDetourHookApplier =>
        new(target, manipulator, null, typeof(TApplier), owner, config, applyByDefault);

    /// <summary>
    /// Tries to get the corresponding <see cref="MonoDetourHook"/> for the
    /// specified <see cref="ILContext.Manipulator"/> if it exists.
    /// </summary>
    /// <param name="key">The <see cref="ILContext.Manipulator"/>
    /// whose <see cref="MonoDetourHook"/> to get.</param>
    /// <param name="monoDetourHook">The found <see cref="MonoDetourHook"/> or null.</param>
    /// <returns>
    /// <see langword="true"/> if the corresponding <see cref="MonoDetourHook"/> was found;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    public static bool TryGetFrom(
        ILContext.Manipulator key,
        [NotNullWhen(true)] out MonoDetourHook? monoDetourHook
    ) => s_ManipulatorToHook.TryGetValue(key, out monoDetourHook);

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
        GC.SuppressFinalize(this);

        isDisposed = true;
    }
}
