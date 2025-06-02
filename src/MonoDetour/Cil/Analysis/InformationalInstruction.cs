using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoDetour.Interop.MonoModUtils;
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

    public List<(HandlerPart HandlerPart, ExceptionHandlerType HandlerType)> HandlerParts =>
        handlerParts;

    public List<Annotation> Annotations { get; } = [];

    public bool HasAnnotations => Annotations.Count != 0;

    public record AnnotationStackSizeMustBeX(string Message, AnnotationRange? Range)
        : Annotation(Message, Range)
    {
        public override string ToString()
        {
            return base.ToString();
        }
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

            sb.Append("   ¦ └ ").AppendLine(instructions[end].ToString());
            sb.Append("   ¦");

            return sb.ToString();
        }
    }

    public record class AnnotationRangeWalkBack(
        List<InformationalInstruction> Instructions,
        int End,
        int IncorrectStackSize
    )
        : AnnotationRange(
            Instructions,
            GetProblematicInstructionIndexWithWalkBack(Instructions, End, IncorrectStackSize),
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
        List<InformationalInstruction> informationalInstructions = [];

        // This whole algorithm is from:
        // https://github.com/jbevain/cecil/blob/3136847e/Mono.Cecil.Cil/CodeWriter.cs#L332-L341
        Dictionary<Instruction, int> stackSizes = [];
        int stackSize = 0;
        int maxStack = 0;
        if (body.HasExceptionHandlers)
        {
            ComputeExceptionHandlerStackSize(ref stackSizes, body);
        }

        var instructions = body.Instructions;

        for (int i = 0; i < instructions.Count; i++)
        {
            var instruction = instructions[i];

            List<(HandlerPart, ExceptionHandlerType)> handlerParts = [];

            foreach (var eh in body.ExceptionHandlers)
            {
                HandlerPart handlerPart = 0;

                if (eh.TryStart.Previous == instruction)
                    handlerPart |= HandlerPart.BeforeTryStart;

                if (eh.TryStart == instruction)
                    handlerPart |= HandlerPart.TryStart;
                if (eh.TryEnd == instruction)
                    handlerPart |= HandlerPart.TryEnd;
                if (eh.FilterStart == instruction)
                    handlerPart |= HandlerPart.FilterStart;
                if (eh.HandlerStart == instruction)
                    handlerPart |= HandlerPart.HandlerStart;
                if (eh.HandlerEnd == instruction)
                    handlerPart |= HandlerPart.HandlerEnd;

                if (handlerPart == 0)
                    continue;

                handlerParts.Add((handlerPart, eh.HandlerType));
            }

            InformationalInstruction ins = new(instruction, default, default, handlerParts);
            informationalInstructions.Add(ins);

            int oldStackSize = stackSize;
            ComputeStackSize(
                informationalInstructions,
                i,
                ref stackSizes,
                ref stackSize,
                ref maxStack
            );
            ins.StackSize = stackSize;
            ins.StackSizeDelta = stackSize - oldStackSize;
        }

        return informationalInstructions;
    }

    static void ComputeExceptionHandlerStackSize(
        ref Dictionary<Instruction, int> stack_sizes,
        MethodBody body
    )
    {
        var exception_handlers = body.ExceptionHandlers;

        for (int i = 0; i < exception_handlers.Count; i++)
        {
            var exception_handler = exception_handlers[i];

            switch (exception_handler.HandlerType)
            {
                case ExceptionHandlerType.Catch:
                    AddExceptionStackSize(exception_handler.HandlerStart, ref stack_sizes);
                    break;
                case ExceptionHandlerType.Filter:
                    AddExceptionStackSize(exception_handler.FilterStart, ref stack_sizes);
                    AddExceptionStackSize(exception_handler.HandlerStart, ref stack_sizes);
                    break;
            }
        }
    }

    static void AddExceptionStackSize(
        Instruction handler_start,
        ref Dictionary<Instruction, int> stack_sizes
    )
    {
        if (handler_start == null)
            return;

        stack_sizes[handler_start] = 1;
    }

    static void ComputeStackSize(
        List<InformationalInstruction> informationalInstructions,
        int index,
        ref Dictionary<Instruction, int> stack_sizes,
        ref int stack_size,
        ref int max_stack
    )
    {
        var informationalInstruction = informationalInstructions[index];
        var instruction = informationalInstruction.Inst;

        if (stack_sizes.TryGetValue(instruction, out int computed_size))
            stack_size = computed_size;

        max_stack = Math.Max(max_stack, stack_size);
        ComputeStackDelta(instruction, ref stack_size);
        max_stack = Math.Max(max_stack, stack_size);

        CopyBranchStackSize(instruction, ref stack_sizes, stack_size);
        // Removed the following method:
        // ComputeStackSize(informationalInstructions, index, ref stack_size);
        // https://github.com/jbevain/cecil/blob/3136847e/Mono.Cecil.Cil/CodeWriter.cs#L423-L432
        // It would set the stack size to 0 on control flow changes, but we are
        // validating stuff here. We don't add validation here yet though.
    }

    static void CopyBranchStackSize(
        Instruction instruction,
        ref Dictionary<Instruction, int> stack_sizes,
        int stack_size
    )
    {
        if (stack_size == 0)
            return;

        switch (instruction.OpCode.OperandType)
        {
            case OperandType.ShortInlineBrTarget:
            case OperandType.InlineBrTarget:
                Instruction target;

                if (instruction.Operand is ILLabel label)
                    target = label.InteropGetTarget()!;
                else
                    target = (Instruction)instruction.Operand;

                CopyBranchStackSize(ref stack_sizes, target, stack_size);
                break;

            case OperandType.InlineSwitch:
                Instruction[] targets;

                if (instruction.Operand is ILLabel[] labels)
                    targets = [.. labels.Select(x => x.InteropGetTarget()!)];
                else
                    targets = (Instruction[])instruction.Operand;

                for (int i = 0; i < targets.Length; i++)
                    CopyBranchStackSize(ref stack_sizes, targets[i], stack_size);
                break;
        }
    }

    static void CopyBranchStackSize(
        ref Dictionary<Instruction, int> stack_sizes,
        Instruction target,
        int stack_size
    )
    {
        int branch_stack_size = stack_size;

        if (stack_sizes.TryGetValue(target, out int computed_size))
            branch_stack_size = Math.Max(branch_stack_size, computed_size);

        stack_sizes[target] = branch_stack_size;
    }

    static void ComputeStackDelta(Instruction instruction, ref int stack_size)
    {
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
                    stack_size--;
                // pop normal arguments
                if (method.HasParameters)
                    stack_size -= method.Parameters.Count;
                // pop function pointer
                if (instruction.OpCode.Code == Code.Calli)
                    stack_size--;
                // push return value
                if (
                    method.ReturnType.MetadataType != MetadataType.Void
                    || instruction.OpCode.Code == Code.Newobj
                )
                    stack_size++;
                break;
            }
            default:
                ComputePopDelta(instruction, ref stack_size);
                ComputePushDelta(instruction, ref stack_size);
                break;
        }
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
        int incorrectStackSize
    )
    {
        int walkBackStackSize = 0;
        var targetInstruction = instructions[index];

        while (true)
        {
            walkBackStackSize += targetInstruction.StackSizeDelta;

            if (walkBackStackSize == incorrectStackSize)
                break;

            index--;
            targetInstruction = instructions[index];
        }

        return index;
    }
}
