using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoDetour.Interop.MonoModUtils;
using MonoDetour.Logging;
using MonoMod.Cil;
using MonoMod.SourceGen.Internal;
using MonoMod.Utils;
using InstrList = Mono.Collections.Generic.Collection<Mono.Cecil.Cil.Instruction>;
using MethodBody = Mono.Cecil.Cil.MethodBody;

namespace MonoDetour.Cil;

// TODO: fix all warnings.
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable CS1573 // Parameter has no matching param tag in the XML comment (but other parameters do)

public class ILWeaver(ILManipulationInfo il) : IMonoDetourLogSource
{
    /// <inheritdoc cref="ILManipulationInfo"/>
    public ILManipulationInfo ManipulationInfo { get; } = il;

    /// <inheritdoc cref="ILContext"/>
    public ILContext Context { get; } = il.Context;

    /// <inheritdoc cref="ILContext.IL"/>
    public ILProcessor IL => Context.IL;

    /// <inheritdoc cref="ILContext.Body"/>
    public MethodBody Body => Context.Body;

    /// <inheritdoc cref="ILContext.Method"/>
    public MethodDefinition Method => Context.Method;

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
    /// The instruction before what this weaver currently points to.
    /// </summary>
    public Instruction Previous => Current.Previous;

    /// <summary>
    /// The instruction after what this weaver currently points to.
    /// </summary>
    /// <remarks>
    /// This is not equivalent to <see cref="ILCursor.Next"/>.
    /// The equivalent would be <see cref="Current"/>.
    /// </remarks>
    public Instruction Next => Current.Next;

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
        get => Instructions.IndexOf(Current);
        set => CurrentTo(value);
    }

    /// <inheritdoc/>
    public MonoDetourLogger.LogChannel LogFilter { get; set; } =
        MonoDetourLogger.LogChannel.Warning | MonoDetourLogger.LogChannel.Error;

    Instruction current = il.Context.Instrs[0];

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

    // TODO: Make variations like ILLabel DefineLabel

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
    /// <param name="markedLabel">The marked label.</param>
    /// <returns>This <see cref="ILWeaver"/>.</returns>
    public ILWeaver MarkLabelTo(Instruction target, out ILLabel markedLabel)
    {
        Helpers.ThrowIfNull(target);
        markedLabel = Context.DefineLabel(target);
        return this;
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
    public ILWeaver MarkLabelToFutureNextInsert(out ILLabel futureMarkedLabel)
    {
        futureMarkedLabel = Context.DefineLabel();
        pendingFutureNextInsertLabels.Add(futureMarkedLabel);
        return this;
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
    /// Prefer <see cref="MarkLabelToFutureNextInsert(out ILLabel)"/> if the label
    /// will always be redirected to an inserted instruction. Using this
    /// method will then show that there branches where a next instruction isn't inserted.
    /// </remarks>
    /// <param name="futureMarkedLabel">The marked label.</param>
    /// <returns>This <see cref="ILWeaver"/>.</returns>
    public ILWeaver MarkLabelToCurrentOrFutureNextInsert(out ILLabel futureMarkedLabel)
    {
        futureMarkedLabel = Context.DefineLabel(Current);
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

    public ILWeaver RemoveAtCurrent(int instructions, out IEnumerable<ILLabel> orphanedLabels) =>
        RemoveAt(Index, instructions, out orphanedLabels);

    public ILWeaver Remove(Instruction instruction, out ILLabel? orphanedLabel)
    {
        RemoveAt(Instructions.IndexOf(instruction), 1, out var orphanedLabels);
        orphanedLabel = orphanedLabels.FirstOrDefault();
        return this;
    }

    public ILWeaver RemoveCurrent(out ILLabel? orphanedLabel)
    {
        var next = Next;
        Remove(Current, out orphanedLabel);
        CurrentTo(next);
        return this;
    }

    /// <summary>
    /// Create a new <see cref="IWeaverExceptionHandler"/> to be assigned its ranges.
    /// Note that certain values can be figured out implicitly such as HandlerStart when
    /// TryEnd is defined.<br/>
    /// <br/>
    /// The <see cref="IWeaverExceptionHandler"/> needs to be applied to the method body with
    /// <see cref="HandlerApply(IWeaverExceptionHandler)"/> after which it becomes immutable.<br/>
    /// </summary>
    /// <remarks>
    /// See <see cref="WeaverExceptionCatchHandler"/> for more information about this handler type.
    /// </remarks>
    /// <param name="catchType">The types of Exceptions that should be catched.
    /// If left null, <c>object</c> is used.</param>
    /// <param name="handler">The created <see cref="IWeaverExceptionHandler"/> to be configured and then applied.</param>
    /// <returns>The new exception handler instance.</returns>
    public ILWeaver HandlerCreateCatch(Type? catchType, out WeaverExceptionCatchHandler handler)
    {
        handler = new(IL.Import(catchType ?? typeof(Exception)));
        return this;
    }

    /// <remarks>
    /// See <see cref="WeaverExceptionFilterHandler"/> for more information about this handler type.
    /// </remarks>
    /// <inheritdoc cref="HandlerCreateCatch(Type?, out WeaverExceptionCatchHandler)"/>
    public ILWeaver HandlerCreateFilter(Type? catchType, out WeaverExceptionFilterHandler handler)
    {
        handler = new(IL.Import(catchType ?? typeof(Exception)));
        return this;
    }

    /// <remarks>
    /// See <see cref="WeaverExceptionFinallyHandler"/> for more information about this handler type.
    /// </remarks>
    /// <inheritdoc cref="HandlerCreateCatch(Type?, out WeaverExceptionCatchHandler)"/>
    public ILWeaver HandlerCreateFinally(out WeaverExceptionFinallyHandler handler)
    {
        handler = new();
        return this;
    }

    /// <remarks>
    /// See <see cref="WeaverExceptionFaultHandler"/> for more information about this handler type.
    /// </remarks>
    /// <inheritdoc cref="HandlerCreateCatch(Type?, out WeaverExceptionCatchHandler)"/>
    public ILWeaver HandlerCreateFault(out WeaverExceptionFaultHandler handler)
    {
        handler = new();
        return this;
    }

    /// <summary>
    /// Set the TryStart property of the <see cref="IWeaverExceptionHandler"/>.
    /// </summary>
    /// <remarks>
    /// This range is inclusive.
    /// </remarks>
    /// <param name="tryStart">The first ILLabel in the try block.</param>
    /// <param name="handler">The <see cref="IWeaverExceptionHandler"/> to configure.</param>
    /// <returns>This <see cref="ILWeaver"/>.</returns>
    public ILWeaver HandlerSetTryStart(ILLabel tryStart, IWeaverExceptionHandler handler)
    {
        handler.TryStart = tryStart;
        return this;
    }

    /// <inheritdoc cref="HandlerSetTryStart(ILLabel, IWeaverExceptionHandler)"/>
    public ILWeaver HandlerSetTryStart(Instruction tryStart, IWeaverExceptionHandler handler)
    {
        handler.TryStart = Context.DefineLabel(tryStart);
        return this;
    }

    /// <summary>
    /// Set the TryEnd property of the <see cref="IWeaverExceptionHandler"/>.
    /// </summary>
    /// <param name="tryEnd">The last instruction in the try block.</param>
    /// <inheritdoc cref="HandlerSetTryStart(ILLabel, IWeaverExceptionHandler)"/>
    public ILWeaver HandlerSetTryEnd(ILLabel tryEnd, IWeaverExceptionHandler handler)
    {
        handler.TryEnd = tryEnd;
        return this;
    }

    /// <inheritdoc cref="HandlerSetTryEnd(ILLabel, IWeaverExceptionHandler)"/>
    public ILWeaver HandlerSetTryEnd(Instruction tryEnd, IWeaverExceptionHandler handler)
    {
        handler.TryEnd = Context.DefineLabel(tryEnd);
        return this;
    }

    /// <summary>
    /// Set the FilterStart property of the <see cref="WeaverExceptionFilterHandler"/>.
    /// </summary>
    /// <param name="filterStart">The first ILLabel in the filter block.</param>
    /// <inheritdoc cref="HandlerSetTryStart(ILLabel, IWeaverExceptionHandler)"/>
    public ILWeaver HandlerSetFilterStart(ILLabel filterStart, WeaverExceptionFilterHandler handler)
    {
        handler.FilterStart = filterStart;
        return this;
    }

    /// <inheritdoc cref="HandlerSetFilterStart(ILLabel, WeaverExceptionFilterHandler)"/>
    public ILWeaver FilterSetFilterStart(
        Instruction filterStart,
        WeaverExceptionFilterHandler handler
    )
    {
        handler.FilterStart = Context.DefineLabel(filterStart);
        return this;
    }

    /// <summary>
    /// Set the HandlerStart property of the <see cref="IWeaverExceptionHandler"/>.
    /// </summary>
    /// <param name="handlerStart">The first ILLabel in the catch block.</param>
    /// <inheritdoc cref="HandlerSetTryStart(ILLabel, IWeaverExceptionHandler)"/>
    public ILWeaver HandlerSetHandlerStart(ILLabel handlerStart, IWeaverExceptionHandler handler)
    {
        handler.HandlerStart = handlerStart;
        return this;
    }

    /// <inheritdoc cref="HandlerSetHandlerStart(ILLabel, IWeaverExceptionHandler)"/>
    public ILWeaver HandlerSetHandlerStart(
        Instruction handlerStart,
        IWeaverExceptionHandler handler
    )
    {
        handler.HandlerStart = Context.DefineLabel(handlerStart);
        return this;
    }

    /// <summary>
    /// Set the HandlerEnd property of the <see cref="IWeaverExceptionHandler"/>.
    /// </summary>
    /// <param name="handlerEnd">The last ILLabel in the catch block.</param>
    /// <inheritdoc cref="HandlerSetTryStart(ILLabel, IWeaverExceptionHandler)"/>
    public ILWeaver HandlerSetHandlerEnd(ILLabel handlerEnd, IWeaverExceptionHandler handler)
    {
        handler.HandlerEnd = handlerEnd;
        return this;
    }

    /// <inheritdoc cref="HandlerSetHandlerEnd(ILLabel, IWeaverExceptionHandler)"/>
    public ILWeaver HandlerSetHandlerEnd(Instruction handlerEnd, IWeaverExceptionHandler handler)
    {
        handler.HandlerEnd = Context.DefineLabel(handlerEnd);
        return this;
    }

    /// <summary>
    /// Writes the leave instructions for try, catch or finally blocks and applies the
    /// provided <see cref="IWeaverExceptionHandler"/> to the method body.
    /// </summary>
    /// <remarks>
    /// Once applied, the leave label of the handler leave instructions will point to the
    /// instruction that comes after what was set as HandlerEnd. Make sure that once you have
    /// applied the <see cref="IWeaverExceptionHandler"/>, you are not inserting instructions before
    /// the HandlerEnd or you'll need to retarget the leave label to your first inserted instruction
    /// before the EndHandler.
    /// </remarks>
    /// <param name="handler">The handler to apply.</param>
    /// <returns>This <see cref="ILWeaver"/>.</returns>
    /// <exception cref="NullReferenceException"></exception>
    public ILWeaver HandlerApply(IWeaverExceptionHandler handler)
    {
        if (handler.TryStart is null)
            throw new NullReferenceException("TryStart was not set!");
        if (handler.HandlerEnd is null)
            throw new NullReferenceException("HandlerEnd was not set!");

        if (handler.TryStart.InteropGetTarget() is null)
            throw new NullReferenceException("TryStart target was not set!");
        if (handler.HandlerEnd.InteropGetTarget() is null)
            throw new NullReferenceException("HandlerEnd target was not set!");

        ExceptionHandler cecilHandler = new(
            handler switch
            {
                WeaverExceptionFilterHandler => ExceptionHandlerType.Filter,
                WeaverExceptionCatchHandler => ExceptionHandlerType.Catch,
                WeaverExceptionFinallyHandler => ExceptionHandlerType.Finally,
                WeaverExceptionFaultHandler => ExceptionHandlerType.Fault,
                _ => throw new Exception("Unsupported exception handler type."),
            }
        )
        {
            CatchType = (handler as WeaverExceptionCatchHandler)?.CatchType,
            TryStart = handler.TryStart.InteropGetTarget(),
            TryEnd = handler.TryEnd?.InteropGetTarget(),
            FilterStart = (
                handler as WeaverExceptionFilterHandler
            )?.FilterStart?.InteropGetTarget(),
            HandlerStart = handler.HandlerStart?.InteropGetTarget()!,
            HandlerEnd = handler.HandlerEnd.InteropGetTarget()!,
        };

        bool isFilter = cecilHandler.HandlerType == ExceptionHandlerType.Filter;

        if (cecilHandler.TryEnd is not null && cecilHandler.TryEnd == cecilHandler.HandlerStart)
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
        if (cecilHandler.TryEnd is null)
        {
            if (isFilter)
            {
                if (cecilHandler.FilterStart is null)
                    throw new NullReferenceException("FilterStart was not set!");

                cecilHandler.TryEnd = cecilHandler.FilterStart;
            }
            else
            {
                if (cecilHandler.HandlerStart is null)
                    throw new NullReferenceException(
                        "TryEnd and HandlerStart were not set!"
                            + " Note that only one of then needs to be set."
                    );

                cecilHandler.TryEnd = cecilHandler.HandlerStart;
            }
        }
        else
        {
            // inclusive range → exclusive
            cecilHandler.TryEnd = cecilHandler.TryEnd.Next;
        }

        if (cecilHandler.HandlerStart is null)
        {
            if (isFilter)
            {
                throw new NullReferenceException(
                    "HandlerStart must be set when HandlerType is Filter!"
                );
            }
            else
            {
                cecilHandler.HandlerStart = cecilHandler.TryEnd;
            }
        }

        if (cecilHandler.HandlerEnd.Next is null)
        {
            GhostInsertAfter(cecilHandler.HandlerEnd, Create(OpCodes.Nop));
        }
        // inclusive range → exclusive
        cecilHandler.HandlerEnd = cecilHandler.HandlerEnd.Next!;

        // Now we can start inserting all our instructions.
        ILLabel leaveDestination = Context.DefineLabel(cecilHandler.HandlerEnd);

        // And emit the actual leave instructions.
        // Try should have a normal leave instruction or nothing if it throws.
        if (cecilHandler.TryEnd.Previous.OpCode != OpCodes.Throw)
        {
            GhostInsertBefore(cecilHandler.TryEnd, Create(OpCodes.Leave, leaveDestination));
        }

        // If we have a filter, aka: catch (Exception ex) when (/* statement */)
        // then we need the endfilter instruction.
        if (isFilter)
        {
            if (cecilHandler.FilterStart is null)
                throw new NullReferenceException("FilterStart was not set!");

            // FilterEnd doesn't exist, it's implicitly before HandlerStart.
            GhostInsertBefore(cecilHandler.HandlerStart, Create(OpCodes.Endfilter));
        }

        // Finally also has a special instruction.
        if (cecilHandler.HandlerType == ExceptionHandlerType.Finally)
        {
            GhostInsertBefore(cecilHandler.HandlerEnd, Create(OpCodes.Endfinally));
        }
        else
        {
            // For anything other than finally, use a normal leave instruction.
            GhostInsertBefore(cecilHandler.HandlerEnd, Create(OpCodes.Leave, leaveDestination));
        }

        // Body.Method.RecalculateILOffsets();
        // Console.WriteLine("handler.TryStart:     " + cecilHandler.TryStart);
        // Console.WriteLine("handler.TryEnd:       " + cecilHandler.TryEnd);
        // Console.WriteLine("handler.HandlerStart: " + cecilHandler.HandlerStart);
        // Console.WriteLine("handler.HandlerEnd:   " + cecilHandler.HandlerEnd);
        // Console.WriteLine("handler.CatchType:    " + cecilHandler.CatchType?.ToString());
        // Console.WriteLine("handler.HandlerType:  " + cecilHandler.HandlerType.ToString());
        // Console.WriteLine(Context);

        Context.Body.ExceptionHandlers.Add(cecilHandler);
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
    /// For use in <see cref="MatchRelaxed(Predicate{Instruction}[])"/> and other variations,
    /// use <see cref="SetCurrentTo"/> as that method returns true.
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
    /// for use in <see cref="MatchRelaxed(Predicate{Instruction}[])"/> and other variations.
    /// </summary>
    /// <param name="instruction">The instruction to set as current.</param>
    /// <returns>true</returns>
    public bool SetCurrentTo(Instruction instruction)
    {
        // We might match 'original' instructions that don't exist anymore.
        if (!Instructions.Contains(instruction))
        {
            return false;
        }

        CurrentTo(instruction);
        return true;
    }

    /// <summary>
    /// Set instruction to a target instruction and returns true
    /// for use in <see cref="MatchRelaxed(Predicate{Instruction}[])"/> and other variations.
    /// </summary>
    /// <param name="toBeSet">The instruction to be set.</param>
    /// <param name="target">The instruction toBeSet will be set to.</param>
    /// <returns>true</returns>
    public bool SetInstructionTo([NotNull] ref Instruction? toBeSet, Instruction target)
    {
        Helpers.ThrowIfNull(target);
        toBeSet = target;

        // We might match 'original' instructions that don't exist anymore.
        if (!Instructions.Contains(target))
        {
            return false;
        }

        return true;
    }

    public ILWeaver CurrentToNext() => CurrentTo(Current.Next);

    public ILWeaver CurrentToPrevious() => CurrentTo(Current.Previous);

    /// <summary>
    /// Attempts to match a set of predicates to find one specific
    /// location in the instructions. This method searches the entire target method
    /// to ensure the match predicates are matching exactly what was attempted to match.<br/>
    /// <br/>
    /// If the match fails, the match is attempted again against the "original" instructions
    /// of the method before it was manipulated. As such, you must NEVER offset
    /// <see cref="Current"/> to another predicate by index, as there may be instructions
    /// in between, or that instruction may not even exist in the current method body.<br/>
    /// <br/>
    /// If you want to match multiple locations, use
    /// <see cref="MatchMultipleRelaxed(Action{ILWeaver}, Predicate{Instruction}[])"/><br/>
    /// <br/>
    /// <example>
    /// In the following example we match two instructions, setting
    /// <see cref="Current"/> to the brtrue instruction which remains as the
    /// <see cref="Current"/> if the match is successful.
    /// <code>
    /// <![CDATA[
    /// weaver
    ///     .MatchRelaxed(
    ///         x => x.MatchLdloc(1),
    ///         x => x.MatchBrtrue(out _) && weaver.SetCurrentTo(x)
    ///     )
    ///     .ThrowIfFailure()
    ///     .EmitBeforeCurrent(weaver.Create(OpCodes.Call, GetCustomNumber));
    /// ]]>
    /// </code>
    /// </example>
    /// </summary>
    /// <param name="predicates">The predicates to match against.</param>
    /// <returns>An <see cref="ILWeaverResult"/> which can be used
    /// for checking if the match was a success or a failure.</returns>
    public ILWeaverResult MatchRelaxed(params Predicate<Instruction>[] predicates) =>
        MatchInternal(
            allowMultipleMatches: false,
            null,
            allowSecondPass: true,
            secondPassMatchOriginalInstructions: false,
            predicates
        );

    /// <summary>
    /// Attempts to match a set of predicates to find one specific
    /// location in the instructions. This method searches the entire target method
    /// to ensure the match predicates are matching exactly what was attempted to match.<br/>
    /// <br/>
    /// Only a 1:1 match of the current method instructions matching predicates in order
    /// are accepted unlike with <see cref="MatchRelaxed"/>. As such you should use that
    /// method instead to keep your match more compatible with other mods, unless if you
    /// really know what you are doing.<br/>
    /// <br/>
    /// If you want to match multiple locations, use
    /// <see cref="MatchMultipleStrict(Action{ILWeaver}, Predicate{Instruction}[])"/><br/>
    /// <br/>
    /// <example>
    /// In the following example we match two instructions, setting
    /// <see cref="Current"/> to the brtrue instruction which remains as the
    /// <see cref="Current"/> if the match is successful.
    /// <code>
    /// <![CDATA[
    /// weaver
    ///     .MatchStrict(
    ///         x => x.MatchLdloc(1),
    ///         x => x.MatchBrtrue(out _) && weaver.SetCurrentTo(x)
    ///     )
    ///     .ThrowIfFailure()
    ///     .EmitBeforeCurrent(weaver.Create(OpCodes.Call, GetCustomNumber));
    /// ]]>
    /// </code>
    /// </example>
    /// </summary>
    /// <inheritdoc cref="MatchRelaxed"/>
    public ILWeaverResult MatchStrict(params Predicate<Instruction>[] predicates) =>
        MatchInternal(
            allowMultipleMatches: false,
            null,
            allowSecondPass: false,
            secondPassMatchOriginalInstructions: false,
            predicates
        );

    /// <summary>
    /// Attempts to match a set of predicates multiple times to find specific
    /// locations in the instructions.<br/>
    /// <br/>
    /// If the match fails, the match is attempted again against the "original" instructions
    /// of the method before it was manipulated. As such, you must NEVER offset
    /// <see cref="Current"/> to another predicate by index, as there may be instructions
    /// in between, or that instruction may not even exist in the current method body.<br/>
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
    ///     .MatchMultipleRelaxed(
    ///         onMatch: matchWeaver =>
    ///         {
    ///             matchWeaver.EmitBeforeCurrent(
    ///                 matchWeaver.Create(OpCodes.Call, GetCustomNumber)
    ///             );
    ///         },
    ///         x => x.MatchLdloc(1),
    ///         x => x.MatchBrtrue(out _) && weaver.SetCurrentTo(x)
    ///     )
    ///     .ThrowIfFailure();
    /// ]]>
    /// </code>
    /// </example>
    /// </summary>
    /// <param name="onMatch">A delegate which runs for each match, passing a copy of the
    /// original <see cref="ILWeaver"/> with the <see cref="Current"/> pointing to the one
    /// at the time of the match.</param>
    /// <inheritdoc cref="MatchRelaxed"/>
    public ILWeaverResult MatchMultipleRelaxed(
        Action<ILWeaver> onMatch,
        params Predicate<Instruction>[] predicates
    ) =>
        MatchInternal(
            allowMultipleMatches: true,
            onMatch,
            allowSecondPass: true,
            secondPassMatchOriginalInstructions: false,
            predicates
        );

    /// <summary>
    /// Attempts to match a set of predicates multiple times to find specific
    /// locations in the instructions.<br/>
    /// <br/>
    /// Only a 1:1 match of the current method instructions matching predicates in order
    /// are accepted unlike with <see cref="MatchMultipleRelaxed"/>. As such you should use that
    /// method instead to keep your match more compatible with other mods, unless if you
    /// really know what you are doing.<br/>
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
    ///     .MatchMultipleStrict(
    ///         onMatch: matchWeaver =>
    ///         {
    ///             matchWeaver.EmitBeforeCurrent(
    ///                 matchWeaver.Create(OpCodes.Call, GetCustomNumber)
    ///             );
    ///         },
    ///         x => x.MatchLdloc(1),
    ///         x => x.MatchBrtrue(out _) && weaver.SetCurrentTo(x)
    ///     )
    ///     .ThrowIfFailure();
    /// ]]>
    /// </code>
    /// </example>
    /// </summary>
    /// <inheritdoc cref="MatchMultipleRelaxed(Action{ILWeaver}, Predicate{Instruction}[])"/>
    public ILWeaverResult MatchMultipleStrict(
        Action<ILWeaver> onMatch,
        params Predicate<Instruction>[] predicates
    ) =>
        MatchInternal(
            allowMultipleMatches: true,
            onMatch,
            allowSecondPass: false,
            secondPassMatchOriginalInstructions: false,
            predicates
        );

    ILWeaverResult MatchInternal(
        bool allowMultipleMatches,
        Action<ILWeaver>? onMatched,
        bool allowSecondPass,
        bool secondPassMatchOriginalInstructions,
        params Predicate<Instruction>[] predicates
    )
    {
        Helpers.ThrowIfNull(predicates);

        if (allowMultipleMatches)
            Helpers.ThrowIfNull(onMatched);

        Instruction originalCurrent = Current;
        Instruction singleMatchCurrent = Current;

        List<int> matchedIndexes = [];
        List<(int count, int indexBeforeFailed)> bestAttempts = [(0, 0)];

        IList<Instruction> instructions = secondPassMatchOriginalInstructions
            ? ManipulationInfo.OriginalInstructions
            : Instructions;

        int predicatesMatched = 0;
        for (int i = 0; i < instructions.Count; i++)
        {
            if (!predicates[predicatesMatched](instructions[i]))
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
            else
            {
                // Capture Current because it's probably going
                // to be overridden when testing against the rest
                // of the instructions.
                singleMatchCurrent = Current;
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

            if (allowSecondPass && !secondPassMatchOriginalInstructions)
            {
                var secondPassResult = MatchInternal(
                    allowMultipleMatches,
                    onMatched,
                    allowSecondPass,
                    secondPassMatchOriginalInstructions: true,
                    predicates
                );

                if (secondPassResult.IsValid)
                {
                    LogFailure(GetResult().FailureMessage!);
                    return new ILWeaverResult(this, null);
                }
            }
        }
        else
        {
            if (matchedIndexes.Count == 1)
            {
                Current = singleMatchCurrent;
                return new ILWeaverResult(this, null);
            }

            if (allowSecondPass && !secondPassMatchOriginalInstructions)
            {
                var secondPassResult = MatchInternal(
                    allowMultipleMatches,
                    onMatched,
                    allowSecondPass,
                    secondPassMatchOriginalInstructions: true,
                    predicates
                );

                if (secondPassResult.IsValid)
                {
                    LogFailure(GetResult().FailureMessage!);
                    return new ILWeaverResult(this, null);
                }
                else
                {
                    CurrentTo(originalCurrent);
                }
            }
        }

        void LogFailure(string failureMessage)
        {
            this.Log(
                MonoDetourLogger.LogChannel.Warning,
                "Match succeeded against 'original' instructions as a fallback "
                    + "but failed against current ones. This ILHook still probably works.\n"
                    + "Here's what went wrong when matching against current instructions:\n"
                    + failureMessage
            );
        }

        string GetMatchedTooManyError()
        {
            CodeBuilder err = new(new StringBuilder(), 2);

            err.WriteLine(
                    $"- {nameof(ILWeaver)}.{nameof(MatchRelaxed)} matched all predicates more than once in the target method."
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

            err.RemoveIndent()
                .WriteLine()
                .WriteLine("This message originates from:")
                .WriteLine(new StackTrace().ToString());

            return err.ToString();
        }

        string GetNotMatchedAllError()
        {
            CodeBuilder err = new(new StringBuilder(), 2);

            err.Write(
                    $"{nameof(ILWeaver)}.{nameof(MatchRelaxed)} couldn't match all predicates for method: "
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
                err.WriteLine().WriteLine("(first predicate was never matched)");
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

            err.RemoveIndent()
                .WriteLine()
                .WriteLine("This message originates from:")
                .WriteLine(new StackTrace().ToString());

            return err.ToString();
        }

        ILWeaverResult GetResult()
        {
            if (matchedIndexes.Count > 0)
            {
                return new ILWeaverResult(this, GetMatchedTooManyError);
            }

            return new ILWeaverResult(this, GetNotMatchedAllError);
        }

        return GetResult();
    }

    ILWeaver InsertAtInternal(int index, Instruction instruction, bool insertAfterIndex)
    {
        Helpers.ThrowIfNull(instruction);

        if (index == -1)
        {
            throw new IndexOutOfRangeException(
                $"The index -1 or target instruction to be inserted at does not exist in the method body."
            );
        }
        else if (index > Instructions.Count)
        {
            throw new IndexOutOfRangeException(
                $"The index to be inserted is out of range; index: {index} / instructions: {Instructions.Count}"
            );
        }

        Instruction instructionAtIndex = Instructions[index];

        // When inserting before a target instruction that is inside handler ranges,
        // include the inserted instruction inside the start range.
        if (!insertAfterIndex)
        {
            foreach (var eh in Body.ExceptionHandlers)
            {
                if (eh.TryStart == instructionAtIndex)
                    eh.TryStart = instruction;
                if (eh.HandlerStart == instructionAtIndex)
                    eh.HandlerStart = instruction;
                if (eh.FilterStart == instructionAtIndex)
                    eh.FilterStart = instruction;
                // Handler end ranges is exclusive, so we most likely
                // want our instruction to become the new end
                if (eh.TryEnd == instructionAtIndex)
                    eh.TryEnd = instruction;
                if (eh.HandlerEnd == instructionAtIndex)
                    eh.HandlerEnd = instruction;
            }
        }
        // TODO: The following is actually terrible default behavior because
        // hander end ranges are exclusive (end instruction is after leave instruction).
        // Should this even be an option?
        // // In this case we are inserting after a target instruction,
        // // so we want our instruction to be inside the end range.
        // // else
        // // {
        // //     foreach (var eh in Body.ExceptionHandlers)
        // //     {
        // //         if (eh.TryEnd == InstructionAtIndex)
        // //             eh.TryEnd = instruction;
        // //         if (eh.HandlerEnd == InstructionAtIndex)
        // //             eh.HandlerEnd = instruction;
        // //     }
        // // }

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
    /// Insert instructions without attracting <see cref="pendingFutureNextInsertLabels"/>.
    /// </summary>
    ILWeaver GhostInsertBefore(Instruction target, Instruction instruction)
    {
        Instructions.Insert(Instructions.IndexOf(target), instruction);
        return this;
    }

    /// <summary>
    /// Insert instructions without attracting <see cref="pendingFutureNextInsertLabels"/>.
    /// </summary>
    ILWeaver GhostInsertAfter(Instruction target, Instruction instruction)
    {
        Instructions.Insert(Instructions.IndexOf(target) + 1, instruction);
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
    ) => InsertBefore(Instructions.IndexOf(target), instructions);

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
        InsertAfter(Instructions.IndexOf(target), instructions);

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
        ILLabel[] labels = new ILLabel[targets.Length];
        for (int i = 0; i < targets.Length; i++)
        {
            MarkLabelTo(targets[i], out var label);
            labels[i] = label;
        }
        return IL.Create(opcode, targets);
    }

    /// <inheritdoc cref="Create(OpCode, ParameterDefinition)"/>
    public Instruction Create(OpCode opcode, Instruction target)
    {
        MarkLabelTo(target, out var label);
        return IL.Create(opcode, label);
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
}
