using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoDetour.Bindings.Reorg;
using MonoDetour.Interop.MonoModUtils;
using MonoMod.Cil;
using MonoMod.Utils;

namespace MonoDetour.Cil;

public partial class ILWeaver
{
    /// <summary>
    /// Replaces the <paramref name="target"/> instruction.
    /// </summary>
    /// <remarks>
    /// The <paramref name="target"/> instruction instance is left untouched and instead
    /// the actual instance of the instruction that replaces it takes its place,
    /// and as such it steals all the labels of <paramref name="target"/> to itself.<br/>
    /// <br/>
    /// If <see cref="Current"/> points to the instruction being replaced,
    /// it is moved to the <paramref name="replacement"/> instruction.
    /// </remarks>
    /// <param name="target">The instruction to replace.</param>
    /// <param name="replacement">The replacement instruction.</param>
    /// <returns>this <see cref="ILWeaver"/>.</returns>
    public ILWeaver Replace(Instruction target, Instruction replacement)
    {
        InsertAfter(target, replacement);
        return RemoveAndShiftLabels(target);
    }

    /// <summary>
    /// Replaces the <paramref name="target"/> instruction with the first instruction of the
    /// <paramref name="replacement"/> IEnumerable and inserts the rest of the instructions after.
    /// </summary>
    /// <param name="replacement">The replacement instructions.
    /// The first instruction replaces the <paramref name="target"/>.</param>
    /// <returns>this <see cref="ILWeaver"/>.</returns>
    /// <inheritdoc cref="Replace(Instruction, Instruction)"/>
    /// <param name="target"></param>
    public ILWeaver Replace(Instruction target, params IEnumerable<Instruction> replacement)
    {
        var noNull = replacement.Where(x => x is { });
        var first = noNull.FirstOrDefault();
        if (first is null)
            return this;

        Replace(target, first);

        var rest = noNull.Skip(1);
        return InsertAfter(first, rest);
    }

    /// <inheritdoc cref="Replace(Instruction, IEnumerable{Instruction})"/>
    public ILWeaver Replace(
        Instruction target,
        params IEnumerable<InstructionOrEnumerable> replacement
    ) => Replace(target, replacement.Unwrap());

    /// <summary>
    /// Replaces the instruction at <see cref="Current"/>.
    /// </summary>
    /// <remarks>
    /// The <see cref="Current"/> instruction instance is left untouched and instead
    /// the actual instance of the instruction that replaces it takes its place,
    /// and as such it steals all the labels of <see cref="Current"/> to itself.
    /// </remarks>
    /// <param name="replacement">The replacement instruction.</param>
    /// <returns>this <see cref="ILWeaver"/>.</returns>
    public ILWeaver ReplaceCurrent(Instruction replacement)
    {
        Replace(Current, replacement);
        return this;
    }

    /// <summary>
    /// Replaces the <see cref="Current"/> instruction with the first instruction of the
    /// <paramref name="replacement"/> IEnumerable and inserts the rest of the instructions after.
    /// </summary>
    /// <param name="replacement">The replacement instructions.
    /// The first instruction replaces the <see cref="Current"/>.</param>
    /// <returns>this <see cref="ILWeaver"/>.</returns>
    /// <inheritdoc cref="ReplaceCurrent(Instruction)"/>
    public ILWeaver ReplaceCurrent(params IEnumerable<Instruction> replacement) =>
        Replace(Current, replacement);

    /// <inheritdoc cref="ReplaceCurrent(IEnumerable{Instruction})"/>
    public ILWeaver ReplaceCurrent(params IEnumerable<InstructionOrEnumerable> replacement) =>
        ReplaceCurrent(replacement.Unwrap());

    /// <summary>
    /// Replaces the <paramref name="target"/> instruction's Operand.
    /// </summary>
    /// <remarks>
    /// The <paramref name="target"/> instruction instance is left untouched and instead
    /// a copy of the instruction with the replacement operand takes its place,
    /// and as such it steals all the labels of <paramref name="target"/> to itself.<br/>
    /// <br/>
    /// If <see cref="Current"/> points to the instruction being replaced,
    /// it is moved to the replacement instruction.
    /// </remarks>
    /// <param name="target">The instruction whose Operand to replace.</param>
    /// <param name="replacementOperand">The new operand value to replace the old one.</param>
    /// <returns>this <see cref="ILWeaver"/>.</returns>
    public ILWeaver ReplaceOperand(Instruction target, object replacementOperand)
    {
        var replacement = Create(target.OpCode, replacementOperand);
        return Replace(target, replacement);
    }

    /// <summary>
    /// Replaces the <see cref="Current"/> instruction's Operand.
    /// </summary>
    /// <remarks>
    /// The <see cref="Current"/> instruction instance is left untouched and instead
    /// a copy of the instruction with the replacement operand takes its place,
    /// and as such it steals all the labels of <see cref="Current"/> to itself.
    /// </remarks>
    /// <inheritdoc cref="ReplaceOperand(Instruction, object)"/>
    public ILWeaver ReplaceCurrentOperand(object replacementOperand) =>
        ReplaceOperand(Current, replacementOperand);

    /// <summary>
    /// Removes the provided <paramref name="instruction"/> from the method body and
    /// moves all its labels and exception handler range roles to the next instruction.<br/>
    /// <br/>
    /// <b>Important:</b> If you are removing an instruction to replace it, use
    /// <see cref="Replace(Instruction, Instruction)"/> or any of the variants instead.
    /// </summary>
    /// <remarks>
    /// If there is no next instruction, the previous instruction will be the shift target.
    /// If there is no previous instruction either, the next inserted instruction will be
    /// the new shift target.<br/>
    /// <br/>
    /// If <see cref="Current"/> points to an instruction to be removed, it is moved to
    /// the next instruction alongside the labels.<br/>
    /// <br/>
    /// Note: Removing instructions have consequences as described by this method.
    /// This method does what is necessary to not break the target method in cases where
    /// labels would not be shifted. As such, there is no variant which does not shift labels
    /// (that is not deprecate anyways).
    /// </remarks>
    /// <param name="instruction">The instruction to remove.</param>
    /// <returns>This <see cref="ILWeaver"/>.</returns>
    public ILWeaver RemoveAndShiftLabels(Instruction instruction) =>
        RemoveAndShiftLabelsInternal(Instructions.IndexOf(instruction), 1);

    /// <summary>
    /// Removes <see cref="Current"/> from the method body and
    /// moves all its labels and exception handler range roles to the next instruction.<br/>
    /// <br/>
    /// <b>Important:</b> If you are removing an instruction to replace it, use
    /// <see cref="ReplaceCurrent(Instruction)"/> or any of the variants instead.
    /// </summary>
    /// <inheritdoc cref="RemoveAndShiftLabels(Instruction)"/>
    public ILWeaver RemoveCurrentAndShiftLabels() => RemoveAndShiftLabels(Current);

    /// <summary>
    /// Removes the instructions in the inclusive <paramref name="start"/> to
    /// <paramref name="end"/> range from the method body and
    /// moves all their labels and exception handler range roles to the next available instruction.
    /// </summary>
    /// <remarks>
    /// The order of <paramref name="start"/> and <paramref name="end"/> does not matter.<br/>
    /// <br/>
    /// If there is no next instruction, the previous instruction will be the shift target.
    /// If there is no previous instruction either, the next inserted instruction will be
    /// the new shift target.<br/>
    /// <br/>
    /// If <see cref="Current"/> points to an instruction to be removed, it is moved to
    /// the next available instruction alongside the labels.<br/>
    /// <br/>
    /// Exception handlers: If the range contains the <see cref="ExceptionHandler.HandlerStart"/>
    /// and previous instruction of <see cref="ExceptionHandler.HandlerEnd"/> of an
    /// exception handler in a single <see cref="RemoveRangeAndShiftLabels(Instruction, Instruction)"/>
    /// call, the exception handler is removed from the method body as leaving
    /// it be would cause invalid IL. If you wish to rewrite the handler block instead, don't remove
    /// the previous instruction of the leave target which is <see cref="ExceptionHandler.HandlerEnd"/>.<br/>
    /// <br/>
    /// Note: Removing instructions have consequences as described by this method.
    /// This method does what is necessary to not break the target method in cases where
    /// labels would not be shifted. As such, there is no variant which does not shift labels
    /// (that is not deprecate anyways).
    /// </remarks>
    /// <param name="start">The first instruction in the range to remove.</param>
    /// <param name="end">The last instruction in the range to remove.</param>
    /// <returns>This <see cref="ILWeaver"/>.</returns>
    public ILWeaver RemoveRangeAndShiftLabels(Instruction start, Instruction end)
    {
        var startIndex = Instructions.IndexOf(start);
        if (startIndex == -1)
            throw new ArgumentException($"'{nameof(start)}' is not part of the method body.");

        var endIndex = Instructions.IndexOf(end);
        if (endIndex == -1)
            throw new ArgumentException($"'{nameof(end)}' is not part of the method body.");

        var index = Math.Min(startIndex, endIndex);
        var count = Math.Abs(startIndex - endIndex) + 1;
        return RemoveAndShiftLabelsInternal(index, count);
    }

    ILWeaver RemoveAndShiftLabelsInternal(int index, int count, bool insertTemporaryIfNeeded = true)
    {
        int endIndex = index + count - 1;
        int currentIndex = Index;

        if (count < 0)
            throw new IndexOutOfRangeException("Can not remove a negative amount of instructions.");

        if (index == -1)
            throw new IndexOutOfRangeException(
                $"The index -1 or target instruction to be removed does not exist in the method body."
            );
        else if (endIndex >= Instructions.Count)
            throw new IndexOutOfRangeException(
                "Attempted to remove more instructions than there are available."
            );

        // We want to shift labels forward if possible.
        // If not, we will move them backwards for consistency.
        Instruction shiftTarget;
        if (endIndex + 1 < Instructions.Count)
        {
            shiftTarget = Instructions[endIndex + 1];
        }
        else
        {
            if (index != 0)
                shiftTarget = Instructions[index - 1];
            else
            {
                // If there is nowhere for labels to go,
                // they must go into future next inserted instruction.
                // Though, is there a point in doing this?
                // There's no instructions in the method body after this.
                // But let's just do it because maybe there is a use case.
                foreach (var label in Context.Labels.Where(x => x.InteropGetTarget() is { }))
                    pendingFutureNextInsertLabels.Add(label);

                Instructions.Clear();

                if (insertTemporaryIfNeeded)
                {
                    InsertTemporaryCurrentTarget();
                }
                return this;
            }
        }

        if (currentIndex >= index && currentIndex <= endIndex)
        {
            Current = shiftTarget;
        }

        List<ExceptionHandler>? handlersWithHandlerStart = null;

        while (count-- > 0)
        {
            var toRemove = Instructions[index];
            RetargetLabels(toRemove, shiftTarget);
            StealHandlerRole(toRemove, shiftTarget, ref handlersWithHandlerStart);
            Instructions.RemoveAt(index);
        }

        return this;
    }

    /// <summary></summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete(obsoleteMessageRemoveAt, true)]
    public ILWeaver RemoveAt(int index, int instructions, out IEnumerable<ILLabel> orphanedLabels)
    {
        int endIndex = index + instructions - 1;
        int currentIndex = Index;

        if (instructions < 0)
            throw new IndexOutOfRangeException("Can not remove a negative amount of instructions.");

        if (endIndex > Instructions.Count)
            throw new IndexOutOfRangeException(
                "Attempted to remove more instructions than there are available."
            );

        if (currentIndex >= index && currentIndex <= endIndex)
        {
            Current = Instructions[endIndex + 1];
        }

        List<ILLabel> labels = [];

        while (instructions-- > 0)
        {
            foreach (var label in Context.GetIncomingLabels(Instructions[index]))
                labels.Add(label);

            Instructions.RemoveAt(index);
        }

        orphanedLabels = labels;
        return this;
    }

    /// <summary></summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete(obsoleteMessageRemoveAt, true)]
    public ILWeaver RemoveAtCurrent(int instructions, out IEnumerable<ILLabel> orphanedLabels) =>
        RemoveAt(Index, instructions, out orphanedLabels);

    /// <summary></summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete("Use RemoveAndShiftLabels instead.")]
    public ILWeaver Remove(Instruction instruction, out ILLabel? orphanedLabel)
    {
        RemoveAt(Instructions.IndexOf(instruction), 1, out var orphanedLabels);
        orphanedLabel = orphanedLabels.FirstOrDefault();
        return this;
    }

    /// <summary></summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete("Use RemoveCurrentAndShiftLabels instead.")]
    public ILWeaver RemoveCurrent(out ILLabel? orphanedLabel)
    {
        var next = Next;
        Remove(Current, out orphanedLabel);
        CurrentTo(next);
        return this;
    }

    ILWeaver InsertAtInternal(ref int refIndex, Instruction instruction, InsertType insertType)
    {
        Helpers.ThrowIfNull(instruction);
        var index = refIndex;

        if (index == -1)
        {
            // If there are no instructions in the method body, ILWeaver would
            // be incapable of inserting new instructions without this hack.
            // Normally this wouldn't happen because ILWeaver inserts a temporary
            // instruction if instruction count is 0, but users can still remove
            // all instructions by using e.g. Instructions.Clear()
            if (Instructions.Count == 0)
            {
                // We'll have to rewrite refIndex so the potential next
                // instruction to be inserted from an IEnumerable will
                // target our temporary instruction.
                refIndex = 0;
                index = 0;

                InsertTemporaryCurrentTarget();
            }
            else
            {
                throw new IndexOutOfRangeException(
                    $"The index -1 or target instruction to be inserted at does not exist in the method body."
                );
            }
        }

        if (insertType is InsertType.After)
        {
            index += 1;
        }

        if (index > Instructions.Count)
        {
            throw new IndexOutOfRangeException(
                $"The index to be inserted is out of range; index: {index} / instructions: {Instructions.Count}"
            );
        }

        if (insertType is InsertType.BeforeAndStealLabels)
        {
            Instruction instructionAtIndex = Instructions[index];
            StealHandlerRole(instructionAtIndex, instruction);
            RetargetLabels(GetIncomingLabelsFor(instructionAtIndex), instruction);
        }

        RetargetLabels(pendingFutureNextInsertLabels, instruction);
        pendingFutureNextInsertLabels.Clear();

        InstructionOperandToILLabel(instruction);
        Instructions.Insert(index, instruction);
        return this;
    }

    private void StealHandlerRole(Instruction target, Instruction replacement)
    {
        foreach (var eh in Body.ExceptionHandlers)
        {
            if (eh.TryStart == target)
                eh.TryStart = replacement;
            if (eh.HandlerStart == target)
                eh.HandlerStart = replacement;
            if (eh.FilterStart == target)
                eh.FilterStart = replacement;
            if (eh.TryEnd == target)
                eh.TryEnd = replacement;
            if (eh.HandlerEnd == target)
                eh.HandlerEnd = replacement;
        }
    }

    private void StealHandlerRole(
        Instruction target,
        Instruction replacement,
        ref List<ExceptionHandler>? handlersWithHandlerStart
    )
    {
        List<ExceptionHandler>? handlersToRemove = null;

        foreach (var eh in Body.ExceptionHandlers)
        {
            if (eh.TryStart == target)
                eh.TryStart = replacement;
            if (eh.HandlerStart == target)
            {
                eh.HandlerStart = replacement;
                if (handlersWithHandlerStart is null)
                    handlersWithHandlerStart = [eh];
                else
                    handlersWithHandlerStart.Add(eh);
            }
            if (eh.FilterStart == target)
                eh.FilterStart = replacement;
            if (eh.TryEnd == target)
                eh.TryEnd = replacement;

            // HandlerEnd.Previous should be leave instruction,
            // and that's close enough for exception handler removal purposes.
            if (eh.HandlerEnd?.Previous == target)
            {
                if (handlersWithHandlerStart?.Contains(eh) ?? false)
                {
                    // If we are here, we have removed basically the whole handler block
                    // in a single RemoveRangeAndShiftLabels call. This is almost definitely
                    // not something whoever removed these instructions wants to recover from.

                    // If we don't do anything about it, the exception handler will
                    // remain in the method body and explode everything.

                    // However, there's a nonzero chance that they might rewrite the whole catch block.
                    // But if they are going to do that and they end up causing this code to execute,
                    // they are making their lives more difficult than necessary. So we don't support it.
                    if (handlersToRemove is null)
                        handlersToRemove = [eh];
                    else
                        handlersToRemove.Add(eh);
                }
            }
            if (eh.HandlerEnd == target)
                eh.HandlerEnd = replacement;
        }

        if (handlersToRemove is null)
            return;

        foreach (var handlerToRemove in handlersToRemove)
        {
            Body.ExceptionHandlers.Remove(handlerToRemove);
        }
    }

    // MonoMod's instruction matching extensions expect ILLabel[] or ILLabel.
    private void InstructionOperandToILLabel(Instruction instruction)
    {
        if (instruction.Operand is Instruction target)
        {
            instruction.Operand = DefineAndMarkLabelTo(target);
        }
        else if (instruction.Operand is Instruction[] targets)
        {
            ILLabel[] labels = new ILLabel[targets.Length];
            for (int i = 0; i < targets.Length; i++)
            {
                labels[i] = DefineAndMarkLabelTo(targets[i]);
            }
            instruction.Operand = labels;
        }
    }

    /// <summary>
    /// Insert instructions without attracting <see cref="pendingFutureNextInsertLabels"/>.
    /// </summary>
    ILWeaver GhostInsertBefore(Instruction target, Instruction instruction)
    {
        InstructionOperandToILLabel(instruction);
        Instructions.Insert(Instructions.IndexOf(target), instruction);
        return this;
    }

    /// <summary>
    /// Insert instructions without attracting <see cref="pendingFutureNextInsertLabels"/>.
    /// </summary>
    ILWeaver GhostInsertAfter(Instruction target, Instruction instruction)
    {
        InstructionOperandToILLabel(instruction);
        Instructions.Insert(Instructions.IndexOf(target) + 1, instruction);
        return this;
    }

    /// <summary>
    /// Insert instructions before the provided index, stealing any labels.
    /// </summary>
    /// <remarks>
    /// Stealing labels means that if the instruction at the provided instruction
    /// or index has incoming <see cref="ILLabel"/>s or is inside the start of
    /// a try, filter, catch, finally, or fault range, then the first inserted
    /// instruction will become the new start of that range or label.
    /// The same applies to the ends of these handler ranges because they are
    /// exclusive, meaning that the inclusive end of a range is before the
    /// instruction marked as the end.
    /// </remarks>
    public ILWeaver InsertBeforeStealLabels(int index, params IEnumerable<Instruction> instructions)
    {
        var noNull = instructions.Where(x => x is { });
        var first = noNull.FirstOrDefault();
        if (first is null)
            return this;

        InsertAtInternal(ref index, first, InsertType.BeforeAndStealLabels);
        index++;

        foreach (var instruction in noNull.Skip(1))
        {
            InsertAtInternal(ref index, instruction, InsertType.Before);
            index++;
        }

        return this;
    }

    /// <inheritdoc cref="InsertBeforeStealLabels(int, IEnumerable{Instruction})"/>
    public ILWeaver InsertBeforeStealLabels(
        int index,
        params IEnumerable<InstructionOrEnumerable> instructions
    ) => InsertBeforeStealLabels(index, instructions.Unwrap());

    /// <summary>
    /// Insert instructions before the provided instruction, stealing any labels.
    /// </summary>
    /// <inheritdoc cref="InsertBeforeStealLabels(int, IEnumerable{Instruction})"/>
    public ILWeaver InsertBeforeStealLabels(
        Instruction target,
        params IEnumerable<Instruction> instructions
    ) => InsertBeforeStealLabels(Instructions.IndexOf(target), instructions);

    /// <inheritdoc cref="InsertBeforeStealLabels(Instruction, IEnumerable{Instruction})"/>
    public ILWeaver InsertBeforeStealLabels(
        Instruction target,
        params IEnumerable<InstructionOrEnumerable> instructions
    ) => InsertBeforeStealLabels(target, instructions.Unwrap());

    /// <summary>
    /// Insert instructions before this weaver's current position, stealing any labels.
    /// Current target doesn't change.
    /// </summary>
    /// <inheritdoc cref="InsertBeforeStealLabels(int, IEnumerable{Instruction})"/>
    public ILWeaver InsertBeforeCurrentStealLabels(params IEnumerable<Instruction> instructions) =>
        InsertBeforeStealLabels(Index, instructions);

    /// <inheritdoc cref="InsertBeforeCurrentStealLabels(IEnumerable{Instruction})"/>
    public ILWeaver InsertBeforeCurrentStealLabels(
        params IEnumerable<InstructionOrEnumerable> instructions
    ) => InsertBeforeCurrentStealLabels(instructions.Unwrap());

    /// <summary>
    /// Insert instructions before the provided index.
    /// </summary>
    public ILWeaver InsertBefore(int index, params IEnumerable<Instruction> instructions)
    {
        foreach (var instruction in instructions.Where(x => x is { }))
        {
            InsertAtInternal(ref index, instruction, InsertType.Before);
            index++;
        }

        return this;
    }

    /// <inheritdoc cref="InsertBefore(int, IEnumerable{Instruction})"/>
    public ILWeaver InsertBefore(
        int index,
        params IEnumerable<InstructionOrEnumerable> instructions
    ) => InsertBefore(index, instructions.Unwrap());

    /// <summary>
    /// Insert instructions before the provided instruction.
    /// </summary>
    public ILWeaver InsertBefore(
        Instruction target,
        params IEnumerable<Instruction> instructions
    ) => InsertBefore(Instructions.IndexOf(target), instructions);

    /// <inheritdoc cref="InsertBefore(Instruction, IEnumerable{Instruction})"/>
    public ILWeaver InsertBefore(
        Instruction target,
        params IEnumerable<InstructionOrEnumerable> instructions
    ) => InsertBefore(target, instructions.Unwrap());

    /// <summary>
    /// Insert instructions before this weaver's current position.
    /// </summary>
    public ILWeaver InsertBeforeCurrent(params IEnumerable<Instruction> instructions) =>
        InsertBefore(Index, instructions);

    /// <inheritdoc cref="InsertBeforeCurrent(IEnumerable{Instruction})"/>
    public ILWeaver InsertBeforeCurrent(params IEnumerable<InstructionOrEnumerable> instructions) =>
        InsertBeforeCurrent(instructions.Unwrap());

    /// <summary>
    /// Insert instructions after the provided index.
    /// </summary>
    public ILWeaver InsertAfter(int index, params IEnumerable<Instruction> instructions)
    {
        foreach (var instruction in instructions.Where(x => x is { }))
        {
            InsertAtInternal(ref index, instruction, InsertType.After);
            index++;
        }

        return this;
    }

    /// <inheritdoc cref="InsertAfter(int, IEnumerable{Instruction})"/>
    public ILWeaver InsertAfter(
        int index,
        params IEnumerable<InstructionOrEnumerable> instructions
    ) => InsertAfter(index, instructions.Unwrap());

    /// <summary>
    /// Insert instructions after the provided instruction.
    /// </summary>
    public ILWeaver InsertAfter(Instruction target, params IEnumerable<Instruction> instructions) =>
        InsertAfter(Instructions.IndexOf(target), instructions);

    /// <inheritdoc cref="InsertAfter(Instruction, IEnumerable{Instruction})"/>
    public ILWeaver InsertAfter(
        Instruction target,
        params IEnumerable<InstructionOrEnumerable> instructions
    ) => InsertAfter(target, instructions.Unwrap());

    /// <summary>
    /// Insert instructions after this weaver's current position.
    /// Retargets Current to the last inserted instruction.
    /// </summary>
    public ILWeaver InsertAfterCurrent(params IEnumerable<Instruction> instructions)
    {
        int index = Index;
        foreach (var instruction in instructions.Where(x => x is { }))
        {
            InsertAtInternal(ref index, instruction, InsertType.After);
            CurrentTo(instruction);
            index++;
        }

        return this;
    }

    /// <inheritdoc cref="InsertAfterCurrent(IEnumerable{Instruction})"/>
    public ILWeaver InsertAfterCurrent(params IEnumerable<InstructionOrEnumerable> instructions) =>
        InsertAfterCurrent(instructions.Unwrap());

    private ILWeaver InsertBranchOverIfX(
        Instruction start,
        Instruction end,
        OpCode opCode,
        params IEnumerable<Instruction> condition
    )
    {
        var startIndex = Instructions.IndexOf(start);
        InsertBeforeStealLabels(startIndex, Create(opCode, end.Next));
        InsertBeforeStealLabels(startIndex, condition);
        return this;
    }

    /// <summary>
    /// Inserts a <c>brtrue</c> instruction before <paramref name="start"/> instruction
    /// which, if the provided <paramref name="condition"/> returns true
    /// (non-zero on the stack),
    /// branches <b>OVER</b> the <paramref name="end"/> instruction,
    /// thus the instruction after "<paramref name="end"/>" is branched to.
    /// </summary>
    /// <param name="condition">A list of instructions which end up leaving one (1)
    /// value on the stack which represents either true (non-zero value, e.g. <c>ldc.i4.1</c>)
    /// or false (zero e.g. <c>ldc.i4.0</c>) which will be evaluated by <c>brtrue</c>.</param>
    /// <inheritdoc cref="InsertBranchOver(Instruction, Instruction)"/>
    /// <param name="start"/>
    /// <param name="end"/>
    public ILWeaver InsertBranchOverIfTrue(
        Instruction start,
        Instruction end,
        params IEnumerable<Instruction> condition
    ) => InsertBranchOverIfX(start, end, OpCodes.Brtrue, condition);

    /// <inheritdoc cref="InsertBranchOverIfTrue(Instruction, Instruction, IEnumerable{Instruction})"/>
    public ILWeaver InsertBranchOverIfTrue(
        Instruction start,
        Instruction end,
        params IEnumerable<InstructionOrEnumerable> condition
    ) => InsertBranchOverIfTrue(start, end, condition.Unwrap());

    /// <param name="range">A tuple of the first and last instructions to branch over.</param>
    /// <inheritdoc cref="InsertBranchOverIfTrue(Instruction, Instruction, IEnumerable{Instruction})"/>
    /// <param name="condition"/>
    public ILWeaver InsertBranchOverIfTrue(
        (Instruction start, Instruction end) range,
        params IEnumerable<Instruction> condition
    ) => InsertBranchOverIfTrue(range.start, range.end, condition);

    /// <inheritdoc cref="InsertBranchOverIfTrue(ValueTuple{Instruction, Instruction}, IEnumerable{Instruction})"/>
    public ILWeaver InsertBranchOverIfTrue(
        (Instruction start, Instruction end) range,
        params IEnumerable<InstructionOrEnumerable> condition
    ) => InsertBranchOverIfTrue(range, condition.Unwrap());

    /// <summary>
    /// Inserts a <c>brfalse</c> instruction before <paramref name="start"/> instruction
    /// which, if the provided <paramref name="condition"/> returns false
    /// (zero on the stack),
    /// branches <b>OVER</b> the <paramref name="end"/> instruction,
    /// thus the instruction after "<paramref name="end"/>" is branched to.
    /// </summary>
    /// <param name="condition">A list of instructions which end up leaving one (1)
    /// value on the stack which represents either true (non-zero value, e.g. <c>ldc.i4.1</c>)
    /// or false (zero e.g. <c>ldc.i4.0</c>) which will be evaluated by <c>brfalse</c>.</param>
    /// <inheritdoc cref="InsertBranchOver(Instruction, Instruction)"/>
    /// <param name="start"/>
    /// <param name="end"/>
    public ILWeaver InsertBranchOverIfFalse(
        Instruction start,
        Instruction end,
        params IEnumerable<Instruction> condition
    ) => InsertBranchOverIfX(start, end, OpCodes.Brfalse, condition);

    /// <inheritdoc cref="InsertBranchOverIfFalse(Instruction, Instruction, IEnumerable{Instruction})"/>
    public ILWeaver InsertBranchOverIfFalse(
        Instruction start,
        Instruction end,
        params IEnumerable<InstructionOrEnumerable> condition
    ) => InsertBranchOverIfFalse(start, end, condition.Unwrap());

    /// <param name="range">A tuple of the first and last instructions to branch over.</param>
    /// <inheritdoc cref="InsertBranchOverIfFalse(Instruction, Instruction, IEnumerable{Instruction})"/>
    /// <param name="condition"/>
    public ILWeaver InsertBranchOverIfFalse(
        (Instruction start, Instruction end) range,
        params IEnumerable<Instruction> condition
    ) => InsertBranchOverIfFalse(range.start, range.end, condition);

    /// <inheritdoc cref="InsertBranchOverIfFalse(ValueTuple{Instruction, Instruction}, IEnumerable{Instruction})"/>
    public ILWeaver InsertBranchOverIfFalse(
        (Instruction start, Instruction end) range,
        params IEnumerable<InstructionOrEnumerable> condition
    ) => InsertBranchOverIfFalse(range, condition.Unwrap());

    /// <summary>
    /// Inserts a <c>br</c> instruction before <paramref name="start"/> instruction
    /// which unconditionally branches <b>OVER</b> the <paramref name="end"/> instruction,
    /// thus the instruction after "<paramref name="end"/>" is branched to.
    /// </summary>
    /// <remarks>
    /// If control flow would fall or branch to <paramref name="start"/> instruction,
    /// the inserted instruction runs. However if the <paramref name="start"/> instruction
    /// is skipped in control flow, the inserted instruction is also skipped. This means
    /// instructions after <paramref name="start"/> and up to <paramref name="end"/> are possible
    /// to execute if they are directly branched to.
    /// </remarks>
    /// <param name="start">The first instruction to branch over.</param>
    /// <param name="end">The last instruction to branch over.</param>
    /// <returns>This <see cref="ILWeaver"/>.</returns>
    public ILWeaver InsertBranchOver(Instruction start, Instruction end)
    {
        InsertBeforeStealLabels(start, Create(OpCodes.Br, end.Next));
        return this;
    }

    /// <param name="range">A tuple of the first and last instructions to branch over.</param>
    /// <inheritdoc cref="InsertBranchOver(Instruction, Instruction)"/>
    public ILWeaver InsertBranchOver((Instruction start, Instruction end) range) =>
        InsertBranchOver(range.start, range.end);

    /// <summary>
    /// Store an object in the reference store, and emit the IL to retrieve it and place it on the stack.
    /// </summary>
    public ILWeaver EmitReferenceBefore<T>(Instruction target, in T? value, out int id)
    {
        id = InteropILCursor.InteropEmitReferenceBefore(Context, target, value);
        return this;
    }

    /// <inheritdoc cref="EmitReferenceBefore{T}(Instruction, in T, out int)"/>
    public ILWeaver EmitReferenceBeforeCurrent<T>(in T? value, out int id) =>
        EmitReferenceBefore(Current, value, out id);

#pragma warning disable CS1572 // XML comment has a param tag, but there is no parameter by that name
    /// <summary>
    /// Create a new instruction to be emitted by
    /// <see cref="InsertBeforeCurrent(IEnumerable{Instruction})"/> or any of the variations.
    /// </summary>
    /// <param name="opcode">The instruction opcode.</param>
    /// <param name="parameter">The instruction operand.</param>
    /// <param name="variable">The instruction operand.</param>
    /// <param name="targets">The instruction operand.</param>
    /// <param name="target">The instruction operand.</param>
    /// <param name="value">The instruction operand.</param>
    /// <param name="field">The instruction operand.</param>
    /// <param name="site">The instruction operand.</param>
    /// <param name="type">The instruction operand.</param>
    /// <param name="method">The instruction operand.</param>
    /// <param name="operand">The instruction operand.</param>
    /// <returns>The created instruction.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentException"></exception>
    public Instruction Create(OpCode opcode, ParameterDefinition parameter) =>
        IL.Create(opcode, parameter);
#pragma warning restore CS1572 // XML comment has a param tag, but there is no parameter by that name

    /// <inheritdoc cref="Create(OpCode, ParameterDefinition)"/>
    public Instruction Create(OpCode opcode, VariableDefinition variable) =>
        IL.Create(opcode, variable);

    /// <inheritdoc cref="Create(OpCode, ParameterDefinition)"/>
    public Instruction Create(OpCode opcode, Instruction[] targets)
    {
        // Note: We want our created instructions to contain ILLabel[]
        // instead of Instruction[] as MonoMod's matching extensions expect this.

        // However, if we were to do so here, GetIncomingLabels can find the labels
        // even before the instructions have been inserted into the method.
        // In certain cases, this is problematic when InsertBeforeStealLabels is used.

        // The test: ./tests/MonoDetour.UnitTests/ILWeaverTests/StealLabelsTests.cs
        // should catch this and shows in which context it can happen.
        return IL.Create(opcode, targets);
    }

    /// <inheritdoc cref="Create(OpCode, ParameterDefinition)"/>
    public Instruction Create(OpCode opcode, Instruction target)
    {
        // See comment for Instruction Create(OpCode opcode, Instruction[] targets)
        return IL.Create(opcode, target);
    }

    /// <inheritdoc cref="Create(OpCode, ParameterDefinition)"/>
    public Instruction Create(OpCode opcode, double value) => IL.Create(opcode, value);

    /// <inheritdoc cref="Create(OpCode, ParameterDefinition)"/>
    public Instruction Create(OpCode opcode, float value) => IL.Create(opcode, value);

    /// <inheritdoc cref="Create(OpCode, ParameterDefinition)"/>
    public Instruction Create(OpCode opcode, long value) => IL.Create(opcode, value);

    /// <inheritdoc cref="Create(OpCode, ParameterDefinition)"/>
    public Instruction Create(OpCode opcode, sbyte value) => IL.Create(opcode, value);

    /// <inheritdoc cref="Create(OpCode, ParameterDefinition)"/>
    public Instruction Create(OpCode opcode, byte value) => IL.Create(opcode, value);

    /// <inheritdoc cref="Create(OpCode, ParameterDefinition)"/>
    public Instruction Create(OpCode opcode, string value) => IL.Create(opcode, value);

    /// <inheritdoc cref="Create(OpCode, ParameterDefinition)"/>
    public Instruction Create(OpCode opcode, FieldReference field) => IL.Create(opcode, field);

    /// <inheritdoc cref="Create(OpCode, ParameterDefinition)"/>
    public Instruction Create(OpCode opcode, CallSite site) => IL.Create(opcode, site);

    /// <inheritdoc cref="Create(OpCode, ParameterDefinition)"/>
    public Instruction Create(OpCode opcode, TypeReference type) => IL.Create(opcode, type);

    /// <inheritdoc cref="Create(OpCode, ParameterDefinition)"/>
    public Instruction Create(OpCode opcode) => IL.Create(opcode);

    /// <inheritdoc cref="Create(OpCode, ParameterDefinition)"/>
    public Instruction Create(OpCode opcode, int value) => IL.Create(opcode, value);

    /// <inheritdoc cref="Create(OpCode, ParameterDefinition)"/>
    public Instruction Create(OpCode opcode, MethodReference method) => IL.Create(opcode, method);

    /// <inheritdoc cref="Create(OpCode, ParameterDefinition)"/>
    public Instruction Create(OpCode opcode, FieldInfo field) => IL.Create(opcode, field);

    /// <inheritdoc cref="Create(OpCode, ParameterDefinition)"/>
    public Instruction Create(OpCode opcode, MethodBase method) => IL.Create(opcode, method);

    /// <inheritdoc cref="Create(OpCode, ParameterDefinition)"/>
    public Instruction Create(OpCode opcode, Type type) => IL.Create(opcode, type);

    /// <inheritdoc cref="Create(OpCode, ParameterDefinition)"/>
    public Instruction Create(OpCode opcode, object operand) => IL.Create(opcode, operand);

    /// <remarks>
    /// If the delegate method isn't static, its instance must be pushed to the stack first.<br/>
    /// The delegate method must not be a lambda expression, as one requires an anonymous
    /// instance to be loaded. If it is a lambda expression, use <see cref="CreateDelegateCall"/>
    /// instead.
    /// </remarks>
    /// <inheritdoc cref="Create(OpCode, ParameterDefinition)"/>
    public Instruction CreateCall(Delegate method) => IL.Create(OpCodes.Call, method.Method);

    /// <summary>
    /// Creates instructions to invoke a <see cref="Delegate"/> as if it were a method.
    /// Stack behaviour matches the <c>call</c> OpCode.<br/>
    /// <br/>
    /// Normally a Delegate would need to be invoked by pushing the Delegate instance as
    /// the first argument to the stack, after which every other argument is pushed.
    /// Then, the Delegate is invoked with a <c>callvirt</c> instruction.<br/>
    /// <br/>
    /// With this method, the Delegate instance is automatically stored and loaded for you,
    /// and you can simply push all arguments to the stack and then call this method to create
    /// the required instructions to invoke the Delegate.
    /// </summary>
    /// <param name="delegate">The <see cref="Delegate"/> method to be invoked.</param>
    /// <returns>
    /// An array of <see cref="Instruction"/>, containing all the
    /// instructions required to invoke the Delegate.
    /// </returns>
    public Instruction[] CreateDelegateCall<T>(T @delegate)
        where T : Delegate
    {
        Helpers.ThrowIfNull(@delegate);

        if (@delegate.GetInvocationList().Length == 1 && @delegate.Target == null)
        {
            return [Create(OpCodes.Call, @delegate.Method)];
        }

        List<Instruction> instrs = [];

        var invoker = InteropFastDelegateInvokers.GetDelegateInvoker(Context, @delegate.GetType());
        int id;

        if (invoker is { } pair)
        {
            if (MonoModVersion.IsReorg)
            {
                Delegate cast = @delegate.CastDelegate(pair.Delegate);
                id = InteropILContext.InteropAddReference(Context, cast);
                instrs.AddRange(InteropILContext.InteropGetReference(Context, this, id, cast));
            }
            else
            {
                // Yes, we really need the direct delegate reference for Legacy to work properly.
                id = InteropILContext.InteropAddReference(Context, @delegate);
                instrs.AddRange(InteropILContext.InteropGetReference(Context, this, id, @delegate));
            }

            // Prevent the invoker from getting GC'd early, f.e. when it's a DynamicMethod.
            InteropILContext.InteropAddReference(Context, pair.Invoker);
            instrs.Add(Create(OpCodes.Call, pair.Invoker));
        }
        else
        {
            id = InteropILContext.InteropAddReference(Context, @delegate);
            instrs.AddRange(InteropILContext.InteropGetReference(Context, this, id, @delegate));

            var delInvoke = typeof(T).GetMethod("Invoke")!;
            instrs.Add(Create(OpCodes.Callvirt, delInvoke));
        }

        return [.. instrs];
    }

    /// <summary>
    /// Create a new instruction accessing a given member, to be emitted by
    /// <see cref="InsertBeforeCurrent(IEnumerable{Instruction})"/> or any of the variations.
    /// </summary>
    /// <typeparam name="T">The type in which the member is defined.</typeparam>
    /// <param name="memberName">The accessed member name.</param>
    /// <inheritdoc cref="Create(OpCode, ParameterDefinition)"/>
    /// <exception cref="NotSupportedException"></exception>
    /// <param name="opcode"/>
    public Instruction Create<T>(OpCode opcode, string memberName) =>
        IL.Create(opcode, typeof(T).GetMember(memberName, (BindingFlags)(-1)).First());
}
