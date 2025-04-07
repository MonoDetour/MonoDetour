using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;
using InstrList = Mono.Collections.Generic.Collection<Mono.Cecil.Cil.Instruction>;

namespace MonoDetour;

public enum GotoType
{
    FirstPredicate,
    LastPredicate,
}

public class ILWeaver(ILContext il)
{
    public ILWeaver(ILWeaver weaver)
        : this(weaver.Context) { }

    /// <inheritdoc cref="ILContext"/>
    public ILContext Context { get; } = il;

    /// <inheritdoc cref="ILContext.IL"/>
    public ILProcessor IL => Context.IL;
    public InstrList Instructions => Context.Instrs;

    public Instruction Current
    {
        get => _current;
        set => ReplaceInstruction(value);
    }
    Instruction _current = il.Instrs[0];

    /// <summary>
    /// The index of the instruction on <see cref="Current"/>
    /// </summary>
    /// <remarks>
    /// A negative index will loop back.
    /// Setter uses <see cref="Goto(int)"/> which can throw.
    /// </remarks>
    /// <exception cref="IndexOutOfRangeException"></exception>
    public int Index
    {
        get => Context.IndexOf(Current);
        set => Goto(value);
    }

    internal ILWeaverAction? _pendingAction;

    const string gotoMatchingDocsLink = "<insert documentation link here>";

    /// <summary>
    /// Enumerates all labels which point to the current instruction (<c>label.Target == Current</c>)
    /// </summary>
    public IEnumerable<ILLabel> GetIncomingLabels() => Context.GetIncomingLabels(Current);

    public ILWeaver RetargetLabels(IEnumerable<ILLabel> labels, Instruction target)
    {
        foreach (var label in labels)
            label.Target = target;

        return this;
    }

    public ILWeaver ReplaceInstruction(Instruction instruction)
    {
        Remove(1, out var labels);
        InsertAndAdvance(instruction);
        RetargetLabels(labels, instruction);
        return this;
    }

    public ILWeaver Remove(int instructions, out IEnumerable<ILLabel> orphanedLabels)
    {
        var index = Index;

        Instruction? newTarget =
            index + instructions < Instructions.Count ? Instructions[index + instructions] : null;

        if (newTarget is null)
            throw new IndexOutOfRangeException(
                "Attempted to remove more instructions than there are available."
            );

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

    /// <summary>
    /// Move the cursor to a target index. See also <see cref="Goto(Instruction)"/>
    /// </summary>
    /// <remarks>
    /// A negative index will loop back.
    /// </remarks>
    /// <returns>this <see cref="ILWeaver"/></returns>
    /// <exception cref="IndexOutOfRangeException"></exception>
    public ILWeaver Goto(int index)
    {
        if (_pendingAction is not null)
            _pendingAction.Accept();

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

        Goto(instruction);
        return this;
    }

    /// <summary>
    /// Move the cursor to a target instruction. See also <see cref="Goto(int)"/>
    /// </summary>
    /// <returns>this <see cref="ILWeaver"/></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public ILWeaver Goto(Instruction instruction)
    {
        if (instruction is null)
            throw new ArgumentNullException(
                nameof(instruction),
                "Attempted to go to a null instruction."
            );

        Current = instruction;
        return this;
    }

    public ILWeaverAction GotoMatch(
        GotoType gotoType = GotoType.LastPredicate,
        params Predicate<Instruction>[] predicates
    ) => GotoMatchInternal(gotoType, allowMultipleMatches: false, predicates);

    ILWeaverAction GotoMatchInternal(
        GotoType gotoType = GotoType.LastPredicate,
        bool allowMultipleMatches = false,
        params Predicate<Instruction>[] predicates
    )
    {
        Helpers.ThrowIfNull(predicates);

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

            if (predicatesMatched == predicates.Length)
            {
                predicatesMatched = 0;
                matchedIndexes.Add(i);
            }
        }

        if (matchedIndexes.Count == 1 || (allowMultipleMatches && matchedIndexes.Count > 0))
            return new ILWeaverAction(this, null);

        CodeBuilder err = new(new StringBuilder());
        if (matchedIndexes.Count > 0)
        {
            string GetMatchedTooManyError()
            {
                err.WriteLine(
                        $"- {nameof(ILWeaver)}.{nameof(GotoMatch)} matched all predicates more than once in the target method."
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

            return new ILWeaverAction(this, GetMatchedTooManyError);
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
                    $"{nameof(ILWeaver)}.{nameof(GotoMatch)} couldn't match all predicates for method: "
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

        return new ILWeaverAction(this, GetNotMatchedAllError);
    }

    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="ArgumentException"></exception>
    ILWeaver InsertAndAdvance(Instruction instruction)
    {
        Instructions.Insert(Index, instruction);
        Goto(instruction.Next);
        return this;
    }

    /// <summary>
    /// Emit a new instruction at this cursor's current position.
    /// </summary>
    /// <param name="opcode">The instruction opcode.</param>
    /// <param name="parameter">The instruction operand.</param>
    /// <returns>this</returns>
    /// <inheritdoc cref="InsertAndAdvance"/>
    public ILWeaver EmitAndAdvance(OpCode opcode, ParameterDefinition parameter) =>
        InsertAndAdvance(IL.Create(opcode, parameter));

    /// <summary>
    /// Emit a new instruction at this cursor's current position.
    /// </summary>
    /// <param name="opcode">The instruction opcode.</param>
    /// <param name="variable">The instruction operand.</param>
    /// <returns>this</returns>
    /// <inheritdoc cref="InsertAndAdvance"/>
    public ILWeaver EmitAndAdvance(OpCode opcode, VariableDefinition variable) =>
        InsertAndAdvance(IL.Create(opcode, variable));

    /// <summary>
    /// Emit a new instruction at this cursor's current position.
    /// </summary>
    /// <param name="opcode">The instruction opcode.</param>
    /// <param name="targets">The instruction operand.</param>
    /// <returns>this</returns>
    /// <inheritdoc cref="InsertAndAdvance"/>
    public ILWeaver EmitAndAdvance(OpCode opcode, Instruction[] targets) =>
        InsertAndAdvance(IL.Create(opcode, targets));

    /// <summary>
    /// Emit a new instruction at this cursor's current position.
    /// </summary>
    /// <param name="opcode">The instruction opcode.</param>
    /// <param name="target">The instruction operand.</param>
    /// <returns>this</returns>
    /// <inheritdoc cref="InsertAndAdvance"/>
    public ILWeaver EmitAndAdvance(OpCode opcode, Instruction target) =>
        InsertAndAdvance(IL.Create(opcode, target));

    /// <summary>
    /// Emit a new instruction at this cursor's current position.
    /// </summary>
    /// <param name="opcode">The instruction opcode.</param>
    /// <param name="value">The instruction operand.</param>
    /// <returns>this</returns>
    /// <inheritdoc cref="InsertAndAdvance"/>
    public ILWeaver EmitAndAdvance(OpCode opcode, double value) =>
        InsertAndAdvance(IL.Create(opcode, value));

    /// <summary>
    /// Emit a new instruction at this cursor's current position.
    /// </summary>
    /// <param name="opcode">The instruction opcode.</param>
    /// <param name="value">The instruction operand.</param>
    /// <returns>this</returns>
    /// <inheritdoc cref="InsertAndAdvance"/>
    public ILWeaver EmitAndAdvance(OpCode opcode, float value) =>
        InsertAndAdvance(IL.Create(opcode, value));

    /// <summary>
    /// Emit a new instruction at this cursor's current position.
    /// </summary>
    /// <param name="opcode">The instruction opcode.</param>
    /// <param name="value">The instruction operand.</param>
    /// <returns>this</returns>
    /// <inheritdoc cref="InsertAndAdvance"/>
    public ILWeaver EmitAndAdvance(OpCode opcode, long value) =>
        InsertAndAdvance(IL.Create(opcode, value));

    /// <summary>
    /// Emit a new instruction at this cursor's current position.
    /// </summary>
    /// <param name="opcode">The instruction opcode.</param>
    /// <param name="value">The instruction operand.</param>
    /// <returns>this</returns>
    /// <inheritdoc cref="InsertAndAdvance"/>
    public ILWeaver EmitAndAdvance(OpCode opcode, sbyte value) =>
        InsertAndAdvance(IL.Create(opcode, value));

    /// <summary>
    /// Emit a new instruction at this cursor's current position.
    /// </summary>
    /// <param name="opcode">The instruction opcode.</param>
    /// <param name="value">The instruction operand.</param>
    /// <returns>this</returns>
    /// <inheritdoc cref="InsertAndAdvance"/>
    public ILWeaver EmitAndAdvance(OpCode opcode, byte value) =>
        InsertAndAdvance(IL.Create(opcode, value));

    /// <summary>
    /// Emit a new instruction at this cursor's current position.
    /// </summary>
    /// <param name="opcode">The instruction opcode.</param>
    /// <param name="value">The instruction operand.</param>
    /// <returns>this</returns>
    /// <inheritdoc cref="InsertAndAdvance"/>
    public ILWeaver EmitAndAdvance(OpCode opcode, string value) =>
        InsertAndAdvance(IL.Create(opcode, value));

    /// <summary>
    /// Emit a new instruction at this cursor's current position.
    /// </summary>
    /// <param name="opcode">The instruction opcode.</param>
    /// <param name="field">The instruction operand.</param>
    /// <returns>this</returns>
    /// <inheritdoc cref="InsertAndAdvance"/>
    public ILWeaver EmitAndAdvance(OpCode opcode, FieldReference field) =>
        InsertAndAdvance(IL.Create(opcode, field));

    /// <summary>
    /// Emit a new instruction at this cursor's current position.
    /// </summary>
    /// <param name="opcode">The instruction opcode.</param>
    /// <param name="site">The instruction operand.</param>
    /// <returns>this</returns>
    /// <inheritdoc cref="InsertAndAdvance"/>
    public ILWeaver EmitAndAdvance(OpCode opcode, CallSite site) =>
        InsertAndAdvance(IL.Create(opcode, site));

    /// <summary>
    /// Emit a new instruction at this cursor's current position.
    /// </summary>
    /// <param name="opcode">The instruction opcode.</param>
    /// <param name="type">The instruction operand.</param>
    /// <returns>this</returns>
    /// <inheritdoc cref="InsertAndAdvance"/>
    public ILWeaver EmitAndAdvance(OpCode opcode, TypeReference type) =>
        InsertAndAdvance(IL.Create(opcode, type));

    /// <summary>
    /// Emit a new instruction at this cursor's current position.
    /// </summary>
    /// <param name="opcode">The instruction opcode.</param>
    /// <returns>this</returns>
    /// <inheritdoc cref="InsertAndAdvance"/>
    public ILWeaver EmitAndAdvance(OpCode opcode) => InsertAndAdvance(IL.Create(opcode));

    /// <summary>
    /// Emit a new instruction at this cursor's current position.
    /// </summary>
    /// <param name="opcode">The instruction opcode.</param>
    /// <param name="value">The instruction operand.</param>
    /// <returns>this</returns>
    /// <inheritdoc cref="InsertAndAdvance"/>
    public ILWeaver EmitAndAdvance(OpCode opcode, int value) =>
        InsertAndAdvance(IL.Create(opcode, value));

    /// <summary>
    /// Emit a new instruction at this cursor's current position.
    /// </summary>
    /// <param name="opcode">The instruction opcode.</param>
    /// <param name="method">The instruction operand.</param>
    /// <returns>this</returns>
    /// <inheritdoc cref="InsertAndAdvance"/>
    public ILWeaver EmitAndAdvance(OpCode opcode, MethodReference method) =>
        InsertAndAdvance(IL.Create(opcode, method));

    /// <summary>
    /// Emit a new instruction at this cursor's current position.
    /// </summary>
    /// <param name="opcode">The instruction opcode.</param>
    /// <param name="field">The instruction operand.</param>
    /// <returns>this</returns>
    /// <inheritdoc cref="InsertAndAdvance"/>
    public ILWeaver EmitAndAdvance(OpCode opcode, FieldInfo field) =>
        InsertAndAdvance(IL.Create(opcode, field));

    /// <summary>
    /// Emit a new instruction at this cursor's current position.
    /// </summary>
    /// <param name="opcode">The instruction opcode.</param>
    /// <param name="method">The instruction operand.</param>
    /// <returns>this</returns>
    /// <inheritdoc cref="InsertAndAdvance"/>
    public ILWeaver EmitAndAdvance(OpCode opcode, MethodBase method) =>
        InsertAndAdvance(IL.Create(opcode, method));

    /// <summary>
    /// Emit a new instruction at this cursor's current position.
    /// </summary>
    /// <param name="opcode">The instruction opcode.</param>
    /// <param name="type">The instruction operand.</param>
    /// <returns>this</returns>
    /// <inheritdoc cref="InsertAndAdvance"/>
    public ILWeaver EmitAndAdvance(OpCode opcode, Type type) =>
        InsertAndAdvance(IL.Create(opcode, type));

    /// <summary>
    /// Emit a new instruction at this cursor's current position.
    /// </summary>
    /// <param name="opcode">The instruction opcode.</param>
    /// <param name="operand">The instruction operand.</param>
    /// <returns>this</returns>
    /// <inheritdoc cref="InsertAndAdvance"/>
    public ILWeaver EmitAndAdvance(OpCode opcode, object operand) =>
        InsertAndAdvance(IL.Create(opcode, operand));

    /// <summary>
    /// Emit a new instruction at this cursor's current position, accessing a given member.
    /// </summary>
    /// <typeparam name="T">The type in which the member is defined.</typeparam>
    /// <param name="opcode">The instruction opcode.</param>
    /// <param name="memberName">The accessed member name.</param>
    /// <returns>this</returns>
    /// <inheritdoc cref="InsertAndAdvance"/>
    /// <exception cref="NotSupportedException"></exception>
    public ILWeaver Emit<T>(OpCode opcode, string memberName) =>
        InsertAndAdvance(
            IL.Create(opcode, typeof(T).GetMember(memberName, (BindingFlags)(-1)).First())
        );
}

public class ILWeaverAction
{
    public ILWeaverAction(ILWeaver weaver, Func<string>? invalidActionMessage)
    {
        _weaver = weaver;
        _weaver._pendingAction = this;

        if (invalidActionMessage is not null)
        {
            IsInvalid = true;
            getInvalidActionMessage = invalidActionMessage;
        }
    }

    [MemberNotNullWhen(true, nameof(InvalidActionMessage))]
    public bool IsInvalid { get; }
    public string? InvalidActionMessage
    {
        get => invalidActionMessage ??= getInvalidActionMessage?.Invoke();
    }
    readonly Func<string>? getInvalidActionMessage;
    string? invalidActionMessage;
    readonly ILWeaver _weaver;

    /// <summary>
    /// Allows the ILWeaver instance to continue operating.<br/>
    /// </summary>
    /// <param name="throwIfInvalid">Throw when accepting an invalid action.</param>
    /// <returns>The ILWeaver.</returns>
    /// <exception cref="InvalidILWeaverActionException"></exception>
    public ILWeaver Accept(bool throwIfInvalid = true)
    {
        _weaver._pendingAction = null;

        if (throwIfInvalid && IsInvalid)
        {
            throw new InvalidILWeaverActionException(
                $"Invalid action was accepted.\n" + InvalidActionMessage
            );
        }

        return _weaver;
    }
}

[Serializable]
public class InvalidILWeaverActionException : Exception
{
    public InvalidILWeaverActionException() { }

    public InvalidILWeaverActionException(string message)
        : base(message) { }

    public InvalidILWeaverActionException(string message, Exception inner)
        : base(message, inner) { }

    protected InvalidILWeaverActionException(
        System.Runtime.Serialization.SerializationInfo info,
        System.Runtime.Serialization.StreamingContext context
    )
        : base(info, context) { }
}
