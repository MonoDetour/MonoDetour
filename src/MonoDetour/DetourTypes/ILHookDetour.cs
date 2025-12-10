using System;
using System.Reflection;
using MonoDetour.Cil;
using MonoDetour.Cil.Analysis;
using MonoDetour.DetourTypes.Manipulation;
using MonoDetour.Logging;
using MonoMod.Cil;

namespace MonoDetour.DetourTypes;

/// <summary>
/// Implements MonoDetour support for a <see cref="MonoMod.RuntimeDetour.ILHook"/>
/// which supports modifying the target method on the CIL level with a manipulator method
/// of type <see cref="ILManipulationInfo.Manipulator"/>.
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
        ILManipulationInfo info = new(il, Hook.Target);

        invoker(info);

        Hook.Owner.Log(
            MonoDetourLogger.LogChannel.IL,
            () =>
            {
                var body = il.Body.CreateInformationalSnapshotJIT().AnnotateErrors();
                return $"Manipulated by ILHook: {Hook.Manipulator.Name} ({Hook.Owner.Id}):\n{body}";
            }
        );

        Utils.DebugValidateCILValidatorNoErrors(Hook, il.Body);
    }
}
