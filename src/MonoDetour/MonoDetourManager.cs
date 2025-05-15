using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using MonoDetour.DetourTypes;
using MonoDetour.Interop.RuntimeDetour;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace MonoDetour;

/// <summary>
/// A manager for your MonoDetour hooks.
/// </summary>
public class MonoDetourManager : IDisposable
{
    /// <summary>
    /// The hooks applied by this MonoDetourManager.
    /// </summary>
    public List<MonoDetourHook> MonoDetourHooks { get; } = [];

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
    public event Action<MonoDetourHook>? OnHookThrew;

    bool isDisposed = false;

    void ThrowIfDisposed()
    {
        if (!isDisposed)
            return;

        throw new ObjectDisposedException(ToString());
    }

    internal bool CallOnHookThrew(MonoDetourHook hook)
    {
        if (OnHookThrew is null)
            return false;

        OnHookThrew?.Invoke(hook);
        return true;
    }

    /// <summary>
    /// Invokes hook initializers for the assembly that calls this method.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void InvokeHookInitializers() => InvokeHookInitializers(Assembly.GetCallingAssembly());

    /// <summary>
    /// Invokes hook initializers for the specified assembly.
    /// </summary>
    /// <param name="assembly">The assembly whose hook initializers to invoke.</param>
    public void InvokeHookInitializers(Assembly assembly)
    {
        foreach (Type type in MonoDetourUtils.GetTypesFromAssembly(assembly))
        {
            if (!MonoDetourUtils.TryGetCustomAttribute<MonoDetourTargetsAttribute>(type, out _))
                continue;

            InvokeHookInitializers(type);
        }
    }

    /// <summary>
    /// Invokes hook initializers for the specified type.
    /// </summary>
    /// <param name="type">The type whose hook initializers to invoke.</param>
    public void InvokeHookInitializers(Type type)
    {
        MethodInfo[] methods = type.GetMethods((BindingFlags)~0);
        foreach (var method in methods)
        {
            if (!MonoDetourUtils.TryGetCustomAttribute<MonoDetourHookInitAttribute>(method, out _))
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
    /// <see cref="InvokeHookInitializers()"/> or any of its overloads.
    /// </remarks>
    public void ApplyHooks() => MonoDetourHooks.ForEach(x => x.Apply());

    /// <summary>
    /// Undoes all applied hooks belonging to this manager.
    /// </summary>
    public void UndoHooks() => MonoDetourHooks.ForEach(x => x.Undo());

    /// <summary>
    /// Undoes and disposes all hooks belonging to this manager.
    /// </summary>
    public void DisposeHooks()
    {
        MonoDetourHooks.ForEach(x => x.Dispose());
        MonoDetourHooks.Clear();
    }

    /// <inheritdoc cref="ILHook(MethodBase, ILContext.Manipulator, MonoDetourPriority?, bool)"/>
    public MonoDetourHook ILHook(
        Delegate target,
        ILContext.Manipulator manipulator,
        MonoDetourPriority? detourPriority = null,
        bool applyByDefault = true
    ) => ILHook(target.Method, manipulator, detourPriority, applyByDefault);

    /// <summary>
    /// Creates a <see cref="ILHookDetour"/> hook using the information defined.
    /// </summary>
    /// <param name="manipulator">The manipulator method.</param>
    /// <param name="detourPriority">The priority configuration for this hook.</param>
    /// <inheritdoc cref="Hook(MethodBase, MethodBase, MonoDetourConfig, bool)"/>
    /// <param name="target"/>
    /// <param name="applyByDefault"/>
    public MonoDetourHook ILHook(
        MethodBase target,
        ILContext.Manipulator manipulator,
        MonoDetourPriority? detourPriority = null,
        bool applyByDefault = true
    )
    {
        var config = MonoDetourConfig.Create<ILHookDetour>(detourPriority);
        return Hook(target, manipulator.Method, config, applyByDefault);
    }

    /// <inheritdoc cref="Hook(MethodBase, MethodBase, MonoDetourConfig, bool)"/>
    public MonoDetourHook Hook(
        Delegate target,
        Delegate manipulator,
        MonoDetourConfig config,
        bool applyByDefault = true
    ) => Hook(target.Method, manipulator.Method, config, applyByDefault);

    /// <inheritdoc cref="Hook(MethodBase, MethodBase, MonoDetourConfig, bool)"/>
    public MonoDetourHook Hook(
        MethodBase target,
        Delegate manipulator,
        MonoDetourConfig config,
        bool applyByDefault = true
    ) => Hook(target, manipulator.Method, config, applyByDefault);

    /// <summary>
    /// Creates a MonoDetour Hook using the information defined.
    /// </summary>
    /// <remarks>
    /// This method is not intended to be used directly, but is instead
    /// used by MonoDetour's HookGen.
    /// </remarks>
    /// <param name="target">The method to be hooked.</param>
    /// <param name="manipulator">The hook or manipulator method.</param>
    /// <param name="config">Metadata configuration for the MonoDetour Hook.</param>
    /// <param name="applyByDefault">Whether or not the hook should be applied
    /// after it has been constructed.</param>
    /// <returns>The hook.</returns>
    public MonoDetourHook Hook(
        MethodBase target,
        MethodBase manipulator,
        MonoDetourConfig config,
        bool applyByDefault = true
    )
    {
        ThrowIfDisposed();
        Helpers.ThrowIfNull(target);
        Helpers.ThrowIfNull(manipulator);
        Helpers.ThrowIfNull(config);

        var applierInstance = (IMonoDetourHookApplier)Activator.CreateInstance(config.DetourType);
        ILHook applierILHook = ProxyILHookConstructor.ConstructILHook(
            target,
            applierInstance.ApplierManipulator,
            config.DetourPriority
        );
        MonoDetourHook monoDetourHook = new(target, manipulator, this, config, applierILHook);
        applierInstance.Hook = monoDetourHook;
        MonoDetourHooks.Add(monoDetourHook);

        if (applyByDefault)
        {
            applierILHook.Apply();
        }

        return monoDetourHook;
    }

    void Dispose(bool disposing)
    {
        if (isDisposed)
        {
            return;
        }

        if (disposing)
        {
            DisposeHooks();
        }

        isDisposed = true;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
