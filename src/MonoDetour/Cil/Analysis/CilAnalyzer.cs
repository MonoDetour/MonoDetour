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
        sb.AppendLine("INFO: Stack size is on the left, instructions are on the right.");
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

        for (int i = 0; i < instructions.Count; i++)
        {
            AnalyzeAndAnnotateInstruction(instructions, i);
        }

        sb.Append(instructions.ToStringWithAnnotationTypesDeduplicated());

        sb.AppendLine();
        sb.AppendLine("--- MonoDetour CIL Analysis Summary ---");
        sb.AppendLine();

        if (instructions.All(x => !x.HasAnnotations))
        {
            sb.AppendLine("No stack size related mistakes were found.");
            sb.AppendLine("If there are errors, they may be related to the stack behavior of ")
                .AppendLine(
                    "individual instructions which MonoDetour does not check (at least yet)"
                );
        }
        else
        {
            sb.AppendLine("INFO: Stack size is on the left, instructions are on the right.");
            sb.AppendLine();

            sb.Append(instructions.ToStringWithAnnotationTypesDeduplicatedExclusive());

            sb.AppendLine();
            sb.AppendLine("NOTE: This analysis is not perfect; errors may not always be accurate.");
            sb.AppendLine("TIP:  Pay close attention to stack sizes.");
            sb.Append("      A branch pointing to an instruction can cause its ")
                .AppendLine("stack size to increase seemingly out of nowhere.");
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
