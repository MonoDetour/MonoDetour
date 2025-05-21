namespace MonoDetour.DetourTypes;

/// <summary>
/// Specifies how the return value affects control flow.
/// </summary>
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
    SkipOriginal = 1,

    /// <summary>
    /// Returns immediately and doesn't run any
    /// hooks written to the method.
    /// </summary>
    HardReturn = 2,
}
