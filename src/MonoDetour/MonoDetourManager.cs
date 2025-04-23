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
public class MonoDetourManager
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
    public event Action<MonoDetourInfo>? OnDispose;

    internal bool CallOnDispose(MonoDetourInfo info)
    {
        if (OnDispose is null)
            return false;

        OnDispose?.Invoke(info);
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
    /// Cleans up, undoes and gets rid of all hooks belonging to this manager.
    /// Use this is you never want to see those hooks again.
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

    /// <summary>
    /// Disposes the <see cref="MonoDetourManager"/> and its hooks.
    /// </summary>
    ~MonoDetourManager()
    {
        DisposeHooks();
    }
}
