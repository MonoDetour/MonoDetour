using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil.Cil;
using MonoDetour.Cil;
using MonoDetour.Interop.MonoModUtils;
using MonoDetour.Logging;
using MonoMod.Cil;
using MonoMod.Utils;

namespace MonoDetour.DetourTypes;

/// <summary>
/// Implements MonoDetour support for a Hook that will run at the end of the target method.
/// </summary>
public class PostfixDetour : IMonoDetourHookApplier
{
    /// <inheritdoc/>
    public IReadOnlyMonoDetourHook Hook { get; set; } = null!;

    /// <inheritdoc/>
    public void ApplierManipulator(ILContext il)
    {
        if (Hook.ModifiesControlFlow())
        {
            throw new NotSupportedException("A PostfixDetour may not modify control flow.");
        }

        HookedMethodInfo info = MethodHookRecords.GetFor(il, Hook.Target);
        ILWeaver w = new(new(il, Hook.Target));
        w.CurrentTo(w.Last);

        var firstPostfixInstructions = info.PostfixInfo.FirstPostfixInstructions;
        var postfixStart = RedirectEarlyReturnsToLabel(w, firstPostfixInstructions);
        var branches = w.GetIncomingLabelsFor(postfixStart.InteropGetTarget()!);
        w.MarkLabelToFutureNextInsert(postfixStart);

        w.HandlerCreateCatch(null, out var handler);
        w.DefineLabel(out var tryStart);
        w.HandlerSetTryStart(tryStart, handler);

        if (info.ReturnValue is not null)
        {
            w.InsertBeforeCurrent(w.Create(OpCodes.Stloc, info.ReturnValue));
            w.MarkLabelToFutureNextInsert(tryStart);
            w.EmitParamsAndReturnValueBeforeCurrent(info.ReturnValue, Hook);
        }
        else
        {
            w.MarkLabelToFutureNextInsert(tryStart);
            w.EmitParamsBeforeCurrent(Hook);
        }

        w.InsertBeforeCurrent(w.Create(OpCodes.Call, Hook.Manipulator));

        w.HandlerSetTryEnd(w.Previous, handler);

        // w.InsertBeforeCurrent(w.Create(OpCodes.Pop));
        w.EmitReferenceBeforeCurrent(Hook, out _);
        w.InsertBeforeCurrent(w.CreateCall(GeneralDetour.DisposeBadHooks));

        w.HandlerSetHandlerEnd(w.Previous, handler);

        if (info.ReturnValue is not null)
        {
            w.InsertBeforeCurrent(w.Create(OpCodes.Ldloc, info.ReturnValue));
        }
        else
        {
            w.InsertBeforeCurrent(w.Create(OpCodes.Nop));
        }

        w.RetargetLabels(branches, postfixStart.InteropGetTarget()!);
        firstPostfixInstructions.Add(postfixStart.InteropGetTarget()!);

        w.HandlerApply(handler);

        if (Hook.Owner.PrintIL)
        {
            w.Method.RecalculateILOffsets();
            // Console.WriteLine(postfixStart.InteropGetTarget()!);
            // Console.WriteLine("handler.TryStart:     " + handler.TryStart);
            // Console.WriteLine("handler.TryEnd:       " + handler.TryEnd);
            // Console.WriteLine("handler.HandlerStart: " + handler.HandlerStart);
            // Console.WriteLine("handler.HandlerEnd:   " + handler.HandlerEnd);
            // Console.WriteLine("handler.CatchType:    " + handler.CatchType?.ToString());
            // Console.WriteLine("handler.HandlerType:  " + handler.HandlerType.ToString());
            Console.WriteLine($"Manipulated by Postfix: {Hook.Manipulator.Name}: {il}");
        }

        MonoDetourLogger.Log(
            MonoDetourLogger.LogChannel.IL,
            () =>
            {
                w.Method.RecalculateILOffsets();
                return $"Manipulated by Postfix: {Hook.Manipulator.Name}: {il}";
            }
        );
    }

    // Taken and adapted from HarmonyX
    private static ILLabel RedirectEarlyReturnsToLabel(ILWeaver w, List<Instruction> postfixes)
    {
        if (w.Body.Instructions.Count == 0)
        {
            w.Body.Instructions.Add(w.Create(OpCodes.Nop));
        }

        var instructions = w.Body.Instructions;
        var last = w.Last;
        w.MarkLabelTo(last, out var retLabel);

        bool hasRet = false;
        foreach (var ins in w.Body.Instructions.Where(ins => ins.MatchRet()))
        {
            hasRet = true;

            if (ins.Next is null)
            {
                continue;
            }

            // To allow intentional early returns,
            // we ignore cases where ret is called twice in a row.
            // But double ret at end of method is not early, and probably not intentional.
            if (ins.Next.Next is not null)
            {
                if (ins.Next.OpCode == OpCodes.Ret || ins.Previous?.OpCode == OpCodes.Ret)
                    continue;
            }

            bool targeted = false;
            foreach (var postfix in postfixes)
            {
                if (postfix is null)
                {
                    continue;
                }

                if (instructions.IndexOf(ins) < instructions.IndexOf(postfix))
                {
                    // Console.WriteLine("Retargeted instruction!");
                    ins.OpCode = OpCodes.Br;
                    ins.Operand = postfix;
                    targeted = true;
                    break;
                }
            }

            if (targeted)
            {
                continue;
            }

            // If the above isn't the case, we can still point them to the return label.
            ins.OpCode = OpCodes.Br;
            ins.Operand = retLabel;
        }

        // Turn the last return back into a return.
        last.OpCode = hasRet ? OpCodes.Ret : OpCodes.Nop;
        last.Operand = null;

        return retLabel;
    }
}
