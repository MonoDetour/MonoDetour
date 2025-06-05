using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil.Cil;
using static MonoDetour.Cil.Analysis.InformationalInstruction;

namespace MonoDetour.Cil.Analysis;

internal static class CilAnalyzer
{
    internal static InformationalMethodBody AnnotateErrors(
        this InformationalMethodBody informationalBody
    )
    {
        // TODO: I realize that sorting by only distance is kinda flawed,
        // I optimally would check if the branch has previously ever gotten an error,
        // and then decide if I should show an error there.
        var sorted = informationalBody.Instructions.ToList();
        sorted.Sort((x, y) => x.Distance - y.Distance);
        var analyzable = sorted;

        var firstStackSizeMismatch = sorted.FirstOrDefault(x =>
            x.ErrorAnnotations.Any(x => x is AnnotationStackSizeMismatch)
        );

        if (firstStackSizeMismatch is not null)
        {
            var mismatchBranch = firstStackSizeMismatch.CollectIncoming().ToList();
            mismatchBranch.Sort((x, y) => x.Distance - y.Distance);
            analyzable = mismatchBranch;
        }

        HashSet<Type> types = [];

        foreach (var instruction in analyzable)
        {
            AnalyzeAndAnnotateInstruction(instruction, types);
        }

        return informationalBody;
    }

    static void AnalyzeAndAnnotateInstruction(
        InformationalInstruction instruction,
        HashSet<Type> types
    )
    {
        var stackSize = instruction.StackSize;
        var handlerParts = instruction.HandlerParts;

        // Unreachable instructions seem to sometimes not be evaluated,
        // but sometimes they are evaluated anyways.
        // I think it's safer to ignore unreachable instructions for now.
        if (instruction.Unreachable)
        {
            return;
        }

        if (instruction.StackPop > instruction.StackSizeBefore)
        {
            if (types.Contains(typeof(AnnotationPoppingMoreThanStackSize)))
                return;
            types.Add(typeof(AnnotationPoppingMoreThanStackSize));

            instruction.ErrorAnnotations.Add(
                new AnnotationPoppingMoreThanStackSize(
                    $"Error: Popping more than stack size; cannot pop {instruction.StackPop} "
                        + $"value(s) when stack size was {instruction.StackSizeBefore}"
                )
            );
        }
        else if (
            stackSize != 0
            && handlerParts.Any(x => x.HandlerPart.HasFlag(HandlerPart.BeforeTryStart))
        )
        {
            if (types.Contains(typeof(AnnotationStackSizeMustBeX)))
                return;
            types.Add(typeof(AnnotationStackSizeMustBeX));

            instruction.ErrorAnnotations.Add(
                new AnnotationStackSizeMustBeX(
                    $"Error: Stack size before try start must be 0; it was {stackSize}",
                    new AnnotationRangeWalkBack(instruction, stackSize)
                )
            );
        }
        // Apparently stack size doesn't matter on throw
        else if (stackSize != 0 && instruction.Inst.OpCode.FlowControl == FlowControl.Return)
        {
            if (types.Contains(typeof(AnnotationStackSizeMustBeX)))
                return;
            types.Add(typeof(AnnotationStackSizeMustBeX));

            instruction.ErrorAnnotations.Add(
                new AnnotationStackSizeMustBeX(
                    $"Error: Stack size on return must be 0; it was {stackSize}",
                    new AnnotationRangeWalkBack(instruction, stackSize)
                )
            );
        }

        // TODO: Better analysis of exception handlers.
        // Those are a pain to work with so it'd be really good to have that.

        // TODO: Check stuff like ldfld/ldsfld having a valid operand.
        // It's not necessary, but will be more user-friendly.
    }
}
