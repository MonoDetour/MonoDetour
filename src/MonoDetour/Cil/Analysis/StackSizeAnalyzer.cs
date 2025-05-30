using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoDetour.Interop.MonoModUtils;
using MonoDetour.Logging;
using MonoMod.Cil;
using MonoMod.Utils;

namespace MonoDetour.Cil.Analysis;

class InformationalInstruction(
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
    public int StackSize => stackSize;
    public int StackSizeDelta => stackSizeDelta;

    public List<(HandlerPart HandlerPart, ExceptionHandlerType HandlerType)> HandlerParts =>
        handlerParts;

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

    public override string ToString()
    {
        if (HandlerParts.Count == 0)
            return $"{StackSize, 2} | {Inst}";

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

    internal static List<InformationalInstruction> CreateListFor(MethodBody body)
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

        foreach (var instruction in body.Instructions)
        {
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

            int oldStackSize = stackSize;
            ComputeStackSize(instruction, ref stackSizes, ref stackSize, ref maxStack);
            int stackSizeDelta = stackSize - oldStackSize;

            InformationalInstruction ins = new(
                instruction,
                stackSize,
                stackSizeDelta,
                handlerParts
            );
            informationalInstructions.Add(ins);
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
        Instruction instruction,
        ref Dictionary<Instruction, int> stack_sizes,
        ref int stack_size,
        ref int max_stack
    )
    {
        if (stack_sizes.TryGetValue(instruction, out int computed_size))
            stack_size = computed_size;

        max_stack = Math.Max(max_stack, stack_size);
        ComputeStackDelta(instruction, ref stack_size);
        max_stack = Math.Max(max_stack, stack_size);

        CopyBranchStackSize(instruction, ref stack_sizes, stack_size);
        // Mono.Cecil would call this here:
        // ComputeStackSize(instruction, ref stack_size);
        // But it just sets stack size to 0 in some cases
        // which we don't want in this case.
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
                Instruction ins = instruction;

                if (instruction.Operand is ILLabel label)
                    ins.Operand = label.InteropGetTarget()!;

                CopyBranchStackSize(ref stack_sizes, (Instruction)ins.Operand, stack_size);
                break;

            case OperandType.InlineSwitch:
                Instruction[] targets;
                if (instruction.Operand is ILLabel[] labels)
                {
                    targets = [.. labels.Select(x => x.InteropGetTarget()!)];
                }
                else
                {
                    targets = (Instruction[])instruction.Operand;
                }
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

    static void ComputeStackSize(Instruction instruction, ref int stack_size)
    {
        switch (instruction.OpCode.FlowControl)
        {
            case FlowControl.Branch:
            case FlowControl.Throw:
            case FlowControl.Return:
                stack_size = 0;
                break;
        }
    }

    internal static void ComputeStackDelta(Instruction instruction, ref int stack_size)
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
}

internal static class StackSizeAnalyzer
{
    internal static void Analyze(MethodBody body)
    {
        StringBuilder sb = new();
        sb.AppendLine("\n--- MonoDetour CIL Stack Analysis Start Full Method ---");
        sb.AppendLine();
        sb.AppendLine("INFO: Stack size is on the left, instructions are on the right.");
        sb.AppendLine();

        if (body is null)
        {
            sb.AppendLine("Method Body is null, can't analyze.");
            sb.AppendLine();
            sb.AppendLine("--- MonoDetour CIL Stack Analysis End ---");
            return;
        }

        body.Method.RecalculateILOffsets();
        var informationalInstructions = InformationalInstruction.CreateListFor(body);

        int i = 0;
        bool anyErrorFound = false;
        bool negativeStackSizeFound = false;
        foreach (var instruction in informationalInstructions)
        {
            sb.AppendLine(instruction.ToString());

            bool notDuplicateNegative = !negativeStackSizeFound;
            string? error = AnalyzeInstructionIsFine(
                informationalInstructions,
                i,
                ref negativeStackSizeFound
            );
            if (error is not null && notDuplicateNegative)
            {
                anyErrorFound = true;
                sb.Append(error);
            }
            i++;
        }

        sb.AppendLine();
        sb.AppendLine("--- MonoDetour CIL Stack Analysis Summary ---");
        sb.AppendLine();

        if (!anyErrorFound)
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

            i = 0;
            negativeStackSizeFound = false;
            foreach (var instruction in informationalInstructions)
            {
                bool notDuplicateNegative = !negativeStackSizeFound;
                string? error = AnalyzeInstructionIsFine(
                    informationalInstructions,
                    i,
                    ref negativeStackSizeFound
                );
                if (error is not null && notDuplicateNegative)
                {
                    sb.AppendLine(instruction.ToString());
                    sb.Append(error);
                }
                i++;
            }
            sb.AppendLine();
            sb.AppendLine("NOTE: This analysis may not be perfect.");
            sb.AppendLine("TIP:  Pay close attention to stack sizes.");
            sb.Append("      A branch pointing to an instruction can cause its ")
                .AppendLine("stack size to increase seemingly out of nowhere.");
        }

        sb.AppendLine();
        sb.Append("--- MonoDetour CIL Stack Analysis End ---");

        Console.WriteLine(sb.ToString());
    }

    static string? AnalyzeInstructionIsFine(
        List<InformationalInstruction> instructions,
        int index,
        ref bool negativeStackSizeFound
    )
    {
        StringBuilder sb;
        var instruction = instructions[index];
        var realInstruction = instruction.Inst;
        var stackSize = instruction.StackSize;
        var handlerParts = instruction.HandlerParts;

        // Stack on Try start must be 0
        if (
            stackSize != 0
            && handlerParts.Any(x =>
                x.HandlerPart.HasFlag(InformationalInstruction.HandlerPart.BeforeTryStart)
            )
        )
        {
            sb = new StringBuilder()
                .Append($" └── ERROR: Stack size before try start must be 0; it was ")
                .AppendLine(stackSize.ToString());
        }
        else if (stackSize < 0)
        {
            negativeStackSizeFound = true;
            sb = new StringBuilder()
                .Append(" └── ERROR: Negative stack size; cannot be ")
                .AppendLine(stackSize.ToString());
        }
        else if (realInstruction.OpCode.FlowControl is FlowControl.Return && stackSize != 0)
        {
            sb = new StringBuilder()
                .Append(" └── ERROR: Stack size on return must be 0; it was ")
                .AppendLine(stackSize.ToString());
        }
        else
        {
            // Everything is fine!
            return null;
        }

        var problematicIndex = GetProblematicInstructionIndexWithWalkBack(
            instructions,
            index,
            stackSize
        );

        sb.Append("   ¦ │ ").AppendLine("INFO: Stack imbalance starts at:");

        var enumeration = instructions[problematicIndex];

        if (enumeration != instruction)
        {
            sb.Append("   ¦ ├ ").AppendLine(enumeration.ToString());
            problematicIndex++;
            enumeration = instructions[problematicIndex];
        }

        while (enumeration != instruction)
        {
            sb.Append("   ¦ │ ").AppendLine(enumeration.ToString());
            problematicIndex++;
            enumeration = instructions[problematicIndex];
        }

        sb.Append("   ¦ └ ").AppendLine(enumeration.ToString());
        sb.AppendLine("   ¦");

        return sb.ToString();
    }

    static int GetProblematicInstructionIndexWithWalkBack(
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

            if (index == 0)
            {
                Console.WriteLine("index is 0!");
                break;
            }

            index--;
            targetInstruction = instructions[index];
        }

        return index;
    }
}
