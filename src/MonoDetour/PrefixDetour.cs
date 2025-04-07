using MonoMod.Cil;

namespace MonoDetour;

/// <summary>
/// Implements MonoDetour support for a Hook that will run at the start of the target method.
/// </summary>
public class PrefixDetour : IMonoDetourHookEmitter
{
    /// <inheritdoc/>
    public MonoDetourInfo Info { get; set; } = null!;

    /// <inheritdoc/>
    public void ILHookManipulator(ILContext il) => GenericDetour.Manipulator(il, Info);
}
