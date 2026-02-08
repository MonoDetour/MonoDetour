using System;
using System.Collections.Generic;
using System.Reflection;
using Mono.Cecil;
using MonoDetour.Aot.DetourTypes;
using MonoDetour.Cil;
using MonoDetour.Logging;

namespace MonoDetour.Aot;

/// <summary>
/// A manager for <see cref="AotMonoDetourHook"/>s.
/// </summary>
/// <param name="id">
/// The identifier for this manager. This will be used as
/// the identifier in <see cref="MonoDetourConfig"/> by default.<br/>
/// This ID should be unique per mod, such as the assembly name, but
/// a single mod can use the same ID for all its <see cref="AotMonoDetourManager"/>s.
/// </param>
public class AotMonoDetourManager(string id) : IMonoDetourLogSource
{
    /// <summary>
    /// Identifier for a <see cref="AotMonoDetourManager"/>.
    /// This will be used as the identifier in <see cref="MonoDetourConfig"/> by default.<br/>
    /// This ID should be unique per mod such as the assembly name, but
    /// a single mod can use the same ID for all its <see cref="AotMonoDetourManager"/>s.
    /// </summary>
    public string Id { get; } = Helpers.ThrowIfNull(id);

    /// <inheritdoc/>
    public MonoDetourLogger.LogChannel LogFilter { get; set; } =
        MonoDetourLogger.LogChannel.Warning | MonoDetourLogger.LogChannel.Error;

    /// <summary>
    /// The hooks applied by this <see cref="AotMonoDetourManager"/>.
    /// </summary>
    public List<AotMonoDetourHook> Hooks { get; } = [];

    /// <summary>
    /// An event which is called when a hook owned by this <see cref="AotMonoDetourManager"/>
    /// throws, just before all hooks from the <see cref="AotMonoDetourManager"/> are disposed
    /// as a consequence.<br/>
    /// <br/>
    /// Use this event for cleaning up related resources to help prevent
    /// as much damage as possible.<br/>
    /// <br/>
    /// The hook which threw is passed as the only argument.
    /// </summary>
    public event Action<IReadOnlyMonoDetourHook>? OnHookThrew;

    internal bool CallOnHookThrew(IReadOnlyMonoDetourHook hook)
    {
        if (OnHookThrew is null)
            return false;

        OnHookThrew.Invoke(hook);
        return true;
    }

    /// <summary>
    /// Invokes hook initializers for the specified assembly.
    /// </summary>
    /// <param name="assembly">The assembly whose hook initializers to invoke.</param>
    /// <param name="reportUnloadableTypes">
    /// Whether or not MonoDetour will log on unloadable types.<br/>
    /// <br/>
    /// If you are aware that sometimes types can't be loaded, e.g. when soft-depending
    /// on another assembly, and you don't need MonoDetour to tell you that this is the reason
    /// your hook initializer isn't running, you can set this value to <see langword="false"/>.
    /// </param>
    /// <remarks>
    /// If a hook initializer throws, this method throws.
    /// </remarks>
    public static void InvokeHookInitializers(
        Assembly assembly,
        bool reportUnloadableTypes = true
    ) => MonoDetourManager.InvokeHookInitializers(assembly, reportUnloadableTypes);

    /// <summary>
    /// Invokes hook initializers for the specified type.
    /// </summary>
    /// <param name="type">The type whose hook initializers to invoke.</param>
    /// <inheritdoc cref="InvokeHookInitializers(Assembly, bool)"/>
    /// <param name="reportUnloadableTypes"></param>
    public static void InvokeHookInitializers(Type type, bool reportUnloadableTypes = true) =>
        MonoDetourManager.InvokeHookInitializers(type, reportUnloadableTypes);

    /// <summary>
    /// Applies all hooks belonging to this manager.
    /// </summary>
    /// <remarks>
    /// By default, an <see cref="AotMonoDetourManager"/> won't have any hooks.
    /// You need to initialize the hooks first, either calling them manually or using
    /// <see cref="InvokeHookInitializers(Assembly, bool)"/>.
    /// </remarks>
    public void ApplyHooks() => Hooks.ForEach(x => x.Apply());

    /// <summary>
    /// Undoes all applied hooks belonging to this manager.
    /// </summary>
    public void UndoHooks() => Hooks.ForEach(x => x.Undo());

    /// <summary>
    /// Creates a <see cref="AotILHookDetour"/> hook using the information defined.
    /// </summary>
    /// <param name="manipulator">The manipulator method.</param>
    /// <param name="config">The priority configuration for this hook.</param>
    /// <inheritdoc cref="Hook(MethodDefinition, MethodBase, MonoDetourConfig, bool)"/>
    /// <param name="target"/>
    /// <param name="applyByDefault"/>
    public AotMonoDetourHook ILHook(
        MethodDefinition target,
        ILManipulationInfo.Manipulator manipulator,
        MonoDetourConfig? config = null,
        bool applyByDefault = true
    )
    {
        return Hook<AotILHookDetour>(target, manipulator, config, applyByDefault);
    }

    /// <inheritdoc cref="Hook(MethodDefinition, MethodBase, MonoDetourConfig, bool)"/>
    public AotMonoDetourHook Hook<TApplier>(
        MethodDefinition target,
        Delegate manipulator,
        MonoDetourConfig? config = null,
        bool applyByDefault = true
    )
        where TApplier : class, IAotMonoDetourHookApplier, new()
    {
        return AotMonoDetourHook.Create<TApplier>(
            target,
            manipulator,
            this,
            config,
            applyByDefault
        );
    }

    /// <summary>
    /// Creates an AOT MonoDetour Hook using the information defined.
    /// </summary>
    /// <remarks>
    /// This method is not intended to be used directly, but is instead
    /// used by MonoDetour's HookGen.
    /// </remarks>
    /// <typeparam name="TApplier">The <see cref="IAotMonoDetourHookApplier"/>
    /// type to define how to apply this hook.</typeparam>
    /// <param name="target">The method to be hooked.</param>
    /// <param name="manipulator">The hook or manipulator method.</param>
    /// <param name="config">Metadata configuration for the MonoDetour Hook.</param>
    /// <param name="applyByDefault">Whether or not the hook should be applied
    /// after it has been constructed.</param>
    /// <returns>The hook.</returns>
    public AotMonoDetourHook Hook<TApplier>(
        MethodDefinition target,
        MethodBase manipulator,
        MonoDetourConfig? config = null,
        bool applyByDefault = true
    )
        where TApplier : class, IAotMonoDetourHookApplier, new()
    {
        return AotMonoDetourHook.Create<TApplier>(
            target,
            manipulator,
            this,
            config,
            applyByDefault
        );
    }
}
