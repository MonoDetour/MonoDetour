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
    public HashSet<InformationalInstruction> IncomingBranches { get; private set; } = [];

    /// <summary>
    /// The immediate previous instruction in the list or an incoming branch if
    /// control flow stops at the immediate previous instruction.
    /// </summary>
    /// <remarks>
    /// In a case where there are multiple incoming branches,
    /// this will point to the first evaluated incoming branch.<br/>
    /// If you care about finding a previous incoming invalid stack size somewhere,
    /// this will work fine.<br/>
    /// <br/>
    /// In a case where an incoming invalid stack size exists on a branch other than
    /// what is found via backtracking with this, it's not a problem because the error
    /// in that case is stack size mismatch which is evaluated during the CIL instruction
    /// crawling phase. And in such a case we do NOT evaluate further errors after that point
    /// because the whole stack after that point is invalid and any stack size related
    /// error would be misleading.
    /// </remarks>
    public InformationalInstruction? PreviousChronological { get; private set; }
    public InformationalInstruction? Next { get; internal set; }
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
    public int StackSizeBefore =>
        PreviousChronological?.StackSize
        ?? (
            HandlerParts.Any(x =>
                (x.HandlerPart & HandlerPart.FilterOrHandlerStart) != HandlerPart.None
            )
                ? 1
                : 0
        );
    public int StackPop { get; private set; }
    public int StackPush { get; private set; }

    /// <summary>
    /// The minimum distance from this instruction to the first instruction.
    /// </summary>
    public int Distance { get; private set; }

    /// <summary>
    /// True if this instruction can not be reached from anywhere in the method based
    /// on the OpCode of instructions. Instructions after conditional branches are
    /// evaluated and considered reachable.
    /// </summary>
    public bool Unreachable => !explored;

    public List<(HandlerPart HandlerPart, ExceptionHandlerType HandlerType)> HandlerParts =>
        handlerParts;

    public List<Annotation> ErrorAnnotations { get; } = [];

    public bool HasErrorAnnotations => ErrorAnnotations.Count != 0;

    private bool explored = false;

    internal const string LongPipe = "│";
    internal const string LeftWall = LongPipe;
    internal const string EmptyPad = $"{LeftWall}   ¦ ";
    internal static string VariableEmptyPad = $"{LeftWall}   ¦ ";

    public class AnnotationStackSizeMustBeX(string Message, AnnotationRange? Range)
        : Annotation(Message, Range)
    {
        public override string ToString() => base.ToString();
    }

    public class AnnotationPoppingMoreThanStackSize(string Message) : Annotation(Message, null)
    {
        public override string ToString() => base.ToString();
    }

    public class AnnotationDuplicateInstance()
        : Annotation($"Warning: Duplicate instruction instance; These may break the method.", null)
    {
        public override string ToString() => base.ToString();
    }

    public class AnnotationNullOperand() : Annotation($"Error: Operand is null", null)
    {
        public override string ToString() => base.ToString();
    }

    public class AnnotationNullSwitchTarget() : Annotation($"Error: Null switch target", null)
    {
        public override string ToString() => base.ToString();
    }

    public class AnnotationStackSizeMismatch(
        string Message,
        InformationalInstruction MismatchInstruction
    ) : Annotation(Message, null)
    {
        readonly string message = Message;

        public override string ToString()
        {
            StringBuilder sb = new();
            sb.Append(message);

            var incomingBranches = MismatchInstruction.IncomingBranches;

            var last = incomingBranches.Last();
            foreach (var m in incomingBranches)
            {
                if (m == last)
                    break;

                sb.Append(", ").Append(m.StackSize);
            }

            sb.Append(" and ").AppendLine(last.StackSize.ToString());

            var previous = MismatchInstruction.PreviousChronological!;
            if (!incomingBranches.Contains(previous))
            {
                sb.AppendLine($"{VariableEmptyPad}│ Info: Previous instruction:");
                sb.Append($"{VariableEmptyPad}├ ").AppendLine(previous.ToString());
            }

            sb.AppendLine($"{VariableEmptyPad}│ Info: Incoming branches:");

            foreach (var incomingBranch in incomingBranches)
            {
                if (incomingBranch == last)
                    break;

                sb.Append($"{VariableEmptyPad}├ ").AppendLine(incomingBranch.ToString());
            }

            sb.Append($"{VariableEmptyPad}└ ").Append(last.ToString());

            return sb.ToString();
        }
    }

    public class Annotation(string Message, AnnotationRange? Range)
    {
        public override string ToString()
        {
            StringBuilder sb = new();
            sb.Append(Message);

            if (Range is null || Range.Instructions.Count == 0)
            {
                return sb.ToString();
            }

            var instructions = Range.Instructions;
            var start = 0;
            var end = instructions.Count - 1;

            sb.AppendLine();
            sb.AppendLine($"{VariableEmptyPad}│ Info: Stack imbalance starts at:");

            if (start != end)
            {
                sb.Append($"{VariableEmptyPad}├ ").AppendLine(instructions[start].ToString());
            }

            for (int i = start + 1; i < end; i++)
            {
                sb.Append($"{VariableEmptyPad}│ ").AppendLine(instructions[i].ToString());
            }

            sb.Append($"{VariableEmptyPad}└ ").Append(instructions[end].ToString());

            return sb.ToString();
        }
    }

    public class AnnotationRangeWalkBack(
        InformationalInstruction Instruction,
        int RequiredStackSize
    ) : AnnotationRange(Instruction.GetProblematicInstructionsWithWalkBack(RequiredStackSize));

    public class AnnotationRange(List<InformationalInstruction> Instructions)
    {
        public List<InformationalInstruction> Instructions { get; } = Instructions;
    }

    [Flags]
    public enum HandlerPart
    {
        None = 0,
        TryStart = 1 << 0,
        TryEnd = 1 << 1,
        FilterStart = 1 << 2,
        HandlerStart = 1 << 3,
        HandlerEnd = 1 << 4,
        BeforeTryStart = 1 << 5,
        TryOrHandlerEnd = TryEnd | HandlerEnd,
        FilterOrHandlerStart = FilterStart | HandlerStart,
    }

    public override string ToString() => ToStringInternal(withAnnotations: false);

    public string ToStringWithAnnotations() => ToStringInternal(withAnnotations: true);

    internal string ToStringInternal(bool withAnnotations)
    {
        StringBuilder sb = new();

        if (withAnnotations)
        {
            foreach (var (handlerPart, handlerType) in HandlerParts)
            {
                if (handlerPart.HasFlag(HandlerPart.TryEnd))
                    sb.AppendLine(EmptyPad + "} // end try");

                if (handlerPart.HasFlag(HandlerPart.HandlerEnd))
                    sb.AppendLine(HandlerTypeToStringEnd(handlerType));
            }

            foreach (var (handlerPart, handlerType) in HandlerParts)
            {
                if (handlerPart.HasFlag(HandlerPart.TryStart))
                    sb.AppendLine(EmptyPad + ".try\n" + EmptyPad + "{");

                if (handlerPart.HasFlag(HandlerPart.FilterStart))
                    sb.AppendLine(EmptyPad + "filter\n" + EmptyPad + "{");

                if (handlerPart.HasFlag(HandlerPart.HandlerStart))
                {
                    if (handlerType == ExceptionHandlerType.Filter)
                        sb.AppendLine(EmptyPad + "} // end filter");

                    sb.AppendLine(HandlerTypeToStringStart(handlerType));
                }
            }
        }

        if (Unreachable)
            sb.Append($"{LeftWall} - | {Inst}");
        else
        {
            if (withAnnotations)
            {
                TryAppendIncomingBranchesInfo(sb, IncomingBranches);
                sb.Append($"{LeftWall}{StackSize, 2} | {Inst}");
            }
            else
            {
                sb.Append($"{StackSize, 2} | {Inst}");
            }
        }

        if (withAnnotations)
        {
            if (HasErrorAnnotations)
            {
                VariableEmptyPad = $"{LeftWall} {LongPipe}  ";
                var last = ErrorAnnotations[^1];
                foreach (var annotation in ErrorAnnotations)
                {
                    sb.AppendLine();
                    if (annotation != last)
                    {
                        sb.Append($"{LeftWall} ├ ");
                    }
                    else
                    {
                        sb.Append($"{LeftWall} └ ");
                        VariableEmptyPad = EmptyPad;
                    }
                    sb.Append(annotation.ToString());
                }
            }
            else
            {
                if (
                    Inst.OpCode.FlowControl
                        is FlowControl.Branch
                            or FlowControl.Cond_Branch
                            or FlowControl.Return
                            or FlowControl.Throw
                    && ( // get rid of space before the } bracket
                        (
                            Next?.HandlerParts.Any(x =>
                                (x.HandlerPart & HandlerPart.TryOrHandlerEnd) != HandlerPart.None
                            ) ?? true
                        ) == false
                    )
                )
                {
                    sb.AppendLine().Append(EmptyPad);
                }
            }
        }

        return sb.ToString();
    }

    static void TryAppendIncomingBranchesInfo(
        StringBuilder sb,
        IEnumerable<InformationalInstruction> incomingBranches
    )
    {
        if (incomingBranches.FirstOrDefault() == null)
        {
            return;
        }

        sb.Append($"{EmptyPad}┌ Incoming branches:");

        foreach (var informational in incomingBranches)
        {
            sb.Append(" IL_");
            sb.Append(informational.Inst.Offset.ToString("x4"));
            sb.Append(";");
        }
        sb.AppendLine();
    }

    static string HandlerTypeToStringStart(ExceptionHandlerType handlerType)
    {
        return handlerType switch
        {
            ExceptionHandlerType.Catch => EmptyPad + "catch\n" + EmptyPad + "{",
            ExceptionHandlerType.Filter => EmptyPad + "catch\n" + EmptyPad + "{",
            ExceptionHandlerType.Fault => EmptyPad + "fault\n" + EmptyPad + "{",
            ExceptionHandlerType.Finally => EmptyPad + "finally\n" + EmptyPad + "{",
            _ => throw new ArgumentOutOfRangeException(handlerType.ToString()),
        };
    }

    static string HandlerTypeToStringEnd(ExceptionHandlerType handlerType)
    {
        return handlerType switch
        {
            ExceptionHandlerType.Catch => EmptyPad + "} // end catch",
            ExceptionHandlerType.Filter => EmptyPad + "} // end catch",
            ExceptionHandlerType.Fault => EmptyPad + "} // end fault",
            ExceptionHandlerType.Finally => EmptyPad + "} // end finally",
            _ => throw new ArgumentOutOfRangeException(handlerType.ToString()),
        };
    }

    // This logic is heavily based on:
    // https://github.com/jbevain/cecil/blob/3136847e/Mono.Cecil.Cil/CodeWriter.cs#L332-L341
    internal static void CrawlInstructions(
        InformationalInstruction instruction,
        Dictionary<Instruction, InformationalInstruction> map,
        ref int stackSize,
        MethodBody body,
        int distance,
        bool outsideExceptionHandler = true
    )
    {
        var enumerable = instruction;

        while (true)
        {
            if (enumerable.explored)
            {
                if (enumerable.StackSizeBefore != stackSize)
                {
                    if (!enumerable.HasErrorAnnotations)
                    {
                        enumerable.ErrorAnnotations.Add(
                            new AnnotationStackSizeMismatch(
                                $"Error: Stack size mismatch; incoming stack size "
                                    + $"is both {enumerable.StackSizeBefore}",
                                enumerable
                            )
                        );
                        // The distance after this point does not need to be set to the minimum
                    }
                    // since those instructions won't be evaluated for errors anyways.
                    return;
                }

                if (outsideExceptionHandler && distance < enumerable.Distance)
                {
                    goto evaluateDistanceAndMoveNext;
                }
                return;
            }

            ComputeStackDelta(enumerable, ref stackSize, body);
            enumerable.explored = true;

            evaluateDistanceAndMoveNext:
            enumerable.Distance = distance;
            if (outsideExceptionHandler)
                distance += 10_000;
            else
                distance += 1;

            TryCrawlBranch(enumerable, map, stackSize, body, distance, outsideExceptionHandler);

            if (
                enumerable.Inst.OpCode.FlowControl
                is FlowControl.Branch
                    or FlowControl.Throw
                    or FlowControl.Return
            )
            {
                return;
            }
            var previous = enumerable;
            enumerable = enumerable.Next;

            if (enumerable is null)
                return;

            enumerable.PreviousChronological = previous;
        }
    }

    static void TryCrawlBranch(
        InformationalInstruction informationalInstruction,
        Dictionary<Instruction, InformationalInstruction> map,
        int stackSize,
        MethodBody body,
        int distance,
        bool outsideExceptionHandler
    )
    {
        var instruction = informationalInstruction.Inst;

        switch (instruction.OpCode.OperandType)
        {
            case OperandType.ShortInlineBrTarget:
            case OperandType.InlineBrTarget:
                Instruction? target;

                if (instruction.Operand is ILLabel label)
                    target = label.InteropGetTarget();
                else
                    target = instruction.Operand as Instruction;

                if (target is null)
                {
                    informationalInstruction.ErrorAnnotations.Add(new AnnotationNullOperand());
                    break;
                }

                var informationalTarget = map[target];
                informationalTarget.IncomingBranches.Add(informationalInstruction);

                // This will get overridden if the target is explored later,
                // which is what we want.
                if (!informationalTarget.explored)
                    informationalTarget.PreviousChronological = informationalInstruction;

                CrawlInstructions(
                    informationalTarget,
                    map,
                    ref stackSize,
                    body,
                    distance,
                    outsideExceptionHandler
                );
                break;

            case OperandType.InlineSwitch:
                Instruction?[]? targets;

                if (instruction.Operand is ILLabel[] labels)
                    targets = [.. labels.Select(x => x.InteropGetTarget())];
                else
                    targets = instruction.Operand as Instruction[];

                if (targets is null)
                {
                    informationalInstruction.ErrorAnnotations.Add(new AnnotationNullOperand());
                    break;
                }

                for (int i = 0; i < targets.Length; i++)
                {
                    var switchTarget = targets[i];
                    if (switchTarget is null)
                    {
                        informationalInstruction.ErrorAnnotations.Add(
                            new AnnotationNullSwitchTarget()
                        );
                        continue;
                    }

                    informationalTarget = map[switchTarget];
                    informationalTarget.IncomingBranches.Add(informationalInstruction);

                    if (!informationalTarget.explored)
                        informationalTarget.PreviousChronological = informationalInstruction;

                    CrawlInstructions(
                        informationalTarget,
                        map,
                        ref stackSize,
                        body,
                        distance,
                        outsideExceptionHandler
                    );
                }
                break;
        }
    }

    static void ComputeStackDelta(
        InformationalInstruction informationalInstruction,
        ref int stackSize,
        MethodBody body
    )
    {
        var instruction = informationalInstruction.Inst;
        int oldStackSize = stackSize;

        switch (instruction.OpCode.FlowControl)
        {
            case FlowControl.Call:
            {
                if (instruction.Operand is not IMethodSignature method)
                {
                    informationalInstruction.ErrorAnnotations.Add(new AnnotationNullOperand());
                    break;
                }
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
            case FlowControl.Return:
                if (body.Method.ReturnType.MetadataType != MetadataType.Void)
                {
                    stackSize--;
                    informationalInstruction.StackPop = 1;
                }
                break;
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

    internal List<InformationalInstruction> GetProblematicInstructionsWithWalkBack(
        int requiredStackSize
    )
    {
        Stack<InformationalInstruction> walkBackInstructions = [];

        int walkBackStackSize = 0;
        InformationalInstruction targetInstruction = this;

        while (true)
        {
            walkBackInstructions.Push(targetInstruction);

            walkBackStackSize += targetInstruction.StackSizeDelta;

            if (walkBackStackSize == requiredStackSize)
                break;

            var previous = targetInstruction.PreviousChronological;
            if (previous is null)
                break;

            // Find the shortest path.
            // We know that the undesired stack size must be at the earlier branch
            // because otherwise there would be a stack size mismatch which we
            // already check for and do not evaluate errors after it.
            foreach (var branch in targetInstruction.IncomingBranches)
            {
                if (branch.Distance < previous.Distance)
                {
                    previous = branch;
                }
            }

            targetInstruction = previous;
        }

        return [.. walkBackInstructions];
    }

    public HashSet<InformationalInstruction> CollectIncoming()
    {
        HashSet<InformationalInstruction> collected = [];
        CollectIncoming(this, ref collected);
        collected.Remove(this);
        return collected;
    }

    public static void CollectIncoming(
        InformationalInstruction instruction,
        ref HashSet<InformationalInstruction> collected
    )
    {
        while (true)
        {
            collected.Add(instruction);

            foreach (var branch in instruction.IncomingBranches)
                CollectIncoming(branch, ref collected);

            var previous = instruction.PreviousChronological;
            if (previous is null || collected.Contains(previous))
                break;

            instruction = previous;
        }
    }
}
