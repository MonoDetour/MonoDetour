namespace MonoDetour.DetourTypes;

/// <summary>
/// Specifies how the return value affects control flow.
/// </summary>
/// <remarks>
/// WARNING: Modifying the control flow of a method in a hook should NOT
/// be done if you are copy pasting logic from the original method.
/// This will <i>destroy</i> compatibility with ILHooks applied to the target method.
/// In such a case, ALWAYS use an <see cref="ILHookDetour"/> to modify the
/// CIL instructions of the method directly.<br/>
/// <br/>
/// To learn more about ILHooking, see
/// https://monodetour.github.io/ilhooking/introduction/
/// </remarks>
public enum ReturnFlow
{
    /// <summary>
    /// Signifies that this return value doesn't affect control flow.
    /// </summary>
    None = 0,

    /// <summary>
    /// Skips the original method body,
    /// but doesn't skip prefixes and postfixes written to it.<br/>
    /// This is essentially the same as prefix return false in HarmonyX.
    /// </summary>
    /// <inheritdoc cref="ReturnFlow"/>
    SkipOriginal = 1,

    /// <summary>
    /// Returns immediately and doesn't run any
    /// hooks written to the method.
    /// </summary>
    /// <inheritdoc cref="ReturnFlow"/>
    HardReturn = 2,
}
