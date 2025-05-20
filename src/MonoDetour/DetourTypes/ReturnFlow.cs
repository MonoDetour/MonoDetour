namespace MonoDetour.DetourTypes;

/// <summary>
/// Specifies how the return value affects control flow.
/// </summary>
public enum ReturnFlow
{
    /// <summary>
    /// Signifies that this return doesn't affect control flow.
    /// </summary>
    None = 0,

    /// <summary>
    /// Skips the original method body and prefixes,
    /// but doesn't skip postfixes.
    /// </summary>
    SkipOriginal = 1,

    /// <summary>
    /// Returns immediately and doesn't run any
    /// hooks written to the method.
    /// </summary>
    HardReturn = 2,
}
