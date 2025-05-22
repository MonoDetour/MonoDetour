using System;
using System.Reflection;
using MonoDetour.Cil;
using MonoMod.Cil;

namespace MonoDetour.DetourTypes;

/// <summary>
/// Implements MonoDetour support for a regular <see cref="MonoMod.RuntimeDetour.ILHook"/>
/// which supports modifying the target method on the CIL level.
/// </summary>
public class ILHookDetour : IMonoDetourHookApplier
{
    /// <inheritdoc/>
    public IReadOnlyMonoDetourHook Hook
    {
        get => _hook;
        set
        {
            _hook = value;
            invoker = (ILManipulationInfo.Manipulator)
                Delegate.CreateDelegate(
                    typeof(ILManipulationInfo.Manipulator),
                    Hook.Manipulator as MethodInfo
                );
        }
    }
    IReadOnlyMonoDetourHook _hook = null!;
    ILManipulationInfo.Manipulator invoker = null!;

    /// <inheritdoc/>
    public void ApplierManipulator(ILContext il) => invoker(new(il, Hook.Target));
}
