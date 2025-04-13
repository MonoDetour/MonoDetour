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
    /// The log level for a <see cref="MonoDetourManager"/>.
    /// </summary>
    public enum Logging
    {
        /// <summary>
        /// Nothing will be logged.
        /// </summary>
        None,

        /// <summary>
        /// Logs a lot of information useful for debugging MonoDetour itself.
        /// </summary>
        Diagnostic,
    }

    /// <summary>
    /// The log level for this <see cref="MonoDetourManager"/>.
    /// </summary>
    public Logging LogLevel { get; set; } = Logging.None;

    /// <summary>
    /// The hooks applied by this MonoDetourManager.
    /// </summary>
    public List<ILHook> ILHooks { get; } = [];

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
    /// Undoes all applied hooks.
    /// </summary>
    public void UndoHooks() => ILHooks.ForEach(x => x.Undo());

    /// <summary>
    /// Cleans up, undoes and gets rid of all hooks. Use this is you never want to see these hooks again.
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
}
