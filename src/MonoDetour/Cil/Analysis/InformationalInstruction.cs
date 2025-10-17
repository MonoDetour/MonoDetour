using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoDetour.Interop.MonoModUtils;
using MonoMod.Cil;
using static MonoDetour.Cil.Analysis.IInformationalInstruction;

namespace MonoDetour.Cil.Analysis;

/// <summary>
/// A Mono.Cecil <see cref="Mono.Cecil.Cil.Instruction"/> wrapper
/// which contains extra information about the instruction for analysis.
/// </summary>
public interface IInformationalInstruction
{
    /// <summary>
    /// Incoming branches to this instruction.
    /// </summary>
    HashSet<IInformationalInstruction> IncomingBranches { get; }

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
    IInformationalInstruction? PreviousChronological { get; }

    /// <summary>
    /// The immediate instruction before this instruction.
    /// </summary>
    /// <remarks>
    /// The previous instruction may be unreachable, in which case any of its
    /// informational data not evaluated. If you want to get the "real" previous
    /// instruction, use <see cref="PreviousChronological"/>.
    /// </remarks>
    IInformationalInstruction? Previous { get; }

    /// <summary>
    /// The immediate instruction after this instruction.
    /// </summary>
    IInformationalInstruction? Next { get; }

    /// <summary>
    /// The Mono.Cecil <see cref="Mono.Cecil.Cil.Instruction"/> wrapped by this
    /// <see cref="IInformationalInstruction"/>.
    /// </summary>
    Instruction Instruction { get; }

    /// <summary>
    /// The calculated stack size at the end of this instruction.
    /// </summary>
    int StackSize { get; }

    /// <summary>
    /// The stack size before we got to this instruction.
    /// </summary>
    int IncomingStackSize { get; }

    /// <summary>
    /// The total change to the stack size done by this instruction.
    /// Calculated by summing up <see cref="StackPop"/> and <see cref="StackPush"/>.
    /// </summary>
    int StackDelta { get; }

    /// <summary>
    /// The pop behavior of this instruction.
    /// </summary>
    int StackPop { get; }

    /// <summary>
    /// The push behavior of this instruction.
    /// </summary>
    int StackPush { get; }

    /// <summary>
    /// The minimum relative distance from the first instruction in the method body,
    /// taking branching into account.
    /// </summary>
    /// <remarks>
    /// Exception handlers are exceptional, which is why this distance has
    /// to be relative.
    /// </remarks>
    int RelativeDistance { get; }

    /// <summary>
    /// If this instruction can be reached through following the control flow of the instructions.
    /// Instructions after conditional branches are always evaluated and considered reachable.
    /// </summary>
    bool IsReachable { get; }

    /// <summary>
    /// If this instruction was evaluated after it was created. This is the same as
    /// <see cref="IsReachable"/> when unreachable instructions aren't evaluated such as when
    /// using <see cref="MethodBodyExtensions.CreateInformationalSnapshotJIT(MethodBody)"/>
    /// </summary>
    bool IsEvaluated { get; }

    /// <summary>
    /// A list of error annotations on this instruction.
    /// </summary>
    List<IAnnotation> ErrorAnnotations { get; }

    /// <summary>
    /// Whether or not this instruction has any error annotations.
    /// </summary>
    bool HasErrorAnnotations { get; }

    /// <summary>
    /// Exception handler information related to this instruction.
    /// </summary>
    ReadOnlyCollection<IHandlerInfo> HandlerInfos { get; }

    /// <summary>
    /// Collects all incoming instructions, excluding <see langword="this"/>
    /// <see cref="IInformationalInstruction"/>.
    /// </summary>
    /// <returns>All incoming instructions.</returns>
    HashSet<IInformationalInstruction> CollectIncoming();

    /// <summary>
    /// Returns a string presentation of this <see cref="IInformationalInstruction"/>.
    /// </summary>
    string ToString();

    /// <summary>
    /// Returns a string presentation of this <see cref="IInformationalInstruction"/>,
    /// including error annotations.
    /// </summary>
    string ToStringWithAnnotations();

    /// <summary>
    /// An annotation.
    /// </summary>
    public interface IAnnotation;

    /// <summary>
    /// Exception handler information related to an <see cref="IInformationalInstruction"/>.
    /// </summary>
    public interface IHandlerInfo
    {
        /// <summary>
        /// The handler part of this instruction.
        /// </summary>
        HandlerPart HandlerPart { get; }

        /// <summary>
        /// The Mono.Cecil <see cref="ExceptionHandler"/> this instruction belongs to.
        /// </summary>
        ExceptionHandler Handler { get; }

        /// <summary>
        /// Deconstructs this <see cref="IHandlerInfo"/>.
        /// </summary>
        public void Deconstruct(out HandlerPart handlerPart, out ExceptionHandler exceptionHandler);
    }

    /// <summary>
    /// The handler part of an instruction.
    /// </summary>
    [Flags]
    public enum HandlerPart
    {
        /// <summary>
        /// This is not a part of any Exception handler block.
        /// </summary>
        None = 0,

        /// <summary>
        /// Start of a try block.
        /// </summary>
        TryStart = 1 << 0,

        /// <summary>
        /// End of a try block (exclusive).
        /// </summary>
        TryEnd = 1 << 1,

        /// <summary>
        /// Start of a filter block.
        /// </summary>
        FilterStart = 1 << 2,

        /// <summary>
        /// Start of a handler block.
        /// </summary>
        HandlerStart = 1 << 3,

        /// <summary>
        /// End of a try block (exclusive).
        /// </summary>
        HandlerEnd = 1 << 4,

        /// <summary>
        /// One instruction before the start of a  try block.
        /// </summary>
        BeforeTryStart = 1 << 5,

        /// <summary>
        /// End of a try or handler block (exclusive).
        /// </summary>
        TryOrHandlerEnd = TryEnd | HandlerEnd,

        /// <summary>
        /// Start of a filter or handler block.
        /// </summary>
        FilterOrHandlerStart = FilterStart | HandlerStart,
    }
}

internal sealed class InformationalInstruction(
    Instruction instruction,
    int stackSize,
    int stackSizeDelta,
    List<IHandlerInfo> handlerParts
) : IInformationalInstruction
{
    public HashSet<IInformationalInstruction> IncomingBranches { get; private set; } = [];
    public IInformationalInstruction? PreviousChronological { get; internal set; }
    public IInformationalInstruction? Previous { get; internal set; }
    public IInformationalInstruction? Next => next;
    internal InformationalInstruction? next;

    public Instruction Instruction => instruction;
    public int StackSize
    {
        get => stackSize;
        private set => stackSize = value;
    }
    public int StackDelta
    {
        get => stackSizeDelta;
        private set => stackSizeDelta = value;
    }
    public int IncomingStackSize =>
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
    public int RelativeDistance { get; internal set; }
    public bool IsReachable { get; private set; }
    public bool IsEvaluated => explored;
    public List<IHandlerInfo> HandlerParts => handlerParts;
    public ReadOnlyCollection<IHandlerInfo> HandlerInfos => HandlerParts.AsReadOnly();

    public List<IAnnotation> ErrorAnnotations { get; } = [];

    public bool HasErrorAnnotations => ErrorAnnotations.Count != 0;

    private bool explored = false;

    internal const string LongPipe = "│";
    internal const string LeftWall = LongPipe;
    internal const string EmptyPad = $"{LeftWall}   ¦ ";
    internal static string VariableEmptyPad = $"{LeftWall}   ¦ ";

    internal class HandlerInfo(HandlerPart handlerPart, ExceptionHandler handler) : IHandlerInfo
    {
        public HandlerPart HandlerPart => handlerPart;

        public ExceptionHandler Handler => handler;

        public void Deconstruct(out HandlerPart part, out ExceptionHandler exceptionHandler)
        {
            part = HandlerPart;
            exceptionHandler = Handler;
        }
    }

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
        : Annotation(
            $"Warning: Duplicate instruction instance; This may break the method and analysis.",
            null
        )
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

            var previous = MismatchInstruction.PreviousChronological;
            if (previous is null)
            {
                sb.AppendLine($"{VariableEmptyPad}│ Info: Previous instruction:");
                sb.Append($"{VariableEmptyPad}├ ").AppendLine($" 0 | <before method body>");
            }
            else if (!incomingBranches.Contains(previous))
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

    public class Annotation(string Message, AnnotationRange? Range) : IAnnotation
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
        IInformationalInstruction Instruction,
        int RequiredStackSize
    ) : AnnotationRange(GetProblematicInstructionsWithWalkBack(Instruction, RequiredStackSize));

    public class AnnotationRange(List<IInformationalInstruction> Instructions)
    {
        public List<IInformationalInstruction> Instructions { get; } = Instructions;
    }

    public override string ToString() => ToStringInternal(withAnnotations: false);

    public string ToStringWithAnnotations() => ToStringInternal(withAnnotations: true);

    internal string ToStringInternal(bool withAnnotations)
    {
        StringBuilder sb = new();

        if (withAnnotations)
        {
            foreach (var (handlerPart, handler) in HandlerParts)
            {
                if (handlerPart.HasFlag(HandlerPart.TryEnd))
                    sb.AppendLine(EmptyPad + "} // end try");

                if (handlerPart.HasFlag(HandlerPart.HandlerEnd))
                    sb.AppendLine(HandlerTypeToStringEnd(handler.HandlerType));
            }

            foreach (var (handlerPart, handler) in HandlerParts)
            {
                if (handlerPart.HasFlag(HandlerPart.TryStart))
                    sb.AppendLine(EmptyPad + ".try\n" + EmptyPad + "{");

                if (handlerPart.HasFlag(HandlerPart.FilterStart))
                    sb.AppendLine(EmptyPad + "filter\n" + EmptyPad + "{");

                if (handlerPart.HasFlag(HandlerPart.HandlerStart))
                {
                    if (handler.HandlerType == ExceptionHandlerType.Filter)
                        sb.AppendLine(EmptyPad + "} // end filter");

                    sb.AppendLine(
                        HandlerTypeToStringStart(handler.HandlerType, handler.CatchType?.ToString())
                    );
                }
            }
        }

        if (!IsEvaluated)
            sb.Append($"{LeftWall} - | {Instruction}");
        else
        {
            if (withAnnotations)
            {
                TryAppendIncomingBranchesInfo(sb, IncomingBranches);
                sb.Append($"{LeftWall}{StackSize, 2} | {Instruction}");
            }
            else
            {
                sb.Append($"{StackSize, 2} | {Instruction}");
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
                    Instruction.OpCode.FlowControl
                        is FlowControl.Branch
                            or FlowControl.Cond_Branch
                            or FlowControl.Return
                            or FlowControl.Throw
                    && ( // get rid of space before the } bracket
                        (
                            Next?.HandlerInfos.Any(x =>
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
        IEnumerable<IInformationalInstruction> incomingBranches
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
            sb.Append(informational.Instruction.Offset.ToString("x4"));
            sb.Append(';');
        }
        sb.AppendLine();
    }

    static string HandlerTypeToStringStart(ExceptionHandlerType handlerType, string? catchType)
    {
        return handlerType switch
        {
            ExceptionHandlerType.Catch => EmptyPad + $"catch ({catchType})\n" + EmptyPad + "{",
            ExceptionHandlerType.Filter => EmptyPad + $"catch ({catchType})\n" + EmptyPad + "{",
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
        int stackSize,
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
                if (enumerable.IncomingStackSize != stackSize)
                {
                    if (!enumerable.HasErrorAnnotations && distance >= 0)
                    {
                        enumerable.ErrorAnnotations.Add(
                            new AnnotationStackSizeMismatch(
                                $"Error: Stack size mismatch; incoming stack size "
                                    + $"is both {enumerable.IncomingStackSize}",
                                enumerable
                            )
                        );
                        // The distance after this point does not need to be set to the minimum
                    }
                    // since those instructions won't be evaluated for errors anyways.
                    return;
                }

                if (
                    outsideExceptionHandler
                    && distance >= 0
                    && distance < enumerable.RelativeDistance
                )
                {
                    goto evaluateDistanceAndMoveNext;
                }
                return;
            }

            ComputeStackDelta(enumerable, ref stackSize, body);
            enumerable.explored = true;
            if (distance >= 0)
            {
                enumerable.IsReachable = true;
            }

            evaluateDistanceAndMoveNext:
            // Important when rewriting distance
            stackSize = enumerable.StackSize;

            enumerable.RelativeDistance = distance;
            if (outsideExceptionHandler)
                distance += 10_000;
            else
                distance += 1;

            TryCrawlBranch(enumerable, map, stackSize, body, distance, outsideExceptionHandler);

            if (
                enumerable.Instruction.OpCode.FlowControl
                is FlowControl.Branch
                    or FlowControl.Throw
                    or FlowControl.Return
            )
            {
                return;
            }
            var previous = enumerable;
            enumerable = enumerable.next;

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
        var instruction = informationalInstruction.Instruction;

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
                    stackSize,
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
                        stackSize,
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
        var instruction = informationalInstruction.Instruction;
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
                // Endfinally and Endfilter have FlowControl.Return:
                // https://github.com/jbevain/cecil/blob/master/Mono.Cecil.Cil/OpCodes.cs
                if (instruction.OpCode == OpCodes.Ret)
                {
                    if (body.Method.ReturnType.MetadataType != MetadataType.Void)
                    {
                        stackSize--;
                        informationalInstruction.StackPop = 1;
                    }
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
        informationalInstruction.StackDelta = stackSize - oldStackSize;
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

    internal static List<IInformationalInstruction> GetProblematicInstructionsWithWalkBack(
        IInformationalInstruction instruction,
        int requiredStackSize
    )
    {
        Stack<IInformationalInstruction> walkBackInstructions = [];

        int walkBackStackSize = 0;
        IInformationalInstruction targetInstruction = instruction;

        while (true)
        {
            walkBackInstructions.Push(targetInstruction);

            walkBackStackSize += targetInstruction.StackDelta;

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
                if (branch.RelativeDistance < previous.RelativeDistance)
                {
                    previous = branch;
                }
            }

            targetInstruction = previous;
        }

        return [.. walkBackInstructions];
    }

    public HashSet<IInformationalInstruction> CollectIncoming()
    {
        HashSet<IInformationalInstruction> collected = [];
        CollectIncoming(this, ref collected);
        collected.Remove(this);
        return collected;
    }

    public static void CollectIncoming(
        IInformationalInstruction instruction,
        ref HashSet<IInformationalInstruction> collected
    )
    {
        while (true)
        {
            if (!collected.Add(instruction))
            {
                return;
            }

            foreach (var branch in instruction.IncomingBranches)
                CollectIncoming(branch, ref collected);

            var previous = instruction.PreviousChronological;
            if (previous is null)
                break;

            instruction = previous;
        }
    }
}
