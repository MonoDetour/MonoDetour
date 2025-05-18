using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoDetour.DetourTypes;
using MonoDetour.Interop.MonoModUtils;
using MonoMod.Cil;
using MonoMod.SourceGen.Internal;
using MonoMod.Utils;
using InstrList = Mono.Collections.Generic.Collection<Mono.Cecil.Cil.Instruction>;
using MethodBody = Mono.Cecil.Cil.MethodBody;

namespace MonoDetour;

// TODO: fix all warnings.
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable CS1573 // Parameter has no matching param tag in the XML comment (but other parameters do)

public class ILWeaver(ILManipulationInfo il)
{
    /// <inheritdoc cref="ILManipulationInfo"/>
    public ILManipulationInfo ManipulationInfo { get; } = il;

    /// <inheritdoc cref="ILContext"/>
    public ILContext Context { get; } = il.ManipulationContext;

    /// <inheritdoc cref="ILContext.IL"/>
    public ILProcessor IL => Context.IL;

    /// <inheritdoc cref="ILContext.Body"/>
    public MethodBody Body => Context.Body;

    /// <inheritdoc cref="ILContext.Instrs"/>
    public InstrList Instructions => Context.Instrs;

    /// <summary>
    /// The instruction this weaver currently points to.
    /// </summary>
    /// <remarks>
    /// Setter is <see cref="CurrentTo(Instruction)"/>.<br/>
    /// For replacing the current instruction,
    /// see <see cref="ReplaceCurrent(Instruction)"/>
    /// </remarks>
    public Instruction Current
    {
        get => current;
        set => CurrentTo(value);
    }

    /// <summary>
    /// Gets the first instruction in the method body.
    /// </summary>
    public Instruction First => Instructions[0];

    /// <summary>
    /// Gets the last instruction in the method body.
    /// </summary>
    public Instruction Last => Instructions[^1];

    /// <summary>
    /// The index of the instruction on <see cref="Current"/>
    /// </summary>
    /// <remarks>
    /// A negative index will loop back.
    /// Setter uses <see cref="CurrentTo(int)"/> which can throw.
    /// </remarks>
    /// <exception cref="IndexOutOfRangeException"></exception>
    public int Index
    {
        get => Context.IndexOf(Current);
        set => CurrentTo(value);
    }

    Instruction current = il.ManipulationContext.Instrs[0];

    readonly List<ILLabel> pendingFutureNextInsertLabels = [];

    const string gotoMatchingDocsLink = "<insert documentation link here>";

    /// <summary>
    /// Create a new <see cref="ILWeaver"/> for the current <see cref="ILManipulationInfo"/>
    /// with state copied optionally.
    /// </summary>
    /// <returns>A new <see cref="ILWeaver"/> or a copy with state.</returns>
    public ILWeaver(ILWeaver weaver, bool copyState = true)
        : this(weaver.ManipulationInfo)
    {
        if (copyState == false)
            return;

        Current = weaver.Current;
    }

    /// <summary>
    /// Create a new <see cref="ILWeaver"/> for the current <see cref="ILManipulationInfo"/>
    /// using the <see cref="ILWeaver(ILWeaver, bool)"/> constructor.<br/>
    /// Does not copy state.
    /// </summary>
    /// <returns>A new <see cref="ILWeaver"/> for the current <see cref="ILManipulationInfo"/>.</returns>
    public ILWeaver New() => new(this, copyState: false);

    /// <summary>
    /// Create a clone of the <see cref="ILWeaver"/>
    /// using the <see cref="ILWeaver(ILWeaver, bool)"/> constructor.<br/>
    /// State is copied.
    /// </summary>
    /// <returns>A clone of the <see cref="ILWeaver"/>.</returns>
    public ILWeaver Clone() => new(this, copyState: true);

    /// <summary>
    /// Gets all branching labels pointing to <see cref="Current"/>.
    /// </summary>
    public IEnumerable<ILLabel> GetIncomingLabelsForCurrent() => Context.GetIncomingLabels(Current);

    /// <summary>
    /// Gets all branching labels pointing to the provided instruction.
    /// </summary>
    /// <param name="target">The target instruction for the labels.</param>
    public IEnumerable<ILLabel> GetIncomingLabelsFor(Instruction target) =>
        Context.GetIncomingLabels(target);

    /// <summary>
    /// Retargets ILLabels to a target instruction.
    /// </summary>
    /// <param name="labels">The labels to retarget.</param>
    /// <param name="target">The new target instruction for labels.</param>
    /// <returns>this <see cref="ILWeaver"/></returns>
    public ILWeaver RetargetLabels(IEnumerable<ILLabel> labels, Instruction target)
    {
        foreach (var label in labels)
            label.InteropSetTarget(target);

        return this;
    }

    /// <param name="label">The label to retarget.</param>
    /// <param name="target">The new target instruction for the label.</param>
    /// <inheritdoc cref="RetargetLabels(IEnumerable{ILLabel}, Instruction)"/>
    public ILWeaver RetargetLabels(ILLabel? label, Instruction target)
    {
        if (label is not null)
        {
            label.InteropSetTarget(target);
        }

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
        label = Context.DefineLabel(target);
        return this;
    }

    /// <summary>
    /// Creates a new label targetting the provided <see cref="Instruction"/>.
    /// </summary>
    /// <param name="markedLabel">The marked label.</param>
    /// <returns>This <see cref="ILWeaver"/>.</returns>
    public ILWeaver MarkLabelTo(Instruction target, out ILLabel markedLabel)
    {
        Helpers.ThrowIfNull(target);
        markedLabel = Context.DefineLabel(target);
        return this;
    }

    /// <summary>
    /// Creates a new label targetting the future next inserted instruction.
    /// </summary>
    /// <param name="futureMarkedLabel">The marked label.</param>
    /// <returns>This <see cref="ILWeaver"/>.</returns>
    public ILWeaver MarkLabelToFutureNextInsert(out ILLabel futureMarkedLabel)
    {
        futureMarkedLabel = Context.DefineLabel();
        pendingFutureNextInsertLabels.Add(futureMarkedLabel);
        return this;
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
    public ILWeaver MarkLabelToCurrent(out ILLabel markedLabel)
    {
        markedLabel = Context.DefineLabel(Current);
        return this;
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
    public ILWeaver MarkLabelToCurrentPrevious(out ILLabel markedLabel)
    {
        markedLabel = Context.DefineLabel(Current.Previous);
        return this;
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
    public ILWeaver MarkLabelToCurrentNext(out ILLabel markedLabel)
    {
        markedLabel = Context.DefineLabel(Current.Next);
        return this;
    }

    public ILWeaver Replace(Instruction target, Instruction replacement)
    {
        InsertAfter(target, replacement);
        Remove(target, out var label);
        RetargetLabels(label, replacement);
        return this;
    }

    /// <summary>
    /// Replaces the instruction at <see cref="Current"/>.
    /// </summary>
    /// <param name="replacement"></param>
    /// <returns>this <see cref="ILWeaver"/></returns>
    public ILWeaver ReplaceCurrent(Instruction replacement)
    {
        Replace(Current, replacement);
        return this;
    }

    public ILWeaver RemoveAt(int index, int instructions, out IEnumerable<ILLabel> orphanedLabels)
    {
        if (index + instructions < Instructions.Count)
            throw new IndexOutOfRangeException(
                "Attempted to remove more instructions than there are available."
            );

        Instruction? newTarget = Instructions[index + instructions];

        List<ILLabel> labels = [];

        while (instructions-- > 0)
        {
            foreach (var label in Context.GetIncomingLabels(Instructions[index]))
                labels.Add(label);

            Instructions.RemoveAt(index);
        }

        Current = newTarget;
        orphanedLabels = labels;
        return this;
    }

    public ILWeaver RemoveAtCurrent(int instructions, out IEnumerable<ILLabel> orphanedLabels) =>
        RemoveAt(Index, instructions, out orphanedLabels);

    public ILWeaver Remove(Instruction instruction, out ILLabel? orphanedLabel)
    {
        RemoveAt(Context.IndexOf(instruction), 1, out var orphanedLabels);
        orphanedLabel = orphanedLabels.FirstOrDefault();
        return this;
    }

    public ILWeaver RemoveCurrent(out ILLabel? orphanedLabel)
    {
        Remove(Current, out orphanedLabel);
        return this;
    }

    /// <summary>
    /// Create a new <see cref="ExceptionHandler"/> to be assigned the following:<br/>
    /// <see cref="HandlerSetTryStart(Instruction, ExceptionHandler)"/><br/>
    /// <see cref="HandlerSetTryEnd(Instruction, ExceptionHandler)"/>
    /// - NOTE: If left null, is set implicitly.<br/>
    /// <see cref="HandlerSetCatchStart(Instruction, ExceptionHandler)"/><br/>
    /// <see cref="HandlerSetCatchEnd(Instruction, ExceptionHandler)"/><br/>
    /// <br/>
    /// And if the <see cref="ExceptionHandlerType"/> is <see cref="ExceptionHandlerType.Filter"/>, also:<br/>
    /// <see cref="HandlerSetFilterStart(Instruction, ExceptionHandler)"/><br/>
    /// <br/>
    /// The <see cref="ExceptionHandler"/> needs to then finally be applied to the method body with:<br/>
    /// <see cref="HandlerApply(ExceptionHandler)"/><br/>
    /// </summary>
    /// <param name="exceptionHandlerType">The type of the <see cref="ExceptionHandler"/>.</param>
    /// <param name="catchType">The types of Exceptions that should be catched.
    /// If left null, type `System.Exception` is used.</param>
    /// <param name="handler">The created <see cref="ExceptionHandler"/> to be configured and then applied.</param>
    /// <returns></returns>
    public ILWeaver HandlerCreate(
        ExceptionHandlerType exceptionHandlerType,
        Type? catchType,
        out ExceptionHandler handler
    )
    {
        catchType ??= typeof(Exception);
        handler = new ExceptionHandler(exceptionHandlerType) { CatchType = IL.Import(catchType) };
        return this;
    }

    /// <summary>
    /// Set the TryStart property of the <see cref="ExceptionHandler"/>.
    /// </summary>
    /// <remarks>
    /// This value is inclusive.
    /// </remarks>
    /// <param name="tryStart">The first instruction in the try block.</param>
    /// <param name="handler">The <see cref="ExceptionHandler"/> to configure.</param>
    /// <returns>This <see cref="ILWeaver"/>.</returns>
    public ILWeaver HandlerSetTryStart(Instruction tryStart, ExceptionHandler handler)
    {
        handler.TryStart = tryStart;
        return this;
    }

    /// <inheritdoc cref="HandlerSetTryStart(Instruction, ExceptionHandler)"/>
    public ILWeaver HandlerSetTryStartOnCurrent(ExceptionHandler handler) =>
        HandlerSetTryStart(Current, handler);

    /// <summary>
    /// Set the TryEnd property of the <see cref="ExceptionHandler"/>.
    /// </summary>
    /// <param name="tryEnd">The last instruction in the try block.</param>
    /// <inheritdoc cref="HandlerSetTryStart(Instruction, ExceptionHandler)"/>
    public ILWeaver HandlerSetTryEnd(Instruction tryEnd, ExceptionHandler handler)
    {
        handler.TryEnd = tryEnd;
        return this;
    }

    /// <inheritdoc cref="HandlerSetTryEnd(Instruction, ExceptionHandler)"/>
    public ILWeaver HandlerSetTryEndOnCurrent(ExceptionHandler handler) =>
        HandlerSetTryEnd(Current, handler);

    /// <summary>
    /// Set the FilterStart property of the <see cref="ExceptionHandler"/>.
    /// </summary>
    /// <param name="filterStart">The first instruction in the filter block.</param>
    /// <inheritdoc cref="HandlerSetTryStart(Instruction, ExceptionHandler)"/>
    public ILWeaver HandlerSetFilterStart(Instruction filterStart, ExceptionHandler handler)
    {
        handler.FilterStart = filterStart;
        return this;
    }

    /// <inheritdoc cref="HandlerSetFilterStart(Instruction, ExceptionHandler)"/>
    public ILWeaver HandlerSetFilterStartOnCurrent(ExceptionHandler handler) =>
        HandlerSetFilterStart(Current, handler);

    /// <summary>
    /// Set the CatchStart property of the <see cref="ExceptionHandler"/>.
    /// </summary>
    /// <param name="catchStart">The first instruction in the catch block.</param>
    /// <inheritdoc cref="HandlerSetTryStart(Instruction, ExceptionHandler)"/>
    public ILWeaver HandlerSetCatchStart(Instruction catchStart, ExceptionHandler handler)
    {
        handler.HandlerStart = catchStart;
        return this;
    }

    /// <inheritdoc cref="HandlerSetCatchStart(Instruction, ExceptionHandler)"/>
    public ILWeaver HandlerSetCatchStartOnCurrent(ExceptionHandler handler) =>
        HandlerSetCatchStart(Current, handler);

    /// <summary>
    /// Set the CatchEnd property of the <see cref="ExceptionHandler"/>.
    /// </summary>
    /// <param name="catchEnd">The last instruction in the catch block.</param>
    /// <inheritdoc cref="HandlerSetTryStart(Instruction, ExceptionHandler)"/>
    public ILWeaver HandlerSetCatchEnd(Instruction catchEnd, ExceptionHandler handler)
    {
        handler.HandlerEnd = catchEnd;
        return this;
    }

    /// <inheritdoc cref="HandlerSetCatchEnd(Instruction, ExceptionHandler)"/>
    public ILWeaver HandlerSetCatchEndOnCurrent(ExceptionHandler handler) =>
        HandlerSetCatchEnd(Current, handler);

    /// <summary>
    /// Writes the leave instructions for try, catch or finally blocks and applies the
    /// provided <see cref="ExceptionHandler"/> to the method body.
    /// </summary>
    /// <remarks>
    /// Once applied, the leave label of the handler leave instructions will point to the
    /// instruction that comes after what was set as HandlerEnd. Make sure that once you have
    /// applied the <see cref="ExceptionHandler"/>, you are not inserting instructions before
    /// the HandlerEnd or you'll need to retarget the leave label to your first inserted instruction
    /// before the EndHandler.
    /// </remarks>
    /// <param name="handler">The handler to apply.</param>
    /// <returns>This <see cref="ILWeaver"/>.</returns>
    /// <exception cref="NullReferenceException"></exception>
    public ILWeaver HandlerApply(ExceptionHandler handler)
    {
        if (handler.TryStart is null)
            throw new NullReferenceException("TryStart was not set!");
        if (handler.HandlerEnd is null)
            throw new NullReferenceException("HandlerEnd was not set!");

        bool isFilter = handler.HandlerType == ExceptionHandlerType.Filter;

        if (handler.TryEnd is not null && handler.TryEnd == handler.HandlerStart)
        {
            string notFilterMessage =
                " Either don't set HandlerStart and let it be set implicitly, or"
                + " set it to the next instruction.";

            throw new InvalidOperationException(
                "TryEnd was set to the same instruction as HandlerStart."
                    + (isFilter ? null : notFilterMessage)
            );
        }

        // Time to figure out values implicitly.
        if (handler.TryEnd is null)
        {
            if (isFilter)
            {
                if (handler.FilterStart is null)
                    throw new NullReferenceException("FilterStart was not set!");

                handler.TryEnd = handler.FilterStart;
            }
            else
            {
                if (handler.HandlerStart is null)
                    throw new NullReferenceException(
                        "TryEnd and HandlerStart were not set!"
                            + " Note that only one of then needs to be set."
                    );

                handler.TryEnd = handler.HandlerStart;
            }
        }
        else
        {
            // inclusive range → exclusive
            handler.TryEnd = handler.TryEnd.Next;
        }

        if (handler.HandlerStart is null)
        {
            if (isFilter)
            {
                throw new NullReferenceException(
                    "HandlerStart must be set when HandlerType is Filter!"
                );
            }
            else
            {
                handler.HandlerStart = handler.TryEnd;
            }
        }

        if (handler.HandlerEnd.Next is null)
        {
            InsertAfter(handler.HandlerEnd, Create(OpCodes.Nop));
        }
        // inclusive range → exclusive
        handler.HandlerEnd = handler.HandlerEnd.Next!;

        // Now we can start inserting all our instructions.
        ILLabel leaveDestination = Context.DefineLabel(handler.HandlerEnd);
        Instruction leave = Create(OpCodes.Leave, leaveDestination);

        // And emit the actual leave instructions.
        // Try should have a normal leave instruction or nothing if it throws.
        if (handler.TryEnd.Previous.OpCode != OpCodes.Throw)
        {
            InsertBefore(handler.TryEnd, leave);
        }

        // If we have a filter, aka: catch (Exception ex) when (/* statement */)
        // then we need the endfilter instruction.
        if (isFilter)
        {
            if (handler.FilterStart is null)
                throw new NullReferenceException("FilterStart was not set!");

            // FilterEnd doesn't exist, it's implicitly before HandlerStart.
            InsertBefore(handler.HandlerStart, Create(OpCodes.Endfilter));
        }

        // Finally also has a special instruction.
        if (handler.HandlerType == ExceptionHandlerType.Finally)
        {
            InsertBefore(handler.HandlerEnd, Create(OpCodes.Endfinally));
        }
        else
        {
            // For anything other than finally, use a normal leave instruction.
            InsertBefore(handler.HandlerEnd, leave);
        }
        // We retarget handler end again as it must be after leave instructions.
        // handler.HandlerEnd = handler.HandlerEnd.Next;

        // Body.Method.RecalculateILOffsets();
        // Console.WriteLine("handler.TryStart:     " + handler.TryStart);
        // Console.WriteLine("handler.TryEnd:       " + handler.TryEnd);
        // Console.WriteLine("handler.HandlerStart: " + handler.HandlerStart);
        // Console.WriteLine("handler.HandlerEnd:   " + handler.HandlerEnd);

        Context.Body.ExceptionHandlers.Add(handler);
        return this;
    }

    /// <summary>
    /// Set <see cref="Current"/> to a target index. See also <see cref="CurrentTo(Instruction)"/>
    /// </summary>
    /// <remarks>
    /// A negative index will loop back.
    /// </remarks>
    /// <returns>this <see cref="ILWeaver"/></returns>
    /// <exception cref="IndexOutOfRangeException"></exception>
    public ILWeaver CurrentTo(int index)
    {
        int idx = index;
        if (idx < 0)
            idx += Instructions.Count;

        Instruction? instruction = idx >= Instructions.Count ? null : Instructions[idx];
        if (instruction is null)
            throw new IndexOutOfRangeException(
                "Attempted to set ILWeaver's position out of bounds. "
                    + $"Attempted index: {idx}; valid range: 0 to {Instructions.Count - 1}"
                    + (index == idx ? "" : $" (actual index without loop back: {index})")
            );

        CurrentTo(instruction);
        return this;
    }

    /// <summary>
    /// Set <see cref="Current"/> to a target instruction.
    /// See also <see cref="CurrentTo(int)"/><br/>
    /// <br/>
    /// For use in <see cref="Match(Predicate{Instruction}[])"/> and other variations,
    /// use <see cref="PointCurrentTo"/> as that method returns true.
    /// </summary>
    /// <returns>this <see cref="ILWeaver"/></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public ILWeaver CurrentTo(Instruction instruction)
    {
        if (instruction is null)
            throw new ArgumentNullException(
                nameof(instruction),
                "Attempted to set Current to a null instruction."
            );

        current = instruction;
        return this;
    }

    /// <summary>
    /// Set <see cref="Current"/> to a target instruction just like
    /// <see cref="CurrentTo(Instruction)"/>, except this returns true
    /// for use in <see cref="Match(Predicate{Instruction}[])"/> and other variations.
    /// </summary>
    /// <param name="instruction">The instruction to set as current.</param>
    /// <returns>true</returns>
    public bool PointCurrentTo(Instruction instruction)
    {
        CurrentTo(instruction);
        return true;
    }

    /// <summary>
    /// Set instruction to a target instruction and returns true
    /// for use in <see cref="Match(Predicate{Instruction}[])"/> and other variations.
    /// </summary>
    /// <param name="toBeSet">The instruction to be set.</param>
    /// <param name="target">The instruction toBeSet will be set to.</param>
    /// <returns>true</returns>
    public bool PointInstructionTo([NotNull] Instruction? toBeSet, Instruction target)
    {
        Helpers.ThrowIfNull(target);
        toBeSet = target;
        return true;
    }

    public ILWeaver CurrentToNext() => CurrentTo(Current.Next);

    public ILWeaver CurrentToPrevious() => CurrentTo(Current.Previous);

    /// <summary>
    /// Attempts to match a set of predicates to find one specific
    /// location in the instructions. This method searches the entire target method
    /// to ensure the match predicates are matching exactly what was attempted to match.<br/>
    /// <br/>
    /// If you want to match multiple locations, use
    /// <see cref="MatchMultiple(Action{ILWeaver}, Predicate{Instruction}[])"/><br/>
    /// <br/>
    /// <example>
    /// In the following example we match two instructions, setting
    /// <see cref="Current"/> to the brtrue instruction which remains as the
    /// <see cref="Current"/> if the match is successful.
    /// <code>
    /// <![CDATA[
    /// weaver
    ///     .Match(
    ///         x => x.MatchLdloc(1),
    ///         x => x.MatchBrtrue(out _) && weaver.PointCurrentTo(x)
    ///     )
    ///     .ThrowIfFailure()
    ///     .EmitBeforeCurrent(weaver.Create(OpCodes.Call, GetCustomNumber));
    /// ]]>
    /// </code>
    /// </example>
    /// </summary>
    /// if the match is successful.
    /// <param name="predicates">The predicates to match against.</param>
    /// <returns>An <see cref="ILWeaverResult"/> which can be used
    /// for checking if the match was a success or a failure.</returns>
    public ILWeaverResult Match(params Predicate<Instruction>[] predicates) =>
        MatchInternal(allowMultipleMatches: false, null, predicates);

    /// <summary>
    /// Attempts to match a set of predicates multiple times to find specific
    /// locations in the instructions.<br/>
    /// <br/>
    /// <example>
    /// In the following example we match two instructions, setting
    /// <see cref="Current"/> to the brtrue instruction which only applies to
    /// the <see cref="ILWeaver"/> clones which are passed to the `onMatch` delegate's argument.
    /// This means that the <see cref="ILWeaver"/> you run this method on will keep its original
    /// <see cref="Current"/> even if it's set in the predicates.
    /// <code>
    /// <![CDATA[
    /// weaver
    ///     .MatchMultiple(
    ///         onMatch: matchWeaver =>
    ///         {
    ///             matchWeaver.EmitBeforeCurrent(
    ///                 matchWeaver.Create(OpCodes.Call, GetCustomNumber)
    ///             );
    ///         },
    ///         x => x.MatchLdloc(1),
    ///         x => x.MatchBrtrue(out _) && weaver.PointCurrentTo(x)
    ///     )
    ///     .ThrowIfFailure();
    /// ]]>
    /// </code>
    /// </example>
    /// </summary>
    /// <param name="onMatch">A delegate which runs for each match, passing a copy of the
    /// original <see cref="ILWeaver"/> with the <see cref="Current"/> pointing to the one
    /// at the time of the match.</param>
    /// <inheritdoc cref="Match"/>
    public ILWeaverResult MatchMultiple(
        Action<ILWeaver> onMatch,
        params Predicate<Instruction>[] predicates
    ) => MatchInternal(allowMultipleMatches: true, onMatch, predicates);

    ILWeaverResult MatchInternal(
        bool allowMultipleMatches,
        Action<ILWeaver>? onMatched,
        params Predicate<Instruction>[] predicates
    )
    {
        Helpers.ThrowIfNull(predicates);

        if (allowMultipleMatches)
            Helpers.ThrowIfNull(onMatched);

        Instruction originalCurrent = Current;

        List<int> matchedIndexes = [];
        List<(int count, int indexBeforeFailed)> bestAttempts = [(0, 0)];

        int predicatesMatched = 0;
        for (int i = 0; i < Instructions.Count; i++)
        {
            if (!predicates[predicatesMatched](Instructions[i]))
            {
                if (predicatesMatched > 0)
                {
                    if (bestAttempts[0].count < predicatesMatched)
                    {
                        bestAttempts.Clear();
                        bestAttempts.Add((predicatesMatched, i - 1));
                    }
                    else if (bestAttempts[0].count == predicatesMatched)
                        bestAttempts.Add((predicatesMatched, i - 1));
                }

                predicatesMatched = 0;
                continue;
            }

            predicatesMatched++;

            if (predicatesMatched != predicates.Length)
                continue;

            predicatesMatched = 0;
            matchedIndexes.Add(i);

            // It's possible that the User didn't set Current in the matching predicates.
            // We don't want that to happen, but I don't like the idea of keeping state about
            // if Current was set, and it would only be caught at runtime anyways.
            //
            // I also don't like implicitly setting Current to the first matched predicate if
            // it wasn't set, as the user might not read the xml docs and never learns they can
            // set Current to any matched predicate.
            //
            // So I think the best solution is an analyzer that enforces setting Current.

            if (allowMultipleMatches)
            {
                onMatched!(Clone());
            }
        }

        if (allowMultipleMatches)
        {
            // When matching multiple, keep original for this weaver in any case.
            CurrentTo(originalCurrent);

            if (matchedIndexes.Count > 0)
            {
                return new ILWeaverResult(this, null);
            }
        }
        else
        {
            if (matchedIndexes.Count == 1)
            {
                return new ILWeaverResult(this, null);
            }

            // When failing a single match, reset to original.
            CurrentTo(originalCurrent);
        }

        CodeBuilder err = new(new StringBuilder(), 2);
        if (matchedIndexes.Count > 0)
        {
            string GetMatchedTooManyError()
            {
                err.WriteLine(
                        $"- {nameof(ILWeaver)}.{nameof(Match)} matched all predicates more than once in the target method."
                    )
                    .IncreaseIndent()
                    .Write("- Total matches: ")
                    .WriteLine(matchedIndexes.Count)
                    .IncreaseIndent();

                foreach (var match in matchedIndexes)
                    err.Write("- at indexes: ")
                        .Write(match - predicates.Length + 1)
                        .Write(" to ")
                        .WriteLine(match);

                err.DecreaseIndent()
                    .WriteLine("- HELP: Add more predicates to find a unique match.")
                    .IncreaseIndent()
                    .WriteLine($"- Documentation: {gotoMatchingDocsLink}")
                    .DecreaseIndent()
                    .WriteLine(
                        $"- INFO: Use {nameof(ILWeaver)}.GotoMatchMultiple if you intend to match multiple instances."
                    )
                    .WriteLine(
                        $"- INFO: Use {nameof(ILWeaver)}.GotoNext if you intend to only match the first valid instance."
                    );

                return err.ToString();
            }

            return new ILWeaverResult(this, GetMatchedTooManyError);
        }

        // - ILWeaver.Find couldn't match all predicates.
        //   - Matched predicates required: 3
        //   - Best attempts: predicates matched: 2 (see: [Detailed Info])
        //   - HELP: Instruction matching predicates should match a valid pattern in the target method's instructions.
        //     - HELP: See [Detailed Info] for what went wrong.
        //     - NOTE: Other ILHooks have modified the target method's instructions. <<CONDITIONAL>>
        //     - HELP: Documentation: https://hamunii.github.io/monodetour/ilweaver/matching-instructions
        //
        // [Detailed Info]
        // Best attempts are listed here.
        // The following attempts matched the number of predicates: 2
        string GetNotMatchedAllError()
        {
            err.Write(
                    $"{nameof(ILWeaver)}.{nameof(Match)} couldn't match all predicates for method: "
                )
                .WriteLine(Context.Method.FullName)
                // .WriteLine("HELP: Instruction matching predicates must match a valid pattern in the target method's instructions.")
                // .WriteLine("| NOTE: Other ILHooks could have modified the target method's instructions. <<CONDITIONAL>>")
                // .WriteLine($"| HELP: Documentation: {gotoMatchingDocsLink}")
                .Write("Matched predicates required: ")
                .WriteLine(predicates.Length)
                .Write("The following best attempts matched the number of predicates: ")
                .WriteLine(bestAttempts[0].count);

            if (bestAttempts[0].count == 0)
            {
                err.WriteLine().WriteLine("<no meaningful data for 0 predicates matched>");
            }
            else
            {
                for (int i = 0; i < bestAttempts.Count; i++)
                {
                    var (count, indexBeforeFailed) = bestAttempts[i];
                    var nextInstruction = Instructions[indexBeforeFailed + 1];
                    err.RemoveIndent()
                        .WriteLine()
                        .Write(i + 1)
                        .Write(". Matched predicates: ")
                        .Write(count)
                        .Write(" (at: ")
                        .Write(indexBeforeFailed - count + 1)
                        .Write(" to ")
                        .Write(indexBeforeFailed)
                        .WriteLine(')')
                        .IncreaseIndent()
                        .WriteLine("- next predicate didn't match instruction:")
                        .IncreaseIndent()
                        .Write("- ")
                        .Write(indexBeforeFailed + 1)
                        .Write(' ')
                        .WriteLine(nextInstruction.ToString())
                        .WriteLine(
                            "- this instruction could be matched with any of the following predicates:"
                        )
                        .IncreaseIndent()
                        .WriteLine("- ignore OpCode and Operand:")
                        .IncreaseIndent()
                        .WriteLine("x => true");
                }
            }

            return err.ToString();
        }

        return new ILWeaverResult(this, GetNotMatchedAllError);
    }

    ILWeaver InsertAtInternal(int index, Instruction instruction, bool insertAfterIndex)
    {
        Helpers.ThrowIfNull(instruction);

        Instruction InstructionAtIndex = Instructions[index];

        // When inserting before a target instruction that is inside handler ranges,
        // include the inserted instruction inside the start range.
        if (!insertAfterIndex)
        {
            foreach (var eh in Body.ExceptionHandlers)
            {
                if (eh.TryStart == InstructionAtIndex)
                    eh.TryStart = instruction;
                if (eh.HandlerStart == InstructionAtIndex)
                    eh.HandlerStart = instruction;
                if (eh.FilterStart == InstructionAtIndex)
                    eh.FilterStart = instruction;
            }
        }
        // In this case we are inserting after a target instruction,
        // so we want our instruction to be inside the end range.
        else
        {
            foreach (var eh in Body.ExceptionHandlers)
            {
                if (eh.TryEnd == InstructionAtIndex)
                    eh.TryEnd = instruction;
                if (eh.HandlerEnd == InstructionAtIndex)
                    eh.HandlerEnd = instruction;
            }
        }

        if (insertAfterIndex)
        {
            index += 1;
        }

        RetargetLabels(pendingFutureNextInsertLabels, instruction);
        pendingFutureNextInsertLabels.Clear();

        Instructions.Insert(index, instruction);
        return this;
    }

    /// <summary>
    /// Insert instructions before the provided index.
    /// </summary>
    /// <remarks>
    /// If the instruction at the provided index is inside the start of
    /// a try, filter, or catch range, then the first inserted instruction
    /// will become the new start of that range.
    /// </remarks>
    public ILWeaver InsertBefore(int index, params IEnumerable<Instruction> instructions)
    {
        foreach (var instruction in instructions)
        {
            InsertAtInternal(index, instruction, insertAfterIndex: false);
            index++;
        }

        return this;
    }

    /// <summary>
    /// Insert instructions before the provided instruction.
    /// </summary>
    /// <remarks>
    /// If the provided target instruction is inside the start of
    /// a try, filter, or catch range, then the first inserted instruction
    /// will become the new start of that range.
    /// </remarks>
    public ILWeaver InsertBefore(
        Instruction target,
        params IEnumerable<Instruction> instructions
    ) => InsertBefore(Context.IndexOf(target), instructions);

    /// <summary>
    /// Insert instructions before this weaver's current position.
    /// Current target doesn't change.
    /// </summary>
    /// <remarks>
    /// If <see cref="Current"/> is inside the start of
    /// a try, filter, or catch range, then the first inserted instruction
    /// will become the new start of that range.
    /// </remarks>
    public ILWeaver InsertBeforeCurrent(params IEnumerable<Instruction> instructions) =>
        InsertBefore(Index, instructions);

    /// <summary>
    /// Insert instructions after the provided index.
    /// </summary>
    /// <remarks>
    /// If the instruction at the provided index is inside the end of
    /// a try or a catch range, then the last inserted instruction
    /// will become the new end of that range.
    /// </remarks>
    public ILWeaver InsertAfter(int index, params IEnumerable<Instruction> instructions)
    {
        foreach (var instruction in instructions)
        {
            InsertAtInternal(index, instruction, insertAfterIndex: true);
            index++;
        }

        return this;
    }

    /// <summary>
    /// Insert instructions after the provided instruction.
    /// </summary>
    /// <remarks>
    /// If the provided target instruction is inside the end of
    /// a try or a catch range, then the last inserted instruction
    /// will become the new end of that range.
    /// </remarks>
    public ILWeaver InsertAfter(Instruction target, params IEnumerable<Instruction> instructions) =>
        InsertAfter(Context.IndexOf(target), instructions);

    /// <summary>
    /// Insert instructions after this weaver's current position.
    /// Retargets Current to the last inserted instruction.
    /// </summary>
    /// <remarks>
    /// If <see cref="Current"/> is inside the end of
    /// a try or a catch range, then the last inserted instruction
    /// will become the new end of that range.
    /// </remarks>
    public ILWeaver InsertAfterCurrent(params IEnumerable<Instruction> instructions)
    {
        int index = Index;
        foreach (var instruction in instructions)
        {
            InsertAtInternal(index, instruction, insertAfterIndex: true);
            CurrentTo(instruction);
            index++;
        }

        return this;
    }

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
    public Instruction Create(OpCode opcode, Instruction[] targets) => IL.Create(opcode, targets);

    /// <inheritdoc cref="Create(OpCode, ParameterDefinition)"/>
    public Instruction Create(OpCode opcode, Instruction target) => IL.Create(opcode, target);

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

#pragma warning disable CS1574 // XML comment has cref attribute that could not be resolved
    /// <remarks>
    /// If the delegate method isn't static, its instance must be pushed to the stack first.<br/>
    /// The delegate method must not be a lambda expression, as one requires an anonymous
    /// instance to be loaded. If it is a lambda expression, use <see cref="CreateDelegate"/>.
    /// </remarks>
    /// <inheritdoc cref="Create(OpCode, ParameterDefinition)"/>
    public Instruction CreateCall(Delegate method) => IL.Create(OpCodes.Call, method.Method);
#pragma warning restore CS1574 // XML comment has cref attribute that could not be resolved

    /// <summary>
    /// Create a new instruction accessing a given member, to be emitted by
    /// <see cref="InsertBeforeCurrent(IEnumerable{Instruction})"/> or any of the variations.
    /// </summary>
    /// <typeparam name="T">The type in which the member is defined.</typeparam>
    /// <param name="memberName">The accessed member name.</param>
    /// <inheritdoc cref="Create(OpCode, ParameterDefinition)"/>
    /// <exception cref="NotSupportedException"></exception>
    public Instruction Create<T>(OpCode opcode, string memberName) =>
        IL.Create(opcode, typeof(T).GetMember(memberName, (BindingFlags)(-1)).First());
}

public class ILWeaverResult
{
    public ILWeaverResult(ILWeaver weaver, Func<string>? failureMessage)
    {
        this.weaver = weaver;

        if (failureMessage is not null)
        {
            IsValid = false;
            getFailureMessage = failureMessage;
        }
    }

    [MemberNotNullWhen(false, nameof(FailureMessage))]
    public bool IsValid { get; } = true;
    public string? FailureMessage => invalidActionMessage ??= getFailureMessage?.Invoke();

    string? invalidActionMessage;
    readonly Func<string>? getFailureMessage;
    readonly ILWeaver weaver;

    /// <summary>
    /// Throws if the previous action was not successful.<br/>
    /// <br/>
    /// For checking if the action was valid without throwing, see
    /// <see cref="IsValid"/> or <see cref="Extract"/>.
    /// </summary>
    /// <returns>The <see cref="ILWeaver"/>.</returns>
    /// <exception cref="ILWeaverResultException"></exception>
    public ILWeaver ThrowIfFailure()
    {
        if (IsValid)
            return weaver;

        throw new ILWeaverResultException($"Failed result was thrown.\n" + FailureMessage);
    }

    /// <summary>
    /// Outputs this <see cref="ILWeaverResult"/>. This method exists to allow
    /// more fluent chaining of the <see cref="ILWeaver"/> methods.
    /// </summary>
    /// <returns>The <see cref="ILWeaver"/>.</returns>
    public ILWeaver Extract(out ILWeaverResult result)
    {
        result = this;
        return weaver;
    }
}

[Serializable]
public class ILWeaverResultException : Exception
{
    public ILWeaverResultException() { }

    public ILWeaverResultException(string message)
        : base(message) { }

    public ILWeaverResultException(string message, Exception inner)
        : base(message, inner) { }

    protected ILWeaverResultException(
        System.Runtime.Serialization.SerializationInfo info,
        System.Runtime.Serialization.StreamingContext context
    )
        : base(info, context) { }
}
