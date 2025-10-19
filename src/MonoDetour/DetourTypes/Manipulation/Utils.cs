using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using Mono.Cecil.Cil;
using MonoDetour.Cil;
using MonoDetour.Cil.Analysis;
using MonoDetour.Logging;
using MonoDetour.Reflection.Unspeakable;

namespace MonoDetour.DetourTypes.Manipulation;

static class Utils
{
    static void WriteSpeakableEnumerator(
        ILWeaver w,
        Type speakableEnumeratorType,
        Type enumeratorType
    )
    {
        var preBuildMethod = speakableEnumeratorType.GetMethod("PreBuildFieldReferenceGetters")!;
        preBuildMethod.Invoke(null, [enumeratorType]);

        MethodInfo method = speakableEnumeratorType.GetMethod("GetOrCreate")!;
        w.InsertBeforeCurrent(w.Create(OpCodes.Call, method));
    }

    public static bool ModifiesControlFlow(this IReadOnlyMonoDetourHook hook) =>
        hook.Manipulator is MethodInfo mi && mi.ReturnType == typeof(ReturnFlow);

    public static void EmitParamsAndReturnValueBeforeCurrent(
        this ILWeaver w,
        VariableDefinition returnValue,
        IReadOnlyMonoDetourHook hook
    )
    {
        Helpers.ThrowIfNull(returnValue);

        w.EmitParamsBeforeCurrent(hook);

        // Push return value address to the stack
        // so it can be manipulated by a hook.
        w.InsertBeforeCurrent(w.Create(OpCodes.Ldloca, returnValue));
    }

    public static void EmitParamsBeforeCurrent(this ILWeaver w, IReadOnlyMonoDetourHook hook)
    {
        bool isStatic = hook.Target.IsStatic;

        foreach (var origParam in w.Method.Parameters)
        {
            bool isThis = !isStatic && origParam.Index == 0;

            if (isThis)
            {
                // Having this logic here is kinda dirty.
                var firstArg = hook.Manipulator.GetParameters().First().ParameterType;
                if (typeof(ISpeakableEnumerator).IsAssignableFrom(firstArg))
                {
                    var targetDeclaringType =
                        hook.Target.DeclaringType
                        ?? throw new NullReferenceException(
                            "Declaring type of target method is null!"
                        );

                    w.InsertBeforeCurrent(w.Create(OpCodes.Ldarg, origParam.Index));
                    WriteSpeakableEnumerator(w, firstArg, targetDeclaringType);
                }
                else
                {
                    w.InsertBeforeCurrent(w.Create(OpCodes.Ldarg, origParam.Index));
                }
            }
            // 'this', and reference types must not be passed by reference.
            else if (origParam.ParameterType.IsByReference)
            {
                w.InsertBeforeCurrent(w.Create(OpCodes.Ldarg, origParam.Index));
            }
            else
            {
                w.InsertBeforeCurrent(w.Create(OpCodes.Ldarga, origParam.Index));
            }
        }
    }

    internal static void DisposeBadHooks(Exception ex, IReadOnlyMonoDetourHook hook)
    {
        MethodBase manipulator = hook.Manipulator;
        MethodBase target = hook.Target;
        string? targetTypeName = target.DeclaringType?.FullName;

        hook.Owner.Log(
            MonoDetourLogger.LogChannel.Error,
            () =>
                $"Hook '{manipulator}' targeting method '{target}' from type '{targetTypeName}'"
                + $" threw an exception, and its {nameof(MonoDetourManager)}'s hooks will be disposed.\n"
                + $"The Exception that was thrown: {ex}"
        );
        try
        {
            bool hadHandler = hook.Owner.CallOnHookThrew(hook);

            if (!hadHandler)
            {
                hook.Owner.Log(
                    MonoDetourLogger.LogChannel.Warning,
                    () =>
                        $"No disposal event handler for the {nameof(MonoDetourManager)} was registered."
                );
            }
        }
        catch (Exception disposalEx)
        {
            hook.Owner.Log(
                MonoDetourLogger.LogChannel.Error,
                () => $"Disposal event handler threw an exception:\n{disposalEx}"
            );
        }
        finally
        {
            hook.Owner.DisposeHooks();
        }
    }

    internal static void ThrowIfHookManipulatorDelegateIsNull(
        IReadOnlyMonoDetourHook hook,
        out Delegate manipulatorDelegate
    )
    {
        if (hook.ManipulatorDelegate is { } @delegate)
        {
            manipulatorDelegate = @delegate;
            return;
        }

        throw new NullReferenceException(
            $"{nameof(IReadOnlyMonoDetourHook)}.{nameof(IReadOnlyMonoDetourHook.Manipulator)} "
                + "is not static, and "
                + $"{nameof(IReadOnlyMonoDetourHook)}.{nameof(IReadOnlyMonoDetourHook.ManipulatorDelegate)} "
                + "is null. Please use a constructor which takes a Delegate instead "
                + "of a MethodBase for Manipulator."
        );
    }

    internal static Instruction GetCallMaybeInsertGetDelegate(
        ILWeaver w,
        IReadOnlyMonoDetourHook hook,
        int id,
        Func<int, Delegate> getDelegate
    )
    {
        if (hook.Manipulator.IsStatic)
        {
            return w.Create(OpCodes.Call, hook.Manipulator);
        }

        ThrowIfHookManipulatorDelegateIsNull(hook, out var manipulatorDelegate);
        w.InsertBeforeCurrent(w.Create(OpCodes.Ldc_I4, id), w.CreateCall(getDelegate));

        var invoke = manipulatorDelegate.GetType().GetMethod("Invoke")!;
        return w.Create(OpCodes.Callvirt, invoke);
    }

    [Conditional("DEBUG")]
    internal static void DebugValidateCILValidatorNoErrors(
        IReadOnlyMonoDetourHook hook,
        Mono.Cecil.Cil.MethodBody body
    )
    {
        var informationalBody = body.CreateInformationalSnapshotJIT().AnnotateErrors();
        if (informationalBody.HasErrors())
        {
            hook.Owner.Log(
                MonoDetourLogger.LogChannel.Error,
                $"Hook CIL validation failed! {hook.Manipulator.Name}\n{informationalBody.ToErrorMessageString()}"
            );
        }
    }
}
