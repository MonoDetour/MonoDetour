using System;
using System.Collections.Generic;
using System.Reflection;
using MonoDetour.Cil;
using MonoDetour.DetourTypes;
using MonoDetour.Logging;

namespace MonoDetour;

/// <summary>
/// A manager for <see cref="MonoDetourHook"/>s.
/// </summary>
/// <param name="id">
/// The identifier for this manager. This will be used as
/// the identifier in <see cref="MonoDetourConfig"/> by default.<br/>
/// This ID should be unique per mod, such as the assembly name, but
/// a single mod can use the same ID for all its <see cref="MonoDetourManager"/>s.
/// </param>
public class MonoDetourManager(string id) : IDisposable, IMonoDetourLogSource
{
    /// <summary>
    /// Identifier for a <see cref="MonoDetourManager"/>.
    /// This will be used as the identifier in <see cref="MonoDetourConfig"/> by default.<br/>
    /// This ID should be unique per mod such as the assembly name, but
    /// a single mod can use the same ID for all its <see cref="MonoDetourManager"/>s.
    /// </summary>
    public string Id { get; } = Helpers.ThrowIfNull(id);

    /// <inheritdoc/>
    public MonoDetourLogger.LogChannel LogFilter { get; set; } =
        MonoDetourLogger.LogChannel.Warning | MonoDetourLogger.LogChannel.Error;

    /// <summary>
    /// The hooks applied by this MonoDetourManager.
    /// </summary>
    public List<IMonoDetourHook> Hooks { get; } = [];

    /// <summary>
    /// An event which is called when a hook owned by this <see cref="MonoDetourManager"/>
    /// throws, just before all hooks from the <see cref="MonoDetourManager"/> are disposed
    /// as a consequence.<br/>
    /// <br/>
    /// Use this event for cleaning up related resources to help prevent
    /// as much damage as possible.<br/>
    /// <br/>
    /// The hook which threw is passed as the only argument.
    /// </summary>
    public event Action<IReadOnlyMonoDetourHook>? OnHookThrew;

    bool isDisposed = false;

    void ThrowIfDisposed()
    {
        if (!isDisposed)
            return;

        throw new ObjectDisposedException(ToString());
    }

    internal bool CallOnHookThrew(IReadOnlyMonoDetourHook hook)
    {
        if (OnHookThrew is null)
            return false;

        OnHookThrew?.Invoke(hook);
        return true;
    }

    /// <summary>
    /// Invokes hook initializers for the specified assembly.
    /// </summary>
    /// <param name="assembly">The assembly whose hook initializers to invoke.</param>
    /// <remarks>
    /// If a hook initializer throws, this method throws.
    /// </remarks>
    public static void InvokeHookInitializers(Assembly assembly)
    {
        foreach (Type type in MonoDetourUtils.GetTypesFromAssembly(assembly))
        {
            if (!MonoDetourUtils.TryGetCustomAttribute<IMonoDetourTargets>(type, out _))
                continue;

            InvokeHookInitializers(type);
        }
    }

    /// <summary>
    /// Invokes hook initializers for the specified type.
    /// </summary>
    /// <param name="type">The type whose hook initializers to invoke.</param>
    /// <inheritdoc cref="InvokeHookInitializers(Assembly)"/>
    public static void InvokeHookInitializers(Type type)
    {
        MethodInfo[] methods = type.GetMethods((BindingFlags)~0);
        foreach (var method in methods)
        {
            if (
                !MonoDetourUtils.TryGetCustomAttribute<MonoDetourHookInitializeAttribute>(
                    method,
                    out _
                )
            )
                continue;

            method.Invoke(null, null);
        }
    }

    /// <summary>
    /// Applies all hooks belonging to this manager.
    /// </summary>
    /// <remarks>
    /// By default, a <see cref="MonoDetourManager"/> won't have any hooks.
    /// You need to initialize the hooks first, either calling them manually or using
    /// <see cref="InvokeHookInitializers(Assembly)"/> or any of its overloads.
    /// </remarks>
    public void ApplyHooks() => Hooks.ForEach(x => x.Apply());

    /// <summary>
    /// Undoes all applied hooks belonging to this manager.
    /// </summary>
    public void UndoHooks() => Hooks.ForEach(x => x.Undo());

    /// <summary>
    /// Undoes and disposes all hooks belonging to this manager.
    /// </summary>
    public void DisposeHooks()
    {
        Hooks.ForEach(x => x.Dispose());
        Hooks.Clear();
    }

    /// <inheritdoc cref="ILHook(MethodBase, ILManipulationInfo.Manipulator, MonoDetourConfig?, bool)"/>
    public MonoDetourHook ILHook(
        Delegate target,
        ILManipulationInfo.Manipulator manipulator,
        MonoDetourConfig? config = null,
        bool applyByDefault = true
    ) => ILHook(target.Method, manipulator, config, applyByDefault);

    /// <summary>
    /// Creates a <see cref="ILHookDetour"/> hook using the information defined.
    /// </summary>
    /// <param name="manipulator">The manipulator method.</param>
    /// <param name="config">The priority configuration for this hook.</param>
    /// <inheritdoc cref="Hook(MethodBase, MethodBase, MonoDetourConfig, bool)"/>
    /// <param name="target"/>
    /// <param name="applyByDefault"/>
    public MonoDetourHook ILHook(
        MethodBase target,
        ILManipulationInfo.Manipulator manipulator,
        MonoDetourConfig? config = null,
        bool applyByDefault = true
    )
    {
        return Hook<ILHookDetour>(target, manipulator, config, applyByDefault);
    }

    /// <inheritdoc cref="Hook(MethodBase, MethodBase, MonoDetourConfig, bool)"/>
    public MonoDetourHook Hook<TApplier>(
        Delegate target,
        Delegate manipulator,
        MonoDetourConfig? config = null,
        bool applyByDefault = true
    )
        where TApplier : class, IMonoDetourHookApplier =>
        Hook<TApplier>(target.Method, manipulator, config, applyByDefault);

    /// <inheritdoc cref="Hook(MethodBase, MethodBase, MonoDetourConfig, bool)"/>
    public MonoDetourHook Hook<TApplier>(
        MethodBase target,
        Delegate manipulator,
        MonoDetourConfig? config = null,
        bool applyByDefault = true
    )
        where TApplier : class, IMonoDetourHookApplier
    {
        ThrowIfDisposed();
        return MonoDetourHook.Create<TApplier>(target, manipulator, this, config, applyByDefault);
    }

    /// <summary>
    /// Creates a MonoDetour Hook using the information defined.
    /// </summary>
    /// <remarks>
    /// This method is not intended to be used directly, but is instead
    /// used by MonoDetour's HookGen.
    /// </remarks>
    /// <typeparam name="TApplier">The <see cref="IMonoDetourHookApplier"/>
    /// type to define how to apply this hook.</typeparam>
    /// <param name="target">The method to be hooked.</param>
    /// <param name="manipulator">The hook or manipulator method.</param>
    /// <param name="config">Metadata configuration for the MonoDetour Hook.</param>
    /// <param name="applyByDefault">Whether or not the hook should be applied
    /// after it has been constructed.</param>
    /// <returns>The hook.</returns>
    public MonoDetourHook Hook<TApplier>(
        MethodBase target,
        MethodBase manipulator,
        MonoDetourConfig? config = null,
        bool applyByDefault = true
    )
        where TApplier : class, IMonoDetourHookApplier
    {
        ThrowIfDisposed();
        return MonoDetourHook.Create<TApplier>(target, manipulator, this, config, applyByDefault);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        DisposeHooks();
        GC.SuppressFinalize(this);

        isDisposed = true;
    }
}
