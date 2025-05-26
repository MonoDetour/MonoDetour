using System;
using System.Linq;
using System.Reflection;
using Mono.Cecil.Cil;
using MonoDetour.Cil;
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
        var genericArgs = speakableEnumeratorType.GetGenericArguments();

        MethodBase method;
        if (genericArgs.Length == 0)
        {
            var type = typeof(SpeakableEnumerator<>).MakeGenericType(genericArgs[0]);
            method = type.GetMethod("GetOrCreate");
            type.GetMethod("PreBuildFieldReferenceGetters").Invoke(null, [enumeratorType]);
        }
        else
        {
            var type = typeof(SpeakableEnumerator<,>).MakeGenericType(
                genericArgs[0],
                genericArgs[1]
            );
            method = type.GetMethod("GetOrCreate");
            type.GetMethod("PreBuildFieldReferenceGetters").Invoke(null, [enumeratorType]);
        }

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
                    w.InsertBeforeCurrent(w.Create(OpCodes.Ldarg, origParam.Index));
                    WriteSpeakableEnumerator(w, firstArg, hook.Target.DeclaringType);
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
                    MonoDetourLogger.LogChannel.Warn,
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
}
