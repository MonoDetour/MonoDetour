using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil.Cil;
using MonoMod.Utils;
using static MonoDetour.Cil.Analysis.InformationalInstruction;

namespace MonoDetour.Cil.Analysis;

internal class InformationalMethodBody
{
    public MethodBody CecilMethodBody { get; }
    public List<InformationalInstruction> Instructions { get; } = [];
    public HashSet<Instruction> Duplicates { get; } = [];
    public bool HasDuplicates => Duplicates.Count != 0;

    private InformationalMethodBody(MethodBody body)
    {
        CecilMethodBody = body;

        var bodyInstructions = body.Instructions;
        if (bodyInstructions.Count == 0)
        {
            return;
        }

        IList<Instruction> enumerableInstructions = bodyInstructions;
        HashSet<Instruction> originalInstructions = [];
        HashSet<Instruction> originallyDuplicateInstructions = [];

        foreach (var instruction in bodyInstructions)
        {
            if (!originalInstructions.Add(instruction))
            {
                Duplicates.Add(instruction);
            }
        }

        if (HasDuplicates)
        {
            enumerableInstructions = [];
            int offset = 0;
            foreach (var instruction in bodyInstructions)
            {
                if (Duplicates.Contains(instruction))
                {
                    var newIns = Instruction.Create(OpCodes.Ret);
                    newIns.OpCode = instruction.OpCode;
                    newIns.Operand = instruction.Operand;
                    newIns.Offset = offset;
                    enumerableInstructions.Add(newIns);
                    originallyDuplicateInstructions.Add(newIns);
                }
                else
                {
                    instruction.Offset = offset;
                    enumerableInstructions.Add(instruction);
                }
                offset += instruction.GetSize();
            }
        }
        else
        {
            body.Method.RecalculateILOffsets();
        }

        Dictionary<Instruction, InformationalInstruction> map = [];

        for (int i = 0; i < enumerableInstructions.Count; i++)
        {
            var cecilIns = enumerableInstructions[i];

            List<(HandlerPart, ExceptionHandlerType)> handlerParts = [];

            foreach (var eh in body.ExceptionHandlers)
            {
                HandlerPart handlerPart = 0;

                if (eh.TryStart.Previous == cecilIns)
                    handlerPart |= HandlerPart.BeforeTryStart;

                if (eh.TryStart == cecilIns)
                    handlerPart |= HandlerPart.TryStart;
                if (eh.TryEnd == cecilIns)
                    handlerPart |= HandlerPart.TryEnd;
                if (eh.FilterStart == cecilIns)
                    handlerPart |= HandlerPart.FilterStart;
                if (eh.HandlerStart == cecilIns)
                    handlerPart |= HandlerPart.HandlerStart;
                if (eh.HandlerEnd == cecilIns)
                    handlerPart |= HandlerPart.HandlerEnd;

                if (handlerPart == 0)
                    continue;

                handlerParts.Add((handlerPart, eh.HandlerType));
            }

            InformationalInstruction ins = new(cecilIns, default, default, handlerParts);
            Instructions.Add(ins);
            map.Add(cecilIns, ins);

            if (originallyDuplicateInstructions.Contains(cecilIns))
            {
                ins.ErrorAnnotations.Add(new AnnotationDuplicateInstance());
            }

            if (i > 0)
            {
                Instructions[i - 1].Next = ins;
            }
        }

        int stackSize = 0;
        CrawlInstructions(Instructions[0], map, ref stackSize, body, distance: 0);

        foreach (var eh in body.ExceptionHandlers)
        {
            stackSize = 1;

            if (eh.FilterStart is not null)
            {
                CrawlInstructions(
                    map[eh.FilterStart],
                    map,
                    ref stackSize,
                    body,
                    map[eh.HandlerEnd].Distance - 10_000 + 1,
                    outsideExceptionHandler: false
                );
            }
            if (eh.HandlerStart is not null)
            {
                CrawlInstructions(
                    map[eh.HandlerStart],
                    map,
                    ref stackSize,
                    body,
                    map[eh.HandlerEnd].Distance - 9_000,
                    outsideExceptionHandler: false
                );
            }
        }
    }

    public static InformationalMethodBody CreateInformationalSnapshot(MethodBody body) => new(body);

    internal string ToErrorMessageString()
    {
        StringBuilder sb = new();
        sb.AppendLine("An ILHook manipulation target method threw on compilation:");
        sb.AppendLine(CecilMethodBody.Method.FullName);
        sb.AppendLine("--- MonoDetour CIL Analysis Start Full Method ---");
        sb.AppendLine();
        sb.AppendLine("Info: Stack size is on the left, instructions are on the right.");
        sb.AppendLine();

        if (Instructions.Count == 0)
        {
            sb.AppendLine("Method has 0 instructions.");
            goto analysisEnd;
        }

        sb.Append(ToStringWithAnnotations());

        sb.AppendLine();
        sb.AppendLine("--- MonoDetour CIL Analysis Summary ---");
        sb.AppendLine();

        if (!HasErrors())
        {
            sb.AppendLine("MonoDetour didn't catch any mistakes.")
                .AppendLine("The errors may not be directly stack behavior related.")
                .AppendLine("Operands or types on the stack aren't validated.")
                .AppendLine("You can improve the analysis:")
                .AppendLine(
                    "https://github.com/MonoDetour/MonoDetour/blob/main/src/MonoDetour/Cil/Analysis/CilAnalyzer.cs"
                );
        }
        else
        {
            sb.AppendLine("Info: Stack size is on the left, instructions are on the right.");
            sb.AppendLine();

            sb.Append(ToStringWithAnnotationsExclusive());

            sb.AppendLine();
            sb.AppendLine("Note: This analysis may not be perfect.");
        }

        analysisEnd:
        sb.AppendLine();
        sb.Append("--- MonoDetour CIL Analysis End ---");

        return sb.ToString();
    }

    static void TryAppendDuplicates(StringBuilder sb, InformationalMethodBody body)
    {
        if (!body.HasDuplicates)
        {
            return;
        }

        sb.AppendLine("Warning: Duplicate instruction instances; These may break the method");

        var instructions = body.Duplicates.ToList();
        var end = instructions.Count - 1;

        for (int i = 0; i < end; i++)
        {
            sb.Append($"├ ").AppendLine(instructions[i].ToString());
        }

        sb.Append($"└ ").AppendLine(instructions[end].ToString());
    }

    /// <summary>
    /// To string with annotations.
    /// </summary>
    public string ToStringWithAnnotations()
    {
        StringBuilder sb = new();

        foreach (var instruction in Instructions)
        {
            sb.AppendLine(instruction.ToStringWithAnnotations());
        }

        return sb.ToString();
    }

    /// <summary>
    /// To string with annotations. Excludes all other instructions.
    /// </summary>
    public string ToStringWithAnnotationsExclusive()
    {
        StringBuilder sb = new();

        foreach (var instruction in Instructions.Where(x => x.HasErrorAnnotations))
        {
            sb.AppendLine(instruction.ToStringWithAnnotations());
        }

        return sb.ToString();
    }

    /// <summary>
    /// Returns true if the list of informational instructions has annotated errors.
    /// </summary>
    /// <returns>Whether or not the list of informational instructions has annotated errors.</returns>
    public bool HasErrors()
    {
        // Technically duplicate instructions aren't errors, but they can cause them.
        if (Duplicates.Count != 0 || Instructions.Any(x => x.HasErrorAnnotations))
        {
            return true;
        }

        return false;
    }

    internal void ThrowIfErrorAnnotations()
    {
        if (HasErrors())
        {
            throw new Exception("Informational instructions had exception annotations.");
        }
    }

    internal void ThrowIfNoErrorAnnotations()
    {
        if (!HasErrors())
        {
            throw new Exception("Informational instructions had exception annotations.");
        }
    }
}
