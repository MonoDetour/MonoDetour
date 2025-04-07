namespace MonoDetour;

/// <summary>
/// The basic detour types implemented by MonoDetour. See <see cref="IMonoDetourHookEmitter"/>
/// for implementing custom detour types.
/// </summary>
public enum DetourType
{
    /// <summary>
    /// This hook will run at the start of the target method.<br/>
    /// Implementation is <see cref="PrefixDetour"/>.
    /// </summary>
    Prefix = 1,

    /// <summary>
    /// This hook will run at the end of the target method.<br/>
    /// Implementation is <see cref="PostfixDetour"/>.
    /// </summary>
    Postfix = 2,

    /// <summary>
    /// This is a regular <see cref="MonoMod.RuntimeDetour.ILHook"/>
    /// which supports modifying the target method on the CIL level.<br/>
    /// Implementation is <see cref="ILHookDetour"/>.
    /// </summary>
    ILHook = 3,
}
