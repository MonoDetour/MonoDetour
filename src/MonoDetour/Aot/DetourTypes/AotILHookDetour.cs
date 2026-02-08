using System;
using System.Reflection;
using Mono.Cecil;
using MonoDetour.Cil;
using MonoDetour.Cil.Analysis;
using MonoDetour.Logging;
using MonoMod.Cil;

namespace MonoDetour.Aot.DetourTypes;

/// <summary>
/// Implements MonoDetour support for a <see cref="MonoMod.RuntimeDetour.ILHook"/>
/// which supports modifying the target method on the CIL level with a manipulator method
/// of type <see cref="ILManipulationInfo.Manipulator"/>.
/// </summary>
public class AotILHookDetour : IAotMonoDetourHookApplier
{
    /// <inheritdoc/>
    public IReadOnlyAotMonoDetourHook AotHook
    {
        get => _aotHook;
        set
        {
            _aotHook = value;

            if (value.ManipulatorBase is MethodInfo { } methodInfo)
            {
                invoker = methodInfo.CreateDelegate<ILManipulationInfo.Manipulator>();
            }
            else
            {
                throw new NotSupportedException(
                    $"{nameof(AotILHookDetour)} only supports a manipulator of type {nameof(MethodInfo)}"
                );
            }
        }
    }
    IReadOnlyAotMonoDetourHook _aotHook = null!;
    ILManipulationInfo.Manipulator invoker = null!;

    /// <inheritdoc/>
    public void ApplierManipulator(MethodDefinition method)
    {
        var il = new ILContext(method);
        ILManipulationInfo info = new(il, null, out var onFinish);

        invoker(info);
        onFinish();

        AotHook.Owner.Log(
            MonoDetourLogger.LogChannel.IL,
            () =>
            {
                var body = il.Body.CreateInformationalSnapshotJIT().AnnotateErrors();
                return $"Manipulated by AotILHook: {AotHook.ManipulatorBase!.Name} ({AotHook.Owner.Id}):\n{body}";
            }
        );
    }
}
