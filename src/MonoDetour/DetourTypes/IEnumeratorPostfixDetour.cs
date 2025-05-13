using MonoMod.Cil;

namespace MonoDetour.DetourTypes;

/// <summary>
/// Implements MonoDetour support for a Hook that runs after an IEnumerator method
/// has finished enumerating.
/// </summary>
public class IEnumeratorPostfixDetour : IMonoDetourHookEmitter
{
    /// <inheritdoc/>
    public MonoDetourInfo Info { get; set; } = null!;

    /// <inheritdoc/>
    public void Manipulator(ILContext il) => GeneralIEnumeratorDetour.Manipulator(il, Info);
}
