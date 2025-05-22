using System.Collections.ObjectModel;
using System.Reflection;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using MonoDetour.DetourTypes.Manipulation;
using MonoMod.Cil;
using MonoMod.Utils;

namespace MonoDetour.Cil;

/// <summary>
/// A manipulation info containing the <see cref="ILContext"/> intended for manipulation
/// and an untouched <see cref="ILContext"/> useful for observing the untouched
/// state, including the original method as a <see cref="MethodBase"/>.
/// </summary>
/// <param name="il">The main <see cref="ILContext"/> for manipulation.</param>
public class ILManipulationInfo(ILContext il)
{
    /// <summary>
    /// An IL manipulator method accepting a <see cref="ILManipulationInfo"/>.
    /// </summary>
    /// <param name="info">
    /// A manipulation info containing the <see cref="ILContext"/> intended for manipulation
    /// and an untouched <see cref="ILContext"/> useful for observing the untouched
    /// state, including the original method as a <see cref="MethodBase"/>.
    /// See <see cref="ILManipulationInfo"/> for more.
    /// </param>
    public delegate void Manipulator(ILManipulationInfo info);

    /// <summary>
    /// The original method.
    /// </summary>
    public MethodBase Original { get; } = HookTargetRecords.GetOriginalMethod(il.Method);

    /// <summary>
    /// The <see cref="ILContext"/> used for manipulating the target method.
    /// </summary>
    public ILContext Context { get; } = il;

    /// <summary>
    /// A list of the original instructions before the method was manipulated.
    /// </summary>
    /// <remarks>
    /// This list has the same instruction instances as the current <see cref="Context"/>,
    /// meaning some may have been modified. For unmanipulated instructions, see
    /// <see cref="UnmanipulatedContext"/>.
    /// </remarks>
    public ReadOnlyCollection<Instruction> OriginalInstructions { get; } =
        HookTargetRecords.GetOriginalInstructions(il.Method);

    /// <summary>
    /// Similar to <see cref="Context"/> except this is untouched and won't
    /// be used for anything as it is just the original method before anyone manipulated it.
    /// This is purely for observing the unmanipulated state of the original method.<br/>
    /// <br/>
    /// This can for example be used by an <see cref="ILContext"/> manipulation API like
    /// MonoDetour.ILWeaver to automagically resolve incompatibilities due to instruction
    /// matching where another mod might have inserted harmless instructions somewhere between.
    /// </summary>
    public ILContext UnmanipulatedContext =>
        _original ??= new ILContext(new DynamicMethodDefinition(Original).Definition);
    ILContext? _original;
}
