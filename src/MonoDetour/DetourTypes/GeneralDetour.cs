using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;

namespace MonoDetour.DetourTypes;

static class GeneralDetour
{
    static readonly Dictionary<MethodBase, ILLabel> firstRedirectForMethod = [];

    public static void Manipulator(ILContext il, MonoDetourInfo info)
    {
        if (!info.Data.IsInitialized())
            throw new InvalidProgramException();

        MonoDetourData data = info.Data;

        if (!data.Manipulator.IsStatic)
        {
            throw new NotSupportedException(
                "Only static manipulator methods are supported for now."
            );
        }

        ILCursor c = new(il);

        if (info.DetourType == typeof(PostfixDetour))
        {
            c.Index -= 1;
            bool found = firstRedirectForMethod.TryGetValue(info.Data.Target, out var target);

            ILLabel redirectedRet = RedirectEarlyReturnsToLabel(c, target);

            if (!found)
                firstRedirectForMethod.Add(info.Data.Target, redirectedRet);

            // Move redirectedRet label to next emitted instruction
            // as we want it to point to our postfix hook.
            c.MoveAfterLabels();
        }

        ILLabel firstParamForHook = c.MarkLabel();

        int? retLocIdx = c.EmitParams(info, out var storedReturnValue);

        c.Emit(OpCodes.Call, data.Manipulator);

        if (storedReturnValue is not null)
        {
            firstParamForHook.Target = storedReturnValue.Next;
        }

        var outsideThisHook = il.DefineLabel(c.Next!);
        c.Emit(OpCodes.Leave, outsideThisHook);
        Instruction leaveCallInTry = c.Previous;

        // This is the start of an exception handler block,
        // and the exception should be on the stack if we are here.
        c.EmitReference(info);
        c.EmitDelegate(DisposeBadHooks);
        c.Emit(OpCodes.Leave, outsideThisHook);
        Instruction leaveCallInCatch = c.Previous;

        if (info.DetourType == typeof(PostfixDetour) && retLocIdx is not null)
        {
            // This must be outside of the catch and we must branch to it.
            c.ApplyReturnValue(info, (int)retLocIdx);
            outsideThisHook.Target = c.Previous;
        }

        il.Body.ExceptionHandlers.Add(
            new ExceptionHandler(ExceptionHandlerType.Catch)
            {
                CatchType = il.Import(typeof(Exception)),

                TryStart = firstParamForHook.Target,
                TryEnd = leaveCallInTry.Next,

                HandlerStart = leaveCallInTry.Next,
                HandlerEnd = leaveCallInCatch.Next,
            }
        );

        if (data.Owner.LogLevel == MonoDetourManager.Logging.Diagnostic)
        {
            c.Method.RecalculateILOffsets();
            Console.WriteLine($"Manipulated by {data.Manipulator.Name}: " + il);
        }
    }

    static void DisposeBadHooks(Exception ex, MonoDetourInfo info)
    {
        var manipulator = info.Data.Manipulator!;
        Console.WriteLine(
            $"[MonoDetour] Hook '{manipulator}' threw an exception,"
                + $" and its {nameof(MonoDetourManager)}'s hooks will be disposed.\n"
                + $"The Exception that was thrown: {ex}"
        );
        info.Data.Owner!.DisposeHooks();
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
                && c.Body.Instructions.IndexOf(ins) < c.Body.Instructions.IndexOf(target.Target)
            )
            {
                // Console.WriteLine("Retargeted instruction!");
                ins.OpCode = OpCodes.Br;
                ins.Operand = target.Target!.Next;
                continue;
            }

            // If the above isn't the case, we can still point them to us.
            ins.OpCode = OpCodes.Br;
            ins.Operand = retLabel;
        }

        Instruction lastInstruction = c.Body.Instructions[c.Body.Instructions.Count - 1];
        lastInstruction.OpCode = hasRet ? OpCodes.Ret : OpCodes.Nop;
        lastInstruction.Operand = null;
        retLabel.Target = lastInstruction;

        if (target is not null && c.Body.Instructions.IndexOf(target.Target) == -1)
        {
            // We set the first target to previous instruction so it
            // doesn't get retargeted as someone who points to a ret label.
            target.Target = lastInstruction.Previous;
        }

        // Console.WriteLine("Last instruction is " + lastInstruction);
        // Console.WriteLine("target is " + target?.Target);

        return retLabel;
    }
}
