using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoDetour.Interop.MonoModUtils;
using MonoDetour.Logging;
using MonoMod.Cil;

namespace MonoDetour.Cil.Analysis;

internal class InformationalInstruction(
    Instruction instruction,
    int stackSize,
    int stackSizeDelta,
    List<(
        InformationalInstruction.HandlerPart handlerPart,
        ExceptionHandlerType handlerType
    )> handlerParts
)
{
    public InformationalInstruction? Next { get; private set; }
    public Instruction Inst => instruction;
    public int StackSize
    {
        get => stackSize;
        private set => stackSize = value;
    }
    public int StackSizeDelta
    {
        get => stackSizeDelta;
        private set => stackSizeDelta = value;
    }
    public int StackSizeBefore { get; private set; }
    public int StackPop { get; private set; }
    public int StackPush { get; private set; }
    public bool Unreachable => !explored;

    public List<(HandlerPart HandlerPart, ExceptionHandlerType HandlerType)> HandlerParts =>
        handlerParts;

    public List<Annotation> Annotations { get; } = [];

    public bool HasAnnotations => Annotations.Count != 0;

    private bool explored = false;

    public record AnnotationStackSizeMustBeX(string Message, AnnotationRange? Range)
        : Annotation(Message, Range)
    {
        public override string ToString() => base.ToString();
    }

    public record AnnotationPoppingMoreThanStackSize(string Message) : Annotation(Message, null)
    {
        public override string ToString() => base.ToString();
    }

    public record AnnotationStackSizeMismatch(string Message) : Annotation(Message, null)
    {
        public override string ToString() => base.ToString();
    }

    public record Annotation(string Message, AnnotationRange? Range)
    {
        public override string ToString()
        {
            StringBuilder sb = new();
            sb.AppendLine().Append(" └ ").Append(Message);

            if (Range is null)
            {
                return sb.ToString();
            }

            var start = Range.Start;
            var end = Range.End;
            var instructions = Range.Instructions;

            sb.AppendLine();
            sb.AppendLine("   ¦ │ Info: Stack imbalance starts at:");

            if (start != end)
            {
                sb.Append("   ¦ ├ ").AppendLine(instructions[start].ToString());
            }

            for (int i = start + 1; i < end; i++)
            {
                sb.Append("   ¦ │ ").AppendLine(instructions[i].ToString());
            }

            sb.Append("   ¦ └ ").Append(instructions[end].ToString());

            return sb.ToString();
        }
    }

    public record class AnnotationRangeWalkBack(
        List<InformationalInstruction> Instructions,
        int End,
        int RequiredStackSize
    )
        : AnnotationRange(
            Instructions,
            GetProblematicInstructionIndexWithWalkBack(Instructions, End, RequiredStackSize),
            End
        );

    public record class AnnotationRange(
        List<InformationalInstruction> Instructions,
        int Start,
        int End
    );

    [Flags]
    public enum HandlerPart
    {
        TryStart = 1 << 0,
        TryEnd = 1 << 1,
        FilterStart = 1 << 2,
        HandlerStart = 1 << 3,
        HandlerEnd = 1 << 4,
        BeforeTryStart = 1 << 5,
    }

    public override string ToString() => ToStringInternal(withAnnotations: false);

    public string ToStringWithAnnotations() => ToStringInternal(withAnnotations: true);

    internal string ToStringInternal(bool withAnnotations, HashSet<Type>? types = null)
    {
        StringBuilder sb = new();

        foreach (var (handlerPart, handlerType) in HandlerParts)
        {
            if (handlerPart.HasFlag(HandlerPart.TryEnd))
                sb.AppendLine("} // end try");

            if (handlerPart.HasFlag(HandlerPart.HandlerEnd))
                sb.AppendLine(HandlerTypeToStringEnd(handlerType));
        }

        foreach (var (handlerPart, handlerType) in HandlerParts)
        {
            if (handlerPart.HasFlag(HandlerPart.TryStart))
                sb.AppendLine(".try {");

            if (handlerPart.HasFlag(HandlerPart.FilterStart))
                sb.AppendLine("filter {");

            if (handlerPart.HasFlag(HandlerPart.HandlerStart))
            {
                if (handlerType == ExceptionHandlerType.Filter)
                    sb.AppendLine("} // end filter");

                sb.AppendLine(HandlerTypeToStringStart(handlerType));
            }
        }

        if (Unreachable)
            sb.Append($" - | {Inst}");
        else
            sb.Append($"{StackSize, 2} | {Inst}");

        if (withAnnotations && Annotations.Count != 0)
        {
            foreach (var annotation in Annotations)
            {
                // Deduplication time!
                if (types is not null)
                {
                    var annotationType = annotation.GetType();
                    if (types.Contains(annotationType))
                    {
                        continue;
                    }
                    types.Add(annotationType);
                }

                sb.Append(annotation.ToString());
            }
        }

        return sb.ToString();
    }

    static string HandlerTypeToStringStart(ExceptionHandlerType handlerType)
    {
        return handlerType switch
        {
            ExceptionHandlerType.Catch => "catch {",
            ExceptionHandlerType.Filter => "catch {",
            ExceptionHandlerType.Fault => "fault {",
            ExceptionHandlerType.Finally => "finally {",
            _ => throw new ArgumentOutOfRangeException(handlerType.ToString()),
        };
    }

    static string HandlerTypeToStringEnd(ExceptionHandlerType handlerType)
    {
        return handlerType switch
        {
            ExceptionHandlerType.Catch => "} // end catch",
            ExceptionHandlerType.Filter => "} // end catch",
            ExceptionHandlerType.Fault => "} // end fault",
            ExceptionHandlerType.Finally => "} // end finally",
            _ => throw new ArgumentOutOfRangeException(handlerType.ToString()),
        };
    }

    internal static List<InformationalInstruction> CreateList(MethodBody body)
    {
        var instructions = body.Instructions;
        List<InformationalInstruction> informationalInstructions = [];
        Dictionary<Instruction, InformationalInstruction> map = [];

        for (int i = 0; i < instructions.Count; i++)
        {
            var cecilIns = instructions[i];

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
            informationalInstructions.Add(ins);
            map.Add(cecilIns, ins);

            if (i > 0)
            {
                informationalInstructions[i - 1].Next = ins;
            }
        }

        int stackSize = 0;
        // This logic is heavily based on:
        // https://github.com/jbevain/cecil/blob/3136847e/Mono.Cecil.Cil/CodeWriter.cs#L332-L341
        CrawlInstructions(informationalInstructions[0], map, ref stackSize);

        return informationalInstructions;
    }

    static void CrawlInstructions(
        InformationalInstruction instruction,
        Dictionary<Instruction, InformationalInstruction> map,
        ref int stackSize
    )
    {
        var enumerable = instruction;

        while (enumerable is not null)
        {
            if (enumerable.explored)
            {
                if (enumerable.StackSizeBefore == stackSize)
                {
                    break;
                }

                enumerable.Annotations.Add(
                    new AnnotationStackSizeMismatch(
                        $"Error: Stack size mismatch; incoming stack size from branches "
                            + $"is both {enumerable.StackSizeBefore} and {stackSize}"
                    )
                );
                break;
            }

            if (
                enumerable.HandlerParts.Any(x =>
                    x.HandlerPart is HandlerPart.FilterStart or HandlerPart.HandlerStart
                )
            )
            {
                stackSize++;
            }

            enumerable.explored = true;
            ComputeStackDelta(enumerable, ref stackSize);
            TryCrawlBranch(enumerable, map, stackSize);

            if (
                enumerable.Inst.OpCode.FlowControl
                is FlowControl.Branch
                    or FlowControl.Throw
                    or FlowControl.Return
            )
            {
                break;
            }
            enumerable = enumerable.Next;
        }
    }

    static void TryCrawlBranch(
        InformationalInstruction informationalInstruction,
        Dictionary<Instruction, InformationalInstruction> map,
        int stackSize
    )
    {
        var instruction = informationalInstruction.Inst;

        switch (instruction.OpCode.OperandType)
        {
            case OperandType.ShortInlineBrTarget:
            case OperandType.InlineBrTarget:
                Instruction target;

                if (instruction.Operand is ILLabel label)
                    target = label.InteropGetTarget()!;
                else
                    target = (Instruction)instruction.Operand;

                CrawlInstructions(map[target], map, ref stackSize);
                break;

            case OperandType.InlineSwitch:
                Instruction[] targets;

                if (instruction.Operand is ILLabel[] labels)
                    targets = [.. labels.Select(x => x.InteropGetTarget()!)];
                else
                    targets = (Instruction[])instruction.Operand;

                for (int i = 0; i < targets.Length; i++)
                    CrawlInstructions(map[targets[i]], map, ref stackSize);
                break;
        }
    }

    static void ComputeStackDelta(
        InformationalInstruction informationalInstruction,
        ref int stackSize
    )
    {
        var instruction = informationalInstruction.Inst;
        int oldStackSize = stackSize;
        informationalInstruction.StackSizeBefore = oldStackSize;

        switch (instruction.OpCode.FlowControl)
        {
            case FlowControl.Call:
            {
                var method = (IMethodSignature)instruction.Operand;
                // pop 'this' argument
                if (
                    method.HasThis
                    && !method.ExplicitThis
                    && instruction.OpCode.Code != Code.Newobj
                )
                    stackSize--;
                // pop normal arguments
                if (method.HasParameters)
                    stackSize -= method.Parameters.Count;
                // pop function pointer
                if (instruction.OpCode.Code == Code.Calli)
                    stackSize--;

                informationalInstruction.StackPop = oldStackSize - stackSize;

                // push return value
                if (
                    method.ReturnType.MetadataType != MetadataType.Void
                    || instruction.OpCode.Code == Code.Newobj
                )
                {
                    stackSize++;
                    informationalInstruction.StackPush = 1;
                }
                break;
            }
            default:
                ComputePopDelta(instruction, ref stackSize);
                informationalInstruction.StackPop = oldStackSize - stackSize;
                var stackSizeAfterPop = stackSize;
                ComputePushDelta(instruction, ref stackSize);
                informationalInstruction.StackPush = stackSize - stackSizeAfterPop;
                break;
        }

        informationalInstruction.StackSize = stackSize;
        informationalInstruction.StackSizeDelta = stackSize - oldStackSize;
    }

    static void ComputePopDelta(Instruction instruction, ref int stack_size)
    {
        switch (instruction.OpCode.StackBehaviourPop)
        {
            case StackBehaviour.Popi:
            case StackBehaviour.Popref:
            case StackBehaviour.Pop1:
                stack_size--;
                break;
            case StackBehaviour.Pop1_pop1:
            case StackBehaviour.Popi_pop1:
            case StackBehaviour.Popi_popi:
            case StackBehaviour.Popi_popi8:
            case StackBehaviour.Popi_popr4:
            case StackBehaviour.Popi_popr8:
            case StackBehaviour.Popref_pop1:
            case StackBehaviour.Popref_popi:
                stack_size -= 2;
                break;
            case StackBehaviour.Popi_popi_popi:
            case StackBehaviour.Popref_popi_popi:
            case StackBehaviour.Popref_popi_popi8:
            case StackBehaviour.Popref_popi_popr4:
            case StackBehaviour.Popref_popi_popr8:
            case StackBehaviour.Popref_popi_popref:
                stack_size -= 3;
                break;
            case StackBehaviour.PopAll:
                stack_size = 0;
                break;
        }
    }

    static void ComputePushDelta(Instruction instruction, ref int stack_size)
    {
        switch (instruction.OpCode.StackBehaviourPush)
        {
            case StackBehaviour.Push1:
            case StackBehaviour.Pushi:
            case StackBehaviour.Pushi8:
            case StackBehaviour.Pushr4:
            case StackBehaviour.Pushr8:
            case StackBehaviour.Pushref:
                stack_size++;
                break;
            case StackBehaviour.Push1_push1:
                stack_size += 2;
                break;
        }
    }

    internal static int GetProblematicInstructionIndexWithWalkBack(
        List<InformationalInstruction> instructions,
        int index,
        int requiredStackSize
    )
    {
        int walkBackStackSize = 0;
        var targetInstruction = instructions[index];

        while (true)
        {
            walkBackStackSize += targetInstruction.StackSizeDelta;

            if (walkBackStackSize == requiredStackSize)
                break;

            if (index <= 0)
            {
                break;
            }

            index--;
            targetInstruction = instructions[index];
        }

        return index;
    }
}
