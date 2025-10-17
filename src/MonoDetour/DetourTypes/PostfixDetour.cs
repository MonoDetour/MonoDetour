using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil.Cil;
using MonoDetour.Cil;
using MonoDetour.Cil.Analysis;
using MonoDetour.DetourTypes.Manipulation;
using MonoDetour.Interop.MonoModUtils;
using MonoDetour.Logging;
using MonoMod.Cil;

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

        HookTargetRecords.HookTargetInfo info = HookTargetRecords.GetHookTargetInfo(il);
        var originalInstructions = HookTargetRecords.GetOriginalInstructions(il.Method);
        ILWeaver w = new(new(il, Hook.Target, originalInstructions));
        w.CurrentTo(w.Last);

        var firstPostfixInstructions = info.PostfixInfo.FirstPostfixInstructions;
        var postfixStart = RedirectEarlyReturnsToLabel(w, info);
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
        w.InsertBeforeCurrent(w.CreateCall(Utils.DisposeBadHooks));

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
        foreach (var eh in il.Body.ExceptionHandlers)
        {
            if (eh.HandlerEnd == w.Last)
                eh.HandlerEnd = postfixStart.InteropGetTarget()!;
        }

        w.HandlerApply(handler);

        Hook.Owner.Log(
            MonoDetourLogger.LogChannel.IL,
            () =>
            {
                var body = w.Body.CreateInformationalSnapshotJIT().AnnotateErrors();
                return $"Manipulated by Postfix: {Hook.Manipulator.Name} ({Hook.Owner.Id}):\n{body}";
            }
        );

        Utils.DebugValidateCILValidatorNoErrors(Hook, w.Body);
    }

    // Taken and adapted from HarmonyX
    private static ILLabel RedirectEarlyReturnsToLabel(
        ILWeaver w,
        HookTargetRecords.HookTargetInfo info
    )
    {
        var postfixes = info.PostfixInfo.FirstPostfixInstructions;
        var retValLocIndex = info.ReturnValue?.Index;

        if (w.Body.Instructions.Count == 0)
        {
            w.Body.Instructions.Add(w.Create(OpCodes.Nop));
        }

        var instructions = w.Body.Instructions;
        var last = w.Last;
        w.DefineAndMarkLabelTo(last, out var retLabel);

        bool hasRet = false;
        foreach (var ins in w.Body.Instructions.Where(ins => ins.MatchRet()))
        {
            hasRet = true;

            if (ins.Next is not { } next)
            {
                continue;
            }

            // To allow intentional early returns,
            // we ignore cases where ret is called twice in a row.
            // But double ret at end of method is not early, and probably not intentional.
            if (retValLocIndex is { } retValIndex)
            {
                // This code is not nice. Please rewrite. Optimally so that
                // there is a single ILHook at the end that redirects all the ret instructions.

                // ldloc <-- ins.Previous.Previous.Previous
                // ret <-- ins.Previous.Previous
                // ldloc <-- ins.Previous
                // ret <-- ins
                // ldloc <-- next
                // ret <-- next.Next
                // maybe null <-- next.Next.Next
                if (next.Next?.Next is not null && ins.Previous?.MatchLdloc(retValIndex) is true)
                {
                    if (next.Next.OpCode == OpCodes.Ret && next.MatchLdloc(retValIndex))
                    {
                        continue;
                    }

                    if (
                        ins.Previous.Previous?.OpCode == OpCodes.Ret
                        && ins.Previous.Previous.Previous?.MatchLdloc(retValIndex) is true
                    )
                    {
                        continue;
                    }
                }
            }
            else
            {
                if (next.Next is not null)
                {
                    if (next.OpCode == OpCodes.Ret || ins.Previous?.OpCode == OpCodes.Ret)
                        continue;
                }
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
