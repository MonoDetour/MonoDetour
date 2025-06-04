using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil.Cil;
using MonoDetour.Logging;
using MonoMod.Utils;
using static MonoDetour.Cil.Analysis.InformationalInstruction;

namespace MonoDetour.Cil.Analysis;

internal static class CilAnalyzer
{
    internal static void Analyze(MethodBody body)
    {
        StringBuilder sb = new();
        sb.AppendLine("An ILHook manipulation target method threw on compilation:");
        sb.AppendLine("--- MonoDetour CIL Analysis Start Full Method ---");
        sb.AppendLine();
        sb.AppendLine("Info: Stack size is on the left, instructions are on the right.");
        sb.AppendLine();

        if (body is null)
        {
            sb.AppendLine("Method Body is null, can't analyze.");
            goto analysisEnd;
        }

        if (body.Instructions.Count == 0)
        {
            sb.AppendLine("Method has 0 instructions.");
            goto analysisEnd;
        }

        body.Method.RecalculateILOffsets();
        List<InformationalInstruction> instructions = CreateList(body);

        // TODO: I realize that sorting by only distance is kinda flawed,
        // I optimally would check if the branch has previously ever gotten an error,
        // and then decide if I should show an error there.
        var sorted = instructions.ToList();
        sorted.Sort((x, y) => x.Distance - y.Distance);
        var analyzable = sorted;

        var firstStackSizeMismatch = sorted.FirstOrDefault(x =>
            x.Annotations.Any(x => x is AnnotationStackSizeMismatch)
        );

        if (firstStackSizeMismatch is not null)
        {
            var mismatchBranch = firstStackSizeMismatch.CollectIncoming().ToList();
            mismatchBranch.Sort((x, y) => x.Distance - y.Distance);
            analyzable = mismatchBranch;
        }

        HashSet<Type> types = [];

        foreach (var instruction in analyzable)
            AnalyzeAndAnnotateInstruction(instruction, types);

        sb.Append(instructions.ToStringWithAnnotationTypesDeduplicated());

        sb.AppendLine();
        sb.AppendLine("--- MonoDetour CIL Analysis Summary ---");
        sb.AppendLine();

        if (instructions.All(x => !x.HasAnnotations))
        {
            sb.AppendLine("No mistakes were found.");
            sb.AppendLine("If there are errors, MonoDetour simply didn't catch them.")
                .AppendLine("Currently unreachable instructions aren't evaluated.")
                .AppendLine("You can improve the analysis:")
                .AppendLine(
                    "https://github.com/MonoDetour/MonoDetour/blob/main/src/MonoDetour/Cil/Analysis/CilAnalyzer.cs"
                );
        }
        else
        {
            sb.AppendLine("Info: Stack size is on the left, instructions are on the right.");
            sb.AppendLine();

            sb.Append(instructions.ToStringWithAnnotationTypesDeduplicatedExclusive());

            sb.AppendLine();
            sb.AppendLine("Note: This analysis may not be perfect.");
        }

        analysisEnd:
        sb.AppendLine();
        sb.Append("--- MonoDetour CIL Analysis End ---");

        // This is an Info log and not a Debug one so that the developer who
        // needs it the most actually finds it in their console output.
        MonoDetourLogger.Log(MonoDetourLogger.LogChannel.Info, sb.ToString());
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

            instruction.Annotations.Add(
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

            instruction.Annotations.Add(
                new AnnotationStackSizeMustBeX(
                    $"Error: Stack size before try start must be 0; it was {stackSize}",
                    new AnnotationRangeWalkBack(instruction, stackSize)
                )
            );
        }
        else if (
            stackSize != 0
            && instruction.Inst.OpCode.FlowControl is FlowControl.Throw or FlowControl.Return
        )
        {
            if (types.Contains(typeof(AnnotationPoppingMoreThanStackSize)))
                return;
            types.Add(typeof(AnnotationStackSizeMustBeX));

            string throwOrReturn = instruction.Inst.OpCode.FlowControl switch
            {
                FlowControl.Throw => "throw",
                FlowControl.Return => "return",
                _ => throw new Exception("Unreachable"),
            };

            instruction.Annotations.Add(
                new AnnotationStackSizeMustBeX(
                    $"Error: Stack size on {throwOrReturn} must be 0; it was {stackSize}",
                    new AnnotationRangeWalkBack(instruction, stackSize)
                )
            );
        }

        // TODO: Check stuff like ldfld/ldsfld having a valid operand.
        // It's not necessary, but will be more user-friendly.
    }
}
