using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil.Cil;

namespace MonoDetour.Cil;

/// <summary>
/// A wrapper which represents <see cref="Instruction"/> or its <see cref="IEnumerable{T}"/>.<br/>
/// <br/>
/// You don't need to directly interact with this type; implicit type conversions are provided
/// which allow using e.g. <see cref="ILWeaver"/>'s Insert methods seamlessly while mixing up
/// <see cref="Instruction"/> and its <see cref="IEnumerable{T}"/> in parameters marked as
/// <see langword="params"/> <see cref="IEnumerable{T}"/> of <see cref="InstructionOrEnumerable"/>.
/// </summary>
/// <remarks>
/// This is a hacky workaround for the fact that C# does not have discriminated unions (as of C# 14).
/// </remarks>
public sealed class InstructionOrEnumerable : IEnumerable<Instruction>
{
    readonly IEnumerable<Instruction> _instructions;

    /// <inheritdoc cref="InstructionOrEnumerable"/>
    /// <param name="instructions">Value to wrap as <see cref="InstructionOrEnumerable"/>.</param>
    public InstructionOrEnumerable(IEnumerable<Instruction> instructions) =>
        _instructions = instructions;

    /// <inheritdoc cref="InstructionOrEnumerable"/>
    /// <param name="instruction">Value to wrap as <see cref="InstructionOrEnumerable"/>.</param>
    public InstructionOrEnumerable(Instruction instruction) => _instructions = [instruction];

    /// <inheritdoc cref="InstructionOrEnumerable(Instruction)"/>
    public static implicit operator InstructionOrEnumerable(Instruction instruction) =>
        new(instruction);

    /// <inheritdoc cref="InstructionOrEnumerable(IEnumerable{Instruction})"/>
    public static implicit operator InstructionOrEnumerable(Instruction[] instructions) =>
        new(instructions);

    /// <inheritdoc cref="InstructionOrEnumerable(IEnumerable{Instruction})"/>
    public static implicit operator InstructionOrEnumerable(List<Instruction> instructions) =>
        new(instructions);

    /// <summary>
    /// Cast wrapper which represents <see cref="Instruction"/> or its <see cref="IEnumerable{T}"/>
    /// to a collection of the specified type.
    /// </summary>
    /// <param name="instructions">The wrapper.</param>
    public static implicit operator Instruction[](InstructionOrEnumerable instructions) =>
        [.. instructions._instructions];

    /// <summary>
    /// Cast wrapper which represents <see cref="Instruction"/> or its <see cref="IEnumerable{T}"/>
    /// to a collection of the specified type.
    /// </summary>
    /// <param name="instructions">The wrapper.</param>
    public static implicit operator List<Instruction>(InstructionOrEnumerable instructions) =>
        [.. instructions._instructions];

    /// <inheritdoc/>
    public IEnumerator<Instruction> GetEnumerator() => _instructions.GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// Extension methods to help working with <see cref="InstructionOrEnumerable"/> types.
/// </summary>
public static class InstructionOrEnumerableExtensions
{
    /// <summary>
    /// Unwraps an <see cref="IEnumerable"/> of the wrapper type
    /// <see cref="InstructionOrEnumerable"/> which may contain an arbitrary
    /// amount of <see cref="Instruction"/>s per wrapper type
    /// into a flat <see cref="IEnumerable"/> of <see cref="Instruction"/>.
    /// </summary>
    /// <returns>A flat <see cref="IEnumerable"/> of <see cref="Instruction"/>.</returns>
    public static IEnumerable<Instruction> Unwrap(
        this IEnumerable<InstructionOrEnumerable> instructions
    ) => instructions.SelectMany(x => x);
}
