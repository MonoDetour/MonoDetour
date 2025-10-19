using System;
using System.Reflection;
using System.Threading;
using MonoDetour.Cil;
using MonoDetour.DetourTypes.Manipulation;
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
            invoker =
                (ILManipulationInfo.Manipulator?)_hook.ManipulatorDelegate
                ?? (ILManipulationInfo.Manipulator)
                    Delegate.CreateDelegate(
                        typeof(ILManipulationInfo.Manipulator),
                        Hook.Manipulator as MethodInfo
                            ?? throw new InvalidCastException(
                                $"{nameof(Hook)} {nameof(Hook.Manipulator)} method is not {nameof(MethodInfo)}!"
                            )
                    );
        }
    }
    IReadOnlyMonoDetourHook _hook = null!;
    ILManipulationInfo.Manipulator invoker = null!;

    /// <inheritdoc/>
    public void ApplierManipulator(ILContext il)
    {
        var originalInstructions = HookTargetRecords.GetOriginalInstructions(il.Method);
        ILManipulationInfo info = new(il, Hook.Target, originalInstructions);

        invoker(info);

        Utils.DebugValidateCILValidatorNoErrors(Hook, il.Body);
    }
}
