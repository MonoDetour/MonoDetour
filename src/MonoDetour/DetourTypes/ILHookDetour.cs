using System;
using System.IO;
using System.Reflection;
using Mono.Cecil;
using MonoMod.Cil;

namespace MonoDetour.DetourTypes;

/// <summary>
/// Implements MonoDetour support for a regular <see cref="MonoMod.RuntimeDetour.ILHook"/>
/// which supports modifying the target method on the CIL level.
/// </summary>
public class ILHookDetour : IMonoDetourHookApplier
{
    /// <summary>
    /// The manipulator method for a <see cref="ILHookDetour"/>.
    /// </summary>
    /// <param name="info">
    /// A manipulation info containing the <see cref="ILContext"/> intended for manipulation
    /// and an untouched <see cref="ILContext"/> useful for observing the untouched
    /// state, including the original method as a <see cref="MethodBase"/>.
    /// See <see cref="ManipulationInfo"/> for more.
    /// </param>
    public delegate void Manipulator(ManipulationInfo info);

    /// <inheritdoc/>
    public IReadOnlyMonoDetourHook Hook
    {
        get => _hook;
        set
        {
            _hook = value;
            invoker = (Manipulator)
                Delegate.CreateDelegate(typeof(Manipulator), Hook.Manipulator as MethodInfo);
        }
    }
    IReadOnlyMonoDetourHook _hook = null!;
    Manipulator invoker = null!;

    /// <inheritdoc/>
    public void ApplierManipulator(ILContext il) => invoker(new(il, Hook.Target));
}
