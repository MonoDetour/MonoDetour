using MonoMod.Cil;

namespace MonoDetour.DetourTypes;

/// <summary>
/// Implements MonoDetour support for a Hook that runs after an IEnumerator method
/// has finished enumerating.
/// </summary>
public class IEnumeratorPostfixDetour : IMonoDetourHookApplier
{
    /// <inheritdoc/>
    public MonoDetourHook Hook { get; set; } = null!;

    /// <inheritdoc/>
    public void ApplierManipulator(ILContext il) => GeneralIEnumeratorDetour.Manipulator(il, Hook);
}
