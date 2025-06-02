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
            sb.AppendLine();
            sb.AppendLine("--- MonoDetour CIL Analysis End ---");
            return;
        }

        body.Method.RecalculateILOffsets();
        List<InformationalInstruction> instructions = CreateList(body);

        if (!instructions.Any(x => x.Annotations.Any(x => x is AnnotationStackSizeMismatch)))
        {
            for (int i = 0; i < instructions.Count; i++)
            {
                AnalyzeAndAnnotateInstruction(instructions, i);
            }
        }

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
            sb.AppendLine("Note: This analysis is not perfect; errors may not always be accurate.");
        }

        sb.AppendLine();
        sb.Append("--- MonoDetour CIL Analysis End ---");

        // This is an Info log and not a Debug one so that the developer who
        // needs it the most actually finds it in their console output.
        MonoDetourLogger.Log(MonoDetourLogger.LogChannel.Info, sb.ToString());
    }

    static void AnalyzeAndAnnotateInstruction(
        List<InformationalInstruction> instructions,
        int index
    )
    {
        var instruction = instructions[index];
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
            instruction.Annotations.Add(
                new AnnotationStackSizeMustBeX(
                    $"Error: Stack size before try start must be 0; it was {stackSize}",
                    new AnnotationRangeWalkBack(instructions, index, stackSize)
                )
            );
        }
        else if (
            stackSize != 0
            && instruction.Inst.OpCode.FlowControl is FlowControl.Throw or FlowControl.Return
        )
        {
            string throwOrReturn = instruction.Inst.OpCode.FlowControl switch
            {
                FlowControl.Throw => "throw",
                FlowControl.Return => "return",
                _ => throw new Exception("Unreachable"),
            };

            instruction.Annotations.Add(
                new AnnotationStackSizeMustBeX(
                    $"Error: Stack size on {throwOrReturn} must be 0; it was {stackSize}",
                    new AnnotationRangeWalkBack(instructions, index, stackSize)
                )
            );
        }
    }
}
