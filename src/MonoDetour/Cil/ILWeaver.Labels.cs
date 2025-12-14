using System.Collections.Generic;
using Mono.Cecil.Cil;
using MonoDetour.Interop.MonoModUtils;
using MonoMod.Cil;

namespace MonoDetour.Cil;

public partial class ILWeaver
{
    /// <summary>
    /// Gets all branching labels pointing to the provided instruction.
    /// </summary>
    /// <param name="target">The target instruction for the labels.</param>
    public IEnumerable<ILLabel> GetIncomingLabelsFor(Instruction target) =>
        Context.GetIncomingLabels(target);

    /// <summary>
    /// Gets all branching labels pointing to <see cref="Current"/>.
    /// </summary>
    public IEnumerable<ILLabel> GetIncomingLabelsForCurrent() => Context.GetIncomingLabels(Current);

    /// <summary>
    /// Retargets ILLabels to a target instruction.
    /// </summary>
    /// <param name="labels">The labels to retarget.</param>
    /// <param name="target">The new target instruction for labels.</param>
    /// <returns>This <see cref="ILWeaver"/></returns>
    public ILWeaver RetargetLabels(IEnumerable<ILLabel> labels, Instruction target)
    {
        foreach (var label in labels)
            label.InteropSetTarget(target);

        return this;
    }

    /// <summary>
    /// Retargets ILLabels targeting <paramref name="source"/> instruction
    /// to <paramref name="target"/> instruction.
    /// </summary>
    /// <param name="source">The instruction whose labels to retarget.</param>
    /// <inheritdoc cref="RetargetLabels(IEnumerable{ILLabel}, Instruction)"/>
    /// <param name="target"></param>
    public ILWeaver RetargetLabels(Instruction source, Instruction target) =>
        RetargetLabels(GetIncomingLabelsFor(source), target);

    /// <param name="label">The label to retarget.</param>
    /// <inheritdoc cref="RetargetLabels(IEnumerable{ILLabel}, Instruction)"/>
    /// <param name="target"></param>
    public ILWeaver RetargetLabels(ILLabel? label, Instruction target)
    {
        label?.InteropSetTarget(target);
        return this;
    }

    /// <summary>
    /// Defines a new <see cref="ILLabel"/> to be targeted.
    /// </summary>
    /// <returns>The new <see cref="ILLabel"/>.</returns>
    public ILLabel DefineLabel() => Context.DefineLabel();

    /// <summary>
    /// Defines a new <see cref="ILLabel"/> to be targeted.
    /// </summary>
    /// <param name="label">The new label.</param>
    /// <returns>This <see cref="ILWeaver"/>.</returns>
    public ILWeaver DefineLabel(out ILLabel label)
    {
        label = Context.DefineLabel();
        return this;
    }

    /// <summary>
    /// Sets the target of a label to the provided <see cref="Instruction"/>.
    /// </summary>
    /// <param name="target">The target for the label.</param>
    /// <param name="label">The label to mark.</param>
    /// <returns>This <see cref="ILWeaver"/>.</returns>
    public ILWeaver MarkLabelTo(Instruction target, ILLabel label)
    {
        Helpers.ThrowIfNull(target);
        label.InteropSetTarget(target);
        return this;
    }

    /// <summary>
    /// Creates a new label targetting the provided <see cref="Instruction"/>.
    /// </summary>
    /// <param name="target">The target for the label.</param>
    /// <param name="markedLabel">The marked label.</param>
    /// <returns>This <see cref="ILWeaver"/>.</returns>
    public ILWeaver DefineAndMarkLabelTo(Instruction target, out ILLabel markedLabel)
    {
        Helpers.ThrowIfNull(target);
        markedLabel = Context.DefineLabel(target);
        return this;
    }

    /// <returns>The new <see cref="ILLabel"/>.</returns>
    ///<inheritdoc cref="DefineAndMarkLabelTo(Instruction, out ILLabel)"/>
    public ILLabel DefineAndMarkLabelTo(Instruction target)
    {
        DefineAndMarkLabelTo(target, out var markedLabel);
        return markedLabel;
    }

    /// <summary>
    /// Sets the target of a label to the future next inserted instruction.
    /// </summary>
    /// <param name="label">The label to mark.</param>
    /// <returns>This <see cref="ILWeaver"/>.</returns>
    public ILWeaver MarkLabelToFutureNextInsert(ILLabel label)
    {
        Helpers.ThrowIfNull(label);
        pendingFutureNextInsertLabels.Add(label);
        return this;
    }

    /// <summary>
    /// Creates a new label targetting the future next inserted instruction.
    /// </summary>
    /// <param name="futureMarkedLabel">The marked label.</param>
    /// <returns>This <see cref="ILWeaver"/>.</returns>
    public ILWeaver DefineAndMarkLabelToFutureNextInsert(out ILLabel futureMarkedLabel)
    {
        futureMarkedLabel = Context.DefineLabel();
        pendingFutureNextInsertLabels.Add(futureMarkedLabel);
        return this;
    }

    /// <returns>The new <see cref="ILLabel"/>.</returns>
    ///<inheritdoc cref="DefineAndMarkLabelToFutureNextInsert(out ILLabel)"/>
    public ILLabel DefineAndMarkLabelToFutureNextInsert()
    {
        DefineAndMarkLabelToFutureNextInsert(out var futureMarkedLabel);
        return futureMarkedLabel;
    }

    /// <summary>
    /// Sets the target of a label to the future next inserted instruction.<br/>
    /// Targets <see cref="Current"/> as a placeholder.
    /// </summary>
    /// <remarks>
    /// Prefer <see cref="MarkLabelToFutureNextInsert(ILLabel)"/> if the label
    /// will always be redirected to an inserted instruction. Using this
    /// method will then show that there branches where a next instruction isn't inserted.
    /// </remarks>
    /// <param name="label">The label to mark.</param>
    /// <returns>This <see cref="ILWeaver"/>.</returns>
    public ILWeaver MarkLabelToCurrentOrFutureNextInsert(ILLabel label)
    {
        Helpers.ThrowIfNull(label);
        label.InteropSetTarget(Current);
        pendingFutureNextInsertLabels.Add(label);
        return this;
    }

    /// <summary>
    /// Creates a new label targetting the future next inserted instruction.<br/>
    /// Targets <see cref="Current"/> as a placeholder.
    /// </summary>
    /// <remarks>
    /// Prefer <see cref="DefineAndMarkLabelToFutureNextInsert(out ILLabel)"/> if the label
    /// will always be redirected to an inserted instruction. Using this
    /// method will then show that there branches where a next instruction isn't inserted.
    /// </remarks>
    /// <param name="futureMarkedLabel">The marked label.</param>
    /// <returns>This <see cref="ILWeaver"/>.</returns>
    public ILWeaver DefineAndMarkLabelToCurrentOrFutureNextInsert(out ILLabel futureMarkedLabel)
    {
        futureMarkedLabel = Context.DefineLabel(Current);
        pendingFutureNextInsertLabels.Add(futureMarkedLabel);
        return this;
    }

    /// <returns>The new <see cref="ILLabel"/>.</returns>
    ///<inheritdoc cref="DefineAndMarkLabelToCurrentOrFutureNextInsert(out ILLabel)"/>
    public ILLabel DefineAndMarkLabelToCurrentOrFutureNextInsert()
    {
        DefineAndMarkLabelToCurrentOrFutureNextInsert(out var futureMarkedLabel);
        return futureMarkedLabel;
    }

    /// <summary>
    /// Sets the target of a label to <see cref="Current"/>.
    /// </summary>
    /// <param name="label">The label to mark.</param>
    /// <returns>This <see cref="ILWeaver"/>.</returns>
    public ILWeaver MarkLabelToCurrent(ILLabel label)
    {
        Helpers.ThrowIfNull(label);
        label.InteropSetTarget(Current);
        return this;
    }

    /// <summary>
    /// Creates a new label targetting <see cref="Current"/>.
    /// </summary>
    /// <param name="markedLabel">The marked label.</param>
    /// <returns>This <see cref="ILWeaver"/>.</returns>
    public ILWeaver DefineAndMarkLabelToCurrent(out ILLabel markedLabel)
    {
        markedLabel = Context.DefineLabel(Current);
        return this;
    }

    /// <returns>The new <see cref="ILLabel"/>.</returns>
    ///<inheritdoc cref="DefineAndMarkLabelToCurrent(out ILLabel)"/>
    public ILLabel DefineAndMarkLabelToCurrent()
    {
        DefineAndMarkLabelToCurrent(out var markedLabel);
        return markedLabel;
    }

    /// <summary>
    /// Sets the target of a label to <see cref="Current"/>'s <see cref="Instruction.Previous"/>.
    /// </summary>
    /// <remarks>
    /// The label will point to the Previous instruction at the moment of calling this method.
    /// </remarks>
    /// <param name="label">The label to mark.</param>
    /// <returns>This <see cref="ILWeaver"/>.</returns>
    public ILWeaver MarkLabelToCurrentPrevious(ILLabel label)
    {
        Helpers.ThrowIfNull(label);
        label.InteropSetTarget(Current.Previous);
        return this;
    }

    /// <summary>
    /// Creates a new label targetting <see cref="Current"/>'s <see cref="Instruction.Previous"/>.
    /// </summary>
    /// <param name="markedLabel">The marked label.</param>
    /// <inheritdoc cref="MarkLabelToCurrentPrevious(ILLabel)"/>
    public ILWeaver DefineAndMarkLabelToCurrentPrevious(out ILLabel markedLabel)
    {
        markedLabel = Context.DefineLabel(Current.Previous);
        return this;
    }

    /// <returns>The new <see cref="ILLabel"/>.</returns>
    ///<inheritdoc cref="DefineAndMarkLabelToCurrentPrevious(out ILLabel)"/>
    public ILLabel DefineAndMarkLabelToCurrentPrevious()
    {
        DefineAndMarkLabelToCurrentPrevious(out var markedLabel);
        return markedLabel;
    }

    /// <summary>
    /// Sets the target of a label to <see cref="Current"/>'s <see cref="Instruction.Next"/>.
    /// </summary>
    /// <remarks>
    /// The label will point to the Next instruction at the moment of calling this method.
    /// </remarks>
    /// <param name="label">The label to mark.</param>
    /// <returns>This <see cref="ILWeaver"/>.</returns>
    public ILWeaver MarkLabelToCurrentNext(ILLabel label)
    {
        Helpers.ThrowIfNull(label);
        label.InteropSetTarget(Current.Next);
        return this;
    }

    /// <summary>
    /// Creates a new label targetting <see cref="Current"/>'s <see cref="Instruction.Next"/>.
    /// </summary>
    /// <param name="markedLabel">The marked label.</param>
    /// <inheritdoc cref="MarkLabelToCurrentNext(ILLabel)"/>
    public ILWeaver DefineAndMarkLabelToCurrentNext(out ILLabel markedLabel)
    {
        markedLabel = Context.DefineLabel(Current.Next);
        return this;
    }

    /// <returns>The new <see cref="ILLabel"/>.</returns>
    ///<inheritdoc cref="DefineAndMarkLabelToCurrentNext(out ILLabel)"/>
    public ILLabel DefineAndMarkLabelToCurrentNext()
    {
        DefineAndMarkLabelToCurrentNext(out var markedLabel);
        return markedLabel;
    }
}
