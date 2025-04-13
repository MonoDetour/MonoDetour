using MonoMod.Cil;

namespace MonoDetour.DetourTypes;

/// <summary>
/// A type which implements this interface can be used as a
/// detour type, meaning the type can be passed in as a parameter to
/// <see cref="MonoDetourInfo"/>.<br/>
/// <br/>
/// MonoDetour uses this to implement <see cref="PrefixDetour"/>,
/// <see cref="PostfixDetour"/> and <see cref="ILHookDetour"/>.
/// If none of the available detour types satisfy your needs,
/// you can implement your own. See any of the implemented detour types for reference.
/// </summary>
/// <remarks>
/// For MonoDetour to be able to use a type which implements this
/// interface, the type must have a parameterless constructor.
/// </remarks>
public interface IMonoDetourHookEmitter
{
    /// <summary>
    /// All the available metadata for the MonoDetour Hook.
    /// </summary>
    MonoDetourInfo Info { get; set; }

    /// <summary>
    /// The <see cref="ILContext.Manipulator"/> method that is called
    /// when the ILHook is applied.
    /// </summary>
    /// <param name="il">The <see cref="ILContext"/> passed for manipulating the target method.</param>
    void Manipulator(ILContext il);
}
