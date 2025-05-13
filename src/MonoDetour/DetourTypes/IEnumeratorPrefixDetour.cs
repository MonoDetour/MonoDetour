using MonoMod.Cil;

namespace MonoDetour.DetourTypes;

/// <summary>
/// Implements MonoDetour support for a Hook that runs before an IEnumerator method
/// has started enumerating.
/// </summary>
public class IEnumeratorPrefixDetour : IMonoDetourHookEmitter
{
    /// <inheritdoc/>
    public MonoDetourInfo Info { get; set; } = null!;

    /// <inheritdoc/>
    public void Manipulator(ILContext il) => GeneralIEnumeratorDetour.Manipulator(il, Info);
}
