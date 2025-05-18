using System.Reflection;
using MonoMod.Cil;
using MonoMod.Utils;

namespace MonoDetour.DetourTypes;

/// <summary>
/// A manipulation info containing the <see cref="ILContext"/> intended for manipulation
/// and an untouched <see cref="ILContext"/> useful for observing the untouched
/// state, including the original method as a <see cref="MethodBase"/>.
/// </summary>
/// <param name="il">The main <see cref="ILContext"/> for manipulation.</param>
/// <param name="original">The original method.</param>
public class ILManipulationInfo(ILContext il, MethodBase original)
{
    /// <summary>
    /// The original method.
    /// </summary>
    public MethodBase Original { get; } = original;

    /// <inheritdoc cref="ILContext"/>
    public ILContext ManipulationContext { get; } = il;

    /// <summary>
    /// Similar to <see cref="ManipulationContext"/> except this is untouched and won't
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
