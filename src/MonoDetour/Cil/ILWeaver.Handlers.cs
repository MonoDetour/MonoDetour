using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil.Cil;
using MonoDetour.Interop.MonoModUtils;
using MonoMod.Cil;
using MonoMod.Utils;

namespace MonoDetour.Cil;

public partial class ILWeaver
{
    /// <summary>
    /// Create a new <see cref="IWeaverExceptionHandler"/> to be assigned its ranges.
    /// Note that certain values can be figured out implicitly such as HandlerStart when
    /// TryEnd is defined.<br/>
    /// <br/>
    /// The <see cref="IWeaverExceptionHandler"/> will automatically be applied to the method body
    /// after your manipulator method is finished.
    /// </summary>
    /// <remarks>
    /// See <see cref="WeaverExceptionCatchHandler"/> for more information about this handler type.
    /// </remarks>
    /// <param name="catchType">The types of Exceptions that should be catched.
    /// If left null, <c>Exception</c> is used.</param>
    /// <param name="handler">The created <see cref="IWeaverExceptionHandler"/> to be configured.</param>
    /// <returns>The new exception handler instance.</returns>
    public ILWeaver HandlerCreateCatch(Type? catchType, out WeaverExceptionCatchHandler handler)
    {
        handler = new(IL.Import(catchType ?? typeof(Exception)));
        pendingHandlers.Add(handler);
        return this;
    }

    /// <remarks>
    /// See <see cref="WeaverExceptionFilterHandler"/> for more information about this handler type.
    /// </remarks>
    /// <inheritdoc cref="HandlerCreateCatch(Type?, out WeaverExceptionCatchHandler)"/>
    public ILWeaver HandlerCreateFilter(Type? catchType, out WeaverExceptionFilterHandler handler)
    {
        handler = new(IL.Import(catchType ?? typeof(Exception)));
        pendingHandlers.Add(handler);
        return this;
    }

    /// <remarks>
    /// See <see cref="WeaverExceptionFinallyHandler"/> for more information about this handler type.
    /// </remarks>
    /// <inheritdoc cref="HandlerCreateCatch(Type?, out WeaverExceptionCatchHandler)"/>
    public ILWeaver HandlerCreateFinally(out WeaverExceptionFinallyHandler handler)
    {
        handler = new();
        pendingHandlers.Add(handler);
        return this;
    }

    /// <remarks>
    /// See <see cref="WeaverExceptionFaultHandler"/> for more information about this handler type.
    /// </remarks>
    /// <inheritdoc cref="HandlerCreateCatch(Type?, out WeaverExceptionCatchHandler)"/>
    public ILWeaver HandlerCreateFault(out WeaverExceptionFaultHandler handler)
    {
        handler = new();
        pendingHandlers.Add(handler);
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
    /// <param name="handler"/>
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
    /// <param name="handler"/>
    public ILWeaver HandlerSetFilterStart(ILLabel filterStart, WeaverExceptionFilterHandler handler)
    {
        handler.FilterStart = filterStart;
        return this;
    }

    /// <inheritdoc cref="HandlerSetFilterStart(ILLabel, WeaverExceptionFilterHandler)"/>
    public ILWeaver HandlerSetFilterStart(
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
    /// <param name="handler"/>
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
    /// <param name="handler"/>
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
    /// Wraps an area around <see cref="Current"/> where the stack size is non-zero
    /// to a try block, catching exceptions of type <paramref name="catchType"/>.
    /// The argument <paramref name="catchInstructions"/> is written as the catch block's instructions.<br/>
    /// <br/>
    /// After this method, <see cref="Current"/> will be on the instruction after
    /// the catch block.
    /// </summary>
    /// <inheritdoc cref="HandlerWrapTryCatchStackSizeNonZero(Type?, Instruction, out Instruction, IEnumerable{Instruction})"/>
    public ILWeaver HandlerWrapTryCatchStackSizeNonZeroOnCurrent(
        Type? catchType,
        params IEnumerable<Instruction> catchInstructions
    ) =>
        HandlerWrapTryCatchStackSizeNonZeroOnCurrent(
            catchType,
            () =>
            {
                InsertAfterCurrent(catchInstructions);
            }
        );

    /// <inheritdoc cref="HandlerWrapTryCatchStackSizeNonZeroOnCurrent(Type?, IEnumerable{Instruction})"/>
    public ILWeaver HandlerWrapTryCatchStackSizeNonZeroOnCurrent(
        Type? catchType,
        params IEnumerable<InstructionOrEnumerable> catchInstructions
    ) => HandlerWrapTryCatchStackSizeNonZeroOnCurrent(catchType, catchInstructions.Unwrap());

    /// <summary>
    /// Wraps an area around <see cref="Current"/> where the stack size is non-zero
    /// to a try block, catching exceptions of type <paramref name="catchType"/>.
    /// Catching logic can be written in the <paramref name="writeCatch"/> callback, where
    /// at the start <see cref="Current"/> is the inclusive end of the try block, and
    /// at the end <see cref="Current"/> should be the end of the catch block.<br/>
    /// <br/>
    /// After this method, <see cref="Current"/> will be on the instruction after
    /// the catch block.
    /// </summary>
    /// <inheritdoc cref="HandlerWrapTryCatchStackSizeNonZero(Type?, Instruction, out Instruction, Func{Instruction, Instruction})"/>
    public ILWeaver HandlerWrapTryCatchStackSizeNonZeroOnCurrent(
        Type? catchType,
        Action writeCatch
    ) =>
        HandlerWrapTryCatchStackSizeNonZero(
                catchType,
                Current,
                out var afterCatch,
                tryEnd =>
                {
                    CurrentTo(tryEnd);
                    writeCatch();
                    return Current;
                }
            )
            .CurrentTo(afterCatch);

    /// <summary>
    /// Wraps an area around <paramref name="origin"/> where the stack size is non-zero
    /// to a try block, catching exceptions of type <paramref name="catchType"/>.
    /// The argument <paramref name="catchInstructions"/> is written as the catch block's instructions.
    /// </summary>
    /// <param name="catchInstructions">The instructions to be written to the catch handler.</param>
    /// <inheritdoc cref="HandlerWrapTryCatchStackSizeNonZero(Type?, Instruction, out Instruction, Func{Instruction, Instruction})"/>
    /// <param name="catchType"/>
    /// <param name="origin"/>
    /// <param name="afterCatch"/>
    public ILWeaver HandlerWrapTryCatchStackSizeNonZero(
        Type? catchType,
        Instruction origin,
        out Instruction afterCatch,
        params IEnumerable<Instruction> catchInstructions
    )
    {
        HandlerWrapTryCatchStackSizeNonZero(
            catchType,
            origin,
            out afterCatch,
            tryEnd =>
            {
                InsertAfter(tryEnd, catchInstructions);
                return catchInstructions.Last();
            }
        );

        return this;
    }

    /// <inheritdoc cref="HandlerWrapTryCatchStackSizeNonZero(Type?, Instruction, out Instruction, IEnumerable{Instruction})"/>
    public ILWeaver HandlerWrapTryCatchStackSizeNonZero(
        Type? catchType,
        Instruction origin,
        out Instruction afterCatch,
        params IEnumerable<InstructionOrEnumerable> catchInstructions
    ) =>
        HandlerWrapTryCatchStackSizeNonZero(
            catchType,
            origin,
            out afterCatch,
            catchInstructions.Unwrap()
        );

    /// <summary>
    /// Wraps an area around <paramref name="origin"/> where the stack size is non-zero
    /// to a try block, catching exceptions of type <paramref name="catchType"/>.
    /// Catching logic can be written in the <paramref name="writeCatch"/> callback, where
    /// the <see cref="Instruction"/> parameter is the inclusive end of the try block, and
    /// the return value is the end of the catch block.
    /// </summary>
    /// <param name="catchType">The types of Exceptions that should be catched.
    /// If left null, <c>Exception</c> is used.</param>
    /// <param name="origin">The instruction around which the area
    /// where stack size is non-zero is selected.</param>
    /// <param name="afterCatch">The instruction outside the written catch handler.</param>
    /// <param name="writeCatch">The callback where catch logic is written.</param>
    /// <returns>This <see cref="ILWeaver"/>.</returns>
    public ILWeaver HandlerWrapTryCatchStackSizeNonZero(
        Type? catchType,
        Instruction origin,
        out Instruction afterCatch,
        Func<Instruction, Instruction> writeCatch
    )
    {
        HandlerCreateCatch(catchType, out var handler);

        var (tryStart, tryEnd) = GetStackSizeZeroAreaContinuous(origin);
        HandlerSetTryStart(tryStart, handler);
        HandlerSetTryEnd(tryEnd, handler);

        var handlerEnd = writeCatch(tryEnd);
        HandlerSetHandlerEnd(handlerEnd, handler);
        afterCatch = handlerEnd.Next;

        if (afterCatch is null)
        {
            afterCatch = Create(OpCodes.Nop);
            InsertAfter(handlerEnd, afterCatch);
        }

        HandlerApply(handler);

        return this;
    }

    /// <summary>
    /// Note: This method is automatically called for all unapplied handlers
    /// after your manipulator method is finished. This wasn't always the case,
    /// but it's unlikely you'll ever need to call this anymore.<br/>
    /// <br/>
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
        // We remove this from the list at the start of this method because
        // if we did so at the end, someone could try catch this method throwing
        // without any intention to apply the handler again.
        // If that were to happen, the handler would be tried to be applied again
        // after their manipulator has finished, and it would throw.
        // So if someone explicitly applies a handler and it fails,
        // they must apply it explicitly again after fixing it.
        pendingHandlers.Remove(handler);

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
}
