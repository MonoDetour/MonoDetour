using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;

namespace MonoDetour.Cil.Analysis;

internal static class StackSizeAnalyzer
{
    class InformationalInstruction(
        Instruction instruction,
        int stackSize,
        List<(HandlerPart HandlerPart, ExceptionHandlerType HandlerType)> handlerParts
    )
    {
        public Instruction Instruction => instruction;
        public int StackSize => stackSize;

        public List<(HandlerPart HandlerPart, ExceptionHandlerType HandlerType)> HandlerParts =>
            handlerParts;

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
                if (
                    eh.HandlerType != ExceptionHandlerType.Catch
                    && eh.HandlerType != ExceptionHandlerType.Filter
                )
                {
                    continue;
                }

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

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void Analyze(MethodBody body)
    {
        StringBuilder sb = new();
        sb.AppendLine("--- MonoDetour Stack Size Analysis Start ---");
        sb.AppendLine("INFO: Stack size is on the left, instructions on the right.");
        sb.AppendLine();

        if (body is null)
        {
            sb.AppendLine("Method Body is null, can't analyze.");
            sb.AppendLine("--- MonoDetour Stack Size Analysis End ---");
            return;
        }

        body.Method.RecalculateILOffsets();
        var informationalInstructions = InformationalInstruction.CreateListFor(body);

        int i = 0;
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
                sb.Append(error);
            }
            i++;
        }

        sb.AppendLine();
        sb.AppendLine("--- MonoDetour Stack Size Analysis Summary ---");
        sb.AppendLine("INFO: Stack size is on the left, instructions on the right.");
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
        sb.Append("Analysis by MonoDetour ");
        sb.AppendLine(typeof(StackSizeAnalyzer).Assembly.GetName().Version.ToString());
        sb.AppendLine("NOTE: This analysis may not be perfect.");
        sb.AppendLine("--- MonoDetour Stack Size Analysis End ---");

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
        var realInstruction = instruction.Instruction;
        var stackSize = instruction.StackSize;
        var handlerParts = instruction.HandlerParts;

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
            ComputeStackDelta(targetInstruction.Instruction, ref walkBackStackSize);

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
