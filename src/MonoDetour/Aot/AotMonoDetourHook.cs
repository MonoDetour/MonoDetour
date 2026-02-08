using System;
using System.Collections.Generic;
using System.Reflection;
using Mono.Cecil;
using MonoDetour.Aot.DetourTypes;
using MonoDetour.DetourTypes;

namespace MonoDetour.Aot;

/// <summary>
/// A MonoDetour Hook.
/// </summary>
public class AotMonoDetourHook : IReadOnlyAotMonoDetourHook
{
    internal static Dictionary<MethodDefinition, List<AotMonoDetourHook>> TargetToHooks { get; } =
    [];

#if NETSTANDARD2_0
    static readonly object tableLock = new();
#else
    static readonly System.Threading.Lock tableLock = new();
#endif

    /// <inheritdoc/>
    public MethodDefinition Target { get; }

    /// <summary>
    /// The hook or manipulator method.
    /// </summary>
    public MethodBase? ManipulatorBase { get; }

    /// <summary>
    /// Manipulator method as a <see cref="MethodDefinition"/>.
    /// </summary>
    public MethodDefinition? ManipulatorDefinition { get; }

    /// <inheritdoc/>
    public AotMonoDetourManager Owner { get; }

    /// <inheritdoc cref="MonoDetourConfig"/>
    public MonoDetourConfig? Config { get; }

    /// <summary>
    /// The <see cref="IAotMonoDetourHookApplier"/> type which defines how this
    /// <see cref="AotMonoDetourHook"/> is applied to the target method.
    /// </summary>
    public Type ApplierType { get; }

    /// <inheritdoc/>
    public bool IsApplied { get; private set; }

    readonly IAotMonoDetourHookApplier aotHookApplier;

    /// <summary>
    /// Constructs a <see cref="AotMonoDetourHook"/> with an applier defined by
    /// <paramref name="applierType"/>.
    /// </summary>
    /// <param name="target">The method to hook.</param>
    /// <param name="manipulatorBase">The hook or manipulator method.</param>
    /// <param name="manipulatorDefinition">Manipulator method as a <see cref="MethodDefinition"/>.</param>
    /// <param name="applierType">The <see cref="IMonoDetourHookApplier"/> type.</param>
    /// <param name="owner">The owner of this hook.</param>
    /// <param name="config">The config which defines how to apply and treat this hook.</param>
    /// <param name="applyByDefault">Whether or not the hook should be applied
    /// after it has been constructed.</param>
    private AotMonoDetourHook(
        MethodDefinition target,
        MethodBase? manipulatorBase,
        MethodDefinition? manipulatorDefinition,
        Type applierType,
        AotMonoDetourManager owner,
        MonoDetourConfig? config = null,
        bool applyByDefault = true
    )
    {
        Target = Helpers.ThrowIfNull(target);
        ManipulatorBase = manipulatorBase;
        ManipulatorDefinition = manipulatorDefinition;
        Owner = Helpers.ThrowIfNull(owner);
        ApplierType = applierType;
        Config = config;

        var applierInstance = (IAotMonoDetourHookApplier)Activator.CreateInstance(applierType)!;
        applierInstance.AotHook = this;
        aotHookApplier = applierInstance;

        owner.Hooks.Add(this);

        lock (tableLock)
        {
            if (TargetToHooks.TryGetValue(Target, out var hooks))
                hooks.Add(this);
            else
                TargetToHooks.Add(Target, [this]);
        }

        if (applyByDefault)
        {
            IsApplied = true;
        }

        // Unlike runtime detour, AOT hooks are all applied at once later.
    }

    /// <summary>
    /// Constructs a <see cref="AotMonoDetourHook"/> with an applier defined by
    /// <typeparamref name="TApplier"/>.
    /// </summary>
    /// <typeparam name="TApplier">The <see cref="IMonoDetourHookApplier"/>
    /// type to define how to apply this hook.</typeparam>
    /// <returns>
    /// A new <see cref="AotMonoDetourHook"/>.
    /// </returns>
    /// <inheritdoc cref="AotMonoDetourHook(MethodDefinition, MethodBase, MethodDefinition, Type, AotMonoDetourManager, MonoDetourConfig, bool)"/>
    public static AotMonoDetourHook Create<TApplier>(
        MethodDefinition target,
        Delegate manipulatorBase,
        AotMonoDetourManager owner,
        MonoDetourConfig? config = null,
        bool applyByDefault = true
    )
        where TApplier : class, IAotMonoDetourHookApplier =>
        new(
            target,
            Helpers.ThrowIfNull(manipulatorBase).Method,
            null,
            typeof(TApplier),
            owner,
            config,
            applyByDefault
        );

    /// <inheritdoc cref="Create{TApplier}(MethodDefinition, Delegate, AotMonoDetourManager, MonoDetourConfig?, bool)"/>
    public static AotMonoDetourHook Create<TApplier>(
        MethodDefinition target,
        MethodBase manipulatorBase,
        AotMonoDetourManager owner,
        MonoDetourConfig? config = null,
        bool applyByDefault = true
    )
        where TApplier : class, IAotMonoDetourHookApplier =>
        new(
            target,
            Helpers.ThrowIfNull(manipulatorBase),
            null,
            typeof(TApplier),
            owner,
            config,
            applyByDefault
        );

    /// <inheritdoc cref="Create{TApplier}(MethodDefinition, Delegate, AotMonoDetourManager, MonoDetourConfig?, bool)"/>
    public static AotMonoDetourHook Create<TApplier>(
        MethodDefinition target,
        MethodDefinition manipulatorDefinition,
        AotMonoDetourManager owner,
        MonoDetourConfig? config = null,
        bool applyByDefault = true
    )
        where TApplier : class, IAotMonoDetourHookApplier =>
        new(
            target,
            null,
            Helpers.ThrowIfNull(manipulatorDefinition),
            typeof(TApplier),
            owner,
            config,
            applyByDefault
        );

    /// <inheritdoc/>
    public void Apply() => IsApplied = true;

    /// <inheritdoc/>
    public void Undo() => IsApplied = false;

    internal void Manipulate(MethodDefinition method)
    {
        aotHookApplier.ApplierManipulator(method);
    }
}
