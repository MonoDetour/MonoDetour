using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using MonoDetour.DetourTypes;
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
    public List<ILHook> ILHooks { get; } = [];

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
    public event Action<MonoDetourInfo>? OnHookThrew;

    bool isDisposed = false;

    void ThrowIfDisposed()
    {
        if (!isDisposed)
            return;

        throw new ObjectDisposedException(ToString());
    }

    internal bool CallOnHookThrew(MonoDetourInfo info)
    {
        if (OnHookThrew is null)
            return false;

        OnHookThrew?.Invoke(info);
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
    public void ApplyHooks() => ILHooks.ForEach(x => x.Apply());

    /// <summary>
    /// Undoes all applied hooks belonging to this manager.
    /// </summary>
    public void UndoHooks() => ILHooks.ForEach(x => x.Undo());

    /// <summary>
    /// Undoes and disposes all hooks belonging to this manager.
    /// </summary>
    public void DisposeHooks()
    {
        ILHooks.ForEach(x => x.Dispose());
        ILHooks.Clear();
    }

    /// <summary>
    /// Applies a regular <see cref="ILHook"/>.
    /// </summary>
    /// <inheritdoc cref="Hook(MethodBase, MethodBase, MonoDetourInfo?)"/>
    public ILHook Hook(MethodBase target, ILContext.Manipulator manipulator)
    {
        // TODO: make this call Hook(MethodBase target, MethodBase manipulator, MonoDetourInfo info)
        ThrowIfDisposed();
        var ilHook = new ILHook(target, manipulator);
        ILHooks.Add(ilHook);
        return ilHook;
    }

    /// <inheritdoc cref="Hook(MethodBase, MethodBase, MonoDetourInfo)"/>
    public ILHook Hook(Delegate target, Delegate manipulator, MonoDetourInfo info) =>
        Hook(target.Method, manipulator.Method, info);

    /// <inheritdoc cref="Hook(MethodBase, MethodBase, MonoDetourInfo)"/>
    public ILHook Hook(MethodBase target, Delegate manipulator, MonoDetourInfo info) =>
        Hook(target, manipulator.Method, info);

    /// <summary>
    /// Applies a MonoDetour Hook using the information defined.
    /// </summary>
    /// <remarks>
    /// This method is not intended to be used directly, but is instead
    /// used by MonoDetour's HookGen.
    /// </remarks>
    /// <param name="target">The method to be hooked.</param>
    /// <param name="manipulator">The hook or manipulator method.</param>
    /// <param name="info">Metadata configuration for the MonoDetour Hook.</param>
    /// <returns>The hook.</returns>
    public ILHook Hook(MethodBase target, MethodBase manipulator, MonoDetourInfo info)
    {
        ThrowIfDisposed();
        Helpers.ThrowIfNull(target);
        Helpers.ThrowIfNull(manipulator);
        Helpers.ThrowIfNull(info);

        info.Data.Owner = this;
        info.Data.Target = target;
        info.Data.Manipulator = manipulator;

        var emitter = (IMonoDetourHookEmitter)Activator.CreateInstance(info.DetourType)!;
        emitter.Info = info;

        ILHook iLHook = new(info.Data.Target, emitter.Manipulator);
        ILHooks.Add(iLHook);
        return iLHook;
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
