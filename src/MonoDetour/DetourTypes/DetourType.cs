namespace MonoDetour.DetourTypes;

/// <summary>
/// The basic detour types implemented by MonoDetour. See <see cref="IMonoDetourHookApplier"/>
/// for implementing custom detour types.
/// </summary>
public enum DetourType
{
    /// <summary>
    /// This hook will run at the start of the target method.<br/>
    /// Implementation class is <see cref="DetourTypes.PrefixDetour"/>.
    /// </summary>
    PrefixDetour = 1,

    /// <summary>
    /// This hook will run at the end of the target method.<br/>
    /// Implementation class is <see cref="DetourTypes.PostfixDetour"/>.
    /// </summary>
    PostfixDetour = 2,

    /// <summary>
    /// This is a regular <see cref="MonoMod.RuntimeDetour.ILHook"/>
    /// which supports modifying the target method on the CIL level.<br/>
    /// Implementation class is <see cref="DetourTypes.ILHookDetour"/>.
    /// </summary>
    ILHookDetour = 3,
}
