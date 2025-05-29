using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;

namespace MonoDetour.Cil.Analysis;

internal static class StackSizeAnalyzer
{
    record InformationalInstruction(
        Instruction Instruction,
        int StackSize,
        List<(HandlerPart HandlerPart, ExceptionHandlerType HandlerType)> HandlerParts
    )
    {
        public override string ToString()
        {
            if (HandlerParts.Count == 0)
                return $"{StackSize, 2} | {Instruction}";

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

            sb.Append($"{StackSize, 2} | {Instruction}");

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
            HashSet<Instruction> handlerStarts = [];
            List<InformationalInstruction> informationalInstructions = [];

            foreach (var eh in body.ExceptionHandlers)
            {
                handlerStarts.Add(eh.HandlerStart);
            }

            int stackSize = 0;
            foreach (var instruction in body.Instructions)
            {
                ComputeStackDelta(instruction, ref stackSize);

                // Catch start range has the exception on stack
                if (handlerStarts.Contains(instruction))
                    stackSize++;

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

                InformationalInstruction ins = new(instruction, stackSize, handlerParts);
                informationalInstructions.Add(ins);
            }

            return informationalInstructions;
        }
    }

    [Flags]
    enum HandlerPart
    {
        TryStart = 1 << 0,
        TryEnd = 1 << 1,
        FilterStart = 1 << 2,
        HandlerStart = 1 << 3,
        HandlerEnd = 1 << 4,
        BeforeTryStart = 1 << 5,
    }

    internal static void Analyze(ILManipulationInfo info)
    {
        Console.WriteLine("--- MonoDetour Stack Size Analysis Start ---");
        Console.WriteLine("INFO: Stack size is on the left, instructions on the right.");
        Console.WriteLine();

        info.Context.Method.RecalculateILOffsets();
        var informationalInstructions = InformationalInstruction.CreateListFor(info.Context.Body);

        bool negativeStackSizeFound = false;
        foreach (var instruction in informationalInstructions)
        {
            Console.WriteLine(instruction);

            bool notDuplicateNegative = !negativeStackSizeFound;
            string? error = AnalyzeInstructionIsFine(instruction, ref negativeStackSizeFound);
            if (error is not null && notDuplicateNegative)
            {
                Console.Write(error);
            }
        }

        Console.WriteLine();
        Console.WriteLine("--- MonoDetour Stack Size Analysis Summary ---");
        Console.WriteLine("INFO: Stack size is on the left, instructions on the right.");
        Console.WriteLine();

        negativeStackSizeFound = false;
        foreach (var instruction in informationalInstructions)
        {
            bool notDuplicateNegative = !negativeStackSizeFound;
            string? error = AnalyzeInstructionIsFine(instruction, ref negativeStackSizeFound);
            if (error is not null && notDuplicateNegative)
            {
                Console.WriteLine(instruction);
                Console.Write(error);
            }
        }

        Console.WriteLine();
        Console.WriteLine("--- MonoDetour Stack Size Analysis End ---");
    }

    static string? AnalyzeInstructionIsFine(
        InformationalInstruction informationalInstruction,
        ref bool negativeStackSizeFound
    )
    {
        StringBuilder sb;
        var instruction = informationalInstruction.Instruction;
        var stackSize = informationalInstruction.StackSize;
        var handlerParts = informationalInstruction.HandlerParts;

        // Stack on Try start must be 0
        if (
            stackSize != 0
            && handlerParts.Any(x => x.HandlerPart.HasFlag(HandlerPart.BeforeTryStart))
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
        else if (instruction.OpCode.FlowControl is FlowControl.Return && stackSize != 0)
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

        var problematicInstruction = GetProblematicInstructionWithWalkBack(instruction, stackSize);
        Instruction enumeration = problematicInstruction;
        sb.Append("   ¦ │ ").AppendLine("INFO: Stack imbalance starts at:");

        if (enumeration != instruction)
        {
            sb.Append("   ¦ ├ ").Append(stackSize).Append(" | ").AppendLine(enumeration.ToString());
            enumeration = enumeration.Next;
        }

        while (enumeration != instruction)
        {
            sb.Append("   ¦ │ ").Append(stackSize).Append(" | ").AppendLine(enumeration.ToString());
            enumeration = enumeration.Next;
        }
        sb.Append("   ¦ └ ").Append(stackSize).Append(" | ").AppendLine(enumeration.ToString());
        ;
        sb.AppendLine("   ¦");

        return sb.ToString(); // $"\n| Problematic instruction: {problematicInstruction}\n";
    }

    static Instruction GetProblematicInstructionWithWalkBack(
        Instruction instruction,
        int incorrectStackSize
    )
    {
        int walkBackStackSize = 0;
        Instruction targetInstruction = instruction;

        while (true)
        {
            ComputeStackDelta(targetInstruction, ref walkBackStackSize);

            if (walkBackStackSize == incorrectStackSize)
                break;

            targetInstruction = targetInstruction.Previous;
        }

        return targetInstruction;
    }

    // https://github.com/jbevain/cecil/blob/3136847e/Mono.Cecil.Cil/CodeWriter.cs#L434
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
}
