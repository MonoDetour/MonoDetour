using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
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
    /// Hooks all MonoDetour methods in the assembly that calls this method.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void HookAll() => HookAll(Assembly.GetCallingAssembly());

    /// <summary>
    /// Hooks all MonoDetour methods in the specified assembly.
    /// </summary>
    /// <param name="assembly">The assembly whose MonoDetour hooks to apply.</param>
    public void HookAll(Assembly assembly)
    {
        foreach (Type type in MonoDetourUtils.GetTypesFromAssembly(assembly))
        {
            if (!MonoDetourUtils.TryGetCustomAttribute<MonoDetourTargetsAttribute>(type, out _))
                continue;

            HookAll(type);
        }
    }

    /// <summary>
    /// Hooks all MonoDetour methods in the specified type.
    /// </summary>
    /// <param name="type">The type whose MonoDetour hooks to apply.</param>
    public void HookAll(Type type)
    {
        MethodInfo[] methods = type.GetMethods((BindingFlags)~0);
        foreach (var method in methods)
        {
            if (!MonoDetourUtils.TryGetCustomAttribute<MonoDetourHookAttribute>(method, out _))
                continue;

            HookGenReflectedHook(method);
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
    /// <inheritdoc cref="HookGenReflectedHook(MethodBase, MethodBase, MonoDetourInfo?)"/>
    public ILHook Hook(MethodBase target, ILContext.Manipulator manipulator)
    {
        var ilHook = new ILHook(target, manipulator);
        ILHooks.Add(ilHook);
        return ilHook;
    }

    /// <inheritdoc cref="HookGenReflectedHook(MethodBase, MethodBase, MonoDetourInfo?)"/>
    public ILHook HookGenReflectedHook(Delegate manipulator, MonoDetourInfo? info = null)
    {
        Helpers.ThrowIfNull(manipulator);
        return HookGenReflectedHook(manipulator.Method, info);
    }

    /// <inheritdoc cref="HookGenReflectedHook(MethodBase, MethodBase, MonoDetourInfo?)"/>
    public ILHook HookGenReflectedHook(MethodBase manipulator, MonoDetourInfo? info = null)
    {
        Helpers.ThrowIfNull(manipulator);

        if (!MonoDetourUtils.TryGetMonoDetourParameter(manipulator, out _, out var parameterType))
            throw new Exception("Manipulator method must have only one parameter.");

        if (parameterType.DeclaringType is null)
            throw new Exception("DeclaringType of Manipulator method's parameter Type is null.");

        var targetMethod =
            parameterType.DeclaringType.GetMethod("Target")
            ?? throw new Exception(
                "DeclaringType of Manipulator method's parameter Type does not have a method 'Target'."
            );

        var targetReturnValue = targetMethod.Invoke(null, null);
        if (targetReturnValue is not MethodBase returnedTargetMethod)
            throw new Exception(
                "'Target' method in DeclaringType of Manipulator method's parameter Type doesn't return a MethodBase."
            );

        return HookGenReflectedHook(returnedTargetMethod, manipulator, info);
    }

    /// <summary>
    /// Uses reflection to gain all the required information to then
    /// apply a MonoDetour hook with the assumption that the manipulator
    ///  method has a valid signature and its parameter follows the expected
    /// structure of MonoDetour's HookGen.
    /// </summary>
    /// <remarks>
    /// This method is not intended to be used directly, but is instead
    /// used by MonoDetour's HookGen.
    /// </remarks>
    /// <param name="target">The method to be hooked.</param>
    /// <param name="manipulator">The hook or manipulator method.</param>
    /// <param name="info">Metadata configuration for the MonoDetour Hook.</param>
    /// <returns>The hook.</returns>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="Exception"></exception>
    public ILHook HookGenReflectedHook(
        MethodBase target,
        MethodBase manipulator,
        MonoDetourInfo? info = null
    )
    {
        Helpers.ThrowIfNull(target);
        Helpers.ThrowIfNull(manipulator);

        if (info is null || info.DetourType is null)
        {
            if (
                !MonoDetourUtils.TryGetCustomAttribute<MonoDetourHookAttribute>(
                    manipulator,
                    out var monoDetourAttribute
                )
            )
                throw new ArgumentException($"Missing {nameof(MonoDetourHookAttribute)}");

            if (info is null)
                info = new(monoDetourAttribute.Info.DetourType);
            else
            {
                info.DetourType = monoDetourAttribute.Info.DetourType;
                MonoDetourUtils.ThrowIfInvalidDetourType(info.DetourType);
            }
        }

        if (
            !MonoDetourUtils.TryGetMonoDetourParameter(
                manipulator,
                out var parameterInfo,
                out var structType
            )
        )
            throw new Exception("Manipulator method must have only one parameter.");

        FieldInfo[] fields = structType.GetFields(BindingFlags.Public | BindingFlags.Instance);

        if (fields.Length == 0)
            throw new Exception(
                "The Manipulator method parameter type has no public instance fields."
            );

        MonoDetourData data = info.Data;
        data.Target = target;
        data.Manipulator = manipulator;
        data.ManipulatorParameter = parameterInfo;
        data.ManipulatorParameterType = structType;
        data.ManipulatorParameterTypeFields = fields;

        return HookGenReflectedHook(info);
    }

    /// <summary>
    /// Applies a MonoDetour Hook using the information defined.
    /// </summary>
    /// <inheritdoc cref="HookGenReflectedHook(MethodBase, MethodBase, MonoDetourInfo?)"/>
    public ILHook HookGenReflectedHook(MonoDetourInfo info)
    {
        Helpers.ThrowIfNull(info);

        info.Data.Owner = this;

        if (!info.Data.IsInitialized())
            throw new ArgumentException($"{nameof(MonoDetourInfo)} is not fully initialized.");

        var emitter = (IMonoDetourHookEmitter)Activator.CreateInstance(info.DetourType)!;
        emitter.Info = info;

        ILHook iLHook = new(info.Data.Target, emitter.Manipulator);
        ILHooks.Add(iLHook);
        return iLHook;
    }
}
