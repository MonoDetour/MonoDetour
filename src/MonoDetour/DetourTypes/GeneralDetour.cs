using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mono.Cecil.Cil;
using MonoDetour.Interop.MonoModUtils;
using MonoDetour.Logging;
using MonoMod.Cil;
using MonoMod.Utils;

namespace MonoDetour.DetourTypes;

static class GeneralDetour
{
    static readonly Dictionary<MethodBase, ILLabel> firstRedirectForMethod = [];
    static readonly object _lock = new();

    public static void Manipulator(ILContext il, IReadOnlyMonoDetourHook hook)
    {
        if (!hook.Manipulator.IsStatic)
        {
            throw new NotSupportedException(
                "Only static manipulator methods are supported for now."
            );
        }

        ILCursor c = new(il);

        if (hook is MonoDetourHook<PostfixDetour>)
        {
            c.Index -= 1;
            lock (_lock)
            {
                bool found = firstRedirectForMethod.TryGetValue(hook.Target, out var target);

                ILLabel redirectedRet = RedirectEarlyReturnsToLabel(c, target);

                if (!found)
                {
                    firstRedirectForMethod.Add(hook.Target, redirectedRet);
                }
            }

            // Move redirectedRet label to next emitted instruction
            // as we want it to point to our postfix hook.
            c.MoveAfterLabels();
        }

        ILLabel firstParamForHook = c.MarkLabel();

        int? retLocIdx = c.EmitParams(hook, out var storedReturnValue);

        c.Emit(OpCodes.Call, hook.Manipulator);

        if (storedReturnValue is not null)
        {
            firstParamForHook.InteropSetTarget(storedReturnValue.Next);
        }

        var outsideThisHook = il.DefineLabel(c.Next!);
        c.Emit(OpCodes.Leave, outsideThisHook);
        Instruction leaveCallInTry = c.Previous;

        // This is the start of an exception handler block,
        // and the exception should be on the stack if we are here.
        c.InteropEmitReference(hook);
        c.EmitDelegate(DisposeBadHooks);
        c.Emit(OpCodes.Leave, outsideThisHook);
        Instruction leaveCallInCatch = c.Previous;

        if (hook is MonoDetourHook<PostfixDetour> && retLocIdx is not null)
        {
            // This must be outside of the catch and we must branch to it.
            c.ApplyReturnValue(hook, (int)retLocIdx);
            outsideThisHook.InteropSetTarget(c.Previous);
        }

        il.Body.ExceptionHandlers.Add(
            new ExceptionHandler(ExceptionHandlerType.Catch)
            {
                CatchType = il.Import(typeof(Exception)),

                TryStart = firstParamForHook.InteropGetTarget(),
                TryEnd = leaveCallInTry.Next,

                HandlerStart = leaveCallInTry.Next,
                HandlerEnd = leaveCallInCatch.Next,
            }
        );

        MonoDetourLogger.Log(
            MonoDetourLogger.LogChannel.IL,
            () =>
            {
                c.Method.RecalculateILOffsets();
                return $"Manipulated by {hook.Manipulator.Name}: {il}";
            }
        );
    }

    static void DisposeBadHooks(Exception ex, IReadOnlyMonoDetourHook hook)
    {
        MethodBase manipulator = hook.Manipulator;
        MethodBase target = hook.Target;
        string? targetTypeName = target.DeclaringType?.FullName;

        MonoDetourLogger.Log(
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
                MonoDetourLogger.Log(
                    MonoDetourLogger.LogChannel.Warn,
                    () =>
                        $"No disposal event handler for the {nameof(MonoDetourManager)} was registered."
                );
            }
        }
        catch (Exception disposalEx)
        {
            MonoDetourLogger.Log(
                MonoDetourLogger.LogChannel.Error,
                () => $"Disposal event handler threw an exception:\n{disposalEx}"
            );
        }
        finally
        {
            hook.Owner.DisposeHooks();
        }
    }

    // Taken and adapted from HarmonyX
    private static ILLabel RedirectEarlyReturnsToLabel(ILCursor c, ILLabel? target)
    {
        if (c.Body.Instructions.Count == 0)
            c.Emit(OpCodes.Nop);

        ILLabel retLabel = c.Context.DefineLabel();

        var hasRet = false;
        foreach (var ins in c.Body.Instructions.Where(ins => ins.MatchRet()))
        {
            hasRet = true;

            // To allow intentional early returns,
            // we ignore cases where ret is called twice in a row.
            // But double ret at end of method is not early, and probably not intentional.
            if (ins.Next?.Next is not null)
            {
                if (ins.Next.OpCode == OpCodes.Ret || ins.Previous?.OpCode == OpCodes.Ret)
                    continue;
            }

            // A previous label may exist before us, but
            // someone has emitted a ret instruction afterwards.
            // If that is the case, redirect their ret to the existing label.
            if (
                target is not null
                && c.Body.Instructions.IndexOf(ins)
                    < c.Body.Instructions.IndexOf(target.InteropGetTarget())
            )
            {
                // Console.WriteLine("Retargeted instruction!");
                ins.OpCode = OpCodes.Br;
                ins.Operand = target.InteropGetTarget()!.Next;
                continue;
            }

            // If the above isn't the case, we can still point them to us.
            ins.OpCode = OpCodes.Br;
            ins.Operand = retLabel;
        }

        Instruction lastInstruction = c.Body.Instructions[^1];
        lastInstruction.OpCode = hasRet ? OpCodes.Ret : OpCodes.Nop;
        lastInstruction.Operand = null;
        retLabel.InteropSetTarget(lastInstruction);

        if (target is not null && c.Body.Instructions.IndexOf(target.InteropGetTarget()) == -1)
        {
            // We set the first target to previous instruction so it
            // doesn't get retargeted as someone who points to a ret label.
            target.InteropSetTarget(lastInstruction.Previous);
        }

        // Console.WriteLine("Last instruction is " + lastInstruction);
        // Console.WriteLine("target is " + target?.Target);

        return retLabel;
    }
}
