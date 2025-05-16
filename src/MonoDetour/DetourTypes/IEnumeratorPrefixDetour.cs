using MonoMod.Cil;

namespace MonoDetour.DetourTypes;

/// <summary>
/// Implements MonoDetour support for a Hook that runs before an IEnumerator method
/// has started enumerating.
/// </summary>
public class IEnumeratorPrefixDetour : IMonoDetourHookApplier
{
    /// <inheritdoc/>
    public IReadOnlyMonoDetourHook Hook { get; set; } = null!;

    /// <inheritdoc/>
    public void ApplierManipulator(ILContext il) => GeneralIEnumeratorDetour.Manipulator(il, Hook);
}
