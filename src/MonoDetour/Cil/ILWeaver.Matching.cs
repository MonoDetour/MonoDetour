using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoDetour.Cil.Analysis;
using MonoDetour.Logging;
using MonoMod.Cil;
using MonoMod.SourceGen.Internal;
using MonoMod.Utils;

namespace MonoDetour.Cil;

public partial class ILWeaver
{
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
    /// Set <see cref="Current"/> to a target instruction like
    /// <see cref="CurrentTo(Instruction)"/>, except for use in
    /// <see cref="MatchRelaxed(Predicate{Instruction}[])"/> and other variations.
    /// </summary>
    /// <param name="instruction">The instruction to set as current.</param>
    /// <returns>Whether or not the <paramref name="instruction"/> exists in
    /// the current method body.</returns>
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
    /// Set instruction to a target instruction for use in
    /// <see cref="MatchRelaxed(Predicate{Instruction}[])"/> and other variations.
    /// </summary>
    /// <param name="toBeSet">The instruction to be set.</param>
    /// <param name="target">The instruction toBeSet will be set to.</param>
    /// <returns>Whether or not the <paramref name="target"/> instruction exists in
    /// the current method body.</returns>
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

    /// <summary>
    /// Sets <see cref="Current"/> to the instruction after it.
    /// </summary>
    /// <returns>This <see cref="ILWeaver"/>.</returns>
    public ILWeaver CurrentToNext() => CurrentTo(Current.Next);

    /// <summary>
    /// Sets <see cref="Current"/> to the instruction before it.
    /// </summary>
    /// <returns>This <see cref="ILWeaver"/>.</returns>
    public ILWeaver CurrentToPrevious() => CurrentTo(Current.Previous);

    /// <summary>
    /// Gets the first reachable instruction backward whose incoming stack size is 0,
    /// without any branching.
    /// </summary>
    /// <param name="start">The instruction to start searching at.</param>
    /// <param name="informationalBody">
    /// The <see cref="IInformationalMethodBody"/> whose
    /// <see cref="IInformationalInstruction"/>s' stack size to check.
    /// An <see cref="IInformationalMethodBody"/> should not be reused after
    /// the method has been modified.
    /// </param>
    public Instruction GetStackSizeZeroBeforeContinuous(
        Instruction start,
        IInformationalMethodBody? informationalBody = null
    )
    {
        informationalBody ??= Body.CreateInformationalSnapshotEvaluateAll();
        var infoStart = informationalBody.GetInformationalInstruction(start);

        var enumerable = infoStart;
        while (true)
        {
            if (enumerable is { IsEvaluated: true, IncomingStackSize: <= 0 })
                break;

            enumerable = enumerable.Previous;

            if (enumerable is null)
            {
                // This should be unreachable because the first instruction in a method
                // is always reachable and has incoming stack size 0.
                throw new NullReferenceException("There was no previous empty stack.");
            }
        }

        return enumerable.Instruction;
    }

    /// <summary>
    /// Gets the first reachable instruction forward whose stack size is 0,
    /// without any branching.
    /// </summary>
    /// <param name="start">The instruction to start searching at.</param>
    /// <exception cref="NullReferenceException"></exception>
    /// <inheritdoc cref="GetStackSizeZeroBeforeContinuous(Instruction, IInformationalMethodBody?)"/>
    /// <param name="informationalBody"/>
    public Instruction GetStackSizeZeroAfterContinuous(
        Instruction start,
        IInformationalMethodBody? informationalBody = null
    )
    {
        informationalBody ??= Body.CreateInformationalSnapshotEvaluateAll();
        var infoStart = informationalBody.GetInformationalInstruction(start);

        var enumerable = infoStart;
        while (true)
        {
            if (enumerable is { IsEvaluated: true, StackSize: <= 0 })
                break;

            enumerable =
                enumerable.Next
                ?? throw new NullReferenceException("There was no next empty stack.");
        }

        return enumerable.Instruction;
    }

    /// <summary>
    /// Gets the both the first reachable instruction backward whose incoming stack size is 0,
    /// and the first instruction forward whose stack size is 0, without any branching.
    /// </summary>
    /// <param name="start"></param>
    /// <param name="informationalBody"></param>
    /// <returns>A tuple with the start and end instructions.</returns>
    /// <inheritdoc cref="GetStackSizeZeroBeforeContinuous(Instruction, IInformationalMethodBody?)"/>
    public (Instruction start, Instruction end) GetStackSizeZeroAreaContinuous(
        Instruction start,
        IInformationalMethodBody? informationalBody = null
    )
    {
        informationalBody ??= Body.CreateInformationalSnapshotEvaluateAll();
        var before = GetStackSizeZeroBeforeContinuous(start, informationalBody);
        var after = GetStackSizeZeroAfterContinuous(start, informationalBody);
        return (before, after);
    }

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
    ///     .InsertBeforeCurrent(weaver.Create(OpCodes.Call, GetCustomNumber));
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
            passType: MatchPassType.RelaxedAllowOriginalPass,
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
    ///     .InsertBeforeCurrent(weaver.Create(OpCodes.Call, GetCustomNumber));
    /// ]]>
    /// </code>
    /// </example>
    /// </summary>
    /// <inheritdoc cref="MatchRelaxed"/>
    public ILWeaverResult MatchStrict(params Predicate<Instruction>[] predicates) =>
        MatchInternal(
            allowMultipleMatches: false,
            null,
            passType: MatchPassType.StrictNoOriginalPass,
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
    ///             matchWeaver.InsertBeforeCurrent(
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
    /// <param name="predicates"/>
    public ILWeaverResult MatchMultipleRelaxed(
        Action<ILWeaver> onMatch,
        params Predicate<Instruction>[] predicates
    ) =>
        MatchInternal(
            allowMultipleMatches: true,
            onMatch,
            passType: MatchPassType.RelaxedAllowOriginalPass,
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
    ///             matchWeaver.InsertBeforeCurrent(
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
            passType: MatchPassType.StrictNoOriginalPass,
            predicates
        );

    ILWeaverResult MatchInternal(
        bool allowMultipleMatches,
        Action<ILWeaver>? onMatched,
        MatchPassType passType,
        params Predicate<Instruction>[] predicates
    )
    {
        Helpers.ThrowIfNull(predicates);

        if (allowMultipleMatches)
            Helpers.ThrowIfNull(onMatched);

        Instruction originalCurrent = Current;

        List<int> matchedIndexes = [];
        List<Instruction> matchedInstructionsStart = [];
        List<(int count, int indexBeforeFailed)> bestAttempts = [(0, 0)];

        IList<Instruction> instructions =
            passType is MatchPassType.IsOriginalPass
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
            matchedInstructionsStart.Add(instructions[i - predicates.Length + 1]);

            // It's possible that the User didn't set Current in the matching predicates.
            // We don't want that to happen, but I don't like the idea of keeping state about
            // if Current was set, and it would only be caught at runtime anyways.
            //
            // I also don't like implicitly setting Current to the first matched predicate if
            // it wasn't set, as the user might not read the xml docs and never learns they can
            // set Current to any matched predicate.
            //
            // So I think the best solution is an analyzer that enforces setting Current.

            // if (allowMultipleMatches)
            // {
            //     We don't execute `onMatched` until everything is matched
            //     because otherwise we would potentially be matching against
            //     newly inserted instructions in an infinite loop.
            // }
        }

        if (allowMultipleMatches)
        {
            if (matchedIndexes.Count > 0)
            {
                // Re-evaluate predicates so all variables set in them
                // will be correct in the onMatched call.
                foreach (var matchedInstruction in matchedInstructionsStart)
                {
                    int startIndex = instructions.IndexOf(matchedInstruction);
                    int endIndex = startIndex + predicates.Length;

                    int predicateIndex = 0;
                    for (int i = startIndex; i < endIndex; i++)
                    {
                        predicates[predicateIndex](instructions[i]);
                        predicateIndex++;
                    }

                    onMatched!(Clone());
                }
                // When matching multiple, keep original for this weaver in any case.
                CurrentTo(originalCurrent);
                return new ILWeaverResult(this, null);
            }

            CurrentTo(originalCurrent);

            if (passType is MatchPassType.RelaxedAllowOriginalPass)
            {
                var secondPassResult = MatchInternal(
                    allowMultipleMatches,
                    onMatched,
                    passType: MatchPassType.IsOriginalPass,
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
                // Re-evaluate predicates also when matching once
                var matchedInstruction = matchedInstructionsStart.First();

                int startIndex = instructions.IndexOf(matchedInstruction);
                int endIndex = startIndex + predicates.Length;

                int predicateIndex = 0;
                for (int i = startIndex; i < endIndex; i++)
                {
                    predicates[predicateIndex](instructions[i]);
                    predicateIndex++;
                }

                return new ILWeaverResult(this, null);
            }

            if (passType is MatchPassType.RelaxedAllowOriginalPass)
            {
                var secondPassResult = MatchInternal(
                    allowMultipleMatches,
                    onMatched,
                    passType: MatchPassType.IsOriginalPass,
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
            Method.RecalculateILOffsets();

            CodeBuilder err = new(new StringBuilder(), 2);

            err.WriteLine(
                    $"{nameof(ILWeaver)} matched all predicates more than once in the target method."
                )
                .Write("Total matches: ")
                .WriteLine(matchedIndexes.Count);

            int i = 0;
            foreach (var match in matchedIndexes)
            {
                i++;

                err.Write(i)
                    .Write(". At indexes: ")
                    .Write(match - predicates.Length + 1)
                    .Write(" to ")
                    .Write(match)
                    .Write(" (")
                    .Write(instructions[match - predicates.Length + 1].ToStringSafe())
                    .Write(" to ")
                    .Write(instructions[match].ToStringSafe())
                    .WriteLine(")");
            }

            err.WriteLine(
                    "Help: Add more predicates to find a unique match."
                        + $" Documentation: {gotoMatchingDocsLink}"
                )
                .WriteLine(
                    $"Info: Use {nameof(ILWeaver)}.MatchMultiple* methods if you intend to match multiple instances."
                );

            err.RemoveIndent()
                .WriteLine()
                .WriteLine("This message originates from:")
                .WriteLine(new StackTrace(true).ToString());

            return err.ToString();
        }

        string GetNotMatchedAllError()
        {
            Method.RecalculateILOffsets();

            CodeBuilder err = new(new StringBuilder(), 2);

            err.Write($"{nameof(ILWeaver)} couldn't match all predicates for method: ")
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
                var failedPredicate = predicates[bestAttempts[0].count];

                // It'd be helpful to know our predicate method contents, so we
                // take all method calls and use that. It's simple but effective enough.
                string? failedPredicateCallName = null;

                // If the predicate method isn't compiler generated, let's just use that.
                // This case is pretty rare though, but it's better to have it anyways.
#if NETSTANDARD2_0
                if (!failedPredicate.Method.Name.StartsWith("<", StringComparison.InvariantCulture))
#else
                if (!failedPredicate.Method.Name.StartsWith('<'))
#endif
                {
                    failedPredicateCallName = failedPredicate.Method.Name;
                }
                else
                {
                    using var dmd = new DynamicMethodDefinition(failedPredicate.Method);
                    List<string> methodCalls = [];

                    var calls = dmd.Definition.Body.Instructions.Where(x =>
                        x.MatchCallOrCallvirt(out _)
                    );

                    foreach (var call in calls)
                    {
                        if (call.Operand is not MethodReference method)
                            continue;

                        methodCalls.Add(method.Name);
                    }

                    if (methodCalls.Count > 0)
                    {
                        failedPredicateCallName = string.Join(", ", methodCalls);
                    }
                }
                err.Write("The first predicate which failed is at index ") // consists of the method calls: ")
                    .Write(bestAttempts[0].count)
                    .WriteLine(", failing to match any of the following instructions:");

                if (failedPredicateCallName is not null)
                {
                    err.Write("(The predicate consists of the method calls: ")
                        .Write(failedPredicateCallName)
                        .WriteLine(')');
                }

                for (int i = 0; i < bestAttempts.Count; i++)
                {
                    var (count, indexBeforeFailed) = bestAttempts[i];
                    var nextInstruction = instructions[indexBeforeFailed + 1];

                    err.RemoveIndent()
                        .WriteLine()
                        .Write(i + 1)
                        .Write(". Matched a range: (")
                        .Write(indexBeforeFailed - count + 1)
                        .Write(" to ")
                        .Write(indexBeforeFailed)
                        .Write(" | ")
                        .Write(instructions[indexBeforeFailed - count + 1].ToStringSafe())
                        .Write(" to ")
                        .Write(instructions[indexBeforeFailed].ToStringSafe())
                        .WriteLine(") but failed to match the next instruction:")
                        .IncreaseIndent()
                        .Write(' ')
                        .Write(indexBeforeFailed + 1)
                        .Write(' ')
                        .WriteLine(nextInstruction.ToStringSafe());
                }
            }

            err.RemoveIndent()
                .WriteLine(
                    $"\nHelp: Use MonoMod's {nameof(ILPatternMatchingExt)} extension methods."
                        + "\nThe general format is 'Match{OpCode}' and the methods have overloads"
                        + " for matching just the OpCode or also the Operand.\n"
                        + "  Example 1: x => x.MatchLdarg(0) // Match Ldarg with Operand 0\n"
                        + "  Example 2: x => x.MatchLdfld(out _) // Match Ldfld with any Operand"
                        + " and discard 'out' Operand value\n"
                        + "  Example 3: x => x.MatchBrtrue(out branchTarget) // Match Brtrue and"
                        + " store Operand value to a local variable\n"
                        + "  Example 4: x => x.MatchCall(out var method) && method.Name.StartsWith(\"<Foo>\")"
                        + " // Match Call with any method starting with '<Foo>'\n\n"
                        + "This message originates from:"
                )
                .WriteLine(new StackTrace(true).ToString());

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
}
